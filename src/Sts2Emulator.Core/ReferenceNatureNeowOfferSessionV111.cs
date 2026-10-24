using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

internal sealed record NeowOfferDecisionStepV111(
    RandomIndexRequestV111 Request,
    string[] OutcomeIds,
    string ConditioningFingerprint,
    int? SelectedIndex);

internal sealed record NeowOfferDecisionPlanV111(
    NeowOfferDecisionStepV111[] Steps,
    NeowOfferDecisionStepV111? PendingStep)
{
    public bool Complete => PendingStep is null;
}

/// <summary>Builds the existing Neow option-generator request sequence from
/// an index prefix, without consulting any generated or selected offer.</summary>
internal static class NeowOfferDecisionPlanBuilderV111
{
    public static NeowOfferDecisionPlanV111 Build(
        string seed, int ascension, int[] priorIndices)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        ArgumentNullException.ThrowIfNull(priorIndices);
        if (ascension < 0)
            throw new ArgumentOutOfRangeException(nameof(ascension));

        var streamId = NativeRngV111Contexts.ForEvent(
            seed, PrototypeNativeOvergrowthEvents.NeowRngEventEntry).StreamId;
        var completed = new List<NeowOfferDecisionStepV111>();
        NeowOfferDecisionStepV111? pending = null;

        int? Consume(
            string siteId,
            string ownerId,
            string[] outcomeIds,
            string conditioningContext,
            int? boundedCount = null,
            int requestOrdinal = 0)
        {
            if (!NeowOfferSessionSiteIds.Contains(
                    siteId, StringComparer.Ordinal))
                throw new NotSupportedException(
                    $"Neow offer decision does not support site '{siteId}'.");
            RandomIndexRequestV111 request = boundedCount is { } count
                ? new RandomIndexRequestV111(
                    siteId, streamId, ownerId, requestOrdinal, [],
                    OperationKind: "bounded_integer", BoundedCount: count)
                : new RandomIndexRequestV111(
                    siteId, streamId, ownerId, requestOrdinal,
                    Enumerable.Range(0, outcomeIds.Length).ToArray());
            var conditioningFingerprint = CanonicalJson.Sha256(new
            {
                boundary = "act1.neow.initial_options",
                ascension,
                priorIndices = priorIndices.Take(completed.Count).ToArray(),
                requestFingerprint = CanonicalJson.Sha256(request),
                conditioningContext,
                policy = "neow-sequential-offer-generator-v1"
            });
            if (completed.Count == priorIndices.Length)
            {
                pending = new NeowOfferDecisionStepV111(
                    request, outcomeIds, conditioningFingerprint, null);
                return null;
            }

            int selected = priorIndices[completed.Count];
            if (selected < 0 || selected >= request.SupportCount)
                throw new ArgumentOutOfRangeException(nameof(priorIndices),
                    $"Neow outcome {completed.Count} is outside its saved support.");
            completed.Add(new NeowOfferDecisionStepV111(
                request, outcomeIds, conditioningFingerprint, selected));
            return selected;
        }

        string ChoiceId(string name) => "take_" +
            PrototypeNativeOvergrowthEvents.NeowRelicId(name);

        var cursedOutcomes = PrototypeNativeOvergrowthEvents
            .NeowCursedRelicNames.Select(ChoiceId).ToArray();
        int? cursedIndex = Consume(
            PrototypeNativeOvergrowthEvents.NeowCursedRelicSiteId,
            "run:act:1:neow:cursed_relic", cursedOutcomes,
            "ordered_cursed_relic_names");
        if (pending is not null)
            return new(completed.ToArray(), pending);
        string cursed = PrototypeNativeOvergrowthEvents
            .NeowCursedRelicNames[cursedIndex!.Value];

