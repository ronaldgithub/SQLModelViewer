namespace SQLModelViewer.Core.Export;

public static class HtmlFileWriter
{
    public static async Task WriteAsync(string html, string path, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(path, html, cancellationToken).ConfigureAwait(false);
    }
}
