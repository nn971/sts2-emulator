using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111LostAndForgottenNormalTests
{
    [Theory]
    [InlineData(0, 93, 106, 4, 13)]
    [InlineData(8, 99, 111, 4, 13)]
    [InlineData(9, 99, 111, 5, 15)]
    public void BothSoulsHavePinnedMovesAndHealth(
        int asc, int lostHp, int forgottenHp, int laser, int dread)
    {
        var lost = PrototypeContent.Enemy(PrototypeNativeGloryNewNormals.TheLostId);
        var forgotten = PrototypeContent.Enemy(
            PrototypeNativeGloryNewNormals.TheForgottenId);
        Assert.Equal((lostHp, lostHp), lost.HpRangeAt(3, asc));
        Assert.Equal((forgottenHp, forgottenHp),
            forgotten.HpRangeAt(3, asc));
        Assert.Equal(new[] { "debilitating_smog", "eye_lasers" },
            lost.Moves.Select(m => m.Id));
        Assert.Equal(new[] { "miasma", "dread" },
            forgotten.Moves.Select(m => m.Id));
        Assert.Equal(PrototypeEnemyEffectKind.StealPlayerPower,
            lost.Moves[0].Effects[0].Kind);
        Assert.Equal("proto.power.strength",
            lost.Moves[0].Effects[0].PowerId);
        Assert.Equal("proto.power.dexterity",
            forgotten.Moves[0].Effects[0].PowerId);
        Assert.Equal(8, forgotten.Moves[0].Effects[1].Amount);
        Assert.Equal(laser, lost.Moves[1].Effects[0].AmountAt(3, asc));
        Assert.Equal(2, lost.Moves[1].Effects[0].Repetitions);
        Assert.Equal(dread, forgotten.Moves[1].Effects[0].AmountAt(3, asc));
        Assert.Equal("proto.power.dexterity",
            forgotten.Moves[1].Effects[0].ExtraAmountFromOwnerPowerId);
        Assert.Equal(PrototypeNativeGloryNewNormals.PossessStrengthId,
            lost.StartingPowers!.Single().PowerId);
        Assert.Equal(PrototypeNativeGloryNewNormals.PossessSpeedId,
            forgotten.StartingPowers!.Single().PowerId);
        var encounter = PrototypeContent.Encounter(
            PrototypeNativeGloryNewNormals.LostAndForgottenEncounterId);
        Assert.Equal((3, 3, 0),
            (encounter.MinAct, encounter.MaxAct, encounter.Weight));
        Assert.Equal(new[] { lost.Id, forgotten.Id }, encounter.EnemyIds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PossessionReturnsOnlyActuallyStolenStatsOnce(bool strength)
    {
        var possession = strength
            ? PrototypeNativeGloryNewNormals.PossessStrengthId
            : PrototypeNativeGloryNewNormals.PossessSpeedId;
        var enemyId = strength
            ? PrototypeNativeGloryNewNormals.TheLostId
            : PrototypeNativeGloryNewNormals.TheForgottenId;
        var statId = strength ? "proto.power.strength" : "proto.power.dexterity";
        var defeated = new EnemyCombatState(1, enemyId, 0, 0, 0,
            new Dictionary<string, int>(),
            Powers: [new(possession, 1, 1, StoredValue: 4)]);
        var combat = new CombatState(1, 3, 0, [], [], [], [],
            [defeated], 1, [],
            [new PrototypePowerInstanceState(statId, -4, 2)], 3, Act: 3);
        var method = typeof(PrototypeGameEngine).GetMethod(
            "ResolveAllyDeathPowers",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var restored = Assert.IsType<CombatState>(
            method!.Invoke(null, [combat, 1]));
        Assert.Equal(0, restored.PlayerPowers
            .Where(p => p.PowerId == statId).Sum(p => p.Stacks));
        Assert.Equal(0, restored.Enemies[0].PowerStates.Single().StoredValue);
        var repeated = Assert.IsType<CombatState>(
            method.Invoke(null, [restored, 1]));
        Assert.Equal(0, repeated.PlayerPowers
            .Where(p => p.PowerId == statId).Sum(p => p.Stacks));
        Assert.Equal(-4, combat.PlayerPowers[0].Stacks);
    }

    [Fact]
    public void ForgottenDreadReadsCurrentDexterity()
    {
        var model = PrototypeContent.Enemy(
            PrototypeNativeGloryNewNormals.TheForgottenId);
        var enemy = new EnemyCombatState(1, model.Id, 100, 0, 0,
            new Dictionary<string, int>(),
            Powers: [new("proto.power.dexterity", 6, 1)]);
        var combat = new CombatState(1, 3, 0, [], [], [], [],
            [enemy], 1, [], [], 2, Act: 3);
        Assert.Equal(19, PrototypeGameEngine.EnemyHitDamage(
            enemy, combat, model.Moves[1].Effects[0], 3, 0));
        Assert.Equal(21, PrototypeGameEngine.EnemyHitDamage(
            enemy, combat, model.Moves[1].Effects[0], 3, 9));
    }
}
