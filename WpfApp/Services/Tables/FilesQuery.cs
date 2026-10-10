namespace WpfApp.Services.Tables;

public sealed record FilesQuery(
    TablePage Page,
    string? Search = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,

    long? SessionId = null,
    IReadOnlyList<int>? ChainIds = null,
    IReadOnlyList<string>? Extensions = null,
    string? KeysetCursorJson = null)
{
    public IReadOnlyList<int> EffectiveChainIds => ChainIds ?? [];
    public IReadOnlyList<string> EffectiveExtensions => Extensions ?? [];

    public bool IsUnfiltered =>
        string.IsNullOrWhiteSpace(Search)
        && FromDate is null
        && ToDate is null
        && SessionId is null
        && EffectiveChainIds.Count == 0
        && EffectiveExtensions.Count == 0;
    public string Describe()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            parts.Add($"từ khóa \"{Search.Trim()}\"");
        }
        if (FromDate is { } fromDate)
        {
            parts.Add($"từ ngày {fromDate:dd/MM/yyyy}");
        }
        if (ToDate is { } toDate)
        {
            parts.Add($"đến ngày {toDate:dd/MM/yyyy}");
        }
        if (SessionId is { } sessionId)
        {
            parts.Add($"phiên {sessionId}");
        }
        if (EffectiveChainIds is { Count: > 0 } chainIds)
        {
            parts.Add(chainIds.Count == 1
                ? $"nguồn {chainIds[0]}"
                : $"{chainIds.Count} nguồn");
        }
        if (EffectiveExtensions is { Count: > 0 } extensions)
        {
            var extensionText = string.Join(", ", extensions.Select(extension => $".{extension.TrimStart('.')}"));
            parts.Add($"đuôi {extensionText}");
        }
        return parts.Count == 0 ? "không lọc (toàn bộ bảng Tệp tin)" : string.Join(", ", parts);
    }
}
