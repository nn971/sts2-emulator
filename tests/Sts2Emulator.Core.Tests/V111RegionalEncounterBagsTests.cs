using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111RegionalEncounterBagsTests
{
    [Theory]
    [InlineData(2, 4, 10, 3, 3, 16)]
    [InlineData(3, 3, 9, 3, 3, 13)]
    public void RegionalPoolsCoverEveryCataloguedEncounterWithoutEventCombats(
        int act, int weakCount, int normalCount, int eliteCount,
        int bossCount, int eventCount)
    {
        var weak = ExperimentalSilentThreeActRouteV111.Pool(
            act, PrototypeRoomType.Combat, true);
        var normal = ExperimentalSilentThreeActRouteV111.Pool(
            act, PrototypeRoomType.Combat, false);
        var elite = ExperimentalSilentThreeActRouteV111.Pool(
            act, PrototypeRoomType.Elite, false);
        var boss = ExperimentalSilentThreeActRouteV111.Pool(
            act, PrototypeRoomType.Boss, false);
        Assert.Equal(weakCount, weak.Length);
        Assert.Equal(normalCount, normal.Length);
        Assert.Equal(eliteCount, elite.Length);
        Assert.Equal(bossCount, boss.Length);
        var all = weak.Concat(normal).Concat(elite).Concat(boss).ToArray();
        Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(act == 2 ? 20 : 18, all.Length);
        foreach (var id in all)
        {
            var definition = PrototypeContent.Encounter(id);
            Assert.Equal(act, definition.MinAct);
            Assert.Equal(act, definition.MaxAct);
            Assert.True(ExperimentalSilentThreeActRouteV111.ContainsEncounter(id, act));
            Assert.False(ExperimentalSilentThreeActRouteV111.ContainsEncounter(id, act == 2 ? 3 : 2));
        }
        var events = act == 2
            ? ExperimentalSilentThreeActRouteV111.HiveEvents
            : ExperimentalSilentThreeActRouteV111.GloryEvents;
        Assert.Equal(eventCount, events.Length);
        Assert.Equal(events.Length, events.Distinct(StringComparer.Ordinal).Count());
        foreach (var id in events)
        {
            var definition = PrototypeContent.Event(id);
            Assert.True(ExperimentalSilentThreeActRouteV111.ContainsEvent(id, act));
            Assert.InRange(act, definition.MinAct, definition.MaxAct);
        }
        Assert.All(all, id =>
            Assert.DoesNotContain("event_encounter", id, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void RegionalBagsSelectWithoutReplacementAndForkTheirArrays(int act)
    {
        var seed = "regional-bags-" + act;
        var engine = new PrototypeGameEngine();
        var configuration = RunConfiguration.Create() with
        {
            FidelityId = V111Build.ExperimentalSilentThreeActRouteFidelityId
        };
        var state = engine.Step(
            V111RunFactory.Create(seed, configuration),
            GameAction.Empty("start_run")).State;
        var rng = state.Rng.Fork();
        var initial = ExperimentalSilentThreeActRouteV111.InitialBag(act, rng);
        var world = state.World! with
        {
            Act = act,
            Floor = 1,
            LaterActEncounterBag = initial
        };
        var first = ExperimentalSilentThreeActRouteV111.SelectEncounter(
            world, PrototypeRoomType.Combat, rng);
        Assert.Contains(first.Encounter.Id,
            ExperimentalSilentThreeActRouteV111.Pool(
                act, PrototypeRoomType.Combat, true));
        Assert.Equal(initial.RemainingWeak.Length - 1,
            first.Bag.RemainingWeak.Length);
        Assert.Equal(1, first.Bag.OrdinaryCombatsStarted);
        Assert.Equal(initial.RemainingWeak.Length,
            initial.Fork().RemainingWeak.Length);
        var copy = first.Bag.Fork();
        var changedWeak = copy.RemainingWeak;
        changedWeak[0] = "tampered";
        copy = copy with { RemainingWeak = changedWeak };
        Assert.Contains("tampered", copy.RemainingWeak);
        Assert.DoesNotContain("tampered", first.Bag.RemainingWeak);

        var bag = first.Bag;
        var visited = new List<string> { first.Encounter.Id };
        for (var i = 1; i < 2; i++)
        {
            var next = ExperimentalSilentThreeActRouteV111.SelectEncounter(
                world with { LaterActEncounterBag = bag },
                PrototypeRoomType.Combat, rng);
            visited.Add(next.Encounter.Id);
            bag = next.Bag;
        }
        Assert.Equal(2, visited.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, bag.OrdinaryCombatsStarted);
        var normal = ExperimentalSilentThreeActRouteV111.SelectEncounter(
            world with { LaterActEncounterBag = bag },
            PrototypeRoomType.Combat, rng);
        Assert.Contains(normal.Encounter.Id,
            ExperimentalSilentThreeActRouteV111.Pool(
                act, PrototypeRoomType.Combat, false));

        var elite = ExperimentalSilentThreeActRouteV111.SelectEncounter(
            world with { LaterActEncounterBag = bag },
            PrototypeRoomType.Elite, rng);
        Assert.Contains(elite.Encounter.Id,
            ExperimentalSilentThreeActRouteV111.Pool(
                act, PrototypeRoomType.Elite, false));
        Assert.Equal(bag.OrdinaryCombatsStarted,
            elite.Bag.OrdinaryCombatsStarted);

        var boss = ExperimentalSilentThreeActRouteV111.SelectEncounter(
            world with { LaterActEncounterBag = bag },
            PrototypeRoomType.Boss, rng);
        Assert.Equal(initial.BossEncounterId, boss.Encounter.Id);
        Assert.NotSame(bag, boss.Bag);
        Assert.Equal(bag.RemainingWeak, boss.Bag.RemainingWeak);
        Assert.Equal(bag.RemainingNormal, boss.Bag.RemainingNormal);
        Assert.Equal(bag.RemainingElite, boss.Bag.RemainingElite);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void RegionalPoolSelectionIsDeterministic(int act)
    {
        var left = PrototypeRng.CreateBundle("regional-fork-" + act);
        var right = left.Fork();
        var initial = ExperimentalSilentThreeActRouteV111.InitialBag(act, left);
        var counterpart = ExperimentalSilentThreeActRouteV111.InitialBag(act, right);
        Assert.Equal(initial.BossEncounterId, counterpart.BossEncounterId);
        Assert.Equal(initial.RemainingWeak, counterpart.RemainingWeak);
        Assert.Equal(PrototypeRng.NextInt(left, "combat", 100000),
            PrototypeRng.NextInt(right, "combat", 100000));
    }

    [Fact]
    public void UnderdocksActOneRegionalRegistrationsAreComplete()
    {
        Assert.Equal(4, PrototypeNativeUnderdocks.SupportedWeakEncounterIds.Length);
        Assert.Equal(10, PrototypeNativeUnderdocks.SupportedNormalEncounterIds.Length);
        Assert.Equal(3, PrototypeNativeUnderdocks.SupportedEliteEncounterIds.Length);
        Assert.Equal(3, PrototypeNativeUnderdocks.SupportedBossEncounterIds.Length);
        Assert.Equal(13, PrototypeNativeUnderdocksEvents.SupportedRegionEventIds.Length);
        Assert.Equal(
            PrototypeNativeUnderdocks.NativeWeakEncounterIds.OrderBy(x => x),
            PrototypeNativeUnderdocks.SupportedWeakEncounterIds.OrderBy(x => x));
        Assert.Equal(
            PrototypeNativeUnderdocks.NativeNormalEncounterIds.OrderBy(x => x),
            PrototypeNativeUnderdocks.SupportedNormalEncounterIds.OrderBy(x => x));
        Assert.Equal(
            PrototypeNativeUnderdocks.NativeEliteEncounterIds.OrderBy(x => x),
            PrototypeNativeUnderdocks.SupportedEliteEncounterIds.OrderBy(x => x));
        Assert.Equal(
            PrototypeNativeUnderdocks.NativeBossEncounterIds.OrderBy(x => x),
            PrototypeNativeUnderdocks.SupportedBossEncounterIds.OrderBy(x => x));
        Assert.Equal(
            PrototypeNativeUnderdocksEvents.NativeRegionEventIds.OrderBy(x => x),
            PrototypeNativeUnderdocksEvents.SupportedRegionEventIds.OrderBy(x => x));
    }
}
