namespace Sts2Emulator.Core;

internal sealed record ActOneEncounterSelectionSupportV111(
    string SiteId,
    string OwnerId,
    string[] EncounterIds)
{
    internal bool MatchesLabels(string[] labels) =>
        labels.SequenceEqual(EncounterIds, StringComparer.Ordinal) ||
        labels.SequenceEqual(
            Enumerable.Range(0, EncounterIds.Length).Select(index =>
                index.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
            StringComparer.Ordinal);

    internal static bool MatchesTraceIdentityLabels(
        string siteId, string ownerId, string[] labels)
    {
        if (labels.Length == 0 ||
            labels.Distinct(StringComparer.Ordinal).Count() != labels.Length)
            return false;

        string[] sourcePool;
        string expectedOwnerPrefix;
        switch (siteId)
        {
            case "combat.encounter.overgrowth_elite.index_draw":
                sourcePool = PrototypeContent.OvergrowthEliteEncounterPool;
                expectedOwnerPrefix = "act:1:floor:";
                break;
            case "combat.encounter.overgrowth_weak.index_draw":
                sourcePool = PrototypeContent.OvergrowthWeakEncounterPool;
                expectedOwnerPrefix = "act:1:floor:";
                break;
            case "combat.encounter.overgrowth_normal.index_draw":
                sourcePool = PrototypeContent.OvergrowthNormalEncounterPool;
                expectedOwnerPrefix = "act:1:floor:";
                break;
            case "combat.encounter.underdocks.elite.index_draw":
                sourcePool = PrototypeNativeUnderdocks
                    .SupportedEliteEncounterIds;
                expectedOwnerPrefix = "act:1:elite:pool:";
                break;
            case "combat.encounter.underdocks.weak.index_draw":
                sourcePool = PrototypeNativeUnderdocks
                    .SupportedWeakEncounterIds;
                expectedOwnerPrefix = "act:1:weak:pool:";
                break;
            case "combat.encounter.underdocks.normal.index_draw":
                sourcePool = PrototypeNativeUnderdocks
                    .SupportedNormalEncounterIds;
                expectedOwnerPrefix = "act:1:normal:pool:";
                break;
            default:
                return false;
        }

        if (labels.Any(label => !sourcePool.Contains(
                label, StringComparer.Ordinal)))
            return false;

        if (expectedOwnerPrefix == "act:1:floor:")
        {
            const string poolMarker = ":pool:";
            var markerIndex = ownerId.IndexOf(
                poolMarker, StringComparison.Ordinal);
            if (!ownerId.StartsWith(expectedOwnerPrefix,
                    StringComparison.Ordinal) ||
                markerIndex <= expectedOwnerPrefix.Length ||
                !int.TryParse(ownerId.AsSpan(expectedOwnerPrefix.Length,
                        markerIndex - expectedOwnerPrefix.Length),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var floor) || floor < 0 ||
                ownerId[(markerIndex + poolMarker.Length)..] !=
                    CanonicalJson.Sha256(labels))
                return false;
        }
        else if (ownerId != expectedOwnerPrefix +
            CanonicalJson.Sha256(labels))
        {
            return false;
        }

        return true;
    }
}

public sealed partial class PrototypeGameEngine
{
    internal static ActOneEncounterSelectionSupportV111?
        DescribeActOneEncounterSelection(
            RunWorldState world,
            int floor,
            PrototypeRoomType roomType)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Act != 1 || world.Stochastic.ActOneEncounterPool is not
            { } pool || pool.Region != world.ActOneRegion)
            return null;

        return world.ActOneRegion switch
        {
            PrototypeActOneRegion.Overgrowth =>
                DescribeOvergrowthEncounterSelection(
                    world, floor, roomType, pool),
            PrototypeActOneRegion.Underdocks =>
                DescribeUnderdocksEncounterSelection(
                    world, roomType, pool),
            _ => null
        };
    }

    internal static ActOneEncounterSelectionSupportV111?
        DescribeActOneEncounterSelection(
            RunState source, GameAction action, string siteId)
    {
        if (action.Kind != "choose_map_node" ||
            source.Phase != RunPhase.MapChoice ||
            source.World is not { } world ||
            !(ControlledRunActionEngineV111
                    .IsLegacyActOneCombatEntryAction(source, action) ||
              ControlledRunActionEngineV111
                    .IsNativeActOneCombatEntryAction(source, action)))
            return null;

        var nodeId = action.ReadPayload<ChooseMapNodePayload>().NodeId;
        var node = PrototypeNativeWingedBoots.AvailableNodes(
                world, source.Player)
            .SingleOrDefault(candidate => candidate.NodeId == nodeId);
        if (node is null)
            return null;

        var support = DescribeActOneEncounterSelection(
            world, node.Floor, node.RoomType);
        return support?.SiteId == siteId ? support : null;
    }

    private static ActOneEncounterSelectionSupportV111?
        DescribeOvergrowthEncounterSelection(
            RunWorldState world,
            int floor,
            PrototypeRoomType roomType,
            PrototypeActOneEncounterPoolState pool)
    {
        var kind = roomType switch
        {
            PrototypeRoomType.Elite => "elite",
            PrototypeRoomType.Combat =>
                pool.OrdinaryCombatsStarted < 3 ? "weak" : "normal",
            _ => null
        };
        if (kind is null)
            return null;

        string[] available;
        string[] eligible;
        switch (kind)
        {
            case "elite":
                available = pool.RemainingEliteEncounterIds ??
                    (string[])PrototypeContent
                        .OvergrowthEliteEncounterPool.Clone();
                if (available.Length == 0)
                    available = (string[])PrototypeContent
                        .OvergrowthEliteEncounterPool.Clone();
                var previousElite = world.EncounterIds.Reverse()
                    .FirstOrDefault(id => PrototypeContent
                        .OvergrowthEliteEncounterPool.Contains(
                            id, StringComparer.Ordinal));
                eligible = previousElite is null
                    ? available
                    : available.Where(id => !StringComparer.Ordinal.Equals(
                        id, previousElite)).ToArray();
                if (eligible.Length == 0)
                    eligible = available;
                break;
            case "weak":
                available = pool.RemainingWeakEncounterIds;
                if (available.Length == 0)
                    throw new InvalidOperationException(
                        "Overgrowth weak encounter pool exhausted before three ordinary combats.");
                eligible = available;
                break;
            default:
                available = pool.RemainingNormalEncounterIds ??
                    (string[])PrototypeContent
                        .OvergrowthNormalEncounterPool.Clone();
                if (available.Length == 0)
                    throw new InvalidOperationException(
                        "Overgrowth normal encounter bag is exhausted.");
                var previous = world.EncounterIds.LastOrDefault();
                eligible = previous is null
                    ? available
                    : available.Where(candidate => CanFollowOvergrowthEncounter(
                        previous, candidate)).ToArray();
                if (eligible.Length == 0)
                    eligible = available;
                break;
        }

        var siteId = kind switch
        {
            "elite" => "combat.encounter.overgrowth_elite.index_draw",
            "weak" => "combat.encounter.overgrowth_weak.index_draw",
            _ => "combat.encounter.overgrowth_normal.index_draw"
        };
        return new ActOneEncounterSelectionSupportV111(
            siteId,
            $"act:1:floor:{floor}:pool:{CanonicalJson.Sha256(eligible)}",
            (string[])eligible.Clone());
    }

    private static ActOneEncounterSelectionSupportV111?
        DescribeUnderdocksEncounterSelection(
            RunWorldState world,
            PrototypeRoomType roomType,
            PrototypeActOneEncounterPoolState pool)
    {
        var kind = roomType switch
        {
            PrototypeRoomType.Elite => "elite",
            PrototypeRoomType.Combat =>
                pool.OrdinaryCombatsStarted < 3 ? "weak" : "normal",
            _ => null
        };
        if (kind is null)
            return null;

        string[] available;
        string[] eligible;
        string[] supportedPool;
        switch (kind)
        {
            case "elite":
                available = pool.RemainingEliteEncounterIds ??
                    throw new InvalidOperationException(
                        "Underdocks elite encounter bag is missing.");
                supportedPool = PrototypeNativeUnderdocks
                    .SupportedEliteEncounterIds;
                if (available.Any(id => !supportedPool.Contains(
                        id, StringComparer.Ordinal)))
                    throw new InvalidOperationException(
                        "Underdocks elite bag contains unsupported encounters.");
                if (available.Length == 0)
                    available = (string[])supportedPool.Clone();
                var previousElite = world.EncounterIds.Reverse()
                    .FirstOrDefault(id => supportedPool.Contains(
                        id, StringComparer.Ordinal));
                eligible = previousElite is null
                    ? available
                    : available.Where(id => !StringComparer.Ordinal.Equals(
                        id, previousElite)).ToArray();
                if (eligible.Length == 0)
                    eligible = available;
                break;
            default:
                var isWeak = kind == "weak";
                var remaining = isWeak
                    ? pool.RemainingWeakEncounterIds
                    : pool.RemainingNormalEncounterIds ??
                        throw new InvalidOperationException(
                            "Underdocks normal encounter bag is missing.");
                if (isWeak && remaining.Length == 0)
                    throw new InvalidOperationException(
                        "Underdocks weak encounter pool was exhausted too early.");
                supportedPool = isWeak
                    ? PrototypeNativeUnderdocks.SupportedWeakEncounterIds
                    : PrototypeNativeUnderdocks.SupportedNormalEncounterIds;
                if (remaining.Any(id => !supportedPool.Contains(
                        id, StringComparer.Ordinal)))
                    throw new InvalidOperationException(
                        $"Underdocks {kind} encounter bag contains an unsupported encounter.");
                available = !isWeak && remaining.Length == 0
                    ? (string[])supportedPool.Clone()
                    : remaining;
                var previous = world.EncounterIds.Reverse()
                    .FirstOrDefault(id =>
                        PrototypeNativeUnderdocks.SupportedWeakEncounterIds
                            .Contains(id, StringComparer.Ordinal) ||
                        PrototypeNativeUnderdocks.SupportedNormalEncounterIds
                            .Contains(id, StringComparer.Ordinal));
                eligible = previous is null
                    ? available
                    : available.Where(id => !StringComparer.Ordinal.Equals(
                        id, previous)).ToArray();
                if (eligible.Length == 0)
                    eligible = available;
                break;
        }

        var siteId = kind switch
        {
            "elite" => "combat.encounter.underdocks.elite.index_draw",
            "weak" => "combat.encounter.underdocks.weak.index_draw",
            _ => "combat.encounter.underdocks.normal.index_draw"
        };
        return new ActOneEncounterSelectionSupportV111(
            siteId,
            $"act:1:{kind}:pool:{CanonicalJson.Sha256(eligible)}",
            (string[])eligible.Clone());
    }
}
