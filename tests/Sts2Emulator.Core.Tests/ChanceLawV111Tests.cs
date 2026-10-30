using System.Globalization;
using System.Text.Json;
using Sts2Emulator.Core;
using Sts2Emulator.Cli;

namespace Sts2Emulator.Core.Tests;

public sealed class ChanceLawV111Tests
{
    [Fact]
    public void StochasticCapabilitiesNegotiateOnlyImplementedUnverifiedSites()
    {
        var capabilities = StochasticCapabilitiesV111.Create();

        Assert.Equal(StochasticCapabilitiesV111.SchemaId,
            capabilities.SchemaId);
        Assert.Equal(ChanceLawV111.CurrentSchemaId,
            capabilities.ChanceLawSchemaId);
        Assert.Equal("UNVERIFIED", capabilities.ProbabilityStatus);
        Assert.Equal(PrototypeAiEnvironment.SchemaId,
            capabilities.PlayerObservationSchemaId);
        Assert.Contains("combat.committed_intent.index_draw",
            capabilities.NatureChoiceSiteIds);
        Assert.Contains("combat.next_turn_intent.index_draw",
            capabilities.NatureChoiceSiteIds);
        Assert.Contains("combat.draw_pile.draw_card",
            capabilities.NatureChoiceSiteIds);
        Assert.Contains(ReferenceNatureV111.ProviderId,
            capabilities.ProviderModes);
        Assert.Contains(SemanticNativePolicyAuditV111.NativeProviderId,
            capabilities.ProviderModes);
        Assert.Contains(StochasticCapabilitiesV111.ExternalSelectionProviderId,
            capabilities.ProviderModes);
        Assert.Contains("semantic_reference_resolve_next",
            capabilities.ReferenceNatureOperations);
        Assert.Contains("semantic_reference_save",
            capabilities.ReferenceNatureOperations);
        Assert.Contains("semantic_reference_load",
            capabilities.ReferenceNatureOperations);
        Assert.Equal(CanonicalJson.Serialize(capabilities),
            CanonicalJson.Serialize(
                PrototypeCapabilities.Create().StochasticV111));
    }

