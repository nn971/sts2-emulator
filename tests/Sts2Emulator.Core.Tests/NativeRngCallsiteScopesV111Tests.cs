using Sts2ReferenceMod.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeRngCallsiteScopesV111Tests
{
    [Fact]
    public void NativeMonsterMoveScopeAttributesOnlyItsMonsterAiCalls()
    {
        Assert.Null(NativeRngCallsiteScopesV111.NextFor("run.monster_ai"));
        using (NativeRngCallsiteScopesV111.Enter(
            "combat.committed_intent.index_draw", "run.monster_ai", "creature:7"))
        {
            Assert.Null(NativeRngCallsiteScopesV111.NextFor("run.niche"));
            var first = NativeRngCallsiteScopesV111.NextFor("run.monster_ai");
            Assert.True(first.HasValue);
            Assert.Equal("combat.committed_intent.index_draw", first.Value.SiteId);
            Assert.Equal("creature:7", first.Value.OwnerId);
            Assert.Equal(0, first.Value.CallOrdinal);
            Assert.Equal(1,
                NativeRngCallsiteScopesV111.NextFor("run.monster_ai")!.Value.CallOrdinal);
        }
        Assert.Null(NativeRngCallsiteScopesV111.NextFor("run.monster_ai"));
    }

    [Fact]
    public void NestedSourceScopeRestoresParentAndDoesNotMergeLocalOrdinals()
    {
        using (NativeRngCallsiteScopesV111.Enter("outer", "run.niche", "creature:1"))
        {
            Assert.Equal(0, NativeRngCallsiteScopesV111.NextFor("run.niche")!.Value.CallOrdinal);
            using (NativeRngCallsiteScopesV111.Enter(
                "intent", "run.monster_ai", "creature:2"))
            {
                Assert.Null(NativeRngCallsiteScopesV111.NextFor("run.niche"));
                Assert.Equal(0, NativeRngCallsiteScopesV111.NextFor(
                    "run.monster_ai")!.Value.CallOrdinal);
            }
            Assert.Equal(1, NativeRngCallsiteScopesV111.NextFor("run.niche")!.Value.CallOrdinal);
        }
    }

    [Fact]
    public void ScalarOwnerPeekDoesNotConsumeIndexDrawOrdinal()
    {
        Assert.Null(NativeRngCallsiteScopesV111.PeekOwnerFor("run.monster_ai"));
        using (NativeRngCallsiteScopesV111.Enter(
            "combat.committed_intent.index_draw", "run.monster_ai", "creature:9"))
        {
            Assert.Null(NativeRngCallsiteScopesV111.PeekOwnerFor("run.niche"));
            Assert.Equal("creature:9",
                NativeRngCallsiteScopesV111.PeekOwnerFor("run.monster_ai"));
            Assert.Equal("creature:9",
                NativeRngCallsiteScopesV111.PeekOwnerFor("run.monster_ai"));
            Assert.Equal(0, NativeRngCallsiteScopesV111.NextFor(
                "run.monster_ai")!.Value.CallOrdinal);
        }
    }

    [Fact]
    public void ScopeRequiresStrictLifoDisposal()
    {
        var parent = NativeRngCallsiteScopesV111.Enter("outer", "run.niche", "run");
        var child = NativeRngCallsiteScopesV111.Enter("inner", "run.monster_ai", "creature:1");
        try
        {
            Assert.Throws<InvalidOperationException>(() => parent.Dispose());
        }
        finally
        {
            child.Dispose();
            parent.Dispose();
        }
        Assert.Null(NativeRngCallsiteScopesV111.NextFor("run.niche"));
    }
}
