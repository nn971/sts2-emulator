using System.Text.Json;
using System.Threading;

namespace Sts2Emulator.Core;

/// <summary>
/// Opt-in diagnostic journal at the *actual shared random-choice provider
/// boundary*. Never exposed as a fair/player-visible observation: it contains
/// hidden stream ownership and (native mode only) RNG cursor words. It does
/// not itself create or resolve a random choice.
/// </summary>
public sealed record RandomBoundaryEventV111(
    int Sequence,
    RandomIndexRequestV111 Request,
    int SelectedIndex,
    int SelectedValue,
    string ProviderMode,
    NativeRngSnapshot? NativeCursorBefore,
    NativeRngSnapshot? NativeCursorAfter)
{
    public RandomBoundaryEventV111 Fork() => this with
    {
        Request = Request with
        {
            OrderedCandidates = Request.OrderedCandidates
        }
    };
}

public sealed record RandomUnitFloatBoundaryEventV111(
    int Sequence,
    RandomUnitFloatRequestV111 Request,
    float SelectedValue,
    string ProviderMode,
    NativeRngSnapshot? NativeCursorBefore,
    NativeRngSnapshot? NativeCursorAfter);

public sealed record RandomRangedFloatBoundaryEventV111(
    int Sequence,
    RandomRangedFloatRequestV111 Request,
    float SelectedValue,
    string ProviderMode,
    NativeRngSnapshot? NativeCursorBefore,
    NativeRngSnapshot? NativeCursorAfter);

public sealed record RandomRangedDoubleBoundaryEventV111(
    int Sequence,
    RandomRangedDoubleRequestV111 Request,
    double SelectedValue,
    string ProviderMode,
    NativeRngSnapshot? NativeCursorBefore,
    NativeRngSnapshot? NativeCursorAfter);

public sealed record RandomGaussianIntBoundaryEventV111(
    int Sequence,
    RandomGaussianIntRequestV111 Request,
    int SelectedValue,
    string ProviderMode,
    NativeRngSnapshot? NativeCursorBefore,
    NativeRngSnapshot? NativeCursorAfter);

public sealed record RandomGaussianDoubleBoundaryEventV111(
    int Sequence,
    RandomGaussianDoubleRequestV111 Request,
    double SelectedValue,
    string ProviderMode,
    NativeRngSnapshot? NativeCursorBefore,
    NativeRngSnapshot? NativeCursorAfter);

/// <summary>One entry in the mixed-operation order produced by an actual
/// emulator transition. Event is the serialized typed boundary payload.</summary>
public sealed record RandomBoundaryTranscriptEntryV111(
    int Sequence, string OperationKind, JsonElement Event,
    string SchemaId = "sts2-v111-random-boundary-transcript-v1")
{
    private static readonly JsonSerializerOptions TranscriptJsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

    public RandomBoundaryTranscriptEntryV111 Fork() => this with
    {
        Event = Event.Clone()
    };

    public void Validate()
    {
        if (SchemaId != "sts2-v111-random-boundary-transcript-v1" ||
            Sequence < 1 ||
            OperationKind is not ("index" or "unit_float" or
                "ranged_float" or "ranged_double" or "gaussian_int" or
                "gaussian_double") || Event.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(
                "Invalid ordered random-boundary transcript entry.");

        switch (OperationKind)
        {
            case "index":
                ValidateEvent(DeserializeEvent<RandomBoundaryEventV111>());
                break;
            case "unit_float":
                ValidateEvent(DeserializeEvent<RandomUnitFloatBoundaryEventV111>());
                break;
            case "ranged_float":
                ValidateEvent(DeserializeEvent<RandomRangedFloatBoundaryEventV111>());
                break;
            case "ranged_double":
                ValidateEvent(DeserializeEvent<RandomRangedDoubleBoundaryEventV111>());
                break;
            case "gaussian_int":
                ValidateEvent(DeserializeEvent<RandomGaussianIntBoundaryEventV111>());
                break;
            case "gaussian_double":
                ValidateEvent(DeserializeEvent<RandomGaussianDoubleBoundaryEventV111>());
                break;
        }
    }

    internal T DeserializeEvent<T>() where T : class =>
        Event.Deserialize<T>(TranscriptJsonOptions) ??
        throw new InvalidDataException(
            "Ordered random-boundary entry has no typed event payload.");

    internal string ReadProviderMode()
    {
        if (!Event.TryGetProperty("provider_mode", out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException(
                "Ordered random-boundary entry has no provider mode.");
        return value.GetString()!;
    }

    internal static JsonElement SerializeEvent(object boundaryEvent) =>
        JsonSerializer.SerializeToElement(
            boundaryEvent, boundaryEvent.GetType(), TranscriptJsonOptions);

    private static void ValidateEvent(RandomBoundaryEventV111 value)
    {
        if (value.Sequence < 1 || value.Request is null)
            throw new InvalidDataException(
                "Ordered index boundary event has invalid metadata.");
        var decision = new RandomIndexDecisionV111(
            value.Request, value.SelectedIndex, value.ProviderMode,
            value.NativeCursorBefore, value.NativeCursorAfter);
        decision.Validate();
        if (value.SelectedValue != decision.SelectedValue)
            throw new InvalidDataException(
                "Ordered index boundary value disagrees with its selected index.");
    }

    private static void ValidateEvent(
        RandomUnitFloatBoundaryEventV111 value)
    {
        if (value.Sequence < 1 || value.Request is null)
            throw new InvalidDataException(
                "Ordered unit-float boundary event has invalid metadata.");
        new RandomUnitFloatDecisionV111(value.Request, value.SelectedValue,
            value.ProviderMode, value.NativeCursorBefore,
            value.NativeCursorAfter).Validate();
    }

    private static void ValidateEvent(RandomRangedFloatBoundaryEventV111 value)
    {
        if (value.Sequence < 1 || value.Request is null)
            throw new InvalidDataException(
                "Ordered ranged-float boundary event has invalid metadata.");
        new RandomRangedFloatDecisionV111(value.Request, value.SelectedValue,
            value.ProviderMode, value.NativeCursorBefore,
            value.NativeCursorAfter).Validate();
    }

    private static void ValidateEvent(RandomRangedDoubleBoundaryEventV111 value)
    {
        if (value.Sequence < 1 || value.Request is null)
            throw new InvalidDataException(
                "Ordered ranged-double boundary event has invalid metadata.");
        new RandomRangedDoubleDecisionV111(value.Request, value.SelectedValue,
            value.ProviderMode, value.NativeCursorBefore,
            value.NativeCursorAfter).Validate();
    }

    private static void ValidateEvent(RandomGaussianIntBoundaryEventV111 value)
    {
        if (value.Sequence < 1 || value.Request is null)
            throw new InvalidDataException(
                "Ordered Gaussian-integer boundary event has invalid metadata.");
        new RandomGaussianIntDecisionV111(value.Request, value.SelectedValue,
            value.ProviderMode, value.NativeCursorBefore,
            value.NativeCursorAfter).Validate();
    }

    private static void ValidateEvent(
        RandomGaussianDoubleBoundaryEventV111 value)
    {
        if (value.Sequence < 1 || value.Request is null)
            throw new InvalidDataException(
                "Ordered Gaussian-double boundary event has invalid metadata.");
        new RandomGaussianDoubleDecisionV111(value.Request,
            value.SelectedValue, value.ProviderMode,
            value.NativeCursorBefore, value.NativeCursorAfter).Validate();
    }
}

/// <summary>Semantic identity paired with a Native index decision whose
/// primitive request uses positional indices instead of physical card IDs.</summary>
public sealed record SemanticCardInstanceIndexAuditV111(
    string SchemaId, string EventId, string SupportFingerprint,
    RandomIndexRequestV111 Request, long[] OrderedCardInstanceIds,
    int SelectedIndex, long SelectedCardInstanceId,
    RandomIndexDecisionV111 ProviderDecision)
{
    public const string CurrentSchemaId =
        "sts2-v111-semantic-card-instance-index-audit-v1";

    private long[]? _orderedCardInstanceIds =
        CloneIds(OrderedCardInstanceIds);

    public long[] OrderedCardInstanceIds
    {
        get => CloneIds(_orderedCardInstanceIds)!;
        init => _orderedCardInstanceIds = CloneIds(value);
    }

    public SemanticCardInstanceIndexAuditV111 Fork() => this with
    {
        Request = Request with
        {
            OrderedCandidates = (int[])Request.OrderedCandidates.Clone()
        },
        OrderedCardInstanceIds = CloneIds(_orderedCardInstanceIds)!,
        ProviderDecision = CloneDecision(ProviderDecision)
    };

    private static long[]? CloneIds(long[]? values) => values is null
        ? null : (long[])values.Clone();

    private static RandomIndexDecisionV111 CloneDecision(
        RandomIndexDecisionV111 value) => value with
    {
        Request = value.Request with
        {
            OrderedCandidates = (int[])value.Request.OrderedCandidates.Clone()
        }
    };

    public static SemanticCardInstanceIndexAuditV111 Create(
        RandomIndexDecisionV111 providerDecision,
        IReadOnlyList<long> orderedCardInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(providerDecision);
        ArgumentNullException.ThrowIfNull(orderedCardInstanceIds);
        providerDecision.Validate();
        var request = providerDecision.Request;
        var identities = orderedCardInstanceIds.ToArray();
        if (providerDecision.ProviderMode != "native-v111" ||
            request.OperationKind != "choose_index" ||
            !StochasticSiteRegistryV111.IsCombatCardInstanceIndexAuditSite(
                request.SiteId) ||
            request.SupportCount != identities.Length ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, identities.Length)) ||
            identities.Length == 0 || identities.Any(id => id <= 0) ||
            identities.Distinct().Count() != identities.Length ||
            providerDecision.SelectedIndex < 0 ||
            providerDecision.SelectedIndex >= identities.Length)
            throw new InvalidDataException(
                "Native card-instance index audit has an unsupported request or identity support.");

        var supportFingerprint = CanonicalJson.Sha256(identities);
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = CurrentSchemaId,
            request,
            supportFingerprint,
            providerDecision
        });
        var outcome = new SemanticCardInstanceIndexAuditV111(
            CurrentSchemaId, eventId, supportFingerprint,
            request with
            {
                OrderedCandidates = (int[])request.OrderedCandidates.Clone()
            }, identities, providerDecision.SelectedIndex,
            identities[providerDecision.SelectedIndex],
            CloneDecision(providerDecision));
        outcome.Validate();
        return outcome;
    }

    public void Validate()
    {
        var identities = _orderedCardInstanceIds;
        ArgumentNullException.ThrowIfNull(Request);
        ArgumentNullException.ThrowIfNull(ProviderDecision);
        Request.ValidateForNativeProvider();
        ProviderDecision.Validate();
        if (SchemaId != CurrentSchemaId ||
            ProviderDecision.ProviderMode != "native-v111" ||
            ProviderDecision.SelectedIndex != SelectedIndex ||
            CanonicalJson.Sha256(ProviderDecision.Request) !=
                CanonicalJson.Sha256(Request) ||
            !StochasticSiteRegistryV111.IsCombatCardInstanceIndexAuditSite(
                Request.SiteId) ||
            Request.OperationKind != "choose_index" || identities is null ||
            identities.Length == 0 ||
            !Request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, identities.Length)) ||
            identities.Any(identity => identity <= 0) ||
            identities.Distinct().Count() != identities.Length ||
            Request.SupportCount != identities.Length ||
            SelectedIndex < 0 || SelectedIndex >= identities.Length ||
            SelectedCardInstanceId != identities[SelectedIndex] ||
            SupportFingerprint != CanonicalJson.Sha256(identities) ||
            EventId != CanonicalJson.Sha256(new
            {
                schemaId = SchemaId,
                request = Request,
                supportFingerprint = SupportFingerprint,
                providerDecision = ProviderDecision
            }))
            throw new InvalidDataException(
                "Invalid Native card-instance index audit outcome.");
    }
}

