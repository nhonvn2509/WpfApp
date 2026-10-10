using System.Data;
using Microsoft.Data.SqlClient;
using WpfApp.Models.Tables;

namespace WpfApp.Services.Tables;

internal static class DayFgrScope
{
    public const byte DefaultDayFgr = 0;

    public static byte[] Build(DateTime? fromDate, DateTime? toDate)
    {
        if (fromDate is null && toDate is null)
        {
            return [];
        }

        var first = fromDate ?? toDate!.Value;
        var last = toDate ?? fromDate!.Value;

        if (first > last)
        {
            (first, last) = (last, first);
        }

        if (first.Year != last.Year || first.Month != last.Month)
        {
            return [];
        }

        var firstDay = first.Day;
        var lastDay = last.Day;
        if (firstDay > lastDay)
        {
            (firstDay, lastDay) = (lastDay, firstDay);
        }

        var days = new byte[lastDay - firstDay + 2];
        days[0] = DefaultDayFgr;
        var count = 1;
        for (var day = firstDay; day <= lastDay; day++)
        {
            days[count++] = (byte)day;
        }

        return days[..count];
    }

    public static void Append(
        SqlCommand command,
        SqlConditionBuilder conditions,
        string column,
        string parameterPrefix,
        IReadOnlyList<byte> days,
        bool forceIncludeZero = false)
    {
        if (days.Count == 0 && !forceIncludeZero)
        {
            return;
        }

        var names = new string[days.Count];
        for (var index = 0; index < days.Count; index++)
        {
            var name = $"{parameterPrefix}{index}";
            names[index] = name;
            command.Parameters.Add(name, SqlDbType.TinyInt).Value = days[index];
        }

        conditions.Add($"{column} IN ({string.Join(", ", names)})");
    }
}
internal sealed class FilesSqlQueryBuilder
{
    private const string BaseColumns = """"
        f.ID_F, f.ID_S, f.DAY_FGR, f.SHORT_NAME, f.EXT, f.LEN,
        ts.NAME, f.QUALITY, f.DT_START, f.PATH_NAME, f.IS_EXISTS
        """";

    public FilesSqlQueryBuilder(
        SqlCommand command,
        FilesQuery query,
        bool includeExtensions)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(query);
        _oldestFirst = query.SessionId.HasValue;

        var where = new SqlConditionBuilder();
        SqlConditionBuilder.AppendLike(
            command,
            where,
            "@Search",
            query.Search,
            "f.SHORT_NAME",
            "f.EXT",
            "f.PATH_NAME");
        where.AddDateRange(
            command,
            "f.DT_START",
            "@FromDate",
            "@ToDateExclusive",
            query.FromDate,
            query.ToDate);

        if (query.SessionId is { } sessionId)
        {
            where.Add("f.ID_S = @SessionId");
            command.Parameters.Add("@SessionId", SqlDbType.BigInt).Value = sessionId;
        }

        DayFgrScope.Append(
            command,
            where,
            "f.DAY_FGR",
            "@Day",
            DayFgrScope.Build(query.FromDate, query.ToDate));

        if (query.EffectiveChainIds.Count > 0)
        {
            var chainCondition = new SqlConditionBuilder();
            chainCondition.AddInList(
                command,
                "s.ID_TRANSP",
                "@ChainId",
                query.EffectiveChainIds.Distinct().OrderBy(id => id).ToArray());
            _chainExists = $"""
                EXISTS (
                    SELECT 1
                    FROM msg.SESSIONS AS s WITH (NOLOCK)
                    WHERE s.ID_S = f.ID_S
                      AND {chainCondition.Build()}
                )
                """;
        }

        if (includeExtensions)
        {
            var extensions = FileExtensionCatalog.Normalize(query.EffectiveExtensions);
            where.AddInList(command, "f.EXT", "@Extension", extensions, size: 16);
        }

        _where = where.Build();
    }

    private readonly string _where;

    private readonly string? _chainExists;

    private readonly bool _oldestFirst;

    public string WhereClause => _chainExists is null
        ? $"WHERE {_where}"
        : $"WHERE {_where}\nAND {_chainExists}";

    private const string FromClause = """
        msg.FILES AS f WITH (NOLOCK)
        LEFT JOIN info.TYPES_STATUS AS ts ON ts.ID = f.ID_STATUS
        """;

    public string BuildPageSql(bool keyset, FilePageCursor? cursor)
    {
        var direction = _oldestFirst ? "ASC" : "DESC";
        var cursorOperator = _oldestFirst ? ">" : "<";
        var cursorCondition = keyset && cursor is not null
            ? $"AND (f.ID_F {cursorOperator} @CursorId OR (f.ID_F = @CursorId AND f.DAY_FGR {cursorOperator} @CursorDay))"
            : string.Empty;

        var head = keyset ? "SELECT TOP (@Fetch)" : "SELECT";
        var pagination = keyset
            ? $"ORDER BY f.ID_F {direction}, f.DAY_FGR {direction}"
            : $"""
              ORDER BY f.ID_F {direction}, f.DAY_FGR {direction}
              OFFSET @Offset ROWS FETCH NEXT @Fetch ROWS ONLY
              """;
        return $""""
            {head}
                {BaseColumns}
            FROM {FromClause}
            {WhereClause}
            {cursorCondition}
            {pagination}
            OPTION (RECOMPILE)
            """";
    }

    public string BuildCountSql() => $""""
        SELECT COUNT_BIG(*)
        FROM {FromClause}
        {WhereClause}
        OPTION (RECOMPILE)
        """";

    public string BuildStatisticsSql() => $""""
        SELECT f.EXT, COUNT_BIG(DISTINCT f.ID_S) AS SESSION_COUNT
        FROM {FromClause}
        {WhereClause}
        GROUP BY f.EXT
        OPTION (RECOMPILE)
        """";

    private static string[] NormalizeExtensions(IEnumerable<string> extensions) =>
        extensions
            .Select(value => value.Trim().TrimStart('.'))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
