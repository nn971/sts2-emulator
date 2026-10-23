namespace Sts2Emulator.Core;

/// <summary>
/// Source-backed Silent implementation of The Future of Potions for the
/// configured Hive and Glory route.
/// </summary>
public static class PrototypeNativeFutureOfPotions
{
    public const string EventId = "proto.native.shared.the_future_of_potions";
    public const string NativeRngEventId = "THE_FUTURE_OF_POTIONS";
    public const string CardTypeSiteId =
        StochasticSiteRegistryV111.NativeFutureOfPotionsCardTypeSiteId;
    public const string TradeChoiceId = "trade_potion";
    public const string TradeAction = "trade_event_potion";

    public static PrototypeEventDefinition Definition =>
        PrototypeDefinitionCopiesV111.CloneEventDefinition(
            EventDefinitionValue);

    private static readonly PrototypeEventDefinition EventDefinitionValue = new(
        EventId,
        "The Future of Potions",
        [new(TradeChoiceId, "Trade a potion", [])],
        MinAct: 2,
        MaxAct: 3,
        Weight: 0,
        OncePerRun: true);

    // Inventory branches from TheFutureOfPotions.cs at the pinned source.
    public static string[] CoveredBranches =>
        (string[])CoveredBranchValues.Clone();

    private static readonly string[] CoveredBranchValues =
    [
        "initial/POTION",
        "page/DONE",
        "page/INITIAL",
        "page/INITIAL/POTION"
    ];

    public static bool IsEligible(PlayerState player)
    {
        var owned = player.PotionSlots
            .Where(potion => potion is not null)
            .Select(potion => potion!.PotionId)
            .ToArray();
        return owned.Length >= 2
            && owned.All(potionId => TryGetCardRarity(potionId, out _));
    }

    public static RunState RollOffers(RunState state)
    {
        var world = state.World
            ?? throw new InvalidOperationException(
                "The Future of Potions requires an active run world.");
        var eventState = world.Event
            ?? throw new InvalidOperationException(
                "The Future of Potions has no active event.");
        var offers = new List<PrototypeFuturePotionOffer>(3);
        foreach (var (potion, slot) in state.Player.PotionSlots
                     .Select((potion, slot) => (Potion: potion, Slot: slot))
                     .Where(item => item.Potion is not null))
        {
            var potionId = potion!.PotionId;
            if (!TryGetCardRarity(potionId, out var rarity))
            {
                throw new InvalidOperationException(
                    $"The Future of Potions has no rarity mapping for '{potionId}'.");
            }

            var cardTypes = CardTypesForRarity(rarity);
            var draw = NativeEventContextRngV111.ResolveIndex(
                state,
                eventState,
                NativeRngEventId,
                CardTypeSiteId,
                $"potion_slot:{slot}:potion:{potionId}:pool:"
                    + CanonicalJson.Sha256(cardTypes),
                cardTypes.Length);
            state = draw.State;
            world = state.World!;
            eventState = world.Event!;
            var cardType = cardTypes[draw.SelectedIndex];
            if (offers.Count < 3)
            {
                offers.Add(new PrototypeFuturePotionOffer(
                    slot, potionId, cardType, rarity));
            }
        }

        world = world with
        {
            Event = eventState with
            {
                Stochastic = EventStochasticStateV111.WithCommittedRandom(
                    eventState.Stochastic,
                    eventState.Stochastic.RandomOutcomes with
                    {
                        FuturePotionOffers = offers.ToArray()
                    })
            }
        };
        return state with { World = world };
    }

    public static PrototypeCardType[] CardTypesForRarity(
        PrototypeCardRarity rarity) => rarity switch
    {
        PrototypeCardRarity.Common =>
            [PrototypeCardType.Attack, PrototypeCardType.Skill],
        PrototypeCardRarity.Uncommon or PrototypeCardRarity.Rare =>
        [
            PrototypeCardType.Attack,
            PrototypeCardType.Skill,
            PrototypeCardType.Power
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(rarity))
    };

