using System.Text.Json;
using Sts2Emulator.Core;
using Sts2Emulator.Trace;

namespace Sts2ReferenceMod.Trace;

/// <summary>
/// Independent, non-intervening native action-executor timeline. This sidecar
/// retains callback and history evidence even when the strict decision trace
/// cannot identify a stable player input boundary. It does not claim hook
/// completeness or parity with the emulated effects.
/// </summary>
/// <summary>Card identity already assigned by native NetCombatCardDb.</summary>
public sealed record NativeHandMenuOption(
    long CombatCardId, string ModelId, int UiHandIndex);

public sealed class NativeActionEnvelopeRecorder : IAsyncDisposable
{
    public const string Schema = "native-action-envelope-v1";
    private readonly AsyncTraceWriter _writer;
    private readonly Dictionary<object, long> _running =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<long> _activeOrder = [];
    // The native executor may invoke BeforeActionExecuted again on the same
    // action instance after resuming a player-choice suspension.
    private readonly HashSet<object> _pendingResumption =
        new(ReferenceEqualityComparer.Instance);
    // RequestEnqueue can defer a request or cancel it. Only a later
    // ActionExecutor callback proves execution of the same action object.
    private readonly Dictionary<object, long> _requested =
        new(ReferenceEqualityComparer.Instance);
    private long _nextInputRequestId;
    private long _nextHandMenuId;
    private sealed class HandMenuLifetime
    {
        public required long MenuId { get; init; }
        public required string Mode { get; init; }
        public ulong? OwnerId { get; init; }
        public uint? ChoiceId { get; init; }
        public required string Binding { get; init; }
        public string LastSignature { get; set; } = "";
        public int Revision { get; set; } = 1;
    }
    private readonly Dictionary<object, HandMenuLifetime> _activeHandMenus =
        new(ReferenceEqualityComparer.Instance);
    private readonly string _gameBuild;
    private long _sequence;
    private long _nextActionId;
    private bool _incomplete;
    private bool _closed;
    private long _frameIndex;
    private string? _lastFrameStateHash;

    public NativeActionEnvelopeRecorder(
        string path, string gameBuild, string runId,
        int queueCapacity = 8192, bool nativeRngHookInstalled = false,
        bool nativeMonsterAiScopeInstalled = false,
        bool nativeShuffleScopeInstalled = false,
        bool nativeInitialHpScopeInstalled = false,
        bool nativeRngDoubleHookInstalled = false,
        bool nativeRngBoolHookInstalled = false,
        bool nativeRngStreamCheckpointsEnabled = false,
        bool nativeRngFloatHookInstalled = false,
        bool nativeInputHookInstalled = false,
        bool nativeHandMenuHookInstalled = false,
        bool nativeHandChoiceFlowHookInstalled = false,
        bool nativeManualPlayHookInstalled = false,
        bool nativeUiPlayBeginHookInstalled = false,
        bool nativeUiGateHookInstalled = false)
    {
        _gameBuild = gameBuild;
        _writer = new AsyncTraceWriter(path, queueCapacity);
        Write(new NativeActionEnvelopeRecord(
            Type: "header", Ordinal: Next(),
            Data: JsonSerializer.SerializeToElement(new
            {
                schema = Schema, game_build = gameBuild, run_id = runId,
                started_at_utc = DateTimeOffset.UtcNow,
                source = "native_action_executor_and_combat_history",
                coverage = "callback_order_and_observed_snapshots_only",
                native_rng_hook_installed = nativeRngHookInstalled,
                native_rng_double_hook_installed = nativeRngDoubleHookInstalled,
                native_rng_bool_hook_installed = nativeRngBoolHookInstalled,
                native_rng_float_hook_installed = nativeRngFloatHookInstalled,
                native_input_hook_installed = nativeInputHookInstalled,
                native_hand_menu_hook_installed = nativeHandMenuHookInstalled,
                native_hand_choice_flow_hook_installed =
                    nativeHandChoiceFlowHookInstalled,
                native_manual_play_hook_installed = nativeManualPlayHookInstalled,
                native_ui_play_begin_hook_installed = nativeUiPlayBeginHookInstalled,
                native_ui_gate_hook_installed = nativeUiGateHookInstalled,
                native_rng_stream_checkpoints_enabled = nativeRngStreamCheckpointsEnabled,
                native_monster_ai_scope_installed = nativeMonsterAiScopeInstalled,
                native_shuffle_scope_installed = nativeShuffleScopeInstalled,
                native_initial_hp_scope_installed = nativeInitialHpScopeInstalled,
                native_rng_capture_scope = nativeRngHookInstalled
                    ? "post_first_observed_combat_frame"
                    : "hook_disabled",
                rng_source_hook_complete = false,
                exact_rng_parity = "NOT_TESTED",
                native_hook_parity = "NOT_TESTED"
            })));
    }

