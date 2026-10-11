using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeActOneNormalCombatEntryV111Tests
{
    private static readonly PrototypeGameEngine Engine = new();

    private static RunState Initial(string seed, ActIdentity act)
    {
        var config = RunConfiguration.Create(firstAct: act) with
        {
            FidelityId = V111Build.NativeWeakCombatEntryFidelityId
        };
        var state = Engine.Step(
            V111RunFactory.Create(seed, config),
            GameAction.Empty("start_run")).State;
        for (var step = 0;
            step < 30 && state.Phase is RunPhase.Event or RunPhase.Reward;
            step++)
            state = Engine.Step(state, Engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        return state;
    }

    private static RunState SetupNormalRoom(
        RunState state, string encounterId)
    {
        var world = state.World!;
        // Reach an actual normal-combat node through a pre-existing
        // map edge. Never rewrite first-row entries or the native map
        // geometry just to force the encounter in a test.
        var nodes = world.Map.Nodes.ToDictionary(
            node => node.NodeId, StringComparer.Ordinal);
        var edge = world.Map.Nodes
            .Where(parent => parent.Floor >= 3 && parent.Floor < 14)
            .SelectMany(parent => (parent.NextNodeIds
                ?? Array.Empty<string>()).Select(id =>
                    (Parent: parent, Child: nodes[id])))
            .First(pair => pair.Child.RoomType
                == PrototypeRoomType.Combat
                && pair.Child.Floor >= 4);
        var pool = world.ActOneEncounterPool!;
        // The pool invariant tracks every not-yet-drawn normal model.
        // Arrange the full native-shaped bag so the legacy encounter
        // choice selects the requested model with its next draw.
        var bag = (string[])(pool.RemainingNormalEncounterIds
            ?? throw new InvalidOperationException(
                "Run is missing its normal encounter bag.")).Clone();
        var selection = PrototypeRng.NextInt(
            state.Rng.Fork(), "combat", bag.Length);
        var requestedIndex = Array.IndexOf(bag, encounterId);
        if (requestedIndex < 0)
            throw new InvalidOperationException(
                "Requested normal encounter is not in the regional bag.");
        (bag[requestedIndex], bag[selection]) =
            (bag[selection], bag[requestedIndex]);
        return state with
        {
            Phase = RunPhase.MapChoice,
            World = world with
            {
                Floor = edge.Parent.Floor,
                ActiveRoom = null,
                ActOneEncounterPool = pool with
                {
                    OrdinaryCombatsStarted = 3,
                    RemainingWeakEncounterIds =
                        pool.RemainingWeakEncounterIds.Take(
                            pool.RemainingWeakEncounterIds.Length - 3).ToArray(),
                    RemainingNormalEncounterIds = bag
                },
                Map = world.Map with
                {
                    CurrentNodeId = edge.Parent.NodeId
                }
            }
        };
    }

    private static NativeEncounterFormationV111 PrepareDirect(
        RunState state, string id, int floor = 5)
    {
        var node = state.World!.Map.Nodes.First(node =>
            node.Floor == floor);
        var world = state.World with
        {
            Floor = floor, ActiveRoom = PrototypeRoomType.Combat,
            Map = state.World.Map with
            {
                CurrentNodeId = node.NodeId
            }
        };
        return NativeActOneNormalCombatEntryV111.Prepare(
            state, world, PrototypeContent.Encounter(id));
    }

    private static NativeRngSnapshot Cursor(
        NativeEncounterFormationV111 formation) =>
        NativeRngV111.ToSnapshot(formation.EncounterRng);

    [Theory]
    [InlineData(ActIdentity.Overgrowth, 12)]
    [InlineData(ActIdentity.Underdocks, 10)]
    public void AllNormalEntriesHaveSourceSpecificNativeIdentityAndRegion(
        ActIdentity act, int expected)
    {
        var entries = NativeActOneNormalCombatEntryV111.SupportedEncounterIds
            .Where(id => (act == ActIdentity.Overgrowth
                ? PrototypeContent.OvergrowthNormalEncounterPool
                : PrototypeNativeUnderdocks.SupportedNormalEncounterIds)
                .Contains(id, StringComparer.Ordinal))
            .ToArray();
        Assert.Equal(expected, entries.Length);

        var state = Initial("source-normal-catalog-" + act, act);
        foreach (var id in entries)
        {
            var proto = PrototypeContent.Encounter(id);
            Assert.Equal((1, 1, 0),
                (proto.MinAct, proto.MaxAct, proto.Weight));
            var formation = PrepareDirect(state, id);
            Assert.NotEmpty(formation.Enemies);
            Assert.Equal(formation.Enemies.Length,
                formation.MonsterContexts.Length);
            Assert.Equal(5, formation.TotalFloor);
            Assert.All(formation.MonsterContexts,
                monster => Assert.Equal(0,
                    NativeRngV111.ToSnapshot(monster.Rng).Counter));
            Assert.All(formation.Enemies,
                spec => Assert.NotNull(PrototypeContent.Enemy(spec.EnemyId)));
            Assert.Equal(formation.Enemies.Length,
                formation.Enemies.Select(x => x.FormationPosition)
                    .Distinct().Count());
            Assert.Equal(CanonicalJson.Sha256(formation),
                CanonicalJson.Sha256(PrepareDirect(state, id)));
        }
    }

    [Theory]
    [InlineData(ActIdentity.Overgrowth)]
    [InlineData(ActIdentity.Underdocks)]
    public void EveryNormalEntryCanStartAndReplayAnActualCombat(
        ActIdentity act)
    {
        var candidates = act == ActIdentity.Overgrowth
            ? PrototypeContent.OvergrowthNormalEncounterPool
            : PrototypeNativeUnderdocks.SupportedNormalEncounterIds;
        foreach (var id in candidates)
        {
            var seed = "live-normal-entry-" + id;
            var state = SetupNormalRoom(Initial(seed, act), id);
            var initialHash = CanonicalJson.Sha256(state);
            var action = Engine.GetLegalActions(state)
                .Where(action => action.Kind == "choose_map_node")
                .First(action => state.World!.Map.Nodes
                    .Any(node => node.RoomType == PrototypeRoomType.Combat
                        && node.NodeId == action
                            .ReadPayload<ChooseMapNodePayload>().NodeId));
            var advanced = Engine.Step(state, action).State;
            Assert.Equal(RunPhase.Combat, advanced.Phase);
            Assert.Equal(id, advanced.World!.EncounterIds.Last());
            Assert.Equal(initialHash, CanonicalJson.Sha256(state));
            var combat = advanced.World.Combat!;
            var native = combat.NativeRng!;
            Assert.NotNull(native);
            Assert.Equal(combat.Enemies.Length, native.Formation.Enemies.Length);
            Assert.Equal(native.Formation.Enemies,
                combat.Enemies.Select(enemy => new PrototypeEncounterEnemySpec(
                    enemy.EnemyId, enemy.FormationPosition,
                    enemy.SlotName)).ToArray());
            Assert.Equal(combat.Enemies.Length,
                NativeRngV111.ToSnapshot(
                    advanced.NativeRunRng!.Streams.Single(s =>
                        s.StreamId == "run.niche")).Counter);
            NativeRunRngV111.Validate(advanced);
            PrototypeStateInvariants.Validate(advanced);

            var snapshot = RunSnapshot.Save(advanced);
            var loaded = RunSnapshot.Load(snapshot);
            Assert.Equal(CanonicalJson.Sha256(advanced),
                CanonicalJson.Sha256(loaded));
            Assert.Equal(CanonicalJson.Sha256(advanced),
                CanonicalJson.Sha256(advanced.Fork()));
            // Neow rewards can insert pending choices. Replay their
            // public action sequence before the first completed turn.
            for (var iteration = 0; iteration < 32; iteration++)
            {
                var legal = Engine.GetLegalActions(advanced);
                if (legal.Any(option => option.Kind == "end_turn"))
                    break;
                var nextAction = legal[0];
                advanced = Engine.Step(advanced, nextAction).State;
                loaded = Engine.Step(loaded, nextAction).State;
                Assert.Equal(CanonicalJson.Sha256(advanced),
                    CanonicalJson.Sha256(loaded));
            }
            var end = Engine.GetLegalActions(advanced)
                .Single(option => option.Kind == "end_turn");
            var next = Engine.Step(advanced, end).State;
            var replay = Engine.Step(loaded, end).State;
            Assert.Equal(CanonicalJson.Sha256(next),
                CanonicalJson.Sha256(replay));
            NativeRunRngV111.Validate(next);
        }
    }

    [Fact]
    public void NativeFormationsCorrectKnownPrototypeSourceOrderAndSlots()
    {
        var state = Initial("normal-native-layout", ActIdentity.Overgrowth);
        var crawlers = PrepareDirect(state,
            "proto.encounter.overgrowth_crawlers");
        Assert.Equal(new[]
        {
            "proto.enemy.shrinker_beetle",
            "proto.enemy.fuzzy_wurm_crawler"
        }, crawlers.Enemies.Select(e => e.EnemyId));

        var nibbits = PrepareDirect(state, "proto.encounter.nibbits_normal");
        Assert.Equal(new[] { "front", "back" },
            nibbits.Enemies.Select(e => e.SlotName));

        var inklets = PrepareDirect(state, "proto.encounter.inklets_normal");
        Assert.All(inklets.Enemies,
            enemy => Assert.Null(enemy.SlotName));

        var fogmog = PrepareDirect(state, "proto.encounter.fogmog_normal");
        Assert.Equal("fogmog", Assert.Single(fogmog.Enemies).SlotName);

        var underdocks = Initial("normal-native-layout-under",
            ActIdentity.Underdocks);
        var livingFog = PrepareDirect(underdocks,
            "proto.encounter.living_fog_normal");
        var enemy = Assert.Single(livingFog.Enemies);
        Assert.Equal(5, enemy.FormationPosition);
        Assert.Equal("livingFog", enemy.SlotName);
    }

    [Fact]
    public void DynamicNormalEncounterRngMatchesIndependentSourceOracles()
    {
        const int floor = 5;
        for (var i = 0; i < 72; i++)
        {
            var seed = "normal-source-oracle-" + i;
            foreach (var act in new[]
            {
                ActIdentity.Overgrowth, ActIdentity.Underdocks
            })
            {
                var state = Initial(seed + act, act);
                var ids = act == ActIdentity.Overgrowth
                    ? new[]
                    {
                        "proto.encounter.flyconid_normal",
                        "proto.encounter.ruby_raiders_normal",
                        "proto.encounter.slimes_normal",
                        "proto.encounter.slithering_strangler_normal"
                    }
                    : new[]
                    {
                        "proto.encounter.corpse_slugs_normal",
                        "proto.encounter.two_tailed_rats_normal"
                    };
                foreach (var id in ids)
                {
                    var formed = PrepareDirect(state, id, floor);
                    var nativeName = id switch
                    {
                        "proto.encounter.flyconid_normal" => "FlyconidNormal",
                        "proto.encounter.ruby_raiders_normal" => "RubyRaidersNormal",
                        "proto.encounter.slimes_normal" => "SlimesNormal",
                        "proto.encounter.slithering_strangler_normal" =>
                            "SlitheringStranglerNormal",
                        "proto.encounter.corpse_slugs_normal" =>
                            "CorpseSlugsNormal",
                        _ => "TwoTailedRatsNormal"
                    };
                    var initial = NativeRngV111Contexts.ForEncounterFormation(
                        seed + act, nativeName, floor);
                    var rng = new RngBundle([initial]);
                    int Next(int max) => NativeRngV111.NextInt(
                        rng, initial.StreamId, max);
                    string[] expected;
                    int? offset = null;
                    switch (id)
                    {
                        case "proto.encounter.flyconid_normal":
                            expected = [
                                Next(2) == 0 ? "proto.enemy.leaf_slime_m"
                                : "proto.enemy.twig_slime_m",
                                "proto.enemy.flyconid"];
                            break;
                        case "proto.encounter.ruby_raiders_normal":
                        {
                            var sourcePool = new List<string>
                            {
                                "proto.enemy.axe_ruby_raider",
                                "proto.enemy.assassin_ruby_raider",
                                "proto.enemy.brute_ruby_raider",
                                "proto.enemy.crossbow_ruby_raider",
                                "proto.enemy.tracker_ruby_raider"
                            };
                            var chosen = new List<string>();
                            for (var j = 0; j < 3; j++)
                            {
                                var selection = Next(sourcePool.Count);
                                chosen.Add(sourcePool[selection]);
                                sourcePool.RemoveAt(selection);
                            }
                            expected = chosen.ToArray();
                            break;
                        }
                        case "proto.encounter.slimes_normal":
                        {
                            var leaf = Next(2) == 0;
                            expected = [
                                "proto.enemy.twig_slime_m",
                                "proto.enemy.leaf_slime_m",
                                leaf ? "proto.enemy.leaf_slime_s"
                                : "proto.enemy.twig_slime_s",
                                leaf ? "proto.enemy.twig_slime_s"
                                : "proto.enemy.leaf_slime_s"];
                            break;
                        }
                        case "proto.encounter.slithering_strangler_normal":
                        {
                            string[] mediums = [
                                "proto.enemy.leaf_slime_m",
                                "proto.enemy.twig_slime_m"];
                            string[] smalls = [
                                "proto.enemy.leaf_slime_s",
                                "proto.enemy.twig_slime_s"];
                            var kind = Next(3);
                            var companion = kind switch
                            {
                                0 => new[] {"proto.enemy.snapping_jaxfruit"},
                                1 => new[] {mediums[Next(2)]},
                                _ => new[] {smalls[Next(2)], smalls[Next(2)]}
                            };
                            expected = companion.Append(
                                "proto.enemy.slithering_strangler").ToArray();
                            break;
                        }
                        case "proto.encounter.corpse_slugs_normal":
                            offset = Next(3);
                            expected = Enumerable.Repeat(
                                "proto.enemy.corpse_slug", 3).ToArray();
                            break;
                        default:
                            offset = Next(3);
                            expected = Enumerable.Repeat(
                                "proto.enemy.two_tailed_rat", 3).ToArray();
                            break;
                    }
                    Assert.Equal(expected,
                        formed.Enemies.Select(e => e.EnemyId));
                    Assert.Equal(offset, formed.EncounterOpeningOffset);
                    Assert.Equal(NativeRngV111.ToSnapshot(rng.Streams[0]),
                        Cursor(formed));
                    Assert.Equal(CanonicalJson.Sha256(formed),
                        CanonicalJson.Sha256(formed.Fork()));
                }
            }
        }
    }

    [Fact]
    public void UnsupportedEliteOrWrongRegionHasNoNativeFallback()
    {
        var over = Initial("strict-normal-region", ActIdentity.Overgrowth);
        var node = over.World!.Map.Nodes.First(n => n.Floor == 5);
        var wrongWorld = over.World with
        {
            Floor = 5, ActiveRoom = PrototypeRoomType.Combat,
            Map = over.World.Map with { CurrentNodeId = node.NodeId }
        };
        var hash = CanonicalJson.Sha256(over);
        Assert.Throws<NotSupportedException>(() =>
            NativeActOneNormalCombatEntryV111.Prepare(
                over, wrongWorld, PrototypeContent.Encounter(
                    "proto.encounter.seapunk_normal")));
        Assert.Throws<NotSupportedException>(() =>
            NativeActOneNormalCombatEntryV111.Prepare(
                over, wrongWorld, PrototypeContent.Encounter(
                    "proto.encounter.byrdonis_elite")));
        Assert.False(NativeActOneNormalCombatEntryV111.IsSupported(
            "proto.encounter.byrdonis_elite"));
        Assert.Equal(hash, CanonicalJson.Sha256(over));
    }
}
