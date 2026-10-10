using WpfApp.Models.Tables;
namespace WpfApp.Services.Tables;

public interface IChainsLoadService : ITableLoadService<ChainsQuery, ChainRow>
{
    Task<DateTime?> LoadLatestSessionDayAsync(CancellationToken cancellationToken = default);
}