/// <summary>Native provider provenance paired with the existing semantic cost
/// decision and its ordered physical card support.</summary>
public sealed record SemanticCardEnergyCostAuditV111(
    string SchemaId, string EventId, string EligibleCardSupportFingerprint,
    long[] OrderedEligibleCardInstanceIds,
    SemanticCostDecisionV111 Outcome,
    RandomIndexDecisionV111 ProviderDecision)
{
    public const string CurrentSchemaId =
        "sts2-v111-semantic-card-energy-cost-audit-v1";

    private long[]? _orderedEligibleCardInstanceIds =
        CloneIds(OrderedEligibleCardInstanceIds);

    public long[] OrderedEligibleCardInstanceIds
    {
        get => CloneIds(_orderedEligibleCardInstanceIds)!;
        init => _orderedEligibleCardInstanceIds = CloneIds(value);
    }

    public SemanticCardEnergyCostAuditV111 Fork() => this with
    {
        OrderedEligibleCardInstanceIds =
            CloneIds(_orderedEligibleCardInstanceIds)!,
        Outcome = Outcome.Fork(),
        ProviderDecision = CloneDecision(ProviderDecision)
    };

    private static long[]? CloneIds(long[]? values) => values is null
        ? null : (long[])values.Clone();

    private static RandomIndexDecisionV111 CloneDecision(
        RandomIndexDecisionV111 value) => value with
    {
        Request = value.Request with
        {
            OrderedCandidates = (int[])value.Request.OrderedCandidates.Clone()
        }
    };

    public static SemanticCardEnergyCostAuditV111 Create(
        RandomIndexDecisionV111 providerDecision,
        IReadOnlyList<long> orderedEligibleCardInstanceIds,
        long cardInstanceId, string cardModelId)
    {
        ArgumentNullException.ThrowIfNull(providerDecision);
        ArgumentNullException.ThrowIfNull(orderedEligibleCardInstanceIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardModelId);
        providerDecision.Validate();
        var request = providerDecision.Request;
        var identities = orderedEligibleCardInstanceIds.ToArray();
        var supportFingerprint = CanonicalJson.Sha256(identities);
        bool handCost = request.SiteId ==
            StochasticSiteRegistryV111.CombatRandomHandCardEnergyCostSiteId;
        bool drawHookCost = request.SiteId is
            StochasticSiteRegistryV111.CombatDrawRelicEnergyCostSiteId or
            StochasticSiteRegistryV111.CombatDrawSlitherEnergyCostSiteId;
        var expectedOwner = "card_instance:" + cardInstanceId.ToString(
            System.Globalization.CultureInfo.InvariantCulture) + ":model:" +
            cardModelId + ":stage:" +
            (request.SiteId ==
                StochasticSiteRegistryV111.CombatDrawRelicEnergyCostSiteId
                    ? "relic"
                    : "slither");
        if (providerDecision.ProviderMode != "native-v111" ||
            (!handCost && !drawHookCost) ||
            request.OperationKind != "bounded_integer" ||
            request.BoundedCount is not > 0 ||
            identities.Length == 0 || identities.Any(id => id <= 0) ||
            identities.Distinct().Count() != identities.Length ||
            cardInstanceId <= 0 || !identities.Contains(cardInstanceId) ||
            (handCost && !request.OwnerId.EndsWith(
                ":hand:" + supportFingerprint + ":card_instance:" +
                    cardInstanceId.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal)) ||
            (drawHookCost &&
                (identities.Length != 1 || identities[0] != cardInstanceId ||
                 request.OwnerId != expectedOwner)))
            throw new InvalidDataException(
                "Native card energy-cost audit has an unsupported request or card support.");

        var boundaryFingerprint = CanonicalJson.Sha256(new
        {
            schemaId = CurrentSchemaId,
            request,
            supportFingerprint
        });
        var semanticEvent = SemanticCostChoiceV111.CreateEvent(
            boundaryFingerprint, request, cardInstanceId, cardModelId);
        var semanticOutcome = SemanticCostChoiceV111.Select(
            semanticEvent, semanticEvent.EventId,
            providerDecision.SelectedIndex);
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = CurrentSchemaId,
            semanticOutcome,
            providerDecision
        });
        var audit = new SemanticCardEnergyCostAuditV111(
            CurrentSchemaId, eventId, supportFingerprint, identities,
            semanticOutcome, CloneDecision(providerDecision));
        audit.Validate();
        return audit;
    }

    public void Validate()
    {
        var identities = _orderedEligibleCardInstanceIds;
        ArgumentNullException.ThrowIfNull(Outcome);
        ArgumentNullException.ThrowIfNull(ProviderDecision);
        Outcome.Validate();
        ProviderDecision.Validate();
        var semanticEvent = Outcome.Event;
        var cardInstanceId = semanticEvent.TargetCardInstanceId;
        var selectedCardInstanceId = cardInstanceId ?? 0;
        var expectedBoundaryFingerprint = CanonicalJson.Sha256(new
        {
            schemaId = CurrentSchemaId,
            request = semanticEvent.Request,
            supportFingerprint = EligibleCardSupportFingerprint
        });
        bool handCost = semanticEvent.Request.SiteId ==
            StochasticSiteRegistryV111.CombatRandomHandCardEnergyCostSiteId;
        bool drawHookCost = semanticEvent.Request.SiteId is
            StochasticSiteRegistryV111.CombatDrawRelicEnergyCostSiteId or
            StochasticSiteRegistryV111.CombatDrawSlitherEnergyCostSiteId;
        var expectedDrawOwner = "card_instance:" + selectedCardInstanceId
            .ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ":model:" + semanticEvent.TargetCardModelId + ":stage:" +
            (semanticEvent.Request.SiteId ==
                StochasticSiteRegistryV111.CombatDrawRelicEnergyCostSiteId
                    ? "relic"
                    : "slither");
        if (SchemaId != CurrentSchemaId ||
            ProviderDecision.ProviderMode != "native-v111" ||
            CanonicalJson.Sha256(semanticEvent.Request) !=
                CanonicalJson.Sha256(ProviderDecision.Request) ||
            Outcome.SelectedIndex != ProviderDecision.SelectedIndex ||
            Outcome.SelectedCost != ProviderDecision.SelectedIndex ||
            (!handCost && !drawHookCost) ||
            semanticEvent.Request.OperationKind != "bounded_integer" ||
            semanticEvent.BoundaryFingerprint !=
                expectedBoundaryFingerprint ||
            identities is not { Length: > 0 } ||
            identities.Any(id => id <= 0) ||
            identities.Distinct().Count() != identities.Length ||
            selectedCardInstanceId <= 0 ||
            !identities.Contains(selectedCardInstanceId) ||
            EligibleCardSupportFingerprint != CanonicalJson.Sha256(identities) ||
            (handCost && !semanticEvent.Request.OwnerId.EndsWith(
                ":hand:" + EligibleCardSupportFingerprint +
                    ":card_instance:" + selectedCardInstanceId.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal)) ||
            (drawHookCost &&
                (identities.Length != 1 ||
                 identities[0] != selectedCardInstanceId ||
                 semanticEvent.Request.OwnerId != expectedDrawOwner)) ||
            EventId != CanonicalJson.Sha256(new
            {
                schemaId = SchemaId,
                semanticOutcome = Outcome,
                providerDecision = ProviderDecision
            }))
            throw new InvalidDataException(
                "Invalid Native card energy-cost audit outcome.");
    }
}

