using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeEngineFidelityBatchV111Tests
{
    private static string Fixture(int expectedEnemyHp = 35)
    {
        object State(bool after)
        {
            var strike = new {
                instance_id = 0L, model_id = "STRIKE_SILENT",
                cost = 1, upgrades = 0, target_type = "AnyEnemy"
            };
            return new {
                player = new { hp = 56, max_hp = 70, gold = 99,
                    block = 0, powers = Array.Empty<object>() },
                combat = new {
                    energy = after ? 2 : 3, turn = 1,
                    creatures = new [] {
                        new { id = "creature:0", model_id = "SILENT",
                            hp = 56, max_hp = 70, block = 0, alive = true,
                            powers = Array.Empty<object>() },
                        new { id = "creature:1", model_id = "SLUDGE_SPINNER",
                            hp = after ? expectedEnemyHp : 41,
                            max_hp = 41, block = 0, alive = true,
                            powers = Array.Empty<object>() }
                    },
                    hand = after ? Array.Empty<object>() : new object[] { strike },
                    draw_pile = Array.Empty<object>(),
                    discard_pile = after ? new object[] { strike } : Array.Empty<object>(),
                    exhaust_pile = Array.Empty<object>()
                }
            };
        }
        return JsonSerializer.Serialize(new {
            fixture_schema = "native-expectation-v1",
            status = "RECORDED_NATIVE_EXPECTATION",
            decision_id = 1,
            native_trace_header = new { game_build = "synthetic-not-native" },
            selected_card = new { model_id = "STRIKE_SILENT" },
            selected_action = new {
                kind = "play_card", payload = new {
                    card_instance_id = 0L, target_id = "creature:1"
                }
            },
            pre_state = State(false), post_state = State(true)
        });
    }

    private static string TemporaryDirectory()
    {
        string folder = Path.Combine(Path.GetTempPath(),
            "sts2-batch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    [Fact]
    public void BatchRunsRealEngineAndSeparatesMatchesFromMismatchesAndUnsupported()
    {
        string folder = TemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(folder, "a-valid.json"), Fixture());
            File.WriteAllText(Path.Combine(folder, "b-mismatch.json"), Fixture(36));
            File.WriteAllText(Path.Combine(folder, "c-corrupt.json"), "{invalid");
            var result = NativeEngineFidelityBatchV111.CompareDirectory(folder);
            Assert.Equal("native-engine-fidelity-batch-v1", result.Schema);
            Assert.Equal(3, result.TotalFixtures);
            Assert.Equal(2, result.ActuallyStepped);
            Assert.Equal(1, result.Consistent);
            Assert.Equal(1, result.Mismatched);
            Assert.Equal(1, result.UnsupportedOrInvalid);
            Assert.Equal("CHECKPOINT_MISMATCHES_DETECTED", result.Status);
            Assert.Equal("NOT_TESTED", result.NativeGameParity);
            Assert.Equal("NOT_TESTED", result.ExactRngParity);
            Assert.Equal("NOT_TESTED", result.NativeSourceHookOrderParity);
            Assert.Equal("$.combat.creatures[creature:1].hp",
                result.Entries[1].FirstMismatchPath);
            Assert.Equal("INVALID_FIXTURE", result.Entries[2].Status);
            Assert.All(result.Entries, item => Assert.NotEmpty(item.File));
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void EmptyDirectoryCannotPassFidelity()
    {
        string folder = TemporaryDirectory();
        try
        {
            var result = NativeEngineFidelityBatchV111.CompareDirectory(folder);
            Assert.Equal("NO_FIXTURES", result.Status);
            Assert.Equal(0, result.ActuallyStepped);
            Assert.Equal("NOT_TESTED", result.NativeGameParity);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }
}
