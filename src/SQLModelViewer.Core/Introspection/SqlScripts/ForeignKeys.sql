-- @objectIds: comma-delimited list of sys.tables.object_id — FKs whose *child* (referencing) table is in this set
SELECT
    fk.object_id              AS FkObjectId,
    fk.name                   AS ConstraintName,
    fk.parent_object_id       AS ChildObjectId,
    fk.referenced_object_id   AS ParentObjectId,
    fkc.constraint_column_id  AS ColumnOrdinal,
    cc.name                   AS ChildColumn,
    pc.name                   AS ParentColumn,
    cs.name                   AS ChildSchema,
    ctab.name                 AS ChildTable,
    ps.name                   AS ParentSchema,
    ptab.name                 AS ParentTable
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns cc ON cc.object_id = fkc.parent_object_id AND cc.column_id = fkc.parent_column_id
JOIN sys.columns pc ON pc.object_id = fkc.referenced_object_id AND pc.column_id = fkc.referenced_column_id
JOIN sys.tables ctab ON ctab.object_id = fk.parent_object_id
JOIN sys.schemas cs ON cs.schema_id = ctab.schema_id
JOIN sys.tables ptab ON ptab.object_id = fk.referenced_object_id
JOIN sys.schemas ps ON ps.schema_id = ptab.schema_id
WHERE fk.parent_object_id IN (SELECT CAST(value AS int) FROM STRING_SPLIT(@objectIds, N','))
ORDER BY fk.object_id, fkc.constraint_column_id;
