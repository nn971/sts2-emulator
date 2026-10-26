using System.Text.Json;
using System.Text.Json.Nodes;
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
    public void RealEngineJournalCapturesStrikeExecutionOrder()
    {
        var result = NativeEngineFidelityV111.Compare(Fixture(monster: "SLUDGE_SPINNER"));
        var journal = Assert.IsType<NativeEngineJournalEntryV111[]>(
            result.EmulatorEventJournal);
        Assert.Equal(Enumerable.Range(1, journal.Length),
            journal.Select(entry => entry.Sequence));
        var energy = Assert.Single(journal, entry =>
            entry.Kind == "energy_spent");
        var damage = Assert.Single(journal, entry =>
            entry.Kind == "enemy_damage");
        var cardPlayed = Assert.Single(journal, entry =>
            entry.Stage == "combat_event_dispatch" &&
            entry.Kind == "CardPlayed");
        Assert.Equal(1, energy.Amount);
        Assert.Equal(6, damage.Amount);
        Assert.True(energy.Sequence < damage.Sequence);
        Assert.True(damage.Sequence < cardPlayed.Sequence);
        Assert.Equal("NOT_TESTED", result.NativeGameParity);
    }

    [Fact]
    public void NestedFidelityJournalAlsoRecordsInItsParentScope()
    {
        using var parent = NativeEngineEventJournalV111.Begin();
        var result = NativeEngineFidelityV111.Compare(Fixture());
        Assert.NotNull(result.EmulatorEventJournal);
        Assert.NotEmpty(result.EmulatorEventJournal!);
        Assert.NotEmpty(parent.Entries);
    }

    [Fact]
    public void FidelityMismatchRetainsExecutionJournal()
    {
        var result = NativeEngineFidelityV111.Compare(Fixture(nativeTargetAfterHp: 36));
        Assert.Equal("CORE_ENGINE_CHECKPOINT_MISMATCH", result.Status);
        Assert.Contains(result.EmulatorEventJournal!, entry =>
            entry.Kind == "enemy_damage" && entry.Amount == 6);
    }

    [Fact]
    public void RealNativeSludgeSpinnerIdentityMapsToSourceBackedEngineAndSteps()
    {
        // SLUDGE_SPINNER is registered in the executable v111 content,
        // although the initial GitHub code-text search did not find it.
        var report = NativeEngineFidelityV111.Compare(Fixture(monster: "SLUDGE_SPINNER"));
        Assert.Equal("CORE_ENGINE_CHECKPOINT_CONSISTENT", report.Status);
        Assert.True(report.ExecutedActualEngineStep, report.EngineFailure);
        Assert.All(report.CheckedFields, check => Assert.True(check.Equal, check.Path));
        Assert.Equal("NOT_TESTED", report.NativeGameParity);
    }

    [Fact]
    public void UnknownMonsterNeverSilentlyBecomesAnUnrelatedSpecies()
    {
        var report = NativeEngineFidelityV111.Compare(Fixture(monster: "MISSING_NATIVE_MONSTER"));
        Assert.Equal("UNSUPPORTED_NATIVE_STATE", report.Status);
        Assert.False(report.ExecutedActualEngineStep);
        Assert.Contains("UNSUPPORTED_NATIVE_MONSTER:MISSING_NATIVE_MONSTER", report.Blockers);
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
    public void FullSevenCardNativeShapedSludgeSpinnerStrikeRunsActualEngine()
    {
        static object Card(long id, string model, int cost, string target) =>
            new { instance_id = id, model_id = model, cost,
                upgrades = 0, target_type = target };
        object[] hand =
        [
            Card(0, "STRIKE_SILENT", 1, "AnyEnemy"),
            Card(1, "SURVIVOR", 1, "Self"),
            Card(2, "DEFEND_SILENT", 1, "Self"),
            Card(3, "DEFEND_SILENT", 1, "Self"),
            Card(4, "DEFEND_SILENT", 1, "Self"),
            Card(5, "DEFEND_SILENT", 1, "Self"),
            Card(6, "DEFEND_SILENT", 1, "Self")
        ];
        object[] draw =
        [
            Card(7, "NEUTRALIZE", 0, "AnyEnemy"),
            Card(8, "ASCENDERS_BANE", 0, "None"),
            Card(9, "STRIKE_SILENT", 1, "AnyEnemy"),
            Card(10, "STRIKE_SILENT", 1, "AnyEnemy"),
            Card(11, "STRIKE_SILENT", 1, "AnyEnemy"),
            Card(12, "STRIKE_SILENT", 1, "AnyEnemy")
        ];
        object Snapshot(bool post) => new
        {
            player = new { hp = 56, max_hp = 70, gold = 99, block = 0,
                powers = Array.Empty<object>() },
            combat = new
            {
                energy = post ? 2 : 3,
                turn = 1,
                creatures = new[]
                {
                    new { id = "creature:0", model_id = "SILENT", hp = 56,
                        max_hp = 70, block = 0, alive = true,
                        powers = Array.Empty<object>() },
                    new { id = "creature:1", model_id = "SLUDGE_SPINNER",
                        hp = post ? 35 : 41, max_hp = 41, block = 0,
                        alive = true, powers = Array.Empty<object>() }
                },
                hand = post ? hand.Skip(1).ToArray() : hand,
                draw_pile = draw,
                discard_pile = post ? new[] { hand[0] } : Array.Empty<object>(),
                exhaust_pile = Array.Empty<object>()
            }
        };
        var fixture = JsonSerializer.SerializeToElement(new
        {
            fixture_schema = "native-expectation-v1",
            status = "RECORDED_NATIVE_EXPECTATION",
            native_trace_header = new { game_build = "synthetic-shaped-like-captured-strike" },
            selected_action = new
            {
                kind = "play_card",
                payload = new { card_instance_id = 0L, target_id = "creature:1" }
            },
            pre_state = Snapshot(false),
            post_state = Snapshot(true)
        });
        var report = NativeEngineFidelityV111.Compare(fixture);
        Assert.True(report.Status == "CORE_ENGINE_CHECKPOINT_CONSISTENT",
            report.Status + ": " + (report.EngineFailure ??
                string.Join(", ", report.Blockers)) + "; mismatch=" + report.FirstMismatchPath);
        Assert.True(report.ExecutedActualEngineStep, report.EngineFailure ??
            string.Join(", ", report.Blockers));
        Assert.Equal("NOT_TESTED", report.NativeGameParity);
        Assert.Null(report.FirstMismatchPath);
        Assert.True(report.CheckedFields.Length >= 25);
    }

    [Fact]
    public void UnknownNativeCardNeverReplacedByAnotherImplementation()
    {
        var report = NativeEngineFidelityV111.Compare(Fixture(card: "MAGIC_STRIKE_NOT_EXIST"));
        Assert.Equal("UNSUPPORTED_NATIVE_STATE", report.Status);
        Assert.Contains("UNSUPPORTED_NATIVE_CARD:MAGIC_STRIKE_NOT_EXIST", report.Blockers);
        Assert.False(report.ExecutedActualEngineStep);
    }

    private static JsonElement ChangedFixture(Action<JsonNode> change)
    {
        var root = JsonNode.Parse(Fixture().GetRawText())!;
        change(root);
        return JsonSerializer.SerializeToElement(root);
    }

    [Fact]
    public void MultipleNativeCardPlaysInsideOneActionRequireCausalHookEvidence()
    {
        var fixture = ChangedFixture(root =>
            root["native_events"] = JsonNode.Parse(
                """[{"kind":"card_play_started"},{"kind":"block_gained"},{"kind":"card_play_started"}]"""));
        var report = NativeEngineFidelityV111.Compare(fixture);
        Assert.Equal("UNSUPPORTED_NATIVE_STATE", report.Status);
        Assert.False(report.ExecutedActualEngineStep);
        Assert.Contains("UNVERIFIED_MULTI_CARD_PLAY_TRIGGER_FROM_NATIVE_HISTORY",
            report.Blockers);
        Assert.Equal("NOT_TESTED", report.NativeGameParity);
    }

    [Fact]
    public void UncheckedVisibleNativePowerCannotBeReportedAsFullyConsistent()
    {
        var fixture = ChangedFixture(root =>
            root["post_state"]!["combat"]!["creatures"]![1]!["powers"] =
                JsonNode.Parse("""[{"model_id":"WEAK_POWER","amount":1}]"""));
        var report = NativeEngineFidelityV111.Compare(fixture);
        Assert.True(report.ExecutedActualEngineStep);
        Assert.Equal("CORE_ENGINE_CHECKPOINT_PARTIAL", report.Status);
        Assert.Contains(report.UnverifiedPreconditions, item =>
            item == "POWER_SEMANTICS_UNVERIFIED:creature:1");
        Assert.Equal("NOT_TESTED", report.NativeGameParity);
        Assert.All(report.CheckedFields, check => Assert.True(check.Equal, check.Path));
    }

    [Fact]
    public void MissingNativePostEnemyIsACheckpointMismatch()
    {
        var fixture = ChangedFixture(root =>
            root["post_state"]!["combat"]!["creatures"]!.AsArray().RemoveAt(1));
        var report = NativeEngineFidelityV111.Compare(fixture);
        Assert.True(report.ExecutedActualEngineStep);
        Assert.Equal("CORE_ENGINE_CHECKPOINT_MISMATCH", report.Status);
        Assert.Equal("$.combat.creatures.ids", report.FirstMismatchPath);
    }

    [Fact]
    public void DuplicateNativePostEnemyIsACheckpointMismatch()
    {
        var fixture = ChangedFixture(root =>
        {
            var creatures = root["post_state"]!["combat"]!["creatures"]!.AsArray();
            creatures.Add(creatures[1]!.DeepClone());
        });
        var report = NativeEngineFidelityV111.Compare(fixture);
        Assert.True(report.ExecutedActualEngineStep);
        Assert.Equal("CORE_ENGINE_CHECKPOINT_MISMATCH", report.Status);
        Assert.Equal("$.combat.creatures.ids", report.FirstMismatchPath);
    }

    [Fact]
    public void ChangedNativeEnemyModelIsDetectedEvenWhenHpMatches()
    {
        var fixture = ChangedFixture(root =>
            root["post_state"]!["combat"]!["creatures"]![1]!["model_id"] =
                "LEAF_SLIME_M");
        var report = NativeEngineFidelityV111.Compare(fixture);
        Assert.True(report.ExecutedActualEngineStep);
        Assert.Equal("CORE_ENGINE_CHECKPOINT_MISMATCH", report.Status);
        Assert.Equal("$.combat.creatures[creature:1].model_id", report.FirstMismatchPath);
    }

    [Fact]
    public void UnknownNativePostCreatureCannotAliasPlayerState()
    {
        var fixture = ChangedFixture(root =>
        {
            var creature = root["post_state"]!["combat"]!["creatures"]![1]!;
            creature["id"] = "creature:99";
            creature["model_id"] = "SILENT";
            creature["hp"] = 56;
            creature["max_hp"] = 70;
        });
        var report = NativeEngineFidelityV111.Compare(fixture);
        Assert.Equal("CORE_ENGINE_CHECKPOINT_MISMATCH", report.Status);
        Assert.Equal("$.combat.creatures.ids", report.FirstMismatchPath);
    }

    [Fact]
    public void PreStatePlayerAndCreatureMustDescribeTheSamePlayer()
    {
        var fixture = ChangedFixture(root =>
            root["pre_state"]!["combat"]!["creatures"]![0]!["hp"] = 55);
        var report = NativeEngineFidelityV111.Compare(fixture);
        Assert.Equal("UNSUPPORTED_NATIVE_STATE", report.Status);
        Assert.False(report.ExecutedActualEngineStep);
        Assert.Contains("INCONSISTENT_NATIVE_PLAYER_CREATURE", report.Blockers);
    }

    [Fact]
    public void SelectedNativeCardMustExistInOriginalHand()
    {
        var fixture = ChangedFixture(root =>
            root["selected_action"]!["payload"]!["card_instance_id"] = 999);
        var report = NativeEngineFidelityV111.Compare(fixture);
        Assert.Equal("UNSUPPORTED_NATIVE_STATE", report.Status);
        Assert.False(report.ExecutedActualEngineStep);
        Assert.Contains("NATIVE_SELECTED_CARD_NOT_IN_HAND", report.Blockers);
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
