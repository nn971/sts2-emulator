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
            Type: PrototypeCardType.Skill),
        // Native Purity may select zero through 3 (5 upgraded) cards from
        // hand; each selected card is exhausted with the normal hooks.
        new(
            "proto.colorless.purity",
            "Purity",
            0,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.ChooseCards, 0,
                    Selection: new(
                        PrototypeCardZone.Hand, 0, 3,
                        PrototypeCardSelectionResolutionKind.MoveToExhaust,
                        MaxSelectionsUpgradeDelta: 2))
            ],
            ExhaustOnUse: true,
            Retain: true,
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Skill),
        new(
            "proto.colorless.thinking_ahead",
            "Thinking Ahead",
            0,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.DrawCards, 2),
                new(PrototypeCombatEffectKind.ChooseCards, 0,
                    Selection: new(
                        PrototypeCardZone.Hand, 1, 1,
                        PrototypeCardSelectionResolutionKind.MoveToDrawTop))
            ],
            ExhaustOnUse: true,
            LoseExhaustOnUpgrade: true,
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Skill),
        // The native card disallows combat generation and awards gold only
        // for a target-killing Fatal attack, not for ordinary damage.
        new(
            "proto.colorless.hand_of_greed",
            "Hand of Greed",
            2,
            PrototypeCardTarget.Enemy,
            [
                new(PrototypeCombatEffectKind.DamageEnemy, 20, 5,
                    GoldOnFatal: 20,
                    GoldOnFatalUpgradeDelta: 5)
            ],
            CanBeGeneratedInCombat: false,
            Rarity: PrototypeCardRarity.Rare,
            Type: PrototypeCardType.Attack),
        // Each Panache is an independent five-card counter that resets
        // after the player's turn; its own play does not count.
        new(
            "proto.colorless.panache",
            "Panache",
            0,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.ApplyPlayerPower, 10, 4,
                    PowerId: "proto.power.panache")
            ],
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Power),
        // The native Bomb has an instanced three-turn timer and 40/50
        // unpowered all-enemy damage when the timer expires.
        new(
            "proto.colorless.the_bomb",
            "The Bomb",
            2,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.ApplyPlayerPower, 3,
                    PowerId: "proto.power.the_bomb",
                    PowerStoredValue: 40,
                    PowerStoredValueUpgradeDelta: 10)
            ],
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Skill),
        new(
            "proto.colorless.mayhem",
            "Mayhem",
            new(PrototypeCardCostKind.Fixed, 2, -1),
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.ApplyPlayerPower, 1,
                    PowerId: "proto.power.mayhem")
            ],
            Rarity: PrototypeCardRarity.Rare,
            Type: PrototypeCardType.Power),
        // The initial Block precedes No Block, and the latter only
        // suppresses subsequently card-sourced Block for two enemy turns.
        new(
            "proto.colorless.panic_button",
            "Panic Button",
            0,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.GainPlayerBlock, 30, 10),
                new(PrototypeCombatEffectKind.ApplyPlayerPower, 2,
                    PowerId: "proto.power.no_block")
            ],
            ExhaustOnUse: true,
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Skill),
        // The target temporarily loses Strength, recovered when its
        // side turn ends. Upgrade changes 9 -> 15.
        new(
            "proto.colorless.dark_shackles",
            "Dark Shackles",
            0,
            PrototypeCardTarget.Enemy,
            [
                new(PrototypeCombatEffectKind.ApplyEnemyPower, 9, 6,
                    PowerId: "proto.power.dark_shackles")
            ],
            ExhaustOnUse: true,
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Skill),
        // Source: Discovery.cs. Optional choice of three distinct unlocked
        // character cards, chosen copy free for the remainder of this turn.
        new(
            "proto.colorless.discovery",
            "Discovery",
            1,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.ChooseGeneratedCards, 3,
                    GeneratedChoiceCardsFreeThisTurn: true,
                    GeneratedChoiceMustPick: false)
            ],
            ExhaustOnUse: true,
            LoseExhaustOnUpgrade: true,
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Skill),
        // Source: JackOfAllTrades.cs. Add distinct colorless copies, not
        // a choice; native combat-generation and unlock gates apply.
        new(
            "proto.colorless.jack_of_all_trades",
            "Jack of All Trades",
            0,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.CreateDistinctColorlessCardsInHand,
                    1, 1)
            ],
            ExhaustOnUse: true,
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Skill),
        new(
            "proto.colorless.scrawl",
            "Scrawl",
            1,
            PrototypeCardTarget.None,
            [
                // Draw's normal hand capacity of 10 bounds this request.
                new(PrototypeCombatEffectKind.DrawCards, 10)
            ],
            ExhaustOnUse: true,
            RetainOnUpgrade: true,
            Rarity: PrototypeCardRarity.Rare,
            Type: PrototypeCardType.Skill),
        new(
            "proto.colorless.restlessness",
            "Restlessness",
            0,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.DrawCards, 2, 1,
                    Condition: new(
                        PrototypeCombatPredicateKind.HandEmptyAtEnqueue)),
                new(PrototypeCombatEffectKind.GainEnergy, 2, 1,
                    Condition: new(
                        PrototypeCombatPredicateKind.HandEmptyAtEnqueue))
            ],
            Retain: true,
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Skill),
        new(
            "proto.colorless.prowess",
            "Prowess",
            1,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.ApplyPlayerPower, 1, 1,
                    PowerId: "proto.power.strength"),
                new(PrototypeCombatEffectKind.ApplyPlayerPower, 1, 1,
                    PowerId: "proto.power.dexterity")
            ],
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Power),
        new(
            "proto.colorless.equilibrium",
            "Equilibrium",
            2,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.GainPlayerBlock, 13, 3),
                new(PrototypeCombatEffectKind.ApplyPlayerPower, 1,
                    PowerId: "proto.power.retain_hand")
            ],
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Skill),
        new(
            "proto.colorless.production",
            "Production",
            0,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.GainEnergy, 2, 1)
            ],
            ExhaustOnUse: true,
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Skill),
        new(
            "proto.colorless.prolong",
            "Prolong",
            0,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.ApplyPlayerPower, 0,
                    CountKind: PrototypeCombatCountKind.PlayerBlock,
                    AmountPerCount: 1,
                    PowerId: "proto.power.block_next_turn")
            ],
            ExhaustOnUse: true,
            LoseExhaustOnUpgrade: true,
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Skill),
        new(
            "proto.colorless.salvo",
            "Salvo",
            1,
            PrototypeCardTarget.Enemy,
            [
                new(PrototypeCombatEffectKind.DamageEnemy, 12, 4),
                new(PrototypeCombatEffectKind.ApplyPlayerPower, 1,
                    PowerId: "proto.power.retain_hand")
            ],
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Attack),
        new(
            "proto.colorless.seeker_strike",
            "Seeker Strike",
            1,
            PrototypeCardTarget.Enemy,
            [
                new(PrototypeCombatEffectKind.DamageEnemy, 9, 3),
                new(PrototypeCombatEffectKind.ChooseCards, 0,
                    Selection: new(
                        PrototypeCardZone.DrawPile, 1, 1,
                        PrototypeCardSelectionResolutionKind.MoveToHand,
                        RandomCandidateCount: 3))
            ],
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Attack,
            Tags: ["Strike"]),
        // Native Volley is an untargeted X-cost attack: each X point
        // generates an independent hit with a fresh random enemy target.
        new(
            "proto.colorless.volley",
            "Volley",
            new(PrototypeCardCostKind.X),
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.DamageEnemy, 10, 4,
                    Target: PrototypeEffectTarget.RandomEnemy,
                    Repetitions: 0,
                    RepetitionsPerEnergySpent: 1)
            ],
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Attack),
        // Native Splash is an optional three-attack discovery. The
        // generated card becomes free this turn; Splash+ upgrades it.
        new(
            "proto.colorless.splash",
            "Splash",
            1,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.ChooseGeneratedCards, 3,
                    GeneratedChoiceCardType: PrototypeCardType.Attack,
                    GeneratedChoiceCardsFreeThisTurn: true,
                    GeneratedChoiceCardsUpgradeWithSource: true)
            ],
            Rarity: PrototypeCardRarity.Rare,
            Type: PrototypeCardType.Skill),
        // Native Anointed moves randomly chosen Rare cards straight from
        // draw pile into hand, bounded by remaining hand capacity.
        new(
            "proto.colorless.anointed",
            "Anointed",
            1,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.MoveRandomRareDrawCardsToHand,
                    10)
            ],
            ExhaustOnUse: true,
            RetainOnUpgrade: true,
            Rarity: PrototypeCardRarity.Rare,
            Type: PrototypeCardType.Skill),
        // Native Gold Axe counts completed card plays over the *entire*
        // combat, including earlier turns. Its own play counts afterward.
        new(
            "proto.colorless.gold_axe",
            "Gold Axe",
            1,
            PrototypeCardTarget.Enemy,
            [
                new(PrototypeCombatEffectKind.DamageEnemy, 0,
                    CountKind: PrototypeCombatCountKind.CardsPlayedThisCombat,
                    AmountPerCount: 1)
            ],
            RetainOnUpgrade: true,
            Rarity: PrototypeCardRarity.Rare,
            Type: PrototypeCardType.Attack),
        // Fisticuffs gains Block equal to resolved attack damage,
        // including blocked and overkill portions of a successful hit.
        new(
            "proto.colorless.fisticuffs", "Fisticuffs", 1,
            PrototypeCardTarget.Enemy,
            [new(PrototypeCombatEffectKind.DamageEnemy, 7, 2,
                GainBlockEqualToAttackDamage: true)],
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Attack),
        // These two attacks return from their current piles at the next
        // BeforeHandDraw if that physical card was played last turn.
        new(
            "proto.colorless.bolas", "Bolas", 0,
            PrototypeCardTarget.Enemy,
            [new(PrototypeCombatEffectKind.DamageEnemy, 3, 1)],
            Rarity: PrototypeCardRarity.Rare,
            Type: PrototypeCardType.Attack),
        new(
            "proto.colorless.thrumming_hatchet", "Thrumming Hatchet", 1,
            PrototypeCardTarget.Enemy,
            [new(PrototypeCombatEffectKind.DamageEnemy, 11, 3)],
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Attack),
        // Randomly grant a draw-pile card two (three upgraded) extra replays.
        new(
            "proto.colorless.hidden_gem", "Hidden Gem", 1,
            PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.EmpowerRandomDrawCardReplay, 2, 1)],
            CanBeGeneratedInCombat: false,
            Rarity: PrototypeCardRarity.Rare,
            Type: PrototypeCardType.Skill),
        // For every current non-temporary enemy debuff, Rend gains
        // five damage (+three per debuff after upgrade), atop its base.
        new(
            "proto.colorless.rend", "Rend", 1,
            PrototypeCardTarget.Enemy,
            [new(PrototypeCombatEffectKind.DamageEnemy, 10, 2,
                CountKind: PrototypeCombatCountKind.TargetDebuffs,
                AmountPerCount: 5,
                AmountPerCountUpgradeDelta: 3)],
            Rarity: PrototypeCardRarity.Rare,
            Type: PrototypeCardType.Attack),
        // Native Jackpot creates three zero-cost character cards
        // (with replacement), upgraded when Jackpot itself is upgraded.
        new(
            "proto.colorless.jackpot", "Jackpot", 3,
            PrototypeCardTarget.Enemy,
            [
                new(PrototypeCombatEffectKind.DamageEnemy, 25, 5),
                new(PrototypeCombatEffectKind.AddRandomZeroCostCardsToHand, 3,
                    GeneratedCardUpgradePerSourceUpgrade: 1)
            ],
            Rarity: PrototypeCardRarity.Rare,
            Type: PrototypeCardType.Attack),
        new(
            "proto.colorless.fasten", "Fasten", 1,
            PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.ApplyPlayerPower, 4, 2,
                PowerId: "proto.power.fasten")],
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Power),
        new(
            "proto.colorless.prep_time", "Prep Time", 1,
            PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.ApplyPlayerPower, 4, 2,
                PowerId: "proto.power.prep_time")],
            Rarity: PrototypeCardRarity.Uncommon,
            Type: PrototypeCardType.Power)
    ];

    public static string[] ImplementedShopPool { get; } =
        Implemented.Select(c => c.Id).ToArray();

    // Native Colorless1..5Epoch.Cards (pinned v0.111.0).
    // Until unlock state is available, combat generation deliberately
    // uses the conservative base-pool subset. This does not reproduce a
    // fully-unlocked game and should not be used for RNG parity tests.
    public static string[] EpochGatedIds { get; } =
    [
        "proto.colorless.automation",
        "proto.colorless.entropy",
        "proto.colorless.catastrophe",
        "proto.colorless.eternal_armor",
        "proto.colorless.jackpot",
        "proto.colorless.prep_time",
        "proto.colorless.rend",
        "proto.colorless.beat_down",
        "proto.colorless.prowess",
        "proto.colorless.alchemize",
        "proto.colorless.nostalgia",
        "proto.colorless.scrawl",
        "proto.colorless.splash",
        "proto.colorless.anointed",
        "proto.colorless.calamity"
    ];

    public static string[] ImplementedCombatGenerationPool { get; } =
        NativePoolIds.Where(id =>
            Implemented.Any(card => card.Id == id
                && card.CanBeGeneratedInCombat
                && card.MechanicsImplemented
                && !card.MultiplayerOnly
                && card.Rarity is
                    PrototypeCardRarity.Uncommon or PrototypeCardRarity.Rare)
            && !EpochGatedIds.Contains(id, StringComparer.Ordinal))
        .ToArray();

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
