namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static RunState StartCombat(RunState state, PrototypeRoomType roomType)
    {
        var world = RequireWorld(state);
        var encounters = PrototypeContent.Encounters
            .Where(encounter => encounter.RoomType == roomType)
            .ToArray();

        if (encounters.Length == 0)
        {
            throw new InvalidOperationException($"No prototype encounter exists for {roomType}.");
        }

        var encounter = encounters[
            PrototypeRng.NextInt(state.Rng, "combat", encounters.Length)];

        var enemies = encounter.EnemyIds
            .Select((enemyId, index) =>
            {
                var definition = PrototypeContent.Enemy(enemyId);
                var hp = definition.MaxHp + ((world.Act - 1) * definition.HpPerAct);
                return new EnemyCombatState(
                    InstanceId: index + 1,
                    EnemyId: enemyId,
                    Hp: hp,
                    Block: 0,
                    MoveIndex: 0,
                    Statuses: new Dictionary<string, int>(StringComparer.Ordinal));
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

        var drawPile = combatCards.Select(card => card.InstanceId).ToArray();
        PrototypeRng.Shuffle(state.Rng, "combat", drawPile);

        var combat = new CombatState(
            Turn: 1,
            Energy: EnergyPerTurn(state.Player),
            PlayerBlock: 0,
            Hand: Array.Empty<long>(),
            DrawPile: drawPile,
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies: enemies,
            NextCardInstanceId: nextCombatCardId,
            Cards: combatCards,
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: 1);

        combat = DrawCards(
            combat,
            PrototypeContent.Rules.HandSize + FirstTurnDrawBonus(state.Player),
            state.Rng);

        world = world with { Combat = combat };

        return state with
        {
            World = world,
            Phase = RunPhase.Combat
        };
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
            if (!definition.Cost.IsPlayable(combat.Energy))
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
        if (!definition.Cost.IsPlayable(combat.Energy))
        {
            throw new InvalidOperationException($"Card {payload.CardInstanceId} is unaffordable.");
        }

        ValidateTarget(definition.Target, payload.TargetEnemyId, combat);
        var energySpent = definition.Cost.ResolveEnergySpent(combat.Energy);

        combat = combat with
        {
            Energy = combat.Energy - energySpent,
            Hand = combat.Hand.Where(id => id != payload.CardInstanceId).ToArray()
        };

        var operations = new Queue<PrototypeQueuedOperation>();
        foreach (var effect in definition.Effects)
        {
            EnqueueEffectOperations(
                operations,
                effect,
                card.UpgradeLevel,
                energySpent,
                payload.TargetEnemyId,
                combat);
        }

        var sourceDestination = definition.ExhaustOnUse
            ? PrototypeCardZone.ExhaustPile
            : PrototypeCardZone.DiscardPile;

        var completionEvents = new[]
        {
            new PrototypeCombatEvent(
                PrototypeCombatEventKind.CardPlayed,
                SourceCardInstanceId: payload.CardInstanceId,
                CardId: card.CardId)
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
        var definition = PrototypeContent.Potion(potion.PotionId);
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException("Combat phase has no combat state.");

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
                combat);
        }

        var resolved = ResolveOperations(state.Player, combat, operations, state.Rng);
        var slots = (PotionInstance?[])resolved.Player.PotionSlots.Clone();
        slots[payload.Slot] = null;

        state = state with
        {
            Player = resolved.Player with { PotionSlots = slots },
            World = world with { Combat = resolved.Combat }
        };

        return AllEnemiesDefeated(resolved.Combat)
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
                combat = combat with
                {
                    DiscardPile = combat.DiscardPile.Concat(combat.Hand).ToArray(),
                    Hand = Array.Empty<long>()
                };
                break;

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
                combat = combat with { Turn = combat.Turn + 1 };
                break;

            case PrototypeAutomaticStepKind.ResetPlayerBlock:
                combat = combat with { PlayerBlock = 0 };
                break;

            case PrototypeAutomaticStepKind.RefreshPlayerEnergy:
                combat = combat with { Energy = EnergyPerTurn(player) };
                break;

            case PrototypeAutomaticStepKind.DrawPlayerHand:
                combat = DrawCards(combat, PrototypeContent.Rules.HandSize, state.Rng);
                break;

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

                    if (hp <= 0)
                    {
                        break;
                    }
                }

                if (hp <= 0)
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
        int powerStacks = 0)
    {
        var repetitions = effect.RepetitionsAt(upgradeLevel, energySpent)
            + (effect.RepetitionsPerPowerStack * powerStacks);
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
            _ => throw new ArgumentOutOfRangeException()
        };

        if (targets.Length == 0)
        {
            targets = new int?[] { null };
        }

        for (var repetition = 0; repetition < repetitions; repetition++)
        {
            foreach (var targetEnemyId in targets)
            {
                operations.Enqueue(new PrototypeQueuedOperation(
                    effect.Kind,
                    effect.AmountAt(upgradeLevel, energySpent)
                        + (effect.AmountPerPowerStack * powerStacks),
                    targetEnemyId,
                    effect.StatusId,
                    effect.Selection,
                    effect.CardId,
                    effect.PowerId));
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
            switch (operation.Kind)
            {
                case PrototypeCombatEffectKind.DamageEnemy:
                    if (operation.TargetEnemyId is null)
                    {
                        throw new InvalidOperationException("Damage operation requires an enemy target.");
                    }

                    var damageResult = DamageEnemy(
                        combat,
                        operation.TargetEnemyId.Value,
                        operation.Amount);
                    combat = damageResult.Combat;

                    if (damageResult.DamageDealt > 0)
                    {
                        var damaged = DispatchCombatEvent(
                            player,
                            combat,
                            new PrototypeCombatEvent(
                                PrototypeCombatEventKind.EnemyDamaged,
                                TargetEnemyId: operation.TargetEnemyId.Value,
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
                                TargetEnemyId: operation.TargetEnemyId.Value,
                                Amount: damageResult.DamageDealt),
                            rng,
                            eventDepth + 1);
                        player = defeated.Player;
                        combat = defeated.Combat;
                    }

                    break;

                case PrototypeCombatEffectKind.GainPlayerBlock:
                    combat = combat with
                    {
                        PlayerBlock = combat.PlayerBlock
                            + operation.Amount
                            + PlayerBlockBonus(combat)
                    };
                    break;

                case PrototypeCombatEffectKind.DrawCards:
                    combat = DrawCards(combat, operation.Amount, rng);
                    break;

                case PrototypeCombatEffectKind.ApplyEnemyStatus:
                    if (operation.TargetEnemyId is null || operation.StatusId is null)
                    {
                        throw new InvalidOperationException("Status operation requires target and status ID.");
                    }

                    combat = ApplyEnemyStatus(
                        combat,
                        operation.TargetEnemyId.Value,
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
                            UpgradeLevel: 0,
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

    private static CombatState ApplyPlayerPower(
        CombatState combat,
        string powerId,
        int stacks)
    {
        _ = PrototypeContent.Power(powerId);
        var powers = combat.PlayerPowers.ToList();
        var index = powers.FindIndex(power =>
            StringComparer.Ordinal.Equals(power.PowerId, powerId));

        if (index >= 0)
        {
            var nextStacks = powers[index].Stacks + stacks;
            if (nextStacks <= 0)
            {
                powers.RemoveAt(index);
            }
            else
            {
                powers[index] = powers[index] with { Stacks = nextStacks };
            }

            return combat with { PlayerPowers = powers.ToArray() };
        }

        if (stacks <= 0)
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
        var subscribers = combat.PlayerPowers
            .OrderBy(power => power.ApplicationOrder)
            .SelectMany(power =>
            {
                var definition = PrototypeContent.Power(power.PowerId);
                return definition.Triggers
                    .Where(trigger => trigger.EventKind == combatEvent.Kind)
                    .Select(trigger => (Power: power, Trigger: trigger));
            })
            .ToArray();

        foreach (var subscriber in subscribers)
        {
            var operations = new Queue<PrototypeQueuedOperation>();
            foreach (var effect in subscriber.Trigger.Effects)
            {
                EnqueueEffectOperations(
                    operations,
                    effect,
                    upgradeLevel: 0,
                    energySpent: 0,
                    actionTargetEnemyId: combatEvent.TargetEnemyId,
                    combat: combat,
                    powerStacks: subscriber.Power.Stacks);
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

        combat = ApplyCardSelection(combat, pending.Selection, selected);
        combat = combat with { PendingChoice = null };

        var operations = new Queue<PrototypeQueuedOperation>(pending.Continuation);
        var resolved = ResolveOperations(
            state.Player,
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

    private static CombatState DrawCards(CombatState combat, int count, RngBundle rng)
    {
        var hand = combat.Hand.ToList();
        var draw = combat.DrawPile.ToList();
        var discard = combat.DiscardPile.ToList();

        for (var drawNumber = 0; drawNumber < count; drawNumber++)
        {
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
            hand.Add(draw[index]);
            draw.RemoveAt(index);
        }

        return combat with
        {
            Hand = hand.ToArray(),
            DrawPile = draw.ToArray(),
            DiscardPile = discard.ToArray()
        };
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
