using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class SemanticPilotCombatV111Tests
{
    [Fact]
    public void NativePolicyGuardRejectsUnownedNativePrimitiveDraws()
    {
        var native = NativeRngV111.CreateBundle(
            "semantic-native-policy-guard");
        var prototype = PrototypeRng.CreateBundle(
            "semantic-native-policy-guard");
        using (ControlledChanceGuardV111.Enter())
        using (ControlledChanceGuardV111.AllowEngineOwnedNativeProvider())
        {
            Assert.Throws<NotSupportedException>(() =>
                NativeRngV111.NextInt(native, "run.monster_ai", 2));
            Assert.Throws<NotSupportedException>(() =>
                NativeRngV111.NextInt(native, "run.shuffle", 2));
            Assert.Throws<NotSupportedException>(() =>
                NativeRngV111.NextInt(native, "run.combat_energy_costs", 4));
            Assert.Throws<NotSupportedException>(() =>
                NativeRngV111.NextInt(native, "run.combat_targets", 2));
            Assert.Throws<NotSupportedException>(() =>
                NativeRngV111.NextInt(native, "run.combat_orbs", 2));
            Assert.Throws<NotSupportedException>(() =>
                PrototypeRng.NextInt(prototype, "combat", 2));
        }
    }

    [Fact]
    public void EngineOwnedRewardScopeAllowsOnlyDeclaredLegacyRewardSites()
    {
        var streams = PrototypeRng.CreateBundle(
            "semantic-engine-owned-reward-guard");
        using (ControlledChanceGuardV111.Enter())
        {
            Assert.Throws<NotSupportedException>(() =>
                LegacyRandomChoiceV111.ChooseBoundedInteger(
                    streams, "reward.card_rarity.ticket", "reward", "cards", 3));
            using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
            {
                var decision = LegacyRandomChoiceV111.ChooseBoundedInteger(
                    streams, "reward.card_rarity.ticket", "reward", "cards", 3);
                Assert.InRange(decision.SelectedIndex, 0, 2);
                var potionOdds = LegacyRandomChoiceV111.ChooseBoundedInteger(
                    streams, "reward.potion_odds.ticket", "reward",
                    "run:potion_reward_odds", 1000);
                Assert.InRange(potionOdds.SelectedIndex, 0, 999);
                var gold = LegacyRandomChoiceV111.ChooseBoundedInteger(
                    streams, StochasticSiteRegistryV111.NativeCombatGoldSiteId,
                    "reward", "room:Combat:ascension:0:bounds:10:20", 11);
                Assert.InRange(gold.SelectedIndex, 0, 10);
                var potionRarity = LegacyRandomChoiceV111
                    .ChooseBoundedInteger(streams,
                        PrototypeNativePotionShop.RarityRollSiteId,
                        "reward", "room:potion", 100);
                Assert.InRange(potionRarity.SelectedIndex, 0, 99);
                var potionModel = LegacyRandomChoiceV111.ChooseIndexRange(
                    streams, PrototypeNativePotionShop.ModelWithinRaritySiteId,
                    "reward", "room:potion:pool", 3);
                Assert.InRange(potionModel.SelectedIndex, 0, 2);
                var cardRarity = LegacyRandomChoiceV111.ChooseBoundedInteger(
                    streams, "card_rarity.roll.ticket", "reward",
                    "room:card_rarity", 10000);
                Assert.InRange(cardRarity.SelectedIndex, 0, 9999);
                var rewardCard = LegacyRandomChoiceV111.ChooseIndexRange(
                    streams, StochasticSiteRegistryV111.NativeRewardCardModelSiteId,
                    "reward", "room:reward_card", 5);
                Assert.InRange(rewardCard.SelectedIndex, 0, 4);
                var cardUpgrade = LegacyRandomChoiceV111.ChooseBoundedInteger(
                    streams, "card_rarity.upgrade_roll.ticket", "reward",
                    "room:card_upgrade", 10000);
                Assert.InRange(cardUpgrade.SelectedIndex, 0, 9999);
                Assert.Throws<NotSupportedException>(() =>
                    LegacyRandomChoiceV111.ChooseBoundedInteger(
                        streams, "reward.potion.rarity_roll.ticket",
                        "reward", "potion", 3));
            }
            Assert.Throws<NotSupportedException>(() =>
                LegacyRandomChoiceV111.ChooseBoundedInteger(
                    streams, "reward.card_rarity.ticket", "reward", "cards", 3));
        }
    }

    private static ControlledPlayableCombatV111 Opening(string seed)
    {
        var pre = PrototypeNativeLaterActFixture.CreateCombatRngPreEntry(
            seed, 2, PrototypeNativeHiveNormals.ExoskeletonsNormalId,
            "ExoskeletonsNormal", 20, mapColumn: 3, mapRow: 5);
        var hp = ControlledInitialHpV111.Begin(
            pre.Combat, pre.Formation, pre.NativeStreams);
        while (!hp.Complete)
            hp = ControlledInitialHpV111.Resolve(hp, hp.Fingerprint, 0);
        var intents = ControlledIntentsV111.BeginAfterControlledHp(hp);
        while (!intents.Complete)
            intents = ControlledIntentsV111.Resolve(
                intents, intents.Fingerprint, 0);
        var opening = ControlledPlayableCombatEngineV111.Begin(intents, seed);
        while (!opening.AtPlayerBoundary)
            opening = ControlledPlayableCombatEngineV111.Resolve(
                opening, opening.Fingerprint, 0);
        return opening;
    }

    [Fact]
    public void SemanticProtocolReusesExactlyPrecommittedOpeningOrder()
    {
        var legacy = Opening("semantic-p0-precommitted-opening");
        var original = CanonicalJson.Sha256(legacy);
        var state = SemanticPilotCombatEngineV111.Begin(legacy);
        Assert.True(state.AtPlayerBoundary);
        Assert.Equal(SemanticPilotCombatV111.SchemaId, state.Mode);
        Assert.Equal(legacy.Run.World!.Combat!.DrawPile,
            state.Run.World!.Combat!.DrawPile);
        Assert.Equal(0, state.Run.World.Combat.SemanticDeck!.ShuffleEpoch);
        Assert.Empty(state.Run.World.Combat.SemanticDeck.Transcript!);
        Assert.Empty(state.Run.World.Combat.SemanticDeck.Pools);
        Assert.Equal(legacy.Run.World.Combat.DrawPile.Reverse(),
            state.Run.World.Combat.SemanticDeck.Slots.Select(
                x => x.KnownCardInstanceId!.Value));
        Assert.Null(state.DrawPending);
        Assert.Null(state.IntentPending);
        Assert.False(state.CombatTerminal);
        Assert.Equal(original, CanonicalJson.Sha256(legacy));
        Assert.Equal(state.Fingerprint,
            SemanticPilotCombatEngineV111.Restore(
                SemanticPilotCombatEngineV111.Save(state)).Fingerprint);
    }

    [Fact]
    public void ActualEndTurnAndNatureIntentsReachRealFairPlayerDecision()
    {
        var state = SemanticPilotCombatEngineV111.Begin(
            Opening("semantic-p0-real-action-to-action"));
        var before = state.Fingerprint;
        var actions = new PrototypeAiEnvironment().Observe(state.Run).LegalActions;
        var endTurn = Assert.Single(actions, a => a.Kind == "end_turn");
        state = SemanticPilotCombatEngineV111.Step(
            state, state.Fingerprint, endTurn.ActionId);
        Assert.NotEqual(before, state.Fingerprint);
        Assert.Throws<InvalidOperationException>(() =>
            SemanticPilotCombatEngineV111.Resolve(state, before, 0));
        int intents = 0, draws = 0;
        int step = 0;
        while (!state.AtPlayerBoundary)
        {
            Assert.True(++step < 120, "Expected original automatic turn to finish.");
            if (state.IntentPending is { } intent)
            {
                Assert.True(intent.SupportCount > 0);
                intents++;
                state = SemanticPilotCombatEngineV111.Resolve(
                    state, state.Fingerprint, 0);
            }
            else if (state.DrawPending is { } draw)
            {
                Assert.NotEmpty(draw.CandidateCardInstanceIds);
                var candidate = draw.CandidateCardInstanceIds[0];
                Assert.Contains(candidate,
                    state.Run.World!.Combat!.Cards.Select(c => c.InstanceId));
                draws++;
                state = SemanticPilotCombatEngineV111.Resolve(
                    state, state.Fingerprint, 0);
            }
            else
                throw new InvalidOperationException(
                    "Unsupported source event lacked a semantic chance boundary.");
            var saved = SemanticPilotCombatEngineV111.Save(state);
            Assert.Equal(state.Fingerprint,
                SemanticPilotCombatEngineV111.Restore(saved).Fingerprint);
        }
        Assert.True(intents > 0);
        Assert.True(state.Run.World!.Combat!.IsPlayerTurn);
        Assert.NotEmpty(new PrototypeAiEnvironment()
            .Observe(state.Run).LegalActions);
        // Source opening is fully committed, so first turn's known
        // physical cards never get duplicate Nature identity tickets.
        Assert.Equal(0, draws);
    }

    [Fact]
    public void SemanticDrawResolvesByEventAndCardIdentityAndReplaysTypedDecision()
    {
        var state = SemanticPilotCombatEngineV111.BeginSeeded(
            Seeded("semantic-p0-card-identity-event"));
        for (var stage = 0; state.DrawPending is null; stage++)
        {
            Assert.True(stage < 200,
                "Expected a natural discard recycle to request a card.");
            if (state.IntentPending is { } intent)
            {
                var support = ControlledTurnIntentEngineV111
                    .DescribePendingSemanticIntent(
                        state.PendingEndTurn!.Intents!)!;
                state = SemanticPilotCombatEngineV111.ResolveIntent(
                    state, state.Fingerprint,
                    support.Candidates[0].IntentId);
            }
            else if (state.AtPlayerBoundary && !state.CombatTerminal)
            {
                var endTurn = Assert.Single(new PrototypeAiEnvironment()
                    .Observe(state.Run).LegalActions,
                    action => action.Kind == "end_turn");
                state = SemanticPilotCombatEngineV111.Step(
                    state, state.Fingerprint, endTurn.ActionId);
            }
            else
                Assert.Fail("Expected an intent, player, or draw boundary.");
        }

        var pending = state.DrawPending!;
        var selectedId = pending.CandidateCardInstanceIds[^1];
        var fingerprint = state.Fingerprint;
        Assert.Throws<InvalidOperationException>(() =>
            SemanticPilotCombatEngineV111.ResolveDrawCard(
                state, fingerprint, "stale-event", selectedId));
        Assert.Throws<InvalidOperationException>(() =>
            SemanticPilotCombatEngineV111.ResolveDrawCard(
                state, fingerprint, pending.EventId, long.MaxValue));
        Assert.Equal(fingerprint, state.Fingerprint);

        state = SemanticPilotCombatEngineV111.ResolveDrawCard(
            state, fingerprint, pending.EventId, selectedId);
        var decision = Assert.Single(state.History,
            transition => transition.Kind == "semantic_draw").DrawDecision!;
        Assert.Equal(pending.EventId, decision.Event.EventId);
        Assert.Equal(pending.CandidateCardInstanceIds,
            decision.Event.CandidateCardInstanceIds);
        Assert.Equal(selectedId, decision.SelectedCardInstanceId);
        Assert.Equal(state.Fingerprint,
            SemanticPilotCombatEngineV111.Restore(
                SemanticPilotCombatEngineV111.Save(state)).Fingerprint);
    }

    [Fact]
    public void SemanticRandomTargetResumesRealEffectByEnemyIdentityAndReplays()
    {
        var state = SemanticPilotCombatEngineV111.BeginSeeded(
            Seeded("semantic-p0-target-identity-event") with
            {
                FixtureRandomTargetCardId = "proto.silent.ricochet"
            });
        var combat = state.Run.World!.Combat!;
        var startingEnemyHp = combat.Enemies.Sum(enemy => enemy.Hp);
        var cardInstanceId = combat.Hand[0];
        var action = Assert.Single(new PrototypeAiEnvironment()
            .Observe(state.Run).LegalActions,
            candidate => candidate.Kind == "play_card" &&
                candidate.Payload.Deserialize<PlayCardPayload>()?.CardInstanceId ==
                    cardInstanceId);
        state = SemanticPilotCombatEngineV111.Step(
            state, state.Fingerprint, action.ActionId);
        var pending = state.TargetPending!;
        Assert.Equal("combat.effect.random_enemy", pending.SiteId);
        Assert.Equal("run.combat_targets", pending.StreamId);
        Assert.Equal("choose_enemy_instance", pending.OperationKind);
        Assert.Equal("UNVERIFIED", pending.ProbabilityStatus);
        Assert.True(pending.CandidateEnemyInstanceIds.Length > 1);

        var fingerprint = state.Fingerprint;
        var selectedId = pending.CandidateEnemyInstanceIds[^1];
        Assert.Throws<InvalidOperationException>(() =>
            SemanticPilotCombatEngineV111.ResolveRandomTarget(
                state, fingerprint, "stale-event", selectedId));
        Assert.Throws<InvalidOperationException>(() =>
            SemanticPilotCombatEngineV111.ResolveRandomTarget(
                state, fingerprint, pending.EventId, int.MaxValue));
        Assert.Equal(fingerprint, state.Fingerprint);

        int decisions = 0;
        while (state.TargetPending is { } target)
        {
            Assert.True(decisions < 8,
                "Expected Ricochet's source effect to finish its target sequence.");
            var targetId = target.CandidateEnemyInstanceIds[^1];
            state = SemanticPilotCombatEngineV111.ResolveRandomTarget(
                state, state.Fingerprint, target.EventId, targetId);
            decisions++;
            Assert.Equal(state.Fingerprint,
                SemanticPilotCombatEngineV111.Restore(
                    SemanticPilotCombatEngineV111.Save(state)).Fingerprint);
        }
        Assert.Equal(4, decisions);
        Assert.True(state.AtPlayerBoundary || state.CombatTerminal);
        Assert.Equal(4, state.History.Count(transition =>
            transition.Kind == "semantic_target"));
        Assert.True(state.Run.World!.Combat!.Enemies.Sum(enemy => enemy.Hp) <
            startingEnemyHp);
    }

    [Fact]
    public void SemanticGeneratedCardPoolResolvesByModelIdentityAndReplays()
    {
        var state = SemanticPilotCombatEngineV111.BeginSeeded(
            Seeded("semantic-p0-generation-event") with
            {
                FixtureGenerationPotionId = "proto.potion.orobic_acid"
            });
        var action = Assert.Single(new PrototypeAiEnvironment()
            .Observe(state.Run).LegalActions,
            candidate => candidate.Kind == "use_potion");
        state = SemanticPilotCombatEngineV111.Step(
            state, state.Fingerprint, action.ActionId);
        var originalCardCount = state.Run.World!.Combat!.Cards.Length;
        var pending = state.GeneratedCardPending!;
        Assert.Equal("combat.generated_card.select", pending.SiteId);
        Assert.Equal("prototype.combat", pending.StreamId);
        Assert.Equal("choose_card_model", pending.OperationKind);
        Assert.Equal("UNVERIFIED", pending.ProbabilityStatus);
        Assert.True(pending.CandidateCardIds.Length > 3);

        var fingerprint = state.Fingerprint;
        Assert.Throws<InvalidOperationException>(() =>
            SemanticPilotCombatEngineV111.ResolveGeneratedCard(
                state, fingerprint, "stale-event", pending.CandidateCardIds[0]));
        Assert.Throws<InvalidOperationException>(() =>
            SemanticPilotCombatEngineV111.ResolveGeneratedCard(
                state, fingerprint, pending.EventId, "proto.card.not_real"));
        Assert.Equal(fingerprint, state.Fingerprint);

        int decisions = 0;
        while (state.GeneratedCardPending is { } generated)
        {
            Assert.True(decisions < 4,
                "Expected Orobic Acid to resolve its three generated pools.");
            var cardId = generated.CandidateCardIds[^1];
            state = SemanticPilotCombatEngineV111.ResolveGeneratedCard(
                state, state.Fingerprint, generated.EventId, cardId);
            decisions++;
            Assert.Equal(state.Fingerprint,
                SemanticPilotCombatEngineV111.Restore(
                    SemanticPilotCombatEngineV111.Save(state)).Fingerprint);
        }
        Assert.Equal(3, decisions);
        Assert.True(state.AtPlayerBoundary);
        Assert.Null(state.Run.Player.PotionSlots[0]);
        Assert.Equal(3, state.History.Count(transition =>
            transition.Kind == "semantic_generated_card"));
        Assert.Equal(originalCardCount + 3,
            state.Run.World!.Combat!.Cards.Length);
    }

    [Fact]
    public void BasicSilentActionKeepsCommittedHiddenDeckUntouched()
    {
        var state = SemanticPilotCombatEngineV111.Begin(
            Opening("semantic-p0-basic-player-action"));
        var old = state.Run.World!.Combat!.SemanticDeck!.Fingerprint;
        var run = state.Run;
        var hand = run.World!.Combat!.Hand.ToHashSet();
        var basic = run.World.Combat.Cards.Where(c =>
            hand.Contains(c.InstanceId) &&
            c.CardId is "proto.silent.strike" or "proto.silent.defend")
            .Select(c => c.InstanceId).ToHashSet();
        var action = new PrototypeAiEnvironment().Observe(run)
            .LegalActions.First(a => a.Kind == "play_card" &&
                a.Payload.Deserialize<PlayCardPayload>() is { } card &&
                basic.Contains(card.CardInstanceId));
        state = SemanticPilotCombatEngineV111.Step(
            state, state.Fingerprint, action.ActionId);
        Assert.True(state.AtPlayerBoundary);
        Assert.Equal(old,
            state.Run.World!.Combat!.SemanticDeck!.Fingerprint);
        Assert.Single(state.History);
        Assert.Equal(state.Fingerprint,
            SemanticPilotCombatEngineV111.Restore(
                SemanticPilotCombatEngineV111.Save(state)).Fingerprint);
    }

    [Fact]
    public void WeightedIntentSupportMatchesSourceTicketAndSemanticSelection()
    {
        var state = SemanticPilotCombatEngineV111.Begin(
            Opening("semantic-p0-eligible-weights"));
        var end = Assert.Single(new PrototypeAiEnvironment()
            .Observe(state.Run).LegalActions, a => a.Kind == "end_turn");
        state = SemanticPilotCombatEngineV111.Step(
            state, state.Fingerprint, end.ActionId);
        var intents = state.PendingEndTurn!.Intents!;
        var pending = Assert.IsType<RandomIndexRequestV111>(intents.Pending);
        var support = Assert.IsType<SemanticIntentSupportV111>(
            ControlledTurnIntentEngineV111.DescribePendingSemanticIntent(intents));
        Assert.Equal(pending.OwnerId, support.OwnerId);
        Assert.Equal(pending.SupportCount, support.TotalRawWeight);
        Assert.NotEmpty(support.Candidates);
        Assert.Equal(support.TotalRawWeight,
            support.Candidates.Sum(candidate => candidate.RawWeight));
        var nextTicket = 0;
        foreach (var candidate in support.Candidates)
        {
            Assert.True(candidate.RawWeight > 0);
            Assert.Equal(nextTicket, candidate.TicketStart);
            Assert.False(string.IsNullOrWhiteSpace(candidate.IntentId));
            Assert.False(string.IsNullOrWhiteSpace(candidate.MoveId));
            nextTicket += candidate.RawWeight;
        }
        var last = support.Candidates[^1];
        var bySemanticId = SemanticPilotCombatEngineV111.ResolveIntent(
            state, state.Fingerprint, last.IntentId);
        var bySourceTicket = SemanticPilotCombatEngineV111.Resolve(
            state, state.Fingerprint, last.TicketStart);
        Assert.Equal(CanonicalJson.Sha256(bySourceTicket.Run),
            CanonicalJson.Sha256(bySemanticId.Run));
        Assert.Equal("nature", bySourceTicket.History[^1].Kind);
        Assert.Equal("semantic_intent", bySemanticId.History[^1].Kind);
        Assert.Throws<InvalidOperationException>(() =>
            SemanticPilotCombatEngineV111.ResolveIntent(
                state, state.Fingerprint, "forged-intent-id"));
        Assert.Equal(bySemanticId.Fingerprint,
            SemanticPilotCombatEngineV111.Restore(
                SemanticPilotCombatEngineV111.Save(bySemanticId)).Fingerprint);
    }

    private static SemanticSeededEntryV111 Seeded(string seed) =>
        new(seed, 2, PrototypeNativeHiveNormals.ExoskeletonsNormalId,
            "ExoskeletonsNormal", 20, 0, 1, 3, 5);

    [Fact]
    public void SeededSemanticEntryRequiresOnlyTwoExternalChanceSitesAndReplays()
    {
        var entry = Seeded("semantic-p0-seeded-opening");
        var state = SemanticPilotCombatEngineV111.BeginSeeded(entry);
        Assert.True(state.AtPlayerBoundary);
        Assert.Equal(entry, state.SeededEntry);
        Assert.Null(state.IntentPending);
        Assert.Null(state.DrawPending);
        Assert.Equal(0, state.Run.World!.Combat!.SemanticDeck!.ShuffleEpoch);
        Assert.Empty(state.Run.World.Combat.SemanticDeck.Transcript!);
        Assert.Empty(state.Run.World.Combat.SemanticDeck.Pools);
        Assert.Equal(state.Run.World.Combat.DrawPile.Reverse(),
            state.Run.World.Combat.SemanticDeck.Slots.Select(
                x => x.KnownCardInstanceId!.Value));
        Assert.Equal(state.Fingerprint,
            SemanticPilotCombatEngineV111.Restore(
                SemanticPilotCombatEngineV111.Save(state)).Fingerprint);

        var action = Assert.Single(new PrototypeAiEnvironment()
            .Observe(state.Run).LegalActions, a => a.Kind == "end_turn");
        state = SemanticPilotCombatEngineV111.Step(
            state, state.Fingerprint, action.ActionId);
        int intents = 0;
        for (var count = 0; !state.AtPlayerBoundary && !state.CombatTerminal; count++)
        {
            Assert.True(count < 120);
            Assert.True(state.IntentPending is not null ||
                state.DrawPending is not null);
            if (state.IntentPending is not null) intents++;
            state = SemanticPilotCombatEngineV111.Resolve(
                state, state.Fingerprint, 0);
        }
        Assert.True(intents > 0);
        Assert.True(state.AtPlayerBoundary || state.CombatTerminal);
        Assert.Equal(state.Fingerprint,
            SemanticPilotCombatEngineV111.Restore(
                SemanticPilotCombatEngineV111.Save(state)).Fingerprint);
    }

    [Fact]
    public void SeededNativePolicyAuditAnnotatesInitialWeightedIntents()
    {
        var entry = Seeded("semantic-native-policy-initial-intent-audit") with
        {
            NativePolicyBaseline = true
        };

        var state = SemanticPilotCombatEngineV111.BeginSeeded(entry);
        var audit = Assert.IsType<SemanticNativePolicyAuditV111>(
            state.NativePolicyAudit);
        var intentDecisions = audit.Decisions.Where(decision =>
            decision.Request.SiteId ==
                NativeCombatRngEntryV111.CommittedIntentSiteId).ToArray();

        Assert.NotEmpty(intentDecisions);
        Assert.Equal(intentDecisions.Length, audit.WeightedIntentOutcomes.Length);
        Assert.All(audit.WeightedIntentOutcomes, outcome => outcome.Validate());
        Assert.Equal(SemanticNativePolicyAuditV111.WeightedIntentAuditSchemaV1,
            audit.WeightedIntentAuditSchema);
    }

    [Fact]
    public void SeededOpeningProvenanceRejectsTamperedReplayAndMatchesFifteenWorkers()
    {
        var entry = Seeded("semantic-p0-seeded-multiworker");
        var initial = SemanticPilotCombatEngineV111.BeginSeeded(entry);
        var forged = initial with
        {
            SeededEntry = entry with { TotalFloor = entry.TotalFloor + 1 }
        };
        Assert.Throws<InvalidDataException>(() =>
            SemanticPilotCombatEngineV111.Restore(
                System.Text.Json.JsonSerializer.Serialize(forged)));
        var single = Enumerable.Range(0, 15).Select(_ =>
            SemanticPilotCombatEngineV111.BeginSeeded(entry).Fingerprint).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, i => parallel[i] =
            SemanticPilotCombatEngineV111.BeginSeeded(entry).Fingerprint);
        Assert.Equal(single, parallel);
        Assert.All(parallel, value => Assert.Equal(initial.Fingerprint, value));
    }

    [Fact]
    public void SourcePinnedActOneWeakCombatWinsByOrdinarySilentStrikeActions()
    {
        var entry = new SemanticSeededEntryV111(
            "nature-p0-weak-real-victory", 1,
            "proto.encounter.shrinker_beetle_weak",
            "ShrinkerBeetleWeak", 2, 0, 1, 3, 1,
            ReplayOpeningDrawHooks: true);
        var state = SemanticPilotCombatEngineV111.BeginSeeded(entry);
        Assert.True(state.AtPlayerBoundary);
        Assert.Single(state.Run.World!.Combat!.Enemies);
        int actions = 0, nature = 0, strikeActions = 0;
        while (!state.CombatTerminal)
        {
            Assert.True(++actions + nature < 600,
                "Expected a genuine starter-deck Act 1 victory.");
            if (state.AtPlayerBoundary)
            {
                var combat = state.Run.World!.Combat!;
                var frame = new PrototypeAiEnvironment().Observe(state.Run);
                var strike = frame.LegalActions.FirstOrDefault(action =>
                    action.Kind == "play_card" &&
                    action.Payload.Deserialize<PlayCardPayload>() is { } card &&
                    combat.Cards.Any(instance =>
                        instance.InstanceId == card.CardInstanceId &&
                        instance.CardId == "proto.silent.strike"));
                var defend = frame.LegalActions.FirstOrDefault(action =>
                    action.Kind == "play_card" &&
                    action.Payload.Deserialize<PlayCardPayload>() is { } card &&
                    combat.Cards.Any(instance =>
                        instance.InstanceId == card.CardInstanceId &&
                        instance.CardId == "proto.silent.defend"));
                var chosen = strike ?? defend ??
                    Assert.Single(frame.LegalActions, a => a.Kind == "end_turn");
                if (strike is not null) strikeActions++;
                state = SemanticPilotCombatEngineV111.Step(
                    state, state.Fingerprint, chosen.ActionId);
            }
            else if (state.IntentPending is not null)
            {
                var support = ControlledTurnIntentEngineV111
                    .DescribePendingSemanticIntent(state.PendingEndTurn!.Intents!)!;
                state = SemanticPilotCombatEngineV111.ResolveIntent(
                    state, state.Fingerprint, support.Candidates[0].IntentId);
                nature++;
            }
            else if (state.DrawPending is not null)
            {
                state = SemanticPilotCombatEngineV111.Resolve(
                    state, state.Fingerprint, 0);
                nature++;
            }
            else
                throw new InvalidDataException("Unexpected semantic boundary.");
        }
        Assert.True(strikeActions >= 6);
        Assert.Equal("victory", state.CombatOutcome);
        Assert.Equal(RunPhase.Reward, state.Run.Phase);
        Assert.Null(state.PendingEndTurn);
        Assert.Null(state.IntentPending);
        Assert.Null(state.DrawPending);
        Assert.True(state.Run.Player.Hp > 0);
        Assert.NotNull(state.RewardAudit);
        Assert.Equal(SemanticRewardAuditV111.PrototypeProviderId,
            state.RewardAudit!.ProviderId);
        Assert.True(state.RewardAudit.Calls > 0);
        Assert.Equal("UNVERIFIED", state.RewardAudit.ProbabilityStatus);
        Assert.Equal(state.Fingerprint,
            SemanticPilotCombatEngineV111.Restore(
                SemanticPilotCombatEngineV111.Save(state)).Fingerprint);
    }

    [Fact]
    public void SourceRouteWeakVictorySeedReachesSemanticCombatBoundary()
    {
        var state = SemanticPilotCombatEngineV111.BeginSeeded(
            new SemanticSeededEntryV111(
                "nature-p0-weak-real-victory", 1,
                "@source-route", "@source-route", 1, 0, 1, null, null,
                SourceRouteEntry: true,
                RouteActOneRegion: ActIdentity.Overgrowth));

        while (state.IntentPending is not null)
        {
            var support = ControlledTurnIntentEngineV111
                .DescribePendingSemanticIntent(
                    state.PendingInitialIntents!)!;
            state = SemanticPilotCombatEngineV111.ResolveIntent(
                state, state.Fingerprint, support.Candidates[0].IntentId);
        }
        Assert.True(state.AtPlayerBoundary);
    }

    [Fact]
    public void SeededOpeningReusesExactCommittedOrderThroughRealCardDrawnHooks()
    {
        var entry = Seeded("semantic-143-opening-real-hooks");
        var legacy = SemanticPilotCombatEngineV111.BeginSeeded(entry);
        var state = SemanticPilotCombatEngineV111.BeginSeeded(
            entry with { ReplayOpeningDrawHooks = true });
        var oldCombat = legacy.Run.World!.Combat!;
        var combat = state.Run.World!.Combat!;
        Assert.True(state.AtPlayerBoundary);
        Assert.Equal(oldCombat.Hand, combat.Hand);
        Assert.Equal(oldCombat.DrawPile, combat.DrawPile);
        Assert.Equal(oldCombat.Hand.Length +
            oldCombat.CounterState.CardsDrawnThisCombat,
            combat.CounterState.CardsDrawnThisCombat);
        Assert.Equal(oldCombat.SemanticDeck!.ShuffleEpoch,
            combat.SemanticDeck!.ShuffleEpoch);
        Assert.Equal(oldCombat.SemanticDeck.Slots,
            combat.SemanticDeck.Slots);
        Assert.Empty(combat.SemanticDeck.Transcript!);
        Assert.Null(combat.SemanticDeck.Pending);
        Assert.Null(state.DrawPending);
        Assert.Equal(new PrototypeAiEnvironment()
                .Observe(legacy.Run).LegalActions.Select(a => a.ActionId),
            new PrototypeAiEnvironment()
                .Observe(state.Run).LegalActions.Select(a => a.ActionId));
        Assert.Equal(state.Fingerprint,
            SemanticPilotCombatEngineV111.Restore(
                SemanticPilotCombatEngineV111.Save(state)).Fingerprint);
    }

    [Fact]
    public void SeededSneckoCostsUsePrivateNamedStreamAndReproduceOnRestore()
    {
        var entry = Seeded("semantic-p0-named-cost-stream") with
        {
            OtherChanceProviderId = SemanticOtherChanceEngineV111.ProviderId,
            FixtureDrawCostRelicId = "proto.relic.snecko_eye"
        };
        var state = SemanticPilotCombatEngineV111.BeginSeeded(entry);
        Assert.NotNull(state.OtherChance);
        Assert.Empty(state.OtherChance!.Decisions);
        var openingHash = state.Fingerprint;
        var action = Assert.Single(new PrototypeAiEnvironment()
            .Observe(state.Run).LegalActions, a => a.Kind == "end_turn");
        state = SemanticPilotCombatEngineV111.Step(
            state, state.Fingerprint, action.ActionId);
        for (int stage = 0; !state.AtPlayerBoundary && !state.CombatTerminal; stage++)
        {
            Assert.True(stage < 150);
            if (state.IntentPending is { })
            {
                var support = ControlledTurnIntentEngineV111
                    .DescribePendingSemanticIntent(state.PendingEndTurn!.Intents!)!;
                state = SemanticPilotCombatEngineV111.ResolveIntent(
                    state, state.Fingerprint, support.Candidates[0].IntentId);
            }
            else if (state.DrawPending is { })
                state = SemanticPilotCombatEngineV111.Resolve(
                    state, state.Fingerprint, 0);
            else
                throw new InvalidDataException("Unexpected non-two-site Nature event.");
        }
        Assert.True(state.AtPlayerBoundary || state.CombatTerminal);
        Assert.NotNull(state.OtherChance);
        var decisions = state.OtherChance!.Decisions;
        Assert.NotEmpty(decisions);
        Assert.All(decisions, d =>
        {
            Assert.Equal("native-v111", d.ProviderMode);
            Assert.Equal(SemanticOtherChanceEngineV111.CostStreamId,
                d.Request.StreamId);
            Assert.Equal("UNVERIFIED", d.ProbabilityStatus);
            Assert.NotNull(d.NativeCursorBefore);
            Assert.NotNull(d.NativeCursorAfter);
        });
        Assert.All(decisions.Select((decision, i) => (decision, i)), pair =>
            Assert.Equal(pair.i, pair.decision.Request.Ordinal));
        Assert.Equal(state.Fingerprint,
            SemanticPilotCombatEngineV111.Restore(
                SemanticPilotCombatEngineV111.Save(state)).Fingerprint);
        Assert.NotEqual(openingHash, state.Fingerprint);
        var original = SemanticPilotCombatEngineV111.BeginSeeded(entry);
        Assert.Empty(original.OtherChance!.Decisions);
    }

    [Fact]
    public void SeededCombatEndTurnsCanReachActualDefeatWithoutPhantomIntent()
    {
        var state = SemanticPilotCombatEngineV111.BeginSeeded(
            Seeded("semantic-p0-real-terminal-defeat"));
        Assert.True(state.AtPlayerBoundary);
        int turns = 0;
        int intentChoices = 0;
        while (!state.CombatTerminal && turns < 40)
        {
            Assert.True(state.AtPlayerBoundary);
            var end = Assert.Single(new PrototypeAiEnvironment()
                .Observe(state.Run).LegalActions,
                action => action.Kind == "end_turn");
            state = SemanticPilotCombatEngineV111.Step(
                state, state.Fingerprint, end.ActionId);
            turns++;
            for (int choices = 0; !state.AtPlayerBoundary && !state.CombatTerminal;
                 choices++)
            {
                Assert.True(choices < 100, "Unfinished real turn continuation.");
                if (state.IntentPending is not null)
                {
                    var support = ControlledTurnIntentEngineV111
                        .DescribePendingSemanticIntent(
                            state.PendingEndTurn!.Intents!)!;
                    state = SemanticPilotCombatEngineV111.ResolveIntent(
                        state, state.Fingerprint, support.Candidates[0].IntentId);
                    intentChoices++;
                }
                else if (state.DrawPending is not null)
                    state = SemanticPilotCombatEngineV111.Resolve(
                        state, state.Fingerprint, 0);
                else
                    throw new InvalidDataException("Missing supported two-site ticket.");
            }
        }
        Assert.True(intentChoices > 0);
        Assert.True(state.CombatTerminal, "Expected an actual terminal fight.");
        Assert.Equal("defeat", state.CombatOutcome);
        Assert.True(state.Run.Player.Hp <= 0);
        Assert.Null(state.PendingEndTurn);
        Assert.Null(state.IntentPending);
        Assert.Null(state.DrawPending);
        Assert.Equal(state.Fingerprint,
            SemanticPilotCombatEngineV111.Restore(
                SemanticPilotCombatEngineV111.Save(state)).Fingerprint);
        Assert.Throws<InvalidOperationException>(() =>
            SemanticPilotCombatEngineV111.Step(
                state, state.Fingerprint, "end_turn"));
    }

    [Fact]
    public void FifteenWorkersRemainDeterministicAfterRealSemanticIntentTurns()
    {
        static string Rollout(int worker)
        {
            var state = SemanticPilotCombatEngineV111.Begin(
                Opening("semantic-p0-worker-" + worker));
            var action = Assert.Single(new PrototypeAiEnvironment()
                .Observe(state.Run).LegalActions, a => a.Kind == "end_turn");
            state = SemanticPilotCombatEngineV111.Step(
                state, state.Fingerprint, action.ActionId);
            var steps = 0;
            while (!state.AtPlayerBoundary)
            {
                Assert.True(++steps < 120);
                var support = state.IntentPending?.SupportCount ??
                    state.DrawPending?.CandidateCardInstanceIds.Length ??
                    throw new InvalidOperationException("Unexpected non-two-site chance.");
                state = SemanticPilotCombatEngineV111.Resolve(
                    state, state.Fingerprint, worker % support);
            }
            Assert.Equal(state.Fingerprint,
                SemanticPilotCombatEngineV111.Restore(
                    SemanticPilotCombatEngineV111.Save(state)).Fingerprint);
            return state.Fingerprint + ":" + string.Join(",",
                state.History.Select(x => x.Kind + ":" +
                    (x.ActionId ?? x.SelectedIndex?.ToString())));
        }
        var single = Enumerable.Range(0, 15).Select(Rollout).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, i => parallel[i] = Rollout(i));
        Assert.Equal(single, parallel);
    }
}
