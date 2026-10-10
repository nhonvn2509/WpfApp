using WpfApp.Models.Files;
using WpfApp.Models.Tables;
using WpfApp.Services.Tables;

namespace WpfApp.Services.Files;

public sealed class SaveToFileCollector
{
    private readonly IFilePathResolver _paths;
    private readonly ISessionsLoadService _sessions;

    public SaveToFileCollector(IFilePathResolver paths, ISessionsLoadService sessions)
    {
        _paths = paths;
        _sessions = sessions;
    }

    public async Task<IReadOnlyList<SaveToFileItem>> CollectAsync(
        IEnumerable<SessionRow> sessions,
        IReadOnlyList<string> extensionFilter,
        bool saveHeader,
        bool renameByTime = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(extensionFilter);
        var items = new List<SaveToFileItem>();
        foreach (var session in sessions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sessionName = string.IsNullOrWhiteSpace(session.Name) ? $"Phiên {session.IdS:N0}" : session.Name.Trim();
            var xml = await _sessions.LoadSessionXmlAsync(session.IdS, cancellationToken).ConfigureAwait(false);
            var xmlFiles = SessionFileTreeParser.Parse(xml)
                .Where(node => !node.IsPlaceholder && !node.IsSessionRoot)
                .ToList();
            IReadOnlyDictionary<string, DateTime> fileStarts = renameByTime
                ? await _sessions.LoadFileStartsAsync(session.IdS, cancellationToken).ConfigureAwait(false)
                : new Dictionary<string, DateTime>();
            foreach (var node in xmlFiles)
            {
                if (!SaveToCatalogService.MatchesFilter(node.Extension, extensionFilter))
                {
                    continue;
                }

                var source = _paths.GetFullPath(node.Path, node.Name, node.Extension);
                if (string.IsNullOrWhiteSpace(source))
                {
                    continue;
                }

                items.Add(new SaveToFileItem(
                    session.IdS,
                    session.DayFgr,
                    sessionName,
                    0,
                    source,
                    node.DisplayName,
                    node.Extension,
                    saveHeader ? xml : null,
                    ResolveStart(fileStarts, node, session)));
            }

            if (xmlFiles.Count == 0 && saveHeader && !string.IsNullOrWhiteSpace(xml))
            {
                items.Add(new SaveToFileItem(
                    session.IdS,
                    session.DayFgr,
                    sessionName,
                    0,
                    string.Empty,
                    $"{sessionName}.xml",
                    "xml",
                    xml,
                    session.Start));
            }
        }

        return items;
    }

    private static DateTime? ResolveStart(
        IReadOnlyDictionary<string, DateTime> fileStarts,
        SessionFileTreeNode node,
        SessionRow session)
    {
        if (!string.IsNullOrWhiteSpace(node.Path) && fileStarts.TryGetValue(node.Path, out var byPath))
        {
            return byPath;
        }

        if (!string.IsNullOrWhiteSpace(node.DisplayName) && fileStarts.TryGetValue(node.DisplayName, out var byName))
        {
            return byName;
        }

        // Fallback: DT_START của phiên — vẫn cho ra tên giờ hợp lý khi không match được tệp.
        return session.Start;
    }
}
