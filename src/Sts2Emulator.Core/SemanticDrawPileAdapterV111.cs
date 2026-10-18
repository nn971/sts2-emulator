namespace Sts2Emulator.Core;

internal readonly record struct DrawPileTopCardResolutionV111(
    SemanticDeckStateV111? Deck,
    long? CardInstanceId,
    IReadOnlyList<long> RemainingPhysicalDrawPile)
{
    public bool IsPending => CardInstanceId is null;
}

/// <summary>
/// Keeps the engine-owned draw-pile membership and provider-owned hidden
/// order constraints synchronized at one mutation boundary. Physical zone
/// arrays remain the mechanics membership source; this adapter commits the
/// same operation to the selected stochastic provider before a state is
/// published.
/// </summary>
public static class SemanticDrawPileAdapterV111
{
    /// <summary>
    /// Transfers the engine's already-committed opening pile into a
    /// Nature-mode semantic deck without rerandomizing its order.
    /// </summary>
    public static CombatState InitializeFromCommittedOpening(
        CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(combat);
        var deck = SemanticDeckEngineV111.FromCommittedOpening(
            combat.DrawPile.Reverse().ToArray());
        return InitializeSemanticDrawPile(combat, deck);
    }

    /// <summary>Checks membership correspondence and, for native exact decks,
    /// the top-first order correspondence. Pending Nature reveals are valid
    /// snapshots and are accepted unless the caller requires an idle deck.
    /// </summary>
    public static void ValidateSynchronized(CombatState combat, bool requireIdle = false)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ValidateProviderCorrespondence(combat.SemanticDeck, combat.DrawPile);
        if (requireIdle && combat.SemanticDeck?.Pending is not null)
            throw new InvalidDataException(
                "Cannot mutate a draw pile with a pending semantic reveal.");
    }

    /// <summary>
    /// Rejects a continuation whose supported mechanics must preserve both
    /// the physical draw-pile order and its provider-owned deck state.
    /// </summary>
    internal static void RequireUnchangedDrawPile(
        CombatState before, CombatState after, string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        if (!before.DrawPile.SequenceEqual(after.DrawPile) ||
            CanonicalJson.Sha256(before.SemanticDeck) !=
                CanonicalJson.Sha256(after.SemanticDeck))
            throw new NotSupportedException(errorMessage);
    }

    /// <summary>
    /// Validates a Nature choice boundary against the physical draw pile.
    /// A few legacy-compatible continuations may omit the provider deck;
    /// their caller decides whether that is valid for the current boundary.
    /// </summary>
    internal static SemanticDeckStateV111? ValidateNatureDeckBoundary(
        CombatState combat, string missingDeckMessage,
        string invalidBoundaryMessage, bool allowMissing = false,
        bool allowPending = false,
        string? requiredPendingPurpose = null)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentException.ThrowIfNullOrWhiteSpace(missingDeckMessage);
        ArgumentException.ThrowIfNullOrWhiteSpace(invalidBoundaryMessage);
        if (combat.SemanticDeck is not { } deck)
        {
            if (allowMissing) return null;
            throw new InvalidDataException(missingDeckMessage);
        }

        SemanticDeckEngineV111.Validate(deck);
        if (deck.Mode != SemanticDeckStateV111.NatureMode ||
            (requiredPendingPurpose is not null
                ? deck.Pending?.Purpose != requiredPendingPurpose
                : !allowPending && deck.Pending is not null) ||
            !SemanticDeckEngineV111.AllCardInstanceIds(deck)
                .Order().SequenceEqual(combat.DrawPile.Order()))
            throw new InvalidDataException(invalidBoundaryMessage);
        return deck;
    }

    /// <summary>
    /// Resolves the current top card from the shared provider/physical-pile
    /// contract. A Nature-owned unknown position returns its pending reveal;
    /// a known position is consumed from the provider state and returned by
    /// stable physical identity. With no semantic provider, the physical
    /// pile's top identity is resolved directly. The caller still runs its
    /// own draw hooks.
    /// </summary>
    internal static DrawPileTopCardResolutionV111 ResolveTopCard(
        SemanticDeckStateV111? semantic,
        IReadOnlyList<long> physicalDrawPile)
    {
        ArgumentNullException.ThrowIfNull(physicalDrawPile);
        ValidateProviderCorrespondence(semantic, physicalDrawPile);
        if (physicalDrawPile.Count == 0)
            throw new InvalidOperationException(
                "Cannot resolve the top of an empty draw pile.");

        if (semantic is null)
            return new DrawPileTopCardResolutionV111(
                null,
                physicalDrawPile[^1],
                physicalDrawPile.Take(physicalDrawPile.Count - 1).ToArray());

        var required = SemanticDeckEngineV111.Require(semantic, 0, "draw");
        if (required.Pending is not null)
            return new DrawPileTopCardResolutionV111(
                required, null, physicalDrawPile.ToArray());

        var (consumed, cardInstanceId) =
            SemanticDeckEngineV111.DrawKnown(required);
        if (semantic.Mode == SemanticDeckStateV111.NativeMode &&
            cardInstanceId != physicalDrawPile[^1])
            throw new InvalidDataException(
                "Native semantic draw disagrees with the physical top card.");
        var physicalIndex = IndexOf(physicalDrawPile, cardInstanceId);
        if (physicalIndex < 0)
            throw new InvalidDataException(
                "Resolved semantic card instance is absent from the draw pile.");
        var remainingPhysicalDrawPile = new long[physicalDrawPile.Count - 1];
        for (var i = 0; i < physicalDrawPile.Count; i++)
        {
            if (i < physicalIndex)
                remainingPhysicalDrawPile[i] = physicalDrawPile[i];
            else if (i > physicalIndex)
                remainingPhysicalDrawPile[i - 1] = physicalDrawPile[i];
        }
        ValidateProviderCorrespondence(consumed, remainingPhysicalDrawPile);
        return new DrawPileTopCardResolutionV111(
            consumed, cardInstanceId, remainingPhysicalDrawPile);
    }

    /// <summary>
    /// Replays the saved top-card resolutions from the synchronized source
    /// checkpoint, then commits their final provider state with the matching
    /// physical draw-pile and destination-zone changes.
    /// </summary>
    internal static CombatState CommitTopCardResolutions(
        CombatState combat,
        IReadOnlyList<DrawPileTopCardResolutionV111> resolutions,
        CombatCardZonesV111 nextCardZones)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(resolutions);
        ArgumentNullException.ThrowIfNull(nextCardZones);
        ValidateSynchronized(combat, requireIdle: true);

        var semantic = combat.SemanticDeck;
        IReadOnlyList<long> physicalDrawPile = combat.DrawPile;
        for (int index = 0; index < resolutions.Count; index++)
        {
            var expected = ResolveTopCard(semantic, physicalDrawPile);
            var saved = resolutions[index];
            if (expected.IsPending != saved.IsPending ||
                expected.CardInstanceId != saved.CardInstanceId ||
                expected.Deck?.Fingerprint != saved.Deck?.Fingerprint ||
                !expected.RemainingPhysicalDrawPile.SequenceEqual(
                    saved.RemainingPhysicalDrawPile) ||
                expected.IsPending && index != resolutions.Count - 1)
                throw new InvalidDataException(
                    "Resolved semantic top-card sequence differs from its source checkpoint.");
            semantic = expected.Deck;
            physicalDrawPile = expected.RemainingPhysicalDrawPile;
        }

        if (!nextCardZones.DrawPile.SequenceEqual(physicalDrawPile))
            throw new InvalidDataException(
                "Resolved semantic top cards differ from the committed physical draw pile.");
        ValidateProviderCorrespondence(semantic, nextCardZones.DrawPile);
        return ApplyCommittedZoneTransition(
            combat, nextCardZones, semantic);
    }

    /// <summary>
    /// Atomically transfers an exhausted draw pile's discard identities into
    /// a new unresolved Nature shuffle epoch. Callers retain ownership of
    /// shuffle hooks and any selected physical ordering used by their frame.
    /// </summary>
    internal static CombatState CommitNatureRecycle(
        CombatState combat,
        IReadOnlyList<long> nextPhysicalDrawPile,
        IReadOnlyCollection<long> perfectFitTopCardInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(nextPhysicalDrawPile);
        ArgumentNullException.ThrowIfNull(perfectFitTopCardInstanceIds);
        ValidateSynchronized(combat, requireIdle: true);
        if (combat.SemanticDeck is not { } semantic ||
            semantic.Mode != SemanticDeckStateV111.NatureMode ||
            semantic.Count != 0 || combat.DrawPile.Length != 0)
            throw new InvalidDataException(
                "Nature recycle requires an exhausted synchronized draw pile.");

        var recycled = (long[])combat.DiscardPile.Clone();
        var nextPhysical = nextPhysicalDrawPile.ToArray();
        var perfectFit = perfectFitTopCardInstanceIds.ToArray();
        var recycledSet = recycled.ToHashSet();
        if (recycled.Length == 0 ||
            recycled.Distinct().Count() != recycled.Length ||
            nextPhysical.Distinct().Count() != nextPhysical.Length ||
            perfectFit.Distinct().Count() != perfectFit.Length ||
            !recycled.Order().SequenceEqual(nextPhysical.Order()) ||
            perfectFit.Any(id => !recycledSet.Contains(id)))
            throw new InvalidDataException(
                "Nature recycle physical zones do not preserve card identities.");

        var nextDeck = SemanticDeckEngineV111.RecycleNature(
            semantic, recycled, perfectFit);
        var nextZones = combat.Mechanics.CardZones.WithZoneChanges(
            new Dictionary<PrototypeCardZone, long[]>
            {
                [PrototypeCardZone.DrawPile] = nextPhysical,
                [PrototypeCardZone.DiscardPile] = []
            });
        return ApplyDrawPileShuffle(
            combat, nextPhysical, recycled, nextZones, nextDeck);
    }

    /// <summary>
    /// Commits an already-resolved legacy recycle using the committed discard
    /// zone as its source. Callers own shuffle sampling and source hooks.
    /// </summary>
    internal static CombatState CommitLegacyRecycle(
        CombatState combat,
        IReadOnlyList<long> shuffledPhysicalDrawPile)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(shuffledPhysicalDrawPile);
        ValidateSynchronized(combat, requireIdle: true);
        if (combat.SemanticDeck is not null ||
            combat.DrawPile.Length != 0 || combat.DiscardPile.Length == 0 ||
            combat.DiscardPile.Distinct().Count() != combat.DiscardPile.Length)
            throw new InvalidDataException(
                "Legacy recycle requires an exhausted draw pile with unique discards.");

        var recycled = (long[])combat.DiscardPile.Clone();
        var shuffled = shuffledPhysicalDrawPile.ToArray();
        if (shuffled.Distinct().Count() != shuffled.Length ||
            !shuffled.Order().SequenceEqual(recycled.Order()))
            throw new InvalidDataException(
                "Legacy recycle shuffle changed or duplicated discard identities.");
        var nextZones = combat.Mechanics.CardZones.WithZoneChanges(
            new Dictionary<PrototypeCardZone, long[]>
            {
                [PrototypeCardZone.DrawPile] = shuffled,
                [PrototypeCardZone.DiscardPile] = []
            });
        return ApplyDrawPileShuffle(
            combat, shuffled, recycled, nextZones, committedSemanticDeck: null);
    }

    /// <summary>
    /// Commits an already-resolved Native recycle using the committed discard
    /// zone as its source and the supplied post-hook physical order.
    /// </summary>
    internal static CombatState CommitNativeRecycle(
        CombatState combat,
        IReadOnlyList<long> shuffledPhysicalDrawPile)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(shuffledPhysicalDrawPile);
        ValidateSynchronized(combat, requireIdle: true);
        if (combat.SemanticDeck is not { } semantic ||
            semantic.Mode != SemanticDeckStateV111.NativeMode ||
            semantic.Count != 0 ||
            combat.DrawPile.Length != 0 || combat.DiscardPile.Length == 0 ||
            combat.DiscardPile.Distinct().Count() != combat.DiscardPile.Length)
            throw new InvalidDataException(
                "Native recycle requires an exhausted exact deck and unique discards.");

        var recycled = (long[])combat.DiscardPile.Clone();
        var shuffled = shuffledPhysicalDrawPile.ToArray();
        if (shuffled.Distinct().Count() != shuffled.Length ||
            !shuffled.Order().SequenceEqual(recycled.Order()))
            throw new InvalidDataException(
                "Native recycle shuffle changed or duplicated discard identities.");
        var nextDeck = SemanticDeckEngineV111.RecycleNative(
            semantic, recycled, shuffled.Reverse().ToArray());
        var nextZones = combat.Mechanics.CardZones.WithZoneChanges(
            new Dictionary<PrototypeCardZone, long[]>
            {
                [PrototypeCardZone.DrawPile] = shuffled,
                [PrototypeCardZone.DiscardPile] = []
            });
        return ApplyDrawPileShuffle(
            combat, shuffled, recycled, nextZones, nextDeck);
    }

    private static void ValidateProviderCorrespondence(
        SemanticDeckStateV111? semantic,
        IReadOnlyList<long> physicalDrawPile)
    {
        if (physicalDrawPile.Distinct().Count() != physicalDrawPile.Count)
            throw new InvalidDataException(
                "Physical draw pile contains duplicate card instances.");
        if (semantic is null) return;
        SemanticDeckEngineV111.Validate(semantic);
        if (!SemanticDeckEngineV111.AllCardInstanceIds(semantic)
                .Order().SequenceEqual(physicalDrawPile.Order()))
            throw new InvalidDataException(
                "Semantic deck membership differs from the combat draw pile.");
        if (semantic.Mode == SemanticDeckStateV111.NativeMode &&
            !semantic.Slots.Select(slot => slot.KnownCardInstanceId!.Value)
                .SequenceEqual(physicalDrawPile.Reverse()))
            throw new InvalidDataException(
                "Native semantic deck order differs from the physical draw pile.");
    }

    private static int IndexOf(IReadOnlyList<long> values, long value)
    {
        for (var i = 0; i < values.Count; i++)
            if (values[i] == value)
                return i;
        return -1;
    }

    /// <summary>
    /// Commits a complete physical draw-pile order supplied by a caller that
    /// has already resolved that order, such as a saved Fisher-Yates step.
    /// The identity set must remain unchanged; insertions, removals, and
    /// shuffles with incoming cards have dedicated operations.
    /// </summary>
    public static CombatState ApplyDrawPileOrderCommit(
        CombatState combat, IReadOnlyList<long> nextPhysicalDrawPile)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(nextPhysicalDrawPile);
        var next = nextPhysicalDrawPile.ToArray();
        if (next.Distinct().Count() != next.Length ||
            !next.Order().SequenceEqual(combat.DrawPile.Order()))
            throw new InvalidDataException(
                "Draw-pile order commit must preserve every physical identity.");
        var semantic = SynchronizeSemanticDrawPileAfterMutation(combat, next);
        var nextZones = combat.Mechanics.CardZones.WithCardsIn(
            PrototypeCardZone.DrawPile, next);
        return ApplyCommittedZoneTransition(combat, nextZones, semantic);
    }

    /// <summary>Compatibility alias for a full same-identity order commit.</summary>
    public static CombatState ApplyPhysicalMutation(
        CombatState combat, long[] nextPhysicalDrawPile) =>
        ApplyDrawPileOrderCommit(combat, nextPhysicalDrawPile);

    /// <summary>
    /// Initializes a fixture's physical draw pile and synchronizes its
    /// existing provider state before publishing all supplied card zones.
    /// Runtime identity changes should use insertion, removal, replacement,
    /// or shuffle operations instead.
    /// </summary>
    public static CombatState ApplyDrawPileInitialization(
        CombatState combat, CombatCardZonesV111 initializedCardZones)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(initializedCardZones);
        var semantic = SynchronizeSemanticDrawPileAfterMutation(
            combat, initializedCardZones.DrawPile);
        return CommitZoneTransition(
            combat, initializedCardZones, semantic);
    }

    /// <summary>
    /// Attaches an initial provider realization to an existing physical draw
    /// pile and validates their membership and Native order before publishing.
    /// Runtime identity changes use the dedicated insertion/removal methods.
    /// </summary>
    internal static CombatState InitializeSemanticDrawPile(
        CombatState combat, SemanticDeckStateV111 semanticDeck)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(semanticDeck);
        if (combat.SemanticDeck is not null)
            throw new InvalidOperationException(
                "Semantic draw-pile initialization requires an unowned provider state.");
        return ApplyCommittedPhysicalMutation(
            combat, combat.DrawPile, semanticDeck);
    }

    /// <summary>
    /// Commits a mechanics transition that changes one or more card zones.
    /// When DrawPile changes, its provider constraint is updated before the
    /// complete zone owner is published.
    /// </summary>
    public static CombatState ApplyZoneTransition(
        CombatState combat, CombatCardZonesV111 nextCardZones)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(nextCardZones);
        if (!combat.DrawPile.SequenceEqual(nextCardZones.DrawPile))
            throw new InvalidOperationException(
                "DrawPile changes require a typed draw-pile adapter operation.");
        return ApplyZoneIdentityDelta(
            combat, nextCardZones, addedCardInstanceIds: [],
            removedCardInstanceIds: []);
    }

    /// <summary>
    /// Commits a non-draw-pile transition with explicitly declared physical
    /// card additions and removals. All other identities must remain present
    /// exactly once across the six combat card zones.
    /// </summary>
    public static CombatState ApplyZoneIdentityDelta(
        CombatState combat,
        CombatCardZonesV111 nextCardZones,
        IReadOnlyCollection<long> addedCardInstanceIds,
        IReadOnlyCollection<long> removedCardInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(nextCardZones);
        ArgumentNullException.ThrowIfNull(addedCardInstanceIds);
        ArgumentNullException.ThrowIfNull(removedCardInstanceIds);
        if (!combat.DrawPile.SequenceEqual(nextCardZones.DrawPile))
            throw new InvalidOperationException(
                "DrawPile changes require a typed draw-pile adapter operation.");

        var currentIds = CardZoneIdentitySet(combat.Mechanics.CardZones);
        var nextIds = CardZoneIdentitySet(nextCardZones);
        var added = addedCardInstanceIds.ToArray();
        var removed = removedCardInstanceIds.ToArray();
        if (added.Distinct().Count() != added.Length ||
            removed.Distinct().Count() != removed.Length ||
            added.Intersect(removed).Any() ||
            added.Any(currentIds.Contains) ||
            removed.Any(id => !currentIds.Contains(id)))
            throw new InvalidDataException(
                "Card-zone identity delta has duplicate or unavailable identities.");

        var expectedIds = currentIds.Except(removed).Concat(added).ToHashSet();
        if (!expectedIds.SetEquals(nextIds))
            throw new InvalidDataException(
                "Card-zone transition does not match its declared identity delta.");
        return CommitZoneTransition(
            combat, nextCardZones, combat.SemanticDeck);
    }

    /// <summary>
    /// Replaces one non-draw-pile identity in its existing physical zone.
    /// Every other zone and identity must remain unchanged.
    /// </summary>
    public static CombatState ApplyZoneIdentityReplacement(
        CombatState combat,
        CombatCardZonesV111 nextCardZones,
        long replacedCardInstanceId,
        long replacementCardInstanceId)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(nextCardZones);
        var currentIds = CardZoneIdentitySet(combat.Mechanics.CardZones);
        if (replacedCardInstanceId == replacementCardInstanceId ||
            !currentIds.Contains(replacedCardInstanceId) ||
            currentIds.Contains(replacementCardInstanceId))
            throw new InvalidDataException(
                "Card-zone replacement has an invalid source or destination identity.");

        foreach (var zone in Enum.GetValues<PrototypeCardZone>())
        {
            var expected = combat.Mechanics.CardZones.CardsIn(zone)
                .Select(id => id == replacedCardInstanceId
                    ? replacementCardInstanceId : id).ToArray();
            if (!expected.SequenceEqual(nextCardZones.CardsIn(zone)))
                throw new InvalidDataException(
                    "Card-zone replacement changed an unrelated zone identity or order.");
        }
        return ApplyZoneIdentityDelta(
            combat, nextCardZones, [replacementCardInstanceId],
            [replacedCardInstanceId]);
    }

    private static HashSet<long> CardZoneIdentitySet(
        CombatCardZonesV111 zones)
    {
        ArgumentNullException.ThrowIfNull(zones);
        var ids = Enum.GetValues<PrototypeCardZone>()
            .SelectMany(zone => zones.CardsIn(zone)).ToArray();
        if (ids.Distinct().Count() != ids.Length)
            throw new InvalidDataException(
                "Card-zone owner contains duplicate physical identities.");
        return ids.ToHashSet();
    }

    /// <summary>
    /// Commits a source-visible insertion at one physical bottom-first
    /// draw-pile index, including the corresponding provider constraint and
    /// any other zone changes supplied by the caller.
    /// </summary>
    public static CombatState ApplyDrawPileInsertion(
        CombatState combat,
        int physicalIndex,
        long cardInstanceId,
        CombatCardZonesV111? nextCardZones = null,
        bool publiclyKnown = false) =>
        ApplyDrawPileInsertionSequence(
            combat, physicalIndex, [cardInstanceId], nextCardZones,
            publiclyKnown);

    /// <summary>
    /// Commits a source-ordered run of known card identities at one physical
    /// draw-pile index. The resulting membership, order, provider constraints,
    /// and source-zone removals are published together.
    /// </summary>
    internal static CombatState ApplyDrawPileInsertionSequence(
        CombatState combat,
        int physicalIndex,
        IReadOnlyList<long> insertedCardInstanceIds,
        CombatCardZonesV111? nextCardZones = null,
        bool publiclyKnown = false,
        IReadOnlyCollection<long>? removedCardInstanceIds = null)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(insertedCardInstanceIds);
        removedCardInstanceIds ??= Array.Empty<long>();
        if (physicalIndex < 0 || physicalIndex > combat.DrawPile.Length)
            throw new ArgumentOutOfRangeException(nameof(physicalIndex));

        var expectedDrawPile = combat.DrawPile.ToList();
        var inserted = insertedCardInstanceIds.ToArray();
        if (inserted.Distinct().Count() != inserted.Length ||
            inserted.Any(combat.DrawPile.Contains))
            throw new InvalidDataException(
                "Draw-pile insertion sequence contains duplicate identities.");
        expectedDrawPile.InsertRange(physicalIndex, inserted);
        var zones = nextCardZones ?? combat.Mechanics.CardZones.WithCardsIn(
            PrototypeCardZone.DrawPile, expectedDrawPile.ToArray());
        if (!zones.DrawPile.SequenceEqual(expectedDrawPile))
            throw new InvalidDataException(
                "Draw-pile insertion sequence does not match the requested physical index.");
        if (Enum.GetValues<PrototypeCardZone>()
            .Where(zone => zone != PrototypeCardZone.DrawPile)
            .Any(zone => inserted.Any(zones.CardsIn(zone).Contains)))
            throw new InvalidDataException(
                "Draw-pile insertion sequence duplicates an identity in its resulting zones.");

        var currentIds = CardZoneIdentitySet(combat.Mechanics.CardZones);
        var nextIds = CardZoneIdentitySet(zones);
        var removed = removedCardInstanceIds.ToArray();
        if (removed.Distinct().Count() != removed.Length ||
            removed.Any(id => !currentIds.Contains(id)) ||
            removed.Intersect(inserted).Any())
            throw new InvalidDataException(
                "Draw-pile insertion has an invalid declared identity removal.");
        var expectedIds = currentIds
            .Except(removed)
            .Concat(inserted.Where(id => !currentIds.Contains(id)))
            .ToHashSet();
        if (!expectedIds.SetEquals(nextIds))
            throw new InvalidDataException(
                "Draw-pile insertion transition changed undeclared card identities.");

        ValidateSynchronized(combat, requireIdle: true);
        var semantic = combat.SemanticDeck;
        if (semantic is not null)
        {
            var topOffset = combat.DrawPile.Length - physicalIndex;
            foreach (var cardInstanceId in inserted)
                semantic = SemanticDeckEngineV111.InsertKnown(
                    semantic, topOffset, cardInstanceId, publiclyKnown);
        }
        return CommitZoneTransition(combat, zones, semantic);
    }

    /// <summary>
    /// Commits removal of known physical identities from DrawPile together
    /// with destination-zone changes, while applying the same removal to the
    /// active provider's hidden-order constraints. Identities must move to
    /// another zone unless explicitly declared removed from combat.
    /// </summary>
    public static CombatState ApplyDrawPileRemoval(
        CombatState combat,
        IReadOnlyCollection<long> removedCardInstanceIds,
        CombatCardZonesV111 nextCardZones,
        IReadOnlyCollection<long>? removedFromCombatInstanceIds = null)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(removedCardInstanceIds);
        ArgumentNullException.ThrowIfNull(nextCardZones);
        removedFromCombatInstanceIds ??= Array.Empty<long>();
        var removed = removedCardInstanceIds.ToArray();
        if (removed.Distinct().Count() != removed.Length ||
            removed.Any(id => !combat.DrawPile.Contains(id)))
            throw new InvalidDataException(
                "Draw-pile removal contains duplicate or missing identities.");
        var removedSet = removed.ToHashSet();
        var expectedDrawPile = combat.DrawPile
            .Where(id => !removedSet.Contains(id)).ToArray();
        if (!nextCardZones.DrawPile.SequenceEqual(expectedDrawPile))
            throw new InvalidDataException(
                "Draw-pile removal does not preserve the remaining physical order.");
        var currentIds = CardZoneIdentitySet(combat.Mechanics.CardZones);
        var nextIds = CardZoneIdentitySet(nextCardZones);
        var removedFromCombat = removedFromCombatInstanceIds.ToArray();
        if (removedFromCombat.Distinct().Count() != removedFromCombat.Length ||
            removedFromCombat.Any(id => !removedSet.Contains(id)))
            throw new InvalidDataException(
                "Draw-pile removal has an invalid declared combat identity removal.");
        var expectedIds = currentIds.Except(removedFromCombat).ToHashSet();
        if (!expectedIds.SetEquals(nextIds))
            throw new InvalidDataException(
                "Draw-pile removal transition changed undeclared card identities.");
        if (removed.Length == 0)
            return ApplyZoneTransition(combat, nextCardZones);

        var semantic = SynchronizeSemanticDrawPileAfterRemoval(
            combat, removed);
        return CommitZoneTransition(
            combat, nextCardZones, semantic);
    }

    /// <summary>
    /// Commits replacement of one physical draw-pile identity at a known
    /// bottom-first index together with the provider's corresponding identity
    /// substitution and any other affected zones.
    /// </summary>
    public static CombatState ApplyDrawPileReplacement(
        CombatState combat,
        int physicalIndex,
        long replacedCardInstanceId,
        long replacementCardInstanceId,
        CombatCardZonesV111 nextCardZones)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(nextCardZones);
        if (physicalIndex < 0 || physicalIndex >= combat.DrawPile.Length ||
            combat.DrawPile[physicalIndex] != replacedCardInstanceId)
            throw new InvalidDataException(
                "Draw-pile replacement source does not match its physical index.");
        if (replacedCardInstanceId == replacementCardInstanceId)
            throw new InvalidDataException(
                "Draw-pile replacement must use a different identity.");

        var expectedDrawPile = (long[])combat.DrawPile.Clone();
        expectedDrawPile[physicalIndex] = replacementCardInstanceId;
        if (!nextCardZones.DrawPile.SequenceEqual(expectedDrawPile))
            throw new InvalidDataException(
                "Draw-pile replacement does not preserve surrounding physical order.");
        if (Enum.GetValues<PrototypeCardZone>()
            .Where(zone => zone != PrototypeCardZone.DrawPile)
            .Any(zone => nextCardZones.CardsIn(zone)
                .Contains(replacementCardInstanceId)))
            throw new InvalidDataException(
                "Draw-pile replacement duplicates its new identity in another zone.");

        var currentIds = CardZoneIdentitySet(combat.Mechanics.CardZones);
        var nextIds = CardZoneIdentitySet(nextCardZones);
        var expectedIds = currentIds.ToHashSet();
        if (!expectedIds.Remove(replacedCardInstanceId) ||
            !expectedIds.Add(replacementCardInstanceId) ||
            !expectedIds.SetEquals(nextIds))
            throw new InvalidDataException(
                "Draw-pile replacement changed undeclared card identities.");

        var semantic = ReplaceSemanticDrawPileInstance(
            combat, replacedCardInstanceId, replacementCardInstanceId,
            physicalIndex);
        return CommitZoneTransition(
            combat, nextCardZones, semantic);
    }

    /// <summary>
    /// Commits a complete draw-pile shuffle together with the identities
    /// moved into the pile, provider order, and every affected card zone.
    /// The shuffled result must be a permutation of the existing draw pile
    /// and incoming identities; callers own the random draw and source hooks.
    /// </summary>
    internal static CombatState ApplyDrawPileShuffle(
        CombatState combat,
        IReadOnlyCollection<long> shuffledPhysicalDrawPile,
        IReadOnlyCollection<long> movedIntoDrawPile,
        CombatCardZonesV111 nextCardZones,
        SemanticDeckStateV111? committedSemanticDeck)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(shuffledPhysicalDrawPile);
        ArgumentNullException.ThrowIfNull(movedIntoDrawPile);
        ArgumentNullException.ThrowIfNull(nextCardZones);
        ValidateSynchronized(combat, requireIdle: true);

        var shuffled = shuffledPhysicalDrawPile.ToArray();
        var moved = movedIntoDrawPile.ToArray();
        if (moved.Distinct().Count() != moved.Length ||
            shuffled.Distinct().Count() != shuffled.Length ||
            moved.Any(id => combat.DrawPile.Contains(id)) ||
            moved.Any(id => !Enum.GetValues<PrototypeCardZone>()
                .Where(zone => zone != PrototypeCardZone.DrawPile)
                .Any(zone => combat.Mechanics.CardZones.CardsIn(zone)
                    .Contains(id))))
            throw new InvalidDataException(
                "Draw-pile shuffle has duplicate or unavailable incoming identities.");

        var expectedIdentities = combat.DrawPile.Concat(moved).Order().ToArray();
        if (!shuffled.Order().SequenceEqual(expectedIdentities) ||
            !nextCardZones.DrawPile.SequenceEqual(shuffled))
            throw new InvalidDataException(
                "Draw-pile shuffle does not preserve its complete identity set and order.");

        return ApplyCommittedZoneTransition(
            combat, nextCardZones, committedSemanticDeck);
    }

    /// <summary>
    /// Commits a hand-return shuffle as one provider and zone transition.
    /// The supplied physical order is already resolved by the caller; this
    /// boundary validates that the incoming identities are exactly the
    /// current hand, commits the matching provider order, and empties Hand.
    /// </summary>
    internal static CombatState CommitHandReturnShuffle(
        CombatState combat,
        IReadOnlyList<long> shuffledPhysicalDrawPile,
        IReadOnlyCollection<long> returnedHandCardInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(shuffledPhysicalDrawPile);
        ArgumentNullException.ThrowIfNull(returnedHandCardInstanceIds);
        ValidateSynchronized(combat, requireIdle: true);

        var shuffled = shuffledPhysicalDrawPile.ToArray();
        var returned = returnedHandCardInstanceIds.ToArray();
        if (!returned.SequenceEqual(combat.Hand) ||
            returned.Distinct().Count() != returned.Length)
            throw new InvalidDataException(
                "Hand-return shuffle identities differ from the committed hand.");

        var semantic = CommitHandReturnSemanticShuffle(
            combat.SemanticDeck, shuffled, returned);
        var nextZones = combat.Mechanics.CardZones.WithZoneChanges(
            new Dictionary<PrototypeCardZone, long[]>
            {
                [PrototypeCardZone.DrawPile] = shuffled,
                [PrototypeCardZone.Hand] = []
            });
        return ApplyDrawPileShuffle(
            combat, shuffled, returned, nextZones, semantic);
    }

    /// <summary>
    /// Publishes a physical mutation whose provider-side transition was
    /// already committed by a typed semantic operation. Correspondence is
    /// validated before the combined state is returned.
    /// </summary>
    public static CombatState ApplyCommittedPhysicalMutation(
        CombatState combat, long[] nextPhysicalDrawPile,
        SemanticDeckStateV111? committedSemanticDeck)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(nextPhysicalDrawPile);
        if (combat.SemanticDeck is not null &&
            committedSemanticDeck is null)
            throw new InvalidDataException(
                "A physical draw-pile mutation cannot drop its provider state.");
        if (nextPhysicalDrawPile.Distinct().Count() !=
            nextPhysicalDrawPile.Length)
            throw new InvalidDataException(
                "Physical draw-pile mutation contains duplicate card instances.");
        var nextZones = combat.Mechanics.CardZones.WithCardsIn(
            PrototypeCardZone.DrawPile,
            (long[])nextPhysicalDrawPile.Clone());
        return ApplyCommittedZoneTransition(
            combat, nextZones, committedSemanticDeck);
    }

    /// <summary>
    /// Publishes a complete identity-preserving zone transition whose
    /// provider-side draw-pile operation has already been committed by a
    /// typed operation. Identity additions, removals, and replacements use
    /// their dedicated typed methods.
    /// </summary>
    public static CombatState ApplyCommittedZoneTransition(
        CombatState combat,
        CombatCardZonesV111 nextCardZones,
        SemanticDeckStateV111? committedSemanticDeck)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(nextCardZones);
        if (combat.SemanticDeck is not null &&
            committedSemanticDeck is null)
            throw new InvalidDataException(
                "A card-zone mutation cannot drop its provider state.");
        if (!CardZoneIdentitySet(combat.Mechanics.CardZones)
            .SetEquals(CardZoneIdentitySet(nextCardZones)))
            throw new InvalidDataException(
                "Committed zone transition must preserve every physical card identity.");
        return CommitZoneTransition(
            combat, nextCardZones, committedSemanticDeck);
    }

    private static CombatState CommitZoneTransition(
        CombatState combat,
        CombatCardZonesV111 nextCardZones,
        SemanticDeckStateV111? committedSemanticDeck)
    {
        var zoneLocations = Enum.GetValues<PrototypeCardZone>()
            .SelectMany(zone => nextCardZones.CardsIn(zone))
            .ToArray();
        if (zoneLocations.Distinct().Count() != zoneLocations.Length)
            throw new InvalidDataException(
                "Card-zone transition duplicates a physical card instance.");

        var next = combat with
        {
            Mechanics = combat.Mechanics with
            {
                CardZones = nextCardZones
            },
            SemanticDeck = committedSemanticDeck
        };
        ValidateSynchronized(next);
        return next;
    }

    internal static SemanticDeckStateV111? SynchronizeSemanticDrawPileAfterMutation(
        CombatState combat, long[] nextPhysicalDrawPile)
    {
        var semantic = combat.SemanticDeck;
        if (semantic is null) return null;
        SemanticDeckEngineV111.Validate(semantic);
        if (semantic.Pending is not null)
            throw new InvalidDataException(
                "Cannot mutate a draw pile with a pending semantic reveal.");
        if (nextPhysicalDrawPile.Distinct().Count() !=
            nextPhysicalDrawPile.Length)
            throw new InvalidDataException(
                "Semantic draw-pile mutation produced duplicate instances.");

        var semanticIds = SemanticDeckEngineV111.AllCardInstanceIds(semantic);
        var oldIds = combat.DrawPile;
        var oldMembersMatch = semanticIds.Order().SequenceEqual(oldIds.Order());
        var nextMembersMatch = semanticIds.Order().SequenceEqual(
            nextPhysicalDrawPile.Order());

        if (semantic.Mode == SemanticDeckStateV111.NativeMode)
        {
            var currentTopFirst = semantic.Slots
                .Select(slot => slot.KnownCardInstanceId!.Value).ToArray();
            var oldOrderMatches = currentTopFirst.SequenceEqual(
                oldIds.Reverse());
            if (nextMembersMatch && currentTopFirst.SequenceEqual(
                    nextPhysicalDrawPile.Reverse()))
                return semantic;
            if (!oldMembersMatch || !oldOrderMatches)
                throw new InvalidDataException(
                    "Native semantic draw pile is stale before a physical mutation.");

            var publiclyRevealed = semantic.Slots
                .Where(slot => slot.PubliclyRevealed)
                .Select(slot => slot.KnownCardInstanceId!.Value)
                .ToHashSet();
            var synchronized = new SemanticDeckStateV111(
                SemanticDeckStateV111.NativeMode,
                nextPhysicalDrawPile.Reverse().Select(id =>
                    SemanticDeckSlotV111.Known(
                        id, publiclyRevealed.Contains(id))).ToArray(),
                [], semantic.ShuffleEpoch, Transcript: []);
            SemanticDeckEngineV111.Validate(synchronized);
            return synchronized;
        }

        // Nature transitions may already have updated the provider before
        // the physical zone array is assigned (card selection is one such
        // path). Accept that state only when its instance membership equals
        // the destination pile.
        if (nextMembersMatch && !oldMembersMatch)
            return semantic;
        if (!oldMembersMatch)
            throw new InvalidDataException(
                "Semantic draw pile is stale before a physical mutation.");

        if (IsOrderedSubsequence(nextPhysicalDrawPile, oldIds))
        {
            var nextSet = nextPhysicalDrawPile.ToHashSet();
            var removed = oldIds.Where(id => !nextSet.Contains(id)).ToArray();
            semantic = SemanticDeckEngineV111.RemoveCardInstances(
                semantic, removed);
        }
        else if (nextPhysicalDrawPile.Length >= oldIds.Length &&
                 nextPhysicalDrawPile.Take(oldIds.Length).SequenceEqual(oldIds))
        {
            // DrawPile is bottom-first, so appending physical entries places
            // each card at the semantic top. Applying PlaceTop in append
            // order yields the reversed top-first prefix.
            foreach (var id in nextPhysicalDrawPile.Skip(oldIds.Length))
                semantic = SemanticDeckEngineV111.PlaceTop(semantic, id);
        }
        else if (!oldIds.SequenceEqual(nextPhysicalDrawPile))
        {
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw_pile.zone_move " +
                "operation=unmodeled_hidden_order_change");
        }

        if (!SemanticDeckEngineV111.AllCardInstanceIds(semantic).Order()
                .SequenceEqual(nextPhysicalDrawPile.Order()))
            throw new InvalidDataException(
                "Semantic draw-pile zone transition produced stale membership.");
        return semantic;
    }

    internal static SemanticDeckStateV111? SynchronizeSemanticDrawPileAfterRemoval(
        CombatState combat, IReadOnlyCollection<long> removedCardInstanceIds)
    {
        var semantic = combat.SemanticDeck;
        if (semantic is null) return null;
        ValidateSynchronized(combat, requireIdle: true);
        var removed = removedCardInstanceIds.ToArray();
        if (removed.Distinct().Count() != removed.Length ||
            removed.Any(id => !combat.DrawPile.Contains(id)))
            throw new InvalidDataException(
                "Semantic draw-pile removal contains a missing card instance.");
        semantic = SemanticDeckEngineV111.RemoveCardInstances(
            semantic, removed);
        var nextIds = combat.DrawPile.Where(id => !removed.Contains(id));
        if (!SemanticDeckEngineV111.AllCardInstanceIds(semantic).Order()
                .SequenceEqual(nextIds.Order()))
            throw new InvalidDataException(
                "Semantic draw-pile removal produced stale membership.");
        return semantic;
    }

    internal static SemanticDeckStateV111? SynchronizeSemanticDrawPileAfterInsertion(
        CombatState combat, int physicalIndex, long insertedCardInstanceId,
        bool publiclyKnown = false)
    {
        var semantic = combat.SemanticDeck;
        if (semantic is null) return null;
        ValidateSynchronized(combat, requireIdle: true);
        if (physicalIndex < 0 || physicalIndex > combat.DrawPile.Length)
            throw new InvalidDataException(
                "Semantic draw pile is stale before inserting a card instance.");

        // The source index addresses the physical bottom-first array. The
        // provider deck uses top-first offsets, so invert the index.
        return SemanticDeckEngineV111.InsertKnown(
            semantic, combat.DrawPile.Length - physicalIndex,
            insertedCardInstanceId, publiclyKnown);
    }

    internal static SemanticDeckStateV111? ReplaceSemanticDrawPileInstance(
        CombatState combat, long replacedCardInstanceId,
        long replacementCardInstanceId, int physicalIndex)
    {
        var semantic = combat.SemanticDeck;
        if (semantic is null) return null;
        ValidateSynchronized(combat, requireIdle: true);
        if (physicalIndex < 0 || physicalIndex >= combat.DrawPile.Length ||
            combat.DrawPile[physicalIndex] != replacedCardInstanceId)
            throw new InvalidDataException(
                "Semantic draw pile is stale before replacing a card instance.");
        semantic = SemanticDeckEngineV111.RemoveCardInstance(
            semantic, replacedCardInstanceId);
        return SemanticDeckEngineV111.InsertKnown(
            semantic, combat.DrawPile.Length - 1 - physicalIndex,
            replacementCardInstanceId, publiclyKnown: false);
    }

    private static SemanticDeckStateV111? CommitHandReturnSemanticShuffle(
        SemanticDeckStateV111? semantic,
        IReadOnlyList<long> shuffledPhysicalDrawPile,
        IReadOnlyCollection<long> returnedHandCardInstanceIds)
    {
        if (semantic is null) return null;
        if (semantic.Mode is not (SemanticDeckStateV111.NativeMode or
            SemanticDeckStateV111.NatureMode))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.hand_return.draw_shuffle " +
                "operation=unsupported_provider_mode");
        var existing = SemanticDeckEngineV111.AllCardInstanceIds(semantic)
            .ToHashSet();
        var returned = returnedHandCardInstanceIds.ToArray();
        if (returned.Distinct().Count() != returned.Length ||
            returned.Any(id => existing
                .Contains(id)))
            throw new InvalidDataException(
                "Hand-return shuffle received duplicate incoming identities.");
        foreach (var id in returned)
            semantic = SemanticDeckEngineV111.PlaceBottom(
                semantic, id, publiclyKnown: false);
        var topFirst = shuffledPhysicalDrawPile.Reverse().ToArray();
        return semantic.Mode switch
        {
            SemanticDeckStateV111.NativeMode =>
                SemanticDeckEngineV111.CommitNativeShuffle(
                    semantic, topFirst),
            SemanticDeckStateV111.NatureMode =>
                SemanticDeckEngineV111.CommitControlledNatureShuffle(
                    semantic, topFirst),
            _ => throw new InvalidDataException(
                "Hand-return shuffle provider mode changed during commit.")
        };
    }

    internal static bool IsOrderedSubsequence(
        IReadOnlyList<long> candidate, IReadOnlyList<long> source)
    {
        var index = 0;
        foreach (var id in source)
            if (index < candidate.Count && candidate[index] == id)
                index++;
        return index == candidate.Count;
    }

}
