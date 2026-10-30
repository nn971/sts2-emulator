using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Core.Tests;

public sealed class H15ControlledCombatSessionV1Tests
{
    private static RunState NativeStructureEntry()
    {
        var engine = new PrototypeGameEngine();
        var source = PrototypeNativeOvergrowthRunFactory.Create(
            "native-entry");
        for (var step = 0;
             step < 30 && source.Phase is RunPhase.Event or RunPhase.Reward;
             step++)
            source = engine.Step(
                source, engine.GetLegalActions(source)[0]).State;
        Assert.Equal(RunPhase.MapChoice, source.Phase);
        source = engine.Step(
            source, engine.GetLegalActions(source)
                .First(action => action.Kind == "choose_map_node")).State;
        Assert.Equal(RunPhase.Combat, source.Phase);
        Assert.Null(source.World!.Combat!.NativeRng);
        return source;
    }

    [Fact]
    public void BeginKeepsRealCombatMechanicsAndSeparateNatureOwnership()
    {
        var source = NativeStructureEntry();
        var originalHash = CanonicalJson.Sha256(source);
        var frame = new PrototypeAiEnvironment().Observe(source);
        var session = H15ControlledCombatSessionV1.Begin(
            source, "h15-independent-experimental-seed");
        Assert.Equal("player", session.BoundaryKind);
        Assert.Equal(H15NativeCombatEntryCandidateV1.PrototypeCandidateModelId,
            session.CandidateModelId);
        Assert.Equal("unavailable",
            H15ControlledCombatSessionV1.FairContinuationCapability);
        Assert.Equal(frame.ObservationHash,
            new PrototypeAiEnvironment().Observe(session.Run).ObservationHash);
        Assert.Null(session.Run.World!.Combat!.NativeRng);
        Assert.Equal(SemanticDeckStateV111.NatureMode,
            session.Run.World.Combat.SemanticDeck!.Mode);
        Assert.Equal(originalHash, CanonicalJson.Sha256(source));
        Assert.Throws<InvalidOperationException>(() =>
            session.EndTurn("not-a-legal-end-turn"));
        Assert.Equal(0L, session.DecisionVersion);
    }

    [Fact]
    public void RealEndTurnIsEitherTypedOrExplicitlyUnsupported()
    {
        var source = NativeStructureEntry();
        var session = H15ControlledCombatSessionV1.Begin(
            source, "h15-experimental-endturn-seed");
        var legal = new PrototypeAiEnvironment().Observe(session.Run)
            .LegalActions.Single(action => action.Kind == "end_turn");
        try
        {
            var advanced = session.EndTurn(legal.ActionId);
            Assert.Equal(1L, advanced.DecisionVersion);
            Assert.Equal("player", session.BoundaryKind);
            Assert.Null(session.PendingTurn);
            if (advanced.BoundaryKind is "enemy_action" or "enemy_intent"
                or "draw_card" or "random_target")
            {
                var law = advanced.PendingLaw();
                Assert.Equal(ChanceLawV111.UnverifiedStatus,
                    law.ProbabilityStatus);
                Assert.NotEmpty(law.Outcomes);
                law.Validate();
                var original = advanced.DecisionVersion;
                Assert.Throws<InvalidOperationException>(() =>
                    advanced.Choose(
                        law with { EventId = "stale-event" },
                        law.Outcomes[0].OutcomeId));
                Assert.Equal(original, advanced.DecisionVersion);
            }
            else
                Assert.Contains(advanced.BoundaryKind,
                    new[] { "player", "terminal", "unsupported" });
            Assert.Equal("unavailable",
                H15ControlledCombatSessionV1.FairContinuationCapability);
        }
        catch (NotSupportedException unsupported)
        {
            // The controlled chance guard must refuse any still-unowned
            // gameplay RNG call. No legacy RNG fallback is permitted.
            Assert.Contains("UNSUPPORTED_", unsupported.Message);
        }
    }
}
