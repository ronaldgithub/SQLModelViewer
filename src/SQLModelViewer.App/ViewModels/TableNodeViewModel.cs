using CommunityToolkit.Mvvm.ComponentModel;

namespace SQLModelViewer.App.ViewModels;

public partial class TableNodeViewModel : ViewModelBase
{
    private readonly Action? onCheckedChanged;

    public string Schema { get; }
    public string Name { get; }
    public string QualifiedName => $"{Schema}.{Name}";

    [ObservableProperty]
    private bool isChecked;

    public TableNodeViewModel(string schema, string name, Action? onCheckedChanged = null)
    {
        Schema = schema;
        Name = name;
        this.onCheckedChanged = onCheckedChanged;
    }

    partial void OnIsCheckedChanged(bool value) => onCheckedChanged?.Invoke();
}
