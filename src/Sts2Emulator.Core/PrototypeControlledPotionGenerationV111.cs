namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    internal static string[] CombatPotionModelPool() =>
        PrototypeContent.PotionPool;

    internal static bool ValidateControlledPotionGeneration(
        CombatState combat,
        PrototypeControlledPotionGenerationV111 choice,
        long? sourceCardInstanceId)
    {
        var pending = combat.PendingChoice;
        return choice.Operation.Kind ==
                PrototypeCombatEffectKind.AcquireRandomCombatPotion &&
            choice.Operation.Amount > 0 &&
            choice.CandidatePotionIds is { Length: > 0 } &&
            choice.CandidatePotionIds.SequenceEqual(
                CombatPotionModelPool()) &&
            choice.CandidatePotionIds.Distinct(StringComparer.Ordinal)
                .Count() == choice.CandidatePotionIds.Length &&
            pending is
            {
                ChoiceId: "controlled_potion_generation",
                OuterChoiceContinuation: { } continuation
            } &&
            pending.SourceCardInstanceId == sourceCardInstanceId &&
            continuation.SourceCardInstanceId == sourceCardInstanceId &&
            continuation.SourceCardDestination ==
                pending.SourceCardDestination;
    }

    private static CombatState MakeControlledPotionGenerationChoice(
        CombatState combat, PrototypeQueuedOperation operation,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        var candidates = CombatPotionModelPool();
        if (!ControlledChanceGuardV111.Active || operation.Amount <= 0 ||
            operation.Kind !=
                PrototypeCombatEffectKind.AcquireRandomCombatPotion ||
            combat.PendingChoice is not null || candidates.Length == 0 ||
            candidates.Distinct(StringComparer.Ordinal).Count() !=
                candidates.Length)
            throw new InvalidOperationException(
                "Controlled potion generation requires a unique, live source pool.");
        return combat with
        {
            PendingChoice = new PendingCombatChoiceState(
                ChoiceId: "controlled_potion_generation",
                SourceCardInstanceId: continuation.SourceCardInstanceId,
                SourceCardDestination: continuation.SourceCardDestination,
                Selection: new PrototypeCardSelectionSpec(
                    PrototypeCardZone.Hand, 0, 0,
                    PrototypeCardSelectionResolutionKind.MoveToDiscard),
                CandidateCardInstanceIds: [],
                Continuation: [],
                CompletionEvents: [],
                OuterChoiceContinuation: continuation.Fork(),
                ControlledPotionGeneration:
                    new PrototypeControlledPotionGenerationV111(
                        operation with { }, (string[])candidates.Clone()))
        };
    }

    internal static RunState ResumeControlledPotionGeneration(
        RunState state, string selectedPotionId)
        => ResumeControlledPotionGenerationWithBoundary(
            state, selectedPotionId).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledPotionGenerationWithBoundary(
            RunState state, string selectedPotionId)
        => ResumeControlledPotionGenerationCore(
            state, selectedPotionId, deferSemanticHandDraw: false);

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledPotionGenerationForSemanticHandDrawWithBoundary(
            RunState state, string selectedPotionId)
        => ResumeControlledPotionGenerationCore(
            state, selectedPotionId, deferSemanticHandDraw: true);

    private static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledPotionGenerationCore(
            RunState state, string selectedPotionId,
            bool deferSemanticHandDraw)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Controlled potion generation requires the RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ??
            throw new InvalidOperationException(
                "Missing combat in potion generation.");
        var pending = combat.PendingChoice ??
            throw new InvalidOperationException(
                "No random potion generation is pending.");
        if (pending.ChoiceId != "controlled_potion_generation" ||
            pending.ControlledPotionGeneration is not { } generation ||
            pending.OuterChoiceContinuation is not { } continuation ||
            !ValidateControlledPotionGeneration(
                combat, generation, pending.SourceCardInstanceId))
            throw new InvalidDataException(
                "Saved potion generation is not source-reachable.");
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPotionId);
        if (!generation.CandidatePotionIds.Contains(
                selectedPotionId, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Selected potion identity is outside the saved source pool.");
        _ = PrototypeContent.Potion(selectedPotionId);
        var slots = (PotionInstance?[])state.Player.PotionSlots.Clone();
        var firstEmpty = Array.FindIndex(slots, potion => potion is null);
        if (firstEmpty >= 0)
        {
            var potion = new PotionInstance(
                selectedPotionId, PrototypeJson.EmptyObject());
            slots[firstEmpty] = potion;
            combat = combat with
            {
                Potions = combat.PotionStates.Append(
                    new CombatPotionState(firstEmpty, selectedPotionId,
                        potion.PersistentState.Clone())).ToArray()
            };
        }

        var operation = generation.Operation;
        var remainingOperations = operation.Amount > 1
            ? new[] { operation with { Amount = operation.Amount - 1 } }
                .Concat(continuation.Operations).ToArray()
            : continuation.Operations;
        var resumedContinuation = continuation with
        {
            Operations = remainingOperations
        };
        var prototypeRng = state.Stochastic.PrototypeRng;
        var resumed = ResumeChoiceResolutionContinuation(
            state.Player with { PotionSlots = slots },
            combat with { PendingChoice = null }, prototypeRng,
            resumedContinuation);
        var updated = state with
        {
            Player = resumed.Player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng),
            World = world with { Combat = resumed.Combat }
        };
        return deferSemanticHandDraw
            ? PrototypeAutomaticPipelineResumeResultV111
                .FromDeferredSemanticHandDraw(updated)
            : ResumeAutomaticPipelineContinuation(updated);
    }
}
