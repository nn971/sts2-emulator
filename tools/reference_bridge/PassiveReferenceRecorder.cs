using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MegaCrit.Sts2.Core.Modding;
using Sts2Emulator.Trace;

namespace Sts2ReferenceBridge;

internal static class PassiveReferenceRecorder
{
    private static readonly object Gate = new();
    private static readonly List<(object Source, EventInfo Event, Delegate Handler)> Subscriptions = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private static StreamWriter? _writer;
    private static object? _combatManager;
    private static long _sequence;
    private static string? _outputPath;

    public static void Initialize()
    {
        try
        {
            var sts2Assembly = typeof(ModInitializerAttribute).Assembly;
            var dataDirectory = Path.GetDirectoryName(sts2Assembly.Location)
                ?? throw new InvalidOperationException("Cannot locate sts2.dll directory.");
            var gameDirectory = Directory.GetParent(dataDirectory)?.FullName
                ?? throw new InvalidOperationException("Cannot locate STS2 game directory.");

            var build = ReferenceBuildFingerprint.Capture(gameDirectory, dataDirectory);
            if (!StringComparer.Ordinal.Equals(
                    build.BuildFingerprint,
                    BridgeWorkspace.ExpectedBuildFingerprint))
            {
                Console.Error.WriteLine(
                    "[Sts2ReferenceBridge] Refusing to record: installed build fingerprint " +
                    $"{build.BuildFingerprint} != expected {BridgeWorkspace.ExpectedBuildFingerprint}.");
                return;
            }

            var traceDirectory =
                Environment.GetEnvironmentVariable("STS2_REFERENCE_TRACE_DIR");
            if (string.IsNullOrWhiteSpace(traceDirectory))
            {
                traceDirectory = Path.Combine(gameDirectory, "reference_traces");
            }

            Directory.CreateDirectory(traceDirectory);
            _outputPath = Path.Combine(
                traceDirectory,
                $"probe-{DateTime.UtcNow:yyyyMMdd-HHmmss}-" +
                $"{build.BuildFingerprint[..12]}.jsonl");

            _writer = new StreamWriter(
                new FileStream(
                    _outputPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true
            };

            WriteRecord(new Dictionary<string, object?>
            {
                ["type"] = "session",
                ["schema"] = BridgeWorkspace.ProbeSchema,
                ["captured_at_utc"] = DateTimeOffset.UtcNow,
                ["expected_game_version"] = BridgeWorkspace.ExpectedGameVersion,
                ["expected_game_commit"] = BridgeWorkspace.ExpectedGameCommit,
                ["build_fingerprint"] = build.BuildFingerprint,
                ["build"] = build.Identity,
                ["bridge_assembly"] = typeof(PassiveReferenceRecorder).Assembly
                    .GetName().Version?.ToString(),
                ["output_path"] = _outputPath
            });

            _combatManager = ResolveSingleton(
                sts2Assembly,
                "MegaCrit.Sts2.Core.Combat.CombatManager");

            foreach (var signal in new[]
            {
                "CombatSetUp",
                "CombatBegan",
                "TurnStarted",
                "PlayerEndedTurn",
                "AboutToSwitchToEnemyTurn",
                "TurnEnded",
                "CombatWon",
                "CombatEnded"
            })
            {
                SubscribeIfPresent(_combatManager, signal, $"combat_manager.{signal}");
            }

            var verbose = StringComparer.Ordinal.Equals(
                Environment.GetEnvironmentVariable("STS2_REFERENCE_VERBOSE"),
                "1");
            if (verbose)
            {
                var tracker = GetProperty(_combatManager, "StateTracker");
                if (tracker is not null)
                {
                    SubscribeIfPresent(
                        tracker,
                        "CombatStateChanged",
                        "combat_state_tracker.CombatStateChanged");
                }
            }

            AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
            Console.WriteLine(
                $"[Sts2ReferenceBridge] passive recorder active: {_outputPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[Sts2ReferenceBridge] initialization failed: {ex}");
            Shutdown();
        }
    }

    private static void SubscribeIfPresent(
        object source,
        string eventName,
        string boundary)
    {
        var evt = source.GetType().GetEvent(
            eventName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (evt?.EventHandlerType is null)
        {
            WriteDiagnostic(
                "event_missing",
                $"{source.GetType().FullName}.{eventName}");
            return;
        }

        var handler = CreateForwarder(evt.EventHandlerType, boundary);
        evt.AddEventHandler(source, handler);
        Subscriptions.Add((source, evt, handler));
    }

    private static Delegate CreateForwarder(
        Type delegateType,
        string boundary)
    {
        var invoke = delegateType.GetMethod("Invoke")
            ?? throw new InvalidOperationException(
                $"Delegate {delegateType.FullName} has no Invoke method.");

        var parameters = invoke.GetParameters()
            .Select(parameter =>
                Expression.Parameter(parameter.ParameterType, parameter.Name))
            .ToArray();

        var boxedArgs = Expression.NewArrayInit(
            typeof(object),
            parameters.Select(parameter =>
                Expression.Convert(parameter, typeof(object))));

        var callback = Expression.Call(
            typeof(PassiveReferenceRecorder),
            nameof(OnSignal),
            Type.EmptyTypes,
            Expression.Constant(boundary),
            boxedArgs);

        Expression body = invoke.ReturnType == typeof(void)
            ? callback
            : Expression.Block(callback, Expression.Default(invoke.ReturnType));

        return Expression.Lambda(delegateType, body, parameters).Compile();
    }

    public static void OnSignal(string boundary, object?[] args)
    {
        try
        {
            var state = CaptureState();
            var stateJson = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions);
            var stateHash = Convert.ToHexStringLower(SHA256.HashData(stateJson));

            WriteRecord(new Dictionary<string, object?>
            {
                ["type"] = "boundary",
                ["schema"] = BridgeWorkspace.ProbeSchema,
                ["sequence"] = Interlocked.Increment(ref _sequence),
                ["captured_at_utc"] = DateTimeOffset.UtcNow,
                ["boundary"] = boundary,
                ["state_hash"] = stateHash,
                ["arguments"] = args.Select(SummarizeOpaque).ToArray(),
                ["state"] = state
            });
        }
        catch (Exception ex)
        {
            WriteDiagnostic("capture_failed", $"{boundary}: {ex}");
        }
    }

    private static Dictionary<string, object?> CaptureState()
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (_combatManager is null)
        {
            return result;
        }

        result["manager"] = ReadNamed(
            _combatManager,
            "CurrentCombatId",
            "IsInProgress",
            "IsOverOrEnding",
            "IsStarting",
            "IsEnding",
            "IsEnemyTurnStarted",
            "PlayerActionsDisabled");

        var combatState = InvokeNoArg(_combatManager, "DebugOnlyGetState");
        if (combatState is null)
        {
            result["combat"] = null;
            return result;
        }

        result["combat"] = ReadNamed(
            combatState,
            "CurrentSide",
            "RoundNumber",
            "Encounter");

        var players = GetProperty(combatState, "Players");
        result["players"] = Enumerate(players)
            .Select(SummarizePlayer)
            .ToArray();

        var enemies = GetProperty(combatState, "Enemies");
        result["enemies"] = Enumerate(enemies)
            .Select(SummarizeCreature)
            .ToArray();

        var runState = GetProperty(combatState, "RunState");
        if (runState is not null)
        {
            result["run"] = ReadNamed(
                runState,
                "CurrentAct",
                "CurrentRoom",
                "CurrentMapPoint",
                "Floor",
                "AscensionLevel");

            var runRng = GetProperty(runState, "Rng");
            result["run_rng"] = CaptureRngSet(runRng);
        }

        return result;
    }

