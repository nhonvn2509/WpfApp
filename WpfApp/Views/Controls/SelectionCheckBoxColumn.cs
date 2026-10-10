using System.Windows;
using System.Windows.Controls;

namespace WpfApp.Views.Controls;

public static class SelectionCheckBoxColumn
{
    private const string CheckBoxTemplateKey = "RowSelectionCheckBox";

    public static readonly DependencyProperty VisibleProperty = DependencyProperty.RegisterAttached(
        "Visible",
        typeof(bool),
        typeof(SelectionCheckBoxColumn),
        new PropertyMetadata(true, OnVisibleChanged));

    public static void SetVisible(DependencyObject element, bool value) =>
        element.SetValue(VisibleProperty, value);

    public static bool GetVisible(DependencyObject element) => (bool)element.GetValue(VisibleProperty);

    private static void OnVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid)
        {
            return;
        }

        grid.Loaded -= Grid_Loaded;
        grid.DataContextChanged -= Grid_DataContextChanged;
        grid.Loaded += Grid_Loaded;
        grid.DataContextChanged += Grid_DataContextChanged;
        Apply(grid, e.NewValue is true);
    }

    private static void Grid_Loaded(object sender, RoutedEventArgs e) =>
        Apply((DataGrid)sender, GetVisible((DataGrid)sender));

    private static void Grid_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        Apply((DataGrid)sender, GetVisible((DataGrid)sender));

    private static void Apply(DataGrid grid, bool visible)
    {
        if (Application.Current?.TryFindResource(CheckBoxTemplateKey) is not DataTemplate checkBox)
        {
            return;
        }

        foreach (var column in grid.Columns)
        {
            if (column is DataGridTemplateColumn { CellTemplate: not null } template &&
                ReferenceEquals(template.CellTemplate, checkBox))
            {
                column.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
