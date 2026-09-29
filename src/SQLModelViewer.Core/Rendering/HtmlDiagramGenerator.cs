using System.Text.Json;
using SQLModelViewer.Core.Domain;
using SQLModelViewer.Core.Layout;

namespace SQLModelViewer.Core.Rendering;

public interface IHtmlDiagramGenerator
{
    string Generate(SchemaModel model, LayoutResult layout);

    string GenerateFromViewModel(DiagramViewModel viewModel);
}

/// <summary>
/// Turns a SchemaModel + LayoutResult into a single self-contained HTML file: loads the
/// embedded-resource template and substitutes one JSON payload into its single injection point.
/// Pure/synchronous — no file I/O here (see Export/HtmlFileWriter for that).
/// </summary>
public sealed class HtmlDiagramGenerator : IHtmlDiagramGenerator
{
    private const string PlaceholderToken = "/*__DIAGRAM_DATA__*/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public string Generate(SchemaModel model, LayoutResult layout)
    {
        var viewModel = JsonPayloadBuilder.Build(model, layout);
        return GenerateFromViewModel(viewModel);
    }

    public string GenerateFromViewModel(DiagramViewModel viewModel)
    {
        var template = LoadTemplate();
        if (!template.Contains(PlaceholderToken, StringComparison.Ordinal))
            throw new InvalidOperationException($"Diagram template is missing the '{PlaceholderToken}' injection point.");

        var json = JsonSerializer.Serialize(viewModel, JsonOptions);
        return template.Replace(PlaceholderToken, $"DATA = {json};");
    }

    private static string LoadTemplate()
    {
        var assembly = typeof(HtmlDiagramGenerator).Assembly;
        const string resourceName = "SQLModelViewer.Core.Rendering.Templates.diagram.template.html";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded diagram template not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
