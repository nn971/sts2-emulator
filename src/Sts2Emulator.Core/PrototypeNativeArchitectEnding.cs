namespace Sts2Emulator.Core;

/// <summary>
/// Silent's post-Glory The Architect ending. The native Silent dialogues
/// each contain one line followed by the Proceed option; their animation
/// and save-profile text are outside this headless run model.
/// </summary>
public static class PrototypeNativeArchitectEnding
{
    public const string EventId = "proto.native.glory.the_architect";
    public const string ProceedChoiceId = "proceed";

    public static PrototypeEventDefinition Definition { get; } = new(
        EventId,
        "The Architect",
        [new(ProceedChoiceId, "Proceed", [])],
        MinAct: 3,
        MaxAct: 3,
        Weight: 0,
        OncePerRun: true);

    public static RunState EnterAfterFinalBoss(RunState state)
    {
        var world = state.World
            ?? throw new InvalidOperationException(
                "The Architect ending requires an initialized run.");
        if (state.Configuration is not
                {
                    UsesExperimentalSilentThreeActRoute: true,
                    CharacterId: "silent"
                }
            || state.Phase != RunPhase.Reward
            || world.Reward is not null
            || world.ActiveRoom is not null
            || world.Act != 3
            || world.TerminalOutcome is not null
            || world.EventIds.Contains(EventId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The Architect can only follow the final Glory boss once.");
        }

        return state with
        {
            Phase = RunPhase.Event,
            World = world with
            {
                Event = new EventState(EventId),
                EventHistory = world.EventIds.Append(EventId).ToArray()
            }
        };
    }
}

public sealed partial class PrototypeGameEngine
{
    private static RunState StepNativeArchitectEnding(
        RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        if (world.Act != 3
            || world.Event?.EventId != PrototypeNativeArchitectEnding.EventId
            || world.Event.NativePageIndex != 0
            || action.ReadPayload<EventChoicePayload>().ChoiceId
                != PrototypeNativeArchitectEnding.ProceedChoiceId)
        {
            throw new InvalidOperationException(
                "The Architect ending only accepts its Proceed option.");
        }

        return EndRun(state, "victory");
    }
}
