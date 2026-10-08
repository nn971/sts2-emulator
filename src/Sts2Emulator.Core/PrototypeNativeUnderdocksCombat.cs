namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    /// <summary>
    /// Underdocks native-shaped weak selection. Only the explicitly
    /// implemented weak encounter subset is playable. The first three
    /// ordinary combats draw without replacement; unsupported room types
    /// throw instead of using Overgrowth or unrelated prototype encounters.
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

        if (pool.OrdinaryCombatsStarted >= 3)
        {
            throw new NotSupportedException(
                "Underdocks normal encounters are not implemented yet. " +
                "The first three weak fights are the current supported scope.");
        }

        var remaining = pool.RemainingWeakEncounterIds;
        if (remaining.Length == 0)
        {
            throw new InvalidOperationException(
                "Underdocks weak encounter pool was exhausted too early.");
        }

        var selected = PrototypeRng.NextInt(
            state.Rng, "combat", remaining.Length);
        var encounterId = remaining[selected];
        if (!PrototypeNativeUnderdocks.SupportedWeakEncounterIds.Contains(
                encounterId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported Underdocks weak encounter '{encounterId}'.");
        }

        return (
            PrototypeContent.Encounter(encounterId),
            world with
            {
                ActOneEncounterPool = pool with
                {
                    OrdinaryCombatsStarted =
                        pool.OrdinaryCombatsStarted + 1,
                    RemainingWeakEncounterIds = remaining
                        .Where((_, index) => index != selected)
                        .ToArray()
                }
            });
    }
}
