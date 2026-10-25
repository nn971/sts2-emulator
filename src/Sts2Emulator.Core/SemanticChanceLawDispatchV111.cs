using System.Globalization;

namespace Sts2Emulator.Core;

/// <summary>
/// Maps a live typed semantic chance event to its read-only reference law.
/// Provider policies sample the checked law and pass the same descriptor to
/// the mechanics outcome dispatcher.
/// </summary>
internal static class SemanticChanceLawDispatchV111
{
    public static ChanceLawV111 QueryPendingIndexLaw(
        SemanticPilotCombatV111 combat,
        SemanticPendingIndexChanceEventV111 pending)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        pending.ValidateBoundary(combat.Fingerprint);

        var chanceEvent = pending.Event;
        var law = chanceEvent switch
        {
            SemanticEndTurnIndexEventV111 endTurn =>
                ChanceLawEngineV111.QueryEndTurnIndex(endTurn),
            SemanticTargetEventV111 target =>
                ChanceLawEngineV111.QueryTarget(target),
            SemanticGeneratedCardEventV111 generatedCard =>
                ChanceLawEngineV111.QueryGeneratedCard(generatedCard),
            SemanticCostEventV111 cost =>
                ChanceLawEngineV111.QueryCost(cost),
            SemanticRandomPileCardEventV111 randomPileCard =>
                ChanceLawEngineV111.QueryRandomPileCard(randomPileCard),
            SemanticCardTransformEventV111 transform =>
                ChanceLawEngineV111.QueryCardTransform(transform),
            SemanticPotionGenerationEventV111 potion =>
                ChanceLawEngineV111.QueryPotionGeneration(potion),
            SemanticHandCostSelectionEventV111 handCost =>
                ChanceLawEngineV111.QueryHandCostSelection(handCost),
            SemanticPoolShuffleEventV111 poolShuffle =>
                ChanceLawEngineV111.QueryPoolShuffle(poolShuffle),
            SemanticDrawInsertionEventV111 insertion =>
                ChanceLawEngineV111.QueryDrawInsertion(insertion),
            SemanticCardShortlistEventV111 shortlist =>
                ChanceLawEngineV111.QueryCardShortlist(shortlist),
            SemanticDeckEventV111 { Purpose: "draw" } =>
                ChanceLawEngineV111.QueryDraw(GetSemanticDeck(combat)),
            SemanticDeckEventV111 =>
                ChanceLawEngineV111.QueryDeckReveal(GetSemanticDeck(combat)),
            _ => throw new NotSupportedException(
                $"UNSUPPORTED_REFERENCE_NATURE site={chanceEvent.SiteId}")
        };

        law.Validate();
        if (law.EventId != chanceEvent.EventId ||
            law.SiteId != chanceEvent.SiteId ||
            law.OperationKind != chanceEvent.OperationKind ||
            law.OwnerId != chanceEvent.OwnerId ||
            law.Ordinal != chanceEvent.Ordinal ||
            law.SupportCount != pending.SupportCount)
            throw new InvalidDataException(
                $"ReferenceNature law no longer matches semantic event " +
                $"site={chanceEvent.SiteId} owner={chanceEvent.OwnerId} " +
                $"ordinal={chanceEvent.Ordinal}.");

        var expectedOutcomeIds = ExpectedOutcomeIds(chanceEvent);
        if (expectedOutcomeIds.Length != pending.SupportCount)
            throw new InvalidDataException(
                $"Semantic chance outcome mapping changed size " +
                $"site={chanceEvent.SiteId}.");
        for (var index = 0; index < expectedOutcomeIds.Length; index++)
        {
            if (!StringComparer.Ordinal.Equals(
                    law.OutcomeIdAt(index), expectedOutcomeIds[index]))
                throw new InvalidDataException(
                    $"ReferenceNature law outcome order no longer matches " +
                    $"semantic event site={chanceEvent.SiteId} " +
                    $"owner={chanceEvent.OwnerId} ordinal={chanceEvent.Ordinal} " +
                    $"index={index}.");
        }

        return law;
    }

    private static string[] ExpectedOutcomeIds(
        ISemanticChanceEventV111 chanceEvent) => chanceEvent switch
    {
        SemanticEndTurnIndexEventV111 endTurn => IndexOutcomeIds(
            "choice_index:", endTurn.Request.SupportCount),
        SemanticTargetEventV111 target =>
            target.CandidateEnemyInstanceIds.Select(instanceId =>
                "enemy_instance:" + instanceId.ToString(
                    CultureInfo.InvariantCulture)).ToArray(),
        SemanticGeneratedCardEventV111 generatedCard =>
            generatedCard.CandidateCardIds.Select(cardId =>
                "card_model:" + cardId).ToArray(),
        SemanticCostEventV111 cost =>
            cost.CandidateCosts.Select(value =>
                "cost:" + value.ToString(CultureInfo.InvariantCulture))
                .ToArray(),
        SemanticRandomPileCardEventV111 randomPileCard =>
            randomPileCard.CandidateCardInstanceIds
                .Select(CardInstanceOutcomeId).ToArray(),
        SemanticCardTransformEventV111 transform =>
            transform.CandidateCardIds.Select(cardId =>
                "card_model:" + cardId).ToArray(),
        SemanticPotionGenerationEventV111 potion =>
            potion.CandidatePotionIds.Select(potionId =>
                "potion_model:" + potionId).ToArray(),
        SemanticHandCostSelectionEventV111 handCost =>
            handCost.CandidateCardInstanceIds
                .Select(CardInstanceOutcomeId).ToArray(),
        SemanticPoolShuffleEventV111 poolShuffle => IndexOutcomeIds(
            "swap_index:", poolShuffle.Request.SupportCount),
        SemanticDrawInsertionEventV111 drawInsertion => IndexOutcomeIds(
            "draw_position:", drawInsertion.Request.SupportCount),
        SemanticCardShortlistEventV111 shortlist =>
            shortlist.CandidateCardInstanceIds
                .Select(CardInstanceOutcomeId).ToArray(),
        SemanticDeckEventV111 deck =>
            deck.CandidateCardInstanceIds
                .Select(CardInstanceOutcomeId).ToArray(),
        _ => throw new NotSupportedException(
            $"UNSUPPORTED_REFERENCE_NATURE site={chanceEvent.SiteId}")
    };

    private static string[] IndexOutcomeIds(string prefix, int supportCount) =>
        Enumerable.Range(0, supportCount).Select(index =>
            prefix + index.ToString(CultureInfo.InvariantCulture)).ToArray();

    private static string CardInstanceOutcomeId(long instanceId) =>
        "card_instance:" + instanceId.ToString(CultureInfo.InvariantCulture);

    private static SemanticDeckStateV111 GetSemanticDeck(
        SemanticPilotCombatV111 combat) =>
        combat.Run.World?.Combat?.Stochastic.SemanticDeck ??
        throw new InvalidDataException(
            "Pending semantic deck event lost its provider-owned deck.");
}
