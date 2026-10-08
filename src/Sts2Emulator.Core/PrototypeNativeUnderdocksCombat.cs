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
