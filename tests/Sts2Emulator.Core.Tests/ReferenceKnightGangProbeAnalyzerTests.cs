using System.Text.Json;
using Sts2Emulator.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class ReferenceKnightGangProbeAnalyzerTests
{
    [Fact]
    public void ExtractsKnightGangStateWithoutInventingMissingEvidence()
    {
        var path = Path.GetTempFileName();
        try
        {
            var records = new object[]
            {
                new
                {
                    type = "session",
                    schema = "sts2-reference-probe-v2",
                    build_fingerprint = "test-fingerprint"
                },
                Boundary(
                    1,
                    "combat_manager.CombatSetUp",
                    round: 1,
                    playerHp: 70,
                    flailMove: "RAM",
                    spectralMove: "HEX",
                    magiMove: "POWER_SHIELD",
                    includeIntent: true,
                    includeRng: true),
                Boundary(
                    2,
                    "combat_manager.TurnStarted",
                    round: 2,
                    playerHp: 46,
                    flailMove: "FLAIL",
                    spectralMove: "SOUL_SLASH",
                    magiMove: "DAMPEN",
                    includeIntent: true,
                    includeRng: true)
            };

            File.WriteAllLines(
                path,
                records.Select(record =>
                    JsonSerializer.Serialize(record)));

            var audit =
                ReferenceKnightGangProbeAnalyzer.Analyze(path);

            Assert.Equal(
                "sts2-reference-probe-v2",
                audit.Schema);
            Assert.Equal(
                "test-fingerprint",
                audit.BuildFingerprint);
            Assert.Equal(1, audit.MatchingCombatCount);
            Assert.Equal(2, audit.MatchingCheckpointCount);
            Assert.Empty(audit.Diagnostics);

            var combat = Assert.Single(audit.Combats);
            Assert.Equal(9, combat.Ascension);
            Assert.Equal(2, combat.CheckpointCount);
            Assert.Equal(
                2,
                combat.CheckpointsWithMoveEvidence);
            Assert.Equal(
                2,
                combat.CheckpointsWithIntentEvidence);
            Assert.Equal(
                2,
                combat.CheckpointsWithMonsterAiRng);
            Assert.Empty(combat.StaticMismatches);

            Assert.Equal(
                ["flail", "magi", "spectral"],
                combat.Checkpoints[0].Enemies
                    .Select(enemy => enemy.Role)
                    .Where(role => role is not null)
                    .Select(role => role!)
                    .Order(StringComparer.Ordinal)
                    .ToArray());

            var first = combat.Checkpoints[0];
            Assert.Equal(70, first.PlayerHp);
            Assert.Equal(1, first.Cards.HandCount);
            Assert.Equal(1, first.Cards.UpgradedCardCount);
            Assert.Equal(1, first.Cards.HexedCardCount);
            Assert.Equal(1, first.Cards.AfflictedCardCount);
            Assert.NotNull(
                first.MonsterAiRngFingerprint);
            Assert.NotEmpty(
                first.MonsterAiRngEvidence);

            var flail = Assert.Single(
                first.Enemies,
                enemy => enemy.Role == "flail");
            Assert.Equal(108, flail.MaxHp);
            Assert.Contains(
                "RAM",
                flail.CurrentMove!,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReportsMissingKnightGangRatherThanFabricatingACombat()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(new
                {
                    type = "session",
                    schema = "sts2-reference-probe-v2"
                }));

            var audit =
                ReferenceKnightGangProbeAnalyzer.Analyze(path);

            Assert.Equal(0, audit.MatchingCombatCount);
            Assert.Contains(
                "knight_gang_not_observed",
                audit.Diagnostics);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static object Boundary(
        long sequence,
        string boundary,
        int round,
        int playerHp,
        string flailMove,
        string spectralMove,
        string magiMove,
        bool includeIntent,
        bool includeRng)
    {
        object Enemy(
            long combatId,
            string id,
            int hp,
            string move) =>
            new
            {
                combat_id = combatId,
                current_hp = hp,
                max_hp = hp,
                block = 0,
                model = new
                {
                    id = new { entry = id }
                },
                current_move = new
                {
                    id = new { entry = move }
                },
                intent = includeIntent
                    ? "Attack"
                    : null,
                move_index = round - 1,
                powers = new
                {
                    items = Array.Empty<object>()
                }
            };

        return new
        {
            type = "boundary",
            sequence,
            boundary,
            state_hash = $"hash-{sequence}",
            state = new
            {
                combat = new
                {
                    round_number = round,
                    current_side = "Player",
                    encounter = new
                    {
                        id = new
                        {
                            entry = "KNIGHT_GANG"
                        }
                    }
                },
                run = new
                {
                    ascension_level = 9
                },
                players = new object[]
                {
                    new
                    {
                        creature = new
                        {
                            current_hp = playerHp,
                            max_hp = 70,
                            block = 0
                        },
                        hand = new
                        {
                            items = new object[]
                            {
                                new
                                {
                                    id = new
                                    {
                                        entry = "DEFEND_SILENT"
                                    },
                                    upgrade_level = 1,
                                    is_upgraded = true,
                                    affliction = new
                                    {
                                        type = "HexedAffliction"
                                    }
                                }
                            }
                        },
                        draw_pile = new
                        {
                            items = Array.Empty<object>()
                        },
                        discard_pile = new
                        {
                            items = Array.Empty<object>()
                        },
                        exhaust_pile = new
                        {
                            items = Array.Empty<object>()
                        },
                        play_pile = new
                        {
                            items = Array.Empty<object>()
                        }
                    }
                },
                enemies = new object[]
                {
                    Enemy(
                        1,
                        "FLAIL_KNIGHT",
                        108,
                        flailMove),
                    Enemy(
                        2,
                        "SPECTRAL_KNIGHT",
                        97,
                        spectralMove),
                    Enemy(
                        3,
                        "MAGI_KNIGHT",
                        89,
                        magiMove)
                },
                run_rng = includeRng
                    ? new
                    {
                        monster_ai = new
                        {
                            counter = round,
                            state0 = 1,
                            state1 = 2,
                            state2 = 3,
                            state3 = 4
                        }
                    }
                    : null
            }
        };
    }
}
