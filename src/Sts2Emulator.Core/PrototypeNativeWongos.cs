using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Source-backed Act 1 Welcome to Wongos event and its event-only relics.
/// </summary>
public static class PrototypeNativeWongos
{
    public const string EventId = "proto.native.event.welcome_to_wongos";
    public const string BargainBinChoiceId = "bargain_bin";
    public const string FeaturedItemChoiceId = "featured_item";
    public const string MysteryBoxChoiceId = "mystery_box";
    public const string LeaveChoiceId = "leave";
    public const string MysteryTicketId = "proto.relic.wongos_mystery_ticket";
    public const string AppreciationBadgeId =
        "proto.relic.wongo_customer_appreciation_badge";
    public const int BargainBinCost = 100;
    public const int FeaturedItemCost = 200;
    public const int MysteryBoxCost = 300;
    public const int TicketCombats = 5;
    public const int TicketRelicRewards = 3;

    public static string[] CoveredBranches { get; } =
    [
        "initial/BARGAIN_BIN",
        "initial/BARGAIN_BIN_LOCKED",
        "initial/FEATURED_ITEM",
        "initial/FEATURED_ITEM_LOCKED",
        "initial/LEAVE",
        "initial/MYSTERY_BOX",
        "initial/MYSTERY_BOX_LOCKED",
        "page/AFTER_BUY",
        "page/AFTER_BUY_BADGE_COUNTER",
        "page/AFTER_BUY_RECEIVE_BADGE",
        "page/INITIAL",
        "page/INITIAL/BARGAIN_BIN",
        "page/INITIAL/BARGAIN_BIN_LOCKED",
        "page/INITIAL/FEATURED_ITEM",
        "page/INITIAL/FEATURED_ITEM_LOCKED",
        "page/INITIAL/LEAVE",
        "page/INITIAL/MYSTERY_BOX",
        "page/INITIAL/MYSTERY_BOX_LOCKED",
        "page/LEAVE"
    ];

    public static PrototypeEventDefinition Definition { get; } = new(
        EventId,
        "Welcome to Wongos",
        [
            new(BargainBinChoiceId,
                "Spend 100 gold on a Common shop relic",
                [new(PrototypeRunEffectKind.LoseGold, BargainBinCost)]),
            new(FeaturedItemChoiceId,
                "Spend 200 gold on the featured Rare shop relic",
                [new(PrototypeRunEffectKind.LoseGold, FeaturedItemCost)]),
            new(MysteryBoxChoiceId,
                "Spend 300 gold on a Mystery Ticket",
                [new(PrototypeRunEffectKind.LoseGold, MysteryBoxCost),
                    new(PrototypeRunEffectKind.GainRelic,
                        RelicId: MysteryTicketId)]),
            new(LeaveChoiceId, "Leave; downgrade a random upgraded card", [])
        ],
        MinAct: 1,
        MaxAct: 1);

    public static PrototypeRelicDefinition[] Relics { get; } =
    [
        new(MysteryTicketId, "Wongos Mystery Ticket"),
        new(AppreciationBadgeId, "Wongo Customer Appreciation Badge")
    ];

    public static (RunWorldState World, EventState Event) Initialize(
        RunWorldState world,
        PlayerState player,
        EventState eventState,
        RngBundle rng)
    {
        var draw = PrototypeNativeRelicGrabBag.Draw(
            world,
            player,
            rng,
            merchant: false,
            forcedRarity: PrototypeRelicRarity.Rare,
            shopEligible: true);
        var featured = draw.Id
            ?? throw new InvalidOperationException(
                "No eligible Rare shop relic remains for Wongos.");
        return (draw.World,
            eventState with { NativeWongoFeaturedRelicId = featured });
    }
}

public sealed partial class PrototypeGameEngine
{
    private sealed record WongosMysteryTicketState(
        int CombatsFinished,
        bool GaveRelic);

