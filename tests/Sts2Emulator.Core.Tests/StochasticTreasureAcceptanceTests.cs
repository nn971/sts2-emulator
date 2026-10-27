using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>Source-independent acceptance for the configured Treasure draw.</summary>
public sealed class StochasticTreasureAcceptanceTests
{
    [Fact]
    public void NativeTreasureEntryReplaysRarityAndRelicIdentity()
    {
        var origin = NativeTreasureEntryOrigin();
        var engine = new PrototypeGameEngine();
        var actions = engine.GetLegalActions(origin);
        Assert.NotEmpty(actions);
        var action = actions[0];
        Assert.Equal("choose_map_node", action.Kind);

        var compactLaw = ChanceLawEngineV111.QueryNativeTreasureRelicDraw(
            origin, "treasure.relic_grab_bag.draw", 0);
        Assert.Equal("treasure.relic_grab_bag.draw", compactLaw.SiteId);
        Assert.Equal(ChanceLawV111.UnverifiedStatus,
            compactLaw.ProbabilityStatus);
        Assert.NotEmpty(compactLaw.Outcomes);

        var originHash = CanonicalJson.Sha256(origin);
        var nativeRngHash = CanonicalJson.Sha256(
            origin.Stochastic.NativeRunRng);
        var session = ReferenceNatureTreasureSessionV111.Begin(
            origin, action, 0x175UL);
        var sites = new List<string>();
        var restored = false;

        for (var boundary = 0; boundary < 16 && !session.Complete; boundary++)
        {
            var law = Assert.IsType<ChanceLawV111>(session.PendingLaw);
            Assert.Equal(
                law.SiteId == "relic_grab_bag.rarity.unit_float"
                    ? ChanceLawV111.SourceVerifiedStatus
                    : ChanceLawV111.UnverifiedStatus,
                law.ProbabilityStatus);
            sites.Add(law.SiteId);
            if (!restored)
            {
                session = ReferenceNatureTreasureSessionV111.Restore(
                    ReferenceNatureTreasureSessionV111.Save(session));
                restored = true;
            }
            session = session.ResolveNext();
        }

        Assert.True(restored);
        Assert.True(session.Complete,
            $"Treasure entry stopped at {session.PendingLaw?.SiteId}.");
        Assert.Equal(
            new[]
            {
                "relic_grab_bag.rarity.unit_float",
                "treasure.relic_grab_bag.model.index_draw"
            },
            sites);
        Assert.Equal(originHash, CanonicalJson.Sha256(origin));
        Assert.Equal(nativeRngHash, CanonicalJson.Sha256(
            session.Run.Stochastic.NativeRunRng));
        Assert.Equal(2, session.DecisionTranscript.Length);

        var obtainedRelicId = Assert.IsType<string>(
            session.Run.World!.Reward!.RelicOption);
        Assert.Contains(compactLaw.Outcomes, outcome =>
            outcome.OutcomeId == "relic:" + obtainedRelicId);
        var takeRelic = engine.GetLegalActions(session.Run).Single(action =>
            action.Kind == "take_reward_relic");
        var acquired = engine.Step(session.Run, takeRelic).State;
        Assert.Contains(acquired.Player.Relics, relic =>
            relic.RelicId == obtainedRelicId);
        PrototypeStateInvariants.Validate(acquired);
    }

    private static RunState NativeTreasureEntryOrigin()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeContentTestSupport.ConfiguredNativeRun("native-ci");
        for (var step = 0; step < 2_000; step++)
        {
            if (state.Phase == RunPhase.MapChoice && state.World!.Floor == 8)
            {
                var treasureActions = engine.GetLegalActions(state);
                Assert.NotEmpty(treasureActions);
                Assert.All(treasureActions, action =>
                    Assert.Equal(PrototypeRoomType.Treasure,
                        state.World.Map.Nodes.Single(node =>
                            node.NodeId == action.ReadPayload<ChooseMapNodePayload>()
                                .NodeId).RoomType));
                return state;
            }

            var legal = engine.GetLegalActions(state);
            Assert.NotEmpty(legal);
            state = engine.Step(state, ChooseAction(state, legal)).State;
            PrototypeStateInvariants.Validate(state);
            Assert.NotEqual(RunPhase.Terminal, state.Phase);
        }

        throw new InvalidOperationException(
            "Configured run did not reach its floor 9 Treasure choice.");
    }

    private static GameAction ChooseAction(
        RunState state, IReadOnlyList<GameAction> legal)
    {
        if (state.Phase == RunPhase.Rest)
        {
            return state.Player.Hp * 2 < state.Player.MaxHp
                ? legal.FirstOrDefault(action => action.Kind == "rest_heal")
                    ?? legal[0]
                : legal.FirstOrDefault(action => action.Kind == "rest_upgrade")
                    ?? legal[0];
        }

        if (state.Phase == RunPhase.Shop)
            return legal.FirstOrDefault(action => action.Kind == "leave_shop")
                ?? legal[0];

        if (state.Phase == RunPhase.Combat)
        {
            return legal.FirstOrDefault(action => action.Kind == "select_cards")
                ?? legal.FirstOrDefault(action => action.Kind == "use_potion")
                ?? legal.FirstOrDefault(action => action.Kind == "play_card")
                ?? legal.FirstOrDefault(action => action.Kind == "end_turn")
                ?? legal[0];
        }

        if (state.Phase == RunPhase.Reward)
        {
            return legal.FirstOrDefault(action => action.Kind == "take_reward_card")
                ?? legal.FirstOrDefault(action => action.Kind == "take_reward_relic")
                ?? legal.FirstOrDefault(action => action.Kind == "take_reward_potion")
                ?? legal.FirstOrDefault(action => action.Kind == "leave_reward")
                ?? legal[0];
        }

        return legal[0];
    }
}
