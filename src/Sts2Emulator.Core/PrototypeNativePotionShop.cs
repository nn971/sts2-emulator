namespace Sts2Emulator.Core;

/// <summary>Native potion rarity tiers.</summary>
public enum PrototypeNativePotionRarity
{
    Common,
    Uncommon,
    Rare
}

public sealed record NativePotionModelSelectionAuditV111(
    string[] PotionIds,
    RandomIndexDecisionV111[] Choices,
    RandomUnitFloatDecisionV111[] RarityRolls,
    string IndexProviderMode,
    string FloatProviderMode)
{
    private string[]? _potionIds = Copy(PotionIds);
    private RandomIndexDecisionV111[]? _choices = CloneDecisions(Choices);
    private RandomUnitFloatDecisionV111[]? _rarityRolls = Copy(RarityRolls);

    public string[] PotionIds
    {
        get => Copy(_potionIds)!;
        init => _potionIds = Copy(value);
    }

    public RandomIndexDecisionV111[] Choices
    {
        get => CloneDecisions(_choices)!;
        init => _choices = CloneDecisions(value);
    }

    public RandomUnitFloatDecisionV111[] RarityRolls
    {
        get => Copy(_rarityRolls)!;
        init => _rarityRolls = Copy(value);
    }

    public NativePotionModelSelectionAuditV111 Fork() => this with
    {
        PotionIds = _potionIds!,
        Choices = _choices!,
        RarityRolls = _rarityRolls!
    };

    private static T[]? Copy<T>(T[]? values) => values is null
        ? null
        : (T[])values.Clone();

    private static RandomIndexDecisionV111[]? CloneDecisions(
        RandomIndexDecisionV111[]? values) => values?.Select(decision =>
            decision is null ? null! :
                PrototypeControlledEventChoiceContinuationV111
                    .CloneDecision(decision)).ToArray();
}

/// <summary>
/// Pinned v0.111.0 rarity and merchant price rules for the supported
/// full-unlock Silent/shared potion pool. Out-of-combat native generation
/// preserves the source pool order and draw sequence.
/// </summary>
public static class PrototypeNativePotionShop
{
    public const string UniformModelSiteId =
        "potion.uniform_model.index_draw";
    public const string RarityRollSiteId = "potion.rarity_roll.ticket";
    public const string ModelWithinRaritySiteId =
        "potion.model_within_rarity.index_draw";

    // PotionFactory concatenates the current character's unlocked pool before
    // the shared pool. Keep that ordering for the typed model-index request:
    // a different ordering changes the selected potion for the same draw.
    private static readonly string[] NativeSilentPotionPoolOrder =
    [
        "proto.potion.poison",
        "proto.potion.ghost_in_a_jar",
        "proto.potion.cunning",
        "proto.potion.attack",
        "proto.potion.beetle_juice",
        "proto.potion.blessing_of_the_forge",
        "proto.potion.block",
        "proto.potion.bottled_potential",
        "proto.potion.clarity",
        "proto.potion.colorless",
        "proto.potion.cure_all",
        "proto.potion.dexterity",
        "proto.potion.distilled_chaos",
        "proto.potion.droplet_of_precognition",
        "proto.potion.duplicator",
        "proto.potion.energy",
        "proto.potion.entropic_brew",
        "proto.potion.explosive_ampoule",
        "proto.potion.fairy_in_a_bottle",
        "proto.potion.fire",
        "proto.potion.flex",
        "proto.potion.fortifier",
        "proto.potion.fruit_juice",
        "proto.potion.fysh_oil",
        "proto.potion.gamblers_brew",
        "proto.potion.gigantification",
        "proto.potion.heart_of_iron",
        "proto.potion.liquid_bronze",
        "proto.potion.liquid_memories",
        "proto.potion.lucky_tonic",
        "proto.potion.mazaleths_gift",
        "proto.potion.orobic_acid",
        "proto.potion.binding",
        "proto.potion.powdered_demise",
        "proto.potion.power",
        "proto.potion.radiant_tincture",
        "proto.potion.regen",
        "proto.potion.shackling",
        "proto.potion.ship_in_a_bottle",
        "proto.potion.skill",
        "proto.potion.snecko_oil",
        "proto.potion.speed",
        "proto.potion.stable_serum",
        "proto.potion.strength",
        "proto.potion.swift",
        "proto.potion.touch_of_insanity",
        "proto.potion.vulnerable",
        "proto.potion.weak"
    ];
    private static readonly IReadOnlyList<string>
        NativeSilentPotionPoolOrderView =
            Array.AsReadOnly(NativeSilentPotionPoolOrder);

