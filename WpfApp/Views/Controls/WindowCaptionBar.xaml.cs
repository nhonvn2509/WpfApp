using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp.Views.Controls
{
    public partial class WindowCaptionBar : UserControl
    {
        private const double MaximizedInset = 8d;

        private bool _showMinimizeMaximize = true;

        public WindowCaptionBar()
        {
            InitializeComponent();
            Loaded += CaptionBar_Loaded;
            Unloaded += CaptionBar_Unloaded;
        }

        public bool ShowMinimizeMaximize
        {
            get => _showMinimizeMaximize;
            set
            {
                if (_showMinimizeMaximize == value)
                {
                    return;
                }

                _showMinimizeMaximize = value;
                var visibility = value ? Visibility.Visible : Visibility.Collapsed;
                MinimizeButton.Visibility = visibility;
                MaximizeButton.Visibility = visibility;
            }
        }

        private static Window? Owner(WindowCaptionBar bar) => Window.GetWindow(bar);

        private void CaptionBar_Loaded(object sender, RoutedEventArgs e)
        {
            if (Owner(this) is not { } window)
            {
                return;
            }

            window.StateChanged += Window_StateChanged;
            SyncMaximizeGlyph(window);
        }

        private void CaptionBar_Unloaded(object sender, RoutedEventArgs e)
        {
            if (Owner(this) is { } window)
            {
                window.StateChanged -= Window_StateChanged;
            }
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (sender is Window window)
            {
                SyncMaximizeGlyph(window);
            }
        }

        private void SyncMaximizeGlyph(Window window)
        {
            var maximized = window.WindowState == WindowState.Maximized;
            MaximizeGlyph.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
            RestoreGlyph.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
            MaximizeButton.ToolTip = maximized ? "Khôi phục" : "Phóng to";

            if (window.Content is FrameworkElement root)
            {
                root.Margin = maximized
                    ? new Thickness(MaximizedInset)
                    : new Thickness(0);
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (Owner(this) is { } window)
            {
                window.WindowState = WindowState.Minimized;
            }
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (Owner(this) is not { } window)
            {
                return;
            }

            window.WindowState = window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Owner(this)?.Close();
    }
}
