using System.Data;
using Microsoft.Data.SqlClient;
using WpfApp.Models.Tables;
using WpfApp.Services.Data;

namespace WpfApp.Services.Tables;

public sealed class ContactsLoadService : SqlTableLoadService, IContactsLoadService
{
    private const int CommandTimeoutSeconds = 60;

    public ContactsLoadService(
        ISqlConnectionFactory connectionFactory,
        ITableRowCountProvider rowCountProvider)
        : base(connectionFactory)
    {
        _rowCountProvider = rowCountProvider;
    }

    private readonly ITableRowCountProvider _rowCountProvider;

    public async Task<PagedResult<ContactRow>> LoadPageAsync(
        ContactsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = query.Page;
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.Parameters.Add("@Fetch", SqlDbType.Int).Value = page.Take + 1;
        command.Parameters.Add("@Offset", SqlDbType.Int).Value = page.Skip;

        var where = new SqlConditionBuilder();
        SqlConditionBuilder.AppendLike(command, where, "@Search", query.Search, "c.NAME", "c.VAL", "c.REM");
        if (query.SessionId is { } sessionId)
        {
            where.Add("c.ID_S = @SessionId");
            command.Parameters.Add("@SessionId", SqlDbType.BigInt).Value = sessionId;
        }

        command.CommandText = SqlConditionBuilder.WithRecompile(
            $""""
             SELECT
                 c.ID_C, c.ID_S, c.NAME, c.DIR, c.VAL, c.REM, c.PRIOR, c.DT_START, t.NAME
             FROM msg.CONTACTS AS c WITH (NOLOCK)
             LEFT JOIN info.TYPES_CONTACT AS t ON t.ID = c.ID_TYPE
             WHERE {where.Build()}
             ORDER BY c.ID_C DESC
             OFFSET @Offset ROWS FETCH NEXT @Fetch ROWS ONLY
             """");

        var rows = new List<ContactRow>(page.Take);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ContactRow
            {
                IdC = reader.GetInt64Value(0),
                IdS = reader.GetInt64Value(1),
                Name = reader.GetStringOrNull(2),
                Direction = reader.GetStringOrNull(3),
                Value = reader.GetStringOrNull(4),
                Remark = reader.GetStringOrNull(5),
                Priority = reader.GetInt32OrNull(6),
                Start = reader.GetDateTimeOrNull(7),
                Type = reader.GetStringOrNull(8)
            });
        }

        var hasMoreRows = rows.Count > page.Take;
        if (hasMoreRows)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return PagedResult<ContactRow>.CreateUnknownTotal(rows, page, hasMoreRows);
    }

    public async Task<long> CountAsync(ContactsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.IsUnfiltered)
        {
            var whole = await _rowCountProvider.GetRowCountAsync("msg.CONTACTS", cancellationToken)
                .ConfigureAwait(false);
            if (whole >= 0)
            {
                return whole;
            }
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        var where = new SqlConditionBuilder();
        SqlConditionBuilder.AppendLike(command, where, "@Search", query.Search, "c.NAME", "c.VAL", "c.REM");
        if (query.SessionId is { } sessionId)
        {
            where.Add("c.ID_S = @SessionId");
            command.Parameters.Add("@SessionId", SqlDbType.BigInt).Value = sessionId;
        }

        command.CommandText = SqlConditionBuilder.WithRecompile(
            $""""
             SELECT COUNT_BIG(*)
             FROM msg.CONTACTS AS c WITH (NOLOCK)
             WHERE {where.Build()}
             """");
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is null or DBNull ? -1 : Convert.ToInt64(result);
    }
}
