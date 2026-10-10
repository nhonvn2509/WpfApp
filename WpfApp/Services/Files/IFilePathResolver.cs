using WpfApp.Models.Tables;

namespace WpfApp.Services.Files;

public interface IFilePathResolver
{
    string? GetFullPath(FileRow row);
    string? GetFullPath(string? folder, string? shortName, string? extension);
}
