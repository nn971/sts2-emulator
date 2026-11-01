namespace Sts2Emulator.Core;

/// <summary>
/// Fail-closed identity mapping for source-generated UpFront encounters.
/// A source class name is accepted only when exactly one implemented
/// encounter has the same normalized ID, act and room type. This mapping
/// never chooses or generates an encounter and consumes no RNG.
/// Source-generated queues remain observational until native parity passes.
/// </summary>
public static class NativeUpFrontEncounterCrosswalkV111
{
    public sealed record Mapping(
        int Act, PrototypeRoomType Room, string SourceId,
        string? EmulatorId, string Status);

    private static string Token(string value) =>
        new(value.Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant).ToArray());

    public static Mapping Resolve(
        int act, PrototypeRoomType room, string sourceId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(act, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        if (room is not (PrototypeRoomType.Combat
            or PrototypeRoomType.Elite or PrototypeRoomType.Boss))
            throw new ArgumentOutOfRangeException(nameof(room));

        var token = Token(sourceId);
        var matches = PrototypeContent.Encounters.Where(e =>
            e.RoomType == room
            && e.MinAct <= act && act <= e.MaxAct
            && e.Id.StartsWith("proto.encounter.", StringComparison.Ordinal)
            && Token(e.Id["proto.encounter.".Length..]) == token)
            .Select(e => e.Id).Distinct(StringComparer.Ordinal).ToArray();
        return matches.Length switch
        {
            1 => new Mapping(act, room, sourceId, matches[0], "RESOLVED"),
            0 => new Mapping(act, room, sourceId, null,
                "UNSUPPORTED_SOURCE_ENCOUNTER"),
            _ => new Mapping(act, room, sourceId, null,
                "AMBIGUOUS_SOURCE_ENCOUNTER")
        };
    }

    public static Mapping[] Audit(NativeUpFrontRoomsV111.Result rooms)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        var mappings = new List<Mapping>();
        foreach (var act in rooms.Acts)
        {
            mappings.AddRange(act.NormalEncounters.Select(id =>
                Resolve(act.Act, PrototypeRoomType.Combat, id)));
            mappings.AddRange(act.EliteEncounters.Select(id =>
                Resolve(act.Act, PrototypeRoomType.Elite, id)));
            mappings.Add(Resolve(act.Act, PrototypeRoomType.Boss,
                act.BossEncounter));
            if (act.SecondBossEncounter is { } second)
                mappings.Add(Resolve(act.Act, PrototypeRoomType.Boss,
                    second));
        }
        return mappings.ToArray();
    }
}
