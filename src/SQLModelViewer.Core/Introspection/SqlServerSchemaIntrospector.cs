using Microsoft.Data.SqlClient;
using SQLModelViewer.Core.Domain;

namespace SQLModelViewer.Core.Introspection;

public sealed class SqlServerSchemaIntrospector : ISchemaIntrospector
{
    public async Task<SchemaModel> IntrospectAsync(SqlConnection connection, IntrospectionOptions options, CancellationToken cancellationToken = default)
    {
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var tableFilterText = BuildTableFilter(options.TableFilter);
        var tables = await QueryTablesAsync(connection, tableFilterText, options.CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false);

        if (tables.Count == 0)
        {
            return new SchemaModel(connection.DataSource, connection.Database, DateTime.UtcNow, [], [], []);
        }

        var objectIdsText = string.Join(",", tables.Select(t => t.ObjectId));

        var columns = await QueryColumnsAsync(connection, objectIdsText, options.CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false);
        var primaryKeys = await QueryPrimaryKeysAsync(connection, objectIdsText, options.CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false);
        var foreignKeys = await QueryForeignKeysAsync(connection, objectIdsText, options.CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false);
        var descriptions = await QueryExtendedPropertiesAsync(connection, objectIdsText, options.CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false);
        var rowCounts = options.IncludeRowCounts
            ? await QueryRowCountsAsync(connection, objectIdsText, options.CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false)
            : [];

        var assembled = SchemaAssembler.Assemble(tables, columns, primaryKeys, foreignKeys, descriptions, rowCounts);
        var gaps = GapDetector.Detect(assembled.Tables, assembled.ForeignKeys);

        return new SchemaModel(connection.DataSource, connection.Database, DateTime.UtcNow, assembled.Tables, assembled.ForeignKeys, gaps);
    }

    public async Task<IReadOnlyList<(string Schema, string Table)>> ListTablesAsync(SqlConnection connection, CancellationToken cancellationToken = default)
    {
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var tables = await QueryTablesAsync(connection, string.Empty, 30, cancellationToken).ConfigureAwait(false);
        return tables.Select(t => (t.SchemaName, t.TableName)).ToList();
    }

    private static string BuildTableFilter(IReadOnlySet<(string Schema, string Table)>? filter)
    {
        if (filter is null || filter.Count == 0)
            return string.Empty;
        return string.Join("|", filter.Select(f => $"{f.Schema}.{f.Table}"));
    }

    private static async Task<List<TableRow>> QueryTablesAsync(SqlConnection connection, string tableFilter, int timeout, CancellationToken ct)
    {
        using var cmd = new SqlCommand(SqlScriptLoader.Load("Tables.sql"), connection) { CommandTimeout = timeout };
        cmd.Parameters.AddWithValue("@tableFilter", tableFilter);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<TableRow>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new TableRow(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }
        return results;
    }

    private static async Task<List<ColumnRow>> QueryColumnsAsync(SqlConnection connection, string objectIds, int timeout, CancellationToken ct)
    {
        using var cmd = new SqlCommand(SqlScriptLoader.Load("Columns.sql"), connection) { CommandTimeout = timeout };
        cmd.Parameters.AddWithValue("@objectIds", objectIds);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<ColumnRow>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new ColumnRow(
                ObjectId: reader.GetInt32(0),
                ColumnId: reader.GetInt32(1),
                ColumnName: reader.GetString(2),
                OrdinalPosition: reader.GetInt32(3),
                SqlTypeName: reader.GetString(4),
                MaxLength: reader.IsDBNull(5) ? null : reader.GetInt32(5),
                Precision: reader.IsDBNull(6) ? null : (int)reader.GetByte(6),
                Scale: reader.IsDBNull(7) ? null : (int)reader.GetByte(7),
                IsNullable: reader.GetBoolean(8),
                IsIdentity: reader.GetBoolean(9),
                IsComputed: reader.GetBoolean(10)));
        }
        return results;
    }

    private static async Task<List<PrimaryKeyRow>> QueryPrimaryKeysAsync(SqlConnection connection, string objectIds, int timeout, CancellationToken ct)
    {
        using var cmd = new SqlCommand(SqlScriptLoader.Load("PrimaryKeys.sql"), connection) { CommandTimeout = timeout };
        cmd.Parameters.AddWithValue("@objectIds", objectIds);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<PrimaryKeyRow>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new PrimaryKeyRow(reader.GetInt32(0), reader.GetString(1), (int)reader.GetByte(2)));
        }
        return results;
    }

    private static async Task<List<ForeignKeyRow>> QueryForeignKeysAsync(SqlConnection connection, string objectIds, int timeout, CancellationToken ct)
    {
        using var cmd = new SqlCommand(SqlScriptLoader.Load("ForeignKeys.sql"), connection) { CommandTimeout = timeout };
        cmd.Parameters.AddWithValue("@objectIds", objectIds);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<ForeignKeyRow>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new ForeignKeyRow(
                FkObjectId: reader.GetInt32(0),
                ConstraintName: reader.GetString(1),
                ChildObjectId: reader.GetInt32(2),
                ParentObjectId: reader.GetInt32(3),
                ColumnOrdinal: reader.GetInt32(4),
                ChildColumn: reader.GetString(5),
                ParentColumn: reader.GetString(6),
                ChildSchema: reader.GetString(7),
                ChildTable: reader.GetString(8),
                ParentSchema: reader.GetString(9),
                ParentTable: reader.GetString(10)));
        }
        return results;
    }

    private static async Task<List<ExtendedPropertyRow>> QueryExtendedPropertiesAsync(SqlConnection connection, string objectIds, int timeout, CancellationToken ct)
    {
        using var cmd = new SqlCommand(SqlScriptLoader.Load("ExtendedProperties.sql"), connection) { CommandTimeout = timeout };
        cmd.Parameters.AddWithValue("@objectIds", objectIds);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<ExtendedPropertyRow>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new ExtendedPropertyRow(reader.GetInt32(0), reader.GetInt32(1), reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
        }
        return results;
    }

    private static async Task<List<RowCountRow>> QueryRowCountsAsync(SqlConnection connection, string objectIds, int timeout, CancellationToken ct)
    {
        using var cmd = new SqlCommand(SqlScriptLoader.Load("RowCounts.sql"), connection) { CommandTimeout = timeout };
        cmd.Parameters.AddWithValue("@objectIds", objectIds);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<RowCountRow>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new RowCountRow(reader.GetInt32(0), reader.GetInt64(1)));
        }
        return results;
    }
}
