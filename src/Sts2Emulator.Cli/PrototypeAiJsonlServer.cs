using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Cli;

internal static class PrototypeAiJsonlServer
{
    public const string WireSchemaId = "prototype-ai-jsonl-v0";
    public const string FairPolicyId = "prototype-fair-v0";
    public const string NativeOvergrowthResetId =
        "prototype-native-overgrowth-reset-v1";
    // A semantic capability: v1 guarantees committed, non-rerolling
    // public enemy intents with optional per-hit base/current damage.
    public const string PublicEnemyIntentId =
        "prototype-committed-public-enemy-intents-v1";

    public static void Run(TextReader input, TextWriter output)
    {
        var environment = new PrototypeAiEnvironment();
        var states = new Dictionary<string, RunState>(StringComparer.Ordinal);
        // Opaque handle provenance is managed at the bridge boundary.
        // Generic reset handles are never eligible for draw-order injection.
        var hypotheticalHandles = new HashSet<string>(StringComparer.Ordinal);
        var freshCombatHandles = new HashSet<string>(StringComparer.Ordinal);
        long nextHandle = 1;

        string Store(
            RunState state,
            string? parentHandle = null,
            bool rootHypothetical = false)
        {
            var handle = $"s{nextHandle++}";
            if (rootHypothetical
                || (parentHandle is not null && hypotheticalHandles.Contains(parentHandle)))
            {
                hypotheticalHandles.Add(handle);
            }
            if (parentHandle is not null)
            {
                var parent = states[parentHandle];
                if (state.Phase == RunPhase.Combat
                    && (parent.Phase != RunPhase.Combat
                        || (freshCombatHandles.Contains(parentHandle)
                            && parent.DecisionIndex == state.DecisionIndex)))
                {
                    freshCombatHandles.Add(handle);
                }
            }
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
                            fairPolicyId = FairPolicyId,
                            hypotheticalDrawOrderId = PrototypeHypotheticalDrawOrder.SchemaId,
                            conditionalCombatEntryId = PrototypeConditionedCombatEntry.SchemaId,
                            factorizedInitialStreamsId = PrototypeFactorizedRunFactory.SchemaId,
                            pristineRewardProposalId = PrototypePristineRewardProposal.SchemaId,
                            nativeOvergrowthResetId = NativeOvergrowthResetId,
                            publicEnemyIntentId = PublicEnemyIntentId,
                            coverageSchemaId = V111Coverage.SchemaId,
                            snapshotSchemaId = RunSnapshot.SchemaId,
                            batchSchemaId = DeterministicBatch.SchemaId,
                            v111StateSchemaId = V111Build.StateSchema,
                            v111FidelityId = "v111-mechanics-prototype-rng-v1"
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

                    case "v111_coverage":
                        Write(new { requestId, ok = true, coverage = V111Coverage.Create() });
                        break;

                    case "reset_v111":
                    {
                        var region = OptionalString(request, "first_act") ?? "Overgrowth";
                        var character = OptionalString(request, "character") ?? "silent";
                        var configuration = RunConfiguration.Create(character,
                            OptionalInt(request, "ascension") ?? 0,
                            Enum.Parse<ActIdentity>(region, ignoreCase: true));
                        var state = V111RunFactory.Create(RequiredString(request, "seed"), configuration);
                        var handle = Store(state);
                        Write(new { requestId, ok = true, stateHandle = handle,
                            stateSchemaId = V111Build.StateSchema, configuration.FidelityId,
                            exactHash = CanonicalJson.Sha256(state) });
                        break;
                    }

                    case "save_snapshot":
                        Write(new { requestId, ok = true, schemaId = RunSnapshot.SchemaId,
                            snapshot = RunSnapshot.Save(RequireState(request)) });
                        break;

                    case "load_snapshot":
                    {
                        var state = RunSnapshot.Load(RequiredString(request, "snapshot"));
                        var handle = Store(state);
                        Write(new { requestId, ok = true, stateHandle = handle,
                            exactHash = CanonicalJson.Sha256(state) });
                        break;
                    }

                    case "step_batch":
                    {
                        if (!request.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException("step_batch requires a steps array.");
                        var parents = steps.EnumerateArray().Select(step => new
                        {
                            Handle = RequiredString(step, "state_handle"),
                            State = RequireState(step), ActionId = RequiredString(step, "action_id")
                        }).ToArray();
                        // Resolve every action before storing any children. Errors
                        // leave the session unchanged and results preserve order.
                        var transitions = DeterministicBatch.Step(parents.Select(parent => new BatchStepRequest(
                            parent.State, new PrototypeGameEngine().GetLegalActions(parent.State)
                                .Single(action => PrototypeAiEnvironment.StableActionId(action) == parent.ActionId))).ToArray(),
                            OptionalInt(request, "parallelism") ?? 1);
                        var children = transitions.Select((transition, index) => new
                        {
                            parent = parents[index].Handle,
                            child = Store(transition.State, parents[index].Handle),
                            exactHash = CanonicalJson.Sha256(transition.State)
                        }).ToArray();
                        Write(new { requestId, ok = true, schemaId = DeterministicBatch.SchemaId, children });
                        break;
                    }

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

                    case "reset_native_overgrowth":
                    {
                        // A genuine live-run root, never a hypothetical search
                        // state. Use the factory itself so Neow, map layout,
                        // encounter pools and reward odds are initialized in
                        // exactly the same path as native-structure CLI runs.
                        var seed = RequiredString(request, "seed");
                        var ascension = OptionalInt(request, "ascension") ?? 0;
                        var state = PrototypeNativeOvergrowthRunFactory.Create(
                            seed, ascension);
                        var handle = Store(state);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            stateHandle = handle,
                            schemaId = NativeOvergrowthResetId
                        });
                        break;
                    }

                    case "reset_factorized_hypothetical":
                    {
                        if (!request.TryGetProperty("initial_streams", out var streams)
                            || streams.ValueKind != JsonValueKind.Object)
                        {
                            throw new ArgumentException(
                                "initial_streams must contain six named hexadecimal states.");
                        }

                        var initialStreams = new Dictionary<string, string>(
                            StringComparer.Ordinal);
                        foreach (var property in streams.EnumerateObject())
                        {
                            if (property.Value.ValueKind != JsonValueKind.String
                                || !initialStreams.TryAdd(
                                    property.Name, property.Value.GetString()!))
                            {
                                throw new ArgumentException(
                                    "Invalid or duplicated factorized RNG stream field.");
                            }
                        }

                        var ascension = OptionalInt(request, "ascension") ?? 0;
                        var state = PrototypeFactorizedRunFactory.Create(
                            initialStreams, ascension);
                        var handle = Store(state, rootHypothetical: true);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            stateHandle = handle,
                            hypothetical = true,
                            schemaId = PrototypeFactorizedRunFactory.SchemaId
                        });
                        break;
                    }

                    case "reset_hypothetical":
                    {
                        // Separate declared search-seed namespace from live resets.
                        // The caller MUST generate search_seed independently of
                        // any actual game's seed or internal RNG cursor.
                        var searchSeed = RequiredString(request, "search_seed");
                        if (searchSeed.Length != 32
                            || searchSeed.Any(ch => !Uri.IsHexDigit(ch)))
                        {
                            throw new ArgumentException(
                                "search_seed must be an independent 128-bit hex value.");
                        }
                        var ascension = OptionalInt(request, "ascension") ?? 0;
                        var state = environment.Reset(
                            $"fair-hypothetical:{searchSeed.ToLowerInvariant()}", ascension);
                        var handle = Store(state, rootHypothetical: true);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            stateHandle = handle,
                            terminal = state.Phase == RunPhase.Terminal,
                            hypothetical = true
                        });
                        break;
                    }

                    case "hypothetical_draw_order":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var state = RequireState(request);
                        if (!hypotheticalHandles.Contains(parentHandle)
                            || !freshCombatHandles.Contains(parentHandle))
                        {
                            throw new InvalidOperationException(
                                "Draw-order injection requires a declared hypothetical "
                                + "fresh-combat handle; live, oracle and previously "
                                + "acted-on combat states are ineligible.");
                        }
                        if (!request.TryGetProperty("ordered_cards", out var cards)
                            || cards.ValueKind != JsonValueKind.Array)
                        {
                            throw new ArgumentException(
                                "ordered_cards must be an array of public card variants.");
                        }
                        var order = cards.EnumerateArray().Select(card =>
                        {
                            if (card.ValueKind != JsonValueKind.Object)
                            {
                                throw new ArgumentException(
                                    "Every ordered_cards entry must be an object.");
                            }
                            return new PrototypePublicDrawCard(
                                RequiredString(card, "card_id"),
                                OptionalInt(card, "upgrade_level")
                                ?? throw new ArgumentException(
                                    "Every ordered card requires upgrade_level."));
                        }).ToArray();
                        var branch = PrototypeHypotheticalDrawOrder.Apply(state, order);
                        var child = Store(branch, parentHandle);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            child,
                            schemaId = PrototypeHypotheticalDrawOrder.SchemaId,
                            // No exact-state hash or hidden card instances in
                            // this operation's response.
                            policyId = FairPolicyId,
                            observationHash = environment.Observe(branch).ObservationHash
                        });
                        break;
                    }

                    case "condition_combat_entry":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var state = RequireState(request);
                        if (!hypotheticalHandles.Contains(parentHandle))
                        {
                            throw new InvalidOperationException(
                                "Only independent hypothetical source handles "
                                + "may condition a combat-entry stream.");
                        }

                        var actionId = RequiredString(request, "action_id");
                        var publicObservationHash = RequiredString(
                            request, "expected_observation_hash");
                        var searchSeed = RequiredString(request, "search_seed");
                        var budget = OptionalInt(request, "max_candidates") ?? 256;
                        if (!request.TryGetProperty(
                                "expected_legal_action_ids", out var legalIds)
                            || legalIds.ValueKind != JsonValueKind.Array)
                        {
                            throw new ArgumentException(
                                "Expected public legal action IDs must be an array.");
                        }
                        var expectedActions = legalIds.EnumerateArray().Select(item =>
                        {
                            if (item.ValueKind != JsonValueKind.String)
                            {
                                throw new ArgumentException(
                                    "Every expected legal action ID must be a string.");
                            }
                            return item.GetString()!;
                        }).ToArray();

                        var conditioned = PrototypeConditionedCombatEntry.Sample(
                            state,
                            actionId,
                            publicObservationHash,
                            expectedActions,
                            searchSeed,
                            budget);
                        var child = Store(conditioned.State, parentHandle);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            child,
                            schemaId = PrototypeConditionedCombatEntry.SchemaId,
                            policyId = FairPolicyId,
                            observationHash = publicObservationHash,
                            candidates = conditioned.Candidates
                        });
                        break;
                    }

                    case "propose_pristine_reward":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var state = RequireState(request);
                        if (!hypotheticalHandles.Contains(parentHandle))
                        {
                            throw new InvalidOperationException(
                                "Only independently created hypothetical handles "
                                + "may branch the pristine reward stream.");
                        }

                        var actionId = RequiredString(request, "action_id");
                        var initialHex = RequiredString(request, "reward_initial_state_hex");
                        var next = PrototypePristineRewardProposal.Branch(
                            state, actionId, initialHex);
                        var child = Store(next, parentHandle);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            child,
                            schemaId = PrototypePristineRewardProposal.SchemaId,
                            policyId = FairPolicyId
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
                        var childHandle = Store(next, parentHandle);
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
                                var childHandle = Store(expansion.State, parentHandle);
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
                                var childHandle = Store(next, parentHandle);
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
                                var childHandle = Store(stepped.State, parentHandle);
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

                    case "batch_rollout_step_frame":
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
                                        "Every batch_rollout_step_frame item must be an object.");
                                }

                                var parentHandle = RequiredString(item, "state_handle");
                                var actionId = RequiredString(item, "action_id");
                                var state = states.TryGetValue(parentHandle, out var found)
                                    ? found
                                    : throw new InvalidOperationException(
                                        $"Unknown state handle '{parentHandle}'.");

                                var stepped = environment.RolloutStepFrame(state, actionId);
                                var childHandle = Store(stepped.State, parentHandle);
                                return new
                                {
                                    parent = parentHandle,
                                    child = childHandle,
                                    terminal = stepped.State.Phase == RunPhase.Terminal,
                                    policyId,
                                    schemaId = PrototypeAiEnvironment.SchemaId,
                                    payloadJson = CanonicalJson.Serialize(stepped.Observation),
                                    legalActions = stepped.LegalActions.Select(ActionWire).ToArray()
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
                                    var childHandle = Store(expansion.State, parentHandle);
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
                                hypotheticalHandles.Remove(handle);
                                freshCombatHandles.Remove(handle);
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
                        var parentHandle = RequiredString(request, "state_handle");
                        var state = RequireState(request);
                        var child = Store(environment.Fork(state), parentHandle);
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
