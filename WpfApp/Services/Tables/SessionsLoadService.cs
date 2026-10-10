using System.Data;
using Microsoft.Data.SqlClient;
using WpfApp.Models.Tables;
using WpfApp.Services.Data;

namespace WpfApp.Services.Tables;

internal readonly record struct SessionPageCursor(long IdS, byte DayFgr);

internal sealed class SessionsSqlQueryBuilder
{
    private readonly string _where;

    public SessionsSqlQueryBuilder(SqlCommand command, SessionsQuery query)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(query);

        var conditions = new SqlConditionBuilder();
        SqlConditionBuilder.AppendLike(command, conditions, "@Search", query.Search, "s.NAME", "s.MEMO");
        conditions.AddDateRange(
            command,
            "s.DT_START",
            "@FromDate",
            "@ToDateExclusive",
            query.FromDate,
            query.ToDate);

        if (query.StatusId is { } statusId)
        {
            conditions.Add("s.ID_STATUS = @StatusId");
            command.Parameters.Add("@StatusId", SqlDbType.Int).Value = statusId;
        }

        conditions.AddInList(
            command,
            "s.ID_TRANSP",
            "@ChainId",
            query.EffectiveChainIds.Distinct().OrderBy(id => id).ToArray());

        var extensions = FileExtensionCatalog.Normalize(query.EffectiveExtensions);
        if (extensions.Length > 0)
        {
            var fileConditions = new SqlConditionBuilder();
            fileConditions.AddInList(command, "f.EXT", "@Extension", extensions, size: 16);
            conditions.Add($"""
                EXISTS (
                    SELECT 1
                    FROM msg.FILES AS f WITH (NOLOCK)
                    WHERE f.ID_S = s.ID_S
                      AND {fileConditions.Build()}
                )
                """);
        }

        DayFgrScope.Append(
            command,
            conditions,
            "s.DAY_FGR",
            "@Day",
            DayFgrScope.Build(query.FromDate, query.ToDate));

        _where = conditions.Build();
    }

    public string WhereClause => $"WHERE {_where}";

    public string BuildPageSql(bool keyset, SessionPageCursor? cursor)
    {
        var useKeyset = keyset && cursor is not null;

        var cursorCondition = useKeyset
            ? "AND (s.ID_S < @CursorId OR (s.ID_S = @CursorId AND s.DAY_FGR < @CursorDay))"
            : string.Empty;

        var pagination = useKeyset
            ? "ORDER BY s.ID_S DESC, s.DAY_FGR DESC"
            : $"ORDER BY s.ID_S DESC, s.DAY_FGR DESC{Environment.NewLine}"
              + "               OFFSET @Offset ROWS FETCH NEXT @Fetch ROWS ONLY";

        return $"""
            SELECT {(useKeyset ? "TOP (@Fetch)" : string.Empty)}
                {SelectColumns}
            FROM {FromClause}
            {WhereClause}
            {cursorCondition}
            {pagination}
            """;
    }

    public string BuildCountSql() => $"""
        SELECT COUNT_BIG(*)
        FROM {FromClause}
        {WhereClause}
        """;

    public string BuildMatchingFilesCountSql(SqlCommand command, SessionsQuery query)
    {
        var extensions = FileExtensionCatalog.Normalize(query.EffectiveExtensions);
        var fileConditions = new SqlConditionBuilder();
        fileConditions.AddInList(
            command,
            "f.EXT",
            "@FileExtension",
            extensions,
            size: 16);

        var extensionClause = extensions.Length == 0
            ? string.Empty
            : $"{Environment.NewLine}AND {fileConditions.Build()}";

        return $"""
            SELECT COUNT_BIG(*)
            FROM msg.FILES AS f WITH (NOLOCK)
            WHERE EXISTS (
                SELECT 1
                FROM {FromClause}
                {WhereClause}
                  AND s.ID_S = f.ID_S
            )
            {extensionClause}
            """;
    }

    private const string FromClause = """
        msg.ViewSessionsNC AS s WITH (NOLOCK)
        """;

    private const string SelectColumns = """
        s.ID_S, s.DAY_FGR, s.NAME, s.DT_START, s.DT_FINAL, s.DURATION,
        s.PRIOR, s.QUALITY, s.N_FILES, s.MEMO, s.ID_TRANSP,
        CONVERT(nvarchar(32), s.STATUS) AS ST_NAME,
        CONVERT(nvarchar(32), s.TYPE_S) AS TYPE_NAME,
        CONVERT(nvarchar(64), s.SAT_NAME) AS CHAIN_NAME
        """;
}

