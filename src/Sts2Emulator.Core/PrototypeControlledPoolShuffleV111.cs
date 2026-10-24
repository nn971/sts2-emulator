namespace Sts2Emulator.Core;

/// <summary>
/// Opt-in Nature continuation for source-shaped full-pool combat selections.
/// A whole eligible list is shuffled even when the effect takes one card.
/// Hook and outer operation effects executed before the first swap persist.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal static string[]? SourcePoolForShuffle(
        CombatState combat, PrototypeQueuedOperation op, long? sourceCardInstanceId)
    {
        if (op.Kind ==
            PrototypeCombatEffectKind.ReturnHandToDrawAndShuffle)
            return combat.DrawPile.Concat(combat.Hand).Select(id =>
            {
                var card = RequireCombatCard(combat, id);
                return "card-instance:" + card.InstanceId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                    ":" + card.CardId;
            }).ToArray();
        if (op.Kind == PrototypeCombatEffectKind.MoveRandomRareDrawCardsToHand)
            return combat.DrawPile.Where(id => PrototypeContent.Card(
                    RequireCombatCard(combat, id).CardId).Rarity ==
                    PrototypeCardRarity.Rare)
                .Select(id =>
                {
                    var card = RequireCombatCard(combat, id);
                    return "card-instance:" + card.InstanceId.ToString(
                        System.Globalization.CultureInfo.InvariantCulture) +
                        ":" + card.CardId;
                }).ToArray();
        if (op.Kind ==
            PrototypeCombatEffectKind.RecycleDrawPileForEffectDraw)
        {
            if (combat.Stochastic.SemanticDeck is not null ||
                combat.DrawPile.Length != 0 || combat.DiscardPile.Length <= 1)
                return null;
            return combat.DiscardPile.Select(id =>
            {
                var card = RequireCombatCard(combat, id);
                return "card-instance:" + card.InstanceId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                    ":" + card.CardId;
            }).ToArray();
        }
        if (op.Kind == PrototypeCombatEffectKind.ChooseCards &&
            op.Selection is { RandomCandidateCount: > 0 } selection)
        {
            var candidates = ControlledCardShortlistCandidates(combat, op);
            if (candidates is null ||
                candidates.Length <= selection.RandomCandidateCount)
                return null;
            return candidates.Select(id =>
            {
                var card = RequireCombatCard(combat, id);
                return "card-instance:" + card.InstanceId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                    ":" + card.CardId;
            }).ToArray();
        }

        if (op.Kind == PrototypeCombatEffectKind.CreateDistinctCharacterCommonCardsInHand)
            return CharacterCommonGenerationPool();
        if (op.Kind == PrototypeCombatEffectKind.CreateDistinctColorlessCardsInHand)
        {
            var sourceId = sourceCardInstanceId is null ? null
                : RequireCombatCard(combat, sourceCardInstanceId.Value).CardId;
            return DistinctColorlessGenerationPool(combat, sourceId);
        }

        if (op.Kind is not (PrototypeCombatEffectKind.ChooseGeneratedCards or
            PrototypeCombatEffectKind.ChooseGeneratedColorlessCards))
            return null;

        var source = op.Kind == PrototypeCombatEffectKind.ChooseGeneratedColorlessCards
            ? ColorlessCombatGenerationPool(combat)
            : op.GeneratedChoiceCharacterCards
                ? PrototypeContent.NativeSilentCardPool
                : PrototypeContent.RewardCardPool;
        return source.Where(id =>
        {
            var definition = PrototypeContent.Card(id);
            return (op.GeneratedChoiceCardType is null
                    || definition.Type == op.GeneratedChoiceCardType.Value)
                && definition.CanBeGeneratedInCombat
                && (!op.GeneratedChoiceCharacterCards ||
                    definition.Rarity is not (
                        PrototypeCardRarity.Basic or PrototypeCardRarity.Ancient or
                        PrototypeCardRarity.Event))
                && definition.MechanicsImplemented && !definition.MultiplayerOnly;
        }).ToArray();
    }

    internal static string[] CharacterCommonGenerationPool() =>
        PrototypeContent.NativeSilentCardPool.Where(id =>
        {
            var card = PrototypeContent.Card(id);
            return card.Rarity == PrototypeCardRarity.Common
                && card.MechanicsImplemented && card.CanBeGeneratedInCombat
                && !card.MultiplayerOnly;
        }).ToArray();

    internal static string[] DistinctColorlessGenerationPool(
        CombatState combat, string? sourceCardId)
    {
        return ColorlessCombatGenerationPool(combat)
            .Where(id => !StringComparer.Ordinal.Equals(id, sourceCardId))
            .ToArray();
    }

    private static string[] ColorlessCombatGenerationPool(
        CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(combat);
        if (combat.Stochastic.NativeRng is null)
            return PrototypeColorlessCards.ImplementedCombatGenerationPool;
        if (combat.ConfiguredColorlessGenerationFullyUnlocked is not
            { } fullyUnlocked)
            throw new NotSupportedException(
                "Native Colorless generation requires configured run profile " +
                "data to resolve epoch-gated cards.");
        return PrototypeColorlessCards.CombatGenerationPoolForProfile(
            fullyUnlocked, combat.ConfiguredUnlockedColorlessEpochs);
    }

    private static int ControlledPoolSelectedCount(
        CombatState combat, PrototypeQueuedOperation op, int poolCount)
    {
        var count = op.Kind switch
        {
            PrototypeCombatEffectKind.CreateDistinctCharacterCommonCardsInHand
                when op.SourcePowerApplicationOrder is { } order =>
                combat.PlayerPowers.Single(power =>
                    power.ApplicationOrder == order).StoredValue,
            PrototypeCombatEffectKind.CreateDistinctColorlessCardsInHand =>
                Math.Min(Math.Max(0, op.Amount),
                    PrototypeContent.Rules.RemainingHandCapacity(
                        combat.Hand.Length)),
            PrototypeCombatEffectKind.MoveRandomRareDrawCardsToHand =>
                Math.Min(Math.Max(0, op.Amount),
                    PrototypeContent.Rules.RemainingHandCapacity(
                        combat.Hand.Length)),
            PrototypeCombatEffectKind.RecycleDrawPileForEffectDraw =>
                poolCount,
            PrototypeCombatEffectKind.ReturnHandToDrawAndShuffle => poolCount,
            PrototypeCombatEffectKind.ChooseCards =>
                op.Selection?.RandomCandidateCount ?? 0,
            _ => op.Amount
        };
        return Math.Min(Math.Max(0, count), poolCount);
    }

    private static bool TryMakeControlledPoolShuffleChoice(
        CombatState combat, PrototypeQueuedOperation operation,
        long? sourceCardInstanceId,
        PrototypeChoiceResolutionContinuationState continuation,
        out CombatState suspended)
    {
        suspended = combat;
        if (!ControlledChanceGuardV111.Active ||
            ControlledChanceGuardV111.EngineOwnedNativeProviderActive)
            return false;
        if (operation.Kind ==
                PrototypeCombatEffectKind.MoveRandomRareDrawCardsToHand &&
            combat.Stochastic.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode })
            return false;
        if (operation.Kind == PrototypeCombatEffectKind.ChooseCards &&
            combat.Stochastic.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode })
            return false;
        var pool = SourcePoolForShuffle(
            combat, operation, sourceCardInstanceId);
        if (pool is null || pool.Length <= 1)
            return false;
        var count = ControlledPoolSelectedCount(combat, operation, pool.Length);
        if (count == 0)
            return false;
        if (combat.PendingChoice is not null ||
            pool.Distinct(StringComparer.Ordinal).Count() != pool.Length)
            throw new InvalidOperationException(
                "Controlled pool shuffle requires a unique live candidate pool.");
        suspended = combat with
        {
            PendingChoice = new PendingCombatChoiceState(
                ChoiceId: "controlled_pool_shuffle",
                SourceCardInstanceId: continuation.SourceCardInstanceId,
                SourceCardDestination: continuation.SourceCardDestination,
                Selection: new PrototypeCardSelectionSpec(
                    PrototypeCardZone.Hand, 0, 0,
                    PrototypeCardSelectionResolutionKind.MoveToDiscard),
                CandidateCardInstanceIds: [],
                Continuation: [],
                CompletionEvents: [],
                OuterChoiceContinuation: continuation.Fork(),
                ControlledPoolShuffle: new PrototypeControlledPoolShuffleV111(
                    operation with { }, (string[])pool.Clone(),
                    (string[])pool.Clone(), pool.Length - 1, count))
        };
        return true;
    }

    internal static bool ValidateControlledPoolShuffle(
        CombatState combat, PrototypeControlledPoolShuffleV111 pool,
        long? sourceCardInstanceId)
    {
        if (pool.OriginalCardIds is null || pool.CurrentCardIds is null ||
            pool.NextSwapIndex < 1 ||
            pool.NextSwapIndex >= pool.OriginalCardIds.Length ||
            pool.CurrentCardIds.Length != pool.OriginalCardIds.Length ||
            pool.SelectedCount <= 0)
            return false;
        string[]? source;
        int expectedCount;
        try
        {
            source = SourcePoolForShuffle(
                combat, pool.Operation, sourceCardInstanceId);
            expectedCount = ControlledPoolSelectedCount(
                combat, pool.Operation, source?.Length ?? 0);
        }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException)
        {
            return false;
        }
        return source is not null &&
            pool.OriginalCardIds.SequenceEqual(source) &&
            pool.SelectedCount == expectedCount &&
            pool.CurrentCardIds.Order(StringComparer.Ordinal)
                .SequenceEqual(source.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Select one source-order swap and continue from that exact position.
    /// After the final swap, construct generated cards exactly once and
    /// resume the previously saved operations/choice logic.
    /// </summary>
    internal static RunState ResumeControlledPoolShuffle(
        RunState state, int chosen)
        => ResumeControlledPoolShuffleWithBoundary(state, chosen).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledPoolShuffleWithBoundary(
            RunState state, int chosen)
        => ResumeControlledPoolShuffleCore(state, chosen);

    private static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledPoolShuffleCore(
        RunState state, int chosen)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Controlled pool shuffle requires the RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ??
            throw new InvalidOperationException("Missing combat in pool shuffle.");
        var pending = combat.PendingChoice ??
            throw new InvalidOperationException("No pending pool shuffle.");
        if (pending.ChoiceId != "controlled_pool_shuffle" ||
            pending.ControlledPoolShuffle is not { } shuffle ||
            pending.OuterChoiceContinuation is not { } continuation ||
            !ValidateControlledPoolShuffle(combat, shuffle, pending.SourceCardInstanceId))
            throw new InvalidDataException("Saved pool shuffle is not source-reachable.");
        if (chosen < 0 || chosen > shuffle.NextSwapIndex)
            throw new ArgumentOutOfRangeException(nameof(chosen));
        var values = (string[])shuffle.CurrentCardIds.Clone();
        (values[shuffle.NextSwapIndex], values[chosen]) =
            (values[chosen], values[shuffle.NextSwapIndex]);
        if (shuffle.NextSwapIndex > 1)
            return PrototypeAutomaticPipelineResumeResultV111.FromRun(
                state with
                {
                    World = world with
                    {
                        Combat = combat with
                        {
                            PendingChoice = pending with
                            {
                                ControlledPoolShuffle = shuffle with
                                {
                                    CurrentCardIds = values,
                                    NextSwapIndex = shuffle.NextSwapIndex - 1
                                }
                            }
                        }
                    }
                });

        combat = combat with { PendingChoice = null };
        var prototypeRng = state.Stochastic.PrototypeRng;
        var op = shuffle.Operation;
        if (op.Kind is (PrototypeCombatEffectKind.ChooseGeneratedCards or
            PrototypeCombatEffectKind.ChooseGeneratedColorlessCards))
        {
            var ids = new List<long>();
            foreach (var cardId in values.Take(shuffle.SelectedCount))
            {
                var card = new CombatCardInstance(
                    InstanceId: combat.NextCardInstanceId,
                    PersistentCardInstanceId: null,
                    CardId: cardId,
                    UpgradeLevel: op.GeneratedChoiceCardsUpgraded ? 1 : 0,
                    IsTemporary: true,
                    State: PrototypeJson.EmptyObject(),
                    TemporaryEnergyCost: op.GeneratedChoiceCardsFreeThisTurn
                        ? new PrototypeTemporaryCardCost(0,
                            PrototypeTemporaryCardCostExpiry.EndOfTurn |
                            PrototypeTemporaryCardCostExpiry.WhenPlayed)
                        : null,
                    KeywordOverrides: op.GeneratedChoiceCardsRetain
                        ? [new PrototypeCardKeywordOverride(
                            PrototypeCardKeyword.Retain, true,
                            PrototypeCardKeywordOverrideExpiry.None)]
                        : null);
                card = ApplyActiveSourceBoundAfflictionToCard(combat, card);
                ids.Add(card.InstanceId);
                combat = SemanticDrawPileAdapterV111
                    .ApplyNewCardZoneInsertion(
                        combat, PrototypeCardZone.ChoicePool,
                        combat.ChoiceCardIds.Length, card);
            }
            combat = combat with
            {
                PendingChoice = new PendingCombatChoiceState(
                    ChoiceId: "select_cards",
                    SourceCardInstanceId: continuation.SourceCardInstanceId,
                    SourceCardDestination: continuation.SourceCardDestination,
                    Selection: new PrototypeCardSelectionSpec(
                        PrototypeCardZone.ChoicePool,
                        op.GeneratedChoiceMustPick ? 1 : 0, 1,
                        PrototypeCardSelectionResolutionKind.MoveToHand,
                        RemoveUnselectedFromSource: true),
                    CandidateCardInstanceIds: ids.ToArray(),
                    Continuation: (PrototypeQueuedOperation[])
                        continuation.Operations.Clone(),
                    CompletionEvents: (PrototypeCombatEvent[])
                        continuation.CompletionEvents.Clone(),
                    CardPlaySeries: continuation.CardPlaySeries,
                    MoveSourceCardOnCompletion: continuation.MoveSourceCardOnCompletion,
                    RemoveSourceCardOnCompletion: continuation.RemoveSourceCardOnCompletion,
                    SourceCardAlreadyMoved: continuation.SourceCardAlreadyMoved,
                    EventDispatchContinuation:
                        continuation.EventDispatchContinuation?.Fork())
            };
            return PrototypeAutomaticPipelineResumeResultV111.FromRun(
                state with { World = world with { Combat = combat } });
        }

        if (op.Kind == PrototypeCombatEffectKind.ChooseCards)
        {
            var selection = op.Selection ?? throw new InvalidDataException(
                "Controlled random shortlist lost its selection operation.");
            var candidates = values.Take(shuffle.SelectedCount)
                .Select(ParseShuffledCardInstanceToken).ToArray();
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

            var capacity = selection.Resolution ==
                    PrototypeCardSelectionResolutionKind.MoveToHand
                ? PrototypeContent.Rules.RemainingHandCapacity(
                    combat.Hand.Length)
                : int.MaxValue;
            var maximum = Math.Min(
                Math.Min(selection.MaxSelections, candidates.Length), capacity);
            if (candidates.Length == 0 || maximum <= 0)
            {
                var resumed = ResumeChoiceResolutionContinuation(
                    state.Player, combat, prototypeRng, continuation);
                var updated = state with
                {
                    Player = resumed.Player,
                    Stochastic = RunStochasticStateV111.WithPrototypeRng(
                        state.Stochastic, prototypeRng),
                    World = world with { Combat = resumed.Combat }
                };
                return ResumeAutomaticPipelineContinuation(updated);
            }

            var effective = selection with
            {
                MinSelections = Math.Min(selection.MinSelections, maximum),
                MaxSelections = maximum
            };
            combat = combat with
            {
                PendingChoice = new PendingCombatChoiceState(
                    ChoiceId: "select_cards",
                    SourceCardInstanceId: continuation.SourceCardInstanceId,
                    SourceCardDestination: continuation.SourceCardDestination,
                    Selection: effective,
                    CandidateCardInstanceIds: candidates,
                    Continuation: (PrototypeQueuedOperation[])
                        continuation.Operations.Clone(),
                    CompletionEvents: (PrototypeCombatEvent[])
                        continuation.CompletionEvents.Clone(),
                    CardPlaySeries: continuation.CardPlaySeries,
                    MoveSourceCardOnCompletion:
                        continuation.MoveSourceCardOnCompletion,
                    RemoveSourceCardOnCompletion:
                        continuation.RemoveSourceCardOnCompletion,
                    SourceCardAlreadyMoved:
                        continuation.SourceCardAlreadyMoved,
                    SelectedCardPower: op.SelectedCardPower,
                    SelectedCardKeyword: op.SelectedCardKeyword,
                    SelectedCardTemporaryCost:
                        op.SelectedCardTemporaryCost is null
                            ? null : op.SelectedCardTemporaryCost with { },
                    EventDispatchContinuation:
                        continuation.EventDispatchContinuation?.Fork(),
                    SemanticStratagemDrawSelection:
                        op.SemanticStratagemDrawSelection,
                    AutomaticSemanticStratagemDrawSelection:
                        op.AutomaticSemanticStratagemDrawSelection)
            };
            return PrototypeAutomaticPipelineResumeResultV111.FromRun(
                state with { World = world with { Combat = combat } });
        }

        if (op.Kind ==
            PrototypeCombatEffectKind.RecycleDrawPileForEffectDraw)
        {
            var shuffled = values.Select(ParseShuffledCardInstanceToken)
                .ToArray();
            var recycled = (long[])combat.DiscardPile.Clone();
            if (combat.Stochastic.SemanticDeck is not null ||
                combat.DrawPile.Length != 0 || recycled.Length <= 1 ||
                shuffled.Distinct().Count() != shuffled.Length ||
                !shuffled.Order().SequenceEqual(recycled.Order()))
                throw new InvalidDataException(
                    "Legacy controlled recycle changed its discard identity set.");
            var postHook = NativeShuffleStepReplayV111.CheckPostHook(
                ApplyPerfectFitShuffleOrder(combat, shuffled));
            combat = SemanticDrawPileAdapterV111.CommitLegacyRecycle(
                combat, postHook);
            var afterShuffleInsertions =
                ControlledAfterShuffleRelicInsertions(combat);
            if (afterShuffleInsertions.Length > 0 &&
                ControlledChanceGuardV111.Active &&
                !ControlledChanceGuardV111.EngineOwnedNativeProviderActive)
            {
                var nextCombat = MakeControlledAfterShuffleInsertionChoice(
                    combat, afterShuffleInsertions[0],
                    afterShuffleInsertions.Skip(1).ToArray(), [], null,
                    continuation, ordinal: 0);
                return PrototypeAutomaticPipelineResumeResultV111.FromRun(
                    state with
                    {
                        World = world with { Combat = nextCombat }
                    });
            }
            if (afterShuffleInsertions.Length > 0)
                combat = ApplyRelicAfterShuffleCards(combat, prototypeRng);
        }
        else if (op.Kind ==
            PrototypeCombatEffectKind.MoveRandomRareDrawCardsToHand)
        {
            var selected = values.Take(shuffle.SelectedCount)
                .Select(ParseShuffledCardInstanceToken).ToArray();
            if (selected.Distinct().Count() != selected.Length ||
                selected.Any(id => !combat.DrawPile.Contains(id)))
                throw new InvalidDataException(
                    "Rare-card shuffle selected a stale draw-pile identity.");
            var selectedSet = selected.ToHashSet();
            var nextZones = combat.Mechanics.CardZones.WithZoneChanges(
                new Dictionary<PrototypeCardZone, long[]>
                {
                    [PrototypeCardZone.DrawPile] = combat.DrawPile
                        .Where(id => !selectedSet.Contains(id)).ToArray(),
                    [PrototypeCardZone.Hand] = combat.Hand
                        .Concat(selected).ToArray()
                });
            combat = SemanticDrawPileAdapterV111
                .ApplyDrawPileRemoval(combat, selected, nextZones);
        }
        else if (op.Kind ==
            PrototypeCombatEffectKind.ReturnHandToDrawAndShuffle)
        {
            var shuffled = values.Select(ParseShuffledCardInstanceToken)
                .ToArray();
            var returnedHandIds = (long[])combat.Hand.Clone();
            if (shuffled.Distinct().Count() != shuffled.Length ||
                !shuffled.Order().SequenceEqual(
                    combat.DrawPile.Concat(returnedHandIds).Order()))
                throw new InvalidDataException(
                    "Hand-return shuffle changed or duplicated card identities.");
            combat = combat with { PendingChoice = null };
            var shuffledDrawPile = ApplyPerfectFitShuffleOrder(
                combat, shuffled);
            combat = SemanticDrawPileAdapterV111.CommitHandReturnShuffle(
                combat, shuffledDrawPile, returnedHandIds);
            var afterShuffleInsertions =
                ControlledAfterShuffleRelicInsertions(combat);
            if (afterShuffleInsertions.Length > 0 &&
                ControlledChanceGuardV111.Active &&
                !ControlledChanceGuardV111.EngineOwnedNativeProviderActive)
            {
                var nextCombat = MakeControlledAfterShuffleInsertionChoice(
                    combat, afterShuffleInsertions[0],
                    afterShuffleInsertions.Skip(1).ToArray(), [], null,
                    continuation, ordinal: 0);
                return PrototypeAutomaticPipelineResumeResultV111.FromRun(
                    state with
                    {
                        World = world with { Combat = nextCombat }
                    });
            }
            if (afterShuffleInsertions.Length > 0)
                combat = ApplyRelicAfterShuffleCards(combat, prototypeRng);
        }
        else
        {
            foreach (var cardId in values.Take(shuffle.SelectedCount))
            {
                if (op.Kind == PrototypeCombatEffectKind.CreateDistinctColorlessCardsInHand)
                {
                    if (!PrototypeContent.Rules.HasHandCapacity(
                            combat.Hand.Length))
                        break;
                    var card = new CombatCardInstance(
                        combat.NextCardInstanceId, null, cardId, 0, true,
                        PrototypeJson.EmptyObject());
                    card = ApplyActiveSourceBoundAfflictionToCard(combat, card);
                    combat = SemanticDrawPileAdapterV111
                        .ApplyNewCardZoneInsertion(
                            combat, PrototypeCardZone.Hand,
                            combat.Hand.Length, card);
                }
                else
                    combat = AddGeneratedCardCopies(combat,
                        new PrototypeCombatCardSnapshot(
                            cardId, 0, PrototypeJson.EmptyObject()), 1);
            }
        }
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

    private static long ParseShuffledCardInstanceToken(string token)
    {
        const string prefix = "card-instance:";
        var end = token.IndexOf(':', prefix.Length);
        if (!token.StartsWith(prefix, StringComparison.Ordinal) ||
            end < 0 || !long.TryParse(
                token.AsSpan(prefix.Length, end - prefix.Length),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var instanceId) || instanceId <= 0)
            throw new InvalidDataException(
                "Rare-card shuffle contains an invalid instance token.");
        return instanceId;
    }

    /// <summary>
    /// Narrow internal diagnostic entry for operations absent from a natural
    /// Silent starter fixture. It uses the actual effect kernel, not copied
    /// mechanics, so source eligibility and hooks are shared.
    /// </summary>
    internal static RunState BeginControlledPoolShuffleFixture(
        RunState state, PrototypeQueuedOperation operation)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Controlled pool fixture requires the RNG guard.");
        if (operation.Kind is not (
            PrototypeCombatEffectKind.CreateDistinctCharacterCommonCardsInHand or
            PrototypeCombatEffectKind.CreateDistinctColorlessCardsInHand or
            PrototypeCombatEffectKind.ChooseGeneratedCards or
            PrototypeCombatEffectKind.ChooseGeneratedColorlessCards))
            throw new ArgumentException("Expected a full-pool generation shuffle.");
        var world = RequireWorld(state);
        var combat = world.Combat ??
            throw new InvalidOperationException("No combat for fixture.");
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
