using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfApp.Presentation;

public sealed class ResourceKeyToBrushConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string key && !string.IsNullOrWhiteSpace(key)
            ? Application.Current?.TryFindResource(key) as Brush
            : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("Chỉ dùng chiều dữ liệu đi vào.");
}
