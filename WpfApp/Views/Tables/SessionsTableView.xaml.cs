using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp.Models.Tables;
using WpfApp.ViewModel;

namespace WpfApp.Views.Tables;

public sealed partial class SessionsTableView : UserControl
{
    private bool _canOpenContextMenu;

    public SessionsTableView()
    {
        InitializeComponent();
    }

    private void SessionsGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _canOpenContextMenu = false;
        if (sender is not DataGrid grid ||
            DataContext is not MainWindowViewModel viewModel ||
            ItemsControl.ContainerFromElement(grid, e.OriginalSource as DependencyObject) is not DataGridRow row ||
            row.Item is not SessionRow session)
        {
            return;
        }

        _canOpenContextMenu = true;
        if (!grid.SelectedItems.Contains(session))
        {
            grid.SelectedItems.Clear();
            row.IsSelected = true;
        }

        viewModel.Sessions.SetContextSelectionCount(grid.SelectedItems.Count);
    }

    private void SessionsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid grid && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Sessions.SetContextSelectionCount(grid.SelectedItems.Count);
        }
    }

    private void SessionsGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (!_canOpenContextMenu)
        {
            e.Handled = true;
        }

        _canOpenContextMenu = false;
    }
}
