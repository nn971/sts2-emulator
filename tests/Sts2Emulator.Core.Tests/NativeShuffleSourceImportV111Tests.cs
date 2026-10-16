using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeShuffleSourceImportV111Tests
{
    private static (JsonElement Record, CombatState Before,
        Dictionary<string, long> Mapping) Fixture()
    {
        static string Normalize(string s) => new(
            s.Where(char.IsLetterOrDigit)
             .Select(char.ToUpperInvariant).ToArray());
        string cardId = V111Coverage.Create().Items.Single(item =>
            item.Kind == "cards" && item.Registered &&
            item.Scope == "single-player" &&
            Normalize(item.NativeId) == Normalize("STRIKE_SILENT") &&
            item.EngineIds.Length == 1).EngineIds[0];
        long[] ids = [20, 21, 22, 23];
        var cards = ids.Select(id => new CombatCardInstance(
            id, null, cardId, 0, false, PrototypeJson.EmptyObject())).ToArray();
        var combat = new CombatState(
            Turn: 1, Energy: 3, PlayerBlock: 0,
            Hand: [], DrawPile: [], DiscardPile: ids,
            ExhaustPile: [], Enemies: [],
            NextCardInstanceId: 24, Cards: cards,
            PlayerPowers: [], NextPowerApplicationOrder: 1);
        var rng = NativeRngV111.CreateBundle("actual-native-import");
        var swap = NativeFisherYatesV111.Execute(
            ids, rng, new NativeRandomIndexProviderV111());
        static string Token(long id) => "card:" + (id - 20);
        var source = new
        {
            type = "native_shuffle_operation",
            data = new
            {
                operation_id = 3,
                kind = "recycle",
                source_version = "v0.111.0",
                card_identity_scope = "shuffle_operation_local",
                hook_order_stage = "modify_shuffle_order_completed",
                ordered_input = swap.OrderedInput.Select(Token).ToArray(),
                card_models = ids.Select(_ => "STRIKE_SILENT").ToArray(),
                pre_hook_order = swap.PreHookOutput.Select(Token).ToArray(),
                post_hook_order = swap.PreHookOutput.Select(Token).ToArray(),
                choices = swap.Choices.Select(c => new
                {
                    bounded_count = c.Request.BoundedCount,
                    selected_index = c.SelectedIndex,
                    cursor_before = c.NativeCursorBefore,
                    cursor_after = c.NativeCursorAfter
                }).ToArray()
            }
        };
        return (JsonSerializer.SerializeToElement(source), combat,
            ids.ToDictionary(Token, id => id));
    }

    [Fact]
    public void AuthenticatedSourceChoicesAndExplicitDuplicateModelMappingsReplay()
    {
        var (source, combat, map) = Fixture();
        var expected = NativeShuffleSourceImportV111.Import(source, combat, map);
        Assert.Equal([20L, 21L, 22L, 23L], expected.OrderedSourceInput);
        Assert.Equal(3, expected.SourceSelectedIndices.Length);
        Assert.Equal("INDEPENDENTLY_MAPPED", expected.CardIdentityStatus);
        using var scope = NativeShuffleStepReplayV111.Begin(
            [expected], NativeRngV111.CreateBundle("scripted-shuffle"));
        var actual = NativeShuffleStepReplayV111.ShuffleRecycle(
            (long[])combat.DiscardPile.Clone(),
            NativeRngV111.CreateBundle("unchanged-proto"));
        NativeShuffleStepReplayV111.CheckPostHook(actual);
        scope.AssertComplete();
        Assert.Equal("SOURCE_SHUFFLE_STAGES_CONSISTENT", scope.Status);
    }

    [Fact]
    public void UnknownOrAliasedTokenCannotClaimNativeIdentity()
    {
        var (source, combat, map) = Fixture();
        map.Remove("card:2");
        Assert.Throws<ArgumentException>(() =>
            NativeShuffleSourceImportV111.Import(source, combat, map));
        (_, combat, map) = Fixture();
        map["card:2"] = map["card:0"];
        Assert.Throws<ArgumentException>(() =>
            NativeShuffleSourceImportV111.Import(source, combat, map));
    }

    [Fact]
    public void NativeSourceStateAndEligibleCardsMustBeIndependentlyVerified()
    {
        var (source, combat, map) = Fixture();
        Assert.Throws<ArgumentException>(() =>
            NativeShuffleSourceImportV111.Import(source,
                combat with { DiscardPile = combat.DiscardPile.Skip(1).ToArray() },
                map));
        // A move in the primitive cursor with unchanged claimed selected
        // index is a hard source audit failure during import.
        var json = System.Text.Json.Nodes.JsonNode.Parse(source.GetRawText())!;
        var choices = json["data"]!["choices"]!.AsArray();
        choices[0]!["cursor_after"]!["State2"] =
            0UL;
        using var altered = JsonDocument.Parse(json.ToJsonString());
        Assert.Throws<ArgumentException>(() =>
            NativeShuffleSourceImportV111.Import(altered.RootElement, combat, map));
    }

    [Fact]
    public void InvalidSourceKindNeverBecomesAnInCombatEngineStep()
    {
        var (source, combat, map) = Fixture();
        var json = System.Text.Json.Nodes.JsonNode.Parse(source.GetRawText())!;
        json["data"]!["kind"] = "initial";
        using var altered = JsonDocument.Parse(json.ToJsonString());
        Assert.Throws<ArgumentException>(() =>
            NativeShuffleSourceImportV111.Import(altered.RootElement, combat, map));
    }
}
