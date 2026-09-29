namespace SQLModelViewer.Core.Layout;

public sealed record TablePosition(string Schema, string Name, double X, double Y);

/// <summary>
/// BackEdges holds (childKey, parentKey) pairs — "schema.table" — that were excluded from the
/// acyclic layering computation (see LayeredLayoutEngine/CycleBreaker) but should still be drawn.
/// </summary>
public sealed record LayoutResult(IReadOnlyList<TablePosition> Positions, IReadOnlySet<(string From, string To)> BackEdges);
