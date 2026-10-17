using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>An exact, integer-weighted outcome in a conditional chance law.</summary>
public sealed record ChanceLawOutcomeV111(string OutcomeId, long Weight);

public sealed record StochasticCapabilityManifestV111(
    string SchemaId,
    string ChanceLawSchemaId,
    string ReferenceNatureProviderId,
    string ReferenceNatureSessionSchemaId,
    string ProbabilityStatus,
    string[] NatureChoiceSiteIds,
    string PlayerObservationSchemaId,
    string[] ProviderModes,
    string[] ReferenceNatureOperations)
{
    /// <summary>Individual Chance Laws carry one of these statuses. The
    /// legacy ProbabilityStatus field remains the conservative default.</summary>
    public string[] ProbabilityStatuses { get; init; } =
        [ChanceLawV111.UnverifiedStatus];
    public string ReferenceNatureRunActionSessionSchemaId { get; init; } =
        ReferenceNatureRunActionSessionV111.CurrentSchemaId;
    public string ReferenceNatureEventChoiceSessionSchemaId { get; init; } =
        ReferenceNatureControlledEventChoiceSessionV111.CurrentSchemaId;
    public string ReferenceNatureAncientOfferSessionSchemaId { get; init; } =
        ReferenceNatureAncientOfferSessionV111.CurrentSchemaId;
    public string ReferenceNatureNeowOfferSessionSchemaId { get; init; } =
        ReferenceNatureNeowOfferSessionV111.CurrentSchemaId;
    public string ReferenceNatureTrashHeapSessionSchemaId { get; init; } =
        ReferenceNatureTrashHeapSessionV111.CurrentSchemaId;
    public string ReferenceNatureTabletOfTruthSessionSchemaId { get; init; } =
        ReferenceNatureTabletOfTruthSessionV111.CurrentSchemaId;
    public string ReferenceNatureTrialSessionSchemaId { get; init; } =
        ReferenceNatureTrialSessionV111.CurrentSchemaId;
    public string ReferenceNatureNativeEventShuffleSessionSchemaId { get; init; } =
        ReferenceNatureNativeEventShuffleSessionV111.CurrentSchemaId;
    public string ReferenceNatureNativeEventGoldSessionSchemaId { get; init; } =
        ReferenceNatureNativeEventGoldSessionV111.CurrentSchemaId;
    public string ReferenceNatureEndlessConveyorSessionSchemaId { get; init; } =
        ReferenceNatureEndlessConveyorSessionV111.CurrentSchemaId;
    public string ReferenceNatureGloryReflectionsSessionSchemaId { get; init; } =
        ReferenceNatureGloryReflectionsSessionV111.CurrentSchemaId;
    public string ReferenceNatureInfestedAutomatonSessionSchemaId { get; init; } =
        ReferenceNatureInfestedAutomatonSessionV111.CurrentSchemaId;
    public string ReferenceNaturePunchOffRewardSessionSchemaId { get; init; } =
        ReferenceNaturePunchOffRewardSessionV111.CurrentSchemaId;
    public string ReferenceNaturePunchOffFightSessionSchemaId { get; init; } =
        ReferenceNaturePunchOffFightSessionV111.CurrentSchemaId;
    public string ReferenceNatureMerchantSessionSchemaId { get; init; } =
        ReferenceNatureMerchantSessionV111.CurrentSchemaId;
    public string ReferenceNatureTreasureSessionSchemaId { get; init; } =
        ReferenceNatureTreasureSessionV111.CurrentSchemaId;
    public string SemanticNatureChoiceViewSchemaId { get; init; } =
        SemanticNatureChoiceViewV111.CurrentSchemaId;
    public string[] SemanticNatureChoiceViewOperations { get; init; } =
        ["nature_choice_observe", "semantic_nature_observe"];
    public string[] SemanticNatureChoiceViewSessionKinds { get; init; } =
    [
        "semantic_combat", "reference_combat", "run_action",
        "event_choice", "ancient_offer", "neow_offer", "trash_heap",
        "tablet_of_truth", "trial", "event_shuffle", "native_event_gold",
        "endless_conveyor",
        "merchant", "treasure", "glory_reflections",
        "infested_automaton",
        "punch_off_reward", "punch_off_fight",
        "controlled_event_choice", "controlled_run_action"
    ];
}

public static class StochasticCapabilitiesV111
{
    public const string SchemaId = "sts2-v111-stochastic-capabilities-v2";
    public const string ExternalSelectionProviderId =
        "external-semantic-selection-v1";

    public static StochasticCapabilityManifestV111 Create() => new(
        SchemaId,
        ChanceLawV111.CurrentSchemaId,
        ReferenceNatureV111.ProviderId,
        ReferenceNatureSessionV111.CurrentSchemaId,
        ChanceLawV111.UnverifiedStatus,
        StochasticSiteRegistryV111.NatureChoiceSiteIds,
        PrototypeAiEnvironment.SchemaId,
        [
            ReferenceNatureV111.ProviderId,
            SemanticNativePolicyAuditV111.NativeProviderId,
            ExternalSelectionProviderId
        ],
        [
            "semantic_reference_begin", "semantic_reference_observe",
            "semantic_reference_step", "semantic_reference_resolve_next",
            "semantic_reference_status", "semantic_reference_fork",
            "semantic_reference_save", "semantic_reference_load",
            "semantic_reference_release_many",
            "reference_nature_run_action_begin",
            "reference_nature_run_action_status",
            "reference_nature_run_action_resolve_next",
            "reference_nature_run_action_fork",
            "reference_nature_run_action_save",
            "reference_nature_run_action_load",
            "reference_nature_run_action_release_many",
            "reference_nature_event_choice_begin",
            "reference_nature_event_choice_status",
            "reference_nature_event_choice_resolve_next",
            "reference_nature_event_choice_advance",
            "reference_nature_event_choice_fork",
            "reference_nature_event_choice_save",
            "reference_nature_event_choice_load",
            "reference_nature_event_choice_release_many",
            "reference_nature_ancient_offer_begin",
            "reference_nature_ancient_offer_status",
            "reference_nature_ancient_offer_resolve_next",
            "reference_nature_ancient_offer_fork",
            "reference_nature_ancient_offer_save",
            "reference_nature_ancient_offer_load",
            "reference_nature_ancient_offer_release_many",
            "reference_nature_neow_offer_begin",
            "reference_nature_neow_offer_status",
            "reference_nature_neow_offer_resolve_next",
            "reference_nature_neow_offer_fork",
            "reference_nature_neow_offer_save",
            "reference_nature_neow_offer_load",
            "reference_nature_neow_offer_release_many",
            "reference_nature_trash_heap_begin",
            "reference_nature_trash_heap_status",
            "reference_nature_trash_heap_resolve_next",
            "reference_nature_trash_heap_fork",
            "reference_nature_trash_heap_save",
            "reference_nature_trash_heap_load",
            "reference_nature_trash_heap_release_many",
            "reference_nature_tablet_of_truth_begin",
            "reference_nature_tablet_of_truth_status",
            "reference_nature_tablet_of_truth_resolve_next",
            "reference_nature_tablet_of_truth_fork",
            "reference_nature_tablet_of_truth_save",
            "reference_nature_tablet_of_truth_load",
            "reference_nature_tablet_of_truth_release_many",
            "reference_nature_trial_begin",
            "reference_nature_trial_status",
            "reference_nature_trial_resolve_next",
            "reference_nature_trial_fork",
            "reference_nature_trial_save",
            "reference_nature_trial_load",
            "reference_nature_trial_release_many",
            "reference_nature_native_event_shuffle_begin",
            "reference_nature_native_event_shuffle_status",
            "reference_nature_native_event_shuffle_resolve_next",
            "reference_nature_native_event_shuffle_fork",
            "reference_nature_native_event_shuffle_save",
            "reference_nature_native_event_shuffle_load",
            "reference_nature_native_event_shuffle_release_many",
            "reference_nature_native_event_gold_begin",
            "reference_nature_native_event_gold_status",
            "reference_nature_native_event_gold_resolve_next",
            "reference_nature_native_event_gold_fork",
            "reference_nature_native_event_gold_save",
            "reference_nature_native_event_gold_load",
            "reference_nature_native_event_gold_release_many",
            "reference_nature_endless_conveyor_begin",
            "reference_nature_endless_conveyor_status",
            "reference_nature_endless_conveyor_resolve_next",
            "reference_nature_endless_conveyor_fork",
            "reference_nature_endless_conveyor_save",
            "reference_nature_endless_conveyor_load",
            "reference_nature_endless_conveyor_release_many",
            "reference_nature_glory_reflections_begin",
            "reference_nature_glory_reflections_status",
            "reference_nature_glory_reflections_resolve_next",
            "reference_nature_glory_reflections_fork",
            "reference_nature_glory_reflections_save",
            "reference_nature_glory_reflections_load",
            "reference_nature_glory_reflections_release_many",
            "reference_nature_infested_automaton_begin",
            "reference_nature_infested_automaton_status",
            "reference_nature_infested_automaton_resolve_next",
            "reference_nature_infested_automaton_fork",
            "reference_nature_infested_automaton_save",
            "reference_nature_infested_automaton_load",
            "reference_nature_infested_automaton_release_many",
            "reference_nature_punch_off_reward_begin",
            "reference_nature_punch_off_reward_status",
            "reference_nature_punch_off_reward_resolve_next",
            "reference_nature_punch_off_reward_fork",
            "reference_nature_punch_off_reward_save",
            "reference_nature_punch_off_reward_load",
            "reference_nature_punch_off_reward_release_many",
            "reference_nature_punch_off_fight_begin",
            "reference_nature_punch_off_fight_status",
            "reference_nature_punch_off_fight_resolve_next",
            "reference_nature_punch_off_fight_fork",
            "reference_nature_punch_off_fight_save",
            "reference_nature_punch_off_fight_load",
            "reference_nature_punch_off_fight_release_many",
            "reference_nature_merchant_begin",
            "reference_nature_merchant_status",
            "reference_nature_merchant_resolve_next",
            "reference_nature_merchant_fork",
            "reference_nature_merchant_save",
            "reference_nature_merchant_load",
            "reference_nature_merchant_release_many",
            "reference_nature_treasure_begin",
            "reference_nature_treasure_status",
            "reference_nature_treasure_resolve_next",
            "reference_nature_treasure_fork",
            "reference_nature_treasure_save",
            "reference_nature_treasure_load",
            "reference_nature_treasure_release_many"
        ])
    {
        ProbabilityStatuses =
        [
            ChanceLawV111.UnverifiedStatus,
            ChanceLawV111.SourceVerifiedStatus
        ],
        ReferenceNatureRunActionSessionSchemaId =
            ReferenceNatureRunActionSessionV111.CurrentSchemaId,
        ReferenceNatureEventChoiceSessionSchemaId =
            ReferenceNatureControlledEventChoiceSessionV111.CurrentSchemaId,
        ReferenceNatureAncientOfferSessionSchemaId =
            ReferenceNatureAncientOfferSessionV111.CurrentSchemaId,
        ReferenceNatureNeowOfferSessionSchemaId =
            ReferenceNatureNeowOfferSessionV111.CurrentSchemaId,
        ReferenceNatureTrashHeapSessionSchemaId =
            ReferenceNatureTrashHeapSessionV111.CurrentSchemaId,
        ReferenceNatureTabletOfTruthSessionSchemaId =
            ReferenceNatureTabletOfTruthSessionV111.CurrentSchemaId,
        ReferenceNatureTrialSessionSchemaId =
            ReferenceNatureTrialSessionV111.CurrentSchemaId,
        ReferenceNatureNativeEventShuffleSessionSchemaId =
            ReferenceNatureNativeEventShuffleSessionV111.CurrentSchemaId,
        ReferenceNatureNativeEventGoldSessionSchemaId =
            ReferenceNatureNativeEventGoldSessionV111.CurrentSchemaId,
        ReferenceNatureEndlessConveyorSessionSchemaId =
            ReferenceNatureEndlessConveyorSessionV111.CurrentSchemaId,
        ReferenceNatureGloryReflectionsSessionSchemaId =
            ReferenceNatureGloryReflectionsSessionV111.CurrentSchemaId,
        ReferenceNatureInfestedAutomatonSessionSchemaId =
            ReferenceNatureInfestedAutomatonSessionV111.CurrentSchemaId,
        ReferenceNaturePunchOffRewardSessionSchemaId =
            ReferenceNaturePunchOffRewardSessionV111.CurrentSchemaId,
        ReferenceNaturePunchOffFightSessionSchemaId =
            ReferenceNaturePunchOffFightSessionV111.CurrentSchemaId,
        ReferenceNatureMerchantSessionSchemaId =
            ReferenceNatureMerchantSessionV111.CurrentSchemaId,
        ReferenceNatureTreasureSessionSchemaId =
            ReferenceNatureTreasureSessionV111.CurrentSchemaId
    };
}

/// <summary>
/// Read-only conditional distribution for one unresolved semantic event.
/// EventId correlates the query with mechanics; LawId fingerprints only the
/// declared support and conditioning, never the full engine boundary identity.
/// Weights are exact integer numerators over TotalWeight; provenance makes no
/// claim that an emulator model has been verified against the original game.
/// </summary>
public sealed record ChanceLawV111(
    string SchemaId,
    string LawId,
    string EventId,
    string SiteId,
    string OperationKind,
    string OwnerId,
    int Ordinal,
    string SupportFingerprint,
    string ConditioningPolicyId,
    string ConditioningFingerprint,
    string ProbabilityStatus,
    string ProbabilityProvenance,
    ChanceLawOutcomeV111[] Outcomes,
    long TotalWeight)
{
    public const string CurrentSchemaId = "sts2-v111-chance-law-v1";
    public const string UnverifiedStatus = "UNVERIFIED";
    public const string SourceVerifiedStatus = "SOURCE_VERIFIED";

    public ChanceLawV111 Fork() => this with
    {
        Outcomes = (ChanceLawOutcomeV111[])Outcomes.Clone()
    };

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(LawId) ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(SiteId) ||
            string.IsNullOrWhiteSpace(OperationKind) ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            string.IsNullOrWhiteSpace(ConditioningPolicyId) ||
            string.IsNullOrWhiteSpace(ConditioningFingerprint) ||
            ProbabilityStatus is not (UnverifiedStatus or SourceVerifiedStatus) ||
            string.IsNullOrWhiteSpace(ProbabilityProvenance) ||
            Outcomes is not { Length: > 0 } || TotalWeight <= 0)
            throw new InvalidDataException("Invalid Chance Law envelope.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        foreach (var outcome in Outcomes)
        {
            if (outcome is null || string.IsNullOrWhiteSpace(outcome.OutcomeId) ||
                outcome.Weight <= 0 || !ids.Add(outcome.OutcomeId))
                throw new InvalidDataException("Invalid Chance Law support.");
            total = checked(total + outcome.Weight);
        }

        if (total != TotalWeight ||
            SupportFingerprint != CanonicalJson.Sha256(Outcomes))
            throw new InvalidDataException(
                "Chance Law support fingerprint or total weight is inconsistent.");

        var expectedLawId = CanonicalJson.Sha256(new
        {
            SchemaId,
            SiteId,
            OperationKind,
            OwnerId,
            Ordinal,
            SupportFingerprint,
            ConditioningPolicyId,
            ConditioningFingerprint,
            ProbabilityStatus,
            ProbabilityProvenance
        });
        if (LawId != expectedLawId)
            throw new InvalidDataException("Chance Law identifier is inconsistent.");
    }
}

/// <summary>
/// Outcome-blind queries for supported semantic Nature sites. Queries inspect
/// only declared semantic support and provider constraints; Native RNG state
/// and selected current outcomes are not inputs.
/// </summary>
public static partial class ChanceLawEngineV111
{
    private const long NativeNextDoubleGridSize = 1L << 53;

    public const string NativeBoundedIndexSourceVerifiedProvenance =
        "v0.111.0 Rng.NextInt(maxExclusive) multiplies the uniform 53-bit " +
        "NextDouble grid and truncates. Exact source bin masses include " +
        "binary64 rounding boundaries; run-seed frequencies and stream " +
        "derivation are outside this claim.";
    public const string NativeUnitFloatThresholdSourceVerifiedProvenance =
        "v0.111.0 Rng.NextFloat() rounds the uniform 53-bit NextDouble grid " +
        "to binary32 before the source threshold comparisons. Exact grid " +
        "preimages include float rounding and comparison inclusivity; seed " +
        "frequencies and stream derivation are outside this claim.";
    public const string NativeUnitFloatMappedOutcomeSourceVerifiedProvenance =
        "v0.111.0 Rng.NextFloat() rounds the uniform 53-bit NextDouble grid " +
        "to binary32 before the source outcome mapping. Exact grid " +
        "preimages include subsequent source float and integer rounding; " +
        "seed frequencies and stream derivation are outside this claim.";
    public const string NativeUnitFloatWeightedTicketSourceVerifiedProvenance =
        "v0.111.0 Rng.NextFloat() rounds the uniform 53-bit NextDouble grid " +
        "to binary32 before the weighted ticket mapping. Exact grid " +
        "preimages include the source float multiplication and sequential " +
        "ticket subtraction; seed frequencies are outside this claim.";

    /// <summary>Exact integer masses for each result of the pinned Native
    /// bounded-index transform, scaled by the 2^53 input-grid size. This
    /// counts the binary64 multiply's round-to-nearest bins before truncation.
    /// </summary>
    internal static long[] NativeNextIntGridWeights(int boundExclusive)
    {
        if (boundExclusive <= 0)
            throw new ArgumentOutOfRangeException(nameof(boundExclusive));

        var weights = new long[boundExclusive];
        long total = 0;
        for (var index = 0; index < boundExclusive; index++)
        {
            var first = index == 0 ? 0 : NativeNextIntBoundaryGridIndex(
                index, boundExclusive);
            var end = NativeNextIntBoundaryGridIndex(
                index + 1, boundExclusive);
            var weight = checked(end - first);
            if (weight <= 0)
                throw new InvalidDataException(
                    "Native bounded-index grid produced an empty outcome bin.");
            weights[index] = weight;
            total = checked(total + weight);
        }

        if (total != NativeNextDoubleGridSize)
            throw new InvalidDataException(
                "Native bounded-index grid masses do not cover the full input grid.");
        return weights;
    }

    /// <summary>Counts grid inputs whose source-rounded unit float is strictly
    /// below a threshold. The mapping is monotone, so a binary search over the
    /// 2^53 inputs gives the exact preimage count without enumerating the grid.
    /// </summary>
    internal static long NativeNextFloatGridCountBelow(float threshold)
    {
        if (!float.IsFinite(threshold))
            throw new ArgumentOutOfRangeException(nameof(threshold));
        return NativeNextFloatGridCount(value => value < threshold);
    }

    /// <summary>Counts grid inputs whose source-rounded unit float is at most
    /// a threshold, preserving source branches that use an inclusive check.
    /// </summary>
    internal static long NativeNextFloatGridCountAtOrBelow(float threshold)
    {
        if (!float.IsFinite(threshold))
            throw new ArgumentOutOfRangeException(nameof(threshold));
        return NativeNextFloatGridCount(value => value <= threshold);
    }

    private static long NativeNextFloatGridCount(Func<float, bool> includes)
    {
        ArgumentNullException.ThrowIfNull(includes);
        var low = 0L;
        var high = NativeNextDoubleGridSize;
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            var sourceFloat = NativeUnitFloatGridValue(middle);
            if (includes(sourceFloat))
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }

    private static float NativeUnitFloatGridValue(long gridIndex) =>
        (float)(gridIndex * (1.0 / NativeNextDoubleGridSize));

    internal static long[] NativeNextFloatWeightedTicketGridWeights(
        IReadOnlyList<int> candidateWeights)
    {
        ArgumentNullException.ThrowIfNull(candidateWeights);
        if (candidateWeights.Count == 0 ||
            candidateWeights.Any(weight => weight <= 0))
            throw new ArgumentOutOfRangeException(nameof(candidateWeights));
        var totalWeight = checked(candidateWeights.Sum());

        int MapTicket(float roll)
        {
            var ticket = roll * totalWeight;
            for (var index = 0; index < candidateWeights.Count; index++)
            {
                if (ticket < candidateWeights[index])
                    return index;
                ticket -= candidateWeights[index];
            }
            return candidateWeights.Count - 1;
        }

        var mappedWeights = NativeNextFloatMappedIntegerWeights(MapTicket);
        var weights = new long[candidateWeights.Count];
        long total = 0;
        foreach (var (index, weight) in mappedWeights)
        {
            if (index < 0 || index >= weights.Length)
                throw new InvalidDataException(
                    "Native weighted-ticket mapping returned an invalid index.");
            weights[index] = weight;
            total = checked(total + weight);
        }
        if (total != NativeNextDoubleGridSize)
            throw new InvalidDataException(
                "Native weighted-ticket masses do not cover the input grid.");
        return weights;
    }

