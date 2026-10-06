using Sts2Emulator.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class ReferenceProbeActionExtractorTests
{
    [Fact]
    public void ExtractsPaidAndZeroCostCardEvidenceWithoutInventingMissingBoundaries()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"sts2-reference-actions-{Guid.NewGuid():N}.jsonl");

        try
        {
            File.WriteAllLines(
                path,
                [
                    Boundary(
                        1,
                        "combat_manager.TurnStarted",
                        "h1",
                        State(
                            energy: 3,
                            hand: [Card("STRIKE_SILENT"), Card("NEUTRALIZE")],
                            play: [])),
                    HistoryBoundary(
                        2,
                        "h1",
                        """{"type":"X.EnergySpentEntry","amount":1}""",
                        State(
                            energy: 3,
                            hand: [Card("STRIKE_SILENT"), Card("NEUTRALIZE")],
                            play: [])),
                    HistoryBoundary(
                        3,
                        "h2",
                        CardStart("Strike", "MegaCrit.Sts2.Core.Models.Cards.StrikeSilent", 1, 1),
                        State(
                            energy: 2,
                            hand: [Card("NEUTRALIZE")],
                            play: [Card("STRIKE_SILENT")])),
                    HistoryBoundary(
                        4,
                        "h3",
                        CardFinish("Strike", 1, 1),
                        State(
                            energy: 2,
                            hand: [Card("NEUTRALIZE")],
                            play: [Card("STRIKE_SILENT")])),
                    HistoryBoundary(
                        5,
                        "h4",
                        CardStart("Neutralize", "MegaCrit.Sts2.Core.Models.Cards.Neutralize", 1, 0),
                        State(
                            energy: 2,
                            hand: [],
                            play: [Card("NEUTRALIZE")])),
                    HistoryBoundary(
                        6,
                        "h5",
                        CardFinish("Neutralize", 1, 0),
                        State(
                            energy: 2,
                            hand: [],
                            play: [Card("NEUTRALIZE")])),
                    Boundary(
                        7,
                        "combat_manager.PlayerEndedTurn",
                        "h6",
                        State(
                            energy: 2,
                            hand: [],
                            play: [])),
                    Boundary(
                        8,
                        "combat_manager.TurnStarted",
                        "h7",
                        State(
                            energy: 3,
                            hand: [Card("DEFEND_SILENT")],
                            play: []))
                ]);

            var report = ReferenceProbeActionExtractor.Analyze(path);

            Assert.Equal(2, report.CardPlayCount);
            Assert.Equal(0, report.PotionUseCount);
            Assert.Equal(1, report.EndTurnCount);

            var first = report.Actions[0];
            Assert.Equal("play_card", first.Action.Kind);
            Assert.Equal(2, first.CandidateBefore?.Sequence);
            Assert.Equal(4, first.LifecycleFinish?.Sequence);
            Assert.Null(first.CandidateAfter);
            Assert.Contains(
                "next_card_started_before_a_settled_post_card_snapshot_was_observed",
                first.Diagnostics);

            Assert.Equal(
                "STRIKE_SILENT",
                first.Action.Payload.GetProperty("card_id").GetString());
            Assert.Equal(
                1,
                first.Action.Payload
                    .GetProperty("target_combat_id")
                    .GetInt64());

            var second = report.Actions[1];
            Assert.Null(second.CandidateBefore);
            Assert.Equal(7, second.CandidateAfter?.Sequence);
            Assert.Contains(
                "pre_card_decision_snapshot_not_observed",
                second.Diagnostics);

            var endTurn = report.Actions[2];
            Assert.Equal("end_turn", endTurn.Action.Kind);
            Assert.Equal(7, endTurn.CandidateBefore?.Sequence);
            Assert.Equal(8, endTurn.CandidateAfter?.Sequence);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TreatsPotionUsedAsPostEffectEvidence()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"sts2-reference-potion-{Guid.NewGuid():N}.jsonl");

        try
        {
            File.WriteAllLines(
                path,
                [
                    HistoryBoundary(
                        1,
                        "h1",
                        """{"type":"X.PotionUsedEntry","potion":{"id":{"entry":"SWIFT_POTION"}},"target":{"combat_id":0}}""",
                        State(
                            energy: 3,
                            hand: [Card("STRIKE_SILENT")],
                            play: []))
                ]);

            var report = ReferenceProbeActionExtractor.Analyze(path);

            var action = Assert.Single(report.Actions);
            Assert.Equal("use_potion", action.Action.Kind);
            Assert.Null(action.CandidateBefore);
            Assert.Equal(1, action.CandidateAfter?.Sequence);
            Assert.Equal(
                "SWIFT_POTION",
                action.Action.Payload
                    .GetProperty("potion_id")
                    .GetString());
            Assert.Contains(
                "potion_effects_may_precede_potion_used_entry",
                action.Diagnostics);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string Boundary(
        long sequence,
        string boundary,
        string hash,
        string state) =>
        $"{{\"type\":\"boundary\",\"sequence\":{sequence},\"boundary\":\"{boundary}\",\"state_hash\":\"{hash}\",\"state\":{state}}}";

    private static string HistoryBoundary(
        long sequence,
        string hash,
        string historyEntry,
        string state) =>
        $"{{\"type\":\"boundary\",\"sequence\":{sequence},\"boundary\":\"combat_history.Changed\",\"state_hash\":\"{hash}\",\"history_entry\":{historyEntry},\"state\":{state}}}";

    private static string State(
        int energy,
        string[] hand,
        string[] play) =>
        $$"""
        {
          "manager":{
            "is_in_progress":true,
            "is_over_or_ending":false,
            "player_actions_disabled":false
          },
          "combat":{"current_side":"Player"},
          "players":[{
            "combat":{"energy":{{energy}},"phase":"Play"},
            "hand":{"items":[{{string.Join(",", hand)}}]},
            "play_pile":{"items":[{{string.Join(",", play)}}]}
          }]
        }
        """;

    private static string Card(string id)
    {
        var type = id switch
        {
            "STRIKE_SILENT" => "StrikeSilent",
            "DEFEND_SILENT" => "DefendSilent",
            "NEUTRALIZE" => "Neutralize",
            _ => id
        };

        return $$"""{"type":"MegaCrit.Sts2.Core.Models.Cards.{{type}}","id":{"entry":"{{id}}"},"is_upgraded":false}""";
    }

    private static string CardStart(
        string title,
        string runtimeType,
        int target,
        int energySpent) =>
        $$"""
        {
          "type":"X.CardPlayStartedEntry",
          "card_play":{
            "card":{
              "title":"{{title}}",
              "deck_version":{"type":"{{runtimeType}}"}
            },
            "target":{"combat_id":{{target}}},
            "resources":{"energy_spent":{{energySpent}}},
            "is_auto_play":false,
            "play_index":0,
            "play_count":1
          }
        }
        """;

    private static string CardFinish(
        string title,
        int target,
        int energySpent) =>
        $$"""
        {
          "type":"X.CardPlayFinishedEntry",
          "card_play":{
            "card":{"title":"{{title}}"},
            "target":{"combat_id":{{target}}},
            "resources":{"energy_spent":{{energySpent}}},
            "is_auto_play":false,
            "play_index":0,
            "play_count":1
          }
        }
        """;
}
