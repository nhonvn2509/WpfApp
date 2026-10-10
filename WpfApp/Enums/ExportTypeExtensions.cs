namespace WpfApp.Enums;

public static class ExportTypeExtensions
{
    public static string ToDisplayName(this ExportType type) => type switch
    {
        ExportType.ExportFollowSelected => "Xuất theo các dòng đã đánh dấu",
        ExportType.ExportFollowCurrentFilter => "Xuất theo điều kiện lọc hiện tại",
        _ => type.ToString()
    };
}
