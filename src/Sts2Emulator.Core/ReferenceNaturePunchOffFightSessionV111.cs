using System.Text.Json;

namespace Sts2Emulator.Core;

internal sealed class SelectiveScriptedIndexReplayProviderV111(
    ScriptedRandomIndexProviderV111 script, string[] siteIds)
    : IRandomIndexProviderV111
{
    private readonly HashSet<string> _siteIds =
        new(siteIds, StringComparer.Ordinal);

    public string Mode => "selective-scripted-index-replay-v1";

    public RandomIndexDecisionV111 Resolve(
        RandomIndexRequestV111 request, RngBundle streams)
    {
        if (_siteIds.Contains(request.SiteId))
            return script.Resolve(request, streams);
        var stream = streams.GetStream(request.StreamId);
        IRandomIndexProviderV111 fallback =
            stream.Codec == PrototypeRng.Codec
                ? new LegacyPrototypeRandomIndexProviderV111()
                : new NativeRandomIndexProviderV111();
        return fallback.Resolve(request, streams);
    }
}

internal static class NativePunchOffControlledIndexReplayV111
{
    public static ControlledStochasticContinuationResultV111 Capture(
        RunState checkpoint, RandomIndexDecisionV111[] decisions,
        Func<RunState, RunState> mechanics, string[] siteIds)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(mechanics);
        ArgumentNullException.ThrowIfNull(siteIds);
        var allowedSites = new HashSet<string>(siteIds,
            StringComparer.Ordinal);
        var selected = decisions.Select(decision =>
            ControlledStochasticDecisionV111.FromIndex(decision)).ToArray();
        using var fallbackScope = StochasticProviderScopeV111.Enter(
            new SelectiveScriptedIndexReplayProviderV111(
                new ScriptedRandomIndexProviderV111([]), []),
            overrideExplicitProviders: true);
        return ControlledChanceContinuationExecutorV111.CaptureStochastic(
            checkpoint,
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            selected, mechanics,
            requestFilter: candidate =>
                candidate.IndexRequest is { } request &&
                allowedSites.Contains(request.SiteId));
    }
}

internal sealed record NativePunchOffFightPlanV111(
    RandomIndexRequestV111[] Requests,
    int[] ConstructHpBeforeOffsets,
    NativeRngSnapshot[] MonsterCursorsBefore,
    NativeRngSnapshot[] RunCursorsAfterProbe,
    NativeRngSnapshot? EventCursorBefore,
    string ConditioningFingerprint);

