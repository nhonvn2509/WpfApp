using WpfApp.Models.Settings;

namespace WpfApp.Services.Settings;

public interface IUserSettings
{
    string FilePath { get; }

    UserPreferences Current { get; }

    string? GetString(params string[] path);

    IReadOnlyDictionary<string, string>? GetSection(params string[] path);

    void Save(UserPreferences preferences);

    void Reset();
}
