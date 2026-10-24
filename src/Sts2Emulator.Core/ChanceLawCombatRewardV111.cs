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


    /// <summary>
    /// Query the next legacy card-reward rarity ticket after the caller's
    /// declared within-group selections. Empty rarity pools are removed using
    /// the same ordered prototype pool as reward generation.
    /// </summary>
    public static ChanceLawV111 QueryLegacyRewardCardRarityTicket(
        int act, string groupOwnerId, string[] priorCardIds, int ordinal)
    {
        if (act < 1)
            throw new ArgumentOutOfRangeException(nameof(act));
        ArgumentException.ThrowIfNullOrWhiteSpace(groupOwnerId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var available = RemainingRewardCardPool(priorCardIds);
        var rarityWeights = PrototypeContent.RewardRarityWeights(act)
            .Where(item => item.Weight > 0 && available.Any(cardId =>
                PrototypeContent.Card(cardId).Rarity == item.Rarity))
            .ToArray();
        if (rarityWeights.Length == 0)
            throw new ArgumentException(
                "The declared remaining reward card pool has no drawable rarity.",
                nameof(priorCardIds));

        var raritySupport = rarityWeights.Select(item => new
        {
            item.Rarity,
            item.Weight
        }).ToArray();
        var ownerId = groupOwnerId + ":card:" + priorCardIds.Length +
            ":weights:" + CanonicalJson.Sha256(raritySupport);
        var outcomes = rarityWeights.Select(item =>
            new ChanceLawOutcomeV111(
                "rarity:" + item.Rarity.ToString().ToLowerInvariant(),
                item.Weight)).ToArray();
        const string siteId = "reward.card_rarity.ticket";
        const string streamId = "reward";
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            groupOwnerId,
            ordinal,
            act,
            priorCardIds,
            availableCardPoolFingerprint = CanonicalJson.Sha256(available),
            raritySupport,
            totalWeight = rarityWeights.Sum(item => item.Weight)
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-reward-card-rarity-ticket-v1", conditioning,
            "Uniform ticket-index weights over the source-shaped prototype " +
            "rarity weights after empty tiers are removed; reward pool and " +
            "Native parity remain unverified.", outcomes);
    }

    /// <summary>
    /// Query the legacy reward card identity draw within its selected rarity,
    /// after removing card identities already selected in the same group.
    /// </summary>
    public static ChanceLawV111 QueryLegacyRewardCardModel(
        int act, string groupOwnerId, PrototypeCardRarity rarity,
        string[] priorCardIds, int ordinal)
    {
        if (act < 1)
            throw new ArgumentOutOfRangeException(nameof(act));
        ArgumentException.ThrowIfNullOrWhiteSpace(groupOwnerId);
        if (!Enum.IsDefined(rarity))
            throw new ArgumentOutOfRangeException(nameof(rarity));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (!PrototypeContent.RewardRarityWeights(act).Any(item =>
                item.Rarity == rarity && item.Weight > 0))
            throw new ArgumentException(
                "The declared rarity is not selectable by the reward rarity ticket for this Act.",
                nameof(rarity));

        var available = RemainingRewardCardPool(priorCardIds);
        var candidates = available.Where(cardId =>
            PrototypeContent.Card(cardId).Rarity == rarity).ToArray();
        if (candidates.Length == 0)
            throw new ArgumentException(
                "The declared rarity and prior selections leave no reward card to draw.",
                nameof(priorCardIds));

        const string siteId = "reward.card_model.index_draw";
        const string streamId = "reward";
        var poolFingerprint = CanonicalJson.Sha256(candidates);
        var ownerId = groupOwnerId + ":card:" + priorCardIds.Length +
            ":rarity:" + rarity + ":pool:" + poolFingerprint;
        var outcomes = candidates.Select(cardId =>
            new ChanceLawOutcomeV111("card:" + cardId, 1L)).ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            groupOwnerId,
            ordinal,
            act,
            rarity,
            priorCardIds,
            orderedCandidates = candidates,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-reward-card-model-pool-v1", conditioning,
            "Equal index weights over the remaining source-ordered prototype " +
            "cards of the declared rarity; pool fidelity and Native parity " +
            "remain unverified.", outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query the retained legacy binary combat-potion ticket. The caller must
    /// declare that potion acquisition is currently allowed; otherwise the
    /// source branch does not draw this ticket.
    /// </summary>
    public static ChanceLawV111 QueryLegacyPotionDropTicket(
        int act, int floor, PrototypeRoomType room,
        bool canAcquirePotion, int ordinal)
    {
        if (act < 1)
            throw new ArgumentOutOfRangeException(nameof(act));
        if (floor < 0)
            throw new ArgumentOutOfRangeException(nameof(floor));
        if (room is not (PrototypeRoomType.Combat or
            PrototypeRoomType.Elite or PrototypeRoomType.Boss))
            throw new ArgumentOutOfRangeException(nameof(room),
                "Legacy combat-potion tickets require Combat, Elite, or Boss rewards.");
        if (!canAcquirePotion)
            throw new ArgumentException(
                "The legacy potion-drop ticket is not drawn when potion acquisition is prevented.",
                nameof(canAcquirePotion));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "reward.random_potion.drop.ticket";
        const string streamId = "reward";
        var ownerId = $"act:{act}:floor:{floor}:room:{room}";
        var outcomes = new[]
        {
            new ChanceLawOutcomeV111("potion_reward:offered", 1L),
            new ChanceLawOutcomeV111("potion_reward:not_offered", 1L)
        };
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            act,
            floor,
            room,
            canAcquirePotion,
            ticketCount = 2,
            offeredTicket = 0
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-combat-potion-drop-ticket-v1", conditioning,
            "Uniform two-way legacy bounded ticket when potion acquisition is " +
            "allowed; the legacy RNG law remains unverified against Native.",
            outcomes);
    }

    /// <summary>
    /// Query the legacy reward-potion model after a successful drop ticket.
    /// It uses the fixed ordered prototype pool and does not verify source
    /// pool completeness or Native stream parity.
    /// </summary>
    public static ChanceLawV111 QueryLegacyRewardPotionModel(
        int act, int floor, PrototypeRoomType room,
        bool canAcquirePotion, bool dropOffered, int ordinal)
    {
        if (act < 1)
            throw new ArgumentOutOfRangeException(nameof(act));
        if (floor < 0)
            throw new ArgumentOutOfRangeException(nameof(floor));
        if (room is not (PrototypeRoomType.Combat or
            PrototypeRoomType.Elite or PrototypeRoomType.Boss))
            throw new ArgumentOutOfRangeException(nameof(room),
                "Legacy combat-potion rewards require Combat, Elite, or Boss rooms.");
        if (!canAcquirePotion || !dropOffered)
            throw new ArgumentException(
                "The reward-potion model draw requires an allowed, successful potion drop.");
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "reward.random_potion.model";
        const string streamId = "reward";
        var pool = PrototypeContent.PotionPool;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The legacy reward-potion pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = $"act:{act}:floor:{floor}:room:{room}:pool:" +
            poolFingerprint;
        var outcomes = pool.Select(potionId =>
            new ChanceLawOutcomeV111("potion:" + potionId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            act,
            floor,
            room,
            canAcquirePotion,
            dropOffered,
            orderedPotionPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-reward-potion-model-pool-v1", conditioning,
            "Equal index weights over the declared ordered prototype reward " +
            "potion pool; pool fidelity and Native parity remain unverified.",
            outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query the legacy 1,000-way combat-potion pity ticket. Forced rewards
    /// bypass this draw; the configured Native float route has a separate law.
    /// </summary>
    public static ChanceLawV111 QueryLegacyPotionRewardOddsTicket(
        int currentThousandths, bool elite, bool forced, int ordinal)
    {
        if (forced)
            throw new ArgumentException(
                "Forced potion rewards bypass the legacy odds ticket.",
                nameof(forced));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "reward.potion_odds.ticket";
        const string streamId = "reward";
        const string ownerId = "run:potion_reward_odds";
        var threshold = checked(currentThousandths +
            (elite ? PrototypeNativePotionRewardOdds.EliteBonus : 0));
        var offeredWeight = Math.Clamp((long)threshold, 0L, 1000L);
        var missedWeight = 1000L - offeredWeight;
        var outcomes = new List<ChanceLawOutcomeV111>(2);
        if (offeredWeight > 0)
            outcomes.Add(new ChanceLawOutcomeV111(
                "potion_reward:offered", offeredWeight));
        if (missedWeight > 0)
            outcomes.Add(new ChanceLawOutcomeV111(
                "potion_reward:not_offered", missedWeight));
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            currentThousandths,
            elite,
            forced,
            threshold,
            ticketCount = 1000,
            comparison = "ticket_less_than_threshold"
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-potion-reward-odds-ticket-v1", conditioning,
            "Uniform 1,000-way legacy bounded ticket with the source-shaped " +
            "pity and Elite threshold; RNG parity remains unverified.",
            outcomes.ToArray());
    }

    /// <summary>
    /// Query the legacy Elite relic model draw over its fixed ordered pool.
    /// Configured Native relic-grab-bag generation uses a separate route.
    /// </summary>
    public static ChanceLawV111 QueryLegacyEliteRelicModel(
        int act, int floor, int ordinal)
    {
        if (act < 1)
            throw new ArgumentOutOfRangeException(nameof(act));
        if (floor < 0)
            throw new ArgumentOutOfRangeException(nameof(floor));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "reward.elite_relic.model";
        const string streamId = "reward";
        var pool = PrototypeContent.RelicPool;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The legacy Elite relic pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = $"act:{act}:floor:{floor}:pool:" + poolFingerprint;
        var outcomes = pool.Select(relicId =>
            new ChanceLawOutcomeV111("relic:" + relicId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            act,
            floor,
            orderedRelicPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-elite-relic-model-pool-v1", conditioning,
            "Equal index weights over the ordered prototype Elite relic pool; " +
            "source-pool fidelity and Native relic-bag parity remain " +
            "unverified.", outcomes, operationKind: "choose_index");
    }
}
