using System;
using MahApps.Metro.IconPacks;
using WpfApp.Presentation;

namespace WpfApp.Models.Tables;

public sealed class FileRow : TableRow
{
    public override string SelectionKey => $"{DayFgr}:{IdF}";

    public PackIconLucideKind IconKind => IconCatalog.File(Extension, null);

    public long IdF { get; set; }
    public long IdS { get; set; }
    public byte DayFgr { get; set; }
    public string? Name { get; set; }
    public string? Extension { get; set; }
    public long? Length { get; set; }
    public string? Status { get; set; }
    public int? Quality { get; set; }
    public DateTime? Start { get; set; }
    public string? Path { get; set; }
    public bool? Exists { get; set; }

    public string FullName =>
        string.IsNullOrWhiteSpace(Name)
            ? string.Empty
            : string.IsNullOrWhiteSpace(Extension)
                ? Name.Trim()
                : $"{Name.Trim()}.{Extension.Trim().TrimStart('.')}";
}
