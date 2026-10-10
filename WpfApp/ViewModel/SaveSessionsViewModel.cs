using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfApp.Models.Files;
using WpfApp.Models.Tables;
using WpfApp.Services;
using WpfApp.Services.Files;
using WpfApp.ViewModel.Tables;

namespace WpfApp.ViewModel;

public sealed partial class SaveSessionsViewModel : ObservableObject
{
    private readonly SaveToPreferencesStore _preferences;
    private readonly SaveToCatalogService _catalog;
    private readonly IDialogService _dialogs;
    private readonly SessionsTableViewModel _sessions;
    private readonly SaveToFileCollector _collector;
    private readonly SaveToCopyService _copy;

    public SaveSessionsViewModel(
        SaveToPreferencesStore preferences,
        SaveToCatalogService catalog,
        IDialogService dialogs,
        SessionsTableViewModel sessions,
        SaveToFileCollector collector,
        SaveToCopyService copy)
    {
        _preferences = preferences;
        _catalog = catalog;
        _dialogs = dialogs;
        _sessions = sessions;
        _collector = collector;
        _copy = copy;
        ExtensionOptions = ["All Files", .. _catalog.BuildOptions().Select(static option => option.Name)];
        Destination = _preferences.LoadFolders().FirstOrDefault()?.Path ?? string.Empty;
    }

    public IReadOnlyList<string> ExtensionOptions { get; }

    [ObservableProperty]
    private string _destination = string.Empty;

    [ObservableProperty]
    private bool _makeSessionFolder = true;

    [ObservableProperty]
    private bool _saveHeader = true;

    [ObservableProperty]
    private bool _overwrite = true;

    public bool AddCopyNumber
    {
        get => !Overwrite;
        set => Overwrite = !value;
    }

    [ObservableProperty]
    private bool _useExtensionFilter;

    [ObservableProperty]
    private string _extensionFilter = "All Files";

    [ObservableProperty]
    private bool _closeAfterCopy;

    [ObservableProperty]
    private bool _renameByTime;

    [ObservableProperty]
    private SaveToScope _scope = SaveToScope.SelectedSessions;

    public bool ScopeIsSelected
    {
        get => Scope == SaveToScope.SelectedSessions;
        set
        {
            if (value)
            {
                Scope = SaveToScope.SelectedSessions;
            }
        }
    }

    public bool ScopeIsVisible
    {
        get => Scope == SaveToScope.VisibleSessions;
        set
        {
            if (value)
            {
                Scope = SaveToScope.VisibleSessions;
            }
        }
    }

    public bool ScopeIsFull
    {
        get => Scope == SaveToScope.FullQuery;
        set
        {
            if (value)
            {
                Scope = SaveToScope.FullQuery;
            }
        }
    }

    partial void OnScopeChanged(SaveToScope value)
    {
        OnPropertyChanged(nameof(ScopeIsSelected));
        OnPropertyChanged(nameof(ScopeIsVisible));
        OnPropertyChanged(nameof(ScopeIsFull));
    }

    [ObservableProperty]
    private int _sessionsTotal;

    [ObservableProperty]
    private int _sessionsDone;

    [ObservableProperty]
    private int _filesTotal;

    [ObservableProperty]
    private int _filesDone;

    [ObservableProperty]
    private int _filesFailed;

    [ObservableProperty]
    private string _currentFile = string.Empty;

    [ObservableProperty]
    private double _currentFilePercent;

