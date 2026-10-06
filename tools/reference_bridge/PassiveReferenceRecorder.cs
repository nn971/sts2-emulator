using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MegaCrit.Sts2.Core.Modding;

namespace Sts2ReferenceBridge;

internal static class PassiveReferenceRecorder
{
    private static readonly object Gate = new();
    private static readonly List<(object Source, EventInfo Event, Delegate Handler)> Subscriptions = [];
    private static readonly HashSet<string> CataloguedTypes =
        new(StringComparer.Ordinal);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private static StreamWriter? _writer;
    private static object? _combatManager;
    private static long _sequence;
    private static int _initialized;

    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            return;
        }

        try
        {
            var sts2Assembly = typeof(ModInitializerAttribute).Assembly;
            var dataDirectory = Path.GetDirectoryName(sts2Assembly.Location)
                ?? throw new InvalidOperationException("Cannot locate sts2.dll directory.");
            var gameDirectory = Directory.GetParent(dataDirectory)?.FullName
                ?? throw new InvalidOperationException("Cannot locate STS2 game directory.");

            var build = VerifyPinnedBuild(gameDirectory, dataDirectory);

            var traceDirectory =
                Environment.GetEnvironmentVariable("STS2_REFERENCE_TRACE_DIR");
            if (string.IsNullOrWhiteSpace(traceDirectory))
            {
                traceDirectory = Path.Combine(gameDirectory, "reference_traces");
            }

            Directory.CreateDirectory(traceDirectory);
            var outputPath = Path.Combine(
                traceDirectory,
                $"probe-{DateTime.UtcNow:yyyyMMdd-HHmmss}-" +
                $"{BridgeWorkspace.ExpectedBuildFingerprint[..12]}.jsonl");

            _writer = new StreamWriter(
                new FileStream(
                    outputPath,
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
                ["build_fingerprint"] = BridgeWorkspace.ExpectedBuildFingerprint,
                ["build"] = build,
                ["bridge_assembly"] = typeof(PassiveReferenceRecorder).Assembly
                    .GetName().Version?.ToString(),
                ["output_path"] = outputPath
            });

            AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();

            Console.WriteLine(
                $"[Sts2ReferenceBridge] pinned build verified; recorder output: {outputPath}");

            _ = Task.Run(() => AttachWhenReadyAsync(sts2Assembly));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[Sts2ReferenceBridge] initialization failed: {ex}");
            Shutdown();
        }
    }

    private static async Task AttachWhenReadyAsync(Assembly sts2Assembly)
    {
        const int attempts = 480;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                var manager = TryResolveSingleton(
                    sts2Assembly,
                    "MegaCrit.Sts2.Core.Combat.CombatManager");

                if (manager is not null)
                {
                    AttachCombatSignals(manager);
                    return;
                }
            }
            catch (Exception ex)
            {
                WriteDiagnostic(
                    "attach_attempt_failed",
                    $"attempt={attempt}: {ex.GetType().Name}: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
        }

        WriteDiagnostic(
            "combat_manager_timeout",
            "CombatManager.Instance did not become available within 120 seconds.");
    }

    private static void AttachCombatSignals(object manager)
    {
        lock (Gate)
        {
            if (_combatManager is not null)
            {
                return;
            }

            _combatManager = manager;
        }

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
            SubscribeIfPresent(manager, signal, $"combat_manager.{signal}");
        }

        var history = GetProperty(manager, "History");
        if (history is not null)
        {
            SubscribeIfPresent(
                history,
                "Changed",
                "combat_history.Changed");
        }
        else
        {
            WriteDiagnostic(
                "combat_history_missing",
                "CombatManager.History was null when recorder attached.");
        }

        var verbose = StringComparer.Ordinal.Equals(
            Environment.GetEnvironmentVariable("STS2_REFERENCE_VERBOSE"),
            "1");
        if (verbose)
        {
            var tracker = GetProperty(manager, "StateTracker");
            if (tracker is not null)
            {
                SubscribeIfPresent(
                    tracker,
                    "CombatStateChanged",
                    "combat_state_tracker.CombatStateChanged");
            }
            else
            {
                WriteDiagnostic(
                    "state_tracker_missing",
                    "CombatManager.StateTracker was null when recorder attached.");
            }
        }

        WriteDiagnostic(
            "recorder_attached",
            $"Subscribed to passive combat signals on {manager.GetType().FullName}.");
    }

    private static Dictionary<string, object?> VerifyPinnedBuild(
        string gameDirectory,
        string dataDirectory)
    {
        var releaseInfoPath = ResolveMetadataFile(
            gameDirectory,
            dataDirectory,
            "release_info.json");
        var runtimeConfigPath = ResolveMetadataFile(
            gameDirectory,
            dataDirectory,
            "sts2.runtimeconfig.json");

        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sts2.dll"] = Path.Combine(dataDirectory, "sts2.dll"),
            ["0Harmony.dll"] = Path.Combine(dataDirectory, "0Harmony.dll"),
            ["GodotSharp.dll"] = Path.Combine(dataDirectory, "GodotSharp.dll"),
            ["release_info.json"] = releaseInfoPath,
            ["sts2.runtimeconfig.json"] = runtimeConfigPath
        };

        var fileInfo = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var expected in BridgeWorkspace.ExpectedFileHashes)
        {
            if (!paths.TryGetValue(expected.Key, out var path) || !File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Pinned reference file is missing: {expected.Key}",
                    path);
            }

            var actualHash = Sha256File(path);
            if (!StringComparer.OrdinalIgnoreCase.Equals(actualHash, expected.Value))
            {
                throw new InvalidOperationException(
                    $"Pinned reference hash mismatch for {expected.Key}: " +
                    $"{actualHash} != {expected.Value}.");
            }

            fileInfo[expected.Key] = new Dictionary<string, object?>
            {
                ["sha256"] = actualHash,
                ["size_bytes"] = new FileInfo(path).Length
            };
        }

        using var releaseDocument = JsonDocument.Parse(
            File.ReadAllText(releaseInfoPath));
        var release = releaseDocument.RootElement;
        var version = release.TryGetProperty("version", out var versionElement)
            ? versionElement.GetString()
            : null;
        var commit = release.TryGetProperty("commit", out var commitElement)
            ? commitElement.GetString()
            : null;

        if (!StringComparer.Ordinal.Equals(
                version,
                BridgeWorkspace.ExpectedGameVersion)
            || !StringComparer.Ordinal.Equals(
                commit,
                BridgeWorkspace.ExpectedGameCommit))
        {
            throw new InvalidOperationException(
                $"Pinned release mismatch: version={version}, commit={commit}.");
        }

        using var runtimeDocument = JsonDocument.Parse(
            File.ReadAllText(runtimeConfigPath));
        var tfm = runtimeDocument.RootElement
            .GetProperty("runtimeOptions")
            .GetProperty("tfm")
            .GetString();

        if (!StringComparer.Ordinal.Equals(
                tfm,
                BridgeWorkspace.ExpectedTargetFramework))
        {
            throw new InvalidOperationException(
                $"Pinned target framework mismatch: {tfm}.");
        }

        return new Dictionary<string, object?>
        {
            ["version"] = version,
            ["commit"] = commit,
            ["target_framework"] = tfm,
            ["files"] = fileInfo
        };
    }

    private static string ResolveMetadataFile(
        string gameDirectory,
        string dataDirectory,
        string fileName)
    {
        var rootPath = Path.Combine(gameDirectory, fileName);
        if (File.Exists(rootPath))
        {
            return rootPath;
        }

        var dataPath = Path.Combine(dataDirectory, fileName);
        if (File.Exists(dataPath))
        {
            return dataPath;
        }

        throw new FileNotFoundException(
            $"Required pinned metadata file '{fileName}' was not found.");
    }

    private static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static void SubscribeIfPresent(
        object source,
        string eventName,
        string boundary)
    {
        try
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

            lock (Gate)
            {
                Subscriptions.Add((source, evt, handler));
            }
        }
        catch (Exception ex)
        {
            WriteDiagnostic(
                "event_subscribe_failed",
                $"{source.GetType().FullName}.{eventName}: {ex}");
        }
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

            var record = new Dictionary<string, object?>
            {
                ["type"] = "boundary",
                ["schema"] = BridgeWorkspace.ProbeSchema,
                ["sequence"] = Interlocked.Increment(ref _sequence),
                ["captured_at_utc"] = DateTimeOffset.UtcNow,
                ["boundary"] = boundary,
                ["state_hash"] = stateHash,
                ["arguments"] = args.Select(SummarizeOpaque).ToArray(),
                ["state"] = state
            };

            if (StringComparer.Ordinal.Equals(
                    boundary,
                    "combat_history.Changed"))
            {
                record["history_entry"] = CaptureHistoryTail();
            }

            WriteRecord(record);
        }
        catch (Exception ex)
        {
            WriteDiagnostic("capture_failed", $"{boundary}: {ex}");
        }
    }

    private static Dictionary<string, object?> CaptureState()
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        var manager = _combatManager;
        if (manager is null)
        {
            return result;
        }

        result["manager"] = ReadNamed(
            manager,
            "CurrentCombatId",
            "IsInProgress",
            "IsOverOrEnding",
            "IsStarting",
            "IsEnding",
            "IsEnemyTurnStarted",
            "PlayerActionsDisabled");

        var combatState = TryInvokeNoArg(manager, "DebugOnlyGetState");
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

            result["run_rng"] = CaptureRngSet(
                GetProperty(runState, "Rng"));
        }

        return result;
    }

    private static Dictionary<string, object?> SummarizePlayer(object player)
    {
        WriteTypeCatalogOnce(player.GetType());

        var result = ReadNamed(
            player,
            "CombatId",
            "CurrentHp",
            "MaxHp",
            "Gold",
            "Character");

        var creature = FindNestedByTypeSuffix(
            player,
            ".Entities.Creatures.Creature");
        if (creature is not null)
        {
            result["creature"] = SummarizeCreature(creature);
        }

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
                result[ToSnakeCase(pileName)] =
                    SummarizeCollection(GetProperty(combat, pileName));
            }
        }

        result["player_rng"] = CaptureRngSet(
            GetProperty(player, "PlayerRng"));

        return result;
    }

    private static Dictionary<string, object?> SummarizeCreature(object creature)
    {
        return ReadNamed(
            creature,
            "CombatId",
            "CurrentHp",
            "MaxHp",
            "Block",
            "IsDead",
            "Model",
            "Powers");
    }

    private static object? CaptureRngSet(object? rngSet)
    {
        if (rngSet is null)
        {
            return null;
        }

        try
        {
            var serializable = TryInvokeNoArg(rngSet, "ToSerializable");
            if (serializable is not null)
            {
                WriteTypeCatalogOnce(serializable.GetType());
            }

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
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property is null
                || !property.CanRead
                || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            try
            {
                var value = property.GetValue(target);
                result[ToSnakeCase(propertyName)] =
                    propertyName is "Powers"
                        ? SummarizeCollection(value)
                        : SummarizeOpaque(value);
            }
            catch
            {
                // Observation must not disturb gameplay.
            }
        }

        return result;
    }

    private static object? SummarizeCollection(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var source = value;
        var items = Enumerate(source).Take(128).ToArray();

        if (items.Length == 0)
        {
            foreach (var alternate in new[] { "Cards", "Contents", "Items" })
            {
                var nested = GetProperty(value, alternate);
                var nestedItems = Enumerate(nested).Take(128).ToArray();
                if (nestedItems.Length > 0)
                {
                    source = nested!;
                    items = nestedItems;
                    break;
                }
            }
        }

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

        if (TryProjectSimple(value, out var simple))
        {
            return simple;
        }

        if (value is IEnumerable and not string)
        {
            return SummarizeCollection(value);
        }

        var fullName = value.GetType().FullName ?? string.Empty;
        if (fullName.Contains(".Models.Cards.", StringComparison.Ordinal)
            || fullName.Contains(".Combat.History.Entries.", StringComparison.Ordinal))
        {
            WriteTypeCatalogOnce(value.GetType());
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
            "Index",
            "UpgradeLevel",
            "UpgradeCount",
            "IsUpgraded",
            "EnergyCost",
            "CurrentEnergyCost",
            "Cost",
            "Target",
            "Owner");
    }

    private static bool TryProjectSimple(
        object value,
        out object? projected)
    {
        var type = value.GetType();
        if (type.IsEnum)
        {
            projected = value.ToString();
            return true;
        }

        if (value is JsonElement element)
        {
            projected = element.Clone();
            return true;
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
            projected = value;
            return true;
        }

        projected = null;
        return false;
    }

    private static object? ProjectSerializable(
        object? value,
        int depth)
    {
        if (value is null)
        {
            return null;
        }

        if (TryProjectSimple(value, out var simple))
        {
            return simple;
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
                // Serializable projection is best-effort.
            }
        }

        // SerializableRng in the pinned build exposes its state through fields
        // rather than public getters. Fall back to fields only when public
        // properties yielded no semantic payload.
        if (result.Count == 1)
        {
            foreach (var field in value.GetType().GetFields(
                         BindingFlags.Instance |
                         BindingFlags.Public |
                         BindingFlags.NonPublic))
            {
                if (field.IsStatic || typeof(Delegate).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }

                try
                {
                    result[NormalizeFieldName(field.Name)] = ProjectSerializable(
                        field.GetValue(value),
                        depth - 1);
                }
                catch
                {
                    // Private-field projection is best-effort and read-only.
                }
            }
        }

        return result;
    }

    private static void WriteTypeCatalogOnce(Type type)
    {
        var typeName = type.FullName ?? type.Name;
        lock (Gate)
        {
            if (!CataloguedTypes.Add(typeName))
            {
                return;
            }
        }

        var flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        var properties = type.GetProperties(flags)
            .Where(property => property.GetIndexParameters().Length == 0)
            .Select(property => new Dictionary<string, object?>
            {
                ["name"] = property.Name,
                ["type"] = property.PropertyType.FullName,
                ["can_read"] = property.CanRead,
                ["can_write"] = property.CanWrite,
                ["getter_public"] = property.GetMethod?.IsPublic == true
            })
            .OrderBy(item => item["name"]?.ToString(), StringComparer.Ordinal)
            .ToArray();

        var fields = type.GetFields(flags)
            .Where(field => !field.IsStatic)
            .Select(field => new Dictionary<string, object?>
            {
                ["name"] = field.Name,
                ["type"] = field.FieldType.FullName,
                ["public"] = field.IsPublic,
                ["init_only"] = field.IsInitOnly
            })
            .OrderBy(item => item["name"]?.ToString(), StringComparer.Ordinal)
            .ToArray();

        WriteRecord(new Dictionary<string, object?>
        {
            ["type"] = "type_catalog",
            ["schema"] = BridgeWorkspace.ProbeSchema,
            ["sequence"] = Interlocked.Increment(ref _sequence),
            ["captured_at_utc"] = DateTimeOffset.UtcNow,
            ["runtime_type"] = typeName,
            ["base_type"] = type.BaseType?.FullName,
            ["properties"] = properties,
            ["fields"] = fields
        });
    }

    private static object? CaptureHistoryTail()
    {
        var manager = _combatManager;
        if (manager is null)
        {
            return null;
        }

        var history = GetProperty(manager, "History");
        var entries = history is null
            ? null
            : GetProperty(history, "Entries");
        var last = Enumerate(entries).LastOrDefault();

        return last is null
            ? null
            : ProjectSerializable(last, depth: 3);
    }

    private static object? FindNestedByTypeSuffix(
        object target,
        string typeSuffix)
    {
        var flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        foreach (var property in target.GetType().GetProperties(flags))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            try
            {
                var value = property.GetValue(target);
                if (value?.GetType().FullName?.EndsWith(
                        typeSuffix,
                        StringComparison.Ordinal) == true)
                {
                    return value;
                }
            }
            catch
            {
                // Discovery is read-only and best-effort.
            }
        }

        foreach (var field in target.GetType().GetFields(flags))
        {
            if (field.IsStatic)
            {
                continue;
            }

            try
            {
                var value = field.GetValue(target);
                if (value?.GetType().FullName?.EndsWith(
                        typeSuffix,
                        StringComparison.Ordinal) == true)
                {
                    return value;
                }
            }
            catch
            {
                // Discovery is read-only and best-effort.
            }
        }

        return null;
    }

    private static string NormalizeFieldName(string fieldName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            fieldName,
            "^<(?<name>.+)>k__BackingField$");
        var name = match.Success
            ? match.Groups["name"].Value
            : fieldName.TrimStart('_');

        return ToSnakeCase(name);
    }

    private static object? TryResolveSingleton(
        Assembly assembly,
        string typeName)
    {
        var type = assembly.GetType(typeName, throwOnError: false);
        return type?.GetProperty(
            "Instance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(null);
    }

    private static object? GetProperty(
        object target,
        string propertyName)
    {
        try
        {
            return target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(target);
        }
        catch
        {
            return null;
        }
    }

    private static object? TryInvokeNoArg(
        object target,
        string methodName)
    {
        try
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            return method?.Invoke(target, null);
        }
        catch
        {
            return null;
        }
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

            _combatManager = null;
        }
    }
}
