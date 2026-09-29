using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using SQLModelViewer.App.Services;
using SQLModelViewer.App.ViewModels;

namespace SQLModelViewer.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void SaveAsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.Preview.CurrentFilePath is null)
            return;

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
            return;

        var suggestedName = Path.GetFileName(vm.Preview.CurrentFilePath);
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save diagram as",
            SuggestedFileName = suggestedName,
            DefaultExtension = "html",
            FileTypeChoices = [new FilePickerFileType("HTML file") { Patterns = ["*.html"] }]
        });

        if (file is not null)
            await vm.SaveGeneratedFileAsAsync(file.Path.LocalPath);
    }

    private void RecentConnection_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: ConnectionProfile profile } && DataContext is MainWindowViewModel vm)
            vm.Connection.ApplyProfile(profile);
    }

    private void WebView2Link_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/") { UseShellExecute = true });
    }
}
