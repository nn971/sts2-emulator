using System.Security.Cryptography;
using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Trace;

public static class ReferenceBuildFingerprint
{
    public const string ManifestSchemaId = "sts2-reference-build-v0";

    private static readonly string[] RequiredDataFiles =
    [
        "sts2.dll",
        "0Harmony.dll",
        "GodotSharp.dll"
    ];

    public static ReferenceBuildManifest Capture(
        string gameDirectory,
        string? dataDirectory = null)
    {
        var gameDir = Path.GetFullPath(gameDirectory);
        if (!Directory.Exists(gameDir))
        {
            throw new DirectoryNotFoundException(
                $"STS2 game directory does not exist: {gameDir}");
        }

        var dataDir = ResolveDataDirectory(gameDir, dataDirectory);
        var releaseInfoPath = ResolveRequiredFile(
            "release_info.json",
            gameDir,
            dataDir);
        var runtimeConfigPath = ResolveRequiredFile(
            "sts2.runtimeconfig.json",
            gameDir,
            dataDir);

        using var releaseDoc = JsonDocument.Parse(File.ReadAllText(releaseInfoPath));
        using var runtimeDoc = JsonDocument.Parse(File.ReadAllText(runtimeConfigPath));

        var tfm = runtimeDoc.RootElement
            .GetProperty("runtimeOptions")
            .GetProperty("tfm")
            .GetString();

        if (string.IsNullOrWhiteSpace(tfm))
        {
            throw new InvalidOperationException(
                "sts2.runtimeconfig.json has no runtimeOptions.tfm value.");
        }

        var files = new List<ReferenceBuildFile>
        {
            DescribeFile("release_info", releaseInfoPath),
            DescribeFile("runtime_config", runtimeConfigPath)
        };

        foreach (var fileName in RequiredDataFiles)
        {
            var path = Path.Combine(dataDir, fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Required STS2 assembly '{fileName}' is missing from '{dataDir}'.",
                    path);
            }

            files.Add(DescribeFile(
                Path.GetFileNameWithoutExtension(fileName),
                path));
        }

        var identity = new ReferenceBuildIdentity(
            SchemaId: ManifestSchemaId,
            ReleaseInfo: releaseDoc.RootElement.Clone(),
            TargetFramework: tfm,
            Files: files
                .OrderBy(file => file.LogicalName, StringComparer.Ordinal)
                .ToArray());

        return new ReferenceBuildManifest(
            SchemaId: ManifestSchemaId,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            GameDirectory: gameDir,
            DataDirectory: dataDir,
            Identity: identity,
            BuildFingerprint: CanonicalJson.Sha256(identity));
    }

    private static string ResolveDataDirectory(
        string gameDirectory,
        string? explicitDataDirectory)
    {
        if (!string.IsNullOrWhiteSpace(explicitDataDirectory))
        {
            var explicitPath = Path.GetFullPath(explicitDataDirectory);
            if (!File.Exists(Path.Combine(explicitPath, "sts2.dll")))
            {
                throw new DirectoryNotFoundException(
                    $"Explicit STS2 data directory does not contain sts2.dll: {explicitPath}");
            }

            return explicitPath;
        }

        if (File.Exists(Path.Combine(gameDirectory, "sts2.dll")))
        {
            return gameDirectory;
        }

        var candidates = Directory
            .EnumerateDirectories(gameDirectory, "data_sts2_*", SearchOption.TopDirectoryOnly)
            .Where(path => File.Exists(Path.Combine(path, "sts2.dll")))
            .Order(StringComparer.Ordinal)
            .ToArray();

        return candidates.Length switch
        {
            1 => candidates[0],
            0 => throw new DirectoryNotFoundException(
                "Could not locate an STS2 data_sts2_* directory containing sts2.dll. " +
                "Pass the data directory explicitly."),
            _ => throw new InvalidOperationException(
                "Multiple STS2 data directories contain sts2.dll. " +
                "Pass the intended data directory explicitly.")
        };
    }

    private static string ResolveRequiredFile(
        string fileName,
        string gameDirectory,
        string dataDirectory)
    {
        var candidates = new[]
        {
            Path.Combine(gameDirectory, fileName),
            Path.Combine(dataDirectory, fileName)
        }
        .Distinct(StringComparer.Ordinal)
        .Where(File.Exists)
        .ToArray();

        return candidates.Length switch
        {
            1 => candidates[0],
            0 => throw new FileNotFoundException(
                $"Required STS2 metadata file '{fileName}' was not found in the game or data directory."),
            _ => throw new InvalidOperationException(
                $"Found multiple '{fileName}' files. Pass a data directory that matches the intended build.")
        };
    }

    private static ReferenceBuildFile DescribeFile(
        string logicalName,
        string path)
    {
        using var stream = File.OpenRead(path);
        var hash = Convert.ToHexStringLower(SHA256.HashData(stream));
        var info = new FileInfo(path);

        return new ReferenceBuildFile(
            logicalName,
            info.Name,
            info.Length,
            hash);
    }
}
