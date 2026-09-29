using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SQLModelViewer.App.ViewModels;

public partial class SchemaTreeViewModel : ViewModelBase
{
    public ObservableCollection<SchemaNodeViewModel> Schemas { get; } = [];

    [ObservableProperty]
    private string filterText = string.Empty;

    [ObservableProperty]
    private int selectedCount;

    public bool HasTables => Schemas.Count > 0;

    public void Load(IEnumerable<(string Schema, string Table)> tables)
    {
        Schemas.Clear();
        var bySchema = tables
            .GroupBy(t => t.Schema, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in bySchema)
        {
            var node = new SchemaNodeViewModel(group.Key);
            foreach (var (_, table) in group.OrderBy(t => t.Table, StringComparer.OrdinalIgnoreCase))
                node.AddTable(table);
            node.ApplyFilter(FilterText);
            node.PropertyChanged += (_, _) => RecomputeSelectedCount();
            foreach (var t in node.AllTables)
                t.PropertyChanged += (_, _) => RecomputeSelectedCount();
            Schemas.Add(node);
        }

        OnPropertyChanged(nameof(HasTables));
        RecomputeSelectedCount();
    }

    public IReadOnlySet<(string Schema, string Table)> SelectedTables =>
        Schemas.SelectMany(s => s.AllTables)
            .Where(t => t.IsChecked)
            .Select(t => (t.Schema, t.Name))
            .ToHashSet();

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var schema in Schemas)
            schema.IsChecked = true;
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var schema in Schemas)
            schema.IsChecked = false;
    }

    partial void OnFilterTextChanged(string value)
    {
        foreach (var schema in Schemas)
            schema.ApplyFilter(value);
    }

    private void RecomputeSelectedCount() =>
        SelectedCount = Schemas.SelectMany(s => s.AllTables).Count(t => t.IsChecked);
}