    public bool IsIncomplete => _incomplete;

    /// <summary>
    /// At most one full snapshot per changed canonical state. Frames can
    /// observe UI-driven nested selections even when no executor action
    /// starts. Sampling never establishes sub-frame hook ordering.
    /// </summary>
    public void ObserveFrame(JsonElement state)
    {
        if (_closed) return;
        ++_frameIndex;
        string hash = CanonicalJson.Sha256(state);
        if (hash == _lastFrameStateHash) return;
        _lastFrameStateHash = hash;
        Write(new NativeActionEnvelopeRecord(
            "frame_state", Next(), State: Snapshot(state),
            Data: JsonSerializer.SerializeToElement(new
            {
                frame_index = _frameIndex,
                state_hash = hash,
                capture_basis = "rendered_frame_poll",
                semantic_hook_order_verified = false
            })));
    }

    public void HistoryBaseline(int existingEntryCount)
    {
        if (_closed) return;
        Write(new NativeActionEnvelopeRecord(
            "history_baseline", Next(),
            Data: JsonSerializer.SerializeToElement(new
            {
                existing_entry_count = existingEntryCount,
                note = "Entries before observer attachment have no recorded order"
            })));
    }

    public void ObservationFailure(string stage, string exceptionType)
    {
        if (_closed) return;
        _incomplete = true;
        Write(new NativeActionEnvelopeRecord(
            "observation_failure", Next(),
            Data: JsonSerializer.SerializeToElement(new
            {
                stage, exception_type = exceptionType
            })));
    }

    /// <summary>Actual native run identity, read from RunRngSet.</summary>
    public void NativeRunIdentity(string seed, int ascension)
    {
        if (_closed) return;
        if (string.IsNullOrWhiteSpace(seed) || ascension < 0)
        {
            ObservationFailure("native_run_identity", "INVALID_RUN_IDENTITY");
            return;
        }
        Write(new NativeActionEnvelopeRecord(
            "native_run_identity", Next(),
            Data: JsonSerializer.SerializeToElement(new
            {
                seed, ascension,
                source = "RunState.Rng.StringSeed",
                rng_draws_performed = 0,
                emulator_run_identity_parity = "NOT_TESTED"
            })));
    }

    /// <summary>
    /// Source method receives an action REQUEST before potential deferral,
    /// validation and cancellation. The object identity joins a later
    /// executor callback, if one ever occurs.
    /// </summary>
    public void NativeInputRequest(
        object action, string nativeType, JsonElement? state,
        GameAction? canonicalAction)
    {
        if (_closed) return;
        ArgumentNullException.ThrowIfNull(action);
        long id = ++_nextInputRequestId;
        _requested[action] = id;
        Write(new NativeActionEnvelopeRecord(
            "native_input_request", Next(), NativeType: nativeType,
            CanonicalAction: canonicalAction, State: Snapshot(state),
            Data: JsonSerializer.SerializeToElement(new
            {
                input_request_id = id,
                source = "ActionQueueSynchronizer.RequestEnqueue.prefix",
                native_acceptance = "UNVERIFIED",
                human_origin = "UNVERIFIED",
                input_injected = false
            })));
    }