    private static (long Rare, long Uncommon, long Common)
        NativeCardRarityGridWeights(int rareThreshold, int uncommonEnd)
    {
        var rare = NativeNextFloatGridCountBelow(
            rareThreshold / 10000f);
        var uncommonEndCount = NativeNextFloatGridCountBelow(
            uncommonEnd / 10000f);
        if (uncommonEndCount < rare)
            throw new InvalidDataException(
                "Native card-rarity float thresholds are out of order.");
        var uncommon = checked(uncommonEndCount - rare);
        var common = checked(NativeNextDoubleGridSize - uncommonEndCount);
        return (rare, uncommon, common);
    }

    private static (long Common, long Uncommon, long Rare)
        NativeRelicRarityGridWeights()
    {
        var commonThreshold = PrototypeNativeRelicGrabBag
            .CommonRarityWeight /
            (float)PrototypeNativeRelicGrabBag.RarityWeightScale;
        var uncommonThreshold = (PrototypeNativeRelicGrabBag
            .CommonRarityWeight +
            PrototypeNativeRelicGrabBag.UncommonRarityWeight) /
            (float)PrototypeNativeRelicGrabBag.RarityWeightScale;
        var common = NativeNextFloatGridCountBelow(commonThreshold);
        var uncommonEnd =
            NativeNextFloatGridCountBelow(uncommonThreshold);
        if (uncommonEnd < common)
            throw new InvalidDataException(
                "Native relic-rarity float thresholds are out of order.");
        var uncommon = checked(uncommonEnd - common);
        var rare = checked(NativeNextDoubleGridSize - uncommonEnd);
        return (common, uncommon, rare);
    }

    private static long NativeNextIntBoundaryGridIndex(
        int integerBoundary, int boundExclusive)
    {
        if (integerBoundary <= 0 || integerBoundary > boundExclusive)
            throw new ArgumentOutOfRangeException(nameof(integerBoundary));

        // The lower neighbor of a power-of-two integer has half the spacing
        // of the values immediately above it. The integer is even in binary64
        // at these magnitudes, so the midpoint rounds to the integer itself.
        // Thus a product rounds to at least this boundary at
        // integerBoundary - 2^-denominatorPower. Scaling that rational
        // threshold by 2^53 / boundExclusive and taking its ceiling counts
        // the grid inputs below the boundary.
        var exponent = BitOperations.Log2((uint)integerBoundary);
        var denominatorPower = 53 - exponent +
            ((integerBoundary & (integerBoundary - 1)) == 0 ? 1 : 0);
        var denominatorScale = BigInteger.One << denominatorPower;
        var midpointNumerator =
            (BigInteger)integerBoundary * denominatorScale - BigInteger.One;
        var numerator = midpointNumerator * NativeNextDoubleGridSize;
        var denominator = denominatorScale * boundExclusive;
        var quotient = BigInteger.DivRem(numerator, denominator,
            out var remainder);
        if (!remainder.IsZero)
            quotient += BigInteger.One;
        return checked((long)quotient);
    }

    public const string IntentConditioningPolicyId =
        "source-filtered-intent-weights-v1";
    public const string EndTurnIndexConditioningPolicyId =
        "uniform-saved-automatic-index-support-v1";
    public const string RandomIndexConditioningPolicyId =
        "uniform-saved-random-index-support-v1";
    public const string DrawConditioningPolicyId =
        "uniform-unresolved-pool-permutation-v1";
    public const string RevealConditioningPolicyId =
        "uniform-unresolved-pool-reveal-v1";
    public const string TargetConditioningPolicyId =
        "uniform-live-enemy-instance-support-v1";
    public const string GeneratedCardConditioningPolicyId =
        "uniform-ordered-generated-card-model-pool-v1";
    public const string CostConditioningPolicyId =
        "uniform-bounded-cost-support-v1";
    public const string RandomPileCardConditioningPolicyId =
        "uniform-ordered-physical-pile-card-support-v1";
    public const string CardTransformConditioningPolicyId =
        "uniform-card-transform-replacement-pool-v1";
    public const string PotionGenerationConditioningPolicyId =
        "uniform-potion-generation-model-pool-v1";
    public const string HandCostSelectionConditioningPolicyId =
        "uniform-hand-cost-target-support-v1";
    public const string PoolShuffleConditioningPolicyId =
        "uniform-fisher-yates-bounded-swap-v1";
    public const string DrawInsertionConditioningPolicyId =
        "uniform-bounded-draw-pile-insertion-position-v1";
    public const string CardShortlistConditioningPolicyId =
        "uniform-sequential-without-replacement-v1";
    public const string EventOfferConditioningPolicyId =
        "uniform-ordered-event-offer-entry-v1";
    public const string AncientOfferShuffleConditioningPolicyId =
        "uniform-ancient-offer-fisher-yates-swap-v1";
    public const string DarvOfferVariantConditioningPolicyId =
        "uniform-darv-offer-variant-v1";
    public const string NativeMerchantIndexConditioningPolicyId =
        "uniform-native-merchant-index-support-v1";
    public const string NativeMerchantThresholdConditioningPolicyId =
        "source-threshold-native-merchant-category-v1";
    public const string NativePotionFactoryIndexConditioningPolicyId =
        "source-bounded-potion-factory-model-index-v1";
    public const string NativePotionFactoryThresholdConditioningPolicyId =
        "source-threshold-potion-factory-rarity-v1";
    public const string NativeMerchantPriceConditioningPolicyId =
        "native-merchant-rounded-price-modifier-v1";
    public const string NativeTreasureRelicConditioningPolicyId =
        "native-treasure-relic-bag-draw-v1";

