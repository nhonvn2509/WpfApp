using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfApp.Models;
using WpfApp.Models.Tables;
using WpfApp.Services.Data;
using WpfApp.Services.Settings;
using WpfApp.Services.Tables;
namespace WpfApp.ViewModel.Tables;

public sealed partial class ChainsTableViewModel : PagedTableViewModel<ChainsQuery, ChainRow>
{
    private static readonly TimeSpan FilterDebounceDelay = TimeSpan.FromMilliseconds(1500);

    private readonly IChainsLoadService _chainsService;
    private CancellationTokenSource? _filterDebounceCancellation;
    private bool _isResettingFilter;

    public ChainsTableViewModel(
        IChainsLoadService loadService,
        IDatabaseInsightsService? insights = null,
        ITableRowCountProvider? rowCounts = null,
        IUserSettings? userSettings = null)
        : base(
            "Nguồn",
            loadService,
            userSettings?.Current.Tables.PageSize ?? TablePage.DefaultPageSize,
            insights,
            rowCounts)
    {
        _chainsService = loadService;
        SelectionChanged += (_, _) => NotifyExploitPending();

        var today = DateTime.Today;
        FromDate = today;
        ToDate = today.AddDays(1).AddSeconds(-1);
    }

    private async Task ShowNoDataHintAsync()
    {
        DateTime? latestDay;
        try
        {
            latestDay = await _chainsService.LoadLatestSessionDayAsync().ConfigureAwait(true);
        }
        catch (Exception)
        {
            return;
        }

        if (latestDay is not { } day || Items.Count > 0 || day == FromDate?.Date)
        {
            return;
        }

        StatusText = $"Ngày đã chọn không có phiên nào. Ngày có dữ liệu gần nhất: {day:dd/MM/yyyy}";
    }
    [ObservableProperty]
    private string _searchText = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExploitDateFilter))]
    [NotifyPropertyChangedFor(nameof(HasInvalidDateRange))]
    [NotifyPropertyChangedFor(nameof(DateRangeValidationMessage))]
    private DateTime? _fromDate;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExploitDateFilter))]
    [NotifyPropertyChangedFor(nameof(HasInvalidDateRange))]
    [NotifyPropertyChangedFor(nameof(DateRangeValidationMessage))]
    private DateTime? _toDate;
    [ObservableProperty]
    private bool _hasPendingExploit;

    [ObservableProperty]
    private ChainRow? _selectedRow;

    public ObservableCollection<PropertyDetailItem> DetailItems { get; } = [];

    public bool HasDetail => DetailItems.Count > 0;
    public bool HasExploitDateFilter => FromDate.HasValue || ToDate.HasValue;
    public bool HasInvalidDateRange =>
        FromDate.HasValue && ToDate.HasValue && FromDate.Value > ToDate.Value;
    public string? DateRangeValidationMessage =>
        HasInvalidDateRange ? "Thời điểm bắt đầu phải nhỏ hơn hoặc bằng thời điểm kết thúc." : null;

    protected override ChainsQuery CreateQuery() =>
        new(new TablePage(PageIndex, PageSize), SearchText, FromDate, ToDate);

    public void NotifyExploitPending() => HasPendingExploit = true;

    public void MarkExploited() => HasPendingExploit = false;

    partial void OnSearchTextChanged(string value)
    {
        NotifyExploitPending();
        ScheduleFilter();
    }

    partial void OnSelectedRowChanged(ChainRow? value)
    {
        DetailItems.Clear();
        if (value is not null)
        {
            foreach (var item in Describe(value))
            {
                DetailItems.Add(item);
            }
        }

        OnPropertyChanged(nameof(HasDetail));
    }

    private static IEnumerable<PropertyDetailItem> Describe(ChainRow chain)
    {
        var items = new List<PropertyDetailItem>();
        void Add(string name, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            items.Add(new PropertyDetailItem(name, value));
        }

        Add("Mã", chain.Id.ToString());
        Add("Hồ sơ", chain.Profile);
        Add("Tần số", chain.Frequency);
        Add("Băng", chain.Band);
        Add("Phân cực", chain.Polarization);
        Add("Điều chế", chain.Modulation);
        Add("Modem", chain.Modem);
        Add("Nguồn", chain.Source);
        Add("Vệ tinh", chain.Satellite);
        Add("Thiết bị", chain.Equipment);
        Add("Hướng từ", chain.DirectionFrom);
        Add("Hướng đến", chain.DirectionTo);
        Add("Số phiên", chain.Sessions?.ToString());
        Add("Mô tả", chain.Description);
        Add("Ngày tạo", chain.Inserted?.ToString("dd/MM/yyyy HH:mm:ss"));
        Add("Ngày cập nhật", chain.Updated?.ToString("dd/MM/yyyy HH:mm:ss"));

        return items;
    }

    partial void OnFromDateChanged(DateTime? value)
    {
        NotifyExploitPending();
        ScheduleFilter();
    }

    partial void OnToDateChanged(DateTime? value)
    {
        NotifyExploitPending();
        ScheduleFilter();
    }

    private void ScheduleFilter()
    {
        if (_isResettingFilter)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        var previousCancellation = Interlocked.Exchange(ref _filterDebounceCancellation, cancellation);
        previousCancellation?.Cancel();
        _ = ApplyFilterAfterDelayAsync(cancellation);
    }

    private async Task ApplyFilterAfterDelayAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(FilterDebounceDelay, cancellation.Token);
            if (!ReferenceEquals(Volatile.Read(ref _filterDebounceCancellation), cancellation) ||
                HasInvalidDateRange)
            {
                return;
            }

            await ApplyFilterAsync();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            Interlocked.CompareExchange(ref _filterDebounceCancellation, null, cancellation);
            cancellation.Dispose();
        }
    }

    private void CancelPendingFilter()
    {
        var cancellation = Interlocked.Exchange(ref _filterDebounceCancellation, null);
        cancellation?.Cancel();
    }

    protected override void OnPageApplied(PagedResult<ChainRow> result)
    {
        NotifyExploitPending();
        if (result.Items.Count == 0 && HasExploitDateFilter)
        {
            _ = ShowNoDataHintAsync();
        }
    }

    [RelayCommand]
    public override Task ApplyFilterAsync() => ResetAsync();
    [RelayCommand]
    public override async Task ClearFilterAsync()
    {
        _isResettingFilter = true;
        try
        {
            SearchText = string.Empty;
            var today = DateTime.Today;
            FromDate = today;
            ToDate = today.AddDays(1).AddSeconds(-1);
        }
        finally
        {
            _isResettingFilter = false;
        }

        CancelPendingFilter();
        await ResetAsync();
    }
}
