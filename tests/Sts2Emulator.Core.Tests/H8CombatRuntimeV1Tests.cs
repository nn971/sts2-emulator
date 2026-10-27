using System.Diagnostics;
using Sts2Emulator.Core;
using Xunit.Abstractions;

namespace Sts2Emulator.Core.Tests;

public sealed class H8CombatRuntimeV1Tests
{
    private readonly ITestOutputHelper _output;

    public H8CombatRuntimeV1Tests(ITestOutputHelper output) =>
        _output = output;

    private static SemanticSeededEntryV111 Mawler(string seed) =>
        new(seed, 1, "proto.encounter.mawler_normal", "MawlerNormal",
            2, 10, 1, 3, 1, ReplayOpeningDrawHooks: true);

    private static H8CombatRuntimeV1 ReachNaturalDraw(
        string seed, bool useM2AutomaticDraw = true,
        bool useNativeDrawTransition = true)
    {
        var runtime = H8CombatRuntimeV1.BeginSeeded(
            Mawler(seed), useM2AutomaticDraw, useNativeDrawTransition);
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

    private static H8CombatRuntimeV1 ReachFirstFullPlayerBoundary(
        string seed, bool useNativePlayerEndTurn)
    {
        var runtime = H8CombatRuntimeV1.BeginSeeded(
            Mawler(seed), useNativePlayerEndTurn: useNativePlayerEndTurn);
        for (var i = 0; i < 12; i++)
        {
            if (runtime.LegacySnapshotForAudit().AtPlayerBoundary)
                return runtime;
            Assert.True(runtime.HasPendingIntent,
                "Expected initial enemy-intent choice before first player turn.");
            runtime.ChooseIntent(runtime.Boundary, runtime.PendingIntentIds()[0]);
        }
        throw new InvalidOperationException("No A10 player decision reached.");
    }

    [Fact]
    public void H8PublicTransformerFrameMatchesExactCheckedSourceObservation()
    {
        var runtime = ReachFirstFullPlayerBoundary(
            "h8-h7-public-observe-a10", useNativePlayerEndTurn: true);
        var oracle = new PrototypeAiEnvironment().Observe(
            runtime.LegacySnapshotForAudit().Run);
        var frame = runtime.ObservePlayerFrame();
        Assert.Equal(CanonicalJson.Sha256(oracle.Observation),
            CanonicalJson.Sha256(frame.Observation));
        Assert.Equal(oracle.ObservationHash, frame.ObservationHash);
        Assert.Equal(
            oracle.LegalActions.Select(a => a.ActionId),
            frame.LegalActions.Select(a => a.ActionId));
        Assert.Contains(frame.LegalActions,
            action => action.Kind == "end_turn");
        Assert.Contains(frame.LegalActions,
            action => action.Kind == "play_card");
        Assert.Equal(runtime.LegalPlayerActionIds(),
            frame.LegalActions.Select(a => a.ActionId).ToArray());
        Assert.Equal(
            runtime.Boundary,
            runtime.Boundary);
        runtime.AuditReplay();
    }

    [Fact]
    public void NativePlayerEndTurnMatchesCheckedPathAndDecisionJournal()
    {
        long checkedAllocation = 0, directAllocation = 0;
        long checkedTicks = 0, directTicks = 0;
        for (var index = 0; index < 8; index++)
        {
            var seed = "h8-native-endturn-a10-mawler-" + index;
            var checkedRuntime = ReachFirstFullPlayerBoundary(
                seed, useNativePlayerEndTurn: false);
            var directRuntime = ReachFirstFullPlayerBoundary(
                seed, useNativePlayerEndTurn: true);
            Assert.Equal(checkedRuntime.DecisionHistory,
                directRuntime.DecisionHistory);
            Assert.Equal(checkedRuntime.LegacySnapshotForAudit().Fingerprint,
                directRuntime.LegacySnapshotForAudit().Fingerprint);
            var action = checkedRuntime.EndTurnActionId();
            Assert.Equal(action, directRuntime.EndTurnActionId());
            var oldToken = directRuntime.Boundary;
            var oldCount = directRuntime.DecisionHistory.Count;
            Assert.Throws<InvalidOperationException>(() =>
                directRuntime.ChoosePlayerAction(
                    new H8BoundaryTokenV1(Guid.NewGuid(), oldToken.Version),
                    action));
            Assert.Equal(oldToken, directRuntime.Boundary);
            Assert.Equal(oldCount, directRuntime.DecisionHistory.Count);

            void Measure(H8CombatRuntimeV1 runtime,
                ref long allocated, ref long ticks)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                var started = Stopwatch.GetTimestamp();
                runtime.ChoosePlayerAction(runtime.Boundary, action);
                ticks += Stopwatch.GetTimestamp() - started;
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            }

            if (index % 2 == 0)
            {
                Measure(checkedRuntime, ref checkedAllocation, ref checkedTicks);
                Measure(directRuntime, ref directAllocation, ref directTicks);
            }
            else
            {
                Measure(directRuntime, ref directAllocation, ref directTicks);
                Measure(checkedRuntime, ref checkedAllocation, ref checkedTicks);
            }

            Assert.Equal(checkedRuntime.DecisionHistory,
                directRuntime.DecisionHistory);
            Assert.Equal(checkedRuntime.LegacySnapshotForAudit().Fingerprint,
                directRuntime.LegacySnapshotForAudit().Fingerprint);
            checkedRuntime.AuditReplay();
            directRuntime.AuditReplay();
        }

        _output.WriteLine(
            "H8 checked-vs-native-endturn A10 matched=8 allocated MB/step " +
            (checkedAllocation / 8e6).ToString("F3") + " -> " +
            (directAllocation / 8e6).ToString("F3") +
            " elapsed ms/step " +
            (checkedTicks * 1000.0 / Stopwatch.Frequency / 8).ToString("F3") +
            " -> " +
            (directTicks * 1000.0 / Stopwatch.Frequency / 8).ToString("F3"));
    }