    public static bool TryGetCardRarity(
        string potionId, out PrototypeCardRarity rarity)
    {
        if (potionId is PrototypeNativePotionCourier.FoulPotionId
            or "proto.native.neow.ambergris"
            or "proto.native.underdocks.glowwater_potion")
        {
            rarity = PrototypeCardRarity.Rare;
            return true;
        }

        if (StringComparer.Ordinal.Equals(potionId, "proto.potion.blood"))
        {
            rarity = PrototypeCardRarity.Common;
            return true;
        }

        if (!PrototypeNativePotionShop.TryGetRarity(potionId, out var native))
        {
            rarity = default;
            return false;
        }

        rarity = native switch
        {
            PrototypeNativePotionRarity.Common => PrototypeCardRarity.Common,
            PrototypeNativePotionRarity.Uncommon => PrototypeCardRarity.Uncommon,
            PrototypeNativePotionRarity.Rare => PrototypeCardRarity.Rare,
            _ => throw new ArgumentOutOfRangeException(nameof(native))
        };
        return true;
    }
}

public sealed partial class PrototypeGameEngine
{
    private static RunState StepNativeFutureOfPotions(
        RunState state, GameAction action)
    {
        RequireKind(action, PrototypeNativeFutureOfPotions.TradeAction);
        var world = RequireWorld(state);
        var eventState = world.Event
            ?? throw new InvalidOperationException(
                "The Future of Potions has no active event.");
        var slot = action.ReadPayload<TradeFuturePotionPayload>().Slot;
        var offer = eventState.Stochastic.RandomOutcomes.FuturePotionOffers?
            .SingleOrDefault(item => item.Slot == slot)
            ?? throw new InvalidOperationException(
                $"Potion slot {slot} has no offer in The Future of Potions.");
        if ((uint)slot >= (uint)state.Player.PotionSlots.Length
            || state.Player.PotionSlots[slot]?.PotionId != offer.PotionId)
        {
            throw new InvalidOperationException(
                "The offered potion is no longer in its original slot.");
        }

        var pool = PrototypeContent.NativeSilentRewardCardPool
            .Where(id =>
            {
                var card = PrototypeContent.Card(id);
                return card.Rarity == offer.CardRarity
                    && card.Type == offer.CardType;
            })
            .ToArray();
        if (pool.Length < 3)
        {
            throw new InvalidOperationException(
                "The Future of Potions card pool cannot supply three distinct options.");
        }

        var prototypeRng = state.Stochastic.PrototypeRng;
        var cardOptions = PickDistinct(pool, 3, prototypeRng, "reward");
        var potionSlots = (PotionInstance?[])state.Player.PotionSlots.Clone();
        potionSlots[slot] = null;
        var silverCrucible = ApplySilverCrucibleToCardRewardGeneration(
            state.Player, 1);
        var reward = new RewardState(
            SourceRoom: "Event:" + eventState.EventId,
            CardOptions: cardOptions,
            PotionOption: null,
            RelicOption: null,
            CardResolved: false,
            PotionResolved: true,
            RelicResolved: true,
            EndsAct: false,
            CardOptionsUpgraded: true,
            CardRewardGenerators:
            [
                new PrototypeCardRewardGenerator(
                    PrototypeCardRewardGeneratorKind.RarityPool,
                    world.Act,
                    Rarity: offer.CardRarity,
                    CardType: offer.CardType)
            ]);

        state = state with
        {
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng),
            Player = silverCrucible.Player with
            {
                PotionSlots = potionSlots
            },
            World = world with
            {
                Event = eventState with
                {
                    ChosenChoiceId =
                        PrototypeNativeFutureOfPotions.TradeChoiceId,
                    Stochastic =
                        EventStochasticStateV111.WithCommittedRandom(
                            eventState.Stochastic,
                            eventState.Stochastic.RandomOutcomes with
                            {
                                FuturePotionOffers = null
                            }),
                    PendingReward = reward
                }
            }
        };
        return AdvanceEventContinuations(state);
    }
}