        var positives = PrototypeNativeOvergrowthEvents
            .NeowPositiveRelicNames.ToList();
        switch (cursed)
        {
            case "CursedPearl": positives.Remove("GoldenPearl"); break;
            case "HeftyTablet": positives.Remove("ArcaneScroll"); break;
            case "LeafyPoultice": positives.Remove("NewLeaf"); break;
            case "PrecariousShears": positives.Remove("PreciseScissors"); break;
            case "NeowsSacrifice":
                positives.Remove("PhialHolster");
                positives.Remove("LostCoffer");
                break;
        }

        if (cursed != "LargeCapsule")
        {
            int? lavaIndex = Consume(
                PrototypeNativeOvergrowthEvents.NeowLavaRockPairSiteId,
                "run:act:1:neow:lava_rock_pair",
                [ChoiceId("LavaRock"), ChoiceId("SmallCapsule")],
                "cursed_relic:" + cursed);
            if (pending is not null)
                return new(completed.ToArray(), pending);
            positives.Add(lavaIndex == 0 ? "LavaRock" : "SmallCapsule");
        }

        int? oysterIndex = Consume(
            PrototypeNativeOvergrowthEvents.NeowOysterPairSiteId,
            "run:act:1:neow:oyster_pair",
            [ChoiceId("NutritiousOyster"), ChoiceId("StoneHumidifier")],
            "cursed_relic:" + cursed);
        if (pending is not null)
            return new(completed.ToArray(), pending);
        positives.Add(oysterIndex == 0
            ? "NutritiousOyster" : "StoneHumidifier");

        int? talismanIndex = Consume(
            PrototypeNativeOvergrowthEvents.NeowTalismanPairSiteId,
            "run:act:1:neow:talisman_pair",
            [ChoiceId("NeowsTalisman"), ChoiceId("Pomander")],
            "cursed_relic:" + cursed);
        if (pending is not null)
            return new(completed.ToArray(), pending);
        positives.Add(talismanIndex == 0 ? "NeowsTalisman" : "Pomander");

        // Keep the existing generator's profile-specific filtering and order.
        positives.Remove("MassiveScroll");
        positives.Remove("Kaleidoscope");
        for (int end = positives.Count - 1, ordinal = 0;
             end > 0; --end, ++ordinal)
        {
            var poolBeforeSwap = positives.ToArray();
            int? selectedIndex = Consume(
                PrototypeNativeOvergrowthEvents
                    .NeowPositiveRelicShuffleSiteId,
                "run:act:1:neow:positive_relics",
                Enumerable.Range(0, end + 1).Select(index =>
                    "swap_index:" + index.ToString(
                        CultureInfo.InvariantCulture)).ToArray(),
                "positive_pool:" + CanonicalJson.Sha256(poolBeforeSwap),
                boundedCount: end + 1,
                requestOrdinal: ordinal);
            if (pending is not null)
                return new(completed.ToArray(), pending);
            (positives[end], positives[selectedIndex!.Value]) =
                (positives[selectedIndex.Value], positives[end]);
        }

        if (completed.Count != priorIndices.Length)
            throw new ArgumentException(
                "Neow offer prefix contains more outcomes than the source generator requests.",
                nameof(priorIndices));
        return new(completed.ToArray(), null);
    }
}

public sealed record ReferenceNatureNeowOfferSessionSnapshotV111(
    string SchemaId,
    string OriginSnapshotJson,
    string RunSnapshotJson,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    private ReferenceNatureDecisionV111[]? _decisionTranscript =
        ReferenceNatureSessionValueCopiesV111.CopyArray(DecisionTranscript);

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyArray(_decisionTranscript)!;
        init => _decisionTranscript = ReferenceNatureSessionValueCopiesV111
            .CopyArray(value);
    }
}