internal static class RandomBoundaryCursorSpanV111
{
    internal static void Validate(
        string streamId, string providerMode,
        NativeRngSnapshot? cursorBefore,
        NativeRngSnapshot? cursorAfter)
    {
        bool isNative = providerMode == "native-v111";
        if ((cursorBefore is null) != (cursorAfter is null) ||
            isNative != (cursorBefore is not null))
            throw new ArgumentException(
                "Native provider decisions require both cursor snapshots; other providers cannot claim Native cursors.");

        if (!isNative)
            return;

        ValidateSnapshot(streamId, cursorBefore!);
        ValidateSnapshot(streamId, cursorAfter!);
    }

    private static void ValidateSnapshot(
        string streamId, NativeRngSnapshot snapshot)
    {
        var restored = NativeRngV111.FromSnapshot(streamId, snapshot);
        if (NativeRngV111.ToSnapshot(restored) != snapshot)
            throw new ArgumentException(
                "Native cursor snapshot does not round-trip through its registered codec.");
    }
}

/// <summary>
/// Stores one chronological mixed transcript. The typed entry collections
/// are projections over that transcript, with independent per-operation
/// sequence numbers retained in each payload.
/// </summary>
public sealed class RandomBoundaryJournalV111 : IDisposable
{
    private static readonly AsyncLocal<RandomBoundaryJournalV111?> Active = new();
    private readonly RandomBoundaryJournalV111? _previous;
    private readonly List<RandomBoundaryTranscriptEntryV111> _orderedTranscript = [];
    private readonly List<SemanticIntentAuditEntryV111> _weightedIntentOutcomes = [];
    private readonly List<SemanticCardInstanceIndexAuditV111>
        _cardInstanceIndexOutcomes = [];
    private readonly List<SemanticCardEnergyCostAuditV111>
        _cardEnergyCostOutcomes = [];
    private readonly List<SemanticDrawInsertionNativeAuditV111>
        _drawInsertionOutcomes = [];
    private readonly List<SemanticEnemyPileInsertionNativeAuditV111>
        _enemyPileInsertionOutcomes = [];
    private readonly List<SemanticPoolShuffleDecisionV111>
        _poolShuffleOutcomes = [];
    private readonly List<SemanticGeneratedCardNativeAuditV111>
        _generatedCardModelOutcomes = [];
    private readonly List<SemanticCardTransformNativeAuditV111>
        _cardTransformOutcomes = [];
    private readonly List<SemanticTargetNativeAuditV111>
        _randomTargetOutcomes = [];
    private readonly List<SemanticPotionGenerationNativeAuditV111>
        _potionGenerationOutcomes = [];
    private readonly Dictionary<string, int> _operationSequences =
        new(StringComparer.Ordinal);
    private bool _disposed;

    private RandomBoundaryJournalV111()
    {
        _previous = Active.Value;
        Active.Value = this;
    }

    public static RandomBoundaryJournalV111 Begin() => new();

    internal static bool HasActiveJournal => Active.Value is not null;

    public IReadOnlyList<RandomBoundaryEventV111> Entries =>
        Project<RandomBoundaryEventV111>("index");
    public IReadOnlyList<RandomUnitFloatBoundaryEventV111> UnitFloatEntries =>
        Project<RandomUnitFloatBoundaryEventV111>("unit_float");
    public IReadOnlyList<RandomRangedFloatBoundaryEventV111> RangedFloatEntries =>
        Project<RandomRangedFloatBoundaryEventV111>("ranged_float");
    public IReadOnlyList<RandomRangedDoubleBoundaryEventV111> RangedDoubleEntries =>
        Project<RandomRangedDoubleBoundaryEventV111>("ranged_double");
    public IReadOnlyList<RandomGaussianIntBoundaryEventV111> GaussianIntEntries =>
        Project<RandomGaussianIntBoundaryEventV111>("gaussian_int");
    public IReadOnlyList<RandomGaussianDoubleBoundaryEventV111> GaussianDoubleEntries =>
        Project<RandomGaussianDoubleBoundaryEventV111>("gaussian_double");
    public IReadOnlyList<RandomBoundaryTranscriptEntryV111> OrderedTranscript =>
        _orderedTranscript.ToArray();

