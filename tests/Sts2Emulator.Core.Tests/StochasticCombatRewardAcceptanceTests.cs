using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>Source-independent acceptance for combat reward continuations.</summary>
public sealed class StochasticCombatRewardAcceptanceTests
{
    [Fact]
    public void CombatVictoryReplayResumesLegacyRewardGeneration()
    {
        var (origin, winningAction) = WinningCombatAction();
        var originHash = CanonicalJson.Sha256(origin);
        var prototypeRngHash = CanonicalJson.Sha256(
            origin.Stochastic.PrototypeRng);
        var (session, sites, restored) = CompleteLegacyRewardWithPotionDrop(
            origin, winningAction);
        Assert.True(restored);
        Assert.True(session.Complete,
            $"Combat reward stopped at {session.PendingLaw?.SiteId}.");
        Assert.Contains("reward.card_rarity.ticket", sites);
        Assert.Contains("reward.card_model.index_draw", sites);
        Assert.Contains("reward.random_potion.drop.ticket", sites);
        Assert.Contains("reward.random_potion.model", sites);
        var potionModelDecision = Assert.Single(session.ProviderDecisions,
            decision => decision.IndexDecision?.Request.SiteId ==
                "reward.random_potion.model");
        var potionIndex = Assert.IsType<RandomIndexDecisionV111>(
            potionModelDecision.IndexDecision);
        Assert.Equal(Enumerable.Range(0, PrototypeContent.PotionPool.Length),
            potionIndex.Request.OrderedCandidates);
        Assert.Equal(PrototypeContent.PotionPool[potionIndex.SelectedIndex],
            session.Run.World!.Reward!.PotionOption);
        AssertCardModelOutcomesWereOffered(session);
        Assert.Equal(originHash, CanonicalJson.Sha256(origin));
        Assert.Equal(prototypeRngHash, CanonicalJson.Sha256(
            session.Run.Stochastic.PrototypeRng));
        Assert.NotNull(session.Run.World?.Reward);
        PrototypeStateInvariants.Validate(session.Run);
    }

    private static (ReferenceNatureCombatRewardSessionV111 Session,
        List<string> Sites, bool Restored) CompleteLegacyRewardWithPotionDrop(
            RunState origin, GameAction action)
    {
        for (ulong seed = 0x177; seed < 0x197; seed++)
        {
            var session = ReferenceNatureCombatRewardSessionV111.Begin(
                origin, action, seed);
            var sites = new List<string>();
            var restored = false;
            var potionOffered = false;
            for (var boundary = 0; boundary < 64 && !session.Complete;
                 boundary++)
            {
                var law = Assert.IsType<ChanceLawV111>(session.PendingLaw);
                Assert.Equal(ChanceLawV111.UnverifiedStatus,
                    law.ProbabilityStatus);
                sites.Add(law.SiteId);
                if (!restored && boundary == 2)
                {
                    session = ReferenceNatureCombatRewardSessionV111.Restore(
                        ReferenceNatureCombatRewardSessionV111.Save(session));
                    restored = true;
                }
                session = session.ResolveNext();
                if (law.SiteId == "reward.random_potion.drop.ticket")
                {
                    potionOffered = Assert.IsType<RandomIndexDecisionV111>(
                        session.ProviderDecisions[^1].IndexDecision)
                        .SelectedIndex == 0;
                }
            }

            if (potionOffered && session.Complete)
                return (session, sites, restored);
            if (session.Complete && !potionOffered)
            {
                Assert.Null(session.Run.World!.Reward!.PotionOption);
                Assert.DoesNotContain("reward.random_potion.model", sites);
            }
        }

        throw new InvalidOperationException(
            "No bounded ReferenceNature seed exercised a legacy combat-potion reward.");
    }

