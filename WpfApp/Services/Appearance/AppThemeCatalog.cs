using WpfApp.Models.Appearance;
using WpfApp.Models.Settings;

namespace WpfApp.Services.Appearance;

public static class AppThemeCatalog
{
    private static readonly IReadOnlyList<AppTheme> All =
    [
        new(
            "Midnight",
            "Midnight",
            "Theme tối · xanh than đậm, dịu mắt khi làm việc lâu",
            "SkinDark",
            "ThemeMidnight.xaml",
            "#0B0E13",
            "#4C9AFF"),
        new(
            "Ocean",
            "Ocean",
            "Theme sáng · xanh biển nhạt, dịu mắt và thoáng",
            "SkinDefault",
            "ThemeOcean.xaml",
            "#F3F8FA",
            "#087E9B"),
        new(
            "Violet",
            "Violet",
            "Theme trung tính · tím xám cân bằng giữa sáng và tối",
            "SkinViolet",
            "ThemeViolet.xaml",
            "#4A4653",
            "#B69AFF"),
        new(
            "Daylight",
            "Daylight",
            "Theme sáng · nền sáng rõ, xanh lam cổ điển",
            "SkinDefault",
            "ThemeDaylight.xaml",
            "#F5F7FA",
            "#1F6FEB"),
        new(
            "Carbon",
            "Carbon",
            "Theme tối · than chì trung tính, phong cách VS Code",
            "SkinDark",
            "ThemeCarbon.xaml",
            "#1F1F1F",
            "#3794FF")
    ];

    public static IReadOnlyList<AppTheme> Themes => All;

    public static AppTheme Default { get; } = All[0];

    public static AppTheme? Find(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : All.FirstOrDefault(theme => string.Equals(theme.Key, key, StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string? key) => Find(key)?.Key ?? Default.Key;

    public static string NormalizeFromJson(string? key) =>
        string.IsNullOrWhiteSpace(key) ? AppearanceSection.DefaultTheme : Normalize(key);
}