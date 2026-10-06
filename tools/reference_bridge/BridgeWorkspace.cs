namespace Sts2ReferenceBridge;

internal static class BridgeWorkspace
{
    public const string TraceSchema = "0.2";
    public const string ProbeSchema = "sts2-reference-probe-v0";

    // User-audited oracle build:
    // STS2 v0.111.0, commit 41cef1ea.
    public const string ExpectedBuildFingerprint =
        "3bb5598a35f7763c9de22078643ac2777190994b2be85ebd4ebf5c9d154aa45e";
    public const string ExpectedGameVersion = "v0.111.0";
    public const string ExpectedGameCommit = "41cef1ea";
    public const string ExpectedTargetFramework = "net9.0";

    public static IReadOnlyDictionary<string, string> ExpectedFileHashes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sts2.dll"] =
                "0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9",
            ["0Harmony.dll"] =
                "ef1898322c9f5c86dc1b0758b272a9c440823b4a41ca9a0b82a3aa6b3d206387",
            ["GodotSharp.dll"] =
                "0e4897ecdfb31456a97c7d8028dfb8d7dbdc632e2f73fc9b438d7b266a139289",
            ["release_info.json"] =
                "74f244b13e9c6849149bec3b37e1729777a7f21306c56383405c2949eea62e22",
            ["sts2.runtimeconfig.json"] =
                "bd81a252bc3f0e8bcb649b404c1da945913c5377b9d3a2c416c52730be8714c0"
        };
}
