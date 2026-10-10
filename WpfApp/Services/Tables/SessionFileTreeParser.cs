using System.Globalization;
using System.Xml.Linq;
using WpfApp.Models.Tables;

namespace WpfApp.Services.Tables;

internal static class SessionFileTreeParser
{
    public static IReadOnlyList<SessionFileTreeNode> Parse(string? valXml)
    {
        if (string.IsNullOrWhiteSpace(valXml))
        {
            return [];
        }

        try
        {
            var document = XDocument.Parse(valXml);
            var fileElements = document.Root?
                .DescendantsAndSelf()

                .Where(element => !element.HasElements && IsFileElement(element))
                .ToList() ?? [];

            if (fileElements.Count == 0)
            {
                return [];
            }

            return fileElements
                .Select(element => SessionFileTreeNode.CreateFile(
                    FirstNonEmpty(Attribute(element, "name"), Attribute(element, "filename"), Attribute(element, "file_name"), Normalize(element.Value)) ?? "(không tên)",
                    FirstNonEmpty(Attribute(element, "ext"), Attribute(element, "extension")),
                    ParseLength(FirstNonEmpty(Attribute(element, "size"), Attribute(element, "length"), Attribute(element, "bytes"))),
                    FirstNonEmpty(Attribute(element, "type"), Attribute(element, "kind")),
                    FirstNonEmpty(Attribute(element, "path"), Attribute(element, "fullpath"), Attribute(element, "full_path"))))
                .Where(node => !string.IsNullOrWhiteSpace(node.Name))
                .ToArray();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static bool IsFileElement(XElement element)
    {
        var localName = element.Name.LocalName;
        return localName.Equals("file", StringComparison.OrdinalIgnoreCase)
            || localName.Equals("files", StringComparison.OrdinalIgnoreCase)
            || localName.Equals("item", StringComparison.OrdinalIgnoreCase)
            || localName.Equals("record", StringComparison.OrdinalIgnoreCase)
            || localName.Equals("entry", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool NameIs(XElement element, string name) =>
        string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase);

    private static string? Attribute(XElement element, string name) =>
        element.Attributes()
            .FirstOrDefault(attribute => string.Equals(
                attribute.Name.LocalName,
                name,
                StringComparison.OrdinalIgnoreCase))
            ?.Value;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static long? ParseLength(string? value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length)
            ? length
            : null;
}
