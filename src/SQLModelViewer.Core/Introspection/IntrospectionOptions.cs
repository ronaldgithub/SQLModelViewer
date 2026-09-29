namespace SQLModelViewer.Core.Introspection;

public sealed record IntrospectionOptions
{
    /// <summary>
    /// Tables to include, as (Schema, Table) pairs. Null or empty means "all non-system tables".
    /// The app's schema-tree UI and the CLI's --tables/--schemas flags both funnel into this.
    /// </summary>
    public IReadOnlySet<(string Schema, string Table)>? TableFilter { get; init; }

    /// <summary>
    /// Row counts require VIEW DATABASE STATE and an extra query per table set; opt-in.
    /// </summary>
    public bool IncludeRowCounts { get; init; }

    public int CommandTimeoutSeconds { get; init; } = 30;
}
