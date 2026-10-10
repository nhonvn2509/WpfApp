using WpfApp.Models.Database;

namespace WpfApp.Services.Data;

public interface IDatabaseInsightsService
{
    Task<bool> IsColumnIndexedAsync(string qualifiedTable, string column, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TableStorageInsight>> GetStorageAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IndexInsight>> GetIndexesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SlowQueryInsight>> GetTopSlowQueriesAsync(int topCount, CancellationToken cancellationToken = default);
}
