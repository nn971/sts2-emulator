namespace Sts2Emulator.Core;

/// <summary>
/// Shared mechanics for resuming the remaining cards in an automatic
/// DrawPlayerHand segment. Provider adapters choose which tagged boundaries
/// they can suspend and interpret the next result.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private static PrototypeDrawCardsResult ResumeAutomaticHandDrawCards(
        RunState checkpoint,
        int remaining,
        PrototypeDrawCardsSuspensionPolicyV111 suspensionPolicy,
        int eventDepth = 0)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Automatic hand-draw resumption requires the RNG guard.");
        if (remaining < 0 || eventDepth < 0)
            throw new ArgumentOutOfRangeException(
                remaining < 0 ? nameof(remaining) : nameof(eventDepth));

        var world = RequireWorld(checkpoint);
        var combat = world.Combat ?? throw new InvalidDataException(
            "Automatic hand-draw resumption has no combat.");
        var cursor = combat.AutomaticPipelineContinuation;
        if (cursor is null ||
            !cursor.Stochastic.TryGetAutomaticHandDrawRemainder(
                cursor.Mechanics, out int savedRemaining) ||
            savedRemaining != remaining)
            throw new InvalidDataException(
                "Automatic hand-draw resume cursor is inconsistent.");

        if (combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            combat.SemanticDeck?.Pending is not null)
            throw new InvalidDataException(
                "Automatic hand-draw resume boundary is inconsistent.");

        combat = combat with
        {
            AutomaticPipelineContinuation = cursor.WithHandDrawProgress(null)
        };
        return DrawCards(
            checkpoint.Player, combat, remaining, checkpoint.Rng,
            fromHandDraw: true, eventDepth: eventDepth,
            suspensionPolicy: suspensionPolicy);
    }
}
