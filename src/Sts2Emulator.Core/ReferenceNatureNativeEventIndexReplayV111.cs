namespace Sts2Emulator.Core;

/// <summary>Replays a typed Native index transcript through its ordinary
/// player action while preserving the relevant Native stream cursor.</summary>
internal static class ReferenceNatureNativeEventIndexReplayV111
{
    public static RunState ReplayControlledIndex(
        RunState origin, RandomIndexRequestV111 request,
        ControlledStochasticDecisionV111 selected,
        Func<RunState, RunState> mechanics,
        string nativeEventRngEventId, string eventName)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(mechanics);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEventRngEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        selected.Validate();
        var typedRequest = ControlledStochasticRequestV111.FromIndex(request);
        if (!ControlledStochasticChanceReplayV111.RequestsMatch(
                selected.ToRequest(), typedRequest))
            throw new InvalidDataException(
                $"Selected {eventName} outcome differs from its index request.");
        return ReplayControlledPrefix(
            origin, [selected], mechanics, nativeEventRngEventId, eventName,
            candidate => ControlledStochasticChanceReplayV111.RequestsMatch(
                candidate, typedRequest));
    }

    public static RunState ReplayControlledIndices(
        RunState origin, RandomIndexDecisionV111[] decisions,
        Func<RunState, RunState> mechanics,
        string nativeEventRngEventId, string eventName,
        Func<ControlledStochasticRequestV111, bool>? requestFilter = null)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        var selected = decisions.Select(decision =>
            ControlledStochasticDecisionV111.FromIndex(decision)).ToArray();
        var requests = selected.Select(decision => decision.ToRequest())
            .ToArray();
        return ReplayControlledPrefix(
            origin, selected, mechanics, nativeEventRngEventId, eventName,
            requestFilter ?? (candidate => requests.Any(request =>
                ControlledStochasticChanceReplayV111.RequestsMatch(
                    candidate, request))));
    }

    private static RunState ReplayControlledPrefix(
        RunState origin, ControlledStochasticDecisionV111[] selected,
        Func<RunState, RunState> mechanics,
        string nativeEventRngEventId, string eventName,
        Func<ControlledStochasticRequestV111, bool>? requestFilter = null,
        bool validateNativeEventCursor = true)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(mechanics);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEventRngEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        if (selected.Length == 0 || selected.Any(decision =>
                decision is null || !decision.IsIndexOrUnitFloat()))
            throw new InvalidDataException(
                $"{eventName} replay requires a nonempty index/unit-float prefix.");
        var expectedStream = origin.World?.Event?.Stochastic.NativeRng ??
            NativeRngV111Contexts.ForEvent(
                origin.RunSeed, nativeEventRngEventId);
        var replay = ControlledChanceContinuationExecutorV111.CaptureStochastic(
            origin,
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            selected, checkpoint => mechanics(checkpoint.Fork()),
            requestFilter);
        if (replay.Suspended || replay.Run is not { } resolved)
            throw new InvalidDataException(
                $"{eventName} action did not consume exactly its selected index prefix.");
        if (validateNativeEventCursor &&
            CanonicalJson.Sha256(
                resolved.World?.Event?.Stochastic.NativeRng) !=
                CanonicalJson.Sha256(expectedStream))
            throw new InvalidDataException(
                $"Scripted {eventName} resolution advanced its Native event cursor.");
        var world = resolved.World ?? throw new InvalidDataException(
            $"{eventName} replay lost its event world.");
        var eventState = world.Event ?? throw new InvalidDataException(
            $"{eventName} replay lost its event state.");
        return resolved with
        {
            World = world with
            {
                Event = eventState with
                {
                    Stochastic = EventStochasticStateV111.WithProviderMode(
                        eventState.Stochastic, "scripted-v111")
                }
            }
        };
    }

    public static RunState Replay(
        RunState origin, RandomIndexRequestV111 request,
        int selectedIndex, GameAction action, string nativeEventRngEventId,
        string eventName) => Replay(
            origin,
            [new RandomIndexDecisionV111(
                request, selectedIndex, "scripted-v111")],
            action, nativeEventRngEventId, eventName);

    public static RunState Replay(
        RunState origin, IEnumerable<RandomIndexDecisionV111> decisions,
        GameAction action, string nativeEventRngEventId,
        string eventName, string? scriptedSiteId = null,
        string[]? expectedOrderedCandidateIds = null,
        NativeRngSnapshot? expectedRunNicheCursor = null,
        bool preserveEventRng = true,
        string? expectedUnscriptedStreamId = null,
        NativeRngSnapshot? expectedUnscriptedCursorBefore = null,
        int expectedUnscriptedDrawCount = 0)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        if (expectedRunNicheCursor is not null &&
            scriptedSiteId != StochasticSiteRegistryV111.WarHammerEliteUpgradeShuffleSiteId)
            throw new ArgumentException(
                "A run.niche cursor expectation is supported only for War Hammer replay.");
        if ((expectedUnscriptedStreamId is null) !=
                (expectedUnscriptedCursorBefore is null) ||
            expectedUnscriptedDrawCount < 0 ||
            expectedUnscriptedStreamId is not null &&
                expectedRunNicheCursor is not null)
            throw new ArgumentException(
                "An unscripted run-stream expectation requires one stream and its pre-draw cursor.");
        if (expectedUnscriptedStreamId is { } unscriptedStreamId)
        {
            RngStreamState? stream = null;
            if (origin.Stochastic.NativeRunRng is { } nativeRunRng)
                nativeRunRng.TryGetStream(unscriptedStreamId, out stream);
            if (stream is null ||
                NativeRngV111.ToSnapshot(stream) !=
                    expectedUnscriptedCursorBefore)
                throw new InvalidOperationException(
                    "The unscripted run-stream cursor differs from its replay boundary.");
        }
        var decisionArray = decisions.ToArray();
        if (scriptedSiteId is null && expectedOrderedCandidateIds is null &&
            expectedRunNicheCursor is null && preserveEventRng &&
            expectedUnscriptedStreamId is null &&
            expectedUnscriptedDrawCount == 0 && decisionArray.Length > 0)
            return ReplayControlledIndices(
                origin, decisionArray,
                run => new PrototypeGameEngine().Step(
                    run, action).State,
                nativeEventRngEventId, eventName,
                requestFilter: candidate => candidate.IndexRequest is not null);

        RngStreamState? expectedStream = null;
        if (expectedRunNicheCursor is null && preserveEventRng)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(nativeEventRngEventId);
            expectedStream = origin.World?.Event?.Stochastic.NativeRng ??
                NativeRngV111Contexts.ForEvent(
                    origin.RunSeed, nativeEventRngEventId);
        }
        else if (!preserveEventRng &&
            origin.World?.Event?.Stochastic.NativeRng is not null)
            throw new InvalidOperationException(
                "This replay boundary requires the event RNG context to remain absent.");
        var scripted = new ScriptedRandomIndexProviderV111(decisionArray);
        RunState run;
        RandomBoundaryEventV111[] replayIndexEvents = [];
        if (scriptedSiteId is null)
        {
            using var providerScope = StochasticProviderScopeV111.Enter(
                scripted, overrideExplicitProviders: true);
            run = new PrototypeGameEngine().Step(
                origin.Fork(), action).State;
        }
        else
        {
            using var shuffleProbe = NativeEventRandomProbeV111.Begin();
            using var boundaryJournal = RandomBoundaryJournalV111.Begin();
            var selected = decisionArray.Select(decision =>
                ControlledStochasticDecisionV111.FromIndex(decision))
                .ToArray();
            var requests = selected.Select(decision => decision.ToRequest())
                .ToArray();
            var isRepyRelicShuffle = scriptedSiteId ==
                PrototypeNativeHiveEvents.WarHistorianRepyRelicShuffleSiteId;
            using var fallbackScope = StochasticProviderScopeV111.Enter(
                new SelectiveReplayProvider([],
                    new ScriptedRandomIndexProviderV111([])),
                overrideExplicitProviders: true);
            var replay = ControlledChanceContinuationExecutorV111
                .CaptureStochastic(
                    origin,
                    ControlledStochasticContinuationStateV111.CurrentSchemaId,
                    selected,
                    checkpoint => new PrototypeGameEngine().Step(
                        checkpoint.Fork(), action).State,
                    requestFilter: candidate =>
                    {
                        if (candidate.IndexRequest is not { } request)
                            return false;
                        if (requests.Any(selectedRequest =>
                                ControlledStochasticChanceReplayV111.RequestsMatch(
                                    candidate, selectedRequest)))
                            return true;
                        if (request.SiteId == scriptedSiteId)
                            return true;
                        return isRepyRelicShuffle &&
                            request.SiteId ==
                                PrototypeNativeHiveEvents
                                    .WarHistorianRepyPotionOfferSiteId;
                    },
                    indexFallbackProvider: isRepyRelicShuffle
                        ? new NativeRandomIndexProviderV111() : null);
            if (replay.Suspended || replay.Run is not { } completed)
                throw new InvalidDataException(
                    "Scripted Native event shuffle action did not consume its complete typed index prefix.");
            run = completed;
            var matchingCaptures = shuffleProbe.ShuffleCaptures.Where(capture =>
                capture.SiteId == scriptedSiteId).ToArray();
            if (matchingCaptures.Length != 1 ||
                expectedOrderedCandidateIds is null ||
                !matchingCaptures[0].OrderedCandidateIds.SequenceEqual(
                    expectedOrderedCandidateIds, StringComparer.Ordinal))
                throw new InvalidDataException(
                    "Scripted event shuffle candidate identities differ from the replay plan.");
            replayIndexEvents = boundaryJournal.Entries.ToArray();
        }
        if (scriptedSiteId is null)
            scripted.AssertComplete();
        if (expectedRunNicheCursor is not null)
        {
            RngStreamState? runNiche = null;
            if (run.Stochastic.NativeRunRng is { } nativeRunRng)
                nativeRunRng.TryGetStream("run.niche", out runNiche);
            if (run.Phase != RunPhase.Reward ||
                run.World?.Combat is not null || runNiche is null ||
                NativeRngV111.ToSnapshot(runNiche) !=
                    expectedRunNicheCursor)
                throw new InvalidDataException(
                    "Scripted War Hammer victory changed run.niche after its captured pre-shuffle cursor.");
            if (run.World is null)
                throw new InvalidDataException(
                    "Scripted War Hammer victory lost its run world.");
            return run;
        }
        if (!preserveEventRng)
        {
            var replayEventState = run.World?.Event;
            if (replayEventState is null ||
                replayEventState.Stochastic.NativeRng is not null ||
                replayEventState.Stochastic.ProviderMode is not null)
                throw new InvalidDataException(
                    "Scripted player-stream replay changed the absent Native event RNG context.");
            var expectedRewardsCursor = expectedUnscriptedCursorBefore
                ?? throw new InvalidDataException(
                    "Scripted player-stream replay is missing its pre-draw cursor.");
            var streamId = expectedUnscriptedStreamId
                ?? throw new InvalidDataException(
                    "Scripted player-stream replay is missing its stream identity.");
            RngStreamState? finalStream = null;
            if (run.Stochastic.NativeRunRng is { } nativeRunRng)
                nativeRunRng.TryGetStream(streamId, out finalStream);
            var finalCursor = finalStream is null ? null :
                NativeRngV111.ToSnapshot(finalStream);
            var streamEvents = replayIndexEvents.Where(entry =>
                entry.Request.StreamId == streamId).ToArray();
            var nativeEvents = streamEvents.Where(entry =>
                entry.ProviderMode == "native-v111").ToArray();
            var scriptedSwapCount = Math.Max(0,
                (expectedOrderedCandidateIds?.Length ?? 0) - 1);
            var expectedScriptedDrawCount = scriptedSwapCount + 2;
            if (finalStream is null ||
                unchecked((uint)(finalCursor!.Counter -
                    expectedRewardsCursor.Counter)) !=
                    unchecked((uint)expectedUnscriptedDrawCount) ||
                expectedUnscriptedDrawCount == 0 &&
                    finalCursor != expectedRewardsCursor ||
                streamEvents.Length !=
                    expectedScriptedDrawCount + expectedUnscriptedDrawCount ||
                nativeEvents.Length != expectedUnscriptedDrawCount ||
                streamEvents.Take(scriptedSwapCount).Where((entry, index) =>
                    entry.ProviderMode != "controlled-v111" ||
                    entry.Request.SiteId != scriptedSiteId ||
                    entry.Request.OwnerId !=
                        "event:war_historian_repy:relic_offer" ||
                    entry.Request.Ordinal != index ||
                    entry.Request.OperationKind != "bounded_integer" ||
                    entry.Request.BoundedCount !=
                        expectedOrderedCandidateIds!.Length - index).Any() ||
                streamEvents.Skip(scriptedSwapCount)
                    .Take(2).Where((entry, index) =>
                    entry.ProviderMode != "controlled-v111" ||
                    entry.Request.SiteId !=
                        PrototypeNativeHiveEvents
                            .WarHistorianRepyPotionOfferSiteId ||
                    entry.Request.OwnerId !=
                        $"event:war_historian_repy:potion_offer:{index}" ||
                    entry.Request.OperationKind != "choose_index" ||
                    entry.Request.SupportCount !=
                        PrototypeContent.PotionPool.Length).Any() ||
                streamEvents.Skip(scriptedSwapCount + 2)
                    .Where((entry, index) =>
                        entry.ProviderMode != "native-v111" ||
                        entry.Request.SiteId !=
                            PrototypeNativeHiveEvents
                                .WarHistorianRepyPotionOfferSiteId ||
                        entry.Request.OwnerId !=
                            $"event:war_historian_repy:potion_offer:{index + 2}" ||
                        entry.Request.OperationKind != "choose_index" ||
                        entry.Request.SupportCount !=
                            PrototypeContent.PotionPool.Length).Any() ||
                nativeEvents.Where((entry, index) => index > 0 &&
                    entry.Request.Ordinal !=
                        nativeEvents[index - 1].Request.Ordinal + 1).Any())
                throw new InvalidDataException(
                    "Scripted player-stream replay did not retain the complete ordered Repy shuffle and potion request sequence.");
            return run;
        }
        if (CanonicalJson.Sha256(run.World?.Event?.Stochastic.NativeRng) !=
            CanonicalJson.Sha256(expectedStream))
            throw new InvalidDataException(
                $"Scripted {eventName} resolution advanced its Native event cursor.");
        var world = run.World ?? throw new InvalidDataException(
            $"Scripted {eventName} resolution lost its event world.");
        var eventState = world.Event ?? throw new InvalidDataException(
            $"Scripted {eventName} resolution lost its event state.");
        return run with
        {
            World = world with
            {
                Event = eventState with
                {
                    Stochastic = EventStochasticStateV111.WithProviderMode(
                        eventState.Stochastic, "scripted-v111")
                }
            }
        };
    }

    public static RunState ReplayUnitFloat(
        RunState origin, RandomUnitFloatDecisionV111 decision,
        GameAction action, string nativeEventRngEventId,
        string expectedEventId, string scriptedSiteId,
        int expectedPreDrawDishCount)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEventRngEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptedSiteId);
        decision.Validate();
        if (decision.Request.SiteId != scriptedSiteId ||
            expectedPreDrawDishCount < 0 ||
            string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.Stochastic.NativeRunRng is null)
            throw new InvalidOperationException(
                "Scripted event unit-float replay requires its configured native run and exact site.");

        var selected = ControlledStochasticDecisionV111.FromUnitFloat(
            decision with { ProviderMode = "scripted-v111" });
        using var probe = NativeEventRandomProbeV111.Begin();
        using var indexFallbackScope = StochasticProviderScopeV111.Enter(
            new SelectiveReplayProvider([],
                new ScriptedRandomIndexProviderV111([])),
            overrideExplicitProviders: true);
        using var unitFloatFallbackScope = StochasticProviderScopeV111.Enter(
            unitFloatProvider: new SelectiveUnitFloatReplayProvider(
                string.Empty, new ScriptedRandomUnitFloatProviderV111([])),
            overrideExplicitProviders: true);
        var run = ReplayControlledPrefix(
            origin, [selected],
            checkpoint => new PrototypeGameEngine().Step(checkpoint, action).State,
            nativeEventRngEventId, expectedEventId,
            candidate =>
                (candidate.IndexRequest is { } index &&
                    index.SiteId == scriptedSiteId) ||
                (candidate.UnitFloatRequest is { } unitFloat &&
                    unitFloat.SiteId == scriptedSiteId),
            validateNativeEventCursor: false);
        var matchingCaptures = probe.UnitFloatCaptures.Where(capture =>
            capture.Request.SiteId == scriptedSiteId).ToArray();
        var capture = matchingCaptures.Length == 1
            ? matchingCaptures[0] : null;
        var preDrawEvent = capture?.PreDrawState.World?.Event;
        var preDrawStream = preDrawEvent?.Stochastic.NativeRng;
        var expectedStream = NativeRngV111Contexts.ForEvent(
            origin.RunSeed, nativeEventRngEventId);
        var entryDishBoundary = expectedPreDrawDishCount == 0 &&
            preDrawEvent is
            {
                NativePageIndex: 0,
                ChosenChoiceId: null,
                NativeDishCount: 0,
                NativeDishId: null,
                NativeLastDishId: null
            };
        var continuationDishBoundary = expectedPreDrawDishCount > 0 &&
            preDrawEvent is
            {
                NativePageIndex: 1,
                ChosenChoiceId: "grab",
                NativeDishRollPending: false
            } &&
            preDrawEvent.Stochastic.RandomOutcomes.NativeDishCount == expectedPreDrawDishCount &&
            preDrawEvent.Stochastic.RandomOutcomes.NativeDishId is not null &&
            preDrawEvent.Stochastic.RandomOutcomes.NativeDishId == preDrawEvent.Stochastic.RandomOutcomes.NativeLastDishId;
        if (capture is null || preDrawEvent is null ||
            !ControlledStochasticChanceReplayV111.RequestsMatch(
                capture.Request, decision.Request) ||
            capture.PreDrawState.Phase != RunPhase.Event ||
            capture.PreDrawState.World?.Combat is not null ||
            preDrawEvent.EventId != expectedEventId ||
            preDrawEvent.Stochastic.RandomOutcomes.NativeDishRollPending ||
            preDrawEvent.Stochastic.RandomOutcomes.NativeEventPotionSlot
                is not null ||
            preDrawStream is null ||
            preDrawStream.StreamId != expectedStream.StreamId ||
            !(entryDishBoundary || continuationDishBoundary))
            throw new InvalidDataException(
                "Scripted event unit-float request differs from the replay plan.");
        var expectedPostPrefixCursor = NativeRngV111.ToSnapshot(preDrawStream);
        if (run.World?.Event?.Stochastic.NativeRng is not { } replayStream ||
            NativeRngV111.ToSnapshot(replayStream) != expectedPostPrefixCursor)
            throw new InvalidDataException(
                $"Scripted {expectedEventId} resolution advanced its Native event cursor.");
        var world = run.World ?? throw new InvalidDataException(
            $"Scripted {expectedEventId} resolution lost its event world.");
        var eventState = world.Event ?? throw new InvalidDataException(
            $"Scripted {expectedEventId} resolution lost its event state.");
        return run with
        {
            World = world with
            {
                Event = eventState with
                {
                    Stochastic = EventStochasticStateV111.WithProviderMode(
                        eventState.Stochastic, "scripted-v111")
                }
            }
        };
    }

    public static RunState ReplayMixedIndexAndUnitFloat(
        RunState origin, RandomIndexDecisionV111 indexDecision,
        RandomUnitFloatDecisionV111 unitFloatDecision, GameAction action,
        string nativeEventRngEventId, string expectedEventId,
        string scriptedIndexSiteId, string scriptedUnitFloatSiteId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(indexDecision);
        ArgumentNullException.ThrowIfNull(unitFloatDecision);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEventRngEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptedIndexSiteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptedUnitFloatSiteId);
        indexDecision.Validate();
        unitFloatDecision.Validate();
        if (origin.Phase != RunPhase.Event ||
            origin.World?.Event is not { } originEvent ||
            originEvent.EventId != expectedEventId ||
            originEvent.Stochastic.NativeRng is not { } originStream ||
            string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.Stochastic.NativeRunRng is null ||
            indexDecision.Request.SiteId != scriptedIndexSiteId ||
            unitFloatDecision.Request.SiteId != scriptedUnitFloatSiteId ||
            indexDecision.Request.StreamId != originStream.StreamId ||
            unitFloatDecision.Request.StreamId != originStream.StreamId ||
            unitFloatDecision.Request.Ordinal !=
                indexDecision.Request.Ordinal + 1 ||
            originStream.StreamId != NativeRngV111Contexts.ForEvent(
                origin.RunSeed, nativeEventRngEventId).StreamId)
            throw new InvalidOperationException(
                "Mixed Endless Conveyor replay requires consecutive index and unit-float requests on its current event stream.");

        var selected = new[]
        {
            ControlledStochasticDecisionV111.FromIndex(
                indexDecision with { ProviderMode = "scripted-v111" }),
            ControlledStochasticDecisionV111.FromUnitFloat(
                unitFloatDecision with { ProviderMode = "scripted-v111" })
        };
        IReadOnlyList<NativeEventUnitFloatCaptureV111> captures;
        RunState run;
        using (var probe = NativeEventRandomProbeV111.Begin())
        using (StochasticProviderScopeV111.Enter(
            new SelectiveReplayProvider([],
                new ScriptedRandomIndexProviderV111([])),
            overrideExplicitProviders: true))
        using (StochasticProviderScopeV111.Enter(
            unitFloatProvider: new SelectiveUnitFloatReplayProvider(
                string.Empty, new ScriptedRandomUnitFloatProviderV111([])),
            overrideExplicitProviders: true))
        {
            run = ReplayControlledPrefix(
                origin, selected,
                checkpoint => new PrototypeGameEngine().Step(
                    checkpoint, action).State,
                nativeEventRngEventId, expectedEventId,
                candidate =>
                    (candidate.IndexRequest is { } index &&
                        index.SiteId == scriptedIndexSiteId) ||
                    (candidate.UnitFloatRequest is { } unitFloat &&
                        unitFloat.SiteId == scriptedUnitFloatSiteId));
            captures = probe.UnitFloatCaptures;
        }
        var matching = captures.Where(capture =>
            capture.Request.SiteId == scriptedUnitFloatSiteId).ToArray();
        if (matching.Length != 1 ||
            !ControlledStochasticChanceReplayV111.RequestsMatch(
                matching[0].Request, unitFloatDecision.Request) ||
            matching[0].PreDrawState.World?.Event?.Stochastic.NativeRng is not
                { } preDrawStream ||
            preDrawStream.StreamId != originStream.StreamId ||
            NativeRngV111.ToSnapshot(preDrawStream) !=
                NativeRngV111.ToSnapshot(originStream))
            throw new InvalidDataException(
                "Mixed Endless Conveyor replay did not reach the scripted ticket after its counterfactual index.");
        if (run.World?.Event is not { } eventState ||
            eventState.EventId != expectedEventId ||
            eventState.Stochastic.NativeRng is not { } replayStream ||
            NativeRngV111.ToSnapshot(replayStream) !=
                NativeRngV111.ToSnapshot(originStream))
            throw new InvalidDataException(
                $"Scripted {expectedEventId} mixed resolution advanced its Native event cursor.");
        return run with
        {
            World = run.World! with
            {
                Event = eventState with
                {
                    Stochastic = EventStochasticStateV111.WithProviderMode(
                        eventState.Stochastic, "scripted-v111")
                }
            }
        };
    }

    private sealed class SelectiveReplayProvider(
        string[] scriptedSiteIds,
        ScriptedRandomIndexProviderV111 scripted)
        : IRandomIndexProviderV111
    {
        public string Mode => "selective-event-shuffle-replay-v111";

        public RandomIndexDecisionV111 Resolve(
            RandomIndexRequestV111 request, RngBundle streams)
        {
            if (scriptedSiteIds.Contains(
                    request.SiteId, StringComparer.Ordinal))
                return scripted.Resolve(request, streams);

            var stream = streams.GetStream(request.StreamId);
            IRandomIndexProviderV111 fallback =
                stream.Codec == PrototypeRng.Codec
                    ? new LegacyPrototypeRandomIndexProviderV111()
                    : new NativeRandomIndexProviderV111();
            return fallback.Resolve(request, streams);
        }
    }

    private sealed class SelectiveUnitFloatReplayProvider(
        string scriptedSiteId,
        ScriptedRandomUnitFloatProviderV111 scripted)
        : IRandomUnitFloatProviderV111
    {
        public string Mode => "selective-event-unit-float-replay-v111";

        public RandomUnitFloatDecisionV111 Resolve(
            RandomUnitFloatRequestV111 request, RngBundle streams)
        {
            if (request.SiteId == scriptedSiteId)
                return scripted.Resolve(request, streams);

            var stream = streams.GetStream(request.StreamId);
            IRandomUnitFloatProviderV111 fallback =
                stream.Codec == PrototypeRng.Codec
                    ? throw new InvalidOperationException(
                        "Legacy prototype streams do not support typed unit-float requests.")
                    : new NativeRandomUnitFloatProviderV111();
            return fallback.Resolve(request, streams);
        }
    }
}
