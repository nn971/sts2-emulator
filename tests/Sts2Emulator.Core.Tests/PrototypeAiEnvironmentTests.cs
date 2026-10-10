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
        state = new PrototypeGameEngine().Step(
            state,
            Assert.Single(
                new PrototypeGameEngine()
                    .GetLegalActions(state))).State;
        var observation =
            environment.Observe(state).Observation;
        Assert.Equal(
            PrototypeActOneRegion.Overgrowth,
            observation.ActOneRegion);
        Assert.Equal(
            state.World!.ActOneEncounterPool!.BossEncounterId,
            observation.ActOneBossEncounterId);
        Assert.Contains(
            observation.ActOneBossEncounterId,
            PrototypeContent.OvergrowthBossEncounterPool);

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
    public void StepFrameReturnsTheSelectedActionAndNextPlayerFrame()
    {
        var environment = new PrototypeAiEnvironment();
        var state = environment.Reset("ai-step-frame-test");
        var selected = Assert.Single(environment.Observe(state).LegalActions);

        var stepped = environment.StepFrame(state, selected.ActionId);

        Assert.Equal(selected.ActionId, stepped.Action.ActionId);
        Assert.Equal(
            CanonicalJson.Sha256(stepped.State),
            stepped.Frame.CanonicalStateHash);
        Assert.Equal(
            environment.Observe(stepped.State).ObservationHash,
            stepped.Frame.ObservationHash);
        Assert.Equal(
            environment.Observe(stepped.State).LegalActions.Length,
            stepped.Frame.LegalActions.Length);
    }

    [Fact]
    public void RolloutStepFrameMatchesFullFrameWithoutComputingHashes()
    {
        var environment = new PrototypeAiEnvironment();
        var state = environment.Reset("ai-rollout-frame-test");
        var selected = Assert.Single(environment.Observe(state).LegalActions);

        var rollout = environment.RolloutStepFrame(state, selected.ActionId);
        var full = environment.Observe(rollout.State);

        Assert.Equal(selected.ActionId, rollout.Action.ActionId);
        Assert.Equal(
            CanonicalJson.Serialize(full.Observation),
            CanonicalJson.Serialize(rollout.Observation));
        Assert.Equal(
            full.LegalActions.Select(action => action.ActionId),
            rollout.LegalActions.Select(action => action.ActionId));
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

    [Fact]
    public void DrawPileObservationIsUnorderedButPreservesVisibleCards()
    {
        var environment = new PrototypeAiEnvironment();
        var engine = new PrototypeGameEngine();
        var state = environment.Reset("public-draw-multiset");
        state = engine.Step(state, Assert.Single(engine.GetLegalActions(state))).State;
        var mapAction = engine.GetLegalActions(state).First(action =>
        {
            var payload = action.ReadPayload<ChooseMapNodePayload>();
            return state.World!.Map.AvailableNodes().Single(node =>
                node.NodeId == payload.NodeId).RoomType == PrototypeRoomType.Combat;
        });
        state = engine.Step(state, mapAction).State;

        var original = state.World!.Combat!;
        Assert.True(original.DrawPile.Length >= 2);
        var reversed = original with
        {
            DrawPile = original.DrawPile.Reverse().ToArray()
        };
        var reordered = state with
        {
            World = state.World with { Combat = reversed }
        };

        var first = Assert.IsType<PrototypeAiCombat>(
            environment.Observe(state).Observation.Combat);
        var second = Assert.IsType<PrototypeAiCombat>(
            environment.Observe(reordered).Observation.Combat);

        Assert.Equal(original.DrawPile.Length, first.DrawPileCount);
        Assert.NotNull(first.DrawPile);
        Assert.Equal(
            original.DrawPile.OrderBy(id => id),
            first.DrawPile.Select(card => card.InstanceId));
        Assert.Equal(
            first.DrawPile.Select(card => card.CardId),
            second.DrawPile!.Select(card => card.CardId));
        Assert.Equal(
            CanonicalJson.Serialize(environment.Observe(state).Observation),
            CanonicalJson.Serialize(environment.Observe(reordered).Observation));
        Assert.NotEqual(CanonicalJson.Sha256(state), CanonicalJson.Sha256(reordered));
    }

    [Fact]
    public void PublicObservationIncludesOwnedCombatRelicProgress()
    {
        var environment = new PrototypeAiEnvironment();
        var engine = new PrototypeGameEngine();
        var state = environment.Reset("visible-relic-counters");
        state = engine.Step(state, Assert.Single(engine.GetLegalActions(state))).State;
        var mapAction = engine.GetLegalActions(state).First(action =>
        {
            var payload = action.ReadPayload<ChooseMapNodePayload>();
            return state.World!.Map.AvailableNodes().Single(node =>
                node.NodeId == payload.NodeId).RoomType == PrototypeRoomType.Combat;
        });
        state = engine.Step(state, mapAction).State;

        var relic = new RelicInstance("proto.relic.kunai", PrototypeJson.EmptyObject());
        var index = state.Player.Relics.Length;
        var counter = new CombatRelicState(
            index, "proto.relic.kunai", 1L, [2]);
        state = state with
        {
            Player = state.Player with
            {
                Relics = state.Player.Relics.Append(relic).ToArray()
            },
            World = state.World! with
            {
                Combat = state.World.Combat! with
                {
                    Relics = state.World.Combat.RelicStates.Append(counter).ToArray()
                }
            }
        };

        var view = environment.Observe(state).Observation;
        Assert.Contains(view.Relics, r => r.RelicId == "proto.relic.kunai");
        var counters = Assert.IsType<PrototypeAiRelicCounter[]>(
            view.Combat!.RelicCounters);
        var kunai = Assert.Single(counters, r => r.RelicId == "proto.relic.kunai");
        Assert.Equal([2], kunai.TriggerCounts);

        var changed = state with
        {
            World = state.World! with
            {
                Combat = state.World.Combat! with
                {
                    Relics = state.World.Combat.RelicStates
                        .Select(r => r.RelicId == "proto.relic.kunai"
                            ? r with { TriggerCounts = [1] }
                            : r)
                        .ToArray()
                }
            }
        };
        Assert.NotEqual(
            environment.Observe(state).ObservationHash,
            environment.Observe(changed).ObservationHash);
    }

}