    /// <summary>
    /// Silent's full-unlock character potion pool followed by the shared pool,
    /// in the order used by the pinned source's PotionFactory.
    /// </summary>
    public static IReadOnlyList<string> NativePotionPoolOrder =>
        NativeSilentPotionPoolOrderView;

    private static readonly IReadOnlyDictionary<string, PrototypeNativePotionRarity>
        Rarities = new Dictionary<string, PrototypeNativePotionRarity>(
            StringComparer.Ordinal)
        {
            ["proto.potion.block"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.fire"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.poison"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.swift"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.weak"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.dexterity"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.energy"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.strength"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.flex"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.explosive_ampoule"] =
                PrototypeNativePotionRarity.Common,
            ["proto.potion.attack"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.skill"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.power"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.speed"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.vulnerable"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.blood"] = PrototypeNativePotionRarity.Common,

            ["proto.potion.regen"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.cunning"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.fysh_oil"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.binding"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.clarity"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.colorless"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.powdered_demise"] =
                PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.lucky_tonic"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.orobic_acid"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.heart_of_iron"] =
                PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.radiant_tincture"] =
                PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.duplicator"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.blessing_of_the_forge"] =
                PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.liquid_bronze"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.cure_all"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.fortifier"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.stable_serum"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.touch_of_insanity"] =
                PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.gamblers_brew"] = PrototypeNativePotionRarity.Uncommon,

            ["proto.potion.liquid_memories"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.ghost_in_a_jar"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.fruit_juice"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.entropic_brew"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.snecko_oil"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.fairy_in_a_bottle"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.distilled_chaos"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.ship_in_a_bottle"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.droplet_of_precognition"] =
                PrototypeNativePotionRarity.Rare,
            ["proto.potion.bottled_potential"] =
                PrototypeNativePotionRarity.Rare,
            ["proto.potion.shackling"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.mazaleths_gift"] =
                PrototypeNativePotionRarity.Rare,
            ["proto.potion.beetle_juice"] =
                PrototypeNativePotionRarity.Rare,
            ["proto.potion.gigantification"] =
                PrototypeNativePotionRarity.Rare
        };

    public static bool TryGetRarity(
        string potionId, out PrototypeNativePotionRarity rarity) =>
        Rarities.TryGetValue(potionId, out rarity);

    /// <summary>Source-ordered potion models for one merchant rarity tier.</summary>
    public static string[] CandidatesForRarity(
        PrototypeNativePotionRarity rarity,
        IReadOnlyCollection<string>? excluded = null) =>
        NativePotionPoolOrder.Where(id => Rarities[id] == rarity &&
            (excluded is null || !excluded.Contains(id))).ToArray();

    /// <summary>
    /// PotionFactory.CreateRandomPotions first rolls rarity (10% Rare,
    /// 25% Uncommon, 65% Common), then chooses an item of that rarity.
    /// Integer [0,100) rolls preserve these probabilities for prototype-owned
    /// generation. Native source-owned generation uses NextFloat below.
    /// </summary>
    public static PrototypeNativePotionRarity RollRarity(
        RngBundle rng, string streamId,
        string ownerId = "potion:weighted_offer")
    {
        var roll = SemanticIndexOutcomeV111.SelectByBoundedOffset(
            LegacyRandomChoiceV111.ChooseBoundedInteger(
                rng, RarityRollSiteId, streamId, ownerId, 100), 0, 100);
        return roll < 10
            ? PrototypeNativePotionRarity.Rare
            : roll < 35
                ? PrototypeNativePotionRarity.Uncommon
                : PrototypeNativePotionRarity.Common;
    }

    public static string PickWeighted(
        RngBundle rng, string streamId,
        IReadOnlyCollection<string>? excluded = null,
        string ownerId = "potion:weighted_offer")
    {
        var rarity = RollRarity(rng, streamId, ownerId + ":rarity");
        var candidates = CandidatesForRarity(rarity, excluded);
        if (candidates.Length == 0)
        {
            // Native PotionFactory does not silently substitute a
            // different rarity if the selected tier is exhausted.
            throw new InvalidOperationException(
                $"No unexcluded {rarity} potions remain.");
        }

        var decision = LegacyRandomChoiceV111.ChooseIndexRange(
            rng, ModelWithinRaritySiteId, streamId,
            ownerId + ":rarity:" + rarity + ":pool:"
                + CanonicalJson.Sha256(candidates), candidates.Length);
        return SemanticIndexOutcomeV111.SelectByChooseIndex(
            decision, candidates);
    }

    /// <summary>
    /// Native merchant inventories call CreateRandomPotions with a
    /// mutable candidate list, removing each selected model. Retain source
    /// order because each NextItem indexes the remaining rarity subset.
    /// </summary>
    public static string[] PickWeightedDistinct(
        RngBundle rng, string streamId, int count,
        string ownerId = "potion:weighted_distinct")
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var selected = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            selected.Add(PickWeighted(
                rng, streamId, selected,
                ownerId + ":pick:" + i));
        }

        return selected.ToArray();
    }

