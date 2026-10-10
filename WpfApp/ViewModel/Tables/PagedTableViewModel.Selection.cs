using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WpfApp.Models.Tables;

namespace WpfApp.ViewModel.Tables;

public abstract partial class PagedTableViewModel<TQuery, TRow>
{
    private readonly Dictionary<string, TRow> _selectedRows = new(StringComparer.Ordinal);
    private bool _isChangingSelection;

    [ObservableProperty]
    private bool _canSelect = true;

    [ObservableProperty]
    private int _selectedCount;

    public bool HasSelection => SelectedCount > 0;
    public bool HasPartiallySelectedVisibleRows =>
        Items.Any(row => row.IsSelected) && Items.Any(row => !row.IsSelected);

    public bool? AreAllRowsSelected
    {
        get
        {
            if (Items.Count == 0 || !Items.Any(row => row.IsSelected))
            {
                return false;
            }

            return Items.All(row => row.IsSelected) ? true : null;
        }
        set
        {
            if (value == true || HasPartiallySelectedVisibleRows)
            {
                SelectAllRows();
            }
            else
            {
                ClearSelectedRows();
            }
        }
    }

    public string SelectionText => $"Đã chọn {SelectedCount} dòng";
    public IReadOnlyList<TRow> SelectedItems => _selectedRows.Values.ToList();
    public event EventHandler? SelectionChanged;

    partial void OnCanSelectChanged(bool value)
    {
        if (!value)
        {
            ClearSelection();
        }
    }

    private void SelectAll()
    {
        _isChangingSelection = true;
        try
        {
            foreach (var row in Items)
            {
                row.IsSelected = true;
                _selectedRows[row.SelectionKey] = row;
            }
        }
        finally
        {
            _isChangingSelection = false;
        }

        UpdateSelectionState();
    }

    private void ClearSelection()
    {
        _isChangingSelection = true;
        try
        {
            foreach (var row in _selectedRows.Values.Concat(Items).Distinct())
            {
                row.IsSelected = false;
            }

            _selectedRows.Clear();
        }
        finally
        {
            _isChangingSelection = false;
        }

        UpdateSelectionState();
    }

    public void SelectAllRows() => SelectAll();
    public void ClearSelectedRows() => ClearSelection();

    public void SetSelectedKeys(IEnumerable<string> selectionKeys)
    {
        var selectedKeys = selectionKeys.ToHashSet(StringComparer.Ordinal);
        _isChangingSelection = true;
        try
        {
            foreach (var key in _selectedRows.Keys.ToArray())
            {
                if (selectedKeys.Contains(key))
                {
                    continue;
                }

                if (_selectedRows.TryGetValue(key, out var selectedRow))
                {
                    selectedRow.IsSelected = false;
                }

                _selectedRows.Remove(key);
            }

            foreach (var row in Items)
            {
                var shouldSelect = selectedKeys.Contains(row.SelectionKey);
                if (row.IsSelected != shouldSelect)
                {
                    row.IsSelected = shouldSelect;
                }

                if (shouldSelect)
                {
                    _selectedRows[row.SelectionKey] = row;
                }
                else
                {
                    _selectedRows.Remove(row.SelectionKey);
                }
            }
        }
        finally
        {
            _isChangingSelection = false;
        }

        UpdateSelectionState();
    }

    private bool IsRowSelected(TableRow row) => _selectedRows.ContainsKey(row.SelectionKey);

    private void ClearLoadedSelection()
    {
        _selectedRows.Clear();
        UpdateSelectionState();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TableRow.IsSelected) || sender is not TRow row)
        {
            return;
        }

        if (row.IsSelected)
        {
            _selectedRows[row.SelectionKey] = row;
        }
        else
        {
            _selectedRows.Remove(row.SelectionKey);
        }

        if (!_isChangingSelection)
        {
            UpdateSelectionState();
        }
    }

    private void UpdateSelectionState()
    {
        SelectedCount = _selectedRows.Count;
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasPartiallySelectedVisibleRows));
        OnPropertyChanged(nameof(AreAllRowsSelected));
        OnPropertyChanged(nameof(SelectionText));
        OnPropertyChanged(nameof(SelectedItems));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
