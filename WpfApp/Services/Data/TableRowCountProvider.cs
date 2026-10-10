using System.Data;
using Microsoft.Data.SqlClient;

namespace WpfApp.Services.Data;

public sealed class TableRowCountProvider : ITableRowCountProvider
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);

    private const int CommandTimeoutSeconds = 30;

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly Dictionary<string, CachedCount> _cache = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TableRowCountProvider(ISqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task<long> GetRowCountAsync(
        string qualifiedTableName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(qualifiedTableName))
        {
            return -1;
        }

        if (IsFresh(qualifiedTableName))
        {
            return _cache[qualifiedTableName].Count;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsFresh(qualifiedTableName))
            {
                return _cache[qualifiedTableName].Count;
            }

            var count = await QueryAsync(qualifiedTableName, cancellationToken).ConfigureAwait(false);
            _cache[qualifiedTableName] = new CachedCount(count, DateTime.UtcNow);
            return count;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsFresh(string key) =>
        _cache.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.Stamp < CacheLifetime;

    private async Task<long> QueryAsync(string qualifiedTableName, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.Create();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = CommandTimeoutSeconds;
            command.CommandText = """
                SELECT SUM(p.row_count)
                FROM sys.dm_db_partition_stats AS p
                WHERE p.object_id = OBJECT_ID(@TableName)
                  AND p.index_id IN (0, 1)
                """;
            command.Parameters.Add("@TableName", SqlDbType.NVarChar, 300).Value = qualifiedTableName;
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null or DBNull ? -1 : Convert.ToInt64(value);
        }
        catch (Exception)
        {
            return -1;
        }
    }

    private sealed record CachedCount(long Count, DateTime Stamp);
}
