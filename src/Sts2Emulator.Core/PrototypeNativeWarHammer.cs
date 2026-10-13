namespace Sts2Emulator.Core;

/// <summary>
/// Persistent deck callback for Glory's War Hammer Ancient relic.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal static RunState ApplyWarHammerEliteVictory(
        RunState state,
        PrototypeRoomType? roomType)
    {
        if (roomType != PrototypeRoomType.Elite)
            return state;

        var hammers = state.Player.ActiveRelics.Count(relic =>
            relic.RelicId == NativeGloryAncientsV111.WarHammerId);
        for (var hammer = 0; hammer < hammers; hammer++)
        {
            var candidates = state.Player.Deck
                .Where(card => CanSelectEventDeckCard(
                    card,
                    PrototypePersistentDeckChoiceKind.Upgrade,
                    null,
                    null,
                    false))
                // Native StableShuffle sorts by ModelId before shuffling.
                // Flat prototype IDs use their canonical text plus instance
                // ID as a deterministic proxy for that model ordering.
                .OrderBy(card => card.CardId, StringComparer.Ordinal)
                .ThenBy(card => card.InstanceId)
                .ToList();
            if (candidates.Count == 0)
                continue;

            if (state.NativeRunRng is not null)
            {
                // Native War Hammer uses the persistent run.niche stream.
                // Keep the run and active-combat views synchronized per draw.
                for (var last = candidates.Count - 1; last > 0; last--)
                {
                    state = NativeRunRngV111.SampleNextInt(
                        state,
                        "run.niche",
                        last + 1,
                        out var chosen);
                    (candidates[last], candidates[chosen]) =
                        (candidates[chosen], candidates[last]);
                }
            }
            else
            {
                // Prototype runs do not own run.niche. Keep their test path
                // on the legacy event stream without claiming native parity.
                var shuffled = candidates.ToArray();
                PrototypeRng.Shuffle(state.Rng, "event", shuffled);
                candidates = shuffled.ToList();
            }

            var upgradedIds = candidates.Take(4)
                .Select(card => card.InstanceId)
                .ToHashSet();
            state = state with
            {
                Player = state.Player with
                {
                    Deck = state.Player.Deck.Select(card =>
                        upgradedIds.Contains(card.InstanceId)
                            ? card with
                            {
                                UpgradeLevel = checked(card.UpgradeLevel + 1)
                            }
                            : card).ToArray()
                }
            };
        }

        return state;
    }
}
