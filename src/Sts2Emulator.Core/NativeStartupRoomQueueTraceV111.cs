namespace Sts2Emulator.Core;

/// <summary>
/// Diagnostic-only cursor over the pre-generated UpFront encounter queues.
/// It follows map combat entries but NEVER substitutes its predicted
/// encounter into combat, consumes RNG, or certifies native queue parity.
/// </summary>
public static class NativeStartupRoomQueueTraceV111
{
    public sealed record Comparison(
        int Act, PrototypeRoomType RoomType, string MapNodeId,
        int QueueIndex, string SourceEncounterId,
        string? ExpectedEmulatorEncounterId, string ActualEmulatorEncounterId,
        string Status);

    public sealed record Snapshot(
        int[] NormalVisits, int[] EliteVisits, int[] BossVisits,
        Comparison[] Comparisons)
    {
        public Snapshot Fork() => this with
        {
            NormalVisits = (int[])NormalVisits.Clone(),
            EliteVisits = (int[])EliteVisits.Clone(),
            BossVisits = (int[])BossVisits.Clone(),
            Comparisons = (Comparison[])Comparisons.Clone()
        };
    }

    public static Snapshot Empty() => new(new int[3], new int[3],
        new int[3], []);

    public static Snapshot Observe(
        Snapshot prior, NativeUpFrontRoomsV111.Result generated,
        int act, PrototypeRoomType room, string nodeId, string actualEncounterId)
    {
        ArgumentNullException.ThrowIfNull(prior);
        ArgumentNullException.ThrowIfNull(generated);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualEncounterId);
        if (act is < 1 or > 3 || generated.Acts.Length != 3
            || prior.NormalVisits.Length != 3
            || prior.EliteVisits.Length != 3 || prior.BossVisits.Length != 3)
            throw new InvalidDataException("Invalid native startup queue cursor.");
        if (prior.Comparisons.Any(x => x.Act == act
            && StringComparer.Ordinal.Equals(x.MapNodeId, nodeId)))
            return prior;

        var updated = prior.Fork();
        var actRooms = generated.Acts[act - 1];
        if (actRooms.Act != act)
            throw new InvalidDataException("Source queue act identity differs.");
        int queueIndex;
        string sourceId;
        switch (room)
        {
            case PrototypeRoomType.Combat:
                queueIndex = updated.NormalVisits[act - 1]++;
                if (actRooms.NormalEncounters.Length == 0)
                    throw new InvalidDataException("Empty source normal queue.");
                sourceId = actRooms.NormalEncounters[
                    queueIndex % actRooms.NormalEncounters.Length];
                break;
            case PrototypeRoomType.Elite:
                queueIndex = updated.EliteVisits[act - 1]++;
                if (actRooms.EliteEncounters.Length == 0)
                    throw new InvalidDataException("Empty source elite queue.");
                sourceId = actRooms.EliteEncounters[
                    queueIndex % actRooms.EliteEncounters.Length];
                break;
            case PrototypeRoomType.Boss:
                queueIndex = updated.BossVisits[act - 1]++;
                sourceId = queueIndex == 0 || actRooms.SecondBossEncounter is null
                    ? actRooms.BossEncounter : actRooms.SecondBossEncounter;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(room));
        }

        var mapped = NativeUpFrontEncounterCrosswalkV111.Resolve(
            act, room, sourceId);
        var status = mapped.Status == "RESOLVED"
            ? StringComparer.Ordinal.Equals(mapped.EmulatorId, actualEncounterId)
                ? "MATCHED_SOURCE_PREDICTION"
                : "DIFFERS_FROM_SOURCE_PREDICTION"
            : mapped.Status;
        var comparison = new Comparison(
            act, room, nodeId, queueIndex, sourceId, mapped.EmulatorId,
            actualEncounterId, status);
        return updated with
        {
            Comparisons = updated.Comparisons.Append(comparison).ToArray()
        };
    }
}
