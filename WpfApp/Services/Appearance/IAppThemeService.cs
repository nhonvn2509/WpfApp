using WpfApp.Models.Appearance;

namespace WpfApp.Services.Appearance;

public interface IAppThemeService
{
    IReadOnlyList<AppTheme> Themes { get; }

    string CurrentKey { get; }

    AppTheme Current { get; }

    void Apply(string key);

    void Save(string key);

    void Load();
}

public static class ThemeProvider
{
    public static AppTheme? Current { get; internal set; }
}