    [Fact]
    public void ConfiguredCombatVictoryReplaysNativeRewardGeneration()
    {
        var (origin, winningAction) = ConfiguredNativeWinningCombatAction();
        var originHash = CanonicalJson.Sha256(origin);
        var nativeRngHash = CanonicalJson.Sha256(
            origin.Stochastic.NativeRunRng);
        var (session, sites, restored) = CompleteNativeRewardWithPotionDrop(
            origin, winningAction);
        Assert.True(restored);
        Assert.True(session.Complete,
            $"Configured combat reward stopped at {session.PendingLaw?.SiteId}.");
        Assert.Contains("reward.combat_gold.index_draw", sites);
        Assert.Contains("reward.potion_odds.unit_float", sites);
        Assert.Contains("card_rarity.roll.unit_float", sites);
        Assert.Contains("card_rarity.encounter_model.index_draw", sites);
        Assert.Contains("card_rarity.upgrade_roll.unit_float", sites);
        Assert.Contains("potion_factory.rarity_roll.unit_float", sites);
        Assert.Contains("potion_factory.model_within_rarity.index_draw", sites);
        var potionOddsDecisionIndex = Array.FindIndex(
            session.ProviderDecisions, decision =>
                decision.UnitFloatDecision?.Request.SiteId ==
                "reward.potion_odds.unit_float");
        Assert.InRange(potionOddsDecisionIndex, 0,
            session.DecisionTranscript.Length - 1);
        Assert.Equal(session.Run.World!.Reward!.PotionOption is not null,
            session.DecisionTranscript[potionOddsDecisionIndex].OutcomeId ==
                "potion_reward:offered");
        Assert.Equal(checked(origin.World!.PotionRewardOddsThousandths -
            PrototypeNativePotionRewardOdds.SuccessPenalty),
            session.Run.World.PotionRewardOddsThousandths);
        Assert.Equal(session.Run.World.Reward.PotionOption is not null,
            sites.Contains("potion_factory.rarity_roll.unit_float",
                StringComparer.Ordinal));
        var potionRarityDecisionIndex = Array.FindIndex(
            session.ProviderDecisions, decision =>
                decision.UnitFloatDecision?.Request.SiteId ==
                "potion_factory.rarity_roll.unit_float");
        var potionRarityOutcome = session.DecisionTranscript[
            potionRarityDecisionIndex].OutcomeId;
        Assert.StartsWith("rarity:", potionRarityOutcome,
            StringComparison.Ordinal);
        var potionRarity = Enum.Parse<PrototypeNativePotionRarity>(
            potionRarityOutcome["rarity:".Length..], ignoreCase: true);
        var potionCandidates = PrototypeNativePotionShop
            .CandidatesForRarity(potionRarity);
        var potionModelDecision = Assert.Single(session.ProviderDecisions,
            decision => decision.IndexDecision?.Request.SiteId ==
                "potion_factory.model_within_rarity.index_draw");
        Assert.Equal(potionCandidates[
                potionModelDecision.IndexDecision!.SelectedIndex],
            session.Run.World.Reward.PotionOption);
        var goldDecision = Assert.Single(session.ProviderDecisions,
            decision => decision.IndexDecision?.Request.SiteId ==
                "reward.combat_gold.index_draw");
        var combatGoldBounds = PrototypeNativeCombatGoldReward.Bounds(
            PrototypeRoomType.Combat, session.Origin.Ascension)!.Value;
        Assert.Equal(combatGoldBounds.Min + goldDecision.IndexDecision!
                .SelectedIndex,
            session.Run.World.Reward.GoldOption);
        var upgradeDecisionIndices = session.ProviderDecisions
            .Select((decision, index) => (decision, index))
            .Where(item => item.decision.UnitFloatDecision?.Request.SiteId ==
                "card_rarity.upgrade_roll.unit_float")
            .ToArray();
        var cardUpgradeFlags = Assert.IsType<bool[]>(
            session.Run.World.Reward.CardOptionUpgradeFlags);
        Assert.Equal(session.Run.World.Reward.CardOptions.Length,
            upgradeDecisionIndices.Length);
        Assert.Equal(cardUpgradeFlags, upgradeDecisionIndices.Select(item =>
            session.DecisionTranscript[item.index].OutcomeId ==
                "upgrade_check:passed"));
        AssertCardModelOutcomesWereOffered(session);
        Assert.Equal(originHash, CanonicalJson.Sha256(origin));
        Assert.Equal(nativeRngHash, CanonicalJson.Sha256(
            session.Run.Stochastic.NativeRunRng));
        Assert.NotNull(session.Run.World?.Reward);
        PrototypeStateInvariants.Validate(session.Run);
    }

    private static (ReferenceNatureCombatRewardSessionV111 Session,
        List<string> Sites, bool Restored) CompleteNativeRewardWithPotionDrop(
            RunState origin, GameAction action)
    {
        for (ulong seed = 0x176; seed < 0x196; seed++)
        {
            var session = ReferenceNatureCombatRewardSessionV111.Begin(
                origin, action, seed);
            var sites = new List<string>();
            var restored = false;
            var potionOffered = false;
            var potionOddsResolved = false;
            for (var boundary = 0; boundary < 64 && !session.Complete;
                 boundary++)
            {
                var law = Assert.IsType<ChanceLawV111>(session.PendingLaw);
                Assert.Equal(ChanceLawV111.SourceVerifiedStatus,
                    law.ProbabilityStatus);
                sites.Add(law.SiteId);
                if (!restored && boundary == 1)
                {
                    session = ReferenceNatureCombatRewardSessionV111.Restore(
                        ReferenceNatureCombatRewardSessionV111.Save(session));
                    restored = true;
                }
                session = session.ResolveNext();
                if (law.SiteId == "reward.potion_odds.unit_float")
                {
                    potionOddsResolved = true;
                    potionOffered = session.DecisionTranscript[^1].OutcomeId ==
                        "potion_reward:offered";
                }
            }

            if (potionOffered && session.Complete)
                return (session, sites, restored);
            if (session.Complete && potionOddsResolved && !potionOffered)
            {
                Assert.Null(session.Run.World!.Reward!.PotionOption);
                Assert.Equal(checked(origin.World!.PotionRewardOddsThousandths +
                    PrototypeNativePotionRewardOdds.MissBonus),
                    session.Run.World.PotionRewardOddsThousandths);
                Assert.DoesNotContain("potion_factory.rarity_roll.unit_float",
                    sites);
            }
        }

        throw new InvalidOperationException(
            "No bounded ReferenceNature seed exercised a Native combat-potion reward.");
    }

