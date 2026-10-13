using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Trace;

public sealed record ReplayDivergence(
    long DecisionIndex, string Dimension, string Path, string Expected, string Actual);
public sealed record ExactReplayResult(bool Matched, int TransitionsChecked, ReplayDivergence? Divergence);

/// <summary>
/// Replays already-normalized canonical transitions and reports the first
/// decision/state/RNG/legal-action difference. Native probe normalization is
/// a separate adapter; synthetic engine traces are not native evidence.
/// </summary>
public static class ExactReplay
{
    public static ExactReplayResult Run(TraceHeader header, IEnumerable<TraceTransition> transitions,
        IDeterministicEngine engine)
    {
        if (header.Type != "header" || header.TraceSchema != "0.1")
            throw new NotSupportedException("Exact replay requires canonical trace schema 0.1.");
        RunState? current = null;
        var checkedCount = 0;
        foreach (var transition in transitions)
        {
            var before = transition.Before.State;
            var after = transition.After.State;
            RunConfiguration.ValidateState(before);
            RunConfiguration.ValidateState(after);
            if (transition.Type != "transition"
                || before.GameBuild != header.GameBuild || after.GameBuild != header.GameBuild
                || before.RunId != header.RunId || after.RunId != header.RunId
                || before.RunSeed != header.RunSeed || after.RunSeed != header.RunSeed
                || transition.DecisionIndex != before.DecisionIndex + 1
                || after.DecisionIndex != transition.DecisionIndex || transition.Phase != before.Phase)
                throw new InvalidDataException("Trace transition does not match its header, phase or decision index.");
            if (before.Configuration is not null && (transition.LegalActionsBefore is null
                || transition.LegalActionsAfter is null))
                throw new InvalidDataException("v111 traces must record legal actions on both decision boundaries.");
            if (transition.Before.Hash != CanonicalJson.Sha256(before)
                || transition.After.Hash != CanonicalJson.Sha256(after))
                throw new InvalidDataException("Trace canonical state hash is corrupt.");
            var difference = Compare(transition.DecisionIndex, "trace-rng-before", before.Rng, transition.RngBefore)
                ?? Compare(transition.DecisionIndex, "trace-rng-after", after.Rng, transition.RngAfter);
            if (difference is not null) return new(false, checkedCount, difference);
            if (current is not null)
            {
                difference = Compare(transition.DecisionIndex, "before-state", before, current);
                if (difference is not null) return new(false, checkedCount, difference);
            }
            else current = before.Fork();

            var legal = engine.GetLegalActions(current);
            if (transition.LegalActionsBefore is not null)
            {
                difference = CompareActions(transition.DecisionIndex, "legal-actions-before", transition.LegalActionsBefore, legal);
                if (difference is not null) return new(false, checkedCount, difference);
            }
            if (!legal.Any(action => CanonicalJson.Serialize(action) == CanonicalJson.Serialize(transition.Action)))
                return new(false, checkedCount, new(transition.DecisionIndex, "action", "$",
                    CanonicalJson.Serialize(transition.Action), "illegal action"));
            var next = engine.Step(current, transition.Action).State;
            difference = Compare(transition.DecisionIndex, "after-rng", after.Rng, next.Rng)
                ?? Compare(transition.DecisionIndex, "after-state", after, next);
            if (difference is not null) return new(false, checkedCount, difference);
            if (transition.LegalActionsAfter is not null)
            {
                difference = CompareActions(transition.DecisionIndex, "legal-actions-after", transition.LegalActionsAfter,
                    engine.GetLegalActions(next));
                if (difference is not null) return new(false, checkedCount, difference);
            }
            current = next;
            checkedCount++;
        }
        if (checkedCount == 0) throw new InvalidDataException("A replay trace must contain at least one transition.");
        return new(true, checkedCount, null);
    }

    public static ExactReplayResult ReadAndRun(string path, IDeterministicEngine engine)
    {
        using var reader = File.OpenText(path);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var line = reader.ReadLine() ?? throw new InvalidDataException("Trace is empty.");
        var header = JsonSerializer.Deserialize<TraceHeader>(line, options)
            ?? throw new InvalidDataException("Trace header is invalid.");
        IEnumerable<TraceTransition> ReadTransitions()
        {
            string? record;
            while ((record = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(record)) continue;
                yield return JsonSerializer.Deserialize<TraceTransition>(record, options)
                    ?? throw new InvalidDataException("Trace transition is invalid.");
            }
        }
        return Run(header, ReadTransitions(), engine);
    }

    private static ReplayDivergence? CompareActions(long index, string dimension,
        IEnumerable<GameAction> expected, IEnumerable<GameAction> actual) =>
        Compare(index, dimension,
            expected.Select(CanonicalJson.Serialize).Order(StringComparer.Ordinal).ToArray(),
            actual.Select(CanonicalJson.Serialize).Order(StringComparer.Ordinal).ToArray());

    private static ReplayDivergence? Compare<T>(long index, string dimension, T expected, T actual)
    {
        using var left = JsonDocument.Parse(CanonicalJson.Serialize(expected));
        using var right = JsonDocument.Parse(CanonicalJson.Serialize(actual));
        return FirstDifference(left.RootElement, right.RootElement, "$", index, dimension);
    }

    private static ReplayDivergence? FirstDifference(JsonElement left, JsonElement right,
        string path, long index, string dimension)
    {
        ReplayDivergence Difference() => new(index, dimension, path, left.GetRawText(), right.GetRawText());
        if (left.ValueKind != right.ValueKind) return Difference();
        if (left.ValueKind == JsonValueKind.Object)
        {
            var keys = left.EnumerateObject().Select(property => property.Name)
                .Union(right.EnumerateObject().Select(property => property.Name))
                .Order(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                var hasLeft = left.TryGetProperty(key, out var a);
                var hasRight = right.TryGetProperty(key, out var b);
                if (!hasLeft || !hasRight) return new(index, dimension, path + "." + key,
                    hasLeft ? a.GetRawText() : "<missing>", hasRight ? b.GetRawText() : "<missing>");
                var child = FirstDifference(a, b, path + "." + key, index, dimension);
                if (child is not null) return child;
            }
            return null;
        }
        if (left.ValueKind == JsonValueKind.Array)
        {
            if (left.GetArrayLength() != right.GetArrayLength()) return Difference();
            for (var childIndex = 0; childIndex < left.GetArrayLength(); childIndex++)
            {
                var child = FirstDifference(left[childIndex], right[childIndex], $"{path}[{childIndex}]", index, dimension);
                if (child is not null) return child;
            }
            return null;
        }
        return left.GetRawText() == right.GetRawText() ? null : Difference();
    }
}

public static class V111ReferenceTraceIdentity
{
    public static void Validate(ReferenceTraceHeaderV02 header)
    {
        if (header.TraceSchema != "0.2" || header.Type != "header"
            || header.BuildFingerprint != V111Build.Fingerprint
            || CanonicalJson.Sha256(header.Build) != header.BuildFingerprint)
            throw new InvalidDataException("Reference trace build identity does not match pinned v111.");
    }
}
