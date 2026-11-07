using System.Globalization;

namespace Sts2Emulator.Core;

/// <summary>Game-rule data only; no private combat state or RNG cursor.</summary>
public sealed record PublicEnemyAiBranchV111(
    string TargetStateId, int Weight, string RepeatRule,
    int MaxTimes, bool RequiresAvailableSummon);

public sealed record PublicEnemyAiConditionalBranchV111(
    string TargetStateId, string Condition, string? Value);

public sealed record PublicEnemyAiStateV111(
    string Id, string Kind, int? MoveIndex, string? NextStateId,
    PublicEnemyAiBranchV111[] Branches,
    PublicEnemyAiConditionalBranchV111[] ConditionalBranches);

public sealed record PublicEnemyAiDefinitionV111(
    string InitialStateId, PublicEnemyAiStateV111[] States);

// Static source definitions only: the next attack preview is evaluated
// against PUBLIC powers and a specified act/ascension. No random draw or
// canonical EnemyCombatState is needed to export these templates.
public sealed record PublicEnemyDamageEffectV111(
    int Amount, int AmountPerAct, int Repetitions,
    PrototypeAscensionDelta[] AscensionDeltas,
    PrototypeAscensionDelta[] RepetitionAscensionDeltas,
    bool IsAttack, bool UseStoredEnemyDamage,
    int ExtraAmountPerPriorMoveUse, string? ExtraAmountFromOwnerPowerId,
    string? OwnerPowerStackConditionId, int? OwnerPowerStacksLessThan,
    int? OwnerPowerStacksAtLeast, bool EarlierNonDamageEffect);

public sealed record PublicEnemyMoveDamageV111(
    string MoveId, PublicEnemyDamageEffectV111[] Effects);

public sealed record PublicEnemyDamagePowerV111(
    string PowerId,
    int EnemyAttackDamageBonusPerStack,
    int EnemyOutgoingDamageNumerator,
    int EnemyOutgoingDamageDenominator,
    int EnemyOutgoingPoweredAttackNumerator,
    int EnemyOutgoingPoweredAttackDenominator,
    int PlayerIncomingAttackDamageNumerator,
    int PlayerIncomingAttackDamageDenominator,
    int PlayerIncomingDamageCap,
    bool LethalAfterUnblockedPoweredAttack,
    int PlayerMaxHpLossOnUnblockedAttackHitPerStack);

public sealed record PublicEnemyDamageRelicV111(
    string RelicId, int WeakAttackDamageReductionPercent);

/// <summary>
/// Public source-defined rules for next-intent inference. The `Ai` graph
/// can be evaluated against observed moves and public status/formation;
/// the catalog intentionally omits all canonical enemy-local counters.
/// </summary>
public sealed record PublicEnemyIntentRuleV111(
    string EnemyId,
    string MovePolicy,
    string[] MoveIds,
    int[] MaxConsecutiveUses,
    int[] OpeningMoveIndices,
    int RandomMovePoolStartIndex,
    int MoveLoopStartIndex,
    PublicEnemyAiDefinitionV111? Ai = null,
    PublicEnemyMoveDamageV111[]? MoveDamage = null);

