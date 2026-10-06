using System.Text.Json;
using System.Text.RegularExpressions;
using Sts2Emulator.Core;

namespace Sts2Emulator.Trace;

public sealed record ReferenceCorpusEntity(
    string Kind,
    string Id,
    string? Name,
    JsonElement Data);

public sealed record ReferenceCorpusTableSummary(
    string Kind,
    int Count,
    string[] Fields);

public sealed record ReferenceCorpusSummary(
    string RootDirectory,
    IReadOnlyList<ReferenceCorpusTableSummary> Tables);

public sealed class ReferenceCorpus
{
    private readonly string _rootDirectory;

    private ReferenceCorpus(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    public string RootDirectory => _rootDirectory;

    public static ReferenceCorpus Open(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException(
                "Reference corpus directory must be non-empty.",
                nameof(rootDirectory));
        }

        var fullPath = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"Reference corpus directory does not exist: {fullPath}");
        }

        return new ReferenceCorpus(fullPath);
    }

    public string[] AvailableKinds() =>
        Directory.EnumerateFiles(_rootDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

    public ReferenceCorpusEntity[] ReadKind(string kind)
    {
        var normalizedKind = NormalizeKind(kind);
        var path = Path.Combine(_rootDirectory, $"{normalizedKind}.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Reference corpus kind '{normalizedKind}' is unavailable at {path}.",
                path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"Reference corpus file must contain a JSON array: {path}");
        }

        var entities = new List<ReferenceCorpusEntity>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = TryGetString(item, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            entities.Add(new ReferenceCorpusEntity(
                normalizedKind,
                id,
                TryGetString(item, "name"),
                item.Clone()));
        }

        return entities.ToArray();
    }

    public ReferenceCorpusEntity Resolve(
        string kind,
        string idOrName)
    {
        var candidates = Find(kind, idOrName);
        if (candidates.Length == 1)
        {
            return candidates[0];
        }

        if (candidates.Length == 0)
        {
            throw new KeyNotFoundException(
                $"No {NormalizeKind(kind)} entry matched '{idOrName}'.");
        }

        throw new InvalidOperationException(
            $"Reference query '{idOrName}' matched multiple {NormalizeKind(kind)} entries: " +
            string.Join(", ", candidates.Select(candidate => candidate.Id)));
    }

    public ReferenceCorpusEntity[] Find(
        string kind,
        string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException(
                "Reference corpus query must be non-empty.",
                nameof(query));
        }

        var entities = ReadKind(kind);
        var normalizedQuery = NormalizeIdentifier(query);

        var exact = entities
            .Where(entity =>
                StringComparer.Ordinal.Equals(
                    NormalizeIdentifier(entity.Id),
                    normalizedQuery)
                || (entity.Name is not null
                    && StringComparer.Ordinal.Equals(
                        NormalizeIdentifier(entity.Name),
                        normalizedQuery)))
            .ToArray();
        if (exact.Length > 0)
        {
            return exact;
        }

        return entities
            .Where(entity =>
                entity.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (entity.Name?.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase) == true))
            .OrderBy(entity => entity.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public ReferenceCorpusSummary Summarize()
    {
        var tables = new List<ReferenceCorpusTableSummary>();
        foreach (var kind in AvailableKinds())
        {
            var entities = ReadKind(kind);
            var fields = entities
                .SelectMany(entity => entity.Data.EnumerateObject())
                .Select(property => property.Name)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            tables.Add(new ReferenceCorpusTableSummary(
                kind,
                entities.Length,
                fields));
        }

        return new ReferenceCorpusSummary(
            _rootDirectory,
            tables);
    }

    public static string NormalizeIdentifier(string value)
    {
        var buffer = new char[value.Length];
        var length = 0;

        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character))
            {
                continue;
            }

            buffer[length++] = char.ToUpperInvariant(character);
        }

        return new string(buffer, 0, length);
    }

    private static string NormalizeKind(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException(
                "Reference corpus kind must be non-empty.",
                nameof(kind));
        }

        return Path.GetFileNameWithoutExtension(kind.Trim());
    }

    private static string? TryGetString(
        JsonElement element,
        string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }
}

