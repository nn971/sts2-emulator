using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class SemanticOtherChanceV111Tests
{
    private static RandomIndexRequestV111 Cost(
        string site = "combat.card_draw.relic_energy_cost", int ordinal = 0,
        int support = 4) => new(
            site, "semantic.combat", "card_instance:3:model:proto.silent.strike:stage:relic",
            ordinal, [], OperationKind: "bounded_integer",
            BoundedCount: support);

    [Fact]
    public void NamedStreamConsumesExactlyOneNativeBoundedCallPerCost()
    {
        const string seed = "semantic-native-cost-provider-143";
        var native = NativeRngV111.CreateBundle(seed);
        var expected = native.Fork();
        var privateState = SemanticOtherChanceEngineV111.Begin(native);
        var before = CanonicalJson.Sha256(privateState);
        var untouchedOther = NativeRngV111.ToSnapshot(
            privateState.Streams.Streams.Single(x => x.StreamId == "run.shuffle"));
        var wanted = NativeRngV111.NextInt(expected,
            SemanticOtherChanceEngineV111.CostStreamId, 4);
        var (next, decision) = SemanticOtherChanceEngineV111.ResolveDrawCost(
            privateState, Cost());
        Assert.Equal(wanted, decision.SelectedIndex);
        Assert.Equal("native-v111", decision.ProviderMode);
        Assert.Equal(SemanticOtherChanceEngineV111.CostStreamId,
            decision.Request.StreamId);
        Assert.Equal(NativeRngV111.ToSnapshot(
                native.Streams.Single(x => x.StreamId ==
                    SemanticOtherChanceEngineV111.CostStreamId)),
            decision.NativeCursorBefore);
        Assert.Equal(NativeRngV111.ToSnapshot(
                expected.Streams.Single(x => x.StreamId ==
                    SemanticOtherChanceEngineV111.CostStreamId)),
            decision.NativeCursorAfter);
        Assert.Single(next.Decisions);
        Assert.Equal(untouchedOther, NativeRngV111.ToSnapshot(
            next.Streams.Streams.Single(x => x.StreamId == "run.shuffle")));
        Assert.Equal(before, CanonicalJson.Sha256(privateState));
        Assert.Equal(CanonicalJson.Sha256(next), CanonicalJson.Sha256(next.Fork()));
    }

    [Fact]
    public void UnsupportedSiteAndWrongStreamFailWithoutChangingPrivateRng()
    {
        var state = SemanticOtherChanceEngineV111.Begin(
            NativeRngV111.CreateBundle("semantic-cost-unsupported"));
        var hash = CanonicalJson.Sha256(state);
        Assert.Throws<ArgumentException>(() =>
            SemanticOtherChanceEngineV111.ResolveDrawCost(state,
                Cost("combat.random_target")));
        Assert.Throws<NotSupportedException>(() =>
            SemanticOtherChanceEngineV111.ResolveDrawCost(state,
                Cost() with { StreamId = "run.shuffle" }));
        Assert.Equal(hash, CanonicalJson.Sha256(state));
    }
}
