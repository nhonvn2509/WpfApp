using WpfApp.Models;
using WpfApp.Models.Files;
using WpfApp.Models.Tables;
using WpfApp.ViewModel;
namespace WpfApp.Services;

public interface IDialogService
{
    void ShowSettings();

    void ShowSaveSessions(SaveSessionsViewModel viewModel);

    void ShowPropertyDetail(PropertyDetailReport report);

    void ShowFilePreview(FileRow row);

    string? AskExportWordPath(string suggestedFileName);

    string? AskFolder(string title, string? initialDirectory = null);
    void OpenWithShell(string path);
    void CopyToClipboard(string text);
    void ShowInformation(string message, string title = "DeltaBat");
    void ShowError(string message, string title = "DeltaBat - Lỗi");
    bool Confirm(string message, string title = "DeltaBat - Xác nhận");
}