    public static ChanceLawV111 QueryIntent(SemanticIntentEventV111 intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        intent.Validate();
        var outcomes = intent.Support.Candidates
            .Select(candidate => new ChanceLawOutcomeV111(
                candidate.IntentId, candidate.RawWeight))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            intent.SupportFingerprint,
            intent.SiteId,
            intent.OwnerId,
            intent.Ordinal,
            policy = IntentConditioningPolicyId
        });

        return Create(intent.EventId, intent.SiteId, intent.OperationKind,
            intent.OwnerId, intent.Ordinal, IntentConditioningPolicyId,
            conditioningFingerprint, "source-shaped-unverified", outcomes);
    }

    /// <summary>
    /// Query the declared uniform index law at a suspended automatic
    /// enemy-action boundary. This preserves index identity even when
    /// candidate values repeat.
    /// </summary>
    public static ChanceLawV111 QueryEndTurnIndex(
        SemanticEndTurnIndexEventV111 indexEvent)
    {
        ArgumentNullException.ThrowIfNull(indexEvent);
        indexEvent.Validate();
        var outcomes = Enumerable.Range(0,
                indexEvent.Request.SupportCount)
            .Select(index => new ChanceLawOutcomeV111(
                "choice_index:" + index.ToString(CultureInfo.InvariantCulture),
                1L))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            indexEvent.SupportFingerprint,
            indexEvent.Request.SiteId,
            indexEvent.Request.OwnerId,
            indexEvent.Request.Ordinal,
            indexEvent.Request.OperationKind,
            policy = EndTurnIndexConditioningPolicyId
        });
        return Create(indexEvent.EventId, indexEvent.Request.SiteId,
            indexEvent.Request.OperationKind, indexEvent.Request.OwnerId,
            indexEvent.Request.Ordinal,
            EndTurnIndexConditioningPolicyId, conditioningFingerprint,
            "uniform-saved-enemy-action-index-support-unverified", outcomes);
    }

    /// <summary>Query a uniform law over the legal indices in a saved typed
    /// random request. Only the typed request enters the law fingerprint; the
    /// selected index, enclosing run state and private RNG state do not.</summary>
    public static ChanceLawV111 QueryRandomIndex(
        string eventId, RandomIndexRequestV111 request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        var outcomes = Enumerable.Range(0,
                request.SupportCount)
            .Select(index => new ChanceLawOutcomeV111(
                "choice_index:" + index.ToString(CultureInfo.InvariantCulture),
                1L))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            RequestFingerprint = CanonicalJson.Sha256(request),
            policy = RandomIndexConditioningPolicyId
        });
        return Create(eventId, request.SiteId,
            request.OperationKind, request.OwnerId, request.Ordinal,
            RandomIndexConditioningPolicyId, conditioningFingerprint,
            "uniform-saved-index-support-unverified", outcomes);
    }

    /// <summary>
    /// Query one pre-event Ancient offer decision. Direct offer pools return
    /// stable choice identities; shuffle-based offers return the bounded
    /// Fisher-Yates swap index at the requested ordinal. The same pool builders
    /// and opening boundaries are shared with the event resolvers.
    /// </summary>
    public static ChanceLawV111 QueryAncientOffer(
        RunState run, string siteId, int ordinal) =>
        QueryAncientOffer(run, siteId, ordinal, null, null);

    public static ChanceLawV111 QueryAncientOffer(
        RunState run, string siteId, int ordinal,
        int[]? priorIndices, int? priorVariantIndex = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        if (siteId == NativeDarvEventV111.OfferVariantSiteId)
        {
            if (priorVariantIndex is not null)
                throw new ArgumentException(
                    "prior_variant_index applies only to Darv relic-shuffle queries.",
                    nameof(priorVariantIndex));
            return QueryDarvOfferVariant(run, ordinal, priorIndices);
        }
        if (siteId == NativeDarvEventV111.RelicShuffleSiteId)
            return QueryDarvRelicShuffle(
                run, ordinal, priorIndices, priorVariantIndex);
        if (priorVariantIndex is not null)
            throw new ArgumentException(
                "prior_variant_index applies only to Darv relic-shuffle queries.",
                nameof(priorVariantIndex));
        if (IsShuffleAncientOfferSite(siteId))
            return QueryAncientOfferShuffle(
                run, siteId, ordinal, priorIndices);
        if (priorIndices is { Length: > 0 })
            throw new ArgumentException(
                "prior_indices apply only to shuffle-based Ancient offers.",
                nameof(priorIndices));
        string ownerId;
        string[] candidates;
        if (siteId == NativeOrobasEventV111.OfferSiteId)
        {
            NativeOrobasEventV111.ValidateOpeningBoundary(run);
            ownerId = "event:OROBAS:act:2:floor:0";
            candidates = NativeOrobasEventV111.OfferCandidateIds(
                run.Player, ordinal);
        }
        else if (siteId == NativePaelRewardsV111.OfferSiteId)
        {
            NativePaelRewardsV111.ValidateOpeningBoundary(run);
            ownerId = "event:PAEL:act:2:floor:0";
            var pools = NativePaelRewardsV111.OfferPools(run.Player);
            if (ordinal < 0 || ordinal >= pools.Length)
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            candidates = pools[ordinal].Select(id => "take_" + id).ToArray();
        }
        else if (siteId == NativeAncientRewardsV111.TezcataraOfferSiteId)
        {
            NativeAncientRewardsV111.ValidateTezcataraOpeningBoundary(run);
            ownerId = "event:TEZCATARA:act:2:floor:0";
            var pools = NativeAncientRewardsV111.TezcataraPools(run.Player);
            if (ordinal < 0 || ordinal >= pools.Length)
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            candidates = pools[ordinal].Select(id => "take_" + id).ToArray();
        }
        else
        {
            throw new NotSupportedException(
                $"Ancient offer query does not support site '{siteId}'.");
        }

        var orderedChoiceIds = candidates;
        var indexWeights = NativeNextIntGridWeights(orderedChoiceIds.Length);
        var weights = new Dictionary<string, long>(StringComparer.Ordinal);
        var outcomeOrder = new List<string>();
        for (var index = 0; index < orderedChoiceIds.Length; index++)
        {
            var choiceId = orderedChoiceIds[index];
            var outcomeId = "event_choice:" + choiceId;
            if (!weights.TryGetValue(outcomeId, out var weight))
            {
                weights.Add(outcomeId, indexWeights[index]);
                outcomeOrder.Add(outcomeId);
            }
            else
            {
                weights[outcomeId] = checked(weight + indexWeights[index]);
            }
        }
        var outcomes = outcomeOrder.Select(outcomeId =>
            new ChanceLawOutcomeV111(outcomeId, weights[outcomeId])).ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            ownerId,
            ordinal,
            CandidateChoiceIds = orderedChoiceIds,
            policy = EventOfferConditioningPolicyId
        });
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ChanceLawV111.CurrentSchemaId,
            siteId,
            ownerId,
            ordinal,
            conditioningFingerprint
        });
        return Create(eventId, siteId, "event_offer_index", ownerId,
            ordinal, EventOfferConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>Compatibility entry point for the original query-only site.</summary>
    public static ChanceLawV111 QueryOrobasOffer(
        RunState run, int ordinal) => QueryAncientOffer(
            run, NativeOrobasEventV111.OfferSiteId, ordinal);

    /// <summary>
    /// Query discrete native merchant offer decisions before entering an
    /// available configured Shop node. Bounded-index transforms are
    /// source-verified on the declared support; model-pool completeness and
    /// threshold/float provenance remain separate claims.
    /// </summary>
    public static ChanceLawV111 QueryNativeMerchantOffer(
        RunState run, string siteId, int ordinal,
        string? cardType = null, string? rarity = null,
        string[]? excludedPotionIds = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        PrototypeStateInvariants.Validate(run);
        ValidateNativeMerchantQueryBoundary(run);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var world = run.World!;
        var actFloor = $"act:{world.Act}:floor:{world.Floor}";
        string ownerId;
        string operationKind;
        string policy;
        string provenance;
        var probabilityStatus = ChanceLawV111.UnverifiedStatus;
        object conditioning;
        ChanceLawOutcomeV111[] outcomes;

        if (siteId == "shop.merchant.sale_slot.index_draw")
        {
            if (ordinal != 0 || cardType is not null || rarity is not null ||
                excludedPotionIds is not null)
                throw new ArgumentException(
                    "Merchant sale-slot queries use ordinal 0 and no pool parameters.");
            ownerId = $"merchant:act:{world.Act}:floor:{world.Floor}";
            operationKind = "choose_index";
            policy = NativeMerchantIndexConditioningPolicyId;
            provenance = NativeBoundedIndexSourceVerifiedProvenance;
            conditioning = new { siteId, ownerId, candidateCount = 5 };
            var weights = NativeNextIntGridWeights(5);
            outcomes = Enumerable.Range(0, 5)
                .Select(index => new ChanceLawOutcomeV111(
                    "sale_slot:" + index.ToString(CultureInfo.InvariantCulture),
                    weights[index]))
                .ToArray();
            probabilityStatus = ChanceLawV111.SourceVerifiedStatus;
        }
        else if (siteId == "card_rarity.roll.unit_float")
        {
            var type = ParseMerchantCardType(cardType);
            if (rarity is not null || excludedPotionIds is not null)
                throw new ArgumentException(
                    "Merchant card-rarity queries accept only card_type.");
            var count = MerchantCardCount(type);
            if (ordinal >= count)
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            var (rareThreshold, uncommonEnd) =
                PrototypeNativeCardRarityOdds.MerchantFloatThresholds(
                    world.CardRarityOffsetBasisPoints, run.Ascension);
            var weights = rareThreshold >= 10000
                ? (Rare: NativeNextDoubleGridSize, Uncommon: 0L, Common: 0L)
                : NativeCardRarityGridWeights(
                    rareThreshold, uncommonEnd);
            ownerId = $"type:{type}:offer:{ordinal}:offset:{world.CardRarityOffsetBasisPoints}";
            operationKind = "unit_float_threshold";
            policy = NativeMerchantThresholdConditioningPolicyId;
            provenance = NativeUnitFloatThresholdSourceVerifiedProvenance;
            conditioning = new
            {
                siteId, ownerId, cardType = type.ToString(),
                world.CardRarityOffsetBasisPoints, run.Ascension,
                rareThreshold, uncommonEnd, weights
            };
            outcomes = new[]
                {
                    new ChanceLawOutcomeV111("rarity:rare", weights.Rare),
                    new ChanceLawOutcomeV111("rarity:uncommon", weights.Uncommon),
                    new ChanceLawOutcomeV111("rarity:common", weights.Common)
                }
                .Where(outcome => outcome.Weight > 0).ToArray();
            probabilityStatus = ChanceLawV111.SourceVerifiedStatus;
        }
        else if (siteId == "shop.merchant.card_model.index_draw")
        {
            var type = ParseMerchantCardType(cardType);
            var selectedRarity = ParseCardRarity(rarity);
            if (excludedPotionIds is not null)
                throw new ArgumentException(
                    "Merchant card-model queries do not accept potion exclusions.");
            var count = MerchantCardCount(type);
            if (ordinal >= count)
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            var candidates = PrototypeContent.NativeSilentRewardCardPool
                .Where(id => PrototypeContent.Card(id).Type == type &&
                    PrototypeContent.Card(id).Rarity == selectedRarity)
                .ToArray();
            if (candidates.Length == 0)
                throw new InvalidOperationException(
                    "Native merchant card pool has no models for the requested rarity.");
            ownerId = $"type:{type}:offer:{ordinal}:rarity:{selectedRarity}:pool:" +
                CanonicalJson.Sha256(candidates);
            operationKind = "choose_index";
            policy = NativeMerchantIndexConditioningPolicyId;
            provenance = NativeBoundedIndexSourceVerifiedProvenance;
            conditioning = new
            {
                siteId, ownerId, cardType = type.ToString(),
                rarity = selectedRarity.ToString(), candidates
            };
            var weights = NativeNextIntGridWeights(candidates.Length);
            outcomes = candidates.Select((id, index) =>
                new ChanceLawOutcomeV111(
                    $"card_model:{id}:index:{index}", weights[index])).ToArray();
            probabilityStatus = ChanceLawV111.SourceVerifiedStatus;
        }
        else if (siteId == "shop.colorless_card_model.index_draw")
        {
            if (cardType is not null || excludedPotionIds is not null)
                throw new ArgumentException(
                    "Colorless merchant model queries accept only ordinal and optional rarity.");
            var selectedRarity = ordinal switch
            {
                0 => PrototypeCardRarity.Uncommon,
                1 => PrototypeCardRarity.Rare,
                _ => throw new ArgumentOutOfRangeException(nameof(ordinal))
            };
            if (rarity is not null &&
                ParseCardRarity(rarity) != selectedRarity)
                throw new ArgumentException(
                    "Colorless merchant offer rarity is fixed by its ordinal.", nameof(rarity));
            var candidates = PrototypeColorlessCards.Implemented
                .Where(card => card.Rarity == selectedRarity &&
                    !card.MultiplayerOnly && card.MechanicsImplemented)
                .Select(card => card.Id).ToArray();
            if (candidates.Length == 0)
                throw new InvalidOperationException(
                    "Native merchant colorless pool is empty for this rarity.");
            ownerId = $"rarity:{selectedRarity}:pool:" +
                CanonicalJson.Sha256(candidates);
            operationKind = "choose_index";
            policy = NativeMerchantIndexConditioningPolicyId;
            provenance = NativeBoundedIndexSourceVerifiedProvenance;
            conditioning = new
            {
                siteId, ownerId, ordinal,
                rarity = selectedRarity.ToString(), candidates
            };
            var weights = NativeNextIntGridWeights(candidates.Length);
            outcomes = candidates.Select((id, index) =>
                new ChanceLawOutcomeV111(
                    $"card_model:{id}:index:{index}", weights[index])).ToArray();
            probabilityStatus = ChanceLawV111.SourceVerifiedStatus;
        }
        else if (siteId == "potion_factory.rarity_roll.unit_float")
        {
            if (ordinal >= 3 || cardType is not null || rarity is not null ||
                excludedPotionIds is not null)
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            ownerId = $"merchant:act:{world.Act}:floor:{world.Floor}:potions";
            operationKind = "unit_float_threshold";
            policy = NativeMerchantThresholdConditioningPolicyId;
            provenance = NativeUnitFloatThresholdSourceVerifiedProvenance;
            var rareWeight = NativeNextFloatGridCountAtOrBelow(0.1f);
            var uncommonEndWeight =
                NativeNextFloatGridCountAtOrBelow(0.35f);
            var commonWeight = checked(
                NativeNextDoubleGridSize - uncommonEndWeight);
            var uncommonWeight = checked(
                uncommonEndWeight - rareWeight);
            conditioning = new
            {
                siteId, ownerId, ordinal,
                rareInclusiveThreshold = 0.1f,
                uncommonInclusiveThreshold = 0.35f,
                common = commonWeight,
                uncommon = uncommonWeight,
                rare = rareWeight
            };
            outcomes =
            [
                new ChanceLawOutcomeV111("rarity:common", commonWeight),
                new ChanceLawOutcomeV111("rarity:uncommon", uncommonWeight),
                new ChanceLawOutcomeV111("rarity:rare", rareWeight)
            ];
            probabilityStatus = ChanceLawV111.SourceVerifiedStatus;
        }
        else if (siteId == "potion_factory.model_within_rarity.index_draw")
        {
            if (cardType is not null || rarity is null || ordinal >= 3)
                throw new ArgumentException(
                    "Potion model queries require a supported rarity and offer ordinal 0-2.");
            if (excludedPotionIds is null)
                excludedPotionIds = [];
            if (excludedPotionIds.Any(string.IsNullOrWhiteSpace) ||
                excludedPotionIds.Distinct(StringComparer.Ordinal).Count() !=
                    excludedPotionIds.Length || excludedPotionIds.Length != ordinal ||
                excludedPotionIds.Any(id =>
                    !PrototypeNativePotionShop.NativePotionPoolOrder.Contains(
                        id, StringComparer.Ordinal)))
                throw new ArgumentException(
                    "excluded_potion_ids must contain exactly one unique native potion ID per preceding offer.");
            var selectedRarity = ParsePotionRarity(rarity);
            var candidates = PrototypeNativePotionShop.CandidatesForRarity(
                selectedRarity, excludedPotionIds);
            if (candidates.Length == 0)
                throw new InvalidOperationException(
                    "Native merchant potion pool is empty for the requested rarity and exclusions.");
            ownerId = $"merchant:act:{world.Act}:floor:{world.Floor}:potions";
            operationKind = "choose_index";
            policy = NativeMerchantIndexConditioningPolicyId;
            provenance = NativeBoundedIndexSourceVerifiedProvenance;
            conditioning = new
            {
                siteId, ownerId, ordinal,
                rarity = selectedRarity.ToString(),
                excludedPotionIds, candidates
            };
            var weights = NativeNextIntGridWeights(candidates.Length);
            outcomes = candidates.Select((id, index) =>
                new ChanceLawOutcomeV111(
                    $"potion_model:{id}:index:{index}", weights[index])).ToArray();
            probabilityStatus = ChanceLawV111.SourceVerifiedStatus;
        }
        else if (siteId == "relic_grab_bag.rarity.unit_float")
        {
            if (ordinal >= 2 || cardType is not null || rarity is not null ||
                excludedPotionIds is not null)
                throw new ArgumentException(
                    "Merchant relic-rarity queries support only the first two offer ordinals.");
            ownerId = $"{actFloor}:merchant_relic:{ordinal}:merchant";
            operationKind = "unit_float_threshold";
            policy = NativeMerchantThresholdConditioningPolicyId;
            provenance = NativeUnitFloatThresholdSourceVerifiedProvenance;
            var weights = NativeRelicRarityGridWeights();
            conditioning = new
            {
                siteId, ownerId, ordinal,
                commonThreshold = PrototypeNativeRelicGrabBag
                    .CommonRarityWeight /
                    (float)PrototypeNativeRelicGrabBag.RarityWeightScale,
                uncommonThreshold = (PrototypeNativeRelicGrabBag
                    .CommonRarityWeight +
                    PrototypeNativeRelicGrabBag.UncommonRarityWeight) /
                    (float)PrototypeNativeRelicGrabBag.RarityWeightScale,
                weights
            };
            outcomes =
            [
                new ChanceLawOutcomeV111("rarity:common", weights.Common),
                new ChanceLawOutcomeV111("rarity:uncommon", weights.Uncommon),
                new ChanceLawOutcomeV111("rarity:rare", weights.Rare)
            ];
            probabilityStatus = ChanceLawV111.SourceVerifiedStatus;
        }
        else
        {
            throw new NotSupportedException(
                $"Native merchant Chance Law query does not support site '{siteId}'.");
        }

        return CreateNativeBoundaryLaw(
            siteId, operationKind, ownerId, ordinal, policy,
            CanonicalJson.Sha256(conditioning), provenance, outcomes,
            probabilityStatus);
    }

    /// <summary>
    /// Query the next identity from a Native merchant relic queue while its
    /// order remains private. The ordered support is canonicalized by the
    /// caller and checked against the source fallback plan after prior
    /// session selections have been removed.
    /// </summary>
    public static ChanceLawV111 QueryNativeMerchantRelicSelection(
        RunState run, RandomIndexRequestV111 request,
        int offerOrdinal, PrototypeRelicRarity rarity,
        string[] candidateIds, string[] excludedRelicIds)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateIds);
        ArgumentNullException.ThrowIfNull(excludedRelicIds);
        PrototypeStateInvariants.Validate(run);
        ValidateNativeMerchantQueryBoundary(run);
        request.Validate();
        var world = run.World!;
        if (request.SiteId !=
                PrototypeNativeRelicGrabBag.NativeMerchantModelSiteId ||
            request.StreamId != NativePlayerRngV111.ShopsStreamId ||
            request.OperationKind != "choose_index" ||
            offerOrdinal is < 0 or >= 3 ||
            !Enum.IsDefined(rarity) ||
            candidateIds.Length == 0 ||
            request.SupportCount != candidateIds.Length ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, candidateIds.Length)) ||
            !candidateIds.SequenceEqual(candidateIds.OrderBy(
                id => id, StringComparer.Ordinal), StringComparer.Ordinal) ||
            candidateIds.Any(string.IsNullOrWhiteSpace) ||
            candidateIds.Distinct(StringComparer.Ordinal).Count() !=
                candidateIds.Length ||
            excludedRelicIds.Any(string.IsNullOrWhiteSpace) ||
            excludedRelicIds.Distinct(StringComparer.Ordinal).Count() !=
                excludedRelicIds.Length ||
            excludedRelicIds.Length > offerOrdinal ||
            excludedRelicIds.Any(id =>
                !PrototypeContent.RelicPool.Contains(id, StringComparer.Ordinal)))
            throw new ArgumentException(
                "Native merchant relic selection request is malformed.");

        var ownerId = PrototypeNativeRelicGrabBag.MerchantModelOwnerId(
            world, offerOrdinal, rarity, candidateIds);
        if (request.OwnerId != ownerId || world.RelicBag is not { } bag)
            throw new InvalidDataException(
                "Native merchant relic request owner or bag differs from its boundary.");

        var excluded = excludedRelicIds.ToHashSet(StringComparer.Ordinal);
        static string[] RemoveSelected(string[] ids,
            HashSet<string> selected) => ids.Where(id => !selected.Contains(id))
            .ToArray();
        var conditionedBag = bag with
        {
            Common = RemoveSelected(bag.Common, excluded),
            Uncommon = RemoveSelected(bag.Uncommon, excluded),
            Rare = RemoveSelected(bag.Rare, excluded),
            Shop = RemoveSelected(bag.Shop, excluded)
        };
        var plan = PrototypeNativeRelicGrabBag.PlanPull(
            conditionedBag, rarity, merchant: true, run.Player);
        var expectedCandidates = plan.CandidateIds
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (plan.SelectedTier is null ||
            !candidateIds.SequenceEqual(expectedCandidates,
                StringComparer.Ordinal))
            throw new InvalidDataException(
                "Native merchant relic support differs from the conditioned hidden queue.");

        var policy = NativeMerchantIndexConditioningPolicyId;
        var conditioning = new
        {
            request,
            offerOrdinal,
            rarity = rarity.ToString(),
            selectedTier = plan.SelectedTier.Value.ToString(),
            candidateIds,
            excludedRelicIds = excludedRelicIds.OrderBy(
                id => id, StringComparer.Ordinal).ToArray(),
            policy
        };
        var outcomes = candidateIds.Select((id, index) =>
            new ChanceLawOutcomeV111(
                $"relic_model:{id}:index:{index}", 1L)).ToArray();
        return CreateNativeBoundaryLaw(
            request.SiteId, request.OperationKind, request.OwnerId,
            request.Ordinal, policy, CanonicalJson.Sha256(conditioning),
            "native-merchant-hidden-relic-queue-identity-unverified",
            outcomes);
    }

    /// <summary>
    /// Query the mapped integer price produced by one native merchant unit
    /// float. We count exact source-grid preimages through the unit-float
    /// rounding and the source price conversion.
    /// </summary>
    public static ChanceLawV111 QueryNativeMerchantPrice(
        RunState run, string siteId, int ordinal,
        string? cardId = null, string? potionId = null,
        string? relicId = null, bool colorlessOffer = false,
        bool saleReprice = false)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        PrototypeStateInvariants.Validate(run);
        ValidateNativeMerchantQueryBoundary(run);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var world = run.World!;
        var actFloor = $"act:{world.Act}:floor:{world.Floor}";
        int baseCost;
        double minimumModifier;
        double maximumModifier;
        string ownerId;
        string itemId;
        if (siteId == "shop.merchant.card_price_modifier.unit_float")
        {
            if (cardId is null || potionId is not null || relicId is not null ||
                (colorlessOffer
                    ? ordinal >= 2 || !PrototypeColorlessCards.ImplementedShopPool
                        .Contains(cardId, StringComparer.Ordinal)
                    : ordinal >= 5 || !PrototypeContent.NativeSilentRewardCardPool
                        .Contains(cardId, StringComparer.Ordinal)) ||
                saleReprice && colorlessOffer)
                throw new ArgumentException(
                    "Card-price queries require a supported character or Colorless card and offer ordinal.");
            itemId = cardId;
            baseCost = PrototypeGameEngine.NativeMerchantCardBaseCost(cardId);
            minimumModifier = 0.95;
            maximumModifier = 1.05;
            ownerId = colorlessOffer
                ? $"{actFloor}:colorless_offer:{ordinal}:card:{cardId}"
                : saleReprice
                    ? $"{actFloor}:offer:{ordinal}:sale_reprice:{cardId}"
                    : $"{actFloor}:offer:{ordinal}:card:{cardId}";
        }
        else if (siteId == "shop.merchant.potion_price_modifier.unit_float")
        {
            if (potionId is null || cardId is not null || relicId is not null ||
                colorlessOffer || saleReprice || ordinal >= 3 ||
                !PrototypeNativePotionShop.NativePotionPoolOrder.Contains(
                    potionId, StringComparer.Ordinal))
                throw new ArgumentException(
                    "Potion-price queries require a native potion ID and offer ordinal 0-2.");
            itemId = potionId;
            baseCost = PrototypeNativePotionShop.MerchantBaseCost(potionId);
            minimumModifier = 0.95;
            maximumModifier = 1.05;
            ownerId = $"{actFloor}:offer:{ordinal}:potion:{potionId}:{potionId}";
        }
        else if (siteId == "shop.merchant.relic_price_modifier.unit_float")
        {
            if (relicId is null || cardId is not null || potionId is not null ||
                colorlessOffer || saleReprice || ordinal >= 3 ||
                !PrototypeNativeRelicGrabBag.TryGetRarity(relicId, out _))
                throw new ArgumentException(
                    "Relic-price queries require a native relic ID and offer ordinal 0-2.");
            itemId = relicId;
            baseCost = PrototypeNativeRelicGrabBag.MerchantBaseCost(relicId);
            minimumModifier = 0.85;
            maximumModifier = 1.15;
            ownerId = $"{actFloor}:offer:{ordinal}:relic:{relicId}:{relicId}";
        }
        else
        {
            throw new NotSupportedException(
                $"Native merchant price query does not support site '{siteId}'.");
        }

        int MapPrice(float roll)
        {
            if (siteId == "shop.merchant.card_price_modifier.unit_float")
            {
                var price = PrototypeGameEngine
                    .ResolveNativeMerchantCardPrice(itemId, roll);
                return saleReprice ? price / 2 : price;
            }
            if (siteId == "shop.merchant.potion_price_modifier.unit_float")
                return PrototypeNativePotionShop
                    .ResolveMerchantPriceNative(itemId, roll);
            if (siteId == "shop.merchant.relic_price_modifier.unit_float")
                return PrototypeNativeRelicGrabBag
                    .ResolveMerchantPriceNative(itemId, roll);
            throw new NotSupportedException(
                $"Native merchant price mapping does not support '{siteId}'.");
        }

        var outcomes = NativeNextFloatMappedIntegerWeights(MapPrice)
            .Select(pair => new ChanceLawOutcomeV111(
                "price:" + pair.Key.ToString(CultureInfo.InvariantCulture),
                pair.Value)).ToArray();
        var conditioning = new
        {
            siteId, ownerId, ordinal, itemId, baseCost,
            minimumModifier, maximumModifier, colorlessOffer, saleReprice,
            outcomes
        };
        return CreateNativeBoundaryLaw(
            siteId, "unit_float_mapped_price", ownerId, ordinal,
            NativeMerchantPriceConditioningPolicyId,
            CanonicalJson.Sha256(conditioning),
            NativeUnitFloatMappedOutcomeSourceVerifiedProvenance,
            outcomes, ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Merchant card creation consumes this float even though the pinned
    /// negative upgrade chance makes every value mechanically equivalent.
    /// The only semantic result is source-verified as no_effect; the raw float
    /// sample and Native cursor advancement remain outside this law.
    /// </summary>
    public static ChanceLawV111 QueryNativeMerchantNoEffectUnitFloat(
        RunState run, RandomUnitFloatRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(request);
        PrototypeStateInvariants.Validate(run);
        ValidateNativeMerchantQueryBoundary(run);
        request.Validate();
        if (request.SiteId != "shop.merchant.card_upgrade_roll.unit_float" ||
            request.StreamId != NativePlayerRngV111.ShopsStreamId ||
            !request.OwnerId.StartsWith("type:", StringComparison.Ordinal) ||
            !request.OwnerId.Contains(":offer:", StringComparison.Ordinal) ||
            !request.OwnerId.Contains(":card:", StringComparison.Ordinal))
            throw new ArgumentException(
                "Merchant upgrade-roll request is malformed.");
        return CreateNativeBoundaryLaw(
            request.SiteId, "unit_float_no_effect", request.OwnerId,
            request.Ordinal, "native-merchant-no-visible-upgrade-effect-v1",
            CanonicalJson.Sha256(request),
            "native-merchant-upgrade-roll-always-has-no-mechanical-effect",
            [new ChanceLawOutcomeV111("no_effect", 1L)],
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query one source-ordered potion-factory request made while buying an
    /// Alchemical Coffer from a configured Native shop. The request and exact
    /// legal purchase action bind the law; prior selected models condition the
    /// remaining rarity pool without consulting either run RNG stream.
    /// </summary>
    public static ChanceLawV111 QueryNativeAlchemicalCofferPotionRequest(
        RunState run, GameAction action,
        ControlledStochasticRequestV111 request,
        PrototypeNativePotionRarity? rarity,
        string[] excludedPotionIds)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(excludedPotionIds);
        PrototypeStateInvariants.Validate(run);
        request.Validate();
        if (run.Phase != RunPhase.Shop || run.World is not { } world ||
            run.NativeRunRng is null || run.ControlledRunActionChoice is not null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            world.RelicBag is null || action.Kind != "buy_relic" ||
            world.Shop is not { } shop)
            throw new InvalidOperationException(
                "Alchemical Coffer potion laws require a configured Native Shop purchase boundary.");

        var payload = action.ReadPayload<BuyOfferPayload>();
        if (!shop.RelicOffers.Any(offer =>
                offer.OfferId == payload.OfferId && !offer.Sold &&
                offer.ItemId == PrototypeNativeOrobasRelics.AlchemicalCofferId) ||
            !new PrototypeGameEngine().GetLegalActions(run).Any(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) ==
                PrototypeAiEnvironment.StableActionId(action)))
            throw new InvalidOperationException(
                "Alchemical Coffer potion laws require its current legal relic purchase.");

        const string ownerId = "relic:alchemical_coffer:acquisition";
        const string streamId = "run.combat_potion_generation";
        if (request.StreamId != streamId || request.OwnerId != ownerId ||
            request.Ordinal >= 4)
            throw new InvalidDataException(
                "Alchemical Coffer potion request has an invalid source identity.");

        var actionFingerprint = PrototypeAiEnvironment.StableActionId(action);
        if (request.UnitFloatRequest is { } floatRequest &&
            floatRequest.SiteId == "potion_factory.rarity_roll.unit_float")
        {
            if (rarity is not null || excludedPotionIds.Length != 0)
                throw new ArgumentException(
                    "Coffer rarity laws do not accept a model-pool prefix.");
            var rare = NativeNextFloatGridCountAtOrBelow(0.1f);
            var uncommonEnd = NativeNextFloatGridCountAtOrBelow(0.35f);
            var common = checked(NativeNextDoubleGridSize - uncommonEnd);
            var uncommon = checked(uncommonEnd - rare);
            var conditioning = new
            {
                actionFingerprint, request = floatRequest,
                rareInclusiveThreshold = 0.1f,
                uncommonInclusiveThreshold = 0.35f,
                common, uncommon, rare
            };
            return CreateNativeBoundaryLaw(
                floatRequest.SiteId, "unit_float_threshold", ownerId,
                floatRequest.Ordinal,
                NativePotionFactoryThresholdConditioningPolicyId,
                CanonicalJson.Sha256(conditioning),
                NativeUnitFloatThresholdSourceVerifiedProvenance,
                [
                    new ChanceLawOutcomeV111("rarity:common", common),
                    new ChanceLawOutcomeV111("rarity:uncommon", uncommon),
                    new ChanceLawOutcomeV111("rarity:rare", rare)
                ], ChanceLawV111.SourceVerifiedStatus);
        }

        if (request.IndexRequest is not { } indexRequest ||
            indexRequest.SiteId != "potion_factory.model_within_rarity.index_draw" ||
            rarity is null || excludedPotionIds.Length != indexRequest.Ordinal ||
            excludedPotionIds.Any(string.IsNullOrWhiteSpace) ||
            excludedPotionIds.Distinct(StringComparer.Ordinal).Count() !=
                excludedPotionIds.Length ||
            excludedPotionIds.Any(id =>
                !PrototypeNativePotionShop.NativePotionPoolOrder.Contains(
                    id, StringComparer.Ordinal)))
            throw new InvalidDataException(
                "Alchemical Coffer potion model request has an invalid rarity prefix.");

        var candidates = PrototypeNativePotionShop.CandidatesForRarity(
            rarity.Value, excludedPotionIds);
        if (candidates.Length == 0 ||
            indexRequest.SupportCount != candidates.Length ||
            !indexRequest.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, candidates.Length)))
            throw new InvalidDataException(
                "Alchemical Coffer potion model support differs from its source pool.");
        var weights = NativeNextIntGridWeights(candidates.Length);
        var outcomes = candidates.Select((id, index) =>
            new ChanceLawOutcomeV111(
                $"potion_model:{id}:index:{index}", weights[index])).ToArray();
        var modelConditioning = new
        {
            actionFingerprint, request = indexRequest,
            rarity = rarity.Value.ToString(), excludedPotionIds, candidates
        };
        return CreateNativeBoundaryLaw(
            indexRequest.SiteId, indexRequest.OperationKind, ownerId,
            indexRequest.Ordinal,
            NativePotionFactoryIndexConditioningPolicyId,
            CanonicalJson.Sha256(modelConditioning),
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes, ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query the configured native treasure-room relic draw without exposing
    /// the private order of the persistent rarity queues. The law combines
    /// the nominal rarity thresholds with equal conditional support over the
    /// still-hidden queue permutations.
    /// </summary>
    public static ChanceLawV111 QueryNativeTreasureRelicDraw(
        RunState run, string siteId, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        PrototypeStateInvariants.Validate(run);
        if (siteId != "treasure.relic_grab_bag.draw")
            throw new NotSupportedException(
                $"Native treasure Chance Law query does not support site '{siteId}'.");
        if (ordinal != 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (run.Phase != RunPhase.MapChoice || run.World is not { } world ||
            run.NativeRunRng is null || run.ControlledRunActionChoice is not null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            !PrototypeNativeWingedBoots.AvailableNodes(world, run.Player)
                .Any(node => node.RoomType == PrototypeRoomType.Treasure) ||
            PrototypeGameEngine.WillSkipNextTreasure(run.Player))
            throw new InvalidOperationException(
                "Native treasure Chance Laws require a non-skipped configured Treasure node before entry.");

        var bag = world.RelicBag ??
            PrototypeNativeRelicGrabBag.CreateNatureUnordered(run.Player);
        var candidatesByTier = new Dictionary<PrototypeRelicRarity, string[]>
        {
            [PrototypeRelicRarity.Common] =
                PrototypeNativeRelicGrabBag.EligiblePullIds(
                    bag, PrototypeRelicRarity.Common, merchant: false,
                    player: run.Player)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            [PrototypeRelicRarity.Uncommon] =
                PrototypeNativeRelicGrabBag.EligiblePullIds(
                    bag, PrototypeRelicRarity.Uncommon, merchant: false,
                    player: run.Player)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            [PrototypeRelicRarity.Rare] =
                PrototypeNativeRelicGrabBag.EligiblePullIds(
                    bag, PrototypeRelicRarity.Rare, merchant: false,
                    player: run.Player)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray()
        };
        var rarityBranches =
            PrototypeNativeRelicGrabBag.NativeRarityBranches();
        var resolvedBranches = rarityBranches.Select(branch =>
        {
            var plan = PrototypeNativeRelicGrabBag.PlanPull(
                bag, branch.Rarity, merchant: false, run.Player);
            var candidates = plan.SelectedTier is { } tier
                ? candidatesByTier[tier] : [];
            return (Rarity: branch.Rarity.ToString(),
                BasisPoints: branch.BasisPoints,
                SelectedTier: plan.SelectedTier, Candidates: candidates);
        }).ToArray();

        var denominator = resolvedBranches
            .Where(branch => branch.Candidates.Length > 0)
            .Select(branch => branch.Candidates.Length)
            .Aggregate(1L, (current, count) => LeastCommonMultiple(
                current, count));
        var outcomeWeights = new SortedDictionary<string, long>(
            StringComparer.Ordinal);
        foreach (var branch in resolvedBranches)
        {
            if (branch.Candidates.Length == 0)
            {
                outcomeWeights["no_relic"] = checked(
                    outcomeWeights.GetValueOrDefault("no_relic") +
                    branch.BasisPoints * denominator);
                continue;
            }

            var candidateWeight = checked(branch.BasisPoints *
                (denominator / branch.Candidates.Length));
            foreach (var candidate in branch.Candidates)
                outcomeWeights["relic:" + candidate] = checked(
                    outcomeWeights.GetValueOrDefault("relic:" + candidate) +
                    candidateWeight);
        }

        var outcomes = outcomeWeights.Select(pair =>
            new ChanceLawOutcomeV111(pair.Key, pair.Value)).ToArray();
        var ownerId =
            $"act:{world.Act}:floor:{world.Floor}:treasure:relic_grab_bag";
        var conditioning = new
        {
            siteId, ownerId, ordinal,
            rarityBranches = rarityBranches.Select(branch => new
            {
                rarity = branch.Rarity.ToString(), branch.BasisPoints,
                fallbackTiers = PrototypeNativeRelicGrabBag
                    .PullFallbackTiers(branch.Rarity)
                    .Select(tier => tier.ToString()).ToArray()
            }).ToArray(),
            candidatesByTier = candidatesByTier.OrderBy(
                pair => pair.Key.ToString(), StringComparer.Ordinal)
                .Select(pair => new
                {
                    rarity = pair.Key.ToString(), candidates = pair.Value
                }).ToArray(),
            resolvedBranches = resolvedBranches.Select(branch => new
            {
                branch.Rarity, branch.BasisPoints,
                selectedTier = branch.Candidates.Length == 0
                    ? (string?)null : branch.SelectedTier!.Value.ToString(),
                branch.Candidates
            }).ToArray(),
            denominator
        };
        return CreateNativeBoundaryLaw(
            siteId, "conditional_relic_bag_draw", ownerId, ordinal,
            NativeTreasureRelicConditioningPolicyId,
            CanonicalJson.Sha256(conditioning),
            "native-relic-rarity-and-hidden-queue-permutation-unverified",
            outcomes);
    }

    /// <summary>
    /// Query the rarity float consumed by configured native Treasure entry.
    /// The owner and stream are taken from the actual typed request so a
    /// serialized replay checks the source occurrence exactly.
    /// </summary>
    public static ChanceLawV111 QueryNativeTreasureRarity(
        RunState run, RandomUnitFloatRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(request);
        PrototypeStateInvariants.Validate(run);
        var world = ValidateNativeTreasureQueryBoundary(run);
        request.Validate();
        if (request.SiteId != "relic_grab_bag.rarity.unit_float" ||
            request.StreamId != NativePlayerRngV111.RewardsStreamId ||
            request.OwnerId != PrototypeNativeRelicGrabBag
                .NativeTreasureRarityOwnerId)
            throw new ArgumentException(
                "Native treasure rarity request differs from its entry boundary.");

        var rarityWeights = NativeRelicRarityGridWeights();
        var bag = world.RelicBag ??
            PrototypeNativeRelicGrabBag.CreateNatureUnordered(run.Player);
        var conditioning = new
        {
            request,
            candidatesByTier = new Dictionary<string, string[]>(
                StringComparer.Ordinal)
            {
                ["Common"] = PrototypeNativeRelicGrabBag.EligiblePullIds(
                    bag, PrototypeRelicRarity.Common, merchant: false,
                    run.Player).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                ["Uncommon"] = PrototypeNativeRelicGrabBag.EligiblePullIds(
                    bag, PrototypeRelicRarity.Uncommon, merchant: false,
                    run.Player).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                ["Rare"] = PrototypeNativeRelicGrabBag.EligiblePullIds(
                    bag, PrototypeRelicRarity.Rare, merchant: false,
                    run.Player).OrderBy(id => id, StringComparer.Ordinal).ToArray()
            },
            branches = PrototypeNativeRelicGrabBag.NativeRarityBranches()
                .Select(branch => new
                {
                    rarity = branch.Rarity.ToString(), branch.BasisPoints,
                    gridWeight = branch.Rarity switch
                    {
                        PrototypeRelicRarity.Common => rarityWeights.Common,
                        PrototypeRelicRarity.Uncommon => rarityWeights.Uncommon,
                        PrototypeRelicRarity.Rare => rarityWeights.Rare,
                        _ => throw new ArgumentOutOfRangeException()
                    },
                    fallbackTiers = PrototypeNativeRelicGrabBag
                        .PullFallbackTiers(branch.Rarity)
                        .Select(tier => tier.ToString()).ToArray()
                }).ToArray()
        };
        var resolvedBranches = PrototypeNativeRelicGrabBag
            .NativeRarityBranches().Select(branch =>
            {
                var plan = PrototypeNativeRelicGrabBag.PlanPull(
                    bag, branch.Rarity, merchant: false, run.Player);
                return (Branch: branch, Plan: plan);
            }).ToArray();
        var outcomeWeights = new SortedDictionary<string, long>(
            StringComparer.Ordinal);
        foreach (var (branch, plan) in resolvedBranches)
        {
            var outcomeId = plan.CandidateIds.Length == 0
                ? "no_relic"
                : "rarity:" + branch.Rarity.ToString().ToLowerInvariant();
            var weight = branch.Rarity switch
            {
                PrototypeRelicRarity.Common => rarityWeights.Common,
                PrototypeRelicRarity.Uncommon => rarityWeights.Uncommon,
                PrototypeRelicRarity.Rare => rarityWeights.Rare,
                _ => throw new ArgumentOutOfRangeException()
            };
            outcomeWeights[outcomeId] = checked(
                outcomeWeights.GetValueOrDefault(outcomeId) +
                weight);
        }
        var outcomes = outcomeWeights.Select(pair =>
            new ChanceLawOutcomeV111(pair.Key, pair.Value)).ToArray();
        return CreateNativeBoundaryLaw(
            request.SiteId, "unit_float_threshold", request.OwnerId,
            request.Ordinal, "native-treasure-rarity-threshold-v1",
            CanonicalJson.Sha256(conditioning),
            NativeUnitFloatThresholdSourceVerifiedProvenance,
            outcomes, ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query a Treasure relic identity after its rarity float has been
    /// selected. Candidate identities are canonicalized so private queue
    /// order is absent from both the request and the Nature projection.
    /// </summary>
    public static ChanceLawV111 QueryNativeTreasureRelicSelection(
        RunState run, RandomIndexRequestV111 request,
        PrototypeRelicRarity rarity, string[] candidateIds)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateIds);
        PrototypeStateInvariants.Validate(run);
        var world = ValidateNativeTreasureQueryBoundary(run);
        request.Validate();
        if (request.SiteId != PrototypeNativeRelicGrabBag
                .NativeTreasureModelSiteId ||
            request.StreamId != NativePlayerRngV111.RewardsStreamId ||
            request.OperationKind != "choose_index" ||
            !Enum.IsDefined(rarity) || candidateIds.Length == 0 ||
            request.SupportCount != candidateIds.Length ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, candidateIds.Length)) ||
            candidateIds.Any(string.IsNullOrWhiteSpace) ||
            candidateIds.Distinct(StringComparer.Ordinal).Count() !=
                candidateIds.Length ||
            !candidateIds.SequenceEqual(candidateIds.OrderBy(
                id => id, StringComparer.Ordinal), StringComparer.Ordinal))
            throw new ArgumentException(
                "Native treasure relic selection request is malformed.");

        var bag = world.RelicBag ??
            PrototypeNativeRelicGrabBag.CreateNatureUnordered(run.Player);
        var plan = PrototypeNativeRelicGrabBag.PlanPull(
            bag, rarity, merchant: false, run.Player);
        var expectedCandidates = plan.CandidateIds.OrderBy(
            id => id, StringComparer.Ordinal).ToArray();
        var ownerId = PrototypeNativeRelicGrabBag.TreasureModelOwnerId(
            world, rarity, candidateIds);
        if (plan.SelectedTier is null || request.OwnerId != ownerId ||
            !candidateIds.SequenceEqual(expectedCandidates,
                StringComparer.Ordinal))
            throw new InvalidDataException(
                "Native treasure relic support differs from its conditioned hidden queue.");

        var conditioning = new
        {
            request,
            requestedRarity = rarity.ToString(),
            selectedTier = plan.SelectedTier.Value.ToString(),
            fallbackTiers = PrototypeNativeRelicGrabBag
                .PullFallbackTiers(rarity).Select(tier => tier.ToString())
                .ToArray(),
            candidateIds
        };
        var outcomes = candidateIds.Select((id, index) =>
            new ChanceLawOutcomeV111(
                $"relic_model:{id}:index:{index}", 1L)).ToArray();
        return CreateNativeBoundaryLaw(
            request.SiteId, request.OperationKind, request.OwnerId,
            request.Ordinal, "uniform-native-treasure-relic-identity-v1",
            CanonicalJson.Sha256(conditioning),
            "native-treasure-hidden-relic-queue-identity-unverified",
            outcomes);
    }

    private static RunWorldState ValidateNativeTreasureQueryBoundary(
        RunState run)
    {
        if (run.Phase != RunPhase.MapChoice || run.World is not { } world ||
            run.NativeRunRng is null || run.ControlledRunActionChoice is not null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            !PrototypeNativeWingedBoots.AvailableNodes(world, run.Player)
                .Any(node => node.RoomType == PrototypeRoomType.Treasure) ||
            PrototypeGameEngine.WillSkipNextTreasure(run.Player))
            throw new InvalidOperationException(
                "Native treasure Chance Laws require a non-skipped configured Treasure node before entry.");
        return world;
    }

    private static long LeastCommonMultiple(long left, long right)
    {
        static long GreatestCommonDivisor(long a, long b)
        {
            while (b != 0)
                (a, b) = (b, a % b);
            return a;
        }

        return checked(left / GreatestCommonDivisor(left, right) * right);
    }

    /// <summary>
    /// Counts exact 53-bit NextDouble grid preimages for a monotone integer
    /// mapping of the source-rounded unit NextFloat. Binary search locates
    /// each integer outcome's contiguous grid interval.
    /// </summary>
    private static SortedDictionary<int, long>
        NativeNextFloatMappedIntegerWeights(Func<float, int> mapOutcome)
    {
        ArgumentNullException.ThrowIfNull(mapOutcome);
        var weights = new SortedDictionary<int, long>();
        long firstGridIndex = 0;
        long totalWeight = 0;
        while (firstGridIndex < NativeNextDoubleGridSize)
        {
            var mappedOutcome = mapOutcome(
                NativeUnitFloatGridValue(firstGridIndex));
            var low = firstGridIndex + 1;
            var high = NativeNextDoubleGridSize;
            while (low < high)
            {
                var middle = low + ((high - low) >> 1);
                var candidate = mapOutcome(
                    NativeUnitFloatGridValue(middle));
                if (candidate > mappedOutcome)
                    high = middle;
                else
                    low = middle + 1;
            }

            var endGridIndex = low;
            var weight = checked(endGridIndex - firstGridIndex);
            if (weight <= 0 ||
                weights.Count > 0 && weights.Last().Key >= mappedOutcome)
                throw new InvalidDataException(
                    "Native unit-float outcome mapping is not monotone.");
            weights.Add(mappedOutcome, weight);
            totalWeight = checked(totalWeight + weight);
            firstGridIndex = endGridIndex;
        }

        if (weights.Count == 0 ||
            totalWeight != NativeNextDoubleGridSize)
            throw new InvalidOperationException(
                "Native unit-float outcome masses do not cover the input grid.");
        return weights;
    }

    private static ChanceLawV111 CreateNativeBoundaryLaw(
        string siteId, string operationKind, string ownerId, int ordinal,
        string policy, string conditioningFingerprint,
        string provenance, ChanceLawOutcomeV111[] outcomes,
        string probabilityStatus = ChanceLawV111.UnverifiedStatus)
    {
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ChanceLawV111.CurrentSchemaId,
            siteId, ownerId, ordinal, conditioningFingerprint
        });
        return Create(eventId, siteId, operationKind, ownerId, ordinal,
            policy, conditioningFingerprint, provenance, outcomes,
            probabilityStatus);
    }

    private static void ValidateNativeMerchantQueryBoundary(RunState run)
    {
        if (run.Phase != RunPhase.MapChoice || run.World is not { } world ||
            run.NativeRunRng is null || run.ControlledRunActionChoice is not null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            !PrototypeNativeWingedBoots.AvailableNodes(world, run.Player)
                .Any(node => node.RoomType == PrototypeRoomType.Shop))
            throw new InvalidOperationException(
                "Native merchant Chance Laws require an available configured Shop node before entry.");
    }

    private static PrototypeCardType ParseMerchantCardType(string? value)
    {
        if (!Enum.TryParse<PrototypeCardType>(value, ignoreCase: true,
                out var parsed) || !Enum.IsDefined(parsed) ||
            parsed is not (PrototypeCardType.Attack or PrototypeCardType.Skill or
                PrototypeCardType.Power))
            throw new ArgumentException(
                "card_type must be Attack, Skill, or Power.", nameof(value));
        return parsed;
    }

    private static PrototypeCardRarity ParseCardRarity(string? value)
    {
        if (!Enum.TryParse<PrototypeCardRarity>(value, ignoreCase: true,
                out var parsed) || !Enum.IsDefined(parsed) ||
            parsed is not (PrototypeCardRarity.Common or
                PrototypeCardRarity.Uncommon or PrototypeCardRarity.Rare))
            throw new ArgumentException(
                "rarity must be Common, Uncommon, or Rare.", nameof(value));
        return parsed;
    }

    private static PrototypeNativePotionRarity ParsePotionRarity(string value)
    {
        if (!Enum.TryParse<PrototypeNativePotionRarity>(value, ignoreCase: true,
                out var parsed) || !Enum.IsDefined(parsed))
            throw new ArgumentException(
                "Potion rarity must be Common, Uncommon, or Rare.", nameof(value));
        return parsed;
    }

    private static int MerchantCardCount(PrototypeCardType type) => type switch
    {
        PrototypeCardType.Attack or PrototypeCardType.Skill => 2,
        PrototypeCardType.Power => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static int AncientOfferDecisionCount(RunState run, string siteId)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        if (IsChoiceAncientOfferSite(siteId))
        {
            ValidateAncientOfferOpeningBoundary(run, siteId);
            return 3;
        }
        if (siteId == NativeDarvEventV111.OfferVariantSiteId)
        {
            NativeDarvEventV111.ValidateOpeningBoundary(run);
            return checked(1 + DarvOfferShuffleRequests(run).Length);
        }
        ValidateAncientOfferOpeningBoundary(run, siteId);
        if (siteId == NativeDarvEventV111.RelicShuffleSiteId)
            return DarvOfferShuffleRequests(run).Length;
        var pools = AncientOfferShuffleChoicePools(run, siteId);
        return AncientOfferShuffleRequests(siteId, pools).Length;
    }

    internal static bool IsSupportedAncientOfferSite(string siteId) =>
        IsChoiceAncientOfferSite(siteId) || IsShuffleAncientOfferSite(siteId) ||
        siteId == NativeDarvEventV111.OfferVariantSiteId;

    public static bool IsSupportedAncientOfferQuerySite(string siteId) =>
        IsSupportedAncientOfferSite(siteId);

    internal static bool IsSupportedAncientOfferSessionSite(string siteId) =>
        siteId != NativeDarvEventV111.RelicShuffleSiteId &&
        IsSupportedAncientOfferSite(siteId);

    public static bool IsShuffleAncientOfferSite(string siteId) =>
        siteId is NativeNonupeipeEventV111.ShuffleSiteId or
            NativeTanxEventV111.ShuffleSiteId or
            NativeVakuuEventV111.ShuffleSiteId or
            NativeDarvEventV111.RelicShuffleSiteId;

    internal static ChanceLawV111 QueryAncientOfferSessionStep(
        RunState run, string sessionSiteId,
        ReferenceNatureDecisionV111[] prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (sessionSiteId == NativeDarvEventV111.OfferVariantSiteId)
        {
            if (prefix.Length == 0)
                return QueryDarvOfferVariant(run, 0, null);
            return QueryDarvRelicShuffle(run, prefix.Length - 1,
                prefix.Skip(1).Select(decision => decision.OutcomeIndex)
                    .ToArray(),
                prefix[0].OutcomeIndex);
        }
        var priorIndices = IsShuffleAncientOfferSite(sessionSiteId)
            ? prefix.Select(decision => decision.OutcomeIndex).ToArray()
            : null;
        return QueryAncientOffer(run, sessionSiteId, prefix.Length,
            priorIndices);
    }

    internal static void ValidateAncientOfferOpeningBoundary(
        RunState run, string siteId)
    {
        if (siteId is NativeDarvEventV111.OfferVariantSiteId or
            NativeDarvEventV111.RelicShuffleSiteId)
            NativeDarvEventV111.ValidateOpeningBoundary(run);
        else if (siteId == NativeOrobasEventV111.OfferSiteId)
            NativeOrobasEventV111.ValidateOpeningBoundary(run);
        else if (siteId == NativePaelRewardsV111.OfferSiteId)
            NativePaelRewardsV111.ValidateOpeningBoundary(run);
        else if (siteId == NativeAncientRewardsV111.TezcataraOfferSiteId)
            NativeAncientRewardsV111.ValidateTezcataraOpeningBoundary(run);
        else if (siteId == NativeNonupeipeEventV111.ShuffleSiteId)
            NativeNonupeipeEventV111.ValidateOpeningBoundary(run);
        else if (siteId == NativeTanxEventV111.ShuffleSiteId)
            NativeTanxEventV111.ValidateOpeningBoundary(run);
        else if (siteId == NativeVakuuEventV111.ShuffleSiteId)
            NativeVakuuEventV111.ValidateOpeningBoundary(run);
        else
            throw new NotSupportedException(
                $"Ancient offer query does not support site '{siteId}'.");
    }

    internal static string[][] AncientOfferShuffleChoicePools(
        RunState run, string siteId)
    {
        if (siteId == NativeDarvEventV111.RelicShuffleSiteId)
            return [NativeDarvEventV111.PoolOneRelicIdsForAct(
                    run.World!.Act, run.Player)
                .Select(id => "take_" + id).ToArray()];
        if (siteId == NativeNonupeipeEventV111.ShuffleSiteId)
            return [NativeNonupeipeEventV111.CandidateRelics(
                    NativeNonupeipeEventV111.HasBeautifulBraceletEligibility(
                        run.Player))
                .Select(id => "take_" + id).ToArray()];
        if (siteId == NativeTanxEventV111.ShuffleSiteId)
            return [NativeTanxEventV111.CandidateRelics(
                    NativeTanxEventV111.HasTriBoomerangEligibility(run.Player))
                .Select(id => "take_" + id).ToArray()];
        if (siteId == NativeVakuuEventV111.ShuffleSiteId)
            return NativeVakuuEventV111.RelicPools.Select(pool =>
                pool.Select(id => "take_" + id).ToArray()).ToArray();
        throw new NotSupportedException(
            $"Ancient offer query does not support shuffle site '{siteId}'.");
    }

    internal static AncientOfferShuffleRequestV111[]
        AncientOfferShuffleRequests(string siteId, string[][] pools)
    {
        ArgumentNullException.ThrowIfNull(pools);
        var streamId = siteId switch
        {
            NativeNonupeipeEventV111.ShuffleSiteId =>
                "context.event.NONUPEIPE.player_0",
            NativeTanxEventV111.ShuffleSiteId =>
                "context.event.TANX.player_0",
            NativeVakuuEventV111.ShuffleSiteId =>
                "context.event.VAKUU.player_0",
            _ => throw new NotSupportedException(
                $"Ancient offer query does not support shuffle site '{siteId}'.")
        };
        var ownerId = siteId switch
        {
            NativeNonupeipeEventV111.ShuffleSiteId =>
                "event:NONUPEIPE:act:3:floor:0",
            NativeTanxEventV111.ShuffleSiteId =>
                "event:TANX:act:3:floor:0",
            NativeVakuuEventV111.ShuffleSiteId =>
                "event:VAKUU:act:3:floor:0",
            _ => throw new NotSupportedException(
                $"Ancient offer query does not support shuffle site '{siteId}'.")
        };
        var requests = new List<AncientOfferShuffleRequestV111>();
        for (var poolIndex = 0; poolIndex < pools.Length; poolIndex++)
        {
            var pool = pools[poolIndex];
            for (var end = pool.Length - 1; end > 0; end--)
            {
                var ordinal = requests.Count;
                requests.Add(new AncientOfferShuffleRequestV111(
                    poolIndex, end,
                    new RandomIndexRequestV111(
                        siteId, streamId, ownerId, ordinal, [],
                        OperationKind: "bounded_integer",
                        BoundedCount: end + 1)));
            }
        }
        return requests.ToArray();
    }

    private static ChanceLawV111 QueryAncientOfferShuffle(
        RunState run, string siteId, int ordinal, int[]? priorIndices)
    {
        ValidateAncientOfferOpeningBoundary(run, siteId);
        var initialPools = AncientOfferShuffleChoicePools(run, siteId);
        var requests = AncientOfferShuffleRequests(siteId, initialPools);
        if (ordinal < 0 || ordinal >= requests.Length)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        priorIndices ??= [];
        if (priorIndices.Length != ordinal)
            throw new ArgumentException(
                "Shuffle-based Ancient offer queries require one prior index for every earlier ordinal.",
                nameof(priorIndices));

        var currentPools = initialPools
            .Select(pool => (string[])pool.Clone()).ToArray();
        for (var index = 0; index < ordinal; index++)
        {
            var request = requests[index];
            var selectedIndex = priorIndices[index];
            if (selectedIndex < 0 ||
                selectedIndex >= request.Request.SupportCount)
                throw new ArgumentOutOfRangeException(nameof(priorIndices),
                    $"Prior swap index {index} is outside its saved support.");
            (currentPools[request.PoolIndex][request.EndIndex],
                currentPools[request.PoolIndex][selectedIndex]) =
                (currentPools[request.PoolIndex][selectedIndex],
                    currentPools[request.PoolIndex][request.EndIndex]);
        }

        var selectedRequest = requests[ordinal].Request;
        var ownerId = selectedRequest.OwnerId;
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            InitialChoicePools = initialPools,
            CurrentChoicePools = currentPools,
            PriorIndices = priorIndices,
            RequestFingerprint = CanonicalJson.Sha256(selectedRequest),
            policy = AncientOfferShuffleConditioningPolicyId
        });
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ChanceLawV111.CurrentSchemaId,
            siteId,
            ownerId,
            ordinal,
            conditioningFingerprint
        });
        var weights = NativeNextIntGridWeights(
            selectedRequest.SupportCount);
        var outcomes = Enumerable.Range(0, selectedRequest.SupportCount)
            .Select(index => new ChanceLawOutcomeV111(
                "swap_index:" + index.ToString(CultureInfo.InvariantCulture),
                weights[index])).ToArray();
        return Create(eventId, siteId, selectedRequest.OperationKind,
            ownerId, ordinal, AncientOfferShuffleConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    private static ChanceLawV111 QueryDarvOfferVariant(
        RunState run, int ordinal, int[]? priorIndices)
    {
        NativeDarvEventV111.ValidateOpeningBoundary(run);
        if (ordinal != 0 || priorIndices is { Length: > 0 })
            throw new InvalidOperationException(
                "Darv offer-variant law supports only ordinal zero without a prefix.");
        var request = DarvOfferVariantRequest(run);
        var ownerId = request.OwnerId;
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            act = run.World!.Act,
            request,
            outcomes = new[] { "offer_variant:dusty_tome",
                "offer_variant:three_relics" },
            policy = DarvOfferVariantConditioningPolicyId
        });
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ChanceLawV111.CurrentSchemaId,
            siteId = request.SiteId,
            ownerId,
            ordinal,
            conditioningFingerprint
        });
        var weights = NativeNextIntGridWeights(request.SupportCount);
        var outcomes = new[]
        {
            new ChanceLawOutcomeV111("offer_variant:dusty_tome", weights[0]),
            new ChanceLawOutcomeV111("offer_variant:three_relics", weights[1])
        };
        return Create(eventId, request.SiteId, request.OperationKind,
            ownerId, ordinal, DarvOfferVariantConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    private static ChanceLawV111 QueryDarvRelicShuffle(
        RunState run, int ordinal, int[]? priorIndices,
        int? priorVariantIndex)
    {
        NativeDarvEventV111.ValidateOpeningBoundary(run);
        var requests = DarvOfferShuffleRequests(run);
        if (ordinal < 0 || ordinal >= requests.Length)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        priorIndices ??= [];
        if (priorIndices.Length != ordinal)
            throw new ArgumentException(
                "Darv shuffle queries require one prior index per earlier swap.",
                nameof(priorIndices));
        if (priorVariantIndex is { } variant && variant is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(priorVariantIndex));

        var initial = NativeDarvEventV111.PoolOneRelicIdsForAct(
            run.World!.Act, run.Player);
        var current = (string[])initial.Clone();
        for (var index = 0; index < ordinal; index++)
        {
            var selectedIndex = priorIndices[index];
            if (selectedIndex < 0 ||
                selectedIndex >= requests[index].SupportCount)
                throw new ArgumentOutOfRangeException(nameof(priorIndices),
                    $"Prior Darv swap index {index} is outside its support.");
            var end = initial.Length - index - 1;
            (current[end], current[selectedIndex]) =
                (current[selectedIndex], current[end]);
        }

        var selectedRequest = requests[ordinal];
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            act = run.World.Act,
            initial,
            current,
            priorVariantIndex,
            priorIndices,
            request = CanonicalJson.Sha256(selectedRequest),
            policy = AncientOfferShuffleConditioningPolicyId
        });
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ChanceLawV111.CurrentSchemaId,
            siteId = selectedRequest.SiteId,
            ownerId = selectedRequest.OwnerId,
            ordinal,
            conditioningFingerprint
        });
        var weights = NativeNextIntGridWeights(
            selectedRequest.SupportCount);
        var outcomes = Enumerable.Range(0, selectedRequest.SupportCount)
            .Select(index => new ChanceLawOutcomeV111(
                "swap_index:" + index.ToString(CultureInfo.InvariantCulture),
                weights[index])).ToArray();
        return Create(eventId, selectedRequest.SiteId,
            selectedRequest.OperationKind, selectedRequest.OwnerId,
            ordinal, AncientOfferShuffleConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    internal static RandomIndexRequestV111 DarvOfferVariantRequest(
        RunState run)
    {
        NativeDarvEventV111.ValidateOpeningBoundary(run);
        var streamId = NativeRngV111Contexts.ForEvent(
            run.RunSeed, NativeDarvEventV111.NativeRngEventId).StreamId;
        var ownerId = $"event:DARV:act:{run.World!.Act}:floor:0";
        var request = new RandomIndexRequestV111(
            NativeDarvEventV111.OfferVariantSiteId,
            streamId, ownerId, 0, [],
            OperationKind: "bounded_integer", BoundedCount: 2);
        request.Validate();
        return request;
    }

    internal static RandomIndexRequestV111[] DarvOfferShuffleRequests(
        RunState run)
    {
        NativeDarvEventV111.ValidateOpeningBoundary(run);
        var relics = NativeDarvEventV111.PoolOneRelicIdsForAct(
            run.World!.Act, run.Player);
        var streamId = NativeRngV111Contexts.ForEvent(
            run.RunSeed, NativeDarvEventV111.NativeRngEventId).StreamId;
        var ownerId = $"event:DARV:act:{run.World.Act}:floor:0";
        var requests = new List<RandomIndexRequestV111>();
        for (var end = relics.Length - 1; end > 0; end--)
        {
            var request = new RandomIndexRequestV111(
                NativeDarvEventV111.RelicShuffleSiteId,
                streamId, ownerId, requests.Count, [],
                OperationKind: "bounded_integer", BoundedCount: end + 1);
            request.Validate();
            requests.Add(request);
        }
        return requests.ToArray();
    }

    private static bool IsChoiceAncientOfferSite(string siteId) =>
        siteId is NativeOrobasEventV111.OfferSiteId or
            NativePaelRewardsV111.OfferSiteId or
            NativeAncientRewardsV111.TezcataraOfferSiteId;

    /// <summary>Query a controlled legacy run-action request without exposing
    /// the selected result. The event identity binds the action and request
    /// context without incorporating private run-provider state.</summary>
    public static ChanceLawV111 QueryControlledRunAction(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var continuation = run.ControlledRunActionChoice ??
            throw new InvalidOperationException(
                "No controlled run-action chance is pending.");
        continuation.Validate();
        var boundaryFingerprint = ControlledRunActionBoundaryFingerprint(run);
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ControlledRunActionEngineV111.SchemaId,
            boundaryFingerprint
        });
        return QueryRandomIndex(eventId, continuation.PendingRequest);
    }

    /// <summary>Fingerprint the pending controlled action and request without
    /// hashing the enclosing RunState (which contains private provider state).</summary>
    public static string ControlledRunActionBoundaryFingerprint(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var continuation = run.ControlledRunActionChoice ??
            throw new InvalidOperationException(
                "No controlled run-action chance is pending.");
        continuation.Validate();
        var boundary = new
        {
            schemaId = ControlledRunActionEngineV111.SchemaId,
            continuation.ActionId,
            continuation.MapNodeId,
            request = continuation.PendingRequest,
            candidateLabels = continuation.PendingCandidateLabels
        };
        return continuation.RunAction is null
            ? CanonicalJson.Sha256(boundary)
            : CanonicalJson.Sha256(new
            {
                boundary,
                actionFingerprint = PrototypeAiEnvironment.StableActionId(
                    continuation.RunAction)
            });
    }

    public static ChanceLawV111 QueryControlledEventChoice(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var description = DescribeControlledEventChoice(run);
        if (run.World?.Event?.EventId ==
                PrototypeNativeStoneOfAllTime.EventId &&
            run.World?.Event?.ControlledChoice?.ChoiceId is "push" or "lift")
            return QueryControlledStoneOfAllTimeTicket(
                run, description.Request,
                description.BoundaryFingerprint);
        return QueryRandomIndex(description.EventId, description.Request);
    }

    private static ChanceLawV111 QueryControlledStoneOfAllTimeTicket(
        RunState run, RandomIndexRequestV111 request,
        string boundaryFingerprint)
    {
        var continuation = run.World?.Event?.ControlledChoice ??
            throw new InvalidOperationException(
                "Stone of All Time ticket law requires a pending controlled event choice.");
        continuation.Validate();
        var choiceId = continuation.ChoiceId;
        var expectedSiteId = choiceId switch
        {
            "push" => NativeStoneOfAllTimePushTicketSiteId,
            "lift" => NativeStoneOfAllTimeLiftTicketSiteId,
            _ => throw new InvalidOperationException(
                "Stone of All Time controlled choice has no ticket law.")
        };
        var partialState = continuation.PartialState;
        var partialWorld = partialState.World;
        var partialEvent = partialWorld?.Event ??
            throw new InvalidOperationException(
                "Pending Stone of All Time ticket has no event state.");
        var nativeRoute = partialState.NativeRunRng is not null;
        var expectedStreamId = nativeRoute
            ? NativeRngV111Contexts.EventStreamId(
                PrototypeNativeStoneOfAllTime.NativeRngEventId)
            : "event";
        if (continuation.EventId != PrototypeNativeStoneOfAllTime.EventId ||
            request.SiteId != expectedSiteId ||
            request.StreamId != expectedStreamId ||
            request.OwnerId != $"event:stone_of_all_time:{choiceId}" ||
            (nativeRoute && request.Ordinal != 1) ||
            request.OperationKind != "bounded_integer" ||
            request.BoundedCount != 100)
            throw new InvalidOperationException(
                "Pending Stone of All Time ticket request disagrees with its source boundary.");
        var nativeRouteMismatch = nativeRoute &&
            (partialWorld is not { Act: 2 } ||
             !PrototypeGameEngine.UsesNativeRegionalSystems(partialWorld!));
        var legacyRouteMismatch = !nativeRoute &&
            (partialWorld is not { Act: 2 } ||
             PrototypeGameEngine.UsesNativeRegionalSystems(partialWorld!));
        var eventRngMismatch = nativeRoute
            ? partialEvent.NativeEventRng?.StreamId != expectedStreamId ||
                partialEvent.NativeEventRngProviderMode is not null
            : partialEvent.NativeEventRng is not null ||
                partialEvent.NativeEventRngProviderMode is not null;
        if (partialState.Phase != RunPhase.Event ||
            partialWorld is not { Act: 2 } ||
            partialEvent.EventId != PrototypeNativeStoneOfAllTime.EventId ||
            partialEvent.NativePageIndex != 0 ||
            eventRngMismatch ||
            nativeRouteMismatch || legacyRouteMismatch ||
            (choiceId == "lift" && partialEvent.ChosenChoiceId is not null) ||
            (choiceId == "push" && partialEvent.ChosenChoiceId != "push"))
            throw new InvalidOperationException(
                "Pending Stone of All Time ticket is outside its Native or legacy event-action boundary.");
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            boundary = "reference_nature.controlled_event.stone_of_all_time.ticket",
            boundaryFingerprint,
            eventId = partialEvent.EventId,
            actionId = choiceId,
            providerRoute = nativeRoute ? "native" : "legacy",
            request
        });
        return CreateNativeBoundaryLaw(
            request.SiteId, "bounded_integer_no_effect", request.OwnerId,
            request.Ordinal,
            nativeRoute
                ? NativeStoneOfAllTimeNoEffectPolicyId
                : LegacyStoneOfAllTimeNoEffectPolicyId,
            conditioningFingerprint,
            nativeRoute
                ? NativeStoneOfAllTimeNoEffectProvenance
                : LegacyStoneOfAllTimeNoEffectProvenance,
            [new ChanceLawOutcomeV111("no_effect", 1L)],
            ChanceLawV111.SourceVerifiedStatus);
    }

    public static string ControlledEventChoiceBoundaryFingerprint(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return DescribeControlledEventChoice(run).BoundaryFingerprint;
    }

    private static (string EventId, string ChoiceId,
        RandomIndexRequestV111 Request, string[] CandidateLabels,
        string BoundaryFingerprint) DescribeControlledEventChoice(RunState run)
    {
        var effect = run.World?.Event?.ControlledChoice;
        var reward = run.ControlledEventRewardChoice;
        if ((effect is null) == (reward is null))
            throw new InvalidOperationException(
                "A controlled event-choice law requires exactly one pending request.");
        effect?.Validate();
        reward?.Validate();
        var eventId = effect?.EventId ?? reward!.EventId;
        var choiceId = effect?.ChoiceId ?? reward!.ChoiceId;
        var request = effect?.PendingRequest ?? reward!.PendingRequest;
        var candidateLabels = effect?.PendingCandidateLabels ??
            reward!.PendingCandidateLabels;
        var boundaryFingerprint = CanonicalJson.Sha256(new
        {
            schemaId = ControlledEventChoiceEngineV111.SchemaId,
            kind = effect is null ? "event_reward_random_choice" :
                "event_random_choice",
            eventId,
            choiceId,
            nextEffectIndex = effect?.NextEffectIndex,
            decisionPrefixLength = reward?.DecisionPrefixLength,
            request,
            candidateLabels
        });
        var chanceEventId = CanonicalJson.Sha256(new
        {
            schemaId = ControlledEventChoiceEngineV111.SchemaId,
            boundaryFingerprint
        });
        return (chanceEventId, choiceId, request, candidateLabels,
            boundaryFingerprint);
    }

    /// <summary>
    /// Query the uniform conditional law over the live physical enemy
    /// identities at a suspended random-target boundary. The selected index
    /// is not part of the event, so querying cannot depend on a prior result.
    /// </summary>
    public static ChanceLawV111 QueryTarget(SemanticTargetEventV111 target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Validate();
        var outcomes = target.CandidateEnemyInstanceIds
            .Select(instanceId => new ChanceLawOutcomeV111(
                "enemy_instance:" + instanceId.ToString(
                    CultureInfo.InvariantCulture), 1L))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            target.SupportFingerprint,
            target.SiteId,
            target.OwnerId,
            target.Ordinal,
            target.TargetKind,
            policy = TargetConditioningPolicyId
        });
        return Create(target.EventId, target.SiteId, target.OperationKind,
            target.OwnerId, target.Ordinal, TargetConditioningPolicyId,
            conditioningFingerprint,
            "uniform-live-source-support-unverified", outcomes);
    }

    /// <summary>
    /// Query the uniform law over the ordered, duplicate-free generated-card
    /// model pool saved at a source effect boundary.
    /// </summary>
    public static ChanceLawV111 QueryGeneratedCard(
        SemanticGeneratedCardEventV111 generatedCard)
    {
        ArgumentNullException.ThrowIfNull(generatedCard);
        generatedCard.Validate();
        var outcomes = generatedCard.CandidateCardIds
            .Select(cardId => new ChanceLawOutcomeV111(
                "card_model:" + cardId, 1L))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            generatedCard.SupportFingerprint,
            generatedCard.SiteId,
            generatedCard.OwnerId,
            generatedCard.Ordinal,
            policy = GeneratedCardConditioningPolicyId
        });
        return Create(generatedCard.EventId, generatedCard.SiteId,
            generatedCard.OperationKind, generatedCard.OwnerId,
            generatedCard.Ordinal, GeneratedCardConditioningPolicyId,
            conditioningFingerprint,
            "uniform-ordered-model-pool-unverified", outcomes);
    }

    /// <summary>
    /// Query the uniform bounded-integer law declared by a pending semantic
    /// cost event. Card identity and source stage remain in its conditioning
    /// fingerprint through the event's support fingerprint.
    /// </summary>
    public static ChanceLawV111 QueryCost(SemanticCostEventV111 cost)
    {
        ArgumentNullException.ThrowIfNull(cost);
        cost.Validate();
        var outcomes = cost.CandidateCosts
            .Select(value => new ChanceLawOutcomeV111(
                "cost:" + value.ToString(CultureInfo.InvariantCulture), 1L))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            cost.SupportFingerprint,
            cost.SiteId,
            cost.OwnerId,
            cost.Ordinal,
            policy = CostConditioningPolicyId
        });
        return Create(cost.EventId, cost.SiteId, cost.OperationKind,
            cost.OwnerId, cost.Ordinal, CostConditioningPolicyId,
            conditioningFingerprint,
            "uniform-bounded-cost-unverified", outcomes);
    }

    /// <summary>
    /// Query the uniform law over physical card instances in the ordered
    /// source support for a random pile-card effect.
    /// </summary>
    public static ChanceLawV111 QueryRandomPileCard(
        SemanticRandomPileCardEventV111 pileCard)
    {
        ArgumentNullException.ThrowIfNull(pileCard);
        pileCard.Validate();
        var outcomes = pileCard.CandidateCardInstanceIds
            .Select(instanceId => new ChanceLawOutcomeV111(
                "card_instance:" + instanceId.ToString(
                    CultureInfo.InvariantCulture), 1L))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            pileCard.SupportFingerprint,
            pileCard.SiteId,
            pileCard.OwnerId,
            pileCard.Ordinal,
            policy = RandomPileCardConditioningPolicyId
        });
        return Create(pileCard.EventId, pileCard.SiteId,
            pileCard.OperationKind, pileCard.OwnerId, pileCard.Ordinal,
            RandomPileCardConditioningPolicyId, conditioningFingerprint,
            "uniform-physical-pile-card-support-unverified", outcomes);
    }

    /// <summary>
    /// Query the uniform law over replacement models for one saved physical
    /// card transform. The source card identity and model are conditioned on.
    /// </summary>
    public static ChanceLawV111 QueryCardTransform(
        SemanticCardTransformEventV111 transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        transform.Validate();
        var outcomes = transform.CandidateCardIds
            .Select(cardId => new ChanceLawOutcomeV111(
                "card_model:" + cardId, 1L))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            transform.SupportFingerprint,
            transform.SiteId,
            transform.OwnerId,
            transform.Ordinal,
            transform.SourceCardInstanceId,
            transform.OriginalCardId,
            policy = CardTransformConditioningPolicyId
        });
        return Create(transform.EventId, transform.SiteId,
            transform.OperationKind, transform.OwnerId, transform.Ordinal,
            CardTransformConditioningPolicyId, conditioningFingerprint,
            "uniform-transform-pool-unverified", outcomes);
    }

    /// <summary>
    /// Query the uniform law over the ordered source potion-model pool for
    /// one pending generated-potion choice.
    /// </summary>
    public static ChanceLawV111 QueryPotionGeneration(
        SemanticPotionGenerationEventV111 potion)
    {
        ArgumentNullException.ThrowIfNull(potion);
        potion.Validate();
        var outcomes = potion.CandidatePotionIds
            .Select(potionId => new ChanceLawOutcomeV111(
                "potion_model:" + potionId, 1L))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            potion.SupportFingerprint,
            potion.SiteId,
            potion.OwnerId,
            potion.Ordinal,
            policy = PotionGenerationConditioningPolicyId
        });
        return Create(potion.EventId, potion.SiteId,
            potion.OperationKind, potion.OwnerId, potion.Ordinal,
            PotionGenerationConditioningPolicyId, conditioningFingerprint,
            "uniform-potion-model-pool-unverified", outcomes);
    }

    /// <summary>
    /// Query the uniform law over eligible physical hand-card identities for
    /// a pending fixed-cost operation.
    /// </summary>
    public static ChanceLawV111 QueryHandCostSelection(
        SemanticHandCostSelectionEventV111 selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        selection.Validate();
        var outcomes = selection.CandidateCardInstanceIds
            .Select(instanceId => new ChanceLawOutcomeV111(
                "card_instance:" + instanceId.ToString(
                    CultureInfo.InvariantCulture), 1L))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            selection.SupportFingerprint,
            selection.SiteId,
            selection.OwnerId,
            selection.Ordinal,
            selection.AppliedCost,
            policy = HandCostSelectionConditioningPolicyId
        });
        return Create(selection.EventId, selection.SiteId,
            selection.OperationKind, selection.OwnerId, selection.Ordinal,
            HandCostSelectionConditioningPolicyId, conditioningFingerprint,
            "uniform-hand-card-support-unverified", outcomes);
    }

    /// <summary>
    /// Query one uniform Fisher–Yates swap in a saved without-replacement
    /// pool shuffle. Prior swaps remain part of the conditioned state.
    /// </summary>
    public static ChanceLawV111 QueryPoolShuffle(
        SemanticPoolShuffleEventV111 shuffle)
    {
        ArgumentNullException.ThrowIfNull(shuffle);
        shuffle.Validate();
        var outcomes = Enumerable.Range(0, shuffle.Request.SupportCount)
            .Select(index => new ChanceLawOutcomeV111(
                "swap_index:" + index.ToString(CultureInfo.InvariantCulture),
                1L))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            shuffle.SupportFingerprint,
            shuffle.SiteId,
            shuffle.OwnerId,
            shuffle.Ordinal,
            shuffle.NextSwapIndex,
            shuffle.SelectedCount,
            policy = PoolShuffleConditioningPolicyId
        });
        return Create(shuffle.EventId, shuffle.SiteId,
            shuffle.OperationKind, shuffle.OwnerId, shuffle.Ordinal,
            PoolShuffleConditioningPolicyId, conditioningFingerprint,
            "uniform-fisher-yates-swap-unverified", outcomes);
    }

    /// <summary>
    /// Query the uniform bounded insertion position for a generated card.
    /// The eligible physical pile and generated model are in the saved
    /// support fingerprint.
    /// </summary>
    public static ChanceLawV111 QueryDrawInsertion(
        SemanticDrawInsertionEventV111 insertion)
    {
        ArgumentNullException.ThrowIfNull(insertion);
        insertion.Validate();
        var outcomes = Enumerable.Range(0, insertion.Request.SupportCount)
            .Select(position => new ChanceLawOutcomeV111(
                "draw_position:" + position.ToString(
                    CultureInfo.InvariantCulture), 1L))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            insertion.SupportFingerprint,
            insertion.SiteId,
            insertion.OwnerId,
            insertion.Ordinal,
            insertion.GeneratedCardModelId,
            policy = DrawInsertionConditioningPolicyId
        });
        return Create(insertion.EventId, insertion.SiteId,
            insertion.OperationKind, insertion.OwnerId, insertion.Ordinal,
            DrawInsertionConditioningPolicyId, conditioningFingerprint,
            "uniform-bounded-insertion-position-unverified", outcomes);
    }

    /// <summary>
    /// Query one conditional pick from a saved without-replacement shortlist.
    /// The remaining physical identities and all prior picks are included in
    /// the event conditioning fingerprint.
    /// </summary>
    public static ChanceLawV111 QueryCardShortlist(
        SemanticCardShortlistEventV111 shortlist)
    {
        ArgumentNullException.ThrowIfNull(shortlist);
        shortlist.Validate();
        var outcomes = shortlist.CandidateCardInstanceIds
            .Select(instanceId => new ChanceLawOutcomeV111(
                "card_instance:" + instanceId.ToString(
                    CultureInfo.InvariantCulture), 1L))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            shortlist.SupportFingerprint,
            shortlist.SiteId,
            shortlist.OwnerId,
            shortlist.Ordinal,
            shortlist.RequiredCount,
            shortlist.SelectedCardInstanceIds,
            policy = CardShortlistConditioningPolicyId
        });
        return Create(shortlist.EventId, shortlist.SiteId,
            shortlist.OperationKind, shortlist.OwnerId, shortlist.Ordinal,
            CardShortlistConditioningPolicyId, conditioningFingerprint,
            "uniform-sequential-card-shortlist-unverified", outcomes);
    }

    public static ChanceLawV111 QueryDraw(SemanticDeckStateV111 deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        if (deck.Mode != SemanticDeckStateV111.NatureMode ||
            deck.Pending is not
            {
                SiteId: "combat.draw_pile.draw_card",
                Purpose: "draw"
            })
            throw new InvalidOperationException(
                "Chance Law draw query requires one unresolved Nature draw_card event.");

        return QueryDeckReveal(deck);
    }

    /// <summary>
    /// Query the conditional identity distribution for a pending draw, peek,
    /// or scry reveal. A known position has no pending event and therefore
    /// cannot be sampled again through this API.
    /// </summary>
    public static ChanceLawV111 QueryDeckReveal(
        SemanticDeckStateV111 deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        SemanticDeckEngineV111.Validate(deck);
        var pending = deck.Pending;
        if (deck.Mode != SemanticDeckStateV111.NatureMode ||
            pending is null ||
            (pending.Purpose, pending.SiteId) is not
                ("draw", "combat.draw_pile.draw_card") and not
                ("peek", "combat.draw_pile.reveal_card") and not
                ("scry", "combat.draw_pile.reveal_card"))
            throw new InvalidOperationException(
                "Chance Law reveal query requires one unresolved Nature deck event.");
        var drawEvent = SemanticDeckEngineV111.Inspect(deck) ??
            throw new InvalidDataException("Pending deck reveal disappeared.");
        var outcomes = pending.CandidateCardInstanceIds
            .Select(cardId => new ChanceLawOutcomeV111(
                "card_instance:" + cardId.ToString(CultureInfo.InvariantCulture), 1L))
            .ToArray();
        var policy = pending.Purpose == "draw"
            ? DrawConditioningPolicyId
            : RevealConditioningPolicyId;
        return Create(drawEvent.EventId, drawEvent.SiteId,
            drawEvent.OperationKind, drawEvent.OwnerId, drawEvent.Ordinal,
            policy, deck.Fingerprint,
            "uniform-deferred-permutation-model-unverified", outcomes);
    }

    private static ChanceLawV111 Create(
        string eventId, string siteId, string operationKind, string ownerId,
        int ordinal, string conditioningPolicyId,
        string conditioningFingerprint, string probabilityProvenance,
        ChanceLawOutcomeV111[] outcomes,
        string probabilityStatus = ChanceLawV111.UnverifiedStatus)
    {
        var totalWeight = outcomes.Aggregate(0L,
            (total, outcome) => checked(total + outcome.Weight));
        var supportFingerprint = CanonicalJson.Sha256(outcomes);
        var lawId = CanonicalJson.Sha256(new
        {
            SchemaId = ChanceLawV111.CurrentSchemaId,
            SiteId = siteId,
            OperationKind = operationKind,
            OwnerId = ownerId,
            Ordinal = ordinal,
            SupportFingerprint = supportFingerprint,
            ConditioningPolicyId = conditioningPolicyId,
            ConditioningFingerprint = conditioningFingerprint,
            ProbabilityStatus = probabilityStatus,
            ProbabilityProvenance = probabilityProvenance
        });
        var law = new ChanceLawV111(
            ChanceLawV111.CurrentSchemaId, lawId, eventId, siteId,
            operationKind, ownerId, ordinal, supportFingerprint,
            conditioningPolicyId, conditioningFingerprint,
            probabilityStatus, probabilityProvenance,
            outcomes, totalWeight);
        law.Validate();
        return law;
    }
}

