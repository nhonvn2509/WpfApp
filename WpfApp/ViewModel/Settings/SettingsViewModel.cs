using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.IconPacks;
using WpfApp.Models.Appearance;
using WpfApp.Models.Settings;
using WpfApp.Services;
using WpfApp.Services.Appearance;
using WpfApp.Services.Data;
using WpfApp.Services.Settings;
using WpfApp.Services.Tables;

namespace WpfApp.ViewModel.Settings;

public sealed partial class SettingsViewModel : ObservableObject
{
    private const string ConnectionSectionName = "ConnectionStrings";
    private const string ConnectionName = "DeltaBat";

    private readonly IAppSettings _appSettings;
    private readonly IUserSettings _userSettings;
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IAppThemeService _themeService;
    private readonly IDialogService _dialog;

    private UserPreferences _saved;

    private bool _closeConfirmed;

    private bool _applyingDatabaseTarget;

    public SettingsViewModel(
        IAppSettings appSettings,
        IUserSettings userSettings,
        ISqlConnectionFactory connectionFactory,
        IAppThemeService themeService,
        IDialogService dialog)
    {
        ArgumentNullException.ThrowIfNull(appSettings);
        ArgumentNullException.ThrowIfNull(userSettings);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(dialog);
        _appSettings = appSettings;
        _userSettings = userSettings;
        _connectionFactory = connectionFactory;
        _themeService = themeService;
        _dialog = dialog;

        _saved = _userSettings.Current.Clone();

        // Dropdown CSDL lấy từ "ValueConnectionStrings" của appsettings.json;
        // thiếu mục này thì rơi về danh sách dựng sẵn KnownDatabases.
        var valueConnections = _appSettings.GetSection("ValueConnectionStrings");
        DatabaseTargets = valueConnections is { Count: > 0 }
            ? valueConnections
                  .Select(pair => new DatabaseTarget(pair.Key, pair.Value))
                  .ToList()
            : KnownDatabases.All;
        Sections =
        [
            new SettingsNavItem(
                SettingsSection.Connection,
                "Kết nối",
                "Máy chủ và cơ sở dữ liệu DeltaBat",
                PackIconLucideKind.Database),
            new SettingsNavItem(
                SettingsSection.Files,
                "Kho tệp tin",
                "Nơi chứa tệp thu được trên đĩa",
                PackIconLucideKind.Folder),
            new SettingsNavItem(
                SettingsSection.Tables,
                "Bảng dữ liệu",
                "Cỡ trang mặc định của bảng",
                PackIconLucideKind.Table),
            new SettingsNavItem(
                SettingsSection.Appearance,
                "Giao diện",
                "Màu sắc của ứng dụng",
                PackIconLucideKind.Palette),
            new SettingsNavItem(
                SettingsSection.About,
                "Giới thiệu",
                "Phiên bản và nơi lưu cấu hình",
                PackIconLucideKind.Info)
        ];

        LoadFields();
    }

    public event EventHandler? CloseRequested;

    public IReadOnlyList<SettingsNavItem> Sections { get; }

    public IReadOnlyList<int> PageSizeOptions => TablePage.SizeOptions;

    public IReadOnlyList<DatabaseTarget> DatabaseTargets { get; }

    public IReadOnlyList<AppTheme> Themes => _themeService.Themes;

    public string SettingsFilePath => _userSettings.FilePath;

    public string DeploymentFileName => _appSettings.SettingsFileName;

    public string EffectiveConnectionSource => _appSettings.ConnectionStringSource;

    public bool HasUserConnectionOverride =>
        _userSettings.GetString(ConnectionSectionName, ConnectionName) is not null;

    public string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public string RuntimeDescription => RuntimeInformation.FrameworkDescription;

    public string OperatingSystem => RuntimeInformation.OSDescription;

    [ObservableProperty]
    private string _connectionString = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    private bool _isTestingConnection;

    [ObservableProperty]
    private string? _connectionTestText;

    [ObservableProperty]
    private DatabaseTarget? _selectedDatabaseTarget;

    [ObservableProperty]
    private bool _connectionTestSucceeded;

