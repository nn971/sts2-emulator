namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine : IDeterministicEngine
{
    /// <summary>
    /// Identifies the ordinary combat player-decision boundary shared by
    /// semantic action and continuation facades.
    /// </summary>
    internal static bool IsAtCombatPlayerDecisionBoundary(RunState run) =>
        run.Phase == RunPhase.Combat &&
        run.World?.Combat is { IsPlayerTurn: true,
            PendingChoice: null, PendingKnowledgeDemonChoice: null,
            AutomaticPipelineContinuation: null };

    /// <summary>
    /// Recognizes suspended enemy-action, controlled-target, and supported
    /// source-action choices in the automatic pipeline. End-turn and
    /// controlled-playable facades share this classification.
    /// </summary>
    internal static bool HasPendingControlledAutomaticPipelineChoice(
        RunState run) => run.World?.Combat is { } combat &&
        (combat.AutomaticPipelineContinuation?.ControlledEnemyAction is not null ||
         combat.PendingChoice?.ControlledTarget is not null ||
         combat.PendingKnowledgeDemonChoice is not null);

    /// <summary>
    /// Recognizes supported source-action prompts and typed semantic chance
    /// choices at end-turn handoff boundaries. In-flight hand-draw progress
    /// remains owned by its draw continuation. Callers that need a particular
    /// payload should still use its specific predicate after this check.
    /// </summary>
    internal static bool HasPendingSupportedAutomaticPipelineChoice(
        RunState run) =>
        HasPendingControlledAutomaticPipelineChoice(run) ||
        HasPendingSupportedAutomaticPipelineSemanticChoice(run);

    /// <summary>
    /// Recognizes a source-supported semantic effect draw, insertion, or its
    /// randomized cost while an automatic pipeline is suspended in autoplay
    /// or at an exact opening continuation.
    /// </summary>
    internal static bool HasPendingSupportedAutomaticPipelineSemanticDraw(
        RunState run) => run.World?.Combat is { } combat &&
        combat.PendingChoice is { } pending &&
        IsSupportedAutomaticPipelineSemanticDrawContinuation(
            combat, pending);

    internal static bool HasPendingSupportedAutomaticPipelineSemanticGeneration(
        RunState run) => run.World?.Combat is { } combat &&
        combat.PendingChoice is { } pending &&
        IsAutomaticPipelineSemanticGenerationContinuation(
            combat, pending);

    internal static bool HasPendingSupportedAutomaticPipelineSemanticPotionGeneration(
        RunState run) => run.World?.Combat is { } combat &&
        combat.PendingChoice is { } pending &&
        IsAutomaticPipelineSemanticPotionGenerationContinuation(
            combat, pending);

    internal static bool HasPendingSupportedAutomaticPipelineSemanticCardTransform(
        RunState run) => run.World?.Combat is { } combat &&
        combat.PendingChoice is { } pending &&
        IsAutomaticPipelineSemanticCardTransformContinuation(
            combat, pending);

    internal static bool HasPendingSupportedAutomaticPipelineSemanticCardShortlist(
        RunState run) => run.World?.Combat is { } combat &&
        combat.PendingChoice is { } pending &&
        IsAutomaticPipelineSemanticCardShortlistContinuation(
            combat, pending);

    internal static bool HasPendingSupportedAutomaticPipelineSemanticPoolShuffle(
        RunState run) => run.World?.Combat is { } combat &&
        combat.PendingChoice is { } pending &&
        IsAutomaticPipelineSemanticPoolShuffleContinuation(
            combat, pending);

    internal static bool HasPendingSupportedAutomaticPipelineSemanticRandomHandCost(
        RunState run) => run.World?.Combat is { } combat &&
        combat.PendingChoice is { } pending &&
        IsAutomaticPipelineSemanticRandomHandCostContinuation(
            combat, pending);

    internal static bool HasPendingSupportedAutomaticPipelineSemanticCardSelection(
        RunState run) => run.World?.Combat is { } combat &&
        combat.PendingChoice is { } pending &&
        IsAutomaticPipelineSemanticCardSelectionContinuation(
            combat, pending);

    internal static bool HasPendingSupportedAutomaticPipelineSemanticDrawHookChoice(
        RunState run) => run.World?.Combat is { } combat &&
        combat.PendingChoice is { } pending &&
        IsAutomaticPipelineSemanticDrawHookChoiceContinuation(
            combat, pending);

    internal static bool HasPendingSupportedAutomaticPipelineSemanticChoice(
        RunState run) =>
        HasPendingSupportedAutomaticPipelineSemanticDraw(run) ||
        HasPendingSupportedAutomaticPipelineSemanticGeneration(run) ||
        HasPendingSupportedAutomaticPipelineSemanticPotionGeneration(run) ||
        HasPendingSupportedAutomaticPipelineSemanticCardTransform(run) ||
        HasPendingSupportedAutomaticPipelineSemanticCardShortlist(run) ||
        HasPendingSupportedAutomaticPipelineSemanticPoolShuffle(run) ||
        HasPendingSupportedAutomaticPipelineSemanticRandomHandCost(run) ||
        HasPendingSupportedAutomaticPipelineSemanticCardSelection(run) ||
        HasPendingSupportedAutomaticPipelineSemanticDrawHookChoice(run);

    internal static bool IsSupportedAutomaticPipelineSemanticDrawContinuation(
        CombatState combat, PendingCombatChoiceState pending) =>
        IsMayhemSemanticDrawContinuation(combat, pending) ||
        IsLateAutoplaySemanticDrawContinuation(combat, pending) ||
        IsAutomaticPipelineSemanticDrawInsertionContinuation(
            combat, pending) ||
        IsGamblingChipOpeningSemanticDrawContinuation(combat, pending);

    /// <summary>
    /// Identifies automatic-pipeline cursors where controlled targets may
    /// suspend: ordinary combat work, post-draw player hooks, late autoplay,
    /// or the exact first-turn Whispering Earring opening continuation.
    /// </summary>
    internal static bool IsAtSupportedAutomaticPipelineTargetBoundary(
        CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(combat);
        if (combat.PendingKnowledgeDemonChoice is not null)
            return false;
        var continuation = combat.AutomaticPipelineContinuation;
        var pipelineLength = PrototypeContent.Rules.EndTurnPipeline.Length;
        if (continuation?.OpeningStage is not null)
            return IsWhisperingEarringOpeningAutoplayBoundary(combat);
        if (continuation is not
            {
                OpeningStage: null,
                PendingPostDispatchEventKind: null,
                ControlledDrawRemaining: null,
                ControlledDrawCostRemaining: null,
                ControlledSemanticDrawRemaining: null,
                ControlledSemanticDrawRequested: null,
                ControlledEnemyAction: null
            } || continuation.NextStepIndex < 0 ||
            continuation.NextStepIndex > pipelineLength)
            return false;

        if (!combat.IsPlayerTurn)
            return continuation.NextStepIndex < pipelineLength;
        return continuation.NextStepIndex >=
            PlayerTurnAutomaticPipelineTargetStartIndex();
    }

    private static int PlayerTurnAutomaticPipelineTargetStartIndex()
    {
        var pipeline = PrototypeContent.Rules.EndTurnPipeline;
        for (var index = 0; index < pipeline.Length; index++)
        {
            if (pipeline[index].Kind ==
                    PrototypeAutomaticStepKind.DispatchCombatEvent &&
                pipeline[index].EventKind ==
                    PrototypeCombatEventKind.PlayerTurnStarted)
                return index + 1;
        }
        return int.MaxValue;
    }

    internal static bool IsAtCombatRewardOrTerminalBoundary(RunState run) =>
        (run.Phase is RunPhase.Reward or RunPhase.Terminal) &&
        IsAtCombatActionResultBoundary(run);

    /// <summary>
    /// Identifies a complete result of one ordinary combat action: the next
    /// combat phase, an initialized combat reward, or a closed player defeat.
    /// </summary>
    internal static bool IsAtCombatActionResultBoundary(RunState run) =>
        run.Phase switch
        {
            RunPhase.Combat => true,
            RunPhase.Reward => run.World is
                { Combat: null, Reward: not null },
            RunPhase.Terminal => run.World is
                { Combat: null, Reward: null, TerminalOutcome: "defeat" },
            _ => false
        };

    public IReadOnlyList<GameAction> GetLegalActions(RunState state)
    {
        RunConfiguration.ValidateState(state);
        if (state.ControlledEventRewardChoice is { } rewardChoice)
        {
            var request = rewardChoice.PendingRequest;
            return Enumerable.Range(0, request.SupportCount)
                .Select(index => GameAction.Create(
                    "choose_controlled_event_outcome",
                    new ChooseControlledEventOutcomePayload(index)))
                .ToArray();
        }
        if (state.ControlledRunActionChoice is { } runActionChoice)
        {
            var request = runActionChoice.PendingRequest;
            if (request.SupportCount >
                ControlledRunActionEngineV111.MaximumInlineOutcomeActions)
                return Array.Empty<GameAction>();
            return Enumerable.Range(0, request.SupportCount)
                .Select(index => GameAction.Create(
                    "choose_controlled_run_action_outcome",
                    new ChooseControlledEventOutcomePayload(index)))
                .ToArray();
        }
        var phaseActions = state.Phase switch
        {
            RunPhase.RunStart => [GameAction.Empty("start_run")],
            RunPhase.MapChoice => GetMapActions(state),
            RunPhase.Combat => GetCombatActions(state),
            RunPhase.Reward => GetRewardActions(state),
            RunPhase.Shop => GetShopActions(state),
            RunPhase.Event => GetEventActions(state),
            RunPhase.Rest => GetRestActions(state),
            RunPhase.ActTransition => [GameAction.Empty("continue_act")],
            RunPhase.Terminal => Array.Empty<GameAction>(),
            _ => Array.Empty<GameAction>()
        };

        if (state.Phase is RunPhase.RunStart
            or RunPhase.Combat
            or RunPhase.Terminal
            || state.Phase == RunPhase.Event
                && state.World?.Event?.ControlledChoice is not null
            || state.ControlledEventRewardChoice is not null
            || state.ControlledRunActionChoice is not null)
        {
            return phaseActions;
        }

        return phaseActions
            .Concat(GetAnyTimePotionActions(state))
            .ToArray();
    }

    public TransitionResult Step(RunState state, GameAction action) =>
        StepCore(state, action, trustedH8: false);

    /// <summary>
    /// Version-guarded H8 execution. The live session exclusively owns the
    /// input checkpoint and the combat engine's transitions are functional.
    /// Ordinary public Step retains full defensive cloning for external
    /// callers, forks and legacy interop.
    /// </summary>
    internal TransitionResult StepFromH8TrustedBoundary(
        RunState state, GameAction action) =>
        StepCore(state, action, trustedH8: true);

    private TransitionResult StepCore(
        RunState state, GameAction action, bool trustedH8)
    {
        RunConfiguration.ValidateState(state);
        using var providerScope = StochasticProviderScopeV111.Enter(
            new NativeRandomIndexProviderV111(),
            new NativeRandomUnitFloatProviderV111(),
            overrideExplicitProviders: false);
        if ((state.Phase == RunPhase.Event
                && state.World?.Event?.ControlledChoice is not null
            || state.ControlledEventRewardChoice is not null
            || state.ControlledRunActionChoice is not null)
            && (!ControlledChanceGuardV111.Active
                || action.Kind != (state.ControlledRunActionChoice is not null
                    ? "choose_controlled_run_action_outcome"
                    : "choose_controlled_event_outcome")))
        {
            throw new InvalidOperationException(
                "A suspended controlled choice requires its explicit outcome adapter.");
        }
        // Every public Step owns an isolated copy. H8's versioned owner
        // instead advances from an immutable input; all mechanics updates
        // publish successor records, avoiding a complete clone for every
        // combat player action.
        var fork = (trustedH8 ? state : state.Fork()) with
        {
            DecisionIndex = state.DecisionIndex + 1
        };

        var next = fork.ControlledRunActionChoice is { } runActionContinuation
            ? ResumeControlledRunAction(
                fork, action, runActionContinuation)
            : StringComparer.Ordinal.Equals(
                action.Kind,
                "use_potion")
            && state.Phase != RunPhase.Combat
                ? UsePotionOutsideCombat(
                    fork,
                    action.ReadPayload<UsePotionPayload>())
                : state.Phase switch
        {
            RunPhase.RunStart => StepRunStart(fork, action),
            RunPhase.MapChoice => StepMap(fork, action),
            RunPhase.Combat => StepCombat(fork, action),
            RunPhase.Reward => StepReward(fork, action),
            RunPhase.Shop => StepShop(fork, action),
            RunPhase.Event => StepEvent(fork, action),
            RunPhase.Rest => StepRest(fork, action),
            RunPhase.ActTransition => StepActTransition(fork, action),
            RunPhase.Terminal => throw new InvalidOperationException("A terminal run has no legal actions."),
            _ => throw new NotSupportedException($"Prototype phase '{state.Phase}' is unsupported.")
        };

        next = ApplyToyBoxAcquisitionReward(state, next);
        next = ApplyGlassEyeAcquisitionReward(state, next);
        next = ApplyNeowRelicPickupRewards(state, next);
        next = PrototypeGoldenPathMapV111.ApplyAcquisition(state, next);
        next = NativePaelRewardsV111.SynchronizeLegionPet(next);
        if (state.World?.Combat is not null && next.World?.Combat is null)
            next = NativeAncientRewardsV111.ApplyCombatEnd(next);

        // Direct event and quest relic obtains bypass a bag draw. Keep
        // the retained regional relic queues disjoint from owned relics
        // at every decision boundary without introducing an RNG call.
        if (next.World?.RelicBag is not null)
            next = next with
            {
                World = PrototypeNativeRelicGrabBag.PruneOwned(
                    next.World, next.Player)
            };
        next = NativeRunRngV111.ReconcileAfterStep(state, next);
        if (next.ControlledEventRewardChoice is { } rewardChoice)
        {
            next = next with
            {
                ControlledEventRewardChoice = rewardChoice with
                {
                    PartialState = next with
                    {
                        ControlledEventRewardChoice = null
                    }
                }
            };
        }
        if (next.ControlledRunActionChoice is { } runActionChoice)
        {
            next = next with
            {
                ControlledRunActionChoice = runActionChoice with
                {
                    PartialState = next with
                    {
                        ControlledRunActionChoice = null
                    }
                }
            };
        }
        return new TransitionResult(next, Array.Empty<string>());
    }

    private static IReadOnlyList<GameAction>
        GetAnyTimePotionActions(RunState state) =>
        state.Player.PotionSlots
            .Select((potion, slot) =>
                new { Potion = potion, Slot = slot })
            .Where(item =>
                item.Potion is not null
                && PrototypeContent.Potion(
                    item.Potion.PotionId)
                    .UsableOutsideCombat)
            .Select(item =>
                GameAction.Create(
                    "use_potion",
                    new UsePotionPayload(
                        item.Slot,
                        null)))
            .ToArray();

    private static RunState UsePotionOutsideCombat(
        RunState state,
        UsePotionPayload payload)
    {
        if (payload.TargetEnemyId is not null)
        {
            throw new InvalidOperationException(
                "Out-of-combat potions cannot target an enemy.");
        }

        if (payload.Slot < 0
            || payload.Slot
                >= state.Player.PotionSlots.Length)
        {
            throw new InvalidOperationException(
                $"Potion slot {payload.Slot} is invalid.");
        }

        var potion =
            state.Player.PotionSlots[payload.Slot]
            ?? throw new InvalidOperationException(
                $"Potion slot {payload.Slot} is empty.");
        var definition =
            PrototypeContent.Potion(potion.PotionId);
        if (!definition.UsableOutsideCombat)
        {
            throw new InvalidOperationException(
                $"Potion '{definition.Name}' is combat-only.");
        }

        var player = ConsumePotionSlot(
            state.Player,
            payload.Slot);
        var nativeRunRng = state.NativeRunRng?.Fork();
        player = ApplyPotionRunEffects(
            player,
            definition,
            state.Rng,
            nativeRunRng);

        state = state with
        {
            Player = player,
            NativeRunRng = nativeRunRng ?? state.NativeRunRng
        };

        // An event's full-belt potion offer is still pending when a
        // usable potion is consumed from the belt. The newly vacated
        // slot can accept that offer directly; leaving its old replacement
        // candidates active would expose an invalid slot and could make
        // the event impossible to finish.
        if (state.Phase == RunPhase.Event
            && state.World?.Event?.PendingPotionReplacement
                is { } replacement)
        {
            var emptySlot = Array.IndexOf(
                player.PotionSlots,
                null);
            if (emptySlot >= 0)
            {
                var slots =
                    (PotionInstance?[])player.PotionSlots.Clone();
                slots[emptySlot] = new PotionInstance(
                    replacement.PotionId,
                    PrototypeJson.EmptyObject());
                var world = RequireWorld(state);
                state = state with
                {
                    Player = player with { PotionSlots = slots },
                    World = world with
                    {
                        Event = world.Event! with
                        {
                            PendingPotionReplacement = null
                        }
                    }
                };
                return AdvanceEventContinuations(state);
            }
        }

        return state;
    }

    private static PlayerState ConsumePotionSlot(
        PlayerState player,
        int slot)
    {
        var slots =
            (PotionInstance?[])player.PotionSlots.Clone();
        slots[slot] = null;
        return player with { PotionSlots = slots };
    }

    private static PlayerState ApplyPotionRunEffects(
        PlayerState player,
        PrototypePotionDefinition definition,
        RngBundle rng,
        RngBundle? nativeRunRng = null) =>
        ApplyPlayerRunEffects(
            player,
            definition.RunEffects
                ?? Array.Empty<PrototypeRunEffectSpec>(),
            rng,
            nativeRunRng);

    private static PlayerState ApplyPlayerRunEffects(
        PlayerState player,
        IEnumerable<PrototypeRunEffectSpec> effects,
        RngBundle? rng = null,
        RngBundle? nativeRunRng = null)
    {
        foreach (var effect in effects)
        {
            switch (effect.Kind)
            {
                case PrototypeRunEffectKind.Heal:
                    player = player with
                    {
                        Hp = Math.Min(
                            player.MaxHp,
                            player.Hp + effect.Amount)
                    };
                    break;

                case PrototypeRunEffectKind.HealToFull:
                    player = player with
                    {
                        Hp = player.MaxHp
                    };
                    break;

                case PrototypeRunEffectKind.HealPercentMaxHp:
                {
                    var amount = Math.Max(
                        0,
                        (player.MaxHp
                            * effect.Amount) / 100);
                    player = player with
                    {
                        Hp = Math.Min(
                            player.MaxHp,
                            player.Hp + amount)
                    };
                    break;
                }

                case PrototypeRunEffectKind.GainMaxHp:
                {
                    var amount = Math.Max(
                        0,
                        effect.Amount);
                    player = player with
                    {
                        MaxHp = player.MaxHp + amount,
                        Hp = player.Hp + amount
                    };
                    break;
                }

                case PrototypeRunEffectKind.LoseMaxHp:
                {
                    var amount = Math.Max(
                        0,
                        effect.Amount);
                    var maxHp = Math.Max(
                        1,
                        player.MaxHp - amount);
                    player = player with
                    {
                        MaxHp = maxHp,
                        Hp = Math.Min(
                            player.Hp,
                            maxHp)
                    };
                    break;
                }

                case PrototypeRunEffectKind.FillPotionSlots:
                {
                    if (!CanAcquirePotion(player))
                    {
                        break;
                    }

                    if (rng is null)
                    {
                        throw new InvalidOperationException(
                            "Filling potion slots requires an RNG bundle.");
                    }

                    var slots =
                        (PotionInstance?[])
                        player.PotionSlots.Clone();
                    for (var index = 0;
                         index < slots.Length;
                         index++)
                    {
                        if (slots[index] is not null)
                        {
                            continue;
                        }

                        var potionPool = PrototypeContent.PotionPool;
                        var ownerId = "player:potion_slot:" + index + ":pool:"
                            + CanonicalJson.Sha256(potionPool);
                        var potionIndex = nativeRunRng is { } nativeRewardRng
                            ? NativePlayerRngV111.ChooseIndex(
                                nativeRewardRng,
                                "run.potion.fill_empty_slots.model",
                                NativePlayerRngV111.RewardsStreamId,
                                ownerId,
                                potionPool.Length)
                            : ResolveLegacyRandomIndex(
                                rng,
                                "run.potion.fill_empty_slots.model",
                                ownerId,
                                Enumerable.Range(0, potionPool.Length),
                                streamId: "reward",
                                controlledCandidateLabels:
                                    (string[])potionPool.Clone());
                        var potionId = potionPool[potionIndex];
                        slots[index] =
                            new PotionInstance(
                                potionId,
                                PrototypeJson
                                    .EmptyObject());
                    }

                    player = player with
                    {
                        PotionSlots = slots
                    };
                    break;
                }

                case PrototypeRunEffectKind.LoseHp:
                    player = player with
                    {
                        Hp = Math.Max(
                            0,
                            player.Hp - effect.Amount)
                    };
                    break;

                case PrototypeRunEffectKind.GainGold:
                    player = player with
                    {
                        Gold = GoldAfterGain(player, effect.Amount)
                    };
                    break;

                case PrototypeRunEffectKind.LoseGold:
                    player = player with
                    {
                        Gold = Math.Max(
                            0,
                            player.Gold - effect.Amount)
                    };
                    break;

                case PrototypeRunEffectKind.LoseAllGold:
                    player = player with { Gold = 0 };
                    break;

                case PrototypeRunEffectKind.UpgradeLastCardOfId:
                {
                    var id = effect.CardId ?? throw new InvalidOperationException(
                        "Upgrading the last matching card requires a card ID.");
                    var index = Array.FindLastIndex(
                        player.Deck,
                        card => StringComparer.Ordinal.Equals(card.CardId, id));
                    if (index >= 0 && player.Deck[index].UpgradeLevel == 0)
                    {
                        var deck = (CardInstance[])player.Deck.Clone();
                        deck[index] = deck[index] with { UpgradeLevel = 1 };
                        player = player with { Deck = deck };
                    }

                    break;
                }

                case PrototypeRunEffectKind.AddCard:
                    throw new NotSupportedException(
                        "Generic run effects do not currently create persistent cards directly.");

                case PrototypeRunEffectKind.GainRelic:
                case PrototypeRunEffectKind.GainPotion:
                    throw new NotSupportedException(
                        "Item acquisition run effects require run-state context.");

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return player;
    }

    private static PlayerState ApplyRelicRunEvent(
        PlayerState player,
        PrototypeRunEventKind eventKind,
        CardInstance? addedCard = null,
        string? acquiredRelicId = null,
        RngBundle? rng = null,
        RunWorldState? world = null,
        Func<long>? allocateCardId = null,
        RngBundle? nativeRunRng = null)
    {
        // RelicAcquired models RelicModel.AfterObtained on the newly
        // appended instance, not a broadcast to every owned relic with the
        // same model ID. Older copies retain their independent state and
        // must not replay acquisition-only rewards (e.g. Old Coin).
        var acquiredIndex = eventKind == PrototypeRunEventKind.RelicAcquired
            ? Array.FindLastIndex(player.Relics, relic =>
                StringComparer.Ordinal.Equals(relic.RelicId, acquiredRelicId))
            : -1;
        for (var relicIndex = 0; relicIndex < player.Relics.Length;
             relicIndex++)
        {
            if (eventKind == PrototypeRunEventKind.RelicAcquired
                && relicIndex != acquiredIndex)
            {
                continue;
            }

            var relic = player.Relics[relicIndex];
            if (relic.IsMelted)
                continue;
            var definition =
                PrototypeContent.Relic(relic.RelicId);
            if (eventKind == PrototypeRunEventKind.RelicAcquired
                && relic.RelicId == NativeDarvEventV111.CallingBellId)
            {
                player = AppendCard(
                    player,
                    (allocateCardId ?? throw new InvalidOperationException(
                        "Calling Bell acquisition requires card IDs."))(),
                    NativeDarvEventV111.CurseOfTheBellCardId,
                    rng ?? throw new InvalidOperationException(
                        "Calling Bell acquisition requires RNG context."));
            }
            if (eventKind == PrototypeRunEventKind.RelicAcquired
                && relic.RelicId == NativePaelRewardsV111.WingId)
                player = NativePaelRewardsV111.SetRewardsSacrificed(player, relicIndex, 0);
            if (eventKind == PrototypeRunEventKind.RelicAcquired
                && relic.RelicId == NativeGloryAncientsV111.BeautifulBraceletId)
                player = NativeGloryAncientsV111.ApplyBeautifulBraceletAcquisition(
                    player,
                    rng ?? throw new InvalidOperationException(
                        "Beautiful Bracelet acquisition requires RNG context."),
                    nativeRunRng);
            if (eventKind == PrototypeRunEventKind.RelicAcquired
                && relic.RelicId == NativeGloryAncientsV111.DistinguishedCapeId)
            {
                var acquisitionRng = rng
                    ?? throw new InvalidOperationException(
                        "Distinguished Cape acquisition requires RNG context.");
                var allocate = allocateCardId
                    ?? throw new InvalidOperationException(
                        "Distinguished Cape acquisition requires card IDs.");
                var curseIds = NativeGloryAncientsV111
                    .ModifierGeneratedCurseIds.ToList();
                IRandomIndexProviderV111 curseProvider = nativeRunRng is null
                    ? new LegacyPrototypeRandomIndexProviderV111()
                    : new NativeRandomIndexProviderV111();
                var curseRng = nativeRunRng ?? acquisitionRng;
                var curseStreamId = nativeRunRng is null
                    ? "event"
                    : "run.niche";
                var controlledEventReplay =
                    ControlledEventChoiceReplayV111.IsActive
                    && ControlledEventChoiceReplayV111.IsEventChoiceSite(
                        NativeGloryAncientsV111.DistinguishedCapeCurseSiteId);
                for (var i = 0; i < 2; i++)
                {
                    var request = new RandomIndexRequestV111(
                        NativeGloryAncientsV111
                            .DistinguishedCapeCurseSiteId,
                        curseStreamId,
                        "relic:distinguished_cape:acquisition",
                        controlledEventReplay ? 0 : i,
                        Enumerable.Range(0, curseIds.Count).ToArray());
                    using var candidateScope =
                        ControlledEventChoiceReplayV111.DescribeCandidates(
                            curseIds.ToArray());
                    var selectedIndex =
                        StochasticProviderScopeV111.ResolveIndex(
                            curseProvider, request, curseRng).SelectedIndex;
                    var curseId = curseIds[selectedIndex];
                    curseIds.RemoveAt(selectedIndex);
                    player = AppendCard(player, allocate(), curseId,
                        acquisitionRng);
                }

                for (var i = 0; i < 3; i++)
                    player = AppendCard(player, allocate(),
                        NativeGloryAncientsV111.ApparitionCardId,
                        acquisitionRng);
            }
            if (eventKind == PrototypeRunEventKind.RelicAcquired
                && relic.RelicId == NativePaelRewardsV111.ClawId)
                player = NativePaelRewardsV111.EnchantClawDefends(player);
            if (eventKind == PrototypeRunEventKind.RelicAcquired
                && relic.RelicId == NativePaelRewardsV111.ToothId)
                player = NativePaelRewardsV111.InitializeTooth(player, relicIndex);
            if (eventKind == PrototypeRunEventKind.RelicAcquired
                && relic.RelicId == NativePaelRewardsV111.LegionId)
                player = NativePaelRewardsV111.InitializeLegion(player, relicIndex,
                    rng ?? throw new InvalidOperationException("Pael's Legion acquisition requires RNG context."));
            if (eventKind == PrototypeRunEventKind.RelicAcquired
                && relic.RelicId == NativeAncientRewardsV111.PumpkinCandleId)
                player = NativeAncientRewardsV111.Rekindle(player, relicIndex, acquisition: true);
            if (eventKind == PrototypeRunEventKind.RelicAcquired
                && relic.RelicId == NativeAncientRewardsV111.NutritiousSoupId)
                player = NativeAncientRewardsV111.EnchantSoupStrikes(player);
            if (eventKind == PrototypeRunEventKind.RelicAcquired
                && relic.RelicId == NativeAncientRewardsV111.ToyBoxId)
                player = NativeAncientRewardsV111.InitializeToyBox(player, relicIndex);
            if (eventKind == PrototypeRunEventKind.RelicAcquired
                && relic.RelicId == PrototypeNativeOrobasRelics.SandCastleId)
                player = PrototypeNativeOrobasRelics.ApplySandCastleAcquisition(
                    player, rng ?? throw new InvalidOperationException(
                        "Sand Castle acquisition requires RNG context."));
            if (eventKind == PrototypeRunEventKind.RelicAcquired
                && relic.RelicId == PrototypeNativeOrobasRelics.TouchOfOrobasId)
                player = PrototypeNativeOrobasRelics.ApplyTouchOfOrobasAcquisition(
                    player, relicIndex);
            if (eventKind == PrototypeRunEventKind.RelicAcquired
                && relic.RelicId == PrototypeNativeOrobasRelics.ArchaicToothId)
                player = PrototypeNativeOrobasRelics.ApplyArchaicToothAcquisition(
                    player, relicIndex);
            if (eventKind == PrototypeRunEventKind.RelicAcquired
                && relic.RelicId == PrototypeNativeOrobasRelics.AlchemicalCofferId)
                player = PrototypeNativeOrobasRelics.ApplyAlchemicalCofferAcquisition(
                    player, relicIndex, rng ?? throw new InvalidOperationException(
                        "Alchemical Coffer acquisition requires RNG context."),
                    nativeRunRng);
            foreach (var trigger in
                     definition.RunTriggers
                     ?? Array.Empty<PrototypeRelicRunTriggerSpec>())
            {
                if (trigger.EventKind != eventKind)
                {
                    continue;
                }

                if (trigger.RequiredCardType is not null)
                {
                    if (addedCard is null
                        || PrototypeContent.Card(
                            addedCard.CardId).Type
                            != trigger.RequiredCardType.Value)
                    {
                        continue;
                    }
                }

                if (trigger.RouteCondition is not null
                    && (world is null
                        || !MatchesRouteCondition(
                            world,
                            trigger.RouteCondition)))
                {
                    continue;
                }

                player = ApplyRelicAcquisitionRunEffects(
                    player, trigger.Effects, rng, allocateCardId,
                    nativeRunRng);
            }
        }

        return player;
    }

    private static PlayerState ApplyRelicAcquisitionRunEffects(
        PlayerState player,
        IEnumerable<PrototypeRunEffectSpec> effects,
        RngBundle? rng,
        Func<long>? allocateCardId,
        RngBundle? nativeRunRng)
    {
        foreach (var effect in effects)
        {
            switch (effect.Kind)
            {
                case PrototypeRunEffectKind.AddCard:
                    player = AppendCard(
                        player,
                        (allocateCardId ?? throw new InvalidOperationException(
                            "Card-granting relic acquisition requires an instance allocator."))(),
                        effect.CardId ?? throw new InvalidOperationException(
                            "Card grant requires a card ID."),
                        rng ?? throw new InvalidOperationException(
                            "Card grant requires RNG context."),
                        nativeRunRng);
                    break;

                case PrototypeRunEffectKind.GainRandomRareCard:
                {
                    var rewardRng = rng ?? throw new InvalidOperationException(
                        "Random Rare-card relic acquisition requires RNG context.");
                    var cardAllocator = allocateCardId
                        ?? throw new InvalidOperationException(
                            "Random Rare-card relic acquisition requires an instance allocator.");
                    var rarePool = PrototypeContent.RewardCardPool
                        .Where(cardId => PrototypeContent.Card(cardId).Rarity
                            == PrototypeCardRarity.Rare)
                        .ToArray();
                    if (rarePool.Length == 0)
                    {
                        throw new InvalidOperationException(
                            "Random Rare-card reward has an empty pool.");
                    }

                    var ownerId = "player:rare_pool:"
                        + CanonicalJson.Sha256(rarePool);
                    var cardIndex = nativeRunRng is { } nativeRewardRng
                        ? NativePlayerRngV111.ChooseIndex(
                            nativeRewardRng,
                            "relic.acquisition.random_rare_card.model",
                            NativePlayerRngV111.RewardsStreamId,
                            ownerId,
                            rarePool.Length)
                        : ResolveLegacyRandomIndex(
                            rewardRng,
                            "relic.acquisition.random_rare_card.model",
                            ownerId,
                            Enumerable.Range(0, rarePool.Length),
                            streamId: "reward",
                            controlledCandidateLabels:
                                (string[])rarePool.Clone());
                    var cardId = rarePool[cardIndex];
                    player = AppendCard(
                        player, cardAllocator(), cardId, rewardRng,
                        nativeRunRng);
                    break;
                }

                case PrototypeRunEffectKind.GainPotionSlots:
                    if (effect.Amount <= 0)
                    {
                        throw new InvalidOperationException(
                            "Potion slot increase must be positive.");
                    }
                    player = player with
                    {
                        PotionSlots = player.PotionSlots
                            .Concat(new PotionInstance?[effect.Amount])
                            .ToArray()
                    };
                    break;

                case PrototypeRunEffectKind.GainPotion:
                case PrototypeRunEffectKind.GainRandomPotion:
                {
                    if (!CanAcquirePotion(player))
                    {
                        throw new InvalidOperationException(
                            "Player cannot acquire potions.");
                    }

                    var slot = Array.IndexOf(player.PotionSlots, null);
                    if (slot < 0)
                    {
                        throw new InvalidOperationException(
                            "Relic pickup potion reward has no open slot.");
                    }

                    string potionId;
                    if (effect.Kind == PrototypeRunEffectKind.GainRandomPotion)
                    {
                        var rewardRng = rng ?? throw new InvalidOperationException(
                            "Random potion relic acquisition requires RNG context.");
                        var potionPool = PrototypeContent.PotionPool;
                        var potionOwner = "player:potion_slot:" + slot + ":pool:"
                            + CanonicalJson.Sha256(potionPool);
                        var potionIndex = nativeRunRng is { } nativeRewardRng
                            ? NativePlayerRngV111.ChooseIndex(
                                nativeRewardRng,
                                "relic.acquisition.random_potion.model",
                                NativePlayerRngV111.RewardsStreamId,
                                potionOwner,
                                potionPool.Length)
                            : ResolveLegacyRandomIndex(
                                rewardRng,
                                "relic.acquisition.random_potion.model",
                                potionOwner,
                                Enumerable.Range(0, potionPool.Length),
                                streamId: "event",
                                controlledCandidateLabels:
                                    (string[])potionPool.Clone());
                        potionId = potionPool[potionIndex];
                    }
                    else
                    {
                        potionId = effect.PotionId
                            ?? throw new InvalidOperationException(
                                "Potion relic acquisition requires a potion ID.");
                    }
                    _ = PrototypeContent.Potion(potionId);
                    var slots = (PotionInstance?[])player.PotionSlots.Clone();
                    slots[slot] = new PotionInstance(
                        potionId, PrototypeJson.EmptyObject());
                    player = player with { PotionSlots = slots };
                    break;
                }

                case PrototypeRunEffectKind.TransformFirstCardOfId:
                {
                    var cardId = effect.CardId ?? throw new InvalidOperationException(
                        "Transform-first-card effect requires a card ID.");
                    var first = player.Deck.FirstOrDefault(card =>
                        StringComparer.Ordinal.Equals(card.CardId, cardId));
                    if (first is not null)
                    {
                        player = TransformPersistentDeckCard(
                            player,
                            first.InstanceId,
                            upgradeResult: false,
                            rng ?? throw new InvalidOperationException(
                                "Deck transformation requires RNG context."));
                    }
                    break;
                }

                default:
                    player = ApplyPlayerRunEffects(
                        player, [effect], rng, nativeRunRng);
                    break;
            }
        }

        return player;
    }

    private static RunState StepRunStart(RunState state, GameAction action)
    {
        if (!ControlledChanceGuardV111.Active
            || !ControlledEventChoiceReplayV111.IsActive
            || action.Kind != "start_run"
            || state.Configuration is not null)
            return StartRun(state, action);

        try
        {
            return StartRun(state, action);
        }
        catch (ControlledEventChoiceChanceRequiredException exception)
        {
            var bossPool = PrototypeContent.OvergrowthBossEncounterPool;
            if (exception.Request.SiteId !=
                    "run.encounter.overgrowth_boss.index_draw"
                || exception.Request.StreamId != "combat"
                || exception.CandidateLabels is null
                || !exception.CandidateLabels.SequenceEqual(
                    bossPool, StringComparer.Ordinal))
                throw new NotSupportedException(
                    "Controlled run start reached an unsupported chance request.");

            var continuation = new
                PrototypeControlledRunActionContinuationV111(
                    state,
                    "start_run",
                    [],
                    [],
                    exception.Request,
                    exception.CandidateLabels);
            continuation.Validate();
            return state with { ControlledRunActionChoice = continuation };
        }
    }

    private static RunState StartRun(RunState state, GameAction action)
    {
        RequireKind(action, "start_run");

        if (state.Configuration is not null)
            return V111RunFactory.Initialize(state);

        var rules = PrototypeContent.Rules;
        // Ordinary starts use the historical correlated run-seed streams.
        // A distinct, hypothetical factorized prior may instead supply
        // all six independent stream states *before* the first RNG event.
        var rng = state.Rng.Streams.Length == 0
            ? PrototypeRng.CreateBundle(state.RunSeed)
            : state.Rng.Fork();
        var bossPool = PrototypeContent.OvergrowthBossEncounterPool;
        var actOneBossEncounterId = bossPool[
            ResolveLegacyRandomIndex(
                rng,
                "run.encounter.overgrowth_boss.index_draw",
                "run:act:1:boss_pool:" + CanonicalJson.Sha256(bossPool),
                Enumerable.Range(0, bossPool.Length),
                streamId: "combat",
                controlledCandidateLabels:
                    (string[])bossPool.Clone())];
        var nextId = 1L;
        var deck = PrototypeContent.StartingDeck
            .Select(cardId => new CardInstance(
                nextId++,
                cardId,
                0,
                PrototypeJson.EmptyObject()))
            .ToArray();

        var player = new PlayerState(
            Hp: rules.StartingHp,
            MaxHp: rules.StartingHp,
            Gold: rules.StartingGold,
            Deck: deck,
            Relics: PrototypeContent.StartingRelics
                .Select(relicId => new RelicInstance(
                    relicId,
                    PrototypeJson.EmptyObject()))
                .ToArray(),
            PotionSlots: new PotionInstance?[rules.PotionSlots]);

        var world = new RunWorldState(
            RulesetId: rules.Id,
            CharacterId: PrototypeContent.CharacterId,
            Act: 1,
            Floor: 0,
            NextCardInstanceId: nextId,
            ActiveRoom: null,
            Map: new MapState(Array.Empty<MapNodeState>()),
            Combat: null,
            Reward: null,
            Shop: null,
            Event: null,
            TerminalOutcome: null,
            ActOneRegion: PrototypeActOneRegion.Overgrowth,
            ActOneEncounterPool: new PrototypeActOneEncounterPoolState(
                PrototypeActOneRegion.Overgrowth,
                OrdinaryCombatsStarted: 0,
                RemainingWeakEncounterIds:
                    (string[])PrototypeContent
                        .OvergrowthWeakEncounterPool
                        .Clone(),
                RemainingNormalEncounterIds:
                    (string[])PrototypeContent
                        .OvergrowthNormalEncounterPool
                        .Clone(),
                RemainingEliteEncounterIds:
                    (string[])PrototypeContent
                        .OvergrowthEliteEncounterPool
                        .Clone(),
                BossEncounterId:
                    actOneBossEncounterId));

        world = world with { Map = GenerateActMap(world.Act, rng) };

        return state with
        {
            Player = player,
            Rng = rng,
            World = world,
            Phase = RunPhase.MapChoice
        };
    }

    private static RunState StepActTransition(RunState state, GameAction action)
    {
        RequireKind(action, "continue_act");
        var world = RequireWorld(state);

        if (state.Configuration?.UsesExperimentalSilentThreeActRoute == true)
            return ExperimentalSilentThreeActRouteV111.ContinueAct(state);

        if (state.Configuration is not null)
            throw new NotSupportedException("Native Hive/Glory mechanics are not implemented; prototype later-act fallback is disabled for v111 runs.");

        if (world.Act >= PrototypeContent.Rules.Acts)
        {
            throw new InvalidOperationException("The final act cannot transition to another act.");
        }

        world = world with
        {
            Act = world.Act + 1,
            Floor = 0,
            ActiveRoom = null,
            Combat = null,
            Reward = null,
            CustomRewardReturn = null,
            Shop = null,
            Event = null
        };
        world = world with { Map = GenerateActMap(world.Act, state.Rng) };

        return state with
        {
            World = world,
            Phase = RunPhase.MapChoice
        };
    }

    private static RunWorldState RequireWorld(RunState state)
    {
        var world = state.World
            ?? throw new InvalidOperationException("Prototype world state has not been initialized.");

        if (!StringComparer.Ordinal.Equals(world.RulesetId, state.Configuration?.RulesetId ?? PrototypeContent.RulesetId))
        {
            throw new InvalidOperationException(
                $"Prototype engine cannot run ruleset '{world.RulesetId}'.");
        }

        return world;
    }

    private static void RequireKind(GameAction action, string expected)
    {
        if (!StringComparer.Ordinal.Equals(action.Kind, expected))
        {
            throw new InvalidOperationException(
                $"Expected action '{expected}', received '{action.Kind}'.");
        }
    }

    private static CardInstance RequireCard(PlayerState player, long instanceId) =>
        player.Deck.FirstOrDefault(card => card.InstanceId == instanceId)
        ?? throw new InvalidOperationException($"Card instance {instanceId} is missing.");

    private static bool PreventsGoldGain(PlayerState player) =>
        player.ActiveRelics.Any(relic => PrototypeContent.Relic(relic.RelicId)
            .PreventGoldGain);

    private static int GoldAfterGain(PlayerState player, int amount) =>
        amount <= 0 || PreventsGoldGain(player)
            ? player.Gold
            : checked(player.Gold + amount);

    private static int EnergyPerTurn(PlayerState player, int turn = 1) =>
        PrototypeContent.Rules.BaseEnergy
        + player.ActiveRelics.Sum(relic =>
        {
            var definition = PrototypeContent.Relic(relic.RelicId);
            return turn >= definition.EnergyBonusMinTurn ? definition.EnergyPerTurnBonus : 0;
        })
        + player.ActiveRelics.Count(relic => relic.RelicId == NativeAncientRewardsV111.PumpkinCandleId
            && NativeAncientRewardsV111.KindleCount(relic) > 0);

    private static int FirstTurnDrawBonus(PlayerState player) =>
        player.ActiveRelics.Sum(relic => PrototypeContent.Relic(relic.RelicId).FirstTurnDrawBonus);

    private static int RewardCardChoiceCount(PlayerState player) =>
        Math.Max(
            0,
            3 + player.ActiveRelics.Sum(relic =>
                PrototypeContent.Relic(relic.RelicId)
                    .RewardCardChoiceBonus));

    private static int ExtraNormalCombatCardRewardGroups(
        PlayerState player) =>
        Math.Max(
            0,
            player.ActiveRelics.Sum(relic =>
                PrototypeContent.Relic(relic.RelicId)
                    .ExtraNormalCombatCardRewardGroups));

    private static bool CanAcquirePotion(PlayerState player) =>
        !player.ActiveRelics.Any(relic =>
            PrototypeContent.Relic(relic.RelicId)
                .PreventPotionAcquisition);

    private static bool CanRestHeal(PlayerState player) =>
        !player.ActiveRelics.Any(relic =>
            PrototypeContent.Relic(relic.RelicId)
                .PreventRestHealing);

    private static bool CanRestCook(PlayerState player) =>
        player.ActiveRelics.Any(relic =>
            relic.RelicId == NativeGloryAncientsV111.MeatCleaverId)
        && player.Deck.Count(card =>
            !PrototypeCardRules.IsEternal(card)) >= 2;

    private static bool CanRestUpgrade(PlayerState player) =>
        !player.ActiveRelics.Any(relic =>
            PrototypeContent.Relic(relic.RelicId)
                .PreventRestUpgrade);

    private static bool HideEnemyIntents(PlayerState player) =>
        player.ActiveRelics.Any(relic =>
            PrototypeContent.Relic(relic.RelicId)
                .HideEnemyIntents);

    private static int ApplyShopPriceModifiers(
        PlayerState player,
        int basePrice)
    {
        long numerator = Math.Max(0, basePrice);
        long denominator = 1;

        foreach (var relic in player.ActiveRelics)
        {
            var definition =
                PrototypeContent.Relic(relic.RelicId);
            if (definition.ShopPriceNumerator < 0
                || definition.ShopPriceDenominator <= 0)
            {
                throw new InvalidOperationException(
                    $"Relic '{definition.Id}' has an invalid shop-price multiplier.");
            }

            numerator *= definition.ShopPriceNumerator;
            denominator *= definition.ShopPriceDenominator;
        }

        return (int)Math.Max(
            0,
            numerator / denominator);
    }

    private static int ShopRemovalPrice(
        PlayerState player,
        int basePrice)
    {
        var overridePrices = player.ActiveRelics
            .Select(relic =>
                PrototypeContent.Relic(relic.RelicId)
                    .ShopRemovalPriceOverride)
            .Where(price => price is not null)
            .Select(price => price!.Value)
            .ToArray();
        var undiscounted = overridePrices.Length == 0
            ? basePrice
            : overridePrices.Min();
        return ApplyShopPriceModifiers(
            player,
            undiscounted);
    }

    private static ShopState RepriceShop(
        ShopState shop,
        PlayerState player)
    {
        ShopOffer Reprice(ShopOffer offer)
        {
            var basePrice = offer.UndiscountedPrice;
            return offer with
            {
                Price = ApplyShopPriceModifiers(
                    player,
                    basePrice),
                BasePrice = basePrice
            };
        }

        var baseRemovalPrice =
            shop.UndiscountedRemovalPrice;
        return shop with
        {
            CardOffers = shop.CardOffers
                .Select(Reprice)
                .ToArray(),
            PotionOffer = shop.PotionOffer is null
                ? null
                : Reprice(shop.PotionOffer),
            RelicOffer = shop.RelicOffer is null
                ? null
                : Reprice(shop.RelicOffer),
            AdditionalPotionOffers =
                shop.AdditionalPotionOffers is null
                    ? null
                    : shop.AdditionalPotionOffers
                        .Select(Reprice)
                        .ToArray(),
            AdditionalRelicOffers =
                shop.AdditionalRelicOffers is null
                    ? null
                    : shop.AdditionalRelicOffers
                        .Select(Reprice)
                        .ToArray(),
            RemovalPrice = ShopRemovalPrice(
                player,
                baseRemovalPrice),
            BaseRemovalPrice = baseRemovalPrice
        };
    }

    private static PlayerState AppendCard(
        PlayerState player,
        long instanceId,
        string cardId,
        RngBundle rng,
        RngBundle? nativeRunRng = null)
    {
        var definition = PrototypeContent.Card(cardId);
        var upgradeLevel = InitialAddedCardUpgradeLevel(player, definition);

        var card = ApplyNewCardEnchantments(
            player,
            new CardInstance(
                instanceId,
                cardId,
                upgradeLevel,
                PrototypeJson.EmptyObject()));
        player = player with
        {
            Deck = player.Deck.Append(card).ToArray()
        };

        return ApplyRelicRunEvent(
            player,
            PrototypeRunEventKind.CardAdded,
            addedCard: card,
            rng: rng,
            nativeRunRng: nativeRunRng);
    }

    internal static int InitialAddedCardUpgradeLevel(
        PlayerState player, PrototypeCardDefinition definition) =>
        player.ActiveRelics.Any(relic =>
        {
            var relicDefinition =
                PrototypeContent.Relic(relic.RelicId);
            return relicDefinition.UpgradeAddedCardType
                    == definition.Type
                && definition.Type is not
                    PrototypeCardType.Status
                    and not PrototypeCardType.Curse;
        }) ? 1 : 0;

    /// <summary>
    /// Apply acquisition-time relic enchantments to persistent cards.
    /// Cards that already carry a persistent enchantment preserve it.
    /// Source: FresnelLens.TryModifyCardBeingAddedToDeck.
    /// </summary>
    internal static CardInstance ApplyNewCardEnchantments(
        PlayerState player, CardInstance card)
    {
        if (card.Enchantment is not null)
        {
            return card;
        }

        var definition = PrototypeContent.Card(card.CardId);
        if (!PrototypeCardRules.GainsBlock(definition))
        {
            return card;
        }

        // Fresnel Lens attaches Nimble through an ordered relic hook.
        // The pinned Nimble EnchantmentModel is NOT stackable, so the
        // first owned Lens enchants the card; later copies cannot add
        // their amounts again to an already enchanted card.
        var amount = player.ActiveRelics
            .Select(relic => PrototypeContent.Relic(relic.RelicId)
                .EnchantNewBlockCardsNimble)
            .FirstOrDefault(value => value > 0);
        return amount > 0
            ? card with
            {
                Enchantment = new PrototypeCardEnchantment(
                    PrototypeCardEnchantmentKind.Nimble, amount)
            }
            : card;
    }

    /// <summary>
    /// Compute the total non-minion HP over the whole encounter, including
    /// completed forms and future scripted death-spawn stages. Defeated
    /// parents remain in the formation. Before the non-minion death summons
    /// exist, their declared maximum HP is reserved as *both* remaining and
    /// total HP; once created the budget uses their actual rolled MaxHp.
    /// A staged transition must not erase progress earned against the parent.
    /// Minion definitions and dynamically minion-marked enemies are excluded.
    /// </summary>
    internal static (int Total, int Remaining) NonMinionEncounterHp(
        CombatState combat)
    {
        var total = 0;
        var remaining = 0;
        foreach (var enemy in combat.Enemies)
        {
            var definition = PrototypeContent.Enemy(enemy.EnemyId);
            if (definition.IsMinion ||
                enemy.PowerStates.Any(power =>
                    power.PowerId == "proto.power.minion" && power.Stacks > 0))
                continue;
            total = checked(total + Math.Max(enemy.MaxHp, Math.Max(0, enemy.Hp))
                + enemy.CompletedPhaseMaxHp);
            remaining = checked(remaining + Math.Max(0, enemy.Hp));

            // Summons are deterministic stage obligations, while their exact
            // HP is rolled on creation. Max is a stable conservative budget:
            // replacing it with the rolled value cannot decrease progress.
            if (!enemy.DeathEffectsResolved && definition.DeathSummons is { } summons)
                foreach (var summon in summons)
                {
                    var summoned = PrototypeContent.Enemy(summon.EnemyId);
                    if (summoned.IsMinion ||
                        summoned.StartingPowers?.Any(power =>
                            power.PowerId == "proto.power.minion") == true)
                        continue;
                    var pendingHp = summoned.HpRangeAt(
                        combat.Act, combat.Ascension).Max;
                    total = checked(total + pendingHp);
                    remaining = checked(remaining + pendingHp);
                }

            // The Test Subject restores the same monster's HP twice. These
            // phases are source-declared, not new combat instances.
            if (enemy.EnemyId == PrototypeNativeGloryBosses.TestSubjectId)
            {
                if (enemy.BossPhase < 1)
                {
                    var hp = combat.Ascension >= 8 ? 212 : 200;
                    total = checked(total + hp);
                    remaining = checked(remaining + hp);
                }
                if (enemy.BossPhase < 2)
                {
                    var hp = combat.Ascension >= 8 ? 313 : 300;
                    total = checked(total + hp);
                    remaining = checked(remaining + hp);
                }
            }
        }
        return (total, remaining);
    }

    private static RunState EndRun(RunState state, string outcome)
    {
        var original = RequireWorld(state);
        var nonMinion = outcome == "defeat" && original.Combat is { } combat
            ? NonMinionEncounterHp(combat) : ((int Total, int Remaining)?)null;
        var world = original with
        {
            // Keep the legacy all-enemy terminal scalar for existing clients.
            DefeatRemainingEnemyHp = outcome == "defeat"
                ? original.Combat?.Enemies.Sum(enemy => Math.Max(0, enemy.Hp))
                : null,
            DefeatTotalNonMinionEnemyHp = nonMinion?.Total,
            DefeatRemainingNonMinionEnemyHp = nonMinion?.Remaining,
            Combat = null,
            Reward = null,
            Shop = null,
            Event = null,
            ActiveRoom = null,
            TerminalOutcome = outcome
        };

        return state with
        {
            World = world,
            Phase = RunPhase.Terminal
        };
    }
}
