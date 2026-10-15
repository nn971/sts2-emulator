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

    public const string NatureBoundarySchemaId =
        "sts2-v111-nature-isolated-boundary-v1";
    public const string ControlledChanceSchemaId =
        ControlledPlayableCombatEngineV111.SchemaId;

    public static void Run(TextReader input, TextWriter output)
    {
        var environment = new PrototypeAiEnvironment();
        var states = new Dictionary<string, RunState>(StringComparer.Ordinal);
        // Opaque handle provenance is managed at the bridge boundary.
        // Generic reset handles are never eligible for draw-order injection.
        var hypotheticalHandles = new HashSet<string>(StringComparer.Ordinal);
        var freshCombatHandles = new HashSet<string>(StringComparer.Ordinal);
        long nextHandle = 1;
        // Experimental Nature handles are a separate namespace. They are NOT
        // playable RunState handles; only explicitly supported isolated entry
        // mechanics may be resumed. Existing Step() remains unaffected.
        var natureHp = new Dictionary<string, ControlledInitialHpStateV111>(
            StringComparer.Ordinal);
        var natureIntents = new Dictionary<string, ControlledIntentStateV111>(
            StringComparer.Ordinal);
        long nextNatureHandle = 1;
        var controlledStates = new Dictionary<string, ControlledPlayableCombatV111>(
            StringComparer.Ordinal);
        long nextControlledHandle = 1;

        string StoreControlled(ControlledPlayableCombatV111 state)
        {
            string handle = "c" + nextControlledHandle++;
            controlledStates.Add(handle, state.Fork());
            return handle;
        }

        ControlledPlayableCombatV111 RequireControlled(JsonElement request)
        {
            var handle = RequiredString(request, "controlled_handle");
            return controlledStates.TryGetValue(handle, out var value)
                ? value
                : throw new InvalidOperationException(
                    "Unknown controlled handle: " + handle);
        }

        string StoreNature(ControlledInitialHpStateV111 state)
        {
            string handle = "n" + nextNatureHandle++;
            natureHp.Add(handle, state.Fork());
            return handle;
        }

        string StoreNatureIntent(ControlledIntentStateV111 state)
        {
            string handle = "n" + nextNatureHandle++;
            natureIntents.Add(handle, state.Fork());
            return handle;
        }




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
                            natureBoundarySchemaId = NatureBoundarySchemaId,
                            natureBoundaryScope = "isolated-combat-entry-only",
                            natureEventMetadataSchemaId = NatureBoundaryMetadataV111.SchemaId,
                            naturePlayable = false,
                            controlledChanceSchemaId = ControlledChanceSchemaId,
                            controlledChanceScope = "isolated-playable-combat-stage-continuations-v1",
                            controlledChanceSupportedSites = new[]
                            {
                                ControlledPlayableCombatEngineV111.ShuffleSite,
                                ControlledTurnIntentEngineV111.SiteId,
                                ControlledRecycleShuffleEngineV111.SiteId,
                                "combat.effect.random_enemy",
                                "combat.sly_autoplay.random_enemy"
                            },
                            controlledChanceLimitations =
                                "Opt-in HP/intents, opening shuffle, next-turn intents, " +
                                "hand-draw recycle, random-enemy card effects and targeted " +
                                "Sly autoplay; other chance sites fail closed; native " +
                                "entry and conditional laws unverified.",
                            natureBoundaryKinds = new[] { "initial_hp", "intents" },
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

                    case "nature_begin":
                    {
                        var kind = RequiredString(request, "kind");
                        var seed = RequiredString(request, "seed");
                        int act = OptionalInt(request, "act") ?? 2;
                        var encounterId = RequiredString(request, "encounter_id");
                        var nativeEntry = RequiredString(request, "native_entry");
                        int floor = OptionalInt(request, "total_floor") ?? 20;
                        int ascension = OptionalInt(request, "ascension") ?? 0;
                        int column = OptionalInt(request, "map_column") ?? 3;
                        int row = OptionalInt(request, "map_row") ?? 5;
                        if (act is not (2 or 3))
                            throw new NotSupportedException(
                                "Nature v1 isolated formation fixture supports Hive/Glory only.");
                        if (kind == "initial_hp")
                        {
                            var pre = PrototypeNativeLaterActFixture.CreateCombatRngPreEntry(
                                seed, act, encounterId, nativeEntry, floor,
                                ascension, 1, column, row);
                            var state = ControlledInitialHpV111.Begin(
                                pre.Combat, pre.Formation, pre.NativeStreams);
                            var handle = StoreNature(state);
                            Write(new { requestId, ok = true,
                                schemaId = NatureBoundarySchemaId,
                                kind, natureHandle = handle,
                                fingerprint = state.Fingerprint,
                                pending = state.Pending, complete = state.Complete,
                                mode = state.Mode });
                        }
                        else if (kind == "intents")
                        {
                            var pre = PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
                                seed, act, encounterId, nativeEntry, floor,
                                ascension, 1, column, row,
                                commitInitialIntents: false);
                            var state = ControlledIntentsV111.Begin(pre);
                            var handle = StoreNatureIntent(state);
                            Write(new { requestId, ok = true,
                                schemaId = NatureBoundarySchemaId,
                                kind, natureHandle = handle,
                                fingerprint = state.Fingerprint,
                                pending = state.Pending, complete = state.Complete,
                                mode = state.Mode });
                        }
                        else throw new NotSupportedException(
                            "Unsupported Nature boundary kind.");
                        break;
                    }

                    case "nature_advance":
                    {
                        // Compose completed controlled HP outcomes into source
                        // intent selection without sampling native RNG again.
                        var handle = RequiredString(request, "nature_handle");
                        if (!natureHp.TryGetValue(handle, out var hp))
                            throw new InvalidOperationException(
                                "nature_advance requires an initial_hp handle.");
                        var next = ControlledIntentsV111.BeginAfterControlledHp(hp);
                        string childHandle = StoreNatureIntent(next);
                        Write(new { requestId, ok = true,
                            schemaId = NatureBoundarySchemaId,
                            kind = "intents", parent = handle,
                            child = childHandle, natureHandle = childHandle,
                            fingerprint = next.Fingerprint,
                            pending = next.Pending, complete = next.Complete,
                            mode = next.Mode });
                        break;
                    }

                    case "nature_resolve":
                    {
                        var handle = RequiredString(request, "nature_handle");
                        var fingerprint = RequiredString(request, "fingerprint");
                        int selectedIndex = OptionalInt(request, "selected_index") ??
                            throw new ArgumentException("Nature requires selected_index.");
                        if (natureHp.TryGetValue(handle, out var hp))
                        {
                            var child = ControlledInitialHpV111.Resolve(
                                hp, fingerprint, selectedIndex);
                            string childHandle = StoreNature(child);
                            Write(new { requestId, ok = true,
                                schemaId = NatureBoundarySchemaId,
                                kind = "initial_hp", parent = handle,
                                child = childHandle, natureHandle = childHandle,
                                fingerprint = child.Fingerprint,
                                pending = child.Pending, complete = child.Complete,
                                mode = child.Mode });
                        }
                        else if (natureIntents.TryGetValue(handle, out var intent))
                        {
                            var child = ControlledIntentsV111.Resolve(
                                intent, fingerprint, selectedIndex);
                            string childHandle = StoreNatureIntent(child);
                            Write(new { requestId, ok = true,
                                schemaId = NatureBoundarySchemaId,
                                kind = "intents", parent = handle,
                                child = childHandle, natureHandle = childHandle,
                                fingerprint = child.Fingerprint,
                                pending = child.Pending, complete = child.Complete,
                                mode = child.Mode });
                        }
                        else throw new InvalidOperationException(
                            "Unknown Nature handle: " + handle);
                        break;
                    }

                    case "nature_inspect":
                    {
                        // The chance controller may inspect support/ordered
                        // outcomes and replay provenance, not the player's
                        // hidden exact state. Ordinary fair observe remains
                        // exclusive to playable state handles.
                        var handle = RequiredString(request, "nature_handle");
                        NatureBoundaryViewV111 view;
                        if (natureHp.TryGetValue(handle, out var hp))
                            view = NatureBoundaryMetadataV111.Describe(hp);
                        else if (natureIntents.TryGetValue(handle, out var intents))
                            view = NatureBoundaryMetadataV111.Describe(intents);
                        else throw new InvalidOperationException(
                            "Unknown Nature handle: " + handle);
                        Write(new { requestId, ok = true,
                            schemaId = NatureBoundaryMetadataV111.SchemaId,
                            kind = view.Kind, mode = view.Mode,
                            natureHandle = handle, fingerprint = view.Fingerprint,
                            complete = view.Complete,
                            pending = view.Pending, transcript = view.Transcript });
                        break;
                    }

                    case "nature_fork":
                    {
                        var handle = RequiredString(request, "nature_handle");
                        if (natureHp.TryGetValue(handle, out var hp))
                        {
                            var child = hp.Fork();
                            string newHandle = StoreNature(child);
                            Write(new { requestId, ok = true,
                                kind = "initial_hp", parent = handle,
                                natureHandle = newHandle,
                                fingerprint = child.Fingerprint,
                                pending = child.Pending, complete = child.Complete });
                        }
                        else if (natureIntents.TryGetValue(handle, out var intent))
                        {
                            var child = ControlledIntentsV111.Restore(
                                ControlledIntentsV111.Save(intent));
                            string newHandle = StoreNatureIntent(child);
                            Write(new { requestId, ok = true,
                                kind = "intents", parent = handle,
                                natureHandle = newHandle,
                                fingerprint = child.Fingerprint,
                                pending = child.Pending, complete = child.Complete });
                        }
                        else throw new InvalidOperationException(
                            "Unknown Nature handle: " + handle);
                        break;
                    }

                    case "nature_release_many":
                    {
                        if (!request.TryGetProperty("nature_handles", out var elements) ||
                            elements.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "nature_release_many requires nature_handles array.");
                        // Validate the complete batch before removing any
                        // handle so malformed requests leave session intact.
                        var handles = elements.EnumerateArray().Select(element =>
                            element.ValueKind == JsonValueKind.String
                                ? element.GetString()
                                : throw new ArgumentException(
                                    "nature_handles entries must be strings")).ToArray();
                        if (handles.Any(string.IsNullOrWhiteSpace))
                            throw new ArgumentException("Empty Nature handle.");
                        int released = 0;
                        foreach (var handle in handles)
                        {
                            if (natureHp.Remove(handle!) || natureIntents.Remove(handle!))
                                released++;
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "nature_save":
                    {
                        var handle = RequiredString(request, "nature_handle");
                        if (natureHp.TryGetValue(handle, out var hp))
                            Write(new { requestId, ok = true,
                                schemaId = NatureBoundarySchemaId,
                                kind = "initial_hp",
                                snapshot = ControlledInitialHpV111.Save(hp) });
                        else if (natureIntents.TryGetValue(handle, out var intent))
                            Write(new { requestId, ok = true,
                                schemaId = NatureBoundarySchemaId,
                                kind = "intents",
                                snapshot = ControlledIntentsV111.Save(intent) });
                        else throw new InvalidOperationException(
                            "Unknown Nature handle: " + handle);
                        break;
                    }

                    case "nature_load":
                    {
                        var kind = RequiredString(request, "kind");
                        var snapshot = RequiredString(request, "snapshot");
                        if (kind == "initial_hp")
                        {
                            var state = ControlledInitialHpV111.Restore(snapshot);
                            string handle = StoreNature(state);
                            Write(new { requestId, ok = true,
                                schemaId = NatureBoundarySchemaId, kind,
                                natureHandle = handle,
                                fingerprint = state.Fingerprint,
                                pending = state.Pending, complete = state.Complete });
                        }
                        else if (kind == "intents")
                        {
                            var state = ControlledIntentsV111.Restore(snapshot);
                            string handle = StoreNatureIntent(state);
                            Write(new { requestId, ok = true,
                                schemaId = NatureBoundarySchemaId, kind,
                                natureHandle = handle,
                                fingerprint = state.Fingerprint,
                                pending = state.Pending, complete = state.Complete });
                        }
                        else throw new NotSupportedException(
                            "Unsupported Nature snapshot kind.");
                        break;
                    }

                    case "controlled_begin":
                    {
                        // Only a completed HP→intent chain may supply enemy
                        // state. The source provenance is checked before any
                        // playable state handle is published.
                        var sourceHandle = RequiredString(request, "nature_handle");
                        if (!natureIntents.TryGetValue(sourceHandle, out var intents))
                            throw new InvalidOperationException(
                                "controlled_begin requires an intent Nature handle.");
                        var state = ControlledPlayableCombatEngineV111.Begin(
                            intents, RequiredString(request, "seed"));
                        var handle = StoreControlled(state);
                        var view = NatureBoundaryMetadataV111.Describe(state);
                        Write(new { requestId, ok = true,
                            schemaId = ControlledChanceSchemaId,
                            controlledHandle = handle, fingerprint = state.Fingerprint,
                            parent = sourceHandle, phase = "opening_shuffle",
                            pending = view.Pending, transcript = view.Transcript,
                            terminal = false });
                        break;
                    }

                    case "controlled_resolve":
                    {
                        var parentHandle = RequiredString(request, "controlled_handle");
                        var parent = RequireControlled(request);
                        int selectedIndex = OptionalInt(request, "selected_index")
                            ?? throw new ArgumentException("Missing selected_index.");
                        var next = ControlledPlayableCombatEngineV111.Resolve(
                            parent, RequiredString(request, "fingerprint"), selectedIndex);
                        var handle = StoreControlled(next);
                        var view = NatureBoundaryMetadataV111.Describe(next);
                        Write(new { requestId, ok = true,
                            schemaId = ControlledChanceSchemaId,
                            parent = parentHandle, controlledHandle = handle,
                            fingerprint = next.Fingerprint,
                            phase = view.Kind, pending = view.Pending,
                            transcript = view.Transcript,
                            terminal = next.Run.Phase == RunPhase.Terminal });
                        break;
                    }

                    case "controlled_trace":
                    {
                        var current = RequireControlled(request);
                        var view = NatureBoundaryMetadataV111.Describe(current);
                        Write(new { requestId, ok = true,
                            schemaId = view.SchemaId, kind = view.Kind,
                            fingerprint = view.Fingerprint,
                            pending = view.Pending, transcript = view.Transcript,
                            playerActionIds = current.PlayerActions,
                            complete = view.Complete });
                        break;
                    }

                    case "controlled_observe":
                    {
                        var current = RequireControlled(request);
                        var policy = RequiredString(request, "policy_id");
                        if (policy != FairPolicyId)
                            throw new NotSupportedException(
                                "Controlled combat uses the existing fair policy.");
                        if (!current.AtPlayerBoundary)
                            throw new InvalidOperationException(
                                "Nature shuffle is pending; player observation unavailable.");
                        var frame = environment.Observe(current.Run);
                        Write(new { requestId, ok = true,
                            policyId = policy, schemaId = frame.SchemaId,
                            payloadJson = CanonicalJson.Serialize(frame.Observation),
                            observationHash = frame.ObservationHash,
                            actions = frame.LegalActions.Select(ActionWire).ToArray(),
                            terminal = current.Run.Phase == RunPhase.Terminal });
                        break;
                    }

                    case "controlled_legal_actions":
                    {
                        var current = RequireControlled(request);
                        if (!current.AtPlayerBoundary)
                            throw new InvalidOperationException(
                                "Nature shuffle is pending; player choices unavailable.");
                        var frame = environment.Observe(current.Run);
                        Write(new { requestId, ok = true,
                            actions = frame.LegalActions.Select(ActionWire).ToArray() });
                        break;
                    }

                    case "controlled_step":
                    {
                        var parentHandle = RequiredString(request, "controlled_handle");
                        var current = RequireControlled(request);
                        var next = ControlledPlayableCombatEngineV111.Step(
                            current, RequiredString(request, "action_id"));
                        var handle = StoreControlled(next);
                        var boundary = NatureBoundaryMetadataV111.Describe(next);
                        Write(new { requestId, ok = true,
                            schemaId = ControlledChanceSchemaId,
                            parent = parentHandle, controlledHandle = handle,
                            fingerprint = next.Fingerprint,
                            phase = boundary.Kind, pending = boundary.Pending,
                            transcript = boundary.Transcript,
                            terminal = next.Run.Phase == RunPhase.Terminal });
                        break;
                    }

                    case "controlled_fork":
                    {
                        var parentHandle = RequiredString(request, "controlled_handle");
                        var current = RequireControlled(request);
                        var next = current.Fork();
                        var handle = StoreControlled(next);
                        Write(new { requestId, ok = true, parent = parentHandle,
                            controlledHandle = handle, fingerprint = next.Fingerprint });
                        break;
                    }

                    case "controlled_save":
                    {
                        var current = RequireControlled(request);
                        Write(new { requestId, ok = true,
                            schemaId = ControlledChanceSchemaId,
                            snapshot = ControlledPlayableCombatEngineV111.Save(current) });
                        break;
                    }

                    case "controlled_load":
                    {
                        var next = ControlledPlayableCombatEngineV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreControlled(next);
                        var view = NatureBoundaryMetadataV111.Describe(next);
                        Write(new { requestId, ok = true,
                            schemaId = ControlledChanceSchemaId,
                            controlledHandle = handle, fingerprint = next.Fingerprint,
                            phase = view.Kind, pending = view.Pending,
                            terminal = next.Run.Phase == RunPhase.Terminal });
                        break;
                    }

                    case "controlled_release_many":
                    {
                        if (!request.TryGetProperty("controlled_handles", out var elements) ||
                            elements.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException("controlled_handles must be an array.");
                        var handles = elements.EnumerateArray().Select(element =>
                            element.ValueKind == JsonValueKind.String
                                ? element.GetString()
                                : throw new ArgumentException("Invalid controlled handle")).ToArray();
                        if (handles.Any(string.IsNullOrWhiteSpace))
                            throw new ArgumentException("Empty controlled handle.");
                        int released = 0;
                        foreach (var handle in handles)
                            if (controlledStates.Remove(handle!))
                                released++;
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reset_v111":
                    {
                        var region = OptionalString(request, "first_act") ?? "Overgrowth";
                        var character = OptionalString(request, "character") ?? "silent";
                        var configuration = RunConfiguration.Create(character,
                            OptionalInt(request, "ascension") ?? 0,
                            Enum.Parse<ActIdentity>(region, ignoreCase: true));
                        var requestedFidelity = OptionalString(request, "fidelity_id");
                        if (requestedFidelity is not null)
                            configuration = configuration with
                            {
                                FidelityId = requestedFidelity
                            };
                        configuration.Validate();
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
