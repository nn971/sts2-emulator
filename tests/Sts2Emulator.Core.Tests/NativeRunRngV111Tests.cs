using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeRunRngV111Tests
{
    private static NativeRngSnapshot Cursor(RunState state, string name) =>
        NativeRngV111.ToSnapshot(state.NativeRunRng!.Streams.Single(
            stream => stream.StreamId == name));

    private static RunState Initialized(string seed)
    {
        var engine = new PrototypeGameEngine();
        var state = engine.Step(V111RunFactory.Create(seed),
            GameAction.Empty("start_run")).State;
        for (var decision = 0;
             decision < 30 && state.Phase is RunPhase.Event or RunPhase.Reward;
             decision++)
            state = engine.Step(state, engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        return state;
    }

    private static RunState EnterNativeOwnedCombat(string seed)
    {
        var engine = new PrototypeGameEngine();
        var state = Initialized(seed);
        var before = state.NativeRunRng!;
        var first = engine.GetLegalActions(state)
            .First(action => action.Kind == "choose_map_node");
        state = engine.Step(state, first).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        Assert.Equal(
            CanonicalJson.Sha256(before),
            CanonicalJson.Sha256(state.NativeRunRng));
        var enemies = state.World!.Combat!.Enemies;
        var specs = enemies.Select(enemy =>
            new PrototypeEncounterEnemySpec(
                enemy.EnemyId, enemy.FormationPosition,
                enemy.SlotName)).ToArray();
        var definition = new PrototypeEncounterDefinition(
            "test.captured.encounter", PrototypeRoomType.Combat,
            specs.Select(spec => spec.EnemyId).ToArray(),
            Formation: specs);
        var formation = NativeEncounterFormationFactoryV111.Generate(
            seed, definition, "CapturedSourceEncounter", state.World.Floor,
            currentActIndex: 0, firstMonsterCombatId: 1);
        return NativeRunRngV111.BeginEncounter(state, formation);
    }

    [Theory]
    [InlineData("native-run-initial-a")]
    [InlineData("native-run-initial-b")]
    public void ConfiguredRunCarriesAllNativeStreamsAcrossInitializationAndMap(
        string seed)
    {
        var factory = V111RunFactory.Create(seed);
        Assert.NotNull(factory.NativeRunRng);
        NativeRunRngV111.Validate(factory);
        Assert.Equal(15, factory.NativeRunRng!.Streams.Length);
        Assert.All(factory.NativeRunRng.Streams,
            stream => Assert.Equal(0,
                NativeRngV111.ToSnapshot(stream).Counter));
        var engine = new PrototypeGameEngine();
        var map = engine.Step(factory, GameAction.Empty("start_run")).State;
        var afterInitialization = CanonicalJson.Sha256(map.NativeRunRng);
        for (var decision = 0;
             decision < 30 && map.Phase is RunPhase.Event or RunPhase.Reward;
             decision++)
            map = engine.Step(map, engine.GetLegalActions(map)[0]).State;
        Assert.Equal(RunPhase.MapChoice, map.Phase);
        Assert.Equal(afterInitialization,
            CanonicalJson.Sha256(map.NativeRunRng));
        Assert.NotEqual(afterInitialization,
            CanonicalJson.Sha256(factory.NativeRunRng));
        PrototypeStateInvariants.Validate(map);
        var action = engine.GetLegalActions(map).First(a =>
            a.Kind == "choose_map_node");
        var combat = engine.Step(map, action).State;
        Assert.Equal(afterInitialization,
            CanonicalJson.Sha256(combat.NativeRunRng));
        Assert.Equal(RunPhase.Combat, combat.Phase);
        Assert.Null(combat.World!.Combat!.NativeRng);
        NativeRunRngV111.Validate(combat);
    }

    [Fact]
    public void RunStreamSamplesPersistThroughMapChoiceAndNativeHpEntry()
    {
        const string seed = "native-persistent-hp-no-reset";
        var fresh = Initialized(seed);
        var originalHash = CanonicalJson.Sha256(fresh);
        var seeded = NativeRunRngV111.SampleNextInt(
            fresh, "run.niche", 13, out var first);
        var reference = NativeRngV111.CreateBundle(seed);
        Assert.Equal(NativeRngV111.NextInt(
            reference, "run.niche", 13), first);
        Assert.Equal(1, Cursor(seeded, "run.niche").Counter);
        Assert.Equal(originalHash, CanonicalJson.Sha256(fresh));

        var engine = new PrototypeGameEngine();
        var choice = engine.GetLegalActions(seeded)
            .First(a => a.Kind == "choose_map_node");
        var combat = engine.Step(seeded, choice).State;
        Assert.Equal(1, Cursor(combat, "run.niche").Counter);

        var enemies = combat.World!.Combat!.Enemies;
        var specs = enemies.Select(e =>
            new PrototypeEncounterEnemySpec(e.EnemyId,
                e.FormationPosition, e.SlotName)).ToArray();
        var definition = new PrototypeEncounterDefinition(
            "test.current.formation", PrototypeRoomType.Combat,
            specs.Select(e => e.EnemyId).ToArray(), Formation: specs);
        var formation = NativeEncounterFormationFactoryV111.Generate(
            seed, definition, "CurrentActOneEncounter",
            combat.World.Floor, 0, 1);
        var attached = NativeRunRngV111.BeginEncounter(combat, formation);
        Assert.Equal(enemies.Length + 1, Cursor(attached, "run.niche").Counter);
        Assert.Equal(
            Cursor(attached, "run.niche"),
            NativeRngV111.ToSnapshot(
                attached.World!.Combat!.NativeRng!.RunStreams.Streams
                    .Single(s => s.StreamId == "run.niche")));

        // Independently reproduce the entire initial HP trajectory after
        // one earlier native run-level RNG draw.
        var occupied = new HashSet<int>();
        foreach (var enemy in attached.World.Combat.Enemies)
        {
            var range = PrototypeContent.Enemy(enemy.EnemyId)
                .HpRangeAt(1, attached.Ascension);
            var choices = Enumerable.Range(
                    range.Min, range.Max - range.Min + 1)
                .Where(hp => !occupied.Contains(hp)).ToArray();
            if (choices.Length == 0)
                choices = Enumerable.Range(range.Min,
                    range.Max - range.Min + 1).ToArray();
            var hp = choices[NativeRngV111.NextInt(
                reference, "run.niche", choices.Length)];
            Assert.Equal(hp, enemy.Hp);
            occupied.Add(hp);
        }
        Assert.Equal(
            NativeRngV111.ToSnapshot(
                reference.Streams.Single(s => s.StreamId == "run.niche")),
            Cursor(attached, "run.niche"));
        PrototypeStateInvariants.Validate(attached);
    }

    [Fact]
    public void ActiveCombatNativeStreamDrawsRemainAtomicAndSnapshotSafe()
    {
        var state = EnterNativeOwnedCombat("native-run-combat-atom");
        var hash = CanonicalJson.Sha256(state);
        var advanced = NativeRunRngV111.SampleNextInt(
            state, "run.monster_ai", 9, out var value);
        Assert.InRange(value, 0, 8);
        Assert.Equal(hash, CanonicalJson.Sha256(state));
        Assert.Equal(Cursor(advanced, "run.monster_ai"),
            NativeRngV111.ToSnapshot(
                advanced.World!.Combat!.NativeRng!.RunStreams.Streams
                    .Single(s => s.StreamId == "run.monster_ai")));

        var nativeCommit = NativeCombatRngEntryV111.CommitIntents(
            advanced.World.Combat);
        advanced = NativeRunRngV111.ReplaceCombat(advanced, nativeCommit);
        NativeRunRngV111.Validate(advanced);
        var saved = RunSnapshot.Save(advanced);
        var restored = RunSnapshot.Load(saved);
        Assert.Equal(CanonicalJson.Sha256(advanced),
            CanonicalJson.Sha256(restored));
        Assert.Equal(
            CanonicalJson.Sha256(advanced),
            CanonicalJson.Sha256(restored.Fork()));

        restored.NativeRunRng!.Streams[0].StateBytes[0] ^= 1;
        Assert.Equal(CanonicalJson.Sha256(advanced),
            CanonicalJson.Sha256(RunSnapshot.Load(saved)));
        Assert.Equal(hash, CanonicalJson.Sha256(state));
    }

    [Fact]
    public void ReleasedNativeCombatRetainsNextRoomRngCounters()
    {
        var state = EnterNativeOwnedCombat("native-run-two-encounters");
        state = NativeRunRngV111.SampleNextInt(
            state, "run.combat_targets", 30, out _);
        var previous = Cursor(state, "run.combat_targets");
        var previousHp = Cursor(state, "run.niche");
        var released = NativeRunRngV111.ReleaseCombat(state);
        Assert.Null(released.World!.Combat!.NativeRng);
        Assert.Equal(previous, Cursor(released, "run.combat_targets"));
        Assert.Equal(previousHp, Cursor(released, "run.niche"));
        NativeRunRngV111.Validate(released);

        var nextRoom = released with
        {
            Phase = RunPhase.MapChoice,
            World = released.World with
            {
                Combat = null, ActiveRoom = null
            }
        };
        var next = NativeRunRngV111.SampleNextInt(
            nextRoom, "run.combat_targets", 30, out _);
        Assert.Equal(previous.Counter + 1,
            Cursor(next, "run.combat_targets").Counter);
        Assert.Equal(previousHp, Cursor(next, "run.niche"));
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    [Fact]
    public void BrokenRunCombatSynchronizationIsRejected()
    {
        var state = EnterNativeOwnedCombat("native-run-corruption");
        Assert.Throws<InvalidOperationException>(() =>
            NativeRunRngV111.Validate(
                state with { NativeRunRng = null }));
        var changedStreams = state.NativeRunRng!.Fork();
        _ = NativeRngV111.NextInt(changedStreams, "run.niche", 7);
        var corrupted = state with
        {
            Stochastic = RunStochasticStateV111.WithNativeRunRng(
                state.Stochastic, changedStreams)
        };
        Assert.Throws<InvalidOperationException>(() =>
            NativeRunRngV111.Validate(corrupted));
        Assert.Throws<InvalidOperationException>(() =>
            PrototypeStateInvariants.Validate(corrupted));
        Assert.Throws<InvalidOperationException>(() =>
            NativeRunRngV111.Enable(state));
    }

    [Fact]
    public void LegacyPrototypeRunRemainsUnaffectedUnlessExplicitlyEnabled()
    {
        const string seed = "native-run-legacy-unchanged";
        var engine = new PrototypeGameEngine();
        var initial = PrototypeGameFactory.Create(seed);
        Assert.Null(initial.NativeRunRng);
        var start = engine.Step(initial, GameAction.Empty("start_run")).State;
        Assert.Null(start.NativeRunRng);
        var enabled = NativeRunRngV111.Enable(start);
        Assert.NotNull(enabled.NativeRunRng);
        Assert.Equal(CanonicalJson.Sha256(start.Rng),
            CanonicalJson.Sha256(enabled.Rng));
        var next = engine.Step(enabled,
            engine.GetLegalActions(enabled)
                .First(a => a.Kind == "choose_map_node")).State;
        Assert.Equal(
            CanonicalJson.Sha256(enabled.NativeRunRng),
            CanonicalJson.Sha256(next.NativeRunRng));
        Assert.Null(next.World!.Combat!.NativeRng);
        Assert.Null(start.NativeRunRng);
    }
}
