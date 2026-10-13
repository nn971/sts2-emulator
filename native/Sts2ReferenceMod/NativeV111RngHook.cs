#if STS2_NATIVE
using System.Reflection;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Entities.Rngs;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using Sts2Emulator.Core;
using Sts2ReferenceMod.Trace;
using NativeRng = MegaCrit.Sts2.Core.Random.Rng;

namespace Sts2ReferenceMod;

/// <summary>
/// Opt-in source-primitive observer for the pinned v0.111 Rng.NextInt overloads.
/// Harmony patches run the original unmodified RNG exactly once: prefixes
/// snapshot ToSerializable, postfixes observe __result and the new snapshot.
/// Captures within a known single-player combat are written to the independent
/// action sidecar. Call-site semantics, contextual streams and pre-attachment
/// combat creation draws remain explicitly unattributed/unobserved.
/// </summary>
internal static class NativeV111RngHook
{
    private readonly record struct CallState(
        bool Active, NativeRngSnapshot? Before, int Min, int Bound);

    private static readonly NativeRngSourceRegistryV111 Owners = new();
    private static Harmony? _patches;
    private static bool _attached;
    private static bool _observationFailed;
    private static int _threadId;
    private static bool _rollMoveScopeInstalled;
    private static bool _doubleHookInstalled;
    private static bool _boolHookInstalled;
    private static readonly List<(string Id, string Owner, NativeRng Rng)> StreamSources = [];
    internal static bool StreamCheckpointsEnabled => IsInstalled &&
        System.Environment.GetEnvironmentVariable("STS2_FIDELITY_RNG_STREAM_CHECKPOINTS") == "1";

    internal static bool IsInstalled => _patches is not null;
    internal static bool HasRollMoveScope => IsInstalled && _rollMoveScopeInstalled;
    internal static bool HasDoubleHook => IsInstalled && _doubleHookInstalled;
    internal static bool HasBoolHook => IsInstalled && _boolHookInstalled;

