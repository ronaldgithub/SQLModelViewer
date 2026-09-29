namespace SQLModelViewer.Core.Domain;

public sealed record SchemaModel(
    string ServerName,
    string DatabaseName,
    DateTime GeneratedAtUtc,
    IReadOnlyList<Table> Tables,
    IReadOnlyList<ForeignKey> ForeignKeys,
    IReadOnlyList<UnresolvedReference> Gaps);
