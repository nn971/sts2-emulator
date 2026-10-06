namespace Sts2Emulator.Trace;

public sealed record ReferenceSourceContextLine(
    int LineNumber,
    bool IsMatch,
    string Line);

public sealed record ReferenceSourceMatch(
    string RelativePath,
    int LineNumber,
    string Line,
    IReadOnlyList<ReferenceSourceContextLine>? Context = null);

public sealed record ReferenceSourceSearchResult(
    string RootDirectory,
    string Query,
    int FilesScanned,
    IReadOnlyList<ReferenceSourceMatch> Matches);

public static class ReferenceSourceSearch
{
    public static ReferenceSourceSearchResult Search(
        string rootDirectory,
        string query,
        int maxMatches = 200,
        int contextLines = 0)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException(
                "Source directory must be non-empty.",
                nameof(rootDirectory));
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException(
                "Source query must be non-empty.",
                nameof(query));
        }

        if (maxMatches <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxMatches),
                "Maximum match count must be positive.");
        }

        if (contextLines < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contextLines),
                "Context line count cannot be negative.");
        }

        var root = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(
                $"Decompiled source directory does not exist: {root}");
        }

        var matches = new List<ReferenceSourceMatch>();
        var filesScanned = 0;

        foreach (var path in Directory.EnumerateFiles(
                     root,
                     "*.cs",
                     SearchOption.AllDirectories)
                 .Order(StringComparer.Ordinal))
        {
            filesScanned++;

            var lines = File.ReadAllLines(path);
            var relativePath = Path.GetRelativePath(root, path)
                .Replace(Path.DirectorySeparatorChar, '/');

            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (!line.Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                IReadOnlyList<ReferenceSourceContextLine>? context = null;
                if (contextLines > 0)
                {
                    var start = Math.Max(0, index - contextLines);
                    var end = Math.Min(lines.Length - 1, index + contextLines);
                    var contextBuffer = new List<ReferenceSourceContextLine>();

                    for (var contextIndex = start;
                         contextIndex <= end;
                         contextIndex++)
                    {
                        contextBuffer.Add(new ReferenceSourceContextLine(
                            contextIndex + 1,
                            contextIndex == index,
                            lines[contextIndex]));
                    }

                    context = contextBuffer;
                }

                matches.Add(new ReferenceSourceMatch(
                    relativePath,
                    index + 1,
                    line.Trim(),
                    context));

                if (matches.Count >= maxMatches)
                {
                    return new ReferenceSourceSearchResult(
                        root,
                        query,
                        filesScanned,
                        matches);
                }
            }
        }

        return new ReferenceSourceSearchResult(
            root,
            query,
            filesScanned,
            matches);
    }
}
