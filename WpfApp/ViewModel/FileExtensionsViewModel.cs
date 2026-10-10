using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using WpfApp.Models.Files;
using WpfApp.Services.Tables;

namespace WpfApp.ViewModel;

public sealed partial class FileExtensionsViewModel : ObservableObject
{
    private readonly IFilesLoadService _loadService;
    private bool _isSyncingSelections;
    private bool _isAllExtensionsSelected;

    public FileExtensionsViewModel(IFilesLoadService loadService)
    {
        _loadService = loadService;
    }

    public ObservableCollection<FileExtensionFilterRowViewModel> ExtensionRows { get; } = [];

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _statusText = "Chưa khai thác nguồn nào.";

    public bool HasNoExtensions => ExtensionRows.Count == 0;
}
