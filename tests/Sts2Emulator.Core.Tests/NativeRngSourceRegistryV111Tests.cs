using Sts2ReferenceMod.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeRngSourceRegistryV111Tests
{
    [Fact]
    public void PersistentRngRegistrationUsesReferenceIdentityAndZeroBasedLocalOrdinal()
    {
        var owners = new NativeRngSourceRegistryV111();
        object niche = new();
        object ai = new();
        owners.Register(niche, "run.niche", "run");
        owners.Register(ai, "run.monster_ai", "run");
        owners.Register(niche, "run.niche", "run");
        Assert.Equal(2, owners.RegisteredOwners);

        var first = owners.Next(niche);
        Assert.Equal("run.niche", first.StreamId);
        Assert.Equal("run", first.OwnerId);
        Assert.Equal(0, first.CallOrdinal);
        Assert.Equal(1, owners.Next(niche).CallOrdinal);
        Assert.Equal(0, owners.Next(ai).CallOrdinal);
        Assert.Equal(2, owners.Next(niche).CallOrdinal);
    }

    [Fact]
    public void DuplicateObjectWithConflictingOwnerFailsClosed()
    {
        var owners = new NativeRngSourceRegistryV111();
        object original = new();
        owners.Register(original, "run.niche", "run");
        Assert.Throws<InvalidOperationException>(() =>
            owners.Register(original, "player.rewards", "player:0"));
        Assert.Equal("run.niche", owners.Next(original).StreamId);
    }

    [Fact]
    public void UnknownAndPerCombatLifecycleRemainExplicit()
    {
        var owners = new NativeRngSourceRegistryV111();
        object rng = new();
        var unknown = owners.Next(rng);
        Assert.Equal("UNATTRIBUTED", unknown.StreamId);
        Assert.Equal("UNATTRIBUTED", unknown.OwnerId);
        owners.Register(rng, "run.shuffle", "run");
        Assert.Equal(0, owners.Next(rng).CallOrdinal);
        owners.Clear();
        Assert.Equal(0, owners.RegisteredOwners);
        Assert.Equal("UNATTRIBUTED", owners.Next(rng).StreamId);
        owners.Register(rng, "run.shuffle", "run");
        Assert.Equal(0, owners.Next(rng).CallOrdinal);
    }
}
