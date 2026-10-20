namespace Sts2Emulator.Core;

/// <summary>
/// Source-backed Silent relic behavior for the configured single-player
/// profile. Source: Flaaax/STS2-source at the pinned v0.111.0 commit,
/// Relics/TwistedFunnel.cs.
/// </summary>
public static class PrototypeNativeSilentRelics
{
    public const string HelicalDartId = "proto.relic.helical_dart";
    public const string NinjaScrollId = "proto.relic.ninja_scroll";
    public const string PaperKraneId = "proto.relic.paper_krane";
    public const string SneckoSkullId = "proto.relic.snecko_skull";
    public const string TwistedFunnelId = "proto.relic.twisted_funnel";

    private static readonly PrototypeRelicDefinition[] RelicDefinitions =
    [
        // Native AfterCardPlayed adds Temporary Dexterity for each Shiv.
        new(HelicalDartId, "Helical Dart",
            Triggers:
            [
                new(PrototypeCombatEventKind.CardPlayed,
                    [new(PrototypeCombatEffectKind.ApplyPlayerPower, 1,
                        PowerId: "proto.power.temporary_dexterity")],
                    RequiredSourceCardTag: "Shiv")
            ]),
        // Native additive power modification adds one Poison whenever the
        // owner applies Poison to an enemy.
        new(SneckoSkullId, "Snecko Skull",
            AppliedEnemyStatusAmountBonusId: "proto.status.poison",
            AppliedEnemyStatusAmountBonus: 1),
        // Native ModifyWeakMultiplier subtracts fifteen points for attacks
        // that target the Silent while an enemy has Weak.
        new(PaperKraneId, "Paper Krane",
            WeakAttackDamageReductionPercent: 15),
        // Native BeforeHandDraw creates three Shivs on the first player turn.
        new(NinjaScrollId, "Ninja Scroll",
            Triggers:
            [
                new(PrototypeCombatEventKind.BeforeHandDraw,
                    [new(PrototypeCombatEffectKind.CreateCardsInHand, 3,
                        CardId: "proto.silent.shiv")],
                    TurnEquals: 1)
            ]),
        // Native BeforeSideTurnStart applies 4 Poison to every hittable enemy
        // during the owner's first turn. CombatStarted is the shared engine
        // boundary immediately before that opening turn in this profile.
        new(TwistedFunnelId, "Twisted Funnel",
            Triggers:
            [
                new(PrototypeCombatEventKind.CombatStarted,
                    [new(PrototypeCombatEffectKind.ApplyEnemyStatus, 4,
                        StatusId: "proto.status.poison",
                        Target: PrototypeEffectTarget.AllEnemies)])
            ])
    ];

    public static PrototypeRelicDefinition[] Relics => RelicDefinitions
        .Select(PrototypeDefinitionCopiesV111.CloneRelicDefinition)
        .ToArray();
}
