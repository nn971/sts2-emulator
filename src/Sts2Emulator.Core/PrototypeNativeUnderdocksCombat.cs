namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    /// <summary>
    /// Underdocks native-shaped ordinary selection. The first three
    /// combats draw from all four weak encounters without replacement.
    /// Later ordinary fights draw from the complete implemented normal bag,
    /// refilling it after exhaustion. Unsupported region content does not
    /// fall back to Overgrowth or legacy prototype encounters.
    /// </summary>
    private static (
        PrototypeEncounterDefinition Encounter,
        RunWorldState World) PickUnderdocksEncounter(
        RunState state,
        RunWorldState world,
        PrototypeRoomType roomType)
    {
        var pool = world.ActOneEncounterPool
            ?? throw new InvalidOperationException(
                "Underdocks run is missing encounter pool state.");
        if (pool.Region != PrototypeActOneRegion.Underdocks)
        {
            throw new InvalidOperationException(
                "Underdocks region and encounter pool disagree.");
        }

        if (roomType == PrototypeRoomType.Boss)
        {
            if (pool.BossEncounterId is null
                || !PrototypeNativeUnderdocks.SupportedBossEncounterIds.Contains(
                    pool.BossEncounterId, StringComparer.Ordinal))
            {
                throw new NotSupportedException(
                    "The selected Underdocks boss is not implemented. " +
                    "Do not replace it with a different boss.");
            }

            return (PrototypeContent.Encounter(pool.BossEncounterId), world);
        }

        if (roomType == PrototypeRoomType.Elite)
        {
            var elites = pool.RemainingEliteEncounterIds
                ?? throw new InvalidOperationException(
                    "Underdocks elite encounter bag is missing.");
            if (elites.Any(id =>
                    !PrototypeNativeUnderdocks.SupportedEliteEncounterIds
                        .Contains(id, StringComparer.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Underdocks elite bag contains unsupported encounters.");
            }

            var availableElites = elites.Length == 0
                ? (string[])PrototypeNativeUnderdocks
                    .SupportedEliteEncounterIds.Clone()
                : elites;
            var previousElite = world.EncounterIds.Reverse()
                .FirstOrDefault(id => PrototypeNativeUnderdocks
                    .SupportedEliteEncounterIds.Contains(
                        id, StringComparer.Ordinal));
            var eligibleElites = previousElite is null
                ? availableElites
                : availableElites.Where(id => !StringComparer.Ordinal.Equals(
                    id, previousElite)).ToArray();
            if (eligibleElites.Length == 0)
                eligibleElites = availableElites;
            var index = LegacyRandomChoiceV111.ChooseIndexRange(
                state.Rng,
                "combat.encounter.underdocks.elite.index_draw",
                "combat",
                "act:1:elite:pool:" + CanonicalJson.Sha256(eligibleElites),
                eligibleElites.Length).SelectedIndex;
            var selectedElite = eligibleElites[index];
            return (
                PrototypeContent.Encounter(selectedElite),
                world with
                {
                    Stochastic = world.Stochastic with
                    {
                        ActOneEncounterPool = pool with
                        {
                            RemainingEliteEncounterIds = availableElites
                                .Where(id => !StringComparer.Ordinal.Equals(
                                    id, selectedElite))
                                .ToArray()
                        }
                    }
                });
        }

        if (roomType != PrototypeRoomType.Combat)
        {
            throw new NotSupportedException(
                $"Underdocks {roomType} encounters are not implemented yet. " +
                "Do not substitute Overgrowth or legacy prototype enemies.");
        }

        var isWeak = pool.OrdinaryCombatsStarted < 3;
        var remaining = isWeak
            ? pool.RemainingWeakEncounterIds
            : pool.RemainingNormalEncounterIds
                ?? throw new InvalidOperationException(
                    "Underdocks normal encounter bag is missing.");
        if (isWeak && remaining.Length == 0)
        {
            throw new InvalidOperationException(
                "Underdocks weak encounter pool was exhausted too early.");
        }

        var supportedPool = isWeak
            ? PrototypeNativeUnderdocks.SupportedWeakEncounterIds
            : PrototypeNativeUnderdocks.SupportedNormalEncounterIds;
        if (remaining.Any(id =>
                !supportedPool.Contains(id, StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Underdocks {(isWeak ? "weak" : "normal")} encounter " +
                "bag contains an unsupported encounter.");
        }

        var available = !isWeak && remaining.Length == 0
            ? (string[])PrototypeNativeUnderdocks
                .SupportedNormalEncounterIds.Clone()
            : remaining;
        var previous = world.EncounterIds.Reverse().FirstOrDefault(id =>
            PrototypeNativeUnderdocks.SupportedWeakEncounterIds.Contains(
                id, StringComparer.Ordinal)
            || PrototypeNativeUnderdocks.SupportedNormalEncounterIds.Contains(
                id, StringComparer.Ordinal));
        var eligible = previous is null
            ? available
            : available.Where(id => !StringComparer.Ordinal.Equals(
                id, previous)).ToArray();
        if (eligible.Length == 0)
            eligible = available;

        var kind = isWeak ? "weak" : "normal";
        var selected = LegacyRandomChoiceV111.ChooseIndexRange(
            state.Rng,
            "combat.encounter.underdocks." + kind + ".index_draw",
            "combat",
            $"act:1:{kind}:pool:{CanonicalJson.Sha256(eligible)}",
            eligible.Length).SelectedIndex;
        var encounterId = eligible[selected];
        var nextRemaining = available
            .Where(id => !StringComparer.Ordinal.Equals(id, encounterId))
            .ToArray();
        var nextPool = pool with
        {
            OrdinaryCombatsStarted = pool.OrdinaryCombatsStarted + 1,
            RemainingWeakEncounterIds = isWeak
                ? nextRemaining : pool.RemainingWeakEncounterIds,
            RemainingNormalEncounterIds = isWeak
                ? pool.RemainingNormalEncounterIds : nextRemaining
        };
        return (
            PrototypeContent.Encounter(encounterId),
            world with
            {
                Stochastic = world.Stochastic with
                {
                    ActOneEncounterPool = nextPool
                }
            });
    }
}