internal sealed record AncientOfferShuffleRequestV111(
    int PoolIndex, int EndIndex, RandomIndexRequestV111 Request);

/// <summary>Forkable state for the independent ReferenceNature sampler.</summary>
public sealed record ReferenceNatureStateV111(
    string SchemaId, ulong SamplerState, long SampleCount)
{
    public const string CurrentSchemaId = "sts2-v111-reference-nature-state-v1";

    public static ReferenceNatureStateV111 Create(ulong seed) =>
        new(CurrentSchemaId, seed, 0);

    public ReferenceNatureStateV111 Fork() => this;

    public static ReferenceNatureStateV111 ForRunSession(
        RunState run, ulong? seed)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.ReferenceNatureState is { } saved)
        {
            if (seed is not null)
                throw new InvalidOperationException(
                    "This run already owns a ReferenceNature cursor; omit nature_seed to continue it.");
            saved.Validate();
            return saved.Fork();
        }
        if (seed is null)
            throw new InvalidOperationException(
                "A first ReferenceNature session requires nature_seed.");
        return Create(seed.Value);
    }

    public bool SameCursor(ReferenceNatureStateV111? other) =>
        other is not null &&
        CanonicalJson.Sha256(this) == CanonicalJson.Sha256(other);

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || SampleCount < 0)
            throw new InvalidDataException("Invalid ReferenceNature sample count.");
    }
}

