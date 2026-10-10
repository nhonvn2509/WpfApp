using System.ComponentModel;
using System.Windows;
using WpfApp.ViewModel.Settings;

namespace WpfApp.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow(SettingsViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;

            viewModel.CloseRequested += (_, _) => Close();
        }

        public SettingsViewModel ViewModel { get; }

        private void SettingsWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (!ViewModel.ConfirmClose())
            {
                e.Cancel = true;
            }
        }
    }
}