public sealed record PublicEnemyIntentCatalogV111(
    string Schema, string ProbabilityProvenance,
    long ProbabilityDenominator,
    PublicEnemyIntentRuleV111[] Enemies,
    Dictionary<string, long[]> NativeNextIntWeights,
    PublicEnemyDamagePowerV111[]? DamagePowers = null,
    PublicEnemyDamageRelicV111[]? DamageRelics = null,
    Dictionary<string, string>? EnemyStatusPowerIds = null,
    string? TaintedPowerId = null,
    string? SurroundedPowerId = null)
{
    public const string SchemaId = "sts2-h26-public-intent-rules-v3";
    public const int MaxPublicTicketBound = 256;

    /// <summary>
    /// Static, public move and AI-state rules with exact source Native RNG
    /// masses; read-only, independent of any run or hidden future move.
    /// </summary>
    public static PublicEnemyIntentCatalogV111 Create()
    {
        var rules = PrototypeContent.Enemies.Values
            .Where(enemy => enemy.Moves.Length > 0)
            .OrderBy(enemy => enemy.Id, StringComparer.Ordinal)
            .Select(enemy => new PublicEnemyIntentRuleV111(
                enemy.Id,
                enemy.MovePolicy.ToString(),
                enemy.Moves.Select(move => move.Id).ToArray(),
                enemy.Moves.Select(move => move.MaxConsecutiveUses).ToArray(),
                (int[])(enemy.OpeningMoveIndices
                    ?? [enemy.OpeningMoveIndex]).Clone(),
                enemy.RandomMovePoolStartIndex,
                enemy.MoveLoopStartIndex,
                enemy.Ai is null ? null : new PublicEnemyAiDefinitionV111(
                    enemy.Ai.InitialStateId,
                    enemy.Ai.States.Select(state => new PublicEnemyAiStateV111(
                        state.Id, state.Kind.ToString(),
                        state.MoveIndex, state.NextStateId,
                        (state.Branches ?? [])
                            .Select(branch => new PublicEnemyAiBranchV111(
                                branch.TargetStateId, branch.Weight,
                                branch.RepeatRule.ToString(),
                                branch.MaxTimes, branch.RequiresAvailableSummon))
                            .ToArray(),
                        (state.ConditionalBranches ?? [])
                            .Select(branch => new PublicEnemyAiConditionalBranchV111(
                                branch.TargetStateId,
                                branch.Condition.ToString(), branch.Value))
                            .ToArray()
                    )).ToArray()
                ),
                enemy.Moves.Select(move =>
                {
                    var earlierNonDamage = false;
                    var effects = new List<PublicEnemyDamageEffectV111>();
                    foreach (var effect in move.Effects)
                    {
                        if (effect.Kind != PrototypeEnemyEffectKind.DamagePlayer)
                        {
                            earlierNonDamage = true;
                            continue;
                        }
                        effects.Add(new PublicEnemyDamageEffectV111(
                            effect.Amount, effect.AmountPerAct,
                            effect.Repetitions,
                            effect.AscensionDeltas ?? [],
                            effect.RepetitionAscensionDeltas ?? [],
                            effect.IsAttack, effect.UseStoredEnemyDamage,
                            effect.ExtraAmountPerPriorMoveUse,
                            effect.ExtraAmountFromOwnerPowerId,
                            effect.OwnerPowerStackConditionId,
                            effect.OwnerPowerStacksLessThan,
                            effect.OwnerPowerStacksAtLeast,
                            earlierNonDamage));
                    }
                    return new PublicEnemyMoveDamageV111(
                        move.Id, effects.ToArray());
                }).ToArray()))
            .ToArray();
        var largestMovePool = rules.Max(rule => rule.MoveIds.Length);
        var largestRawTicketPool = rules
            .Where(rule => rule.Ai is not null)
            .SelectMany(rule => rule.Ai!.States)
            .Where(state => state.Kind == "Random")
            .Select(state => state.Branches.Sum(branch =>
                Math.Max(0, branch.Weight)))
            .DefaultIfEmpty(1)
            .Max();
        var maximum = Math.Min(MaxPublicTicketBound,
            Math.Max(largestMovePool, largestRawTicketPool));
        var nativeWeights = Enumerable.Range(1, maximum)
            .ToDictionary(
                count => count.ToString(CultureInfo.InvariantCulture),
                ChanceLawEngineV111.NativeNextIntGridWeights,
                StringComparer.Ordinal);
        return new PublicEnemyIntentCatalogV111(
            SchemaId,
            "source-native-nextint-grid-with-public-conditioning-gate",
            ChanceLawV111.NativeSourceGridSize,
            rules, nativeWeights,
            PrototypeContent.Powers.Values
                .OrderBy(power => power.Id, StringComparer.Ordinal)
                .Select(power => new PublicEnemyDamagePowerV111(
                    power.Id,
                    power.EnemyAttackDamageBonusPerStack,
                    power.EnemyOutgoingDamageNumerator,
                    power.EnemyOutgoingDamageDenominator,
                    power.EnemyOutgoingPoweredAttackNumerator,
                    power.EnemyOutgoingPoweredAttackDenominator,
                    power.PlayerIncomingAttackDamageNumerator,
                    power.PlayerIncomingAttackDamageDenominator,
                    power.PlayerIncomingDamageCap,
                    power.LethalAfterUnblockedPoweredAttack,
                    power.PlayerMaxHpLossOnUnblockedAttackHitPerStack))
                .ToArray(),
            PrototypeContent.Relics.Values
                .Where(relic => relic.WeakAttackDamageReductionPercent != 0)
                .OrderBy(relic => relic.Id, StringComparer.Ordinal)
                .Select(relic => new PublicEnemyDamageRelicV111(
                    relic.Id, relic.WeakAttackDamageReductionPercent))
                .ToArray(),
            new Dictionary<string, string>(
                PrototypeContent.EnemyStatusPowerIds, StringComparer.Ordinal),
            PrototypeNativeHiveElites.TaintedPowerId,
            PrototypeNativeHiveBosses.SurroundedId);
    }
}
