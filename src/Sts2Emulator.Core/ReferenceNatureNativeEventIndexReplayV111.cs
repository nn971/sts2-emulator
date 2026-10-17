namespace Sts2Emulator.Core;

/// <summary>Replays a typed Native event index transcript through its
/// ordinary player action while preserving the event stream cursor.</summary>
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
        string[]? expectedOrderedCandidateIds = null)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEventRngEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        var expectedStream = origin.World?.Event?.NativeEventRng ??
            NativeRngV111Contexts.ForEvent(
                origin.RunSeed, nativeEventRngEventId);
        var scripted = new ScriptedRandomIndexProviderV111(decisions);
        IRandomIndexProviderV111 replayProvider = scriptedSiteId is null
            ? scripted
            : new SelectiveReplayProvider(scriptedSiteId, scripted);
        RunState run;
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
        }
        scripted.AssertComplete();
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
        string expectedEventId, string scriptedSiteId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEventRngEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptedSiteId);
        decision.Validate();
        if (decision.Request.SiteId != scriptedSiteId ||
            string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.NativeRunRng is null)
            throw new InvalidOperationException(
                "Scripted event unit-float replay requires its configured native run and exact site.");

        var expectedStream = NativeRngV111Contexts.ForEvent(
            origin.RunSeed, nativeEventRngEventId);
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
        if (matchingCaptures.Length != 1 ||
            CanonicalJson.Sha256(matchingCaptures[0].Request) !=
                CanonicalJson.Sha256(decision.Request) ||
            matchingCaptures[0].PreDrawState.World?.Event is not { } preDrawEvent ||
            matchingCaptures[0].PreDrawState.Phase != RunPhase.Event ||
            matchingCaptures[0].PreDrawState.World?.Combat is not null ||
            preDrawEvent.EventId != expectedEventId ||
            preDrawEvent.NativePageIndex != 0 ||
            preDrawEvent.NativeDishCount != 0 ||
            preDrawEvent.NativeDishId is not null ||
            preDrawEvent.NativeLastDishId is not null ||
            preDrawEvent.NativeEventPotionSlot is not null ||
            preDrawEvent.NativeEventRng is not { } preDrawStream ||
            NativeRngV111.ToSnapshot(preDrawStream) !=
                NativeRngV111.ToSnapshot(expectedStream))
            throw new InvalidDataException(
                "Scripted event unit-float request differs from the replay plan.");
        indexScript.AssertComplete();
        unitFloatScript.AssertComplete();
        if (CanonicalJson.Sha256(run.World?.Event?.NativeEventRng) !=
            CanonicalJson.Sha256(expectedStream))
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
