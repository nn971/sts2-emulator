using Sts2Emulator.Core;
using Sts2ReferenceMod.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeInitialHpRollEvidenceV111Tests
{
    [Fact]
    public void SourcePoolAndSelectedHpPreserveActualNativeRngCursor()
    {
        var streams = NativeRngV111.CreateBundle("native-initial-hp-observer");
        var before = NativeRngV111.ToSnapshot(
            streams.Streams.Single(x => x.StreamId == "run.niche"));
        var choice = new NativeRandomIndexProviderV111().Resolve(
            new RandomIndexRequestV111(
                "combat.initial_unique_hp", "run.niche",
                "monster_creation:1", 0, [41, 42, 44]),
            streams);
        var evidence = new NativeInitialHpRollEvidenceV111(
            1, "SludgeSpinner", [41, 42, 44],
            choice.SelectedIndex, choice.SelectedValue,
            false, choice.NativeCursorBefore!, choice.NativeCursorAfter!);
        evidence.Validate();
        Assert.Equal(before, evidence.CursorBefore);
        Assert.Equal(3, evidence.OrderedEligibleHp.Length);
    }

    [Fact]
    public void WrongHpAndDuplicateSourcePoolFail()
    {
        var streams = NativeRngV111.CreateBundle("native-hp-invalid");
        var choice = new NativeRandomIndexProviderV111().Resolve(
            new RandomIndexRequestV111(
                "combat.initial_unique_hp", "run.niche",
                "monster_creation:2", 0, [25, 26]),
            streams);
        var valid = new NativeInitialHpRollEvidenceV111(
            2, "SludgeSpinner", [25, 26],
            choice.SelectedIndex, choice.SelectedValue,
            false, choice.NativeCursorBefore!, choice.NativeCursorAfter!);
        valid.Validate();
        Assert.Throws<ArgumentException>(() =>
            (valid with { SelectedHp = 999 }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (valid with { OrderedEligibleHp = [25, 25] }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (valid with { CursorAfter = valid.CursorBefore }).Validate());
    }
}
