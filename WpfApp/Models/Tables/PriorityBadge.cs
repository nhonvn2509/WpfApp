namespace WpfApp.Models.Tables;

public sealed record PriorityBadge(
    string Text,
    string BackgroundBrushKey,
    string ForegroundBrushKey,
    string ToolTip)
{
    public static PriorityBadge None { get; } =
        new("—", "Transparent", "ThirdlyTextBrush", "Phiên không ghi mức ưu tiên")
        {
            HasValue = false
        };

    public bool HasValue { get; init; } = true;

    public static PriorityBadge From(int? priority) => priority switch
    {
        null => None,
        <= 1 => new("1", "PriorityLowBrush", "ThirdlyTextBrush", "Ưu tiên 1 — thấp nhất"),
        2 => new("2", "PriorityNormalBrush", "PrimaryBrush", "Ưu tiên 2 — thấp"),
        3 => new("3", "PriorityGoodBrush", "SuccessBrush", "Ưu tiên 3 — trung bình"),
        4 => new("4", "PriorityHighBrush", "ExploitBrush", "Ưu tiên 4 — cao"),
        5 => new("5", "PriorityTopBrush", "DangerBrush", "Ưu tiên 5 — cao nhất"),
        var value => new(
            value.Value.ToString("N0"),
            "PriorityTopBrush",
            "DangerBrush",
            $"Ưu tiên {value.Value:N0}")
    };
}
