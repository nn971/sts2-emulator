namespace Sts2Emulator.Core;

/// <summary>
/// Single mechanics owner for combat card-zone membership. CombatState keeps
/// its historical flattened fields as projections of this value.
/// </summary>
public sealed record CombatCardZonesV111(
    long[] Hand,
    long[] DrawPile,
    long[] DiscardPile,
    long[] ExhaustPile,
    long[]? PlayPile = null,
    long[]? ChoicePool = null)
{
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

    public CombatCardZonesV111 Fork() => this with
    {
        Hand = (long[])Hand.Clone(),
        DrawPile = (long[])DrawPile.Clone(),
        DiscardPile = (long[])DiscardPile.Clone(),
        ExhaustPile = (long[])ExhaustPile.Clone(),
        PlayPile = PlayPile is null ? null : (long[])PlayPile.Clone(),
        ChoicePool = ChoicePool is null ? null : (long[])ChoicePool.Clone()
    };
}
