using WpfApp.Models.Files;
using WpfApp.Models.Tables;
namespace WpfApp.Services.Tables;

public interface IFilesLoadService : ITableLoadService<FilesQuery, FileRow>
{
    Task<FileExtensionStatistics> LoadExtensionStatisticsAsync(
        FilesQuery query,
        CancellationToken cancellationToken = default);

    Task<FileRow?> LoadFirstFileOfSessionAsync(
        long sessionId,
        byte dayFgr,
        CancellationToken cancellationToken = default);
}
