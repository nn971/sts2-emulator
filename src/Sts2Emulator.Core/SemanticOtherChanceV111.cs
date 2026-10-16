namespace Sts2Emulator.Core;

/// <summary>
/// Engine-owned fallback for non-Nature drawn-card costs in the restricted
/// two-site pilot. Uses the pinned xoshiro/NextInt codec on a separate seeded
/// run.combat_energy_costs stream. This stream has NOT been synchronized with
/// the native game's pre-combat run history, so source parity is UNVERIFIED.
/// The complete cursor and decision transcript are saved privately.
/// </summary>
public sealed record SemanticOtherChanceV111(
    RngBundle Streams, RandomIndexDecisionV111[] Transcript,
    string Mode = "engine-owned-seeded-cost-v111-unverified-v1")
{
    public const string ProviderId = "engine-owned-seeded-cost-v111-unverified-v1";
    public const string CostStream = "run.combat_energy_costs";
    public SemanticOtherChanceV111 Fork() => this with
    {
        Streams = Streams.Fork(),
        Transcript = Transcript.Select(d => d with
        {
            Request = d.Request with
            {
                OrderedCandidates = (int[])d.Request.OrderedCandidates.Clone()
            }
        }).ToArray()
    };
}

public static class SemanticOtherChanceEngineV111
{
    public static SemanticOtherChanceV111 Begin(string seed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        return new SemanticOtherChanceV111(NativeRngV111.CreateBundle(seed), []);
    }

    public static (SemanticOtherChanceV111 State, int Index) ResolveCost(
        SemanticOtherChanceV111 current, RandomIndexRequestV111 request)
    {
        Validate(current);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        if (request.SiteId is not
            ("combat.card_draw.relic_energy_cost"
                or "combat.card_draw.slither_energy_cost") ||
            request.OperationKind != "bounded_integer" ||
            request.StreamId != "semantic.combat")
            throw new NotSupportedException(
                "UNSUPPORTED_OTHER_CHANCE site=" + request.SiteId +
                " operation=" + request.OperationKind);
        var next = current.Fork();
        var stream = next.Streams.Streams.Single(s =>
            s.StreamId == SemanticOtherChanceV111.CostStream);
        var before = NativeRngV111.ToSnapshot(stream);
        var index = NativeRngV111.NextInt(
            next.Streams, SemanticOtherChanceV111.CostStream,
            request.SupportCount);
        var after = NativeRngV111.ToSnapshot(next.Streams.Streams.Single(s =>
            s.StreamId == SemanticOtherChanceV111.CostStream));
        var decision = new RandomIndexDecisionV111(
            request with
            {
                OrderedCandidates = (int[])request.OrderedCandidates.Clone()
            },
            index, SemanticOtherChanceV111.ProviderId,
            before, after);
        decision.Validate();
        return (next with
        {
            Transcript = next.Transcript.Append(decision).ToArray()
        }, index);
    }

    public static void Validate(SemanticOtherChanceV111 state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Mode != SemanticOtherChanceV111.ProviderId ||
            state.Transcript is null)
            throw new InvalidDataException(
                "Invalid engine-owned fallback provider version.");
        var streams = state.Streams.Streams.Where(stream =>
            stream.StreamId == SemanticOtherChanceV111.CostStream).ToArray();
        if (streams.Length != 1)
            throw new InvalidDataException(
                "Engine-owned cost stream is missing or ambiguous.");
        _ = NativeRngV111.ToSnapshot(streams[0]);
        foreach (var decision in state.Transcript)
        {
            decision.Validate();
            if (decision.ProviderMode != state.Mode ||
                decision.ProbabilityStatus != "UNVERIFIED" ||
                decision.Request.SiteId is not
                    ("combat.card_draw.relic_energy_cost"
                        or "combat.card_draw.slither_energy_cost") ||
                decision.Request.StreamId != "semantic.combat" ||
                decision.NativeCursorBefore is null ||
                decision.NativeCursorAfter is null)
                throw new InvalidDataException(
                    "Malformed engine-owned cost provenance.");
        }
    }
}
