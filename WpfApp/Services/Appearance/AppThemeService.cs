using System.Windows;
using WpfApp.Models.Appearance;
using WpfApp.Models.Settings;
using WpfApp.Services.Settings;

namespace WpfApp.Services.Appearance;

public sealed class AppThemeService : IAppThemeService
{
    private const string HandyControlSkinMarker = "Themes/Skin";

    private const string HandyControlSkinPrefix = "pack://application:,,,/HandyControl;component/Themes/";

    private const string AppPaletteMarker = "Views/Themes/Theme";

    private const string AppPalettePrefix = "pack://application:,,,/WpfApp;component/Views/Themes/";

    private readonly IUserSettings _userSettings;
    private AppTheme _current;

    public AppThemeService(IUserSettings userSettings)
    {
        ArgumentNullException.ThrowIfNull(userSettings);
        _userSettings = userSettings;
        _current = AppThemeCatalog.Default;
    }

    public IReadOnlyList<AppTheme> Themes => AppThemeCatalog.Themes;

    public string CurrentKey => _current.Key;

    public AppTheme Current => _current;

    public void Apply(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var theme = AppThemeCatalog.Find(key) ?? AppThemeCatalog.Default;

        if (Application.Current?.Resources is { } resources)
        {
            Swap(
                resources.MergedDictionaries,
                HandyControlSkinMarker,
                HandyControlSkinPrefix + theme.HandyControlSkin + ".xaml");
            Swap(
                resources.MergedDictionaries,
                AppPaletteMarker,
                AppPalettePrefix + theme.FileName);
        }

        _current = theme;
        ThemeProvider.Current = theme;
    }

    public void Save(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var preferences = _userSettings.Current.Clone();
        preferences.Appearance.Theme = AppThemeCatalog.Normalize(key);
        _userSettings.Save(preferences);
    }

    public void Load() => Apply(_userSettings.Current.Appearance.Theme ?? AppearanceSection.DefaultTheme);

    private static void Swap(object merged, string marker, string packUri)
    {
        if (merged is not System.Collections.IList list)
        {
            return;
        }

        for (var index = 0; index < list.Count; index++)
        {
            if (list[index] is not ResourceDictionary dictionary)
            {
                continue;
            }

            var source = dictionary.Source?.OriginalString ?? string.Empty;
            if (!source.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            list[index] = new ResourceDictionary { Source = new Uri(packUri, UriKind.Absolute) };
            return;
        }
    }
}