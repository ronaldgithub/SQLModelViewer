namespace SQLModelViewer.Core.Introspection;

// One record per catalog-view query result row (see SqlScripts/*.sql). Kept separate from the
// Domain model so SchemaAssembler's mapping logic can be unit-tested with plain in-memory rows,
// without needing a real SqlConnection/DbDataReader.

public sealed record TableRow(int ObjectId, string SchemaName, string TableName);

public sealed record ColumnRow(
    int ObjectId,
    int ColumnId,
    string ColumnName,
    int OrdinalPosition,
    string SqlTypeName,
    int? MaxLength,
    int? Precision,
    int? Scale,
    bool IsNullable,
    bool IsIdentity,
    bool IsComputed);

public sealed record PrimaryKeyRow(int ObjectId, string ColumnName, int KeyOrdinal);

public sealed record ForeignKeyRow(
    int FkObjectId,
    string ConstraintName,
    int ChildObjectId,
    int ParentObjectId,
    int ColumnOrdinal,
    string ChildColumn,
    string ParentColumn,
    string ChildSchema,
    string ChildTable,
    string ParentSchema,
    string ParentTable);

public sealed record ExtendedPropertyRow(int ObjectId, int ColumnId, string Description);

public sealed record RowCountRow(int ObjectId, long RowCount);
