# CLAUDE.md

Orientation for Claude Code (or any future contributor) picking this repo up cold.

## What this repo is

SQLModelViewer connects to a SQL Server database, introspects its real schema (tables, columns,
primary/foreign keys, `MS_Description` documentation, optionally row counts), automatically lays
out the resulting relationship graph, and generates a single self-contained interactive HTML file
to explore it — pan/zoom, drag-to-reposition, search by table or column, click-to-highlight
relationships, filter by schema, isolate a table's N-hop neighborhood, and drill into a table's
columns/PK/FK/description in a detail panel. The goal is to replace SSMS's database diagram tool,
which is slow to use, doesn't scale past a handful of tables, and has no search or filtering.

[example/topdesk_erd.html](example/topdesk_erd.html) is the original hand-crafted proof of what
"good" looks like — every interaction the generated HTML supports traces back to this file. It
stays in the repo as the canonical design reference; **do not delete it.**

## Solution layout

```
src/
  SQLModelViewer.Core/     No Avalonia/UI references — this is an invariant, not a preference.
                            Fully unit-testable in isolation. Owns: schema introspection (SQL
                            Server catalog-view queries → immutable Domain model), the auto-layout
                            algorithm, and HTML generation (embedded-resource template + JSON
                            payload substitution).
  SQLModelViewer.App/       Avalonia desktop UI (connection panel, schema-tree picker, embedded
                            WebView2 preview). Thin — it only gathers inputs, calls into Core, and
                            displays/exports the result. Targets net8.0-windows (not plain net8.0)
                            since it's Windows-only by design and uses the Windows registry
                            directly — see Conventions below.
  SQLModelViewer.Cli/       Headless test harness: `dotnet run --project src/SQLModelViewer.Cli --
                            --connection "..." --out diagram.html`. Exercises the exact same Core
                            pipeline the App will use, without needing Avalonia/WebView2 — this is
                            how Core changes get end-to-end-verified against a real database during
                            development, and it's also a legitimate standalone tool in its own right
                            for scripted/CI regeneration.
tests/
  SQLModelViewer.Core.Tests/  xUnit + FluentAssertions. Mirrors Core's folder structure.
```

## Key classes and where logic lives

- `Introspection/SqlServerSchemaIntrospector.cs` — runs the catalog-view queries in
  `Introspection/SqlScripts/*.sql` (embedded resources) against a `SqlConnection` and maps raw rows
  to the `Domain` model via `SchemaAssembler`. `ListTablesAsync` is a lightweight schema+name-only
  query used to populate a table picker before running full introspection on the chosen subset.
