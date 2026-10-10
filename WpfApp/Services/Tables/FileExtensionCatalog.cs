namespace WpfApp.Services.Tables;

public sealed record FileExtensionGroup(string Name, IReadOnlyList<string> Extensions);

public static class FileExtensionCatalog
{
    private static readonly FileExtensionGroup[] Groups =
    [
        new("Nguồn dữ liệu", Split("dta,dat,bin,raw,hdr,header")),
        new("Tệp nén", Split("arj,rar,zip,7z,iso")),
        new("Âm thanh", Split("wav,mp3,voc")),
        new("Cơ sở dữ liệu", Split("mdf,ndf,sdf,mdb,sql")),
        new("Hình ảnh", Split("bmp,gif,jpg,jpeg,ico,png,tif,tiff")),
        new("Ms Word Office", Split("doc,docx,docm,ppt,pptx,rtf,xls,xlsx,xlsm,xlt")),
        new("Video", Split("3gp,avi,avchd,f4v,flv,m2ts,m4p,mp4,mkv,mov,mpg,mpeg,mpe,mpv,mts,swf,ts,wmv")),
        new("Chương trình", Split("exe,dll,obj,cpp,cs,js,apk")),
        new("Trang web", Split("html,htm,css,json")),
    ];

    public static IReadOnlyList<FileExtensionGroup> All => Groups;

    public static IReadOnlyList<string> AllExtensions { get; } =
        Groups
            .SelectMany(group => group.Extensions)
            .Select(extension => extension.TrimStart('.').ToLowerInvariant())
            .Where(extension => extension.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static string[] Normalize(IEnumerable<string> extensions) =>
        Services.Files.FileExtensionCatalog.NormalizeForSql(extensions);

    public static string[] NormalizeWithoutDot(IEnumerable<string> extensions) =>
        Services.Files.FileExtensionCatalog.Normalize(extensions);

    private static string[] Split(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
