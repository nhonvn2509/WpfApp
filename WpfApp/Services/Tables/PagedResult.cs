namespace WpfApp.Services.Tables;

public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required TablePage Page { get; init; }
    public required long TotalCount { get; init; }
    public bool TotalCountKnown { get; init; } = true;
    public bool HasMoreRows { get; init; }
    public int TotalPages => TotalCount <= 0
        ? 1
        : (int)Math.Min(int.MaxValue, Math.Ceiling(TotalCount / (double)Page.PageSize));
    public int EffectivePageIndex => TotalCountKnown
        ? Math.Min(Page.PageIndex, TotalPages)
        : Page.PageIndex;
    public int FirstRowNumber => Items.Count == 0
        ? 0
        : ((Page.PageIndex - 1) * Page.PageSize) + 1;
    public int LastRowNumber => Items.Count == 0 ? 0 : FirstRowNumber + Items.Count - 1;
    public bool IsPageOutOfRange => TotalCountKnown && Page.PageIndex > TotalPages;
    public static PagedResult<T> Create(IReadOnlyList<T> items, TablePage page, long totalCount)
    {
        var totalPages = totalCount <= 0
            ? 1
            : (int)Math.Min(int.MaxValue, Math.Ceiling(totalCount / (double)page.PageSize));
        return new()
        {
            Items = items,
            Page = page,
            TotalCount = totalCount,
            TotalCountKnown = true,
            HasMoreRows = page.PageIndex < totalPages
        };
    }
    public static PagedResult<T> CreateUnknownTotal(
        IReadOnlyList<T> items,
        TablePage page,
        bool hasMoreRows) =>
        new()
        {
            Items = items,
            Page = page,
            TotalCount = 0,
            TotalCountKnown = false,
            HasMoreRows = hasMoreRows
        };
}
