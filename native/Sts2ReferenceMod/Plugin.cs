#if STS2_NATIVE
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace Sts2ReferenceMod;

/// <summary>
/// Verified game-assembly builds expose a loader entrypoint. This intentionally
/// installs no speculative Harmony patches: native hook binding must be audited
/// against the exact local game build before capture can be advertised.
/// </summary>
[ModInitializer("Initialize")]
public static class Plugin
{
    public static void Initialize()
    {
        string? captureValue = System.Environment.GetEnvironmentVariable("STS2_FIDELITY_CAPTURE");
        Log.Info("[Sts2ReferenceMod] CAPTURE_STATUS=MOD_INITIALIZED; capture_env=" +
            (captureValue is null ? "MISSING" : captureValue == "1" ? "ENABLED" : "DISABLED") +
            "; trace_dir=" +
            (string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("STS2_FIDELITY_TRACE_DIR"))
                ? "GODOT_USER_DATA_DEFAULT" : "EXPLICIT"));
        if (string.Equals(captureValue, "1", StringComparison.Ordinal))
        {
            Log.Info("[Sts2ReferenceMod] Native passive combat capture is explicitly enabled.");
            NativeV111PassiveBridge.Start();
            // Extra environment gate: default passive Inspector stays intact,
            // while source-level RNG observation must be explicitly requested.
            NativeV111RngHook.TryInstall();
        }
        else
        {
            Log.Info("[Sts2ReferenceMod] Passive capture disabled (set STS2_FIDELITY_CAPTURE=1).");
        }
    }
}
#else
namespace Sts2ReferenceMod;

/// <summary>Standalone contract/runtime build, independent of game assemblies.</summary>
public static class Plugin
{
    public const string IntegrationState = "NATIVE_HOOKS_UNWIRED";
}
#endif
