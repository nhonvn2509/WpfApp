using WpfApp.Services.Settings;

namespace WpfApp.Services.Settings;

public sealed class UserFirstAppSettings : IAppSettings
{
    private const string ConnectionStringsSection = "ConnectionStrings";
    private const string ConnectionStringName = "DeltaBat";

    private readonly IAppSettings _deployment;
    private readonly IUserSettings _user;

    public UserFirstAppSettings(IUserSettings userSettings, JsonAppSettings deploymentSettings)
    {
        ArgumentNullException.ThrowIfNull(userSettings);
        ArgumentNullException.ThrowIfNull(deploymentSettings);
        _user = userSettings;
        _deployment = deploymentSettings;
    }

    public string SettingsFileName => _deployment.SettingsFileName;

    public string ConnectionString =>
        _user.GetString(ConnectionStringsSection, ConnectionStringName) ?? _deployment.ConnectionString;

    public string ConnectionStringSource =>
        _user.GetString(ConnectionStringsSection, ConnectionStringName) is not null
            ? "Cấu hình người dùng (Cài đặt)"
            : _deployment.ConnectionStringSource;

    public string? GetString(params string[] path) =>
        _user.GetString(path) ?? _deployment.GetString(path);

    public IReadOnlyDictionary<string, string>? GetSection(params string[] path) =>
        _user.GetSection(path) ?? _deployment.GetSection(path);
}
