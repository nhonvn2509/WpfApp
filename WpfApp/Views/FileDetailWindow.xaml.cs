using System.Windows;
using WpfApp.ViewModel;

namespace WpfApp.Views
{
    public partial class FileDetailWindow : Window
    {
        public FileDetailWindow() => InitializeComponent();

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PropertyDetailViewModel viewModel)
            {
                return;
            }

            try
            {
                Clipboard.SetText(viewModel.BuildText());
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"Không chép được vào clipboard.\n\n{ex.Message}",
                    "DeltaBat - Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
