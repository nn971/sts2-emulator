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

public sealed record SemanticDeckPoolV111(
    string PoolId, long[] RemainingCardInstanceIds)
{
    public SemanticDeckPoolV111 Fork() => this with
    {
        RemainingCardInstanceIds = (long[])RemainingCardInstanceIds.Clone()
    };
}

public sealed record SemanticDeckPendingV111(
    string SiteId, string Purpose, int TopOffset, string PoolId,
    long[] CandidateCardInstanceIds, int Ordinal)
{
    public SemanticDeckPendingV111 Fork() => this with
    {
        CandidateCardInstanceIds = (long[])CandidateCardInstanceIds.Clone()
    };
}

public sealed record SemanticDeckChoiceV111(
    string EventId, string SiteId, string Purpose, int TopOffset,
    int Ordinal, long[] CandidateCardInstanceIds,
    int SelectedIndex, long SelectedCardInstanceId,
    string ProbabilityStatus = "UNVERIFIED")
{
    public SemanticDeckChoiceV111 Fork() => this with
    {
        CandidateCardInstanceIds = (long[])CandidateCardInstanceIds.Clone()
    };
}

public sealed record SemanticDeckPublicCardV111(
    int TopOffset, long CardInstanceId);

public sealed record SemanticDeckPublicViewV111(
    int Count, SemanticDeckPublicCardV111[] RevealedPositions);

public sealed record SemanticDeckEventV111(
    string SchemaId, string EventId, string SiteId, string Purpose,
    int TopOffset, int Ordinal, string OperationKind,
    string StreamId, string OwnerId, long[] CandidateCardInstanceIds,
    string SupportFingerprint, string ProbabilityStatus = "UNVERIFIED")
    : ISemanticChanceEventV111;

/// <summary>Typed result for one semantic draw-pile event.</summary>
public sealed record SemanticDrawDecisionV111(
    SemanticDeckEventV111 Event, int SelectedIndex,
    long SelectedCardInstanceId)
{
    public void Validate()
    {
        if (Event.SchemaId != SemanticDeckStateV111.SchemaId ||
            string.IsNullOrWhiteSpace(Event.EventId) ||
            Event.OperationKind != "choose_card_instance" ||
            Event.StreamId != "semantic.draw_pile" ||
            Event.OwnerId != "player:draw_pile" ||
            Event.CandidateCardInstanceIds is not { Length: > 0 } ||
            Event.SupportFingerprint != CanonicalJson.Sha256(
                Event.CandidateCardInstanceIds) ||
            Event.ProbabilityStatus != "UNVERIFIED" ||
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

/// <summary>
/// Provider-private stochastic deck state. Arrays are treated as immutable:
/// every transition forks before changing a slot or a pool. Known and unknown
/// slots may interleave; one shared pool supplies all remaining unresolved
/// positions of an initial or source-triggered shuffle.
/// </summary>
public sealed record SemanticDeckStateV111(
    string Mode, SemanticDeckSlotV111[] Slots,
    SemanticDeckPoolV111[] Pools, int ShuffleEpoch = 0,
    SemanticDeckPendingV111? Pending = null,
    SemanticDeckChoiceV111[]? Transcript = null)
{
    public const string SchemaId = "sts2-v111-semantic-deck-v1";
    public const string NatureMode = "nature-deferred-v1";
    public const string NativeMode = "native-exact-v1";

    [JsonIgnore] public int Count => Slots.Length;
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);

    public SemanticDeckStateV111 Fork() => this with
    {
        Slots = (SemanticDeckSlotV111[])Slots.Clone(),
        Pools = Pools.Select(p => p.Fork()).ToArray(),
        Pending = Pending?.Fork(),
        Transcript = (Transcript ?? []).Select(t => t.Fork()).ToArray()
    };
}

/// <summary>
/// A versioned semantic chance kernel. It does not execute game hooks:
/// the combat engine consumes a concrete card after Require/Resolve and
/// then runs CardDrawn exactly once. This initial adapter is opt-in and
/// separate from the older shuffle-swap controlled combat fixture.
/// </summary>
public static class SemanticDeckEngineV111
{
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
                (state.Transcript ?? []).Length)
        });
    }

    public static SemanticDeckEventV111? Inspect(SemanticDeckStateV111 state)
    {
        Validate(state);
        var pending = state.Pending;
        if (pending is null) return null;
        return new SemanticDeckEventV111(
            SemanticDeckStateV111.SchemaId, EventId(state), pending.SiteId,
            pending.Purpose, pending.TopOffset, pending.Ordinal, "choose_card_instance",
            "semantic.draw_pile", "player:draw_pile",
            (long[])pending.CandidateCardInstanceIds.Clone(),
            CanonicalJson.Sha256(pending.CandidateCardInstanceIds));
    }

    public static SemanticDeckStateV111 Resolve(
        SemanticDeckStateV111 state, string fingerprint, int selectedIndex)
    {
        Validate(state);
        if (!StringComparer.Ordinal.Equals(state.Fingerprint, fingerprint))
            throw new InvalidOperationException("Stale semantic deck fingerprint.");
        var pending = state.Pending ??
            throw new InvalidOperationException("No semantic card reveal pending.");
        if (selectedIndex < 0 || selectedIndex >= pending.CandidateCardInstanceIds.Length)
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));
        var cardId = pending.CandidateCardInstanceIds[selectedIndex];
        var slots = (SemanticDeckSlotV111[])state.Slots.Clone();
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
            EventId(state), pending.SiteId, pending.Purpose,
            pending.TopOffset, pending.Ordinal,
            (long[])pending.CandidateCardInstanceIds.Clone(),
            selectedIndex, cardId);
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

    public static SemanticDeckStateV111 MoveKnownToTop(
        SemanticDeckStateV111 state, long cardId, bool publiclyKnown = true)
    {
        Validate(state);
        AssertIdle(state);
        var index = Array.FindIndex(state.Slots, slot =>
            slot.KnownCardInstanceId == cardId);
        if (index < 0)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK move_unknown_position");
        var slots = state.Slots.ToList();
        var slot = slots[index];
        slots.RemoveAt(index);
        slots.Insert(0, slot with
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
}
