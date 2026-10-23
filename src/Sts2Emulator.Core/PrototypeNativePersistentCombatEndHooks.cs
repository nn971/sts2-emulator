using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Native-shaped persistent card and relic callbacks on a victorious
/// combat's completion. These update the run's persistent deck/relic
/// instances before the combat reward is generated.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    public const string FishingRodUpgradeSiteId =
        "relic.persistent_combat_end.random_upgrade.index_draw";

    private sealed record NativeCombatsSeen(int CombatsSeen);

    private sealed record NativeElitesDefeated(int ElitesDefeated);

    private static RunState ApplyNativePersistentCombatEnd(
        RunState state,
        PrototypeRoomType? roomType)
    {
        var player = state.Player;
        var rng = state.Stochastic.PrototypeRng;
        var nativeRunStreams =
            state.World?.Combat?.Stochastic.NativeRng?.RunStreams.Fork()
            ?? state.Stochastic.NativeRunRng;

        // Guilty counts every completed combat and leaves the deck
        // after the fifth combat, including Elite/Boss combats.
        var deck = player.Deck.Select(card =>
        {
            if (card.CardId != "proto.native.event.guilty")
            {
                return card;
            }

            var count = GetNativeCombatsSeen(card.PersistentState) + 1;
            return card with
            {
                PersistentState = JsonSerializer.SerializeToElement(
                    new NativeCombatsSeen(count))
            };
        }).Where(card =>
            card.CardId != "proto.native.event.guilty"
            || GetNativeCombatsSeen(card.PersistentState) < 5)
            .ToArray();

        player = player with { Deck = deck };

        // Each Sword of Stone is its own native relic instance with its
        // own persisted elite counter. The fifth elite victory replaces
        // that instance with Jade while preserving the relic's slot.
        if (roomType == PrototypeRoomType.Elite)
        {
            const string stoneId = "proto.native.event.sword_of_stone";
            const string jadeId = "proto.native.event.sword_of_jade";
            player = player with
            {
                Relics = player.Relics.Select(relic =>
                {
                    if (relic.RelicId != stoneId || relic.IsMelted)
                    {
                        return relic;
                    }

                    var defeated = checked(
                        GetNativeElitesDefeated(relic.PersistentState) + 1);
                    return defeated >= 5
                        ? new RelicInstance(jadeId, PrototypeJson.EmptyObject())
                        : relic with
                        {
                            PersistentState = JsonSerializer.SerializeToElement(
                                new NativeElitesDefeated(defeated))
                        };
                }).ToArray()
            };
        }

        // Each Fishing Rod independently counts ordinary monster combats.
        // On every third combat its native callback upgrades one currently
        // eligible deck card; when two Rods are present, the callbacks
        // execute separately and see each other's deck modifications.
        if (roomType == PrototypeRoomType.Combat)
        {
            var rodId = PrototypeNativeOvergrowthEvents.NeowRelicId(
                "FishingRod");
            var relics = (RelicInstance[])player.Relics.Clone();
            for (var index = 0; index < relics.Length; index++)
            {
                var relic = relics[index];
                if (relic.RelicId != rodId || relic.IsMelted)
                {
                    continue;
                }

                var count = checked(
                    GetNativeCombatsSeen(relic.PersistentState) + 1);
                relics[index] = relic with
                {
                    PersistentState = JsonSerializer.SerializeToElement(
                        new NativeCombatsSeen(count))
                };
                if (count % 3 != 0)
                {
                    continue;
                }

                var candidates = player.Deck
                    .Where(card => CanSelectEventDeckCard(
                        card, PrototypePersistentDeckChoiceKind.Upgrade,
                        null, null, false))
                    .Select(card => card.InstanceId)
                    .ToArray();
                if (candidates.Length == 0)
                {
                    continue;
                }

                int selectedIndex;
                if (nativeRunStreams is not null)
                {
                    var ownerId = $"relic:{relics[index].RelicId}:slot:{index}";
                    var request = new RandomIndexRequestV111(
                        FishingRodUpgradeSiteId,
                        "run.niche",
                        ownerId,
                        count / 3 - 1,
                        Enumerable.Range(0, candidates.Length).ToArray());
                    var decision = StochasticProviderScopeV111.ResolveIndex(
                        new NativeRandomIndexProviderV111(), request,
                        nativeRunStreams);
                    if (RandomBoundaryJournalV111.HasActiveJournal)
                        RandomBoundaryJournalV111
                            .RecordCardInstanceIndexOutcome(
                                SemanticCardInstanceIndexAuditV111.Create(
                                    decision, candidates));
                    selectedIndex = decision.SelectedIndex;
                }
                else
                {
                    // Prototype-only runs retain their existing event stream.
                    selectedIndex = LegacyRandomChoiceV111.ChooseIndexRange(
                        rng,
                        "relic.persistent_combat_end.random_upgrade.index_draw",
                        "event",
                        $"relic:{relics[index].RelicId}:combat_count:{count}:pool:"
                            + CanonicalJson.Sha256(candidates),
                        candidates.Length).SelectedIndex;
                }
                var selected = candidates[selectedIndex];
                player = player with
                {
                    Deck = player.Deck.Select(card =>
                        card.InstanceId == selected
                            ? card with { UpgradeLevel = card.UpgradeLevel + 1 }
                            : card).ToArray()
                };
            }

            player = player with { Relics = relics };
        }

        var world = state.World;
        var combat = world?.Combat;
        if (nativeRunStreams is not null &&
            combat?.Stochastic.NativeRng is { } active)
        {
            var sourceCombat = combat;
            combat = sourceCombat with
            {
                Stochastic = CombatStochasticStateV111.WithNativeRng(
                    sourceCombat.Stochastic, active with
                {
                    RunStreams = nativeRunStreams.Fork()
                })
            };
        }

        var stochastic = RunStochasticStateV111.WithPrototypeRng(
            state.Stochastic, rng);
        if (nativeRunStreams is not null)
            stochastic = RunStochasticStateV111.WithNativeRunRng(
                stochastic, nativeRunStreams);
        var updated = state with
        {
            Player = player,
            Stochastic = stochastic,
            World = world is null || combat is null
                ? world
                : world with { Combat = combat }
        };
        if (nativeRunStreams is not null &&
            combat?.Stochastic.NativeRng is not null)
            NativeRunRngV111.Validate(updated);
        return updated;
    }

    private static int GetNativeElitesDefeated(JsonElement state) =>
        state.ValueKind == JsonValueKind.Object
        && state.TryGetProperty(
            nameof(NativeElitesDefeated.ElitesDefeated), out var count)
        && count.ValueKind == JsonValueKind.Number
            ? count.GetInt32()
            : 0;

    private static int GetNativeCombatsSeen(JsonElement state) =>
        state.ValueKind == JsonValueKind.Object
        && state.TryGetProperty(
            nameof(NativeCombatsSeen.CombatsSeen), out var count)
        && count.ValueKind == JsonValueKind.Number
            ? count.GetInt32()
            : 0;
}
