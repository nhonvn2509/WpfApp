using MahApps.Metro.IconPacks;

namespace WpfApp.ViewModel.Settings;

public enum SettingsSection
{
    Connection,
    Files,
    Tables,
    Appearance,
    About
}

public sealed record SettingsNavItem(
    SettingsSection Section,
    string Title,
    string Description,
    PackIconLucideKind Icon);
