using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.SqlClient;
using SQLModelViewer.App.Services;
using SQLModelViewer.Core.Export;
using SQLModelViewer.Core.Introspection;
using SQLModelViewer.Core.Layout;
using SQLModelViewer.Core.Rendering;

namespace SQLModelViewer.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ISchemaIntrospector introspector;
    private readonly ILayoutEngine layoutEngine;
    private readonly IHtmlDiagramGenerator htmlGenerator;

    public ConnectionViewModel Connection { get; }
    public SchemaTreeViewModel SchemaTree { get; }
    public PreviewViewModel Preview { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private bool isGenerating;

    [ObservableProperty]
    private string statusMessage = "Connect to a SQL Server to begin.";

    public MainWindowViewModel() : this(
        new ConnectionProfileStore(),
        new WebView2AvailabilityChecker(),
        new SqlServerSchemaIntrospector(),
        new LayeredLayoutEngine(),
        new HtmlDiagramGenerator())
    {
    }

    public MainWindowViewModel(
        IConnectionProfileStore profileStore,
        IWebViewAvailabilityChecker webViewChecker,
        ISchemaIntrospector introspector,
        ILayoutEngine layoutEngine,
        IHtmlDiagramGenerator htmlGenerator)
    {
        this.introspector = introspector;
        this.layoutEngine = layoutEngine;
        this.htmlGenerator = htmlGenerator;

        Connection = new ConnectionViewModel(profileStore);
        SchemaTree = new SchemaTreeViewModel();
        Preview = new PreviewViewModel(webViewChecker);

        Connection.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(ConnectionViewModel.SelectedDatabase) && Connection.SelectedDatabase != null)
                await LoadTablesAsync();
            GenerateCommand.NotifyCanExecuteChanged();
        };
        SchemaTree.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SchemaTreeViewModel.SelectedCount))
                GenerateCommand.NotifyCanExecuteChanged();
        };

        _ = Connection.LoadRecentConnectionsAsync();
    }

    private async Task LoadTablesAsync()
    {
        StatusMessage = $"Loading tables from {Connection.SelectedDatabase}...";
        try
        {
            await using var connection = new SqlConnection(Connection.BuildConnectionString());
            var tables = await introspector.ListTablesAsync(connection).ConfigureAwait(true);
            // sysdiagrams is an SSMS-internal support table (created the moment anyone opens the
            // old Database Diagrams tool), never a real user table — exclude it by default.
            var userTables = tables.Where(t => !t.Table.Equals("sysdiagrams", StringComparison.OrdinalIgnoreCase)).ToList();
            SchemaTree.Load(userTables);
            StatusMessage = $"{userTables.Count} tables found in {Connection.SelectedDatabase}. Select tables and click Generate.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load tables: {ex.Message}";
        }
    }

    private bool CanGenerate => !IsGenerating && SchemaTree.SelectedCount > 0 && Connection.SelectedDatabase != null;

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        IsGenerating = true;
        StatusMessage = "Introspecting schema...";
        try
        {
            await using var connection = new SqlConnection(Connection.BuildConnectionString());
            var options = new IntrospectionOptions { TableFilter = SchemaTree.SelectedTables };
            var model = await introspector.IntrospectAsync(connection, options).ConfigureAwait(true);

            StatusMessage = "Computing layout...";
            var layout = layoutEngine.Layout(model);

            StatusMessage = "Generating HTML...";
            var html = htmlGenerator.Generate(model, layout);

            var path = AppPaths.NewTempHtmlPath(Connection.SelectedDatabase!);
            await HtmlFileWriter.WriteAsync(html, path).ConfigureAwait(true);

            Preview.Show(path);
            await Connection.RememberCurrentAsync().ConfigureAwait(true);
            await Connection.LoadRecentConnectionsAsync().ConfigureAwait(true);

            StatusMessage = $"Generated {model.Tables.Count} tables, {model.ForeignKeys.Count} foreign keys, {model.Gaps.Count} data notes.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Generation failed: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private void OpenInBrowser()
    {
        if (Preview.CurrentFilePath is null)
            return;

        Process.Start(new ProcessStartInfo(Preview.CurrentFilePath) { UseShellExecute = true });
    }

    /// <summary>
    /// Copies the already-generated diagram to a user-chosen path. The file picker itself is a
    /// View concern (needs IStorageProvider from the TopLevel) — the View calls this once it has
    /// a destination path, keeping the actual file I/O here rather than in code-behind.
    /// </summary>
    public async Task SaveGeneratedFileAsAsync(string destinationPath)
    {
        if (Preview.CurrentFilePath is null)
            return;

        await using var source = File.OpenRead(Preview.CurrentFilePath);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination).ConfigureAwait(true);
        StatusMessage = $"Saved to {destinationPath}";
    }
}
