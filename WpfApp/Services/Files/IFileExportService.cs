using WpfApp.Models.Files;

namespace WpfApp.Services.Files;

public interface IFileExportService
{
    Task<ExportWordResult> ExportAsync(
        ExportWordRequest request,
        string targetFilePath,
        CancellationToken cancellationToken = default);
}
