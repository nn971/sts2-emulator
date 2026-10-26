namespace Sts2Emulator.Core;

public sealed partial record RunState
{
    public void Validate()
    {
        var state = this;
        RunConfiguration.ValidateState(state);
        state.Stochastic.Validate(state);
        NativeAncientRewardsV111.ValidateEventContext(state);
        state.Stochastic.ValidateNativeEventContexts(state);
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

        if (world.Event is { } activeEvent)
            activeEvent.Stochastic.ValidateControlledChoice(
                activeEvent, state);

        state.Stochastic.ValidateControlledEventRewardChoice(state);

        if (!StringComparer.Ordinal.Equals(world.RulesetId, state.Configuration?.RulesetId ?? PrototypeContent.RulesetId))
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

        foreach (var relic in state.Player.Relics.Where(relic =>
            relic.RelicId == NativeAncientRewardsV111.PumpkinCandleId))
            _ = NativeAncientRewardsV111.KindleCount(relic);
        foreach (var relic in state.Player.Relics)
        {
            if (relic.IsMelted && !relic.IsWax)
                throw new InvalidOperationException("Only wax relics can be melted.");
            if (relic.RelicId == NativeAncientRewardsV111.ToyBoxId)
                _ = NativeAncientRewardsV111.ToyBoxCombatsSeen(relic);
            if (relic.RelicId == NativePaelRewardsV111.WingId)
                _ = NativePaelRewardsV111.RewardsSacrificed(relic);
            if (relic.RelicId == NativePaelRewardsV111.ToothId)
                _ = NativePaelRewardsV111.ToothCards(relic);
            if (relic.RelicId == NativePaelRewardsV111.LegionId)
                _ = NativePaelRewardsV111.LegionSkin(relic);
            if (relic.RelicId == PrototypeNativeWingedBoots.RelicId
                && !PrototypeNativeWingedBoots.HasValidState(relic))
                throw new InvalidOperationException(
                    "Winged Boots uses must be between zero and three.");
        }
        if (world.LastCombatEndDecisionIndex is { } endIndex
            && (endIndex < 0 || endIndex > state.DecisionIndex))
            throw new InvalidOperationException("Combat-end callback stamp exceeds the decision cursor.");
        foreach (var relic in state.Player.Relics.Where(relic =>
            relic.RelicId == NativeAncientRewardsV111.GoldenCompassId))
            _ = PrototypeGoldenPathMapV111.AcquisitionActIndex(relic);
        if (world.Map.GenerationProfileId == PrototypeGoldenPathMapV111.ProfileId
            && !PrototypeGoldenPathMapV111.IsActive(state.Player, world.Act))
            throw new InvalidOperationException("Golden path map requires a Compass acquired in this act.");

        if (world.ShopRemovalsUsed < 0)
        {
            throw new InvalidOperationException(
                "Run-wide shop removal count cannot be negative.");
        }

        if (world.PotionRewardOddsThousandths % 100 != 0)
        {
            throw new InvalidOperationException(
                "Potion reward odds must preserve native 10-percent increments.");
        }

        if (world.CardRarityOffsetBasisPoints <
                PrototypeNativeCardRarityOdds.InitialOffsetBasisPoints
            || world.CardRarityOffsetBasisPoints >
                PrototypeNativeCardRarityOdds.MaximumOffsetBasisPoints
            || world.CardRarityOffsetBasisPoints % 50 != 0)
        {
            throw new InvalidOperationException(
                "Native card-rarity offset is outside the source-shaped pity grid.");
        }

        if (world.Act < 1 || world.Act > PrototypeContent.Rules.Acts)
        {
            throw new InvalidOperationException($"Invalid act {world.Act}.");
        }

        var currentBossFloor = world.Map.GenerationProfileId switch
        {
            PrototypeGoldenPathMapV111.ProfileId => PrototypeGoldenPathMapV111.BossFloor,
            PrototypeNativeOvergrowthMap.GenerationProfileId
                or PrototypeNativeOvergrowthMap.LegacyGenerationProfileId
                or PrototypeNativeUnderdocks.GenerationProfileId =>
                PrototypeNativeOvergrowthMap.BossFloor,
            PrototypeNativeLaterActMap.HiveProfileId
                or PrototypeNativeLaterActMap.GloryProfileId =>
                PrototypeNativeLaterActMap.RoomRows(world.Act) + 1,
            _ => PrototypeContent.Rules.FloorsPerAct
        };
        if (world.Map.GenerationProfileId == PrototypeNativeLaterActMap.GloryProfileId
            && world.Stochastic.LaterActEncounterBag?.SecondBossEncounterId
                is not null)
            currentBossFloor++;
        if (world.Floor < 0 || world.Floor > currentBossFloor)
        {
            throw new InvalidOperationException($"Invalid floor {world.Floor}.");
        }

        // An opt-in native Overgrowth opening starts with three potion
        // slots; Tight Belt at A4+ subtracts one. This run-provenance
        // flag survives Act 1 so later hybrid prototype acts keep the
        // same belt capacity. Legacy prototype starts remain unchanged.
        var basePotionSlots = world.NativeOvergrowthOpening
            ? state.Ascension >= 4 ? 2 : 3
            : PrototypeContent.Rules.PotionSlots;
        if (state.Player.PotionSlots.Length
            != basePotionSlots
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

        if (state.Player.Deck.Any(card =>
                card.Enchantment is { Disabled: true } disabled
                && (disabled.Kind != PrototypeCardEnchantmentKind.Vigorous
                    || PrototypeContent.Card(card.CardId).Type
                        != PrototypeCardType.Attack)))
        {
            throw new InvalidOperationException(
                "Only an Attack with Vigorous can retain a disabled enchantment.");
        }

        if (world.NextCardInstanceId <= deckIds.DefaultIfEmpty(0).Max())
        {
            throw new InvalidOperationException("Next card instance ID does not exceed all live card IDs.");
        }

        if (world.Map.Nodes.Length > 0)
        {
            ValidateMap(world);
        }

        ValidateEncounterHistory(world);
        ValidateEventHistory(world);
        ValidateCompletedRoomHistory(world, state.Player);
        ValidatePhaseState(state, world);

        if (world.Combat is not null)
        {
            world.Combat.Validate(state.Player);
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
        if (world.Reward?.SourceRoom == "Relic:ToyBox" && world.CustomRewardReturn is null)
            throw new InvalidOperationException("Toy Box custom rewards have no return continuation.");
        if (world.Reward?.SourceRoom == "Relic:GlassEye" && world.CustomRewardReturn is null)
            throw new InvalidOperationException("Glass Eye custom rewards have no return continuation.");
        if (world.CustomRewardReturn is { } customReturn)
        {
            var sourceRoom = world.Reward?.SourceRoom;
            var isEventReward = sourceRoom is not null
                && sourceRoom.StartsWith("Event:", StringComparison.Ordinal)
                && world.Event is not null
                && sourceRoom == "Event:" + world.Event.EventId
                && customReturn.Phase is RunPhase.Event or RunPhase.Reward;
            if (state.Phase != RunPhase.Reward
                || sourceRoom is not ("Relic:ToyBox" or "Relic:GlassEye")
                    && !isEventReward)
                throw new InvalidOperationException("Custom relic rewards require an active reward continuation.");
            if (sourceRoom == "Relic:ToyBox")
            {
                if (!state.Player.Relics.Any(relic => relic.RelicId == NativeAncientRewardsV111.ToyBoxId)
                    || world.Reward!.ExtraRelicRewardIds?.Length != 5
                    || world.Reward.ExtraRelicWaxFlags is not { Length: 5 } waxFlags
                    || waxFlags.Any(flag => !flag) || world.Reward.EndsAct)
                    throw new InvalidOperationException("Toy Box custom rewards require five reserved wax relics.");
            }
            else if (sourceRoom == "Relic:GlassEye"
                && (!state.Player.Relics.Any(relic => relic.RelicId == PrototypeNativeOrobasRelics.GlassEyeId)
                    || !world.Reward!.IndependentSelection
                    || world.Reward.CardOptionsForGroup(0).Length != 3
                    || world.Reward.ExtraCardOptions is not { Length: 4 }
                    || world.Reward.EndsAct))
            {
                throw new InvalidOperationException("Glass Eye custom rewards require five independent card groups.");
            }
            for (var saved = customReturn; saved is not null; saved = saved.Parent)
            {
                if (saved.Phase is not (RunPhase.Reward or RunPhase.Shop or RunPhase.Event or RunPhase.MapChoice)
                    || (saved.Phase == RunPhase.Reward) != (saved.Reward is not null)
                    || saved.Phase == RunPhase.Shop && world.Shop is null
                    || saved.Phase == RunPhase.Event && world.Event is null)
                    throw new InvalidOperationException("Invalid custom reward return phase.");
                if (saved.Reward is not null)
                    ValidateReward(state.Player, saved.Reward);
            }
        }
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
                // Native event rewards retain a live event continuation.
                // Check the quest's pre-rolled offers even while reward
                // choices, rather than event choices, are displayed.
                if (world.Event?.EventId ==
                    PrototypeNativeHiveEvents.WarHistorianRepyId)
                    world.Event.Validate(state.Player);
                break;

            case RunPhase.Shop:
                if (world.Shop is null)
                {
                    throw new InvalidOperationException("Shop phase has no shop state.");
                }

                ValidateShop(state, state.Player, world.Shop);
                break;

            case RunPhase.Event:
                if (world.Event is null)
                {
                    throw new InvalidOperationException(
                        "Event phase has no event state.");
                }

                world.Event.Validate(state.Player);
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

    private static void ValidateCompletedRoomHistory(
        RunWorldState world, PlayerState player)
    {
        var history = world.CompletedRooms;
        for (var index = 0; index < history.Length; index++)
        {
            var visit = history[index];
            if (visit.Act < 1
                || visit.Act > world.Act
                || visit.Floor < 1
                || visit.Floor > (world.Map.GenerationProfileId
                        == PrototypeContent.MapGenerationProfileId
                    ? PrototypeContent.Rules.FloorsPerAct
                    : PrototypeGoldenPathMapV111.IsActive(player, visit.Act)
                        ? PrototypeGoldenPathMapV111.BossFloor
                        : visit.Act == 1
                        ? PrototypeNativeOvergrowthMap.BossFloor
                        : PrototypeNativeLaterActMap.RoomRows(visit.Act) + 1
                            + (visit.Act == 3
                                && world.Stochastic.LaterActEncounterBag?
                                    .SecondBossEncounterId is not null
                                ? 1 : 0))
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
                && (world.Map.GenerationProfileId is
                    PrototypeContent.MapGenerationProfileId
                    or PrototypeNativeOvergrowthMap.GenerationProfileId
                    or PrototypeNativeOvergrowthMap.LegacyGenerationProfileId
                    or PrototypeNativeUnderdocks.GenerationProfileId
                    || (world.RulesetId == V111Build.RulesetId
                        && world.Map.GenerationProfileId is
                            PrototypeNativeLaterActMap.HiveProfileId
                            or PrototypeNativeLaterActMap.GloryProfileId
                            or PrototypeGoldenPathMapV111.ProfileId)))
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

        // Isolated Hive/Glory event fixtures deliberately start in the
        // middle of an act. Consecutive history is required only by the
        // configured whole-route engine, not by those source test seams.
        var fullRouteMap =
            world.Map.GenerationProfileId is
                PrototypeContent.MapGenerationProfileId
                or PrototypeNativeOvergrowthMap.GenerationProfileId
                or PrototypeNativeOvergrowthMap.LegacyGenerationProfileId
                or PrototypeNativeUnderdocks.GenerationProfileId
            || (world.RulesetId == V111Build.RulesetId
                && world.Map.GenerationProfileId is
                    PrototypeNativeLaterActMap.HiveProfileId
                    or PrototypeNativeLaterActMap.GloryProfileId
                    or PrototypeGoldenPathMapV111.ProfileId);
        if (!fullRouteMap)
            return;

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
        if (world.Act < active.MinAct || world.Act > active.MaxAct
            || (active.Weight <= 0
                && !NativeAncientRewardsV111.IsOpeningEvent(world)
                && !NativeTanxEventV111.IsOpeningEvent(world)
                && !NativeVakuuEventV111.IsOpeningEvent(world)
                && !NativeNonupeipeEventV111.IsOpeningEvent(world)
                && active.Id != PrototypeNativeHiveEvents.WarHistorianRepyId
                && !(world.Act is 2 or 3
                    && (world.Map.GenerationProfileId ==
                        PrototypeNativeLaterActMap.ProfileId(world.Act)
                        || world.Map.GenerationProfileId == PrototypeGoldenPathMapV111.ProfileId)
                    && ExperimentalSilentThreeActRouteV111.ContainsEvent(
                        active.Id, world.Act))
                && active.Id !=
                    PrototypeNativeGloryEvents.HungryForMushroomsId
                && active.Id != PrototypeNativeGloryEvents.ReflectionsId
                && active.Id != PrototypeNativeGloryEvents.RoundTeaPartyId
                && active.Id !=
                    PrototypeNativeGloryEvents.GraveOfTheForgottenId
                && active.Id != PrototypeNativeGloryEvents.TrialId
                && active.Id != PrototypeNativeGloryEvents.BattlewornDummyId
                && active.Id != PrototypeNativeGloryEvents.TinkerTimeId))
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

    private static void ValidateShop(
        RunState state, PlayerState player, ShopState shop)
    {
        var parasolHeld = player.Relics.Any(relic =>
            StringComparer.Ordinal.Equals(
                relic.RelicId,
                NativeGloryAncientsV111.LordsParasolId));
        if ((shop.LordsParasolPurchaseFlowActive
                || shop.LordsParasolPendingPotionOfferId is not null
                || shop.LordsParasolRemovalChoicePending)
            && (!parasolHeld || !shop.LordsParasolPurchaseFlowActive))
        {
            throw new InvalidOperationException(
                "Lord's Parasol shop continuation requires its relic and active flow.");
        }

        if (shop.LordsParasolPendingPotionOfferId is { } potionOfferId
            && (Array.IndexOf(player.PotionSlots, null) >= 0
                || !shop.PotionOffers.Any(offer =>
                    offer.OfferId == potionOfferId && !offer.Sold)))
        {
            throw new InvalidOperationException(
                "Lord's Parasol potion choice must target an unsold offer while the belt is full.");
        }

        if (shop.LordsParasolRemovalChoicePending
            && (shop.RemovalUsed
                || !player.Deck.Any(card =>
                    !PrototypeCardRules.IsEternal(card))))
        {
            throw new InvalidOperationException(
                "Lord's Parasol removal choice needs an unused removal and eligible card.");
        }

        if (shop.PendingDeckChoice is { } pending)
        {
            if (pending.RemainingSelections < 0
                || (pending.RemainingSelections == 0
                    && pending.CardIdAfterChoices is null)
                || pending.MinimumSelectionsRemaining < 0
                || pending.MinimumSelectionsRemaining
                    > pending.RemainingSelections
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
                    != pending.UpgradeTransformedCards
                || pending.CardIdAfterChoices
                    != PrototypeContent.Relic(pending.SourceRelicId)
                        .CardIdAfterAcquisitionDeckChoices)
            {
                throw new InvalidOperationException(
                    "Shop relic deck choice disagrees with the source relic.");
            }
            if (pending.CardIdAfterChoices is { } postChoiceCardId)
                _ = PrototypeContent.Card(postChoiceCardId);

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
                if (pending.Kind is PrototypePersistentDeckChoiceKind.Remove
                        or PrototypePersistentDeckChoiceKind.Transform
                    && PrototypeCardRules.IsEternal(candidate))
                {
                    throw new InvalidOperationException(
                        "Shop relic deck choice cannot target Eternal cards.");
                }
                if (pending.Kind == PrototypePersistentDeckChoiceKind.Transform
                    && spec.TransformToCardId is not null
                    && PrototypeContent.Card(candidate.CardId).Type
                        == PrototypeCardType.Quest)
                {
                    throw new InvalidOperationException(
                        "Shop relic deck choice cannot target Quest cards.");
                }
                if (pending.Kind == PrototypePersistentDeckChoiceKind.Enchant
                    && !PrototypeGameEngine.CanSelectEventDeckCard(candidate, pending.Kind,
                        null, spec.EnchantmentKind, false))
                    throw new InvalidOperationException("Shop relic enchant includes an ineligible card.");
                if (spec.RequiresUpgradable
                    && candidate.UpgradeLevel >= PrototypeContent.Card(candidate.CardId).MaxUpgradeLevel)
                    throw new InvalidOperationException("Shop relic choice includes a non-upgradable card.");
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

        if (shop.CardOffers.Count(offer => offer.OnSale) > 1
            || shop.PotionOffers.Any(offer => offer.OnSale)
            || shop.RelicOffers.Any(offer => offer.OnSale))
        {
            throw new InvalidOperationException(
                "Only one character card can be marked on sale.");
        }

        foreach (var offer in shop.CardOffers)
        {
            var characterCard = PrototypeContent.RewardCardPool.Contains(
                offer.ItemId, StringComparer.Ordinal);
            var colorlessCard = PrototypeColorlessCards.ImplementedShopPool.Contains(
                offer.ItemId, StringComparer.Ordinal);
            if (colorlessCard && state.Configuration?.Profile is { } profile)
            {
                var rarity = PrototypeContent.Card(offer.ItemId).Rarity;
                colorlessCard = rarity is PrototypeCardRarity.Uncommon or
                    PrototypeCardRarity.Rare;
                if (colorlessCard)
                    colorlessCard = PrototypeColorlessCards
                        .MerchantPoolForProfile(
                            rarity, profile.FullyUnlocked,
                            profile.UnlockedColorlessEpochs)
                        .Contains(offer.ItemId, StringComparer.Ordinal);
            }
            if (offer.Price <= 0
                || !(characterCard || colorlessCard)
                || (colorlessCard && offer.OnSale)
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
            if (world.Map.CurrentNodeId is not null)
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

        var promotedUnderdocksEncounter =
            world.Act == 1
            && world.ActOneRegion == PrototypeActOneRegion.Underdocks
            && (PrototypeNativeUnderdocks.SupportedWeakEncounterIds.Contains(
                    encounter.Id, StringComparer.Ordinal)
                || PrototypeNativeUnderdocks.SupportedNormalEncounterIds.Contains(
                    encounter.Id, StringComparer.Ordinal)
                || PrototypeNativeUnderdocks.SupportedEliteEncounterIds.Contains(
                    encounter.Id, StringComparer.Ordinal)
                || PrototypeNativeUnderdocks.SupportedBossEncounterIds.Contains(
                    encounter.Id, StringComparer.Ordinal));

        if (encounter.RoomType != world.ActiveRoom
            || world.Act < encounter.MinAct
            || world.Act > encounter.MaxAct
            || world.Floor < encounter.MinFloor
            || world.Floor > encounter.MaxFloor
            || (encounter.Weight <= 0
                && !promotedOvergrowthEncounter
                && !promotedUnderdocksEncounter
                && !(world.Map.GenerationProfileId is
                    PrototypeNativeLaterActMap.HiveProfileId
                    or PrototypeNativeLaterActMap.GloryProfileId
                    or PrototypeGoldenPathMapV111.ProfileId
                    && ExperimentalSilentThreeActRouteV111.ContainsEncounter(
                        encounter.Id, world.Act))))
        {
            throw new InvalidOperationException(
                $"Encounter '{encounter.Id}' is ineligible at act {world.Act}, floor {world.Floor}.");
        }
    }

    private static void ValidateReward(
        PlayerState player,
        RewardState reward)
    {
        if ((reward.ExtraGoldOptions is null) != (reward.ExtraGoldGroupsResolved is null)
            || (reward.ExtraGoldOptions is not null && (!reward.IndependentSelection
                || reward.ExtraGoldOptions.Length != reward.ExtraGoldGroupsResolved!.Length
                || reward.ExtraGoldOptions.Any(amount => amount <= 0))))
            throw new InvalidOperationException("Extra gold offers require positive amounts and matching independent resolution flags.");

        if (reward.GoldOption is < 0
            || (reward.GoldOption is null && !reward.GoldResolved)
            || (reward.GoldOption is not null
                && reward.SourceRoom is not ("Combat" or "Elite" or "Boss")))
        {
            throw new InvalidOperationException(
                "Combat gold offer is invalid or unresolved without an offer.");
        }

        // Native custom event rewards (such as Glory's Trial and
        // Battleworn Dummy) may expose independent offers, just like
        // combat rewards. Their event continuation is validated by
        // CompleteReward/ValidatePhaseState rather than the source name.
        var registeredEventReward =
            reward.SourceRoom.StartsWith("Event:", StringComparison.Ordinal)
            && PrototypeContent.Events.ContainsKey(
                reward.SourceRoom["Event:".Length..]);
        var registeredRelicReward = reward.SourceRoom is
            "Relic:ToyBox" or "Relic:GlassEye";
        if (reward.IndependentSelection
            && reward.SourceRoom is not ("Combat" or "Elite" or "Boss" or "Rest" or "Relic:ToyBox")
            && !registeredEventReward && !registeredRelicReward)
        {
            throw new InvalidOperationException(
                "Independent reward selection requires a recognized combat, rest or event reward.");
        }

        var extraCards = reward.ExtraCardOptions ?? Array.Empty<string[]>();
        var extraRelics = reward.ExtraRelicRewardIds
            ?? Array.Empty<string>();
        if ((reward.ExtraPotionOptions is null)
                != (reward.ExtraPotionGroupsResolved is null)
            || reward.ExtraPotionOptions is { } extraPotions
                && (!reward.IndependentSelection
                    || reward.ExtraPotionGroupsResolved!.Length
                        != extraPotions.Length
                    || extraPotions.Any(id =>
                        !PrototypeContent.Potions.ContainsKey(id)))
            || !reward.IndependentSelection
                && reward.ExtraPotionGroupsResolved is not null)
            throw new InvalidOperationException(
                "Extra potion reward groups are invalid.");
        if (reward.CardRewardGenerators is { } generators)
        {
            if (generators.Length != 1 + extraCards.Length)
                throw new InvalidOperationException(
                    "Card reward generators must match the reward groups.");
            for (var index = 0; index < generators.Length; index++)
            {
                if (generators[index] is not { } generator)
                    continue;
                if (generator.Act < 1
                    || !Enum.IsDefined(generator.Kind)
                    || (generator.Kind == PrototypeCardRewardGeneratorKind.Encounter
                        ? generator.Room is not (PrototypeRoomType.Combat
                            or PrototypeRoomType.Elite
                            or PrototypeRoomType.Boss)
                        : generator.Room is not null)
                    || (generator.Kind == PrototypeCardRewardGeneratorKind.RarityPool
                        ? generator.Rarity is not (PrototypeCardRarity.Common
                            or PrototypeCardRarity.Uncommon
                            or PrototypeCardRarity.Rare)
                        : generator.Rarity is not null)
                    || (generator.CardType is { } cardType
                        && (generator.Kind != PrototypeCardRewardGeneratorKind.RarityPool
                            || cardType is not (PrototypeCardType.Attack
                                or PrototypeCardType.Skill
                                or PrototypeCardType.Power))))
                    throw new InvalidOperationException(
                        "Card reward generator metadata is invalid.");
                var options = reward.CardOptionsForGroup(index);
                if (generator.Kind == PrototypeCardRewardGeneratorKind.RarePool
                    && options.Any(id => PrototypeContent.Card(id).Rarity
                        != PrototypeCardRarity.Rare))
                    throw new InvalidOperationException(
                        "Rare-pool generator contains a non-Rare option.");
                if (generator.Kind == PrototypeCardRewardGeneratorKind.RarityPool
                    && options.Any(id => PrototypeContent.Card(id).Rarity
                        != generator.Rarity))
                    throw new InvalidOperationException(
                        "Rarity-pool generator contains an option of another rarity.");
                if (generator.CardType is { } requiredCardType
                    && options.Any(id => PrototypeContent.Card(id).Type
                        != requiredCardType))
                    throw new InvalidOperationException(
                        "Filtered card reward generator contains an option of another type.");
            }
        }
        if (reward.ExtraRelicWaxFlags is { } waxFlags && waxFlags.Length != extraRelics.Length)
            throw new InvalidOperationException("Wax flags must match the persisted extra relic offers.");

        if (reward.ExtraCardRewardsResolved < 0
            || reward.ExtraCardRewardsResolved > extraCards.Length
            || reward.IndependentSelection
                && (reward.ExtraCardGroupsResolved is null
                    || reward.ExtraCardGroupsResolved.Length != extraCards.Length
                    || reward.ExtraCardGroupsResolved.Count(resolved => resolved)
                        != reward.ExtraCardRewardsResolved)
            || !reward.IndependentSelection
                && reward.ExtraCardGroupsResolved is not null)
        {
            throw new InvalidOperationException(
                "Extra card reward group resolution is invalid.");
        }

        if (reward.IndependentSelection
            && (reward.ExtraRelicGroupsResolved is null
                || reward.ExtraRelicGroupsResolved.Length != extraRelics.Length
                || reward.ExtraRelicGroupsResolved.Count(resolved => resolved)
                    != reward.ExtraRelicsResolved)
            || !reward.IndependentSelection
                && reward.ExtraRelicGroupsResolved is not null)
        {
            throw new InvalidOperationException(
                "Extra relic reward group resolution is invalid.");
        }

        if (reward.ExtraRelicsResolved < 0
            || reward.ExtraRelicsResolved > extraRelics.Length
            || extraRelics.Distinct(StringComparer.Ordinal).Count()
                != extraRelics.Length
            || extraRelics.Any(id =>
                !PrototypeContent.Relics.ContainsKey(id)))
        {
            throw new InvalidOperationException(
                "Extra relic rewards have invalid progression or IDs.");
        }

        if (reward.ExtraPotionGroupsResolved is { } potionGroups
            && potionGroups.Length
                != (reward.ExtraPotionOptions?.Length ?? 0))
            throw new InvalidOperationException(
                "Extra potion reward group resolution is invalid.");

        if (reward.ExtraCardOptionsUpgraded is { } upgrades
            && upgrades.Length !=
                (reward.ExtraCardOptions?.Length ?? 0))
        {
            throw new InvalidOperationException(
                "Reward card-upgrade metadata does not match reward groups.");
        }

        if (reward.CardOptionUpgradeFlags is { } flags
            && flags.Length != reward.CardOptions.Length)
        {
            throw new InvalidOperationException(
                "Card-option upgrade flags disagree with the options.");
        }

        if (reward.ExtraCardOptionUpgradeFlags is { } extraFlags
            && (extraFlags.Length !=
                    (reward.ExtraCardOptions?.Length ?? 0)
                || extraFlags.Where((group, index) =>
                    group is null
                    || group.Length != reward.ExtraCardOptions![index].Length)
                    .Any()))
        {
            throw new InvalidOperationException(
                "Extra reward upgrade flags disagree with the options.");
        }

        if (reward.CardOptionEnchantments is { } enchantments
            && enchantments.Length != reward.CardOptions.Length)
        {
            throw new InvalidOperationException(
                "Card-option enchantments disagree with the options.");
        }

        if (reward.ExtraCardOptionEnchantments is { } extraEnchantments
            && (extraEnchantments.Length !=
                    (reward.ExtraCardOptions?.Length ?? 0)
                || extraEnchantments.Where((group, index) =>
                    group is null
                    || group.Length != reward.ExtraCardOptions![index].Length)
                    .Any()))
        {
            throw new InvalidOperationException(
                "Extra reward enchantments disagree with the options.");
        }

        if (reward.CardBundles is { } bundles)
        {
            if (reward.CardOptions.Length > 0
                || bundles.Length != 2
                || bundles.Any(bundle =>
                    bundle.Length != 3
                    || bundle.Take(2).Any(id =>
                        !PrototypeContent.RewardCardPool.Contains(id,
                            StringComparer.Ordinal)
                        || PrototypeContent.Card(id).Rarity
                            != PrototypeCardRarity.Common)
                    || !PrototypeContent.RewardCardPool.Contains(
                        bundle[2], StringComparer.Ordinal)
                    || PrototypeContent.Card(bundle[2]).Rarity
                        != PrototypeCardRarity.Uncommon)
                || bundles.SelectMany(bundle => bundle)
                    .Distinct(StringComparer.Ordinal).Count() != 6)
            {
                throw new InvalidOperationException(
                    "Card-bundle reward must contain two distinct Common-Common-Uncommon offers.");
            }
        }

        if (reward.CardOptions.Length != reward.CardOptions.Distinct(StringComparer.Ordinal).Count())
        {
            throw new InvalidOperationException("Reward card options contain duplicates.");
        }

        foreach (var cardId in reward.CardOptions)
        {
            if (!PrototypeContent.RewardCardPool.Contains(
                    cardId, StringComparer.Ordinal)
                && !PrototypeColorlessCards.ImplementedRewardPool.Contains(
                    cardId, StringComparer.Ordinal))
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
            if (!reward.RelicResolved && reward.ExtraRelicsResolved == 0)
            {
                throw new InvalidOperationException(
                    "Pending relic deck choice requires an acquired relic reward.");
            }

            if (deckChoice.RemainingSelections < 0
                || (deckChoice.RemainingSelections == 0
                    && deckChoice.CardIdAfterChoices is null
                    && deckChoice.HpLossAfterChoices <= 0))
            {
                throw new InvalidOperationException(
                    "Pending relic deck choice has no remaining selections.");
            }

            if (deckChoice.HpLossAfterChoices < 0)
            {
                throw new InvalidOperationException(
                    "Pending relic deck choice has a negative HP loss.");
            }

            if (deckChoice.MinimumSelectionsRemaining < 0
                || deckChoice.MinimumSelectionsRemaining
                    > deckChoice.RemainingSelections)
            {
                throw new InvalidOperationException(
                    "Pending relic deck choice has invalid minimum selections.");
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
            if (deckChoice.CardIdAfterChoices
                != sourceDefinition.CardIdAfterAcquisitionDeckChoices)
            {
                throw new InvalidOperationException(
                    "Pending relic deck choice disagrees with its post-choice card effect.");
            }
            if (deckChoice.CardIdAfterChoices is { } postChoiceCardId)
                _ = PrototypeContent.Card(postChoiceCardId);
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
                if (deckChoice.Kind is PrototypePersistentDeckChoiceKind.Remove
                        or PrototypePersistentDeckChoiceKind.Transform
                    && PrototypeCardRules.IsEternal(card))
                {
                    throw new InvalidOperationException(
                        "Pending relic deck choice includes an Eternal card.");
                }
                if (deckChoice.Kind == PrototypePersistentDeckChoiceKind.Enchant
                    && !PrototypeGameEngine.CanSelectRelicDeckCard(card, sourceSpec))
                    throw new InvalidOperationException("Pending relic enchant includes an ineligible card.");
                if (sourceSpec.RequiresUpgradable
                    && card.UpgradeLevel >= PrototypeContent.Card(card.CardId).MaxUpgradeLevel)
                    throw new InvalidOperationException("Pending relic choice includes a non-upgradable card.");
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

    private static void ValidateNativeLaterActMap(RunWorldState world)
    {
        if (world.Act is not (2 or 3)
            || world.Map.GenerationProfileId !=
                PrototypeNativeLaterActMap.ProfileId(world.Act))
            throw new InvalidOperationException(
                "Native Hive/Glory map profile disagrees with the act.");

        var roomRows = PrototypeNativeLaterActMap.RoomRows(world.Act);
        var bossFloor = roomRows + 1;
        var hasSecondBoss = world.Act == 3
            && world.Stochastic.LaterActEncounterBag?.SecondBossEncounterId
                is not null;
        var finalFloor = bossFloor + (hasSecondBoss ? 1 : 0);
        var treasureFloor = bossFloor - 7;
        var map = world.Map;
        var nodes = map.Nodes;
        if (nodes.Length == 0
            || nodes.Select(node => node.NodeId)
                .Distinct(StringComparer.Ordinal).Count() != nodes.Length)
        {
            throw new InvalidOperationException(
                "Native later-act map is empty or contains duplicate nodes.");
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
                "Native later-act map must have distinct first-row entries.");
        }

        for (var floor = 1;
             floor <= finalFloor;
             floor++)
        {
            var layer = nodes.Where(node => node.Floor == floor).ToArray();
            if (layer.Length == 0
                || layer.Length > PrototypeNativeLaterActMap.MapWidth)
            {
                throw new InvalidOperationException(
                    $"Native later-act floor {floor} has an invalid node count.");
            }

            if (floor == 1
                && layer.Any(node => node.RoomType != PrototypeRoomType.Combat)
                || floor == treasureFloor
                && layer.Any(node => node.RoomType != PrototypeRoomType.Treasure)
                || floor == roomRows
                && layer.Any(node => node.RoomType != PrototypeRoomType.Rest))
            {
                throw new InvalidOperationException(
                    "Native later-act map has an invalid fixed room row.");
            }

            if (floor == bossFloor
                && (layer.Length != 1
                    || layer[0].RoomType != PrototypeRoomType.Boss
                    || (layer[0].NextNodeIds?.Length ?? 0)
                        != (hasSecondBoss ? 1 : 0)))
            {
                throw new InvalidOperationException(
                    "Native later-act map has an invalid first boss node.");
            }
            if (hasSecondBoss && floor == finalFloor
                && (layer.Length != 1
                    || layer[0].RoomType != PrototypeRoomType.Boss
                    || (layer[0].NextNodeIds?.Length ?? 0) != 0))
            {
                throw new InvalidOperationException(
                    "A10 Glory map must terminate at its second boss.");
            }
        }

        foreach (var node in nodes)
        {
            if (node.Act != world.Act
                || node.Floor < 1 || node.Floor > finalFloor
                || !TryLaterActColumn(node.NodeId, world.Act, out var column)
                || column is < 0 or >= PrototypeNativeLaterActMap.MapWidth)
            {
                throw new InvalidOperationException(
                    "Native later-act map has an invalid node coordinate.");
            }

            var children = node.NextNodeIds ?? Array.Empty<string>();
            if (children.Distinct(StringComparer.Ordinal).Count() != children.Length
                || (node.Floor < finalFloor
                    && children.Length == 0))
            {
                throw new InvalidOperationException(
                    "Native later-act map has duplicate or missing outgoing edges.");
            }

            foreach (var id in children)
            {
                if (!byId.TryGetValue(id, out var child)
                    || child.Floor != node.Floor + 1
                    || (node.Floor < roomRows
                        && (!TryLaterActColumn(id, world.Act, out var targetColumn)
                            || Math.Abs(targetColumn - column) > 1)))
                {
                    throw new InvalidOperationException(
                        "Native later-act map has an invalid path edge.");
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
                "Native later-act map has unreachable nodes.");
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
                "Native later-act current position disagrees with run floor.");
        }

        if (world.UnknownRoomOdds is { } odds
            && (odds.MonsterWeight < 0 || odds.ShopWeight < 0
                || odds.TreasureWeight < 0))
        {
            throw new InvalidOperationException(
                "Native later-act unknown-room weights must be nonnegative.");
        }
    }

    private static bool TryLaterActColumn(
        string id, int act, out int column)
    {
        column = -1;
        var parts = id.Split(':');
        return parts.Length == 3
            && parts[0] == act.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
            && int.TryParse(parts[1], out var floor)
            && floor is >= 1 and <= 15
            && int.TryParse(parts[2], out column);
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
        if (world.Map.GenerationProfileId == PrototypeGoldenPathMapV111.ProfileId)
        {
            var expected = PrototypeGoldenPathMapV111.Generate(world.Act);
            if (world.Map.Nodes.Length != expected.Nodes.Length
                || !world.Map.Nodes.Zip(expected.Nodes).All(pair =>
                    pair.First.NodeId == pair.Second.NodeId
                    && pair.First.Act == pair.Second.Act && pair.First.Floor == pair.Second.Floor
                    && pair.First.RoomType == pair.Second.RoomType
                    && (pair.First.NextNodeIds ?? []).SequenceEqual(pair.Second.NextNodeIds ?? []))
                || !(world.Map.EntryNodeIds ?? []).SequenceEqual(expected.EntryNodeIds ?? [])
                || (world.Map.CurrentNodeId is not null
                    && !expected.Nodes.Any(node => node.NodeId == world.Map.CurrentNodeId)))
                throw new InvalidOperationException("Golden path geometry disagrees with its source layout.");
            return;
        }
        if (world.Map.GenerationProfileId is
            PrototypeNativeLaterActMap.HiveProfileId
            or PrototypeNativeLaterActMap.GloryProfileId)
        {
            ValidateNativeLaterActMap(world);
            return;
        }

        if (world.Map.GenerationProfileId
            == PrototypeNativeHiveEvents.RepyFixtureMapProfileId)
        {
            ValidateRepyForcedFixtureMap(world);
            return;
        }

        if (PrototypeNativeOvergrowthMap.IsGenerationProfile(
                world.Map.GenerationProfileId)
            || world.Map.GenerationProfileId
                == PrototypeNativeUnderdocks.GenerationProfileId)
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

    /// <summary>
    /// The Glory quest slice deliberately supplies two manual nodes.
    /// The profile is explicit and is forbidden outside act 3; full
    /// production map validation continues through the existing path.
    /// </summary>
    private static void ValidateRepyForcedFixtureMap(RunWorldState world)
    {
        var map = world.Map;
        if (world.Act != 3 || map.Nodes.Length == 0)
            throw new InvalidOperationException(
                "Repy forced map requires a non-empty Glory fixture.");
        var nodes = map.Nodes.ToDictionary(node => node.NodeId,
            StringComparer.Ordinal);
        if (nodes.Count != map.Nodes.Length
            || map.EntryNodeIds is not { Length: > 0 }
            || map.EntryNodeIds.Distinct(StringComparer.Ordinal).Count()
                != map.EntryNodeIds.Length)
            throw new InvalidOperationException(
                "Repy fixture map has duplicate or missing entry nodes.");
        foreach (var entry in map.EntryNodeIds)
        {
            if (!nodes.TryGetValue(entry, out var node)
                || node.Floor != 1)
                throw new InvalidOperationException(
                    "Repy fixture entry must be on floor one.");
        }
        foreach (var node in map.Nodes)
        {
            if (node.Act != 3 || node.Floor is < 1 or > 6
                || node.NextNodeIds is { } next
                    && next.Distinct(StringComparer.Ordinal).Count()
                        != next.Length)
                throw new InvalidOperationException(
                    "Invalid Glory quest fixture node.");
            foreach (var target in node.NextNodeIds
                ?? Array.Empty<string>())
            {
                if (!nodes.TryGetValue(target, out var other)
                    || other.Floor != node.Floor + 1)
                    throw new InvalidOperationException(
                        "Glory quest fixture edges must advance one floor.");
            }
        }

        if (map.CurrentNodeId is null)
        {
            if (world.Floor != 0)
                throw new InvalidOperationException(
                    "Unentered quest fixture must start at floor zero.");
        }
        else if (!nodes.TryGetValue(map.CurrentNodeId, out var current)
            || current.Floor != world.Floor)
        {
            throw new InvalidOperationException(
                "Glory quest fixture cursor disagrees with run floor.");
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


}
