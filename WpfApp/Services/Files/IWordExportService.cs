using WpfApp.Models.Files;
using WpfApp.Models.Tables;

namespace WpfApp.Services.Files;

public interface IWordExportService
{
    Task<ExportWordResult> ExportAsync(
        ExportWordRequest request,
        IReadOnlyList<FileRow> rows,
        string targetFilePath,
        CancellationToken cancellationToken = default);

    Task<ExportWordResult> ExportAsync(
        ExportWordRequest request,
        IAsyncEnumerable<FileRow> rows,
        int declaredRowCount,
        string targetFilePath,
        CancellationToken cancellationToken = default);
}
