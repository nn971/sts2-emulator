using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeBasicCardStepperTests
{
    private static string BeforeState(string model = "STRIKE_SILENT",
        string targetType = "AnyEnemy", int upgrade = 0,
        string playerPowers = "[]", string targetPowers = "[]")
    {
        return $$"""
        {
          "player":{"hp":70,"max_hp":70,"block":0,"powers":{{playerPowers}}},
          "combat":{
            "energy":3,"max_energy":3,"turn":1,
            "creatures":[
              {"id":"creature:0","model_id":"SILENT","hp":70,"max_hp":70,
               "block":0,"alive":true,"powers":[]},
              {"id":"creature:1","model_id":"SLUDGE_SPINNER","hp":41,"max_hp":41,
               "block":0,"alive":true,"powers":{{targetPowers}}}
            ],
            "hand":[{"instance_id":0,"model_id":"{{model}}",
                     "upgrades":{{upgrade}},"cost":1,
                     "target_type":"{{targetType}}"}],
            "draw_pile":[],"discard_pile":[],"exhaust_pile":[]
          }
        }
        """;
    }

    private static GameAction Action(string? target) =>
        new("play_card", JsonSerializer.SerializeToElement(
            new { card_instance_id = 0, target_id = target }));

    private static NativeCombatSnapshot Import(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = NativeCombatSnapshotImporter.Import(doc.RootElement);
        Assert.Equal("PARTIAL", result.Status);
        return Assert.IsType<NativeCombatSnapshot>(result.Snapshot);
    }

    [Fact]
    public void StrikeUsesIndependentBasicCardRuleButDoesNotClaimParity()
    {
        var outcome = NativeBasicCardStepper.Project(Import(BeforeState()), Action("creature:1"));
        Assert.Equal("RULE_PROJECTION_ONLY", outcome.Status);
        Assert.Equal(2, outcome.Energy);
        Assert.Equal(35, outcome.TargetHp);
        Assert.Equal(0, outcome.TargetBlock);
        Assert.Equal(0, outcome.HandCount);
        Assert.Equal(1, outcome.DiscardCount);
        Assert.Contains("RELIC_AND_POTION_EFFECTS_UNOBSERVED", outcome.UnverifiedContinuation);
    }

    [Fact]
    public void UpgradedStrikeIndependentRuleDealsNine()
    {
        var outcome = NativeBasicCardStepper.Project(
            Import(BeforeState(upgrade: 1)), Action("creature:1"));
        Assert.Equal(32, outcome.TargetHp);
    }

    [Fact]
    public void DefendIndependentRuleGainsFiveBlock()
    {
        var outcome = NativeBasicCardStepper.Project(
            Import(BeforeState("DEFEND_SILENT", "Self")), Action(null));
        Assert.Equal("RULE_PROJECTION_ONLY", outcome.Status);
        Assert.Equal(5, outcome.PlayerBlock);
        Assert.Null(outcome.TargetHp);
        Assert.Equal(2, outcome.Energy);
    }

    [Fact]
    public void PowersFailClosedRatherThanAssumingDamage()
    {
        var snapshot = Import(BeforeState(playerPowers:
            """[{"model_id":"Strength","amount":1}]"""));
        var outcome = NativeBasicCardStepper.Project(snapshot, Action("creature:1"));
        Assert.Equal("UNSUPPORTED", outcome.Status);
    }

    [Fact]
    public void EnemyPowersFailClosed()
    {
        var snapshot = Import(BeforeState(targetPowers:
            """[{"model_id":"Vulnerable","amount":1}]"""));
        Assert.Equal("UNSUPPORTED",
            NativeBasicCardStepper.Project(snapshot, Action("creature:1")).Status);
    }

    [Fact]
    public void UnspecifiedTargetAndInvalidCardDoNotStep()
    {
        var snapshot = Import(BeforeState());
        Assert.Equal("UNSUPPORTED",
            NativeBasicCardStepper.Project(snapshot, Action(null)).Status);
        var missingCard = new GameAction("play_card", JsonSerializer.SerializeToElement(
            new { card_instance_id = 999, target_id = "creature:1" }));
        Assert.Equal("UNSUPPORTED",
            NativeBasicCardStepper.Project(snapshot, missingCard).Status);
    }

    [Fact]
    public void NativeExpectationComparisonIsOnlyRuleLevelConsistency()
    {
        using var before = JsonDocument.Parse(BeforeState());
        using var after = JsonDocument.Parse(BeforeState()
            .Replace("\"energy\":3", "\"energy\":2")
            .Replace("\"hp\":41", "\"hp\":35")
            .Replace("\"hand\":[{\"instance_id\":0",
                     "\"discard_pile\":[{\"instance_id\":0")
            .Replace("\"draw_pile\":[],\"discard_pile\":[],",
                     "\"draw_pile\":[],\"hand\":[],"));
        // Construct a canonical synthetic post-state without fragile string
        // movement replacement (the comparator accepts complete state data).
        var post = JsonSerializer.SerializeToElement(new {
            player = new { hp = 70, max_hp = 70, block = 0, powers = Array.Empty<object>() },
            combat = new {
                energy = 2, max_energy = 3, turn = 1,
                creatures = new[] {
                    new { id = "creature:0", model_id = "SILENT", hp = 70, max_hp = 70,
                          block = 0, alive = true, powers = Array.Empty<object>() },
                    new { id = "creature:1", model_id = "SLUDGE_SPINNER", hp = 35, max_hp = 41,
                          block = 0, alive = true, powers = Array.Empty<object>() }
                },
                hand = Array.Empty<object>(),
                draw_pile = Array.Empty<object>(),
                discard_pile = new[] {
                    new { instance_id = 0, model_id = "STRIKE_SILENT",
                          upgrades = 0, cost = 1, target_type = "AnyEnemy" }
                },
                exhaust_pile = Array.Empty<object>()
            }
        });
        var fixture = JsonSerializer.SerializeToElement(new {
            status = "RECORDED_NATIVE_EXPECTATION",
            pre_state = before.RootElement,
            post_state = post,
            selected_action = new {
                kind = "play_card",
                payload = new { card_instance_id = 0, target_id = "creature:1" }
            }
        });
        var report = NativeBasicRuleComparer.Check(fixture);
        Assert.Equal("RULE_PROJECTION_CONSISTENT", report.Status);
        Assert.Equal("NOT_TESTED", report.EmulatorParity);
        Assert.All(report.Fields, entry => Assert.True(entry.Agrees, entry.Field));
        Assert.NotEmpty(report.Projection!.UnverifiedContinuation);

        using var wrong = JsonDocument.Parse(post.ToString().Replace(
            "\"hp\":35", "\"hp\":36"));
        var badFixture = JsonSerializer.SerializeToElement(new {
            status = "RECORDED_NATIVE_EXPECTATION",
            pre_state = before.RootElement,
            post_state = wrong.RootElement,
            selected_action = new {
                kind = "play_card",
                payload = new { card_instance_id = 0, target_id = "creature:1" }
            }
        });
        var mismatch = NativeBasicRuleComparer.Check(badFixture);
        Assert.Equal("RULE_PROJECTION_MISMATCH", mismatch.Status);
        Assert.Equal("NOT_TESTED", mismatch.EmulatorParity);
    }
}
