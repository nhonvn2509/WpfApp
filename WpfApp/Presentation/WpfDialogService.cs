using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using WpfApp.Models;
using WpfApp.Models.Tables;
using WpfApp.Services;
using WpfApp.Services.Files;
using WpfApp.ViewModel;
using WpfApp.ViewModel.Settings;
using WpfApp.Views;
using WpfApp.Views.Controls;
namespace WpfApp.Presentation;

public sealed class WpfDialogService : IDialogService
{
    private readonly IServiceProvider? _services;
    private readonly Window? _owner;

    public WpfDialogService(IServiceProvider? services = null, Window? owner = null)
    {
        _services = services;
        _owner = owner;
    }

    public void ShowSettings()
    {
        var viewModel = _services?.GetService<SettingsViewModel>();
        if (viewModel is null)
        {
            ShowError("Không dựng được dữ liệu cho cửa sổ Cài đặt.");
            return;
        }

        var window = new SettingsWindow(viewModel);
        if (Owner is { } owner && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }

        window.ShowDialog();
    }

    public void ShowSaveSessions(SaveSessionsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        var window = new SaveSessionsWindow(viewModel);
        var owner = Owner;
        if (owner is not null && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }

        void OnCloseRequested(object? sender, EventArgs e) => window.Close();
        viewModel.CloseRequested += OnCloseRequested;
        window.Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
        window.Show();
        window.Activate();
    }

    public void ShowPropertyDetail(PropertyDetailReport report) =>
        ShowWindow(
            new FileDetailWindow(),
            new PropertyDetailViewModel(report.Title, report.Subtitle, report.Groups));

    public void ShowFilePreview(FileRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var pathResolver = _services?.GetService<IFilePathResolver>();
        if (pathResolver is null)
        {
            ShowError("Không tìm thấy dịch vụ xác định đường dẫn tệp.");
            return;
        }

        if (Owner is not MainWindow mainWindow)
        {
            ShowError("Không tìm thấy khung preview tệp của cửa sổ chính.");
            return;
        }

        mainWindow.ShowFilePreview(row, pathResolver.GetFullPath(row));
    }

    public string? AskExportWordPath(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Xuất file Word",
            FileName = suggestedFileName,
            DefaultExt = ".docx",
            Filter = "Tài liệu Word (*.docx)|*.docx|Tất cả các file (*.*)|*.*",
            AddExtension = true,
            OverwritePrompt = true
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? AskFolder(string title, string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
    }
    public void OpenWithShell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowError($"Không mở được file:\n{path}\n\n{ex.Message}");
        }
    }
    public void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            ShowError($"Không chép được vào clipboard.\n\n{ex.Message}");
        }
    }
    public void ShowInformation(string message, string title = "DeltaBat") =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    public void ShowError(string message, string title = "DeltaBat - Lỗi") =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    public bool Confirm(string message, string title = "DeltaBat - Xác nhận") =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    private Window? Owner => _owner ?? Application.Current?.MainWindow;
    private void ShowWindow(Window window, object viewModel)
    {
        var owner = Owner;
        if (owner is not null && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }
        window.DataContext = viewModel;
        window.Show();
    }
}
