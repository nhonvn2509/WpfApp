using System.Data;
using Microsoft.Data.SqlClient;

namespace WpfApp.Services.Tables;

internal sealed class SqlConditionBuilder
{
    private readonly List<string> _conditions = ["1 = 1"];

    public string Build() => string.Join(Environment.NewLine + "AND ", _conditions);

    public void Add(string condition) => _conditions.Add(condition);

    public void AddDateRange(
        SqlCommand command,
        string column,
        string fromParameter,
        string toParameter,
        DateTime? fromDate,
        DateTime? toDate)
    {
        if (fromDate is { } from)
        {
            Add($"{column} >= {fromParameter}");
            command.Parameters.Add(fromParameter, SqlDbType.DateTime).Value = from.Date;
        }

        if (toDate is { } to)
        {
            Add($"{column} < {toParameter}");
            command.Parameters.Add(toParameter, SqlDbType.DateTime).Value = to.Date.AddDays(1);
        }
    }

    public void AddInList(
        SqlCommand command,
        string column,
        string parameterPrefix,
        IReadOnlyList<int> values,
        SqlDbType parameterType = SqlDbType.Int)
    {
        if (values.Count == 0)
        {
            return;
        }

        var names = new string[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            var name = $"{parameterPrefix}{index}";
            names[index] = name;
            command.Parameters.Add(name, parameterType).Value = values[index];
        }

        Add($"{column} IN ({string.Join(", ", names)})");
    }

    public void AddInList(
        SqlCommand command,
        string column,
        string parameterPrefix,
        IReadOnlyList<string> values,
        int size,
        SqlDbType parameterType = SqlDbType.VarChar)
    {
        if (values.Count == 0)
        {
            return;
        }

        var names = new string[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            var name = $"{parameterPrefix}{index}";
            names[index] = name;
            command.Parameters.Add(name, parameterType, size).Value = values[index];
        }

        Add($"{column} IN ({string.Join(", ", names)})");
    }

    public static string ContainsPattern(string search) =>
        $"%{search.Trim().Replace("!", "!!", StringComparison.Ordinal)
            .Replace("%", "!%", StringComparison.Ordinal)
            .Replace("_", "!_", StringComparison.Ordinal)
            .Replace("[", "![", StringComparison.Ordinal)}%";

    public static void AppendLike(
        SqlCommand command,
        SqlConditionBuilder builder,
        string parameterName,
        string? search,
        params string[] columns)
    {
        if (string.IsNullOrWhiteSpace(search) || columns.Length == 0)
        {
            return;
        }

        command.Parameters.Add(parameterName, SqlDbType.VarChar, 258).Value = ContainsPattern(search);
        builder.Add($"({string.Join(" OR ", columns.Select(column => $"{column} LIKE {parameterName} ESCAPE '!'"))})");
    }

    public static string WithRecompile(string body) => $"{body}{Environment.NewLine}OPTION (RECOMPILE)";
}