    /// <summary>
    /// A native UI hand-selection menu, recorded after the original UI
    /// has already applied its own filter. This is the exact holder-visibility
    /// projection at that instant, not all legal game actions or proof that
    /// the same options remained valid until the user selected one.
    /// </summary>
    public void NativeHandMenu(
        string mode, string? sourceModelId,
        int minSelect, int maxSelect, bool requireConfirmation,
        int nativeHolderCount, int missingCardIds,
        IReadOnlyList<NativeHandMenuOption> visibleOptions,
        ulong? selectionOwnerId = null, uint? selectionChoiceId = null,
        string selectionIdBinding = "UNVERIFIED",
        object? nativeHand = null)
    {
        if (_closed) return;
        bool exact = selectionIdBinding == "SOURCE_ASYNC_FLOW_RESERVED_ID";
        if (mode is not ("SimpleSelect" or "UpgradeSelect")
            || minSelect < 0 || maxSelect < minSelect
            || nativeHolderCount < 0 || missingCardIds < 0
            || selectionIdBinding is not ("UNVERIFIED"
                or "SOURCE_ASYNC_FLOW_RESERVED_ID"
                or "CHOICE_ID_NOT_YET_OBSERVED"
                or "AMBIGUOUS_SOURCE_RESERVATION")
            || (exact && (!selectionOwnerId.HasValue
                          || !selectionChoiceId.HasValue))
            || (!exact && selectionChoiceId.HasValue)
            || visibleOptions.Count + missingCardIds > nativeHolderCount
            || visibleOptions.Any(x => x.CombatCardId < 0
                || string.IsNullOrWhiteSpace(x.ModelId)
                || x.UiHandIndex < 0 || x.UiHandIndex >= nativeHolderCount)
            || visibleOptions.Select(x => x.CombatCardId).Distinct().Count()
                != visibleOptions.Count)
        {
            ObservationFailure("native_hand_menu", "INVALID_NATIVE_UI_PROJECTION");
            return;
        }
        if (nativeHand is not null && _activeHandMenus.ContainsKey(nativeHand))
        {
            ObservationFailure("native_hand_menu", "OVERLAPPING_NATIVE_SELECTION");
            return;
        }
        long menuId = ++_nextHandMenuId;
        if (nativeHand is not null)
        {
            _activeHandMenus[nativeHand] = new HandMenuLifetime
            {
                MenuId = menuId,
                Mode = mode,
                OwnerId = selectionOwnerId,
                ChoiceId = selectionChoiceId,
                Binding = selectionIdBinding,
                LastSignature = MenuSignature(
                    nativeHolderCount, missingCardIds, visibleOptions, false)
            };
        }
        Write(new NativeActionEnvelopeRecord(
            "native_hand_menu", Next(),
            _activeOrder.Count == 0 ? null : _activeOrder[^1],
            Data: JsonSerializer.SerializeToElement(new
            {
                menu_id = menuId,
                mode,
                source_model_id = sourceModelId,
                min_select = minSelect,
                max_select = maxSelect,
                require_manual_confirmation = requireConfirmation,
                native_hand_holder_count = nativeHolderCount,
                missing_card_ids = missingCardIds,
                options = visibleOptions.Select(x => new
                {
                    combat_card_id = x.CombatCardId,
                    card_model_id = x.ModelId,
                    ui_hand_index = x.UiHandIndex
                }).ToArray(),
                coverage = missingCardIds == 0
                    ? "SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS"
                    : "INCOMPLETE_NATIVE_CARD_ID_MAPPING",
                selection_owner_id = selectionOwnerId,
                selection_choice_id = selectionChoiceId,
                selection_id_binding = selectionIdBinding,
                predicate_re_evaluated_by_inspector = false,
                source = "NPlayerHand.SelectCards.postfix_after_native_visibility_update",
                emulator_parity = "NOT_TESTED"
            })));
    }

    private static string MenuSignature(
        int holderCount, int missing, IReadOnlyList<NativeHandMenuOption> options,
        bool peeking) =>
        JsonSerializer.Serialize(new
        {
            holderCount, missing, peeking,
            options = options.Select(x => new
            {
                x.CombatCardId, x.ModelId, x.UiHandIndex
            })
        });

    /// <summary>
    /// The original NPlayerHand.UpdateSelectModeCardVisibility has already
    /// finished running all native predicates and setting holder.Visible.
    /// Only changed UI snapshots are emitted; the source menu's identity
    /// and choice ID are carried from its original SelectCards call.
    /// </summary>
    public void NativeHandMenuRefresh(
        object nativeHand, string mode, bool peeking,
        int holderCount, int missingCardIds,
        IReadOnlyList<NativeHandMenuOption> visibleOptions)
    {
        if (_closed || !_activeHandMenus.TryGetValue(
                nativeHand, out var lifetime))
            return;
        if (mode != lifetime.Mode || holderCount < 0 || missingCardIds < 0
            || (!peeking &&
                (visibleOptions.Count + missingCardIds > holderCount
                 || visibleOptions.Select(x => x.CombatCardId).Distinct().Count()
                    != visibleOptions.Count))
            || (peeking && (missingCardIds != 0 || visibleOptions.Count != 0))
            || visibleOptions.Any(x => x.CombatCardId < 0
                || string.IsNullOrWhiteSpace(x.ModelId)
                || x.UiHandIndex < 0 || x.UiHandIndex >= holderCount))
        {
            ObservationFailure("native_hand_menu_refresh", "INVALID_NATIVE_UI_REFRESH");
            return;
        }
        string signature = MenuSignature(
            holderCount, missingCardIds, visibleOptions, peeking);
        if (signature == lifetime.LastSignature) return;
        lifetime.LastSignature = signature;
        int revision = ++lifetime.Revision;
        Write(new NativeActionEnvelopeRecord(
            "native_hand_menu_revision", Next(),
            _activeOrder.Count == 0 ? null : _activeOrder[^1],
            Data: JsonSerializer.SerializeToElement(new
            {
                menu_id = lifetime.MenuId,
                revision,
                selection_owner_id = lifetime.OwnerId,
                selection_choice_id = lifetime.ChoiceId,
                selection_id_binding = lifetime.Binding,
                mode,
                peek_mode = peeking,
                native_hand_holder_count = holderCount,
                missing_card_ids = missingCardIds,
                options = visibleOptions.Select(x => new
                {
                    combat_card_id = x.CombatCardId,
                    card_model_id = x.ModelId,
                    ui_hand_index = x.UiHandIndex
                }).ToArray(),
                coverage = peeking ? "NATIVE_PEEK_VIEW_NOT_SELECTABLE"
                    : missingCardIds == 0
                        ? "SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS"
                        : "INCOMPLETE_NATIVE_CARD_ID_MAPPING",
                source = "NPlayerHand.UpdateSelectModeCardVisibility.postfix",
                predicate_re_evaluated_by_inspector = false,
                complete_menu_history = "NOT_VERIFIED",
                emulator_parity = "NOT_TESTED"
            })));
    }

