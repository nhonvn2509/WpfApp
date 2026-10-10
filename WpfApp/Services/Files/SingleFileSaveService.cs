using System.IO;
using WpfApp.Models.Files;
using WpfApp.Models.Tables;
using WpfApp.Services.Tables;

namespace WpfApp.Services.Files;

public interface ISingleFileSaveService
{
    string BuildFilter(string fileName);
    string DefaultFileName(string fileName);
    Task SaveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
    Task SaveHeaderAsync(string headerXml, string destinationPath, CancellationToken cancellationToken = default);
}

public sealed class SingleFileSaveService : ISingleFileSaveService
{
    public string BuildFilter(string fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty).Trim('.').Trim();
        return extension.Length == 0
            ? "Tất cả các file (*.*)|*.*"
            : $"File đã chọn (*.{extension})|*.{extension}|Tất cả các file (*.*)|*.*";
    }

    public string DefaultFileName(string fileName) =>
        string.IsNullOrWhiteSpace(fileName) ? "file" : Path.GetFileName(fileName.Trim());

    public Task SaveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Không tìm thấy tệp nguồn: {sourcePath}");
        }

        return SaveToCopyService.CopySingleAsync(sourcePath, destinationPath, cancellationToken);
    }

    public async Task SaveHeaderAsync(string headerXml, string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        if (string.IsNullOrWhiteSpace(headerXml))
        {
            throw new InvalidOperationException("Header của phiên đang trống nên không lưu được.");
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(destinationPath, headerXml, cancellationToken).ConfigureAwait(false);
    }
}

public sealed record MultiFileSaveResult(int Copied, int Failed, IReadOnlyList<string> Errors);

public interface IMultiFileSaveService
{
    Task<MultiFileSaveResult> SaveAsync(
        IReadOnlyList<FileRow> rows,
        Func<FileRow, string?> resolveSource,
        Func<string, string?> askFirstDestination,
        CancellationToken cancellationToken = default);
}

public sealed class MultiFileSaveService : IMultiFileSaveService
{
    public async Task<MultiFileSaveResult> SaveAsync(
        IReadOnlyList<FileRow> rows,
        Func<FileRow, string?> resolveSource,
        Func<string, string?> askFirstDestination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(resolveSource);
        ArgumentNullException.ThrowIfNull(askFirstDestination);
        var copied = 0;
        var errors = new List<string>();
        string? folder = null;
        var first = true;
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = resolveSource(row);
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
            {
                errors.Add($"{row.FullName}: không tìm thấy tệp nguồn.");
                continue;
            }

            try
            {
                string target;
                if (first)
                {
                    first = false;
                    var chosen = askFirstDestination(source);
                    if (string.IsNullOrWhiteSpace(chosen))
                    {
                        break;
                    }

                    folder = Path.GetDirectoryName(chosen);
                    target = chosen;
                }
                else
                {
                    target = Path.Combine(folder ?? string.Empty, Path.GetFileName(source));
                }

                await SaveToCopyService.CopySingleAsync(source, target, cancellationToken).ConfigureAwait(false);
                copied++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                errors.Add($"{row.FullName}: {exception.Message}");
            }
        }

        return new MultiFileSaveResult(copied, errors.Count, errors);
    }
}
