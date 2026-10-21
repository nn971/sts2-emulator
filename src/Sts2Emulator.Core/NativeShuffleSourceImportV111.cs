using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Converts a REAL native shuffle source observation to a source-ordered
/// emulator Step expectation. The native sidecar uses card:0/card:1 tokens
/// scoped to ONE operation; an explicit independently verified one-to-one
/// map is mandatory. No card identities are inferred from model duplicates,
/// and no native post-state is copied into the emulator.
/// </summary>
public static class NativeShuffleSourceImportV111
{
    private static JsonElement Field(JsonElement element, string key)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(key, out var property))
            throw new ArgumentException("Missing native source field: " + key);
        return property;
    }

    private static string String(JsonElement obj, string key) =>
        Field(obj, key).GetString()
        ?? throw new ArgumentException("Invalid native string: " + key);

    private static long[] MapTokens(
        JsonElement arr, IReadOnlyDictionary<string, long> explicitMap)
    {
        if (arr.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("Expected ordered native card tokens.");
        return arr.EnumerateArray().Select(item =>
        {
            if (item.ValueKind != JsonValueKind.String ||
                !explicitMap.TryGetValue(item.GetString()!, out long id))
                throw new ArgumentException(
                    "Native shuffle card token has no independent emulator ID mapping.");
            return id;
        }).ToArray();
    }

    private static NativeRngSnapshot Snapshot(JsonElement element)
    {
        long GetCount()
        {
            if (element.TryGetProperty("Counter", out var v) ||
                element.TryGetProperty("counter", out v))
                return v.GetInt64();
            throw new ArgumentException("Missing native RNG counter.");
        }
        ulong Word(string name)
        {
            if (element.TryGetProperty(name, out var v))
                return v.GetUInt64();
            var camel = char.ToLowerInvariant(name[0]) + name[1..];
            return Field(element, camel).GetUInt64();
        }
        long counter = GetCount();
        if (counter < 0 || counter > int.MaxValue)
            throw new ArgumentException("Invalid native RNG source counter.");
        return new NativeRngSnapshot((int)counter,
            Word("State0"), Word("State1"), Word("State2"), Word("State3"));
    }

    private static string? ResolveNativeCardModel(string nativeEntry)
    {
        static string Normalize(string s) => new(
            s.Where(char.IsLetterOrDigit)
             .Select(char.ToUpperInvariant).ToArray());
        var models = V111Coverage.Create().Items.Where(item =>
            item.Kind == "cards" &&
            Normalize(item.NativeId) == Normalize(nativeEntry) &&
            item.Registered && item.Scope == "single-player" &&
            item.EngineIds.Length == 1).ToArray();
        return models.Length == 1 ? models[0].EngineIds[0] : null;
    }

    public static NativeShuffleStepExpectationV111 Import(
        JsonElement sourceRecord, CombatState emulatorBefore,
        IReadOnlyDictionary<string, long> independentlyMappedCardTokens)
    {
        ArgumentNullException.ThrowIfNull(emulatorBefore);
        ArgumentNullException.ThrowIfNull(independentlyMappedCardTokens);
        var data = sourceRecord;
        if (sourceRecord.ValueKind == JsonValueKind.Object &&
            sourceRecord.TryGetProperty("type", out var type))
        {
            if (type.GetString() != "native_shuffle_operation")
                throw new ArgumentException(
                    "Only native source shuffle_operation records can be replayed.");
            data = Field(sourceRecord, "data");
        }

        if (String(data, "source_version") != "v0.111.0" ||
            String(data, "kind") != "recycle" ||
            String(data, "card_identity_scope") != "shuffle_operation_local" ||
            String(data, "hook_order_stage") != "modify_shuffle_order_completed")
            throw new ArgumentException(
                "Native source shuffle is not an audited in-combat recycle.");
        if (Field(data, "operation_id").GetInt64() <= 0)
            throw new ArgumentException("Invalid native shuffle operation ID.");

        var nativeTokens = Field(data, "ordered_input")
            .EnumerateArray().Select(x => x.GetString()!).ToArray();
        if (nativeTokens.Distinct(StringComparer.Ordinal).Count() !=
            nativeTokens.Length ||
            nativeTokens.Any(string.IsNullOrWhiteSpace) ||
            independentlyMappedCardTokens.Count != nativeTokens.Length ||
            independentlyMappedCardTokens.Keys.Except(nativeTokens).Any())
            throw new ArgumentException(
                "Native card tokens require a complete one-to-one mapping.");

        var ids = MapTokens(Field(data, "ordered_input"), independentlyMappedCardTokens);
        var pre = MapTokens(Field(data, "pre_hook_order"), independentlyMappedCardTokens);
        var post = MapTokens(Field(data, "post_hook_order"), independentlyMappedCardTokens);
        if (ids.Distinct().Count() != ids.Length)
            throw new ArgumentException(
                "Multiple native card objects mapped to the same emulator instance.");
        var eligible = emulatorBefore.DiscardPile
            .Concat(emulatorBefore.DrawPile).Order().ToArray();
        if (!ids.Order().SequenceEqual(eligible))
            throw new ArgumentException(
                "Native card tokens do not cover exactly the emulator source card zones.");

        var originalModels = Field(data, "card_models");
        if (originalModels.ValueKind != JsonValueKind.Array ||
            originalModels.GetArrayLength() != ids.Length)
            throw new ArgumentException("Native source models/instances mismatch.");
        var emulatorCards = emulatorBefore.Cards
            .ToDictionary(card => card.InstanceId);
        int index = 0;
        foreach (var nativeModel in originalModels.EnumerateArray())
        {
            if (!emulatorCards.TryGetValue(ids[index], out var card))
                throw new ArgumentException("Native source maps to a nonexistent emulator card.");
            string model = nativeModel.GetString() ?? "";
            string? mapped = ResolveNativeCardModel(model);
            if (mapped is null || mapped != card.CardId)
                throw new ArgumentException(
                    "Native source card model or upgrade mapping is unverified: " + model);
            ++index;
        }

        var choices = Field(data, "choices");
        if (choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() != Math.Max(0, ids.Length - 1))
            throw new ArgumentException("Native Fisher-Yates source choices incomplete.");
        var indices = new List<int>();
        NativeRngSnapshot? previous = null;
        foreach (var choice in choices.EnumerateArray())
        {
            int bound = Field(choice, "bounded_count").GetInt32();
            int selected = Field(choice, "selected_index").GetInt32();
            if (bound != ids.Length - indices.Count ||
                selected < 0 || selected >= bound)
                throw new ArgumentException("Native shuffle bounded-index mismatch.");
            var before = Snapshot(Field(choice, "cursor_before"));
            var after = Snapshot(Field(choice, "cursor_after"));
            if (previous is not null && previous != before)
                throw new ArgumentException("Native shuffle RNG cursor discontinuity.");
            // Verify exact source RNG primitive, not only an integer result
            // and a monotonically increasing cursor. This uses a disposable
            // one-stream native codec and does not modify the engine state.
            var rng = new RngBundle([
                NativeRngV111.FromSnapshot("run.shuffle", before)]);
            int expected = NativeRngV111.NextInt(rng, "run.shuffle", bound);
            if (expected != selected ||
                NativeRngV111.ToSnapshot(
                    rng.GetStream("run.shuffle")) != after)
                throw new ArgumentException(
                    "Native source RNG output/xoshiro transition mismatch.");
            indices.Add(selected);
            previous = after;
        }
        var result = new NativeShuffleStepExpectationV111(
            "recycle", ids, pre, post, indices.ToArray());
        result.Validate();
        return result;
    }
}
