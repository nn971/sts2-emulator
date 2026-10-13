namespace Sts2Emulator.Core;

/// <summary>
/// Shared v0.111.0 relic behavior used by configured Silent runs.
/// </summary>
public static class PrototypeNativeSharedRelics
{
    public const string AmethystAubergineId =
        "proto.relic.amethyst_aubergine";
    public const string BreadId = "proto.relic.bread";
    public const string BeatingRemnantId = "proto.relic.beating_remnant";
    public const string GorgetId = "proto.relic.gorget";
    public const string TinyMailboxId = "proto.relic.tiny_mailbox";
    public const string WhiteStarId = "proto.relic.white_star";
    public const string AkabekoId = "proto.relic.akabeko";
    public const string BellowsId = "proto.relic.bellows";
    public const string CandelabraId = "proto.relic.candelabra";
    public const string ChandelierId = "proto.relic.chandelier";
    public const string FestivePopperId = "proto.relic.festive_popper";
    public const string GamePieceId = "proto.relic.game_piece";
    public const string RegalPillowId = "proto.relic.regal_pillow";
    public const string CloakClaspId = "proto.relic.cloak_clasp";
    public const string RingingTriangleId = "proto.relic.ringing_triangle";
    public const string CentennialPuzzleId =
        "proto.relic.centennial_puzzle";
    public const string EternalFeatherId = "proto.relic.eternal_feather";
    public const string JuzuBraceletId = "proto.relic.juzu_bracelet";
    public const string LizardTailId = "proto.relic.lizard_tail";
    public const string SlingOfCourageId =
        "proto.relic.sling_of_courage";
    public const string PearId = "proto.relic.pear";
    public const string PantographId = "proto.relic.pantograph";
    public const string PetrifiedToadId = "proto.relic.petrified_toad";
    public const string RedMaskId = "proto.relic.red_mask";
    public const string StoneCalendarId =
        "proto.relic.stone_calendar";
    public const string PotionShapedRockId = "proto.potion.shaped_rock";
    public const string StrawberryId = "proto.relic.strawberry";
    public const string TheBootId = "proto.relic.the_boot";
    public const string TungstenRodId = "proto.relic.tungsten_rod";
    public const string VenerableTeaSetId =
        "proto.relic.venerable_tea_set";
    public const string WarPaintId = "proto.relic.war_paint";
    public const string WhetstoneId = "proto.relic.whetstone";
    public const string VigorId = "proto.power.vigor";

