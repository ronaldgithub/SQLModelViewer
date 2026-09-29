namespace SQLModelViewer.Core.Rendering;

public sealed record ColumnDto(
    string Name,
    string TypeLabel,
    bool Nullable,
    bool IsIdentity,
    bool IsPk,
    bool IsFk,
    string? Description);

public sealed record TableDto(
    string Key,
    string Schema,
    string Name,
    double X,
    double Y,
    string? Description,
    long? RowCount,
    IReadOnlyList<ColumnDto> Columns);

public sealed record ForeignKeyColumnPairDto(string ChildColumn, string ParentColumn);

public sealed record ForeignKeyDto(
    string ConstraintName,
    string Child,
    string Parent,
    IReadOnlyList<ForeignKeyColumnPairDto> Columns,
    bool IsSelfReferencing,
    bool IsBackEdge);

/// <summary>Hue is a 0-359 degree value; the client computes light/dark HSL strings from it.</summary>
public sealed record SchemaGroupDto(string Key, string Label, double Hue, int TableCount);

public sealed record GapDto(string Table, string Column, string Reason, string Message);

public sealed record DiagramViewModel(
    string ServerName,
    string DatabaseName,
    DateTime GeneratedAtUtc,
    IReadOnlyList<SchemaGroupDto> Schemas,
    IReadOnlyList<TableDto> Tables,
    IReadOnlyList<ForeignKeyDto> ForeignKeys,
    IReadOnlyList<GapDto> Gaps);
