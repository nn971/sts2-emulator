namespace Sts2Emulator.Core;

/// <summary>
/// Source-shaped Byrdonis Egg rest-site lifecycle. The native
/// HatchRestSiteOption acquires Byrdpip, whose AfterObtained converts
/// all Byrdonis Eggs in the deck to Byrd Swoops. Byrdpip also summons
/// a non-attacking pet; its visible animation/skin is intentionally
/// omitted from canonical decision state.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private const string ByrdonisEggId =
        "proto.native.event.byrdonis_egg";
    private const string ByrdSwoopId =
        "proto.native.event.byrd_swoop";
    private const string ByrdpipRelicId =
        "proto.native.event.byrdpip";

    private static bool CanHatchByrdonisEgg(PlayerState player) =>
        player.Deck.Any(card => card.CardId == ByrdonisEggId)
        && !player.Relics.Any(relic => relic.RelicId == ByrdpipRelicId);

    private static PlayerState HatchByrdonisEgg(
        PlayerState player, RngBundle rng)
    {
        if (!CanHatchByrdonisEgg(player))
        {
            throw new InvalidOperationException(
                "Hatching requires a Byrdonis Egg and no Byrdpip relic.");
        }

        player = player with
        {
            Relics =
            [
                .. player.Relics,
                new RelicInstance(ByrdpipRelicId, PrototypeJson.EmptyObject())
            ]
        };
        player = ApplyRelicRunEvent(
            player, PrototypeRunEventKind.RelicAcquired,
            acquiredRelicId: ByrdpipRelicId, rng: rng);

        return player with
        {
            Deck = player.Deck.Select(card =>
                card.CardId == ByrdonisEggId
                    ? new CardInstance(
                        card.InstanceId, ByrdSwoopId,
                        0, PrototypeJson.EmptyObject())
                    : card).ToArray()
        };
    }
}
