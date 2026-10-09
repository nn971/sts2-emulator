using System.Reflection;

namespace Sts2ReferenceBridge;

/// <summary>
/// Opt-in read-only native Act 1 run observation. This partial recorder
/// complements combat callbacks with run/room/event snapshots and never
/// calls gameplay commands, adds a Harmony patch or changes a player's run.
/// </summary>
internal static partial class PassiveReferenceRecorder
{
    private static object? _runManager;
    private static readonly HashSet<object> ObservedEvents =
        new(ReferenceEqualityComparer.Instance);
    private static readonly HashSet<object> ObservedPlayers =
        new(ReferenceEqualityComparer.Instance);

    private static bool ShouldCaptureRunObservation(string boundary) =>
        StringComparer.OrdinalIgnoreCase.Equals(
            _corpusMode, "underdocks-silent-act1")
        && (boundary.StartsWith("run_manager.", StringComparison.Ordinal)
            || boundary.StartsWith("run_event.", StringComparison.Ordinal)
            || boundary.StartsWith("run_player.", StringComparison.Ordinal)
            || boundary is
                "combat_manager.CombatSetUp"
                or "combat_manager.CombatBegan"
                or "combat_manager.CombatWon"
                or "combat_manager.CombatEnded");

    private static async Task AttachRunWhenReadyAsync(Assembly sts2Assembly)
    {
        if (!StringComparer.OrdinalIgnoreCase.Equals(
                _corpusMode, "underdocks-silent-act1"))
        {
            return;
        }

        // Unlike CombatManager, RunManager has a stable singleton before
        // the first battle, enabling observation of non-combat rooms.
        for (var attempt = 0; attempt < 480; attempt++)
        {
            try
            {
                var manager = TryResolveSingleton(
                    sts2Assembly, "MegaCrit.Sts2.Core.Runs.RunManager");
                if (manager is not null)
                {
                    _runManager = manager;
                    foreach (var signal in new[]
                    {
                        "RunStarted",
                        "RoomEntered",
                        "RoomExited",
                        "ActEntered"
                    })
                    {
                        SubscribeIfPresent(
                            manager, signal, "run_manager." + signal);
                    }

                    WriteDiagnostic("run_recorder_attached",
                        "Attached RunManager lifecycle signals; " +
                        "run snapshots are enabled for Underdocks corpus mode.");
                    return;
                }
            }
            catch (Exception ex)
            {
                WriteDiagnostic("run_attach_failed",
                    ex.GetType().Name + ": " + ex.Message);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250))
                .ConfigureAwait(false);
        }
        WriteDiagnostic("run_manager_timeout",
            "RunManager singleton unavailable within 120 seconds.");
    }

    private static void AttachRunPlayerSignals()
    {
        var manager = _runManager;
        var run = manager is null
            ? null : TryInvokeNoArg(manager, "DebugOnlyGetState");
        if (run is null)
        {
            return;
        }

        foreach (var player in Enumerate(GetProperty(run, "Players")))
        {
            bool firstObservation;
            lock (Gate)
            {
                firstObservation = ObservedPlayers.Add(player);
            }
            if (!firstObservation)
            {
                continue;
            }

            foreach (var signal in new[]
            {
                "GoldChanged",
                "RelicObtained",
                "RelicRemoved",
                "PotionProcured",
                "PotionDiscarded",
                "UsedPotionRemoved",
                "MaxPotionCountChanged"
            })
            {
                SubscribeIfPresent(
                    player, signal, "run_player." + signal);
            }
        }
    }

    private static void AttachActiveEventSignals()
    {
        var manager = _runManager;
        if (manager is null)
        {
            return;
        }

        var synchronizer = GetProperty(manager, "EventSynchronizer");
        foreach (var nativeEvent in Enumerate(
                     synchronizer is null
                         ? null
                         : GetProperty(synchronizer, "Events")))
        {
            bool firstObservation;
            lock (Gate)
            {
                firstObservation = ObservedEvents.Add(nativeEvent);
            }
            if (!firstObservation)
            {
                continue;
            }

            SubscribeIfPresent(
                nativeEvent, "StateChanged", "run_event.StateChanged");
            SubscribeIfPresent(
                nativeEvent, "EnteringEventCombat",
                "run_event.EnteringEventCombat");
        }
    }

    private static Dictionary<string, object?>? CaptureRunObservation(
        string boundary)
    {
        if (_runManager is not { } manager)
        {
            return new Dictionary<string, object?>
            {
                ["available"] = false,
                ["reason"] = "run_manager_unattached"
            };
        }

        // At room entry an EventModel may have just been instantiated.
        // Subscribe before sampling so subsequent page changes are seen.
        if (boundary is "run_manager.RunStarted"
            or "run_manager.RoomEntered"
            or "run_manager.ActEntered"
            or "combat_manager.CombatSetUp")
        {
            AttachRunPlayerSignals();
            AttachActiveEventSignals();
        }

        var run = TryInvokeNoArg(manager, "DebugOnlyGetState");
        if (run is null)
        {
            return new Dictionary<string, object?>
            {
                ["available"] = false,
                ["reason"] = "run_not_active"
            };
        }

        var result = ReadNamed(run,
            "CurrentActIndex", "ActFloor", "TotalFloor",
            "AscensionLevel", "GameMode", "CurrentRoomCount",
            "CurrentMapCoord", "MapLocation", "RunLocation",
            "IsGameOver", "Seed", "Id");

        result["available"] = true;
        result["boundary"] = boundary;
        result["current_room"] = SummarizeOpaque(
            GetProperty(run, "CurrentRoom"));
        result["base_room"] = SummarizeOpaque(
            GetProperty(run, "BaseRoom"));
        result["current_map_point"] = SummarizeMapPoint(
            GetProperty(run, "CurrentMapPoint"));
        result["run_rng"] = CaptureRngSet(GetProperty(run, "Rng"));
        result["run_odds"] = ReadOptionalNamed(
            GetProperty(run, "Odds"),
            "UnknownMapPoint", "CardRarity", "Potion",
            "PotionDropChance", "CardRarityOffset");
        result["visited_events"] = SummarizeCollection(
            GetProperty(run, "VisitedEventIds"));
        result["visited_map_coords"] = SummarizeCollection(
            GetProperty(run, "VisitedMapCoords"));

        // History contains the *actual* event option chosen, reward/card
        // selections, room stack, and per-player changes. Its current entry
        // is captured at every room boundary, without guessing actions.
        result["current_map_point_history"] = ProjectSerializable(
            GetProperty(run, "CurrentMapPointHistoryEntry"), depth: 5);

        result["players"] = Enumerate(GetProperty(run, "Players"))
            .Select(SummarizeRunPlayer)
            .ToArray();
        result["events"] = CaptureActiveEventOptions(manager);

        // Map topology need only be recorded on run/act/room entry.
        // Keeping it off combat-history callbacks limits trace growth.
        if (boundary is "run_manager.RunStarted"
            or "run_manager.ActEntered"
            or "run_manager.RoomEntered")
        {
            result["map"] = CaptureMapGraph(GetProperty(run, "Map"));
        }

        return result;
    }

    private static object? ReadOptionalNamed(
        object? value, params string[] names) =>
        value is null ? null : ReadNamed(value, names);

    private static Dictionary<string, object?> SummarizeRunPlayer(
        object player)
    {
        var result = ReadNamed(player,
            "Gold", "Character", "NetId", "MaxEnergy",
            "MaxPotionCount", "HasOpenPotionSlots");
        var creature = GetProperty(player, "Creature");
        if (creature is not null)
        {
            result["creature"] = SummarizeCreature(creature);
        }

        // The persistent deck is *not* the in-combat draw pile.
        var deck = GetProperty(player, "Deck");
        result["deck"] = SummarizeCollection(
            deck is null ? null : GetProperty(deck, "Cards"));
        result["potions"] = SummarizeCollection(
            GetProperty(player, "PotionSlots"));
        result["relics"] = SummarizeCollection(
            GetProperty(player, "Relics"));
        result["player_rng"] = CaptureRngSet(
            GetProperty(player, "PlayerRng"));
        result["player_odds"] = ReadOptionalNamed(
            GetProperty(player, "PlayerOdds"),
            "Potion", "PotionDropChance", "CardRarityOffset");
        return result;
    }

    private static object[] CaptureActiveEventOptions(object manager)
    {
        var sync = GetProperty(manager, "EventSynchronizer");
        var events = sync is null
            ? null
            : GetProperty(sync, "Events");
        return Enumerate(events).Select(nativeEvent =>
        {
            var result = ReadNamed(nativeEvent,
                "Id", "IsFinished", "LayoutType");
            result["options"] = Enumerate(
                    GetProperty(nativeEvent, "CurrentOptions"))
                .Select(option => ReadNamed(option,
                    "TextKey", "IsLocked", "IsProceed", "WasChosen"))
                .ToArray();
            result["event_rng"] = CaptureRngSet(
                GetProperty(nativeEvent, "Rng"));
            return (object)result;
        }).ToArray();
    }

    private static object? SummarizeMapPoint(object? point)
    {
        if (point is null)
        {
            return null;
        }

        var result = ReadNamed(point, "PointType", "CanBeModified");
        var coordinate = point.GetType().GetField(
            "coord", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(point);
        result["coord"] = ProjectSerializable(coordinate, depth: 2);
        result["children"] = Enumerate(GetProperty(point, "Children"))
            .Take(12)
            .Select(child =>
                ProjectSerializable(child.GetType().GetField(
                    "coord", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(child), depth: 2))
            .ToArray();
        return result;
    }

    private static object? CaptureMapGraph(object? map)
    {
        if (map is null)
        {
            return null;
        }

        return new Dictionary<string, object?>
        {
            ["type"] = map.GetType().FullName,
            ["columns"] = TryInvokeNoArg(map, "GetColumnCount"),
            ["rows"] = TryInvokeNoArg(map, "GetRowCount"),
            ["starting_point"] = SummarizeMapPoint(
                GetProperty(map, "StartingMapPoint")),
            ["boss_point"] = SummarizeMapPoint(
                GetProperty(map, "BossMapPoint")),
            ["second_boss_point"] = SummarizeMapPoint(
                GetProperty(map, "SecondBossMapPoint")),
            ["points"] = Enumerate(TryInvokeNoArg(
                    map, "GetAllMapPoints"))
                .Take(200)
                .Select(SummarizeMapPoint)
                .ToArray()
        };
    }
}
