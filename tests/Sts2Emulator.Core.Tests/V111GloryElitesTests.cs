using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111GloryElitesTests
{
    [Theory]
    [InlineData(0, 234, 29, 6, 18, 300, 25, 8, 35)]
    [InlineData(8, 254, 29, 6, 18, 320, 25, 8, 35)]
    [InlineData(9, 254, 31, 7, 19, 320, 30, 12, 40)]
    public void PinnedHealthDamageAndEncounterGates(
        int asc, int soulHp, int burnDamage, int maelstromDamage,
        int drainDamage, int knightHp, int chargeDamage,
        int flamethrowerDamage, int cleaveDamage)
    {
        var soul = PrototypeContent.Enemy(PrototypeNativeGloryElites.SoulNexusId);
        var knight = PrototypeContent.Enemy(PrototypeNativeGloryElites.MechaKnightId);
        Assert.Equal((soulHp, soulHp), soul.HpRangeAt(3, asc));
        Assert.Equal((knightHp, knightHp), knight.HpRangeAt(3, asc));
        Assert.Equal(burnDamage, soul.Moves[0].Effects[0].AmountAt(3, asc));
        Assert.Equal(maelstromDamage, soul.Moves[1].Effects[0].AmountAt(3, asc));
        Assert.Equal(4, soul.Moves[1].Effects[0].Repetitions);
        Assert.Equal(drainDamage, soul.Moves[2].Effects[0].AmountAt(3, asc));
        Assert.Equal(new[] { "proto.power.vulnerable", "proto.power.weak" },
            soul.Moves[2].Effects.Skip(1).Select(effect => effect.PowerId));
        Assert.All(soul.Moves[2].Effects.Skip(1), effect =>
            Assert.Equal(2, effect.Amount));
        Assert.Equal(chargeDamage, knight.Moves[0].Effects[0].AmountAt(3, asc));
        Assert.Equal(flamethrowerDamage, knight.Moves[1].Effects[0].AmountAt(3, asc));
        Assert.Equal(cleaveDamage, knight.Moves[3].Effects[0].AmountAt(3, asc));
        Assert.Equal(15, knight.Moves[2].Effects[0].Amount);
        Assert.Equal(5, knight.Moves[2].Effects[1].Amount);
        Assert.Equal(3, Assert.Single(knight.StartingPowers!).Stacks);
        Assert.Equal("proto.power.artifact", knight.StartingPowers![0].PowerId);
        Assert.Equal(1, knight.MoveLoopStartIndex);

        foreach (var encounterId in new[]
        {
            PrototypeNativeGloryElites.SoulNexusEncounterId,
            PrototypeNativeGloryElites.MechaKnightEncounterId
        })
        {
            var encounter = PrototypeContent.Encounter(encounterId);
            Assert.Equal(PrototypeRoomType.Elite, encounter.RoomType);
            Assert.Equal((3, 3, 0),
                (encounter.MinAct, encounter.MaxAct, encounter.Weight));
            Assert.Single(encounter.EnemyIds);
        }
    }

    [Fact]
    public void SoulNexusRandomMovesNeverRepeatAndAreCommittedBeforeObservation()
    {
        var engine = new PrototypeGameEngine();
        var state = Start(PrototypeNativeGloryElites.SoulNexusEncounterId, "soul-nexus");
        var previous = (string?)null;
        for (var i = 0; i < 18; i++)
        {
            var before = state.World!.Combat!.Enemies[0];
            Assert.NotNull(before.PlannedMoveIndex);
            var fork = state.Fork();
            Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));
            state = EndTurn(engine, state);
            var sameFork = EndTurn(engine, fork);
            Assert.Equal(CanonicalJson.Sha256(state),
                CanonicalJson.Sha256(sameFork));
            var enemy = state.World!.Combat!.Enemies[0];
            Assert.NotNull(enemy.LastMoveId);
            if (previous is not null)
                Assert.NotEqual(previous, enemy.LastMoveId);
            if (i == 0)
                Assert.Equal("soul_burn", enemy.LastMoveId);
            previous = enemy.LastMoveId;
        }
    }

    [Fact]
    public void KnightFlamethrowerAddsFourBurnsWhichDamageAtTurnEnd()
    {
        var engine = new PrototypeGameEngine();
        var state = Start(PrototypeNativeGloryElites.MechaKnightEncounterId,
            "mecha-flamethrower");
        var enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("charge", enemy.PlannedMoveIndex == 0
            ? "charge" : "unexpected");
        Assert.Equal(3, enemy.PowerStates.Single(power =>
            power.PowerId == "proto.power.artifact").Stacks);

        state = EndTurn(engine, state);
        Assert.Equal("charge",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
        state = EndTurn(engine, state);
        var combat = state.World!.Combat!;
        Assert.Equal("flamethrower", Assert.Single(combat.Enemies).LastMoveId);
        var burns = combat.Hand.Select(id => combat.Cards.Single(
            card => card.InstanceId == id)).Where(card =>
            card.CardId == PrototypeNativeGloryElites.BurnId).ToArray();
        Assert.Equal(4, burns.Length);
        Assert.Equal(4, burns.Select(b => b.InstanceId).Distinct().Count());
        var burnDef = PrototypeContent.Card(PrototypeNativeGloryElites.BurnId);
        Assert.Equal(2, burnDef.EndTurnDamageIfInHand);
        Assert.True(burnDef.Unplayable);
        Assert.Equal(0, burnDef.MaxUpgradeLevel);
        Assert.DoesNotContain(engine.GetLegalActions(state),
            action => action.Kind == "play_card"
                && action.PayloadJson.Contains(PrototypeNativeGloryElites.BurnId));

        var hpBefore = state.Player.Hp;
        state = EndTurn(engine, state);
        Assert.Equal(hpBefore - 8, state.Player.Hp);
        enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("windup", enemy.LastMoveId);
        Assert.Equal(15, enemy.Block);
        Assert.Equal(5, enemy.PowerStates.Single(power =>
            power.PowerId == "proto.power.strength").Stacks);
        state = EndTurn(engine, state);
        Assert.Equal("heavy_cleave",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
        state = EndTurn(engine, state);
        Assert.Equal("flamethrower",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
    }

    private static RunState Start(string encounterId, string seed)
    {
        var initial = new RunState("prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat,
            new PlayerState(9000, 9000, 0, [], [],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 3, 1, 1,
                PrototypeRoomType.Elite, new MapState([]),
                new CombatState(1, 3, 0, [], [], [], [], [],
                    1, [], [], 1, Act: 3),
                null, null, null, null));
        var method = typeof(PrototypeGameEngine).GetMethod(
            "StartCombat", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<RunState>(method!.Invoke(null,
        [
            initial, PrototypeRoomType.Elite,
            PrototypeContent.Encounter(encounterId)
        ]));
    }

    private static RunState EndTurn(PrototypeGameEngine engine,
        RunState state) =>
        engine.Step(state, engine.GetLegalActions(state)
            .Single(action => action.Kind == "end_turn")).State;
}