/// <summary>Atomic ReferenceNature continuation for the existing Act 1 Neow
/// offer generator. The ordinary run and sampler cursor publish together only
/// after the ordered choices and conditional positive-pool shuffle finish.</summary>
public sealed record ReferenceNatureNeowOfferSessionV111(
    string SchemaId,
    RunState Origin,
    RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
    : IReferenceNatureRunSetupSessionV111
{
    [JsonIgnore]
    public SimulationStateV111<PrototypeMechanicsContinuationStateV111,
        ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>>
        Simulation { get; init; } = new(
            new PrototypeMechanicsContinuationStateV111(
                PrototypeMechanicsContinuationKindV111.ReferenceNatureRunSession,
                Origin, Run),
            new ReferenceNatureStochasticStateV111<
                ReferenceNatureDecisionV111>(InitialNatureState, NatureState,
                DecisionTranscript));

    [JsonIgnore]
    public PrototypeMechanicsContinuationStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [JsonIgnore]
    public ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public RunState Origin
    {
        get => Mechanics.Origin!;
        init => Mechanics = Mechanics with { Origin = value };
    }

    public RunState Run
    {
        get => Mechanics.Current;
        init => Mechanics = Mechanics with { Current = value };
    }

    public ReferenceNatureStateV111 InitialNatureState
    {
        get => Stochastic.InitialNatureState;
        init => Stochastic = Stochastic with { InitialNatureState = value };
    }

    public ReferenceNatureStateV111 NatureState
    {
        get => Stochastic.NatureState;
        init => Stochastic = Stochastic with { NatureState = value };
    }

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => Stochastic.DecisionTranscript;
        init => Stochastic = Stochastic with { DecisionTranscript = value };
    }

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-neow-offer-session-v1";

    public static string[] NeowOfferSessionSiteIds =>
    [
        PrototypeNativeOvergrowthEvents.NeowCursedRelicSiteId,
        PrototypeNativeOvergrowthEvents.NeowLavaRockPairSiteId,
        PrototypeNativeOvergrowthEvents.NeowOysterPairSiteId,
        PrototypeNativeOvergrowthEvents.NeowTalismanPairSiteId,
        PrototypeNativeOvergrowthEvents.NeowPositiveRelicShuffleSiteId
    ];

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public int NextOrdinal => Stochastic.DecisionTranscript.Length;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => Complete ? null :
        ChanceLawEngineV111.QueryNeowOffer(
            Origin.RunSeed, Origin.Ascension, NextOrdinal,
            Stochastic.DecisionTranscript.Select(decision => decision.OutcomeIndex)
                .ToArray());

    [JsonIgnore]
    public bool Complete => NeowOfferDecisionPlanBuilderV111.Build(
        Origin.RunSeed, Origin.Ascension,
        Stochastic.DecisionTranscript.Select(decision => decision.OutcomeIndex)
            .ToArray()).Complete;

    [JsonIgnore]
    public ControlledStochasticDecisionV111[] ProviderDecisions
    {
        get
        {
            var plan = NeowOfferDecisionPlanBuilderV111.Build(
                Origin.RunSeed, Origin.Ascension,
                Stochastic.DecisionTranscript.Select(decision =>
                    decision.OutcomeIndex).ToArray());
            if (!plan.Complete || plan.Steps.Length !=
                    Stochastic.DecisionTranscript.Length)
                throw new InvalidOperationException(
                    "Provider decisions require a completed Neow-offer prefix.");
            return plan.Steps.Select(step =>
            {
                var selectedIndex = step.SelectedIndex ??
                    throw new InvalidDataException(
                        "Completed Neow-offer plan has a pending request.");
                var indexDecision = new RandomIndexDecisionV111(
                    step.Request, selectedIndex, "controlled-v111");
                var decision = ControlledStochasticDecisionV111.FromIndex(
                    indexDecision);
                decision.Validate();
                return decision;
            }).ToArray();
        }
    }

    public static ReferenceNatureNeowOfferSessionV111 Begin(
        RunState origin, ulong natureSeed)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ValidateOpeningBoundary(origin);
        var initial = ReferenceNatureRunCursorV111.Begin(
            origin, natureSeed, "Neow-offer");
        return Begin(origin, initial);
    }

    private static ReferenceNatureNeowOfferSessionV111 Begin(
        RunState origin, ReferenceNatureStateV111 initial)
    {
        ValidateOpeningBoundary(origin);
        var session = new ReferenceNatureNeowOfferSessionV111(
            CurrentSchemaId, origin.Fork(), origin.Fork(),
            initial.Fork(), initial.Fork(), []);
        session.Validate();
        return session;
    }

    public ReferenceNatureNeowOfferSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork()
        };
    }

    public ReferenceNatureNeowOfferSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature Neow-offer session is already complete.");

        var priorIndices = Stochastic.DecisionTranscript.Select(
            decision => decision.OutcomeIndex).ToArray();
        var law = ChanceLawEngineV111.QueryNeowOffer(
            Origin.RunSeed, Origin.Ascension, NextOrdinal, priorIndices);
        var selection = ReferenceNatureV111.SampleValidated(
            law, Stochastic.NatureState);
        var transcript = Stochastic.DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var nextNatureState = selection.NextState;
        var nextRun = Origin.Fork();
        var plan = NeowOfferDecisionPlanBuilderV111.Build(
            Origin.RunSeed, Origin.Ascension,
            transcript.Select(decision => decision.OutcomeIndex).ToArray());
        if (plan.Complete)
            nextRun = ReferenceNatureRunCursorV111.Commit(
                Replay(Origin, plan), nextNatureState);

        var next = this with
        {
            Run = nextRun,
            NatureState = nextNatureState,
            DecisionTranscript = transcript
        };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Origin is null || Run is null ||
            string.IsNullOrWhiteSpace(Origin.RunSeed) ||
            Origin.Ascension < 0)
            throw new InvalidDataException(
                "Invalid ReferenceNature Neow-offer session envelope.");
        Mechanics.Validate(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunSession);
        Stochastic.Validate();
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        ValidateOpeningBoundary(Origin);
        var plan = NeowOfferDecisionPlanBuilderV111.Build(
            Origin.RunSeed, Origin.Ascension,
            Stochastic.DecisionTranscript.Select(decision => decision.OutcomeIndex)
                .ToArray());
        if (!ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, Stochastic.InitialNatureState,
                Stochastic.NatureState,
                Stochastic.DecisionTranscript.Length, plan.Complete))
            throw new InvalidDataException(
                "ReferenceNature Neow-offer cursor differs from its session envelope.");

        var expectedNatureState = Stochastic.InitialNatureState.Fork();
        var priorIndices = new List<int>();
        for (int ordinal = 0; ordinal < Stochastic.DecisionTranscript.Length; ordinal++)
        {
            var law = ChanceLawEngineV111.QueryNeowOffer(
                Origin.RunSeed, Origin.Ascension, ordinal,
                priorIndices.ToArray());
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            var decision = Stochastic.DecisionTranscript[ordinal];
            decision.Validate();
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) != CanonicalJson.Sha256(decision))
                throw new InvalidDataException(
                    "ReferenceNature Neow-offer transcript does not replay.");
            priorIndices.Add(selection.OutcomeIndex);
            expectedNatureState = selection.NextState;
        }

        RunState expectedRun;
        if (plan.Complete)
            expectedRun = ReferenceNatureRunCursorV111.Commit(
                Replay(Origin, plan), expectedNatureState);
        else
        {
            expectedRun = Origin.Fork();
        }

        if (CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(Stochastic.NatureState) ||
            CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "ReferenceNature Neow-offer replay differs from its session envelope.");
    }

    private sealed record NeowOfferRunReplayV111(
        RunState Run, NativeNeowOfferAuditV111 Audit);

    private static RunState Replay(
        RunState origin, NeowOfferDecisionPlanV111 plan)
    {
        var scriptedDecisions = plan.Steps.Select(step =>
            new RandomIndexDecisionV111(
                step.Request, step.SelectedIndex!.Value,
                "scripted-v111")).ToArray();
        var selected = scriptedDecisions.Select(decision =>
            ControlledStochasticDecisionV111.FromIndex(decision)).ToArray();
        var captured = ControlledChanceContinuationExecutorV111.CaptureTyped(
            origin.RunSeed,
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            selected,
            seed =>
            {
                var generated = PrototypeNativeOvergrowthRunFactory
                    .CreateWithAudit(seed, origin.Ascension);
                return new NeowOfferRunReplayV111(
                    generated.State, generated.NeowOffers);
            },
            requestFilter: candidate => candidate.IndexRequest is { } request &&
                NeowOfferSessionSiteIds.Contains(
                    request.SiteId, StringComparer.Ordinal));
        if (captured.Suspended || captured.Value is not { } replay)
            throw new InvalidDataException(
                "Native Neow generator did not consume its complete selected offer prefix.");
        if (CanonicalJson.Sha256(replay.Audit.Choices.Select(decision =>
                new RandomIndexDecisionV111(
                    decision.Request, decision.SelectedIndex,
                    "scripted-v111")).ToArray()) !=
            CanonicalJson.Sha256(scriptedDecisions))
            throw new InvalidDataException(
                "Native Neow generation diverged from the saved offer prefix.");
        var world = replay.Run.World ?? throw new InvalidDataException(
            "Native Neow generation lost its run world.");
        var eventState = world.Event ?? throw new InvalidDataException(
            "Native Neow generation lost its event state.");
        var expectedEventStream = NativeRngV111Contexts.ForEvent(
            origin.RunSeed,
            PrototypeNativeOvergrowthEvents.NeowRngEventEntry);
        if (eventState.EventId != PrototypeNativeOvergrowthEvents.NeowEventId ||
            eventState.Stochastic.NativeRng is not { } eventStream ||
            NativeRngV111.ToSnapshot(eventStream) !=
                NativeRngV111.ToSnapshot(expectedEventStream) ||
            NativeRngV111.ToSnapshot(eventStream) !=
                NativeRngV111.ToSnapshot(replay.Audit.Stochastic.NativeRng))
            throw new InvalidDataException(
                "Native Neow generation changed its audited event stream.");
        return replay.Run with
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

    public static string Save(ReferenceNatureNeowOfferSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureNeowOfferSessionSnapshotV111(
            CurrentSchemaId, RunSnapshot.Save(session.Origin),
            RunSnapshot.Save(session.Run),
            session.Stochastic.InitialNatureState,
            session.Stochastic.NatureState,
            (ReferenceNatureDecisionV111[])
                session.Stochastic.DecisionTranscript.Clone());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureNeowOfferSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureNeowOfferSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature Neow-offer snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Neow-offer snapshot.");
        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var replay = Begin(origin, snapshot.InitialNatureState.Fork());
        for (int index = 0; index < snapshot.DecisionTranscript.Length; index++)
            replay = replay.ResolveNext();
        replay.Validate();
        if (CanonicalJson.Sha256(replay.Run) != CanonicalJson.Sha256(savedRun) ||
            CanonicalJson.Sha256(replay.Stochastic.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            !replay.Stochastic.DecisionTranscript.SequenceEqual(snapshot.DecisionTranscript))
            throw new InvalidDataException(
                "ReferenceNature Neow-offer snapshot does not replay from its origin.");
        return replay;
    }

    private static void ValidateOpeningBoundary(RunState origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (string.IsNullOrWhiteSpace(origin.RunSeed) ||
            CanonicalJson.Sha256(PrototypeGameFactory.Create(
                origin.RunSeed, origin.Ascension)) !=
            CanonicalJson.Sha256(origin))
            throw new InvalidOperationException(
                "ReferenceNature Neow offers require a pristine prototype run-start state.");
    }
}
