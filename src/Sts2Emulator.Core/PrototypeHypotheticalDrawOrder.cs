namespace Sts2Emulator.Core;

/// <summary>
/// One publicly nameable draw-pile card variant. Instance IDs, private pile
/// order, hidden RNG streams and exact-state hashes do not cross this contract.
/// </summary>
public sealed record PrototypePublicDrawCard(string CardId, int UpgradeLevel);

/// <summary>
/// A narrow mechanic-level action on a hypothetical, previously seeded state.
/// This is NOT by itself a posterior sampler: the caller must establish that
/// the source state was generated independently and that its complete public
/// transcript is consistent with the player's observations.
///
/// DrawCards consumes from the end of DrawPile. Requested order is first-drawn
/// first; no RNG stream is advanced by this operation. Other future chance
/// events still depend on the source hypothetical state's RNG cursors.
/// </summary>
public static class PrototypeHypotheticalDrawOrder
{
    public const string SchemaId = "prototype-hypothetical-draw-order-v1";

    public static RunState Apply(
        RunState hypothetical,
        IReadOnlyList<PrototypePublicDrawCard> orderedCards)
    {
        if (orderedCards is null)
        {
            throw new ArgumentNullException(nameof(orderedCards));
        }

        var combat = hypothetical.World?.Combat
            ?? throw new InvalidOperationException(
                "Draw-order conditioning requires an active combat.");
        if (hypothetical.Phase != RunPhase.Combat
            || combat.Turn != 1
            || combat.PendingChoice is not null
            || combat.DiscardPile.Length != 0
            || combat.ExhaustPile.Length != 0
            || combat.PlayCardIds.Length != 0
            || combat.Hand.Length + combat.DrawPile.Length
                != hypothetical.Player.Deck.Length)
        {
            throw new InvalidOperationException(
                "Unsupported combat state: require a certified, unmodified "
                + "first-turn opening with a complete persistent deck.");
        }

        var byInstance = combat.Cards.ToDictionary(card => card.InstanceId);
        var available = new Dictionary<(string CardId, int Upgrade), Queue<long>>();

        foreach (var id in combat.DrawPile.Order())
        {
            if (!byInstance.TryGetValue(id, out var card)
                || card.IsTemporary
                || card.Enchantment is not null
                || card.State.ValueKind != System.Text.Json.JsonValueKind.Object
                || card.State.EnumerateObject().Any())
            {
                throw new InvalidOperationException(
                    "Draw pile contains an unknown/generated/mutated card variant.");
            }

            var key = (card.CardId, card.UpgradeLevel);
            if (!available.TryGetValue(key, out var queue))
            {
                queue = new Queue<long>();
                available.Add(key, queue);
            }
            queue.Enqueue(id);
        }

        // Verify the complete persistent deck multiset against both visible
        // hand and hidden draw pile. This prevents silently omitting a card.
        var publicDeck = new Dictionary<(string CardId, int Upgrade), int>();
        foreach (var card in hypothetical.Player.Deck)
        {
            var key = (card.CardId, card.UpgradeLevel);
            publicDeck[key] = publicDeck.GetValueOrDefault(key) + 1;
        }

        foreach (var id in combat.Hand.Concat(combat.DrawPile))
        {
            if (!byInstance.TryGetValue(id, out var card))
            {
                throw new InvalidOperationException("Combat card instance is missing.");
            }
            var key = (card.CardId, card.UpgradeLevel);
            if (!publicDeck.TryGetValue(key, out var count) || count == 0)
            {
                throw new InvalidOperationException(
                    "Combat inventory differs from persistent deck.");
            }
            publicDeck[key] = count - 1;
        }

        if (publicDeck.Values.Any(count => count != 0))
        {
            throw new InvalidOperationException(
                "Combat inventory does not account for the persistent deck.");
        }

        if (orderedCards.Count != combat.DrawPile.Length)
        {
            throw new ArgumentException(
                "Requested draw order must contain every remaining card.",
                nameof(orderedCards));
        }

        var firstDrawnFirst = new long[orderedCards.Count];
        for (var i = 0; i < orderedCards.Count; i++)
        {
            var item = orderedCards[i];
            if (item is null || string.IsNullOrEmpty(item.CardId)
                || item.UpgradeLevel < 0
                || !available.TryGetValue((item.CardId, item.UpgradeLevel), out var queue)
                || queue.Count == 0)
            {
                throw new ArgumentException(
                    "Requested draw order is not a permutation of certified "
                    + "hidden card variants.",
                    nameof(orderedCards));
            }
            firstDrawnFirst[i] = queue.Dequeue();
        }

        // Never mutate the input, and do not consume/change any RNG streams.
        var branch = hypothetical.Fork();
        var world = branch.World!;
        var nextCombat = world.Combat! with
        {
            DrawPile = firstDrawnFirst.Reverse().ToArray()
        };
        return branch with { World = world with { Combat = nextCombat } };
    }
}
