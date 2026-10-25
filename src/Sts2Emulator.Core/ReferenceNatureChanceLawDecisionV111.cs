using System.Globalization;

namespace Sts2Emulator.Core;

/// <summary>
/// Maps one sampled semantic Chance Law outcome back to the typed source
/// provider value consumed by ordinary mechanics. Current mappings cover
/// native merchant laws, Native event gold, and the Endless Conveyor ticket.
/// </summary>
public static class ReferenceNatureChanceLawDecisionV111
{
    private const uint OneFloatBits = 0x3f800000;

    public static ControlledStochasticDecisionV111 FromInitialState(
        ChanceLawV111 law,
        ControlledStochasticRequestV111 request,
        ReferenceNatureStateV111 initialNatureState,
        IReadOnlyList<string> orderedOutcomeIds)
    {
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ArgumentNullException.ThrowIfNull(orderedOutcomeIds);
        var selection = ReferenceNatureV111.SampleValidated(
            law, initialNatureState);
        var decision = FromOrderedIndexSelection(
            law, request, selection, orderedOutcomeIds);
        var indexDecision = decision.IndexDecision ??
            throw new InvalidDataException(
                "An ordered index selection did not produce an index decision.");
        var controlled = ControlledStochasticDecisionV111.FromIndex(
            indexDecision, orderedOutcomeIds.ToArray());
        controlled.Validate();
        return controlled;
    }

    public static ControlledStochasticDecisionV111 FromOrderedIndexSelection(
        ChanceLawV111 law,
        ControlledStochasticRequestV111 request,
        ReferenceNatureSelectionV111 selection,
        IReadOnlyList<string> orderedOutcomeIds)
    {
        ArgumentNullException.ThrowIfNull(orderedOutcomeIds);
        _ = ValidateSelection(law, request, selection);
        if (request.IndexRequest is not { } indexRequest ||
            law.OperationKind != indexRequest.OperationKind ||
            law.SupportCount != indexRequest.SupportCount ||
            orderedOutcomeIds.Count != indexRequest.SupportCount)
            throw new InvalidDataException(
                "ReferenceNature selection does not match the ordered index support.");
        for (var index = 0; index < orderedOutcomeIds.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(orderedOutcomeIds[index]) ||
                law.OutcomeIdAt(index) != orderedOutcomeIds[index])
                throw new InvalidDataException(
                    "Chance Law outcome order differs from its typed index support.");
        }

