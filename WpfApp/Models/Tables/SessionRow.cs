using System;
using MahApps.Metro.IconPacks;
using WpfApp.Presentation;

namespace WpfApp.Models.Tables;

public sealed class SessionRow : TableRow
{
    public override string SelectionKey => IdS.ToString();

    public PackIconLucideKind SessionIconKind => IconCatalog.Session;

    public PackIconLucideKind ChainIconKind => IconCatalog.Chain;

    public PackIconLucideKind StatusIconKind => IconCatalog.Status(StatusName);

    public string StatusBrushKey => IconCatalog.StatusBrushKey(StatusName);

    public PackIconLucideKind TypeIconKind => IconCatalog.DataType(TypeName);

    private PriorityBadge? _priorityBadge;

    public PriorityBadge PriorityBadge => _priorityBadge ??= PriorityBadge.From(Priority);

    private int? _priority;

    public int? Priority
    {
        get => _priority;
        set
        {
            _priority = value;
            _priorityBadge = null;
        }
    }

    private int? _quality;
    private IReadOnlyList<QualityStar>? _qualityStars;

    public int? Quality
    {
        get => _quality;
        set
        {
            _quality = value;
            _qualityStars = null;
        }
    }

    public IReadOnlyList<QualityStar> QualityStars => _qualityStars ??= BuildQualityStars();

    public string QualityStarsText => Quality is { } value
        ? $"Chất lượng {value}/{QualityStar.Count}"
        : "Không ghi chất lượng";

    private IReadOnlyList<QualityStar> BuildQualityStars()
    {
        var filled = Math.Clamp(Quality ?? 0, 0, QualityStar.Count);
        return Enumerable.Range(1, QualityStar.Count)
            .Select(index => new QualityStar(index <= filled))
            .ToArray();
    }

    public long IdS { get; set; }
    public byte DayFgr { get; set; }
    public string? Name { get; set; }
    public DateTime Start { get; set; }
    public DateTime Final { get; set; }
    public string? Duration { get; set; }
    public string? StatusName { get; set; }
    public string? TypeName { get; set; }
    public string? ChainName { get; set; }
    public string? IpFrom { get; set; }
    public string? IpTo { get; set; }
    public string? PhoneFrom { get; set; }
    public string? PhoneTo { get; set; }
    public int? ChainId { get; set; }
    public int? Files { get; set; }
    public string? Memo { get; set; }
    public string? ValXml { get; set; }
}
