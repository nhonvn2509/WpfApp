using System.Data;
using Microsoft.Data.SqlClient;
using WpfApp.Models.Tables;
using WpfApp.Services.Data;

namespace WpfApp.Services.Tables;

public sealed class ChainsLoadService : SqlTableLoadService, IChainsLoadService
{
    private const int CommandTimeoutSeconds = 60;

    private const string SelectColumns = """"
        c.ID, c.PROFILE, c.MODEM, c.SRC_NAME, c.SAT_NAME, c.SAT_BAND, c.SAT_POL,
        c.FREQUNCY_MHZ, c.MODULATION, c.EQUIPMENT, c.DIR_FROM, c.DIR_TO,
        c.DT_INS, c.DT_UPD, c.DESCRIPTION, c.CNT_SS
        """";

    public ChainsLoadService(ISqlConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public async Task<PagedResult<ChainRow>> LoadPageAsync(
        ChainsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = query.Page;

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;

        var conditions = new SqlConditionBuilder();
        SqlConditionBuilder.AppendLike(
            command,
            conditions,
            "@Search",
            query.Search,
            "c.PROFILE",
            "c.MODEM",
            "c.SRC_NAME",
            "c.SAT_NAME",
            "c.EQUIPMENT",
            "c.DESCRIPTION");

        var chainConditions = conditions.Build();
        var sessionScope = BuildSessionScope(command, query);
        if (sessionScope is not null)
        {
            chainConditions += $""""
                AND EXISTS (
                    SELECT 1
                    FROM msg.SESSIONS AS ss WITH (NOLOCK)
                    WHERE ss.ID_TRANSP = c.ID
                      AND {sessionScope}
                )
                """";
        }

        command.CommandText = SqlConditionBuilder.WithRecompile(
            $""""
             SELECT
                 {SelectColumns}
             FROM main.CHAINS AS c WITH (NOLOCK)
             WHERE {chainConditions}
             ORDER BY c.PROFILE ASC, c.ID ASC
             """");

        var all = new List<ChainRow>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var chain = new ChainRow
                {
                    Id = reader.GetInt32Value(0),
                    Profile = reader.GetStringOrNull(1),
                    Modem = reader.GetStringOrNull(2),
                    Source = reader.GetStringOrNull(3),
                    Satellite = reader.GetStringOrNull(4),
                    Band = reader.GetStringOrNull(5),
                    Polarization = reader.GetStringOrNull(6),
                    Frequency = reader.GetStringOrNull(7),
                    Modulation = reader.GetStringOrNull(8),
                    Equipment = reader.GetStringOrNull(9),
                    DirectionFrom = reader.GetStringOrNull(10),
                    DirectionTo = reader.GetStringOrNull(11),
                    Inserted = reader.GetDateTimeOrNull(12),
                    Updated = reader.GetDateTimeOrNull(13),
                    Description = reader.GetStringOrNull(14),
                    Sessions = reader.GetInt32OrNull(15)
                };

                all.Add(chain);
            }
        }

        return PagedResult<ChainRow>.Create(Slice(all, page), page, all.Count);
    }

    private static List<ChainRow> Slice(List<ChainRow> rows, TablePage page) =>
        page.Skip >= rows.Count ? [] : rows.GetRange(page.Skip, Math.Min(page.Take, rows.Count - page.Skip));

    public async Task<long> CountAsync(ChainsQuery query, CancellationToken cancellationToken = default)
    {
        var result = await LoadPageAsync(query, cancellationToken).ConfigureAwait(false);
        return result.TotalCount;
    }

    public async Task<DateTime?> LoadLatestSessionDayAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText = """"
            SELECT TOP (1) s.DT_START
            FROM msg.SESSIONS AS s WITH (NOLOCK)
            ORDER BY s.ID_S DESC, s.DAY_FGR DESC
            OPTION (RECOMPILE)
            """";
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToDateTime(value).Date;
    }

    private static string? BuildSessionScope(SqlCommand command, ChainsQuery query)
    {
        if (query.FromDate is null && query.ToDate is null)
        {
            return null;
        }

        var conditions = new SqlConditionBuilder();
        if (query.FromDate is { } fromDate)
        {
            conditions.Add("ss.DT_START >= @FromDate");
            command.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = fromDate;
        }

        if (query.ToDate is { } toDate)
        {
            conditions.Add("ss.DT_START <= @ToDate");
            command.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = toDate;
        }

        DayFgrScope.Append(
            command,
            conditions,
            "ss.DAY_FGR",
            "@Day",
            DayFgrScope.Build(query.FromDate?.Date, query.ToDate?.Date));

        return conditions.Build();
    }
}
