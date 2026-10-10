namespace Sts2Emulator.Core;

/// <summary>
/// Pure predicates over completed rooms. In-progress rooms never count:
/// they enter the history only after their rewards/continuations are resolved.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private static bool MatchesRouteCondition(
        RunWorldState world,
        PrototypeRouteConditionSpec? condition)
    {
        if (condition is null)
        {
            return true;
        }

        if (world.Act < condition.MinAct
            || world.Act > condition.MaxAct
            || world.Floor < condition.MinFloor
            || world.Floor > condition.MaxFloor)
        {
            return false;
        }

        var history = condition.CurrentActOnly
            ? world.CompletedRooms
                .Where(room => room.Act == world.Act)
                .ToArray()
            : world.CompletedRooms;

        var matches = history.Count(room =>
            condition.RoomType is null
            || room.RoomType == condition.RoomType.Value);
        if (matches < condition.MinimumCompletedVisits)
        {
            return false;
        }

        if (condition.EveryNthMatchingVisit > 0
            && (matches == 0
                || matches % condition.EveryNthMatchingVisit != 0))
        {
            return false;
        }

        if (condition.MinimumConsecutiveCompleted > 0)
        {
            if (condition.RoomType is null)
            {
                return history.Length >=
                    condition.MinimumConsecutiveCompleted;
            }

            var streak = 0;
            for (var index = history.Length - 1;
                 index >= 0;
                 index--)
            {
                if (history[index].RoomType !=
                    condition.RoomType.Value)
                {
                    break;
                }

                streak++;
            }

            if (streak < condition.MinimumConsecutiveCompleted)
            {
                return false;
            }
        }

        return true;
    }

    private static RunState RecordCompletedRoom(
        RunState state)
    {
        var world = RequireWorld(state);
        if (world.ActiveRoom is not { } roomType
            || world.Map.CurrentNodeId is not { } nodeId)
        {
            // Hand-authored test worlds may omit a map or active room.
            // Generated worlds always carry both; validate those strictly.
            if (world.Map.GenerationProfileId
                is PrototypeContent.MapGenerationProfileId
                or PrototypeNativeOvergrowthMap.GenerationProfileId
                or PrototypeNativeUnderdocks.GenerationProfileId)
            {
                throw new InvalidOperationException(
                    "Generated room completion has no active map node.");
            }

            return state;
        }

        var node = world.Map.Nodes.FirstOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.NodeId, nodeId));
        if (node is null
            || node.Act != world.Act
            || node.Floor != world.Floor
            || (node.RoomType != roomType
                && !(node.RoomType == PrototypeRoomType.Unknown
                    && roomType is PrototypeRoomType.Combat
                        or PrototypeRoomType.Event
                        or PrototypeRoomType.Shop
                        or PrototypeRoomType.Treasure)))
        {
            throw new InvalidOperationException(
                "Completed room disagrees with its active map node.");
        }

        if (world.CompletedRooms.Any(item =>
                item.Act == world.Act
                && item.Floor == world.Floor))
        {
            throw new InvalidOperationException(
                "A map floor cannot be completed twice.");
        }

        world = world with
        {
            CompletedRoomHistory =
                world.CompletedRooms
                    .Append(new PrototypeCompletedRoomRecord(
                        world.Act,
                        world.Floor,
                        nodeId,
                        roomType))
                    .ToArray()
        };

        var player = ApplyRelicRunEvent(
            state.Player,
            PrototypeRunEventKind.RoomCompleted,
            rng: state.Rng,
            world: world);

        return state with
        {
            World = world,
            Player = player
        };
    }
}
