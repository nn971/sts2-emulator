namespace Sts2Emulator.Core;

/// <summary>
/// Shared v0.111.0 relic behavior used by configured Silent runs.
/// </summary>
public static class PrototypeNativeSharedRelics
{
    public const string AkabekoId = "proto.relic.akabeko";
    public const string EternalFeatherId = "proto.relic.eternal_feather";
    public const string JuzuBraceletId = "proto.relic.juzu_bracelet";
    public const string PearId = "proto.relic.pear";
    public const string PantographId = "proto.relic.pantograph";
    public const string PetrifiedToadId = "proto.relic.petrified_toad";
    public const string PotionShapedRockId = "proto.potion.shaped_rock";
    public const string StrawberryId = "proto.relic.strawberry";
    public const string TheBootId = "proto.relic.the_boot";
    public const string WarPaintId = "proto.relic.war_paint";
    public const string WhetstoneId = "proto.relic.whetstone";
    public const string VigorId = "proto.power.vigor";

    public static PrototypeRelicDefinition[] Relics { get; } =
    [
        // Pantograph.BeforeCombatStart heals 25 when the current room is Boss.
        new(PantographId, "Pantograph", HealBeforeBossCombat: 25),
        // JuzuBracelet.ModifyUnknownMapPointRoomTypes removes Monster.
        new(JuzuBraceletId, "Juzu Bracelet",
            ExcludesMonsterRoomsFromUnknownMap: true),
        // PetrifiedToad.BeforeCombatStartLate tries to procure a token
        // Potion Shaped Rock when a potion slot is open.
        new(PetrifiedToadId, "Petrified Toad",
            PotionIdBeforeCombat: PotionShapedRockId),
        // EternalFeather.AfterRoomEntered heals 3 per five cards on deck.
        new(EternalFeatherId, "Eternal Feather",
            RestSiteHealPerFiveCards: 3),
        // WarPaint.AfterObtained randomly upgrades up to two Skills.
        new(WarPaintId, "War Paint",
            UpgradeRandomCardsOnAcquisitionCount: 2,
            UpgradeRandomCardsOnAcquisitionType: PrototypeCardType.Skill),
        // Whetstone.AfterObtained randomly upgrades up to two Attacks.
        new(WhetstoneId, "Whetstone",
            UpgradeRandomCardsOnAcquisitionCount: 2,
            UpgradeRandomCardsOnAcquisitionType: PrototypeCardType.Attack),
        // Pear.AfterObtained grants ten Max HP.
        new(PearId, "Pear",
            RunTriggers:
            [
                new(PrototypeRunEventKind.RelicAcquired,
                    [new(PrototypeRunEffectKind.GainMaxHp, 10)])
            ]),
        // Strawberry.AfterObtained grants seven Max HP; GainMaxHp also
        // raises current HP by the same amount in the run model.
        new(StrawberryId, "Strawberry",
            RunTriggers:
            [
                new(PrototypeRunEventKind.RelicAcquired,
                    [new(PrototypeRunEffectKind.GainMaxHp, 7)])
            ]),
        // Akabeko.AfterSideTurnStart grants 8 Vigor on the owner's first
        // turn. PlayerSideTurnStarted is the corresponding combat boundary.
        new(AkabekoId, "Akabeko",
            Triggers:
            [
                new(PrototypeCombatEventKind.PlayerSideTurnStarted,
                    [new(PrototypeCombatEffectKind.ApplyPlayerPower, 8,
                        PowerId: VigorId)],
                    TurnEquals: 1)
            ]),
        // TheBoot.ModifyHpLostAfterOstyLate raises an owner-powered attack's
        // unblocked HP loss to 5. The event-rarity relic remains outside the
        // ordinary relic pool and is awarded by Trash Heap.
        new(TheBootId, "The Boot", MinPoweredAttackHpLoss: 5)
    ];
}
