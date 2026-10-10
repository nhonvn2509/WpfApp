using WpfApp.Models;
using WpfApp.Models.Files;
using WpfApp.Models.Tables;

namespace WpfApp.Services.Files;

public interface IFileDetailReportBuilder
{
    PropertyDetailReport Build(
        FileRow row,
        FileDetailRecord? record,
        string? fullPath);
}