    /// <summary>
    /// Record the native menu's end of UI lifetime. A selection may be
    /// completed, canceled, or torn down. The synchronized choice result
    /// remains a distinct event, usually observed after UI close.
    /// </summary>
    public void NativeHandMenuClosed(object nativeHand, string nativeReason)
    {
        if (_closed || !_activeHandMenus.Remove(nativeHand, out var lifetime))
            return;
        if (nativeReason is not ("AfterCardsSelected"
            or "NativeHandExitTree"))
        {
            ObservationFailure("native_hand_menu_closed",
                "INVALID_NATIVE_CLOSE_REASON");
            return;
        }
        Write(new NativeActionEnvelopeRecord(
            "native_hand_menu_closed", Next(),
            _activeOrder.Count == 0 ? null : _activeOrder[^1],
            Data: JsonSerializer.SerializeToElement(new
            {
                menu_id = lifetime.MenuId,
                final_revision = lifetime.Revision,
                selection_owner_id = lifetime.OwnerId,
                selection_choice_id = lifetime.ChoiceId,
                selection_id_binding = lifetime.Binding,
                native_reason = nativeReason,
                source = nativeReason == "AfterCardsSelected"
                    ? "NPlayerHand.AfterCardsSelected.postfix"
                    : "NPlayerHand._ExitTree.prefix",
                native_choice_result_verified = false,
                complete_menu_history = "NOT_VERIFIED"
            })));
    }

    /// <summary>
    /// Source NPlayerHand click/shortcut entry has already evaluated its
    /// private native gate. We capture the return value without calling a
    /// second eligibility check or projecting a full legal action menu.
    /// </summary>
    public void NativeUiHandGate(
        string sourceMethod, bool allowed, string nativeMode,
        bool existingCardPlay)
    {
        if (_closed) return;
        if (sourceMethod is not ("CanPlayCards" or "AreCardActionsAllowed")
            || nativeMode is not ("Play" or "None"
                or "SimpleSelect" or "UpgradeSelect"))
        {
            ObservationFailure("native_ui_hand_gate", "INVALID_NATIVE_UI_GATE");
            return;
        }
        Write(new NativeActionEnvelopeRecord(
            "native_ui_hand_gate", Next(),
            Data: JsonSerializer.SerializeToElement(new
            {
                gate = sourceMethod,
                returned_allowed = allowed,
                native_hand_mode = nativeMode,
                existing_card_play_active = existingCardPlay,
                source = "NPlayerHand." + sourceMethod + ".postfix",
                result_basis = "ORIGINAL_NATIVE_RETURN_VALUE",
                source_evaluation_only = true,
                human_origin = "UNVERIFIED",
                full_legal_menu = "NOT_TESTED",
                emulator_parity = "NOT_TESTED"
            })));
    }

    /// <summary>
    /// Native hand input was initiated from click or shortcut and passed
    /// NPlayerHand's existing UI guards. Drag/targeting may still cancel it.
    /// </summary>
    public void NativeUiPlayBegin(long? cardId, string modelId, bool shortcut,
        JsonElement? uiState = null)
    {
        if (_closed) return;
        if (string.IsNullOrWhiteSpace(modelId) || cardId is < 0)
        {
            ObservationFailure("native_ui_play_begin", "INVALID_NATIVE_CARD_ID");
            return;
        }
        Write(new NativeActionEnvelopeRecord(
            "native_ui_play_begin", Next(), State: Snapshot(uiState),
            Data: JsonSerializer.SerializeToElement(new
            {
                ui_state_observation = uiState.HasValue
                    ? "SOURCE_UI_BEGIN_CANONICAL_SNAPSHOT"
                    : "UNOBSERVED",
                combat_card_id = cardId,
                card_model_id = modelId,
                started_via_shortcut = shortcut,
                source = "NPlayerHand.StartCardPlay.prefix",
                native_ui_gate = "SOURCE_CALLSITE_PASSED",
                manual_play_accepted = "NOT_TESTED",
                queue_acceptance = "NOT_TESTED",
                human_origin = "UNVERIFIED",
                full_legal_menu = "NOT_TESTED",
                emulator_parity = "NOT_TESTED"
            })));
    }

