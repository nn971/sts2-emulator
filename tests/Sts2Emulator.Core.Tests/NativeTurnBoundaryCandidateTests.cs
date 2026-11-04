using System.Text.Json;
using Sts2ReferenceMod.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeTurnBoundaryCandidateTests
{
    private static async Task<JsonElement[]> Capture(
        Action<NativeActionEnvelopeRecorder> record)
    {
        string file = Path.Combine(Path.GetTempPath(),
            "sts2-turn-boundary-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            var recorder = new NativeActionEnvelopeRecorder(
                file, "v111;sha256=synthetic", "synthetic-run");
            record(recorder);
            recorder.Finish("Combat ended", null);
            await recorder.DisposeAsync();
            return File.ReadAllLines(file)
                .Select(line => JsonDocument.Parse(line).RootElement.Clone())
                .ToArray();
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
            if (File.Exists(file + ".incomplete"))
                File.Delete(file + ".incomplete");
        }
    }

    [Fact]
    public async Task BothTurnStartsRemainUnverifiedWithoutPhaseSettlementProof()
    {
        var state = JsonSerializer.SerializeToElement(new
        {
            player = new { hp = 56 },
            combat = new { turn = 3, energy = 3 }
        });
        var rows = await Capture(recorder =>
        {
            recorder.TurnBoundaryCandidate(
                "PLAYER_TURN_START", 3, state, "Player", "Play");
            recorder.TurnBoundaryCandidate(
                "ENEMY_TURN_START", 3, state, "Player", "End");
        });
        var checkpoints = rows.Where(row =>
            row.GetProperty("type").GetString() == "native_turn_checkpoint")
            .ToArray();
        Assert.Equal(2, checkpoints.Length);
        Assert.Equal(new[] { "PLAYER_TURN_START", "ENEMY_TURN_START" },
            checkpoints.Select(row =>
                row.GetProperty("data").GetProperty("kind").GetString()));
        Assert.All(checkpoints, row =>
        {
            var data = row.GetProperty("data");
            Assert.Equal(3, data.GetProperty("turn").GetInt32());
            Assert.Equal("SETTLEMENT_UNVERIFIED",
                data.GetProperty("settlement_status").GetString());
            Assert.False(data.GetProperty("complete_native_queue_verified")
                .GetBoolean());
            Assert.False(data.GetProperty("native_phase_handoff_order_verified")
                .GetBoolean());
            Assert.Equal("NOT_TESTED",
                data.GetProperty("emulator_parity").GetString());
            Assert.Equal(56, row.GetProperty("state")
                .GetProperty("player").GetProperty("hp").GetInt32());
        });
        Assert.Equal("OBSERVED", rows[^1].GetProperty("data")
            .GetProperty("status").GetString());
    }

    [Fact]
    public async Task NativeTrackedEffectEvidenceIsBoundedToOnePlayerFrame()
    {
        var state = JsonSerializer.SerializeToElement(new {
            player = new { hp = 56 },
            combat = new { turn = 1, energy = 3 }
        });
        var stateTurnTwo = JsonSerializer.SerializeToElement(new {
            player = new { hp = 56 },
            combat = new { turn = 2, energy = 3 }
        });
        var rows = await Capture(recorder =>
        {
            recorder.TurnBoundaryCandidate(
                "PLAYER_TURN_START", 1, state, "Player", "Play",
                effectEvidence: new NativeAutomaticEffectEvidence(
                    NoActiveCardOrPotionEffectBody: true,
                    NoPlayerTurnEndTransition: true,
                    EnemyTurnNotStarted: true));
            recorder.TurnBoundaryCandidate(
                "PLAYER_TURN_START", 2, stateTurnTwo, "Player", "Play",
                effectEvidence: new NativeAutomaticEffectEvidence(
                    NoActiveCardOrPotionEffectBody: false,
                    NoPlayerTurnEndTransition: true,
                    EnemyTurnNotStarted: true));
        });
        var checkpoints = rows.Where(row =>
            row.GetProperty("type").GetString() == "native_turn_checkpoint")
            .ToArray();
        Assert.Equal(2, checkpoints.Length);
        Assert.Equal("SOURCE_TRACKED_EFFECT_BODY_INACTIVE_AT_FRAME",
            checkpoints[0].GetProperty("data")
                .GetProperty("automatic_effect_evidence_status").GetString());
        Assert.Equal("SOURCE_TRACKED_EFFECT_ACTIVITY_UNVERIFIED",
            checkpoints[1].GetProperty("data")
                .GetProperty("automatic_effect_evidence_status").GetString());
        Assert.False(checkpoints[1].GetProperty("data")
            .GetProperty("automatic_effect_evidence")
            .GetProperty("no_active_card_or_potion_effect_body").GetBoolean());
        Assert.All(checkpoints, row =>
        {
            var data = row.GetProperty("data");
            Assert.Equal("SETTLEMENT_UNVERIFIED",
                data.GetProperty("settlement_status").GetString());
            Assert.False(data.GetProperty("native_phase_handoff_order_verified")
                .GetBoolean());
        });
        Assert.Equal("OBSERVED", rows[^1].GetProperty("data")
            .GetProperty("status").GetString());
    }

    [Fact]
    public async Task DuplicateTurnCandidateIsExplicitObservationFailure()
    {
        var state = JsonSerializer.SerializeToElement(new
        {
            combat = new { turn = 1 }
        });
        var rows = await Capture(recorder =>
        {
            recorder.TurnBoundaryCandidate(
                "PLAYER_TURN_START", 1, state, "Player", "Play");
            recorder.TurnBoundaryCandidate(
                "PLAYER_TURN_START", 1, state, "Player", "Play");
        });
        Assert.Single(rows, row =>
            row.GetProperty("type").GetString() == "native_turn_checkpoint");
        Assert.Contains(rows, row =>
            row.GetProperty("type").GetString() == "observation_failure");
        Assert.Equal("INCOMPLETE_CAPTURE", rows[^1].GetProperty("data")
            .GetProperty("status").GetString());
    }
}
