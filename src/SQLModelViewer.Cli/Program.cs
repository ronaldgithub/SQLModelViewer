using Microsoft.Data.SqlClient;
using SQLModelViewer.Core.Export;
using SQLModelViewer.Core.Introspection;
using SQLModelViewer.Core.Layout;
using SQLModelViewer.Core.Rendering;

return await Cli.RunAsync(args);

internal static class Cli
{
    public static async Task<int> RunAsync(string[] args)
    {
        var options = ParseArgs(args);
        if (options is null)
        {
            PrintUsage();
            return 1;
        }

        if (options.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        try
        {
            await using var connection = new SqlConnection(options.ConnectionString);
            await connection.OpenAsync();

            ISchemaIntrospector introspector = new SqlServerSchemaIntrospector();

            IReadOnlySet<(string Schema, string Table)>? tableFilter = null;
            if (options.Tables.Count > 0 || options.Schemas.Count > 0)
            {
                var explicitTables = options.Tables
                    .Select(SplitQualifiedName)
                    .ToHashSet();

                if (options.Schemas.Count > 0)
                {
                    Console.Error.WriteLine($"Listing tables to resolve --schemas filter ({string.Join(",", options.Schemas)})...");
                    var allTables = await introspector.ListTablesAsync(connection);
                    foreach (var t in allTables.Where(t => options.Schemas.Contains(t.Schema, StringComparer.OrdinalIgnoreCase)))
                        explicitTables.Add(t);
                }

                tableFilter = explicitTables;
            }

            Console.Error.WriteLine("Introspecting schema...");
            var introspectionOptions = new IntrospectionOptions
            {
                TableFilter = tableFilter,
                IncludeRowCounts = options.IncludeRowCounts
            };
            var model = await introspector.IntrospectAsync(connection, introspectionOptions);
            Console.Error.WriteLine($"Found {model.Tables.Count} tables, {model.ForeignKeys.Count} foreign keys, {model.Gaps.Count} data notes.");

            Console.Error.WriteLine("Computing layout...");
            ILayoutEngine layoutEngine = new LayeredLayoutEngine();
            var layout = layoutEngine.Layout(model);

            Console.Error.WriteLine("Generating HTML...");
            IHtmlDiagramGenerator generator = new HtmlDiagramGenerator();
            var html = generator.Generate(model, layout);

            await HtmlFileWriter.WriteAsync(html, options.OutputPath);
            Console.Error.WriteLine($"Wrote {options.OutputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static (string Schema, string Table) SplitQualifiedName(string qualifiedName)
    {
        var parts = qualifiedName.Split('.', 2);
        if (parts.Length != 2)
            throw new ArgumentException($"'{qualifiedName}' is not a valid schema.table name.");
        return (parts[0], parts[1]);
    }

    private static CliOptions? ParseArgs(string[] args)
    {
        string? connectionString = null;
        string? outputPath = null;
        var tables = new List<string>();
        var schemas = new List<string>();
        var includeRowCounts = false;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--connection":
                    connectionString = RequireValue(args, ref i, "--connection");
                    break;
                case "--out":
                    outputPath = RequireValue(args, ref i, "--out");
                    break;
                case "--tables":
                    tables.AddRange(RequireValue(args, ref i, "--tables").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case "--schemas":
                    schemas.AddRange(RequireValue(args, ref i, "--schemas").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case "--row-counts":
                    includeRowCounts = true;
                    break;
                case "-h":
                case "--help":
                    showHelp = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    return null;
            }
        }

        if (showHelp)
            return new CliOptions("", "", [], [], false) { ShowHelp = true };

        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(outputPath))
        {
            Console.Error.WriteLine("--connection and --out are required.");
            return null;
        }

        return new CliOptions(connectionString, outputPath, tables, schemas, includeRowCounts);
    }

    private static string RequireValue(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"{flag} requires a value.");
        return args[++i];
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            SQLModelViewer CLI — headless HTML ERD generator, used for scripted regeneration and as a
            dev/test harness for SQLModelViewer.Core without the Avalonia UI.

            Usage:
              sqlmodelviewer-cli --connection "<connection string>" --out <path.html> [options]

            Options:
              --connection <string>   Required. ADO.NET SQL Server connection string.
              --out <path>            Required. Output HTML file path.
              --tables <list>         Comma-separated schema.table list to include. Default: all tables.
              --schemas <list>        Comma-separated schema names to include (all tables within). Combines with --tables.
              --row-counts            Include row counts (requires VIEW DATABASE STATE).
              -h, --help              Show this help.

            Example:
              dotnet run --project src/SQLModelViewer.Cli -- --connection "Server=.;Database=Northwind;Trusted_Connection=True;TrustServerCertificate=True" --schemas dbo --row-counts --out diagram.html
            """);
    }
}

internal sealed record CliOptions(string ConnectionString, string OutputPath, List<string> Tables, List<string> Schemas, bool IncludeRowCounts)
{
    public bool ShowHelp { get; init; }
}
