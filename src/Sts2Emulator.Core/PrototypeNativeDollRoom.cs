namespace Sts2Emulator.Core;

/// <summary>
/// Source-shaped Hive event from DollRoom.cs. The two paid paths damage the
/// player before publishing their relic offers; all three relics keep their
/// own run/combat hooks after the event.
/// </summary>
public static class PrototypeNativeDollRoom
{
    public const string EventId = "proto.native.hive.doll_room";
    public const string DaughterOfTheWindId =
        "proto.native.event.daughter_of_the_wind";
    public const string MrStrugglesId = "proto.native.event.mr_struggles";
    public const string BingBongId = "proto.native.event.bing_bong";

    internal static readonly string[] RelicChoiceIds =
    [
        "daughter_of_the_wind",
        "mr_struggles",
        "fable"
    ];

    public static string[] CoveredBranches { get; } =
    [
        "initial/EXAMINE",
        "initial/RANDOM",
        "initial/TAKE_SOME_TIME",
        "page/DAUGHTER_OF_WIND",
        "page/EXAMINE",
        "page/FABLE",
        "page/INITIAL",
        "page/INITIAL/EXAMINE",
        "page/INITIAL/RANDOM",
        "page/INITIAL/TAKE_SOME_TIME",
        "page/MR_STRUGGLES",
        "page/TAKE",
        "page/TAKE/TAKE",
        "page/TAKE_SOME_TIME"
    ];

    public static PrototypeEventDefinition Definition { get; } = new(
        EventId,
        "Doll Room",
        [
            new("random", "Choose a random doll", []),
            new("take_some_time", "Lose 5 HP to choose one of two dolls",
                [new(PrototypeRunEffectKind.LoseHp, 5)]),
            new("examine", "Lose 15 HP to choose any doll",
                [new(PrototypeRunEffectKind.LoseHp, 15)]),
            new("daughter_of_the_wind", "Take Daughter of the Wind",
                [new(PrototypeRunEffectKind.GainRelic,
                    RelicId: DaughterOfTheWindId)]),
            new("mr_struggles", "Take Mr. Struggles",
                [new(PrototypeRunEffectKind.GainRelic,
                    RelicId: MrStrugglesId)]),
            new("fable", "Take Bing Bong",
                [new(PrototypeRunEffectKind.GainRelic,
                    RelicId: BingBongId)])
        ],
        MinAct: 2,
        MaxAct: 2);

    public static PrototypeRelicDefinition[] Relics { get; } =
    [
        new(
            DaughterOfTheWindId,
            "Daughter of the Wind",
            Triggers:
            [
                new(
                    PrototypeCombatEventKind.CardPlayed,
                    [new(PrototypeCombatEffectKind.GainPlayerBlock, 1)],
                    RequiredSourceCardType: PrototypeCardType.Attack)
            ]),
        new(
            MrStrugglesId,
            "Mr. Struggles",
            Triggers:
            [
                new(
                    PrototypeCombatEventKind.OpeningPlayerTurnStarted,
                    [new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        0,
                        Target: PrototypeEffectTarget.AllEnemies,
                        AmountPerCount: 1,
                        CountKind: PrototypeCombatCountKind.TurnNumber)]),
                new(
                    PrototypeCombatEventKind.PlayerTurnStarted,
                    [new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        0,
                        Target: PrototypeEffectTarget.AllEnemies,
                        AmountPerCount: 1,
                        CountKind: PrototypeCombatCountKind.TurnNumber)])
            ]),
        new(
            BingBongId,
            "Bing Bong",
            RunTriggers:
            [
                new(
                    PrototypeRunEventKind.CardAdded,
                    [],
                    CopyAddedCardToDeckBottom: true)
            ])
    ];

    public static bool IsSupported(string eventId) =>
        StringComparer.Ordinal.Equals(eventId, EventId);

    public static string RelicIdForChoice(string choiceId) => choiceId switch
    {
        "daughter_of_the_wind" => DaughterOfTheWindId,
        "mr_struggles" => MrStrugglesId,
        "fable" => BingBongId,
        _ => throw new InvalidOperationException(
            $"Doll Room reward '{choiceId}' is unavailable.")
    };

}

