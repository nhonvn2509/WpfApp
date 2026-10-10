using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MahApps.Metro.IconPacks;
using WpfApp.Presentation;

namespace WpfApp.Models.Tables;

public sealed partial class SessionFileTreeNode : ObservableObject
{
    private SessionFileTreeNode(
        string name,
        bool isSessionRoot,
        string details,
        string? path,
        long? sessionId = null)
    {
        Name = name;
        IsSessionRoot = isSessionRoot;
        Details = details;
        Path = path;
        SessionId = sessionId;
    }

    public string Name { get; }
    public bool IsSessionRoot { get; }
    public string Details { get; private set; }
    public string? Path { get; }

    public long? SessionId { get; }

    public ObservableCollection<SessionFileTreeNode> Children { get; } = [];

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isLoadingChildren;

    [ObservableProperty]
    private bool _areChildrenLoaded;

    [ObservableProperty]
    private bool _isContextSelected;

    public string DisplayName => IsSessionRoot || string.IsNullOrWhiteSpace(Extension)
        ? Name
        : $"{Name}.{Extension.TrimStart('.')}";

    public PackIconLucideKind IconKind => IsSessionRoot
        ? IconCatalog.Folder
        : IconCatalog.File(Extension, Type);

    public string IconBrushKey => IsSessionRoot ? IconCatalog.SessionBrushKey : IconCatalog.FileBrushKey;

    public string? Extension { get; private init; }

    public string? Type { get; private init; }

    public long? Length { get; private init; }

    public bool IsPlaceholder { get; private init; }

    public FileRow? ToFileRow() =>
        IsSessionRoot || IsPlaceholder || string.IsNullOrWhiteSpace(Name)
            ? null
            : new FileRow
            {
                Name = Name,
                Extension = Extension,
                Path = Path,
                Length = Length
            };

    public static SessionFileTreeNode CreateRoot(
        string title,
        IReadOnlyList<SessionFileTreeNode>? children = null,
        long? sessionId = null,
        int? fileCount = null)
    {
        var node = new SessionFileTreeNode(
            title,
            true,
            FormatCount(fileCount ?? children?.Count ?? 0),
            null,
            sessionId)
        {
            AreChildrenLoaded = children is not null
        };

        if (children is not null)
        {
            foreach (var child in children)
            {
                node.Children.Add(child);
            }
        }

        return node;
    }

    public static SessionFileTreeNode CreateSessionRoot(SessionRow session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return CreateRoot(
            string.IsNullOrWhiteSpace(session.Name) ? $"Phiên {session.IdS:N0}" : session.Name,
            sessionId: session.IdS,
            fileCount: session.Files);
    }

    public void LoadChildren(IReadOnlyList<SessionFileTreeNode> children, string? message = null)
    {
        Children.Clear();
        foreach (var child in children)
        {
            Children.Add(child);
        }

        Details = message ?? FormatCount(children.Count);
        AreChildrenLoaded = true;
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(Children));
    }

    private static string FormatCount(int count) => $"{count:N0} tệp";

    public static SessionFileTreeNode CreateEmpty(string message) =>
        new(message, false, "Không có dữ liệu file", null) { IsPlaceholder = true };

    public static SessionFileTreeNode CreateFile(
        string name,
        string? extension,
        long? length,
        string? type,
        string? path) =>
        new SessionFileTreeNode(
            string.IsNullOrWhiteSpace(name) ? "(không tên)" : name,
            false,
            FormatDetails(length),
            path)
        {
            Extension = extension,
            Type = type,
            Length = length
        };

    private static string FormatDetails(long? length) =>
        length is { } size ? $"{size:N0} byte" : string.Empty;
}
