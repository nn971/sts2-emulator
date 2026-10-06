using System.Text.Json;
using Sts2Emulator.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class ReferenceCorpusTests
{
    [Fact]
    public void ResolvesIdsNamesAndClassStyleAliases()
    {
        var root = CreateCorpus();

        try
        {
            var corpus = ReferenceCorpus.Open(root);

            Assert.Equal(
                "STRIKE_SILENT",
                corpus.Resolve("cards", "StrikeSilent").Id);
            Assert.Equal(
                "STRIKE_SILENT",
                corpus.Resolve("cards", "Strike").Id);
            Assert.Equal(
                "RING_OF_THE_SNAKE",
                corpus.Resolve("relics", "RingOfTheSnake").Id);

            var summary = corpus.Summarize();
            var cards = Assert.Single(
                summary.Tables,
                table => table.Kind == "cards");

            Assert.Equal(12, cards.Count);
            Assert.Contains("description", cards.Fields);
            Assert.Contains("target", cards.Fields);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GapReportSeparatesContentAndPrimitiveGaps()
    {
        var root = CreateCorpus();

        try
        {
            var report = ReferenceMechanicsGapAnalyzer.Analyze(
                ReferenceCorpus.Open(root));

            Assert.Equal(12, report.NativeSilentCardCount);
            Assert.Equal(11, report.NameMatchedCardCount);
            Assert.Equal(11, report.StructuredFieldMatchCount);
            Assert.Equal(1, report.MissingNativeCardCount);

            var mystery = Assert.Single(
                report.Cards,
                card => card.NativeId == "MYSTERY_CHOICE");

            Assert.Null(mystery.PrototypeId);
            Assert.Contains("choose_generated", mystery.UnsupportedFeatures);
            Assert.Contains("random_effect", mystery.UnsupportedFeatures);
            Assert.DoesNotContain("random_target", mystery.UnsupportedFeatures);
            Assert.DoesNotContain("retain", mystery.UnsupportedFeatures);
            var retainCoverage = Assert.Single(
                report.FeatureCoverage,
                item => item.Feature == "retain");
            Assert.True(retainCoverage.PrototypeEngineHasPrimitive);
            Assert.Contains(
                mystery.SourceWarnings,
                warning => warning.Contains("cards_draw", StringComparison.Ordinal));
            Assert.Contains(
                mystery.SourceWarnings,
                warning => warning.Contains("spawns_cards", StringComparison.Ordinal));

            var strike = Assert.Single(
                report.Cards,
                card => card.NativeId == "STRIKE_SILENT");
            Assert.Equal("proto.silent.strike", strike.PrototypeId);
            Assert.Empty(strike.StructuredMismatches);

            var grandFinale = Assert.Single(
                report.Cards,
                card => card.NativeId == "GRAND_FINALE");
            Assert.Contains(
                "play_condition_draw_pile_empty",
                grandFinale.RequiredFeatures);
            Assert.DoesNotContain(
                "play_condition_draw_pile_empty",
                grandFinale.UnsupportedFeatures);
            Assert.DoesNotContain(
                "conditional_effect",
                grandFinale.RequiredFeatures);

            var bubbleBubble = Assert.Single(
                report.Cards,
                card => card.NativeId == "BUBBLE_BUBBLE");
            Assert.Contains(
                "condition_target_has_status",
                bubbleBubble.RequiredFeatures);
            Assert.DoesNotContain(
                "condition_target_has_status",
                bubbleBubble.UnsupportedFeatures);
            Assert.DoesNotContain(
                "conditional_effect",
                bubbleBubble.RequiredFeatures);

            var pinpoint = Assert.Single(
                report.Cards,
                card => card.NativeId == "PINPOINT");
            Assert.Contains(
                "dynamic_cost_skills_played",
                pinpoint.RequiredFeatures);
            Assert.DoesNotContain(
                "dynamic_cost_skills_played",
                pinpoint.UnsupportedFeatures);
            Assert.DoesNotContain(
                "cost_mutation",
                pinpoint.RequiredFeatures);

            var pounce = Assert.Single(
                report.Cards,
                card => card.NativeId == "POUNCE");
            Assert.Contains(
                "next_skill_free",
                pounce.RequiredFeatures);
            Assert.DoesNotContain(
                "next_skill_free",
                pounce.UnsupportedFeatures);
            Assert.DoesNotContain(
                "cost_mutation",
                pounce.RequiredFeatures);

            var upMySleeve = Assert.Single(
                report.Cards,
                card => card.NativeId == "UP_MY_SLEEVE");
            Assert.Contains(
                "combat_card_cost_mutation",
                upMySleeve.RequiredFeatures);
            Assert.DoesNotContain(
                "combat_card_cost_mutation",
                upMySleeve.UnsupportedFeatures);
            Assert.DoesNotContain(
                "cost_mutation",
                upMySleeve.RequiredFeatures);

            var burst = Assert.Single(
                report.Cards,
                card => card.NativeId == "BURST");
            Assert.Contains(
                "next_skill_replay",
                burst.RequiredFeatures);
            Assert.DoesNotContain(
                "next_skill_replay",
                burst.UnsupportedFeatures);
            Assert.DoesNotContain(
                "replay",
                burst.RequiredFeatures);

            var bulletTime = Assert.Single(
                report.Cards,
                card => card.NativeId == "BULLET_TIME");
            Assert.Contains(
                "hand_cards_free_this_turn",
                bulletTime.RequiredFeatures);
            Assert.Contains(
                "no_additional_draw",
                bulletTime.RequiredFeatures);
            Assert.DoesNotContain(
                "hand_cards_free_this_turn",
                bulletTime.UnsupportedFeatures);
            Assert.DoesNotContain(
                "no_additional_draw",
                bulletTime.UnsupportedFeatures);
            Assert.DoesNotContain(
                "cost_mutation",
                bulletTime.RequiredFeatures);

            Assert.Empty(report.StartingRunMismatches);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateCorpus()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"sts2-reference-corpus-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        WriteJson(
            root,
            "cards.json",
            """
            [
              {
                "id":"STRIKE_SILENT",
                "name":"Strike",
                "description":"Deal 6 damage.",
                "cost":1,
                "is_x_cost":null,
                "is_x_star_cost":null,
                "star_cost":null,
                "type":"Attack",
                "target":"AnyEnemy",
                "color":"silent",
                "damage":6,
                "block":null,
                "hit_count":null,
                "powers_applied":null,
                "cards_draw":null,
                "energy_gain":null,
                "hp_loss":null,
                "keywords":null,
                "spawns_cards":null,
                "upgrade":{"damage":"+3"}
              },
              {
                "id":"DEFEND_SILENT",
                "name":"Defend",
                "description":"Gain 5 Block.",
                "cost":1,
                "is_x_cost":null,
                "is_x_star_cost":null,
                "star_cost":null,
                "type":"Skill",
                "target":"Self",
                "color":"silent",
                "damage":null,
                "block":5,
                "hit_count":null,
                "powers_applied":null,
                "cards_draw":null,
                "energy_gain":null,
                "hp_loss":null,
                "keywords":null,
                "spawns_cards":null,
                "upgrade":{"block":"+3"}
              },
              {
                "id":"NEUTRALIZE",
                "name":"Neutralize",
                "description":"Deal 3 damage. Apply 1 Weak.",
                "cost":0,
                "is_x_cost":null,
                "is_x_star_cost":null,
                "star_cost":null,
                "type":"Attack",
                "target":"AnyEnemy",
                "color":"silent",
                "damage":3,
                "block":null,
                "hit_count":null,
                "powers_applied":[{"power":"Weak","amount":1}],
                "cards_draw":null,
                "energy_gain":null,
                "hp_loss":null,
                "keywords":null,
                "spawns_cards":null,
                "upgrade":{"damage":"+1","weak":"+1"}
              },
              {
                "id":"SURVIVOR",
                "name":"Survivor",
                "description":"Gain 8 Block. Discard 1 card.",
                "cost":1,
                "is_x_cost":null,
                "is_x_star_cost":null,
                "star_cost":null,
                "type":"Skill",
                "target":"Self",
                "color":"silent",
                "damage":null,
                "block":8,
                "hit_count":null,
                "powers_applied":null,
                "cards_draw":null,
                "energy_gain":null,
                "hp_loss":null,
                "keywords":null,
                "spawns_cards":null,
                "upgrade":{"block":"+3"}
              },
              {
                "id":"MYSTERY_CHOICE",
                "name":"Mystery Choice",
                "description":"Choose 1 of 3 random cards. Add it into your Hand. Play it for free.",
                "cost":1,
                "is_x_cost":null,
                "is_x_star_cost":null,
                "star_cost":null,
                "type":"Skill",
                "target":"RandomEnemy",
                "color":"silent",
                "damage":null,
                "block":null,
                "hit_count":null,
                "powers_applied":null,
                "cards_draw":3,
                "energy_gain":null,
                "hp_loss":null,
                "keywords":["Retain"],
                "spawns_cards":null,
                "upgrade":{}
              },
              {
                "id":"GRAND_FINALE",
                "name":"Grand Finale",
                "description":"Can only be played if there are no cards in your Draw Pile. Deal 60 damage to ALL enemies.",
                "cost":0,
                "is_x_cost":null,
                "is_x_star_cost":null,
                "star_cost":null,
                "type":"Attack",
                "rarity":"Rare",
                "target":"AllEnemies",
                "color":"silent",
                "damage":60,
                "block":null,
                "hit_count":null,
                "powers_applied":null,
                "cards_draw":null,
                "energy_gain":null,
                "hp_loss":null,
                "keywords":null,
                "spawns_cards":null,
                "upgrade":{"damage":"+15"}
              },
              {
                "id":"BUBBLE_BUBBLE",
                "name":"Bubble Bubble",
                "description":"If the enemy has Poison, apply 9 Poison.",
                "cost":1,
                "is_x_cost":null,
                "is_x_star_cost":null,
                "star_cost":null,
                "type":"Skill",
                "rarity":"Uncommon",
                "target":"AnyEnemy",
                "color":"silent",
                "damage":null,
                "block":null,
                "hit_count":null,
                "powers_applied":[{"power":"Poison","amount":9}],
                "cards_draw":null,
                "energy_gain":null,
                "hp_loss":null,
                "keywords":null,
                "spawns_cards":null,
                "upgrade":{"poison":"+3"}
              },
              {
                "id":"PINPOINT",
                "name":"Pinpoint",
                "description":"Deal 15 damage. Costs 1 less for each Skill played this turn.",
                "cost":3,
                "is_x_cost":null,
                "is_x_star_cost":null,
                "star_cost":null,
                "type":"Attack",
                "rarity":"Uncommon",
                "target":"AnyEnemy",
                "color":"silent",
                "damage":15,
                "block":null,
                "hit_count":null,
                "powers_applied":null,
                "cards_draw":null,
                "energy_gain":null,
                "hp_loss":null,
                "keywords":null,
                "spawns_cards":null,
                "upgrade":{"damage":"+4"}
              },
              {
                "id":"POUNCE",
                "name":"Pounce",
                "description":"Deal 14 damage. The next Skill you play costs 0 [energy:1].",
                "cost":2,
                "is_x_cost":null,
                "is_x_star_cost":null,
                "star_cost":null,
                "type":"Attack",
                "rarity":"Uncommon",
                "target":"AnyEnemy",
                "color":"silent",
                "damage":14,
                "block":null,
                "hit_count":null,
                "powers_applied":null,
                "cards_draw":null,
                "energy_gain":null,
                "hp_loss":null,
                "keywords":null,
                "spawns_cards":null,
                "upgrade":{"damage":"+6"}
              },
              {
                "id":"UP_MY_SLEEVE",
                "name":"Up My Sleeve",
                "description":"Add 3 Shivs into your Hand. Reduce this card's cost by 1.",
                "cost":2,
                "is_x_cost":null,
                "is_x_star_cost":null,
                "star_cost":null,
                "type":"Skill",
                "rarity":"Uncommon",
                "target":"Self",
                "color":"silent",
                "damage":null,
                "block":null,
                "hit_count":null,
                "powers_applied":null,
                "cards_draw":3,
                "energy_gain":null,
                "hp_loss":null,
                "keywords":null,
                "spawns_cards":["SHIV"],
                "upgrade":{"cards":"+1"}
              },
              {
                "id":"BURST",
                "name":"Burst",
                "description":"This turn, your next Skill is played an extra time.",
                "cost":1,
                "is_x_cost":null,
                "is_x_star_cost":null,
                "star_cost":null,
                "type":"Skill",
                "rarity":"Rare",
                "target":"Self",
                "color":"silent",
                "damage":null,
                "block":null,
                "hit_count":null,
                "powers_applied":null,
                "cards_draw":null,
                "energy_gain":null,
                "hp_loss":null,
                "keywords":null,
                "spawns_cards":null,
                "upgrade":{"skills":"+1"}
              },
              {
                "id":"BULLET_TIME",
                "name":"Bullet Time",
                "description":"You cannot draw additional cards this turn. ALL cards in your Hand are free to play this turn.",
                "cost":3,
                "is_x_cost":null,
                "is_x_star_cost":null,
                "star_cost":null,
                "type":"Skill",
                "rarity":"Rare",
                "target":"Self",
                "color":"silent",
                "damage":null,
                "block":null,
                "hit_count":null,
                "powers_applied":null,
                "cards_draw":null,
                "energy_gain":null,
                "hp_loss":null,
                "keywords":null,
                "spawns_cards":null,
                "upgrade":{"cost":2}
              }
            ]
            """);

        WriteJson(
            root,
            "characters.json",
            """
            [
              {
                "id":"SILENT",
                "name":"The Silent",
                "starting_hp":70,
                "starting_gold":99,
                "max_energy":3,
                "starting_deck":[
                  "StrikeSilent","StrikeSilent","StrikeSilent","StrikeSilent","StrikeSilent",
                  "DefendSilent","DefendSilent","DefendSilent","DefendSilent","DefendSilent",
                  "Neutralize","Survivor"
                ],
                "starting_relics":["RingOfTheSnake"]
              }
            ]
            """);

        WriteJson(
            root,
            "relics.json",
            """
            [
              {
                "id":"RING_OF_THE_SNAKE",
                "name":"Ring of the Snake",
                "description":"At the start of each combat, draw 2 additional cards."
              }
            ]
            """);

        return root;
    }

    private static void WriteJson(
        string root,
        string filename,
        string json)
    {
        using var document = JsonDocument.Parse(json);
        File.WriteAllText(
            Path.Combine(root, filename),
            JsonSerializer.Serialize(document.RootElement));
    }
}
