using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksSoulFyshTests
{
    [Fact]
    public void SoulFyshModelMatchesSourceAtAscensionThresholds()
    {
        var boss = PrototypeContent.Enemy("proto.enemy.soul_fysh");
        Assert.Equal((211, 211), boss.HpRangeAt(1, 0));
        Assert.Equal((221, 221), boss.HpRangeAt(1, 8));
        Assert.Equal(new[]
        {
            "beckon", "de_gas", "gaze", "fade", "scream"
        }, boss.Moves.Select(move => move.Id));
        Assert.Equal(16, boss.Moves[1].Effects[0].AmountAt(1, 0));
        Assert.Equal(18, boss.Moves[1].Effects[0].AmountAt(1, 9));
        Assert.Equal(7, boss.Moves[2].Effects[0].AmountAt(1, 0));
        Assert.Equal(8, boss.Moves[2].Effects[0].AmountAt(1, 9));
        Assert.Equal(13, boss.Moves[4].Effects[0].AmountAt(1, 0));
        Assert.Equal(15, boss.Moves[4].Effects[0].AmountAt(1, 9));
        Assert.Equal(PrototypeEnemyEffectKind.AddCardsToRandomDraw,
            boss.Moves[0].Effects[0].Kind);
        Assert.Equal(PrototypeEnemyEffectKind.AddCardsToDiscard,
            boss.Moves[0].Effects[1].Kind);
        Assert.Equal("proto.status.beckon",
            boss.Moves[0].Effects[0].CardId);
        var beckon = PrototypeContent.Card("proto.status.beckon");
        Assert.Equal(6, beckon.EndTurnDamageIfInHand);
        Assert.True(beckon.EndTurnDamageUnblockable);
        Assert.Equal(0, beckon.MaxUpgradeLevel);
        Assert.Equal(PrototypeCardRarity.Status, beckon.Rarity);

        var intangible = PrototypeContent.Power(
            "proto.power.enemy_intangible");
        Assert.Equal(1, intangible.EnemyHpLossCapPerTrigger);
        Assert.Equal(1, intangible.EnemyStacksDecayAtSideTurnEnd);
    }

    [Fact]
    public void SoulFyshBeckonCreatesOneRandomDrawAndOneDiscard()
    {
        var engine = new PrototypeGameEngine();
        var state = FindBoss(engine);
        Assert.Single(state.World!.Combat!.Enemies);
        var before = state.World.Combat;
        state = EndTurn(engine, state);
        var combat = state.World!.Combat!;
        Assert.Equal("beckon", Assert.Single(combat.Enemies).LastMoveId);
        var beckons = combat.Cards.Where(card =>
            card.CardId == "proto.status.beckon").ToArray();
        Assert.Equal(2, beckons.Length);
        Assert.Single(combat.DrawPile.Concat(combat.Hand), id =>
            beckons.Any(card => card.InstanceId == id));
        Assert.Single(combat.DiscardPile, id =>
            beckons.Any(card => card.InstanceId == id));
        Assert.Equal(before.Cards.Length + 2, combat.Cards.Length);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    [Fact]
    public void SoulFyshCycleAndEnemyIntangibleExpiration()
    {
        var engine = new PrototypeGameEngine();
        var state = FindBoss(engine);
        state = EndTurn(engine, state); // Beckon
        state = EndTurn(engine, state); // De-Gas
        Assert.Equal("de_gas",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
        state = EndTurn(engine, state); // Gaze
        Assert.Equal("gaze",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(3, state.World.Combat.Cards.Count(card =>
            card.CardId == "proto.status.beckon"));
        state = EndTurn(engine, state); // Fade
        var enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("fade", enemy.LastMoveId);
        Assert.Equal(1, enemy.PowerStates.Single(power =>
            power.PowerId == "proto.power.enemy_intangible").Stacks);
        state = EndTurn(engine, state); // Scream
        enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("scream", enemy.LastMoveId);
        Assert.DoesNotContain(enemy.PowerStates, power =>
            power.PowerId == "proto.power.enemy_intangible");
        Assert.Equal(3, state.World.Combat.PlayerPowers.Single(power =>
            power.PowerId == "proto.power.vulnerable").Stacks);
    }

    [Fact]
    public void BeckonDealsUnblockableHpLossWhenHeld()
    {
        var engine = new PrototypeGameEngine();
        var state = FindBoss(engine);
        var combat = state.World!.Combat!;
        var prototype = PrototypeContent.Card("proto.status.beckon");
        Assert.Equal(6, prototype.EndTurnDamageIfInHand);
        var cardId = combat.NextCardInstanceId;
        var card = new CombatCardInstance(
            cardId, null, prototype.Id, 0, true,
            PrototypeJson.EmptyObject());
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Hand = combat.Hand.Append(cardId).ToArray(),
                    Cards = combat.Cards.Append(card).ToArray(),
                    PlayerBlock = 50,
                    NextCardInstanceId = cardId + 1
                }
            }
        };
        state = EndTurn(engine, state);
        Assert.Equal(64, state.Player.Hp);
    }

    [Fact]
    public void UnimplementedSelectedBossIsNotSubstituted()
    {
        var engine = new PrototypeGameEngine();
        var initial = PrototypeNativeUnderdocksRunFactory.Create(
            "underdocks-other-boss");
        var unsupported = PrototypeNativeUnderdocks.NativeBossEncounterIds
            .Single(id => id == "proto.encounter.waterfall_giant_boss");
        var state = initial with
        {
            Phase = RunPhase.MapChoice,
            World = initial.World! with
            {
                ActOneEncounterPool = initial.World.ActOneEncounterPool! with
                {
                    BossEncounterId = unsupported
                }
            }
        };
        Assert.Throws<NotSupportedException>(() =>
            StartBoss(engine, state));
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine, RunState state)
    {
        var action = engine.GetLegalActions(state).Single(item =>
            item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static RunState FindBoss(PrototypeGameEngine engine)
    {
        for (var index = 0; index < 60; index++)
        {
            var state = PrototypeNativeUnderdocksRunFactory.Create(
                "soul-fysh-boss-" + index);
            if (state.World!.ActOneEncounterPool!.BossEncounterId
                == "proto.encounter.soul_fysh_boss")
            {
                return StartBoss(engine, state);
            }
        }
        throw new InvalidOperationException(
            "Soul Fysh boss was not selected within sampled seeds.");
    }

    private static RunState StartBoss(
        PrototypeGameEngine engine, RunState state)
    {
        const int floor = 16;
        var node = new MapNodeState(
            "underdocks-soul-fysh-boss-test", 1, floor,
            PrototypeRoomType.Boss, []);
        state = state with
        {
            Phase = RunPhase.MapChoice,
            World = state.World! with
            {
                Floor = floor - 1,
                ActiveRoom = null,
                Map = new MapState(
                    [node], CurrentNodeId: null,
                    EntryNodeIds: [node.NodeId],
                    GenerationProfileId:
                        PrototypeNativeUnderdocks.GenerationProfileId),
                Combat = null, Reward = null, Shop = null, Event = null
            }
        };
        return engine.Step(state,
            Assert.Single(engine.GetLegalActions(state))).State;
    }
}
