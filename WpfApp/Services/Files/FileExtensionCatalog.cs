namespace WpfApp.Services.Files;

public static class FileExtensionCatalog
{
    public static bool ExtStoredWithLeadingDot { get; set; }

    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var value = path.Trim();

        var lastDot = value.LastIndexOf('.');
        return (lastDot >= 0 ? value[(lastDot + 1)..] : value).TrimStart('.').ToLowerInvariant();
    }

    public static string[] Normalize(IEnumerable<string>? extensions) =>
        extensions?
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Select(Normalize)
            .Where(extension => extension.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

    public static string[] NormalizeForSql(IEnumerable<string>? extensions) =>
        ExtStoredWithLeadingDot
            ? [.. Normalize(extensions).Select(extension => "." + extension)]
            : Normalize(extensions);
}