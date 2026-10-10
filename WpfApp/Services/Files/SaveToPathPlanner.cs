using System.IO;
using WpfApp.Models.Files;

namespace WpfApp.Services.Files;

public static class SaveToPathPlanner
{
    /// <summary>Tên kiểu 12:10:22 → 12.10.22 (giữ đuôi tệp gốc).</summary>
    public static string BuildTimeFileName(DateTime start, string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName ?? string.Empty);
        return start.ToString("HH.mm.ss") + extension;
    }

    public static string BuildPlanFileName(string fileName, SaveToConflictMode mode)
    {
        var safe = string.IsNullOrWhiteSpace(fileName) ? "file" : fileName.Trim();
        if (mode == SaveToConflictMode.Overwrite)
        {
            return safe;
        }

        var name = Path.GetFileNameWithoutExtension(safe);
        var extension = Path.GetExtension(safe);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "file";
        }

        return $"{name} (1){extension}";
    }

    public static string ResolveNumberedPath(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "file";
        }

        var candidate = path;
        var copy = 1;
        while (File.Exists(candidate) || Directory.Exists(candidate))
        {
            copy++;
            candidate = Path.Combine(directory, $"{name} ({copy}){extension}");
        }

        return candidate;
    }

    public static string SanitizeFolderName(string name)
    {
        var cleaned = (name ?? string.Empty).Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            cleaned = cleaned.Replace(invalid, '_');
        }

        cleaned = cleaned.Trim().TrimEnd('.');
        return cleaned.Length == 0 ? "Session" : cleaned;
    }
}
