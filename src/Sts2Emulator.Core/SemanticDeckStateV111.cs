using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// One top-first deck position. A known card belongs to mechanics; an
/// unresolved position refers to a provider-owned pool. Multiple positions
/// can share a pool across a forced insertion, preserving their coupling.
/// </summary>
public sealed record SemanticDeckSlotV111(
    long? KnownCardInstanceId, string? PoolId, bool PubliclyRevealed = false)
{
    public static SemanticDeckSlotV111 Known(long id, bool isPublic = false) =>
        new(id, null, isPublic);
    public static SemanticDeckSlotV111 Unknown(string pool) =>
        new(null, pool);
}

public sealed record SemanticDeckPoolV111
{
    private long[] _remainingCardInstanceIds;

    [JsonConstructor]
    public SemanticDeckPoolV111(
        string PoolId, long[] RemainingCardInstanceIds)
    {
        this.PoolId = PoolId;
        _remainingCardInstanceIds = CloneRequired(
            RemainingCardInstanceIds, nameof(RemainingCardInstanceIds));
    }

    public string PoolId { get; init; }
    public long[] RemainingCardInstanceIds
    {
        get => (long[])_remainingCardInstanceIds.Clone();
        init => _remainingCardInstanceIds = CloneRequired(
            value, nameof(RemainingCardInstanceIds));
    }

    public SemanticDeckPoolV111 Fork() => this with
    {
        RemainingCardInstanceIds = _remainingCardInstanceIds
    };

    public void Deconstruct(
        out string PoolId, out long[] RemainingCardInstanceIds)
    {
        PoolId = this.PoolId;
        RemainingCardInstanceIds = this.RemainingCardInstanceIds;
    }

    private static long[] CloneRequired(long[] values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return (long[])values.Clone();
    }
}

public sealed record SemanticDeckPendingV111
{
    private long[] _candidateCardInstanceIds;

    [JsonConstructor]
    public SemanticDeckPendingV111(
        string SiteId, string Purpose, int TopOffset, string PoolId,
        long[] CandidateCardInstanceIds, int Ordinal)
    {
        this.SiteId = SiteId;
        this.Purpose = Purpose;
        this.TopOffset = TopOffset;
        this.PoolId = PoolId;
        _candidateCardInstanceIds = CloneRequired(
            CandidateCardInstanceIds, nameof(CandidateCardInstanceIds));
        this.Ordinal = Ordinal;
    }

    public string SiteId { get; init; }
    public string Purpose { get; init; }
    public int TopOffset { get; init; }
    public string PoolId { get; init; }
    public long[] CandidateCardInstanceIds
    {
        get => (long[])_candidateCardInstanceIds.Clone();
        init => _candidateCardInstanceIds = CloneRequired(
            value, nameof(CandidateCardInstanceIds));
    }
    public int Ordinal { get; init; }

    public SemanticDeckPendingV111 Fork() => this with
    {
        CandidateCardInstanceIds = _candidateCardInstanceIds
    };

    public void Deconstruct(
        out string SiteId, out string Purpose, out int TopOffset,
        out string PoolId, out long[] CandidateCardInstanceIds,
        out int Ordinal)
    {
        SiteId = this.SiteId;
        Purpose = this.Purpose;
        TopOffset = this.TopOffset;
        PoolId = this.PoolId;
        CandidateCardInstanceIds = this.CandidateCardInstanceIds;
        Ordinal = this.Ordinal;
    }

    private static long[] CloneRequired(long[] values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return (long[])values.Clone();
    }
}

public sealed record SemanticDeckChoiceV111
{
    private long[] _candidateCardInstanceIds;

    [JsonConstructor]
    public SemanticDeckChoiceV111(
        string EventId, string SiteId, string Purpose, int TopOffset,
        int Ordinal, long[] CandidateCardInstanceIds,
        int SelectedIndex, long SelectedCardInstanceId,
        string ProbabilityStatus = "UNVERIFIED")
    {
        this.EventId = EventId;
        this.SiteId = SiteId;
        this.Purpose = Purpose;
        this.TopOffset = TopOffset;
        this.Ordinal = Ordinal;
        _candidateCardInstanceIds = CloneRequired(
            CandidateCardInstanceIds, nameof(CandidateCardInstanceIds));
        this.SelectedIndex = SelectedIndex;
        this.SelectedCardInstanceId = SelectedCardInstanceId;
        this.ProbabilityStatus = ProbabilityStatus;
    }

    public string EventId { get; init; }
    public string SiteId { get; init; }
    public string Purpose { get; init; }
    public int TopOffset { get; init; }
    public int Ordinal { get; init; }
    public long[] CandidateCardInstanceIds
    {
        get => (long[])_candidateCardInstanceIds.Clone();
        init => _candidateCardInstanceIds = CloneRequired(
            value, nameof(CandidateCardInstanceIds));
    }
    public int SelectedIndex { get; init; }
    public long SelectedCardInstanceId { get; init; }
    public string ProbabilityStatus { get; init; }

    public SemanticDeckChoiceV111 Fork() => this with
    {
        CandidateCardInstanceIds = _candidateCardInstanceIds
    };

    public void Deconstruct(
        out string EventId, out string SiteId, out string Purpose,
        out int TopOffset, out int Ordinal,
        out long[] CandidateCardInstanceIds, out int SelectedIndex,
        out long SelectedCardInstanceId, out string ProbabilityStatus)
    {
        EventId = this.EventId;
        SiteId = this.SiteId;
        Purpose = this.Purpose;
        TopOffset = this.TopOffset;
        Ordinal = this.Ordinal;
        CandidateCardInstanceIds = this.CandidateCardInstanceIds;
        SelectedIndex = this.SelectedIndex;
        SelectedCardInstanceId = this.SelectedCardInstanceId;
        ProbabilityStatus = this.ProbabilityStatus;
    }

    private static long[] CloneRequired(long[] values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return (long[])values.Clone();
    }
}

public sealed record SemanticDeckPublicCardV111(
    int TopOffset, long CardInstanceId);

public sealed record SemanticDeckPublicViewV111(
    int Count, SemanticDeckPublicCardV111[] RevealedPositions);

public sealed record SemanticDeckOrderV111
{
    private long[] _cardInstanceIds;

    [JsonConstructor]
    public SemanticDeckOrderV111(long[] CardInstanceIds) =>
        _cardInstanceIds = CloneRequired(CardInstanceIds);

    public long[] CardInstanceIds
    {
        get => (long[])_cardInstanceIds.Clone();
        init => _cardInstanceIds = CloneRequired(value);
    }

    public SemanticDeckOrderV111 Fork() => this with
    {
        CardInstanceIds = _cardInstanceIds
    };

    public void Deconstruct(out long[] CardInstanceIds) =>
        CardInstanceIds = this.CardInstanceIds;

    private static long[] CloneRequired(long[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return (long[])values.Clone();
    }
}

/// <summary>
/// Bounded exact enumeration of completions of one Nature-owned deck
/// constraint state. The listed completions have equal mass only under the
/// explicitly unverified independent-uniform-pool model.
/// </summary>
public sealed record SemanticDeckEnumerationV111
{
    private SemanticDeckOrderV111[] _outcomes;

    public const string CurrentSchemaId =
        "sts2-v111-semantic-deck-enumeration-v1";

