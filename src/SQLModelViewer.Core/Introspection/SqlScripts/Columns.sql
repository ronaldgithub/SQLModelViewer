-- @objectIds: comma-delimited list of sys.tables.object_id to fetch columns for
SELECT
    c.object_id                                            AS ObjectId,
    c.column_id                                             AS ColumnId,
    c.name                                                   AS ColumnName,
    c.column_id                                             AS OrdinalPosition,
    ty.name                                                  AS SqlTypeName,
    CASE
        WHEN ty.name IN (N'nvarchar', N'nchar') AND c.max_length <> -1 THEN c.max_length / 2
        ELSE c.max_length
    END                                                      AS MaxLength,
    c.precision                                             AS Precision,
    c.scale                                                  AS Scale,
    c.is_nullable                                           AS IsNullable,
    c.is_identity                                           AS IsIdentity,
    c.is_computed                                           AS IsComputed
FROM sys.columns c
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE c.object_id IN (SELECT CAST(value AS int) FROM STRING_SPLIT(@objectIds, N','))
ORDER BY c.object_id, c.column_id;