    /// <summary>
    /// Semantic annotations for sampled weighted intent tickets in this
    /// transition. Each annotation references its primitive request and does
    /// not add an RNG operation to the mixed provider chronology.
    /// </summary>
    public IReadOnlyList<SemanticIntentAuditEntryV111> WeightedIntentOutcomes =>
        _weightedIntentOutcomes.Select(outcome => outcome.Fork()).ToArray();

    /// <summary>Semantic physical-card identities selected at registered
    /// Native combat index sites. These annotations remain outside primitive
    /// chronology and are paired with their exact index records.</summary>
    public IReadOnlyList<SemanticCardInstanceIndexAuditV111>
        CardInstanceIndexOutcomes => _cardInstanceIndexOutcomes
            .Select(outcome => outcome.Fork()).ToArray();

    /// <summary>Typed Native outcomes for registered hand and drawn-card
    /// energy-cost sites.</summary>
    public IReadOnlyList<SemanticCardEnergyCostAuditV111>
        CardEnergyCostOutcomes => _cardEnergyCostOutcomes
            .Select(outcome => outcome.Fork()).ToArray();

    /// <summary>Typed Native draw-pile insertion positions and supports.</summary>
    public IReadOnlyList<SemanticDrawInsertionNativeAuditV111>
        DrawInsertionOutcomes => _drawInsertionOutcomes
            .Select(outcome => outcome.Fork()).ToArray();

    /// <summary>Typed Native enemy-triggered player-pile insertion outcomes.</summary>
    public IReadOnlyList<SemanticEnemyPileInsertionNativeAuditV111>
        EnemyPileInsertionOutcomes => _enemyPileInsertionOutcomes
            .Select(outcome => outcome.Fork()).ToArray();

    /// <summary>Semantic ordered supports for Native combat pool swaps.</summary>
    public IReadOnlyList<SemanticPoolShuffleDecisionV111>
        PoolShuffleOutcomes => _poolShuffleOutcomes
            .Select(outcome => outcome.Fork()).ToArray();

    /// <summary>Typed Native generated-card model results.</summary>
    public IReadOnlyList<SemanticGeneratedCardNativeAuditV111>
        GeneratedCardModelOutcomes => _generatedCardModelOutcomes
            .Select(outcome => outcome.Fork()).ToArray();

    /// <summary>Typed Native card-transform replacement results.</summary>
    public IReadOnlyList<SemanticCardTransformNativeAuditV111>
        CardTransformOutcomes => _cardTransformOutcomes
            .Select(outcome => outcome.Fork()).ToArray();

    /// <summary>Typed Native selected enemy identities at random-target sites.</summary>
    public IReadOnlyList<SemanticTargetNativeAuditV111>
        RandomTargetOutcomes => _randomTargetOutcomes
            .Select(outcome => outcome.Fork()).ToArray();

    /// <summary>Typed Native combat potion model results.</summary>
    public IReadOnlyList<SemanticPotionGenerationNativeAuditV111>
        PotionGenerationOutcomes => _potionGenerationOutcomes
            .Select(outcome => outcome.Fork()).ToArray();

    internal static void RecordWeightedIntentOutcome(
        SemanticIntentAuditEntryV111 auditEntry)
    {
        ArgumentNullException.ThrowIfNull(auditEntry);
        var current = Active.Value;
        if (current is null)
            return;
        auditEntry.Validate();
        var lastEntry = current._orderedTranscript.LastOrDefault();
        if (lastEntry is null || lastEntry.OperationKind != "index")
            throw new InvalidDataException(
                "Weighted intent annotation has no preceding primitive index entry.");
        var boundary = lastEntry.DeserializeEvent<RandomBoundaryEventV111>();
        var journalDecision = new RandomIndexDecisionV111(
            boundary.Request, boundary.SelectedIndex, boundary.ProviderMode,
            boundary.NativeCursorBefore, boundary.NativeCursorAfter);
        if (CanonicalJson.Sha256(journalDecision) !=
            CanonicalJson.Sha256(auditEntry.ProviderDecision))
            throw new InvalidDataException(
                "Weighted intent annotation differs from the latest primitive index entry.");
        current._weightedIntentOutcomes.Add(auditEntry.Fork());
    }

    internal static void RecordCardInstanceIndexOutcome(
        SemanticCardInstanceIndexAuditV111 auditEntry)
    {
        ArgumentNullException.ThrowIfNull(auditEntry);
        var current = Active.Value;
        if (current is null)
            return;
        auditEntry.Validate();
        var lastEntry = current._orderedTranscript.LastOrDefault();
        if (lastEntry is null || lastEntry.OperationKind != "index")
            throw new InvalidDataException(
                "Card-instance index annotation has no preceding primitive index entry.");
        var boundary = lastEntry.DeserializeEvent<RandomBoundaryEventV111>();
        var journalDecision = new RandomIndexDecisionV111(
            boundary.Request, boundary.SelectedIndex, boundary.ProviderMode,
            boundary.NativeCursorBefore, boundary.NativeCursorAfter);
        if (CanonicalJson.Sha256(journalDecision) !=
            CanonicalJson.Sha256(auditEntry.ProviderDecision))
            throw new InvalidDataException(
                "Card-instance index annotation differs from the latest primitive index entry.");
        current._cardInstanceIndexOutcomes.Add(auditEntry.Fork());
    }

    internal static void RecordCardEnergyCostOutcome(
        SemanticCardEnergyCostAuditV111 auditEntry)
    {
        ArgumentNullException.ThrowIfNull(auditEntry);
        var current = Active.Value;
        if (current is null)
            return;
        auditEntry.Validate();
        var lastEntry = current._orderedTranscript.LastOrDefault();
        if (lastEntry is null || lastEntry.OperationKind != "index")
            throw new InvalidDataException(
                "Card energy-cost annotation has no preceding primitive index entry.");
        var boundary = lastEntry.DeserializeEvent<RandomBoundaryEventV111>();
        var journalDecision = new RandomIndexDecisionV111(
            boundary.Request, boundary.SelectedIndex, boundary.ProviderMode,
            boundary.NativeCursorBefore, boundary.NativeCursorAfter);
        if (CanonicalJson.Sha256(journalDecision) !=
            CanonicalJson.Sha256(auditEntry.ProviderDecision))
            throw new InvalidDataException(
                "Card energy-cost annotation differs from the latest primitive index entry.");
        current._cardEnergyCostOutcomes.Add(auditEntry.Fork());
    }

    internal static void RecordDrawInsertionOutcome(
        SemanticDrawInsertionNativeAuditV111 auditEntry)
    {
        ArgumentNullException.ThrowIfNull(auditEntry);
        var current = Active.Value;
        if (current is null)
            return;
        auditEntry.Validate();
        var lastEntry = current._orderedTranscript.LastOrDefault();
        if (lastEntry is null || lastEntry.OperationKind != "index")
            throw new InvalidDataException(
                "Draw-insertion annotation has no preceding primitive index entry.");
        var boundary = lastEntry.DeserializeEvent<RandomBoundaryEventV111>();
        var journalDecision = new RandomIndexDecisionV111(
            boundary.Request, boundary.SelectedIndex, boundary.ProviderMode,
            boundary.NativeCursorBefore, boundary.NativeCursorAfter);
        if (CanonicalJson.Sha256(journalDecision) !=
            CanonicalJson.Sha256(auditEntry.ProviderDecision))
            throw new InvalidDataException(
                "Draw-insertion annotation differs from the latest primitive index entry.");
        current._drawInsertionOutcomes.Add(auditEntry.Fork());
    }