    internal static void TryInstall()
    {
        if (IsInstalled) return;
        if (System.Environment.GetEnvironmentVariable("STS2_FIDELITY_RNG_HOOK") != "1")
            return;
        if (!NativeV111PassiveBridge.ReadyForNativeRngHook)
        {
            GD.PushError("[Sts2ReferenceMod] RNG_HOOK_STATUS=PIN_OR_SCENE_REJECTED");
            return;
        }
        const BindingFlags methods = BindingFlags.Static | BindingFlags.NonPublic;
        Harmony? harmony = null;
        try
        {
            MethodInfo one = AccessTools.Method(typeof(NativeRng),
                nameof(NativeRng.NextInt), [typeof(int)])
                ?? throw new MissingMethodException("Rng.NextInt(int)");
            MethodInfo two = AccessTools.Method(typeof(NativeRng),
                nameof(NativeRng.NextInt), [typeof(int), typeof(int)])
                ?? throw new MissingMethodException("Rng.NextInt(int,int)");
            var prefixOne = typeof(NativeV111RngHook).GetMethod(
                nameof(PrefixOne), methods)!;
            var postfixOne = typeof(NativeV111RngHook).GetMethod(
                nameof(PostfixOne), methods)!;
            var prefixTwo = typeof(NativeV111RngHook).GetMethod(
                nameof(PrefixTwo), methods)!;
            var postfixTwo = typeof(NativeV111RngHook).GetMethod(
                nameof(PostfixTwo), methods)!;

            harmony = new Harmony("nn971.sts2-inspector.native-rng-v111");
            harmony.Patch(one, prefix: new HarmonyMethod(prefixOne),
                postfix: new HarmonyMethod(postfixOne));
            harmony.Patch(two, prefix: new HarmonyMethod(prefixTwo),
                postfix: new HarmonyMethod(postfixTwo));
            // Source-verified one-step scalar RNG calls, installed as optional
            // patches so a hotfix signature mismatch preserves NextInt capture.
            // Only the zero-argument overloads are hooked: higher-level
            // wrappers and Gaussian rejection compose these source calls.
            _doubleHookInstalled = TryInstallScalar(
                harmony, nameof(NativeRng.NextDouble),
                nameof(PrefixScalar), nameof(PostfixDouble));
            _boolHookInstalled = TryInstallScalar(
                harmony, nameof(NativeRng.NextBool),
                nameof(PrefixScalar), nameof(PostfixBool));
            // Additional source-level causal identity for enemy intentions.
            // MonsterModel.RollMove is synchronous and delegates directly to
            // MoveStateMachine.RollMove(..., RunRng.MonsterAi) in pinned v111.
            // If this optional method signature changed, the two primitive
            // hooks still work and all semantic sites stay UNATTRIBUTED.
            bool rollMoveScopeInstalled = false;
            try
            {
                var target = AccessTools.Method(typeof(MonsterModel),
                    nameof(MonsterModel.RollMove),
                    [typeof(IEnumerable<Creature>)]);
                if (target is not null)
                {
                    harmony.Patch(target,
                        prefix: new HarmonyMethod(typeof(NativeV111RngHook)
                            .GetMethod(nameof(PrefixRollMove), methods)!),
                        postfix: new HarmonyMethod(typeof(NativeV111RngHook)
                            .GetMethod(nameof(PostfixRollMove), methods)!),
                        finalizer: new HarmonyMethod(typeof(NativeV111RngHook)
                            .GetMethod(nameof(FinalizerRollMove), methods)!));
                    rollMoveScopeInstalled = true;
                }
            }
            catch (Exception)
            {
                // Preserve primitive capture when the semantic scope differs
                // in the locally installed hotfix; the log is authoritative.
            }
            var shuffleScopeInstalled = NativeV111ShuffleHooks.TryInstall(harmony);
            var initialHpScopeInstalled = NativeV111InitialHpHooks.TryInstall(harmony);
            _patches = harmony;
            _rollMoveScopeInstalled = rollMoveScopeInstalled;
            GD.Print("[Sts2ReferenceMod] RNG_HOOK_STATUS=INSTALLED_BOUND_INT_OVERLOADS;" +
                " owner_attribution=run_and_player;" +
                " rollmove_source_scope=" +
                (rollMoveScopeInstalled ? "INSTALLED" : "UNAVAILABLE") +
                "; shuffle_source_scope=" +
                (shuffleScopeInstalled ? "INSTALLED" : "UNAVAILABLE") +
                "; initial_hp_source_scope=" +
                (initialHpScopeInstalled ? "INSTALLED" : "UNAVAILABLE") +
                "; double_source_hook=" +
                (_doubleHookInstalled ? "INSTALLED" : "UNAVAILABLE") +
                "; bool_source_hook=" +
                (_boolHookInstalled ? "INSTALLED" : "UNAVAILABLE"));
        }
        catch (Exception error)
        {
            // The game's pinned Harmony exposes UnpatchAll(ownerId),
            // while UnpatchSelf() is absent. Restrict cleanup to our ID so
            // failure during installation cannot remove unrelated game mods.
            try { harmony?.UnpatchAll("nn971.sts2-inspector.native-rng-v111"); }
            catch (Exception) { /* original game must still run */ }
            _patches = null;
            _rollMoveScopeInstalled = false;
            _doubleHookInstalled = false;
            _boolHookInstalled = false;
            GD.PushError("[Sts2ReferenceMod] RNG_HOOK_STATUS=INSTALL_FAILED;" +
                " reason=" + error.GetType().Name + "; exact RNG fidelity NOT_TESTED");
        }
    }

    private static bool TryInstallScalar(Harmony harmony, string methodName,
        string prefixName, string postfixName)
    {
        const BindingFlags methods = BindingFlags.Static | BindingFlags.NonPublic;
        try
        {
            var target = AccessTools.Method(typeof(NativeRng), methodName,
                Type.EmptyTypes) ?? throw new MissingMethodException(
                    "Rng." + methodName + "()");
            harmony.Patch(target,
                prefix: new HarmonyMethod(
                    typeof(NativeV111RngHook).GetMethod(prefixName, methods)!),
                postfix: new HarmonyMethod(
                    typeof(NativeV111RngHook).GetMethod(postfixName, methods)!));
            return true;
        }
        catch (Exception error)
        {
            GD.PushError("[Sts2ReferenceMod] SCALAR_RNG_HOOK_STATUS=UNAVAILABLE;" +
                " method=" + methodName + ";reason=" + error.GetType().Name);
            return false;
        }
    }

