namespace SQLModelViewer.Core.Layout.GraphUtil;

/// <summary>
/// Finds a feedback arc set via DFS back-edge classification: removing the returned edges from
/// the graph guarantees an acyclic result. Used to keep FK-cycles (common in real schemas, e.g.
/// reciprocal "created_by"/"owner" pairs) from breaking longest-path layering.
/// </summary>
public static class CycleBreaker
{
    public static HashSet<(string From, string To)> FindBackEdges(
        IReadOnlyCollection<string> nodes,
        IReadOnlyList<(string From, string To)> edges)
    {
        var adjacency = nodes.ToDictionary(n => n, _ => new List<string>());
        foreach (var (from, to) in edges)
        {
            if (adjacency.TryGetValue(from, out var list))
                list.Add(to);
        }

        var state = new Dictionary<string, byte>(); // absent = unvisited, 1 = on current DFS stack, 2 = finished
        var backEdges = new HashSet<(string From, string To)>();

        foreach (var node in nodes)
        {
            if (!state.ContainsKey(node))
                Visit(node);
        }

        return backEdges;

        void Visit(string node)
        {
            state[node] = 1;
            foreach (var next in adjacency[node])
            {
                if (!state.TryGetValue(next, out var s))
                {
                    Visit(next);
                }
                else if (s == 1)
                {
                    backEdges.Add((node, next));
                }
            }
            state[node] = 2;
        }
    }
}
