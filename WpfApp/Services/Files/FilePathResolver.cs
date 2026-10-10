using System.IO;
using WpfApp.Models.Tables;
using WpfApp.Services;

namespace WpfApp.Services.Files;

public sealed class FilePathResolver : IFilePathResolver
{
    private const string RootSection = "Files";
    private const string RootKey = "RootPath";
    private readonly IAppSettings _settings;

    public FilePathResolver(IAppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    private string? RootPath
    {
        get
        {
            var root = _settings.GetString(RootSection, RootKey);
            return string.IsNullOrWhiteSpace(root) ? null : root.Trim();
        }
    }

    public string? GetFullPath(FileRow row) =>
        GetFullPath(row.Path, row.Name, row.Extension);

    public string? GetFullPath(string? folder, string? shortName, string? extension)
    {
        if (string.IsNullOrWhiteSpace(shortName))
        {
            return null;
        }

        var rootPath = RootPath;
        var fileName = string.IsNullOrWhiteSpace(extension)
            ? shortName.Trim()
            : $"{shortName.Trim()}.{extension.Trim().TrimStart('.')}";
        var directory = folder?.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return string.IsNullOrWhiteSpace(rootPath) ? fileName : Path.Combine(rootPath, fileName);
        }

        var combined = Path.Combine(directory, fileName);
        return Path.IsPathRooted(combined) || string.IsNullOrWhiteSpace(rootPath)
            ? combined
            : Path.Combine(rootPath, combined);
    }
}
