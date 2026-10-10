using WpfApp.Models.Settings;

namespace WpfApp.Services.Tables;

public readonly record struct TablePage
{
    public const int DefaultPageSize = UserPreferences.DefaultPageSize;
    public const int MinPageSize = 5;
    public static readonly IReadOnlyList<int> SizeOptions = [25, 50, 100, 200, 500];
    public const int MaxPageSize = 1000;
    public TablePage(int pageIndex, int pageSize)
    {
        PageIndex = pageIndex < 1 ? 1 : pageIndex;
        PageSize = ClampPageSize(pageSize);
    }
    public int PageIndex { get; }
    public int PageSize { get; }
    public int Skip => (PageIndex - 1) * PageSize;
    public int Take => PageSize;
    public static TablePage First(int pageSize) => new(1, pageSize);
    public static int ClampPageSize(int pageSize) =>
        pageSize < MinPageSize ? MinPageSize : pageSize > MaxPageSize ? MaxPageSize : pageSize;
}
