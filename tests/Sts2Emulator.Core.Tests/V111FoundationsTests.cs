using System.Text.Json.Nodes;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111FoundationsTests
{
    [Theory]
    [InlineData(ActIdentity.Overgrowth)]
    [InlineData(ActIdentity.Underdocks)]
    public void OpeningEventContinuationsReachTheMapWithoutRecordingAMapRoom(ActIdentity region)
    {
        var engine = new PrototypeGameEngine();
        for (var seed = 0; seed < 24; seed++)
        {
            var state = engine.Step(V111RunFactory.Create("bench-" + region + seed,
                RunConfiguration.Create(firstAct: region)), GameAction.Empty("start_run")).State;
            for (var decision = 0; decision < 30 && state.Phase is RunPhase.Event or RunPhase.Reward; decision++)
                state = engine.Step(state, engine.GetLegalActions(state)[0]).State;
            Assert.Equal(RunPhase.MapChoice, state.Phase);
            Assert.Empty(state.World!.CompletedRooms);
            Assert.Equal(0, state.World.Floor);
            PrototypeStateInvariants.Validate(state);
        }
    }
    [Fact]
    public void InventoryHasPinnedSinglePlayerDenominatorsAndSpecialCombats()
    {
        var inventory = V111Coverage.LoadInventory();
        Assert.Equal(V111Build.SourceCommit, inventory.SourceCommit);
        Assert.Equal(19, inventory.CorpusBlobs.Count);
        Assert.Equal(558, inventory.Items.Count(item => item.Kind == "cards" && item.Scope == "single-player"));
        Assert.Equal(37, inventory.Items.Count(item => item.Kind == "cards" && item.Scope == "multiplayer-only"));
        Assert.Equal(90, inventory.Items.Count(item => item.Kind == "encounters"));
        Assert.Equal(10, inventory.Items.Count(item => item.Kind == "encounters" && item.Act is null));
        Assert.DoesNotContain(inventory.Items, item => item.Scope == "review-required");
        Assert.Contains(inventory.Items, item => item.Kind == "rest_site_options"
            && item.Id == "MEND" && item.Scope == "multiplayer-only");
        Assert.Contains(inventory.Items, item => item.Kind == "relics"
            && item.Id == "MASSIVE_SCROLL" && item.Scope == "multiplayer-only");
    }

    [Fact]
    public void CoverageDoesNotPromoteMetadataIntoImplementationOrParity()
    {
        var report = V111Coverage.Create();
        Assert.False(report.ReleaseReady);
        Assert.All(report.Items, item => Assert.Equal("unverified", item.Validation));
        Assert.Contains(report.Items, item => item.Kind == "characters" && item.NativeId == "DEFECT"
            && item.Catalogued && item.Implementation == "partial" && item.Integration == "missing");
        Assert.Contains(report.Items, item => item.Kind == "cards" && item.NativeId == "BLADE_SYMPHONY"
            && item.Scope == "multiplayer-only" && item.Implementation == "excluded");
        Assert.Contains(report.Items, item => item.Kind == "events" && item.NativeId == "NEOW"
            && item.MissingBranches.Length > 0);
        Assert.Equal(CanonicalJson.Serialize(report), CanonicalJson.Serialize(V111Coverage.Create()));
    }

    [Theory]
    [InlineData("silent", 70, 12, 0)]
    [InlineData("ironclad", 80, 10, 0)]
    [InlineData("defect", 75, 10, 3)]
    [InlineData("regent", 75, 10, 0)]
    [InlineData("necrobinder", 66, 10, 0)]
    public void CharacterConfigurationsRoundTripWithoutSilentSubstitution(
        string character, int hp, int deckCount, int orbSlots)
    {
        var definition = V111Characters.Get(character);
        Assert.Equal(hp, definition.StartingHp);
        Assert.Equal(deckCount, definition.StartingDeck.Length);
        Assert.Equal(orbSlots, definition.OrbSlots);
        var state = V111RunFactory.Create("character-config", RunConfiguration.Create(character));
        var restored = RunSnapshot.Load(RunSnapshot.Save(state));
        Assert.Equal(character, restored.Configuration!.CharacterId);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(restored));
        if (character != "silent")
            Assert.Throws<NotSupportedException>(() => new PrototypeGameEngine().Step(state, GameAction.Empty("start_run")));
    }

    [Theory]
    [InlineData(ActIdentity.Overgrowth, 0, 12)]
    [InlineData(ActIdentity.Underdocks, 0, 12)]
    [InlineData(ActIdentity.Overgrowth, 5, 13)]
    [InlineData(ActIdentity.Underdocks, 10, 13)]
    public void ConfiguredOpeningHasNativeStarterIdentityAndExplicitFidelity(
        ActIdentity act, int ascension, int deckCount)
    {
        var initial = V111RunFactory.Create("configured-opening", RunConfiguration.Create(ascension: ascension, firstAct: act));
        var opened = new PrototypeGameEngine().Step(initial, GameAction.Empty("start_run")).State;
        Assert.Equal(V111Build.Id, opened.GameBuild);
        Assert.Equal(act, opened.World!.ActIdentity);
        Assert.Equal(deckCount, opened.Player.Deck.Length);
        Assert.DoesNotContain(opened.Player.Deck, card => card.CardId == "proto.common.restlessness");
        Assert.Equal("v111-mechanics-prototype-rng-v1", opened.Configuration!.FidelityId);
        Assert.All(opened.Rng.Streams, stream => Assert.Equal(PrototypeRng.Codec, stream.Codec));
        PrototypeStateInvariants.Validate(opened);
        Assert.Equal(CanonicalJson.Sha256(opened), CanonicalJson.Sha256(RunSnapshot.Load(RunSnapshot.Save(opened))));
    }

    [Fact]
    public void ConfigurationProfileAndResourcesAreOwnedByEachFork()
    {
        var profile = new SimulationProfile(true, ["silent"], ["FIRST"]);
        var configuration = RunConfiguration.Create(profile: profile);
        var state = V111RunFactory.Create("fork-config", configuration);
        var fork = state.Fork();
        fork.Configuration!.Acts[0] = ActIdentity.Underdocks;
        fork.Configuration.Profile.RevealedEpochs[0] = "SECOND";
        Assert.Equal(ActIdentity.Overgrowth, state.Configuration!.Acts[0]);
        Assert.Equal("FIRST", state.Configuration.Profile.RevealedEpochs[0]);
        Assert.Equal("FIRST", profile.RevealedEpochs[0]);
        var resources = new CharacterResourceState(Stars: 3, OrbSlots: 3, Orbs: [new("DARK_ORB", 6)]);
        var player = state.Player with { Resources = resources };
        var playerFork = player.Fork();
        playerFork.Resources!.Orbs![0] = new("FROST_ORB");
        Assert.Equal("DARK_ORB", player.Resources!.Orbs![0].Kind);
    }

    [Fact]
    public void ChangedBuildAscensionOrWorldCannotBypassConfigurationChecks()
    {
        var state = V111RunFactory.Create("identity");
        var engine = new PrototypeGameEngine();
        Assert.Throws<InvalidDataException>(() => engine.GetLegalActions(state with { GameBuild = "another-build" }));
        Assert.Throws<InvalidDataException>(() => engine.Step(state with { Ascension = 1 }, GameAction.Empty("start_run")));
        Assert.Throws<InvalidDataException>(() => engine.GetLegalActions(state with { Configuration = null }));
        Assert.Throws<ArgumentOutOfRangeException>(() => V111RunFactory.Create("bad", RunConfiguration.Create(ascension: 11)));
        var opened = engine.Step(state, GameAction.Empty("start_run")).State;
        Assert.Throws<InvalidDataException>(() => engine.GetLegalActions(opened with
        {
            World = opened.World! with { CharacterId = "ironclad" }
        }));
        var transition = opened with { Phase = RunPhase.ActTransition };
        var hive = engine.Step(transition, GameAction.Empty("continue_act")).State;
        Assert.Equal(2, hive.World!.Act);
        Assert.Equal(ActIdentity.Hive, hive.World.ActIdentity);
        Assert.Equal(PrototypeNativeLaterActRouting.HiveMapProfile,
            hive.World.Map.GenerationProfileId);
    }

    [Fact]
    public void SnapshotIntegritySchemaAndForkIsolationAreChecked()
    {
        var state = new PrototypeGameEngine().Step(PrototypeGameFactory.Create("snapshot"), GameAction.Empty("start_run")).State;
        var encoded = RunSnapshot.Save(state);
        var restored = RunSnapshot.Load(encoded);
        restored.Rng.Streams[0].StateBytes[0] ^= 0xff;
        Assert.NotEqual(CanonicalJson.Sha256(state), CanonicalJson.Sha256(restored));
        var changed = JsonNode.Parse(encoded)!.AsObject();
        changed["state"]!["player"]!["gold"] = 1234;
        Assert.Throws<InvalidDataException>(() => RunSnapshot.Load(changed.ToJsonString()));
        changed = JsonNode.Parse(encoded)!.AsObject();
        changed["schema"] = "unsupported";
        Assert.Throws<InvalidDataException>(() => RunSnapshot.Load(changed.ToJsonString()));
    }

    [Fact]
    public void SequentialAndParallelBatchAgreeAndLeaveParentsUnchanged()
    {
        var roots = Enumerable.Range(0, 16).Select(index => PrototypeGameFactory.Create($"batch-{index}")).ToArray();
        var hashes = roots.Select(CanonicalJson.Sha256).ToArray();
        var requests = roots.Select(state => new BatchStepRequest(state, GameAction.Empty("start_run"))).ToArray();
        var sequential = DeterministicBatch.Step(requests);
        var parallel = DeterministicBatch.Step(requests, 4);
        Assert.Equal(sequential.Select(result => CanonicalJson.Sha256(result.State)),
            parallel.Select(result => CanonicalJson.Sha256(result.State)));
        Assert.Equal(hashes, roots.Select(CanonicalJson.Sha256));
    }
}
