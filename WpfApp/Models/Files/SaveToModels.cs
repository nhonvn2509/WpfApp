using WpfApp.Models.Tables;

namespace WpfApp.Models.Files;

public enum SaveToScope
{
    SelectedSessions = 0,
    VisibleSessions = 1,
    FullQuery = 2
}

public enum SaveToConflictMode
{
    Overwrite = 0,
    AddCopyNumber = 1
}

public sealed record SaveToFolderEntry(string Path, bool IsActive)
{
    public SaveToFolderEntry WithActive(bool isActive) => this with { IsActive = isActive };
}

public sealed record SaveToFileItem(
    long SessionId,
    byte DayFgr,
    string SessionName,
    long FileId,
    string SourcePath,
    string FileName,
    string? Extension,
    string? HeaderXml,
    DateTime? Start = null);

public sealed record SaveToPlanItem(
    SaveToFileItem File,
    string DestinationPath,
    bool DestinationExists);

public sealed record SaveToCopyResult(
    int SessionsTotal,
    int SessionsCopied,
    int FilesCopied,
    int FilesFailed,
    IReadOnlyList<string> Errors);
