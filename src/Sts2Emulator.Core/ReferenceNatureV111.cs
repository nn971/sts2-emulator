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

        if (combat.EnemyActionEventPending is { } enemyActionEvent)
        {
            var law = ChanceLawEngineV111.QueryEndTurnIndex(
                enemyActionEvent);
            var selection = SampleValidated(law, state);
            if (selection.OutcomeId != "choice_index:" +
                selection.OutcomeIndex.ToString(
                    CultureInfo.InvariantCulture))
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending enemy-action index event.");
            var child = SemanticPilotCombatEngineV111
                .ResolveChanceOutcomeByIndex(
                    combat, selection.EventId, selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.TargetPending is { } targetEvent)
        {
            var law = ChanceLawEngineV111.QueryTarget(targetEvent);
            var selection = SampleValidated(law, state);
            var child = SemanticPilotCombatEngineV111
                .ResolveChanceOutcomeByIndex(
                    combat, selection.EventId, selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.GeneratedCardPending is { } generatedCardEvent)
        {
            var law = ChanceLawEngineV111.QueryGeneratedCard(
                generatedCardEvent);
            var selection = SampleValidated(law, state);
            var child = SemanticPilotCombatEngineV111
                .ResolveChanceOutcomeByIndex(
                    combat, selection.EventId, selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.CostPending is { } costEvent)
        {
            var law = ChanceLawEngineV111.QueryCost(costEvent);
            var selection = SampleValidated(law, state);
            var child = SemanticPilotCombatEngineV111
                .ResolveChanceOutcomeByIndex(
                    combat, selection.EventId, selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.RandomPileCardPending is { } randomPileCardEvent)
        {
            var law = ChanceLawEngineV111.QueryRandomPileCard(
                randomPileCardEvent);
            var selection = SampleValidated(law, state);
            var child = SemanticPilotCombatEngineV111
                .ResolveChanceOutcomeByIndex(
                    combat, selection.EventId, selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.CardTransformPending is { } transformEvent)
        {
            var law = ChanceLawEngineV111.QueryCardTransform(transformEvent);
            var selection = SampleValidated(law, state);
            var child = SemanticPilotCombatEngineV111
                .ResolveChanceOutcomeByIndex(
                    combat, selection.EventId, selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.PotionGenerationPending is { } potionEvent)
        {
            var law = ChanceLawEngineV111.QueryPotionGeneration(potionEvent);
            var selection = SampleValidated(law, state);
            var child = SemanticPilotCombatEngineV111
                .ResolveChanceOutcomeByIndex(
                    combat, selection.EventId, selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.HandCostSelectionPending is { } handCostEvent)
        {
            var law = ChanceLawEngineV111.QueryHandCostSelection(
                handCostEvent);
            var selection = SampleValidated(law, state);
            var child = SemanticPilotCombatEngineV111
                .ResolveChanceOutcomeByIndex(
                    combat, selection.EventId, selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.PoolShufflePending is { } poolShuffleEvent)
        {
            var law = ChanceLawEngineV111.QueryPoolShuffle(poolShuffleEvent);
            var selection = SampleValidated(law, state);
            var child = SemanticPilotCombatEngineV111
                .ResolveChanceOutcomeByIndex(
                    combat, selection.EventId, selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.DrawInsertionPending is { } insertionEvent)
        {
            var law = ChanceLawEngineV111.QueryDrawInsertion(insertionEvent);
            var selection = SampleValidated(law, state);
            var child = SemanticPilotCombatEngineV111
                .ResolveChanceOutcomeByIndex(
                    combat, selection.EventId, selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.CardShortlistPending is { } shortlistEvent)
        {
            var law = ChanceLawEngineV111.QueryCardShortlist(shortlistEvent);
            var selection = SampleValidated(law, state);
            var child = SemanticPilotCombatEngineV111
                .ResolveChanceOutcomeByIndex(
                    combat, selection.EventId, selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.DrawPending is { } drawEvent)
        {
            var deck = combat.Run.World?.Combat?.Stochastic.SemanticDeck ??
                throw new InvalidDataException(
                    "Pending semantic draw lost its provider-owned deck.");
            var law = ChanceLawEngineV111.QueryDraw(deck);
            var selection = SampleValidated(law, state);
            if (selection.OutcomeIndex >=
                    drawEvent.CandidateCardInstanceIds.Length ||
                selection.OutcomeId != "card_instance:" +
                    drawEvent.CandidateCardInstanceIds[
                        selection.OutcomeIndex].ToString(
                            CultureInfo.InvariantCulture))
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending draw event.");
            var child = SemanticPilotCombatEngineV111
                .ResolveChanceOutcomeByIndex(
                    combat, selection.EventId, selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.DeckRevealPending is { } revealEvent)
        {
            var deck = combat.Run.World?.Combat?.Stochastic.SemanticDeck ??
                throw new InvalidDataException(
                    "Pending semantic reveal lost its provider-owned deck.");
            var law = ChanceLawEngineV111.QueryDeckReveal(deck);
            var selection = SampleValidated(law, state);
            if (selection.OutcomeIndex >=
                    revealEvent.CandidateCardInstanceIds.Length ||
                selection.OutcomeId != "card_instance:" +
                    revealEvent.CandidateCardInstanceIds[
                        selection.OutcomeIndex].ToString(
                            CultureInfo.InvariantCulture))
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending deck reveal.");
            var child = SemanticPilotCombatEngineV111
                .ResolveChanceOutcomeByIndex(
                    combat, selection.EventId, selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        throw new NotSupportedException(
            "UNSUPPORTED_REFERENCE_NATURE site=no_supported_pending_event");
    }

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
