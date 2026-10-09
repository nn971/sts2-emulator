namespace Sts2Emulator.Core;

/// <summary>
/// Native v0.111.0 ColorlessCardPool metadata and source-backed
/// playable single-player batches. Catalog membership is NOT a claim that all
/// 65 native cards have implemented mechanics or are solo-unlocked.
/// </summary>
public static class PrototypeColorlessCards
{
    // Exact ColorlessCardPool.GenerateAllCards ordering from the pinned
    // v0.111.0 source. Multiplayer and epoch gates are not applied here.
    public static string[] NativePoolIds { get; } =
    [
        "proto.colorless.alchemize",
        "proto.colorless.anointed",
        "proto.colorless.automation",
        "proto.colorless.beacon_of_hope",
        "proto.colorless.beat_down",
        "proto.colorless.believe_in_you",
        "proto.colorless.bolas",
        "proto.colorless.calamity",
        "proto.colorless.catastrophe",
        "proto.colorless.coordinate",
        "proto.colorless.dark_shackles",
        "proto.colorless.discovery",
        "proto.colorless.dramatic_entrance",
        "proto.colorless.entropy",
        "proto.colorless.equilibrium",
        "proto.colorless.eternal_armor",
        "proto.colorless.fasten",
        "proto.colorless.finesse",
        "proto.colorless.fisticuffs",
        "proto.colorless.flash_of_steel",
        "proto.colorless.gang_up",
        "proto.colorless.gold_axe",
        "proto.colorless.hand_of_greed",
        "proto.colorless.hidden_gem",
        "proto.colorless.huddle_up",
        "proto.colorless.impatience",
        "proto.colorless.intercept",
        "proto.colorless.jack_of_all_trades",
        "proto.colorless.jackpot",
        "proto.colorless.knockdown",
        "proto.colorless.lift",
        "proto.colorless.master_of_strategy",
        "proto.colorless.mayhem",
        "proto.colorless.mimic",
        "proto.colorless.mind_blast",
        "proto.colorless.nostalgia",
        "proto.colorless.omnislice",
        "proto.colorless.panache",
        "proto.colorless.panic_button",
        "proto.colorless.prep_time",
        "proto.colorless.production",
        "proto.colorless.prolong",
        "proto.colorless.prowess",
        "proto.colorless.purity",
        "proto.colorless.rally",
        "proto.colorless.rend",
        "proto.colorless.restlessness",
        "proto.colorless.rolling_boulder",
        "proto.colorless.salvo",
        "proto.colorless.scrawl",
        "proto.colorless.secret_technique",
        "proto.colorless.secret_weapon",
        "proto.colorless.seeker_strike",
        "proto.colorless.shockwave",
        "proto.colorless.splash",
        "proto.colorless.stratagem",
        "proto.colorless.tag_team",
        "proto.colorless.the_ball",
        "proto.colorless.the_bomb",
        "proto.colorless.the_gambit",
        "proto.colorless.thinking_ahead",
        "proto.colorless.thrumming_hatchet",
        "proto.colorless.ultimate_defend",
        "proto.colorless.ultimate_strike",
        "proto.colorless.volley",
    ];

