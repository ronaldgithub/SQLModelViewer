# SQLModelViewer

An interactive, auto-generated entity-relationship diagram viewer for SQL Server databases —
built to replace SSMS's database diagram tool, which is slow, doesn't scale past a handful of
tables, and has no search, filtering, or relationship highlighting.

Point it at a database, pick the tables you care about, and it generates a single self-contained
HTML file you can open in any browser or send to a colleague — no viewer app required to *look*
at the result, even though generating it does need this tool.

![Screenshot placeholder — real screenshots coming in pictures/](pictures/screenshot-main.png)

## Features

- **Auto schema discovery** — tables, columns, primary/foreign keys, and `MS_Description`
  documentation read directly from SQL Server's catalog views. No manual curation.
- **Auto layout** — a layered graph algorithm places tables by FK dependency depth and clusters
  them by schema, handling cycles, self-references, and disconnected components automatically.
- **Pan, zoom, drag** — drag tables to reposition them (saved per-database in your browser), scroll
  to zoom, drag the background to pan, "Fit" and "Reset layout" buttons.
- **Search** — find a table, or a specific `table.column`, instantly.
- **Click to highlight** — click a table to highlight its direct relationships and dim everything
  else.
- **Isolate mode** — show only a table and its N-hop neighbors, so you're never staring at
  whole-database spaghetti just to understand one corner of the schema.
- **Schema legend** — filter the diagram by schema, color-coded automatically.
- **Column detail** — data type, nullability, identity, and description shown per column.
- **Row counts** — optional, shown per table when enabled (requires `VIEW DATABASE STATE`).
- **Auto-detected data notes** — self-referencing FKs and FK-shaped columns with no matching
  constraint are flagged automatically (best-effort heuristic, not ground truth).
- **Light/dark theme**, keyboard navigation, responsive layout.

## Download

> **Status:** the command-line generator (`SQLModelViewer.Cli`) is done and has been validated
> end-to-end against real SQL Server databases. The planned Avalonia desktop app (connection
> dialog, schema-tree picker, embedded live preview) is the next phase and isn't built yet — for
> now, generate diagrams with the CLI below.

Download the latest self-contained win-x64 build from the
[Releases page](https://github.com/ronaldgithub/SQLModelViewer/releases) (no .NET runtime install
required). Unzip and run `SQLModelViewer.Cli.exe` from a terminal.

## Quick start

```
SQLModelViewer.Cli.exe --connection "Server=.;Database=YourDb;Trusted_Connection=True;TrustServerCertificate=True" --schemas dbo --row-counts --out diagram.html
```

Then open `diagram.html` in your browser.

```
Options:
  --connection <string>   Required. ADO.NET SQL Server connection string.
  --out <path>            Required. Output HTML file path.
  --tables <list>         Comma-separated schema.table list to include. Default: all tables.
  --schemas <list>        Comma-separated schema names to include (all tables within). Combines with --tables.
  --row-counts            Include row counts (requires VIEW DATABASE STATE).
```

Tip: generating a diagram for an entire large database at once produces the same unreadable
spaghetti SSMS gives you — use `--schemas`/`--tables` to scope it to what you're actually working
on, then use isolate mode in the generated diagram to drill in further.

## Building from source

Prerequisites: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (the repo's
`global.json` pins `8.0.206`).

```
dotnet build
dotnet run --project src/SQLModelViewer.Cli -- --connection "..." --out diagram.html
```

## Running tests

```
dotnet test
```

## Architecture

See [CLAUDE.md](CLAUDE.md) for a full orientation (solution layout, data flow, key classes). In
short: `SQLModelViewer.Core` does schema introspection, auto-layout, and HTML generation as a
UI-free, fully unit-tested library; `SQLModelViewer.Cli` is a thin headless wrapper around it.

## Known limitations

- **Windows only.** The planned GUI depends on WebView2; the CLI itself is cross-platform-capable
  but is only built/tested for win-x64 releases today.
- **SQL Server only.** No plans to support other database vendors.
- **Read-only.** SQLModelViewer never modifies your database or writes back descriptions — it only
  reads schema metadata.
- The auto-detected "data notes" (self-refs, FK-shaped columns without a constraint) are a
  best-effort heuristic, not a guarantee — always verify manually.

## License

See [LICENSE](LICENSE).
