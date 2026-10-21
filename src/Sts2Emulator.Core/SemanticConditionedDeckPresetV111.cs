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

    public static CardInstance[] Apply(
        IReadOnlyList<CardInstance> starter, string? presetId)
    {
        ArgumentNullException.ThrowIfNull(starter);
        var result = starter.ToArray();
        var preset = presetId ?? Standard;
        if (preset == Standard)
            return result;
        if (preset is not (StrikeHeavy or DefendHeavy or PoisonKit))
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
