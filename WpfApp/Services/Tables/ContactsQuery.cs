namespace WpfApp.Services.Tables;

public sealed record ContactsQuery(
    TablePage Page,
    string? Search = null,
    long? SessionId = null)
{
    public bool IsUnfiltered => string.IsNullOrWhiteSpace(Search) && SessionId is null;
}
