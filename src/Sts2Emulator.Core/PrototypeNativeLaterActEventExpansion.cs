using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111.0 event and event-only relic slice:
/// Hive Lost Wisp, Colossal Flower; Glory Round Tea Party.
/// Their event pools remain a non-native-distributed subset.
/// </summary>
public static class PrototypeNativeLaterActEventExpansion
{
    public const string LostWispEventId = "proto.native.hive.lost_wisp";
    public const string ColossalFlowerEventId = "proto.native.hive.colossal_flower";
    public const string RoundTeaPartyEventId = "proto.native.glory.round_tea_party";
    public const string LostWispRelicId = "proto.native.hive.relic.lost_wisp";
    public const string PollinousCoreRelicId = "proto.native.hive.relic.pollinous_core";
    public const string RoyalPoisonRelicId = "proto.native.glory.relic.royal_poison";
    public const string DecayId = "proto.curse.decay";

    public static PrototypeCardDefinition[] Cards { get; } =
    [
        // Decay: 2 blockable, unpowered Move damage if in hand at end of turn.
        new(DecayId, "Decay", -1, PrototypeCardTarget.None, [],
            Rarity: PrototypeCardRarity.Curse, Type: PrototypeCardType.Curse,
            Unplayable: true, RewardEligible: false,
            CanBeGeneratedInCombat: false, MaxUpgradeLevel: 0,
            EndTurnDamageIfInHand: 2)
    ];

    public static PrototypeRelicDefinition[] Relics { get; } =
    [
        // Power cards trigger 8 unpowered damage to every live enemy.
        new(LostWispRelicId, "Lost Wisp", Triggers:
        [
            new(PrototypeCombatEventKind.CardPlayed,
            [
                new(PrototypeCombatEffectKind.DamageEnemy, 8,
                    Target: PrototypeEffectTarget.AllEnemies)
            ], RequiredSourceCardType: PrototypeCardType.Power)
        ]),
        // An owner's four consecutive BeforeHandDraw calls (across
        // encounters) grant +2 to that draw. See the persistent run hook.
        new(PollinousCoreRelicId, "Pollinous Core"),
        // On the first turn of each combat, inflict four unpowered,
        // explicitly unblockable self-damage.
        new(RoyalPoisonRelicId, "Royal Poison", Triggers:
        [
            new(PrototypeCombatEventKind.PlayerTurnStarted,
            [
                new(PrototypeCombatEffectKind.DamagePlayer, 4,
                    IgnorePlayerBlock: true)
            ], TurnEquals: 1)
        ])
    ];

    public static PrototypeEventDefinition[] Events { get; } =
    [
        new(LostWispEventId, "Lost Wisp",
        [
            new("claim", "Claim Lost Wisp and receive Decay", []),
            new("search", "Search — receive 45–75 Gold", [])
        ], MinAct: 2, MaxAct: 2, Weight: 0),
        new(ColossalFlowerEventId, "Colossal Flower",
        [
            new("extract", "Extract current prize", []),
            new("deeper", "Reach deeper", []),
            new("pollinous_core", "Claim Pollinous Core", [])
        ], MinAct: 2, MaxAct: 2, Weight: 0),
        new(RoundTeaPartyEventId, "Round Tea Party",
        [
            new("enjoy_tea", "Enjoy tea — Royal Poison and full heal", []),
            new("pick_fight", "Pick a fight", []),
            new("continue_fight", "Continue fight — 11 damage and relic", [])
        ], MinAct: 3, MaxAct: 3, Weight: 0)
    ];

    public static bool IsEligible(string id, PlayerState player) =>
        id switch
        {
            ColossalFlowerEventId => player.Hp >= 19,
            RoundTeaPartyEventId => player.Hp >= 12,
            _ => true
        };

    public static int[] FlowerPrizes { get; } = [35, 75, 135];
    public static int[] FlowerDamage { get; } = [5, 6, 7];

    public static (PlayerState Player, int DrawBonus)
        AdvancePollinousCoreHandDraw(PlayerState player)
    {
        var bonus = 0;
        var updated = player.Relics.Select(relic =>
        {
            if (relic.RelicId != PollinousCoreRelicId)
                return relic;
            var turns = 0;
            if (relic.PersistentState.ValueKind == JsonValueKind.Object
                && relic.PersistentState.TryGetProperty(
                    "turnsSeen", out var saved)
                && saved.ValueKind == JsonValueKind.Number)
                turns = saved.GetInt32();
            turns++;
            if (turns >= 4)
            {
                turns = 0;
                bonus += 2;
            }
            return relic with
            {
                PersistentState = JsonSerializer.SerializeToElement(
                    new { turnsSeen = turns })
            };
        }).ToArray();
        return (player with { Relics = updated }, bonus);
    }
}

public sealed partial class PrototypeGameEngine
{
    private static bool IsLaterActExpandedEvent(string id) =>
        id is PrototypeNativeLaterActEventExpansion.LostWispEventId
            or PrototypeNativeLaterActEventExpansion.ColossalFlowerEventId
            or PrototypeNativeLaterActEventExpansion.RoundTeaPartyEventId;

