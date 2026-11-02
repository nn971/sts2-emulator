using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace NativeV111MetadataProbe;

/// <summary>
/// Read-only PE metadata check. Opening an assembly via PEReader does not
/// execute game code, load dependencies, instantiate game objects, or call
/// any private reflection target.
/// </summary>
internal static class Program
{
    private sealed record Member(string Type, string Kind, string Name);
    private sealed record Result(string Type, string Kind, string Name, bool Present);

    private static readonly Member[] Required =
    [
        new("MegaCrit.Sts2.Core.Models.ActModel", "field", "_rooms"),
        new("MegaCrit.Sts2.Core.Rooms.RoomSet", "field", "events"),
        new("MegaCrit.Sts2.Core.Rooms.RoomSet", "field", "normalEncounters"),
        new("MegaCrit.Sts2.Core.Rooms.RoomSet", "field", "eliteEncounters"),
        new("MegaCrit.Sts2.Core.Rooms.RoomSet", "field", "eventsVisited"),
        new("MegaCrit.Sts2.Core.Rooms.RoomSet", "field", "normalEncountersVisited"),
        new("MegaCrit.Sts2.Core.Rooms.RoomSet", "field", "eliteEncountersVisited"),
        new("MegaCrit.Sts2.Core.Rooms.RoomSet", "field", "bossEncountersVisited"),
        new("MegaCrit.Sts2.Core.Rooms.RoomSet", "property", "Boss"),
        new("MegaCrit.Sts2.Core.Rooms.RoomSet", "property", "SecondBoss"),
        new("MegaCrit.Sts2.Core.Rooms.RoomSet", "property", "Ancient"),
        new("MegaCrit.Sts2.Core.Map.StandardActMap", "field", "_rng"),
        new("MegaCrit.Sts2.Core.Runs.RunManager", "event", "RunStarted"),
        new("MegaCrit.Sts2.Core.Runs.RunManager", "event", "RoomEntered"),
        new("MegaCrit.Sts2.Core.Runs.RunState", "property", "Acts"),
        new("MegaCrit.Sts2.Core.Runs.RunState", "property", "Rng"),
        new("MegaCrit.Sts2.Core.Runs.RunState", "property", "Players"),
        // Turn-boundary checkpoint support: API existence only. Method
        // ordering and queue settlement require native execution evidence.
        new("MegaCrit.Sts2.Core.Combat.CombatManager", "property", "IsInProgress"),
        new("MegaCrit.Sts2.Core.Combat.CombatManager", "method", "DebugOnlyGetState"),
        new("MegaCrit.Sts2.Core.Combat.CombatState", "property", "CurrentSide"),
        new("MegaCrit.Sts2.Core.GameActions.ActionExecutor", "event", "BeforeActionExecuted"),
        new("MegaCrit.Sts2.Core.GameActions.ActionExecutor", "event", "AfterActionExecuted"),
        // Source-pinned queue quiescence contracts. Method existence is
        // verified without invoking the original game or traversing queues.
        new("MegaCrit.Sts2.Core.GameActions.ActionExecutor", "property", "IsRunning"),
        new("MegaCrit.Sts2.Core.GameActions.ActionExecutor", "property", "IsPaused"),
        new("MegaCrit.Sts2.Core.GameActions.ActionExecutor", "property", "CurrentlyRunningAction"),
        new("MegaCrit.Sts2.Core.GameActions.ActionExecutor", "method", "FinishedExecutingActions"),
        new("MegaCrit.Sts2.Core.GameActions.Multiplayer.ActionQueueSet", "property", "IsEmpty"),
        new("MegaCrit.Sts2.Core.GameActions.Multiplayer.ActionQueueSet", "method", "BecameEmpty"),
        new("MegaCrit.Sts2.Core.GameActions.Multiplayer.ActionQueueSynchronizer", "property", "CombatState"),
        new("MegaCrit.Sts2.Core.GameActions.Multiplayer.ActionQueueSynchronizer", "field",
            "_requestedActionsWaitingForPlayerTurn"),
        new("MegaCrit.Sts2.Core.GameActions.ReadyToBeginEnemyTurnAction", "type", ""),
        new("MegaCrit.Sts2.Core.GameActions.EndPlayerTurnAction", "type", "")
    ];

    private static bool Exists(MetadataReader reader, Member requested)
    {
        foreach (var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);
            var name = reader.GetString(type.Namespace) + "."
                + reader.GetString(type.Name);
            if (name != requested.Type) continue;

            switch (requested.Kind)
            {
                case "type":
                    return true;
                case "method":
                    return type.GetMethods().Any(item =>
                        reader.GetString(reader.GetMethodDefinition(item).Name)
                            == requested.Name);
                case "field":
                    return type.GetFields().Any(item =>
                        reader.GetString(reader.GetFieldDefinition(item).Name)
                            == requested.Name);
                case "property":
                    return type.GetProperties().Any(item =>
                        reader.GetString(reader.GetPropertyDefinition(item).Name)
                            == requested.Name);
                case "event":
                    return type.GetEvents().Any(item =>
                        reader.GetString(reader.GetEventDefinition(item).Name)
                            == requested.Name);
                default:
                    throw new InvalidOperationException(
                        "Unsupported metadata member kind.");
            }
        }
        return false;
    }

    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: NativeV111MetadataProbe <installed-sts2.dll>");
            return 2;
        }
        try
        {
            var path = Path.GetFullPath(args[0]);
            using var input = File.OpenRead(path);
            using var pe = new PEReader(input);
            if (!pe.HasMetadata)
                throw new BadImageFormatException("PE has no managed metadata.");
            var reader = pe.GetMetadataReader();
            var checks = Required.Select(expected => new Result(
                expected.Type, expected.Kind, expected.Name,
                Exists(reader, expected))).ToArray();
            input.Position = 0;
            var hash = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
            var matched = checks.All(check => check.Present);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                schema = "sts2-native-metadata-contract-v1",
                status = matched ? "NATIVE_METADATA_MEMBERS_PRESENT"
                                 : "NATIVE_METADATA_MEMBERS_MISSING",
                managed_assembly_sha256 = hash,
                metadata_only = true,
                native_game_executed = false,
                reflection_runtime_parity = "NOT_TESTED",
                checks = checks.Select(check => new
                {
                    type = check.Type, kind = check.Kind,
                    member = check.Name, present = check.Present
                })
            }, new JsonSerializerOptions { WriteIndented = true }));
            return matched ? 0 : 2;
        }
        catch (Exception error) when (error is IOException
            or UnauthorizedAccessException or BadImageFormatException
            or InvalidOperationException)
        {
            Console.Error.WriteLine("NATIVE_METADATA_PROBE_FAILED: "
                + error.GetType().Name + ": " + error.Message);
            return 2;
        }
    }
}
