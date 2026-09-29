using System.Reflection;

namespace SQLModelViewer.Core.Introspection;

internal static class SqlScriptLoader
{
    private static readonly Assembly ResourceAssembly = typeof(SqlScriptLoader).Assembly;

    public static string Load(string fileName)
    {
        var resourceName = $"SQLModelViewer.Core.Introspection.SqlScripts.{fileName}";
        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded SQL script not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
