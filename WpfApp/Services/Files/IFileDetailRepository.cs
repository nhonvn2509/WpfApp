using WpfApp.Models.Files;
using WpfApp.Models.Tables;

namespace WpfApp.Services.Files;

public interface IFileDetailRepository
{
    Task<FileDetailRecord?> GetAsync(FileRow row, CancellationToken cancellationToken = default);
}
