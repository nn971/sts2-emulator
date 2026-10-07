using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeAiEnvironmentTests
{
    [Fact]
    public void StableActionIdsAreDeterministicAndUnique()
    {
        var environment = new PrototypeAiEnvironment();
        var state = environment.Reset("ai-action-test");

        var first = environment.Observe(state);
        var second = environment.Observe(state);

        Assert.Equal(PrototypeAiEnvironment.SchemaId, first.SchemaId);
        Assert.Equal(PrototypeContent.RulesetId, first.Observation.RulesetId);
        Assert.Equal(PrototypeContent.CharacterId, first.Observation.CharacterId);
        Assert.Equal(
            first.LegalActions.Select(action => action.ActionId),
            second.LegalActions.Select(action => action.ActionId));
        Assert.Equal(
            first.LegalActions.Length,
            first.LegalActions.Select(action => action.ActionId).Distinct().Count());
        Assert.Equal(first.ObservationHash, second.ObservationHash);
        Assert.Equal(first.CanonicalStateHash, second.CanonicalStateHash);
    }

    [Fact]
    public void ResetCarriesTypedAscension()
    {
        var environment = new PrototypeAiEnvironment();
        var state = environment.Reset(
            "ai-ascension-test",
            ascension: 9);

        Assert.Equal(9, state.Ascension);

        var fork = environment.Fork(state);
        Assert.Equal(9, fork.Ascension);
        Assert.Equal(
            CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(fork));
    }

    [Fact]
    public void StepByStableActionMatchesCanonicalEngine()
    {
        var environment = new PrototypeAiEnvironment();
        var canonicalEngine = new PrototypeGameEngine();
        var state = environment.Reset("ai-step-test");

        var frame = environment.Observe(state);
        var selected = Assert.Single(frame.LegalActions);
        var canonicalAction = Assert.Single(canonicalEngine.GetLegalActions(state));

        var throughAdapter = environment.Step(state, selected.ActionId).State;
        var direct = canonicalEngine.Step(state, canonicalAction).State;

        Assert.Equal(
            CanonicalJson.Sha256(direct),
            CanonicalJson.Sha256(throughAdapter));
    }

    [Fact]
    public void ObservationExposesMapAndVisibleCombatWithoutRngOrDrawOrder()
    {
        var environment = new PrototypeAiEnvironment();
        var canonicalEngine = new PrototypeGameEngine();
        var state = environment.Reset("ai-observation-test");

        state = canonicalEngine.Step(
            state,
            Assert.Single(canonicalEngine.GetLegalActions(state))).State;

        var mapFrame = environment.Observe(state);
        Assert.NotEmpty(mapFrame.Observation.Map);
        Assert.Null(mapFrame.Observation.Combat);

        var firstCombatAction = canonicalEngine.GetLegalActions(state)
            .First(action =>
            {
                var payload = action.ReadPayload<ChooseMapNodePayload>();
                return state.World!.Map.AvailableNodes()
                    .Single(node => node.NodeId == payload.NodeId)
                    .RoomType == PrototypeRoomType.Combat;
            });
        state = canonicalEngine.Step(state, firstCombatAction).State;

        var combatFrame = environment.Observe(state);
        var combat = Assert.IsType<PrototypeAiCombat>(combatFrame.Observation.Combat);

        Assert.Equal(state.World!.Combat!.DrawPile.Length, combat.DrawPileCount);
        Assert.Equal(state.World.Combat.Hand.Length, combat.Hand.Length);
        Assert.All(combat.Enemies, enemy => Assert.NotNull(enemy.MoveId));

        var observationJson = CanonicalJson.Serialize(combatFrame.Observation);
        Assert.DoesNotContain(state.RunSeed, observationJson, StringComparison.Ordinal);
        Assert.DoesNotContain("rng", observationJson, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void ExpandReturnsIndependentSuccessorsForEveryLegalAction()
    {
        var environment = new PrototypeAiEnvironment();
        var state = environment.Reset("ai-expand-test");

        var frame = environment.Observe(state);
        var expansions = environment.Expand(state);

        Assert.Equal(frame.LegalActions.Length, expansions.Length);
        Assert.Equal(
            frame.LegalActions.Select(action => action.ActionId).Order(StringComparer.Ordinal),
            expansions.Select(item => item.Action.ActionId).Order(StringComparer.Ordinal));

        Assert.All(
            expansions,
            expansion =>
            {
                Assert.NotEqual(
                    CanonicalJson.Sha256(state),
                    expansion.CanonicalStateHash);
                Assert.Equal(
                    expansion.CanonicalStateHash,
                    CanonicalJson.Sha256(expansion.State));
            });

        Assert.Equal(0, state.DecisionIndex);
    }

    [Fact]
    public void ForkProducesIndependentSearchBranch()
    {
        var environment = new PrototypeAiEnvironment();
        var state = environment.Reset("ai-fork-test");
        var originalHash = CanonicalJson.Sha256(state);

        var branch = environment.Fork(state);
        var action = Assert.Single(environment.Observe(branch).LegalActions);
        branch = environment.Step(branch, action.ActionId).State;

        Assert.Equal(originalHash, CanonicalJson.Sha256(state));
        Assert.NotEqual(originalHash, CanonicalJson.Sha256(branch));
    }
}
