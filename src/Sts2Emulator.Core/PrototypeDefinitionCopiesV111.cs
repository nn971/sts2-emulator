using System.Collections;

namespace Sts2Emulator.Core;

/// <summary>
/// Deep copies for source catalog definitions that cross public array
/// projections. Definition records contain mutable arrays even though their
/// scalar values are immutable.
/// </summary>
internal static class PrototypeDefinitionCopiesV111
{
    internal static PrototypeCardDefinition CloneCardDefinition(
        PrototypeCardDefinition card) => card with
    {
        Effects = (PrototypeCombatEffectSpec[])card.Effects.Clone(),
        Tags = card.Tags is null ? null : (string[])card.Tags.Clone()
    };

    internal static PrototypePotionDefinition ClonePotionDefinition(
        PrototypePotionDefinition potion) => potion with
    {
        Effects = (PrototypeCombatEffectSpec[])potion.Effects.Clone(),
        RunEffects = CloneArray(potion.RunEffects)
    };

    internal static PrototypeRelicDefinition CloneRelicDefinition(
        PrototypeRelicDefinition relic) => relic with
    {
        Triggers = relic.Triggers?.Select(trigger => trigger with
        {
            Effects = (PrototypeCombatEffectSpec[])trigger.Effects.Clone()
        }).ToArray(),
        RunTriggers = relic.RunTriggers?.Select(trigger => trigger with
        {
            Effects = (PrototypeRunEffectSpec[])trigger.Effects.Clone()
        }).ToArray()
    };

    internal static PrototypeEventDefinition CloneEventDefinition(
        PrototypeEventDefinition definition) => definition with
    {
        Choices = definition.Choices.Select(choice => choice with
        {
            Effects = (PrototypeRunEffectSpec[])choice.Effects.Clone(),
            DeckChoice = choice.DeckChoice is null
                ? null
                : choice.DeckChoice with { },
            RouteCondition = choice.RouteCondition is null
                ? null
                : choice.RouteCondition with { }
        }).ToArray()
    };

    internal static PrototypePowerDefinition ClonePowerDefinition(
        PrototypePowerDefinition power) => power with
    {
        Triggers = power.Triggers.Select(trigger => trigger with
        {
            Effects = (PrototypeCombatEffectSpec[])trigger.Effects.Clone()
        }).ToArray(),
        RemoveOwnerPowersOnWake = power.RemoveOwnerPowersOnWake is null
            ? null
            : (string[])power.RemoveOwnerPowersOnWake.Clone(),
        RemoveOwnerPowersOnPowerExpiry =
            power.RemoveOwnerPowersOnPowerExpiry is null
                ? null
                : (string[])power.RemoveOwnerPowersOnPowerExpiry.Clone()
    };

    internal static PrototypeEnemyDefinition CloneEnemyDefinition(
        PrototypeEnemyDefinition enemy) => enemy with
    {
        Moves = enemy.Moves.Select(move => move with
        {
            Effects = move.Effects
                .Select(CloneEnemyEffect)
                .ToArray()
        }).ToArray(),
        StartingPowers = enemy.StartingPowers?.Select(power => power with
        {
            AscensionDeltas = CloneArray(power.AscensionDeltas)
        }).ToArray(),
        HpAscensionDeltas = CloneArray(enemy.HpAscensionDeltas),
        OpeningMoveIndices = CloneArray(enemy.OpeningMoveIndices),
        MinHpAscensionDeltas = CloneArray(enemy.MinHpAscensionDeltas),
        Ai = enemy.Ai is null ? null : CloneEnemyAi(enemy.Ai),
        DeathSummons = enemy.DeathSummons is null
            ? null
            : (PrototypeEnemyDeathSummonSpec[])enemy.DeathSummons.Clone()
    };

    internal static PrototypeEncounterDefinition CloneEncounterDefinition(
        PrototypeEncounterDefinition encounter) => encounter with
    {
        EnemyIds = (string[])encounter.EnemyIds.Clone(),
        Formation = encounter.Formation?.Select(spec => spec with { })
            .ToArray(),
        EnemyPool = CloneArray(encounter.EnemyPool),
        SelectionGroups = encounter.SelectionGroups?.Select(CloneSelectionGroup)
            .ToArray(),
        FormationVariants = encounter.FormationVariants?
            .Select(CloneFormationVariant).ToArray(),
        CyclicOpeningAiStateIds = CloneArray(
            encounter.CyclicOpeningAiStateIds),
        FixedTrailingOpeningAiStateIds = CloneArray(
            encounter.FixedTrailingOpeningAiStateIds)
    };

    private static PrototypeEnemyEffectSpec CloneEnemyEffect(
        PrototypeEnemyEffectSpec effect) => effect with
    {
        AscensionDeltas = CloneArray(effect.AscensionDeltas),
        RepetitionAscensionDeltas =
            CloneArray(effect.RepetitionAscensionDeltas),
        SummonSlotNames = CloneArray(effect.SummonSlotNames),
        SummonEnemyPool = CloneArray(effect.SummonEnemyPool),
        SummonSlotFormationPositions =
            CloneArray(effect.SummonSlotFormationPositions)
    };

    private static PrototypeEnemyAiDefinition CloneEnemyAi(
        PrototypeEnemyAiDefinition ai) => ai with
    {
        States = ai.States.Select(state => state with
        {
            Branches = state.Branches is null
                ? null
                : (PrototypeEnemyAiBranch[])state.Branches.Clone(),
            ConditionalBranches = state.ConditionalBranches is null
                ? null
                : (PrototypeEnemyAiConditionalBranch[])
                    state.ConditionalBranches.Clone()
        }).ToArray()
    };

    private static PrototypeEncounterSelectionGroup CloneSelectionGroup(
        PrototypeEncounterSelectionGroup group) => group with
    {
        EnemyPool = (string[])group.EnemyPool.Clone(),
        FormationPositions = (int[])group.FormationPositions.Clone(),
        SlotNames = CloneArray(group.SlotNames)
    };

    private static PrototypeEncounterFormationVariant CloneFormationVariant(
        PrototypeEncounterFormationVariant variant) => variant with
    {
        Formation = variant.Formation?.Select(spec => spec with { }).ToArray(),
        SelectionGroups = variant.SelectionGroups?
            .Select(CloneSelectionGroup).ToArray()
    };

    private static T[]? CloneArray<T>(T[]? values) => values is null
        ? null
        : (T[])values.Clone();
}

/// <summary>
/// Read-only catalog view that clones each value as it crosses the public
/// lookup boundary. This keeps mutable definition arrays private without
/// materializing a full catalog copy for every lookup.
/// </summary>
internal sealed class DefensiveReadOnlyDictionary<TKey, TValue>(
    IReadOnlyDictionary<TKey, TValue> source,
    Func<TValue, TValue> clone) : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    public int Count => source.Count;
    public IEnumerable<TKey> Keys => source.Keys;
    public IEnumerable<TValue> Values => source.Values.Select(clone);
    public TValue this[TKey key] => clone(source[key]);

    public bool ContainsKey(TKey key) => source.ContainsKey(key);

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (source.TryGetValue(key, out var saved))
        {
            value = clone(saved);
            return true;
        }

        value = default!;
        return false;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
        source.Select(entry => new KeyValuePair<TKey, TValue>(
            entry.Key, clone(entry.Value))).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
