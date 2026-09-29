-- @objectIds: comma-delimited list of sys.tables.object_id
-- Table-level (minor_id = 0) and column-level (minor_id = column_id) MS_Description values.
SELECT
    ep.major_id  AS ObjectId,
    ep.minor_id  AS ColumnId,
    CAST(ep.value AS nvarchar(max)) AS Description
FROM sys.extended_properties ep
WHERE ep.class = 1
  AND ep.name = N'MS_Description'
  AND ep.major_id IN (SELECT CAST(value AS int) FROM STRING_SPLIT(@objectIds, N','));
