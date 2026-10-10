using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfApp.Presentation;

namespace WpfApp.Views.Controls;

public static class TableViewBestFit
{
    public static void ApplyToNearestTable(DependencyObject element)
    {
        for (var current = VisualTreeHelper.GetParent(element); current is not null;)
        {
            if (current is UserControl && FindDescendant<DataGrid>(current) is { } grid)
            {
                DataGridBestFit.Apply(grid);
                return;
            }

            current = VisualTreeHelper.GetParent(current);
        }
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }
}
