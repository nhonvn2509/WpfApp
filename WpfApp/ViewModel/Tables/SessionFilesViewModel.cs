using System.Collections.ObjectModel;
using System.IO;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfApp.Enums;
using WpfApp.Models;
using WpfApp.Models.Files;
using WpfApp.Models.Tables;
using WpfApp.Services;
using WpfApp.Services.Files;
using WpfApp.Services.Tables;

namespace WpfApp.ViewModel.Tables;

internal static class SessionFileTreeBuilder
{
    public static IReadOnlyList<SessionFileTreeNode> Build(
        IEnumerable<SessionRow> sessions,
        IEnumerable<SessionFileTreeNode>? reuseLoaded = null)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        var loaded = (reuseLoaded ?? [])
            .Where(node => node.IsSessionRoot && node.SessionId is not null)
            .GroupBy(node => node.SessionId!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        var roots = new List<SessionFileTreeNode>();
        foreach (var session in sessions)
        {
            roots.Add(loaded.TryGetValue(session.IdS, out var existing)
                ? existing
                : SessionFileTreeNode.CreateSessionRoot(session));
        }

        return roots;
    }
}

public sealed partial class SessionFilesViewModel : ObservableObject
{
    public const int FilePageSize = TablePage.MaxPageSize;

    public const int HardLoadLimit = 200_000;

    private readonly IFilesLoadService _loadService;
    private readonly IFileDetailService _detailService;
    private readonly IFileExportService _exportService;
    private readonly IDialogService _dialogService;
    private readonly FileExtensionsViewModel _fileExtensions;
    private readonly SessionsTableViewModel _sessions;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly HashSet<string> _checkedKeys = new(StringComparer.Ordinal);
    private readonly List<SessionFileTreeNode> _contextSelectedNodes = [];

    private List<FileRow> _allFiles = [];
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _sessionOpenCancellation;

    private string? _currentSessionXml;
    private FilesQuery? _currentSearchQuery;

    private string? _currentSessionName;

    public SessionFilesViewModel(
        IFilesLoadService loadService,
        IFileDetailService detailService,
        IFileExportService exportService,
        IDialogService dialogService,
        FileExtensionsViewModel fileExtensions,
        SessionsTableViewModel sessions)
    {
        _loadService = loadService;
        _detailService = detailService;
        _exportService = exportService;
        _dialogService = dialogService;
        _fileExtensions = fileExtensions;
        _sessions = sessions;
        _fileExtensions.PropertyChanged += FileExtensions_PropertyChanged;
        _sessions.PropertyChanged += Sessions_PropertyChanged;
        RefreshSessionTree();
    }

    public ObservableCollection<FileRow> Files { get; } = [];
    public ObservableCollection<SessionFileTreeNode> SessionTree { get; } = [];
    public bool HasSessionTreeData => SessionTree.Count > 0;
    public bool IsViewingSession => _sessionId is not null;

    public ObservableCollection<SessionMetadataSection> MetadataSections { get; } = [];

    [ObservableProperty]
    private int _selectedMetadataTabIndex;

    public IReadOnlyList<PropertyDetailItem> DossierItems { get; private set; } = [];

    public bool HasDossier => DossierItems.Count > 0;

