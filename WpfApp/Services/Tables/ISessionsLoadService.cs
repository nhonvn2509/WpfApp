using WpfApp.Models.Tables;
namespace WpfApp.Services.Tables;

public interface ISessionsLoadService : ITableLoadService<SessionsQuery, SessionRow>
{
    Task<IReadOnlyList<TableFilterOption>> LoadStatusOptionsAsync(CancellationToken cancellationToken = default);

    Task<long> CountMatchingFilesAsync(SessionsQuery query, CancellationToken cancellationToken = default);

    Task<string?> LoadSessionXmlAsync(long sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, DateTime>> LoadFileStartsAsync(long sessionId, CancellationToken cancellationToken = default);
}
