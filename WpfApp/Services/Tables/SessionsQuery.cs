namespace WpfApp.Services.Tables;

public sealed record SessionsQuery(
    TablePage Page,
    string? Search = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int? StatusId = null,
    IReadOnlyList<int>? ChainIds = null,

    string? KeysetCursorJson = null,
    IReadOnlyList<string>? Extensions = null)
{
    public IReadOnlyList<int> EffectiveChainIds => ChainIds ?? [];
    public IReadOnlyList<string> EffectiveExtensions => Extensions ?? [];

    public bool IsUnfiltered =>
        string.IsNullOrWhiteSpace(Search)
        && FromDate is null
        && ToDate is null
        && StatusId is null
        && EffectiveChainIds.Count == 0
        && EffectiveExtensions.Count == 0;
}
