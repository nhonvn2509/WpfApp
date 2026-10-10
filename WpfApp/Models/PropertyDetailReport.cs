namespace WpfApp.Models;

public sealed record PropertyDetailReport(
    string Title,
    string? Subtitle,
    IReadOnlyList<PropertyDetailGroup> Groups);
