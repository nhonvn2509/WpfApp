using System.ComponentModel;
using WpfApp.Models.Files;
using WpfApp.Models.Tables;
using WpfApp.Services.Tables;

namespace WpfApp.ViewModel;

public sealed partial class FileExtensionsViewModel
{
    private FileExtensionFilterRowViewModel? _selectedRow;

    public FileExtensionFilterRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set => SetProperty(ref _selectedRow, value);
    }

    private string _activeExtension = string.Empty;

    public void SetActiveFile(FileRow? file)
    {
        _activeExtension = ToBareExtension(file?.Extension);
        ApplyActiveRow();
    }

    private void ApplyActiveRow()
    {
        var active = _activeExtension.Length == 0
            ? null
            : ExtensionRows.FirstOrDefault(row => ToBareExtension(row.Extension) == _activeExtension);

        foreach (var row in ExtensionRows)
        {
            row.IsActive = ReferenceEquals(row, active);
        }

        SelectedRow = active;
    }

    public bool AreAllExtensionsSelected
    {
        get => _isAllExtensionsSelected;
        set
        {
            if (!SetProperty(ref _isAllExtensionsSelected, value) || _isSyncingSelections)
            {
                return;
            }

            foreach (var row in ExtensionRows)
            {
                row.IsSelected = value;
            }
        }
    }

    public IReadOnlyList<string> SelectedExtensions => ExtensionRows
        .Where(row => row.IsSelected)
        .Select(row => row.Extension)
        .ToArray();

    private static string ToBareExtension(string? extension) =>
        extension?.Trim().TrimStart('.').ToLowerInvariant() ?? string.Empty;

    private void ApplyExtensionCounts(
        IReadOnlyList<FileExtensionCount> counts,
        IReadOnlyCollection<string> selectedExtensions)
    {
        var selected = selectedExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in ExtensionRows)
        {
            row.PropertyChanged -= ExtensionRow_PropertyChanged;
        }

        ExtensionRows.Clear();
        foreach (var count in counts)
        {
            var row = new FileExtensionFilterRowViewModel(count.Extension, count.Count)
            {
                IsSelected = selected.Contains(count.Extension)
            };
            row.PropertyChanged += ExtensionRow_PropertyChanged;
            ExtensionRows.Add(row);
            selected.Remove(count.Extension);
        }

        foreach (var extension in selected)
        {
            var row = new FileExtensionFilterRowViewModel(extension, 0)
            {
                IsSelected = true
            };
            row.PropertyChanged += ExtensionRow_PropertyChanged;
            ExtensionRows.Add(row);
        }

        UpdateAllExtensionsSelected();
        ApplyActiveRow();
        OnPropertyChanged(nameof(HasNoExtensions));
    }

    private void ExtensionRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FileExtensionFilterRowViewModel.IsSelected) || _isSyncingSelections)
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedExtensions));
        UpdateAllExtensionsSelected();
    }

    private void UpdateAllExtensionsSelected()
    {
        _isSyncingSelections = true;
        try
        {
            _isAllExtensionsSelected = ExtensionRows.Count > 0 && ExtensionRows.All(row => row.IsSelected);
        }
        finally
        {
            _isSyncingSelections = false;
        }

        OnPropertyChanged(nameof(AreAllExtensionsSelected));
    }
}
