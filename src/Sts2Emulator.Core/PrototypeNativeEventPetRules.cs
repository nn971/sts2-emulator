namespace Sts2Emulator.Core;

/// <summary>Shared source rule used by regional events that depend on Player.HasEventPet.</summary>
internal static class PrototypeNativeEventPetRules
{
    public static bool HasEventPet(PlayerState player) =>
        player.Relics.Any(relic => PrototypeContent.Relic(relic.RelicId).AddsPet)
        || player.Deck.Any(card => card.CardId == "proto.native.event.byrdonis_egg");
}