    /// <summary>
    /// Result from the native manual play method, after its own
    /// CanPlayTargeting check and optional queue request. Do not treat a
    /// true result as permission from the native action queue.
    /// </summary>
    public void NativeManualPlayResult(long? combatCardId, string modelId,
        string? targetId, bool returnedSuccess)
    {
        if (_closed) return;
        if (string.IsNullOrWhiteSpace(modelId)
            || combatCardId is < 0)
        {
            ObservationFailure("native_manual_play_result", "INVALID_CARD_ID");
            return;
        }
        Write(new NativeActionEnvelopeRecord(
            "native_manual_play_result", Next(),
            Data: JsonSerializer.SerializeToElement(new
            {
                combat_card_id = combatCardId,
                card_model_id = modelId,
                target_id = targetId,
                returned_success = returnedSuccess,
                source = "CardModel.TryManualPlay.postfix",
                queue_acceptance = "UNVERIFIED",
                human_origin = "UNVERIFIED",
                emulator_parity = "NOT_TESTED"
            })));
    }

    /// <summary>
    /// A synchronized source-observed choice result; no menu is fabricated.
    /// Survivor's hand discard uses CombatCard and a combat-card index.
    /// Other choice categories remain distinguishable even if their full
    /// source payload has not been projected.
    /// </summary>
    /// <summary>
    /// Return-time action state from RequestEnqueue; a state of None can
    /// indicate an intentionally deferred input. A Canceled action may have
    /// already received an ID before it was rejected by queue policy.
    /// </summary>
    public void NativeInputOutcome(object action, string nativeState,
        uint? nativeActionId)
    {
        if (_closed) return;
        ArgumentNullException.ThrowIfNull(action);
        if (!_requested.TryGetValue(action, out long requestId))
        {
            ObservationFailure("native_input_request_outcome",
                "NO_MATCHING_SOURCE_REQUEST");
            return;
        }
        string result = nativeState switch
        {
            "None" when !nativeActionId.HasValue => "DEFERRED_OR_UNENQUEUED",
            "Canceled" => "CANCELED_AT_RETURN",
            "WaitingForExecution" or "Executing" or
                "GatheringPlayerChoice" or "ReadyToResumeExecuting" or
                "Finished" when nativeActionId.HasValue =>
                "ASSIGNED_QUEUE_ID_AND_NOT_CANCELED_AT_RETURN",
            _ => "STATE_UNCLASSIFIED"
        };
        Write(new NativeActionEnvelopeRecord(
            "native_input_outcome", Next(),
            NativeType: action.GetType().FullName ?? action.GetType().Name,
            Data: JsonSerializer.SerializeToElement(new
            {
                input_request_id = requestId,
                native_action_state = nativeState,
                native_action_id = nativeActionId,
                queue_result = result,
                source = "ActionQueueSynchronizer.RequestEnqueue.postfix",
                human_origin = "UNVERIFIED",
                legal_menu_verified = false
            })));
    }

    public void NativePlayerChoice(
        ulong ownerId, uint choiceId, string choiceType,
        long[]? combatCardIds, int[]? indexes,
        string[]? canonicalCardModels, ulong? selectedPlayerId)
    {
        if (_closed) return;
        if (string.IsNullOrWhiteSpace(choiceType))
        {
            ObservationFailure("native_player_choice", "MISSING_CHOICE_TYPE");
            return;
        }
        Write(new NativeActionEnvelopeRecord(
            "native_player_choice", Next(),
            _activeOrder.Count == 0 ? null : _activeOrder[^1],
            Data: JsonSerializer.SerializeToElement(new
            {
                owner_id = ownerId,
                choice_id = choiceId,
                choice_type = choiceType,
                selected_combat_card_ids = combatCardIds,
                selected_indexes = indexes,
                selected_canonical_card_models = canonicalCardModels,
                selected_player_id = selectedPlayerId,
                selection_projection = choiceType switch
                {
                    "CombatCard" => "EXACT_COMBAT_CARD_IDS",
                    "Index" => "EXACT_INDEXES",
                    "CanonicalCard" => "EXACT_CANONICAL_MODEL_IDS",
                    "Player" => "EXACT_PLAYER_ID",
                    _ => "UNPROJECTED_CHOICE_PAYLOAD"
                },
                origin = "PlayerChoiceSynchronizer.PlayerChoiceReceived",
                accepted_choice_menu = "NOT_CAPTURED",
                human_origin = "UNVERIFIED"
            })));
    }

