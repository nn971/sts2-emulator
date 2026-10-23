namespace Sts2Emulator.Core;

/// <summary>
/// Provider-neutral mechanics for advancing and suspending a saved draw
/// segment. Provider adapters retain their own choice and transcript owners.
/// </summary>
public sealed partial class PrototypeGameEngine
{
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
        int remaining,
        PrototypeControlledSemanticDrawV111 draw,
        PrototypeChoiceResolutionContinuationState continuation,
        long[] drawnCardInstanceIds,
        bool accumulateDrawnCardIds,
        bool markStandaloneHookChoice)
    {
        var resumedDraw = AdvanceControlledSemanticDrawFrame(
            draw, remaining, drawnCardInstanceIds,
            accumulateDrawnCardIds);
        var resumeDraw = CreateDrawResumeOperation(
            resumedDraw.RemainingCount, resumedDraw.FromHandDraw,
            resumedDraw.EventDepth, resumedDraw.DrawnCardKeyword,
            resumedDraw.DeferredKeywordCardInstanceIds,
            resumedDraw.StandaloneSemanticDrawSegment,
            resumedDraw.DrawEqualSelectionCount);
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
