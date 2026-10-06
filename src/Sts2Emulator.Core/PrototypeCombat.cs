namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private sealed record PrototypeEventSubscriber(
        long ApplicationOrder,
        PrototypeCombatEffectSpec[] Effects,
        int PowerStacks,
        PrototypeEffectSourceKind SourceKind,
        int? RelicStateIndex = null,
        int? RelicTriggerIndex = null,
        int EveryNth = 1);

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
            .Where(card => PrototypeContent.Card(card.CardId).Innate)
            .Select(card => card.InstanceId)
            .ToArray();
        var drawPile = combatCards
            .Where(card => !PrototypeContent.Card(card.CardId).Innate)
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
            if (definition.Unplayable
                || !definition.Cost.IsPlayable(
                    combat.Energy,
                    card.UpgradeLevel,
                    ResolveCardCostReductionCount(definition.Cost, combat))
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

        if (!definition.Cost.IsPlayable(
                combat.Energy,
                card.UpgradeLevel,
                ResolveCardCostReductionCount(definition.Cost, combat)))
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
        var energySpent = definition.Cost.ResolveEnergySpent(
            combat.Energy,
            card.UpgradeLevel,
            ResolveCardCostReductionCount(definition.Cost, combat));

        combat = combat with
        {
            Energy = combat.Energy - energySpent,
            Hand = combat.Hand.Where(id => id != payload.CardInstanceId).ToArray()
        };

        var powerApplicationOrderCeiling =
            combat.NextPowerApplicationOrder - 1;

        var operations = new Queue<PrototypeQueuedOperation>();
        foreach (var effect in definition.Effects)
        {
            EnqueueEffectOperations(
                operations,
                effect,
                card.UpgradeLevel,
                energySpent,
                payload.TargetEnemyId,
                combat,
                sourceKind: PrototypeEffectSourceKind.Card,
                isPoweredAttack: definition.Type == PrototypeCardType.Attack);
        }

        var sourceDestination = definition.ExhaustOnUse
            ? PrototypeCardZone.ExhaustPile
            : PrototypeCardZone.DiscardPile;

        var completionEvents = new[]
        {
            new PrototypeCombatEvent(
                PrototypeCombatEventKind.CardPlayed,
                SourceCardInstanceId: payload.CardInstanceId,
                CardId: card.CardId,
                PowerApplicationOrderCeiling:
                    powerApplicationOrderCeiling)
        };

        var resolved = ResolveOperations(
            state.Player,
            combat,
            operations,
            state.Rng,
            payload.CardInstanceId,
            sourceDestination,
            completionEvents);
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

        foreach (var automaticStep in PrototypeContent.Rules.EndTurnPipeline)
        {
            state = ResolveAutomaticStep(state, automaticStep);

            if (state.Player.Hp <= 0)
            {
                return EndRun(state, "defeat");
            }

            var currentCombat = RequireWorld(state).Combat
                ?? throw new InvalidOperationException("Combat unexpectedly disappeared.");

            if (AllEnemiesDefeated(currentCombat))
            {
                return EnterReward(state);
            }
        }

        return state;
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
                var ethereal = combat.Hand
                    .Where(instanceId =>
                        PrototypeContent.Card(
                            RequireCombatCard(combat, instanceId).CardId).Ethereal)
                    .ToArray();
                var etherealSet = ethereal.ToHashSet();
                var retained = combat.Hand
                    .Where(instanceId =>
                        !etherealSet.Contains(instanceId)
                        && PrototypeContent.Card(
                            RequireCombatCard(combat, instanceId).CardId).Retain)
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
                combat = combat with { PlayerBlock = 0 };
                break;

            case PrototypeAutomaticStepKind.RefreshPlayerEnergy:
                combat = combat with { Energy = EnergyPerTurn(player) };
                break;

            case PrototypeAutomaticStepKind.DrawPlayerHand:
            {
                var drawn = DrawCards(
                    player,
                    combat,
                    PrototypeContent.Rules.HandSize,
                    state.Rng,
                    fromHandDraw: true);
                player = drawn.Player;
                combat = drawn.Combat;
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
        bool isPoweredAttack = false)
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
                    effect.Selection,
                    effect.CardId,
                    effect.PowerId,
                    effect.GeneratedCardUpgradeLevel
                        + (effect.GeneratedCardUpgradePerSourceUpgrade * upgradeLevel),
                    TargetMode: effect.Target,
                    SourceKind: sourceKind,
                    IsPoweredAttack: isPoweredAttack && effect.Kind == PrototypeCombatEffectKind.DamageEnemy,
                    Condition: effect.Condition));
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
        int eventDepth = 0)
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

                    var damageAmount = operation.IsPoweredAttack
                        ? ModifyIncomingAttackDamage(
                            combat,
                            targetEnemyId.Value,
                            operation.Amount)
                        : operation.Amount;

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
                                : (PrototypeCombatEvent[])completionEvents.Clone())
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

                    _ = PrototypeContent.Card(operation.CardId);
                    for (var index = 0; index < operation.Amount; index++)
                    {
                        var instance = new CombatCardInstance(
                            InstanceId: combat.NextCardInstanceId,
                            PersistentCardInstanceId: null,
                            CardId: operation.CardId,
                            UpgradeLevel: operation.GeneratedCardUpgradeLevel,
                            IsTemporary: true,
                            State: PrototypeJson.EmptyObject());

                        combat = combat with
                        {
                            NextCardInstanceId = combat.NextCardInstanceId + 1,
                            Cards = combat.Cards.Append(instance).ToArray(),
                            Hand = combat.Hand.Append(instance.InstanceId).ToArray()
                        };
                    }

                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (sourceCardInstanceId is not null)
        {
            var destination = GetZone(combat, sourceCardDestination);
            combat = SetZone(
                combat,
                sourceCardDestination,
                destination.Append(sourceCardInstanceId.Value).ToArray());
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

        return (player, combat);
    }

    private static int PlayerBlockBonus(CombatState combat) =>
        combat.PlayerPowers.Sum(power =>
            PrototypeContent.Power(power.PowerId).BlockBonusPerStack * power.Stacks);

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

    private static CombatState ApplyEnemyPower(
        CombatState combat,
        int enemyId,
        string powerId,
        int stacks)
    {
        var definition = PrototypeContent.Power(powerId);
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
        var powerIndex = powers.FindIndex(power =>
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
        var subscribers = new List<PrototypeEventSubscriber>();

        foreach (var power in combat.PlayerPowers)
        {
            var definition = PrototypeContent.Power(power.PowerId);
            subscribers.AddRange(
                definition.Triggers
                    .Where(trigger =>
                        trigger.EventKind == combatEvent.Kind
                        && !trigger.RequiresOwnerTarget
                        && (combatEvent.PowerApplicationOrderCeiling is null
                            || power.ApplicationOrder
                                <= combatEvent.PowerApplicationOrderCeiling.Value))
                    .Select(trigger => new PrototypeEventSubscriber(
                        power.ApplicationOrder,
                        trigger.Effects,
                        power.Stacks,
                        PrototypeEffectSourceKind.Power)));
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
                            && (combatEvent.PowerApplicationOrderCeiling is null
                                || power.ApplicationOrder
                                    <= combatEvent.PowerApplicationOrderCeiling.Value)
                            && (!trigger.RequiresOwnerTarget
                                || combatEvent.TargetEnemyId == enemy.InstanceId)
                            && (enemy.Hp > 0
                                || (trigger.RequiresOwnerTarget
                                    && combatEvent.TargetEnemyId == enemy.InstanceId)))
                        .Select(trigger => new PrototypeEventSubscriber(
                            power.ApplicationOrder,
                            trigger.Effects,
                            power.Stacks,
                            PrototypeEffectSourceKind.Power)));
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

                subscribers.Add(new PrototypeEventSubscriber(
                    relic.ApplicationOrder,
                    trigger.Effects,
                    PowerStacks: 0,
                    SourceKind: PrototypeEffectSourceKind.Relic,
                    RelicStateIndex: relicIndex,
                    RelicTriggerIndex: triggerIndex,
                    EveryNth: trigger.EveryNth));
            }
        }

        foreach (var subscriber in subscribers.OrderBy(item => item.ApplicationOrder))
        {
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
                    sourceKind: subscriber.SourceKind);
            }

            var resolved = ResolveOperations(
                player,
                combat,
                operations,
                rng,
                eventDepth: eventDepth);
            player = resolved.Player;
            combat = resolved.Combat;

            if (combat.PendingChoice is not null)
            {
                throw new NotSupportedException(
                    "Automatic combat-event triggers that request player choices are not supported yet.");
            }
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

        return (player, combat);
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
                    .Where(card => PrototypeContent.Card(card.CardId).Sly)
                    .Select(card => card.InstanceId)
                    .ToArray()
                : Array.Empty<long>();

        combat = ApplyCardSelection(combat, pending.Selection, selected);
        combat = combat with { PendingChoice = null };

        var player = state.Player;
        if (pending.Selection.SourceZone == PrototypeCardZone.Hand
            && pending.Selection.Resolution == PrototypeCardSelectionResolutionKind.MoveToDiscard)
        {
            foreach (var cardId in selected)
            {
                var discardedCard = RequireCombatCard(combat, cardId);
                var dispatched = DispatchCombatEvent(
                    player,
                    combat,
                    new PrototypeCombatEvent(
                        PrototypeCombatEventKind.CardDiscarded,
                        SourceCardInstanceId: cardId,
                        CardId: discardedCard.CardId),
                    state.Rng);
                player = dispatched.Player;
                combat = dispatched.Combat;
            }

            foreach (var slyCardId in slyCards)
            {
                var autoPlayed = AutoPlaySlyCard(
                    player,
                    combat,
                    slyCardId,
                    state.Rng);
                player = autoPlayed.Player;
                combat = autoPlayed.Combat;

                if (AllEnemiesDefeated(combat))
                {
                    break;
                }
            }
        }

        var operations = new Queue<PrototypeQueuedOperation>(pending.Continuation);
        var resolved = ResolveOperations(
            player,
            combat,
            operations,
            state.Rng,
            pending.SourceCardInstanceId,
            pending.SourceCardDestination,
            pending.CompletionEvents);

        state = state with
        {
            Player = resolved.Player,
            World = world with { Combat = resolved.Combat }
        };

        return AllEnemiesDefeated(resolved.Combat)
            ? EnterReward(state)
            : state;
    }

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
        if (!definition.Sly)
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

        var powerApplicationOrderCeiling =
            combat.NextPowerApplicationOrder - 1;

        var operations = new Queue<PrototypeQueuedOperation>();
        foreach (var effect in definition.Effects)
        {
            EnqueueEffectOperations(
                operations,
                effect,
                card.UpgradeLevel,
                energySpent: 0,
                actionTargetEnemyId: null,
                combat,
                sourceKind: PrototypeEffectSourceKind.Card,
                isPoweredAttack: definition.Type == PrototypeCardType.Attack);
        }

        var sourceDestination = definition.ExhaustOnUse
            ? PrototypeCardZone.ExhaustPile
            : PrototypeCardZone.DiscardPile;

        return ResolveOperations(
            player,
            combat,
            operations,
            rng,
            cardInstanceId,
            sourceDestination,
            [
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.CardPlayed,
                    SourceCardInstanceId: cardInstanceId,
                    CardId: card.CardId,
                    PowerApplicationOrderCeiling:
                        powerApplicationOrderCeiling)
            ]);
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

    private static (PlayerState Player, CombatState Combat) DrawCards(
        PlayerState player,
        CombatState combat,
        int count,
        RngBundle rng,
        bool fromHandDraw,
        int eventDepth = 0)
    {
        const int maxHandSize = 10;

        for (var drawNumber = 0; drawNumber < count; drawNumber++)
        {
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

        return (player, combat);
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
