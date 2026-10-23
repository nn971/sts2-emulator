namespace Sts2Emulator.Core;

/// <summary>
/// Provider-neutral result for resuming a DrawCards mechanics boundary.
/// Provider-specific decisions and transcript progress stay with their
/// existing stochastic owners.
/// </summary>
internal readonly record struct PrototypeDrawCardsResumeResultV111(
    RunState Run, int RemainingCount);
