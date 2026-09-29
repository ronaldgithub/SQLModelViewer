using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
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

    private async void ConnectButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var dialog = new ConnectionDialog { DataContext = vm.Connection };
        await dialog.ShowDialog(this);
    }

    private void WebView2Link_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/") { UseShellExecute = true });
    }

    private async void AboutButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new AboutDialog();
        await dialog.ShowDialog(this);
    }

    private bool sidebarCollapsed;

    private void SidebarToggle_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        sidebarCollapsed = !sidebarCollapsed;
        RootGrid.ColumnDefinitions[0].Width = new GridLength(sidebarCollapsed ? 44 : 280);
        SidebarContent.IsVisible = !sidebarCollapsed;
        SidebarToggleButton.Content = sidebarCollapsed ? "▶" : "◀";
        ToolTip.SetTip(SidebarToggleButton, sidebarCollapsed ? "Show sidebar" : "Hide sidebar");
    }
}