public sealed class SessionsLoadService : SqlTableLoadService, ISessionsLoadService
{
    private const int CommandTimeoutSeconds = 60;

    private IReadOnlyList<TableFilterOption>? _statusOptions;

    private static readonly TimeSpan CountCacheLifetime = TimeSpan.FromMinutes(2);

    private const int CountCommandTimeoutSeconds = 180;

    private readonly Dictionary<string, CachedCount> _countCache = [];
    private readonly SemaphoreSlim _countGate = new(1, 1);

    private sealed record CachedCount(long Value, DateTime Stamp);

    public SessionsLoadService(
        ISqlConnectionFactory connectionFactory,
        ITableRowCountProvider rowCountProvider)
        : base(connectionFactory)
    {
        _rowCountProvider = rowCountProvider;
    }

    private readonly ITableRowCountProvider _rowCountProvider;

    public async Task<PagedResult<SessionRow>> LoadPageAsync(
        SessionsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = query.Page;
        SessionPageCursor cursor = default;
        var useKeyset = page.PageIndex > 1 && TryParseCursor(query.KeysetCursorJson, out cursor);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.Parameters.Add("@Fetch", SqlDbType.Int).Value = page.Take + 1;

        var builder = new SessionsSqlQueryBuilder(command, query);
        if (useKeyset)
        {
            command.Parameters.Add("@CursorId", SqlDbType.BigInt).Value = cursor.IdS;
            command.Parameters.Add("@CursorDay", SqlDbType.TinyInt).Value = cursor.DayFgr;
        }
        else
        {
            command.Parameters.Add("@Offset", SqlDbType.Int).Value = page.Skip;
        }

        command.CommandText = SqlConditionBuilder.WithRecompile(
            builder.BuildPageSql(useKeyset, cursor));

        var rows = new List<SessionRow>(page.Take);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(ReadSessionRow(reader));
            }
        }

        var hasMoreRows = rows.Count > page.Take;
        if (hasMoreRows)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        await LoadPageContactsAsync(connection, rows, cancellationToken).ConfigureAwait(false);

        return PagedResult<SessionRow>.CreateUnknownTotal(rows, page, hasMoreRows);
    }

    private static async Task LoadPageContactsAsync(
        SqlConnection connection,
        IReadOnlyList<SessionRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        var sessionValues = string.Join(
            ", ",
            rows.Select((_, index) => $"(@SessionId{index}, @DayFgr{index})"));

        for (var index = 0; index < rows.Count; index++)
        {
            command.Parameters.Add($"@SessionId{index}", SqlDbType.BigInt).Value = rows[index].IdS;
            command.Parameters.Add($"@DayFgr{index}", SqlDbType.TinyInt).Value = rows[index].DayFgr;
        }

        command.CommandText = $"""
            WITH PageSessions (ID_S, DAY_FGR) AS
            (
                SELECT selected.ID_S, selected.DAY_FGR
                FROM (VALUES {sessionValues}) AS selected (ID_S, DAY_FGR)
            ),
            RankedContacts AS
            (
                SELECT
                    c.ID_S, c.DAY_FGR, contactType.NAME AS CONTACT_TYPE, c.DIR, c.VAL,
                    ROW_NUMBER() OVER (
                        PARTITION BY c.ID_S, c.DAY_FGR, c.ID_TYPE, c.DIR
                        ORDER BY c.ID_C DESC
                    ) AS ContactRank
                FROM PageSessions AS page
                INNER JOIN msg.CONTACTS AS c WITH (NOLOCK)
                    ON c.ID_S = page.ID_S
                   AND c.DAY_FGR = page.DAY_FGR
                INNER JOIN info.TYPES_CONTACT AS contactType
                    ON contactType.ID = c.ID_TYPE
                WHERE contactType.NAME IN (N'IP', N'PHONE')
                  AND c.DIR IN ('FROM', 'TO')
            )
            SELECT ID_S, DAY_FGR, CONTACT_TYPE, DIR, VAL
            FROM RankedContacts
            WHERE ContactRank = 1
            """;

        var sessionRows = rows.ToDictionary(row => (row.IdS, row.DayFgr));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var key = (reader.GetInt64Value(0), reader.GetByteValue(1));
            if (!sessionRows.TryGetValue(key, out var row))
            {
                continue;
            }

            var contactType = reader.GetStringOrNull(2);
            var direction = reader.GetStringOrNull(3);
            var value = reader.GetStringOrNull(4);

            if (string.Equals(contactType, "IP", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(direction, "FROM", StringComparison.OrdinalIgnoreCase))
                {
                    row.IpFrom = value;
                }
                else if (string.Equals(direction, "TO", StringComparison.OrdinalIgnoreCase))
                {
                    row.IpTo = value;
                }
            }
            else if (string.Equals(contactType, "PHONE", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(direction, "FROM", StringComparison.OrdinalIgnoreCase))
                {
                    row.PhoneFrom = value;
                }
                else if (string.Equals(direction, "TO", StringComparison.OrdinalIgnoreCase))
                {
                    row.PhoneTo = value;
                }
            }
        }
    }

    private static bool TryParseCursor(string? json, out SessionPageCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            var parts = json.Split('|', 2);
            if (parts.Length == 2
                && long.TryParse(parts[0], out var id)
                && byte.TryParse(parts[1], out var day))
            {
                cursor = new SessionPageCursor(id, day);
                return true;
            }
        }
        catch (FormatException)
        {
        }

        return false;
    }

    public async Task<long> CountAsync(SessionsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.IsUnfiltered)
        {
            var whole = await _rowCountProvider.GetRowCountAsync("msg.SESSIONS", cancellationToken)
                .ConfigureAwait(false);
            if (whole >= 0)
            {
                return whole;
            }
        }

        var cacheKey = BuildCountCacheKey(query);
        if (TryGetCachedCount(cacheKey, out var cached))
        {
            return cached;
        }

        await _countGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TryGetCachedCount(cacheKey, out cached))
            {
                return cached;
            }

            var count = await QueryCountAsync(query, cancellationToken).ConfigureAwait(false);
            _countCache[cacheKey] = new CachedCount(count, DateTime.Now);
            return count;
        }
        finally
        {
            _countGate.Release();
        }
    }

    public async Task<long> CountMatchingFilesAsync(
        SessionsQuery query,
        CancellationToken cancellationToken = default)
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

        var cacheKey = $"files|{BuildCountCacheKey(query)}";
        if (TryGetCachedCount(cacheKey, out var cached))
        {
            return cached;
        }

        await _countGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TryGetCachedCount(cacheKey, out cached))
            {
                return cached;
            }

            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = CountCommandTimeoutSeconds;
            var builder = new SessionsSqlQueryBuilder(command, query);
            command.CommandText = SqlConditionBuilder.WithRecompile(
                builder.BuildMatchingFilesCountSql(command, query));
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var count = result is null or DBNull ? -1 : Convert.ToInt64(result);
            _countCache[cacheKey] = new CachedCount(count, DateTime.Now);
            return count;
        }
        finally
        {
            _countGate.Release();
        }
    }

    private bool TryGetCachedCount(string key, out long count)
    {
        if (_countCache.TryGetValue(key, out var entry)
            && DateTime.Now - entry.Stamp < CountCacheLifetime)
        {
            count = entry.Value;
            return true;
        }

        _countCache.Remove(key);
        count = 0;
        return false;
    }

    private static string BuildCountCacheKey(SessionsQuery query) =>
        string.Join(
            '|',
            query.Search?.Trim() ?? "-",
            query.FromDate?.ToString("yyyyMMdd") ?? "-",
            query.ToDate?.ToString("yyyyMMdd") ?? "-",
            query.StatusId?.ToString() ?? "-",
            string.Join(',', query.EffectiveChainIds.Distinct().OrderBy(id => id)),
            string.Join(',', FileExtensionCatalog.Normalize(query.EffectiveExtensions)
                .Order(StringComparer.OrdinalIgnoreCase)));

    private async Task<long> QueryCountAsync(SessionsQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = CountCommandTimeoutSeconds;
        var builder = new SessionsSqlQueryBuilder(command, query);
        command.CommandText = SqlConditionBuilder.WithRecompile(builder.BuildCountSql());
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is null or DBNull ? -1 : Convert.ToInt64(result);
    }

    private static SessionRow ReadSessionRow(SqlDataReader reader) => new()
    {
        IdS = reader.GetInt64Value(0),
        DayFgr = reader.GetByteValue(1),
        Name = reader.GetStringOrNull(2),
        Start = reader.GetDateTimeOrNull(3) ?? default,
        Final = reader.GetDateTimeOrNull(4) ?? default,
        Duration = reader.GetStringOrNull(5),
        Priority = reader.GetInt32OrNull(6),
        Quality = reader.GetInt32OrNull(7),
        Files = reader.GetInt32OrNull(8),
        Memo = reader.GetStringOrNull(9),
        ChainId = reader.GetInt32OrNull(10),
        ValXml = null,
        StatusName = reader.GetStringOrNull(11),
        TypeName = reader.GetStringOrNull(12),
        ChainName = reader.GetStringOrNull(13)
    };

    public async Task<string?> LoadSessionXmlAsync(
        long sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.Parameters.Add("@SessionId", SqlDbType.BigInt).Value = sessionId;

        command.CommandText = """
            SELECT CONVERT(nvarchar(max), VAL_XML)
            FROM msg.SESSIONS WITH (NOLOCK)
            WHERE ID_S = @SessionId
            OPTION (RECOMPILE)
            """;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is null or DBNull ? null : Convert.ToString(result);
    }

    public async Task<IReadOnlyDictionary<string, DateTime>> LoadFileStartsAsync(
        long sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.Parameters.Add("@SessionId", SqlDbType.BigInt).Value = sessionId;

        command.CommandText = """
            SELECT PATH_NAME, SHORT_NAME, DT_START
            FROM msg.FILES WITH (NOLOCK)
            WHERE ID_S = @SessionId
            OPTION (RECOMPILE)
            """;

        var starts = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var start = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
            if (start is null)
            {
                continue;
            }

            // Khóa theo path trước, name (fallback) sau — match với node cây tệp.
            if (!reader.IsDBNull(0) && !string.IsNullOrWhiteSpace(reader.GetString(0)))
            {
                starts[reader.GetString(0)] = start.Value;
            }

            if (!reader.IsDBNull(1) && !string.IsNullOrWhiteSpace(reader.GetString(1)))
            {
                starts.TryAdd(reader.GetString(1), start.Value);
            }
        }

        return starts;
    }

    public async Task<IReadOnlyList<TableFilterOption>> LoadStatusOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        if (_statusOptions is { } cached)
        {
            return cached;
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText = """"
            SELECT ts.ID, ts.NAME
            FROM info.TYPES_STATUS AS ts WITH (NOLOCK)
            ORDER BY ts.NAME ASC
            OPTION (RECOMPILE)
            """";
        var options = new List<TableFilterOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            options.Add(new TableFilterOption(reader.GetInt32Value(0), reader.GetStringOrNull(1) ?? string.Empty));
        }

        options.Insert(0, new TableFilterOption(null, "Tất cả trạng thái"));
        _statusOptions = options;
        return options;
    }
}
