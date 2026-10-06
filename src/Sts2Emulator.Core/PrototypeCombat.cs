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
        var enemies = encounter.EnemyIds
            .Select((enemyId, index) =>
            {
                var definition = PrototypeContent.Enemy(enemyId);
                var hp = definition.MaxHp + ((world.Act - 1) * definition.HpPerAct);
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
                    EnemyId: enemyId,
                    Hp: hp,
                    Block: 0,
                    MoveIndex: 0,
                    Statuses: new Dictionary<string, int>(StringComparer.Ordinal),
                    Powers: powers);
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
                State: card.PersistentState.Clone()))
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
                    state.Rng);
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
                var result = ResolveEnemyActions(player, combat, world.Act);
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
                        CardsDiscardedThisTurn = 0
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

        return combat with { Enemies = enemies };
    }

    private static (PlayerState Player, CombatState Combat) ResolveEnemyActions(
        PlayerState player,
        CombatState combat,
        int act)
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

            var move = definition.Moves[enemy.MoveIndex % definition.Moves.Length];

            foreach (var effect in move.Effects)
            {
                var amount = effect.Amount + ((act - 1) * effect.AmountPerAct);
                for (var repetition = 0; repetition < effect.Repetitions; repetition++)
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

                            var damage = amount;
                            foreach (var status in enemy.Statuses)
                            {
                                var statusDefinition = PrototypeContent.Status(status.Key);
                                damage = (damage * statusDefinition.OutgoingDamageNumerator)
                                    / statusDefinition.OutgoingDamageDenominator;
                            }

                            var absorbed = Math.Min(block, Math.Max(0, damage));
                            block -= absorbed;
                            hp = Math.Max(0, hp - Math.Max(0, damage - absorbed));
                            break;
                        }

                        case PrototypeEnemyEffectKind.GainBlock:
                            enemy = enemy with { Block = enemy.Block + Math.Max(0, amount) };
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

            enemies[index] = enemy with { MoveIndex = enemy.MoveIndex + 1 };
            if (hp <= 0)
            {
                break;
            }
        }

        return (
            player with { Hp = hp },
            combat with
            {
                PlayerBlock = block,
                Enemies = enemies
            });
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
                            effect.SelectedCardPower.AmountAt(upgradeLevel)),
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
                    var modifiedAmount = operation.Amount
                        + (operation.SourceKind == PrototypeEffectSourceKind.Card
                            ? PlayerBlockBonus(combat)
                            : 0);
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
                    var modifiedAmount = operation.Amount
                        + statusTotal
                        + (operation.SourceKind == PrototypeEffectSourceKind.Card
                            ? PlayerBlockBonus(combat)
                            : 0);
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

                    var modifiedAmount = operation.Amount
                        + (operation.SourceKind == PrototypeEffectSourceKind.Card
                            ? PlayerBlockBonus(combat)
                            : 0);
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
            if (!removeSourceCardOnCompletion)
            {
                var destination = GetZone(combat, sourceCardDestination);
                combat = SetZone(
                    combat,
                    sourceCardDestination,
                    destination.Append(sourceCardInstanceId.Value).ToArray());
            }

            combat = combat with { PendingChoice = null };
        }

        foreach (var completionEvent in completionEvents ?? Array.Empty<PrototypeCombatEvent>())
        {
            var dispatched = DispatchCombatEvent(
                player,
                combat,
                completionEvent,
                rng,
                eventDepth + 1);
            player = dispatched.Player;
            combat = dispatched.Combat;
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
                ReplayCount: snapshot.ReplayCount);

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

        var playCount = 1
            + Math.Max(0, card.ReplayCount)
            + modifiers.Sum(power =>
                PrototypeContent.Power(power.PowerId).AdditionalPlayCount);

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

        var matching = card.KeywordOverrides?
            .Where(item => item.Keyword == keyword)
            .ToArray();
        return matching is { Length: > 0 }
            ? matching[^1].Enabled
            : baseValue;
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

                var type = PrototypeContent.Card(combatEvent.CardId).Type;
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

        var modified = damage + additive;
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

        return modified + conditionalBonus;
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

    private static CombatState ApplyPlayerPower(
        CombatState combat,
        string powerId,
        int stacks)
    {
        var definition = PrototypeContent.Power(powerId);
        if (definition.IsInstanced)
        {
            if (definition.RequiresCardPayload)
            {
                throw new InvalidOperationException(
                    $"Instanced power '{powerId}' requires a card payload.");
            }

            return AddPlayerPowerInstance(combat, powerId, stacks, null);
        }

        var powers = combat.PlayerPowers.ToList();
        var index = powers.FindIndex(power =>
            StringComparer.Ordinal.Equals(power.PowerId, powerId));

        if (index >= 0)
        {
            var nextStacks = powers[index].Stacks + stacks;
            if (nextStacks == 0
                || (!definition.AllowNegative && nextStacks < 0))
            {
                powers.RemoveAt(index);
            }
            else
            {
                powers[index] = powers[index] with { Stacks = nextStacks };
            }

            return combat with { PlayerPowers = powers.ToArray() };
        }

        if (stacks == 0 || (!definition.AllowNegative && stacks < 0))
        {
            return combat;
        }

        powers.Add(new PrototypePowerInstanceState(
            powerId,
            stacks,
            combat.NextPowerApplicationOrder));

        return combat with
        {
            PlayerPowers = powers.ToArray(),
            NextPowerApplicationOrder = combat.NextPowerApplicationOrder + 1
        };
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
            selectedCard.UpgradeLevel,
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
            selectedCard.ReplayCount);

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
        PrototypeCombatCardSnapshot? cardPayload)
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

        return combat with
        {
            PlayerPowers = combat.PlayerPowers.Append(
                new PrototypePowerInstanceState(
                    powerId,
                    stacks,
                    combat.NextPowerApplicationOrder,
                    cardPayload?.Fork())).ToArray(),
            NextPowerApplicationOrder = combat.NextPowerApplicationOrder + 1
        };
    }

    private static CombatState ApplyEnemyPower(
        CombatState combat,
        int enemyId,
        string powerId,
        int stacks)
    {
        var definition = PrototypeContent.Power(powerId);
        if (definition.IsInstanced && definition.RequiresCardPayload)
        {
            throw new InvalidOperationException(
                $"Enemy power '{powerId}' requires a card payload.");
        }

        var enemies = combat.Enemies.Select(enemy => enemy.Fork()).ToArray();
        var enemyIndex = Array.FindIndex(enemies, enemy => enemy.InstanceId == enemyId);
        if (enemyIndex < 0)
        {
            throw new InvalidOperationException($"Enemy {enemyId} is missing.");
        }

        var enemy = enemies[enemyIndex];
        if (enemy.Hp <= 0)
        {
            return combat;
        }

        var powers = enemy.PowerStates.ToList();
        var powerIndex = definition.IsInstanced
            ? -1
            : powers.FindIndex(power =>
                StringComparer.Ordinal.Equals(power.PowerId, powerId));

        if (powerIndex >= 0)
        {
            var nextStacks = powers[powerIndex].Stacks + stacks;
            if (nextStacks == 0
                || (!definition.AllowNegative && nextStacks < 0))
            {
                powers.RemoveAt(powerIndex);
            }
            else
            {
                powers[powerIndex] = powers[powerIndex] with { Stacks = nextStacks };
            }

            enemies[enemyIndex] = enemy with { Powers = powers.ToArray() };
            return combat with { Enemies = enemies };
        }

        if (stacks == 0 || (!definition.AllowNegative && stacks < 0))
        {
            return combat;
        }

        powers.Add(new PrototypePowerInstanceState(
            powerId,
            stacks,
            combat.NextPowerApplicationOrder));
        enemies[enemyIndex] = enemy with { Powers = powers.ToArray() };

        return combat with
        {
            Enemies = enemies,
            NextPowerApplicationOrder = combat.NextPowerApplicationOrder + 1
        };
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
        int eventDepth = 0)
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
        _ = PrototypeContent.Status(statusId);
        var found = false;

        var enemies = combat.Enemies
            .Select(enemy =>
            {
                if (enemy.InstanceId != enemyId)
                {
                    return enemy;
                }

                found = true;
                if (enemy.Hp <= 0)
                {
                    return enemy;
                }

                var statuses = new Dictionary<string, int>(enemy.Statuses, StringComparer.Ordinal);
                statuses[statusId] = statuses.GetValueOrDefault(statusId) + amount;
                return enemy with { Statuses = statuses };
            })
            .ToArray();

        if (!found)
        {
            throw new InvalidOperationException($"Enemy {enemyId} is missing.");
        }

        return combat with { Enemies = enemies };
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
