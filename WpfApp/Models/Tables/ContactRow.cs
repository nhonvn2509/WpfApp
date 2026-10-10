using System;
namespace WpfApp.Models.Tables;

public sealed class ContactRow : TableRow
{
    public override string SelectionKey => IdC.ToString();
    public long IdC { get; set; }
    public long IdS { get; set; }
    public string? Name { get; set; }
    public string? Direction { get; set; }
    public string? Value { get; set; }
    public string? Remark { get; set; }
    public string? Type { get; set; }
    public int? Priority { get; set; }
    public DateTime? Start { get; set; }
}
