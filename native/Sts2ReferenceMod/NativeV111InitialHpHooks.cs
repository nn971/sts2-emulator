#if STS2_NATIVE
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Random;
using Sts2Emulator.Core;
using Sts2ReferenceMod.Trace;
using NativeRng = MegaCrit.Sts2.Core.Random.Rng;

namespace Sts2ReferenceMod;

/// <summary>
/// Native combat creature initialization observer. A pinned v0.111 source
/// SetUniqueMonsterHpValue call always makes one named run.niche NextInt
/// using the enumerable HashSet of unused HPs (or a fallback full range).
/// It usually runs before the first Inspector frame; buffer bounded source
/// evidence until the combat recorder exists.
/// </summary>
internal static class NativeV111InitialHpHooks
{
    private sealed class Frame
    {
        internal readonly long Ordinal;
        internal readonly NativeRng Rng;
        internal readonly int[] Eligible;
        internal readonly string MonsterId;
        internal readonly bool Exhausted;
        internal readonly IDisposable Scope;
        internal readonly List<NativeShuffleChoiceV111> Draws = [];
        internal bool Closed;

        internal Frame(long ordinal, NativeRng rng,
            string monsterId, int[] eligible, bool exhausted)
        {
            Ordinal = ordinal;
            Rng = rng;
            MonsterId = monsterId;
            Eligible = eligible;
            Exhausted = exhausted;
            Scope = NativeRngCallsiteScopesV111.Enter(
                "combat.initial_unique_hp", "run.niche",
                "monster_creation:" + ordinal);
        }
    }

    [ThreadStatic] private static Stack<Frame>? _scopes;
    private static readonly List<NativeInitialHpRollEvidenceV111> Buffered = [];
    private static long _nextOrdinal;
    private static bool _installed;
    private static bool _failed;
    internal static bool HasActiveSourceScope => _scopes is { Count: > 0 };
    internal static bool IsInstalled => _installed;

    internal static bool TryInstall(Harmony harmony)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        try
        {
            var method = AccessTools.Method(typeof(Creature),
                nameof(Creature.SetUniqueMonsterHpValue),
                [typeof(IReadOnlyList<Creature>), typeof(NativeRng)])
                ?? throw new MissingMethodException(
                    "Creature.SetUniqueMonsterHpValue");
            var cls = typeof(NativeV111InitialHpHooks);
            harmony.Patch(method,
                prefix: new HarmonyMethod(cls.GetMethod(nameof(Begin), flags)!),
                postfix: new HarmonyMethod(cls.GetMethod(nameof(End), flags)!),
                finalizer: new HarmonyMethod(cls.GetMethod(nameof(Finalizer), flags)!));
            _installed = true;
            return true;
        }
        catch (Exception error)
        {
            _failed = true;
            Godot.GD.PushError("[Sts2ReferenceMod] INITIAL_HP_SOURCE_HOOK=UNAVAILABLE;" +
                " reason=" + error.GetType().Name);
            return false;
        }
    }

    private static void Begin(
        Creature __instance, IReadOnlyList<Creature> creaturesOnSide,
        NativeRng rng, out Frame? __state)
    {
        __state = null;
        if (_failed || __instance.Monster is null) return;
        try
        {
            int minHp = __instance.Monster.MinInitialHp;
            int maxHp = __instance.Monster.MaxInitialHp;
            var all = Enumerable.Range(minHp, checked(maxHp - minHp + 1));
            // Exact source HashSet operation/iteration order, rather than
            // assuming the numerically sorted candidates are equivalent.
            var eligible = all.ToHashSet();
            eligible.ExceptWith(creaturesOnSide
                .Where(c => !ReferenceEquals(c, __instance))
                .Select(c => c.MaxHp));
            bool exhausted = eligible.Count == 0;
            var values = exhausted ? all.ToArray() : eligible.ToArray();
            if (values.Length == 0)
                throw new InvalidOperationException("Monster initial HP pool empty.");
            var frame = new Frame(
                System.Threading.Interlocked.Increment(ref _nextOrdinal),
                rng, __instance.Monster.Id.Entry, values, exhausted);
            (_scopes ??= new Stack<Frame>()).Push(frame);
            __state = frame;
        }
        catch (Exception error) { Failure(error); }
    }

    internal static void ObserveChoice(NativeRng rng, int bound, int selected,
        NativeRngSnapshot before, NativeRngSnapshot after)
    {
        if (_failed || _scopes is not { Count: > 0 }) return;
        var frame = _scopes.Peek();
        if (frame.Closed || !ReferenceEquals(frame.Rng, rng)) return;
        try
        {
            frame.Draws.Add(new NativeShuffleChoiceV111(
                bound, selected, before, after));
        }
        catch (Exception error) { Failure(error); }
    }

    private static void End(Creature __instance, Frame? __state) =>
        Complete(__instance, __state);

    private static Exception? Finalizer(
        Creature __instance, Exception? __exception, Frame? __state)
    {
        Complete(__instance, __state, __exception is not null);
        return __exception;
    }

    private static void Complete(Creature creature, Frame? frame,
        bool aborted = false)
    {
        if (frame is null || frame.Closed) return;
        frame.Closed = true;
        try
        {
            if (!ReferenceEquals(_scopes?.Peek(), frame))
                throw new InvalidOperationException("Monster initial HP scopes out of order.");
            _scopes!.Pop();
            frame.Scope.Dispose();
            if (aborted || _failed) return;
            var draw = frame.Draws.Single();
            var evidence = new NativeInitialHpRollEvidenceV111(
                frame.Ordinal, frame.MonsterId, frame.Eligible,
                draw.SelectedIndex, creature.MaxHp, frame.Exhausted,
                draw.CursorBefore, draw.CursorAfter);
            if (draw.BoundedCount != frame.Eligible.Length)
                throw new InvalidOperationException(
                    "Native unique HP bound differs from source eligible pool.");
            evidence.Validate();
            if (NativeV111PassiveBridge.HasActionSidecar)
                NativeV111PassiveBridge.NativeInitialHpRoll(evidence);
            else
            {
                if (Buffered.Count >= 32)
                    throw new InvalidOperationException(
                        "Initial HP source evidence buffer exhausted.");
                Buffered.Add(evidence);
            }
        }
        catch (Exception error) { Failure(error); }
    }

    internal static void FlushBuffered()
    {
        var entries = Buffered.ToArray();
        Buffered.Clear();
        foreach (var evidence in entries)
        {
            NativeV111PassiveBridge.NativeRngBoundary(
                new NativeRngBoundaryEvidenceV111(
                    "combat.initial_unique_hp", "run.niche",
                    "monster_creation:" + evidence.CreationOrdinal, 0,
                    evidence.OrderedEligibleHp.Length, evidence.SelectedIndex,
                    evidence.CursorBefore, evidence.CursorAfter));
            NativeV111PassiveBridge.NativeInitialHpRoll(evidence);
        }
    }

    private static void Failure(Exception error)
    {
        if (_failed) return;
        _failed = true;
        try { NativeV111PassiveBridge.NativeRngHookFailure(
            "initial_hp_observation_" + error.GetType().Name); }
        catch { /* Observer only, preserve native exception behavior. */ }
    }
}
#endif
