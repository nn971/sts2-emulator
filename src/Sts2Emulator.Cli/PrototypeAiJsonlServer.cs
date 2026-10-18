Warning: truncated output (original token count: 139379)
Total output lines: 10256

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
            semanticStates.Add(handle, state.Fork());
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
            var choiceId = session.Action.ReadPayload<EventChoicePayload>()
                .ChoiceId;
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
                eventId = NativeAncientRewardsV111.TezcataraId,
                choiceId,
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending = law is null ? null : new
                {
                    kind = "native_event_toy_box_relic_draw",
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
            string handle…109379 tokens truncated… policy, schemaId = frame.SchemaId,
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