    /// <summary>
    /// Source-ordered out-of-combat PotionFactory generation. Each item asks
    /// a float provider for rarity, then an index provider for a model in the
    /// remaining same-rarity pool, and removes that model before the next item.
    /// </summary>
    public static string[] PickWeightedDistinctNative(
        RngBundle rng, string streamId, int count)
        => PickWeightedDistinctNativeWithAudit(
            rng, streamId, count, "potion_factory",
            new NativeRandomIndexProviderV111()).PotionIds;

    public static NativePotionModelSelectionAuditV111
        PickWeightedDistinctNativeWithAudit(
            RngBundle rng,
            string streamId,
            int count,
            string ownerId,
            IRandomIndexProviderV111 provider,
            IRandomUnitFloatProviderV111? floatProvider = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var effectiveIndexProvider =
            StochasticProviderScopeV111.SelectIndexProvider(provider);
        var effectiveFloatProvider =
            StochasticProviderScopeV111.SelectUnitFloatProvider(floatProvider);
        var workingRng = rng.Fork();
        var remaining = NativePotionPoolOrder.ToList();
        var selected = new List<string>(count);
        var choices = new List<RandomIndexDecisionV111>(count);
        var rarityRolls = new List<RandomUnitFloatDecisionV111>(count);
        for (var index = 0; index < count; index++)
        {
            var rarityDecision = StochasticProviderScopeV111.ResolveUnitFloat(
                effectiveFloatProvider,
                new RandomUnitFloatRequestV111(
                    StochasticSiteRegistryV111
                        .NativePotionFactoryRarityRollSiteId,
                    streamId,
                    ownerId, index), workingRng);
            rarityDecision.Validate();
            rarityRolls.Add(rarityDecision);
            var roll = rarityDecision.SelectedValue;
            var rarity = ResolveRarityNative(roll);
            var candidates = remaining.Where(id => Rarities[id] == rarity)
                .ToArray();
            if (candidates.Length == 0)
                throw new InvalidOperationException(
                    $"No unselected {rarity} potions remain.");
            var decision = StochasticProviderScopeV111.ResolveIndex(
                effectiveIndexProvider,
                new RandomIndexRequestV111(
                StochasticSiteRegistryV111.NativePotionFactoryModelSiteId,
                streamId,
                ownerId, index,
                Enumerable.Range(0, candidates.Length).ToArray()), workingRng);
            decision.Validate();
            choices.Add(decision);
            var potionId = SemanticIndexOutcomeV111.SelectByChooseIndex(
                decision, candidates);
            if (streamId == NativePlayerRngV111.RewardsStreamId &&
                rarityDecision.ProviderMode == "native-v111" &&
                decision.ProviderMode == "native-v111" &&
                RandomBoundaryJournalV111.HasActiveJournal)
                RandomBoundaryJournalV111.RecordPotionFactoryOutcome(
                    SemanticPotionFactoryNativeAuditV111.Create(
                        rarityDecision, decision, candidates, rarity,
                        potionId));
            selected.Add(potionId);
            remaining.Remove(potionId);
        }
        foreach (var initialStream in rng.Streams)
            rng.ReplaceStream(initialStream.StreamId,
                workingRng.GetStream(initialStream.StreamId).Fork());
        return new NativePotionModelSelectionAuditV111(
            selected.ToArray(), choices.ToArray(), rarityRolls.ToArray(),
            effectiveIndexProvider.Mode, effectiveFloatProvider.Mode);
    }

