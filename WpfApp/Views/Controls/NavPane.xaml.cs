using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WpfApp.Models.Tables;
using WpfApp.ViewModel;
using WpfApp.ViewModel.Tables;

namespace WpfApp.Views.Controls;

public sealed partial class NavPane : UserControl
{
    private bool _isSyncingTreeSelection;
    private bool _canOpenFileContextMenu;
    private SessionFileTreeNode? _selectionAnchor;

    private TreeView? _sessionFileTree;

    private MainWindowViewModel? ViewModel;

    public NavPane()
    {
        InitializeComponent();
        NavTabs.SelectionChanged += NavTabs_SelectionChanged;
        DataContextChanged += OnDataContextChangedImpl;
    }

    private void NavTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source == NavTabs && NavTabs.SelectedItem == TreeTab)
        {
            QueueSessionTreeSelectionSync();
        }
    }

    private void SessionTree_SelectedItemChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object?> e)
    {
        if (!_isSyncingTreeSelection && e.NewValue is SessionFileTreeNode node)
        {
            ViewModel?.SessionFiles.SelectFromTree(node);
        }
    }

    private void SessionFileTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TreeView tree ||
            ItemsControl.ContainerFromElement(tree, e.OriginalSource as DependencyObject) is not TreeViewItem container ||
            container.DataContext is not SessionFileTreeNode { IsSessionRoot: false, IsPlaceholder: false } node ||
            ViewModel is not { } viewModel)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            viewModel.SessionFiles.ToggleContextSelection(node);
            _selectionAnchor = node;
        }
        else if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            SelectContextRange(viewModel.SessionFiles, node);
        }
        else
        {
            viewModel.SessionFiles.SetContextSelection([node]);
            _selectionAnchor = node;
        }

        SetNativeTreeSelection(container);
    }

    private void SessionFileTree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _canOpenFileContextMenu = false;
        if (sender is not TreeView tree ||
            ItemsControl.ContainerFromElement(tree, e.OriginalSource as DependencyObject) is not TreeViewItem container ||
            container.DataContext is not SessionFileTreeNode { IsSessionRoot: false, IsPlaceholder: false } node ||
            ViewModel is not { } viewModel)
        {
            return;
        }

        _canOpenFileContextMenu = true;
        if (!node.IsContextSelected)
        {
            viewModel.SessionFiles.SetContextSelection([node]);
            _selectionAnchor = node;
        }

        SetNativeTreeSelection(container);
    }

    private void SessionFileTree_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (!_canOpenFileContextMenu)
        {
            e.Handled = true;
        }

        _canOpenFileContextMenu = false;
    }

    private void SelectContextRange(SessionFilesViewModel sessionFiles, SessionFileTreeNode node)
    {
        var fileNodes = sessionFiles.SessionTree
            .SelectMany(FlattenTree)
            .Where(item => !item.IsSessionRoot && !item.IsPlaceholder)
            .ToList();
        var anchorIndex = _selectionAnchor is null ? -1 : fileNodes.IndexOf(_selectionAnchor);
        var targetIndex = fileNodes.IndexOf(node);
        if (anchorIndex < 0 || targetIndex < 0)
        {
            sessionFiles.SetContextSelection([node]);
            _selectionAnchor = node;
            return;
        }

        var start = Math.Min(anchorIndex, targetIndex);
        var count = Math.Abs(anchorIndex - targetIndex) + 1;
        sessionFiles.SetContextSelection(fileNodes.GetRange(start, count));
    }

    private static IEnumerable<SessionFileTreeNode> FlattenTree(SessionFileTreeNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in FlattenTree(child))
            {
                yield return descendant;
            }
        }
    }

    private void SetNativeTreeSelection(TreeViewItem container)
    {
        _isSyncingTreeSelection = true;
        try
        {
            container.IsSelected = true;
        }
        finally
        {
            _isSyncingTreeSelection = false;
        }
    }

    private void SessionFileTree_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeView tree &&
            ItemsControl.ContainerFromElement(tree, e.OriginalSource as DependencyObject) is TreeViewItem
            {
                DataContext: SessionFileTreeNode node
            })
        {
            ViewModel?.SessionFiles.SelectFromTree(node);
        }
    }

    private void SessionFileTree_Loaded(object sender, RoutedEventArgs e)
    {
        _sessionFileTree = sender as TreeView;
        QueueSessionTreeSelectionSync();
    }

    private void SessionFileTree_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: SessionFileTreeNode node } && node.IsSessionRoot)
        {
            _ = ViewModel?.SessionFiles.LoadTreeChildrenAsync(node);
        }
    }

    private void OnDataContextChangedImpl(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is MainWindowViewModel next && !ReferenceEquals(next, ViewModel))
        {
            ViewModel = next;
            next.SessionFiles.PropertyChanged += SessionFiles_PropertyChanged;
        }

        QueueSessionTreeSelectionSync();
    }

    private void SessionFiles_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SessionFilesViewModel.SelectedFile)
            or nameof(SessionFilesViewModel.SessionTree))
        {
            QueueSessionTreeSelectionSync();
        }
    }

    private void QueueSessionTreeSelectionSync() =>
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() => _ = SyncSessionTreeSelectionAsync()));

    private async Task SyncSessionTreeSelectionAsync()
    {
        var sessionFiles = ViewModel?.SessionFiles;
        var selectedFile = sessionFiles?.SelectedFile;
        if (sessionFiles is null)
        {
            return;
        }

        if (selectedFile is null)
        {
            ClearTreeSelection();
            return;
        }

        if (sessionFiles.CurrentSessionId is { } sessionId &&
            sessionFiles.SessionTree.FirstOrDefault(root => root.SessionId == sessionId) is { } sessionRoot)
        {
            sessionRoot.IsExpanded = true;
            if (!sessionRoot.AreChildrenLoaded)
            {
                await sessionFiles.LoadTreeChildrenAsync(sessionRoot);
                while (sessionRoot.IsLoadingChildren)
                {
                    await Task.Delay(25);
                }
            }
        }

        if (!ReferenceEquals(sessionFiles.SelectedFile, selectedFile))
        {
            return;
        }

        var node = sessionFiles.FindTreeNode(selectedFile);
        if (node is null)
        {
            return;
        }

        NavTabs.SelectedItem = TreeTab;
        await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Loaded);

        var tree = _sessionFileTree;
        if (tree is null)
        {
            return;
        }

        var root = SessionTreeNodeContaining(node);
        if (root is null)
        {
            return;
        }

        root.IsExpanded = true;
        tree.UpdateLayout();
        var rootContainer = tree.ItemContainerGenerator.ContainerFromItem(root) as TreeViewItem;
        if (rootContainer is null)
        {
            return;
        }

        rootContainer.IsExpanded = true;
        rootContainer.BringIntoView();
        tree.UpdateLayout();
        var container = rootContainer.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
        if (container is null)
        {
            return;
        }

        _isSyncingTreeSelection = true;
        try
        {
            container.IsSelected = true;
            container.BringIntoView();
        }
        finally
        {
            _isSyncingTreeSelection = false;
        }
    }

    private void ClearTreeSelection()
    {
        var tree = _sessionFileTree;
        if (tree?.SelectedItem is not SessionFileTreeNode selectedNode)
        {
            return;
        }

        var root = SessionTreeNodeContaining(selectedNode);
        var container = root is null
            ? tree.ItemContainerGenerator.ContainerFromItem(selectedNode) as TreeViewItem
            : FindContainer(tree, root, selectedNode);
        if (container is null)
        {
            return;
        }

        _isSyncingTreeSelection = true;
        try
        {
            container.IsSelected = false;
        }
        finally
        {
            _isSyncingTreeSelection = false;
        }
    }

    private static TreeViewItem? FindContainer(
        ItemsControl parent,
        SessionFileTreeNode parentNode,
        SessionFileTreeNode target)
    {
        var parentContainer = parent.ItemContainerGenerator.ContainerFromItem(parentNode) as TreeViewItem;
        if (ReferenceEquals(parentNode, target))
        {
            return parentContainer;
        }

        if (parentContainer is null)
        {
            return null;
        }

        foreach (var child in parentNode.Children)
        {
            var found = FindContainer(parentContainer, child, target);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private SessionFileTreeNode? SessionTreeNodeContaining(SessionFileTreeNode target) =>
        ViewModel?.SessionFiles.SessionTree.FirstOrDefault(root => ContainsNode(root, target));

    private static bool ContainsNode(SessionFileTreeNode parent, SessionFileTreeNode target) =>
        ReferenceEquals(parent, target) || parent.Children.Any(child => ContainsNode(child, target));
}
