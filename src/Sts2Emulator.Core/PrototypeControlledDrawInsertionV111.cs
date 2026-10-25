namespace Sts2Emulator.Core;

/// <summary>
/// The original operation kernel supplies this continuation before consuming
/// the model/position RNG draws. Each branch materializes exactly one card
/// only after both required decisions are known.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal static string[] RandomDrawAttackModelPool() =>
        PrototypeContent.NativeSilentCardPool.Where(id =>
        {
            var card = PrototypeContent.Card(id);
            return card.Type == PrototypeCardType.Attack
                && card.Rarity is (PrototypeCardRarity.Common or
                    PrototypeCardRarity.Uncommon or PrototypeCardRarity.Rare)
                && card.MechanicsImplemented
                && !card.MultiplayerOnly
                && card.CanBeGeneratedInCombat;
        }).ToArray();

    private static CombatState MakeControlledDrawInsertionChoice(
        CombatState combat, PrototypeQueuedOperation operation,
        PrototypeChoiceResolutionContinuationState continuation,
        PrototypeControlledSemanticDrawV111? semanticDrawContinuation = null,
        PrototypeAfterShuffleRelicInsertionV111? afterShuffleRelic = null,
        PrototypeAfterShuffleRelicInsertionV111[]? completedAfterShuffleRelics = null,
        PrototypeAfterShuffleRelicInsertionV111[]? remainingAfterShuffleRelics = null,
        int requestOrdinal = 0,
        string? requestSiteId = null,
        string? requestOwnerId = null,
        PrototypePersonalHiveDamageContinuationV111?
            personalHiveDamageContinuation = null,
        int? automaticHandDrawRemaining = null)
    {
        if (!ControlledChanceGuardV111.Active ||
            combat.PendingChoice is not null || operation.Amount <= 0)
            throw new InvalidOperationException(
                "Controlled draw insertion requires one live source operation.");
        if (operation.Kind is not (
                PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions
                or PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile))
            throw new ArgumentOutOfRangeException(nameof(operation));
        string[] pool;
        string stage;
        string? selected;
        if (operation.Kind ==
            PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions)
        {
            if (operation.CardId is null)
                throw new InvalidOperationException(
                    "Generated draw-pile card ID is missing.");
            _ = PrototypeContent.Card(operation.CardId);
            pool = [];
            selected = operation.CardId;
            stage = "position";
        }
        else
        {
            pool = RandomDrawAttackModelPool();
            if (pool.Length == 0)
                throw new NotSupportedException(
                    "UNSUPPORTED_CONTROLLED_CHANCE site=combat.random_draw_attack empty_pool");
            selected = null;
            stage = "card_model";
        }

        return combat with
        {
            PendingChoice = new PendingCombatChoiceState(
                ChoiceId: "controlled_draw_insertion",
                SourceCardInstanceId: continuation.SourceCardInstanceId,
                SourceCardDestination: continuation.SourceCardDestination,
                Selection: new PrototypeCardSelectionSpec(
                    PrototypeCardZone.Hand, 0, 0,
                    PrototypeCardSelectionResolutionKind.MoveToDiscard),
                CandidateCardInstanceIds: [],
                Continuation: [],
                CompletionEvents: [],
                OuterChoiceContinuation: continuation.Fork(),
                ControlledDrawInsertion: new PrototypeControlledDrawInsertionV111(
                    operation with { }, stage, selected, pool,
                    (long[])combat.DrawPile.Clone(),
                    SemanticDrawContinuation: semanticDrawContinuation?.Fork(),
                    AfterShuffleRelic: afterShuffleRelic is null
                        ? null : afterShuffleRelic with { },
                    CompletedAfterShuffleRelics:
                        completedAfterShuffleRelics is null
                            ? null : completedAfterShuffleRelics
                                .Select(item => item with { }).ToArray(),
                    RemainingAfterShuffleRelics: remainingAfterShuffleRelics is null
                        ? null : remainingAfterShuffleRelics
                            .Select(item => item with { }).ToArray(),
                    RequestSiteId: requestSiteId ?? (afterShuffleRelic is null
                        ? StochasticSiteRegistryV111.DrawPileRandomInsertSiteId
                        : StochasticSiteRegistryV111.AfterShuffleRelicDrawPileInsertSiteId),
                    RequestOwnerId: requestOwnerId ?? (afterShuffleRelic is null
                        ? "prototype.combat"
                        : "relic:" + afterShuffleRelic.RelicId +
                            ":application:" + afterShuffleRelic.RelicApplicationOrder),
                    RequestOrdinal: requestOrdinal,
                    PersonalHiveDamageContinuation:
                        personalHiveDamageContinuation?.Fork(),
                    AutomaticHandDrawRemaining:
                        automaticHandDrawRemaining))
        };
    }

    private static bool ValidateAfterShuffleRelicInsertion(
        CombatState combat, PrototypeAfterShuffleRelicInsertionV111 insertion)
    {
        var relic = combat.RelicStates.SingleOrDefault(state =>
            state.ApplicationOrder == insertion.RelicApplicationOrder &&
            state.RelicId == insertion.RelicId && !state.IsMelted);
        return relic is not null &&
            PrototypeContent.Relic(relic.RelicId)
                .AfterShuffleGeneratedCardId == insertion.CardId;
    }

    private static PrototypeAfterShuffleRelicInsertionV111[]
        ControlledAfterShuffleRelicInsertions(CombatState combat) =>
        combat.RelicStates.Where(relic => !relic.IsMelted)
            .OrderBy(relic => relic.ApplicationOrder)
            .Select(relic => new
            {
                Relic = relic,
                CardId = PrototypeContent.Relic(relic.RelicId)
                    .AfterShuffleGeneratedCardId
            })
            .Where(item => item.CardId is not null)
            .Select(item => new PrototypeAfterShuffleRelicInsertionV111(
                item.Relic.RelicId, item.Relic.ApplicationOrder,
                item.CardId!))
            .ToArray();

    internal static bool ValidateControlledDrawInsertion(
        CombatState combat, PrototypeControlledDrawInsertionV111 choice)
    {
        if (choice.CandidateCardIds is null ||
            choice.EligibleDrawPile is null || choice.Operation.Amount <= 0 ||
            !combat.DrawPile.SequenceEqual(choice.EligibleDrawPile) ||
            (choice.RequestSiteId ==
                PrototypeControlledDrawInsertionV111.PersonalHiveDazedSiteId) !=
            (choice.PersonalHiveDamageContinuation is not null))
            return false;
        if (choice.Operation.Kind ==
            PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions)
        {
            var basicSupport = choice.Stage == "position" &&
                choice.Operation.CardId is not null &&
                choice.SelectedCardId == choice.Operation.CardId &&
                choice.CandidateCardIds.Length == 0;
            if (!basicSupport) return false;
            if (choice.SemanticDrawContinuation is null)
            {
                if (choice.AutomaticHandDrawRemaining is { } handRemaining)
                {
                    var cursor = combat.AutomaticPipelineContinuation;
                    bool savedControlledDrawProgress = cursor is not null &&
                        cursor.Stochastic.TryGetActiveAutomaticHandDrawProgress(
                            cursor.Mechanics, out var progress) &&
                        progress.Kind is (
                            PrototypeAutomaticHandDrawProgressKindV111.ControlledDraw or
                            PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawHookChoice) &&
                        progress.RemainingCount == handRemaining;
                    if (handRemaining <= 0 ||
                        choice.AfterShuffleRelic is null ||
                        combat.Stochastic.SemanticDeck is not null || cursor is null ||
                        !savedControlledDrawProgress)
                        return false;
                }
                if (choice.AfterShuffleRelic is null)
                {
                    if (choice.CompletedAfterShuffleRelics is not null ||
                        choice.RemainingAfterShuffleRelics is not null ||
                        choice.AutomaticHandDrawRemaining is not null)
                        return false;
                    if (choice.RequestSiteId ==
                        PrototypeControlledDrawInsertionV111
                            .PersonalHiveDazedSiteId)
                        return ValidateControlledPersonalHiveInsertion(
                            combat, choice);
                    return choice.RequestSiteId ==
                            StochasticSiteRegistryV111.DrawPileRandomInsertSiteId &&
                        choice.RequestOwnerId == "prototype.combat" &&
                        choice.RequestOrdinal == 0;
                }
                var insertion = choice.AfterShuffleRelic;
                var afterShuffleCompleted =
                    choice.CompletedAfterShuffleRelics ?? [];
                var afterShuffleRemaining =
                    choice.RemainingAfterShuffleRelics ?? [];
                var expected = ControlledAfterShuffleRelicInsertions(combat);
                return choice.Operation.Amount == 1 &&
                    (choice.AutomaticHandDrawRemaining is null ||
                     choice.AutomaticHandDrawRemaining > 0) &&
                    choice.Operation.CardId == insertion.CardId &&
                    afterShuffleCompleted.All(item =>
                        ValidateAfterShuffleRelicInsertion(combat, item)) &&
                    ValidateAfterShuffleRelicInsertion(combat, insertion) &&
                    afterShuffleRemaining.All(item =>
                        ValidateAfterShuffleRelicInsertion(combat, item)) &&
                    afterShuffleCompleted.Concat([insertion])
                        .Concat(afterShuffleRemaining)
                        .SequenceEqual(expected) &&
                    choice.RequestSiteId ==
                        StochasticSiteRegistryV111.AfterShuffleRelicDrawPileInsertSiteId &&
                    choice.RequestOwnerId == "relic:" + insertion.RelicId +
                        ":application:" + insertion.RelicApplicationOrder &&
                    choice.RequestOrdinal >= 0;
            }
            var semanticDraw = choice.SemanticDrawContinuation;
            if (choice.AutomaticHandDrawRemaining is not null)
                return false;
            var afterShuffle = choice.AfterShuffleRelic;
            var completed = choice.CompletedAfterShuffleRelics ?? [];
            var remaining = choice.RemainingAfterShuffleRelics ?? [];
            var expectedInsertions = ControlledAfterShuffleRelicInsertions(
                combat);
            var savedInsertions = afterShuffle is null
                ? Array.Empty<PrototypeAfterShuffleRelicInsertionV111>()
                : completed.Concat(new[] { afterShuffle })
                    .Concat(remaining).ToArray();
            return semanticDraw.RemainingCount > 0 &&
                semanticDraw.EventDepth >= 0 &&
                semanticDraw.DeferredKeywordCardInstanceIds?.Distinct().Count() ==
                    semanticDraw.DeferredKeywordCardInstanceIds?.Length &&
                (!semanticDraw.StratagemSelectionPending ||
                 ExpectedStratagemSelectionCount(combat) > 0) &&
                (!semanticDraw.StandaloneSemanticDrawSegment ||
                 semanticDraw.EventDepth == 0) &&
                !(semanticDraw.StandaloneSemanticDrawSegment &&
                  semanticDraw.AutomaticSemanticHandDrawSegment) &&
                (!semanticDraw.AutomaticSemanticHandDrawSegment ||
                 (semanticDraw.FromHandDraw &&
                  semanticDraw.EventDepth == 0 &&
                  PrototypeGameEngine
                      .HasSemanticAutomaticHandDrawProgress(combat))) &&
                afterShuffle is not null &&
                choice.Operation.Amount == 1 &&
                choice.Operation.CardId == afterShuffle.CardId &&
                completed.All(item =>
                    ValidateAfterShuffleRelicInsertion(combat, item)) &&
                ValidateAfterShuffleRelicInsertion(combat, afterShuffle) &&
                remaining.All(item =>
                    ValidateAfterShuffleRelicInsertion(combat, item)) &&
                savedInsertions.SequenceEqual(expectedInsertions) &&
                choice.RequestSiteId ==
                    StochasticSiteRegistryV111.AfterShuffleRelicDrawPileInsertSiteId &&
                choice.RequestOwnerId == "relic:" + afterShuffle.RelicId +
                    ":application:" + afterShuffle.RelicApplicationOrder &&
                choice.RequestOrdinal >= 0;
        }
        if (choice.Operation.Kind !=
            PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile ||
            choice.PersonalHiveDamageContinuation is not null ||
            choice.RequestSiteId !=
                StochasticSiteRegistryV111.DrawPileRandomInsertSiteId ||
            choice.RequestOwnerId != "prototype.combat" ||
            choice.RequestOrdinal < 0 ||
            !choice.CandidateCardIds.SequenceEqual(RandomDrawAttackModelPool()))
            return false;
        return choice.Stage switch
        {
            "card_model" => choice.SelectedCardId is null,
            "position" => choice.SelectedCardId is not null &&
                choice.CandidateCardIds.Contains(choice.SelectedCardId),
            _ => false
        };
    }

    private static bool ValidateControlledPersonalHiveInsertion(
        CombatState combat,
        PrototypeControlledDrawInsertionV111 choice)
    {
        if (choice.Operation.CardId != "proto.status.dazed" ||
            choice.Operation.Amount <= 0 || choice.RequestOrdinal < 0 ||
            combat.PendingChoice is not { } pending ||
            choice.PersonalHiveDamageContinuation is not { } damage)
            return false;
        if (damage.Operation.Kind != PrototypeCombatEffectKind.DamageEnemy ||
            !damage.Operation.IsPoweredAttack ||
            damage.Operation.TargetEnemyId != damage.TargetEnemyId ||
            damage.TargetEnemyId <= 0 ||
            damage.SplashTarget.InstanceId != damage.TargetEnemyId ||
            damage.SplashTarget.Hp <= 0 || damage.HiveStackCount <= 0 ||
            damage.SplashTarget.PowerStates
                .Where(power => power.PowerId ==
                    PrototypeNativeHiveElites.PersonalHiveId)
                .Sum(power => power.Stacks) != damage.HiveStackCount ||
            damage.EventDepth < 0 ||
            (long)choice.RequestOrdinal + choice.Operation.Amount !=
                damage.HiveStackCount)
            return false;
        var sourceCardId = pending.SourceCardInstanceId?.ToString() ?? "none";
        if (pending.OuterChoiceContinuation is not { } outer ||
            pending.SourceCardInstanceId != outer.SourceCardInstanceId ||
            string.IsNullOrWhiteSpace(choice.RequestOwnerId))
            return false;
        var ownerParts = choice.RequestOwnerId.Split(':');
        return ownerParts.Length == 6 && ownerParts[0] == "enemy" &&
            int.TryParse(ownerParts[1],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var enemyInstanceId) &&
            enemyInstanceId > 0 && ownerParts[2] == "power" &&
            ownerParts[3] == "personal_hive" &&
            ownerParts[4] == "source" &&
            ownerParts[5] == sourceCardId &&
            enemyInstanceId == damage.TargetEnemyId &&
            combat.Enemies.Any(enemy =>
                enemy.InstanceId == enemyInstanceId);
    }

    /// <summary>
    /// Advance one Nature ticket: card model first for Metamorphosis-like
    /// effects, then bounded insertion index. Subsequent generated cards
    /// resume the exact remaining queue, yielding new chance tickets.
    /// </summary>
    internal static RunState ResumeControlledDrawInsertion(
        RunState state, int chosen)
        => ResumeControlledDrawInsertionWithBoundary(state, chosen).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledDrawInsertionWithBoundary(
            RunState state, int chosen)
        => ResumeControlledDrawInsertionCore(state, chosen, null);

    internal static RunState ResumeControlledDrawInsertionByCardModel(
        RunState state, string selectedCardModelId)
        => ResumeControlledDrawInsertionByCardModelWithBoundary(
            state, selectedCardModelId).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledDrawInsertionByCardModelWithBoundary(
            RunState state, string selectedCardModelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedCardModelId);
        return ResumeControlledDrawInsertionCore(
            state, null, selectedCardModelId);
    }

    private static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledDrawInsertionCore(
        RunState state, int? chosenPosition, string? selectedCardModelId)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Controlled draw insertion requires an RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ??
            throw new InvalidOperationException("Draw-insertion combat missing.");
        var pending = combat.PendingChoice ??
            throw new InvalidOperationException("Draw-insertion ticket missing.");
        if (pending.ChoiceId != "controlled_draw_insertion" ||
            pending.ControlledDrawInsertion is not { } choice ||
            pending.OuterChoiceContinuation is not { } continuation ||
            !ValidateControlledDrawInsertion(combat, choice))
            throw new InvalidDataException(
                "Pending draw insertion is not source-reachable.");
        if (choice.Stage == "card_model")
        {
            string selected;
            if (selectedCardModelId is { } selectedModelId)
            {
                if (!choice.CandidateCardIds.Contains(
                        selectedModelId, StringComparer.Ordinal))
                    throw new ArgumentOutOfRangeException(
                        nameof(selectedCardModelId));
                selected = selectedModelId;
            }
            else
            {
                if (chosenPosition is not { } selectedPosition ||
                    selectedPosition < 0 ||
                    selectedPosition >= choice.CandidateCardIds.Length)
                    throw new ArgumentOutOfRangeException(
                        nameof(chosenPosition));
                selected = choice.CandidateCardIds[selectedPosition];
            }
            return PrototypeAutomaticPipelineResumeResultV111.FromRun(
                state with
                {
                    World = world with
                    {
                        Combat = combat with
                        {
                            PendingChoice = pending with
                            {
                                ControlledDrawInsertion = choice with
                                {
                                    Stage = "position",
                                    SelectedCardId = selected
                                }
                            }
                        }
                    }
                });
        }

        if (selectedCardModelId is not null ||
            chosenPosition is not { } chosen || chosen < 0 ||
            chosen > combat.DrawPile.Length ||
            choice.SelectedCardId is not { } cardId)
            throw new ArgumentOutOfRangeException(nameof(chosenPosition));
        var instanceId = combat.NextCardInstanceId;
        CombatCardInstance? newCard = null;
        if (choice.AfterShuffleRelic is not null)
        {
            combat = AddGeneratedCardsToDiscard(combat, cardId, 1);
            if (combat.NextCardInstanceId != instanceId + 1)
                throw new InvalidDataException(
                    "After-shuffle relic insertion changed its card identity.");
        }
        else
        {
            newCard = new CombatCardInstance(
                instanceId, null, cardId, 0, true,
                PrototypeJson.EmptyObject(),
                TemporaryEnergyCost: choice.Operation.Kind ==
                    PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile
                    ? new PrototypeTemporaryCardCost(
                        0, PrototypeTemporaryCardCostExpiry.None)
                    : null);
        }
        var draw = combat.DrawPile.ToList();
        draw.Insert(chosen, instanceId);
        var zoneChanges = new Dictionary<PrototypeCardZone, long[]>
        {
            [PrototypeCardZone.DrawPile] = draw.ToArray()
        };
        if (choice.AfterShuffleRelic is not null)
            zoneChanges[PrototypeCardZone.DiscardPile] =
                combat.DiscardPile.Where(id => id != instanceId).ToArray();
        var nextCardZones = combat.Mechanics.CardZones
            .WithZoneChanges(zoneChanges);
        combat = newCard is null
            ? SemanticDrawPileAdapterV111.ApplyDrawPileInsertion(
                combat, chosen, instanceId, nextCardZones)
            : SemanticDrawPileAdapterV111.ApplyNewDrawPileInsertion(
                combat, chosen, newCard, nextCardZones);
        combat = combat with { PendingChoice = null };

        var remainingAfterShuffleRelics =
            choice.RemainingAfterShuffleRelics ?? [];
        if (choice.AfterShuffleRelic is not null &&
            remainingAfterShuffleRelics.Length > 0)
        {
            var nextCombat = MakeControlledAfterShuffleInsertionChoice(
                combat, remainingAfterShuffleRelics[0],
                remainingAfterShuffleRelics.Skip(1).ToArray(),
                (choice.CompletedAfterShuffleRelics ?? [])
                    .Append(choice.AfterShuffleRelic).ToArray(),
                choice.SemanticDrawContinuation, continuation,
                choice.RequestOrdinal + 1,
                choice.AutomaticHandDrawRemaining);
            return PrototypeAutomaticPipelineResumeResultV111.FromRun(
                state with
                {
                    World = world with { Combat = nextCombat }
                });
        }

        if (choice.RequestSiteId ==
                PrototypeControlledDrawInsertionV111
                    .PersonalHiveDazedSiteId &&
            choice.Operation.Amount > 1)
        {
            var nextCombat = MakeControlledDrawInsertionChoice(
                combat,
                choice.Operation with
                {
                    Amount = choice.Operation.Amount - 1
                },
                continuation,
                requestOrdinal: choice.RequestOrdinal + 1,
                requestSiteId: choice.RequestSiteId,
                requestOwnerId: choice.RequestOwnerId,
                personalHiveDamageContinuation:
                    choice.PersonalHiveDamageContinuation);
            return PrototypeAutomaticPipelineResumeResultV111.FromRun(
                state with
                {
                    World = world with { Combat = nextCombat }
                });
        }

        if (choice.SemanticDrawContinuation is { } semanticDraw)
        {
            if (semanticDraw.StratagemSelectionPending)
            {
                var stratagemRun = ResumeControlledStratagemSelection(
                    state, combat, semanticDraw, continuation);
                return semanticDraw.StandaloneSemanticDrawSegment
                    ? PrototypeAutomaticPipelineResumeResultV111.FromRun(
                        stratagemRun)
                    : ResumeControlledInsertionPipeline(stratagemRun);
            }

            if (semanticDraw.StandaloneSemanticDrawSegment)
            {
                var drawRun = state with
                {
                    World = world with { Combat = combat }
                };
                return PrototypeAutomaticPipelineResumeResultV111.FromRun(
                    BeginSemanticCombatDraw(
                        drawRun, semanticDraw.RemainingCount,
                        semanticDraw.FromHandDraw).Run);
            }

            if (semanticDraw.AutomaticSemanticHandDrawSegment)
            {
                var drawRun = state with
                {
                    World = world with { Combat = combat }
                };
                var resumedHandDraw =
                    ResumeSemanticAutomaticHandDrawAfterNestedChoice(
                        state, drawRun, null, "controlled draw insertion",
                        expectedSourceRemaining:
                            semanticDraw.RemainingCount);
                return resumedHandDraw
                    .ToAutomaticPipelineResumeResult();
            }

            var drawnSegment = ExecuteDrawCardsSegment(
                state, prototypeRng => DrawCards(
                    state.Player, combat, semanticDraw.RemainingCount,
                    prototypeRng, semanticDraw.FromHandDraw,
                    eventDepth: semanticDraw.EventDepth,
                    suspensionPolicy:
                        PrototypeDrawCardsSuspensionPolicyV111
                            .SemanticDrawCards(effectDraw: true)));
            var drawState = drawnSegment.Run with
            {
                World = world with { Combat = combat }
            };
            return ResumeControlledInsertionPipeline(
                ContinueControlledSemanticEffectDraw(
                    drawState, drawnSegment.Drawn, semanticDraw,
                    continuation));
        }

        if (choice.AutomaticHandDrawRemaining is { } handDrawRemaining)
        {
            if (continuation.Operations.Length != 0 ||
                continuation.PendingDiscardEvents.Length != 0 ||
                continuation.PendingSlyCardInstanceIds.Length != 0 ||
                continuation.CompletionEvents.Length != 0 ||
                continuation.Parent is not null ||
                continuation.EventDispatchContinuation is not null)
                throw new InvalidDataException(
                    "Automatic hand-draw insertion has unrelated operations.");
            var drawRun = state with
            {
                World = world with { Combat = combat }
            };
            var (resumedRun, resumedDraw) = ResumeAutomaticHandDrawSegment(
                drawRun, handDrawRemaining,
                PrototypeDrawCardsSuspensionPolicyV111
                    .ControlledLegacyAutomaticHandDraw(
                        combat,
                        cardDrawnChoice: !ControlledChanceGuardV111
                            .EngineOwnedNativeProviderActive));
            return ResumeSavedControlledAutomaticHandDraw(
                resumedRun, resumedDraw)
                    .ToAutomaticPipelineResumeResult();
        }

        var resumePlayer = state.Player;
        var resumeCombat = combat;
        var resumeContinuation = continuation;
        var prototypeRng = state.Stochastic.PrototypeRng;
        if (choice.PersonalHiveDamageContinuation is { } hiveDamage)
        {
            var remainingOperations = new Queue<PrototypeQueuedOperation>(
                continuation.Operations);
            var afterHive = ContinueDamageEnemyAfterHit(
                resumePlayer, resumeCombat, prototypeRng, hiveDamage,
                continuation.SourceCardInstanceId, remainingOperations,
                continuation);
            resumePlayer = afterHive.Player;
            resumeCombat = afterHive.Combat;
            resumeContinuation = continuation with
            {
                Operations = remainingOperations.ToArray()
            };

            if (resumeCombat.PendingChoice is not null)
            {
                var suspendedRun = state with
                {
                    Player = resumePlayer,
                    Stochastic = RunStochasticStateV111.WithPrototypeRng(
                        state.Stochastic, prototypeRng),
                    World = world with { Combat = resumeCombat }
                };
                return ResumeAutomaticPipelineContinuation(suspendedRun);
            }
        }
        var resolved = ResumeChoiceResolutionContinuation(
            resumePlayer, resumeCombat, prototypeRng, resumeContinuation);
        var updated = state with
        {
            Player = resolved.Player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng),
            World = world with { Combat = resolved.Combat }
        };
        return ResumeAutomaticPipelineContinuation(updated);
    }

    private static CombatState MakeControlledAfterShuffleInsertionChoice(
        CombatState combat,
        PrototypeAfterShuffleRelicInsertionV111 insertion,
        PrototypeAfterShuffleRelicInsertionV111[] remaining,
        PrototypeAfterShuffleRelicInsertionV111[] completed,
        PrototypeControlledSemanticDrawV111? semanticDraw,
        PrototypeChoiceResolutionContinuationState continuation,
        int ordinal,
        int? automaticHandDrawRemaining = null)
    {
        var operation = new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions,
            1, CardId: insertion.CardId);
        return MakeControlledDrawInsertionChoice(
            combat, operation, continuation, semanticDraw, insertion,
            completed, remaining, ordinal,
            automaticHandDrawRemaining: automaticHandDrawRemaining);
    }

    private static RunState ResumeControlledStratagemSelection(
        RunState state, CombatState combat,
        PrototypeControlledSemanticDrawV111 draw,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        var drawOperation = CreateDrawResumeOperation(draw);
        var resumedContinuation = continuation with
        {
            Operations = new[]
            {
                StratagemChoiceOperation(
                    combat,
                    semanticStandaloneDraw:
                        draw.StandaloneSemanticDrawSegment,
                    semanticAutomaticDraw:
                        draw.AutomaticSemanticHandDrawSegment)
            }.Concat(draw.AutomaticSemanticHandDrawSegment
                ? continuation.Operations
                : new[] { drawOperation }.Concat(
                    continuation.Operations)).ToArray()
        };
        var prototypeRng = state.Stochastic.PrototypeRng;
        var resolved = ResumeChoiceResolutionContinuation(
            state.Player, combat, prototypeRng, resumedContinuation);
        return state with
        {
            Player = resolved.Player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng),
            World = state.World! with { Combat = resolved.Combat }
        };
    }

    private static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledInsertionPipeline(RunState state)
    {
        if (state.Phase == RunPhase.Combat)
            return ResumeAutomaticPipelineContinuation(state);
        return PrototypeAutomaticPipelineResumeResultV111.FromRun(state);
    }

    /// <summary>
    /// Narrow test/diagnostic entry for an effect that is not yet reachable
    /// through a normal Silent fixture card; executes the actual operations
    /// kernel, preserving a real RunState and RNG guard, not fake pile edits.
    /// </summary>
    internal static RunState BeginControlledDrawInsertionFixture(
        RunState state, PrototypeQueuedOperation operation)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException("Controlled test fixture needs RNG guard.");
        if (operation.Kind is not (
                PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions
                or PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile))
            throw new ArgumentException("Expected a random draw-pile insertion effect.");
        var world = RequireWorld(state);
        var combat = world.Combat ??
            throw new InvalidOperationException("No combat.");
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
