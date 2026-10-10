using System.Reflection;
using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111EventObservabilityAndRelicInstanceTests
{
    [Theory]
    [InlineData(52, 303)]
    [InlineData(67, 363)]
    public void TreasuryRevealsBothRolledAmountsAndItsGuaranteedCurse(
        int small, int large)
    {
        var state = AtEvent("proto.native.underdocks.sunken_treasury",
            gold: small, secondary: large);
        var originalStateHash = CanonicalJson.Sha256(state);
        var originalRngHash = CanonicalJson.Sha256(state.Rng);
        var frame = new PrototypeAiEnvironment().Observe(state);
        var entries = frame.Observation.Event!.VisibleChoiceValues!;
        Assert.Equal(small, Amount(entries, "first_chest").GoldGain);
        Assert.Equal(large, Amount(entries, "second_chest").GoldGain);
        Assert.Equal("proto.native.underdocks.greed",
            Amount(entries, "second_chest").GuaranteedCardId);
        Assert.Null(Amount(entries, "first_chest").GuaranteedCardId);
        Assert.Equal(originalStateHash, CanonicalJson.Sha256(state));
        Assert.Equal(originalRngHash, CanonicalJson.Sha256(state.Rng));
        Assert.Equal(frame.Observation.Event.VisibleChoiceValues,
            new PrototypeAiEnvironment().Observe(state.Fork())
                .Observation.Event!.VisibleChoiceValues);
    }

    [Fact]
    public void StatueBeaconAndScriptoriumRevealFixedCostsAndItems()
    {
        var ai = new PrototypeAiEnvironment();
        var statue = ai.Observe(
            AtEvent("proto.native.underdocks.sunken_statue", gold: 117))
            .Observation.Event!.VisibleChoiceValues!;
        Assert.Equal(117, Amount(statue, "dive").GoldGain);
        Assert.Equal(7, Amount(statue, "dive").HpLoss);
        Assert.Equal("proto.native.event.sword_of_stone",
            Amount(statue, "sword").GuaranteedRelicId);

        var beacon = ai.Observe(
            AtEvent("proto.native.underdocks.drowning_beacon"))
            .Observation.Event!.VisibleChoiceValues!;
        Assert.Equal(-13, Amount(beacon, "climb").MaxHpDelta);
        Assert.Equal("proto.native.underdocks.fresnel_lens",
            Amount(beacon, "climb").GuaranteedRelicId);
        Assert.Equal("proto.native.underdocks.glowwater_potion",
            Amount(beacon, "bottle").GuaranteedPotionId);

        var script = ai.Observe(
            AtEvent("proto.native.underdocks.waterlogged_scriptorium"))
            .Observation.Event!.VisibleChoiceValues!;
        Assert.Equal(55, Amount(script, "tentacle_quill").GoldCost);
        Assert.Equal(99, Amount(script, "prickly_sponge").GoldCost);
        Assert.Equal(6, Amount(script, "bloody_ink").MaxHpDelta);
    }

    [Fact]
    public void BathsRevealsEscalatingDamageAndWhirlpoolCurrentHeal()
    {
        var ai = new PrototypeAiEnvironment();
        var baths = AtEvent("proto.native.underdocks.abyssal_baths");
        var first = ai.Observe(baths).Observation.Event!.VisibleChoiceValues!;
        Assert.Equal(3, Amount(first, "immerse").HpLoss);
        Assert.Equal(2, Amount(first, "immerse").MaxHpDelta);
        Assert.Equal(10, Amount(first, "abstain").Heal);
        baths = baths with
        {
            World = baths.World! with
            {
                Event = baths.World.Event! with { NativePageIndex = 5 }
            }
        };
        var later = ai.Observe(baths).Observation.Event!.VisibleChoiceValues!;
        Assert.Equal(8, Amount(later, "linger").HpLoss);
        Assert.Equal(2, Amount(later, "linger").MaxHpDelta);
        Assert.DoesNotContain(later, option => option.ChoiceId == "immerse");

        var whirlpool = AtEvent("proto.native.underdocks.spiraling_whirlpool");
        whirlpool = whirlpool with
        {
            Player = whirlpool.Player with { MaxHp = 99, Hp = 50 }
        };
        var drink = ai.Observe(whirlpool)
            .Observation.Event!.VisibleChoiceValues!;
        Assert.Equal(32, Amount(drink, "drink").Heal);
    }

    [Fact]
    public void ConveyorRevealsCurrentDishButKeepsFutureRollAndSuspendedChoicesHidden()
    {
        var ai = new PrototypeAiEnvironment();
        var state = AtEvent(PrototypeNativeEndlessConveyor.EventId);
        state = state with
        {
            World = state.World! with
            {
                Event = state.World.Event! with
                {
                    NativeDishId = "SEAPUNK_SALAD",
                    NativeDishCount = 5,
                    NativeLastDishId = "SEAPUNK_SALAD"
                }
            }
        };
        var original = CanonicalJson.Sha256(state.Rng);
        var observation = ai.Observe(state).Observation.Event!;
        Assert.Equal("SEAPUNK_SALAD", observation.CurrentDishId);
        Assert.Equal(5, observation.CurrentDishNumber);
        Assert.Equal(40,
            Amount(observation.VisibleChoiceValues!, "grab").GoldCost);
        Assert.Equal(PrototypeNativeEndlessConveyor.FeedingFrenzyId,
            Amount(observation.VisibleChoiceValues!, "grab")
                .GuaranteedCardId);
        var json = JsonSerializer.Serialize(observation);
        Assert.DoesNotContain("NativeEventGold", json);
        Assert.DoesNotContain("NativeDishRollPending", json);
        Assert.Equal(original, CanonicalJson.Sha256(state.Rng));

        state = state with
        {
            World = state.World! with
            {
                Event = state.World.Event! with
                {
                    PendingDeckChoice = new PrototypePendingEventDeckChoiceState(
                        "grab", PrototypePersistentDeckChoiceKind.Transform,
                        1, [state.Player.Deck[0].InstanceId])
                }
            }
        };
        Assert.Null(ai.Observe(state).Observation.Event!.VisibleChoiceValues);

        state = state with
        {
            World = state.World! with
            {
                Event = state.World.Event! with
                {
                    PendingDeckChoice = null,
                    NativeDishId = "GOLDEN_FYSH",
                    NativeDishCount = 6
                }
            }
        };
        var fysh = ai.Observe(state).Observation.Event!;
        Assert.Null(Amount(fysh.VisibleChoiceValues!, "grab").GoldCost);
        Assert.Equal(75, Amount(fysh.VisibleChoiceValues!, "grab").GoldGain);
    }

    [Fact]
    public void PunchOffKeepsItsUnusedRandomEntryRollPrivate()
    {
        var state = AtEvent(PrototypeNativePunchOff.EventId, gold: 96);
        var observation = new PrototypeAiEnvironment()
            .Observe(state).Observation.Event!;
        Assert.Equal("proto.native.neow.injury",
            Amount(observation.VisibleChoiceValues!, "nab").GuaranteedCardId);
        var json = JsonSerializer.Serialize(observation);
        Assert.DoesNotContain("NativeEventGold", json);
        Assert.DoesNotContain("96", json);
    }

    [Fact]
    public void DuplicateSwordsCountAndTransformIndependentlyAtEliteVictory()
    {
        const string stone = "proto.native.event.sword_of_stone";
        const string jade = "proto.native.event.sword_of_jade";
        var player = PrototypeNativeUnderdocksRunFactory.Create(
            "two-swords").Player with
        {
            Relics =
            [
                new(stone, JsonSerializer.SerializeToElement(
                    new { ElitesDefeated = 4 })),
                new(stone, JsonSerializer.SerializeToElement(
                    new { ElitesDefeated = 1 })),
                new(jade, PrototypeJson.EmptyObject())
            ]
        };
        var untouched = ApplyPersistent(player, PrototypeRoomType.Combat,
            PrototypeRng.CreateBundle("two-swords-normal"));
        Assert.Equal(CanonicalJson.Sha256(player),
            CanonicalJson.Sha256(untouched));

        var changed = ApplyPersistent(player, PrototypeRoomType.Elite,
            PrototypeRng.CreateBundle("two-swords-elite"));
        Assert.Equal(jade, changed.Relics[0].RelicId);
        Assert.Equal(stone, changed.Relics[1].RelicId);
        Assert.Equal(2, changed.Relics[1].PersistentState
            .GetProperty("ElitesDefeated").GetInt32());
        Assert.Equal(jade, changed.Relics[2].RelicId);
        Assert.Equal(4, player.Relics[0].PersistentState
            .GetProperty("ElitesDefeated").GetInt32());
    }

    [Fact]
    public void DuplicateFishingRodsKeepSeparateCountersAndUpgradeTwice()
    {
        var rod = PrototypeNativeOvergrowthEvents.NeowRelicId("FishingRod");
        var player = PrototypeNativeUnderdocksRunFactory.Create(
            "two-fishing-rods").Player with
        {
            Relics =
            [
                new(rod, JsonSerializer.SerializeToElement(
                    new { CombatsSeen = 2 })),
                new(rod, JsonSerializer.SerializeToElement(
                    new { CombatsSeen = 2 }))
            ]
        };
        var original = CanonicalJson.Sha256(player);
        var rng = PrototypeRng.CreateBundle("two-rods");
        var changed = ApplyPersistent(player, PrototypeRoomType.Combat, rng);
        Assert.Equal(2, changed.Deck.Count(card => card.UpgradeLevel == 1));
        Assert.All(changed.Relics, relic =>
            Assert.Equal(3, relic.PersistentState.GetProperty("CombatsSeen")
                .GetInt32()));
        Assert.Equal(original, CanonicalJson.Sha256(player));

        var other = ApplyPersistent(player, PrototypeRoomType.Elite,
            PrototypeRng.CreateBundle("two-rods-elite"));
        Assert.Equal(original, CanonicalJson.Sha256(other));
        var replay = ApplyPersistent(player, PrototypeRoomType.Combat,
            PrototypeRng.CreateBundle("two-rods"));
        Assert.Equal(CanonicalJson.Sha256(changed),
            CanonicalJson.Sha256(replay));
    }

    [Fact]
    public void FishingRodStillAdvancesWhenAllCardsAlreadyUpgraded()
    {
        var rod = PrototypeNativeOvergrowthEvents.NeowRelicId("FishingRod");
        var player = PrototypeNativeUnderdocksRunFactory.Create(
            "rod-upgrade-exhausted").Player;
        player = player with
        {
            Deck = player.Deck.Select(card => card with { UpgradeLevel = 1 })
                .ToArray(),
            Relics =
            [
                new(rod, JsonSerializer.SerializeToElement(
                    new { CombatsSeen = 2 }))
            ]
        };
        var rng = PrototypeRng.CreateBundle("rod-empty-candidates");
        var before = CanonicalJson.Sha256(rng);
        var changed = ApplyPersistent(player, PrototypeRoomType.Combat, rng);
        Assert.Equal(3, changed.Relics[0].PersistentState
            .GetProperty("CombatsSeen").GetInt32());
        Assert.All(changed.Deck, card =>
            Assert.Equal(1, card.UpgradeLevel));
        Assert.Equal(before, CanonicalJson.Sha256(rng));
    }

    [Fact]
    public void EventValidatorAcceptsUnderdocksGoldRollsAndMultiPageStates()
    {
        ValidateEvent(AtEvent("proto.native.underdocks.sunken_treasury",
            gold: 62, secondary: 315));
        ValidateEvent(AtEvent("proto.native.underdocks.sunken_statue",
            gold: 117));
        ValidateEvent(AtEvent(PrototypeNativePunchOff.EventId, gold: 96));

        var baths = AtEvent("proto.native.underdocks.abyssal_baths");
        baths = baths with
        {
            World = baths.World! with
            {
                Event = baths.World.Event! with { NativePageIndex = 12 }
            }
        };
        ValidateEvent(baths);

        var conveyor = AtEvent(PrototypeNativeEndlessConveyor.EventId);
        conveyor = conveyor with
        {
            World = conveyor.World! with
            {
                Event = conveyor.World.Event! with
                {
                    NativeDishId = "CAVIAR",
                    NativeLastDishId = "CAVIAR",
                    NativeDishCount = 1
                }
            }
        };
        ValidateEvent(conveyor);
        conveyor = conveyor with
        {
            World = conveyor.World! with
            {
                Event = conveyor.World.Event! with
                {
                    NativePageIndex = 1,
                    NativeDishCount = 2,
                    NativeDishId = "JELLY_LIVER",
                    NativeLastDishId = "JELLY_LIVER",
                    ChosenChoiceId = "grab",
                    NativeDishRollPending = true,
                    PendingDeckChoice = new PrototypePendingEventDeckChoiceState(
                        "grab", PrototypePersistentDeckChoiceKind.Transform,
                        1, [conveyor.Player.Deck[0].InstanceId])
                }
            }
        };
        ValidateEvent(conveyor);
    }

    [Fact]
    public void EventValidatorRejectsOutOfRangeRollsAndOrphanedConveyorState()
    {
        var treasury = AtEvent("proto.native.underdocks.sunken_treasury",
            gold: 62, secondary: 315);
        var invalidTreasury = treasury with
        {
            World = treasury.World! with
            {
                Event = treasury.World.Event! with
                {
                    NativeEventSecondaryGold = 400
                }
            }
        };
        AssertInvalidEvent(invalidTreasury);

        var orphan = AtEvent("proto.native.underdocks.sunken_statue",
            gold: 112);
        orphan = orphan with
        {
            World = orphan.World! with
            {
                Event = orphan.World.Event! with
                {
                    NativeDishId = "CAVIAR",
                    NativeDishCount = 1
                }
            }
        };
        AssertInvalidEvent(orphan);

        var conveyor = AtEvent(PrototypeNativeEndlessConveyor.EventId);
        conveyor = conveyor with
        {
            World = conveyor.World! with
            {
                Event = conveyor.World.Event! with
                {
                    NativeDishId = "CAVIAR",
                    NativeLastDishId = "CAVIAR",
                    NativeDishCount = 1,
                    NativeDishRollPending = true
                }
            }
        };
        AssertInvalidEvent(conveyor);
    }

    private static void ValidateEvent(RunState state)
    {
        var method = typeof(PrototypeStateInvariants).GetMethod(
            "ValidateEvent", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        method!.Invoke(null, [state.Player, state.World!.Event!]);
    }

    private static void AssertInvalidEvent(RunState state)
    {
        var exception = Assert.Throws<TargetInvocationException>(
            () => ValidateEvent(state));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    private static RunState AtEvent(
        string eventId, int gold = 0, int secondary = 0)
    {
        var state = PrototypeNativeUnderdocksRunFactory.Create(
            "observable-" + eventId + gold + "-" + secondary);
        return state with
        {
            Phase = RunPhase.Event,
            Player = state.Player with { Gold = 300 },
            World = state.World! with
            {
                ActiveRoom = PrototypeRoomType.Event,
                Event = new EventState(
                    eventId,
                    NativeEventGold: gold,
                    NativeEventSecondaryGold: secondary)
            }
        };
    }

    private static PrototypeAiEventChoiceValue Amount(
        IEnumerable<PrototypeAiEventChoiceValue> values, string choice) =>
        values.Single(option => option.ChoiceId == choice);

    private static PlayerState ApplyPersistent(
        PlayerState player, PrototypeRoomType room, RngBundle rng)
    {
        var method = typeof(PrototypeGameEngine).GetMethod(
            "ApplyNativePersistentCombatEnd",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (PlayerState)method!.Invoke(null, [player, room, rng])!;
    }
}
