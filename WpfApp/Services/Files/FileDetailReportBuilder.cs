using System.IO;
using WpfApp.Models;
using WpfApp.Models.Files;
using WpfApp.Models.Tables;

namespace WpfApp.Services.Files;

public sealed class FileDetailReportBuilder : IFileDetailReportBuilder
{
    private const string DateFormat = "dd/MM/yyyy HH:mm:ss";
    private const int MaxXmlPreviewChars = 2000;

    public PropertyDetailReport Build(
        FileRow row,
        FileDetailRecord? record,
        string? fullPath)
    {
        var fileName = BuildFileName(row.Name, row.Extension);
        var general = new List<PropertyDetailItem>
        {
            new("Mã tệp (ID_F)", row.IdF.ToString("N0")),
            new("Phiên (ID_S)", row.IdS.ToString("N0")),
            new("Tên phiên", Display(record?.SessionName)),
            new("Tên tệp (SHORT_NAME)", row.Name ?? "—"),
            new("Phần mở rộng (EXT)", row.Extension ?? "—"),
            new("Dung lượng ghi nhận (LEN)", FileSizeFormatter.Format(row.Length)),
            new("Thời gian (DT_START)", FormatDate(row.Start)),
            new("Trạng thái (ID_STATUS)", FormatIdWithName(record?.StatusId, record?.StatusName)),
            new("Loại tệp (ID_TYPE)", FormatIdWithName(record?.TypeId, record?.TypeName)),
            new("Chất lượng (QUALITY)", row.Quality?.ToString() ?? "—"),
            new("Ưu tiên (PRIOR)", record?.Priority?.ToString() ?? "—"),
            new("Người dùng chọn (USER_SEL)", record?.UserSelected?.ToString() ?? "—"),
            new("Ngày phân vùng (DAY_FGR)", record?.DayFgr.ToString() ?? "—")
        };
        var storage = new List<PropertyDetailItem>
        {
            new("Lưu trữ (ID_STOR)", record?.StorageId?.ToString() ?? "—"),
            new("Cách lưu (ID_SAVE)", record?.SaveId?.ToString() ?? "—"),
            new("Ngôn ngữ (ID_LANG)", record?.LanguageId?.ToString() ?? "—"),
            new("FN / FP", record is null ? "—" : $"{record.Fn?.ToString() ?? "—"} / {record.Fp?.ToString() ?? "—"}"),
            new("MD5", record?.Md5 is { } md5 ? md5.ToString("N").ToUpperInvariant() : "—"),
            new("Còn tồn tại theo CSDL (IS_EXISTS)", FormatBool(record?.Exists))
        };
        var groups = new List<PropertyDetailGroup>
        {
            new("Thông tin chung", general),
            new("Lưu trữ", storage),
            new("Tệp trên đĩa", BuildDiskGroup(row, fullPath))
        };
        if (!string.IsNullOrWhiteSpace(record?.Xml))
        {
            groups.Add(BuildXmlGroup(record.Xml));
        }

        return new PropertyDetailReport(fileName, fullPath, groups);
    }

    private static List<PropertyDetailItem> BuildDiskGroup(
        FileRow row,
        string? fullPath)
    {
        var items = new List<PropertyDetailItem>
        {
            new("Thư mục (PATH_NAME)", row.Path ?? "—"),
            new("Tên tệp trên đĩa", BuildFileName(row.Name, row.Extension))
        };
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            items.Add(new PropertyDetailItem("Đường dẫn đầy đủ", "— (dòng dữ liệu thiếu PATH_NAME/SHORT_NAME)"));
            return items;
        }

        items.Add(new PropertyDetailItem("Đường dẫn đầy đủ", fullPath));
        var info = new FileInfo(fullPath);
        items.Add(new PropertyDetailItem("Còn tồn tại trên đĩa", info.Exists ? "Có" : "Không"));
        if (info.Exists)
        {
            items.Add(new PropertyDetailItem("Dung lượng trên đĩa", FileSizeFormatter.Format(info.Length)));
            items.Add(new PropertyDetailItem("Ngày tạo", FormatDate(info.CreationTime)));
            items.Add(new PropertyDetailItem("Sửa đổi lần cuối", FormatDate(info.LastWriteTime)));
        }

        return items;
    }

    private static PropertyDetailGroup BuildXmlGroup(string xml)
    {
        var preview = xml.Length > MaxXmlPreviewChars ? xml[..MaxXmlPreviewChars] + " ..." : xml;
        return new PropertyDetailGroup(
            "XML kèm theo (VAL_XML)",
            [new("Độ dài", $"{xml.Length:N0} ký tự"), new("Nội dung (đầu tệp)", preview)]);
    }

    private static string BuildFileName(string? shortName, string? extension) =>
        string.IsNullOrWhiteSpace(shortName)
            ? "—"
            : string.IsNullOrWhiteSpace(extension) ? shortName.Trim() : $"{shortName.Trim()}.{extension.Trim().TrimStart('.')}";

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static string FormatDate(DateTime? value) => value?.ToString(DateFormat) ?? "—";

    private static string FormatBool(bool? value) => value switch
    {
        true => "Có",
        false => "Không",
        _ => "—"
    };

    private static string FormatIdWithName(int? id, string? name) =>
        string.IsNullOrWhiteSpace(name) ? id?.ToString() ?? "—" : $"{id?.ToString() ?? "—"} - {name}";
}
