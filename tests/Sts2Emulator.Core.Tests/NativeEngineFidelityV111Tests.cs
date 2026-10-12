using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeEngineFidelityV111Tests
{
    private static JsonElement Fixture(string monster = "LEAF_SLIME_S",
        int nativeTargetAfterHp = 35, string card = "STRIKE_SILENT")
    {
        object State(bool after)
        {
            var strike = new { instance_id = 0L, model_id = card, cost = 1,
                upgrades = 0, target_type = "AnyEnemy" };
            return new
            {
                player = new { hp = 56, max_hp = 70, gold = 99, block = 0,
                    powers = Array.Empty<object>() },
                combat = new
                {
                    energy = after ? 2 : 3,
                    turn = 1,
                    creatures = new[]
                    {
                        new { id = "creature:0", model_id = "SILENT", hp = 56,
                            max_hp = 70, block = 0, alive = true,
                            powers = Array.Empty<object>() },
                        new { id = "creature:1", model_id = monster,
                            hp = after ? nativeTargetAfterHp : 41,
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

        return JsonSerializer.SerializeToElement(new
        {
            fixture_schema = "native-expectation-v1",
            status = "RECORDED_NATIVE_EXPECTATION",
            native_trace_header = new { game_build = "synthetic-not-native" },
            selected_action = new
            {
                kind = "play_card",
                payload = new { card_instance_id = 0L, target_id = "creature:1" }
            },
            pre_state = State(false),
            post_state = State(true),
        });
    }

    [Fact]
    public void NativeStrikeReachesActualEngineAndAgreesWithSupportedSyntheticTarget()
    {
        var report = NativeEngineFidelityV111.Compare(Fixture());
        Assert.True(report.ExecutedActualEngineStep, report.EngineFailure ??
            string.Join(",", report.Blockers));
        Assert.Equal("CORE_ENGINE_CHECKPOINT_CONSISTENT", report.Status);
        Assert.Equal("NOT_TESTED", report.NativeGameParity);
        Assert.Null(report.FirstMismatchPath);
        Assert.True(report.CheckedFields.Length > 12);
        Assert.All(report.CheckedFields, check => Assert.True(check.Equal, check.Path));
        Assert.Contains("$.inventory.relics", report.UnverifiedPreconditions);
        Assert.Contains("UNVERIFIED_NATIVE_EVENT_ORDER", report.UnverifiedPreconditions);
    }

    [Fact]
    public void RealCapturedSludgeSpinnerMustNotBeSwappedForAnUnrelatedEnemy()
    {
        var report = NativeEngineFidelityV111.Compare(Fixture(monster: "SLUDGE_SPINNER"));
        Assert.Equal("UNSUPPORTED_NATIVE_STATE", report.Status);
        Assert.False(report.ExecutedActualEngineStep);
        Assert.Contains("UNSUPPORTED_NATIVE_MONSTER:SLUDGE_SPINNER", report.Blockers);
        Assert.Equal("NOT_TESTED", report.NativeGameParity);
    }

    [Fact]
    public void ChangedObservedDamageIsDetectedByTheUnmodifiedEngine()
    {
        var report = NativeEngineFidelityV111.Compare(Fixture(nativeTargetAfterHp: 36));
        Assert.True(report.ExecutedActualEngineStep, report.EngineFailure);
        Assert.Equal("CORE_ENGINE_CHECKPOINT_MISMATCH", report.Status);
        Assert.Equal("$.combat.creatures[creature:1].hp", report.FirstMismatchPath);
        Assert.Equal("NOT_TESTED", report.NativeGameParity);
    }

    [Fact]
    public void UnknownNativeCardNeverReplacedByAnotherImplementation()
    {
        var report = NativeEngineFidelityV111.Compare(Fixture(card: "MAGIC_STRIKE_NOT_EXIST"));
        Assert.Equal("UNSUPPORTED_NATIVE_STATE", report.Status);
        Assert.Contains("UNSUPPORTED_NATIVE_CARD:MAGIC_STRIKE_NOT_EXIST", report.Blockers);
        Assert.False(report.ExecutedActualEngineStep);
    }

    [Fact]
    public void InvalidFixtureIsRejectedBeforeSimulation()
    {
        var fixture = JsonSerializer.SerializeToElement(new
        {
            fixture_schema = "native-expectation-v1", status = "INCOMPLETE_CAPTURE"
        });
        var report = NativeEngineFidelityV111.Compare(fixture);
        Assert.Equal("INVALID_FIXTURE", report.Status);
        Assert.False(report.ExecutedActualEngineStep);
    }
}
