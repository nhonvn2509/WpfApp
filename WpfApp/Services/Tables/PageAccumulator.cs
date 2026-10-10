namespace WpfApp.Services.Tables;

public static class PageAccumulator
{
    public static async Task<(List<T> Items, bool Truncated)> LoadAllAsync<T>(
        Func<TablePage, Task<PagedResult<T>>> loadPage,
        int pageSize,
        int hardLimit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loadPage);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(hardLimit, 1);

        var all = new List<T>();
        for (var pageIndex = 1; ; pageIndex++)
        {
            var result = await loadPage(new TablePage(pageIndex, pageSize)).ConfigureAwait(true);

            if (result.Items.Count == 0)
            {
                return (all, false);
            }

            all.AddRange(result.Items);
            if (!result.HasMoreRows)
            {
                return (all, false);
            }

            if (all.Count >= hardLimit)
            {
                return (all, true);
            }
        }
    }
}
