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

        var drawPile = state.Player.Deck.Select(card => card.InstanceId).ToArray();
        PrototypeRng.Shuffle(state.Rng, "combat", drawPile);

        var combat = new CombatState(
            Turn: 1,
            Energy: EnergyPerTurn(state.Player),
            PlayerBlock: 0,
            Hand: Array.Empty<long>(),
            DrawPile: drawPile,
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies: enemies);

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
            var card = RequireCard(state.Player, instanceId);
            var definition = PrototypeContent.Card(card.CardId);
            if (definition.Cost > combat.Energy)
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

        var card = RequireCard(state.Player, payload.CardInstanceId);
        var definition = PrototypeContent.Card(card.CardId);
        if (definition.Cost > combat.Energy)
        {
            throw new InvalidOperationException($"Card {payload.CardInstanceId} is unaffordable.");
        }

        ValidateTarget(definition.Target, payload.TargetEnemyId, combat);

        combat = combat with
        {
            Energy = combat.Energy - definition.Cost,
            Hand = combat.Hand.Where(id => id != payload.CardInstanceId).ToArray()
        };

        var operations = new Queue<PrototypeQueuedOperation>();
        foreach (var effect in definition.Effects)
        {
            operations.Enqueue(new PrototypeQueuedOperation(
                effect.Kind,
                effect.AmountAtUpgrade(card.UpgradeLevel),
                payload.TargetEnemyId,
                effect.StatusId,
                effect.Selection));
        }

        var resolved = ResolveOperations(
            state.Player,
            combat,
            operations,
            state.Rng,
            payload.CardInstanceId);
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
            operations.Enqueue(new PrototypeQueuedOperation(
                effect.Kind,
                effect.Amount,
                payload.TargetEnemyId,
                effect.StatusId,
                effect.Selection));
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

        combat = combat with
        {
            DiscardPile = combat.DiscardPile.Concat(combat.Hand).ToArray(),
            Hand = Array.Empty<long>()
        };
        state = state with { World = world with { Combat = combat } };

        foreach (var stage in PrototypeContent.Rules.EnemyTurnSequence)
        {
            state = ResolveTurnStage(state, stage);

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

    private static RunState ResolveTurnStage(RunState state, PrototypeTurnStage stage)
    {
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException("Combat phase has no combat state.");
        var player = state.Player;

        if (stage is PrototypeTurnStage.EnemyTurnStart or PrototypeTurnStage.EnemyTurnEnd)
        {
            combat = ResolveEnemyStatusStage(combat, stage);
        }

        if (stage == PrototypeTurnStage.EnemyAction)
        {
            var result = ResolveEnemyActions(player, combat, world.Act);
            player = result.Player;
            combat = result.Combat;
        }

        if (stage == PrototypeTurnStage.PlayerTurnStart)
        {
            combat = combat with
            {
                Turn = combat.Turn + 1,
                Energy = EnergyPerTurn(player),
                PlayerBlock = 0
            };
            combat = DrawCards(combat, PrototypeContent.Rules.HandSize, state.Rng);
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
            var damage = move.Damage + ((act - 1) * move.DamagePerAct);

            foreach (var status in enemy.Statuses)
            {
                var statusDefinition = PrototypeContent.Status(status.Key);
                damage = (damage * statusDefinition.OutgoingDamageNumerator)
                    / statusDefinition.OutgoingDamageDenominator;
            }

            var absorbed = Math.Min(block, Math.Max(0, damage));
            block -= absorbed;
            hp = Math.Max(0, hp - Math.Max(0, damage - absorbed));

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

    private static (PlayerState Player, CombatState Combat) ResolveOperations(
        PlayerState player,
        CombatState combat,
        Queue<PrototypeQueuedOperation> operations,
        RngBundle rng,
        long? sourceCardInstanceId = null)
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

                    combat = DamageEnemy(combat, operation.TargetEnemyId.Value, operation.Amount);
                    break;

                case PrototypeCombatEffectKind.GainPlayerBlock:
                    combat = combat with
                    {
                        PlayerBlock = combat.PlayerBlock + operation.Amount
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
                            Selection: effective,
                            CandidateCardInstanceIds: (long[])candidates.Clone(),
                            Continuation: operations.ToArray())
                    };
                    return (player, combat);
                }

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (sourceCardInstanceId is not null)
        {
            combat = combat with
            {
                DiscardPile = combat.DiscardPile.Append(sourceCardInstanceId.Value).ToArray(),
                PendingChoice = null
            };
        }

        return (player, combat);
    }

    private static CombatState DamageEnemy(CombatState combat, int enemyId, int damage)
    {
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

                var absorbed = Math.Min(enemy.Block, Math.Max(0, damage));
                return enemy with
                {
                    Block = enemy.Block - absorbed,
                    Hp = Math.Max(0, enemy.Hp - Math.Max(0, damage - absorbed))
                };
            })
            .ToArray();

        if (!found)
        {
            throw new InvalidOperationException($"Enemy {enemyId} is missing.");
        }

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
