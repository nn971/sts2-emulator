using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveEliteReactivePowerTests
{
    private const string Entomancer = PrototypeNativeHiveElites.EntomancerId;
    private const string Prism = PrototypeNativeHiveElites.PrismId;

    [Fact]
    public void PersonalHiveAddsOneDazedPerStackOnPoweredAttack()
    {
        foreach (var count in new[] { 1, 3 })
        {
            var engine = new PrototypeGameEngine();
            var original = State(Entomancer, "hive-hit-" + count,
                "proto.silent.strike", count);
            var before = CanonicalJson.Sha256(original);
            var action = engine.GetLegalActions(original).Single(a =>
                a.Kind == "play_card"
                && a.ReadPayload<PlayCardPayload>().TargetEnemyId == 1);
            var after = engine.Step(original, action).State.World!.Combat!;
            Assert.Equal(count, after.Cards.Count(card =>
                card.CardId == "proto.status.dazed"));
            Assert.Equal(count, after.DrawPile.Count(id =>
                after.Cards.Any(card => card.InstanceId == id
                    && card.CardId == "proto.status.dazed")));
            Assert.Equal(before, CanonicalJson.Sha256(original));
        }
    }

    [Theory]
    [InlineData(1, 2, 1)]
    [InlineData(2, 3, 1)]
    [InlineData(3, 3, 2)]
    public void PheromoneSpitCapsHiveAndAdjustsStrength(
        int initial, int expectedHive, int expectedStrength)
    {
        var engine = new PrototypeGameEngine();
        var state = State(Entomancer, "hive-pheromone-" + initial,
            null, initial, moveIndex: 2);
        state = state with
        {
            World = state.World! with
            {
                Combat = PrototypeGameEngine.CommitEnemyIntents(
                    state.World.Combat!, state.Rng)
            }
        };
        var next = engine.Step(state,
            engine.GetLegalActions(state).Single(a =>
                a.Kind == "end_turn")).State;
        var enemy = Assert.Single(next.World!.Combat!.Enemies);
        Assert.Equal("pheromone_spit", enemy.LastMoveId);
        Assert.Equal(expectedHive, Stacks(enemy,
            PrototypeNativeHiveElites.PersonalHiveId));
        Assert.Equal(expectedStrength, Stacks(enemy,
            "proto.power.strength"));
    }

    [Fact]
    public void VitalSparkAfflictsSkillsAndCreatesTemporaryIncomingDamage()
    {
        var current = State(Prism, "hive-tainted", "proto.silent.defend",
            3).World!.Combat!;
        var refresh = typeof(PrototypeGameEngine).GetMethod(
            "RefreshNativeHiveVitalSpark",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(refresh);
        var afflicted = Assert.IsType<CombatState>(
            refresh!.Invoke(null, [current]));
        var card = Assert.Single(afflicted.Cards);
        Assert.Equal(PrototypeCardAfflictionKind.Tainted,
            card.Affliction?.Kind);
        Assert.Equal(3, card.Affliction?.Amount);
        Assert.Equal(1, card.Affliction?.SourceEnemyInstanceId);
        Assert.Null(Assert.Single(current.Cards).Affliction);

        var completed = typeof(PrototypeGameEngine).GetMethod(
            "ResolveNativeCardCompletionPowers",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(completed);
        var next = Assert.IsType<CombatState>(completed!.Invoke(null,
        [
            afflicted,
            new PrototypeCombatEvent(PrototypeCombatEventKind.CardPlayed,
                SourceCardInstanceId: card.InstanceId,
                CardId: card.CardId)
        ]));
        Assert.Equal(3, next.PlayerPowers.Single(power =>
            power.PowerId == PrototypeNativeHiveElites.TaintedPowerId).Stacks);

        var incoming = typeof(PrototypeGameEngine).GetMethod(
            "ModifyIncomingPlayerAttackDamage",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(incoming);
        Assert.Equal(13, incoming!.Invoke(null, [next, 10]));

        var expired = next with
        {
            Enemies = next.Enemies.Select(e => e with { Hp = 0 }).ToArray()
        };
        expired = Assert.IsType<CombatState>(refresh.Invoke(null, [expired]));
        Assert.Null(Assert.Single(expired.Cards).Affliction);
    }

    private static int Stacks(EnemyCombatState enemy, string powerId) =>
        enemy.PowerStates
            .Where(power => power.PowerId == powerId)
            .Sum(power => power.Stacks);

    private static RunState State(
        string enemyId, string seed, string? cardId, int stacks,
        int moveIndex = 0)
    {
        var definition = PrototypeContent.Enemy(enemyId);
        var power = enemyId == Entomancer
            ? PrototypeNativeHiveElites.PersonalHiveId
            : PrototypeNativeHiveElites.VitalSparkId;
        var enemy = new EnemyCombatState(
            InstanceId: 1, EnemyId: enemyId,
            Hp: definition.HpAt(2, 0), Block: 0,
            MoveIndex: moveIndex,
            Statuses: new Dictionary<string, int>(StringComparer.Ordinal),
            Powers: [new PrototypePowerInstanceState(power, stacks, 1)]);
        var card = cardId is null
            ? null
            : new CardInstance(1, cardId, 0, PrototypeJson.EmptyObject());
        var cards = cardId is null
            ? Array.Empty<CombatCardInstance>()
            : [new CombatCardInstance(
                1, 1, cardId, 0, false, PrototypeJson.EmptyObject())];
        var combat = new CombatState(
            1, 3, 0,
            cardId is null ? [] : [1],
            [], [], [], [enemy], cardId is null ? 1 : 2,
            cards, [], 2, Act: 2);
        return new RunState("prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat,
            new PlayerState(1000, 1000, 0,
                card is null ? [] : [card], [],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 2, 1, 1,
                PrototypeRoomType.Elite, new MapState([]),
                combat, null, null, null, null));
    }
}
