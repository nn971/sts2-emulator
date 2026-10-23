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

            if (state.Stochastic.NativeRunRng is not null)
            {
                // Native War Hammer uses the persistent run.niche stream.
                // Resolve each swap through the typed boundary and keep the
                // run and active-combat cursor copies synchronized.
                var sourceRunStreams = state.World?.Combat?.Stochastic.NativeRng
                    ?.RunStreams ?? state.Stochastic.NativeRunRng ??
                    throw new InvalidOperationException(
                        "Native War Hammer requires run RNG ownership.");
                var nativeStreams = sourceRunStreams.Fork();
                var originalIdentities =
                    RandomBoundaryJournalV111.HasActiveJournal
                        ? candidates.Select(card => "card-instance:" +
                            card.InstanceId.ToString(
                                System.Globalization.CultureInfo.InvariantCulture))
                            .ToArray()
                        : null;
                NativeEventRandomProbeV111.CaptureShuffle(
                    StochasticSiteRegistryV111
                        .WarHammerEliteUpgradeShuffleSiteId,
                    $"player:0:war_hammer:{hammer}:eligible_cards",
                    candidates, nativeStreams, "run.niche");
                var decisions = NativeFisherYatesV111.ShuffleInPlace(
                    candidates,
                    nativeStreams,
                    new NativeRandomIndexProviderV111(),
                    "run.niche",
                    $"player:0:war_hammer:{hammer}:eligible_cards",
                    StochasticSiteRegistryV111
                        .WarHammerEliteUpgradeShuffleSiteId);
                if (originalIdentities is not null)
                    SemanticPoolShuffleChoiceV111.RecordNativeAudit(
                        originalIdentities,
                        candidates.Select(card => "card-instance:" +
                            card.InstanceId.ToString(
                                System.Globalization.CultureInfo.InvariantCulture))
                            .ToArray(),
                        decisions, Math.Min(4, candidates.Count),
                        SemanticPoolShuffleChoiceV111
                            .NativeWarHammerOperation,
                        null);
                var activeNativeRng =
                    state.World?.Combat?.Stochastic.NativeRng;
                var combat = state.World?.Combat;
                if (activeNativeRng is not null && combat is not null)
                {
                    var sourceCombat = combat;
                    combat = sourceCombat with
                    {
                        Stochastic = CombatStochasticStateV111.WithNativeRng(
                            sourceCombat.Stochastic, activeNativeRng with
                        {
                            RunStreams = nativeStreams.Fork()
                        })
                    };
                }
                state = state with
                {
                    Stochastic = RunStochasticStateV111.WithNativeRunRng(
                        state.Stochastic, nativeStreams),
                    World = state.World is null || combat is null
                        ? state.World
                        : state.World with { Combat = combat }
                };
                NativeRunRngV111.Validate(state);
            }
            else
            {
                // Prototype runs do not own run.niche. Keep their test path
                // on the legacy event stream without claiming native parity.
                var shuffled = candidates.ToArray();
                var prototypeRng = state.Stochastic.PrototypeRng;
                _ = LegacyFisherYatesV111.ShuffleInPlace(
                    shuffled,
                    prototypeRng,
                    "event",
                    StochasticSiteRegistryV111
                        .WarHammerEliteUpgradeShuffleSiteId,
                    $"player:0:war_hammer:{hammer}:eligible_cards");
                candidates = shuffled.ToList();
                state = state with
                {
                    Stochastic = RunStochasticStateV111.WithPrototypeRng(
                        state.Stochastic, prototypeRng)
                };
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
