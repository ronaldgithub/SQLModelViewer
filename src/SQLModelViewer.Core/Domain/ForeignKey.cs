namespace SQLModelViewer.Core.Domain;

public sealed record ForeignKeyColumnPair(string ChildColumn, string ParentColumn);

public sealed record ForeignKey(
    string ConstraintName,
    string ChildSchema,
    string ChildTable,
    string ParentSchema,
    string ParentTable,
    IReadOnlyList<ForeignKeyColumnPair> Columns)
{
    public bool IsSelfReferencing =>
        string.Equals(ChildSchema, ParentSchema, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(ChildTable, ParentTable, StringComparison.OrdinalIgnoreCase);
}
