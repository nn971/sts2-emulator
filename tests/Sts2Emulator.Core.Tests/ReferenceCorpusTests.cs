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

            Assert.Equal(5, cards.Count);
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

            Assert.Equal(5, report.NativeSilentCardCount);
            Assert.Equal(4, report.NameMatchedCardCount);
            Assert.Equal(4, report.StructuredFieldMatchCount);
            Assert.Equal(1, report.MissingNativeCardCount);

            var mystery = Assert.Single(
                report.Cards,
                card => card.NativeId == "MYSTERY_CHOICE");

            Assert.Null(mystery.PrototypeId);
            Assert.Contains("choose_generated", mystery.UnsupportedFeatures);
            Assert.Contains("random_effect", mystery.UnsupportedFeatures);
            Assert.Contains("random_target", mystery.UnsupportedFeatures);
            Assert.Contains("retain", mystery.UnsupportedFeatures);
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
