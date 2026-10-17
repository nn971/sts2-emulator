namespace Sts2Emulator.Core;

/// <summary>
/// Typed provider decisions and chronology for one semantic automatic-turn
/// continuation. SemanticEndTurnV111 keeps its established flattened fields.
/// </summary>
public sealed record SemanticEndTurnStochasticStateV111(
    RandomIndexDecisionV111[] IntentTranscript,
    RandomIndexDecisionV111[] EnemyActionTranscript,
    SemanticTargetDecisionV111[] TargetTranscript,
    SemanticEndTurnBoundaryChoiceV111[] BoundaryTranscript)
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
            (SemanticEndTurnBoundaryChoiceV111[])BoundaryTranscript.Clone()
    };
}