    internal static void RecordEnemyPileInsertionOutcome(
        SemanticEnemyPileInsertionNativeAuditV111 auditEntry)
    {
        ArgumentNullException.ThrowIfNull(auditEntry);
        var current = Active.Value;
        if (current is null)
            return;
        auditEntry.Validate();
        var lastEntry = current._orderedTranscript.LastOrDefault();
        if (lastEntry is null || lastEntry.OperationKind != "index")
            throw new InvalidDataException(
                "Enemy pile-insertion annotation has no preceding primitive index entry.");
        var boundary = lastEntry.DeserializeEvent<RandomBoundaryEventV111>();
        var journalDecision = new RandomIndexDecisionV111(
            boundary.Request, boundary.SelectedIndex, boundary.ProviderMode,
            boundary.NativeCursorBefore, boundary.NativeCursorAfter);
        if (CanonicalJson.Sha256(journalDecision) !=
            CanonicalJson.Sha256(auditEntry.ProviderDecision))
            throw new InvalidDataException(
                "Enemy pile-insertion annotation differs from the latest primitive index entry.");
        current._enemyPileInsertionOutcomes.Add(auditEntry.Fork());
    }

    internal static void RecordPoolShuffleOutcomes(
        IReadOnlyList<SemanticPoolShuffleDecisionV111> outcomes,
        IReadOnlyList<RandomIndexDecisionV111> providerDecisions)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        ArgumentNullException.ThrowIfNull(providerDecisions);
        var current = Active.Value;
        if (current is null)
            return;
        if (outcomes.Count == 0 ||
            outcomes.Count != providerDecisions.Count ||
            outcomes.Count > current._orderedTranscript.Count)
            throw new InvalidDataException(
                "Native pool-shuffle annotations do not match their provider decisions.");

        var firstTranscriptIndex =
            current._orderedTranscript.Count - outcomes.Count;
        for (var index = 0; index < outcomes.Count; index++)
        {
            var outcome = outcomes[index];
            var providerDecision = providerDecisions[index];
            providerDecision.Validate();
            SemanticPoolShuffleChoiceV111.ValidateNativeAudit(
                outcome, providerDecision);
            var entry = current._orderedTranscript[
                firstTranscriptIndex + index];
            if (entry.OperationKind != "index")
                throw new InvalidDataException(
                    "Native pool-shuffle annotation has no matching index transcript entry.");
            var boundary = entry.DeserializeEvent<RandomBoundaryEventV111>();
            var journalDecision = new RandomIndexDecisionV111(
                boundary.Request, boundary.SelectedIndex,
                boundary.ProviderMode, boundary.NativeCursorBefore,
                boundary.NativeCursorAfter);
            if (providerDecision.ProviderMode != "native-v111" ||
                CanonicalJson.Sha256(providerDecision) !=
                    CanonicalJson.Sha256(journalDecision) ||
                CanonicalJson.Sha256(outcome.Event.Request) !=
                    CanonicalJson.Sha256(providerDecision.Request) ||
                outcome.SelectedSwapIndex != providerDecision.SelectedIndex)
                throw new InvalidDataException(
                    "Native pool-shuffle annotation differs from its ordered primitive transcript.");
            current._poolShuffleOutcomes.Add(outcome.Fork());
        }
    }

    internal static void RecordGeneratedCardModelOutcome(
        SemanticGeneratedCardNativeAuditV111 auditEntry)
    {
        ArgumentNullException.ThrowIfNull(auditEntry);
        var current = Active.Value;
        if (current is null)
            return;
        auditEntry.Validate();
        var lastEntry = current._orderedTranscript.LastOrDefault();
        if (lastEntry is null || lastEntry.OperationKind != "index")
            throw new InvalidDataException(
                "Generated-card model annotation has no preceding primitive index entry.");
        var boundary = lastEntry.DeserializeEvent<RandomBoundaryEventV111>();
        var journalDecision = new RandomIndexDecisionV111(
            boundary.Request, boundary.SelectedIndex, boundary.ProviderMode,
            boundary.NativeCursorBefore, boundary.NativeCursorAfter);
        if (CanonicalJson.Sha256(journalDecision) !=
            CanonicalJson.Sha256(auditEntry.ProviderDecision))
            throw new InvalidDataException(
                "Generated-card model annotation differs from the latest primitive index entry.");
        current._generatedCardModelOutcomes.Add(auditEntry.Fork());
    }

    internal static void RecordCardTransformOutcome(
        SemanticCardTransformNativeAuditV111 auditEntry)
    {
        ArgumentNullException.ThrowIfNull(auditEntry);
        var current = Active.Value;
        if (current is null)
            return;
        auditEntry.Validate();
        var lastEntry = current._orderedTranscript.LastOrDefault();
        if (lastEntry is null || lastEntry.OperationKind != "index")
            throw new InvalidDataException(
                "Card-transform annotation has no preceding primitive index entry.");
        var boundary = lastEntry.DeserializeEvent<RandomBoundaryEventV111>();
        var journalDecision = new RandomIndexDecisionV111(
            boundary.Request, boundary.SelectedIndex, boundary.ProviderMode,
            boundary.NativeCursorBefore, boundary.NativeCursorAfter);
        if (CanonicalJson.Sha256(journalDecision) !=
            CanonicalJson.Sha256(auditEntry.ProviderDecision))
            throw new InvalidDataException(
                "Card-transform annotation differs from the latest primitive index entry.");
        current._cardTransformOutcomes.Add(auditEntry.Fork());
    }

    internal static void RecordRandomTargetOutcome(
        SemanticTargetNativeAuditV111 auditEntry)
    {
        ArgumentNullException.ThrowIfNull(auditEntry);
        var current = Active.Value;
        if (current is null)
            return;
        auditEntry.Validate();
        var lastEntry = current._orderedTranscript.LastOrDefault();
        if (lastEntry is null || lastEntry.OperationKind != "index")
            throw new InvalidDataException(
                "Random-target annotation has no preceding primitive index entry.");
        var boundary = lastEntry.DeserializeEvent<RandomBoundaryEventV111>();
        var journalDecision = new RandomIndexDecisionV111(
            boundary.Request, boundary.SelectedIndex, boundary.ProviderMode,
            boundary.NativeCursorBefore, boundary.NativeCursorAfter);
        if (CanonicalJson.Sha256(journalDecision) !=
            CanonicalJson.Sha256(auditEntry.ProviderDecision))
            throw new InvalidDataException(
                "Random-target annotation differs from the latest primitive index entry.");
        current._randomTargetOutcomes.Add(auditEntry.Fork());
    }

    internal static void RecordPotionGenerationOutcome(
        SemanticPotionGenerationNativeAuditV111 auditEntry)
    {
        ArgumentNullException.ThrowIfNull(auditEntry);
        var current = Active.Value;
        if (current is null)
            return;
        auditEntry.Validate();
        var lastEntry = current._orderedTranscript.LastOrDefault();
        if (lastEntry is null || lastEntry.OperationKind != "index")
            throw new InvalidDataException(
                "Potion-generation annotation has no preceding primitive index entry.");
        var boundary = lastEntry.DeserializeEvent<RandomBoundaryEventV111>();
        var journalDecision = new RandomIndexDecisionV111(
            boundary.Request, boundary.SelectedIndex, boundary.ProviderMode,
            boundary.NativeCursorBefore, boundary.NativeCursorAfter);
        if (CanonicalJson.Sha256(journalDecision) !=
            CanonicalJson.Sha256(auditEntry.ProviderDecision))
            throw new InvalidDataException(
                "Potion-generation annotation differs from the latest primitive index entry.");
        current._potionGenerationOutcomes.Add(auditEntry.Fork());
    }

    /// <summary>Returns a provider-specific mixed transcript in the order
    /// recorded by the active journal. Its sequence numbers are local to the
    /// returned trace; each payload retains its original typed event shape.</summary>
    public RandomBoundaryTranscriptEntryV111[] EntriesForProvider(
        string providerMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerMode);
        return _orderedTranscript
            .Where(entry => entry.Event.TryGetProperty(
                    "provider_mode", out var mode) &&
                mode.ValueKind == System.Text.Json.JsonValueKind.String &&
                mode.GetString() == providerMode)
            .Select((entry, index) => entry with
            {
                Sequence = index + 1,
                Event = entry.Event.Clone()
            })
            .ToArray();
    }

    public static RandomBoundaryTranscriptEntryV111[] ConcatOrdered(
        IEnumerable<RandomBoundaryTranscriptEntryV111> first,
        IEnumerable<RandomBoundaryTranscriptEntryV111> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return first.Concat(second).Select((entry, index) => entry with
        {
            Sequence = index + 1,
            Event = entry.Event.Clone()
        }).ToArray();
    }

    public static void Record(RandomIndexDecisionV111 decision)
    {
        var scope = Active.Value;
        if (scope is null) return;
        if (scope._disposed)
            throw new InvalidOperationException("Random boundary journal is closed.");
        // All request/cursor data are copied after provider validation.
        // Neither game RNG nor mechanical continuation is touched.
        var entry = new RandomBoundaryEventV111(
            scope.NextOperationSequence("index"),
            decision.Request with
            {
                OrderedCandidates = (int[])decision.Request.OrderedCandidates.Clone()
            },
            decision.SelectedIndex,
            decision.SelectedValue,
            decision.ProviderMode,
            decision.NativeCursorBefore,
            decision.NativeCursorAfter);
        scope.RecordOrdered("index", entry);
    }

    public static void Record(RandomUnitFloatDecisionV111 decision)
    {
        var scope = Active.Value;
        if (scope is null) return;
        if (scope._disposed)
            throw new InvalidOperationException("Random boundary journal is closed.");
        var entry = new RandomUnitFloatBoundaryEventV111(
            scope.NextOperationSequence("unit_float"),
            decision.Request with { },
            decision.SelectedValue,
            decision.ProviderMode,
            decision.NativeCursorBefore,
            decision.NativeCursorAfter);
        scope.RecordOrdered("unit_float", entry);
    }

    public static void Record(RandomRangedFloatDecisionV111 decision)
    {
        var scope = Active.Value;
        if (scope is null) return;
        if (scope._disposed)
            throw new InvalidOperationException("Random boundary journal is closed.");
        var entry = new RandomRangedFloatBoundaryEventV111(
            scope.NextOperationSequence("ranged_float"),
            decision.Request with { },
            decision.SelectedValue,
            decision.ProviderMode,
            decision.NativeCursorBefore,
            decision.NativeCursorAfter);
        scope.RecordOrdered("ranged_float", entry);
    }

    public static void Record(RandomRangedDoubleDecisionV111 decision)
    {
        var scope = Active.Value;
        if (scope is null) return;
        if (scope._disposed)
            throw new InvalidOperationException("Random boundary journal is closed.");
        var entry = new RandomRangedDoubleBoundaryEventV111(
            scope.NextOperationSequence("ranged_double"),
            decision.Request with { },
            decision.SelectedValue,
            decision.ProviderMode,
            decision.NativeCursorBefore,
            decision.NativeCursorAfter);
        scope.RecordOrdered("ranged_double", entry);
    }

    public static void Record(RandomGaussianIntDecisionV111 decision)
    {
        var scope = Active.Value;
        if (scope is null) return;
        if (scope._disposed)
            throw new InvalidOperationException("Random boundary journal is closed.");
        var entry = new RandomGaussianIntBoundaryEventV111(
            scope.NextOperationSequence("gaussian_int"),
            decision.Request with { },
            decision.SelectedValue,
            decision.ProviderMode,
            decision.NativeCursorBefore,
            decision.NativeCursorAfter);
        scope.RecordOrdered("gaussian_int", entry);
    }

    public static void Record(RandomGaussianDoubleDecisionV111 decision)
    {
        var scope = Active.Value;
        if (scope is null) return;
        if (scope._disposed)
            throw new InvalidOperationException("Random boundary journal is closed.");
        var entry = new RandomGaussianDoubleBoundaryEventV111(
            scope.NextOperationSequence("gaussian_double"),
            decision.Request with { },
            decision.SelectedValue,
            decision.ProviderMode,
            decision.NativeCursorBefore,
            decision.NativeCursorAfter);
        scope.RecordOrdered("gaussian_double", entry);
    }

    private TEvent[] Project<TEvent>(string operationKind)
        where TEvent : class => _orderedTranscript
        .Where(entry => entry.OperationKind == operationKind)
        .Select(entry => entry.DeserializeEvent<TEvent>())
        .ToArray();

    private int NextOperationSequence(string operationKind) =>
        _operationSequences.TryGetValue(operationKind, out var current)
            ? checked(current + 1) : 1;

    private void RecordOrdered(string operationKind, object boundaryEvent)
    {
        int operationSequence = boundaryEvent switch
        {
            RandomBoundaryEventV111 value => value.Sequence,
            RandomUnitFloatBoundaryEventV111 value => value.Sequence,
            RandomRangedFloatBoundaryEventV111 value => value.Sequence,
            RandomRangedDoubleBoundaryEventV111 value => value.Sequence,
            RandomGaussianIntBoundaryEventV111 value => value.Sequence,
            RandomGaussianDoubleBoundaryEventV111 value => value.Sequence,
            _ => throw new ArgumentException(
                "Unsupported random-boundary event type.",
                nameof(boundaryEvent))
        };
        if (operationSequence != NextOperationSequence(operationKind))
            throw new InvalidDataException(
                "Random-boundary operation sequence is not contiguous.");
        var transcript = new RandomBoundaryTranscriptEntryV111(
            _orderedTranscript.Count + 1, operationKind,
            RandomBoundaryTranscriptEntryV111.SerializeEvent(boundaryEvent));
        transcript.Validate();
        _orderedTranscript.Add(transcript);
        _operationSequences[operationKind] = operationSequence;
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (!ReferenceEquals(Active.Value, this))
            throw new InvalidOperationException("Random boundary scopes must close in LIFO order.");
        Active.Value = _previous;
        _disposed = true;
    }
}

