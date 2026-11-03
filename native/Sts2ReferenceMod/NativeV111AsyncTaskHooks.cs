#if STS2_NATIVE
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Sts2ReferenceMod.Trace;

namespace Sts2ReferenceMod;

/// <summary>
/// Opt-in, pinned-v0.111 passive lifecycle instrumentation. Harmony postfixes
/// read original task references, never replace results or manipulate tasks.
/// Registrations are queued and observed on the normal native game frame.
/// </summary>
internal static class NativeV111AsyncTaskHooks
{
    private const string Owner = "nn971.sts2-inspector.native-async-tasks-v111";
    private static Harmony? _patches;
    private static NativeAsyncTaskTracker? _tracker;
    private static Task? _lastTurnLoopTask;
    private static readonly FieldInfo? ExecutionTaskField =
        typeof(GameAction).GetField("_executionTask",
            BindingFlags.Instance | BindingFlags.NonPublic);

    internal static bool IsInstalled => _patches is not null;

    internal static void TryInstall()
    {
        if (IsInstalled ||
            Environment.GetEnvironmentVariable("STS2_FIDELITY_TASK_HOOK") != "1")
            return;
        if (!NativeV111PassiveBridge.ReadyForNativeRngHook)
        {
            GD.PushError("[Sts2ReferenceMod] TASK_HOOK_STATUS=PIN_OR_SCENE_REJECTED");
            return;
        }
        Harmony? patches = null;
        try
        {
            if (ExecutionTaskField is null ||
                !typeof(Task).IsAssignableFrom(ExecutionTaskField.FieldType))
                throw new MissingFieldException("GameAction._executionTask");

            patches = new Harmony(Owner);
            Patch(patches, AccessTools.Method(typeof(GameAction),
                nameof(GameAction.Execute), Type.EmptyTypes),
                nameof(PostfixExecute));
            Patch(patches, AccessTools.Method(typeof(HookPlayerChoiceContext),
                nameof(HookPlayerChoiceContext.AssignTaskAndWaitForPauseOrCompletion),
                [typeof(Task)]), nameof(PostfixHookAssign));
            Patch(patches, AccessTools.Method(typeof(HookPlayerChoiceContext),
                nameof(HookPlayerChoiceContext.WaitForPauseOrCompletionWithoutAssigningTask),
                [typeof(Task)]), nameof(PostfixHookWait));
            Patch(patches, AccessTools.Method(typeof(HookPlayerChoiceContext),
                nameof(HookPlayerChoiceContext.WaitForCompletion),
                Type.EmptyTypes), nameof(PostfixHookCompletion));
            Patch(patches, AccessTools.Method(typeof(CombatManager),
                "RunTurnLoopAfter", [typeof(Task)]),
                nameof(PostfixTurnLoop));
            _patches = patches;
            GD.Print("[Sts2ReferenceMod] TASK_HOOK_STATUS=PINNED_TASK_REFERENCES_INSTALLED;" +
                " hook_sites=5; task_mutations=0; source_exhaustive=false");
        }
        catch (Exception ex)
        {
            try { patches?.UnpatchAll(Owner); } catch { }
            GD.PushError("[Sts2ReferenceMod] TASK_HOOK_STATUS=UNAVAILABLE; reason=" +
                ex.GetType().Name);
        }
    }

    private static void Patch(Harmony patches, MethodInfo? target, string postfixName)
    {
        if (target is null)
            throw new MissingMethodException(postfixName);
        var postfix = typeof(NativeV111AsyncTaskHooks).GetMethod(
            postfixName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(postfixName);
        patches.Patch(target, postfix: new HarmonyMethod(postfix));
    }

    internal static void BeginCombat(NativeActionEnvelopeRecorder observer)
    {
        _tracker?.EndCombat();
        _tracker = IsInstalled ? new NativeAsyncTaskTracker(observer) : null;
        if (_lastTurnLoopTask is { } turnLoop)
            _tracker?.Register(turnLoop, "TURN_LOOP",
                "CombatManager.RunTurnLoopAfter(pre-attachment)");
    }

    internal static void PollFrame() => _tracker?.Poll();

    internal static void PlayerCheckpoint(int turn) =>
        _tracker?.Checkpoint(turn, "PLAYER_TURN_START");

    internal static void EndCombat()
    {
        _tracker?.EndCombat();
        _tracker = null;
    }

    private static void PostfixExecute(GameAction __instance, Task __result)
    {
        // Capturing both references is essential: GameAction.Execute can
        // finish its step on a choice pause while _executionTask remains live.
        _tracker?.Register(__result, "GAME_ACTION_STEP", "GameAction.Execute");
        try
        {
            if (ExecutionTaskField?.GetValue(__instance) is Task underlying)
                _tracker?.Register(underlying, "GAME_ACTION_BODY",
                    "GameAction._executionTask");
        }
        catch (Exception ex)
        {
            NativeV111PassiveBridge.NativeTaskHookFailure(ex.GetType().Name);
        }
    }

    private static void PostfixHookAssign(
        Task task, Task<bool> __result)
    {
        _tracker?.Register(task, "HOOK_TASK_BODY",
            "HookPlayerChoiceContext.AssignTaskAndWaitForPauseOrCompletion");
        _tracker?.Register(__result, "HOOK_PAUSE_WAIT",
            "HookPlayerChoiceContext.AssignTaskAndWaitForPauseOrCompletion");
    }

    private static void PostfixHookWait(
        Task task, Task<bool> __result)
    {
        _tracker?.Register(task, "HOOK_TASK_BODY",
            "HookPlayerChoiceContext.WaitForPauseOrCompletionWithoutAssigningTask");
        _tracker?.Register(__result, "HOOK_PAUSE_WAIT",
            "HookPlayerChoiceContext.WaitForPauseOrCompletionWithoutAssigningTask");
    }

    private static void PostfixHookCompletion(
        HookPlayerChoiceContext __instance, Task __result)
    {
        if (__instance.Task is { } task)
            _tracker?.Register(task, "HOOK_TASK_BODY",
                "HookPlayerChoiceContext.WaitForCompletion");
        _tracker?.Register(__result, "HOOK_COMPLETION_BARRIER",
            "HookPlayerChoiceContext.WaitForCompletion");
    }

    private static void PostfixTurnLoop(Task __result)
    {
        _lastTurnLoopTask = __result;
        _tracker?.Register(__result, "TURN_LOOP",
            "CombatManager.RunTurnLoopAfter");
    }
}
#endif
