namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    /// <summary>
    /// Query a Native combat reward's relic identity from its still-hidden
    /// rarity queues after earlier reward pulls have been removed.
    /// </summary>
    public static ChanceLawV111 QueryNativeCombatRewardRelicSelection(
        RunState run, RandomIndexRequestV111 request,
        PrototypeRelicRarity rarity, PrototypeRelicBagState bag,
        string[] candidateIds)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(bag);
        ArgumentNullException.ThrowIfNull(candidateIds);
        PrototypeStateInvariants.Validate(run);
        request.Validate();
        if (run.Phase != RunPhase.Combat || run.World is not { } world ||
            run.Stochastic.NativeRunRng is null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            world.ActiveRoom is not (PrototypeRoomType.Combat or
                PrototypeRoomType.Elite or PrototypeRoomType.Boss) ||
            request.SiteId != PrototypeNativeRelicGrabBag
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
                "Native combat reward relic identity request is malformed.");

        var plan = PrototypeNativeRelicGrabBag.PlanNaturePull(
            bag, rarity, merchant: false, run.Player);
        var expectedCandidates = plan.CandidateIds;
        var ownerId = PrototypeNativeRelicGrabBag.TreasureModelOwnerId(
            world, rarity, candidateIds);
        if (plan.SelectedTier is null || request.OwnerId != ownerId ||
            !candidateIds.SequenceEqual(expectedCandidates,
                StringComparer.Ordinal))
            throw new InvalidDataException(
                "Native combat reward relic support differs from its conditioned hidden queue.");

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
        var weights = NativeNextIntGridWeights(candidateIds.Length);
        var outcomes = candidateIds.Select((id, index) =>
            new ChanceLawOutcomeV111(
                $"relic_model:{id}:index:{index}",
                weights[index]))
            .ToArray();
        return CreateNativeBoundaryLaw(
            request.SiteId, request.OperationKind, request.OwnerId,
            request.Ordinal, NativeTreasureRelicConditioningPolicyId,
            CanonicalJson.Sha256(conditioning),
            "native-combat-hidden-relic-queue-identity-unverified",
            outcomes);
    }
}
