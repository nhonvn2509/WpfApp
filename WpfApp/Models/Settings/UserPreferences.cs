using System.Text.Json.Serialization;

namespace WpfApp.Models.Settings;

public sealed class UserPreferences
{
    public const int DefaultPageSize = 100;

    [JsonPropertyName("ConnectionStrings")]
    public ConnectionStringsSection ConnectionStrings { get; set; } = new();

    [JsonPropertyName("Files")]
    public FilesSection Files { get; set; } = new();

    [JsonPropertyName("Tables")]
    public TablesSection Tables { get; set; } = new();

    [JsonPropertyName("Appearance")]
    public AppearanceSection Appearance { get; set; } = new();

    [JsonPropertyName("SaveTo")]
    public SaveToSection SaveTo { get; set; } = new();

    public UserPreferences Clone() => new()
    {
        ConnectionStrings = ConnectionStrings?.Clone() ?? new ConnectionStringsSection(),
        Files = Files?.Clone() ?? new FilesSection(),
        Tables = Tables?.Clone() ?? new TablesSection(),
        Appearance = Appearance?.Clone() ?? new AppearanceSection(),
        SaveTo = SaveTo?.Clone() ?? new SaveToSection()
    };
}

public sealed class ConnectionStringsSection
{
    [JsonPropertyName("DeltaBat")]
    public string? DeltaBat { get; set; }

    public ConnectionStringsSection Clone() => new() { DeltaBat = DeltaBat };
}

public sealed class FilesSection
{
    [JsonPropertyName("RootPath")]
    public string? RootPath { get; set; }

    public FilesSection Clone() => new() { RootPath = RootPath };
}

public sealed class TablesSection
{
    [JsonPropertyName("PageSize")]
    public int PageSize { get; set; } = UserPreferences.DefaultPageSize;

    public TablesSection Clone() => new() { PageSize = PageSize };
}

public sealed class AppearanceSection
{
    public const string DefaultTheme = "Midnight";

    [JsonPropertyName("Theme")]
    public string? Theme { get; set; }

    public AppearanceSection Clone() => new() { Theme = Theme };
}

public sealed class SaveToSection
{
    [JsonPropertyName("MakeSessionFolder")]
    public bool MakeSessionFolder { get; set; } = true;

    [JsonPropertyName("SaveHeader")]
    public bool SaveHeader { get; set; } = true;

    [JsonPropertyName("AddCopyNumber")]
    public bool AddCopyNumber { get; set; }

    [JsonPropertyName("UseExtensionFilter")]
    public bool UseExtensionFilter { get; set; }

    [JsonPropertyName("ExtensionFilter")]
    public string ExtensionFilter { get; set; } = "All Files";

    [JsonPropertyName("CloseAfterCopy")]
    public bool CloseAfterCopy { get; set; }

    [JsonPropertyName("RenameByTime")]
    public bool RenameByTime { get; set; }

    [JsonPropertyName("Folders")]
    public List<SaveToFolderSettings> Folders { get; set; } = [];

    [JsonPropertyName("LastPath")]
    public string? LastPath { get; set; }

    public SaveToSection Clone() => new()
    {
        MakeSessionFolder = MakeSessionFolder,
        SaveHeader = SaveHeader,
        AddCopyNumber = AddCopyNumber,
        UseExtensionFilter = UseExtensionFilter,
        ExtensionFilter = ExtensionFilter,
        CloseAfterCopy = CloseAfterCopy,
        RenameByTime = RenameByTime,
        LastPath = LastPath,
        Folders = Folders.Select(entry => entry.Clone()).ToList()
    };
}

public sealed class SaveToFolderSettings
{
    [JsonPropertyName("Path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("IsActive")]
    public bool IsActive { get; set; }

    public SaveToFolderSettings Clone() => new() { Path = Path, IsActive = IsActive };
}

