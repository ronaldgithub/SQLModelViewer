namespace SQLModelViewer.App.Services;

public static class AppPaths
{
    private static readonly string RootDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SQLModelViewer");

    public static string ConnectionsFile => Path.Combine(RootDir, "connections.json");

    public static string TempHtmlDir => Path.Combine(RootDir, "generated");

    public static string NewTempHtmlPath(string databaseName)
    {
        Directory.CreateDirectory(TempHtmlDir);
        var safeName = string.Join("_", databaseName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(TempHtmlDir, $"{safeName}-{DateTime.Now:yyyyMMdd-HHmmss}.html");
    }
}