/// <summary>
/// Compare already resolved index, float/double, Gaussian-integer or
/// Gaussian-double provider traces on source site, stream, owner, request
/// parameters and outcome.
/// Provider mode and cursor may differ between native and counterfactual
/// scripted replay. This is only a consistency test of provider calls; it
/// cannot establish native game call-site completeness or hidden hook-order
/// parity.
/// </summary>
public sealed record RandomBoundaryDiffV111(
    string Status, int Compared, int? FirstMismatchIndex,
    string? MismatchField, string SourceHookParity = "NOT_TESTED");

public static class RandomBoundaryComparisonV111
{
    /// <summary>Compare mixed primitive operations in their actual global
    /// provider-call order while retaining each typed request comparison.</summary>
    public static RandomBoundaryDiffV111 CompareOrdered(
        IEnumerable<RandomBoundaryTranscriptEntryV111> expected,
        IEnumerable<RandomBoundaryTranscriptEntryV111> actual,
        bool requireNativeCursorEquality = false)
    {
        var left = expected.ToArray();
        var right = actual.ToArray();
        var shared = Math.Min(left.Length, right.Length);
        for (int index = 0; index < shared; index++)
        {
            var a = left[index];
            var b = right[index];
            if (a is null || b is null)
                return new("RANDOM_BOUNDARY_MISMATCH", index, index,
                    "event_payload");
            string? mismatch =
                a.Sequence != index + 1 || b.Sequence != index + 1
                    ? "sequence"
                    : a.OperationKind != b.OperationKind
                        ? "operation_kind" : null;
            if (mismatch is null)
                mismatch = CompareOrderedPayload(
                    a, b, requireNativeCursorEquality);
            if (mismatch is not null)
                return new("RANDOM_BOUNDARY_MISMATCH", index, index, mismatch);
        }
        if (left.Length != right.Length)
            return new("RANDOM_BOUNDARY_LENGTH_MISMATCH", shared,
                shared, "request_count");
        return new("RANDOM_BOUNDARIES_CONSISTENT", shared, null, null);
    }

