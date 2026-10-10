using System;
using MahApps.Metro.IconPacks;
using WpfApp.Presentation;

namespace WpfApp.Models.Tables;

public sealed class ChainRow : TableRow
{
    public override string SelectionKey => Id.ToString();

    public PackIconLucideKind ChainIconKind => IconCatalog.Chain;

    public int Id { get; set; }
    public string? Profile { get; set; }
    public string? Modem { get; set; }
    public string? Source { get; set; }
    public string? Satellite { get; set; }
    public string? Band { get; set; }
    public string? Polarization { get; set; }
    public string? Frequency { get; set; }
    public string? Modulation { get; set; }
    public string? Equipment { get; set; }
    public string? DirectionFrom { get; set; }
    public string? DirectionTo { get; set; }
    public int? Sessions { get; set; }
    public string? Description { get; set; }
    public DateTime? Inserted { get; set; }
    public DateTime? Updated { get; set; }
}
