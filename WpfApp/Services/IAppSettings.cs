namespace WpfApp.Services;

public interface IAppSettings
{
    string SettingsFileName { get; }
    string ConnectionString { get; }

    string ConnectionStringSource { get; }

    string? GetString(params string[] path);

    /// <summary>Đọc một mục object { "khoá": "giá trị" } ở đường dẫn path; null nếu không có.</summary>
    IReadOnlyDictionary<string, string>? GetSection(params string[] path);
}