    private static string? CompareOrderedPayload(
        RandomBoundaryTranscriptEntryV111 expected,
        RandomBoundaryTranscriptEntryV111 actual,
        bool requireNativeCursorEquality)
    {
        expected.Validate();
        actual.Validate();
        try
        {
            return expected.OperationKind switch
            {
                "index" => CompareOne(
                    expected.DeserializeEvent<RandomBoundaryEventV111>(),
                    actual.DeserializeEvent<RandomBoundaryEventV111>(),
                    requireNativeCursorEquality),
                "unit_float" => CompareOne(
                    expected.DeserializeEvent<RandomUnitFloatBoundaryEventV111>(),
                    actual.DeserializeEvent<RandomUnitFloatBoundaryEventV111>(),
                    requireNativeCursorEquality),
                "ranged_float" => CompareOne(
                    expected.DeserializeEvent<RandomRangedFloatBoundaryEventV111>(),
                    actual.DeserializeEvent<RandomRangedFloatBoundaryEventV111>(),
                    requireNativeCursorEquality),
                "ranged_double" => CompareOne(
                    expected.DeserializeEvent<RandomRangedDoubleBoundaryEventV111>(),
                    actual.DeserializeEvent<RandomRangedDoubleBoundaryEventV111>(),
                    requireNativeCursorEquality),
                "gaussian_int" => CompareOne(
                    expected.DeserializeEvent<RandomGaussianIntBoundaryEventV111>(),
                    actual.DeserializeEvent<RandomGaussianIntBoundaryEventV111>(),
                    requireNativeCursorEquality),
                "gaussian_double" => CompareOne(
                    expected.DeserializeEvent<RandomGaussianDoubleBoundaryEventV111>(),
                    actual.DeserializeEvent<RandomGaussianDoubleBoundaryEventV111>(),
                    requireNativeCursorEquality),
                _ => "operation_kind"
            };
        }
        catch (JsonException)
        {
            return "event_payload";
        }
        catch (InvalidDataException)
        {
            return "event_payload";
        }
        catch (Exception exception) when (exception is ArgumentException or
            InvalidOperationException or NullReferenceException)
        {
            return "event_payload";
        }
    }

    private static string? CompareOne<T>(
        T? expected, T? actual, bool requireNativeCursorEquality)
        where T : class
    {
        if (expected is null || actual is null)
            return "event_payload";
        RandomBoundaryDiffV111 result = (expected, actual) switch
        {
            (RandomBoundaryEventV111 a, RandomBoundaryEventV111 b) =>
                Compare([a with { Sequence = 1 }],
                    [b with { Sequence = 1 }], requireNativeCursorEquality),
            (RandomUnitFloatBoundaryEventV111 a,
                RandomUnitFloatBoundaryEventV111 b) =>
                Compare([a with { Sequence = 1 }],
                    [b with { Sequence = 1 }], requireNativeCursorEquality),
            (RandomRangedFloatBoundaryEventV111 a,
                RandomRangedFloatBoundaryEventV111 b) =>
                Compare([a with { Sequence = 1 }],
                    [b with { Sequence = 1 }], requireNativeCursorEquality),
            (RandomRangedDoubleBoundaryEventV111 a,
                RandomRangedDoubleBoundaryEventV111 b) =>
                Compare([a with { Sequence = 1 }],
                    [b with { Sequence = 1 }], requireNativeCursorEquality),
            (RandomGaussianIntBoundaryEventV111 a,
                RandomGaussianIntBoundaryEventV111 b) =>
                Compare([a with { Sequence = 1 }],
                    [b with { Sequence = 1 }], requireNativeCursorEquality),
            (RandomGaussianDoubleBoundaryEventV111 a,
                RandomGaussianDoubleBoundaryEventV111 b) =>
                Compare([a with { Sequence = 1 }],
                    [b with { Sequence = 1 }], requireNativeCursorEquality),
            _ => new RandomBoundaryDiffV111(
                "RANDOM_BOUNDARY_MISMATCH", 0, 0, "event_payload")
        };
        return result.Status == "RANDOM_BOUNDARIES_CONSISTENT"
            ? null : result.MismatchField ?? "event_payload";
    }

    public static RandomBoundaryDiffV111 Compare(
        IEnumerable<RandomBoundaryEventV111> expected,
        IEnumerable<RandomBoundaryEventV111> actual,
        bool requireNativeCursorEquality = false)
    {
        var left = expected.ToArray();
        var right = actual.ToArray();
        int shared = Math.Min(left.Length, right.Length);
        for (int i = 0; i < shared; i++)
        {
            var a = left[i]; var b = right[i];
            string? mismatch =
                a.Sequence != i + 1 || b.Sequence != i + 1 ? "sequence" :
                a.Request.SiteId != b.Request.SiteId ? "site_id" :
                a.Request.StreamId != b.Request.StreamId ? "stream_id" :
                a.Request.OwnerId != b.Request.OwnerId ? "owner_id" :
                a.Request.Ordinal != b.Request.Ordinal ? "ordinal" :
                a.Request.SourceVersion != b.Request.SourceVersion ? "source_version" :
                a.Request.OperationKind != b.Request.OperationKind ? "operation_kind" :
                a.Request.BoundedCount != b.Request.BoundedCount ? "bounded_count" :
                !a.Request.OrderedCandidates.SequenceEqual(b.Request.OrderedCandidates)
                    ? "ordered_candidates" :
                a.SelectedIndex != b.SelectedIndex ? "selected_index" :
                a.SelectedValue != b.SelectedValue ? "selected_value" :
                requireNativeCursorEquality && (
                    a.NativeCursorBefore is null || b.NativeCursorBefore is null ||
                    a.NativeCursorAfter is null || b.NativeCursorAfter is null ||
                    a.NativeCursorBefore != b.NativeCursorBefore ||
                    a.NativeCursorAfter != b.NativeCursorAfter) ? "native_cursors" :
                null;
            if (mismatch is not null)
                return new("RANDOM_BOUNDARY_MISMATCH", i, i, mismatch);
        }
        if (left.Length != right.Length)
            return new("RANDOM_BOUNDARY_LENGTH_MISMATCH", shared,
                shared, "request_count");
        return new("RANDOM_BOUNDARIES_CONSISTENT", shared, null, null);
    }

    public static RandomBoundaryDiffV111 Compare(
        IEnumerable<RandomUnitFloatBoundaryEventV111> expected,
        IEnumerable<RandomUnitFloatBoundaryEventV111> actual,
        bool requireNativeCursorEquality = false)
    {
        var left = expected.ToArray();
        var right = actual.ToArray();
        int shared = Math.Min(left.Length, right.Length);
        for (int i = 0; i < shared; i++)
        {
            var a = left[i]; var b = right[i];
            string? mismatch =
                a.Sequence != i + 1 || b.Sequence != i + 1 ? "sequence" :
                a.Request.SiteId != b.Request.SiteId ? "site_id" :
                a.Request.StreamId != b.Request.StreamId ? "stream_id" :
                a.Request.OwnerId != b.Request.OwnerId ? "owner_id" :
                a.Request.Ordinal != b.Request.Ordinal ? "ordinal" :
                a.Request.SourceVersion != b.Request.SourceVersion ? "source_version" :
                a.SelectedValue != b.SelectedValue ? "selected_value" :
                requireNativeCursorEquality && (
                    a.NativeCursorBefore is null || b.NativeCursorBefore is null ||
                    a.NativeCursorAfter is null || b.NativeCursorAfter is null ||
                    a.NativeCursorBefore != b.NativeCursorBefore ||
                    a.NativeCursorAfter != b.NativeCursorAfter) ? "native_cursors" :
                null;
            if (mismatch is not null)
                return new("RANDOM_BOUNDARY_MISMATCH", i, i, mismatch);
        }
        if (left.Length != right.Length)
            return new("RANDOM_BOUNDARY_LENGTH_MISMATCH", shared,
                shared, "request_count");
        return new("RANDOM_BOUNDARIES_CONSISTENT", shared, null, null);
    }

