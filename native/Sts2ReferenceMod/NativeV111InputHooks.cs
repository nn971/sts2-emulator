#if STS2_NATIVE
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using Sts2ReferenceMod.Trace;

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
    internal static bool HasHandMenuHook { get; private set; }
    internal static bool HasManualPlayHook { get; private set; }

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
            MethodInfo postfix = typeof(NativeV111InputHooks).GetMethod(
                nameof(PostfixRequestEnqueue),
                BindingFlags.NonPublic | BindingFlags.Static)!;
            patches.Patch(target, prefix: new HarmonyMethod(prefix),
                postfix: new HarmonyMethod(postfix));
            // Optional UI sources. Native RequestEnqueue evidence stays usable
            // if either local game signature has changed since the pinned source.
            HasHandMenuHook = TryPatchHandMenu(patches);
            HasManualPlayHook = TryPatchManualPlay(patches);
            _patches = patches;
            GD.Print("[Sts2ReferenceMod] INPUT_HOOK_STATUS=REQUEST_SOURCE_INSTALLED;" +
                " source=ActionQueueSynchronizer.RequestEnqueue;" +
                " acceptance=UNVERIFIED; input_injection=DISABLED;" +
                " hand_menu_source=" + (HasHandMenuHook ? "INSTALLED" : "UNAVAILABLE") +
                "; manual_play_source=" + (HasManualPlayHook ? "INSTALLED" : "UNAVAILABLE"));
        }
        catch (Exception error)
        {
            try { patches?.UnpatchAll(Owner); }
            catch (Exception) { }
            HasHandMenuHook = false;
            HasManualPlayHook = false;
            GD.PushError("[Sts2ReferenceMod] INPUT_HOOK_STATUS=UNAVAILABLE;" +
                " reason=" + error.GetType().Name);
        }
    }

    private static bool TryPatchHandMenu(Harmony patches)
    {
        try
        {
            // The async method initializes the native hand selection UI,
            // runs UpdateSelectModeCardVisibility (which evaluates the native
            // filter once), then awaits the user's choice. The ordinary method
            // postfix reads the resulting UI; it never invokes the predicate.
            var target = AccessTools.Method(typeof(NPlayerHand),
                nameof(NPlayerHand.SelectCards),
                [typeof(CardSelectorPrefs), typeof(Func<CardModel, bool>),
                 typeof(AbstractModel), typeof(NPlayerHand.Mode)]);
            if (target is null)
                throw new MissingMethodException("NPlayerHand.SelectCards");
            patches.Patch(target, postfix: new HarmonyMethod(
                typeof(NativeV111InputHooks).GetMethod(
                    nameof(PostfixHandSelect),
                    BindingFlags.NonPublic | BindingFlags.Static)!));
            return true;
        }
        catch (Exception error)
        {
            GD.PushError("[Sts2ReferenceMod] HAND_MENU_HOOK_STATUS=UNAVAILABLE;" +
                " reason=" + error.GetType().Name);
            return false;
        }
    }

    private static bool TryPatchManualPlay(Harmony patches)
    {
        try
        {
            var target = AccessTools.Method(typeof(CardModel),
                nameof(CardModel.TryManualPlay), [typeof(Creature)]);
            if (target is null)
                throw new MissingMethodException("CardModel.TryManualPlay");
            patches.Patch(target, postfix: new HarmonyMethod(
                typeof(NativeV111InputHooks).GetMethod(
                    nameof(PostfixManualPlay),
                    BindingFlags.NonPublic | BindingFlags.Static)!));
            return true;
        }
        catch (Exception error)
        {
            GD.PushError("[Sts2ReferenceMod] MANUAL_PLAY_HOOK_STATUS=UNAVAILABLE;" +
                " reason=" + error.GetType().Name);
            return false;
        }
    }

    // This postfix observes the native UI *after* its own hand-card filter
    // and visibility update. Reading the existing holders and looking up
    // preassigned NetCombatCardDb IDs is side-effect-free in pinned v111.
    private static void PostfixHandSelect(
        NPlayerHand __instance, CardSelectorPrefs prefs,
        AbstractModel? source, NPlayerHand.Mode mode)
    {
        if (!NativeV111PassiveBridge.HasActionSidecar) return;
        try
        {
            if (!__instance.IsInCardSelection || __instance.CurrentMode != mode
                || __instance.PeekButton is null
                || __instance.PeekButton.IsPeeking
                || __instance.CardHolderContainer is null)
                return;
            var holders = __instance.CardHolderContainer.GetChildren()
                .OfType<NHandCardHolder>().ToArray();
            var options = new List<NativeHandMenuOption>();
            int missingIds = 0;
            for (int i = 0; i < holders.Length; i++)
            {
                var holder = holders[i];
                if (!holder.Visible || holder.CardNode?.Model is not CardModel card)
                    continue;
                // TryGetCardId is a Dictionary.TryGetValue on an already
                // registered native combat card; it never assigns a new ID.
                if (!NetCombatCardDb.Instance.TryGetCardId(card, out uint id))
                {
                    ++missingIds;
                    continue;
                }
                options.Add(new NativeHandMenuOption(
                    (long)id, card.Id.Entry, i));
            }
            NativeV111PassiveBridge.NativeHandMenu(
                mode.ToString(), source?.Id.Entry,
                prefs.MinSelect, prefs.MaxSelect,
                prefs.RequireManualConfirmation, holders.Length,
                missingIds, options);
        }
        catch (Exception error)
        {
            NativeV111PassiveBridge.NativeInputHookFailure(
                error.GetType().Name);
        }
    }

    // Source-v111 CardModel.TryManualPlay calls CanPlayTargeting and
    // EnqueueManualPlay only on success. True is a manual-play request
    // result; queue admission and legal menu remain separate claims.
    private static void PostfixManualPlay(
        CardModel __instance, Creature? target, bool __result)
    {
        if (!NativeV111PassiveBridge.HasActionSidecar) return;
        try
        {
            long? cardId = NetCombatCardDb.Instance.TryGetCardId(
                __instance, out uint id) ? (long)id : null;
            NativeV111PassiveBridge.NativeManualPlayReturn(
                cardId, __instance.Id.Entry,
                target?.CombatId is uint targetId
                    ? "creature:" + targetId : null,
                __result);
        }
        catch (Exception error)
        {
            NativeV111PassiveBridge.NativeInputHookFailure(
                error.GetType().Name);
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

    // The game's request synchronizer has now returned. In singleplayer,
    // the action queue has either accepted, canceled or deferred this
    // request. This reflects the actual GameAction state at this instant;
    // it does not imply that a full legal input menu was observed.
    private static void PostfixRequestEnqueue(GameAction action)
    {
        try
        {
            NativeV111PassiveBridge.NativeInputRequestOutcome(
                action, action.State.ToString(), action.Id);
        }
        catch (Exception error)
        {
            NativeV111PassiveBridge.NativeInputHookFailure(
                error.GetType().Name);
        }
    }
}
#endif