public sealed record ReferenceMechanicCoverage(
    string Feature,
    int NativeSilentCards,
    bool PrototypeEngineHasPrimitive);

public sealed record ReferenceNativeCardGap(
    string NativeId,
    string NativeName,
    string? PrototypeId,
    string[] RequiredFeatures,
    string[] UnsupportedFeatures,
    string[] StructuredMismatches,
    string[] SourceWarnings);

public sealed record ReferenceMechanicsGapReport(
    int NativeSilentCardCount,
    int PrototypeCardCount,
    int NameMatchedCardCount,
    int StructuredFieldMatchCount,
    int MissingNativeCardCount,
    int CardsWithUnsupportedFeatures,
    string[] PrototypeCardsOutsideSilentPool,
    string[] PrototypeCardsAbsentFromReference,
    string[] StartingRunMismatches,
    IReadOnlyList<ReferenceMechanicCoverage> FeatureCoverage,
    IReadOnlyList<ReferenceNativeCardGap> Cards);

public static partial class ReferenceMechanicsGapAnalyzer
{
    private static readonly HashSet<string> PrototypeCapabilities =
        new(StringComparer.Ordinal)
        {
            "fixed_energy_cost",
            "x_energy_cost",
            "target_self",
            "target_enemy",
            "target_all_enemies",
            "random_target",
            "damage",
            "block",
            "draw",
            "gain_energy",
            "apply_power",
            "multi_hit",
            "exhaust_on_play",
            "choose_hand_discard",
            "choose_hand_exhaust",
            "choose_draw_exhaust",
            "choose_discard_exhaust",
            "choose_exhaust_exhaust",
            "generate_cards_in_hand",
            "innate",
            "retain",
            "sly",
            "ethereal",
            "unplayable",
            "eternal",
            "upgrade_scalar"
        };

    public static ReferenceMechanicsGapReport Analyze(
        ReferenceCorpus corpus)
    {
        var allReferenceCards = corpus.ReadKind("cards");
        var nativeCards = allReferenceCards
            .Where(card =>
                StringComparer.OrdinalIgnoreCase.Equals(
                    GetString(card.Data, "color"),
                    "silent"))
            .OrderBy(card => card.Id, StringComparer.Ordinal)
            .ToArray();

        var allReferenceByName = allReferenceCards
            .GroupBy(
                card => ReferenceCorpus.NormalizeIdentifier(card.Name ?? card.Id),
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.Ordinal);

        var prototypeByName = PrototypeContent.Cards.Values
            .GroupBy(
                card => ReferenceCorpus.NormalizeIdentifier(card.Name),
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);

        var cardGaps = new List<ReferenceNativeCardGap>();
        var featureCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var native in nativeCards)
        {
            var required = InferFeatures(native.Data);
            foreach (var feature in required)
            {
                featureCounts[feature] = featureCounts.GetValueOrDefault(feature) + 1;
            }

            prototypeByName.TryGetValue(
                ReferenceCorpus.NormalizeIdentifier(native.Name ?? native.Id),
                out var prototype);

            var unsupported = required
                .Where(feature => !PrototypeCapabilities.Contains(feature))
                .Order(StringComparer.Ordinal)
                .ToArray();

            var mismatches = prototype is null
                ? Array.Empty<string>()
                : CompareStructuredFields(native.Data, prototype);
            var sourceWarnings = DetectSourceWarnings(native.Data);

            cardGaps.Add(new ReferenceNativeCardGap(
                native.Id,
                native.Name ?? native.Id,
                prototype?.Id,
                required,
                unsupported,
                mismatches,
                sourceWarnings));
        }

        var startingRunMismatches = CompareStartingRun(corpus);

