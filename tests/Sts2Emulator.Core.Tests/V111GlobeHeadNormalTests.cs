using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111GlobeHeadNormalTests
{
    [Theory]
    [InlineData(0, 148, 13, 6, 16, 6)]
    [InlineData(8, 158, 13, 6, 16, 6)]
    [InlineData(9, 158, 14, 7, 17, 8)]
    public void GloryGlobeSourceScalingAndMoveCycle(
        int asc, int hp, int slap, int thunder, int burst, int galvanic)
    {
        var model = PrototypeContent.Enemy(
            PrototypeNativeGloryNewNormals.GlobeHeadId);
        Assert.Equal((hp, hp), model.HpRangeAt(3, asc));
        Assert.Equal(new[] {"shocking_slap", "thunder_strike",
            "galvanic_burst"},
            model.Moves.Select(move => move.Id));
        Assert.Equal(slap, model.Moves[0].Effects[0].AmountAt(3, asc));
        Assert.Equal(2, model.Moves[0].Effects[1].Amount);
        Assert.Equal(thunder, model.Moves[1].Effects[0].AmountAt(3, asc));
        Assert.Equal(3, model.Moves[1].Effects[0].Repetitions);
        Assert.Equal(burst, model.Moves[2].Effects[0].AmountAt(3, asc));
        Assert.Equal(2, model.Moves[2].Effects[1].Amount);
        Assert.Equal(galvanic, model.StartingPowers!.Single().StacksAt(asc));
        Assert.Equal(1, PrototypeContent.Power(
            PrototypeNativeGloryNewNormals.GalvanicId)
            .GalvanizePowerCardsPerStack);
        var encounter = PrototypeContent.Encounter(
            PrototypeNativeGloryNewNormals.GlobeHeadEncounterId);
        Assert.Equal((3, 3, 0),
            (encounter.MinAct, encounter.MaxAct, encounter.Weight));
    }
}
