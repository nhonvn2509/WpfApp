using System.Data;
using Microsoft.Data.SqlClient;
using WpfApp.Models.Files;
using WpfApp.Models.Tables;
using WpfApp.Services.Data;

namespace WpfApp.Services.Files;

public sealed class FileDetailRepository : IFileDetailRepository
{
    private const int CommandTimeoutSeconds = 60;
    private readonly ISqlConnectionFactory _connectionFactory;

    public FileDetailRepository(ISqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async Task<FileDetailRecord?> GetAsync(
        FileRow row,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await QueryAsync(connection, row.IdF, row.IdS, cancellationToken).ConfigureAwait(false)
            ?? await QueryAsync(connection, row.IdF, null, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<FileDetailRecord?> QueryAsync(
        SqlConnection connection,
        long idFile,
        long? idSession,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText = """"
            SELECT TOP (1)
                f.ID_F, f.ID_S, f.DAY_FGR, f.DT_START, f.FN, f.FP,
                f.PATH_NAME, f.SHORT_NAME, f.EXT, f.LEN, f.PRIOR, f.USER_SEL,
                f.ID_STATUS, f.ID_TYPE, f.ID_SAVE, f.ID_LANG, f.QUALITY, f.ID_STOR,
                f.IS_EXISTS, f.MD5, f.VAL_XML,
                ts.NAME AS STATUS_NAME, tf.NAME AS TYPE_NAME, s.NAME AS SESSION_NAME
            FROM msg.FILES AS f
            LEFT JOIN info.TYPES_STATUS AS ts ON ts.ID = f.ID_STATUS
            LEFT JOIN info.TYPES_FILE AS tf ON tf.ID = f.ID_TYPE
            LEFT JOIN msg.SESSIONS AS s ON s.ID_S = f.ID_S AND s.DAY_FGR = f.DAY_FGR
            WHERE f.ID_F = @IdF
              AND (@IdS IS NULL OR f.ID_S = @IdS)
            ORDER BY f.DAY_FGR DESC;
            """";
        command.Parameters.Add("@IdF", SqlDbType.BigInt).Value = idFile;
        command.Parameters.Add("@IdS", SqlDbType.BigInt).Value = idSession ?? (object)DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    private static FileDetailRecord Read(SqlDataReader reader) => new(
        GetValue<long>(reader, 0),
        GetValue<long>(reader, 1),
        GetValue<byte>(reader, 2),
        GetNullable<DateTime>(reader, 3),
        GetNullable<int>(reader, 4),
        GetNullable<int>(reader, 5),
        GetNullableString(reader, 6),
        GetNullableString(reader, 7),
        GetNullableString(reader, 8),
        GetNullable<long>(reader, 9),
        GetNullable<int>(reader, 10),
        GetNullable<byte>(reader, 11),
        GetNullable<int>(reader, 12),
        GetNullable<int>(reader, 13),
        GetNullable<int>(reader, 14),
        GetNullable<int>(reader, 15),
        GetNullable<int>(reader, 16),
        GetNullable<short>(reader, 17),
        GetNullable<bool>(reader, 18),
        GetNullable<Guid>(reader, 19),
        GetNullableString(reader, 20),
        GetNullableString(reader, 21),
        GetNullableString(reader, 22),
        GetNullableString(reader, 23));

    private static T GetValue<T>(SqlDataReader reader, int ordinal) =>
        (T)reader.GetValue(ordinal);

    private static T? GetNullable<T>(SqlDataReader reader, int ordinal) where T : struct =>
        reader.IsDBNull(ordinal) ? null : (T)reader.GetValue(ordinal);

    private static string? GetNullableString(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
