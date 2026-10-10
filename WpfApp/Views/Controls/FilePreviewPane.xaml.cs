using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfApp.Models.Tables;

namespace WpfApp.Views.Controls;

public partial class FilePreviewPane : UserControl
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".gif", ".ico", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".wdp"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".aac", ".aif", ".aiff", ".flac", ".m4a", ".m4b", ".mp3", ".oga", ".ogg", ".opus", ".wav", ".wma"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".3gp", ".avi", ".m2ts", ".m4v", ".mkv", ".mov", ".mp4", ".mpeg", ".mpg", ".mts", ".wmv"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bat", ".c", ".cfg", ".conf", ".cpp", ".cs", ".css", ".csv", ".go", ".h", ".htm", ".html",
        ".ini", ".java", ".js", ".json", ".log", ".md", ".ps1", ".py", ".sql", ".ts", ".txt", ".xml",
        ".yaml", ".yml"
    };

    private const int MaximumTextCharacters = 1_000_000;
    private readonly DispatcherTimer _positionTimer;
    private string? _currentPath;
    private string? _currentSelectionKey;
    private FileRow? _currentRow;
    private bool _mediaOpened;
    private bool _isUpdatingPosition;
    private bool _imageFillMode;

    public FilePreviewPane()
    {
        InitializeComponent();
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _positionTimer.Tick += PositionTimer_Tick;
        ClearPreview();
    }

    public void ClearPreview()
    {
        StopMedia();
        _currentPath = null;
        _currentSelectionKey = null;
        _currentRow = null;
        _imageFillMode = false;
        ImagePreview.Stretch = System.Windows.Media.Stretch.Uniform;
        FileNameText.Text = "Xem trước tệp";
        FilePathText.Text = "Chọn một phiên hoặc tệp để xem nội dung tại đây.";
        StatusText.Text = "Khung preview luôn được giữ cố định ở dưới.";
        UnknownTitle.Text = "Chưa chọn tệp";
        UnknownDescription.Text = "Nội dung xem trước sẽ hiển thị tại đây khi bạn chọn một tệp.";
        FileDetailsText.Text = string.Empty;
        AudioArtwork.Visibility = Visibility.Collapsed;
        UpdateFileActionButtons();
        SetPreviewVisibility(PreviewKind.Unknown);
    }

    public void ShowPreview(FileRow row, string? path)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (_currentSelectionKey == row.SelectionKey && _currentPath == path)
        {
            return;
        }

        StopMedia();
        _currentPath = path;
        _currentSelectionKey = row.SelectionKey;
        _currentRow = row;
        FileNameText.Text = string.IsNullOrWhiteSpace(row.FullName) ? "(không có tên)" : row.FullName;
        FilePathText.Text = path ?? "Không xác định được đường dẫn tệp.";
        StatusText.Text = string.Empty;
        AudioArtwork.Visibility = Visibility.Collapsed;
        UpdateFileActionButtons();
        SetPreviewVisibility(PreviewKind.None);

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ShowUnknown(
                "Không tìm thấy tệp",
                "Đường dẫn tệp không tồn tại hoặc chưa được cấu hình. Hãy kiểm tra thư mục gốc trong Cài đặt.",
                null);
            return;
        }

        try
        {
            var fileInfo = new FileInfo(path);
            var extension = Path.GetExtension(path);
            var typeLabel = extension.Length > 0 ? extension.ToUpperInvariant() : "không rõ";
            var details = $"Loại: {typeLabel}"
                + $" · Kích thước: {FormatFileSize(fileInfo.Length)}"
                + $" · Cập nhật: {fileInfo.LastWriteTime:dd/MM/yyyy HH:mm:ss}";

            if (ImageExtensions.Contains(extension))
            {
                ShowImage(path);
                StatusText.Text = "Ảnh được hiển thị trực tiếp trong ứng dụng.";
            }
            else if (AudioExtensions.Contains(extension) || VideoExtensions.Contains(extension))
            {
                ShowMedia(path, details, AudioExtensions.Contains(extension));
            }
            else if (TextExtensions.Contains(extension) || IsLikelyText(path))
            {
                ShowText(path, fileInfo.Length, details);
            }
            else
            {
                var isPdf = extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
                ShowUnknown(
                    isPdf ? "Tài liệu PDF" : "Không hỗ trợ xem trước định dạng này",
                    isPdf
                        ? "Ứng dụng chưa tích hợp bộ đọc PDF. Bạn có thể mở tệp bằng ứng dụng PDF mặc định."
                        : "Ứng dụng chưa có bộ xem trước cho định dạng này. Bạn có thể mở tệp bằng ứng dụng mặc định.",
                    details);
            }
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or NotSupportedException
                                          or ArgumentException
                                          or FormatException)
        {
            ShowUnknown("Không thể xem trước tệp", exception.Message, null);
            StatusText.Text = "Đã xảy ra lỗi khi đọc tệp.";
        }
    }

    private void ShowImage(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 1800;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        ImagePreview.Source = image;
        SetPreviewVisibility(PreviewKind.Image);
    }

    private void ShowText(string path, long length, string details)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[MaximumTextCharacters];
        var count = reader.ReadBlock(buffer, 0, buffer.Length);
        TextPreview.Text = new string(buffer, 0, count);
        SetPreviewVisibility(PreviewKind.Text);
        StatusText.Text = length > MaximumTextCharacters
            ? $"{details} · Đang hiển thị tối đa {MaximumTextCharacters:N0} ký tự đầu tiên."
            : details;
    }

    private void ShowMedia(string path, string details, bool isAudio)
    {
        SetPreviewVisibility(PreviewKind.Media);
        AudioArtwork.Visibility = isAudio ? Visibility.Visible : Visibility.Collapsed;
        AudioFileName.Text = FileNameText.Text;
        StatusText.Text = isAudio
            ? $"{details} · Phát âm thanh bằng bộ giải mã có sẵn trên Windows."
            : $"{details} · Phát video bằng bộ giải mã có sẵn trên Windows.";

        try
        {
            MediaPlayer.Source = new Uri(path, UriKind.Absolute);
            MediaPlayer.Play();
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or NotSupportedException
                                          or ArgumentException)
        {
            ShowUnknown("Không thể phát tệp", exception.Message, details);
        }
    }

    private void ShowUnknown(string title, string description, string? details)
    {
        UnknownTitle.Text = title;
        UnknownDescription.Text = description;
        FileDetailsText.Text = details ?? string.Empty;
        SetPreviewVisibility(PreviewKind.Unknown);
    }

    private void SetPreviewVisibility(PreviewKind kind)
    {
        ImagePreview.Visibility = kind == PreviewKind.Image ? Visibility.Visible : Visibility.Collapsed;
        TextPreview.Visibility = kind == PreviewKind.Text ? Visibility.Visible : Visibility.Collapsed;
        MediaPreview.Visibility = kind == PreviewKind.Media ? Visibility.Visible : Visibility.Collapsed;
        UnknownPreview.Visibility = kind == PreviewKind.Unknown ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool IsLikelyText(string path)
    {
        using var stream = File.OpenRead(path);
        var buffer = new byte[4096];
        var count = stream.Read(buffer, 0, buffer.Length);
        if (count == 0)
        {
            return true;
        }

        var controlCharacters = 0;
        for (var index = 0; index < count; index++)
        {
            var value = buffer[index];
            if (value == 0)
            {
                return false;
            }

            if (value < 0x20 && value is not (0x09 or 0x0A or 0x0C or 0x0D))
            {
                controlCharacters++;
            }
        }

        return controlCharacters < count / 20;
    }

    private static string FormatFileSize(long length)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)length;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    private void StopMedia()
    {
        _positionTimer.Stop();
        _mediaOpened = false;
        MediaPlayer.Stop();
        MediaPlayer.Source = null;
        PositionSlider.Value = 0;
        PositionSlider.Maximum = 0;
        PlaybackTimeText.Text = "00:00 / 00:00";
        TextPreview.Clear();
        ImagePreview.Source = null;
    }

    private void PositionTimer_Tick(object? sender, EventArgs e)
    {
        if (_mediaOpened && MediaPlayer.NaturalDuration.HasTimeSpan)
        {
            _isUpdatingPosition = true;
            PositionSlider.Maximum = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
            PositionSlider.Value = MediaPlayer.Position.TotalSeconds;
            _isUpdatingPosition = false;
            PlaybackTimeText.Text = $"{MediaPlayer.Position:mm\\:ss} / {MediaPlayer.NaturalDuration.TimeSpan:mm\\:ss}";
        }
    }

    private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        _mediaOpened = true;
        _positionTimer.Start();
        PlayPauseButton.Content = "Tạm dừng";
    }

    private void MediaPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _positionTimer.Stop();
        _mediaOpened = false;
        StatusText.Text = $"Không phát được tệp: {e.ErrorException?.Message ?? "Bộ giải mã Windows không hỗ trợ định dạng này."}";
    }

    private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        MediaPlayer.Stop();
        PlayPauseButton.Content = "Phát";
    }

    private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isUpdatingPosition && _mediaOpened && MediaPlayer.NaturalDuration.HasTimeSpan)
        {
            MediaPlayer.Position = TimeSpan.FromSeconds(PositionSlider.Value);
        }
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (MediaPlayer.Source is null)
        {
            return;
        }

        if (MediaPlayer.CanPause && PlayPauseButton.Content?.ToString() == "Tạm dừng")
        {
            MediaPlayer.Pause();
            PlayPauseButton.Content = "Phát";
        }
        else
        {
            MediaPlayer.Play();
            PlayPauseButton.Content = "Tạm dừng";
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        MediaPlayer.Stop();
        PlayPauseButton.Content = "Phát";
    }

    private void FilePreviewPane_Unloaded(object sender, RoutedEventArgs e)
    {
        _positionTimer.Stop();
        MediaPlayer.Stop();
    }

    private void OpenExternal_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentPath) || !File.Exists(_currentPath))
        {
            StatusText.Text = "Không thể mở tệp vì không tìm thấy đường dẫn.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_currentPath) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidOperationException
                                          or System.ComponentModel.Win32Exception)
        {
            StatusText.Text = $"Không mở được tệp: {exception.Message}";
        }
    }

    private void RefreshPreview_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRow is null)
        {
            StatusText.Text = "Chưa chọn tệp để tải lại preview.";
            return;
        }

        _currentSelectionKey = null;
        ShowPreview(_currentRow, _currentPath);
    }

    private void FitImage_Click(object sender, RoutedEventArgs e)
    {
        _imageFillMode = !_imageFillMode;
        ImagePreview.Stretch = _imageFillMode
            ? System.Windows.Media.Stretch.UniformToFill
            : System.Windows.Media.Stretch.Uniform;
        FitImageButton.ToolTip = _imageFillMode
            ? "Ảnh đang lấp đầy khung; bấm để căn vừa toàn bộ ảnh"
            : "Ảnh đang căn vừa; bấm để lấp đầy khung";
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetExistingPath(out var path))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidOperationException
                                          or System.ComponentModel.Win32Exception)
        {
            StatusText.Text = $"Không mở được thư mục chứa tệp: {exception.Message}";
        }
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentPath))
        {
            StatusText.Text = "Tệp chưa có đường dẫn để sao chép.";
            return;
        }

        try
        {
            Clipboard.SetText(_currentPath);
            StatusText.Text = "Đã sao chép đường dẫn tệp.";
        }
        catch (Exception exception) when (exception is System.Runtime.InteropServices.ExternalException
                                          or InvalidOperationException)
        {
            StatusText.Text = $"Không sao chép được đường dẫn: {exception.Message}";
        }
    }

    private void FileProperties_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetExistingPath(out var path))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true,
                Verb = "properties"
            });
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidOperationException
                                          or System.ComponentModel.Win32Exception)
        {
            StatusText.Text = $"Không mở được thuộc tính tệp: {exception.Message}";
        }
    }

    private void UpdateFileActionButtons()
    {
        var hasPath = !string.IsNullOrWhiteSpace(_currentPath);
        RefreshPreviewButton.IsEnabled = _currentRow is not null;
        OpenFolderButton.IsEnabled = hasPath;
        CopyPathButton.IsEnabled = hasPath;
        FilePropertiesButton.IsEnabled = hasPath;
    }

    private bool TryGetExistingPath(out string path)
    {
        path = _currentPath ?? string.Empty;
        if (path.Length > 0 && File.Exists(path))
        {
            return true;
        }

        StatusText.Text = "Không thể thực hiện thao tác vì không tìm thấy tệp.";
        return false;
    }

    private enum PreviewKind
    {
        None,
        Image,
        Text,
        Media,
        Unknown
    }
}
