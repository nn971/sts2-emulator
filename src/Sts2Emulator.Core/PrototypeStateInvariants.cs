namespace Sts2Emulator.Core;

public static class PrototypeStateInvariants
{
    public static void Validate(RunState state)
    {
        if (state.Ascension < 0)
        {
            throw new InvalidOperationException(
                $"Ascension is negative: {state.Ascension}.");
        }

        if (state.Phase == RunPhase.RunStart)
        {
            if (state.World is not null)
            {
                throw new InvalidOperationException("RunStart state must not have initialized world state.");
            }

            return;
        }

        var world = state.World
            ?? throw new InvalidOperationException("Initialized prototype run is missing world state.");

        if (!StringComparer.Ordinal.Equals(world.RulesetId, PrototypeContent.RulesetId))
        {
            throw new InvalidOperationException($"Unexpected prototype ruleset '{world.RulesetId}'.");
        }

        if (state.Player.MaxHp <= 0 || state.Player.Hp < 0 || state.Player.Hp > state.Player.MaxHp)
        {
            throw new InvalidOperationException(
                $"Invalid player HP {state.Player.Hp}/{state.Player.MaxHp}.");
        }

        if (state.Player.Gold < 0)
        {
            throw new InvalidOperationException($"Player gold is negative: {state.Player.Gold}.");
        }

        if (world.Act < 1 || world.Act > PrototypeContent.Rules.Acts)
        {
            throw new InvalidOperationException($"Invalid act {world.Act}.");
        }

        if (world.Floor < 0 || world.Floor > PrototypeContent.Rules.FloorsPerAct)
        {
            throw new InvalidOperationException($"Invalid floor {world.Floor}.");
        }

        if (state.Player.PotionSlots.Length != PrototypeContent.Rules.PotionSlots)
        {
            throw new InvalidOperationException("Potion slot count differs from the active ruleset.");
        }

        var deckIds = state.Player.Deck.Select(card => card.InstanceId).ToArray();
        if (deckIds.Length != deckIds.Distinct().Count())
        {
            throw new InvalidOperationException("Persistent deck contains duplicate card instance IDs.");
        }

        if (world.NextCardInstanceId <= deckIds.DefaultIfEmpty(0).Max())
        {
            throw new InvalidOperationException("Next card instance ID does not exceed all live card IDs.");
        }

        if (world.Map.Nodes.Length > 0)
        {
            ValidateMap(world);
        }

        ValidateActOneEncounterPool(world);
        ValidateEncounterHistory(world);
        ValidateEventHistory(world);
        ValidatePhaseState(state, world);

        if (world.Combat is not null)
        {
            ValidateCombat(state.Player, world.Combat);
        }

        if (state.Phase == RunPhase.Terminal)
        {
            if (world.TerminalOutcome is not ("victory" or "defeat"))
            {
                throw new InvalidOperationException("Terminal state has no recognized outcome.");
            }

            if (world.TerminalOutcome == "defeat" && state.Player.Hp != 0)
            {
                throw new InvalidOperationException("Defeat state must have zero HP.");
            }
        }
        else if (world.TerminalOutcome is not null)
        {
            throw new InvalidOperationException("Non-terminal state carries a terminal outcome.");
        }
    }

    private static void ValidatePhaseState(RunState state, RunWorldState world)
    {
        switch (state.Phase)
        {
            case RunPhase.MapChoice:
                if (world.Map.Options.Length == 0)
                {
                    throw new InvalidOperationException("MapChoice phase has no map options.");
                }
                break;

            case RunPhase.Combat:
                if (world.Combat is null)
                {
                    throw new InvalidOperationException("Combat phase has no combat state.");
                }
                break;

            case RunPhase.Reward:
                if (world.Reward is null)
                {
                    throw new InvalidOperationException("Reward phase has no reward state.");
                }

                ValidateReward(world.Reward);
                break;

            case RunPhase.Shop:
                if (world.Shop is null)
                {
                    throw new InvalidOperationException("Shop phase has no shop state.");
                }

                ValidateShop(world.Shop);
                break;

            case RunPhase.Event:
                if (world.Event is null)
                {
                    throw new InvalidOperationException("Event phase has no event state.");
                }
                break;

            case RunPhase.Rest:
            case RunPhase.ActTransition:
                break;

            case RunPhase.Terminal:
                break;

            default:
                throw new InvalidOperationException($"Unexpected initialized prototype phase {state.Phase}.");
        }
    }

