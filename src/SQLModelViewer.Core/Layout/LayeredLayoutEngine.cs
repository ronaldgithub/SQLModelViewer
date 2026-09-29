using SQLModelViewer.Core.Domain;
using SQLModelViewer.Core.Layout.GraphUtil;

namespace SQLModelViewer.Core.Layout;

/// <summary>
/// Sugiyama-style layered layout: nodes are laid out left-to-right by FK dependency depth
/// (children/dependents left, referenced "parent"/lookup tables right), clustered by schema
/// within each layer, with a barycenter crossing-reduction pass. Cycles are broken via
/// <see cref="CycleBreaker"/> before layering (back edges are still drawn, just excluded from
/// the acyclic depth computation). Disconnected components are laid out independently and
/// packed left-to-right, largest first.
/// </summary>
public sealed class LayeredLayoutEngine : ILayoutEngine
{
    public LayoutResult Layout(SchemaModel model, LayoutOptions? options = null)
    {
        var opts = options ?? new LayoutOptions();
        var tables = model.Tables;
        if (tables.Count == 0)
            return new LayoutResult([], new HashSet<(string, string)>());

        var tableByKey = tables.ToDictionary(t => t.QualifiedName);
        var nodeKeys = tableByKey.Keys.ToList();

        var directedEdges = model.ForeignKeys
            .Where(fk => !fk.IsSelfReferencing)
            .Select(fk => (From: $"{fk.ChildSchema}.{fk.ChildTable}", To: $"{fk.ParentSchema}.{fk.ParentTable}"))
            .Where(e => tableByKey.ContainsKey(e.From) && tableByKey.ContainsKey(e.To) && e.From != e.To)
            .Distinct()
            .ToList();

        var backEdges = CycleBreaker.FindBackEdges(nodeKeys, directedEdges);
        var layoutEdges = directedEdges.Where(e => !backEdges.Contains(e)).ToList();

        var layer = ComputeLayers(nodeKeys, layoutEdges);
        var components = ConnectedComponents.Find(nodeKeys, layoutEdges)
            .OrderByDescending(c => c.Count)
            .ToList();

        var positions = new List<TablePosition>();
        double xCursor = 0;

        foreach (var component in components)
        {
            var maxLayer = component.Max(n => layer[n]);

            var byLayer = Enumerable.Range(0, maxLayer + 1).ToDictionary(
                l => l,
                l => component
                    .Where(n => layer[n] == l)
                    .OrderBy(n => tableByKey[n].Schema, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(n => tableByKey[n].Name, StringComparer.OrdinalIgnoreCase)
                    .ToList());

            ReduceCrossings(byLayer, maxLayer, layoutEdges, opts.BarycenterPasses);

            for (var l = 0; l <= maxLayer; l++)
            {
                double y = 0;
                foreach (var key in byLayer[l])
                {
                    var table = tableByKey[key];
                    positions.Add(new TablePosition(table.Schema, table.Name, xCursor + l * (opts.CardWidth + opts.LayerGutter), y));
                    y += CardHeight(table, opts) + opts.RowGutter;
                }
            }

            xCursor += (maxLayer + 1) * (opts.CardWidth + opts.LayerGutter) + opts.ComponentGutter;
        }

        return new LayoutResult(positions, backEdges);
    }

    private static double CardHeight(Table table, LayoutOptions opts) =>
        opts.HeaderHeight + table.Columns.Count * opts.RowHeight + opts.CardBottomPadding;

    private static Dictionary<string, int> ComputeLayers(List<string> nodeKeys, List<(string From, string To)> edges)
    {
        var layer = nodeKeys.ToDictionary(n => n, _ => 0);
        var inDegree = nodeKeys.ToDictionary(n => n, _ => 0);
        var adjacency = nodeKeys.ToDictionary(n => n, _ => new List<string>());
        foreach (var (from, to) in edges)
        {
            adjacency[from].Add(to);
            inDegree[to]++;
        }

        var queue = new Queue<string>(nodeKeys.Where(n => inDegree[n] == 0));
        var processed = new HashSet<string>();

        while (queue.Count > 0)
        {
            var u = queue.Dequeue();
            if (!processed.Add(u))
                continue;

            foreach (var v in adjacency[u])
            {
                layer[v] = Math.Max(layer[v], layer[u] + 1);
                inDegree[v]--;
                if (inDegree[v] == 0)
                    queue.Enqueue(v);
            }
        }

        // Defensive guard: a residual cycle CycleBreaker somehow missed would otherwise leave
        // nodes stuck at inDegree > 0 forever. Rather than hang, park them at layer 0.
        foreach (var n in nodeKeys)
        {
            if (!processed.Contains(n) && inDegree[n] > 0)
                layer[n] = 0;
        }

        return layer;
    }

    private static void ReduceCrossings(Dictionary<int, List<string>> byLayer, int maxLayer, List<(string From, string To)> edges, int passes)
    {
        var downEdges = new Dictionary<string, List<string>>(); // From -> [To] (next layer)
        var upEdges = new Dictionary<string, List<string>>();   // To -> [From] (previous layer)
        foreach (var (from, to) in edges)
        {
            AddEdge(downEdges, from, to);
            AddEdge(upEdges, to, from);
        }

        for (var pass = 0; pass < passes; pass++)
        {
            for (var l = 1; l <= maxLayer; l++)
                SortLayerByBarycenter(byLayer, l, l - 1, upEdges);

            for (var l = maxLayer - 1; l >= 0; l--)
                SortLayerByBarycenter(byLayer, l, l + 1, downEdges);
        }
    }

    private static void AddEdge(Dictionary<string, List<string>> map, string key, string value)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }
        list.Add(value);
    }

    private static void SortLayerByBarycenter(Dictionary<int, List<string>> byLayer, int layer, int adjacentLayer, Dictionary<string, List<string>> neighborsOf)
    {
        if (!byLayer.TryGetValue(adjacentLayer, out var adjacentNodes))
            return;

        var indexInAdjacent = adjacentNodes.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i);
        var nodes = byLayer[layer];
        var originalIndex = nodes.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i);

        byLayer[layer] = nodes
            .Select(n =>
            {
                var neighbors = neighborsOf.TryGetValue(n, out var list) ? list.Where(indexInAdjacent.ContainsKey).ToList() : [];
                double barycenter = neighbors.Count > 0 ? neighbors.Average(x => indexInAdjacent[x]) : originalIndex[n];
                return (n, barycenter);
            })
            .OrderBy(x => x.barycenter)
            .ThenBy(x => originalIndex[x.n])
            .Select(x => x.n)
            .ToList();
    }
}
