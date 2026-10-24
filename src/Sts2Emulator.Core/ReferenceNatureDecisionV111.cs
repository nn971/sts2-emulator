using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureDecisionV111(
    string LawId, string EventId, string OutcomeId, int OutcomeIndex)
{
    public void Validate()
    {
        var indexText = OutcomeIndex.ToString(CultureInfo.InvariantCulture);
        var indexedOutcome = OutcomeId == "choice_index:" + indexText ||
            OutcomeId == "swap_index:" + indexText ||
            OutcomeId == "path_column:" + indexText;
        var mapQuotaOutcome = OutcomeId.StartsWith(
                "rest_quota:", StringComparison.Ordinal) &&
            int.TryParse(OutcomeId["rest_quota:".Length..],
                NumberStyles.None, CultureInfo.InvariantCulture,
                out var restQuota) && restQuota is >= 0 and <= 20;
        var mapGaussianOutcome = OutcomeId.StartsWith(
                "integer:", StringComparison.Ordinal) &&
            int.TryParse(OutcomeId["integer:".Length..],
                NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var mapGaussian) && mapGaussian >= 0;
        var identifiedReward = OutcomeId is { } outcomeId &&
            (outcomeId.StartsWith("relic:", StringComparison.Ordinal) &&
                outcomeId.Length > "relic:".Length ||
             outcomeId.StartsWith("potion:", StringComparison.Ordinal) &&
                outcomeId.Length > "potion:".Length);
        var identifiedEncounter = OutcomeId is { } encounterId &&
            encounterId.StartsWith("encounter:", StringComparison.Ordinal) &&
            encounterId.Length > "encounter:".Length;
        var identifiedAncient = OutcomeId is { } ancientId &&
            ancientId.StartsWith("ancient_event:", StringComparison.Ordinal) &&
            ancientId.Length > "ancient_event:".Length;
        var darvAssignment = OutcomeId is "darv_assignment:none" or
            "darv_assignment:hive" or "darv_assignment:glory";
        var identifiedHpOffset = OutcomeId is { } hpOffsetId &&
            hpOffsetId.StartsWith("hp_offset:", StringComparison.Ordinal) &&
            int.TryParse(hpOffsetId["hp_offset:".Length..],
                NumberStyles.None, CultureInfo.InvariantCulture,
                out var hpOffset) && hpOffset is >= 2 and <= 9;
        var mappedGold = OutcomeId is { } goldId &&
            goldId.StartsWith("gold:", StringComparison.Ordinal) &&
            int.TryParse(goldId["gold:".Length..],
                NumberStyles.None, CultureInfo.InvariantCulture, out var gold) &&
            gold >= 0;
        var nativePotionRarity = OutcomeId is
            "rarity:common" or "rarity:uncommon" or "rarity:rare";
        var nativePotionModel = OutcomeId is { } potionModelId &&
            potionModelId.StartsWith("potion_model:", StringComparison.Ordinal) &&
            potionModelId.EndsWith(":index:" + indexText,
                StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(LawId) ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(OutcomeId) || OutcomeIndex < 0 ||
            !(indexedOutcome || mapQuotaOutcome || mapGaussianOutcome ||
                identifiedReward || identifiedEncounter ||
                identifiedAncient || darvAssignment ||
                identifiedHpOffset || mappedGold || nativePotionRarity ||
                nativePotionModel))
            throw new InvalidDataException(
                "Invalid ReferenceNature decision.");
    }
}

