using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureNativeEventRelicDecisionV111(
    string LawId, string EventId, string OutcomeId, int OutcomeIndex)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LawId) ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(OutcomeId) || OutcomeIndex < 0)
            throw new InvalidDataException(
                "Invalid ReferenceNature Native event relic decision.");
    }
}

public sealed record ReferenceNatureNativeEventRelicSessionSnapshotV111(
    string SchemaId, string OriginSnapshotJson, GameAction Action,
    string RunSnapshotJson, ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ControlledStochasticDecisionV111[] ProviderDecisions,
    ReferenceNatureNativeEventRelicDecisionV111[] DecisionTranscript,
    ControlledStochasticRequestV111? PendingRequest,
    string[]? PendingCandidateLabels);

/// <summary>
/// Atomic configured Native event relic choice. Direct random-relic effects
/// and relic pulls triggered by Toy Box share one typed ReferenceNature
/// transcript and publish the run cursor only after all requests resolve.
/// </summary>
public sealed record ReferenceNatureNativeEventRelicSessionV111(
    string SchemaId, RunState Origin, GameAction Action, RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ControlledStochasticDecisionV111[] ProviderDecisions,
    ReferenceNatureNativeEventRelicDecisionV111[] DecisionTranscript,
    ControlledStochasticRequestV111? PendingRequest,
    string[]? PendingCandidateLabels)
{
    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-native-event-relic-session-v1";
    private const int ToyBoxDrawCount = 5;
    private const string RelicRaritySiteId = "relic_grab_bag.rarity.unit_float";
    private const string RelicModelSiteId =
        PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId;

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool Complete => PendingRequest is null;

    [JsonIgnore]
    public int NextOrdinal => ProviderDecisions.Length;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => PendingRequest is null ? null :
        QueryLaw(Origin, Action, PendingRequest, PendingCandidateLabels,
            ProviderDecisions);

    public static string[] SessionSiteIds =>
        [RelicRaritySiteId, RelicModelSiteId];

    public static ReferenceNatureNativeEventRelicSessionV111 Begin(
        RunState source, GameAction action, ulong natureSeed) => Begin(
            source, action, ReferenceNatureRunCursorV111.Begin(
                source, natureSeed, "Native event relic"));

    public static ReferenceNatureNativeEventRelicSessionV111 Begin(
        RunState source, GameAction action) => Begin(
            source, action, ReferenceNatureRunCursorV111.Begin(
                source, null, "Native event relic"));

    private static ReferenceNatureNativeEventRelicSessionV111 Begin(
        RunState source, GameAction action,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ValidateOpeningBoundary(source, action);
        ReferenceNatureRunCursorV111.ValidateInitial(
            source, initialNatureState, "Native event relic");

        var origin = ReferenceNatureRunProjectionV111
            .CreateNatureReplayOrigin(source, createRelicBagWhenMissing: true);
        var replay = Replay(origin, action, []);
        if (replay.Continuation is null)
            throw new NotSupportedException(
                "Native event choice did not reach a supported relic draw.");
        var session = new ReferenceNatureNativeEventRelicSessionV111(
            CurrentSchemaId, origin.Fork(), CloneAction(action), origin.Fork(),
            initialNatureState.Fork(), initialNatureState.Fork(), [], [],
            replay.Continuation.PendingRequest.Fork(),
            CloneLabels(replay.Continuation.PendingCandidateLabels));
        session.Validate();
        return session;
    }

    public ReferenceNatureNativeEventRelicSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Origin = Origin.Fork(),
            Action = CloneAction(Action),
            Run = Run.Fork(),
            InitialNatureState = InitialNatureState.Fork(),
            NatureState = NatureState.Fork(),
            ProviderDecisions = ProviderDecisions
                .Select(decision => decision.Fork()).ToArray(),
            DecisionTranscript = (ReferenceNatureNativeEventRelicDecisionV111[])
                DecisionTranscript.Clone(),
            PendingRequest = PendingRequest?.Fork(),
            PendingCandidateLabels = CloneLabels(PendingCandidateLabels)
        };
    }

    public ReferenceNatureNativeEventRelicSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature Native event relic session is already complete.");

        var law = QueryLaw(Origin, Action, PendingRequest!,
            PendingCandidateLabels, ProviderDecisions);
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var providerDecision = ReferenceNatureChanceLawDecisionV111.FromSelection(
            law, PendingRequest!, selection, Origin, PendingCandidateLabels);
        var continuation = new ControlledStochasticContinuationStateV111(
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            ProviderDecisions, PendingRequest!, PendingCandidateLabels);
        var replay = ControlledChanceContinuationExecutorV111.ResolveStochastic(
            CreateNatureReplayOrigin(Origin), continuation, providerDecision,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, CloneAction(Action)).State,
            RequireSessionRequest);
        var providerDecisions = replay.Continuation?.Decisions ??
            ProviderDecisions.Append(providerDecision.Fork()).ToArray();
        var transcript = DecisionTranscript.Append(
            new ReferenceNatureNativeEventRelicDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var run = replay.Run is { } completed
            ? ReferenceNatureRunCursorV111.Commit(
                completed, selection.NextState)
            : Origin.Fork();
        var next = this with
        {
            Run = run,
            NatureState = selection.NextState,
            ProviderDecisions = providerDecisions,
            DecisionTranscript = transcript,
            PendingRequest = replay.Continuation?.PendingRequest.Fork(),
            PendingCandidateLabels = CloneLabels(
                replay.Continuation?.PendingCandidateLabels)
        };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Origin is null || Action is null ||
            Run is null || InitialNatureState is null || NatureState is null ||
            ProviderDecisions is null || DecisionTranscript is null ||
            ProviderDecisions.Length != DecisionTranscript.Length ||
            DecisionTranscript.Length >
                ExpectedDrawCount(Origin, Action) * 2 ||
            string.IsNullOrWhiteSpace(Action.Kind))
            throw new InvalidDataException(
                "Invalid ReferenceNature Native event relic session envelope.");
        if (PendingRequest is { } pendingRequest)
        {
            pendingRequest.Validate();
            if (PendingCandidateLabels is { } pendingLabels &&
                (pendingRequest.IndexRequest is null ||
                 pendingLabels.Length != pendingRequest.IndexRequest.SupportCount ||
                 pendingLabels.Any(string.IsNullOrWhiteSpace)))
                throw new InvalidDataException(
                    "Invalid Native event relic pending candidate labels.");
        }
        else if (PendingCandidateLabels is not null)
        {
            throw new InvalidDataException(
                "Completed Native event relic session has pending candidate labels.");
        }

        ValidateOpeningBoundary(Origin, Action);
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
            Origin, "Native event relic session origin");
        ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
            Run, "Native event relic session replay");
        InitialNatureState.Validate();
        NatureState.Validate();
        ReferenceNatureRunCursorV111.ValidateInitial(
            Origin, InitialNatureState, "Native event relic");

        var expectedNatureState = InitialNatureState.Fork();
        for (var index = 0; index < ProviderDecisions.Length; index++)
        {
            var prefix = ProviderDecisions.Take(index).ToArray();
            var replayAtBoundary = Replay(Origin, Action, prefix);
            var request = replayAtBoundary.Continuation?.PendingRequest ??
                throw new InvalidDataException(
                    "Native event relic replay completed before its transcript.");
            var labels = replayAtBoundary.Continuation!.PendingCandidateLabels;
            var savedDecision = ProviderDecisions[index];
            savedDecision.Validate();
            if (!SameRequest(request, AsRequest(savedDecision)) ||
                !SameLabels(labels, savedDecision.CandidateLabels))
                throw new InvalidDataException(
                    "Native event relic provider transcript changed its request.");

            var law = QueryLaw(Origin, Action, request, labels, prefix);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            var expectedTranscript = new ReferenceNatureNativeEventRelicDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            if (CanonicalJson.Sha256(expectedTranscript) !=
                    CanonicalJson.Sha256(DecisionTranscript[index]) ||
                CanonicalJson.Sha256(ReferenceNatureChanceLawDecisionV111
                    .FromSelection(law, request, selection, Origin, labels)) !=
                CanonicalJson.Sha256(savedDecision))
                throw new InvalidDataException(
                    "Native event relic law decisions do not replay.");
            expectedNatureState = selection.NextState;
        }

        var replay = Replay(Origin, Action, ProviderDecisions);
        if ((replay.Continuation is null) != Complete ||
            !SameRequest(replay.Continuation?.PendingRequest, PendingRequest) ||
            !SameLabels(replay.Continuation?.PendingCandidateLabels,
                PendingCandidateLabels) ||
            CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(NatureState) ||
            Complete && ProviderDecisions.Length !=
                ExpectedDrawCount(Origin, Action) * 2)
            throw new InvalidDataException(
                "Native event relic continuation differs from its replay.");

        var expectedRun = replay.Run is { } completed
            ? ReferenceNatureRunCursorV111.Commit(completed, expectedNatureState)
            : Origin.Fork();
        if (CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run) ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "Native event relic run differs from its atomic Nature cursor.");
        foreach (var decision in DecisionTranscript)
            decision.Validate();
    }

    public static string Save(
        ReferenceNatureNativeEventRelicSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        return JsonSerializer.Serialize(
            new ReferenceNatureNativeEventRelicSessionSnapshotV111(
                CurrentSchemaId, RunSnapshot.Save(session.Origin),
                CloneAction(session.Action), RunSnapshot.Save(session.Run),
                session.InitialNatureState, session.NatureState,
                session.ProviderDecisions.Select(
                    decision => decision.Fork()).ToArray(),
                (ReferenceNatureNativeEventRelicDecisionV111[])
                    session.DecisionTranscript.Clone(),
                session.PendingRequest?.Fork(),
                CloneLabels(session.PendingCandidateLabels)));
    }

    public static ReferenceNatureNativeEventRelicSessionV111 Restore(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureNativeEventRelicSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature Native event relic snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            snapshot.Action is null ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null || snapshot.ProviderDecisions is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Native event relic snapshot.");
        var session = new ReferenceNatureNativeEventRelicSessionV111(
            CurrentSchemaId, RunSnapshot.Load(snapshot.OriginSnapshotJson),
            CloneAction(snapshot.Action), RunSnapshot.Load(snapshot.RunSnapshotJson),
            snapshot.InitialNatureState.Fork(), snapshot.NatureState.Fork(),
            snapshot.ProviderDecisions.Select(decision => decision.Fork()).ToArray(),
            (ReferenceNatureNativeEventRelicDecisionV111[])
                snapshot.DecisionTranscript.Clone(),
            snapshot.PendingRequest?.Fork(),
            CloneLabels(snapshot.PendingCandidateLabels));
        session.Validate();
        return session;
    }

    private static ChanceLawV111 QueryLaw(
        RunState source, GameAction action,
        ControlledStochasticRequestV111 request, string[]? candidateLabels,
        ControlledStochasticDecisionV111[] prefix)
    {
        request.Validate();
        if (request.UnitFloatRequest is { } floatRequest &&
            floatRequest.SiteId == RelicRaritySiteId)
        {
            if (floatRequest.StreamId != NativePlayerRngV111.RewardsStreamId ||
                floatRequest.OwnerId != PrototypeNativeRelicGrabBag
                    .NativeTreasureRarityOwnerId || candidateLabels is not null)
                throw new InvalidDataException(
                    "Native event relic rarity request differs from the declared draw mechanics.");
            return ChanceLawEngineV111.QueryNativeRelicRarity(floatRequest);
        }

        if (request.IndexRequest is { } indexRequest &&
            indexRequest.SiteId == RelicModelSiteId)
        {
            if (source.World is not { } world || candidateLabels is null ||
                indexRequest.StreamId != NativePlayerRngV111.RewardsStreamId ||
                indexRequest.OperationKind != "choose_index" ||
                indexRequest.SupportCount != candidateLabels.Length ||
                !indexRequest.OrderedCandidates.SequenceEqual(
                    Enumerable.Range(0, candidateLabels.Length)) ||
                !candidateLabels.SequenceEqual(candidateLabels.OrderBy(
                    id => id, StringComparer.Ordinal), StringComparer.Ordinal))
                throw new InvalidDataException(
                    "Native event relic identity request is malformed.");

            var rarity = ParseRarity(indexRequest.OwnerId);
            var bag = BagAfterPrefix(source, prefix);
            var plan = PrototypeNativeRelicGrabBag.PlanPull(
                bag, rarity, merchant: false, source.Player);
            var expectedCandidates = plan.CandidateIds.OrderBy(
                id => id, StringComparer.Ordinal).ToArray();
            var expectedOwner = PrototypeNativeRelicGrabBag.TreasureModelOwnerId(
                world, rarity, expectedCandidates);
            if (plan.SelectedTier is null ||
                !candidateLabels.SequenceEqual(expectedCandidates,
                    StringComparer.Ordinal) || indexRequest.OwnerId != expectedOwner)
                throw new InvalidDataException(
                    "Native event relic identities differ from the canonical remaining relic bag.");

            var eventId = source.World?.Event?.EventId;
            var legacyToyBoxBoundary = eventId ==
                    NativeAncientRewardsV111.TezcataraId &&
                EventChoiceId(action) is { } choiceId &&
                HasToyBoxEffect(eventId, choiceId);
            var lawEventId = CanonicalJson.Sha256(new
            {
                boundary = legacyToyBoxBoundary
                    ? "native-event-toy-box-relic"
                    : "native-event-relic",
                eventId = legacyToyBoxBoundary
                    ? NativeAncientRewardsV111.TezcataraId
                    : eventId,
                choiceId = EventChoiceId(action),
                request = indexRequest
            });
            return ChanceLawEngineV111.QueryRandomIndex(
                lawEventId, indexRequest);
        }

        throw new NotSupportedException(
            $"Native event relic replay does not support stochastic site '{request.SiteId}'.");
    }

    private static PrototypeRelicBagState BagAfterPrefix(
        RunState source, ControlledStochasticDecisionV111[] prefix)
    {
        var bag = source.World?.RelicBag ?? throw new InvalidDataException(
            "Native event relic replay lost its canonical relic bag.");
        foreach (var decision in prefix.Where(decision =>
            decision.IndexDecision?.Request.SiteId == RelicModelSiteId))
        {
            var indexDecision = decision.IndexDecision!;
            var labels = decision.CandidateLabels ?? throw new InvalidDataException(
                "Native event relic prefix lost its selected identity.");
            var rarity = ParseRarity(indexDecision.Request.OwnerId);
            if (indexDecision.SelectedIndex >= labels.Length)
                throw new InvalidDataException(
                    "Native event relic prefix selected outside its candidate labels.");
            var plan = PrototypeNativeRelicGrabBag.PlanPull(
                bag, rarity, merchant: false, source.Player);
            var expected = plan.CandidateIds.OrderBy(
                id => id, StringComparer.Ordinal).ToArray();
            if (plan.SelectedTier is null ||
                !labels.SequenceEqual(expected, StringComparer.Ordinal) ||
                indexDecision.Request.OwnerId !=
                    PrototypeNativeRelicGrabBag.TreasureModelOwnerId(
                        source.World!, rarity, expected))
                throw new InvalidDataException(
                    "Native event relic prefix differs from its canonical bag draw.");
            (bag, _) = PrototypeNativeRelicGrabBag.PullSelected(
                bag, plan with { CandidateIds = expected }, merchant: false,
                source.Player, labels[indexDecision.SelectedIndex]);
        }
        return bag;
    }

    private static PrototypeRelicRarity ParseRarity(string ownerId)
    {
        var start = ownerId.LastIndexOf(":rarity:", StringComparison.Ordinal);
        var end = ownerId.IndexOf(":pool:",
            start < 0 ? 0 : start + ":rarity:".Length,
            StringComparison.Ordinal);
        if (start < 0 || end <= start + ":rarity:".Length ||
            !Enum.TryParse<PrototypeRelicRarity>(
                ownerId[(start + ":rarity:".Length)..end], false,
                out var rarity) || !Enum.IsDefined(rarity))
            throw new InvalidDataException(
                "Native event relic request owner has a malformed rarity.");
        return rarity;
    }

    private static ControlledStochasticContinuationResultV111 Replay(
        RunState source, GameAction action,
        ControlledStochasticDecisionV111[] decisions) =>
        ControlledChanceContinuationExecutorV111.CaptureStochastic(
            CreateNatureReplayOrigin(source),
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            decisions,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, CloneAction(action)).State,
            RequireSessionRequest);

    private static RunState CreateNatureReplayOrigin(RunState source) =>
        ReferenceNatureRunProjectionV111.CreateNatureReplayOrigin(
            source, createRelicBagWhenMissing: true);

    private static bool IsSessionRequest(
        ControlledStochasticRequestV111 request) =>
        request.SiteId is RelicRaritySiteId or RelicModelSiteId;

    private static bool RequireSessionRequest(
        ControlledStochasticRequestV111 request)
    {
        if (IsSessionRequest(request))
            return true;
        throw new NotSupportedException(
            $"Native event relic replay does not support stochastic site '{request.SiteId}'.");
    }

    private static void ValidateOpeningBoundary(
        RunState source, GameAction action)
    {
        var world = source.World;
        var eventState = world?.Event;
        var choiceId = action.Kind == "event_choice"
            ? EventChoiceId(action) : null;
        if (source.Phase != RunPhase.Event || world is null ||
            eventState is null ||
            eventState.ControlledChoice is not null ||
            eventState.ChosenChoiceId is not null ||
            source.NativeRunRng is null ||
            source.ControlledRunActionChoice is not null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            choiceId is null ||
            !PrototypeGameEngine.IsControlledEventChoice(
                eventState.EventId, choiceId) ||
            ExpectedDrawCount(source, action) <= 0 ||
            HasUnsupportedFixedRelicEffect(eventState.EventId, choiceId) ||
            !new PrototypeGameEngine().GetLegalActions(source).Any(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) ==
                PrototypeAiEnvironment.StableActionId(action)))
            throw new InvalidOperationException(
                "Native event relic session requires a current legal configured Native event choice with supported relic draws.");
        PrototypeStateInvariants.Validate(source);
    }

    private static int ExpectedDrawCount(RunState source, GameAction action)
    {
        var eventState = source.World?.Event;
        var choiceId = EventChoiceId(action);
        if (eventState is null || choiceId is null ||
            !PrototypeContent.Events.TryGetValue(
                eventState.EventId, out var definition))
            return 0;
        var effects = definition.Choices.FirstOrDefault(choice =>
            StringComparer.Ordinal.Equals(choice.Id, choiceId))?.Effects;
        if (effects is null)
            return 0;
        var randomRelicDraws = effects.Count(effect =>
            effect.Kind == PrototypeRunEffectKind.GainRandomRelic);
        var toyBoxAcquisitions = effects.Count(effect =>
            effect.Kind == PrototypeRunEffectKind.GainRelic &&
            StringComparer.Ordinal.Equals(
                effect.RelicId, NativeAncientRewardsV111.ToyBoxId));
        var existingReward = source.World?.Reward;
        if (existingReward?.ExtraRelicWaxFlags is { Length: 5 } flags &&
            flags.All(flag => flag) &&
            existingReward.SourceRoom.StartsWith(
                "Event:", StringComparison.Ordinal))
            toyBoxAcquisitions = 0;
        return randomRelicDraws + toyBoxAcquisitions * ToyBoxDrawCount;
    }

    private static bool HasUnsupportedFixedRelicEffect(
        string eventId, string choiceId)
    {
        if (!PrototypeContent.Events.TryGetValue(eventId, out var definition))
            return true;
        var effects = definition.Choices.FirstOrDefault(choice =>
            StringComparer.Ordinal.Equals(choice.Id, choiceId))?.Effects;
        return effects is null || effects.Any(effect =>
            effect.Kind == PrototypeRunEffectKind.GainRelic &&
            !StringComparer.Ordinal.Equals(
                effect.RelicId, NativeAncientRewardsV111.ToyBoxId));
    }

    private static bool HasToyBoxEffect(string eventId, string choiceId) =>
        PrototypeContent.Events.TryGetValue(eventId, out var definition) &&
        definition.Choices.FirstOrDefault(choice =>
            StringComparer.Ordinal.Equals(choice.Id, choiceId))?.Effects.Any(
                effect => effect.Kind == PrototypeRunEffectKind.GainRelic &&
                    StringComparer.Ordinal.Equals(
                        effect.RelicId, NativeAncientRewardsV111.ToyBoxId)) == true;

    private static string? EventChoiceId(GameAction action) =>
        action.Payload.Deserialize<EventChoicePayload>()?.ChoiceId;

    private static bool SameRequest(
        ControlledStochasticRequestV111? left,
        ControlledStochasticRequestV111? right) =>
        left is null ? right is null : right is not null &&
        CanonicalJson.Sha256(left) == CanonicalJson.Sha256(right);

    private static bool SameLabels(string[]? left, string[]? right) =>
        left is null ? right is null : right is not null &&
        left.SequenceEqual(right, StringComparer.Ordinal);

    private static ControlledStochasticRequestV111 AsRequest(
        ControlledStochasticDecisionV111 decision) => decision switch
    {
        { UnitFloatDecision: { } unit } =>
            ControlledStochasticRequestV111.FromUnitFloat(unit.Request),
        { IndexDecision: { } index } =>
            ControlledStochasticRequestV111.FromIndex(index.Request),
        _ => throw new InvalidDataException(
            "Native event relic provider decision has an unsupported type.")
    };

    private static string[]? CloneLabels(string[]? labels) =>
        labels is null ? null : (string[])labels.Clone();

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };
}
