using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureAncientOfferSessionSnapshotV111(
    string SchemaId, string SiteId, string OriginSnapshotJson,
    string RunSnapshotJson, ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    private ReferenceNatureDecisionV111[]? _decisionTranscript =
        Copy(DecisionTranscript);

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => Copy(_decisionTranscript)!;
        init => _decisionTranscript = Copy(value);
    }

    public ReferenceNatureAncientOfferSessionSnapshotV111 Fork() => this with
    {
        InitialNatureState = InitialNatureState.Fork(),
        NatureState = NatureState.Fork(),
        DecisionTranscript = (ReferenceNatureDecisionV111[])
            DecisionTranscript.Clone()
    };

    private static ReferenceNatureDecisionV111[]? Copy(
        ReferenceNatureDecisionV111[]? transcript) => transcript is null
        ? null
        : (ReferenceNatureDecisionV111[])transcript.Clone();
}

/// <summary>
/// Atomic ReferenceNature envelope for Ancient offer generation. Individual
/// decisions remain private to the session until every source event-offer or
/// Fisher-Yates request has been sampled and replayed through its generator.
/// </summary>
public sealed record ReferenceNatureAncientOfferSessionV111(
    string SchemaId, string SiteId, RunState Origin, RunState Run,
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
        "sts2-v111-reference-nature-ancient-offer-session-v1";

    private const string EventOfferOutcomePrefix = "event_choice:";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool Complete => DecisionTranscript.Length ==
        ChanceLawEngineV111.AncientOfferDecisionCount(Origin, SiteId);

    [JsonIgnore]
    public int NextOrdinal => DecisionTranscript.Length;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => Complete ? null :
        ChanceLawEngineV111.QueryAncientOfferSessionStep(
            Origin, SiteId, DecisionTranscript);

    [JsonIgnore]
    public ControlledStochasticDecisionV111[] ProviderDecisions
    {
        get
        {
            if (!Complete)
                throw new InvalidOperationException(
                    "Provider decisions require a completed Ancient-offer prefix.");
            return BuildProviderDecisions(
                Origin, SiteId, DecisionTranscript);
        }
    }

    public static ReferenceNatureAncientOfferSessionV111 Begin(
        RunState source, string siteId, ulong seed) =>
        Begin(source, siteId, (ulong?)seed);

    public static ReferenceNatureAncientOfferSessionV111 Begin(
        RunState source, string siteId) =>
        Begin(source, siteId, (ulong?)null);

    private static ReferenceNatureAncientOfferSessionV111 Begin(
        RunState source, string siteId, ulong? seed)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        return Begin(source, siteId, ReferenceNatureRunCursorV111.Begin(
            source, seed, "Ancient-offer"));
    }

    private static ReferenceNatureAncientOfferSessionV111 Begin(
        RunState source, string siteId,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ValidateOpeningBoundary(source, siteId);
        ReferenceNatureRunCursorV111.ValidateInitial(
            source, initialNatureState, "Ancient-offer");
        var session = new ReferenceNatureAncientOfferSessionV111(
            CurrentSchemaId, siteId, source.Fork(), source.Fork(),
            initialNatureState.Fork(), initialNatureState.Fork(), []);
        session.Validate();
        return session;
    }

    public ReferenceNatureAncientOfferSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork()
        };
    }

    public ReferenceNatureAncientOfferSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature Ancient-offer session is already complete.");

        var law = ChanceLawEngineV111.QueryAncientOfferSessionStep(
            Origin, SiteId, DecisionTranscript);
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var transcript = DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var nextNatureState = selection.NextState;
        var nextRun = Origin.Fork();
        if (transcript.Length ==
            ChanceLawEngineV111.AncientOfferDecisionCount(Origin, SiteId))
        {
            nextRun = GenerateEvent(Origin, SiteId, transcript);
            nextRun = ReferenceNatureRunCursorV111.Commit(
                nextRun, nextNatureState);
        }
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
        if (SchemaId != CurrentSchemaId ||
            !IsSupportedSite(SiteId) || Origin is null || Run is null ||
            Stochastic is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Ancient-offer session envelope.");
        Mechanics.Validate(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunSession);
        Stochastic.Validate();
        ValidateOpeningBoundary(Origin, SiteId);
        if (DecisionTranscript.Length >
            ChanceLawEngineV111.AncientOfferDecisionCount(Origin, SiteId))
            throw new InvalidDataException(
                "ReferenceNature Ancient-offer transcript exceeds its source draw count.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        if (!ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature Ancient-offer cursor differs from the run envelope.");

        var expectedNatureState = InitialNatureState.Fork();
        for (var ordinal = 0; ordinal < DecisionTranscript.Length; ordinal++)
        {
            var decision = DecisionTranscript[ordinal];
            decision.Validate();
            var law = ChanceLawEngineV111.QueryAncientOfferSessionStep(
                Origin, SiteId, DecisionTranscript.Take(ordinal).ToArray());
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) != CanonicalJson.Sha256(decision))
                throw new InvalidDataException(
                    "ReferenceNature Ancient-offer transcript does not replay.");
            expectedNatureState = selection.NextState;
        }

        var expectedRun = Complete
            ? ReferenceNatureRunCursorV111.Commit(
                GenerateEvent(Origin, SiteId, DecisionTranscript),
                expectedNatureState)
            : Origin.Fork();
        if (CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(NatureState) ||
            CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "ReferenceNature Ancient-offer replay differs from its session envelope.");
    }

    public static ReferenceNatureAncientOfferSessionSnapshotV111 SaveSnapshot(
        ReferenceNatureAncientOfferSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        return new ReferenceNatureAncientOfferSessionSnapshotV111(
            CurrentSchemaId, session.SiteId, RunSnapshot.Save(session.Origin),
            RunSnapshot.Save(session.Run), session.InitialNatureState,
            session.NatureState,
            (ReferenceNatureDecisionV111[])session.DecisionTranscript.Clone());
    }

    public static string Save(ReferenceNatureAncientOfferSessionV111 session) =>
        JsonSerializer.Serialize(SaveSnapshot(session));

    public static ReferenceNatureAncientOfferSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureAncientOfferSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature Ancient-offer snapshot.");
        return Restore(snapshot);
    }

    public static ReferenceNatureAncientOfferSessionV111 Restore(
        ReferenceNatureAncientOfferSessionSnapshotV111 snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SchemaId != CurrentSchemaId ||
            !IsSupportedSite(snapshot.SiteId) ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Ancient-offer snapshot.");

        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var replay = Begin(origin, snapshot.SiteId,
            snapshot.InitialNatureState);
        for (var index = 0; index < snapshot.DecisionTranscript.Length; index++)
        {
            if (replay.Complete)
                throw new InvalidDataException(
                    "ReferenceNature Ancient-offer replay exceeded its source draw count.");
            replay = replay.ResolveNext();
            if (CanonicalJson.Sha256(replay.DecisionTranscript[^1]) !=
                CanonicalJson.Sha256(snapshot.DecisionTranscript[index]))
                throw new InvalidDataException(
                    "ReferenceNature Ancient-offer replay selected a different outcome.");
        }
        if (CanonicalJson.Sha256(replay.Run) != CanonicalJson.Sha256(savedRun) ||
            CanonicalJson.Sha256(replay.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            replay.DecisionTranscript.Length !=
                snapshot.DecisionTranscript.Length)
            throw new InvalidDataException(
                "ReferenceNature Ancient-offer replay differs from its snapshot.");
        replay.Validate();
        return replay;
    }

    internal static string? SiteForAssignedAncientId(string? ancientId) =>
        ancientId switch
        {
            null => null,
            NativeOrobasEventV111.EventId =>
                NativeOrobasEventV111.OfferSiteId,
            NativePaelRewardsV111.PaelId => NativePaelRewardsV111.OfferSiteId,
            NativeAncientRewardsV111.TezcataraId =>
                NativeAncientRewardsV111.TezcataraOfferSiteId,
            NativeDarvEventV111.EventId =>
                NativeDarvEventV111.OfferVariantSiteId,
            NativeNonupeipeEventV111.EventId =>
                NativeNonupeipeEventV111.ShuffleSiteId,
            NativeTanxEventV111.EventId => NativeTanxEventV111.ShuffleSiteId,
            NativeVakuuEventV111.EventId => NativeVakuuEventV111.ShuffleSiteId,
            _ => throw new NotSupportedException(
                $"Assigned Ancient '{ancientId}' has no resumable offer session.")
        };

    private static RunState GenerateEvent(
        RunState origin, string siteId,
        ReferenceNatureDecisionV111[] transcript)
    {
        var decisions = BuildProviderDecisions(
            origin, siteId, transcript);
        // Session validation replays the generator repeatedly; keep those
        // audit-only calls out of any enclosing source-boundary journal.
        using var replayJournal = RandomBoundaryJournalV111.Begin();
        var replay = ControlledChanceContinuationExecutorV111.CaptureStochastic(
            origin, ControlledStochasticContinuationStateV111.CurrentSchemaId,
            decisions, checkpoint => GenerateEventMechanics(checkpoint, siteId));
        if (replay.Run is not { } run || replay.Continuation is not null)
            throw new InvalidOperationException(
                "Ancient offer generator did not consume the complete Nature transcript.");
        return run;
    }

    private static ControlledStochasticDecisionV111[] BuildProviderDecisions(
        RunState origin, string siteId,
        ReferenceNatureDecisionV111[] transcript)
    {
        var scripted = BuildReplayChoices(origin, siteId, transcript);
        return scripted.Select(choice =>
        {
            var decision = new RandomIndexDecisionV111(
                choice.Request with
                {
                    OrderedCandidates = (int[])
                        choice.Request.OrderedCandidates.Clone()
                }, choice.SelectedIndex, "controlled-v111");
            decision.Validate();
            return ControlledStochasticDecisionV111.FromIndex(decision);
        }).ToArray();
    }

    private static AncientOfferReplayChoiceV111[] BuildReplayChoices(
        RunState origin, string siteId,
        ReferenceNatureDecisionV111[] transcript)
    {
        if (siteId is not (
                NativeOrobasEventV111.OfferSiteId or
                NativePaelRewardsV111.OfferSiteId or
                NativeAncientRewardsV111.TezcataraOfferSiteId or
                NativeNonupeipeEventV111.ShuffleSiteId or
                NativeTanxEventV111.ShuffleSiteId or
                NativeVakuuEventV111.ShuffleSiteId or
                NativeDarvEventV111.OfferVariantSiteId))
            throw new NotSupportedException(
                $"Ancient offer replay does not support site '{siteId}'.");
        if (siteId == NativeDarvEventV111.OfferVariantSiteId)
            return BuildDarvReplayChoices(origin, transcript);
        var isShuffle = ChanceLawEngineV111.IsShuffleAncientOfferSite(siteId);
        var expectedCount = ChanceLawEngineV111.AncientOfferDecisionCount(
            origin, siteId);
        if (transcript.Length != expectedCount)
            throw new InvalidOperationException(
                "Ancient offer generation requires its complete sampled transcript.");
        AncientOfferReplayChoiceV111[] scripted;
        if (isShuffle)
        {
            var pools = ChanceLawEngineV111.AncientOfferShuffleChoicePools(
                origin, siteId);
            var requests = ChanceLawEngineV111.AncientOfferShuffleRequests(
                siteId, pools);
            scripted = transcript.Select((decision, ordinal) =>
            {
                var expectedOutcome = "swap_index:" +
                    decision.OutcomeIndex.ToString(CultureInfo.InvariantCulture);
                if (decision.OutcomeId != expectedOutcome ||
                    decision.OutcomeIndex < 0 ||
                    decision.OutcomeIndex >=
                        requests[ordinal].Request.SupportCount)
                    throw new InvalidDataException(
                        "Sampled Ancient offer shuffle index is outside its source support.");
                return new AncientOfferReplayChoiceV111(
                    requests[ordinal].Request, decision.OutcomeIndex);
            }).ToArray();
        }
        else
        {
            var candidates = Enumerable.Range(0, 3).Select(ordinal =>
                CandidateChoiceIds(origin, siteId, ordinal)).ToArray();
            scripted = transcript.Select((decision, ordinal) =>
            {
                var selectedIndex = Array.FindIndex(candidates[ordinal], choiceId =>
                    EventOfferOutcomePrefix + choiceId == decision.OutcomeId);
                if (selectedIndex < 0)
                    throw new InvalidDataException(
                        "Sampled Ancient offer is outside its source candidate pool.");
                var request = new RandomIndexRequestV111(
                    siteId, StreamId(siteId), OwnerId(siteId), ordinal,
                    Enumerable.Range(0, candidates[ordinal].Length).ToArray());
                return new AncientOfferReplayChoiceV111(request, selectedIndex);
            }).ToArray();
        }
        return scripted;
    }

    private static RunState GenerateEventMechanics(
        RunState origin, string siteId)
    {
        var provider = new AncientOfferScopedIndexProviderV111();
        return siteId switch
        {
            NativeOrobasEventV111.OfferSiteId =>
                NativeOrobasEventV111.BeginOpeningWithAudit(origin, provider).State,
            NativePaelRewardsV111.OfferSiteId =>
                NativePaelRewardsV111.BeginPaelOpeningWithAudit(origin, provider).State,
            NativeAncientRewardsV111.TezcataraOfferSiteId =>
                NativeAncientRewardsV111.BeginTezcataraOpeningWithAudit(
                    origin, provider).State,
            NativeNonupeipeEventV111.ShuffleSiteId =>
                NativeNonupeipeEventV111.BeginOpeningWithAudit(
                    origin, provider).State,
            NativeTanxEventV111.ShuffleSiteId =>
                NativeTanxEventV111.BeginOpeningWithAudit(origin, provider).State,
            NativeVakuuEventV111.ShuffleSiteId =>
                NativeVakuuEventV111.BeginOpeningWithAudit(origin, provider).State,
            NativeDarvEventV111.OfferVariantSiteId =>
                NativeDarvEventV111.BeginOpeningWithAudit(origin, provider).State,
            _ => throw new NotSupportedException(
                $"ReferenceNature Ancient-offer session does not support '{siteId}'.")
        };
    }

    private static AncientOfferReplayChoiceV111[] BuildDarvReplayChoices(
        RunState origin, ReferenceNatureDecisionV111[] transcript)
    {
        var shuffleRequests = ChanceLawEngineV111
            .DarvOfferShuffleRequests(origin);
        if (transcript.Length != shuffleRequests.Length + 1)
            throw new InvalidOperationException(
                "Darv generation requires the offer variant and complete shuffle prefix.");
        var variant = transcript[0];
        var expectedVariant = variant.OutcomeIndex switch
        {
            0 => "offer_variant:dusty_tome",
            1 => "offer_variant:three_relics",
            _ => null
        };
        if (variant.LawId is null ||
            variant.OutcomeId != expectedVariant)
            throw new InvalidDataException(
                "Sampled Darv offer variant is outside its binary support.");

        var scripted = new List<AncientOfferReplayChoiceV111>(
            shuffleRequests.Length + 1)
        {
            new(ChanceLawEngineV111.DarvOfferVariantRequest(origin),
                variant.OutcomeIndex)
        };
        for (var index = 0; index < shuffleRequests.Length; index++)
        {
            var decision = transcript[index + 1];
            var expectedOutcome = "swap_index:" +
                decision.OutcomeIndex.ToString(CultureInfo.InvariantCulture);
            if (decision.OutcomeId != expectedOutcome ||
                decision.OutcomeIndex < 0 ||
                decision.OutcomeIndex >= shuffleRequests[index].SupportCount)
                throw new InvalidDataException(
                    "Sampled Darv shuffle index is outside its source support.");
            scripted.Add(new AncientOfferReplayChoiceV111(
                shuffleRequests[index], decision.OutcomeIndex));
        }
        return scripted.ToArray();
    }

    internal static string[] CandidateChoiceIds(
        RunState run, string siteId, int ordinal)
    {
        if (siteId == NativeOrobasEventV111.OfferSiteId)
            return NativeOrobasEventV111.OfferCandidateIds(run.Player, ordinal);
        if (siteId == NativePaelRewardsV111.OfferSiteId)
            return NativePaelRewardsV111.OfferPools(run.Player)[ordinal]
                .Select(id => "take_" + id).ToArray();
        if (siteId == NativeAncientRewardsV111.TezcataraOfferSiteId)
            return NativeAncientRewardsV111.TezcataraPools(run.Player)[ordinal]
                .Select(id => "take_" + id).ToArray();
        throw new NotSupportedException(
            $"ReferenceNature Ancient-offer session does not support '{siteId}'.");
    }

    private static void ValidateOpeningBoundary(RunState run, string siteId)
    {
        ChanceLawEngineV111.ValidateAncientOfferOpeningBoundary(run, siteId);
    }

    private static bool IsSupportedSite(string siteId) =>
        ChanceLawEngineV111.IsSupportedAncientOfferSessionSite(siteId);

    internal static string StreamId(string siteId) => siteId switch
    {
        NativeOrobasEventV111.OfferSiteId => "context.event.OROBAS.player_0",
        NativePaelRewardsV111.OfferSiteId => "context.event.PAEL.player_0",
        NativeAncientRewardsV111.TezcataraOfferSiteId =>
            "context.event.TEZCATARA.player_0",
        _ => throw new NotSupportedException(
            $"No event RNG stream is registered for '{siteId}'.")
    };

    internal static string OwnerId(string siteId) => siteId switch
    {
        NativeOrobasEventV111.OfferSiteId => "event:OROBAS:act:2:floor:0",
        NativePaelRewardsV111.OfferSiteId => "event:PAEL:act:2:floor:0",
        NativeAncientRewardsV111.TezcataraOfferSiteId =>
            "event:TEZCATARA:act:2:floor:0",
        _ => throw new NotSupportedException(
            $"No event owner is registered for '{siteId}'.")
    };

    private sealed record AncientOfferReplayChoiceV111(
        RandomIndexRequestV111 Request, int SelectedIndex);

    /// <summary>Routes explicit Ancient-offer providers through the active
    /// shared continuation scope while preserving the event generator's
    /// established controlled-provider marker.</summary>
    private sealed class AncientOfferScopedIndexProviderV111
        : IRandomIndexProviderV111
    {
        public string Mode => "scripted-v111";

        public RandomIndexDecisionV111 Resolve(
            RandomIndexRequestV111 request, RngBundle streams) =>
            StochasticProviderScopeV111.ResolveIndex(this, request, streams);
    }
}

