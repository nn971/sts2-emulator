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
    public const string SemanticChanceSchemaId =
        SemanticPilotCombatV111.SchemaId;

    private static readonly JsonSerializerOptions SnakeCaseJsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
        };

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
        var semanticStates = new Dictionary<string, SemanticPilotCombatV111>(
            StringComparer.Ordinal);
        long nextSemanticHandle = 1;
        var semanticProfiler = new SemanticOperationProfiler();
        string? activeOperation = null;
        long activeStarted = 0;
        long activeAllocated = 0;
        var referenceNatureStates = new Dictionary<string, ReferenceNatureSessionV111>(
            StringComparer.Ordinal);
        long nextReferenceNatureHandle = 1;
        var referenceNatureRunActionStates =
            new Dictionary<string, ReferenceNatureRunActionSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureRunActionRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureRunActionHandle = 1;
        var referenceNatureEventChoiceStates =
            new Dictionary<string, ReferenceNatureControlledEventChoiceSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureEventChoiceRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureEventChoiceHandle = 1;
        var referenceNatureAncientOfferStates =
            new Dictionary<string, ReferenceNatureAncientOfferSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureAncientOfferRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureAncientOfferHandle = 1;
        var referenceNatureNeowOfferStates =
            new Dictionary<string, ReferenceNatureNeowOfferSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureNeowOfferRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureNeowOfferHandle = 1;
        var referenceNatureTrashHeapStates =
            new Dictionary<string, ReferenceNatureTrashHeapSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureTrashHeapRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureTrashHeapHandle = 1;
        var referenceNatureTabletOfTruthStates =
            new Dictionary<string, ReferenceNatureTabletOfTruthSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureTabletOfTruthRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureTabletOfTruthHandle = 1;
        var referenceNatureTrialStates =
            new Dictionary<string, ReferenceNatureTrialSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureTrialRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureTrialHandle = 1;
        var referenceNatureNativeEventShuffleStates =
            new Dictionary<string, ReferenceNatureNativeEventShuffleSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureNativeEventShuffleRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureNativeEventShuffleHandle = 1;
        var referenceNatureNativeEventGoldStates =
            new Dictionary<string, ReferenceNatureNativeEventGoldSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureNativeEventGoldRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureNativeEventGoldHandle = 1;
        var referenceNatureNativeEventRelicStates =
            new Dictionary<string, ReferenceNatureNativeEventRelicSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureNativeEventRelicRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureNativeEventRelicHandle = 1;
        var referenceNatureNativeShovelStates =
            new Dictionary<string, ReferenceNatureNativeShovelSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureNativeShovelRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureNativeShovelHandle = 1;
        var referenceNatureEndlessConveyorStates =
            new Dictionary<string, ReferenceNatureEndlessConveyorSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureEndlessConveyorRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureEndlessConveyorHandle = 1;
        var referenceNatureGloryReflectionsStates =
            new Dictionary<string, ReferenceNatureGloryReflectionsSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureGloryReflectionsRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureGloryReflectionsHandle = 1;
        var referenceNatureInfestedAutomatonStates =
            new Dictionary<string, ReferenceNatureInfestedAutomatonSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureInfestedAutomatonRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureInfestedAutomatonHandle = 1;
        var referenceNaturePunchOffRewardStates =
            new Dictionary<string, ReferenceNaturePunchOffRewardSessionV111>(
                StringComparer.Ordinal);
        var referenceNaturePunchOffRewardRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNaturePunchOffRewardHandle = 1;
        var referenceNaturePunchOffFightStates =
            new Dictionary<string, ReferenceNaturePunchOffFightSessionV111>(
                StringComparer.Ordinal);
        var referenceNaturePunchOffFightRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNaturePunchOffFightHandle = 1;
        var referenceNatureMerchantStates =
            new Dictionary<string, ReferenceNatureMerchantSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureMerchantRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureMerchantHandle = 1;
        var referenceNatureTreasureStates =
            new Dictionary<string, ReferenceNatureTreasureSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureTreasureRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureTreasureHandle = 1;
        var referenceNatureCombatRewardStates =
            new Dictionary<string, ReferenceNatureCombatRewardSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureCombatRewardRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureCombatRewardHandle = 1;
        var referenceNatureMapGenerationStates =
            new Dictionary<string, ReferenceNatureMapGenerationSessionV111>(
                StringComparer.Ordinal);
        long nextReferenceNatureMapGenerationHandle = 1;
        var scriptedRunActionStates =
            new Dictionary<string, ScriptedRunActionSessionV111>(
                StringComparer.Ordinal);
        var scriptedRunActionRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextScriptedRunActionHandle = 1;
        var scriptedRunStates =
            new Dictionary<string, ScriptedRunSessionV111>(
                StringComparer.Ordinal);
        var scriptedRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextScriptedRunHandle = 1;

        string StoreSemantic(SemanticPilotCombatV111 state)
        {
            var handle = "m" + nextSemanticHandle++;
            var started = semanticProfiler.Enabled
                ? SemanticOperationProfiler.Timestamp() : 0L;
            var allocated = semanticProfiler.Enabled
                ? SemanticOperationProfiler.Allocated() : 0L;
            semanticStates.Add(handle, state.Fork());
            semanticProfiler.Record("sub.semantic_store_fork", started, allocated);
            return handle;
        }

        SemanticPilotCombatV111 RequireSemantic(JsonElement request)
        {
            var handle = RequiredString(request, "semantic_handle");
            return semanticStates.TryGetValue(handle, out var state)
                ? state
                : throw new InvalidOperationException(
                    "Unknown semantic session handle: " + handle);
        }

        string StoreReferenceNature(ReferenceNatureSessionV111 session)
        {
            var handle = "r" + nextReferenceNatureHandle++;
            referenceNatureStates.Add(handle, session.Fork());
            return handle;
        }

        ReferenceNatureSessionV111 RequireReferenceNature(JsonElement request)
        {
            var handle = RequiredString(request, "reference_nature_handle");
            return referenceNatureStates.TryGetValue(handle, out var state)
                ? state
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature session handle: " + handle);
        }

        string StoreReferenceNatureRunAction(
            ReferenceNatureRunActionSessionV111 session)
        {
            var handle = "u" + nextReferenceNatureRunActionHandle++;
            referenceNatureRunActionStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureRunActionRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureRunActionSessionV111 RequireReferenceNatureRunAction(
            JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_run_action_handle");
            return referenceNatureRunActionStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature run-action session handle: " + handle);
        }

        string StoreReferenceNatureEventChoice(
            ReferenceNatureControlledEventChoiceSessionV111 session)
        {
            var handle = "v" + nextReferenceNatureEventChoiceHandle++;
            referenceNatureEventChoiceStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureEventChoiceRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureControlledEventChoiceSessionV111
            RequireReferenceNatureEventChoice(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_event_choice_handle");
            return referenceNatureEventChoiceStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature event-choice session handle: " +
                    handle);
        }

        string StoreReferenceNatureAncientOffer(
            ReferenceNatureAncientOfferSessionV111 session)
        {
            var handle = "w" + nextReferenceNatureAncientOfferHandle++;
            referenceNatureAncientOfferStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureAncientOfferRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureAncientOfferSessionV111
            RequireReferenceNatureAncientOffer(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_ancient_offer_handle");
            return referenceNatureAncientOfferStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature Ancient-offer session handle: " +
                    handle);
        }

        string StoreReferenceNatureNeowOffer(
            ReferenceNatureNeowOfferSessionV111 session)
        {
            var handle = "x" + nextReferenceNatureNeowOfferHandle++;
            referenceNatureNeowOfferStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureNeowOfferRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureNeowOfferSessionV111
            RequireReferenceNatureNeowOffer(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_neow_offer_handle");
            return referenceNatureNeowOfferStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature Neow-offer session handle: " +
                    handle);
        }

        string StoreReferenceNatureTrashHeap(
            ReferenceNatureTrashHeapSessionV111 session)
        {
            var handle = "th" + nextReferenceNatureTrashHeapHandle++;
            referenceNatureTrashHeapStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureTrashHeapRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureTrashHeapSessionV111
            RequireReferenceNatureTrashHeap(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_trash_heap_handle");
            return referenceNatureTrashHeapStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature Trash Heap session handle: " +
                    handle);
        }

        string StoreReferenceNatureTabletOfTruth(
            ReferenceNatureTabletOfTruthSessionV111 session)
        {
            var handle = "tt" + nextReferenceNatureTabletOfTruthHandle++;
            referenceNatureTabletOfTruthStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureTabletOfTruthRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureTabletOfTruthSessionV111
            RequireReferenceNatureTabletOfTruth(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_tablet_of_truth_handle");
            return referenceNatureTabletOfTruthStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature Tablet of Truth session handle: " +
                    handle);
        }

        string StoreReferenceNatureTrial(
            ReferenceNatureTrialSessionV111 session)
        {
            var handle = "tr" + nextReferenceNatureTrialHandle++;
            referenceNatureTrialStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureTrialRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureTrialSessionV111 RequireReferenceNatureTrial(
            JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_trial_handle");
            return referenceNatureTrialStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature Trial session handle: " + handle);
        }

        string StoreReferenceNatureNativeEventShuffle(
            ReferenceNatureNativeEventShuffleSessionV111 session)
        {
            var handle = "es" + nextReferenceNatureNativeEventShuffleHandle++;
            referenceNatureNativeEventShuffleStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureNativeEventShuffleRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureNativeEventShuffleSessionV111
            RequireReferenceNatureNativeEventShuffle(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_native_event_shuffle_handle");
            return referenceNatureNativeEventShuffleStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature native event shuffle session handle: " +
                    handle);
        }

        string StoreReferenceNatureNativeEventGold(
            ReferenceNatureNativeEventGoldSessionV111 session)
        {
            var handle = "eg" + nextReferenceNatureNativeEventGoldHandle++;
            referenceNatureNativeEventGoldStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureNativeEventGoldRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureNativeEventGoldSessionV111
            RequireReferenceNatureNativeEventGold(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_native_event_gold_handle");
            return referenceNatureNativeEventGoldStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature native event gold session handle: " +
                    handle);
        }

        string StoreReferenceNatureNativeEventRelic(
            ReferenceNatureNativeEventRelicSessionV111 session)
        {
            var handle = "er" + nextReferenceNatureNativeEventRelicHandle++;
            referenceNatureNativeEventRelicStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureNativeEventRelicRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureNativeEventRelicSessionV111
            RequireReferenceNatureNativeEventRelic(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_native_event_relic_handle");
            return referenceNatureNativeEventRelicStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature native event relic session handle: " +
                    handle);
        }

        string StoreReferenceNatureNativeShovel(
            ReferenceNatureNativeShovelSessionV111 session)
        {
            var handle = "sh" + nextReferenceNatureNativeShovelHandle++;
            referenceNatureNativeShovelStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureNativeShovelRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureNativeShovelSessionV111
            RequireReferenceNatureNativeShovel(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_native_shovel_handle");
            return referenceNatureNativeShovelStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature Native Shovel session handle: " +
                    handle);
        }

        string StoreReferenceNatureEndlessConveyor(
            ReferenceNatureEndlessConveyorSessionV111 session)
        {
            var handle = "ec" + nextReferenceNatureEndlessConveyorHandle++;
            referenceNatureEndlessConveyorStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureEndlessConveyorRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureEndlessConveyorSessionV111
            RequireReferenceNatureEndlessConveyor(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_endless_conveyor_handle");
            return referenceNatureEndlessConveyorStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature Endless Conveyor session handle: " +
                    handle);
        }

        string StoreReferenceNatureGloryReflections(
            ReferenceNatureGloryReflectionsSessionV111 session)
        {
            var handle = "gr" + nextReferenceNatureGloryReflectionsHandle++;
            referenceNatureGloryReflectionsStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureGloryReflectionsRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureGloryReflectionsSessionV111
            RequireReferenceNatureGloryReflections(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_glory_reflections_handle");
            return referenceNatureGloryReflectionsStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature Glory Reflections session handle: " +
                    handle);
        }

        string StoreReferenceNatureInfestedAutomaton(
            ReferenceNatureInfestedAutomatonSessionV111 session)
        {
            var handle = "ia" + nextReferenceNatureInfestedAutomatonHandle++;
            referenceNatureInfestedAutomatonStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureInfestedAutomatonRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureInfestedAutomatonSessionV111
            RequireReferenceNatureInfestedAutomaton(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_infested_automaton_handle");
            return referenceNatureInfestedAutomatonStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature Infested Automaton session handle: " +
                    handle);
        }

        string StoreReferenceNaturePunchOffReward(
            ReferenceNaturePunchOffRewardSessionV111 session)
        {
            var handle = "po" + nextReferenceNaturePunchOffRewardHandle++;
            referenceNaturePunchOffRewardStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNaturePunchOffRewardRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNaturePunchOffRewardSessionV111
            RequireReferenceNaturePunchOffReward(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_punch_off_reward_handle");
            return referenceNaturePunchOffRewardStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature Punch Off reward session handle: " +
                    handle);
        }

        string StoreReferenceNaturePunchOffFight(
            ReferenceNaturePunchOffFightSessionV111 session)
        {
            var handle = "pf" + nextReferenceNaturePunchOffFightHandle++;
            referenceNaturePunchOffFightStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNaturePunchOffFightRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNaturePunchOffFightSessionV111
            RequireReferenceNaturePunchOffFight(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_punch_off_fight_handle");
            return referenceNaturePunchOffFightStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature Punch Off fight session handle: " +
                    handle);
        }

        string StoreReferenceNatureMerchant(
            ReferenceNatureMerchantSessionV111 session)
        {
            var handle = "x" + nextReferenceNatureMerchantHandle++;
            referenceNatureMerchantStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureMerchantRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureMerchantSessionV111 RequireReferenceNatureMerchant(
            JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_merchant_handle");
            return referenceNatureMerchantStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature merchant session handle: " +
                    handle);
        }

        string StoreReferenceNatureTreasure(
            ReferenceNatureTreasureSessionV111 session)
        {
            var handle = "y" + nextReferenceNatureTreasureHandle++;
            referenceNatureTreasureStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureTreasureRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureTreasureSessionV111 RequireReferenceNatureTreasure(
            JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_treasure_handle");
            return referenceNatureTreasureStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature treasure session handle: " +
                    handle);
        }

        string StoreReferenceNatureCombatReward(
            ReferenceNatureCombatRewardSessionV111 session)
        {
            var handle = "w" + nextReferenceNatureCombatRewardHandle++;
            referenceNatureCombatRewardStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureCombatRewardRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureCombatRewardSessionV111
            RequireReferenceNatureCombatReward(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_combat_reward_handle");
            return referenceNatureCombatRewardStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature combat reward session handle: " +
                    handle);
        }

        string StoreReferenceNatureMapGeneration(
            ReferenceNatureMapGenerationSessionV111 session)
        {
            var handle = "z" + nextReferenceNatureMapGenerationHandle++;
            referenceNatureMapGenerationStates.Add(handle, session.Fork());
            return handle;
        }

        ReferenceNatureMapGenerationSessionV111
            RequireReferenceNatureMapGeneration(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_map_generation_handle");
            return referenceNatureMapGenerationStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature map-generation session handle: " +
                    handle);
        }

        string StoreScriptedRunAction(
            ScriptedRunActionSessionV111 session)
        {
            var handle = "s" + nextScriptedRunActionHandle++;
            scriptedRunActionStates.Add(handle, session.Fork());
            if (session.Complete)
                scriptedRunActionRunStateHandles.Add(handle,
                    Store(session.Run!.Fork()));
            return handle;
        }

        ScriptedRunActionSessionV111 RequireScriptedRunAction(
            JsonElement request)
        {
            var handle = RequiredString(request,
                "scripted_run_action_handle");
            return scriptedRunActionStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown scripted run-action session handle: " + handle);
        }

        string StoreScriptedRun(ScriptedRunSessionV111 session)
        {
            var handle = "u" + nextScriptedRunHandle++;
            scriptedRunStates.Add(handle, session.Fork());
            scriptedRunStateHandles.Add(handle, Store(session.Run.Fork()));
            return handle;
        }

        ScriptedRunSessionV111 RequireScriptedRun(JsonElement request)
        {
            var handle = RequiredString(request, "scripted_run_handle");
            return scriptedRunStates.TryGetValue(handle, out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown scripted run session handle: " + handle);
        }

        object ReferenceNatureResult(
            ReferenceNatureSessionV111 session, string handle,
            string? parent = null)
        {
            var combat = session.Combat;
            return new
            {
                ok = true,
                schemaId = ReferenceNatureSessionV111.CurrentSchemaId,
                referenceNatureHandle = handle,
                parent,
                fingerprint = session.Fingerprint,
                combatFingerprint = combat.Fingerprint,
                boundary = combat.CombatTerminal ? "terminal" :
                    combat.AtPlayerBoundary ? "player" :
                    combat.AtPlayerChoiceBoundary ? "player_choice" : "nature",
                pending = DescribeSemanticPending(combat),
                terminal = combat.CombatTerminal,
                outcome = combat.CombatOutcome,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus,
                sourceOpeningFingerprint = combat.SourceOpeningFingerprint,
                sourceRouteProvider = combat.SourceRouteAudit?.ProviderId,
                otherChanceProvider = combat.OtherChance?.ProviderId ??
                    "engine-owned-deterministic-unverified-v1",
                otherChanceDecisionCount = combat.OtherChance?.Decisions.Length ?? 0,
                rewardProvider = combat.RewardAudit?.ProviderId,
                rewardDrawCount = combat.RewardAudit?.Calls
            };
        }

        object ReferenceNatureRunActionResult(
            ReferenceNatureRunActionSessionV111 session, string handle,
            string? parent = null)
        {
            var continuation = session.Run.ControlledRunActionChoice;
            var chanceLaw = continuation is null ? null :
                ChanceLawEngineV111.QueryControlledRunAction(session.Run);
            var runFingerprint = CanonicalJson.Sha256(session.Run);
            var boundaryFingerprint = continuation is null ? null :
                ChanceLawEngineV111.ControlledRunActionBoundaryFingerprint(
                    session.Run);
            object? pending = continuation is null ? null : new
            {
                kind = "run_action_random_choice",
                eventId = chanceLaw!.EventId,
                boundaryFingerprint,
                actionId = continuation.ActionId,
                request = continuation.PendingRequest,
                candidateLabels = continuation.PendingCandidateLabels,
                chanceLaw,
                probabilityStatus = chanceLaw.ProbabilityStatus
            };
            return new
            {
                ok = true,
                schemaId = ReferenceNatureRunActionSessionV111.CurrentSchemaId,
                referenceNatureRunActionHandle = handle,
                stateHandle = referenceNatureRunActionRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint,
                actionId = session.Action.Kind,
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                pending,
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureEventChoiceResult(
            ReferenceNatureControlledEventChoiceSessionV111 session,
            string handle, string? parent = null)
        {
            var effect = session.Run.World?.Event?.ControlledChoice;
            var reward = session.Run.ControlledEventRewardChoice;
            var chanceLaw = session.NaturePending
                ? ChanceLawEngineV111.QueryControlledEventChoice(session.Run)
                : null;
            var boundaryFingerprint = session.NaturePending
                ? ChanceLawEngineV111.ControlledEventChoiceBoundaryFingerprint(
                    session.Run)
                : null;
            var request = effect?.PendingRequest ?? reward?.PendingRequest;
            var candidateLabels = effect?.PendingCandidateLabels ??
                reward?.PendingCandidateLabels;
            var pendingProbabilityStatus = chanceLaw?.ProbabilityStatus ??
                ChanceLawV111.UnverifiedStatus;
            object? pending = session.NaturePending ? new
            {
                kind = effect is null
                    ? "event_reward_random_choice" : "event_random_choice",
                eventId = chanceLaw!.EventId,
                choiceId = session.ChoiceId,
                boundaryFingerprint,
                request,
                candidateLabels,
                chanceLaw,
                probabilityStatus = pendingProbabilityStatus
            } : null;
            var actions = session.CanAdvancePlayer
                ? environment.Observe(session.Run).LegalActions
                : Array.Empty<PrototypeAiAction>();
            return new
            {
                ok = true,
                schemaId =
                    ReferenceNatureControlledEventChoiceSessionV111.CurrentSchemaId,
                referenceNatureEventChoiceHandle = handle,
                stateHandle = referenceNatureEventChoiceRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                eventId = session.Origin.World?.Event?.EventId,
                choiceId = session.ChoiceId,
                runPhase = session.Run.Phase.ToString(),
                boundary = session.NaturePending ? "nature" :
                    session.CanAdvancePlayer ? "player" : "committed",
                pending,
                actions = actions.Select(ActionWire).ToArray(),
                decisionTranscript = session.DecisionTranscript,
                playerActionTranscript = session.Steps
                    .Where(step => step.Kind == "player_action")
                    .Select(step => step.PlayerAction).ToArray(),
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = pendingProbabilityStatus
            };
        }

        object ReferenceNatureAncientOfferResult(
            ReferenceNatureAncientOfferSessionV111 session,
            string handle, string? parent = null)
        {
            var eventState = session.Run.World?.Event;
            var law = session.PendingLaw;
            return new
            {
                ok = true,
                schemaId = ReferenceNatureAncientOfferSessionV111.CurrentSchemaId,
                referenceNatureAncientOfferHandle = handle,
                stateHandle = referenceNatureAncientOfferRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                siteId = session.SiteId,
                eventId = session.SiteId switch
                {
                    var site when site == NativeOrobasEventV111.OfferSiteId =>
                        NativeOrobasEventV111.EventId,
                    var site when site == NativePaelRewardsV111.OfferSiteId =>
                        NativePaelRewardsV111.PaelId,
                    var site when site ==
                        NativeAncientRewardsV111.TezcataraOfferSiteId =>
                        NativeAncientRewardsV111.TezcataraId,
                    var site when site ==
                        NativeNonupeipeEventV111.ShuffleSiteId =>
                        NativeNonupeipeEventV111.EventId,
                    var site when site == NativeTanxEventV111.ShuffleSiteId =>
                        NativeTanxEventV111.EventId,
                    var site when site ==
                        NativeDarvEventV111.OfferVariantSiteId =>
                        NativeDarvEventV111.EventId,
                    _ => NativeVakuuEventV111.EventId
                },
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending = law is null ? null : new
                {
                    kind = "ancient_event_offer_random_choice",
                    boundaryFingerprint = law.EventId,
                    chanceLaw = law,
                    probabilityStatus = law.ProbabilityStatus
                },
                offeredChoiceIds = eventState?.OfferedChoiceIds,
                eventRandomProvider = eventState?.NativeEventRngProviderMode ??
                    (eventState is null ? null : "native-v111"),
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = law?.ProbabilityStatus ??
                    ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureMerchantResult(
            ReferenceNatureMerchantSessionV111 session, string handle,
            string? parent = null)
        {
            var law = session.PendingLaw;
            object? pending = law is null ? null : new
            {
                kind = "native_merchant_random_choice",
                request = session.PendingRequest,
                candidateLabels = session.PendingCandidateLabels,
                chanceLaw = law,
                probabilityStatus = law.ProbabilityStatus
            };
            return new
            {
                ok = true,
                schemaId = ReferenceNatureMerchantSessionV111.CurrentSchemaId,
                referenceNatureMerchantHandle = handle,
                stateHandle = referenceNatureMerchantRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                actionId = PrototypeAiEnvironment.StableActionId(session.Action),
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending,
                shop = session.Complete ? session.Run.World?.Shop : null,
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureNeowOfferResult(
            ReferenceNatureNeowOfferSessionV111 session,
            string handle, string? parent = null)
        {
            var eventState = session.Run.World?.Event;
            var law = session.PendingLaw;
            return new
            {
                ok = true,
                schemaId = ReferenceNatureNeowOfferSessionV111.CurrentSchemaId,
                referenceNatureNeowOfferHandle = handle,
                stateHandle = referenceNatureNeowOfferRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                siteId = law?.SiteId,
                eventId = ChanceLawEngineV111.NeowOfferEventId,
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending = law is null ? null : new
                {
                    kind = "neow_initial_offer_random_choice",
                    boundaryFingerprint = law.EventId,
                    chanceLaw = law,
                    probabilityStatus = law.ProbabilityStatus
                },
                offeredChoiceIds = eventState?.OfferedChoiceIds,
                eventRandomProvider = eventState?.NativeEventRngProviderMode ??
                    (eventState is null ? null : "native-v111"),
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureTrashHeapResult(
            ReferenceNatureTrashHeapSessionV111 session,
            string handle, string? parent = null)
        {
            var law = session.PendingLaw;
            return new
            {
                ok = true,
                schemaId = ReferenceNatureTrashHeapSessionV111.CurrentSchemaId,
                referenceNatureTrashHeapHandle = handle,
                stateHandle = referenceNatureTrashHeapRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                siteId = law?.SiteId,
                eventId = law?.EventId,
                choiceId = session.ChoiceId,
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending = law is null ? null : new
                {
                    kind = "trash_heap_reward_random_choice",
                    boundaryFingerprint = law.EventId,
                    chanceLaw = law,
                    probabilityStatus = law.ProbabilityStatus
                },
                selectedChoiceId = session.Run.World?.Event?.ChosenChoiceId,
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureTabletOfTruthResult(
            ReferenceNatureTabletOfTruthSessionV111 session,
            string handle, string? parent = null)
        {
            var law = session.PendingLaw;
            var eventState = session.Run.World?.Event;
            return new
            {
                ok = true,
                schemaId =
                    ReferenceNatureTabletOfTruthSessionV111.CurrentSchemaId,
                referenceNatureTabletOfTruthHandle = handle,
                stateHandle = referenceNatureTabletOfTruthRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                siteId = law?.SiteId,
                eventId = law?.EventId ??
                    session.DecisionTranscript.LastOrDefault()?.EventId,
                choiceId = "decipher",
                page = session.Origin.World?.Event?.NativePageIndex,
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending = law is null ? null : new
                {
                    kind = "tablet_of_truth_card_upgrade_random_choice",
                    boundaryFingerprint = law.EventId,
                    chanceLaw = law,
                    probabilityStatus = law.ProbabilityStatus
                },
                nextPage = eventState?.NativePageIndex,
                eventRandomProvider = eventState?.NativeEventRngProviderMode ??
                    (eventState is null ? null : "native-v111"),
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureTrialResult(
            ReferenceNatureTrialSessionV111 session,
            string handle, string? parent = null)
        {
            var law = session.PendingLaw;
            var eventState = session.Run.World?.Event;
            return new
            {
                ok = true,
                schemaId = ReferenceNatureTrialSessionV111.CurrentSchemaId,
                referenceNatureTrialHandle = handle,
                stateHandle = referenceNatureTrialRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                siteId = law?.SiteId,
                eventId = law?.EventId ??
                    session.DecisionTranscript.LastOrDefault()?.EventId,
                choiceId = "accept",
                page = session.Origin.World?.Event?.NativePageIndex,
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending = law is null ? null : new
                {
                    kind = "glory_trial_case_random_choice",
                    boundaryFingerprint = law.EventId,
                    chanceLaw = law,
                    probabilityStatus = law.ProbabilityStatus
                },
                nextPage = eventState?.NativePageIndex,
                eventRandomProvider = eventState?.NativeEventRngProviderMode ??
                    (eventState is null ? null : "native-v111"),
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureNativeEventShuffleResult(
            ReferenceNatureNativeEventShuffleSessionV111 session,
            string handle, string? parent = null)
        {
            var law = session.PendingLaw;
            var eventState = session.Run.World?.Event;
            return new
            {
                ok = true,
                schemaId =
                    ReferenceNatureNativeEventShuffleSessionV111.CurrentSchemaId,
                referenceNatureNativeEventShuffleHandle = handle,
                stateHandle = referenceNatureNativeEventShuffleRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                siteId = law?.SiteId ??
                    session.SiteId,
                eventId = law?.EventId ??
                    session.DecisionTranscript.LastOrDefault()?.EventId,
                choiceId = session.Origin.Phase == RunPhase.Event
                    ? session.ActionId : null,
                actionId = session.ActionId,
                page = session.Origin.World?.Event?.NativePageIndex,
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending = law is null ? null : new
                {
                    kind = law.SiteId switch
                    {
                        PrototypeGameEngine.FishingRodUpgradeSiteId =>
                            "fishing_rod_combat_end_upgrade_random_choice",
                        PrototypeGameEngine.ImprovementCardSiteId =>
                            "improvement_combat_end_upgrade_random_choice",
                        PrototypeGameEngine.PaelsToothCardReturnSiteId =>
                            "paels_tooth_combat_end_return_random_choice",
                        "relic.war_hammer.elite_upgrade.shuffle_swap_index" =>
                            "war_hammer_elite_shuffle_swap_random_choice",
                        "event.war_historian.repy.potion_offer.index_draw" =>
                            "repy_potion_offer_index_random_choice",
                        PrototypeNativeHiveEvents
                            .WarHistorianRepyRelicShuffleSiteId =>
                            "repy_relic_shuffle_swap_random_choice",
                        _ => "native_event_shuffle_swap_random_choice"
                    },
                    boundaryFingerprint = law.EventId,
                    chanceLaw = law,
                    probabilityStatus = law.ProbabilityStatus
                },
                selectedChoiceId = eventState?.ChosenChoiceId,
                nextPage = eventState?.NativePageIndex,
                eventRandomProvider = session.SiteId ==
                    PrototypeNativeHiveEvents.WarHistorianRepyRelicShuffleSiteId ||
                    session.SiteId is PrototypeGameEngine.FishingRodUpgradeSiteId or
                        PrototypeGameEngine.ImprovementCardSiteId or
                        PrototypeGameEngine.PaelsToothCardReturnSiteId or
                        "relic.war_hammer.elite_upgrade.shuffle_swap_index"
                        ? "not_applicable"
                        : eventState?.NativeEventRngProviderMode ??
                            (eventState is null ? null : "native-v111"),
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = law?.ProbabilityStatus ??
                    ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureNativeEventGoldResult(
            ReferenceNatureNativeEventGoldSessionV111 session,
            string handle, string? parent = null)
        {
            var law = session.PendingLaw;
            return new
            {
                ok = true,
                schemaId =
                    ReferenceNatureNativeEventGoldSessionV111.CurrentSchemaId,
                referenceNatureNativeEventGoldHandle = handle,
                stateHandle = referenceNatureNativeEventGoldRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                siteId = law?.SiteId ??
                    ChanceLawEngineV111.NativeEventGoldUnitFloatSiteId,
                eventId = session.EventId,
                actionId = session.ActionId,
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending = law is null ? null : new
                {
                    kind = "native_event_generated_gold_random_choice",
                    boundaryFingerprint = law.EventId,
                    chanceLaw = law,
                    probabilityStatus = law.ProbabilityStatus
                },
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureNativeEventRelicResult(
            ReferenceNatureNativeEventRelicSessionV111 session,
            string handle, string? parent = null)
        {
            var law = session.PendingLaw;
            var choiceId = session.Action.Kind == "event_choice"
                ? session.Action.ReadPayload<EventChoicePayload>().ChoiceId
                : null;
            return new
            {
                ok = true,
                schemaId =
                    ReferenceNatureNativeEventRelicSessionV111.CurrentSchemaId,
                referenceNatureNativeEventRelicHandle = handle,
                stateHandle = referenceNatureNativeEventRelicRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                siteId = law?.SiteId ??
                    PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId,
                eventId = session.Origin.World?.Event?.EventId,
                choiceId,
                actionId = PrototypeAiEnvironment.StableActionId(
                    session.Action),
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending = law is null ? null : new
                {
                    kind = "native_event_relic_draw",
                    boundaryFingerprint = law.EventId,
                    chanceLaw = law,
                    probabilityStatus = law.ProbabilityStatus
                },
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureNativeShovelResult(
            ReferenceNatureNativeShovelSessionV111 session,
            string handle, string? parent = null)
        {
            var law = session.PendingLaw;
            return new
            {
                ok = true,
                schemaId = ReferenceNatureNativeShovelSessionV111.CurrentSchemaId,
                referenceNatureNativeShovelHandle = handle,
                stateHandle = referenceNatureNativeShovelRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                siteId = law?.SiteId ??
                    PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId,
                actionId = PrototypeAiEnvironment.StableActionId(session.Action),
                boundaryId = "rest_shovel_dig",
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending = law is null ? null : new
                {
                    kind = "native_shovel_relic_draw",
                    boundaryFingerprint = law.EventId,
                    chanceLaw = law,
                    probabilityStatus = law.ProbabilityStatus
                },
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureEndlessConveyorResult(
            ReferenceNatureEndlessConveyorSessionV111 session,
            string handle, string? parent = null)
        {
            var law = session.PendingLaw;
            var eventState = session.Run.World?.Event;
            return new
            {
                ok = true,
                schemaId =
                    ReferenceNatureEndlessConveyorSessionV111.CurrentSchemaId,
                referenceNatureEndlessConveyorHandle = handle,
                stateHandle = referenceNatureEndlessConveyorRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                siteId = law?.SiteId ??
                    PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId,
                eventId = law?.EventId ??
                    session.DecisionTranscript.LastOrDefault()?.EventId,
                actionId = session.ActionId,
                page = eventState?.NativePageIndex,
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending = law is null ? null : new
                {
                    kind = law.SiteId switch
                    {
                        PrototypeNativeEndlessConveyor.UpgradeCardSiteId =>
                            "native_endless_conveyor_card_upgrade_random_choice",
                        PrototypeNativeEndlessConveyor.ColorlessCardSiteId =>
                            "native_endless_conveyor_colorless_card_random_choice",
                        _ => "native_endless_conveyor_dish_random_choice"
                    },
                    boundaryFingerprint = law.EventId,
                    chanceLaw = law,
                    probabilityStatus = law.ProbabilityStatus
                },
                selectedDishId = eventState?.NativeDishId,
                selectedOutcomeId = session.DecisionTranscript
                    .LastOrDefault()?.OutcomeId,
                nextPage = eventState?.NativePageIndex,
                eventRandomProvider = eventState?.NativeEventRngProviderMode ??
                    (eventState is null ? null : "native-v111"),
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureGloryReflectionsResult(
            ReferenceNatureGloryReflectionsSessionV111 session,
            string handle, string? parent = null)
        {
            var law = session.PendingLaw;
            var eventState = session.Run.World?.Event;
            return new
            {
                ok = true,
                schemaId =
                    ReferenceNatureGloryReflectionsSessionV111.CurrentSchemaId,
                referenceNatureGloryReflectionsHandle = handle,
                stateHandle = referenceNatureGloryReflectionsRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                siteId = law?.SiteId ?? session.LastSiteId,
                eventId = law?.EventId ??
                    session.DecisionTranscript.LastOrDefault()?.EventId,
                choiceId = "touch_mirror",
                page = session.Origin.World?.Event?.NativePageIndex,
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.DecisionTranscript.Length,
                pending = law is null ? null : new
                {
                    kind = "native_event_card_pick_random_choice",
                    eventId = law.EventId,
                    siteId = law.SiteId,
                    operationKind = law.OperationKind,
                    supportFingerprint = law.SupportFingerprint,
                    boundaryFingerprint = law.EventId,
                    chanceLaw = law,
                    probabilityStatus = law.ProbabilityStatus
                },
                selectedChoiceId = eventState?.ChosenChoiceId,
                nextPage = eventState?.NativePageIndex,
                eventRandomProvider = eventState?.NativeEventRngProviderMode ??
                    (eventState is null ? null : "native-v111"),
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureInfestedAutomatonResult(
            ReferenceNatureInfestedAutomatonSessionV111 session,
            string handle, string? parent = null)
        {
            var law = session.PendingLaw;
            var eventState = session.Run.World?.Event;
            return new
            {
                ok = true,
                schemaId =
                    ReferenceNatureInfestedAutomatonSessionV111.CurrentSchemaId,
                referenceNatureInfestedAutomatonHandle = handle,
                stateHandle = referenceNatureInfestedAutomatonRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                siteId = law?.SiteId ?? session.LastSiteId,
                eventId = law?.EventId ??
                    session.DecisionTranscript.LastOrDefault()?.EventId,
                choiceId = session.ChoiceId,
                page = session.Origin.World?.Event?.NativePageIndex,
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.DecisionTranscript.Length,
                pending = law is null ? null : new
                {
                    kind = "native_event_card_pick_random_choice",
                    eventId = law.EventId,
                    siteId = law.SiteId,
                    operationKind = law.OperationKind,
                    supportFingerprint = law.SupportFingerprint,
                    boundaryFingerprint = law.EventId,
                    chanceLaw = law,
                    probabilityStatus = law.ProbabilityStatus
                },
                selectedChoiceId = eventState?.ChosenChoiceId,
                selectedCardId = eventState?.ChosenChoiceId is not null
                    ? session.Run.Player.Deck.LastOrDefault()?.CardId
                    : null,
                nextPage = eventState?.NativePageIndex,
                eventRandomProvider = eventState?.NativeEventRngProviderMode ??
                    (eventState is null ? null : "native-v111"),
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNaturePunchOffRewardResult(
            ReferenceNaturePunchOffRewardSessionV111 session,
            string handle, string? parent = null)
        {
            var law = session.PendingLaw;
            var reward = session.Run.World?.Reward;
            var selectedRelic = session.DecisionTranscript.Length > 0
                ? session.DecisionTranscript[0].OutcomeId["relic:".Length..]
                : null;
            var selectedPotion = session.DecisionTranscript.Length > 1
                ? session.DecisionTranscript[1].OutcomeId["potion:".Length..]
                : null;
            var currentSiteId = law?.SiteId ??
                (session.DecisionTranscript.Length == 0 ? null :
                    session.DecisionTranscript.Length == 1
                        ? "event.punch_off.relic_reward.index_draw"
                        : "event.punch_off.potion_reward.index_draw");
            var entryRewardsStream = session.Origin.NativeRunRng?.Streams
                .SingleOrDefault(item => item.StreamId ==
                    "player.rewards");
            var finalRewardsStream = session.Run.NativeRunRng?.Streams
                .SingleOrDefault(item => item.StreamId ==
                    "player.rewards");
            return new
            {
                ok = true,
                schemaId =
                    ReferenceNaturePunchOffRewardSessionV111.CurrentSchemaId,
                referenceNaturePunchOffRewardHandle = handle,
                stateHandle = referenceNaturePunchOffRewardRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                actionId = session.ActionId,
                eventId = law?.EventId ??
                    session.DecisionTranscript.LastOrDefault()?.EventId,
                sourceEventId = PrototypeNativePunchOff.EventId,
                siteId = currentSiteId,
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.DecisionTranscript.Length,
                pending = law is { } pendingLaw ? new
                {
                    kind = "punch_off_combat_reward_random_choice",
                    eventId = pendingLaw.EventId,
                    siteId = pendingLaw.SiteId,
                    operationKind = pendingLaw.OperationKind,
                    supportFingerprint = pendingLaw.SupportFingerprint,
                    boundaryFingerprint = pendingLaw.EventId,
                    chanceLaw = pendingLaw,
                    probabilityStatus = pendingLaw.ProbabilityStatus
                } : null,
                selectedRelic,
                selectedPotion,
                rewardRelic = reward?.RelicOption,
                rewardPotion = reward?.PotionOption,
                playerRewardsCursorAdvanced = session.Complete &&
                    (entryRewardsStream is null || finalRewardsStream is null ||
                     NativeRngV111.ToSnapshot(entryRewardsStream) !=
                     NativeRngV111.ToSnapshot(finalRewardsStream)),
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNaturePunchOffFightResult(
            ReferenceNaturePunchOffFightSessionV111 session,
            string handle, string? parent = null)
        {
            var law = session.PendingLaw;
            var offsets = session.DecisionTranscript.Select(decision =>
                int.Parse(decision.OutcomeId["hp_offset:".Length..],
                    System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var contexts = session.Run.World?.Combat?.NativeRng?.Formation
                .MonsterContexts ?? [];
            return new
            {
                ok = true,
                schemaId =
                    ReferenceNaturePunchOffFightSessionV111.CurrentSchemaId,
                referenceNaturePunchOffFightHandle = handle,
                stateHandle = referenceNaturePunchOffFightRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                actionId = session.ActionId,
                sourceEventId = PrototypeNativePunchOff.EventId,
                eventId = law?.EventId ??
                    session.DecisionTranscript.LastOrDefault()?.EventId,
                siteId = PrototypeNativePunchOff.ConstructHpOffsetSiteId,
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.DecisionTranscript.Length,
                pending = law is { } pendingLaw ? new
                {
                    kind = "punch_off_construct_hp_offset_random_choice",
                    eventId = pendingLaw.EventId,
                    siteId = pendingLaw.SiteId,
                    operationKind = pendingLaw.OperationKind,
                    supportFingerprint = pendingLaw.SupportFingerprint,
                    boundaryFingerprint = pendingLaw.EventId,
                    chanceLaw = pendingLaw,
                    probabilityStatus = pendingLaw.ProbabilityStatus
                } : null,
                selectedHpOffsets = offsets,
                monsterCombatIds = contexts.Select(context => context.CombatId)
                    .ToArray(),
                monsterCursorsUnadvanced = session.Complete,
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = law?.ProbabilityStatus ??
                    ChanceLawV111.SourceVerifiedStatus
            };
        }

        object ReferenceNatureTreasureResult(
            ReferenceNatureTreasureSessionV111 session, string handle,
            string? parent = null)
        {
            var law = session.PendingLaw;
            object? pending = law is null ? null : new
            {
                kind = "native_treasure_random_choice",
                request = session.PendingRequest,
                candidateLabels = session.PendingCandidateLabels,
                chanceLaw = law,
                probabilityStatus = law.ProbabilityStatus
            };
            return new
            {
                ok = true,
                schemaId = ReferenceNatureTreasureSessionV111.CurrentSchemaId,
                referenceNatureTreasureHandle = handle,
                stateHandle = referenceNatureTreasureRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                actionId = PrototypeAiEnvironment.StableActionId(session.Action),
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending,
                reward = session.Complete ? session.Run.World?.Reward : null,
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureCombatRewardResult(
            ReferenceNatureCombatRewardSessionV111 session, string handle,
            string? parent = null)
        {
            var law = session.PendingLaw;
            object? pending = law is null ? null : new
            {
                kind = "native_combat_reward_random_choice",
                request = session.PendingRequest,
                candidateLabels = session.PendingCandidateLabels,
                chanceLaw = law,
                probabilityStatus = law.ProbabilityStatus
            };
            return new
            {
                ok = true,
                schemaId = ReferenceNatureCombatRewardSessionV111.CurrentSchemaId,
                referenceNatureCombatRewardHandle = handle,
                stateHandle = referenceNatureCombatRewardRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                actionId = PrototypeAiEnvironment.StableActionId(session.Action),
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending,
                reward = session.Complete ? session.Run.World?.Reward : null,
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureMapGenerationResult(
            ReferenceNatureMapGenerationSessionV111 session,
            string handle, string? parent = null)
        {
            var law = session.PendingLaw;
            return new
            {
                ok = true,
                schemaId =
                    ReferenceNatureMapGenerationSessionV111.CurrentSchemaId,
                referenceNatureMapGenerationHandle = handle,
                parent,
                fingerprint = session.Fingerprint,
                act = session.Act,
                ascension = session.Ascension,
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending = law is null ? null : new
                {
                    kind = "native_map_random_choice",
                    request = session.PendingRequest,
                    chanceLaw = law,
                    probabilityStatus = law.ProbabilityStatus
                },
                map = session.Complete ? session.Map : null,
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ScriptedRunActionResult(
            ScriptedRunActionSessionV111 session, string handle,
            string? parent = null)
        {
            scriptedRunActionRunStateHandles.TryGetValue(handle,
                out var runStateHandle);
            return new
            {
                ok = true,
                schemaId = ScriptedRunActionSessionV111.CurrentSchemaId,
                scriptedRunActionHandle = handle,
                parent,
                fingerprint = session.Fingerprint,
                actionId = PrototypeAiEnvironment.StableActionId(
                    session.Action),
                boundary = session.Complete ? "committed" : "scripted",
                nextSequence = session.Decisions.Length + 1,
                pending = session.PendingRequest is { } pending
                    ? new
                    {
                        request = pending,
                        candidateLabels = session.PendingCandidateLabels,
                        probabilityStatus = ChanceLawV111.UnverifiedStatus
                    }
                    : null,
                decisionTranscript = session.Decisions,
                runStateHandle,
                runFingerprint = session.Run is null ? null :
                    CanonicalJson.Sha256(session.Run),
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ScriptedRunResult(
            ScriptedRunSessionV111 session, string handle,
            string? parent = null)
        {
            scriptedRunStateHandles.TryGetValue(handle,
                out var runStateHandle);
            return new
            {
                ok = true,
                schemaId = ScriptedRunSessionV111.CurrentSchemaId,
                scriptedRunHandle = handle,
                parent,
                fingerprint = session.Fingerprint,
                boundary = session.Complete ? "terminal" :
                    session.ActiveAction is null ? "player" : "scripted",
                completedActionCount = session.CompletedActions.Length,
                activeActionId = session.ActiveAction is null ? null :
                    PrototypeAiEnvironment.StableActionId(
                        session.ActiveAction.Action),
                nextSequence = session.ActiveAction is null ? (int?)null :
                    session.ActiveAction.Decisions.Length + 1,
                pending = session.PendingRequest is { } pending
                    ? new
                    {
                        request = pending,
                        candidateLabels = session.PendingCandidateLabels,
                        probabilityStatus = ChanceLawV111.UnverifiedStatus
                    }
                    : null,
                decisionTranscript = session.ActiveAction?.Decisions ?? [],
                legalActions = session.LegalActions.Select(action =>
                    new
                    {
                        actionId = PrototypeAiEnvironment.StableActionId(action),
                        kind = action.Kind,
                        payload = action.Payload
                    }).ToArray(),
                runStateHandle,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object? DescribeSemanticPending(SemanticPilotCombatV111 state)
        {
            if (state.DrawHookChoicePending is { } hookChoice)
            {
                return new
                {
                    kind = "draw_hook_action_choice",
                    schemaId = hookChoice.SchemaId,
                    eventId = hookChoice.EventId,
                    boundaryFingerprint = hookChoice.BoundaryFingerprint,
                    supportFingerprint = hookChoice.SupportFingerprint,
                    choiceId = hookChoice.ChoiceId,
                    automaticHandDraw = hookChoice.AutomaticHandDraw,
                    candidates = hookChoice.Candidates.Select(candidate => new
                    {
                        actionId = candidate.ActionId,
                        kind = candidate.Kind,
                        payloadFingerprint = candidate.PayloadFingerprint
                    }).ToArray()
                };
            }
            if (state.IntentPending is { } intent)
            {
                var semanticEvent = SemanticPilotCombatEngineV111
                    .DescribePendingIntentEvent(state);
                var support = semanticEvent.Support;
                var chanceLaw = ChanceLawEngineV111.QueryIntent(semanticEvent);
                return new
                {
                    kind = "enemy_intent",
                    siteId = intent.SiteId,
                    streamId = intent.StreamId,
                    ownerId = intent.OwnerId,
                    ordinal = intent.Ordinal,
                    operationKind = semanticEvent.OperationKind,
                    supportCount = intent.SupportCount,
                    eligibleIndices = Enumerable.Range(0, intent.SupportCount),
                    semanticIntentSupportSchemaId = SemanticIntentSupportV111.SchemaId,
                    semanticIntentEventSchemaId =
                        SemanticIntentEventV111.EventSchemaId,
                    semanticEvent,
                    chanceLaw,
                    eventId = semanticEvent.EventId,
                    semanticCandidates =
                        support.Candidates.Select(candidate => new
                        {
                            candidate.IntentId,
                            candidate.MoveId,
                            candidate.MoveIndex,
                            candidate.RawWeight,
                            candidate.TicketStart,
                            normalizedWeight = (double)candidate.RawWeight /
                                support.TotalRawWeight
                        }).ToArray(),
                    rawEligibleWeights = support.Candidates
                        .Select(candidate => candidate.RawWeight).ToArray(),
                    totalRawWeight = support.TotalRawWeight,
                    semanticSupportFingerprint = CanonicalJson.Sha256(support),
                    weightsVerified = false,
                    probabilityStatus = chanceLaw.ProbabilityStatus,
                    probabilityProvenance = chanceLaw.ProbabilityProvenance,
                    supportFingerprint = CanonicalJson.Sha256(intent),
                    sourceScope = "source_filtered_semantic_eligible_moves"
                };
            }
            if (state.EnemyActionEventPending is { } enemyActionEvent)
            {
                var enemyAction = enemyActionEvent.Request;
                var chanceLaw = ChanceLawEngineV111.QueryEndTurnIndex(
                    enemyActionEvent);
                return new
                {
                    kind = "enemy_action_chance",
                    schemaId = enemyActionEvent.SchemaId,
                    eventId = enemyActionEvent.EventId,
                    boundaryFingerprint =
                        enemyActionEvent.BoundaryFingerprint,
                    siteId = enemyAction.SiteId,
                    streamId = enemyAction.StreamId,
                    ownerId = enemyAction.OwnerId,
                    ordinal = enemyAction.Ordinal,
                    operationKind = enemyAction.OperationKind,
                    supportCount = enemyAction.SupportCount,
                    eligibleIndices = Enumerable.Range(
                        0, enemyAction.SupportCount).ToArray(),
                    supportFingerprint =
                        enemyActionEvent.SupportFingerprint,
                    chanceLaw,
                    probabilityStatus = chanceLaw.ProbabilityStatus
                };
            }
            if (state.RandomPileCardPending is { } pileCard)
            {
                var chanceLaw = ChanceLawEngineV111
                    .QueryRandomPileCard(pileCard);
                return new
                {
                    kind = "random_pile_card",
                    schemaId = pileCard.SchemaId,
                    eventId = pileCard.EventId,
                    siteId = pileCard.SiteId,
                    streamId = pileCard.StreamId,
                    ownerId = pileCard.OwnerId,
                    ordinal = pileCard.Ordinal,
                    operationKind = pileCard.OperationKind,
                    supportFingerprint = pileCard.SupportFingerprint,
                    chanceLaw,
                    probabilityStatus = pileCard.ProbabilityStatus,
                    candidates = pileCard.CandidateCardInstanceIds
                        .Select((id, index) => new
                        {
                            index,
                            cardInstanceId = id,
                            cardModelId = pileCard.CandidateCardModelIds[index]
                        }).ToArray()
                };
            }
            if (state.DrawPending is { } drawn)
            {
                var combat = state.Run.World?.Combat ??
                    throw new InvalidDataException("Draw event missing its combat.");
                var chanceLaw = ChanceLawEngineV111.QueryDraw(
                    combat.SemanticDeck ?? throw new InvalidDataException(
                        "Draw event missing its provider-owned deck."));
                return new
                {
                    kind = "draw_card",
                    schemaId = drawn.SchemaId,
                    eventId = drawn.EventId,
                    siteId = drawn.SiteId,
                    streamId = drawn.StreamId,
                    ownerId = drawn.OwnerId,
                    ordinal = drawn.Ordinal,
                    shuffleEpoch = combat.SemanticDeck!.ShuffleEpoch,
                    topOffset = drawn.TopOffset,
                    operationKind = drawn.OperationKind,
                    supportFingerprint = drawn.SupportFingerprint,
                    constraintFingerprint = combat.SemanticDeck.Fingerprint,
                    chanceLaw,
                    probabilityStatus = drawn.ProbabilityStatus,
                    candidates = drawn.CandidateCardInstanceIds
                        .Select((id, index) => new
                        {
                            index,
                            cardInstanceId = id,
                            cardModelId = combat.Cards.Single(c =>
                                c.InstanceId == id).CardId
                        }).ToArray()
                };
            }
            if (state.DeckRevealPending is { } reveal)
            {
                var combat = state.Run.World?.Combat ??
                    throw new InvalidDataException(
                        "Deck-reveal event missing its combat.");
                var deck = combat.SemanticDeck ??
                    throw new InvalidDataException(
                        "Deck-reveal event missing its provider-owned deck.");
                var chanceLaw = ChanceLawEngineV111.QueryDeckReveal(deck);
                return new
                {
                    kind = "deck_reveal",
                    schemaId = SemanticDeckRevealDecisionV111.CurrentSchemaId,
                    eventId = reveal.EventId,
                    siteId = reveal.SiteId,
                    purpose = reveal.Purpose,
                    streamId = reveal.StreamId,
                    ownerId = reveal.OwnerId,
                    ordinal = reveal.Ordinal,
                    topOffset = reveal.TopOffset,
                    operationKind = reveal.OperationKind,
                    supportFingerprint = reveal.SupportFingerprint,
                    constraintFingerprint = deck.Fingerprint,
                    chanceLaw,
                    probabilityStatus = reveal.ProbabilityStatus,
                    candidates = reveal.CandidateCardInstanceIds
                        .Select((id, index) => new
                        {
                            index,
                            cardInstanceId = id,
                            cardModelId = combat.Cards.Single(c =>
                                c.InstanceId == id).CardId
                        }).ToArray()
                };
            }
            if (state.TargetPending is { } target)
            {
                var chanceLaw = ChanceLawEngineV111.QueryTarget(target);
                return new
                {
                    kind = "random_target",
                    schemaId = target.SchemaId,
                    eventId = target.EventId,
                    siteId = target.SiteId,
                    streamId = target.StreamId,
                    ownerId = target.OwnerId,
                    ordinal = target.Ordinal,
                    targetKind = target.TargetKind,
                    operationKind = target.OperationKind,
                    supportFingerprint = target.SupportFingerprint,
                    chanceLaw,
                    probabilityStatus = target.ProbabilityStatus,
                    candidates = target.CandidateEnemyInstanceIds
                        .Select((id, index) => new
                        {
                            index,
                            enemyInstanceId = id
                        }).ToArray()
                };
            }
            if (state.GeneratedCardPending is { } generated)
            {
                var chanceLaw = ChanceLawEngineV111
                    .QueryGeneratedCard(generated);
                return new
                {
                    kind = "generated_card",
                    schemaId = generated.SchemaId,
                    eventId = generated.EventId,
                    siteId = generated.SiteId,
                    streamId = generated.StreamId,
                    ownerId = generated.OwnerId,
                    ordinal = generated.Ordinal,
                    operationKind = generated.OperationKind,
                    supportFingerprint = generated.SupportFingerprint,
                    chanceLaw,
                    probabilityStatus = generated.ProbabilityStatus,
                    candidates = generated.CandidateCardIds
                        .Select((id, index) => new
                        {
                            index,
                            cardModelId = id
                        }).ToArray()
                };
            }
            if (state.PotionGenerationPending is { } potionGeneration)
            {
                var chanceLaw = ChanceLawEngineV111
                    .QueryPotionGeneration(potionGeneration);
                return new
                {
                    kind = "potion_generation",
                    schemaId = potionGeneration.SchemaId,
                    eventId = potionGeneration.EventId,
                    siteId = potionGeneration.SiteId,
                    streamId = potionGeneration.StreamId,
                    ownerId = potionGeneration.OwnerId,
                    ordinal = potionGeneration.Ordinal,
                    operationKind = potionGeneration.OperationKind,
                    supportFingerprint = potionGeneration.SupportFingerprint,
                    chanceLaw,
                    probabilityStatus = potionGeneration.ProbabilityStatus,
                    candidates = potionGeneration.CandidatePotionIds
                        .Select((id, index) => new
                        {
                            index,
                            potionModelId = id
                        }).ToArray()
                };
            }
            if (state.CardTransformPending is { } cardTransform)
            {
                var chanceLaw = ChanceLawEngineV111
                    .QueryCardTransform(cardTransform);
                return new
                {
                    kind = "card_transform",
                    schemaId = cardTransform.SchemaId,
                    eventId = cardTransform.EventId,
                    siteId = cardTransform.SiteId,
                    streamId = cardTransform.StreamId,
                    ownerId = cardTransform.OwnerId,
                    ordinal = cardTransform.Ordinal,
                    operationKind = cardTransform.OperationKind,
                    supportFingerprint = cardTransform.SupportFingerprint,
                    chanceLaw,
                    sourceCardInstanceId = cardTransform.SourceCardInstanceId,
                    originalCardId = cardTransform.OriginalCardId,
                    probabilityStatus = cardTransform.ProbabilityStatus,
                    candidates = cardTransform.CandidateCardIds
                        .Select((id, index) => new
                        {
                            index,
                            cardModelId = id
                        }).ToArray()
                };
            }
            if (state.CardSelectionPending is { } selection)
            {
                return new
                {
                    kind = "player_card_selection",
                    schemaId = selection.SchemaId,
                    eventId = selection.EventId,
                    supportFingerprint = selection.SupportFingerprint,
                    sourceZone = selection.SourceZone,
                    resolution = selection.Resolution,
                    sourceCardInstanceId = selection.SourceCardInstanceId,
                    minimumSelections = selection.MinimumSelections,
                    maximumSelections = selection.MaximumSelections,
                    candidates = selection.CandidateCardInstanceIds
                        .Select((id, index) => new
                        {
                            cardInstanceId = id,
                            cardModelId = selection.CandidateCardModelIds[index]
                        }).ToArray(),
                    actions = selection.Candidates.Select(candidate => new
                    {
                        actionId = candidate.ActionId,
                        cardInstanceIds = candidate.CardInstanceIds
                    }).ToArray()
                };
            }
            if (state.HandCostSelectionPending is { } handCost)
            {
                var chanceLaw = ChanceLawEngineV111
                    .QueryHandCostSelection(handCost);
                return new
                {
                    kind = "random_hand_card_selection",
                    schemaId = handCost.SchemaId,
                    eventId = handCost.EventId,
                    siteId = handCost.SiteId,
                    streamId = handCost.StreamId,
                    ownerId = handCost.OwnerId,
                    ordinal = handCost.Ordinal,
                    operationKind = handCost.OperationKind,
                    supportFingerprint = handCost.SupportFingerprint,
                    chanceLaw,
                    appliedCost = handCost.AppliedCost,
                    probabilityStatus = handCost.ProbabilityStatus,
                    candidates = handCost.CandidateCardInstanceIds
                        .Select((id, index) => new
                        {
                            index,
                            cardInstanceId = id,
                            cardModelId = handCost.CandidateCardModelIds[index]
                        }).ToArray()
                };
            }
            if (state.CardShortlistPending is { } shortlist)
            {
                var chanceLaw = ChanceLawEngineV111
                    .QueryCardShortlist(shortlist);
                return new
                {
                    kind = shortlist.SiteId switch
                    {
                        "combat.autoplay.random_card.select" =>
                            "random_auto_play_card",
                        "combat.draw_pile.random_replay_card.select" =>
                            "random_replay_card",
                        _ => "random_pile_selection_shortlist"
                    },
                    schemaId = shortlist.SchemaId,
                    eventId = shortlist.EventId,
                    siteId = shortlist.SiteId,
                    streamId = shortlist.StreamId,
                    ownerId = shortlist.OwnerId,
                    ordinal = shortlist.Ordinal,
                    operationKind = shortlist.OperationKind,
                    supportFingerprint = shortlist.SupportFingerprint,
                    chanceLaw,
                    requiredCount = shortlist.RequiredCount,
                    eligibleCards = shortlist.EligibleCardInstanceIds
                        .Select((id, index) => new
                        {
                            cardInstanceId = id,
                            cardModelId = shortlist.EligibleCardModelIds[index]
                        }).ToArray(),
                    selectedCardInstanceIds =
                        shortlist.SelectedCardInstanceIds,
                    candidates = shortlist.CandidateCardInstanceIds
                        .Select((id, index) => new
                        {
                            index,
                            cardInstanceId = id,
                            cardModelId = shortlist.CandidateCardModelIds[index]
                        }).ToArray(),
                    probabilityStatus = shortlist.ProbabilityStatus
                };
            }
            if (state.CostPending is { } cost)
            {
                var chanceLaw = ChanceLawEngineV111.QueryCost(cost);
                return new
                {
                    kind = cost.TargetCardInstanceId is null
                        ? "drawn_card_cost" : "random_hand_card_cost",
                    schemaId = cost.SchemaId,
                    eventId = cost.EventId,
                    siteId = cost.Request.SiteId,
                    streamId = cost.Request.StreamId,
                    ownerId = cost.Request.OwnerId,
                    ordinal = cost.Request.Ordinal,
                    operationKind = cost.Request.OperationKind,
                    supportFingerprint = cost.SupportFingerprint,
                    chanceLaw,
                    probabilityStatus = cost.ProbabilityStatus,
                    targetCardInstanceId = cost.TargetCardInstanceId,
                    targetCardModelId = cost.TargetCardModelId,
                    candidates = cost.CandidateCosts.Select((value, index) =>
                        new { index, cost = value }).ToArray()
                };
            }
            if (state.PoolShufflePending is { } pool)
            {
                var chanceLaw = ChanceLawEngineV111.QueryPoolShuffle(pool);
                return new
                {
                    kind = pool.SiteId ==
                        "combat.draw_pile.random_rare_to_hand.shuffle_swap_index"
                            ? "random_rare_to_hand_shuffle"
                            : pool.SiteId ==
                                "combat.hand_return.draw_pile.shuffle_swap_index"
                                ? "hand_return_shuffle"
                            : "generated_card_pool_shuffle",
                    schemaId = pool.SchemaId,
                    eventId = pool.EventId,
                    siteId = pool.SiteId,
                    streamId = pool.StreamId,
                    ownerId = pool.OwnerId,
                    ordinal = pool.Ordinal,
                    operationKind = pool.OperationKind,
                    sourceOperationKind = pool.SourceOperationKind,
                    supportFingerprint = pool.SupportFingerprint,
                    chanceLaw,
                    nextSwapIndex = pool.NextSwapIndex,
                    selectedCount = pool.SelectedCount,
                    sourceCardInstanceId = pool.SourceCardInstanceId,
                    originalCardIds = pool.OriginalCardIds,
                    currentCardIds = pool.CurrentCardIds,
                    candidates = Enumerable.Range(0, pool.Request.SupportCount)
                        .Select(index => new { index, swapIndex = index })
                        .ToArray(),
                    probabilityStatus = pool.ProbabilityStatus
                };
            }
            if (state.DrawInsertionPending is { } insertion)
            {
                var chanceLaw = ChanceLawEngineV111
                    .QueryDrawInsertion(insertion);
                return new
                {
                    kind = "generated_draw_pile_insertion",
                    schemaId = insertion.SchemaId,
                    eventId = insertion.EventId,
                    siteId = insertion.SiteId,
                    streamId = insertion.StreamId,
                    ownerId = insertion.OwnerId,
                    ordinal = insertion.Ordinal,
                    operationKind = insertion.OperationKind,
                    supportFingerprint = insertion.SupportFingerprint,
                    chanceLaw,
                    generatedCardModelId = insertion.GeneratedCardModelId,
                    eligibleDrawPileInstanceIds =
                        insertion.EligibleDrawPileInstanceIds,
                    candidates = Enumerable.Range(
                            0, insertion.Request.SupportCount)
                        .Select(position => new { position })
                        .ToArray(),
                    probabilityStatus = insertion.ProbabilityStatus
                };
            }
            return null;
        }

        SemanticNatureChoiceViewV111? CreateNatureChoiceView(
            object? pending)
        {
            if (pending is null) return null;
            var payload = JsonSerializer.SerializeToElement(
                pending, pending.GetType(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (!payload.TryGetProperty("chanceLaw", out _)) return null;
            return SemanticNatureChoiceViewV111.Create(payload);
        }

        SemanticNatureChoiceViewV111? DescribeSemanticNatureChoice(
            SemanticPilotCombatV111 state) =>
            state.UsesNativePolicyBaseline
                ? null
                : CreateNatureChoiceView(DescribeSemanticPending(state));

        (string Fingerprint, SemanticNatureChoiceViewV111? Choice)
            DescribeNatureChoiceStatus(string sessionKind, string handle)
        {
            object status = sessionKind switch
            {
                "semantic_combat" when semanticStates.TryGetValue(
                    handle, out var semantic) => SemanticResult(semantic, handle),
                "reference_combat" when referenceNatureStates.TryGetValue(
                    handle, out var combatSession) =>
                    ReferenceNatureResult(combatSession, handle),
                "run_action" when referenceNatureRunActionStates.TryGetValue(
                    handle, out var runActionSession) =>
                    ReferenceNatureRunActionResult(runActionSession, handle),
                "event_choice" when referenceNatureEventChoiceStates.TryGetValue(
                    handle, out var eventChoiceSession) =>
                    ReferenceNatureEventChoiceResult(eventChoiceSession, handle),
                "ancient_offer" when referenceNatureAncientOfferStates.TryGetValue(
                    handle, out var ancientOfferSession) =>
                    ReferenceNatureAncientOfferResult(ancientOfferSession, handle),
                "neow_offer" when referenceNatureNeowOfferStates.TryGetValue(
                    handle, out var neowOfferSession) =>
                    ReferenceNatureNeowOfferResult(neowOfferSession, handle),
                "trash_heap" when referenceNatureTrashHeapStates.TryGetValue(
                    handle, out var trashHeapSession) =>
                    ReferenceNatureTrashHeapResult(trashHeapSession, handle),
                "tablet_of_truth" when referenceNatureTabletOfTruthStates.TryGetValue(
                    handle, out var tabletOfTruthSession) =>
                    ReferenceNatureTabletOfTruthResult(
                        tabletOfTruthSession, handle),
                "trial" when referenceNatureTrialStates.TryGetValue(
                    handle, out var trialSession) =>
                    ReferenceNatureTrialResult(trialSession, handle),
                "event_shuffle" when referenceNatureNativeEventShuffleStates
                    .TryGetValue(handle, out var nativeEventShuffleSession) =>
                    ReferenceNatureNativeEventShuffleResult(
                        nativeEventShuffleSession, handle),
                "native_event_gold" when referenceNatureNativeEventGoldStates
                    .TryGetValue(handle, out var nativeEventGoldSession) =>
                    ReferenceNatureNativeEventGoldResult(
                        nativeEventGoldSession, handle),
                "native_event_relic" when referenceNatureNativeEventRelicStates
                    .TryGetValue(handle, out var nativeEventRelicSession) =>
                    ReferenceNatureNativeEventRelicResult(
                        nativeEventRelicSession, handle),
                "native_shovel" when referenceNatureNativeShovelStates
                    .TryGetValue(handle, out var nativeShovelSession) =>
                    ReferenceNatureNativeShovelResult(
                        nativeShovelSession, handle),
                "endless_conveyor" when referenceNatureEndlessConveyorStates
                    .TryGetValue(handle, out var endlessConveyorSession) =>
                    ReferenceNatureEndlessConveyorResult(
                        endlessConveyorSession, handle),
                "glory_reflections" when referenceNatureGloryReflectionsStates
                    .TryGetValue(handle, out var gloryReflectionsSession) =>
                    ReferenceNatureGloryReflectionsResult(
                        gloryReflectionsSession, handle),
                "infested_automaton" when referenceNatureInfestedAutomatonStates
                    .TryGetValue(handle, out var infestedAutomatonSession) =>
                    ReferenceNatureInfestedAutomatonResult(
                        infestedAutomatonSession, handle),
                "punch_off_reward" when referenceNaturePunchOffRewardStates
                    .TryGetValue(handle, out var punchOffRewardSession) =>
                    ReferenceNaturePunchOffRewardResult(
                        punchOffRewardSession, handle),
                "punch_off_fight" when referenceNaturePunchOffFightStates
                    .TryGetValue(handle, out var punchOffFightSession) =>
                    ReferenceNaturePunchOffFightResult(
                        punchOffFightSession, handle),
                "merchant" when referenceNatureMerchantStates.TryGetValue(
                    handle, out var merchantSession) =>
                    ReferenceNatureMerchantResult(merchantSession, handle),
                "treasure" when referenceNatureTreasureStates.TryGetValue(
                    handle, out var treasureSession) =>
                    ReferenceNatureTreasureResult(treasureSession, handle),
                "combat_reward" when referenceNatureCombatRewardStates
                    .TryGetValue(handle, out var combatRewardSession) =>
                    ReferenceNatureCombatRewardResult(
                        combatRewardSession, handle),
                "map_generation" when referenceNatureMapGenerationStates
                    .TryGetValue(handle, out var mapGenerationSession) =>
                    ReferenceNatureMapGenerationResult(
                        mapGenerationSession, handle),
                "controlled_event_choice" when states.TryGetValue(
                    handle, out var eventRun) =>
                    ControlledEventChoiceResult(eventRun, handle, null),
                "controlled_run_action" when states.TryGetValue(
                    handle, out var actionRun) =>
                    ControlledRunActionResult(actionRun, handle, null),
                "semantic_combat" or "reference_combat" or "run_action" or
                    "event_choice" or "ancient_offer" or "merchant" or
                    "neow_offer" or "trash_heap" or "tablet_of_truth" or
                    "trial" or "event_shuffle" or "glory_reflections" or
                    "infested_automaton" or "punch_off_reward" or
                    "punch_off_fight" or
                    "native_shovel" or
                    "endless_conveyor" or
                    "treasure" or "combat_reward" or "map_generation" or
                    "controlled_event_choice" or
                    "controlled_run_action" => throw new InvalidOperationException(
                        "Unknown Nature session handle for kind " + sessionKind +
                        ": " + handle),
                _ => throw new ArgumentException(
                    "Unsupported Nature session kind: " + sessionKind)
            };
            var result = JsonSerializer.SerializeToElement(
                status, status.GetType(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            string fingerprint = RequiredString(result, "fingerprint");
            var pending = result.TryGetProperty("pending", out var pendingValue) &&
                pendingValue.ValueKind == JsonValueKind.Object
                    ? pendingValue
                    : default;
            var choice = pending.ValueKind == JsonValueKind.Object &&
                pending.TryGetProperty("chanceLaw", out _)
                    ? SemanticNatureChoiceViewV111.Create(pending)
                    : null;
            return (fingerprint, choice);
        }

        string[] SemanticChanceSites(SemanticPilotCombatV111 state)
        {
            if (state.UsesNativePolicyBaseline) return [];
            var sites = new List<string> { "enemy_intent", "draw_card" };
            if (state.DeckRevealPending is not null)
                sites.Add("deck_reveal");
            if (state.RandomPileCardPending is not null ||
                state.Run.Player.ActiveRelics.Any(relic =>
                    relic.RelicId == "proto.relic.jeweled_mask"))
                sites.Add("random_pile_card");
            if (state.SeededEntry?.FixtureRandomTargetCardId is not null)
                sites.Add("random_target");
            if (state.SeededEntry?.FixtureGenerationPotionId is not null)
                sites.Add("generated_card");
            if (state.SeededEntry?.FixturePotionGenerationCardId is not null ||
                state.PotionGenerationPending is not null)
                sites.Add("random_potion_generation");
            if (state.CardTransformPending is not null)
                sites.Add("random_card_transform");
            if (state.SeededEntry?.FixtureNatureDrawCosts == true)
                sites.Add("drawn_card_cost");
            if (state.SeededEntry?.FixturePoolGenerationPotionId is not null ||
                state.PoolShufflePending?.SiteId ==
                    "combat.generation.full_pool_shuffle.swap_index")
                sites.Add("generated_card_pool_shuffle");
            if (state.Run.Player.PotionSlots.Any(potion =>
                    potion?.PotionId == "proto.potion.bottled_potential") ||
                state.PoolShufflePending?.SiteId ==
                    "combat.hand_return.draw_pile.shuffle_swap_index")
                sites.Add("hand_return_shuffle");
            if (state.EnemyActionPending is not null)
                sites.Add("enemy_action_chance");
            if (state.SeededEntry?.FixtureSelectionCardId is
                    "proto.colorless.beat_down" or
                    "proto.colorless.catastrophe" ||
                state.CardShortlistPending?.SiteId ==
                    "combat.autoplay.random_card.select")
                sites.Add("random_auto_play_card");
            if (state.SeededEntry?.FixtureSelectionCardId ==
                    "proto.colorless.hidden_gem" ||
                state.CardShortlistPending?.SiteId ==
                    "combat.draw_pile.random_replay_card.select")
                sites.Add("random_replay_card");
            if (state.SeededEntry?.FixtureSelectionCardId ==
                    "proto.colorless.anointed" ||
                state.PoolShufflePending?.SiteId ==
                    "combat.draw_pile.random_rare_to_hand.shuffle_swap_index")
                sites.Add("random_rare_to_hand_shuffle");
            if (state.SeededEntry?.FixtureGeneratedDrawInsertionCardId is not null)
                sites.Add("generated_draw_pile_insertion");
            if (state.DrawInsertionPending?.SiteId ==
                    PrototypeControlledDrawInsertionV111
                        .PersonalHiveDazedSiteId &&
                !sites.Contains("generated_draw_pile_insertion"))
                sites.Add("generated_draw_pile_insertion");
            if (state.SeededEntry?.FixtureRandomFreeAttackDrawInsertion == true)
            {
                sites.Add("random_draw_attack_card_model");
                sites.Add("generated_draw_pile_insertion");
            }
            if ((state.Run.World?.Combat?.PendingChoice?
                    .ControlledDrawInsertion?.AfterShuffleRelic is not null ||
                 state.Run.Player.ActiveRelics.Any(relic =>
                     PrototypeContent.Relic(relic.RelicId)
                         .AfterShuffleGeneratedCardId is not null)) &&
                !sites.Contains("generated_draw_pile_insertion"))
                sites.Add("generated_draw_pile_insertion");
            if (state.SeededEntry?.FixtureRandomizeHandCostsMaxExclusive is not null)
                sites.Add("random_hand_card_energy_cost");
            if (state.SeededEntry?.FixtureSetRandomHandCardCost is not null)
                sites.Add("random_hand_card_select");
            if (state.SeededEntry?.FixtureSelectionCardId ==
                "proto.colorless.seeker_strike")
                sites.Add("random_pile_selection_shortlist");
            if (state.CardShortlistPending is not null &&
                !sites.Contains("random_pile_selection_shortlist"))
                sites.Add("random_pile_selection_shortlist");
            if (state.HandCostSelectionPending is not null &&
                !sites.Contains("random_hand_card_select"))
                sites.Add("random_hand_card_select");
            if (state.TargetPending is not null &&
                !sites.Contains("random_target"))
                sites.Add("random_target");
            if (state.GeneratedCardPending is not null &&
                !sites.Contains("generated_card"))
                sites.Add("generated_card");
            if (state.PotionGenerationPending is not null &&
                !sites.Contains("random_potion_generation"))
                sites.Add("random_potion_generation");
            if (state.CardTransformPending is not null &&
                !sites.Contains("random_card_transform"))
                sites.Add("random_card_transform");
            if (state.CostPending is
                    { TargetCardInstanceId: null } &&
                !sites.Contains("drawn_card_cost"))
                sites.Add("drawn_card_cost");
            if (state.CostPending?.TargetCardInstanceId is not null &&
                !sites.Contains("random_hand_card_energy_cost"))
                sites.Add("random_hand_card_energy_cost");
            return sites.ToArray();
        }

        string SemanticControllerMode(SemanticPilotCombatV111 state) =>
            state.UsesNativePolicyBaseline
                ? "engine-owned-native-v111-policy-baseline"
                : state.SeededEntry?.FixtureRandomTargetCardId is not null ||
                  state.SeededEntry?.FixtureGenerationPotionId is not null ||
                  state.SeededEntry?.FixturePotionGenerationCardId is not null ||
                  state.SeededEntry?.FixtureSelectionCardId is not null ||
                  state.SeededEntry?.FixtureNatureDrawCosts == true ||
                  state.SeededEntry?.FixturePoolGenerationPotionId is not null ||
                  state.SeededEntry?.FixtureTopAutoplayPotionId is not null ||
                  state.SeededEntry?.FixtureGeneratedDrawInsertionCardId is not null ||
                  state.SeededEntry?.FixtureRandomFreeAttackDrawInsertion == true ||
                  state.DeckRevealPending is not null ||
                  state.DrawInsertionPending?.SiteId ==
                    PrototypeControlledDrawInsertionV111
                        .PersonalHiveDazedSiteId ||
                  state.SeededEntry?.FixtureRandomizeHandCostsMaxExclusive is not null ||
                  state.SeededEntry?.FixtureSetRandomHandCardCost is not null ||
                  state.EnemyActionPending is not null ||
                  state.RandomPileCardPending is not null ||
                  state.Run.Player.ActiveRelics.Any(relic =>
                      relic.RelicId == "proto.relic.jeweled_mask") ||
                  state.Run.World?.Combat?.PendingChoice?
                      .ControlledDrawInsertion?.AfterShuffleRelic is not null ||
                  state.Run.Player.ActiveRelics.Any(relic =>
                      PrototypeContent.Relic(relic.RelicId)
                          .AfterShuffleGeneratedCardId is not null) ||
                  state.CardShortlistPending is not null ||
                  state.HandCostSelectionPending is not null ||
                  state.CostPending?.TargetCardInstanceId is not null
                    ? "nature-extended-fixture"
                    : "nature-two-site";

        object SemanticResult(
            SemanticPilotCombatV111 state, string handle,
            string? parent = null)
        {
            var measure = semanticProfiler.Enabled;
            var started = measure ? SemanticOperationProfiler.Timestamp() : 0L;
            var allocated = measure ? SemanticOperationProfiler.Allocated() : 0L;
            var fingerprint = state.Fingerprint;
            semanticProfiler.Record("sub.semantic_result_fingerprint", started, allocated);
            started = measure ? SemanticOperationProfiler.Timestamp() : 0L;
            allocated = measure ? SemanticOperationProfiler.Allocated() : 0L;
            var pending = DescribeSemanticPending(state);
            semanticProfiler.Record("sub.semantic_result_pending", started, allocated);
            started = measure ? SemanticOperationProfiler.Timestamp() : 0L;
            allocated = measure ? SemanticOperationProfiler.Allocated() : 0L;
            var chanceSites = SemanticChanceSites(state);
            semanticProfiler.Record("sub.semantic_result_chance_sites", started, allocated);
            return new
            {
                ok = true, schemaId = SemanticChanceSchemaId,
                semanticHandle = handle, parent,
                fingerprint,
                sourceOpeningFingerprint = state.SourceOpeningFingerprint,
                sourceRouteProvider = state.SourceRouteAudit?.ProviderId,
                sourceRouteActionCount = state.SourceRouteAudit?.ActionIds.Length ?? 0,
                sourceRouteEncounterId = state.SourceRouteAudit?.EncounterId,
                sourceRouteNativeEntry = state.SourceRouteAudit?.NativeEncounterEntry,
                sourceRouteProbabilityStatus = state.SourceRouteAudit?.ProbabilityStatus,
                boundary = state.CombatTerminal ? "terminal" :
                    state.AtPlayerBoundary ? "player" :
                    state.AtPlayerChoiceBoundary ? "player_choice" : "nature",
                pending,
                terminal = state.CombatTerminal,
                outcome = state.CombatOutcome,
                exitHp = state.CombatTerminal ? state.Run.Player.Hp : (int?)null,
                // Research-only terminal progress signal for H4 curriculum scoring.
                // Read the full enemy set so progress remains well-defined for
                // encounters with additional units; never infer HP from intents.
                terminalEnemyHp = state.CombatTerminal &&
                    state.CombatOutcome == "defeat"
                    ? state.Run.World?.DefeatRemainingEnemyHp
                    : null,
                chanceSites,
                controllerMode = SemanticControllerMode(state),
                nativePolicyProvider = state.UsesNativePolicyBaseline
                    ? SemanticNativePolicyAuditV111.NativeProviderId
                    : null,
                nativePolicyDecisionCount = state.NativePolicyAudit?.Decisions.Length ?? 0,
                nativePolicyShuffleCount = state.NativePolicyAudit?.Decisions.Count(
                    decision => decision.Request.SiteId ==
                        NativeFisherYatesV111.SiteId) ?? 0,
                otherChanceProvider = state.UsesNativePolicyBaseline
                    ? null
                    : state.OtherChance?.ProviderId ??
                        "engine-owned-deterministic-unverified-v1",
                otherChanceDecisionCount = state.OtherChance?.Decisions.Length ?? 0,
                rewardProvider = state.RewardAudit?.ProviderId,
                rewardDrawCount = state.RewardAudit?.Calls,
                sourceOpeningProvider = state.SeededEntry is null
                    ? "precommitted-fixture-v1"
                    : state.SeededEntry.ReplayOpeningDrawHooks
                        ? "engine-owned-seeded-source-shaped-draw-hooks-unverified-v1"
                        : "engine-owned-seeded-source-shaped-unverified-v1",
                probabilityStatus = "UNVERIFIED"
            };
        }

        object ControlledEventChoiceResult(
            RunState state, string handle, string? parent)
        {
            var continuation = state.World?.Event?.ControlledChoice;
            var rewardContinuation = state.ControlledEventRewardChoice;
            var stateFingerprint = CanonicalJson.Sha256(state);
            if (continuation is not null && rewardContinuation is not null)
                throw new InvalidDataException(
                    "A state cannot suspend both an event effect and its reward.");
            var isPending = continuation is not null
                || rewardContinuation is not null;
            var isPlayerBoundary = state.Phase == RunPhase.Event
                && state.World?.Event is
                {
                    EventId: PrototypeNativeStoneOfAllTime.EventId,
                    ChosenChoiceId: "push",
                    ControlledChoice: null,
                    PendingDeckChoice: not null
                };
            var frame = environment.Observe(state);
            var actions = !isPending
                ? frame.LegalActions
                : new PrototypeGameEngine().GetLegalActions(state)
                    .Select(action => new PrototypeAiAction(
                        PrototypeAiEnvironment.StableActionId(action),
                        action.Kind,
                        action.Payload.Clone()))
                    .ToArray();
            var history = state.ControlledEventChoiceHistory;
            RunState? parentState = null;
            if (parent is not null
                && states.TryGetValue(parent, out var foundParent))
                parentState = foundParent;
            var parentHistory = parentState?.ControlledEventChoiceHistory;
            var changedTraceIndex = -1;
            if (history is not null)
            {
                for (var index = 0; index < history.Length; index++)
                {
                    if (parentHistory is null
                        || index >= parentHistory.Length
                        || CanonicalJson.Sha256(history[index]) !=
                            CanonicalJson.Sha256(parentHistory[index]))
                        changedTraceIndex = index;
                }
            }
            var completedTrace = changedTraceIndex >= 0
                ? history![changedTraceIndex]
                : null;
            object? transcript = null;
            if (continuation is not null)
                transcript = new
                {
                    eventId = continuation.EventId,
                    choiceId = continuation.ChoiceId,
                    decisions = continuation.Decisions,
                    candidateLabels = continuation.DecisionCandidateLabels,
                    probabilityStatus = "UNVERIFIED"
                };
            if (rewardContinuation is not null)
                transcript = new
                {
                    eventId = rewardContinuation.EventId,
                    choiceId = rewardContinuation.ChoiceId,
                    decisions = rewardContinuation.Decisions,
                    candidateLabels = rewardContinuation.DecisionCandidateLabels,
                    probabilityStatus = "UNVERIFIED"
                };
            if (!isPending)
                transcript = completedTrace;
            var pendingLaw = continuation is not null
                ? ChanceLawEngineV111.QueryControlledEventChoice(state)
                : rewardContinuation is not null
                    ? ChanceLawEngineV111.QueryRandomIndex(
                        rewardContinuation.EventId,
                        rewardContinuation.PendingRequest)
                    : null;
            object? pending = continuation is not null
                ? (object)new
                {
                    kind = "event_random_choice",
                    eventId = continuation.EventId,
                    choiceId = continuation.ChoiceId,
                    boundaryFingerprint = CanonicalJson.Sha256(new
                    {
                        schemaId = ControlledEventChoiceEngineV111.SchemaId,
                        continuation.EventId,
                        continuation.ChoiceId,
                        continuation.NextEffectIndex,
                        request = continuation.PendingRequest,
                        candidateLabels = continuation.PendingCandidateLabels
                    }),
                    request = continuation.PendingRequest,
                    candidateLabels = continuation.PendingCandidateLabels,
                    chanceLaw = pendingLaw,
                    probabilityStatus = pendingLaw!.ProbabilityStatus
                }
                : rewardContinuation is not null
                    ? (object)new
                    {
                        kind = "event_reward_random_choice",
                        eventId = rewardContinuation.EventId,
                        choiceId = rewardContinuation.ChoiceId,
                        boundaryFingerprint = CanonicalJson.Sha256(new
                        {
                            schemaId = ControlledEventChoiceEngineV111.SchemaId,
                            rewardContinuation.EventId,
                            rewardContinuation.ChoiceId,
                            rewardContinuation.DecisionPrefixLength,
                            request = rewardContinuation.PendingRequest,
                            candidateLabels = rewardContinuation.PendingCandidateLabels
                        }),
                        request = rewardContinuation.PendingRequest,
                        candidateLabels = rewardContinuation.PendingCandidateLabels,
                        chanceLaw = pendingLaw,
                        probabilityStatus = pendingLaw!.ProbabilityStatus
                    }
                    : null;
            return new
            {
                ok = true,
                schemaId = ControlledEventChoiceEngineV111.SchemaId,
                stateHandle = handle,
                parent,
                fingerprint = stateFingerprint,
                boundary = isPending ? "nature" : isPlayerBoundary
                    ? "player" : "committed",
                pending,
                transcript,
                actions = actions.Select(ActionWire).ToArray()
            };
        }

        object ControlledRunActionResult(RunState state, string handle,
            string? parent)
        {
            var continuation = state.ControlledRunActionChoice;
            var stateFingerprint = CanonicalJson.Sha256(state);
            var boundaryFingerprint = continuation is null ? null :
                ChanceLawEngineV111.ControlledRunActionBoundaryFingerprint(state);
            var chanceLaw = continuation is null
                ? null : ChanceLawEngineV111.QueryControlledRunAction(state);
            var actions = continuation is null
                ? environment.Observe(state).LegalActions
                : new PrototypeGameEngine().GetLegalActions(state)
                    .Select(action => new PrototypeAiAction(
                        PrototypeAiEnvironment.StableActionId(action),
                        action.Kind,
                        action.Payload.Clone()))
                    .ToArray();
            object? transcript = continuation is not null
                ? new
                {
                    actionId = continuation.ActionId,
                    decisions = continuation.Decisions,
                    candidateLabels = continuation.DecisionCandidateLabels,
                    probabilityStatus = "UNVERIFIED"
                }
                : state.ControlledRunActionHistory is { Length: > 0 } history
                    ? history[^1]
                    : null;
            object? pending = continuation is null
                ? null
                : new
                {
                    kind = "run_action_random_choice",
                    eventId = chanceLaw!.EventId,
                    boundaryFingerprint,
                    actionId = continuation.ActionId,
                    request = continuation.PendingRequest,
                    candidateLabels = continuation.PendingCandidateLabels,
                    chanceLaw,
                    probabilityStatus = "UNVERIFIED"
                };
            return new
            {
                ok = true,
                schemaId = ControlledRunActionEngineV111.SchemaId,
                stateHandle = handle,
                parent,
                fingerprint = stateFingerprint,
                boundary = continuation is null ? "committed" : "nature",
                pending,
                transcript,
                actions = actions.Select(ActionWire).ToArray()
            };
        }

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
            var measure = semanticProfiler.Enabled &&
                activeOperation is not null &&
                activeOperation.StartsWith("semantic_", StringComparison.Ordinal) &&
                !activeOperation.StartsWith("semantic_perf_", StringComparison.Ordinal);
            var started = measure ? SemanticOperationProfiler.Timestamp() : 0L;
            var allocated = measure ? SemanticOperationProfiler.Allocated() : 0L;
            output.WriteLine(JsonSerializer.Serialize(
                response,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            output.Flush();
            if (measure)
            {
                semanticProfiler.Record("sub.semantic_wire_response", started, allocated);
                semanticProfiler.Record("op." + activeOperation, activeStarted,
                    activeAllocated);
            }
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
                activeOperation = operation;
                activeStarted = semanticProfiler.Enabled
                    ? SemanticOperationProfiler.Timestamp() : 0L;
                activeAllocated = semanticProfiler.Enabled
                    ? SemanticOperationProfiler.Allocated() : 0L;

                switch (operation)
                {
                    case "hello":
                    {
                        var sessionCoverage =
                            StochasticSiteRegistryV111.CreateSessionCoverage();
                        var stochasticCapabilities =
                            StochasticCapabilitiesV111.Create(sessionCoverage);
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
                            semanticChanceSchemaId = SemanticChanceSchemaId,
                            semanticNatureChoiceViewSchemaId =
                                SemanticNatureChoiceViewV111.CurrentSchemaId,
                            semanticNatureChoiceViewOperations = new[]
                            {
                                "nature_choice_observe"
                            },
                            simulationMechanicsComparisonSchemaId =
                                SimulationMechanicsComparisonV111.SchemaId,
                            simulationMechanicsComparisonOperations = new[]
                            {
                                "mechanics_compare"
                            },
                            simulationMechanicsComparisonInputs = new[]
                            {
                                "each side: <side>_snapshot or <side>_semantic_handle"
                            },
                            simulationMechanicsComparisonScope =
                                "provider-independent-mechanics-after-matched-semantic-outcomes",
                            semanticNatureChoiceViewSessionKinds =
                                stochasticCapabilities
                                    .SemanticNatureChoiceViewSessionKinds,
                            stochasticCapabilities,
                            stochasticSessionCoverage = sessionCoverage,
                            stochasticChanceLawQueryCoverage =
                                StochasticSiteRegistryV111
                                    .CreateChanceLawQueryCoverage(),
                            referenceNatureStateSnapshotSchemaId =
                                RunSnapshot.ReferenceNatureSchemaId,
                            natureRelicBagSnapshotSchemaId =
                                RunSnapshot.NatureRelicBagOrderSchemaId,
                            referenceNatureSessionSchemaId =
                                ReferenceNatureSessionV111.CurrentSchemaId,
                            referenceNatureRunActionSessionSchemaId =
                                ReferenceNatureRunActionSessionV111.CurrentSchemaId,
                            scriptedRunActionSessionSchemaId =
                                ScriptedRunActionSessionV111.CurrentSchemaId,
                            scriptedRunActionOperations =
                                ScriptedRunActionSessionV111.Operations,
                            scriptedRunSessionSchemaId =
                                ScriptedRunSessionV111.CurrentSchemaId,
                            scriptedRunSessionOperations =
                                ScriptedRunSessionV111.Operations,
                            scriptedRunSessionScope =
                                "whole-run-ordinary-legal-actions-with-typed-random-boundary-transcripts",
                            referenceNatureEventChoiceSessionSchemaId =
                                ReferenceNatureControlledEventChoiceSessionV111
                                    .CurrentSchemaId,
                            referenceNatureNativeEventGoldSessionSchemaId =
                                ReferenceNatureNativeEventGoldSessionV111
                                    .CurrentSchemaId,
                            referenceNatureNativeEventRelicSessionSchemaId =
                                ReferenceNatureNativeEventRelicSessionV111
                                    .CurrentSchemaId,
                            referenceNatureNativeShovelSessionSchemaId =
                                ReferenceNatureNativeShovelSessionV111
                                    .CurrentSchemaId,
                            referenceNatureProviderId =
                                ReferenceNatureV111.ProviderId,
                            controlledEventChoiceSchemaId =
                                ControlledEventChoiceEngineV111.SchemaId,
                            controlledEventChoiceScope =
                                "saved-event-effect-and-event-reward-frame",
                            controlledRunActionSchemaId =
                                ControlledRunActionEngineV111.SchemaId,
                            controlledRunActionScope =
                                "legacy-run-start-map-event-shop-treasure-entry-shovel-act-one-combat-entry-potion-and-supported-shop-purchase-effects",
                            controlledRunActionSiteGroups = new
                            {
                                runStartAndMapGeneration =
                                    StochasticSiteRegistryV111.RunActionSiteIds,
                                eventNodeEntry =
                                    StochasticSiteRegistryV111
                                        .RunActionEventEntrySiteIds,
                                legacyShopNodeGeneration =
                                    StochasticSiteRegistryV111
                                        .RunActionShopSiteIds,
                                shopPurchaseAcquisitionEffects =
                                    StochasticSiteRegistryV111
                                        .RunActionShopPurchaseSiteIds,
                                legacyTreasureNodeSelection =
                                    StochasticSiteRegistryV111
                                        .RunActionTreasureSiteIds,
                                shovelDig =
                                    StochasticSiteRegistryV111
                                        .RunActionShovelSiteIds,
                                legacyOvergrowthCombatEntry =
                                    StochasticSiteRegistryV111
                                        .RunActionOvergrowthCombatEntrySiteIds,
                                legacyUnderdocksCombatEntry =
                                    StochasticSiteRegistryV111
                                        .RunActionUnderdocksCombatEntrySiteIds
                            },
                            controlledRunActionMaximumInlineOutcomeActions =
                                ControlledRunActionEngineV111
                                    .MaximumInlineOutcomeActions,
                            controlledRunActionSites =
                                StochasticSiteRegistryV111
                                    .ControlledRunActionSiteIds,
                            controlledEventChoiceEnumerationSchemaId =
                                ControlledEventChoiceEnumeratorV111.SchemaId,
                            controlledEventChoiceEnumerationMaximumOutcomes =
                                ControlledEventChoiceEnumeratorV111
                                    .MaximumSupportedOutcomes,
                            controlledEventChoiceSites =
                                StochasticSiteRegistryV111
                                    .ControlledEventChoiceSiteIds,
                            semanticPersistentDeckActionSchemaId =
                                SemanticPlayerActionV111.SchemaId,
                            semanticPersistentDeckActionScope =
                                "draw-pile-neutral-single-player",
                            semanticPersistentDeckActionCardIds =
                                SemanticPlayerActionEngineV111.SupportedCardIds,
                            semanticNativePolicyComparatorId =
                                "engine-owned-native-v111-comparator-unverified-v1",
                            semanticSeededRouteEntryProviderId =
                                SemanticSourceRouteAuditV111.SourceRouteProviderId,
                            semanticPlayable = true,
                            semanticPlayableScope =
                                "seeded-source-route-or-post-committed-opening-fixture-restricted-Silent-combat",
                            semanticControlledSites = new[]
                            {
                                "enemy_intent", "draw_card"
                            },
                            semanticOtherChanceProvider =
                                "engine-owned-deterministic-unverified-v1",
                            semanticOtherChanceProviders = new[]
                            {
                                "engine-owned-deterministic-unverified-v1",
                                SemanticOtherChanceEngineV111.ProviderId
                            },
                            semanticRewardProvider =
                                SemanticRewardAuditV111.PrototypeProviderId,
                            semanticOpeningProvider = "precommitted-fixture-v1",
                            semanticSeededOpeningProvider =
                                "engine-owned-seeded-source-shaped-unverified-v1",
                            semanticSeededOpeningDrawHooksId =
                                "engine-owned-seeded-source-shaped-draw-hooks-unverified-v1",
                            semanticIntentSupportSchemaId =
                                SemanticIntentSupportV111.SchemaId,
                            semanticIntentEventSchemaId =
                                SemanticIntentEventV111.EventSchemaId,
                            semanticChanceEventContractId =
                                SemanticChanceEventContractV111.SchemaId,
                            semanticEndTurnIndexEventSchemaId =
                                SemanticEndTurnIndexEventV111.CurrentSchemaId,
                            semanticDrawChoiceSchemaId =
                                SemanticDeckStateV111.SchemaId,
                            semanticDeckRevealChoiceSchemaId =
                                SemanticDeckRevealDecisionV111.CurrentSchemaId,
                            semanticDrawHookChoiceSchemaId =
                                SemanticDrawHookChoiceEventV111.EventSchemaId,
                            semanticRandomPileCardChoiceSchemaId =
                                SemanticRandomPileCardEventV111.EventSchemaId,
                            semanticTargetChoiceSchemaId =
                                SemanticTargetChoiceV111.SchemaId,
                            semanticGeneratedCardChoiceSchemaId =
                                SemanticGenerationChoiceV111.SchemaId,
                            semanticPotionGenerationChoiceSchemaId =
                                SemanticPotionGenerationChoiceV111.SchemaId,
                            semanticCardTransformChoiceSchemaId =
                                SemanticCardTransformEventV111.EventSchemaId,
                            semanticCardSelectionChoiceSchemaId =
                                SemanticCardSelectionEventV111.EventSchemaId,
                            semanticCostChoiceSchemaId =
                                SemanticCostEventV111.EventSchemaId,
                            semanticHandCostSelectionChoiceSchemaId =
                                SemanticHandCostSelectionEventV111.EventSchemaId,
                            semanticCardShortlistChoiceSchemaId =
                                SemanticCardShortlistEventV111.EventSchemaId,
                            semanticPoolShuffleChoiceSchemaId =
                                SemanticPoolShuffleEventV111.EventSchemaId,
                            semanticDrawInsertionChoiceSchemaId =
                                SemanticDrawInsertionEventV111.EventSchemaId,
                            semanticEnumerationSchemaId =
                                SemanticNatureEnumeratorV111.SchemaId,
                            semanticEnumerationMaximumOutcomes =
                                SemanticNatureEnumeratorV111.MaximumSupportedOutcomes,
                            semanticExtendedControlledSites = new[]
                            {
                                "random_enemy_target", "enemy_action_chance",
                                "generated_card_pool",
                                "drawn_card_cost", "generated_card_pool_shuffle",
                                "generated_draw_pile_insertion",
                                "random_draw_attack_card_model",
                                "random_hand_card_energy_cost",
                                "random_hand_card_select",
                                "random_pile_selection_shortlist",
                                "random_pile_card", "random_auto_play_card",
                                "random_replay_card",
                                "random_rare_to_hand_shuffle",
                                "hand_return_shuffle",
                                "random_potion_generation", "random_card_transform"
                            },
                            semanticExtendedPlayerChoices = new[]
                            {
                                "select_cards", "card_drawn_hook_action"
                            },
                            semanticProbabilityStatus = "UNVERIFIED",
                            controlledChanceScope = "isolated-playable-combat-stage-continuations-v1",
                            controlledChanceSupportedSites =
                                StochasticSiteRegistryV111
                                    .ControlledPlayableSiteIds,
                            controlledChanceLimitations =
                                "Opt-in HP/intents, shuffle, turn intents, hand-draw recycle, " +
                                "random-enemy, Sly, and auto-play targeting, five generated-card " +
                                "effects, draw insertion, " +
                                "full-pool generation shuffles, " +
                                "hand-cost selection/rerolls, random pile shortlists, " +
                                "per-potion model generation, selected-card transforms, " +
                                "Bottled Potential hand-return shuffles, and their relic insertions; " +
                                "legacy enemy summon pools, summoned/hatchling HP and " +
                                "persistent-card theft during end turn; " +
                                "other chance sites fail closed; native entry and laws unverified.",
                            natureBoundaryKinds = new[] { "initial_hp", "intents" },
                            coverageSchemaId = V111Coverage.SchemaId,
                            snapshotSchemaId = RunSnapshot.SchemaId,
                            batchSchemaId = DeterministicBatch.SchemaId,
                            v111StateSchemaId = V111Build.StateSchema,
                            v111FidelityId = "v111-mechanics-prototype-rng-v1"
                        });
                        break;
                    }

                    case "manifest":
                        Write(new
                        {
                            requestId,
                            ok = true,
                            manifest = PrototypeCapabilities.Create()
                        });
                        break;

                    case "chance_law_query":
                    {
                        var siteId = RequiredString(request, "site_id");
                        if (!StochasticSiteRegistryV111
                                .IsChanceLawQuerySite(siteId))
                            throw new NotSupportedException(
                                $"Chance Law query does not support site '{siteId}'.");
                        if (siteId != NativeEncounterFormationFactoryV111
                                .FormationSiteId &&
                            (request.TryGetProperty(
                                 "native_encounter_entry", out _) ||
                             request.TryGetProperty(
                                 "total_floor", out _)))
                            throw new ArgumentException(
                                "native_encounter_entry and total_floor apply only to Native encounter-formation laws.");
                        if (OptionalString(request, "stream_id") is not null &&
                            siteId is not
                                ("potion_factory.rarity_roll.unit_float" or
                                 "relic_grab_bag.rarity.unit_float" or
                                 "potion.rarity_roll.ticket" or
                                 "potion.uncommon_model.index_draw" or
                                 "potion.model_within_rarity.index_draw" or
                                 "potion.uniform_model.index_draw" or
                                 "relic.acquisition.random_rare_card.model" or
                                 "relic.acquisition.random_potion.model" or
                                 "run.potion.fill_empty_slots.model" or
                                 PrototypeNativeOvergrowthMap
                                     .RestCountGaussianSiteId or
                                 PrototypeNativeOvergrowthMap
                                     .UnknownCountGaussianSiteId or
                                 PrototypeNativeLaterActMap
                                     .HiveRestCountGaussianSiteId or
                                 PrototypeNativeLaterActMap
                                     .UnknownCountGaussianSiteId or
                                 PrototypeGameEngine.ImprovementCardSiteId or
                                 StochasticSiteRegistryV111
                                     .UnownedRandomRelicSelectionSiteId or
                                 PrototypeNativePunchOff.RelicRewardSiteId or
                                 PrototypeNativePunchOff.PotionRewardSiteId))
                            throw new ArgumentException(
                                "stream_id is accepted only for PotionFactory, relic, legacy potion rarity/model laws, map Gaussian laws, and the legacy Mad Science Improvement route.");
                        int[]? priorIndices = null;
                        if (request.TryGetProperty(
                                "prior_indices", out var priorIndicesElement))
                        {
                            if (priorIndicesElement.ValueKind !=
                                JsonValueKind.Array)
                                throw new ArgumentException(
                                    "Request field 'prior_indices' must be an integer array.");
                            var parsedPriorIndices = new List<int>();
                            foreach (var element in
                                priorIndicesElement.EnumerateArray())
                            {
                                if (element.ValueKind != JsonValueKind.Number ||
                                    !element.TryGetInt32(out var selectedIndex) ||
                                    selectedIndex < 0)
                                    throw new ArgumentException(
                                        "Request field 'prior_indices' must contain nonnegative integers.");
                                parsedPriorIndices.Add(selectedIndex);
                            }
                            priorIndices = parsedPriorIndices.ToArray();
                        }
                        var priorVariantIndex = OptionalInt(
                            request, "prior_variant_index");
                        if (priorVariantIndex is not null && siteId !=
                            NativeDarvEventV111.RelicShuffleSiteId)
                            throw new ArgumentException(
                                "prior_variant_index applies only to Darv relic-shuffle queries.");
                        var suppliedOrdinal = OptionalInt(request, "ordinal");
                        if (suppliedOrdinal is null && siteId is not
                                (PrototypeNativeEndlessConveyor
                                    .DishWeightedTicketSiteId or
                                 PrototypeNativeEndlessConveyor
                                    .UpgradeCardSiteId or
                                 PrototypeNativeEndlessConveyor
                                    .ColorlessCardSiteId))
                            throw new ArgumentException(
                                "Request field 'ordinal' is required.");
                        var ordinal = suppliedOrdinal ?? 0;
                        ChanceLawV111 law;
                        if (ChanceLawEngineV111
                            .IsNativeMapGaussianIntQuerySite(siteId))
                        {
                            RejectChanceLawQueryParameters(
                                request, "owner_id", "stream_id");
                            var ownerId = OptionalString(
                                request, "owner_id") ??
                                throw new ArgumentException(
                                    "Native map Gaussian law requires owner_id.");
                            var streamId = OptionalString(
                                request, "stream_id") ??
                                throw new ArgumentException(
                                    "Native map Gaussian law requires stream_id.");
                            law = ChanceLawEngineV111
                                .QueryNativeMapGaussianInt(
                                    siteId, streamId, ownerId, ordinal);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == NativeEncounterFormationFactoryV111
                                .FormationSiteId &&
                            (request.TryGetProperty(
                                 "native_encounter_entry", out _) ||
                             request.TryGetProperty(
                                 "total_floor", out _)))
                        {
                            RejectChanceLawQueryParameters(
                                request, "native_encounter_entry",
                                "total_floor", "support_count");
                            var encounterEntry = OptionalString(
                                request, "native_encounter_entry") ??
                                throw new ArgumentException(
                                    "Native formation law requires native_encounter_entry.");
                            var totalFloor = OptionalInt(
                                request, "total_floor") ??
                                throw new ArgumentException(
                                    "Native formation law requires total_floor.");
                            var supportCount = OptionalInt(
                                request, "support_count") ??
                                throw new ArgumentException(
                                    "Native formation law requires support_count.");
                            law = ChanceLawEngineV111
                                .QueryNativeEncounterFormationIndex(
                                    encounterEntry, totalFloor,
                                    supportCount, ordinal);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (ChanceLawEngineV111
                                .IsNativeBoundedIntegerLawSite(siteId))
                        {
                            RejectChanceLawQueryParameters(
                                request, "owner_id", "support_count");
                            var ownerId = OptionalString(
                                request, "owner_id") ??
                                throw new ArgumentException(
                                    "Native bounded-index law requires owner_id.");
                            var supportCount = OptionalInt(
                                request, "support_count") ??
                                throw new ArgumentException(
                                    "Native bounded-index law requires support_count.");
                            law = ChanceLawEngineV111
                                .QueryNativeBoundedIntegerLawSite(
                                    siteId, ownerId, supportCount, ordinal);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == PrototypeGameEngine
                                .ImprovementCardSiteId &&
                            OptionalString(request, "stream_id") is { } improvementStreamId)
                        {
                            RejectChanceLawQueryParameters(
                                request, "stream_id", "owner_id",
                                "support_count");
                            if (improvementStreamId != "combat")
                                throw new ArgumentException(
                                    "The direct Mad Science Improvement law requires the legacy combat stream; omit stream_id to use the state-bound query.");
                            var improvementOwnerId = OptionalString(
                                request, "owner_id") ??
                                throw new ArgumentException(
                                    "Legacy bounded-index law requires owner_id.");
                            var improvementSupportCount = OptionalInt(
                                request, "support_count") ??
                                throw new ArgumentException(
                                    "Legacy bounded-index law requires support_count.");
                            law = ChanceLawEngineV111
                                .QueryLegacyCallerBoundIndex(
                                    siteId, improvementOwnerId, ordinal,
                                    improvementSupportCount);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (ChanceLawEngineV111
                                .IsLegacyCallerBoundIndexQuerySite(siteId) &&
                            siteId != PrototypeGameEngine
                                .ImprovementCardSiteId)
                        {
                            RejectChanceLawQueryParameters(
                                request, "owner_id", "support_count");
                            var legacyBoundedOwnerId = OptionalString(
                                request, "owner_id") ??
                                throw new ArgumentException(
                                    "Legacy bounded-index law requires owner_id.");
                            var legacyBoundedSupportCount = OptionalInt(
                                request, "support_count") ??
                                throw new ArgumentException(
                                    "Legacy bounded-index law requires support_count.");
                            law = ChanceLawEngineV111
                                .QueryLegacyCallerBoundIndex(
                                    siteId, legacyBoundedOwnerId, ordinal,
                                    legacyBoundedSupportCount);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == StochasticSiteRegistryV111
                                .UnownedRandomRelicSelectionSiteId)
                        {
                            RejectChanceLawQueryParameters(
                                request, "stream_id", "owner_id",
                                "support_count");
                            var unownedRelicStream = OptionalString(
                                request, "stream_id") ??
                                throw new ArgumentException(
                                    "Unowned-relic selection law requires stream_id (event, reward, or player.rewards).");
                            var unownedRelicOwner = OptionalString(
                                request, "owner_id") ??
                                throw new ArgumentException(
                                    "Unowned-relic selection law requires owner_id, including the current pool fingerprint.");
                            var unownedRelicSupport = OptionalInt(
                                request, "support_count") ??
                                throw new ArgumentException(
                                    "Unowned-relic selection law requires support_count for the current eligible pool.");
                            law = ChanceLawEngineV111
                                .QueryUnownedRelicSelectionIndex(
                                    unownedRelicStream, unownedRelicOwner,
                                    unownedRelicSupport, ordinal);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == NativePaelRewardsV111.LegionSkinSiteId)
                        {
                            var ownerId = OptionalString(request, "owner_id");
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (priorIndices is not null ||
                                priorVariantIndex is not null ||
                                ownerId is null ||
                                supportCount is not null && supportCount !=
                                    NativePaelRewardsV111.LegionSkins.Length ||
                                OptionalString(request, "action_id") is not null ||
                                OptionalString(request, "card_type") is not null ||
                                OptionalString(request, "rarity") is not null ||
                                OptionalString(request, "card_id") is not null ||
                                OptionalString(request, "potion_id") is not null ||
                                OptionalString(request, "relic_id") is not null ||
                                OptionalBool(request, "colorless_offer") ||
                                OptionalBool(request, "sale_reprice") ||
                                OptionalStringArray(request,
                                    "excluded_potion_ids") is not null ||
                                OptionalInt(request, "offer_ordinal") is not null ||
                                OptionalStringArray(request,
                                    "relic_candidate_ids") is not null ||
                                OptionalStringArray(request,
                                    "excluded_relic_ids") is not null)
                                throw new ArgumentException(
                                    "Pael's Legion queries accept state_handle, site_id, ordinal, owner_id and an optional support_count of four.");
                            law = ChanceLawEngineV111.QueryPaelsLegionSkin(
                                ownerId, ordinal);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "reward.combat_gold.index_draw")
                        {
                            RejectChanceLawQueryParameters(
                                request, "room", "ascension",
                                "gold_proportion", "support_count");
                            var roomText = OptionalString(request, "room") ??
                                throw new ArgumentException(
                                    "Combat gold law requires room.");
                            if (!Enum.TryParse<PrototypeRoomType>(
                                    roomText, ignoreCase: false,
                                    out var room) || !Enum.IsDefined(room))
                                throw new ArgumentException(
                                    "Combat gold room must be Combat, Elite, or Boss.");
                            var ascension = OptionalInt(request, "ascension") ??
                                throw new ArgumentException(
                                    "Combat gold law requires ascension.");
                            float? goldProportion = null;
                            if (request.TryGetProperty(
                                    "gold_proportion", out var proportion))
                            {
                                if (proportion.ValueKind != JsonValueKind.Number)
                                    throw new ArgumentException(
                                        "gold_proportion must be a JSON number.");
                                goldProportion = proportion.GetSingle();
                            }
                            law = ChanceLawEngineV111.QueryNativeCombatGold(
                                room, ascension, goldProportion, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Combat gold support_count must match the room, Ascension, and declared gold proportion.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "reward.potion_odds.unit_float")
                        {
                            RejectChanceLawQueryParameters(
                                request, "current_thousandths", "elite",
                                "forced", "support_count");
                            var currentThousandths = OptionalInt(
                                request, "current_thousandths") ??
                                throw new ArgumentException(
                                    "Potion odds law requires current_thousandths.");
                            var elite = RequiredBool(request, "elite");
                            var forced = RequiredBool(request, "forced");
                            law = ChanceLawEngineV111
                                .QueryNativePotionRewardOdds(
                                    currentThousandths, elite, forced,
                                    ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Potion odds support_count must match the declared pity, Elite, and force-hook state.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "reward.potion_odds.ticket")
                        {
                            RejectChanceLawQueryParameters(
                                request, "current_thousandths", "elite",
                                "forced", "support_count");
                            var currentThousandths = OptionalInt(
                                request, "current_thousandths") ??
                                throw new ArgumentException(
                                    "Legacy potion odds law requires current_thousandths.");
                            var elite = RequiredBool(request, "elite");
                            var forced = RequiredBool(request, "forced");
                            law = ChanceLawEngineV111
                                .QueryLegacyPotionRewardOddsTicket(
                                    currentThousandths, elite, forced,
                                    ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Legacy potion odds support_count must match the declared pity, Elite, and force-hook state.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId ==
                            "card_rarity.upgrade_roll.unit_float")
                        {
                            RejectChanceLawQueryParameters(
                                request, "card_id", "act", "ascension",
                                "support_count");
                            var cardUpgradeCardId = OptionalString(
                                request, "card_id") ??
                                throw new ArgumentException(
                                    "Card-upgrade law requires card_id.");
                            var act = OptionalInt(request, "act") ??
                                throw new ArgumentException(
                                    "Card-upgrade law requires act.");
                            var ascension = OptionalInt(request, "ascension") ??
                                throw new ArgumentException(
                                    "Card-upgrade law requires ascension.");
                            law = ChanceLawEngineV111
                                .QueryNativeEncounterCardUpgrade(
                                    cardUpgradeCardId, act, ascension,
                                    ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Card-upgrade support_count must match the declared card, act, and Ascension.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId ==
                            "card_rarity.encounter_model.index_draw")
                        {
                            RejectChanceLawQueryParameters(
                                request, "owner_id", "support_count");
                            var ownerId = OptionalString(
                                request, "owner_id") ??
                                throw new ArgumentException(
                                    "Encounter card-model index law requires owner_id.");
                            var supportCount = OptionalInt(
                                request, "support_count") ??
                                throw new ArgumentException(
                                    "Encounter card-model index law requires support_count.");
                            law = ChanceLawEngineV111
                                .QueryNativeEncounterCardModelIndex(
                                    ownerId, supportCount, ordinal);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId ==
                                "potion_factory.rarity_roll.unit_float" &&
                            (OptionalString(request, "owner_id") is not null ||
                             OptionalString(request, "stream_id") is not null))
                        {
                            RejectChanceLawQueryParameters(
                                request, "owner_id", "stream_id",
                                "support_count");
                            var potionFactoryOwner = OptionalString(
                                request, "owner_id") ??
                                throw new ArgumentException(
                                    "PotionFactory rarity law requires owner_id.");
                            var potionFactoryStream = OptionalString(
                                request, "stream_id") ??
                                throw new ArgumentException(
                                    "PotionFactory rarity law requires stream_id.");
                            var typedRequest = new RandomUnitFloatRequestV111(
                                siteId, potionFactoryStream,
                                potionFactoryOwner, ordinal);
                            law = ChanceLawEngineV111
                                .QueryNativePotionFactoryRarity(typedRequest);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "PotionFactory rarity support_count must match its declared source request.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId ==
                                "relic_grab_bag.rarity.unit_float" &&
                            OptionalString(request, "stream_id") is not null)
                        {
                            RejectChanceLawQueryParameters(
                                request, "owner_id", "stream_id",
                                "support_count");
                            var relicRarityOwner = OptionalString(
                                request, "owner_id") ??
                                throw new ArgumentException(
                                    "Native relic rarity law requires owner_id.");
                            var relicRarityStream = OptionalString(
                                request, "stream_id") ??
                                throw new ArgumentException(
                                    "Native relic rarity law requires stream_id.");
                            var typedRequest = new RandomUnitFloatRequestV111(
                                siteId, relicRarityStream,
                                relicRarityOwner, ordinal);
                            law = ChanceLawEngineV111
                                .QueryNativeRelicRarity(typedRequest);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Relic rarity support_count must match the declared source request.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == ExperimentalSilentThreeActRouteV111
                                .SecondBossIndexSiteId)
                        {
                            var firstBoss = OptionalString(
                                request, "first_boss") ??
                                throw new ArgumentException(
                                    "Glory second-boss law requires first_boss.");
                            var ascension = OptionalInt(request, "ascension") ??
                                throw new ArgumentException(
                                    "Glory second-boss law requires ascension.");
                            if (priorIndices is not null ||
                                priorVariantIndex is not null ||
                                OptionalInt(request, "support_count") is { } supportCount &&
                                    supportCount != 2 ||
                                OptionalString(request, "action_id") is not null ||
                                OptionalString(request, "event_id") is not null ||
                                OptionalInt(request, "act") is not null ||
                                OptionalString(request, "card_type") is not null ||
                                OptionalString(request, "rarity") is not null ||
                                OptionalString(request, "owner_id") is not null ||
                                OptionalString(request, "card_id") is not null ||
                                OptionalString(request, "potion_id") is not null ||
                                OptionalString(request, "relic_id") is not null ||
                                OptionalBool(request, "colorless_offer") ||
                                OptionalBool(request, "sale_reprice") ||
                                OptionalStringArray(request,
                                    "excluded_potion_ids") is not null ||
                                OptionalInt(request, "offer_ordinal") is not null ||
                                OptionalStringArray(request,
                                    "relic_candidate_ids") is not null ||
                                OptionalStringArray(request,
                                    "excluded_relic_ids") is not null)
                                throw new ArgumentException(
                                    "Glory second-boss queries accept state_handle, site_id, ordinal, first_boss, ascension and optional support_count two.");
                            law = ChanceLawEngineV111
                                .QueryLaterActSecondBoss(
                                    firstBoss, ascension, ordinal);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId is
                            ExperimentalSilentThreeActRouteV111.DarvHiveAssignmentSiteId or
                            ExperimentalSilentThreeActRouteV111.DarvGloryAssignmentSiteId or
                            ExperimentalSilentThreeActRouteV111.AncientAssignmentSiteId)
                        {
                            if (siteId == ExperimentalSilentThreeActRouteV111
                                    .DarvHiveAssignmentSiteId)
                            {
                                RejectChanceLawQueryParameters(
                                    request, "support_count");
                                var supportCount = OptionalInt(
                                    request, "support_count");
                                if (supportCount is not null && supportCount != 2)
                                    throw new ArgumentException(
                                        "Hive Darv assignment support_count must be two.");
                                law = ChanceLawEngineV111
                                    .QueryLaterActDarvHiveAssignment(ordinal);
                            }
                            else if (siteId == ExperimentalSilentThreeActRouteV111
                                         .DarvGloryAssignmentSiteId)
                            {
                                RejectChanceLawQueryParameters(
                                    request, "support_count",
                                    "hive_receives_darv");
                                var hiveReceivesDarv = RequiredBool(
                                    request, "hive_receives_darv");
                                var expectedSupport = hiveReceivesDarv ? 1 : 2;
                                var supportCount = OptionalInt(
                                    request, "support_count");
                                if (supportCount is not null &&
                                    supportCount != expectedSupport)
                                    throw new ArgumentException(
                                        $"Glory Darv assignment support_count must be {expectedSupport} for the declared Hive assignment.");
                                law = ChanceLawEngineV111
                                    .QueryLaterActDarvGloryAssignment(
                                        hiveReceivesDarv, ordinal);
                            }
                            else
                            {
                                RejectChanceLawQueryParameters(
                                    request, "support_count", "act",
                                    "include_darv");
                                var act = OptionalInt(request, "act") ??
                                    throw new ArgumentException(
                                        "Later-act Ancient law requires act 2 or 3.");
                                var includeDarv = RequiredBool(
                                    request, "include_darv");
                                var candidateIds =
                                    ExperimentalSilentThreeActRouteV111
                                        .AncientPool(act, includeDarv);
                                var supportCount = OptionalInt(
                                    request, "support_count");
                                if (supportCount is not null &&
                                    supportCount != candidateIds.Length)
                                    throw new ArgumentException(
                                        "Later-act Ancient support_count must match the declared act and Darv condition.");
                                law = ChanceLawEngineV111
                                    .QueryLaterActAncientAssignment(
                                        act, includeDarv, ordinal);
                            }
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == ExperimentalSilentThreeActRouteV111
                                .BossIndexSiteId)
                        {
                            var act = OptionalInt(request, "act") ??
                                throw new ArgumentException(
                                    "Later-act boss law requires act 2 or 3.");
                            var bossCount = ExperimentalSilentThreeActRouteV111
                                .Pool(act, PrototypeRoomType.Boss, weak: false)
                                .Length;
                            if (priorIndices is not null ||
                                priorVariantIndex is not null ||
                                OptionalInt(request, "support_count") is { } supportCount &&
                                    supportCount != bossCount ||
                                OptionalString(request, "action_id") is not null ||
                                OptionalString(request, "event_id") is not null ||
                                OptionalString(request, "first_boss") is not null ||
                                OptionalInt(request, "ascension") is not null ||
                                OptionalString(request, "card_type") is not null ||
                                OptionalString(request, "rarity") is not null ||
                                OptionalString(request, "owner_id") is not null ||
                                OptionalString(request, "card_id") is not null ||
                                OptionalString(request, "potion_id") is not null ||
                                OptionalString(request, "relic_id") is not null ||
                                OptionalBool(request, "colorless_offer") ||
                                OptionalBool(request, "sale_reprice") ||
                                OptionalStringArray(request,
                                    "excluded_potion_ids") is not null ||
                                OptionalInt(request, "offer_ordinal") is not null ||
                                OptionalStringArray(request,
                                    "relic_candidate_ids") is not null ||
                                OptionalStringArray(request,
                                    "excluded_relic_ids") is not null)
                                throw new ArgumentException(
                                    "Later-act boss queries accept state_handle, site_id, ordinal, act and optional support_count matching that act's boss pool.");
                            law = ChanceLawEngineV111
                                .QueryLaterActBossPool(act, ordinal);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == PrototypeNativeOvergrowthMap
                                .PathDeltaShuffleSiteId)
                        {
                            RejectChanceLawQueryParameters(
                                request, "owner_id", "support_count");
                            var ownerId = OptionalString(
                                request, "owner_id") ??
                                throw new ArgumentException(
                                    "Overgrowth path-delta law requires owner_id.");
                            var expectedSupport = ordinal % 2 == 0 ? 3 : 2;
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != expectedSupport)
                                throw new ArgumentException(
                                    $"Overgrowth path-delta support_count must be {expectedSupport} at this ordinal.");
                            law = ChanceLawEngineV111
                                .QueryOvergrowthPathDeltaSwap(ownerId, ordinal);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == PrototypeNativeOvergrowthMap
                                .RoomAssignmentShuffleSiteId)
                        {
                            RejectChanceLawQueryParameters(
                                request, "owner_id", "support_count");
                            var ownerId = OptionalString(
                                request, "owner_id") ??
                                throw new ArgumentException(
                                    "Overgrowth room-assignment law requires owner_id.");
                            var supportCount = OptionalInt(
                                request, "support_count") ??
                                throw new ArgumentException(
                                    "Overgrowth room-assignment law requires support_count.");
                            law = ChanceLawEngineV111
                                .QueryOvergrowthRoomAssignmentSwap(
                                    ownerId, ordinal, supportCount);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == PrototypeNativeLaterActMap
                                .RoomAssignmentShuffleSiteId)
                        {
                            RejectChanceLawQueryParameters(
                                request, "act", "support_count");
                            var act = OptionalInt(request, "act") ??
                                throw new ArgumentException(
                                    "Later-act room-assignment law requires act 2 or 3.");
                            var supportCount = OptionalInt(
                                request, "support_count") ??
                                throw new ArgumentException(
                                    "Later-act room-assignment law requires support_count.");
                            law = ChanceLawEngineV111
                                .QueryLaterActRoomAssignmentSwap(
                                    act, ordinal, supportCount);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == PrototypeNativeLaterActMap
                                .PathDeltaShuffleSiteId)
                        {
                            RejectChanceLawQueryParameters(
                                request, "act", "support_count");
                            var act = OptionalInt(request, "act") ??
                                throw new ArgumentException(
                                    "Later-act path-delta law requires act 2 or 3.");
                            var expectedSupport = ordinal % 2 == 0 ? 3 : 2;
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != expectedSupport)
                                throw new ArgumentException(
                                    $"Later-act path-delta support_count must be {expectedSupport} at this ordinal.");
                            law = ChanceLawEngineV111
                                .QueryLaterActPathDeltaSwap(act, ordinal);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == PrototypeNativeOvergrowthMap
                                .PathStartColumnSiteId)
                        {
                            RejectChanceLawQueryParameters(
                                request, "owner_id", "support_count");
                            var ownerId = OptionalString(
                                request, "owner_id") ??
                                throw new ArgumentException(
                                    "Overgrowth path-start law requires owner_id.");
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != PrototypeNativeOvergrowthMap.MapWidth)
                                throw new ArgumentException(
                                    "Overgrowth path-start support_count must be seven.");
                            law = ChanceLawEngineV111
                                .QueryOvergrowthPathStart(ownerId, ordinal);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == PrototypeNativeLaterActMap.PathStartSiteId)
                        {
                            RejectChanceLawQueryParameters(
                                request, "act", "support_count");
                            var act = OptionalInt(request, "act") ??
                                throw new ArgumentException(
                                    "Later-act path-start law requires act 2 or 3.");
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != PrototypeNativeLaterActMap.MapWidth)
                                throw new ArgumentException(
                                    "Later-act path-start support_count must be seven.");
                            law = ChanceLawEngineV111
                                .QueryLaterActPathStart(act, ordinal);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == PrototypeNativeLaterActMap
                                .GloryRestCountSiteId)
                        {
                            RejectChanceLawQueryParameters(
                                request, "support_count");
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null && supportCount != 2)
                                throw new ArgumentException(
                                    "Glory rest-count support_count must be two.");
                            law = ChanceLawEngineV111
                                .QueryLaterActGloryRestCount(ordinal);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId is "run.encounter.overgrowth_boss.index_draw"
                            or PrototypeNativeUnderdocks.BossEncounterIndexSiteId)
                        {
                            var bossCount = siteId ==
                                    "run.encounter.overgrowth_boss.index_draw"
                                ? PrototypeContent.OvergrowthBossEncounterPool.Length
                                : PrototypeNativeUnderdocks
                                    .NativeBossEncounterIds.Length;
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (priorIndices is not null ||
                                priorVariantIndex is not null ||
                                supportCount is not null && supportCount !=
                                    bossCount ||
                                OptionalString(request, "action_id") is not null ||
                                OptionalString(request, "event_id") is not null ||
                                OptionalString(request, "first_boss") is not null ||
                                OptionalInt(request, "ascension") is not null ||
                                OptionalInt(request, "act") is not null ||
                                OptionalString(request, "card_type") is not null ||
                                OptionalString(request, "rarity") is not null ||
                                OptionalString(request, "owner_id") is not null ||
                                OptionalString(request, "card_id") is not null ||
                                OptionalString(request, "potion_id") is not null ||
                                OptionalString(request, "relic_id") is not null ||
                                OptionalBool(request, "colorless_offer") ||
                                OptionalBool(request, "sale_reprice") ||
                                OptionalStringArray(request,
                                    "excluded_potion_ids") is not null ||
                                OptionalInt(request, "offer_ordinal") is not null ||
                                OptionalStringArray(request,
                                    "relic_candidate_ids") is not null ||
                                OptionalStringArray(request,
                                    "excluded_relic_ids") is not null)
                                throw new ArgumentException(
                                    "Act 1 boss queries accept state_handle, site_id, ordinal and an optional support_count matching the configured pool size.");
                            law = siteId ==
                                    "run.encounter.overgrowth_boss.index_draw"
                                ? ChanceLawEngineV111
                                    .QueryOvergrowthBossEncounter(ordinal)
                                : ChanceLawEngineV111
                                    .QueryUnderdocksBossEncounter(ordinal);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (StochasticSiteRegistryV111
                            .NeowOfferSessionSiteIds.Contains(
                                siteId, StringComparer.Ordinal))
                        {
                            if (OptionalString(request, "card_type") is not null ||
                                OptionalString(request, "rarity") is not null ||
                                OptionalString(request, "card_id") is not null ||
                                OptionalString(request, "potion_id") is not null ||
                                OptionalString(request, "relic_id") is not null ||
                                OptionalBool(request, "colorless_offer") ||
                                OptionalBool(request, "sale_reprice") ||
                                OptionalString(request, "owner_id") is not null ||
                                OptionalStringArray(request,
                                    "excluded_potion_ids") is not null ||
                                OptionalInt(request, "offer_ordinal") is not null ||
                                OptionalStringArray(request,
                                    "relic_candidate_ids") is not null ||
                                OptionalStringArray(request,
                                    "excluded_relic_ids") is not null)
                                throw new ArgumentException(
                                    "Neow offer queries accept only state_handle, ordinal and prior_indices.");
                            law = ChanceLawEngineV111.QueryNeowOffer(
                                RequireState(request), ordinal, priorIndices);
                            if (law.SiteId != siteId)
                                throw new ArgumentException(
                                    $"Neow offer ordinal {ordinal} expects site '{law.SiteId}', not '{siteId}'.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (StochasticSiteRegistryV111
                            .TrashHeapSessionSiteIds.Contains(
                                siteId, StringComparer.Ordinal))
                        {
                            if (priorIndices is { Length: > 0 } ||
                                OptionalString(request, "card_type") is not null ||
                                OptionalString(request, "rarity") is not null ||
                                OptionalString(request, "card_id") is not null ||
                                OptionalString(request, "potion_id") is not null ||
                                OptionalString(request, "relic_id") is not null ||
                                OptionalBool(request, "colorless_offer") ||
                                OptionalBool(request, "sale_reprice") ||
                                OptionalString(request, "owner_id") is not null ||
                                OptionalStringArray(request,
                                    "excluded_potion_ids") is not null ||
                                OptionalInt(request, "offer_ordinal") is not null ||
                                OptionalStringArray(request,
                                    "relic_candidate_ids") is not null ||
                                OptionalStringArray(request,
                                    "excluded_relic_ids") is not null)
                                throw new ArgumentException(
                                    "Trash Heap queries accept only state_handle, site_id and ordinal.");
                            law = ChanceLawEngineV111.QueryTrashHeap(
                                RequireState(request), siteId);
                            if (law.Ordinal != ordinal)
                                throw new ArgumentException(
                                    $"Trash Heap site '{siteId}' expects ordinal {law.Ordinal}, not {ordinal}.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (StochasticSiteRegistryV111
                            .TabletOfTruthSessionSiteIds.Contains(
                                siteId, StringComparer.Ordinal))
                        {
                            if (priorIndices is { Length: > 0 } ||
                                OptionalString(request, "card_type") is not null ||
                                OptionalString(request, "rarity") is not null ||
                                OptionalString(request, "card_id") is not null ||
                                OptionalString(request, "potion_id") is not null ||
                                OptionalString(request, "relic_id") is not null ||
                                OptionalBool(request, "colorless_offer") ||
                                OptionalBool(request, "sale_reprice") ||
                                OptionalString(request, "owner_id") is not null ||
                                OptionalStringArray(request,
                                    "excluded_potion_ids") is not null ||
                                OptionalInt(request, "offer_ordinal") is not null ||
                                OptionalStringArray(request,
                                    "relic_candidate_ids") is not null ||
                                OptionalStringArray(request,
                                    "excluded_relic_ids") is not null)
                                throw new ArgumentException(
                                    "Tablet of Truth queries accept only state_handle, site_id and page ordinal.");
                            law = ChanceLawEngineV111.QueryTabletOfTruth(
                                RequireState(request));
                            if (law.SiteId != siteId || law.Ordinal != ordinal)
                                throw new ArgumentException(
                                    $"Tablet of Truth page expects site '{law.SiteId}' and ordinal {law.Ordinal}.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (StochasticSiteRegistryV111
                            .TrialSessionSiteIds.Contains(
                                siteId, StringComparer.Ordinal))
                        {
                            if (priorIndices is { Length: > 0 } ||
                                OptionalString(request, "card_type") is not null ||
                                OptionalString(request, "rarity") is not null ||
                                OptionalString(request, "card_id") is not null ||
                                OptionalString(request, "potion_id") is not null ||
                                OptionalString(request, "relic_id") is not null ||
                                OptionalBool(request, "colorless_offer") ||
                                OptionalBool(request, "sale_reprice") ||
                                OptionalString(request, "owner_id") is not null ||
                                OptionalStringArray(request,
                                    "excluded_potion_ids") is not null ||
                                OptionalInt(request, "offer_ordinal") is not null ||
                                OptionalStringArray(request,
                                    "relic_candidate_ids") is not null ||
                                OptionalStringArray(request,
                                    "excluded_relic_ids") is not null)
                                throw new ArgumentException(
                                    "Trial queries accept only state_handle, site_id and ordinal.");
                            law = ChanceLawEngineV111.QueryTrial(
                                RequireState(request));
                            if (law.SiteId != siteId || law.Ordinal != ordinal)
                                throw new ArgumentException(
                                    $"Trial case query expects site '{law.SiteId}' and ordinal {law.Ordinal}.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == PrototypeNativeEndlessConveyor
                                .DishWeightedTicketSiteId ||
                            siteId == PrototypeNativeEndlessConveyor
                                .UpgradeCardSiteId ||
                            siteId == PrototypeNativeEndlessConveyor
                                .ColorlessCardSiteId)
                        {
                            if (priorIndices is not null ||
                                OptionalString(request, "card_type") is not null ||
                                OptionalString(request, "rarity") is not null ||
                                OptionalString(request, "card_id") is not null ||
                                OptionalString(request, "potion_id") is not null ||
                                OptionalString(request, "relic_id") is not null ||
                                OptionalBool(request, "colorless_offer") ||
                                OptionalBool(request, "sale_reprice") ||
                                OptionalString(request, "owner_id") is not null ||
                                OptionalStringArray(request,
                                    "excluded_potion_ids") is not null ||
                                OptionalInt(request, "offer_ordinal") is not null ||
                                OptionalStringArray(request,
                                    "relic_candidate_ids") is not null ||
                                OptionalStringArray(request,
                                    "excluded_relic_ids") is not null)
                                throw new ArgumentException(
                                    "Endless Conveyor queries accept only state_handle, site_id, ordinal and action_id.");
                            var actionId = OptionalString(request, "action_id")
                                ?? throw new ArgumentException(
                                    "Endless Conveyor query requires action_id.");
                            var source = RequireState(request);
                            law = siteId switch
                            {
                                PrototypeNativeEndlessConveyor
                                    .DishWeightedTicketSiteId =>
                                    ChanceLawEngineV111
                                        .QueryNativeEndlessConveyorDishEntry(
                                            source, actionId, suppliedOrdinal),
                                PrototypeNativeEndlessConveyor
                                    .UpgradeCardSiteId =>
                                    ChanceLawEngineV111
                                        .QueryNativeEndlessConveyorUpgrade(
                                            source, actionId, suppliedOrdinal),
                                PrototypeNativeEndlessConveyor
                                    .ColorlessCardSiteId =>
                                    ChanceLawEngineV111
                                        .QueryNativeEndlessConveyorColorlessCard(
                                            source, actionId, suppliedOrdinal),
                                _ => throw new InvalidOperationException(
                                    "Unknown Endless Conveyor Chance Law site.")
                            };
                            if (law.SiteId != siteId ||
                                suppliedOrdinal is { } requestedOrdinal &&
                                    law.Ordinal != requestedOrdinal)
                                throw new ArgumentException(
                                    $"Endless Conveyor query expects site '{law.SiteId}' and ordinal {law.Ordinal}.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (ChanceLawEngineV111
                            .NativePunchOffFightQuerySiteIds.Contains(
                                siteId, StringComparer.Ordinal))
                        {
                            if (OptionalString(request, "card_type") is not null ||
                                OptionalString(request, "rarity") is not null ||
                                OptionalString(request, "card_id") is not null ||
                                OptionalString(request, "potion_id") is not null ||
                                OptionalString(request, "relic_id") is not null ||
                                OptionalBool(request, "colorless_offer") ||
                                OptionalBool(request, "sale_reprice") ||
                                OptionalString(request, "owner_id") is not null ||
                                OptionalStringArray(request,
                                    "excluded_potion_ids") is not null ||
                                OptionalInt(request, "offer_ordinal") is not null ||
                                OptionalStringArray(request,
                                    "relic_candidate_ids") is not null ||
                                OptionalStringArray(request,
                                    "excluded_relic_ids") is not null ||
                                OptionalInt(request,
                                    "prior_variant_index") is not null)
                                throw new ArgumentException(
                                    "Punch Off fight queries accept only state_handle, site_id, ordinal, prior_indices and action_id.");
                            var actionId = OptionalString(request, "action_id")
                                ?? throw new ArgumentException(
                                    "Punch Off fight query requires action_id.");
                            law = ChanceLawEngineV111.QueryNativePunchOffFight(
                                RequireState(request), siteId, ordinal,
                                priorIndices, actionId);
                            if (law.SiteId != siteId || law.Ordinal != ordinal)
                                throw new ArgumentException(
                                    $"Punch Off fight query expects site '{law.SiteId}' and ordinal {law.Ordinal}.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (ChanceLawEngineV111
                            .NativePunchOffRewardQuerySiteIds.Contains(
                                siteId, StringComparer.Ordinal))
                        {
                            var punchOffStream = OptionalString(
                                request, "stream_id");
                            if (punchOffStream == "reward")
                            {
                                RejectChanceLawQueryParameters(
                                    request, "stream_id", "owner_id",
                                    "support_count");
                                var ownerId = OptionalString(
                                    request, "owner_id") ??
                                    throw new ArgumentException(
                                        "Legacy Punch Off reward law requires owner_id with its current pool fingerprint.");
                                var supportCount = OptionalInt(
                                    request, "support_count") ??
                                    throw new ArgumentException(
                                        "Legacy Punch Off reward law requires support_count for the current reward pool.");
                                law = ChanceLawEngineV111
                                    .QueryLegacyPunchOffReward(
                                        siteId, ownerId, supportCount, ordinal);
                                Write(new
                                {
                                    requestId,
                                    ok = true,
                                    chanceLawSchemaId =
                                        ChanceLawV111.CurrentSchemaId,
                                    law
                                });
                                break;
                            }
                            if (punchOffStream is not null &&
                                punchOffStream != "player.rewards")
                                throw new ArgumentException(
                                    "Punch Off reward stream_id must be reward or player.rewards.");
                            if (OptionalInt(request, "support_count") is not null)
                                throw new ArgumentException(
                                    "Native Punch Off reward laws derive support_count from the live reward plan.");
                            if (OptionalString(request, "card_type") is not null ||
                                OptionalString(request, "rarity") is not null ||
                                OptionalString(request, "card_id") is not null ||
                                OptionalString(request, "potion_id") is not null ||
                                OptionalString(request, "relic_id") is not null ||
                                OptionalBool(request, "colorless_offer") ||
                                OptionalBool(request, "sale_reprice") ||
                                OptionalString(request, "owner_id") is not null ||
                                OptionalStringArray(request,
                                    "excluded_potion_ids") is not null ||
                                OptionalInt(request, "offer_ordinal") is not null ||
                                OptionalStringArray(request,
                                    "relic_candidate_ids") is not null ||
                                OptionalStringArray(request,
                                    "excluded_relic_ids") is not null ||
                                OptionalInt(request,
                                    "prior_variant_index") is not null)
                                throw new ArgumentException(
                                    "Punch Off reward queries accept only state_handle, site_id, ordinal, prior_indices and action_id.");
                            var actionId = OptionalString(request, "action_id")
                                ?? throw new ArgumentException(
                                    "Punch Off reward query requires action_id.");
                            law = ChanceLawEngineV111.QueryNativePunchOffReward(
                                RequireState(request), siteId, ordinal,
                                priorIndices, actionId);
                            if (law.SiteId != siteId ||
                                law.Ordinal != ordinal)
                                throw new ArgumentException(
                                    $"Punch Off reward query expects site '{law.SiteId}' and ordinal {law.Ordinal}.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (StochasticSiteRegistryV111
                            .NativeEventShuffleSessionSiteIds.Contains(
                                siteId, StringComparer.Ordinal))
                        {
                            if (siteId == PrototypeGameEngine
                                .FishingRodUpgradeSiteId)
                            {
                                var supportCount = OptionalInt(
                                    request, "support_count");
                                var ownerId = OptionalString(
                                    request, "owner_id");
                                var fishingRun = RequireState(request);
                                var rodRelicId =
                                    PrototypeNativeOvergrowthEvents
                                        .NeowRelicId("FishingRod");
                                var ownerPrefix = $"relic:{rodRelicId}:slot:";
                                var slotText = ownerId is not null &&
                                    ownerId.StartsWith(ownerPrefix,
                                        StringComparison.Ordinal)
                                    ? ownerId[ownerPrefix.Length..]
                                    : string.Empty;
                                var streams = fishingRun.World?.Combat?.NativeRng?
                                    .RunStreams ?? fishingRun.NativeRunRng;
                                if (priorIndices is not null ||
                                    priorVariantIndex is not null ||
                                    OptionalString(request, "action_id") is not null ||
                                    OptionalString(request, "card_type") is not null ||
                                    OptionalString(request, "rarity") is not null ||
                                    OptionalString(request, "card_id") is not null ||
                                    OptionalString(request, "potion_id") is not null ||
                                    OptionalString(request, "relic_id") is not null ||
                                    OptionalBool(request, "colorless_offer") ||
                                    OptionalBool(request, "sale_reprice") ||
                                    OptionalStringArray(request,
                                        "excluded_potion_ids") is not null ||
                                    OptionalInt(request,
                                        "offer_ordinal") is not null ||
                                    OptionalStringArray(request,
                                        "relic_candidate_ids") is not null ||
                                    OptionalStringArray(request,
                                        "excluded_relic_ids") is not null ||
                                    supportCount is null or <= 0 ||
                                    supportCount.Value > fishingRun.Player.Deck.Length ||
                                    ownerId is null ||
                                    !int.TryParse(slotText,
                                        System.Globalization.NumberStyles.None,
                                        System.Globalization.CultureInfo.InvariantCulture,
                                        out var relicSlot) ||
                                    relicSlot >= fishingRun.Player.Relics.Length ||
                                    fishingRun.Player.Relics[relicSlot].RelicId !=
                                        rodRelicId ||
                                    fishingRun.Player.Relics[relicSlot].IsMelted ||
                                    fishingRun.Phase != RunPhase.Combat ||
                                    fishingRun.World?.ActiveRoom !=
                                        PrototypeRoomType.Combat ||
                                    fishingRun.World?.Combat is null ||
                                    streams is null || streams.Streams.Count(
                                        stream => stream.StreamId == "run.niche") != 1)
                                    throw new ArgumentException(
                                        "Fishing Rod law requires an active ordinary combat, a live Fishing Rod slot, a Native run.niche stream, and a support_count within the current deck size.");
                                law = ChanceLawEngineV111
                                    .QueryNativePersistentCombatEndUpgrade(
                                        new RandomIndexRequestV111(
                                            siteId, "run.niche", ownerId!,
                                            ordinal, Enumerable.Range(0,
                                                supportCount.Value).ToArray()));
                            }
                            else
                            {
                                if (OptionalString(request, "card_type") is not null ||
                                    OptionalString(request, "rarity") is not null ||
                                    OptionalString(request, "card_id") is not null ||
                                    OptionalString(request, "potion_id") is not null ||
                                    OptionalString(request, "relic_id") is not null ||
                                    OptionalBool(request, "colorless_offer") ||
                                    OptionalBool(request, "sale_reprice") ||
                                    OptionalString(request, "owner_id") is not null ||
                                    OptionalStringArray(request,
                                        "excluded_potion_ids") is not null ||
                                    OptionalInt(request, "offer_ordinal") is not null ||
                                    OptionalStringArray(request,
                                        "relic_candidate_ids") is not null ||
                                    OptionalStringArray(request,
                                        "excluded_relic_ids") is not null)
                                    throw new ArgumentException(
                                        "Native event shuffle queries accept only state_handle, site_id, ordinal, prior_indices and action_id.");
                                var actionId = OptionalString(request, "action_id");
                                law = siteId ==
                                    "event.war_historian.repy.potion_offer.index_draw"
                                    ? ChanceLawEngineV111
                                        .QueryNativeRepyPotionOffer(
                                            RequireState(request), ordinal,
                                            priorIndices, actionId)
                                    : ChanceLawEngineV111.QueryNativeEventShuffle(
                                        RequireState(request), siteId, ordinal,
                                        priorIndices, actionId);
                            }
                            if (law.SiteId != siteId ||
                                siteId !=
                                    "event.war_historian.repy.potion_offer.index_draw" &&
                                    law.Ordinal != ordinal)
                                throw new ArgumentException(
                                    $"Native event shuffle expects site '{law.SiteId}' and ordinal {law.Ordinal}.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (ChanceLawEngineV111.NativeEventEntryIndexQuerySiteIds
                            .Contains(siteId, StringComparer.Ordinal))
                        {
                            if (priorIndices is not null ||
                                OptionalString(request, "card_type") is not null ||
                                OptionalString(request, "rarity") is not null ||
                                OptionalString(request, "card_id") is not null ||
                                OptionalString(request, "potion_id") is not null ||
                                OptionalString(request, "relic_id") is not null ||
                                OptionalBool(request, "colorless_offer") ||
                                OptionalBool(request, "sale_reprice") ||
                                OptionalString(request, "owner_id") is not null ||
                                OptionalStringArray(request,
                                    "excluded_potion_ids") is not null ||
                                OptionalInt(request, "offer_ordinal") is not null ||
                                OptionalStringArray(request,
                                    "relic_candidate_ids") is not null ||
                                OptionalStringArray(request,
                                    "excluded_relic_ids") is not null)
                                throw new ArgumentException(
                                    "Native event entry-index queries accept only state_handle, site_id, ordinal, action_id and event_id.");
                            var actionId = OptionalString(request, "action_id")
                                ?? throw new ArgumentException(
                                    "Native event entry-index query requires action_id.");
                            var eventId = OptionalString(request, "event_id")
                                ?? throw new ArgumentException(
                                    "Native event entry-index query requires event_id as an explicit conditioning branch.");
                            var queryRun = RequireState(request);
                            law = siteId == ChanceLawEngineV111
                                .NativeStoneOfAllTimePotionSlotSiteId
                                ? ChanceLawEngineV111
                                    .QueryStoneOfAllTimePotionSlot(
                                        queryRun, siteId, ordinal, actionId, eventId)
                                : ChanceLawEngineV111
                                    .QueryNativeFutureOfPotionsCardType(
                                        queryRun, siteId, ordinal, actionId, eventId);
                            if (law.SiteId != siteId || law.Ordinal != ordinal)
                                throw new ArgumentException(
                                    $"Native event entry-index query expects site '{law.SiteId}' and ordinal {law.Ordinal}.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (ChanceLawEngineV111.NativeEventGoldQuerySiteIds
                            .Contains(siteId, StringComparer.Ordinal))
                        {
                            if (priorIndices is { Length: > 0 } ||
                                OptionalString(request, "card_type") is not null ||
                                OptionalString(request, "rarity") is not null ||
                                OptionalString(request, "card_id") is not null ||
                                OptionalString(request, "potion_id") is not null ||
                                OptionalString(request, "relic_id") is not null ||
                                OptionalBool(request, "colorless_offer") ||
                                OptionalBool(request, "sale_reprice") ||
                                OptionalString(request, "owner_id") is not null ||
                                OptionalStringArray(request,
                                    "excluded_potion_ids") is not null ||
                                OptionalInt(request, "offer_ordinal") is not null ||
                                OptionalStringArray(request,
                                    "relic_candidate_ids") is not null ||
                                OptionalStringArray(request,
                                    "excluded_relic_ids") is not null)
                                throw new ArgumentException(
                                    "Native event gold queries accept only state_handle, site_id, ordinal and action_id.");
                            var actionId = OptionalString(request, "action_id")
                                ?? throw new ArgumentException(
                                    "Native event gold query requires action_id.");
                            var eventId = OptionalString(request, "event_id")
                                ?? throw new ArgumentException(
                                    "Native event gold query requires event_id as an explicit conditioning branch.");
                            law = siteId == ChanceLawEngineV111
                                .NativeEventGoldUnitFloatSiteId
                                ? ChanceLawEngineV111
                                    .QueryNativeEventGoldUnitFloat(
                                        RequireState(request), siteId, ordinal,
                                        actionId, eventId)
                                : ChanceLawEngineV111.QueryNativeEventGoldIndex(
                                    RequireState(request), siteId, ordinal,
                                    actionId, eventId);
                            if (law.SiteId != siteId || law.Ordinal != ordinal)
                                throw new ArgumentException(
                                    $"Native event gold query expects site '{law.SiteId}' and ordinal {law.Ordinal}.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (ChanceLawEngineV111
                            .NativeEventNoEffectIndexQuerySiteIds.Contains(
                                siteId, StringComparer.Ordinal))
                        {
                            if (priorIndices is not null ||
                                OptionalString(request, "event_id") is not null ||
                                OptionalString(request, "card_type") is not null ||
                                OptionalString(request, "rarity") is not null ||
                                OptionalString(request, "card_id") is not null ||
                                OptionalString(request, "potion_id") is not null ||
                                OptionalString(request, "relic_id") is not null ||
                                OptionalBool(request, "colorless_offer") ||
                                OptionalBool(request, "sale_reprice") ||
                                OptionalString(request, "owner_id") is not null ||
                                OptionalStringArray(request,
                                    "excluded_potion_ids") is not null ||
                                OptionalInt(request, "offer_ordinal") is not null ||
                                OptionalStringArray(request,
                                    "relic_candidate_ids") is not null ||
                                OptionalStringArray(request,
                                    "excluded_relic_ids") is not null)
                                throw new ArgumentException(
                                    "Stone of All Time ticket queries accept only state_handle, site_id, ordinal and action_id.");
                            var actionId = OptionalString(request, "action_id")
                                ?? throw new ArgumentException(
                                    "Stone of All Time ticket query requires action_id.");
                            law = ChanceLawEngineV111
                                .QueryStoneOfAllTimeNoEffectIndex(
                                    RequireState(request), siteId, ordinal,
                                    actionId);
                            if (law.SiteId != siteId || law.Ordinal != ordinal)
                                throw new ArgumentException(
                                    $"Stone of All Time ticket query expects site '{law.SiteId}' and ordinal {law.Ordinal}.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (StochasticSiteRegistryV111
                            .ChanceLawQuerySiteIds.Contains(
                                siteId, StringComparer.Ordinal) &&
                            ChanceLawEngineV111.NativeEventCardPickQuerySiteIds
                                .Contains(siteId, StringComparer.Ordinal))
                        {
                            if (OptionalString(request, "card_type") is not null ||
                                OptionalString(request, "rarity") is not null ||
                                OptionalString(request, "card_id") is not null ||
                                OptionalString(request, "potion_id") is not null ||
                                OptionalString(request, "relic_id") is not null ||
                                OptionalBool(request, "colorless_offer") ||
                                OptionalBool(request, "sale_reprice") ||
                                OptionalString(request, "owner_id") is not null ||
                                OptionalStringArray(request,
                                    "excluded_potion_ids") is not null ||
                                OptionalInt(request, "offer_ordinal") is not null ||
                                OptionalStringArray(request,
                                    "relic_candidate_ids") is not null ||
                                OptionalStringArray(request,
                                    "excluded_relic_ids") is not null)
                                throw new ArgumentException(
                                    "Native event card-pick queries accept only state_handle, site_id, ordinal, prior_indices and action_id.");
                            law = ChanceLawEngineV111.QueryNativeEventCardPick(
                                RequireState(request), siteId, ordinal,
                                priorIndices,
                                OptionalString(request, "action_id"));
                            if (law.SiteId != siteId || law.Ordinal != ordinal)
                                throw new ArgumentException(
                                    $"Native event card-pick query expects site '{law.SiteId}' and ordinal {law.Ordinal}.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        var run = RequireState(request);
                        var cardType = OptionalString(request, "card_type");
                        var rarity = OptionalString(request, "rarity");
                        var cardId = OptionalString(request, "card_id");
                        var potionId = OptionalString(request, "potion_id");
                        var relicId = OptionalString(request, "relic_id");
                        var colorlessOffer = OptionalBool(
                            request, "colorless_offer");
                        var saleReprice = OptionalBool(
                            request, "sale_reprice");
                        var ownerIdParameter = OptionalString(
                            request, "owner_id");
                        var cardRaritySourceParameter = OptionalString(
                            request, "card_rarity_source");
                        var currentOffsetBasisPoints = OptionalInt(
                            request, "current_offset_basis_points");
                        var excludedPotionIds = OptionalStringArray(
                            request, "excluded_potion_ids");
                        var offerOrdinal = OptionalInt(
                            request, "offer_ordinal");
                        var relicCandidateIds = OptionalStringArray(
                            request, "relic_candidate_ids");
                        var excludedRelicIds = OptionalStringArray(
                            request, "excluded_relic_ids");
                        if (ChanceLawEngineV111.LegacyMerchantIndexQuerySiteIds
                            .Contains(siteId, StringComparer.Ordinal))
                        {
                            RejectChanceLawQueryParameters(
                                request, "owner_id", "support_count");
                            var ownerId = OptionalString(request, "owner_id")
                                ?? throw new ArgumentException(
                                    "Legacy merchant index query requires owner_id.");
                            var supportCount = OptionalInt(
                                request, "support_count") ??
                                throw new ArgumentException(
                                    "Legacy merchant index query requires support_count.");
                            law = ChanceLawEngineV111
                                .QueryLegacyMerchantIndexRequest(
                                    siteId, ownerId, ordinal, supportCount);
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "card_rarity.roll.ticket")
                        {
                            RejectChanceLawQueryParameters(
                                request, "card_rarity_source",
                                "current_offset_basis_points", "ascension",
                                "support_count");
                            var sourceText = OptionalString(
                                request, "card_rarity_source") ??
                                throw new ArgumentException(
                                    "Legacy card-rarity ticket law requires card_rarity_source.");
                            if (!Enum.TryParse<
                                    PrototypeNativeCardOddsSource>(
                                        sourceText, ignoreCase: false,
                                        out var source) ||
                                !Enum.IsDefined(source))
                                throw new ArgumentException(
                                    "card_rarity_source must be RegularEncounter, EliteEncounter, BossEncounter, or Shop.");
                            var currentOffset = OptionalInt(
                                request, "current_offset_basis_points") ??
                                throw new ArgumentException(
                                    "Legacy card-rarity ticket law requires current_offset_basis_points.");
                            var ascension = OptionalInt(request, "ascension") ??
                                throw new ArgumentException(
                                    "Legacy card-rarity ticket law requires ascension.");
                            law = ChanceLawEngineV111
                                .QueryLegacyCardRarityTicket(
                                    source, currentOffset, ascension,
                                    ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Card-rarity ticket support_count must match the declared source, offset, and Ascension.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "potion.rarity_roll.ticket")
                        {
                            RejectChanceLawQueryParameters(
                                request, "owner_id", "stream_id",
                                "support_count");
                            var ownerId = OptionalString(request, "owner_id")
                                ?? throw new ArgumentException(
                                    "Legacy potion-rarity ticket law requires owner_id.");
                            var streamId = OptionalString(request, "stream_id")
                                ?? throw new ArgumentException(
                                    "Legacy potion-rarity ticket law requires stream_id.");
                            law = ChanceLawEngineV111
                                .QueryLegacyPotionRarityTicket(
                                    streamId, ownerId, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Potion-rarity ticket support_count must be three.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "relic_grab_bag.rarity.ticket")
                        {
                            RejectChanceLawQueryParameters(
                                request, "owner_id", "merchant",
                                "support_count");
                            var ownerId = OptionalString(request, "owner_id")
                                ?? throw new ArgumentException(
                                    "Legacy relic-rarity ticket law requires owner_id.");
                            var merchant = RequiredBool(request, "merchant");
                            law = ChanceLawEngineV111
                                .QueryLegacyRelicRarityTicket(
                                    ownerId, merchant, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Relic-rarity ticket support_count must be three.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "card_rarity.upgrade_roll.ticket")
                        {
                            RejectChanceLawQueryParameters(
                                request, "card_id", "act", "ascension",
                                "support_count");
                            var upgradeTicketCardId = OptionalString(
                                request, "card_id") ??
                                throw new ArgumentException(
                                    "Legacy card-upgrade ticket law requires card_id.");
                            var act = OptionalInt(request, "act") ??
                                throw new ArgumentException(
                                    "Legacy card-upgrade ticket law requires act.");
                            var ascension = OptionalInt(request, "ascension") ??
                                throw new ArgumentException(
                                    "Legacy card-upgrade ticket law requires ascension.");
                            law = ChanceLawEngineV111
                                .QueryLegacyCardUpgradeTicket(
                                    upgradeTicketCardId, act, ascension,
                                    ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Card-upgrade ticket support_count must match the declared card, Act, and Ascension.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "reward.card_rarity.ticket")
                        {
                            RejectChanceLawQueryParameters(
                                request, "owner_id", "act",
                                "prior_card_ids", "support_count");
                            var groupOwnerId = OptionalString(
                                request, "owner_id") ??
                                throw new ArgumentException(
                                    "Reward card-rarity ticket law requires owner_id as the reward-group owner prefix.");
                            var rewardCardAct = OptionalInt(request, "act") ??
                                throw new ArgumentException(
                                    "Reward card-rarity ticket law requires act.");
                            var priorCardIds = OptionalStringArray(
                                request, "prior_card_ids") ??
                                throw new ArgumentException(
                                    "Reward card-rarity ticket law requires prior_card_ids (an empty array for the first pick).");
                            law = ChanceLawEngineV111
                                .QueryLegacyRewardCardRarityTicket(
                                    rewardCardAct, groupOwnerId,
                                    priorCardIds, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Reward card-rarity ticket support_count must match the declared Act and prior selections.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "shop.card.price.index_draw")
                        {
                            RejectChanceLawQueryParameters(
                                request, "card_id", "support_count");
                            var shopPriceCardId = OptionalString(
                                request, "card_id") ??
                                throw new ArgumentException(
                                    "Shop card-price law requires card_id.");
                            law = ChanceLawEngineV111
                                .QueryLegacyShopCardPrice(
                                    shopPriceCardId, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Shop card-price support_count must match the card's rarity-specific price range.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "shop.card_rarity.ticket")
                        {
                            RejectChanceLawQueryParameters(
                                request, "act", "card_type",
                                "prior_card_ids", "support_count");
                            var shopCardAct = OptionalInt(request, "act") ??
                                throw new ArgumentException(
                                    "Shop card-rarity law requires act.");
                            var shopCardTypeText = OptionalString(
                                request, "card_type") ??
                                throw new ArgumentException(
                                    "Shop card-rarity law requires card_type.");
                            if (!Enum.TryParse<PrototypeCardType>(
                                    shopCardTypeText, ignoreCase: false,
                                    out var shopCardType) ||
                                !Enum.IsDefined(shopCardType) ||
                                shopCardType is not (
                                    PrototypeCardType.Attack or
                                    PrototypeCardType.Skill or
                                    PrototypeCardType.Power))
                                throw new ArgumentException(
                                    "card_type must be Attack, Skill, or Power.");
                            var priorShopCardIds = OptionalStringArray(
                                request, "prior_card_ids") ??
                                throw new ArgumentException(
                                    "Shop card-rarity law requires prior_card_ids (an empty array for the first offer).");
                            law = ChanceLawEngineV111
                                .QueryLegacyShopCardRarityTicket(
                                    shopCardAct, shopCardType,
                                    priorShopCardIds, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Shop card-rarity support_count must match the Act, card type, and prior offers.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "shop.card_model.index_draw")
                        {
                            RejectChanceLawQueryParameters(
                                request, "act", "card_type", "rarity",
                                "prior_card_ids", "support_count");
                            var shopCardAct = OptionalInt(request, "act") ??
                                throw new ArgumentException(
                                    "Shop card-model law requires act.");
                            var shopCardTypeText = OptionalString(
                                request, "card_type") ??
                                throw new ArgumentException(
                                    "Shop card-model law requires card_type.");
                            if (!Enum.TryParse<PrototypeCardType>(
                                    shopCardTypeText, ignoreCase: false,
                                    out var shopCardType) ||
                                !Enum.IsDefined(shopCardType) ||
                                shopCardType is not (
                                    PrototypeCardType.Attack or
                                    PrototypeCardType.Skill or
                                    PrototypeCardType.Power))
                                throw new ArgumentException(
                                    "card_type must be Attack, Skill, or Power.");
                            var shopCardRarityText = OptionalString(
                                request, "rarity") ??
                                throw new ArgumentException(
                                    "Shop card-model law requires rarity.");
                            if (!Enum.TryParse<PrototypeCardRarity>(
                                    shopCardRarityText, ignoreCase: false,
                                    out var shopCardRarity) ||
                                !Enum.IsDefined(shopCardRarity))
                                throw new ArgumentException(
                                    "rarity must be Common, Uncommon, or Rare.");
                            var priorShopCardIds = OptionalStringArray(
                                request, "prior_card_ids") ??
                                throw new ArgumentException(
                                    "Shop card-model law requires prior_card_ids (an empty array for the first offer).");
                            law = ChanceLawEngineV111
                                .QueryLegacyShopCardModel(
                                    shopCardAct, shopCardType,
                                    shopCardRarity, priorShopCardIds, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Shop card-model support_count must match the Act, card type, rarity, and prior offers.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "shop.potion_offer.price.index_draw")
                        {
                            RejectChanceLawQueryParameters(
                                request, "act", "floor", "offer_ordinal",
                                "potion_id", "support_count");
                            var shopPriceAct = OptionalInt(request, "act") ??
                                throw new ArgumentException(
                                    "Shop potion-price law requires act.");
                            var shopPriceFloor = OptionalInt(request, "floor") ??
                                throw new ArgumentException(
                                    "Shop potion-price law requires floor.");
                            var potionOfferOrdinal = OptionalInt(
                                request, "offer_ordinal") ??
                                throw new ArgumentException(
                                    "Shop potion-price law requires offer_ordinal.");
                            var shopPricePotionId = OptionalString(
                                request, "potion_id") ??
                                throw new ArgumentException(
                                    "Shop potion-price law requires potion_id.");
                            law = ChanceLawEngineV111
                                .QueryLegacyShopPotionPrice(
                                    shopPriceAct, shopPriceFloor,
                                    potionOfferOrdinal, shopPricePotionId,
                                    ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Shop potion-price support_count must be twenty-one.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "shop.relic_offer.price.index_draw")
                        {
                            RejectChanceLawQueryParameters(
                                request, "act", "floor", "offer_ordinal",
                                "relic_id", "support_count");
                            var shopPriceAct = OptionalInt(request, "act") ??
                                throw new ArgumentException(
                                    "Shop relic-price law requires act.");
                            var shopPriceFloor = OptionalInt(request, "floor") ??
                                throw new ArgumentException(
                                    "Shop relic-price law requires floor.");
                            var relicOfferOrdinal = OptionalInt(
                                request, "offer_ordinal") ??
                                throw new ArgumentException(
                                    "Shop relic-price law requires offer_ordinal.");
                            var shopPriceRelicId = OptionalString(
                                request, "relic_id") ??
                                throw new ArgumentException(
                                    "Shop relic-price law requires relic_id.");
                            law = ChanceLawEngineV111
                                .QueryLegacyShopRelicPrice(
                                    shopPriceAct, shopPriceFloor,
                                    relicOfferOrdinal, shopPriceRelicId,
                                    ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Shop relic-price support_count must be forty-one.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "reward.card_model.index_draw")
                        {
                            RejectChanceLawQueryParameters(
                                request, "owner_id", "act", "rarity",
                                "prior_card_ids", "support_count");
                            var cardModelOwnerId = OptionalString(
                                request, "owner_id") ??
                                throw new ArgumentException(
                                    "Reward card-model law requires owner_id as the reward-group owner prefix.");
                            var cardModelAct = OptionalInt(request, "act") ??
                                throw new ArgumentException(
                                    "Reward card-model law requires act.");
                            var cardModelRarityText = OptionalString(
                                request, "rarity") ??
                                throw new ArgumentException(
                                    "Reward card-model law requires rarity.");
                            if (!Enum.TryParse<PrototypeCardRarity>(
                                    cardModelRarityText, ignoreCase: false,
                                    out var cardModelRarity) ||
                                !Enum.IsDefined(cardModelRarity))
                                throw new ArgumentException(
                                    "rarity must be Common, Uncommon, or Rare.");
                            var priorCardIds = OptionalStringArray(
                                request, "prior_card_ids") ??
                                throw new ArgumentException(
                                    "Reward card-model law requires prior_card_ids (an empty array for the first pick).");
                            law = ChanceLawEngineV111
                                .QueryLegacyRewardCardModel(
                                    cardModelAct, cardModelOwnerId,
                                    cardModelRarity, priorCardIds, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Reward card-model support_count must match the declared rarity and prior selections.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId ==
                            "reward.card_transform.replacement.index_draw")
                        {
                            RejectChanceLawQueryParameters(
                                request, "card_instance_id", "card_id",
                                "support_count");
                            var transformCardInstanceId = RequiredLong(
                                request, "card_instance_id");
                            var transformOriginalCardId = OptionalString(
                                request, "card_id") ??
                                throw new ArgumentException(
                                    "Reward card-transform law requires card_id as the original model.");
                            law = ChanceLawEngineV111
                                .QueryLegacyRewardCardTransform(
                                    transformCardInstanceId,
                                    transformOriginalCardId, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Reward card-transform support_count must match the reward pool excluding the original model.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "reward.random_potion.drop.ticket")
                        {
                            RejectChanceLawQueryParameters(
                                request, "act", "floor", "room",
                                "can_acquire_potion", "support_count");
                            var rewardPotionAct = OptionalInt(
                                request, "act") ??
                                throw new ArgumentException(
                                    "Legacy potion-drop ticket law requires act.");
                            var rewardPotionFloor = OptionalInt(
                                request, "floor") ??
                                throw new ArgumentException(
                                    "Legacy potion-drop ticket law requires floor.");
                            var rewardPotionRoomText = OptionalString(
                                request, "room") ??
                                throw new ArgumentException(
                                    "Legacy potion-drop ticket law requires room.");
                            if (!Enum.TryParse<PrototypeRoomType>(
                                    rewardPotionRoomText, ignoreCase: false,
                                    out var rewardPotionRoom) ||
                                !Enum.IsDefined(rewardPotionRoom))
                                throw new ArgumentException(
                                    "room must be Combat, Elite, or Boss.");
                            var canAcquirePotion = RequiredBool(
                                request, "can_acquire_potion");
                            law = ChanceLawEngineV111
                                .QueryLegacyPotionDropTicket(
                                    rewardPotionAct, rewardPotionFloor,
                                    rewardPotionRoom, canAcquirePotion,
                                    ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Legacy potion-drop ticket support_count must be two.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "reward.random_potion.model")
                        {
                            RejectChanceLawQueryParameters(
                                request, "act", "floor", "room",
                                "can_acquire_potion", "drop_offered",
                                "support_count");
                            var modelAct = OptionalInt(request, "act") ??
                                throw new ArgumentException(
                                    "Legacy reward-potion model law requires act.");
                            var modelFloor = OptionalInt(request, "floor") ??
                                throw new ArgumentException(
                                    "Legacy reward-potion model law requires floor.");
                            var modelRoomText = OptionalString(
                                request, "room") ??
                                throw new ArgumentException(
                                    "Legacy reward-potion model law requires room.");
                            if (!Enum.TryParse<PrototypeRoomType>(
                                    modelRoomText, ignoreCase: false,
                                    out var modelRoom) ||
                                !Enum.IsDefined(modelRoom))
                                throw new ArgumentException(
                                    "room must be Combat, Elite, or Boss.");
                            var canAcquirePotion = RequiredBool(
                                request, "can_acquire_potion");
                            var dropOffered = RequiredBool(
                                request, "drop_offered");
                            law = ChanceLawEngineV111
                                .QueryLegacyRewardPotionModel(
                                    modelAct, modelFloor, modelRoom,
                                    canAcquirePotion, dropOffered, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Legacy reward-potion model support_count must match the ordered prototype potion pool.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "potion.uncommon_model.index_draw")
                        {
                            RejectChanceLawQueryParameters(
                                request, "owner_id", "stream_id",
                                "support_count");
                            var ownerPrefix = OptionalString(
                                request, "owner_id") ??
                                throw new ArgumentException(
                                    "Uncommon-potion model law requires owner_id as the request owner prefix.");
                            var streamId = OptionalString(
                                request, "stream_id") ??
                                throw new ArgumentException(
                                    "Uncommon-potion model law requires stream_id.");
                            law = ChanceLawEngineV111
                                .QueryLegacyUncommonPotionModel(
                                    streamId, ownerPrefix, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Uncommon-potion model support_count must match the implemented source-ordered pool.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId ==
                            "potion.model_within_rarity.index_draw")
                        {
                            RejectChanceLawQueryParameters(
                                request, "rarity", "owner_id", "stream_id",
                                "excluded_potion_ids", "support_count");
                            var potionRarityText = OptionalString(
                                request, "rarity") ??
                                throw new ArgumentException(
                                    "Potion model law requires rarity.");
                            if (!Enum.TryParse<PrototypeNativePotionRarity>(
                                    potionRarityText, ignoreCase: false,
                                    out var potionRarity) ||
                                !Enum.IsDefined(potionRarity))
                                throw new ArgumentException(
                                    "rarity must be Common, Uncommon, or Rare.");
                            var potionModelOwnerPrefix = OptionalString(
                                request, "owner_id") ??
                                throw new ArgumentException(
                                    "Potion model law requires owner_id as the request owner prefix.");
                            var potionModelStreamId = OptionalString(
                                request, "stream_id") ??
                                throw new ArgumentException(
                                    "Potion model law requires stream_id.");
                            law = ChanceLawEngineV111
                                .QueryLegacyPotionModelWithinRarity(
                                    potionRarity, potionModelStreamId,
                                    potionModelOwnerPrefix,
                                    excludedPotionIds ?? [], ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Potion model support_count must match the declared rarity and exclusions.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "potion.uniform_model.index_draw")
                        {
                            RejectChanceLawQueryParameters(
                                request, "owner_id", "stream_id",
                                "support_count");
                            var uniformPotionOwnerPrefix = OptionalString(
                                request, "owner_id") ??
                                throw new ArgumentException(
                                    "Uniform potion model law requires owner_id as the request owner prefix.");
                            var uniformPotionStreamId = OptionalString(
                                request, "stream_id") ??
                                throw new ArgumentException(
                                    "Uniform potion model law requires stream_id.");
                            law = ChanceLawEngineV111
                                .QueryLegacyUniformPotionModel(
                                    uniformPotionStreamId,
                                    uniformPotionOwnerPrefix, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Uniform potion model support_count must match the complete source-ordered pool.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "event.reward.random_potion.model")
                        {
                            RejectChanceLawQueryParameters(
                                request, "event_id", "action_id",
                                "support_count");
                            var eventId = OptionalString(request, "event_id") ??
                                throw new ArgumentException(
                                    "Event reward-potion model law requires event_id.");
                            var choiceId = OptionalString(
                                request, "action_id") ??
                                throw new ArgumentException(
                                    "Event reward-potion model law requires action_id as the selected choice ID.");
                            law = ChanceLawEngineV111
                                .QueryLegacyEventRewardPotionModel(
                                    eventId, choiceId, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Event reward-potion model support_count must match the prototype event potion pool.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "event.reward.random_rare_card.model")
                        {
                            RejectChanceLawQueryParameters(
                                request, "event_id", "action_id",
                                "support_count");
                            var eventId = OptionalString(request, "event_id") ??
                                throw new ArgumentException(
                                    "Event Rare-card model law requires event_id.");
                            var choiceId = OptionalString(
                                request, "action_id") ??
                                throw new ArgumentException(
                                    "Event Rare-card model law requires action_id as the selected choice ID.");
                            law = ChanceLawEngineV111
                                .QueryLegacyEventRewardRareCardModel(
                                    eventId, choiceId, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Event Rare-card model support_count must match the prototype Rare-card pool.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId ==
                            "relic.acquisition.random_rare_card.model")
                        {
                            RejectChanceLawQueryParameters(
                                request, "stream_id", "owner_id",
                                "support_count");
                            var acquisitionStream = OptionalString(
                                request, "stream_id") ?? "reward";
                            if (acquisitionStream == "reward")
                            {
                                if (OptionalString(request, "owner_id") is not null)
                                    throw new ArgumentException(
                                        "Legacy Rare-card relic acquisition derives its owner from the prototype pool.");
                                law = ChanceLawEngineV111
                                    .QueryLegacyRelicAcquisitionRareCardModel(
                                        ordinal);
                                var supportCount = OptionalInt(
                                    request, "support_count");
                                if (supportCount is not null &&
                                    supportCount != law.Outcomes.Length)
                                    throw new ArgumentException(
                                        "Legacy Rare-card relic acquisition support_count must match the prototype Rare-card pool.");
                            }
                            else if (acquisitionStream == "player.rewards")
                            {
                                var ownerId = OptionalString(
                                    request, "owner_id") ??
                                    throw new ArgumentException(
                                        "Native Rare-card relic acquisition law requires owner_id.");
                                var supportCount = OptionalInt(
                                    request, "support_count") ??
                                    throw new ArgumentException(
                                        "Native Rare-card relic acquisition law requires support_count.");
                                law = ChanceLawEngineV111
                                    .QueryNativePlayerModelIndex(
                                        siteId, ownerId, supportCount, ordinal);
                            }
                            else
                            {
                                throw new ArgumentException(
                                    "Rare-card relic acquisition stream_id must be reward or player.rewards.");
                            }
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId ==
                            "relic.acquisition.random_potion.model")
                        {
                            RejectChanceLawQueryParameters(
                                request, "stream_id", "owner_id", "slot_index",
                                "slot_empty", "support_count");
                            var slotIndex = OptionalInt(request, "slot_index") ??
                                throw new ArgumentException(
                                    "Random-potion relic acquisition law requires slot_index for the first open potion slot.");
                            if (OptionalBool(request, "slot_empty") != true)
                                throw new ArgumentException(
                                    "Random-potion relic acquisition law requires slot_empty=true.");
                            var acquisitionStream = OptionalString(
                                request, "stream_id") ?? "event";
                            if (acquisitionStream == "event")
                            {
                                if (OptionalString(request, "owner_id") is not null)
                                    throw new ArgumentException(
                                        "Legacy potion relic acquisition derives its owner from the open slot and prototype pool.");
                                law = ChanceLawEngineV111
                                    .QueryLegacyRelicAcquisitionPotionModel(
                                        slotIndex, ordinal);
                                var supportCount = OptionalInt(
                                    request, "support_count");
                                if (supportCount is not null &&
                                    supportCount != law.Outcomes.Length)
                                    throw new ArgumentException(
                                        "Legacy potion relic acquisition support_count must match the ordered prototype potion pool.");
                            }
                            else if (acquisitionStream == "player.rewards")
                            {
                                var ownerId = OptionalString(
                                    request, "owner_id") ??
                                    throw new ArgumentException(
                                        "Native potion relic acquisition law requires owner_id.");
                                var supportCount = OptionalInt(
                                    request, "support_count") ??
                                    throw new ArgumentException(
                                        "Native potion relic acquisition law requires support_count.");
                                law = ChanceLawEngineV111
                                    .QueryNativePlayerModelIndex(
                                        siteId, ownerId, supportCount, ordinal);
                            }
                            else
                            {
                                throw new ArgumentException(
                                    "Potion relic acquisition stream_id must be event or player.rewards.");
                            }
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "run.potion.fill_empty_slots.model")
                        {
                            RejectChanceLawQueryParameters(
                                request, "stream_id", "owner_id", "slot_index",
                                "slot_empty", "can_acquire_potion",
                                "support_count");
                            var slotIndex = OptionalInt(request, "slot_index") ??
                                throw new ArgumentException(
                                    "Run potion-fill law requires slot_index for the current potion slot.");
                            if (OptionalBool(request, "can_acquire_potion") != true ||
                                OptionalBool(request, "slot_empty") != true)
                                throw new ArgumentException(
                                    "Run potion-fill law requires can_acquire_potion=true and slot_empty=true.");
                            var fillStream = OptionalString(
                                request, "stream_id") ?? "reward";
                            if (fillStream == "reward")
                            {
                                if (OptionalString(request, "owner_id") is not null)
                                    throw new ArgumentException(
                                        "Legacy Run potion fill derives its owner from the slot and prototype pool.");
                                law = ChanceLawEngineV111
                                    .QueryLegacyRunPotionFillSlotModel(
                                        slotIndex, canAcquirePotion: true,
                                        slotIsEmpty: true, ordinal);
                                var supportCount = OptionalInt(
                                    request, "support_count");
                                if (supportCount is not null &&
                                    supportCount != law.Outcomes.Length)
                                    throw new ArgumentException(
                                        "Legacy Run potion-fill support_count must match the ordered prototype potion pool.");
                            }
                            else if (fillStream == "player.rewards")
                            {
                                var ownerId = OptionalString(
                                    request, "owner_id") ??
                                    throw new ArgumentException(
                                        "Native Run potion-fill law requires owner_id.");
                                var supportCount = OptionalInt(
                                    request, "support_count") ??
                                    throw new ArgumentException(
                                        "Native Run potion-fill law requires support_count.");
                                law = ChanceLawEngineV111
                                    .QueryNativePlayerModelIndex(
                                        siteId, ownerId, supportCount, ordinal);
                            }
                            else
                            {
                                throw new ArgumentException(
                                    "Run potion-fill stream_id must be reward or player.rewards.");
                            }
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId ==
                            "event.reward.random_modifier_curse.index_draw")
                        {
                            RejectChanceLawQueryParameters(
                                request, "event_id", "support_count");
                            var modifierCurseEventId = OptionalString(
                                request, "event_id") ??
                                throw new ArgumentException(
                                    "Event modifier-curse model law requires event_id.");
                            law = ChanceLawEngineV111
                                .QueryLegacyEventModifierCurseModel(
                                    modifierCurseEventId, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Event modifier-curse support_count must match the implemented ordered pool.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "event.potion.fill_empty_slots.model")
                        {
                            RejectChanceLawQueryParameters(
                                request, "slot_index",
                                "can_acquire_potion", "slot_empty",
                                "support_count");
                            var potionSlotIndex = OptionalInt(
                                request, "slot_index") ??
                                throw new ArgumentException(
                                    "Event potion fill law requires slot_index.");
                            var eventPotionCapacity = RequiredBool(
                                request, "can_acquire_potion");
                            var eventPotionSlotEmpty = RequiredBool(
                                request, "slot_empty");
                            law = ChanceLawEngineV111
                                .QueryLegacyEventFillPotionSlot(
                                    potionSlotIndex, eventPotionCapacity,
                                    eventPotionSlotEmpty, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Event potion fill support_count must match the ordered prototype potion pool.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId ==
                            "event.reward.offer_potion.model.index_draw")
                        {
                            RejectChanceLawQueryParameters(
                                request, "owner_id", "can_acquire_potion",
                                "support_count");
                            var rewardOwnerPrefix = OptionalString(
                                request, "owner_id") ??
                                throw new ArgumentException(
                                    "Event potion-option law requires owner_id as the custom reward prefix.");
                            var eventOfferCanAcquirePotion = RequiredBool(
                                request, "can_acquire_potion");
                            law = ChanceLawEngineV111
                                .QueryLegacyEventOfferPotionModel(
                                    rewardOwnerPrefix,
                                    eventOfferCanAcquirePotion, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Event potion-option support_count must match the prototype potion pool.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "reward.elite_relic.model")
                        {
                            RejectChanceLawQueryParameters(
                                request, "act", "floor", "support_count");
                            var eliteRelicAct = OptionalInt(request, "act") ??
                                throw new ArgumentException(
                                    "Legacy Elite relic model law requires act.");
                            var eliteRelicFloor = OptionalInt(
                                request, "floor") ??
                                throw new ArgumentException(
                                    "Legacy Elite relic model law requires floor.");
                            law = ChanceLawEngineV111
                                .QueryLegacyEliteRelicModel(
                                    eliteRelicAct, eliteRelicFloor, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Legacy Elite relic model support_count must match the ordered prototype relic pool.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (siteId == "treasure.relic_model.index_draw")
                        {
                            RejectChanceLawQueryParameters(
                                request, "act", "floor", "owned_relic_ids",
                                "support_count");
                            var treasureRelicAct = OptionalInt(request, "act") ??
                                throw new ArgumentException(
                                    "Legacy Treasure relic model law requires act.");
                            var treasureRelicFloor = OptionalInt(
                                request, "floor") ??
                                throw new ArgumentException(
                                    "Legacy Treasure relic model law requires floor.");
                            var ownedRelicIds = OptionalStringArray(
                                request, "owned_relic_ids") ??
                                throw new ArgumentException(
                                    "Legacy Treasure relic model law requires owned_relic_ids (an empty array when the player owns no prototype relics).");
                            law = ChanceLawEngineV111
                                .QueryLegacyTreasureRelicModel(
                                    treasureRelicAct, treasureRelicFloor,
                                    ownedRelicIds, ordinal);
                            var supportCount = OptionalInt(
                                request, "support_count");
                            if (supportCount is not null &&
                                supportCount != law.Outcomes.Length)
                                throw new ArgumentException(
                                    "Legacy Treasure relic model support_count must match the ordered unowned prototype relic pool.");
                            Write(new
                            {
                                requestId,
                                ok = true,
                                chanceLawSchemaId =
                                    ChanceLawV111.CurrentSchemaId,
                                law
                            });
                            break;
                        }
                        if (ChanceLawEngineV111
                            .IsSupportedAncientOfferQuerySite(siteId))
                        {
                            if (cardType is not null || rarity is not null ||
                                cardId is not null || potionId is not null ||
                                relicId is not null || colorlessOffer ||
                                saleReprice || excludedPotionIds is not null ||
                                offerOrdinal is not null ||
                                relicCandidateIds is not null ||
                                excludedRelicIds is not null)
                                throw new ArgumentException(
                                    "Ancient offer queries do not accept merchant parameters.");
                            law = ChanceLawEngineV111.QueryAncientOffer(
                                run, siteId, ordinal, priorIndices,
                                priorVariantIndex);
                        }
                        else
                        {
                            if (priorIndices is not null)
                                throw new ArgumentException(
                                    "prior_indices apply only to Ancient offer shuffle queries.");
                            if (siteId == "card_rarity.roll.unit_float" &&
                                (cardRaritySourceParameter is not null ||
                                 currentOffsetBasisPoints is not null ||
                                 OptionalInt(request, "ascension") is not null ||
                                 ownerIdParameter is not null))
                            {
                                RejectChanceLawQueryParameters(
                                    request, "card_rarity_source",
                                    "current_offset_basis_points", "ascension",
                                    "owner_id", "support_count");
                                if (!Enum.TryParse<
                                        PrototypeNativeCardOddsSource>(
                                        cardRaritySourceParameter,
                                        ignoreCase: false,
                                        out var cardRaritySource) ||
                                    !Enum.IsDefined(cardRaritySource))
                                    throw new ArgumentException(
                                        "card_rarity_source must be RegularEncounter, EliteEncounter, BossEncounter, or Shop.");
                                var currentOffset =
                                    currentOffsetBasisPoints ??
                                    throw new ArgumentException(
                                        "Native card-rarity law requires current_offset_basis_points.");
                                var cardRarityAscension = OptionalInt(
                                    request, "ascension") ??
                                    throw new ArgumentException(
                                        "Native card-rarity law requires ascension.");
                                var cardRarityOwner = ownerIdParameter ??
                                    throw new ArgumentException(
                                        "Native card-rarity law requires owner_id.");
                                law = ChanceLawEngineV111.QueryNativeCardRarity(
                                    cardRaritySource, currentOffset,
                                    cardRarityAscension, cardRarityOwner,
                                    ordinal);
                                var supportCount = OptionalInt(
                                    request, "support_count");
                                if (supportCount is not null &&
                                    supportCount != law.Outcomes.Length)
                                    throw new ArgumentException(
                                        "Card-rarity support_count must match the declared source, offset, and Ascension.");
                            }
                            else if (siteId == "shop.merchant.card_upgrade_roll.unit_float")
                            {
                                if (cardType is not null || rarity is not null ||
                                    cardId is not null || potionId is not null ||
                                    relicId is not null || colorlessOffer ||
                                    saleReprice || excludedPotionIds is not null ||
                                    offerOrdinal is not null ||
                                    relicCandidateIds is not null ||
                                    excludedRelicIds is not null)
                                    throw new ArgumentException(
                                        "Merchant no-effect float query does not accept pool parameters.");
                                var typedRequest = new RandomUnitFloatRequestV111(
                                    siteId, "player.shops",
                                    OptionalString(request, "owner_id") ??
                                        throw new ArgumentException(
                                            "Merchant no-effect float query requires owner_id."),
                                    ordinal);
                                law = ChanceLawEngineV111
                                    .QueryNativeMerchantNoEffectUnitFloat(
                                        run, typedRequest);
                            }
                            else if (StochasticSiteRegistryV111
                                .IsNativeMerchantPriceSite(siteId))
                            {
                                if (cardType is not null || rarity is not null ||
                                    excludedPotionIds is not null ||
                                    offerOrdinal is not null ||
                                    relicCandidateIds is not null ||
                                    excludedRelicIds is not null)
                                    throw new ArgumentException(
                                        "Native merchant price queries do not accept model-pool parameters.");
                                law = ChanceLawEngineV111
                                    .QueryNativeMerchantPrice(
                                        run, siteId, ordinal, cardId,
                                        potionId, relicId, colorlessOffer,
                                        saleReprice);
                            }
                            else if (siteId == "relic_grab_bag.rarity.unit_float" &&
                                ownerIdParameter == PrototypeNativeRelicGrabBag
                                    .NativeTreasureRarityOwnerId)
                            {
                                if (cardType is not null || rarity is not null ||
                                    cardId is not null || potionId is not null ||
                                    relicId is not null || colorlessOffer ||
                                    saleReprice || excludedPotionIds is not null ||
                                    offerOrdinal is not null ||
                                    relicCandidateIds is not null ||
                                    excludedRelicIds is not null)
                                    throw new ArgumentException(
                                        "Native treasure rarity queries accept only the source owner and ordinal.");
                                var typedRequest = new RandomUnitFloatRequestV111(
                                    siteId, "player.rewards", ownerIdParameter,
                                    ordinal);
                                law = ChanceLawEngineV111
                                    .QueryNativeTreasureRarity(run, typedRequest);
                            }
                            else if (siteId == "treasure.relic_grab_bag.draw")
                            {
                                if (cardType is not null || rarity is not null ||
                                    cardId is not null || potionId is not null ||
                                    relicId is not null || colorlessOffer ||
                                    saleReprice || excludedPotionIds is not null ||
                                    offerOrdinal is not null ||
                                    relicCandidateIds is not null ||
                                    excludedRelicIds is not null)
                                    throw new ArgumentException(
                                        "Native treasure relic queries do not accept merchant parameters.");
                                law = ChanceLawEngineV111
                                    .QueryNativeTreasureRelicDraw(
                                        run, siteId, ordinal);
                            }
                            else if (siteId == PrototypeNativeRelicGrabBag
                                .NativeTreasureModelSiteId)
                            {
                                if (cardType is not null || cardId is not null ||
                                    potionId is not null || relicId is not null ||
                                    colorlessOffer || saleReprice ||
                                    excludedPotionIds is not null ||
                                    offerOrdinal is not null ||
                                    excludedRelicIds is not null ||
                                    rarity is null || relicCandidateIds is null ||
                                    run.World is null ||
                                    !Enum.TryParse<PrototypeRelicRarity>(
                                        rarity, ignoreCase: false,
                                        out var treasureRarity) ||
                                    !Enum.IsDefined(treasureRarity))
                                    throw new ArgumentException(
                                        "Native treasure relic selection requires rarity and relic_candidate_ids only.");
                                var ownerId = PrototypeNativeRelicGrabBag
                                    .TreasureModelOwnerId(
                                        run.World, treasureRarity,
                                        relicCandidateIds);
                                var typedRequest = new RandomIndexRequestV111(
                                    siteId,
                                    "player.rewards",
                                    ownerId, ordinal,
                                    Enumerable.Range(0,
                                        relicCandidateIds.Length).ToArray());
                                law = ChanceLawEngineV111
                                    .QueryNativeTreasureRelicSelection(
                                        run, typedRequest, treasureRarity,
                                        relicCandidateIds);
                            }
                            else if (siteId == PrototypeNativeRelicGrabBag
                                .NativeMerchantModelSiteId)
                            {
                                if (cardType is not null || cardId is not null ||
                                    potionId is not null || relicId is not null ||
                                    colorlessOffer || saleReprice ||
                                    excludedPotionIds is not null ||
                                    offerOrdinal is null || rarity is null ||
                                    relicCandidateIds is null ||
                                    excludedRelicIds is null ||
                                    run.World is null ||
                                    !Enum.TryParse<PrototypeRelicRarity>(
                                        rarity, true, out var relicRarity) ||
                                    !Enum.IsDefined(relicRarity))
                                    throw new ArgumentException(
                                        "Native merchant relic selection requires offer_ordinal, rarity, relic_candidate_ids and excluded_relic_ids only.");
                                var ownerId = PrototypeNativeRelicGrabBag
                                    .MerchantModelOwnerId(
                                        run.World, offerOrdinal.Value,
                                        relicRarity, relicCandidateIds);
                                var typedRequest = new RandomIndexRequestV111(
                                    siteId,
                                    "player.shops",
                                    ownerId, ordinal,
                                    Enumerable.Range(0,
                                        relicCandidateIds.Length).ToArray());
                                law = ChanceLawEngineV111
                                    .QueryNativeMerchantRelicSelection(
                                        run, typedRequest,
                                        offerOrdinal.Value, relicRarity,
                                        relicCandidateIds,
                                        excludedRelicIds);
                            }
                            else
                            {
                                if (cardId is not null || potionId is not null ||
                                    relicId is not null || colorlessOffer ||
                                    saleReprice || offerOrdinal is not null ||
                                    relicCandidateIds is not null ||
                                    excludedRelicIds is not null)
                                    throw new ArgumentException(
                                        "Native merchant offer queries do not accept item-price parameters.");
                                law = ChanceLawEngineV111
                                    .QueryNativeMerchantOffer(
                                        run, siteId, ordinal, cardType, rarity,
                                        excludedPotionIds);
                            }
                        }
                        Write(new
                        {
                            requestId,
                            ok = true,
                            chanceLawSchemaId = ChanceLawV111.CurrentSchemaId,
                            law
                        });
                        break;
                    }

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

                    case "semantic_begin":
                    {
                        // P0 migration boundary: a previously completed
                        // source-shaped opening fixture. Opening shuffle
                        // positions are retained as engine-committed known
                        // positions, never offered as deferred Nature choices.
                        var sourceHandle = RequiredString(request, "controlled_handle");
                        if (!controlledStates.TryGetValue(sourceHandle, out var source))
                            throw new InvalidOperationException(
                                "semantic_begin requires a valid completed controlled_handle.");
                        var next = SemanticPilotCombatEngineV111.Begin(source);
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle) });
                        break;
                    }

                    case "semantic_begin_seeded":
                    {
                        // Only intent and deferred draws can reach the
                        // external Nature policy. The opening is generated
                        // independently by engine-owned seeded streams,
                        // without externally selected HP or shuffle tickets.
                        var sourceRouteEntry = OptionalBool(request,
                            "source_route_entry");
                        var routeRegionText = OptionalString(request,
                            "route_region") ?? nameof(ActIdentity.Overgrowth);
                        if (!Enum.TryParse<ActIdentity>(routeRegionText,
                                ignoreCase: true, out var routeRegion) ||
                            routeRegion is not (ActIdentity.Overgrowth or
                                ActIdentity.Underdocks))
                            throw new ArgumentException(
                                "route_region must be Overgrowth or Underdocks.");
                        var entry = new SemanticSeededEntryV111(
                            RequiredString(request, "seed"),
                            OptionalInt(request, "act") ?? (sourceRouteEntry ? 1 :
                                throw new ArgumentException("Missing act.")),
                            OptionalString(request, "encounter_id") ??
                                (sourceRouteEntry ? "@source-route" :
                                    throw new ArgumentException("Missing encounter_id.")),
                            OptionalString(request, "native_entry") ??
                                (sourceRouteEntry ? "@source-route" :
                                    throw new ArgumentException("Missing native_entry.")),
                            OptionalInt(request, "total_floor") ??
                                (sourceRouteEntry ? 1 :
                                    throw new ArgumentException("Missing total_floor.")),
                            OptionalInt(request, "ascension") ?? 0,
                            OptionalInt(request, "first_monster_combat_id") ?? 1,
                            OptionalInt(request, "map_column"),
                            OptionalInt(request, "map_row"),
                            OptionalString(request, "other_chance_provider_id"),
                            OptionalString(request, "fixture_draw_cost_relic_id"),
                            OptionalBool(request, "replay_opening_draw_hooks"),
                            OptionalBool(request, "native_policy_baseline"),
                            sourceRouteEntry, routeRegion,
                            OptionalString(request, "route_neow_choice_id"),
                            OptionalString(request, "route_map_node_id"),
                            OptionalString(request, "fixture_random_target_card_id"),
                            OptionalString(request, "fixture_generation_potion_id"),
                            OptionalString(request, "fixture_selection_card_id"),
                            OptionalBool(request, "fixture_nature_draw_costs"),
                            OptionalString(request,
                                "fixture_pool_generation_potion_id"),
                            OptionalString(request,
                                "fixture_generated_draw_insertion_card_id"),
                            OptionalBool(request,
                                "fixture_random_free_attack_draw_insertion"),
                            OptionalInt(request,
                                "fixture_randomize_hand_costs_max_exclusive"),
                            OptionalInt(request,
                                "fixture_set_random_hand_card_cost"),
                            OptionalString(request,
                                "fixture_selection_potion_id"),
                            OptionalString(request,
                                "fixture_potion_generation_card_id"),
                            OptionalString(request,
                                "fixture_top_autoplay_potion_id"),
                            OptionalString(request,
                                "fixture_deck_preset_id"));
                        var next = SemanticPilotCombatEngineV111.BeginSeeded(entry);
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle) });
                        break;
                    }

                    case "semantic_perf_reset":
                    {
                        semanticProfiler.Reset();
                        Write(new { requestId, ok = true,
                            schemaId = "sts2-semantic-jsonl-performance-v1" });
                        break;
                    }

                    case "semantic_perf_read":
                    {
                        Write(new { requestId, ok = true,
                            profile = semanticProfiler.Snapshot() });
                        break;
                    }

                    case "semantic_status":
                    {
                        var current = RequireSemantic(request);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(current,
                                RequiredString(request, "semantic_handle")) });
                        break;
                    }

                    case "semantic_observe":
                    {
                        var current = RequireSemantic(request);
                        if (RequiredString(request, "policy_id") != FairPolicyId)
                            throw new NotSupportedException(
                                "Semantic observation requires the existing fair policy.");
                        if (!current.AtPlayerBoundary &&
                            !current.AtPlayerChoiceBoundary)
                            throw new InvalidOperationException(
                                "Semantic observation is available only at a player boundary.");
                        var frame = environment.Observe(current.Run);
                        Write(new { requestId, ok = true,
                            schemaId = SemanticChanceSchemaId,
                            policyId = FairPolicyId,
                            payloadJson = CanonicalJson.Serialize(frame.Observation),
                            observationHash = frame.ObservationHash,
                            actions = frame.LegalActions.Select(ActionWire).ToArray(),
                            terminal = false });
                        break;
                    }

                    case "semantic_nature_public_observe":
                    {
                        // Read-only view using exactly the existing fair player
                        // observation record/serializer, prior to resolving Nature.
                        var handle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        if (current.CombatTerminal ||
                            current.AtPlayerBoundary ||
                            current.AtPlayerChoiceBoundary)
                            throw new InvalidOperationException(
                                "Nature's public observation requires a pending Nature boundary.");
                        if (RequiredString(request, "policy_id") != FairPolicyId)
                            throw new NotSupportedException(
                                "Nature's public observation requires the existing fair policy.");
                        var visible = environment.ObservePublic(current.Run);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            schemaId = SemanticChanceSchemaId,
                            policyId = FairPolicyId,
                            semanticHandle = handle,
                            fingerprint = current.Fingerprint,
                            payloadJson = CanonicalJson.Serialize(visible),
                            observationHash = CanonicalJson.Sha256(visible)
                        });
                        break;
                    }

                    case "semantic_nature_observe":
                    {
                        var handle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var natureChoice =
                            DescribeSemanticNatureChoice(current);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            schemaId =
                                SemanticNatureChoiceViewV111.CurrentSchemaId,
                            semanticHandle = handle,
                            fingerprint = current.Fingerprint,
                            natureChoice
                        });
                        break;
                    }

                    case "nature_choice_observe":
                    {
                        var sessionKind = RequiredString(request,
                            "session_kind");
                        var handle = RequiredString(request, "session_handle");
                        var (fingerprint, natureChoice) =
                            DescribeNatureChoiceStatus(sessionKind, handle);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            schemaId =
                                SemanticNatureChoiceViewV111.CurrentSchemaId,
                            sessionKind,
                            sessionHandle = handle,
                            fingerprint,
                            natureChoice
                        });
                        break;
                    }

                    case "semantic_step":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var transitionStart = semanticProfiler.Enabled
                            ? SemanticOperationProfiler.Timestamp() : 0L;
                        var transitionAllocated = semanticProfiler.Enabled
                            ? SemanticOperationProfiler.Allocated() : 0L;
                        var next = SemanticPilotCombatEngineV111.Step(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "action_id"));
                        semanticProfiler.Record(
                            "sub.semantic_step_transition",
                            transitionStart, transitionAllocated);
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var transitionStart = semanticProfiler.Enabled
                            ? SemanticOperationProfiler.Timestamp() : 0L;
                        var transitionAllocated = semanticProfiler.Enabled
                            ? SemanticOperationProfiler.Allocated() : 0L;
                        var next = SemanticPilotCombatEngineV111.Resolve(
                            current, RequiredString(request, "fingerprint"),
                            OptionalInt(request, "selected_index") ??
                                throw new ArgumentException("Missing selected_index."));
                        semanticProfiler.Record(
                            "sub.semantic_resolve_transition",
                            transitionStart, transitionAllocated);
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_draw_card":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        Action<string, long, long>? recorder = semanticProfiler.Enabled
                            ? semanticProfiler.Record : null;
                        using var drawStages = SemanticDrawStageProfilerV111.Begin(recorder);
                        var transitionStart = semanticProfiler.Enabled
                            ? SemanticOperationProfiler.Timestamp() : 0L;
                        var transitionAllocated = semanticProfiler.Enabled
                            ? SemanticOperationProfiler.Allocated() : 0L;
                        var next = SemanticPilotCombatEngineV111.ResolveDrawCard(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            RequiredLong(request, "card_instance_id"));
                        semanticProfiler.Record(
                            "sub.semantic_resolve_draw_card_transition",
                            transitionStart, transitionAllocated);
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_deck_reveal":
                    {
                        var parentHandle = RequiredString(request,
                            "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveDeckReveal(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            RequiredLong(request, "card_instance_id"));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_random_pile_card":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveRandomPileCard(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            RequiredLong(request, "card_instance_id"));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_random_target":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveRandomTarget(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            OptionalInt(request, "enemy_instance_id") ??
                                throw new ArgumentException(
                                    "Missing enemy_instance_id."));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_generated_card":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveGeneratedCard(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            RequiredString(request, "card_model_id"));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_potion_generation":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolvePotionGeneration(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            RequiredString(request, "potion_model_id"));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_card_transform":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveCardTransform(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            RequiredString(request, "card_model_id"));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_cost":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveCost(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            OptionalInt(request, "cost") ??
                                throw new ArgumentException("Missing cost."));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_hand_cost_selection":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111
                            .ResolveHandCostSelection(
                                current, RequiredString(request, "fingerprint"),
                                RequiredString(request, "event_id"),
                                RequiredLong(request, "card_instance_id"));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_card_shortlist":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveCardShortlist(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            RequiredLong(request, "card_instance_id"));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_pool_shuffle":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolvePoolShuffle(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            OptionalInt(request, "swap_index") ??
                                throw new ArgumentException(
                                    "Missing swap_index."));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_draw_insertion":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveDrawInsertion(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            OptionalInt(request, "position") ??
                                throw new ArgumentException("Missing position."));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_intent":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var transitionStart = semanticProfiler.Enabled
                            ? SemanticOperationProfiler.Timestamp() : 0L;
                        var transitionAllocated = semanticProfiler.Enabled
                            ? SemanticOperationProfiler.Allocated() : 0L;
                        var next = SemanticPilotCombatEngineV111.ResolveIntent(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "intent_id"));
                        semanticProfiler.Record(
                            "sub.semantic_resolve_intent_transition",
                            transitionStart, transitionAllocated);
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_enemy_action":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.Resolve(
                            current, RequiredString(request, "fingerprint"),
                            OptionalInt(request, "selected_index") ??
                                throw new ArgumentException(
                                    "Missing selected_index."));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_enumerate":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var enumeration = SemanticNatureEnumeratorV111.Enumerate(
                            current, RequiredString(request, "fingerprint"),
                            OptionalInt(request, "maximum_outcomes") ?? 32);
                        var outcomes = enumeration.Outcomes.Select(outcome =>
                        {
                            var handle = StoreSemantic(outcome.Child);
                            return new
                            {
                                outcomeId = outcome.OutcomeId,
                                result = SemanticResult(
                                    outcome.Child, handle, parentHandle)
                            };
                        }).ToArray();
                        Write(new { requestId, ok = true,
                            schemaId = enumeration.SchemaId,
                            eventKind = enumeration.EventKind,
                            eventId = enumeration.EventId, outcomes });
                        break;
                    }

                    case "semantic_fork":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var next = RequireSemantic(request).Fork();
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_save":
                    {
                        var state = RequireSemantic(request);
                        Write(new { requestId, ok = true,
                            schemaId = SemanticChanceSchemaId,
                            snapshot = SemanticPilotCombatEngineV111.Save(state) });
                        break;
                    }

                    case "semantic_load":
                    {
                        var state = SemanticPilotCombatEngineV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreSemantic(state);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(state, handle) });
                        break;
                    }

                    case "reference_nature_run_action_begin":
                    {
                        var source = RequireState(request);
                        var actionId = OptionalString(request, "action_id");
                        GameAction action;
                        if (actionId is not null)
                        {
                            var actionView = environment.Observe(source)
                                .LegalActions.SingleOrDefault(candidate =>
                                    StringComparer.Ordinal.Equals(
                                        candidate.ActionId, actionId))
                                ?? throw new InvalidOperationException(
                                    "ReferenceNature run-action begin requires a current legal action.");
                            action = GameAction.Create(actionView.Kind,
                                actionView.Payload);
                        }
                        else
                        {
                            action = new PrototypeGameEngine().GetLegalActions(source)
                                .SingleOrDefault(candidate => candidate.Kind ==
                                    PrototypeNativeRestSiteActionsV111.Dig)
                                ?? throw new InvalidOperationException(
                                    "Missing action_id; only Shovel dig has a default.");
                        }
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureRunActionSessionV111.Begin(
                                source, action, natureSeed)
                            : ReferenceNatureRunActionSessionV111.Begin(
                                source, action);
                        var handle = StoreReferenceNatureRunAction(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureRunActionResult(session, handle) });
                        break;
                    }

                    case "reference_nature_run_action_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_run_action_handle");
                        var session = RequireReferenceNatureRunAction(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureRunActionResult(session, handle) });
                        break;
                    }

                    case "reference_nature_run_action_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_run_action_handle");
                        var session = RequireReferenceNatureRunAction(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature run-action session fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureRunAction(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureRunActionResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_run_action_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_run_action_handle");
                        var next = RequireReferenceNatureRunAction(request).Fork();
                        var handle = StoreReferenceNatureRunAction(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureRunActionResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_run_action_save":
                    {
                        var session = RequireReferenceNatureRunAction(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureRunActionSessionV111.CurrentSchemaId,
                            snapshot = ReferenceNatureRunActionSessionV111.Save(session) });
                        break;
                    }

                    case "reference_nature_run_action_load":
                    {
                        var session = ReferenceNatureRunActionSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureRunAction(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureRunActionResult(session, handle) });
                        break;
                    }

                    case "reference_nature_run_action_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_run_action_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_run_action_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature run-action handle.");
                            if (referenceNatureRunActionStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNatureRunActionRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "scripted_run_action_begin":
                    {
                        var source = RequireState(request);
                        var actionId = RequiredString(request, "action_id");
                        var actionView = environment.Observe(source)
                            .LegalActions.SingleOrDefault(candidate =>
                                StringComparer.Ordinal.Equals(
                                    candidate.ActionId, actionId))
                            ?? throw new InvalidOperationException(
                                "Scripted run-action begin requires a current legal action.");
                        var action = GameAction.Create(actionView.Kind,
                            actionView.Payload);
                        var session = ScriptedRunActionSessionV111.Begin(
                            source, action);
                        var handle = StoreScriptedRunAction(session);
                        Write(new { requestId, ok = true, result =
                            ScriptedRunActionResult(session, handle) });
                        break;
                    }

                    case "scripted_run_action_status":
                    {
                        var handle = RequiredString(request,
                            "scripted_run_action_handle");
                        var session = RequireScriptedRunAction(request);
                        Write(new { requestId, ok = true, result =
                            ScriptedRunActionResult(session, handle) });
                        break;
                    }

                    case "scripted_run_action_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "scripted_run_action_handle");
                        var session = RequireScriptedRunAction(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale scripted run-action fingerprint.");
                        if (!request.TryGetProperty("transcript_entry",
                                out var entryElement) ||
                            entryElement.ValueKind != JsonValueKind.Object)
                            throw new ArgumentException(
                                "transcript_entry must be a random-boundary transcript object.");
                        var entry = entryElement.Deserialize<
                                RandomBoundaryTranscriptEntryV111>(
                                SnakeCaseJsonOptions)
                            ?? throw new InvalidDataException(
                                "transcript_entry is empty.");
                        var outcome = new ScriptedRunActionOutcomeV111(
                            entry, OptionalStringArray(request,
                                "candidate_labels"));
                        var next = session.ResolveNext(outcome);
                        var handle = StoreScriptedRunAction(next);
                        Write(new { requestId, ok = true, result =
                            ScriptedRunActionResult(next, handle,
                                parentHandle) });
                        break;
                    }

                    case "scripted_run_action_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "scripted_run_action_handle");
                        var next = RequireScriptedRunAction(request).Fork();
                        var handle = StoreScriptedRunAction(next);
                        Write(new { requestId, ok = true, result =
                            ScriptedRunActionResult(next, handle,
                                parentHandle) });
                        break;
                    }

                    case "scripted_run_action_save":
                    {
                        var session = RequireScriptedRunAction(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ScriptedRunActionSessionV111.CurrentSchemaId,
                            snapshot =
                                ScriptedRunActionSessionV111.Save(session) });
                        break;
                    }

                    case "scripted_run_action_load":
                    {
                        var session = ScriptedRunActionSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreScriptedRunAction(session);
                        Write(new { requestId, ok = true, result =
                            ScriptedRunActionResult(session, handle) });
                        break;
                    }

                    case "scripted_run_action_release_many":
                    {
                        if (!request.TryGetProperty(
                                "scripted_run_action_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "scripted_run_action_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid scripted run-action handle.");
                            var handle = element.GetString()!;
                            if (scriptedRunActionStates.Remove(handle))
                                released++;
                            scriptedRunActionRunStateHandles.Remove(handle);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "scripted_run_begin":
                    {
                        var session = ScriptedRunSessionV111.Begin(
                            RequireState(request));
                        var handle = StoreScriptedRun(session);
                        Write(new { requestId, ok = true, result =
                            ScriptedRunResult(session, handle) });
                        break;
                    }

                    case "scripted_run_status":
                    {
                        var handle = RequiredString(request,
                            "scripted_run_handle");
                        var session = RequireScriptedRun(request);
                        Write(new { requestId, ok = true, result =
                            ScriptedRunResult(session, handle) });
                        break;
                    }

                    case "scripted_run_begin_action":
                    {
                        var parentHandle = RequiredString(request,
                            "scripted_run_handle");
                        var session = RequireScriptedRun(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale scripted run fingerprint.");
                        var actionId = RequiredString(request, "action_id");
                        var action = session.LegalActions.SingleOrDefault(
                            candidate => StringComparer.Ordinal.Equals(
                                PrototypeAiEnvironment.StableActionId(candidate),
                                actionId))
                            ?? throw new InvalidOperationException(
                                "Scripted run action must be currently legal.");
                        var next = session.BeginAction(action);
                        var handle = StoreScriptedRun(next);
                        Write(new { requestId, ok = true, result =
                            ScriptedRunResult(next, handle, parentHandle) });
                        break;
                    }

                    case "scripted_run_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "scripted_run_handle");
                        var session = RequireScriptedRun(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale scripted run fingerprint.");
                        if (!request.TryGetProperty("transcript_entry",
                                out var entryElement) ||
                            entryElement.ValueKind != JsonValueKind.Object)
                            throw new ArgumentException(
                                "transcript_entry must be a random-boundary transcript object.");
                        var entry = entryElement.Deserialize<
                                RandomBoundaryTranscriptEntryV111>(
                                SnakeCaseJsonOptions)
                            ?? throw new InvalidDataException(
                                "transcript_entry is empty.");
                        var next = session.ResolveNext(
                            new ScriptedRunActionOutcomeV111(
                                entry, OptionalStringArray(request,
                                    "candidate_labels")));
                        var handle = StoreScriptedRun(next);
                        Write(new { requestId, ok = true, result =
                            ScriptedRunResult(next, handle, parentHandle) });
                        break;
                    }

                    case "scripted_run_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "scripted_run_handle");
                        var next = RequireScriptedRun(request).Fork();
                        var handle = StoreScriptedRun(next);
                        Write(new { requestId, ok = true, result =
                            ScriptedRunResult(next, handle, parentHandle) });
                        break;
                    }

                    case "scripted_run_save":
                    {
                        var session = RequireScriptedRun(request);
                        Write(new { requestId, ok = true,
                            schemaId = ScriptedRunSessionV111.CurrentSchemaId,
                            snapshot = ScriptedRunSessionV111.Save(session) });
                        break;
                    }

                    case "scripted_run_load":
                    {
                        var session = ScriptedRunSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreScriptedRun(session);
                        Write(new { requestId, ok = true, result =
                            ScriptedRunResult(session, handle) });
                        break;
                    }

                    case "scripted_run_release_many":
                    {
                        if (!request.TryGetProperty(
                                "scripted_run_handles", out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "scripted_run_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid scripted run session handle.");
                            var handle = element.GetString()!;
                            if (scriptedRunStates.Remove(handle))
                                released++;
                            scriptedRunStateHandles.Remove(handle);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_event_choice_begin":
                    {
                        var source = RequireState(request);
                        var choiceId = RequiredString(request, "choice_id");
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureControlledEventChoiceSessionV111.Begin(
                                source, choiceId, natureSeed)
                            : ReferenceNatureControlledEventChoiceSessionV111.Begin(
                                source, choiceId);
                        var handle = StoreReferenceNatureEventChoice(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureEventChoiceResult(session, handle) });
                        break;
                    }

                    case "reference_nature_event_choice_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_event_choice_handle");
                        var session = RequireReferenceNatureEventChoice(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureEventChoiceResult(session, handle) });
                        break;
                    }

                    case "reference_nature_event_choice_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_event_choice_handle");
                        var session = RequireReferenceNatureEventChoice(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature event-choice fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureEventChoice(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureEventChoiceResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_event_choice_advance":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_event_choice_handle");
                        var session = RequireReferenceNatureEventChoice(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature event-choice fingerprint.");
                        var actionId = RequiredString(request, "action_id");
                        var action = environment.Observe(session.Run)
                            .LegalActions.SingleOrDefault(candidate =>
                                StringComparer.Ordinal.Equals(
                                    candidate.ActionId, actionId))
                            ?? throw new InvalidOperationException(
                                "ReferenceNature event advance requires a current legal player action.");
                        var next = session.AdvancePlayerAction(
                            GameAction.Create(action.Kind, action.Payload));
                        var handle = StoreReferenceNatureEventChoice(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureEventChoiceResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_event_choice_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_event_choice_handle");
                        var next = RequireReferenceNatureEventChoice(request).Fork();
                        var handle = StoreReferenceNatureEventChoice(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureEventChoiceResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_event_choice_save":
                    {
                        var session = RequireReferenceNatureEventChoice(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureControlledEventChoiceSessionV111
                                    .CurrentSchemaId,
                            snapshot =
                                ReferenceNatureControlledEventChoiceSessionV111
                                    .Save(session) });
                        break;
                    }

                    case "reference_nature_event_choice_load":
                    {
                        var session =
                            ReferenceNatureControlledEventChoiceSessionV111.Restore(
                                RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureEventChoice(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureEventChoiceResult(session, handle) });
                        break;
                    }

                    case "reference_nature_event_choice_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_event_choice_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_event_choice_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature event-choice handle.");
                            if (referenceNatureEventChoiceStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNatureEventChoiceRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_ancient_offer_begin":
                    {
                        var source = RequireState(request);
                        var siteId = RequiredString(request, "site_id");
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureAncientOfferSessionV111.Begin(
                                source, siteId, natureSeed)
                            : ReferenceNatureAncientOfferSessionV111.Begin(
                                source, siteId);
                        var handle = StoreReferenceNatureAncientOffer(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureAncientOfferResult(session, handle) });
                        break;
                    }

                    case "reference_nature_ancient_offer_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_ancient_offer_handle");
                        var session = RequireReferenceNatureAncientOffer(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureAncientOfferResult(session, handle) });
                        break;
                    }

                    case "reference_nature_ancient_offer_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_ancient_offer_handle");
                        var session = RequireReferenceNatureAncientOffer(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature Ancient-offer fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureAncientOffer(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureAncientOfferResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_ancient_offer_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_ancient_offer_handle");
                        var next = RequireReferenceNatureAncientOffer(request).Fork();
                        var handle = StoreReferenceNatureAncientOffer(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureAncientOfferResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_ancient_offer_save":
                    {
                        var session = RequireReferenceNatureAncientOffer(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureAncientOfferSessionV111.CurrentSchemaId,
                            snapshot =
                                ReferenceNatureAncientOfferSessionV111.Save(session) });
                        break;
                    }

                    case "reference_nature_ancient_offer_load":
                    {
                        var session = ReferenceNatureAncientOfferSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureAncientOffer(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureAncientOfferResult(session, handle) });
                        break;
                    }

                    case "reference_nature_ancient_offer_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_ancient_offer_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_ancient_offer_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature Ancient-offer handle.");
                            if (referenceNatureAncientOfferStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNatureAncientOfferRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_neow_offer_begin":
                    {
                        var session = ReferenceNatureNeowOfferSessionV111.Begin(
                            RequireState(request),
                            OptionalULong(request, "nature_seed") ??
                                throw new ArgumentException(
                                    "A first Neow-offer session requires nature_seed."));
                        var handle = StoreReferenceNatureNeowOffer(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNeowOfferResult(session, handle) });
                        break;
                    }

                    case "reference_nature_neow_offer_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_neow_offer_handle");
                        var session = RequireReferenceNatureNeowOffer(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNeowOfferResult(session, handle) });
                        break;
                    }

                    case "reference_nature_neow_offer_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_neow_offer_handle");
                        var session = RequireReferenceNatureNeowOffer(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature Neow-offer fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureNeowOffer(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNeowOfferResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_neow_offer_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_neow_offer_handle");
                        var next = RequireReferenceNatureNeowOffer(request).Fork();
                        var handle = StoreReferenceNatureNeowOffer(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNeowOfferResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_neow_offer_save":
                    {
                        var session = RequireReferenceNatureNeowOffer(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureNeowOfferSessionV111.CurrentSchemaId,
                            snapshot =
                                ReferenceNatureNeowOfferSessionV111.Save(session) });
                        break;
                    }

                    case "reference_nature_neow_offer_load":
                    {
                        var session = ReferenceNatureNeowOfferSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureNeowOffer(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNeowOfferResult(session, handle) });
                        break;
                    }

                    case "reference_nature_neow_offer_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_neow_offer_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_neow_offer_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature Neow-offer handle.");
                            if (referenceNatureNeowOfferStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNatureNeowOfferRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_trash_heap_begin":
                    {
                        var session = ReferenceNatureTrashHeapSessionV111.Begin(
                            RequireState(request),
                            RequiredString(request, "choice_id"),
                            OptionalULong(request, "nature_seed") ??
                                throw new ArgumentException(
                                    "A first Trash Heap session requires nature_seed."));
                        var handle = StoreReferenceNatureTrashHeap(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTrashHeapResult(session, handle) });
                        break;
                    }

                    case "reference_nature_trash_heap_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_trash_heap_handle");
                        var session = RequireReferenceNatureTrashHeap(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTrashHeapResult(session, handle) });
                        break;
                    }

                    case "reference_nature_trash_heap_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_trash_heap_handle");
                        var session = RequireReferenceNatureTrashHeap(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature Trash Heap fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureTrashHeap(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTrashHeapResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_trash_heap_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_trash_heap_handle");
                        var next = RequireReferenceNatureTrashHeap(request).Fork();
                        var handle = StoreReferenceNatureTrashHeap(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTrashHeapResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_trash_heap_save":
                    {
                        var session = RequireReferenceNatureTrashHeap(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureTrashHeapSessionV111.CurrentSchemaId,
                            snapshot =
                                ReferenceNatureTrashHeapSessionV111.Save(session) });
                        break;
                    }

                    case "reference_nature_trash_heap_load":
                    {
                        var session = ReferenceNatureTrashHeapSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureTrashHeap(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTrashHeapResult(session, handle) });
                        break;
                    }

                    case "reference_nature_trash_heap_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_trash_heap_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_trash_heap_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature Trash Heap handle.");
                            if (referenceNatureTrashHeapStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNatureTrashHeapRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_tablet_of_truth_begin":
                    {
                        var source = RequireState(request);
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureTabletOfTruthSessionV111.Begin(
                                source, natureSeed)
                            : ReferenceNatureTabletOfTruthSessionV111.Begin(
                                source);
                        var handle = StoreReferenceNatureTabletOfTruth(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTabletOfTruthResult(session, handle) });
                        break;
                    }

                    case "reference_nature_tablet_of_truth_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_tablet_of_truth_handle");
                        var session = RequireReferenceNatureTabletOfTruth(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTabletOfTruthResult(session, handle) });
                        break;
                    }

                    case "reference_nature_tablet_of_truth_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_tablet_of_truth_handle");
                        var session = RequireReferenceNatureTabletOfTruth(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature Tablet of Truth fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureTabletOfTruth(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTabletOfTruthResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_tablet_of_truth_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_tablet_of_truth_handle");
                        var next = RequireReferenceNatureTabletOfTruth(request).Fork();
                        var handle = StoreReferenceNatureTabletOfTruth(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTabletOfTruthResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_tablet_of_truth_save":
                    {
                        var session = RequireReferenceNatureTabletOfTruth(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureTabletOfTruthSessionV111.CurrentSchemaId,
                            snapshot =
                                ReferenceNatureTabletOfTruthSessionV111.Save(session) });
                        break;
                    }

                    case "reference_nature_tablet_of_truth_load":
                    {
                        var session = ReferenceNatureTabletOfTruthSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureTabletOfTruth(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTabletOfTruthResult(session, handle) });
                        break;
                    }

                    case "reference_nature_tablet_of_truth_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_tablet_of_truth_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_tablet_of_truth_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature Tablet of Truth handle.");
                            if (referenceNatureTabletOfTruthStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNatureTabletOfTruthRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_trial_begin":
                    {
                        var source = RequireState(request);
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureTrialSessionV111.Begin(
                                source, natureSeed)
                            : ReferenceNatureTrialSessionV111.Begin(source);
                        var handle = StoreReferenceNatureTrial(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTrialResult(session, handle) });
                        break;
                    }

                    case "reference_nature_trial_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_trial_handle");
                        var session = RequireReferenceNatureTrial(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTrialResult(session, handle) });
                        break;
                    }

                    case "reference_nature_trial_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_trial_handle");
                        var session = RequireReferenceNatureTrial(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature Trial fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureTrial(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTrialResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_trial_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_trial_handle");
                        var next = RequireReferenceNatureTrial(request).Fork();
                        var handle = StoreReferenceNatureTrial(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTrialResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_trial_save":
                    {
                        var session = RequireReferenceNatureTrial(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureTrialSessionV111.CurrentSchemaId,
                            snapshot = ReferenceNatureTrialSessionV111.Save(session) });
                        break;
                    }

                    case "reference_nature_trial_load":
                    {
                        var session = ReferenceNatureTrialSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureTrial(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTrialResult(session, handle) });
                        break;
                    }

                    case "reference_nature_trial_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_trial_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_trial_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature Trial handle.");
                            if (referenceNatureTrialStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNatureTrialRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_native_event_shuffle_begin":
                    {
                        var source = RequireState(request);
                        var seed = OptionalULong(request, "nature_seed");
                        var actionId = OptionalString(request, "action_id");
                        var siteId = OptionalString(request, "site_id");
                        var resolvedActionId = actionId ??
                            (siteId is PrototypeNativeHiveEvents
                                .WarHistorianRepyRelicShuffleSiteId or
                                "event.war_historian.repy.potion_offer.index_draw"
                                    ? "unlock_chest" : null);
                        var session = siteId is null
                            ? seed is { } natureSeed
                                ? ReferenceNatureNativeEventShuffleSessionV111.Begin(
                                    source, resolvedActionId, natureSeed)
                                : ReferenceNatureNativeEventShuffleSessionV111.Begin(
                                    source, resolvedActionId)
                            : seed is { } siteNatureSeed
                                ? ReferenceNatureNativeEventShuffleSessionV111.Begin(
                                    source, siteId,
                                    resolvedActionId ?? throw new ArgumentException(
                                        "A native event shuffle site requires action_id."),
                                    siteNatureSeed)
                                : ReferenceNatureNativeEventShuffleSessionV111.Begin(
                                    source, siteId,
                                    resolvedActionId ?? throw new ArgumentException(
                                        "A native event shuffle site requires action_id."));
                        var handle = StoreReferenceNatureNativeEventShuffle(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeEventShuffleResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_native_event_shuffle_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_native_event_shuffle_handle");
                        var session = RequireReferenceNatureNativeEventShuffle(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeEventShuffleResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_native_event_shuffle_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_native_event_shuffle_handle");
                        var session = RequireReferenceNatureNativeEventShuffle(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature native event shuffle fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureNativeEventShuffle(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeEventShuffleResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_native_event_shuffle_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_native_event_shuffle_handle");
                        var next = RequireReferenceNatureNativeEventShuffle(request)
                            .Fork();
                        var handle = StoreReferenceNatureNativeEventShuffle(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeEventShuffleResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_native_event_shuffle_save":
                    {
                        var session = RequireReferenceNatureNativeEventShuffle(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureNativeEventShuffleSessionV111.CurrentSchemaId,
                            snapshot =
                                ReferenceNatureNativeEventShuffleSessionV111.Save(
                                    session) });
                        break;
                    }

                    case "reference_nature_native_event_shuffle_load":
                    {
                        var session =
                            ReferenceNatureNativeEventShuffleSessionV111.Restore(
                                RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureNativeEventShuffle(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeEventShuffleResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_native_event_shuffle_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_native_event_shuffle_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_native_event_shuffle_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature native event shuffle handle.");
                            if (referenceNatureNativeEventShuffleStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNatureNativeEventShuffleRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_native_event_gold_begin":
                    {
                        var source = RequireState(request);
                        var actionId = RequiredString(request, "action_id");
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureNativeEventGoldSessionV111.Begin(
                                source, actionId, natureSeed)
                            : ReferenceNatureNativeEventGoldSessionV111.Begin(
                                source, actionId);
                        var handle = StoreReferenceNatureNativeEventGold(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeEventGoldResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_native_event_gold_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_native_event_gold_handle");
                        var session = RequireReferenceNatureNativeEventGold(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeEventGoldResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_native_event_gold_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_native_event_gold_handle");
                        var session = RequireReferenceNatureNativeEventGold(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature native event gold fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureNativeEventGold(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeEventGoldResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_native_event_gold_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_native_event_gold_handle");
                        var next = RequireReferenceNatureNativeEventGold(request)
                            .Fork();
                        var handle = StoreReferenceNatureNativeEventGold(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeEventGoldResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_native_event_gold_save":
                    {
                        var session = RequireReferenceNatureNativeEventGold(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureNativeEventGoldSessionV111.CurrentSchemaId,
                            snapshot =
                                ReferenceNatureNativeEventGoldSessionV111.Save(
                                    session) });
                        break;
                    }

                    case "reference_nature_native_event_gold_load":
                    {
                        var session =
                            ReferenceNatureNativeEventGoldSessionV111.Restore(
                                RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureNativeEventGold(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeEventGoldResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_native_event_gold_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_native_event_gold_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_native_event_gold_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature native event gold handle.");
                            if (referenceNatureNativeEventGoldStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNatureNativeEventGoldRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_native_event_relic_begin":
                    {
                        var source = RequireState(request);
                        GameAction action;
                        if (OptionalString(request, "action_id") is { } actionId)
                        {
                            var actionView = environment.Observe(source)
                                .LegalActions.SingleOrDefault(candidate =>
                                    StringComparer.Ordinal.Equals(
                                        candidate.ActionId, actionId))
                                ?? throw new InvalidOperationException(
                                    "Native event relic begin requires a current legal action.");
                            action = GameAction.Create(
                                actionView.Kind, actionView.Payload);
                        }
                        else
                        {
                            var choiceId = RequiredString(request, "choice_id");
                            action = GameAction.Create("event_choice",
                                new EventChoicePayload(choiceId));
                        }
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureNativeEventRelicSessionV111.Begin(
                                source, action, natureSeed)
                            : ReferenceNatureNativeEventRelicSessionV111.Begin(
                                source, action);
                        var handle = StoreReferenceNatureNativeEventRelic(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeEventRelicResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_native_event_relic_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_native_event_relic_handle");
                        var session = RequireReferenceNatureNativeEventRelic(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeEventRelicResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_native_event_relic_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_native_event_relic_handle");
                        var session = RequireReferenceNatureNativeEventRelic(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature native event relic fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureNativeEventRelic(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeEventRelicResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_native_event_relic_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_native_event_relic_handle");
                        var next = RequireReferenceNatureNativeEventRelic(request)
                            .Fork();
                        var handle = StoreReferenceNatureNativeEventRelic(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeEventRelicResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_native_event_relic_save":
                    {
                        var session = RequireReferenceNatureNativeEventRelic(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureNativeEventRelicSessionV111.CurrentSchemaId,
                            snapshot =
                                ReferenceNatureNativeEventRelicSessionV111.Save(
                                    session) });
                        break;
                    }

                    case "reference_nature_native_event_relic_load":
                    {
                        var session =
                            ReferenceNatureNativeEventRelicSessionV111.Restore(
                                RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureNativeEventRelic(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeEventRelicResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_native_event_relic_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_native_event_relic_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_native_event_relic_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature native event relic handle.");
                            if (referenceNatureNativeEventRelicStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNatureNativeEventRelicRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_native_shovel_begin":
                    {
                        var source = RequireState(request);
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureNativeShovelSessionV111.Begin(
                                source, natureSeed)
                            : ReferenceNatureNativeShovelSessionV111.Begin(
                                source);
                        var handle = StoreReferenceNatureNativeShovel(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeShovelResult(session, handle) });
                        break;
                    }

                    case "reference_nature_native_shovel_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_native_shovel_handle");
                        var session = RequireReferenceNatureNativeShovel(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeShovelResult(session, handle) });
                        break;
                    }

                    case "reference_nature_native_shovel_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_native_shovel_handle");
                        var session = RequireReferenceNatureNativeShovel(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature Native Shovel fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureNativeShovel(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeShovelResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_native_shovel_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_native_shovel_handle");
                        var next = RequireReferenceNatureNativeShovel(request)
                            .Fork();
                        var handle = StoreReferenceNatureNativeShovel(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeShovelResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_native_shovel_save":
                    {
                        var session = RequireReferenceNatureNativeShovel(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureNativeShovelSessionV111.CurrentSchemaId,
                            snapshot = ReferenceNatureNativeShovelSessionV111.Save(
                                session) });
                        break;
                    }

                    case "reference_nature_native_shovel_load":
                    {
                        var session = ReferenceNatureNativeShovelSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureNativeShovel(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureNativeShovelResult(session, handle) });
                        break;
                    }

                    case "reference_nature_native_shovel_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_native_shovel_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_native_shovel_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature Native Shovel handle.");
                            if (referenceNatureNativeShovelStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNatureNativeShovelRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_endless_conveyor_begin":
                    {
                        var source = RequireState(request);
                        var actionId = RequiredString(request, "action_id");
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureEndlessConveyorSessionV111.Begin(
                                source, actionId, natureSeed)
                            : ReferenceNatureEndlessConveyorSessionV111.Begin(
                                source, actionId);
                        var handle = StoreReferenceNatureEndlessConveyor(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureEndlessConveyorResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_endless_conveyor_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_endless_conveyor_handle");
                        var session = RequireReferenceNatureEndlessConveyor(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureEndlessConveyorResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_endless_conveyor_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_endless_conveyor_handle");
                        var session = RequireReferenceNatureEndlessConveyor(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature Endless Conveyor fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureEndlessConveyor(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureEndlessConveyorResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_endless_conveyor_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_endless_conveyor_handle");
                        var next = RequireReferenceNatureEndlessConveyor(request)
                            .Fork();
                        var handle = StoreReferenceNatureEndlessConveyor(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureEndlessConveyorResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_endless_conveyor_save":
                    {
                        var session = RequireReferenceNatureEndlessConveyor(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureEndlessConveyorSessionV111.CurrentSchemaId,
                            snapshot =
                                ReferenceNatureEndlessConveyorSessionV111.Save(
                                    session) });
                        break;
                    }

                    case "reference_nature_endless_conveyor_load":
                    {
                        var session =
                            ReferenceNatureEndlessConveyorSessionV111.Restore(
                                RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureEndlessConveyor(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureEndlessConveyorResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_endless_conveyor_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_endless_conveyor_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_endless_conveyor_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature Endless Conveyor handle.");
                            if (referenceNatureEndlessConveyorStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNatureEndlessConveyorRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_glory_reflections_begin":
                    {
                        var source = RequireState(request);
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureGloryReflectionsSessionV111.Begin(
                                source, natureSeed)
                            : ReferenceNatureGloryReflectionsSessionV111.Begin(
                                source);
                        var handle = StoreReferenceNatureGloryReflections(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureGloryReflectionsResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_glory_reflections_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_glory_reflections_handle");
                        var session =
                            RequireReferenceNatureGloryReflections(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureGloryReflectionsResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_glory_reflections_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_glory_reflections_handle");
                        var session =
                            RequireReferenceNatureGloryReflections(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature Glory Reflections fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureGloryReflections(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureGloryReflectionsResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_glory_reflections_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_glory_reflections_handle");
                        var next = RequireReferenceNatureGloryReflections(request)
                            .Fork();
                        var handle = StoreReferenceNatureGloryReflections(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureGloryReflectionsResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_glory_reflections_save":
                    {
                        var session =
                            RequireReferenceNatureGloryReflections(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureGloryReflectionsSessionV111.CurrentSchemaId,
                            snapshot =
                                ReferenceNatureGloryReflectionsSessionV111.Save(
                                    session) });
                        break;
                    }

                    case "reference_nature_glory_reflections_load":
                    {
                        var session =
                            ReferenceNatureGloryReflectionsSessionV111.Restore(
                                RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureGloryReflections(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureGloryReflectionsResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_glory_reflections_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_glory_reflections_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_glory_reflections_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature Glory Reflections handle.");
                            if (referenceNatureGloryReflectionsStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNatureGloryReflectionsRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_infested_automaton_begin":
                    {
                        var source = RequireState(request);
                        var choiceId = RequiredString(request, "action_id");
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureInfestedAutomatonSessionV111.Begin(
                                source, choiceId, natureSeed)
                            : ReferenceNatureInfestedAutomatonSessionV111.Begin(
                                source, choiceId);
                        var handle = StoreReferenceNatureInfestedAutomaton(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureInfestedAutomatonResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_infested_automaton_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_infested_automaton_handle");
                        var session =
                            RequireReferenceNatureInfestedAutomaton(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureInfestedAutomatonResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_infested_automaton_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_infested_automaton_handle");
                        var session =
                            RequireReferenceNatureInfestedAutomaton(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature Infested Automaton fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureInfestedAutomaton(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureInfestedAutomatonResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_infested_automaton_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_infested_automaton_handle");
                        var next = RequireReferenceNatureInfestedAutomaton(request)
                            .Fork();
                        var handle = StoreReferenceNatureInfestedAutomaton(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureInfestedAutomatonResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_infested_automaton_save":
                    {
                        var session =
                            RequireReferenceNatureInfestedAutomaton(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureInfestedAutomatonSessionV111.CurrentSchemaId,
                            snapshot =
                                ReferenceNatureInfestedAutomatonSessionV111.Save(
                                    session) });
                        break;
                    }

                    case "reference_nature_infested_automaton_load":
                    {
                        var session =
                            ReferenceNatureInfestedAutomatonSessionV111.Restore(
                                RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureInfestedAutomaton(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureInfestedAutomatonResult(
                                session, handle) });
                        break;
                    }

                    case "reference_nature_infested_automaton_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_infested_automaton_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_infested_automaton_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature Infested Automaton handle.");
                            if (referenceNatureInfestedAutomatonStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNatureInfestedAutomatonRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_punch_off_reward_begin":
                    {
                        var source = RequireState(request);
                        var actionId = RequiredString(request, "action_id");
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNaturePunchOffRewardSessionV111.Begin(
                                source, actionId, natureSeed)
                            : ReferenceNaturePunchOffRewardSessionV111.Begin(
                                source, actionId);
                        var handle = StoreReferenceNaturePunchOffReward(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNaturePunchOffRewardResult(session, handle) });
                        break;
                    }

                    case "reference_nature_punch_off_reward_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_punch_off_reward_handle");
                        var session =
                            RequireReferenceNaturePunchOffReward(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNaturePunchOffRewardResult(session, handle) });
                        break;
                    }

                    case "reference_nature_punch_off_reward_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_punch_off_reward_handle");
                        var session =
                            RequireReferenceNaturePunchOffReward(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature Punch Off reward fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNaturePunchOffReward(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNaturePunchOffRewardResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_punch_off_reward_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_punch_off_reward_handle");
                        var next = RequireReferenceNaturePunchOffReward(request)
                            .Fork();
                        var handle = StoreReferenceNaturePunchOffReward(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNaturePunchOffRewardResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_punch_off_reward_save":
                    {
                        var session =
                            RequireReferenceNaturePunchOffReward(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNaturePunchOffRewardSessionV111.CurrentSchemaId,
                            snapshot = ReferenceNaturePunchOffRewardSessionV111.Save(
                                session) });
                        break;
                    }

                    case "reference_nature_punch_off_reward_load":
                    {
                        var session =
                            ReferenceNaturePunchOffRewardSessionV111.Restore(
                                RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNaturePunchOffReward(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNaturePunchOffRewardResult(session, handle) });
                        break;
                    }

                    case "reference_nature_punch_off_reward_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_punch_off_reward_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_punch_off_reward_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature Punch Off reward handle.");
                            if (referenceNaturePunchOffRewardStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNaturePunchOffRewardRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_punch_off_fight_begin":
                    {
                        var source = RequireState(request);
                        var actionId = RequiredString(request, "action_id");
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNaturePunchOffFightSessionV111.Begin(
                                source, actionId, natureSeed)
                            : ReferenceNaturePunchOffFightSessionV111.Begin(
                                source, actionId);
                        var handle = StoreReferenceNaturePunchOffFight(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNaturePunchOffFightResult(session, handle) });
                        break;
                    }

                    case "reference_nature_punch_off_fight_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_punch_off_fight_handle");
                        var session = RequireReferenceNaturePunchOffFight(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNaturePunchOffFightResult(session, handle) });
                        break;
                    }

                    case "reference_nature_punch_off_fight_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_punch_off_fight_handle");
                        var session = RequireReferenceNaturePunchOffFight(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature Punch Off fight fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNaturePunchOffFight(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNaturePunchOffFightResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_punch_off_fight_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_punch_off_fight_handle");
                        var next = RequireReferenceNaturePunchOffFight(request)
                            .Fork();
                        var handle = StoreReferenceNaturePunchOffFight(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNaturePunchOffFightResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_punch_off_fight_save":
                    {
                        var session = RequireReferenceNaturePunchOffFight(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNaturePunchOffFightSessionV111.CurrentSchemaId,
                            snapshot = ReferenceNaturePunchOffFightSessionV111.Save(
                                session) });
                        break;
                    }

                    case "reference_nature_punch_off_fight_load":
                    {
                        var session =
                            ReferenceNaturePunchOffFightSessionV111.Restore(
                                RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNaturePunchOffFight(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNaturePunchOffFightResult(session, handle) });
                        break;
                    }

                    case "reference_nature_punch_off_fight_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_punch_off_fight_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_punch_off_fight_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature Punch Off fight handle.");
                            if (referenceNaturePunchOffFightStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNaturePunchOffFightRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_merchant_begin":
                    {
                        var source = RequireState(request);
                        var actionId = RequiredString(request, "action_id");
                        var action = environment.Observe(source).LegalActions
                            .SingleOrDefault(candidate =>
                                StringComparer.Ordinal.Equals(
                                    candidate.ActionId, actionId))
                            ?? throw new InvalidOperationException(
                                "ReferenceNature merchant begin requires a current legal configured Shop entry or supported Shop purchase action.");
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureMerchantSessionV111.Begin(
                                source, GameAction.Create(
                                    action.Kind, action.Payload), natureSeed)
                            : ReferenceNatureMerchantSessionV111.Begin(
                                source, GameAction.Create(
                                    action.Kind, action.Payload));
                        var handle = StoreReferenceNatureMerchant(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureMerchantResult(session, handle) });
                        break;
                    }

                    case "reference_nature_merchant_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_merchant_handle");
                        var session = RequireReferenceNatureMerchant(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureMerchantResult(session, handle) });
                        break;
                    }

                    case "reference_nature_merchant_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_merchant_handle");
                        var session = RequireReferenceNatureMerchant(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature merchant fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureMerchant(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureMerchantResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_merchant_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_merchant_handle");
                        var next = RequireReferenceNatureMerchant(request).Fork();
                        var handle = StoreReferenceNatureMerchant(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureMerchantResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_merchant_save":
                    {
                        var session = RequireReferenceNatureMerchant(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureMerchantSessionV111.CurrentSchemaId,
                            snapshot =
                                ReferenceNatureMerchantSessionV111.Save(session) });
                        break;
                    }

                    case "reference_nature_merchant_load":
                    {
                        var session = ReferenceNatureMerchantSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureMerchant(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureMerchantResult(session, handle) });
                        break;
                    }

                    case "reference_nature_merchant_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_merchant_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_merchant_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature merchant handle.");
                            var handle = element.GetString()!;
                            if (referenceNatureMerchantStates.Remove(handle))
                                released++;
                            referenceNatureMerchantRunStateHandles.Remove(handle);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_treasure_begin":
                    {
                        var source = RequireState(request);
                        var actionId = RequiredString(request, "action_id");
                        var action = environment.Observe(source).LegalActions
                            .SingleOrDefault(candidate =>
                                StringComparer.Ordinal.Equals(
                                    candidate.ActionId, actionId))
                            ?? throw new InvalidOperationException(
                                "ReferenceNature treasure begin requires a current legal Treasure-node action.");
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureTreasureSessionV111.Begin(
                                source, GameAction.Create(
                                    action.Kind, action.Payload), natureSeed)
                            : ReferenceNatureTreasureSessionV111.Begin(
                                source, GameAction.Create(
                                    action.Kind, action.Payload));
                        var handle = StoreReferenceNatureTreasure(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTreasureResult(session, handle) });
                        break;
                    }

                    case "reference_nature_treasure_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_treasure_handle");
                        var session = RequireReferenceNatureTreasure(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTreasureResult(session, handle) });
                        break;
                    }

                    case "reference_nature_treasure_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_treasure_handle");
                        var session = RequireReferenceNatureTreasure(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature treasure fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureTreasure(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTreasureResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_treasure_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_treasure_handle");
                        var next = RequireReferenceNatureTreasure(request).Fork();
                        var handle = StoreReferenceNatureTreasure(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTreasureResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_treasure_save":
                    {
                        var session = RequireReferenceNatureTreasure(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureTreasureSessionV111.CurrentSchemaId,
                            snapshot = ReferenceNatureTreasureSessionV111
                                .Save(session) });
                        break;
                    }

                    case "reference_nature_treasure_load":
                    {
                        var session = ReferenceNatureTreasureSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureTreasure(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTreasureResult(session, handle) });
                        break;
                    }

                    case "reference_nature_treasure_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_treasure_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_treasure_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature treasure handle.");
                            var handle = element.GetString()!;
                            if (referenceNatureTreasureStates.Remove(handle))
                                released++;
                            referenceNatureTreasureRunStateHandles
                                .Remove(handle);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_combat_reward_begin":
                    {
                        var source = RequireState(request);
                        var actionId = RequiredString(request, "action_id");
                        var action = environment.Observe(source).LegalActions
                            .SingleOrDefault(candidate =>
                                StringComparer.Ordinal.Equals(
                                    candidate.ActionId, actionId))
                            ?? throw new InvalidOperationException(
                                "ReferenceNature combat reward begin requires a current legal winning combat action.");
                        var seed = OptionalULong(request, "nature_seed");
                        var nativeAction = GameAction.Create(
                            action.Kind, action.Payload);
                        var session = seed is { } natureSeed
                            ? ReferenceNatureCombatRewardSessionV111.Begin(
                                source, nativeAction, natureSeed)
                            : ReferenceNatureCombatRewardSessionV111.Begin(
                                source, nativeAction);
                        var handle = StoreReferenceNatureCombatReward(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureCombatRewardResult(session, handle) });
                        break;
                    }

                    case "reference_nature_combat_reward_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_combat_reward_handle");
                        var session = RequireReferenceNatureCombatReward(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureCombatRewardResult(session, handle) });
                        break;
                    }

                    case "reference_nature_combat_reward_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_combat_reward_handle");
                        var session = RequireReferenceNatureCombatReward(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature combat reward fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureCombatReward(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureCombatRewardResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_combat_reward_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_combat_reward_handle");
                        var next = RequireReferenceNatureCombatReward(request)
                            .Fork();
                        var handle = StoreReferenceNatureCombatReward(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureCombatRewardResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_combat_reward_save":
                    {
                        var session = RequireReferenceNatureCombatReward(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureCombatRewardSessionV111.CurrentSchemaId,
                            snapshot = ReferenceNatureCombatRewardSessionV111
                                .Save(session) });
                        break;
                    }

                    case "reference_nature_combat_reward_load":
                    {
                        var session = ReferenceNatureCombatRewardSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureCombatReward(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureCombatRewardResult(session, handle) });
                        break;
                    }

                    case "reference_nature_combat_reward_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_combat_reward_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_combat_reward_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature combat reward handle.");
                            var handle = element.GetString()!;
                            if (referenceNatureCombatRewardStates.Remove(handle))
                                released++;
                            referenceNatureCombatRewardRunStateHandles
                                .Remove(handle);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_map_generation_begin":
                    {
                        var seed = RequiredString(request, "seed");
                        var natureSeed = OptionalULong(request, "nature_seed")
                            ?? throw new ArgumentException(
                                "ReferenceNature map generation requires nature_seed.");
                        var act = OptionalInt(request, "act") ?? 1;
                        var ascension = OptionalInt(request, "ascension") ?? 0;
                        var session = ReferenceNatureMapGenerationSessionV111.Begin(
                            seed, act, ascension, natureSeed);
                        var handle = StoreReferenceNatureMapGeneration(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureMapGenerationResult(session, handle) });
                        break;
                    }

                    case "reference_nature_map_generation_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_map_generation_handle");
                        var session =
                            RequireReferenceNatureMapGeneration(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureMapGenerationResult(session, handle) });
                        break;
                    }

                    case "reference_nature_map_generation_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_map_generation_handle");
                        var session =
                            RequireReferenceNatureMapGeneration(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature map-generation fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureMapGeneration(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureMapGenerationResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_map_generation_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_map_generation_handle");
                        var next = RequireReferenceNatureMapGeneration(request)
                            .Fork();
                        var handle = StoreReferenceNatureMapGeneration(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureMapGenerationResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_map_generation_save":
                    {
                        var session =
                            RequireReferenceNatureMapGeneration(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureMapGenerationSessionV111.CurrentSchemaId,
                            snapshot =
                                ReferenceNatureMapGenerationSessionV111.Save(
                                    session) });
                        break;
                    }

                    case "reference_nature_map_generation_load":
                    {
                        var session =
                            ReferenceNatureMapGenerationSessionV111.Restore(
                                RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureMapGeneration(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureMapGenerationResult(session, handle) });
                        break;
                    }

                    case "reference_nature_map_generation_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_map_generation_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_map_generation_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature map-generation handle.");
                            if (referenceNatureMapGenerationStates.Remove(
                                    element.GetString()!))
                                released++;
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "semantic_reference_begin":
                    {
                        var combat = RequireSemantic(request);
                        if (combat.UsesNativePolicyBaseline)
                            throw new NotSupportedException(
                                "ReferenceNature cannot start from a native-policy session.");
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureSessionV111.Begin(combat, natureSeed)
                            : ReferenceNatureSessionV111.Begin(combat);
                        var handle = StoreReferenceNature(session);
                        Write(new { requestId, result =
                            ReferenceNatureResult(session, handle) });
                        break;
                    }

                    case "semantic_reference_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_handle");
                        var session = RequireReferenceNature(request);
                        Write(new { requestId, result =
                            ReferenceNatureResult(session, handle) });
                        break;
                    }

                    case "semantic_reference_observe":
                    {
                        var session = RequireReferenceNature(request);
                        if (RequiredString(request, "policy_id") != FairPolicyId)
                            throw new NotSupportedException(
                                "ReferenceNature observation requires the existing fair policy.");
                        if (!session.Combat.AtPlayerBoundary &&
                            !session.Combat.AtPlayerChoiceBoundary)
                            throw new InvalidOperationException(
                                "ReferenceNature observation is available only at a player boundary.");
                        var frame = environment.Observe(session.Combat.Run);
                        Write(new { requestId, ok = true,
                            schemaId = SemanticChanceSchemaId,
                            policyId = FairPolicyId,
                            payloadJson = CanonicalJson.Serialize(frame.Observation),
                            observationHash = frame.ObservationHash,
                            actions = frame.LegalActions.Select(ActionWire).ToArray(),
                            terminal = false });
                        break;
                    }

                    case "semantic_reference_step":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_handle");
                        var session = RequireReferenceNature(request);
                        if (session.Fingerprint != RequiredString(request, "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature session fingerprint.");
                        var combat = SemanticPilotCombatEngineV111.Step(
                            session.Combat, session.Combat.Fingerprint,
                            RequiredString(request, "action_id"));
                        var next = session with { Combat = combat };
                        next.Validate();
                        var handle = StoreReferenceNature(next);
                        Write(new { requestId, result =
                            ReferenceNatureResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_reference_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_handle");
                        var session = RequireReferenceNature(request);
                        if (session.Fingerprint != RequiredString(request, "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature session fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNature(next);
                        Write(new { requestId, result =
                            ReferenceNatureResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_reference_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_handle");
                        var next = RequireReferenceNature(request).Fork();
                        var handle = StoreReferenceNature(next);
                        Write(new { requestId, result =
                            ReferenceNatureResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_reference_save":
                    {
                        var session = RequireReferenceNature(request);
                        Write(new { requestId, ok = true,
                            schemaId = ReferenceNatureSessionV111.CurrentSchemaId,
                            snapshot = ReferenceNatureSessionV111.Save(session) });
                        break;
                    }

                    case "semantic_reference_load":
                    {
                        var session = ReferenceNatureSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNature(session);
                        Write(new { requestId, result =
                            ReferenceNatureResult(session, handle) });
                        break;
                    }

                    case "semantic_reference_release_many":
                    {
                        if (!request.TryGetProperty("reference_nature_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature handle.");
                            if (referenceNatureStates.Remove(element.GetString()!))
                                released++;
                        }
                        Write(new { requestId, released });
                        break;
                    }

                    case "semantic_release_many":
                    {
                        if (!request.TryGetProperty("semantic_handles", out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "semantic_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException("Invalid semantic handle.");
                            if (semanticStates.Remove(element.GetString()!))
                                released++;
                        }
                        Write(new { requestId, ok = true, released });
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

                    case "controlled_event_begin":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var source = RequireState(request);
                        var next = ControlledEventChoiceEngineV111.Begin(
                            source, RequiredString(request, "choice_id"));
                        var handle = Store(next, parentHandle);
                        Write(ControlledEventChoiceResult(
                            next, handle, parentHandle));
                        break;
                    }

                    case "controlled_event_advance":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var source = RequireState(request);
                        var actionId = RequiredString(request, "action_id");
                        var frame = environment.Observe(source);
                        var action = frame.LegalActions.SingleOrDefault(candidate =>
                            StringComparer.Ordinal.Equals(
                                candidate.ActionId, actionId))
                            ?? throw new InvalidOperationException(
                                $"Action '{actionId}' is not a player-legal reward action in state '{parentHandle}'.");
                        var next = ControlledEventChoiceEngineV111.Advance(
                            source,
                            GameAction.Create(action.Kind, action.Payload));
                        var handle = Store(next, parentHandle);
                        Write(ControlledEventChoiceResult(
                            next, handle, parentHandle));
                        break;
                    }

                    case "controlled_event_inspect":
                    {
                        var handle = RequiredString(request, "state_handle");
                        var state = RequireState(request);
                        if (state.World?.Event?.ControlledChoice is null)
                        {
                            if (state.ControlledEventRewardChoice is null)
                                throw new InvalidOperationException(
                                    "State has no pending controlled event choice.");
                        }
                        Write(ControlledEventChoiceResult(state, handle, null));
                        break;
                    }

                    case "controlled_event_resolve":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var source = RequireState(request);
                        var selectedIndex = OptionalInt(request, "selected_index")
                            ?? throw new ArgumentException("Missing selected_index.");
                        var next = ControlledEventChoiceEngineV111.Resolve(
                            source, selectedIndex);
                        var handle = Store(next, parentHandle);
                        Write(ControlledEventChoiceResult(
                            next, handle, parentHandle));
                        break;
                    }

                    case "controlled_event_enumerate":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var current = RequireState(request);
                        var enumeration =
                            ControlledEventChoiceEnumeratorV111.Enumerate(
                                current,
                                RequiredString(request, "fingerprint"),
                                OptionalInt(request, "maximum_outcomes") ?? 32);
                        var outcomes = enumeration.Outcomes.Select(outcome =>
                        {
                            var handle = Store(outcome.Child, parentHandle);
                            return new
                            {
                                outcomeId = outcome.SelectedIndex,
                                outcome.CandidateLabel,
                                result = ControlledEventChoiceResult(
                                    outcome.Child, handle, parentHandle)
                            };
                        }).ToArray();
                        Write(new
                        {
                            requestId,
                            ok = true,
                            schemaId = enumeration.SchemaId,
                            enumeration.EventId,
                            enumeration.ChoiceId,
                            outcomes
                        });
                        break;
                    }

                    case "controlled_run_action_begin":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var source = RequireState(request);
                        var engine = new PrototypeGameEngine();
                        GameAction action;
                        if (OptionalString(request, "action_id") is { } actionId)
                        {
                            var frame = environment.Observe(source);
                            var actionView = frame.LegalActions.SingleOrDefault(candidate =>
                                StringComparer.Ordinal.Equals(
                                    candidate.ActionId, actionId))
                                ?? throw new InvalidOperationException(
                                    "Controlled run-action begin requires a current player-legal action.");
                            action = GameAction.Create(
                                actionView.Kind, actionView.Payload);
                        }
                        else
                        {
                            action = engine.GetLegalActions(source)
                                .SingleOrDefault(candidate =>
                                    candidate.Kind ==
                                        PrototypeNativeRestSiteActionsV111.Dig)
                                ?? throw new InvalidOperationException(
                                    "Missing action_id; only Shovel dig can use the legacy default.");
                        }
                        var next = ControlledRunActionEngineV111.Begin(
                            source, action);
                        var handle = Store(next, parentHandle);
                        Write(ControlledRunActionResult(next, handle, parentHandle));
                        break;
                    }

                    case "controlled_run_action_inspect":
                    {
                        var handle = RequiredString(request, "state_handle");
                        var state = RequireState(request);
                        if (state.ControlledRunActionChoice is null)
                            throw new InvalidOperationException(
                                "State has no pending controlled run action.");
                        Write(ControlledRunActionResult(state, handle, null));
                        break;
                    }

                    case "controlled_run_action_resolve":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var source = RequireState(request);
                        var selectedIndex = OptionalInt(request, "selected_index")
                            ?? throw new ArgumentException("Missing selected_index.");
                        var next = ControlledRunActionEngineV111.Resolve(
                            source, selectedIndex);
                        var handle = Store(next, parentHandle);
                        Write(ControlledRunActionResult(next, handle, parentHandle));
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

                    case "mechanics_compare":
                    {
                        RunState ResolveComparisonRun(string side)
                        {
                            var snapshotName = side + "_snapshot";
                            var semanticHandleName =
                                side + "_semantic_handle";
                            bool hasSnapshot = request.TryGetProperty(
                                snapshotName, out var snapshotElement);
                            bool hasSemanticHandle = request.TryGetProperty(
                                semanticHandleName, out var handleElement);
                            if (hasSnapshot == hasSemanticHandle)
                                throw new ArgumentException(
                                    $"mechanics_compare requires exactly one of {snapshotName} or {semanticHandleName}.");
                            if (hasSnapshot)
                            {
                                if (snapshotElement.ValueKind !=
                                        JsonValueKind.String ||
                                    string.IsNullOrWhiteSpace(
                                        snapshotElement.GetString()))
                                    throw new ArgumentException(
                                        $"{snapshotName} must be a nonempty snapshot string.");
                                return RunSnapshot.Load(
                                    snapshotElement.GetString()!);
                            }

                            if (handleElement.ValueKind !=
                                    JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(
                                    handleElement.GetString()))
                                throw new ArgumentException(
                                    $"{semanticHandleName} must be a nonempty session handle.");
                            var handle = handleElement.GetString()!;
                            return semanticStates.TryGetValue(handle,
                                out var semanticState)
                                ? semanticState.Run
                                : throw new InvalidOperationException(
                                    "Unknown semantic session handle: " + handle);
                        }

                        var left = ResolveComparisonRun("left");
                        var right = ResolveComparisonRun("right");
                        var maximumDifferences =
                            OptionalInt(request, "maximum_differences") ?? 64;
                        var comparison = SimulationMechanicsProjectionEngineV111
                            .Compare(left, right, maximumDifferences);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            schemaId =
                                SimulationMechanicsComparisonV111.SchemaId,
                            comparison
                        });
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

    private static long RequiredLong(JsonElement request, string name)
    {
        if (!request.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out var result))
            throw new InvalidOperationException(
                $"Request field '{name}' must be an integer.");
        return result;
    }

    private static ulong RequiredULong(JsonElement request, string name)
    {
        if (!request.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetUInt64(out var result))
            throw new InvalidOperationException(
                $"Request field '{name}' must be a non-negative integer.");
        return result;
    }

    private static ulong? OptionalULong(JsonElement request, string name)
    {
        if (!request.TryGetProperty(name, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetUInt64(out var result))
            throw new InvalidOperationException(
                $"Request field '{name}' must be a non-negative integer.");
        return result;
    }

    private static bool RequiredBool(JsonElement request, string name)
    {
        if (!request.TryGetProperty(name, out var value) ||
            value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new ArgumentException(
                $"Request field '{name}' must be a boolean.");
        return value.GetBoolean();
    }

    private static void RejectChanceLawQueryParameters(
        JsonElement request, params string[] allowedFields)
    {
        var allowed = new HashSet<string>(allowedFields,
            StringComparer.Ordinal);
        string[] optionalFields =
        [
            "prior_indices", "prior_variant_index", "prior_card_ids",
            "support_count",
            "action_id", "event_id", "card_type", "rarity", "card_id",
            "potion_id", "relic_id", "colorless_offer", "sale_reprice",
            "owner_id", "excluded_potion_ids", "offer_ordinal",
            "relic_candidate_ids", "excluded_relic_ids", "first_boss",
            "owned_relic_ids",
            "ascension", "act", "include_darv", "hive_receives_darv",
            "room", "gold_proportion", "current_thousandths", "elite",
            "forced", "card_rarity_source",
            "current_offset_basis_points", "stream_id", "merchant",
            "native_encounter_entry", "total_floor",
            "can_acquire_potion", "floor", "drop_offered",
            "card_instance_id", "slot_index", "slot_empty"
        ];
        var unsupported = optionalFields.Where(field =>
            !allowed.Contains(field) && request.TryGetProperty(field, out _))
            .ToArray();
        if (unsupported.Length > 0)
            throw new ArgumentException(
                "This Chance Law query does not accept parameter(s): "
                + string.Join(", ", unsupported) + ".");
    }

    private static bool OptionalBool(JsonElement request, string name)
    {
        if (!request.TryGetProperty(name, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return false;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new ArgumentException(
                "Request field '" + name + "' must be a boolean.")
        };
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

    private static string[]? OptionalStringArray(
        JsonElement request, string name)
    {
        if (!request.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind != JsonValueKind.Array)
            throw new ArgumentException(
                $"Request field '{name}' must be an array of strings.");
        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(item.GetString()))
                throw new ArgumentException(
                    $"Request field '{name}' must contain nonempty strings.");
            items.Add(item.GetString()!);
        }
        return items.ToArray();
    }
}