    [JsonConstructor]
    public SemanticDeckEnumerationV111(
        string SchemaId, string StateFingerprint, string ConditioningPolicyId,
        string ProbabilityStatus, string ProbabilityProvenance,
        string CompletionCount, string OutcomeFingerprint,
        SemanticDeckOrderV111[] Outcomes)
    {
        this.SchemaId = SchemaId;
        this.StateFingerprint = StateFingerprint;
        this.ConditioningPolicyId = ConditioningPolicyId;
        this.ProbabilityStatus = ProbabilityStatus;
        this.ProbabilityProvenance = ProbabilityProvenance;
        this.CompletionCount = CompletionCount;
        this.OutcomeFingerprint = OutcomeFingerprint;
        _outcomes = CloneRequired(Outcomes);
    }

    public string SchemaId { get; init; }
    public string StateFingerprint { get; init; }
    public string ConditioningPolicyId { get; init; }
    public string ProbabilityStatus { get; init; }
    public string ProbabilityProvenance { get; init; }
    public string CompletionCount { get; init; }
    public string OutcomeFingerprint { get; init; }
    public SemanticDeckOrderV111[] Outcomes
    {
        get => (SemanticDeckOrderV111[])_outcomes.Clone();
        init => _outcomes = CloneRequired(value);
    }

    public SemanticDeckEnumerationV111 Fork() => this with
    {
        Outcomes = _outcomes.Select(outcome => outcome.Fork()).ToArray()
    };

    public void Deconstruct(
        out string SchemaId, out string StateFingerprint,
        out string ConditioningPolicyId, out string ProbabilityStatus,
        out string ProbabilityProvenance, out string CompletionCount,
        out string OutcomeFingerprint, out SemanticDeckOrderV111[] Outcomes)
    {
        SchemaId = this.SchemaId;
        StateFingerprint = this.StateFingerprint;
        ConditioningPolicyId = this.ConditioningPolicyId;
        ProbabilityStatus = this.ProbabilityStatus;
        ProbabilityProvenance = this.ProbabilityProvenance;
        CompletionCount = this.CompletionCount;
        OutcomeFingerprint = this.OutcomeFingerprint;
        Outcomes = this.Outcomes;
    }

    private static SemanticDeckOrderV111[] CloneRequired(
        SemanticDeckOrderV111[] outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        return (SemanticDeckOrderV111[])outcomes.Clone();
    }
}

public sealed record SemanticDeckEventV111 : ISemanticChanceEventV111
{
    private long[] _candidateCardInstanceIds;

    [JsonConstructor]
    public SemanticDeckEventV111(
        string SchemaId, string EventId, string SiteId, string Purpose,
        int TopOffset, int Ordinal, string OperationKind,
        string StreamId, string OwnerId, long[] CandidateCardInstanceIds,
        string SupportFingerprint, string ProbabilityStatus = "UNVERIFIED")
    {
        this.SchemaId = SchemaId;
        this.EventId = EventId;
        this.SiteId = SiteId;
        this.Purpose = Purpose;
        this.TopOffset = TopOffset;
        this.Ordinal = Ordinal;
        this.OperationKind = OperationKind;
        this.StreamId = StreamId;
        this.OwnerId = OwnerId;
        _candidateCardInstanceIds = CloneRequired(CandidateCardInstanceIds);
        this.SupportFingerprint = SupportFingerprint;
        this.ProbabilityStatus = ProbabilityStatus;
    }

    public string SchemaId { get; init; }
    public string EventId { get; init; }
    public string SiteId { get; init; }
    public string Purpose { get; init; }
    public int TopOffset { get; init; }
    public int Ordinal { get; init; }
    public string OperationKind { get; init; }
    public string StreamId { get; init; }
    public string OwnerId { get; init; }
    public long[] CandidateCardInstanceIds
    {
        get => (long[])_candidateCardInstanceIds.Clone();
        init => _candidateCardInstanceIds = CloneRequired(value);
    }
    public string SupportFingerprint { get; init; }
    public string ProbabilityStatus { get; init; }

    public void Validate()
    {
        if (SchemaId != SemanticDeckStateV111.SchemaId ||
            string.IsNullOrWhiteSpace(EventId) ||
            Purpose is not ("draw" or "peek" or "scry") ||
            SiteId != (Purpose == "draw"
                ? "combat.draw_pile.draw_card"
                : "combat.draw_pile.reveal_card") ||
            TopOffset < 0 || (Purpose == "draw" && TopOffset != 0) ||
            Ordinal < 0 || OperationKind != "choose_card_instance" ||
            StreamId != "semantic.draw_pile" ||
            OwnerId != "player:draw_pile" ||
            CandidateCardInstanceIds is not { Length: > 0 } ||
            SupportFingerprint != CanonicalJson.Sha256(
                CandidateCardInstanceIds) ||
            ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Invalid semantic deck event envelope.");
    }

    public void Deconstruct(
        out string SchemaId, out string EventId, out string SiteId,
        out string Purpose, out int TopOffset, out int Ordinal,
        out string OperationKind, out string StreamId, out string OwnerId,
        out long[] CandidateCardInstanceIds, out string SupportFingerprint,
        out string ProbabilityStatus)
    {
        SchemaId = this.SchemaId;
        EventId = this.EventId;
        SiteId = this.SiteId;
        Purpose = this.Purpose;
        TopOffset = this.TopOffset;
        Ordinal = this.Ordinal;
        OperationKind = this.OperationKind;
        StreamId = this.StreamId;
        OwnerId = this.OwnerId;
        CandidateCardInstanceIds = this.CandidateCardInstanceIds;
        SupportFingerprint = this.SupportFingerprint;
        ProbabilityStatus = this.ProbabilityStatus;
    }

    private static long[] CloneRequired(long[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return (long[])values.Clone();
    }
}

/// <summary>Typed result for one semantic draw-pile event.</summary>
public sealed record SemanticDrawDecisionV111(
    SemanticDeckEventV111 Event, int SelectedIndex,
    long SelectedCardInstanceId)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Event);
        Event.Validate();
        if (Event.Purpose != "draw" ||
            SelectedIndex < 0 ||
            SelectedIndex >= Event.CandidateCardInstanceIds.Length ||
            Event.CandidateCardInstanceIds[SelectedIndex] !=
                SelectedCardInstanceId)
            throw new InvalidDataException(
                "Invalid semantic draw decision event or outcome.");
    }

    public SemanticDrawDecisionV111 Fork() => this with
    {
        Event = Event with
        {
            CandidateCardInstanceIds =
                (long[])Event.CandidateCardInstanceIds.Clone()
        }
    };
}

/// <summary>Typed result for one deferred peek or scry reveal.</summary>
public sealed record SemanticDeckRevealDecisionV111(
    SemanticDeckEventV111 Event, int SelectedIndex,
    long SelectedCardInstanceId)
{
    public const string CurrentSchemaId =
        "sts2-v111-semantic-deck-reveal-decision-v1";

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Event);
        Event.Validate();
        if (Event.Purpose is not ("peek" or "scry") ||
            SelectedIndex < 0 ||
            SelectedIndex >= Event.CandidateCardInstanceIds.Length ||
            Event.CandidateCardInstanceIds[SelectedIndex] !=
                SelectedCardInstanceId)
            throw new InvalidDataException(
                "Invalid semantic deck-reveal event or outcome.");
    }

    public SemanticDeckRevealDecisionV111 Fork() => this with
    {
        Event = Event with
        {
            CandidateCardInstanceIds =
                (long[])Event.CandidateCardInstanceIds.Clone()
        }
    };
}

