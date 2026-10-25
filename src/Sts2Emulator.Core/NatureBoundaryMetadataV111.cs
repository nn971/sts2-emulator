namespace Sts2Emulator.Core;

/// <summary>Stable, Nature-facing choice metadata for the isolated v111 fixture.
/// These views never contain playable state or hidden RNG cursors.</summary>
public sealed record NatureBoundaryEventV111(
    string EventId, string SiteId, string StreamId, string OwnerId,
    int Ordinal, string OperationKind, string SourceVersion,
    int[] OrderedCandidates, int? BoundedCount, int SupportCount,
    string SupportFingerprint, string ProbabilityStatus = "UNVERIFIED",
    string? ReferenceLawId = null,
    string[]? CandidateCardIds = null)
{
    private int[]? _orderedCandidates = Clone(OrderedCandidates);
    private string[]? _candidateCardIds = Clone(CandidateCardIds);
    private string[]? _candidateLabels;

    public int[] OrderedCandidates
    {
        get => Clone(_orderedCandidates)!;
        init => _orderedCandidates = Clone(value);
    }

    public string[]? CandidateCardIds
    {
        get => Clone(_candidateCardIds);
        init => _candidateCardIds = Clone(value);
    }

    public string[]? CandidateLabels
    {
        get => Clone(_candidateLabels);
        init => _candidateLabels = Clone(value);
    }

    public NatureBoundaryEventV111 Fork() => this with
    {
        OrderedCandidates = _orderedCandidates!,
        CandidateCardIds = _candidateCardIds,
        CandidateLabels = _candidateLabels
    };

    private static int[]? Clone(int[]? values) => values is null
        ? null
        : (int[])values.Clone();

    private static string[]? Clone(string[]? values) => values is null
        ? null
        : (string[])values.Clone();
}

public sealed record NatureReplayChoiceV111(
    string EventId, string SiteId, string StreamId, string OwnerId,
    int Ordinal, string OperationKind, string SourceVersion,
    int[] OrderedCandidates, int? BoundedCount, int SelectedIndex,
    string ProbabilityStatus = "UNVERIFIED")
{
    private int[]? _orderedCandidates = Clone(OrderedCandidates);

    public int[] OrderedCandidates
    {
        get => Clone(_orderedCandidates)!;
        init => _orderedCandidates = Clone(value);
    }

    public NatureReplayChoiceV111 Fork() => this with
    {
        OrderedCandidates = _orderedCandidates!
    };

    private static int[]? Clone(int[]? values) => values is null
        ? null
        : (int[])values.Clone();
}

public sealed record NatureBoundaryViewV111(
    string SchemaId, string Kind, string Mode, bool Complete,
    string Fingerprint, NatureBoundaryEventV111? Pending,
    NatureReplayChoiceV111[] Transcript)
{
    private NatureBoundaryEventV111? _pending = Pending?.Fork();
    private NatureReplayChoiceV111[]? _transcript = Clone(Transcript);

    public NatureBoundaryEventV111? Pending
    {
        get => _pending?.Fork();
        init => _pending = value?.Fork();
    }

    public NatureReplayChoiceV111[] Transcript
    {
        get => Clone(_transcript)!;
        init => _transcript = Clone(value);
    }

    public NatureBoundaryViewV111 Fork() => this with
    {
        Pending = _pending,
        Transcript = _transcript!
    };

    private static NatureReplayChoiceV111[]? Clone(
        NatureReplayChoiceV111[]? values) => values?.Select(choice =>
            choice?.Fork()!).ToArray();
}

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
            .Concat(state.ShuffleTranscript)
            .Concat(state.LiveTranscript).ToArray();
        var drawCost = state.Run.World?.Combat?.PendingChoice?.ControlledDrawCost;
        var randomCost = state.Run.World?.Combat?.PendingChoice?.ControlledRandomCost;
        var pool = state.Run.World?.Combat?.PendingChoice?.ControlledPoolShuffle;
        var insertion = state.Run.World?.Combat?.PendingChoice?.ControlledDrawInsertion;
        var generation = state.Run.World?.Combat?.PendingChoice?.ControlledGeneration;
        var potionGeneration = state.Run.World?.Combat?.PendingChoice?
            .ControlledPotionGeneration;
        var cardTransform = state.Run.World?.Combat?.PendingChoice?
            .ControlledCardTransform;
        var kind = drawCost is not null ? "drawn_card_cost"
            : randomCost is not null ? "random_card_cost"
            : potionGeneration is not null ? "potion_generation"
            : cardTransform is not null ? "card_transform"
            : pool is not null ? "generation_pool_shuffle"
            : insertion is not null ? "draw_insertion"
            : generation is not null ? "generated_card"
            : state.TargetPending is not null ? "random_target"
            : state.Recycle is not null ? "draw_recycle"
            : state.TurnIntents is not null ? "next_turn_intent"
            : state.Pending is not null ? "opening_shuffle" : "player";
        var view = Build(kind, state.Mode, state.Fingerprint,
            state.ActivePending, history);
        var candidateModels = pool?.CurrentCardIds ??
            (insertion is { Stage: "card_model" }
                ? insertion.CandidateCardIds : generation?.CandidateCardIds ??
                    potionGeneration?.CandidatePotionIds ??
                    cardTransform?.CandidateCardIds);
        if (view.Pending is not { } pending)
            return view;
        var candidateLabels = state.IntentChoicePending is { } intent
            ? intent.Support.Candidates.Select(candidate =>
                candidate.IntentId).ToArray()
            : ControlledPlayableCombatEngineV111
                .PendingCandidateLabels(state);
        return view with
        {
            Pending = pending with
            {
                CandidateCardIds = candidateModels is null
                    ? pending.CandidateCardIds
                    : (string[])candidateModels.Clone(),
                CandidateLabels = candidateLabels
            }
        };
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
            var eventId = EventId(r, transcript);
            var law = ChanceLawEngineV111.QueryRandomIndex(eventId, r);
            transcript.Add(new NatureReplayChoiceV111(
                eventId, r.SiteId, r.StreamId, r.OwnerId,
                r.Ordinal, r.OperationKind, r.SourceVersion,
                (int[])r.OrderedCandidates.Clone(), r.BoundedCount,
                decision.SelectedIndex, law.ProbabilityStatus));
        }
        NatureBoundaryEventV111? eventView = null;
        if (pending is not null)
        {
            pending.Validate();
            var eventId = EventId(pending, transcript);
            var law = ChanceLawEngineV111.QueryRandomIndex(eventId, pending);
            eventView = new NatureBoundaryEventV111(
                eventId, pending.SiteId,
                pending.StreamId, pending.OwnerId, pending.Ordinal,
                pending.OperationKind, pending.SourceVersion,
                (int[])pending.OrderedCandidates.Clone(), pending.BoundedCount,
                pending.SupportCount, CanonicalJson.Sha256(pending),
                law.ProbabilityStatus, law.LawId);
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
