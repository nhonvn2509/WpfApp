using System.Diagnostics;
using Microsoft.Data.SqlClient;
using WpfApp.Models.Database;

namespace WpfApp.Services.Data;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private const int ProbeTimeoutSeconds = 15;

    private readonly IAppSettings _appSettings;

    public SqlConnectionFactory(IAppSettings appSettings)
    {
        ArgumentNullException.ThrowIfNull(appSettings);
        _appSettings = appSettings;
    }

    public SqlConnection Create() => new(_appSettings.ConnectionString);

    public bool IsWellFormedConnectionString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            _ = new SqlConnectionStringBuilder(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public async Task<ConnectionProbe> ProbeAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var stopwatch = Stopwatch.StartNew();

        var builder = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = ProbeTimeoutSeconds };
        await using var connection = new SqlConnection(builder.ConnectionString);

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = ProbeTimeoutSeconds;
        command.CommandText = """
            SELECT @@SERVERNAME,
                   DB_NAME(),
                   CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(60)),
                   CAST(SERVERPROPERTY('Edition') AS nvarchar(120))
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return new ConnectionProbe(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            stopwatch.Elapsed.TotalMilliseconds);
    }
}
