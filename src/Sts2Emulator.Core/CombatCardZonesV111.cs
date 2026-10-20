namespace Sts2Emulator.Core;

/// <summary>
/// Single mechanics owner for combat card-zone membership. CombatState keeps
/// its historical flattened fields as defensive array projections. Zone arrays
/// are copied on construction and replacement, and getters never expose the
/// arrays stored by this owner.
/// </summary>
public sealed record CombatCardZonesV111
{
    private long[] _hand;
    private long[] _drawPile;
    private long[] _discardPile;
    private long[] _exhaustPile;
    private long[]? _playPile;
    private long[]? _choicePool;

    public CombatCardZonesV111(
        long[] Hand,
        long[] DrawPile,
        long[] DiscardPile,
        long[] ExhaustPile,
        long[]? PlayPile = null,
        long[]? ChoicePool = null)
    {
        _hand = CloneRequired(Hand, nameof(Hand));
        _drawPile = CloneRequired(DrawPile, nameof(DrawPile));
        _discardPile = CloneRequired(DiscardPile, nameof(DiscardPile));
        _exhaustPile = CloneRequired(ExhaustPile, nameof(ExhaustPile));
        _playPile = CloneOptional(PlayPile);
        _choicePool = CloneOptional(ChoicePool);
    }

    public long[] Hand
    {
        get => (long[])_hand.Clone();
        init => _hand = CloneRequired(value, nameof(Hand));
    }

    public long[] DrawPile
    {
        get => (long[])_drawPile.Clone();
        init => _drawPile = CloneRequired(value, nameof(DrawPile));
    }

    public long[] DiscardPile
    {
        get => (long[])_discardPile.Clone();
        init => _discardPile = CloneRequired(value, nameof(DiscardPile));
    }

    public long[] ExhaustPile
    {
        get => (long[])_exhaustPile.Clone();
        init => _exhaustPile = CloneRequired(value, nameof(ExhaustPile));
    }

    public long[]? PlayPile
    {
        get => CloneOptional(_playPile);
        init => _playPile = CloneOptional(value);
    }

    public long[]? ChoicePool
    {
        get => CloneOptional(_choicePool);
        init => _choicePool = CloneOptional(value);
    }

    public long[] CardsIn(PrototypeCardZone zone) => zone switch
    {
        PrototypeCardZone.Hand => Hand,
        PrototypeCardZone.DrawPile => DrawPile,
        PrototypeCardZone.DiscardPile => DiscardPile,
        PrototypeCardZone.ExhaustPile => ExhaustPile,
        PrototypeCardZone.PlayPile => PlayPile ?? Array.Empty<long>(),
        PrototypeCardZone.ChoicePool => ChoicePool ?? Array.Empty<long>(),
        _ => throw new ArgumentOutOfRangeException(nameof(zone))
    };

    public CombatCardZonesV111 WithCardsIn(
        PrototypeCardZone zone, long[] cardInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(cardInstanceIds);
        return zone switch
        {
            PrototypeCardZone.Hand => this with { Hand = cardInstanceIds },
            PrototypeCardZone.DrawPile => this with { DrawPile = cardInstanceIds },
            PrototypeCardZone.DiscardPile => this with
                { DiscardPile = cardInstanceIds },
            PrototypeCardZone.ExhaustPile => this with
                { ExhaustPile = cardInstanceIds },
            PrototypeCardZone.PlayPile => this with { PlayPile = cardInstanceIds },
            PrototypeCardZone.ChoicePool => this with
                { ChoicePool = cardInstanceIds },
            _ => throw new ArgumentOutOfRangeException(nameof(zone))
        };
    }

    public CombatCardZonesV111 WithZoneChanges(
        IReadOnlyDictionary<PrototypeCardZone, long[]> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var next = this;
        foreach (var (zone, cardInstanceIds) in changes)
            next = next.WithCardsIn(zone, cardInstanceIds);
        return next;
    }

    public CombatCardZonesV111 MoveCards(
        PrototypeCardZone sourceZone,
        PrototypeCardZone destinationZone,
        IReadOnlyCollection<long> cardInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(cardInstanceIds);
        if (sourceZone == destinationZone)
            throw new ArgumentException(
                "Card movement requires distinct source and destination zones.");

        var moved = cardInstanceIds.ToArray();
        if (moved.Distinct().Count() != moved.Length)
            throw new InvalidDataException(
                "Card movement contains duplicate instance identities.");

        var source = CardsIn(sourceZone);
        var sourceSet = source.ToHashSet();
        if (moved.Any(id => !sourceSet.Contains(id)))
            throw new InvalidOperationException(
                "Card movement source no longer contains every requested identity.");

        var destination = CardsIn(destinationZone);
        var destinationSet = destination.ToHashSet();
        if (moved.Any(destinationSet.Contains))
            throw new InvalidDataException(
                "Card movement would duplicate an identity in its destination zone.");

        return WithCardsIn(sourceZone,
                source.Where(id => !moved.Contains(id)).ToArray())
            .WithCardsIn(destinationZone, destination.Concat(moved).ToArray());
    }

    public CombatCardZonesV111 Fork() => new(
        _hand, _drawPile, _discardPile, _exhaustPile, _playPile, _choicePool);

    public void Deconstruct(
        out long[] Hand,
        out long[] DrawPile,
        out long[] DiscardPile,
        out long[] ExhaustPile,
        out long[]? PlayPile,
        out long[]? ChoicePool)
    {
        Hand = this.Hand;
        DrawPile = this.DrawPile;
        DiscardPile = this.DiscardPile;
        ExhaustPile = this.ExhaustPile;
        PlayPile = this.PlayPile;
        ChoicePool = this.ChoicePool;
    }

    private static long[] CloneRequired(long[] cards, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(cards, parameterName);
        return (long[])cards.Clone();
    }

    private static long[]? CloneOptional(long[]? cards) =>
        cards is null ? null : (long[])cards.Clone();
}
