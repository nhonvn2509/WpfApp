using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using WpfApp.Models.Files;
using WpfApp.Models.Tables;
using WpfApp.Services.Data;
namespace WpfApp.Services.Tables;

internal readonly record struct FilePageCursor(long IdF, byte DayFgr);

public sealed class FilesLoadService : SqlTableLoadService, IFilesLoadService
{
    private const int CommandTimeoutSeconds = 90;

    public FilesLoadService(
        ISqlConnectionFactory connectionFactory,
        ITableRowCountProvider rowCountProvider)
        : base(connectionFactory)
    {
        _rowCountProvider = rowCountProvider;
    }

    private readonly ITableRowCountProvider _rowCountProvider;

    public async Task<PagedResult<FileRow>> LoadPageAsync(
        FilesQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = query.Page;
        FilePageCursor cursor = default;
        var useKeyset = page.PageIndex > 1 && TryParseCursor(query.KeysetCursorJson, out cursor);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        var builder = new FilesSqlQueryBuilder(command, query, includeExtensions: true);
        command.Parameters.Add("@Fetch", System.Data.SqlDbType.Int).Value = page.Take + 1;
        if (useKeyset)
        {
            command.Parameters.Add("@CursorId", System.Data.SqlDbType.BigInt).Value = cursor.IdF;
            command.Parameters.Add("@CursorDay", System.Data.SqlDbType.TinyInt).Value = cursor.DayFgr;
        }
        else
        {
            command.Parameters.Add("@Offset", System.Data.SqlDbType.Int).Value = page.Skip;
        }
        command.CommandText = builder.BuildPageSql(useKeyset, useKeyset ? cursor : null);
        var rows = new List<FileRow>(page.Take);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(ReadFileRow(reader));
        }

        var hasMoreRows = rows.Count > page.Take;
        if (hasMoreRows)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return PagedResult<FileRow>.CreateUnknownTotal(rows, page, hasMoreRows);
    }

    public async Task<long> CountAsync(FilesQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.IsUnfiltered)
        {
            var whole = await _rowCountProvider.GetRowCountAsync("msg.FILES", cancellationToken)
                .ConfigureAwait(false);
            if (whole >= 0)
            {
                return whole;
            }
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        var builder = new FilesSqlQueryBuilder(command, query, includeExtensions: true);
        command.CommandText = builder.BuildCountSql();
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is null or DBNull ? -1 : Convert.ToInt64(result);
    }

    public async Task<FileRow?> LoadFirstFileOfSessionAsync(
        long sessionId,
        byte dayFgr,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText = """"
            SELECT TOP (1)
                f.ID_F, f.ID_S, f.DAY_FGR, f.SHORT_NAME, f.EXT, f.LEN,
                ts.NAME, f.QUALITY, f.DT_START, f.PATH_NAME, f.IS_EXISTS
            FROM msg.FILES AS f WITH (NOLOCK)
            LEFT JOIN info.TYPES_STATUS AS ts ON ts.ID = f.ID_STATUS
            WHERE f.ID_S = @IdS AND f.DAY_FGR = @DayFgr
            ORDER BY f.ID_F ASC
            OPTION (RECOMPILE)
            """";
        command.Parameters.Add("@IdS", SqlDbType.BigInt).Value = sessionId;
        command.Parameters.Add("@DayFgr", SqlDbType.TinyInt).Value = dayFgr;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadFileRow(reader) : null;
    }

    internal static bool TryParseCursor(string? json, out FilePageCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            cursor = JsonSerializer.Deserialize<FilePageCursor>(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public async Task<FileExtensionStatistics> LoadExtensionStatisticsAsync(
        FilesQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        var builder = new FilesSqlQueryBuilder(command, query, includeExtensions: false);
        command.CommandText = builder.BuildStatisticsSql();
        var counts = new List<FileExtensionCount>();
        long totalCount = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var count = reader.GetInt64Value(1);
            totalCount += count;
            if (!reader.IsDBNull(0))
            {
                var extension = reader.GetStringOrNull(0)?.Trim() ?? string.Empty;
                if (extension.Length > 0)
                {
                    counts.Add(new FileExtensionCount(extension, count));
                }
            }
        }

        counts.Sort(static (left, right) =>
        {
            var byCount = right.Count.CompareTo(left.Count);
            return byCount != 0 ? byCount : string.CompareOrdinal(left.Extension, right.Extension);
        });

        return new FileExtensionStatistics(counts, totalCount);
    }

    private static FileRow ReadFileRow(SqlDataReader reader) => new()
    {
        IdF = reader.GetInt64Value(0),
        IdS = reader.GetInt64Value(1),
        DayFgr = reader.GetByteValue(2),
        Name = reader.GetStringOrNull(3),
        Extension = reader.GetStringOrNull(4),
        Length = reader.GetInt64OrNull(5),
        Status = reader.GetStringOrNull(6),
        Quality = reader.GetInt32OrNull(7),
        Start = reader.GetDateTimeOrNull(8),
        Path = reader.GetStringOrNull(9),
        Exists = reader.GetBooleanOrNull(10)
    };
}
