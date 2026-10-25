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
            var intentLaw = ChanceLawEngineV111.QueryIntent(intentEvent);
            var intentSelection = SampleValidated(intentLaw, state);
            var intentChild = SemanticPilotCombatEngineV111.ResolveIntent(
                combat, combat.Fingerprint, intentSelection.OutcomeId);
            return new ReferenceNatureResolutionV111(
                intentChild, intentLaw, intentSelection);
        }

        var pending = SemanticPilotCombatEngineV111
            .DescribePendingIndexChanceEvent(combat) ??
            throw new NotSupportedException(
                "UNSUPPORTED_REFERENCE_NATURE site=no_supported_pending_event");
        var law = SemanticChanceLawDispatchV111
            .QueryPendingIndexLaw(combat, pending);
        var selection = SampleValidated(law, state);
        if (selection.OutcomeIndex >= pending.SupportCount)
            throw new InvalidDataException(
                "ReferenceNature outcome is outside the active semantic support.");

        var child = SemanticPilotCombatEngineV111
            .ResolveChanceOutcomeById(
                combat, pending, selection.OutcomeId);
        return new ReferenceNatureResolutionV111(child, law, selection);
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
                    checked(state.SampleCount + 1))
                {
                    Seed = state.Seed
                });
        }

        if (law.UniformIndexRange is { } range)
        {
            var selectedOffset = checked((int)NextBounded(
                ref randomState, (ulong)range.Count));
            return new ReferenceNatureSelectionV111(
                law.LawId, law.EventId, range.OutcomeIdAt(selectedOffset),
                selectedOffset, new ReferenceNatureStateV111(
                    ReferenceNatureStateV111.CurrentSchemaId, randomState,
                    checked(state.SampleCount + 1))
                {
                    Seed = state.Seed
                });
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
                        randomState, checked(state.SampleCount + 1))
                    {
                        Seed = state.Seed
                    });
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
