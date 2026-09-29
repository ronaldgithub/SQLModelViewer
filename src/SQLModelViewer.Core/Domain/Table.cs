namespace SQLModelViewer.Core.Domain;

public sealed record Table(
    string Schema,
    string Name,
    IReadOnlyList<Column> Columns,
    IReadOnlyList<string> PrimaryKeyColumns,
    long? RowCount,
    string? Description)
{
    public string QualifiedName => $"{Schema}.{Name}";
}