    private static void ValidateActOneEncounterPool(
        RunWorldState world)
    {
        var pool = world.ActOneEncounterPool;
        if (pool is null)
        {
            return;
        }

        if (world.ActOneRegion is { } region
            && region != pool.Region)
        {
            throw new InvalidOperationException(
                "Act 1 encounter pool region disagrees with the run region.");
        }

        var maxTrackedOrdinaryCombats =
            3 + PrototypeContent
                .OvergrowthNormalEncounterPool.Length;
        if (pool.OrdinaryCombatsStarted < 0
            || pool.OrdinaryCombatsStarted
                > maxTrackedOrdinaryCombats)
        {
            throw new InvalidOperationException(
                $"Invalid Act 1 ordinary-combat counter {pool.OrdinaryCombatsStarted}.");
        }

        var weakCombatsStarted = Math.Min(
            3,
            pool.OrdinaryCombatsStarted);
        if (pool.RemainingWeakEncounterIds.Length
            != PrototypeContent.OvergrowthWeakEncounterPool.Length
                - weakCombatsStarted)
        {
            throw new InvalidOperationException(
                "Act 1 weak encounter pool size disagrees with its combat counter.");
        }

        if (pool.RemainingWeakEncounterIds
                .Distinct(StringComparer.Ordinal)
                .Count()
            != pool.RemainingWeakEncounterIds.Length)
        {
            throw new InvalidOperationException(
                "Act 1 weak encounter pool contains duplicate encounter IDs.");
        }

        if (pool.Region == PrototypeActOneRegion.Overgrowth
            && pool.RemainingWeakEncounterIds.Any(id =>
                !PrototypeContent.OvergrowthWeakEncounterPool.Contains(
                    id,
                    StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "Overgrowth weak encounter pool contains a foreign encounter.");
        }

        if (pool.RemainingNormalEncounterIds is { } normal)
        {
            if (normal.Distinct(StringComparer.Ordinal).Count()
                != normal.Length)
            {
                throw new InvalidOperationException(
                    "Act 1 normal encounter pool contains duplicate encounter IDs.");
            }

            if (pool.Region == PrototypeActOneRegion.Overgrowth
                && normal.Any(id =>
                    !PrototypeContent.OvergrowthNormalEncounterPool.Contains(
                        id,
                        StringComparer.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Overgrowth normal encounter pool contains a foreign encounter.");
            }

            var normalCombatsStarted = Math.Max(
                0,
                pool.OrdinaryCombatsStarted - 3);
            var expectedNormalRemaining =
                PrototypeContent
                    .OvergrowthNormalEncounterPool.Length
                - normalCombatsStarted;
            if (normal.Length != expectedNormalRemaining)
            {
                throw new InvalidOperationException(
                    "Act 1 normal encounter pool size disagrees with its combat counter.");
            }
        }

        if (pool.RemainingEliteEncounterIds is { } elite)
        {
            if (elite.Distinct(StringComparer.Ordinal).Count()
                != elite.Length)
            {
                throw new InvalidOperationException(
                    "Act 1 elite encounter pool contains duplicate encounter IDs.");
            }

            if (pool.Region == PrototypeActOneRegion.Overgrowth
                && elite.Any(id =>
                    !PrototypeContent.OvergrowthEliteEncounterPool.Contains(
                        id,
                        StringComparer.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Overgrowth elite encounter pool contains a foreign encounter.");
            }
        }

        if (pool.Region == PrototypeActOneRegion.Overgrowth
            && (pool.BossEncounterId is null
                || !PrototypeContent.OvergrowthBossEncounterPool.Contains(
                    pool.BossEncounterId,
                    StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "Overgrowth run has an invalid selected boss encounter.");
        }
    }

    private static void ValidateEventHistory(RunWorldState world)
    {
        foreach (var eventId in world.EventIds)
        {
            if (!PrototypeContent.Events.ContainsKey(eventId))
            {
                throw new InvalidOperationException(
                    $"Event history contains unknown event '{eventId}'.");
            }
        }

        foreach (var group in world.EventIds.GroupBy(id => id, StringComparer.Ordinal))
        {
            var definition = PrototypeContent.Event(group.Key);
            if (definition.OncePerRun && group.Count() > 1)
            {
                throw new InvalidOperationException(
                    $"Once-per-run event '{group.Key}' appears multiple times.");
            }
        }

        if (world.Event is null)
        {
            return;
        }

        var active = PrototypeContent.Event(world.Event.EventId);
        if (world.Act < active.MinAct || world.Act > active.MaxAct || active.Weight <= 0)
        {
            throw new InvalidOperationException(
                $"Event '{active.Id}' is ineligible in act {world.Act}.");
        }

        if (world.Map.Nodes.Length > 0)
        {
            var latest = world.EventIds.LastOrDefault()
                ?? throw new InvalidOperationException(
                    "Map-generated active event has no event-history entry.");
            if (!StringComparer.Ordinal.Equals(latest, active.Id))
            {
                throw new InvalidOperationException(
                    "Active event disagrees with the latest event-history entry.");
            }
        }
    }

    private static void ValidateShop(ShopState shop)
    {
        if (shop.RemovalPrice <= 0)
        {
            throw new InvalidOperationException("Shop removal price must be positive.");
        }

        var offerIds = shop.CardOffers
            .Select(offer => offer.OfferId)
            .Concat(shop.PotionOffer is null ? [] : [shop.PotionOffer.OfferId])
            .Concat(shop.RelicOffer is null ? [] : [shop.RelicOffer.OfferId])
            .ToArray();

        if (offerIds.Length != offerIds.Distinct().Count())
        {
            throw new InvalidOperationException("Shop offer IDs are duplicated.");
        }

        foreach (var offer in shop.CardOffers)
        {
            if (offer.Price <= 0
                || !PrototypeContent.RewardCardPool.Contains(
                    offer.ItemId,
                    StringComparer.Ordinal)
                || PrototypeContent.Card(offer.ItemId).Rarity == PrototypeCardRarity.Basic)
            {
                throw new InvalidOperationException("Shop contains an invalid card offer.");
            }
        }

        if (shop.PotionOffer is { } potion)
        {
            if (potion.Price <= 0 || !PrototypeContent.Potions.ContainsKey(potion.ItemId))
            {
                throw new InvalidOperationException("Shop contains an invalid potion offer.");
            }
        }

        if (shop.RelicOffer is { } relic)
        {
            if (relic.Price <= 0 || !PrototypeContent.Relics.ContainsKey(relic.ItemId))
            {
                throw new InvalidOperationException("Shop contains an invalid relic offer.");
            }
        }
    }

    private static void ValidateEncounterHistory(RunWorldState world)
    {
        foreach (var historicalEncounterId in world.EncounterIds)
        {
            if (!PrototypeContent.Encounters.Any(encounter =>
                StringComparer.Ordinal.Equals(encounter.Id, historicalEncounterId)))
            {
                throw new InvalidOperationException(
                    $"Encounter history contains unknown encounter '{historicalEncounterId}'.");
            }
        }

        if (world.ActiveRoom is not (
                PrototypeRoomType.Combat
                or PrototypeRoomType.Elite
                or PrototypeRoomType.Boss))
        {
            return;
        }

        var encounterId = world.EncounterIds.LastOrDefault();
        if (encounterId is null)
        {
            if (world.Map.Nodes.Length > 0)
            {
                throw new InvalidOperationException(
                    "Map-generated active combat room has no encounter-history entry.");
            }

            return;
        }
        var encounter = PrototypeContent.Encounters.Single(item =>
            StringComparer.Ordinal.Equals(item.Id, encounterId));

        var promotedOvergrowthEncounter =
            world.Act == 1
            && world.ActOneRegion
                == PrototypeActOneRegion.Overgrowth
            && (PrototypeContent.OvergrowthWeakEncounterPool.Contains(
                    encounter.Id,
                    StringComparer.Ordinal)
                || PrototypeContent.OvergrowthNormalEncounterPool.Contains(
                    encounter.Id,
                    StringComparer.Ordinal)
                || PrototypeContent.OvergrowthEliteEncounterPool.Contains(
                    encounter.Id,
                    StringComparer.Ordinal)
                || PrototypeContent.OvergrowthBossEncounterPool.Contains(
                    encounter.Id,
                    StringComparer.Ordinal));

        if (encounter.RoomType != world.ActiveRoom
            || world.Act < encounter.MinAct
            || world.Act > encounter.MaxAct
            || world.Floor < encounter.MinFloor
            || world.Floor > encounter.MaxFloor
            || (encounter.Weight <= 0
                && !promotedOvergrowthEncounter))
        {
            throw new InvalidOperationException(
                $"Encounter '{encounter.Id}' is ineligible at act {world.Act}, floor {world.Floor}.");
        }
    }

    private static void ValidateReward(RewardState reward)
    {
        if (reward.CardOptions.Length != reward.CardOptions.Distinct(StringComparer.Ordinal).Count())
        {
            throw new InvalidOperationException("Reward card options contain duplicates.");
        }

        foreach (var cardId in reward.CardOptions)
        {
            if (!PrototypeContent.RewardCardPool.Contains(cardId, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Reward contains card '{cardId}' outside the reward pool.");
            }

            if (PrototypeContent.Card(cardId).Rarity == PrototypeCardRarity.Basic)
            {
                throw new InvalidOperationException("Reward contains a basic-only card.");
            }
        }
    }

    private static void ValidateCombatPotions(
        PlayerState player,
        CombatState combat)
    {
        var occupiedSlots = player.PotionSlots
            .Select((potion, slot) => (Potion: potion, Slot: slot))
            .Where(item => item.Potion is not null)
            .ToArray();

        if (combat.PotionStates.Length != occupiedSlots.Length)
        {
            throw new InvalidOperationException(
                "Occupied persistent potion slots are not represented exactly once in combat.");
        }

        if (combat.PotionStates.Select(potion => potion.Slot).Distinct().Count()
            != combat.PotionStates.Length)
        {
            throw new InvalidOperationException("Combat potion slots are duplicated.");
        }

        foreach (var combatPotion in combat.PotionStates)
        {
            if (combatPotion.Slot < 0 || combatPotion.Slot >= player.PotionSlots.Length)
            {
                throw new InvalidOperationException("Combat potion has an invalid slot.");
            }

            var persistent = player.PotionSlots[combatPotion.Slot]
                ?? throw new InvalidOperationException(
                    "Combat potion points to an empty persistent slot.");
            if (!StringComparer.Ordinal.Equals(combatPotion.PotionId, persistent.PotionId))
            {
                throw new InvalidOperationException(
                    "Combat potion disagrees with its persistent origin.");
            }

            _ = PrototypeContent.Potion(combatPotion.PotionId);
        }
    }

    private static void ValidateCombatRelics(
        PlayerState player,
        CombatState combat)
    {
        if (combat.RelicStates.Length != player.Relics.Length)
        {
            throw new InvalidOperationException(
                "Persistent relics are not represented exactly once in combat.");
        }

        foreach (var relic in combat.RelicStates)
        {
            if (relic.PersistentIndex < 0 || relic.PersistentIndex >= player.Relics.Length)
            {
                throw new InvalidOperationException("Combat relic has an invalid persistent index.");
            }

            var persistent = player.Relics[relic.PersistentIndex];
            if (!StringComparer.Ordinal.Equals(relic.RelicId, persistent.RelicId))
            {
                throw new InvalidOperationException("Combat relic disagrees with its persistent origin.");
            }

            var definition = PrototypeContent.Relic(relic.RelicId);
            var triggerCount = (definition.Triggers ?? Array.Empty<PrototypeRelicTriggerSpec>()).Length;
            if (relic.TriggerCounts.Length != triggerCount
                || relic.TriggerCounts.Any(count => count < 0))
            {
                throw new InvalidOperationException("Combat relic trigger counters are invalid.");
            }
        }

        if (combat.RelicStates.Select(relic => relic.PersistentIndex).Distinct().Count()
            != combat.RelicStates.Length)
        {
            throw new InvalidOperationException("Combat relic persistent indices are duplicated.");
        }
    }

    private static void ValidatePowerOwner(
        string owner,
        PrototypePowerInstanceState[] powers)
    {
        if (powers.Any(power => power.Stacks <= 0))
        {
            throw new InvalidOperationException($"{owner} power stacks must stay positive.");
        }

        foreach (var group in powers.GroupBy(
                     power => power.PowerId,
                     StringComparer.Ordinal))
        {
            var definition = PrototypeContent.Power(group.Key);
            if (!definition.IsInstanced && group.Count() > 1)
            {
                throw new InvalidOperationException(
                    $"{owner} stacking power '{group.Key}' occurs more than once.");
            }
        }

        foreach (var power in powers)
        {
            var definition = PrototypeContent.Power(power.PowerId);
            if (definition.RequiresCardPayload != (power.CardPayload is not null))
            {
                throw new InvalidOperationException(
                    $"{owner} power '{power.PowerId}' has invalid card-payload state.");
            }

            if (power.CardPayload is not null)
            {
                _ = PrototypeContent.Card(power.CardPayload.CardId);
            }
        }
    }

    private static void ValidateMap(RunWorldState world)
    {
        var map = world.Map;
        var nodesById = map.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        if (nodesById.Count != map.Nodes.Length)
        {
            throw new InvalidOperationException("Map node IDs are not unique.");
        }

        if (map.EntryNodeIds is null || map.EntryNodeIds.Length == 0)
        {
            throw new InvalidOperationException("Non-empty map has no entry nodes.");
        }

        if (map.EntryNodeIds.Length != map.EntryNodeIds.Distinct(StringComparer.Ordinal).Count())
        {
            throw new InvalidOperationException("Map entry nodes contain duplicates.");
        }

        foreach (var entryId in map.EntryNodeIds)
        {
            if (!nodesById.TryGetValue(entryId, out var entry) || entry.Floor != 1)
            {
                throw new InvalidOperationException("Map entry must reference a floor-one node.");
            }
        }

        foreach (var node in map.Nodes)
        {
            if (node.Act != world.Act)
            {
                throw new InvalidOperationException("Map node belongs to a different act.");
            }

            if (node.Floor < 1 || node.Floor > PrototypeContent.Rules.FloorsPerAct)
            {
                throw new InvalidOperationException("Map node has an invalid floor.");
            }

            var matchingRules = PrototypeContent.Rules.MapFloorRules
                .Where(rule =>
                    node.Floor >= rule.MinFloor
                    && node.Floor <= rule.MaxFloor)
                .ToArray();
            if (matchingRules.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Floor {node.Floor} must be covered by exactly one map rule.");
            }

            if (!matchingRules[0].RoomPool.Contains(node.RoomType))
            {
                throw new InvalidOperationException(
                    $"Room {node.RoomType} is not allowed on floor {node.Floor}.");
            }

            foreach (var nextId in node.NextNodeIds ?? Array.Empty<string>())
            {
                if (!nodesById.TryGetValue(nextId, out var next))
                {
                    throw new InvalidOperationException($"Map edge targets missing node '{nextId}'.");
                }

                if (next.Floor != node.Floor + 1)
                {
                    throw new InvalidOperationException("Map edge must advance exactly one floor.");
                }
            }
        }

        foreach (var floorGroup in map.Nodes.GroupBy(node => node.Floor))
        {
            var matchingRules = PrototypeContent.Rules.MapFloorRules
                .Where(rule =>
                    floorGroup.Key >= rule.MinFloor
                    && floorGroup.Key <= rule.MaxFloor)
                .ToArray();
            if (matchingRules.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Floor {floorGroup.Key} must be covered by exactly one map rule.");
            }

            if (!matchingRules[0].AllowDuplicateSpecialRooms)
            {
                var duplicateSpecial = floorGroup
                    .Where(node => node.RoomType != PrototypeRoomType.Combat)
                    .GroupBy(node => node.RoomType)
                    .FirstOrDefault(group => group.Count() > 1);
                if (duplicateSpecial is not null)
                {
                    throw new InvalidOperationException(
                        $"Floor {floorGroup.Key} duplicates special room {duplicateSpecial.Key}.");
                }
            }
        }

        var bosses = map.Nodes
            .Where(node => node.RoomType == PrototypeRoomType.Boss)
            .ToArray();
        if (bosses.Length != 1
            || bosses[0].Floor != PrototypeContent.Rules.FloorsPerAct
            || (bosses[0].NextNodeIds?.Length ?? 0) != 0)
        {
            throw new InvalidOperationException("Prototype act map must end in exactly one boss node.");
        }

        if (map.CurrentNodeId is null)
        {
            if (world.Floor != 0)
            {
                throw new InvalidOperationException("Unentered act map must be at floor zero.");
            }
        }
        else
        {
            if (!nodesById.TryGetValue(map.CurrentNodeId, out var current))
            {
                throw new InvalidOperationException("Map current node is missing from the graph.");
            }

            if (current.Floor != world.Floor)
            {
                throw new InvalidOperationException("World floor disagrees with current map node.");
            }
        }
    }

    private static void ValidateCombat(PlayerState player, CombatState combat)
    {
        if (combat.Turn <= 0)
        {
            throw new InvalidOperationException($"Combat turn must be positive, got {combat.Turn}.");
        }

        if (combat.Energy < 0 || combat.PlayerBlock < 0)
        {
            throw new InvalidOperationException("Combat energy/block cannot be negative.");
        }

        if (combat.Enemies.Any(enemy => enemy.Hp < 0 || enemy.Block < 0))
        {
            throw new InvalidOperationException("Enemy HP/block cannot be negative.");
        }

        var known = combat.Cards.Select(card => card.InstanceId).ToHashSet();
        if (known.Count != combat.Cards.Length)
        {
            throw new InvalidOperationException("Combat card instance IDs are not unique.");
        }

        if (combat.NextCardInstanceId <= known.DefaultIfEmpty(0).Max())
        {
            throw new InvalidOperationException("Next combat card instance ID is not fresh.");
        }

        var persistentIds = player.Deck.Select(card => card.InstanceId).ToHashSet();
        var persistentCombatCards = combat.Cards
            .Where(card => card.PersistentCardInstanceId is not null)
            .ToArray();
        var representedPersistentIds = persistentCombatCards
            .Select(card => card.PersistentCardInstanceId!.Value)
            .ToArray();

        if (representedPersistentIds.Distinct().Count()
                != representedPersistentIds.Length
            || persistentCombatCards.Any(card =>
                card.PersistentCardInstanceId is null
                || !persistentIds.Contains(card.PersistentCardInstanceId.Value)))
        {
            throw new InvalidOperationException(
                "Persistent deck cards have invalid combat provenance.");
        }

        if (combat.Cards.Any(card => card.IsTemporary != (card.PersistentCardInstanceId is null)))
        {
            throw new InvalidOperationException("Combat temporary-card provenance is inconsistent.");
        }

        foreach (var card in persistentCombatCards)
        {
            var persistent = player.Deck.Single(item =>
                item.InstanceId == card.PersistentCardInstanceId!.Value);
            if (!StringComparer.Ordinal.Equals(card.CardId, persistent.CardId)
                || card.UpgradeLevel
                    + card.SuppressedUpgradeLevels
                    < persistent.UpgradeLevel
                || card.Enchantment != persistent.Enchantment)
            {
                throw new InvalidOperationException(
                    "Combat card no longer matches its persistent origin.");
            }
        }

        var representedPersistentSet = representedPersistentIds.ToHashSet();
        foreach (var persistent in player.Deck.Where(card =>
                     !representedPersistentSet.Contains(card.InstanceId)))
        {
            if (PrototypeContent.Card(persistent.CardId).Type
                != PrototypeCardType.Power)
            {
                throw new InvalidOperationException(
                    "Only resolved Power cards may leave the combat card scope.");
            }
        }

        ValidatePowerOwner("player", combat.PlayerPowers);

        foreach (var power in combat.PlayerPowers)
        {
            var definition = PrototypeContent.Power(power.PowerId);
            if (definition.SourceBoundToEnemy)
            {
                if (power.SourceEnemyInstanceId is not
                    { } sourceEnemyId)
                {
                    throw new InvalidOperationException(
                        "Source-bound player power is missing its enemy source.");
                }

                var source = combat.Enemies.FirstOrDefault(enemy =>
                    enemy.InstanceId == sourceEnemyId);
                if (source is null || source.Hp <= 0)
                {
                    throw new InvalidOperationException(
                        "Source-bound player power references a missing or defeated enemy.");
                }
            }
            else if (power.SourceEnemyInstanceId is not null)
            {
                throw new InvalidOperationException(
                    "Non-source-bound player power carries an enemy source.");
            }
        }

        foreach (var card in combat.Cards)
        {
            if (card.Affliction?.SourceEnemyInstanceId is not
                { } sourceEnemyId)
            {
                continue;
            }

            var hasMatchingSourcePower =
                combat.PlayerPowers.Any(power =>
                {
                    if (power.SourceEnemyInstanceId
                            != sourceEnemyId)
                    {
                        return false;
                    }

                    var definition =
                        PrototypeContent.Power(power.PowerId);
                    return definition.SourceBoundCardAffliction
                        == card.Affliction.Kind;
                });
            if (!hasMatchingSourcePower)
            {
                throw new InvalidOperationException(
                    "Source-owned card Affliction has no matching active power.");
            }
        }

        foreach (var enemy in combat.Enemies)
        {
            ValidatePowerOwner($"enemy {enemy.InstanceId}", enemy.PowerStates);
        }

        var allPowers = combat.PlayerPowers
            .Concat(combat.Enemies.SelectMany(enemy => enemy.PowerStates))
            .ToArray();

        ValidateCombatRelics(player, combat);
        ValidateCombatPotions(player, combat);

        var allSubscriberOrders = allPowers
            .Select(power => power.ApplicationOrder)
            .Concat(combat.RelicStates.Select(relic => relic.ApplicationOrder))
            .ToArray();

        if (allSubscriberOrders.Distinct().Count() != allSubscriberOrders.Length)
        {
            throw new InvalidOperationException(
                "Event-subscriber application order must be globally unique.");
        }

        if (allSubscriberOrders.Any(order =>
            order <= 0 || order >= combat.NextPowerApplicationOrder))
        {
            throw new InvalidOperationException("Event-subscriber application order is invalid.");
        }

        if (combat.NextPowerApplicationOrder <= allSubscriberOrders.DefaultIfEmpty(0).Max())
        {
            throw new InvalidOperationException("Next event-subscriber application order is not fresh.");
        }

        var zones = combat.Hand
            .Concat(combat.DrawPile)
            .Concat(combat.DiscardPile)
            .Concat(combat.ExhaustPile)
            .ToArray();

        var suspendedSources = new List<long>();
        if (combat.PendingChoice is
                {
                    SourceCardInstanceId: { } pendingSource,
                    SourceCardAlreadyMoved: false
                })
        {
            suspendedSources.Add(pendingSource);
        }

        for (var continuation =
                 combat.PendingChoice?.OuterChoiceContinuation;
             continuation is not null;
             continuation = continuation.Parent)
        {
            if (!continuation.SourceCardAlreadyMoved
                && continuation.SourceCardInstanceId is
                    { } continuationSource)
            {
                suspendedSources.Add(continuationSource);
            }
        }

        var represented = zones
            .Concat(suspendedSources)
            .ToArray();

        if (represented.Any(cardId => !known.Contains(cardId)))
        {
            throw new InvalidOperationException("Combat zone contains an unknown combat card instance.");
        }

        if (represented.Length != represented.Distinct().Count())
        {
            throw new InvalidOperationException("A card instance occurs in multiple combat zones/continuations.");
        }

        if (represented.Length != known.Count)
        {
            throw new InvalidOperationException("Combat cards are not fully represented by zones/continuations.");
        }

        if (combat.PendingChoice is not null)
        {
            var pending = combat.PendingChoice;
            var sourceZone = pending.Selection.SourceZone switch
            {
                PrototypeCardZone.Hand => combat.Hand,
                PrototypeCardZone.DrawPile => combat.DrawPile,
                PrototypeCardZone.DiscardPile => combat.DiscardPile,
                PrototypeCardZone.ExhaustPile => combat.ExhaustPile,
                _ => throw new ArgumentOutOfRangeException()
            };

            if (pending.CandidateCardInstanceIds.Length
                != pending.CandidateCardInstanceIds.Distinct().Count())
            {
                throw new InvalidOperationException("Pending choice contains duplicate candidates.");
            }

            if (pending.CandidateCardInstanceIds.Any(cardId => !sourceZone.Contains(cardId)))
            {
                throw new InvalidOperationException("Pending choice candidate left its declared source zone.");
            }

            if (pending.Selection.MinSelections < 0
                || pending.Selection.MaxSelections < pending.Selection.MinSelections
                || pending.Selection.MaxSelections > pending.CandidateCardInstanceIds.Length)
            {
                throw new InvalidOperationException("Pending choice has an invalid selection range.");
            }


            for (var continuation = pending.OuterChoiceContinuation;
                 continuation is not null;
                 continuation = continuation.Parent)
            {
                if (continuation.PendingSlyCardInstanceIds.Any(
                        cardId => !combat.DiscardPile.Contains(cardId)))
                {
                    throw new InvalidOperationException(
                        "Nested choice continuation has a pending Sly card outside Discard.");
                }

                if (continuation.PendingDiscardEvents.Any(combatEvent =>
                        combatEvent.SourceCardInstanceId is
                            { } cardId
                        && !combat.DiscardPile.Contains(cardId)))
                {
                    throw new InvalidOperationException(
                        "Nested choice continuation has a pending discard event for a card outside Discard.");
                }
            }
        }
    }
}
