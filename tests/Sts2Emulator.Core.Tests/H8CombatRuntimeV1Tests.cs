using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class H8CombatRuntimeV1Tests
{
    private static SemanticSeededEntryV111 Mawler(string seed) =>
        new(seed, 1, "proto.encounter.mawler_normal", "MawlerNormal",
            2, 10, 1, 3, 1, ReplayOpeningDrawHooks: true);

    private static H8CombatRuntimeV1 ReachNaturalDraw(string seed)
    {
        var runtime = H8CombatRuntimeV1.BeginSeeded(Mawler(seed));
        for (var step = 0; !runtime.HasPendingCardDraw; step++)
        {
            Assert.True(step < 200,
                "Source-valid A10 Mawler must reach an actual deck draw.");
            var boundary = runtime.Boundary;
            if (runtime.HasPendingIntent)
            {
                var intentId = runtime.PendingIntentIds()[0];
                runtime.ChooseIntent(boundary, intentId);
            }
            else if (runtime.AtPlayerBoundary && !runtime.Terminal)
            {
                var state = runtime.LegacySnapshotForAudit();
                var action = Assert.Single(
                    new PrototypeAiEnvironment().Observe(state.Run).LegalActions,
                    a => a.Kind == "end_turn");
                runtime.ChoosePlayerAction(boundary, action.ActionId);
            }
            else
            {
                Assert.Fail(
                    "H8 bridge encountered an unimplemented boundary before draw.");
            }
        }
        return runtime;
    }

    [Fact]
    public void ControlledDrawSkipsInputHashWhilePreservingCheckedMechanicsAndReplay()
    {
        var runtime = ReachNaturalDraw("h8-versioned-mawler-draw-v1");
        var source = runtime.LegacySnapshotForAudit();
        var pending = Assert.IsType<SemanticDeckEventV111>(source.DrawPending);
        var selected = pending.CandidateCardInstanceIds[^1];
        var expected = SemanticPilotCombatEngineV111.ResolveDrawCard(
            source, source.Fingerprint, pending.EventId, selected);

        var boundary = runtime.Boundary;
        var oldCount = runtime.DecisionHistory.Count;
        var sourceFingerprint = source.Fingerprint;
        Assert.Throws<InvalidOperationException>(() =>
            runtime.ChooseDrawCard(
                new H8BoundaryTokenV1(Guid.NewGuid(), boundary.Version),
                selected));
        Assert.Throws<InvalidOperationException>(() =>
            runtime.ChooseDrawCard(boundary, long.MaxValue));
        Assert.Equal(boundary, runtime.Boundary);
        Assert.Equal(oldCount, runtime.DecisionHistory.Count);
        Assert.Equal(sourceFingerprint,
            runtime.LegacySnapshotForAudit().Fingerprint);

        var stages = new List<string>();
        using (SemanticDrawStageProfilerV111.Begin(
            (name, _, _) => stages.Add(name)))
        {
            runtime.ChooseDrawCard(boundary, selected, "scripted-h8-test");
        }
        Assert.DoesNotContain("sub.semantic_draw_pilot_input_validate", stages);
        Assert.DoesNotContain("sub.semantic_draw_pilot_input_fingerprint", stages);
        // H8 M2 owns this automatic-hand-draw cursor. The actual gameplay
        // transition still executes, while full nested audits run only when
        // AuditReplay is explicitly requested.
        Assert.DoesNotContain("sub.semantic_draw_endturn_state_fork", stages);
        Assert.DoesNotContain("sub.semantic_draw_combat_output_validate", stages);
        Assert.DoesNotContain("sub.semantic_draw_endturn_output_validate", stages);
        Assert.Contains("sub.semantic_draw_combat_engine_mechanics", stages);
        Assert.Equal(expected.Fingerprint,
            runtime.LegacySnapshotForAudit().Fingerprint);
        Assert.Equal(boundary.Version + 1, runtime.Boundary.Version);
        var decision = runtime.DecisionHistory[^1];
        Assert.Equal("draw_card", decision.Kind);
        Assert.Equal(selected, decision.CardInstanceId);
        Assert.Equal("scripted-h8-test", decision.ProviderId);
        Assert.Equal(pending.SiteId, decision.SiteId);
        runtime.AuditReplay();
    }

    [Fact]
    public void ForkHasIndependentRuntimeIdentityAndDeterministicJournal()
    {
        var runtime = ReachNaturalDraw("h8-versioned-mawler-fork-v1");
        var branch = runtime.Fork();
        Assert.NotEqual(runtime.Boundary.RuntimeId, branch.Boundary.RuntimeId);
        Assert.Equal(runtime.Boundary.Version, branch.Boundary.Version);
        Assert.Equal(runtime.DecisionHistory, branch.DecisionHistory);

        var source = runtime.LegacySnapshotForAudit();
        var candidates = Assert.IsType<SemanticDeckEventV111>(
            source.DrawPending).CandidateCardInstanceIds;
        Assert.NotEmpty(candidates);

        var token = runtime.Boundary;
        Assert.Throws<InvalidOperationException>(() =>
            branch.ChooseDrawCard(token, candidates[0]));
        var branchToken = branch.Boundary;
        runtime.ChooseDrawCard(token, candidates[0]);
        branch.ChooseDrawCard(branchToken, candidates[^1]);

        Assert.Equal(token.Version + 1, runtime.Boundary.Version);
        Assert.Equal(branchToken.Version + 1, branch.Boundary.Version);
        Assert.NotEqual(runtime.Boundary.RuntimeId, branch.Boundary.RuntimeId);
        Assert.Throws<InvalidOperationException>(() =>
            runtime.ChooseDrawCard(token, candidates[0]));
        runtime.AuditReplay();
        branch.AuditReplay();
    }
}
