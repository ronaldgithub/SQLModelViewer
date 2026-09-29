using Microsoft.Data.SqlClient;
using SQLModelViewer.Core.Domain;

namespace SQLModelViewer.Core.Introspection;

public interface ISchemaIntrospector
{
    Task<SchemaModel> IntrospectAsync(SqlConnection connection, IntrospectionOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lightweight table listing (schema + name only, no columns/keys) for populating a schema-tree
    /// picker before running the full introspection those choices feed into.
    /// </summary>
    Task<IReadOnlyList<(string Schema, string Table)>> ListTablesAsync(SqlConnection connection, CancellationToken cancellationToken = default);
}
