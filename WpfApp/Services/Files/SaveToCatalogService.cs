using WpfApp.Models.Files;
using WpfApp.Services.Tables;

namespace WpfApp.Services.Files;

public sealed record SaveToExtensionOption(string Name, IReadOnlyList<string> Extensions);

public sealed class SaveToCatalogService
{
    public IReadOnlyList<SaveToExtensionOption> BuildOptions() =>
        Tables.FileExtensionCatalog.All
            .Select(group => new SaveToExtensionOption(
                group.Name,
                group.Extensions.Select(Normalize).Where(static value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()))
            .ToList();

    public IReadOnlyList<string> ResolveFilter(SaveToOptions options, IReadOnlyList<SaveToExtensionOption> catalog)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(catalog);
        if (!options.UseExtensionFilter)
        {
            return ["*.*"];
        }

        var wanted = (options.ExtensionFilter ?? string.Empty).Trim();
        if (wanted.Length == 0 || wanted.Equals("All Files", StringComparison.OrdinalIgnoreCase))
        {
            return ["*.*"];
        }

        var group = catalog.FirstOrDefault(entry => entry.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase));
        if (group is not null && group.Extensions.Count > 0)
        {
            return group.Extensions.Select(static extension => "*." + extension).ToArray();
        }

        var direct = wanted
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static extension => extension.StartsWith("*.") ? extension : "*." + extension.TrimStart('.'))
            .ToArray();
        return direct.Length == 0 ? ["*.*"] : direct;
    }

    public static bool MatchesFilter(string? extension, IReadOnlyList<string> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.Count == 0 || filter.Any(static pattern => pattern is "*.*" or "*"))
        {
            return true;
        }

        var normalized = Normalize(extension);
        return filter.Any(pattern =>
        {
            var cleaned = pattern.Trim().TrimStart('*').TrimStart('.').ToLowerInvariant();
            return cleaned.Length > 0 && cleaned == normalized;
        });
    }

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
}
