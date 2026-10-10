namespace WpfApp.Models.Database;

public sealed record TableStorageInsight(string Table, long Rows, long ReservedMegabytes);

public sealed record IndexInsight(
    string Table,
    string Index,
    string Type,
    long Seeks,
    long Scans,
    DateTime? LastSeek);

public sealed record SlowQueryInsight(
    string SqlText,
    long TotalMilliseconds,
    long Executions,
    double AverageMilliseconds);
