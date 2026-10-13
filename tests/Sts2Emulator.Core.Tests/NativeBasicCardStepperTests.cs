using System.Text.Json;
using System.Text.Json.Nodes;
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
    [Fact]
    public void IndependentVisibleStepMatchesAllCapturedFieldsInSyntheticStrike()
    {
        using var before = JsonDocument.Parse(BeforeState());
        // Synthetic expectation is independently assembled by the test: no
        // native fixture post-state feeds the actual state stepper.
        var expected = JsonNode.Parse(BeforeState())!.AsObject();
        var combat = expected["combat"]!.AsObject();
        combat["energy"] = 2;
        combat["creatures"]!.AsArray()[1]!["hp"] = 35;
        var hand = combat["hand"]!.AsArray();
        var card = hand[0]!.DeepClone();
        hand.RemoveAt(0);
        combat["discard_pile"]!.AsArray().Add(card);

        var action = new {
            kind = "play_card",
            payload = new { card_instance_id = 0, target_id = "creature:1" }
        };
        var fixture = JsonSerializer.SerializeToElement(new {
            status = "RECORDED_NATIVE_EXPECTATION",
            pre_state = before.RootElement,
            post_state = expected,
            selected_action = action
        });
        var result = NativeVisibleSliceComparer.Check(fixture);
        Assert.Equal("VISIBLE_SLICE_CONSISTENT", result.Status);
        Assert.Equal("NOT_TESTED", result.EmulatorParity);
        Assert.Null(result.FirstDifferencePath);
        Assert.NotNull(result.ProjectedPostState);
        Assert.Contains("RNG_CONTINUATION_UNOBSERVED", result.UnverifiedContinuation);

        // Perturb actual observation, not the independent computation.
        expected["combat"]!["creatures"]![1]!["hp"] = 36;
        var mismatchedFixture = JsonSerializer.SerializeToElement(new {
            status = "RECORDED_NATIVE_EXPECTATION",
            pre_state = before.RootElement,
            post_state = expected,
            selected_action = action
        });
        var mismatch = NativeVisibleSliceComparer.Check(mismatchedFixture);
        Assert.Equal("VISIBLE_SLICE_MISMATCH", mismatch.Status);
        Assert.Equal("$.combat.creatures[1].hp", mismatch.FirstDifferencePath);
        Assert.Equal("NOT_TESTED", mismatch.EmulatorParity);
    }

    [Fact]
    public void VisibleStepRejectsPowersRatherThanClaimingReplay()
    {
        using var before = JsonDocument.Parse(BeforeState(playerPowers:
            """[{"model_id":"Strength","amount":1}]"""));
        var fixture = JsonSerializer.SerializeToElement(new {
            status = "RECORDED_NATIVE_EXPECTATION",
            pre_state = before.RootElement,
            post_state = before.RootElement,
            selected_action = new {
                kind = "play_card",
                payload = new { card_instance_id = 0, target_id = "creature:1" }
            }
        });
        var result = NativeVisibleSliceComparer.Check(fixture);
        Assert.Equal("UNSUPPORTED", result.Status);
        Assert.Equal("NOT_TESTED", result.EmulatorParity);
    }

    [Fact]
    public void VisibleStepUpdatesBothPlayerBlockRepresentationsForDefend()
    {
        using var before = JsonDocument.Parse(BeforeState("DEFEND_SILENT", "Self"));
        var native = JsonNode.Parse(BeforeState("DEFEND_SILENT", "Self"))!.AsObject();
        native["player"]!["block"] = 5;
        native["combat"]!["creatures"]![0]!["block"] = 5;
        native["combat"]!["energy"] = 2;
        var hand = native["combat"]!["hand"]!.AsArray();
        var played = hand[0]!.DeepClone();
        hand.RemoveAt(0);
        native["combat"]!["discard_pile"]!.AsArray().Add(played);
        var fixture = JsonSerializer.SerializeToElement(new {
            status = "RECORDED_NATIVE_EXPECTATION",
            pre_state = before.RootElement,
            post_state = native,
            selected_action = new {
                kind = "play_card",
                payload = new { card_instance_id = 0, target_id = (string?)null }
            }
        });
        var result = NativeVisibleSliceComparer.Check(fixture);
        Assert.Equal("VISIBLE_SLICE_CONSISTENT", result.Status);
        Assert.Equal("NOT_TESTED", result.EmulatorParity);
    }

}
