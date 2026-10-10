using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
namespace WpfApp.Views
{
    public partial class LoadingWindow : Window
    {
        public LoadingWindow()
        {
            InitializeComponent();
            VersionText.Text = $"Phiên bản {GetType().Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";
        }
        public void UpdateStatus(string message)
        {
            if (Dispatcher.CheckAccess())
            {
                StatusText.Text = message;
                return;
            }
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = message;
            });
        }
    }
}
