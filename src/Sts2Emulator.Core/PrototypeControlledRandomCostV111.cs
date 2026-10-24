namespace Sts2Emulator.Core;

/// <summary>Explicit suspension of source-ordered hand-cost effects. Nature
/// resolves each draw without repeating earlier gameplay operations.</summary>
public sealed partial class PrototypeGameEngine
{
    internal static long[]? ControlledRandomCostCandidates(
        CombatState combat, PrototypeQueuedOperation operation)
    {
        if (operation.Kind is not (
            PrototypeCombatEffectKind.SetRandomHandCardEnergyCostUntilTurnEndOrPlayed
            or PrototypeCombatEffectKind.RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed))
            return null;
        var single = operation.Kind ==
            PrototypeCombatEffectKind.SetRandomHandCardEnergyCostUntilTurnEndOrPlayed;
        return combat.Hand.Where(id =>
        {
            var card = RequireCombatCard(combat, id);
            var definition = PrototypeContent.Card(card.CardId);
            return !definition.Unplayable &&
                definition.Cost.Kind == PrototypeCardCostKind.Fixed &&
                (single
                    ? ResolveFixedCardEnergyCost(combat, card, definition) > 0
                    : definition.Cost.Amount >= 0);
        }).ToArray();
    }

    private static CombatState MakeControlledRandomCostChoice(
        CombatState combat, PrototypeQueuedOperation operation,
        long[] candidates,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        if (!ControlledChanceGuardV111.Active ||
            combat.PendingChoice is not null ||
            candidates.Length == 0 ||
            candidates.Distinct().Count() != candidates.Length)
            throw new InvalidOperationException(
                "Controlled hand-cost effect needs live unique candidates.");
        return combat with
        {
            PendingChoice = new PendingCombatChoiceState(
                ChoiceId: "controlled_random_hand_cost",
                SourceCardInstanceId: continuation.SourceCardInstanceId,
                SourceCardDestination: continuation.SourceCardDestination,
                Selection: new PrototypeCardSelectionSpec(
                    PrototypeCardZone.Hand, 0, 0,
                    PrototypeCardSelectionResolutionKind.MoveToDiscard),
                CandidateCardInstanceIds: [],
                Continuation: [],
                CompletionEvents: [],
                OuterChoiceContinuation: continuation.Fork(),
                ControlledRandomCost: new PrototypeControlledRandomCostV111(
                    operation with { }, (long[])candidates.Clone()))
        };
    }

    internal static bool ValidateControlledRandomCost(
        CombatState combat, PrototypeControlledRandomCostV111 choice)
    {
        var expected = ControlledRandomCostCandidates(combat, choice.Operation);
        return expected is { Length: > 0 } &&
            choice.EligibleCardInstanceIds is { Length: > 0 } &&
            choice.NextCardIndex >= 0 &&
            choice.NextCardIndex < choice.EligibleCardInstanceIds.Length &&
            choice.EligibleCardInstanceIds.SequenceEqual(expected) &&
            (choice.Operation.Kind ==
                PrototypeCombatEffectKind.RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed
                || choice.NextCardIndex == 0);
    }

