using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfApp.Models.Files;
using WpfApp.Models.Tables;
using WpfApp.Services;
using WpfApp.Services.Data;
using WpfApp.Services.Tables;
using WpfApp.ViewModel.Tables;
using WpfApp.ViewModel.Workflows;

namespace WpfApp.ViewModel;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IChainExploitationCoordinator _exploitationCoordinator;
    private readonly ITableRowCountProvider _rowCountProvider;
    private readonly IDialogService _dialog;
    private readonly SaveSessionsViewModel _saveSessions;
    private CancellationTokenSource? _totalsRefreshCancellation;
    private long? _filteredSessionCount;
    private long? _filteredFileCount;
    private long? _databaseSessionTotal;
    private long? _databaseFileTotal;

    public MainWindowViewModel(
        SessionsTableViewModel sessions,
        SessionFilesViewModel sessionFiles,
        FileExtensionsViewModel fileExtensions,
        ChainsTableViewModel chains,
        IChainExploitationCoordinator exploitationCoordinator,
        ITableRowCountProvider rowCountProvider,
        IDialogService dialog,
        SaveSessionsViewModel saveSessions)
    {
        Sessions = sessions;
        SessionFiles = sessionFiles;
        FileExtensions = fileExtensions;
        Chains = chains;
        _exploitationCoordinator = exploitationCoordinator;
        _rowCountProvider = rowCountProvider;
        _dialog = dialog;
        _saveSessions = saveSessions;
        Chains.SelectionChanged += Chains_SelectionChanged;
        FileExtensions.PropertyChanged += FileExtensions_PropertyChanged;
        _exploitationCoordinator.PropertyChanged += ExploitationCoordinator_PropertyChanged;

        _exploitationCoordinator.SessionsReady += Coordinator_SessionsReady;
        Tables = [Sessions, Chains];
        foreach (var table in Tables)
        {
            table.PropertyChanged += Table_PropertyChanged;
        }

        Chains.IsActive = true;
        RefreshStatusBar();
        _ = RefreshDatabaseTotalsAsync();
    }

    public SessionsTableViewModel Sessions { get; }

    public SessionFilesViewModel SessionFiles { get; }

    public FileExtensionsViewModel FileExtensions { get; }
    public ChainsTableViewModel Chains { get; }
    public IReadOnlyList<IPagedTableViewModel> Tables { get; }
    public IPagedTableViewModel? ActiveTable => Tables.FirstOrDefault(table => table.IsActive);
    public bool IsExploitingChains => _exploitationCoordinator.IsRunning;

    /// <summary>
    /// Binded bởi <see cref="ExploitButton"/>. Tiền tố "Pending" vì đang chờ confirmation khai thác (như đang chờ thư
    /// danh sách/mã code). Cần ngoài lớp để binding không lỗi vì DataContext là MainWindowViewModel.
    /// </summary>
    public bool HasPendingExploit
    {
        get => Chains.HasPendingExploit;
        set => Chains.HasPendingExploit = value;
    }

    [ObservableProperty]
    private bool _isNavPaneOpen = true;

    [ObservableProperty]
    private bool _isSidePaneOpen = true;

    public string ActiveSelectionText => ActiveTable?.SelectionText ?? string.Empty;

    public bool ActiveCanSelect
    {
        get => ActiveTable?.CanSelect ?? false;
        set
        {
            if (ActiveTable is { } table)
            {
                table.CanSelect = value;
            }
        }
    }

    [ObservableProperty]
    private int _selectedTabIndex = 1;

    [ObservableProperty]
    private bool _hasStatusError;

    [ObservableProperty]
    private string _statusBarMessage = string.Empty;

    [ObservableProperty]
    private string _pageSummary = string.Empty;

    [ObservableProperty]
    private string _databaseTotals = string.Empty;

    public string ActiveSelectedSummary
    {
        get
        {
            var table = ActiveTable;
            var total = table is null
                ? "0"
                : table.IsTotalCountKnown
                    ? table.TotalCount.ToString("N0")
                    : "…";
            return $"Selected: {table?.SelectedCount ?? 0:N0}/{total}";
        }
    }

    public string NewPrioritySummary => "New Priority: ---";

    public string SessionTotalSummary
    {
        get
        {
            var filtered = FormatCount(_filteredSessionCount);
            var total = _databaseSessionTotal?.ToString("N0") ?? "…";
            return $"Total: {filtered}/{total}";
        }
    }

    private void RefreshStatusBar()
    {
        var table = ActiveTable;
        PageSummary = table?.PageText ?? string.Empty;
        HasStatusError = table?.ErrorMessage is { Length: > 0 };
        OnPropertyChanged(nameof(ActiveSelectedSummary));

        StatusBarMessage = table switch
        {
            null => string.Empty,
            { } when table.ErrorMessage is { Length: > 0 } error => error,
            { IsLoading: true } => string.Empty,
            { } when table.StatusText is { Length: > 0 } status => status,
            _ => string.Empty
        };
    }

    private async Task RefreshDatabaseTotalsAsync()
    {
        var cancellation = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _totalsRefreshCancellation, cancellation);
        previous?.Cancel();

        try
        {
            var token = cancellation.Token;
            var sessionsTask = Sessions.CountMatchingSessionsAsync(token);
            var filesTask = Sessions.CountMatchingFilesAsync(token);
            var totalSessionsTask = _rowCountProvider.GetRowCountAsync("msg.SESSIONS", token);
            var totalFilesTask = _rowCountProvider.GetRowCountAsync("msg.FILES", token);

            await Task.WhenAll(sessionsTask, filesTask, totalSessionsTask, totalFilesTask)
                .ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            _filteredSessionCount = sessionsTask.Result >= 0 ? sessionsTask.Result : null;
            _filteredFileCount = filesTask.Result >= 0 ? filesTask.Result : null;
            _databaseSessionTotal = totalSessionsTask.Result >= 0 ? totalSessionsTask.Result : null;
            _databaseFileTotal = totalFilesTask.Result >= 0 ? totalFilesTask.Result : null;
            if (_filteredSessionCount is null || _filteredFileCount is null)
            {
                StatusBarMessage = "Không đếm được tổng số phiên/tệp theo bộ lọc hiện tại.";
                HasStatusError = true;
            }
            else if (HasStatusError && StatusBarMessage.StartsWith("Không đếm được tổng số", StringComparison.Ordinal))
            {
                HasStatusError = false;
                RefreshStatusBar();
            }

            DatabaseTotals =
                $"Phiên {FormatCount(_filteredSessionCount)}/{FormatCount(_databaseSessionTotal)}"
                + $" · Tệp {FormatCount(_filteredFileCount)}/{FormatCount(_databaseFileTotal)}";
            OnPropertyChanged(nameof(SessionTotalSummary));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(_totalsRefreshCancellation, cancellation))
            {
                HasStatusError = true;
                StatusBarMessage = $"Không đếm được tổng số theo bộ lọc: {exception.Message}";
            }
        }
        finally
        {
            if (ReferenceEquals(_totalsRefreshCancellation, cancellation))
            {
                _totalsRefreshCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private static string FormatCount(long? count) => count?.ToString("N0") ?? "…";

    private void FileExtensions_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(FileExtensionsViewModel.SelectedExtensions))
        {
            _ = RefreshDatabaseTotalsAsync();
        }
    }

    public const int SessionsTabIndex = 0;
    public const int ChainsTabIndex = 1;
    public const int NavTreeTabIndex = 0;
    public const int NavExtensionsTabIndex = 1;

    [ObservableProperty]
    private int _selectedNavTabIndex = NavTreeTabIndex;

    [RelayCommand]
    private void ShowNavTreeTab() => SelectedNavTabIndex = NavTreeTabIndex;

    [RelayCommand]
    private void ShowNavExtensionsTab() => SelectedNavTabIndex = NavExtensionsTabIndex;

    public bool IsSessionsTabActive => Sessions.IsActive;

    public bool IsChainsTabActive => Chains.IsActive;

    public string SideDetailTitle => IsChainsTabActive ? "Chi tiết nguồn" : "Chi tiết phiên";

    public void ShowSessionsTab() => SelectedTabIndex = SessionsTabIndex;

    [RelayCommand]
    private void OpenSettings() => _dialog.ShowSettings();

    [RelayCommand]
    private void OpenSaveSessions() => _dialog.ShowSaveSessions(_saveSessions);

    [RelayCommand]
    private void OpenSaveSessionsForCheckedSessions()
    {
        _saveSessions.Scope = SaveToScope.SelectedSessions;
        _dialog.ShowSaveSessions(_saveSessions);
    }

    [RelayCommand]
    private void OpenContextSession() => ShowSelectedSessionAsync(Sessions.SelectedRow);

    [RelayCommand]
    private Task ApplyFilterAsync()
    {
        if (FileExtensions.SelectedExtensions.Count > 0)
        {
            if (SessionFiles.IsViewingSession)
            {
                return SessionFiles.RefreshCommand.ExecuteAsync(null);
            }

            var chainIds = Chains.SelectedItems
                .Select(chain => chain.Id)
                .Distinct()
                .ToArray();
            return SessionFiles.SearchForChainsAsync(chainIds, Chains.FromDate, Chains.ToDate);
        }

        return ActiveTable?.ApplyFilterAsync() ?? Task.CompletedTask;
    }

    [RelayCommand]
    private Task ClearFilterAsync() => ActiveTable?.ClearFilterAsync() ?? Task.CompletedTask;

    [RelayCommand]
    private Task FirstPageActiveAsync() => ActiveTable?.FirstPageAsync() ?? Task.CompletedTask;

    [RelayCommand]
    private Task PreviousPageActiveAsync() => ActiveTable?.PreviousPageAsync() ?? Task.CompletedTask;

    [RelayCommand]
    private Task NextPageActiveAsync() => ActiveTable?.NextPageAsync() ?? Task.CompletedTask;

    [RelayCommand]
    private Task LastPageActiveAsync() => ActiveTable?.LastPageAsync() ?? Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanExploitChains), AllowConcurrentExecutions = true)]
    private Task ExploitChainsAsync() => _exploitationCoordinator.ExecuteAsync();

    private bool CanExploitChains() => !Chains.HasInvalidDateRange;

    public async Task EnsureActiveTableLoadedAsync()
    {
        foreach (var table in Tables)
        {
            if (table.IsActive)
            {
                await table.EnsureLoadedAsync();
                return;
            }
        }
    }

    private void Sessions_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(SessionsTableViewModel.SelectedRow) || IsExploitingChains)
        {
            return;
        }

        ShowSelectedSessionAsync(Sessions.SelectedRow);
    }

    private async void ShowSelectedSessionAsync(SessionRow? session)
    {
        if (session is null || session.IdS == SessionFiles.CurrentSessionId)
        {
            return;
        }

        // Click phiên là cũng phải bật tab Cây tệp bên trái để thấy ngay cây của phiên đó.
        ShowNavTreeTab();

        try
        {
            await SessionFiles.OpenAsync(session);
        }
        catch (Exception exception)
        {
            SessionFiles.ReportLoadError("mở phiên", exception);
        }
    }

    private void Chains_SelectionChanged(object? sender, EventArgs args) =>
        ExploitChainsCommand.NotifyCanExecuteChanged();

    private void Table_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is SessionsTableViewModel sessions &&
            args.PropertyName == nameof(SessionsTableViewModel.Items))
        {
            var viewedSessionId = SessionFiles.CurrentSessionId;
            var selectedSession = viewedSessionId is { } sessionId
                ? sessions.Items.FirstOrDefault(row => row.IdS == sessionId)
                : null;

            if (!ReferenceEquals(sessions.SelectedRow, selectedSession))
            {
                sessions.SelectedRow = selectedSession;
            }
        }

        if (args.PropertyName == nameof(IPagedTableViewModel.IsActive))
        {
            OnPropertyChanged(nameof(ActiveTable));
            OnPropertyChanged(nameof(ActiveSelectionText));
            OnPropertyChanged(nameof(ActiveCanSelect));
            OnPropertyChanged(nameof(ActiveSelectedSummary));
            RefreshStatusBar();

            OnPropertyChanged(nameof(IsSessionsTabActive));
            OnPropertyChanged(nameof(IsChainsTabActive));
            OnPropertyChanged(nameof(SideDetailTitle));
        }

        if (ReferenceEquals(sender, ActiveTable) &&
            args.PropertyName is nameof(IPagedTableViewModel.PageText)
                or nameof(IPagedTableViewModel.IsLoading)
                or nameof(IPagedTableViewModel.ErrorMessage)
                or nameof(IPagedTableViewModel.StatusText)
                or nameof(IPagedTableViewModel.TotalCount)
                or nameof(IPagedTableViewModel.IsTotalCountKnown))
        {
            RefreshStatusBar();
        }

        if (sender is SessionsTableViewModel { IsLoading: true } &&
            args.PropertyName == nameof(IPagedTableViewModel.IsLoading))
        {
            _ = RefreshDatabaseTotalsAsync();
        }

        if (ReferenceEquals(sender, ActiveTable) &&
            args.PropertyName == nameof(IPagedTableViewModel.SelectedCount))
        {
            OnPropertyChanged(nameof(ActiveSelectedSummary));
        }

        if (ReferenceEquals(sender, ActiveTable) &&
            args.PropertyName is nameof(IPagedTableViewModel.CanSelect)
                or nameof(IPagedTableViewModel.SelectionText))
        {
            OnPropertyChanged(nameof(ActiveSelectionText));
            OnPropertyChanged(nameof(ActiveCanSelect));
            OnPropertyChanged(nameof(ActiveSelectedSummary));
        }

        if (sender is SessionsTableViewModel &&
            args.PropertyName == nameof(SessionsTableViewModel.SelectedRow))
        {
            Sessions_PropertyChanged(sender, args);
        }

        if (sender is ChainsTableViewModel &&
            args.PropertyName is nameof(ChainsTableViewModel.HasExploitDateFilter)
                or nameof(ChainsTableViewModel.HasInvalidDateRange))
        {
            ExploitChainsCommand.NotifyCanExecuteChanged();
        }
    }

    private void Coordinator_SessionsReady(object? sender, EventArgs e) => ShowSessionsTab();

    private void ExploitationCoordinator_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(IChainExploitationCoordinator.IsRunning))
        {
            OnPropertyChanged(nameof(IsExploitingChains));
            ExploitChainsCommand.NotifyCanExecuteChanged();
        }
    }
}
