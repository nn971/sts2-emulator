using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Exercise actual engine room/reward/transition plumbing from either Act 1
/// starting region through Glory victory, with explicitly synthetic combat
/// finishers. This tests route reachability and persistence, NOT unassisted
/// combat victories or native game parity.
/// </summary>
public sealed class ExperimentalSilentThreeActRouteFullSweepV111Tests
{
    private static readonly PrototypeGameEngine Engine = new();

    private static RunState NewRun(
        string seed, ActIdentity firstAct, int ascension = 0)
    {
        var config = RunConfiguration.Create(
            firstAct: firstAct, ascension: ascension) with
        {
            FidelityId = V111Build.ExperimentalSilentThreeActRouteFidelityId
        };
        var state = Engine.Step(
            V111RunFactory.Create(seed, config),
            GameAction.Empty("start_run")).State;
        for (var i = 0; i < 30
            && state.Phase is RunPhase.Event or RunPhase.Reward; i++)
            state = Engine.Step(state, Engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        return state;
    }

    private static RunState FinishFightWithSyntheticAllEnemyCard(
        RunState state)
    {
        var combat = state.World!.Combat!;
        var instanceId = combat.NextCardInstanceId;
        var card = new CombatCardInstance(
            instanceId, null,
            PrototypeNativeLaterActs.ExterminateId,
            0, true, PrototypeJson.EmptyObject());
        var updated = combat with
        {
            Energy = Math.Max(3, combat.Energy),
            Enemies = combat.Enemies.Select(enemy =>
                enemy.Hp <= 0 ? enemy with { } : enemy with
                {
                    Hp = 1,
                    Block = 0
                }).ToArray(),
            Hand = combat.Hand.Append(instanceId).ToArray(),
            Cards = combat.Cards.Append(card).ToArray(),
            NextCardInstanceId = instanceId + 1
        };
        var prepared = state with
        {
            World = state.World with { Combat = updated }
        };
        var allActions = Engine.GetLegalActions(prepared);
        var action = allActions.FirstOrDefault(option =>
            option.Kind == "play_card"
            && option.ReadPayload<PlayCardPayload>().CardInstanceId
                == instanceId);
        // Opening Ancient autoplays can consume the held Normality or relic
        // play limit before this synthetic card is inserted. Advance the real
        // turn to reset those limits and try the finisher on the next pass.
        if (action is null && allActions.Any(option => option.Kind == "end_turn"))
            return Engine.Step(state, GameAction.Empty("end_turn")).State;
        if (action is null)
            throw new InvalidOperationException(
                $"Synthetic finisher unavailable on act {prepared.World!.Act} " +
                $"floor {prepared.World.Floor}, turn {updated.Turn}, " +
                $"energy {updated.Energy}, hand {updated.Hand.Length}; " +
                $"available: {string.Join(",", allActions.Select(a => a.Kind))}; " +
                $"powers: {string.Join(",", updated.PlayerPowers.Select(p => p.PowerId))}.");
        return Engine.Step(prepared, action).State;
    }

    private static GameAction ChooseEventAction(
        RunState state, IReadOnlyList<GameAction> actions)
    {
        // This route probe should preserve the run while exercising event
        // dispatch. Abyssal Baths exposes legal, potentially fatal repeated
        // immersions, so take the safe exit on each page.
        if (state.World?.Event is { } evt
            && evt.EventId == "proto.native.underdocks.abyssal_baths")
        {
            var preferred = evt.NativePageIndex == 0 ? "abstain" : "exit";
            return actions.Single(action =>
                action.ReadPayload<EventChoicePayload>().ChoiceId == preferred);
        }

        return actions[0];
    }

    [Theory]
    [InlineData("silent-overgrowth-route-one", ActIdentity.Overgrowth, 0)]
    [InlineData("silent-overgrowth-route-two", ActIdentity.Overgrowth, 0)]
    [InlineData("silent-overgrowth-route-three", ActIdentity.Overgrowth, 0)]
    [InlineData("silent-underdocks-route-one", ActIdentity.Underdocks, 0)]
    [InlineData("silent-underdocks-route-two", ActIdentity.Underdocks, 0)]
    [InlineData("silent-underdocks-route-three", ActIdentity.Underdocks, 0)]
    [InlineData("silent-overgrowth-route-a10-double-boss", ActIdentity.Overgrowth, 10)]
    [InlineData("silent-underdocks-route-a10-double-boss", ActIdentity.Underdocks, 10)]
    public void SyntheticCombatFinishersExerciseBothActOneRoutesThroughGloryVictory(
        string seed, ActIdentity firstAct, int ascension)
    {
        var state = NewRun(seed, firstAct, ascension);
        var route = new List<int> { state.World!.Act };
        var encounters = new Dictionary<int, int>();
        var bossEncounters = new List<string>();
        var decisions = 0;
        var consecutiveCombatFinishers = 0;
        while (state.Phase != RunPhase.Terminal && decisions++ < 750)
        {
            PrototypeStateInvariants.Validate(state);
            var actions = Engine.GetLegalActions(state);
            Assert.NotEmpty(actions);
            switch (state.Phase)
            {
                case RunPhase.MapChoice:
                    consecutiveCombatFinishers = 0;
                    state = Engine.Step(state,
                        actions.First(a => a.Kind == "choose_map_node")).State;
                    break;
                case RunPhase.Combat:
                    // Some source-backed cards/relics suspend the opening
                    // combat pipeline for a legal card selection. Resolve
                    // that existing decision before injecting a finisher.
                    if (state.World!.Combat!.PendingChoice is not null)
                    {
                        state = Engine.Step(state, actions[0]).State;
                        break;
                    }
                    // Blood-Soaked Rose can put Enthralled in the hand.
                    // Its compulsory play must settle before another manual
                    // card, including this fixture's synthetic finisher.
                    var compulsory = actions.FirstOrDefault(action =>
                        action.Kind == "play_card"
                        && PrototypeContent.Card(state.World.Combat.Cards.Single(card =>
                            card.InstanceId == action.ReadPayload<PlayCardPayload>()
                                .CardInstanceId).CardId)
                            .PreventsOtherManualCardPlaysWhileInHand);
                    if (compulsory is not null)
                    {
                        state = Engine.Step(state, compulsory).State;
                        break;
                    }
                    consecutiveCombatFinishers++;
                    Assert.True(consecutiveCombatFinishers <= 25,
                        $"A combat failed to resolve after synthetic finishers: " +
                        $"act={state.World.Act} floor={state.World.Floor} " +
                        $"encounter={state.World.EncounterIds.LastOrDefault()} " +
                        $"enemies={string.Join(",", state.World.Combat.Enemies.Select(e =>
                            e.EnemyId + ":" + e.Hp + "/" + e.MaxHp))}.");
                    encounters[state.World.Act] =
                        encounters.GetValueOrDefault(state.World.Act) + 1;
                    if (state.World.ActiveRoom == PrototypeRoomType.Boss)
                        bossEncounters.Add(state.World.EncounterIds.Last());
                    state = FinishFightWithSyntheticAllEnemyCard(state);
                    break;
                case RunPhase.Reward:
                {
                    var action = actions.FirstOrDefault(a =>
                        a.Kind == "leave_reward")
                        ?? actions.FirstOrDefault(a =>
                            a.Kind == "skip_reward_card")
                        ?? actions.FirstOrDefault(a =>
                            a.Kind == "skip_reward_potion")
                        ?? actions[0];
                    state = Engine.Step(state, action).State;
                    break;
                }
                case RunPhase.Shop:
                    state = Engine.Step(state,
                        actions.FirstOrDefault(a =>
                            a.Kind == "leave_shop") ?? actions[0]).State;
                    break;
                case RunPhase.Event:
                    state = Engine.Step(state,
                        ChooseEventAction(state, actions)).State;
                    break;
                case RunPhase.Rest:
                    state = Engine.Step(state, actions[0]).State;
                    break;
                case RunPhase.ActTransition:
                    state = Engine.Step(state,
                        GameAction.Empty("continue_act")).State;
                    route.Add(state.World!.Act);
                    // The synthetic victory sweep is a progression
                    // smoke, not a boss resurrection mechanics test.
                    // Pin Queen here; Test Subject's two revivals and
                    // Nemesis are separately covered by boss tests.
                    if (state.World.Act == 3)
                        state = state with
                        {
                            World = state.World with
                            {
                                LaterActEncounterBag = state.World
                                    .LaterActEncounterBag! with
                                {
                                    BossEncounterId =
                                        PrototypeNativeGloryBosses.QueenEncounterId,
                                    SecondBossEncounterId = ascension >= 10
                                        ? PrototypeNativeGloryBosses.AeonglassEncounterId
                                        : null
                                }
                            }
                        };
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unexpected route phase {state.Phase}.");
            }
            PrototypeStateInvariants.Validate(state);
        }

