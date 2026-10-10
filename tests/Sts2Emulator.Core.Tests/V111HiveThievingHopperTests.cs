using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveThievingHopperTests
{
    private const string Hopper = "proto.native.hive.thieving_hopper";
    private const string Encounter =
        "proto.native.hive.encounter.thieving_hopper_weak";

    [Theory]
    [InlineData(0, 79, 17, 21, 14)]
    [InlineData(8, 84, 17, 21, 14)]
    [InlineData(9, 84, 19, 23, 16)]
    public void NativeHopperStatsAndFiveMoveSequence(
        int asc, int hp, int theft, int hat, int nab)
    {
        var enemy = PrototypeContent.Enemy(Hopper);
        Assert.Equal((hp, hp), enemy.HpRangeAt(2, asc));
        Assert.Equal(5, enemy.StartingPowers!.Single().StacksAt(asc));
        Assert.Equal("proto.native.hive.escape_artist",
            enemy.StartingPowers!.Single().PowerId);
        Assert.Equal(new[] { "thievery", "flutter", "hat_trick",
            "nab", "escape", "stunned" },
            enemy.Moves.Select(m => m.Id));
        Assert.Equal(theft, enemy.Moves[0].Effects[1].AmountAt(2, asc));
        Assert.Equal(hat, enemy.Moves[2].Effects[0].AmountAt(2, asc));
        Assert.Equal(nab, enemy.Moves[3].Effects[0].AmountAt(2, asc));
        Assert.Equal(5, enemy.Moves[1].Effects[0].Amount);
        Assert.Equal(PrototypeEnemyEffectKind.EscapeEnemy,
            enemy.Moves[4].Effects[0].Kind);

        var formation = PrototypeContent.Encounter(Encounter);
        Assert.Equal(new[] { Hopper }, formation.EnemyIds);
        Assert.Equal(0, formation.Weight);
        Assert.Equal(2, formation.MinAct);
        Assert.Equal(2, formation.MaxAct);
    }

    [Fact]
    public void TheftSelectsUncommonAheadOfRareCommonAndBasicAndRemovesDeckCopy()
    {
        var before = State("hopper-theft-priority",
            [
                ("proto.silent.strike", 0),
                ("proto.silent.dodge_and_roll", 0),
                ("proto.silent.footwork", 1),
                ("proto.silent.defend", 0)
            ]);
        var engine = new PrototypeGameEngine();
        var stable = CanonicalJson.Sha256(before);
        var next = EndTurn(engine, before);
        Assert.Equal("thievery",
            Assert.Single(next.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(83, next.Player.Hp);
        Assert.Equal(3, next.Player.Deck.Length);
        Assert.DoesNotContain(next.Player.Deck, card =>
            card.CardId == "proto.silent.footwork");
        Assert.Equal(3, next.World.Combat.Cards.Length);
        var thief = Assert.Single(next.World.Combat.Enemies);
        var stolen = Assert.Single(thief.StolenCards!);
        Assert.Equal(3, stolen.InstanceId);
        Assert.Equal(1, stolen.UpgradeLevel);
        Assert.Equal("proto.silent.footwork", stolen.CardId);
        Assert.Contains(thief.PowerStates, power =>
            power.PowerId == "proto.native.hive.swipe");
        Assert.DoesNotContain(3, next.World.Combat.DrawPile);
        Assert.DoesNotContain(3, next.World.Combat.DiscardPile);
        Assert.DoesNotContain(next.World.Combat.Cards, card =>
            card.InstanceId == 3);
        Assert.Equal(stable, CanonicalJson.Sha256(before));

        var replay = EndTurn(engine, before.Fork());
        Assert.Equal(CanonicalJson.Sha256(next),
            CanonicalJson.Sha256(replay));
    }

    [Fact]
    public void KillingThiefReturnsOriginalPersistentInstanceExactlyOnce()
    {
        var engine = new PrototypeGameEngine();
        var state = State("hopper-recover",
            [
                ("proto.silent.footwork", 1),
                ("proto.silent.strike", 0)
            ]);
        state = EndTurn(engine, state);
        var stolen = Assert.Single(
            Assert.Single(state.World!.Combat!.Enemies).StolenCards!);
        Assert.DoesNotContain(state.Player.Deck, card =>
            card.InstanceId == stolen.InstanceId);

        var defeated = Hit(state.World.Combat!, 1000);
        Assert.Equal(0, Assert.Single(defeated.Enemies).Hp);
        state = state with
        {
            World = state.World with { Combat = defeated }
        };
        var rewardState = Reward(state);
        Assert.Equal(RunPhase.Reward, rewardState.Phase);
        Assert.Equal(2, rewardState.Player.Deck.Length);
        var restored = Assert.Single(rewardState.Player.Deck,
            card => card.InstanceId == stolen.InstanceId);
        Assert.Equal(stolen, restored);
        var reward = rewardState.World!.Reward!;
        Assert.Equal(stolen,
            Assert.Single(reward.ReturnedStolenCards!));
        Assert.Equal(CanonicalJson.Sha256(rewardState),
            CanonicalJson.Sha256(rewardState.Fork()));
    }

    [Fact]
    public void EscapeKeepsStolenCardOutOfPersistentDeck()
    {
        var engine = new PrototypeGameEngine();
        var state = State("hopper-escapes",
            [
                ("proto.silent.footwork", 1),
                ("proto.silent.strike", 0)
            ]);
        var ids = new[] { "thievery", "flutter", "hat_trick", "nab",
            "escape" };
        foreach (var move in ids)
        {
            state = EndTurn(engine, state);
            if (move != "escape")
            {
                Assert.Equal(move,
                    Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
            }
        }
        Assert.Equal(RunPhase.Reward, state.Phase);
        Assert.Equal(1, state.Player.Deck.Length);
        Assert.DoesNotContain(state.Player.Deck, card =>
            card.CardId == "proto.silent.footwork");
        Assert.Null(state.World!.Reward!.ReturnedStolenCards);
    }

    [Fact]
    public void FlutterReducesPoweredHitsButNotOrdinaryDamage()
    {
        var combat = State("hopper-flutter", []).World!.Combat!;
        var enemy = combat.Enemies.Single() with
        {
            Powers = [new PrototypePowerInstanceState(
                "proto.native.hive.flutter", 5, 3)],
            PlannedMoveIndex = 2,
            PlannedNextAiStateId = "nab",
            AiStateId = "hat_trick"
        };
        combat = combat with { Enemies = [enemy] };
        var raw = Hit(combat, 10, powered: false);
        Assert.Equal(69, raw.Enemies.Single().Hp);
        Assert.Equal(5, Assert.Single(raw.Enemies.Single().PowerStates).Stacks);
        var powered = Hit(combat, 10, powered: true);
        Assert.Equal(74, powered.Enemies.Single().Hp);
        Assert.Equal(4,
            Assert.Single(powered.Enemies.Single().PowerStates).Stacks);
        Assert.Equal(2, powered.Enemies.Single().PlannedMoveIndex);
    }

    [Fact]
    public void FiveUnblockedPoweredHitsConsumeFlutterAndForceOneStun()
    {
        var engine = new PrototypeGameEngine();
        var state = State("hopper-flutter-stun", []);
        var current = state.World!.Combat! with
        {
            Enemies = [state.World.Combat.Enemies.Single() with
            {
                Powers = [new PrototypePowerInstanceState(
                    "proto.native.hive.flutter", 5, 3)],
                AiStateId = "hat_trick",
                PlannedMoveIndex = 2,
                PlannedNextAiStateId = "nab"
            }]
        };
        for (var i = 1; i <= 5; i++)
        {
            current = Hit(current, 10, powered: true);
            var enemy = current.Enemies.Single();
            if (i < 5)
            {
                Assert.Equal(5 - i,
                    Assert.Single(enemy.PowerStates).Stacks);
                Assert.Equal(2, enemy.PlannedMoveIndex);
            }
            else
            {
                Assert.Empty(enemy.PowerStates);
                Assert.Equal(5, enemy.PlannedMoveIndex);
                Assert.Equal("hat_trick", enemy.PlannedNextAiStateId);
            }
        }
        Assert.Equal(54, current.Enemies.Single().Hp);
        state = state with
        {
            World = state.World with { Combat = current }
        };
        state = EndTurn(engine, state);
        Assert.Equal("stunned",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(100, state.Player.Hp);
        Assert.Equal(2,
            Assert.Single(state.World.Combat.Enemies).PlannedMoveIndex);
        state = EndTurn(engine, state);
        Assert.Equal("hat_trick",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
    }

    [Fact]
    public void NoAvailablePersistentDrawOrDiscardCardDoesNotSteal()
    {
        var engine = new PrototypeGameEngine();
        var state = State("hopper-no-cards", []);
        var before = state.Player.Deck;
        state = EndTurn(engine, state);
        Assert.Equal(83, state.Player.Hp);
        Assert.Equal(before, state.Player.Deck);
        Assert.Null(Assert.Single(state.World!.Combat!.Enemies).StolenCards);
    }

    private static RunState State(
        string seed, (string id, int upgrade)[] cards)
    {
        var deck = cards.Select((card, i) =>
            new CardInstance(i + 1, card.id, card.upgrade,
                PrototypeJson.EmptyObject())).ToArray();
        var combatCards = deck.Select(card =>
            new CombatCardInstance(card.InstanceId, card.InstanceId,
                card.CardId, card.UpgradeLevel, false,
                card.PersistentState)).ToArray();
        var combat = new CombatState(1, 3, 0, [],
            deck.Select(card => card.InstanceId).ToArray(),
            [], [], [
                new EnemyCombatState(1, Hopper, 79, 0, 0,
                    new Dictionary<string, int>(StringComparer.Ordinal),
                    Powers: [new PrototypePowerInstanceState(
                        "proto.native.hive.escape_artist", 5, 1)],
                    AiStateId: "thievery")
            ], cards.Length + 1, combatCards, [], 2,
            Act: 2, Ascension: 0);
        var rng = PrototypeRng.CreateBundle(seed);
        combat = PrototypeGameEngine.CommitEnemyIntents(combat, rng);
        return new RunState("prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat,
            new PlayerState(100, 100, 0, deck, [],
                new PotionInstance?[2]), rng,
            PrototypeJson.EmptyObject(),
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 2, 1, cards.Length + 1,
                PrototypeRoomType.Combat,
                new MapState([]), combat, null, null, null, null));
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine, RunState state) =>
        engine.Step(state, engine.GetLegalActions(state)
            .Single(action => action.Kind == "end_turn")).State;

    private static CombatState Hit(
        CombatState combat, int damage, bool powered = false)
    {
        var method = typeof(PrototypeGameEngine).GetMethod(
            "DamageEnemyInternal", BindingFlags.NonPublic |
                BindingFlags.Static);
        Assert.NotNull(method);
        var result = method!.Invoke(null,
            [combat, 1, damage, 0, powered]);
        var property = result!.GetType().GetProperty("Combat");
        Assert.NotNull(property);
        return Assert.IsType<CombatState>(property!.GetValue(result));
    }

    private static RunState Reward(RunState state)
    {
        var method = typeof(PrototypeGameEngine).GetMethod(
            "EnterReward", BindingFlags.NonPublic |
                BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<RunState>(method!.Invoke(null, [state]));
    }
}
