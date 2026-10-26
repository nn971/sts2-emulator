using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>Source-independent comparison of the engine's authoritative
/// mechanical projection, regardless of the RNG realization.</summary>
public sealed class StochasticMechanicsProjectionAcceptanceTests
{
    [Fact]
    public void ProviderPrivateRngChangesDoNotChangeMechanicalComparison()
    {
        var original = PrototypeGameFactory.Create(
            "source-independent-stochastic-mechanics", 0);
        original.Validate();
        var differentProvider = original.Fork() with
        {
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                original.Stochastic,
                PrototypeRng.CreateBundle("independent-replay-provider"))
        };
        differentProvider.Validate();

        var mechanics = SimulationMechanicsProjectionEngineV111.Compare(
            original, differentProvider);
        Assert.True(mechanics.Equivalent);
        Assert.Empty(mechanics.Differences);
        Assert.Equal(mechanics.LeftFingerprint, mechanics.RightFingerprint);

        // Comparing mechanical differences must remain stricter than merely
        // comparing stochastic trace or player action shapes.
        var changedGold = differentProvider with
        {
            Player = differentProvider.Player with
            {
                Gold = differentProvider.Player.Gold + 1
            }
        };
        changedGold.Validate();
        var changed = SimulationMechanicsProjectionEngineV111.Compare(
            original, changedGold);
        Assert.False(changed.Equivalent);
        Assert.NotEmpty(changed.Differences);
    }
}