    public void Start(
        object action, string type, JsonElement? state,
        GameAction? canonicalAction = null)
    {
        if (_closed) return;
        ArgumentNullException.ThrowIfNull(action);
        if (_running.TryGetValue(action, out long existingId))
        {
            if (_pendingResumption.Remove(action))
            {
                if (state is null)
                    ObservationFailure("action_resume_snapshot", "STATE_UNAVAILABLE");
                Write(new NativeActionEnvelopeRecord(
                    "action_resumed", Next(), existingId,
                    NativeType: type, State: Snapshot(state),
                    Data: Data("native_executor_before_action_after_choice", type)));
                return;
            }
            _incomplete = true;
            Write(new NativeActionEnvelopeRecord(
                "anomaly", Next(), existingId,
                NativeType: type,
                Data: Data("duplicate_action_start_without_choice", type)));
            return;
        }
        if (state is null) ObservationFailure("action_start_snapshot", "STATE_UNAVAILABLE");
        long id = ++_nextActionId;
        long? parent = _activeOrder.Count == 0 ? null : _activeOrder[^1];
        _running.Add(action, id);
        _activeOrder.Add(id);
        long? requestId = _requested.TryGetValue(action, out long requested)
            ? requested : null;
        Write(new NativeActionEnvelopeRecord(
            "action_started", Next(), id, parent, type,
            canonicalAction, Snapshot(state),
            requestId.HasValue
                ? JsonSerializer.SerializeToElement(new
                {
                    input_request_id = requestId,
                    request_execution_link = "SAME_NATIVE_ACTION_OBJECT",
                    human_origin = "UNVERIFIED"
                })
                : null));
    }

    public void Complete(object action, JsonElement? state)
    {
        if (_closed) return;
        ArgumentNullException.ThrowIfNull(action);
        if (!_running.Remove(action, out long id))
        {
            _incomplete = true;
            Write(new NativeActionEnvelopeRecord(
                "anomaly", Next(), Data: Data("orphan_action_complete",
                    action.GetType().FullName ?? action.GetType().Name),
                State: Snapshot(state)));
            return;
        }
        _pendingResumption.Remove(action);
        // Keep the request mapping until encounter end. An action may finish
        // synchronously inside RequestEnqueue before its postfix runs.
        if (state is null) ObservationFailure("action_complete_snapshot", "STATE_UNAVAILABLE");
        int index = _activeOrder.LastIndexOf(id);
        if (index >= 0) _activeOrder.RemoveAt(index);
        Write(new NativeActionEnvelopeRecord(
            "action_completed", Next(), id,
            NativeType: action.GetType().FullName ?? action.GetType().Name,
            State: Snapshot(state)));
    }

    public void ChoicePause(object action, JsonElement? state)
    {
        if (_closed) return;
        long? actionId = _running.TryGetValue(action, out long id) ? id : null;
        if (actionId.HasValue) _pendingResumption.Add(action);
        else _incomplete = true;
        Write(new NativeActionEnvelopeRecord(
            "nested_choice_pause", Next(), actionId,
            NativeType: action.GetType().FullName ?? action.GetType().Name,
            State: Snapshot(state),
            Data: Data("choice_kind_and_selected_option_not_yet_audited",
                "passive observation")));
    }

    /// <summary>
    /// A canonical executor action began without a preceding unique strict
    /// decision. The sidecar remains usable: it already has the actual
    /// executor-start snapshot and action identity. No legal menu or human
    /// input origin is inferred from this diagnostic.
    /// </summary>
    public void StrictInputGap(
        string? lastGateReason, int decisionGeneration,
        int lastCapturedGeneration)
    {
        if (_closed) return;
        Write(new NativeActionEnvelopeRecord(
            "strict_input_gap", Next(),
            _activeOrder.Count == 0 ? null : _activeOrder[^1],
            Data: JsonSerializer.SerializeToElement(new
            {
                reason = "executor_action_without_unique_strict_decision",
                last_reported_gate = lastGateReason,
                decision_generation = decisionGeneration,
                last_captured_generation = lastCapturedGeneration,
                provenance = "native_executor_callback_and_cached_frame_gate",
                human_input_origin_verified = false,
                legal_menu_reconstructed = false
            })));
    }

    public void StableBoundary(long decisionId, JsonElement state)
    {
        if (_closed) return;
        Write(new NativeActionEnvelopeRecord(
            "stable_decision", Next(), State: Snapshot(state),
            Data: JsonSerializer.SerializeToElement(new { decision_id = decisionId })));
    }