    private static IReadOnlyList<GameAction> GetNativeWongosActions(
        EventState eventState,
        PlayerState player)
    {
        if (eventState.NativePageIndex != 0)
            throw new InvalidOperationException(
                "Welcome to Wongos has no active choice page.");

        var actions = new List<GameAction>();
        if (player.Gold >= PrototypeNativeWongos.BargainBinCost)
        {
            actions.Add(GameAction.Create("event_choice",
                new EventChoicePayload(
                    PrototypeNativeWongos.BargainBinChoiceId)));
        }
        if (player.Gold >= PrototypeNativeWongos.FeaturedItemCost)
        {
            actions.Add(GameAction.Create("event_choice",
                new EventChoicePayload(
                    PrototypeNativeWongos.FeaturedItemChoiceId)));
        }
        if (player.Gold >= PrototypeNativeWongos.MysteryBoxCost)
        {
            actions.Add(GameAction.Create("event_choice",
                new EventChoicePayload(
                    PrototypeNativeWongos.MysteryBoxChoiceId)));
        }
        actions.Add(GameAction.Create("event_choice",
            new EventChoicePayload(PrototypeNativeWongos.LeaveChoiceId)));
        return actions;
    }

    private static RunState StepNativeWongos(
        RunState state,
        GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        var eventState = world.Event
            ?? throw new InvalidOperationException(
                "Welcome to Wongos has no active event.");
        if (eventState.EventId != PrototypeNativeWongos.EventId
            || world.Act != 1
            || eventState.NativePageIndex != 0)
        {
            throw new InvalidOperationException(
                "Welcome to Wongos requires its initial Act 1 page.");
        }

        var choiceId = action.ReadPayload<EventChoicePayload>().ChoiceId;
        var player = state.Player;
        var nextCardId = world.NextCardInstanceId;
        var nativeRunRng = state.NativeRunRng?.Fork();
        if (choiceId == PrototypeNativeWongos.LeaveChoiceId)
        {
            var candidates = player.Deck
                .Where(card => card.UpgradeLevel > 0
                    && PrototypeContent.Card(card.CardId).MaxUpgradeLevel > 0)
                .ToArray();
            if (candidates.Length > 0)
            {
                var selected = candidates[PrototypeRng.NextInt(
                    state.Rng, "event", candidates.Length)];
                player = player with
                {
                    Deck = player.Deck.Select(card =>
                        card.InstanceId == selected.InstanceId
                            ? card with
                            {
                                UpgradeLevel = card.UpgradeLevel - 1
                            }
                            : card).ToArray()
                };
            }

            return AdvanceEventContinuations(state with
            {
                Player = player,
                World = world with
                {
                    Event = eventState with
                    {
                        ChosenChoiceId = choiceId,
                        NativePageIndex = 0,
                        NativeWongoResultPage = "leave"
                    }
                }
            });
        }

        var (cost, points) = choiceId switch
        {
            PrototypeNativeWongos.BargainBinChoiceId =>
                (PrototypeNativeWongos.BargainBinCost, 32),
            PrototypeNativeWongos.FeaturedItemChoiceId =>
                (PrototypeNativeWongos.FeaturedItemCost, 16),
            PrototypeNativeWongos.MysteryBoxChoiceId =>
                (PrototypeNativeWongos.MysteryBoxCost, 8),
            _ => throw new InvalidOperationException(
                $"Unknown Welcome to Wongos choice '{choiceId}'.")
        };
        if (player.Gold < cost)
            throw new InvalidOperationException(
                "Wongos purchase is locked by its gold cost.");

        string relicId;
        if (choiceId == PrototypeNativeWongos.BargainBinChoiceId)
        {
            var draw = PrototypeNativeRelicGrabBag.Draw(
                world,
                player,
                state.Rng,
                merchant: false,
                forcedRarity: PrototypeRelicRarity.Common,
                shopEligible: true);
            world = draw.World;
            relicId = draw.Id
                ?? throw new InvalidOperationException(
                    "No eligible Common shop relic remains for the bargain bin.");
        }
        else if (choiceId == PrototypeNativeWongos.FeaturedItemChoiceId)
        {
            relicId = eventState.NativeWongoFeaturedRelicId
                ?? throw new InvalidOperationException(
                    "Welcome to Wongos has no featured relic.");
        }
        else
        {
            relicId = PrototypeNativeWongos.MysteryTicketId;
        }

        player = player with { Gold = player.Gold - cost };
        player = AppendWongosRelic(
            player,
            relicId,
            state.Rng,
            world,
            nativeRunRng,
            allocateCardId: () => nextCardId++);

        var priorPoints = state.Configuration?.Profile.WongoPoints ?? 0;
        var newTotal = checked(priorPoints + points);
        var receivesBadge = priorPoints % 2000 + points >= 2000;
        var resultPage = receivesBadge
            ? "receive_badge"
            : newTotal / 2000 > 0
                ? "badge_counter"
                : "after_buy";
        if (receivesBadge)
        {
            player = AppendWongosRelic(
                player,
                PrototypeNativeWongos.AppreciationBadgeId,
                state.Rng,
                world,
                nativeRunRng,
                allocateCardId: () => nextCardId++);
        }

        var configuration = state.Configuration is { } configured
            ? configured with
            {
                Profile = configured.Profile with
                {
                    WongoPoints = newTotal
                }
            }
            : null;
        return AdvanceEventContinuations(state with
        {
            Player = player,
            Configuration = configuration,
            World = world with
            {
                NextCardInstanceId = nextCardId,
                Event = eventState with
                {
                    ChosenChoiceId = choiceId,
                    NativePageIndex = 0,
                    NativeWongoResultPage = resultPage
                }
            },
            NativeRunRng = nativeRunRng
        });
    }