    internal static RunState ResumeControlledRandomCost(
        RunState state, int selectedIndex)
        => ResumeControlledRandomCostWithBoundary(state, selectedIndex).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledRandomCostWithBoundary(
            RunState state, int selectedIndex)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException("Controlled cost needs RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ??
            throw new InvalidOperationException("Missing controlled combat.");
        var pending = combat.PendingChoice ??
            throw new InvalidOperationException("No controlled cost choice.");
        if (pending.ChoiceId != "controlled_random_hand_cost" ||
            pending.ControlledRandomCost is not { } choice ||
            pending.OuterChoiceContinuation is not { } continuation ||
            !ValidateControlledRandomCost(combat, choice))
            throw new InvalidDataException("Saved random-cost support is invalid.");

        var single = choice.Operation.Kind ==
            PrototypeCombatEffectKind.SetRandomHandCardEnergyCostUntilTurnEndOrPlayed;
        int support = single ? choice.EligibleCardInstanceIds.Length
                             : Math.Max(1, choice.Operation.Amount);
        if (selectedIndex < 0 || selectedIndex >= support)
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));
        var cardId = choice.EligibleCardInstanceIds[
            single ? selectedIndex : choice.NextCardIndex];
        int newCost = single ? Math.Max(0, choice.Operation.Amount) : selectedIndex;
        combat = SetCardTemporaryEnergyCost(combat, cardId,
            new PrototypeTemporaryCardCost(newCost,
                PrototypeTemporaryCardCostExpiry.EndOfTurn |
                PrototypeTemporaryCardCostExpiry.WhenPlayed));
        if (!single &&
            choice.NextCardIndex + 1 < choice.EligibleCardInstanceIds.Length)
        {
            return PrototypeAutomaticPipelineResumeResultV111.FromRun(
                state with
                {
                    World = world with
                    {
                        Combat = combat with
                        {
                            PendingChoice = pending with
                            {
                                ControlledRandomCost = choice with
                                {
                                    NextCardIndex = choice.NextCardIndex + 1
                                }
                            }
                        }
                    }
                });
        }

        combat = combat with { PendingChoice = null };
        var prototypeRng = state.Stochastic.PrototypeRng;
        var resolved = ResumeChoiceResolutionContinuation(
            state.Player, combat, prototypeRng, continuation);
        var updated = state with
        {
            Player = resolved.Player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng),
            World = world with { Combat = resolved.Combat }
        };
        return ResumeAutomaticPipelineContinuation(updated);
    }

    internal static RunState ResumeControlledRandomCostByCard(
        RunState state, long selectedCardInstanceId)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Controlled cost needs RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ??
            throw new InvalidOperationException(
                "Missing controlled combat.");
        var pending = combat.PendingChoice ??
            throw new InvalidOperationException(
                "No controlled cost choice.");
        if (pending.ChoiceId != "controlled_random_hand_cost" ||
            pending.ControlledRandomCost is not { } choice ||
            pending.OuterChoiceContinuation is not { } continuation ||
            choice.Operation.Kind != PrototypeCombatEffectKind
                .SetRandomHandCardEnergyCostUntilTurnEndOrPlayed ||
            !ValidateControlledRandomCost(combat, choice))
            throw new InvalidDataException(
                "Saved single-card random-cost support is invalid.");
        if (!choice.EligibleCardInstanceIds.Contains(selectedCardInstanceId))
            throw new InvalidOperationException(
                "Selected hand-card identity is outside the saved cost support.");

        combat = SetCardTemporaryEnergyCost(combat, selectedCardInstanceId,
            new PrototypeTemporaryCardCost(
                Math.Max(0, choice.Operation.Amount),
                PrototypeTemporaryCardCostExpiry.EndOfTurn |
                    PrototypeTemporaryCardCostExpiry.WhenPlayed));
        combat = combat with { PendingChoice = null };
        var prototypeRng = state.Stochastic.PrototypeRng;
        var resolved = ResumeChoiceResolutionContinuation(
            state.Player, combat, prototypeRng, continuation);
        var updated = state with
        {
            Player = resolved.Player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng),
            World = world with { Combat = resolved.Combat }
        };
        return ResumeAutomaticPipelineContinuation(updated).Run;
    }

    /// <summary>Narrow test entry using the actual operations kernel for
    /// effects outside the initial Silent starter fixture.</summary>
    internal static RunState BeginControlledRandomCostFixture(
        RunState state, PrototypeQueuedOperation operation)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException("Controlled cost fixture needs RNG guard.");
        if (operation.Kind is not (
            PrototypeCombatEffectKind.SetRandomHandCardEnergyCostUntilTurnEndOrPlayed
            or PrototypeCombatEffectKind.RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed))
            throw new ArgumentException("Expected a hand-cost chance operation.");
        var world = RequireWorld(state);
        var combat = world.Combat ??
            throw new InvalidOperationException("Missing fixture combat.");
        var prototypeRng = state.Stochastic.PrototypeRng;
        var resolved = ResolveOperations(state.Player, combat,
            new Queue<PrototypeQueuedOperation>([operation]), prototypeRng,
            moveSourceCardOnCompletion: false);
        return state with
        {
            Player = resolved.Player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng),
            World = world with { Combat = resolved.Combat }
        };
    }
}
