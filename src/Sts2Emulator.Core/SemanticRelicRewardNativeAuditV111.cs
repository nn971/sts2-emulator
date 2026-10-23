namespace Sts2Emulator.Core;

/// <summary>Native relic-reward rarity and committed queue result. The
/// ordered identities are the eligible fallback support from the persistent
/// Native bag immediately before its first-item pull.</summary>
public sealed record SemanticRelicRewardNativeAuditV111(
    string SchemaId, string EventId, string SupportFingerprint,
    PrototypeRelicRarity RequestedRarity,
    bool Merchant, PrototypeRelicRarity? SelectedTier,
    string[] CandidateRelicIds,
    string? SelectedRelicId,
    RandomUnitFloatDecisionV111 ProviderDecision,
    string ProbabilityStatus = "UNVERIFIED")
{
    private string[]? _candidateRelicIds = Clone(CandidateRelicIds);

    public const string CurrentSchemaId =
        "sts2-v111-native-relic-reward-audit-v1";

    public string[] CandidateRelicIds
    {
        get => Clone(_candidateRelicIds)!;
        init => _candidateRelicIds = Clone(value);
    }

    public SemanticRelicRewardNativeAuditV111 Fork() => this with
    {
        CandidateRelicIds = _candidateRelicIds!,
        ProviderDecision = ProviderDecision with
        {
            Request = ProviderDecision.Request with { }
        }
    };

    public static SemanticRelicRewardNativeAuditV111 Create(
        RandomUnitFloatDecisionV111 providerDecision,
        PrototypeRelicBagPullPlanV111 plan, string? selectedRelicId,
        bool merchant)
    {
        ArgumentNullException.ThrowIfNull(providerDecision);
        ArgumentNullException.ThrowIfNull(plan);
        providerDecision.Validate();
        var candidates = plan.CandidateIds;
        var audit = new SemanticRelicRewardNativeAuditV111(
            CurrentSchemaId, string.Empty, CanonicalJson.Sha256(candidates),
            plan.RequestedRarity, merchant, plan.SelectedTier, candidates,
            selectedRelicId, providerDecision with
            {
                Request = providerDecision.Request with { }
            });
        audit = audit with { EventId = audit.FingerprintFor() };
        audit.Validate();
        return audit;
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(ProviderDecision);
        ProviderDecision.Validate();
        var request = ProviderDecision.Request;
        var candidates = _candidateRelicIds;
        var empty = candidates is { Length: 0 };
        if (SchemaId != CurrentSchemaId ||
            ProbabilityStatus != "UNVERIFIED" ||
            ProviderDecision.ProviderMode != "native-v111" ||
            request.SiteId != StochasticSiteRegistryV111
                .RelicGrabBagRaritySiteId ||
            request.StreamId != NativePlayerRngV111.RewardsStreamId ||
            (Merchant
                ? !request.OwnerId.StartsWith("act:", StringComparison.Ordinal) ||
                  !request.OwnerId.Contains(":merchant_relic:",
                      StringComparison.Ordinal) ||
                  !request.OwnerId.EndsWith(":merchant", StringComparison.Ordinal)
                : request.OwnerId !=
                    PrototypeNativeRelicGrabBag.NativeTreasureRarityOwnerId +
                        ":reward") ||
            PrototypeNativeRelicGrabBag.ResolveRarityNative(
                ProviderDecision.SelectedValue) != RequestedRarity ||
            candidates is null || candidates.Any(string.IsNullOrWhiteSpace) ||
            candidates.Distinct(StringComparer.Ordinal).Count() !=
                candidates.Length ||
            SupportFingerprint != CanonicalJson.Sha256(candidates) ||
            (empty && (SelectedTier is not null ||
                       SelectedRelicId is not null)) ||
            (!empty && (SelectedTier is not { } tier ||
                        SelectedRelicId != candidates[0] ||
                        !PrototypeNativeRelicGrabBag
                            .PullFallbackTiers(RequestedRarity)
                            .Contains(tier) ||
                        candidates.Any(id =>
                            !PrototypeNativeRelicGrabBag.TryGetRarity(
                                id, out var actual) || actual != tier))) ||
            EventId != FingerprintFor())
            throw new InvalidDataException(
                "Native relic reward differs from its rarity and queue support.");
    }

    private string FingerprintFor() => CanonicalJson.Sha256(new
    {
        schemaId = SchemaId,
        supportFingerprint = SupportFingerprint,
        requestedRarity = RequestedRarity,
        merchant = Merchant,
        selectedTier = SelectedTier,
        selectedRelicId = SelectedRelicId,
        providerDecision = ProviderDecision
    });

    private static string[]? Clone(string[]? values) => values is null
        ? null
        : (string[])values.Clone();
}
