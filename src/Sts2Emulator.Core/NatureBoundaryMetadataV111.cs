namespace Sts2Emulator.Core;

/// <summary>Stable, Nature-facing choice metadata for the isolated v111 fixture.
/// These views never contain playable state or hidden RNG cursors.</summary>
public sealed record NatureBoundaryEventV111(
    string EventId, string SiteId, string StreamId, string OwnerId,
    int Ordinal, string OperationKind, string SourceVersion,
    int[] OrderedCandidates, int? BoundedCount, int SupportCount,
    string SupportFingerprint, string ProbabilityStatus = "UNVERIFIED",
    string? ReferenceLawId = null);

public sealed record NatureReplayChoiceV111(
    string EventId, string SiteId, string StreamId, string OwnerId,
    int Ordinal, string OperationKind, string SourceVersion,
    int[] OrderedCandidates, int? BoundedCount, int SelectedIndex,
    string ProbabilityStatus = "UNVERIFIED");

public sealed record NatureBoundaryViewV111(
    string SchemaId, string Kind, string Mode, bool Complete,
    string Fingerprint, NatureBoundaryEventV111? Pending,
    NatureReplayChoiceV111[] Transcript);

/// <summary>Deterministic event IDs are derived from the ordered mechanics
/// request and prior controlled outcomes, never from handle allocation.</summary>
public static class NatureBoundaryMetadataV111
{
    public const string SchemaId = "sts2-v111-nature-event-metadata-v1";

    public static NatureBoundaryViewV111 Describe(ControlledInitialHpStateV111 state)
    {
        var s = ControlledInitialHpV111.Restore(ControlledInitialHpV111.Save(state));
        return Build("initial_hp", s.Mode, s.Fingerprint, s.Pending, s.Transcript);
    }

    public static NatureBoundaryViewV111 Describe(ControlledIntentStateV111 state)
    {
        var s = ControlledIntentsV111.Restore(ControlledIntentsV111.Save(state));
        var history = (s.PriorHpTranscript ?? []).Concat(s.Transcript).ToArray();
        return Build("intents", s.Mode, s.Fingerprint, s.Pending, history);
    }

    public static NatureBoundaryViewV111 Describe(ControlledPlayableCombatV111 state)
    {
        ControlledPlayableCombatEngineV111.VerifyStage(state);
        var history = (state.Origin.PriorHpTranscript ?? [])
            .Concat(state.Origin.Transcript)
            .Concat(state.ShuffleTranscript).ToArray();
        return Build(state.Pending is null ? "player" : "opening_shuffle",
            state.Mode, state.Fingerprint, state.Pending, history);
    }

    private static NatureBoundaryViewV111 Build(
        string kind, string mode, string fingerprint,
        RandomIndexRequestV111? pending, RandomIndexDecisionV111[] history)
    {
        var transcript = new List<NatureReplayChoiceV111>();
        foreach (var decision in history)
        {
            decision.Validate();
            if (decision.ProviderMode != "controlled-v111" ||
                decision.NativeCursorBefore is not null ||
                decision.NativeCursorAfter is not null)
                throw new InvalidDataException("Nature transcript is not controlled.");
            var r = decision.Request;
            transcript.Add(new NatureReplayChoiceV111(
                EventId(r, transcript), r.SiteId, r.StreamId, r.OwnerId,
                r.Ordinal, r.OperationKind, r.SourceVersion,
                (int[])r.OrderedCandidates.Clone(), r.BoundedCount,
                decision.SelectedIndex));
        }
        NatureBoundaryEventV111? eventView = null;
        if (pending is not null)
        {
            pending.Validate();
            eventView = new NatureBoundaryEventV111(
                EventId(pending, transcript), pending.SiteId,
                pending.StreamId, pending.OwnerId, pending.Ordinal,
                pending.OperationKind, pending.SourceVersion,
                (int[])pending.OrderedCandidates.Clone(), pending.BoundedCount,
                pending.SupportCount, CanonicalJson.Sha256(pending));
        }
        return new NatureBoundaryViewV111(
            SchemaId, kind, mode, pending is null, fingerprint,
            eventView, transcript.ToArray());
    }

    private static string EventId(
        RandomIndexRequestV111 request,
        IEnumerable<NatureReplayChoiceV111> history) =>
        CanonicalJson.Sha256(new
        {
            schema = SchemaId,
            request,
            prefix = history.Select(x => new { x.EventId, x.SelectedIndex }).ToArray()
        });
}