    private static IReadOnlyList<GameAction> GetLaterActExpandedEventActions(
        EventState evt)
    {
        string[] options = evt.EventId switch
        {
            PrototypeNativeLaterActEventExpansion.LostWispEventId =>
                ["claim", "search"],
            PrototypeNativeLaterActEventExpansion.ColossalFlowerEventId =>
                evt.NativePageIndex switch
                {
                    0 or 1 => ["extract", "deeper"],
                    2 => ["extract", "pollinous_core"],
                    _ => throw new InvalidOperationException(
                        "Colossal Flower has no fourth dig.")
                },
            PrototypeNativeLaterActEventExpansion.RoundTeaPartyEventId =>
                evt.NativePageIndex switch
                {
                    0 => ["enjoy_tea", "pick_fight"],
                    1 => ["continue_fight"],
                    _ => throw new InvalidOperationException(
                        "Round Tea Party is already completed.")
                },
            _ => throw new InvalidOperationException(
                "Unknown expanded later-act event.")
        };
        return options.Select(id => GameAction.Create(
            "event_choice", new EventChoicePayload(id))).ToArray();
    }

    private static RunState StepLaterActExpandedEvent(
        RunState state, string choice)
    {
        var world = RequireWorld(state);
        var evt = world.Event ?? throw new InvalidOperationException(
            "Expanded later-act event requires active event state.");
        var legal = GetLaterActExpandedEventActions(evt)
            .Select(action =>
                action.ReadPayload<EventChoicePayload>().ChoiceId);
        if (!legal.Contains(choice, StringComparer.Ordinal))
            throw new InvalidOperationException(
                $"Unavailable event action '{choice}' on page {evt.NativePageIndex}.");
        var player = state.Player;
        var nextId = world.NextCardInstanceId;

        if (evt.EventId ==
            PrototypeNativeLaterActEventExpansion.LostWispEventId)
        {
            if (choice == "search")
                player = player with
                {
                    Gold = checked(player.Gold + evt.NativeEventGold)
                };
            else
            {
                player = AppendCard(player, nextId++,
                    PrototypeNativeLaterActEventExpansion.DecayId,
                    state.Rng);
                player = ObtainLaterActEventRelic(player,
                    PrototypeNativeLaterActEventExpansion.LostWispRelicId,
                    state.Rng);
            }
        }
        else if (evt.EventId ==
            PrototypeNativeLaterActEventExpansion.ColossalFlowerEventId)
        {
            var depth = evt.NativePageIndex;
            if (choice == "deeper")
            {
                player = player with
                {
                    Hp = Math.Max(0, player.Hp
                        - PrototypeNativeLaterActEventExpansion.FlowerDamage[depth])
                };
                if (player.Hp <= 0)
                    return EndRun(state with { Player = player }, "defeat");
                return state with
                {
                    Player = player,
                    World = world with
                    {
                        Event = evt with { NativePageIndex = depth + 1 }
                    }
                };
            }
            if (choice == "extract")
                player = player with
                {
                    Gold = checked(player.Gold
                        + PrototypeNativeLaterActEventExpansion.FlowerPrizes[depth])
                };
            else if (choice == "pollinous_core")
            {
                player = player with
                {
                    Hp = Math.Max(0, player.Hp - 7)
                };
                if (player.Hp <= 0)
                    return EndRun(state with { Player = player }, "defeat");
                player = ObtainLaterActEventRelic(player,
                    PrototypeNativeLaterActEventExpansion.PollinousCoreRelicId,
                    state.Rng);
            }
        }
        else if (evt.EventId ==
            PrototypeNativeLaterActEventExpansion.RoundTeaPartyEventId)
        {
            if (choice == "pick_fight")
                return state with
                {
                    World = world with
                    {
                        Event = evt with { NativePageIndex = 1 }
                    }
                };
            if (choice == "enjoy_tea")
            {
                player = ObtainLaterActEventRelic(player,
                    PrototypeNativeLaterActEventExpansion.RoyalPoisonRelicId,
                    state.Rng);
                player = player with { Hp = player.MaxHp };
            }
            else
            {
                player = player with { Hp = Math.Max(0, player.Hp - 11) };
                if (player.Hp <= 0)
                    return EndRun(state with { Player = player }, "defeat");
                // RelicFactory.PullNextRelicFromFront; current emulator
                // rarity-partitioned grab bag approximates source RNG.
                var draw = PrototypeNativeRelicGrabBag.Draw(
                    world, player, state.Rng, merchant: false);
                world = draw.World;
                if (draw.Id is { } relicId)
                    player = ObtainLaterActEventRelic(
                        player, relicId, state.Rng);
            }
        }

        return CompleteRoomToMap(state with
        {
            Player = player,
            World = world with
            {
                Event = null,
                NextCardInstanceId = nextId
            }
        });
    }

    private static PlayerState ObtainLaterActEventRelic(
        PlayerState player, string relicId, RngBundle rng)
    {
        _ = PrototypeContent.Relic(relicId);
        if (player.Relics.Any(r => r.RelicId == relicId))
            throw new InvalidOperationException(
                $"Player already owns event relic '{relicId}'.");
        player = player with
        {
            Relics = player.Relics.Append(
                new RelicInstance(relicId, PrototypeJson.EmptyObject()))
                .ToArray()
        };
        return ApplyRelicRunEvent(player,
            PrototypeRunEventKind.RelicAcquired,
            acquiredRelicId: relicId, rng: rng);
    }
}
