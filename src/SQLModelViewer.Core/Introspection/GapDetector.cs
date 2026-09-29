using SQLModelViewer.Core.Domain;

namespace SQLModelViewer.Core.Introspection;

/// <summary>
/// Best-effort, heuristic equivalent of a hand-curated "open items" list: flags self-referencing
/// FKs as informational notes, and flags FK-shaped columns (e.g. "vestigingid") that have no
/// resolved foreign key constraint but a same-named table exists. This is a heuristic, not ground
/// truth — false negatives (missed relationships) and occasional false positives are expected.
/// </summary>
public static class GapDetector
{
    public static IReadOnlyList<UnresolvedReference> Detect(IReadOnlyList<Table> tables, IReadOnlyList<ForeignKey> foreignKeys)
    {
        var gaps = new List<UnresolvedReference>();

        foreach (var fk in foreignKeys.Where(f => f.IsSelfReferencing))
        {
            var cols = string.Join(", ", fk.Columns.Select(c => c.ChildColumn));
            gaps.Add(new UnresolvedReference(
                fk.ChildSchema, fk.ChildTable, cols, GapReason.SelfReferencing,
                $"{fk.ChildSchema}.{fk.ChildTable}.{cols} references its own table via '{fk.ConstraintName}' — verify this is an intentional hierarchy/self-link."));
        }

        var tableNamesByCandidate = tables
            .Select(t => t.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(n => n, n => n, StringComparer.OrdinalIgnoreCase);
        var resolvedChildColumns = foreignKeys
            .SelectMany(fk => fk.Columns.Select(c => (fk.ChildSchema, fk.ChildTable, c.ChildColumn)))
            .ToHashSet(FkColumnComparer.Instance);

        foreach (var table in tables)
        {
            foreach (var column in table.Columns)
            {
                if (table.PrimaryKeyColumns.Contains(column.Name, StringComparer.OrdinalIgnoreCase))
                    continue;
                if (resolvedChildColumns.Contains((table.Schema, table.Name, column.Name)))
                    continue;

                var prefix = ExtractFkLikePrefix(column.Name);
                if (prefix is null)
                    continue;

                var matchedTable = NameCandidates(prefix)
                    .Select(c => tableNamesByCandidate.GetValueOrDefault(c))
                    .FirstOrDefault(t => t is not null);
                if (matchedTable is null)
                    continue;
                // A column named after its own table (e.g. Person.PersonId) is a near-universal
                // PK/identity naming convention, not a dangling reference to itself — skip it,
                // independent of whether it happens to be flagged as the PK column.
                if (string.Equals(matchedTable, table.Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                gaps.Add(new UnresolvedReference(
                    table.Schema, table.Name, column.Name, GapReason.NameLooksLikeFkNoConstraintFound,
                    $"{table.Schema}.{table.Name}.{column.Name} looks like a reference to '{matchedTable}' but no foreign key constraint links them — verify manually."));
            }
        }

        return gaps;
    }

    /// <summary>
    /// FK-shaped columns are conventionally singular ("PostId") while the referenced table is
    /// often plural ("Posts") — try the exact name plus a few common English pluralizations
    /// before giving up. Deliberately simple; not a general NLP solution.
    /// </summary>
    private static IEnumerable<string> NameCandidates(string prefix)
    {
        yield return prefix;
        yield return prefix + "s";
        yield return prefix + "es";
        if (prefix.Length > 1 && prefix.EndsWith('y') && !IsVowel(prefix[^2]))
            yield return prefix[..^1] + "ies";
    }

    private static bool IsVowel(char c) => "aeiouAEIOU".IndexOf(c) >= 0;

    private static string? ExtractFkLikePrefix(string columnName)
    {
        if (columnName.Length <= 2 || !columnName.EndsWith("id", StringComparison.OrdinalIgnoreCase))
            return null;
        var prefix = columnName[..^2];
        if (prefix.EndsWith('_'))
            prefix = prefix[..^1];
        return prefix.Length == 0 ? null : prefix;
    }

    private sealed class FkColumnComparer : IEqualityComparer<(string Schema, string Table, string Column)>
    {
        public static readonly FkColumnComparer Instance = new();

        public bool Equals((string Schema, string Table, string Column) x, (string Schema, string Table, string Column) y) =>
            string.Equals(x.Schema, y.Schema, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Table, y.Table, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Column, y.Column, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Schema, string Table, string Column) obj) =>
            HashCode.Combine(obj.Schema.ToLowerInvariant(), obj.Table.ToLowerInvariant(), obj.Column.ToLowerInvariant());
    }
}