    private static PlayerState AppendWongosRelic(
        PlayerState player,
        string relicId,
        RngBundle rng,
        RunWorldState world,
        RngBundle? nativeRunRng,
        Func<long> allocateCardId)
    {
        var persistent = relicId == PrototypeNativeWongos.MysteryTicketId
            ? JsonSerializer.SerializeToElement(
                new WongosMysteryTicketState(0, GaveRelic: false))
            : PrototypeJson.EmptyObject();
        player = player with
        {
            Relics = player.Relics.Append(
                new RelicInstance(relicId, persistent)).ToArray()
        };
        return ApplyRelicRunEvent(
            player,
            PrototypeRunEventKind.RelicAcquired,
            acquiredRelicId: relicId,
            rng: rng,
            world: world,
            allocateCardId: allocateCardId,
            nativeRunRng: nativeRunRng);
    }

    internal static PlayerState UpdateWongosTicketCombatEnd(
        PlayerState player,
        ref RunWorldState world,
        RngBundle rng,
        List<string> extraRelicRewardIds,
        out bool addedTicketRewards)
    {
        addedTicketRewards = false;
        if (world.ActiveRoom != PrototypeRoomType.Combat)
            return player;

        var relics = (RelicInstance[])player.Relics.Clone();
        for (var index = 0; index < relics.Length; index++)
        {
            var relic = relics[index];
            if (relic.RelicId != PrototypeNativeWongos.MysteryTicketId)
                continue;

            var oldState = ReadWongosMysteryTicketState(
                relic.PersistentState);
            var fights = oldState.CombatsFinished + 1;
            var gaveRelic = oldState.GaveRelic;
            if (!gaveRelic && fights >= PrototypeNativeWongos.TicketCombats)
            {
                for (var rewardIndex = 0;
                     rewardIndex < PrototypeNativeWongos.TicketRelicRewards;
                     rewardIndex++)
                {
                    var draw = PrototypeNativeRelicGrabBag.Draw(
                        world,
                        player,
                        rng,
                        merchant: false);
                    world = draw.World;
                    if (draw.Id is { } rewardId)
                        extraRelicRewardIds.Add(rewardId);
                }
                gaveRelic = true;
                addedTicketRewards = true;
            }

            relics[index] = relic with
            {
                PersistentState = JsonSerializer.SerializeToElement(
                    new WongosMysteryTicketState(fights, gaveRelic))
            };
        }

        return player with { Relics = relics };
    }

    private static WongosMysteryTicketState ReadWongosMysteryTicketState(
        JsonElement persistentState)
    {
        if (persistentState.ValueKind != JsonValueKind.Object)
            return new WongosMysteryTicketState(0, false);
        var fights = persistentState.TryGetProperty(
                nameof(WongosMysteryTicketState.CombatsFinished),
                out var fightValue)
            && fightValue.ValueKind == JsonValueKind.Number
            ? Math.Max(0, fightValue.GetInt32()) : 0;
        var gaveRelic = persistentState.TryGetProperty(
            nameof(WongosMysteryTicketState.GaveRelic), out var gaveValue)
            && gaveValue.ValueKind == JsonValueKind.True;
        return new WongosMysteryTicketState(fights, gaveRelic);
    }
}