    /// <summary>
    /// Source uniform selection over the full-unlock character/shared potion
    /// pool, with no rarity weighting. Used by Wellspring and event offers.
    /// </summary>
    public static string PickUniform(
        RngBundle rng, string streamId,
        string ownerId = "potion:uniform_offer")
    {
        var decision = LegacyRandomChoiceV111.ChooseIndexRange(
            rng, UniformModelSiteId, streamId,
            ownerId + ":pool:" + CanonicalJson.Sha256(NativePotionPoolOrder),
            NativePotionPoolOrder.Count);
        return SemanticIndexOutcomeV111.SelectByChooseIndex(
            decision, NativePotionPoolOrder);
    }

    /// <summary>
    /// MerchantPotionEntry.GetCost: Common 50, Uncommon 75, Rare 100.
    /// </summary>
    public static int MerchantBaseCost(string potionId)
    {
        if (!TryGetRarity(potionId, out var rarity))
        {
            throw new InvalidOperationException(
                $"No native merchant rarity for potion '{potionId}'.");
        }

        return rarity switch
        {
            PrototypeNativePotionRarity.Common => 50,
            PrototypeNativePotionRarity.Uncommon => 75,
            PrototypeNativePotionRarity.Rare => 100,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// Native merchant potions use Shops.NextFloat(0.95, 1.05)
    /// and Godot Mathf.Round; discrete percent samples approximate
    /// the distribution in the retained legacy overload. Configured runs
    /// use MerchantPriceNative for the native unit-float operation.
    /// </summary>
    public static int MerchantPrice(
        string potionId, RngBundle rng,
        string ownerId = "shop:merchant:potion")
    {
        var priceModifier = SemanticIndexOutcomeV111.SelectByChooseIndex(
            LegacyRandomChoiceV111.ChooseIndexRange(
                rng,
                "shop.merchant.potion_price_modifier.index_draw",
                "shop", ownerId + ":" + potionId, 11),
            Enumerable.Range(95, 11).ToArray());
        return Math.Max(1,
            (int)Math.Round(
                MerchantBaseCost(potionId) * priceModifier / 100.0));
    }

    public static int MerchantPriceNative(
        string potionId, RngBundle nativeRunRng, string ownerId)
    {
        var roll = NativePlayerRngV111.NextUnitFloat(
            nativeRunRng,
            "shop.merchant.potion_price_modifier.unit_float",
            NativePlayerRngV111.ShopsStreamId,
            ownerId + ":" + potionId);
        return ResolveMerchantPriceNative(potionId, roll);
    }

    internal static PrototypeNativePotionRarity ResolveRarityNative(
        float roll)
    {
        if (!float.IsFinite(roll) || roll < 0 || roll > 1)
            throw new ArgumentOutOfRangeException(nameof(roll));
        return roll <= 0.1f
            ? PrototypeNativePotionRarity.Rare
            : roll <= 0.35f
                ? PrototypeNativePotionRarity.Uncommon
                : PrototypeNativePotionRarity.Common;
    }

    internal static int ResolveMerchantPriceNative(
        string potionId, float roll)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(potionId);
        if (!float.IsFinite(roll) || roll < 0 || roll > 1)
            throw new ArgumentOutOfRangeException(nameof(roll));
        var modifier = 0.95f + (1.05f - 0.95f) * roll;
        return Math.Max(1,
            (int)MathF.Round(MerchantBaseCost(potionId) * modifier,
                MidpointRounding.ToEven));
    }
}
