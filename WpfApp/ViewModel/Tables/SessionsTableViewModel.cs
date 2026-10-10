using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfApp.Models.Tables;
using WpfApp.Services.Data;
using WpfApp.Services.Settings;
using WpfApp.Services.Tables;
using WpfApp.ViewModel;
namespace WpfApp.ViewModel.Tables;

public sealed partial class SessionsTableViewModel : PagedTableViewModel<SessionsQuery, SessionRow>
{
    private readonly ISessionsLoadService _loadService;
    private readonly FileExtensionsViewModel _fileExtensions;
    public SessionsTableViewModel(
        ISessionsLoadService loadService,
        FileExtensionsViewModel fileExtensions,
        IDatabaseInsightsService? insights = null,
        ITableRowCountProvider? rowCounts = null,
        IUserSettings? userSettings = null)
        : base(
            "Phiên",
            loadService,
            userSettings?.Current.Tables.PageSize ?? TablePage.DefaultPageSize,
            insights,
            rowCounts)
    {
        _loadService = loadService;
        _fileExtensions = fileExtensions;
        SetLoadWhenActivated(false);
        _ = LoadStatusOptionsAsync();
    }
    public IReadOnlyList<TableFilterOption> StatusOptions { get; private set; } =
        [new TableFilterOption(null, "Tất cả trạng thái")];
    [ObservableProperty]
    private string _searchText = string.Empty;
    [ObservableProperty]
    private DateTime? _fromDate;
    [ObservableProperty]
    private DateTime? _toDate;
    [ObservableProperty]
    private SessionRow? _selectedRow;
    [ObservableProperty]
    private int? _statusId;

    public int ContextSelectionCount { get; private set; }

    public bool HasSingleContextSelection => ContextSelectionCount == 1;

    public void SetContextSelectionCount(int count)
    {
        if (ContextSelectionCount == count)
        {
            return;
        }

        ContextSelectionCount = count;
        OnPropertyChanged(nameof(ContextSelectionCount));
        OnPropertyChanged(nameof(HasSingleContextSelection));
    }

    public IReadOnlyList<int> ChainIds { get; private set; } = [];

    protected override SessionsQuery CreateQuery() =>
        new(
            new TablePage(PageIndex, PageSize),
            SearchText,
            FromDate,
            ToDate,
            StatusId,
            ChainIds,
            PageIndex > 1 ? QueryKeysetCursorJson : null,
            _fileExtensions.SelectedExtensions);

    public Task<long> CountMatchingSessionsAsync(CancellationToken cancellationToken = default) =>
        _loadService.CountAsync(CreateCountQuery(), cancellationToken);

    public Task<long> CountMatchingFilesAsync(CancellationToken cancellationToken = default) =>
        _loadService.CountMatchingFilesAsync(CreateCountQuery(), cancellationToken);

    private SessionsQuery CreateCountQuery() =>
        CreateQuery() with
        {
            Page = TablePage.First(PageSize),
            KeysetCursorJson = null
        };

    protected override void OnPageLoaded(IReadOnlyList<SessionRow> rows)
    {
        base.OnPageLoaded(rows);
        if (rows.Count == 0)
        {
            StorePageCursor(null);
            return;
        }

        var last = rows[^1];
        StorePageCursor($"{last.IdS}|{last.DayFgr}");
    }

    protected override (string Table, string Column, string? Term)? ScanProbe =>
        ("msg.SESSIONS", "NAME", SearchText);
    private async Task LoadStatusOptionsAsync()
    {
        try
        {
            StatusOptions = await _loadService.LoadStatusOptionsAsync();
            OnPropertyChanged(nameof(StatusOptions));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
    [RelayCommand]
    public override Task ApplyFilterAsync() => ResetAsync();

    public void ApplyExploitFilter(IReadOnlyList<int> chainIds, DateTime? fromDate, DateTime? toDate)
    {
        SearchText = string.Empty;
        StatusId = null;
        SelectedRow = null;
        ChainIds = chainIds.Distinct().ToArray();
        OnPropertyChanged(nameof(ChainIds));
        FromDate = fromDate;
        ToDate = toDate;
        SetLoadWhenActivated(true, loadIfActive: false);
    }

    public async Task<string?> LoadSessionXmlAsync(
        long sessionId,
        CancellationToken cancellationToken = default)
    {
        if (_xmlCache.TryGetValue(sessionId, out var cached))
        {
            _xmlCache.Remove(sessionId);
            _xmlCache[sessionId] = cached;
            return cached;
        }

        var xml = await _loadService.LoadSessionXmlAsync(sessionId, cancellationToken).ConfigureAwait(true);
        _xmlCache[sessionId] = xml;

        while (_xmlCache.Count > XmlCacheCapacity)
        {
            _xmlCache.Remove(_xmlCache.Keys.First());
        }

        return xml;
    }

    private const int XmlCacheCapacity = 8;

    private readonly Dictionary<long, string?> _xmlCache = [];

    // ponytail: lặp trang 1000 dòng/lượt tới khi hết; nếu bộ dữ liệu phình thêm thì chuyển sang đếm trước (CountAsync) rồi nạp song song.
    public async Task<IReadOnlyList<SessionRow>> LoadAllMatchingAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<SessionRow>();
        var pageIndex = 1;
        while (true)
        {
            var query = CreateQuery() with
            {
                Page = new TablePage(pageIndex, TablePage.MaxPageSize),
                KeysetCursorJson = null
            };
            var page = await _loadService.LoadPageAsync(query, cancellationToken).ConfigureAwait(true);
            rows.AddRange(page.Items);
            if (!page.HasMoreRows || page.Items.Count == 0)
            {
                return rows;
            }

            pageIndex++;
        }
    }

    public void ClearForNoSelection()
    {
        SetLoadWhenActivated(false);
        SearchText = string.Empty;
        StatusId = null;
        SelectedRow = null;
        ChainIds = [];
        OnPropertyChanged(nameof(ChainIds));
        FromDate = null;
        ToDate = null;
        ClearLoadedData();
    }

    [RelayCommand]
    public override async Task ClearFilterAsync()
    {
        SearchText = string.Empty;
        StatusId = null;
        ChainIds = [];
        OnPropertyChanged(nameof(ChainIds));
        FromDate = null;
        ToDate = null;
        await ResetAsync();
    }
}
