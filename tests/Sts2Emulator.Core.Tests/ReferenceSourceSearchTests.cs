using Sts2Emulator.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class ReferenceSourceSearchTests
{
    [Fact]
    public void FindsCaseInsensitiveMatchesWithStableRelativePaths()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"sts2-source-search-{Guid.NewGuid():N}");
        var nested = Path.Combine(root, "MegaCrit", "Sts2");
        Directory.CreateDirectory(nested);

        try
        {
            File.WriteAllLines(
                Path.Combine(nested, "CardPlay.cs"),
                [
                    "namespace Example;",
                    "public static class CardPlay",
                    "{",
                    "    public static void ResolveSly() { }",
                    "}"
                ]);
            File.WriteAllText(
                Path.Combine(root, "Other.cs"),
                "public static class Other { }");

            var result = ReferenceSourceSearch.Search(
                root,
                "sly");

            Assert.Equal(2, result.FilesScanned);
            var match = Assert.Single(result.Matches);
            Assert.Equal(
                "MegaCrit/Sts2/CardPlay.cs",
                match.RelativePath);
            Assert.Equal(4, match.LineNumber);
            Assert.Equal(
                "public static void ResolveSly() { }",
                match.Line);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void StopsAtRequestedMatchLimit()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"sts2-source-limit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllLines(
                Path.Combine(root, "Many.cs"),
                [
                    "Sly",
                    "Sly",
                    "Sly"
                ]);

            var result = ReferenceSourceSearch.Search(
                root,
                "Sly",
                maxMatches: 2);

            Assert.Equal(2, result.Matches.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