    private static Dictionary<string, object?> SummarizePlayer(object player)
    {
        var result = ReadNamed(
            player,
            "CombatId",
            "CurrentHp",
            "MaxHp",
            "Gold",
            "Character",
            "PlayerRng");

        var combat = GetProperty(player, "PlayerCombatState");
        if (combat is not null)
        {
            result["combat"] = ReadNamed(
                combat,
                "Energy",
                "MaxEnergy",
                "TurnNumber",
                "Phase",
                "Stars");

            foreach (var pileName in new[]
            {
                "Hand",
                "DrawPile",
                "DiscardPile",
                "ExhaustPile",
                "PlayPile"
            })
            {
                var pile = GetProperty(combat, pileName);
                result[ToSnakeCase(pileName)] = SummarizeCollection(pile);
            }
        }

        var playerRng = GetProperty(player, "PlayerRng");
        if (playerRng is not null)
        {
            result["player_rng"] = CaptureRngSet(playerRng);
        }

        return result;
    }

    private static Dictionary<string, object?> SummarizeCreature(object creature)
    {
        var result = ReadNamed(
            creature,
            "CombatId",
            "CurrentHp",
            "MaxHp",
            "Block",
            "IsDead",
            "Model",
            "Powers");

        return result;
    }

    private static object? CaptureRngSet(object? rngSet)
    {
        if (rngSet is null)
        {
            return null;
        }