    public void HistoryEntry(
        long historyIndex, string kind, JsonElement payload,
        string? actor, string? target, string[] missingFields)
    {
        if (_closed) return;
        Write(new NativeActionEnvelopeRecord(
            "history_entry", Next(),
            _activeOrder.Count == 0 ? null : _activeOrder[^1],
            NativeType: kind,
            Data: JsonSerializer.SerializeToElement(new
            {
                history_index = historyIndex,
                payload, actor_id = actor, target_id = target,
                missing_fields = missingFields,
                origin = "CombatHistory.Changed",
                semantic_hook_order_verified = false
            })));
    }

    /// <summary>
    /// Record a real native RNG source call without generating a number or
    /// peeking ahead. This method is intentionally NOT called from the
    /// existing frame/history observer: those callbacks cannot attribute
    /// a particular primitive RNG operation or exact hook order.
    /// </summary>
    /// <summary>
    /// Complete source-observed shuffle stage, including the BEFORE-HOOK
    /// Fisher-Yates permutation and AFTER-HOOK order. An initial shuffle
    /// may arrive from the pre-first-frame buffer; it has no executor action.
    /// </summary>
    public void NativeInitialHpRoll(NativeInitialHpRollEvidenceV111 evidence,
        bool bufferedBeforeFirstFrame = false)
    {
        if (_closed) return;
        ArgumentNullException.ThrowIfNull(evidence);
        evidence.Validate();
        Write(new NativeActionEnvelopeRecord(
            "native_initial_hp_roll", Next(),
            _activeOrder.Count == 0 ? null : _activeOrder[^1],
            NativeType: "Creature.SetUniqueMonsterHpValue",
            Data: JsonSerializer.SerializeToElement(new
            {
                creation_ordinal = evidence.CreationOrdinal,
                monster_model_id = evidence.MonsterModelId,
                ordered_eligible_hp = evidence.OrderedEligibleHp,
                selected_index = evidence.SelectedIndex,
                selected_hp = evidence.SelectedHp,
                source_eligible_values_exhausted =
                    evidence.UniqueEligibleValuesExhausted,
                cursor_before = evidence.CursorBefore,
                cursor_after = evidence.CursorAfter,
                source_version = evidence.SourceVersion,
                native_creature_combat_id = (long?)null,
                native_creature_combat_id_status =
                    "PRE_ATTACH_CREATURE_ID_UNVERIFIED",
                recorded_before_first_frame = bufferedBeforeFirstFrame,
                source_callsite_observed = true,
                native_to_emulator_creature_identity = "NOT_TESTED"
            })));
    }

    public void NativeShuffleOperation(NativeShuffleOperationEvidenceV111 evidence)
    {
        if (_closed) return;
        ArgumentNullException.ThrowIfNull(evidence);
        evidence.Validate();
        Write(new NativeActionEnvelopeRecord(
            "native_shuffle_operation", Next(),
            _activeOrder.Count == 0 ? null : _activeOrder[^1],
            NativeType: "native.source.card_pile.shuffle",
            Data: JsonSerializer.SerializeToElement(new
            {
                operation_id = evidence.OperationId,
                kind = evidence.Kind,
                ordered_input = evidence.OrderedInput,
                choices = evidence.Choices.Select(c => new
                {
                    bounded_count = c.BoundedCount,
                    selected_index = c.SelectedIndex,
                    cursor_before = c.CursorBefore,
                    cursor_after = c.CursorAfter
                }).ToArray(),
                pre_hook_order = evidence.PreHookOrder,
                post_hook_order = evidence.PostHookOrder,
                card_models = evidence.CardModels,
                source_version = evidence.SourceVersion,
                card_identity_scope = "shuffle_operation_local",
                recorded_before_first_frame = evidence.Kind == "initial",
                hook_order_stage = "modify_shuffle_order_completed",
                exact_rng_parity = "NOT_TESTED"
            })));
    }

    public void NativeRngBoundary(NativeRngBoundaryEvidenceV111 evidence)
    {
        if (_closed) return;
        ArgumentNullException.ThrowIfNull(evidence);
        evidence.Validate();
        Write(new NativeActionEnvelopeRecord(
            "native_rng_boundary", Next(),
            _activeOrder.Count == 0 ? null : _activeOrder[^1],
            NativeType: "Rng.NextInt",
            Data: JsonSerializer.SerializeToElement(new
            {
                site_id = evidence.SiteId,
                stream_id = evidence.StreamId,
                owner_id = evidence.OwnerId,
                call_ordinal = evidence.CallOrdinal,
                operation_kind = "bounded_integer",
                bounded_count = evidence.BoundedCount,
                selected_index = evidence.SelectedIndex,
                cursor_before = evidence.CursorBefore,
                cursor_after = evidence.CursorAfter,
                source_version = evidence.SourceVersion,
                origin = evidence.Origin,
                attributed = evidence.Attributed,
                source_hook_observed = true
            })));
    }

