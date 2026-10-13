#if STS2_NATIVE
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using Sts2Emulator.Core;
using Sts2ReferenceMod.Trace;
using NativeRng = MegaCrit.Sts2.Core.Random.Rng;

namespace Sts2ReferenceMod;

/// <summary>
/// Source-local card order oracle. The initial shuffle can execute before
/// the first Inspector frame: preserve its source evidence in a bounded
/// pre-attachment buffer, then emit it after the action-sidecar opens.
/// A recycle shuffle is async; its Fisher-Yates and ModifyShuffleOrder
/// execute synchronously before the first await in the pinned v0.111 source.
/// The diagnostic stage is explicitly pre-async-card-movement.
/// </summary>
internal static class NativeV111ShuffleHooks
{
    private sealed class Frame
    {
        internal readonly long Id;
        internal readonly string Kind;
        internal readonly NativeRng Rng;
        internal readonly string[] OrderedInput;
        internal readonly string[] CardModels;
        internal readonly Dictionary<CardModel, string> Tokens;
        internal readonly IDisposable RngScope;
        internal readonly List<NativeShuffleChoiceV111> Choices = [];
        internal string[]? PreHook;
        internal string[]? PostHook;
        internal bool Closed;

        internal Frame(long id, string kind, NativeRng rng,
            List<CardModel> orderedInput)
        {
            Id = id;
            Kind = kind;
            Rng = rng;
            Tokens = new Dictionary<CardModel, string>(
                ReferenceEqualityComparer.Instance);
            // Card identities here are intentionally operation-local. The
            // native CardModel reference persists through Fisher-Yates.
            for (int i = 0; i < orderedInput.Count; i++)
                Tokens.Add(orderedInput[i], "card:" + i);
            OrderedInput = ToTokens(orderedInput);
            CardModels = orderedInput.Select(c => c.Id.Entry).ToArray();
            RngScope = NativeRngCallsiteScopesV111.Enter(
                NativeFisherYatesV111.SiteId,
                NativeFisherYatesV111.StreamId, "player:0");
        }

        internal string[] ToTokens(IEnumerable<CardModel> cards) =>
            cards.Select(c => Tokens.TryGetValue(c, out var id)
                ? id : "UNKNOWN_CARD").ToArray();
    }

    [ThreadStatic] private static Stack<Frame>? _scopes;
    private static readonly List<(
        NativeShuffleOperationEvidenceV111 Evidence,
        NativeRngBoundaryEvidenceV111[] PrimitiveCalls)> BufferedInitial = [];
    private const int MaxBufferedInitial = 16;
    private static long _nextOperationId;
    private static bool _observationFailed;
    internal static bool HasActiveSourceScope => _scopes is { Count: > 0 };

    internal static bool TryInstall(Harmony harmony)
    {
        const BindingFlags methods = BindingFlags.Static | BindingFlags.NonPublic;
        try
        {
            var initial = AccessTools.Method(typeof(CardPile),
                nameof(CardPile.RandomizeOrderInternal),
                [typeof(Player), typeof(NativeRng),
                    typeof(MegaCrit.Sts2.Core.Combat.CombatState)])
                ?? throw new MissingMethodException("CardPile.RandomizeOrderInternal");
            var recycle = AccessTools.Method(typeof(CardPileCmd),
                nameof(CardPileCmd.Shuffle),
                [typeof(PlayerChoiceContext), typeof(Player)])
                ?? throw new MissingMethodException("CardPileCmd.Shuffle");
            var modifier = AccessTools.Method(typeof(Hook),
                nameof(Hook.ModifyShuffleOrder),
                [typeof(MegaCrit.Sts2.Core.Combat.ICombatState),
                 typeof(Player), typeof(List<CardModel>), typeof(bool)])
                ?? throw new MissingMethodException("Hook.ModifyShuffleOrder");

            MethodInfo HookMethod(string name) =>
                typeof(NativeV111ShuffleHooks).GetMethod(name, methods)
                ?? throw new MissingMethodException(name);

            harmony.Patch(initial,
                prefix: new HarmonyMethod(HookMethod(nameof(BeginInitial))),
                postfix: new HarmonyMethod(HookMethod(nameof(EndInitial))),
                finalizer: new HarmonyMethod(HookMethod(nameof(FinishInitialException))));
            harmony.Patch(recycle,
                prefix: new HarmonyMethod(HookMethod(nameof(BeginRecycle))),
                postfix: new HarmonyMethod(HookMethod(nameof(EndRecycle))),
                finalizer: new HarmonyMethod(HookMethod(nameof(FinishRecycleException))));
            harmony.Patch(modifier,
                prefix: new HarmonyMethod(HookMethod(nameof(BeforeModifyOrder))),
                postfix: new HarmonyMethod(HookMethod(nameof(AfterModifyOrder))));
            return true;
        }
        catch (Exception error)
        {
            // Patches may be partially installed. Any detected shuffle
            // observation failure disables the entire semantic observer.
            _observationFailed = true;
            Godot.GD.PushError(
                "[Sts2ReferenceMod] SHUFFLE_HOOK_STATUS=UNAVAILABLE; " +
                error.GetType().Name);
            return false;
        }
    }

    internal static void FlushBufferedInitial()
    {
        var captured = BufferedInitial.ToArray();
        BufferedInitial.Clear();
        foreach (var (evidence, primitives) in captured)
        {
            foreach (var primitive in primitives)
                NativeV111PassiveBridge.NativeRngBoundary(primitive);
            NativeV111PassiveBridge.NativeShuffleOperation(evidence);
        }
    }