    public static PrototypeRelicDefinition[] Relics { get; } =
    [
        // Tiny Mailbox offers two random potions after a Rest heal.
        new(TinyMailboxId, "Tiny Mailbox",
            RestHealPotionRewardCount: 2),
        // Beating Remnant caps the owner's HP loss at 20 per combat turn.
        new(BeatingRemnantId, "Beating Remnant",
            PlayerHpLossLimitPerTurn: 20),
        // White Star adds one separate all-Rare card reward after elites.
        new(WhiteStarId, "White Star",
            ExtraEliteRareCardRewardCount: 1),
        // Bread removes 2 Energy on turn one, then grants 1 each later turn.
        new(BreadId, "Bread",
            EnergyPerTurnBonus: 1,
            EnergyBonusMinTurn: 2,
            Triggers:
            [
                new(PrototypeCombatEventKind.PlayerSideTurnStarted,
                    [new(PrototypeCombatEffectKind.LoseEnergy, 2)],
                    TurnEquals: 1)
            ]),
        // Gorget grants 4 Plating when combat starts.
        new(GorgetId, "Gorget",
            Triggers:
            [
                new(PrototypeCombatEventKind.CombatStarted,
                    [new(PrototypeCombatEffectKind.ApplyPlayerPower, 4,
                        PowerId: "proto.power.plating")])
            ]),
        // Amethyst Aubergine adds a separate 15 Gold reward after fights,
        // except for the final Glory boss.
        new(AmethystAubergineId, "Amethyst Aubergine",
            CombatVictoryGoldReward: 15),
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
        // Bellows upgrades every upgradeable card in hand at the owner's
        // first turn start of each combat.
        new(BellowsId, "Bellows",
            Triggers:
            [
                new(PrototypeCombatEventKind.PlayerSideTurnStarted,
                    [new(PrototypeCombatEffectKind.UpgradeHandCards, 0)],
                    TurnEquals: 1)
            ]),
        // Candelabra grants two Energy when the owner's second turn starts.
        new(CandelabraId, "Candelabra",
            Triggers:
            [
                new(PrototypeCombatEventKind.PlayerSideTurnStarted,
                    [new(PrototypeCombatEffectKind.GainEnergy, 2)],
                    TurnEquals: 2)
            ]),
        // Chandelier grants three Energy when the owner's third turn starts.
        new(ChandelierId, "Chandelier",
            Triggers:
            [
                new(PrototypeCombatEventKind.PlayerSideTurnStarted,
                    [new(PrototypeCombatEffectKind.GainEnergy, 3)],
                    TurnEquals: 3)
            ]),
        // Festive Popper deals nine unpowered damage to all enemies on turn one.
        new(FestivePopperId, "Festive Popper",
            Triggers:
            [
                new(PrototypeCombatEventKind.PlayerSideTurnStarted,
                    [new(PrototypeCombatEffectKind.DamageEnemy, 9,
                        Target: PrototypeEffectTarget.AllEnemies)],
                    TurnEquals: 1)
            ]),
        // Game Piece draws one card after each Power card the owner plays.
        new(GamePieceId, "Game Piece",
            Triggers:
            [
                new(PrototypeCombatEventKind.CardPlayed,
                    [new(PrototypeCombatEffectKind.DrawCards, 1)],
                    RequiredSourceCardType: PrototypeCardType.Power,
                    RequiresPlayerSide: true)
            ]),
        // Regal Pillow adds 15 HP to each rest-site heal.
        new(RegalPillowId, "Regal Pillow", RestSiteHealBonus: 15),
        // Cloak Clasp grants one unpowered Block per card in hand at turn end.
        new(CloakClaspId, "Cloak Clasp",
            Triggers:
            [
                new(PrototypeCombatEventKind.PlayerTurnEnded,
                    [new(PrototypeCombatEffectKind.GainPlayerBlock, 0,
                        CountKind: PrototypeCombatCountKind.OtherCardsInHand,
                        AmountPerCount: 1)],
                    RequiresPlayerSide: true)
            ]),
        // Ringing Triangle keeps the owner's first-turn hand through
        // the enemy turn; later turns flush it normally.
        new(RingingTriangleId, "Ringing Triangle",
            PreventsFirstTurnHandDiscard: true),
        // CentennialPuzzle draws three cards after the owner's first
        // unblocked damage callback in each combat.
        new(CentennialPuzzleId, "Centennial Puzzle",
            Triggers:
            [
                new(PrototypeCombatEventKind.PlayerDamaged,
                    [new(PrototypeCombatEffectKind.DrawCards, 3)],
                    MaxTriggersPerCounterWindow: 1)
            ]),
        // TungstenRod.ModifyHpLostAfterOsty reduces every HP loss to its owner by 1.
        new(TungstenRodId, "Tungsten Rod",
            PlayerHpLossReduction: 1),
        // TheBoot.ModifyHpLostAfterOstyLate raises an owner-powered attack's
        // unblocked HP loss to 5. The event-rarity relic remains outside the
        // ordinary relic pool and is awarded by Trash Heap.
        new(TheBootId, "The Boot", MinPoweredAttackHpLoss: 5),
        new(VenerableTeaSetId, "Venerable Tea Set"),
        new(LizardTailId, "Lizard Tail"),
        new(SlingOfCourageId, "Sling of Courage",
            StrengthOnEliteRoomEntry: 2),
        new(RedMaskId, "Red Mask",
            Triggers:
            [
                new(PrototypeCombatEventKind.PlayerSideTurnStarted,
                    [new(PrototypeCombatEffectKind.ApplyEnemyPower, 1,
                        Target: PrototypeEffectTarget.AllEnemies,
                        PowerId: "proto.power.weak")],
                    TurnEquals: 1)
            ]),
        new(StoneCalendarId, "Stone Calendar",
            Triggers:
            [
                new(PrototypeCombatEventKind.PlayerTurnEnded,
                    [new(PrototypeCombatEffectKind.DamageEnemy, 52,
                        Target: PrototypeEffectTarget.AllEnemies)],
                    TurnEquals: 7)
            ])
    ];
}
