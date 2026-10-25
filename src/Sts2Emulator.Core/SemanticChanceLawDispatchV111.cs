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
            law.SupportCount != pending.SupportCount)
            throw new InvalidDataException(
                $"ReferenceNature law no longer matches semantic event " +
                $"site={chanceEvent.SiteId}.");
        return law;
    }

    private static SemanticDeckStateV111 GetSemanticDeck(
        SemanticPilotCombatV111 combat) =>
        combat.Run.World?.Combat?.Stochastic.SemanticDeck ??
        throw new InvalidDataException(
            "Pending semantic deck event lost its provider-owned deck.");
}
