namespace Sts2ReferenceBridge;

internal static class BridgeWorkspace
{
    public const string TraceSchema = "0.2";
    public const string ProbeSchema = "sts2-reference-probe-v0";

    // User-audited oracle build:
    // STS2 v0.111.0, commit 41cef1ea, sts2.dll SHA-256
    // 0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9.
    public const string ExpectedBuildFingerprint =
        "3bb5598a35f7763c9de22078643ac2777190994b2be85ebd4ebf5c9d154aa45e";

    public const string ExpectedGameVersion = "v0.111.0";
    public const string ExpectedGameCommit = "41cef1ea";
}
