using System.Text.Json;
using WpfApp.Enums;
using WpfApp.Models.Files;
using WpfApp.Models.Tables;
using WpfApp.Services.Tables;

namespace WpfApp.Services.Files;

public sealed class FileExportService : IFileExportService
{
    public const int MaxExportRows = 20_000;
    private const int ExportPageSize = 2_000;
    private readonly IFilesLoadService _filesLoadService;
    private readonly IWordExportService _wordExportService;

    public FileExportService(IFilesLoadService filesLoadService, IWordExportService wordExportService)
    {
        _filesLoadService = filesLoadService;
        _wordExportService = wordExportService;
    }

    public async Task<ExportWordResult> ExportAsync(
        ExportWordRequest request,
        string targetFilePath,
        CancellationToken cancellationToken = default)
    {
        if (request.Type == ExportType.ExportFollowSelected)
        {
            if (request.SelectedRows.Count == 0)
            {
                throw new InvalidOperationException("Không có dòng nào để xuất.");
            }

            return await _wordExportService
                .ExportAsync(request, request.SelectedRows, targetFilePath, cancellationToken)
                .ConfigureAwait(false);
        }

        if (request.Type != ExportType.ExportFollowCurrentFilter)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Type, "Kiểu xuất không được hỗ trợ.");
        }

        var total = await CountMatchingRowsAsync(request.Query, cancellationToken).ConfigureAwait(false);
        if (total == 0)
        {
            throw new InvalidOperationException("Không có dòng nào khớp điều kiện lọc để xuất.");
        }

        var truncated = total > MaxExportRows;
        if (truncated)
        {
            throw new InvalidOperationException(
                $"Điều kiện lọc đang cho ra {total:N0} dòng, vượt quá giới hạn {MaxExportRows:N0} dòng. " +
                "Hãy thu hẹp điều kiện lọc hoặc dùng cách xuất theo các dòng đã đánh dấu.");
        }

        return await _wordExportService
            .ExportAsync(
                request,
                StreamRowsAsync(request.Query, cancellationToken),
                (int)total,
                targetFilePath,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<long> CountMatchingRowsAsync(FilesQuery query, CancellationToken cancellationToken)
    {
        var total = await _filesLoadService.CountAsync(query, cancellationToken).ConfigureAwait(false);
        return total < 0 ? 0 : total;
    }

    private async IAsyncEnumerable<FileRow> StreamRowsAsync(
        FilesQuery query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? cursorJson = null;
        var pageIndex = 1;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await _filesLoadService.LoadPageAsync(
                query with
                {
                    Page = new TablePage(pageIndex, ExportPageSize),
                    KeysetCursorJson = cursorJson
                },
                cancellationToken).ConfigureAwait(false);
            foreach (var row in page.Items)
            {
                yield return row;
            }

            if (page.Items.Count == 0 || !page.HasMoreRows)
            {
                yield break;
            }

            var last = page.Items[^1];
            cursorJson = JsonSerializer.Serialize(new { IdF = last.IdF, DayFgr = last.DayFgr });
            pageIndex++;
        }
    }
}
