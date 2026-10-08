namespace Sts2Emulator.Core;

/// <summary>
/// v0.111.0 Dense Vegetation: trudge through for 8 unblockable HP
/// and 61..99 gold; or mimic rest-site healing, then enter a
/// four-Wriggler fight with no postcombat rewards.
/// </summary>
public static class PrototypeNativeDenseVegetation
{
    public const string EventId = "proto.native.event.dense_vegetation";
    public const string EncounterId =
        "proto.native.encounter.dense_vegetation_event";
}

public sealed partial class PrototypeGameEngine
{
    private static RunState StepNativeDenseVegetation(
        RunState state,
        GameAction action)
    {
        RequireKind(action, "event_choice");
        var choiceId = action.ReadPayload<EventChoicePayload>().ChoiceId;
        var world = RequireWorld(state);
        var current = world.Event
            ?? throw new InvalidOperationException(
                "Dense Vegetation has no event state.");
        if (current.EventId != PrototypeNativeDenseVegetation.EventId)
        {
            throw new InvalidOperationException(
                "Unexpected Dense Vegetation source event.");
        }

        if (current.NativePageIndex == 0)
        {
            if (choiceId == "trudge")
            {
                var hpAfter = Math.Max(0, state.Player.Hp - 8);
                if (hpAfter == 0)
                {
                    return EndRun(state with
                    {
                        Player = state.Player with { Hp = 0 }
                    }, "defeat");
                }

                var gold = current.NativeEventGold is >= 61 and <= 99
                    ? current.NativeEventGold
                    : 61 + PrototypeRng.NextInt(
                        state.Rng, "event", 39);
                return CompleteRoomToMap(state with
                {
                    Player = state.Player with
                    {
                        Hp = hpAfter,
                        Gold = state.Player.Gold + gold
                    }
                });
            }

            if (choiceId == "rest")
            {
                var healing = Math.Max(1,
                    state.Player.MaxHp
                    * PrototypeContent.Rules.RestHealPercent / 100);
                var player = state.Player with
                {
                    Hp = Math.Min(state.Player.MaxHp,
                        state.Player.Hp + healing)
                };
                player = ApplyRelicRunEvent(
                    player,
                    PrototypeRunEventKind.RestSiteHealed,
                    rng: state.Rng);
                return state with
                {
                    Player = player,
                    World = world with
                    {
                        Event = current with { NativePageIndex = 1 }
                    }
                };
            }
        }
        else if (current.NativePageIndex == 1 && choiceId == "fight")
        {
            var encounter = PrototypeContent.Encounter(
                PrototypeNativeDenseVegetation.EncounterId);
            return StartCombat(
                state with
                {
                    World = world with
                    {
                        Event = current with { NativePageIndex = 2 }
                    }
                },
                PrototypeRoomType.Combat,
                encounter);
        }

        throw new InvalidOperationException(
            $"Choice '{choiceId}' is invalid on Dense Vegetation page {current.NativePageIndex}.");
    }
}
