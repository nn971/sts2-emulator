using System.Threading;

namespace Sts2Emulator.Core;

/// <summary>
/// Fail closed when legacy/unmigrated randomness is encountered in an opt-in
/// controlled player action. Ambient state is scoped to the synchronous
/// transition and isolated by async execution context (including workers).
/// </summary>
public static class ControlledChanceGuardV111
{
    private static readonly AsyncLocal<int> Depth = new();
    private static readonly AsyncLocal<int> EngineOwnedRewardDepth = new();
    private static readonly AsyncLocal<int> EngineOwnedNativeDepth = new();
    public static bool Active => Depth.Value > 0;
    internal static bool EngineOwnedNativeProviderActive =>
        EngineOwnedNativeDepth.Value > 0;

    /// <summary>
    /// An explicit, narrowly scoped *prototype* reward RNG fallback.
    /// This is used only for terminal reward population after a legal
    /// action in the restricted semantic pilot. It does not authorize
    /// combat/targeting/deck/monster RNG calls or claim native parity.
    /// </summary>
    public static IDisposable AllowEngineOwnedPrototypeReward()
    {
        if (!Active)
            throw new InvalidOperationException(
                "Engine-owned reward delegation requires a guarded semantic action.");
        EngineOwnedRewardDepth.Value++;
        return new RewardExitScope();
    }

    /// <summary>
    /// Allow the versioned native-v111 provider while a guarded comparator
    /// action runs. Prototype RNG remains rejected, so a chance site that has
    /// not migrated to the native provider cannot silently contaminate the
    /// native-policy baseline.
    /// </summary>
    public static IDisposable AllowEngineOwnedNativeProvider()
    {
        if (!Active)
            throw new InvalidOperationException(
                "Native provider delegation requires a guarded comparator action.");
        EngineOwnedNativeDepth.Value++;
        return new NativeExitScope();
    }

    private sealed class RewardExitScope : IDisposable
    {
        private bool _exited;
        public void Dispose()
        {
            if (_exited) return;
            _exited = true;
            EngineOwnedRewardDepth.Value--;
        }
    }

    public static IDisposable Enter()
    {
        Depth.Value++;
        return new ExitScope();
    }

    public static void RequireMigrated(string site, string operation)
    {
        if (Active && EngineOwnedNativeDepth.Value > 0 &&
            (site is "native_rng.run.monster_ai" or
                "native_rng.run.shuffle" or
                "native_rng.run.combat_energy_costs" or
                "native_rng.run.combat_targets" or
                "native_rng.run.combat_card_selection" or
                "native_rng.run.combat_card_generation" or
                "native_rng.run.combat_potion_generation") &&
            operation == "next_uint64")
            return;
        if (Active && !(EngineOwnedRewardDepth.Value > 0 &&
                site == "prototype_rng.reward" && operation == "next_int"))
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE site=" + site +
                " operation=" + operation +
                " : gameplay RNG site lacks resumable Nature ownership.");
    }

    private sealed class NativeExitScope : IDisposable
    {
        private bool _exited;
        public void Dispose()
        {
            if (_exited) return;
            _exited = true;
            EngineOwnedNativeDepth.Value--;
        }
    }

    private sealed class ExitScope : IDisposable
    {
        private bool _exited;
        public void Dispose()
        {
            if (_exited)
                return;
            _exited = true;
            Depth.Value--;
        }
    }
}
