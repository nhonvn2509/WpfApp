namespace WpfApp.Models.Files;

public sealed record FileExtensionStatistics(
    IReadOnlyList<FileExtensionCount> Counts,
    long TotalCount)
{
    public static FileExtensionStatistics Empty { get; } = new([], 0);
}
