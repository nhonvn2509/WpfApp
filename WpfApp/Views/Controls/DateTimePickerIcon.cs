using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Xceed.Wpf.Toolkit;

namespace WpfApp.Views.Controls;

public static class DateTimePickerIcon
{
    private const string ToggleButtonName = "_calendarToggleButton";
    private const string TemplateKey = "DateTimePickerToggleIconTemplate";

    public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
        "Enable",
        typeof(bool),
        typeof(DateTimePickerIcon),
        new PropertyMetadata(false, OnEnableChanged));

    public static void SetEnable(DependencyObject element, bool value) =>
        element.SetValue(EnableProperty, value);

    public static bool GetEnable(DependencyObject element) => (bool)element.GetValue(EnableProperty);

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DateTimePicker picker)
        {
            return;
        }

        picker.Loaded -= Picker_Loaded;
        if (e.NewValue is true)
        {
            picker.Loaded += Picker_Loaded;
        }
    }

    private static void Picker_Loaded(object sender, RoutedEventArgs e)
    {
        if (Application.Current?.TryFindResource(TemplateKey) is not ControlTemplate template ||
            sender is not DateTimePicker picker)
        {
            return;
        }

        foreach (var button in FindToggleButtons(picker))
        {
            button.Template = template;
        }
    }

    private static IEnumerable<ToggleButton> FindToggleButtons(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is ToggleButton { Name: ToggleButtonName } button)
            {
                yield return button;
            }

            foreach (var nested in FindToggleButtons(child))
            {
                yield return nested;
            }
        }
    }
}
