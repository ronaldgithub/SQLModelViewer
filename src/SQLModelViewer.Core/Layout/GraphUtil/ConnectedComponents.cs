namespace SQLModelViewer.Core.Layout.GraphUtil;

public static class ConnectedComponents
{
    public static List<List<string>> Find(IReadOnlyCollection<string> nodes, IReadOnlyCollection<(string From, string To)> edges)
    {
        var adjacency = nodes.ToDictionary(n => n, _ => new List<string>());
        foreach (var (from, to) in edges)
        {
            if (!adjacency.ContainsKey(from) || !adjacency.ContainsKey(to))
                continue;
            adjacency[from].Add(to);
            adjacency[to].Add(from);
        }

        var visited = new HashSet<string>();
        var components = new List<List<string>>();
        foreach (var node in nodes)
        {
            if (!visited.Add(node))
                continue;

            var component = new List<string>();
            var stack = new Stack<string>();
            stack.Push(node);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                component.Add(current);
                foreach (var neighbor in adjacency[current])
                {
                    if (visited.Add(neighbor))
                        stack.Push(neighbor);
                }
            }
            components.Add(component);
        }
        return components;
    }
}
