using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111OrobasRelicDependencyTests
{
    private const string SandCastle =
        PrototypeNativeOrobasRelics.SandCastleId;

    [Fact]
    public void SandCastleUpgradesSixUniqueUpgradableCardsOnAcquisition()
    {
        var player = PlayerWithSandCastle(eligibleCount: 8);
        var before = CanonicalJson.Sha256(player);
        var rng = PrototypeRng.CreateBundle("orobas-sand-castle");
        var result = Dispatch(player, rng);

        var eligibleIds = player.Deck.Take(8)
            .Select(card => card.InstanceId).ToHashSet();
        var upgradedIds = result.Deck.Where(card =>
                eligibleIds.Contains(card.InstanceId)
                && card.UpgradeLevel == 1)
            .Select(card => card.InstanceId).ToHashSet();
        Assert.Equal(6, upgradedIds.Count);
        Assert.True(upgradedIds.IsSubsetOf(eligibleIds));
        Assert.All(result.Deck.Where(card =>
            !eligibleIds.Contains(card.InstanceId)), card =>
            Assert.Equal(player.Deck.Single(original =>
                original.InstanceId == card.InstanceId).UpgradeLevel,
                card.UpgradeLevel));
        Assert.Equal((ulong?)7, rng.Streams.Single(stream =>
            stream.StreamId == "event").CallCount);
        Assert.Equal(before, CanonicalJson.Sha256(player));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(Dispatch(PlayerWithSandCastle(8),
                PrototypeRng.CreateBundle("orobas-sand-castle"))));
    }

    [Fact]
    public void SandCastleWithNoUpgradableCardsDoesNotDrawOrChangeDeck()
    {
        var player = PlayerWithSandCastle(eligibleCount: 0);
        var deck = CanonicalJson.Sha256(player.Deck);
        var rng = PrototypeRng.CreateBundle("orobas-sand-castle-empty");

        var result = Dispatch(player, rng);

        Assert.Equal(deck, CanonicalJson.Sha256(result.Deck));
        Assert.Equal((ulong?)0, rng.Streams.Single(stream =>
            stream.StreamId == "event").CallCount);
        Assert.Single(result.Relics, relic => relic.RelicId == SandCastle);
    }

    [Fact]
    public void TouchOfOrobasReplacesSilentStarterAndLimitsDrawBonusToThreeTurns()
    {
        var player = PrototypeNativeUnderdocksRunFactory.Create(
            "orobas-touch-of-orobas").Player;
        Assert.True(PrototypeNativeOrobasRelics
            .IsTouchOfOrobasEligible(player));
        var originalDeck = CanonicalJson.Sha256(player.Deck);
        player = player with
        {
            Relics = player.Relics.Append(new RelicInstance(
                PrototypeNativeOrobasRelics.TouchOfOrobasId,
                PrototypeJson.EmptyObject())).ToArray()
        };

        var result = Dispatch(player,
            PrototypeRng.CreateBundle("orobas-touch-of-orobas"),
            PrototypeNativeOrobasRelics.TouchOfOrobasId);

        Assert.DoesNotContain(result.Relics, relic =>
            relic.RelicId == PrototypeNativeOrobasRelics.RingOfTheSnakeId);
        Assert.Single(result.Relics, relic =>
            relic.RelicId == PrototypeNativeOrobasRelics.RingOfTheDrakeId);
        var touch = Assert.Single(result.Relics, relic =>
            relic.RelicId == PrototypeNativeOrobasRelics.TouchOfOrobasId);
        Assert.Equal(PrototypeNativeOrobasRelics.RingOfTheSnakeId,
            touch.PersistentState.GetProperty("StarterRelicId").GetString());
        Assert.Equal(PrototypeNativeOrobasRelics.RingOfTheDrakeId,
            touch.PersistentState.GetProperty("UpgradedRelicId").GetString());
        Assert.Equal(originalDeck, CanonicalJson.Sha256(result.Deck));
        Assert.Equal(new[] { 2, 2, 2, 0 }, Enumerable.Range(1, 4)
            .Select(turn => PrototypeNativeOrobasRelics
                .HandDrawBonusForTurn(result, turn)));
        Assert.False(PrototypeNativeOrobasRelics.IsTouchOfOrobasEligible(
            result with { Relics = result.Relics.Where(relic =>
                relic.RelicId != PrototypeNativeOrobasRelics.RingOfTheDrakeId)
                .ToArray() }));
    }

    [Fact]
    public void RingOfTheDrakeDrawsSevenCardsForOnlyTheFirstThreeTurns()
    {
        var engine = new PrototypeGameEngine();
        var state = engine.Step(PrototypeGameFactory.Create(
            "orobas-ring-of-the-drake"), GameAction.Empty("start_run")).State;
        var world = state.World!;
        var entry = Assert.IsType<string>(world.Map.EntryNodeIds![0]);
        state = state with
        {
            Player = state.Player with
            {
                Relics = state.Player.Relics.Where(relic =>
                    relic.RelicId != PrototypeNativeOrobasRelics.RingOfTheSnakeId)
                    .Append(new RelicInstance(
                        PrototypeNativeOrobasRelics.RingOfTheDrakeId,
                        PrototypeJson.EmptyObject())).ToArray()
            },
            World = world with
            {
                Map = world.Map with
                {
                    Nodes = world.Map.Nodes.Select(node =>
                        node.NodeId == entry
                            ? node with { RoomType = PrototypeRoomType.Combat }
                            : node).ToArray()
                }
            }
        };
        state = engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload(entry))).State;

        Assert.Equal(7, state.World!.Combat!.Hand.Length);
        for (var turn = 2; turn <= 4; turn++)
        {
            state = engine.Step(state, GameAction.Empty("end_turn")).State;
            var combat = state.World!.Combat!;
            Assert.Equal(turn, combat.Turn);
            Assert.Equal(turn <= 3 ? 7 : 5, combat.Hand.Length);
        }
    }

    [Fact]
    public void ArchaicToothTransformsTheFirstNeutralizeAndPreservesItsInstance()
    {
        var player = PrototypeNativeUnderdocksRunFactory.Create(
            "orobas-archaic-tooth").Player with
        {
            Deck =
            [
                new CardInstance(7101,
                    PrototypeNativeOrobasRelics.NeutralizeId, 1,
                    System.Text.Json.JsonSerializer.SerializeToElement(
                        new { Marker = "keep" }),
                    new PrototypeCardEnchantment(
                        PrototypeCardEnchantmentKind.SoulsPower, 2)),
                new CardInstance(7102,
                    PrototypeNativeOrobasRelics.NeutralizeId, 0,
                    PrototypeJson.EmptyObject())
            ],
            Relics = [new RelicInstance(
                PrototypeNativeOrobasRelics.ArchaicToothId,
                PrototypeJson.EmptyObject())]
        };
        Assert.True(PrototypeNativeOrobasRelics
            .IsArchaicToothEligible(player));
        var originalDeck = CanonicalJson.Sha256(player.Deck);
        var rng = PrototypeRng.CreateBundle("orobas-archaic-tooth");
        var originalRng = CanonicalJson.Sha256(rng);

        var result = Dispatch(player, rng,
            PrototypeNativeOrobasRelics.ArchaicToothId);

        var transformed = Assert.Single(result.Deck, card =>
            card.CardId == PrototypeNativeOrobasRelics.SuppressId);
        Assert.Equal(7101, transformed.InstanceId);
        Assert.Equal(1, transformed.UpgradeLevel);
        Assert.Equal(player.Deck[0].PersistentState,
            transformed.PersistentState);
        Assert.Equal(player.Deck[0].Enchantment, transformed.Enchantment);
        Assert.Equal(PrototypeNativeOrobasRelics.NeutralizeId,
            result.Deck[1].CardId);
        var tooth = Assert.Single(result.Relics, relic =>
            relic.RelicId == PrototypeNativeOrobasRelics.ArchaicToothId);
        Assert.Equal(7101, tooth.PersistentState
            .GetProperty("CardInstanceId").GetInt64());
        Assert.Equal(originalDeck, CanonicalJson.Sha256(player.Deck));
        Assert.Equal(originalRng, CanonicalJson.Sha256(rng));
        Assert.False(PrototypeNativeOrobasRelics.IsArchaicToothEligible(
            player with { Deck = [new CardInstance(7103,
                "proto.silent.strike", 0, PrototypeJson.EmptyObject())] }));
    }

    [Fact]
    public void RadiantPearlAddsRetainedLuminesceWhichGrantsEnergyAndExhausts()
    {
        var engine = new PrototypeGameEngine();
        var state = engine.Step(PrototypeGameFactory.Create(
            "orobas-radiant-pearl"), GameAction.Empty("start_run")).State;
        var world = state.World!;
        var entry = world.Map.EntryNodeIds![0];
        state = state with
        {
            Player = state.Player with
            {
                Relics = state.Player.Relics.Append(new RelicInstance(
                    PrototypeNativeOrobasRelics.RadiantPearlId,
                    PrototypeJson.EmptyObject())).ToArray()
            },
            World = world with
            {
                Map = world.Map with
                {
                    Nodes = world.Map.Nodes.Select(node =>
                        node.NodeId == entry
                            ? node with { RoomType = PrototypeRoomType.Combat }
                            : node).ToArray()
                }
            }
        };
        state = engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload(entry))).State;

        var combat = state.World!.Combat!;
        var luminesce = Assert.Single(combat.Cards, card =>
            card.CardId == PrototypeNativeOrobasRelics.LuminesceId);
        Assert.True(luminesce.IsTemporary);
        Assert.Null(luminesce.PersistentCardInstanceId);
        Assert.Contains(luminesce.InstanceId, combat.Hand);
        Assert.True(PrototypeContent.Card(luminesce.CardId).Retain);
        Assert.True(PrototypeContent.Card(luminesce.CardId).ExhaustOnUse);
        var energy = Assert.Single(PrototypeContent.Card(luminesce.CardId)
            .Effects);
        Assert.Equal((2, 1), (energy.Amount, energy.UpgradeDelta));
        Assert.Equal(3, energy.AmountAt(1, 0));
        Assert.DoesNotContain(state.Player.Deck, card =>
            card.CardId == PrototypeNativeOrobasRelics.LuminesceId);
        var snapshotHash = CanonicalJson.Sha256(state);
        state = RunSnapshot.Load(RunSnapshot.Save(state));
        Assert.Equal(snapshotHash, CanonicalJson.Sha256(state));

        state = engine.Step(state, GameAction.Empty("end_turn")).State;
        combat = state.World!.Combat!;
        Assert.Equal(2, combat.Turn);
        Assert.Contains(luminesce.InstanceId, combat.Hand);
        var play = engine.GetLegalActions(state).Single(action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId
                == luminesce.InstanceId);
        state = engine.Step(state, play).State;

        combat = state.World!.Combat!;
        Assert.Equal(5, combat.Energy);
        Assert.Contains(luminesce.InstanceId, combat.ExhaustPile);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void PrismaticGemAddsOneEnergyAtCombatStart()
    {
        var engine = new PrototypeGameEngine();
        var state = engine.Step(PrototypeGameFactory.Create(
            "orobas-prismatic-gem-energy"),
            GameAction.Empty("start_run")).State;
        var world = state.World!;
        var entry = world.Map.EntryNodeIds![0];
        state = state with
        {
            Player = state.Player with
            {
                Relics = state.Player.Relics.Append(new RelicInstance(
                    PrototypeNativeOrobasRelics.PrismaticGemId,
                    PrototypeJson.EmptyObject())).ToArray()
            },
            World = world with
            {
                Map = world.Map with
                {
                    Nodes = world.Map.Nodes.Select(node =>
                        node.NodeId == entry
                            ? node with { RoomType = PrototypeRoomType.Combat }
                            : node).ToArray()
                }
            }
        };

        state = engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload(entry))).State;

        Assert.Equal(4, state.World!.Combat!.Energy);
        Assert.Equal(1, PrototypeContent.Relic(
            PrototypeNativeOrobasRelics.PrismaticGemId).EnergyPerTurnBonus);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void AlchemicalCofferAddsFourSlotsAndFillsOnlyTheNewSlots()
    {
        var original = PrototypeNativeUnderdocksRunFactory.Create(
            "orobas-alchemical-coffer").Player;
        var firstPotion = new PotionInstance("proto.potion.block",
            PrototypeJson.EmptyObject());
        var player = original with
        {
            PotionSlots = [firstPotion, null, null],
            Relics = [new RelicInstance(
                PrototypeNativeOrobasRelics.AlchemicalCofferId,
                PrototypeJson.EmptyObject())]
        };
        var rng = PrototypeRng.CreateBundle("orobas-alchemical-coffer");
        var originalSlotsHash = CanonicalJson.Sha256(player.PotionSlots);
        var result = Dispatch(player, rng,
            PrototypeNativeOrobasRelics.AlchemicalCofferId);

        Assert.Equal(7, result.PotionSlots.Length);
        Assert.Equal(firstPotion, result.PotionSlots[0]);
        Assert.Null(result.PotionSlots[1]);
        Assert.Null(result.PotionSlots[2]);
        var gained = result.PotionSlots.Skip(3).ToArray();
        Assert.All(gained, potion => Assert.NotNull(potion));
        Assert.Equal(4, gained.Select(potion => potion!.PotionId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal((ulong?)8, rng.Streams.Single(stream =>
            stream.StreamId == "event").CallCount);
        Assert.Equal(originalSlotsHash,
            CanonicalJson.Sha256(player.PotionSlots));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(Dispatch(player,
                PrototypeRng.CreateBundle("orobas-alchemical-coffer"),
                PrototypeNativeOrobasRelics.AlchemicalCofferId)));
    }

    [Fact]
    public void AlchemicalCofferStillAddsEmptySlotsWhenPotionGainIsBlocked()
    {
        var player = PrototypeNativeUnderdocksRunFactory.Create(
            "orobas-alchemical-coffer-sozu").Player with
        {
            PotionSlots = [new PotionInstance("proto.potion.block",
                PrototypeJson.EmptyObject()), null],
            Relics =
            [
                new RelicInstance(PrototypeNativeOrobasRelics
                    .AlchemicalCofferId, PrototypeJson.EmptyObject()),
                new RelicInstance("proto.relic.sozu",
                    PrototypeJson.EmptyObject())
            ]
        };
        var rng = PrototypeRng.CreateBundle("orobas-alchemical-coffer-sozu");
        var result = Dispatch(player, rng,
            PrototypeNativeOrobasRelics.AlchemicalCofferId);

        Assert.Equal(6, result.PotionSlots.Length);
        Assert.Equal(player.PotionSlots[0], result.PotionSlots[0]);
        Assert.All(result.PotionSlots.Skip(1), potion =>
            Assert.Null(potion));
        Assert.Equal((ulong?)8, rng.Streams.Single(stream =>
            stream.StreamId == "event").CallCount);
    }

    [Fact]
    public void AlchemicalCofferUsesNativeCombatPotionGenerationStream()
    {
        var original = PrototypeNativeUnderdocksRunFactory.Create(
            "orobas-alchemical-coffer-native").Player;
        var player = original with
        {
            PotionSlots = [null],
            Relics = [new RelicInstance(
                PrototypeNativeOrobasRelics.AlchemicalCofferId,
                PrototypeJson.EmptyObject())]
        };
        var nativeRng = NativeRngV111.CreateBundle(
            "orobas-alchemical-coffer-native");
        var expectedRng = nativeRng.Fork();
        var expected = PrototypeNativePotionShop.PickWeightedDistinctNative(
            expectedRng, "run.combat_potion_generation", 4);
        var legacyRng = PrototypeRng.CreateBundle(
            "orobas-alchemical-coffer-legacy-owner");

        var result = Dispatch(player, legacyRng,
            PrototypeNativeOrobasRelics.AlchemicalCofferId, nativeRng);

        Assert.Equal(expected, result.PotionSlots.Skip(1)
            .Select(potion => potion!.PotionId));
        Assert.Equal(NativeRngV111.ToSnapshot(expectedRng.Streams.Single(
                stream => stream.StreamId
                    == "run.combat_potion_generation")),
            NativeRngV111.ToSnapshot(nativeRng.Streams.Single(
                stream => stream.StreamId
                    == "run.combat_potion_generation")));
        Assert.Equal((ulong?)0, legacyRng.Streams.Single(stream =>
            stream.StreamId == "event").CallCount);
    }

    [Fact]
    public void SozuStillConsumesTheNativeCofferPotionGenerationDraws()
    {
        var original = PrototypeNativeUnderdocksRunFactory.Create(
            "orobas-alchemical-coffer-native-sozu").Player;
        var player = original with
        {
            PotionSlots = [null],
            Relics =
            [
                new RelicInstance(
                    PrototypeNativeOrobasRelics.AlchemicalCofferId,
                    PrototypeJson.EmptyObject()),
                new RelicInstance("proto.relic.sozu",
                    PrototypeJson.EmptyObject())
            ]
        };
        var nativeRng = NativeRngV111.CreateBundle(
            "orobas-alchemical-coffer-native-sozu");
        var legacyRng = PrototypeRng.CreateBundle(
            "orobas-alchemical-coffer-native-sozu-legacy");

        var result = Dispatch(player, legacyRng,
            PrototypeNativeOrobasRelics.AlchemicalCofferId, nativeRng);

        Assert.Equal(5, result.PotionSlots.Length);
        Assert.All(result.PotionSlots, potion => Assert.Null(potion));
        Assert.Equal((ulong?)8, nativeRng.Streams.Single(stream =>
            stream.StreamId == "run.combat_potion_generation").CallCount);
        Assert.Equal((ulong?)0, legacyRng.Streams.Single(stream =>
            stream.StreamId == "event").CallCount);
    }

    [Fact]
    public void ElectricShrympOffersOnlySkillsAndImbuedPlaysAtCombatOpening()
    {
        var engine = new PrototypeGameEngine();
        var state = engine.Step(PrototypeGameFactory.Create(
            "orobas-electric-shrymp"), GameAction.Empty("start_run")).State;
        var world = state.World!;
        state = state with
        {
            Phase = RunPhase.Reward,
            World = world with
            {
                ActiveRoom = null,
                Reward = new RewardState("Combat", [], null,
                    PrototypeNativeOrobasRelics.ElectricShrympId,
                    true, true, false, false)
            }
        };
        var acquisition = engine.GetLegalActions(state).Single(action =>
            action.Kind == "take_reward_relic");
        state = engine.Step(state, acquisition).State;
        PrototypeStateInvariants.Validate(state);

        var choice = Assert.IsType<PrototypePendingDeckChoiceState>(
            state.World!.Reward!.PendingDeckChoice);
        Assert.Equal(PrototypeCardEnchantmentKind.Imbued,
            PrototypeContent.Relic(PrototypeNativeOrobasRelics.ElectricShrympId)
                .AcquisitionDeckChoice!.EnchantmentKind);
        Assert.All(choice.CandidateCardInstanceIds, instanceId =>
        {
            var card = state.Player.Deck.Single(card =>
                card.InstanceId == instanceId);
            Assert.Equal(PrototypeCardType.Skill,
                PrototypeContent.Card(card.CardId).Type);
        });
        var snapshot = RunSnapshot.Save(state);
        var stateHash = CanonicalJson.Sha256(state);
        var targetDefend = state.Player.Deck.First(card =>
            card.CardId == "proto.silent.defend"
            && choice.CandidateCardInstanceIds.Contains(card.InstanceId));
        var selected = engine.Step(state, engine.GetLegalActions(state)
            .Single(action => action.Kind == "choose_relic_deck_card"
                && action.ReadPayload<ChooseDeckCardPayload>().CardInstanceId
                    == targetDefend.InstanceId)).State;
        Assert.Equal(stateHash, CanonicalJson.Sha256(
            RunSnapshot.Load(snapshot)));
        var imbued = Assert.Single(selected.Player.Deck, card =>
            card.InstanceId == targetDefend.InstanceId);
        Assert.Equal(PrototypeCardEnchantmentKind.Imbued,
            imbued.Enchantment!.Kind);
        Assert.Equal(targetDefend.InstanceId, imbued.InstanceId);
        Assert.Null(selected.World!.Reward!.PendingDeckChoice);
        PrototypeStateInvariants.Validate(selected);

        var entry = world.Map.EntryNodeIds![0];
        selected = selected with
        {
            Phase = RunPhase.MapChoice,
            World = selected.World with
            {
                Reward = null,
                Map = selected.World.Map with
                {
                    Nodes = selected.World.Map.Nodes.Select(node =>
                        node.NodeId == entry
                            ? node with { RoomType = PrototypeRoomType.Combat }
                            : node).ToArray()
                }
            }
        };
        selected = engine.Step(selected, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload(entry))).State;

        var combat = selected.World!.Combat!;
        Assert.Equal(5, combat.PlayerBlock);
        Assert.Contains(combat.Cards, card =>
            card.PersistentCardInstanceId == imbued.InstanceId
            && card.EnchantmentTriggeredThisCombat);
        var combatCard = Assert.Single(combat.Cards, card =>
            card.PersistentCardInstanceId == imbued.InstanceId);
        Assert.Contains(combatCard.InstanceId, combat.DiscardPile);
        PrototypeStateInvariants.Validate(selected);
    }

    [Fact]
    public void DriftwoodRerollsEachGeneratedCardGroupAtMostOnce()
    {
        var engine = new PrototypeGameEngine();
        var state = engine.Step(PrototypeGameFactory.Create(
            "orobas-driftwood-reroll"), GameAction.Empty("start_run")).State;
        var world = state.World!;
        var commons = PrototypeContent.RewardCardPool
            .Where(id => PrototypeContent.Card(id).Rarity
                == PrototypeCardRarity.Common).Take(3).ToArray();
        var uncommons = PrototypeContent.RewardCardPool
            .Where(id => PrototypeContent.Card(id).Rarity
                == PrototypeCardRarity.Uncommon).Take(3).ToArray();
        var reward = new RewardState("Combat", commons, null, null,
            false, true, true, false,
            ExtraCardOptions: [uncommons],
            IndependentSelection: true,
            ExtraCardGroupsResolved: [false],
            ExtraRelicGroupsResolved: [],
            CardRewardGenerators:
            [
                new PrototypeCardRewardGenerator(
                    PrototypeCardRewardGeneratorKind.RewardPool, 1),
                new PrototypeCardRewardGenerator(
                    PrototypeCardRewardGeneratorKind.RewardPool, 1)
            ]);
        state = state with
        {
            Rng = PrototypeRng.CreateBundle("orobas-driftwood-reroll"),
            Phase = RunPhase.Reward,
            Player = state.Player with
            {
                Relics = state.Player.Relics.Append(new RelicInstance(
                    PrototypeNativeOrobasRelics.DriftwoodId,
                    PrototypeJson.EmptyObject())).ToArray()
            },
            World = world with { Reward = reward }
        };
        PrototypeStateInvariants.Validate(state);
        var originalHash = CanonicalJson.Sha256(state);
        var frame = new PrototypeAiEnvironment().Observe(state);
        Assert.Equal(new[] { 0, 1 }, frame.Observation.Reward!
            .AvailableCardRerolls!.Select(reroll => reroll.GroupIndex));

        var rerollExtra = engine.GetLegalActions(state).Single(action =>
            action.Kind == "reroll_reward_card"
            && action.ReadPayload<RerollRewardCardPayload>().GroupIndex == 1);
        var rerolled = engine.Step(state, rerollExtra).State;
        Assert.Equal(originalHash, CanonicalJson.Sha256(state));
        Assert.Equal(commons, rerolled.World!.Reward!.CardOptions);
        Assert.NotEqual(uncommons,
            rerolled.World.Reward.ExtraCardOptions![0]);
        Assert.True(rerolled.World.Reward.CardRewardGenerators![1]!
            .Rerolled);
        Assert.Contains(engine.GetLegalActions(rerolled), action =>
            action.Kind == "reroll_reward_card"
            && action.ReadPayload<RerollRewardCardPayload>().GroupIndex == 0);
        Assert.DoesNotContain(engine.GetLegalActions(rerolled), action =>
            action.Kind == "reroll_reward_card"
            && action.ReadPayload<RerollRewardCardPayload>().GroupIndex == 1);

        var rerollPrimary = engine.GetLegalActions(rerolled).Single(action =>
            action.Kind == "reroll_reward_card"
            && action.ReadPayload<RerollRewardCardPayload>().GroupIndex == 0);
        rerolled = engine.Step(rerolled, rerollPrimary).State;
        Assert.NotEqual(commons, rerolled.World!.Reward!.CardOptions);
        Assert.All(rerolled.World.Reward.CardRewardGenerators!, generator =>
            Assert.True(generator!.Rerolled));
        Assert.DoesNotContain(engine.GetLegalActions(rerolled), action =>
            action.Kind == "reroll_reward_card");
        PrototypeStateInvariants.Validate(rerolled);

        var replay = engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)),
            rerollExtra).State;
        Assert.Equal(CanonicalJson.Sha256(
                engine.Step(state, rerollExtra).State),
            CanonicalJson.Sha256(replay));
    }

    [Fact]
    public void DriftwoodEncounterRerollAdvancesCardRarityState()
    {
        var engine = new PrototypeGameEngine();
        var state = engine.Step(PrototypeGameFactory.Create(
            "orobas-driftwood-encounter"), GameAction.Empty("start_run")).State;
        var world = state.World!;
        var options = PrototypeContent.RewardCardPool.Take(3).ToArray();
        state = state with
        {
            Rng = PrototypeRng.CreateBundle("orobas-driftwood-encounter"),
            Phase = RunPhase.Reward,
            Player = state.Player with
            {
                Relics = state.Player.Relics.Append(new RelicInstance(
                    PrototypeNativeOrobasRelics.DriftwoodId,
                    PrototypeJson.EmptyObject())).ToArray()
            },
            World = world with
            {
                CardRarityOffsetBasisPoints = 100,
                Reward = new RewardState("Combat", options, null, null,
                    false, true, true, false,
                    CardOptionUpgradeFlags: new bool[options.Length],
                    CardRewardGenerators:
                    [new PrototypeCardRewardGenerator(
                        PrototypeCardRewardGeneratorKind.Encounter,
                        1, PrototypeRoomType.Combat)])
            }
        };
        PrototypeStateInvariants.Validate(state);

        var reroll = engine.GetLegalActions(state).Single(action =>
            action.Kind == "reroll_reward_card");
        var next = engine.Step(state, reroll).State;

        Assert.NotEqual(options, next.World!.Reward!.CardOptions);
        Assert.NotEqual(100, next.World.CardRarityOffsetBasisPoints);
        Assert.Equal(options.Length,
            next.World.Reward.CardOptionUpgradeFlags!.Length);
        Assert.True(next.World.Reward.CardRewardGenerators![0]!.Rerolled);
        PrototypeStateInvariants.Validate(next);
    }

    [Fact]
    public void GlassEyeOffersFiveIndependentRarityGroupsAndReturnsToParentReward()
    {
        var engine = new PrototypeGameEngine();
        var state = engine.Step(PrototypeGameFactory.Create(
            "orobas-glass-eye-reward"), GameAction.Empty("start_run")).State;
        var world = state.World!;
        state = state with
        {
            Phase = RunPhase.Reward,
            Player = state.Player with
            {
                Relics = state.Player.Relics.Append(new RelicInstance(
                    PrototypeNativeOrobasRelics.DriftwoodId,
                    PrototypeJson.EmptyObject())).ToArray()
            },
            World = world with
            {
                Reward = new RewardState("Combat", [], null,
                    PrototypeNativeOrobasRelics.GlassEyeId,
                    true, true, false, false)
            }
        };
        PrototypeStateInvariants.Validate(state);

        var acquire = engine.GetLegalActions(state).Single(action =>
            action.Kind == "take_reward_relic");
        var offered = engine.Step(state, acquire).State;
        var custom = offered.World!.Reward!;
        Assert.Equal("Relic:GlassEye", custom.SourceRoom);
        Assert.True(custom.IndependentSelection);
        Assert.Equal(4, custom.ExtraCardOptions!.Length);
        Assert.Equal(5, custom.CardRewardGenerators!.Length);
        var expectedRarities = new[]
        {
            PrototypeCardRarity.Common, PrototypeCardRarity.Common,
            PrototypeCardRarity.Uncommon, PrototypeCardRarity.Uncommon,
            PrototypeCardRarity.Rare
        };
        for (var group = 0; group < expectedRarities.Length; group++)
        {
            var cards = custom.CardOptionsForGroup(group);
            Assert.Equal(3, cards.Length);
            Assert.Equal(3, cards.Distinct().Count());
            Assert.All(cards, id => Assert.Equal(expectedRarities[group],
                PrototypeContent.Card(id).Rarity));
            Assert.Equal(expectedRarities[group],
                custom.CardRewardGenerators[group]!.Rarity);
        }
        Assert.Equal(Enumerable.Range(0, 5),
            new PrototypeAiEnvironment().Observe(offered).Observation
                .Reward!.AvailableCardRerolls!.Select(reroll =>
                    reroll.GroupIndex));
        Assert.Equal(RunPhase.Reward,
            offered.World.CustomRewardReturn!.Phase);
        Assert.True(offered.World.CustomRewardReturn.Reward!.RelicResolved);
        PrototypeStateInvariants.Validate(offered);

        var rerollRare = engine.GetLegalActions(offered).Single(action =>
            action.Kind == "reroll_reward_card"
            && action.ReadPayload<RerollRewardCardPayload>().GroupIndex == 4);
        var rerolled = engine.Step(offered, rerollRare).State;
        var rareOptions = rerolled.World!.Reward!.CardOptionsForGroup(4);
        Assert.NotEqual(custom.CardOptionsForGroup(4), rareOptions);
        Assert.All(rareOptions, id => Assert.Equal(PrototypeCardRarity.Rare,
            PrototypeContent.Card(id).Rarity));
        Assert.True(rerolled.World.Reward.CardRewardGenerators![4]!.Rerolled);
        Assert.Contains(engine.GetLegalActions(rerolled), action =>
            action.Kind == "reroll_reward_card"
            && action.ReadPayload<RerollRewardCardPayload>().GroupIndex == 3);

        var pickRare = engine.GetLegalActions(rerolled).Single(action =>
            action.Kind == "take_reward_card_group"
            && action.ReadPayload<ChooseRewardCardGroupPayload>().GroupIndex == 4
            && action.ReadPayload<ChooseRewardCardGroupPayload>().Index == 0);
        var picked = engine.Step(RunSnapshot.Load(RunSnapshot.Save(rerolled)),
            pickRare).State;
        Assert.Contains(picked.Player.Deck, card =>
            card.CardId == rareOptions[0]);
        Assert.True(picked.World!.Reward!.ExtraCardGroupsResolved![3]);
        Assert.Equal(1, picked.World.Reward.ExtraCardRewardsResolved);
        Assert.True(picked.World.Reward.CardRewardGenerators![4]!.Rerolled);

        var restored = engine.Step(picked,
            GameAction.Empty("leave_reward")).State;
        Assert.Null(restored.World!.CustomRewardReturn);
        Assert.Equal(RunPhase.Reward, restored.Phase);
        Assert.Equal(PrototypeNativeOrobasRelics.GlassEyeId,
            restored.World.Reward!.RelicOption);
        Assert.True(restored.World.Reward.RelicResolved);
        Assert.Contains(restored.Player.Deck, card =>
            card.CardId == rareOptions[0]);
        PrototypeStateInvariants.Validate(restored);
    }

    private static PlayerState PlayerWithSandCastle(int eligibleCount)
    {
        var source = PrototypeNativeUnderdocksRunFactory.Create(
            "orobas-sand-castle-player").Player;
        var deck = Enumerable.Range(0, eligibleCount)
            .Select(index => new CardInstance(
                1000 + index, "proto.silent.strike", 0,
                PrototypeJson.EmptyObject()))
            .Append(new CardInstance(2000, "proto.silent.strike", 1,
                PrototypeJson.EmptyObject()))
            .Append(new CardInstance(2001,
                PrototypeNativeHiveEvents.NormalityId, 0,
                PrototypeJson.EmptyObject()))
            .ToArray();
        return source with
        {
            Deck = deck,
            Relics = [new RelicInstance(SandCastle,
                PrototypeJson.EmptyObject())]
        };
    }

    private static PlayerState Dispatch(PlayerState player, RngBundle rng,
        string acquired = SandCastle, RngBundle? nativeRunRng = null)
    {
        var method = typeof(PrototypeGameEngine).GetMethod(
            "ApplyRelicRunEvent", BindingFlags.NonPublic |
                BindingFlags.Static);
        Assert.NotNull(method);
        return (PlayerState)method!.Invoke(null,
            [player, PrototypeRunEventKind.RelicAcquired, null, acquired,
                rng, null, null, nativeRunRng])!;
    }
}
