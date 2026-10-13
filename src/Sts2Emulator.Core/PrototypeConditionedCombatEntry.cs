namespace Sts2Emulator.Core;

/// <summary>
/// Samples the *entire* room-entry combat RNG trajectory from an independent
/// synthetic pre-entry stream prior, accepting candidates whose public
/// observation and legal action menu match the supplied visible frame.
/// Unlike post-entry deck permutation injection, all calls to the combat
/// stream during room entry are replayed in order and the accepted cursor
/// is retained for subsequent turns.
///
/// This is exact rejection conditioning only for a *fixed source map state*
/// and the synthetic rekeyed combat stream prior. It is NOT a complete-run
/// posterior under the native or seeded prototype prior; the original
/// pre-entry stream may be correlated with earlier visible events.
/// </summary>
public static class PrototypeConditionedCombatEntry
{
    public const string SchemaId = "prototype-local-combat-stream-condition-v1";
    public const int MaxCandidateBudget = 16384;

    public sealed record Result(RunState State, int Candidates);

    public static Result Sample(
        RunState hypotheticalMapState,
        string actionId,
        string expectedObservationHash,
        IReadOnlyList<string> expectedLegalActionIds,
        string independentSearchSeed,
        int maxCandidates)
    {
        if (hypotheticalMapState.Phase != RunPhase.MapChoice
            || hypotheticalMapState.World is null)
        {
            throw new InvalidOperationException(
                "Combat entry conditioning requires a hypothetical map-choice state.");
        }
        if (string.IsNullOrWhiteSpace(expectedObservationHash)
            || expectedLegalActionIds is null
            || expectedLegalActionIds.Count == 0
            || expectedLegalActionIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Public observation hash and complete legal action menu are required.");
        }
        if (independentSearchSeed is null
            || independentSearchSeed.Length != 32
            || independentSearchSeed.Any(ch => !Uri.IsHexDigit(ch)))
        {
            throw new ArgumentException(
                "An independent search-side 128-bit hex seed is required.");
        }
        if (maxCandidates < 1 || maxCandidates > MaxCandidateBudget)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCandidates));
        }

        var environment = new PrototypeAiEnvironment();
        var matching = environment.Observe(hypotheticalMapState)
            .LegalActions.SingleOrDefault(action =>
                StringComparer.Ordinal.Equals(action.ActionId, actionId));
        if (matching is null || matching.Kind != "choose_map_node")
        {
            throw new InvalidOperationException(
                "Only a legal public map-node choice may be conditioned.");
        }

        for (var trial = 0; trial < maxCandidates; trial++)
        {
            var candidate = hypotheticalMapState.Fork();
            var combatStream = PrototypeRng.CreateBundle(
                $"local-combat-condition-v1:{independentSearchSeed.ToLowerInvariant()}:{trial}")
                .Streams.Single(stream =>
                    StringComparer.Ordinal.Equals(stream.StreamId, "combat"));
            var index = Array.FindIndex(
                candidate.Rng.Streams,
                stream => StringComparer.Ordinal.Equals(stream.StreamId, "combat"));
            if (index < 0)
            {
                throw new InvalidOperationException("Combat RNG stream is missing.");
            }
            candidate.Rng.Streams[index] = combatStream;

            var result = environment.Step(candidate, actionId).State;
            if (result.Phase != RunPhase.Combat)
            {
                throw new InvalidOperationException(
                    "Chosen map node did not enter combat; no conditioning performed.");
            }
            var frame = environment.Observe(result);
            if (StringComparer.Ordinal.Equals(frame.ObservationHash, expectedObservationHash)
                && frame.LegalActions.Select(action => action.ActionId)
                    .SequenceEqual(expectedLegalActionIds, StringComparer.Ordinal))
            {
                return new Result(result, trial + 1);
            }
        }

        throw new InvalidOperationException(
            $"Conditional local combat-stream rejection exhausted {maxCandidates} "
            + "independently rekeyed candidates. No state was returned.");
    }
}
