using CommunityToolkit.Mvvm.ComponentModel;
using SQLModelViewer.App.Services;

namespace SQLModelViewer.App.ViewModels;

public partial class PreviewViewModel : ViewModelBase
{
    [ObservableProperty]
    private Uri? source;

    [ObservableProperty]
    private string? currentFilePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWebView))]
    private bool hasDiagram;

    public bool IsWebViewAvailable { get; }

    /// <summary>
    /// The NativeWebView control is a native/hwnd-hosted control that paints above ordinary
    /// Avalonia content regardless of declared z-order, even with no Source loaded — so it must
    /// stay collapsed (not just "empty") until there's actually something to show, or it blanks
    /// out the "no diagram yet" placeholder and the WebView2-unavailable fallback behind it.
    /// </summary>
    public bool ShowWebView => IsWebViewAvailable && HasDiagram;

    public PreviewViewModel(IWebViewAvailabilityChecker availabilityChecker)
    {
        IsWebViewAvailable = availabilityChecker.IsAvailable();
    }

    public void Show(string filePath)
    {
        CurrentFilePath = filePath;
        HasDiagram = true;
        // Force a Source change even if the path string is the same as before (new temp file each
        // generation, but be defensive) so the WebView always re-navigates.
        Source = null;
        Source = new Uri(filePath);
    }
}
