-- @objectIds: comma-delimited list of sys.tables.object_id
SELECT
    ic.object_id     AS ObjectId,
    c.name           AS ColumnName,
    ic.key_ordinal   AS KeyOrdinal
FROM sys.key_constraints kc
JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE kc.type = 'PK'
  AND kc.parent_object_id IN (SELECT CAST(value AS int) FROM STRING_SPLIT(@objectIds, N','))
ORDER BY ic.object_id, ic.key_ordinal;
