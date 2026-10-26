namespace Sts2Emulator.Core;

/// <summary>
/// One source-owned potion-pool draw made while Entropic Brew fills an empty
/// slot. The run-action continuation owns the action replay; this event gives
/// the individual draw a semantic support for the pilot and Chance Law APIs.
/// </summary>
public sealed record SemanticRunPotionFillEventV111(
    string SchemaId, string EventId, string SiteId, string StreamId,
    string OwnerId, int Ordinal, string OperationKind,
    string[] CandidatePotionIds, string SupportFingerprint,
    string ActionBoundaryFingerprint, string ActionFingerprint,
    string ProbabilityStatus = "UNVERIFIED") : ISemanticChanceEventV111
{
    public const string CurrentSchemaId =
        "sts2-v111-semantic-run-potion-fill-v1";
    public const string CurrentSiteId = "run.potion.fill_empty_slots.model";

    private string[]? _candidatePotionIds = CloneOptional(CandidatePotionIds);

    public string[] CandidatePotionIds
    {
        get => CloneOptional(_candidatePotionIds)!;
        init => _candidatePotionIds = CloneOptional(value);
    }

    public string[] OrderedOutcomeIds => StreamId == "reward"
        ? _candidatePotionIds!.Select(potionId => "potion:" + potionId)
            .ToArray()
        : Enumerable.Range(0, _candidatePotionIds!.Length).Select(index =>
            "choice_index:" + index.ToString(
                System.Globalization.CultureInfo.InvariantCulture)).ToArray();

    public string[] SemanticOutcomeIds => CloneOptional(_candidatePotionIds)!;

    public SemanticRunPotionFillEventV111 Fork() => this with
    {
        CandidatePotionIds = _candidatePotionIds!
    };

    public void Validate()
    {
        var candidates = _candidatePotionIds;
        var request = candidates is null ? null :
            new RandomIndexRequestV111(
                SiteId, StreamId, OwnerId, Ordinal,
                Enumerable.Range(0, candidates.Length).ToArray(),
                OperationKind: OperationKind);
        if (SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(EventId) || SiteId != CurrentSiteId ||
            StreamId is not ("reward" or NativePlayerRngV111.RewardsStreamId) ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            OperationKind != "choose_index" ||
            candidates is not { Length: > 0 } ||
            !candidates.SequenceEqual(
                PrototypeContent.PotionPool, StringComparer.Ordinal) ||
            SupportFingerprint != CanonicalJson.Sha256(candidates) ||
            request is null ||
            !ControlledRunActionEngineV111
                .IsEntropicBrewFillRequestShape(request) ||
            string.IsNullOrWhiteSpace(ActionBoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(ActionFingerprint) ||
            ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Invalid semantic run potion-fill event.");
    }

    internal static SemanticRunPotionFillEventV111 DescribePending(
        RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var continuation = run.Stochastic.ControlledRunActionChoice ??
            throw new InvalidDataException(
                "Run potion-fill event has no controlled run-action frame.");
        var action = continuation.RunAction ??
            throw new InvalidDataException(
                "Run potion-fill event has no saved potion action.");
        if (continuation.ActionId != "use_potion" ||
            !ControlledRunActionEngineV111.IsEntropicBrewRunAction(
                continuation.PartialState, action) ||
            !ControlledRunActionEngineV111.IsEntropicBrewFillRequest(
                continuation.PartialState, action,
                continuation.PendingRequest))
            throw new InvalidDataException(
                "Controlled run-action frame is not an Entropic Brew slot fill.");

        var pending = ControlledRunActionPendingV111.Describe(run);
        var sourcePool = PrototypeContent.PotionPool;
        var labelsAreNumeric = pending.CandidateLabels.SequenceEqual(
            Enumerable.Range(0, pending.Request.SupportCount).Select(index =>
                index.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
            StringComparer.Ordinal);
        var candidates = pending.Request.StreamId switch
        {
            "reward" when pending.CandidateLabels.SequenceEqual(
                sourcePool, StringComparer.Ordinal) =>
                    (string[])pending.CandidateLabels.Clone(),
            NativePlayerRngV111.RewardsStreamId when
                pending.CandidateLabels.Length == 0 || labelsAreNumeric =>
                    (string[])sourcePool.Clone(),
            _ => throw new InvalidDataException(
                "Run potion-fill candidate labels differ from its ordered source pool.")
        };
        var eventValue = new SemanticRunPotionFillEventV111(
            CurrentSchemaId, pending.ChanceEventId,
            pending.Request.SiteId, pending.Request.StreamId,
            pending.Request.OwnerId, pending.Request.Ordinal,
            pending.Request.OperationKind, candidates,
            CanonicalJson.Sha256(candidates),
            pending.BoundaryFingerprint,
            PrototypeAiEnvironment.StableActionId(action));
        eventValue.Validate();
        return eventValue;
    }

    internal static void ValidateBoundary(
        RunState run, SemanticRunPotionFillEventV111 pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        var expected = DescribePending(run);
        if (CanonicalJson.Sha256(expected) != CanonicalJson.Sha256(pending))
            throw new InvalidDataException(
                "Semantic run potion-fill event differs from its saved action continuation.");
    }

    private static string[]? CloneOptional(string[]? values) => values is null
        ? null
        : (string[])values.Clone();
}

/// <summary>
/// Provider-private identity annotation for a Native Entropic Brew draw.
/// The cursor transition remains unverified against the game reference.
/// </summary>
public sealed record SemanticRunPotionFillNativeAuditV111(
    RandomIndexDecisionV111 ProviderDecision, string[] CandidatePotionIds)
{
    private string[]? _candidatePotionIds = CandidatePotionIds is null
        ? null : (string[])CandidatePotionIds.Clone();

    public const string CurrentSchemaId =
        "sts2-v111-native-run-potion-fill-audit-v1";

    public string[] CandidatePotionIds
    {
        get => _candidatePotionIds is null
            ? null! : (string[])_candidatePotionIds.Clone();
        init => _candidatePotionIds = value is null
            ? null : (string[])value.Clone();
    }

    public SemanticRunPotionFillNativeAuditV111 Fork() => this with
    {
        ProviderDecision = ProviderDecision with
        {
            Request = ProviderDecision.Request with
            {
                OrderedCandidates = (int[])ProviderDecision.Request
                    .OrderedCandidates.Clone()
            }
        },
        CandidatePotionIds = CandidatePotionIds
    };

    public void Validate()
    {
        var decision = ProviderDecision;
        var request = decision?.Request;
        var candidates = _candidatePotionIds;
        if (decision is null || request is null ||
            decision.ProviderMode != "native-v111" ||
            request.SiteId != SemanticRunPotionFillEventV111.CurrentSiteId ||
            request.StreamId != NativePlayerRngV111.RewardsStreamId ||
            request.OperationKind != "choose_index" ||
            candidates is null ||
            !candidates.SequenceEqual(PrototypeContent.PotionPool,
                StringComparer.Ordinal) ||
            request.SupportCount != candidates.Length ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, candidates.Length)))
            throw new InvalidDataException(
                "Invalid Native run potion-fill identity annotation.");
        decision.Validate();
    }

    public static SemanticRunPotionFillNativeAuditV111 Create(
        RandomIndexDecisionV111 decision, string[] candidatePotionIds)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(candidatePotionIds);
        var audit = new SemanticRunPotionFillNativeAuditV111(
            decision, candidatePotionIds);
        audit.Validate();
        return audit;
    }
}