/// <summary>
/// Provider-private stochastic deck state. Arrays are treated as immutable:
/// every transition forks before changing a slot or a pool. Known and unknown
/// slots may interleave; one shared pool supplies all remaining unresolved
/// positions of an initial or source-triggered shuffle.
/// </summary>
public sealed record SemanticDeckStateV111
{
    private SemanticDeckSlotV111[] _slots;
    private SemanticDeckPoolV111[] _pools;
    private SemanticDeckChoiceV111[]? _transcript;
    // H8 commits share immutable transcript prefixes between decisions.
    private H8DeckTranscriptTrailV1? _h8TranscriptTrail;

    public const string SchemaId = "sts2-v111-semantic-deck-v1";
    public const string NatureMode = "nature-deferred-v1";
    public const string NativeMode = "native-exact-v1";

    [JsonConstructor]
    public SemanticDeckStateV111(
        string Mode, SemanticDeckSlotV111[] Slots,
        SemanticDeckPoolV111[] Pools, int ShuffleEpoch = 0,
        SemanticDeckPendingV111? Pending = null,
        SemanticDeckChoiceV111[]? Transcript = null)
    {
        this.Mode = Mode;
        _slots = CloneRequired(Slots, nameof(Slots));
        _pools = CloneRequired(Pools, nameof(Pools));
        this.ShuffleEpoch = ShuffleEpoch;
        this.Pending = Pending;
        _transcript = CloneOptional(Transcript);
    }

    public string Mode { get; init; }
    public SemanticDeckSlotV111[] Slots
    {
        get => (SemanticDeckSlotV111[])_slots.Clone();
        init => _slots = CloneRequired(value, nameof(Slots));
    }
    public SemanticDeckPoolV111[] Pools
    {
        get => (SemanticDeckPoolV111[])_pools.Clone();
        init => _pools = CloneRequired(value, nameof(Pools));
    }
    public int ShuffleEpoch { get; init; }
    public SemanticDeckPendingV111? Pending { get; init; }
    public SemanticDeckChoiceV111[]? Transcript
    {
        get => _h8TranscriptTrail?.Materialize() ?? CloneOptional(_transcript);
        init
        {
            _transcript = CloneOptional(value);
            _h8TranscriptTrail = null;
        }
    }

    [JsonIgnore]
    internal H8DeckTranscriptTrailV1? H8TranscriptTrail
    {
        get => _h8TranscriptTrail;
        init => _h8TranscriptTrail = value;
    }

    [JsonIgnore] internal int TranscriptCount =>
        _h8TranscriptTrail?.Count ?? _transcript?.Length ?? -1;

    [JsonIgnore] public int Count => _slots.Length;
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);

    // Trusted in-process H8 continuation can address the just-committed
    // transcript entry by its source ordinal. Avoid cloning and scanning
    // the full defensive-copy Transcript array for every physical draw.
    // The returned record is read-only to the internal caller; public
    // Transcript and explicit Fork still provide detached copies.
    internal SemanticDeckChoiceV111? TranscriptChoiceAtOrdinal(int ordinal)
    {
        if (ordinal < 0 || ordinal >= TranscriptCount)
            return null;
        if (_h8TranscriptTrail is { } trail)
            return ordinal == trail.Count - 1
                ? trail.LastChoice
                : trail.Materialize()[ordinal];
        return _transcript![ordinal];
    }

    internal SemanticDeckStateV111 AppendH8Transcript(
        SemanticDeckChoiceV111 choice)
    {
        var trail = _h8TranscriptTrail ??
            H8DeckTranscriptTrailV1.FromHistory(_transcript ?? []);
        return this with { H8TranscriptTrail = trail.Append(choice) };
    }

    /// <summary>
    /// Exact field equality for the in-process H8 mechanics-owner guard.
    /// The v111 checked API still uses canonical SHA fingerprints at its
    /// external trust boundary; an H8 version token already proves freshness.
    /// This comparison deliberately covers EVERY serialized state field,
    /// including provenance and transcript entries. It allocates no JSON
    /// buffers, constructs no digest and never changes provider state.
    /// </summary>
    internal bool HasSameSerializedContentsAs(SemanticDeckStateV111? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null ||
            !string.Equals(Mode, other.Mode, StringComparison.Ordinal) ||
            ShuffleEpoch != other.ShuffleEpoch ||
            _slots.Length != other._slots.Length ||
            _pools.Length != other._pools.Length ||
            TranscriptCount != other.TranscriptCount)
            return false;
        for (var i = 0; i < _slots.Length; i++)
            if (_slots[i] != other._slots[i])
                return false;
        for (var i = 0; i < _pools.Length; i++)
        {
            if (_pools[i].PoolId != other._pools[i].PoolId ||
                !_pools[i].RemainingCardInstanceIds.AsSpan().SequenceEqual(
                    other._pools[i].RemainingCardInstanceIds))
                return false;
        }
        if ((Pending is null) != (other.Pending is null))
            return false;
        if (Pending is { } leftPending &&
            other.Pending is { } rightPending &&
            (leftPending.SiteId != rightPending.SiteId ||
             leftPending.Purpose != rightPending.Purpose ||
             leftPending.TopOffset != rightPending.TopOffset ||
             leftPending.PoolId != rightPending.PoolId ||
             leftPending.Ordinal != rightPending.Ordinal ||
             !leftPending.CandidateCardInstanceIds.AsSpan().SequenceEqual(
                 rightPending.CandidateCardInstanceIds)))
            return false;
        if (TranscriptCount >= 0 &&
            !(_h8TranscriptTrail is not null &&
              ReferenceEquals(_h8TranscriptTrail, other._h8TranscriptTrail)))
        {
            // Explicit deep comparison only; normal H8 transitions share
            // immutable prefixes and never materialize them.
            var leftChoices = Transcript!;
            var rightChoices = other.Transcript!;
            for (var i = 0; i < leftChoices.Length; i++)
            {
                var left = leftChoices[i];
                var right = rightChoices[i];
                if (left.EventId != right.EventId ||
                    left.SiteId != right.SiteId ||
                    left.Purpose != right.Purpose ||
                    left.TopOffset != right.TopOffset ||
                    left.Ordinal != right.Ordinal ||
                    left.SelectedIndex != right.SelectedIndex ||
                    left.SelectedCardInstanceId != right.SelectedCardInstanceId ||
                    left.ProbabilityStatus != right.ProbabilityStatus ||
                    !left.CandidateCardInstanceIds.AsSpan().SequenceEqual(
                        right.CandidateCardInstanceIds))
                    return false;
            }
        }
        return true;
    }

    public SemanticDeckStateV111 Fork() => this with
    {
        Slots = (SemanticDeckSlotV111[])_slots.Clone(),
        Pools = _pools.Select(p => p.Fork()).ToArray(),
        Pending = Pending?.Fork(),
        Transcript = (Transcript ?? []).Select(t => t.Fork()).ToArray()
    };

    public void Deconstruct(
        out string Mode, out SemanticDeckSlotV111[] Slots,
        out SemanticDeckPoolV111[] Pools, out int ShuffleEpoch,
        out SemanticDeckPendingV111? Pending,
        out SemanticDeckChoiceV111[]? Transcript)
    {
        Mode = this.Mode;
        Slots = this.Slots;
        Pools = this.Pools;
        ShuffleEpoch = this.ShuffleEpoch;
        Pending = this.Pending;
        Transcript = this.Transcript;
    }

    private static T[] CloneRequired<T>(T[] values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return (T[])values.Clone();
    }

    private static T[]? CloneOptional<T>(T[]? values) =>
        values is null ? null : (T[])values.Clone();
}

