using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Source-independent regression coverage for the isolated fixture/ordinary
/// mechanics seam; does not test installed-game parity or source RNG cursors.
/// </summary>
public sealed class StochasticAcceptanceFixtureTests
{
    [Fact]
    public void SeededHiveFixturePreservesGlobalSubscriberOrdering()
    {
        // Each Exoskeleton can initially carry a modeled Power with the same
        // local application order. The composed combat requires distinct
        // combat-wide subscriber orders before its first player decision.
        var seeded = new SemanticSeededEntryV111(
            "stochastic-subscriber-order-acceptance", 2,
            PrototypeNativeHiveNormals.ExoskeletonsNormalId,
            "ExoskeletonsNormal", 20, 0, 1, 3, 5);
        var state = SemanticPilotCombatEngineV111.BeginSeeded(seeded);
        state.Run.Validate();
        var combat = state.Run.World?.Combat
            ?? throw new InvalidOperationException("Fixture lost its combat.");
        var orders = combat.RelicStates.Select(relic => relic.ApplicationOrder)
            .Concat(combat.PlayerPowers.Select(power => power.ApplicationOrder))
            .Concat(combat.Enemies.SelectMany(enemy => enemy.PowerStates)
                .Select(power => power.ApplicationOrder))
            .ToArray();
        Assert.Equal(orders.Length, orders.Distinct().Count());
        Assert.All(orders, order =>
        {
            Assert.True(order > 0);
            Assert.True(order < combat.NextPowerApplicationOrder);
        });
    }
}
