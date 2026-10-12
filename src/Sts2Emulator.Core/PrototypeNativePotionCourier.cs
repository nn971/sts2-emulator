namespace Sts2Emulator.Core;

/// <summary>
/// Potion Courier's later-act event and event-only Foul Potion.
/// </summary>
public static class PrototypeNativePotionCourier
{
    public const string EventId = "proto.native.event.potion_courier";
    public const string FoulPotionId = "proto.native.event.foul_potion";
    public const string GrabPotionsChoiceId = "grab_potions";
    public const string RansackChoiceId = "ransack";

    public static PrototypeEventDefinition Definition { get; } = new(
        EventId,
        "Potion Courier",
        [
            new(
                GrabPotionsChoiceId,
                "Grab Potions",
                [
                    new(PrototypeRunEffectKind.GainPotion,
                        PotionId: FoulPotionId),
                    new(PrototypeRunEffectKind.GainPotion,
                        PotionId: FoulPotionId),
                    new(PrototypeRunEffectKind.GainPotion,
                        PotionId: FoulPotionId)
                ]),
            new(
                RansackChoiceId,
                "Ransack",
                [new(PrototypeRunEffectKind.GainRandomUncommonPotion)])
        ],
        MinAct: 2,
        MaxAct: 3);

    public static PrototypePotionDefinition FoulPotion { get; } = new(
        FoulPotionId,
        "Foul Potion",
        PrototypeCardTarget.None,
        [
            new(
                PrototypeCombatEffectKind.DamageEnemy,
                12,
                Target: PrototypeEffectTarget.AllEnemies),
            new(PrototypeCombatEffectKind.DamagePlayer, 12)
        ]);

    public static string[] ImplementedUncommonPotionPool =>
        PrototypeNativePotionShop.NativePotionPoolOrder
            .Where(id => PrototypeNativePotionShop.TryGetRarity(
                    id, out var rarity)
                && rarity == PrototypeNativePotionRarity.Uncommon
                && PrototypeContent.Potions.ContainsKey(id))
            .ToArray();

    public static string PickRandomUncommonPotion(
        RngBundle rng,
        string streamId)
    {
        if (ImplementedUncommonPotionPool.Length == 0)
            throw new InvalidOperationException(
                "No implemented Uncommon potions are available.");

        return ImplementedUncommonPotionPool[PrototypeRng.NextInt(
            rng, streamId, ImplementedUncommonPotionPool.Length)];
    }
}
