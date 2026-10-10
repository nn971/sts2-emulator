using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111LaterActsFirstSliceTests
{
    [Fact]
    public void NativeLaterActInventoriesAreExactAndRouteRemainsGated()
    {
        Assert.Equal(20, PrototypeNativeLaterActs.HiveEncounterNames.Length);
        Assert.Equal(18, PrototypeNativeLaterActs.GloryEncounterNames.Length);
        Assert.Equal(20, PrototypeNativeLaterActs.HiveEncounterNames.Distinct().Count());
        Assert.Equal(18, PrototypeNativeLaterActs.GloryEncounterNames.Distinct().Count());
        Assert.Contains("BowlbugsWeak",
            PrototypeNativeLaterActs.HiveEncounterNames);
        Assert.Contains("DevotedSculptorWeak",
            PrototypeNativeLaterActs.GloryEncounterNames);
        Assert.Contains("TurretOperatorWeak",
            PrototypeNativeLaterActs.GloryEncounterNames);
        var supported = PrototypeContent.Encounter(
            PrototypeNativeLaterActs.GlorySculptorWeakId);
        Assert.Equal(3, supported.MinAct);
        Assert.Equal(3, supported.MaxAct);
        Assert.Equal(0, supported.Weight);
        Assert.Equal(new[] { "proto.native.glory.devoted_sculptor" },
            supported.EnemyIds);
        Assert.DoesNotContain(PrototypeNativeLaterActs.Cards,
            card => PrototypeContent.RewardCardPool.Contains(card.Id));

    }

    [Theory]
    [InlineData(0, 21, 22, 35, 38)]
    [InlineData(8, 23, 24, 36, 39)]
    [InlineData(9, 23, 24, 36, 39)]
    public void HiveWorkerHpMatchesSourceAscensionBreakpoints(
        int asc, int eggMin, int eggMax, int nectarMin, int nectarMax)
    {
        Assert.Equal((eggMin, eggMax),
            PrototypeContent.Enemy("proto.native.hive.bowlbug_egg")
                .HpRangeAt(2, asc));
        Assert.Equal((nectarMin, nectarMax),
            PrototypeContent.Enemy("proto.native.hive.bowlbug_nectar")
                .HpRangeAt(2, asc));
    }

    [Fact]
    public void HiveWorkerActionsHaveNativeOrderAndDifficultyDeltas()
    {
        var egg = PrototypeContent.Enemy("proto.native.hive.bowlbug_egg");
        Assert.Equal(new[] { "bite" }, egg.Moves.Select(move => move.Id));
        Assert.Equal(7, egg.Moves[0].Effects[0].AmountAt(2, 0));
        Assert.Equal(8, egg.Moves[0].Effects[0].AmountAt(2, 9));
        Assert.Equal(PrototypeEnemyEffectKind.GainBlock,
            egg.Moves[0].Effects[1].Kind);
        Assert.Equal(8, egg.Moves[0].Effects[1].AmountAt(2, 9));

        var nectar = PrototypeContent.Enemy("proto.native.hive.bowlbug_nectar");
        Assert.Equal(new[] { "thrash", "buff", "thrash_2" },
            nectar.Moves.Select(move => move.Id));
        Assert.Equal(2, nectar.MoveLoopStartIndex);
        Assert.Equal(PrototypeEnemyEffectKind.ApplyEnemyPower,
            nectar.Moves[1].Effects[0].Kind);
        Assert.Equal(15, nectar.Moves[1].Effects[0].AmountAt(2, 0));
        Assert.Equal(16, nectar.Moves[1].Effects[0].AmountAt(2, 9));
    }

    [Theory]
    [InlineData(0, 162, 12, 41, 3)]
    [InlineData(8, 172, 12, 51, 3)]
    [InlineData(9, 172, 15, 51, 4)]
    public void GloryWeakEnemiesHaveNativeHpAndDamage(
        int asc, int sculptorHp, int savage, int turretHp, int perHit)
    {
        var sculptor = PrototypeContent.Enemy(
            "proto.native.glory.devoted_sculptor");
        var turret = PrototypeContent.Enemy(
            "proto.native.glory.turret_operator");
        Assert.Equal((sculptorHp, sculptorHp),
            sculptor.HpRangeAt(3, asc));
        Assert.Equal(savage, sculptor.Moves[1].Effects[0].AmountAt(3, asc));
        Assert.Equal(1, sculptor.MoveLoopStartIndex);
        Assert.Equal(9, sculptor.Moves[0].Effects[0].Amount);
        Assert.Equal((turretHp, turretHp), turret.HpRangeAt(3, asc));
        Assert.Equal(perHit, turret.Moves[0].Effects[0].AmountAt(3, asc));
        Assert.Equal(5, turret.Moves[0].Effects[0].Repetitions);
        Assert.Equal(1, turret.Moves[2].Effects[0].Amount);
    }

    [Theory]
    [InlineData("extermination", "proto.native.hive.exterminate")]
    [InlineData("squash", "proto.native.hive.squash")]
    public void BugslayerEventAddsExactlyItsChosenEventCard(
        string choiceId, string cardId)
    {
        var state = EventRun("bugslayer-" + choiceId);
        var engine = new PrototypeGameEngine();
        var actions = engine.GetLegalActions(state);
        Assert.Equal(2, actions.Count(a => a.Kind == "event_choice"));
        var selected = actions.Single(a =>
            a.Kind == "event_choice"
            && a.ReadPayload<EventChoicePayload>().ChoiceId == choiceId);
        var before = CanonicalJson.Sha256(state);
        var nextId = state.World!.NextCardInstanceId;
        var result = engine.Step(state, selected).State;
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Equal(state.Player.Deck.Length + 1, result.Player.Deck.Length);
        Assert.Equal(cardId, result.Player.Deck.Last().CardId);
        Assert.Equal(nextId, result.Player.Deck.Last().InstanceId);
        Assert.Equal(1, result.Player.Deck.Count(c => c.CardId == cardId));
        Assert.Equal(nextId + 1, result.World!.NextCardInstanceId);
        Assert.Equal(before, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(result.Fork()));
    }

    [Fact]
    public void BugslayerCardsHaveExactUpgradeAndTargetSemantics()
    {
        var extermination = PrototypeContent.Card(
            PrototypeNativeLaterActs.ExterminateId);
        Assert.Equal(1, extermination.Cost.AmountAt(0));
        Assert.Equal(PrototypeCardTarget.None, extermination.Target);
        Assert.Equal(PrototypeCardType.Attack, extermination.Type);
        Assert.Equal(PrototypeCardRarity.Event, extermination.Rarity);
        Assert.False(extermination.RewardEligible);
        var e = Assert.Single(extermination.Effects);
        Assert.Equal(PrototypeEffectTarget.AllEnemies, e.Target);
        Assert.Equal(4, e.Repetitions);
        Assert.Equal(3, e.AmountAt(0, 0));
        Assert.Equal(4, e.AmountAt(1, 0));

        var squash = PrototypeContent.Card(PrototypeNativeLaterActs.SquashId);
        Assert.Equal(PrototypeCardTarget.Enemy, squash.Target);
        Assert.Equal(1, squash.Cost.AmountAt(1));
        Assert.Equal(2, squash.Effects.Length);
        Assert.Equal(10, squash.Effects[0].AmountAt(0, 0));
        Assert.Equal(12, squash.Effects[0].AmountAt(1, 0));
        Assert.Equal("proto.power.vulnerable", squash.Effects[1].PowerId);
        Assert.Equal(2, squash.Effects[1].AmountAt(0, 0));
        Assert.Equal(3, squash.Effects[1].AmountAt(1, 0));
    }

    [Theory]
    [InlineData("proto.native.hive.exterminate", 0, 88, 88)]
    [InlineData("proto.native.hive.exterminate", 1, 84, 84)]
    [InlineData("proto.native.hive.squash", 0, 90, 100)]
    [InlineData("proto.native.hive.squash", 1, 88, 100)]
    public void NewHiveEventAttacksResolveThroughExistingCombatKernel(
        string cardId, int upgrade, int hp1, int hp2)
    {
        var state = CombatWithCard(cardId, upgrade);
        var engine = new PrototypeGameEngine();
        var available = engine.GetLegalActions(state)
            .Where(action => action.Kind == "play_card").ToArray();
        Assert.Equal(cardId == PrototypeNativeLaterActs.ExterminateId
                ? 1 : 2, available.Length);
        var action = available[0];
        var next = engine.Step(state, action).State;
        var enemies = next.World!.Combat!.Enemies;
        Assert.Equal(hp1, enemies[0].Hp);
        Assert.Equal(hp2, enemies[1].Hp);
        if (cardId == PrototypeNativeLaterActs.SquashId)
        {
            var power = Assert.Single(enemies[0].PowerStates,
                p => p.PowerId == "proto.power.vulnerable");
            Assert.Equal(2 + upgrade, power.Stacks);
        }
        Assert.Equal(2, next.World.Combat.Energy);
    }

    [Fact]
    public void NativeBugslayerAndSculptorRemainOutsidePrototypeRandomPools()
    {
        var bugslayer = PrototypeContent.Event(
            PrototypeNativeLaterActs.HiveBugslayerId);
        Assert.Equal(2, bugslayer.MinAct);
        Assert.Equal(2, bugslayer.MaxAct);
        Assert.Equal(0, bugslayer.Weight);
        Assert.Equal(0, PrototypeContent.Encounter(
            PrototypeNativeLaterActs.GlorySculptorWeakId).Weight);
    }

    private static RunState EventRun(string seed)
    {
        var state = new PrototypeGameEngine().Step(
            PrototypeGameFactory.Create(seed),
            GameAction.Empty("start_run")).State;
        var world = state.World!;
        return state with
        {
            Phase = RunPhase.Event,
            World = world with
            {
                Act = 2,
                Floor = 1,
                ActiveRoom = PrototypeRoomType.Event,
                Event = new EventState(PrototypeNativeLaterActs.HiveBugslayerId),
                Map = new MapState(
                    [
                        new("current", 2, 1, PrototypeRoomType.Event,
                            ["next"]),
                        new("next", 2, 2, PrototypeRoomType.Combat, [])
                    ], "current", ["current"])
            }
        };
    }

    private static RunState CombatWithCard(string id, int upgrade)
    {
        var empty = PrototypeJson.EmptyObject();
        var card = new CardInstance(1, id, upgrade, empty);
        var combatCard = new CombatCardInstance(1, 1, id, upgrade,
            false, empty);
        var combat = new CombatState(
            1, 3, 0, [1], [], [], [],
            [
                new(1, "proto.enemy.crawler", 100, 0, 0,
                    new Dictionary<string, int>(StringComparer.Ordinal)),
                new(2, "proto.enemy.crawler", 100, 0, 0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            2, [combatCard], [], 1);
        return new RunState(
            "prototype-unbound", "prototype-0.1", "later-card-test",
            "later-card-test", 0, RunPhase.Combat,
            new PlayerState(70, 70, 100, [card], [],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle("later-card-test"), empty,
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 2, 1, 2,
                PrototypeRoomType.Combat, new MapState([]),
                combat, null, null, null, null));
    }
}