        var decision = new RandomIndexDecisionV111(
            indexRequest with
            {
                OrderedCandidates = (int[])indexRequest.OrderedCandidates.Clone()
            }, selection.OutcomeIndex, "controlled-v111");
        decision.Validate();
        var controlled = ControlledStochasticDecisionV111.FromIndex(decision);
        controlled.Validate();
        return controlled;
    }

    public static ControlledStochasticDecisionV111 FromSelection(
        ChanceLawV111 law,
        ControlledStochasticRequestV111 request,
        ReferenceNatureSelectionV111 selection,
        RunState? conditioningRun = null,
        string[]? candidateLabels = null)
    {
        var outcome = ValidateSelection(law, request, selection);
        if (request.IndexRequest is { } indexRequest)
        {
            if (indexRequest.SiteId ==
                    PrototypeNativeLaterActMap.GloryRestCountSiteId)
            {
                var expectedLaw = ChanceLawEngineV111
                    .QueryLaterActGloryRestCount(indexRequest.Ordinal);
                if (candidateLabels is not null ||
                    law.LawId != expectedLaw.LawId ||
                    indexRequest.StreamId != "context.act_3_map" ||
                    indexRequest.OwnerId != "map:act:3" ||
                    indexRequest.Ordinal != 0 ||
                    indexRequest.OperationKind != "bounded_integer" ||
                    indexRequest.BoundedCount != 2 ||
                    outcome.OutcomeId != "rest_quota:" +
                        (5 + selection.OutcomeIndex).ToString(
                            CultureInfo.InvariantCulture))
                    throw new InvalidDataException(
                        "Glory rest-quota law differs from its bounded-index request.");
                var restDecision = new RandomIndexDecisionV111(
                    indexRequest with
                    {
                        OrderedCandidates = (int[])
                            indexRequest.OrderedCandidates.Clone()
                    }, selection.OutcomeIndex, "controlled-v111");
                restDecision.Validate();
                var controlledRest = ControlledStochasticDecisionV111
                    .FromIndex(restDecision);
                controlledRest.Validate();
                return controlledRest;
            }
            if (law.OperationKind == "index_mapped_gold")
            {
                if (candidateLabels is not null ||
                    indexRequest.SiteId != ChanceLawEngineV111
                        .NativeEventGoldIndexSiteId ||
                    indexRequest.OperationKind != "bounded_integer" ||
                    !outcome.OutcomeId.StartsWith(
                        "gold:", StringComparison.Ordinal) ||
                    !int.TryParse(outcome.OutcomeId[5..],
                        NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out var gold))
                    throw new InvalidDataException(
                        "Mapped-gold Chance Law does not match its bounded-index request.");
                var (eventId, stage) = ParseNativeEventGoldOwner(
                    indexRequest.OwnerId);
                var indexWeights = ChanceLawEngineV111
                    .NativeNextIntGridWeights(indexRequest.SupportCount);
                var expectedGoldWeights = new SortedDictionary<int, long>();
                for (var index = 0; index < indexWeights.Length; index++)
                {
                    var mappedGold = ChanceLawEngineV111
                        .NativeEventGoldIndexOutcome(eventId, stage, index);
                    expectedGoldWeights[mappedGold] = checked(
                        expectedGoldWeights.GetValueOrDefault(mappedGold) +
                        indexWeights[index]);
                }
                var expectedOutcomes = expectedGoldWeights.Select(pair =>
                    new ChanceLawOutcomeV111(
                        "gold:" + pair.Key.ToString(
                            CultureInfo.InvariantCulture), pair.Value))
                    .ToArray();
                if (law.SiteId != indexRequest.SiteId ||
                    !law.Outcomes.SequenceEqual(expectedOutcomes))
                    throw new InvalidDataException(
                        "Mapped-gold Chance Law differs from the bounded-index source mapping.");
                var selectedIndex = Enumerable.Range(
                        0, indexRequest.SupportCount)
                    .FirstOrDefault(index =>
                        ChanceLawEngineV111.NativeEventGoldIndexOutcome(
                            eventId, stage, index) == gold, -1);
                if (selectedIndex < 0)
                    throw new InvalidDataException(
                        "Selected gold outcome has no source bounded-index representative.");
                var mappedDecision = new RandomIndexDecisionV111(
                    indexRequest with
                    {
                        OrderedCandidates = (int[])
                            indexRequest.OrderedCandidates.Clone()
                    }, selectedIndex, "controlled-v111");
                mappedDecision.Validate();
                var mappedControlled = ControlledStochasticDecisionV111
                    .FromIndex(mappedDecision);
                mappedControlled.Validate();
                return mappedControlled;
            }
            if (law.OperationKind == "legacy_index_mapped_gold")
            {
                var candidates = indexRequest.OrderedCandidates;
                var expectedOutcomes = candidates.Select(gold =>
                    new ChanceLawOutcomeV111(
                        "gold:" + gold.ToString(
                            CultureInfo.InvariantCulture), 1L)).ToArray();
                if (candidateLabels is not null ||
                    indexRequest.SiteId != ChanceLawEngineV111
                        .NativeEventGoldIndexSiteId ||
                    indexRequest.StreamId != "event" ||
                    indexRequest.OperationKind != "choose_index" ||
                    law.SiteId != indexRequest.SiteId ||
                    law.OperationKind != "legacy_index_mapped_gold" ||
                    law.OwnerId != indexRequest.OwnerId ||
                    law.Ordinal != indexRequest.Ordinal ||
                    law.ConditioningPolicyId !=
                        "native-regional-legacy-event-gold-index-v1" ||
                    law.ProbabilityStatus != ChanceLawV111.UnverifiedStatus ||
                    law.ProbabilityProvenance !=
                        "uniform-saved-legacy-event-gold-index-support-unverified" ||
                    !law.Outcomes.SequenceEqual(expectedOutcomes))
                    throw new InvalidDataException(
                        "Legacy mapped-gold Chance Law does not match its saved event-stream index request.");
                var legacyMappedDecision = new RandomIndexDecisionV111(
                    indexRequest with
                    {
                        OrderedCandidates = (int[])
                            indexRequest.OrderedCandidates.Clone()
                    }, selection.OutcomeIndex, "controlled-v111");
                legacyMappedDecision.Validate();
                var legacyMappedControlled = ControlledStochasticDecisionV111
                    .FromIndex(legacyMappedDecision);
                legacyMappedControlled.Validate();
                return legacyMappedControlled;
            }
            if (indexRequest.SiteId == "reward.combat_gold.index_draw")
            {
                if (candidateLabels is not null ||
                    indexRequest.OperationKind != "bounded_integer" ||
                    !outcome.OutcomeId.StartsWith(
                        "gold:", StringComparison.Ordinal) ||
                    !int.TryParse(outcome.OutcomeId[5..],
                        NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out var selectedGold))
                    throw new InvalidDataException(
                        "Combat gold Chance Law does not match its bounded-integer request.");
                var ownerParts = indexRequest.OwnerId.Split(':');
                if (ownerParts.Length != 7 || ownerParts[0] != "room" ||
                    !Enum.TryParse<PrototypeRoomType>(ownerParts[1], out var room) ||
                    ownerParts[2] != "ascension" ||
                    !int.TryParse(ownerParts[3], out var ascension) ||
                    ownerParts[4] != "bounds" ||
                    !int.TryParse(ownerParts[5], out var minimum) ||
                    !int.TryParse(ownerParts[6], out var maximum) ||
                    selectedGold < minimum || selectedGold > maximum)
                    throw new InvalidDataException(
                        "Combat gold owner or selected amount is malformed.");
                var expectedLaw = ChanceLawEngineV111.QueryNativeCombatGold(
                    room, ascension,
                    room == PrototypeRoomType.Combat ? 1f : null,
                    indexRequest.Ordinal);
                if (CanonicalJson.Sha256(law) !=
                        CanonicalJson.Sha256(expectedLaw) ||
                    indexRequest.SupportCount != maximum - minimum + 1)
                    throw new InvalidDataException(
                        "Combat gold Chance Law differs from its Native reward bounds.");
                var selectedIndex = selectedGold - minimum;
                var goldDecision = new RandomIndexDecisionV111(
                    indexRequest with
                    {
                        OrderedCandidates = (int[])
                            indexRequest.OrderedCandidates.Clone()
                    }, selectedIndex, "controlled-v111");
                goldDecision.Validate();
                var controlledGold = ControlledStochasticDecisionV111
                    .FromIndex(goldDecision);
                controlledGold.Validate();
                return controlledGold;
            }
            if (indexRequest.SiteId == StochasticSiteRegistryV111
                    .NativeGloryTrialDamageSiteId)
            {
                if (candidateLabels is not null ||
                    conditioningRun is null)
                    throw new InvalidDataException(
                        "Glory Trial case mapping requires its saved event boundary without candidate labels.");
                var plan = TrialCaseChoicePlanBuilderV111.Build(
                    conditioningRun);
                var expectedLaw = ChanceLawEngineV111.QueryTrial(
                    conditioningRun);
                if (!ControlledStochasticChanceReplayV111.RequestsMatch(
                        plan.Request, indexRequest) ||
                    CanonicalJson.Sha256(law) !=
                        CanonicalJson.Sha256(expectedLaw) ||
                    selection.OutcomeIndex >= plan.OutcomeIds.Length ||
                    outcome.OutcomeId !=
                        plan.OutcomeIds[selection.OutcomeIndex])
                    throw new InvalidDataException(
                        "Glory Trial Chance Law differs from its named case support.");
                var trialDecision = new RandomIndexDecisionV111(
                    indexRequest with
                    {
                        OrderedCandidates = (int[])
                            indexRequest.OrderedCandidates.Clone()
                    }, selection.OutcomeIndex, "controlled-v111");
                trialDecision.Validate();
                var controlledTrial = ControlledStochasticDecisionV111
                    .FromIndex(trialDecision);
                controlledTrial.Validate();
                return controlledTrial;
            }
            if (law.OperationKind == "bounded_integer_no_effect")
            {
                if (candidateLabels is not null ||
                    conditioningRun is null ||
                    indexRequest.SiteId is not (
                        ChanceLawEngineV111
                            .NativeStoneOfAllTimePushTicketSiteId or
                        ChanceLawEngineV111
                            .NativeStoneOfAllTimeLiftTicketSiteId) ||
                    indexRequest.OperationKind != "bounded_integer" ||
                    indexRequest.BoundedCount != 100 ||
                    outcome.OutcomeId != "no_effect")
                    throw new InvalidDataException(
                        "Stone of All Time no-effect law differs from its typed ticket request.");
                var actionId = law.OwnerId switch
                {
                    "event:stone_of_all_time:push" => "push",
                    "event:stone_of_all_time:lift" => "lift",
                    _ => throw new InvalidDataException(
                        "Stone of All Time no-effect law has an unknown action owner.")
                };
                var expectedLaw = ChanceLawEngineV111
                    .QueryNativeStoneOfAllTimeNoEffectIndex(
                        conditioningRun, indexRequest.SiteId,
                        indexRequest.Ordinal, actionId);
                var expectedStream =
                    conditioningRun.Stochastic.NativeRunRng is not null
                        ? NativeRngV111Contexts.EventStreamId(
                            PrototypeNativeStoneOfAllTime.NativeRngEventId)
                        : "event";
                if (CanonicalJson.Sha256(law) !=
                        CanonicalJson.Sha256(expectedLaw) ||
                    indexRequest.StreamId != expectedStream ||
                    indexRequest.OwnerId != law.OwnerId ||
                    indexRequest.Ordinal != law.Ordinal)
                    throw new InvalidDataException(
                        "Stone of All Time no-effect law differs from its event boundary.");

                // The source ticket is mechanically discarded. Use a stable
                // representative to resume the bounded provider request.
                var noEffectDecision = new RandomIndexDecisionV111(
                    indexRequest with
                    {
                        OrderedCandidates = (int[])
                            indexRequest.OrderedCandidates.Clone()
                    }, 0, "controlled-v111");
                noEffectDecision.Validate();
                var controlledNoEffect = ControlledStochasticDecisionV111
                    .FromIndex(noEffectDecision);
                controlledNoEffect.Validate();
                return controlledNoEffect;
            }
            if (law.OperationKind == "event_offer_index")
                return FromAncientOfferSelection(
                    law, indexRequest, selection, outcome,
                    conditioningRun, candidateLabels);
            if (law.OperationKind != indexRequest.OperationKind ||
                law.SupportCount != indexRequest.SupportCount ||
                !OutcomeEndsInIndex(outcome.OutcomeId,
                    selection.OutcomeIndex))
                throw new NotSupportedException(
                    "Chance Law support does not map one-to-one to this index request.");
            var labels = candidateLabels is { Length: > 0 }
                ? candidateLabels : null;
            var decision = new RandomIndexDecisionV111(
                indexRequest with
                {
                    OrderedCandidates = (int[])
                        indexRequest.OrderedCandidates.Clone()
                }, selection.OutcomeIndex, "controlled-v111");
            decision.Validate();
            var controlled = ControlledStochasticDecisionV111.FromIndex(
                decision, labels);
            controlled.Validate();
            return controlled;
        }

        if (request.GaussianIntRequest is { } gaussianIntRequest)
        {
            if (candidateLabels is not null ||
                law.OperationKind != "gaussian_int" ||
                !ChanceLawEngineV111.MatchesNativeMapGaussianIntRequest(
                    gaussianIntRequest))
                throw new NotSupportedException(
                    "Gaussian-integer Chance Law mapping currently supports only the registered Native map quota requests.");
            var expectedLaw = ChanceLawEngineV111
                .QueryNativeMapGaussianInt(
                    gaussianIntRequest.SiteId,
                    gaussianIntRequest.StreamId,
                    gaussianIntRequest.OwnerId,
                    gaussianIntRequest.Ordinal);
            if (law.LawId != expectedLaw.LawId ||
                !outcome.OutcomeId.StartsWith(
                    "integer:", StringComparison.Ordinal) ||
                !int.TryParse(outcome.OutcomeId[8..],
                    NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var selectedInteger) ||
                selectedInteger < gaussianIntRequest.MinInclusive ||
                selectedInteger > gaussianIntRequest.MaxInclusive)
                throw new InvalidDataException(
                    "Gaussian-integer Chance Law differs from its typed map request.");
            var gaussianDecision = new RandomGaussianIntDecisionV111(
                gaussianIntRequest with { }, selectedInteger,
                "controlled-v111");
            gaussianDecision.Validate();
            var controlledGaussian = ControlledStochasticDecisionV111
                .FromGaussianInt(gaussianDecision);
            controlledGaussian.Validate();
            return controlledGaussian;
        }

        if (candidateLabels is not null ||
            law.OperationKind is not ("unit_float_threshold" or
                "unit_float_inclusive_threshold" or
                "unit_float_weighted_ticket" or
                "unit_float_mapped_price" or "unit_float_mapped_gold" or
                "unit_float_no_effect"))
            throw new NotSupportedException(
                "Chance Law is not a supported unit-float outcome mapping.");

        var floatRequest = request.UnitFloatRequest ??
            throw new NotSupportedException(
                "Chance Law outcome mapping does not yet support this typed provider primitive.");
        var selectedValue = ResolveUnitFloatOutcome(
            law, floatRequest, outcome.OutcomeId, conditioningRun);
        var floatDecision = new RandomUnitFloatDecisionV111(
            floatRequest with { }, selectedValue, "controlled-v111");
        floatDecision.Validate();
        var result = ControlledStochasticDecisionV111.FromUnitFloat(
            floatDecision);
        result.Validate();
        return result;
    }

    private static ControlledStochasticDecisionV111
        FromAncientOfferSelection(
            ChanceLawV111 law,
            RandomIndexRequestV111 indexRequest,
            ReferenceNatureSelectionV111 selection,
            ChanceLawOutcomeV111 outcome,
            RunState? conditioningRun,
            string[]? candidateLabels)
    {
        if (conditioningRun is null ||
            indexRequest.SiteId is not (
                NativeOrobasEventV111.OfferSiteId or
                NativePaelRewardsV111.OfferSiteId or
                NativeAncientRewardsV111.TezcataraOfferSiteId))
            throw new NotSupportedException(
                "Ancient event-offer mapping requires a conditioned direct-offer boundary.");

        var siteId = indexRequest.SiteId;
        var ordinal = indexRequest.Ordinal;
        var candidates = ReferenceNatureAncientOfferSessionV111
            .CandidateChoiceIds(conditioningRun, siteId, ordinal);
        var expectedLaw = ChanceLawEngineV111.QueryAncientOffer(
            conditioningRun, siteId, ordinal);
        var expectedRequest = new RandomIndexRequestV111(
            siteId,
            ReferenceNatureAncientOfferSessionV111.StreamId(siteId),
            ReferenceNatureAncientOfferSessionV111.OwnerId(siteId),
            ordinal,
            Enumerable.Range(0, candidates.Length).ToArray());
        if (law.OperationKind != "event_offer_index" ||
            indexRequest.OperationKind != "choose_index" ||
            CanonicalJson.Sha256(law) !=
                CanonicalJson.Sha256(expectedLaw) ||
            CanonicalJson.Sha256(indexRequest) !=
                CanonicalJson.Sha256(expectedRequest) ||
            candidates.Length == 0)
            throw new InvalidDataException(
                "Ancient event-offer law differs from its ordered source candidates.");

        var semanticLabels = candidates.Select(choiceId =>
            "event_choice:" + choiceId).ToArray();
        if (candidateLabels is { Length: > 0 } labels &&
            (labels.Length != candidates.Length ||
             labels.Where((label, index) =>
                 label != candidates[index] &&
                 label != semanticLabels[index]).Any()))
            throw new InvalidDataException(
                "Ancient event-offer candidate labels differ from its source choice identities.");

        const string outcomePrefix = "event_choice:";
        if (!outcome.OutcomeId.StartsWith(
                outcomePrefix, StringComparison.Ordinal))
            throw new InvalidDataException(
                "Ancient event-offer law returned a non-choice outcome.");
        var choiceId = outcome.OutcomeId[outcomePrefix.Length..];
        var selectedIndex = Array.IndexOf(candidates, choiceId);
        if (selectedIndex < 0 ||
            outcome.OutcomeId != semanticLabels[selectedIndex] ||
            selection.OutcomeId != outcome.OutcomeId)
            throw new InvalidDataException(
                "Selected Ancient event-offer identity has no source candidate representative.");

        var decision = new RandomIndexDecisionV111(
            indexRequest with
            {
                OrderedCandidates = (int[])
                    indexRequest.OrderedCandidates.Clone()
            }, selectedIndex, "controlled-v111");
        decision.Validate();
        var controlled = ControlledStochasticDecisionV111.FromIndex(
            decision, semanticLabels);
        controlled.Validate();
        return controlled;
    }

    private static ChanceLawOutcomeV111 ValidateSelection(
        ChanceLawV111 law,
        ControlledStochasticRequestV111 request,
        ReferenceNatureSelectionV111 selection)
    {
        ArgumentNullException.ThrowIfNull(law);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(selection);
        law.Validate();
        request.Validate();
        if (selection.LawId != law.LawId ||
            selection.EventId != law.EventId ||
            selection.OutcomeIndex < 0 ||
            selection.OutcomeIndex >= law.SupportCount ||
            selection.OutcomeId != law.OutcomeIdAt(
                selection.OutcomeIndex) ||
            law.SiteId != request.SiteId ||
            law.OwnerId != request.OwnerId ||
            law.Ordinal != request.Ordinal)
            throw new InvalidDataException(
                "ReferenceNature selection does not match the typed provider request.");
        return new ChanceLawOutcomeV111(
            law.OutcomeIdAt(selection.OutcomeIndex),
            law.OutcomeWeightAt(selection.OutcomeIndex));
    }

    private static float ResolveUnitFloatOutcome(
        ChanceLawV111 law, RandomUnitFloatRequestV111 request,
        string outcomeId, RunState? conditioningRun)
    {
        if (law.OperationKind == "unit_float_no_effect" &&
            request.SiteId == "shop.merchant.card_upgrade_roll.unit_float" &&
            outcomeId == "no_effect")
            return 0.5f;

        if (law.OperationKind == "unit_float_threshold")
        {
            if (request.SiteId == "card_rarity.roll.unit_float")
            {
                if (conditioningRun?.World is not { } world)
                    throw new InvalidOperationException(
                        "Card rarity mapping requires its conditioned run.");
                if (!law.OwnerId.StartsWith("type:", StringComparison.Ordinal))
                {
                    var parts = law.OwnerId.Split(':');
                    if (parts.Length != 8 || parts[0] != "act" ||
                        !int.TryParse(parts[1], out var rewardAct) ||
                        parts[2] != "room" || parts[4] != "pick" ||
                        !int.TryParse(parts[5], out _) ||
                        parts[6] != "offset" ||
                        !int.TryParse(parts[7], out var rewardOffset) ||
                        rewardAct != world.Act)
                        throw new InvalidDataException(
                            "Native reward card-rarity owner is malformed.");
                    var roomText = parts[3];
                    var source = roomText switch
                    {
                        "Combat" => PrototypeNativeCardOddsSource.RegularEncounter,
                        "Elite" => PrototypeNativeCardOddsSource.EliteEncounter,
                        "Boss" => PrototypeNativeCardOddsSource.BossEncounter,
                        _ => throw new InvalidDataException(
                            "Native reward card-rarity owner has an invalid room.")
                    };
                    var expected = ChanceLawEngineV111.QueryNativeCardRarity(
                        source, rewardOffset, conditioningRun.Ascension,
                        law.OwnerId, request.Ordinal);
                    if (CanonicalJson.Sha256(law) !=
                        CanonicalJson.Sha256(expected))
                        throw new InvalidDataException(
                            "Native reward card-rarity law differs from its owner.");
                    return PrototypeNativeCardRarityOdds
                        .NativeRarityRepresentative(outcomeId, rewardOffset,
                            conditioningRun.Ascension, source);
                }
                var offsetMarker = ":offset:";
                var offsetStart = law.OwnerId.LastIndexOf(
                    offsetMarker, StringComparison.Ordinal);
                if (offsetStart < 0 || !int.TryParse(
                        law.OwnerId[(offsetStart + offsetMarker.Length)..],
                        NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out var merchantOffset) ||
                    merchantOffset != world.CardRarityOffsetBasisPoints)
                    throw new InvalidDataException(
                        "Card-rarity Chance Law and conditioning run differ.");
                return PrototypeNativeCardRarityOdds
                    .MerchantRarityRepresentative(
                        outcomeId, merchantOffset, conditioningRun.Ascension);
            }

            if (request.SiteId == "reward.potion_odds.unit_float")
            {
                if (conditioningRun is null ||
                    conditioningRun.Phase != RunPhase.Combat ||
                    conditioningRun.World is not { ActiveRoom: { } room } world)
                    throw new InvalidOperationException(
                        "Combat potion odds mapping requires its pre-victory run.");
                var forced = conditioningRun.Player.ActiveRelics.Any(relic =>
                    PrototypeContent.Relic(relic.RelicId)
                        .ForceCombatPotionReward);
                if (forced || law.OwnerId != "run:potion_reward_odds")
                    throw new InvalidDataException(
                        "Combat potion odds law differs from its source hook.");
                var expectedLaw = ChanceLawEngineV111.QueryNativePotionRewardOdds(
                    world.PotionRewardOddsThousandths,
                    room == PrototypeRoomType.Elite, forced,
                    request.Ordinal);
                if (CanonicalJson.Sha256(law) !=
                    CanonicalJson.Sha256(expectedLaw))
                    throw new InvalidDataException(
                        "Combat potion odds Chance Law differs from its run.");
                var selectedOffered = outcomeId switch
                {
                    "potion_reward:offered" => 1,
                    "potion_reward:not_offered" => 0,
                    _ => throw new InvalidDataException(
                        "Unknown combat potion reward outcome.")
                };
                var threshold = (world.PotionRewardOddsThousandths +
                    (room == PrototypeRoomType.Elite
                        ? PrototypeNativePotionRewardOdds.EliteBonus : 0)) /
                    1000f;
                return FindUnitFloatRepresentative(
                    roll => roll < threshold ? 1 : 0, selectedOffered);
            }

            if (request.SiteId == "potion_factory.rarity_roll.unit_float")
            {
                var rarity = outcomeId switch
                {
                    "rarity:rare" => PrototypeNativePotionRarity.Rare,
                    "rarity:uncommon" => PrototypeNativePotionRarity.Uncommon,
                    "rarity:common" => PrototypeNativePotionRarity.Common,
                    _ => throw new InvalidDataException(
                        "Unknown native potion rarity outcome.")
                };
                var representative = rarity switch
                {
                    PrototypeNativePotionRarity.Rare => 0.05f,
                    PrototypeNativePotionRarity.Uncommon => 0.2f,
                    PrototypeNativePotionRarity.Common => 0.675f,
                    _ => throw new ArgumentOutOfRangeException()
                };
                if (PrototypeNativePotionShop.ResolveRarityNative(
                        representative) != rarity)
                    throw new InvalidOperationException(
                        "Potion rarity representative changed source outcome.");
                return representative;
            }

            if (request.SiteId == "relic_grab_bag.rarity.unit_float")
            {
                if (outcomeId == "no_relic")
                    return FindNativeTreasureNoRelicRepresentative(
                        conditioningRun);
                var rarity = outcomeId switch
                {
                    "rarity:common" => PrototypeRelicRarity.Common,
                    "rarity:uncommon" => PrototypeRelicRarity.Uncommon,
                    "rarity:rare" => PrototypeRelicRarity.Rare,
                    _ => throw new InvalidDataException(
                        "Unknown native relic rarity outcome.")
                };
                var representative = rarity switch
                {
                    PrototypeRelicRarity.Common => 0.25f,
                    PrototypeRelicRarity.Uncommon => 0.65f,
                    PrototypeRelicRarity.Rare => 0.915f,
                    _ => throw new ArgumentOutOfRangeException()
                };
                if (PrototypeNativeRelicGrabBag.ResolveRarityNative(
                        representative) != rarity)
                    throw new InvalidOperationException(
                        "Relic rarity representative changed source outcome.");
                return representative;
            }

            throw new NotSupportedException(
                "No semantic representative is defined for unit-float site '" +
                request.SiteId + "'.");
        }

        if (law.OperationKind == "unit_float_inclusive_threshold" &&
            request.SiteId == "card_rarity.upgrade_roll.unit_float")
        {
            var owner = request.OwnerId.Split(':');
            if (owner.Length < 6 || owner[0] != "card" ||
                !int.TryParse(owner[^3], out var act) ||
                owner[^4] != "act" || owner[^2] != "ascension" ||
                !int.TryParse(owner[^1], out var ascension))
                throw new InvalidDataException(
                    "Native card-upgrade request owner is malformed.");
            var cardId = string.Join(":", owner[1..^4]);
            var expectedLaw = ChanceLawEngineV111
                .QueryNativeEncounterCardUpgrade(
                    cardId, act, ascension, request.Ordinal);
            if (CanonicalJson.Sha256(law) !=
                CanonicalJson.Sha256(expectedLaw))
                throw new InvalidDataException(
                    "Native card-upgrade Chance Law differs from its request.");
            var expectedPass = outcomeId switch
            {
                "upgrade_check:passed" => 1,
                "upgrade_check:not_passed" => 0,
                _ => throw new InvalidDataException(
                    "Unknown Native card-upgrade outcome.")
            };
            var threshold = PrototypeNativeCardRarityOdds
                .EncounterCardUpgradeThresholdBasisPoints(
                    cardId, act, ascension) / 10000f;
            return FindUnitFloatRepresentative(
                roll => roll <= threshold ? 1 : 0, expectedPass);
        }

        if (law.OperationKind == "unit_float_weighted_ticket" &&
            request.SiteId == PrototypeNativeEndlessConveyor
                .DishWeightedTicketSiteId)
        {
            if (conditioningRun?.World is not { } world ||
                outcomeId is null || !outcomeId.StartsWith(
                    "dish:", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Endless Conveyor dish mapping requires its conditioned run and a dish outcome.");
            EventState eventState;
            if (conditioningRun.Phase == RunPhase.MapChoice &&
                world.Event is null)
                eventState = new EventState(
                    PrototypeNativeEndlessConveyor.EventId);
            else if (conditioningRun.Phase == RunPhase.Event &&
                world.Event is { EventId:
                    PrototypeNativeEndlessConveyor.EventId } activeEvent)
                eventState = activeEvent;
            else
                throw new InvalidOperationException(
                    "Endless Conveyor dish mapping requires its pre-entry or pre-roll state.");
            var candidates = PrototypeNativeEndlessConveyor
                .GetWeightedDishCandidates(conditioningRun, eventState);
            var totalWeight = candidates.Sum(candidate => candidate.Weight);
            var rollNumber = eventState.Stochastic.RandomOutcomes.NativeDishCount + 1;
            var gridWeights = ChanceLawEngineV111
                .NativeNextFloatWeightedTicketGridWeights(candidates
                    .Select(candidate => candidate.Weight).ToArray());
            var expectedOutcomes = candidates.Select((candidate, index) =>
                    new ChanceLawOutcomeV111(
                        "dish:" + candidate.Id, gridWeights[index]))
                .Where(candidate => candidate.Weight > 0).ToArray();
            if (totalWeight <= 0 || law.OwnerId != $"dish:{rollNumber}:pool:" +
                    CanonicalJson.Sha256(candidates) ||
                !law.Outcomes.SequenceEqual(expectedOutcomes))
                throw new InvalidDataException(
                    "Endless Conveyor dish law differs from its pre-entry run.");
            var selectedDish = outcomeId[5..];
            var selectedIndex = Array.FindIndex(candidates,
                candidate => candidate.Id == selectedDish);
            if (selectedIndex < 0 || gridWeights[selectedIndex] <= 0)
                throw new InvalidDataException(
                    "Endless Conveyor dish outcome has no positive Native grid mass.");

            int MapDish(float roll)
            {
                var ticket = roll * totalWeight;
                for (var index = 0; index < candidates.Length; index++)
                {
                    if (ticket < candidates[index].Weight)
                        return index;
                    ticket -= candidates[index].Weight;
                }
                return candidates.Length - 1;
            }

            return FindUnitFloatRepresentative(MapDish, selectedIndex);
        }

        if (law.OperationKind == "unit_float_mapped_price")
        {
            if (!outcomeId.StartsWith("price:", StringComparison.Ordinal) ||
                !int.TryParse(outcomeId[6..], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var price))
                throw new InvalidDataException(
                    "Mapped-price Chance Law has a malformed outcome.");
            return FindPriceRepresentative(request, price);
        }

        if (law.OperationKind == "unit_float_mapped_gold")
        {
            if (request.SiteId != ChanceLawEngineV111
                    .NativeEventGoldUnitFloatSiteId ||
                !outcomeId.StartsWith("gold:", StringComparison.Ordinal) ||
                !int.TryParse(outcomeId[5..], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var gold))
                throw new InvalidDataException(
                    "Mapped-gold Chance Law has a malformed outcome.");
            var (eventId, stage) = ParseNativeEventGoldOwner(request.OwnerId);
            return FindUnitFloatRepresentative(roll =>
                ChanceLawEngineV111.NativeEventGoldUnitFloatOutcome(
                    eventId, stage, roll), gold);
        }

        throw new NotSupportedException(
            "Unsupported unit-float Chance Law operation.");
    }

    private static (string EventId, string Stage) ParseNativeEventGoldOwner(
        string ownerId)
    {
        foreach (var eventId in new[]
        {
            PrototypeNativeHiveEvents.LostWispId,
            "proto.native.event.jungle_maze_adventure",
            PrototypeNativeDenseVegetation.EventId,
            "proto.native.event.whispering_hollow",
            "proto.native.event.sunken_statue",
            "proto.native.underdocks.sunken_statue",
            "proto.native.event.luminous_choir",
            "proto.native.underdocks.sunken_treasury",
            PrototypeNativePunchOff.EventId,
            PrototypeNativeSharedEvents.ThisOrThatId
        })
        {
            var prefix = $"event:{eventId}:gold:";
            if (!ownerId.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            var stage = ownerId[prefix.Length..];
            if (stage is "primary" or "secondary")
                return (eventId, stage);
        }
        throw new InvalidDataException(
            "Mapped-gold Chance Law has an unsupported event owner.");
    }

    private static float FindNativeTreasureNoRelicRepresentative(
        RunState? conditioningRun)
    {
        if (conditioningRun?.World is not { RelicBag: { } bag } ||
            conditioningRun.Phase != RunPhase.MapChoice)
            throw new InvalidOperationException(
                "Treasure no-relic mapping requires its pre-entry run and relic bag.");
        var candidates = new[]
        {
            (PrototypeRelicRarity.Common, 0.25f),
            (PrototypeRelicRarity.Uncommon, 0.65f),
            (PrototypeRelicRarity.Rare, 0.915f)
        };
        foreach (var (rarity, representative) in candidates)
        {
            if (PrototypeNativeRelicGrabBag.PlanPull(
                    bag, rarity, merchant: false, conditioningRun.Player)
                .CandidateIds.Length == 0)
                return representative;
        }
        throw new InvalidDataException(
            "Treasure no-relic outcome has no empty rarity branch.");
    }

    private static float FindPriceRepresentative(
        RandomUnitFloatRequestV111 request, int expectedPrice)
    {
        int Map(float roll)
        {
            if (request.SiteId ==
                "shop.merchant.card_price_modifier.unit_float")
            {
                var cardId = request.OwnerId.Contains(":sale_reprice:",
                    StringComparison.Ordinal)
                    ? SuffixAfter(request.OwnerId, ":sale_reprice:")
                    : SuffixAfter(request.OwnerId, ":card:");
                var price = PrototypeGameEngine
                    .ResolveNativeMerchantCardPrice(cardId, roll);
                return request.OwnerId.Contains(":sale_reprice:",
                    StringComparison.Ordinal) ? price / 2 : price;
            }
            if (request.SiteId ==
                "shop.merchant.potion_price_modifier.unit_float")
                return PrototypeNativePotionShop.ResolveMerchantPriceNative(
                    SuffixAfter(request.OwnerId, ":potion:").Split(':')[0],
                    roll);
            if (request.SiteId ==
                "shop.merchant.relic_price_modifier.unit_float")
                return PrototypeNativeRelicGrabBag.ResolveMerchantPriceNative(
                    SuffixAfter(request.OwnerId, ":relic:").Split(':')[0],
                    roll);
            throw new NotSupportedException(
                "No mapped-price source function is defined for '" +
                request.SiteId + "'.");
        }

        uint low = 0;
        uint high = OneFloatBits;
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            if (Map(FloatFromBits(middle)) >= expectedPrice)
                high = middle;
            else
                low = middle + 1;
        }
        var first = low;
        if (Map(FloatFromBits(first)) != expectedPrice)
            throw new InvalidOperationException(
                "Chance Law selected a price that no source float can produce.");

        low = first;
        high = OneFloatBits;
        while (low < high)
        {
            var middle = low + ((high - low + 1) >> 1);
            if (Map(FloatFromBits(middle)) <= expectedPrice)
                low = middle;
            else
                high = middle - 1;
        }
        var last = low;
        if (Map(FloatFromBits(last)) != expectedPrice)
            throw new InvalidOperationException(
                "Chance Law price interval has no source-float representative.");
        var representative = FloatFromBits(first + ((last - first) >> 1));
        if (Map(representative) != expectedPrice)
            throw new InvalidOperationException(
                "Selected source-float representative changed the mapped price.");
        return representative;
    }

    private static float FindUnitFloatRepresentative(
        Func<float, int> map, int expectedOutcome)
    {
        uint low = 0;
        uint high = OneFloatBits + 1;
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            if (middle <= OneFloatBits &&
                map(FloatFromBits(middle)) >= expectedOutcome)
                high = middle;
            else
                low = middle + 1;
        }
        if (low > OneFloatBits ||
            map(FloatFromBits(low)) != expectedOutcome)
            throw new InvalidDataException(
                "Weighted-ticket outcome has no source-float representative.");
        var first = low;

        low = first;
        high = OneFloatBits + 1;
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            if (middle <= OneFloatBits &&
                map(FloatFromBits(middle)) <= expectedOutcome)
                low = middle + 1;
            else
                high = middle;
        }
        var last = low - 1;
        var representative = FloatFromBits(
            first + ((last - first) >> 1));
        if (map(representative) != expectedOutcome)
            throw new InvalidDataException(
                "Weighted-ticket representative changed its source outcome.");
        return representative;
    }

    private static string SuffixAfter(string value, string marker)
    {
        var index = value.LastIndexOf(marker, StringComparison.Ordinal);
        if (index < 0 || index + marker.Length >= value.Length)
            throw new InvalidDataException(
                "Chance Law owner does not identify its mapped-price item.");
        return value[(index + marker.Length)..];
    }

    private static bool OutcomeEndsInIndex(string outcomeId, int index)
    {
        var markerIndex = outcomeId.LastIndexOf(':');
        return markerIndex >= 0 && int.TryParse(
            outcomeId[(markerIndex + 1)..], NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var parsed) && parsed == index;
    }

    private static float FloatFromBits(uint bits) =>
        BitConverter.Int32BitsToSingle(unchecked((int)bits));
}
