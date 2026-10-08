namespace Sts2Emulator.Core;

/// <summary>
/// v0.111.0 Underdocks single-player Act 1 content inventory.
/// The entire native weak encounter set is now playable. Normal,
/// elite and boss content remains explicitly gated rather than replaced
/// with Overgrowth content.
/// </summary>
public static class PrototypeNativeUnderdocks
{
    public const string GenerationProfileId =
        "native-underdocks-map-structure-v0.111.0-v1";

    // Source: Underdocks.GenerateAllEncounters() and IsWeak.
    public static string[] NativeWeakEncounterIds { get; } =
    [
        "proto.encounter.corpse_slugs_weak",
        "proto.encounter.seapunk_weak",
        "proto.encounter.sludge_spinner_weak",
        "proto.encounter.toadpoles_weak"
    ];

    public static string[] NativeNormalEncounterIds { get; } =
    [
        "proto.encounter.corpse_slugs_normal",
        "proto.encounter.cultists_normal",
        "proto.encounter.fossil_stalker_normal",
        "proto.encounter.gremlin_merc_normal",
        "proto.encounter.haunted_ship_normal",
        "proto.encounter.living_fog_normal",
        "proto.encounter.punch_construct_normal",
        "proto.encounter.seapunk_normal",
        "proto.encounter.sewer_clam_normal",
        "proto.encounter.two_tailed_rats_normal"
    ];

    public static string[] NativeEliteEncounterIds { get; } =
    [
        "proto.encounter.phantasmal_gardeners_elite",
        "proto.encounter.skulking_colony_elite",
        "proto.encounter.terror_eel_elite"
    ];

    public static string[] NativeBossEncounterIds { get; } =
    [
        "proto.encounter.waterfall_giant_boss",
        "proto.encounter.soul_fysh_boss",
        "proto.encounter.lagavulin_matriarch_boss"
    ];

    public static string[] SupportedWeakEncounterIds { get; } =
    [
        "proto.encounter.corpse_slugs_weak",
        "proto.encounter.seapunk_weak",
        "proto.encounter.sludge_spinner_weak",
        "proto.encounter.toadpoles_weak"
    ];

