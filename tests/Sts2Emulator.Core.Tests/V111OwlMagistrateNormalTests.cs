using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111OwlMagistrateNormalTests
{
    [Theory]
    [InlineData(0, 231, 16, 4, 33)]
    [InlineData(8, 247, 16, 4, 33)]
    [InlineData(9, 247, 17, 4, 36)]
    public void OwlMoveCycleAndAscensionScaling(
        int asc, int hp, int scrutiny, int peck, int verdict)
    {
        var owl = PrototypeContent.Enemy(PrototypeNativeGloryNewNormals.OwlId);
        Assert.Equal((hp, hp), owl.HpRangeAt(3, asc));
        Assert.Equal(new[] { "scrutiny", "peck_assault",
            "judicial_flight", "verdict" },
            owl.Moves.Select(move => move.Id));
        Assert.Equal(scrutiny, owl.Moves[0].Effects[0].AmountAt(3, asc));
        Assert.Equal(peck, owl.Moves[1].Effects[0].AmountAt(3, asc));
        Assert.Equal(6, owl.Moves[1].Effects[0].Repetitions);
        Assert.Equal(verdict, owl.Moves[3].Effects[0].AmountAt(3, asc));
        Assert.Equal(4, owl.Moves[3].Effects[1].Amount);
        Assert.Equal(PrototypeEnemyEffectKind.RemoveEnemyPower,
            owl.Moves[3].Effects[2].Kind);
        var encounter = PrototypeContent.Encounter(
            PrototypeNativeGloryNewNormals.OwlEncounterId);
        Assert.Equal((3, 3, 0),
            (encounter.MinAct, encounter.MaxAct, encounter.Weight));
        Assert.Equal(new[] { PrototypeNativeGloryNewNormals.OwlId },
            encounter.EnemyIds);
    }

    [Fact]
    public void SoarHalvesPoweredDamageButNotOrdinaryHpLoss()
    {
        var soar = PrototypeContent.Power(PrototypeNativeGloryNewNormals.SoarId);
        Assert.True(soar.DoesNotStack);
        Assert.Equal(1, soar.EnemyIncomingPoweredAttackNumerator);
        Assert.Equal(2, soar.EnemyIncomingPoweredAttackDenominator);

        var enemy = new EnemyCombatState(1,
            PrototypeNativeGloryNewNormals.OwlId, 231, 0, 0,
            new Dictionary<string, int>(),
            Powers: [new(PrototypeNativeGloryNewNormals.SoarId, 1, 1)]);
        var combat = new CombatState(1, 3, 0, [], [], [], [],
            [enemy], 1, [], [], 2, Act: 3);
        var method = typeof(PrototypeGameEngine).GetMethod(
            "DamageEnemyInternal", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        CombatState Hit(CombatState value, bool powered)
        {
            var result = method!.Invoke(null, [value, 1, 21, 0, powered])!;
            return Assert.IsType<CombatState>(
                result.GetType().GetProperty("Combat")!.GetValue(result));
        }

        Assert.Equal(221, Hit(combat, true).Enemies[0].Hp);
        Assert.Equal(210, Hit(combat, false).Enemies[0].Hp);
        Assert.Equal(231, combat.Enemies[0].Hp);
    }
}
