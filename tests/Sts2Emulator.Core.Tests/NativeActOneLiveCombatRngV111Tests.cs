using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeActOneLiveCombatRngV111Tests
{
    private static readonly PrototypeGameEngine Engine = new();

    private static RunState ToMap(string seed, ActIdentity region,
        bool nativeCombat)
    {
        var configuration = RunConfiguration.Create(firstAct: region);
        if (nativeCombat)
            configuration = configuration with
            {
                FidelityId = V111Build.NativeWeakCombatEntryFidelityId
            };
        var state = Engine.Step(
            V111RunFactory.Create(seed, configuration),
            GameAction.Empty("start_run")).State;
        for (var i = 0;
            i < 30 && state.Phase is RunPhase.Event or RunPhase.Reward;
            i++)
            state = Engine.Step(state, Engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        return state;
    }

    private static NativeRngSnapshot Cursor(
        RunState state, string streamId) =>
        NativeRngV111.ToSnapshot(
            state.NativeRunRng!.Streams.Single(s => s.StreamId == streamId));

    [Theory]
    [InlineData(ActIdentity.Overgrowth)]
    [InlineData(ActIdentity.Underdocks)]
    public void LiveWeakEntryUsesPersistentNativeNicheAndAiStreams(
        ActIdentity region)
    {
        var supported = NativeActOneWeakCombatEntryV111.SupportedEncounterIds;
        var implemented = new HashSet<string>(StringComparer.Ordinal);
        var rejected = 0;
        for (var seedIndex = 0; seedIndex < 48; seedIndex++)
        {
            var seed = $"v111-native-live-{region}-{seedIndex}";
            var prototype = ToMap(seed, region, false);
            var experimental = ToMap(seed, region, true);
            var action = Engine.GetLegalActions(prototype)
                .First(a => a.Kind == "choose_map_node");

            Assert.Equal(CanonicalJson.Sha256(prototype.World!.Map),
                CanonicalJson.Sha256(experimental.World!.Map));
            Assert.Equal(CanonicalJson.Sha256(prototype.Rng),
                CanonicalJson.Sha256(experimental.Rng));

            var baseline = Engine.Step(prototype, action).State;
            var encounterId = baseline.World!.EncounterIds.Last();
            var originalHash = CanonicalJson.Sha256(experimental);
            if (!supported.Contains(encounterId, StringComparer.Ordinal))
            {
                Assert.Throws<NotSupportedException>(() =>
                    Engine.Step(experimental, action));
                Assert.Equal(originalHash, CanonicalJson.Sha256(experimental));
                rejected++;
                continue;
            }

            implemented.Add(encounterId);
            var expectedRng = NativeRngV111.CreateBundle(seed);
            var entered = Engine.Step(experimental, action).State;
            Assert.Equal(originalHash, CanonicalJson.Sha256(experimental));
            Assert.Equal(RunPhase.Combat, entered.Phase);
            Assert.Equal(encounterId, entered.World!.EncounterIds.Last());
            var combat = entered.World.Combat!;
            Assert.NotNull(combat.NativeRng);
            Assert.Equal(combat.Enemies.Length,
                combat.NativeRng!.Formation.Enemies.Length);
            Assert.Equal(combat.Enemies.Length,
                combat.NativeRng.Formation.MonsterContexts.Length);
            var expectedEncounterCalls = encounterId switch
            {
                "proto.encounter.slimes_weak" => 3,
                "proto.encounter.corpse_slugs_weak" => 1,
                _ => 0
            };
            Assert.Equal(expectedEncounterCalls,
                NativeRngV111.ToSnapshot(
                    combat.NativeRng.Formation.EncounterRng).Counter);
            if (encounterId == "proto.encounter.corpse_slugs_weak")
            {
                var opener = combat.NativeRng.Formation.EncounterOpeningOffset;
                Assert.InRange(opener!.Value, 0, 2);
                Assert.Equal(opener, Array.IndexOf(
                    PrototypeContent.Encounter(encounterId)
                        .CyclicOpeningAiStateIds!,
                    combat.Enemies[0].AiStateId));
                Assert.Equal((opener.Value + 1) % 3, Array.IndexOf(
                    PrototypeContent.Encounter(encounterId)
                        .CyclicOpeningAiStateIds!,
                    combat.Enemies[1].AiStateId));
            }

            var assignedHp = new HashSet<int>();
            foreach (var enemy in combat.Enemies)
            {
                var range = PrototypeContent.Enemy(enemy.EnemyId)
                    .HpRangeAt(1, entered.Ascension);
                var choices = Enumerable.Range(
                    range.Min, range.Max - range.Min + 1)
                    .Where(hp => !assignedHp.Contains(hp))
                    .ToArray();
                if (choices.Length == 0)
                    choices = Enumerable.Range(range.Min,
                        range.Max - range.Min + 1).ToArray();
                var hp = choices[NativeRngV111.NextInt(
                    expectedRng, "run.niche", choices.Length)];
                Assert.Equal(hp, enemy.Hp);
                Assert.Equal(hp, enemy.MaxHp);
                assignedHp.Add(hp);
            }
            Assert.Equal(
                NativeRngV111.ToSnapshot(expectedRng.Streams.Single(
                    stream => stream.StreamId == "run.niche")),
                Cursor(entered, "run.niche"));

            Assert.All(combat.NativeRng.Formation.MonsterContexts,
                monster => Assert.Equal(0,
                    NativeRngV111.ToSnapshot(monster.Rng).Counter));
            Assert.Equal(Cursor(entered, "run.niche"),
                NativeRngV111.ToSnapshot(
                    combat.NativeRng.RunStreams.Streams.Single(
                        stream => stream.StreamId == "run.niche")));
            NativeRunRngV111.Validate(entered);
            PrototypeStateInvariants.Validate(entered);

            var saved = RunSnapshot.Save(entered);
            var restored = RunSnapshot.Load(saved);
            Assert.Equal(CanonicalJson.Sha256(entered),
                CanonicalJson.Sha256(restored));
            Assert.Equal(CanonicalJson.Sha256(entered),
                CanonicalJson.Sha256(restored.Fork()));

            // Some Neow starts create a pending combat choice (e.g.
            // a card selection) before ending the first turn is legal.
            // Resolve the same public choices on both exact snapshots
            // and then run the actual enemy turn.
            var pending = entered;
            var pendingReplay = restored;
            for (var choiceIndex = 0; choiceIndex < 32; choiceIndex++)
            {
                var legal = Engine.GetLegalActions(pending);
                if (legal.Any(action => action.Kind == "end_turn"))
                    break;
                Assert.NotEmpty(legal);
                var choice = legal[0];
                pending = Engine.Step(pending, choice).State;
                pendingReplay = Engine.Step(pendingReplay, choice).State;
                Assert.Equal(CanonicalJson.Sha256(pending),
                    CanonicalJson.Sha256(pendingReplay));
            }
            var endTurn = Engine.GetLegalActions(pending)
                .Single(action => action.Kind == "end_turn");
            var next = Engine.Step(pending, endTurn).State;
            var replay = Engine.Step(pendingReplay, endTurn).State;
            Assert.Equal(CanonicalJson.Sha256(next),
                CanonicalJson.Sha256(replay));
            NativeRunRngV111.Validate(next);
            Assert.Equal(originalHash, CanonicalJson.Sha256(experimental));
        }

        Assert.NotEmpty(implemented);
        Assert.True(rejected > 0,
            "Unsupported source-specific formations should fail explicitly.");
    }

    [Fact]
    public void OptInRequiresPinnedNativeActOneMapCoordinates()
    {
        var seed = "bad-native-map-coordinate";
        var state = ToMap(seed, ActIdentity.Overgrowth, true);
        var id = NativeActOneWeakCombatEntryV111.SupportedEncounterIds[0];
        var definition = PrototypeContent.Encounter(id);
        var invalidWorld = state.World! with
        {
            ActiveRoom = PrototypeRoomType.Combat,
            Floor = 1,
            Map = state.World.Map with { CurrentNodeId = "broken" }
        };
        Assert.Throws<NotSupportedException>(() =>
            NativeActOneWeakCombatEntryV111.Prepare(
                state, invalidWorld, definition));
        Assert.Throws<InvalidOperationException>(() =>
            NativeActOneWeakCombatEntryV111.Prepare(
                ToMap(seed, ActIdentity.Overgrowth, false),
                invalidWorld, definition));
    }

    [Fact]
    public void OptInRegistryMatchesEightPinnedSourceEncounters()
    {
        var ids = NativeActOneWeakCombatEntryV111.SupportedEncounterIds;
        Assert.Equal(8, ids.Length);
        Assert.All(ids, id =>
        {
            var definition = PrototypeContent.Encounter(id);
            Assert.Equal(PrototypeRoomType.Combat, definition.RoomType);
            Assert.Single(new[] { definition.MinAct });
            Assert.Equal((1, 1, 0),
                (definition.MinAct, definition.MaxAct, definition.Weight));
            if (id == "proto.encounter.slimes_weak")
            {
                Assert.Empty(definition.FixedEnemySpecs);
                Assert.NotNull(definition.SelectionGroups);
            }
            else
            {
                Assert.NotEmpty(definition.FixedEnemySpecs);
                Assert.Null(definition.SelectionGroups);
            }
            if (id == "proto.encounter.corpse_slugs_weak")
                Assert.Equal(3, definition.CyclicOpeningAiStateIds!.Length);
            else
                Assert.Null(definition.CyclicOpeningAiStateIds);
        });
        Assert.Contains("proto.encounter.slimes_weak", ids);
        Assert.Contains("proto.encounter.corpse_slugs_weak", ids);
    }
}