    public static PrototypePowerDefinition[] Powers { get; } =
    [
        // Native RavenousPower.AfterDeath: when another ally dies, gain
        // Strength equal to Ravenous stacks and replace the next action
        // with one stunned turn. Death of the owner never triggers itself.
        new(
            "proto.power.ravenous",
            "Ravenous",
            BlockBonusPerStack: 0,
            Triggers: [],
            AllyDeathStrengthPerStack: 1,
            StunOnAllyDeath: true)
    ];

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        // Native CorpseSlug: 25–27 HP (A8: 27–29), rotating
        // whip/slap, glomp, goop. The encounter coordinates openers.
        new(
            "proto.enemy.corpse_slug",
            "Corpse Slug",
            27,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "whip_slap",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 3,
                        Repetitions: 2)]),
                new PrototypeEnemyMoveDefinition(
                    "glomp",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 8,
                        AscensionDeltas: [new(9, 1)])]),
                new PrototypeEnemyMoveDefinition(
                    "goop",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.ApplyPlayerPower, 2,
                        PowerId: "proto.power.frail")])
            ],
            StartingPowers:
            [
                new("proto.power.ravenous", 4,
                    [new(9, 1)])
            ],
            MinHp: 25,
            HpAscensionDeltas: [new(8, 2)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new PrototypeEnemyAiDefinition(
                "whip",
                [
                    new PrototypeEnemyAiStateDefinition(
                        "whip", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "glomp"),
                    new PrototypeEnemyAiStateDefinition(
                        "glomp", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "goop"),
                    new PrototypeEnemyAiStateDefinition(
                        "goop", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 2, NextStateId: "whip")
                ])),
        // Native SludgeSpinner: 37–39 HP (A8: 41–42).
        // Forced Oil Spray, then uniform no-immediate-repeat branch
        // between Oil Spray, Slam and Rage.
        new(
            "proto.enemy.sludge_spinner",
            "Sludge Spinner",
            39,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "oil_spray",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 8,
                            AscensionDeltas: [new(9, 1)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyPlayerPower, 1,
                            PowerId: "proto.power.weak")
                    ]),
                new PrototypeEnemyMoveDefinition(
                    "slam",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 11,
                        AscensionDeltas: [new(9, 1)])]),
                new PrototypeEnemyMoveDefinition(
                    "rage",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 6,
                            AscensionDeltas: [new(9, 1)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyEnemyPower, 3,
                            PowerId: "proto.power.strength")
                    ])
            ],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            HpAscensionDeltas: [new(8, 3)],
            MinHp: 37,
            MinHpAscensionDeltas: [new(8, 4)],
            Ai: new PrototypeEnemyAiDefinition(
                "oil",
                [
                    new PrototypeEnemyAiStateDefinition(
                        "oil", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "random"),
                    new PrototypeEnemyAiStateDefinition(
                        "slam", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "random"),
                    new PrototypeEnemyAiStateDefinition(
                        "rage", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 2, NextStateId: "random"),
                    new PrototypeEnemyAiStateDefinition(
                        "random", PrototypeEnemyAiStateKind.Random,
                        Branches:
                        [
                            new("oil",
                                RepeatRule: PrototypeEnemyAiRepeatRule.CannotRepeat),
                            new("slam",
                                RepeatRule: PrototypeEnemyAiRepeatRule.CannotRepeat),
                            new("rage",
                                RepeatRule: PrototypeEnemyAiRepeatRule.CannotRepeat)
                        ])
                ]))
    ];

    // Pinned native encounter membership and formation. All encounters
    // have Weight=0 outside the opt-in Underdocks pool.
    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        new(
            "proto.encounter.corpse_slugs_weak",
            PrototypeRoomType.Combat,
            ["proto.enemy.corpse_slug", "proto.enemy.corpse_slug"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation:
            [
                new("proto.enemy.corpse_slug", 0),
                new("proto.enemy.corpse_slug", 1)
            ],
            CyclicOpeningAiStateIds: ["whip", "glomp", "goop"]),
        new(
            "proto.encounter.seapunk_weak",
            PrototypeRoomType.Combat,
            ["proto.enemy.seapunk"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation: [new("proto.enemy.seapunk", 0)]),
        new(
            "proto.encounter.sludge_spinner_weak",
            PrototypeRoomType.Combat,
            ["proto.enemy.sludge_spinner"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation: [new("proto.enemy.sludge_spinner", 0)]),
        new(
            "proto.encounter.toadpoles_weak",
            PrototypeRoomType.Combat,
            ["proto.enemy.toadpole", "proto.enemy.toadpole"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation:
            [
                new("proto.enemy.toadpole", 0),
                new("proto.enemy.toadpole", 1)
            ])
    ];
}

/// <summary>
/// Opt-in native-shaped Act 1 Underdocks start. Reuses the Act 1
/// StandardActMap approximation and the shared Neow opening, not
/// Overgrowth encounter or event pools. Source v0.111.0: both
/// Act 1 regions have 15 map rooms and three initial weak fights.
/// </summary>
public static class PrototypeNativeUnderdocksRunFactory
{
    public static RunState Create(string seed, int ascension = 0)
    {
        // The existing native-shaped Act 1 starter handles Neow, 13-card
        // starter deck, Ascension, potion slots and the shared map geometry.
        var baseState = PrototypeNativeOvergrowthRunFactory.Create(
            seed, ascension);
        var world = baseState.World
            ?? throw new InvalidOperationException("Missing Act 1 world.");
        var rng = baseState.Rng.Fork();

        // The prototype StartRun had already rolled an Overgrowth boss.
        // Replace its combat stream with the clean initial stream before
        // rolling this region's selected boss, avoiding a hidden extra
        // Overgrowth draw.
        var clean = PrototypeRng.CreateBundle(seed);
        var combatIndex = Array.FindIndex(rng.Streams, stream =>
            stream.StreamId == "combat");
        var cleanCombatIndex = Array.FindIndex(clean.Streams, stream =>
            stream.StreamId == "combat");
        if (combatIndex < 0 || cleanCombatIndex < 0)
        {
            throw new InvalidOperationException("Missing combat RNG.");
        }
        rng.Streams[combatIndex] = clean.Streams[cleanCombatIndex].Fork();
        var bosses = PrototypeNativeUnderdocks.NativeBossEncounterIds;
        var boss = bosses[PrototypeRng.NextInt(
            rng, "combat", bosses.Length)];

        // Both native Act 1 regions use the same StandardActMap room
        // constraints, with a distinct region profile for routing.
        var map = world.Map with
        {
            GenerationProfileId =
                PrototypeNativeUnderdocks.GenerationProfileId
        };
        return baseState with
        {
            Rng = rng,
            World = world with
            {
                ActOneRegion = PrototypeActOneRegion.Underdocks,
                ActOneEncounterPool = new PrototypeActOneEncounterPoolState(
                    PrototypeActOneRegion.Underdocks,
                    OrdinaryCombatsStarted: 0,
                    RemainingWeakEncounterIds:
                        (string[])PrototypeNativeUnderdocks
                            .SupportedWeakEncounterIds.Clone(),
                    RemainingNormalEncounterIds: [],
                    RemainingEliteEncounterIds: [],
                    BossEncounterId: boss),
                Map = map
            }
        };
    }
}