        try
        {
            var serializable = InvokeNoArg(rngSet, "ToSerializable");
            return ProjectSerializable(serializable, depth: 5);
        }
        catch (Exception ex)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = rngSet.GetType().FullName,
                ["error"] = ex.GetType().Name,
                ["message"] = ex.Message
            };
        }
    }

    private static Dictionary<string, object?> ReadNamed(
        object target,
        params string[] propertyNames)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = target.GetType().FullName
        };

        foreach (var propertyName in propertyNames)
        {
            var value = GetProperty(target, propertyName);
            if (value is null)
            {
                continue;
            }

            result[ToSnakeCase(propertyName)] =
                propertyName is "PlayerRng" or "Powers"
                    ? SummarizeOpaque(value)
                    : ProjectSimple(value);
        }

        return result;
    }

    private static object? SummarizeCollection(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var items = Enumerate(value).Take(128).ToArray();
        return new Dictionary<string, object?>
        {
            ["type"] = value.GetType().FullName,
            ["count"] = items.Length,
            ["items"] = items.Select(SummarizeOpaque).ToArray()
        };
    }

    private static object? SummarizeOpaque(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var simple = ProjectSimple(value);
        if (!ReferenceEquals(simple, value))
        {
            return simple;
        }

        return ReadNamed(
            value,
            "Id",
            "Entry",
            "CombatId",
            "CurrentHp",
            "MaxHp",
            "Block",
            "Amount",
            "Index");
    }

    private static object? ProjectSimple(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var type = value.GetType();
        if (type.IsEnum)
        {
            return value.ToString();
        }

        if (value is string
            or bool
            or byte
            or sbyte
            or short
            or ushort
            or int
            or uint
            or long
            or ulong
            or float
            or double
            or decimal
            or DateTime
            or DateTimeOffset
            or Guid)
        {
            return value;
        }

        return value;
    }

    private static object? ProjectSerializable(
        object? value,
        int depth)
    {
        if (value is null)
        {
            return null;
        }

        var simple = ProjectSimple(value);
        if (!ReferenceEquals(simple, value))
        {
            return simple;
        }

        if (value is JsonElement element)
        {
            return element.Clone();
        }

        if (depth <= 0)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = value.GetType().FullName
            };
        }

        if (value is IDictionary dictionary)
        {
            var projected = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (DictionaryEntry item in dictionary)
            {
                projected[item.Key?.ToString() ?? "<null>"] =
                    ProjectSerializable(item.Value, depth - 1);
            }

            return projected;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            var projected = new List<object?>();
            foreach (var item in enumerable)
            {
                if (projected.Count >= 512)
                {
                    break;
                }

                projected.Add(ProjectSerializable(item, depth - 1));
            }

            return projected;
        }

        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = value.GetType().FullName
        };

        foreach (var property in value.GetType().GetProperties(
                     BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead
                || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            try
            {
                result[ToSnakeCase(property.Name)] = ProjectSerializable(
                    property.GetValue(value),
                    depth - 1);
            }
            catch
            {
                // Probe serialization is best-effort and must never disturb gameplay.
            }
        }

        return result;
    }

    private static object ResolveSingleton(
        Assembly assembly,
        string typeName)
    {
        var type = assembly.GetType(typeName, throwOnError: true)
            ?? throw new InvalidOperationException($"Missing type {typeName}.");
        var instance = type.GetProperty(
            "Instance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(null);

        return instance
            ?? throw new InvalidOperationException(
                $"{typeName}.Instance returned null during mod initialization.");
    }

    private static object? GetProperty(
        object target,
        string propertyName)
    {
        try
        {
            return target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(target);
        }
        catch
        {
            return null;
        }
    }

    private static object? InvokeNoArg(
        object target,
        string methodName)
    {
        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        return method?.Invoke(target, null);
    }

    private static IEnumerable<object> Enumerate(object? value)
    {
        if (value is not IEnumerable enumerable)
        {
            yield break;
        }

        foreach (var item in enumerable)
        {
            if (item is not null)
            {
                yield return item;
            }
        }
    }

    private static string ToSnakeCase(string value) =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(value);

    private static void WriteDiagnostic(
        string code,
        string message)
    {
        WriteRecord(new Dictionary<string, object?>
        {
            ["type"] = "diagnostic",
            ["schema"] = BridgeWorkspace.ProbeSchema,
            ["sequence"] = Interlocked.Increment(ref _sequence),
            ["captured_at_utc"] = DateTimeOffset.UtcNow,
            ["code"] = code,
            ["message"] = message
        });
    }

    private static void WriteRecord(object record)
    {
        lock (Gate)
        {
            if (_writer is null)
            {
                return;
            }

            _writer.WriteLine(JsonSerializer.Serialize(record, JsonOptions));
        }
    }

    private static void Shutdown()
    {
        lock (Gate)
        {
            foreach (var subscription in Subscriptions)
            {
                try
                {
                    subscription.Event.RemoveEventHandler(
                        subscription.Source,
                        subscription.Handler);
                }
                catch
                {
                    // Process exit / game shutdown: best effort only.
                }
            }

            Subscriptions.Clear();

            if (_writer is not null)
            {
                try
                {
                    _writer.Flush();
                    _writer.Dispose();
                }
                catch
                {
                    // Best effort during shutdown.
                }

                _writer = null;
            }
        }
    }
}