        var coverage = featureCounts
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new ReferenceMechanicCoverage(
                item.Key,
                item.Value,
                PrototypeCapabilities.Contains(item.Key)))
            .ToArray();

        var outsideSilentPool = PrototypeContent.Cards.Values
            .Where(prototype =>
                allReferenceByName.TryGetValue(
                    ReferenceCorpus.NormalizeIdentifier(prototype.Name),
                    out var references)
                && references.All(reference =>
                    !StringComparer.OrdinalIgnoreCase.Equals(
                        GetString(reference.Data, "color"),
                        "silent")))
            .Select(prototype => prototype.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var absentFromReference = PrototypeContent.Cards.Values
            .Where(prototype =>
                !allReferenceByName.ContainsKey(
                    ReferenceCorpus.NormalizeIdentifier(prototype.Name)))
            .Select(prototype => prototype.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new ReferenceMechanicsGapReport(
            nativeCards.Length,
            PrototypeContent.Cards.Count,
            cardGaps.Count(card => card.PrototypeId is not null),
            cardGaps.Count(card =>
                card.PrototypeId is not null
                && card.StructuredMismatches.Length == 0),
            cardGaps.Count(card => card.PrototypeId is null),
            cardGaps.Count(card => card.UnsupportedFeatures.Length > 0),
            outsideSilentPool,
            absentFromReference,
            startingRunMismatches,
            coverage,
            cardGaps);
    }

    private static string[] InferFeatures(JsonElement card)
    {
        var features = new HashSet<string>(StringComparer.Ordinal);

        if (GetBool(card, "is_x_cost") == true)
        {
            features.Add("x_energy_cost");
        }
        else
        {
            features.Add("fixed_energy_cost");
        }

        if (GetBool(card, "is_x_star_cost") == true)
        {
            features.Add("x_star_cost");
        }

        if (HasNonNull(card, "star_cost"))
        {
            features.Add("star_cost");
        }

        switch (GetString(card, "target"))
        {
            case "Self":
            case "None":
                features.Add("target_self");
                break;
            case "AnyEnemy":
                features.Add("target_enemy");
                break;
            case "AllEnemies":
                features.Add("target_all_enemies");
                break;
            case "RandomEnemy":
                features.Add("random_target");
                break;
            case "AnyAlly":
            case "AllAllies":
                features.Add("ally_target");
                break;
        }

        if (HasNumber(card, "damage"))
        {
            features.Add("damage");
        }

        if (HasNumber(card, "block"))
        {
            features.Add("block");
        }

        var description = GetString(card, "description") ?? string.Empty;

        if (HasNumber(card, "cards_draw")
            && DrawRegex().IsMatch(description))
        {
            features.Add("draw");
        }

        if (HasNumber(card, "energy_gain"))
        {
            features.Add("gain_energy");
        }

        if (HasNumber(card, "hp_loss"))
        {
            features.Add("direct_hp_loss");
        }

        if (GetInt(card, "hit_count") is > 1)
        {
            features.Add("multi_hit");
        }

        if (HasArrayItems(card, "powers_applied"))
        {
            features.Add("apply_power");
        }

        if (HasArrayItems(card, "spawns_cards")
            || GenerateIntoHandRegex().IsMatch(description))
        {
            features.Add("generate_cards_in_hand");
        }

        if (card.TryGetProperty("upgrade", out var upgrade)
            && upgrade.ValueKind == JsonValueKind.Object
            && upgrade.EnumerateObject().Any())
        {
            features.Add("upgrade_scalar");
        }

        if (card.TryGetProperty("keywords", out var keywords)
            && keywords.ValueKind == JsonValueKind.Array)
        {
            foreach (var keywordElement in keywords.EnumerateArray())
            {
                if (keywordElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                switch (keywordElement.GetString())
                {
                    case "Exhaust":
                        features.Add("exhaust_on_play");
                        break;
                    case "Retain":
                        features.Add("retain");
                        break;
                    case "Ethereal":
                        features.Add("ethereal");
                        break;
                    case "Innate":
                        features.Add("innate");
                        break;
                    case "Sly":
                        features.Add("sly");
                        break;
                    case "Unplayable":
                        features.Add("unplayable");
                        break;
                    case "Eternal":
                        features.Add("eternal");
                        break;
                }
            }
        }

        if (ChooseGeneratedRegex().IsMatch(description))
        {
            features.Add("choose_generated");
        }

        if (DiscardChoiceRegex().IsMatch(description))
        {
            features.Add("choose_hand_discard");
        }

        if (ExhaustChoiceRegex().IsMatch(description))
        {
            features.Add("choose_hand_exhaust");
        }

        if (DrawPileExhaustRegex().IsMatch(description))
        {
            features.Add("choose_draw_exhaust");
        }

        if (DiscardToHandRegex().IsMatch(description))
        {
            features.Add("move_discard_to_hand");
        }

        if (DrawToHandRegex().IsMatch(description))
        {
            features.Add("move_draw_to_hand");
        }

        if (TopDeckRegex().IsMatch(description))
        {
            features.Add("topdeck_selected_card");
        }

        if (TransformRegex().IsMatch(description))
        {
            features.Add("transform_card");
        }

        if (CopyRegex().IsMatch(description))
        {
            features.Add("copy_card");
        }

        if (RandomRegex().IsMatch(description)
            && !StringComparer.Ordinal.Equals(
                GetString(card, "target"),
                "RandomEnemy"))
        {
            features.Add("random_effect");
        }

        if (ReplayRegex().IsMatch(description))
        {
            features.Add("replay");
        }

        if (AutoPlayRegex().IsMatch(description))
        {
            features.Add("automatic_play");
        }

        if (ConditionalRegex().IsMatch(description))
        {
            features.Add("conditional_effect");
        }

        if (CostMutationRegex().IsMatch(description))
        {
            features.Add("cost_mutation");
        }

        if (PermanentRegex().IsMatch(description))
        {
            features.Add("permanent_card_or_deck_mutation");
        }

        return features.Order(StringComparer.Ordinal).ToArray();
    }

    private static string[] CompareStructuredFields(
        JsonElement native,
        PrototypeCardDefinition prototype)
    {
        var mismatches = new List<string>();

        if (GetBool(native, "is_x_cost") == true)
        {
            if (prototype.Cost.Kind != PrototypeCardCostKind.X)
            {
                mismatches.Add(
                    $"cost: native=X, prototype={prototype.Cost.Kind}");
            }
        }
        else if (GetInt(native, "cost") is int nativeCost
            && (prototype.Cost.Kind != PrototypeCardCostKind.Fixed
                || prototype.Cost.Amount != nativeCost))
        {
            mismatches.Add(
                $"cost: native={nativeCost}, prototype={prototype.Cost.Kind}:{prototype.Cost.Amount}");
        }

        var expectedTarget = GetString(native, "target") switch
        {
            "AnyEnemy" => PrototypeCardTarget.Enemy,
            "Self" or "None" or "AllEnemies" or "RandomEnemy" =>
                PrototypeCardTarget.None,
            _ => (PrototypeCardTarget?)null
        };

        if (expectedTarget is not null
            && prototype.Target != expectedTarget.Value)
        {
            mismatches.Add(
                $"target: native={GetString(native, "target")}, prototype={prototype.Target}");
        }

        CompareEffectAmount(
            mismatches,
            native,
            "damage",
            prototype,
            PrototypeCombatEffectKind.DamageEnemy);
        CompareEffectAmount(
            mismatches,
            native,
            "block",
            prototype,
            PrototypeCombatEffectKind.GainPlayerBlock);
        var description = GetString(native, "description") ?? string.Empty;
        if (DrawRegex().IsMatch(description))
        {
            CompareEffectAmount(
                mismatches,
                native,
                "cards_draw",
                prototype,
                PrototypeCombatEffectKind.DrawCards);
        }
        CompareEffectAmount(
            mismatches,
            native,
            "energy_gain",
            prototype,
            PrototypeCombatEffectKind.GainEnergy);

        var nativeHitCount = GetInt(native, "hit_count");
        if (nativeHitCount is > 1)
        {
            var damage = prototype.Effects.FirstOrDefault(
                effect => effect.Kind == PrototypeCombatEffectKind.DamageEnemy);
            if (damage is null || damage.Repetitions != nativeHitCount)
            {
                mismatches.Add(
                    $"hit_count: native={nativeHitCount}, prototype={damage?.Repetitions.ToString() ?? "missing"}");
            }
        }

        var nativeExhaust = ArrayContains(native, "keywords", "Exhaust");
        if (nativeExhaust != prototype.ExhaustOnUse)
        {
            mismatches.Add(
                $"exhaust: native={nativeExhaust}, prototype={prototype.ExhaustOnUse}");
        }

        return mismatches.ToArray();
    }

    private static string[] CompareStartingRun(ReferenceCorpus corpus)
    {
        var mismatches = new List<string>();
        ReferenceCorpusEntity silent;

        try
        {
            silent = corpus.Resolve("characters", "SILENT");
        }
        catch (FileNotFoundException)
        {
            return ["characters.json unavailable; starting-run parity not evaluated"];
        }
        catch (KeyNotFoundException)
        {
            return ["SILENT character unavailable; starting-run parity not evaluated"];
        }

        CompareScalar(
            mismatches,
            "starting_hp",
            GetInt(silent.Data, "starting_hp"),
            PrototypeContent.Rules.StartingHp);
        CompareScalar(
            mismatches,
            "starting_gold",
            GetInt(silent.Data, "starting_gold"),
            PrototypeContent.Rules.StartingGold);
        CompareScalar(
            mismatches,
            "max_energy",
            GetInt(silent.Data, "max_energy"),
            PrototypeContent.Rules.BaseEnergy);

        var nativeDeck = ReadStringArray(silent.Data, "starting_deck")
            .Select(token => ResolveReferenceName(corpus, "cards", token))
            .Select(ReferenceCorpus.NormalizeIdentifier)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var prototypeDeck = PrototypeContent.StartingDeck
            .Select(PrototypeContent.Card)
            .Select(card => ReferenceCorpus.NormalizeIdentifier(card.Name))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (!nativeDeck.SequenceEqual(prototypeDeck, StringComparer.Ordinal))
        {
            mismatches.Add(
                $"starting_deck: native=[{string.Join(",", nativeDeck)}], " +
                $"prototype=[{string.Join(",", prototypeDeck)}]");
        }

        var nativeRelics = ReadStringArray(silent.Data, "starting_relics")
            .Select(token => ResolveReferenceName(corpus, "relics", token))
            .Select(ReferenceCorpus.NormalizeIdentifier)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var prototypeRelics = PrototypeContent.StartingRelics
            .Select(PrototypeContent.Relic)
            .Select(relic => ReferenceCorpus.NormalizeIdentifier(relic.Name))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (!nativeRelics.SequenceEqual(prototypeRelics, StringComparer.Ordinal))
        {
            mismatches.Add(
                $"starting_relics: native=[{string.Join(",", nativeRelics)}], " +
                $"prototype=[{string.Join(",", prototypeRelics)}]");
        }

        return mismatches.ToArray();
    }

    private static string[] DetectSourceWarnings(JsonElement card)
    {
        var warnings = new List<string>();
        var description = GetString(card, "description") ?? string.Empty;

        if (HasNumber(card, "cards_draw")
            && !DrawRegex().IsMatch(description))
        {
            warnings.Add(
                "derived cards_draw is populated but localized text contains no draw instruction");
        }

        if (!HasArrayItems(card, "spawns_cards")
            && GenerateIntoHandRegex().IsMatch(description))
        {
            warnings.Add(
                "localized text describes card generation but derived spawns_cards is empty");
        }

        return warnings.ToArray();
    }

    private static string ResolveReferenceName(
        ReferenceCorpus corpus,
        string kind,
        string token)
    {
        try
        {
            var resolved = corpus.Resolve(kind, token);
            return resolved.Name ?? resolved.Id;
        }
        catch (FileNotFoundException)
        {
            return token;
        }
        catch (KeyNotFoundException)
        {
            return token;
        }
    }

    private static void CompareEffectAmount(
        List<string> mismatches,
        JsonElement native,
        string nativeField,
        PrototypeCardDefinition prototype,
        PrototypeCombatEffectKind kind)
    {
        if (GetInt(native, nativeField) is not int nativeValue)
        {
            return;
        }

        var effect = prototype.Effects.FirstOrDefault(
            candidate => candidate.Kind == kind);
        if (effect is null || effect.Amount != nativeValue)
        {
            mismatches.Add(
                $"{nativeField}: native={nativeValue}, prototype={effect?.Amount.ToString() ?? "missing"}");
        }
    }

    private static void CompareScalar(
        List<string> mismatches,
        string name,
        int? native,
        int prototype)
    {
        if (native is not null && native.Value != prototype)
        {
            mismatches.Add(
                $"{name}: native={native.Value}, prototype={prototype}");
        }
    }

    private static bool HasNonNull(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind is not JsonValueKind.Null
        and not JsonValueKind.Undefined;

    private static bool HasNumber(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.Number;

    private static bool HasArrayItems(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.Array
        && property.GetArrayLength() > 0;

    private static int? GetInt(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.Number
        && property.TryGetInt32(out var value)
            ? value
            : null;

    private static bool? GetBool(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static string? GetString(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool ArrayContains(
        JsonElement element,
        string propertyName,
        string expected)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return property.EnumerateArray().Any(item =>
            item.ValueKind == JsonValueKind.String
            && StringComparer.Ordinal.Equals(item.GetString(), expected));
    }

    private static string[] ReadStringArray(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();
    }

    [GeneratedRegex(@"\bdraw\b", RegexOptions.IgnoreCase)]
    private static partial Regex DrawRegex();

    [GeneratedRegex(
        @"\badd\b.*\binto (?:your|the) (?:\[gold\])?hand\b",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex GenerateIntoHandRegex();

    [GeneratedRegex(
        @"\bchoose\s+(?:up to\s+)?\d+\s+of\s+\d+|\bchoose\s+one\s+of\s+",
        RegexOptions.IgnoreCase)]
    private static partial Regex ChooseGeneratedRegex();

    [GeneratedRegex(
        @"\bdiscard\s+(?:up to\s+)?\d+\s+card|\bdiscard\s+a\s+card",
        RegexOptions.IgnoreCase)]
    private static partial Regex DiscardChoiceRegex();

    [GeneratedRegex(
        @"\bexhaust\s+(?:up to\s+)?\d+\s+card|\bexhaust\s+a\s+card",
        RegexOptions.IgnoreCase)]
    private static partial Regex ExhaustChoiceRegex();

    [GeneratedRegex(
        @"\bexhaust\b.*\bdraw pile\b",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DrawPileExhaustRegex();

    [GeneratedRegex(
        @"\bput\b.*\bfrom your discard pile\b.*\binto your hand\b",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DiscardToHandRegex();

    [GeneratedRegex(
        @"\bput\b.*\bfrom your draw pile\b.*\binto your hand\b",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DrawToHandRegex();

    [GeneratedRegex(
        @"\btop of your draw pile\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex TopDeckRegex();

    [GeneratedRegex(@"\btransform\b", RegexOptions.IgnoreCase)]
    private static partial Regex TransformRegex();

    [GeneratedRegex(
        @"\bcopy\b|\bcopies\b|\bduplicat",
        RegexOptions.IgnoreCase)]
    private static partial Regex CopyRegex();

    [GeneratedRegex(@"\brandom\b", RegexOptions.IgnoreCase)]
    private static partial Regex RandomRegex();

    [GeneratedRegex(@"\breplay\b", RegexOptions.IgnoreCase)]
    private static partial Regex ReplayRegex();

    [GeneratedRegex(
        @"\bplay(?:ed)? automatically\b|\bplay it for free\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex AutoPlayRegex();

    [GeneratedRegex(
        @"(^|[.\n]\s*)if\b|\bwhenever\b|\bfor each\b|\bevery time\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ConditionalRegex();

    [GeneratedRegex(
        @"\bcost\b.*\b(?:less|more|free|0|1|2|3|random)\b|\breduce\b.*\bcost\b|\bincrease\b.*\bcost\b",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CostMutationRegex();

    [GeneratedRegex(
        @"\bpermanently\b|\bfrom your deck\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex PermanentRegex();
}
