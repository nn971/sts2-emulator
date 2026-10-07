using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Cli;

internal static class PrototypeAiJsonlServer
{
    public const string WireSchemaId = "prototype-ai-jsonl-v0";
    public const string FairPolicyId = "prototype-fair-v0";

    public static void Run(TextReader input, TextWriter output)
    {
        var environment = new PrototypeAiEnvironment();
        var states = new Dictionary<string, RunState>(StringComparer.Ordinal);
        long nextHandle = 1;

        string Store(RunState state)
        {
            var handle = $"s{nextHandle++}";
            states.Add(handle, state);
            return handle;
        }

        RunState RequireState(JsonElement request)
        {
            var handle = RequiredString(request, "state_handle");
            return states.TryGetValue(handle, out var state)
                ? state
                : throw new InvalidOperationException($"Unknown state handle '{handle}'.");
        }

        void Write(object response)
        {
            output.WriteLine(JsonSerializer.Serialize(
                response,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            output.Flush();
        }

        string? line;
        while ((line = input.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string? requestId = null;
            try
            {
                using var document = JsonDocument.Parse(line);
                var request = document.RootElement;
                requestId = OptionalString(request, "request_id");
                var operation = RequiredString(request, "op");

                switch (operation)
                {
                    case "hello":
                        Write(new
                        {
                            requestId,
                            ok = true,
                            wireSchemaId = WireSchemaId,
                            aiSchemaId = PrototypeAiEnvironment.SchemaId,
                            rulesetId = PrototypeContent.RulesetId,
                            fairPolicyId = FairPolicyId
                        });
                        break;

                    case "manifest":
                        Write(new
                        {
                            requestId,
                            ok = true,
                            manifest = PrototypeCapabilities.Create()
                        });
                        break;

                    case "reset":
                    {
                        var seed = RequiredString(request, "seed");
                        var ascension =
                            OptionalInt(request, "ascension")
                            ?? 0;
                        var state = environment.Reset(
                            seed,
                            ascension);
                        var handle = Store(state);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            stateHandle = handle,
                            exactHash = CanonicalJson.Sha256(state),
                            terminal = state.Phase == RunPhase.Terminal
                        });
                        break;
                    }

                    case "legal_actions":
                    {
                        var state = RequireState(request);
                        var frame = environment.Observe(state);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            actions = frame.LegalActions.Select(ActionWire).ToArray()
                        });
                        break;
                    }

                    case "observe":
                    {
                        var state = RequireState(request);
                        var policyId = RequiredString(request, "policy_id");
                        if (!StringComparer.Ordinal.Equals(policyId, FairPolicyId))
                        {
                            throw new NotSupportedException(
                                $"Prototype JSONL bridge supports only information policy '{FairPolicyId}'.");
                        }

                        var frame = environment.Observe(state);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            policyId,
                            payloadJson = CanonicalJson.Serialize(frame.Observation),
                            observationHash = frame.ObservationHash,
                            schemaId = frame.SchemaId
                        });
                        break;
                    }

                    case "step":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var state = states.TryGetValue(parentHandle, out var found)
                            ? found
                            : throw new InvalidOperationException(
                                $"Unknown state handle '{parentHandle}'.");
                        var actionId = RequiredString(request, "action_id");
                        var frame = environment.Observe(state);
                        var action = frame.LegalActions.SingleOrDefault(candidate =>
                            StringComparer.Ordinal.Equals(candidate.ActionId, actionId))
                            ?? throw new InvalidOperationException(
                                $"Action '{actionId}' is not legal in state '{parentHandle}'.");

                        var next = environment.Step(state, actionId).State;
                        var childHandle = Store(next);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            parent = parentHandle,
                            action = ActionWire(action),
                            child = childHandle,
                            terminal = next.Phase == RunPhase.Terminal,
                            exactHash = CanonicalJson.Sha256(next)
                        });
                        break;
                    }

                    case "expand":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var state = states.TryGetValue(parentHandle, out var found)
                            ? found
                            : throw new InvalidOperationException(
                                $"Unknown state handle '{parentHandle}'.");

                        var expansions = environment.Expand(state)
                            .Select(expansion =>
                            {
                                var childHandle = Store(expansion.State);
                                return new
                                {
                                    parent = parentHandle,
                                    action = ActionWire(expansion.Action),
                                    child = childHandle,
                                    terminal = expansion.State.Phase == RunPhase.Terminal,
                                    exactHash = expansion.CanonicalStateHash
                                };
                            })
                            .ToArray();

                        Write(new
                        {
                            requestId,
                            ok = true,
                            expansions
                        });
                        break;
                    }

                    case "batch_step":
                    {
                        if (!request.TryGetProperty("items", out var itemsElement)
                            || itemsElement.ValueKind != JsonValueKind.Array)
                        {
                            throw new InvalidOperationException(
                                "Request field 'items' must be an array.");
                        }

                        var transitions = itemsElement.EnumerateArray()
                            .Select(item =>
                            {
                                if (item.ValueKind != JsonValueKind.Object)
                                {
                                    throw new InvalidOperationException(
                                        "Every batch_step item must be an object.");
                                }

                                var parentHandle = RequiredString(item, "state_handle");
                                var actionId = RequiredString(item, "action_id");
                                var state = states.TryGetValue(parentHandle, out var found)
                                    ? found
                                    : throw new InvalidOperationException(
                                        $"Unknown state handle '{parentHandle}'.");

                                var frame = environment.Observe(state);
                                var action = frame.LegalActions.SingleOrDefault(candidate =>
                                    StringComparer.Ordinal.Equals(candidate.ActionId, actionId))
                                    ?? throw new InvalidOperationException(
                                        $"Action '{actionId}' is not legal in state '{parentHandle}'.");

                                var next = environment.Step(state, actionId).State;
                                var childHandle = Store(next);
                                return new
                                {
                                    parent = parentHandle,
                                    action = ActionWire(action),
                                    child = childHandle,
                                    terminal = next.Phase == RunPhase.Terminal,
                                    exactHash = CanonicalJson.Sha256(next)
                                };
                            })
                            .ToArray();

                        Write(new
                        {
                            requestId,
                            ok = true,
                            transitions
                        });
                        break;
                    }

                    case "batch_step_frame":
                    {
                        var policyId = RequiredString(request, "policy_id");
                        if (!StringComparer.Ordinal.Equals(policyId, FairPolicyId))
                        {
                            throw new NotSupportedException(
                                $"Prototype JSONL bridge supports only information policy '{FairPolicyId}'.");
                        }

                        if (!request.TryGetProperty("items", out var itemsElement)
                            || itemsElement.ValueKind != JsonValueKind.Array)
                        {
                            throw new InvalidOperationException(
                                "Request field 'items' must be an array.");
                        }

                        var frames = itemsElement.EnumerateArray()
                            .Select(item =>
                            {
                                if (item.ValueKind != JsonValueKind.Object)
                                {
                                    throw new InvalidOperationException(
                                        "Every batch_step_frame item must be an object.");
                                }

                                var parentHandle = RequiredString(item, "state_handle");
                                var actionId = RequiredString(item, "action_id");
                                var state = states.TryGetValue(parentHandle, out var found)
                                    ? found
                                    : throw new InvalidOperationException(
                                        $"Unknown state handle '{parentHandle}'.");

                                var stepped = environment.StepFrame(state, actionId);
                                var childHandle = Store(stepped.State);
                                var frame = stepped.Frame;
                                return new
                                {
                                    parent = parentHandle,
                                    action = ActionWire(stepped.Action),
                                    child = childHandle,
                                    terminal = stepped.State.Phase == RunPhase.Terminal,
                                    exactHash = frame.CanonicalStateHash,
                                    policyId,
                                    payloadJson = CanonicalJson.Serialize(frame.Observation),
                                    observationHash = frame.ObservationHash,
                                    schemaId = frame.SchemaId,
                                    legalActions = frame.LegalActions.Select(ActionWire).ToArray()
                                };
                            })
                            .ToArray();

                        Write(new
                        {
                            requestId,
                            ok = true,
                            frames
                        });
                        break;
                    }

                    case "batch_observe":
                    {
                        var policyId = RequiredString(request, "policy_id");
                        if (!StringComparer.Ordinal.Equals(policyId, FairPolicyId))
                        {
                            throw new NotSupportedException(
                                $"Prototype JSONL bridge supports only information policy '{FairPolicyId}'.");
                        }

                        if (!request.TryGetProperty("state_handles", out var handlesElement)
                            || handlesElement.ValueKind != JsonValueKind.Array)
                        {
                            throw new InvalidOperationException(
                                "Request field 'state_handles' must be an array.");
                        }

                        var observations = handlesElement.EnumerateArray()
                            .Select(element =>
                            {
                                if (element.ValueKind != JsonValueKind.String)
                                {
                                    throw new InvalidOperationException(
                                        "Every state_handles entry must be a string.");
                                }

                                var handle = element.GetString()
                                    ?? throw new InvalidOperationException(
                                        "state_handles entry is null.");
                                var state = states.TryGetValue(handle, out var found)
                                    ? found
                                    : throw new InvalidOperationException(
                                        $"Unknown state handle '{handle}'.");
                                var frame = environment.Observe(state);
                                return new
                                {
                                    stateHandle = handle,
                                    policyId,
                                    payloadJson = CanonicalJson.Serialize(frame.Observation),
                                    observationHash = frame.ObservationHash,
                                    schemaId = frame.SchemaId
                                };
                            })
                            .ToArray();

                        Write(new
                        {
                            requestId,
                            ok = true,
                            observations
                        });
                        break;
                    }

                    case "batch_expand":
                    {
                        if (!request.TryGetProperty("state_handles", out var handlesElement)
                            || handlesElement.ValueKind != JsonValueKind.Array)
                        {
                            throw new InvalidOperationException(
                                "Request field 'state_handles' must be an array.");
                        }

                        var handles = handlesElement.EnumerateArray()
                            .Select(element =>
                                element.ValueKind == JsonValueKind.String
                                    ? element.GetString()
                                    : throw new InvalidOperationException(
                                        "Every state_handles entry must be a string."))
                            .Select(handle => handle
                                ?? throw new InvalidOperationException(
                                    "state_handles entry is null."))
                            .ToArray();

                        var batches = handles.Select(parentHandle =>
                        {
                            var state = states.TryGetValue(parentHandle, out var found)
                                ? found
                                : throw new InvalidOperationException(
                                    $"Unknown state handle '{parentHandle}'.");

                            var expansions = environment.Expand(state)
                                .Select(expansion =>
                                {
                                    var childHandle = Store(expansion.State);
                                    return new
                                    {
                                        parent = parentHandle,
                                        action = ActionWire(expansion.Action),
                                        child = childHandle,
                                        terminal = expansion.State.Phase == RunPhase.Terminal,
                                        exactHash = expansion.CanonicalStateHash
                                    };
                                })
                                .ToArray();

                            return new
                            {
                                parent = parentHandle,
                                expansions
                            };
                        }).ToArray();

                        Write(new
                        {
                            requestId,
                            ok = true,
                            batches
                        });
                        break;
                    }

                    case "release_many":
                    {
                        if (!request.TryGetProperty("state_handles", out var handlesElement)
                            || handlesElement.ValueKind != JsonValueKind.Array)
                        {
                            throw new InvalidOperationException(
                                "Request field 'state_handles' must be an array.");
                        }

                        var released = 0;
                        foreach (var element in handlesElement.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String)
                            {
                                throw new InvalidOperationException(
                                    "Every state_handles entry must be a string.");
                            }

                            var handle = element.GetString()
                                ?? throw new InvalidOperationException(
                                    "state_handles entry is null.");
                            if (states.Remove(handle))
                            {
                                released++;
                            }
                        }

                        Write(new
                        {
                            requestId,
                            ok = true,
                            released
                        });
                        break;
                    }

                    case "fork":
                    {
                        var state = RequireState(request);
                        var child = Store(environment.Fork(state));
                        Write(new
                        {
                            requestId,
                            ok = true,
                            child,
                            exactHash = CanonicalJson.Sha256(states[child])
                        });
                        break;
                    }

                    case "exact_hash":
                    {
                        var state = RequireState(request);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            exactHash = CanonicalJson.Sha256(state)
                        });
                        break;
                    }

                    case "is_terminal":
                    {
                        var state = RequireState(request);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            terminal = state.Phase == RunPhase.Terminal
                        });
                        break;
                    }

                    case "close":
                        Write(new { requestId, ok = true });
                        return;

                    default:
                        throw new InvalidOperationException($"Unknown JSONL operation '{operation}'.");
                }
            }
            catch (Exception exception)
            {
                Write(new
                {
                    requestId,
                    ok = false,
                    errorType = exception.GetType().Name,
                    error = exception.Message
                });
            }
        }
    }

    private static object ActionWire(PrototypeAiAction action) =>
        new
        {
            actionId = action.ActionId,
            kind = action.Kind,
            payloadJson = CanonicalJson.Serialize(action.Payload)
        };

    private static string RequiredString(JsonElement request, string name)
    {
        if (!request.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Request field '{name}' must be a string.");
        }

        return value.GetString()
            ?? throw new InvalidOperationException($"Request field '{name}' is null.");
    }

    private static int? OptionalInt(
        JsonElement request,
        string name)
    {
        if (!request.TryGetProperty(name, out var value)
            || value.ValueKind is
                JsonValueKind.Null
                or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var result))
        {
            throw new InvalidOperationException(
                $"Request field '{name}' must be an integer.");
        }

        return result;
    }

    private static string? OptionalString(JsonElement request, string name)
    {
        if (!request.TryGetProperty(name, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Request field '{name}' must be a string.");
        }

        return value.GetString();
    }
}
