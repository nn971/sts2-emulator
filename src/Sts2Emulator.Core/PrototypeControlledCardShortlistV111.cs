namespace Sts2Emulator.Core;

/// <summary>Explicit continuation for source pile selectors that first
/// shuffle and take a random ordered prefix of eligible card instances.</summary>
public sealed partial class PrototypeGameEngine
{
    internal static long[]? ControlledCardShortlistCandidates(
        CombatState combat, PrototypeQueuedOperation operation)
    {
        if (operation.Kind != PrototypeCombatEffectKind.ChooseCards ||
            operation.Selection is not { RandomCandidateCount: > 0 } selection)
            return null;

        var candidates = GetZone(combat, selection.SourceZone);
        if (selection.Resolution ==
            PrototypeCardSelectionResolutionKind.TransformRandom)
            candidates = candidates.Where(id =>
                !PrototypeCardRules.IsEternal(RequireCombatCard(combat, id)))
                .ToArray();
        if (selection.RequireAttackOrPower)
            candidates = candidates.Where(id =>
                PrototypeContent.Card(
                    RequireCombatCard(combat, id).CardId).Type is
                    PrototypeCardType.Attack or PrototypeCardType.Power)
                .ToArray();
        if (selection.RequiredCardType is { } requiredType)
            candidates = candidates.Where(id =>
                PrototypeContent.Card(
                    RequireCombatCard(combat, id).CardId).Type == requiredType)
                .ToArray();
        return (long[])candidates.Clone();
    }

