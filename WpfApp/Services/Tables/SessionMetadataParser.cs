using System.Globalization;
using System.Xml.Linq;
using WpfApp.Models;
using WpfApp.Models.Tables;

namespace WpfApp.Services.Tables;

internal static class SessionMetadataParser
{
    private const int MaxRowsPerSection = 1000;

    public const string PreferredSectionElement = "files";

    public static int SelectPreferredTabIndex(IReadOnlyList<SessionMetadataSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        for (var index = 0; index < sections.Count; index++)
        {
            if (string.Equals(
                    sections[index].ElementName,
                    PreferredSectionElement,
                    StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return 0;
    }

    private const string TextKey = "value";
    private static readonly HashSet<string> StructuredSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "sources",
        "contacts",
        "geolocation"
    };

    private static readonly HashSet<string> HiddenSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "session",
        "filters",
        "parser",
        "inp_hdr"
    };

    private static readonly HashSet<string> HiddenColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "path",
        "q"
    };

    private static readonly HashSet<string> HiddenFileColumns =
        new(HiddenColumns, StringComparer.OrdinalIgnoreCase) { "type" };

    private static readonly IReadOnlyDictionary<string, string> Titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["session"] = "Thông tin phiên",
        ["sources"] = "Nguồn phát",
        ["contacts"] = "Liên hệ",
        ["geolocation"] = "Vị trí",
        ["filters"] = "Bộ lọc",
        ["files"] = "Tệp",
        ["parser"] = "Bộ phân tích",
        ["inp_hdr"] = "Đầu vào"
    };

    private static readonly IReadOnlyDictionary<string, double> Widths = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
    {
        ["path"] = 190,
        ["name"] = 130,
        ["val"] = 110,
        ["profile"] = 110,
        ["dt_start"] = 135,
        ["dt_final"] = 135,
        ["dt"] = 135
    };

    public static IReadOnlyList<SessionMetadataSection> Parse(string? valXml)
    {
        if (string.IsNullOrWhiteSpace(valXml))
        {
            return [];
        }

        try
        {
            if (XDocument.Parse(valXml).Root is not { } root)
            {
                return [];
            }

            var sections = new List<SessionMetadataSection>();
            Collect(root, sections);
            Disambiguate(sections);
            return sections;
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static void Collect(XElement parent, List<SessionMetadataSection> sections)
    {
        foreach (var element in parent.Elements())
        {
            if (StructuredSections.Contains(element.Name.LocalName))
            {
                AddStructuredSection(sections, element);
                continue;
            }

            if (ReadRow(element) is not null)
            {
                Add(sections, element, [element]);
            }

            var children = element.Elements().ToList();
            if (children.Count == 0)
            {
                continue;
            }

            if (children.All(child => !child.HasElements))
            {
                Add(sections, element, children);
            }
            else
            {
                Collect(element, sections);
            }
        }
    }

    private static void AddStructuredSection(List<SessionMetadataSection> sections, XElement element)
    {
        var children = element.Elements().ToList();
        var rows = new List<Dictionary<string, string>>();

        if (children.Count == 0)
        {
            if (FlattenRow(element) is { Count: > 0 } row)
            {
                rows.Add(row);
            }
        }
        else if (children.All(child => !child.HasElements && !child.HasAttributes) &&
                 children.Select(child => child.Name.LocalName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == children.Count)
        {
            var row = FlattenRow(element);
            if (row.Count > 0)
            {
                rows.Add(row);
            }
        }
        else
        {
            rows.AddRange(children.Select(FlattenRow).Where(row => row.Count > 0));
        }

        AddRows(sections, element, rows);
    }

    private static Dictionary<string, string> FlattenRow(XElement element)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in element.DescendantsAndSelf())
        {
            foreach (var attribute in node.Attributes())
            {
                if (!attribute.IsNamespaceDeclaration)
                {
                    row[attribute.Name.LocalName] = attribute.Value;
                }
            }

            if (!node.HasElements && Normalize(node.Value) is { } value)
            {
                row[node.Name.LocalName] = value;
            }
        }

        return row;
    }

    private static void Add(List<SessionMetadataSection> sections, XElement element, List<XElement> rowElements)
    {
        var rows = new List<Dictionary<string, string>>(rowElements.Count);
        foreach (var rowElement in rowElements)
        {
            if (ReadRow(rowElement) is { } row)
            {
                rows.Add(row);
            }
        }

        AddRows(sections, element, rows);
    }

    private static void AddRows(
        List<SessionMetadataSection> sections,
        XElement element,
        List<Dictionary<string, string>> rows)
    {
        if (HiddenSections.Contains(element.Name.LocalName))
        {
            return;
        }

        if (rows.Count == 0)
        {
            return;
        }

        var columns = BuildColumns(rows, IsFileElement(element) ? HiddenFileColumns : HiddenColumns);
        if (columns.Count == 0)
        {
            return;
        }

        if (rows.Count > MaxRowsPerSection)
        {
            rows.RemoveRange(MaxRowsPerSection, rows.Count - MaxRowsPerSection);
        }

        sections.Add(new SessionMetadataSection(
            BuildTitle(element),
            element.Name.LocalName,
            columns,
            rows,
            BuildFileRows(element, rows)));
    }

    public static IReadOnlyList<PropertyDetailItem> ParseDossier(string? valXml)
    {
        if (string.IsNullOrWhiteSpace(valXml))
        {
            return [];
        }

        try
        {
            var session = XDocument.Parse(valXml)
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName.Equals("session", StringComparison.OrdinalIgnoreCase));
            if (session is null || ReadRow(session) is not { } row)
            {
                return [];
            }

            return row
                .Where(pair => !HiddenColumns.Contains(pair.Key))
                .Select(pair => new PropertyDetailItem(FormatLabel(pair.Key), Normalize(pair.Value) ?? string.Empty))
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static string FormatLabel(string key) => key.Replace('_', ' ').ToUpperInvariant();

    private static Dictionary<string, string>? ReadRow(XElement element)
    {
        Dictionary<string, string>? row = null;
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                continue;
            }

            row ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            row[attribute.Name.LocalName] = attribute.Value;
        }

        if (row is not null)
        {
            return row;
        }

        var text = Normalize(element.Value);
        return text is null
            ? null
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [element.HasElements ? TextKey : element.Name.LocalName] = text
            };
    }

    private static List<SessionMetadataColumn> BuildColumns(
        List<Dictionary<string, string>> rows,
        IReadOnlySet<string> hiddenColumns)
    {
        var keys = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            foreach (var key in row.Keys)
            {
                if (hiddenColumns.Contains(key) || !seen.Add(key))
                {
                    continue;
                }

                keys.Add(key);
            }
        }

        return keys
            .Select(key => new SessionMetadataColumn(
                key,
                key.Replace('_', ' ').ToUpperInvariant(),
                Widths.TryGetValue(key, out var width) ? width : 80))
            .ToList();
    }

    private static IReadOnlyList<FileRow>? BuildFileRows(XElement element, List<Dictionary<string, string>> rows)
    {
        if (!IsFileElement(element))
        {
            return null;
        }

        return rows
            .Select(row => new FileRow
            {
                Name = Value(row, "name", "filename", "file_name"),
                Extension = Value(row, "ext", "extension"),
                Length = long.TryParse(
                    Value(row, "size", "length", "bytes"),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var length)
                    ? length
                    : null,
                Quality = int.TryParse(
                    Value(row, "q", "quality"),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var quality)
                    ? quality
                    : null,
                Path = Value(row, "path", "fullpath", "full_path")
            })
            .ToList();
    }

    private static bool IsFileElement(XElement element) =>
        element.Name.LocalName.Equals("files", StringComparison.OrdinalIgnoreCase)
        || element.Name.LocalName.Equals("file", StringComparison.OrdinalIgnoreCase);

    private static void Disambiguate(List<SessionMetadataSection> sections)
    {
        var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = sections.Count - 1; index >= 0; index--)
        {
            var section = sections[index];
            if (titles.Add(section.Title))
            {
                continue;
            }

            sections[index] = new SessionMetadataSection(
                $"{section.Title} (thuộc tính)",
                section.ElementName,
                section.Columns,
                section.Rows,
                section.FileRows);
        }
    }

    private static string BuildTitle(XElement element)
    {
        var name = element.Name.LocalName;
        return Titles.TryGetValue(name, out var title)
            ? $"{title} ({name})"
            : name.ToUpperInvariant();
    }

    private static string? Value(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