    internal static void BeginCombat(MegaCrit.Sts2.Core.Combat.CombatState combat)
    {
        if (!IsInstalled) return;
        _attached = false;
        Owners.Clear();
        StreamSources.Clear();
        _observationFailed = false;
        _threadId = System.Environment.CurrentManagedThreadId;
        // Register only identities actually confirmed by the native run/player
        // RNG sets, not guessed from counter changes. The caller has already
        // established single-player combat and the v0.111 binary pin.
        var player = combat.Players.Single();
        var run = player.RunState.Rng;
        foreach (RunRngType type in Enum.GetValues<RunRngType>())
        {
            var rng = run.GetRng(type);
            var name = "run." + RunName(type);
            Owners.Register(rng, name, "run");
            StreamSources.Add((name, "run", rng));
        }
        foreach (PlayerRngType type in Enum.GetValues<PlayerRngType>())
        {
            var rng = player.PlayerRng.GetRng(type);
            var name = "player." + PlayerName(type);
            Owners.Register(rng, name, "player:0");
            StreamSources.Add((name, "player:0", rng));
        }
        _attached = true;
        CaptureStreamCheckpoint("combat_attach");
    }

    internal static void EndCombat()
    {
        CaptureStreamCheckpoint("combat_end");
        _attached = false;
        Owners.Clear();
        StreamSources.Clear();
    }

    internal static void CaptureStreamCheckpoint(string phase)
    {
        if (!StreamCheckpointsEnabled || !_attached || _observationFailed)
            return;
        if (System.Environment.CurrentManagedThreadId != _threadId)
        {
            ObservationFailure(new InvalidOperationException(
                "RNG checkpoint observed on another native game thread."));
            return;
        }
        try
        {
            var evidence = new NativeRngStreamCheckpointV111(
                phase, StreamSources.Select(s =>
                    new NativeRngStreamCursorV111(
                        s.Id, s.Owner, Snapshot(s.Rng))).ToArray());
            evidence.Validate();
            NativeV111PassiveBridge.NativeRngStreamCheckpoint(evidence);
        }
        catch (Exception error) { ObservationFailure(error); }
    }

    private static NativeRngSnapshot Snapshot(NativeRng rng)
    {
        var data = rng.ToSerializable(); // Game source: pure state copy, zero draws
        return new NativeRngSnapshot(
            data.counter, data.state0, data.state1, data.state2, data.state3);
    }

    private static CallState Capture(NativeRng rng, int min, int max)
    {
        if ((!_attached && !NativeV111ShuffleHooks.HasActiveSourceScope
                && !NativeV111InitialHpHooks.HasActiveSourceScope)
            || _observationFailed
            || (_attached
                && System.Environment.CurrentManagedThreadId != _threadId)
            || max <= min)
            return default;
        long count = (long)max - min;
        if (count is <= 0 or > int.MaxValue) return default;
        try { return new CallState(true, Snapshot(rng), min, (int)count); }
        catch (Exception error) { ObservationFailure(error); return default; }
    }

    // Exact v0.111.0 overload and parameter names are source-audited:
    // Rng.NextInt(int maxExclusive) and NextInt(int minInclusive,int maxExclusive).
    private static void PrefixOne(
        NativeRng __instance, int maxExclusive, out CallState __state) =>
        __state = Capture(__instance, 0, maxExclusive);

    private static void PrefixTwo(
        NativeRng __instance, int minInclusive, int maxExclusive,
        out CallState __state) =>
        __state = Capture(__instance, minInclusive, maxExclusive);

    private static void PostfixOne(
        NativeRng __instance, int __result, CallState __state) =>
        Observe(__instance, __result, __state);

    private static void PostfixTwo(
        NativeRng __instance, int __result, CallState __state) =>
        Observe(__instance, __result, __state);

    private static void PrefixScalar(NativeRng __instance, out CallState __state) =>
        __state = Capture(__instance, 0, 1);

    private static void PostfixDouble(
        NativeRng __instance, double __result, CallState __state) =>
        ObserveScalar(__instance, "unit_double", __result, null, __state);

    private static void PostfixBool(
        NativeRng __instance, bool __result, CallState __state) =>
        ObserveScalar(__instance, "boolean", null, __result, __state);

    private static void PrefixRollMove(
        MonsterModel __instance, out IDisposable? __state)
    {
        __state = null;
        if (!_attached || _observationFailed ||
            System.Environment.CurrentManagedThreadId != _threadId) return;
        try
        {
            var combatId = __instance.Creature.CombatId;
            var owner = combatId.HasValue
                ? "creature:" + combatId.Value
                : "UNATTRIBUTED";
            __state = NativeRngCallsiteScopesV111.Enter(
                "combat.committed_intent.index_draw", "run.monster_ai", owner);
        }
        catch (Exception error) { ObservationFailure(error); }
    }

