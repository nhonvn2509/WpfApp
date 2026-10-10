using System.Windows.Controls;
namespace WpfApp.Presentation;

public static class DataGridBestFit
{
    private const double ExtraPadding = 6d;
    private const double MinColumnWidth = 40d;
    public static void Apply(DataGrid? grid)
    {
        if (grid is null || grid.Columns.Count == 0)
        {
            return;
        }
        var rowVirtualization = grid.EnableRowVirtualization;
        var columnVirtualization = grid.EnableColumnVirtualization;
        grid.EnableRowVirtualization = false;
        grid.EnableColumnVirtualization = false;
        try
        {
            grid.UpdateLayout();
            var widths = new List<DataGridLength>(grid.Columns.Count);
            foreach (var column in grid.Columns)
            {
                widths.Add(MeasureColumn(grid, column));
            }
            for (var index = 0; index < grid.Columns.Count; index++)
            {
                grid.Columns[index].Width = widths[index];
            }
        }
        finally
        {
            grid.EnableRowVirtualization = rowVirtualization;
            grid.EnableColumnVirtualization = columnVirtualization;
            grid.UpdateLayout();
        }
    }
    private static DataGridLength MeasureColumn(DataGrid grid, DataGridColumn column)
    {
        column.Width = DataGridLength.SizeToHeader;
        grid.UpdateLayout();
        var headerWidth = column.ActualWidth;
        column.Width = DataGridLength.SizeToCells;
        grid.UpdateLayout();
        var cellsWidth = column.ActualWidth;
        var width = Math.Max(headerWidth, cellsWidth) + ExtraPadding;
        width = Math.Max(width, MinColumnWidth);
        if (!double.IsInfinity(column.MaxWidth) && width > column.MaxWidth)
        {
            width = column.MaxWidth;
        }
        return new DataGridLength(width);
    }
}
