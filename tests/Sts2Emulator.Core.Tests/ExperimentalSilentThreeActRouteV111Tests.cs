using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Opt-in end-to-end route integration tests. A forged act-transition
/// boundary isolates Hive/Glory plumbing from unfinished Act 1 parity;
/// these are emulator determinism tests, not native-game oracle traces.
/// </summary>
public sealed class ExperimentalSilentThreeActRouteV111Tests
{
    private static readonly PrototypeGameEngine Engine = new();

    private static RunState ActOneMap(string seed, bool enabled = true)
    {
        var configuration = RunConfiguration.Create() with
        {
            FidelityId = enabled
                ? V111Build.ExperimentalSilentThreeActRouteFidelityId
                : "v111-mechanics-prototype-rng-v1"
        };
        var state = Engine.Step(
            V111RunFactory.Create(seed, configuration),
            GameAction.Empty("start_run")).State;
        for (var i = 0; i < 30
            && state.Phase is RunPhase.Event or RunPhase.Reward; i++)
            state = Engine.Step(
                state, Engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        // These fixtures isolate transfer/room behavior; explicit assignment
        // tests below and whole-route sweeps exercise the opening events.
        if (state.World!.PreparedLaterActEncounterBags is { } prepared)
            state = state with
            {
                World = state.World with
                {
                    PreparedLaterActEncounterBags = prepared.Select(bag =>
                        bag with { AssignedAncientId = null }).ToArray()
                }
            };
        return state;
    }

    private static RunState Transition(RunState state)
    {
        Assert.NotNull(state.World);
        var transition = state with
        {
            Phase = RunPhase.ActTransition,
            World = state.World! with
            {
                ActiveRoom = null,
                Combat = null,
                Reward = null,
                Shop = null,
                Event = null
            }
        };
        return Engine.Step(
            transition, GameAction.Empty("continue_act")).State;
    }

    [Theory]
    [InlineData("silent-route-one")]
    [InlineData("silent-route-two")]
    [InlineData("silent-route-three")]
    public void PersistentSilentStateTransfersThroughBothActBoundaries(
        string seed)
    {
        var actOne = ActOneMap(seed);
        var originalHash = CanonicalJson.Sha256(actOne);
        var hive = Transition(actOne);
        Assert.Equal(RunPhase.MapChoice, hive.Phase);
        Assert.Equal(2, hive.World!.Act);
        Assert.Equal(ActIdentity.Hive, hive.World.ActIdentity);
        Assert.Equal(PrototypeNativeLaterActMap.HiveProfileId,
            hive.World.Map.GenerationProfileId);
        Assert.Equal(0, hive.World.Floor);
        Assert.Null(hive.World.ActOneRegion);
        Assert.Null(hive.World.ActOneEncounterPool);
        Assert.NotNull(hive.World.LaterActEncounterBag);
        Assert.Equal(2, hive.World.LaterActEncounterBag!.Act);
        Assert.Equal(actOne.Player, actOne.Player);
        Assert.Equal(CanonicalJson.Sha256(actOne.Player),
            CanonicalJson.Sha256(hive.Player));
        Assert.Equal(CanonicalJson.Sha256(actOne.NativeRunRng),
            CanonicalJson.Sha256(hive.NativeRunRng));
        // Encounter identities were prepared before map traversal; entry
        // consumes their persisted history without another selection draw.
        Assert.Equal(CanonicalJson.Sha256(actOne.Rng),
            CanonicalJson.Sha256(hive.Rng));
        Assert.Equal(originalHash, CanonicalJson.Sha256(actOne));
        PrototypeStateInvariants.Validate(hive);

        var restored = JsonSerializer.Deserialize<RunState>(
            JsonSerializer.Serialize(hive))!;
        Assert.Equal(CanonicalJson.Sha256(hive),
            CanonicalJson.Sha256(restored));
        PrototypeStateInvariants.Validate(restored);
        var glory = Transition(restored);
        Assert.Equal(3, glory.World!.Act);
        Assert.Equal(ActIdentity.Glory, glory.World.ActIdentity);
        Assert.Equal(PrototypeNativeLaterActMap.GloryProfileId,
            glory.World.Map.GenerationProfileId);
        Assert.Equal(CanonicalJson.Sha256(hive.Player),
            CanonicalJson.Sha256(glory.Player));
        Assert.Equal(CanonicalJson.Sha256(hive.NativeRunRng),
            CanonicalJson.Sha256(glory.NativeRunRng));
        PrototypeStateInvariants.Validate(glory);
        Assert.Equal(CanonicalJson.Sha256(glory),
            CanonicalJson.Sha256(Transition(hive.Fork())));
    }

    [Theory]
    [InlineData(NativePaelRewardsV111.PaelId)]
    [InlineData(NativeAncientRewardsV111.TezcataraId)]
    [InlineData(NativeOrobasEventV111.EventId)]
    public void AssignedHiveAncientOpensBeforeMapChoice(string ancientId)
    {
        var state = ActOneMap("silent-assigned-ancient-" + ancientId);
        var world = state.World!;
        var prepared = world.PreparedLaterActEncounterBags!
            .Select(bag => bag.Act == 2
                ? bag with { AssignedAncientId = ancientId }
                : bag.Fork())
            .ToArray();
        state = state with
        {
            World = world with { PreparedLaterActEncounterBags = prepared }
        };

        var hive = Transition(state);
        Assert.Equal(RunPhase.Event, hive.Phase);
        Assert.Equal(2, hive.World!.Act);
        Assert.Equal(0, hive.World.Floor);
        Assert.Equal(ancientId, hive.World.Event!.EventId);
        Assert.Equal(ancientId,
            hive.World.LaterActEncounterBag!.AssignedAncientId);
        Assert.NotEmpty(Engine.GetLegalActions(hive));
        PrototypeStateInvariants.Validate(hive);

        var restored = JsonSerializer.Deserialize<RunState>(
            JsonSerializer.Serialize(hive))!;
        Assert.Equal(CanonicalJson.Sha256(hive),
            CanonicalJson.Sha256(restored));
        Assert.Equal(CanonicalJson.Sha256(hive),
            CanonicalJson.Sha256(Transition(state.Fork())));
    }

    [Fact]
    public void ConfiguredRunsPrepareEverySupportedActLocalAncientAtRunStart()
    {
        var seen = new Dictionary<int, HashSet<string>>
        {
            [2] = new(StringComparer.Ordinal),
            [3] = new(StringComparer.Ordinal)
        };
        for (var index = 0; index < 48; index++)
        {
            var configuration = RunConfiguration.Create() with
            {
                FidelityId = V111Build.ExperimentalSilentThreeActRouteFidelityId
            };
            var initial = V111RunFactory.Create("assigned-run-start-" + index, configuration);
            var before = CanonicalJson.Sha256(initial);
            var started = Engine.Step(initial, GameAction.Empty("start_run")).State;
            var prepared = started.World!.PreparedLaterActEncounterBags!;
            Assert.Equal(new[] { 2, 3 }, prepared.Select(bag => bag.Act));
            foreach (var bag in prepared)
                seen[bag.Act].Add(Assert.IsType<string>(bag.AssignedAncientId));
            PrototypeStateInvariants.Validate(started);
            Assert.Equal(before, CanonicalJson.Sha256(initial));
            Assert.Equal(CanonicalJson.Sha256(started), CanonicalJson.Sha256(
                Engine.Step(initial.Fork(), GameAction.Empty("start_run")).State));
            Assert.Equal(CanonicalJson.Sha256(prepared), CanonicalJson.Sha256(
                RunSnapshot.Load(RunSnapshot.Save(started))
                    .World!.PreparedLaterActEncounterBags));
        }
        Assert.True(seen[2].SetEquals(new[] { NativeOrobasEventV111.EventId,
            NativePaelRewardsV111.PaelId, NativeAncientRewardsV111.TezcataraId }));
        Assert.True(seen[3].SetEquals(new[] { NativeNonupeipeEventV111.EventId,
            NativeTanxEventV111.EventId, NativeVakuuEventV111.EventId }));
    }

    [Theory]
    [InlineData(NativeTanxEventV111.EventId)]
    [InlineData(NativeVakuuEventV111.EventId)]
    [InlineData(NativeNonupeipeEventV111.EventId)]
    public void AssignedGloryAncientOpensBeforeMapChoice(string ancientId)
    {
        var state = ActOneMap("glory-assigned-" + ancientId);
        state = state with
        {
            World = state.World! with
            {
                PreparedLaterActEncounterBags = state.World.PreparedLaterActEncounterBags!
                    .Select(bag => bag.Act == 3
                        ? bag with { AssignedAncientId = ancientId } : bag.Fork())
                    .ToArray()
            }
        };
        PrototypeStateInvariants.Validate(state);
        var hive = Transition(state);
        var glory = Transition(hive);
        Assert.Equal(RunPhase.Event, glory.Phase);
        Assert.Equal(3, glory.World!.Act);
        Assert.Equal(0, glory.World.Floor);
        Assert.Equal(ancientId, glory.World.Event!.EventId);
        Assert.Equal(ancientId, glory.World.LaterActEncounterBag!.AssignedAncientId);
        Assert.NotEmpty(Engine.GetLegalActions(glory));
        PrototypeStateInvariants.Validate(glory);
        Assert.Equal(CanonicalJson.Sha256(glory), CanonicalJson.Sha256(
            RunSnapshot.Load(RunSnapshot.Save(glory))));
        Assert.Equal(CanonicalJson.Sha256(glory), CanonicalJson.Sha256(
            Transition(hive.Fork())));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void RealMapEntryProducesAnExplicitRegionalCombatFixture(int act)
    {
        var seed = "silent-route-combat-" + act;
        var state = Transition(ActOneMap(seed));
        if (act == 3)
            state = Transition(state);
        var initialHash = CanonicalJson.Sha256(state);
        var action = Engine.GetLegalActions(state)
            .First(option => option.Kind == "choose_map_node");
        var combat = Engine.Step(state, action).State;
        Assert.Equal(RunPhase.Combat, combat.Phase);
        Assert.Equal(act, combat.World!.Act);
        var encounter = Assert.Single(combat.World.EncounterIds);
        Assert.True(ExperimentalSilentThreeActRouteV111
            .ContainsEncounter(encounter, act));
        Assert.All(combat.World.Combat!.Enemies, enemy =>
            Assert.True(enemy.Hp > 0));
        Assert.Null(combat.World.Combat.NativeRng);
        PrototypeStateInvariants.Validate(combat);
        Assert.Equal(initialHash, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(combat),
            CanonicalJson.Sha256(Engine.Step(state.Fork(), action).State));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void LaterActUnknownUsesStatefulNativeShapedRoomOdds(int act)
    {
        var seed = "silent-route-event-" + act;
        var state = Transition(ActOneMap(seed));
        if (act == 3)
            state = Transition(state);
        var world = state.World!;
        var nodes = world.Map.Nodes.ToDictionary(
            node => node.NodeId, StringComparer.Ordinal);
        var entry = world.Map.Nodes
            .SelectMany(parent => (parent.NextNodeIds ?? [])
                .Select(next => (Parent: parent, Child: nodes[next])))
            .First(pair => pair.Child.RoomType == PrototypeRoomType.Unknown);
        state = state with
        {
            World = world with
            {
                Floor = entry.Parent.Floor,
                Map = world.Map with
                {
                    CurrentNodeId = entry.Parent.NodeId
                }
            }
        };
        var action = Engine.GetLegalActions(state)
            .Single(option => option.Kind == "choose_map_node"
                && option.ReadPayload<ChooseMapNodePayload>().NodeId
                    == entry.Child.NodeId);
        var started = Engine.Step(state, action).State;
        Assert.Contains(started.Phase,
            new[] { RunPhase.Event, RunPhase.Combat,
                RunPhase.Shop, RunPhase.Reward });
        Assert.NotNull(started.World!.UnknownRoomOdds);
        if (started.Phase == RunPhase.Event)
            Assert.True(ExperimentalSilentThreeActRouteV111.ContainsEvent(
                started.World.Event!.EventId, act));
        if (started.Phase == RunPhase.Combat)
            Assert.True(ExperimentalSilentThreeActRouteV111.ContainsEncounter(
                started.World.EncounterIds.Last(), act));
        PrototypeStateInvariants.Validate(started);
        Assert.NotEmpty(Engine.GetLegalActions(started));
        Assert.Equal(CanonicalJson.Sha256(started),
            CanonicalJson.Sha256(Engine.Step(state.Fork(), action).State));
    }

    [Fact]
    public void GloryUnknownEventSelectionExcludesIneligibleEvents()
    {
        for (var index = 0; index < 96; index++)
        {
            var state = Transition(ActOneMap(
                "glory-event-eligibility-route-" + index));
            state = Transition(state);
            var world = state.World!;
            var nodes = world.Map.Nodes.ToDictionary(
                node => node.NodeId, StringComparer.Ordinal);
            var entry = world.Map.Nodes
                .SelectMany(parent => (parent.NextNodeIds ?? [])
                    .Select(next => (Parent: parent, Child: nodes[next])))
                .First(pair => pair.Child.RoomType
                    == PrototypeRoomType.Unknown);
            state = state with
            {
                Player = state.Player with { Hp = 11 },
                World = world with
                {
                    Floor = entry.Parent.Floor,
                    UnknownRoomOdds = new(0, 0, 0),
                    Map = world.Map with
                    {
                        CurrentNodeId = entry.Parent.NodeId
                    }
                }
            };
            var action = Engine.GetLegalActions(state).Single(option =>
                option.Kind == "choose_map_node"
                && option.ReadPayload<ChooseMapNodePayload>().NodeId
                    == entry.Child.NodeId);
            var started = Engine.Step(state, action).State;
            Assert.Equal(RunPhase.Event, started.Phase);
            Assert.DoesNotContain(started.World!.Event!.EventId,
                new[]
                {
                    PrototypeNativeGloryEvents.RoundTeaPartyId,
                    PrototypeNativeGloryEvents.GraveOfTheForgottenId
                });
        }
    }

    [Fact]
    public void DefaultConfiguredRunStillRejectsLaterActs()
    {
        var original = ActOneMap("silent-route-isolation", enabled: false);
        var hash = CanonicalJson.Sha256(original);
        Assert.Throws<NotSupportedException>(() => Transition(original));
        Assert.Equal(hash, CanonicalJson.Sha256(original));
    }
}