    /// <summary>
    /// Typed zero-argument scalar primitive callback. Only a source Harmony
    /// postfix may invoke this in production; it neither samples nor advances.
    /// </summary>
    public void NativeRngScalarBoundary(NativeRngScalarEvidenceV111 evidence)
    {
        if (_closed) return;
        ArgumentNullException.ThrowIfNull(evidence);
        evidence.Validate();
        Write(new NativeActionEnvelopeRecord(
            "native_rng_scalar_boundary", Next(),
            _activeOrder.Count == 0 ? null : _activeOrder[^1],
            NativeType: evidence.OperationKind switch
            {
                "unit_double" => "Rng.NextDouble()",
                "boolean" => "Rng.NextBool()",
                "range_float" => "Rng.NextFloat(float,float)",
                _ => "Rng.UNKNOWN"
            },
            Data: JsonSerializer.SerializeToElement(new
            {
                site_id = evidence.SiteId,
                stream_id = evidence.StreamId,
                owner_id = evidence.OwnerId,
                call_ordinal = evidence.CallOrdinal,
                operation_kind = evidence.OperationKind,
                selected_double = evidence.SelectedDouble,
                selected_bool = evidence.SelectedBool,
                selected_float = evidence.SelectedFloat,
                float_min = evidence.FloatMin,
                float_max = evidence.FloatMax,
                cursor_before = evidence.CursorBefore,
                cursor_after = evidence.CursorAfter,
                source_version = evidence.SourceVersion,
                origin = evidence.Origin,
                attributed = evidence.Attributed,
                source_hook_observed = true
            })));
    }

    /// <summary>
    /// Read-only snapshot of all native persistent RNG owners. Never call
    /// this from a synthetic frame cursor reconstruction.
    /// </summary>
    public void NativeRngStreamCheckpoint(NativeRngStreamCheckpointV111 evidence)
    {
        if (_closed) return;
        ArgumentNullException.ThrowIfNull(evidence);
        evidence.Validate();
        Write(new NativeActionEnvelopeRecord(
            "native_rng_stream_checkpoint", Next(),
            _activeOrder.Count == 0 ? null : _activeOrder[^1],
            NativeType: "native.persistent_rng_snapshot",
            Data: JsonSerializer.SerializeToElement(new
            {
                phase = evidence.Phase,
                source = "native_rng_object_to_serializable",
                rng_draws_performed = 0,
                scope = "15_persistent_streams_in_observed_combat",
                streams = evidence.Streams.Select(s => new
                {
                    stream_id = s.StreamId,
                    owner_id = s.OwnerId,
                    cursor = s.Cursor
                }).ToArray(),
                complete_native_rng_inventory = false,
                exact_rng_parity = "NOT_TESTED"
            })));
    }

    public void HistoryReset(long previouslyObserved, long newLength)
    {
        if (_closed) return;
        // The native game clears CombatHistory when an encounter ends.
        // Whether a reset is benign depends on the following callbacks;
        // the auditor only accepts a final reset directly before terminal.
        if (_activeOrder.Count > 0) _incomplete = true;
        Write(new NativeActionEnvelopeRecord(
            "history_reset", Next(),
            Data: JsonSerializer.SerializeToElement(new
            {
                previous_count = previouslyObserved,
                new_count = newLength
            })));
    }

    public void Finish(string reason, JsonElement? state)
    {
        if (_closed) return;
        var outstanding = _activeOrder.ToArray();
        if (outstanding.Length > 0) _incomplete = true;
        Write(new NativeActionEnvelopeRecord(
            "terminal", Next(), State: Snapshot(state),
            Data: JsonSerializer.SerializeToElement(new
            {
                reason, status = _incomplete ? "INCOMPLETE_CAPTURE" : "OBSERVED",
                open_actions = outstanding,
                action_count = _nextActionId,
                native_build = _gameBuild,
                native_game_parity = "NOT_TESTED"
            })));
        _closed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_closed) Finish("Recorder disposed before combat ended", null);
        await _writer.CompleteAsync().ConfigureAwait(false);
    }

    private void Write(NativeActionEnvelopeRecord record)
    {
        if (!_writer.TryEnqueue(record)) _incomplete = true;
    }

    private long Next() => ++_sequence;
    private static JsonElement? Snapshot(JsonElement? state) => state?.Clone();
    private static JsonElement Data(string reason, string detail) =>
        JsonSerializer.SerializeToElement(new { reason, detail });
}

public sealed record NativeActionEnvelopeRecord(
    string Type, long Ordinal, long? ActionId = null,
    long? ParentActionId = null, string? NativeType = null,
    GameAction? CanonicalAction = null, JsonElement? State = null,
    JsonElement? Data = null);