/// <summary>
/// Shared lifecycle rules for ReferenceNature sessions attached to a run.
/// Boundary sessions keep their mechanics and transcript types, but cursor
/// initialization, publication, and run-cursor validation have one owner.
/// </summary>
internal static class ReferenceNatureRunCursorV111
{
    public static ReferenceNatureStateV111 Begin(
        RunState source, ulong? seed, string boundary)
    {
        ArgumentNullException.ThrowIfNull(source);
        var cursor = ReferenceNatureStateV111.ForRunSession(source, seed);
        ValidateInitial(source, cursor, boundary);
        return cursor;
    }

    public static void ValidateInitial(
        RunState source,
        ReferenceNatureStateV111 initial,
        string boundary)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(initial);
        if (source.ReferenceNatureState is { } saved &&
            !initial.SameCursor(saved))
            throw new InvalidOperationException(
                $"{boundary} session cannot replace the run's ReferenceNature cursor.");
    }

    public static bool MatchesRunSession(
        RunState origin,
        RunState current,
        ReferenceNatureStateV111 initial,
        ReferenceNatureStateV111 cursor,
        int decisionCount,
        bool complete)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(cursor);

        var initialMatchesOrigin = origin.ReferenceNatureState is not { } saved ||
            initial.SameCursor(saved);
        var pendingRunMatchesOrigin = origin.ReferenceNatureState is { } originCursor
            ? originCursor.SameCursor(current.ReferenceNatureState)
            : current.ReferenceNatureState is null;
        var runMatchesCursor = complete
            ? cursor.SameCursor(current.ReferenceNatureState)
            : pendingRunMatchesOrigin;
        return initialMatchesOrigin && runMatchesCursor &&
            cursor.SampleCount == checked(initial.SampleCount + decisionCount);
    }

    public static RunState Commit(
        RunState run, ReferenceNatureStateV111 cursor)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(cursor);
        cursor.Validate();
        return run with { ReferenceNatureState = cursor.Fork() };
    }
}

