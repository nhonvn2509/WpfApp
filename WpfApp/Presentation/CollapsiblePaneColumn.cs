using System.Windows;
using System.Windows.Controls;

namespace WpfApp.Presentation;

public sealed class CollapsiblePaneColumn
{
    private readonly ColumnDefinition _column;

    private readonly double _expandedMinWidth;

    private GridLength _expandedWidth;

    public CollapsiblePaneColumn(ColumnDefinition column)
    {
        ArgumentNullException.ThrowIfNull(column);

        _column = column;
        _expandedMinWidth = column.MinWidth;

        _expandedWidth = column.Width;
    }

    public GridLength ExpandedWidth => _expandedWidth;

    public bool IsOpen { get; private set; } = true;

    public void SetOpen(bool isOpen)
    {
        if (IsOpen == isOpen)
        {
            return;
        }

        IsOpen = isOpen;
        if (isOpen)
        {
            _column.MinWidth = _expandedMinWidth;
            _column.Width = _expandedWidth;
            return;
        }

        if (_column.Width.IsAbsolute && _column.Width.Value > 0)
        {
            _expandedWidth = _column.Width;
        }

        _column.MinWidth = 0;
        _column.Width = new GridLength(0);
    }
}
