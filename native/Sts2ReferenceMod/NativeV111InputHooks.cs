#if STS2_NATIVE
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Sts2ReferenceMod;

/// <summary>
/// Optional, observer-only hook at the actual v0.111 local action *request*
/// entry. This is deliberately before the game's deferral/cancellation logic:
/// observing RequestEnqueue never proves acceptance. Executed actions are
/// separately paired by object identity with ActionExecutor callbacks.
/// </summary>
internal static class NativeV111InputHooks
{
    private const string Owner = "nn971.sts2-inspector.native-input-v111";
    private static Harmony? _patches;
    internal static bool IsInstalled => _patches is not null;

    internal static void TryInstall()
    {
        if (IsInstalled ||
            System.Environment.GetEnvironmentVariable("STS2_FIDELITY_INPUT_HOOK") != "1")
            return;
        if (!NativeV111PassiveBridge.ReadyForNativeRngHook)
        {
            GD.PushError("[Sts2ReferenceMod] INPUT_HOOK_STATUS=PIN_OR_SCENE_REJECTED");
            return;
        }
        Harmony? patches = null;
        try
        {
            // Source: Flaaax/STS2-source@706ef1c9, v0.111.0
            // ActionQueueSynchronizer.RequestEnqueue(GameAction) is public.
            // Its implementation may defer the request or cancel the
            // eventual queue item. Prefix observes the original arguments;
            // it never bypasses validation or changes the action.
            MethodInfo target = AccessTools.Method(typeof(ActionQueueSynchronizer),
                nameof(ActionQueueSynchronizer.RequestEnqueue),
                [typeof(GameAction)])
                ?? throw new MissingMethodException(
                    "ActionQueueSynchronizer.RequestEnqueue(GameAction)");
            MethodInfo prefix = typeof(NativeV111InputHooks).GetMethod(
                nameof(PrefixRequestEnqueue),
                BindingFlags.NonPublic | BindingFlags.Static)!;
            patches = new Harmony(Owner);
            patches.Patch(target, prefix: new HarmonyMethod(prefix));
            _patches = patches;
            GD.Print("[Sts2ReferenceMod] INPUT_HOOK_STATUS=REQUEST_SOURCE_INSTALLED;" +
                " source=ActionQueueSynchronizer.RequestEnqueue;" +
                " acceptance=UNVERIFIED; input_injection=DISABLED");
        }
        catch (Exception error)
        {
            try { patches?.UnpatchAll(Owner); }
            catch (Exception) { }
            GD.PushError("[Sts2ReferenceMod] INPUT_HOOK_STATUS=UNAVAILABLE;" +
                " reason=" + error.GetType().Name);
        }
    }

    // Harmony uses the original parameter's name (action).
    // A void prefix does not skip or mutate the original method.
    private static void PrefixRequestEnqueue(GameAction action)
    {
        try { NativeV111PassiveBridge.NativeInputRequested(action); }
        catch (Exception error)
        {
            NativeV111PassiveBridge.NativeInputHookFailure(
                error.GetType().Name);
        }
    }
}
#endif
