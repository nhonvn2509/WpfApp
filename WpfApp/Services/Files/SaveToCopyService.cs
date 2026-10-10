using System.IO;
using WpfApp.Models.Files;

namespace WpfApp.Services.Files;

public sealed record SaveToProgress(
    int SessionsTotal,
    int SessionsDone,
    int FilesTotal,
    int FilesDone,
    int FilesFailed,
    string CurrentFile,
    double CurrentFilePercent,
    double TotalPercent);

public sealed class SaveToCopyService
{
    public const int BufferSize = 81920;

    public async Task<SaveToCopyResult> CopyAsync(
        IReadOnlyList<SaveToFileItem> files,
        string destination,
        SaveToOptions options,
        IProgress<SaveToProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        Directory.CreateDirectory(destination);
        var sessionsTotal = files.Select(static item => item.SessionId).Distinct().Count();
        var sessionsDone = new HashSet<long>();
        var copied = 0;
        var failed = 0;
        var errors = new List<string>();
        for (var index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[index];
            try
            {
                var targetFolder = options.MakeSessionFolder
                    ? Path.Combine(destination, SaveToPathPlanner.SanitizeFolderName(file.SessionName))
                    : destination;
                Directory.CreateDirectory(targetFolder);
                var renamedByStart = options.RenameByTime && file.Start is not null;
                var fileName = options.RenameByTime && file.Start is { } start
                    ? SaveToPathPlanner.BuildTimeFileName(start, file.FileName)
                    : file.FileName;
                // Trùng giờ khi đổi tên theo DT_START: luôn đánh số (2), (3)... thay vì ghi đè,
                // bất kể chế độ trùng tên đang chọn (chỉ áp dụng cho tệp đã đổi tên).
                var conflictMode = renamedByStart ? SaveToConflictMode.Overwrite : options.ConflictMode;
                var targetPath = Path.Combine(targetFolder, SaveToPathPlanner.BuildPlanFileName(fileName, conflictMode));
                if (renamedByStart || options.ConflictMode == SaveToConflictMode.AddCopyNumber)
                {
                    targetPath = SaveToPathPlanner.ResolveNumberedPath(targetPath);
                }

                if (string.IsNullOrWhiteSpace(file.SourcePath))
                {
                    if (string.IsNullOrWhiteSpace(file.HeaderXml))
                    {
                        throw new FileNotFoundException($"Không có nội dung để lưu cho {file.FileName}.");
                    }

                    await File.WriteAllTextAsync(targetPath, file.HeaderXml, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    if (!File.Exists(file.SourcePath))
                    {
                        throw new FileNotFoundException($"Không tìm thấy tệp nguồn: {file.SourcePath}");
                    }

                    await CopyFileAsync(file.SourcePath, targetPath, options.ConflictMode, progress, file.FileName, cancellationToken).ConfigureAwait(false);
                }

                copied++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                failed++;
                errors.Add($"{file.FileName}: {exception.Message}");
            }

            sessionsDone.Add(file.SessionId);
            progress?.Report(new SaveToProgress(
                sessionsTotal,
                sessionsDone.Count,
                files.Count,
                copied + failed,
                failed,
                file.FileName,
                100,
                files.Count == 0 ? 100 : ((copied + failed) * 100d / files.Count)));
        }

        return new SaveToCopyResult(sessionsTotal, sessionsDone.Count, copied, failed, errors);
    }

    public static async Task CopySingleAsync(string source, string target, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        var directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        await using var targetStream = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        await sourceStream.CopyToAsync(targetStream, cancellationToken).ConfigureAwait(false);
    }

    private static async Task CopyFileAsync(
        string source,
        string target,
        SaveToConflictMode mode,
        IProgress<SaveToProgress>? progress,
        string displayName,
        CancellationToken cancellationToken)
    {
        var modeCreate = mode == SaveToConflictMode.Overwrite ? FileMode.Create : FileMode.CreateNew;
        await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        await using var targetStream = new FileStream(target, modeCreate, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        var buffer = new byte[BufferSize];
        long totalRead = 0;
        int read;
        while ((read = await sourceStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await targetStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            totalRead += read;
            if (sourceStream.Length > 0)
            {
                progress?.Report(new SaveToProgress(0, 0, 0, 0, 0, displayName, totalRead * 100d / sourceStream.Length, 0));
            }
        }
    }
}
