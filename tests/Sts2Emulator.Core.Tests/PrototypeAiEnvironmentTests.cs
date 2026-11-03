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
    public void PublicObservationKeepsEnchantedDeckVariantsAndEmptyPotionSlots()
    {
        var environment = new PrototypeAiEnvironment();
        var state = environment.Reset("public-modifier-capacity-v1");
        var enchantment = new PrototypeCardEnchantment(
            PrototypeCardEnchantmentKind.Nimble, Amount: 2);
        state = state with
        {
            Player = state.Player with
            {
                Deck =
                [
                    new CardInstance(900, "proto.silent.strike", 0,
                        PrototypeJson.EmptyObject()),
                    new CardInstance(901, "proto.silent.strike", 0,
                        PrototypeJson.EmptyObject(), enchantment)
                ],
                PotionSlots =
                [
                    null,
                    new PotionInstance("proto.potion.test", PrototypeJson.EmptyObject()),
                    null
                ]
            }
        };

        var frame = environment.Observe(state);
        Assert.Equal("prototype-ai-v1", frame.SchemaId);
        Assert.Equal(3, frame.Observation.PotionSlotCapacity);
        Assert.Equal(1, frame.Observation.PotionSlotsOccupied);
        Assert.Equal(2, frame.Observation.PotionSlotsEmpty);
        Assert.Equal(1, Assert.Single(frame.Observation.Potions).Slot);
        Assert.Null(frame.Observation.Deck[0].Enchantment);
        Assert.Equal(enchantment, frame.Observation.Deck[1].Enchantment);
        Assert.NotEqual(
            CanonicalJson.Serialize(frame.Observation.Deck[0]),
            CanonicalJson.Serialize(frame.Observation.Deck[1]));
    }

    [Fact]
    public void PublicCombatProjectsVisibleModifiersAndPlayHistory()
    {
        var environment = new PrototypeAiEnvironment();
        var engine = new PrototypeGameEngine();
        var state = environment.Reset("public-combat-history-v1");
        state = engine.Step(state, Assert.Single(engine.GetLegalActions(state))).State;
        var action = engine.GetLegalActions(state).First(candidate =>
        {
            var nodeId = candidate.ReadPayload<ChooseMapNodePayload>().NodeId;
            return state.World!.Map.AvailableNodes()
                .Single(node => node.NodeId == nodeId)
                .RoomType == PrototypeRoomType.Combat;
        });
        state = engine.Step(state, action).State;

        var combat = state.World!.Combat!;
        var handCardId = combat.Hand[0];
        var enchantment = new PrototypeCardEnchantment(
            PrototypeCardEnchantmentKind.Sharp, Amount: 3);
        var temporaryCost = new PrototypeTemporaryCardCost(
            Cost: 0, Expiry: PrototypeTemporaryCardCostExpiry.EndOfTurn);
        var changedCombat = combat with
        {
            Cards = combat.Cards.Select(card => card.InstanceId == handCardId
                ? card with
                {
                    Enchantment = enchantment,
                    CombatEnergyCostDelta = 1,
                    TemporaryEnergyCost = temporaryCost,
                    KeywordOverrides =
                    [
                        new PrototypeCardKeywordOverride(
                            PrototypeCardKeyword.Retain, true,
                            PrototypeCardKeywordOverrideExpiry.EndOfTurn)
                    ],
                    EnchantmentTriggeredThisCombat = true
                }
                : card).ToArray(),
            Counters = new PrototypeCombatCounters(
                CardsPlayedThisTurn: 4,
                AttacksPlayedThisTurn: 2,
                SkillsPlayedThisTurn: 1,
                AttacksPlayedLastTurn: 3,
                CardsPlayedThisCombat: 9,
                ManualCardsPlayedThisTurn: 4)
        };
        state = state with { World = state.World with { Combat = changedCombat } };

        var observation = environment.ObservePublic(state);
        var result = Assert.IsType<PrototypeAiCombat>(observation.Combat);
        var card = result.Hand.Single(item => item.InstanceId == handCardId);
        Assert.Equal(enchantment, card.Enchantment);
        Assert.Equal(1, card.CombatEnergyCostDelta);
        Assert.Equal(temporaryCost, card.TemporaryEnergyCost);
        Assert.True(card.EnchantmentTriggeredThisCombat);
        Assert.Equal(PrototypeCardKeyword.Retain,
            Assert.Single(card.KeywordOverrides!).Keyword);
        var history = Assert.IsType<PrototypeAiCombatHistory>(result.History);
        Assert.Equal(4, history.CardsPlayedThisTurn);
        Assert.Equal(2, history.AttacksPlayedThisTurn);
        Assert.Equal(1, history.SkillsPlayedThisTurn);
        Assert.Equal(3, history.AttacksPlayedLastTurn);
        Assert.Equal(9, history.CardsPlayedThisCombat);
        Assert.Equal(4, history.ManualCardsPlayedThisTurn);

        var observationJson = CanonicalJson.Serialize(observation);
        Assert.DoesNotContain(state.RunSeed, observationJson, StringComparison.Ordinal);
        Assert.DoesNotContain("played_card_ids", observationJson,
            StringComparison.OrdinalIgnoreCase);
    }
}
