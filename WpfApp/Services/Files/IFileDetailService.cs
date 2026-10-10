using WpfApp.Models;
using WpfApp.Models.Tables;

namespace WpfApp.Services.Files;

public interface IFileDetailService
{
    Task<PropertyDetailReport> GetAsync(FileRow row, CancellationToken cancellationToken = default);
}
