using WpfApp.Services.Data;

namespace WpfApp.Services.Tables;

public interface ITableLoadService<in TQuery, TRow>
{
    Task<PagedResult<TRow>> LoadPageAsync(TQuery query, CancellationToken cancellationToken = default);

    Task<long> CountAsync(TQuery query, CancellationToken cancellationToken = default);
}
