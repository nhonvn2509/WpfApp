using WpfApp.Models.Files;
using WpfApp.Models.Settings;

namespace WpfApp.Services.Files;

public sealed record SaveToOptions(
    bool MakeSessionFolder,
    bool SaveHeader,
    SaveToConflictMode ConflictMode,
    bool UseExtensionFilter,
    string ExtensionFilter,
    bool CloseAfterCopy,
    bool RenameByTime = false)
{
    public static SaveToOptions Default => new(true, true, SaveToConflictMode.Overwrite, false, "All Files", false);

    public static SaveToOptions FromPreferences(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var section = preferences.SaveTo ?? new SaveToSection();
        return new SaveToOptions(
            section.MakeSessionFolder,
            section.SaveHeader,
            section.AddCopyNumber ? SaveToConflictMode.AddCopyNumber : SaveToConflictMode.Overwrite,
            section.UseExtensionFilter,
            string.IsNullOrWhiteSpace(section.ExtensionFilter) ? "All Files" : section.ExtensionFilter.Trim(),
            section.CloseAfterCopy,
            section.RenameByTime);
    }

    public void ApplyToPreferences(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        preferences.SaveTo ??= new SaveToSection();
        preferences.SaveTo.MakeSessionFolder = MakeSessionFolder;
        preferences.SaveTo.SaveHeader = SaveHeader;
        preferences.SaveTo.AddCopyNumber = ConflictMode == SaveToConflictMode.AddCopyNumber;
        preferences.SaveTo.UseExtensionFilter = UseExtensionFilter;
        preferences.SaveTo.ExtensionFilter = ExtensionFilter;
        preferences.SaveTo.CloseAfterCopy = CloseAfterCopy;
        preferences.SaveTo.RenameByTime = RenameByTime;
    }
}

public sealed class SaveToPreferencesStore
{
    private readonly Services.Settings.IUserSettings _settings;

    public SaveToPreferencesStore(Services.Settings.IUserSettings settings)
    {
        _settings = settings;
    }

    public SaveToOptions Load() => SaveToOptions.FromPreferences(_settings.Current);

    public IReadOnlyList<SaveToFolderEntry> LoadFolders() =>
        _settings.Current.SaveTo?.Folders
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
            .Select(entry => new SaveToFolderEntry(entry.Path.Trim(), entry.IsActive))
            .ToList() ?? [];

    public void Save(SaveToOptions options, IEnumerable<SaveToFolderEntry> folders)
    {
        ArgumentNullException.ThrowIfNull(options);
        var preferences = _settings.Current.Clone();
        options.ApplyToPreferences(preferences);
        preferences.SaveTo ??= new SaveToSection();
        preferences.SaveTo.Folders = folders
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
            .Select(entry => new SaveToFolderSettings
            {
                Path = entry.Path.Trim(),
                IsActive = entry.IsActive
            })
            .ToList();
        _settings.Save(preferences);
    }
}
