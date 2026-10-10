namespace WpfApp.Models;

public sealed record PropertyDetailGroup(string Name, IReadOnlyList<PropertyDetailItem> Items);