        Assert.True(decisions < 750, "The whole-route smoke exceeded the decision budget.");
        Assert.Equal(RunPhase.Terminal, state.Phase);
        Assert.True(state.World!.TerminalOutcome == "victory",
            $"{firstAct} route ended as {state.World.TerminalOutcome} at " +
            $"act {state.World.Act}, floor {state.World.Floor}, " +
            $"HP {state.Player.Hp}/{state.Player.MaxHp}; " +
            $"events={string.Join(",", state.World.EventIds)}; " +
            $"rooms={state.World.CompletedRooms.Length}.");
        Assert.Equal(new[] { 1, 2, 3 }, route);
        Assert.True(encounters.ContainsKey(1));
        Assert.True(encounters.ContainsKey(2));
        Assert.True(encounters.ContainsKey(3));
        Assert.Equal(PrototypeNativeLaterActMap.RoomRows(2) + 1,
            state.World.CompletedRooms.Count(r => r.Act == 2));
        Assert.Equal(PrototypeNativeLaterActMap.RoomRows(3) + 1,
            state.World.CompletedRooms.Count(r => r.Act == 3)
                - (ascension >= 10 ? 1 : 0));
        var gloryBossEncounters = bossEncounters.Where(id =>
            ExperimentalSilentThreeActRouteV111.GloryBosses
                .Contains(id, StringComparer.Ordinal)).ToArray();
        Assert.Equal(ascension >= 10
                ? new[] { PrototypeNativeGloryBosses.QueenEncounterId,
                    PrototypeNativeGloryBosses.AeonglassEncounterId }
                : [PrototypeNativeGloryBosses.QueenEncounterId],
            gloryBossEncounters);
    }
}
