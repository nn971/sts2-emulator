using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111DarvEventTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Fact]
    public void RevealedDarvIsAssignedToAtMostOneLaterActAndPersists()
    {
        var sawHive = false;
        var sawGlory = false;
        var sawNoAssignment = false;
        for (var seed = 0; seed < 96; seed++)
        {
            var config = RunConfiguration.Create(profile:
                new SimulationProfile(true, [], [NativeDarvEventV111.EpochId])) with
            {
                FidelityId = V111Build.ExperimentalSilentThreeActRouteFidelityId
            };
            var initial = V111RunFactory.Create("darv-assignment-" + seed, config);
            var started = Engine.Step(initial, GameAction.Empty("start_run")).State;
            var bags = started.World!.PreparedLaterActEncounterBags!;
            var assigned = bags.Where(bag =>
                bag.AssignedAncientId == NativeDarvEventV111.EventId).ToArray();
            Assert.InRange(assigned.Length, 0, 1);
            sawHive |= assigned.Any(bag => bag.Act == 2);
            sawGlory |= assigned.Any(bag => bag.Act == 3);
            sawNoAssignment |= assigned.Length == 0;
            PrototypeStateInvariants.Validate(started);
            var restored = RunSnapshot.Load(RunSnapshot.Save(started));
            Assert.Equal(CanonicalJson.Sha256(started), CanonicalJson.Sha256(restored));
            Assert.Equal(CanonicalJson.Sha256(started), CanonicalJson.Sha256(
                Engine.Step(initial.Fork(), GameAction.Empty("start_run")).State));
        }
        Assert.True(sawHive && sawGlory && sawNoAssignment);
    }

    [Fact]
    public void PreparedSharedAssignmentOpensAtHiveAndGloryBoundaries()
    {
        RunState? hiveOpening = null;
        RunState? gloryOpening = null;
        for (var seed = 0; seed < 160
            && (hiveOpening is null || gloryOpening is null); seed++)
        {
            var profile = new SimulationProfile(true, [],
                [NativeDarvEventV111.EpochId]);
            var config = RunConfiguration.Create(profile: profile) with
            {
                FidelityId = V111Build.ExperimentalSilentThreeActRouteFidelityId
            };
            var state = Engine.Step(V111RunFactory.Create(
                "darv-opening-boundary-" + seed, config),
                GameAction.Empty("start_run")).State;
            state = ResolveToMapChoice(state);
            var hiveAssigned = state.World!.PreparedLaterActEncounterBags!
                .Single(bag => bag.Act == 2).AssignedAncientId
                == NativeDarvEventV111.EventId;
            var gloryAssigned = state.World.PreparedLaterActEncounterBags!
                .Single(bag => bag.Act == 3).AssignedAncientId
                == NativeDarvEventV111.EventId;
            var hive = Transition(state);
            if (hiveAssigned)
            {
                Assert.Equal(NativeDarvEventV111.EventId,
                    hive.World!.Event!.EventId);
                hiveOpening ??= hive;
            }
            else
            {
                hive = ResolveToMapChoice(hive);
                var glory = Transition(hive);
                if (gloryAssigned)
                {
                    Assert.Equal(NativeDarvEventV111.EventId,
                        glory.World!.Event!.EventId);
                    gloryOpening ??= glory;
                }
            }
        }
        Assert.NotNull(hiveOpening);
        Assert.NotNull(gloryOpening);
        PrototypeStateInvariants.Validate(hiveOpening!);
        PrototypeStateInvariants.Validate(gloryOpening!);
        Assert.Equal(CanonicalJson.Sha256(gloryOpening), CanonicalJson.Sha256(
            RunSnapshot.Load(RunSnapshot.Save(gloryOpening!))));
    }

    [Fact]
    public void EverySupportedDarvRelicOfferAcquiresAndReturnsToTheMap()
    {
        var openings = Enumerable.Range(0, 96).Select(index =>
            NativeDarvEventV111.CreateOpeningFixture("darv-offers-" + index))
            .ToArray();
        foreach (var relicId in NativeDarvEventV111.SupportedRelicIds)
        {
            var initial = openings.First(state =>
                state.World!.Event!.OfferedChoiceIds!.Contains("take_" + relicId));
            var action = GameAction.Create("event_choice",
                new EventChoicePayload("take_" + relicId));
            var acquired = Resolve(Engine.Step(initial, action).State);
            for (var decision = 0; decision < 12
                && acquired.Phase == RunPhase.Event; decision++)
            {
                var next = Engine.GetLegalActions(acquired)[0];
                acquired = Engine.Step(acquired, next).State;
            }
            Assert.Single(acquired.Player.Relics, relic => relic.RelicId == relicId);
            Assert.Equal(RunPhase.MapChoice, acquired.Phase);
            Assert.Null(acquired.World!.Event);
            Assert.Empty(acquired.World.CompletedRooms);
            PrototypeStateInvariants.Validate(acquired);
            Assert.Equal(CanonicalJson.Sha256(acquired), CanonicalJson.Sha256(
                Resolve(Engine.Step(initial.Fork(), action).State)));
        }
    }

    [Fact]
    public void DarvOfferContextRejectsMissingOrUnselectedRelics()
    {
        var state = NativeDarvEventV111.CreateOpeningFixture("darv-invalid");
        PrototypeStateInvariants.Validate(state);
        var absent = NativeDarvEventV111.SupportedRelicIds.Select(id => "take_" + id)
            .First(id => !state.World!.Event!.OfferedChoiceIds!.Contains(id));
        var before = CanonicalJson.Sha256(state);
        Assert.Throws<InvalidOperationException>(() => Engine.Step(state,
            GameAction.Create("event_choice", new EventChoicePayload(absent))));
        Assert.Equal(before, CanonicalJson.Sha256(state));
        Assert.Throws<InvalidOperationException>(() => PrototypeStateInvariants.Validate(
            state with
            {
                World = state.World! with
                {
                    Event = state.World.Event! with { NativeEventRng = null }
                }
            }));
    }

    private static RunState Resolve(RunState state)
    {
        for (var decision = 0; decision < 12
            && state.Phase == RunPhase.Event; decision++)
            state = Engine.Step(state, Engine.GetLegalActions(state)[0]).State;
        return state;
    }

    private static RunState ResolveToMapChoice(RunState state)
    {
        for (var decision = 0; decision < 36
            && state.Phase is RunPhase.Event or RunPhase.Reward; decision++)
            state = Engine.Step(state, Engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        return state;
    }

    private static RunState Transition(RunState state) => Engine.Step(
        state with
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
        }, GameAction.Empty("continue_act")).State;
}
