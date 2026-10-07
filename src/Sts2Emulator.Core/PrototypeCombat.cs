namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static RunState StartCombat(RunState state, PrototypeRoomType roomType)
    {
        var world = RequireWorld(state);
        var encounters = PrototypeContent.Encounters
            .Where(encounter =>
                encounter.RoomType == roomType
                && world.Act >= encounter.MinAct
                && world.Act <= encounter.MaxAct
                && world.Floor >= encounter.MinFloor
                && world.Floor <= encounter.MaxFloor
                && encounter.Weight > 0)
            .ToArray();

        if (encounters.Length == 0)
        {
            throw new InvalidOperationException(
                $"No prototype encounter is eligible for {roomType} " +
                $"at act {world.Act}, floor {world.Floor}.");
        }

        var previousEncounter = world.EncounterIds.LastOrDefault();
        if (previousEncounter is not null && encounters.Length > 1)
        {
            var withoutImmediateRepeat = encounters
                .Where(encounter =>
                    !StringComparer.Ordinal.Equals(encounter.Id, previousEncounter))
                .ToArray();
            if (withoutImmediateRepeat.Length > 0)
            {
                encounters = withoutImmediateRepeat;
            }
        }

        var encounter = PickWeightedEncounter(encounters, state.Rng);

        var combatRelics = state.Player.Relics
            .Select((relic, index) =>
            {
                var definition = PrototypeContent.Relic(relic.RelicId);
                return new CombatRelicState(
                    PersistentIndex: index,
                    RelicId: relic.RelicId,
                    ApplicationOrder: index + 1L,
                    TriggerCounts: new int[(definition.Triggers ?? Array.Empty<PrototypeRelicTriggerSpec>()).Length]);
            })
            .ToArray();

        var combatPotions = state.Player.PotionSlots
            .Select((potion, slot) => potion is null
                ? null
                : new CombatPotionState(
                    Slot: slot,
                    PotionId: potion.PotionId,
                    State: potion.PersistentState.Clone()))
            .Where(potion => potion is not null)
            .Select(potion => potion!)
            .ToArray();

        var nextPowerApplicationOrder = combatRelics.Length + 1L;
        var enemies = encounter.EnemySpecs
            .Select((enemySpec, index) =>
            {
                var definition = PrototypeContent.Enemy(
                    enemySpec.EnemyId);
                var hpRange = definition.HpRangeAt(
                    world.Act,
                    state.Ascension);
                var hp = hpRange.Min == hpRange.Max
                    ? hpRange.Min
                    : hpRange.Min
                        + PrototypeRng.NextInt(
                            state.Rng,
                            "combat",
                            hpRange.Max - hpRange.Min + 1);
                var powers = (definition.StartingPowers ?? Array.Empty<PrototypeStartingPowerSpec>())
                    .Select(power =>
                    {
                        _ = PrototypeContent.Power(power.PowerId);
                        return new PrototypePowerInstanceState(
                            power.PowerId,
                            power.Stacks,
                            nextPowerApplicationOrder++);
                    })
                    .Where(power => power.Stacks > 0)
                    .ToArray();

                return new EnemyCombatState(
                    InstanceId: index + 1,
                    EnemyId: enemySpec.EnemyId,
                    Hp: hp,
                    Block: 0,
                    MoveIndex: 0,
                    Statuses: new Dictionary<string, int>(StringComparer.Ordinal),
                    Powers: powers,
                    FormationPosition:
                        enemySpec.FormationPosition,
                    SlotName: enemySpec.SlotName);
            })
            .ToArray();

        var nextCombatCardId = 1L;
        var combatCards = state.Player.Deck
            .Select(card => new CombatCardInstance(
                InstanceId: nextCombatCardId++,
                PersistentCardInstanceId: card.InstanceId,
                CardId: card.CardId,
                UpgradeLevel: card.UpgradeLevel,
                IsTemporary: false,
                State: card.PersistentState.Clone(),
                Enchantment: card.Enchantment))
            .ToArray();

        var innateCards = combatCards
            .Where(IsInnate)
            .Select(card => card.InstanceId)
            .ToArray();
        var drawPile = combatCards
            .Where(card => !IsInnate(card))
            .Select(card => card.InstanceId)
            .ToArray();
        PrototypeRng.Shuffle(state.Rng, "combat", drawPile);

        var combat = new CombatState(
            Turn: 1,
            Energy: EnergyPerTurn(state.Player),
            PlayerBlock: 0,
            Hand: innateCards,
            DrawPile: drawPile,
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies: enemies,
            NextCardInstanceId: nextCombatCardId,
            Cards: combatCards,
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: nextPowerApplicationOrder,
            Relics: combatRelics,
            Potions: combatPotions);

        var openingHandTarget =
            PrototypeContent.Rules.HandSize + FirstTurnDrawBonus(state.Player);
        var openingDraw = DrawCards(
            state.Player,
            combat,
            Math.Max(0, openingHandTarget - combat.Hand.Length),
            state.Rng,
            fromHandDraw: true);
        state = state with { Player = openingDraw.Player };
        combat = openingDraw.Combat;

        world = world with
        {
            Combat = combat,
            EncounterHistory = world.EncounterIds.Append(encounter.Id).ToArray()
        };

        return state with
        {
            World = world,
            Phase = RunPhase.Combat
        };
    }

    private static PrototypeEncounterDefinition PickWeightedEncounter(
        PrototypeEncounterDefinition[] encounters,
        RngBundle rng)
    {
        var totalWeight = encounters.Sum(encounter => encounter.Weight);
        if (totalWeight <= 0)
        {
            throw new InvalidOperationException("Eligible encounter pool has no positive weight.");
        }

        var roll = PrototypeRng.NextInt(rng, "combat", totalWeight);
        foreach (var encounter in encounters)
        {
            if (roll < encounter.Weight)
            {
                return encounter;
            }

            roll -= encounter.Weight;
        }

        throw new InvalidOperationException("Weighted encounter selection fell through.");
    }

    private static IReadOnlyList<GameAction> GetCombatActions(RunState state)
    {
        var combat = RequireWorld(state).Combat
            ?? throw new InvalidOperationException("Combat phase has no combat state.");

        if (combat.PendingChoice is not null)
        {
            return GetPendingChoiceActions(combat.PendingChoice);
        }

        var actions = new List<GameAction>();

        foreach (var instanceId in combat.Hand)
        {
            var card = RequireCombatCard(combat, instanceId);
            var definition = PrototypeContent.Card(card.CardId);
            var isFreeByPower = IsCardFreeByPower(combat, definition);
            if (definition.Unplayable
                || (!isFreeByPower
                    && !IsCardAffordable(
                        combat,
                        card,
                        definition))
                || !EvaluateCombatPredicate(
                    definition.PlayCondition,
                    combat,
                    targetEnemyId: null))
            {
                continue;
            }

            if (definition.Target == PrototypeCardTarget.Enemy)
            {
                foreach (var enemy in combat.Enemies.Where(enemy => enemy.Hp > 0))
                {
                    actions.Add(GameAction.Create(
                        "play_card",
                        new PlayCardPayload(instanceId, enemy.InstanceId)));
                }
            }
            else
            {
                actions.Add(GameAction.Create(
                    "play_card",
                    new PlayCardPayload(instanceId, null)));
            }
        }

        for (var slot = 0; slot < state.Player.PotionSlots.Length; slot++)
        {
            var potion = state.Player.PotionSlots[slot];
            if (potion is null)
            {
                continue;
            }

            var definition = PrototypeContent.Potion(potion.PotionId);
            if (definition.Target == PrototypeCardTarget.Enemy)
            {
                foreach (var enemy in combat.Enemies.Where(enemy => enemy.Hp > 0))
                {
                    actions.Add(GameAction.Create(
                        "use_potion",
                        new UsePotionPayload(slot, enemy.InstanceId)));
                }
            }
            else
            {
                actions.Add(GameAction.Create(
                    "use_potion",
                    new UsePotionPayload(slot, null)));
            }
        }

        actions.Add(GameAction.Empty("end_turn"));
        return actions;
    }

    private static RunState StepCombat(RunState state, GameAction action)
    {
        var combat = RequireWorld(state).Combat
            ?? throw new InvalidOperationException("Combat phase has no combat state.");

        if (combat.PendingChoice is not null)
        {
            return ResolvePendingChoice(state, action);
        }

        return action.Kind switch
        {
            "play_card" => PlayCard(state, action.ReadPayload<PlayCardPayload>()),
            "use_potion" => UsePotion(state, action.ReadPayload<UsePotionPayload>()),
            "end_turn" => EndPlayerTurn(state),
            _ => throw new InvalidOperationException($"Unknown combat action '{action.Kind}'.")
        };
    }

    private static RunState PlayCard(RunState state, PlayCardPayload payload)
    {
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException("Combat phase has no combat state.");

        if (!combat.Hand.Contains(payload.CardInstanceId))
        {
            throw new InvalidOperationException($"Card {payload.CardInstanceId} is not in hand.");
        }

        var card = RequireCombatCard(combat, payload.CardInstanceId);
        var definition = PrototypeContent.Card(card.CardId);
        if (definition.Unplayable)
        {
            throw new InvalidOperationException(
                $"Card {payload.CardInstanceId} is unplayable.");
        }

        var isFreeByPower = IsCardFreeByPower(combat, definition);
        if (!isFreeByPower
            && !IsCardAffordable(
                combat,
                card,
                definition))
        {
            throw new InvalidOperationException($"Card {payload.CardInstanceId} is unaffordable.");
        }

        if (!EvaluateCombatPredicate(
                definition.PlayCondition,
                combat,
                payload.TargetEnemyId))
        {
            throw new InvalidOperationException(
                $"Card {payload.CardInstanceId} does not satisfy its play condition.");
        }

        ValidateTarget(definition.Target, payload.TargetEnemyId, combat);
        var energySpent = isFreeByPower
            ? 0
            : ResolveCardEnergySpent(
                combat,
                card,
                definition);

        combat = combat with
        {
            Energy = combat.Energy - energySpent,
            Hand = combat.Hand.Where(id => id != payload.CardInstanceId).ToArray()
        };

        if (isFreeByPower)
        {
            combat = ConsumeMatchingFreeCardPower(combat, definition.Type);
        }

        var playCountResult =
            ResolveCardPlayCountAndConsumeModifiers(
                combat,
                card,
                definition.Type);
        combat = playCountResult.Combat;

        var sourceDestination = CardExhaustsOnUse(
                definition,
                card.UpgradeLevel)
            ? PrototypeCardZone.ExhaustPile
            : PrototypeCardZone.DiscardPile;

        var series = new PrototypeCardPlaySeriesState(
            SourceCardInstanceId: payload.CardInstanceId,
            SourceCardDestination: sourceDestination,
            TargetEnemyId: payload.TargetEnemyId,
            EnergySpent: energySpent,
            PlayCount: playCountResult.PlayCount,
            NextPlayIndex: 0,
            RemoveSourceCardOnCompletion:
                definition.Type == PrototypeCardType.Power);

        var resolved = ResolveCardPlaySeries(
            state.Player,
            combat,
            state.Rng,
            series);
        combat = resolved.Combat;

        state = state with
        {
            Player = resolved.Player,
            World = world with { Combat = combat }
        };

        return AllEnemiesDefeated(combat)
            ? EnterReward(state)
            : state;
    }

    private static RunState UsePotion(RunState state, UsePotionPayload payload)
    {
        if (payload.Slot < 0 || payload.Slot >= state.Player.PotionSlots.Length)
        {
            throw new InvalidOperationException($"Potion slot {payload.Slot} is invalid.");
        }

        var potion = state.Player.PotionSlots[payload.Slot]
            ?? throw new InvalidOperationException($"Potion slot {payload.Slot} is empty.");
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException("Combat phase has no combat state.");
        var combatPotion = combat.PotionStates.SingleOrDefault(item => item.Slot == payload.Slot)
            ?? throw new InvalidOperationException(
                $"Combat potion state for slot {payload.Slot} is missing.");

        if (!StringComparer.Ordinal.Equals(potion.PotionId, combatPotion.PotionId))
        {
            throw new InvalidOperationException(
                $"Persistent/combat potion IDs disagree in slot {payload.Slot}.");
        }

        var definition = PrototypeContent.Potion(combatPotion.PotionId);
        ValidateTarget(definition.Target, payload.TargetEnemyId, combat);

        var operations = new Queue<PrototypeQueuedOperation>();
        foreach (var effect in definition.Effects)
        {
            EnqueueEffectOperations(
                operations,
                effect,
                0,
                0,
                payload.TargetEnemyId,
                combat,
                sourceKind: PrototypeEffectSourceKind.Potion);
        }

        var resolved = ResolveOperations(state.Player, combat, operations, state.Rng);
        var slots = (PotionInstance?[])resolved.Player.PotionSlots.Clone();
        slots[payload.Slot] = null;

        var combatAfterPotion = resolved.Combat with
        {
            Potions = resolved.Combat.PotionStates
                .Where(item => item.Slot != payload.Slot)
                .ToArray()
        };

        state = state with
        {
            Player = resolved.Player with { PotionSlots = slots },
            World = world with { Combat = combatAfterPotion }
        };

        return AllEnemiesDefeated(combatAfterPotion)
            ? EnterReward(state)
            : state;
    }

    private static RunState EndPlayerTurn(RunState state)
    {
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException("Combat phase has no combat state.");

        combat = combat with
        {
            IsPlayerTurn = false,
            AutomaticPipelineContinuation = null
        };
        state = state with
        {
            World = world with { Combat = combat }
        };

        return ResumeAutomaticPipeline(state, 0);
    }

    private static RunState ResumeAutomaticPipeline(
        RunState state,
        int startIndex,
        PrototypeCombatEventKind? pendingPostDispatchEventKind = null)
    {
        if (pendingPostDispatchEventKind is not null)
        {
            state = ResolvePostDispatchAutomaticPolicy(
                state,
                pendingPostDispatchEventKind.Value);

            if (RequireWorld(state).Combat?.PendingChoice is not null)
            {
                return SetAutomaticPipelineContinuation(
                    state,
                    startIndex,
                    pendingPostDispatchEventKind: null);
            }
        }

        for (var index = startIndex;
             index < PrototypeContent.Rules.EndTurnPipeline.Length;
             index++)
        {
            var automaticStep =
                PrototypeContent.Rules.EndTurnPipeline[index];
            state = ResolveAutomaticStep(state, automaticStep);

            var currentCombat = RequireWorld(state).Combat
                ?? throw new InvalidOperationException(
                    "Combat unexpectedly disappeared.");

            if (currentCombat.PendingChoice is not null)
            {
                return SetAutomaticPipelineContinuation(
                    state,
                    index + 1,
                    automaticStep.Kind
                            == PrototypeAutomaticStepKind.DispatchCombatEvent
                        ? automaticStep.EventKind
                        : null);
            }

            if (automaticStep.Kind
                    == PrototypeAutomaticStepKind.DispatchCombatEvent
                && automaticStep.EventKind is not null)
            {
                state = ResolvePostDispatchAutomaticPolicy(
                    state,
                    automaticStep.EventKind.Value);

                currentCombat = RequireWorld(state).Combat
                    ?? throw new InvalidOperationException(
                        "Combat unexpectedly disappeared.");
                if (currentCombat.PendingChoice is not null)
                {
                    return SetAutomaticPipelineContinuation(
                        state,
                        index + 1,
                        pendingPostDispatchEventKind: null);
                }
            }

            if (state.Player.Hp <= 0)
            {
                return EndRun(state, "defeat");
            }

            currentCombat = RequireWorld(state).Combat
                ?? throw new InvalidOperationException(
                    "Combat unexpectedly disappeared.");
            if (AllEnemiesDefeated(currentCombat))
            {
                return EnterReward(state);
            }
        }

        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException(
                "Combat unexpectedly disappeared.");
        return state with
        {
            World = world with
            {
                Combat = combat with
                {
                    AutomaticPipelineContinuation = null
                }
            }
        };
    }

    private static RunState SetAutomaticPipelineContinuation(
        RunState state,
        int nextStepIndex,
        PrototypeCombatEventKind? pendingPostDispatchEventKind)
    {
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException(
                "Combat unexpectedly disappeared.");
        return state with
        {
            World = world with
            {
                Combat = combat with
                {
                    AutomaticPipelineContinuation =
                        new PrototypeAutomaticPipelineContinuationState(
                            nextStepIndex,
                            pendingPostDispatchEventKind)
                }
            }
        };
    }

    private static RunState ResolvePostDispatchAutomaticPolicy(
        RunState state,
        PrototypeCombatEventKind eventKind)
    {
        if (eventKind != PrototypeCombatEventKind.PlayerTurnStarted)
        {
            return state;
        }

        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException(
                "Combat unexpectedly disappeared.");
        var discardCount = combat.PlayerPowers.Sum(power =>
            PrototypeContent.Power(power.PowerId)
                .DiscardAfterPlayerTurnStartPerStack
            * power.Stacks);

        if (discardCount <= 0 || combat.Hand.Length == 0)
        {
            return state;
        }

        var effectiveCount = Math.Min(
            discardCount,
            combat.Hand.Length);
        combat = combat with
        {
            PendingChoice = new PendingCombatChoiceState(
                ChoiceId: "select_cards",
                SourceCardInstanceId: null,
                SourceCardDestination:
                    PrototypeCardZone.DiscardPile,
                Selection: new PrototypeCardSelectionSpec(
                    PrototypeCardZone.Hand,
                    effectiveCount,
                    effectiveCount,
                    PrototypeCardSelectionResolutionKind
                        .MoveToDiscard),
                CandidateCardInstanceIds:
                    (long[])combat.Hand.Clone(),
                Continuation:
                    Array.Empty<PrototypeQueuedOperation>(),
                CompletionEvents:
                    Array.Empty<PrototypeCombatEvent>())
        };

        return state with
        {
            World = world with { Combat = combat }
        };
    }

    private static RunState ResolveAutomaticStep(
        RunState state,
        PrototypeAutomaticStep automaticStep)
    {
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException("Combat phase has no combat state.");
        var player = state.Player;

        switch (automaticStep.Kind)
        {
            case PrototypeAutomaticStepKind.DispatchCombatEvent:
                if (automaticStep.EventKind is null)
                {
                    throw new InvalidOperationException("Combat-event pipeline step is missing an event kind.");
                }

                if (automaticStep.EventKind
                    == PrototypeCombatEventKind.PlayerTurnStarted)
                {
                    combat = combat with { IsPlayerTurn = true };
                }

                var dispatched = DispatchCombatEvent(
                    player,
                    combat,
                    new PrototypeCombatEvent(automaticStep.EventKind.Value),
                    state.Rng,
                    allowSuspension: true);
                player = dispatched.Player;
                combat = dispatched.Combat;

                break;

            case PrototypeAutomaticStepKind.DiscardPlayerHand:
            {
                combat = ClearTemporaryCardCosts(
                    combat,
                    PrototypeTemporaryCardCostExpiry.EndOfTurn);

                var ethereal = combat.Hand
                    .Where(instanceId =>
                        CardHasKeyword(
                            combat,
                            instanceId,
                            PrototypeCardKeyword.Ethereal))
                    .ToArray();
                var etherealSet = ethereal.ToHashSet();
                var retained = combat.Hand
                    .Where(instanceId =>
                        !etherealSet.Contains(instanceId)
                        && CardHasKeyword(
                            combat,
                            instanceId,
                            PrototypeCardKeyword.Retain))
                    .ToArray();
                var retainedSet = retained.ToHashSet();
                var discarded = combat.Hand
                    .Where(instanceId =>
                        !etherealSet.Contains(instanceId)
                        && !retainedSet.Contains(instanceId))
                    .ToArray();

                combat = combat with
                {
                    ExhaustPile = combat.ExhaustPile.Concat(ethereal).ToArray(),
                    DiscardPile = combat.DiscardPile.Concat(discarded).ToArray(),
                    Hand = retained
                };

                foreach (var cardInstanceId in ethereal)
                {
                    var exhaustedCard = RequireCombatCard(combat, cardInstanceId);
                    var exhausted = DispatchCombatEvent(
                        player,
                        combat,
                        new PrototypeCombatEvent(
                            PrototypeCombatEventKind.CardExhausted,
                            SourceCardInstanceId: cardInstanceId,
                            CardId: exhaustedCard.CardId),
                        state.Rng);
                    player = exhausted.Player;
                    combat = exhausted.Combat;
                }

                combat = ClearTemporaryCardKeywordOverrides(
                    combat,
                    PrototypeCardKeywordOverrideExpiry.EndOfTurn);

                break;
            }

            case PrototypeAutomaticStepKind.DispatchEnemyStatusStage:
                if (automaticStep.Stage is null)
                {
                    throw new InvalidOperationException("Enemy-status pipeline step is missing a stage.");
                }

                combat = ResolveEnemyStatusStage(combat, automaticStep.Stage.Value);
                break;

            case PrototypeAutomaticStepKind.ResetEnemyBlock:
                combat = combat with
                {
                    Enemies = combat.Enemies
                        .Select(enemy => enemy with { Block = 0 })
                        .ToArray()
                };
                break;

            case PrototypeAutomaticStepKind.ResolveEnemyActions:
            {
                var result = ResolveEnemyActions(
                    player,
                    combat,
                    world.Act,
                    state.Ascension,
                    state.Rng);
                player = result.Player;
                combat = result.Combat;
                break;
            }

            case PrototypeAutomaticStepKind.AdvanceTurn:
                combat = combat with
                {
                    Turn = combat.Turn + 1,
                    Counters = combat.CounterState with
                    {
                        AttacksPlayedThisTurn = 0,
                        SkillsPlayedThisTurn = 0,
                        CardsDiscardedThisTurn = 0,
                        PlayedCardTagsThisTurn =
                            Array.Empty<string>()
                    }
                };
                break;

            case PrototypeAutomaticStepKind.ResetPlayerBlock:
            {
                var preventsBlockClear = combat.PlayerPowers.Any(power =>
                    power.Stacks > 0
                    && PrototypeContent.Power(power.PowerId)
                        .PreventsPlayerBlockClear);
                if (!preventsBlockClear)
                {
                    combat = combat with { PlayerBlock = 0 };
                }

                var delayedBlock = combat.PlayerPowers.Sum(power =>
                    PrototypeContent.Power(power.PowerId)
                        .BlockAfterClearPerStack * power.Stacks);
                if (delayedBlock > 0)
                {
                    combat = combat with
                    {
                        PlayerBlock = combat.PlayerBlock + delayedBlock
                    };
                }

                combat = RemovePlayerPowers(
                    combat,
                    definition => definition.RemoveAfterBlockClear);
                break;
            }

            case PrototypeAutomaticStepKind.RefreshPlayerEnergy:
            {
                combat = combat with { Energy = EnergyPerTurn(player) };
                var delayedEnergy = combat.PlayerPowers.Sum(power =>
                    PrototypeContent.Power(power.PowerId)
                        .EnergyAfterResetPerStack * power.Stacks);
                if (delayedEnergy > 0)
                {
                    combat = combat with
                    {
                        Energy = combat.Energy + delayedEnergy
                    };
                }

                combat = RemovePlayerPowers(
                    combat,
                    definition => definition.RemoveAfterEnergyReset);
                break;
            }

            case PrototypeAutomaticStepKind.DrawPlayerHand:
            {
                var beforeHandDraw = DispatchCombatEvent(
                    player,
                    combat,
                    new PrototypeCombatEvent(
                        PrototypeCombatEventKind.BeforeHandDraw),
                    state.Rng);
                player = beforeHandDraw.Player;
                combat = beforeHandDraw.Combat;

                var handDrawBonus = combat.PlayerPowers.Sum(power =>
                    PrototypeContent.Power(power.PowerId)
                        .HandDrawBonusPerStack * power.Stacks);
                var drawn = DrawCards(
                    player,
                    combat,
                    PrototypeContent.Rules.HandSize
                        + Math.Max(0, handDrawBonus),
                    state.Rng,
                    fromHandDraw: true);
                player = drawn.Player;
                combat = RemovePlayerPowers(
                    drawn.Combat,
                    definition => definition.RemoveAfterHandDraw);
                break;
            }

            default:
                throw new ArgumentOutOfRangeException();
        }

        return state with
        {
            Player = player,
            World = world with { Combat = combat }
        };
    }

    private static CombatState ResolveEnemyStatusStage(
        CombatState combat,
        PrototypeTurnStage stage)
    {
        var enemies = combat.Enemies.Select(enemy => enemy.Fork()).ToArray();

        for (var enemyIndex = 0; enemyIndex < enemies.Length; enemyIndex++)
        {
            var enemy = enemies[enemyIndex];
            if (enemy.Hp <= 0)
            {
                continue;
            }

            var statuses = new Dictionary<string, int>(enemy.Statuses, StringComparer.Ordinal);
            foreach (var pair in enemy.Statuses.ToArray())
            {
                var definition = PrototypeContent.Status(pair.Key);
                var stacks = pair.Value;

                if (definition.TriggerStage == stage)
                {
                    if (definition.TriggerKind == PrototypeStatusTriggerKind.DamageSelfByStacks)
                    {
                        enemy = enemy with { Hp = Math.Max(0, enemy.Hp - stacks) };
                    }

                    stacks -= definition.DecayOnTrigger;
                }

                if (definition.DecayStage == stage)
                {
                    stacks -= definition.DecayAtStage;
                }

                if (stacks > 0)
                {
                    statuses[pair.Key] = stacks;
                }
                else
                {
                    statuses.Remove(pair.Key);
                }
            }

            enemies[enemyIndex] = enemy with { Statuses = statuses };
        }

        combat = combat with { Enemies = enemies };
        return CleanupSourceBoundPowersForDefeatedEnemies(
            combat);
    }

    private static (
        PrototypeEnemyMoveDefinition Move,
        string? NextAiStateId)
        SelectEnemyMove(
            PrototypeEnemyDefinition definition,
            EnemyCombatState enemy,
            IReadOnlyList<EnemyCombatState> formation,
            RngBundle rng)
    {
        if (definition.Moves.Length == 0)
        {
            throw new InvalidOperationException(
                $"Enemy '{definition.Id}' has no moves.");
        }

        switch (definition.MovePolicy)
        {
            case PrototypeEnemyMovePolicy.SequentialLoop:
            {
                if (definition.MoveLoopStartIndex < 0
                    || definition.MoveLoopStartIndex
                        >= definition.Moves.Length)
                {
                    throw new InvalidOperationException(
                        $"Enemy '{definition.Id}' has invalid move-loop start index {definition.MoveLoopStartIndex}.");
                }

                var moveIndex = enemy.MoveIndex;
                if (moveIndex >= definition.Moves.Length)
                {
                    var loopLength =
                        definition.Moves.Length
                        - definition.MoveLoopStartIndex;
                    moveIndex =
                        definition.MoveLoopStartIndex
                        + ((moveIndex
                            - definition.MoveLoopStartIndex)
                           % loopLength);
                }

                return (
                    definition.Moves[moveIndex],
                    enemy.AiStateId);
            }

            case PrototypeEnemyMovePolicy.UniformRandomAfterOpener:
            {
                var openingMoves =
                    definition.OpeningMoveIndices
                    ?? [definition.OpeningMoveIndex];

                if (openingMoves.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Enemy '{definition.Id}' has an empty opening move sequence.");
                }

                foreach (var openingMoveIndex in openingMoves)
                {
                    if (openingMoveIndex < 0
                        || openingMoveIndex
                            >= definition.Moves.Length)
                    {
                        throw new InvalidOperationException(
                            $"Enemy '{definition.Id}' has invalid opening move index {openingMoveIndex}.");
                    }
                }

                if (enemy.MoveIndex < openingMoves.Length)
                {
                    return (
                        definition.Moves[
                            openingMoves[enemy.MoveIndex]],
                        enemy.AiStateId);
                }

                if (definition.RandomMovePoolStartIndex < 0
                    || definition.RandomMovePoolStartIndex
                        >= definition.Moves.Length)
                {
                    throw new InvalidOperationException(
                        $"Enemy '{definition.Id}' has invalid random-pool start index {definition.RandomMovePoolStartIndex}.");
                }

                var candidates = definition.Moves
                    .Skip(definition.RandomMovePoolStartIndex)
                    .Where(move =>
                        move.MaxConsecutiveUses <= 0
                        || !StringComparer.Ordinal.Equals(
                            enemy.LastMoveId,
                            move.Id)
                        || enemy.ConsecutiveMoveUses
                            < move.MaxConsecutiveUses)
                    .ToArray();
                if (candidates.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Enemy '{definition.Id}' has no legal random moves.");
                }

                return (
                    candidates[
                        PrototypeRng.NextInt(
                            rng,
                            "combat",
                            candidates.Length)],
                    enemy.AiStateId);
            }

            case PrototypeEnemyMovePolicy.StateMachine:
                return SelectEnemyStateMachineMove(
                    definition,
                    enemy,
                    formation,
                    rng);

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(definition.MovePolicy));
        }
    }

    private static (
        PrototypeEnemyMoveDefinition Move,
        string? NextAiStateId)
        SelectEnemyStateMachineMove(
            PrototypeEnemyDefinition definition,
            EnemyCombatState enemy,
            IReadOnlyList<EnemyCombatState> formation,
            RngBundle rng)
    {
        var ai = definition.Ai
            ?? throw new InvalidOperationException(
                $"Enemy '{definition.Id}' uses StateMachine policy without an AI definition.");
        if (ai.States.Length == 0)
        {
            throw new InvalidOperationException(
                $"Enemy '{definition.Id}' has an empty AI state machine.");
        }

        var states = ai.States.ToDictionary(
            state => state.Id,
            StringComparer.Ordinal);
        var stateId = enemy.AiStateId
            ?? ai.InitialStateId;

        for (var depth = 0; depth < 32; depth++)
        {
            if (!states.TryGetValue(
                    stateId,
                    out var state))
            {
                throw new InvalidOperationException(
                    $"Enemy '{definition.Id}' AI references missing state '{stateId}'.");
            }

            switch (state.Kind)
            {
                case PrototypeEnemyAiStateKind.Move:
                {
                    if (state.MoveIndex is null
                        || state.MoveIndex.Value < 0
                        || state.MoveIndex.Value
                            >= definition.Moves.Length)
                    {
                        throw new InvalidOperationException(
                            $"Enemy '{definition.Id}' AI move state '{state.Id}' has invalid move index.");
                    }

                    return (
                        definition.Moves[
                            state.MoveIndex.Value],
                        state.NextStateId);
                }

                case PrototypeEnemyAiStateKind.Random:
                {
                    var branches =
                        state.Branches
                        ?? Array.Empty<
                            PrototypeEnemyAiBranch>();
                    var legal = branches
                        .Where(branch =>
                            branch.Weight > 0
                            && IsEnemyAiBranchLegal(
                                definition,
                                enemy,
                                states,
                                branch))
                        .ToArray();
                    if (legal.Length == 0)
                    {
                        throw new InvalidOperationException(
                            $"Enemy '{definition.Id}' AI random state '{state.Id}' has no legal branches.");
                    }

                    var totalWeight =
                        legal.Sum(branch =>
                            branch.Weight);
                    var roll = PrototypeRng.NextInt(
                        rng,
                        "combat",
                        totalWeight);
                    var selected = legal[^1];
                    foreach (var branch in legal)
                    {
                        if (roll < branch.Weight)
                        {
                            selected = branch;
                            break;
                        }

                        roll -= branch.Weight;
                    }

                    stateId =
                        selected.TargetStateId;
                    break;
                }

                case PrototypeEnemyAiStateKind.Conditional:
                {
                    var branches =
                        state.ConditionalBranches
                        ?? Array.Empty<
                            PrototypeEnemyAiConditionalBranch>();
                    var selected = branches.FirstOrDefault(
                        branch => EnemyAiConditionMatches(
                            enemy,
                            formation,
                            branch));
                    if (selected is null)
                    {
                        throw new InvalidOperationException(
                            $"Enemy '{definition.Id}' AI conditional state '{state.Id}' has no matching branch.");
                    }

                    if (!states.TryGetValue(
                            selected.TargetStateId,
                            out var target)
                        || target.Kind
                            != PrototypeEnemyAiStateKind.Move)
                    {
                        throw new InvalidOperationException(
                            $"Enemy '{definition.Id}' AI conditional branch targets invalid move state '{selected.TargetStateId}'.");
                    }

                    stateId = selected.TargetStateId;
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(state.Kind));
            }
        }

        throw new InvalidOperationException(
            $"Enemy '{definition.Id}' AI exceeded the state-resolution depth limit.");
    }

    private static bool EnemyAiConditionMatches(
        EnemyCombatState enemy,
        IReadOnlyList<EnemyCombatState> formation,
        PrototypeEnemyAiConditionalBranch branch)
    {
        var living = formation
            .Where(item => item.Hp > 0)
            .OrderBy(item => item.FormationPosition)
            .ThenBy(item => item.InstanceId)
            .ToArray();
        var isAlone = living.Length == 1
            && living[0].InstanceId == enemy.InstanceId;
        var isFront = living.Length > 0
            && living[0].InstanceId == enemy.InstanceId;

        return branch.Condition switch
        {
            PrototypeEnemyAiConditionKind.IsAlone =>
                isAlone,
            PrototypeEnemyAiConditionKind.IsFront =>
                isFront,
            PrototypeEnemyAiConditionKind.IsNotFront =>
                !isFront,
            PrototypeEnemyAiConditionKind.SlotNameEquals =>
                branch.Value is not null
                && StringComparer.Ordinal.Equals(
                    enemy.SlotName,
                    branch.Value),
            _ => throw new ArgumentOutOfRangeException(
                nameof(branch.Condition))
        };
    }

    private static bool IsEnemyAiBranchLegal(
        PrototypeEnemyDefinition definition,
        EnemyCombatState enemy,
        IReadOnlyDictionary<
            string,
            PrototypeEnemyAiStateDefinition> states,
        PrototypeEnemyAiBranch branch)
    {
        if (!states.TryGetValue(
                branch.TargetStateId,
                out var target)
            || target.Kind
                != PrototypeEnemyAiStateKind.Move
            || target.MoveIndex is null
            || target.MoveIndex.Value < 0
            || target.MoveIndex.Value
                >= definition.Moves.Length)
        {
            throw new InvalidOperationException(
                $"Enemy '{definition.Id}' AI branch targets invalid move state '{branch.TargetStateId}'.");
        }

        var move = definition.Moves[
            target.MoveIndex.Value];

        return branch.RepeatRule switch
        {
            PrototypeEnemyAiRepeatRule
                .CanRepeatForever => true,
            PrototypeEnemyAiRepeatRule
                .CannotRepeat =>
                !StringComparer.Ordinal.Equals(
                    enemy.LastMoveId,
                    move.Id),
            PrototypeEnemyAiRepeatRule
                .CanRepeatXTimes =>
                branch.MaxTimes > 0
                && (!StringComparer.Ordinal.Equals(
                        enemy.LastMoveId,
                        move.Id)
                    || enemy.ConsecutiveMoveUses
                        < branch.MaxTimes),
            PrototypeEnemyAiRepeatRule
                .UseOnlyOnce =>
                (enemy.MoveUseCounts?.GetValueOrDefault(
                    move.Id) ?? 0) == 0,
            _ => throw new ArgumentOutOfRangeException(
                nameof(branch.RepeatRule))
        };
    }

    private static (PlayerState Player, CombatState Combat) ResolveEnemyActions(
        PlayerState player,
        CombatState combat,
        int act,
        int ascension,
        RngBundle rng)
    {
        var enemies = combat.Enemies.Select(enemy => enemy.Fork()).ToArray();
        var block = combat.PlayerBlock;
        var hp = player.Hp;

        for (var index = 0; index < enemies.Length; index++)
        {
            var enemy = enemies[index];
            if (enemy.Hp <= 0)
            {
                continue;
            }

            var definition = PrototypeContent.Enemy(enemy.EnemyId);
            if (definition.Moves.Length == 0)
            {
                continue;
            }

            var selection = SelectEnemyMove(
                definition,
                enemy,
                enemies,
                rng);
            var move = selection.Move;

            foreach (var effect in move.Effects)
            {
                var amount = effect.AmountAt(
                    act,
                    ascension);
                var repetitions =
                    effect.RepetitionsAt(ascension);
                for (var repetition = 0; repetition < repetitions; repetition++)
                {
                    switch (effect.Kind)
                    {
                        case PrototypeEnemyEffectKind.DamagePlayer:
                        {
                            var retaliation = PlayerAttackRetaliation(combat);
                            if (retaliation > 0 && enemy.Hp > 0)
                            {
                                var retaliationBlocked = Math.Min(
                                    enemy.Block,
                                    retaliation);
                                enemy = enemy with
                                {
                                    Block = enemy.Block - retaliationBlocked,
                                    Hp = Math.Max(
                                        0,
                                        enemy.Hp - Math.Max(
                                            0,
                                            retaliation - retaliationBlocked))
                                };
                            }

                            var damage = amount
                                + enemy.PowerStates.Sum(power =>
                                    PrototypeContent.Power(
                                        power.PowerId)
                                        .EnemyAttackDamageBonusPerStack
                                    * power.Stacks);
                            foreach (var status in enemy.Statuses)
                            {
                                var statusDefinition = PrototypeContent.Status(status.Key);
                                damage = (damage * statusDefinition.OutgoingDamageNumerator)
                                    / statusDefinition.OutgoingDamageDenominator;
                            }

                            if (effect.IsAttack)
                            {
                                damage = ModifyIncomingPlayerAttackDamage(
                                    combat,
                                    damage);
                            }

                            var absorbed = Math.Min(block, Math.Max(0, damage));
                            block -= absorbed;
                            hp = Math.Max(0, hp - Math.Max(0, damage - absorbed));
                            break;
                        }

                        case PrototypeEnemyEffectKind.GainBlock:
                            enemy = enemy with { Block = enemy.Block + Math.Max(0, amount) };
                            break;

                        case PrototypeEnemyEffectKind.ApplyPlayerPower:
                            if (effect.PowerId is null)
                            {
                                throw new InvalidOperationException(
                                    "Enemy apply-power effect is missing a power ID.");
                            }

                            combat = ApplyPlayerPower(
                                combat,
                                effect.PowerId,
                                amount,
                                sourceEnemyInstanceId: enemy.InstanceId);
                            break;

                        case PrototypeEnemyEffectKind.ApplyEnemyPower:
                            if (effect.PowerId is null)
                            {
                                throw new InvalidOperationException(
                                    "Enemy self-power effect is missing a power ID.");
                            }

                            var selfPower = ApplyEnemyPowerToState(
                                combat,
                                enemy,
                                effect.PowerId,
                                amount);
                            combat = selfPower.Combat;
                            enemy = selfPower.Enemy;
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    if (hp <= 0 || enemy.Hp <= 0)
                    {
                        break;
                    }
                }

                if (hp <= 0 || enemy.Hp <= 0)
                {
                    break;
                }
            }

            var consecutiveUses =
                StringComparer.Ordinal.Equals(
                    enemy.LastMoveId,
                    move.Id)
                    ? enemy.ConsecutiveMoveUses + 1
                    : 1;
            var moveUseCounts =
                enemy.MoveUseCounts is null
                    ? new Dictionary<string, int>(
                        StringComparer.Ordinal)
                    : new Dictionary<string, int>(
                        enemy.MoveUseCounts,
                        StringComparer.Ordinal);
            moveUseCounts[move.Id] =
                moveUseCounts.GetValueOrDefault(
                    move.Id) + 1;

            enemies[index] = enemy with
            {
                MoveIndex = enemy.MoveIndex + 1,
                LastMoveId = move.Id,
                ConsecutiveMoveUses = consecutiveUses,
                AiStateId = selection.NextAiStateId,
                MoveUseCounts = moveUseCounts
            };
            if (hp <= 0)
            {
                break;
            }
        }

        combat = combat with
        {
            PlayerBlock = block,
            Enemies = enemies
        };
        combat = CleanupSourceBoundPowersForDefeatedEnemies(
            combat);

        return (
            player with { Hp = hp },
            combat);
    }

    private static void EnqueueEffectOperations(
        Queue<PrototypeQueuedOperation> operations,
        PrototypeCombatEffectSpec effect,
        int upgradeLevel,
        int energySpent,
        int? actionTargetEnemyId,
        CombatState combat,
        int powerStacks = 0,
        PrototypeEffectSourceKind sourceKind = PrototypeEffectSourceKind.System,
        bool isPoweredAttack = false,
        PrototypeCombatCardSnapshot? powerCardPayload = null)
    {
        var count = effect.CountKind is null
            ? 0
            : ResolveCombatCount(effect.CountKind.Value, combat);

        var repetitions = effect.RepetitionsAt(upgradeLevel, energySpent)
            + (effect.RepetitionsPerPowerStack * powerStacks)
            + (effect.RepetitionsPerCount * count);
        if (repetitions <= 0)
        {
            return;
        }

        var targets = effect.Target switch
        {
            PrototypeEffectTarget.ActionTargetEnemy => new int?[] { actionTargetEnemyId },
            PrototypeEffectTarget.AllEnemies => combat.Enemies
                .Where(enemy => enemy.Hp > 0)
                .Select(enemy => (int?)enemy.InstanceId)
                .ToArray(),
            PrototypeEffectTarget.RandomEnemy => new int?[] { null },
            _ => throw new ArgumentOutOfRangeException()
        };

        if (targets.Length == 0)
        {
            if (effect.Target == PrototypeEffectTarget.AllEnemies)
            {
                return;
            }

            targets = new int?[] { null };
        }

        for (var repetition = 0; repetition < repetitions; repetition++)
        {
            foreach (var targetEnemyId in targets)
            {
                operations.Enqueue(new PrototypeQueuedOperation(
                    effect.Kind,
                    effect.AmountAt(upgradeLevel, energySpent)
                        + (effect.AmountPerPowerStack * powerStacks)
                        + ((effect.AmountPerCount
                            + (effect.AmountPerCountUpgradeDelta * upgradeLevel))
                            * count),
                    targetEnemyId,
                    effect.StatusId,
                    effect.Selection is null
                        ? null
                        : effect.Selection with
                        {
                            MinSelections =
                                effect.Selection.MinSelections
                                + (effect.Selection.SelectionsPerPowerStack
                                    * powerStacks),
                            MaxSelections =
                                effect.Selection.MaxSelections
                                + (effect.Selection.SelectionsPerPowerStack
                                    * powerStacks)
                        },
                    effect.CardId,
                    effect.PowerId,
                    effect.GeneratedCardUpgradeLevel
                        + (effect.GeneratedCardUpgradePerSourceUpgrade * upgradeLevel),
                    TargetMode: effect.Target,
                    SourceKind: sourceKind,
                    IsPoweredAttack: isPoweredAttack && effect.Kind == PrototypeCombatEffectKind.DamageEnemy,
                    Condition: effect.Condition,
                    SelectedCardPower: effect.SelectedCardPower is null
                        ? null
                        : new PrototypeSelectedCardPowerAction(
                            effect.SelectedCardPower.PowerId,
                            effect.SelectedCardPower.AmountAt(upgradeLevel),
                            effect.SelectedCardPower
                                .ClearAfflictionFromPayload,
                            effect.SelectedCardPower
                                .RestoreSuppressedUpgradesInPayload),
                    SelectedCardKeyword: effect.SelectedCardKeyword,
                    DrawnCardKeyword: effect.DrawnCardKeyword,
                    EventSourceCardKeyword: effect.EventSourceCardKeyword,
                    PowerCardPayload: powerCardPayload?.Fork()));
            }
        }
    }

    private static (PlayerState Player, CombatState Combat) ResolveOperations(
        PlayerState player,
        CombatState combat,
        Queue<PrototypeQueuedOperation> operations,
        RngBundle rng,
        long? sourceCardInstanceId = null,
        PrototypeCardZone sourceCardDestination = PrototypeCardZone.DiscardPile,
        PrototypeCombatEvent[]? completionEvents = null,
        int eventDepth = 0,
        PrototypeCardPlaySeriesState? cardPlaySeries = null,
        bool moveSourceCardOnCompletion = true,
        bool removeSourceCardOnCompletion = false,
        bool sourceCardAlreadyMoved = false,
        long? eventSourceCardInstanceId = null,
        PrototypeEventDispatchContinuationState? eventDispatchContinuation = null)
    {
        while (operations.Count > 0)
        {
            var operation = operations.Dequeue();
            var targetEnemyId = operation.TargetEnemyId;

            if (operation.TargetMode == PrototypeEffectTarget.RandomEnemy)
            {
                var liveEnemies = combat.Enemies
                    .Where(enemy => enemy.Hp > 0)
                    .Select(enemy => enemy.InstanceId)
                    .ToArray();

                if (liveEnemies.Length == 0)
                {
                    continue;
                }

                targetEnemyId = liveEnemies[
                    PrototypeRng.NextInt(
                        rng,
                        "combat_targets",
                        liveEnemies.Length)];
            }

            if (!EvaluateCombatPredicate(
                    operation.Condition,
                    combat,
                    targetEnemyId))
            {
                continue;
            }

            switch (operation.Kind)
            {
                case PrototypeCombatEffectKind.DamageEnemy:
                    if (targetEnemyId is null)
                    {
                        throw new InvalidOperationException("Damage operation requires an enemy target.");
                    }

                    if (operation.IsPoweredAttack && player.Hp <= 0)
                    {
                        break;
                    }

                    if (operation.IsPoweredAttack)
                    {
                        var enemyBeforeHit = combat.Enemies.SingleOrDefault(
                            enemy => enemy.InstanceId == targetEnemyId.Value)
                            ?? throw new InvalidOperationException(
                                $"Enemy {targetEnemyId.Value} is missing.");

                        if (enemyBeforeHit.Hp > 0)
                        {
                            var retaliation = EnemyAttackRetaliation(
                                combat,
                                targetEnemyId.Value);
                            if (retaliation > 0)
                            {
                                var absorbed = Math.Min(
                                    combat.PlayerBlock,
                                    retaliation);
                                combat = combat with
                                {
                                    PlayerBlock = combat.PlayerBlock - absorbed
                                };
                                player = player with
                                {
                                    Hp = Math.Max(
                                        0,
                                        player.Hp - Math.Max(
                                            0,
                                            retaliation - absorbed))
                                };
                            }
                        }
                    }

                    var damageAmount = operation.Amount;
                    if (operation.IsPoweredAttack)
                    {
                        damageAmount = ModifyPlayerAttackDamage(
                            combat,
                            sourceCardInstanceId,
                            targetEnemyId.Value,
                            damageAmount);
                        damageAmount = ModifyIncomingAttackDamage(
                            combat,
                            targetEnemyId.Value,
                            damageAmount);
                    }

                    var damageResult = DamageEnemy(
                        combat,
                        targetEnemyId.Value,
                        damageAmount);
                    combat = damageResult.Combat;
                    if (damageResult.Defeated)
                    {
                        combat =
                            CleanupSourceBoundPowersForDefeatedEnemies(
                                combat);
                    }

                    if (damageResult.DamageDealt > 0)
                    {
                        var damaged = DispatchCombatEvent(
                            player,
                            combat,
                            new PrototypeCombatEvent(
                                PrototypeCombatEventKind.EnemyDamaged,
                                TargetEnemyId: targetEnemyId.Value,
                                Amount: damageResult.DamageDealt),
                            rng,
                            eventDepth + 1);
                        player = damaged.Player;
                        combat = damaged.Combat;
                    }

                    if (damageResult.Defeated)
                    {
                        var defeated = DispatchCombatEvent(
                            player,
                            combat,
                            new PrototypeCombatEvent(
                                PrototypeCombatEventKind.EnemyDefeated,
                                TargetEnemyId: targetEnemyId.Value,
                                Amount: damageResult.DamageDealt),
                            rng,
                            eventDepth + 1);
                        player = defeated.Player;
                        combat = defeated.Combat;
                    }

                    break;

                case PrototypeCombatEffectKind.GainPlayerBlock:
                {
                    var modifiedAmount =
                        operation.SourceKind == PrototypeEffectSourceKind.Card
                            ? ModifyPlayerCardBlock(
                                combat,
                                operation.Amount)
                            : operation.Amount;
                    combat = combat with
                    {
                        PlayerBlock = combat.PlayerBlock
                            + Math.Max(0, modifiedAmount)
                    };
                    break;
                }

                case PrototypeCombatEffectKind.GainPlayerBlockFromEnemyStatusTotal:
                {
                    if (operation.StatusId is null)
                    {
                        throw new InvalidOperationException(
                            "Status-total block operation is missing a status ID.");
                    }

                    var statusTotal = combat.Enemies
                        .Where(enemy => enemy.Hp > 0)
                        .Sum(enemy =>
                            Math.Max(
                                0,
                                enemy.Statuses.GetValueOrDefault(
                                    operation.StatusId)));
                    var baseAmount =
                        operation.Amount + statusTotal;
                    var modifiedAmount =
                        operation.SourceKind == PrototypeEffectSourceKind.Card
                            ? ModifyPlayerCardBlock(
                                combat,
                                baseAmount)
                            : baseAmount;
                    combat = combat with
                    {
                        PlayerBlock = combat.PlayerBlock
                            + Math.Max(0, modifiedAmount)
                    };
                    break;
                }

                case PrototypeCombatEffectKind.GainPlayerBlockAndApplyPowerFromActualGain:
                {
                    if (operation.PowerId is null)
                    {
                        throw new InvalidOperationException(
                            "Block-and-delay operation is missing a power ID.");
                    }

                    var modifiedAmount =
                        operation.SourceKind == PrototypeEffectSourceKind.Card
                            ? ModifyPlayerCardBlock(
                                combat,
                                operation.Amount)
                            : operation.Amount;
                    var actualGain = Math.Max(0, modifiedAmount);
                    combat = combat with
                    {
                        PlayerBlock = combat.PlayerBlock + actualGain
                    };
                    if (actualGain > 0)
                    {
                        combat = ApplyPlayerPower(
                            combat,
                            operation.PowerId,
                            actualGain);
                    }

                    break;
                }

                case PrototypeCombatEffectKind.DrawCards:
                {
                    var drawn = DrawCards(
                        player,
                        combat,
                        operation.Amount,
                        rng,
                        fromHandDraw: false,
                        eventDepth: eventDepth + 1);
                    player = drawn.Player;
                    combat = drawn.Combat;
                    if (operation.DrawnCardKeyword is not null)
                    {
                        foreach (var cardInstanceId in drawn.DrawnCardInstanceIds)
                        {
                            combat = ApplyCardKeywordOverride(
                                combat,
                                cardInstanceId,
                                operation.DrawnCardKeyword);
                        }
                    }

                    break;
                }

                case PrototypeCombatEffectKind.ApplyEnemyStatus:
                    if (targetEnemyId is null || operation.StatusId is null)
                    {
                        throw new InvalidOperationException("Status operation requires target and status ID.");
                    }

                    combat = ApplyEnemyStatus(
                        combat,
                        targetEnemyId.Value,
                        operation.StatusId,
                        operation.Amount);
                    break;

                case PrototypeCombatEffectKind.ChooseCards:
                {
                    var selection = operation.Selection
                        ?? throw new InvalidOperationException("Choose-cards operation has no selection specification.");
                    var candidates = GetZone(combat, selection.SourceZone);
                    if (selection.RequiredCardType is not null)
                    {
                        candidates = candidates
                            .Where(instanceId =>
                                PrototypeContent.Card(
                                    RequireCombatCard(combat, instanceId).CardId).Type
                                == selection.RequiredCardType.Value)
                            .ToArray();
                    }

                    if (candidates.Length == 0 || selection.MaxSelections <= 0)
                    {
                        break;
                    }

                    var effective = selection with
                    {
                        MinSelections = Math.Min(selection.MinSelections, candidates.Length),
                        MaxSelections = Math.Min(selection.MaxSelections, candidates.Length)
                    };

                    combat = combat with
                    {
                        PendingChoice = new PendingCombatChoiceState(
                            ChoiceId: "select_cards",
                            SourceCardInstanceId: sourceCardInstanceId,
                            SourceCardDestination: sourceCardDestination,
                            Selection: effective,
                            CandidateCardInstanceIds: (long[])candidates.Clone(),
                            Continuation: operations.ToArray(),
                            CompletionEvents: completionEvents is null
                                ? Array.Empty<PrototypeCombatEvent>()
                                : (PrototypeCombatEvent[])completionEvents.Clone(),
                            CardPlaySeries: cardPlaySeries,
                            MoveSourceCardOnCompletion:
                                moveSourceCardOnCompletion,
                            RemoveSourceCardOnCompletion:
                                removeSourceCardOnCompletion,
                            SourceCardAlreadyMoved:
                                sourceCardAlreadyMoved,
                            SelectedCardPower:
                                operation.SelectedCardPower,
                            SelectedCardKeyword:
                                operation.SelectedCardKeyword,
                            EventDispatchContinuation:
                                eventDispatchContinuation?.Fork())
                    };
                    return (player, combat);
                }

                case PrototypeCombatEffectKind.ApplyPlayerPower:
                    if (operation.PowerId is null)
                    {
                        throw new InvalidOperationException("Apply-power operation is missing a power ID.");
                    }

                    combat = ApplyPlayerPower(combat, operation.PowerId, operation.Amount);
                    break;

                case PrototypeCombatEffectKind.ApplyEnemyPower:
                    if (operation.PowerId is null || targetEnemyId is null)
                    {
                        throw new InvalidOperationException(
                            "Apply-enemy-power operation requires a power ID and enemy target.");
                    }

                    combat = ApplyEnemyPower(
                        combat,
                        targetEnemyId.Value,
                        operation.PowerId,
                        operation.Amount);
                    break;

                case PrototypeCombatEffectKind.DamagePlayer:
                {
                    var absorbed = Math.Min(
                        combat.PlayerBlock,
                        Math.Max(0, operation.Amount));
                    combat = combat with
                    {
                        PlayerBlock = combat.PlayerBlock - absorbed
                    };
                    player = player with
                    {
                        Hp = Math.Max(
                            0,
                            player.Hp - Math.Max(0, operation.Amount - absorbed))
                    };
                    break;
                }

                case PrototypeCombatEffectKind.GainEnergy:
                    combat = combat with
                    {
                        Energy = combat.Energy + Math.Max(0, operation.Amount)
                    };
                    break;

                case PrototypeCombatEffectKind.ModifySourceCardEnergyCost:
                    if (sourceCardInstanceId is null)
                    {
                        throw new InvalidOperationException(
                            "Source-card cost mutation requires a source card.");
                    }

                    combat = ModifyCombatCardEnergyCost(
                        combat,
                        sourceCardInstanceId.Value,
                        operation.Amount);
                    break;

                case PrototypeCombatEffectKind.SetHandCardsEnergyCostUntilTurnEndOrPlayed:
                    combat = SetHandCardsTemporaryEnergyCost(
                        combat,
                        operation.Amount);
                    break;

                case PrototypeCombatEffectKind.ModifyEventSourceCardKeyword:
                    if (eventSourceCardInstanceId is null
                        || operation.EventSourceCardKeyword is null)
                    {
                        throw new InvalidOperationException(
                            "Event-source card keyword mutation requires a source card and keyword specification.");
                    }

                    combat = ApplyCardKeywordOverride(
                        combat,
                        eventSourceCardInstanceId.Value,
                        operation.EventSourceCardKeyword);
                    break;

                case PrototypeCombatEffectKind.MultiplyEnemyStatus:
                    if (targetEnemyId is null || operation.StatusId is null)
                    {
                        throw new InvalidOperationException(
                            "Multiply-status operation requires target and status ID.");
                    }

                    combat = MultiplyEnemyStatus(
                        combat,
                        targetEnemyId.Value,
                        operation.StatusId,
                        operation.Amount);
                    break;

                case PrototypeCombatEffectKind.CreateCardsInHand:
                {
                    if (operation.CardId is null)
                    {
                        throw new InvalidOperationException("Create-card operation is missing a card ID.");
                    }

                    combat = AddGeneratedCardCopies(
                        combat,
                        new PrototypeCombatCardSnapshot(
                            operation.CardId,
                            operation.GeneratedCardUpgradeLevel,
                            PrototypeJson.EmptyObject()),
                        operation.Amount);
                    break;
                }

                case PrototypeCombatEffectKind.CreateCardsInHandFromPowerCardPayload:
                {
                    var snapshot = operation.PowerCardPayload
                        ?? throw new InvalidOperationException(
                            "Power-payload card generation requires a stored card snapshot.");
                    combat = AddGeneratedCardCopies(
                        combat,
                        snapshot,
                        operation.Amount);
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (sourceCardInstanceId is not null
            && moveSourceCardOnCompletion)
        {
            if (!sourceCardAlreadyMoved
                && !removeSourceCardOnCompletion)
            {
                var destination = GetZone(combat, sourceCardDestination);
                combat = SetZone(
                    combat,
                    sourceCardDestination,
                    destination.Append(sourceCardInstanceId.Value).ToArray());
            }

            combat = combat with { PendingChoice = null };
        }

        var completionEventArray =
            completionEvents ?? Array.Empty<PrototypeCombatEvent>();
        for (var completionEventIndex = 0;
             completionEventIndex < completionEventArray.Length;
             completionEventIndex++)
        {
            var dispatched = DispatchCombatEvent(
                player,
                combat,
                completionEventArray[completionEventIndex],
                rng,
                eventDepth + 1,
                allowSuspension: true);
            player = dispatched.Player;
            combat = dispatched.Combat;

            if (combat.PendingChoice is not null)
            {
                var outerContinuation =
                    new PrototypeChoiceResolutionContinuationState(
                        SourceCardInstanceId: sourceCardInstanceId,
                        SourceCardDestination: sourceCardDestination,
                        Operations:
                            Array.Empty<PrototypeQueuedOperation>(),
                        PendingDiscardEvents:
                            Array.Empty<PrototypeCombatEvent>(),
                        PendingSlyCardInstanceIds:
                            Array.Empty<long>(),
                        CompletionEvents:
                            completionEventArray
                                .Skip(completionEventIndex + 1)
                                .ToArray(),
                        CardPlaySeries: cardPlaySeries,
                        MoveSourceCardOnCompletion:
                            moveSourceCardOnCompletion,
                        RemoveSourceCardOnCompletion:
                            removeSourceCardOnCompletion,
                        SourceCardAlreadyMoved:
                            sourceCardAlreadyMoved
                            || (sourceCardInstanceId is not null
                                && moveSourceCardOnCompletion
                                && !removeSourceCardOnCompletion));
                combat = AttachOuterChoiceContinuation(
                    combat,
                    outerContinuation);
                return (player, combat);
            }
        }

        if (sourceCardInstanceId is not null
            && moveSourceCardOnCompletion)
        {
            combat = ClearTemporaryCardCost(
                combat,
                sourceCardInstanceId.Value,
                PrototypeTemporaryCardCostExpiry.WhenPlayed);
            combat = ClearTemporaryCardKeywordOverrides(
                combat,
                sourceCardInstanceId.Value,
                PrototypeCardKeywordOverrideExpiry.WhenPlayed);

            if (removeSourceCardOnCompletion)
            {
                combat = combat with
                {
                    Cards = combat.Cards
                        .Where(card =>
                            card.InstanceId != sourceCardInstanceId.Value)
                        .ToArray()
                };
            }
        }

        return (player, combat);
    }

    private static CombatState AddGeneratedCardCopies(
        CombatState combat,
        PrototypeCombatCardSnapshot snapshot,
        int count)
    {
        const int maxHandSize = 10;

        _ = PrototypeContent.Card(snapshot.CardId);
        for (var index = 0; index < Math.Max(0, count); index++)
        {
            var instance = new CombatCardInstance(
                InstanceId: combat.NextCardInstanceId,
                PersistentCardInstanceId: null,
                CardId: snapshot.CardId,
                UpgradeLevel: snapshot.UpgradeLevel,
                IsTemporary: true,
                State: snapshot.State.Clone(),
                CombatEnergyCostDelta: snapshot.CombatEnergyCostDelta,
                TemporaryEnergyCost: snapshot.TemporaryEnergyCost is null
                    ? null
                    : snapshot.TemporaryEnergyCost with { },
                KeywordOverrides: snapshot.KeywordOverrides is null
                    ? null
                    : snapshot.KeywordOverrides
                        .Select(item => item with { })
                        .ToArray(),
                ReplayCount: snapshot.ReplayCount,
                Enchantment: snapshot.Enchantment,
                EnchantmentTriggeredThisCombat:
                    snapshot.EnchantmentTriggeredThisCombat,
                Affliction: snapshot.Affliction,
                SuppressedUpgradeLevels:
                    snapshot.SuppressedUpgradeLevels);
            instance = ApplyActiveSourceBoundAfflictionToCard(
                combat,
                instance);

            var addToHand = combat.Hand.Length < maxHandSize;
            combat = combat with
            {
                NextCardInstanceId = combat.NextCardInstanceId + 1,
                Cards = combat.Cards.Append(instance).ToArray(),
                Hand = addToHand
                    ? combat.Hand.Append(instance.InstanceId).ToArray()
                    : combat.Hand,
                DiscardPile = addToHand
                    ? combat.DiscardPile
                    : combat.DiscardPile.Append(instance.InstanceId).ToArray()
            };
        }

        return combat;
    }

    private sealed record PrototypeCardPlayCountResult(
        CombatState Combat,
        int PlayCount);

    private static PrototypeCardPlayCountResult
        ResolveCardPlayCountAndConsumeModifiers(
            CombatState combat,
            CombatCardInstance card,
            PrototypeCardType cardType)
    {
        var modifiers = combat.PlayerPowers
            .Where(power =>
            {
                var definition = PrototypeContent.Power(power.PowerId);
                return power.Stacks > 0
                    && definition.ReplayCardType == cardType
                    && definition.AdditionalPlayCount > 0;
            })
            .OrderBy(power => power.ApplicationOrder)
            .ToArray();

        var enchantmentReplay = card.Enchantment switch
        {
            {
                Kind: PrototypeCardEnchantmentKind.Spiral
            } enchantment =>
                Math.Max(0, enchantment.Amount),
            {
                Kind: PrototypeCardEnchantmentKind.Glam
            } enchantment
                when !card.EnchantmentTriggeredThisCombat =>
                Math.Max(0, enchantment.Amount),
            _ => 0
        };

        var playCount = 1
            + Math.Max(0, card.ReplayCount)
            + enchantmentReplay
            + modifiers.Sum(power =>
                PrototypeContent.Power(power.PowerId).AdditionalPlayCount);

        if (card.Enchantment is
                {
                    Kind: PrototypeCardEnchantmentKind.Glam
                }
            && enchantmentReplay > 0)
        {
            combat = combat with
            {
                Cards = combat.Cards
                    .Select(item =>
                        item.InstanceId == card.InstanceId
                            ? item with
                            {
                                EnchantmentTriggeredThisCombat = true
                            }
                            : item)
                    .ToArray()
            };
        }

        if (modifiers.Length == 0)
        {
            return new PrototypeCardPlayCountResult(combat, playCount);
        }

        var consumedOrders = modifiers
            .Where(power =>
                PrototypeContent.Power(power.PowerId)
                    .ConsumeOnMatchingPlayCountModification)
            .Select(power => power.ApplicationOrder)
            .ToHashSet();

        if (consumedOrders.Count > 0)
        {
            var powers = combat.PlayerPowers
                .Select(power =>
                {
                    if (!consumedOrders.Contains(power.ApplicationOrder))
                    {
                        return power;
                    }

                    return power with { Stacks = power.Stacks - 1 };
                })
                .Where(power => power.Stacks > 0)
                .ToArray();
            combat = combat with { PlayerPowers = powers };
        }

        return new PrototypeCardPlayCountResult(combat, playCount);
    }

    private static (PlayerState Player, CombatState Combat)
        ResolveCardPlaySeries(
            PlayerState player,
            CombatState combat,
            RngBundle rng,
            PrototypeCardPlaySeriesState series)
    {
        if (!series.HasRemainingExecutions)
        {
            return (player, combat);
        }

        var card = RequireCombatCard(
            combat,
            series.SourceCardInstanceId);
        var definition = PrototypeContent.Card(card.CardId);
        var powerApplicationOrderCeiling =
            combat.NextPowerApplicationOrder - 1;

        var operations = new Queue<PrototypeQueuedOperation>();
        foreach (var effect in definition.Effects)
        {
            EnqueueEffectOperations(
                operations,
                effect,
                card.UpgradeLevel,
                series.EnergySpent,
                series.TargetEnemyId,
                combat,
                sourceKind: PrototypeEffectSourceKind.Card,
                isPoweredAttack:
                    definition.Type == PrototypeCardType.Attack);
        }

        var nextSeries = series with
        {
            NextPlayIndex = series.NextPlayIndex + 1
        };
        var isLastExecution = !nextSeries.HasRemainingExecutions;

        var resolved = ResolveOperations(
            player,
            combat,
            operations,
            rng,
            series.SourceCardInstanceId,
            series.SourceCardDestination,
            [
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.CardPlayed,
                    SourceCardInstanceId:
                        series.SourceCardInstanceId,
                    CardId: card.CardId,
                    PowerApplicationOrderCeiling:
                        powerApplicationOrderCeiling)
            ],
            cardPlaySeries: nextSeries,
            moveSourceCardOnCompletion: isLastExecution,
            removeSourceCardOnCompletion:
                series.RemoveSourceCardOnCompletion);

        if (resolved.Combat.PendingChoice is not null
            || isLastExecution
            || resolved.Player.Hp <= 0)
        {
            return resolved;
        }

        if (AllEnemiesDefeated(resolved.Combat))
        {
            return FinalizeInterruptedCardPlaySeries(
                resolved.Player,
                resolved.Combat,
                rng,
                nextSeries);
        }

        return ResolveCardPlaySeries(
            resolved.Player,
            resolved.Combat,
            rng,
            nextSeries);
    }

    private static (PlayerState Player, CombatState Combat)
        FinalizeInterruptedCardPlaySeries(
            PlayerState player,
            CombatState combat,
            RngBundle rng,
            PrototypeCardPlaySeriesState series) =>
        ResolveOperations(
            player,
            combat,
            new Queue<PrototypeQueuedOperation>(),
            rng,
            series.SourceCardInstanceId,
            series.SourceCardDestination,
            completionEvents: Array.Empty<PrototypeCombatEvent>(),
            cardPlaySeries: series,
            moveSourceCardOnCompletion: true,
            removeSourceCardOnCompletion:
                series.RemoveSourceCardOnCompletion);

    private static CombatState DecrementPlayerPowers(
        CombatState combat,
        Func<PrototypePowerDefinition, bool> predicate)
    {
        var powers = combat.PlayerPowers
            .Select(power =>
            {
                if (!predicate(PrototypeContent.Power(power.PowerId)))
                {
                    return power;
                }

                return power with { Stacks = power.Stacks - 1 };
            })
            .Where(power => power.Stacks > 0)
            .ToArray();

        return combat with { PlayerPowers = powers };
    }

    private static CombatState RemovePlayerPowers(
        CombatState combat,
        Func<PrototypePowerDefinition, bool> predicate) =>
        combat with
        {
            PlayerPowers = combat.PlayerPowers
                .Where(power =>
                    !predicate(PrototypeContent.Power(power.PowerId)))
                .ToArray()
        };

    private static int PlayerBlockBonus(CombatState combat) =>
        combat.PlayerPowers.Sum(power =>
            PrototypeContent.Power(power.PowerId).BlockBonusPerStack * power.Stacks);

    private static int ModifyPlayerCardBlock(
        CombatState combat,
        int amount)
    {
        var modified = amount + PlayerBlockBonus(combat);
        foreach (var power in combat.PlayerPowers.Where(power => power.Stacks > 0))
        {
            var definition = PrototypeContent.Power(power.PowerId);
            if (definition.PlayerCardBlockDenominator <= 0)
            {
                throw new InvalidOperationException(
                    $"Power '{definition.Id}' has invalid player-card-block denominator.");
            }

            modified = (modified * definition.PlayerCardBlockNumerator)
                / definition.PlayerCardBlockDenominator;
        }

        return modified;
    }

    private static int ModifyPlayerOutgoingAttackDamage(
        CombatState combat,
        int damage)
    {
        var modified = damage;
        foreach (var power in combat.PlayerPowers.Where(power => power.Stacks > 0))
        {
            var definition = PrototypeContent.Power(power.PowerId);
            if (definition.PlayerAttackDamageDenominator <= 0)
            {
                throw new InvalidOperationException(
                    $"Power '{definition.Id}' has invalid player-attack denominator.");
            }

            modified = (modified * definition.PlayerAttackDamageNumerator)
                / definition.PlayerAttackDamageDenominator;
        }

        return modified;
    }

    private static int ModifyIncomingPlayerAttackDamage(
        CombatState combat,
        int damage)
    {
        var modified = damage;
        foreach (var power in combat.PlayerPowers.Where(power => power.Stacks > 0))
        {
            var definition = PrototypeContent.Power(power.PowerId);
            if (definition.PlayerIncomingAttackDamageDenominator <= 0)
            {
                throw new InvalidOperationException(
                    $"Power '{definition.Id}' has invalid incoming-player-attack denominator.");
            }

            modified =
                (modified
                 * definition.PlayerIncomingAttackDamageNumerator)
                / definition.PlayerIncomingAttackDamageDenominator;
        }

        return modified;
    }

    private static bool IsCardFreeByPower(
        CombatState combat,
        PrototypeCardDefinition card) =>
        combat.PlayerPowers.Any(power =>
        {
            var definition = PrototypeContent.Power(power.PowerId);
            return power.Stacks > 0
                && definition.FreeCardType == card.Type;
        });

    private static CombatState ConsumeMatchingFreeCardPower(
        CombatState combat,
        PrototypeCardType cardType)
    {
        var candidate = combat.PlayerPowers
            .Where(power =>
            {
                var definition = PrototypeContent.Power(power.PowerId);
                return power.Stacks > 0
                    && definition.FreeCardType == cardType
                    && definition.ConsumeOnMatchingCardPlay;
            })
            .OrderBy(power => power.ApplicationOrder)
            .FirstOrDefault();

        if (candidate is null)
        {
            return combat;
        }

        var powers = combat.PlayerPowers.ToList();
        var index = powers.FindIndex(power =>
            power.ApplicationOrder == candidate.ApplicationOrder);
        if (index < 0)
        {
            throw new InvalidOperationException(
                "Free-card power disappeared before consumption.");
        }

        if (powers[index].Stacks <= 1)
        {
            powers.RemoveAt(index);
        }
        else
        {
            powers[index] = powers[index] with
            {
                Stacks = powers[index].Stacks - 1
            };
        }

        return combat with { PlayerPowers = powers.ToArray() };
    }

    private static bool IsCardAffordable(
        CombatState combat,
        CombatCardInstance card,
        PrototypeCardDefinition definition) =>
        definition.Cost.Kind switch
        {
            PrototypeCardCostKind.Fixed =>
                ResolveFixedCardEnergyCost(
                    combat,
                    card,
                    definition) <= combat.Energy,
            PrototypeCardCostKind.X => true,
            _ => throw new ArgumentOutOfRangeException()
        };

    private static int ResolveCardEnergySpent(
        CombatState combat,
        CombatCardInstance card,
        PrototypeCardDefinition definition) =>
        definition.Cost.Kind switch
        {
            PrototypeCardCostKind.Fixed =>
                ResolveFixedCardEnergyCost(
                    combat,
                    card,
                    definition),
            PrototypeCardCostKind.X => combat.Energy,
            _ => throw new ArgumentOutOfRangeException()
        };

    private static bool CardExhaustsOnUse(
        PrototypeCardDefinition definition,
        int upgradeLevel) =>
        definition.ExhaustOnUse
        && !(upgradeLevel > 0 && definition.LoseExhaustOnUpgrade);

    private static int ResolveFixedCardEnergyCost(
        CombatState combat,
        CombatCardInstance card,
        PrototypeCardDefinition definition)
    {
        if (card.TemporaryEnergyCost is not null)
        {
            return Math.Max(0, card.TemporaryEnergyCost.Cost);
        }

        var baseCost = definition.Cost.AmountAt(
            card.UpgradeLevel,
            ResolveCardCostReductionCount(
                definition.Cost,
                combat));

        return Math.Max(
            0,
            baseCost + card.CombatEnergyCostDelta);
    }

    private static CombatState SetHandCardsTemporaryEnergyCost(
        CombatState combat,
        int cost)
    {
        var hand = combat.Hand.ToHashSet();
        var cards = combat.Cards
            .Select(card =>
            {
                if (!hand.Contains(card.InstanceId))
                {
                    return card;
                }

                var definition = PrototypeContent.Card(card.CardId);
                if (definition.Cost.Kind == PrototypeCardCostKind.X
                    || definition.Cost.Amount < 0)
                {
                    return card;
                }

                return card with
                {
                    TemporaryEnergyCost =
                        new PrototypeTemporaryCardCost(
                            Math.Max(0, cost),
                            PrototypeTemporaryCardCostExpiry.EndOfTurn
                            | PrototypeTemporaryCardCostExpiry.WhenPlayed)
                };
            })
            .ToArray();

        return combat with { Cards = cards };
    }

    private static CombatState ClearTemporaryCardCost(
        CombatState combat,
        long cardInstanceId,
        PrototypeTemporaryCardCostExpiry expiry)
    {
        var cards = combat.Cards
            .Select(card =>
                card.InstanceId == cardInstanceId
                    && card.TemporaryEnergyCost is not null
                    && card.TemporaryEnergyCost.Expiry.HasFlag(expiry)
                        ? card with { TemporaryEnergyCost = null }
                        : card)
            .ToArray();

        return combat with { Cards = cards };
    }

    private static CombatState ClearTemporaryCardCosts(
        CombatState combat,
        PrototypeTemporaryCardCostExpiry expiry) =>
        combat with
        {
            Cards = combat.Cards
                .Select(card =>
                    card.TemporaryEnergyCost is not null
                    && card.TemporaryEnergyCost.Expiry.HasFlag(expiry)
                        ? card with { TemporaryEnergyCost = null }
                        : card)
                .ToArray()
        };

    private static CombatState ClearTemporaryCardKeywordOverrides(
        CombatState combat,
        long cardInstanceId,
        PrototypeCardKeywordOverrideExpiry expiry)
    {
        var cards = combat.Cards
            .Select(card =>
            {
                if (card.InstanceId != cardInstanceId
                    || card.KeywordOverrides is null)
                {
                    return card;
                }

                var remaining = card.KeywordOverrides
                    .Where(item => !item.Expiry.HasFlag(expiry))
                    .ToArray();
                return card with
                {
                    KeywordOverrides = remaining.Length == 0
                        ? null
                        : remaining
                };
            })
            .ToArray();

        return combat with { Cards = cards };
    }

    private static CombatState ClearTemporaryCardKeywordOverrides(
        CombatState combat,
        PrototypeCardKeywordOverrideExpiry expiry)
    {
        var cards = combat.Cards
            .Select(card =>
            {
                if (card.KeywordOverrides is null)
                {
                    return card;
                }

                var remaining = card.KeywordOverrides
                    .Where(item => !item.Expiry.HasFlag(expiry))
                    .ToArray();
                return card with
                {
                    KeywordOverrides = remaining.Length == 0
                        ? null
                        : remaining
                };
            })
            .ToArray();

        return combat with { Cards = cards };
    }

    private static bool CardHasKeyword(
        CombatState combat,
        long cardInstanceId,
        PrototypeCardKeyword keyword)
    {
        var card = RequireCombatCard(combat, cardInstanceId);
        var baseValue = keyword switch
        {
            PrototypeCardKeyword.Retain =>
                PrototypeContent.Card(card.CardId).Retain,
            PrototypeCardKeyword.Sly =>
                PrototypeContent.Card(card.CardId).Sly,
            PrototypeCardKeyword.Ethereal =>
                PrototypeContent.Card(card.CardId).Ethereal,
            _ => throw new ArgumentOutOfRangeException(nameof(keyword))
        };

        var definition = PrototypeContent.Card(card.CardId);
        var tags = definition.Tags ?? Array.Empty<string>();
        var grantedByAffliction =
            keyword == PrototypeCardKeyword.Ethereal
            && card.Affliction?.Kind
                == PrototypeCardAfflictionKind.Hexed;
        var grantedByPower = combat.PlayerPowers.Any(power =>
        {
            if (power.Stacks <= 0)
            {
                return false;
            }

            var powerDefinition = PrototypeContent.Power(
                power.PowerId);
            return powerDefinition.GrantedCardKeyword == keyword
                && powerDefinition
                    .GrantedCardKeywordRequiredCardTag is
                    { } requiredTag
                && tags.Contains(
                    requiredTag,
                    StringComparer.Ordinal);
        });

        var matching = card.KeywordOverrides?
            .Where(item => item.Keyword == keyword)
            .ToArray();
        return matching is { Length: > 0 }
            ? matching[^1].Enabled
            : baseValue || grantedByAffliction || grantedByPower;
    }

    private static CombatState ApplyCardKeywordOverride(
        CombatState combat,
        long cardInstanceId,
        PrototypeCardKeywordOverrideSpec action)
    {
        var cards = combat.Cards
            .Select(card =>
            {
                if (card.InstanceId != cardInstanceId)
                {
                    return card;
                }

                var overrides = card.KeywordOverrides
                    ?? Array.Empty<PrototypeCardKeywordOverride>();
                return card with
                {
                    KeywordOverrides = overrides.Append(
                        new PrototypeCardKeywordOverride(
                            action.Keyword,
                            action.Enabled,
                            action.Expiry)).ToArray()
                };
            })
            .ToArray();

        if (!cards.Any(card => card.InstanceId == cardInstanceId))
        {
            throw new InvalidOperationException(
                $"Combat card instance {cardInstanceId} is missing.");
        }

        return combat with { Cards = cards };
    }

    private static CombatState ModifyCombatCardEnergyCost(
        CombatState combat,
        long cardInstanceId,
        int delta)
    {
        var cards = combat.Cards
            .Select(card =>
                card.InstanceId == cardInstanceId
                    ? card with
                    {
                        CombatEnergyCostDelta =
                            card.CombatEnergyCostDelta + delta
                    }
                    : card)
            .ToArray();

        if (!cards.Any(card => card.InstanceId == cardInstanceId))
        {
            throw new InvalidOperationException(
                $"Combat card instance {cardInstanceId} is missing.");
        }

        return combat with { Cards = cards };
    }

    private static int ResolveCardCostReductionCount(
        PrototypeCardCostSpec cost,
        CombatState combat) =>
        cost.ReductionCountKind is null
            ? 0
            : ResolveCombatCount(cost.ReductionCountKind.Value, combat);

    private static int ResolveCombatCount(
        PrototypeCombatCountKind kind,
        CombatState combat) =>
        kind switch
        {
            PrototypeCombatCountKind.SkillsInHand =>
                combat.Hand.Count(instanceId =>
                    PrototypeContent.Card(
                        RequireCombatCard(combat, instanceId).CardId).Type
                    == PrototypeCardType.Skill),
            PrototypeCombatCountKind.SkillsPlayedThisTurn =>
                combat.CounterState.SkillsPlayedThisTurn,
            PrototypeCombatCountKind.AttacksPlayedThisTurn =>
                combat.CounterState.AttacksPlayedThisTurn,
            PrototypeCombatCountKind.CardsDiscardedThisTurn =>
                combat.CounterState.CardsDiscardedThisTurn,
            PrototypeCombatCountKind.CardsDrawnThisCombat =>
                combat.CounterState.CardsDrawnThisCombat,
            PrototypeCombatCountKind.OtherCardsInHand =>
                combat.Hand.Length,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    private static CombatState RecordCombatCounterEvent(
        CombatState combat,
        PrototypeCombatEvent combatEvent)
    {
        var counters = combat.CounterState;

        switch (combatEvent.Kind)
        {
            case PrototypeCombatEventKind.CardPlayed:
                if (combatEvent.CardId is null)
                {
                    return combat;
                }

                var cardDefinition =
                    PrototypeContent.Card(combatEvent.CardId);
                var type = cardDefinition.Type;
                counters = type switch
                {
                    PrototypeCardType.Attack => counters with
                    {
                        AttacksPlayedThisTurn =
                            counters.AttacksPlayedThisTurn + 1
                    },
                    PrototypeCardType.Skill => counters with
                    {
                        SkillsPlayedThisTurn =
                            counters.SkillsPlayedThisTurn + 1
                    },
                    _ => counters
                };

                var tags = cardDefinition.Tags
                    ?? Array.Empty<string>();
                if (tags.Length > 0)
                {
                    counters = counters with
                    {
                        PlayedCardTagsThisTurn =
                            counters.PlayedTags
                                .Concat(tags)
                                .ToArray()
                    };
                }

                break;

            case PrototypeCombatEventKind.CardDrawn:
                counters = counters with
                {
                    CardsDrawnThisCombat =
                        counters.CardsDrawnThisCombat + 1
                };
                break;

            case PrototypeCombatEventKind.CardDiscarded:
                counters = counters with
                {
                    CardsDiscardedThisTurn =
                        counters.CardsDiscardedThisTurn + 1
                };
                break;
        }

        return combat with { Counters = counters };
    }

    private static bool EvaluateCombatPredicate(
        PrototypeCombatPredicateSpec? predicate,
        CombatState combat,
        int? targetEnemyId)
    {
        if (predicate is null)
        {
            return true;
        }

        return predicate.Kind switch
        {
            PrototypeCombatPredicateKind.DrawPileEmpty =>
                combat.DrawPile.Length == 0,
            PrototypeCombatPredicateKind.TargetHasStatus =>
                TargetHasStatus(
                    combat,
                    targetEnemyId,
                    predicate.StatusId),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static bool TargetHasStatus(
        CombatState combat,
        int? targetEnemyId,
        string? statusId)
    {
        if (targetEnemyId is null || statusId is null)
        {
            return false;
        }

        var enemy = combat.Enemies.FirstOrDefault(item =>
            item.InstanceId == targetEnemyId.Value);
        return enemy is not null
            && enemy.Statuses.GetValueOrDefault(statusId) > 0;
    }

    private static int ModifyPlayerAttackDamage(
        CombatState combat,
        long? sourceCardInstanceId,
        int targetEnemyId,
        int damage)
    {
        if (sourceCardInstanceId is null)
        {
            return damage;
        }

        var sourceCard = RequireCombatCard(
            combat,
            sourceCardInstanceId.Value);
        var sourceDefinition = PrototypeContent.Card(
            sourceCard.CardId);
        var tags = sourceDefinition.Tags
            ?? Array.Empty<string>();

        var additive = combat.PlayerPowers.Sum(power =>
        {
            var definition = PrototypeContent.Power(
                power.PowerId);
            if (definition.AttackDamageBonusPerStack == 0
                || definition.AttackDamageBonusRequiredCardTag is null
                || !tags.Contains(
                    definition.AttackDamageBonusRequiredCardTag,
                    StringComparer.Ordinal))
            {
                return 0;
            }

            return definition.AttackDamageBonusPerStack
                * power.Stacks;
        });

        var firstTaggedPlayBonus = combat.PlayerPowers.Sum(
            power =>
            {
                var definition = PrototypeContent.Power(
                    power.PowerId);
                if (power.Stacks <= 0
                    || definition
                        .FirstAttackDamageBonusPerStack == 0
                    || definition
                        .FirstAttackDamageBonusRequiredCardTag is
                        not { } requiredTag
                    || !tags.Contains(
                        requiredTag,
                        StringComparer.Ordinal)
                    || combat.CounterState.PlaysWithTag(
                        requiredTag) > 0)
                {
                    return 0;
                }

                return definition.FirstAttackDamageBonusPerStack
                    * power.Stacks;
            });

        var modified =
            damage + additive + firstTaggedPlayBonus;
        var conditionalBonus = combat.PlayerPowers.Sum(power =>
        {
            var definition = PrototypeContent.Power(power.PowerId);
            if (definition.AttackDamageBonusRequiredTargetStatus is null
                || definition.AttackDamageBonusTargetNumeratorPerStack == 0
                || definition.AttackDamageBonusTargetDenominator <= 0
                || !TargetHasStatus(
                    combat,
                    targetEnemyId,
                    definition.AttackDamageBonusRequiredTargetStatus))
            {
                return 0;
            }

            return (modified
                    * definition.AttackDamageBonusTargetNumeratorPerStack
                    * power.Stacks)
                / definition.AttackDamageBonusTargetDenominator;
        });

        modified += conditionalBonus;
        if (sourceDefinition.Type != PrototypeCardType.Attack)
        {
            return modified;
        }

        return ModifyPlayerOutgoingAttackDamage(
            combat,
            modified);
    }

    private static int ModifyIncomingAttackDamage(
        CombatState combat,
        int enemyId,
        int damage)
    {
        var enemy = combat.Enemies.SingleOrDefault(item => item.InstanceId == enemyId)
            ?? throw new InvalidOperationException($"Enemy {enemyId} is missing.");

        var modified = damage;
        foreach (var status in enemy.Statuses)
        {
            var definition = PrototypeContent.Status(status.Key);
            modified = (modified * definition.IncomingAttackDamageNumerator)
                / definition.IncomingAttackDamageDenominator;
        }

        return modified;
    }

    private static int PlayerAttackRetaliation(CombatState combat) =>
        combat.PlayerPowers.Sum(power =>
            PrototypeContent.Power(power.PowerId).AttackRetaliationPerStack * power.Stacks);

    private static int EnemyAttackRetaliation(
        CombatState combat,
        int enemyId)
    {
        var enemy = combat.Enemies.SingleOrDefault(item =>
            item.InstanceId == enemyId)
            ?? throw new InvalidOperationException($"Enemy {enemyId} is missing.");

        return enemy.PowerStates.Sum(power =>
            PrototypeContent.Power(power.PowerId).AttackRetaliationPerStack
            * power.Stacks);
    }

    private sealed record PrototypeDebuffBlockResult(
        CombatState Combat,
        bool Blocked);

    private static PrototypeDebuffBlockResult
        TryBlockIncomingPlayerDebuff(CombatState combat)
    {
        var candidate = combat.PlayerPowers
            .Where(power =>
                power.Stacks > 0
                && PrototypeContent.Power(power.PowerId)
                    .BlocksNextDebuff)
            .OrderBy(power => power.ApplicationOrder)
            .FirstOrDefault();
        if (candidate is null)
        {
            return new PrototypeDebuffBlockResult(
                combat,
                false);
        }

        var powers = combat.PlayerPowers.ToList();
        var index = powers.FindIndex(power =>
            power.ApplicationOrder
                == candidate.ApplicationOrder);
        if (index < 0)
        {
            throw new InvalidOperationException(
                "Debuff-blocking power disappeared before consumption.");
        }

        if (powers[index].Stacks <= 1)
        {
            powers.RemoveAt(index);
        }
        else
        {
            powers[index] = powers[index] with
            {
                Stacks = powers[index].Stacks - 1
            };
        }

        return new PrototypeDebuffBlockResult(
            combat with { PlayerPowers = powers.ToArray() },
            true);
    }

    private static CombatState ApplyPlayerPower(
        CombatState combat,
        string powerId,
        int stacks,
        int? sourceEnemyInstanceId = null)
    {
        var definition = PrototypeContent.Power(powerId);
        if (definition.SourceBoundToEnemy
            && sourceEnemyInstanceId is null)
        {
            throw new InvalidOperationException(
                $"Source-bound power '{powerId}' requires an enemy source.");
        }

        if (definition.IsDebuff && stacks > 0)
        {
            var blocked = TryBlockIncomingPlayerDebuff(combat);
            combat = blocked.Combat;
            if (blocked.Blocked)
            {
                return combat;
            }
        }

        if (definition.IsInstanced)
        {
            if (definition.RequiresCardPayload)
            {
                throw new InvalidOperationException(
                    $"Instanced power '{powerId}' requires a card payload.");
            }

            return AddPlayerPowerInstance(
                combat,
                powerId,
                stacks,
                null,
                sourceEnemyInstanceId);
        }

        var powers = combat.PlayerPowers.ToList();
        var index = powers.FindIndex(power =>
            StringComparer.Ordinal.Equals(power.PowerId, powerId)
            && (!definition.SourceBoundToEnemy
                || power.SourceEnemyInstanceId
                    == sourceEnemyInstanceId));

        if (index >= 0)
        {
            var nextStacks = definition.DoesNotStack
                ? Math.Max(powers[index].Stacks, stacks)
                : powers[index].Stacks + stacks;
            PrototypePowerInstanceState? updatedPower = null;
            if (nextStacks == 0
                || (!definition.AllowNegative && nextStacks < 0))
            {
                powers.RemoveAt(index);
            }
            else
            {
                powers[index] = powers[index] with
                {
                    Stacks = nextStacks
                };
                updatedPower = powers[index];
            }

            combat = combat with
            {
                PlayerPowers = powers.ToArray()
            };
            return updatedPower is null
                ? combat
                : ApplySourceBoundCardEffects(
                    combat,
                    updatedPower);
        }

        if (stacks == 0 || (!definition.AllowNegative && stacks < 0))
        {
            return combat;
        }

        var instance = new PrototypePowerInstanceState(
            powerId,
            stacks,
            combat.NextPowerApplicationOrder,
            SourceEnemyInstanceId: sourceEnemyInstanceId);
        powers.Add(instance);

        combat = combat with
        {
            PlayerPowers = powers.ToArray(),
            NextPowerApplicationOrder =
                combat.NextPowerApplicationOrder + 1
        };
        return ApplySourceBoundCardEffects(
            combat,
            instance);
    }

    private static CombatState ApplyPlayerPowerWithSelectedCard(
        CombatState combat,
        PrototypeSelectedCardPowerAction action,
        CombatCardInstance selectedCard)
    {
        var definition = PrototypeContent.Power(action.PowerId);
        if (!definition.IsInstanced || !definition.RequiresCardPayload)
        {
            throw new InvalidOperationException(
                $"Selected-card power '{action.PowerId}' must be an instanced card-payload power.");
        }

        var snapshot = new PrototypeCombatCardSnapshot(
            selectedCard.CardId,
            action.RestoreSuppressedUpgradesInPayload
                ? selectedCard.UpgradeLevel
                    + selectedCard.SuppressedUpgradeLevels
                : selectedCard.UpgradeLevel,
            selectedCard.State.Clone(),
            selectedCard.CombatEnergyCostDelta,
            selectedCard.TemporaryEnergyCost is null
                ? null
                : selectedCard.TemporaryEnergyCost with { },
            selectedCard.KeywordOverrides is null
                ? null
                : selectedCard.KeywordOverrides
                    .Select(item => item with { })
                    .ToArray(),
            selectedCard.ReplayCount,
            selectedCard.Enchantment,
            selectedCard.EnchantmentTriggeredThisCombat,
            action.ClearAfflictionFromPayload
                ? null
                : selectedCard.Affliction,
            action.RestoreSuppressedUpgradesInPayload
                ? 0
                : selectedCard.SuppressedUpgradeLevels);

        return AddPlayerPowerInstance(
            combat,
            action.PowerId,
            action.Amount,
            snapshot);
    }

    private static CombatState AddPlayerPowerInstance(
        CombatState combat,
        string powerId,
        int stacks,
        PrototypeCombatCardSnapshot? cardPayload,
        int? sourceEnemyInstanceId = null)
    {
        var definition = PrototypeContent.Power(powerId);
        if (stacks == 0 || (!definition.AllowNegative && stacks < 0))
        {
            return combat;
        }

        if (definition.RequiresCardPayload != (cardPayload is not null))
        {
            throw new InvalidOperationException(
                $"Power '{powerId}' card-payload requirement is not satisfied.");
        }

        if (definition.SourceBoundToEnemy
            && sourceEnemyInstanceId is null)
        {
            throw new InvalidOperationException(
                $"Source-bound power '{powerId}' requires an enemy source.");
        }

        combat = combat with
        {
            PlayerPowers = combat.PlayerPowers.Append(
                new PrototypePowerInstanceState(
                    powerId,
                    stacks,
                    combat.NextPowerApplicationOrder,
                    cardPayload?.Fork(),
                    sourceEnemyInstanceId)).ToArray(),
            NextPowerApplicationOrder =
                combat.NextPowerApplicationOrder + 1
        };

        var added = combat.PlayerPowers[^1];
        return ApplySourceBoundCardEffects(
            combat,
            added);
    }

    private static CombatState ApplySourceBoundCardEffects(
        CombatState combat,
        PrototypePowerInstanceState power)
    {
        combat = ApplySourceBoundCardAffliction(
            combat,
            power);
        return ApplySourceBoundCardDowngrade(
            combat,
            power);
    }

    private static CombatState ApplySourceBoundCardDowngrade(
        CombatState combat,
        PrototypePowerInstanceState power)
    {
        var definition = PrototypeContent.Power(power.PowerId);
        if (!definition.DowngradeExistingCardsOnApply)
        {
            return combat;
        }

        if (power.SourceEnemyInstanceId is not { } sourceEnemyId)
        {
            throw new InvalidOperationException(
                $"Downgrade source power '{power.PowerId}' is missing its enemy source.");
        }

        var source = combat.Enemies.FirstOrDefault(enemy =>
            enemy.InstanceId == sourceEnemyId);
        if (source is null || source.Hp <= 0)
        {
            return combat;
        }

        return combat with
        {
            Cards = combat.Cards
                .Select(card =>
                {
                    if (card.UpgradeLevel <= 0)
                    {
                        return card;
                    }

                    return card with
                    {
                        SuppressedUpgradeLevels =
                            card.SuppressedUpgradeLevels
                            + card.UpgradeLevel,
                        UpgradeLevel = 0
                    };
                })
                .ToArray()
        };
    }

    private static CombatState ApplySourceBoundCardAffliction(
        CombatState combat,
        PrototypePowerInstanceState power)
    {
        var definition = PrototypeContent.Power(power.PowerId);
        if (definition.SourceBoundCardAffliction is not
                { } afflictionKind)
        {
            return combat;
        }

        if (power.SourceEnemyInstanceId is not { } sourceEnemyId)
        {
            throw new InvalidOperationException(
                $"Affliction source power '{power.PowerId}' is missing its enemy source.");
        }

        var source = combat.Enemies.FirstOrDefault(enemy =>
            enemy.InstanceId == sourceEnemyId);
        if (source is null || source.Hp <= 0)
        {
            return combat;
        }

        return combat with
        {
            Cards = combat.Cards
                .Select(card =>
                {
                    if (card.Affliction is not null
                        && definition.SkipCardsWithExistingAffliction)
                    {
                        return card;
                    }

                    return card with
                    {
                        Affliction = new PrototypeCardAffliction(
                            afflictionKind,
                            SourceEnemyInstanceId: sourceEnemyId)
                    };
                })
                .ToArray()
        };
    }

    private static CombatCardInstance
        ApplyActiveSourceBoundAfflictionToCard(
            CombatState combat,
            CombatCardInstance card)
    {
        foreach (var power in combat.PlayerPowers
                     .OrderBy(item => item.ApplicationOrder))
        {
            var definition = PrototypeContent.Power(
                power.PowerId);
            if (definition.SourceBoundCardAffliction is not
                    { } afflictionKind
                || power.SourceEnemyInstanceId is not
                    { } sourceEnemyId)
            {
                continue;
            }

            var source = combat.Enemies.FirstOrDefault(enemy =>
                enemy.InstanceId == sourceEnemyId);
            if (source is null || source.Hp <= 0)
            {
                continue;
            }

            if (card.Affliction is not null
                && definition.SkipCardsWithExistingAffliction)
            {
                continue;
            }

            return card with
            {
                Affliction = new PrototypeCardAffliction(
                    afflictionKind,
                    SourceEnemyInstanceId: sourceEnemyId)
            };
        }

        return card;
    }

    private static CombatState
        CleanupSourceBoundPowersForDefeatedEnemies(
            CombatState combat)
    {
        var defeatedEnemyIds = combat.Enemies
            .Where(enemy => enemy.Hp <= 0)
            .Select(enemy => enemy.InstanceId)
            .ToHashSet();
        if (defeatedEnemyIds.Count == 0)
        {
            return combat;
        }

        var removed = combat.PlayerPowers
            .Where(power =>
                power.SourceEnemyInstanceId is { } sourceEnemyId
                && defeatedEnemyIds.Contains(sourceEnemyId)
                && PrototypeContent.Power(power.PowerId)
                    .SourceBoundToEnemy)
            .ToArray();
        if (removed.Length == 0)
        {
            return combat;
        }

        var clearAfflictions = removed
            .Select(power =>
            {
                var definition = PrototypeContent.Power(
                    power.PowerId);
                return (
                    SourceEnemyId:
                        power.SourceEnemyInstanceId!.Value,
                    Kind:
                        definition.SourceBoundCardAffliction,
                    Clear:
                        definition.ClearSourceAfflictionWhenRemoved);
            })
            .Where(item =>
                item.Clear && item.Kind is not null)
            .Select(item => (
                item.SourceEnemyId,
                Kind: item.Kind!.Value))
            .ToHashSet();

        var removedOrders = removed
            .Select(power => power.ApplicationOrder)
            .ToHashSet();
        var remainingPowers = combat.PlayerPowers
            .Where(power =>
                !removedOrders.Contains(
                    power.ApplicationOrder))
            .ToArray();

        var restoreDowngradePowerIds = removed
            .Select(power => power.PowerId)
            .Distinct(StringComparer.Ordinal)
            .Where(powerId =>
            {
                var definition = PrototypeContent.Power(powerId);
                return definition
                        .RestoreDowngradedCardsWhenLastSourceRemoved
                    && !remainingPowers.Any(power =>
                        StringComparer.Ordinal.Equals(
                            power.PowerId,
                            powerId));
            })
            .ToHashSet(StringComparer.Ordinal);

        var shouldRestoreSuppressedUpgrades =
            restoreDowngradePowerIds.Count > 0;

        return combat with
        {
            PlayerPowers = remainingPowers,
            Cards = combat.Cards
                .Select(card =>
                {
                    var next = card;
                    if (card.Affliction is
                            {
                                SourceEnemyInstanceId:
                                    { } sourceEnemyId
                            } affliction
                        && clearAfflictions.Contains((
                            sourceEnemyId,
                            affliction.Kind)))
                    {
                        next = next with
                        {
                            Affliction = null
                        };
                    }

                    if (shouldRestoreSuppressedUpgrades
                        && next.SuppressedUpgradeLevels > 0)
                    {
                        next = next with
                        {
                            UpgradeLevel =
                                next.UpgradeLevel
                                + next.SuppressedUpgradeLevels,
                            SuppressedUpgradeLevels = 0
                        };
                    }

                    return next;
                })
                .ToArray()
        };
    }

    private static (
        CombatState Combat,
        EnemyCombatState Enemy)
        ApplyEnemyPowerToState(
            CombatState combat,
            EnemyCombatState enemy,
            string powerId,
            int stacks)
    {
        var definition = PrototypeContent.Power(powerId);
        if (definition.IsInstanced
            && definition.RequiresCardPayload)
        {
            throw new InvalidOperationException(
                $"Enemy power '{powerId}' requires a card payload.");
        }

        if (enemy.Hp <= 0)
        {
            return (combat, enemy);
        }

        var powers = enemy.PowerStates.ToList();
        var powerIndex = definition.IsInstanced
            ? -1
            : powers.FindIndex(power =>
                StringComparer.Ordinal.Equals(
                    power.PowerId,
                    powerId));

        if (powerIndex >= 0)
        {
            var nextStacks =
                powers[powerIndex].Stacks + stacks;
            if (nextStacks == 0
                || (!definition.AllowNegative
                    && nextStacks < 0))
            {
                powers.RemoveAt(powerIndex);
            }
            else
            {
                powers[powerIndex] =
                    powers[powerIndex] with
                    {
                        Stacks = nextStacks
                    };
            }

            return (
                combat,
                enemy with { Powers = powers.ToArray() });
        }

        if (stacks == 0
            || (!definition.AllowNegative && stacks < 0))
        {
            return (combat, enemy);
        }

        powers.Add(new PrototypePowerInstanceState(
            powerId,
            stacks,
            combat.NextPowerApplicationOrder));

        return (
            combat with
            {
                NextPowerApplicationOrder =
                    combat.NextPowerApplicationOrder + 1
            },
            enemy with { Powers = powers.ToArray() });
    }

    private static CombatState ApplyEnemyPower(
        CombatState combat,
        int enemyId,
        string powerId,
        int stacks)
    {
        var enemies = combat.Enemies
            .Select(enemy => enemy.Fork())
            .ToArray();
        var enemyIndex = Array.FindIndex(
            enemies,
            enemy => enemy.InstanceId == enemyId);
        if (enemyIndex < 0)
        {
            throw new InvalidOperationException(
                $"Enemy {enemyId} is missing.");
        }

        var result = ApplyEnemyPowerToState(
            combat,
            enemies[enemyIndex],
            powerId,
            stacks);
        enemies[enemyIndex] = result.Enemy;
        return result.Combat with { Enemies = enemies };
    }

    private static (CombatState Combat, int Count) IncrementRelicTriggerCounter(
        CombatState combat,
        int relicStateIndex,
        int triggerIndex)
    {
        var relics = combat.RelicStates.Select(relic => relic.Fork()).ToArray();
        if (relicStateIndex < 0 || relicStateIndex >= relics.Length)
        {
            throw new InvalidOperationException("Relic trigger references a missing combat relic.");
        }

        var relic = relics[relicStateIndex];
        if (triggerIndex < 0 || triggerIndex >= relic.TriggerCounts.Length)
        {
            throw new InvalidOperationException("Relic trigger counter index is invalid.");
        }

        var counts = (int[])relic.TriggerCounts.Clone();
        counts[triggerIndex]++;
        relics[relicStateIndex] = relic with { TriggerCounts = counts };

        return (combat with { Relics = relics }, counts[triggerIndex]);
    }

    private static (PlayerState Player, CombatState Combat) DispatchCombatEvent(
        PlayerState player,
        CombatState combat,
        PrototypeCombatEvent combatEvent,
        RngBundle rng,
        int eventDepth = 0,
        bool allowSuspension = false)
    {
        if (eventDepth > 64)
        {
            throw new InvalidOperationException(
                "Prototype combat event chain exceeded the safety depth limit.");
        }

        combat = RecordCombatCounterEvent(combat, combatEvent);
        var subscribers = new List<PrototypeEventSubscriberState>();

        foreach (var power in combat.PlayerPowers)
        {
            var definition = PrototypeContent.Power(power.PowerId);
            subscribers.AddRange(
                definition.Triggers
                    .Where(trigger =>
                        trigger.EventKind == combatEvent.Kind
                        && (!trigger.ExcludeHandDraw
                            || !combatEvent.FromHandDraw)
                        && (!trigger.RequiresPlayerTurn
                            || combat.IsPlayerTurn)
                        && (trigger.RequiredSourceCardType is null
                            || (combatEvent.CardId is not null
                                && PrototypeContent.Card(
                                    combatEvent.CardId).Type
                                    == trigger.RequiredSourceCardType.Value))
                        && !trigger.RequiresOwnerTarget
                        && (combatEvent.PowerApplicationOrderCeiling is null
                            || power.ApplicationOrder
                                <= combatEvent.PowerApplicationOrderCeiling.Value))
                    .Select(trigger => new PrototypeEventSubscriberState(
                        power.ApplicationOrder,
                        trigger.Effects,
                        power.Stacks,
                        PrototypeEffectSourceKind.Power,
                        SourcePowerApplicationOrder: power.ApplicationOrder,
                        PowerCardPayload: power.CardPayload?.Fork(),
                        RemoveSourcePowerAfterTrigger:
                            trigger.RemoveSourcePowerAfterTrigger)));
        }

        foreach (var enemy in combat.Enemies)
        {
            foreach (var power in enemy.PowerStates)
            {
                var definition = PrototypeContent.Power(power.PowerId);
                subscribers.AddRange(
                    definition.Triggers
                        .Where(trigger =>
                            trigger.EventKind == combatEvent.Kind
                            && (!trigger.ExcludeHandDraw
                                || !combatEvent.FromHandDraw)
                            && (!trigger.RequiresPlayerTurn
                                || combat.IsPlayerTurn)
                            && (trigger.RequiredSourceCardType is null
                                || (combatEvent.CardId is not null
                                    && PrototypeContent.Card(
                                        combatEvent.CardId).Type
                                        == trigger.RequiredSourceCardType.Value))
                            && (combatEvent.PowerApplicationOrderCeiling is null
                                || power.ApplicationOrder
                                    <= combatEvent.PowerApplicationOrderCeiling.Value)
                            && (!trigger.RequiresOwnerTarget
                                || combatEvent.TargetEnemyId == enemy.InstanceId)
                            && (enemy.Hp > 0
                                || (trigger.RequiresOwnerTarget
                                    && combatEvent.TargetEnemyId == enemy.InstanceId)))
                        .Select(trigger => new PrototypeEventSubscriberState(
                            power.ApplicationOrder,
                            trigger.Effects,
                            power.Stacks,
                            PrototypeEffectSourceKind.Power,
                            SourcePowerApplicationOrder: power.ApplicationOrder,
                            SourcePowerEnemyId: enemy.InstanceId,
                            PowerCardPayload: power.CardPayload?.Fork(),
                            RemoveSourcePowerAfterTrigger:
                                trigger.RemoveSourcePowerAfterTrigger)));
            }
        }

        for (var relicIndex = 0; relicIndex < combat.RelicStates.Length; relicIndex++)
        {
            var relic = combat.RelicStates[relicIndex];
            var definition = PrototypeContent.Relic(relic.RelicId);
            var triggers = definition.Triggers ?? Array.Empty<PrototypeRelicTriggerSpec>();
            for (var triggerIndex = 0; triggerIndex < triggers.Length; triggerIndex++)
            {
                var trigger = triggers[triggerIndex];
                if (trigger.EventKind != combatEvent.Kind)
                {
                    continue;
                }

                subscribers.Add(new PrototypeEventSubscriberState(
                    relic.ApplicationOrder,
                    trigger.Effects,
                    PowerStacks: 0,
                    SourceKind: PrototypeEffectSourceKind.Relic,
                    RelicStateIndex: relicIndex,
                    RelicTriggerIndex: triggerIndex,
                    EveryNth: trigger.EveryNth));
            }
        }

        var orderedSubscribers = subscribers
            .OrderBy(item => item.ApplicationOrder)
            .ToArray();

        for (var subscriberIndex = 0;
             subscriberIndex < orderedSubscribers.Length;
             subscriberIndex++)
        {
            var subscriber = orderedSubscribers[subscriberIndex];
            if (subscriber.RelicStateIndex is not null)
            {
                if (subscriber.RelicTriggerIndex is null || subscriber.EveryNth <= 0)
                {
                    throw new InvalidOperationException("Relic trigger counter metadata is invalid.");
                }

                var incremented = IncrementRelicTriggerCounter(
                    combat,
                    subscriber.RelicStateIndex.Value,
                    subscriber.RelicTriggerIndex.Value);
                combat = incremented.Combat;
                if (incremented.Count % subscriber.EveryNth != 0)
                {
                    continue;
                }
            }

            var operations = new Queue<PrototypeQueuedOperation>();
            foreach (var effect in subscriber.Effects)
            {
                EnqueueEffectOperations(
                    operations,
                    effect,
                    upgradeLevel: 0,
                    energySpent: 0,
                    actionTargetEnemyId: combatEvent.TargetEnemyId,
                    combat: combat,
                    powerStacks: subscriber.PowerStacks,
                    sourceKind: subscriber.SourceKind,
                    powerCardPayload: subscriber.PowerCardPayload);
            }

            var continuation =
                new PrototypeEventDispatchContinuationState(
                    combatEvent,
                    subscriber.Fork(),
                    orderedSubscribers
                        .Skip(subscriberIndex + 1)
                        .Select(item => item.Fork())
                        .ToArray(),
                    eventDepth);
            var resolved = ResolveOperations(
                player,
                combat,
                operations,
                rng,
                eventDepth: eventDepth,
                eventSourceCardInstanceId:
                    combatEvent.SourceCardInstanceId,
                eventDispatchContinuation:
                    continuation);
            player = resolved.Player;
            combat = resolved.Combat;

            if (combat.PendingChoice is not null)
            {
                if (!allowSuspension)
                {
                    throw new NotSupportedException(
                        "Combat-event suspension outside a top-level automatic event step requires an outer continuation frame.");
                }

                return (player, combat);
            }

            combat = FinalizeEventSubscriber(
                combat,
                subscriber);
        }

        if (combatEvent.Kind == PrototypeCombatEventKind.PlayerTurnEnded)
        {
            combat = combat with
            {
                PlayerPowers = combat.PlayerPowers
                    .Where(power =>
                        !PrototypeContent.Power(power.PowerId)
                            .RemoveAtPlayerTurnEnd)
                    .ToArray()
            };
            combat = DecrementPlayerPowers(
                combat,
                definition => definition.DecrementAtPlayerTurnEnd);
        }

        if (combatEvent.Kind == PrototypeCombatEventKind.PlayerTurnStarted)
        {
            combat = DecrementPlayerPowers(
                combat,
                definition => definition.DecrementAfterPlayerTurnStart);
        }

        return (player, combat);
    }

    private static (PlayerState Player, CombatState Combat)
        ResumeEventDispatchContinuation(
            PlayerState player,
            CombatState combat,
            PrototypeEventDispatchContinuationState continuation,
            RngBundle rng)
    {
        combat = FinalizeEventSubscriber(
            combat,
            continuation.CurrentSubscriber);

        var remaining = continuation.RemainingSubscribers;
        for (var index = 0; index < remaining.Length; index++)
        {
            var subscriber = remaining[index];
            if (subscriber.RelicStateIndex is not null)
            {
                if (subscriber.RelicTriggerIndex is null
                    || subscriber.EveryNth <= 0)
                {
                    throw new InvalidOperationException(
                        "Relic trigger counter metadata is invalid.");
                }

                var incremented = IncrementRelicTriggerCounter(
                    combat,
                    subscriber.RelicStateIndex.Value,
                    subscriber.RelicTriggerIndex.Value);
                combat = incremented.Combat;
                if (incremented.Count % subscriber.EveryNth != 0)
                {
                    continue;
                }
            }

            var operations = new Queue<PrototypeQueuedOperation>();
            foreach (var effect in subscriber.Effects)
            {
                EnqueueEffectOperations(
                    operations,
                    effect,
                    upgradeLevel: 0,
                    energySpent: 0,
                    actionTargetEnemyId:
                        continuation.CombatEvent.TargetEnemyId,
                    combat: combat,
                    powerStacks: subscriber.PowerStacks,
                    sourceKind: subscriber.SourceKind,
                    powerCardPayload: subscriber.PowerCardPayload);
            }

            var nextContinuation =
                new PrototypeEventDispatchContinuationState(
                    continuation.CombatEvent,
                    subscriber.Fork(),
                    remaining
                        .Skip(index + 1)
                        .Select(item => item.Fork())
                        .ToArray(),
                    continuation.EventDepth);
            var resolved = ResolveOperations(
                player,
                combat,
                operations,
                rng,
                eventDepth: continuation.EventDepth,
                eventSourceCardInstanceId:
                    continuation.CombatEvent.SourceCardInstanceId,
                eventDispatchContinuation:
                    nextContinuation);
            player = resolved.Player;
            combat = resolved.Combat;

            if (combat.PendingChoice is not null)
            {
                return (player, combat);
            }

            combat = FinalizeEventSubscriber(
                combat,
                subscriber);
        }

        if (continuation.CombatEvent.Kind
            == PrototypeCombatEventKind.PlayerTurnEnded)
        {
            combat = combat with
            {
                PlayerPowers = combat.PlayerPowers
                    .Where(power =>
                        !PrototypeContent.Power(power.PowerId)
                            .RemoveAtPlayerTurnEnd)
                    .ToArray()
            };
            combat = DecrementPlayerPowers(
                combat,
                definition => definition.DecrementAtPlayerTurnEnd);
        }

        if (continuation.CombatEvent.Kind
            == PrototypeCombatEventKind.PlayerTurnStarted)
        {
            combat = DecrementPlayerPowers(
                combat,
                definition => definition.DecrementAfterPlayerTurnStart);
        }

        return (player, combat);
    }

    private static CombatState FinalizeEventSubscriber(
        CombatState combat,
        PrototypeEventSubscriberState subscriber)
    {
        if (!subscriber.RemoveSourcePowerAfterTrigger)
        {
            return combat;
        }

        if (subscriber.SourcePowerApplicationOrder is null)
        {
            throw new InvalidOperationException(
                "Self-removing power trigger is missing source-power identity.");
        }

        return RemovePowerInstance(
            combat,
            subscriber.SourcePowerApplicationOrder.Value,
            subscriber.SourcePowerEnemyId);
    }

    private static CombatState RemovePowerInstance(
        CombatState combat,
        long applicationOrder,
        int? enemyId)
    {
        if (enemyId is null)
        {
            return combat with
            {
                PlayerPowers = combat.PlayerPowers
                    .Where(power => power.ApplicationOrder != applicationOrder)
                    .ToArray()
            };
        }

        var enemies = combat.Enemies.Select(enemy => enemy.Fork()).ToArray();
        var index = Array.FindIndex(
            enemies,
            enemy => enemy.InstanceId == enemyId.Value);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Enemy {enemyId.Value} is missing while removing a power.");
        }

        enemies[index] = enemies[index] with
        {
            Powers = enemies[index].PowerStates
                .Where(power => power.ApplicationOrder != applicationOrder)
                .ToArray()
        };
        return combat with { Enemies = enemies };
    }

    private static IReadOnlyList<GameAction> GetPendingChoiceActions(
        PendingCombatChoiceState pending)
    {
        if (!StringComparer.Ordinal.Equals(pending.ChoiceId, "select_cards"))
        {
            throw new InvalidOperationException($"Unknown pending combat choice '{pending.ChoiceId}'.");
        }

        var actions = new List<GameAction>();
        for (var count = pending.Selection.MinSelections;
             count <= pending.Selection.MaxSelections;
             count++)
        {
            foreach (var selection in ChooseCombinations(
                         pending.CandidateCardInstanceIds,
                         count))
            {
                actions.Add(GameAction.Create(
                    "select_cards",
                    new SelectCardsPayload(selection)));
            }
        }

        return actions;
    }

    private static RunState ResolvePendingChoice(RunState state, GameAction action)
    {
        RequireKind(action, "select_cards");
        var payload = action.ReadPayload<SelectCardsPayload>();
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException("Combat phase has no combat state.");
        var pending = combat.PendingChoice
            ?? throw new InvalidOperationException("Combat has no pending choice.");

        if (!StringComparer.Ordinal.Equals(pending.ChoiceId, "select_cards"))
        {
            throw new InvalidOperationException($"Unknown pending combat choice '{pending.ChoiceId}'.");
        }

        var selected = payload.CardInstanceIds;
        if (selected.Length != selected.Distinct().Count())
        {
            throw new InvalidOperationException("Card selection contains duplicate instance IDs.");
        }

        if (selected.Length < pending.Selection.MinSelections
            || selected.Length > pending.Selection.MaxSelections)
        {
            throw new InvalidOperationException(
                $"Selection count {selected.Length} is outside " +
                $"{pending.Selection.MinSelections}..{pending.Selection.MaxSelections}.");
        }

        var candidates = pending.CandidateCardInstanceIds.ToHashSet();
        if (selected.Any(cardId => !candidates.Contains(cardId)))
        {
            throw new InvalidOperationException("Selection contains a card outside the pending candidate set.");
        }

        var slyCards = pending.Selection.SourceZone == PrototypeCardZone.Hand
            && pending.Selection.Resolution == PrototypeCardSelectionResolutionKind.MoveToDiscard
                ? selected
                    .Select(cardId => RequireCombatCard(combat, cardId))
                    .Where(card =>
                        CardHasKeyword(
                            combat,
                            card.InstanceId,
                            PrototypeCardKeyword.Sly))
                    .Select(card => card.InstanceId)
                    .ToArray()
                : Array.Empty<long>();

        combat = ApplyCardSelection(combat, pending.Selection, selected);
        combat = combat with { PendingChoice = null };

        if (pending.SelectedCardPower is not null)
        {
            if (selected.Length != 1)
            {
                throw new InvalidOperationException(
                    "Selected-card power actions require exactly one selected card.");
            }

            combat = ApplyPlayerPowerWithSelectedCard(
                combat,
                pending.SelectedCardPower,
                RequireCombatCard(combat, selected[0]));
        }

        if (pending.SelectedCardKeyword is not null)
        {
            foreach (var cardInstanceId in selected)
            {
                combat = ApplyCardKeywordOverride(
                    combat,
                    cardInstanceId,
                    pending.SelectedCardKeyword);
            }
        }

        var discardEvents =
            pending.Selection.SourceZone == PrototypeCardZone.Hand
            && pending.Selection.Resolution
                == PrototypeCardSelectionResolutionKind.MoveToDiscard
                ? selected.Select(cardId =>
                {
                    var discardedCard =
                        RequireCombatCard(combat, cardId);
                    return new PrototypeCombatEvent(
                        PrototypeCombatEventKind.CardDiscarded,
                        SourceCardInstanceId: cardId,
                        CardId: discardedCard.CardId);
                }).ToArray()
                : Array.Empty<PrototypeCombatEvent>();

        var resolutionContinuation =
            new PrototypeChoiceResolutionContinuationState(
                SourceCardInstanceId:
                    pending.SourceCardInstanceId,
                SourceCardDestination:
                    pending.SourceCardDestination,
                Operations:
                    (PrototypeQueuedOperation[])
                    pending.Continuation.Clone(),
                PendingDiscardEvents: discardEvents,
                PendingSlyCardInstanceIds:
                    (long[])slyCards.Clone(),
                CompletionEvents:
                    (PrototypeCombatEvent[])
                    pending.CompletionEvents.Clone(),
                CardPlaySeries:
                    pending.CardPlaySeries,
                MoveSourceCardOnCompletion:
                    pending.MoveSourceCardOnCompletion,
                RemoveSourceCardOnCompletion:
                    pending.RemoveSourceCardOnCompletion,
                SourceCardAlreadyMoved:
                    pending.SourceCardAlreadyMoved,
                EventDispatchContinuation:
                    pending.EventDispatchContinuation?.Fork(),
                Parent:
                    pending.OuterChoiceContinuation?.Fork());

        var resolved = ResumeChoiceResolutionContinuation(
            state.Player,
            combat,
            state.Rng,
            resolutionContinuation);

        state = state with
        {
            Player = resolved.Player,
            World = world with { Combat = resolved.Combat }
        };

        if (resolved.Combat.PendingChoice is null
            && resolved.Combat.AutomaticPipelineContinuation is
                { } automaticContinuation)
        {
            var resumedWorld = RequireWorld(state);
            var resumedCombat = resumedWorld.Combat
                ?? throw new InvalidOperationException(
                    "Combat unexpectedly disappeared.");
            state = state with
            {
                World = resumedWorld with
                {
                    Combat = resumedCombat with
                    {
                        AutomaticPipelineContinuation = null
                    }
                }
            };
            state = ResumeAutomaticPipeline(
                state,
                automaticContinuation.NextStepIndex,
                automaticContinuation.PendingPostDispatchEventKind);
        }

        if (state.Phase != RunPhase.Combat)
        {
            return state;
        }

        var currentCombat = RequireWorld(state).Combat
            ?? throw new InvalidOperationException(
                "Combat unexpectedly disappeared.");
        return AllEnemiesDefeated(currentCombat)
            ? EnterReward(state)
            : state;
    }

    private static (PlayerState Player, CombatState Combat)
        ResumeChoiceResolutionContinuation(
            PlayerState player,
            CombatState combat,
            RngBundle rng,
            PrototypeChoiceResolutionContinuationState continuation)
    {
        for (var eventIndex = 0;
             eventIndex < continuation.PendingDiscardEvents.Length;
             eventIndex++)
        {
            var dispatched = DispatchCombatEvent(
                player,
                combat,
                continuation.PendingDiscardEvents[eventIndex],
                rng);
            player = dispatched.Player;
            combat = dispatched.Combat;

            if (combat.PendingChoice is not null)
            {
                var remaining = continuation with
                {
                    PendingDiscardEvents =
                        continuation.PendingDiscardEvents
                            .Skip(eventIndex + 1)
                            .ToArray()
                };
                combat = AttachOuterChoiceContinuation(
                    combat,
                    remaining);
                return (player, combat);
            }
        }

        for (var slyIndex = 0;
             slyIndex < continuation.PendingSlyCardInstanceIds.Length;
             slyIndex++)
        {
            if (AllEnemiesDefeated(combat))
            {
                break;
            }

            var slyCardId =
                continuation.PendingSlyCardInstanceIds[slyIndex];
            if (!combat.DiscardPile.Contains(slyCardId))
            {
                continue;
            }

            var autoPlayed = AutoPlaySlyCard(
                player,
                combat,
                slyCardId,
                rng);
            player = autoPlayed.Player;
            combat = autoPlayed.Combat;

            if (combat.PendingChoice is not null)
            {
                var remaining = continuation with
                {
                    PendingDiscardEvents =
                        Array.Empty<PrototypeCombatEvent>(),
                    PendingSlyCardInstanceIds =
                        continuation.PendingSlyCardInstanceIds
                            .Skip(slyIndex + 1)
                            .ToArray()
                };
                combat = AttachOuterChoiceContinuation(
                    combat,
                    remaining);
                return (player, combat);
            }
        }

        var resolved = ResolveOperations(
            player,
            combat,
            new Queue<PrototypeQueuedOperation>(
                continuation.Operations),
            rng,
            continuation.SourceCardInstanceId,
            continuation.SourceCardDestination,
            continuation.CompletionEvents,
            cardPlaySeries: continuation.CardPlaySeries,
            moveSourceCardOnCompletion:
                continuation.MoveSourceCardOnCompletion,
            removeSourceCardOnCompletion:
                continuation.RemoveSourceCardOnCompletion,
            sourceCardAlreadyMoved:
                continuation.SourceCardAlreadyMoved,
            eventDispatchContinuation:
                continuation.EventDispatchContinuation);

        if (resolved.Combat.PendingChoice is not null)
        {
            return (
                resolved.Player,
                AttachParentChoiceContinuation(
                    resolved.Combat,
                    continuation.Parent));
        }

        if (continuation.EventDispatchContinuation is not null)
        {
            resolved = ResumeEventDispatchContinuation(
                resolved.Player,
                resolved.Combat,
                continuation.EventDispatchContinuation,
                rng);
            if (resolved.Combat.PendingChoice is not null)
            {
                return (
                    resolved.Player,
                    AttachParentChoiceContinuation(
                        resolved.Combat,
                        continuation.Parent));
            }
        }

        if (continuation.CardPlaySeries is
            { HasRemainingExecutions: true } series)
        {
            resolved = AllEnemiesDefeated(resolved.Combat)
                ? FinalizeInterruptedCardPlaySeries(
                    resolved.Player,
                    resolved.Combat,
                    rng,
                    series)
                : ResolveCardPlaySeries(
                    resolved.Player,
                    resolved.Combat,
                    rng,
                    series);

            if (resolved.Combat.PendingChoice is not null)
            {
                return (
                    resolved.Player,
                    AttachParentChoiceContinuation(
                        resolved.Combat,
                        continuation.Parent));
            }
        }

        return continuation.Parent is null
            ? resolved
            : ResumeChoiceResolutionContinuation(
                resolved.Player,
                resolved.Combat,
                rng,
                continuation.Parent);
    }

    private static CombatState AttachOuterChoiceContinuation(
        CombatState combat,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        var pending = combat.PendingChoice
            ?? throw new InvalidOperationException(
                "Nested choice continuation requires a pending choice.");
        var combined = pending.OuterChoiceContinuation is null
            ? continuation.Fork()
            : AppendChoiceContinuationParent(
                pending.OuterChoiceContinuation,
                continuation);
        return combat with
        {
            PendingChoice = pending with
            {
                OuterChoiceContinuation = combined
            }
        };
    }

    private static CombatState AttachParentChoiceContinuation(
        CombatState combat,
        PrototypeChoiceResolutionContinuationState? parent) =>
        parent is null
            ? combat
            : AttachOuterChoiceContinuation(combat, parent);

    private static PrototypeChoiceResolutionContinuationState
        AppendChoiceContinuationParent(
            PrototypeChoiceResolutionContinuationState current,
            PrototypeChoiceResolutionContinuationState parent) =>
        current.Parent is null
            ? current with { Parent = parent.Fork() }
            : current with
            {
                Parent = AppendChoiceContinuationParent(
                    current.Parent,
                    parent)
            };

    private static (PlayerState Player, CombatState Combat) AutoPlaySlyCard(
        PlayerState player,
        CombatState combat,
        long cardInstanceId,
        RngBundle rng)
    {
        if (!combat.DiscardPile.Contains(cardInstanceId))
        {
            throw new InvalidOperationException(
                $"Sly card {cardInstanceId} must be in the discard pile before auto-play.");
        }

        var card = RequireCombatCard(combat, cardInstanceId);
        var definition = PrototypeContent.Card(card.CardId);
        if (!CardHasKeyword(
                combat,
                cardInstanceId,
                PrototypeCardKeyword.Sly))
        {
            throw new InvalidOperationException(
                $"Card {cardInstanceId} is not Sly.");
        }

        // Native CardCmd.AutoPlay receives target=null for SlyDiscard. Self,
        // all-enemy and intrinsically-random cards resolve targeting inside
        // their own effect implementation. Explicit enemy-target Sly cards
        // remain unsupported until native target resolution is modeled.
        if (definition.Target == PrototypeCardTarget.Enemy)
        {
            throw new NotSupportedException(
                $"Sly auto-play for explicit enemy-target card '{definition.Name}' " +
                "requires native target-resolution semantics.");
        }

        combat = combat with
        {
            DiscardPile = combat.DiscardPile
                .Where(id => id != cardInstanceId)
                .ToArray()
        };

        if (IsCardFreeByPower(combat, definition))
        {
            combat = ConsumeMatchingFreeCardPower(combat, definition.Type);
        }

        var playCountResult =
            ResolveCardPlayCountAndConsumeModifiers(
                combat,
                card,
                definition.Type);
        combat = playCountResult.Combat;

        var sourceDestination = CardExhaustsOnUse(
                definition,
                card.UpgradeLevel)
            ? PrototypeCardZone.ExhaustPile
            : PrototypeCardZone.DiscardPile;

        return ResolveCardPlaySeries(
            player,
            combat,
            rng,
            new PrototypeCardPlaySeriesState(
                SourceCardInstanceId: cardInstanceId,
                SourceCardDestination: sourceDestination,
                TargetEnemyId: null,
                EnergySpent: 0,
                PlayCount: playCountResult.PlayCount,
                NextPlayIndex: 0,
                RemoveSourceCardOnCompletion:
                    definition.Type == PrototypeCardType.Power));
    }

    private static CombatState ApplyCardSelection(
        CombatState combat,
        PrototypeCardSelectionSpec selection,
        long[] selected)
    {
        var source = GetZone(combat, selection.SourceZone);
        var selectedSet = selected.ToHashSet();

        if (selected.Any(cardId => !source.Contains(cardId)))
        {
            throw new InvalidOperationException("Selected card is no longer in the requested source zone.");
        }

        if (selection.Resolution == PrototypeCardSelectionResolutionKind.Preserve)
        {
            return combat;
        }

        var destinationZone = selection.Resolution switch
        {
            PrototypeCardSelectionResolutionKind.MoveToDiscard => PrototypeCardZone.DiscardPile,
            PrototypeCardSelectionResolutionKind.MoveToExhaust => PrototypeCardZone.ExhaustPile,
            _ => throw new ArgumentOutOfRangeException()
        };

        if (destinationZone == selection.SourceZone)
        {
            throw new InvalidOperationException("Card-selection source and destination zones coincide.");
        }

        combat = SetZone(
            combat,
            selection.SourceZone,
            source.Where(cardId => !selectedSet.Contains(cardId)).ToArray());

        var destination = GetZone(combat, destinationZone);
        combat = SetZone(
            combat,
            destinationZone,
            destination.Concat(selected).ToArray());

        return combat;
    }

    private static long[] GetZone(CombatState combat, PrototypeCardZone zone) =>
        zone switch
        {
            PrototypeCardZone.Hand => combat.Hand,
            PrototypeCardZone.DrawPile => combat.DrawPile,
            PrototypeCardZone.DiscardPile => combat.DiscardPile,
            PrototypeCardZone.ExhaustPile => combat.ExhaustPile,
            _ => throw new ArgumentOutOfRangeException(nameof(zone))
        };

    private static CombatState SetZone(
        CombatState combat,
        PrototypeCardZone zone,
        long[] cards) =>
        zone switch
        {
            PrototypeCardZone.Hand => combat with { Hand = cards },
            PrototypeCardZone.DrawPile => combat with { DrawPile = cards },
            PrototypeCardZone.DiscardPile => combat with { DiscardPile = cards },
            PrototypeCardZone.ExhaustPile => combat with { ExhaustPile = cards },
            _ => throw new ArgumentOutOfRangeException(nameof(zone))
        };

    private static IEnumerable<long[]> ChooseCombinations(long[] source, int count)
    {
        if (count < 0 || count > source.Length)
        {
            yield break;
        }

        if (count == 0)
        {
            yield return Array.Empty<long>();
            yield break;
        }

        var buffer = new long[count];
        foreach (var result in ChooseCombinationsCore(source, 0, 0, buffer))
        {
            yield return result;
        }
    }

    private static IEnumerable<long[]> ChooseCombinationsCore(
        long[] source,
        int start,
        int depth,
        long[] buffer)
    {
        if (depth == buffer.Length)
        {
            yield return (long[])buffer.Clone();
            yield break;
        }

        var remaining = buffer.Length - depth;
        for (var index = start; index <= source.Length - remaining; index++)
        {
            buffer[depth] = source[index];
            foreach (var result in ChooseCombinationsCore(
                         source,
                         index + 1,
                         depth + 1,
                         buffer))
            {
                yield return result;
            }
        }
    }

    private static bool IsInnate(CombatCardInstance card)
    {
        var definition = PrototypeContent.Card(card.CardId);
        return definition.Innate
            || (card.UpgradeLevel > 0 && definition.InnateOnUpgrade);
    }

    private static CombatCardInstance RequireCombatCard(
        CombatState combat,
        long instanceId) =>
        combat.Cards.FirstOrDefault(card => card.InstanceId == instanceId)
        ?? throw new InvalidOperationException($"Combat card instance {instanceId} is missing.");

    private sealed record PrototypeDamageResult(
        CombatState Combat,
        int DamageDealt,
        bool Defeated);

    private static PrototypeDamageResult DamageEnemy(
        CombatState combat,
        int enemyId,
        int damage)
    {
        var enemies = combat.Enemies.Select(enemy => enemy.Fork()).ToArray();
        var index = Array.FindIndex(enemies, enemy => enemy.InstanceId == enemyId);
        if (index < 0)
        {
            throw new InvalidOperationException($"Enemy {enemyId} is missing.");
        }

        var enemy = enemies[index];
        if (enemy.Hp <= 0)
        {
            return new PrototypeDamageResult(combat, 0, false);
        }

        var absorbed = Math.Min(enemy.Block, Math.Max(0, damage));
        var hpDamage = Math.Max(0, damage - absorbed);
        var nextHp = Math.Max(0, enemy.Hp - hpDamage);
        var damageDealt = enemy.Hp - nextHp;

        enemies[index] = enemy with
        {
            Block = enemy.Block - absorbed,
            Hp = nextHp
        };

        return new PrototypeDamageResult(
            combat with { Enemies = enemies },
            damageDealt,
            Defeated: enemy.Hp > 0 && nextHp == 0);
    }

    private static CombatState MultiplyEnemyStatus(
        CombatState combat,
        int enemyId,
        string statusId,
        int multiplier)
    {
        _ = PrototypeContent.Status(statusId);
        if (multiplier < 0)
        {
            throw new InvalidOperationException("Status multiplier cannot be negative.");
        }

        var enemies = combat.Enemies.Select(enemy => enemy.Fork()).ToArray();
        var index = Array.FindIndex(enemies, enemy => enemy.InstanceId == enemyId);
        if (index < 0)
        {
            throw new InvalidOperationException($"Enemy {enemyId} is missing.");
        }

        var enemy = enemies[index];
        if (enemy.Hp <= 0)
        {
            return combat;
        }

        var statuses = new Dictionary<string, int>(enemy.Statuses, StringComparer.Ordinal);
        var current = statuses.GetValueOrDefault(statusId);
        var next = current * multiplier;
        if (next > 0)
        {
            statuses[statusId] = next;
        }
        else
        {
            statuses.Remove(statusId);
        }

        enemies[index] = enemy with { Statuses = statuses };
        return combat with { Enemies = enemies };
    }

    private static CombatState ApplyEnemyStatus(
        CombatState combat,
        int enemyId,
        string statusId,
        int amount)
    {
        var statusDefinition =
            PrototypeContent.Status(statusId);
        var enemies = combat.Enemies
            .Select(enemy => enemy.Fork())
            .ToArray();
        var index = Array.FindIndex(
            enemies,
            enemy => enemy.InstanceId == enemyId);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Enemy {enemyId} is missing.");
        }

        var enemy = enemies[index];
        if (enemy.Hp <= 0)
        {
            return combat;
        }

        if (amount > 0 && statusDefinition.IsDebuff)
        {
            var blocked =
                TryBlockIncomingEnemyDebuff(enemy);
            enemy = blocked.Enemy;
            if (blocked.Blocked)
            {
                enemies[index] = enemy;
                return combat with { Enemies = enemies };
            }
        }

        var statuses = new Dictionary<string, int>(
            enemy.Statuses,
            StringComparer.Ordinal);
        statuses[statusId] =
            statuses.GetValueOrDefault(statusId)
            + amount;
        enemies[index] = enemy with
        {
            Statuses = statuses
        };
        return combat with { Enemies = enemies };
    }

    private sealed record PrototypeEnemyDebuffBlockResult(
        EnemyCombatState Enemy,
        bool Blocked);

    private static PrototypeEnemyDebuffBlockResult
        TryBlockIncomingEnemyDebuff(
            EnemyCombatState enemy)
    {
        var candidate = enemy.PowerStates
            .Where(power =>
                power.Stacks > 0
                && PrototypeContent.Power(
                    power.PowerId)
                    .BlocksNextDebuff)
            .OrderBy(power => power.ApplicationOrder)
            .FirstOrDefault();
        if (candidate is null)
        {
            return new PrototypeEnemyDebuffBlockResult(
                enemy,
                false);
        }

        var powers = enemy.PowerStates
            .Select(power => power.Fork())
            .ToList();
        var index = powers.FindIndex(power =>
            power.ApplicationOrder
                == candidate.ApplicationOrder);
        if (index < 0)
        {
            throw new InvalidOperationException(
                "Enemy debuff-blocking power disappeared before consumption.");
        }

        if (powers[index].Stacks <= 1)
        {
            powers.RemoveAt(index);
        }
        else
        {
            powers[index] = powers[index] with
            {
                Stacks = powers[index].Stacks - 1
            };
        }

        return new PrototypeEnemyDebuffBlockResult(
            enemy with { Powers = powers.ToArray() },
            true);
    }

    private sealed record PrototypeDrawCardsResult(
        PlayerState Player,
        CombatState Combat,
        long[] DrawnCardInstanceIds);

    private static PrototypeDrawCardsResult DrawCards(
        PlayerState player,
        CombatState combat,
        int count,
        RngBundle rng,
        bool fromHandDraw,
        int eventDepth = 0)
    {
        const int maxHandSize = 10;
        var drawnCardInstanceIds = new List<long>();

        for (var drawNumber = 0; drawNumber < count; drawNumber++)
        {
            if (!fromHandDraw
                && combat.PlayerPowers.Any(power =>
                    PrototypeContent.Power(power.PowerId)
                        .PreventsAdditionalDraw))
            {
                break;
            }

            if (combat.Hand.Length >= maxHandSize)
            {
                break;
            }

            var draw = combat.DrawPile.ToList();
            var discard = combat.DiscardPile.ToList();

            if (draw.Count == 0)
            {
                if (discard.Count == 0)
                {
                    break;
                }

                var recycled = discard.ToArray();
                discard.Clear();
                PrototypeRng.Shuffle(rng, "combat", recycled);
                draw.AddRange(recycled);
            }

            var index = draw.Count - 1;
            var cardInstanceId = draw[index];
            draw.RemoveAt(index);

            combat = combat with
            {
                Hand = combat.Hand.Append(cardInstanceId).ToArray(),
                DrawPile = draw.ToArray(),
                DiscardPile = discard.ToArray()
            };
            drawnCardInstanceIds.Add(cardInstanceId);

            var card = RequireCombatCard(combat, cardInstanceId);
            var dispatched = DispatchCombatEvent(
                player,
                combat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.CardDrawn,
                    SourceCardInstanceId: cardInstanceId,
                    CardId: card.CardId,
                    FromHandDraw: fromHandDraw),
                rng,
                eventDepth);
            player = dispatched.Player;
            combat = dispatched.Combat;
        }

        return new PrototypeDrawCardsResult(
            player,
            combat,
            drawnCardInstanceIds.ToArray());
    }

    private static void ValidateTarget(
        PrototypeCardTarget targetKind,
        int? targetEnemyId,
        CombatState combat)
    {
        if (targetKind == PrototypeCardTarget.None)
        {
            if (targetEnemyId is not null)
            {
                throw new InvalidOperationException("This action does not accept an enemy target.");
            }

            return;
        }

        if (targetEnemyId is null)
        {
            throw new InvalidOperationException("This action requires an enemy target.");
        }

        if (!combat.Enemies.Any(
            enemy => enemy.InstanceId == targetEnemyId.Value && enemy.Hp > 0))
        {
            throw new InvalidOperationException($"Enemy {targetEnemyId.Value} is not a live target.");
        }
    }

    private static bool AllEnemiesDefeated(CombatState combat) =>
        combat.Enemies.All(enemy => enemy.Hp <= 0);
}
