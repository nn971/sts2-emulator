#if STS2_NATIVE
using System.Reflection;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Rngs;
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

    internal static bool IsInstalled => _patches is not null;

    internal static void TryInstall()
    {
        if (IsInstalled) return;
        if (Environment.GetEnvironmentVariable("STS2_FIDELITY_RNG_HOOK") != "1")
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
            _patches = harmony;
            GD.Print("[Sts2ReferenceMod] RNG_HOOK_STATUS=INSTALLED_BOUND_INT_OVERLOADS;" +
                " owner_attribution=run_and_player_only; call_site=UNATTRIBUTED");
        }
        catch (Exception error)
        {
            try { harmony?.UnpatchSelf(); }
            catch (Exception) { /* original game must still run */ }
            _patches = null;
            GD.PushError("[Sts2ReferenceMod] RNG_HOOK_STATUS=INSTALL_FAILED;" +
                " reason=" + error.GetType().Name + "; exact RNG fidelity NOT_TESTED");
        }
    }

    internal static void BeginCombat(CombatState combat)
    {
        if (!IsInstalled) return;
        _attached = false;
        Owners.Clear();
        _observationFailed = false;
        _threadId = Environment.CurrentManagedThreadId;
        // Register only identities actually confirmed by the native run/player
        // RNG sets, not guessed from counter changes. The caller has already
        // established single-player combat and the v0.111 binary pin.
        var player = combat.Players.Single();
        var run = player.RunState.Rng;
        foreach (RunRngType type in Enum.GetValues<RunRngType>())
            Owners.Register(run.GetRng(type), "run." + RunName(type), "run");
        foreach (PlayerRngType type in Enum.GetValues<PlayerRngType>())
            Owners.Register(player.PlayerRng.GetRng(type),
                "player." + PlayerName(type), "player:0");
        _attached = true;
    }

    internal static void EndCombat()
    {
        _attached = false;
        Owners.Clear();
    }

    private static NativeRngSnapshot Snapshot(NativeRng rng)
    {
        var data = rng.ToSerializable(); // Game source: pure state copy, zero draws
        return new NativeRngSnapshot(
            data.counter, data.state0, data.state1, data.state2, data.state3);
    }

    private static CallState Capture(NativeRng rng, int min, int max)
    {
        if (!_attached || _observationFailed ||
            Environment.CurrentManagedThreadId != _threadId ||
            max <= min)
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

    private static void Observe(NativeRng rng, int result, CallState state)
    {
        if (!state.Active || !_attached || _observationFailed) return;
        try
        {
            var after = Snapshot(rng);
            var (stream, owner, ordinal) = Owners.Next(rng);
            // Hook provenance establishes Rng.NextInt's primitive boundary,
            // not the higher-level rule/eligible candidates or creature actor.
            // Explicit UNATTRIBUTED is mandatory until a source call-site
            // instrumentation registry supplies that causal information.
            var evidence = new NativeRngBoundaryEvidenceV111(
                "UNATTRIBUTED", stream, owner, ordinal, state.Bound,
                checked(result - state.Min), state.Before!, after);
            NativeV111PassiveBridge.NativeRngBoundary(evidence);
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