    private static Frame? Enter(string kind, NativeRng rng,
        List<CardModel> ordered)
    {
        if (_observationFailed) return null;
        try
        {
            var frame = new Frame(
                System.Threading.Interlocked.Increment(ref _nextOperationId),
                kind, rng, ordered);
            (_scopes ??= new Stack<Frame>()).Push(frame);
            return frame;
        }
        catch (Exception error)
        {
            Failure(error);
            return null;
        }
    }

    private static void BeginInitial(
        CardPile __instance, NativeRng rng, out Frame? __state)
    {
        __state = Enter("initial", rng, __instance.Cards.ToList());
    }

    private static void EndInitial(Frame? __state) => Finish(__state);

    private static Exception? FinishInitialException(
        Exception? __exception, Frame? __state)
    {
        Finish(__state, __exception is not null);
        return __exception;
    }

    private static void BeginRecycle(Player player, out Frame? __state)
    {
        __state = null;
        if (player.PlayerCombatState is null) return;
        try
        {
            // Reproduce CardPileCmd.Shuffle source collection and stable
            // pre-sort exactly: discard.ToList(); AddRange(draw.ToHashSet());
            // StableShuffle sorts the assembled source list before RNG.
            var candidates = player.PlayerCombatState.DiscardPile.Cards.ToList();
            candidates.AddRange(
                player.PlayerCombatState.DrawPile.Cards.ToHashSet());
            candidates.Sort();
            __state = Enter("recycle", player.RunState.Rng.Shuffle,
                candidates);
        }
        catch (Exception error) { Failure(error); }
    }

    private static void EndRecycle(Frame? __state) => Finish(__state);

    private static Exception? FinishRecycleException(
        Exception? __exception, Frame? __state)
    {
        Finish(__state, __exception is not null);
        return __exception;
    }

    private static void BeforeModifyOrder(
        List<CardModel> cards, out string[]? __state)
    {
        __state = null;
        var f = Current;
        if (f is null || f.Closed) return;
        try
        {
            __state = f.ToTokens(cards);
            f.PreHook = __state;
        }
        catch (Exception error) { Failure(error); }
    }

    private static void AfterModifyOrder(
        List<CardModel> cards, string[]? __state)
    {
        var f = Current;
        if (f is null || f.Closed || __state is null) return;
        try { f.PostHook = f.ToTokens(cards); }
        catch (Exception error) { Failure(error); }
    }

    private static Frame? Current =>
        _scopes is { Count: > 0 } ? _scopes.Peek() : null;

    /// <summary>
    /// Called only by the actual Rng.NextInt postfix. It observes the
    /// ORIGINAL selected integer and never takes an additional RNG draw.
    /// </summary>
    internal static void ObserveChoice(NativeRng rng,
        int bound, int selectedIndex,
        NativeRngSnapshot before, NativeRngSnapshot after)
    {
        var frame = Current;
        if (frame is null || frame.Closed
            || !ReferenceEquals(frame.Rng, rng)) return;
        try
        {
            frame.Choices.Add(new NativeShuffleChoiceV111(
                bound, selectedIndex, before, after));
        }
        catch (Exception error) { Failure(error); }
    }

    private static void Finish(Frame? frame, bool aborted = false)
    {
        if (frame is null || frame.Closed) return;
        frame.Closed = true;
        try
        {
            if (!ReferenceEquals(Current, frame))
                throw new InvalidOperationException("Shuffle scopes out of order.");
            _scopes!.Pop();
            frame.RngScope.Dispose();
            if (aborted || _observationFailed) return;
            if (frame.PreHook is null || frame.PostHook is null)
                throw new InvalidOperationException(
                    "Native shuffle ended before ModifyShuffleOrder completed.");
            var evidence = new NativeShuffleOperationEvidenceV111(
                frame.Id, frame.Kind, frame.OrderedInput,
                frame.Choices.ToArray(), frame.PreHook, frame.PostHook,
                frame.CardModels);
            evidence.Validate();
            if (frame.Kind == "initial" && !NativeV111PassiveBridge.HasActionSidecar)
            {
                if (BufferedInitial.Count >= MaxBufferedInitial)
                    throw new InvalidOperationException(
                        "Initial shuffle buffer capacity exceeded.");
                // Each source call has a full pre/post cursor. The original
                // action log does not yet exist at the native combat entry,
                // so flush these when the first combat sidecar is opened.
                var calls = frame.Choices.Select((choice, index) =>
                    new NativeRngBoundaryEvidenceV111(
                        NativeFisherYatesV111.SiteId,
                        NativeFisherYatesV111.StreamId,
                        "player:0", index, choice.BoundedCount,
                        choice.SelectedIndex, choice.CursorBefore,
                        choice.CursorAfter)).ToArray();
                BufferedInitial.Add((evidence, calls));
            }
            else
            {
                NativeV111PassiveBridge.NativeShuffleOperation(evidence);
            }
        }
        catch (Exception error) { Failure(error); }
    }

    private static void Failure(Exception error)
    {
        if (_observationFailed) return;
        _observationFailed = true;
        try { NativeV111PassiveBridge.NativeRngHookFailure(
            "shuffle_observation_" + error.GetType().Name); }
        catch { /* Observer exceptions must not interrupt native play. */ }
    }
}
#endif
