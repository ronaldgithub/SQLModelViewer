using SQLModelViewer.Core.Domain;

namespace SQLModelViewer.Core.Introspection;

public sealed record AssembledSchema(IReadOnlyList<Table> Tables, IReadOnlyList<ForeignKey> ForeignKeys);

/// <summary>
/// Pure mapping from raw catalog-view rows to the immutable Domain model. Kept free of ADO.NET
/// so it can be unit-tested with plain in-memory row lists.
/// </summary>
public static class SchemaAssembler
{
    public static AssembledSchema Assemble(
        IReadOnlyList<TableRow> tables,
        IReadOnlyList<ColumnRow> columns,
        IReadOnlyList<PrimaryKeyRow> primaryKeys,
        IReadOnlyList<ForeignKeyRow> foreignKeys,
        IReadOnlyList<ExtendedPropertyRow> descriptions,
        IReadOnlyList<RowCountRow> rowCounts)
    {
        var columnsByObject = columns.GroupBy(c => c.ObjectId).ToDictionary(g => g.Key, g => g.OrderBy(c => c.OrdinalPosition).ToList());
        var pkByObject = primaryKeys.GroupBy(p => p.ObjectId).ToDictionary(g => g.Key, g => g.OrderBy(p => p.KeyOrdinal).Select(p => p.ColumnName).ToList());
        var rowCountByObject = rowCounts.ToDictionary(r => r.ObjectId, r => r.RowCount);

        var tableDescByObject = descriptions.Where(d => d.ColumnId == 0).ToDictionary(d => d.ObjectId, d => d.Description);
        var columnDescByObjectAndColumn = descriptions.Where(d => d.ColumnId != 0).ToDictionary(d => (d.ObjectId, d.ColumnId), d => d.Description);

        var resultTables = new List<Table>(tables.Count);
        foreach (var t in tables)
        {
            var cols = columnsByObject.TryGetValue(t.ObjectId, out var colRows) ? colRows : new List<ColumnRow>();
            var mappedColumns = cols.Select(c => new Column(
                Name: c.ColumnName,
                OrdinalPosition: c.OrdinalPosition,
                SqlTypeName: c.SqlTypeName,
                MaxLength: c.MaxLength,
                Precision: c.Precision,
                Scale: c.Scale,
                IsNullable: c.IsNullable,
                IsIdentity: c.IsIdentity,
                IsComputed: c.IsComputed,
                Description: columnDescByObjectAndColumn.TryGetValue((t.ObjectId, c.ColumnId), out var cd) ? cd : null
            )).ToList();

            var pkCols = pkByObject.TryGetValue(t.ObjectId, out var pk) ? pk : new List<string>();
            var rowCount = rowCountByObject.TryGetValue(t.ObjectId, out var rc) ? rc : (long?)null;
            var tableDesc = tableDescByObject.TryGetValue(t.ObjectId, out var td) ? td : null;

            resultTables.Add(new Table(t.SchemaName, t.TableName, mappedColumns, pkCols, rowCount, tableDesc));
        }

        var resultForeignKeys = foreignKeys
            .GroupBy(f => f.FkObjectId)
            .Select(g =>
            {
                var first = g.OrderBy(f => f.ColumnOrdinal).First();
                var pairs = g.OrderBy(f => f.ColumnOrdinal)
                    .Select(f => new ForeignKeyColumnPair(f.ChildColumn, f.ParentColumn))
                    .ToList();
                return new ForeignKey(first.ConstraintName, first.ChildSchema, first.ChildTable, first.ParentSchema, first.ParentTable, pairs);
            })
            .ToList();

        return new AssembledSchema(resultTables, resultForeignKeys);
    }
}
