namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    /// <summary>
    /// Underdocks native-shaped ordinary selection. The first three
    /// combats draw from all four weak encounters without replacement.
    /// Later ordinary fights draw from a restricted implemented normal bag
    /// without replacement. Unsupported region content does not fall back
    /// to Overgrowth or legacy prototype encounters.
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
            if (elites.Length == 0)
            {
                throw new NotSupportedException(
                    "The implemented Underdocks elite bag is exhausted; " +
                    "remaining elite encounters are not implemented.");
            }

            if (elites.Any(id =>
                    !PrototypeNativeUnderdocks.SupportedEliteEncounterIds
                        .Contains(id, StringComparer.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Underdocks elite bag contains unsupported encounters.");
            }

            var index = PrototypeRng.NextInt(
                state.Rng, "combat", elites.Length);
            return (
                PrototypeContent.Encounter(elites[index]),
                world with
                {
                    ActOneEncounterPool = pool with
                    {
                        RemainingEliteEncounterIds = elites
                            .Where((_, i) => i != index)
                            .ToArray()
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
        if (remaining.Length == 0)
        {
            if (!isWeak)
            {
                throw new NotSupportedException(
                    "The implemented Underdocks normal encounter bag is " +
                    "exhausted. Remaining native normal encounters are " +
                    "not implemented; refusing foreign encounter fallback.");
            }

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

        var selected = PrototypeRng.NextInt(
            state.Rng, "combat", remaining.Length);
        var encounterId = remaining[selected];
        var nextRemaining = remaining
            .Where((_, index) => index != selected)
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
            world with { ActOneEncounterPool = nextPool });
    }
}