    public bool IsMetadataEmpty => MetadataSections.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedFile))]
    private FileRow? _selectedFile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MetadataEmptyMessage))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportWordCommand))]
    private bool _isExporting;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportWordCommand))]
    [NotifyPropertyChangedFor(nameof(HasCheckedFiles))]
    [NotifyPropertyChangedFor(nameof(CheckedText))]
    private int _checkedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyStateMessage))]
    [NotifyPropertyChangedFor(nameof(MetadataEmptyMessage))]
    private bool _hasSession;

    private const int FileCacheCapacity = 8;

    private readonly Dictionary<string, CachedFileList> _fileCache = [];

    [ObservableProperty]
    private string _sessionTitle = string.Empty;

    [ObservableProperty]
    private string _sessionSubtitle = string.Empty;

    [ObservableProperty]
    private string _statusText = "Chọn một phiên ở bảng Phiên để xem metadata VAL_XML của phiên đó.";

    public string SlowQueryNotice =>
        "Truy vấn tệp giới hạn theo khoảng ngày đang chọn. Khoảng ngày càng rộng thì càng lâu — "
        + "nên thu hẹp lại nếu thấy chậm. Vui lòng chờ đến khi hoàn tất.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyStateMessage))]
    private bool _isTruncated;

    private long? _sessionId;

    public bool HasFiles => Files.Count > 0;

    public long? CurrentSessionId => _sessionId;

    public void ReportLoadError(string action, Exception exception) => ReportError(action, exception);

    public bool IsEmpty => !HasFiles;

    public bool HasSelectedFile => SelectedFile is not null;

    public int ContextSelectionCount => _contextSelectedNodes.Count;

    public bool HasSingleContextSelection => ContextSelectionCount == 1;

    public bool HasMultipleContextSelections => ContextSelectionCount > 1;

    public bool HasCheckedFiles => CheckedCount > 0;

    public string CheckedText => CheckedCount == 0
        ? string.Empty
        : $"Đã tick {CheckedCount:N0} tệp";

    public string EmptyStateMessage => !HasSession
        ? "Chưa chọn phiên nào. Bấm một dòng ở bảng Phiên để xem danh sách tệp của phiên đó."
        : IsLoading
            ? "Đang tải danh sách tệp..."
            : IsTruncated
                ? $"Phiên này có trên {HardLoadLimit:N0} tệp — danh sách đang bị cắt. "
                  + "Tick thêm đuôi file ở khung bên trái để thu hẹp lại."
                : "Phiên này không có tệp nào khớp bộ lọc đuôi file đang chọn.";

    public string MetadataEmptyMessage => !HasSession
        ? "Chưa chọn phiên nào. Bấm một dòng ở bảng Phiên để xem toàn bộ metadata VAL_XML của phiên đó."
        : IsLoading
            ? "Đang tải dữ liệu..."
            : "Phiên này không có VAL_XML nên không có metadata nào để hiện.";

    private async Task ShowSessionMetadataAsync(SessionRow session, CancellationToken cancellationToken)
    {
        string? xml = session.ValXml;
        if (xml is null)
        {
            try
            {
                xml = await _sessions.LoadSessionXmlAsync(session.IdS, cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                ReportError("nạp metadata phiên", exception);
                xml = null;
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        ApplySessionMetadata(xml);
    }

    private void ApplySessionMetadata(string? valXml)
    {
        _currentSessionXml = valXml;

        RefreshSessionTree();

        var selectedSectionTitle = SelectedMetadataTabIndex >= 0 &&
                                   SelectedMetadataTabIndex < MetadataSections.Count
            ? MetadataSections[SelectedMetadataTabIndex].Title
            : null;
        var sections = SessionMetadataParser.Parse(valXml);
        MetadataSections.Clear();
        foreach (var section in sections)
        {
            MetadataSections.Add(section);
        }

        OnPropertyChanged(nameof(MetadataSections));
        SelectMetadataTab(selectedSectionTitle);
        SetDossier(SessionMetadataParser.ParseDossier(valXml));
        OnPropertyChanged(nameof(IsMetadataEmpty));
        OnPropertyChanged(nameof(MetadataEmptyMessage));
        StatusText = sections.Count == 0
            ? "Phiên này không có VAL_XML nên không hiện được metadata."
            : $"VAL_XML: {sections.Count:N0} phần · {valXml!.Length:N0} ký tự.";
    }

    private void SelectMetadataTab(string? preferredTitle)
    {
        if (!string.IsNullOrWhiteSpace(preferredTitle))
        {
            for (var index = 0; index < MetadataSections.Count; index++)
            {
                if (string.Equals(MetadataSections[index].Title, preferredTitle, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedMetadataTabIndex = index;
                    return;
                }
            }
        }

        SelectedMetadataTabIndex = SessionMetadataParser.SelectPreferredTabIndex(MetadataSections);
    }

    private void ClearMetadata()
    {
        MetadataSections.Clear();
        OnPropertyChanged(nameof(MetadataSections));
        SelectedMetadataTabIndex = 0;
        SetDossier([]);
        OnPropertyChanged(nameof(IsMetadataEmpty));
        OnPropertyChanged(nameof(MetadataEmptyMessage));
    }

    private void SetDossier(IReadOnlyList<PropertyDetailItem> items)
    {
        DossierItems = items;
        OnPropertyChanged(nameof(DossierItems));
        OnPropertyChanged(nameof(HasDossier));
    }

    public void SelectFromTree(SessionFileTreeNode? node)
    {
        if (node is null)
        {
            return;
        }

        var row = node.IsSessionRoot
            ? node.Children
                .Select(child => child.ToFileRow())
                .FirstOrDefault(file => file is not null)
            : node.ToFileRow();
        if (row is not null)
        {
            SelectFile(row);
        }
    }

    public void SetContextSelection(IEnumerable<SessionFileTreeNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        foreach (var selectedNode in _contextSelectedNodes)
        {
            selectedNode.IsContextSelected = false;
        }

        _contextSelectedNodes.Clear();
        foreach (var node in nodes.Distinct())
        {
            if (node.IsSessionRoot || node.IsPlaceholder)
            {
                continue;
            }

            node.IsContextSelected = true;
            _contextSelectedNodes.Add(node);
        }

        OnPropertyChanged(nameof(ContextSelectionCount));
        OnPropertyChanged(nameof(HasSingleContextSelection));
        OnPropertyChanged(nameof(HasMultipleContextSelections));
        PreviewContextSelectionCommand.NotifyCanExecuteChanged();
    }

    public void ToggleContextSelection(SessionFileTreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var selection = _contextSelectedNodes.ToList();
        if (!selection.Remove(node))
        {
            selection.Add(node);
        }

        SetContextSelection(selection);
    }

    private bool CanPreviewContextSelection() => HasSingleContextSelection;

    [RelayCommand(CanExecute = nameof(CanPreviewContextSelection))]
    private void PreviewContextSelection()
    {
        if (_contextSelectedNodes is [var node])
        {
            SelectFromTree(node);
        }
    }

    public void SelectFile(FileRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var selected = SessionFileMatcher.Find(Files, row) ?? row;
        if (SelectedFile?.SelectionKey != selected.SelectionKey)
        {
            SelectedFile = selected;
        }

        _dialogService.ShowFilePreview(selected);
    }

    public SessionFileTreeNode? FindTreeNode(FileRow? file)
    {
        if (file is null)
        {
            return null;
        }

        IEnumerable<SessionFileTreeNode> roots = SessionTree;
        if (_sessionId is { } sessionId)
        {
            roots = SessionTree.Where(root => root.SessionId == sessionId);
        }

        var nodes = roots.SelectMany(Flatten).ToList();
        var rows = nodes.Select(node => node.ToFileRow()).OfType<FileRow>().ToList();
        return SessionFileMatcher.Find(rows, file) is { } matched
            ? nodes[rows.IndexOf(matched)]
            : null;
    }

    private static IEnumerable<SessionFileTreeNode> Flatten(SessionFileTreeNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;

            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    public async Task OpenAsync(SessionRow session, CancellationToken cancellationToken = default)
    {
        _sessionOpenCancellation?.Cancel();
        using var sessionOpen = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _sessionOpenCancellation = sessionOpen;
        _loadCancellation?.Cancel();
        SelectedFile = null;
        _allFiles = [];
        ReplaceFiles([]);
        _sessionId = session.IdS;
        _currentSearchQuery = null;
        _currentSessionName = session.Name;
        OnPropertyChanged(nameof(IsViewingSession));
        SessionTitle = string.IsNullOrWhiteSpace(session.Name)
            ? $"Phiên {session.IdS:N0}"
            : session.Name;
        SessionSubtitle = $"#{session.IdS:N0} · bắt đầu {session.Start:dd/MM/yyyy HH:mm}";
        HasSession = true;

        try
        {
            await ShowSessionMetadataAsync(session, sessionOpen.Token).ConfigureAwait(true);
            if (sessionOpen.IsCancellationRequested)
            {
                return;
            }

            await LoadAsync(sessionOpen.Token);
            if (!sessionOpen.IsCancellationRequested &&
                _sessionId == session.IdS &&
                Files.FirstOrDefault() is { } firstFile)
            {
                SelectFile(firstFile);
            }
        }
        finally
        {
            if (ReferenceEquals(_sessionOpenCancellation, sessionOpen))
            {
                _sessionOpenCancellation = null;
            }
        }
    }

    public async Task SearchForChainsAsync(
        IReadOnlyList<int> chainIds,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        var extensions = _fileExtensions.SelectedExtensions;
        if (chainIds.Count == 0 || extensions.Count == 0)
        {
            return;
        }

        _sessionOpenCancellation?.Cancel();
        _loadCancellation?.Cancel();
        _sessionId = null;
        _currentSearchQuery = null;
        _currentSessionXml = null;
        _currentSessionName = null;
        OnPropertyChanged(nameof(IsViewingSession));
        SessionTitle = "Kết quả tìm tệp";
        SessionSubtitle = $"{string.Join(", ", extensions.Select(extension => $".{extension.TrimStart('.')}"))} · {chainIds.Count:N0} nguồn";
        HasSession = true;
        IsTruncated = false;

        ClearMetadata();
        RefreshSessionTree();

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCancellation = linkedCancellation;
        var gateEntered = false;
        try
        {
            await _loadGate.WaitAsync(linkedCancellation.Token);
            gateEntered = true;
            IsLoading = true;
            StatusText = "Đang tìm tệp theo đuôi đã chọn...";
            var query = new FilesQuery(
                TablePage.First(FilePageSize),
                FromDate: fromDate,
                ToDate: toDate,
                ChainIds: chainIds,
                Extensions: extensions);
            _currentSearchQuery = query;
            var (loaded, truncated) = await LoadEveryFileAsync(query, linkedCancellation.Token)
                .ConfigureAwait(true);
            if (linkedCancellation.IsCancellationRequested)
            {
                return;
            }

            _allFiles = loaded;
            IsTruncated = truncated;
            ApplyExtensionFilter();

            RefreshSessionTree();

            StatusText = Files.Count == 0
                ? "Không tìm thấy tệp khớp các đuôi đã chọn."
                : IsTruncated
                    ? $"Có trên {HardLoadLimit:N0} tệp; đang hiện {Files.Count:N0} tệp."
                    : $"Đã tìm thấy {Files.Count:N0} tệp.";
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            StatusText = $"Không tìm được tệp: {exception.Message}";
        }
        finally
        {
            if (gateEntered)
            {
                _loadGate.Release();
            }

            if (ReferenceEquals(_loadCancellation, linkedCancellation))
            {
                IsLoading = false;
                _loadCancellation = null;
            }
        }
    }

    public void Clear()
    {
        _sessionOpenCancellation?.Cancel();
        _loadCancellation?.Cancel();
        _allFiles = [];
        ReplaceFiles([]);
        _sessionId = null;
        _currentSearchQuery = null;
        _currentSessionXml = null;
        _currentSessionName = null;
        OnPropertyChanged(nameof(IsViewingSession));
        SessionTitle = string.Empty;
        SessionSubtitle = string.Empty;
        IsTruncated = false;
        HasSession = false;
        SelectedFile = null;
        ClearMetadata();
        RefreshSessionTree();
        StatusText = "Chọn một phiên ở bảng Phiên để xem metadata VAL_XML của phiên đó.";
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private FilesQuery CreateQuerySnapshot(long sessionId) =>
        new(
            TablePage.First(FilePageSize),
            SessionId: sessionId,
            Extensions: _fileExtensions.SelectedExtensions);

    private FilesQuery? CreateCurrentQuerySnapshot()
    {
        if (_sessionId is { } sessionId)
        {
            return CreateQuerySnapshot(sessionId);
        }

        return _currentSearchQuery is { } query
            ? query with
            {
                Page = TablePage.First(FilePageSize),
                Extensions = _fileExtensions.SelectedExtensions
            }
            : null;
    }

    private string BuildFileCacheKey(long sessionId) =>
        string.Join(
            '|',
            sessionId.ToString(),
            string.Join(',', _fileExtensions.SelectedExtensions.Order(StringComparer.OrdinalIgnoreCase)));

    private sealed record CachedFileList(IReadOnlyList<FileRow> Files, bool Truncated);

    private async Task<(List<FileRow> Files, bool Truncated)> LoadEveryFileAsync(
        FilesQuery query,
        CancellationToken cancellationToken) =>
        await PageAccumulator.LoadAllAsync<FileRow>(
                page => _loadService.LoadPageAsync(query with { Page = page }, cancellationToken),
                FilePageSize,
                HardLoadLimit,
                cancellationToken)
            .ConfigureAwait(true);

    private void StoreFileCache(string key, IReadOnlyList<FileRow> files, bool truncated)
    {
        _fileCache[key] = new CachedFileList(files, truncated);
        while (_fileCache.Count > FileCacheCapacity)
        {
            _fileCache.Remove(_fileCache.Keys.First());
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_sessionId is not { } sessionId)
        {
            return;
        }

        var cacheKey = BuildFileCacheKey(sessionId);
        if (_fileCache.Remove(cacheKey, out var cached))
        {
            _fileCache[cacheKey] = cached;
            IsLoading = false;
            _allFiles = cached.Files.ToList();
            IsTruncated = cached.Truncated;
            ApplyExtensionFilter();
            StatusText = $"Đã tải {Files.Count:N0} tệp (từ bộ nhớ đệm).";
            return;
        }

        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCancellation = cancellation;
        var gateEntered = false;
        try
        {
            await _loadGate.WaitAsync(cancellation.Token);
            gateEntered = true;
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            if (_sessionId != sessionId)
            {
                return;
            }

            IsLoading = true;
            StatusText = "Đang tải toàn bộ danh sách tệp (chậm, xem lưu ý bên dưới)...";
            var (loaded, truncated) = await LoadEveryFileAsync(
                    CreateQuerySnapshot(sessionId),
                    cancellation.Token)
                .ConfigureAwait(true);
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            StoreFileCache(cacheKey, loaded, truncated);
            _allFiles = loaded;
            IsTruncated = truncated;
            ApplyExtensionFilter();
            StatusText = Files.Count == 0
                ? "Phiên này không có tệp nào."
                : IsTruncated
                    ? $"Phiên này có trên {HardLoadLimit:N0} tệp — đang hiện {Files.Count:N0} tệp."
                    : $"Đã tải {Files.Count:N0} tệp. Chọn một tệp để xem trước.";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            StatusText = $"Không tải được danh sách tệp: {exception.Message}";
        }
        finally
        {
            if (gateEntered)
            {
                _loadGate.Release();
            }

            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                IsLoading = false;
                _loadCancellation = null;
            }
        }
    }

    private void ApplyExtensionFilter()
    {
        var selected = _fileExtensions.SelectedExtensions
            .Select(ToBareExtension)
            .Where(extension => extension.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var visible = selected.Count == 0
            ? _allFiles
            : _allFiles.Where(row => selected.Contains(ToBareExtension(row.Extension))).ToList();
        var previous = SelectedFile;
        ReplaceFiles(visible);

        SelectedFile = previous is null ? Files.FirstOrDefault() : SessionFileMatcher.Find(Files, previous) ?? Files.FirstOrDefault();
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    private void FileExtensions_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FileExtensionsViewModel.SelectedExtensions))
        {
            return;
        }

        ApplyExtensionFilter();

        if (_allFiles.Count == 0 && SelectedExtensionsSelected())
        {
            StatusText = "Đã chọn đuôi file. Bấm một dòng ở bảng Phiên để xem tệp thuộc các đuôi đã chọn.";
        }
    }

    private bool SelectedExtensionsSelected() => _fileExtensions.SelectedExtensions.Count > 0;

    private static string ToBareExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var value = extension.Trim();

        if (value.Contains(Path.DirectorySeparatorChar) || value.Contains(Path.AltDirectorySeparatorChar))
        {
            value = Path.GetExtension(value);
        }

        return value.TrimStart('.').ToLowerInvariant();
    }

    private void Sessions_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionsTableViewModel.Items))
        {
            RefreshSessionTree();
        }
    }

    private void RefreshSessionTree()
    {
        var reuseLoaded = SessionTree.ToList();

        SessionTree.Clear();

        if (_sessionId is null && _currentSearchQuery is not null)
        {
            BuildTreeFromFiles();
            NotifyTreeChanged();
            _ = ExpandAndLoadAllTreeAsync();
            return;
        }

        foreach (var node in SessionFileTreeBuilder.Build(_sessions.Items, reuseLoaded))
        {
            SessionTree.Add(node);
        }

        if (SessionTree.Count == 0)
        {
            BuildTreeFromFiles();
        }

        HighlightCurrentSessionNode();
        NotifyTreeChanged();
        _ = ExpandAndLoadAllTreeAsync();
    }

    // ponytail: tuần tự từng root để không bắn song song hàng loạt truy vấn VAL_XML; nếu số phiên lớn hơn một trang thì chuyển sang nạp song song có giới hạn.
    private async Task ExpandAndLoadAllTreeAsync()
    {
        foreach (var node in SessionTree.ToList())
        {
            node.IsExpanded = true;
            await LoadTreeChildrenAsync(node);
        }
    }

    private void NotifyTreeChanged()
    {
        OnPropertyChanged(nameof(HasSessionTreeData));
        OnPropertyChanged(nameof(SessionTree));
    }

    private void HighlightCurrentSessionNode()
    {
        if (_sessionId is not { } sessionId)
        {
            return;
        }

        var node = SessionTree.FirstOrDefault(item => item.SessionId == sessionId);
        if (node is null)
        {
            return;
        }

        node.IsExpanded = true;
        _ = LoadTreeChildrenAsync(node);
    }

    public async Task LoadTreeChildrenAsync(SessionFileTreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (!node.IsSessionRoot || node.SessionId is not { } sessionId)
        {
            return;
        }

        if (node.AreChildrenLoaded || node.IsLoadingChildren)
        {
            return;
        }

        node.IsLoadingChildren = true;
        try
        {
            var xml = sessionId == _sessionId
                ? _currentSessionXml
                : await _sessions.LoadSessionXmlAsync(sessionId).ConfigureAwait(true);

            var fileNodes = SessionFileTreeParser.Parse(xml);
            if (fileNodes.Count > 0)
            {
                node.LoadChildren(fileNodes);
            }
            else
            {
                node.LoadChildren(
                    [SessionFileTreeNode.CreateEmpty("Không có tệp nào trong phiên này")],
                    "Không có tệp nào trong phiên này");
            }
        }
        catch (Exception exception)
        {
            node.LoadChildren(
                [SessionFileTreeNode.CreateEmpty("Không tải được danh sách tệp")],
                $"Lỗi: {exception.Message}");
        }
        finally
        {
            node.IsLoadingChildren = false;
        }
    }

    private void BuildTreeFromFiles()
    {
        if (Files.Count == 0)
        {
            return;
        }

        var nodes = Files
            .Where(row => !string.IsNullOrWhiteSpace(row.Name))
            .Select(row => SessionFileTreeNode.CreateFile(
                row.Name!,
                row.Extension,
                row.Length,
                row.Status,
                row.Path))
            .ToList();

        if (nodes.Count == 0)
        {
            return;
        }

        var rootTitle = IsTruncated
            ? $"Kết quả lọc (còn {nodes.Count:N0}/{HardLoadLimit:N0} tệp)"
            : $"Kết quả lọc ({nodes.Count:N0} tệp)";

        SessionTree.Add(SessionFileTreeNode.CreateRoot(rootTitle, nodes));
    }

    private void ReplaceFiles(IEnumerable<FileRow> rows)
    {
        SetContextSelection([]);
        foreach (var row in Files)
        {
            row.PropertyChanged -= Row_PropertyChanged;
            row.IsSelected = false;
        }

        _checkedKeys.Clear();
        Files.Clear();
        foreach (var row in rows)
        {
            row.PropertyChanged += Row_PropertyChanged;
            Files.Add(row);
        }

        CheckedCount = 0;
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(IsEmpty));

        RefreshSessionTree();
    }

    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TableRow.IsSelected) || sender is not FileRow row)
        {
            return;
        }

        if (row.IsSelected)
        {
            _checkedKeys.Add(row.SelectionKey);
        }
        else
        {
            _checkedKeys.Remove(row.SelectionKey);
        }

        CheckedCount = _checkedKeys.Count;
    }

    public IReadOnlyList<FileRow> CheckedFiles => Files
        .Where(row => _checkedKeys.Contains(row.SelectionKey))
        .ToList();

    [RelayCommand]
    private void ToggleCheckAll()
    {
        var value = CheckedCount != Files.Count;
        foreach (var row in Files)
        {
            row.IsSelected = value;
        }

        CheckedCount = _checkedKeys.Count;
    }

    [RelayCommand]
    private void ClearChecked()
    {
        foreach (var row in Files)
        {
            row.IsSelected = false;
        }

        CheckedCount = 0;
    }

    private bool CanExportWord(ExportType type) => !IsExporting && Files.Count > 0;

    [RelayCommand(CanExecute = nameof(CanExportWord))]
    private async Task ExportWordAsync(ExportType type)
    {
        var query = CreateCurrentQuerySnapshot();
        if (query is null)
        {
            return;
        }

        IsExporting = true;
        try
        {
            var request = type == ExportType.ExportFollowSelected
                ? ExportWordRequest.FollowSelected(CheckedFiles, query)
                : ExportWordRequest.FollowCurrentFilter(query);
            if (type == ExportType.ExportFollowSelected && request.SelectedRows.Count == 0)
            {
                _dialogService.ShowInformation(
                    "Chưa có tệp nào được tick. Hãy tick vào ô ở cột Chọn rồi thực hiện lại.",
                    "DeltaBat - Xuất Word");
                return;
            }

            var targetPath = _dialogService.AskExportWordPath(BuildExportFileName(type));
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return;
            }

            StatusText = $"Đang xuất Word: {type.ToDisplayName().ToLowerInvariant()}...";
            var result = await _exportService.ExportAsync(request, targetPath);
            StatusText = $"Đã xuất {result.RowCount:N0} dòng ra {result.FilePath}";
            if (_dialogService.Confirm(
                    $"Đã xuất {result.RowCount:N0} dòng ra tệp:\n{result.FilePath}\n\nMở tệp vừa xuất?",
                    "DeltaBat - Xuất Word"))
            {
                _dialogService.OpenWithShell(result.FilePath);
            }
        }
        catch (Exception exception)
        {
            ReportError("Không xuất được tệp Word.", exception);
        }
        finally
        {
            IsExporting = false;
        }
    }

    private static string BuildExportFileName(ExportType type)
    {
        var suffix = type == ExportType.ExportFollowSelected ? "theo-dong-chon" : "theo-dieu-kien-loc";
        return $"Danh-sach-file_{suffix}_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
    }

    private void ReportError(string action, Exception exception)
    {
        StatusText = action;
        _dialogService.ShowError($"{action}\n\n{exception.Message}");
    }
}
