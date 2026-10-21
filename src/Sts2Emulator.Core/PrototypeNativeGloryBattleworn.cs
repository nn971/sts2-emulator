namespace Sts2Emulator.Core;

/// <summary>
/// Pinned Glory Battleworn Dummy event (v0.111.0). All three
/// training encounters are inert targets with a three-enemy-side
/// turn timer. Defeating them grants the setting-dependent reward;
/// letting them escape completes the event with no reward.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private static RunState StepNativeBattlewornDummy(
        RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        var evt = world.Event;
        if (world.Act != 3 || evt?.EventId !=
                PrototypeNativeGloryEvents.BattlewornDummyId
            || evt.NativePageIndex != 0)
            throw new InvalidOperationException(
                "Battleworn Dummy requires its initial Glory event page.");

        var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
        var (page, encounterId) = choice switch
        {
            "setting_1" => (1,
                PrototypeNativeGloryEvents.BattlewornV1EncounterId),
            "setting_2" => (2,
                PrototypeNativeGloryEvents.BattlewornV2EncounterId),
            "setting_3" => (3,
                PrototypeNativeGloryEvents.BattlewornV3EncounterId),
            _ => throw new InvalidOperationException(
                $"Unknown Battleworn Dummy setting '{choice}'.")
        };
        return StartCombat(state with
        {
            World = world with
            {
                Event = evt with
                {
                    ChosenChoiceId = choice,
                    NativePageIndex = page,
                    NativeEventRng = evt.NativeEventRng
                        ?? NativeEventContextRngV111.CreateAtEntry(
                            state,
                            PrototypeNativeGloryEvents
                                .BattlewornDummyNativeRngEventId)
                }
            }
        }, PrototypeRoomType.Combat, PrototypeContent.Encounter(encounterId));
    }

    private static RunState CompleteNativeBattlewornDummy(
        RunState state, CombatState combat)
    {
        var world = RequireWorld(state);
        var evt = world.Event
            ?? throw new InvalidOperationException(
                "Battleworn event combat has no active event.");
        if (evt.NativePageIndex is < 1 or > 3
            || evt.ChosenChoiceId is not
                ("setting_1" or "setting_2" or "setting_3"))
            throw new InvalidOperationException(
                "Battleworn resolution needs a selected setting.");

        // Escape is a successful combat termination but loses the
        // event's reward (RanOutOfTime flag in the native encounter).
        if (combat.Enemies.Any(enemy => enemy.Escaped))
            return SetBattlewornResultPage(state with
            {
                Phase = RunPhase.Event,
                World = world with { Combat = null }
            }, "defeat");

        switch (evt.NativePageIndex)
        {
            case 1:
            {
                // Native random unlocked potion: optional custom reward.
                var potionId = PrototypeNativePotionShop.PickWeighted(
                    state.Rng, "reward");
                var rewardState = state with
                {
                    World = world with
                    {
                        Combat = null,
                        Reward = new RewardState(
                            "Event:" + evt.EventId,
                            [], potionId, null,
                            CardResolved: true,
                            PotionResolved: false,
                            RelicResolved: true,
                            EndsAct: false,
                            IndependentSelection: true,
                            ExtraCardGroupsResolved: [],
                            ExtraRelicGroupsResolved: [])
                    },
                    Phase = RunPhase.Reward
                };
                return SetBattlewornResultPage(
                    rewardState, "victory");
            }
            case 2:
            {
                // StableShuffle from all currently upgradable deck cards.
                var selected = state.Player.Deck
                    .Where(card => CanSelectEventDeckCard(card,
                        PrototypePersistentDeckChoiceKind.Upgrade,
                        null, null, false))
                    .Select(card => card.InstanceId)
                    .ToArray();
                state = NativeEventContextRngV111.ShuffleInPlace(
                    state,
                    evt,
                    PrototypeNativeGloryEvents
                        .BattlewornDummyNativeRngEventId,
                    selected,
                    "event.battleworn.upgrade.shuffle_swap_index",
                    "event:battleworn:setting:2:eligible_cards");
                world = RequireWorld(state);
                evt = world.Event!;
                var upgraded = selected.Take(2).ToHashSet();
                var player = state.Player with
                {
                    Deck = state.Player.Deck.Select(card =>
                        upgraded.Contains(card.InstanceId)
                            ? card with { UpgradeLevel = card.UpgradeLevel + 1 }
                            : card).ToArray()
                };
                return SetBattlewornResultPage(state with
                {
                    Player = player,
                    Phase = RunPhase.Event,
                    World = world with { Combat = null }
                }, "victory");
            }
            case 3:
            {
                // Native PullNextRelicFromFront, offered via custom rewards.
                var nativeRunRng = UsesNativeRegionalSystems(world)
                    ? state.NativeRunRng?.Fork()
                    : null;
                var drawn = PrototypeNativeRelicGrabBag.Draw(
                    world, state.Player, state.Rng, merchant: false,
                    nativeRunRng: nativeRunRng);
                if (drawn.Id is null)
                    return SetBattlewornResultPage(state with
                    {
                        Phase = RunPhase.Event,
                        Stochastic = RunStochasticStateV111.WithNativeRunRng(
                            state.Stochastic, nativeRunRng ?? state.NativeRunRng),
                        World = drawn.World with { Combat = null }
                    }, "victory");
                var rewardState = state with
                {
                    Stochastic = RunStochasticStateV111.WithNativeRunRng(
                        state.Stochastic, nativeRunRng ?? state.NativeRunRng),
                    World = drawn.World with
                    {
                        Combat = null,
                        Reward = new RewardState(
                            "Event:" + evt.EventId,
                            [], null, drawn.Id,
                            CardResolved: true,
                            PotionResolved: true,
                            RelicResolved: false,
                            EndsAct: false,
                            IndependentSelection: true,
                            ExtraCardGroupsResolved: [],
                            ExtraRelicGroupsResolved: [])
                    },
                    Phase = RunPhase.Reward
                };
                return SetBattlewornResultPage(
                    rewardState, "victory");
            }
            default:
                throw new InvalidOperationException(
                    "Unknown Battleworn Dummy difficulty.");
        }
    }

    private static RunState SetBattlewornResultPage(
        RunState state, string result) =>
        PreserveNativeEventCompletionPage(state, result);
}