    internal static bool TryMakeControlledCardShortlistChoice(
        CombatState combat, PrototypeQueuedOperation operation,
        long[] sourceCandidates,
        PrototypeChoiceResolutionContinuationState continuation,
        out CombatState suspended)
    {
        suspended = combat;
        if (!ControlledChanceGuardV111.Active ||
            ControlledChanceGuardV111.EngineOwnedNativeProviderActive ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode } ||
            operation.Selection is not { RandomCandidateCount: > 0 } selection ||
            sourceCandidates.Length <= selection.RandomCandidateCount)
            return false;
        if (combat.PendingChoice is not null ||
            sourceCandidates.Distinct().Count() != sourceCandidates.Length)
            throw new InvalidOperationException(
                "Controlled card shortlist requires a unique source pile.");
        suspended = combat with
        {
            PendingChoice = new PendingCombatChoiceState(
                ChoiceId: "controlled_card_shortlist",
                SourceCardInstanceId: continuation.SourceCardInstanceId,
                SourceCardDestination: continuation.SourceCardDestination,
                Selection: selection,
                CandidateCardInstanceIds: [],
                Continuation: [],
                CompletionEvents: [],
                OuterChoiceContinuation: continuation.Fork(),
                ControlledCardShortlist:
                    new PrototypeControlledCardShortlistV111(
                        operation with { },
                        (long[])sourceCandidates.Clone(),
                        (long[])sourceCandidates.Clone(), [],
                        selection.RandomCandidateCount))
        };
        return true;
    }

    internal static bool ValidateControlledCardShortlist(
        CombatState combat, PrototypeControlledCardShortlistV111 shortlist)
    {
        if (shortlist.Operation.Selection is not
                { RandomCandidateCount: > 0 } selection ||
            shortlist.RequiredCount != selection.RandomCandidateCount ||
            shortlist.OriginalCandidateCardInstanceIds is not { Length: > 1 } ||
            shortlist.RemainingCandidateCardInstanceIds is null ||
            shortlist.SelectedCandidateCardInstanceIds is null ||
            shortlist.OriginalCandidateCardInstanceIds.Length <=
                shortlist.RequiredCount ||
            shortlist.SelectedCandidateCardInstanceIds.Length < 0 ||
            shortlist.SelectedCandidateCardInstanceIds.Length >=
                shortlist.RequiredCount)
            return false;
        long[]? source;
        try
        {
            source = ControlledCardShortlistCandidates(
                combat, shortlist.Operation);
        }
        catch (Exception e) when (e is InvalidOperationException or
            KeyNotFoundException or ArgumentOutOfRangeException)
        {
            return false;
        }
        var remaining = shortlist.OriginalCandidateCardInstanceIds
            .Where(id => !shortlist.SelectedCandidateCardInstanceIds.Contains(id))
            .ToArray();
        return source is not null &&
            source.SequenceEqual(shortlist.OriginalCandidateCardInstanceIds) &&
            shortlist.SelectedCandidateCardInstanceIds.Distinct().Count() ==
                shortlist.SelectedCandidateCardInstanceIds.Length &&
            shortlist.SelectedCandidateCardInstanceIds.All(
                shortlist.OriginalCandidateCardInstanceIds.Contains) &&
            shortlist.RemainingCandidateCardInstanceIds.SequenceEqual(remaining);
    }

    internal static RunState ResumeControlledCardShortlist(
        RunState state, int selectedIndex)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Controlled card shortlist requires the RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ??
            throw new InvalidOperationException(
                "Missing combat in card-shortlist continuation.");
        var pending = combat.PendingChoice ??
            throw new InvalidOperationException(
                "No pending card shortlist.");
        if (pending.ChoiceId != "controlled_card_shortlist" ||
            pending.ControlledCardShortlist is not { } shortlist ||
            pending.OuterChoiceContinuation is not { } continuation ||
            !ValidateControlledCardShortlist(combat, shortlist))
            throw new InvalidDataException(
                "Saved card shortlist is not source-reachable.");
        if (selectedIndex < 0 ||
            selectedIndex >= shortlist.RemainingCandidateCardInstanceIds.Length)
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));

        var selected = shortlist.RemainingCandidateCardInstanceIds[selectedIndex];
        var selectedPrefix = shortlist.SelectedCandidateCardInstanceIds
            .Append(selected).ToArray();
        var remaining = shortlist.RemainingCandidateCardInstanceIds
            .Where(id => id != selected).ToArray();
        if (selectedPrefix.Length < shortlist.RequiredCount)
        {
            var nextPending = pending with
            {
                ControlledCardShortlist = shortlist with
                {
                    SelectedCandidateCardInstanceIds = selectedPrefix,
                    RemainingCandidateCardInstanceIds = remaining
                }
            };
            return state with
            {
                World = world with
                {
                    Combat = combat with { PendingChoice = nextPending }
                }
            };
        }

        combat = combat with { PendingChoice = null };
        var selection = shortlist.Operation.Selection!;
        var candidates = selectedPrefix;
        if (selection.RequireEnergyCostingCard)
            candidates = candidates.Where(id =>
            {
                var card = RequireCombatCard(combat, id);
                var definition = PrototypeContent.Card(card.CardId);
                return !definition.Unplayable &&
                    (definition.Cost.Kind == PrototypeCardCostKind.X ||
                     definition.Cost.Kind == PrototypeCardCostKind.Fixed &&
                     definition.Cost.Amount >= 0);
            }).ToArray();

        const int maxHandSize = 10;
        var capacity = selection.Resolution ==
                PrototypeCardSelectionResolutionKind.MoveToHand
            ? Math.Max(0, maxHandSize - combat.Hand.Length)
            : int.MaxValue;
        var maximum = Math.Min(
            Math.Min(selection.MaxSelections, candidates.Length), capacity);
        if (candidates.Length == 0 || maximum <= 0)
        {
            var resumed = ResumeChoiceResolutionContinuation(
                state.Player, combat, state.Rng, continuation);
            return ResumeAfterCardShortlist(state, world, resumed);
        }

        var effective = selection with
        {
            RandomCandidateCount = 0,
            MinSelections = Math.Min(selection.MinSelections, maximum),
            MaxSelections = maximum
        };
        var choice = new PendingCombatChoiceState(
            ChoiceId: "select_cards",
            SourceCardInstanceId: continuation.SourceCardInstanceId,
            SourceCardDestination: continuation.SourceCardDestination,
            Selection: effective,
            CandidateCardInstanceIds: (long[])candidates.Clone(),
            Continuation: (PrototypeQueuedOperation[])
                continuation.Operations.Clone(),
            CompletionEvents: (PrototypeCombatEvent[])
                continuation.CompletionEvents.Clone(),
            CardPlaySeries: continuation.CardPlaySeries,
            MoveSourceCardOnCompletion:
                continuation.MoveSourceCardOnCompletion,
            RemoveSourceCardOnCompletion:
                continuation.RemoveSourceCardOnCompletion,
            SourceCardAlreadyMoved: continuation.SourceCardAlreadyMoved,
            SelectedCardPower: shortlist.Operation.SelectedCardPower,
            SelectedCardKeyword: shortlist.Operation.SelectedCardKeyword,
            SelectedCardTemporaryCost:
                shortlist.Operation.SelectedCardTemporaryCost is null
                    ? null
                    : shortlist.Operation.SelectedCardTemporaryCost with { },
            EventDispatchContinuation:
                continuation.EventDispatchContinuation?.Fork(),
            OuterChoiceContinuation: continuation.Parent?.Fork());
        return state with
        {
            World = world with
            {
                Combat = combat with { PendingChoice = choice }
            }
        };
    }

    private static RunState ResumeAfterCardShortlist(
        RunState state, RunWorldState world,
        (PlayerState Player, CombatState Combat) resumed)
    {
        var updated = state with
        {
            Player = resumed.Player,
            World = world with { Combat = resumed.Combat }
        };
        if (resumed.Combat.PendingChoice is null &&
            resumed.Combat.AutomaticPipelineContinuation is { } automatic)
        {
            updated = updated with
            {
                World = updated.World! with
                {
                    Combat = resumed.Combat with
                    {
                        AutomaticPipelineContinuation = null
                    }
                }
            };
            updated = automatic.OpeningStage is { } openingStage
                ? ResumeOpeningCombat(updated, openingStage)
                : ResumeAutomaticPipeline(updated,
                    automatic.NextStepIndex,
                    automatic.PendingPostDispatchEventKind);
        }
        if (updated.Phase == RunPhase.Combat &&
            updated.World?.Combat is { } live && AllEnemiesDefeated(live))
            return EnterCombatReward(updated);
        return updated;
    }
}
