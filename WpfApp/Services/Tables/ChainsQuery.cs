namespace WpfApp.Services.Tables;

public sealed record ChainsQuery(
    TablePage Page,
    string? Search = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null);
