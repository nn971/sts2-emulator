using Sts2Emulator.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class ReferenceBuildFingerprintTests
{
    [Fact]
    public void FingerprintIsStableAcrossCaptureTimeAndInstallPath()
    {
        var left = CreateFakeGame("left");
        var right = CreateFakeGame("right");

        try
        {
            var first = ReferenceBuildFingerprint.Capture(left.GameDir);
            var second = ReferenceBuildFingerprint.Capture(left.GameDir);
            var relocated = ReferenceBuildFingerprint.Capture(right.GameDir);

            Assert.Equal(first.BuildFingerprint, second.BuildFingerprint);
            Assert.Equal(first.BuildFingerprint, relocated.BuildFingerprint);
            Assert.NotEqual(first.CapturedAtUtc, second.CapturedAtUtc);
            Assert.NotEqual(first.GameDirectory, relocated.GameDirectory);
            Assert.Equal("net9.0", first.Identity.TargetFramework);
            Assert.Equal(5, first.Identity.Files.Length);
        }
        finally
        {
            Directory.Delete(left.Root, recursive: true);
            Directory.Delete(right.Root, recursive: true);
        }
    }

    [Fact]
    public void FingerprintChangesWhenGameAssemblyChanges()
    {
        var fake = CreateFakeGame("mutation");
        try
        {
            var before = ReferenceBuildFingerprint.Capture(fake.GameDir);
            File.AppendAllText(Path.Combine(fake.DataDir, "sts2.dll"), "changed");
            var after = ReferenceBuildFingerprint.Capture(fake.GameDir);

            Assert.NotEqual(before.BuildFingerprint, after.BuildFingerprint);
        }
        finally
        {
            Directory.Delete(fake.Root, recursive: true);
        }
    }

    private static (string Root, string GameDir, string DataDir) CreateFakeGame(
        string suffix)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"sts2-fingerprint-{suffix}-{Guid.NewGuid():N}");
        var gameDir = Path.Combine(root, "Slay the Spire 2");
        var dataDir = Path.Combine(gameDir, "data_sts2_linuxbsd_x86_64");
        Directory.CreateDirectory(dataDir);

        File.WriteAllText(
            Path.Combine(gameDir, "release_info.json"),
            """{"version":"test-build","commit":"abcdef"}""");
        File.WriteAllText(
            Path.Combine(gameDir, "sts2.runtimeconfig.json"),
            """{"runtimeOptions":{"tfm":"net9.0"}}""");

        File.WriteAllText(Path.Combine(dataDir, "sts2.dll"), "sts2");
        File.WriteAllText(Path.Combine(dataDir, "0Harmony.dll"), "harmony");
        File.WriteAllText(Path.Combine(dataDir, "GodotSharp.dll"), "godot");

        return (root, gameDir, dataDir);
    }
}
