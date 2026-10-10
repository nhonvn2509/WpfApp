using System.ComponentModel;
using System.Windows;
using WpfApp.Models.Tables;
using WpfApp.Presentation;
using WpfApp.ViewModel;
using WpfApp.ViewModel.Tables;

namespace WpfApp.Views;

public partial class MainWindow : Window
{
    private readonly CollapsiblePaneColumn _navColumn;

    private readonly CollapsiblePaneColumn _sideColumn;

    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        _navColumn = new CollapsiblePaneColumn(NavColumn);
        _sideColumn = new CollapsiblePaneColumn(SideColumn);

        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        viewModel.SessionFiles.PropertyChanged += SessionFiles_PropertyChanged;
        Closed += (_, _) => viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        Closed += (_, _) => viewModel.SessionFiles.PropertyChanged -= SessionFiles_PropertyChanged;
    }

    public MainWindowViewModel ViewModel { get; }

    public void ShowFilePreview(FileRow row, string? path) =>
        PreviewPane.ShowPreview(row, path);

    private void MainWindow_Loaded(object sender, RoutedEventArgs e) =>
        _ = ViewModel.EnsureActiveTableLoadedAsync();

    private void SessionFiles_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionFilesViewModel.SelectedFile) &&
            ViewModel.SessionFiles.SelectedFile is null)
        {
            PreviewPane.ClearPreview();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.IsNavPaneOpen):
                _navColumn.SetOpen(ViewModel.IsNavPaneOpen);
                break;

            case nameof(MainWindowViewModel.IsSidePaneOpen):
                _sideColumn.SetOpen(ViewModel.IsSidePaneOpen);
                break;
        }
    }
}
