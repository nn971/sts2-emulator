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
    public static bool Active => Depth.Value > 0;

    public static IDisposable Enter()
    {
        Depth.Value++;
        return new ExitScope();
    }

    public static void RequireMigrated(string site, string operation)
    {
        if (Active)
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
