namespace Sts2Emulator.Core;

/// <summary>
/// Typed provider decisions and chronology for one semantic automatic-turn
/// continuation. SemanticEndTurnV111 keeps its established flattened fields.
/// </summary>
public sealed record SemanticEndTurnStochasticStateV111(
    RandomIndexDecisionV111[] IntentTranscript,
    RandomIndexDecisionV111[] EnemyActionTranscript,
    SemanticTargetDecisionV111[] TargetTranscript,
    SemanticEndTurnBoundaryChoiceV111[] BoundaryTranscript,
    SemanticCombatDrawStochasticStateV111? HandDraw = null)
{
    public SemanticEndTurnStochasticStateV111 Fork() => this with
    {
        IntentTranscript = IntentTranscript.Select(
            PrototypeControlledEventChoiceContinuationV111.CloneDecision)
            .ToArray(),
        EnemyActionTranscript = EnemyActionTranscript.Select(
            PrototypeControlledEventChoiceContinuationV111.CloneDecision)
            .ToArray(),
        TargetTranscript = TargetTranscript.Select(decision =>
            decision.Fork()).ToArray(),
        BoundaryTranscript =
            (SemanticEndTurnBoundaryChoiceV111[])BoundaryTranscript.Clone(),
        HandDraw = HandDraw?.Fork()
    };

    public void Validate()
    {
        if (IntentTranscript is null || EnemyActionTranscript is null ||
            TargetTranscript is null || BoundaryTranscript is null)
            throw new InvalidDataException(
                "Invalid semantic end-turn stages.");
        HandDraw?.Validate();

        foreach (var decision in IntentTranscript)
        {
            ValidateDecision(decision, "Turn-intent");
            if (decision.ProviderMode != "controlled-v111" ||
                decision.NativeCursorBefore is not null ||
                decision.NativeCursorAfter is not null)
                throw new InvalidDataException(
                    "Invalid semantic turn-intent Nature decision.");
        }
        foreach (var decision in EnemyActionTranscript)
        {
            ValidateDecision(decision, "Enemy-action");
            if (decision.ProviderMode != "controlled-v111" ||
                decision.NativeCursorBefore is not null ||
                decision.NativeCursorAfter is not null ||
                !PrototypeControlledEnemyActionContinuationV111
                    .IsEnemyActionSite(decision.Request.SiteId))
                throw new InvalidDataException(
                    "Invalid semantic enemy-action Nature decision.");
        }
        for (int i = 0; i < TargetTranscript.Length; i++)
        {
            var decision = TargetTranscript[i] ?? throw new InvalidDataException(
                "Semantic end-turn target transcript contains a null decision.");
            decision.Validate();
            if (decision.Event.Ordinal != i)
                throw new InvalidDataException(
                    "Semantic end-turn target transcript ordinal is invalid.");
        }

        int transcriptEnemyActions = 0, transcriptTargets = 0;
        foreach (var boundary in BoundaryTranscript)
        {
            (boundary ?? throw new InvalidDataException(
                "Semantic end-turn boundary transcript contains a null entry."))
                .Validate();
            if (boundary.Kind == "enemy_action")
            {
                if (boundary.TranscriptIndex != transcriptEnemyActions++ ||
                    transcriptEnemyActions > EnemyActionTranscript.Length)
                    throw new InvalidDataException(
                        "Semantic end-turn enemy-action boundary order is invalid.");
            }
            else if (boundary.TranscriptIndex != transcriptTargets++ ||
                     transcriptTargets > TargetTranscript.Length)
                throw new InvalidDataException(
                    "Semantic end-turn target boundary order is invalid.");
        }
        if ((BoundaryTranscript.Length > 0 &&
             (transcriptEnemyActions != EnemyActionTranscript.Length ||
              transcriptTargets != TargetTranscript.Length)) ||
            (BoundaryTranscript.Length == 0 &&
             TargetTranscript.Length != 0))
            throw new InvalidDataException(
                "Semantic end-turn boundary transcript is incomplete.");
    }

    private static void ValidateDecision(
        RandomIndexDecisionV111 decision, string kind)
    {
        if (decision is null)
            throw new InvalidDataException(
                $"Semantic {kind.ToLowerInvariant()} transcript contains a null decision.");
        decision.Validate();
    }
}
