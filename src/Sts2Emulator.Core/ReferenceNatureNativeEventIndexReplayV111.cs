namespace Sts2Emulator.Core;

/// <summary>Replays a typed Native index transcript through its ordinary
/// player action while preserving the relevant Native stream cursor.</summary>
internal static class ReferenceNatureNativeEventIndexReplayV111
{
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
            scriptedSiteId != "relic.war_hammer.elite_upgrade.shuffle_swap_index")
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
            var stream = origin.NativeRunRng?.Streams.SingleOrDefault(item =>
                item.StreamId == unscriptedStreamId);
            if (stream is null ||
                NativeRngV111.ToSnapshot(stream) !=
                    expectedUnscriptedCursorBefore)
                throw new InvalidOperationException(
                    "The unscripted run-stream cursor differs from its replay boundary.");
        }
        RngStreamState? expectedStream = null;
        if (expectedRunNicheCursor is null && preserveEventRng)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(nativeEventRngEventId);
            expectedStream = origin.World?.Event?.NativeEventRng ??
                NativeRngV111Contexts.ForEvent(
                    origin.RunSeed, nativeEventRngEventId);
        }
        else if (!preserveEventRng &&
            origin.World?.Event?.NativeEventRng is not null)
            throw new InvalidOperationException(
                "This replay boundary requires the event RNG context to remain absent.");
        var scripted = new ScriptedRandomIndexProviderV111(decisions);
        IRandomIndexProviderV111 replayProvider = scriptedSiteId is null
            ? scripted
            : new SelectiveReplayProvider(scriptedSiteId, scripted);
        RunState run;
        RandomBoundaryEventV111[] replayIndexEvents = [];
        if (scriptedSiteId is null)
        {
            using var providerScope = StochasticProviderScopeV111.Enter(
                replayProvider, overrideExplicitProviders: true);
            run = new PrototypeGameEngine().Step(
                origin.Fork(), action).State;
        }
        else
        {
            using var shuffleProbe = NativeEventRandomProbeV111.Begin();
            using var boundaryJournal = RandomBoundaryJournalV111.Begin();
            using var providerScope = StochasticProviderScopeV111.Enter(
                replayProvider, overrideExplicitProviders: true);
            run = new PrototypeGameEngine().Step(
                origin.Fork(), action).State;
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
        scripted.AssertComplete();
        if (expectedRunNicheCursor is not null)
        {
            var runNiche = run.NativeRunRng?.Streams.SingleOrDefault(stream =>
                stream.StreamId == "run.niche");
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
                replayEventState.NativeEventRng is not null ||
                replayEventState.NativeEventRngProviderMode is not null)
                throw new InvalidDataException(
                    "Scripted player-stream replay changed the absent Native event RNG context.");
            var expectedRewardsCursor = expectedUnscriptedCursorBefore
                ?? throw new InvalidDataException(
                    "Scripted player-stream replay is missing its pre-draw cursor.");
            var streamId = expectedUnscriptedStreamId
                ?? throw new InvalidDataException(
                    "Scripted player-stream replay is missing its stream identity.");
            var finalStream = run.NativeRunRng?.Streams.SingleOrDefault(item =>
                item.StreamId == streamId);
            var streamEvents = replayIndexEvents.Where(entry =>
                entry.Request.StreamId == streamId).ToArray();
            var nativeEvents = streamEvents.Where(entry =>
                entry.ProviderMode == "native-v111").ToArray();
            var scriptedSwapCount = Math.Max(0,
                (expectedOrderedCandidateIds?.Length ?? 0) - 1);
            if (finalStream is null ||
                unchecked((uint)(NativeRngV111.ToSnapshot(finalStream).Counter -
                    expectedRewardsCursor.Counter)) !=
                    unchecked((uint)expectedUnscriptedDrawCount) ||
                streamEvents.Length !=
                    scriptedSwapCount + expectedUnscriptedDrawCount ||
                nativeEvents.Length != expectedUnscriptedDrawCount ||
                nativeEvents.Where((entry, index) =>
                    entry.Request.SiteId !=
                        "event.war_historian_repy.potion_offer.index_draw" ||
                    entry.Request.OwnerId !=
                        $"event:war_historian_repy:potion_offer:{index}" ||
                    entry.Request.OperationKind != "choose_index" ||
                    entry.Request.SupportCount !=
                        PrototypeContent.PotionPool.Length).Any() ||
                nativeEvents.Length == 2 &&
                    nativeEvents[1].Request.Ordinal !=
                        nativeEvents[0].Request.Ordinal + 1)
                throw new InvalidDataException(
                    "Scripted player-stream replay did not retain the two ordered Native Repy potion draws and their exact cursor advance.");
            return run;
        }
        if (CanonicalJson.Sha256(run.World?.Event?.NativeEventRng) !=
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
                    NativeEventRngProviderMode = "scripted-v111"
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
            origin.NativeRunRng is null)
            throw new InvalidOperationException(
                "Scripted event unit-float replay requires its configured native run and exact site.");

        var indexScript = new ScriptedRandomIndexProviderV111([]);
        var unitFloatScript = new ScriptedRandomUnitFloatProviderV111(
            [decision with { ProviderMode = "scripted-v111" }]);
        using var probe = NativeEventRandomProbeV111.Begin();
        using var providerScope = StochasticProviderScopeV111.Enter(
            new SelectiveReplayProvider(scriptedSiteId, indexScript),
            new SelectiveUnitFloatReplayProvider(
                scriptedSiteId, unitFloatScript),
            overrideExplicitProviders: true);
        var run = new PrototypeGameEngine().Step(
            origin.Fork(), action).State;
        var matchingCaptures = probe.UnitFloatCaptures.Where(capture =>
            capture.Request.SiteId == scriptedSiteId).ToArray();
        var capture = matchingCaptures.Length == 1
            ? matchingCaptures[0] : null;
        var preDrawEvent = capture?.PreDrawState.World?.Event;
        var preDrawStream = preDrawEvent?.NativeEventRng;
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
            preDrawEvent.NativeDishCount == expectedPreDrawDishCount &&
            preDrawEvent.NativeDishId is not null &&
            preDrawEvent.NativeDishId == preDrawEvent.NativeLastDishId;
        if (capture is null || preDrawEvent is null ||
            CanonicalJson.Sha256(capture.Request) !=
                CanonicalJson.Sha256(decision.Request) ||
            capture.PreDrawState.Phase != RunPhase.Event ||
            capture.PreDrawState.World?.Combat is not null ||
            preDrawEvent.EventId != expectedEventId ||
            preDrawEvent.NativeDishRollPending ||
            preDrawEvent.NativeEventPotionSlot is not null ||
            preDrawStream is null ||
            preDrawStream.StreamId != expectedStream.StreamId ||
            !(entryDishBoundary || continuationDishBoundary))
            throw new InvalidDataException(
                "Scripted event unit-float request differs from the replay plan.");
        var expectedPostPrefixCursor = NativeRngV111.ToSnapshot(preDrawStream);
        indexScript.AssertComplete();
        unitFloatScript.AssertComplete();
        if (run.World?.Event?.NativeEventRng is not { } replayStream ||
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
                    NativeEventRngProviderMode = "scripted-v111"
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
            originEvent.NativeEventRng is not { } originStream ||
            string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.NativeRunRng is null ||
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

        var indexScript = new ScriptedRandomIndexProviderV111(
            [indexDecision with { ProviderMode = "scripted-v111" }]);
        var unitFloatScript = new ScriptedRandomUnitFloatProviderV111(
            [unitFloatDecision with { ProviderMode = "scripted-v111" }]);
        IReadOnlyList<NativeEventUnitFloatCaptureV111> captures;
        RunState run;
        using (var probe = NativeEventRandomProbeV111.Begin())
        using (var providerScope = StochasticProviderScopeV111.Enter(
            new SelectiveReplayProvider(scriptedIndexSiteId, indexScript),
            new SelectiveUnitFloatReplayProvider(
                scriptedUnitFloatSiteId, unitFloatScript),
            overrideExplicitProviders: true))
        {
            run = new PrototypeGameEngine().Step(
                origin.Fork(), action).State;
            captures = probe.UnitFloatCaptures;
        }
        var matching = captures.Where(capture =>
            capture.Request.SiteId == scriptedUnitFloatSiteId).ToArray();
        if (matching.Length != 1 ||
            CanonicalJson.Sha256(matching[0].Request) !=
                CanonicalJson.Sha256(unitFloatDecision.Request) ||
            matching[0].PreDrawState.World?.Event?.NativeEventRng is not
                { } preDrawStream ||
            preDrawStream.StreamId != originStream.StreamId ||
            NativeRngV111.ToSnapshot(preDrawStream) !=
                NativeRngV111.ToSnapshot(originStream))
            throw new InvalidDataException(
                "Mixed Endless Conveyor replay did not reach the scripted ticket after its counterfactual index.");
        indexScript.AssertComplete();
        unitFloatScript.AssertComplete();
        if (run.World?.Event is not { } eventState ||
            eventState.EventId != expectedEventId ||
            eventState.NativeEventRng is not { } replayStream ||
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
                    NativeEventRngProviderMode = "scripted-v111"
                }
            }
        };
    }

    private sealed class SelectiveReplayProvider(
        string scriptedSiteId,
        ScriptedRandomIndexProviderV111 scripted)
        : IRandomIndexProviderV111
    {
        public string Mode => "selective-event-shuffle-replay-v111";

        public RandomIndexDecisionV111 Resolve(
            RandomIndexRequestV111 request, RngBundle streams)
        {
            if (request.SiteId == scriptedSiteId)
                return scripted.Resolve(request, streams);

            var stream = streams.Streams.SingleOrDefault(item =>
                item.StreamId == request.StreamId)
                ?? throw new InvalidOperationException(
                    "Replay random request has no matching stream.");
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

            var stream = streams.Streams.SingleOrDefault(item =>
                item.StreamId == request.StreamId)
                ?? throw new InvalidOperationException(
                    "Replay unit-float request has no matching stream.");
            IRandomUnitFloatProviderV111 fallback =
                stream.Codec == PrototypeRng.Codec
                    ? throw new InvalidOperationException(
                        "Legacy prototype streams do not support typed unit-float requests.")
                    : new NativeRandomUnitFloatProviderV111();
            return fallback.Resolve(request, streams);
        }
    }
}