- `Introspection/GapDetector.cs` — heuristic, best-effort "data notes": flags self-referencing FKs,
  and FK-shaped columns (`XyzId`) with no resolved constraint but a same/pluralized-named table.
  **Deliberately simple** — it does not do NLP-style prefix stripping (e.g. `OwnerUserId` → `Users`
  is intentionally not caught; see the test `Detect_does_not_flag_role_prefixed_fk_shaped_columns`
  and the original example's own "Role-named keys ... were not scanned" caveat). Don't "fix" this
  without re-reading that test — it documents a scope boundary, not a bug.
- `Layout/LayeredLayoutEngine.cs` — Sugiyama-style layered layout: `GraphUtil/CycleBreaker.cs` finds
  a feedback-arc set via DFS so cyclic FK graphs don't break layering (back edges are excluded from
  depth computation but still rendered — the SVG edge-routing code already handles "parent left of
  child" geometrically, no special-casing needed); layers are assigned by longest-path/Kahn's
  algorithm (children left, referenced/parent tables right); `GraphUtil/ConnectedComponents.cs`
  packs disconnected subgraphs left-to-right, largest first; a barycenter pass reduces edge
  crossings within each layer. `LayoutOptions.CardWidth` **must stay in sync** with the `W` constant
  in the HTML template — they were out of sync once already (390 vs 260) and it silently shrank the
  visual gutter between layers without causing any overlap, so nothing failed loudly.
- `Rendering/HtmlDiagramGenerator.cs` + `Rendering/Templates/diagram.template.html` — the template is
  a real, hand-editable HTML/CSS/JS file (not generated from C#), directly openable in a browser
  (it has a `SAMPLE_DATA` fallback so `DATA = SAMPLE_DATA` renders something if the injection point
  is untouched). Generation is one `string.Replace` of the token `/*__DIAGRAM_DATA__*/` with a
  literal `DATA = {...json...};` statement — the token must stay on its own line directly after
  `let DATA = SAMPLE_DATA;` for that substitution to produce valid JS. `JsonPayloadBuilder.cs` maps
  the Domain model + layout positions into the `DiagramViewModel` DTOs that get JSON-serialized
  (camelCase) into the page; it also drops FKs that point outside the selected table set and turns
  them into `GapDto`s with `reason: "ExternalReference"` so they still surface as a note.
- App/`Services/WebView2AvailabilityChecker.cs` — Avalonia's `Avalonia.Controls.WebView` (the
  first-party AvaloniaUI package, not a third-party fork) doesn't expose a managed "is the WebView2
  runtime installed" check, so this probes the registry directly (`SOFTWARE\Microsoft\EdgeUpdate\
  Clients\{F3017226-...}`) the way Microsoft documents for native apps that don't want the full
  WebView2 SDK as a dependency. `PreviewViewModel.ShowWebView` (= `IsWebViewAvailable && HasDiagram`)
  gates the `NativeWebView` control's visibility — it must stay collapsed, not just empty, until
  there's a diagram to show, because the native/hwnd-hosted control paints above ordinary Avalonia
  content regardless of declared z-order even with no `Source` set, which otherwise blanks out both
  the "no diagram yet" placeholder and the WebView2-unavailable fallback behind it.
- App/`ViewModels/MainWindowViewModel.cs` — orchestrates `ConnectionViewModel` (server/auth/database
  picker, recent-connections via `IConnectionProfileStore`), `SchemaTreeViewModel` (checkbox tree
  over `ListTablesAsync`'s lightweight table listing, tri-state schema-level cascading, and filters
  out `sysdiagrams` — an SSMS-internal artifact table, never a real user table), and
  `PreviewViewModel`. `GenerateAsync` is the same Introspect → Layout → Generate pipeline the CLI
  runs, just invoked from the UI thread with status updates instead of console output.
- App/`Views/ConnectionDialog.axaml(.cs)` — the connection UI (server/auth/database picker, recent
  connections) lives in a modal dialog opened from a "Connect…" button in the sidebar, not inline —
  this was a deliberate change from the first cut of the UI, which embedded it directly in the
  sidebar and ate too much vertical space that the table tree needed. It shares the same
  `ConnectionViewModel` instance as the main window (passed in as `DataContext`), so property
  changes make during the dialog (e.g. `SelectedDatabase`) are already visible to
  `MainWindowViewModel`'s existing `PropertyChanged` subscription before the dialog even closes —
  no extra plumbing needed. Picking a database (by hand or via a recent-connection auto-reconnect)
  closes the dialog immediately; there's no separate "OK" button for that step.

## Data flow

```
SqlConnection → SqlServerSchemaIntrospector → SchemaModel (Domain)
              → LayeredLayoutEngine → LayoutResult
              → JsonPayloadBuilder → DiagramViewModel
              → HtmlDiagramGenerator → HTML string
              → HtmlFileWriter (disk) | Avalonia WebView2 preview | CLI stdout path
```

## Build / run / test

```
dotnet build
dotnet test
dotnet run --project src/SQLModelViewer.App
dotnet run --project src/SQLModelViewer.Cli -- --connection "Server=.;Database=Northwind;Trusted_Connection=True;TrustServerCertificate=True" --schemas dbo --row-counts --out diagram.html
```

`global.json` pins the SDK to `8.0.206` — running `dotnet new`/`dotnet sln` commands without that
pin active (e.g. a bare shell outside the repo root) can silently produce a newer `.slnx` solution
format instead of the classic `.sln`; this happened once during initial scaffolding.

`<InvariantGlobalization>` must **not** be enabled in `Directory.Build.props` — it was tried once
and breaks `Microsoft.Data.SqlClient` connections outright ("Globalization Invariant Mode is not
supported") because SqlClient needs ICU for collation-aware comparisons.

This repo has a `sqlserver` MCP tool available to Claude Code sessions for *Claude's own* ad-hoc
schema exploration/query-running during development — that is a separate mechanism from the app's
own runtime `Microsoft.Data.SqlClient` connections and does not share credentials or code with it.

## Conventions

- Domain model types (`Table`, `Column`, `ForeignKey`, `SchemaModel`, ...) are immutable `record`s.
- `SQLModelViewer.Core` never references Avalonia. If a change seems to need that, the logic belongs
  in `SQLModelViewer.App` instead, calling into Core's existing public surface.
- SQL Server catalog-view gotchas worth remembering (all hit during initial development):
  `sys.index_columns.key_ordinal` is `tinyint`, not `int`; `RowCount` is a reserved T-SQL keyword
  and must be bracketed (`[RowCount]`) as a column alias; a `CASE` expression mixing a `smallint`
  column with an `int` literal (e.g. `max_length / 2`) gets promoted to `int` as its overall result
  type, even though the untouched `ELSE` branch is `smallint`.
- Nullable reference types are enabled solution-wide via `Directory.Build.props`.
- New catalog queries go in `Introspection/SqlScripts/*.sql` as embedded resources loaded by
  `SqlScriptLoader`, not inline strings in C#.

## Where the HTML template lives and how to iterate on it

`src/SQLModelViewer.Core/Rendering/Templates/diagram.template.html` — open it directly in a browser
to iterate on CSS/JS without running the app or CLI; it renders a small built-in 3-table sample
dataset (`SAMPLE_DATA`) when the injection point hasn't been substituted. Before the Avalonia App
existed, the fastest loop for testing a real generated file's interactivity was: run the CLI
against a real database, then either open the output in a real browser or drive it headlessly with
`jsdom` (`npm install jsdom` in a scratch dir) to catch runtime errors without a GUI — this caught
several real bugs (a missing `DATA =` assignment, `CSS.escape`/`scrollIntoView` calls that jsdom
doesn't implement but real Chromium/WebView2 does) faster than eyeballing generated markup. That
approach is still useful for quick template iteration even now that the App's embedded preview
exists.

## CI / release

`.github/workflows/ci.yml` builds and tests on every push/PR. `.github/workflows/release.yml`
publishes a self-contained win-x64 build on `v*.*.*` tags to GitHub Releases. **It currently only
publishes the CLI**, not the App — the release predates the App's completion. Add an
`SQLModelViewer.App` publish step (same `dotnet publish -r win-x64 --self-contained true
-p:PublishSingleFile=true` pattern) before the next release if the App should ship too.

Getting the release workflow green took two fix-forward tags after the first attempt (`v0.1.0`
failed on a CRLF/LF-dependent test regex breaking under the Windows runner's checkout, `v0.1.1`
then failed on missing `permissions: contents: write` for `GITHUB_TOKEN`) — `v0.1.2` is the first
tag that actually produced a release. Both fixes are already in place; mentioned here so a future
session doesn't have to rediscover them if a similar workflow change regresses one of them.

## Status (update this section as phases land)

- **Done**: `SQLModelViewer.Core` (introspection, gap detection, layout, HTML generation),
  `SQLModelViewer.Cli`, and `SQLModelViewer.App` (Avalonia desktop GUI — connection panel,
  schema-tree picker, embedded WebView2 preview, Save As/Open in Browser). All validated end-to-end
  against real SQL Server databases (including the `topdesk` database the original example was
  hand-built from — the auto `GapDetector` independently rediscovered the same two
  self-referencing-FK notes the human author had written by hand — and against `ContosoOrders`,
  which exercised the App's full connect → pick tables → generate → embedded-preview → drag →
  Open in Browser → Save As loop with a real, unconstrained FK-shaped column the `GapDetector`
  correctly flagged).
- The App is not yet part of the release workflow (see CI/release above) — only the CLI ships in
  GitHub Releases today.

## Explicit non-goals

- No multi-database-vendor support (SQL Server only — no provider abstraction layer).
- No Linux/macOS build target (Windows-only release; the App targets `net8.0-windows` and uses the
  Windows registry and WebView2 directly).
- No write-back of descriptions/model changes to the database — introspection is read-only by
  design; this was an explicit scope decision (bigger trust/permissions surface), not an oversight.
- No general NLP-style FK-name inference in `GapDetector` beyond simple English pluralization — see
  the note on that class above.
