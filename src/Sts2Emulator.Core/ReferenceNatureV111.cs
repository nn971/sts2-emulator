using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// Small seeded reference policy that samples the declared law using an
/// independent SplitMix64 stream. It does not use or advance a game RNG.
/// </summary>
public static class ReferenceNatureV111
{
    public const string ProviderId = "reference-nature-splitmix64-v1";
    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

    /// <summary>
    /// Sample and resolve the next supported Nature site through the existing
    /// semantic mechanics continuation. The returned child and next sampler
    /// state are published together only after resolution succeeds.
    /// </summary>
    public static ReferenceNatureResolutionV111 ResolvePending(
        SemanticPilotCombatV111 combat, ReferenceNatureStateV111 state)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(state);
        combat.Validate();
        state.Validate();

        if (combat.IntentPending is not null)
        {
            var intentEvent = SemanticPilotCombatEngineV111
                .DescribePendingIntentEvent(combat);
            var law = ChanceLawEngineV111.QueryIntent(intentEvent);
            var selection = SampleValidated(law, state);
            var child = SemanticPilotCombatEngineV111.ResolveIntent(
                combat, combat.Fingerprint, selection.OutcomeId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        var pending = SemanticPilotCombatEngineV111
            .DescribePendingIndexChanceEvent(combat) ??
            throw new NotSupportedException(
                "UNSUPPORTED_REFERENCE_NATURE site=no_supported_pending_event");
        var law = QueryPendingLaw(combat, pending.Event);
        if (law.EventId != pending.Event.EventId ||
            law.SupportCount != pending.SupportCount)
            throw new InvalidDataException(
                "ReferenceNature law no longer matches the active semantic event.");
        var selection = SampleValidated(law, state);
        if (selection.OutcomeIndex >= pending.SupportCount)
            throw new InvalidDataException(
                "ReferenceNature outcome is outside the active semantic support.");

        if (pending.Event is SemanticEndTurnIndexEventV111 &&
            selection.OutcomeId != "choice_index:" +
                selection.OutcomeIndex.ToString(CultureInfo.InvariantCulture))
            throw new InvalidDataException(
                "ReferenceNature law no longer matches the pending enemy-action index event.");
        if (pending.Event is SemanticDeckEventV111 deckEvent &&
            selection.OutcomeId != "card_instance:" +
                deckEvent.CandidateCardInstanceIds[selection.OutcomeIndex]
                    .ToString(CultureInfo.InvariantCulture))
            throw new InvalidDataException(
                "ReferenceNature law no longer matches the pending deck event.");

        var child = SemanticPilotCombatEngineV111
            .ResolveChanceOutcomeByIndex(
                combat, selection.EventId, selection.OutcomeIndex);
        return new ReferenceNatureResolutionV111(child, law, selection);
    }

    private static ChanceLawV111 QueryPendingLaw(
        SemanticPilotCombatV111 combat, ISemanticChanceEventV111 pending) =>
        pending switch
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
                "UNSUPPORTED_REFERENCE_NATURE site=unhandled_pending_event")
        };

    private static SemanticDeckStateV111 GetSemanticDeck(
        SemanticPilotCombatV111 combat) =>
        combat.Run.World?.Combat?.Stochastic.SemanticDeck ??
        throw new InvalidDataException(
            "Pending semantic deck event lost its provider-owned deck.");

    public static ReferenceNatureSelectionV111 SampleValidated(
        ChanceLawV111 law, ReferenceNatureStateV111 state)
    {
        var selection = Sample(law, state);
        if (selection.LawId != law.LawId ||
            selection.EventId != law.EventId ||
            selection.OutcomeIndex < 0 ||
            selection.OutcomeIndex >= law.SupportCount ||
            selection.OutcomeId != law.OutcomeIdAt(
                selection.OutcomeIndex))
            throw new InvalidDataException(
                "ReferenceNature sample does not match the queried Chance Law support.");
        return selection;
    }

    public static ReferenceNatureSelectionV111 Sample(
        ChanceLawV111 law, ReferenceNatureStateV111 state)
    {
        ArgumentNullException.ThrowIfNull(law);
        ArgumentNullException.ThrowIfNull(state);
        law.Validate();
        state.Validate();

        ulong randomState = state.SamplerState;
        if (law.NativeBoundedIndexGrid is { } nativeGrid)
        {
            var gridIndex = checked((long)NextBounded(
                ref randomState, (ulong)nativeGrid.InputGridSize));
            var selectedIndex = ChanceLawEngineV111
                .NativeNextIntOutcomeFromGrid(
                    nativeGrid.BoundExclusive, gridIndex);
            return new ReferenceNatureSelectionV111(
                law.LawId, law.EventId,
                nativeGrid.OutcomeIdAt(selectedIndex), selectedIndex,
                new ReferenceNatureStateV111(
                    ReferenceNatureStateV111.CurrentSchemaId, randomState,
                    checked(state.SampleCount + 1)));
        }

        if (law.UniformIndexRange is { } range)
        {
            var selectedOffset = checked((int)NextBounded(
                ref randomState, (ulong)range.Count));
            return new ReferenceNatureSelectionV111(
                law.LawId, law.EventId, range.OutcomeIdAt(selectedOffset),
                selectedOffset, new ReferenceNatureStateV111(
                    ReferenceNatureStateV111.CurrentSchemaId, randomState,
                    checked(state.SampleCount + 1)));
        }

        ulong ticket = NextBounded(ref randomState, (ulong)law.TotalWeight);
        long cumulative = 0;
        for (var index = 0; index < law.Outcomes.Length; index++)
        {
            cumulative = checked(cumulative + law.Outcomes[index].Weight);
            if (ticket < (ulong)cumulative)
            {
                return new ReferenceNatureSelectionV111(
                    law.LawId, law.EventId, law.Outcomes[index].OutcomeId,
                    index, new ReferenceNatureStateV111(
                        ReferenceNatureStateV111.CurrentSchemaId,
                        randomState, checked(state.SampleCount + 1)));
            }
        }

        throw new InvalidDataException("ReferenceNature sampled outside law support.");
    }

    private static ulong NextBounded(ref ulong state, ulong bound)
    {
        if (bound == 0)
            throw new ArgumentOutOfRangeException(nameof(bound));
        var threshold = unchecked(0UL - bound) % bound;
        while (true)
        {
            var value = NextUInt64(ref state);
            if (value >= threshold)
                return value % bound;
        }
    }

    private static ulong NextUInt64(ref ulong state)
    {
        state = unchecked(state + GoldenGamma);
        ulong value = state;
        value = unchecked((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL);
        value = unchecked((value ^ (value >> 27)) * 0x94D049BB133111EBUL);
        return value ^ (value >> 31);
    }
}
