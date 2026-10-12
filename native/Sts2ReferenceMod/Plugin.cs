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
        Log.Info("[Sts2ReferenceMod] Inspector foundation loaded; native decision hooks NOT YET ATTACHED. No parity capture is active.");
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
