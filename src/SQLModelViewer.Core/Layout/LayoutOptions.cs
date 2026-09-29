namespace SQLModelViewer.Core.Layout;

public sealed record LayoutOptions
{
    /// <summary>Must match the template's SVG card width (const W in diagram.template.html).</summary>
    public double CardWidth { get; init; } = 260;
    public double HeaderHeight { get; init; } = 34;
    public double RowHeight { get; init; } = 22;
    public double CardBottomPadding { get; init; } = 6;
    public double LayerGutter { get; init; } = 120;
    public double RowGutter { get; init; } = 40;
    public double ComponentGutter { get; init; } = 200;
    public int BarycenterPasses { get; init; } = 2;
}
