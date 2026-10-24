namespace Sts2Emulator.Core;

/// <summary>The raw Native GoldReward amount before later reward modifiers,
/// paired with its exact bounded player-rewards decision.</summary>
public sealed record SemanticCombatGoldNativeAuditV111(
    string SchemaId, string EventId, PrototypeRoomType Room, int Ascension,
    float GoldProportion, int Minimum, int Maximum, int Amount,
    RandomIndexDecisionV111 ProviderDecision,
    string ProbabilityStatus = "UNVERIFIED")
{
    public const string CurrentSchemaId =
        "sts2-v111-native-combat-gold-audit-v1";

    public SemanticCombatGoldNativeAuditV111 Fork() => this with
    {
        ProviderDecision = ProviderDecision with
        {
            Request = ProviderDecision.Request with
            {
                OrderedCandidates = (int[])ProviderDecision.Request
                    .OrderedCandidates.Clone()
            }
        }
    };

    public static SemanticCombatGoldNativeAuditV111 Create(
        RandomIndexDecisionV111 providerDecision, PrototypeRoomType room,
        int ascension, float goldProportion)
    {
        ArgumentNullException.ThrowIfNull(providerDecision);
        providerDecision.Validate();
        var bounds = PrototypeNativeCombatGoldReward.Bounds(
            room, ascension, goldProportion)
            ?? throw new InvalidDataException(
                "Native combat-gold audit cannot annotate a draw-free reward.");
        var amount = NativeSemanticOutcomeV111
            .SelectByBoundedOffset(providerDecision, bounds.Min,
                checked(bounds.Max - bounds.Min + 1));
        var audit = new SemanticCombatGoldNativeAuditV111(
            CurrentSchemaId, string.Empty, room, ascension, goldProportion,
            bounds.Min, bounds.Max, amount,
            providerDecision with
            {
                Request = providerDecision.Request with
                {
                    OrderedCandidates = (int[])providerDecision.Request
                        .OrderedCandidates.Clone()
                }
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
        var bounds = PrototypeNativeCombatGoldReward.Bounds(
            Room, Ascension, GoldProportion);
        var expectedOwner = bounds is { } value
            ? $"room:{Room}:ascension:{Ascension}:bounds:" +
              $"{value.Min}:{value.Max}"
            : null;
        if (request.BoundedCount is not { } boundedCount)
            throw new InvalidDataException(
                "Native combat-gold request has no bounded support.");
        if (SchemaId != CurrentSchemaId ||
            ProbabilityStatus != "UNVERIFIED" ||
            request.SiteId != StochasticSiteRegistryV111.NativeCombatGoldSiteId ||
            request.StreamId != NativePlayerRngV111.RewardsStreamId ||
            boundedCount <= 0 ||
            request.OwnerId != expectedOwner || bounds is null ||
            Minimum != bounds.Value.Min || Maximum != bounds.Value.Max ||
            boundedCount != checked(Maximum - Minimum + 1) ||
            Amount != NativeSemanticOutcomeV111.SelectByBoundedOffset(
                ProviderDecision, Minimum, boundedCount) ||
            EventId != FingerprintFor())
            throw new InvalidDataException(
                "Native combat-gold result differs from its bounded reward request.");
    }

    private string FingerprintFor() => CanonicalJson.Sha256(new
    {
        schemaId = SchemaId,
        room = Room,
        ascension = Ascension,
        goldProportion = GoldProportion,
        minimum = Minimum,
        maximum = Maximum,
        amount = Amount,
        providerDecision = ProviderDecision
    });
}
