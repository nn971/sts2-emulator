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

        if (world.Floor < 0 || world.Floor > (
                world.Map.GenerationProfileId == PrototypeNativeOvergrowthMap.GenerationProfileId
                    ? PrototypeNativeOvergrowthMap.BossFloor
                    : PrototypeContent.Rules.FloorsPerAct))
        {
            throw new InvalidOperationException($"Invalid floor {world.Floor}.");
        }

        if (state.Player.PotionSlots.Length
            != PrototypeContent.Rules.PotionSlots
                + state.Player.Relics.Sum(relic =>
                    PrototypeContent.Relic(relic.RelicId).ExtraPotionSlots))
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
        ValidateCompletedRoomHistory(world);
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

                ValidateReward(
                    state.Player,
                    world.Reward);
                break;

            case RunPhase.Shop:
                if (world.Shop is null)
                {
                    throw new InvalidOperationException("Shop phase has no shop state.");
                }

                ValidateShop(state.Player, world.Shop);
                break;

            case RunPhase.Event:
                if (world.Event is null)
                {
                    throw new InvalidOperationException(
                        "Event phase has no event state.");
                }

                ValidateEvent(
                    state.Player,
                    world.Event);
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

    private static void ValidateCompletedRoomHistory(
        RunWorldState world)
    {
        var history = world.CompletedRooms;
        for (var index = 0; index < history.Length; index++)
        {
            var visit = history[index];
            if (visit.Act < 1
                || visit.Act > world.Act
                || visit.Floor < 1
                || visit.Floor > (world.Act == 1
                    && world.Map.GenerationProfileId
                        == PrototypeNativeOvergrowthMap.GenerationProfileId
                        ? PrototypeNativeOvergrowthMap.BossFloor
                        : PrototypeContent.Rules.FloorsPerAct)
                || string.IsNullOrWhiteSpace(visit.NodeId))
            {
                throw new InvalidOperationException(
                    "Completed-room history contains an invalid act, floor or node.");
            }

            if (index > 0)
            {
                var previous = history[index - 1];
                if (visit.Act < previous.Act
                    || (visit.Act == previous.Act
                        && visit.Floor <= previous.Floor))
                {
                    throw new InvalidOperationException(
                        "Completed-room history is not strictly ordered.");
                }
            }

            if (visit.Act == world.Act
                && world.Map.GenerationProfileId
                    is PrototypeContent.MapGenerationProfileId
                        or PrototypeNativeOvergrowthMap.GenerationProfileId)
            {
                var node = world.Map.Nodes
                    .FirstOrDefault(candidate =>
                        StringComparer.Ordinal.Equals(
                            candidate.NodeId,
                            visit.NodeId));
                if (node is null
                    || node.Floor != visit.Floor
                    || (node.RoomType != visit.RoomType
                        && !(node.RoomType == PrototypeRoomType.Unknown
                            && visit.RoomType is PrototypeRoomType.Combat
                                or PrototypeRoomType.Event
                                or PrototypeRoomType.Shop
                                or PrototypeRoomType.Treasure)))
                {
                    throw new InvalidOperationException(
                        "Completed-room history disagrees with the active generated map.");
                }
            }
        }

        if (world.Map.GenerationProfileId
            is not (PrototypeContent.MapGenerationProfileId
                or PrototypeNativeOvergrowthMap.GenerationProfileId))
        {
            return;
        }

        var completedThisAct = history
            .Where(visit => visit.Act == world.Act)
            .ToArray();
        for (var index = 0;
             index < completedThisAct.Length;
             index++)
        {
            if (completedThisAct[index].Floor != index + 1)
            {
                throw new InvalidOperationException(
                    "Generated map history must complete consecutive floors.");
            }
        }

        if (world.ActiveRoom is not null
            && world.Map.CurrentNodeId is { } activeId
            && completedThisAct.Any(visit =>
                StringComparer.Ordinal.Equals(
                    visit.NodeId, activeId)))
        {
            throw new InvalidOperationException(
                "An active room has already been completed.");
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

    private static void ValidateEvent(
        PlayerState player,
        EventState eventState)
    {
        var definition =
            PrototypeContent.Event(
                eventState.EventId);

        if (eventState.OfferedChoiceIds is { } offered
            && (offered.Length == 0
                || offered.Distinct(StringComparer.Ordinal).Count()
                    != offered.Length
                || offered.Any(id =>
                    !definition.Choices.Any(choice =>
                        StringComparer.Ordinal.Equals(choice.Id, id)))))
        {
            throw new InvalidOperationException(
                "Event option offer list is empty, duplicated or unknown.");
        }

        if (eventState.EventId
                == PrototypeNativeOvergrowthEvents.NeowEventId
            && eventState.OfferedChoiceIds is { Length: not 3 })
        {
            throw new InvalidOperationException(
                "Neow must offer exactly three distinct choices.");
        }

        if (eventState.ChosenChoiceId is null)
        {
            if (eventState.PendingDeckChoice is not null
                || eventState.PendingPotionReplacement is not null
                || eventState.RemainingPotionIds.Length > 0
                || eventState.RemainingDeckChoices.Length > 0)
            {
                throw new InvalidOperationException(
                    "Pending event continuation has no chosen event option.");
            }

            return;
        }

        var choice = definition.Choices
            .FirstOrDefault(item =>
                StringComparer.Ordinal.Equals(
                    item.Id,
                    eventState.ChosenChoiceId))
            ?? throw new InvalidOperationException(
                "Event state references an unknown chosen option.");

        var pending =
            eventState.PendingDeckChoice;
        var potionReplacement =
            eventState.PendingPotionReplacement;
        if (pending is null
            && potionReplacement is null)
        {
            throw new InvalidOperationException(
                "Resolved event choice should not remain in Event phase without an active continuation.");
        }

        if (pending is not null
            && potionReplacement is not null)
        {
            throw new InvalidOperationException(
                "An event cannot present two continuation prompts simultaneously.");
        }

        foreach (var queuedPotionId in eventState.RemainingPotionIds)
        {
            _ = PrototypeContent.Potion(queuedPotionId);
            if (!choice.Effects.Any(effect =>
                    (effect.Kind == PrototypeRunEffectKind.GainPotion
                        && StringComparer.Ordinal.Equals(
                            effect.PotionId, queuedPotionId))
                    || effect.Kind == PrototypeRunEffectKind.GainRandomPotion))
            {
                throw new InvalidOperationException(
                    "Queued event potion has no matching source effect.");
            }
        }

        foreach (var queuedDeckChoice in eventState.RemainingDeckChoices)
        {
            if (!StringComparer.Ordinal.Equals(
                    queuedDeckChoice.ChoiceId,
                    choice.Id)
                || queuedDeckChoice.RemainingSelections <= 0)
            {
                throw new InvalidOperationException(
                    "Queued event deck continuation has invalid provenance or selections.");
            }

            if (queuedDeckChoice.SourceRelicId is { } sourceRelic)
            {
                if (!player.Relics.Any(relic =>
                        StringComparer.Ordinal.Equals(
                            relic.RelicId, sourceRelic)))
                {
                    throw new InvalidOperationException(
                        "Queued event deck continuation references an unowned relic.");
                }

                var sourceSpec = PrototypeContent.Relic(sourceRelic)
                    .AcquisitionDeckChoice
                    ?? throw new InvalidOperationException(
                        "Queued event deck continuation has no relic source specification.");
                if (sourceSpec.Kind != queuedDeckChoice.Kind
                    || sourceSpec.UpgradeTransformedCards
                        != queuedDeckChoice.UpgradeTransformedCards)
                {
                    throw new InvalidOperationException(
                        "Queued event deck continuation disagrees with its relic.");
                }
            }
            else if (choice.DeckChoice is not { } eventSpec
                || eventSpec.Kind != queuedDeckChoice.Kind
                || eventSpec.UpgradeTransformedCards
                    != queuedDeckChoice.UpgradeTransformedCards)
            {
                throw new InvalidOperationException(
                    "Queued event deck continuation disagrees with its source choice.");
            }
        }

        if (pending is not null)
        {
            PrototypePersistentDeckChoiceKind expectedKind;
            bool expectedUpgradeTransformedCards;

            if (pending.SourceRelicId is { } sourceRelicId)
            {
                if (!player.Relics.Any(relic =>
                        StringComparer.Ordinal.Equals(
                            relic.RelicId,
                            sourceRelicId)))
                {
                    throw new InvalidOperationException(
                        "Pending relic-sourced event deck choice references an unowned relic.");
                }

                var relicSpec =
                    PrototypeContent.Relic(
                        sourceRelicId)
                        .AcquisitionDeckChoice
                    ?? throw new InvalidOperationException(
                        "Pending relic-sourced event deck choice has no source specification.");
                expectedKind = relicSpec.Kind;
                expectedUpgradeTransformedCards =
                    relicSpec.UpgradeTransformedCards;
            }
            else
            {
                var spec = choice.DeckChoice
                    ?? throw new InvalidOperationException(
                        "Pending event deck choice source option has no deck-choice specification.");
                expectedKind = spec.Kind;
                expectedUpgradeTransformedCards =
                    spec.UpgradeTransformedCards;
            }

            if (!StringComparer.Ordinal.Equals(
                    pending.ChoiceId,
                    choice.Id)
                || pending.Kind != expectedKind
                || pending.UpgradeTransformedCards
                    != expectedUpgradeTransformedCards)
            {
                throw new InvalidOperationException(
                    "Pending event deck choice disagrees with its source.");
            }

            if (pending.RemainingSelections <= 0
                || pending.CandidateCardInstanceIds.Length
                    < pending.RemainingSelections)
            {
                throw new InvalidOperationException(
                    "Pending event deck choice has an invalid selection count.");
            }

            if (pending.CandidateCardInstanceIds.Length
                != pending.CandidateCardInstanceIds
                    .Distinct()
                    .Count())
            {
                throw new InvalidOperationException(
                    "Pending event deck choice contains duplicate card instances.");
            }

            foreach (var cardInstanceId in
                     pending.CandidateCardInstanceIds)
            {
                var card = player.Deck
                    .FirstOrDefault(item =>
                        item.InstanceId
                            == cardInstanceId)
                    ?? throw new InvalidOperationException(
                        $"Pending event deck choice references missing card {cardInstanceId}.");

                if (pending.Kind is
                        PrototypePersistentDeckChoiceKind.Remove
                        or PrototypePersistentDeckChoiceKind.Transform
                    && PrototypeContent.Card(card.CardId)
                        .Eternal)
                {
                    throw new InvalidOperationException(
                        "Pending event deck choice includes an Eternal card.");
                }

                if (pending.Kind
                        == PrototypePersistentDeckChoiceKind.Upgrade
                    && card.UpgradeLevel != 0)
                {
                    throw new InvalidOperationException(
                        "Pending event upgrade choice includes an already-upgraded card.");
                }
            }
        }

        if (potionReplacement is not null)
        {
            if (!StringComparer.Ordinal.Equals(
                    potionReplacement.ChoiceId,
                    choice.Id))
            {
                throw new InvalidOperationException(
                    "Pending event potion replacement disagrees with its source option.");
            }

            if (!choice.Effects.Any(effect =>
                    (effect.Kind
                        == PrototypeRunEffectKind.GainPotion
                        && StringComparer.Ordinal.Equals(
                            effect.PotionId,
                            potionReplacement.PotionId))
                    || effect.Kind
                        == PrototypeRunEffectKind.GainRandomPotion))
            {
                throw new InvalidOperationException(
                    "Pending event potion replacement has no matching acquisition effect.");
            }

            _ = PrototypeContent.Potion(
                potionReplacement.PotionId);
            if (potionReplacement.CandidateSlots.Length == 0
                || potionReplacement.CandidateSlots.Length
                    != potionReplacement.CandidateSlots
                        .Distinct()
                        .Count())
            {
                throw new InvalidOperationException(
                    "Pending event potion replacement has invalid slot candidates.");
            }

            foreach (var slot in potionReplacement.CandidateSlots)
            {
                if (slot < 0
                    || slot >= player.PotionSlots.Length
                    || player.PotionSlots[slot] is null)
                {
                    throw new InvalidOperationException(
                        $"Pending event potion replacement references invalid slot {slot}.");
                }
            }
        }
    }

    private static void ValidateShop(PlayerState player, ShopState shop)
    {
        if (shop.PendingDeckChoice is { } pending)
        {
            if (pending.RemainingSelections <= 0
                || pending.CandidateCardInstanceIds.Length
                    < pending.RemainingSelections
                || pending.CandidateCardInstanceIds.Length
                    != pending.CandidateCardInstanceIds
                        .Distinct().Count())
            {
                throw new InvalidOperationException(
                    "Shop relic deck choice has invalid selections or duplicate candidates.");
            }

            if (!player.Relics.Any(relic =>
                    StringComparer.Ordinal.Equals(
                        relic.RelicId, pending.SourceRelicId)))
            {
                throw new InvalidOperationException(
                    "Shop deck-choice source relic is not owned.");
            }

            var spec = PrototypeContent.Relic(
                    pending.SourceRelicId)
                .AcquisitionDeckChoice
                ?? throw new InvalidOperationException(
                    "Shop relic deck choice has no acquisition specification.");
            if (spec.Kind != pending.Kind
                || spec.UpgradeTransformedCards
                    != pending.UpgradeTransformedCards)
            {
                throw new InvalidOperationException(
                    "Shop relic deck choice disagrees with the source relic.");
            }

            if (!shop.RelicOffers.Any(offer =>
                    offer.Sold
                    && StringComparer.Ordinal.Equals(
                        offer.ItemId, pending.SourceRelicId)))
            {
                throw new InvalidOperationException(
                    "Shop relic deck-choice source offer was not sold.");
            }

            foreach (var id in pending.CandidateCardInstanceIds)
            {
                var candidate = player.Deck
                    .FirstOrDefault(card => card.InstanceId == id)
                    ?? throw new InvalidOperationException(
                        $"Shop relic deck choice refers to missing card {id}.");
                if (PrototypeContent.Card(candidate.CardId).Eternal)
                {
                    throw new InvalidOperationException(
                        "Shop relic deck choice cannot target Eternal cards.");
                }
            }
        }

        if (shop.RemovalPrice <= 0)
        {
            throw new InvalidOperationException("Shop removal price must be positive.");
        }

        var offerIds = shop.CardOffers
            .Select(offer => offer.OfferId)
            .Concat(
                shop.PotionOffers.Select(
                    offer => offer.OfferId))
            .Concat(
                shop.RelicOffers.Select(
                    offer => offer.OfferId))
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

        if (shop.PotionOffers
            .Select(offer => offer.ItemId)
            .Distinct(StringComparer.Ordinal)
            .Count()
            != shop.PotionOffers.Length)
        {
            throw new InvalidOperationException(
                "Shop contains duplicate potion offers.");
        }

        foreach (var potion in shop.PotionOffers)
        {
            if (potion.Price <= 0
                || !PrototypeContent.Potions.ContainsKey(
                    potion.ItemId))
            {
                throw new InvalidOperationException(
                    "Shop contains an invalid potion offer.");
            }
        }

        if (shop.RelicOffers
            .Select(offer => offer.ItemId)
            .Distinct(StringComparer.Ordinal)
            .Count()
            != shop.RelicOffers.Length)
        {
            throw new InvalidOperationException(
                "Shop contains duplicate relic offers.");
        }

        foreach (var relic in shop.RelicOffers)
        {
            if (relic.Price <= 0
                || !PrototypeContent.Relics.ContainsKey(
                    relic.ItemId))
            {
                throw new InvalidOperationException(
                    "Shop contains an invalid relic offer.");
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

    private static void ValidateReward(
        PlayerState player,
        RewardState reward)
    {
        if (reward.ExtraCardOptionsUpgraded is { } upgrades
            && upgrades.Length !=
                (reward.ExtraCardOptions?.Length ?? 0))
        {
            throw new InvalidOperationException(
                "Reward card-upgrade metadata does not match reward groups.");
        }

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

        var relicOptions = reward.CurrentRelicOptions;
        if (relicOptions.Length
            != relicOptions
                .Distinct(StringComparer.Ordinal)
                .Count())
        {
            throw new InvalidOperationException(
                "Reward relic options contain duplicates.");
        }

        foreach (var relicId in relicOptions)
        {
            if (!PrototypeContent.Relics.ContainsKey(relicId))
            {
                throw new InvalidOperationException(
                    $"Reward contains unknown relic '{relicId}'.");
            }
        }

        if (!reward.RelicResolved
            && relicOptions.Length == 0)
        {
            throw new InvalidOperationException(
                "Unresolved relic reward has no relic options.");
        }

        if (reward.PendingDeckChoice is { } deckChoice)
        {
            if (!reward.RelicResolved)
            {
                throw new InvalidOperationException(
                    "Pending relic deck choice requires the relic reward itself to be resolved.");
            }

            if (deckChoice.RemainingSelections <= 0)
            {
                throw new InvalidOperationException(
                    "Pending relic deck choice has no remaining selections.");
            }

            if (deckChoice.CandidateCardInstanceIds.Length
                < deckChoice.RemainingSelections)
            {
                throw new InvalidOperationException(
                    "Pending relic deck choice has too few candidates.");
            }

            if (deckChoice.CandidateCardInstanceIds.Length
                != deckChoice.CandidateCardInstanceIds
                    .Distinct()
                    .Count())
            {
                throw new InvalidOperationException(
                    "Pending relic deck choice contains duplicate card instances.");
            }

            if (!player.Relics.Any(relic =>
                    StringComparer.Ordinal.Equals(
                        relic.RelicId,
                        deckChoice.SourceRelicId)))
            {
                throw new InvalidOperationException(
                    "Pending relic deck choice source relic is not owned.");
            }

            var sourceDefinition =
                PrototypeContent.Relic(
                    deckChoice.SourceRelicId);
            var sourceSpec =
                sourceDefinition.AcquisitionDeckChoice
                ?? throw new InvalidOperationException(
                    "Pending relic deck choice source has no acquisition deck-choice capability.");
            if (sourceSpec.Kind != deckChoice.Kind
                || sourceSpec.UpgradeTransformedCards
                    != deckChoice.UpgradeTransformedCards)
            {
                throw new InvalidOperationException(
                    "Pending relic deck choice disagrees with its source relic.");
            }

            foreach (var cardInstanceId in
                     deckChoice.CandidateCardInstanceIds)
            {
                var card = player.Deck
                    .FirstOrDefault(item =>
                        item.InstanceId == cardInstanceId)
                    ?? throw new InvalidOperationException(
                        $"Pending relic deck choice references missing card {cardInstanceId}.");
                if (PrototypeContent.Card(card.CardId)
                    .Eternal)
                {
                    throw new InvalidOperationException(
                        "Pending relic deck choice includes an Eternal card.");
                }
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
        foreach (var power in powers)
        {
            var definition = PrototypeContent.Power(
                power.PowerId);
            if (power.Stacks == 0
                || (power.Stacks < 0
                    && !definition.AllowNegative))
            {
                throw new InvalidOperationException(
                    $"{owner} power '{power.PowerId}' has invalid stacks {power.Stacks}.");
            }
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

    private static void ValidateNativeOvergrowthMap(RunWorldState world)
    {
        if (world.Act != 1)
        {
            throw new InvalidOperationException(
                "Native Overgrowth map profile is only valid in Act 1.");
        }

        var map = world.Map;
        var nodes = map.Nodes;
        if (nodes.Length == 0
            || nodes.Select(node => node.NodeId)
                .Distinct(StringComparer.Ordinal).Count() != nodes.Length)
        {
            throw new InvalidOperationException(
                "Native Overgrowth map is empty or contains duplicate nodes.");
        }

        var byId = nodes.ToDictionary(
            node => node.NodeId, StringComparer.Ordinal);
        var entries = map.EntryNodeIds ?? Array.Empty<string>();
        if (entries.Length < 2
            || entries.Distinct(StringComparer.Ordinal).Count()
                != entries.Length
            || entries.Any(id => !byId.TryGetValue(id, out var node)
                || node.Floor != 1))
        {
            throw new InvalidOperationException(
                "Native Overgrowth map must have distinct first-row entries.");
        }

        for (var floor = 1;
             floor <= PrototypeNativeOvergrowthMap.BossFloor;
             floor++)
        {
            var layer = nodes.Where(node => node.Floor == floor).ToArray();
            if (layer.Length == 0
                || layer.Length > PrototypeNativeOvergrowthMap.MapWidth)
            {
                throw new InvalidOperationException(
                    $"Native Overgrowth floor {floor} has an invalid node count.");
            }

            if (floor == 1
                && layer.Any(node => node.RoomType != PrototypeRoomType.Combat)
                || floor == PrototypeNativeOvergrowthMap.FirstTreasureFloor
                && layer.Any(node => node.RoomType != PrototypeRoomType.Treasure)
                || floor == PrototypeNativeOvergrowthMap.PreBossRestFloor
                && layer.Any(node => node.RoomType != PrototypeRoomType.Rest))
            {
                throw new InvalidOperationException(
                    "Native Overgrowth map has an invalid fixed room row.");
            }

            if (floor == PrototypeNativeOvergrowthMap.BossFloor
                && (layer.Length != 1
                    || layer[0].RoomType != PrototypeRoomType.Boss
                    || (layer[0].NextNodeIds?.Length ?? 0) != 0))
            {
                throw new InvalidOperationException(
                    "Native Overgrowth map does not terminate at one boss.");
            }
        }

        foreach (var node in nodes)
        {
            if (node.Act != 1
                || node.Floor is < 1 or > PrototypeNativeOvergrowthMap.BossFloor
                || !TryNativeColumn(node.NodeId, out var column)
                || column is < 0 or >= PrototypeNativeOvergrowthMap.MapWidth)
            {
                throw new InvalidOperationException(
                    "Native Overgrowth map has an invalid node coordinate.");
            }

            var children = node.NextNodeIds ?? Array.Empty<string>();
            if (children.Distinct(StringComparer.Ordinal).Count() != children.Length
                || (node.Floor < PrototypeNativeOvergrowthMap.BossFloor
                    && children.Length == 0))
            {
                throw new InvalidOperationException(
                    "Native Overgrowth map has duplicate or missing outgoing edges.");
            }

            foreach (var id in children)
            {
                if (!byId.TryGetValue(id, out var child)
                    || child.Floor != node.Floor + 1
                    || (node.Floor < PrototypeNativeOvergrowthMap.RoomRows
                        && (!TryNativeColumn(id, out var targetColumn)
                            || Math.Abs(targetColumn - column) > 1)))
                {
                    throw new InvalidOperationException(
                        "Native Overgrowth map has an invalid path edge.");
                }
            }
        }

        var reachable = new HashSet<string>(entries, StringComparer.Ordinal);
        var queue = new Queue<string>(entries);
        while (queue.Count > 0)
        {
            foreach (var id in byId[queue.Dequeue()].NextNodeIds
                     ?? Array.Empty<string>())
            {
                if (reachable.Add(id))
                {
                    queue.Enqueue(id);
                }
            }
        }

        if (reachable.Count != nodes.Length)
        {
            throw new InvalidOperationException(
                "Native Overgrowth map has unreachable nodes.");
        }

        if (map.CurrentNodeId is null)
        {
            if (world.Floor != 0)
            {
                throw new InvalidOperationException(
                    "Unentered native Overgrowth map must be at floor zero.");
            }
        }
        else if (!byId.TryGetValue(map.CurrentNodeId, out var current)
            || current.Floor != world.Floor)
        {
            throw new InvalidOperationException(
                "Native Overgrowth current position disagrees with run floor.");
        }

        if (world.UnknownRoomOdds is { } odds
            && (odds.MonsterWeight < 0 || odds.ShopWeight < 0
                || odds.TreasureWeight < 0))
        {
            throw new InvalidOperationException(
                "Native Overgrowth unknown-room weights must be nonnegative.");
        }
    }

    private static bool TryNativeColumn(string id, out int column)
    {
        column = -1;
        var parts = id.Split(':');
        return parts.Length == 3
            && parts[0] == "1"
            && int.TryParse(parts[2], out column);
    }

    private static void ValidateMap(RunWorldState world)
    {
        if (world.Map.GenerationProfileId
            == PrototypeNativeOvergrowthMap.GenerationProfileId)
        {
            ValidateNativeOvergrowthMap(world);
            return;
        }

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

            var nextNodeIds =
                node.NextNodeIds
                ?? Array.Empty<string>();
            if (nextNodeIds.Length
                != nextNodeIds
                    .Distinct(StringComparer.Ordinal)
                    .Count())
            {
                throw new InvalidOperationException(
                    $"Map node '{node.NodeId}' contains duplicate outgoing edges.");
            }

            foreach (var nextId in nextNodeIds)
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

        if (StringComparer.Ordinal.Equals(
                map.GenerationProfileId,
                PrototypeContent.MapGenerationProfileId))
        {
            ValidateGeneratedStrategicMap(
                map,
                nodesById);
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

    private static void ValidateGeneratedStrategicMap(
        MapState map,
        IReadOnlyDictionary<string, MapNodeState> nodesById)
    {
        var rules = PrototypeContent.Rules;
        for (var floor = 1;
             floor <= rules.FloorsPerAct;
             floor++)
        {
            var floorRule =
                rules.MapFloorRules.Single(rule =>
                    floor >= rule.MinFloor
                    && floor <= rule.MaxFloor);
            var nodes = map.Nodes
                .Where(node =>
                    node.Floor == floor)
                .ToArray();

            if (nodes.Length < floorRule.MinNodes
                || nodes.Length > floorRule.MaxNodes)
            {
                throw new InvalidOperationException(
                    $"Generated map floor {floor} width {nodes.Length} is outside [{floorRule.MinNodes}, {floorRule.MaxNodes}].");
            }

            foreach (var requiredRoom in
                     floorRule.RequiredRooms)
            {
                if (!nodes.Any(node =>
                        node.RoomType
                            == requiredRoom))
                {
                    throw new InvalidOperationException(
                        $"Generated map floor {floor} is missing required room {requiredRoom}.");
                }
            }

            if (floorRule
                .AvoidMatchingSpecialPredecessors)
            {
                var previous = map.Nodes
                    .Where(node =>
                        node.Floor == floor - 1)
                    .ToArray();
                foreach (var node in nodes.Where(node =>
                             node.RoomType
                                 != PrototypeRoomType.Combat))
                {
                    if (previous.Any(predecessor =>
                            predecessor.RoomType
                                == node.RoomType
                            && (predecessor.NextNodeIds
                                    ?? Array.Empty<string>())
                                .Contains(
                                    node.NodeId,
                                    StringComparer.Ordinal)))
                    {
                        throw new InvalidOperationException(
                            $"Generated map repeats special room {node.RoomType} across an edge into floor {floor}.");
                    }
                }
            }
        }

        var incoming = map.Nodes.ToDictionary(
            node => node.NodeId,
            _ => 0,
            StringComparer.Ordinal);
        foreach (var node in map.Nodes)
        {
            var next =
                node.NextNodeIds
                ?? Array.Empty<string>();
            if (node.Floor
                < rules.FloorsPerAct
                && (next.Length < 1
                    || next.Length > 2))
            {
                throw new InvalidOperationException(
                    $"Generated map node '{node.NodeId}' must have one or two outgoing edges.");
            }

            foreach (var nextId in next)
            {
                incoming[nextId]++;
            }
        }

        var entryIds =
            map.EntryNodeIds
            ?? Array.Empty<string>();
        foreach (var node in map.Nodes.Where(node =>
                     node.Floor > 1))
        {
            if (incoming[node.NodeId] == 0)
            {
                throw new InvalidOperationException(
                    $"Generated map node '{node.NodeId}' has no incoming route.");
            }
        }

        var reachable =
            new HashSet<string>(
                entryIds,
                StringComparer.Ordinal);
        var queue =
            new Queue<string>(
                entryIds);
        while (queue.Count > 0)
        {
            var node =
                nodesById[queue.Dequeue()];
            foreach (var nextId in
                     node.NextNodeIds
                     ?? Array.Empty<string>())
            {
                if (reachable.Add(nextId))
                {
                    queue.Enqueue(nextId);
                }
            }
        }

        if (reachable.Count != map.Nodes.Length)
        {
            throw new InvalidOperationException(
                "Generated map contains a node that is unreachable from every entry.");
        }

        var boss = map.Nodes.Single(node =>
            node.RoomType
                == PrototypeRoomType.Boss);
        var canReachBoss =
            new HashSet<string>(
                [boss.NodeId],
                StringComparer.Ordinal);
        for (var floor =
                 rules.FloorsPerAct - 1;
             floor >= 1;
             floor--)
        {
            foreach (var node in map.Nodes.Where(node =>
                         node.Floor == floor))
            {
                if ((node.NextNodeIds
                        ?? Array.Empty<string>())
                    .Any(canReachBoss.Contains))
                {
                    canReachBoss.Add(
                        node.NodeId);
                }
            }
        }

        if (canReachBoss.Count
            != map.Nodes.Length)
        {
            throw new InvalidOperationException(
                "Generated map contains a dead-end route that cannot reach the boss.");
        }

        if (!map.Nodes.Any(node =>
                node.Floor
                    < rules.FloorsPerAct - 1
                && (node.NextNodeIds?.Length ?? 0)
                    == 2))
        {
            throw new InvalidOperationException(
                "Generated map contains no meaningful pre-boss branch.");
        }

        if (!map.Nodes.Any(node =>
                node.Floor > 1
                && node.Floor
                    < rules.FloorsPerAct
                && incoming[node.NodeId] > 1))
        {
            throw new InvalidOperationException(
                "Generated map contains no pre-boss route convergence.");
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

        var zoneLocations = combat.Hand
            .Select(id => (Id: id, Location: "Hand"))
            .Concat(combat.DrawPile.Select(id => (Id: id, Location: "DrawPile")))
            .Concat(combat.DiscardPile.Select(id => (Id: id, Location: "DiscardPile")))
            .Concat(combat.ExhaustPile.Select(id => (Id: id, Location: "ExhaustPile")))
            .Concat(combat.PlayCardIds.Select(id => (Id: id, Location: "PlayPile")))
            .Concat(combat.ChoiceCardIds.Select(id => (Id: id, Location: "ChoicePool")))
            .ToList();
        var zones = zoneLocations
            .Select(item => item.Id)
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
            var suspendedLocations = suspendedSources
                .Select((id, index) =>
                    (Id: id, Location: $"SuspendedSource[{index}]"));
            var locations = zoneLocations
                .Concat(suspendedLocations)
                .GroupBy(item => item.Id)
                .Where(group => group.Count() > 1)
                .Select(group =>
                {
                    var card = combat.Cards.Single(item =>
                        item.InstanceId == group.Key);
                    return $"{group.Key}:{card.CardId} ({string.Join(", ", group.Select(item => item.Location))})";
                })
                .ToArray();
            throw new InvalidOperationException(
                "A card instance occurs in multiple combat zones/continuations: "
                + string.Join("; ", locations));
        }

        if (represented.Length != known.Count)
        {
            throw new InvalidOperationException("Combat cards are not fully represented by zones/continuations.");
        }

        if (combat.ChoiceCardIds.Length > 0
            && (combat.PendingChoice is null
                || combat.PendingChoice.Selection.SourceZone
                    != PrototypeCardZone.ChoicePool))
        {
            throw new InvalidOperationException(
                "Combat choice pool exists without a matching pending choice.");
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
                PrototypeCardZone.PlayPile => combat.PlayCardIds,
                PrototypeCardZone.ChoicePool => combat.ChoiceCardIds,
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

            var sequentialSelected =
                pending.SequentialSelectedCardInstanceIds
                ?? Array.Empty<long>();
            if (!pending.Selection.SequentialOptional
                && sequentialSelected.Length > 0)
            {
                throw new InvalidOperationException(
                    "Non-sequential pending choice carries sequential selections.");
            }

            if (sequentialSelected.Length
                != sequentialSelected.Distinct().Count())
            {
                throw new InvalidOperationException(
                    "Sequential pending choice contains duplicate accumulated selections.");
            }

            if (sequentialSelected.Any(cardId =>
                    !sourceZone.Contains(cardId))
                || sequentialSelected.Any(cardId =>
                    pending.CandidateCardInstanceIds.Contains(cardId)))
            {
                throw new InvalidOperationException(
                    "Sequential pending choice has an invalid accumulated selection.");
            }

            var availableSelectionCapacity =
                pending.CandidateCardInstanceIds.Length
                + sequentialSelected.Length;
            if (pending.Selection.MinSelections < 0
                || pending.Selection.MaxSelections
                    < pending.Selection.MinSelections
                || pending.Selection.MaxSelections
                    > availableSelectionCapacity
                || sequentialSelected.Length
                    > pending.Selection.MaxSelections
                || (!pending.Selection.SequentialOptional
                    && pending.Selection.MaxSelections
                        > pending.CandidateCardInstanceIds.Length))
            {
                throw new InvalidOperationException(
                    "Pending choice has an invalid selection range.");
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