internal static class NativePunchOffFightPlanBuilderV111
{
    public static NativePunchOffFightPlanV111 Build(
        RunState origin, string actionId, int ordinal, int[] priorIndices)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentNullException.ThrowIfNull(priorIndices);
        if (ordinal is < 0 or >= 2 || priorIndices.Length != ordinal ||
            priorIndices.Any(index => index is < 0 or >= 8))
            throw new InvalidOperationException(
                "Punch Off fight queries require the complete preceding HP-offset prefix.");
        if (string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.Stochastic.NativeRunRng is null || origin.Phase != RunPhase.Event ||
            origin.World is not { Act: 1, Event: { } evt, Combat: null } world ||
            evt.EventId != PrototypeNativePunchOff.EventId ||
            evt.NativePageIndex != 1 || evt.ChosenChoiceId is not null ||
            evt.Stochastic.ProviderMode is not null ||
            world.ActiveRoom != PrototypeRoomType.Event ||
            world.Reward is not null ||
            origin.Stochastic.ControlledEventRewardChoice is not null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world))
            throw new InvalidOperationException(
                "Punch Off fight laws require its live configured Fight page.");

        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(origin).SingleOrDefault(candidate =>
            PrototypeAiEnvironment.StableActionId(candidate) == actionId)
            ?? throw new InvalidOperationException(
                "Punch Off fight action is not legal in the current event state.");
        if (action.Kind != "event_choice" ||
            action.ReadPayload<EventChoicePayload>().ChoiceId != "fight")
            throw new InvalidOperationException(
                "Punch Off fight law requires the Fight choice.");

        var eventCursorBefore = evt.Stochastic.NativeRng is { } eventRng
            ? NativeRngV111.ToSnapshot(eventRng) : null;
        using var journal = RandomBoundaryJournalV111.Begin();
        using var providerScope = StochasticProviderScopeV111.Enter(
            new NativeRandomIndexProviderV111(),
            overrideExplicitProviders: false);
        var projected = engine.Step(origin.Fork(), action).State;
        var projectedEvent = projected.World?.Event;
        var projectedCombat = projected.World?.Combat;
        var entries = journal.Entries.Where(entry =>
            entry.Request.SiteId ==
                PrototypeNativePunchOff.ConstructHpOffsetSiteId).ToArray();
        if (projected.Phase != RunPhase.Combat ||
            projected.World is not { Act: 1, ActiveRoom: PrototypeRoomType.Combat } ||
            projectedEvent is not
                { EventId: PrototypeNativePunchOff.EventId, NativePageIndex: 2 } ||
            projectedCombat is not
                { Enemies.Length: 2, Stochastic.NativeRng: not null } ||
            entries.Length != 2 ||
            entries.Any(entry => entry.ProviderMode != "native-v111" ||
                entry.Request.OperationKind != "bounded_integer" ||
                entry.Request.BoundedCount != 8 || entry.Request.Ordinal != 0 ||
                !entry.Request.StreamId.StartsWith(
                    "context.monster.", StringComparison.Ordinal) ||
                entry.NativeCursorBefore is null ||
                entry.NativeCursorAfter is null) ||
            entries.Select(entry => entry.Request.OwnerId)
                .Distinct(StringComparer.Ordinal).Count() != 2 ||
            eventCursorBefore != (projectedEvent.Stochastic.NativeRng is { } afterEvent
                ? NativeRngV111.ToSnapshot(afterEvent) : null))
            throw new InvalidOperationException(
                "Punch Off Fight must produce exactly two ordered monster-local 2–9 HP requests and preserve its event cursor.");

        var enemyContexts = projectedCombat.Stochastic.NativeRng!.Formation.MonsterContexts;
        if (enemyContexts.Length != 2 ||
            entries.Where((entry, index) =>
                entry.Request.OwnerId !=
                    "event:punch_off:construct_combat_id:" +
                        projectedCombat.Enemies[index].InstanceId ||
                entry.Request.StreamId != enemyContexts[index].Rng.StreamId ||
                entry.Request.OwnerId.EndsWith(
                    ":" + enemyContexts[index].CombatId,
                    StringComparison.Ordinal) is false ||
                entry.NativeCursorAfter !=
                    NativeRngV111.ToSnapshot(enemyContexts[index].Rng)).Any())
            throw new InvalidOperationException(
                "Punch Off HP offsets are not owned by the matching construct CombatIds.");

        var requests = entries.Select(entry => entry.Request with
        {
            OrderedCandidates = (int[])entry.Request.OrderedCandidates.Clone()
        }).ToArray();
        var constructHpBeforeOffsets = projectedCombat.Enemies
            .Select((enemy, index) => checked(
                enemy.Hp + 2 + entries[index].SelectedIndex)).ToArray();
        var hpRange = PrototypeContent.Enemy(
            "proto.enemy.punch_construct").HpRangeAt(1, origin.Ascension);
        if (constructHpBeforeOffsets.Any(hp =>
                hp < hpRange.Min || hp > hpRange.Max))
            throw new InvalidOperationException(
                "Punch Off probe produced an impossible pre-offset construct HP.");
        var monsterCursorsBefore = entries.Select(entry =>
            entry.NativeCursorBefore!).ToArray();
        var runCursorsAfterProbe = projectedCombat.Stochastic.NativeRng.RunStreams.Streams
            .OrderBy(stream => stream.StreamId, StringComparer.Ordinal)
            .Select(NativeRngV111.ToSnapshot).ToArray();
        var fingerprint = CanonicalJson.Sha256(new
        {
            boundary = "punch_off.native_fight_entry",
            origin = CanonicalJson.Sha256(origin),
            actionId,
            requestOrder = requests,
            constructHpBeforeOffsets,
            monsterCursorsBefore,
            runCursorsAfterProbe,
            eventCursorBefore,
            probabilityModel =
                "uniform-index-for-each-2-to-9-offset-under-unverified-source-law"
        });
        return new(requests, constructHpBeforeOffsets, monsterCursorsBefore,
            runCursorsAfterProbe, eventCursorBefore, fingerprint);
    }
}
