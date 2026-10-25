namespace Sts2Emulator.Core;

/// <summary>
/// Owns the tagged mechanical result of DrawCards and its common publication
/// boundary. Provider adapters interpret validated results and retain their
/// own continuation and transcript state.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private enum PrototypeDrawCardsBoundaryKind
    {
        Complete,
        ShuffleSelection,
        ControlledRecycle,
        ControlledDrawCost,
        SemanticDraw,
        AfterShuffleInsertions,
        NestedCardDrawnChoice
    }

    private sealed record PrototypeDrawCardsBoundaryV111
    {
        public PrototypeDrawCardsBoundaryKind Kind { get; }
        public int RemainingCount { get; }
        public PrototypeAfterShuffleRelicInsertionV111[]?
            AfterShuffleInsertions { get; }

        private PrototypeDrawCardsBoundaryV111(
            PrototypeDrawCardsBoundaryKind kind,
            int remainingCount,
            PrototypeAfterShuffleRelicInsertionV111[]? insertions)
        {
            Kind = kind;
            RemainingCount = remainingCount;
            AfterShuffleInsertions = insertions;
        }

        public static PrototypeDrawCardsBoundaryV111 Complete() =>
            new(PrototypeDrawCardsBoundaryKind.Complete, 0, null);

        public static PrototypeDrawCardsBoundaryV111 ShuffleSelection(
            int remainingCount) => Pending(
                PrototypeDrawCardsBoundaryKind.ShuffleSelection,
                remainingCount, requiresPositiveCount: true);

        public static PrototypeDrawCardsBoundaryV111 ControlledRecycle(
            int remainingCount) => Pending(
                PrototypeDrawCardsBoundaryKind.ControlledRecycle,
                remainingCount, requiresPositiveCount: true);

        public static PrototypeDrawCardsBoundaryV111 ControlledDrawCost(
            int remainingCount) => Pending(
                PrototypeDrawCardsBoundaryKind.ControlledDrawCost,
                remainingCount, requiresPositiveCount: false);

        public static PrototypeDrawCardsBoundaryV111 SemanticDraw(
            int remainingCount) => Pending(
                PrototypeDrawCardsBoundaryKind.SemanticDraw,
                remainingCount, requiresPositiveCount: true);

        public static PrototypeDrawCardsBoundaryV111
            AfterShuffleInsertionPending(
            int remainingCount,
            PrototypeAfterShuffleRelicInsertionV111[] insertions)
        {
            ArgumentNullException.ThrowIfNull(insertions);
            if (insertions.Length == 0)
                throw new ArgumentException(
                    "After-shuffle boundary needs a pending insertion.",
                    nameof(insertions));
            return Pending(
                PrototypeDrawCardsBoundaryKind.AfterShuffleInsertions,
                remainingCount, requiresPositiveCount: true,
                (PrototypeAfterShuffleRelicInsertionV111[])insertions.Clone());
        }

        public static PrototypeDrawCardsBoundaryV111 NestedCardDrawnChoice(
            int remainingCount) => Pending(
                PrototypeDrawCardsBoundaryKind.NestedCardDrawnChoice,
                remainingCount, requiresPositiveCount: false);

        private static PrototypeDrawCardsBoundaryV111 Pending(
            PrototypeDrawCardsBoundaryKind kind,
            int remainingCount,
            bool requiresPositiveCount,
            PrototypeAfterShuffleRelicInsertionV111[]? insertions = null)
        {
            if (remainingCount < (requiresPositiveCount ? 1 : 0))
                throw new ArgumentOutOfRangeException(nameof(remainingCount));
            return new PrototypeDrawCardsBoundaryV111(
                kind, remainingCount, insertions);
        }
    }

    private sealed record PrototypeDrawCardsResult(
        PlayerState Player,
        CombatState Combat,
        long[] DrawnCardInstanceIds,
        PrototypeDrawCardsBoundaryV111 Boundary)
    {
        public PrototypeDrawCardsBoundaryKind BoundaryKind => Boundary.Kind;
        public int RemainingCount => Boundary.RemainingCount;
        public PrototypeAfterShuffleRelicInsertionV111[]?
            ControlledAfterShuffleInsertions =>
                Boundary.AfterShuffleInsertions;
    }

    private static void ValidateDrawCardsResult(
        PrototypeDrawCardsResult drawn, bool automaticHandDraw)
    {
        ArgumentNullException.ThrowIfNull(drawn);
        var combat = drawn.Combat;
        if (drawn.RemainingCount < 0)
            throw new InvalidDataException(
                "DrawCards result has a negative remaining count.");
        if (automaticHandDraw &&
            (combat.AutomaticPipelineContinuation is not { } cursor ||
             !cursor.Mechanics.IsAtStep(
                 PrototypeAutomaticStepKind.DrawPlayerHand)))
            throw new InvalidDataException(
                "Automatic hand-draw result lost its DrawPlayerHand checkpoint.");

        bool validBoundary = drawn.BoundaryKind switch
        {
            PrototypeDrawCardsBoundaryKind.Complete =>
                drawn.RemainingCount == 0 &&
                combat.PendingChoice is null &&
                combat.PendingKnowledgeDemonChoice is null &&
                combat.Stochastic.SemanticDeck?.Pending is null,
            PrototypeDrawCardsBoundaryKind.ShuffleSelection =>
                drawn.RemainingCount > 0 &&
                combat.PendingChoice is null &&
                combat.PendingKnowledgeDemonChoice is null &&
                ExpectedStratagemSelectionCount(combat) > 0,
            PrototypeDrawCardsBoundaryKind.ControlledRecycle =>
                drawn.RemainingCount > 0 &&
                combat.Stochastic.SemanticDeck is null &&
                combat.PendingChoice is null &&
                combat.PendingKnowledgeDemonChoice is null &&
                combat.DrawPile.Length == 0 &&
                combat.DiscardPile.Length > 0,
            PrototypeDrawCardsBoundaryKind.ControlledDrawCost =>
                combat.PendingChoice?.ControlledDrawCost is not null &&
                combat.PendingKnowledgeDemonChoice is null,
            PrototypeDrawCardsBoundaryKind.SemanticDraw =>
                drawn.RemainingCount > 0 &&
                combat.Stochastic.SemanticDeck is
                    { Mode: SemanticDeckStateV111.NatureMode,
                      Pending: not null } &&
                combat.PendingChoice is null &&
                combat.PendingKnowledgeDemonChoice is null,
            PrototypeDrawCardsBoundaryKind.AfterShuffleInsertions =>
                drawn.RemainingCount > 0 &&
                combat.PendingChoice is null &&
                combat.PendingKnowledgeDemonChoice is null &&
                drawn.ControlledAfterShuffleInsertions is
                    { Length: > 0 },
            PrototypeDrawCardsBoundaryKind.NestedCardDrawnChoice =>
                combat.PendingChoice is not null &&
                combat.PendingKnowledgeDemonChoice is null &&
                combat.Stochastic.SemanticDeck?.Pending is null,
            _ => false
        };
        if (!validBoundary)
            throw new InvalidDataException(
                "DrawCards result does not match its saved mechanical boundary.");
    }

    private static PrototypeDrawCardsResult CreateDrawCardsResult(
        PlayerState player, CombatState combat,
        long[] drawnCardInstanceIds,
        PrototypeDrawCardsBoundaryV111 boundary,
        bool automaticHandDrawStage)
    {
        var result = new PrototypeDrawCardsResult(
            player, combat, drawnCardInstanceIds, boundary);
        ValidateDrawCardsResult(result, automaticHandDrawStage);
        return result;
    }

    private static RunState PublishDrawCardsResult(
        RunState checkpoint, PrototypeDrawCardsResult drawn)
    {
        ArgumentNullException.ThrowIfNull(drawn);
        return PublishDrawMechanicsState(
            checkpoint, drawn.Player, drawn.Combat);
    }

    /// <summary>
    /// Publish the coupled mechanics and prototype-provider result of a draw
    /// continuation whose operations kernel advanced the caller's RNG.
    /// </summary>
    private static RunState PublishDrawMechanicsAndPrototypeRng(
        RunState checkpoint,
        PlayerState player,
        CombatState combat,
        RngBundle prototypeRng)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(prototypeRng);
        var stochastic = RunStochasticStateV111.WithPrototypeRng(
            checkpoint.Stochastic, prototypeRng);
        return PublishDrawMechanicsState(
            checkpoint with { Stochastic = stochastic }, player, combat);
    }

    /// <summary>
    /// Publish the coupled player/combat mechanics from ordinary, nested,
    /// opening, or automatic hand-draw resolution.
    /// </summary>
    private static RunState PublishDrawMechanicsState(
        RunState checkpoint, PlayerState player, CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(combat);
        var world = RequireWorld(checkpoint);
        return checkpoint with
        {
            Player = player,
            World = world with { Combat = combat }
        };
    }
}
