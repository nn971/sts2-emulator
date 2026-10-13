namespace Sts2Emulator.Core;

/// <summary>
/// The Courier's merchant-wide price modifier and purchase restocking.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private static bool HasTheCourier(PlayerState player) =>
        player.ActiveRelics.Any(relic =>
            relic.RelicId == PrototypeNativeSharedRelics.TheCourierId);

    private static ShopOffer RestockTheCourierCardOffer(
        RunState state,
        RunWorldState world,
        ShopState shop,
        ShopOffer soldOffer)
    {
        var nativeMerchant = UsesNativeRegionalSystems(world);
        var excluded = shop.CardOffers.Select(offer => offer.ItemId)
            .ToHashSet(StringComparer.Ordinal);
        var card = PrototypeContent.Card(soldOffer.ItemId);
        string replacementId;
        if (nativeMerchant && soldOffer.OfferId >= 6)
        {
            replacementId = PrototypeColorlessCards.PickMerchantCard(
                card.Rarity, state.Rng, excluded);
        }
        else if (nativeMerchant)
        {
            replacementId = PrototypeNativeCardRarityOdds
                .GenerateMerchantCards(
                    1, state.Ascension, card.Type,
                    world.CardRarityOffsetBasisPoints,
                    state.Rng, excluded)[0];
        }
        else
        {
            replacementId = PickShopCards(
                world.Act, 1, state.Rng, card.Type)[0];
        }

        var basePrice = nativeMerchant
            ? NativeMerchantCardPrice(replacementId, state.Rng)
            : ShopCardPrice(replacementId, state.Rng);
        return soldOffer with
        {
            ItemId = replacementId,
            Price = basePrice,
            Sold = false,
            BasePrice = basePrice,
            OnSale = false
        };
    }

    private static ShopOffer RestockTheCourierPotionOffer(
        RunState state, ShopOffer soldOffer)
    {
        var nativeMerchant = UsesNativeRegionalSystems(
            RequireWorld(state));
        var potionId = nativeMerchant
            ? PrototypeNativePotionShop.PickWeighted(
                state.Rng, "shop")
            : PickDistinct(
                PrototypeContent.PotionPool, 1, state.Rng, "shop")[0];
        var basePrice = nativeMerchant
            ? PrototypeNativePotionShop.MerchantPrice(
                potionId, state.Rng)
            : 40 + PrototypeRng.NextInt(state.Rng, "shop", 21);
        return soldOffer with
        {
            ItemId = potionId,
            Price = basePrice,
            Sold = false,
            BasePrice = basePrice,
            OnSale = false
        };
    }

    private static (RunWorldState World, ShopOffer? Offer)
        RestockTheCourierRelicOffer(
            RunState state,
            RunWorldState world,
            PlayerState player,
            ShopOffer soldOffer)
    {
        string? relicId;
        int basePrice;
        if (UsesNativeRegionalSystems(world))
        {
            var draw = PrototypeNativeRelicGrabBag.Draw(
                world, player, state.Rng, merchant: true,
                shopEligible: true);
            world = draw.World;
            relicId = draw.Id;
            if (relicId is null)
                return (world, null);
            basePrice = PrototypeNativeRelicGrabBag.MerchantPrice(
                relicId, state.Rng);
        }
        else
        {
            var currentlyOffered = RequireWorld(state).Shop!.RelicOffers
                .Select(offer => offer.ItemId)
                .ToHashSet(StringComparer.Ordinal);
            var available = PrototypeContent.RelicPool
                .Where(id => PrototypeNativeRelicGrabBag.IsShopEligible(id)
                    && !currentlyOffered.Contains(id)
                    && !player.Relics.Any(relic => relic.RelicId == id))
                .ToArray();
            if (available.Length == 0)
                return (world, null);
            relicId = available[PrototypeRng.NextInt(
                state.Rng, "shop", available.Length)];
            basePrice = 100 + PrototypeRng.NextInt(
                state.Rng, "shop", 41);
        }

        return (world, soldOffer with
        {
            ItemId = relicId,
            Price = basePrice,
            Sold = false,
            BasePrice = basePrice,
            OnSale = false
        });
    }
}
