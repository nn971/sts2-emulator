namespace Sts2Emulator.Core;

/// <summary>
/// Source-backed Silent relic behavior for the configured single-player
/// profile. Source: Flaaax/STS2-source at the pinned v0.111.0 commit,
/// Relics/TwistedFunnel.cs.
/// </summary>
public static class PrototypeNativeSilentRelics
{
    public const string TwistedFunnelId = "proto.relic.twisted_funnel";

    public static PrototypeRelicDefinition[] Relics { get; } =
    [
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
}
