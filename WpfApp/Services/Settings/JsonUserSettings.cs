using System.IO;
using System.Text.Json;
using WpfApp.Models.Settings;
using WpfApp.Services.Tables;

namespace WpfApp.Services.Settings;

public sealed class JsonUserSettings : IUserSettings
{
    private const string ApplicationFolderName = "DeltaBat";
    private const string FileName = "settings.json";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly object _gate = new();
    private UserPreferences? _current;

    public JsonUserSettings(string? filePath = null)
    {
        FilePath = filePath is null
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ApplicationFolderName,
                FileName)
            : Path.GetFullPath(filePath);
    }

    public string FilePath { get; }

    public UserPreferences Current
    {
        get
        {
            lock (_gate)
            {
                return _current ??= Load();
            }
        }
    }

    public string? GetString(params string[] path)
    {
        var preferences = Current;
        return path switch
        {
            ["ConnectionStrings", "DeltaBat"] => Clean(preferences.ConnectionStrings.DeltaBat),
            ["Files", "RootPath"] => Clean(preferences.Files.RootPath),
            _ => null
        };
    }

    // settings.json của người dùng chỉ có ConnectionStrings/Files/Tables — không có mục object theo khoá.
    public IReadOnlyDictionary<string, string>? GetSection(params string[] path) => null;

    public int PageSize => Current.Tables.PageSize;

    public void Save(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var normalized = preferences.Clone();
        normalized.Tables.PageSize = TablePage.ClampPageSize(normalized.Tables.PageSize);
        normalized.ConnectionStrings.DeltaBat = Clean(normalized.ConnectionStrings.DeltaBat);
        normalized.Files.RootPath = Clean(normalized.Files.RootPath);

        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(normalized, WriteOptions);
        var temporaryPath = FilePath + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, FilePath, overwrite: true);

        lock (_gate)
        {
            _current = normalized;
        }
    }

    public void Reset()
    {
        try
        {
            File.Delete(FilePath);
        }
        catch (IOException)
        {
        }

        lock (_gate)
        {
            _current = null;
        }
    }

    private UserPreferences Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var preferences =
                    JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(FilePath), ReadOptions);
                if (preferences is not null)
                {
                    WithSections(preferences);
                    preferences.Tables.PageSize =
                        TablePage.ClampPageSize(preferences.Tables.PageSize);
                    return preferences;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
        }

        return new UserPreferences();
    }

    private static void WithSections(UserPreferences preferences)
    {
        preferences.ConnectionStrings ??= new ConnectionStringsSection();
        preferences.Files ??= new FilesSection();
        preferences.Tables ??= new TablesSection();
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