public sealed record ReferenceNatureSelectionV111(
    string LawId, string EventId, string OutcomeId, int OutcomeIndex,
    ReferenceNatureStateV111 NextState);

public sealed record ReferenceNatureResolutionV111(
    SemanticPilotCombatV111 Combat, ChanceLawV111 Law,
    ReferenceNatureSelectionV111 Selection);

public sealed record ReferenceNatureSessionSnapshotV111(
    string SchemaId, string CombatSnapshotJson,
    ReferenceNatureStateV111 NatureState);

/// <summary>
/// Atomic ReferenceNature simulation state: semantic mechanics continuation
/// and the independent sampler cursor share one fork/save/restore boundary.
/// </summary>
public sealed record ReferenceNatureSessionV111(
    string SchemaId, SemanticPilotCombatV111 Combat,
    ReferenceNatureStateV111 NatureState)
{
    public const string CurrentSchemaId = "sts2-v111-reference-nature-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    public static ReferenceNatureSessionV111 Begin(
        SemanticPilotCombatV111 combat, ulong seed) => Begin(combat, (ulong?)seed);

    public static ReferenceNatureSessionV111 Begin(
        SemanticPilotCombatV111 combat) => Begin(combat, null);

    private static ReferenceNatureSessionV111 Begin(
        SemanticPilotCombatV111 combat, ulong? seed)
    {
        ArgumentNullException.ThrowIfNull(combat);
        var natureState = ReferenceNatureRunCursorV111.Begin(
            combat.Run, seed, "Semantic combat");
        if (combat.CombatTerminal &&
            !natureState.SameCursor(combat.Run.ReferenceNatureState))
        {
            var committedRun = ReferenceNatureRunCursorV111.Commit(
                combat.Run, natureState);
            combat = combat with
            {
                Run = committedRun,
                PendingEndTurn = combat.PendingEndTurn is { } endTurn
                    ? endTurn with { Run = committedRun }
                    : null
            };
        }
        var session = new ReferenceNatureSessionV111(
            CurrentSchemaId, combat.Fork(), natureState);
        session.Validate();
        return session;
    }

    public ReferenceNatureSessionV111 Fork()
    {
        Validate();
        return this with { Combat = Combat.Fork(), NatureState = NatureState.Fork() };
    }

    public ReferenceNatureSessionV111 ResolveNext()
    {
        Validate();
        var resolution = ReferenceNatureV111.ResolvePending(Combat, NatureState);
        var resolvedCombat = resolution.Combat;
        if (resolvedCombat.CombatTerminal)
        {
            var committedRun = ReferenceNatureRunCursorV111.Commit(
                resolvedCombat.Run, resolution.Selection.NextState);
            resolvedCombat = resolvedCombat with
            {
                Run = committedRun,
                PendingEndTurn = resolvedCombat.PendingEndTurn is { } endTurn
                    ? endTurn with { Run = committedRun }
                    : null
            };
        }
        var child = this with
        {
            Combat = resolvedCombat,
            NatureState = resolution.Selection.NextState
        };
        child.Validate();
        return child;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Combat is null || NatureState is null)
            throw new InvalidDataException("Invalid ReferenceNature session envelope.");
        Combat.Validate();
        NatureState.Validate();
        if (Combat.CombatTerminal &&
            !NatureState.SameCursor(Combat.Run.ReferenceNatureState))
            throw new InvalidDataException(
                "ReferenceNature sampler cursor differs from the committed run state.");
    }

    public static string Save(ReferenceNatureSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureSessionSnapshotV111(
            CurrentSchemaId,
            SemanticPilotCombatEngineV111.Save(session.Combat),
            session.NatureState);
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<ReferenceNatureSessionSnapshotV111>(json)
            ?? throw new InvalidDataException("Null ReferenceNature snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.CombatSnapshotJson) ||
            snapshot.NatureState is null)
            throw new InvalidDataException("Invalid ReferenceNature snapshot.");
        var combat = SemanticPilotCombatEngineV111.Restore(
            snapshot.CombatSnapshotJson);
        if (combat.CombatTerminal && combat.Run.ReferenceNatureState is null)
        {
            var committedRun = combat.Run with
            {
                ReferenceNatureState = snapshot.NatureState.Fork()
            };
            combat = combat with
            {
                Run = committedRun,
                PendingEndTurn = combat.PendingEndTurn is { } endTurn
                    ? endTurn with { Run = committedRun }
                    : null
            };
        }
        var restored = new ReferenceNatureSessionV111(
            CurrentSchemaId, combat, snapshot.NatureState);
        restored.Validate();
        return restored;
    }
}

public sealed record ReferenceNatureDecisionV111(
    string LawId, string EventId, string OutcomeId, int OutcomeIndex)
{
    public void Validate()
    {
        var indexText = OutcomeIndex.ToString(CultureInfo.InvariantCulture);
        var indexedOutcome = OutcomeId == "choice_index:" + indexText ||
            OutcomeId == "swap_index:" + indexText;
        var identifiedReward = OutcomeId is { } outcomeId &&
            (outcomeId.StartsWith("relic:", StringComparison.Ordinal) &&
                outcomeId.Length > "relic:".Length ||
             outcomeId.StartsWith("potion:", StringComparison.Ordinal) &&
                outcomeId.Length > "potion:".Length);
        var identifiedHpOffset = OutcomeId is { } hpOffsetId &&
            hpOffsetId.StartsWith("hp_offset:", StringComparison.Ordinal) &&
            int.TryParse(hpOffsetId["hp_offset:".Length..],
                NumberStyles.None, CultureInfo.InvariantCulture,
                out var hpOffset) && hpOffset is >= 2 and <= 9;
        var mappedGold = OutcomeId is { } goldId &&
            goldId.StartsWith("gold:", StringComparison.Ordinal) &&
            int.TryParse(goldId["gold:".Length..],
                NumberStyles.None, CultureInfo.InvariantCulture, out var gold) &&
            gold >= 0;
        if (string.IsNullOrWhiteSpace(LawId) ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(OutcomeId) || OutcomeIndex < 0 ||
            !(indexedOutcome || identifiedReward || identifiedHpOffset ||
                mappedGold))
            throw new InvalidDataException(
                "Invalid ReferenceNature decision.");
    }
}

public sealed record ReferenceNatureRunActionSessionSnapshotV111(
    string SchemaId, string OriginSnapshotJson, GameAction Action,
    string RunSnapshotJson, ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript);

