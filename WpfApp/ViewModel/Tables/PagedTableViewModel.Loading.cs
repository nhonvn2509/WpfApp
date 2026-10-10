using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfApp.Services.Tables;

namespace WpfApp.ViewModel.Tables;

public abstract partial class PagedTableViewModel<TQuery, TRow>
{
    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _loadWhenActivated = true;

    private bool _suppressLoadOnActivation;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isPagingEnabled = true;

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private string _statusText = "Chưa tải dữ liệu.";

    [ObservableProperty]
    private string? _errorMessage;

    public bool IsEmpty { get; private set; }

    public string EmptyStateMessage { get; private set; } = string.Empty;

    private void UpdateEmptyState()
    {
        var isEmpty = IsLoaded && !IsLoading && Items.Count == 0 && string.IsNullOrEmpty(ErrorMessage);
        var message = string.Empty;
        if (isEmpty)
        {
            message = string.IsNullOrWhiteSpace(StatusText) || StatusText == "Chưa tải dữ liệu."
                ? $"Không có dữ liệu nào khớp bộ lọc của bảng {Title}."
                : StatusText;
        }

        if (IsEmpty == isEmpty && string.Equals(EmptyStateMessage, message, StringComparison.Ordinal))
        {
            return;
        }

        IsEmpty = isEmpty;
        EmptyStateMessage = message;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    partial void OnIsLoadingChanged(bool value) => UpdateEmptyState();

    partial void OnIsLoadedChanged(bool value) => UpdateEmptyState();

    partial void OnStatusTextChanged(string value) => UpdateEmptyState();

    partial void OnErrorMessageChanged(string? value) => UpdateEmptyState();

    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _externalCancellation;
    private int _loadVersion;
    private bool _isExternalRequest;
    private bool _countInFlight;

    partial void OnIsActiveChanged(bool value)
    {
        if (value && LoadWhenActivated)
        {
            _ = EnsureLoadedAsync();
        }
    }

    partial void OnLoadWhenActivatedChanged(bool value)
    {
        if (!_suppressLoadOnActivation && value && IsActive)
        {
            _ = EnsureLoadedAsync();
        }
    }

    public async Task EnsureLoadedAsync()
    {
        if (!IsLoaded && !IsLoading && LoadWhenActivated)
        {
            await LoadPageAsync();
        }
    }

    public void SetLoadWhenActivated(bool value, bool loadIfActive = true)
    {
        if (LoadWhenActivated == value)
        {
            return;
        }

        _suppressLoadOnActivation = !loadIfActive;
        try
        {
            LoadWhenActivated = value;
        }
        finally
        {
            _suppressLoadOnActivation = false;
        }
    }

    public void ClearLoadedData()
    {
        DetachRows();
        ClearLoadedSelection();
        Items = [];
        OnPropertyChanged(nameof(Items));
        UpdateEmptyState();
        PageIndex = 1;
        TotalCount = 0;
        IsTotalCountKnown = false;
        HasMoreRows = false;
        ClearPageCursors();
        IsLoaded = false;
        ErrorMessage = null;
        StatusText = "Chưa tải dữ liệu.";
    }

    [RelayCommand]
    public Task RefreshAsync() => LoadWhenActivated ? LoadPageAsync() : Task.CompletedTask;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        _externalCancellation?.Cancel();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _externalCancellation = linked;
        _isExternalRequest = true;
        try
        {
            await LoadPageAsync();
        }
        finally
        {
            if (ReferenceEquals(_externalCancellation, linked))
            {
                _externalCancellation = null;
                _isExternalRequest = false;
            }
        }
    }

    public Task ResetAsync() => LoadWhenActivated
        ? GoToPageAsync(1, forceReload: true)
        : Task.CompletedTask;

    private async Task LoadPageAsync()
    {
        _loadCancellation?.Cancel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _isExternalRequest && _externalCancellation is { } external
                ? external.Token
                : CancellationToken.None);
        _loadCancellation = cancellation;
        var version = ++_loadVersion;
        var gateEntered = false;
        try
        {
            await _loadGate.WaitAsync(cancellation.Token);
            gateEntered = true;
            if (cancellation.IsCancellationRequested || version != _loadVersion)
            {
                return;
            }

            IsLoading = true;
            ErrorMessage = null;
            StatusText = $"Đang tải bảng {Title}...";
            var stopwatch = Stopwatch.StartNew();
            var result = await _loadService.LoadPageAsync(CreateQuery(), cancellation.Token);
            stopwatch.Stop();
            if (result.IsPageOutOfRange)
            {
                PageIndex = result.TotalPages;
                ClearPageCursors();
                result = await _loadService.LoadPageAsync(CreateQuery(), cancellation.Token);
            }

            if (version != _loadVersion)
            {
                return;
            }

            ApplyResult(result);
            OnPageApplied(result);
            await WarnIfTableScanAsync(stopwatch.Elapsed);

            if (!result.TotalCountKnown)
            {
                var versionAtStart = version;
                var queryForCount = CreateQuery();
                _ = LoadTotalCountInBackgroundAsync(queryForCount, versionAtStart);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (version == _loadVersion)
            {
                StatusText = $"Đã hủy tải bảng {Title}.";
            }
        }
        catch (Exception exception)
        {
            if (version == _loadVersion)
            {
                ErrorMessage = exception.Message;
                StatusText = $"Không tải được bảng {Title}.";
            }
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                IsLoading = false;
                _loadCancellation = null;
            }

            if (gateEntered)
            {
                _loadGate.Release();
            }
        }
    }

    private async Task LoadTotalCountInBackgroundAsync(TQuery query, int version)
    {
        if (_countInFlight)
        {
            return;
        }

        _countInFlight = true;
        try
        {
            var total = await _loadService.CountAsync(query, CancellationToken.None).ConfigureAwait(true);
            if (total < 0 || version != _loadVersion)
            {
                return;
            }

            TotalCount = total;
            IsTotalCountKnown = true;
            NotifyPagingChanged();
            if (Items.Count > 0)
            {
                StatusText = $"Hiển thị {FirstRowNumber}-{LastRowNumber} / {total:N0} dòng · {DateTime.Now:HH:mm:ss}";
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            _countInFlight = false;
        }
    }

    private void ApplyResult(PagedResult<TRow> result)
    {
        DetachRows();
        var nextItems = new ObservableCollection<TRow>();
        foreach (var row in result.Items)
        {
            row.IsSelected = IsRowSelected(row);
            row.PropertyChanged += OnRowPropertyChanged;
            nextItems.Add(row);
        }

        Items = nextItems;
        OnPropertyChanged(nameof(Items));
        UpdateEmptyState();
        UpdateSelectionState();
        OnPageLoaded(result.Items);
        IsTotalCountKnown = result.TotalCountKnown;
        HasMoreRows = result.HasMoreRows;
        if (result.TotalCountKnown)
        {
            TotalCount = result.TotalCount;
        }

        NotifyPagingChanged();
        IsLoaded = true;
        StatusText = result.Items.Count == 0
            ? "Không có dòng nào phù hợp."
            : result.TotalCountKnown
                ? $"Hiển thị {result.FirstRowNumber}-{result.LastRowNumber} / {result.TotalCount:N0} dòng · {DateTime.Now:HH:mm:ss}"
                : $"Đã tải nhanh {result.Items.Count} dòng; đang tính tổng số...";
    }

    private void DetachRows()
    {
        foreach (var row in Items)
        {
            row.PropertyChanged -= OnRowPropertyChanged;
        }
    }
}
