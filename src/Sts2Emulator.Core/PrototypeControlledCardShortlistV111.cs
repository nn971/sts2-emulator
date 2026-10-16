namespace Sts2Emulator.Core;

/// <summary>Explicit continuation for source pile selectors that first
/// shuffle and take a random ordered prefix of eligible card instances.</summary>
public sealed partial class PrototypeGameEngine
{
    internal static long[]? ControlledCardShortlistCandidates(
        CombatState combat, PrototypeQueuedOperation operation)
    {
        if (operation.Kind ==
            PrototypeCombatEffectKind.AutoPlayRandomCardsFromZone)
        {
            var zone = operation.AutoPlaySourceZone ??
                throw new InvalidDataException(
                    "Random auto-play has no source pile.");
            var ids = GetZone(combat, zone);
            var eligible = ids.Where(id =>
            {
                var definition = PrototypeContent.Card(
                    RequireCombatCard(combat, id).CardId);
                return !definition.Unplayable &&
                    definition.MechanicsImplemented &&
                    (operation.AutoPlayRequiredCardType is null ||
                     definition.Type == operation.AutoPlayRequiredCardType);
            }).ToArray();
            return eligible.Length == 0 &&
                    operation.AutoPlayFallbackToUnplayable
                ? (long[])ids.Clone()
                : eligible;
        }

        if (operation.Kind ==
            PrototypeCombatEffectKind.EmpowerRandomDrawCardReplay)
        {
            var eligible = combat.DrawPile.Where(id =>
            {
                var card = RequireCombatCard(combat, id);
                var definition = PrototypeContent.Card(card.CardId);
                return !definition.Unplayable &&
                    definition.Type is not (
                        PrototypeCardType.Curse or PrototypeCardType.Status) &&
                    card.ReplayCount == 0 &&
                    card.Enchantment is not
                        { Kind: PrototypeCardEnchantmentKind.Spiral or
                            PrototypeCardEnchantmentKind.Glam };
            }).ToArray();
            var preferred = eligible.Where(id =>
                PrototypeContent.Card(
                    RequireCombatCard(combat, id).CardId).Type is
                    PrototypeCardType.Attack or PrototypeCardType.Skill or
                    PrototypeCardType.Power).ToArray();
            return preferred.Length > 0 ? preferred : eligible;
        }

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

    private static int ControlledCardShortlistRequiredCount(
        PrototypeQueuedOperation operation) =>
        (operation.Kind ==
                PrototypeCombatEffectKind.AutoPlayRandomCardsFromZone &&
            operation.Amount > 0) ||
        operation.Kind ==
            PrototypeCombatEffectKind.EmpowerRandomDrawCardReplay
            ? 1
            : operation.Selection?.RandomCandidateCount ?? 0;

    internal static bool TryMakeControlledCardShortlistChoice(
        CombatState combat, PrototypeQueuedOperation operation,
        long[] sourceCandidates,
        PrototypeChoiceResolutionContinuationState continuation,
        out CombatState suspended)
    {
        suspended = combat;
        var requiredCount = ControlledCardShortlistRequiredCount(operation);
        if (!ControlledChanceGuardV111.Active ||
            ControlledChanceGuardV111.EngineOwnedNativeProviderActive ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode } ||
            combat.AutomaticPipelineContinuation is not null ||
            requiredCount <= 0 ||
            sourceCandidates.Length <= requiredCount)
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
                Selection: operation.Selection ?? new PrototypeCardSelectionSpec(
                    operation.AutoPlaySourceZone ??
                        PrototypeCardZone.DrawPile,
                    0, 0,
                    PrototypeCardSelectionResolutionKind.MoveToDiscard),
                CandidateCardInstanceIds: [],
                Continuation: [],
                CompletionEvents: [],
                OuterChoiceContinuation: continuation.Fork(),
                ControlledCardShortlist:
                    new PrototypeControlledCardShortlistV111(
                        operation with { },
                        (long[])sourceCandidates.Clone(),
                        (long[])sourceCandidates.Clone(), [],
                        requiredCount))
        };
        return true;
    }

    internal static bool ValidateControlledCardShortlist(
        CombatState combat, PrototypeControlledCardShortlistV111 shortlist)
    {
        var requiredCount = ControlledCardShortlistRequiredCount(
            shortlist.Operation);
        if (requiredCount <= 0 ||
            shortlist.RequiredCount != requiredCount ||
            shortlist.OriginalCandidateCardInstanceIds is not { Length: > 1 } ||
            shortlist.RemainingCandidateCardInstanceIds is null ||
            shortlist.SelectedCandidateCardInstanceIds is null ||
            shortlist.OriginalCandidateCardInstanceIds.Length <=
                requiredCount ||
            shortlist.SelectedCandidateCardInstanceIds.Length < 0 ||
            shortlist.SelectedCandidateCardInstanceIds.Length >=
                requiredCount)
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

        if (shortlist.Operation.Kind ==
            PrototypeCombatEffectKind.AutoPlayRandomCardsFromZone)
        {
            if (selectedPrefix.Length != 1 ||
                shortlist.Operation.Amount <= 0 ||
                shortlist.Operation.AutoPlaySourceZone is not { } sourceZone)
                throw new InvalidDataException(
                    "Random auto-play shortlist has an invalid source operation.");
            combat = combat with { PendingChoice = null };
            if (sourceZone == PrototypeCardZone.DrawPile)
            {
                var deck = combat.SemanticDeck ??
                    throw new InvalidDataException(
                        "Draw-pile auto-play lost its semantic deck.");
                combat = combat with
                {
                    SemanticDeck = SemanticDeckEngineV111.RemoveCardInstance(
                        deck, selected)
                };
            }
            combat = SetZone(combat, sourceZone,
                GetZone(combat, sourceZone).Where(id => id != selected).ToArray());
            combat = SetZone(combat, PrototypeCardZone.PlayPile,
                combat.PlayCardIds.Append(selected).ToArray());
            var resumedOperations = new List<PrototypeQueuedOperation>
            {
                new(PrototypeCombatEffectKind.AutoPlayCombatCard, 0,
                    SourceKind: shortlist.Operation.SourceKind,
                    CardInstanceId: selected)
            };
            if (shortlist.Operation.Amount > 1)
                resumedOperations.Add(shortlist.Operation with
                {
                    Amount = shortlist.Operation.Amount - 1
                });
            resumedOperations.AddRange(continuation.Operations);
            var autoplay = ResumeChoiceResolutionContinuation(
                state.Player, combat, state.Rng, continuation with
                {
                    Operations = resumedOperations.ToArray()
                });
            return ResumeAfterCardShortlist(state, world, autoplay);
        }

        if (shortlist.Operation.Kind ==
            PrototypeCombatEffectKind.EmpowerRandomDrawCardReplay)
        {
            if (selectedPrefix.Length != 1)
                throw new InvalidDataException(
                    "Random replay-card shortlist has an invalid prefix.");
            combat = combat with
            {
                PendingChoice = null,
                Cards = combat.Cards.Select(card =>
                    card.InstanceId == selected
                        ? card with
                        {
                            ReplayCount = card.ReplayCount +
                                Math.Max(0, shortlist.Operation.Amount)
                        }
                        : card).ToArray()
            };
            var resumed = ResumeChoiceResolutionContinuation(
                state.Player, combat, state.Rng, continuation with
                {
                    Operations = continuation.Operations.ToArray()
                });
            return ResumeAfterCardShortlist(state, world, resumed);
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
