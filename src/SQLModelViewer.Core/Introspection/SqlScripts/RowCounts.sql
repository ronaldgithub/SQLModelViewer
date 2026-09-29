-- @objectIds: comma-delimited list of sys.tables.object_id
-- Requires VIEW DATABASE STATE permission; opt-in via IntrospectionOptions.IncludeRowCounts.
SELECT
    p.object_id    AS ObjectId,
    SUM(p.rows)    AS [RowCount]
FROM sys.partitions p
WHERE p.index_id IN (0, 1)
  AND p.object_id IN (SELECT CAST(value AS int) FROM STRING_SPLIT(@objectIds, N','))
GROUP BY p.object_id;
