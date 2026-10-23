namespace Sts2Emulator.Core;

/// <summary>Native combat-potion presence result paired with its exact
/// persistent Rewards unit-float decision.</summary>
public sealed record SemanticPotionRewardOddsNativeAuditV111(
    string SchemaId, int CurrentThousandths, bool Elite,
    int ThresholdThousandths, bool Offered, int UpdatedThousandths,
    string OutcomeFingerprint,
    RandomUnitFloatDecisionV111 ProviderDecision)
{
    public const string CurrentSchemaId =
        "sts2-v111-native-potion-reward-odds-audit-v1";

    public SemanticPotionRewardOddsNativeAuditV111 Fork() => this with
    {
        ProviderDecision = ProviderDecision with
        {
            Request = ProviderDecision.Request with { }
        }
    };

    public static SemanticPotionRewardOddsNativeAuditV111 Create(
        RandomUnitFloatDecisionV111 providerDecision,
        int currentThousandths, bool elite)
    {
        ArgumentNullException.ThrowIfNull(providerDecision);
        providerDecision.Validate();
        var threshold = checked(currentThousandths +
            (elite ? PrototypeNativePotionRewardOdds.EliteBonus : 0));
        var offered = providerDecision.SelectedValue < threshold / 1000f;
        var updated = checked(currentThousandths + (offered
            ? -PrototypeNativePotionRewardOdds.SuccessPenalty
            : PrototypeNativePotionRewardOdds.MissBonus));
        var audit = new SemanticPotionRewardOddsNativeAuditV111(
            CurrentSchemaId, currentThousandths, elite, threshold, offered,
            updated, string.Empty, providerDecision with
            {
                Request = providerDecision.Request with { }
            });
        audit = audit with { OutcomeFingerprint = audit.FingerprintFor() };
        audit.Validate();
        return audit;
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(ProviderDecision);
        ProviderDecision.Validate();
        var request = ProviderDecision.Request;
        if (SchemaId != CurrentSchemaId ||
            ProviderDecision.ProviderMode != "native-v111" ||
            request.SiteId != StochasticSiteRegistryV111
                .RewardPotionOddsUnitFloatSiteId ||
            request.StreamId != NativePlayerRngV111.RewardsStreamId ||
            request.OwnerId != "run:potion_reward_odds" ||
            ProviderDecision.ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Native potion-reward odds audit has unsupported provider provenance.");

        var expectedThreshold = checked(CurrentThousandths +
            (Elite ? PrototypeNativePotionRewardOdds.EliteBonus : 0));
        var expectedOffered = ProviderDecision.SelectedValue <
            expectedThreshold / 1000f;
        var expectedUpdated = checked(CurrentThousandths +
            (expectedOffered
                ? -PrototypeNativePotionRewardOdds.SuccessPenalty
                : PrototypeNativePotionRewardOdds.MissBonus));
        if (ThresholdThousandths != expectedThreshold ||
            Offered != expectedOffered ||
            UpdatedThousandths != expectedUpdated ||
            OutcomeFingerprint != FingerprintFor())
            throw new InvalidDataException(
                "Native potion-reward odds result differs from its float threshold.");
    }

    private string FingerprintFor() => CanonicalJson.Sha256(new
    {
        schemaId = SchemaId,
        currentThousandths = CurrentThousandths,
        elite = Elite,
        thresholdThousandths = ThresholdThousandths,
        offered = Offered,
        updatedThousandths = UpdatedThousandths,
        providerDecision = ProviderDecision
    });
}
