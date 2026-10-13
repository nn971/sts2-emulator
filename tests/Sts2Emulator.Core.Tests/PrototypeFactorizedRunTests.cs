using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeFactorizedRunTests
{
    private static readonly string[] StreamIds =
    {
        "map", "combat", "combat_targets", "reward", "shop", "event"
    };

    private static Dictionary<string, string> Prior(
        ulong map, ulong combat, ulong targets = 1,
        ulong reward = 2, ulong shop = 3, ulong events = 4) =>
        new(StringComparer.Ordinal)
        {
            ["map"] = map.ToString("x16"),
            ["combat"] = combat.ToString("x16"),
            ["combat_targets"] = targets.ToString("x16"),
            ["reward"] = reward.ToString("x16"),
            ["shop"] = shop.ToString("x16"),
            ["event"] = events.ToString("x16")
        };

    private static RunState Start(Dictionary<string, string> prior)
    {
        var environment = new PrototypeAiEnvironment();
        var initial = PrototypeFactorizedRunFactory.Create(prior);
        Assert.Equal(RunPhase.RunStart, initial.Phase);
        Assert.Equal(6, initial.Rng.Streams.Length);
        var action = Assert.Single(environment.Observe(initial).LegalActions);
        return environment.Step(initial, action.ActionId).State;
    }

    [Fact]
    public void FactorizedInitialStreamsSeparateVisibleMapAndBoss()
    {
        var environment = new PrototypeAiEnvironment();
        var a = Start(Prior(11, 29));
        var b = Start(Prior(11, 901));
        var c = Start(Prior(1201, 29));
        var d = Start(Prior(11, 29, targets: 777, reward: 999, shop: 333));

        var frameA = environment.Observe(a);
        var frameB = environment.Observe(b);
        var frameC = environment.Observe(c);
        var frameD = environment.Observe(d);

        // Changing combat seed changes the preselected boss but cannot
        // alter any other visible start-of-run fields, including map menu.
        Assert.Equal(
            CanonicalJson.Serialize(frameA.Observation with
                { ActOneBossEncounterId = null }),
            CanonicalJson.Serialize(frameB.Observation with
                { ActOneBossEncounterId = null }));
        Assert.Equal(
            frameA.LegalActions.Select(action => action.ActionId),
            frameB.LegalActions.Select(action => action.ActionId));
        Assert.Equal(
            frameA.Observation.ActOneBossEncounterId,
            frameC.Observation.ActOneBossEncounterId);

        // Four streams are unused during StartRun. A future transition
        // can consume their true independent initial state as normal.
        Assert.Equal(frameA.ObservationHash, frameD.ObservationHash);
        Assert.NotEqual(CanonicalJson.Sha256(a.Rng),
            CanonicalJson.Sha256(d.Rng));

        // Exact deterministic replay with a chosen factorized prior.
        Assert.Equal(CanonicalJson.Sha256(a),
            CanonicalJson.Sha256(Start(Prior(11, 29))));
        Assert.Equal(
            PrototypeFactorizedRunFactory.SchemaId,
            "prototype-independent-initial-streams-v1");
        Assert.Equal(StreamIds,
            a.Rng.Streams.Select(stream => stream.StreamId));
    }

    [Fact]
    public void FactorizedPriorRejectsPartialOrMalformedStreamBundles()
    {
        var all = Prior(1, 2);
        foreach (var missing in StreamIds)
        {
            var bad = Prior(1, 2);
            bad.Remove(missing);
            Assert.Throws<ArgumentException>(() =>
                PrototypeFactorizedRunFactory.Create(bad));
        }
        foreach (var malformed in new[] { "x", "-00000000000001", "xyzxyzxyzxyzxyzz" })
        {
            var bad = Prior(1, 2);
            bad["map"] = malformed;
            Assert.Throws<ArgumentException>(() =>
                PrototypeFactorizedRunFactory.Create(bad));
        }
        all["unexpected_stream"] = "0000000000000001";
        Assert.Throws<ArgumentException>(() =>
            PrototypeFactorizedRunFactory.Create(all));
    }

    [Fact]
    public void OrdinarySingleSeedRunsRetainPreviousSemantics()
    {
        var environment = new PrototypeAiEnvironment();
        var state = environment.Reset("historical-single-seed-regression");
        Assert.Empty(state.Rng.Streams);
        var action = Assert.Single(environment.Observe(state).LegalActions);
        var expectedRng = PrototypeRng.CreateBundle(state.RunSeed);
        var expectedBossIndex = PrototypeRng.NextInt(
            expectedRng, "combat",
            PrototypeContent.OvergrowthBossEncounterPool.Length);
        var stepped = environment.Step(state, action.ActionId).State;
        Assert.Equal(
            PrototypeContent.OvergrowthBossEncounterPool[expectedBossIndex],
            stepped.World!.ActOneEncounterPool!.BossEncounterId);
        Assert.NotEmpty(stepped.World.Map.Nodes);
    }
}