    [RelayCommand(CanExecute = nameof(CanTestConnection))]
    private async Task TestConnectionAsync()
    {
        if (!_connectionFactory.IsWellFormedConnectionString(ConnectionString))
        {
            ConnectionTestSucceeded = false;
            ConnectionTestText = "Chuỗi kết nối chưa đúng cú pháp nên chưa thử kết nối được.";
            return;
        }

        IsTestingConnection = true;
        ConnectionTestText = null;
        try
        {
            var probe = await _connectionFactory.ProbeAsync(ConnectionString).ConfigureAwait(true);
            ConnectionTestSucceeded = true;
            ConnectionTestText =
                $"Kết nối được {probe.DatabaseName} trên {probe.ServerName} " +
                $"({probe.Edition} {probe.ProductVersion}) trong {probe.ElapsedMilliseconds:F0} ms.";
        }
        catch (Exception exception)
        {
            ConnectionTestSucceeded = false;
            ConnectionTestText = $"Không kết nối được: {exception.Message}";
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    private bool CanTestConnection() =>
        !IsTestingConnection && !string.IsNullOrWhiteSpace(ConnectionString);

    [ObservableProperty]
    private string _filesRootPath = string.Empty;

    [ObservableProperty]
    private string _filesRootStatus = string.Empty;

    [ObservableProperty]
    private bool _filesRootExists;

    [RelayCommand]
    private void BrowseFilesRoot()
    {
        var picked = _dialog.AskFolder(
            "Chọn thư mục gốc chứa tệp thu được",
            string.IsNullOrWhiteSpace(FilesRootPath) ? null : FilesRootPath);
        if (!string.IsNullOrWhiteSpace(picked))
        {
            FilesRootPath = picked;
        }
    }

    [ObservableProperty]
    private int _pageSize = UserPreferences.DefaultPageSize;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _themeKey = AppThemeCatalog.Default.Key;

    [ObservableProperty]
    private SettingsSection _selectedSection = SettingsSection.Connection;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isDirty;

    [ObservableProperty]
    private bool _needsRestart;

    [RelayCommand]
    private void RestoreDefaults()
    {
        ConnectionString = string.Empty;
        FilesRootPath = string.Empty;
        PageSize = UserPreferences.DefaultPageSize;
        ThemeKey = AppThemeCatalog.Default.Key;
        OnPropertyChanged(nameof(HasUserConnectionOverride));
    }

    [RelayCommand]
    private void ClearUserSettings()
    {
        if (!_dialog.Confirm(
                "Xoá toàn bộ cấu hình người dùng?\n\n" +
                "Các tuỳ chọn trong cửa sổ này sẽ trở về mặc định. " +
                "Cấu hình kèm theo của ứng dụng không bị đụng tới.",
                "DeltaBat - Xoá cấu hình"))
        {
            return;
        }

        _userSettings.Reset();
        _saved = _userSettings.Current.Clone();
        LoadFields();
        _dialog.ShowInformation("Đã xoá cấu hình người dùng.");
    }

    [RelayCommand]
    private void OpenSettingsFolder()
    {
        var directory = Path.GetDirectoryName(_userSettings.FilePath);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        _dialog.OpenWithShell(directory);
    }

    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void Save()
    {
        if (!TryValidate(out var problem))
        {
            _dialog.ShowError(problem, "DeltaBat - Chưa lưu được cài đặt");
            return;
        }

        var preferences = _userSettings.Current.Clone();
        preferences.ConnectionStrings.DeltaBat = string.IsNullOrWhiteSpace(ConnectionString)
            ? null
            : ConnectionString.Trim();
        preferences.Files.RootPath = string.IsNullOrWhiteSpace(FilesRootPath) ? null : FilesRootPath.Trim();
        preferences.Tables.PageSize = TablePage.ClampPageSize(PageSize);
        preferences.Appearance.Theme = AppThemeCatalog.Normalize(ThemeKey);

        _userSettings.Save(preferences);
        _themeService.Apply(ThemeKey);

        var restartHint = NeedsRestart
            ? "Chuỗi kết nối và cỡ trang sẽ có hiệu lực sau khi khởi động lại ứng dụng."
            : "Các thay đổi đã có hiệu lực ngay.";

        _saved = _userSettings.Current.Clone();

        UpdateDirtyState();

        _dialog.ShowInformation($"Đã lưu cài đặt vào:\n{_userSettings.FilePath}\n\n{restartHint}");

        Close();
    }

    [RelayCommand]
    private void Close()
    {
        if (!ConfirmClose())
        {
            return;
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public bool ConfirmClose()
    {
        if (_closeConfirmed || !IsDirty)
        {
            return true;
        }

        if (!_dialog.Confirm(
                "Bạn có thay đổi chưa lưu.\n\nThoát và bỏ các thay đổi này?",
                "DeltaBat - Thoát cài đặt"))
        {
            return false;
        }

        _closeConfirmed = true;
        return true;
    }

    private bool HasChanges() => IsDirty;

    private bool TryValidate(out string problem)
    {
        problem = string.Empty;
        if (!string.IsNullOrWhiteSpace(ConnectionString) &&
            !_connectionFactory.IsWellFormedConnectionString(ConnectionString))
        {
            problem = "Chuỗi kết nối chưa đúng cú pháp nên chưa thể lưu.\n\n" +
                "Ví dụ: Server=(localdb)\\MSSQLLocalDB;Database=DeltaBat;Trusted_Connection=True;";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(FilesRootPath) && !Directory.Exists(FilesRootPath))
        {
            problem = $"Thư mục không tồn tại:\n{FilesRootPath}";
            return false;
        }

        return true;
    }

    private void LoadFields()
    {
        var preferences = _userSettings.Current;
        ConnectionString = preferences.ConnectionStrings.DeltaBat ?? string.Empty;
        FilesRootPath = preferences.Files.RootPath ?? string.Empty;
        PageSize = TablePage.ClampPageSize(preferences.Tables.PageSize);

        ThemeKey = AppThemeCatalog.NormalizeFromJson(preferences.Appearance.Theme);
        _themeService.Apply(ThemeKey);

        IsDirty = false;
        NeedsRestart = false;
        OnPropertyChanged(nameof(HasUserConnectionOverride));
        RefreshFilesRootStatus();
    }

    private void RefreshFilesRootStatus()
    {
        if (string.IsNullOrWhiteSpace(FilesRootPath))
        {
            FilesRootExists = false;
            FilesRootStatus = "Để trống thì đường dẫn trong CSDL được dùng nguyên như nó là.";
            return;
        }

        FilesRootExists = Directory.Exists(FilesRootPath);
        FilesRootStatus = FilesRootExists
            ? $"Thư mục có tồn tại: {Path.GetFullPath(FilesRootPath)}"
            : "Thư mục chưa tồn tại — sẽ không mở được tệp cho tới khi sửa lại.";
    }

    private void UpdateDirtyState()
    {
        var connectionChanged = !string.Equals(
            ConnectionString,
            _saved.ConnectionStrings.DeltaBat ?? string.Empty,
            StringComparison.Ordinal);
        var rootChanged = !string.Equals(
            FilesRootPath,
            _saved.Files.RootPath ?? string.Empty,
            StringComparison.Ordinal);
        var pageSizeChanged =
            TablePage.ClampPageSize(PageSize) != TablePage.ClampPageSize(_saved.Tables.PageSize);
        var themeChanged = !string.Equals(
            ThemeKey,
            AppThemeCatalog.NormalizeFromJson(_saved.Appearance.Theme),
            StringComparison.Ordinal);

        IsDirty = connectionChanged || rootChanged || pageSizeChanged || themeChanged;

        NeedsRestart = connectionChanged || pageSizeChanged;
    }

    partial void OnConnectionStringChanged(string value)
    {
        if (!_applyingDatabaseTarget)
        {
            SelectedDatabaseTarget = null;
        }

        UpdateDirtyState();
        TestConnectionCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedDatabaseTargetChanged(DatabaseTarget? value)
    {
        if (value is null)
        {
            return;
        }

        _applyingDatabaseTarget = true;
        try
        {
            ConnectionString = value.ConnectionString;
        }
        finally
        {
            _applyingDatabaseTarget = false;
        }
    }

    partial void OnFilesRootPathChanged(string value)
    {
        RefreshFilesRootStatus();
        UpdateDirtyState();
    }

    partial void OnPageSizeChanged(int value) => UpdateDirtyState();

    partial void OnThemeKeyChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _themeService.Apply(value);
        }

        UpdateDirtyState();
    }
}
