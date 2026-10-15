namespace Sts2Emulator.Core;

/// <summary>
/// Opt-in initial semantic combat draw bridge. Uses the *real* DrawCards
/// kernel; its isolated fixture envelope is distinct from the legacy
/// opening Fisher-Yates controlled-playable state and transcript.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal static (RunState Run, int RemainingCount)
        BeginSemanticCombatDraw(RunState run, int count, bool fromHandDraw)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Semantic combat draw requires a controlled chance guard.");
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        var world = RequireWorld(run);
        var combat = world.Combat ??
            throw new InvalidOperationException("No combat to draw from.");
        if (combat.SemanticDeck is null)
            throw new InvalidOperationException("Missing semantic deck provider.");
        if (combat.PendingChoice is not null ||
            combat.AutomaticPipelineContinuation is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw " +
                "operation=nested_choice_or_automatic_pipeline");
        var result = DrawCards(
            run.Player, combat, count, run.Rng, fromHandDraw,
            controlledSemanticDrawSuspension: true);
        if (result.ControlledRecyclePending || result.ShuffleSelectionPending ||
            result.ControlledDrawCostPending)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw " +
                "operation=unsupported_nested_stage");
        return (run with
        {
            Player = result.Player,
            World = world with { Combat = result.Combat }
        }, result.ControlledSemanticDrawPending
            ? result.RemainingCount : 0);
    }
}
