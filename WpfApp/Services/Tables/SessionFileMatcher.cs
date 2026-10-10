using WpfApp.Models.Tables;

namespace WpfApp.Services.Tables;

internal static class SessionFileMatcher
{
    private const int ScoreExtension = 1;
    private const int ScoreName = 2;
    private const int ScoreNameAndFolder = 3;
    private const int ScoreId = 4;
    private const int ScoreSameInstance = 5;

    public static int FindIndex(IReadOnlyList<FileRow> candidates, FileRow? target)
    {
        if (target is null || candidates.Count == 0)
        {
            return -1;
        }

        var bestIndex = -1;
        var bestScore = 0;
        var extensionMatch = -1;
        var extensionMatches = 0;

        for (var index = 0; index < candidates.Count; index++)
        {
            var score = Score(candidates[index], target);
            if (score >= ScoreName)
            {
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = index;
                }

                continue;
            }

            if (score == ScoreExtension)
            {
                extensionMatches++;
                extensionMatch = index;
            }
        }

        return bestIndex >= 0 ? bestIndex : extensionMatches == 1 ? extensionMatch : -1;
    }

    public static FileRow? Find(IReadOnlyList<FileRow> candidates, FileRow? target)
    {
        var index = FindIndex(candidates, target);
        return index >= 0 ? candidates[index] : null;
    }

    private static int Score(FileRow candidate, FileRow target)
    {
        if (ReferenceEquals(candidate, target))
        {
            return ScoreSameInstance;
        }

        if (target.IdF > 0 && candidate.IdF > 0)
        {
            return target.IdF == candidate.IdF ? ScoreId : 0;
        }

        if (NameMatches(candidate, target))
        {
            return FoldersMatch(candidate, target) ? ScoreNameAndFolder : ScoreName;
        }

        return ExtensionMatches(candidate, target) ? ScoreExtension : 0;
    }

    private static bool NameMatches(FileRow candidate, FileRow target)
    {
        var expected = GetComparableFileName(target);
        return expected.Length > 0
            && string.Equals(expected, GetComparableFileName(candidate), StringComparison.OrdinalIgnoreCase);
    }

    private static bool ExtensionMatches(FileRow candidate, FileRow target)
    {
        var expected = NormalizeExtension(target.Extension);
        return expected.Length > 0
            && string.Equals(expected, NormalizeExtension(candidate.Extension), StringComparison.OrdinalIgnoreCase);
    }

    private static bool FoldersMatch(FileRow candidate, FileRow target)
    {
        var left = GetComparableFolder(candidate);
        var right = GetComparableFolder(target);
        return left.Length > 0 && right.Length > 0
            && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeExtension(string? extension) =>
        extension?.Trim().TrimStart('.').ToLowerInvariant() ?? string.Empty;

    private static string GetComparableFolder(FileRow file) =>
        file.Path?.Trim().TrimEnd('\\', '/') ?? string.Empty;

    private static string GetComparableFileName(FileRow file)
    {
        var name = file.Name?.Trim() ?? string.Empty;
        var extension = NormalizeExtension(file.Extension);
        if (extension.Length > 0 && !name.EndsWith($".{extension}", StringComparison.OrdinalIgnoreCase))
        {
            name += $".{extension}";
        }

        return name;
    }
}
