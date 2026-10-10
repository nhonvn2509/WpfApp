namespace WpfApp.Models.Tables;

public sealed record SessionMetadataColumn(string Key, string Header, double Width);

public sealed class SessionMetadataSection
{
    public SessionMetadataSection(
        string title,
        string elementName,
        IReadOnlyList<SessionMetadataColumn> columns,
        IReadOnlyList<Dictionary<string, string>> rows,
        IReadOnlyList<FileRow>? fileRows = null)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        Title = title;
        ElementName = elementName;
        Columns = columns;
        Rows = rows;
        FileRows = fileRows;
    }

    public string Title { get; }

    public string ElementName { get; }

    public IReadOnlyList<SessionMetadataColumn> Columns { get; }

    public IReadOnlyList<Dictionary<string, string>> Rows { get; }

    public IReadOnlyList<FileRow>? FileRows { get; }
}
