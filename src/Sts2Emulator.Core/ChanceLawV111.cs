using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>An exact, integer-weighted outcome in a conditional chance law.</summary>
public sealed record ChanceLawOutcomeV111(string OutcomeId, long Weight);

/// <summary>
/// Compact uniform support for a bounded integer request whose outcomes are
/// consecutive raw indices. The current representation deliberately fixes the
/// first value at zero so the selected outcome index remains the provider value.
/// </summary>
public sealed record ChanceLawUniformIndexRangeV111(
    int StartInclusive, int Count, string OutcomeIdPrefix)
{
    public string OutcomeIdAt(int offset)
    {
        if ((uint)offset >= (uint)Count)
            throw new ArgumentOutOfRangeException(nameof(offset));
        return OutcomeIdPrefix + checked(StartInclusive + offset)
            .ToString(CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Compact exact support for Native NextInt(boundExclusive), represented as
/// the source's uniform 53-bit input grid and its bounded-index transform.
/// </summary>
public sealed record ChanceLawNativeBoundedIndexGridV111(
    int BoundExclusive, long InputGridSize, string OutcomeIdPrefix)
{
    public string OutcomeIdAt(int index)
    {
        if ((uint)index >= (uint)BoundExclusive)
            throw new ArgumentOutOfRangeException(nameof(index));
        return OutcomeIdPrefix + index.ToString(
            CultureInfo.InvariantCulture);
    }
}

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
    private string[]? _natureChoiceSiteIds = Copy(NatureChoiceSiteIds);
    private string[]? _providerModes = Copy(ProviderModes);
    private string[]? _referenceNatureOperations =
        Copy(ReferenceNatureOperations);
    private string[]? _probabilityStatuses =
        [ChanceLawV111.UnverifiedStatus];
    private string[]? _scriptedRunActionOperations =
        Copy(ScriptedRunActionSessionV111.Operations);
    private string[]? _scriptedRunSessionOperations =
        Copy(ScriptedRunSessionV111.Operations);
    private string[]? _semanticNatureChoiceViewOperations =
        ["nature_choice_observe", "semantic_nature_observe"];
    private string[]? _semanticNatureChoiceViewSessionKinds =
        StochasticCapabilitiesV111.SemanticNatureChoiceViewSessionKinds;
    private string[]? _chanceLawSchemaIds =
        StochasticCapabilitiesV111.ChanceLawSchemaIds;

    public string[] NatureChoiceSiteIds
    {
        get => Copy(_natureChoiceSiteIds)!;
        init => _natureChoiceSiteIds = Copy(value);
    }
    public string[] ProviderModes
    {
        get => Copy(_providerModes)!;
        init => _providerModes = Copy(value);
    }
    public string[] ReferenceNatureOperations
    {
        get => Copy(_referenceNatureOperations)!;
        init => _referenceNatureOperations = Copy(value);
    }

    /// <summary>Individual Chance Laws carry one of these statuses. The
    /// legacy ProbabilityStatus field remains the conservative default.</summary>
    public string[] ProbabilityStatuses
    {
        get => Copy(_probabilityStatuses)!;
        init => _probabilityStatuses = Copy(value);
    }
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
    public string ReferenceNatureNativeEventRelicSessionSchemaId { get; init; } =
        ReferenceNatureNativeEventRelicSessionV111.CurrentSchemaId;
    public string ReferenceNatureNativeShovelSessionSchemaId { get; init; } =
        ReferenceNatureNativeShovelSessionV111.CurrentSchemaId;
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
    public string ReferenceNatureCombatRewardSessionSchemaId { get; init; } =
        ReferenceNatureCombatRewardSessionV111.CurrentSchemaId;
    public string ReferenceNatureMapGenerationSessionSchemaId { get; init; } =
        ReferenceNatureMapGenerationSessionV111.CurrentSchemaId;
    public string ScriptedRunActionSessionSchemaId { get; init; } =
        ScriptedRunActionSessionV111.CurrentSchemaId;
    public string[] ScriptedRunActionOperations
    {
        get => Copy(_scriptedRunActionOperations)!;
        init => _scriptedRunActionOperations = Copy(value);
    }
    public string ScriptedRunSessionSchemaId { get; init; } =
        ScriptedRunSessionV111.CurrentSchemaId;
    public string[] ScriptedRunSessionOperations
    {
        get => Copy(_scriptedRunSessionOperations)!;
        init => _scriptedRunSessionOperations = Copy(value);
    }
    public string ReferenceNatureScriptedRunSessionSchemaId { get; init; } =
        ReferenceNatureScriptedRunSessionV111.CurrentSchemaId;
    private string[]? _referenceNatureScriptedRunSessionOperations =
        Copy(ReferenceNatureScriptedRunSessionV111.Operations);
    public string[] ReferenceNatureScriptedRunSessionOperations
    {
        get => Copy(_referenceNatureScriptedRunSessionOperations)!;
        init => _referenceNatureScriptedRunSessionOperations = Copy(value);
    }
    public string SemanticNatureChoiceViewSchemaId { get; init; } =
        SemanticNatureChoiceViewV111.CurrentSchemaId;
    public string[] SemanticNatureChoiceViewOperations
    {
        get => Copy(_semanticNatureChoiceViewOperations)!;
        init => _semanticNatureChoiceViewOperations = Copy(value);
    }
    public string[] SemanticNatureChoiceViewSessionKinds
    {
        get => Copy(_semanticNatureChoiceViewSessionKinds)!;
        init => _semanticNatureChoiceViewSessionKinds = Copy(value);
    }
    public string[] ChanceLawSchemaIds
    {
        get => Copy(_chanceLawSchemaIds)!;
        init => _chanceLawSchemaIds = Copy(value);
    }

    private static string[]? Copy(string[]? values) =>
        values is null ? null : (string[])values.Clone();
}

public static class StochasticCapabilitiesV111
{
    public const string SchemaId = "sts2-v111-stochastic-capabilities-v6";
    public const string ExternalSelectionProviderId =
        "external-semantic-selection-v1";

    public static string[] ChanceLawSchemaIds =>
    [
        ChanceLawV111.CurrentSchemaId,
        ChanceLawV111.UniformIndexRangeSchemaId,
        ChanceLawV111.NativeBoundedIndexGridSchemaId
    ];

    private static readonly string[] NatureChoiceViewSessionKinds =
    [
        "semantic_combat", "reference_combat", "run_action",
        "event_choice", "ancient_offer", "neow_offer", "trash_heap",
        "tablet_of_truth", "trial", "event_shuffle", "native_event_gold",
        "native_event_relic", "native_shovel", "endless_conveyor",
        "merchant", "treasure", "combat_reward", "glory_reflections",
        "infested_automaton", "punch_off_reward", "punch_off_fight",
        "map_generation", "controlled_event_choice", "controlled_run_action",
        "reference_nature_scripted_run"
    ];

    public static string[] SemanticNatureChoiceViewSessionKinds =>
        (string[])NatureChoiceViewSessionKinds.Clone();

    public static bool IsSemanticNatureChoiceViewSessionKind(string sessionKind) =>
        NatureChoiceViewSessionKinds.Contains(sessionKind,
            StringComparer.Ordinal);

    public static StochasticCapabilityManifestV111 Create() =>
        Create(StochasticSiteRegistryV111.CreateSessionCoverage());

    public static StochasticCapabilityManifestV111 Create(
        StochasticSessionCoverageV111 sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        sessions.Validate();
        return CreateValidated(sessions);
    }

    private static StochasticCapabilityManifestV111 CreateValidated(
        StochasticSessionCoverageV111 sessions) => new(
        SchemaId,
        ChanceLawV111.CurrentSchemaId,
        ReferenceNatureV111.ProviderId,
        sessions.SemanticReferenceNatureSessionSchemaId,
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
            .. ReferenceNatureScriptedRunSessionV111.Operations,
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
            "reference_nature_native_event_relic_begin",
            "reference_nature_native_event_relic_status",
            "reference_nature_native_event_relic_resolve_next",
            "reference_nature_native_event_relic_fork",
            "reference_nature_native_event_relic_save",
            "reference_nature_native_event_relic_load",
            "reference_nature_native_event_relic_release_many",
            "reference_nature_native_shovel_begin",
            "reference_nature_native_shovel_status",
            "reference_nature_native_shovel_resolve_next",
            "reference_nature_native_shovel_fork",
            "reference_nature_native_shovel_save",
            "reference_nature_native_shovel_load",
            "reference_nature_native_shovel_release_many",
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
            "reference_nature_treasure_release_many",
            "reference_nature_combat_reward_begin",
            "reference_nature_combat_reward_status",
            "reference_nature_combat_reward_resolve_next",
            "reference_nature_combat_reward_fork",
            "reference_nature_combat_reward_save",
            "reference_nature_combat_reward_load",
            "reference_nature_combat_reward_release_many",
            "reference_nature_map_generation_begin",
            "reference_nature_map_generation_status",
            "reference_nature_map_generation_resolve_next",
            "reference_nature_map_generation_fork",
            "reference_nature_map_generation_save",
            "reference_nature_map_generation_load",
            "reference_nature_map_generation_release_many"
        ])
    {
        ProbabilityStatuses =
        [
            ChanceLawV111.UnverifiedStatus,
            ChanceLawV111.SourceVerifiedStatus
        ],
        ReferenceNatureRunActionSessionSchemaId =
            sessions.ReferenceNatureRunActionSessionSchemaId,
        ReferenceNatureEventChoiceSessionSchemaId =
            sessions.ReferenceNatureEventChoiceSessionSchemaId,
        ReferenceNatureAncientOfferSessionSchemaId =
            sessions.ReferenceNatureAncientOfferSessionSchemaId,
        ReferenceNatureNeowOfferSessionSchemaId =
            sessions.ReferenceNatureNeowOfferSessionSchemaId,
        ReferenceNatureTrashHeapSessionSchemaId =
            sessions.ReferenceNatureTrashHeapSessionSchemaId,
        ReferenceNatureTabletOfTruthSessionSchemaId =
            sessions.ReferenceNatureTabletOfTruthSessionSchemaId,
        ReferenceNatureTrialSessionSchemaId =
            sessions.ReferenceNatureTrialSessionSchemaId,
        ReferenceNatureNativeEventShuffleSessionSchemaId =
            sessions.ReferenceNatureNativeEventShuffleSessionSchemaId,
        ReferenceNatureNativeEventGoldSessionSchemaId =
            sessions.ReferenceNatureNativeEventGoldSessionSchemaId,
        ReferenceNatureNativeEventRelicSessionSchemaId =
            sessions.ReferenceNatureNativeEventRelicSessionSchemaId,
        ReferenceNatureNativeShovelSessionSchemaId =
            sessions.ReferenceNatureNativeShovelSessionSchemaId,
        ReferenceNatureEndlessConveyorSessionSchemaId =
            sessions.ReferenceNatureEndlessConveyorSessionSchemaId,
        ReferenceNatureGloryReflectionsSessionSchemaId =
            sessions.ReferenceNatureGloryReflectionsSessionSchemaId,
        ReferenceNatureInfestedAutomatonSessionSchemaId =
            sessions.ReferenceNatureInfestedAutomatonSessionSchemaId,
        ReferenceNaturePunchOffRewardSessionSchemaId =
            sessions.ReferenceNaturePunchOffRewardSessionSchemaId,
        ReferenceNaturePunchOffFightSessionSchemaId =
            sessions.ReferenceNaturePunchOffFightSessionSchemaId,
        ReferenceNatureMerchantSessionSchemaId =
            sessions.ReferenceNatureMerchantSessionSchemaId,
        ReferenceNatureTreasureSessionSchemaId =
            sessions.ReferenceNatureTreasureSessionSchemaId,
        ReferenceNatureCombatRewardSessionSchemaId =
            sessions.ReferenceNatureCombatRewardSessionSchemaId,
        ReferenceNatureMapGenerationSessionSchemaId =
            sessions.ReferenceNatureMapGenerationSessionSchemaId,
        ScriptedRunActionSessionSchemaId =
            ScriptedRunActionSessionV111.CurrentSchemaId,
        ScriptedRunActionOperations =
            ScriptedRunActionSessionV111.Operations,
        ScriptedRunSessionSchemaId =
            ScriptedRunSessionV111.CurrentSchemaId,
        ScriptedRunSessionOperations =
            ScriptedRunSessionV111.Operations
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
    private ChanceLawOutcomeV111[]? _outcomes = Copy(Outcomes);

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChanceLawUniformIndexRangeV111? UniformIndexRange { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChanceLawNativeBoundedIndexGridV111?
        NativeBoundedIndexGrid { get; init; }

    /// <summary>
    /// Maximum support size for laws represented as an explicit outcome
    /// array. Larger source requests need a compact law representation.
    /// </summary>
    public const int MaximumExplicitOutcomeCount = 65_536;

    public ChanceLawOutcomeV111[] Outcomes
    {
        get => Copy(_outcomes)!;
        init => _outcomes = Copy(value);
    }

    public const string CurrentSchemaId = "sts2-v111-chance-law-v1";
    public const string UniformIndexRangeSchemaId =
        "sts2-v111-chance-law-v2-uniform-index-range";
    public const string NativeBoundedIndexGridSchemaId =
        "sts2-v111-chance-law-v3-native-nextint-grid";
    public const string UnverifiedStatus = "UNVERIFIED";
    public const string SourceVerifiedStatus = "SOURCE_VERIFIED";
    public const long NativeSourceGridSize = 1L << 53;

    [JsonIgnore]
    public int SupportCount => NativeBoundedIndexGrid?.BoundExclusive ??
        UniformIndexRange?.Count ?? Outcomes.Length;

    public string OutcomeIdAt(int supportOffset) =>
        NativeBoundedIndexGrid is { } nativeRange
            ? nativeRange.OutcomeIdAt(supportOffset)
            : UniformIndexRange is { } range
                ? range.OutcomeIdAt(supportOffset)
                : Outcomes[supportOffset].OutcomeId;

    public long OutcomeWeightAt(int supportOffset) =>
        NativeBoundedIndexGrid is { } nativeRange
            ? ChanceLawEngineV111.NativeNextIntGridWeightAt(
                nativeRange.BoundExclusive, supportOffset)
            : UniformIndexRange is null
                ? Outcomes[supportOffset].Weight : 1L;

    private static ChanceLawOutcomeV111[]? Copy(
        ChanceLawOutcomeV111[]? outcomes) => outcomes is null
        ? null
        : (ChanceLawOutcomeV111[])outcomes.Clone();

    public ChanceLawV111 Fork() => this with
    {
        Outcomes = (ChanceLawOutcomeV111[])Outcomes.Clone()
    };

    public void Validate()
    {
        var outcomes = Outcomes;
        if (outcomes is { Length: > MaximumExplicitOutcomeCount })
            throw new InvalidDataException(
                "Chance Law exceeds the explicit outcome support limit.");
        var compactRange = UniformIndexRange;
        var nativeGrid = NativeBoundedIndexGrid;
        var compactRangeLaw = SchemaId == UniformIndexRangeSchemaId;
        var nativeGridLaw = SchemaId == NativeBoundedIndexGridSchemaId;
        if (SchemaId is not (CurrentSchemaId or UniformIndexRangeSchemaId or
                NativeBoundedIndexGridSchemaId) ||
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
            outcomes is null || TotalWeight <= 0 ||
            (compactRangeLaw
                ? compactRange is null || nativeGrid is not null ||
                    outcomes.Length != 0 ||
                    compactRange.StartInclusive != 0 ||
                    compactRange.Count <= MaximumExplicitOutcomeCount ||
                    compactRange.OutcomeIdPrefix != "choice_index:" ||
                    TotalWeight != compactRange.Count ||
                    ProbabilityStatus != UnverifiedStatus
                : nativeGridLaw
                    ? nativeGrid is null || compactRange is not null ||
                        outcomes.Length != 0 ||
                        nativeGrid.BoundExclusive <=
                            MaximumExplicitOutcomeCount ||
                        nativeGrid.InputGridSize != NativeSourceGridSize ||
                        nativeGrid.OutcomeIdPrefix != "choice_index:" ||
                        OperationKind is not ("bounded_integer" or
                            "choose_index") ||
                        TotalWeight != NativeSourceGridSize ||
                        ProbabilityStatus != SourceVerifiedStatus
                    : compactRange is not null || nativeGrid is not null ||
                        outcomes.Length == 0))
            throw new InvalidDataException("Invalid Chance Law envelope.");

        long total;
        string expectedSupportFingerprint;
        if (nativeGrid is { } nativeRange)
        {
            expectedSupportFingerprint = NativeBoundedIndexGridFingerprint(
                nativeRange);
            total = nativeRange.InputGridSize;
        }
        else if (compactRange is { } range)
        {
            expectedSupportFingerprint = CompactUniformIndexRangeFingerprint(
                range);
            total = range.Count;
        }
        else
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            total = 0;
            foreach (var outcome in outcomes)
            {
                if (outcome is null || string.IsNullOrWhiteSpace(outcome.OutcomeId) ||
                    outcome.Weight <= 0 || !ids.Add(outcome.OutcomeId))
                    throw new InvalidDataException("Invalid Chance Law support.");
                total = checked(total + outcome.Weight);
            }
            expectedSupportFingerprint = CanonicalJson.Sha256(outcomes);
        }

        if (total != TotalWeight ||
            SupportFingerprint != expectedSupportFingerprint)
            throw new InvalidDataException(
                "Chance Law support fingerprint or total weight is inconsistent.");
        var canonicalDeterministicOutcome = compactRange is null &&
            nativeGrid is null &&
            outcomes.Length == 1 &&
            outcomes[0].Weight == 1L;
        if (ProbabilityStatus == SourceVerifiedStatus &&
            TotalWeight != NativeSourceGridSize &&
            !canonicalDeterministicOutcome)
            throw new InvalidDataException(
                "A source-verified Chance Law must cover the complete Native 53-bit input grid or encode one deterministic outcome.");

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

    internal static string CompactUniformIndexRangeFingerprint(
        ChanceLawUniformIndexRangeV111 range) => CanonicalJson.Sha256(new
        {
            supportKind = "uniform_index_range_v1",
            range
        });

    internal static string NativeBoundedIndexGridFingerprint(
        ChanceLawNativeBoundedIndexGridV111 range) => CanonicalJson.Sha256(new
        {
            supportKind = "native_next_int_grid_v1",
            range
        });
}

/// <summary>
/// Outcome-blind queries for supported semantic Nature sites. Queries inspect
/// only declared semantic support and provider constraints; Native RNG state
/// and selected current outcomes are not inputs.
/// </summary>
public static partial class ChanceLawEngineV111
{
    private static readonly string[] LegacyMerchantIndexQuerySites =
    [
        "shop.merchant.card_price_modifier.index_draw",
        "shop.merchant.card_upgrade_roll.ticket",
        "shop.merchant.potion_price_modifier.index_draw",
        "shop.merchant.relic_price_modifier.index_draw"
    ];

    private static readonly string[] LegacyRarityTicketQuerySites =
    [
        "card_rarity.roll.ticket",
        PrototypeNativePotionShop.RarityRollSiteId,
        "relic_grab_bag.rarity.ticket"
    ];

    private static readonly string[] LegacyCardUpgradeTicketQuerySites =
    [
        "card_rarity.upgrade_roll.ticket"
    ];

    private static readonly string[] LegacyRewardCardRarityTicketQuerySites =
    [
        "reward.card_rarity.ticket"
    ];

    private static readonly string[] LegacyRewardCardModelQuerySites =
    [
        "reward.card_model.index_draw"
    ];

    private static readonly string[] LegacyRewardCardTransformQuerySites =
    [
        "reward.card_transform.replacement.index_draw"
    ];

    private static readonly string[] LegacyShopCardRarityQuerySites =
    [
        "shop.card_rarity.ticket"
    ];

    private static readonly string[] LegacyShopCardModelQuerySites =
    [
        "shop.card_model.index_draw"
    ];

    private static readonly string[] LegacyShopCardPriceQuerySites =
    [
        "shop.card.price.index_draw"
    ];

    private static readonly string[] LegacyShopPotionPriceQuerySites =
    [
        "shop.potion_offer.price.index_draw"
    ];

    private static readonly string[] LegacyShopRelicPriceQuerySites =
    [
        "shop.relic_offer.price.index_draw"
    ];

    private static readonly string[] LegacyPotionDropTicketQuerySites =
    [
        "reward.random_potion.drop.ticket"
    ];

    private static readonly string[] LegacyRewardPotionModelQuerySites =
    [
        "reward.random_potion.model"
    ];

    private static readonly string[] LegacyPotionRewardOddsTicketQuerySites =
    [
        "reward.potion_odds.ticket"
    ];

    private static readonly string[] LegacyUncommonPotionModelQuerySites =
    [
        "potion.uncommon_model.index_draw"
    ];

    private static readonly string[] LegacyPotionModelWithinRarityQuerySites =
    [
        PrototypeNativePotionShop.ModelWithinRaritySiteId
    ];

    private static readonly string[] LegacyUniformPotionModelQuerySites =
    [
        PrototypeNativePotionShop.UniformModelSiteId
    ];

    private static readonly string[] LegacyEventRewardPotionModelQuerySites =
    [
        "event.reward.random_potion.model"
    ];

    private static readonly string[] LegacyEventRewardRareCardModelQuerySites =
    [
        "event.reward.random_rare_card.model"
    ];

    private static readonly string[] LegacyEventOfferPotionModelQuerySites =
    [
        "event.reward.offer_potion.model.index_draw"
    ];

    private static readonly string[] LegacyEventModifierCurseModelQuerySites =
    [
        "event.reward.random_modifier_curse.index_draw"
    ];

    private static readonly string[] LegacyEventFillPotionSlotQuerySites =
    [
        "event.potion.fill_empty_slots.model"
    ];

    private static readonly string[] LegacyEliteRelicModelQuerySites =
    [
        "reward.elite_relic.model"
    ];

    private static readonly string[] LegacyTreasureRelicModelQuerySites =
    [
        "treasure.relic_model.index_draw"
    ];

    private static readonly string[] LegacyRelicAcquisitionRareCardQuerySites =
    [
        "relic.acquisition.random_rare_card.model"
    ];

    private static readonly string[] LegacyRelicAcquisitionPotionQuerySites =
    [
        "relic.acquisition.random_potion.model"
    ];

    private static readonly string[] LegacyRunPotionFillSlotQuerySites =
    [
        "run.potion.fill_empty_slots.model"
    ];

    private static readonly string[] UnownedRelicSelectionQuerySites =
    [
        StochasticSiteRegistryV111.UnownedRandomRelicSelectionSiteId
    ];

    private sealed record NativeBoundedIntegerLawSiteV111(
        string StreamId, string Boundary);

    private static readonly Dictionary<string,
        NativeBoundedIntegerLawSiteV111> NativeBoundedIntegerLawSites =
        new(StringComparer.Ordinal)
        {
            [PrototypeNativeRelicGrabBag.QueueShuffleSiteId] = new(
                "run.treasure_room_relics",
                "native-relic-bag-queue-shuffle-index"),
            [StochasticSiteRegistryV111.RewardBossRelicOfferShuffleSiteId] = new(
                NativePlayerRngV111.RewardsStreamId,
                "native-boss-relic-offer-shuffle-index"),
            [StochasticSiteRegistryV111.RewardLavaRockRelicOfferShuffleSiteId] = new(
                NativePlayerRngV111.RewardsStreamId,
                "native-lava-rock-relic-offer-shuffle-index"),
            [NativeFisherYatesV111.SiteId] = new(
                NativeFisherYatesV111.StreamId,
                "native-draw-pile-shuffle-index"),
            [ControlledRecycleShuffleEngineV111.SiteId] = new(
                NativeFisherYatesV111.StreamId,
                "native-recycle-shuffle-index"),
            [PrototypeControlledDrawInsertionV111.PersonalHiveDazedSiteId] = new(
                NativeFisherYatesV111.StreamId,
                "native-personal-hive-dazed-insertion-index"),
            [StochasticSiteRegistryV111.DrawPileRandomInsertSiteId] = new(
                NativeFisherYatesV111.StreamId,
                "native-draw-pile-random-insertion-index"),
            [StochasticSiteRegistryV111.AfterShuffleRelicDrawPileInsertSiteId] = new(
                NativeFisherYatesV111.StreamId,
                "native-after-shuffle-relic-insertion-index"),
            ["combat.enemy.draw.random_insert_index"] = new(
                NativeFisherYatesV111.StreamId,
                "native-enemy-draw-insertion-index"),
            ["combat.enemy.discard.random_insert_index"] = new(
                NativeFisherYatesV111.StreamId,
                "native-enemy-discard-insertion-index")
        };

    private const long NativeNextDoubleGridSize =
        ChanceLawV111.NativeSourceGridSize;

    public static bool IsNativeBoundedIntegerLawSite(string siteId) =>
        NativeBoundedIntegerLawSites.ContainsKey(siteId);

    public static string[] LegacyMerchantIndexQuerySiteIds =>
        (string[])LegacyMerchantIndexQuerySites.Clone();

    public static string[] LegacyRarityTicketQuerySiteIds =>
        (string[])LegacyRarityTicketQuerySites.Clone();

    public static string[] LegacyCardUpgradeTicketQuerySiteIds =>
        (string[])LegacyCardUpgradeTicketQuerySites.Clone();

    public static string[] LegacyRewardCardRarityTicketQuerySiteIds =>
        (string[])LegacyRewardCardRarityTicketQuerySites.Clone();

    public static string[] LegacyRewardCardModelQuerySiteIds =>
        (string[])LegacyRewardCardModelQuerySites.Clone();

    public static string[] LegacyRewardCardTransformQuerySiteIds =>
        (string[])LegacyRewardCardTransformQuerySites.Clone();

    public static string[] LegacyShopCardRarityQuerySiteIds =>
        (string[])LegacyShopCardRarityQuerySites.Clone();

    public static string[] LegacyShopCardModelQuerySiteIds =>
        (string[])LegacyShopCardModelQuerySites.Clone();

    public static string[] LegacyShopCardPriceQuerySiteIds =>
        (string[])LegacyShopCardPriceQuerySites.Clone();

    public static string[] LegacyShopPotionPriceQuerySiteIds =>
        (string[])LegacyShopPotionPriceQuerySites.Clone();

    public static string[] LegacyShopRelicPriceQuerySiteIds =>
        (string[])LegacyShopRelicPriceQuerySites.Clone();

    public static string[] LegacyCallerBoundIndexQuerySiteIds =>
        StochasticSiteRegistryV111.LegacyCallerBoundIndexQuerySiteIds;

    public static bool IsLegacyCallerBoundIndexQuerySite(string siteId) =>
        StochasticSiteRegistryV111.IsLegacyCallerBoundIndexQuerySite(siteId);

    public static string[] LegacyPotionDropTicketQuerySiteIds =>
        (string[])LegacyPotionDropTicketQuerySites.Clone();

    public static string[] LegacyRewardPotionModelQuerySiteIds =>
        (string[])LegacyRewardPotionModelQuerySites.Clone();

    public static string[] LegacyPotionRewardOddsTicketQuerySiteIds =>
        (string[])LegacyPotionRewardOddsTicketQuerySites.Clone();

    public static string[] LegacyUncommonPotionModelQuerySiteIds =>
        (string[])LegacyUncommonPotionModelQuerySites.Clone();

    public static string[] LegacyPotionModelWithinRarityQuerySiteIds =>
        (string[])LegacyPotionModelWithinRarityQuerySites.Clone();

    public static string[] LegacyUniformPotionModelQuerySiteIds =>
        (string[])LegacyUniformPotionModelQuerySites.Clone();

    public static string[] LegacyEventRewardPotionModelQuerySiteIds =>
        (string[])LegacyEventRewardPotionModelQuerySites.Clone();

    public static string[] LegacyEventRewardRareCardModelQuerySiteIds =>
        (string[])LegacyEventRewardRareCardModelQuerySites.Clone();

    public static string[] LegacyEventOfferPotionModelQuerySiteIds =>
        (string[])LegacyEventOfferPotionModelQuerySites.Clone();

    public static string[] LegacyEventModifierCurseModelQuerySiteIds =>
        (string[])LegacyEventModifierCurseModelQuerySites.Clone();

    public static string[] LegacyEventFillPotionSlotQuerySiteIds =>
        (string[])LegacyEventFillPotionSlotQuerySites.Clone();

    public static string[] LegacyEliteRelicModelQuerySiteIds =>
        (string[])LegacyEliteRelicModelQuerySites.Clone();

    public static string[] LegacyTreasureRelicModelQuerySiteIds =>
        (string[])LegacyTreasureRelicModelQuerySites.Clone();

    public static string[] LegacyRelicAcquisitionRareCardQuerySiteIds =>
        (string[])LegacyRelicAcquisitionRareCardQuerySites.Clone();

    public static string[] LegacyRelicAcquisitionPotionQuerySiteIds =>
        (string[])LegacyRelicAcquisitionPotionQuerySites.Clone();

    public static string[] LegacyRunPotionFillSlotQuerySiteIds =>
        (string[])LegacyRunPotionFillSlotQuerySites.Clone();

    public static string[] UnownedRelicSelectionQuerySiteIds =>
        (string[])UnownedRelicSelectionQuerySites.Clone();

    /// <summary>
    /// Query the retained legacy 50/33/17 relic rarity ticket. The merchant
    /// flag selects the exact owner suffix used by the legacy call.
    /// </summary>
    public static ChanceLawV111 QueryLegacyRelicRarityTicket(
        string ownerId, bool merchant, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "relic_grab_bag.rarity.ticket";
        const string streamId = "reward";
        var requestOwnerId = ownerId + (merchant ? ":merchant" : ":reward");
        var outcomes = LegacyRarityOutcomes(50, 33, 17);
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId = requestOwnerId,
            ownerPrefix = ownerId,
            ordinal,
            merchant,
            ticketCount = 100,
            commonTicketEnd = 50,
            uncommonTicketEnd = 83
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, requestOwnerId, ordinal,
            "legacy-relic-rarity-ticket-v1", conditioning,
            "uniform 100-way legacy bounded-ticket model with source-shaped " +
            "50/33/17 rarity bins; probabilities remain unverified against " +
            "the native game RNG.", outcomes);
    }

    private static string[] RemainingRewardCardPool(
        string[] priorCardIds, PrototypeCardType? requiredType = null)
    {
        ArgumentNullException.ThrowIfNull(priorCardIds);
        var pool = PrototypeContent.RewardCardPool.Where(cardId =>
                requiredType is null ||
                PrototypeContent.Card(cardId).Type == requiredType.Value)
            .ToArray();
        var poolSet = pool.ToHashSet(StringComparer.Ordinal);
        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cardId in priorCardIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
            if (!poolSet.Contains(cardId) || !selected.Add(cardId))
                throw new ArgumentException(
                    "Prior card IDs must be unique members of the declared source card pool.",
                    nameof(priorCardIds));
        }

        return pool.Where(cardId => !selected.Contains(cardId)).ToArray();
    }

private static ChanceLawOutcomeV111[] LegacyRarityOutcomes(
        long common, long uncommon, long rare) =>
        new[]
        {
            new ChanceLawOutcomeV111("rarity:common", common),
            new ChanceLawOutcomeV111("rarity:uncommon", uncommon),
            new ChanceLawOutcomeV111("rarity:rare", rare)
        }.Where(outcome => outcome.Weight > 0).ToArray();

    private static ChanceLawV111 CreateLegacyCategoricalLaw(
        string siteId, string streamId, string ownerId, int ordinal,
        string conditioningPolicyId, object conditioning,
        string probabilityProvenance, ChanceLawOutcomeV111[] outcomes,
        string operationKind = "bounded_integer")
    {
        var conditioningFingerprint = CanonicalJson.Sha256(conditioning);
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            streamId,
            ownerId,
            ordinal,
            conditioningFingerprint
        });
        return Create(eventId, siteId, operationKind, ownerId,
            ordinal, conditioningPolicyId, conditioningFingerprint,
            probabilityProvenance, outcomes);
    }

    /// <summary>
    /// Query an allowlisted legacy index request using its fixed stream and
    /// source operation. The caller declares the owner and current support.
    /// </summary>
    public static ChanceLawV111 QueryLegacyCallerBoundIndex(
        string siteId, string ownerId, int ordinal, int supportCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (!StochasticSiteRegistryV111.TryGetLegacyCallerBoundIndexQuerySite(
                siteId, out var site))
            throw new NotSupportedException(
                $"Legacy bounded-index Chance Law does not support site '{siteId}'.");
        if (supportCount < site.MinimumSupportCount)
            throw new ArgumentOutOfRangeException(nameof(supportCount),
                $"The declared support for '{siteId}' must be at least {site.MinimumSupportCount}.");
        if (site.ExactSupportCount is { } exactSupportCount &&
            supportCount != exactSupportCount)
            throw new ArgumentException(
                $"The declared support for '{siteId}' must be {exactSupportCount}.",
                nameof(supportCount));

        var request = site.OperationKind == "bounded_integer"
            ? new RandomIndexRequestV111(
                siteId, site.StreamId, ownerId, ordinal, [],
                OperationKind: site.OperationKind,
                BoundedCount: supportCount)
            : new RandomIndexRequestV111(
                siteId, site.StreamId, ownerId, ordinal,
                Enumerable.Range(0, supportCount).ToArray(),
                OperationKind: site.OperationKind);
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "legacy-caller-bounded-index-request",
            request
        });
        return QueryRandomIndex(eventId, request);
    }

    /// <summary>
    /// Query an allowlisted Native bounded-integer site using its fixed source
    /// stream. The caller declares the current exclusive bound, owner, and
    /// ordinal; site-specific pool and prefix checks remain outside this law.
    /// </summary>
    public static ChanceLawV111 QueryNativeBoundedIntegerLawSite(
        string siteId, string ownerId, int supportCount, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        if (!NativeBoundedIntegerLawSites.TryGetValue(
                siteId, out var site))
            throw new NotSupportedException(
                $"Native bounded-integer law does not support site '{siteId}'.");
        return QueryNativeBoundedIntegerIndex(
            siteId, site.StreamId, ownerId, supportCount, ordinal,
            site.Boundary);
    }

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
        RequireExplicitOutcomeSupport(boundExclusive);

        var weights = new long[boundExclusive];
        long total = 0;
        for (var index = 0; index < boundExclusive; index++)
        {
            var weight = NativeNextIntGridWeightAt(boundExclusive, index);
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

    internal static long NativeNextIntGridWeightAt(
        int boundExclusive, int outcomeIndex)
    {
        if (boundExclusive <= 0)
            throw new ArgumentOutOfRangeException(nameof(boundExclusive));
        if ((uint)outcomeIndex >= (uint)boundExclusive)
            throw new ArgumentOutOfRangeException(nameof(outcomeIndex));
        var first = outcomeIndex == 0 ? 0 :
            NativeNextIntBoundaryGridIndex(outcomeIndex, boundExclusive);
        var end = NativeNextIntBoundaryGridIndex(
            checked(outcomeIndex + 1), boundExclusive);
        return checked(end - first);
    }

    internal static int NativeNextIntOutcomeFromGrid(
        int boundExclusive, long gridIndex)
    {
        if (boundExclusive <= 0)
            throw new ArgumentOutOfRangeException(nameof(boundExclusive));
        if (gridIndex < 0 || gridIndex >= NativeNextDoubleGridSize)
            throw new ArgumentOutOfRangeException(nameof(gridIndex));
        var unit = gridIndex * (1.0 / NativeNextDoubleGridSize);
        var outcome = checked((int)(long)(unit * boundExclusive));
        if ((uint)outcome >= (uint)boundExclusive)
            throw new InvalidDataException(
                "Native bounded-index grid transform escaped its declared support.");
        return outcome;
    }

    private static void RequireExplicitOutcomeSupport(int supportCount)
    {
        if (supportCount > ChanceLawV111.MaximumExplicitOutcomeCount)
            throw new NotSupportedException(
                $"Chance Law support of {supportCount} outcomes exceeds the " +
                $"explicit limit of {ChanceLawV111.MaximumExplicitOutcomeCount}; " +
                "a compact law representation is required.");
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
        NativePotionFactoryRarityGridWeights()
    {
        var rare = NativeNextFloatGridCountAtOrBelow(0.1f);
        var uncommonEnd = NativeNextFloatGridCountAtOrBelow(0.35f);
        if (uncommonEnd < rare)
            throw new InvalidDataException(
                "Native PotionFactory rarity thresholds are out of order.");
        var uncommon = checked(uncommonEnd - rare);
        var common = checked(NativeNextDoubleGridSize - uncommonEnd);
        return (common, uncommon, rare);
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

    public const string NativeRandomIndexConditioningPolicyId =
        "source-native-random-index-nextint-grid-v1";
    public const string RandomIndexConditioningPolicyId =
        "uniform-saved-random-index-support-v1";

    /// <summary>Query legal indices in a saved typed random request. Canonical
    /// Native stream IDs use exact NextInt grid masses; legacy or unknown
    /// streams retain the unverified uniform request-index model. Only the
    /// typed request enters the law fingerprint; selected outcomes, enclosing
    /// run state and private RNG state do not.</summary>
    public static ChanceLawV111 QueryRandomIndex(
        string eventId, RandomIndexRequestV111 request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        var nativeIndexLaw = NativeRngV111.IsCanonicalStreamId(
            request.StreamId);
        if (request.SupportCount > ChanceLawV111.MaximumExplicitOutcomeCount)
        {
            if (nativeIndexLaw)
            {
                var nativeConditioningFingerprint = CanonicalJson.Sha256(new
                {
                    RequestFingerprint = CanonicalJson.Sha256(request),
                    policy = NativeRandomIndexConditioningPolicyId
                });
                return CreateCompactNativeBoundedIndexGrid(
                    eventId, request.SiteId, request.OperationKind,
                    request.OwnerId, request.Ordinal,
                    nativeConditioningFingerprint, request.SupportCount);
            }
            if (!nativeIndexLaw &&
                request.OperationKind == "bounded_integer" &&
                request.BoundedCount == request.SupportCount)
            {
                var compactConditioningFingerprint = CanonicalJson.Sha256(new
                {
                    RequestFingerprint = CanonicalJson.Sha256(request),
                    policy = RandomIndexConditioningPolicyId
                });
                return CreateCompactUniformIndexRange(
                    eventId, request.SiteId, request.OperationKind,
                    request.OwnerId, request.Ordinal,
                    RandomIndexConditioningPolicyId,
                    compactConditioningFingerprint,
                    "uniform-saved-index-support-unverified",
                    request.SupportCount);
            }
            RequireExplicitOutcomeSupport(request.SupportCount);
        }
        var policy = nativeIndexLaw
            ? NativeRandomIndexConditioningPolicyId
            : RandomIndexConditioningPolicyId;
        var weights = nativeIndexLaw
            ? NativeNextIntGridWeights(request.SupportCount)
            : Enumerable.Repeat(1L, request.SupportCount).ToArray();
        var outcomes = Enumerable.Range(0,
                request.SupportCount)
            .Select(index => new ChanceLawOutcomeV111(
                "choice_index:" + index.ToString(CultureInfo.InvariantCulture),
                weights[index]))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            RequestFingerprint = CanonicalJson.Sha256(request),
            policy
        });
        return Create(eventId, request.SiteId,
            request.OperationKind, request.OwnerId, request.Ordinal,
            policy, conditioningFingerprint,
            nativeIndexLaw
                ? NativeBoundedIndexSourceVerifiedProvenance
                : "uniform-saved-index-support-unverified", outcomes,
            nativeIndexLaw
                ? ChanceLawV111.SourceVerifiedStatus
                : ChanceLawV111.UnverifiedStatus);
    }

    private static ChanceLawV111 CreateCompactUniformIndexRange(
        string eventId, string siteId, string operationKind,
        string ownerId, int ordinal, string conditioningPolicyId,
        string conditioningFingerprint, string probabilityProvenance,
        int supportCount)
    {
        if (supportCount <= ChanceLawV111.MaximumExplicitOutcomeCount)
            throw new ArgumentOutOfRangeException(nameof(supportCount));
        var range = new ChanceLawUniformIndexRangeV111(
            0, supportCount, "choice_index:");
        var schemaId = ChanceLawV111.UniformIndexRangeSchemaId;
        var supportFingerprint =
            ChanceLawV111.CompactUniformIndexRangeFingerprint(range);
        var lawId = CanonicalJson.Sha256(new
        {
            SchemaId = schemaId,
            SiteId = siteId,
            OperationKind = operationKind,
            OwnerId = ownerId,
            Ordinal = ordinal,
            SupportFingerprint = supportFingerprint,
            ConditioningPolicyId = conditioningPolicyId,
            ConditioningFingerprint = conditioningFingerprint,
            ProbabilityStatus = ChanceLawV111.UnverifiedStatus,
            ProbabilityProvenance = probabilityProvenance
        });
        var law = new ChanceLawV111(
            schemaId, lawId, eventId, siteId, operationKind, ownerId,
            ordinal, supportFingerprint, conditioningPolicyId,
            conditioningFingerprint, ChanceLawV111.UnverifiedStatus,
            probabilityProvenance, [], supportCount)
        {
            UniformIndexRange = range
        };
        law.Validate();
        return law;
    }

    private static ChanceLawV111 CreateCompactNativeBoundedIndexGrid(
        string eventId, string siteId, string operationKind,
        string ownerId, int ordinal, string conditioningFingerprint,
        int supportCount)
    {
        if (supportCount <= ChanceLawV111.MaximumExplicitOutcomeCount)
            throw new ArgumentOutOfRangeException(nameof(supportCount));
        var range = new ChanceLawNativeBoundedIndexGridV111(
            supportCount, ChanceLawV111.NativeSourceGridSize,
            "choice_index:");
        var schemaId = ChanceLawV111.NativeBoundedIndexGridSchemaId;
        var supportFingerprint =
            ChanceLawV111.NativeBoundedIndexGridFingerprint(range);
        var lawId = CanonicalJson.Sha256(new
        {
            SchemaId = schemaId,
            SiteId = siteId,
            OperationKind = operationKind,
            OwnerId = ownerId,
            Ordinal = ordinal,
            SupportFingerprint = supportFingerprint,
            ConditioningPolicyId = NativeRandomIndexConditioningPolicyId,
            ConditioningFingerprint = conditioningFingerprint,
            ProbabilityStatus = ChanceLawV111.SourceVerifiedStatus,
            ProbabilityProvenance =
                NativeBoundedIndexSourceVerifiedProvenance
        });
        var law = new ChanceLawV111(
            schemaId, lawId, eventId, siteId, operationKind, ownerId,
            ordinal, supportFingerprint,
            NativeRandomIndexConditioningPolicyId,
            conditioningFingerprint, ChanceLawV111.SourceVerifiedStatus,
            NativeBoundedIndexSourceVerifiedProvenance, [],
            ChanceLawV111.NativeSourceGridSize)
        {
            NativeBoundedIndexGrid = range
        };
        law.Validate();
        return law;
    }

    internal static ChanceLawV111 QueryUniformLabeledIndexSupport(
        string eventId, RandomIndexRequestV111 request,
        string[] candidateLabels, string conditioningPolicyId,
        string probabilityProvenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateLabels);
        ArgumentException.ThrowIfNullOrWhiteSpace(conditioningPolicyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(probabilityProvenance);
        request.Validate();
        if (request.OperationKind != "choose_index" ||
            candidateLabels.Length != request.SupportCount ||
            candidateLabels.Any(string.IsNullOrWhiteSpace) ||
            candidateLabels.Distinct(StringComparer.Ordinal).Count() !=
                candidateLabels.Length)
            throw new ArgumentException(
                "Labeled index support must match the request and contain distinct labels.",
                nameof(candidateLabels));

        var outcomes = candidateLabels.Select((label, index) =>
            new ChanceLawOutcomeV111(
                $"{label}:index:{index.ToString(CultureInfo.InvariantCulture)}",
                1L)).ToArray();
        return Create(eventId, request.SiteId, request.OperationKind,
            request.OwnerId, request.Ordinal, conditioningPolicyId,
            CanonicalJson.Sha256(new
            {
                RequestFingerprint = CanonicalJson.Sha256(request),
                CandidateLabels = candidateLabels
            }), probabilityProvenance, outcomes);
    }

    /// <summary>
    /// Query the raw Native relic rarity threshold for a caller-declared
    /// Rewards-stream request. Queue identity and tier fallback are separate.
    /// </summary>
    public static ChanceLawV111 QueryNativeRelicRarity(
        RandomUnitFloatRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        const string siteId = "relic_grab_bag.rarity.unit_float";
        if (request.SiteId != siteId ||
            request.StreamId != NativePlayerRngV111.RewardsStreamId)
            throw new ArgumentException(
                "Native relic rarity requests require the player.rewards stream.",
                nameof(request));

        var (common, uncommon, rare) = NativeRelicRarityGridWeights();
        var outcomes = new[]
            {
                new ChanceLawOutcomeV111("rarity:common", common),
                new ChanceLawOutcomeV111("rarity:uncommon", uncommon),
                new ChanceLawOutcomeV111("rarity:rare", rare)
            }
            .Where(outcome => outcome.Weight > 0).ToArray();
        var conditioning = new
        {
            request,
            commonThreshold = PrototypeNativeRelicGrabBag
                .CommonRarityWeight /
                (float)PrototypeNativeRelicGrabBag.RarityWeightScale,
            uncommonThreshold = (PrototypeNativeRelicGrabBag
                .CommonRarityWeight +
                PrototypeNativeRelicGrabBag.UncommonRarityWeight) /
                (float)PrototypeNativeRelicGrabBag.RarityWeightScale,
            common,
            uncommon,
            rare
        };
        return CreateNativeBoundaryLaw(
            siteId, "unit_float_threshold", request.OwnerId,
            request.Ordinal, "native-relic-rarity-float-threshold-v1",
            CanonicalJson.Sha256(conditioning),
            NativeUnitFloatThresholdSourceVerifiedProvenance,
            outcomes, ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query one Native relic-bag Fisher-Yates index over its caller-declared
    /// current bound. Queue eligibility and the preceding shuffle prefix are
    /// not inspected.
    /// </summary>
    public static ChanceLawV111 QueryNativeRelicBagQueueShuffleIndex(
        string ownerId, int supportCount, int ordinal)
    {
        return QueryNativeBoundedIntegerLawSite(
            PrototypeNativeRelicGrabBag.QueueShuffleSiteId, ownerId,
            supportCount, ordinal);
    }

    private static ChanceLawV111 QueryNativeBoundedIntegerIndex(
        string siteId, string streamId, string ownerId,
        int supportCount, int ordinal, string boundary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundary);
        if (!NativeBoundedIntegerLawSites.ContainsKey(siteId))
            throw new NotSupportedException(
                $"Native bounded-integer law does not support site '{siteId}'.");
        if (supportCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(supportCount));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var request = new RandomIndexRequestV111(
            siteId, streamId, ownerId, ordinal, Array.Empty<int>(),
            OperationKind: "bounded_integer", BoundedCount: supportCount);
        var eventId = CanonicalJson.Sha256(new { boundary, request });
        return QueryRandomIndex(eventId, request);
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

    private static ChanceLawV111 Create(
        string eventId, string siteId, string operationKind, string ownerId,
        int ordinal, string conditioningPolicyId,
        string conditioningFingerprint, string probabilityProvenance,
        ChanceLawOutcomeV111[] outcomes,
        string probabilityStatus = ChanceLawV111.UnverifiedStatus)
    {
        RequireExplicitOutcomeSupport(outcomes.Length);
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
