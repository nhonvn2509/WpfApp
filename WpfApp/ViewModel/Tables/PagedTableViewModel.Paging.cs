using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Data;
using WpfApp.Services.Tables;

namespace WpfApp.ViewModel.Tables;

public abstract partial class PagedTableViewModel<TQuery, TRow>
{
    [ObservableProperty]
    private int _pageIndex = 1;

    [ObservableProperty]
    private int _pageSize = TablePage.DefaultPageSize;

    [ObservableProperty]
    private long _totalCount;

    [ObservableProperty]
    private bool _isTotalCountKnown;

    [ObservableProperty]
    private bool _hasMoreRows;

    private string? _queryKeysetCursorJson;
    public int TotalPages => !IsTotalCountKnown
        ? Math.Max(PageIndex, 1)
        : TotalCount <= 0
            ? 1
            : (int)Math.Min(int.MaxValue, Math.Ceiling(TotalCount / (double)PageSize));
    public int PaginationMaxPageCount => IsTotalCountKnown
        ? TotalPages
        : Math.Max(PageIndex + (HasMoreRows ? 1 : 0), 1);
    public int FirstRowNumber => Items.Count == 0 ? 0 : ((PageIndex - 1) * PageSize) + 1;
    public int LastRowNumber => Items.Count == 0 ? 0 : FirstRowNumber + Items.Count - 1;
    public string PageText => IsTotalCountKnown
        ? $"Trang {PageIndex}/{TotalPages}"
        : $"Trang {PageIndex}/…";
    public bool CanGoPreviousPage => PageIndex > 1;
    public bool CanGoNextPage => IsTotalCountKnown ? PageIndex < TotalPages : HasMoreRows;
    public IReadOnlyList<int> PageNumberOptions
    {
        get
        {
            if (!IsTotalCountKnown)
            {
                return [];
            }

            var totalPages = TotalPages;
            if (totalPages <= 7)
            {
                return Enumerable.Range(1, totalPages).ToArray();
            }

            return PageIndex <= 4
                ? [1, 2, 3, 4, 5, 0, totalPages]
                : PageIndex >= totalPages - 3
                    ? [1, 0, totalPages - 4, totalPages - 3, totalPages - 2, totalPages - 1, totalPages]
                    : [1, 0, PageIndex - 1, PageIndex, PageIndex + 1, 0, totalPages];
        }
    }

    public string? QueryKeysetCursorJson
    {
        get => _queryKeysetCursorJson;
        private set => SetProperty(ref _queryKeysetCursorJson, value);
    }

    [RelayCommand]
    private Task GoToPageNumberAsync(int pageNumber) => pageNumber > 0
        ? GoToPageAsync(pageNumber)
        : Task.CompletedTask;

    [RelayCommand]
    private Task PageUpdatedAsync(FunctionEventArgs<int> args) =>
        args.Info == PageIndex && IsLoaded ? Task.CompletedTask : GoToPageAsync(args.Info);

    [RelayCommand]
    public Task FirstPageAsync() => GoToPageAsync(1);

    [RelayCommand]
    public Task PreviousPageAsync() => GoToPageAsync(PageIndex - 1);

    [RelayCommand]
    public Task NextPageAsync() => GoToPageAsync(PageIndex + 1);

    [RelayCommand]
    public Task LastPageAsync() => GoToPageAsync(PaginationMaxPageCount);

    partial void OnPageSizeChanged(int value)
    {
        if (IsLoaded)
        {
            _ = ResetAsync();
        }
    }

    partial void OnPageIndexChanged(int value) => NotifyPagingChanged();

    protected void StorePageCursor(string? cursorJson) => QueryKeysetCursorJson = cursorJson;

    protected virtual void OnPageLoaded(IReadOnlyList<TRow> rows)
    {
    }

    public void ClearPageCursors() => QueryKeysetCursorJson = null;

    public void SetTotalCount(long totalCount)
    {
        TotalCount = totalCount;
        IsTotalCountKnown = true;
        HasMoreRows = PageIndex < TotalPages;
        NotifyPagingChanged();
        StatusText = Items.Count == 0
            ? "Không có dòng nào phù hợp."
            : $"Hiển thị {FirstRowNumber}-{LastRowNumber} / {totalCount:N0} dòng · {DateTime.Now:HH:mm:ss}";
    }

    private void NotifyPagingChanged()
    {
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PaginationMaxPageCount));
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(PageNumberOptions));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
    }

    private async Task GoToPageAsync(int pageIndex, bool forceReload = false)
    {
        if (!LoadWhenActivated)
        {
            return;
        }

        var target = Math.Clamp(pageIndex, 1, PaginationMaxPageCount);
        if (!forceReload && target == PageIndex && IsLoaded)
        {
            return;
        }

        if (IsLoading)
        {
            return;
        }

        PageIndex = target;
        await LoadPageAsync();
    }
}
