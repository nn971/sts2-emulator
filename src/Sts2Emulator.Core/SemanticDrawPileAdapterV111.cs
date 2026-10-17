namespace Sts2Emulator.Core;

internal readonly record struct SemanticDrawPileTopCardV111(
    SemanticDeckStateV111 Deck, long? CardInstanceId)
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
    /// Resolves the current top card from the shared provider/physical-pile
    /// contract. A Nature-owned unknown position returns its pending reveal;
    /// a known position is consumed from the provider state and returned by
    /// stable physical identity. The caller still runs its own draw hooks.
    /// </summary>
    internal static SemanticDrawPileTopCardV111 ResolveTopCard(
        SemanticDeckStateV111 semantic, IReadOnlyList<long> physicalDrawPile)
    {
        ArgumentNullException.ThrowIfNull(semantic);
        ArgumentNullException.ThrowIfNull(physicalDrawPile);
        ValidateProviderCorrespondence(semantic, physicalDrawPile);
        if (physicalDrawPile.Count == 0)
            throw new InvalidOperationException(
                "Cannot resolve the top of an empty semantic draw pile.");

        var required = SemanticDeckEngineV111.Require(semantic, 0, "draw");
        if (required.Pending is not null)
            return new SemanticDrawPileTopCardV111(required, null);

        var (consumed, cardInstanceId) =
            SemanticDeckEngineV111.DrawKnown(required);
        if (semantic.Mode == SemanticDeckStateV111.NativeMode &&
            cardInstanceId != physicalDrawPile[^1])
            throw new InvalidDataException(
                "Native semantic draw disagrees with the physical top card.");
        return new SemanticDrawPileTopCardV111(consumed, cardInstanceId);
    }

    /// <summary>
    /// Atomically transfers an exhausted draw pile's discard identities into
    /// a new unresolved Nature shuffle epoch. Callers retain ownership of
    /// shuffle hooks and any selected physical ordering used by their frame.
    /// </summary>
    internal static CombatState CommitNatureRecycle(
        CombatState combat,
        SemanticDeckStateV111 semantic,
        IReadOnlyList<long> currentPhysicalDrawPile,
        IReadOnlyList<long> currentDiscardPile,
        IReadOnlyCollection<long> recycledCardInstanceIds,
        IReadOnlyList<long> nextPhysicalDrawPile,
        IReadOnlyCollection<long> perfectFitTopCardInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(semantic);
        ArgumentNullException.ThrowIfNull(currentPhysicalDrawPile);
        ArgumentNullException.ThrowIfNull(currentDiscardPile);
        ArgumentNullException.ThrowIfNull(recycledCardInstanceIds);
        ArgumentNullException.ThrowIfNull(nextPhysicalDrawPile);
        ArgumentNullException.ThrowIfNull(perfectFitTopCardInstanceIds);
        var checkpoint = combat with
        {
            DrawPile = currentPhysicalDrawPile.ToArray(),
            DiscardPile = currentDiscardPile.ToArray(),
            SemanticDeck = semantic
        };
        ValidateSynchronized(checkpoint, requireIdle: true);
        if (semantic.Mode != SemanticDeckStateV111.NatureMode ||
            semantic.Count != 0 || currentPhysicalDrawPile.Count != 0)
            throw new InvalidDataException(
                "Nature recycle requires an exhausted synchronized draw pile.");

        var recycled = recycledCardInstanceIds.ToArray();
        var nextPhysical = nextPhysicalDrawPile.ToArray();
        var perfectFit = perfectFitTopCardInstanceIds.ToArray();
        var recycledSet = recycled.ToHashSet();
        if (recycled.Length == 0 ||
            recycled.Distinct().Count() != recycled.Length ||
            nextPhysical.Distinct().Count() != nextPhysical.Length ||
            perfectFit.Distinct().Count() != perfectFit.Length ||
            !combat.DiscardPile.Order().SequenceEqual(
                currentDiscardPile.Order()) ||
            !currentDiscardPile.Order().SequenceEqual(recycled.Order()) ||
            !recycled.Order().SequenceEqual(nextPhysical.Order()) ||
            perfectFit.Any(id => !recycledSet.Contains(id)))
            throw new InvalidDataException(
                "Nature recycle physical zones do not preserve card identities.");

        var nextDeck = SemanticDeckEngineV111.RecycleNature(
            semantic, recycled, perfectFit);
        var nextZones = checkpoint.Mechanics.CardZones.WithZoneChanges(
            new Dictionary<PrototypeCardZone, long[]>
            {
                [PrototypeCardZone.DrawPile] = nextPhysical,
                [PrototypeCardZone.DiscardPile] = []
            });
        return ApplyCommittedZoneTransition(
            checkpoint, nextZones, nextDeck);
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

    /// <summary>
    /// Applies a physical draw-pile mutation and commits its corresponding
    /// provider transition before returning the new immutable combat state.
    /// </summary>
    public static CombatState ApplyPhysicalMutation(
        CombatState combat, long[] nextPhysicalDrawPile)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(nextPhysicalDrawPile);
        return ApplyZoneTransition(
            combat,
            combat.Mechanics.CardZones.WithCardsIn(
                PrototypeCardZone.DrawPile,
                (long[])nextPhysicalDrawPile.Clone()));
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
        var semantic = combat.DrawPile.SequenceEqual(nextCardZones.DrawPile)
            ? combat.SemanticDeck
            : SynchronizeSemanticDrawPileAfterMutation(
                combat, nextCardZones.DrawPile);
        return CommitZoneTransition(combat, nextCardZones, semantic);
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
    /// Publishes a complete zone transition whose provider-side draw-pile
    /// operation has already been committed by a typed operation.
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
        CombatState combat, int physicalIndex, long insertedCardInstanceId)
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
            insertedCardInstanceId, publiclyKnown: false);
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

    internal static SemanticDeckStateV111? CommitSemanticDrawPileShuffle(
        CombatState combat, IReadOnlyCollection<long> shuffledPhysicalDrawPile,
        IReadOnlyCollection<long> cardsMovedIntoDrawPile)
    {
        var semantic = combat.SemanticDeck;
        if (semantic is null) return null;
        ValidateSynchronized(combat, requireIdle: true);
        if (semantic.Mode != SemanticDeckStateV111.NativeMode)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.hand_return.draw_shuffle " +
                "operation=requires_native_exact_deck");
        var added = cardsMovedIntoDrawPile.ToArray();
        if (added.Distinct().Count() != added.Length ||
            added.Any(id => SemanticDeckEngineV111.AllCardInstanceIds(semantic)
                .Contains(id)))
            throw new InvalidDataException(
                "Native draw-pile shuffle received duplicate incoming cards.");
        foreach (var id in added)
            semantic = SemanticDeckEngineV111.PlaceBottom(
                semantic, id, publiclyKnown: false);
        return SemanticDeckEngineV111.CommitNativeShuffle(
            semantic, shuffledPhysicalDrawPile.Reverse().ToArray());
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
