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
        Assert.Same(frame, runtime.ObservePlayerFrame());
        // The H8 version token replaces the expensive hidden-state hash:
        // public observation and legal action hashes remain identical.
        Assert.Equal(string.Empty, frame.CanonicalStateHash);
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
    public void H8VersionOwnedBoundaryTagMatchesCheckedStateAcrossRealA10Choices()
    {
        // The H8 status tag may bypass expensive nested draw projections,
        // but must reproduce the checked v111 precedence at every sampled
        // player, initial/turn intent, and real physical draw boundary.
        var runtime = H8CombatRuntimeV1.BeginSeeded(
            Mawler("h8-tagged-cursor-a10-real-combat"));
        var seenPlayer = 0;
        var seenDraw = 0;
        var seenIntent = 0;
        var lastVersion = runtime.Boundary.Version;
        for (var i = 0; i < 75; i++)
        {
            var state = runtime.LegacySnapshotForAudit();
            var checkedKind = state.CombatTerminal ? "terminal" :
                state.DrawPending is not null ? "draw_card" :
                state.IntentPending is not null ? "enemy_intent" :
                state.AtPlayerChoiceBoundary ? "player_choice" :
                state.AtPlayerBoundary ? "player" : "unsupported";
            var kind = runtime.BoundaryKind;
            Assert.Equal(checkedKind, kind);
            Assert.Equal(kind, runtime.BoundaryKind);
            Assert.Equal(kind == "draw_card", runtime.HasPendingCardDraw);
            Assert.Equal(kind == "enemy_intent", runtime.HasPendingIntent);
            Assert.Equal(kind is "player" or "player_choice",
                runtime.AtPlayerBoundary);
            if (kind == "terminal")
                break;
            var token = runtime.Boundary;
            switch (kind)
            {
                case "enemy_intent":
                    var ids = runtime.PendingIntentIds();
                    var weights = runtime.PendingIntentRawWeights();
                    Assert.Equal(ids.Length, weights.Length);
                    Assert.All(weights, weight => Assert.True(weight > 0));
                    Assert.Equal(ids, runtime.PendingIntentIds());
                    Assert.Equal(weights, runtime.PendingIntentRawWeights());
                    runtime.ChooseIntent(token, ids[0]);
                    seenIntent++;
                    break;
                case "draw_card":
                    var cards = runtime.PendingDrawCardInstanceIds();
                    Assert.NotEmpty(cards);
                    Assert.Equal(cards, runtime.PendingDrawCardInstanceIds());
                    runtime.ChooseDrawCard(token, cards[0]);
                    seenDraw++;
                    break;
                case "player":
                    runtime.ChoosePlayerAction(token, runtime.EndTurnActionId());
                    seenPlayer++;
                    break;
                default:
                    // Other nested choices have intentionally not been
                    // migrated to the H8 decision protocol.
                    i = 75;
                    continue;
            }
            Assert.Equal(lastVersion + 1, runtime.Boundary.Version);
            lastVersion++;
            Assert.Throws<InvalidOperationException>(() =>
                runtime.ChoosePlayerAction(token, "stale-option"));
        }
        Assert.True(seenIntent > 0);
        Assert.True(seenPlayer > 0);
        Assert.True(seenDraw > 0);
        runtime.AuditReplay();
    }

    [Fact]
    public void H8TaggedIntentAndPlayerBoundariesAvoidLegacyDrawCapture()
    {
        // All source-valid initial intents and ordinary player boundaries
        // are classified without materializing nested v111 draw events.
        var runtime = H8CombatRuntimeV1.BeginSeeded(
            Mawler("h8-boundary-no-spurious-draw-projection"));
        var calls = new Dictionary<string, int>(StringComparer.Ordinal);
        H8HotPathProfilerV1.SetSink((operation, _, _) =>
        {
            calls[operation] = calls.GetValueOrDefault(operation) + 1;
        });
        try
        {
            for (var i = 0; i < 12; i++)
            {
                if (runtime.BoundaryKind == "player")
                    break;
                Assert.Equal("enemy_intent", runtime.BoundaryKind);
                var ids = runtime.PendingIntentIds();
                Assert.Equal(ids, runtime.PendingIntentIds());
                Assert.NotEmpty(runtime.PendingIntentRawWeights());
                runtime.ChooseIntent(runtime.Boundary, ids[0]);
            }
            Assert.Equal("player", runtime.BoundaryKind);
            Assert.Equal("player", runtime.BoundaryKind);
            Assert.Equal(0, calls.GetValueOrDefault("h8.draw.cursor_capture"));
            // Repeated status-equivalent observations must reuse the same
            // computed boundary tag until Commit invalidates it.
            Assert.True(calls.GetValueOrDefault("h8.boundary.classify") <= 13);
            Assert.Equal(
                runtime.LegalPlayerActionIds(),
                runtime.LegalPlayerActionIds());
        }
        finally
        {
            H8HotPathProfilerV1.SetSink(null);
        }
        runtime.AuditReplay();
    }

    [Fact]
    public void TrustedH8CardPlayMatchesCheckedV111AndReplaysAcrossSourceSeeds()
    {
        // The H8 runtime must keep its exact action mechanics even though
        // player version tokens replace the expensive v111 pilot SHA.
        long checkedTicks = 0, trustedTicks = 0;
        long checkedAlloc = 0, trustedAlloc = 0;
        for (var index = 0; index < 8; index++)
        {
            var runtime = ReachFirstFullPlayerBoundary(
                "h8-trusted-card-a10-mawler-" + index,
                useNativePlayerEndTurn: true);
            var source = runtime.LegacySnapshotForAudit();
            var action = runtime.ObservePlayerFrame().LegalActions
                .First(item => item.Kind == "play_card");
            var oldToken = runtime.Boundary;
            var oldHistory = runtime.DecisionCount;
            SemanticPilotCombatV111 expected;
            void Checked()
            {
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var start = Stopwatch.GetTimestamp();
                expected = SemanticPilotCombatEngineV111.Step(
                    source, source.Fingerprint, action.ActionId);
                checkedTicks += Stopwatch.GetTimestamp() - start;
                checkedAlloc += GC.GetAllocatedBytesForCurrentThread() - allocated;
            }
            void Trusted()
            {
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var start = Stopwatch.GetTimestamp();
                runtime.ChoosePlayerAction(oldToken, action.ActionId);
                trustedTicks += Stopwatch.GetTimestamp() - start;
                trustedAlloc += GC.GetAllocatedBytesForCurrentThread() - allocated;
            }
            expected = null!;
            if (index % 2 == 0)
            {
                Checked();
                Trusted();
            }
            else
            {
                Trusted();
                Checked();
            }
            Assert.Equal(oldHistory + 1, runtime.DecisionCount);
            Assert.Equal(expected.Fingerprint,
                runtime.LegacySnapshotForAudit().Fingerprint);
            Assert.Throws<InvalidOperationException>(() =>
                runtime.ChoosePlayerAction(oldToken, action.ActionId));
            runtime.AuditReplay();
        }
        _output.WriteLine(
            "H8 trusted player card versus v111 checked: eight real A10 "
            + "plays, exact state and replay; elapsed ms/action "
            + (checkedTicks * 1000.0 / Stopwatch.Frequency / 8).ToString("F3")
            + " -> "
            + (trustedTicks * 1000.0 / Stopwatch.Frequency / 8).ToString("F3")
            + "; managed MB/action "
            + (checkedAlloc / 8e6).ToString("F3") + " -> "
            + (trustedAlloc / 8e6).ToString("F3"));
    }

    [Fact]
    public void OptInNestedProfilerAttributesH8OwnedCardWithoutEngineFork()
    {
        var runtime = ReachFirstFullPlayerBoundary(
            "h8-profile-single-fork-a10", useNativePlayerEndTurn: true);
        var action = runtime.ObservePlayerFrame().LegalActions
            .First(item => item.Kind == "play_card");
        var snapshot = runtime.LegacySnapshotForAudit();
        var expected = SemanticPilotCombatEngineV111.Step(
            snapshot, snapshot.Fingerprint, action.ActionId);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var allocations = new Dictionary<string, long>(StringComparer.Ordinal);
        H8HotPathProfilerV1.SetSink((name, elapsed, bytes) =>
        {
            Assert.True(elapsed >= 0);
            Assert.True(bytes >= 0);
            counts[name] = counts.GetValueOrDefault(name) + 1;
            allocations[name] = allocations.GetValueOrDefault(name) + bytes;
        });
        try
        {
            runtime.ChoosePlayerAction(runtime.Boundary, action.ActionId);
        }
        finally
        {
            H8HotPathProfilerV1.SetSink(null);
        }
        Assert.Equal(1, counts["h8.player.total"]);
        Assert.Equal(1, counts["h8.player.engine_card"]);
        // H8 now advances the version-owned immutable source directly.
        // A mechanics deep fork here is redundant and would regress the hot path.
        Assert.Equal(0, counts.GetValueOrDefault("h8.fork.run_total"));
        Assert.True(allocations["h8.player.total"] > 0);
        Assert.Equal(expected.Fingerprint,
            runtime.LegacySnapshotForAudit().Fingerprint);
        runtime.AuditReplay();
        Assert.False(H8HotPathProfilerV1.Active);
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
            var priorState = prior.LegacySnapshotForAudit();
            var directState = direct.LegacySnapshotForAudit();
            if (priorState.Fingerprint != directState.Fingerprint)
            {
                var aa = System.Text.Json.JsonSerializer.Serialize(priorState);
                var bb = System.Text.Json.JsonSerializer.Serialize(directState);
                var point = Enumerable.Range(0, Math.Min(aa.Length, bb.Length))
                    .FirstOrDefault(k => aa[k] != bb[k], -1);
                var left = Math.Max(0, point - 110);
                _output.WriteLine(
                    "H8 M4 divergence step=" + i + " decisions=" + draws +
                    " priorRun=" + CanonicalJson.Sha256(priorState.Run) +
                    " nativeRun=" + CanonicalJson.Sha256(directState.Run) +
                    " priorEndTurn=" + CanonicalJson.Sha256(priorState.PendingEndTurn) +
                    " nativeEndTurn=" + CanonicalJson.Sha256(directState.PendingEndTurn) +
                    " firstJsonDiff=" + point +
                    " prior=" + aa.Substring(left,
                        Math.Min(350, aa.Length - left)) +
                    " native=" + bb.Substring(left,
                        Math.Min(350, bb.Length - left)));
            }
            Assert.Equal(
                priorState.Fingerprint,
                directState.Fingerprint);
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
        // The real H7 actor asks for an event-scoped reference law before
        // selecting an intent. That query now caches the exact pilot SHA
        // and the subsequent H8 resolve must reuse it without state drift.
        Assert.NotNull(state.DescribePendingReferenceLaw());
        Assert.NotNull(state.DescribePendingReferenceLaw());
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

    [Fact]
    public void DeclaredReferenceNatureUsesSharedCursorAndExactTypedLaws()
    {
        var first = ReachNaturalDraw("h15-h8-declared-reference-cursor");
        var replay = ReachNaturalDraw("h15-h8-declared-reference-cursor");
        var boundary = first.Boundary;
        var staged = first.CopyForAtomicCommit();
        Assert.Equal(boundary, staged.Boundary);
        Assert.Equal(first.DecisionHistory, staged.DecisionHistory);
        var law = first.CurrentDeclaredReferenceLaw();
        law.Validate();
        Assert.NotEmpty(law.Outcomes);
        var initial = ReferenceNatureStateV111.Create(123456789UL);
        var expectedFirst = ReferenceNatureV111.SampleValidated(law, initial);
        Assert.Contains(law.Outcomes, outcome =>
            outcome.OutcomeId == expectedFirst.OutcomeId);

        var firstDecision = first.DecisionHistory.Count;
        // A failed staged transaction is discarded without mutating the
        // published live mechanics state or its boundary token.
        var abandoned = staged.AdvanceDeclaredReferenceNature(
            staged.Boundary, initial);
        Assert.Equal(boundary, first.Boundary);
        Assert.Equal(firstDecision, first.DecisionHistory.Count);
        Assert.True(abandoned.SampleCount > 0);

        var next = first.AdvanceDeclaredReferenceNature(boundary, initial);
        var replayNext = replay.AdvanceDeclaredReferenceNature(
            replay.Boundary, initial);

        Assert.True(next.SampleCount > 0);
        Assert.Equal(0, initial.SampleCount);
        Assert.Equal(next, replayNext);
        Assert.Equal(first.DecisionHistory, replay.DecisionHistory);
        Assert.All(first.DecisionHistory.Skip(firstDecision),
            decision => Assert.Equal(
                ReferenceNatureV111.ProviderId, decision.ProviderId));
        Assert.Throws<InvalidOperationException>(() =>
            first.AdvanceDeclaredReferenceNature(boundary, next));
        first.AuditReplay();
        replay.AuditReplay();
    }
}