    private static void PostfixRollMove(IDisposable? __state)
    {
        try { __state?.Dispose(); }
        catch (Exception error) { ObservationFailure(error); }
    }

    private static Exception? FinalizerRollMove(
        Exception? __exception, IDisposable? __state)
    {
        try { __state?.Dispose(); }
        catch (Exception error) { ObservationFailure(error); }
        // Preserve native exception semantics exactly.
        return __exception;
    }

    private static void Observe(NativeRng rng, int result, CallState state)
    {
        if (!state.Active || _observationFailed) return;
        try
        {
            var after = Snapshot(rng);
            NativeV111ShuffleHooks.ObserveChoice(
                rng, state.Bound, checked(result - state.Min),
                state.Before!, after);
            NativeV111InitialHpHooks.ObserveChoice(
                rng, state.Bound, checked(result - state.Min),
                state.Before!, after);
            // Pre-attachment initial shuffles are emitted from a bounded
            // source evidence buffer after the first combat sidecar opens.
            if (!_attached) return;
            var (stream, owner, streamOrdinal) = Owners.Next(rng);
            // Only a source-audited synchronous scope can promote primitive
            // NextInt to a mechanic site and creature actor. Random calls
            // outside RollMove (including context RNGs) remain unattributed.
            var scoped = NativeRngCallsiteScopesV111.NextFor(stream);
            var site = scoped?.SiteId ?? "UNATTRIBUTED";
            var actor = scoped?.OwnerId ?? owner;
            var ordinal = scoped?.CallOrdinal ?? streamOrdinal;
            var evidence = new NativeRngBoundaryEvidenceV111(
                site, stream, actor, ordinal, state.Bound,
                checked(result - state.Min), state.Before!, after);
            NativeV111PassiveBridge.NativeRngBoundary(evidence);
        }
        catch (Exception error) { ObservationFailure(error); }
    }

    private static void ObserveScalar(NativeRng rng, string operation,
        double? numeric, bool? boolean, CallState state)
    {
        if (!state.Active || _observationFailed || !_attached) return;
        try
        {
            var after = Snapshot(rng);
            var (stream, owner, streamOrdinal) = Owners.Next(rng);
            // MonsterModel.RollMove's existing source scope specifically
            // labels index draws. A bool/double source call within that
            // method has a creature owner, but no audited index eligibility.
            // Peeking must not advance NextInt's per-RollMove ordinal.
            var scopedOwner = NativeRngCallsiteScopesV111.PeekOwnerFor(stream);
            var evidence = new NativeRngScalarEvidenceV111(
                "UNATTRIBUTED", stream,
                scopedOwner ?? owner,
                streamOrdinal, operation,
                numeric, boolean, state.Before!, after);
            NativeV111PassiveBridge.NativeRngScalarBoundary(evidence);
        }
        catch (Exception error) { ObservationFailure(error); }
    }

    private static void ObservationFailure(Exception error)
    {
        if (_observationFailed) return;
        _observationFailed = true;
        try { NativeV111PassiveBridge.NativeRngHookFailure(error.GetType().Name); }
        catch { /* A diagnostic hook must not interrupt the game. */ }
    }

    private static string RunName(RunRngType id) => id switch
    {
        RunRngType.UpFront => "up_front",
        RunRngType.Shuffle => "shuffle",
        RunRngType.UnknownMapPoint => "unknown_map_point",
        RunRngType.CombatCardGeneration => "combat_card_generation",
        RunRngType.CombatPotionGeneration => "combat_potion_generation",
        RunRngType.CombatCardSelection => "combat_card_selection",
        RunRngType.CombatEnergyCosts => "combat_energy_costs",
        RunRngType.CombatTargets => "combat_targets",
        RunRngType.MonsterAi => "monster_ai",
        RunRngType.Niche => "niche",
        RunRngType.CombatOrbs => "combat_orbs",
        RunRngType.TreasureRoomRelics => "treasure_room_relics",
        _ => throw new NotSupportedException("Unknown v111 RunRngType")
    };

    private static string PlayerName(PlayerRngType id) => id switch
    {
        PlayerRngType.Rewards => "rewards",
        PlayerRngType.Shops => "shops",
        PlayerRngType.Transformations => "transformations",
        _ => throw new NotSupportedException("Unknown v111 PlayerRngType")
    };
}
#endif
