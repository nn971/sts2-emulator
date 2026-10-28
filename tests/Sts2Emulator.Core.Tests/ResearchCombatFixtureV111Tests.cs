using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class ResearchCombatFixtureV111Tests
{
    [Fact]
    public void CuratedDistributionProvidesOneThousandStructurallyDistinctSpecs()
    {
        var fixtures = ResearchCombatFixtureGeneratorV111.GenerateDistinct(1000, 20261027);
        Assert.Equal(1000, fixtures.Length);
        Assert.Equal(1000, fixtures.Select(spec => spec.SpecHash).Distinct().Count());
        Assert.Equal(1000, fixtures.Select(spec =>
            string.Join("|", spec.EncounterId,
                string.Join(",", spec.Deck.Select(card =>
                    card.CardId + ":" + card.UpgradeLevel)),
                string.Join(",", spec.RelicIds), string.Join(",", spec.PotionIds),
                spec.Hp.ToString())).Distinct().Count());
        Assert.All(fixtures, spec =>
        {
            spec.Validate();
            Assert.Equal(20, spec.Deck.Length);
            Assert.Equal(10, spec.Ascension);
            Assert.Equal(3, spec.RelicIds.Length);
            Assert.True(spec.Hp > 0 && spec.Hp <= spec.MaxHp);
        });
        Assert.Equal(
            fixtures.Select(spec => spec.SpecHash),
            ResearchCombatFixtureGeneratorV111.GenerateDistinct(1000, 20261027)
                .Select(spec => spec.SpecHash));
    }

    [Fact]
    public void OneHundredDistinctSpecsAllOpenAsGenuinePlayableCombats()
    {
        var fixtures = ResearchCombatFixtureGeneratorV111.GenerateDistinct(
            100, 20261027);
        var encounters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (spec, index) in fixtures.Select((item, i) => (item, i)))
        {
            // The source RNG seed is deliberately separate from the spec index.
            var state = SemanticPilotCombatEngineV111.BeginSeeded(
                Entry(spec, "research-a10-hundred-openings-" + index));
            Assert.True(state.AtPlayerBoundary);
            Assert.NotEmpty(state.Run.World!.Combat!.Enemies);
            Assert.Equal(spec.Deck.Length, state.Run.World.Combat.Cards.Length);
            Assert.Equal(spec.SpecHash, state.OpeningSource.FixtureSpec!.SpecHash);
            encounters.Add(spec.EncounterId);
        }
        Assert.Equal(ResearchCombatFixtureGeneratorV111.SupportedEncounters.Length,
            encounters.Count);
    }

    [Fact]
    public void SourceWorldSeedIsIndependentOfFixtureIdentity()
    {
        var spec = ResearchCombatFixtureGeneratorV111.Generate(0, 20261027);
        Assert.Equal(spec.SpecHash,
            ResearchCombatFixtureGeneratorV111.Generate(0, 20261027).SpecHash);
        var first = Entry(spec, "research-a10-opening-seed-one");
        var second = Entry(spec, "research-a10-opening-seed-two");
        spec.ValidateFor(first);
        spec.ValidateFor(second);
        Assert.NotEqual(first.Seed, second.Seed);
        Assert.Equal(first.FixtureSpec!.SpecHash, second.FixtureSpec!.SpecHash);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ResearchOpeningPreservesPersistentDeckUpgradesRelicsAndReplay(int index)
    {
        var spec = ResearchCombatFixtureGeneratorV111.Generate(index, 20261027);
        var entry = Entry(spec, "research-a10-open-" + index);
        var first = SemanticPilotCombatEngineV111.BeginSeeded(entry);
        var replay = SemanticPilotCombatEngineV111.Restore(
            SemanticPilotCombatEngineV111.Save(first));
        Assert.Equal(first.Fingerprint, replay.Fingerprint);
        Assert.True(first.AtPlayerBoundary);
        Assert.Equal(spec.Deck.Length, first.Run.Player.Deck.Length);
        Assert.Equal(spec.Deck.Length, first.Run.World!.Combat!.Cards.Length);
        Assert.Equal(spec.Deck.Select(card => card.CardId),
            first.Run.Player.Deck.Select(card => card.CardId));
        Assert.Equal(spec.Deck.Select(card => card.UpgradeLevel),
            first.Run.Player.Deck.Select(card => card.UpgradeLevel));
        Assert.Equal(spec.RelicIds,
            first.Run.Player.Relics.Select(relic => relic.RelicId));
        Assert.Equal(spec.Hp, first.Run.Player.Hp);
        Assert.Equal(spec.MaxHp, first.Run.Player.MaxHp);
        Assert.Equal(spec.PotionIds,
            first.Run.Player.PotionSlots.Select(potion => potion?.PotionId));
        Assert.Equal(spec.SpecHash, first.OpeningSource.FixtureSpec!.SpecHash);
        Assert.Equal(spec.Deck.Length,
            first.Run.Player.Deck.Select(card => card.InstanceId).Distinct().Count());
        Assert.Equal(spec.Deck.Length,
            first.Run.World.Combat.Cards
                .Select(card => card.PersistentCardInstanceId)
                .Distinct().Count());
    }

    [Fact]
    public void BadInitialConditionsFailBeforeEntryChanceSampling()
    {
        var valid = ResearchCombatFixtureGeneratorV111.Generate(0);
        Assert.Throws<NotSupportedException>(() =>
            (valid with { Deck = valid.Deck.Select((card, i) =>
                i == 0 ? new ResearchCombatCardV111("unknown.card") : card)
                .ToArray() }).Validate());
        Assert.Throws<InvalidDataException>(() =>
            (valid with
            {
                Deck = valid.Deck.Select(card => card with
                {
                    UpgradeLevel = 999
                }).ToArray()
            }).Validate());
        Assert.Throws<InvalidDataException>(() =>
            (valid with
            {
                RelicIds = ["proto.relic.silent_ring", "proto.relic.lantern",
                    "proto.relic.lantern"]
            }).Validate());
        Assert.Throws<NotSupportedException>(() =>
            (valid with
            {
                PotionIds = ["proto.potion.snecko_oil", null]
            }).Validate());
        Assert.Throws<InvalidDataException>(() =>
            (valid with { Hp = 0 }).Validate());
        Assert.Throws<NotSupportedException>(() =>
            (valid with { TotalFloor = 3 }).Validate());
        Assert.Throws<NotSupportedException>(() =>
            valid.ValidateFor(Entry(valid, "research-invalid-entry") with
            {
                FixtureDeckPresetId = SemanticConditionedDeckPresetV111.PoisonKit
            }));
    }

    [Theory]
    [InlineData("proto.potion.block")]
    [InlineData("proto.potion.fire")]
    [InlineData("proto.potion.poison")]
    public void H8StructuredLegalPotionIsExecutableAndReplays(
        string potionId)
    {
        // Boss HP ensures Fire Potion remains an ordinary combat action.
        var spec = ResearchCombatFixtureGeneratorV111
            .GenerateDistinct(100, 20261027)
            .First(item => item.IsBoss) with
        {
            PotionIds = [potionId, null]
        };
        var runtime = H8CombatRuntimeV1.BeginSeeded(
            Entry(spec, "h9-research-potion-" + potionId));
        runtime.AdvanceLocalReferenceNature(runtime.Boundary, 628391);
        Assert.True(runtime.AtPlayerBoundary);
        var previous = runtime.LegacySnapshotForAudit();
        var beforeCombat = previous.Run.World!.Combat!;
        var beforeHp = beforeCombat.Enemies[0].Hp;
        var beforeBlock = beforeCombat.PlayerBlock;

        var potionAction = Assert.Single(
            runtime.ObservePlayerFrame().LegalActions,
            action => action.Kind == "use_potion");
        runtime.ChoosePlayerAction(runtime.Boundary, potionAction.ActionId);
        runtime.AuditReplay();

        var current = runtime.LegacySnapshotForAudit();
        Assert.Null(current.Run.Player.PotionSlots[0]);
        Assert.True(current.AtPlayerBoundary || current.CombatTerminal);
        var afterCombat = current.Run.World?.Combat;
        switch (potionId)
        {
            case "proto.potion.block":
                Assert.NotNull(afterCombat);
                Assert.Equal(beforeBlock + 12, afterCombat.PlayerBlock);
                break;
            case "proto.potion.fire":
                if (afterCombat is not null)
                    Assert.True(afterCombat.Enemies[0].Hp < beforeHp);
                else
                    Assert.Equal("victory", current.CombatOutcome);
                break;
            case "proto.potion.poison":
                Assert.NotNull(afterCombat);
                Assert.Equal(6, afterCombat.Enemies[0].Statuses
                    .GetValueOrDefault("proto.status.poison"));
                break;
        }
    }

    [Fact]
    public void NativeH8LethalFirePotionCompletesRealRewardAndReplays()
    {
        // An all-Strike deck guarantees enough direct damage to place the
        // Shrinker Beetle within Fire Potion range on a legal player turn.
        // This exercises a genuine win/regular card-reward RNG transition,
        // never a fabricated postcombat state.
        var spec = ResearchCombatFixtureGeneratorV111.Generate(0, 20261027)
            with
            {
                EncounterId = "proto.encounter.shrinker_beetle_weak",
                NativeEncounterEntry = "ShrinkerBeetleWeak",
                TotalFloor = 2,
                MapRow = 1,
                Deck = Enumerable.Range(0, 20)
                    .Select(_ => new ResearchCombatCardV111("proto.silent.strike"))
                    .ToArray(),
                RelicIds = ["proto.relic.silent_ring",
                    "proto.relic.lantern", "proto.relic.vajra"],
                PotionIds = ["proto.potion.fire", null]
            };
        spec.Validate();
        var runtime = H8CombatRuntimeV1.BeginSeeded(
            Entry(spec, "h9-lethal-fire-potion-reward-20261028"));
        const long natureSeed = 76139;
        for (int decision = 0; decision < 35; decision++)
        {
            if (!runtime.AtPlayerBoundary)
                runtime.AdvanceLocalReferenceNature(runtime.Boundary, natureSeed);
            Assert.False(runtime.Terminal,
                "Expected to reach a legal killing potion action.");
            var combat = runtime.LegacySnapshotForAudit().Run.World!.Combat!;
            var enemy = Assert.Single(combat.Enemies);
            var actions = runtime.ObservePlayerFrame().LegalActions;
            if (enemy.Hp <= 20)
            {
                Assert.True(enemy.Hp > 0);
                var fire = Assert.Single(actions,
                    action => action.Kind == "use_potion");
                runtime.ChoosePlayerAction(runtime.Boundary, fire.ActionId);
                Assert.True(runtime.Terminal);
                Assert.Equal("victory", runtime.Outcome);
                Assert.Equal(RunPhase.Reward,
                    runtime.LegacySnapshotForAudit().Run.Phase);
                runtime.AuditReplay();
                return;
            }

            var strike = actions.FirstOrDefault(action =>
                action.Kind == "play_card");
            runtime.ChoosePlayerAction(runtime.Boundary,
                strike?.ActionId ?? runtime.EndTurnActionId());
        }
        Assert.Fail("Source-supported Strike + Fire combat did not reach potion range.");
    }


    [Theory]
    [InlineData("proto.encounter.slimes_weak")]
    [InlineData("proto.encounter.inklets_normal")]
    [InlineData("proto.encounter.ruby_raiders_normal")]
    [InlineData("proto.encounter.phrog_parasite_elite")]
    [InlineData("proto.encounter.the_kin_boss")]
    public void RichV2NewEncounterOpensWithNativeResearchRuntime(string encounterId)
    {
        var encounter = Assert.Single(
            ResearchCombatFixtureGeneratorV111.RichSupportedEncounters,
            item => item.EncounterId == encounterId);
        var legacy = ResearchCombatFixtureGeneratorV111.Generate(0, 20261027);
        var rich = legacy with
        {
            DistributionId = ResearchCombatFixtureGeneratorV111.RichDistributionId,
            EncounterId = encounter.EncounterId,
            NativeEncounterEntry = encounter.NativeEntry,
            TotalFloor = encounter.TotalFloor,
            MapRow = encounter.MapRow,
            RelicIds = ["proto.relic.silent_ring",
                        "proto.relic.bag_of_preparation"],
            Deck = legacy.Deck.Append(
                new ResearchCombatCardV111("proto.silent.blade_dance")
            ).ToArray(),
            PotionIds = ["proto.potion.swift", "proto.potion.fire"]
        };
        rich.Validate();
        Assert.Throws<NotSupportedException>(() =>
            (rich with
            {
                DistributionId = ResearchCombatFixtureGeneratorV111.DistributionId
            }).Validate());
        var runtime = H8CombatRuntimeV1.BeginSeeded(
            Entry(rich, "h9-rich-v2-accept-" + encounterId));
        runtime.AdvanceLocalReferenceNature(runtime.Boundary, 20261028);
        Assert.True(runtime.AtPlayerBoundary);
        Assert.NotEmpty(runtime.ObservePlayerFrame().LegalActions);
        var world = runtime.LegacySnapshotForAudit().Run.World!;
        var combat = world.Combat!;
        var enemies = combat.Enemies;
        Assert.NotEmpty(enemies);
        Assert.Equal(PrototypeContent.Encounter(encounterId).RoomType,
            world.ActiveRoom);
        Assert.Equal(encounter.TotalFloor, world.Floor);
        var orders = combat.PlayerPowers.Select(p => p.ApplicationOrder)
            .Concat(enemies.SelectMany(enemy =>
                enemy.PowerStates.Select(p => p.ApplicationOrder)))
            .Concat(combat.RelicStates.Select(relic => relic.ApplicationOrder))
            .ToArray();
        Assert.Equal(orders.Length, orders.Distinct().Count());
        Assert.All(orders, order =>
            Assert.InRange(order, 1, combat.NextPowerApplicationOrder - 1));
        if (encounterId is "proto.encounter.inklets_normal"
            or "proto.encounter.ruby_raiders_normal"
            or "proto.encounter.the_kin_boss")
            Assert.True(enemies.Length >= 2, "Expected multi-enemy formation");
        runtime.AuditReplay();
    }

    // A legal action and an end turn exercise the real H8 session beyond
    // its opening. In particular, more than one enemy must commit intents
    // to the same combat checkpoint, and Swift must use the Nature draw
    // provider without swallowing its public player action.
    [Theory]
    [InlineData("proto.encounter.inklets_normal")]
    [InlineData("proto.encounter.the_kin_boss")]
    public void RichMultiEnemyTurnAndSwiftPotionAdvance(string encounterId)
    {
        var encounter = Assert.Single(
            ResearchCombatFixtureGeneratorV111.RichSupportedEncounters,
            item => item.EncounterId == encounterId);
        var original = ResearchCombatFixtureGeneratorV111.Generate(0, 20261027);
        var fixture = original with
        {
            DistributionId = ResearchCombatFixtureGeneratorV111.RichDistributionId,
            EncounterId = encounter.EncounterId,
            NativeEncounterEntry = encounter.NativeEntry,
            TotalFloor = encounter.TotalFloor,
            MapRow = encounter.MapRow,
            PotionIds = ["proto.potion.swift", null]
        };
        var session = H8CombatRuntimeV1.BeginSeeded(
            Entry(fixture, "h9-rich-action-" + encounterId));
        session.AdvanceLocalReferenceNature(session.Boundary, 20261028);
        Assert.True(session.AtPlayerBoundary);
        var potion = Assert.Single(
            session.ObservePlayerFrame().LegalActions,
            action => action.Kind == "use_potion");
        session.ChoosePlayerAction(session.Boundary, potion.ActionId);
        session.AdvanceLocalReferenceNature(session.Boundary, 20261028);
        Assert.True(session.AtPlayerBoundary);
        var end = Assert.Single(
            session.ObservePlayerFrame().LegalActions,
            action => action.Kind == "end_turn");
        session.ChoosePlayerAction(session.Boundary, end.ActionId);
        session.AdvanceLocalReferenceNature(session.Boundary, 20261028);
        session.AuditReplay();
    }

    [Theory]
    [InlineData("proto.encounter.vantom_boss")]
    [InlineData("proto.encounter.inklets_normal")]
    public void RichBouncingFlaskUsesReplayableRandomTargetNature(
        string encounterId)
    {
        var encounter = Assert.Single(
            ResearchCombatFixtureGeneratorV111.RichSupportedEncounters,
            item => item.EncounterId == encounterId);
        var fixture = ResearchCombatFixtureGeneratorV111
            .Generate(0, 20261027) with
        {
            DistributionId = ResearchCombatFixtureGeneratorV111.RichDistributionId,
            EncounterId = encounter.EncounterId,
            NativeEncounterEntry = encounter.NativeEntry,
            TotalFloor = encounter.TotalFloor,
            MapRow = encounter.MapRow,
            Deck = Enumerable.Range(0, 12)
                .Select(_ => new ResearchCombatCardV111(
                    "proto.silent.bouncing_flask"))
                .ToArray(),
            PotionIds = [null, null]
        };
        var runtime = H8CombatRuntimeV1.BeginSeeded(
            Entry(fixture, "h9-rich-bouncing-flask-" + encounterId));
        runtime.AdvanceLocalReferenceNature(runtime.Boundary, 20261028);
        Assert.True(runtime.AtPlayerBoundary);
        var flask = runtime.ObservePlayerFrame().LegalActions
            .First(action => action.Kind == "play_card");
        runtime.ChoosePlayerAction(runtime.Boundary, flask.ActionId);
        Assert.Equal("random_target", runtime.BoundaryKind);
        var eventId = runtime.PendingRandomTargetEventId();
        var ids = runtime.PendingRandomTargetEnemyIds();
        Assert.NotEmpty(ids);
        runtime.ChooseRandomTarget(
            runtime.Boundary, eventId, ids[0], "test-reference");
        runtime.AdvanceLocalReferenceNature(runtime.Boundary, 20261028);
        Assert.True(runtime.AtPlayerBoundary || runtime.Terminal);
        Assert.Contains(runtime.DecisionHistory,
            item => item.Kind == "random_target" &&
                item.TargetEventId == eventId &&
                item.TargetEnemyInstanceId == ids[0]);
        runtime.AuditReplay();
    }

    [Fact]
    public void GuardedDeathSummonHpUsesJournaledCombatStreamOnlyInsideItsScope()
    {
        const string site = NativeCombatRngEntryV111.InitialUniqueHpSiteId;
        var rng = PrototypeRng.CreateBundle("h9-rich-death-summon-hp");
        RandomIndexDecisionV111 chosen;
        using (ControlledChanceGuardV111.Enter())
        {
            Assert.Throws<NotSupportedException>(() =>
                LegacyRandomChoiceV111.ChooseIndex(
                    rng, site, "combat", "creature:2", [7, 8, 9]));
            using (ControlledChanceGuardV111.AllowEngineOwnedDeathSummonHp())
            {
                chosen = LegacyRandomChoiceV111.ChooseIndex(
                    rng, site, "combat", "creature:2", [7, 8, 9]);
                Assert.Equal("legacy-prototype-v0", chosen.ProviderMode);
                Assert.InRange(chosen.SelectedValue, 7, 9);
                Assert.Throws<NotSupportedException>(() =>
                    PrototypeRng.NextInt(rng, "reward", 3));
            }
            Assert.Throws<NotSupportedException>(() =>
                LegacyRandomChoiceV111.ChooseIndex(
                    rng, site, "combat", "creature:3", [7, 8, 9]));
        }
        // The exact deterministic source profile is retained; this scope
        // changes admission, never the random outcome or stream cursor.
        var baseline = LegacyRandomChoiceV111.ChooseIndex(
            PrototypeRng.CreateBundle("h9-rich-death-summon-hp"),
            site, "combat", "creature:2", [7, 8, 9]);
        Assert.Equal(baseline.SelectedIndex, chosen.SelectedIndex);
        Assert.Equal((ulong)1, rng.Streams.Single(x =>
            x.StreamId == "combat").CallCount ?? 0UL);
    }

    [Fact]
    public void RichFixtureAllowlistDoesNotAlterLegacyV1Generator()
    {
        var original = ResearchCombatFixtureGeneratorV111.Generate(0, 20261027);
        Assert.Equal(ResearchCombatFixtureGeneratorV111.DistributionId,
            original.DistributionId);
        Assert.Equal(20, original.Deck.Length);
        Assert.Equal(7, ResearchCombatFixtureGeneratorV111.SupportedEncounters.Length);
        Assert.Equal(21, ResearchCombatFixtureGeneratorV111.RichSupportedEncounters.Length);
    }

    private static SemanticSeededEntryV111 Entry(
        ResearchCombatFixtureV111 spec, string sourceSeed) => new(
        sourceSeed, spec.Act, spec.EncounterId, spec.NativeEncounterEntry,
        spec.TotalFloor, spec.Ascension, 1, spec.MapColumn, spec.MapRow,
        ReplayOpeningDrawHooks: true,
        FixtureSpec: spec);
}
