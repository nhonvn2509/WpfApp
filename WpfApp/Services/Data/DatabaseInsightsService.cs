using System.Data;
using Microsoft.Data.SqlClient;
using WpfApp.Models.Database;

namespace WpfApp.Services.Data;

public sealed class DatabaseInsightsService : IDatabaseInsightsService
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

    private const int CommandTimeoutSeconds = 20;

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _metadataStamp = DateTime.MinValue;
    private Dictionary<string, bool> _indexedColumns = [];
    private List<IndexInsight> _indexes = [];
    private List<TableStorageInsight> _storage = [];

    public DatabaseInsightsService(ISqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task<bool> IsColumnIndexedAsync(
        string qualifiedTable,
        string column,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(qualifiedTable) || string.IsNullOrWhiteSpace(column))
        {
            return false;
        }

        await EnsureMetadataAsync(cancellationToken).ConfigureAwait(false);
        return _indexedColumns.TryGetValue(Key(qualifiedTable, column), out var indexed) && indexed;
    }

    public async Task<IReadOnlyList<TableStorageInsight>> GetStorageAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureMetadataAsync(cancellationToken).ConfigureAwait(false);
        return _storage;
    }

    public async Task<IReadOnlyList<IndexInsight>> GetIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureMetadataAsync(cancellationToken).ConfigureAwait(false);
        return _indexes;
    }

    private static string Key(string table, string column) =>
        table + "|" + column;

    private async Task EnsureMetadataAsync(CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow - _metadataStamp < CacheLifetime)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (DateTime.UtcNow - _metadataStamp < CacheLifetime)
            {
                return;
            }

            var columns = await LoadIndexedColumnsAsync(cancellationToken).ConfigureAwait(false);
            var indexes = await LoadIndexesAsync(cancellationToken).ConfigureAwait(false);
            var storage = await LoadStorageAsync(cancellationToken).ConfigureAwait(false);

            _indexedColumns = columns;
            _indexes = indexes;
            _storage = storage;
            _metadataStamp = DateTime.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<SlowQueryInsight>> GetTopSlowQueriesAsync(
        int topCount,
        CancellationToken cancellationToken = default)
    {
        if (topCount <= 0)
        {
            return [];
        }

        return await QueryAsync(
            """
            SELECT TOP (@TopCount)
                REPLACE(REPLACE(REPLACE(LEFT(st.text, 200), CHAR(13), ' '), CHAR(10), ' '), CHAR(9), ' '),
                qs.total_elapsed_time / 1000,
                qs.execution_count,
                qs.total_elapsed_time / 1000.0 / NULLIF(qs.execution_count, 0)
            FROM sys.dm_exec_query_stats AS qs
            CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st
            WHERE st.text IS NOT NULL
              AND st.text NOT LIKE '%sys.%'
            ORDER BY qs.total_elapsed_time DESC
            """,
            command => command.Parameters.Add("@TopCount", SqlDbType.Int).Value = topCount,
            reader => new SlowQueryInsight(
                reader.GetStringOrNull(0) ?? string.Empty,
                reader.GetInt64OrNull(1) ?? 0,
                reader.GetInt64OrNull(2) ?? 0,
                reader.IsDBNull(3) ? 0 : Convert.ToDouble(reader.GetValue(3))),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Dictionary<string, bool>> LoadIndexedColumnsAsync(CancellationToken cancellationToken)
    {
        var pairs = await QueryAsync(
            """
            SELECT s.name + '.' + t.name, c.name
            FROM sys.indexes AS i
            JOIN sys.tables AS t ON t.object_id = i.object_id
            JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            JOIN sys.index_columns AS ic
                ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns AS c
                ON c.object_id = i.object_id AND c.column_id = ic.column_id
            WHERE i.type > 0
              AND s.name IN ('msg', 'main', 'info', 'app', 'stor')
            """,
            _ => { },
            reader => Key(reader.GetStringOrNull(0) ?? string.Empty, reader.GetStringOrNull(1) ?? string.Empty),
            cancellationToken).ConfigureAwait(false);

        var result = new Dictionary<string, bool>(pairs.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in pairs)
        {
            result[pair] = true;
        }

        return result;
    }

    private Task<List<IndexInsight>> LoadIndexesAsync(CancellationToken cancellationToken) =>
        QueryAsync(
            """
            SELECT t.name,
                   i.name,
                   CAST(i.type_desc AS nvarchar(60)),
                   ISNULL(us.user_seeks, 0),
                   ISNULL(us.user_scans, 0),
                   us.last_user_seek
            FROM sys.indexes AS i
            JOIN sys.tables AS t ON t.object_id = i.object_id
            JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            LEFT JOIN sys.dm_db_index_usage_stats AS us
                ON us.object_id = i.object_id
               AND us.index_id = i.index_id
               AND us.database_id = DB_ID()
            WHERE i.type > 0
              AND s.name IN ('msg', 'main')
            ORDER BY t.name, i.name
            """,
            _ => { },
            reader => new IndexInsight(
                reader.GetStringOrNull(0) ?? string.Empty,
                reader.GetStringOrNull(1) ?? string.Empty,
                reader.GetStringOrNull(2) ?? string.Empty,
                reader.GetInt64OrNull(3) ?? 0,
                reader.GetInt64OrNull(4) ?? 0,
                reader.IsDBNull(5) ? null : reader.GetDateTimeOrNull(5)),
            cancellationToken);

    private Task<List<TableStorageInsight>> LoadStorageAsync(CancellationToken cancellationToken) =>
        QueryAsync(
            """
            SELECT s.name + '.' + t.name,
                   SUM(p.row_count),
                   SUM(p.reserved_page_count) * 8 / 1024
            FROM sys.dm_db_partition_stats AS p
            JOIN sys.tables AS t ON t.object_id = p.object_id
            JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            WHERE p.index_id IN (0, 1)
              AND s.name IN ('msg', 'main')
            GROUP BY s.name, t.name
            ORDER BY 2 DESC
            """,
            _ => { },
            reader => new TableStorageInsight(
                reader.GetStringOrNull(0) ?? string.Empty,
                reader.GetInt64OrNull(1) ?? 0,
                reader.GetInt64OrNull(2) ?? 0),
            cancellationToken);

    private async Task<List<TR>> QueryAsync<TR>(
        string sql,
        Action<SqlCommand> addParameters,
        Func<SqlDataReader, TR> map,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.Create();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = CommandTimeoutSeconds;
            command.CommandText = sql;
            addParameters(command);
            var results = new List<TR>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(map(reader));
            }

            return results;
        }
        catch (Exception)
        {
            return [];
        }
    }
}
