using System.Security.Cryptography;
using System.Text;

namespace Sts2Emulator.Core;

/// <summary>
/// Explicit, test-conditioned variants of the legitimate Silent starter deck.
/// Reassign only existing persistent physical card IDs, before the controlled
/// combat card registry, opening shuffle, and draw hooks are constructed.
/// This is a research context, NOT an assertion of native card-reward odds.
/// </summary>
public static class SemanticConditionedDeckPresetV111
{
    public const string Standard = "silent-starter-v1";
    public const string StrikeHeavy = "silent-strike-heavy-v1";
    public const string DefendHeavy = "silent-defend-heavy-v1";
    public const string PoisonKit = "silent-poison-kit-v1";

    // A10 Act 1 boss research decks: 12 starter cards plus eight genuine
    // Silent cards. They are conditioned fixtures, not a native reward model.
    public const string BossCycle20 = "silent-boss-cycle-20-v1";
    public const string BossPoison20 = "silent-boss-poison-20-v1";

    private static readonly string[] BossRelicPool =
    [
        "proto.relic.lantern", "proto.relic.vajra",
        "proto.relic.ornamental_fan", "proto.relic.orichalcum"
    ];

    public static bool IsBossDeck(string? presetId) =>
        presetId is BossCycle20 or BossPoison20;

    /// <summary>
    /// Research-only source-conditioned loadout: add two distinct existing
    /// relics before the combat opening. The seed chooses the pair and cannot
    /// leak as hidden state to the player. This is not a model of native
    /// relic rewards/acquisition probabilities.
    /// </summary>
    public static RelicInstance[] ApplyBossRelics(
        IReadOnlyList<RelicInstance> starter, string seed, string? presetId)
    {
        ArgumentNullException.ThrowIfNull(starter);
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        if (!IsBossDeck(presetId))
            return starter.ToArray();
        var relics = starter.ToList();
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(
            "sts2-h3-boss-relic-pair-v1:" + presetId + ":" + seed));
        var first = digest[0] % BossRelicPool.Length;
        var second = (first + 1 + digest[1] % (BossRelicPool.Length - 1))
            % BossRelicPool.Length;
        foreach (var index in new[] { first, second })
        {
            var relicId = BossRelicPool[index];
            _ = PrototypeContent.Relic(relicId);
            if (relics.Any(r => r.RelicId == relicId))
                throw new InvalidDataException("Boss relic pool overlaps starter relic.");
            relics.Add(new RelicInstance(relicId, PrototypeJson.EmptyObject()));
        }
        return relics.ToArray();
    }

    public static CardInstance[] Apply(
        IReadOnlyList<CardInstance> starter, string? presetId)
    {
        ArgumentNullException.ThrowIfNull(starter);
        var result = starter.ToArray();
        var preset = presetId ?? Standard;
        if (preset == Standard)
            return result;
        if (preset is not (StrikeHeavy or DefendHeavy or PoisonKit
                or BossCycle20 or BossPoison20))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.conditioned_deck " +
                "operation=unknown_preset:" + preset);
        if (result.Length == 0 ||
            result.Select(card => card.InstanceId).Distinct().Count() !=
            result.Length)
            throw new InvalidDataException(
                "Conditioned Silent deck has missing/duplicate source card IDs.");

        const string strike = "proto.silent.strike";
        const string defend = "proto.silent.defend";
        // Preserve deck size, instance identities, starter relic and
        // next-instance accounting. A custom deck is formed before the engine
        // owns the combat zones and original opening shuffle.
        void ReplaceFirst(string oldModel, string newModel, int count)
        {
            _ = PrototypeContent.Card(newModel);
            var positions = Enumerable.Range(0, result.Length)
                .Where(index => result[index].CardId == oldModel)
                .Take(count).ToArray();
            if (positions.Length != count)
                throw new InvalidDataException(
                    "Silent starter deck has changed; cannot apply preset.");
            foreach (var index in positions)
            {
                var old = result[index];
                result[index] = new CardInstance(
                    old.InstanceId, newModel, 0, PrototypeJson.EmptyObject());
            }
        }

        if (IsBossDeck(preset))
        {
            if (result.Length != 12)
                throw new InvalidDataException(
                    "20-card boss preset requires exactly 12 starter cards.");
            var extras = preset == BossCycle20
                ? new[]
                {
                    "proto.silent.acrobatics", "proto.silent.acrobatics",
                    "proto.silent.backflip", "proto.silent.backflip",
                    "proto.silent.slice", "proto.silent.slice",
                    "proto.silent.poisoned_stab", "proto.silent.deadly_poison"
                }
                : new[]
                {
                    "proto.silent.deadly_poison", "proto.silent.deadly_poison",
                    "proto.silent.poisoned_stab", "proto.silent.poisoned_stab",
                    "proto.silent.backflip", "proto.silent.backflip",
                    "proto.silent.catalyst", "proto.silent.acrobatics"
                };
            long next = result.Max(x => x.InstanceId) + 1;
            return result.Concat(extras.Select(id =>
            {
                _ = PrototypeContent.Card(id);
                return new CardInstance(
                    next++, id, 0, PrototypeJson.EmptyObject());
            })).ToArray();
        }

        switch (preset)
        {
            case StrikeHeavy:
                ReplaceFirst(defend, strike, 2);
                break;
            case DefendHeavy:
                ReplaceFirst(strike, defend, 2);
                break;
            case PoisonKit:
                ReplaceFirst(strike, "proto.silent.poisoned_stab", 1);
                ReplaceFirst(defend, "proto.silent.deadly_poison", 1);
                break;
        }
        return result;
    }
}
