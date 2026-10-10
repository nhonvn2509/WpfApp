using System.Collections.Generic;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using WpfApp.Enums;
using WpfApp.Models.Files;
using WpfApp.Models.Tables;
using WordTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;

namespace WpfApp.Services.Files;

public sealed class WordDocumentBuilder : IWordDocumentBuilder
{
    private const string FontName = "Times New Roman";
    private const string HeaderFill = "DCE6F1";
    private const string DateFormat = "dd/MM/yyyy HH:mm:ss";
    private const int TitleFontSize = 32;
    private const int MetaFontSize = 20;
    private const int TableFontSize = 18;
    private const int MaxExportRows = 20_000;
    private static readonly int[] ColumnWidths = [600, 800, 900, 2200, 700, 900, 1100, 800, 1400, 4270, 900];
    private static readonly string[] ColumnHeaders =
        ["STT", "Mã", "Phiên", "Tên file", "Đuôi", "Dung lượng", "Trạng thái", "Chất lượng", "Thời gian", "Đường dẫn", "Tồn tại"];
    private readonly IFilePathResolver _pathResolver;

    public WordDocumentBuilder(IFilePathResolver pathResolver)
    {
        ArgumentNullException.ThrowIfNull(pathResolver);
        _pathResolver = pathResolver;
    }

    public int WrittenRowCount { get; private set; }

    public async Task WriteAsync(
        OpenXmlWriter writer,
        ExportWordRequest request,
        IAsyncEnumerable<FileRow> rows,
        int declaredRowCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rows);

        WrittenRowCount = 0;
        writer.WriteStartElement(new Document());
        writer.WriteStartElement(new Body());
        WriteParagraph(writer, "DANH SÁCH TỆP TIN", TitleFontSize, bold: true, JustificationValues.Center);
        WriteParagraph(writer, $"Kiểu xuất: {request.Type.ToDisplayName()}", MetaFontSize);
        WriteParagraph(writer, $"Điều kiện lọc: {request.Query.Describe()}", MetaFontSize);
        if (request.Type == ExportType.ExportFollowSelected)
        {
            WriteParagraph(writer, "Chỉ gồm các dòng đang được đánh dấu trong các trang đã chọn.", MetaFontSize);
        }

        WriteParagraph(writer, $"Thời điểm xuất: {DateTime.Now.ToString(DateFormat)}", MetaFontSize);
        WriteParagraph(writer, $"Số dòng dự kiến: {declaredRowCount:N0}", MetaFontSize);
        WriteTableHeader(writer);

        var enumerator = rows.GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    break;
                }

                if (WrittenRowCount >= MaxExportRows)
                {
                    break;
                }

                WriteDataRow(writer, ++WrittenRowCount, enumerator.Current);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        writer.WriteEndElement();
        writer.WriteElement(CreateSectionProperties());
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteParagraph(
        OpenXmlWriter writer,
        string text,
        int fontSize,
        bool bold = false,
        JustificationValues? alignment = null)
    {
        var properties = new ParagraphProperties(
            new SpacingBetweenLines { After = "80", Line = "240", LineRule = LineSpacingRuleValues.Auto });
        if (alignment is { } justification)
        {
            properties.Append(new Justification { Val = justification });
        }

        writer.WriteStartElement(properties);
        writer.WriteStartElement(new Run(CreateRunProperties(fontSize, bold)));
        writer.WriteElement(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteTableHeader(OpenXmlWriter writer)
    {
        writer.WriteStartElement(new Table(
            new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                CreateBorders(),
                new TableLayout { Type = TableLayoutValues.Fixed }),
            new TableGrid(ColumnWidths.Select(width => new GridColumn { Width = width.ToString() }))));
        writer.WriteStartElement(new WordTableRow(new TableRowProperties(new TableHeader())));
        for (var index = 0; index < ColumnHeaders.Length; index++)
        {
            WriteCell(writer, ColumnHeaders[index], ColumnWidths[index], TableFontSize, bold: true, HeaderFill);
        }

        writer.WriteEndElement();
    }

    private void WriteDataRow(OpenXmlWriter writer, int order, FileRow row)
    {
        var values = new[]
        {
            order.ToString("N0"),
            row.IdF.ToString("N0"),
            row.IdS.ToString("N0"),
            row.Name ?? "—",
            row.Extension ?? "—",
            FileSizeFormatter.Format(row.Length),
            row.Status ?? "—",
            row.Quality?.ToString() ?? "—",
            row.Start?.ToString(DateFormat) ?? "—",
            _pathResolver.GetFullPath(row) ?? row.Path ?? "—",
            FormatBool(row.Exists)
        };
        writer.WriteStartElement(new WordTableRow());
        for (var index = 0; index < values.Length; index++)
        {
            WriteCell(writer, values[index], ColumnWidths[index], TableFontSize);
        }

        writer.WriteEndElement();
    }

    private static void WriteCell(
        OpenXmlWriter writer,
        string text,
        int width,
        int fontSize,
        bool bold = false,
        string? shading = null)
    {
        var properties = new TableCellProperties(new TableCellWidth
        {
            Type = TableWidthUnitValues.Dxa,
            Width = width.ToString()
        });
        if (shading is not null)
        {
            properties.Append(new Shading
            {
                Val = ShadingPatternValues.Clear,
                Color = "auto",
                Fill = shading
            });
        }

        properties.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
        writer.WriteStartElement(properties);
        writer.WriteElement(new ParagraphProperties(
            new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }));
        writer.WriteStartElement(new Run(CreateRunProperties(fontSize, bold)));
        writer.WriteElement(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        writer.WriteEndElement();
        writer.WriteEndElement();
    }
    private static TableBorders CreateBorders() => new(
        new TopBorder { Val = BorderValues.Single, Size = 4, Color = "808080" },
        new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "808080" },
        new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "808080" },
        new RightBorder { Val = BorderValues.Single, Size = 4, Color = "808080" },
        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "808080" },
        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "808080" });

    private static RunProperties CreateRunProperties(int fontSize, bool bold)
    {
        var properties = new RunProperties(new RunFonts
        {
            Ascii = FontName,
            HighAnsi = FontName,
            EastAsia = FontName,
            ComplexScript = FontName
        });
        if (bold)
        {
            properties.Append(new Bold(), new BoldComplexScript());
        }

        properties.Append(new FontSize { Val = fontSize.ToString() }, new FontSizeComplexScript { Val = fontSize.ToString() });
        return properties;
    }

    private static string FormatBool(bool? value) => value switch
    {
        true => "Có",
        false => "Không",
        _ => "—"
    };

    private static SectionProperties CreateSectionProperties() => new(
        new PageSize { Width = 16838, Height = 11906, Orient = PageOrientationValues.Landscape },
        new PageMargin
        {
            Top = 1134,
            Right = 1134,
            Bottom = 1134,
            Left = 1134,
            Header = 709,
            Footer = 709,
            Gutter = 0
        });
}
