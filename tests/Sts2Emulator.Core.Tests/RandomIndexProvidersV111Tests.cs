using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// First migration gate: v111 native seeded HP results/cursors MUST remain
/// bit-identical. Scripted and controlled providers are strictly standalone
/// building blocks until serializable Nature continuations (D3).
/// </summary>
public sealed class RandomIndexProvidersV111Tests
{
    private static NativeCombatRngV111 Ready(string seed) =>
        PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
            seed, 2, PrototypeNativeHiveNormals.MytesNormalId,
            "MytesNormal", 21, mapColumn: 3, mapRow: 5,
            commitInitialIntents: false).NativeRng!;

    [Fact]
    public void NativeHpCallSiteMaintainsOriginalSeededStateAndCursors()
    {
        for (int seedIndex = 0; seedIndex < 64; seedIndex++)
        {
            string seed = "desired-native-hp-" + seedIndex;
            var previouslyAttached = PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
                seed, 2, PrototypeNativeHiveNormals.MytesNormalId,
                "MytesNormal", 21, mapColumn: 3, mapRow: 5,
                commitInitialIntents: false);
            var fresh = NativeRngV111.CreateBundle(seed);
            var bare = previouslyAttached with { NativeRng = null };
            var oldHash = CanonicalJson.Sha256(previouslyAttached);
            var audit = NativeCombatRngEntryV111.AttachAndRollInitialHpWithAudit(
                bare, previouslyAttached.NativeRng!.Formation.Fork(), fresh);

            // Hash equality includes all exact RNG stream words and counters.
            Assert.Equal(oldHash, CanonicalJson.Sha256(audit.Combat));
            Assert.Equal("native-v111", audit.ProviderMode);
            Assert.Equal(bare.Enemies.Length, audit.Choices.Length);
            Assert.Equal(0, NativeRngV111.ToSnapshot(
                fresh.Streams.Single(s => s.StreamId == "run.niche")).Counter);
            // Primitive oracle independent of the new request/provider:
            // run the original pinned NextInt sequentially from a fresh
            // native stream and require identical selected indices and
            // post-call *complete* xoshiro state.
            var primitiveOracle = NativeRngV111.CreateBundle(seed);
            foreach (var choice in audit.Choices)
            {
                int legacySelection = NativeRngV111.NextInt(
                    primitiveOracle, "run.niche",
                    choice.Request.OrderedCandidates.Length);
                Assert.Equal(legacySelection, choice.SelectedIndex);
            }
            Assert.Equal(CanonicalJson.Sha256(primitiveOracle),
                CanonicalJson.Sha256(audit.Combat.NativeRng!.RunStreams));

            foreach (var (choice, index) in audit.Choices.Select((v, i) => (v, i)))
            {
                Assert.Equal(index, choice.Request.Ordinal);
                Assert.Equal("combat.initial_unique_hp", choice.Request.SiteId);
                Assert.Equal("run.niche", choice.Request.StreamId);
                Assert.Equal("creature:" +
                    previouslyAttached.NativeRng.Formation.MonsterContexts[index].CombatId,
                    choice.Request.OwnerId);
                Assert.Equal(audit.Combat.Enemies[index].Hp, choice.SelectedValue);
                Assert.Equal(index, choice.NativeCursorBefore!.Counter);
                Assert.Equal(index + 1, choice.NativeCursorAfter!.Counter);
                Assert.Equal("UNVERIFIED", choice.ProbabilityStatus);
            }
        }
    }

    [Fact]
    public void SingleEligibleOutcomeStillConsumesNativeCursor()
    {
        var native = new NativeRandomIndexProviderV111();
        var streams = NativeRngV111.CreateBundle("native-singleton");
        var request = new RandomIndexRequestV111(
            "combat.initial_unique_hp", "run.niche", "creature:1", 0, [35]);
        var selected = native.Resolve(request, streams);
        Assert.Equal(0, selected.SelectedIndex);
        Assert.Equal(35, selected.SelectedValue);
        Assert.Equal(0, selected.NativeCursorBefore!.Counter);
        Assert.Equal(1, selected.NativeCursorAfter!.Counter);
        Assert.Equal(1, NativeRngV111.ToSnapshot(
            streams.Streams.Single(s => s.StreamId == "run.niche")).Counter);
    }

    [Fact]
    public void ScriptedReplayUsesExactOwnerOrderAndEligibilityWithoutAdvancingNativeRng()
    {
        const string seed = "scripted-hp-replay";
        var pre = PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
            seed, 2, PrototypeNativeHiveNormals.MytesNormalId,
            "MytesNormal", 21, mapColumn: 3, mapRow: 5,
            commitInitialIntents: false);
        var audit = NativeCombatRngEntryV111.AttachAndRollInitialHpWithAudit(
            pre with { NativeRng = null }, pre.NativeRng!.Formation.Fork(),
            NativeRngV111.CreateBundle(seed));
        var streams = NativeRngV111.CreateBundle("counterfactual-cursors-do-not-move");
        string original = CanonicalJson.Sha256(streams);
        var scripted = new ScriptedRandomIndexProviderV111(audit.Choices);
        foreach (var item in audit.Choices)
        {
            var resolved = scripted.Resolve(item.Request, streams);
            Assert.Equal(item.SelectedValue, resolved.SelectedValue);
            Assert.Equal("scripted-v111", resolved.ProviderMode);
            Assert.Null(resolved.NativeCursorAfter);
        }
        scripted.AssertComplete();
        Assert.Equal(0, scripted.Remaining);
        Assert.Equal(original, CanonicalJson.Sha256(streams));
        Assert.Throws<InvalidOperationException>(() =>
            scripted.Resolve(audit.Choices[0].Request, streams));

        var failed = new ScriptedRandomIndexProviderV111(audit.Choices);
        var originalRequest = audit.Choices[0].Request;
        Assert.Throws<InvalidOperationException>(() =>
            failed.Resolve(originalRequest with { OwnerId = "creature:wrong" }, streams));
        Assert.Equal(audit.Choices.Length, failed.Remaining);
        Assert.Throws<InvalidOperationException>(() =>
            failed.Resolve(originalRequest with { OrderedCandidates =
                originalRequest.OrderedCandidates.Reverse().ToArray() }, streams));
        Assert.Equal(audit.Choices.Length, failed.Remaining);
        Assert.Equal(original, CanonicalJson.Sha256(streams));
        Assert.Throws<InvalidOperationException>(() => failed.AssertComplete());
    }

    [Fact]
    public void ControlledChoiceRejectsIllegalOutcomeAndDoesNotAdvanceNativeCursor()
    {
        var streams = NativeRngV111.CreateBundle("controlled-does-not-consume");
        string initialHash = CanonicalJson.Sha256(streams);
        var req = new RandomIndexRequestV111(
            "combat.initial_unique_hp", "run.niche", "creature:1", 0,
            [11, 13, 15]);
        var outcome = new ControlledRandomIndexProviderV111(1).Resolve(req, streams);
        Assert.Equal(13, outcome.SelectedValue);
        Assert.Equal("controlled-v111", outcome.ProviderMode);
        Assert.Equal("UNVERIFIED", outcome.ProbabilityStatus);
        Assert.Null(outcome.NativeCursorAfter);
        Assert.Throws<ArgumentException>(() =>
            new ControlledRandomIndexProviderV111(3).Resolve(req, streams));
        Assert.Throws<ArgumentException>(() =>
            new ControlledRandomIndexProviderV111(-1).Resolve(req, streams));
        Assert.Equal(initialHash, CanonicalJson.Sha256(streams));
    }

    [Fact]
    public void LargeBoundedTicketIsCompactAndSingletonStillConsumesNativeDraw()
    {
        var streams = NativeRngV111.CreateBundle("large-weight-space");
        var request = new RandomIndexRequestV111(
            "combat.committed_intent.index_draw",
            "run.monster_ai", "creature:42", 0, [],
            OperationKind: "bounded_integer", BoundedCount: int.MaxValue);
        Assert.Equal(int.MaxValue, request.SupportCount);
        Assert.Empty(request.OrderedCandidates);
        var sampled = new NativeRandomIndexProviderV111().Resolve(request, streams);
        Assert.InRange(sampled.SelectedIndex, 0, int.MaxValue - 1);
        Assert.Equal(sampled.SelectedIndex, sampled.SelectedValue);
        Assert.Equal(1, sampled.NativeCursorAfter!.Counter);
        var singleton = request with { Ordinal = 1, BoundedCount = 1 };
        var chosen = new NativeRandomIndexProviderV111().Resolve(singleton, streams);
        Assert.Equal(0, chosen.SelectedIndex);
        Assert.Equal(2, chosen.NativeCursorAfter!.Counter);
        Assert.Throws<ArgumentException>(() =>
            new ControlledRandomIndexProviderV111(1).Resolve(singleton, streams));
    }

    [Theory]
    [InlineData("reward.potion_odds.ticket", false)]
    [InlineData("reward.combat_gold.index_draw", false)]
    [InlineData("potion.rarity_roll.ticket", false)]
    [InlineData("potion.model_within_rarity.index_draw", true)]
    [InlineData("card_rarity.roll.ticket", false)]
    [InlineData("card_rarity.encounter_model.index_draw", true)]
    [InlineData("card_rarity.upgrade_roll.ticket", false)]
    public void GuardedNativeStructureCombatRewardUsesOnlyNamedRewardSites(
        string siteId, bool chooseIndex)
    {
        var streams = PrototypeRng.CreateBundle(
            "h15-postcombat-reward-site:" + siteId);
        var hash = CanonicalJson.Sha256(streams);

        RandomIndexDecisionV111 Select(string stream = "reward") =>
            chooseIndex
                ? LegacyRandomChoiceV111.ChooseIndexRange(
                    streams, siteId, stream, "h15:test:reward", 3)
                : LegacyRandomChoiceV111.ChooseBoundedInteger(
                    streams, siteId, stream, "h15:test:reward", 1000);

        using (ControlledChanceGuardV111.Enter())
        {
            // No implicit gameplay reward delegation outside the narrow
            // source EnterReward scope.
            Assert.Throws<NotSupportedException>(() => Select());
            Assert.Equal(hash, CanonicalJson.Sha256(streams));

            using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
            {
                var accepted = Select();
                Assert.Equal(siteId, accepted.Request.SiteId);
                Assert.Equal("reward", accepted.Request.StreamId);
                Assert.Equal(
                    chooseIndex ? "choose_index" : "bounded_integer",
                    accepted.Request.OperationKind);

                // The scoped permission cannot redirect to a different
                // stream or authorize unrelated hidden combat chance.
                Assert.Throws<NotSupportedException>(() => Select("combat"));
                Assert.Throws<NotSupportedException>(() =>
                    LegacyRandomChoiceV111.ChooseBoundedInteger(
                        streams, "combat.committed_intent.index_draw",
                        "combat", "h15:test", 3));
                Assert.Throws<NotSupportedException>(() =>
                    LegacyRandomChoiceV111.ChooseBoundedInteger(
                        streams, "reward.unaudited.ticket",
                        "reward", "h15:test", 3));
            }
            Assert.Throws<NotSupportedException>(() => Select());
        }
    }

    [Fact]
    public void GuardedLegacyDeathSummonHpConsumesOnlyItsScopedCombatDraw()
    {
        const string seed = "h10-legacy-phrog-death-summon-hp";
        var streams = PrototypeRng.CreateBundle(seed);
        var before = CanonicalJson.Sha256(streams);
        var site = NativeCombatRngEntryV111.InitialUniqueHpSiteId;

        using (ControlledChanceGuardV111.Enter())
        {
            // Ordinary guarded combat cannot use the legacy HP site.
            var blocked = Assert.Throws<NotSupportedException>(() =>
                LegacyRandomChoiceV111.ChooseIndex(
                    streams, site, "combat", "creature:5", [18, 19, 20]));
            Assert.Contains("requires a saved continuation", blocked.Message);
            Assert.Equal(before, CanonicalJson.Sha256(streams));

            // The real death-summon resolver enters precisely this scope.
            using (ControlledChanceGuardV111.AllowEngineOwnedDeathSummonHp())
            {
                var decision = LegacyRandomChoiceV111.ChooseIndex(
                    streams, site, "combat", "creature:5", [18, 19, 20]);
                Assert.Equal("legacy-prototype-v0", decision.ProviderMode);
                Assert.Equal(site, decision.Request.SiteId);
                Assert.Equal("combat", decision.Request.StreamId);
                Assert.Equal("creature:5", decision.Request.OwnerId);
                Assert.InRange(decision.SelectedValue, 18, 20);

                // Even inside the narrow scope, unrelated RNG remains guarded.
                Assert.Throws<NotSupportedException>(() =>
                    PrototypeRng.NextInt(streams, "combat_targets", 2));
            }

            Assert.Throws<NotSupportedException>(() =>
                LegacyRandomChoiceV111.ChooseIndex(
                    streams, site, "combat", "creature:6", [21, 22]));
        }

        var oracle = PrototypeRng.CreateBundle(seed);
        _ = PrototypeRng.NextInt(oracle, "combat", 3);
        Assert.Equal(CanonicalJson.Sha256(oracle),
            CanonicalJson.Sha256(streams));
    }

    [Fact]
    public void InvalidRequestsFailBeforeConsumingRng()
    {
        var streams = NativeRngV111.CreateBundle("invalid-zero-chance");
        var before = CanonicalJson.Sha256(streams);
        var provider = new NativeRandomIndexProviderV111();
        Assert.Throws<ArgumentException>(() => provider.Resolve(new(
            "site", "run.niche", "owner", 0, []), streams));
        Assert.Throws<ArgumentException>(() => provider.Resolve(new(
            "site", "run.niche", "owner", -1, [1]), streams));
        Assert.Throws<InvalidOperationException>(() => provider.Resolve(new(
            "site", "run.invalid", "owner", 0, [1]), streams));
        Assert.Equal(before, CanonicalJson.Sha256(streams));
    }
}
