#if STS2_NATIVE
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
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
    private const string ChoiceOwner = Owner + ".choice-flow";
    private const string HandMenuOwner = Owner + ".hand-menu";
    private static Harmony? _patches;
    private static Harmony? _choicePatches;
    private static Harmony? _handMenuPatches;
    internal static bool IsInstalled => _patches is not null;
    internal static bool HasHandMenuHook { get; private set; }
    internal static bool HasHandChoiceFlowHook { get; private set; }
    internal static bool HasManualPlayHook { get; private set; }
    internal static bool HasUiPlayBeginHook { get; private set; }

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
            HasHandMenuHook = TryPatchHandMenu();
            HasHandChoiceFlowHook = TryPatchHandChoiceFlow();
            HasManualPlayHook = TryPatchManualPlay(patches);
            HasUiPlayBeginHook = TryPatchUiPlayBegin(patches);
            _patches = patches;
            GD.Print("[Sts2ReferenceMod] INPUT_HOOK_STATUS=REQUEST_SOURCE_INSTALLED;" +
                " source=ActionQueueSynchronizer.RequestEnqueue;" +
                " acceptance=UNVERIFIED; input_injection=DISABLED;" +
                " hand_menu_source=" + (HasHandMenuHook ? "INSTALLED" : "UNAVAILABLE") +
                "; hand_choice_id_source=" + (HasHandChoiceFlowHook ? "INSTALLED" : "UNAVAILABLE") +
                "; manual_play_source=" + (HasManualPlayHook ? "INSTALLED" : "UNAVAILABLE") +
                "; ui_play_begin_source=" + (HasUiPlayBeginHook ? "INSTALLED" : "UNAVAILABLE"));
        }
        catch (Exception error)
        {
            try { patches?.UnpatchAll(Owner); }
            catch (Exception) { }
            try { _choicePatches?.UnpatchAll(ChoiceOwner); }
            catch (Exception) { }
            try { _handMenuPatches?.UnpatchAll(HandMenuOwner); }
            catch (Exception) { }
            _choicePatches = null;
            _handMenuPatches = null;
            HasHandChoiceFlowHook = false;
            HasHandMenuHook = false;
            HasManualPlayHook = false;
            HasUiPlayBeginHook = false;
            GD.PushError("[Sts2ReferenceMod] INPUT_HOOK_STATUS=UNAVAILABLE;" +
                " reason=" + error.GetType().Name);
        }
    }

    private static bool TryPatchHandMenu()
    {
        Harmony? instance = null;
        try
        {
            // The original async method initializes the UI and runs the
            // native filter. Its postfix captures the initial menu. Subsequent
            // calls to the original visibility updater produce *revisions*.
            // No observer invokes the source filter a second time.
            var start = AccessTools.Method(typeof(NPlayerHand),
                nameof(NPlayerHand.SelectCards),
                [typeof(CardSelectorPrefs), typeof(Func<CardModel, bool>),
                 typeof(AbstractModel), typeof(NPlayerHand.Mode)])
                ?? throw new MissingMethodException("NPlayerHand.SelectCards");
            var refresh = AccessTools.Method(typeof(NPlayerHand),
                "UpdateSelectModeCardVisibility", Type.EmptyTypes)
                ?? throw new MissingMethodException(
                    "NPlayerHand.UpdateSelectModeCardVisibility");
            var close = AccessTools.Method(typeof(NPlayerHand),
                "AfterCardsSelected", [typeof(AbstractModel)])
                ?? throw new MissingMethodException("NPlayerHand.AfterCardsSelected");
            var exit = AccessTools.Method(typeof(NPlayerHand),
                nameof(NPlayerHand._ExitTree), Type.EmptyTypes)
                ?? throw new MissingMethodException("NPlayerHand._ExitTree");
            instance = new Harmony(HandMenuOwner);
            instance.Patch(start, postfix: new HarmonyMethod(
                typeof(NativeV111InputHooks).GetMethod(
                    nameof(PostfixHandSelect),
                    BindingFlags.NonPublic | BindingFlags.Static)!));
            instance.Patch(refresh, postfix: new HarmonyMethod(
                typeof(NativeV111InputHooks).GetMethod(
                    nameof(PostfixHandVisibilityRefresh),
                    BindingFlags.NonPublic | BindingFlags.Static)!));
            instance.Patch(close, postfix: new HarmonyMethod(
                typeof(NativeV111InputHooks).GetMethod(
                    nameof(PostfixHandSelectionClosed),
                    BindingFlags.NonPublic | BindingFlags.Static)!));
            instance.Patch(exit, prefix: new HarmonyMethod(
                typeof(NativeV111InputHooks).GetMethod(
                    nameof(PrefixHandExitTree),
                    BindingFlags.NonPublic | BindingFlags.Static)!));
            _handMenuPatches = instance;
            return true;
        }
        catch (Exception error)
        {
            try { instance?.UnpatchAll(HandMenuOwner); }
            catch (Exception) { }
            GD.PushError("[Sts2ReferenceMod] HAND_MENU_HOOK_STATUS=UNAVAILABLE;" +
                " reason=" + error.GetType().Name);
            return false;
        }
    }

    private static bool TryPatchHandChoiceFlow()
    {
        Harmony? instance = null;
        try
        {
            // CardSelectCmd.FromHand reserves a synchronizer choice ID
            // before opening NPlayerHand.SelectCards. Patch both source
            // methods to carry the actual returned ID across any awaits.
            var fromHand = AccessTools.Method(typeof(CardSelectCmd),
                nameof(CardSelectCmd.FromHand),
                [typeof(PlayerChoiceContext), typeof(Player),
                 typeof(CardSelectorPrefs), typeof(Func<CardModel, bool>),
                 typeof(AbstractModel)])
                ?? throw new MissingMethodException("CardSelectCmd.FromHand");
            var reserve = AccessTools.Method(typeof(PlayerChoiceSynchronizer),
                nameof(PlayerChoiceSynchronizer.ReserveChoiceId),
                [typeof(Player)])
                ?? throw new MissingMethodException(
                    "PlayerChoiceSynchronizer.ReserveChoiceId");
            instance = new Harmony(ChoiceOwner);
            instance.Patch(fromHand,
                prefix: new HarmonyMethod(typeof(NativeV111InputHooks)
                    .GetMethod(nameof(PrefixFromHand),
                        BindingFlags.NonPublic | BindingFlags.Static)!),
                postfix: new HarmonyMethod(typeof(NativeV111InputHooks)
                    .GetMethod(nameof(PostfixFromHand),
                        BindingFlags.NonPublic | BindingFlags.Static)!));
            instance.Patch(reserve,
                postfix: new HarmonyMethod(typeof(NativeV111InputHooks)
                    .GetMethod(nameof(PostfixReserveChoiceId),
                        BindingFlags.NonPublic | BindingFlags.Static)!));
            _choicePatches = instance;
            return true;
        }
        catch (Exception error)
        {
            try { instance?.UnpatchAll(ChoiceOwner); }
            catch (Exception) { }
            GD.PushError("[Sts2ReferenceMod] CHOICE_FLOW_HOOK_STATUS=UNAVAILABLE;" +
                " reason=" + error.GetType().Name);
            return false;
        }
    }

    // Harmony state spans precisely the synchronous portion of the async
    // FromHand wrapper. Captured async continuations inherit the token.
    private static void PrefixFromHand(
        Player player, out NativeHandChoiceFlow.Scope? __state)
    {
        __state = null;
        try
        {
            if (NativeV111PassiveBridge.HasActionSidecar)
                __state = NativeHandChoiceFlow.BeginFromHand(player.NetId);
        }
        catch (Exception error)
        {
            NativeV111PassiveBridge.NativeInputHookFailure(
                error.GetType().Name);
        }
    }

    private static void PostfixFromHand(NativeHandChoiceFlow.Scope? __state)
    {
        try { __state?.Dispose(); }
        catch (Exception error)
        {
            NativeV111PassiveBridge.NativeInputHookFailure(
                error.GetType().Name);
        }
    }

    private static void PostfixReserveChoiceId(Player player, uint __result)
    {
        try
        {
            if (NativeV111PassiveBridge.HasActionSidecar)
                NativeHandChoiceFlow.Reserved(player.NetId, __result);
        }
        catch (Exception error)
        {
            NativeV111PassiveBridge.NativeInputHookFailure(
                error.GetType().Name);
        }
    }

    private static bool TryPatchUiPlayBegin(Harmony patches)
    {
        try
        {
            // The source paths for click and shortcut call this private
            // method only after their respective hand-action UI gates.
            // This is a UI interaction start, not confirmed card play.
            var target = AccessTools.Method(typeof(NPlayerHand),
                "StartCardPlay",
                [typeof(NHandCardHolder), typeof(bool)]);
            if (target is null)
                throw new MissingMethodException("NPlayerHand.StartCardPlay");
            patches.Patch(target, prefix: new HarmonyMethod(
                typeof(NativeV111InputHooks).GetMethod(
                    nameof(PrefixUiPlayBegin),
                    BindingFlags.NonPublic | BindingFlags.Static)!));
            return true;
        }
        catch (Exception error)
        {
            GD.PushError("[Sts2ReferenceMod] UI_PLAY_BEGIN_HOOK_STATUS=UNAVAILABLE;" +
                " reason=" + error.GetType().Name);
            return false;
        }
    }

    private static void PrefixUiPlayBegin(
        NHandCardHolder holder, bool startedViaShortcut)
    {
        if (!NativeV111PassiveBridge.HasActionSidecar) return;
        try
        {
            var card = holder.CardNode?.Model;
            if (card is null) return;
            long? id = NetCombatCardDb.Instance.TryGetCardId(
                card, out uint nativeId) ? (long)nativeId : null;
            NativeV111PassiveBridge.NativeUiPlayBegin(
                id, card.Id.Entry, startedViaShortcut);
        }
        catch (Exception error)
        {
            NativeV111PassiveBridge.NativeInputHookFailure(
                error.GetType().Name);
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

    /// <summary>
    /// Projects only the game's existing holder.Visible flags and registered
    /// combat card IDs. In peek mode, visible holders are not selectable:
    /// omit them from the selectable candidate set.
    /// </summary>
    private static (int HolderCount, int MissingIds,
        List<NativeHandMenuOption> Options) ReadNativeHandVisibility(
            NPlayerHand hand, bool peekMode)
    {
        var holders = hand.CardHolderContainer.GetChildren()
            .OfType<NHandCardHolder>().ToArray();
        var options = new List<NativeHandMenuOption>();
        int missingIds = 0;
        if (peekMode) return (holders.Length, missingIds, options);
        for (int i = 0; i < holders.Length; i++)
        {
            var holder = holders[i];
            if (!holder.Visible || holder.CardNode?.Model is not CardModel card)
                continue;
            // Pinned native NetCombatCardDb.TryGetCardId is a dictionary lookup.
            if (!NetCombatCardDb.Instance.TryGetCardId(card, out uint id))
            {
                ++missingIds;
                continue;
            }
            options.Add(new NativeHandMenuOption(
                (long)id, card.Id.Entry, i));
        }
        return (holders.Length, missingIds, options);
    }

    // Captures the initial selection after the game's first native
    // UpdateSelectModeCardVisibility has finished. The exact choice ID, when
    // available, comes from the earlier same-async-flow source reservation.
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
            var observed = ReadNativeHandVisibility(__instance, false);
            NativeHandChoiceFlow.Binding flow = NativeHandChoiceFlow.Current();
            NativeV111PassiveBridge.NativeHandMenu(
                mode.ToString(), source?.Id.Entry,
                prefs.MinSelect, prefs.MaxSelect,
                prefs.RequireManualConfirmation, observed.HolderCount,
                observed.MissingIds, observed.Options,
                flow.OwnerId, flow.ChoiceId, flow.Status, __instance);
        }
        catch (Exception error)
        {
            NativeV111PassiveBridge.NativeInputHookFailure(
                error.GetType().Name);
        }
    }

    // The original method has already evaluated the real native selection
    // predicate and assigned holder.Visible. This can be called by native
    // hand insertions, state revalidation, and peek-mode transitions.
    private static void PostfixHandVisibilityRefresh(NPlayerHand __instance)
    {
        if (!NativeV111PassiveBridge.HasActionSidecar) return;
        try
        {
            if (!__instance.IsInCardSelection
                || __instance.PeekButton is null
                || __instance.CardHolderContainer is null)
                return;
            bool peeking = __instance.PeekButton.IsPeeking;
            var observed = ReadNativeHandVisibility(__instance, peeking);
            NativeV111PassiveBridge.NativeHandMenuRefresh(
                __instance, __instance.CurrentMode.ToString(), peeking,
                observed.HolderCount, observed.MissingIds, observed.Options);
        }
        catch (Exception error)
        {
            NativeV111PassiveBridge.NativeInputHookFailure(
                error.GetType().Name);
        }
    }

    private static void PostfixHandSelectionClosed(NPlayerHand __instance)
    {
        try
        {
            if (NativeV111PassiveBridge.HasActionSidecar)
                NativeV111PassiveBridge.NativeHandMenuClosed(
                    __instance, "AfterCardsSelected");
        }
        catch (Exception error)
        {
            NativeV111PassiveBridge.NativeInputHookFailure(
                error.GetType().Name);
        }
    }

    private static void PrefixHandExitTree(NPlayerHand __instance)
    {
        try
        {
            if (NativeV111PassiveBridge.HasActionSidecar)
                NativeV111PassiveBridge.NativeHandMenuClosed(
                    __instance, "NativeHandExitTree");
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