    [ObservableProperty]
    private double _totalPercent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCopyCommand))]
    private bool _isBusy;

    public string ProgressText => FilesTotal == 0
        ? string.Empty
        : $"Đã xử lý {FilesDone:N0}/{FilesTotal:N0} tệp · lỗi {FilesFailed:N0} · phiên {SessionsDone:N0}/{SessionsTotal:N0}";

    public event EventHandler? CloseRequested;

    public SaveToOptions BuildOptions() => new(
        MakeSessionFolder,
        SaveHeader,
        Overwrite ? SaveToConflictMode.Overwrite : SaveToConflictMode.AddCopyNumber,
        UseExtensionFilter,
        ExtensionFilter,
        CloseAfterCopy,
        RenameByTime);

    [RelayCommand]
    private void Browse()
    {
        var chosen = _dialogs.AskFolder("Chọn thư mục lưu phiên", Destination);
        if (!string.IsNullOrWhiteSpace(chosen))
        {
            Destination = chosen;
        }
    }

    private bool CanStartCopy() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanStartCopy))]
    private async Task StartCopyAsync()
    {
        var destination = Destination?.Trim() ?? string.Empty;
        if (destination.Length == 0)
        {
            _dialogs.ShowError("Hãy chọn thư mục lưu trước.");
            return;
        }

        IReadOnlyList<SessionRow> sessions;
        try
        {
            sessions = await ResolveSessionsAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"Không tải được danh sách phiên: {exception.Message}");
            return;
        }

        if (sessions.Count == 0)
        {
            _dialogs.ShowInformation(
                Scope == SaveToScope.SelectedSessions
                    ? "Chưa tick phiên nào ở bảng Phiên. Tick các dòng cần lưu, hoặc đổi phạm vi."
                    : "Không có phiên nào khớp bộ lọc hiện tại.");
            return;
        }

        IsBusy = true;
        SessionsTotal = sessions.Count;
        SessionsDone = 0;
        FilesTotal = 0;
        FilesDone = 0;
        FilesFailed = 0;
        TotalPercent = 0;
        CurrentFile = string.Empty;
        try
        {
            var options = BuildOptions();
            var filter = _catalog.ResolveFilter(options, _catalog.BuildOptions());
            CurrentFile = "Đang thu thập danh sách tệp...";
            var files = await _collector.CollectAsync(sessions, filter, options.SaveHeader, options.RenameByTime).ConfigureAwait(true);
            if (files.Count == 0)
            {
                _dialogs.ShowInformation("Không có tệp nào khớp bộ lọc đuôi tệp để lưu.");
                return;
            }

            FilesTotal = files.Count;
            var progress = new Progress<SaveToProgress>(update =>
            {
                SessionsTotal = update.SessionsTotal;
                SessionsDone = update.SessionsDone;
                FilesTotal = update.FilesTotal;
                FilesDone = update.FilesDone;
                FilesFailed = update.FilesFailed;
                CurrentFile = update.CurrentFile;
                CurrentFilePercent = update.CurrentFilePercent;
                TotalPercent = update.TotalPercent;
                OnPropertyChanged(nameof(ProgressText));
            });

            var result = await _copy.CopyAsync(files, destination, options, progress).ConfigureAwait(true);

            var folders = _preferences.LoadFolders()
                .Where(folder => !string.Equals(folder.Path, destination, StringComparison.OrdinalIgnoreCase))
                .Select(folder => new SaveToFolderEntry(folder.Path, false))
                .ToList();
            folders.Insert(0, new SaveToFolderEntry(destination, true));
            _preferences.Save(options, folders);

            var summary = result.FilesFailed == 0
                ? $"Đã lưu {result.FilesCopied:N0} tệp của {result.SessionsCopied:N0} phiên."
                : $"Lưu xong: {result.FilesCopied:N0} tệp OK, {result.FilesFailed:N0} tệp lỗi.";
            if (result.Errors.Count > 0)
            {
                summary += "\n" + string.Join("\n", result.Errors.Take(5));
            }

            _dialogs.ShowInformation(summary, "Lưu phiên");
            if (options.CloseAfterCopy)
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"Lỗi khi lưu phiên: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
            // Thông báo xong là xong: dọn thanh tiến độ, không giữ trạng thái cũ trên UI.
            TotalPercent = 0;
            CurrentFile = string.Empty;
            FilesTotal = 0;
            FilesDone = 0;
            FilesFailed = 0;
            SessionsTotal = 0;
            SessionsDone = 0;
            OnPropertyChanged(nameof(ProgressText));
        }
    }

    private async Task<IReadOnlyList<SessionRow>> ResolveSessionsAsync()
    {
        switch (Scope)
        {
            case SaveToScope.VisibleSessions:
                return _sessions.Items;
            case SaveToScope.FullQuery:
                return await _sessions.LoadAllMatchingAsync().ConfigureAwait(true);
            default:
                return _sessions.SelectedItems;
        }
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
