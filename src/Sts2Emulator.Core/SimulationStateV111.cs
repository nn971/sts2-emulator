namespace Sts2Emulator.Core;

/// <summary>
/// Atomic composite state for mechanics and its active stochastic provider.
/// The domain-specific records own their contents; this envelope groups their
/// lifecycle and publication boundary.
/// </summary>
public sealed record SimulationStateV111<TMechanics, TStochastic>(
    TMechanics Mechanics,
    TStochastic Stochastic);