/// <summary>
/// Atomic ReferenceNature envelope for existing controlled run-action
/// continuations. It reuses the run mechanics resolver and the same sampler
/// state as semantic combat sessions.
/// </summary>
public sealed record ReferenceNatureRunActionSessionV111(
    string SchemaId, RunState Origin, GameAction Action, RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-run-action-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool Complete => Run.ControlledRunActionChoice is null;

    public static ReferenceNatureRunActionSessionV111 Begin(
        RunState source, GameAction action, ulong seed) =>
        Begin(source, action,
            ReferenceNatureRunCursorV111.Begin(
                source, seed, "Run-action"));

    public static ReferenceNatureRunActionSessionV111 Begin(
        RunState source, GameAction action) =>
        Begin(source, action,
            ReferenceNatureRunCursorV111.Begin(
                source, null, "Run-action"));

    private static ReferenceNatureRunActionSessionV111 Begin(
        RunState source, GameAction action,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ReferenceNatureRunCursorV111.ValidateInitial(
            source, initialNatureState, "Run-action");
        var detachedAction = CloneAction(action);
        var run = ControlledRunActionEngineV111.Begin(
            source.Fork(), detachedAction);
        var session = new ReferenceNatureRunActionSessionV111(
            CurrentSchemaId, source.Fork(), detachedAction, run,
            initialNatureState.Fork(), initialNatureState.Fork(), []);
        if (session.Complete)
            session = session with
            {
                Run = ReferenceNatureRunCursorV111.Commit(
                    session.Run, initialNatureState)
            };
        session.Validate();
        return session;
    }

    public ReferenceNatureRunActionSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Origin = Origin.Fork(),
            Action = CloneAction(Action),
            Run = Run.Fork(),
            InitialNatureState = InitialNatureState.Fork(),
            NatureState = NatureState.Fork(),
            DecisionTranscript = (ReferenceNatureDecisionV111[])
                DecisionTranscript.Clone()
        };
    }

    public ReferenceNatureRunActionSessionV111 ResolveNext()
    {
        Validate();
        var law = ChanceLawEngineV111.QueryControlledRunAction(Run);
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var resolvedRun = ControlledRunActionEngineV111.Resolve(
            Run.Fork(), selection.OutcomeIndex);
        if (resolvedRun.ControlledRunActionChoice is null)
            resolvedRun = ReferenceNatureRunCursorV111.Commit(
                resolvedRun, selection.NextState);
        var next = this with
        {
            Run = resolvedRun,
            NatureState = selection.NextState,
            DecisionTranscript = DecisionTranscript.Append(
                new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)).ToArray()
        };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        var immediateShopAction = Origin is not null
            && Action is not null
            && ControlledRunActionEngineV111.IsShopPurchaseAction(
                Origin, Action)
            && DecisionTranscript is { Length: 0 };
        var immediateActOneCombatAction = Origin is not null
            && Action is not null
            && ControlledRunActionEngineV111
                .IsLegacyActOneCombatEntryAction(Origin, Action)
            && DecisionTranscript is { Length: 0 };
        if (SchemaId != CurrentSchemaId || Origin is null || Action is null ||
            Run is null || InitialNatureState is null || NatureState is null ||
            DecisionTranscript is null || string.IsNullOrWhiteSpace(Action.Kind) ||
            (Run.ControlledRunActionChoice is null &&
             Run.ControlledRunActionHistory is not { Length: > 0 } &&
             !immediateShopAction && !immediateActOneCombatAction))
            throw new InvalidDataException(
                "Invalid ReferenceNature run-action session envelope.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        var actionMapNodeId = Action.Kind == "choose_map_node"
            ? Action.ReadPayload<ChooseMapNodePayload>().NodeId
            : null;
        InitialNatureState.Validate();
        NatureState.Validate();
        if (!ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature run-action cursor differs from the run envelope.");
        for (int index = 0; index < DecisionTranscript.Length; index++)
            DecisionTranscript[index].Validate();

        if (Run.ControlledRunActionChoice is { } continuation)
        {
            continuation.Validate();
            if (continuation.ActionId != Action.Kind ||
                continuation.MapNodeId != actionMapNodeId ||
                (ControlledRunActionEngineV111.IsShopPurchaseAction(
                     Origin, Action)
                    ? continuation.RunAction is null ||
                      PrototypeAiEnvironment.StableActionId(
                          continuation.RunAction) !=
                      PrototypeAiEnvironment.StableActionId(Action)
                    : continuation.RunAction is not null) ||
                continuation.Decisions.Length != DecisionTranscript.Length)
                throw new InvalidDataException(
                    "Pending run-action continuation differs from ReferenceNature history.");
            for (int index = 0; index < continuation.Decisions.Length; index++)
                if (continuation.Decisions[index].ProviderMode !=
                        "controlled-v111" ||
                    continuation.Decisions[index].SelectedIndex !=
                        DecisionTranscript[index].OutcomeIndex)
                    throw new InvalidDataException(
                        "Pending run-action decisions differ from ReferenceNature history.");
        }
        else
        {
            if (immediateShopAction || immediateActOneCombatAction)
            {
                var replayedRun = ControlledRunActionEngineV111.Begin(
                    Origin.Fork(), CloneAction(Action));
                replayedRun = ReferenceNatureRunCursorV111.Commit(
                    replayedRun, InitialNatureState);
                if (CanonicalJson.Sha256(replayedRun) !=
                    CanonicalJson.Sha256(Run))
                    throw new InvalidDataException(
                        "Deterministic ReferenceNature run action differs from its replay.");
                return;
            }

            var trace = Run.ControlledRunActionHistory![^1];
            trace.Validate();
            if (trace.ActionId != Action.Kind ||
                trace.MapNodeId != actionMapNodeId ||
                (ControlledRunActionEngineV111.IsShopPurchaseAction(
                     Origin, Action)
                    ? trace.ActionFingerprint !=
                      PrototypeAiEnvironment.StableActionId(Action)
                    : trace.ActionFingerprint is not null) ||
                trace.Decisions.Length != DecisionTranscript.Length)
                throw new InvalidDataException(
                    "Completed run-action trace differs from ReferenceNature history.");
            for (int index = 0; index < trace.Decisions.Length; index++)
                if (trace.Decisions[index].ProviderMode != "controlled-v111" ||
                    trace.Decisions[index].SelectedIndex !=
                        DecisionTranscript[index].OutcomeIndex)
                    throw new InvalidDataException(
                        "Completed run-action outcomes differ from ReferenceNature history.");
        }
    }

    public static string Save(ReferenceNatureRunActionSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureRunActionSessionSnapshotV111(
            CurrentSchemaId, RunSnapshot.Save(session.Origin),
            CloneAction(session.Action), RunSnapshot.Save(session.Run),
            session.InitialNatureState, session.NatureState,
            (ReferenceNatureDecisionV111[])
                session.DecisionTranscript.Clone());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureRunActionSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureRunActionSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature run-action snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            snapshot.Action is null ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null || snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature run-action snapshot.");
        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var replay = Begin(origin, snapshot.Action,
            snapshot.InitialNatureState);
        for (int index = 0; index < snapshot.DecisionTranscript.Length; index++)
        {
            if (replay.Complete)
                throw new InvalidDataException(
                    "ReferenceNature run-action replay ended before its transcript.");
            replay = replay.ResolveNext();
            if (CanonicalJson.Sha256(replay.DecisionTranscript[^1]) !=
                CanonicalJson.Sha256(snapshot.DecisionTranscript[index]))
                throw new InvalidDataException(
                    "ReferenceNature run-action replay selected a different outcome.");
        }
        if (replay.Complete && savedRun.ReferenceNatureState is null)
            savedRun = savedRun with
            {
                ReferenceNatureState = snapshot.NatureState.Fork()
            };
        if (CanonicalJson.Sha256(replay.Run) != CanonicalJson.Sha256(savedRun) ||
            CanonicalJson.Sha256(replay.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            replay.DecisionTranscript.Length != snapshot.DecisionTranscript.Length)
            throw new InvalidDataException(
                "ReferenceNature run-action replay differs from its snapshot.");
        replay.Validate();
        return replay;
    }

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };
}

public sealed record ReferenceNatureAncientOfferSessionSnapshotV111(
    string SchemaId, string SiteId, string OriginSnapshotJson,
    string RunSnapshotJson, ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript);

/// <summary>
/// Atomic ReferenceNature envelope for Ancient offer generation. Individual
/// decisions remain private to the session until every source event-offer or
/// Fisher-Yates request has been sampled and replayed through its generator.
/// </summary>
public sealed record ReferenceNatureAncientOfferSessionV111(
    string SchemaId, string SiteId, RunState Origin, RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-ancient-offer-session-v1";

    private const string EventOfferOutcomePrefix = "event_choice:";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool Complete => DecisionTranscript.Length ==
        ChanceLawEngineV111.AncientOfferDecisionCount(Origin, SiteId);

    [JsonIgnore]
    public int NextOrdinal => DecisionTranscript.Length;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => Complete ? null :
        ChanceLawEngineV111.QueryAncientOfferSessionStep(
            Origin, SiteId, DecisionTranscript);

    public static ReferenceNatureAncientOfferSessionV111 Begin(
        RunState source, string siteId, ulong seed) =>
        Begin(source, siteId, (ulong?)seed);

    public static ReferenceNatureAncientOfferSessionV111 Begin(
        RunState source, string siteId) =>
        Begin(source, siteId, (ulong?)null);

    private static ReferenceNatureAncientOfferSessionV111 Begin(
        RunState source, string siteId, ulong? seed)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        return Begin(source, siteId, ReferenceNatureRunCursorV111.Begin(
            source, seed, "Ancient-offer"));
    }

    private static ReferenceNatureAncientOfferSessionV111 Begin(
        RunState source, string siteId,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ValidateOpeningBoundary(source, siteId);
        ReferenceNatureRunCursorV111.ValidateInitial(
            source, initialNatureState, "Ancient-offer");
        var session = new ReferenceNatureAncientOfferSessionV111(
            CurrentSchemaId, siteId, source.Fork(), source.Fork(),
            initialNatureState.Fork(), initialNatureState.Fork(), []);
        session.Validate();
        return session;
    }

    public ReferenceNatureAncientOfferSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Origin = Origin.Fork(),
            Run = Run.Fork(),
            InitialNatureState = InitialNatureState.Fork(),
            NatureState = NatureState.Fork(),
            DecisionTranscript = (ReferenceNatureDecisionV111[])
                DecisionTranscript.Clone()
        };
    }

    public ReferenceNatureAncientOfferSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature Ancient-offer session is already complete.");

        var law = ChanceLawEngineV111.QueryAncientOfferSessionStep(
            Origin, SiteId, DecisionTranscript);
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var transcript = DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var nextNatureState = selection.NextState;
        var nextRun = Origin.Fork();
        if (transcript.Length ==
            ChanceLawEngineV111.AncientOfferDecisionCount(Origin, SiteId))
        {
            nextRun = GenerateEvent(Origin, SiteId, transcript);
            nextRun = ReferenceNatureRunCursorV111.Commit(
                nextRun, nextNatureState);
        }
        var next = this with
        {
            Run = nextRun,
            NatureState = nextNatureState,
            DecisionTranscript = transcript
        };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId ||
            !IsSupportedSite(SiteId) || Origin is null || Run is null ||
            InitialNatureState is null || NatureState is null ||
            DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Ancient-offer session envelope.");
        ValidateOpeningBoundary(Origin, SiteId);
        if (DecisionTranscript.Length >
            ChanceLawEngineV111.AncientOfferDecisionCount(Origin, SiteId))
            throw new InvalidDataException(
                "ReferenceNature Ancient-offer transcript exceeds its source draw count.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        InitialNatureState.Validate();
        NatureState.Validate();
        if (!ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature Ancient-offer cursor differs from the run envelope.");

        var expectedNatureState = InitialNatureState.Fork();
        for (var ordinal = 0; ordinal < DecisionTranscript.Length; ordinal++)
        {
            var decision = DecisionTranscript[ordinal];
            decision.Validate();
            var law = ChanceLawEngineV111.QueryAncientOfferSessionStep(
                Origin, SiteId, DecisionTranscript.Take(ordinal).ToArray());
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) != CanonicalJson.Sha256(decision))
                throw new InvalidDataException(
                    "ReferenceNature Ancient-offer transcript does not replay.");
            expectedNatureState = selection.NextState;
        }

        var expectedRun = Complete
            ? ReferenceNatureRunCursorV111.Commit(
                GenerateEvent(Origin, SiteId, DecisionTranscript),
                expectedNatureState)
            : Origin.Fork();
        if (CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(NatureState) ||
            CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "ReferenceNature Ancient-offer replay differs from its session envelope.");
    }

    public static string Save(ReferenceNatureAncientOfferSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureAncientOfferSessionSnapshotV111(
            CurrentSchemaId, session.SiteId, RunSnapshot.Save(session.Origin),
            RunSnapshot.Save(session.Run), session.InitialNatureState,
            session.NatureState,
            (ReferenceNatureDecisionV111[])session.DecisionTranscript.Clone());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureAncientOfferSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureAncientOfferSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature Ancient-offer snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            !IsSupportedSite(snapshot.SiteId) ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Ancient-offer snapshot.");

        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var replay = Begin(origin, snapshot.SiteId,
            snapshot.InitialNatureState);
        for (var index = 0; index < snapshot.DecisionTranscript.Length; index++)
        {
            if (replay.Complete)
                throw new InvalidDataException(
                    "ReferenceNature Ancient-offer replay exceeded its source draw count.");
            replay = replay.ResolveNext();
            if (CanonicalJson.Sha256(replay.DecisionTranscript[^1]) !=
                CanonicalJson.Sha256(snapshot.DecisionTranscript[index]))
                throw new InvalidDataException(
                    "ReferenceNature Ancient-offer replay selected a different outcome.");
        }
        if (CanonicalJson.Sha256(replay.Run) != CanonicalJson.Sha256(savedRun) ||
            CanonicalJson.Sha256(replay.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            replay.DecisionTranscript.Length !=
                snapshot.DecisionTranscript.Length)
            throw new InvalidDataException(
                "ReferenceNature Ancient-offer replay differs from its snapshot.");
        replay.Validate();
        return replay;
    }

    private static RunState GenerateEvent(
        RunState origin, string siteId,
        ReferenceNatureDecisionV111[] transcript)
    {
        if (siteId == NativeDarvEventV111.OfferVariantSiteId)
            return GenerateDarvEvent(origin, transcript);
        var isShuffle = ChanceLawEngineV111.IsShuffleAncientOfferSite(siteId);
        var expectedCount = ChanceLawEngineV111.AncientOfferDecisionCount(
            origin, siteId);
        if (transcript.Length != expectedCount)
            throw new InvalidOperationException(
                "Ancient offer generation requires its complete sampled transcript.");
        AncientOfferReplayChoiceV111[] scripted;
        if (isShuffle)
        {
            var pools = ChanceLawEngineV111.AncientOfferShuffleChoicePools(
                origin, siteId);
            var requests = ChanceLawEngineV111.AncientOfferShuffleRequests(
                siteId, pools);
            scripted = transcript.Select((decision, ordinal) =>
            {
                var expectedOutcome = "swap_index:" +
                    decision.OutcomeIndex.ToString(CultureInfo.InvariantCulture);
                if (decision.OutcomeId != expectedOutcome ||
                    decision.OutcomeIndex < 0 ||
                    decision.OutcomeIndex >=
                        requests[ordinal].Request.SupportCount)
                    throw new InvalidDataException(
                        "Sampled Ancient offer shuffle index is outside its source support.");
                return new AncientOfferReplayChoiceV111(
                    requests[ordinal].Request, decision.OutcomeIndex);
            }).ToArray();
        }
        else
        {
            var candidates = Enumerable.Range(0, 3).Select(ordinal =>
                CandidateChoiceIds(origin, siteId, ordinal)).ToArray();
            scripted = transcript.Select((decision, ordinal) =>
            {
                var selectedIndex = Array.FindIndex(candidates[ordinal], choiceId =>
                    EventOfferOutcomePrefix + choiceId == decision.OutcomeId);
                if (selectedIndex < 0)
                    throw new InvalidDataException(
                        "Sampled Ancient offer is outside its source candidate pool.");
                var request = new RandomIndexRequestV111(
                    siteId, StreamId(siteId), OwnerId(siteId), ordinal,
                    Enumerable.Range(0, candidates[ordinal].Length).ToArray());
                return new AncientOfferReplayChoiceV111(request, selectedIndex);
            }).ToArray();
        }
        var provider = new AncientOfferReplayProviderV111(scripted);
        var run = siteId switch
        {
            NativeOrobasEventV111.OfferSiteId =>
                NativeOrobasEventV111.BeginOpeningWithAudit(origin, provider).State,
            NativePaelRewardsV111.OfferSiteId =>
                NativePaelRewardsV111.BeginPaelOpeningWithAudit(origin, provider).State,
            NativeAncientRewardsV111.TezcataraOfferSiteId =>
                NativeAncientRewardsV111.BeginTezcataraOpeningWithAudit(
                    origin, provider).State,
            NativeNonupeipeEventV111.ShuffleSiteId =>
                NativeNonupeipeEventV111.BeginOpeningWithAudit(
                    origin, provider).State,
            NativeTanxEventV111.ShuffleSiteId =>
                NativeTanxEventV111.BeginOpeningWithAudit(
                    origin, provider).State,
            NativeVakuuEventV111.ShuffleSiteId =>
                NativeVakuuEventV111.BeginOpeningWithAudit(
                    origin, provider).State,
            _ => throw new NotSupportedException(
                $"ReferenceNature Ancient-offer session does not support '{siteId}'.")
        };
        if (provider.Remaining != 0)
            throw new InvalidOperationException(
                "Ancient offer generator did not consume the complete Nature transcript.");
        return run;
    }

    private static RunState GenerateDarvEvent(
        RunState origin, ReferenceNatureDecisionV111[] transcript)
    {
        var shuffleRequests = ChanceLawEngineV111
            .DarvOfferShuffleRequests(origin);
        if (transcript.Length != shuffleRequests.Length + 1)
            throw new InvalidOperationException(
                "Darv generation requires the offer variant and complete shuffle prefix.");
        var variant = transcript[0];
        var expectedVariant = variant.OutcomeIndex switch
        {
            0 => "offer_variant:dusty_tome",
            1 => "offer_variant:three_relics",
            _ => null
        };
        if (variant.LawId is null ||
            variant.OutcomeId != expectedVariant)
            throw new InvalidDataException(
                "Sampled Darv offer variant is outside its binary support.");

        var scripted = new List<AncientOfferReplayChoiceV111>(
            shuffleRequests.Length + 1)
        {
            new(ChanceLawEngineV111.DarvOfferVariantRequest(origin),
                variant.OutcomeIndex)
        };
        for (var index = 0; index < shuffleRequests.Length; index++)
        {
            var decision = transcript[index + 1];
            var expectedOutcome = "swap_index:" +
                decision.OutcomeIndex.ToString(CultureInfo.InvariantCulture);
            if (decision.OutcomeId != expectedOutcome ||
                decision.OutcomeIndex < 0 ||
                decision.OutcomeIndex >= shuffleRequests[index].SupportCount)
                throw new InvalidDataException(
                    "Sampled Darv shuffle index is outside its source support.");
            scripted.Add(new AncientOfferReplayChoiceV111(
                shuffleRequests[index], decision.OutcomeIndex));
        }

        var provider = new AncientOfferReplayProviderV111(scripted.ToArray());
        var run = NativeDarvEventV111
            .BeginOpeningWithAudit(origin, provider).State;
        if (provider.Remaining != 0)
            throw new InvalidOperationException(
                "Darv generator did not consume the complete Nature transcript.");
        return run;
    }

    private static string[] CandidateChoiceIds(
        RunState run, string siteId, int ordinal)
    {
        if (siteId == NativeOrobasEventV111.OfferSiteId)
            return NativeOrobasEventV111.OfferCandidateIds(run.Player, ordinal);
        if (siteId == NativePaelRewardsV111.OfferSiteId)
            return NativePaelRewardsV111.OfferPools(run.Player)[ordinal]
                .Select(id => "take_" + id).ToArray();
        if (siteId == NativeAncientRewardsV111.TezcataraOfferSiteId)
            return NativeAncientRewardsV111.TezcataraPools(run.Player)[ordinal]
                .Select(id => "take_" + id).ToArray();
        throw new NotSupportedException(
            $"ReferenceNature Ancient-offer session does not support '{siteId}'.");
    }

    private static void ValidateOpeningBoundary(RunState run, string siteId)
    {
        ChanceLawEngineV111.ValidateAncientOfferOpeningBoundary(run, siteId);
    }

    private static bool IsSupportedSite(string siteId) =>
        ChanceLawEngineV111.IsSupportedAncientOfferSessionSite(siteId);

    private static string StreamId(string siteId) => siteId switch
    {
        NativeOrobasEventV111.OfferSiteId => "context.event.OROBAS.player_0",
        NativePaelRewardsV111.OfferSiteId => "context.event.PAEL.player_0",
        NativeAncientRewardsV111.TezcataraOfferSiteId =>
            "context.event.TEZCATARA.player_0",
        _ => throw new NotSupportedException(
            $"No event RNG stream is registered for '{siteId}'.")
    };

    private static string OwnerId(string siteId) => siteId switch
    {
        NativeOrobasEventV111.OfferSiteId => "event:OROBAS:act:2:floor:0",
        NativePaelRewardsV111.OfferSiteId => "event:PAEL:act:2:floor:0",
        NativeAncientRewardsV111.TezcataraOfferSiteId =>
            "event:TEZCATARA:act:2:floor:0",
        _ => throw new NotSupportedException(
            $"No event owner is registered for '{siteId}'.")
    };

    private sealed record AncientOfferReplayChoiceV111(
        RandomIndexRequestV111 Request, int SelectedIndex);

    /// <summary>Silent exact replay used during validation; it does not append
    /// duplicate entries to the process-wide random-boundary journal.</summary>
    private sealed class AncientOfferReplayProviderV111(
        AncientOfferReplayChoiceV111[] choices) : IRandomIndexProviderV111
    {
        private int _cursor;
        public string Mode => "scripted-v111";
        public int Remaining => choices.Length - _cursor;

        public RandomIndexDecisionV111 Resolve(
            RandomIndexRequestV111 request, RngBundle streams)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(streams);
            request.Validate();
            if (_cursor >= choices.Length)
                throw new InvalidOperationException(
                    "Ancient offer replay transcript is exhausted.");
            var expected = choices[_cursor++];
            if (request.SiteId != expected.Request.SiteId ||
                request.StreamId != expected.Request.StreamId ||
                request.OwnerId != expected.Request.OwnerId ||
                request.Ordinal != expected.Request.Ordinal ||
                request.SourceVersion != expected.Request.SourceVersion ||
                request.OperationKind != expected.Request.OperationKind ||
                request.BoundedCount != expected.Request.BoundedCount ||
                !request.OrderedCandidates.SequenceEqual(
                    expected.Request.OrderedCandidates))
                throw new InvalidOperationException(
                    "Ancient offer replay site/owner/ordinal/eligibility mismatch.");
            var decision = new RandomIndexDecisionV111(
                request with { OrderedCandidates =
                    (int[])request.OrderedCandidates.Clone() },
                expected.SelectedIndex, Mode);
            decision.Validate();
            return decision;
        }
    }
}

