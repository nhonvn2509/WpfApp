using WpfApp.Enums;
using WpfApp.Models.Tables;
using WpfApp.Services.Tables;
namespace WpfApp.Models.Files;

public sealed record ExportWordRequest(
    ExportType Type,
    FilesQuery Query,
    IReadOnlyList<FileRow> SelectedRows)
{
    public static ExportWordRequest FollowSelected(IReadOnlyList<FileRow> selectedRows, FilesQuery query) =>
        new(ExportType.ExportFollowSelected, query, selectedRows);
    public static ExportWordRequest FollowCurrentFilter(FilesQuery query) =>
        new(ExportType.ExportFollowCurrentFilter, query, []);
}
