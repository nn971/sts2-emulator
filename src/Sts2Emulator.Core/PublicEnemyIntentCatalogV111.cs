using System.Globalization;

namespace Sts2Emulator.Core;

/// <summary>
/// Source-defined rules for next-intent inference from PUBLIC combat history.
/// The catalog exports game rules only: no RunState, selected RNG outcome,
/// planned-next state, hidden owner counters, or private RNG cursor.
/// Consumers must treat unobservable conditioning as UNAVAILABLE.
/// </summary>
public sealed record PublicEnemyIntentRuleV111(
    string EnemyId,
    string MovePolicy,
    string[] MoveIds,
    int[] MaxConsecutiveUses,
    int[] OpeningMoveIndices,
    int RandomMovePoolStartIndex,
    int MoveLoopStartIndex);

public sealed record PublicEnemyIntentCatalogV111(
    string Schema, string ProbabilityProvenance,
    long ProbabilityDenominator,
    PublicEnemyIntentRuleV111[] Enemies,
    Dictionary<string, long[]> NativeNextIntWeights)
{
    public const string SchemaId = "sts2-h26-public-intent-rules-v1";

    /// <summary>
    /// A catalog of source-defined enemy move and RNG rules. Rule metadata is
    /// public game information; applying it to the player's displayed intent
    /// never inspects the canonical EnemyCombatState.
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
                enemy.MoveLoopStartIndex))
            .ToArray();
        var largestPool = rules.Max(rule => rule.MoveIds.Length);
        var nativeWeights = Enumerable.Range(1, largestPool)
            .ToDictionary(
                count => count.ToString(CultureInfo.InvariantCulture),
                ChanceLawEngineV111.NativeNextIntGridWeights,
                StringComparer.Ordinal);
        return new PublicEnemyIntentCatalogV111(
            SchemaId,
            "source-native-nextint-grid-with-public-conditioning-gate",
            ChanceLawV111.NativeSourceGridSize,
            rules, nativeWeights);
    }
}
