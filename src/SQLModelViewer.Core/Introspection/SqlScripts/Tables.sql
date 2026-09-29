-- @tableFilter: '|'-delimited list of 'schema.table' strings, or empty string for "no filter"
SELECT
    t.object_id     AS ObjectId,
    s.name          AS SchemaName,
    t.name          AS TableName
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0
  AND (@tableFilter = N'' OR CONCAT(s.name, N'.', t.name) IN (SELECT value FROM STRING_SPLIT(@tableFilter, N'|')))
ORDER BY s.name, t.name;
