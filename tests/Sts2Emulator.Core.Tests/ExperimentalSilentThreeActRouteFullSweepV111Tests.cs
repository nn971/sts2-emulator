using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Exercise actual engine room/reward/transition plumbing from Hive entry
/// through Glory victory, with explicitly synthetic combat finishers.
/// This tests route reachability and persistence, NOT unassisted combat
/// victories or native game parity. The Act 1 boss boundary is injected.
/// </summary>
public sealed class ExperimentalSilentThreeActRouteFullSweepV111Tests
{
    private static readonly PrototypeGameEngine Engine = new();

    private static RunState HiveEntry(string seed)
    {
        var config = RunConfiguration.Create() with
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
        // The Act 1 boss is outside this route integration regression.
        state = state with { Phase = RunPhase.ActTransition };
        return Engine.Step(
            state, GameAction.Empty("continue_act")).State;
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
            Energy = Math.Max(9, combat.Energy),
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
        if (action is null)
        {
            // An assigned Ancient can add Enthralled, which blocks other
            // manual plays while it remains in hand. Spend that card first;
            // the next synthetic finish step can use Exterminate normally.
            action = allActions.FirstOrDefault(option =>
                option.Kind == "play_card" && combat.Cards.Single(card =>
                    card.InstanceId == option.ReadPayload<PlayCardPayload>()
                        .CardInstanceId).CardId is var cardId &&
                PrototypeContent.Card(cardId)
                    .PreventsOtherManualCardPlaysWhileInHand);
            if (action is not null)
                return Engine.Step(prepared, action).State;
        }
        if (action is null)
            throw new InvalidOperationException(
                $"Synthetic finisher unavailable on act {prepared.World!.Act} " +
                $"floor {prepared.World.Floor}, turn {updated.Turn}, " +
                $"energy {updated.Energy}, hand {updated.Hand.Length}; " +
                $"energy {updated.Energy}, hand {updated.Hand.Length}; " +
                $"available: {string.Join(",", allActions.Select(a => a.Kind))}.");
        return Engine.Step(prepared, action).State;
    }

    [Theory]
    [InlineData("silent-route-full-synthetic-one")]
    [InlineData("silent-route-full-synthetic-two")]
    [InlineData("silent-route-full-synthetic-three")]
    [InlineData("silent-route-full-synthetic-four")]
    [InlineData("silent-route-full-synthetic-five")]
    [InlineData("silent-route-full-synthetic-six")]
    [InlineData("silent-route-full-synthetic-seven")]
    [InlineData("silent-route-full-synthetic-eight")]
    [InlineData("silent-route-full-synthetic-nine")]
    [InlineData("silent-route-full-synthetic-ten")]
    public void SyntheticCombatFinishersExerciseBothLaterActsToVictory(
        string seed)
    {
        var state = HiveEntry(seed);
        var route = new List<int> { state.World!.Act };
        var encounters = new Dictionary<int, int>();
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
                    consecutiveCombatFinishers++;
                    Assert.True(consecutiveCombatFinishers <= 25,
                        $"A combat failed to resolve after synthetic finishers: " +
                        $"act={state.World.Act} floor={state.World.Floor} " +
                        $"encounter={state.World.EncounterIds.LastOrDefault()} " +
                        $"enemies={string.Join(",", state.World.Combat.Enemies.Select(e =>
                            e.EnemyId + ":" + e.Hp + "/" + e.MaxHp))}.");
                    encounters[state.World.Act] =
                        encounters.GetValueOrDefault(state.World.Act) + 1;
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
                                        PrototypeNativeGloryBosses.QueenEncounterId
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
        Assert.Equal("victory", state.World!.TerminalOutcome);
        Assert.Equal(new[] { 2, 3 }, route);
        Assert.True(encounters.ContainsKey(2));
        Assert.True(encounters.ContainsKey(3));
        Assert.Equal(PrototypeNativeLaterActMap.RoomRows(2) + 1,
            state.World.CompletedRooms.Count(r => r.Act == 2));
        Assert.Equal(PrototypeNativeLaterActMap.RoomRows(3) + 1,
            state.World.CompletedRooms.Count(r => r.Act == 3));
    }
}
