using System.Windows;
using WpfApp.ViewModel;

namespace WpfApp.Views.Controls
{
    public sealed partial class SaveSessionsWindow : Window
    {
        public SaveSessionsWindow(SaveSessionsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