public sealed record ReferenceNatureControlledEventChoiceStepV111(
    string Kind, ReferenceNatureDecisionV111? NatureDecision = null,
    GameAction? PlayerAction = null)
{
    public void Validate()
    {
        if (Kind == "nature")
        {
            if (NatureDecision is null || PlayerAction is not null)
                throw new InvalidDataException(
                    "Malformed ReferenceNature event-choice sample step.");
            NatureDecision.Validate();
            return;
        }
        if (Kind == "player_action")
        {
            if (PlayerAction is null || NatureDecision is not null ||
                string.IsNullOrWhiteSpace(PlayerAction.Kind))
                throw new InvalidDataException(
                    "Malformed ReferenceNature event-choice player step.");
            return;
        }
        throw new InvalidDataException(
            "Unknown ReferenceNature event-choice session step.");
    }

    public ReferenceNatureControlledEventChoiceStepV111 Fork() => this with
    {
        PlayerAction = PlayerAction is null
            ? null : CloneAction(PlayerAction)
    };

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };
}

public sealed record ReferenceNatureControlledEventChoiceSnapshotV111(
    string SchemaId, string OriginSnapshotJson, string ChoiceId,
    string RunSnapshotJson, ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureControlledEventChoiceStepV111[] Steps);

/// <summary>
/// Atomic ReferenceNature envelope for controlled event-effect and
/// post-reward event-choice continuations. Event player actions and Nature
/// samples share one ordered replay transcript and sampler state.
/// </summary>
public sealed record ReferenceNatureControlledEventChoiceSessionV111(
    string SchemaId, RunState Origin, string ChoiceId, RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureControlledEventChoiceStepV111[] Steps)
{
    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-event-choice-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool NaturePending => Run.World?.Event?.ControlledChoice is not null ||
        Run.ControlledEventRewardChoice is not null;

    [JsonIgnore]
    public bool CanAdvancePlayer => !NaturePending &&
        (ControlledEventChoiceEngineV111
            .IsStonePushDeckChoiceBoundary(Run)
        || Run.Phase == RunPhase.Reward
            && Run.World?.Event is { } eventState
            && eventState.ChosenChoiceId == ChoiceId
            && Run.World.Reward is
            {
                AddRandomCurseAfterReward: true,
                SourceRoom: var sourceRoom
            }
            && sourceRoom == "Event:" + eventState.EventId
            && Run.ControlledEventChoiceHistory is
                { Length: > 0 } history
            && history[^1].EventId == eventState.EventId
            && history[^1].ChoiceId == ChoiceId);

    [JsonIgnore]
    public bool Complete => !NaturePending && !CanAdvancePlayer;

    [JsonIgnore]
    public ReferenceNatureDecisionV111[] DecisionTranscript => Steps
        .Where(step => step.Kind == "nature")
        .Select(step => step.NatureDecision!)
        .ToArray();

    public static ReferenceNatureControlledEventChoiceSessionV111 Begin(
        RunState source, string choiceId, ulong seed) =>
        Begin(source, choiceId,
            ReferenceNatureRunCursorV111.Begin(
                source, seed, "Event-choice"));

    public static ReferenceNatureControlledEventChoiceSessionV111 Begin(
        RunState source, string choiceId) =>
        Begin(source, choiceId,
            ReferenceNatureRunCursorV111.Begin(
                source, null, "Event-choice"));

    private static ReferenceNatureControlledEventChoiceSessionV111 Begin(
        RunState source, string choiceId,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(choiceId);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ReferenceNatureRunCursorV111.ValidateInitial(
            source, initialNatureState, "Event-choice");
        var run = ControlledEventChoiceEngineV111.Begin(
            source.Fork(), choiceId);
        var session = new ReferenceNatureControlledEventChoiceSessionV111(
            CurrentSchemaId, source.Fork(), choiceId, run,
            initialNatureState.Fork(), initialNatureState.Fork(), []);
        if (session.Complete)
            session = session with
            {
                Run = ReferenceNatureRunCursorV111.Commit(
                    session.Run, initialNatureState)
            };
        session.Validate();
        return session;
    }

    public ReferenceNatureControlledEventChoiceSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Origin = Origin.Fork(),
            Run = Run.Fork(),
            InitialNatureState = InitialNatureState.Fork(),
            NatureState = NatureState.Fork(),
            Steps = Steps.Select(step => step.Fork()).ToArray()
        };
    }

    public ReferenceNatureControlledEventChoiceSessionV111 ResolveNext()
    {
        Validate();
        if (!NaturePending)
            throw new InvalidOperationException(
                "No controlled event Nature outcome is pending.");
        var law = ChanceLawEngineV111.QueryControlledEventChoice(Run);
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var resolvedRun = ControlledEventChoiceEngineV111.Resolve(
            Run.Fork(), selection.OutcomeIndex);
        var next = this with
        {
            Run = resolvedRun,
            NatureState = selection.NextState,
            Steps = Steps.Append(new ReferenceNatureControlledEventChoiceStepV111(
                "nature", new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex))).ToArray()
        };
        if (next.Complete)
            next = next with
            {
                Run = next.Run with
                {
                    ReferenceNatureState = selection.NextState
                }
            };
        next.Validate();
        return next;
    }

    public ReferenceNatureControlledEventChoiceSessionV111 AdvancePlayerAction(
        GameAction action)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(action);
        if (!CanAdvancePlayer)
            throw new InvalidOperationException(
                "No supported event player action is pending.");
        var engine = new PrototypeGameEngine();
        var actionId = PrototypeAiEnvironment.StableActionId(action);
        if (!engine.GetLegalActions(Run).Any(candidate =>
                StringComparer.Ordinal.Equals(
                    PrototypeAiEnvironment.StableActionId(candidate), actionId)))
            throw new InvalidOperationException(
                "ReferenceNature event advance requires a current legal player action.");
        var nextRun = ControlledEventChoiceEngineV111.Advance(
            Run.Fork(), CloneAction(action));
        var next = this with
        {
            Run = nextRun,
            Steps = Steps.Append(new ReferenceNatureControlledEventChoiceStepV111(
                "player_action", PlayerAction: CloneAction(action))).ToArray()
        };
        if (next.Complete)
            next = next with
            {
                Run = ReferenceNatureRunCursorV111.Commit(
                    next.Run, NatureState)
            };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Origin is null || Run is null ||
            string.IsNullOrWhiteSpace(ChoiceId) || InitialNatureState is null ||
            NatureState is null || Steps is null ||
            Origin.Phase != RunPhase.Event || Origin.World?.Event is not { } originEvent ||
            !PrototypeGameEngine.IsControlledEventChoice(
                originEvent.EventId, ChoiceId))
            throw new InvalidDataException(
                "Invalid ReferenceNature controlled event-choice envelope.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        InitialNatureState.Validate();
        NatureState.Validate();
        var decisions = DecisionTranscript;
        if (!ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                decisions.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature event-choice cursor differs from the run envelope.");
        foreach (var step in Steps)
            step.Validate();

        var effect = Run.World?.Event?.ControlledChoice;
        var reward = Run.ControlledEventRewardChoice;
        if (effect is not null && reward is not null)
            throw new InvalidDataException(
                "Run state contains two simultaneous controlled event requests.");
        if (effect is null && reward is null)
        {
            if (ControlledEventChoiceEngineV111
                    .IsStonePushDeckChoiceBoundary(Run))
            {
                if (decisions.Length != 0 || Steps.Length != 0)
                    throw new InvalidDataException(
                        "Stone of All Time push deck-choice boundary has an unexpected transcript prefix.");
                return;
            }
            if (ControlledEventChoiceEngineV111
                    .IsStonePushNoChanceTerminal(Run, Origin, ChoiceId))
            {
                if (decisions.Length != 0 || Steps.Length != 0)
                    throw new InvalidDataException(
                        "Lethal Stone of All Time push has an unexpected chance transcript.");
                return;
            }
            if (Run.ControlledEventChoiceHistory is not { Length: > 0 } history ||
                history[^1].EventId != originEvent.EventId ||
                history[^1].ChoiceId != ChoiceId ||
                history[^1].Decisions.Length != decisions.Length)
                throw new InvalidDataException(
                    "Completed ReferenceNature event choice differs from its mechanics trace.");
            history[^1].Validate();
            for (int index = 0; index < decisions.Length; index++)
                if (history[^1].Decisions[index].SelectedIndex !=
                    decisions[index].OutcomeIndex)
                    throw new InvalidDataException(
                        "Completed event-choice outcomes differ from ReferenceNature history.");
            return;
        }

        var continuationDecisions = effect?.Decisions ?? reward!.Decisions;
        if (continuationDecisions.Length != decisions.Length ||
            (effect?.EventId ?? reward!.EventId) != originEvent.EventId ||
            (effect?.ChoiceId ?? reward!.ChoiceId) != ChoiceId)
            throw new InvalidDataException(
                "Pending event continuation differs from ReferenceNature history.");
        for (int index = 0; index < decisions.Length; index++)
            if (continuationDecisions[index].ProviderMode != "controlled-v111" ||
                continuationDecisions[index].SelectedIndex != decisions[index].OutcomeIndex)
                throw new InvalidDataException(
                    "Pending event outcomes differ from ReferenceNature history.");
    }

    public static string Save(
        ReferenceNatureControlledEventChoiceSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureControlledEventChoiceSnapshotV111(
            CurrentSchemaId, RunSnapshot.Save(session.Origin), session.ChoiceId,
            RunSnapshot.Save(session.Run), session.InitialNatureState,
            session.NatureState,
            session.Steps.Select(step => step.Fork()).ToArray());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureControlledEventChoiceSessionV111 Restore(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureControlledEventChoiceSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature event-choice snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.ChoiceId) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null || snapshot.Steps is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature event-choice snapshot.");
        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var replay = Begin(origin, snapshot.ChoiceId,
            snapshot.InitialNatureState);
        foreach (var savedStep in snapshot.Steps)
        {
            savedStep.Validate();
            replay = savedStep.Kind switch
            {
                "nature" when replay.NaturePending => replay.ResolveNext(),
                "player_action" when replay.CanAdvancePlayer =>
                    replay.AdvancePlayerAction(savedStep.PlayerAction!),
                _ => throw new InvalidDataException(
                    "ReferenceNature event-choice replay step is out of order.")
            };
            if (CanonicalJson.Sha256(replay.Steps[^1]) !=
                CanonicalJson.Sha256(savedStep))
                throw new InvalidDataException(
                    "ReferenceNature event-choice replay diverged from its transcript.");
        }
        if (replay.Complete && savedRun.ReferenceNatureState is null)
            savedRun = savedRun with
            {
                ReferenceNatureState = snapshot.NatureState.Fork()
            };
        if (CanonicalJson.Sha256(replay.Run) != CanonicalJson.Sha256(savedRun) ||
            CanonicalJson.Sha256(replay.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            CanonicalJson.Sha256(replay.Steps) !=
                CanonicalJson.Sha256(snapshot.Steps))
            throw new InvalidDataException(
                "ReferenceNature event-choice replay differs from its snapshot.");
        replay.Validate();
        return replay;
    }

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };
}

/// <summary>
/// Small seeded reference policy that samples the declared law using an
/// independent SplitMix64 stream. It does not use or advance a game RNG.
/// </summary>
public static class ReferenceNatureV111
{
    public const string ProviderId = "reference-nature-splitmix64-v1";
    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

    /// <summary>
    /// Sample and resolve the next supported Nature site through the existing
    /// semantic mechanics continuation. The returned child and next sampler
    /// state are published together only after resolution succeeds.
    /// </summary>
    public static ReferenceNatureResolutionV111 ResolvePending(
        SemanticPilotCombatV111 combat, ReferenceNatureStateV111 state)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(state);
        combat.Validate();
        state.Validate();

        if (combat.IntentPending is not null)
        {
            var intentEvent = SemanticPilotCombatEngineV111
                .DescribePendingIntentEvent(combat);
            var law = ChanceLawEngineV111.QueryIntent(intentEvent);
            var selection = SampleValidated(law, state);
            var child = SemanticPilotCombatEngineV111.ResolveIntent(
                combat, combat.Fingerprint, selection.OutcomeId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.EnemyActionEventPending is { } enemyActionEvent)
        {
            var law = ChanceLawEngineV111.QueryEndTurnIndex(
                enemyActionEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != enemyActionEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >=
                    enemyActionEvent.Request.SupportCount ||
                selection.OutcomeId != "choice_index:" +
                    selection.OutcomeIndex.ToString(
                        CultureInfo.InvariantCulture))
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending enemy-action index event.");
            var child = SemanticPilotCombatEngineV111.Resolve(
                combat, combat.Fingerprint, selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.TargetPending is { } targetEvent)
        {
            var law = ChanceLawEngineV111.QueryTarget(targetEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != targetEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >=
                    targetEvent.CandidateEnemyInstanceIds.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending target event.");
            var enemyInstanceId =
                targetEvent.CandidateEnemyInstanceIds[selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111.ResolveRandomTarget(
                combat, combat.Fingerprint, targetEvent.EventId,
                enemyInstanceId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.GeneratedCardPending is { } generatedCardEvent)
        {
            var law = ChanceLawEngineV111.QueryGeneratedCard(
                generatedCardEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != generatedCardEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >=
                    generatedCardEvent.CandidateCardIds.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending generated-card event.");
            var cardId = generatedCardEvent.CandidateCardIds[
                selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111.ResolveGeneratedCard(
                combat, combat.Fingerprint, generatedCardEvent.EventId,
                cardId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.CostPending is { } costEvent)
        {
            var law = ChanceLawEngineV111.QueryCost(costEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != costEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >= costEvent.CandidateCosts.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending cost event.");
            var selectedCost = costEvent.CandidateCosts[
                selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111.ResolveCost(
                combat, combat.Fingerprint, costEvent.EventId,
                selectedCost);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.RandomPileCardPending is { } randomPileCardEvent)
        {
            var law = ChanceLawEngineV111.QueryRandomPileCard(
                randomPileCardEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != randomPileCardEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >=
                    randomPileCardEvent.CandidateCardInstanceIds.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending pile-card event.");
            var cardInstanceId = randomPileCardEvent
                .CandidateCardInstanceIds[selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111.ResolveRandomPileCard(
                combat, combat.Fingerprint, randomPileCardEvent.EventId,
                cardInstanceId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.CardTransformPending is { } transformEvent)
        {
            var law = ChanceLawEngineV111.QueryCardTransform(transformEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != transformEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >=
                    transformEvent.CandidateCardIds.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending transform event.");
            var cardId = transformEvent.CandidateCardIds[
                selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111.ResolveCardTransform(
                combat, combat.Fingerprint, transformEvent.EventId, cardId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.PotionGenerationPending is { } potionEvent)
        {
            var law = ChanceLawEngineV111.QueryPotionGeneration(potionEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != potionEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >= potionEvent.CandidatePotionIds.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending potion event.");
            var potionId = potionEvent.CandidatePotionIds[
                selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111.ResolvePotionGeneration(
                combat, combat.Fingerprint, potionEvent.EventId, potionId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.HandCostSelectionPending is { } handCostEvent)
        {
            var law = ChanceLawEngineV111.QueryHandCostSelection(
                handCostEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != handCostEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >=
                    handCostEvent.CandidateCardInstanceIds.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending hand-cost event.");
            var cardInstanceId = handCostEvent.CandidateCardInstanceIds[
                selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111
                .ResolveHandCostSelection(
                    combat, combat.Fingerprint, handCostEvent.EventId,
                    cardInstanceId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.PoolShufflePending is { } poolShuffleEvent)
        {
            var law = ChanceLawEngineV111.QueryPoolShuffle(poolShuffleEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != poolShuffleEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >= poolShuffleEvent.Request.SupportCount)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending pool-shuffle event.");
            var child = SemanticPilotCombatEngineV111.ResolvePoolShuffle(
                combat, combat.Fingerprint, poolShuffleEvent.EventId,
                selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.DrawInsertionPending is { } insertionEvent)
        {
            var law = ChanceLawEngineV111.QueryDrawInsertion(insertionEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != insertionEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >= insertionEvent.Request.SupportCount)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending draw-insertion event.");
            var child = SemanticPilotCombatEngineV111.ResolveDrawInsertion(
                combat, combat.Fingerprint, insertionEvent.EventId,
                selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.CardShortlistPending is { } shortlistEvent)
        {
            var law = ChanceLawEngineV111.QueryCardShortlist(shortlistEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != shortlistEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >=
                    shortlistEvent.CandidateCardInstanceIds.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending shortlist event.");
            var cardInstanceId = shortlistEvent.CandidateCardInstanceIds[
                selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111.ResolveCardShortlist(
                combat, combat.Fingerprint, shortlistEvent.EventId,
                cardInstanceId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.DrawPending is { } drawEvent)
        {
            var deck = combat.Run.World?.Combat?.SemanticDeck ??
                throw new InvalidDataException(
                    "Pending semantic draw lost its provider-owned deck.");
            var law = ChanceLawEngineV111.QueryDraw(deck);
            var selection = SampleValidated(law, state);
            if (selection.EventId != drawEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >= drawEvent.CandidateCardInstanceIds.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending draw event.");
            var cardInstanceId =
                drawEvent.CandidateCardInstanceIds[selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111.ResolveDrawCard(
                combat, combat.Fingerprint, drawEvent.EventId,
                cardInstanceId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.DeckRevealPending is { } revealEvent)
        {
            var deck = combat.Run.World?.Combat?.SemanticDeck ??
                throw new InvalidDataException(
                    "Pending semantic reveal lost its provider-owned deck.");
            var law = ChanceLawEngineV111.QueryDeckReveal(deck);
            var selection = SampleValidated(law, state);
            if (selection.EventId != revealEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >=
                    revealEvent.CandidateCardInstanceIds.Length ||
                selection.OutcomeId != "card_instance:" +
                    revealEvent.CandidateCardInstanceIds[
                        selection.OutcomeIndex].ToString(
                            CultureInfo.InvariantCulture))
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending deck reveal.");
            var child = SemanticPilotCombatEngineV111.ResolveDeckReveal(
                combat, combat.Fingerprint, revealEvent.EventId,
                revealEvent.CandidateCardInstanceIds[
                    selection.OutcomeIndex]);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        throw new NotSupportedException(
            "UNSUPPORTED_REFERENCE_NATURE site=no_supported_pending_event");
    }

    public static ReferenceNatureSelectionV111 SampleValidated(
        ChanceLawV111 law, ReferenceNatureStateV111 state)
    {
        var selection = Sample(law, state);
        if (selection.LawId != law.LawId ||
            selection.EventId != law.EventId ||
            selection.OutcomeIndex < 0 ||
            selection.OutcomeIndex >= law.Outcomes.Length ||
            selection.OutcomeId !=
                law.Outcomes[selection.OutcomeIndex].OutcomeId)
            throw new InvalidDataException(
                "ReferenceNature sample does not match the queried Chance Law support.");
        return selection;
    }

    public static ReferenceNatureSelectionV111 Sample(
        ChanceLawV111 law, ReferenceNatureStateV111 state)
    {
        ArgumentNullException.ThrowIfNull(law);
        ArgumentNullException.ThrowIfNull(state);
        law.Validate();
        state.Validate();

        ulong randomState = state.SamplerState;
        ulong ticket = NextBounded(ref randomState, (ulong)law.TotalWeight);
        long cumulative = 0;
        for (var index = 0; index < law.Outcomes.Length; index++)
        {
            cumulative = checked(cumulative + law.Outcomes[index].Weight);
            if (ticket < (ulong)cumulative)
            {
                return new ReferenceNatureSelectionV111(
                    law.LawId, law.EventId, law.Outcomes[index].OutcomeId,
                    index, new ReferenceNatureStateV111(
                        ReferenceNatureStateV111.CurrentSchemaId,
                        randomState, checked(state.SampleCount + 1)));
            }
        }

        throw new InvalidDataException("ReferenceNature sampled outside law support.");
    }

    private static ulong NextBounded(ref ulong state, ulong bound)
    {
        if (bound == 0)
            throw new ArgumentOutOfRangeException(nameof(bound));
        var threshold = unchecked(0UL - bound) % bound;
        while (true)
        {
            var value = NextUInt64(ref state);
            if (value >= threshold)
                return value % bound;
        }
    }

    private static ulong NextUInt64(ref ulong state)
    {
        state = unchecked(state + GoldenGamma);
        ulong value = state;
        value = unchecked((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL);
        value = unchecked((value ^ (value >> 27)) * 0x94D049BB133111EBUL);
        return value ^ (value >> 31);
    }
}
