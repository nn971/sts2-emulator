namespace Sts2Emulator.Core;

/// <summary>Typed Native card-reward rarity and upgrade outcomes paired with
/// their exact reward-stream float decisions.</summary>
public sealed record SemanticCardOddsNativeAuditV111(
    string SchemaId, string OutcomeKind,
    PrototypeNativeCardOddsSource? Source,
    int? CurrentOffsetBasisPoints, int? Ascension,
    PrototypeCardRarity? RolledRarity, int? NextOffsetBasisPoints,
    string? CardId, int? Act, int? ThresholdBasisPoints,
    int? UncommonEndBasisPoints, bool? Upgraded,
    string OutcomeFingerprint,
    RandomUnitFloatDecisionV111 ProviderDecision)
{
    public const string CurrentSchemaId =
        "sts2-v111-native-card-odds-audit-v1";
    public const string RarityOperation = "encounter_rarity";
    public const string UpgradeOperation = "encounter_upgrade";
    public const string RaritySiteId =
        StochasticSiteRegistryV111.CardRarityRollUnitFloatSiteId;
    public const string UpgradeSiteId =
        StochasticSiteRegistryV111.CardRarityUpgradeRollUnitFloatSiteId;

    public SemanticCardOddsNativeAuditV111 Fork() => this with
    {
        ProviderDecision = ProviderDecision with
        {
            Request = ProviderDecision.Request with { }
        }
    };

    public static SemanticCardOddsNativeAuditV111 CreateRarity(
        RandomUnitFloatDecisionV111 providerDecision,
        PrototypeNativeCardOddsSource source,
        int currentOffsetBasisPoints, int ascension)
    {
        ArgumentNullException.ThrowIfNull(providerDecision);
        providerDecision.Validate();
        var rarity = PrototypeNativeCardRarityOdds.ResolveRarity(
            providerDecision.SelectedValue, currentOffsetBasisPoints,
            source, ascension);
        var nextOffset = PrototypeNativeCardRarityOdds
            .NextOffsetBasisPoints(
                rarity, currentOffsetBasisPoints, ascension, source);
        var (rareThreshold, uncommonEnd) = PrototypeNativeCardRarityOdds
            .NativeFloatThresholds(
                currentOffsetBasisPoints, ascension, source);
        return CreateCore(
            providerDecision, RarityOperation, source,
            currentOffsetBasisPoints, ascension, rarity, nextOffset,
            null, null, rareThreshold, null, uncommonEnd);
    }

    public static SemanticCardOddsNativeAuditV111 CreateUpgrade(
        RandomUnitFloatDecisionV111 providerDecision,
        string cardId, int act, int ascension)
    {
        ArgumentNullException.ThrowIfNull(providerDecision);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        providerDecision.Validate();
        var threshold = PrototypeNativeCardRarityOdds
            .EncounterCardUpgradeThresholdBasisPoints(
                cardId, act, ascension);
        var upgraded = providerDecision.SelectedValue <= threshold / 10000f;
        return CreateCore(
            providerDecision, UpgradeOperation, null, null, ascension,
            null, null, cardId, act, threshold, upgraded, null);
    }

    private static SemanticCardOddsNativeAuditV111 CreateCore(
        RandomUnitFloatDecisionV111 providerDecision, string outcomeKind,
        PrototypeNativeCardOddsSource? source,
        int? currentOffsetBasisPoints, int? ascension,
        PrototypeCardRarity? rolledRarity, int? nextOffsetBasisPoints,
        string? cardId, int? act, int thresholdBasisPoints,
        bool? upgraded, int? uncommonEndBasisPoints)
    {
        var audit = new SemanticCardOddsNativeAuditV111(
            CurrentSchemaId, outcomeKind, source,
            currentOffsetBasisPoints, ascension, rolledRarity,
            nextOffsetBasisPoints, cardId, act, thresholdBasisPoints,
            uncommonEndBasisPoints, upgraded, string.Empty,
            providerDecision with
            {
                Request = providerDecision.Request with { }
            });
        audit = audit with
        {
            OutcomeFingerprint = audit.FingerprintFor()
        };
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
            request.StreamId != NativePlayerRngV111.RewardsStreamId ||
            ProviderDecision.ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Native card-odds audit has unsupported provider provenance.");

        int? expectedUncommonEnd = null;
        if (OutcomeKind == RarityOperation)
        {
            if (request.SiteId != RaritySiteId || Source is null ||
                CurrentOffsetBasisPoints is not { } offset ||
                Ascension is not { } rarityAscension ||
                RolledRarity is not { } rarity ||
                NextOffsetBasisPoints is not { } nextOffset ||
                CardId is not null || Act is not null || Upgraded is not null)
                throw new InvalidDataException(
                    "Native encounter-rarity audit has an invalid shape.");
            var room = Source switch
            {
                PrototypeNativeCardOddsSource.RegularEncounter => "Combat",
                PrototypeNativeCardOddsSource.EliteEncounter => "Elite",
                PrototypeNativeCardOddsSource.BossEncounter => "Boss",
                _ => throw new InvalidDataException(
                    "Native encounter-rarity audit has an unsupported source.")
            };
            if (!RarityOwnerMatches(request.OwnerId, room, offset))
                throw new InvalidDataException(
                    "Native encounter-rarity audit owner differs from its source context.");
            var expectedThresholds = PrototypeNativeCardRarityOdds
                .NativeFloatThresholds(offset, rarityAscension, Source.Value);
            expectedUncommonEnd = expectedThresholds.UncommonEnd;
            var expectedRarity = PrototypeNativeCardRarityOdds.ResolveRarity(
                ProviderDecision.SelectedValue, offset, Source.Value,
                rarityAscension);
            var expectedNextOffset = PrototypeNativeCardRarityOdds
                .NextOffsetBasisPoints(
                    expectedRarity, offset, rarityAscension, Source.Value);
            if (ThresholdBasisPoints != expectedThresholds.Rare ||
                UncommonEndBasisPoints != expectedUncommonEnd ||
                rarity != expectedRarity || nextOffset != expectedNextOffset)
                throw new InvalidDataException(
                    "Native encounter-rarity result differs from its float threshold.");
        }
        else if (OutcomeKind == UpgradeOperation)
        {
            if (request.SiteId != UpgradeSiteId || Source is not null ||
                CurrentOffsetBasisPoints is not null ||
                Ascension is not { } upgradeAscension ||
                string.IsNullOrWhiteSpace(CardId) || Act is not { } act ||
                ThresholdBasisPoints is not { } upgradeThreshold ||
                Upgraded is not { } upgraded || RolledRarity is not null ||
                NextOffsetBasisPoints is not null ||
                UncommonEndBasisPoints is not null ||
                request.OwnerId !=
                    $"card:{CardId}:act:{act}:ascension:{upgradeAscension}")
                throw new InvalidDataException(
                    "Native encounter-upgrade audit has an invalid shape or owner.");
            var expectedThreshold = PrototypeNativeCardRarityOdds
                .EncounterCardUpgradeThresholdBasisPoints(
                    CardId!, act, upgradeAscension);
            if (upgradeThreshold != expectedThreshold ||
                upgraded != (ProviderDecision.SelectedValue <=
                    expectedThreshold / 10000f))
                throw new InvalidDataException(
                    "Native encounter-upgrade result differs from its float threshold.");
        }
        else
            throw new InvalidDataException(
                "Native card-odds audit operation is unsupported.");

        if (OutcomeFingerprint != FingerprintFor())
            throw new InvalidDataException(
                "Native card-odds audit fingerprint is invalid.");
    }

    private string FingerprintFor() =>
        CanonicalJson.Sha256(new
        {
            schemaId = SchemaId,
            outcomeKind = OutcomeKind,
            source = Source,
            currentOffsetBasisPoints = CurrentOffsetBasisPoints,
            ascension = Ascension,
            rolledRarity = RolledRarity,
            nextOffsetBasisPoints = NextOffsetBasisPoints,
            cardId = CardId,
            act = Act,
            thresholdBasisPoints = ThresholdBasisPoints,
            uncommonEndBasisPoints = UncommonEndBasisPoints,
            upgraded = Upgraded,
            providerDecision = ProviderDecision
        });

    private static bool RarityOwnerMatches(
        string ownerId, string room, int offset)
    {
        var parts = ownerId.Split(':');
        return parts.Length == 8 && parts[0] == "act" &&
            int.TryParse(parts[1],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var act) && act >= 1 && parts[2] == "room" &&
            parts[3] == room && parts[4] == "pick" &&
            int.TryParse(parts[5],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var pick) && pick >= 0 && parts[6] == "offset" &&
            int.TryParse(parts[7],
                System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture,
                out var ownerOffset) && ownerOffset == offset;
    }
}
