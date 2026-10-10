using System.Data.Common;
using System.Globalization;

namespace WpfApp.Services.Data;

internal static class SqlReaderExtensions
{
    public static string? GetStringOrNull(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    public static long GetInt64Value(this DbDataReader reader, int ordinal) =>
        Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    public static long? GetInt64OrNull(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    public static int GetInt32Value(this DbDataReader reader, int ordinal) =>
        Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    public static int? GetInt32OrNull(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    public static short? GetInt16OrNull(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    public static byte GetByteValue(this DbDataReader reader, int ordinal) =>
        Convert.ToByte(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    public static byte? GetByteOrNull(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToByte(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    public static bool? GetBooleanOrNull(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    public static DateTime? GetDateTimeOrNull(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    public static Guid? GetGuidOrNull(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : (Guid)reader.GetValue(ordinal);
}
