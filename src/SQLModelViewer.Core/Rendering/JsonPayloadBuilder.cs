using SQLModelViewer.Core.Domain;
using SQLModelViewer.Core.Layout;

namespace SQLModelViewer.Core.Rendering;

public static class JsonPayloadBuilder
{
    /// <summary>Golden-angle hue rotation: gives well-distributed, non-clustering colors for any N.</summary>
    private const double GoldenAngle = 137.508;

    public static DiagramViewModel Build(SchemaModel model, LayoutResult layout)
    {
        var positionByKey = layout.Positions.ToDictionary(p => $"{p.Schema}.{p.Name}");

        var fkChildColumnsByTable = model.ForeignKeys
            .SelectMany(fk => fk.Columns.Select(c => (Key: $"{fk.ChildSchema}.{fk.ChildTable}", c.ChildColumn)))
            .ToLookup(x => x.Key, x => x.ChildColumn, StringComparer.OrdinalIgnoreCase);

        var tables = model.Tables
            .Where(t => positionByKey.ContainsKey(t.QualifiedName))
            .Select(t =>
            {
                var pos = positionByKey[t.QualifiedName];
                var fkCols = fkChildColumnsByTable[t.QualifiedName].ToHashSet(StringComparer.OrdinalIgnoreCase);
                var pkCols = t.PrimaryKeyColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var columns = t.Columns
                    .Select(c => new ColumnDto(c.Name, c.TypeLabel, c.IsNullable, c.IsIdentity, pkCols.Contains(c.Name), fkCols.Contains(c.Name), c.Description))
                    .ToList();
                return new TableDto(t.QualifiedName, t.Schema, t.Name, pos.X, pos.Y, t.Description, t.RowCount, columns);
            })
            .ToList();

        var includedKeys = tables.Select(t => t.Key).ToHashSet();

        var foreignKeys = model.ForeignKeys
            .Where(fk => includedKeys.Contains($"{fk.ChildSchema}.{fk.ChildTable}") && includedKeys.Contains($"{fk.ParentSchema}.{fk.ParentTable}"))
            .Select(fk =>
            {
                var childKey = $"{fk.ChildSchema}.{fk.ChildTable}";
                var parentKey = $"{fk.ParentSchema}.{fk.ParentTable}";
                return new ForeignKeyDto(
                    fk.ConstraintName,
                    childKey,
                    parentKey,
                    fk.Columns.Select(c => new ForeignKeyColumnPairDto(c.ChildColumn, c.ParentColumn)).ToList(),
                    fk.IsSelfReferencing,
                    layout.BackEdges.Contains((childKey, parentKey)));
            })
            .ToList();

        var schemaGroups = tables
            .GroupBy(t => t.Schema, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select((g, i) => new SchemaGroupDto(g.Key, g.Key, (i * GoldenAngle) % 360, g.Count()))
            .ToList();

        var gaps = model.Gaps
            .Select(g => new GapDto($"{g.TableSchema}.{g.TableName}", g.ColumnName, g.Reason.ToString(), g.Message))
            .ToList();

        var externalReferences = model.ForeignKeys
            .Where(fk => includedKeys.Contains($"{fk.ChildSchema}.{fk.ChildTable}") && !includedKeys.Contains($"{fk.ParentSchema}.{fk.ParentTable}"))
            .Select(fk => new GapDto(
                $"{fk.ChildSchema}.{fk.ChildTable}",
                string.Join(", ", fk.Columns.Select(c => c.ChildColumn)),
                "ExternalReference",
                $"{fk.ChildSchema}.{fk.ChildTable} references {fk.ParentSchema}.{fk.ParentTable} via '{fk.ConstraintName}', which is not included in this diagram."))
            .ToList();

        return new DiagramViewModel(
            model.ServerName,
            model.DatabaseName,
            model.GeneratedAtUtc,
            schemaGroups,
            tables,
            foreignKeys,
            [.. gaps, .. externalReferences]);
    }
}
