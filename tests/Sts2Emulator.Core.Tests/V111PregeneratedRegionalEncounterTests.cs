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
    [InlineData(PrototypeActOneRegion.Overgrowth)]
    [InlineData(PrototypeActOneRegion.Underdocks)]
    public void FutureHistoriesArePreparedBeforeTraversalAndIndependentOfEarlierDraws(
        PrototypeActOneRegion region)
    {
        var engine = new PrototypeGameEngine();
        var config = RunConfiguration.Create(firstAct:
            region == PrototypeActOneRegion.Overgrowth
                ? ActIdentity.Overgrowth : ActIdentity.Underdocks) with
        {
            FidelityId = V111Build.ExperimentalSilentThreeActRouteFidelityId
        };
        var state = engine.Step(V111RunFactory.Create("upfront-history-" + region, config),
            GameAction.Empty("start_run")).State;
        while (state.Phase is RunPhase.Event or RunPhase.Reward)
            state = engine.Step(state, engine.GetLegalActions(state)[0]).State;
        var prepared = state.World!.PreparedLaterActEncounterBags!;
        Assert.Equal(new[] { 2, 3 }, prepared.Select(bag => bag.Act));
        var hiveHash = CanonicalJson.Sha256(prepared[0]);
        var gloryHash = CanonicalJson.Sha256(prepared[1]);
        PrototypeStateInvariants.Validate(state);

        var branch = RunSnapshot.Load(RunSnapshot.Save(state));
        // Simulate distinct earlier stochastic histories. These draws may
        // affect subsequent mechanics but must not change prepared regions.
        for (var i = 0; i < 37; i++)
            _ = PrototypeRng.NextInt(branch.Rng, "combat", 1000);
        var cursor = CanonicalJson.Sha256(branch.Rng);
        var hive = engine.Step(branch with { Phase = RunPhase.ActTransition },
            GameAction.Empty("continue_act")).State;
        Assert.Equal(hiveHash, CanonicalJson.Sha256(hive.World!.LaterActEncounterBag));
        Assert.Equal(cursor, CanonicalJson.Sha256(hive.Rng));
        Assert.Equal(3, Assert.Single(hive.World.PreparedLaterActEncounterBags!).Act);
        var glory = engine.Step(hive with { Phase = RunPhase.ActTransition },
            GameAction.Empty("continue_act")).State;
        Assert.Equal(gloryHash, CanonicalJson.Sha256(glory.World!.LaterActEncounterBag));
        Assert.Empty(glory.World.PreparedLaterActEncounterBags!);
        Assert.Equal(cursor, CanonicalJson.Sha256(glory.Rng));
        PrototypeStateInvariants.Validate(glory);

        // Taking a queue and mutating a fork must not alias its stored parent.
        var child = hive.Fork();
        child.World!.LaterActEncounterBag!.RemainingWeak[0] = "invalid-child-id";
        child.World.PreparedLaterActEncounterBags![0].RemainingWeak[0] = "invalid-future-id";
        Assert.Equal(hiveHash, CanonicalJson.Sha256(prepared[0]));
        Assert.Equal(gloryHash, CanonicalJson.Sha256(prepared[1]));
        Assert.Equal(hiveHash, CanonicalJson.Sha256(hive.World.LaterActEncounterBag));
        Assert.Equal(gloryHash, CanonicalJson.Sha256(hive.World.PreparedLaterActEncounterBags![0]));
    }

    [Fact]
    public void PreparedHistoryRejectsMissingForeignOrConsumedFutureAct()
    {
        var engine = new PrototypeGameEngine();
        var config = RunConfiguration.Create() with
        {
            FidelityId = V111Build.ExperimentalSilentThreeActRouteFidelityId
        };
        var state = engine.Step(V111RunFactory.Create("invalid-upfront-history", config),
            GameAction.Empty("start_run")).State;
        var histories = state.World!.PreparedLaterActEncounterBags!;
        foreach (var corrupt in new PrototypeLaterActEncounterBagV111[]?[]
        {
            null, [], [histories[1], histories[0]],
            [histories[0] with { OrdinaryCombatsStarted = 1 }, histories[1]],
            [histories[0], histories[1] with { RemainingElite = [] }]
        })
            Assert.Throws<InvalidOperationException>(() =>
                PrototypeStateInvariants.Validate(state with
                {
                    World = state.World with { PreparedLaterActEncounterBags = corrupt }
                }));
    }

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

        var broken = state with
        {
            World = state.World with
            {
                LaterActEncounterBag = original with
                {
                    OrdinaryCombatsStarted = 1
                }
            }
        };
        Assert.Throws<InvalidOperationException>(() =>
            PrototypeStateInvariants.Validate(broken));
    }

    [Fact]
    public void OnlyA10PlusGloryGetsADistinctPreselectedSecondBoss()
    {
        var belowThreshold = ExperimentalSilentThreeActRouteV111.InitialBag(
            3, PrototypeRng.CreateBundle("glory-double-boss-a9"), ascension: 9);
        Assert.Null(belowThreshold.SecondBossEncounterId);

        var atThreshold = ExperimentalSilentThreeActRouteV111.InitialBag(
            3, PrototypeRng.CreateBundle("glory-double-boss-a10"), ascension: 10);
        Assert.Contains(atThreshold.BossEncounterId,
            ExperimentalSilentThreeActRouteV111.GloryBosses);
        Assert.Contains(atThreshold.SecondBossEncounterId,
            ExperimentalSilentThreeActRouteV111.GloryBosses);
        Assert.NotEqual(atThreshold.BossEncounterId,
            atThreshold.SecondBossEncounterId);

        var hive = ExperimentalSilentThreeActRouteV111.InitialBag(
            2, PrototypeRng.CreateBundle("hive-double-boss-a10"), ascension: 10);
        Assert.Null(hive.SecondBossEncounterId);
    }
}
