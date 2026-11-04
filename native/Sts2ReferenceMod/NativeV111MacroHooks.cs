#if STS2_NATIVE
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace Sts2ReferenceMod;

/// <summary>
/// Read-only run-wide macro callbacks. Hooked source methods still execute
/// exactly once; no action, reward generation or RNG method is invoked.
/// </summary>
internal static class NativeV111MacroHooks
{
    private const string Owner = "nn971.sts2-inspector.native-macro-v111";
    private static Harmony? _patches;
    internal static bool IsInstalled => _patches is not null;

    internal static void TryInstall()
    {
        if (IsInstalled ||
            Environment.GetEnvironmentVariable("STS2_FIDELITY_MACRO_HOOK") != "1")
            return;
        if (!NativeV111PassiveBridge.ReadyForNativeRngHook)
        {
            GD.PushError("[Sts2ReferenceMod] MACRO_HOOK_STATUS=PIN_REJECTED");
            return;
        }
        Harmony? patches = null;
        try
        {
            patches = new Harmony(Owner);
            Patch(patches, typeof(RewardsSetSynchronizer),
                nameof(RewardsSetSynchronizer.BeginRewardsSet),
                [typeof(RewardsSet)], nameof(PostfixBeginRewards));
            Patch(patches, typeof(RewardsSetSynchronizer),
                nameof(RewardsSetSynchronizer.SelectLocalReward),
                [typeof(Reward)], nameof(PrefixSelectReward), prefix: true);
            Patch(patches, typeof(RewardsSetSynchronizer),
                nameof(RewardsSetSynchronizer.SkipLocalRewardsSet),
                Type.EmptyTypes, nameof(PrefixSkipRewards), prefix: true);
            Patch(patches, typeof(RunManager),
                nameof(RunManager.OnEnded), [typeof(bool)],
                nameof(PostfixRunEnded));
            // Observe the original getter *return*, never evaluate a
            // price hook from Inspector-owned code or add RNG draws.
            var costGetter = AccessTools.PropertyGetter(
                typeof(MerchantEntry), nameof(MerchantEntry.Cost))
                ?? throw new MissingMethodException("MerchantEntry.get_Cost");
            patches.Patch(costGetter, postfix: new HarmonyMethod(
                typeof(NativeV111MacroHooks).GetMethod(
                    nameof(PostfixShopCost),
                    BindingFlags.NonPublic | BindingFlags.Static)!));
            _patches = patches;
            GD.Print("[Sts2ReferenceMod] MACRO_HOOK_STATUS=PINNED_SOURCE_INSTALLED;" +
                " hook_sites=5; native_gameplay_mutated=false");
        }
        catch (Exception error)
        {
            try { patches?.UnpatchAll(Owner); }
            catch (Exception) { }
            GD.PushError("[Sts2ReferenceMod] MACRO_HOOK_STATUS=UNAVAILABLE;" +
                " reason=" + error.GetType().Name);
        }
    }

    private static void Patch(Harmony patches, Type targetType,
        string targetName, Type[] arguments, string callbackName,
        bool prefix = false)
    {
        var target = AccessTools.Method(targetType, targetName, arguments)
            ?? throw new MissingMethodException(targetType.FullName, targetName);
        var callback = typeof(NativeV111MacroHooks).GetMethod(
            callbackName, BindingFlags.NonPublic | BindingFlags.Static)!;
        patches.Patch(target,
            prefix: prefix ? new HarmonyMethod(callback) : null,
            postfix: prefix ? null : new HarmonyMethod(callback));
    }

    private static void PostfixBeginRewards(RewardsSet set)
    {
        try { NativeV111PassiveBridge.NativeMacroRewardOffered(set); }
        catch (Exception error) { NativeV111PassiveBridge.NativeMacroFailure(
            "reward_offer", error.GetType().Name); }
    }

    private static void PrefixSelectReward(Reward reward)
    {
        try { NativeV111PassiveBridge.NativeMacroRewardSelected(reward); }
        catch (Exception error) { NativeV111PassiveBridge.NativeMacroFailure(
            "reward_selection", error.GetType().Name); }
    }

    private static void PrefixSkipRewards()
    {
        try { NativeV111PassiveBridge.NativeMacroRewardsSkipped(); }
        catch (Exception error) { NativeV111PassiveBridge.NativeMacroFailure(
            "reward_skip", error.GetType().Name); }
    }

    private static void PostfixShopCost(MerchantEntry __instance, int __result)
    {
        try { NativeV111PassiveBridge.NativeMacroShopCostObserved(
            __instance, __result); }
        catch (Exception error) { NativeV111PassiveBridge.NativeMacroFailure(
            "shop_cost_getter", error.GetType().Name); }
    }

    private static void PostfixRunEnded(bool isVictory)
    {
        try { NativeV111PassiveBridge.NativeMacroRunEnded(isVictory); }
        catch (Exception error) { NativeV111PassiveBridge.NativeMacroFailure(
            "run_ended", error.GetType().Name); }
    }
}
#endif