public sealed partial class PrototypeGameEngine
{
    private static IReadOnlyList<GameAction> GetNativeDollRoomActions(
        EventState eventState)
    {
        if (eventState.NativePageIndex == 0)
        {
            return
            [
                GameAction.Create("event_choice",
                    new EventChoicePayload("random")),
                GameAction.Create("event_choice",
                    new EventChoicePayload("take_some_time")),
                GameAction.Create("event_choice",
                    new EventChoicePayload("examine"))
            ];
        }

        if (eventState.NativePageIndex != 1
            || eventState.OfferedChoiceIds is not { Length: > 0 } offered
            || offered.Any(choice => !PrototypeNativeDollRoom
                .RelicChoiceIds.Contains(
                choice, StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "Doll Room has no valid relic selection in progress.");
        }

        return offered.Select(choice => GameAction.Create(
            "event_choice", new EventChoicePayload(choice))).ToArray();
    }

    private static RunState StepNativeDollRoom(
        RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        var eventState = world.Event
            ?? throw new InvalidOperationException(
                "Doll Room has no active event.");
        if (world.Act != 2 || eventState.EventId !=
            PrototypeNativeDollRoom.EventId)
        {
            throw new InvalidOperationException(
                "Doll Room requires its Act 2 event fixture.");
        }

        var choiceId = action.ReadPayload<EventChoicePayload>().ChoiceId;
        if (eventState.NativePageIndex == 0)
        {
            if (choiceId == "random")
            {
                var reward = PrototypeNativeDollRoom.RelicChoiceIds[
                    PrototypeRng.NextInt(state.Rng, "event",
                        PrototypeNativeDollRoom.RelicChoiceIds.Length)];
                return ObtainDoll(state, eventState, reward);
            }

            var hpLoss = choiceId switch
            {
                "take_some_time" => 5,
                "examine" => 15,
                _ => throw new InvalidOperationException(
                    $"Doll Room choice '{choiceId}' is unavailable.")
            };
            var player = ApplyPlayerHpLossAndDeathPrevention(
                state.Player, hpLoss);
            state = state with { Player = player };
            if (player.Hp == 0)
                return EndRun(state, "defeat");

            var offers = (string[])
                PrototypeNativeDollRoom.RelicChoiceIds.Clone();
            PrototypeRng.Shuffle(state.Rng, "event", offers);
            if (choiceId == "take_some_time")
                offers = offers.Take(2).ToArray();

            return state with
            {
                World = world with
                {
                    Event = eventState with
                    {
                        NativePageIndex = 1,
                        OfferedChoiceIds = offers
                    }
                }
            };
        }

        if (eventState.NativePageIndex != 1
            || eventState.OfferedChoiceIds?.Contains(
                choiceId, StringComparer.Ordinal) != true)
        {
            throw new InvalidOperationException(
                $"Doll Room reward '{choiceId}' was not offered.");
        }

        return ObtainDoll(state, eventState, choiceId);
    }

    private static RunState ObtainDoll(
        RunState state, EventState eventState, string choiceId)
    {
        var world = RequireWorld(state);
        var relicId = PrototypeNativeDollRoom
            .RelicIdForChoice(choiceId);
        var player = state.Player with
        {
            Relics = state.Player.Relics.Append(new RelicInstance(
                relicId, PrototypeJson.EmptyObject())).ToArray()
        };
        var nextCardInstanceId = world.NextCardInstanceId;
        var nativeRunRng = state.NativeRunRng?.Fork();
        player = ApplyRelicRunEvent(
            player,
            PrototypeRunEventKind.RelicAcquired,
            acquiredRelicId: relicId,
            rng: state.Rng,
            world: world,
            allocateCardId: () => nextCardInstanceId++,
            nativeRunRng: nativeRunRng);
        state = state with
        {
            Player = player,
            NativeRunRng = nativeRunRng ?? state.NativeRunRng,
            World = world with
            {
                NextCardInstanceId = nextCardInstanceId,
                Event = eventState with { OfferedChoiceIds = null }
            }
        };
        return PreserveNativeEventCompletionPage(state, choiceId);
    }
    }
