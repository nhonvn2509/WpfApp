namespace WpfApp.Models.Appearance;

public sealed record AppTheme(
    string Key,
    string DisplayName,
    string Description,
    string HandyControlSkin,
    string FileName,
    string RegionColor,
    string AccentColor)
{
    public bool IsDark => !HandyControlSkin.EndsWith("Default", StringComparison.Ordinal);
}