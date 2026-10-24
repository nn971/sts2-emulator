namespace Sts2Emulator.Core;

/// <summary>One Native PotionFactory reward pick, pairing its rarity float
/// and within-rarity index with the ordered potion support and selected model.</summary>
public sealed record SemanticPotionFactoryNativeAuditV111(
    string SchemaId, string EventId, string SupportFingerprint,
    string[] CandidatePotionIds, PrototypeNativePotionRarity Rarity,
    string SelectedPotionId,
    RandomUnitFloatDecisionV111 RarityDecision,
    RandomIndexDecisionV111 ModelDecision,
    string ProbabilityStatus = "UNVERIFIED")
{
    private string[]? _candidatePotionIds = Clone(CandidatePotionIds);

    public const string CurrentSchemaId =
        "sts2-v111-native-potion-factory-audit-v1";

    public string[] CandidatePotionIds
    {
        get => Clone(_candidatePotionIds)!;
        init => _candidatePotionIds = Clone(value);
    }

    public SemanticPotionFactoryNativeAuditV111 Fork() => this with
    {
        CandidatePotionIds = _candidatePotionIds!,
        RarityDecision = RarityDecision with
        {
            Request = RarityDecision.Request with { }
        },
        ModelDecision = ModelDecision with
        {
            Request = ModelDecision.Request with
            {
                OrderedCandidates = (int[])ModelDecision.Request
                    .OrderedCandidates.Clone()
            }
        }
    };

    public static SemanticPotionFactoryNativeAuditV111 Create(
        RandomUnitFloatDecisionV111 rarityDecision,
        RandomIndexDecisionV111 modelDecision,
        IReadOnlyList<string> candidatePotionIds,
        PrototypeNativePotionRarity rarity, string selectedPotionId)
    {
        ArgumentNullException.ThrowIfNull(rarityDecision);
        ArgumentNullException.ThrowIfNull(modelDecision);
        ArgumentNullException.ThrowIfNull(candidatePotionIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPotionId);
        var candidates = candidatePotionIds.ToArray();
        var mappedSelectedPotionId = NativeSemanticOutcomeV111
            .SelectByChooseIndex(modelDecision, candidates);
        if (!StringComparer.Ordinal.Equals(
                mappedSelectedPotionId, selectedPotionId))
            throw new InvalidDataException(
                "Native PotionFactory result differs from its model request.");
        var supportFingerprint = CanonicalJson.Sha256(candidates);
        var audit = new SemanticPotionFactoryNativeAuditV111(
            CurrentSchemaId, string.Empty, supportFingerprint, candidates,
            rarity, selectedPotionId, rarityDecision with
            {
                Request = rarityDecision.Request with { }
            }, modelDecision with
            {
                Request = modelDecision.Request with
                {
                    OrderedCandidates = (int[])modelDecision.Request
                        .OrderedCandidates.Clone()
                }
            });
        audit = audit with { EventId = audit.EventFingerprint() };
        audit.Validate();
        return audit;
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(RarityDecision);
        ArgumentNullException.ThrowIfNull(ModelDecision);
        RarityDecision.Validate();
        ModelDecision.Validate();
        var rarityRequest = RarityDecision.Request;
        var modelRequest = ModelDecision.Request;
        var candidates = _candidatePotionIds;
        if (SchemaId != CurrentSchemaId ||
            ProbabilityStatus != "UNVERIFIED" ||
            RarityDecision.ProviderMode != "native-v111" ||
            rarityRequest.SiteId != StochasticSiteRegistryV111
                .NativePotionFactoryRarityRollSiteId ||
            modelRequest.SiteId != StochasticSiteRegistryV111
                .NativePotionFactoryModelSiteId ||
            rarityRequest.StreamId != NativePlayerRngV111.RewardsStreamId ||
            modelRequest.StreamId != NativePlayerRngV111.RewardsStreamId ||
            rarityRequest.OwnerId != modelRequest.OwnerId ||
            rarityRequest.Ordinal != modelRequest.Ordinal ||
            candidates is not { Length: > 0 } ||
            candidates.Any(string.IsNullOrWhiteSpace) ||
            candidates.Distinct(StringComparer.Ordinal).Count() !=
                candidates.Length ||
            SupportFingerprint != CanonicalJson.Sha256(candidates) ||
            !modelRequest.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, candidates.Length)) ||
            !StringComparer.Ordinal.Equals(
                NativeSemanticOutcomeV111.SelectByChooseIndex(
                    ModelDecision, candidates), SelectedPotionId) ||
            PrototypeNativePotionShop.ResolveRarityNative(
                RarityDecision.SelectedValue) != Rarity ||
            !IsOrderedEligibleSubset(candidates, Rarity) ||
            EventId != EventFingerprint())
            throw new InvalidDataException(
                "Native PotionFactory outcome differs from its rarity or model requests.");
    }

    private string EventFingerprint() => CanonicalJson.Sha256(new
    {
        schemaId = SchemaId,
        rarityDecision = RarityDecision,
        modelDecision = ModelDecision,
        supportFingerprint = SupportFingerprint,
        rarity = Rarity,
        selectedPotionId = SelectedPotionId
    });

    private static bool IsOrderedEligibleSubset(
        IReadOnlyList<string> candidates,
        PrototypeNativePotionRarity rarity)
    {
        var fullSupport = PrototypeNativePotionShop
            .CandidatesForRarity(rarity);
        int next = 0;
        foreach (var candidate in candidates)
        {
            while (next < fullSupport.Length &&
                fullSupport[next] != candidate)
                next++;
            if (next == fullSupport.Length)
                return false;
            next++;
        }
        return true;
    }

    private static string[]? Clone(string[]? values) => values is null
        ? null
        : (string[])values.Clone();
}
