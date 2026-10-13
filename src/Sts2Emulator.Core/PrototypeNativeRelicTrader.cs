namespace Sts2Emulator.Core;

/// <summary>
/// Relic Trader's saved three-way exchange for the configured Silent route.
/// </summary>
public static class PrototypeNativeRelicTrader
{
    public const string EventId = "proto.native.shared.relic_trader";

    private static readonly string[] ChoiceIds = ["top", "middle", "bottom"];

    public static string[] CoveredBranches { get; } =
    [
        "initial/BOTTOM",
        "initial/MIDDLE",
        "initial/TOP",
        "page/DONE",
        "page/INITIAL",
        "page/INITIAL/BOTTOM",
        "page/INITIAL/MIDDLE",
        "page/INITIAL/TOP"
    ];

    public static PrototypeEventDefinition Definition { get; } = new(
        EventId,
        "Relic Trader",
        ChoiceIds.Select(choiceId => new PrototypeEventChoiceDefinition(
            choiceId, $"Trade {choiceId}", [])).ToArray(),
        MinAct: 2,
        MaxAct: 3,
        Weight: 0,
        OncePerRun: true);

    public static bool IsEligible(PlayerState player) =>
        player.Relics.Count(IsTradable) >= 5;

    public static bool IsTradable(RelicInstance relic)
    {
        if (relic.IsMelted
            || !PrototypeContent.RelicPool.Contains(
                relic.RelicId, StringComparer.Ordinal))
        {
            return false;
        }

        var definition = PrototypeContent.Relic(relic.RelicId);
        return definition.AcquisitionDeckChoice is null
            && definition.AcquisitionHpLossAfterDeckChoices == 0
            && definition.CardIdAfterAcquisitionDeckChoices is null
            && definition.UpgradeAddedCardType is null
            && definition.ExtraPotionSlots == 0
            && !definition.AddsPet
            && !definition.AddsCombatPet
            && !(definition.RunTriggers
                ?? Array.Empty<PrototypeRelicRunTriggerSpec>())
                .Any(trigger => trigger.EventKind
                    == PrototypeRunEventKind.RelicAcquired);
    }

    public static (RunWorldState World, EventState Event) RollOffers(
        RunWorldState world,
        EventState eventState,
        PlayerState player,
        RngBundle rng)
    {
        var owned = player.Relics
            .Select((relic, index) => (Relic: relic, Index: index))
            .Where(item => IsTradable(item.Relic))
            .ToArray();
        PrototypeRng.Shuffle(rng, "event", owned);
        var selections = owned.Take(3).ToArray();
        if (selections.Length != 3)
        {
            throw new InvalidOperationException(
                "Relic Trader requires three tradable relics to build its offers.");
        }

        var offers = new PrototypeRelicTraderOffer[3];
        for (var index = 0; index < offers.Length; index++)
        {
            var draw = PrototypeNativeRelicGrabBag.Draw(
                world, player, rng, merchant: false);
            world = draw.World;
            var newRelicId = draw.Id ?? "proto.relic.circlet";
            _ = PrototypeContent.Relic(newRelicId);
            offers[index] = new PrototypeRelicTraderOffer(
                ChoiceIds[index],
                selections[index].Index,
                selections[index].Relic.RelicId,
                newRelicId);
        }

        return (world, eventState with
        {
            NativeRelicTraderOffers = offers
        });
    }
}

public sealed partial class PrototypeGameEngine
{
    private static RunState StepNativeRelicTrader(
        RunState state, GameAction action)
    {
        RequireKind(action, "trade_event_relic");
        var world = RequireWorld(state);
        var eventState = world.Event
            ?? throw new InvalidOperationException(
                "Relic Trader has no active event.");
        var offerIndex = action.ReadPayload<TradeEventRelicPayload>()
            .OfferIndex;
        var offers = eventState.NativeRelicTraderOffers
            ?? throw new InvalidOperationException(
                "Relic Trader offers are missing.");
        if ((uint)offerIndex >= (uint)offers.Length)
        {
            throw new InvalidOperationException(
                $"Relic Trader offer {offerIndex} is unavailable.");
        }

        var offer = offers[offerIndex];
        var allowsCircletDuplicates =
            offer.NewRelicId == "proto.relic.circlet";
        if ((uint)offer.OwnedRelicIndex >= (uint)state.Player.Relics.Length
            || state.Player.Relics[offer.OwnedRelicIndex].RelicId
                != offer.OwnedRelicId
            || !PrototypeNativeRelicTrader.IsTradable(
                state.Player.Relics[offer.OwnedRelicIndex])
            || !allowsCircletDuplicates
                && state.Player.Relics.Any(relic =>
                    relic.RelicId == offer.NewRelicId))
        {
            throw new InvalidOperationException(
                "Relic Trader's saved exchange no longer matches the player's relics.");
        }

        var relics = state.Player.Relics
            .Where((_, index) => index != offer.OwnedRelicIndex)
            .Append(new RelicInstance(
                offer.NewRelicId, PrototypeJson.EmptyObject()))
            .ToArray();
        var player = state.Player with { Relics = relics };
        var nextCardInstanceId = world.NextCardInstanceId;
        var nativeRunRng = state.NativeRunRng?.Fork();
        player = ApplyRelicRunEvent(
            player,
            PrototypeRunEventKind.RelicAcquired,
            acquiredRelicId: offer.NewRelicId,
            rng: state.Rng,
            world: world,
            allocateCardId: () => nextCardInstanceId++,
            nativeRunRng: nativeRunRng);

        state = state with
        {
            Player = player,
            NativeRunRng = nativeRunRng ?? state.NativeRunRng,
            World = world with
            {
                NextCardInstanceId = nextCardInstanceId,
                Event = eventState with
                {
                    ChosenChoiceId = offer.ChoiceId,
                    NativeRelicTraderOffers = null
                }
            }
        };
        return AdvanceEventContinuations(state);
    }
}