    [Fact]
    public void JsonlReferenceNatureSessionCarriesLawAndSamplerThroughLifecycle()
    {
        var entry = Seeded("jsonl-reference-nature-lifecycle");
        var combat = SemanticPilotCombatEngineV111.BeginSeeded(entry);
        var environment = new PrototypeAiEnvironment();
        var endTurn = Assert.Single(environment.Observe(combat.Run).LegalActions,
            action => action.Kind == "end_turn");
        var initial = ReferenceNatureSessionV111.Begin(combat, 441);
        var stepped = initial with
        {
            Combat = SemanticPilotCombatEngineV111.Step(
                combat, combat.Fingerprint, endTurn.ActionId)
        };
        var resolved = stepped.ResolveNext();

        var requests = new object[]
        {
            new { op = "hello" },
            new
            {
                op = "semantic_begin_seeded",
                seed = entry.Seed,
                act = entry.Act,
                encounter_id = entry.EncounterId,
                native_entry = entry.NativeEncounterEntry,
                total_floor = entry.TotalFloor,
                ascension = entry.Ascension,
                first_monster_combat_id = entry.FirstMonsterCombatId,
                map_column = entry.MapColumn,
                map_row = entry.MapRow
            },
            new { op = "semantic_reference_begin", semantic_handle = "m1",
                nature_seed = 441UL },
            new { op = "semantic_reference_observe",
                reference_nature_handle = "r1", policy_id = "prototype-fair-v0" },
            new { op = "semantic_reference_step", reference_nature_handle = "r1",
                fingerprint = initial.Fingerprint, action_id = endTurn.ActionId },
            new { op = "semantic_reference_resolve_next",
                reference_nature_handle = "r2", fingerprint = stepped.Fingerprint },
            new { op = "semantic_reference_save", reference_nature_handle = "r3" },
            new { op = "semantic_reference_load",
                snapshot = ReferenceNatureSessionV111.Save(resolved) },
            new { op = "semantic_reference_fork", reference_nature_handle = "r3" }
        };
        using var input = new StringReader(string.Join(Environment.NewLine,
            requests.Select(request => JsonSerializer.Serialize(request))));
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        PrototypeAiJsonlServer.Run(input, output);
        var responses = output.ToString().Split(
            Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line)).ToArray();
        try
        {
            var hello = responses[0].RootElement;
            Assert.Equal(StochasticCapabilitiesV111.SchemaId,
                hello.GetProperty("stochasticCapabilities")
                    .GetProperty("schemaId").GetString());

            var referenceStep = responses[4].RootElement
                .GetProperty("result");
            Assert.Equal("nature", referenceStep.GetProperty("boundary").GetString());
            var pending = referenceStep.GetProperty("pending");
            Assert.Equal("sts2-v111-chance-law-v1",
                pending.GetProperty("chanceLaw").GetProperty("schemaId").GetString());
            Assert.DoesNotContain("chanceLaw",
                responses[3].RootElement.GetProperty("payloadJson").GetString());

            var resolvedResult = responses[5].RootElement.GetProperty("result");
            Assert.Equal(1, resolvedResult.GetProperty("natureSampleCount").GetInt64());
            Assert.Equal(resolved.Fingerprint,
                resolvedResult.GetProperty("fingerprint").GetString());
            Assert.Equal(resolved.Fingerprint,
                responses[7].RootElement.GetProperty("result")
                    .GetProperty("fingerprint").GetString());
            Assert.Equal(resolved.Fingerprint,
                responses[8].RootElement.GetProperty("result")
                    .GetProperty("fingerprint").GetString());
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    private static SemanticIntentEventV111 IntentEvent(
        string eventId, string boundaryFingerprint)
    {
        var support = new SemanticIntentSupportV111(
            "enemy:17",
            [
                new("enemy:17:intent:jab", "jab", 0, 2, 0),
                new("enemy:17:intent:guard", "guard", 1, 3, 2),
                new("enemy:17:intent:charge", "charge", 2, 1, 5)
            ], 6);
        return new SemanticIntentEventV111(
            SemanticIntentEventV111.EventSchemaId, eventId,
            boundaryFingerprint, CanonicalJson.Sha256(support),
            "combat.next_turn_intent.index_draw", "run.monster_ai",
            support.OwnerId, 4, support);
    }

    [Fact]
    public void WeightedIntentLawIsExactAndIndependentOfFullBoundaryIdentity()
    {
        var first = ChanceLawEngineV111.QueryIntent(
            IntentEvent("intent-event-a", "private-boundary-a"));
        var second = ChanceLawEngineV111.QueryIntent(
            IntentEvent("intent-event-b", "private-boundary-b"));

        Assert.Equal(6, first.TotalWeight);
        Assert.Equal("UNVERIFIED", first.ProbabilityStatus);
        Assert.Equal("source-shaped-unverified", first.ProbabilityProvenance);
        Assert.Equal(new long[] { 2, 3, 1 },
            first.Outcomes.Select(outcome => outcome.Weight));
        Assert.Equal(first.LawId, second.LawId);
        Assert.Equal(first.ConditioningFingerprint,
            second.ConditioningFingerprint);
        Assert.NotEqual(first.EventId, second.EventId);
        Assert.Equal(first.Outcomes, second.Outcomes);
        first.Validate();
    }

    [Fact]
    public void ExperimentalLawDowngradeRecomputesOneCanonicalIdentity()
    {
        var original = ChanceLawEngineV111.QueryIntent(
            IntentEvent("h15-downgrade-event", "boundary"));
        var originalFingerprint = CanonicalJson.Sha256(original);
        var replacement = original.Outcomes
            .Select(item => item with { Weight = 1L }).ToArray();
        var downgraded = original.WithUnverifiedProvenance(
            "h15-independent-model-unverified", replacement);

        downgraded.Validate();
        Assert.Equal(ChanceLawV111.UnverifiedStatus,
            downgraded.ProbabilityStatus);
        Assert.Equal("h15-independent-model-unverified",
            downgraded.ProbabilityProvenance);
        Assert.Equal(3L, downgraded.TotalWeight);
        Assert.NotEqual(original.LawId, downgraded.LawId);
        Assert.NotEqual(original.SupportFingerprint,
            downgraded.SupportFingerprint);
        Assert.Equal(originalFingerprint, CanonicalJson.Sha256(original));
        Assert.Equal(
            downgraded.ComputeCanonicalLawId(), downgraded.LawId);
        Assert.Throws<InvalidDataException>(() =>
            (downgraded with { LawId = original.LawId }).Validate());
    }

    [Fact]
    public void IntentCandidateReorderingPreservesIdsAndInvalidatesSupportFingerprint()
    {
        var original = IntentEvent("intent-order-a", "boundary-a");
        var originalLaw = ChanceLawEngineV111.QueryIntent(original);
        var nextTicket = 0;
        var reorderedCandidates = original.Support.Candidates.Reverse()
            .Select((candidate, index) =>
            {
                var reorderedCandidate = candidate with
                {
                    MoveIndex = index,
                    TicketStart = nextTicket
                };
                nextTicket += candidate.RawWeight;
                return reorderedCandidate;
            }).ToArray();
        var reorderedSupport = original.Support with
        {
            Candidates = reorderedCandidates,
            TotalRawWeight = nextTicket
        };
        var reordered = original with
        {
            EventId = "intent-order-b",
            BoundaryFingerprint = "boundary-b",
            Support = reorderedSupport,
            SupportFingerprint = CanonicalJson.Sha256(reorderedSupport)
        };
        var reorderedLaw = ChanceLawEngineV111.QueryIntent(reordered);

        Assert.Equal(
            originalLaw.Outcomes.Select(outcome => outcome.OutcomeId).Order(),
            reorderedLaw.Outcomes.Select(outcome => outcome.OutcomeId).Order());
        Assert.NotEqual(originalLaw.SupportFingerprint,
            reorderedLaw.SupportFingerprint);
        Assert.NotEqual(originalLaw.LawId, reorderedLaw.LawId);
    }

    [Fact]
    public void DrawLawMatchesIndependentEnumerationOfConstrainedPhysicalPermutations()
    {
        var root = SemanticDeckEngineV111.BeginNature([11, 22, 33, 44]);
        root = SemanticDeckEngineV111.InsertKnown(root, 2, 99);
        root = SemanticDeckEngineV111.PlaceTop(root, 98);
        root = SemanticDeckEngineV111.PlaceBottom(root, 97);
        var completedOrders = new Dictionary<string, double>(StringComparer.Ordinal);

        void Visit(SemanticDeckStateV111 deck, long[] prefix, double probability)
        {
            if (deck.Count == 0)
            {
                var key = string.Join(",", prefix);
                completedOrders[key] = completedOrders.GetValueOrDefault(key) + probability;
                return;
            }

            var beforeQuery = deck.Fingerprint;
            var pending = SemanticDeckEngineV111.Require(deck, 0, "draw");
            if (pending.Pending is null)
            {
                var (remaining, cardId) =
                    SemanticDeckEngineV111.DrawKnown(pending);
                Visit(remaining, prefix.Append(cardId).ToArray(), probability);
                return;
            }

            var pendingFingerprint = pending.Fingerprint;
            var law = ChanceLawEngineV111.QueryDraw(pending);
            Assert.Equal(pendingFingerprint, pending.Fingerprint);
            Assert.Equal(beforeQuery, deck.Fingerprint);
            Assert.Equal(pending.Fingerprint, law.ConditioningFingerprint);

            var referenceOrders = IndependentCompletions(pending).ToArray();
            var referenceFirstCounts = referenceOrders
                .GroupBy(order => order[0])
                .ToDictionary(group => group.Key, group => group.Count());
            var lawIds = law.Outcomes.Select(outcome => ParseCardId(outcome.OutcomeId));
            Assert.Equal(referenceFirstCounts.Keys.Order(), lawIds.Order());
            foreach (var outcome in law.Outcomes)
            {
                var cardId = ParseCardId(outcome.OutcomeId);
                Assert.Equal(
                    (long)referenceFirstCounts[cardId] * law.TotalWeight,
                    outcome.Weight * referenceOrders.Length);
            }

            for (var index = 0; index < pending.Pending.CandidateCardInstanceIds.Length; index++)
            {
                var cardId = pending.Pending.CandidateCardInstanceIds[index];
                var child = SemanticDeckEngineV111.Resolve(
                    pending, pending.Fingerprint, index);
                var (remaining, drawnId) =
                    SemanticDeckEngineV111.DrawKnown(child);
                Assert.Equal(cardId, drawnId);
                Visit(remaining, prefix.Append(drawnId).ToArray(),
                    probability * law.Outcomes[index].Weight / law.TotalWeight);
            }
        }

        Visit(root, [], 1.0);
        Assert.Equal(24, completedOrders.Count);
        Assert.All(completedOrders.Values,
            probability => Assert.Equal(1.0 / 24.0, probability, 12));
        Assert.Equal(1.0, completedOrders.Values.Sum(), 12);
    }

    [Fact]
    public void DrawLawRejectsCommittedAndKnownCardsInsteadOfRerandomizingThem()
    {
        var native = SemanticDeckEngineV111.BeginNative([1, 2]);
        Assert.Throws<InvalidOperationException>(() =>
            ChanceLawEngineV111.QueryDraw(native));

        var nature = SemanticDeckEngineV111.PlaceTop(
            SemanticDeckEngineV111.BeginNature([1, 2]), 9);
        Assert.Throws<InvalidOperationException>(() =>
            ChanceLawEngineV111.QueryDraw(nature));
    }

    [Fact]
    public void ReferenceNatureSamplingMatchesWeightsAndReplaysFromForkedState()
    {
        var law = ChanceLawEngineV111.QueryIntent(
            IntentEvent("sample-event", "sample-boundary"));
        var state = ReferenceNatureStateV111.Create(9123401);
        var fork = state.Fork();
        var first = ReferenceNatureV111.Sample(law, state);
        Assert.Equal(first, ReferenceNatureV111.Sample(law, fork));

        var frequencies = law.Outcomes.ToDictionary(
            outcome => outcome.OutcomeId, _ => 0, StringComparer.Ordinal);
        var samples = 24_000;
        for (var sample = 0; sample < samples; sample++)
        {
            var selected = ReferenceNatureV111.Sample(law, state);
            frequencies[selected.OutcomeId]++;
            state = selected.NextState;
        }

        foreach (var outcome in law.Outcomes)
        {
            var expected = samples * (double)outcome.Weight / law.TotalWeight;
            Assert.InRange(frequencies[outcome.OutcomeId],
                (int)Math.Floor(expected - 220),
                (int)Math.Ceiling(expected + 220));
        }
        Assert.Equal(samples, state.SampleCount);
    }

    [Fact]
    public void ReferenceNatureSessionForksAndRestoresIntentContinuationAtomically()
    {
        var combat = SemanticPilotCombatEngineV111.BeginSeeded(Seeded(
            "reference-nature-session-intent"));
        var environment = new PrototypeAiEnvironment();
        var endTurn = Assert.Single(environment.Observe(combat.Run).LegalActions,
            action => action.Kind == "end_turn");
        combat = SemanticPilotCombatEngineV111.Step(
            combat, combat.Fingerprint, endTurn.ActionId);
        Assert.NotNull(combat.IntentPending);

        var session = ReferenceNatureSessionV111.Begin(combat, 441);
        var originalFingerprint = session.Fingerprint;
        var left = session.ResolveNext();
        var right = session.Fork().ResolveNext();
        Assert.Equal(left.Fingerprint, right.Fingerprint);
        Assert.Equal(originalFingerprint, session.Fingerprint);
        Assert.Equal(1, left.NatureState.SampleCount);

        var restored = ReferenceNatureSessionV111.Restore(
            ReferenceNatureSessionV111.Save(left));
        Assert.Equal(left.Fingerprint, restored.Fingerprint);
        Assert.Equal(left.Combat.Fingerprint, restored.Combat.Fingerprint);
        Assert.Equal(left.NatureState, restored.NatureState);
    }

    [Fact]
    public void ReferenceNatureSessionResolvesDeferredDrawThroughOrdinaryContinuation()
    {
        var combat = SemanticPilotCombatEngineV111.BeginSeeded(Seeded(
            "reference-nature-session-draw"));
        for (var stage = 0; combat.DrawPending is null; stage++)
        {
            Assert.True(stage < 200,
                "Expected the seeded combat to reach a deferred draw.");
            if (combat.IntentPending is not null)
            {
                var support = ControlledTurnIntentEngineV111
                    .DescribePendingSemanticIntent(
                        combat.PendingEndTurn?.Intents ??
                        combat.PendingInitialIntents!)!;
                combat = SemanticPilotCombatEngineV111.ResolveIntent(
                    combat, combat.Fingerprint,
                    support.Candidates[0].IntentId);
            }
            else if (combat.AtPlayerBoundary && !combat.CombatTerminal)
            {
                var endTurn = Assert.Single(new PrototypeAiEnvironment()
                    .Observe(combat.Run).LegalActions,
                    action => action.Kind == "end_turn");
                combat = SemanticPilotCombatEngineV111.Step(
                    combat, combat.Fingerprint, endTurn.ActionId);
            }
            else
            {
                Assert.Fail("Expected an intent, player, or draw boundary.");
            }
        }

        var pending = combat.DrawPending!;
        var session = ReferenceNatureSessionV111.Begin(combat, 8821);
        var resolved = session.ResolveNext();
        var decision = Assert.Single(resolved.Combat.History,
            transition => transition.Kind == "semantic_draw").DrawDecision!;
        Assert.Equal(pending.EventId, decision.Event.EventId);
        Assert.Contains(decision.SelectedCardInstanceId,
            pending.CandidateCardInstanceIds);
        Assert.Equal(1, resolved.NatureState.SampleCount);
        Assert.Equal(resolved.Fingerprint,
            ReferenceNatureSessionV111.Restore(
                ReferenceNatureSessionV111.Save(resolved)).Fingerprint);
    }

    private static SemanticSeededEntryV111 Seeded(string seed) =>
        new(seed, 2, PrototypeNativeHiveNormals.ExoskeletonsNormalId,
            "ExoskeletonsNormal", 20, 0, 1, 3, 5);

    private static long ParseCardId(string outcomeId) =>
        long.Parse(outcomeId["card_instance:".Length..],
            CultureInfo.InvariantCulture);

    private static IEnumerable<long[]> IndependentCompletions(
        SemanticDeckStateV111 state)
    {
        var assignments = new Dictionary<string, long[]>(StringComparer.Ordinal);
        var results = new List<long[]>();

        void AssignPool(int poolIndex)
        {
            if (poolIndex == state.Pools.Length)
            {
                var offsets = state.Pools.ToDictionary(
                    pool => pool.PoolId, _ => 0, StringComparer.Ordinal);
                var order = new long[state.Slots.Length];
                for (var slotIndex = 0; slotIndex < state.Slots.Length; slotIndex++)
                {
                    var slot = state.Slots[slotIndex];
                    if (slot.KnownCardInstanceId is long knownId)
                    {
                        order[slotIndex] = knownId;
                    }
                    else
                    {
                        var poolId = slot.PoolId!;
                        var offset = offsets[poolId];
                        order[slotIndex] = assignments[poolId][offset];
                        offsets[poolId] = offset + 1;
                    }
                }
                results.Add(order);
                return;
            }

            var pool = state.Pools[poolIndex];
            foreach (var permutation in Permutations(pool.RemainingCardInstanceIds))
            {
                assignments[pool.PoolId] = permutation;
                AssignPool(poolIndex + 1);
            }
            assignments.Remove(pool.PoolId);
        }

        AssignPool(0);
        return results;
    }

    private static IEnumerable<long[]> Permutations(long[] values)
    {
        if (values.Length == 0)
        {
            yield return [];
            yield break;
        }

        for (var index = 0; index < values.Length; index++)
        {
            var head = values[index];
            var remainder = values.Where((_, candidateIndex) =>
                candidateIndex != index).ToArray();
            foreach (var tail in Permutations(remainder))
                yield return [head, .. tail];
        }
    }
}
