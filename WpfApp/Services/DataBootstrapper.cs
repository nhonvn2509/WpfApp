using System.Data;
using Microsoft.Data.SqlClient;
using WpfApp.Services.Data;
using Task = System.Threading.Tasks.Task;

namespace WpfApp.Services;

public sealed class DataBootstrapper : IDataBootstrapper
{
    private static readonly TimeSpan CompletionDelay = TimeSpan.FromMilliseconds(250);
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IAppSettings _settings;

    public DataBootstrapper(ISqlConnectionFactory connectionFactory, IAppSettings settings)
    {
        _connectionFactory = connectionFactory;
        _settings = settings;
    }

    public string? DatabaseName { get; private set; }
    public string? ServerVersion { get; private set; }
    public int ConfigCount { get; private set; }

    public async Task InitializeAsync(Action<string> updateStatus)
    {
        ArgumentNullException.ThrowIfNull(updateStatus);
        updateStatus("Đang kết nối cơ sở dữ liệu...");
        await using var connection = _connectionFactory.Create();
        try
        {
            await connection.OpenAsync().ConfigureAwait(false);
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                $"Không thể kết nối đến cơ sở dữ liệu '{connection.Database}'. " +
                $"Kiểm tra lại cấu hình trong {_settings.SettingsFileName}.\n\n{ex.Message}",
                ex);
        }

        DatabaseName = connection.Database;
        ServerVersion = connection.ServerVersion;
        updateStatus($"Đã kết nối {DatabaseName} (SQL Server {ServerVersion})");

        updateStatus("Đang nạp tham số hệ thống...");
        ConfigCount = await LoadConfigCountAsync(connection).ConfigureAwait(false);
        updateStatus($"Đã nạp {ConfigCount} tham số hệ thống");

        await DetectExtensionConventionAsync(connection).ConfigureAwait(false);
        updateStatus(ExtConventionText);

        await Task.Delay(CompletionDelay);
        updateStatus("Khởi tạo hoàn tất");
    }

    private string ExtConventionText =>
        Services.Files.FileExtensionCatalog.ExtStoredWithLeadingDot
            ? "Đuôi file trong CSDL lưu kèm dấu chấm (ví dụ .pdf)"
            : "Đuôi file trong CSDL lưu không dấu chấm (ví dụ pdf)";

    private static async Task DetectExtensionConventionAsync(SqlConnection connection)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandTimeout = 20;
            command.CommandText = """
                SELECT TOP (1) EXT
                FROM msg.FILES WITH (NOLOCK)
                WHERE EXT IS NOT NULL AND EXT <> ''
                ORDER BY ID_F DESC
                """;
            var value = await command.ExecuteScalarAsync().ConfigureAwait(false);
            if (value is string text && text.Trim().Length > 0)
            {
                Services.Files.FileExtensionCatalog.ExtStoredWithLeadingDot =
                    text.TrimStart().StartsWith('.');
            }
        }
        catch (Exception)
        {
        }
    }

    private static async Task<int> LoadConfigCountAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM admin.CONFIG WITH (NOLOCK)";
        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }
}
