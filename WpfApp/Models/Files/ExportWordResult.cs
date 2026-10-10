using WpfApp.Enums;
namespace WpfApp.Models.Files;

public sealed record ExportWordResult(string FilePath, ExportType Type, int RowCount);
