namespace Sts2Emulator.Trace;

public sealed record ReferenceSourceMatch(
    string RelativePath,
    int LineNumber,
    string Line);

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
        int maxMatches = 200)
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

            var lineNumber = 0;
            foreach (var line in File.ReadLines(path))
            {
                lineNumber++;
                if (!line.Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                matches.Add(new ReferenceSourceMatch(
                    Path.GetRelativePath(root, path)
                        .Replace(Path.DirectorySeparatorChar, '/'),
                    lineNumber,
                    line.Trim()));

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