/// <summary>
/// A versioned semantic chance kernel. It does not execute game hooks:
/// the combat engine consumes a concrete card after Require/Resolve and
/// then runs CardDrawn exactly once. This initial adapter is opt-in and
/// separate from the older shuffle-swap controlled combat fixture.
/// </summary>
public static class SemanticDeckEngineV111
{
    public const string UniformPoolEnumerationPolicyId =
        "uniform-independent-shared-pool-permutation-unverified-v1";
    public const int MaximumEnumeratedDeckOrders = 65_536;

    public static SemanticDeckStateV111 BeginNature(
        IReadOnlyCollection<long> cardInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(cardInstanceIds);
        var ids = cardInstanceIds.ToArray();
        ValidateUnique(ids);
        var state = new SemanticDeckStateV111(
            SemanticDeckStateV111.NatureMode,
            ids.Select(_ => SemanticDeckSlotV111.Unknown("shuffle:0")).ToArray(),
            ids.Length == 0 ? [] : [new("shuffle:0", ids)],
            Transcript: []);
        Validate(state);
        return state;
    }

    /// <summary>Source-committed native order, explicitly top-first.
    /// The caller performs audited native Fisher-Yates before passing it in.
    /// Reading a known position never generates a Nature ticket.</summary>
    public static SemanticDeckStateV111 BeginNative(
        IReadOnlyCollection<long> orderedTopFirst)
    {
        ArgumentNullException.ThrowIfNull(orderedTopFirst);
        var ids = orderedTopFirst.ToArray();
        ValidateUnique(ids);
        var state = new SemanticDeckStateV111(
            SemanticDeckStateV111.NativeMode,
            ids.Select(id => SemanticDeckSlotV111.Known(id)).ToArray(),
            [], Transcript: []);
        Validate(state);
        return state;
    }

    /// <summary>
    /// Transfer a previously source-committed deck order into the semantic
    /// protocol WITHOUT rerandomizing that order or emitting Nature tickets.
    /// Used only after a distinct, complete opening-shuffle fixture.
    /// Later source-triggered recycle creates genuinely unresolved slots.
    /// </summary>
    public static SemanticDeckStateV111 FromCommittedOpening(
        IReadOnlyCollection<long> orderedTopFirst)
    {
        var committed = BeginNative(orderedTopFirst);
        var nature = committed with { Mode = SemanticDeckStateV111.NatureMode };
        Validate(nature);
        return nature;
    }

    /// <summary>Require a card at a position. TopOffset=0 is the next draw;
    /// greater offsets are used only for a peek or scry. Known results can
    /// be reused, never rerolled. The returned state contains any pending
    /// Nature event; the caller must resolve it before executing hooks.</summary>
    public static SemanticDeckStateV111 Require(
        SemanticDeckStateV111 state, int topOffset, string purpose)
    {
        Validate(state);
        AssertIdle(state);
        if (purpose is not ("draw" or "peek" or "scry") ||
            topOffset < 0 || topOffset >= state.Count ||
            (purpose == "draw" && topOffset != 0))
            throw new ArgumentException("Invalid semantic reveal purpose or position.");
        var slot = state.Slots[topOffset];
        if (slot.KnownCardInstanceId is not null)
        {
            if (purpose == "draw" || slot.PubliclyRevealed)
                return state;
            var known = (SemanticDeckSlotV111[])state.Slots.Clone();
            known[topOffset] = slot with { PubliclyRevealed = true };
            return Checked(state with { Slots = known });
        }
        if (state.Mode != SemanticDeckStateV111.NatureMode)
            throw new InvalidDataException("Native exact deck contains unresolved position.");
        var pool = state.Pools.Single(p => p.PoolId == slot.PoolId);
        return Checked(state with
        {
            Pending = new SemanticDeckPendingV111(
                purpose == "draw"
                    ? "combat.draw_pile.draw_card"
                    : "combat.draw_pile.reveal_card",
                purpose, topOffset, pool.PoolId,
                (long[])pool.RemainingCardInstanceIds.Clone(),
                state.TranscriptCount)
        });
    }

    public static SemanticDeckEventV111? Inspect(SemanticDeckStateV111 state)
    {
        Validate(state);
        var pending = state.Pending;
        if (pending is null) return null;
        var semanticEvent = new SemanticDeckEventV111(
            SemanticDeckStateV111.SchemaId, EventId(state), pending.SiteId,
            pending.Purpose, pending.TopOffset, pending.Ordinal, "choose_card_instance",
            "semantic.draw_pile", "player:draw_pile",
            (long[])pending.CandidateCardInstanceIds.Clone(),
            CanonicalJson.Sha256(pending.CandidateCardInstanceIds));
        semanticEvent.Validate();
        return semanticEvent;
    }

    public static SemanticDeckStateV111 Resolve(
        SemanticDeckStateV111 state, string fingerprint, int selectedIndex)
    {
        Validate(state);
        if (!StringComparer.Ordinal.Equals(state.Fingerprint, fingerprint))
            throw new InvalidOperationException("Stale semantic deck fingerprint.");
        return ResolveCore(state, selectedIndex, EventId(state), trustedH8: false);
    }

    /// <summary>
    /// A versioned H8 boundary already fixes the exact physical draw event.
    /// Resolve it without rehashing the full deck or copying its transcript.
    /// </summary>
    internal static SemanticDeckStateV111 ResolveFromH8TrustedBoundary(
        SemanticDeckStateV111 state, SemanticDeckEventV111 expectedEvent,
        int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(expectedEvent);
        var pending = state.Pending ??
            throw new InvalidOperationException("No semantic card reveal pending.");
        if (expectedEvent.Purpose != pending.Purpose ||
            expectedEvent.SiteId != pending.SiteId ||
            expectedEvent.TopOffset != pending.TopOffset ||
            expectedEvent.Ordinal != pending.Ordinal ||
            !expectedEvent.CandidateCardInstanceIds.AsSpan().SequenceEqual(
                pending.CandidateCardInstanceIds))
            throw new InvalidDataException("Stale H8 physical draw support.");
        return ResolveCore(state, selectedIndex, expectedEvent.EventId,
            trustedH8: true);
    }