    private static void AssertCardModelOutcomesWereOffered(
        ReferenceNatureCombatRewardSessionV111 session)
    {
        var reward = Assert.IsType<RewardState>(session.Run.World?.Reward);
        var options = reward.CardOptions.Concat(
            reward.ExtraCardOptions?.SelectMany(group => group) ?? [])
            .ToHashSet(StringComparer.Ordinal);
        var decisions = session.ProviderDecisions.Where(decision =>
            decision.IndexDecision?.Request.SiteId ==
            "reward.card_model.index_draw" ||
            decision.IndexDecision?.Request.SiteId ==
            StochasticSiteRegistryV111.NativeRewardCardModelSiteId);
        foreach (var decision in decisions)
        {
            var indexDecision = Assert.IsType<RandomIndexDecisionV111>(
                decision.IndexDecision);
            var labels = Assert.IsType<string[]>(decision.CandidateLabels);
            Assert.Equal(indexDecision.Request.SupportCount, labels.Length);
            Assert.Contains(labels[indexDecision.SelectedIndex], options);
        }
        Assert.NotEmpty(decisions);
    }

    private static (RunState Origin, GameAction WinningAction)
        WinningCombatAction()
    {
        var seed = "stochastic-combat-reward-entry";
        var engine = new PrototypeGameEngine();
        var state = engine.Step(
            PrototypeGameFactory.Create(seed),
            GameAction.Empty("start_run")).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        state = engine.Step(
            state, engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.Combat, state.Phase);

        var combat = state.World!.Combat!;
        var neutralize = combat.Cards.Single(card =>
            card.CardId == "proto.silent.neutralize");
        combat = combat with
        {
            Hand = [neutralize.InstanceId],
            DrawPile = combat.Cards.Select(card => card.InstanceId)
                .Where(id => id != neutralize.InstanceId).ToArray(),
            DiscardPile = [],
            ExhaustPile = [],
            PlayPile = [],
            Energy = 3,
            Enemies = combat.Enemies.Select((enemy, index) =>
                enemy with { Hp = index == 0 ? 1 : 0, Block = 0 }).ToArray()
        };
        state = state with { World = state.World with { Combat = combat } };
        PrototypeStateInvariants.Validate(state);
        var action = engine.GetLegalActions(state).Single(candidate =>
            candidate.Kind == "play_card"
            && candidate.ReadPayload<PlayCardPayload>().CardInstanceId
                == neutralize.InstanceId);
        return (state, action);
    }

    private static (RunState Origin, GameAction WinningAction)
        ConfiguredNativeWinningCombatAction()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeContentTestSupport.ConfiguredNativeRun(
            "stochastic-native-combat-reward-entry");
        for (var step = 0; step < 64 && state.Phase != RunPhase.MapChoice;
             step++)
        {
            var legal = engine.GetLegalActions(state);
            Assert.NotEmpty(legal);
            state = engine.Step(state, legal[0]).State;
            PrototypeStateInvariants.Validate(state);
        }
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        state = engine.Step(state, engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.Combat, state.Phase);

        var combat = state.World!.Combat!;
        var neutralize = combat.Cards.Single(card =>
            card.CardId == "proto.silent.neutralize");
        combat = combat with
        {
            Hand = [neutralize.InstanceId],
            DrawPile = combat.Cards.Select(card => card.InstanceId)
                .Where(id => id != neutralize.InstanceId).ToArray(),
            DiscardPile = [],
            ExhaustPile = [],
            PlayPile = [],
            Energy = 3,
            Enemies = combat.Enemies.Select((enemy, index) =>
                enemy with { Hp = index == 0 ? 1 : 0, Block = 0 }).ToArray()
        };
        state = state with { World = state.World with { Combat = combat } };
        PrototypeStateInvariants.Validate(state);
        var action = engine.GetLegalActions(state).Single(candidate =>
            candidate.Kind == "play_card"
            && candidate.ReadPayload<PlayCardPayload>().CardInstanceId
                == neutralize.InstanceId);
        return (state, action);
    }
}
