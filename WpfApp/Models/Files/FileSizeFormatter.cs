using System.Globalization;
namespace WpfApp.Models.Files;

public static class FileSizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];
    public static string Format(long? bytes)
    {
        if (bytes is not { } value || value < 0)
        {
            return "—";
        }
        double size = value;
        var unit = 0;
        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        var text = unit == 0
            ? size.ToString("N0", CultureInfo.CurrentCulture)
            : size.ToString("N2", CultureInfo.CurrentCulture);
        return $"{text} {Units[unit]}";
    }
}
