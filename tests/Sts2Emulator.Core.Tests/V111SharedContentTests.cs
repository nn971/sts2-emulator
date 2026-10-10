using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111SharedContentTests
{
    private const string Prefix = "proto.native.trash_heap.";

    [Theory]
    [InlineData(0, 14)]
    [InlineData(1, 18)]
    public void ClashRequiresAnAttackOnlyHandAndUsesNativeUpgrade(int upgrade, int damage)
    {
        var state = Setup([Prefix + "clash", "proto.silent.strike"], [1, 2], [], upgrade);
        var next = Play(state, 1, 1);
        Assert.Equal(100 - damage, next.World!.Combat!.Enemies[0].Hp);
        Assert.Equal(3, next.World.Combat.Energy);
        var mixed = Setup([Prefix + "clash", "proto.silent.defend"], [1, 2], [], upgrade);
        Assert.DoesNotContain(new PrototypeGameEngine().GetLegalActions(mixed), action =>
            action.Kind == "play_card" && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    public void EntrenchDoublesExistingBlockWithoutDexterityOrFrail(int upgrade, int energy)
    {
        var state = Setup([Prefix + "entrench"], [1], [], upgrade);
        var combat = state.World!.Combat! with
        {
            PlayerBlock = 7,
            PlayerPowers = [new("proto.power.dexterity", 20, 1), new("proto.power.frail", 2, 2)],
            NextPowerApplicationOrder = 3
        };
        state = state with { World = state.World with { Combat = combat } };
        var next = Play(state, 1);
        Assert.Equal(14, next.World!.Combat!.PlayerBlock);
        Assert.Equal(energy, next.World.Combat.Energy);
        Assert.Equal(7, state.World.Combat!.PlayerBlock);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 5)]
    public void StackCountsDiscardedCardsAndDoesNotCountThePlayingCard(int upgrade, int block)
    {
        var state = Setup([Prefix + "stack", "proto.silent.strike", "proto.silent.defend"], [1], [2, 3], upgrade);
        var next = Play(state, 1);
        Assert.Equal(block, next.World!.Combat!.PlayerBlock);
        Assert.Equal(3, next.World.Combat.DiscardPile.Length);
        PrototypeStateInvariants.Validate(next);
    }

    [Theory]
    [InlineData(0, 7)]
    [InlineData(1, 9)]
    public void RipAndTearRetargetsAfterAFatalFirstHit(int upgrade, int damage)
    {
        var state = Setup([Prefix + "rip_and_tear"], [1], [], upgrade);
        var enemies = state.World!.Combat!.Enemies.Select(enemy => enemy with { Hp = 1 }).ToArray();
        enemies[1] = enemies[1] with { Hp = 100 };
        // Find a deterministic first target of the fragile enemy, then prove
        // the second target draw happens against the surviving pool.
        for (var index = 0; index < 32; index++)
        {
            var trial = state with { Rng = PrototypeRng.CreateBundle($"retarget-{index}"),
                World = state.World with { Combat = state.World.Combat! with { Enemies = enemies } } };
            var next = Play(trial, 1);
            var result = next.World!.Combat?.Enemies;
            if (result is not null && result[0].Hp == 0 && result[1].Hp == 100 - damage)
            {
                Assert.Equal(1, trial.World.Combat!.Enemies[0].Hp);
                PrototypeStateInvariants.Validate(next);
                return;
            }
        }
        Assert.Fail("No seeded fatal-first-hit scenario was exercised.");
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 3)]
    public void DistractionGeneratesASoloSkillFreeForTheTurnAndExhausts(int upgrade, int energy)
    {
        var state = Setup([Prefix + "distraction"], [1], [], upgrade);
        var next = Play(state, 1);
        var combat = next.World!.Combat!;
        Assert.Contains(1, combat.ExhaustPile);
        var generated = combat.Cards.Single(card => card.IsTemporary);
        var definition = PrototypeContent.Card(generated.CardId);
        Assert.Equal(PrototypeCardType.Skill, definition.Type);
        Assert.True(definition.CanBeGeneratedInCombat);
        Assert.Contains(definition.Rarity, new[] { PrototypeCardRarity.Common, PrototypeCardRarity.Uncommon, PrototypeCardRarity.Rare });
        Assert.False(definition.MultiplayerOnly);
        Assert.Equal(0, generated.TemporaryEnergyCost!.Cost);
        Assert.Equal(PrototypeTemporaryCardCostExpiry.EndOfTurn, generated.TemporaryEnergyCost.Expiry);
        Assert.Equal(energy, combat.Energy);
        Assert.DoesNotContain(state.Player.Deck, card => card.CardId == generated.CardId);
        PrototypeStateInvariants.Validate(next);
    }

    [Fact]
    public void SuspendedCardChoiceSnapshotResumesTheSameContinuation()
    {
        var state = Setup(["proto.silent.survivor", "proto.silent.strike", "proto.silent.defend"], [1, 2, 3], []);
        var pending = Play(state, 1);
        Assert.NotNull(pending.World!.Combat!.PendingChoice);
        var restored = RunSnapshot.Load(RunSnapshot.Save(pending));
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(pending).First();
        Assert.Equal(CanonicalJson.Sha256(engine.Step(pending, action).State),
            CanonicalJson.Sha256(engine.Step(restored, action).State));
        Assert.NotNull(pending.World.Combat.PendingChoice);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    public void DualWieldCopiesSelectedAttackOrPowerWithIndependentIdentity(int upgrade, int copies)
    {
        var state = Setup([Prefix + "dual_wield", "proto.silent.strike", "proto.silent.defend", Prefix + "caltrops"], [1, 2, 3, 4], [], upgrade);
        var card = state.World!.Combat!.Cards[1] with { UpgradeLevel = 1,
            TemporaryEnergyCost = new(0, PrototypeTemporaryCardCostExpiry.EndOfTurn), ReplayCount = 2 };
        state = state with { World = state.World with { Combat = state.World.Combat with {
            Cards = state.World.Combat.Cards.Select(item => item.InstanceId == 2 ? card : item).ToArray() } } };
        var pending = Play(state, 1);
        Assert.Equal(new long[] { 2, 4 }, pending.World!.Combat!.PendingChoice!.CandidateCardInstanceIds);
        var engine = new PrototypeGameEngine();
        var action = GameAction.Create("select_cards", new SelectCardsPayload([2]));
        var restored = RunSnapshot.Load(RunSnapshot.Save(pending));
        var next = engine.Step(pending, action).State;
        Assert.Equal(CanonicalJson.Sha256(next), CanonicalJson.Sha256(engine.Step(restored, action).State));
        var generated = next.World!.Combat!.Cards.Where(item => item.IsTemporary).ToArray();
        Assert.Equal(copies, generated.Length);
        Assert.All(generated, item => {
            Assert.NotEqual(2, item.InstanceId);
            Assert.Null(item.PersistentCardInstanceId);
            Assert.Equal(card.UpgradeLevel, item.UpgradeLevel);
            Assert.Equal(card.TemporaryEnergyCost, item.TemporaryEnergyCost);
            Assert.Equal(2, item.ReplayCount);
        });
        Assert.Contains(2, next.World.Combat.Hand);
        Assert.Contains(1, next.World.Combat.DiscardPile);
        Assert.Equal(4, next.Player.Deck.Length);
        PrototypeStateInvariants.Validate(next);
    }

    [Theory]
    [InlineData(0, 6, 0, 1)]
    [InlineData(7, 0, 0, 1)]
    [InlineData(6, 0, 2, 1)]
    [InlineData(3, 3, 2, 1)]
    [InlineData(3, 3, 4, 2)]
    public void HandDrillTriggersOnActualBlockBreakAfterResolvingTheHit(int block, int hpLost, int vulnerable, int relicCount)
    {
        var state = Setup(["proto.silent.strike"], [1], []);
        state = state with { Player = state.Player with { Relics = Enumerable.Range(0, relicCount)
                .Select(_ => new RelicInstance("proto.native.trash_heap.hand_drill", PrototypeJson.EmptyObject())).ToArray() },
            World = state.World! with { Combat = state.World!.Combat! with {
                Relics = Enumerable.Range(0, relicCount).Select(index =>
                    new CombatRelicState(index, "proto.native.trash_heap.hand_drill", index + 1, [])).ToArray(),
                NextPowerApplicationOrder = relicCount + 1,
                Enemies = state.World.Combat.Enemies.Select(
                enemy => enemy.InstanceId == 1 ? enemy with { Block = block } : enemy).ToArray() } } };
        var next = Play(state, 1, 1);
        var enemy = next.World!.Combat!.Enemies[0];
        Assert.Equal(100 - hpLost, enemy.Hp);
        Assert.Equal(vulnerable, enemy.PowerStates.Where(power => power.PowerId == "proto.power.vulnerable").Sum(power => power.Stacks));
        Assert.Equal(block, state.World.Combat!.Enemies[0].Block);
        PrototypeStateInvariants.Validate(next);
    }

    [Fact]
    public void ExtraGoldGroupsCanBeCollectedIndependentlyAndSkippedWithoutCredit()
    {
        var root = new PrototypeGameEngine().Step(PrototypeGameFactory.Create("extra-gold"), GameAction.Empty("start_run")).State;
        var entered = new PrototypeGameEngine().Step(root, new PrototypeGameEngine().GetLegalActions(root)[0]).State;
        var reward = new RewardState("Combat", [], null, null, true, true, true, false,
            GoldOption: 12, GoldResolved: false, IndependentSelection: true,
            ExtraCardGroupsResolved: [], ExtraRelicGroupsResolved: [],
            ExtraGoldOptions: [20, 7], ExtraGoldGroupsResolved: [false, false]);
        var state = entered with { Phase = RunPhase.Reward, World = entered.World! with
            { Reward = reward, Combat = null } };
        var hash = CanonicalJson.Sha256(state);
        var engine = new PrototypeGameEngine();
        var collected = engine.Step(state, GameAction.Create("take_reward_extra_gold", new ChooseGoldRewardPayload(1))).State;
        Assert.Equal(state.Player.Gold + 7, collected.Player.Gold);
        Assert.Equal(hash, CanonicalJson.Sha256(state));
        var skipped = engine.Step(collected, GameAction.Empty("leave_reward")).State;
        Assert.Equal(collected.Player.Gold, skipped.Player.Gold);
        Assert.Throws<InvalidOperationException>(() => engine.Step(collected,
            GameAction.Create("take_reward_extra_gold", new ChooseGoldRewardPayload(1))));
        var view = new PrototypeAiEnvironment().Observe(collected).Observation.Reward!;
        Assert.Equal(new PrototypeAiExtraGoldReward(0, 20), Assert.Single(view.PendingExtraGoldRewards!));
        Assert.Equal(CanonicalJson.Sha256(collected), CanonicalJson.Sha256(RunSnapshot.Load(RunSnapshot.Save(collected))));
    }

    [Theory]
    [InlineData(0, 9)]
    [InlineData(1, 12)]
    public void ReboundChangesTheNextPhysicalCardDestinationBeforeItsEffectsAndPersistsThroughChoice(int upgrade, int damage)
    {
        var state = Setup([Prefix + "rebound", "proto.silent.survivor", "proto.silent.strike"], [1, 2, 3], [], upgrade);
        var rebound = Play(state, 1, 1);
        Assert.Equal(100 - damage, rebound.World!.Combat!.Enemies[0].Hp);
        Assert.Contains(1, rebound.World.Combat.DiscardPile);
        var pending = Play(rebound, 2);
        Assert.Equal(PrototypeCardZone.DrawPile, pending.World!.Combat!.PendingChoice!.SourceCardDestination);
        Assert.DoesNotContain(pending.World.Combat.PlayerPowers, power => power.PowerId == "proto.power.rebound");
        var action = GameAction.Create("select_cards", new SelectCardsPayload([3]));
        var engine = new PrototypeGameEngine();
        var next = engine.Step(pending, action).State;
        Assert.Equal(CanonicalJson.Sha256(next), CanonicalJson.Sha256(engine.Step(
            RunSnapshot.Load(RunSnapshot.Save(pending)), action).State));
        Assert.Equal(2, next.World!.Combat!.DrawPile.Last());
        Assert.Contains(3, next.World.Combat.DiscardPile);
        PrototypeStateInvariants.Validate(next);
    }

    [Fact]
    public void ReboundSkipsExhaustAndPowerCardsAndExpiresAtTurnEnd()
    {
        var state = Setup([Prefix + "rebound", Prefix + "distraction", Prefix + "hello_world"], [1, 2, 3], []);
        var next = Play(Play(Play(state, 1, 1), 2), 3);
        Assert.Contains(2, next.World!.Combat!.ExhaustPile);
        Assert.Contains(next.World.Combat.PlayerPowers, power => power.PowerId == "proto.power.rebound");
        var ended = new PrototypeGameEngine().Step(next, GameAction.Empty("end_turn")).State;
        Assert.DoesNotContain(ended.World!.Combat!.PlayerPowers, power => power.PowerId == "proto.power.rebound");
        PrototypeStateInvariants.Validate(ended);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void HelloWorldGeneratesDistinctOwnerCommonCardsBeforeNextTurnDraw(int upgrade)
    {
        var state = Setup([Prefix + "hello_world", Prefix + "hello_world"], [1, 2], [], upgrade);
        var powered = Play(Play(state, 1), 2);
        Assert.DoesNotContain(powered.World!.Combat!.Cards, card => card.IsTemporary);
        var engine = new PrototypeGameEngine();
        var next = engine.Step(powered, GameAction.Empty("end_turn")).State;
        var generated = next.World!.Combat!.Cards.Where(card => card.IsTemporary).ToArray();
        Assert.Equal(2, generated.Length);
        Assert.Equal(2, generated.Select(card => card.CardId).Distinct().Count());
        Assert.All(generated, card => {
            var definition = PrototypeContent.Card(card.CardId);
            Assert.Contains(card.CardId, PrototypeContent.NativeSilentCardPool);
            Assert.Equal(PrototypeCardRarity.Common, definition.Rarity);
            Assert.False(definition.MultiplayerOnly);
            Assert.Null(card.TemporaryEnergyCost);
            Assert.Equal(0, card.UpgradeLevel);
        });
        Assert.Equal(CanonicalJson.Sha256(next), CanonicalJson.Sha256(engine.Step(
            RunSnapshot.Load(RunSnapshot.Save(powered)), GameAction.Empty("end_turn")).State));
        Assert.True(PrototypeContent.Card(Prefix + "hello_world").InnateOnUpgrade);
        PrototypeStateInvariants.Validate(next);
    }

    private static RunState Play(RunState state, long id, int? target = null)
    {
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state).First(action => action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId == id
            && action.ReadPayload<PlayCardPayload>().TargetEnemyId == target);
        return engine.Step(state, action).State;
    }

    private static RunState Setup(string[] cardIds, long[] hand, long[] discard, int upgrade = 0)
    {
        var empty = PrototypeJson.EmptyObject();
        var deck = cardIds.Select((id, index) => new CardInstance(index + 1, id, index == 0 ? upgrade : 0, empty)).ToArray();
        var combat = new CombatState(1, 3, 0, hand, [], discard, [],
            [new(1, "proto.enemy.crawler", 100, 0, 0, new(StringComparer.Ordinal)),
             new(2, "proto.enemy.crawler", 100, 0, 0, new(StringComparer.Ordinal))],
            deck.Length + 1, deck.Select(card => new CombatCardInstance(card.InstanceId,
                card.InstanceId, card.CardId, card.UpgradeLevel, false, empty)).ToArray(), [], 1);
        return new("prototype-unbound", "prototype-0.1", "shared", "shared", 0, RunPhase.Combat,
            new(70, 70, 99, deck, [], new PotionInstance?[2]), PrototypeRng.CreateBundle("shared"), empty,
            new(PrototypeContent.RulesetId, "silent", 1, 1, deck.Length + 1, PrototypeRoomType.Combat,
                new([]), combat, null, null, null, null));
    }
}
