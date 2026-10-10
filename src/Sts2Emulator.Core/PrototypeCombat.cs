namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static RunState StartCombat(
        RunState state,
        PrototypeRoomType roomType,
        PrototypeEncounterDefinition? forcedEncounter = null)
    {
        var world = RequireWorld(state);
        PrototypeEncounterDefinition encounter;

        if (forcedEncounter is { } scriptedEncounter)
        {
            encounter = scriptedEncounter;
        }
        else if (world.Act == 1
            && world.ActOneRegion == PrototypeActOneRegion.Underdocks)
        {
            var selected = PickUnderdocksEncounter(
                state, world, roomType);
            encounter = selected.Encounter;
            world = selected.World;
        }
        else
        {
        var overgrowthBoss =
            TryPickOvergrowthBossEncounter(
                world,
                roomType);
        if (overgrowthBoss is not null)
        {
            encounter = overgrowthBoss;
        }
        else
        {
            var overgrowthElite =
                TryPickOvergrowthEliteEncounter(
                    state,
                    world,
                    roomType);
            if (overgrowthElite is not null)
            {
                encounter = overgrowthElite.Value.Encounter;
                world = overgrowthElite.Value.World;
            }
            else
            {
                var overgrowthWeak =
                TryPickOvergrowthWeakEncounter(
                    state,
                    world,
                    roomType);
            if (overgrowthWeak is not null)
            {
                encounter =
                    overgrowthWeak.Value.Encounter;
                world = overgrowthWeak.Value.World;
            }
            else
            {
                var overgrowthNormal =
                    TryPickOvergrowthNormalEncounter(
                        state,
                        world,
                        roomType);
            if (overgrowthNormal is not null)
            {
                encounter =
                    overgrowthNormal.Value.Encounter;
                world = overgrowthNormal.Value.World;
            }
            else
            {
                var encounters = PrototypeContent.Encounters
                .Where(candidate =>
                    candidate.RoomType == roomType
                    && world.Act >= candidate.MinAct
                    && world.Act <= candidate.MaxAct
                    && world.Floor >= candidate.MinFloor
                    && world.Floor <= candidate.MaxFloor
                    && candidate.Weight > 0)
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
                    .Where(candidate =>
                        !StringComparer.Ordinal.Equals(
                            candidate.Id,
                            previousEncounter))
                    .ToArray();
                if (withoutImmediateRepeat.Length > 0)
                {
                    encounters = withoutImmediateRepeat;
                }
            }

                    encounter = PickWeightedEncounter(
                        encounters,
                        state.Rng);
                }
            }
        }
        }

        }

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
        var enemySpecs = encounter.ResolveEnemySpecs(
            state.Rng);
        var instanceIdByFormationPosition =
            enemySpecs
                .Select((enemySpec, index) =>
                    new
                    {
                        enemySpec.FormationPosition,
                        InstanceId = index + 1
                    })
                .ToDictionary(
                    item => item.FormationPosition,
                    item => item.InstanceId);
        // Some native encounters coordinate their monsters' initial
        // move-state positions with one encounter-local roll. Keep that
        // property declarative and deterministic within the emulator.
        var openingAiStates = encounter.CyclicOpeningAiStateIds;
        if (openingAiStates is { Length: 0 })
        {
            throw new InvalidOperationException(
                $"Encounter '{encounter.Id}' has an empty cyclic opening AI list.");
        }
        if (openingAiStates is not null
            && enemySpecs.Length > openingAiStates.Length)
        {
            throw new InvalidOperationException(
                $"Encounter '{encounter.Id}' has too few distinct opening AI states.");
        }
        var openingOffset = openingAiStates is null
            ? 0
            : PrototypeRng.NextInt(
                state.Rng, "combat", openingAiStates.Length);
        var enemies = enemySpecs
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
                            power.StacksAt(state.Ascension),
                            nextPowerApplicationOrder++);
                    })
                    .Where(power => power.Stacks > 0)
                    .ToArray();

                return new EnemyCombatState(
                    InstanceId: index + 1,
                    EnemyId: enemySpec.EnemyId,
                    Hp: hp,
                    Block: powers.Sum(power =>
                        power.Stacks * PrototypeContent.Power(power.PowerId)
                            .EnemyStartingBlockPerStack),
                    MoveIndex: 0,
                    Statuses: new Dictionary<string, int>(StringComparer.Ordinal),
                    Powers: powers,
                    FormationPosition:
                        enemySpec.FormationPosition,
                    SlotName: enemySpec.SlotName,
                    AiStateId: openingAiStates is null
                        ? null
                        : openingAiStates[(openingOffset + index)
                            % openingAiStates.Length],
                    NonSummonMovesUntilEligible:
                        definition.NonSummonMovesBeforeEligible,
                    LeaderEnemyInstanceId:
                        enemySpec.LeaderFormationPosition is
                            { } leaderFormationPosition
                            ? instanceIdByFormationPosition[
                                leaderFormationPosition]
                            : null);
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

        var boomingConchAtElite = roomType == PrototypeRoomType.Elite
            && state.Player.Relics.Any(relic =>
                relic.RelicId ==
                    PrototypeNativeOvergrowthEvents.NeowRelicId(
                        "BoomingConch"));
        var combat = new CombatState(
            Turn: 1,
            Energy: EnergyPerTurn(state.Player)
                + (boomingConchAtElite ? 1 : 0),
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
            Potions: combatPotions,
            Act: world.Act,
            Ascension: state.Ascension);

        var openingHandTarget =
            PrototypeContent.Rules.HandSize
            + (boomingConchAtElite ? 2 : 0)
            + FirstTurnDrawBonus(state.Player)
            + state.Player.Relics.Sum(relic =>
                PrototypeContent.Relic(relic.RelicId)
                    .HandDrawBonus);
        var openingDraw = DrawCards(
            state.Player,
            combat,
            Math.Max(0, openingHandTarget - combat.Hand.Length),
            state.Rng,
            fromHandDraw: true);
        state = state with { Player = openingDraw.Player };
        combat = openingDraw.Combat;

        var combatStarted = DispatchCombatEvent(
            state.Player,
            combat,
            new PrototypeCombatEvent(
                PrototypeCombatEventKind.CombatStarted),
            state.Rng,
            allowSuspension: true);
        state = state with { Player = combatStarted.Player };
        combat = CommitEnemyIntents(combatStarted.Combat, state.Rng);

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

    private static PrototypeEncounterDefinition?
        TryPickOvergrowthBossEncounter(
            RunWorldState world,
            PrototypeRoomType roomType)
    {
        if (roomType != PrototypeRoomType.Boss
            || world.Act != 1
            || world.ActOneRegion
                != PrototypeActOneRegion.Overgrowth)
        {
            return null;
        }

        var pool = world.ActOneEncounterPool;
        if (pool is null
            || pool.Region
                != PrototypeActOneRegion.Overgrowth
            || pool.BossEncounterId is null)
        {
            return null;
        }

        if (!PrototypeContent.OvergrowthBossEncounterPool.Contains(
                pool.BossEncounterId,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unknown Overgrowth boss encounter '{pool.BossEncounterId}'.");
        }

        return PrototypeContent.Encounter(
            pool.BossEncounterId);
    }

    private static (
        PrototypeEncounterDefinition Encounter,
        RunWorldState World)?
        TryPickOvergrowthEliteEncounter(
            RunState state,
            RunWorldState world,
            PrototypeRoomType roomType)
    {
        if (roomType != PrototypeRoomType.Elite
            || world.Act != 1
            || world.ActOneRegion
                != PrototypeActOneRegion.Overgrowth)
        {
            return null;
        }

        var pool = world.ActOneEncounterPool;
        if (pool is null
            || pool.Region
                != PrototypeActOneRegion.Overgrowth)
        {
            return null;
        }

        var remaining =
            pool.RemainingEliteEncounterIds
            ?? (string[])PrototypeContent
                .OvergrowthEliteEncounterPool
                .Clone();
        if (remaining.Length == 0)
        {
            remaining = (string[])PrototypeContent
                .OvergrowthEliteEncounterPool
                .Clone();
        }

        var previous = world.EncounterIds
            .Reverse()
            .FirstOrDefault(id =>
                PrototypeContent.OvergrowthEliteEncounterPool.Contains(
                    id,
                    StringComparer.Ordinal));
        var eligible = previous is null
            ? remaining
            : remaining
                .Where(id =>
                    !StringComparer.Ordinal.Equals(
                        id,
                        previous))
                .ToArray();
        if (eligible.Length == 0)
        {
            eligible = remaining;
        }

        var selectedId = eligible[
            PrototypeRng.NextInt(
                state.Rng,
                "combat",
                eligible.Length)];
        var encounter = PrototypeContent.Encounter(
            selectedId);
        var nextRemaining = remaining
            .Where(id =>
                !StringComparer.Ordinal.Equals(
                    id,
                    selectedId))
            .ToArray();

        return (
            encounter,
            world with
            {
                ActOneEncounterPool = pool with
                {
                    RemainingEliteEncounterIds =
                        nextRemaining
                }
            });
    }

    private static (
        PrototypeEncounterDefinition Encounter,
        RunWorldState World)?
        TryPickOvergrowthWeakEncounter(
            RunState state,
            RunWorldState world,
            PrototypeRoomType roomType)
    {
        if (roomType != PrototypeRoomType.Combat
            || world.Act != 1
            || world.ActOneRegion
                != PrototypeActOneRegion.Overgrowth)
        {
            return null;
        }

        var pool = world.ActOneEncounterPool;
        if (pool is null
            || pool.Region
                != PrototypeActOneRegion.Overgrowth
            || pool.OrdinaryCombatsStarted >= 3)
        {
            return null;
        }

        if (pool.RemainingWeakEncounterIds.Length == 0)
        {
            throw new InvalidOperationException(
                "Overgrowth weak encounter pool exhausted before three ordinary combats.");
        }

        var selectedIndex = PrototypeRng.NextInt(
            state.Rng,
            "combat",
            pool.RemainingWeakEncounterIds.Length);
        var selectedId =
            pool.RemainingWeakEncounterIds[selectedIndex];
        var encounter = PrototypeContent.Encounter(
            selectedId);

        var remaining = pool.RemainingWeakEncounterIds
            .Where((_, index) => index != selectedIndex)
            .ToArray();
        var nextPool = pool with
        {
            OrdinaryCombatsStarted =
                pool.OrdinaryCombatsStarted + 1,
            RemainingWeakEncounterIds = remaining
        };

        return (
            encounter,
            world with
            {
                ActOneEncounterPool = nextPool
            });
    }

    private static (
        PrototypeEncounterDefinition Encounter,
        RunWorldState World)?
        TryPickOvergrowthNormalEncounter(
            RunState state,
            RunWorldState world,
            PrototypeRoomType roomType)
    {
        if (roomType != PrototypeRoomType.Combat
            || world.Act != 1
            || world.ActOneRegion
                != PrototypeActOneRegion.Overgrowth)
        {
            return null;
        }

        var pool = world.ActOneEncounterPool;
        if (pool is null
            || pool.Region
                != PrototypeActOneRegion.Overgrowth
            || pool.OrdinaryCombatsStarted < 3)
        {
            return null;
        }

        var remaining =
            pool.RemainingNormalEncounterIds
            ?? (string[])PrototypeContent
                .OvergrowthNormalEncounterPool
                .Clone();
        if (remaining.Length == 0)
        {
            throw new InvalidOperationException(
                "Overgrowth normal encounter bag is exhausted.");
        }

        var previous = world.EncounterIds.LastOrDefault();
        var eligible = previous is null
            ? remaining
            : remaining
                .Where(candidate =>
                    CanFollowOvergrowthEncounter(
                        previous,
                        candidate))
                .ToArray();
        if (eligible.Length == 0)
        {
            eligible = remaining;
        }

        var selectedId = eligible[
            PrototypeRng.NextInt(
                state.Rng,
                "combat",
                eligible.Length)];
        var encounter = PrototypeContent.Encounter(
            selectedId);
        var nextRemaining = remaining
            .Where(id =>
                !StringComparer.Ordinal.Equals(
                    id,
                    selectedId))
            .ToArray();

        return (
            encounter,
            world with
            {
                ActOneEncounterPool = pool with
                {
                    OrdinaryCombatsStarted =
                        pool.OrdinaryCombatsStarted + 1,
                    RemainingNormalEncounterIds =
                        nextRemaining
                }
            });
    }

    private static bool CanFollowOvergrowthEncounter(
        string previous,
        string candidate)
    {
        static bool IsEither(
            string value,
            string first,
            string second) =>
            StringComparer.Ordinal.Equals(
                value,
                first)
            || StringComparer.Ordinal.Equals(
                value,
                second);

        if (IsEither(
                previous,
                "proto.encounter.fuzzy_wurm_crawler_weak",
                "proto.encounter.overgrowth_crawlers")
            && IsEither(
                candidate,
                "proto.encounter.fuzzy_wurm_crawler_weak",
                "proto.encounter.overgrowth_crawlers"))
        {
            return false;
        }

        if (IsEither(
                previous,
                "proto.encounter.shrinker_beetle_weak",
                "proto.encounter.overgrowth_crawlers")
            && IsEither(
                candidate,
                "proto.encounter.shrinker_beetle_weak",
                "proto.encounter.overgrowth_crawlers"))
        {
            return false;
        }

        var slimeFamily = new[]
        {
            "proto.encounter.slimes_weak",
            "proto.encounter.flyconid_normal",
            "proto.encounter.slimes_normal"
        };
        if (slimeFamily.Contains(
                previous,
                StringComparer.Ordinal)
            && slimeFamily.Contains(
                candidate,
                StringComparer.Ordinal))
        {
            return false;
        }

        if (IsEither(
                previous,
                "proto.encounter.flyconid_normal",
                "proto.encounter.snapping_jaxfruit_normal")
            && IsEither(
                candidate,
                "proto.encounter.flyconid_normal",
                "proto.encounter.snapping_jaxfruit_normal"))
        {
            return false;
        }

        return true;
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
            if (!CanPlayAnotherCardThisTurn(state.Player, combat, card))
            {
                continue;
            }
            var isFreeByPower = IsCardFreeByPower(combat, definition);
            if (!definition.MechanicsImplemented
                || definition.Unplayable
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

            var effectiveTarget = EffectiveCardTarget(
                combat,
                definition);
            if (effectiveTarget == PrototypeCardTarget.Enemy)
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
            if (definition.AutomaticUsage)
            {
                continue;
            }

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

        var next = combat.PendingChoice is not null
            ? ResolvePendingChoice(state, action)
            : action.Kind switch
            {
                "play_card" =>
                    PlayCard(
                        state,
                        action.ReadPayload<PlayCardPayload>()),
                "use_potion" =>
                    UsePotion(
                        state,
                        action.ReadPayload<UsePotionPayload>()),
                "end_turn" => EndPlayerTurn(state),
                _ => throw new InvalidOperationException(
                    $"Unknown combat action '{action.Kind}'.")
            };

        if (next.Phase != RunPhase.Combat
            || next.Player.Hp > 0)
        {
            return next;
        }

        next = TryUseAutomaticDeathPrevention(next);
        return next.Player.Hp > 0
            ? next
            : EndRun(next, "defeat");
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
        if (!CanPlayAnotherCardThisTurn(state.Player, combat, card))
        {
            throw new InvalidOperationException(
                "A player effect prevents playing this card this turn.");
        }
        var definition = PrototypeContent.Card(card.CardId);
        if (!definition.MechanicsImplemented)
        {
            throw new NotSupportedException(
                $"Card '{definition.Name}' is catalogued but its combat mechanics are not implemented.");
        }

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

        ValidateTarget(
            EffectiveCardTarget(combat, definition),
            payload.TargetEnemyId,
            combat);
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

        if (card.Enchantment is
                { Kind: PrototypeCardEnchantmentKind.Sown } sown
            && !card.EnchantmentTriggeredThisCombat)
        {
            combat = combat with
            {
                Energy = combat.Energy + Math.Max(0, sown.Amount),
                Cards = combat.Cards.Select(item =>
                    item.InstanceId == card.InstanceId
                        ? item with { EnchantmentTriggeredThisCombat = true }
                        : item).ToArray()
            };
        }

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

        var effectEnergySpent =
            definition.Cost.Kind == PrototypeCardCostKind.X
                ? energySpent
                    + state.Player.Relics.Sum(relic =>
                        PrototypeContent.Relic(relic.RelicId)
                            .XValueBonus)
                : energySpent;

        var series = new PrototypeCardPlaySeriesState(
            SourceCardInstanceId: payload.CardInstanceId,
            SourceCardDestination: sourceDestination,
            TargetEnemyId: payload.TargetEnemyId,
            EnergySpent: effectEnergySpent,
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

        // An attack may kill the final enemy while its own effects still
        // require a card-selection choice (e.g. upgraded Dagger Throw).
        // Resolve that choice and its card-play continuation before firing
        // CombatWon: entering rewards here dispatches an unrelated event
        // against an existing pending choice and corrupts the continuation.
        if (combat.PendingChoice is not null)
        {
            return state;
        }

        return AllEnemiesDefeated(combat)
            ? EnterCombatReward(state)
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
        if (definition.AutomaticUsage)
        {
            throw new InvalidOperationException(
                $"Potion '{definition.Name}' is automatic and cannot be used manually.");
        }

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

        var playerAfterPotion =
            ConsumePotionSlot(
                state.Player,
                payload.Slot);
        playerAfterPotion =
            ApplyPotionRunEffects(
                playerAfterPotion,
                definition,
                state.Rng);

        var combatAfterPotion = combat with
        {
            Potions = playerAfterPotion.PotionSlots
                .Select((item, slot) =>
                    item is null
                        ? null
                        : new CombatPotionState(
                            slot,
                            item.PotionId,
                            item.PersistentState
                                .Clone()))
                .Where(item => item is not null)
                .Select(item => item!)
                .ToArray()
        };

        var resolved = ResolveOperations(
            playerAfterPotion,
            combatAfterPotion,
            operations,
            state.Rng,
            completionEvents:
            [
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.PotionUsed)
            ]);
        playerAfterPotion = resolved.Player;
        combatAfterPotion = resolved.Combat;

        state = state with
        {
            Player = playerAfterPotion,
            World = world with { Combat = combatAfterPotion }
        };

        if (combatAfterPotion.PendingChoice is not null)
        {
            return state;
        }

        return AllEnemiesDefeated(combatAfterPotion)
            ? EnterCombatReward(state)
            : state;
    }

    private static RunState TryUseAutomaticDeathPrevention(
        RunState state)
    {
        if (state.Player.Hp > 0
            || state.Phase != RunPhase.Combat)
        {
            return state;
        }

        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException(
                "Combat death prevention has no combat state.");
        var prevented = TryUseAutomaticDeathPrevention(
            state.Player,
            combat,
            state.Rng);

        return state with
        {
            Player = prevented.Player,
            World = world with
            {
                Combat = prevented.Combat
            }
        };
    }

    private static (PlayerState Player, CombatState Combat)
        TryUseAutomaticDeathPrevention(
            PlayerState player,
            CombatState combat,
            RngBundle rng,
            int eventDepth = 0)
    {
        if (player.Hp > 0)
        {
            return (player, combat);
        }

        for (var slot = 0;
             slot < player.PotionSlots.Length;
             slot++)
        {
            var potion = player.PotionSlots[slot];
            if (potion is null)
            {
                continue;
            }

            var definition =
                PrototypeContent.Potion(
                    potion.PotionId);
            if (!definition.AutomaticUsage
                || definition.DeathPreventionHealPercent <= 0)
            {
                continue;
            }

            var slots =
                (PotionInstance?[])player.PotionSlots.Clone();
            slots[slot] = null;
            var healedHp = Math.Max(
                1,
                (player.MaxHp
                    * definition.DeathPreventionHealPercent)
                / 100);
            player = player with
            {
                Hp = Math.Min(
                    player.MaxHp,
                    healedHp),
                PotionSlots = slots
            };
            combat = combat with
            {
                Potions = combat.PotionStates
                    .Where(item =>
                        item.Slot != slot)
                    .ToArray()
            };

            var dispatched = DispatchCombatEvent(
                player,
                combat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.PotionUsed),
                rng,
                eventDepth + 1);
            return (
                dispatched.Player,
                dispatched.Combat);
        }

        return (player, combat);
    }

    private static RunState EnterCombatReward(
        RunState state)
    {
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException(
                "Combat victory has no combat state.");

        var won = DispatchCombatEvent(
            state.Player,
            combat,
            new PrototypeCombatEvent(
                PrototypeCombatEventKind.CombatWon),
            state.Rng);
        state = state with
        {
            Player = won.Player,
            World = world with
            {
                Combat = won.Combat
            }
        };

        state = state with
        {
            Player = ApplyNativePersistentCombatEnd(
                state.Player,
                world.ActiveRoom,
                state.Rng)
        };

        if (world.Event is { } punchOff
            && punchOff.EventId == PrototypeNativePunchOff.EventId
            && punchOff.NativePageIndex == 2)
        {
            return EnterPunchOffReward(state);
        }

        if (world.Event is { } pendingEvent
            && pendingEvent.EventId == PrototypeNativeDenseVegetation.EventId
            && pendingEvent.NativePageIndex == 2)
        {
            // Native Dense Vegetation enters a four-Wriggler combat
            // without offering any postcombat reward.
            return CompleteRoomToMap(state with
            {
                World = RequireWorld(state) with { Combat = null }
            });
        }

        return EnterReward(state);
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
                state = TryUseAutomaticDeathPrevention(
                    state);
                if (state.Player.Hp <= 0)
                {
                    return EndRun(state, "defeat");
                }
            }

            currentCombat = RequireWorld(state).Combat
                ?? throw new InvalidOperationException(
                    "Combat unexpectedly disappeared.");
            if (AllEnemiesDefeated(currentCombat))
            {
                return EnterCombatReward(state);
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
            case PrototypeAutomaticStepKind.ResolvePlayerEndTurnHandEffects:
            {
                foreach (var instanceId in combat.Hand)
                {
                    var card = RequireCombatCard(
                        combat,
                        instanceId);
                    var definition =
                        PrototypeContent.Card(card.CardId);
                    var damage =
                        ApplyPlayerIncomingDamageCap(
                            combat,
                            Math.Max(
                                0,
                                definition
                                    .EndTurnDamageIfInHand));
                    if (damage <= 0)
                    {
                        continue;
                    }

                    var absorbed = definition.EndTurnDamageUnblockable
                        ? 0
                        : Math.Min(combat.PlayerBlock, damage);
                    combat = combat with
                    {
                        PlayerBlock =
                            combat.PlayerBlock - absorbed
                    };
                    player = player with
                    {
                        Hp = Math.Max(
                            0,
                            player.Hp
                            - Math.Max(
                                0,
                                damage - absorbed))
                    };

                    if (player.Hp <= 0)
                    {
                        break;
                    }
                }

                break;
            }

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
                var preserveHand =
                    combat.PlayerPowers.Any(power =>
                        power.Stacks > 0
                        && PrototypeContent.Power(power.PowerId)
                            .PreventsHandDiscard)
                    || player.Relics.Any(relic =>
                        PrototypeContent.Relic(relic.RelicId)
                            .PreventsHandDiscard);
                var retained = combat.Hand
                    .Where(instanceId =>
                        !etherealSet.Contains(instanceId)
                        && (preserveHand
                            || CardHasKeyword(
                                combat,
                                instanceId,
                                PrototypeCardKeyword.Retain)))
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
                combat = DecrementPlayerPowers(
                    combat,
                    definition =>
                        definition.DecrementAfterHandCleanup);

                break;
            }

            case PrototypeAutomaticStepKind.DispatchEnemyStatusStage:
                if (automaticStep.Stage is null)
                {
                    throw new InvalidOperationException("Enemy-status pipeline step is missing a stage.");
                }

                combat = ResolveEnemyStatusStage(
                    combat,
                    automaticStep.Stage.Value);
                if (automaticStep.Stage.Value
                    == PrototypeTurnStage.EnemyTurnEnd)
                {
                    combat = DecrementPlayerPowers(
                        combat,
                        definition =>
                            definition
                                .DecrementAtEnemyTurnEnd);
                }

                combat = ResolveEnemyDeathSummons(
                    combat,
                    state.Rng,
                    enemyActionSkips:
                        automaticStep.Stage.Value
                            == PrototypeTurnStage.EnemyTurnStart
                            ? 2
                            : 1);
                break;

            case PrototypeAutomaticStepKind.ResetEnemyBlock:
                combat = combat with
                {
                    Enemies = combat.Enemies
                        .Select(enemy => enemy with
                        {
                            Block = enemy.PowerStates.Any(power =>
                                power.Stacks > 0
                                && PrototypeContent.Power(power.PowerId)
                                    .PreventsEnemyBlockClear)
                                    ? enemy.Block : 0
                        })
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
                // EnemyTurnEnd statuses, summons, and formation changes have
                // completed. Select/commit the next enemy moves BEFORE the
                // next player turn begins, never during Observe().
                combat = CommitEnemyIntents(combat, state.Rng) with
                {
                    Turn = combat.Turn + 1,
                    PlayerPowers = combat.PlayerPowers.Select(power =>
                        PrototypeContent.Power(power.PowerId).SnapshotGenerationStacksAtTurnStart
                            ? power with { StoredValue = power.Stacks } : power).ToArray(),
                    Counters = combat.CounterState with
                    {
                        CardsPlayedThisTurn = 0,
                        AttacksPlayedLastTurn =
                            combat.CounterState.AttacksPlayedThisTurn,
                        AttacksPlayedThisTurn = 0,
                        SkillsPlayedThisTurn = 0,
                        CardsDiscardedThisTurn = 0,
                        PlayedCardIdsLastTurn =
                            combat.CounterState.PlayedCardIdsThisTurn,
                        PlayedCardIdsThisTurn = Array.Empty<long>(),
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

                var toricShields = combat.ToricShields
                    ?? Array.Empty<PrototypeToricShield>();
                var toricBlock = toricShields.Sum(item =>
                    item.BlockAmount);
                combat = combat with
                {
                    ToricShields = toricShields
                        .Where(item => item.ClearsRemaining > 1)
                        .Select(item => item with
                        {
                            ClearsRemaining = item.ClearsRemaining - 1
                        })
                        .ToArray(),
                    PlayerBlock = combat.PlayerBlock + toricBlock
                };

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
                var preserveUnusedEnergy =
                    player.Relics.Any(relic =>
                        PrototypeContent.Relic(relic.RelicId)
                            .PreserveUnusedEnergy);
                combat = combat with
                {
                    Energy = preserveUnusedEnergy
                        ? combat.Energy + EnergyPerTurn(player)
                        : EnergyPerTurn(player)
                };
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
                combat = ReturnLastTurnReboundCardsToHand(beforeHandDraw.Combat);

                var handDrawBonus =
                    combat.PlayerPowers.Sum(power =>
                        PrototypeContent.Power(power.PowerId)
                            .HandDrawBonusPerStack
                        * power.Stacks)
                    + player.Relics.Sum(relic =>
                        PrototypeContent.Relic(relic.RelicId)
                            .HandDrawBonus);
                var drawn = DrawCards(
                    player,
                    combat,
                    PrototypeContent.Rules.HandSize
                        + Math.Max(0, handDrawBonus),
                    state.Rng,
                    fromHandDraw: true);
                player = drawn.Player;
                combat = drawn.Combat;
                if (drawn.ShuffleSelectionPending)
                {
                    var pendingDraw = new Queue<PrototypeQueuedOperation>(
                        new[]
                        {
                            StratagemChoiceOperation(combat),
                            new PrototypeQueuedOperation(
                                PrototypeCombatEffectKind.DrawCards,
                                drawn.RemainingCount)
                        });
                    var resumed = ResolveOperations(
                        player, combat, pendingDraw, state.Rng,
                        moveSourceCardOnCompletion: false);
                    player = resumed.Player;
                    combat = resumed.Combat;
                }

                combat = RemovePlayerPowers(
                    combat,
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

    private static PrototypeQueuedOperation StratagemChoiceOperation(
        CombatState combat)
    {
        var amount = combat.PlayerPowers
            .Where(power => power.PowerId == "proto.power.stratagem")
            .Sum(power => Math.Max(0, power.Stacks));
        return new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.ChooseCards, 0,
            Selection: new PrototypeCardSelectionSpec(
                PrototypeCardZone.DrawPile,
                amount, amount,
                PrototypeCardSelectionResolutionKind.MoveToHand));
    }

    private static CombatState ReturnLastTurnReboundCardsToHand(
        CombatState combat)
    {
        var lastTurn = combat.CounterState.PlayedCardIdsLastTurn
            ?? Array.Empty<long>();
        foreach (var id in lastTurn.Distinct())
        {
            if (combat.Hand.Contains(id))
            {
                continue;
            }

            var card = combat.Cards.FirstOrDefault(c => c.InstanceId == id);
            if (card is null || card.CardId is not
                ("proto.colorless.bolas" or "proto.colorless.thrumming_hatchet"))
            {
                continue;
            }

            if (combat.Hand.Length >= 10)
            {
                break;
            }

            var fromDraw = combat.DrawPile.Contains(id);
            var fromDiscard = combat.DiscardPile.Contains(id);
            var fromExhaust = combat.ExhaustPile.Contains(id);
            if (!fromDraw && !fromDiscard && !fromExhaust)
            {
                continue;
            }

            combat = combat with
            {
                DrawPile = combat.DrawPile.Where(x => x != id).ToArray(),
                DiscardPile = combat.DiscardPile.Where(x => x != id).ToArray(),
                ExhaustPile = combat.ExhaustPile.Where(x => x != id).ToArray(),
                Hand = combat.Hand.Append(id).ToArray()
            };
        }
        return combat;
    }

    private static CombatState ResolveEnemyStatusStage(
        CombatState combat,
        PrototypeTurnStage stage)
    {
        var enemies = combat.Enemies.Select(enemy => enemy.Fork()).ToArray();

        for (var enemyIndex = 0; enemyIndex < enemies.Length; enemyIndex++)
        {
            var enemy = enemies[enemyIndex];
            var wasAlive = enemy.Hp > 0;
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
                    if (definition.TriggerKind
                        == PrototypeStatusTriggerKind
                            .DamageSelfByStacks)
                    {
                        var triggerCount =
                            ResolveEnemyStatusTriggerCount(
                                combat,
                                pair.Key,
                                stacks);
                        for (var trigger = 0;
                             trigger < triggerCount;
                             trigger++)
                        {
                            if (enemy.Hp <= 0
                                || stacks <= 0)
                            {
                                break;
                            }

                            var hpLoss =
                                ModifyEnemyHpLoss(
                                    enemy,
                                    stacks);
                            enemy = hpLoss.Enemy with
                            {
                                Hp = Math.Max(
                                    0,
                                    hpLoss.Enemy.Hp
                                    - hpLoss.HpLoss)
                            };

                            if (enemy.Hp > 0)
                            {
                                stacks -=
                                    definition
                                        .DecayOnTrigger;
                            }
                        }
                    }
                    else
                    {
                        stacks -=
                            definition.DecayOnTrigger;
                    }
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

            // Expiring an enemy-side countdown can wake its owner and
            // remove companion protection *before* other powers contribute
            // end-of-turn Block (native Asleep/Plating ordering).
            string? aiAfterExpiry = null;
            var powersToRemoveOnExpiry = new HashSet<string>(
                StringComparer.Ordinal);
            if (stage == PrototypeTurnStage.EnemyTurnEnd)
            {
                foreach (var power in enemy.PowerStates)
                {
                    var definition = PrototypeContent.Power(power.PowerId);
                    if (definition.EnemyStacksDecayAtSideTurnEnd > 0
                        && power.Stacks <=
                            definition.EnemyStacksDecayAtSideTurnEnd)
                    {
                        aiAfterExpiry ??=
                            definition.OwnerAiStateOnPowerExpiry;
                        foreach (var id in definition
                            .RemoveOwnerPowersOnPowerExpiry
                            ?? Array.Empty<string>())
                        {
                            powersToRemoveOnExpiry.Add(id);
                        }
                    }
                }
            }

            enemy = InterceptEnemyLethalDeath(enemy);
            var strengthAtSideTurnEnd = 0;
            // Expiring temporary debuffs restore their original Strength
            // *after* the enemy has acted, even though the source power
            // itself is removed at this stage.
            var temporaryStrengthRestoration =
                stage == PrototypeTurnStage.EnemyTurnEnd
                    ? enemy.PowerStates
                        .Where(power =>
                            PrototypeContent.Power(power.PowerId)
                                .RemoveAtEnemyTurnEnd)
                        .Sum(power => power.Stacks
                            * PrototypeContent.Power(power.PowerId)
                                .EnemyStrengthRestoreAtSideTurnEndPerStack)
                    : 0;
            var blockAtSideTurnEnd = 0;
            var powers = stage == PrototypeTurnStage.EnemyTurnEnd
                ? enemy.PowerStates
                    .Where(power =>
                        !PrototypeContent.Power(power.PowerId)
                            .RemoveAtEnemyTurnEnd
                        && !powersToRemoveOnExpiry.Contains(power.PowerId))
                    .Select(power =>
                    {
                        var definition = PrototypeContent.Power(power.PowerId);
                        if (power.SkipNextEnemySideTurnEnd)
                        {
                            // Newly applied enemy Ritual must not activate
                            // on the same enemy turn it was applied.
                            return power with
                            {
                                SkipNextEnemySideTurnEnd = false
                            };
                        }

                        strengthAtSideTurnEnd +=
                            power.Stacks
                            * definition.EnemyStrengthAtSideTurnEndPerStack;
                        blockAtSideTurnEnd +=
                            power.Stacks
                            * definition.EnemyBlockAtSideTurnEndPerStack;
                        return power with
                        {
                            Stacks = power.Stacks -
                                definition.EnemyStacksDecayAtSideTurnEnd
                        };
                    })
                    .Where(power => power.Stacks > 0
                        || PrototypeContent.Power(power.PowerId)
                            .AllowNegative)
                    .ToArray()
                : stage == PrototypeTurnStage.EnemyTurnStart
                  && combat.Turn > 1
                    ? enemy.PowerStates
                        .Select(power => power with
                        {
                            Stacks = power.Stacks -
                                PrototypeContent.Power(power.PowerId)
                                    .EnemyStacksDecayAtSideTurnStartAfterFirst
                        })
                        .Where(power => power.Stacks > 0
                            || PrototypeContent.Power(power.PowerId)
                                .AllowNegative)
                        .ToArray()
                    : enemy.PowerStates;

            enemies[enemyIndex] = enemy with
            {
                Statuses = statuses,
                Powers = powers,
                AiStateId = aiAfterExpiry ?? enemy.AiStateId,
                Block = enemy.Block + blockAtSideTurnEnd,
                SharedSummonUsedThisTurn =
                    stage == PrototypeTurnStage.EnemyTurnStart
                        ? false : enemy.SharedSummonUsedThisTurn,
                HpLossBudgetUsed =
                    stage == PrototypeTurnStage.EnemyTurnStart
                        ? 0 : enemy.HpLossBudgetUsed,
                GainedReactiveBlockThisTurn =
                    stage == PrototypeTurnStage.EnemyTurnEnd
                        ? false : enemy.GainedReactiveBlockThisTurn
            };

            var totalStrengthRestoration =
                strengthAtSideTurnEnd + temporaryStrengthRestoration;
            if (enemy.Hp > 0 && totalStrengthRestoration != 0)
            {
                var strengthened = ApplyEnemyPowerToState(
                    combat, enemies[enemyIndex],
                    "proto.power.strength", totalStrengthRestoration,
                    ignoreDebuffPrevention: true);
                combat = strengthened.Combat;
                enemies[enemyIndex] = strengthened.Enemy;
            }
            if (wasAlive && enemies[enemyIndex].Hp <= 0)
            {
                // Scheduled poison damage is processed directly in this
                // stage rather than through LoseEnemyHp. It must still
                // dispatch ally-death powers before other enemies act.
                combat = ResolveAllyDeathPowers(
                    combat with { Enemies = enemies }, enemy.InstanceId);
                enemies = combat.Enemies
                    .Select(item => item.Fork())
                    .ToArray();
            }
        }

        combat = combat with { Enemies = enemies };
        return CleanupSourceBoundPowersForDefeatedEnemies(
            combat);
    }

    // Commit each enemy's action at combat entry and at the end of each
    // enemy turn. Importantly, this consumes the combat RNG once, at a
    // transition boundary; repeated public observations never reroll moves.
    internal static CombatState CommitEnemyIntents(
        CombatState combat,
        RngBundle rng)
    {
        var formation = combat.Enemies.Select(enemy => enemy.Fork()).ToArray();
        for (var index = 0; index < formation.Length; index++)
        {
            var enemy = formation[index];
            var definition = PrototypeContent.Enemy(enemy.EnemyId);
            if (enemy.Hp <= 0
                || enemy.SkipNextEnemyAction
                || enemy.EnemyActionSkipsRemaining > 0
                || definition.Moves.Length == 0)
            {
                formation[index] = enemy with
                {
                    PlannedMoveIndex = null,
                    PlannedNextAiStateId = null
                };
                continue;
            }

            var selected = SelectEnemyMove(definition, enemy, formation, rng);
            var selectedIndex = Array.IndexOf(definition.Moves, selected.Move);
            if (selectedIndex < 0)
            {
                throw new InvalidOperationException(
                    $"Enemy '{enemy.EnemyId}' selected an unknown move.");
            }

            formation[index] = enemy with
            {
                PlannedMoveIndex = selectedIndex,
                PlannedNextAiStateId = selected.NextAiStateId
            };
        }

        return combat with { Enemies = formation };
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
                                branch,
                                formation))
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
                            out var target))
                    {
                        throw new InvalidOperationException(
                            $"Enemy '{definition.Id}' AI conditional branch targets missing state '{selected.TargetStateId}'.");
                    }

                    // The native Exoskeleton fourth slot branches to
                    // RAND rather than directly to a MoveState.
                    // Continue resolving the graph until a concrete
                    // move is committed, using the same RNG and without
                    // sampling anything during Observe().
                    stateId = target.Id;
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

    internal static bool EnemyAiConditionMatches(
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
            PrototypeEnemyAiConditionKind.IsOffBalance =>
                enemy.IsOffBalance,
            PrototypeEnemyAiConditionKind.IsNotOffBalance =>
                !enemy.IsOffBalance,
            PrototypeEnemyAiConditionKind.HasEnemyPower =>
                branch.Value is not null
                && enemy.PowerStates.Any(power =>
                    power.PowerId == branch.Value && power.Stacks > 0),
            PrototypeEnemyAiConditionKind.LacksEnemyPower =>
                branch.Value is not null
                && !enemy.PowerStates.Any(power =>
                    power.PowerId == branch.Value && power.Stacks > 0),
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
        PrototypeEnemyAiBranch branch,
        IReadOnlyList<EnemyCombatState> formation)
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

        if (branch.RequiresAvailableSummon)
        {
            var summon = move.Effects.FirstOrDefault(effect =>
                effect.Kind == PrototypeEnemyEffectKind.SummonEnemy);
            var slots = summon?.SummonSlotNames;
            if (definition.MaxCoordinatedSummons <= 0
                || enemy.NonSummonMovesUntilEligible > 0
                || enemy.SharedSummonsUsed >= definition.MaxCoordinatedSummons
                || enemy.SharedSummonUsedThisTurn
                || slots is not { Length: > 0 }
                || !slots.Any(slot => !formation.Any(candidate =>
                    candidate.Hp > 0
                    && StringComparer.Ordinal.Equals(candidate.SlotName, slot))))
            {
                return false;
            }
        }

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
        var enemies = combat.Enemies
            .Select(enemy => enemy.Fork())
            .ToList();
        var block = combat.PlayerBlock;
        var hp = player.Hp;
        var gold = player.Gold;

        for (var index = 0; index < enemies.Count; index++)
        {
            var enemy = enemies[index];
            var definition = PrototypeContent.Enemy(enemy.EnemyId);

            if (enemy.Hp <= 0)
            {
                var leaderAlive = enemy.LeaderEnemyInstanceId is not
                        { } leaderId
                    || enemies.Any(candidate =>
                        candidate.InstanceId == leaderId
                        && candidate.Hp > 0);
                if (definition.RevivesOnEnemyTurn && leaderAlive)
                {
                    var hpRange = definition.HpRangeAt(
                        act,
                        ascension);
                    enemy = enemy with
                    {
                        Hp = hpRange.Max,
                        Block = 0,
                        Statuses =
                            new Dictionary<string, int>(
                                StringComparer.Ordinal)
                    };
                    enemies[index] = enemy;
                }

                continue;
            }

            if (enemy.EnemyActionSkipsRemaining > 0)
            {
                enemies[index] = enemy with
                {
                    EnemyActionSkipsRemaining =
                        enemy.EnemyActionSkipsRemaining - 1,
                    SkipNextEnemyAction = false
                };
                continue;
            }

            if (enemy.SkipNextEnemyAction)
            {
                enemies[index] = enemy with
                {
                    SkipNextEnemyAction = false
                };
                continue;
            }

            if (definition.Moves.Length == 0)
            {
                continue;
            }

            // Normal gameplay has a precommitted move. A fallback is kept
            // solely for legacy/manual combat states created without the
            // initial intent-commit step; seeded runs never use it.
            var selection = enemy.PlannedMoveIndex is { } selectedIndex
                ? (Move: definition.Moves[selectedIndex],
                   NextAiStateId: enemy.PlannedNextAiStateId)
                : SelectEnemyMove(definition, enemy, enemies, rng);
            var move = selection.Move;
            // BowlbugRock.DizzyMove unstuns the model and clears its
            // persistent off-balance condition before its next Headbutt.
            if (enemy.EnemyId == "proto.native.hive.bowlbug_rock"
                && move.Id == "dizzy")
            {
                enemy = enemy with { IsOffBalance = false };
            }
            // Vigor applies to every hit of one attack command, then
            // consumes the stacks that existed when the command began.
            // Vigor gained by this move is never consumed by that move.
            var consumeOnAttackOrders = move.Effects.Any(effect =>
                    effect.Kind == PrototypeEnemyEffectKind.DamagePlayer
                    && effect.IsAttack)
                ? enemy.PowerStates
                    .Where(power => power.Stacks > 0
                        && PrototypeContent.Power(power.PowerId)
                            .ConsumeAfterEnemyAttack)
                    .Select(power => power.ApplicationOrder)
                    .ToHashSet()
                : new HashSet<long>();

            foreach (var effect in move.Effects)
            {
                var amount = effect.UseStoredEnemyDamage
                    ? enemy.StoredEnemyDamage
                    : checked(effect.AmountAt(act, ascension)
                        + effect.ExtraAmountPerPriorMoveUse
                        * (enemy.MoveUseCounts?.GetValueOrDefault(move.Id) ?? 0));
                var repetitions =
                    effect.RepetitionsAt(ascension);
                var unblockedAttackHits = 0;
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
                                var hpLoss = ModifyEnemyHpLoss(
                                    enemy,
                                    Math.Max(
                                        0,
                                        retaliation - retaliationBlocked));
                                enemy = hpLoss.Enemy with
                                {
                                    Block = enemy.Block - retaliationBlocked,
                                    Hp = Math.Max(
                                        0,
                                        hpLoss.Enemy.Hp - hpLoss.HpLoss)
                                };
                            }

                            var damage = EnemyHitDamage(
                                enemy,
                                combat,
                                effect,
                                act,
                                ascension,
                                enemy.MoveUseCounts?.GetValueOrDefault(move.Id) ?? 0);

                            var absorbed = Math.Min(
                                block,
                                Math.Max(0, damage));
                            block -= absorbed;
                            var unblocked = Math.Max(0, damage - absorbed);
                            // ImbalancedPower.AfterDamageGiven: a fully
                            // blocked attack leaves Bowlbug Rock off-balance.
                            // Headbutt then commits Dizzy on the NEXT
                            // enemy turn rather than rerolling in Observe.
                            if (effect.IsAttack && damage > 0
                                && unblocked == 0
                                && enemy.PowerStates.Any(power =>
                                    power.Stacks > 0
                                    && power.PowerId ==
                                        "proto.native.hive.imbalanced"))
                            {
                                enemy = enemy with { IsOffBalance = true };
                            }
                            if (effect.IsAttack && unblocked > 0 && hp > 0)
                            {
                                unblockedAttackHits++;
                            }
                            hp = Math.Max(0, hp - unblocked);
                            if (effect.IsAttack && unblocked > 0
                                && combat.PlayerPowers.Any(power =>
                                    power.Stacks > 0
                                    && PrototypeContent.Power(power.PowerId)
                                        .LethalAfterUnblockedPoweredAttack))
                            {
                                // The Gambit deliberately kills its owner
                                // upon the first unblocked powered hit.
                                hp = 0;
                            }
                            break;
                        }

                        case PrototypeEnemyEffectKind.KillSelf:
                            enemy = enemy with { Hp = 0, Block = 0 };
                            break;

                        case PrototypeEnemyEffectKind.StealPlayerCard:
                        {
                            // Thieving Hopper only steals persistent cards
                            // presently in the DRAW or DISCARD piles. A card
                            // in hand/exhaust/temporary play cannot be stolen.
                            var eligible = combat.DrawPile
                                .Concat(combat.DiscardPile)
                                .Select(id => combat.Cards.FirstOrDefault(
                                    card => card.InstanceId == id))
                                .Where(card => card is not null
                                    && card.PersistentCardInstanceId is not null
                                    && player.Deck.Any(persistent =>
                                        persistent.InstanceId
                                            == card.PersistentCardInstanceId))
                                .Select(card => card!)
                                .ToArray();
                            if (eligible.Length == 0)
                            {
                                break;
                            }

                            // Source priority: Uncommon; then Common,
                            // Rare or Event; then Basic or Quest; then
                            // Ancient/Imbued. The latter enchantment is
                            // not represented in this single-player
                            // registry and remains unsupported.
                            static int TheftPriority(CombatCardInstance card)
                            {
                                var rarity = PrototypeContent.Card(
                                    card.CardId).Rarity;
                                return rarity switch
                                {
                                    PrototypeCardRarity.Uncommon => 0,
                                    PrototypeCardRarity.Common
                                        or PrototypeCardRarity.Rare
                                        or PrototypeCardRarity.Event => 1,
                                    PrototypeCardRarity.Basic
                                        or PrototypeCardRarity.Quest => 2,
                                    PrototypeCardRarity.Ancient => 3,
                                    _ => 4
                                };
                            }

                            var best = eligible.Min(TheftPriority);
                            var candidates = eligible.Where(card =>
                                TheftPriority(card) == best).ToArray();
                            // Native uses RunRng.CombatCardGeneration,
                            // which has no matching prototype substream.
                            // This stream is explicitly an approximation.
                            var chosen = candidates[
                                PrototypeRng.NextInt(rng, "combat",
                                    candidates.Length)];
                            var persistentId =
                                chosen.PersistentCardInstanceId!.Value;
                            var persistentCard = player.Deck.Single(card =>
                                card.InstanceId == persistentId);
                            player = player with
                            {
                                Deck = player.Deck.Where(card =>
                                    card.InstanceId != persistentId)
                                    .ToArray()
                            };
                            combat = combat with
                            {
                                DrawPile = combat.DrawPile.Where(id =>
                                    id != chosen.InstanceId).ToArray(),
                                DiscardPile = combat.DiscardPile.Where(id =>
                                    id != chosen.InstanceId).ToArray(),
                                Cards = combat.Cards.Where(card =>
                                    card.InstanceId != chosen.InstanceId)
                                    .ToArray(),
                                NextPowerApplicationOrder =
                                    combat.NextPowerApplicationOrder + 1
                            };
                            enemy = enemy with
                            {
                                StolenCards = enemy.StolenCards
                                    is { } existing
                                        ? existing.Append(persistentCard)
                                            .ToArray()
                                        : [persistentCard],
                                Powers = enemy.PowerStates.Append(
                                    new PrototypePowerInstanceState(
                                        "proto.native.hive.swipe", 1,
                                        combat.NextPowerApplicationOrder - 1))
                                    .ToArray()
                            };
                            break;
                        }

                        case PrototypeEnemyEffectKind.StealPlayerGold:
                        {
                            var stolen = Math.Min(Math.Max(0, amount), gold);
                            gold -= stolen;
                            enemy = enemy with
                            {
                                StolenGold = checked(enemy.StolenGold + stolen)
                            };
                            break;
                        }

                        case PrototypeEnemyEffectKind.EscapeEnemy:
                            enemy = enemy with
                            {
                                Hp = 0,
                                Block = 0,
                                Escaped = true
                            };
                            break;

                        case PrototypeEnemyEffectKind.GainBlock:
                            enemy = enemy with { Block = enemy.Block + Math.Max(0, amount) };
                            break;

                        case PrototypeEnemyEffectKind.HealSelf:
                            enemy = enemy with
                            {
                                Hp = Math.Min(
                                    definition.HpRangeAt(act, ascension).Max,
                                    enemy.Hp + Math.Max(0, amount))
                            };
                            break;

                        case PrototypeEnemyEffectKind.StoreEnemyPowerAsDamage:
                            if (effect.PowerId is null)
                            {
                                throw new InvalidOperationException(
                                    "Stored-damage effect requires a power ID.");
                            }
                            enemy = enemy with
                            {
                                StoredEnemyDamage = enemy.PowerStates
                                    .Where(power => power.PowerId == effect.PowerId)
                                    .Sum(power => power.Stacks),
                                Powers = enemy.PowerStates
                                    .Where(power => power.PowerId != effect.PowerId)
                                    .ToArray()
                            };
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

                        case PrototypeEnemyEffectKind.AddCardsToHand:
                            if (effect.CardId is null)
                            {
                                throw new InvalidOperationException(
                                    "Enemy hand-card effect is missing a card ID.");
                            }

                            combat = AddGeneratedEnemyCardsToHand(
                                combat, effect.CardId, amount);
                            break;

                        case PrototypeEnemyEffectKind.AddCardsToDiscard:
                            if (effect.CardId is null)
                            {
                                throw new InvalidOperationException(
                                    "Enemy add-card effect is missing a card ID.");
                            }

                            combat = AddGeneratedCardsToDiscard(
                                combat,
                                effect.CardId,
                                amount);
                            break;

                        case PrototypeEnemyEffectKind.AddCardsToRandomDraw:
                            if (effect.CardId is null)
                            {
                                throw new InvalidOperationException(
                                    "Enemy draw-card effect is missing card ID.");
                            }

                            for (var generated = 0; generated < amount; generated++)
                            {
                                var instance = new CombatCardInstance(
                                    combat.NextCardInstanceId,
                                    null, effect.CardId, 0, true,
                                    PrototypeJson.EmptyObject());
                                var insertion = PrototypeRng.NextInt(
                                    rng, "combat", combat.DrawPile.Length + 1);
                                var draw = combat.DrawPile.ToList();
                                draw.Insert(insertion, instance.InstanceId);
                                combat = combat with
                                {
                                    Cards = combat.Cards.Append(instance).ToArray(),
                                    DrawPile = draw.ToArray(),
                                    NextCardInstanceId = combat.NextCardInstanceId + 1
                                };
                            }
                            break;

                        case PrototypeEnemyEffectKind.SummonEnemy:
                        {
                            if (effect.EnemyId is null)
                            {
                                throw new InvalidOperationException(
                                    "Enemy summon effect is missing an enemy ID.");
                            }

                            var summonedDefinition =
                                PrototypeContent.Enemy(
                                    effect.EnemyId);
                            var hpRange =
                                summonedDefinition.HpRangeAt(
                                    act,
                                    ascension);
                            var summonedHp =
                                hpRange.Min == hpRange.Max
                                    ? hpRange.Min
                                    : hpRange.Min
                                        + PrototypeRng.NextInt(
                                            rng,
                                            "combat",
                                            hpRange.Max
                                                - hpRange.Min
                                                + 1);
                            var nextEnemyId =
                                enemies.Count == 0
                                    ? 1
                                    : enemies.Max(item =>
                                        item.InstanceId) + 1;
                            var nextPowerOrder =
                                combat.NextPowerApplicationOrder;
                            var powers =
                                (summonedDefinition.StartingPowers
                                    ?? Array.Empty<
                                        PrototypeStartingPowerSpec>())
                                .Select(power =>
                                {
                                    _ = PrototypeContent.Power(
                                        power.PowerId);
                                    return new
                                        PrototypePowerInstanceState(
                                            power.PowerId,
                                            power.StacksAt(ascension),
                                            nextPowerOrder++);
                                })
                                .Where(power =>
                                    power.Stacks > 0)
                                .ToArray();
                            var summonSlots = effect.SummonSlotNames;
                            string? selectedSlot = null;
                            if (summonSlots is { Length: > 0 })
                            {
                                selectedSlot = summonSlots
                                    .Reverse()
                                    .FirstOrDefault(slot =>
                                        !enemies.Any(item =>
                                            item.Hp > 0
                                            && StringComparer.Ordinal.Equals(
                                                item.SlotName, slot)));
                                if (selectedSlot is null)
                                {
                                    break;
                                }
                            }

                            var nextFormationPosition =
                                selectedSlot is null
                                    ? (enemies.Count == 0
                                        ? 0
                                        : enemies.Max(item =>
                                            item.FormationPosition) + 1)
                                    : Array.IndexOf(summonSlots!, selectedSlot);

                            enemies.Add(
                                new EnemyCombatState(
                                    InstanceId: nextEnemyId,
                                    EnemyId: effect.EnemyId,
                                    Hp: summonedHp,
                                    Block: 0,
                                    MoveIndex: 0,
                                    Statuses:
                                        new Dictionary<string, int>(
                                            StringComparer.Ordinal),
                                    Powers: powers,
                                    FormationPosition:
                                        nextFormationPosition,
                                    SlotName: selectedSlot,
                                    LeaderEnemyInstanceId:
                                        summonedDefinition.IsMinion
                                            ? enemy.InstanceId
                                            : null,
                                    SkipNextEnemyAction: true,
                                    NonSummonMovesUntilEligible:
                                        summonedDefinition.NonSummonMovesBeforeEligible,
                                    SharedSummonsUsed:
                                        definition.MaxCoordinatedSummons > 0
                                            ? enemy.SharedSummonsUsed + 1
                                            : 0));
                            if (definition.MaxCoordinatedSummons > 0)
                            {
                                // Coordinated summons share a global
                                // squad budget and reserve this turn.
                                var used = enemy.SharedSummonsUsed + 1;
                                enemy = enemy with
                                {
                                    SharedSummonsUsed = used,
                                    SharedSummonUsedThisTurn = true
                                };
                                for (var otherIndex = 0;
                                     otherIndex < enemies.Count;
                                     otherIndex++)
                                {
                                    if (StringComparer.Ordinal.Equals(
                                            enemies[otherIndex].EnemyId,
                                            enemy.EnemyId))
                                    {
                                        enemies[otherIndex] = enemies[otherIndex] with
                                        {
                                            SharedSummonsUsed = used,
                                            SharedSummonUsedThisTurn = true
                                        };
                                    }
                                }
                            }
                            combat = combat with
                            {
                                NextPowerApplicationOrder =
                                    nextPowerOrder,
                                Enemies = enemies
                                    .Select(item => item.Fork())
                                    .ToArray()
                            };
                            break;
                        }

                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    if (hp <= 0 || enemy.Hp <= 0)
                    {
                        break;
                    }
                }

                if (unblockedAttackHits > 0 && enemy.Hp > 0)
                {
                    // The native Suck hook fires after a powered attack
                    // command and counts hits which dealt unblocked HP
                    // damage, not blocked hits or non-attack HP loss.
                    var strengthGain = enemy.PowerStates.Sum(power =>
                        power.Stacks
                        * PrototypeContent.Power(power.PowerId)
                            .StrengthPerUnblockedAttackHitPerStack)
                        * unblockedAttackHits;
                    if (strengthGain > 0)
                    {
                        var strengthened = ApplyEnemyPowerToState(
                            combat, enemy, "proto.power.strength",
                            strengthGain);
                        combat = strengthened.Combat;
                        enemy = strengthened.Enemy;
                    }
                }

                if (hp <= 0 || enemy.Hp <= 0)
                {
                    break;
                }
            }

            if (consumeOnAttackOrders.Count > 0)
            {
                enemy = enemy with
                {
                    Powers = enemy.PowerStates
                        .Where(power => !consumeOnAttackOrders.Contains(
                            power.ApplicationOrder))
                        .ToArray()
                };
            }

            var turnEndStrength =
                enemy.PowerStates.Sum(power =>
                    PrototypeContent.Power(power.PowerId)
                        .EnemyStrengthGainAtTurnEndPerStack
                    * power.Stacks);
            if (turnEndStrength != 0)
            {
                var strengthened = ApplyEnemyPowerToState(
                    combat,
                    enemy,
                    "proto.power.strength",
                    turnEndStrength);
                combat = strengthened.Combat;
                enemy = strengthened.Enemy;
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

            enemy = InterceptEnemyLethalDeath(enemy);
            enemies[index] = enemy with
            {
                MoveIndex = enemy.MoveIndex + 1,
                NonSummonMovesUntilEligible = move.Effects.Any(effect =>
                    effect.Kind == PrototypeEnemyEffectKind.SummonEnemy)
                    ? enemy.NonSummonMovesUntilEligible
                    : Math.Max(0, enemy.NonSummonMovesUntilEligible - 1),
                LastMoveId = move.Id,
                ConsecutiveMoveUses = consecutiveUses,
                AiStateId = selection.NextAiStateId,
                MoveUseCounts = moveUseCounts,
                PlannedMoveIndex = null,
                PlannedNextAiStateId = null
            };
            if (hp <= 0)
            {
                break;
            }
        }

        combat = combat with
        {
            PlayerBlock = block,
            Enemies = enemies.ToArray()
        };
        combat = CleanupSourceBoundPowersForDefeatedEnemies(
            combat);
        combat = ResolveEnemyDeathSummons(
            combat,
            rng);

        return (
            player with { Hp = hp, Gold = gold },
            combat);
    }

    private static CombatState ResolveEnemyDeathSummons(
        CombatState combat,
        RngBundle rng,
        int enemyActionSkips = 1)
    {
        var enemies = combat.Enemies
            .Select(enemy => enemy.Fork())
            .ToList();
        var nextPowerOrder =
            combat.NextPowerApplicationOrder;
        var changed = false;

        for (var index = 0;
             index < enemies.Count;
             index++)
        {
            var source = enemies[index];
            if (source.Hp > 0
                || source.DeathEffectsResolved)
            {
                continue;
            }

            var sourceDefinition =
                PrototypeContent.Enemy(source.EnemyId);
            var summons =
                sourceDefinition.DeathSummons
                ?? Array.Empty<
                    PrototypeEnemyDeathSummonSpec>();
            if (summons.Length == 0)
            {
                continue;
            }

            enemies[index] = source with
            {
                DeathEffectsResolved = true
            };
            changed = true;

            foreach (var summon in summons)
            {
                var definition =
                    PrototypeContent.Enemy(
                        summon.EnemyId);
                var hpRange = definition.HpRangeAt(
                    combat.Act,
                    combat.Ascension);
                var hp =
                    hpRange.Min == hpRange.Max
                        ? hpRange.Min
                        : hpRange.Min
                            + PrototypeRng.NextInt(
                                rng,
                                "combat",
                                hpRange.Max
                                    - hpRange.Min
                                    + 1);
                var powers =
                    (definition.StartingPowers
                        ?? Array.Empty<
                            PrototypeStartingPowerSpec>())
                    .Select(power =>
                    {
                        _ = PrototypeContent.Power(
                            power.PowerId);
                        return new
                            PrototypePowerInstanceState(
                                power.PowerId,
                                power.StacksAt(combat.Ascension),
                                nextPowerOrder++);
                    })
                    .Where(power =>
                        power.Stacks > 0)
                    .ToArray();
                var nextEnemyId =
                    enemies.Count == 0
                        ? 1
                        : enemies.Max(enemy =>
                            enemy.InstanceId) + 1;

                enemies.Add(
                    new EnemyCombatState(
                        InstanceId: nextEnemyId,
                        EnemyId: summon.EnemyId,
                        Hp: hp,
                        Block: 0,
                        MoveIndex: 0,
                        Statuses:
                            new Dictionary<string, int>(
                                StringComparer.Ordinal),
                        Powers: summon.TransferStolenGold
                            && source.StolenGold > 0
                            ? powers.Append(new PrototypePowerInstanceState(
                                "proto.power.heist",
                                source.StolenGold,
                                nextPowerOrder++)).ToArray()
                            : powers,
                        FormationPosition:
                            summon.FormationPosition,
                        SlotName: summon.SlotName,
                        StolenGold: summon.TransferStolenGold
                            ? source.StolenGold : 0,
                        EnemyActionSkipsRemaining:
                            summon.SkipEnemyActions == 1
                                ? Math.Max(0, enemyActionSkips)
                                : Math.Max(0, summon.SkipEnemyActions)));
            }
        }

        return changed
            ? combat with
            {
                Enemies = enemies
                    .Select(enemy => enemy.Fork())
                    .ToArray(),
                NextPowerApplicationOrder =
                    nextPowerOrder
            }
            : combat;
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
        PrototypeCombatCardSnapshot? powerCardPayload = null,
        int? sourcePowerEnemyId = null,
        int sourcePowerStoredValue = 0,
        long? sourcePowerApplicationOrder = null)
    {
        // Snapshot hand-sensitive card conditions when their operations are
        // queued. Restlessness first draws cards and then gains energy, but
        // both effects depend on the hand at the moment the card is played.
        if (effect.Condition?.Kind
                == PrototypeCombatPredicateKind.HandEmptyAtEnqueue
            && combat.Hand.Length != 0)
        {
            return;
        }

        var queuedCondition = effect.Condition?.Kind
                == PrototypeCombatPredicateKind.HandEmptyAtEnqueue
            ? null
            : effect.Condition;
        var count = effect.CountKind is null
            or PrototypeCombatCountKind.TargetDebuffs
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
            PrototypeEffectTarget.SourcePowerOwnerEnemy =>
                sourcePowerEnemyId is null
                    ? throw new InvalidOperationException(
                        "Source-power-owner targeting requires an enemy power source.")
                    : new int?[] { sourcePowerEnemyId.Value },
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
                    (effect.UseSourcePowerStoredValue
                        ? sourcePowerStoredValue
                        : effect.AmountAt(upgradeLevel, energySpent))
                        + (effect.AmountPerPowerStack * powerStacks)
                        + ((effect.AmountPerCount
                            + (effect.AmountPerCountUpgradeDelta * upgradeLevel))
                            * (effect.CountKind == PrototypeCombatCountKind.TargetDebuffs
                                ? ResolveCombatCount(
                                    PrototypeCombatCountKind.TargetDebuffs,
                                    combat, targetEnemyId)
                                : count)),
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
                                + (effect.Selection.MaxSelectionsUpgradeDelta
                                    * upgradeLevel),
                            CopiesPerSelection = effect.Selection.CopiesPerSelection
                                + effect.Selection.CopiesPerSelectionUpgradeDelta * upgradeLevel
                        },
                    effect.CardId,
                    effect.PowerId,
                    effect.GeneratedCardUpgradeLevel
                        + (effect.GeneratedCardUpgradePerSourceUpgrade * upgradeLevel),
                    TargetMode: effect.Target,
                    SourceKind: sourceKind,
                    IsPoweredAttack: isPoweredAttack
                        && effect.Kind is
                            PrototypeCombatEffectKind.DamageEnemy
                            or PrototypeCombatEffectKind.DamageAllEnemiesRepeatPerKill,
                    Condition: queuedCondition,
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
                    PowerCardPayload: powerCardPayload?.Fork(),
                    GeneratedCardEnchantment:
                        effect.GeneratedCardEnchantment is null
                            ? null
                            : effect.GeneratedCardEnchantment with { },
                    AutoPlaySourceZone:
                        effect.AutoPlaySourceZone,
                    RequiredCardTag:
                        effect.RequiredCardTag,
                    UpgradeAutoPlayedCardsBeforePlay:
                        effect.UpgradeAutoPlayedCardsOnSourceUpgrade
                        && upgradeLevel > 0,
                    ExtraCardRewardsOnFatal:
                        effect.ExtraCardRewardsOnFatal,
                    GoldOnFatal:
                        effect.GoldOnFatal
                        + (effect.GoldOnFatalUpgradeDelta * upgradeLevel),
                    GainBlockEqualToAttackDamage:
                        effect.GainBlockEqualToAttackDamage,
                    SplashUnpoweredAttackToOtherEnemies:
                        effect.SplashUnpoweredAttackToOtherEnemies,
                    AutoPlayRequiredCardType:
                        effect.AutoPlayRequiredCardType,
                    AutoPlayFallbackToUnplayable:
                        effect.AutoPlayFallbackToUnplayable,
                    SourcePowerApplicationOrder:
                        sourcePowerApplicationOrder,
                    PowerStoredValue:
                        effect.PowerStoredValue
                        + (effect.PowerStoredValueUpgradeDelta * upgradeLevel),
                    PlayerPowerOnFatalId:
                        effect.PlayerPowerOnFatalId,
                    PlayerPowerOnFatalAmount:
                        effect.PlayerPowerOnFatalAmount,
                    GeneratedChoiceCardType:
                        effect.GeneratedChoiceCardType,
                    GeneratedChoiceCardsFreeThisTurn:
                        effect.GeneratedChoiceCardsFreeThisTurn,
                    GeneratedChoiceCardsUpgraded:
                        effect.GeneratedChoiceCardsUpgraded
                        || (effect.GeneratedChoiceCardsUpgradeWithSource
                            && upgradeLevel > 0),
                    GeneratedChoiceMustPick:
                        effect.GeneratedChoiceMustPick,
                    SelectedCardTemporaryCost:
                        effect.SelectedCardTemporaryCost is null
                            ? null
                            : effect.SelectedCardTemporaryCost with { }));
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

                    var fatalEligible =
                        (operation.ExtraCardRewardsOnFatal > 0
                            || operation.GoldOnFatal > 0
                            || (operation.PlayerPowerOnFatalId is not null
                                && operation.PlayerPowerOnFatalAmount != 0))
                        && ShouldEnemyDeathTriggerFatal(
                            combat,
                            targetEnemyId.Value);

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
                                if (player.Hp <= 0)
                                {
                                    var prevented =
                                        TryUseAutomaticDeathPrevention(
                                            player,
                                            combat,
                                            rng,
                                            eventDepth);
                                    player = prevented.Player;
                                    combat = prevented.Combat;
                                }
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

                    var splashTarget = combat.Enemies.FirstOrDefault(e =>
                        e.InstanceId == targetEnemyId.Value);
                    var overkill = splashTarget is null ? 0
                        : Math.Max(0, Math.Max(0, damageAmount - splashTarget.Block)
                            - splashTarget.Hp);
                    var damageResult = DamageEnemyInternal(
                        combat,
                        targetEnemyId.Value,
                        damageAmount,
                        operation.IsPoweredAttack
                            ? MinimumPoweredAttackHpLoss(player) : 0,
                        operation.IsPoweredAttack);
                    combat = damageResult.Combat;
                    // CurlUpPower records the attack's physical card source
                    // even on a fully blocked hit, then fires AFTER that
                    // complete card play, including all of its hit repeats.
                    if (operation.IsPoweredAttack
                        && sourceCardInstanceId is { } attackCardId
                        && splashTarget is { Hp: > 0 })
                    {
                        combat = combat with
                        {
                            Enemies = combat.Enemies.Select(enemy =>
                                enemy.InstanceId == targetEnemyId.Value
                                && enemy.Hp > 0
                                    ? enemy with
                                    {
                                        Powers = enemy.PowerStates.Select(power =>
                                            power.PendingAttackingCardInstanceId is null
                                            && PrototypeContent.Power(power.PowerId)
                                                .EnemyBlockAfterAttackingCardPlayedPerStack > 0
                                                ? power with
                                                {
                                                    PendingAttackingCardInstanceId =
                                                        attackCardId
                                                }
                                                : power).ToArray()
                                    } : enemy).ToArray()
                        };
                    }
                    if (splashTarget is { Block: > 0 } && damageAmount >= splashTarget.Block)
                        combat = ApplyPlayerEnemyBlockBrokenRelics(player, combat, targetEnemyId.Value);
                    if (operation.SplashUnpoweredAttackToOtherEnemies
                        && splashTarget is { Hp: > 0 })
                    {
                        // Omnislice: powered primary hit, then separate
                        // unpowered Move-damage to the other enemies.
                        var echoAmount = Math.Max(0, damageAmount + overkill);
                        operations = new Queue<PrototypeQueuedOperation>(
                            combat.Enemies
                                .Where(enemy => enemy.Hp > 0
                                    && enemy.InstanceId != targetEnemyId.Value)
                                .Select(enemy => new PrototypeQueuedOperation(
                                    PrototypeCombatEffectKind.DamageEnemy,
                                    echoAmount, TargetEnemyId: enemy.InstanceId))
                                .Concat(operations));
                    }

                    if (operation.GainBlockEqualToAttackDamage)
                    {
                        // Fisticuffs uses the attack result (including
                        // blocked and overkill damage) as the Block source.
                        // Target was alive before resolution of this hit.
                        var block = ModifyPlayerBlockGain(
                            combat, Math.Max(0, damageAmount),
                            fromCard: true, sourceCardInstanceId);
                        combat = combat with
                        {
                            PlayerBlock = combat.PlayerBlock + Math.Max(0, block)
                        };
                    }
                    if (operation.IsPoweredAttack
                        && sourceCardInstanceId is not null
                        && damageResult.DamageDealt > 0)
                    {
                        var reactiveEnemies = combat.Enemies
                            .Select(item => item.Fork()).ToArray();
                        var reactiveIndex = Array.FindIndex(
                            reactiveEnemies,
                            item => item.InstanceId == targetEnemyId.Value);
                        if (reactiveIndex >= 0)
                        {
                            var target = reactiveEnemies[reactiveIndex];
                            if (target.Hp > 0
                                && !target.GainedReactiveBlockThisTurn)
                            {
                                var reactiveBlock = target.PowerStates.Sum(power =>
                                    power.Stacks * PrototypeContent.Power(power.PowerId)
                                        .EnemyBlockAfterFirstUnblockedCardAttackPerStack);
                                if (reactiveBlock > 0)
                                {
                                    reactiveEnemies[reactiveIndex] = target with
                                    {
                                        Block = target.Block + reactiveBlock,
                                        GainedReactiveBlockThisTurn = true
                                    };
                                    combat = combat with { Enemies = reactiveEnemies };
                                }
                            }
                        }
                    }
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
                        combat = ResolveEnemyDeathSummons(
                            combat,
                            rng);

                        if (fatalEligible)
                        {
                            if (operation.GoldOnFatal > 0)
                            {
                                player = player with
                                {
                                    Gold = checked(player.Gold + operation.GoldOnFatal)
                                };
                            }

                            if (operation.ExtraCardRewardsOnFatal > 0)
                            {
                                combat = combat with
                                {
                                    ExtraCardRewardsEarned =
                                        combat.ExtraCardRewardsEarned
                                        + operation.ExtraCardRewardsOnFatal
                                };
                            }

                            if (operation.PlayerPowerOnFatalId is
                                    { } fatalPowerId
                                && operation.PlayerPowerOnFatalAmount != 0)
                            {
                                combat = ApplyPlayerPower(
                                    combat,
                                    fatalPowerId,
                                    operation.PlayerPowerOnFatalAmount);
                            }
                        }
                    }

                    break;

                case PrototypeCombatEffectKind.DamageAllEnemiesRepeatPerKill:
                {
                    var pendingRounds = 1;
                    while (pendingRounds > 0 && player.Hp > 0)
                    {
                        pendingRounds--;
                        var roundTargets = combat.Enemies
                            .Where(enemy => enemy.Hp > 0)
                            .Select(enemy => enemy.InstanceId)
                            .ToArray();

                        foreach (var enemyId in roundTargets)
                        {
                            var enemyBeforeHit = combat.Enemies
                                .Single(enemy => enemy.InstanceId == enemyId);
                            if (enemyBeforeHit.Hp <= 0)
                            {
                                continue;
                            }

                            if (operation.IsPoweredAttack)
                            {
                                var retaliation = EnemyAttackRetaliation(
                                    combat,
                                    enemyId);
                                if (retaliation > 0)
                                {
                                    var absorbed = Math.Min(
                                        combat.PlayerBlock,
                                        retaliation);
                                    combat = combat with
                                    {
                                        PlayerBlock =
                                            combat.PlayerBlock - absorbed
                                    };
                                    player = player with
                                    {
                                        Hp = Math.Max(
                                            0,
                                            player.Hp
                                            - Math.Max(
                                                0,
                                                retaliation - absorbed))
                                    };
                                    if (player.Hp <= 0)
                                    {
                                        var prevented =
                                            TryUseAutomaticDeathPrevention(
                                                player,
                                                combat,
                                                rng,
                                                eventDepth);
                                        player = prevented.Player;
                                        combat = prevented.Combat;
                                        if (player.Hp <= 0)
                                        {
                                            break;
                                        }
                                    }
                                }
                            }

                            var echoDamageAmount = operation.Amount;
                            if (operation.IsPoweredAttack)
                            {
                                echoDamageAmount = ModifyPlayerAttackDamage(
                                    combat,
                                    sourceCardInstanceId,
                                    enemyId,
                                    echoDamageAmount);
                                echoDamageAmount = ModifyIncomingAttackDamage(
                                    combat,
                                    enemyId,
                                    echoDamageAmount);
                            }

                            var echoTargetBlock = combat.Enemies.Single(enemy => enemy.InstanceId == enemyId).Block;
                            var echoDamageResult = DamageEnemyInternal(
                                combat,
                                enemyId,
                                echoDamageAmount,
                                operation.IsPoweredAttack
                                    ? MinimumPoweredAttackHpLoss(player) : 0,
                                operation.IsPoweredAttack);
                            combat = echoDamageResult.Combat;
                            if (echoTargetBlock > 0 && echoDamageAmount >= echoTargetBlock)
                                combat = ApplyPlayerEnemyBlockBrokenRelics(player, combat, enemyId);
                            if (echoDamageResult.Defeated)
                            {
                                pendingRounds++;
                                combat =
                                    CleanupSourceBoundPowersForDefeatedEnemies(
                                        combat);
                            }

                            if (echoDamageResult.DamageDealt > 0)
                            {
                                var damaged = DispatchCombatEvent(
                                    player,
                                    combat,
                                    new PrototypeCombatEvent(
                                        PrototypeCombatEventKind.EnemyDamaged,
                                        TargetEnemyId: enemyId,
                                        Amount: echoDamageResult.DamageDealt),
                                    rng,
                                    eventDepth + 1);
                                player = damaged.Player;
                                combat = damaged.Combat;
                            }

                            if (echoDamageResult.Defeated)
                            {
                                var defeated = DispatchCombatEvent(
                                    player,
                                    combat,
                                    new PrototypeCombatEvent(
                                        PrototypeCombatEventKind.EnemyDefeated,
                                        TargetEnemyId: enemyId,
                                        Amount: echoDamageResult.DamageDealt),
                                    rng,
                                    eventDepth + 1);
                                player = defeated.Player;
                                combat = defeated.Combat;
                                combat = ResolveEnemyDeathSummons(
                                    combat,
                                    rng);
                            }
                        }
                    }

                    break;
                }

                case PrototypeCombatEffectKind.LoseEnemyHp:
                {
                    if (targetEnemyId is null)
                    {
                        throw new InvalidOperationException(
                            "Enemy HP-loss operation requires a target.");
                    }

                    var hpLoss = LoseEnemyHp(
                        combat,
                        targetEnemyId.Value,
                        operation.Amount);
                    combat = hpLoss.Combat;
                    if (hpLoss.Defeated)
                    {
                        combat =
                            CleanupSourceBoundPowersForDefeatedEnemies(
                                combat);
                        combat = ResolveEnemyDeathSummons(
                            combat,
                            rng);
                    }

                    break;
                }

                case PrototypeCombatEffectKind.GainPlayerBlock:
                {
                    var modifiedAmount =
                        ModifyPlayerBlockGain(
                            combat,
                            operation.Amount,
                            operation.SourceKind
                                == PrototypeEffectSourceKind.Card,
                            sourceCardInstanceId);
                    combat = combat with
                    {
                        PlayerBlock = combat.PlayerBlock
                            + Math.Max(0, modifiedAmount)
                    };
                    break;
                }

                case PrototypeCombatEffectKind.MultiplyPlayerBlock:
                    combat = combat with
                    {
                        PlayerBlock =
                            combat.PlayerBlock
                            * Math.Max(0, operation.Amount)
                    };
                    break;

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
                        ModifyPlayerBlockGain(
                            combat,
                            baseAmount,
                            operation.SourceKind
                                == PrototypeEffectSourceKind.Card);
                    combat = combat with
                    {
                        PlayerBlock = combat.PlayerBlock
                            + Math.Max(0, modifiedAmount)
                    };
                    break;
                }

                case PrototypeCombatEffectKind.GainToricToughnessBlock:
                {
                    var amount = Math.Max(0, ModifyPlayerBlockGain(
                        combat,
                        operation.Amount,
                        operation.SourceKind
                            == PrototypeEffectSourceKind.Card));
                    if (amount > 0)
                    {
                        combat = combat with
                        {
                            PlayerBlock = combat.PlayerBlock + amount,
                            ToricShields = (combat.ToricShields
                                ?? Array.Empty<PrototypeToricShield>())
                                .Append(new PrototypeToricShield(amount, 2))
                                .ToArray()
                        };
                    }
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
                        ModifyPlayerBlockGain(
                            combat,
                            operation.Amount,
                            operation.SourceKind
                                == PrototypeEffectSourceKind.Card);
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
                    if (drawn.ShuffleSelectionPending)
                    {
                        // Suspend this draw at the exact post-shuffle point.
                        // A chosen card moves to hand before any of the
                        // remaining cards are drawn.
                        operations = new Queue<PrototypeQueuedOperation>(
                            new[]
                            {
                                StratagemChoiceOperation(combat),
                                operation with { Amount = drawn.RemainingCount }
                            }.Concat(operations));
                    }

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

                case PrototypeCombatEffectKind.ExhaustHand:
                {
                    // Glowwater exhausts the entire current hand before its
                    // draw begins. Reuse the ordered event-continuation
                    // dispatcher so CardExhausted hooks run in hand order,
                    // including hooks which suspend for a player choice.
                    var exhausted = (long[])combat.Hand.Clone();
                    combat = combat with
                    {
                        Hand = Array.Empty<long>(),
                        ExhaustPile = combat.ExhaustPile
                            .Concat(exhausted).ToArray()
                    };
                    if (exhausted.Length == 0)
                    {
                        break;
                    }

                    var exhaustEvents = exhausted.Select(id =>
                        new PrototypeCombatEvent(
                            PrototypeCombatEventKind.CardExhausted,
                            SourceCardInstanceId: id,
                            CardId: RequireCombatCard(combat, id).CardId))
                        .ToArray();
                    return ResumeChoiceResolutionContinuation(
                        player, combat, rng,
                        new PrototypeChoiceResolutionContinuationState(
                            SourceCardInstanceId: sourceCardInstanceId,
                            SourceCardDestination: sourceCardDestination,
                            Operations: operations.ToArray(),
                            PendingDiscardEvents: exhaustEvents,
                            PendingSlyCardInstanceIds: Array.Empty<long>(),
                            CompletionEvents: completionEvents
                                ?? Array.Empty<PrototypeCombatEvent>(),
                            CardPlaySeries: cardPlaySeries,
                            MoveSourceCardOnCompletion: moveSourceCardOnCompletion,
                            RemoveSourceCardOnCompletion: removeSourceCardOnCompletion,
                            SourceCardAlreadyMoved: sourceCardAlreadyMoved,
                            EventDispatchContinuation:
                                eventDispatchContinuation?.Fork()),
                        resumeCardPlaySeries: false);
                }

                case PrototypeCombatEffectKind.DiscardHand:
                {
                    var discarded = (long[])combat.Hand.Clone();
                    if (discarded.Length == 0)
                    {
                        break;
                    }

                    var slyCards = discarded
                        .Where(cardInstanceId =>
                            CardHasKeyword(
                                combat,
                                cardInstanceId,
                                PrototypeCardKeyword.Sly))
                        .ToArray();

                    combat = combat with
                    {
                        Hand = Array.Empty<long>(),
                        DiscardPile = combat.DiscardPile
                            .Concat(discarded)
                            .ToArray()
                    };

                    var discardEvents = discarded
                        .Select(cardInstanceId =>
                        {
                            var discardedCard =
                                RequireCombatCard(
                                    combat,
                                    cardInstanceId);
                            return new PrototypeCombatEvent(
                                PrototypeCombatEventKind.CardDiscarded,
                                SourceCardInstanceId:
                                    cardInstanceId,
                                CardId:
                                    discardedCard.CardId);
                        })
                        .ToArray();

                    return ResumeChoiceResolutionContinuation(
                        player,
                        combat,
                        rng,
                        new PrototypeChoiceResolutionContinuationState(
                            SourceCardInstanceId:
                                sourceCardInstanceId,
                            SourceCardDestination:
                                sourceCardDestination,
                            Operations:
                                operations.ToArray(),
                            PendingDiscardEvents:
                                discardEvents,
                            PendingSlyCardInstanceIds:
                                slyCards,
                            CompletionEvents:
                                completionEvents
                                ?? Array.Empty<PrototypeCombatEvent>(),
                            CardPlaySeries:
                                cardPlaySeries,
                            MoveSourceCardOnCompletion:
                                moveSourceCardOnCompletion,
                            RemoveSourceCardOnCompletion:
                                removeSourceCardOnCompletion,
                            SourceCardAlreadyMoved:
                                sourceCardAlreadyMoved,
                            EventDispatchContinuation:
                                eventDispatchContinuation?.Fork()),
                        resumeCardPlaySeries: false);
                }

                case PrototypeCombatEffectKind.CreateDistinctColorlessCardsInHand:
                    combat = AddDistinctGeneratedColorlessCardsToHand(
                        combat, operation.Amount, rng,
                        sourceCardInstanceId is null
                            ? null
                            : RequireCombatCard(
                                combat, sourceCardInstanceId.Value).CardId);
                    break;

                case PrototypeCombatEffectKind.ChooseGeneratedCards:
                {
                    var generated = AddGeneratedChoiceCards(
                        combat,
                        operation.GeneratedChoiceCardType,
                        operation.Amount,
                        operation.GeneratedChoiceCardsFreeThisTurn,
                        operation.GeneratedChoiceCardsUpgraded,
                        rng);
                    combat = generated.Combat;
                    if (generated.CardInstanceIds.Length == 0)
                    {
                        break;
                    }

                    combat = combat with
                    {
                        PendingChoice = new PendingCombatChoiceState(
                            ChoiceId: "select_cards",
                            SourceCardInstanceId: sourceCardInstanceId,
                            SourceCardDestination: sourceCardDestination,
                            Selection: new PrototypeCardSelectionSpec(
                                PrototypeCardZone.ChoicePool,
                                operation.GeneratedChoiceMustPick ? 1 : 0,
                                1,
                                PrototypeCardSelectionResolutionKind
                                    .MoveToHand,
                                RemoveUnselectedFromSource: true),
                            CandidateCardInstanceIds:
                                (long[])generated.CardInstanceIds.Clone(),
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
                            EventDispatchContinuation:
                                eventDispatchContinuation?.Fork())
                    };
                    return (player, combat);
                }

                case PrototypeCombatEffectKind.EmpowerRandomDrawCardReplay:
                {
                    var eligible = combat.DrawPile
                        .Where(id =>
                        {
                            var card = RequireCombatCard(combat, id);
                            var definition = PrototypeContent.Card(card.CardId);
                            return !definition.Unplayable
                                && definition.Type is not
                                    (PrototypeCardType.Curse or PrototypeCardType.Status)
                                && card.ReplayCount == 0
                                && card.Enchantment is not
                                    { Kind: PrototypeCardEnchantmentKind.Spiral
                                        or PrototypeCardEnchantmentKind.Glam };
                        })
                        .ToArray();
                    var preferred = eligible.Where(id =>
                        PrototypeContent.Card(
                            RequireCombatCard(combat, id).CardId).Type
                            is PrototypeCardType.Attack
                                or PrototypeCardType.Skill
                                or PrototypeCardType.Power).ToArray();
                    var pool = preferred.Length > 0 ? preferred : eligible;
                    if (pool.Length > 0)
                    {
                        var selected = pool[PrototypeRng.NextInt(
                            rng, "combat", pool.Length)];
                        combat = combat with
                        {
                            Cards = combat.Cards.Select(card =>
                                card.InstanceId == selected
                                    ? card with
                                    {
                                        ReplayCount = card.ReplayCount
                                            + Math.Max(0, operation.Amount)
                                    }
                                    : card).ToArray()
                        };
                    }
                    break;
                }

                case PrototypeCombatEffectKind.AddRandomZeroCostCardsToHand:
                {
                    var pool = PrototypeContent.RewardCardPool
                        .Where(id =>
                        {
                            var definition = PrototypeContent.Card(id);
                            return definition.MechanicsImplemented
                                && definition.CanBeGeneratedInCombat
                                && !definition.MultiplayerOnly
                                && definition.Cost.Kind == PrototypeCardCostKind.Fixed
                                && definition.Cost.Amount == 0;
                        }).ToArray();
                    if (pool.Length == 0 && operation.Amount > 0)
                    {
                        throw new NotSupportedException(
                            "No implemented zero-cost Silent card generation candidates.");
                    }
                    for (var n = 0; n < operation.Amount; n++)
                    {
                        var picked = pool[PrototypeRng.NextInt(
                            rng, "combat", pool.Length)];
                        combat = AddGeneratedCardCopies(combat,
                            new PrototypeCombatCardSnapshot(
                                picked, operation.GeneratedCardUpgradeLevel,
                                PrototypeJson.EmptyObject()), 1);
                    }
                    break;
                }

                case PrototypeCombatEffectKind.MoveRandomRareDrawCardsToHand:
                {
                    // Anointed uses pile insertion rather than a normal draw:
                    // it must preserve card instances and avoid draw triggers.
                    const int maxHandSize = 10;
                    var capacity = Math.Max(0, maxHandSize - combat.Hand.Length);
                    var take = Math.Min(Math.Max(0, operation.Amount), capacity);
                    if (take == 0)
                    {
                        break;
                    }

                    var rareCards = combat.DrawPile
                        .Where(id => PrototypeContent.Card(
                            RequireCombatCard(combat, id).CardId).Rarity
                            == PrototypeCardRarity.Rare)
                        .ToArray();
                    if (rareCards.Length == 0)
                    {
                        break;
                    }

                    PrototypeRng.Shuffle(rng, "combat", rareCards);
                    var selected = rareCards.Take(take).ToArray();
                    var selectedIds = selected.ToHashSet();
                    combat = combat with
                    {
                        DrawPile = combat.DrawPile
                            .Where(id => !selectedIds.Contains(id))
                            .ToArray(),
                        Hand = combat.Hand.Concat(selected).ToArray()
                    };
                    break;
                }

                case PrototypeCombatEffectKind.ChooseCards:
                {
                    var selection = operation.Selection
                        ?? throw new InvalidOperationException("Choose-cards operation has no selection specification.");
                    var candidates = GetZone(combat, selection.SourceZone);
                    if (selection.RequireAttackOrPower)
                        candidates = candidates.Where(id => PrototypeContent.Card(
                            RequireCombatCard(combat, id).CardId).Type
                            is PrototypeCardType.Attack or PrototypeCardType.Power).ToArray();
                    if (selection.RequiredCardType is not null)
                    {
                        candidates = candidates
                            .Where(instanceId =>
                                PrototypeContent.Card(
                                    RequireCombatCard(combat, instanceId).CardId).Type
                                == selection.RequiredCardType.Value)
                            .ToArray();
                    }

                    if (selection.RandomCandidateCount > 0
                        && candidates.Length > selection.RandomCandidateCount)
                    {
                        // Native pile selectors may offer only a random
                        // shortlist. Filter by type before choosing it.
                        var shuffledCandidates = (long[])candidates.Clone();
                        PrototypeRng.Shuffle(rng, "combat", shuffledCandidates);
                        candidates = shuffledCandidates
                            .Take(selection.RandomCandidateCount).ToArray();
                    }

                    if (selection.RequireEnergyCostingCard)
                    {
                        candidates = candidates
                            .Where(instanceId =>
                            {
                                var card =
                                    RequireCombatCard(
                                        combat,
                                        instanceId);
                                var definition =
                                    PrototypeContent.Card(
                                        card.CardId);
                                return !definition.Unplayable
                                    && (definition.Cost.Kind
                                            == PrototypeCardCostKind.X
                                        || (definition.Cost.Kind
                                                == PrototypeCardCostKind.Fixed
                                            && definition.Cost.Amount >= 0));
                            })
                            .ToArray();
                    }

                    const int maxHandSize = 10;
                    var capacity = selection.Resolution
                            == PrototypeCardSelectionResolutionKind.MoveToHand
                        ? Math.Max(0, maxHandSize - combat.Hand.Length)
                        : int.MaxValue;
                    var maxSelectable = Math.Min(
                        Math.Min(selection.MaxSelections, candidates.Length),
                        capacity);
                    if (candidates.Length == 0 || maxSelectable <= 0)
                    {
                        break;
                    }

                    var effective = selection with
                    {
                        MinSelections = Math.Min(selection.MinSelections, maxSelectable),
                        MaxSelections = maxSelectable
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
                            SelectedCardTemporaryCost:
                                operation.SelectedCardTemporaryCost is null
                                    ? null
                                    : operation.SelectedCardTemporaryCost with { },
                            EventDispatchContinuation:
                                eventDispatchContinuation?.Fork())
                    };
                    return (player, combat);
                }

                case PrototypeCombatEffectKind.RemoveEnemyBlock:
                    if (targetEnemyId is null)
                    {
                        throw new InvalidOperationException(
                            "Remove-enemy-block operation requires a target.");
                    }

                    var blockBeforeRemoval = combat.Enemies.Single(enemy => enemy.InstanceId == targetEnemyId.Value).Block;
                    combat = SetEnemyBlock(
                        combat,
                        targetEnemyId.Value,
                        0);
                    if (blockBeforeRemoval > 0)
                        combat = ApplyPlayerEnemyBlockBrokenRelics(player, combat, targetEnemyId.Value);
                    break;

                case PrototypeCombatEffectKind.RemoveEnemyPower:
                    if (targetEnemyId is null
                        || operation.PowerId is null)
                    {
                        throw new InvalidOperationException(
                            "Remove-enemy-power operation requires a target and power ID.");
                    }

                    combat = RemoveEnemyPower(
                        combat,
                        targetEnemyId.Value,
                        operation.PowerId);
                    break;

                case PrototypeCombatEffectKind.TriggerEnemyStatus:
                    if (targetEnemyId is null
                        || operation.StatusId is null)
                    {
                        throw new InvalidOperationException(
                            "Trigger-enemy-status operation requires a target and status ID.");
                    }

                    combat = TriggerEnemyStatus(
                        combat,
                        targetEnemyId.Value,
                        operation.StatusId,
                        rng);
                    break;

                case PrototypeCombatEffectKind.ApplyPlayerPower:
                    if (operation.PowerId is null)
                    {
                        throw new InvalidOperationException("Apply-power operation is missing a power ID.");
                    }

                    combat = ApplyPlayerPower(
                        combat, operation.PowerId, operation.Amount,
                        storedValue: operation.PowerStoredValue);
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

                case PrototypeCombatEffectKind.HealPlayer:
                    player = player with
                    {
                        Hp = Math.Min(
                            player.MaxHp,
                            player.Hp + Math.Max(
                                0,
                                operation.Amount))
                    };
                    break;

                case PrototypeCombatEffectKind.UpgradeHandCards:
                    combat = combat with
                    {
                        Cards = combat.Cards
                            .Select(card =>
                            {
                                if (!combat.Hand.Contains(
                                        card.InstanceId)
                                    || card.UpgradeLevel > 0)
                                {
                                    return card;
                                }

                                var definition =
                                    PrototypeContent.Card(
                                        card.CardId);
                                if (definition.Type is
                                    PrototypeCardType.Status
                                    or PrototypeCardType.Curse)
                                {
                                    return card;
                                }

                                return card with
                                {
                                    UpgradeLevel =
                                        card.UpgradeLevel + 1
                                };
                            })
                            .ToArray()
                    };
                    break;

                case PrototypeCombatEffectKind.DamagePlayer:
                {
                    var incoming =
                        ApplyPlayerIncomingDamageCap(
                            combat,
                            Math.Max(
                                0,
                                operation.Amount));
                    var absorbed = Math.Min(
                        combat.PlayerBlock,
                        incoming);
                    combat = combat with
                    {
                        PlayerBlock =
                            combat.PlayerBlock
                            - absorbed
                    };
                    player = player with
                    {
                        Hp = Math.Max(
                            0,
                            player.Hp
                            - Math.Max(
                                0,
                                incoming - absorbed))
                    };
                    if (player.Hp <= 0)
                    {
                        var prevented =
                            TryUseAutomaticDeathPrevention(
                                player,
                                combat,
                                rng,
                                eventDepth);
                        player = prevented.Player;
                        combat = prevented.Combat;
                    }
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

                case PrototypeCombatEffectKind.SetRandomHandCardEnergyCostUntilTurnEndOrPlayed:
                {
                    var candidates = combat.Hand
                        .Select(instanceId =>
                            RequireCombatCard(
                                combat,
                                instanceId))
                        .Where(card =>
                        {
                            var definition =
                                PrototypeContent.Card(
                                    card.CardId);
                            return !definition.Unplayable
                                && definition.Cost.Kind
                                    == PrototypeCardCostKind.Fixed
                                && ResolveFixedCardEnergyCost(
                                    combat,
                                    card,
                                    definition) > 0;
                        })
                        .ToArray();

                    if (candidates.Length > 0)
                    {
                        var selected = candidates[
                            PrototypeRng.NextInt(
                                rng,
                                "combat",
                                candidates.Length)];
                        combat = SetCardTemporaryEnergyCost(
                            combat,
                            selected.InstanceId,
                            new PrototypeTemporaryCardCost(
                                Math.Max(0, operation.Amount),
                                PrototypeTemporaryCardCostExpiry.EndOfTurn
                                | PrototypeTemporaryCardCostExpiry.WhenPlayed));
                    }

                    break;
                }

                case PrototypeCombatEffectKind.RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed:
                {
                    var upperBound = Math.Max(
                        1,
                        operation.Amount);
                    foreach (var cardInstanceId in combat.Hand)
                    {
                        var card = RequireCombatCard(
                            combat,
                            cardInstanceId);
                        var definition =
                            PrototypeContent.Card(
                                card.CardId);
                        if (definition.Unplayable
                            || definition.Cost.Kind
                                != PrototypeCardCostKind.Fixed
                            || definition.Cost.Amount < 0)
                        {
                            continue;
                        }

                        combat = SetCardTemporaryEnergyCost(
                            combat,
                            cardInstanceId,
                            new PrototypeTemporaryCardCost(
                                PrototypeRng.NextInt(
                                    rng,
                                    "combat",
                                    upperBound),
                                PrototypeTemporaryCardCostExpiry.EndOfTurn
                                | PrototypeTemporaryCardCostExpiry.WhenPlayed));
                    }

                    break;
                }

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

                case PrototypeCombatEffectKind.CreateRandomCharacterAttackCardsInHand:
                case PrototypeCombatEffectKind.CreateRandomCharacterSkillCardsInHand:
                {
                    var skills = operation.Kind == PrototypeCombatEffectKind.CreateRandomCharacterSkillCardsInHand;
                    var sourcePool = skills ? PrototypeContent.NativeSilentCardPool : PrototypeContent.RewardCardPool;
                    var pool = sourcePool.Where(id =>
                    {
                        var definition = PrototypeContent.Card(id);
                        return definition.Type == (skills ? PrototypeCardType.Skill : PrototypeCardType.Attack)
                            && definition.MechanicsImplemented
                            && definition.CanBeGeneratedInCombat
                            && definition.Rarity is PrototypeCardRarity.Common
                                or PrototypeCardRarity.Uncommon or PrototypeCardRarity.Rare
                            && !definition.MultiplayerOnly;
                    }).ToArray();
                    if (pool.Length == 0 && operation.Amount > 0)
                    {
                        throw new NotSupportedException(
                            "No implemented solo character card generation pool.");
                    }
                    for (var i = 0; i < operation.Amount; i++)
                    {
                        var id = pool[PrototypeRng.NextInt(
                            rng, "combat", pool.Length)];
                        combat = AddGeneratedCardCopies(combat,
                            new PrototypeCombatCardSnapshot(
                                id, 0, PrototypeJson.EmptyObject(),
                                TemporaryEnergyCost: skills
                                    ? new(0, PrototypeTemporaryCardCostExpiry.EndOfTurn)
                                    : null), 1);
                    }
                    break;
                }

                case PrototypeCombatEffectKind.CreateDistinctCharacterCommonCardsInHand:
                {
                    var count = operation.SourcePowerApplicationOrder is { } order
                        ? combat.PlayerPowers.Single(power => power.ApplicationOrder == order).StoredValue
                        : operation.Amount;
                    if (count <= 0) break;
                    var pool = PrototypeContent.NativeSilentCardPool.Where(id =>
                    {
                        var definition = PrototypeContent.Card(id);
                        return definition.Rarity == PrototypeCardRarity.Common
                            && definition.MechanicsImplemented && definition.CanBeGeneratedInCombat
                            && !definition.MultiplayerOnly;
                    }).ToArray();
                    // Native TakeRandom shuffles the whole filtered pool before
                    // taking a distinct prefix, even for a single generated card.
                    PrototypeRng.Shuffle(rng, "combat", pool);
                    foreach (var id in pool.Take(count))
                        combat = AddGeneratedCardCopies(combat,
                            new PrototypeCombatCardSnapshot(id, 0, PrototypeJson.EmptyObject()), 1);
                    break;
                }

                case PrototypeCombatEffectKind.AcquireRandomCombatPotion:
                {
                    var pool = PrototypeContent.PotionPool;
                    for (var i = 0; i < operation.Amount; i++)
                    {
                        var generated = pool[PrototypeRng.NextInt(
                            rng, "combat", pool.Length)];
                        var slots = (PotionInstance?[])player.PotionSlots.Clone();
                        var firstEmpty = Array.FindIndex(slots, item => item is null);
                        if (firstEmpty < 0)
                        {
                            continue;
                        }

                        var potion = new PotionInstance(
                            generated, PrototypeJson.EmptyObject());
                        slots[firstEmpty] = potion;
                        player = player with { PotionSlots = slots };
                        combat = combat with
                        {
                            Potions = combat.PotionStates
                                .Append(new CombatPotionState(
                                    firstEmpty, generated,
                                    potion.PersistentState.Clone()))
                                .ToArray()
                        };
                    }
                    break;
                }

                case PrototypeCombatEffectKind.IncreaseSourcePowerStacks:
                {
                    if (operation.SourcePowerApplicationOrder is not { } order)
                    {
                        throw new InvalidOperationException(
                            "Power growth requires source application order.");
                    }

                    combat = combat with
                    {
                        PlayerPowers = combat.PlayerPowers.Select(power =>
                            power.ApplicationOrder == order
                                ? power with
                                {
                                    Stacks = power.Stacks
                                        + operation.Amount
                                }
                                : power).ToArray()
                    };
                    break;
                }

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
                            PrototypeJson.EmptyObject(),
                            Enchantment:
                                operation.GeneratedCardEnchantment),
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

                case PrototypeCombatEffectKind.CreateEventSourceCardCopyInHand:
                {
                    if (eventSourceCardInstanceId is null)
                    {
                        throw new InvalidOperationException(
                            "Event-source card copy requires a source card.");
                    }

                    var source = RequireCombatCard(
                        combat,
                        eventSourceCardInstanceId.Value);
                    combat = AddGeneratedCardCopies(
                        combat,
                        new PrototypeCombatCardSnapshot(
                            source.CardId,
                            source.UpgradeLevel,
                            source.State.Clone(),
                            source.CombatEnergyCostDelta,
                            source.TemporaryEnergyCost is null
                                ? null
                                : source.TemporaryEnergyCost with { },
                            source.KeywordOverrides is null
                                ? null
                                : source.KeywordOverrides
                                    .Select(item => item with { })
                                    .ToArray(),
                            source.ReplayCount,
                            source.Enchantment,
                            source.EnchantmentTriggeredThisCombat,
                            source.Affliction,
                            source.SuppressedUpgradeLevels),
                        operation.Amount);
                    break;
                }

                case PrototypeCombatEffectKind.AutoPlayTaggedCardsFromZone:
                {
                    if (operation.AutoPlaySourceZone is null
                        || operation.RequiredCardTag is null)
                    {
                        throw new InvalidOperationException(
                            "Tagged autoplay requires a source zone and card tag.");
                    }

                    var sourceZone =
                        operation.AutoPlaySourceZone.Value;
                    var autoPlayIds = GetZone(
                            combat,
                            sourceZone)
                        .Where(instanceId =>
                        {
                            var candidate =
                                RequireCombatCard(
                                    combat,
                                    instanceId);
                            var candidateDefinition =
                                PrototypeContent.Card(
                                    candidate.CardId);
                            return (candidateDefinition.Tags
                                    ?? Array.Empty<string>())
                                .Contains(
                                    operation.RequiredCardTag,
                                    StringComparer.Ordinal);
                        })
                        .ToArray();

                    if (autoPlayIds.Length == 0)
                    {
                        break;
                    }

                    var autoPlaySet =
                        autoPlayIds.ToHashSet();
                    combat = SetZone(
                        combat,
                        sourceZone,
                        GetZone(combat, sourceZone)
                            .Where(id =>
                                !autoPlaySet.Contains(id))
                            .ToArray());
                    combat = SetZone(
                        combat,
                        PrototypeCardZone.PlayPile,
                        combat.PlayCardIds
                            .Concat(autoPlayIds)
                            .ToArray());

                    operations = new Queue<
                        PrototypeQueuedOperation>(
                        autoPlayIds.Select(id =>
                            new PrototypeQueuedOperation(
                                PrototypeCombatEffectKind
                                    .AutoPlayCombatCard,
                                0,
                                TargetEnemyId:
                                    targetEnemyId,
                                SourceKind:
                                    operation.SourceKind,
                                UpgradeAutoPlayedCardsBeforePlay:
                                    operation
                                        .UpgradeAutoPlayedCardsBeforePlay,
                                CardInstanceId: id))
                        .Concat(operations));
                    break;
                }

                case PrototypeCombatEffectKind.AutoPlayRandomCardsFromZone:
                {
                    if (operation.Amount <= 0
                        || AllEnemiesDefeated(combat))
                    {
                        break;
                    }

                    var zone = operation.AutoPlaySourceZone
                        ?? throw new InvalidOperationException(
                            "Random autoplay requires a source pile.");
                    var ids = GetZone(combat, zone);
                    var eligible = ids.Where(id =>
                    {
                        var definition = PrototypeContent.Card(
                            RequireCombatCard(combat, id).CardId);
                        return !definition.Unplayable
                            && definition.MechanicsImplemented
                            && (operation.AutoPlayRequiredCardType is null
                                || definition.Type
                                    == operation.AutoPlayRequiredCardType);
                    }).ToArray();
                    if (eligible.Length == 0
                        && operation.AutoPlayFallbackToUnplayable)
                    {
                        eligible = ids;
                    }

                    if (eligible.Length == 0)
                    {
                        break;
                    }

                    var selected = eligible[PrototypeRng.NextInt(
                        rng, "combat", eligible.Length)];
                    combat = SetZone(combat, zone,
                        ids.Where(id => id != selected).ToArray());
                    combat = SetZone(combat, PrototypeCardZone.PlayPile,
                        combat.PlayCardIds.Append(selected).ToArray());

                    // Re-evaluate the source pile after each autoplay,
                    // because the played card can draw or mutate cards.
                    operations = new Queue<PrototypeQueuedOperation>(
                        new[]
                        {
                            new PrototypeQueuedOperation(
                                PrototypeCombatEffectKind.AutoPlayCombatCard,
                                0, SourceKind: operation.SourceKind,
                                CardInstanceId: selected),
                            operation with { Amount = operation.Amount - 1 }
                        }.Concat(operations));
                    break;
                }

                case PrototypeCombatEffectKind.AutoPlayTopDrawCards:
                {
                    var count = Math.Max(
                        0,
                        operation.Amount);
                    var draw =
                        combat.DrawPile.ToList();
                    var discard =
                        combat.DiscardPile.ToList();
                    var staged = new List<long>();

                    for (var index = 0;
                         index < count;
                         index++)
                    {
                        if (draw.Count == 0
                            && discard.Count > 0)
                        {
                            var shuffled =
                                discard.ToArray();
                            PrototypeRng.Shuffle(
                                rng,
                                "combat",
                                shuffled);
                            draw.AddRange(shuffled);
                            discard.Clear();
                        }

                        if (draw.Count == 0)
                        {
                            break;
                        }

                        var cardId =
                            draw[^1];
                        draw.RemoveAt(
                            draw.Count - 1);
                        staged.Add(cardId);
                    }

                    if (staged.Count == 0)
                    {
                        break;
                    }

                    combat = combat with
                    {
                        DrawPile = draw.ToArray(),
                        DiscardPile =
                            discard.ToArray(),
                        PlayPile =
                            combat.PlayCardIds
                                .Concat(staged)
                                .ToArray()
                    };

                    operations = new Queue<
                        PrototypeQueuedOperation>(
                        staged.Select(id =>
                            new PrototypeQueuedOperation(
                                PrototypeCombatEffectKind
                                    .AutoPlayCombatCard,
                                0,
                                SourceKind:
                                    operation.SourceKind,
                                CardInstanceId: id))
                        .Concat(operations));
                    break;
                }

                case PrototypeCombatEffectKind.AutoPlayCombatCard:
                {
                    if (operation.CardInstanceId is not
                        { } autoPlayId)
                    {
                        throw new InvalidOperationException(
                            "Autoplay-card operation is missing a card instance.");
                    }

                    if (!combat.PlayCardIds.Contains(
                            autoPlayId))
                    {
                        throw new InvalidOperationException(
                            $"Autoplay card {autoPlayId} is not staged in the play pile.");
                    }

                    var autoPlayCard =
                        RequireCombatCard(
                            combat,
                            autoPlayId);
                    if (operation
                            .UpgradeAutoPlayedCardsBeforePlay
                        && autoPlayCard.UpgradeLevel <= 0)
                    {
                        combat = combat with
                        {
                            Cards = combat.Cards
                                .Select(item =>
                                    item.InstanceId
                                        == autoPlayId
                                        ? item with
                                        {
                                            UpgradeLevel = 1
                                        }
                                        : item)
                                .ToArray()
                        };
                        autoPlayCard =
                            RequireCombatCard(
                                combat,
                                autoPlayId);
                    }

                    var autoPlayDefinition =
                        PrototypeContent.Card(
                            autoPlayCard.CardId);
                    // Unplayable cards have no on-play behavior. The autoplay
                    // stage still moves them to their destination, even when
                    // their *play* mechanics are deliberately unsupported (for
                    // example, persistent quest cards like Dowsing). Validate
                    // playable card mechanics only after this early exit.
                    var autoPlayDestination =
                        CardExhaustsOnUse(
                            autoPlayDefinition,
                            autoPlayCard.UpgradeLevel)
                            ? PrototypeCardZone.ExhaustPile
                            : PrototypeCardZone.DiscardPile;

                    if (autoPlayDefinition.Unplayable)
                    {
                        combat = SetZone(
                            combat,
                            PrototypeCardZone.PlayPile,
                            combat.PlayCardIds
                                .Where(id =>
                                    id != autoPlayId)
                                .ToArray());
                        combat = SetZone(
                            combat,
                            autoPlayDestination,
                            GetZone(
                                    combat,
                                    autoPlayDestination)
                                .Append(autoPlayId)
                                .ToArray());
                        break;
                    }

                    if (!autoPlayDefinition.MechanicsImplemented)
                    {
                        throw new NotSupportedException(
                            $"Autoplay cannot resolve unsupported card '{autoPlayDefinition.Name}'.");
                    }

                    int? autoPlayTarget =
                        operation.TargetEnemyId;
                    if (EffectiveCardTarget(
                            combat,
                            autoPlayDefinition)
                        == PrototypeCardTarget.Enemy)
                    {
                        var liveTargets =
                            combat.Enemies
                                .Where(enemy =>
                                    enemy.Hp > 0)
                                .Select(enemy =>
                                    enemy.InstanceId)
                                .ToArray();
                        if (liveTargets.Length == 0)
                        {
                            combat = SetZone(
                                combat,
                                PrototypeCardZone.PlayPile,
                                combat.PlayCardIds
                                    .Where(id =>
                                        id != autoPlayId)
                                    .ToArray());
                            combat = SetZone(
                                combat,
                                autoPlayDestination,
                                GetZone(
                                        combat,
                                        autoPlayDestination)
                                    .Append(autoPlayId)
                                    .ToArray());
                            break;
                        }

                        if (autoPlayTarget is null
                            || !liveTargets.Contains(
                                autoPlayTarget.Value))
                        {
                            autoPlayTarget =
                                liveTargets[
                                    PrototypeRng.NextInt(
                                        rng,
                                        "combat_targets",
                                        liveTargets.Length)];
                        }
                    }
                    else
                    {
                        autoPlayTarget = null;
                    }

                    combat = SetZone(
                        combat,
                        PrototypeCardZone.PlayPile,
                        combat.PlayCardIds
                            .Where(id =>
                                id != autoPlayId)
                            .ToArray());

                    var autoPlayCount =
                        ResolveCardPlayCountAndConsumeModifiers(
                            combat,
                            autoPlayCard,
                            autoPlayDefinition.Type);
                    combat = autoPlayCount.Combat;
                    var autoPlayXValue =
                        autoPlayDefinition.Cost.Kind
                            == PrototypeCardCostKind.X
                            ? combat.Energy
                                + player.Relics.Sum(relic =>
                                    PrototypeContent.Relic(
                                            relic.RelicId)
                                        .XValueBonus)
                            : 0;

                    var autoPlaySeries =
                        new PrototypeCardPlaySeriesState(
                            SourceCardInstanceId:
                                autoPlayId,
                            SourceCardDestination:
                                autoPlayDestination,
                            TargetEnemyId:
                                autoPlayTarget,
                            EnergySpent:
                                autoPlayXValue,
                            PlayCount:
                                autoPlayCount.PlayCount,
                            NextPlayIndex: 0,
                            RemoveSourceCardOnCompletion:
                                autoPlayDefinition.Type
                                    == PrototypeCardType.Power);

                    var autoPlayed =
                        ResolveCardPlaySeries(
                            player,
                            combat,
                            rng,
                            autoPlaySeries);
                    player = autoPlayed.Player;
                    combat = autoPlayed.Combat;

                    if (combat.PendingChoice
                        is not null)
                    {
                        var outerContinuation =
                            new PrototypeChoiceResolutionContinuationState(
                                SourceCardInstanceId:
                                    sourceCardInstanceId,
                                SourceCardDestination:
                                    sourceCardDestination,
                                Operations:
                                    operations.ToArray(),
                                PendingDiscardEvents:
                                    Array.Empty<
                                        PrototypeCombatEvent>(),
                                PendingSlyCardInstanceIds:
                                    Array.Empty<long>(),
                                CompletionEvents:
                                    completionEvents is null
                                        ? Array.Empty<
                                            PrototypeCombatEvent>()
                                        : (PrototypeCombatEvent[])
                                            completionEvents.Clone(),
                                CardPlaySeries:
                                    cardPlaySeries,
                                MoveSourceCardOnCompletion:
                                    moveSourceCardOnCompletion,
                                RemoveSourceCardOnCompletion:
                                    removeSourceCardOnCompletion,
                                SourceCardAlreadyMoved:
                                    sourceCardAlreadyMoved,
                                EventDispatchContinuation:
                                    eventDispatchContinuation?.Fork());
                        combat =
                            AttachOuterChoiceContinuation(
                                combat,
                                outerContinuation);
                        return (player, combat);
                    }

                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        var sourceExhaustEvent =
            sourceCardInstanceId is not null
            && moveSourceCardOnCompletion
            && !sourceCardAlreadyMoved
            && !removeSourceCardOnCompletion
            && sourceCardDestination
                == PrototypeCardZone.ExhaustPile
                ? new PrototypeCombatEvent(
                    PrototypeCombatEventKind.CardExhausted,
                    SourceCardInstanceId:
                        sourceCardInstanceId.Value,
                    CardId: RequireCombatCard(
                        combat,
                        sourceCardInstanceId.Value)
                        .CardId)
                : null;

        if (sourceCardInstanceId is not null
            && moveSourceCardOnCompletion)
        {
            if (!sourceCardAlreadyMoved
                && !removeSourceCardOnCompletion)
            {
                var finalZone = sourceCardDestination;
                if (finalZone == PrototypeCardZone.DiscardPile)
                {
                    var source = RequireCombatCard(
                        combat, sourceCardInstanceId.Value);
                    var sourceType = PrototypeContent.Card(source.CardId).Type;
                    if (sourceType is PrototypeCardType.Attack
                        or PrototypeCardType.Skill)
                    {
                        var alreadyPlayed = combat.CounterState
                            .AttacksPlayedThisTurn
                            + combat.CounterState.SkillsPlayedThisTurn;
                        if (combat.PlayerPowers.Any(power =>
                            power.Stacks > alreadyPlayed
                            && PrototypeContent.Power(power.PowerId)
                                .PlayedAttacksAndSkillsReturnToDraw))
                        {
                            finalZone = PrototypeCardZone.DrawPile;
                        }
                    }
                }

                var destination = GetZone(combat, finalZone);
                combat = SetZone(
                    combat, finalZone,
                    destination.Append(sourceCardInstanceId.Value).ToArray());
            }

            combat = combat with { PendingChoice = null };
        }

        var completionEventArray =
            sourceExhaustEvent is null
                ? completionEvents
                    ?? Array.Empty<PrototypeCombatEvent>()
                : new[] { sourceExhaustEvent }
                    .Concat(
                        completionEvents
                        ?? Array.Empty<PrototypeCombatEvent>())
                    .ToArray();
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

    /// <summary>
    /// CardPileCmd.AddToCombatAndPreview(..., PileType.Hand) for enemy
    /// status injection. Generated cards are temporary, have no persistent
    /// deck version, and use stable combat instance identifiers.
    /// A full hand cannot accept another card; excess cards are put
    /// into discard by this emulator pending native overflow verification.
    /// </summary>
    private static CombatState AddGeneratedEnemyCardsToHand(
        CombatState combat,
        string cardId,
        int count)
    {
        _ = PrototypeContent.Card(cardId);
        const int maxHandSize = 10;
        for (var index = 0; index < Math.Max(0, count); index++)
        {
            var instance = new CombatCardInstance(
                InstanceId: combat.NextCardInstanceId,
                PersistentCardInstanceId: null,
                CardId: cardId,
                UpgradeLevel: 0,
                IsTemporary: true,
                State: PrototypeJson.EmptyObject());
            instance = ApplyActiveSourceBoundAfflictionToCard(
                combat, instance);
            var fits = combat.Hand.Length < maxHandSize;
            combat = combat with
            {
                NextCardInstanceId = combat.NextCardInstanceId + 1,
                Cards = combat.Cards.Append(instance).ToArray(),
                Hand = fits ? combat.Hand.Append(instance.InstanceId).ToArray()
                    : combat.Hand,
                DiscardPile = fits ? combat.DiscardPile
                    : combat.DiscardPile.Append(instance.InstanceId).ToArray()
            };
        }

        return combat;
    }

    private static CombatState AddGeneratedCardsToDiscard(
        CombatState combat,
        string cardId,
        int count)
    {
        _ = PrototypeContent.Card(cardId);
        for (var index = 0; index < Math.Max(0, count); index++)
        {
            var instance = new CombatCardInstance(
                InstanceId: combat.NextCardInstanceId,
                PersistentCardInstanceId: null,
                CardId: cardId,
                UpgradeLevel: 0,
                IsTemporary: true,
                State: PrototypeJson.EmptyObject());
            instance = ApplyActiveSourceBoundAfflictionToCard(
                combat,
                instance);

            combat = combat with
            {
                NextCardInstanceId = combat.NextCardInstanceId + 1,
                Cards = combat.Cards.Append(instance).ToArray(),
                DiscardPile = combat.DiscardPile
                    .Append(instance.InstanceId)
                    .ToArray()
            };
        }

        return combat;
    }

    private sealed record PrototypeGeneratedChoiceResult(
        CombatState Combat,
        long[] CardInstanceIds);

    private static CombatState AddDistinctGeneratedColorlessCardsToHand(
        CombatState combat, int count, RngBundle rng, string? sourceCardId)
    {
        const int maxHandSize = 10;
        var capacity = Math.Max(0, maxHandSize - combat.Hand.Length);
        var candidates = PrototypeColorlessCards.ImplementedCombatGenerationPool
            .Where(id => !StringComparer.Ordinal.Equals(id, sourceCardId))
            .ToArray();
        if (capacity == 0 || count <= 0 || candidates.Length == 0)
        {
            return combat;
        }

        // Native GetDistinctForCombat uses CombatCardGeneration and a
        // fully-unlocked card pool. Synthetic RNG currently shares the
        // combat stream, and we intentionally omit locked/unimplemented
        // models until the native unlock bridge exists.
        PrototypeRng.Shuffle(rng, "combat", candidates);
        foreach (var cardId in candidates.Take(
                     Math.Min(Math.Min(count, capacity), candidates.Length)))
        {
            var instance = new CombatCardInstance(
                InstanceId: combat.NextCardInstanceId,
                PersistentCardInstanceId: null,
                CardId: cardId,
                UpgradeLevel: 0,
                IsTemporary: true,
                State: PrototypeJson.EmptyObject());
            instance = ApplyActiveSourceBoundAfflictionToCard(
                combat, instance);
            combat = combat with
            {
                NextCardInstanceId = combat.NextCardInstanceId + 1,
                Cards = combat.Cards.Append(instance).ToArray(),
                Hand = combat.Hand.Append(instance.InstanceId).ToArray()
            };
        }

        return combat;
    }

    private static PrototypeGeneratedChoiceResult AddGeneratedChoiceCards(
        CombatState combat,
        PrototypeCardType? cardType,
        int count,
        bool freeThisTurn,
        bool upgraded,
        RngBundle rng)
    {
        var candidates = PrototypeContent.RewardCardPool
            .Where(cardId =>
            {
                var definition = PrototypeContent.Card(cardId);
                return (cardType is null
                        || definition.Type == cardType.Value)
                    && definition.CanBeGeneratedInCombat
                    && definition.MechanicsImplemented
                    && !definition.MultiplayerOnly;
            })
            .ToArray();
        if (candidates.Length == 0 || count <= 0)
        {
            return new PrototypeGeneratedChoiceResult(
                combat,
                Array.Empty<long>());
        }

        PrototypeRng.Shuffle(rng, "combat", candidates);
        var selectedIds = candidates
            .Take(Math.Min(count, candidates.Length))
            .ToArray();
        var generatedIds = new List<long>(
            selectedIds.Length);
        foreach (var cardId in selectedIds)
        {
            var instance = new CombatCardInstance(
                InstanceId: combat.NextCardInstanceId,
                PersistentCardInstanceId: null,
                CardId: cardId,
                UpgradeLevel: upgraded ? 1 : 0,
                IsTemporary: true,
                State: PrototypeJson.EmptyObject(),
                TemporaryEnergyCost:
                    freeThisTurn
                        ? new PrototypeTemporaryCardCost(
                            0,
                            PrototypeTemporaryCardCostExpiry.EndOfTurn
                            | PrototypeTemporaryCardCostExpiry.WhenPlayed)
                        : null);
            instance = ApplyActiveSourceBoundAfflictionToCard(
                combat,
                instance);

            generatedIds.Add(instance.InstanceId);
            combat = combat with
            {
                NextCardInstanceId =
                    combat.NextCardInstanceId + 1,
                Cards = combat.Cards.Append(instance).ToArray(),
                ChoicePool = combat.ChoiceCardIds
                    .Append(instance.InstanceId)
                    .ToArray()
            };
        }

        return new PrototypeGeneratedChoiceResult(
            combat,
            generatedIds.ToArray());
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
                    && (definition.ReplayAnyCardType
                        || definition.ReplayCardType == cardType)
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
        if (series.NextPlayIndex == 0 && !series.RemoveSourceCardOnCompletion
            && series.SourceCardDestination == PrototypeCardZone.DiscardPile
            && combat.PlayerPowers.Any(power => power.Stacks > 0
                && PrototypeContent.Power(power.PowerId).ReturnNextDiscardedCardToDraw))
        {
            // Native result-location hooks run before OnPlay and apply once to
            // the physical card, including autoplay and repeated executions.
            combat = DecrementPlayerPowers(combat, power => power.ReturnNextDiscardedCardToDraw);
            series = series with { SourceCardDestination = PrototypeCardZone.DrawPile };
        }
        var powerApplicationOrderCeiling =
            combat.NextPowerApplicationOrder - 1;

        var operations = new Queue<PrototypeQueuedOperation>();
        foreach (var effect in definition.Effects)
        {
            var effectiveEffect =
                ShouldCardTargetAllEnemies(combat, definition)
                && effect.Kind == PrototypeCombatEffectKind.DamageEnemy
                    ? effect with
                    {
                        Target = PrototypeEffectTarget.AllEnemies
                    }
                    : effect;
            // Native Nimble adds its amount to a block-granting
            // card's base Block before player-wide multipliers.
            if (card.Enchantment is
                    { Kind: PrototypeCardEnchantmentKind.Nimble } nimble
                && effectiveEffect.Kind is
                    PrototypeCombatEffectKind.GainPlayerBlock
                    or PrototypeCombatEffectKind.GainToricToughnessBlock
                    or PrototypeCombatEffectKind.GainPlayerBlockFromEnemyStatusTotal
                    or PrototypeCombatEffectKind.GainPlayerBlockAndApplyPowerFromActualGain)
            {
                effectiveEffect = effectiveEffect with
                {
                    Amount = checked(effectiveEffect.Amount + nimble.Amount)
                };
            }
            EnqueueEffectOperations(
                operations,
                effectiveEffect,
                card.UpgradeLevel,
                series.EnergySpent,
                series.TargetEnemyId,
                combat,
                sourceKind: PrototypeEffectSourceKind.Card,
                isPoweredAttack:
                    definition.Type == PrototypeCardType.Attack);
        }

        EnqueueCardEnchantmentOperations(
            operations,
            card,
            definition,
            series.TargetEnemyId,
            combat);

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
        var powers = new List<PrototypePowerInstanceState>();
        var removed = new List<PrototypePowerInstanceState>();

        foreach (var power in combat.PlayerPowers)
        {
            var definition =
                PrototypeContent.Power(power.PowerId);
            if (!predicate(definition))
            {
                powers.Add(power);
                continue;
            }

            var next = power with
            {
                Stacks = power.Stacks - 1
            };
            if (next.Stacks == 0
                || (next.Stacks < 0
                    && !definition.AllowNegative))
            {
                removed.Add(next);
            }
            else
            {
                powers.Add(next);
            }
        }

        var clearKinds = removed
            .Select(power =>
                PrototypeContent.Power(power.PowerId))
            .Where(definition =>
                definition.ClearAppliedCardAfflictionWhenRemoved
                && definition.AppliedCardAffliction is not null)
            .Select(definition =>
                definition.AppliedCardAffliction!.Value)
            .Where(kind =>
                !powers.Any(power =>
                    PrototypeContent.Power(power.PowerId)
                        .AppliedCardAffliction == kind))
            .ToHashSet();

        return combat with
        {
            PlayerPowers = powers.ToArray(),
            Cards = clearKinds.Count == 0
                ? combat.Cards
                : combat.Cards
                    .Select(card =>
                        card.Affliction is
                            {
                                SourceEnemyInstanceId: null
                            } affliction
                        && clearKinds.Contains(affliction.Kind)
                            ? card with { Affliction = null }
                            : card)
                    .ToArray()
        };
    }

    private static CombatState RemovePlayerPowers(
        CombatState combat,
        Func<PrototypePowerDefinition, bool> predicate)
    {
        var removed = combat.PlayerPowers
            .Where(power =>
                predicate(
                    PrototypeContent.Power(power.PowerId)))
            .ToArray();
        if (removed.Length == 0)
        {
            return combat;
        }

        var removedOrders = removed
            .Select(power => power.ApplicationOrder)
            .ToHashSet();
        var remaining = combat.PlayerPowers
            .Where(power =>
                !removedOrders.Contains(
                    power.ApplicationOrder))
            .ToArray();

        var clearKinds = removed
            .Select(power =>
                PrototypeContent.Power(power.PowerId))
            .Where(definition =>
                definition.ClearAppliedCardAfflictionWhenRemoved
                && definition.AppliedCardAffliction is not null)
            .Select(definition =>
                definition.AppliedCardAffliction!.Value)
            .Where(kind =>
                !remaining.Any(power =>
                    PrototypeContent.Power(power.PowerId)
                        .AppliedCardAffliction == kind))
            .ToHashSet();

        return combat with
        {
            PlayerPowers = remaining,
            Cards = clearKinds.Count == 0
                ? combat.Cards
                : combat.Cards
                    .Select(card =>
                        card.Affliction is
                            {
                                SourceEnemyInstanceId: null
                            } affliction
                        && clearKinds.Contains(affliction.Kind)
                            ? card with { Affliction = null }
                            : card)
                    .ToArray()
        };
    }

    private static int PlayerBlockBonus(CombatState combat) =>
        combat.PlayerPowers.Sum(power =>
            PrototypeContent.Power(power.PowerId).BlockBonusPerStack * power.Stacks);

    private static int ModifyPlayerBlockGain(
        CombatState combat,
        int amount,
        bool fromCard,
        long? sourceCardInstanceId = null)
    {
        var modified = fromCard
            ? ModifyPlayerCardBlock(
                combat,
                amount,
                sourceCardInstanceId)
            : amount;

        foreach (var power in combat.PlayerPowers
                     .Where(power => power.Stacks > 0))
        {
            var definition =
                PrototypeContent.Power(
                    power.PowerId);
            if (definition
                    .PlayerBlockGainDenominatorPerStack
                <= 0)
            {
                throw new InvalidOperationException(
                    $"Power '{definition.Id}' has invalid all-block denominator.");
            }

            for (var stack = 0;
                 stack < power.Stacks;
                 stack++)
            {
                modified =
                    (modified
                     * definition
                         .PlayerBlockGainNumeratorPerStack)
                    / definition
                        .PlayerBlockGainDenominatorPerStack;
            }
        }

        return modified;
    }

    private static int ModifyPlayerCardBlock(
        CombatState combat,
        int amount,
        long? sourceCardInstanceId = null)
    {
        if (sourceCardInstanceId is { } id)
        {
            var tags = PrototypeContent.Card(
                RequireCombatCard(combat, id).CardId).Tags
                ?? Array.Empty<string>();
            amount += combat.PlayerPowers.Where(power => power.Stacks > 0)
                .Sum(power =>
                {
                    var requiredTag = PrototypeContent.Power(
                        power.PowerId).BlockBonusRequiredCardTag;
                    return requiredTag is not null
                        && tags.Contains(requiredTag, StringComparer.Ordinal)
                        ? power.Stacks : 0;
                });
        }

        if (combat.PlayerPowers.Any(power =>
            power.Stacks > 0
            && PrototypeContent.Power(power.PowerId).PreventsCardBlock))
        {
            return 0;
        }

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

    // This is the single damage calculation used by both actual enemy hits
    // and public intent previews. It deliberately does not account for player
    // Block, and never consumes combat RNG.
    internal static int EnemyHitDamage(
        EnemyCombatState enemy,
        CombatState combat,
        PrototypeEnemyEffectSpec effect,
        int act,
        int ascension,
        int priorMoveUses = 0)
    {
        var baseDamage = effect.UseStoredEnemyDamage
            ? enemy.StoredEnemyDamage
            : checked(effect.AmountAt(act, ascension)
                + effect.ExtraAmountPerPriorMoveUse * priorMoveUses);
        var damage = baseDamage
            + enemy.PowerStates.Sum(power =>
                PrototypeContent.Power(power.PowerId)
                    .EnemyAttackDamageBonusPerStack * power.Stacks);
        foreach (var status in enemy.Statuses)
        {
            var definition = PrototypeContent.Status(status.Key);
            damage = (damage * definition.OutgoingDamageNumerator)
                / definition.OutgoingDamageDenominator;
        }

        return effect.IsAttack
            ? ModifyIncomingPlayerAttackDamage(combat, damage)
            : ApplyPlayerIncomingDamageCap(combat, damage);
    }

    private static int ApplyPlayerIncomingDamageCap(
        CombatState combat,
        int amount)
    {
        var caps = combat.PlayerPowers
            .Where(power => power.Stacks > 0)
            .Select(power =>
                PrototypeContent.Power(power.PowerId)
                    .PlayerIncomingDamageCap)
            .Where(cap => cap > 0)
            .ToArray();

        return caps.Length == 0
            ? amount
            : Math.Min(amount, caps.Min());
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

        return ApplyPlayerIncomingDamageCap(
            combat,
            modified);
    }

    private static void EnqueueCardEnchantmentOperations(
        Queue<PrototypeQueuedOperation> operations,
        CombatCardInstance card,
        PrototypeCardDefinition definition,
        int? actionTargetEnemyId,
        CombatState combat)
    {
        if (card.Enchantment is not
            {
                Kind: PrototypeCardEnchantmentKind.Inky
            } inky
            || inky.Amount <= 0)
        {
            return;
        }

        var target = ShouldCardTargetAllEnemies(combat, definition)
            ? PrototypeEffectTarget.AllEnemies
            : PrototypeEffectTarget.ActionTargetEnemy;
        EnqueueEffectOperations(
            operations,
            new PrototypeCombatEffectSpec(
                PrototypeCombatEffectKind.ApplyEnemyStatus,
                inky.Amount,
                StatusId: "proto.status.weak",
                Target: target),
            upgradeLevel: 0,
            energySpent: 0,
            actionTargetEnemyId,
            combat,
            sourceKind: PrototypeEffectSourceKind.Card);
    }

    private static bool ShouldEnemyDeathTriggerFatal(
        CombatState combat,
        int enemyId)
    {
        var enemy = combat.Enemies.SingleOrDefault(
            item => item.InstanceId == enemyId)
            ?? throw new InvalidOperationException(
                $"Enemy {enemyId} is missing.");

        return enemy.PowerStates.All(power =>
            PrototypeContent.Power(power.PowerId)
                .OwnerDeathTriggersFatal);
    }

    private static PrototypeCardTarget EffectiveCardTarget(
        CombatState combat,
        PrototypeCardDefinition definition) =>
        ShouldCardTargetAllEnemies(combat, definition)
            ? PrototypeCardTarget.None
            : definition.Target;

    private static bool ShouldCardTargetAllEnemies(
        CombatState combat,
        PrototypeCardDefinition definition)
    {
        var tags = definition.Tags ?? Array.Empty<string>();
        return combat.PlayerPowers.Any(power =>
        {
            if (power.Stacks <= 0)
            {
                return false;
            }

            var powerDefinition =
                PrototypeContent.Power(power.PowerId);
            return powerDefinition.AllEnemyTargetCardTag is
                    { } requiredTag
                && tags.Contains(
                    requiredTag,
                    StringComparer.Ordinal);
        });
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

    private static bool CanPlayAnotherCardThisTurn(
        PlayerState player,
        CombatState combat,
        CombatCardInstance card)
    {
        // Smoggy disallows playing a Skill while the card carries Smog.
        // Its affliction is cleared at the end of the player's turn.
        if (card.Affliction is { } affliction
            && combat.PlayerPowers.Any(power =>
                power.Stacks > 0
                && PrototypeContent.Power(power.PowerId)
                    .BlockPlayOfMatchingAffliction
                && PrototypeContent.Power(power.PowerId)
                    .SkillPlayAffliction == affliction.Kind))
        {
            return false;
        }

        // Native RingingPower.ShouldPlay checks the Ringing affliction
        // on the candidate card. A card already afflicted by something
        // else never receives Ringing and remains playable even after
        // the first card played that turn. Other cap powers still apply.
        var powerCaps = combat.PlayerPowers
            .Where(power => power.Stacks > 0
                && (power.PowerId != "proto.power.ringing"
                    || card.Affliction?.Kind
                        == PrototypeCardAfflictionKind.Ringing))
            .Select(power =>
                PrototypeContent.Power(power.PowerId)
                    .MaxCardsPlayablePerTurn);
        var relicCaps = player.Relics
            .Select(relic =>
                PrototypeContent.Relic(relic.RelicId)
                    .MaxCardsPlayablePerTurn);
        var caps = powerCaps
            .Concat(relicCaps)
            .Where(cap => cap > 0)
            .ToArray();

        return caps.Length == 0
            || combat.CounterState.CardsPlayedThisTurn
                < caps.Min();
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
        var baseCost = card.TemporaryEnergyCost is not null
            ? card.TemporaryEnergyCost.Cost
            : definition.Cost.AmountAt(
                card.UpgradeLevel,
                ResolveCardCostReductionCount(
                    definition.Cost,
                    combat))
              + card.CombatEnergyCostDelta;

        var afflictionSurcharge = card.Affliction is not
                { } affliction
            ? 0
            : combat.PlayerPowers.Sum(power =>
            {
                var powerDefinition =
                    PrototypeContent.Power(power.PowerId);
                return power.Stacks > 0
                    && powerDefinition.AppliedCardAffliction
                        == affliction.Kind
                        ? powerDefinition
                            .AfflictedCardEnergyCostPerStack
                          * power.Stacks
                        : 0;
            });

        return Math.Max(
            0,
            baseCost + afflictionSurcharge);
    }

    private static CombatState SetCardTemporaryEnergyCost(
        CombatState combat,
        long cardInstanceId,
        PrototypeTemporaryCardCost temporaryCost) =>
        combat with
        {
            Cards = combat.Cards
                .Select(card =>
                    card.InstanceId == cardInstanceId
                        ? card with
                        {
                            TemporaryEnergyCost =
                                temporaryCost with { }
                        }
                        : card)
                .ToArray()
        };

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
                PrototypeContent.Card(card.CardId).Retain
                || (card.UpgradeLevel > 0
                    && PrototypeContent.Card(card.CardId)
                        .RetainOnUpgrade)
                || card.Enchantment?.Kind
                    == PrototypeCardEnchantmentKind.Steady,
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
        CombatState combat,
        int? targetEnemyId = null) =>
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
            PrototypeCombatCountKind.CardsPlayedThisCombat =>
                combat.CounterState.CardsPlayedThisCombat,
            PrototypeCombatCountKind.OtherCardsInHand =>
                combat.Hand.Length,
            PrototypeCombatCountKind.DrawPileCards =>
                combat.DrawPile.Length,
            PrototypeCombatCountKind.DiscardPileCards =>
                combat.DiscardPile.Length,
            PrototypeCombatCountKind.PlayerBlock =>
                combat.PlayerBlock,
            PrototypeCombatCountKind.TargetDebuffs =>
                combat.Enemies.Where(e => e.InstanceId == targetEnemyId
                    && e.Hp > 0).Sum(enemy =>
                    enemy.Statuses.Count(status => status.Value > 0)
                    + enemy.PowerStates.Count(power =>
                        power.Stacks > 0
                        && PrototypeContent.Power(power.PowerId).IsDebuff
                        && !PrototypeContent.Power(power.PowerId)
                            .RemoveAtEnemyTurnEnd)),

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

                counters = counters with
                {
                    CardsPlayedThisTurn =
                        counters.CardsPlayedThisTurn + 1,
                    CardsPlayedThisCombat =
                        counters.CardsPlayedThisCombat + 1,
                    PlayedCardIdsThisTurn =
                        combatEvent.SourceCardInstanceId is { } playedId
                            ? (counters.PlayedCardIdsThisTurn
                                ?? Array.Empty<long>())
                                .Append(playedId).ToArray()
                            : counters.PlayedCardIdsThisTurn
                };

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

        var powers = combat.PlayerPowers;
        if (combatEvent.Kind == PrototypeCombatEventKind.CardPlayed
            && combatEvent.CardId is { } cardId
            && PrototypeContent.Card(cardId).Type == PrototypeCardType.Attack)
        {
            // Vigor lasts through every hit of the next attack, and is
            // consumed when that card's attack has completed.
            powers = powers.Where(power =>
                power.PowerId != "proto.power.vigor").ToArray();
        }

        return combat with { Counters = counters, PlayerPowers = powers };
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
            PrototypeCombatPredicateKind.HandHasNoAttacks =>
                combat.Hand.All(instanceId =>
                    PrototypeContent.Card(
                        RequireCombatCard(combat, instanceId).CardId).Type
                    != PrototypeCardType.Attack),
            PrototypeCombatPredicateKind.HandHasOnlyAttacks =>
                combat.Hand.All(instanceId =>
                    PrototypeContent.Card(
                        RequireCombatCard(combat, instanceId).CardId).Type
                    == PrototypeCardType.Attack),

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
            var generic =
                definition.PlayerAttackDamageBonusPerStack
                * power.Stacks;
            var tagged = 0;
            if (definition.AttackDamageBonusPerStack != 0
                && definition.AttackDamageBonusRequiredCardTag is
                    { } requiredTag
                && tags.Contains(
                    requiredTag,
                    StringComparer.Ordinal))
            {
                tagged =
                    definition.AttackDamageBonusPerStack
                    * power.Stacks;
            }

            return generic + tagged;
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

        // Native SlowPower increments SlowAmount in AfterCardPlayed.
        // Damage during a card's resolution therefore observes only
        // previously completed plays, not the current card.
        var cardsPlayed = combat.CounterState.CardsPlayedThisTurn;
        var percentBonus = enemy.PowerStates.Sum(power =>
            PrototypeContent.Power(power.PowerId)
                .EnemyIncomingAttackDamagePercentPerCardPlayed
            * power.Stacks
            * cardsPlayed);
        if (percentBonus != 0)
        {
            modified =
                (modified * (100 + percentBonus))
                / 100;
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
        int? sourceEnemyInstanceId = null,
        int storedValue = 0)
    {
        var definition = PrototypeContent.Power(powerId);
        if (definition.SourceBoundToEnemy
            && sourceEnemyInstanceId is null)
        {
            throw new InvalidOperationException(
                $"Source-bound power '{powerId}' requires an enemy source.");
        }

        sourceEnemyInstanceId =
            definition.SourceBoundToEnemy
                ? sourceEnemyInstanceId
                : null;

        var isDebuffApplication =
            (definition.IsDebuff && stacks > 0)
            || (definition.NegativeApplicationIsDebuff
                && stacks < 0);
        if (isDebuffApplication)
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
                sourceEnemyInstanceId,
                storedValue);
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
        int? sourceEnemyInstanceId = null,
        int storedValue = 0)
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
                    sourceEnemyInstanceId,
                    StoredValue: storedValue,
                    TriggerCounts: new int[definition.Triggers.Length])).ToArray(),
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
        combat = ApplyPowerCardAffliction(
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

    private static CombatState ApplyPowerCardAffliction(
        CombatState combat,
        PrototypePowerInstanceState power)
    {
        var definition = PrototypeContent.Power(power.PowerId);
        if (definition.AppliedCardAffliction is not
                { } afflictionKind)
        {
            return combat;
        }

        return combat with
        {
            Cards = combat.Cards
                .Select(card =>
                {
                    if (definition
                            .AppliedCardAfflictionRequiredCardType
                            is { } requiredType
                        && PrototypeContent.Card(card.CardId).Type
                            != requiredType)
                    {
                        return card;
                    }

                    if (card.Affliction is not null
                        && definition.SkipCardsWithExistingAffliction)
                    {
                        return card;
                    }

                    return card with
                    {
                        Affliction =
                            new PrototypeCardAffliction(
                                afflictionKind)
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
            if (definition.SourceBoundCardAffliction is
                    { } sourceAfflictionKind
                && power.SourceEnemyInstanceId is
                    { } sourceEnemyId)
            {
                var source = combat.Enemies.FirstOrDefault(enemy =>
                    enemy.InstanceId == sourceEnemyId);
                if (source is not null && source.Hp > 0)
                {
                    if (card.Affliction is not null
                        && definition.SkipCardsWithExistingAffliction)
                    {
                        continue;
                    }

                    return card with
                    {
                        Affliction = new PrototypeCardAffliction(
                            sourceAfflictionKind,
                            SourceEnemyInstanceId: sourceEnemyId)
                    };
                }
            }

            if (definition.SkillPlayAffliction is
                    { } skillAffliction
                && combat.CounterState.SkillsPlayedThisTurn > 0
                && PrototypeContent.Card(card.CardId).Type
                    == PrototypeCardType.Skill
                && card.Affliction is null)
            {
                return card with
                {
                    Affliction = new PrototypeCardAffliction(
                        skillAffliction)
                };
            }

            if (definition.AppliedCardAffliction is
                    { } appliedAfflictionKind)
            {
                if (definition
                        .AppliedCardAfflictionRequiredCardType
                        is { } requiredType
                    && PrototypeContent.Card(card.CardId).Type
                        != requiredType)
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
                        appliedAfflictionKind)
                };
            }
        }

        return card;
    }

    private static CombatState
        CleanupSourceBoundPowersForDefeatedEnemies(
            CombatState combat)
    {
        var leaderHp = combat.Enemies
            .ToDictionary(
                enemy => enemy.InstanceId,
                enemy => enemy.Hp);
        var cleanedEnemies = combat.Enemies
            .Select(enemy =>
                enemy.LeaderEnemyInstanceId is
                        { } leaderId
                    && leaderHp.GetValueOrDefault(
                        leaderId) <= 0
                    ? enemy with { Hp = 0, Block = 0 }
                    : enemy)
            .ToArray();
        combat = combat with
        {
            Enemies = cleanedEnemies
        };

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
            int stacks,
            bool ignoreDebuffPrevention = false)
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

        var isDebuffApplication =
            (definition.IsDebuff && stacks > 0)
            || (definition.NegativeApplicationIsDebuff
                && stacks < 0);
        if (isDebuffApplication && !ignoreDebuffPrevention)
        {
            var blocked =
                TryBlockIncomingEnemyDebuff(enemy);
            enemy = blocked.Enemy;
            if (blocked.Blocked)
            {
                return (combat, enemy);
            }
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
                        Stacks = nextStacks,
                        SkipNextEnemySideTurnEnd =
                            definition.SkipInitialEnemySideTurnEnd
                                && stacks > 0
                            || powers[powerIndex].SkipNextEnemySideTurnEnd
                    };
            }

            var changedEnemy = enemy with { Powers = powers.ToArray() };
            var strengthDelta =
                stacks * definition.EnemyStrengthOnApplyPerStack;
            return strengthDelta == 0
                ? (combat, changedEnemy)
                : ApplyEnemyPowerToState(
                    combat, changedEnemy, "proto.power.strength",
                    strengthDelta, ignoreDebuffPrevention: true);
        }

        if (stacks == 0
            || (!definition.AllowNegative && stacks < 0))
        {
            return (combat, enemy);
        }

        powers.Add(new PrototypePowerInstanceState(
            powerId,
            stacks,
            combat.NextPowerApplicationOrder,
            SkipNextEnemySideTurnEnd:
                definition.SkipInitialEnemySideTurnEnd));

        var nextCombat = combat with
        {
            NextPowerApplicationOrder =
                combat.NextPowerApplicationOrder + 1
        };
        var nextEnemy = enemy with { Powers = powers.ToArray() };
        var appliedStrengthDelta =
            stacks * definition.EnemyStrengthOnApplyPerStack;
        return appliedStrengthDelta == 0
            ? (nextCombat, nextEnemy)
            : ApplyEnemyPowerToState(
                nextCombat, nextEnemy, "proto.power.strength",
                appliedStrengthDelta, ignoreDebuffPrevention: true);
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

    private static bool RelicTriggerMatchesPlayer(
        PrototypeRelicTriggerSpec trigger,
        PlayerState player,
        CombatState combat)
    {
        if (trigger.TurnEquals is { } turn
            && combat.Turn != turn)
        {
            return false;
        }

        if (trigger.MinTurn is { } minTurn
            && combat.Turn < minTurn)
        {
            return false;
        }

        if (trigger.MaxAttacksPlayedLastTurn is { } maxAttacks
            && combat.CounterState.AttacksPlayedLastTurn > maxAttacks)
        {
            return false;
        }

        if (trigger.RequiresZeroPlayerBlock
            && combat.PlayerBlock != 0)
        {
            return false;
        }

        if (player.MaxHp <= 0)
        {
            return trigger.MaxPlayerHpPercent is null
                && trigger.MinPlayerHpPercent is null;
        }

        var hpTimesHundred = player.Hp * 100;
        if (trigger.MaxPlayerHpPercent is { } maxPercent
            && hpTimesHundred > player.MaxHp * maxPercent)
        {
            return false;
        }

        if (trigger.MinPlayerHpPercent is { } minPercent
            && hpTimesHundred < player.MaxHp * minPercent)
        {
            return false;
        }

        return true;
    }

    private static CombatState ResetPerTurnRelicCounters(
        CombatState combat)
    {
        var relics = combat.RelicStates
            .Select(relic => relic.Fork())
            .ToArray();

        for (var relicIndex = 0;
             relicIndex < relics.Length;
             relicIndex++)
        {
            var definition =
                PrototypeContent.Relic(
                    relics[relicIndex].RelicId);
            var triggers =
                definition.Triggers
                ?? Array.Empty<PrototypeRelicTriggerSpec>();
            var counts =
                (int[])relics[relicIndex]
                    .TriggerCounts.Clone();

            for (var triggerIndex = 0;
                 triggerIndex < triggers.Length;
                 triggerIndex++)
            {
                if (triggers[triggerIndex]
                    .ResetCounterEachTurn)
                {
                    counts[triggerIndex] = 0;
                }
            }

            relics[relicIndex] =
                relics[relicIndex] with
                {
                    TriggerCounts = counts
                };
        }

        return combat with { Relics = relics };
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

    // This is keyed by power application order, so multiple Panaches or
    // separate countdowns remain independent and fork deterministically.
    private static (CombatState Combat, bool ShouldTrigger)
        CheckPowerSubscriberTrigger(
            CombatState combat,
            PrototypeEventSubscriberState subscriber)
    {
        if (subscriber.SourcePowerApplicationOrder is not { } order)
        {
            return (combat, false);
        }

        var index = Array.FindIndex(combat.PlayerPowers,
            power => power.ApplicationOrder == order);
        if (index < 0)
        {
            return (combat, false);
        }

        var current = combat.PlayerPowers[index];
        if (subscriber.CountdownBeforeTrigger)
        {
            if (current.Stacks > 1)
            {
                var powers = (PrototypePowerInstanceState[])combat.PlayerPowers.Clone();
                powers[index] = current with { Stacks = current.Stacks - 1 };
                return (combat with { PlayerPowers = powers }, false);
            }

            return (combat, true);
        }

        if (subscriber.EveryNth <= 1)
        {
            return (combat, true);
        }

        if (subscriber.PowerTriggerIndex is not { } triggerIndex)
        {
            throw new InvalidOperationException("Power trigger counter has no index.");
        }

        var counts = current.TriggerCounts is null
            ? new int[PrototypeContent.Power(current.PowerId).Triggers.Length]
            : (int[])current.TriggerCounts.Clone();
        var nextCount = checked(counts[triggerIndex] + 1);
        // Counter-style native powers restart at zero after their Nth
        // trigger; saving the residue also bounds the persisted state.
        counts[triggerIndex] = nextCount % subscriber.EveryNth;
        var updated = (PrototypePowerInstanceState[])combat.PlayerPowers.Clone();
        updated[index] = current with { TriggerCounts = counts };
        return (combat with { PlayerPowers = updated },
            counts[triggerIndex] == 0);
    }

    private static CombatState ResetPlayerPowerTriggerCounters(CombatState combat) =>
        combat with
        {
            PlayerPowers = combat.PlayerPowers.Select(power =>
                PrototypeContent.Power(power.PowerId)
                    .ResetTriggerCountersAtPlayerTurnEnd
                    && power.TriggerCounts is not null
                        ? power with
                        {
                            TriggerCounts = new int[power.TriggerCounts.Length]
                        }
                        : power).ToArray()
        };


    // Both hooks run at the native AfterCardPlayed boundary: after the
    // effects of the card have resolved, before the next card action.
    // Tender restores only Strength/Dexterity actually lost (Artifact can
    // block either application), with counters stored on the power instance.
    // Curl Up waits for the *same card instance* that first attacked it.
    private static CombatState ResolveNativeCardCompletionPowers(
        CombatState combat, PrototypeCombatEvent combatEvent)
    {
        if (combatEvent.Kind == PrototypeCombatEventKind.PlayerTurnEnded)
        {
            foreach (var tender in combat.PlayerPowers.Where(power =>
                PrototypeContent.Power(power.PowerId)
                    .PlayerStrengthDexterityLossPerCardPlayedPerStack > 0)
                .ToArray())
            {
                var reduced = tender.TriggerCounts ?? Array.Empty<int>();
                if (reduced.Length >= 2)
                {
                    if (reduced[0] > 0)
                        combat = ApplyPlayerPower(combat,
                            "proto.power.strength", reduced[0]);
                    if (reduced[1] > 0)
                        combat = ApplyPlayerPower(combat,
                            "proto.power.dexterity", reduced[1]);
                }
                combat = combat with
                {
                    PlayerPowers = combat.PlayerPowers.Select(power =>
                        power.ApplicationOrder == tender.ApplicationOrder
                            ? power with { TriggerCounts = null,
                                StoredValue = 0 }
                            : power).ToArray()
                };
            }
            return combat;
        }

        if (combatEvent.Kind != PrototypeCombatEventKind.CardPlayed
            || combatEvent.CardId is null)
            return combat;

        foreach (var tender in combat.PlayerPowers.Where(power =>
            power.Stacks > 0
            && PrototypeContent.Power(power.PowerId)
                .PlayerStrengthDexterityLossPerCardPlayedPerStack > 0)
            .ToArray())
        {
            var amount = powerLoss(tender);
            var beforeStrength = combat.PlayerPowers
                .Where(power => power.PowerId == "proto.power.strength")
                .Sum(power => power.Stacks);
            combat = ApplyPlayerPower(combat, "proto.power.strength", -amount);
            var afterStrength = combat.PlayerPowers
                .Where(power => power.PowerId == "proto.power.strength")
                .Sum(power => power.Stacks);
            var beforeDexterity = combat.PlayerPowers
                .Where(power => power.PowerId == "proto.power.dexterity")
                .Sum(power => power.Stacks);
            combat = ApplyPlayerPower(combat, "proto.power.dexterity", -amount);
            var afterDexterity = combat.PlayerPowers
                .Where(power => power.PowerId == "proto.power.dexterity")
                .Sum(power => power.Stacks);
            var previous = tender.TriggerCounts ?? new int[2];
            combat = combat with
            {
                PlayerPowers = combat.PlayerPowers.Select(power =>
                    power.ApplicationOrder == tender.ApplicationOrder
                        ? power with
                        {
                            TriggerCounts = new[]
                            {
                                previous[0] + beforeStrength - afterStrength,
                                previous[1] + beforeDexterity - afterDexterity
                            },
                            StoredValue = power.StoredValue + 1
                        } : power).ToArray()
            };
        }

        if (combatEvent.SourceCardInstanceId is { } cardId)
        {
            combat = combat with
            {
                Enemies = combat.Enemies.Select(enemy =>
                {
                    if (enemy.Hp <= 0)
                        return enemy;
                    var completing = enemy.PowerStates.Where(power =>
                        power.PendingAttackingCardInstanceId == cardId
                        && PrototypeContent.Power(power.PowerId)
                            .EnemyBlockAfterAttackingCardPlayedPerStack > 0)
                        .ToArray();
                    if (completing.Length == 0)
                        return enemy;
                    var block = completing.Sum(power => power.Stacks
                        * PrototypeContent.Power(power.PowerId)
                            .EnemyBlockAfterAttackingCardPlayedPerStack);
                    var orders = completing.Select(power =>
                        power.ApplicationOrder).ToHashSet();
                    return enemy with
                    {
                        Block = enemy.Block + block,
                        Powers = enemy.PowerStates.Where(power =>
                            !orders.Contains(power.ApplicationOrder)).ToArray()
                    };
                }).ToArray()
            };
        }
        return combat;

        static int powerLoss(PrototypePowerInstanceState power) =>
            power.Stacks * PrototypeContent.Power(power.PowerId)
                .PlayerStrengthDexterityLossPerCardPlayedPerStack;
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

        if (combatEvent.Kind
            == PrototypeCombatEventKind.PlayerTurnStarted)
        {
            combat = ResetPerTurnRelicCounters(combat);
        }

        combat = RecordCombatCounterEvent(combat, combatEvent);
        combat = ResolveNativeCardCompletionPowers(combat, combatEvent);
        if (combatEvent.Kind == PrototypeCombatEventKind.PlayerTurnEnded)
        {
            var plating = combat.PlayerPowers.Sum(power =>
                Math.Max(0, power.Stacks)
                * PrototypeContent.Power(power.PowerId)
                    .PlayerBlockAtTurnEndPerStack);
            if (plating > 0)
            {
                combat = combat with
                {
                    PlayerBlock = combat.PlayerBlock
                        + Math.Max(0, ModifyPlayerBlockGain(
                            combat, plating, fromCard: false))
                };
            }
        }

        if (combatEvent.Kind == PrototypeCombatEventKind.CardPlayed
            && combatEvent.CardId is { } playedCardId
            && PrototypeContent.Card(playedCardId).Type
                == PrototypeCardType.Skill)
        {
            foreach (var power in combat.PlayerPowers)
            {
                var definition = PrototypeContent.Power(power.PowerId);
                if (power.Stacks > 0
                    && definition.SkillPlayAffliction is { } kind)
                {
                    combat = combat with
                    {
                        Cards = combat.Cards.Select(card =>
                            PrototypeContent.Card(card.CardId).Type
                                == PrototypeCardType.Skill
                            && card.Affliction is null
                                ? card with
                                {
                                    Affliction = new PrototypeCardAffliction(kind)
                                }
                                : card).ToArray()
                    };
                }
            }
        }

        if (combatEvent.Kind == PrototypeCombatEventKind.PlayerTurnEnded)
        {
            var clearKinds = combat.PlayerPowers
                .Where(power => power.Stacks > 0
                    && PrototypeContent.Power(power.PowerId)
                        .ClearMatchingAfflictionAtPlayerTurnEnd)
                .Select(power => PrototypeContent.Power(power.PowerId)
                    .SkillPlayAffliction)
                .OfType<PrototypeCardAfflictionKind>()
                .ToHashSet();
            if (clearKinds.Count > 0)
            {
                combat = combat with
                {
                    Cards = combat.Cards.Select(card =>
                        card.Affliction is { SourceEnemyInstanceId: null }
                            affliction
                        && clearKinds.Contains(affliction.Kind)
                            ? card with { Affliction = null }
                            : card).ToArray()
                };
            }
        }

        var subscribers = new List<PrototypeEventSubscriberState>();

        foreach (var power in combat.PlayerPowers)
        {
            var definition = PrototypeContent.Power(power.PowerId);
            subscribers.AddRange(
                definition.Triggers
                    .Select((trigger, index) => new { Trigger = trigger, Index = index })
                    .Where(item =>
                        item.Trigger.EventKind == combatEvent.Kind
                        && (!item.Trigger.ExcludeHandDraw
                            || !combatEvent.FromHandDraw)
                        && (!item.Trigger.RequiresPlayerTurn
                            || combat.IsPlayerTurn)
                        && (item.Trigger.RequiredSourceCardType is null
                            || (combatEvent.CardId is not null
                                && PrototypeContent.Card(
                                    combatEvent.CardId).Type
                                    == item.Trigger.RequiredSourceCardType.Value))
                        && !item.Trigger.RequiresOwnerTarget
                        && (combatEvent.PowerApplicationOrderCeiling is null
                            || power.ApplicationOrder
                                <= combatEvent.PowerApplicationOrderCeiling.Value))
                    .Select(item => new PrototypeEventSubscriberState(
                        power.ApplicationOrder,
                        item.Trigger.Effects,
                        power.Stacks,
                        PrototypeEffectSourceKind.Power,
                        SourcePowerApplicationOrder: power.ApplicationOrder,
                        PowerCardPayload: power.CardPayload?.Fork(),
                        RemoveSourcePowerAfterTrigger:
                            item.Trigger.RemoveSourcePowerAfterTrigger,
                        EveryNth: item.Trigger.EveryNth,
                        PowerTriggerIndex: item.Index,
                        CountdownBeforeTrigger: item.Trigger.CountdownBeforeTrigger,
                        SourcePowerStoredValue: power.StoredValue)));
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
                if (trigger.EventKind != combatEvent.Kind
                    || (trigger.RequiredSourceCardType is not null
                        && (combatEvent.CardId is null
                            || PrototypeContent.Card(
                                combatEvent.CardId).Type
                                != trigger.RequiredSourceCardType.Value))
                    || (trigger.RequiresEmptyHand
                        && combat.Hand.Length != 0)
                    || !RelicTriggerMatchesPlayer(
                        trigger,
                        player,
                        combat))
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
                    EveryNth: trigger.EveryNth,
                    MaxTriggersPerCounterWindow:
                        trigger.MaxTriggersPerCounterWindow));
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
            if (subscriber.PowerTriggerIndex is not null)
            {
                var checkedPower = CheckPowerSubscriberTrigger(combat, subscriber);
                combat = checkedPower.Combat;
                if (!checkedPower.ShouldTrigger)
                {
                    continue;
                }
            }

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
                if (subscriber.MaxTriggersPerCounterWindow is
                        { } maxTriggers
                    && incremented.Count > maxTriggers)
                {
                    continue;
                }

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
                    powerCardPayload: subscriber.PowerCardPayload,
                    sourcePowerEnemyId:
                        subscriber.SourcePowerEnemyId,
                    sourcePowerStoredValue:
                        subscriber.SourcePowerStoredValue,
                    sourcePowerApplicationOrder:
                        subscriber.SourcePowerApplicationOrder);
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
            combat = ResetPlayerPowerTriggerCounters(combat);
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
            if (subscriber.PowerTriggerIndex is not null)
            {
                var checkedPower = CheckPowerSubscriberTrigger(combat, subscriber);
                combat = checkedPower.Combat;
                if (!checkedPower.ShouldTrigger)
                {
                    continue;
                }
            }

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
                if (subscriber.MaxTriggersPerCounterWindow is
                        { } maxTriggers
                    && incremented.Count > maxTriggers)
                {
                    continue;
                }

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
                    powerCardPayload: subscriber.PowerCardPayload,
                    sourcePowerEnemyId:
                        subscriber.SourcePowerEnemyId,
                    sourcePowerStoredValue:
                        subscriber.SourcePowerStoredValue,
                    sourcePowerApplicationOrder:
                        subscriber.SourcePowerApplicationOrder);
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
            combat = ResetPlayerPowerTriggerCounters(combat);
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
        if (pending.Selection.SequentialOptional)
        {
            var selectedCount =
                pending.SequentialSelectedCardInstanceIds?.Length
                ?? 0;
            foreach (var candidate in
                     pending.CandidateCardInstanceIds)
            {
                if (selectedCount
                    >= pending.Selection.MaxSelections)
                {
                    break;
                }

                actions.Add(GameAction.Create(
                    "select_cards",
                    new SelectCardsPayload([candidate])));
            }

            if (selectedCount
                >= pending.Selection.MinSelections)
            {
                actions.Add(GameAction.Create(
                    "select_cards",
                    new SelectCardsPayload([])));
            }

            return actions;
        }

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
            throw new InvalidOperationException(
                "Card selection contains duplicate instance IDs.");
        }

        if (pending.Selection.SequentialOptional)
        {
            if (selected.Length > 1)
            {
                throw new InvalidOperationException(
                    "Sequential card selection accepts one card or the finish action.");
            }

            var accumulated =
                pending.SequentialSelectedCardInstanceIds
                ?? Array.Empty<long>();
            if (selected.Length == 1)
            {
                var selectedId = selected[0];
                if (!pending.CandidateCardInstanceIds
                        .Contains(selectedId))
                {
                    throw new InvalidOperationException(
                        "Sequential selection contains a card outside the pending candidate set.");
                }

                var nextAccumulated =
                    accumulated.Append(selectedId).ToArray();
                var remainingCandidates =
                    pending.CandidateCardInstanceIds
                        .Where(id => id != selectedId)
                        .ToArray();

                if (nextAccumulated.Length
                        < pending.Selection.MaxSelections
                    && remainingCandidates.Length > 0)
                {
                    combat = combat with
                    {
                        PendingChoice = pending with
                        {
                            CandidateCardInstanceIds =
                                remainingCandidates,
                            SequentialSelectedCardInstanceIds =
                                nextAccumulated
                        }
                    };
                    return state with
                    {
                        World = world with
                        {
                            Combat = combat
                        }
                    };
                }

                selected = nextAccumulated;
            }
            else
            {
                if (accumulated.Length
                    < pending.Selection.MinSelections)
                {
                    throw new InvalidOperationException(
                        "Sequential selection cannot finish before its minimum selection count.");
                }

                selected = accumulated;
            }
        }

        if (selected.Length < pending.Selection.MinSelections
            || selected.Length > pending.Selection.MaxSelections)
        {
            throw new InvalidOperationException(
                $"Selection count {selected.Length} is outside " +
                $"{pending.Selection.MinSelections}..{pending.Selection.MaxSelections}.");
        }

        if (!pending.Selection.SequentialOptional)
        {
            var candidates =
                pending.CandidateCardInstanceIds.ToHashSet();
            if (selected.Any(cardId =>
                    !candidates.Contains(cardId)))
            {
                throw new InvalidOperationException(
                    "Selection contains a card outside the pending candidate set.");
            }
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

        combat = ApplyCardSelection(
            combat, pending.Selection, selected, state.Rng);
        combat = combat with { PendingChoice = null };

        if (pending.SelectedCardTemporaryCost is not null)
        {
            foreach (var cardInstanceId in selected)
            {
                combat = SetCardTemporaryEnergyCost(
                    combat,
                    cardInstanceId,
                    pending.SelectedCardTemporaryCost);
            }
        }

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

        // Resolve selection-induced exhaust and discard using the normal
        // ordered event-dispatch continuation (including nested choices).
        var discardEvents =
            pending.Selection.SourceZone == PrototypeCardZone.Hand
            && (pending.Selection.Resolution
                    == PrototypeCardSelectionResolutionKind.MoveToDiscard
                || pending.Selection.Resolution
                    == PrototypeCardSelectionResolutionKind.MoveToExhaust)
                ? selected.Select(cardId =>
                {
                    var movedCard = RequireCombatCard(combat, cardId);
                    return new PrototypeCombatEvent(
                        pending.Selection.Resolution
                            == PrototypeCardSelectionResolutionKind.MoveToExhaust
                                ? PrototypeCombatEventKind.CardExhausted
                                : PrototypeCombatEventKind.CardDiscarded,
                        SourceCardInstanceId: cardId,
                        CardId: movedCard.CardId);
                }).ToArray()
                : Array.Empty<PrototypeCombatEvent>();

        var continuationOperations =
            (PrototypeQueuedOperation[])
            pending.Continuation.Clone();
        if (pending.Selection
                .DrawEqualToSelectionsOnCompletion
            && selected.Length > 0)
        {
            continuationOperations =
                new[]
                {
                    new PrototypeQueuedOperation(
                        PrototypeCombatEffectKind.DrawCards,
                        selected.Length)
                }
                .Concat(continuationOperations)
                .ToArray();
        }

        var resolutionContinuation =
            new PrototypeChoiceResolutionContinuationState(
                SourceCardInstanceId:
                    pending.SourceCardInstanceId,
                SourceCardDestination:
                    pending.SourceCardDestination,
                Operations:
                    continuationOperations,
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
            ? EnterCombatReward(state)
            : state;
    }

    private static (PlayerState Player, CombatState Combat)
        ResumeChoiceResolutionContinuation(
            PlayerState player,
            CombatState combat,
            RngBundle rng,
            PrototypeChoiceResolutionContinuationState continuation,
            bool resumeCardPlaySeries = true)
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

        if (resumeCardPlaySeries
            && continuation.CardPlaySeries is
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
        long[] selected,
        RngBundle rng)
    {
        var source = GetZone(combat, selection.SourceZone);
        var selectedSet = selected.ToHashSet();

        if (selected.Any(cardId => !source.Contains(cardId)))
        {
            throw new InvalidOperationException(
                "Selected card is no longer in the requested source zone.");
        }

        if (selection.Resolution == PrototypeCardSelectionResolutionKind.CopyToHand)
        {
            foreach (var id in selected)
            {
                var card = RequireCombatCard(combat, id);
                combat = AddGeneratedCardCopies(combat, new PrototypeCombatCardSnapshot(
                    card.CardId, card.UpgradeLevel, card.State.Clone(), card.CombatEnergyCostDelta,
                    card.TemporaryEnergyCost, card.KeywordOverrides, card.ReplayCount,
                    card.Enchantment, card.EnchantmentTriggeredThisCombat, card.Affliction,
                    card.SuppressedUpgradeLevels), selection.CopiesPerSelection);
            }
            return combat;
        }

        if (selection.Resolution
                == PrototypeCardSelectionResolutionKind.TransformRandom)
        {
            foreach (var instanceId in selected)
            {
                var original = RequireCombatCard(combat, instanceId);
                var definition = PrototypeContent.Card(original.CardId);
                if (definition.Eternal)
                {
                    throw new InvalidOperationException(
                        "Eternal combat cards cannot be transformed.");
                }

                // Native combat transforms into a different eligible
                // card from the ORIGINAL card's own generation pool.
                var pool = original.CardId.StartsWith(
                        "proto.colorless.", StringComparison.Ordinal)
                    ? PrototypeColorlessCards.ImplementedCombatGenerationPool
                    : PrototypeContent.RewardCardPool;
                var eligible = pool.Where(id =>
                {
                    var candidate = PrototypeContent.Card(id);
                    return id != original.CardId
                        && candidate.CanBeGeneratedInCombat
                        && candidate.MechanicsImplemented
                        && candidate.Rarity is
                            PrototypeCardRarity.Common
                            or PrototypeCardRarity.Uncommon
                            or PrototypeCardRarity.Rare;
                }).ToArray();
                if (eligible.Length == 0)
                {
                    throw new NotSupportedException(
                        "No eligible native-pool solo transform targets.");
                }

                var replacement = eligible[PrototypeRng.NextInt(
                    rng, "combat", eligible.Length)];
                var replacementId = combat.NextCardInstanceId;
                var oldPersistentId = original.PersistentCardInstanceId;
                var transformed = combat.TransformedPersistentCardIds
                    ?? Array.Empty<long>();

                // Transform creates a new physical card; it does not
                // mutate the original card's persistent deck identity.
                // Track that absence explicitly for run invariants.
                combat = combat with
                {
                    Cards = combat.Cards
                        .Where(card => card.InstanceId != instanceId)
                        .Append(new CombatCardInstance(
                            replacementId,
                            PersistentCardInstanceId: null,
                            CardId: replacement,
                            UpgradeLevel: 0,
                            IsTemporary: true,
                            State: PrototypeJson.EmptyObject()))
                        .ToArray(),
                    Hand = combat.Hand.Select(id =>
                        id == instanceId ? replacementId : id).ToArray(),
                    DrawPile = combat.DrawPile.Select(id =>
                        id == instanceId ? replacementId : id).ToArray(),
                    DiscardPile = combat.DiscardPile.Select(id =>
                        id == instanceId ? replacementId : id).ToArray(),
                    ExhaustPile = combat.ExhaustPile.Select(id =>
                        id == instanceId ? replacementId : id).ToArray(),
                    NextCardInstanceId = replacementId + 1,
                    TransformedPersistentCardIds =
                        oldPersistentId is { } persistentId
                            ? transformed.Append(persistentId).ToArray()
                            : transformed
                };
            }
            return combat;
        }

        if (selection.Resolution
                == PrototypeCardSelectionResolutionKind.Preserve
            && !selection.RemoveUnselectedFromSource)
        {
            return combat;
        }

        var destinationZone = selection.Resolution switch
        {
            PrototypeCardSelectionResolutionKind.Preserve =>
                (PrototypeCardZone?)null,
            PrototypeCardSelectionResolutionKind.MoveToHand =>
                PrototypeCardZone.Hand,
            PrototypeCardSelectionResolutionKind.MoveToDiscard =>
                PrototypeCardZone.DiscardPile,
            PrototypeCardSelectionResolutionKind.MoveToExhaust =>
                PrototypeCardZone.ExhaustPile,
            // DrawCards pops the last draw-pile entry: append means top.
            PrototypeCardSelectionResolutionKind.MoveToDrawTop =>
                PrototypeCardZone.DrawPile,
            _ => throw new ArgumentOutOfRangeException()
        };

        if (destinationZone == selection.SourceZone)
        {
            throw new InvalidOperationException(
                "Card-selection source and destination zones coincide.");
        }

        var unselected = source
            .Where(cardId => !selectedSet.Contains(cardId))
            .ToArray();
        combat = SetZone(
            combat,
            selection.SourceZone,
            selection.RemoveUnselectedFromSource
                ? Array.Empty<long>()
                : unselected);

        if (selection.RemoveUnselectedFromSource)
        {
            var unselectedSet = unselected.ToHashSet();
            if (combat.Cards.Any(card =>
                    unselectedSet.Contains(card.InstanceId)
                    && !card.IsTemporary))
            {
                throw new InvalidOperationException(
                    "Only temporary choice cards may be removed when a selection resolves.");
            }

            combat = combat with
            {
                Cards = combat.Cards
                    .Where(card =>
                        !unselectedSet.Contains(card.InstanceId))
                    .ToArray()
            };
        }

        if (destinationZone is { } destination)
        {
            var destinationCards = GetZone(combat, destination);
            combat = SetZone(
                combat,
                destination,
                destinationCards.Concat(selected).ToArray());
        }

        return combat;
    }

    private static long[] GetZone(CombatState combat, PrototypeCardZone zone) =>
        zone switch
        {
            PrototypeCardZone.Hand => combat.Hand,
            PrototypeCardZone.DrawPile => combat.DrawPile,
            PrototypeCardZone.DiscardPile => combat.DiscardPile,
            PrototypeCardZone.ExhaustPile => combat.ExhaustPile,
            PrototypeCardZone.PlayPile => combat.PlayCardIds,
            PrototypeCardZone.ChoicePool => combat.ChoiceCardIds,
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
            PrototypeCardZone.PlayPile => combat with { PlayPile = cards },
            PrototypeCardZone.ChoicePool => combat with { ChoicePool = cards },
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

    private static CombatState SetEnemyBlock(
        CombatState combat,
        int enemyId,
        int block)
    {
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

        enemies[index] = enemies[index] with
        {
            Block = Math.Max(0, block)
        };
        return combat with { Enemies = enemies };
    }

    private static CombatState RemoveEnemyPower(
        CombatState combat,
        int enemyId,
        string powerId)
    {
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

        enemies[index] = enemies[index] with
        {
            Powers = enemies[index].PowerStates
                .Where(power =>
                    !StringComparer.Ordinal.Equals(
                        power.PowerId,
                        powerId))
                .ToArray()
        };
        return combat with { Enemies = enemies };
    }

    private static int ResolveEnemyStatusTriggerCount(
        CombatState combat,
        string statusId,
        int stacks)
    {
        var extraTriggers =
            combat.PlayerPowers.Sum(power =>
            {
                var definition =
                    PrototypeContent.Power(
                        power.PowerId);
                return power.Stacks > 0
                    && StringComparer.Ordinal.Equals(
                        definition
                            .ExtraEnemyStatusTriggerStatusId,
                        statusId)
                        ? definition
                            .ExtraEnemyStatusTriggersPerStack
                          * power.Stacks
                        : 0;
            });

        return Math.Min(
            Math.Max(0, stacks),
            1 + Math.Max(0, extraTriggers));
    }

    private static CombatState TriggerEnemyStatus(
        CombatState combat,
        int enemyId,
        string statusId,
        RngBundle rng)
    {
        var enemy = combat.Enemies.SingleOrDefault(
            item => item.InstanceId == enemyId)
            ?? throw new InvalidOperationException(
                $"Enemy {enemyId} is missing.");
        var stacks =
            enemy.Statuses.GetValueOrDefault(statusId);
        var triggerCount =
            ResolveEnemyStatusTriggerCount(
                combat,
                statusId,
                stacks);

        for (var trigger = 0;
             trigger < triggerCount;
             trigger++)
        {
            enemy = combat.Enemies.Single(
                item =>
                    item.InstanceId == enemyId);
            if (enemy.Hp <= 0
                || enemy.Statuses.GetValueOrDefault(
                    statusId) <= 0)
            {
                break;
            }

            combat = TriggerEnemyStatusOnce(
                combat,
                enemyId,
                statusId,
                rng);
        }

        return combat;
    }

    private static CombatState TriggerEnemyStatusOnce(
        CombatState combat,
        int enemyId,
        string statusId,
        RngBundle rng)
    {
        var definition = PrototypeContent.Status(statusId);
        var enemy = combat.Enemies.SingleOrDefault(item =>
            item.InstanceId == enemyId)
            ?? throw new InvalidOperationException(
                $"Enemy {enemyId} is missing.");

        var stacks = enemy.Statuses.GetValueOrDefault(
            statusId);
        if (enemy.Hp <= 0 || stacks <= 0)
        {
            return combat;
        }

        if (definition.TriggerKind
            != PrototypeStatusTriggerKind.DamageSelfByStacks)
        {
            throw new InvalidOperationException(
                $"Status '{statusId}' has no immediate trigger semantics.");
        }

        var hpLoss = LoseEnemyHp(
            combat,
            enemyId,
            stacks);
        combat = hpLoss.Combat;

        if (!hpLoss.Defeated
            && definition.DecayOnTrigger != 0)
        {
            var enemies = combat.Enemies
                .Select(item => item.Fork())
                .ToArray();
            var index = Array.FindIndex(
                enemies,
                item => item.InstanceId == enemyId);
            var statuses = new Dictionary<string, int>(
                enemies[index].Statuses,
                StringComparer.Ordinal);
            var remaining =
                stacks - definition.DecayOnTrigger;
            if (remaining > 0)
            {
                statuses[statusId] = remaining;
            }
            else
            {
                statuses.Remove(statusId);
            }

            enemies[index] = enemies[index] with
            {
                Statuses = statuses
            };
            combat = combat with { Enemies = enemies };
        }

        if (hpLoss.Defeated)
        {
            combat =
                CleanupSourceBoundPowersForDefeatedEnemies(
                    combat);
            combat = ResolveEnemyDeathSummons(
                combat,
                rng);
        }

        return combat;
    }

    private static PrototypeDamageResult LoseEnemyHp(
        CombatState combat,
        int enemyId,
        int hpLoss)
    {
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
            return new PrototypeDamageResult(
                combat,
                0,
                false);
        }

        var modified = ModifyEnemyHpLoss(
            enemy,
            hpLoss);
        enemy = modified.Enemy;
        var nextHp = Math.Max(
            0,
            enemy.Hp - modified.HpLoss);
        var damageDealt = enemy.Hp - nextHp;

        enemies[index] = InterceptEnemyLethalDeath(
            enemy with { Hp = nextHp });

        var defeated = enemy.Hp > 0 && enemies[index].Hp == 0;
        var nextCombat = combat with { Enemies = enemies };
        if (defeated)
        {
            nextCombat = ResolveAllyDeathPowers(nextCombat, enemyId);
        }
        return new PrototypeDamageResult(
            nextCombat, damageDealt, Defeated: defeated);
    }

    private static CombatState ApplyPlayerEnemyBlockBrokenRelics(
        PlayerState player, CombatState combat, int enemyId)
    {
        foreach (var relic in player.Relics)
        {
            var amount = PrototypeContent.Relic(relic.RelicId).EnemyVulnerableOnBlockBroken;
            if (amount > 0) combat = ApplyEnemyPower(combat, enemyId, "proto.power.vulnerable", amount);
        }
        return combat;
    }

    private static PrototypeDamageResult DamageEnemy(
        CombatState combat,
        int enemyId,
        int damage,
        int minPoweredAttackHpLoss = 0) =>
        DamageEnemyInternal(combat, enemyId, damage,
            minPoweredAttackHpLoss, false);

    private static PrototypeDamageResult DamageEnemyInternal(
        CombatState combat,
        int enemyId,
        int damage,
        int minPoweredAttackHpLoss,
        bool isPoweredAttack)
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

        // Flutter modifies only powered attack damage. Ordinary HP loss,
        // poison and non-powered hit effects bypass the 50% modifier.
        var flutter = enemy.PowerStates.FirstOrDefault(power =>
            power.Stacks > 0
            && power.PowerId == "proto.native.hive.flutter");
        if (isPoweredAttack && flutter is not null)
        {
            damage = (int)Math.Floor(damage * 0.5m);
        }

        var absorbed = Math.Min(enemy.Block, Math.Max(0, damage));
        // BurrowedPower.AfterBlockBroken fires when a HIT actually
        // consumes the last point of the shield. Ordinary enemy Block
        // resetting, HP-loss effects and hits on zero Block do not stun.
        var brokeBurrowedBlock = enemy.Block > 0
            && absorbed == enemy.Block
            && enemy.PowerStates.Any(power =>
                power.Stacks > 0
                && power.PowerId == "proto.native.hive.burrowed");
        var unblocked = Math.Max(0, damage - absorbed);
        // The Boot modifies attack HP loss AFTER Block absorption.
        // A fully blocked hit or non-attack HP loss does not trigger.
        // Native enemy HP-loss caps are applied afterwards.
        var hpDamage = ModifyEnemyHpLoss(
            enemy,
            unblocked > 0
                ? Math.Max(unblocked, minPoweredAttackHpLoss)
                : 0);
        enemy = hpDamage.Enemy;
        var nextHp = Math.Max(0, enemy.Hp - hpDamage.HpLoss);
        var damageDealt = enemy.Hp - nextHp;

        if (isPoweredAttack && hpDamage.HpLoss > 0
            && flutter is not null && nextHp > 0)
        {
            // Flutter.AfterDamageReceived decrements once per unblocked
            // powered hit. On the fifth, CreatureCmd.Stun overrides the
            // previously committed move for exactly one enemy turn;
            // resume the interrupted move next turn.
            var remaining = flutter.Stacks - 1;
            enemy = enemy with
            {
                Powers = enemy.PowerStates
                    .Where(power => power.ApplicationOrder
                        != flutter.ApplicationOrder)
                    .Concat(remaining > 0
                        ? new[] { flutter with { Stacks = remaining } }
                        : Array.Empty<PrototypePowerInstanceState>())
                    .OrderBy(power => power.ApplicationOrder)
                    .ToArray(),
                PlannedMoveIndex = remaining <= 0
                    ? 5 : enemy.PlannedMoveIndex,
                PlannedNextAiStateId = remaining <= 0
                    ? enemy.AiStateId : enemy.PlannedNextAiStateId
            };
        }

        // Some buffs (e.g. native Asleep) react only to unblocked
        // attack HP damage, not to blocked attacks or poison HP loss.
        // Stun the pending enemy action and explicitly select its
        // waking AI state, without making the rule monster-ID-specific.
        var wake = hpDamage.HpLoss > 0 && nextHp > 0
            ? enemy.PowerStates
                .Select(power => PrototypeContent.Power(power.PowerId))
                .FirstOrDefault(power =>
                    power.WakeOwnerOnUnblockedAttackDamage)
            : null;
        if (wake is not null)
        {
            var remove = (wake.RemoveOwnerPowersOnWake
                    ?? Array.Empty<string>())
                .ToHashSet(StringComparer.Ordinal);
            enemy = enemy with
            {
                Powers = enemy.PowerStates
                    .Where(power => !remove.Contains(power.PowerId))
                    .ToArray(),
                AiStateId = wake.OwnerAiStateOnWake ?? enemy.AiStateId,
                EnemyActionSkipsRemaining = wake.StunOwnerOnWake
                    ? Math.Max(1, enemy.EnemyActionSkipsRemaining)
                    : enemy.EnemyActionSkipsRemaining
            };
        }

        enemies[index] = InterceptEnemyLethalDeath(
            enemy with
            {
                Block = enemy.Block - absorbed,
                Hp = nextHp
            });

        if (brokeBurrowedBlock && enemies[index].Hp > 0)
        {
            // Tunneler.GetStunned -> CreatureCmd.Stun(StillDizzy,
            // "BITE_MOVE") overrides the previously announced BELOW.
            // Persist the forced action now, not during observation.
            var interrupted = enemies[index];
            enemies[index] = interrupted with
            {
                Powers = interrupted.PowerStates.Where(power =>
                    power.PowerId != "proto.native.hive.burrowed")
                    .ToArray(),
                Block = 0,
                AiStateId = "dizzy",
                PlannedMoveIndex = 3,
                PlannedNextAiStateId = "bite"
            };
        }

        var defeated = enemy.Hp > 0 && enemies[index].Hp == 0;
        var nextCombat = combat with { Enemies = enemies };
        if (defeated)
        {
            nextCombat = ResolveAllyDeathPowers(nextCombat, enemyId);
        }
        return new PrototypeDamageResult(
            nextCombat, damageDealt, Defeated: defeated);
    }

    /// <summary>
    /// A last-stand power can turn lethal enemy HP loss into a separate
    /// scripted combat phase. The intercept is one-shot so the eventual
    /// scripted self-death is not intercepted again.
    /// </summary>
    private static EnemyCombatState InterceptEnemyLethalDeath(
        EnemyCombatState enemy)
    {
        if (enemy.Hp > 0 || enemy.LastStandTriggered)
        {
            return enemy;
        }

        foreach (var power in enemy.PowerStates)
        {
            var definition = PrototypeContent.Power(power.PowerId);
            if (power.Stacks <= 0
                || definition.LastStandHp <= 0
                || definition.LastStandAiStateId is not { } nextState)
            {
                continue;
            }

            // A lethal-intercept script overrides the previously
            // announced action immediately. If its destination is a
            // deterministic move state, commit that move and its
            // continuation without consuming a new RNG draw.
            var moveState = PrototypeContent.Enemy(enemy.EnemyId)
                .Ai?.States.FirstOrDefault(state =>
                    StringComparer.Ordinal.Equals(state.Id, nextState)
                    && state.Kind == PrototypeEnemyAiStateKind.Move);
            return enemy with
            {
                Hp = definition.LastStandHp,
                Block = 0,
                LastStandTriggered = true,
                AiStateId = nextState,
                PlannedMoveIndex = moveState?.MoveIndex,
                PlannedNextAiStateId = moveState?.NextStateId
            };
        }

        return enemy;
    }

    /// <summary>
    /// Dispatch source-backed powers that react to a different ally's
    /// death. This runs for both powered attacks and raw HP loss (e.g.,
    /// poison), before removal of defeated creatures. Only a living
    /// owner may react, and the owner never reacts to its own death.
    /// </summary>
    private static CombatState ResolveAllyDeathPowers(
        CombatState combat, int defeatedEnemyId)
    {
        foreach (var owner in combat.Enemies
            .Where(item => item.Hp > 0
                && item.InstanceId != defeatedEnemyId)
            .OrderBy(item => item.InstanceId))
        {
            var reactions = owner.PowerStates
                .OrderBy(power => power.ApplicationOrder)
                .Where(power => power.Stacks > 0)
                .Select(power => (
                    Definition: PrototypeContent.Power(power.PowerId),
                    power.Stacks))
                .Where(pair =>
                    pair.Definition.AllyDeathStrengthPerStack > 0
                    || pair.Definition.StunOnAllyDeath)
                .ToArray();
            foreach (var reaction in reactions)
            {
                if (reaction.Definition.AllyDeathStrengthPerStack > 0)
                {
                    combat = ApplyEnemyPower(
                        combat, owner.InstanceId, "proto.power.strength",
                        checked(reaction.Stacks
                            * reaction.Definition.AllyDeathStrengthPerStack));
                }
                if (reaction.Definition.StunOnAllyDeath)
                {
                    var enemies = combat.Enemies
                        .Select(item => item.Fork())
                        .ToArray();
                    var index = Array.FindIndex(enemies,
                        item => item.InstanceId == owner.InstanceId);
                    if (index < 0 || enemies[index].Hp <= 0)
                    {
                        continue;
                    }
                    // Native CreatureCmd.Stun replaces the pending move
                    // for one turn; repeated applications before that turn
                    // must not stack multiple skipped turns.
                    enemies[index] = enemies[index] with
                    {
                        EnemyActionSkipsRemaining =
                            Math.Max(1,
                                enemies[index].EnemyActionSkipsRemaining)
                    };
                    combat = combat with { Enemies = enemies };
                }
            }
        }
        return combat;
    }

    private sealed record PrototypeEnemyHpLossResult(
        EnemyCombatState Enemy,
        int HpLoss);

    private static PrototypeEnemyHpLossResult ModifyEnemyHpLoss(
        EnemyCombatState enemy,
        int requestedHpLoss)
    {
        var hpLoss = Math.Max(0, requestedHpLoss);
        if (hpLoss == 0 || enemy.Hp <= 0)
        {
            return new PrototypeEnemyHpLossResult(
                enemy,
                hpLoss);
        }

        var powers = enemy.PowerStates.ToList();
        var capCandidate = powers
            .Where(power =>
                power.Stacks > 0
                && PrototypeContent.Power(power.PowerId)
                    .EnemyHpLossCapPerTrigger > 0)
            .OrderBy(power => power.ApplicationOrder)
            .FirstOrDefault();

        if (capCandidate is not null)
        {
            var definition = PrototypeContent.Power(
                capCandidate.PowerId);
            hpLoss = Math.Min(
                hpLoss,
                definition.EnemyHpLossCapPerTrigger);

            if (definition.ConsumeOnEnemyHpLoss)
            {
                var index = powers.FindIndex(power =>
                    power.ApplicationOrder
                        == capCandidate.ApplicationOrder);
                if (index < 0)
                {
                    throw new InvalidOperationException(
                        "Enemy HP-loss modifier disappeared before consumption.");
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
            }
        }

        var hpLossBudget = powers
            .Where(power => power.Stacks > 0
                && PrototypeContent.Power(power.PowerId)
                    .EnemyHpLossLimitedPerSideTurnByStacks)
            .Select(power => power.Stacks)
            .DefaultIfEmpty(int.MaxValue)
            .Min();
        if (hpLossBudget != int.MaxValue)
        {
            hpLoss = Math.Min(hpLoss,
                Math.Max(0, hpLossBudget - enemy.HpLossBudgetUsed));
            enemy = enemy with
            {
                HpLossBudgetUsed = enemy.HpLossBudgetUsed + hpLoss
            };
        }

        var nextHp = Math.Max(0, enemy.Hp - hpLoss);
        var thresholdCandidate = powers
            .Where(power =>
            {
                if (power.Stacks <= 0)
                {
                    return false;
                }

                var definition = PrototypeContent.Power(
                    power.PowerId);
                return definition.TriggerOwnerAtHpAtOrBelowStacks
                    && nextHp <= power.Stacks;
            })
            .OrderBy(power => power.ApplicationOrder)
            .FirstOrDefault();

        if (thresholdCandidate is not null)
        {
            var thresholdDefinition =
                PrototypeContent.Power(
                    thresholdCandidate.PowerId);
            powers.RemoveAll(power =>
                power.ApplicationOrder
                    == thresholdCandidate.ApplicationOrder);

            if (thresholdDefinition
                    .ClearOwnerStrengthOnHpThresholdTrigger)
            {
                powers.RemoveAll(power =>
                    StringComparer.Ordinal.Equals(
                        power.PowerId,
                        "proto.power.strength"));
            }

            var forcedStateId =
                thresholdDefinition.OwnerAiStateOnHpThresholdTrigger;
            var forcedAiState = forcedStateId is null
                ? null
                : PrototypeContent.Enemy(enemy.EnemyId)
                    .Ai?.States.FirstOrDefault(state =>
                        StringComparer.Ordinal.Equals(
                            state.Id, forcedStateId));
            // A player-triggered threshold may OVERRIDE an announced move,
            // e.g. the Ceremonial Beast is visibly stunned. This is a
            // deterministic interrupt, never a new hidden random roll.
            enemy = enemy with
            {
                AiStateId = forcedStateId ?? enemy.AiStateId,
                PlannedMoveIndex = forcedAiState is
                    { Kind: PrototypeEnemyAiStateKind.Move }
                    ? forcedAiState.MoveIndex : enemy.PlannedMoveIndex,
                PlannedNextAiStateId = forcedAiState is
                    { Kind: PrototypeEnemyAiStateKind.Move }
                    ? forcedAiState.NextStateId : enemy.PlannedNextAiStateId
            };
        }

        enemy = enemy with
        {
            Powers = powers.ToArray()
        };

        return new PrototypeEnemyHpLossResult(
            enemy,
            hpLoss);
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
        long[] DrawnCardInstanceIds,
        int RemainingCount = 0,
        bool ShuffleSelectionPending = false);

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
                var stratagem = combat.PlayerPowers.Sum(power =>
                    power.PowerId == "proto.power.stratagem"
                        ? Math.Max(0, power.Stacks) : 0);
                if (stratagem > 0 && combat.Hand.Length < maxHandSize)
                {
                    // Persist the recycled draw pile before prompting.
                    // The suspended draw resumes after this choice.
                    combat = combat with
                    {
                        DrawPile = draw.ToArray(),
                        DiscardPile = discard.ToArray()
                    };
                    return new PrototypeDrawCardsResult(
                        player, combat, drawnCardInstanceIds.ToArray(),
                        RemainingCount: count - drawNumber,
                        ShuffleSelectionPending: true);
                }
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
            var randomizedCostUpperBound =
                player.Relics
                    .Select(relic =>
                        PrototypeContent.Relic(relic.RelicId)
                            .RandomizeDrawnCardCostMaxExclusive)
                    .DefaultIfEmpty(0)
                    .Max();
            var cardDefinition =
                PrototypeContent.Card(card.CardId);
            if (randomizedCostUpperBound > 0
                && cardDefinition.Cost.Kind
                    == PrototypeCardCostKind.Fixed
                && cardDefinition.Cost.Amount >= 0)
            {
                combat = SetCardTemporaryEnergyCost(
                    combat,
                    cardInstanceId,
                    new PrototypeTemporaryCardCost(
                        PrototypeRng.NextInt(
                            rng,
                            "combat",
                            randomizedCostUpperBound),
                        PrototypeTemporaryCardCostExpiry.None));
                card = RequireCombatCard(
                    combat,
                    cardInstanceId);
            }

            if (card.Enchantment is
                    { Kind: PrototypeCardEnchantmentKind.Slither })
            {
                // Slither rerolls this combat's cost every time its
                // card is drawn, including after pile recycling.
                combat = SetCardTemporaryEnergyCost(
                    combat,
                    cardInstanceId,
                    new PrototypeTemporaryCardCost(
                        PrototypeRng.NextInt(rng, "combat", 4),
                        PrototypeTemporaryCardCostExpiry.None));
                card = RequireCombatCard(combat, cardInstanceId);
            }

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
