using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Mechanical state for one complete run. Run-level provider cursors,
/// controlled histories and pending chance continuations are paired beside
/// it through <see cref="SimulationStateV111{TMechanics, TStochastic}"/>.
/// </summary>
public sealed record RunMechanicsStateV111(
    string GameBuild,
    string EmulatorSchema,
    string RunId,
    string RunSeed,
    long DecisionIndex,
    RunPhase Phase,
    PlayerState Player,
    JsonElement ExtensionState,
    RunWorldState? World = null,
    int Ascension = 0,
    RunConfiguration? Configuration = null);
