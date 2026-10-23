namespace Sts2Emulator.Core;

/// <summary>
/// Provider-neutral mechanics for advancing and suspending a saved draw
/// segment. Provider adapters retain their own choice and transcript owners.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private static PrototypeQueuedOperation CreateDrawResumeOperation(
        PrototypeControlledSemanticDrawV111 draw)
    {
        ArgumentNullException.ThrowIfNull(draw);
        return CreateDrawResumeOperation(
            draw.RemainingCount, draw.FromHandDraw, draw.EventDepth,
            draw.DrawnCardKeyword, draw.DeferredKeywordCardInstanceIds,
            draw.StandaloneSemanticDrawSegment,
            draw.DrawEqualSelectionCount);
    }

    private static PrototypeControlledSemanticDrawV111
        CreateAdvancedEffectDrawFrame(
            PrototypeQueuedOperation operation,
            int remainingCount,
            int eventDepth,
            long[] drawnCardInstanceIds,
            bool? stratagemSelectionPending = null)
    {
        if (operation.Kind != PrototypeCombatEffectKind.DrawCards)
            throw new ArgumentException(
                "Effect draw frame needs a DrawCards operation.",
                nameof(operation));
        var draw = new PrototypeControlledSemanticDrawV111(
            remainingCount, operation.FromHandDraw, eventDepth,
            operation.DrawnCardKeyword,
            operation.DeferredDrawnCardInstanceIds,
            StandaloneSemanticDrawSegment:
                operation.StandaloneSemanticDrawSegment)
        {
            DrawEqualSelectionCount = operation.DrawEqualSelectionCount
        };
        return AdvanceControlledSemanticDrawFrame(
            draw, remainingCount, drawnCardInstanceIds,
            accumulateDrawnCardIds: true,
            stratagemSelectionPending: stratagemSelectionPending);
    }

    private static PrototypeControlledSemanticDrawV111
        AdvanceControlledSemanticDrawFrame(
            PrototypeControlledSemanticDrawV111 draw,
            int remainingCount,
            long[] drawnCardInstanceIds,
            bool accumulateDrawnCardIds,
            bool? stratagemSelectionPending = null)
    {
        var deferredKeywordIds = accumulateDrawnCardIds
            ? AccumulateDrawnCardInstanceIds(
                draw.DeferredKeywordCardInstanceIds,
                drawnCardInstanceIds)
            : draw.DeferredKeywordCardInstanceIds;
        return draw with
        {
            RemainingCount = remainingCount,
            DeferredKeywordCardInstanceIds = deferredKeywordIds,
            StratagemSelectionPending =
                stratagemSelectionPending ?? draw.StratagemSelectionPending
        };
    }

    private static CombatState AttachDrawCardsAtCardDrawnChoice(
        CombatState combat,
        PrototypeControlledSemanticDrawV111 resumedDraw,
        PrototypeChoiceResolutionContinuationState continuation,
        bool markStandaloneHookChoice)
    {
        var resumeDraw = CreateDrawResumeOperation(resumedDraw);
        combat = AttachDrawResumeContinuation(
            combat, resumeDraw, continuation);
        if (markStandaloneHookChoice)
            combat = combat with
            {
                PendingChoice = combat.PendingChoice! with
                {
                    StandaloneSemanticDrawHookChoice = true
                }
            };
        return combat;
    }
}