    [Fact]
    public void NativeEndTurnMatchesCheckedPathAcrossRealIntentAndDrawSequence()
    {
        // Source-valid A10 trajectory with identical player and Nature
        // decisions, comparing a legacy checked end_turn against the new
        // H8 native executor across subsequent turns and random draws.
        const string seed = "h8-native-endturn-sequence-mawler";
        var checkedRuntime = ReachFirstFullPlayerBoundary(
            seed, useNativePlayerEndTurn: false);
        var directRuntime = ReachFirstFullPlayerBoundary(
            seed, useNativePlayerEndTurn: true);
        var decisions = 0;
        var draws = 0;
        var turns = 0;
        for (var i = 0; i < 48; i++)
        {
            Assert.Equal(
                checkedRuntime.LegacySnapshotForAudit().Fingerprint,
                directRuntime.LegacySnapshotForAudit().Fingerprint);
            Assert.Equal(
                checkedRuntime.DecisionHistory, directRuntime.DecisionHistory);
            if (checkedRuntime.Terminal || directRuntime.Terminal)
            {
                Assert.Equal(checkedRuntime.Terminal, directRuntime.Terminal);
                break;
            }

            if (checkedRuntime.HasPendingCardDraw)
            {
                var eligible = checkedRuntime.PendingDrawCardInstanceIds();
                Assert.Equal(eligible, directRuntime.PendingDrawCardInstanceIds());
                var chosen = eligible[(i + 1) % eligible.Length];
                checkedRuntime.ChooseDrawCard(checkedRuntime.Boundary, chosen);
                directRuntime.ChooseDrawCard(directRuntime.Boundary, chosen);
                draws++;
            }
            else if (checkedRuntime.HasPendingIntent)
            {
                var eligible = checkedRuntime.PendingIntentIds();
                Assert.Equal(eligible, directRuntime.PendingIntentIds());
                checkedRuntime.ChooseIntent(checkedRuntime.Boundary, eligible[0]);
                directRuntime.ChooseIntent(directRuntime.Boundary, eligible[0]);
            }
            else if (checkedRuntime.LegacySnapshotForAudit().AtPlayerBoundary)
            {
                var action = checkedRuntime.EndTurnActionId();
                Assert.Equal(action, directRuntime.EndTurnActionId());
                checkedRuntime.ChoosePlayerAction(
                    checkedRuntime.Boundary, action);
                directRuntime.ChoosePlayerAction(
                    directRuntime.Boundary, action);
                turns++;
            }
            else
                break; // Another H8 stochastic kind awaits future migration.
            decisions++;
        }

        Assert.True(turns >= 1);
        Assert.True(draws >= 1);
        Assert.Equal(checkedRuntime.DecisionHistory,
            directRuntime.DecisionHistory);
        Assert.Equal(checkedRuntime.LegacySnapshotForAudit().Fingerprint,
            directRuntime.LegacySnapshotForAudit().Fingerprint);
        checkedRuntime.AuditReplay();
        directRuntime.AuditReplay();
        _output.WriteLine(
            "H8 checked-vs-native-endturn sequential A10 decisions=" +
            decisions + " end_turns=" + turns + " physical_draws=" + draws +
            " exact state and replay passed.");
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
        Assert.Contains("sub.h8_native_draw_engine_mechanics", stages);
        Assert.DoesNotContain("sub.semantic_draw_combat_engine_mechanics", stages);
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
    public void MatchedEightCombatM1M2DrawsPreserveExactMechanicsAndMeasureAllocation()
    {
        // A/B checks the same H8 session implementation with only the M2
        // automatic-draw cursor/recursive-audit switch changed. Player/intent
        // action history, seeds and physical draw cards are identical.
        long m1Alloc = 0, m2Alloc = 0;
        long m1Ticks = 0, m2Ticks = 0;
        var draws = 0;
        for (var i = 0; i < 8; i++)
        {
            var seed = "h8-m2-matched-a10-mawler-" + i;
            var m1 = ReachNaturalDraw(seed, useM2AutomaticDraw: false);
            var m2 = ReachNaturalDraw(
                seed, useM2AutomaticDraw: true,
                useNativeDrawTransition: false);
            Assert.Equal(m1.DecisionHistory, m2.DecisionHistory);
            var candidates = m1.PendingDrawCardInstanceIds();
            Assert.Equal(candidates, m2.PendingDrawCardInstanceIds());
            var selectedId = candidates[i % candidates.Length];

            void Measure(H8CombatRuntimeV1 runtime,
                ref long allocation, ref long ticks)
            {
                var bytesStart = GC.GetAllocatedBytesForCurrentThread();
                var tickStart = Stopwatch.GetTimestamp();
                runtime.ChooseDrawCard(
                    runtime.Boundary, selectedId, "scripted-h8-ab");
                ticks += Stopwatch.GetTimestamp() - tickStart;
                allocation += GC.GetAllocatedBytesForCurrentThread() - bytesStart;
            }

            // Alternating order avoids comparing a systematically colder A
            // run to a warmer B run on every scenario.
            if (i % 2 == 0)
            {
                Measure(m1, ref m1Alloc, ref m1Ticks);
                Measure(m2, ref m2Alloc, ref m2Ticks);
            }
            else
            {
                Measure(m2, ref m2Alloc, ref m2Ticks);
                Measure(m1, ref m1Alloc, ref m1Ticks);
            }
            Assert.Equal(
                m1.LegacySnapshotForAudit().Fingerprint,
                m2.LegacySnapshotForAudit().Fingerprint);
            Assert.Equal(m1.DecisionHistory, m2.DecisionHistory);
            m1.AuditReplay();
            m2.AuditReplay();
            draws++;
        }
        _output.WriteLine(
            "H8 M1-versus-M2 A10 matched draws=" + draws +
            " allocated MB/draw " +
            (m1Alloc / 1e6 / draws).ToString("F3") + " -> " +
            (m2Alloc / 1e6 / draws).ToString("F3") +
            " elapsed ms/draw " +
            (m1Ticks * 1000.0 / Stopwatch.Frequency / draws).ToString("F3") +
            " -> " +
            (m2Ticks * 1000.0 / Stopwatch.Frequency / draws).ToString("F3"));
        Assert.Equal(8, draws);
    }

    [Fact]
    public void MatchedEightCombatDirectNativeDrawsAgreeWithM2Bridge()
    {
        // The prior M2 bridge and the new direct engine route consume the
        // same provider-selected physical identities from actual A10 draws.
        // Inspect mechanics, history and full replay independently.
        long m2Bytes = 0, nativeBytes = 0, m2Ticks = 0, nativeTicks = 0;
        const int combatCount = 8;
        for (var i = 0; i < combatCount; i++)
        {
            var seed = "h8-native-draw-matched-mawler-" + i;
            var prior = ReachNaturalDraw(
                seed, useM2AutomaticDraw: true,
                useNativeDrawTransition: false);
            var direct = ReachNaturalDraw(
                seed, useM2AutomaticDraw: true,
                useNativeDrawTransition: true);
            Assert.Equal(prior.DecisionHistory, direct.DecisionHistory);
            var candidates = prior.PendingDrawCardInstanceIds();
            Assert.Equal(candidates, direct.PendingDrawCardInstanceIds());
            var selected = candidates[(i * 3) % candidates.Length];

            void Measure(H8CombatRuntimeV1 runtime,
                ref long bytes, ref long ticks)
            {
                var beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                var beforeTicks = Stopwatch.GetTimestamp();
                runtime.ChooseDrawCard(
                    runtime.Boundary, selected, "scripted-h8-native-ab");
                ticks += Stopwatch.GetTimestamp() - beforeTicks;
                bytes += GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
            }
            if (i % 2 == 0)
            {
                Measure(prior, ref m2Bytes, ref m2Ticks);
                Measure(direct, ref nativeBytes, ref nativeTicks);
            }
            else
            {
                Measure(direct, ref nativeBytes, ref nativeTicks);
                Measure(prior, ref m2Bytes, ref m2Ticks);
            }
            Assert.Equal(prior.DecisionHistory, direct.DecisionHistory);
            Assert.Equal(
                prior.LegacySnapshotForAudit().Fingerprint,
                direct.LegacySnapshotForAudit().Fingerprint);
            prior.AuditReplay();
            direct.AuditReplay();
        }
        _output.WriteLine(
            "H8 M2-bridge-vs-direct A10 draws=" + combatCount +
            " allocated MB/draw " +
            (m2Bytes / 1e6 / combatCount).ToString("F3") + " -> " +
            (nativeBytes / 1e6 / combatCount).ToString("F3") +
            " elapsed ms/draw " +
            (m2Ticks * 1000.0 / Stopwatch.Frequency / combatCount).ToString("F3") +
            " -> " +
            (nativeTicks * 1000.0 / Stopwatch.Frequency / combatCount).ToString("F3"));
    }

    [Fact]
    public void DirectDrawAndM2BridgeRemainEquivalentAcrossSubsequentBoundaries()
    {
        // The comparison must remain valid after the first resumed draw:
        // end-turn completion, another turn, or a further controlled draw
        // must not depend on a stale projected HandDraw/EndTurn frame.
        var prior = ReachNaturalDraw(
            "h8-native-multiboundary-mawler",
            useM2AutomaticDraw: true, useNativeDrawTransition: false);
        var direct = ReachNaturalDraw(
            "h8-native-multiboundary-mawler",
            useM2AutomaticDraw: true, useNativeDrawTransition: true);
        var decisions = 0;
        var draws = 0;
        for (var i = 0; i < 36; i++)
        {
            Assert.Equal(
                prior.LegacySnapshotForAudit().Fingerprint,
                direct.LegacySnapshotForAudit().Fingerprint);
            Assert.Equal(prior.DecisionHistory, direct.DecisionHistory);
            if (prior.Terminal || direct.Terminal)
            {
                Assert.Equal(prior.Terminal, direct.Terminal);
                break;
            }

            if (prior.HasPendingCardDraw)
            {
                var support = prior.PendingDrawCardInstanceIds();
                Assert.Equal(support, direct.PendingDrawCardInstanceIds());
                var selectedId = support[(i + 1) % support.Length];
                prior.ChooseDrawCard(prior.Boundary, selectedId);
                direct.ChooseDrawCard(direct.Boundary, selectedId);
                draws++;
            }
            else if (prior.HasPendingIntent)
            {
                var support = prior.PendingIntentIds();
                Assert.Equal(support, direct.PendingIntentIds());
                prior.ChooseIntent(prior.Boundary, support[0]);
                direct.ChooseIntent(direct.Boundary, support[0]);
            }
            else if (prior.LegacySnapshotForAudit().AtPlayerBoundary)
            {
                var endTurn = prior.EndTurnActionId();
                Assert.Equal(endTurn, direct.EndTurnActionId());
                prior.ChoosePlayerAction(prior.Boundary, endTurn);
                direct.ChoosePlayerAction(direct.Boundary, endTurn);
            }
            else
            {
                // The H8 typed chooser for this other chance/selection
                // boundary has not been migrated. Prior H8 and direct H8
                // must still agree at the last supported boundary.
                break;
            }
            decisions++;
        }

        Assert.True(draws >= 1, "Expected at least one real physical draw.");
        Assert.Equal(prior.DecisionHistory, direct.DecisionHistory);
        Assert.Equal(
            prior.LegacySnapshotForAudit().Fingerprint,
            direct.LegacySnapshotForAudit().Fingerprint);
        prior.AuditReplay();
        direct.AuditReplay();
        _output.WriteLine(
            "H8 direct-vs-M2 sequential A10 decisions=" + decisions +
            " physical_draws=" + draws + " exact replay passed.");
    }

    [Fact]
    public void TrustedIntentUsesRealA10SupportAndMatchesCheckedLegacyResolver()
    {
        var state = H8CombatRuntimeV1.BeginSeeded(
            Mawler("h8-a10-trusted-intent-real-combat"));
        for (var i = 0; !state.HasPendingIntent; i++)
        {
            Assert.True(i < 20, "Expected a real A10 next-turn intent request.");
            Assert.True(state.AtPlayerBoundary);
            state.ChoosePlayerAction(state.Boundary, state.EndTurnActionId());
        }
        var snapshot = state.LegacySnapshotForAudit();
        var support = state.PendingIntentIds();
        Assert.NotEmpty(support);
        var selected = support[^1];
        var expected = SemanticPilotCombatEngineV111.ResolveIntent(
            snapshot, snapshot.Fingerprint, selected);
        var before = state.Boundary;
        Assert.Throws<InvalidOperationException>(() =>
            state.ChooseIntent(new H8BoundaryTokenV1(Guid.NewGuid(),
                before.Version), selected));
        Assert.Throws<InvalidOperationException>(() =>
            state.ChooseIntent(before, "intent-id-not-in-support"));
        Assert.Equal(before, state.Boundary);
        state.ChooseIntent(before, selected, "scripted-test");
        Assert.Equal(expected.Fingerprint,
            state.LegacySnapshotForAudit().Fingerprint);
        Assert.Equal("intent", state.DecisionHistory[^1].Kind);
        Assert.Equal(selected, state.DecisionHistory[^1].IntentId);
        state.AuditReplay();
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
