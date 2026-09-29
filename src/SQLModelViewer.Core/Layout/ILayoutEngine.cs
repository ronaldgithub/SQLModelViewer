using SQLModelViewer.Core.Domain;

namespace SQLModelViewer.Core.Layout;

public interface ILayoutEngine
{
    LayoutResult Layout(SchemaModel model, LayoutOptions? options = null);
}
