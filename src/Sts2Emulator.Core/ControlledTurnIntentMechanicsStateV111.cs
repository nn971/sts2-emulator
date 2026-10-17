namespace Sts2Emulator.Core;

/// <summary>
/// Mechanical checkpoint for a suspended next-turn intent pass. The
/// provider's pending request and transcript are paired through the
/// simulation envelope.
/// </summary>
public sealed record ControlledTurnIntentMechanicsStateV111(
    CombatState BaselineCombat,
    CombatState Combat,
    string NativeEncounterEntry,
    int NextEnemyIndex,
    string SiteId);
