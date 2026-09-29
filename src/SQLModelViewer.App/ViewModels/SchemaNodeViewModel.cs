using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SQLModelViewer.App.ViewModels;

public partial class SchemaNodeViewModel : ViewModelBase
{
    public string SchemaName { get; }

    /// <summary>The full, unfiltered table list for this schema.</summary>
    public List<TableNodeViewModel> AllTables { get; } = [];

    /// <summary>The subset currently shown in the tree, after the filter box is applied.</summary>
    public ObservableCollection<TableNodeViewModel> VisibleTables { get; } = [];

    [ObservableProperty]
    private bool? isChecked = false;

    private bool suppressCascade;

    public SchemaNodeViewModel(string schemaName)
    {
        SchemaName = schemaName;
    }

    public TableNodeViewModel AddTable(string name)
    {
        var table = new TableNodeViewModel(SchemaName, name, RecomputeTriState);
        AllTables.Add(table);
        return table;
    }

    public void ApplyFilter(string? filter)
    {
        VisibleTables.Clear();
        var matches = string.IsNullOrWhiteSpace(filter)
            ? AllTables
            : AllTables.Where(t => t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
        foreach (var t in matches)
            VisibleTables.Add(t);
    }

    partial void OnIsCheckedChanged(bool? value)
    {
        if (suppressCascade || value is null)
            return;

        foreach (var t in AllTables)
            t.IsChecked = value.Value;
    }

    private void RecomputeTriState()
    {
        suppressCascade = true;
        var checkedCount = AllTables.Count(t => t.IsChecked);
        IsChecked = checkedCount == 0 ? false : checkedCount == AllTables.Count ? true : null;
        suppressCascade = false;
    }
}
