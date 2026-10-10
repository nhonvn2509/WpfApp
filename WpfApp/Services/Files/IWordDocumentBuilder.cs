using System.Collections.Generic;
using DocumentFormat.OpenXml;
using WpfApp.Models.Files;
using WpfApp.Models.Tables;

namespace WpfApp.Services.Files;

public interface IWordDocumentBuilder
{
    Task WriteAsync(
        OpenXmlWriter writer,
        ExportWordRequest request,
        IAsyncEnumerable<FileRow> rows,
        int declaredRowCount,
        CancellationToken cancellationToken);
}