    private static SemanticDeckStateV111 ResolveCore(
        SemanticDeckStateV111 state, int selectedIndex,
        string eventId, bool trustedH8)
    {
        var pending = state.Pending ??
            throw new InvalidOperationException("No semantic card reveal pending.");
        if (selectedIndex < 0 ||
            selectedIndex >= pending.CandidateCardInstanceIds.Length)
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));
        var cardId = pending.CandidateCardInstanceIds[selectedIndex];
        var slots = state.Slots;
        slots[pending.TopOffset] = SemanticDeckSlotV111.Known(
            cardId, pending.Purpose != "draw");
        var pools = state.Pools.Select(pool => pool.PoolId == pending.PoolId
            ? pool with
            {
                RemainingCardInstanceIds = pool.RemainingCardInstanceIds
                    .Where(id => id != cardId).ToArray()
            }
            : pool.Fork())
            .Where(pool => pool.RemainingCardInstanceIds.Length > 0)
            .ToArray();
        var choice = new SemanticDeckChoiceV111(
            eventId, pending.SiteId, pending.Purpose,
            pending.TopOffset, pending.Ordinal,
            (long[])pending.CandidateCardInstanceIds.Clone(),
            selectedIndex, cardId);
        if (trustedH8)
        {
            var next = state with
            {
                Slots = slots, Pools = pools, Pending = null
            };
            return Checked(next.AppendH8Transcript(choice));
        }
        return Checked(state with
        {
            Slots = slots, Pools = pools, Pending = null,
            Transcript = (state.Transcript ?? []).Append(choice).ToArray()
        });
    }

    public static (SemanticDeckStateV111 Deck, long CardInstanceId) DrawKnown(
        SemanticDeckStateV111 state)
    {
        Validate(state);
        AssertIdle(state);
        if (state.Count == 0)
            throw new InvalidOperationException("Cannot draw from an empty pile.");
        var first = state.Slots[0];
        if (first.KnownCardInstanceId is not long cardId)
            throw new InvalidOperationException(
                "SEMANTIC_CHANCE_REQUIRED site=combat.draw_pile.draw_card");
        return (Checked(state with { Slots = state.Slots.Skip(1).ToArray() }), cardId);
    }

    /// <summary>Exact position insertions split unresolved ranges while
    /// preserving the ORIGINAL shared pool across both sides. Position 0
    /// is top; position Count is bottom.</summary>
    public static SemanticDeckStateV111 InsertKnown(
        SemanticDeckStateV111 state, int topOffset, long cardInstanceId,
        bool publiclyKnown = true)
    {
        Validate(state);
        AssertIdle(state);
        if (topOffset < 0 || topOffset > state.Count)
            throw new ArgumentOutOfRangeException(nameof(topOffset));
        AssertAbsent(state, cardInstanceId);
        var slots = state.Slots.ToList();
        slots.Insert(topOffset, SemanticDeckSlotV111.Known(
            cardInstanceId, publiclyKnown));
        return Checked(state with { Slots = slots.ToArray() });
    }

    public static SemanticDeckStateV111 PlaceTop(
        SemanticDeckStateV111 state, long cardId, bool publiclyKnown = true) =>
        InsertKnown(state, 0, cardId, publiclyKnown);

    public static SemanticDeckStateV111 PlaceBottom(
        SemanticDeckStateV111 state, long cardId, bool publiclyKnown = true) =>
        InsertKnown(state, state.Count, cardId, publiclyKnown);

    /// <summary>Remove an explicitly known instance for scry/exhaust/move.
    /// Arbitrarily removing an unresolved member of a split shared pool
    /// would change the number of positions on each side of an anchor,
    /// requiring a new stochastic choice. Fail before modifying anything.</summary>
    public static SemanticDeckStateV111 RemoveKnown(
        SemanticDeckStateV111 state, long cardId)
    {
        Validate(state);
        AssertIdle(state);
        var index = Array.FindIndex(state.Slots, slot =>
            slot.KnownCardInstanceId == cardId);
        if (index < 0)
        {
            if (state.Pools.Any(pool => pool.RemainingCardInstanceIds.Contains(cardId)))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK remove_unknown_card_position");
            throw new ArgumentException("Card instance is absent from this pile.");
        }
        var slots = state.Slots.ToList();
        slots.RemoveAt(index);
        return Checked(state with { Slots = slots.ToArray() });
    }

    /// <summary>Remove a selected card identity without revealing its
    /// position when its unresolved pool occupies one contiguous run.
    /// Removing any slot in that run leaves the same exchangeable
    /// constraints. A pool split around known anchors needs a separate
    /// latent-position choice and is rejected.</summary>
    public static bool CanRemoveCardInstance(
        SemanticDeckStateV111 state, long cardId)
    {
        Validate(state);
        AssertIdle(state);
        if (state.Slots.Any(slot => slot.KnownCardInstanceId == cardId))
            return true;
        var pool = state.Pools.SingleOrDefault(candidate =>
            candidate.RemainingCardInstanceIds.Contains(cardId));
        if (pool is null) return false;
        var positions = state.Slots.Select((slot, index) => (slot, index))
            .Where(item => item.slot.PoolId == pool.PoolId)
            .Select(item => item.index).ToArray();
        return positions.Length > 0 &&
            positions[^1] - positions[0] + 1 == positions.Length;
    }

    public static SemanticDeckStateV111 RemoveCardInstance(
        SemanticDeckStateV111 state, long cardId)
    {
        Validate(state);
        AssertIdle(state);
        var knownIndex = Array.FindIndex(state.Slots, slot =>
            slot.KnownCardInstanceId == cardId);
        if (knownIndex >= 0)
        {
            var knownSlots = state.Slots.ToList();
            knownSlots.RemoveAt(knownIndex);
            return Checked(state with { Slots = knownSlots.ToArray() });
        }

        var poolIndex = Array.FindIndex(state.Pools, pool =>
            pool.RemainingCardInstanceIds.Contains(cardId));
        if (poolIndex < 0)
            throw new ArgumentException("Card instance is absent from this pile.");
        var pool = state.Pools[poolIndex];
        var positions = state.Slots.Select((slot, index) => (slot, index))
            .Where(item => item.slot.PoolId == pool.PoolId)
            .Select(item => item.index).ToArray();
        if (positions.Length == 0 ||
            positions[^1] - positions[0] + 1 != positions.Length)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK " +
                "site=combat.card_selection " +
                "operation=remove_unknown_card_from_split_pool");

        var slots = state.Slots.ToList();
        // Slots in this run are exchangeable. Removing its last slot keeps
        // surrounding known anchors and the remaining pool law intact.
        slots.RemoveAt(positions[^1]);
        var remaining = pool.RemainingCardInstanceIds
            .Where(id => id != cardId).ToArray();
        var pools = new List<SemanticDeckPoolV111>();
        for (var index = 0; index < state.Pools.Length; index++)
        {
            if (index != poolIndex)
            {
                pools.Add(state.Pools[index].Fork());
                continue;
            }
            if (remaining.Length > 0)
                pools.Add(pool with { RemainingCardInstanceIds = remaining });
        }
        return Checked(state with
        {
            Slots = slots.ToArray(),
            Pools = pools.ToArray()
        });
    }

    public static SemanticDeckStateV111 RemoveCardInstances(
        SemanticDeckStateV111 state, IReadOnlyCollection<long> cardInstanceIds)
    {
        Validate(state);
        AssertIdle(state);
        ArgumentNullException.ThrowIfNull(cardInstanceIds);
        var selected = cardInstanceIds.ToArray();
        ValidateUnique(selected);
        var unresolved = selected.Where(id => state.Pools.Any(pool =>
            pool.RemainingCardInstanceIds.Contains(id))).ToArray();
        foreach (var id in selected.Except(unresolved))
            state = RemoveKnown(state, id);
        foreach (var id in unresolved)
        {
            if (!CanRemoveCardInstance(state, id))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK " +
                    "site=combat.card_selection " +
                    "operation=remove_unknown_card_from_split_pool");
            state = RemoveCardInstance(state, id);
        }
        return state;
    }

    public static SemanticDeckStateV111 MoveKnownToTop(
        SemanticDeckStateV111 state, long cardId, bool publiclyKnown = true) =>
        MoveKnownToOffset(state, cardId, 0, publiclyKnown);

    public static SemanticDeckStateV111 MoveKnownToBottom(
        SemanticDeckStateV111 state, long cardId, bool publiclyKnown = true)
    {
        Validate(state);
        AssertIdle(state);
        if (state.Count == 0)
            throw new InvalidOperationException(
                "Cannot move a card to the bottom of an empty pile.");
        return MoveKnownToOffset(
            state, cardId, state.Count - 1, publiclyKnown);
    }

    /// <summary>Move an already known instance to a precise final top-first
    /// offset. Unknown identities cannot be reordered without selecting the
    /// identity at their source position, so those moves fail closed.</summary>
    public static SemanticDeckStateV111 MoveKnownToOffset(
        SemanticDeckStateV111 state, long cardId, int topOffset,
        bool publiclyKnown = true)
    {
        Validate(state);
        AssertIdle(state);
        if (topOffset < 0 || topOffset >= state.Count)
            throw new ArgumentOutOfRangeException(nameof(topOffset));
        var index = Array.FindIndex(state.Slots, slot =>
            slot.KnownCardInstanceId == cardId);
        if (index < 0)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK move_unknown_position");
        var slots = state.Slots.ToList();
        var slot = slots[index];
        slots.RemoveAt(index);
        slots.Insert(topOffset, slot with
        {
            PubliclyRevealed = publiclyKnown || slot.PubliclyRevealed
        });
        return Checked(state with { Slots = slots.ToArray() });
    }

    /// <summary>Commit a new source-triggered shuffle with its distinct epoch.
    /// Hooks and native source RNG calls occur at the outer mechanics site,
    /// not here. Previous positional constraints are reset only by this
    /// explicit shuffle operation.</summary>
    public static SemanticDeckStateV111 ReshuffleNature(
        SemanticDeckStateV111 state)
    {
        Validate(state);
        AssertIdle(state);
        if (state.Mode != SemanticDeckStateV111.NatureMode)
            throw new InvalidOperationException("Nature shuffle requires Nature mode.");
        var cards = AllCards(state);
        var epoch = checked(state.ShuffleEpoch + 1);
        var poolId = "shuffle:" + epoch;
        return Checked(state with
        {
            Slots = cards.Select(_ => SemanticDeckSlotV111.Unknown(poolId)).ToArray(),
            Pools = cards.Length == 0 ? [] : [new(poolId, cards)],
            ShuffleEpoch = epoch
        });
    }

    /// <summary>
    /// The source-time draw-pile exhaustion transition: recycle the actual
    /// discard identities into a NEW deferred shuffle epoch. The optional
    /// Perfect Fit subset is forced to occupy the top of the shuffled pile
    /// (with independent unknown permutations inside each subset).
    /// Source-triggered shuffle hooks are executed by the combat engine
    /// before the next draw; this helper consumes no RNG primitives.
    /// </summary>
    public static SemanticDeckStateV111 RecycleNature(
        SemanticDeckStateV111 state,
        IReadOnlyCollection<long> discardCardInstanceIds,
        IReadOnlyCollection<long>? perfectFitTopCardInstanceIds = null)
    {
        Validate(state);
        AssertIdle(state);
        ArgumentNullException.ThrowIfNull(discardCardInstanceIds);
        if (state.Mode != SemanticDeckStateV111.NatureMode || state.Count != 0)
            throw new InvalidOperationException(
                "Semantic recycle requires an exhausted Nature draw pile.");
        var cards = discardCardInstanceIds.ToArray();
        ValidateUnique(cards);
        var prioritized = perfectFitTopCardInstanceIds?.ToArray() ?? [];
        ValidateUnique(prioritized);
        var priority = prioritized.ToHashSet();
        if (!priority.IsSubsetOf(cards))
            throw new ArgumentException(
                "Perfect Fit priority cards must belong to the recycled discard.");
        var top = cards.Where(priority.Contains).ToArray();
        var ordinary = cards.Where(id => !priority.Contains(id)).ToArray();
        var epoch = checked(state.ShuffleEpoch + 1);
        var topPool = "shuffle:" + epoch + ":perfect_fit";
        var ordinaryPool = "shuffle:" + epoch + ":ordinary";
        var pools = new List<SemanticDeckPoolV111>();
        if (top.Length > 0)
            pools.Add(new SemanticDeckPoolV111(topPool, top));
        if (ordinary.Length > 0)
            pools.Add(new SemanticDeckPoolV111(ordinaryPool, ordinary));
        return Checked(state with
        {
            Slots = top.Select(_ => SemanticDeckSlotV111.Unknown(topPool))
                .Concat(ordinary.Select(_ =>
                    SemanticDeckSlotV111.Unknown(ordinaryPool))).ToArray(),
            Pools = pools.ToArray(),
            ShuffleEpoch = epoch
        });
    }

    /// <summary>Native RNG has already committed the exact permutation;
    /// hand its top-first result to the same state representation.</summary>
    public static SemanticDeckStateV111 CommitNativeShuffle(
        SemanticDeckStateV111 state, IReadOnlyCollection<long> orderedTopFirst)
    {
        Validate(state);
        AssertIdle(state);
        if (state.Mode != SemanticDeckStateV111.NativeMode)
            throw new InvalidOperationException("Native shuffle requires native mode.");
        ArgumentNullException.ThrowIfNull(orderedTopFirst);
        var order = orderedTopFirst.ToArray();
        ValidateUnique(order);
        if (!order.Order().SequenceEqual(AllCards(state).Order()))
            throw new ArgumentException("Native shuffle must preserve card instances.");
        return Checked(state with
        {
            Slots = order.Select(id => SemanticDeckSlotV111.Known(id)).ToArray(),
            ShuffleEpoch = checked(state.ShuffleEpoch + 1)
        });
    }

    /// <summary>Commit the exact order selected through controlled Fisher–Yates
    /// swaps. The identities stay private to the engine; Nature has resolved
    /// every swap, so no unresolved positional pool remains.</summary>
    public static SemanticDeckStateV111 CommitControlledNatureShuffle(
        SemanticDeckStateV111 state, IReadOnlyCollection<long> orderedTopFirst)
    {
        Validate(state);
        AssertIdle(state);
        if (state.Mode != SemanticDeckStateV111.NatureMode)
            throw new InvalidOperationException(
                "Controlled shuffle requires Nature mode.");
        ArgumentNullException.ThrowIfNull(orderedTopFirst);
        var order = orderedTopFirst.ToArray();
        ValidateUnique(order);
        if (!order.Order().SequenceEqual(AllCards(state).Order()))
            throw new ArgumentException(
                "Controlled shuffle must preserve all card instances.");
        return Checked(state with
        {
            Slots = order.Select(id =>
                SemanticDeckSlotV111.Known(id, isPublic: false)).ToArray(),
            Pools = [],
            ShuffleEpoch = checked(state.ShuffleEpoch + 1)
        });
    }

    /// <summary>
    /// Record a source-timed native shuffle of the currently exhausted pile.
    /// The caller owns the source shuffle hook and its native RNG stream;
    /// this transition only commits the resulting top-first physical order.
    /// </summary>
    public static SemanticDeckStateV111 RecycleNative(
        SemanticDeckStateV111 state,
        IReadOnlyCollection<long> recycledCardInstanceIds,
        IReadOnlyCollection<long> orderedTopFirst)
    {
        Validate(state);
        AssertIdle(state);
        ArgumentNullException.ThrowIfNull(recycledCardInstanceIds);
        ArgumentNullException.ThrowIfNull(orderedTopFirst);
        if (state.Mode != SemanticDeckStateV111.NativeMode || state.Count != 0)
            throw new InvalidOperationException(
                "Native recycle requires an exhausted exact deck.");
        var recycled = recycledCardInstanceIds.ToArray();
        var order = orderedTopFirst.ToArray();
        ValidateUnique(recycled);
        ValidateUnique(order);
        if (!order.Order().SequenceEqual(recycled.Order()))
            throw new ArgumentException(
                "Native recycle order must preserve the recycled instances.");
        return Checked(state with
        {
            Slots = order.Select(id => SemanticDeckSlotV111.Known(id)).ToArray(),
            ShuffleEpoch = checked(state.ShuffleEpoch + 1)
        });
    }

    /// <summary>Only information already visible to the player.
    /// Pool contents and private native order are intentionally excluded.</summary>
    public static SemanticDeckPublicViewV111 Observe(SemanticDeckStateV111 state)
    {
        Validate(state);
        return new SemanticDeckPublicViewV111(state.Count,
            state.Slots.Select((slot, i) => new { slot, i })
                .Where(x => x.slot.PubliclyRevealed)
                .Select(x => new SemanticDeckPublicCardV111(
                    x.i, x.slot.KnownCardInstanceId!.Value)).ToArray());
    }

    /// <summary>
    /// Enumerate every full top-first order compatible with the saved Nature
    /// deck constraints. Each pool independently permutes its remaining
    /// physical identities across all of that pool's positions, including
    /// positions on both sides of known insertions. This is an exact
    /// combinatorial model for small states; its equal completion weights
    /// remain UNVERIFIED and are not a source-calibrated Chance Law.
    /// </summary>
    public static SemanticDeckEnumerationV111 EnumeratePossibleOrders(
        SemanticDeckStateV111 state, int maximumOrders = 4096)
    {
        Validate(state);
        if (state.Mode != SemanticDeckStateV111.NatureMode)
            throw new InvalidOperationException(
                "Exact constrained-order enumeration requires Nature-owned state; " +
                "a committed Native order is not Chance Law input.");
        if (maximumOrders is < 1 or > MaximumEnumeratedDeckOrders)
            throw new ArgumentOutOfRangeException(nameof(maximumOrders),
                $"Enumeration cap must be between 1 and {MaximumEnumeratedDeckOrders}.");

        var groups = state.Pools
            .OrderBy(pool => pool.PoolId, StringComparer.Ordinal)
            .Select(pool => (Pool: pool, Positions: state.Slots
                    .Select((slot, index) => (slot, index))
                    .Where(entry => entry.slot.PoolId == pool.PoolId)
                    .Select(entry => entry.index).ToArray()))
            .ToArray();
        var completionCount = groups.Aggregate(BigInteger.One,
            (count, group) => count * Factorial(
                group.Pool.RemainingCardInstanceIds.Length));
        if (completionCount > maximumOrders)
            throw new InvalidOperationException(
                $"Constrained deck has {completionCount.ToString(CultureInfo.InvariantCulture)} " +
                $"possible orders, exceeding the requested enumeration cap " +
                $"of {maximumOrders}.");

        var order = new long[state.Count];
        foreach (var (slot, index) in state.Slots
                     .Select((slot, index) => (slot, index)))
            if (slot.KnownCardInstanceId is long cardInstanceId)
                order[index] = cardInstanceId;

        var outcomes = new List<SemanticDeckOrderV111>((int)completionCount);
        AssignPool(0);
        if (outcomes.Count != checked((int)completionCount))
            throw new InvalidDataException(
                "Constrained deck enumeration did not produce its exact completion count.");

        var uniqueOrderKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var outcome in outcomes)
        {
            var cardIds = outcome.CardInstanceIds;
            var orderKey = string.Join(",", cardIds.Select(cardId =>
                cardId.ToString(CultureInfo.InvariantCulture)));
            if (cardIds.Length != state.Count ||
                !uniqueOrderKeys.Add(orderKey))
                throw new InvalidDataException(
                    "Constrained deck enumeration produced a malformed or duplicate order.");

            foreach (var (slot, index) in state.Slots
                         .Select((slot, index) => (slot, index)))
                if (slot.KnownCardInstanceId is long knownCardId &&
                    cardIds[index] != knownCardId)
                    throw new InvalidDataException(
                        "Constrained deck enumeration changed a known card position.");

            foreach (var group in groups)
            {
                var assignedCardIds = group.Positions
                    .Select(index => cardIds[index]).Order().ToArray();
                if (!assignedCardIds.SequenceEqual(
                        group.Pool.RemainingCardInstanceIds.Order()))
                    throw new InvalidDataException(
                        "Constrained deck enumeration changed a shared-pool identity set.");
            }
        }

        var outcomeFingerprint = CanonicalJson.Sha256(
            outcomes.Select(outcome => outcome.CardInstanceIds).ToArray());
        return new SemanticDeckEnumerationV111(
            SemanticDeckEnumerationV111.CurrentSchemaId,
            state.Fingerprint, UniformPoolEnumerationPolicyId,
            "UNVERIFIED",
            "uniform-independent-shared-pool-permutation-unverified",
            completionCount.ToString(CultureInfo.InvariantCulture),
            outcomeFingerprint, outcomes.ToArray());

        void AssignPool(int poolIndex)
        {
            if (poolIndex == groups.Length)
            {
                outcomes.Add(new SemanticDeckOrderV111((long[])order.Clone()));
                return;
            }

            var group = groups[poolIndex];
            var cardIds = group.Pool.RemainingCardInstanceIds.Order().ToArray();
            var assigned = new bool[cardIds.Length];
            var permutation = new long[cardIds.Length];
            AssignCard(0);

            void AssignCard(int positionIndex)
            {
                if (positionIndex == group.Positions.Length)
                {
                    for (int index = 0; index < group.Positions.Length; index++)
                        order[group.Positions[index]] = permutation[index];
                    AssignPool(poolIndex + 1);
                    return;
                }

                for (int cardIndex = 0; cardIndex < cardIds.Length; cardIndex++)
                {
                    if (assigned[cardIndex]) continue;
                    assigned[cardIndex] = true;
                    permutation[positionIndex] = cardIds[cardIndex];
                    AssignCard(positionIndex + 1);
                    assigned[cardIndex] = false;
                }
            }
        }
    }

    /// <summary>Used only by mechanics to validate physical card-zone
    /// identity, never exposed in the fair player observation.</summary>
    public static long[] AllCardInstanceIds(SemanticDeckStateV111 state)
    {
        Validate(state);
        return AllCards(state);
    }

    public static string Save(SemanticDeckStateV111 state)
    {
        Validate(state);
        return JsonSerializer.Serialize(state);
    }

    public static SemanticDeckStateV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var state = JsonSerializer.Deserialize<SemanticDeckStateV111>(json)
            ?? throw new InvalidDataException("Null semantic deck snapshot.");
        Validate(state);
        return state.Fork();
    }

    public static void Validate(SemanticDeckStateV111 state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Mode is not (
                SemanticDeckStateV111.NatureMode or SemanticDeckStateV111.NativeMode) ||
            state.Slots is null || state.Pools is null ||
            state.Transcript is null || state.ShuffleEpoch < 0)
            throw new InvalidDataException("Invalid semantic deck header.");
        if (state.Mode == SemanticDeckStateV111.NativeMode &&
            (state.Pools.Length > 0 || state.Pending is not null ||
             state.Slots.Any(s => s.PoolId is not null) ||
             state.Transcript.Length > 0))
            throw new InvalidDataException("Native exact state cannot include Nature events.");
        var poolById = new Dictionary<string, SemanticDeckPoolV111>(
            StringComparer.Ordinal);
        var all = new HashSet<long>();
        foreach (var pool in state.Pools)
        {
            if (pool is null || string.IsNullOrWhiteSpace(pool.PoolId) ||
                pool.RemainingCardInstanceIds is not { Length: > 0 } ||
                !poolById.TryAdd(pool.PoolId, pool))
                throw new InvalidDataException("Malformed unresolved card pool.");
            foreach (var id in pool.RemainingCardInstanceIds)
                if (!all.Add(id))
                    throw new InvalidDataException("Duplicate card instance in pools.");
        }
        foreach (var slot in state.Slots)
        {
            if (slot is null ||
                (slot.KnownCardInstanceId is null) == (slot.PoolId is null) ||
                (slot.PubliclyRevealed && slot.KnownCardInstanceId is null))
                throw new InvalidDataException("Malformed semantic deck position.");
            if (slot.KnownCardInstanceId is long id && !all.Add(id))
                throw new InvalidDataException("Duplicate card instance in positions.");
            if (slot.PoolId is not null && !poolById.ContainsKey(slot.PoolId))
                throw new InvalidDataException("Unknown card pool reference.");
        }
        foreach (var pool in state.Pools)
        {
            if (state.Slots.Count(s => s.PoolId == pool.PoolId) !=
                pool.RemainingCardInstanceIds.Length)
                throw new InvalidDataException(
                    "Shared card-pool size conflicts with remaining positions.");
        }
        foreach (var (choice, ordinal) in state.Transcript.Select((c, i) => (c, i)))
        {
            if (choice is null || choice.Ordinal != ordinal ||
                choice.ProbabilityStatus != "UNVERIFIED" ||
                string.IsNullOrWhiteSpace(choice.EventId) ||
                choice.CandidateCardInstanceIds is not { Length: > 0 } ||
                choice.SelectedIndex < 0 ||
                choice.SelectedIndex >= choice.CandidateCardInstanceIds.Length ||
                choice.SelectedCardInstanceId !=
                    choice.CandidateCardInstanceIds[choice.SelectedIndex])
                throw new InvalidDataException("Invalid semantic Nature transcript.");
        }
        if (state.Pending is { } pending)
        {
            if (state.Mode != SemanticDeckStateV111.NatureMode ||
                pending.Purpose is not ("draw" or "peek" or "scry") ||
                pending.SiteId != (pending.Purpose == "draw"
                    ? "combat.draw_pile.draw_card"
                    : "combat.draw_pile.reveal_card") ||
                pending.TopOffset < 0 || pending.TopOffset >= state.Count ||
                (pending.Purpose == "draw" && pending.TopOffset != 0) ||
                pending.Ordinal != state.Transcript.Length ||
                state.Slots[pending.TopOffset].PoolId != pending.PoolId ||
                !poolById.TryGetValue(pending.PoolId, out var pool) ||
                pending.CandidateCardInstanceIds is null ||
                !pool.RemainingCardInstanceIds.SequenceEqual(
                    pending.CandidateCardInstanceIds))
                throw new InvalidDataException("Stale semantic Nature support.");
        }
    }

    private static SemanticDeckStateV111 Checked(SemanticDeckStateV111 state)
    {
        Validate(state);
        return state;
    }

    private static string EventId(SemanticDeckStateV111 state) =>
        CanonicalJson.Sha256(new
        {
            schema = SemanticDeckStateV111.SchemaId,
            precursor = state with { Pending = null },
            state.Pending
        });

    private static void AssertIdle(SemanticDeckStateV111 state)
    {
        if (state.Pending is not null)
            throw new InvalidOperationException(
                "Resolve the pending semantic reveal before any other deck mutation.");
    }

    private static void AssertAbsent(SemanticDeckStateV111 state, long id)
    {
        if (AllCards(state).Contains(id))
            throw new ArgumentException("Card instance is already in this pile.");
    }

    private static long[] AllCards(SemanticDeckStateV111 state) =>
        state.Slots.Where(s => s.KnownCardInstanceId.HasValue)
            .Select(s => s.KnownCardInstanceId!.Value)
            .Concat(state.Pools.SelectMany(p => p.RemainingCardInstanceIds))
            .ToArray();

    private static void ValidateUnique(long[] ids)
    {
        if (ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Duplicate card instance identity.");
    }

    private static BigInteger Factorial(int value)
    {
        var result = BigInteger.One;
        for (int factor = 2; factor <= value; factor++) result *= factor;
        return result;
    }
}

