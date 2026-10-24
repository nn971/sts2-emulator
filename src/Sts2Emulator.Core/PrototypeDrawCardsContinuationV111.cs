namespace Sts2Emulator.Core;

/// <summary>
/// Provider-neutral mechanics for advancing and suspending a saved draw
/// segment, including shared draw-progress interpretation. Provider adapters
/// retain their own choice and transcript owners.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private static PrototypeQueuedOperation CreateDrawResumeOperation(
        PrototypeControlledSemanticDrawV111 draw)
    {
        ArgumentNullException.ThrowIfNull(draw);
        if (draw.RemainingCount < 0 || draw.EventDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(draw),
                "A saved draw cursor requires nonnegative progress values.");
        return new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.DrawCards,
            draw.RemainingCount,
            FromHandDraw: draw.FromHandDraw,
            DrawnCardKeyword: draw.DrawnCardKeyword is null
                ? null : draw.DrawnCardKeyword with { },
            DeferredDrawnCardInstanceIds:
                draw.DeferredKeywordCardInstanceIds,
            DrawEventDepth: draw.EventDepth,
            StandaloneSemanticDrawSegment:
                draw.StandaloneSemanticDrawSegment,
            DrawEqualSelectionCount: draw.DrawEqualSelectionCount);
    }

    private static CombatState AttachDrawResumeContinuation(
        CombatState combat,
        PrototypeQueuedOperation drawOperation,
        PrototypeChoiceResolutionContinuationState continuation) =>
        AttachOuterChoiceContinuation(
            combat,
            continuation with
            {
                Operations = new[] { drawOperation }
                    .Concat(continuation.Operations).ToArray()
            });

    private static long[] AccumulateDrawnCardInstanceIds(
        IEnumerable<long>? previousCardInstanceIds,
        IEnumerable<long> drawnCardInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(drawnCardInstanceIds);
        return (previousCardInstanceIds ?? [])
            .Concat(drawnCardInstanceIds)
            .Distinct()
            .ToArray();
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

    internal static int GetCardsDrawnSince(
        CombatState combat, int drawnBefore)
    {
        ArgumentNullException.ThrowIfNull(combat);
        if (drawnBefore < 0)
            throw new ArgumentOutOfRangeException(nameof(drawnBefore));
        var drawn = combat.CounterState.CardsDrawnThisCombat - drawnBefore;
        if (drawn < 0)
            throw new InvalidDataException(
                "Combat draw count moved behind its saved resume checkpoint.");
        return drawn;
    }

    internal static int ExpectedStratagemSelectionCount(
        CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(combat);
        int stacks = combat.PlayerPowers
            .Where(power => power.PowerId == "proto.power.stratagem")
            .Sum(power => Math.Max(0, power.Stacks));
        return Math.Min(stacks,
            Math.Min(combat.DrawPile.Length,
                PrototypeContent.Rules.RemainingHandCapacity(
                    combat.Hand.Length)));
    }

    /// <summary>
    /// Resume the remaining cards in a saved automatic hand-draw segment.
    /// Provider adapters supply suspension policy and RNG; this owner checks
    /// the shared cursor, clears its progress, and enters the draw kernel.
    /// </summary>
    private static PrototypeDrawCardsResult ResumeAutomaticHandDrawCards(
        RunState checkpoint,
        int remaining,
        PrototypeDrawCardsSuspensionPolicyV111 suspensionPolicy,
        RngBundle prototypeRng,
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
            !cursor.Stochastic.TryGetActiveAutomaticHandDrawProgress(
                cursor.Mechanics, out var progress) ||
            progress.RemainingCount != remaining)
            throw new InvalidDataException(
                "Automatic hand-draw resume cursor is inconsistent.");

        if (combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            combat.Stochastic.SemanticDeck?.Pending is not null)
            throw new InvalidDataException(
                "Automatic hand-draw resume boundary is inconsistent.");

        var cleared = CommitAutomaticHandDrawProgress(
            checkpoint, checkpoint.Player, combat, progress: null);
        return DrawCards(
            cleared.Player, RequireWorld(cleared).Combat!, remaining,
            prototypeRng,
            fromHandDraw: true, eventDepth: eventDepth,
            suspensionPolicy: suspensionPolicy,
            automaticHandDrawStage: true);
    }
}
