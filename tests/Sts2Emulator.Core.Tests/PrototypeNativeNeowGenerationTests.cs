using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeNeowGenerationTests
{
    private static string OptionId(string name) =>
        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(name);

    [Fact]
    public void NeowCandidateCatalogIncludesConditionalPositiveRelics()
    {
        Assert.Equal(14,
            PrototypeNativeOvergrowthEvents.NeowPositiveRelicNames.Length);
        Assert.Equal(6,
            PrototypeNativeOvergrowthEvents.NeowConditionalPositiveRelicNames.Length);
        Assert.Equal(10,
            PrototypeNativeOvergrowthEvents.NeowCursedRelicNames.Length);
        var definition = PrototypeContent.Event(
            PrototypeNativeOvergrowthEvents.NeowEventId);
        Assert.Equal(30, definition.Choices.Length);
        Assert.Equal(30, definition.Choices
            .Select(choice => choice.Id)
            .Distinct(StringComparer.Ordinal).Count());

        foreach (var name in
            PrototypeNativeOvergrowthEvents.NeowPositiveRelicNames
                .Concat(PrototypeNativeOvergrowthEvents
                    .NeowConditionalPositiveRelicNames)
                .Concat(PrototypeNativeOvergrowthEvents.NeowCursedRelicNames))
        {
            Assert.NotNull(PrototypeContent.Relic(
                PrototypeNativeOvergrowthEvents.NeowRelicId(name)));
            Assert.Contains(definition.Choices,
                choice => choice.Id == OptionId(name));
        }
    }

    [Fact]
    public void SeededNeowOffersFollowCursedPairExclusionsAndSpecialGroups()
    {
        var basePositives = PrototypeNativeOvergrowthEvents
            .NeowPositiveRelicNames
            .Concat(PrototypeNativeOvergrowthEvents
                .NeowConditionalPositiveRelicNames)
            .ToHashSet(StringComparer.Ordinal);
        var curses = PrototypeNativeOvergrowthEvents
            .NeowCursedRelicNames
            .ToHashSet(StringComparer.Ordinal);
        var incompatible = new Dictionary<string, string[]>
        {
            ["CursedPearl"] = ["GoldenPearl"],
            ["HeftyTablet"] = ["ArcaneScroll"],
            ["LeafyPoultice"] = ["NewLeaf"],
            ["PrecariousShears"] = ["PreciseScissors"],
            ["NeowsSacrifice"] = ["PhialHolster", "LostCoffer"]
        };
        var seenConditional = new HashSet<string>(StringComparer.Ordinal);
        var seenCurses = new HashSet<string>(StringComparer.Ordinal);

        for (var seed = 0; seed < 800; seed++)
        {
            var label = $"neow-native-generator-{seed}";
            var a = PrototypeNativeOvergrowthEvents.GenerateNeowOfferedChoiceIds(
                PrototypeRng.CreateBundle(label));
            var b = PrototypeNativeOvergrowthEvents.GenerateNeowOfferedChoiceIds(
                PrototypeRng.CreateBundle(label));
            Assert.Equal(a, b);
            Assert.Equal(3, a.Length);
            Assert.Equal(3, a.Distinct(StringComparer.Ordinal).Count());
            // Massive Scroll is restricted to multiplayer in native v0.111.0.
            Assert.DoesNotContain(OptionId("MassiveScroll"), a);

            var names = a.Select(id => id[
                ("take_proto.native.neow.").Length..]).ToArray();
            Assert.All(names.Take(2), name =>
                Assert.Contains(basePositives, candidate =>
                    StringComparer.Ordinal.Equals(
                        candidate.ToLowerInvariant(), name)));
            Assert.Contains(curses, curse =>
                StringComparer.Ordinal.Equals(
                    curse.ToLowerInvariant(), names[2]));
            var curseName = curses.Single(curse =>
                StringComparer.Ordinal.Equals(
                    curse.ToLowerInvariant(), names[2]));
            seenCurses.Add(curseName);
            if (incompatible.TryGetValue(curseName, out var excluded))
            {
                Assert.DoesNotContain(names.Take(2), name =>
                    excluded.Any(item =>
                        StringComparer.OrdinalIgnoreCase.Equals(item, name)));
            }

            if (curseName == "LargeCapsule")
            {
                Assert.DoesNotContain("lavarock", names.Take(2));
                Assert.DoesNotContain("smallcapsule", names.Take(2));
            }

            // Each conditional positive comes from exactly one of three
            // mutually exclusive source-backed pairs.
            Assert.False(names.Contains("lavarock")
                && names.Contains("smallcapsule"));
            Assert.False(names.Contains("nutritiousoyster")
                && names.Contains("stonehumidifier"));
            Assert.False(names.Contains("neowstalisman")
                && names.Contains("pomander"));

            foreach (var candidate in
                PrototypeNativeOvergrowthEvents.NeowConditionalPositiveRelicNames)
            {
                if (names.Take(2).Contains(
                    candidate.ToLowerInvariant(), StringComparer.Ordinal))
                {
                    seenConditional.Add(candidate);
                }
            }
        }

        Assert.Equal(10, seenCurses.Count);
        Assert.Equal(6, seenConditional.Count);
    }

    [Fact]
    public void NeowsTalismanUpgradesTheLastStrikeAndDefendOnAcquisition()
    {
        var engine = new PrototypeGameEngine();
        var state = ForceNeowOption("talisman", "NeowsTalisman");
        var before = state.Player.Deck.Select(card => card.InstanceId).ToArray();
        state = engine.Step(state,
            engine.GetLegalActions(state).Single(action =>
                action.ReadPayload<EventChoicePayload>().ChoiceId
                    == OptionId("NeowsTalisman"))).State;

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Contains(state.Player.Relics, relic =>
            relic.RelicId == PrototypeNativeOvergrowthEvents
                .NeowRelicId("NeowsTalisman"));
        Assert.Equal(before,
            state.Player.Deck.Select(card => card.InstanceId).ToArray());
        Assert.Equal(2, state.Player.Deck.Count(card => card.UpgradeLevel == 1));
        var strikes = state.Player.Deck
            .Where(card => card.CardId == "proto.silent.strike").ToArray();
        var defends = state.Player.Deck
            .Where(card => card.CardId == "proto.silent.defend").ToArray();
        Assert.Equal(new[] { 0, 0, 0, 0, 1 },
            strikes.Select(card => card.UpgradeLevel).ToArray());
        Assert.Equal(new[] { 0, 0, 0, 0, 1 },
            defends.Select(card => card.UpgradeLevel).ToArray());
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void NutritiousOysterAddsElevenMaxHpAtNeow()
    {
        var engine = new PrototypeGameEngine();
        var state = ForceNeowOption("oyster", "NutritiousOyster");
        var oldMax = state.Player.MaxHp;
        state = engine.Step(state,
            engine.GetLegalActions(state).Single(action =>
                action.ReadPayload<EventChoicePayload>().ChoiceId
                    == OptionId("NutritiousOyster"))).State;

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(oldMax + 11, state.Player.MaxHp);
        Assert.Equal(oldMax + 11, state.Player.Hp);
        Assert.Contains(state.Player.Relics, relic =>
            relic.RelicId == PrototypeNativeOvergrowthEvents
                .NeowRelicId("NutritiousOyster"));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void GoldenPearlGrantsOneHundredFiftyGoldOnAcquisition()
    {
        var engine = new PrototypeGameEngine();
        var state = ForceNeowOption("golden-pearl", "GoldenPearl");
        var initialGold = state.Player.Gold;
        state = engine.Step(state,
            engine.GetLegalActions(state).Single(action =>
                action.ReadPayload<EventChoicePayload>().ChoiceId
                    == OptionId("GoldenPearl"))).State;

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(initialGold + 150, state.Player.Gold);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void PomanderOpensExactlyOneDeckUpgradePromptBeforeMap()
    {
        var engine = new PrototypeGameEngine();
        var state = ForceNeowOption("pomander", "Pomander");
        state = engine.Step(state,
            engine.GetLegalActions(state).Single(action =>
                action.ReadPayload<EventChoicePayload>().ChoiceId
                    == OptionId("Pomander"))).State;

        Assert.Equal(RunPhase.Event, state.Phase);
        var pending = state.World!.Event!.PendingDeckChoice!;
        Assert.Equal(PrototypePersistentDeckChoiceKind.Upgrade,
            pending.Kind);
        Assert.Equal(1, pending.RemainingSelections);
        Assert.Equal(13, pending.CandidateCardInstanceIds.Length);
        Assert.Equal(PrototypeNativeOvergrowthEvents.NeowRelicId("Pomander"),
            pending.SourceRelicId);
        Assert.DoesNotContain(engine.GetLegalActions(state),
            action => action.Kind == "choose_map_node");

        var action = engine.GetLegalActions(state)
            .Single(option =>
                option.Kind == "choose_event_deck_card"
                && option.ReadPayload<ChooseEventDeckCardPayload>()
                    .CardInstanceId == 1);
        state = engine.Step(state, action).State;

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(1,
            state.Player.Deck.Single(card => card.InstanceId == 1).UpgradeLevel);
        Assert.Single(state.Player.Deck,
            card => card.UpgradeLevel == 1);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void CursedPearlGrantsGoldAndUnremovableGreed()
    {
        var engine = new PrototypeGameEngine();
        var state = ForceNeowOption("cursed-pearl", "CursedPearl");
        var initialGold = state.Player.Gold;
        state = engine.Step(state,
            engine.GetLegalActions(state).Single(action =>
                action.ReadPayload<EventChoicePayload>().ChoiceId
                    == OptionId("CursedPearl"))).State;

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(initialGold + 333, state.Player.Gold);
        Assert.Equal(14, state.Player.Deck.Length);
        var greed = Assert.Single(state.Player.Deck,
            card => card.CardId == "proto.native.neow.greed");
        var definition = PrototypeContent.Card(greed.CardId);
        Assert.True(definition.Eternal);
        Assert.True(definition.Unplayable);
        Assert.Equal(PrototypeCardRarity.Curse, definition.Rarity);
        Assert.DoesNotContain(definition.Id, PrototypeContent.RewardCardPool);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void PreciseScissorsRemovesOneSelectedStartingCardBeforeTheMap()
    {
        var engine = new PrototypeGameEngine();
        var state = ForceNeowOption("precise-scissors", "PreciseScissors");
        var before = state.Player.Deck;
        state = engine.Step(state, engine.GetLegalActions(state).Single(
            action => action.ReadPayload<EventChoicePayload>().ChoiceId
                == OptionId("PreciseScissors"))).State;

        Assert.Equal(RunPhase.Event, state.Phase);
        var pending = Assert.IsType<PrototypePendingEventDeckChoiceState>(
            state.World!.Event!.PendingDeckChoice);
        Assert.Equal(PrototypePersistentDeckChoiceKind.Remove, pending.Kind);
        Assert.Equal(1, pending.RemainingSelections);
        Assert.Equal(13, pending.CandidateCardInstanceIds.Length);
        Assert.Equal(PrototypeNativeOvergrowthEvents.NeowRelicId("PreciseScissors"),
            pending.SourceRelicId);
        PrototypeStateInvariants.Validate(state);

        state = engine.Step(state, engine.GetLegalActions(state).Single(
            action => action.Kind == "choose_event_deck_card"
                && action.ReadPayload<ChooseEventDeckCardPayload>()
                    .CardInstanceId == 1)).State;

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(12, state.Player.Deck.Length);
        Assert.Equal(before.Skip(1).Select(card => card.InstanceId),
            state.Player.Deck.Select(card => card.InstanceId));
        Assert.Equal(14, state.World!.NextCardInstanceId);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void NewLeafTransformsExactlyOneSelectedStartingCard()
    {
        var engine = new PrototypeGameEngine();
        var state = ForceNeowOption("new-leaf", "NewLeaf");
        var before = state.Player.Deck;
        state = engine.Step(state, engine.GetLegalActions(state).Single(
            action => action.ReadPayload<EventChoicePayload>().ChoiceId
                == OptionId("NewLeaf"))).State;

        Assert.Equal(RunPhase.Event, state.Phase);
        var pending = Assert.IsType<PrototypePendingEventDeckChoiceState>(
            state.World!.Event!.PendingDeckChoice);
        Assert.Equal(PrototypePersistentDeckChoiceKind.Transform, pending.Kind);
        Assert.Equal(1, pending.RemainingSelections);
        Assert.Equal(13, pending.CandidateCardInstanceIds.Length);

        state = engine.Step(state, engine.GetLegalActions(state).Single(
            action => action.Kind == "choose_event_deck_card"
                && action.ReadPayload<ChooseEventDeckCardPayload>()
                    .CardInstanceId == 1)).State;

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(13, state.Player.Deck.Length);
        Assert.Equal(before.Select(card => card.InstanceId),
            state.Player.Deck.Select(card => card.InstanceId));
        Assert.NotEqual(before[0].CardId, state.Player.Deck[0].CardId);
        Assert.Equal(PrototypeCardRarity.Basic,
            PrototypeContent.Card(before[0].CardId).Rarity);
        Assert.NotEqual(PrototypeCardRarity.Basic,
            PrototypeContent.Card(state.Player.Deck[0].CardId).Rarity);
        Assert.Equal(before.Skip(1).Select(card => card.CardId),
            state.Player.Deck.Skip(1).Select(card => card.CardId));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void SilkenTressConsumesAllStartingGold()
    {
        var engine = new PrototypeGameEngine();
        var state = ForceNeowOption("silken-tress", "SilkenTress");
        Assert.True(state.Player.Gold > 0);
        state = engine.Step(state, engine.GetLegalActions(state).Single(
            action => action.ReadPayload<EventChoicePayload>().ChoiceId
                == OptionId("SilkenTress"))).State;

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(0, state.Player.Gold);
        Assert.Contains(state.Player.Relics, relic =>
            relic.RelicId == PrototypeNativeOvergrowthEvents
                .NeowRelicId("SilkenTress"));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void ArcaneScrollAddsExactlyOneRareCardToTheStartingDeck()
    {
        var engine = new PrototypeGameEngine();
        var state = ForceNeowOption("arcane-scroll", "ArcaneScroll");
        var initial = state.Player.Deck;
        state = engine.Step(state, engine.GetLegalActions(state).Single(
            action => action.ReadPayload<EventChoicePayload>().ChoiceId
                == OptionId("ArcaneScroll"))).State;

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(initial.Length + 1, state.Player.Deck.Length);
        Assert.Equal(initial.Select(card => card.InstanceId),
            state.Player.Deck.Take(initial.Length)
                .Select(card => card.InstanceId));
        var gained = state.Player.Deck[^1];
        Assert.Equal(PrototypeCardRarity.Rare,
            PrototypeContent.Card(gained.CardId).Rarity);
        Assert.Equal(14, gained.InstanceId);
        Assert.Equal(15, state.World!.NextCardInstanceId);
        Assert.Contains(state.Player.Relics, relic =>
            relic.RelicId == PrototypeNativeOvergrowthEvents
                .NeowRelicId("ArcaneScroll"));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void StoneHumidifierTriggersAfterRestHealButNotRestTraining()
    {
        var initial = PrototypeNativeOvergrowthRunFactory.Create(
            "stone-humidifier-rest");
        var world = initial.World!;
        var restNode = world.Map.Nodes.First(node =>
            node.Floor == PrototypeNativeOvergrowthMap.PreBossRestFloor);
        // Construct a coherent completed-room history along a real
        // predecessor path; generated maps enforce consecutive floors.
        var reversePath = new List<MapNodeState>();
        var current = restNode;
        for (var floor = restNode.Floor - 1; floor >= 1; floor--)
        {
            var nextId = current.NodeId;
            current = world.Map.Nodes.First(node =>
                node.Floor == floor
                && node.NextNodeIds?.Contains(
                    nextId, StringComparer.Ordinal) == true);
            reversePath.Add(current);
        }

        reversePath.Reverse();
        var completed = reversePath.Select(node =>
            new PrototypeCompletedRoomRecord(
                1, node.Floor, node.NodeId,
                node.RoomType == PrototypeRoomType.Unknown
                    ? PrototypeRoomType.Event
                    : node.RoomType)).ToArray();
        var prepared = initial with
        {
            Phase = RunPhase.Rest,
            Player = initial.Player with
            {
                Hp = 35,
                Relics = initial.Player.Relics
                    .Append(new RelicInstance(
                        PrototypeNativeOvergrowthEvents
                            .NeowRelicId("StoneHumidifier"),
                        PrototypeJson.EmptyObject()))
                    .ToArray()
            },
            World = world with
            {
                Floor = restNode.Floor,
                ActiveRoom = PrototypeRoomType.Rest,
                CompletedRoomHistory = completed,
                Event = null,
                Map = world.Map with
                {
                    CurrentNodeId = restNode.NodeId
                }
            }
        };

        var engine = new PrototypeGameEngine();
        PrototypeStateInvariants.Validate(prepared);
        var healed = engine.Step(prepared,
            GameAction.Empty("rest_heal")).State;
        Assert.Equal(75, healed.Player.MaxHp);
        Assert.Equal(61, healed.Player.Hp);
        Assert.Equal(RunPhase.MapChoice, healed.Phase);
        PrototypeStateInvariants.Validate(healed);

        var trained = engine.Step(prepared,
            GameAction.Empty("rest_train")).State;
        Assert.Equal(74, trained.Player.MaxHp);
        Assert.Equal(39, trained.Player.Hp);
        Assert.Equal(RunPhase.MapChoice, trained.Phase);
        PrototypeStateInvariants.Validate(trained);
    }

    private static RunState ForceNeowOption(string seed, string name)
    {
        var state = PrototypeNativeOvergrowthRunFactory.Create(
            "neow-pickup-" + seed);
        var world = state.World!;
        var eventState = world.Event!;
        var offer = OptionId(name);
        var other = OptionId("ArcaneScroll");
        if (StringComparer.Ordinal.Equals(other, offer))
        {
            other = OptionId("BoomingConch");
        }

        return state with
        {
            World = world with
            {
                Event = eventState with
                {
                    OfferedChoiceIds =
                    [
                        offer,
                        other,
                        OptionId(name == "GoldenPearl"
                            ? "DowsingRod" : "CursedPearl")
                    ]
                }
            }
        };
    }
}
