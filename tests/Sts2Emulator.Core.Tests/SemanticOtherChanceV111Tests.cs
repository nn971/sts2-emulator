using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class SemanticOtherChanceV111Tests
{
    private static RandomIndexRequestV111 Cost(string site, int bound, int ordinal) =>
        new(site, "semantic.combat", "card_instance:11:model:proto.silent.strike:stage:relic",
            ordinal, [], OperationKind: "bounded_integer", BoundedCount: bound);

    [Fact]
    public void UsesDedicatedSeededStreamAndPersistsCursorAcrossCosts()
    {
        const string seed = "semantic-cost-native-codec-p0";
        var source = SemanticOtherChanceEngineV111.Begin(seed);
        var unchanged = source.Fork();
        var (first, index) = SemanticOtherChanceEngineV111.ResolveCost(
            source, Cost("combat.card_draw.relic_energy_cost", 4, 0));
        var (second, secondIndex) = SemanticOtherChanceEngineV111.ResolveCost(
            first, Cost("combat.card_draw.slither_energy_cost", 4, 1));
        var expected = NativeRngV111.CreateBundle(seed);
        Assert.Equal(NativeRngV111.NextInt(expected,
            SemanticOtherChanceV111.CostStream, 4), index);
        Assert.Equal(NativeRngV111.NextInt(expected,
            SemanticOtherChanceV111.CostStream, 4), secondIndex);
        Assert.Equal(CanonicalJson.Sha256(expected), CanonicalJson.Sha256(second.Streams));
        Assert.Equal(CanonicalJson.Sha256(source), CanonicalJson.Sha256(unchanged));
        Assert.Equal(2, second.Transcript.Length);
        Assert.All(second.Transcript, decision =>
        {
            Assert.Equal(SemanticOtherChanceV111.ProviderId, decision.ProviderMode);
            Assert.Equal("UNVERIFIED", decision.ProbabilityStatus);
            Assert.NotNull(decision.NativeCursorBefore);
            Assert.NotNull(decision.NativeCursorAfter);
        });
        Assert.Equal(0, second.Transcript[0].NativeCursorBefore!.Counter);
        Assert.Equal(1, second.Transcript[0].NativeCursorAfter!.Counter);
        Assert.Equal(1, second.Transcript[1].NativeCursorBefore!.Counter);
        Assert.Equal(2, second.Transcript[1].NativeCursorAfter!.Counter);
    }

    [Fact]
    public void SingletonCostConsumesAnRngStepAndUnsupportedSiteFailsClosed()
    {
        var state = SemanticOtherChanceEngineV111.Begin("semantic-cost-singleton");
        var (result, index) = SemanticOtherChanceEngineV111.ResolveCost(
            state, Cost("combat.card_draw.relic_energy_cost", 1, 0));
        Assert.Equal(0, index);
        Assert.Equal(1, result.Transcript[0].NativeCursorAfter!.Counter);
        Assert.Throws<NotSupportedException>(() =>
            SemanticOtherChanceEngineV111.ResolveCost(result,
                Cost("combat.unknown_cost", 4, 1)));
        Assert.Equal(1, result.Transcript.Length);
    }
}