    // Source-backed playable batches, deliberately NOT added to the ordinary
    // Silent combat reward pool. Native merchants expose separate colorless
    // Uncommon and Rare slots; the remaining models require future work.
    public static PrototypeCardDefinition[] Implemented { get; } =
    [
        new(
            "proto.colorless.finesse",
            "Finesse",
            0,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.GainPlayerBlock, 4, 3),
                new(PrototypeCombatEffectKind.DrawCards, 1)
            ],
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Skill),
        new(
            "proto.colorless.flash_of_steel",
            "Flash of Steel",
            0,
            PrototypeCardTarget.Enemy,
            [
                new(PrototypeCombatEffectKind.DamageEnemy, 5, 3),
                new(PrototypeCombatEffectKind.DrawCards, 1)
            ],
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Attack),
        new(
            "proto.colorless.dramatic_entrance",
            "Dramatic Entrance",
            0,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.DamageEnemy, 11, 4,
                    Target: PrototypeEffectTarget.AllEnemies)
            ],
            ExhaustOnUse: true,
            Innate: true,
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Attack),
        new(
            "proto.colorless.ultimate_strike",
            "Ultimate Strike",
            1,
            PrototypeCardTarget.Enemy,
            [new(PrototypeCombatEffectKind.DamageEnemy, 14, 6)],
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Attack,
            Tags: ["Strike"]),
        new(
            "proto.colorless.ultimate_defend",
            "Ultimate Defend",
            1,
            PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.GainPlayerBlock, 11, 4)],
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Skill,
            Tags: ["Defend"]),
        new(
            "proto.colorless.master_of_strategy",
            "Master of Strategy",
            0,
            PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.DrawCards, 3, 1)],
            ExhaustOnUse: true,
            Rarity: PrototypeCardRarity.Rare,
            Type: PrototypeCardType.Skill),
        // Pinned v0.111.0: all enemy targets receive Weak and Vulnerable.
        new(
            "proto.colorless.shockwave",
            "Shockwave",
            2,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.ApplyEnemyStatus, 3, 2,
                    StatusId: "proto.status.weak",
                    Target: PrototypeEffectTarget.AllEnemies),
                new(PrototypeCombatEffectKind.ApplyEnemyStatus, 3, 2,
                    StatusId: "proto.status.vulnerable",
                    Target: PrototypeEffectTarget.AllEnemies)
            ],
            ExhaustOnUse: true,
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Skill),
        // Its hand condition is tested after Impatience enters play.
        new(
            "proto.colorless.impatience",
            "Impatience",
            0,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.DrawCards, 2, 1,
                    Condition: new(
                        PrototypeCombatPredicateKind.HandHasNoAttacks))
            ],
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Skill),
        // Native Mind Blast counts cards in the combat draw pile at play.
        new(
            "proto.colorless.mind_blast",
            "Mind Blast",
            new(PrototypeCardCostKind.Fixed, 1, -1),
            PrototypeCardTarget.Enemy,
            [
                new(PrototypeCombatEffectKind.DamageEnemy, 0,
                    CountKind: PrototypeCombatCountKind.DrawPileCards,
                    AmountPerCount: 1)
            ],
            Innate: true,
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Attack),
        // Draw-pile card selection resumes the normal generic effect queue.
        new(
            "proto.colorless.secret_technique",
            "Secret Technique",
            0,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.ChooseCards, 0,
                    Selection: new(
                        PrototypeCardZone.DrawPile, 1, 1,
                        PrototypeCardSelectionResolutionKind.MoveToHand,
                        RequiredCardType: PrototypeCardType.Skill))
            ],
            ExhaustOnUse: true,
            LoseExhaustOnUpgrade: true,
            Rarity: PrototypeCardRarity.Rare,
            Type: PrototypeCardType.Skill),
        new(
            "proto.colorless.secret_weapon",
            "Secret Weapon",
            0,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.ChooseCards, 0,
                    Selection: new(
                        PrototypeCardZone.DrawPile, 1, 1,
                        PrototypeCardSelectionResolutionKind.MoveToHand,
                        RequiredCardType: PrototypeCardType.Attack))
            ],
            ExhaustOnUse: true,
            LoseExhaustOnUpgrade: true,
            Rarity: PrototypeCardRarity.Rare,
            Type: PrototypeCardType.Skill)
    ];

    public static string[] ImplementedShopPool { get; } =
        Implemented.Select(c => c.Id).ToArray();

    public static string PickMerchantCard(
        PrototypeCardRarity rarity,
        RngBundle rng,
        IReadOnlyCollection<string>? excluded = null)
    {
        if (rarity is not (PrototypeCardRarity.Uncommon or PrototypeCardRarity.Rare))
        {
            throw new ArgumentOutOfRangeException(nameof(rarity));
        }

        var eligible = Implemented
            .Where(c => c.Rarity == rarity
                && !c.MultiplayerOnly
                && c.MechanicsImplemented
                && (excluded is null || !excluded.Contains(c.Id)))
            .Select(c => c.Id)
            .ToArray();
        if (eligible.Length == 0)
        {
            throw new InvalidOperationException(
                $"No playable {rarity} colorless merchant cards.");
        }

        return eligible[PrototypeRng.NextInt(rng, "shop", eligible.Length)];
    }
}
