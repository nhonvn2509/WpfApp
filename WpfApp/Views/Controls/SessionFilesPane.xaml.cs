using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.ComponentModel;
using WpfApp.Models.Tables;
using WpfApp.Services.Tables;
using WpfApp.ViewModel.Tables;

namespace WpfApp.Views.Controls;

public sealed partial class SessionFilesPane : UserControl
{
    private readonly HashSet<DataGrid> _metadataGrids = [];
    private SessionFilesViewModel? _viewModel;
    private bool _isSynchronizingSelection;

    public SessionFilesPane()
    {
        InitializeComponent();
        DataContextChanged += SessionFilesPane_DataContextChanged;
        Loaded += SessionFilesPane_Loaded;
    }

    private void SessionFilesPane_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        AttachViewModel();
        QueueMetadataGridSelectionSync();
    }

    private void SessionFilesPane_Loaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel();
        QueueMetadataGridSelectionSync();
    }

    private void AttachViewModel()
    {
        var nextViewModel = DataContext as SessionFilesViewModel;
        if (ReferenceEquals(_viewModel, nextViewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = nextViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SessionFilesViewModel.SelectedFile) or nameof(SessionFilesViewModel.MetadataSections))
        {
            QueueMetadataGridSelectionSync();
        }
    }

    private void QueueMetadataGridSelectionSync() =>
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(SyncMetadataGridSelections));

    private void MetadataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        AttachViewModel();
        _metadataGrids.Add(grid);
        ConfigureMetadataGrid(grid);
        QueueMetadataGridSelectionSync();
    }

    private void MetadataGrid_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        ConfigureMetadataGrid(grid);
        QueueMetadataGridSelectionSync();
    }

    private static void ConfigureMetadataGrid(DataGrid grid)
    {
        if (grid.DataContext is not SessionMetadataSection section)
        {
            grid.Tag = null;
            grid.Columns.Clear();
            return;
        }

        if (ReferenceEquals(grid.Tag, section))
        {
            return;
        }

        grid.Tag = section;
        grid.Columns.Clear();
        foreach (var column in section.Columns)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = column.Header,
                Width = column.Width,
                Binding = new Binding($"[{column.Key}]")
            });
        }
    }

    private void MetadataGrid_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid)
        {
            _metadataGrids.Remove(grid);
        }
    }

    private void MetadataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not DataGrid grid ||
            DataContext is not SessionFilesViewModel viewModel ||
            _isSynchronizingSelection ||
            grid.DataContext is not SessionMetadataSection { FileRows: { } files })
        {
            return;
        }

        var index = grid.SelectedIndex;
        if (index >= 0 && index < files.Count)
        {
            viewModel.SelectFile(files[index]);
        }
    }

    private void MetadataGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid ||
            DataContext is not SessionFilesViewModel viewModel ||
            grid.DataContext is not SessionMetadataSection { FileRows: { } files } ||
            ItemsControl.ContainerFromElement(grid, e.OriginalSource as DependencyObject) is not DataGridRow row)
        {
            return;
        }

        var index = row.GetIndex();
        if (index < 0 || index >= files.Count)
        {
            return;
        }

        viewModel.SelectFile(files[index]);
    }

    private void SyncMetadataGridSelections()
    {
        foreach (var grid in _metadataGrids.ToArray())
        {
            SyncMetadataGridSelection(grid);
        }
    }

    private void SyncMetadataGridSelection(DataGrid grid)
    {
        if (_viewModel is null || grid.DataContext is not SessionMetadataSection { FileRows: { } files })
        {
            return;
        }

        var selectedFile = _viewModel.SelectedFile;
        var selectedIndex = SessionFileMatcher.FindIndex(files, selectedFile);
        var selectedItem = selectedIndex >= 0 && selectedIndex < grid.Items.Count
            ? grid.Items[selectedIndex]
            : null;

        if (grid.SelectedIndex == selectedIndex && ReferenceEquals(grid.SelectedItem, selectedItem))
        {
            return;
        }

        _isSynchronizingSelection = true;
        try
        {
            if (selectedItem is null)
            {
                grid.UnselectAll();
                grid.SelectedIndex = -1;
            }
            else
            {
                grid.SelectedItem = selectedItem;
                grid.SelectedIndex = selectedIndex;
                grid.ScrollIntoView(selectedItem);
            }
        }
        finally
        {
            _isSynchronizingSelection = false;
        }
    }
}
