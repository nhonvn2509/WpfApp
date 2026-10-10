using MahApps.Metro.IconPacks;

namespace WpfApp.Presentation;

public static class IconCatalog
{
    public const PackIconLucideKind Session = PackIconLucideKind.RadioTower;

    public const PackIconLucideKind Chain = PackIconLucideKind.Link2;

    public const PackIconLucideKind Fallback = PackIconLucideKind.File;

    public const PackIconLucideKind Folder = PackIconLucideKind.Folder;

    public const string FileBrushKey = "PrimaryBrush";

    public const string SessionBrushKey = "InfoBrush";

    public const string ChainBrushKey = "PrimaryBrush";

    public const string DataTypeBrushKey = "PrimaryBrush";

    private static readonly Dictionary<string, PackIconLucideKind> ExtensionIcons =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".wav"] = PackIconLucideKind.FileHeadphone,
            [".amr"] = PackIconLucideKind.FileHeadphone,
            [".pcm"] = PackIconLucideKind.FileHeadphone,
            [".opus"] = PackIconLucideKind.FileHeadphone,

            [".zip"] = PackIconLucideKind.FileArchive,
            [".rar"] = PackIconLucideKind.FileArchive,
            [".7z"] = PackIconLucideKind.FileArchive,
            [".tar"] = PackIconLucideKind.FileArchive,
            [".gz"] = PackIconLucideKind.FileArchive,
            [".bz2"] = PackIconLucideKind.FileArchive,
            [".xz"] = PackIconLucideKind.FileArchive,
            [".iso"] = PackIconLucideKind.FileArchive,

            [".jpg"] = PackIconLucideKind.FileImage,
            [".jpeg"] = PackIconLucideKind.FileImage,
            [".png"] = PackIconLucideKind.FileImage,
            [".gif"] = PackIconLucideKind.FileImage,
            [".bmp"] = PackIconLucideKind.FileImage,
            [".tif"] = PackIconLucideKind.FileImage,
            [".tiff"] = PackIconLucideKind.FileImage,
            [".webp"] = PackIconLucideKind.FileImage,
            [".svg"] = PackIconLucideKind.FileImage,
            [".heic"] = PackIconLucideKind.FileImage,
            [".ico"] = PackIconLucideKind.FileImage,

            [".mp4"] = PackIconLucideKind.FileVideoCamera,
            [".avi"] = PackIconLucideKind.FileVideoCamera,
            [".mkv"] = PackIconLucideKind.FileVideoCamera,
            [".mov"] = PackIconLucideKind.FileVideoCamera,
            [".flv"] = PackIconLucideKind.FileVideoCamera,
            [".wmv"] = PackIconLucideKind.FileVideoCamera,
            [".mpg"] = PackIconLucideKind.FileVideoCamera,
            [".mpeg"] = PackIconLucideKind.FileVideoCamera,
            [".3gp"] = PackIconLucideKind.FileVideoCamera,

            [".pdf"] = PackIconLucideKind.ScrollText,
            [".doc"] = PackIconLucideKind.FileText,
            [".docx"] = PackIconLucideKind.FileText,
            [".odt"] = PackIconLucideKind.FileText,
            [".rtf"] = PackIconLucideKind.FileText,
            [".txt"] = PackIconLucideKind.FileText,
            [".log"] = PackIconLucideKind.FileText,
            [".md"] = PackIconLucideKind.FileText,
            [".xls"] = PackIconLucideKind.FileSpreadsheet,
            [".xlsx"] = PackIconLucideKind.FileSpreadsheet,
            [".xlsm"] = PackIconLucideKind.FileSpreadsheet,
            [".ods"] = PackIconLucideKind.FileSpreadsheet,
            [".csv"] = PackIconLucideKind.FileSpreadsheet,
            [".ppt"] = PackIconLucideKind.FileChartColumn,
            [".pptx"] = PackIconLucideKind.FileChartColumn,
            [".odp"] = PackIconLucideKind.FileChartColumn,

            [".dta"] = PackIconLucideKind.Table,
            [".sav"] = PackIconLucideKind.Table,
            [".parquet"] = PackIconLucideKind.Table,
            [".dbf"] = PackIconLucideKind.Table,
            [".gpsidt"] = PackIconLucideKind.MapPin,
            [".gpx"] = PackIconLucideKind.MapPin,
            [".kml"] = PackIconLucideKind.MapPin,
            [".kmz"] = PackIconLucideKind.MapPin,
            [".nmea"] = PackIconLucideKind.MapPin,
            [".shp"] = PackIconLucideKind.Map,
            [".sqlite"] = PackIconLucideKind.Database,
            [".sqlite3"] = PackIconLucideKind.Database,
            [".db"] = PackIconLucideKind.Database,
            [".mdb"] = PackIconLucideKind.Database,
            [".pcap"] = PackIconLucideKind.Network,
            [".pcapng"] = PackIconLucideKind.Network,
            [".bag"] = PackIconLucideKind.Network,
            [".rosbag"] = PackIconLucideKind.Network,
            [".ttf"] = PackIconLucideKind.Type,
            [".otf"] = PackIconLucideKind.Type,
            [".woff"] = PackIconLucideKind.Type,
            [".woff2"] = PackIconLucideKind.Type,
            [".dwg"] = PackIconLucideKind.PenTool,
            [".dxf"] = PackIconLucideKind.PenTool,
            [".stl"] = PackIconLucideKind.Box,
            [".3ds"] = PackIconLucideKind.Box,
            [".obj"] = PackIconLucideKind.Box,
            [".flt"] = PackIconLucideKind.MapPinned,

            [".json"] = PackIconLucideKind.FileCode,
            [".xml"] = PackIconLucideKind.FileCode,
            [".yaml"] = PackIconLucideKind.FileCode,
            [".yml"] = PackIconLucideKind.FileCode,
            [".ini"] = PackIconLucideKind.FileCode,
            [".html"] = PackIconLucideKind.FileCode,
            [".htm"] = PackIconLucideKind.FileCode,
            [".css"] = PackIconLucideKind.FileCode,
            [".js"] = PackIconLucideKind.FileCode,
            [".ts"] = PackIconLucideKind.FileCode,
            [".cs"] = PackIconLucideKind.FileCode,
            [".py"] = PackIconLucideKind.FileCode,
            [".sql"] = PackIconLucideKind.FileCode,

            [".dat"] = PackIconLucideKind.FileDigit,
            [".bin"] = PackIconLucideKind.FileDigit,
            [".hex"] = PackIconLucideKind.FileDigit,
            [".raw"] = PackIconLucideKind.FileDigit
        };

    private static readonly Dictionary<string, PackIconLucideKind> FileTypeIcons =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["AUDIO"] = PackIconLucideKind.FileMusic,
            ["VIDEO"] = PackIconLucideKind.FileVideoCamera,
            ["IMAGE"] = PackIconLucideKind.FileImage,
            ["ARCHIVE"] = PackIconLucideKind.FileArchive,
            ["TEXT"] = PackIconLucideKind.FileText,
            ["DOCUMENT"] = PackIconLucideKind.FileText,
            ["DATA"] = PackIconLucideKind.FileCode,
            ["OTHER"] = Fallback
        };

    private static readonly Dictionary<string, PackIconLucideKind> DataTypeIcons =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["VOICE"] = PackIconLucideKind.Mic,
            ["DATA"] = PackIconLucideKind.Database,
            ["FAX"] = PackIconLucideKind.Printer,
            ["SMS"] = PackIconLucideKind.MessageSquare,
            ["GPS"] = PackIconLucideKind.MapPin,
            ["MAIL"] = PackIconLucideKind.Mail
        };

    private static readonly Dictionary<string, PackIconLucideKind> StatusIcons =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["NEW"] = PackIconLucideKind.Sparkles,
            ["PARSED"] = PackIconLucideKind.CircleCheck,
            ["PROCESSED"] = PackIconLucideKind.Wand,
            ["READY"] = PackIconLucideKind.Zap,
            ["READ"] = PackIconLucideKind.Eye,
            ["DELETE"] = PackIconLucideKind.Trash2,
            ["ERROR"] = PackIconLucideKind.TriangleAlert,
            ["IGNORE"] = PackIconLucideKind.CircleSlash,
            ["CAPTURED"] = PackIconLucideKind.Radio
        };

    private static readonly Dictionary<string, string> StatusBrushKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["NEW"] = "PrimaryBrush",
            ["PARSED"] = "SuccessBrush",
            ["PROCESSED"] = "DarkPrimaryBrush",
            ["READY"] = "WarningBrush",
            ["READ"] = "ThirdlyTextBrush",
            ["DELETE"] = "DangerBrush",
            ["ERROR"] = "DangerBrush",
            ["IGNORE"] = "ThirdlyTextBrush",
            ["CAPTURED"] = "ExploitBrush"
        };

    public static PackIconLucideKind File(string? extension, string? typeName) =>
        Resolve(ExtensionIcons, NormalizeExtension(extension))
        ?? Resolve(FileTypeIcons, Trim(typeName))
        ?? Fallback;

    public static PackIconLucideKind DataType(string? typeName) =>
        Resolve(DataTypeIcons, Trim(typeName)) ?? PackIconLucideKind.Tag;

    public static PackIconLucideKind Status(string? statusName) =>
        Resolve(StatusIcons, Trim(statusName)) ?? PackIconLucideKind.Circle;

    public static string StatusBrushKey(string? statusName) =>
        StatusBrushKeys.TryGetValue(Trim(statusName) ?? string.Empty, out var key)
            ? key
            : "ThirdlyTextBrush";

    private static PackIconLucideKind? Resolve(
        Dictionary<string, PackIconLucideKind> icons,
        string? key) =>
        key is not null && icons.TryGetValue(key, out var kind) ? kind : null;

    private static string? NormalizeExtension(string? extension)
    {
        var value = Trim(extension);
        if (value is null)
        {
            return null;
        }

        var lastDot = value.LastIndexOf('.');
        return lastDot >= 0 ? value[lastDot..].ToLowerInvariant() : "." + value.ToLowerInvariant();
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
