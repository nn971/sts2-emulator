using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Source-shape oracle for the Hive/Glory ActModel.GenerateRooms encounter
/// histories. Tests sequence construction and engine snapshot ownership,
/// rather than native RNG cursor parity or gameplay-victory probability.
/// </summary>
public sealed class V111PregeneratedRegionalEncounterTests
{
    [Theory]
    [InlineData(2, 14)]
    [InlineData(3, 13)]
    public void PregeneratedQueuesHavePinnedNativeLengthsAndRefillShape(
        int act, int ordinaryRooms)
    {
        for (var i = 0; i < 64; i++)
        {
            var rng = PrototypeRng.CreateBundle(
                $"pregenerated-queue-act{act}-seed{i}");
            var bag = ExperimentalSilentThreeActRouteV111.InitialBag(act, rng);
            Assert.Equal(0, bag.OrdinaryCombatsStarted);
            Assert.Equal(2, bag.RemainingWeak.Length);
            Assert.Equal(ordinaryRooms - 2, bag.RemainingNormal.Length);
            Assert.Equal(15, bag.RemainingElite.Length);
            Assert.Contains(bag.BossEncounterId,
                ExperimentalSilentThreeActRouteV111.Pool(
                    act, PrototypeRoomType.Boss, false));

            // Every refill begins with all source-authored entries.
            // At most one copy of each entry appears per bag cycle.
            var normalPool = ExperimentalSilentThreeActRouteV111.Pool(
                act, PrototypeRoomType.Combat, false);
            Assert.Equal(normalPool.OrderBy(x => x),
                bag.RemainingNormal.Take(normalPool.Length).OrderBy(x => x));
            var elitePool = ExperimentalSilentThreeActRouteV111.Pool(
                act, PrototypeRoomType.Elite, false);
            foreach (var group in bag.RemainingElite.Chunk(elitePool.Length))
                Assert.Equal(elitePool.OrderBy(x => x), group.OrderBy(x => x));

            var ordinary = bag.RemainingWeak.Concat(bag.RemainingNormal)
                .ToArray();
            Assert.NotEqual(ordinary[0], ordinary[1]);

            // Both pools have an eligible alternative at the weak/normal
            // boundary, so identical names/tags are forbidden there.
            Assert.NotEqual(ordinary[1], ordinary[2]);
            var lastWeakTag = ExperimentalSilentThreeActRouteV111
                .EncounterTag(ordinary[1]);
            if (lastWeakTag is not null)
                Assert.NotEqual(lastWeakTag,
                    ExperimentalSilentThreeActRouteV111.EncounterTag(
                        ordinary[2]));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void PullingQueuesConsumesNoNewEncounterIdentityRandomness(int act)
    {
        var rng = PrototypeRng.CreateBundle("queue-draw-boundary-" + act);
        var engine = new PrototypeGameEngine();
        var config = RunConfiguration.Create() with
        {
            FidelityId = V111Build.ExperimentalSilentThreeActRouteFidelityId
        };
        var opening = engine.Step(
            V111RunFactory.Create("queue-entry-" + act, config),
            GameAction.Empty("start_run")).State;
        var bag = ExperimentalSilentThreeActRouteV111.InitialBag(act, rng);
        var cursor = rng.Fork();
        var world = opening.World! with
        {
            Act = act,
            Floor = 8,
            LaterActEncounterBag = bag
        };

        for (var i = 0; i < 4; i++)
        {
            var expectedId = i < 2
                ? bag.RemainingWeak[0]
                : bag.RemainingNormal[0];
            var chosen = ExperimentalSilentThreeActRouteV111.SelectEncounter(
                world with { LaterActEncounterBag = bag },
                PrototypeRoomType.Combat, rng);
            Assert.Equal(expectedId, chosen.Encounter.Id);
            bag = chosen.Bag;
        }
        Assert.Equal(4, bag.OrdinaryCombatsStarted);
        Assert.Empty(bag.RemainingWeak);
        Assert.Equal(
            ExperimentalSilentThreeActRouteV111.OrdinaryRoomCount(act) - 4,
            bag.RemainingNormal.Length);
        for (var i = 0; i < 3; i++)
        {
            var expected = bag.RemainingElite[0];
            var chosen = ExperimentalSilentThreeActRouteV111.SelectEncounter(
                world with { LaterActEncounterBag = bag },
                PrototypeRoomType.Elite, rng);
            Assert.Equal(expected, chosen.Encounter.Id);
            bag = chosen.Bag;
        }
        Assert.Equal(12, bag.RemainingElite.Length);
        Assert.Equal(PrototypeRng.NextInt(cursor, "combat", 100000),
            PrototypeRng.NextInt(rng, "combat", 100000));
    }

    [Fact]
    public void ActEntryQueuesAreSnapshotSafeAndRejectIncoherentCursor()
    {
        var engine = new PrototypeGameEngine();
        var config = RunConfiguration.Create() with
        {
            FidelityId = V111Build.ExperimentalSilentThreeActRouteFidelityId
        };
        var state = engine.Step(
            V111RunFactory.Create("queue-invariant-snapshot", config),
            GameAction.Empty("start_run")).State;
        while (state.Phase is RunPhase.Event or RunPhase.Reward)
            state = engine.Step(state, engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        state = engine.Step(
            state with { Phase = RunPhase.ActTransition },
            GameAction.Empty("continue_act")).State;

        PrototypeStateInvariants.Validate(state);
        var restored = RunSnapshot.Load(RunSnapshot.Save(state));
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(restored));
        var original = state.World!.LaterActEncounterBag!;
        var copy = restored.World!.LaterActEncounterBag!;
        Assert.Equal(original.RemainingWeak, copy.RemainingWeak);
        Assert.Equal(original.RemainingNormal, copy.RemainingNormal);
        Assert.Equal(original.RemainingElite, copy.RemainingElite);

        Assert.Throws<InvalidOperationException>(() =>
            _ = state with
            {
                World = state.World with
                {
                    LaterActEncounterBag = original with
                    {
                        OrdinaryCombatsStarted = 1
                    }
                }
            });
    }
}
