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
    public static bool Active => Depth.Value > 0;

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
        if (Active && !(EngineOwnedRewardDepth.Value > 0 &&
                site == "prototype_rng.reward" && operation == "next_int"))
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE site=" + site +
                " operation=" + operation +
                " : gameplay RNG site lacks resumable Nature ownership.");
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