    public static RandomBoundaryDiffV111 Compare(
        IEnumerable<RandomGaussianIntBoundaryEventV111> expected,
        IEnumerable<RandomGaussianIntBoundaryEventV111> actual,
        bool requireNativeCursorEquality = false)
    {
        var left = expected.ToArray();
        var right = actual.ToArray();
        int shared = Math.Min(left.Length, right.Length);
        for (int i = 0; i < shared; i++)
        {
            var a = left[i]; var b = right[i];
            string? mismatch =
                a.Sequence != i + 1 || b.Sequence != i + 1 ? "sequence" :
                a.Request.SiteId != b.Request.SiteId ? "site_id" :
                a.Request.StreamId != b.Request.StreamId ? "stream_id" :
                a.Request.OwnerId != b.Request.OwnerId ? "owner_id" :
                a.Request.Ordinal != b.Request.Ordinal ? "ordinal" :
                a.Request.Mean != b.Request.Mean ? "mean" :
                a.Request.StandardDeviation != b.Request.StandardDeviation ? "standard_deviation" :
                a.Request.MinInclusive != b.Request.MinInclusive ? "min_inclusive" :
                a.Request.MaxInclusive != b.Request.MaxInclusive ? "max_inclusive" :
                a.Request.SourceVersion != b.Request.SourceVersion ? "source_version" :
                a.SelectedValue != b.SelectedValue ? "selected_value" :
                requireNativeCursorEquality && (
                    a.NativeCursorBefore is null || b.NativeCursorBefore is null ||
                    a.NativeCursorAfter is null || b.NativeCursorAfter is null ||
                    a.NativeCursorBefore != b.NativeCursorBefore ||
                    a.NativeCursorAfter != b.NativeCursorAfter) ? "native_cursors" :
                null;
            if (mismatch is not null)
                return new("RANDOM_BOUNDARY_MISMATCH", i, i, mismatch);
        }
        if (left.Length != right.Length)
            return new("RANDOM_BOUNDARY_LENGTH_MISMATCH", shared,
                shared, "request_count");
        return new("RANDOM_BOUNDARIES_CONSISTENT", shared, null, null);
    }

    public static RandomBoundaryDiffV111 Compare(
        IEnumerable<RandomRangedFloatBoundaryEventV111> expected,
        IEnumerable<RandomRangedFloatBoundaryEventV111> actual,
        bool requireNativeCursorEquality = false)
    {
        var left = expected.ToArray();
        var right = actual.ToArray();
        int shared = Math.Min(left.Length, right.Length);
        for (int i = 0; i < shared; i++)
        {
            var a = left[i]; var b = right[i];
            string? mismatch =
                a.Sequence != i + 1 || b.Sequence != i + 1 ? "sequence" :
                a.Request.SiteId != b.Request.SiteId ? "site_id" :
                a.Request.StreamId != b.Request.StreamId ? "stream_id" :
                a.Request.OwnerId != b.Request.OwnerId ? "owner_id" :
                a.Request.Ordinal != b.Request.Ordinal ? "ordinal" :
                a.Request.MinInclusive != b.Request.MinInclusive ? "min_inclusive" :
                a.Request.MaxInclusive != b.Request.MaxInclusive ? "max_inclusive" :
                a.Request.SourceVersion != b.Request.SourceVersion ? "source_version" :
                a.SelectedValue != b.SelectedValue ? "selected_value" :
                requireNativeCursorEquality && (
                    a.NativeCursorBefore is null || b.NativeCursorBefore is null ||
                    a.NativeCursorAfter is null || b.NativeCursorAfter is null ||
                    a.NativeCursorBefore != b.NativeCursorBefore ||
                    a.NativeCursorAfter != b.NativeCursorAfter) ? "native_cursors" :
                null;
            if (mismatch is not null)
                return new("RANDOM_BOUNDARY_MISMATCH", i, i, mismatch);
        }
        if (left.Length != right.Length)
            return new("RANDOM_BOUNDARY_LENGTH_MISMATCH", shared,
                shared, "request_count");
        return new("RANDOM_BOUNDARIES_CONSISTENT", shared, null, null);
    }

    public static RandomBoundaryDiffV111 Compare(
        IEnumerable<RandomRangedDoubleBoundaryEventV111> expected,
        IEnumerable<RandomRangedDoubleBoundaryEventV111> actual,
        bool requireNativeCursorEquality = false)
    {
        var left = expected.ToArray();
        var right = actual.ToArray();
        int shared = Math.Min(left.Length, right.Length);
        for (int i = 0; i < shared; i++)
        {
            var a = left[i]; var b = right[i];
            string? mismatch =
                a.Sequence != i + 1 || b.Sequence != i + 1 ? "sequence" :
                a.Request.SiteId != b.Request.SiteId ? "site_id" :
                a.Request.StreamId != b.Request.StreamId ? "stream_id" :
                a.Request.OwnerId != b.Request.OwnerId ? "owner_id" :
                a.Request.Ordinal != b.Request.Ordinal ? "ordinal" :
                a.Request.MinInclusive != b.Request.MinInclusive ? "min_inclusive" :
                a.Request.MaxInclusive != b.Request.MaxInclusive ? "max_inclusive" :
                a.Request.SourceVersion != b.Request.SourceVersion ? "source_version" :
                a.SelectedValue != b.SelectedValue ? "selected_value" :
                requireNativeCursorEquality && (
                    a.NativeCursorBefore is null || b.NativeCursorBefore is null ||
                    a.NativeCursorAfter is null || b.NativeCursorAfter is null ||
                    a.NativeCursorBefore != b.NativeCursorBefore ||
                    a.NativeCursorAfter != b.NativeCursorAfter) ? "native_cursors" :
                null;
            if (mismatch is not null)
                return new("RANDOM_BOUNDARY_MISMATCH", i, i, mismatch);
        }
        if (left.Length != right.Length)
            return new("RANDOM_BOUNDARY_LENGTH_MISMATCH", shared,
                shared, "request_count");
        return new("RANDOM_BOUNDARIES_CONSISTENT", shared, null, null);
    }

    public static RandomBoundaryDiffV111 Compare(
        IEnumerable<RandomGaussianDoubleBoundaryEventV111> expected,
        IEnumerable<RandomGaussianDoubleBoundaryEventV111> actual,
        bool requireNativeCursorEquality = false)
    {
        var left = expected.ToArray();
        var right = actual.ToArray();
        int shared = Math.Min(left.Length, right.Length);
        for (int i = 0; i < shared; i++)
        {
            var a = left[i]; var b = right[i];
            string? mismatch =
                a.Sequence != i + 1 || b.Sequence != i + 1 ? "sequence" :
                a.Request.SiteId != b.Request.SiteId ? "site_id" :
                a.Request.StreamId != b.Request.StreamId ? "stream_id" :
                a.Request.OwnerId != b.Request.OwnerId ? "owner_id" :
                a.Request.Ordinal != b.Request.Ordinal ? "ordinal" :
                a.Request.Mean != b.Request.Mean ? "mean" :
                a.Request.StandardDeviation != b.Request.StandardDeviation ? "standard_deviation" :
                a.Request.MinInclusive != b.Request.MinInclusive ? "min_inclusive" :
                a.Request.MaxInclusive != b.Request.MaxInclusive ? "max_inclusive" :
                a.Request.SourceVersion != b.Request.SourceVersion ? "source_version" :
                a.SelectedValue != b.SelectedValue ? "selected_value" :
                requireNativeCursorEquality && (
                    a.NativeCursorBefore is null || b.NativeCursorBefore is null ||
                    a.NativeCursorAfter is null || b.NativeCursorAfter is null ||
                    a.NativeCursorBefore != b.NativeCursorBefore ||
                    a.NativeCursorAfter != b.NativeCursorAfter) ? "native_cursors" :
                null;
            if (mismatch is not null)
                return new("RANDOM_BOUNDARY_MISMATCH", i, i, mismatch);
        }
        if (left.Length != right.Length)
            return new("RANDOM_BOUNDARY_LENGTH_MISMATCH", shared,
                shared, "request_count");
        return new("RANDOM_BOUNDARIES_CONSISTENT", shared, null, null);
    }
}
