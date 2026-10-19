namespace Sts2Emulator.Core;

/// <summary>
/// v0.111.0 HistoryCourse.AfterAutoPrePlayPhaseEntered:
/// starting with player turn 2, find the last finished, non-duplicate
/// Attack from the immediately previous player turn, CreateDupe,
/// then CardCmd.AutoPlay(dupe, target: null).
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private static PrototypeCombatCardSnapshot HistoryCourseSnapshot(
        CombatCardInstance card) => new(
        CardId: card.CardId,
        UpgradeLevel: card.UpgradeLevel,
        State: card.State.Clone(),
        CombatEnergyCostDelta: card.CombatEnergyCostDelta,
        TemporaryEnergyCost: card.TemporaryEnergyCost is null
            ? null : card.TemporaryEnergyCost with { },
        KeywordOverrides: card.KeywordOverrides is null
            ? null : card.KeywordOverrides.Select(x => x with { }).ToArray(),
        ReplayCount: card.ReplayCount,
        Enchantment: card.Enchantment,
        EnchantmentTriggeredThisCombat:
            card.EnchantmentTriggeredThisCombat,
        Affliction: card.Affliction,
        SuppressedUpgradeLevels: card.SuppressedUpgradeLevels,
        WitherFakeUpgradeLevel: card.WitherFakeUpgradeLevel);

    private static RunState ResolveHistoryCourseAutoPlay(RunState state)
    {
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException(
                "History Course requires an active combat.");

        if (combat.PendingChoice is not null ||
            combat.Turn <= 1
            || !combat.RelicStates.Any(relic =>
                relic.RelicId == PrototypeNativeHiveEvents.HistoryCourseId)
            || combat.CounterState.LastOriginalAttackLastTurn is not
                { } lastAttack
            || AllEnemiesDefeated(combat))
            return state;

        // A source card can have been exhausted or transformed since
        // the previous turn. Native CreateDupe keeps the card's state,
        // upgrade and enchantment; the duplicate is an entirely new
        // temporary combat card, not a replay of its original instance.
        var duplicate = new CombatCardInstance(
            InstanceId: combat.NextCardInstanceId,
            PersistentCardInstanceId: null,
            CardId: lastAttack.CardId,
            UpgradeLevel: lastAttack.UpgradeLevel,
            IsTemporary: true,
            State: lastAttack.State.Clone(),
            CombatEnergyCostDelta: lastAttack.CombatEnergyCostDelta,
            TemporaryEnergyCost: lastAttack.TemporaryEnergyCost is null
                ? null : lastAttack.TemporaryEnergyCost with { },
            KeywordOverrides: lastAttack.KeywordOverrides is null
                ? null : lastAttack.KeywordOverrides
                    .Select(x => x with { }).ToArray(),
            ReplayCount: lastAttack.ReplayCount,
            Enchantment: lastAttack.Enchantment,
            EnchantmentTriggeredThisCombat:
                lastAttack.EnchantmentTriggeredThisCombat,
            Affliction: lastAttack.Affliction,
            SuppressedUpgradeLevels: lastAttack.SuppressedUpgradeLevels,
            WitherFakeUpgradeLevel: lastAttack.WitherFakeUpgradeLevel,
            IsHistoryCourseDupe: true);
        combat = SemanticDrawPileAdapterV111
            .ApplyNewCardZoneInsertion(
                combat, PrototypeCardZone.PlayPile,
                combat.PlayCardIds.Length, duplicate);

        // AutoPlayCombatCard already implements the native CardCmd:
        // zero energy spent, a random hittable target drawn from
        // combat_targets for AnyEnemy, and the complete play/event
        // pipeline (including Normality/Unplayable guards).
        var result = ResolveOperations(
            state.Player,
            combat,
            new Queue<PrototypeQueuedOperation>(
            [
                new(PrototypeCombatEffectKind.AutoPlayCombatCard, 0,
                    SourceKind: PrototypeEffectSourceKind.Relic,
                    CardInstanceId: duplicate.InstanceId)
            ]),
            state.Rng,
            moveSourceCardOnCompletion: false);

        return state with
        {
            Player = result.Player,
            World = world with { Combat = result.Combat }
        };
    }
}
