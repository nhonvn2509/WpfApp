using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using WpfApp.Models.Files;
using WpfApp.Models.Tables;

namespace WpfApp.Services.Files;

public sealed class WordExportService : IWordExportService
{
    private readonly IWordDocumentBuilder _documentBuilder;

    public WordExportService(IWordDocumentBuilder documentBuilder)
    {
        ArgumentNullException.ThrowIfNull(documentBuilder);
        _documentBuilder = documentBuilder;
    }

    public Task<ExportWordResult> ExportAsync(
        ExportWordRequest request,
        IReadOnlyList<FileRow> rows,
        string targetFilePath,
        CancellationToken cancellationToken = default) =>
        ExportCoreAsync(
            request,
            ToAsyncEnumerable(rows, cancellationToken),
            rows.Count,
            targetFilePath,
            cancellationToken);

    public Task<ExportWordResult> ExportAsync(
        ExportWordRequest request,
        IAsyncEnumerable<FileRow> rows,
        int declaredRowCount,
        string targetFilePath,
        CancellationToken cancellationToken = default) =>
        ExportCoreAsync(request, rows, declaredRowCount, targetFilePath, cancellationToken);

    private static async IAsyncEnumerable<FileRow> ToAsyncEnumerable(
        IReadOnlyList<FileRow> rows,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
            await Task.Yield();
        }
    }

    private async Task<ExportWordResult> ExportCoreAsync(
        ExportWordRequest request,
        IAsyncEnumerable<FileRow> rows,
        int declaredRowCount,
        string targetFilePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFilePath);
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var package = WordprocessingDocument.Create(targetFilePath, WordprocessingDocumentType.Document);
        var mainPart = package.AddMainDocumentPart();
        using (var stream = mainPart.GetStream(FileMode.Create, FileAccess.Write))
        using (var writer = OpenXmlWriter.Create(stream))
        {
            await _documentBuilder.WriteAsync(writer, request, rows, declaredRowCount, cancellationToken)
                .ConfigureAwait(false);
        }

        return new ExportWordResult(targetFilePath, request.Type, declaredRowCount);
    }
}