public static class SemanticDeckRevealChoiceV111
{
    public static SemanticDeckRevealDecisionV111 Select(
        SemanticDeckEventV111 pending, string eventId,
        long cardInstanceId)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Semantic deck-reveal event ID is stale or mismatched.");
        pending.Validate();
        if (pending.Purpose is not ("peek" or "scry"))
            throw new InvalidDataException(
                "Invalid semantic deck-reveal event.");
        var index = Array.IndexOf(
            pending.CandidateCardInstanceIds, cardInstanceId);
        if (index < 0)
            throw new InvalidOperationException(
                "Card instance is outside the semantic reveal support.");
        var decision = new SemanticDeckRevealDecisionV111(
            pending, index, cardInstanceId);
        decision.Validate();
        return decision;
    }

    /// <summary>Commit a peek or scry reveal to the provider deck and
    /// publish its matching physical-pile state together.</summary>
    internal static RunState Resume(
        RunState checkpoint, SemanticDeckRevealDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var world = checkpoint.World ?? throw new InvalidDataException(
            "Semantic deck reveal lost its world state.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "Semantic deck reveal lost its combat state.");
        var nextCombat = SemanticDrawPileAdapterV111.ResolveNatureDeckEvent(
            combat, decision.Event, decision.SelectedIndex,
            "Semantic deck reveal lost its provider state.",
            "Semantic deck-reveal decision no longer matches its live request.");
        var resumed = checkpoint with
        {
            World = world with { Combat = nextCombat }
        };
        PrototypeStateInvariants.Validate(resumed);
        return resumed;
    }
}
