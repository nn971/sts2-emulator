using System.Globalization;

namespace Sts2Emulator.Core;

/// <summary>
/// Maps one sampled semantic Chance Law outcome back to the typed source
/// provider value consumed by ordinary mechanics. This covers native
/// merchant index requests and the merchant rarity/price float laws only.
/// </summary>
public static class ReferenceNatureChanceLawDecisionV111
{
    private const uint OneFloatBits = 0x3f800000;

    public static ControlledStochasticDecisionV111 FromSelection(
        ChanceLawV111 law,
        ControlledStochasticRequestV111 request,
        ReferenceNatureSelectionV111 selection,
        RunState? conditioningRun = null,
        string[]? candidateLabels = null)
    {
        ArgumentNullException.ThrowIfNull(law);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(selection);
        law.Validate();
        request.Validate();
        if (selection.LawId != law.LawId ||
            selection.EventId != law.EventId ||
            selection.OutcomeIndex < 0 ||
            selection.OutcomeIndex >= law.Outcomes.Length ||
            selection.OutcomeId !=
                law.Outcomes[selection.OutcomeIndex].OutcomeId ||
            law.SiteId != request.SiteId || law.OwnerId != request.OwnerId)
            throw new InvalidDataException(
                "ReferenceNature selection does not match the typed provider request.");

        var outcome = law.Outcomes[selection.OutcomeIndex];
        if (request.IndexRequest is { } indexRequest)
        {
            if (law.OperationKind != indexRequest.OperationKind ||
                law.Outcomes.Length != indexRequest.SupportCount ||
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

        if (candidateLabels is not null ||
            law.OperationKind is not ("unit_float_threshold" or
                "unit_float_mapped_price"))
            throw new NotSupportedException(
                "Chance Law is not a supported unit-float outcome mapping.");

        var floatRequest = request.UnitFloatRequest!;
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

    private static float ResolveUnitFloatOutcome(
        ChanceLawV111 law, RandomUnitFloatRequestV111 request,
        string outcomeId, RunState? conditioningRun)
    {
        if (law.OperationKind == "unit_float_threshold")
        {
            if (request.SiteId == "card_rarity.roll.unit_float")
            {
                if (conditioningRun?.World is not { } world ||
                    !law.OwnerId.StartsWith("type:", StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Merchant card rarity mapping requires its conditioned pre-entry run.");
                var offsetMarker = ":offset:";
                var offsetStart = law.OwnerId.LastIndexOf(
                    offsetMarker, StringComparison.Ordinal);
                if (offsetStart < 0 || !int.TryParse(
                        law.OwnerId[(offsetStart + offsetMarker.Length)..],
                        NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out var offset) ||
                    offset != world.CardRarityOffsetBasisPoints)
                    throw new InvalidDataException(
                        "Card-rarity Chance Law and conditioning run differ.");
                return PrototypeNativeCardRarityOdds
                    .MerchantRarityRepresentative(
                        outcomeId, offset, conditioningRun.Ascension);
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

        if (law.OperationKind == "unit_float_mapped_price")
        {
            if (!outcomeId.StartsWith("price:", StringComparison.Ordinal) ||
                !int.TryParse(outcomeId[6..], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var price))
                throw new InvalidDataException(
                    "Mapped-price Chance Law has a malformed outcome.");
            return FindPriceRepresentative(request, price);
        }

        throw new NotSupportedException(
            "Unsupported unit-float Chance Law operation.");
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
