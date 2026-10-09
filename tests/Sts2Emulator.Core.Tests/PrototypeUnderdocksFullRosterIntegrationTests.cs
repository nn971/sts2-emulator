using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Exercises the regional selector and every registered formation through
/// the public engine, rather than only looking up static enemy definitions.
/// Synthetic one-node maps isolate combat entry; this is not a live oracle.
/// </summary>
public sealed class PrototypeUnderdocksFullRosterIntegrationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(9)]
    public void WeakNormalAndEliteBagsEnterEverySupportedFormation(int ascension)
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeNativeUnderdocksRunFactory.Create(
            "underdocks-all-bags-" + ascension, ascension);

        var weak = new HashSet<string>(StringComparer.Ordinal);
        for (var floor = 1; floor <= 3; floor++)
        {
            state = StartRoom(engine, state, PrototypeRoomType.Combat, floor);
            var encounterId = state.World!.EncounterIds[^1];
            Assert.True(weak.Add(encounterId));
            Assert.Contains(encounterId,
                PrototypeNativeUnderdocks.SupportedWeakEncounterIds);
            AssertFormation(state, encounterId, ascension);
        }
        Assert.Equal(3, weak.Count);

        var normals = new HashSet<string>(StringComparer.Ordinal);
        for (var floor = 4; floor <= 13; floor++)
        {
            state = StartRoom(engine, state, PrototypeRoomType.Combat, floor);
            var encounterId = state.World!.EncounterIds[^1];
            Assert.True(normals.Add(encounterId));
            Assert.Contains(encounterId,
                PrototypeNativeUnderdocks.SupportedNormalEncounterIds);
            AssertFormation(state, encounterId, ascension);
        }
        Assert.Equal(
            PrototypeNativeUnderdocks.SupportedNormalEncounterIds
                .OrderBy(id => id, StringComparer.Ordinal),
            normals.OrderBy(id => id, StringComparer.Ordinal));
        Assert.Empty(state.World!.ActOneEncounterPool!
            .RemainingNormalEncounterIds!);

        var elites = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 3; i++)
        {
            state = StartRoom(engine, state, PrototypeRoomType.Elite, 14);
            var encounterId = state.World!.EncounterIds[^1];
            Assert.True(elites.Add(encounterId));
            Assert.Contains(encounterId,
                PrototypeNativeUnderdocks.SupportedEliteEncounterIds);
            AssertFormation(state, encounterId, ascension);
        }
        Assert.Equal(
            PrototypeNativeUnderdocks.SupportedEliteEncounterIds
                .OrderBy(id => id, StringComparer.Ordinal),
            elites.OrderBy(id => id, StringComparer.Ordinal));
        Assert.Empty(state.World!.ActOneEncounterPool!
            .RemainingEliteEncounterIds!);
        var hash = CanonicalJson.Sha256(state);
        Assert.Throws<NotSupportedException>(() =>
            StartRoom(engine, state, PrototypeRoomType.Elite, 15));
        Assert.Equal(hash, CanonicalJson.Sha256(state));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(9)]
    public void EachSelectedBossCanEnterCombatWithoutSubstitution(int ascension)
    {
        var engine = new PrototypeGameEngine();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 90; i++)
        {
            var state = PrototypeNativeUnderdocksRunFactory.Create(
                "underdocks-boss-cover-" + ascension + "-" + i,
                ascension);
            var selected = state.World!.ActOneEncounterPool!.BossEncounterId!;
            Assert.Contains(selected,
                PrototypeNativeUnderdocks.SupportedBossEncounterIds);
            if (!seen.Add(selected))
            {
                continue;
            }

            var entered = StartRoom(engine, state, PrototypeRoomType.Boss, 16);
            Assert.Equal(selected, entered.World!.EncounterIds[^1]);
            AssertFormation(entered, selected, ascension);
            Assert.Equal(CanonicalJson.Sha256(entered),
                CanonicalJson.Sha256(entered.Fork()));
            if (seen.Count ==
                PrototypeNativeUnderdocks.SupportedBossEncounterIds.Length)
            {
                break;
            }
        }

        Assert.Equal(
            PrototypeNativeUnderdocks.SupportedBossEncounterIds
                .OrderBy(id => id, StringComparer.Ordinal),
            seen.OrderBy(id => id, StringComparer.Ordinal));
    }

    private static void AssertFormation(
        RunState state, string encounterId, int ascension)
    {
        var encounter = PrototypeContent.Encounter(encounterId);
        var combat = state.World!.Combat!;
        Assert.Equal(encounter.FixedEnemySpecs.Length, combat.Enemies.Length);
        Assert.All(combat.Enemies, enemy =>
        {
            var definition = PrototypeContent.Enemy(enemy.EnemyId);
            var range = definition.HpRangeAt(1, ascension);
            Assert.InRange(enemy.Hp, range.Min, range.Max);
            Assert.True(enemy.InstanceId > 0);
            Assert.True(enemy.Hp > 0);
            Assert.Contains(enemy.EnemyId, encounter.EnemyIds);
            Assert.Equal(definition.StartingPowers?.Length ?? 0,
                enemy.PowerStates.Length);
        });
        Assert.Equal(
            combat.Enemies.Length,
            combat.Enemies.Select(enemy => enemy.InstanceId)
                .Distinct().Count());
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    private static RunState StartRoom(
        PrototypeGameEngine engine,
        RunState state,
        PrototypeRoomType kind,
        int floor)
    {
        var node = new MapNodeState(
            "roster-integration:" + floor + ":" + kind,
            1, floor, kind, []);
        state = state with
        {
            Phase = RunPhase.MapChoice,
            World = state.World! with
            {
                Floor = floor - 1,
                ActiveRoom = null,
                Map = new MapState(
                    [node], CurrentNodeId: null,
                    EntryNodeIds: [node.NodeId],
                    GenerationProfileId:
                        PrototypeNativeUnderdocks.GenerationProfileId),
                Combat = null,
                Reward = null,
                Shop = null,
                Event = null
            }
        };
        return engine.Step(state,
            Assert.Single(engine.GetLegalActions(state))).State;
    }
}
