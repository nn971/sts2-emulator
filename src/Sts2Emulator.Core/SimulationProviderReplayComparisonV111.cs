namespace Sts2Emulator.Core;

/// <summary>
/// Player-visible observation and ordered legal-action menu at one committed
/// provider replay boundary.
/// </summary>
public sealed record SimulationProviderReplayPublicBoundaryViewV111(
    string ObservationSchemaId,
    string ObservationHash,
    string LegalActionsFingerprint,
    int LegalActionCount);

/// <summary>
/// Mechanics captured for one provider replay interval. The action list is
/// detached from the caller so its fingerprint cannot change after capture.
/// </summary>
public sealed record SimulationProviderReplayMechanicsCaptureV111
{
    private RunState _origin = null!;
    private GameAction[] _actions = [];
    private RunState _result = null!;
    private RunState[]? _actionBoundaryStates;
    private long[][]? _drawnCardInstanceIdsByAction;
    private NativeEngineJournalEntryV111[][]?
        _combatEventHookTranscriptByAction;
    private SimulationProviderReplayPublicBoundaryViewV111[]?
        _playerBoundaryViews;

    public RunState Origin
    {
        get => _origin.Fork();
        init => _origin = value?.Fork() ?? null!;
    }

    public GameAction[] Actions
    {
        get => CloneActions(_actions);
        init => _actions = CloneActions(value);
    }

    public RunState Result
    {
        get => _result.Fork();
        init => _result = value?.Fork() ?? null!;
    }

    /// <summary>
    /// Mechanics snapshots after each player action has completed its
    /// stochastic continuation. Null means this capture does not provide a
    /// complete action-boundary trajectory.
    /// </summary>
    public RunState[]? ActionBoundaryStates
    {
        get => CloneStates(_actionBoundaryStates);
        init => _actionBoundaryStates = CloneStates(value);
    }

    /// <summary>
    /// Ordered physical card identities consumed by DrawCards during each
    /// player action. Null means this capture has no complete draw trace.
    /// </summary>
    public long[][]? DrawnCardInstanceIdsByAction
    {
        get => CloneDrawnCards(_drawnCardInstanceIdsByAction);
        init => _drawnCardInstanceIdsByAction = CloneDrawnCards(value);
    }

    /// <summary>
    /// Ordered combat event dispatch and triggered-hook journal for each
    /// replayed player action. Null means this capture has no such transcript.
    /// </summary>
    public NativeEngineJournalEntryV111[][]?
        CombatEventHookTranscriptByAction
    {
        get => CloneCombatEventHookTranscripts(
            _combatEventHookTranscriptByAction);
        init => _combatEventHookTranscriptByAction =
            CloneCombatEventHookTranscripts(value);
    }

    /// <summary>
    /// Player-facing observation and legal-action menu at the origin and each
    /// captured action boundary. Null means this capture has no complete view
    /// trace.
    /// </summary>
    public SimulationProviderReplayPublicBoundaryViewV111[]?
        PlayerBoundaryViews
    {
        get => ClonePlayerBoundaryViews(_playerBoundaryViews);
        init => _playerBoundaryViews = ClonePlayerBoundaryViews(value);
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasActionBoundaryStates => _actionBoundaryStates is not null;

    public SimulationProviderReplayMechanicsCaptureV111(
        RunState Origin, GameAction[] Actions, RunState Result)
        : this(Origin, Actions, Result, null, null, null)
    {
    }

    [System.Text.Json.Serialization.JsonConstructor]
    public SimulationProviderReplayMechanicsCaptureV111(
        RunState Origin, GameAction[] Actions, RunState Result,
        RunState[]? ActionBoundaryStates,
        long[][]? DrawnCardInstanceIdsByAction = null,
        NativeEngineJournalEntryV111[][]?
            CombatEventHookTranscriptByAction = null,
        SimulationProviderReplayPublicBoundaryViewV111[]?
            PlayerBoundaryViews = null)
    {
        this.Origin = Origin;
        this.Actions = Actions;
        this.Result = Result;
        this.ActionBoundaryStates = ActionBoundaryStates;
        this.DrawnCardInstanceIdsByAction = DrawnCardInstanceIdsByAction;
        this.CombatEventHookTranscriptByAction =
            CombatEventHookTranscriptByAction;
        this.PlayerBoundaryViews = PlayerBoundaryViews;
    }

    public SimulationProviderReplayMechanicsCaptureV111 Fork() => this with
    {
        Origin = _origin.Fork(),
        Actions = CloneActions(_actions),
        Result = _result.Fork(),
        ActionBoundaryStates = CloneStates(_actionBoundaryStates),
        DrawnCardInstanceIdsByAction =
            CloneDrawnCards(_drawnCardInstanceIdsByAction),
        CombatEventHookTranscriptByAction =
            CloneCombatEventHookTranscripts(
                _combatEventHookTranscriptByAction),
        PlayerBoundaryViews = ClonePlayerBoundaryViews(_playerBoundaryViews)
    };

    public void Validate()
    {
        if (_origin is null || _actions is null || _result is null)
            throw new InvalidDataException(
                "Provider replay mechanics capture is incomplete.");
        RunConfiguration.ValidateState(_origin);
        RunConfiguration.ValidateState(_result);
        if (_actionBoundaryStates is not null)
        {
            if (_actionBoundaryStates.Length != _actions.Length)
                throw new InvalidDataException(
                    "Provider replay action-boundary state count does not match its actions.");
            for (var index = 0; index < _actionBoundaryStates.Length; index++)
            {
                if (_actionBoundaryStates[index] is null)
                    throw new InvalidDataException(
                        "Provider replay action-boundary state is null at " +
                        index + ".");
                RunConfiguration.ValidateState(_actionBoundaryStates[index]);
            }
        }
        if (_drawnCardInstanceIdsByAction is not null &&
            (_drawnCardInstanceIdsByAction.Length != _actions.Length ||
             _drawnCardInstanceIdsByAction.Any(cards => cards is null ||
                 cards.Any(cardId => cardId <= 0))))
            throw new InvalidDataException(
                "Provider replay drawn-card identity trace is invalid.");
        if (_combatEventHookTranscriptByAction is not null &&
            _combatEventHookTranscriptByAction.Length != _actions.Length)
            throw new InvalidDataException(
                "Provider replay combat event/hook transcript count does not match its actions.");
        if (_combatEventHookTranscriptByAction is not null)
        {
            for (var actionIndex = 0;
                actionIndex < _combatEventHookTranscriptByAction.Length;
                actionIndex++)
            {
                var transcript = _combatEventHookTranscriptByAction[actionIndex];
                if (transcript is null)
                    throw new InvalidDataException(
                        "Provider replay combat event/hook transcript is null at " +
                        actionIndex + ".");
                for (var entryIndex = 0; entryIndex < transcript.Length;
                    entryIndex++)
                {
                    var entry = transcript[entryIndex];
                    if (entry is null || entry.Sequence != entryIndex + 1 ||
                        string.IsNullOrWhiteSpace(entry.Stage) ||
                        string.IsNullOrWhiteSpace(entry.Kind))
                        throw new InvalidDataException(
                            "Provider replay combat event/hook entry is invalid at action " +
                            actionIndex + ", entry " + entryIndex + ".");
                }
            }
        }
        if (_playerBoundaryViews is not null)
        {
            if (_actionBoundaryStates is null ||
                _playerBoundaryViews.Length != checked(_actions.Length + 1))
                throw new InvalidDataException(
                    "Provider replay public boundary view count does not match its origin and action boundaries.");
            for (var index = 0; index < _playerBoundaryViews.Length; index++)
            {
                var view = _playerBoundaryViews[index];
                if (view is null ||
                    string.IsNullOrWhiteSpace(view.ObservationSchemaId) ||
                    !IsSha256Fingerprint(view.ObservationHash) ||
                    !IsSha256Fingerprint(view.LegalActionsFingerprint) ||
                    view.LegalActionCount < 0)
                    throw new InvalidDataException(
                        "Provider replay public boundary view is invalid at " +
                        index + ".");
            }
        }
        for (var index = 0; index < _actions.Length; index++)
            if (_actions[index] is null ||
                string.IsNullOrWhiteSpace(_actions[index].Kind) ||
                _actions[index].Payload.ValueKind ==
                    System.Text.Json.JsonValueKind.Undefined)
                throw new InvalidDataException(
                    "Provider replay action is invalid at " + index + ".");
    }

    private static GameAction[] CloneActions(GameAction[] actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        return actions.Select(action => action is null ? null! : action with
        {
            Payload = action.Payload.Clone()
        }).ToArray();
    }

    private static RunState[]? CloneStates(RunState[]? states) =>
        states?.Select(state => state?.Fork() ?? null!).ToArray();

    private static long[][]? CloneDrawnCards(long[][]? cards) =>
        cards?.Select(actionCards => actionCards is null
            ? null!
            : (long[])actionCards.Clone()).ToArray();

    private static NativeEngineJournalEntryV111[][]?
        CloneCombatEventHookTranscripts(
            NativeEngineJournalEntryV111[][]? transcripts) =>
        transcripts?.Select(transcript => transcript is null
            ? null!
            : (NativeEngineJournalEntryV111[])transcript.Clone()).ToArray();

    private static SimulationProviderReplayPublicBoundaryViewV111[]?
        ClonePlayerBoundaryViews(
            SimulationProviderReplayPublicBoundaryViewV111[]? views) =>
        views is null
            ? null
            : (SimulationProviderReplayPublicBoundaryViewV111[])views.Clone();

    private static bool IsSha256Fingerprint(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    internal static SimulationProviderReplayPublicBoundaryViewV111[]?
        CapturePlayerBoundaryViews(
            RunState origin, RunState[]? actionBoundaryStates)
    {
        if (actionBoundaryStates is null)
            return null;
        var environment = new PrototypeAiEnvironment();
        return new[] { origin }.Concat(actionBoundaryStates)
            .Select(state =>
            {
                var frame = environment.Observe(state);
                return new SimulationProviderReplayPublicBoundaryViewV111(
                    frame.SchemaId,
                    frame.ObservationHash,
                    CanonicalJson.Sha256(frame.LegalActions),
                    frame.LegalActions.Length);
            }).ToArray();
    }
}

/// <summary>One replay-validated semantic outcome in a captured interval.</summary>
public sealed record SimulationProviderReplaySemanticOutcomeV111(
    int TransitionIndex, string EventId, string LawId, string OutcomeId,
    int OutcomeIndex)
{
    public string? SiteId { get; init; }
    public string? OperationKind { get; init; }
    public string? OwnerId { get; init; }
    public int? Ordinal { get; init; }
    public string? SupportFingerprint { get; init; }
    public string? SemanticOutcomeId { get; init; }

    public bool HasProviderIndependentIdentity =>
        !string.IsNullOrWhiteSpace(SiteId) &&
        !string.IsNullOrWhiteSpace(OperationKind) &&
        !string.IsNullOrWhiteSpace(OwnerId) && Ordinal is >= 0 &&
        !string.IsNullOrWhiteSpace(SupportFingerprint) &&
        !string.IsNullOrWhiteSpace(SemanticOutcomeId);

    public void Validate()
    {
        if (TransitionIndex < 0 || string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(LawId) ||
            string.IsNullOrWhiteSpace(OutcomeId) || OutcomeIndex < 0)
            throw new InvalidDataException(
                "Provider replay semantic outcome is invalid.");
        var hasAnyProviderIndependentIdentity = SiteId is not null ||
            OperationKind is not null || OwnerId is not null ||
            Ordinal is not null || SupportFingerprint is not null ||
            SemanticOutcomeId is not null;
        if (hasAnyProviderIndependentIdentity &&
            !HasProviderIndependentIdentity)
            throw new InvalidDataException(
                "Provider replay semantic outcome identity is incomplete.");
    }
}

internal sealed record SimulationProviderReplaySemanticSelectionV111(
    string LawId, string EventId, string OutcomeId, int OutcomeIndex);

internal static class SimulationProviderReplaySemanticIdentityV111
{
    public static string[] SupportIds(
        ChanceLawV111 law, string[]? candidateLabels = null)
    {
        ArgumentNullException.ThrowIfNull(law);
        if (candidateLabels is { Length: > 0 } labels)
        {
            if (labels.Length != law.SupportCount)
                throw new InvalidDataException(
                    "Provider-independent labels differ from their Chance Law support.");
            return (string[])labels.Clone();
        }
        return law.Outcomes.Select(outcome => SemanticId(
            outcome.OutcomeId)).ToArray();
    }

    private static string SemanticId(string outcomeId)
    {
        if (outcomeId.StartsWith("card_model:", StringComparison.Ordinal))
            return outcomeId["card_model:".Length..];
        if (outcomeId.StartsWith("potion_model:", StringComparison.Ordinal))
        {
            var modelId = outcomeId["potion_model:".Length..];
            var indexMarker = modelId.IndexOf(":index:",
                StringComparison.Ordinal);
            return indexMarker < 0 ? modelId : modelId[..indexMarker];
        }
        if (outcomeId.StartsWith("relic_model:", StringComparison.Ordinal))
        {
            var relicId = outcomeId["relic_model:".Length..];
            var indexMarker = relicId.IndexOf(":index:",
                StringComparison.Ordinal);
            return indexMarker < 0 ? relicId : relicId[..indexMarker];
        }
        return outcomeId;
    }
}

/// <summary>
/// Primitive provider boundaries and semantic event outcomes captured for
/// the same replay interval. The two traces remain distinct: one records
/// provider calls, while the other records resolved event identities.
/// </summary>
public sealed record SimulationProviderReplayStochasticCaptureV111
{
    private RandomBoundaryTranscriptEntryV111[] _boundaries = [];
    private SimulationProviderReplaySemanticOutcomeV111[] _semanticOutcomes =
        [];

    public RandomBoundaryTranscriptEntryV111[] Boundaries
    {
        get => CloneBoundaries(_boundaries);
        init => _boundaries = CloneBoundaries(value);
    }

    public SimulationProviderReplaySemanticOutcomeV111[] SemanticOutcomes
    {
        get => (SimulationProviderReplaySemanticOutcomeV111[])
            _semanticOutcomes.Clone();
        init => _semanticOutcomes = CloneSemanticOutcomes(value);
    }

    public SimulationProviderReplayStochasticCaptureV111(
        RandomBoundaryTranscriptEntryV111[] Boundaries,
        SimulationProviderReplaySemanticOutcomeV111[]? SemanticOutcomes = null)
    {
        this.Boundaries = Boundaries;
        this.SemanticOutcomes = SemanticOutcomes ?? [];
    }

    public SimulationProviderReplayStochasticCaptureV111 Fork() => this with
    {
        Boundaries = CloneBoundaries(_boundaries),
        SemanticOutcomes = CloneSemanticOutcomes(_semanticOutcomes)
    };

    public void Validate()
    {
        if (_boundaries is null)
            throw new InvalidDataException(
                "Provider replay stochastic capture is incomplete.");
        for (var index = 0; index < _boundaries.Length; index++)
        {
            var boundary = _boundaries[index];
            if (boundary is null || boundary.Sequence != index + 1)
                throw new InvalidDataException(
                    "Provider replay boundary sequence is invalid at " +
                    index + ".");
            boundary.Validate();
        }
        if (_semanticOutcomes is null)
            throw new InvalidDataException(
                "Provider replay semantic outcome capture is incomplete.");
        var previousTransitionIndex = -1;
        foreach (var outcome in _semanticOutcomes)
        {
            if (outcome is null ||
                outcome.TransitionIndex <= previousTransitionIndex)
                throw new InvalidDataException(
                    "Provider replay semantic outcome order is invalid.");
            outcome.Validate();
            previousTransitionIndex = outcome.TransitionIndex;
        }
    }

    private static RandomBoundaryTranscriptEntryV111[] CloneBoundaries(
        RandomBoundaryTranscriptEntryV111[] boundaries)
    {
        ArgumentNullException.ThrowIfNull(boundaries);
        return boundaries.Select(boundary => boundary is null
            ? null!
            : boundary.Fork()).ToArray();
    }

    private static SimulationProviderReplaySemanticOutcomeV111[]
        CloneSemanticOutcomes(
            SimulationProviderReplaySemanticOutcomeV111[] outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        return (SimulationProviderReplaySemanticOutcomeV111[])outcomes.Clone();
    }
}

public static class SimulationProviderReplayCaptureProvenanceV111
{
    public const string CallerSupplied = "caller-supplied-unverified-v1";
    public const string ScriptedRunReplayValidated =
        "scripted-run-replay-validated-v1";
    public const string ScriptedRunSemanticIdentityReplayValidated =
        "scripted-run-semantic-identity-replay-validated-v1";
    public const string ScriptedActionReplayValidated =
        "scripted-action-replay-validated-v1";
    public const string PrototypeEngineActionReplayValidated =
        "prototype-engine-action-replay-validated-v1";
    public const string ScriptedActionSemanticIdentityReplayValidated =
        "scripted-action-semantic-identity-replay-validated-v1";
    public const string SemanticNativePolicyReplayValidated =
        "semantic-native-policy-replay-validated-v1";
    public const string SemanticNativePolicySemanticIdentityReplayValidated =
        "semantic-native-policy-semantic-identity-replay-validated-v2";
    public const string ReferenceNatureRunActionReplayValidated =
        "reference-nature-run-action-replay-validated-v1";
    public const string ReferenceNatureRunActionSemanticIdentityReplayValidated =
        "reference-nature-run-action-semantic-identity-replay-validated-v1";
    public const string ReferenceNatureEventChoiceSemanticIdentityReplayValidated =
        "reference-nature-event-choice-semantic-identity-replay-validated-v1";
    public const string ReferenceNatureWholeRunReplayValidated =
        "reference-nature-whole-run-replay-validated-v1";
    public const string ReferenceNatureWholeRunSemanticIdentityReplayValidated =
        "reference-nature-whole-run-semantic-identity-replay-validated-v1";
    public const string ReferenceNatureTreasureSemanticIdentityReplayValidated =
        "reference-nature-treasure-semantic-identity-replay-validated-v1";
    public const string ReferenceNatureNativeShovelSemanticIdentityReplayValidated =
        "reference-nature-native-shovel-semantic-identity-replay-validated-v1";
    public const string ReferenceNatureNativeEventGoldSemanticIdentityReplayValidated =
        "reference-nature-native-event-gold-semantic-identity-replay-validated-v1";
    public const string ReferenceNatureMerchantSemanticIdentityReplayValidated =
        "reference-nature-merchant-semantic-identity-replay-validated-v1";
    public const string ReferenceNatureNativeEventRelicSemanticIdentityReplayValidated =
        "reference-nature-native-event-relic-semantic-identity-replay-validated-v1";
    public const string ReferenceNatureTrialSemanticIdentityReplayValidated =
        "reference-nature-trial-semantic-identity-replay-validated-v1";
    public const string ReferenceNatureTrashHeapSemanticIdentityReplayValidated =
        "reference-nature-trash-heap-semantic-identity-replay-validated-v1";
    public const string ReferenceNatureTabletOfTruthSemanticIdentityReplayValidated =
        "reference-nature-tablet-of-truth-semantic-identity-replay-validated-v1";
    public const string ReferenceNatureInfestedAutomatonSemanticIdentityReplayValidated =
        "reference-nature-infested-automaton-semantic-identity-replay-validated-v1";
    public const string ReferenceNatureGloryReflectionsSemanticIdentityReplayValidated =
        "reference-nature-glory-reflections-semantic-identity-replay-validated-v1";
    public const string ReferenceNatureNativeEventShuffleSemanticIdentityReplayValidated =
        "reference-nature-native-event-shuffle-semantic-identity-replay-validated-v1";
    public const string ReferenceNatureEndlessConveyorSemanticIdentityReplayValidated =
        "reference-nature-endless-conveyor-semantic-identity-replay-validated-v1";
    public const string ReferenceNaturePunchOffFightSemanticIdentityReplayValidated =
        "reference-nature-punch-off-fight-semantic-identity-replay-validated-v1";
    public const string ReferenceNaturePunchOffRewardSemanticIdentityReplayValidated =
        "reference-nature-punch-off-reward-semantic-identity-replay-validated-v1";
    public const string ReferenceNatureNeowOfferSemanticIdentityReplayValidated =
        "reference-nature-neow-offer-semantic-identity-replay-validated-v1";
    public const string ReferenceNatureAncientOfferSemanticIdentityReplayValidated =
        "reference-nature-ancient-offer-semantic-identity-replay-validated-v1";
    public const string ReferenceNatureEventChoiceReplayValidated =
        "reference-nature-event-choice-replay-validated-v1";
    public const string ReferenceNatureControlledActionReplayValidated =
        "reference-nature-controlled-action-replay-validated-v1";
    public const string ReferenceNatureNeowOfferReplayValidated =
        "reference-nature-neow-offer-replay-validated-v1";
    public const string ReferenceNatureRunSetupReplayValidated =
        "reference-nature-run-setup-replay-validated-v1";
    public const string ReferenceNatureSemanticCombatReplayValidated =
        "reference-nature-semantic-combat-replay-validated-v1";
    public const string ReferenceNatureSemanticCombatOutcomeReplayValidated =
        "reference-nature-semantic-combat-outcome-replay-validated-v2";
    public const string ReferenceNatureSemanticIdentityReplayValidated =
        "reference-nature-semantic-identity-replay-validated-v3";
    public const string ReferenceNatureCombatRewardSemanticIdentityReplayValidated =
        "reference-nature-combat-reward-semantic-identity-replay-validated-v1";

    public static bool HasProviderIndependentSemanticIdentity(
        string? provenance) => provenance is
        SemanticNativePolicySemanticIdentityReplayValidated or
        ReferenceNatureRunActionSemanticIdentityReplayValidated or
        ReferenceNatureEventChoiceSemanticIdentityReplayValidated or
        ReferenceNatureWholeRunSemanticIdentityReplayValidated or
        ReferenceNatureTreasureSemanticIdentityReplayValidated or
        ReferenceNatureNativeShovelSemanticIdentityReplayValidated or
        ReferenceNatureNativeEventGoldSemanticIdentityReplayValidated or
        ReferenceNatureMerchantSemanticIdentityReplayValidated or
        ReferenceNatureNativeEventRelicSemanticIdentityReplayValidated or
        ReferenceNatureTrialSemanticIdentityReplayValidated or
        ReferenceNatureTrashHeapSemanticIdentityReplayValidated or
        ReferenceNatureTabletOfTruthSemanticIdentityReplayValidated or
        ReferenceNatureInfestedAutomatonSemanticIdentityReplayValidated or
        ReferenceNatureGloryReflectionsSemanticIdentityReplayValidated or
        ReferenceNatureNativeEventShuffleSemanticIdentityReplayValidated or
        ReferenceNatureEndlessConveyorSemanticIdentityReplayValidated or
        ReferenceNaturePunchOffFightSemanticIdentityReplayValidated or
        ReferenceNaturePunchOffRewardSemanticIdentityReplayValidated or
        ReferenceNatureNeowOfferSemanticIdentityReplayValidated or
        ReferenceNatureAncientOfferSemanticIdentityReplayValidated or
        ScriptedRunSemanticIdentityReplayValidated or
        ScriptedActionSemanticIdentityReplayValidated or
        ReferenceNatureSemanticIdentityReplayValidated or
        ReferenceNatureCombatRewardSemanticIdentityReplayValidated;

    public static bool IsKnown(string? provenance) =>
        HasProviderIndependentSemanticIdentity(provenance) || provenance is
        CallerSupplied or ScriptedRunReplayValidated or
        ScriptedActionReplayValidated or
        PrototypeEngineActionReplayValidated or
        SemanticNativePolicyReplayValidated or
        ReferenceNatureRunActionReplayValidated or
        ReferenceNatureWholeRunReplayValidated or
        ReferenceNatureEventChoiceReplayValidated or
        ReferenceNatureControlledActionReplayValidated or
        ReferenceNatureNeowOfferReplayValidated or
        ReferenceNatureRunSetupReplayValidated or
        ReferenceNatureSemanticCombatReplayValidated or
        ReferenceNatureSemanticCombatOutcomeReplayValidated;

    /// <summary>
    /// Whether this capture represents every primitive provider boundary in
    /// its interval. Semantic ReferenceNature selections are replayed as
    /// outcome identities and do not provide a primitive provider trace.
    /// Caller-supplied provenance remains an assertion by the caller.
    /// </summary>
    public static bool HasCompleteTypedBoundaryTrace(string? provenance) =>
        IsKnown(provenance) && provenance is not
            (ReferenceNatureSemanticCombatReplayValidated or
             ReferenceNatureSemanticCombatOutcomeReplayValidated or
             ReferenceNatureSemanticIdentityReplayValidated);

    public static bool HasCompleteSemanticOutcomeTrace(string? provenance) =>
        provenance == ReferenceNatureSemanticCombatOutcomeReplayValidated ||
        HasProviderIndependentSemanticIdentity(provenance);

    public static bool HasReplayValidatedDrawIdentityTrace(
        string? provenance) => IsKnown(provenance) &&
        provenance != CallerSupplied;
}

/// <summary>
/// Common replay-validated shape for ReferenceNature sessions that suspend
/// one ordinary run action on a typed controlled-stochastic prefix.
/// </summary>
public interface IReferenceNatureControlledActionSessionV111
{
    RunState Origin { get; }
    GameAction Action { get; }
    RunState Run { get; }
    bool Complete { get; }
    ControlledStochasticDecisionV111[] ProviderDecisions { get; }
    void Validate();
}

/// <summary>
/// Common replay-validated shape for a completed ReferenceNature run-setup
/// interval that has stochastic outcomes but no player action.
/// </summary>
public interface IReferenceNatureRunSetupSessionV111
{
    RunState Origin { get; }
    RunState Run { get; }
    bool Complete { get; }
    ControlledStochasticDecisionV111[] ProviderDecisions { get; }
    void Validate();
}

/// <summary>
/// Versioned, detached capture of one provider replay interval. Mechanics and
/// stochastic evidence are grouped through the shared simulation envelope.
/// </summary>
public sealed record SimulationProviderReplayCaptureV111(
    string SchemaId,
    SimulationStateV111<SimulationProviderReplayMechanicsCaptureV111,
        SimulationProviderReplayStochasticCaptureV111> Simulation)
{
    public const string PreviousSchemaId =
        "sts2-v111-simulation-provider-replay-capture-v9";
    public const string PreviousPreviousSchemaId =
        "sts2-v111-simulation-provider-replay-capture-v8";
    public const string PreviousPreviousPreviousSchemaId =
        "sts2-v111-simulation-provider-replay-capture-v7";
    public const string PreviousPreviousPreviousPreviousSchemaId =
        "sts2-v111-simulation-provider-replay-capture-v6";
    public const string PreviousPreviousPreviousPreviousPreviousSchemaId =
        "sts2-v111-simulation-provider-replay-capture-v5";
    public const string PreviousPreviousPreviousPreviousPreviousPreviousSchemaId =
        "sts2-v111-simulation-provider-replay-capture-v4";
    public const string PreviousPreviousPreviousPreviousPreviousPreviousPreviousSchemaId =
        "sts2-v111-simulation-provider-replay-capture-v3";
    public const string OlderSchemaId =
        "sts2-v111-simulation-provider-replay-capture-v2";
    public const string CurrentSchemaId =
        "sts2-v111-simulation-provider-replay-capture-v10";

    [System.Text.Json.Serialization.JsonInclude]
    public string CaptureProvenance { get; private init; } =
        SimulationProviderReplayCaptureProvenanceV111.CallerSupplied;

    public SimulationProviderReplayMechanicsCaptureV111 Mechanics =>
        Simulation.Mechanics;

    public SimulationProviderReplayStochasticCaptureV111 Stochastic =>
        Simulation.Stochastic;

    public static SimulationProviderReplayCaptureV111 Create(
        RunState origin, IReadOnlyList<GameAction> actions,
        IEnumerable<RandomBoundaryTranscriptEntryV111> boundaries,
        RunState result,
        IEnumerable<SimulationProviderReplaySemanticOutcomeV111>?
            semanticOutcomes = null)
        => Create(origin, actions, boundaries, result, semanticOutcomes,
            null);

    public static SimulationProviderReplayCaptureV111 Create(
        RunState origin, IReadOnlyList<GameAction> actions,
        IEnumerable<RandomBoundaryTranscriptEntryV111> boundaries,
        RunState result,
        IEnumerable<SimulationProviderReplaySemanticOutcomeV111>?
            semanticOutcomes,
        IEnumerable<RunState>? actionBoundaryStates,
        IEnumerable<long[]>? drawnCardInstanceIdsByAction = null,
        IEnumerable<NativeEngineJournalEntryV111[]>?
            combatEventHookTranscriptByAction = null)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(boundaries);
        ArgumentNullException.ThrowIfNull(result);
        var actionBoundaries = actionBoundaryStates?.ToArray();
        var capture = new SimulationProviderReplayCaptureV111(
            CurrentSchemaId,
            new SimulationStateV111<
                SimulationProviderReplayMechanicsCaptureV111,
                SimulationProviderReplayStochasticCaptureV111>(
                    new SimulationProviderReplayMechanicsCaptureV111(
                        origin, actions.ToArray(), result,
                        actionBoundaries,
                        drawnCardInstanceIdsByAction?.ToArray(),
                        combatEventHookTranscriptByAction?.ToArray(),
                        SimulationProviderReplayMechanicsCaptureV111
                            .CapturePlayerBoundaryViews(
                                origin, actionBoundaries)),
                    new SimulationProviderReplayStochasticCaptureV111(
                        boundaries.ToArray(),
                        semanticOutcomes?.ToArray())));
        capture.Validate();
        return capture;
    }

    internal static string NormalizeSemanticOwnerId(
        string siteId, string ownerId)
    {
        if (StochasticSiteRegistryV111.CombatRandomTargetSiteIds.Contains(
                siteId, StringComparer.Ordinal))
            return "combat_target";
        if (StochasticSiteRegistryV111.CombatCardPoolShuffleSiteIds.Contains(
                siteId, StringComparer.Ordinal) ||
            siteId == ControlledRecycleShuffleEngineV111.SiteId)
            return "combat_pool_shuffle";
        if (StochasticSiteRegistryV111.ControlledDrawInsertionSiteIds.Contains(
                siteId, StringComparer.Ordinal))
            return "combat_draw_insertion";
        if (StochasticSiteRegistryV111.CombatEnemyPileInsertionSiteIds
                .Contains(siteId, StringComparer.Ordinal))
            return "combat_draw_insertion";
        if (siteId is not (
                StochasticSiteRegistryV111.CombatAutoplayRandomCardSelectSiteId or
                StochasticSiteRegistryV111.CombatRandomReplayCardSelectSiteId or
                StochasticSiteRegistryV111.CombatGeneratedCardSelectSiteId or
                StochasticSiteRegistryV111.CombatRandomDrawAttackCardModelSiteId))
            return ownerId;
        var poolMarker = ownerId.LastIndexOf(":pool:",
            StringComparison.Ordinal);
        if (poolMarker < 0)
            return ownerId;
        if (poolMarker == 0 || poolMarker + ":pool:".Length >= ownerId.Length)
            throw new InvalidDataException(
                "Semantic owner has an invalid candidate-pool suffix.");
        return ownerId[..poolMarker];
    }

    /// <summary>
    /// Builds a capture from a completed scripted run. Session validation
    /// replays its committed actions from the origin before the existing
    /// boundary journal materializes the saved typed decisions.
    /// </summary>
    public static SimulationProviderReplayCaptureV111 FromScriptedRunSession(
        ScriptedRunSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        // Validation replays mechanics too. Keep its provider callbacks out
        // of any journal owned by the caller.
        using (RandomBoundaryJournalV111.Begin())
            session.Validate();
        if (session.ActiveAction is not null)
            throw new InvalidOperationException(
                "Provider replay capture requires a committed scripted-run boundary.");

        var completedActions = session.CompletedActions;
        var actions = completedActions
            .Select(action => action.Action).ToArray();
        var replay = ReplayScriptedActionBoundaries(
            session.Origin, actions,
            completedActions.Select(action => action.Decisions).ToArray(),
            session.Run);
        var capture = FromScriptedDecisions(
            session.Origin, actions,
            completedActions.SelectMany(action => action.Decisions),
            session.Run,
            SimulationProviderReplayCaptureProvenanceV111
                .ScriptedRunReplayValidated,
            replay.ActionBoundaryStates,
            replay.DrawnCardInstanceIdsByAction,
            replay.CombatEventHookTranscriptByAction);
        if (TryCaptureScriptedSemanticOutcomes(
                session.Origin,
                completedActions.Select(action =>
                    (action.Action, action.Decisions)).ToArray()) is not
            { } semanticOutcomes)
            return capture;
        return AttachSemanticOutcomes(capture, semanticOutcomes,
            SimulationProviderReplayCaptureProvenanceV111
                .ScriptedRunSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Builds a capture from a completed whole-run ReferenceNature session.
    /// The provider-boundary trace contains all six typed controlled
    /// decisions; the aligned optional Nature transcript remains in the
    /// replay-validated session.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureScriptedRunSession(
            ReferenceNatureScriptedRunSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        using (RandomBoundaryJournalV111.Begin())
            session.Validate();
        if (session.ActiveAction is not null)
            throw new InvalidOperationException(
                "Provider replay capture requires a committed ReferenceNature whole-run boundary.");

        var actions = session.CompletedActions;
        var replayRun = session.Origin.Fork();
        var nature = session.InitialNatureState.Fork();
        var actionBoundaries = new RunState[actions.Length];
        var drawnCardsByAction = new long[actions.Length][];
        var combatEventHookTranscripts =
            new NativeEngineJournalEntryV111[actions.Length][];
        var actionStartStates = new RunState[actions.Length];
        using (RandomBoundaryJournalV111.Begin())
        {
            for (var index = 0; index < actions.Length; index++)
            {
                using var drawJournal =
                    SemanticDrawIdentityJournalV111.Begin();
                using var eventJournal =
                    NativeEngineEventJournalV111.Begin();
                var action = actions[index];
                actionStartStates[index] = replayRun.Fork();
                var replayed = ScriptedRunActionSessionV111.ReplayCompleted(
                    replayRun, action.Action, action.Decisions);
                replayRun = replayed.Run?.Fork() ??
                    throw new InvalidDataException(
                        "ReferenceNature whole-run action replay has no result.");
                if (action.NatureDecisions.LastOrDefault(decision =>
                        decision is not null) is { } lastNatureDecision)
                    nature = lastNatureDecision.NextState.Fork();
                replayRun = ReferenceNatureRunCursorV111.Commit(
                    replayRun, nature);
                actionBoundaries[index] = replayRun.Fork();
                drawnCardsByAction[index] = drawJournal.CardInstanceIds;
                combatEventHookTranscripts[index] =
                    eventJournal.Entries.ToArray();
            }
        }
        if (CanonicalJson.Sha256(replayRun) !=
            CanonicalJson.Sha256(session.Run))
            throw new InvalidDataException(
                "ReferenceNature whole-run capture replay differs from its saved result.");

        var semanticOutcomes = new List<
            SimulationProviderReplaySemanticOutcomeV111>();
        var semanticOrdinalCounts = new Dictionary<string, int>(
            StringComparer.Ordinal);
        var semanticTraceComplete = true;
        var previousOutcomeCount = 0;
        using (RandomBoundaryJournalV111.Begin())
        using (SemanticDrawIdentityJournalV111.Begin())
        {
            for (var actionIndex = 0; actionIndex < actions.Length;
                actionIndex++)
            {
                var action = actions[actionIndex];
                var decisions = action.Decisions;
                var natureDecisions = action.NatureDecisions;
                var actionOutcomes = new List<
                    SimulationProviderReplaySemanticOutcomeV111>();
                var replayPrefix = ScriptedRunActionSessionV111.Begin(
                    actionStartStates[actionIndex], action.Action);
                for (var decisionIndex = 0;
                    decisionIndex < decisions.Length; decisionIndex++)
                {
                    var decision = decisions[decisionIndex];
                    var selection = natureDecisions[decisionIndex];
                    if (selection is not null)
                    {
                        var request = replayPrefix.PendingRequest ??
                            throw new InvalidDataException(
                                "ReferenceNature selection has no replayed provider request.");
                        var law = ReferenceNatureScriptedRunSessionV111
                            .QueryNatureLaw(replayPrefix, request,
                                natureDecisions[..decisionIndex]) ??
                            throw new InvalidDataException(
                                "ReferenceNature selection has no supported replayed Chance Law.");
                        law.Validate();
                        if (selection.EventId != law.EventId ||
                            selection.LawId != law.LawId ||
                            selection.OutcomeIndex >= law.SupportCount ||
                            selection.OutcomeId != law.OutcomeIdAt(
                                selection.OutcomeIndex))
                            throw new InvalidDataException(
                                "ReferenceNature whole-run outcome differs from its replayed Chance Law.");

                        var semanticSupportIds =
                            SimulationProviderReplaySemanticIdentityV111
                                .SupportIds(law, decision.CandidateLabels);
                        var semanticOrdinal = law.Ordinal;
                        if (SimulationProviderReplayNativeSemanticOutcomeTraceV111
                                .SemanticOrdinalGroup(law.SiteId) is
                            { } ordinalGroup)
                        {
                            semanticOrdinal = semanticOrdinalCounts
                                .GetValueOrDefault(ordinalGroup);
                            semanticOrdinalCounts[ordinalGroup] =
                                semanticOrdinal + 1;
                        }
                        actionOutcomes.Add(
                            new SimulationProviderReplaySemanticOutcomeV111(
                                actionIndex + previousOutcomeCount +
                                    actionOutcomes.Count + 1,
                                law.EventId, law.LawId,
                                law.OutcomeIdAt(selection.OutcomeIndex),
                                selection.OutcomeIndex)
                            {
                                SiteId = law.SiteId,
                                OperationKind = law.OperationKind,
                                OwnerId = NormalizeSemanticOwnerId(
                                    law.SiteId, law.OwnerId),
                                Ordinal = semanticOrdinal,
                                SupportFingerprint = CanonicalJson.Sha256(
                                    semanticSupportIds),
                                SemanticOutcomeId = semanticSupportIds[
                                    selection.OutcomeIndex]
                            });
                    }
                    else
                        semanticTraceComplete = false;

                    replayPrefix = replayPrefix.ResolveNext(
                        ScriptedRunActionOutcomeV111.FromDecision(
                            decision, decisionIndex + 1));
                }
                if (!replayPrefix.Complete)
                    throw new InvalidDataException(
                        "ReferenceNature whole-run semantic replay did not settle its action.");
                semanticOutcomes.AddRange(actionOutcomes);
                previousOutcomeCount += actionOutcomes.Count;
            }
        }

        var capture = FromScriptedDecisions(
            session.Origin, actions.Select(action => action.Action).ToArray(),
            actions.SelectMany(action => action.Decisions), session.Run,
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureWholeRunReplayValidated,
            actionBoundaries, drawnCardsByAction,
            combatEventHookTranscripts);
        if (!semanticTraceComplete)
        {
            var replayedSemanticOutcomes =
                TryCaptureScriptedSemanticOutcomes(
                    session.Origin,
                    actions.Select(action =>
                        (action.Action, action.Decisions)).ToArray(),
                    actionStartStates);
            if (replayedSemanticOutcomes is null)
                return capture;
            semanticOutcomes = replayedSemanticOutcomes.ToList();
        }

        capture = capture with
        {
            Simulation = capture.Simulation with
            {
                Stochastic = new SimulationProviderReplayStochasticCaptureV111(
                    capture.Stochastic.Boundaries,
                    semanticOutcomes.ToArray())
            },
            CaptureProvenance =
                SimulationProviderReplayCaptureProvenanceV111
                    .ReferenceNatureWholeRunSemanticIdentityReplayValidated
        };
        capture.Validate();
        return capture;
    }

    /// <summary>Builds a capture from one completed scripted player action.</summary>
    public static SimulationProviderReplayCaptureV111
        FromScriptedRunActionSession(
            ScriptedRunActionSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        using (RandomBoundaryJournalV111.Begin())
            session.Validate();
        if (!session.Complete || session.Run is null)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed scripted action.");
        var capture = FromReplayValidatedAction(
            session.Origin, session.Action, session.Decisions, session.Run,
            SimulationProviderReplayCaptureProvenanceV111
                .ScriptedActionReplayValidated);
        if (TryCaptureScriptedSemanticOutcomes(session.Origin,
                [(Action: session.Action,
                    Decisions: session.Decisions)]) is not
            { } semanticOutcomes)
            return capture;
        return AttachSemanticOutcomes(capture, semanticOutcomes,
            SimulationProviderReplayCaptureProvenanceV111
                .ScriptedActionSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Replays a sequence of legal actions through the prototype engine,
    /// records its typed provider and per-action mechanics journals, and
    /// verifies the final run against the supplied endpoint.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromPrototypeEngineReplayValidatedActions(
            RunState origin, IReadOnlyList<GameAction> actions,
            RunState expectedResult)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(expectedResult);
        if (actions.Count == 0)
            throw new ArgumentException(
                "Prototype engine replay requires at least one action.",
                nameof(actions));

        RunConfiguration.ValidateState(origin);
        RunConfiguration.ValidateState(expectedResult);
        var engine = new PrototypeGameEngine();
        var replay = origin.Fork();
        var resolvedActions = new GameAction[actions.Count];
        var actionBoundaries = new RunState[actions.Count];
        var drawnCardIds = new long[actions.Count][];
        var eventHookTranscripts =
            new NativeEngineJournalEntryV111[actions.Count][];
        RandomBoundaryTranscriptEntryV111[] boundaries;

        using (var providerJournal = RandomBoundaryJournalV111.Begin())
        {
            for (var index = 0; index < actions.Count; index++)
            {
                var action = actions[index] ?? throw new ArgumentException(
                    "Prototype engine replay contains a null action at " +
                    index + ".", nameof(actions));
                var actionId = PrototypeAiEnvironment.StableActionId(action);
                GameAction[] matches;
                using (RandomBoundaryJournalV111.Begin())
                    matches = engine.GetLegalActions(replay)
                        .Where(candidate =>
                            PrototypeAiEnvironment.StableActionId(candidate) ==
                            actionId)
                        .ToArray();
                if (matches.Length != 1)
                    throw new InvalidDataException(
                        "Prototype engine replay action " + index +
                        " does not identify exactly one legal action.");

                resolvedActions[index] = matches[0];
                using var drawJournal =
                    SemanticDrawIdentityJournalV111.Begin();
                using var eventJournal = NativeEngineEventJournalV111.Begin();
                replay = engine.Step(replay, matches[0]).State;
                actionBoundaries[index] = replay.Fork();
                drawnCardIds[index] = drawJournal.CardInstanceIds;
                eventHookTranscripts[index] =
                    eventJournal.Entries.ToArray();
            }

            boundaries = providerJournal.OrderedTranscript.ToArray();
        }

        if (CanonicalJson.Sha256(replay) !=
            CanonicalJson.Sha256(expectedResult))
            throw new InvalidDataException(
                "Prototype engine action replay differs from the supplied result snapshot.");

        var capture = Create(origin, resolvedActions, boundaries,
            expectedResult, semanticOutcomes: null, actionBoundaries,
            drawnCardIds, eventHookTranscripts) with
        {
            CaptureProvenance =
                SimulationProviderReplayCaptureProvenanceV111
                    .PrototypeEngineActionReplayValidated
        };
        capture.Validate();
        return capture;
    }

    /// <summary>
    /// Builds a capture from a completed ReferenceNature run action when its
    /// controlled action trace contains the complete typed provider prefix.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureRunActionSession(
            ReferenceNatureRunActionSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        using (RandomBoundaryJournalV111.Begin())
            session.Validate();
        if (!session.Complete || session.Run is null)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature run action.");
        var originHistory =
            session.Origin.Stochastic.ControlledRunActionHistory ?? [];
        var resultHistory =
            session.Run.Stochastic.ControlledRunActionHistory ?? [];
        if (resultHistory.Length < originHistory.Length)
            throw new InvalidDataException(
                "ReferenceNature run-action history lost its origin prefix.");
        for (var index = 0; index < originHistory.Length; index++)
            if (CanonicalJson.Sha256(originHistory[index]) !=
                CanonicalJson.Sha256(resultHistory[index]))
                throw new InvalidDataException(
                    "ReferenceNature run-action history changed before the captured action.");
        if (resultHistory.Length == originHistory.Length &&
            session.DecisionTranscript.Length != 0)
        {
            ControlledStochasticDecisionV111[]? transitionDecisions;
            using (RandomBoundaryJournalV111.Begin())
                transitionDecisions =
                    TryBuildConfiguredExperimentalActTransitionPrefix(
                        session);
            if (transitionDecisions is not null)
                return FromReferenceNatureRunAction(
                    session, transitionDecisions);
        }
        if (resultHistory.Length == originHistory.Length &&
            session.DecisionTranscript.Length == 0)
            return FromReplayValidatedAction(
                session.Origin, session.Action, [], session.Run,
                SimulationProviderReplayCaptureProvenanceV111
                    .ReferenceNatureRunActionReplayValidated);
        if (resultHistory.Length != originHistory.Length + 1)
            throw new NotSupportedException(
                "ReferenceNature action trace does not isolate one committed run action.");

        var trace = resultHistory[^1];
        trace.Validate();
        var mapNodeId = session.Action.Kind == "choose_map_node"
            ? session.Action.ReadPayload<ChooseMapNodePayload>().NodeId
            : null;
        var actionFingerprint = ControlledRunActionEngineV111
                .RequiresRunActionFingerprint(
                    session.Origin, session.Action)
            ? PrototypeAiEnvironment.StableActionId(session.Action)
            : null;
        if (trace.ActionId != session.Action.Kind ||
            trace.MapNodeId != mapNodeId ||
            trace.ActionFingerprint != actionFingerprint)
            throw new NotSupportedException(
                "ReferenceNature run-action trace does not identify its saved action.");

        var decisions = trace.TypedDecisions ?? trace.Decisions
            .Select((decision, index) => ControlledStochasticDecisionV111
                .FromIndex(decision, trace.CandidateLabels[index]))
            .ToArray();
        if (decisions.Length != session.DecisionTranscript.Length)
        {
            using (RandomBoundaryJournalV111.Begin())
                decisions = TryBuildConfiguredRunStartPrefix(
                    session, trace) ?? decisions;
        }
        if (decisions.Length != session.DecisionTranscript.Length)
            throw new NotSupportedException(
                "ReferenceNature run-action trace does not cover every sampled provider request.");
        return FromReferenceNatureRunAction(session, decisions);
    }

    private static SimulationProviderReplayCaptureV111
        FromReferenceNatureRunAction(
            ReferenceNatureRunActionSessionV111 session,
            ControlledStochasticDecisionV111[] providerDecisions)
    {
        SimulationProviderReplayCaptureV111 TypedCapture() =>
            FromReplayValidatedAction(
                session.Origin, session.Action, providerDecisions, session.Run,
                SimulationProviderReplayCaptureProvenanceV111
                    .ReferenceNatureRunActionReplayValidated);

        SimulationProviderReplayCaptureV111 TypedOrSemanticCapture()
        {
            var capture = TypedCapture();
            var semanticOutcomes = TryCaptureScriptedSemanticOutcomes(
                session.Origin,
                [(Action: session.Action, Decisions: providerDecisions)]);
            return semanticOutcomes is null
                ? capture
                : AttachSemanticOutcomes(capture, semanticOutcomes,
                    SimulationProviderReplayCaptureProvenanceV111
                        .ReferenceNatureRunActionSemanticIdentityReplayValidated);
        }

        var decisions = session.DecisionTranscript;
        if (providerDecisions.Length != decisions.Length ||
            decisions.Length == 0)
            return TypedOrSemanticCapture();

        var transcript = decisions.Select(selection =>
            new SimulationProviderReplaySemanticSelectionV111(
                selection.LawId, selection.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var laws = new ChanceLawV111[decisions.Length];
        var supported = true;
        try
        {
            using (RandomBoundaryJournalV111.Begin())
            {
                var replay = ReferenceNatureRunActionSessionV111.BeginForCapture(
                    session.Origin, session.Action,
                    session.InitialNatureState);
                for (var index = 0; index < decisions.Length; index++)
                {
                    var law = replay.PendingLaw;
                    if (law is null)
                    {
                        supported = false;
                        break;
                    }
                    law.Validate();
                    var selection = decisions[index];
                    if (selection is null ||
                        selection.LawId != law.LawId ||
                        selection.EventId != law.EventId ||
                        selection.OutcomeIndex < 0 ||
                        selection.OutcomeIndex >= law.SupportCount ||
                        selection.OutcomeId != law.OutcomeIdAt(
                            selection.OutcomeIndex))
                        throw new InvalidDataException(
                            "ReferenceNature run-action outcome differs from its replayed Chance Law.");
                    laws[index] = law;
                    replay = replay.ResolveNext();
                }
                if (supported &&
                    (replay.DecisionTranscript.Length != decisions.Length ||
                     !replay.DecisionTranscript.SequenceEqual(decisions) ||
                     CanonicalJson.Sha256(replay.Run) !=
                         CanonicalJson.Sha256(session.Run) ||
                     CanonicalJson.Sha256(replay.NatureState) !=
                         CanonicalJson.Sha256(session.NatureState)))
                    throw new InvalidDataException(
                        "ReferenceNature run-action semantic prefix replay differs from its saved result.");
            }
        }
        catch (NotSupportedException)
        {
            supported = false;
        }
        if (!supported)
            return TypedOrSemanticCapture();

        return FromReferenceNatureSemanticAction(
            session.Origin, session.Action, session.Run, providerDecisions,
            transcript, index => laws[index],
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureRunActionSemanticIdentityReplayValidated);
    }

    private static ControlledStochasticDecisionV111[]?
        TryBuildConfiguredRunStartPrefix(
            ReferenceNatureRunActionSessionV111 session,
            ControlledRunActionTraceV111 trace)
    {
        var initialization = session.Run.Stochastic
            .ReferenceNatureRunInitializationTranscript;
        var configuration = session.Origin.Configuration;
        if (session.Action.Kind != "start_run" || configuration is null ||
            configuration.Acts.Length == 0 || initialization is null)
            return null;
        initialization.Validate(replayMapGeneration: true);
        if (!initialization.DecisionTranscript.SequenceEqual(
                session.DecisionTranscript))
            return null;

        var overgrowth = configuration.Acts[0] == ActIdentity.Overgrowth;
        var bossLaw = overgrowth
            ? ChanceLawEngineV111.QueryOvergrowthBossEncounter(0)
            : configuration.Acts[0] == ActIdentity.Underdocks
                ? ChanceLawEngineV111.QueryUnderdocksBossEncounter(0)
                : null;
        if (bossLaw is null)
            return null;
        if (CanonicalJson.Sha256(initialization.ChanceLaws[0]) !=
            CanonicalJson.Sha256(bossLaw))
            return null;
        var expectedBossRequest = new RandomIndexRequestV111(
            bossLaw.SiteId, "combat", bossLaw.OwnerId, bossLaw.Ordinal,
            Enumerable.Range(0, bossLaw.Outcomes.Length).ToArray(),
            OperationKind: bossLaw.OperationKind);
        if (trace.Decisions.Length != 1 ||
            !ControlledStochasticChanceReplayV111.RequestsMatch(
                trace.Decisions[0].Request, expectedBossRequest) ||
            trace.Decisions[0].SelectedIndex !=
                initialization.DecisionTranscript[0].OutcomeIndex)
            return null;

        var prefix = new List<ControlledStochasticDecisionV111>
        {
            ControlledStochasticDecisionV111.FromIndex(
                trace.Decisions[0], trace.CandidateLabels[0])
        };
        var laterActInitialNatureState = ReferenceNatureV111
            .SampleValidated(bossLaw, initialization.InitialNatureState)
            .NextState;
        var prefixDecisionCount = 1;
        if (initialization.ActOneMapGeneration is { } mapGeneration)
        {
            if (!overgrowth)
                return null;
            var mapDecisions = mapGeneration.Decisions;
            prefix.AddRange(mapDecisions.Select(decision =>
                decision.Fork()));
            prefixDecisionCount = checked(prefixDecisionCount +
                mapDecisions.Length);
            laterActInitialNatureState = mapGeneration.NatureState;
        }

        if (initialization.LaterActPreparationInitialPrototypeRng is
                { } prototypeRng)
        {
            if (!configuration.UsesExperimentalSilentThreeActRoute)
                return null;
            var laterDecisionTranscript = initialization.DecisionTranscript
                .Skip(prefixDecisionCount).ToArray();
            var laterPreparation =
                ReferenceNatureLaterActPreparationSessionV111.Rebuild(
                    prototypeRng, session.Origin.Ascension,
                    configuration.Profile, laterActInitialNatureState,
                    laterDecisionTranscript);
            if (!laterPreparation.Complete ||
                !laterPreparation.DecisionTranscript.SequenceEqual(
                    laterDecisionTranscript))
                return null;
            var expectedLaterLaws = initialization.ChanceLaws
                .Skip(prefixDecisionCount).ToArray();
            if (CanonicalJson.Sha256(laterPreparation.ChanceLaws) !=
                CanonicalJson.Sha256(expectedLaterLaws))
                return null;
            prefix.AddRange(laterPreparation.ProviderDecisions.Select(
                decision => decision.Fork()));
        }
        return prefix.Count == session.DecisionTranscript.Length
            ? prefix.ToArray()
            : null;
    }

    private static ControlledStochasticDecisionV111[]?
        TryBuildConfiguredExperimentalActTransitionPrefix(
            ReferenceNatureRunActionSessionV111 session)
    {
        var configuration = session.Origin.Configuration;
        if (session.Action.Kind != "continue_act" ||
            configuration?.UsesExperimentalSilentThreeActRoute != true ||
            session.Origin.World is not { } originWorld)
            return null;

        var originMapHistory = session.Origin.Stochastic
                .ReferenceNatureMapGenerationHistory ?? [];
        var resultMapHistory = session.Run.Stochastic
                .ReferenceNatureMapGenerationHistory ?? [];
        if (resultMapHistory.Length != originMapHistory.Length + 1)
            return null;
        for (var index = 0; index < originMapHistory.Length; index++)
            if (CanonicalJson.Sha256(originMapHistory[index]) !=
                CanonicalJson.Sha256(resultMapHistory[index]))
                throw new InvalidDataException(
                    "Experimental transition changed its prior map audit history.");

        var mapAudit = resultMapHistory[^1];
        mapAudit.Validate();
        if (mapAudit.Seed != session.Origin.RunSeed ||
            mapAudit.Act != checked(originWorld.Act + 1) ||
            mapAudit.Ascension != session.Origin.Ascension ||
            !mapAudit.InitialNatureState.SameCursor(
                session.InitialNatureState))
            return null;

        var originAncientHistory = session.Origin.Stochastic
                .ReferenceNatureAncientOfferHistory ?? [];
        var resultAncientHistory = session.Run.Stochastic
                .ReferenceNatureAncientOfferHistory ?? [];
        if (resultAncientHistory.Length < originAncientHistory.Length ||
            resultAncientHistory.Length > originAncientHistory.Length + 1)
            return null;
        for (var index = 0; index < originAncientHistory.Length; index++)
            if (CanonicalJson.Sha256(originAncientHistory[index]) !=
                CanonicalJson.Sha256(resultAncientHistory[index]))
                throw new InvalidDataException(
                    "Experimental transition changed its prior Ancient-offer audit history.");

        if (session.LegacyNativeAncientOfferCompletion)
        {
            if (resultAncientHistory.Length != originAncientHistory.Length ||
                !mapAudit.DecisionTranscript.SequenceEqual(
                    session.DecisionTranscript))
                return null;

            var nativeAncientDecisions =
                RebuildLegacyNativeAncientProviderDecisions(
                    session, mapAudit);
            return mapAudit.Decisions.Select(decision => decision.Fork())
                .Concat(nativeAncientDecisions.Select(decision =>
                    ControlledStochasticDecisionV111.FromIndex(decision)))
                .ToArray();
        }

        ReferenceNatureDecisionV111[] ancientTranscript = [];
        ControlledStochasticDecisionV111[] ancientProviderDecisions = [];
        if (resultAncientHistory.Length == originAncientHistory.Length + 1)
        {
            var ancientOffer = ReferenceNatureAncientOfferSessionV111.Restore(
                resultAncientHistory[^1]);
            if (!ancientOffer.Complete ||
                !ancientOffer.InitialNatureState.SameCursor(
                    mapAudit.NatureState))
                return null;
            ancientTranscript = ancientOffer.DecisionTranscript;
            ancientProviderDecisions = ancientOffer.ProviderDecisions;
        }

        var reconstructedTranscript = mapAudit.DecisionTranscript
            .Concat(ancientTranscript).ToArray();
        if (!reconstructedTranscript.SequenceEqual(
                session.DecisionTranscript))
            return null;

        var decisions = mapAudit.Decisions.Select(decision =>
                decision.Fork())
            .Concat(ancientProviderDecisions.Select(decision =>
                decision.Fork()))
            .ToArray();
        return decisions.Length == session.DecisionTranscript.Length
            ? decisions
            : null;
    }

    private static RandomIndexDecisionV111[]
        RebuildLegacyNativeAncientProviderDecisions(
            ReferenceNatureRunActionSessionV111 session,
            ReferenceNatureMapGenerationAuditV111 mapAudit)
    {
        var mapGeneration = ReferenceNatureMapGenerationSessionV111.Begin(
            session.Origin.RunSeed, mapAudit.Act, mapAudit.Ascension,
            mapAudit.InitialNatureState);
        foreach (var expected in mapAudit.DecisionTranscript)
        {
            if (mapGeneration.Complete)
                throw new InvalidDataException(
                    "Legacy Native Ancient map transcript exceeds its generated map.");
            mapGeneration = mapGeneration.ResolveNext();
            if (!mapGeneration.DecisionTranscript[^1].Equals(expected))
                throw new InvalidDataException(
                    "Legacy Native Ancient map transcript does not replay.");
        }
        if (!mapGeneration.Complete ||
            CanonicalJson.Sha256(mapGeneration.ToAudit()) !=
                CanonicalJson.Sha256(mapAudit))
            throw new InvalidDataException(
                "Legacy Native Ancient map audit does not replay exactly.");

        var map = mapGeneration.Map ?? throw new InvalidDataException(
            "Legacy Native Ancient map replay has no generated map.");
        var engine = new PrototypeGameEngine();
        var transition = engine.BeginActTransitionWithMap(
            session.Origin, session.Action, map);
        var assignedAncientId = transition.World?.Stochastic
            .LaterActEncounterBag?.AssignedAncientId;
        if (assignedAncientId is null)
            throw new InvalidDataException(
                "Legacy Native Ancient transition has no assigned Ancient.");

        var provider = new NativeRandomIndexProviderV111(
            recordToJournal: false);
        var nativeOpening = ExperimentalSilentThreeActRouteV111
            .BeginAssignedAncientOpeningWithAudit(transition, provider);
        var nativeResult = nativeOpening.State;
        var decisions = nativeOpening.Decisions;

        foreach (var decision in decisions)
        {
            decision.Validate();
            if (decision.ProviderMode != "native-v111")
                throw new InvalidDataException(
                    "Legacy Native Ancient audit returned a non-Native decision.");
        }
        var withMapAudit = nativeResult with
        {
            Stochastic = nativeResult.Stochastic
                .AppendReferenceNatureMapGenerationAudit(mapAudit)
        };
        var replayedRun = ReferenceNatureRunCursorV111.Commit(
            withMapAudit, session.NatureState);
        if (CanonicalJson.Sha256(replayedRun) !=
            CanonicalJson.Sha256(session.Run))
            throw new InvalidDataException(
                "Audited Native Ancient replay differs from its saved run.");
        return decisions;
    }

    /// <summary>
    /// Builds a capture from a completed ReferenceNature event-choice
    /// session. The initiating event choice and subsequent player actions are
    /// replayed with their interleaved Nature decisions to retain each settled
    /// mechanics boundary.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureEventChoiceSession(
            ReferenceNatureControlledEventChoiceSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNatureControlledEventChoiceSessionV111 replay;
        using (RandomBoundaryJournalV111.Begin())
            replay = ReferenceNatureControlledEventChoiceSessionV111.Restore(
                ReferenceNatureControlledEventChoiceSessionV111.Save(session));
        if (!replay.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature event choice.");

        var originHistory =
            replay.Origin.Stochastic.ControlledEventChoiceHistory ?? [];
        var resultHistory =
            replay.Run.Stochastic.ControlledEventChoiceHistory ?? [];
        if (resultHistory.Length < originHistory.Length)
            throw new InvalidDataException(
                "ReferenceNature event-choice history lost its origin prefix.");
        for (var index = 0; index < originHistory.Length; index++)
            if (CanonicalJson.Sha256(originHistory[index]) !=
                CanonicalJson.Sha256(resultHistory[index]))
                throw new InvalidDataException(
                    "ReferenceNature event-choice history changed before the captured choice.");
        var natureSteps = replay.Steps
            .Where(step => step.Kind == "nature")
            .Select(step => step.NatureDecision ?? throw new InvalidDataException(
                "ReferenceNature event-choice step has no Nature decision."))
            .ToArray();
        ControlledStochasticDecisionV111[] decisions;
        if (natureSteps.Length == 0)
        {
            if (resultHistory.Length != originHistory.Length)
                throw new NotSupportedException(
                    "ReferenceNature event choice appended an unpaired stochastic trace.");
            decisions = [];
        }
        else
        {
            if (resultHistory.Length != originHistory.Length + 1)
                throw new NotSupportedException(
                    "ReferenceNature event-choice trace does not isolate one committed choice.");

            var trace = resultHistory[^1];
            trace.Validate();
            if (trace.EventId != replay.Origin.World?.Event?.EventId ||
                trace.ChoiceId != replay.ChoiceId ||
                trace.Decisions.Length != natureSteps.Length)
                throw new NotSupportedException(
                    "ReferenceNature event-choice trace does not cover its Nature chronology.");
            for (var index = 0; index < natureSteps.Length; index++)
                if (trace.Decisions[index].SelectedIndex !=
                    natureSteps[index].OutcomeIndex)
                    throw new InvalidDataException(
                        "ReferenceNature event-choice outcomes differ from the typed action trace.");

            decisions = trace.Decisions.Select((decision, index) =>
                ControlledStochasticDecisionV111.FromIndex(
                    decision, trace.CandidateLabels[index])).ToArray();
        }

        var actions = new List<GameAction>
        {
            GameAction.Create("event_choice",
                new EventChoicePayload(replay.ChoiceId))
        };
        var decisionsByAction = new List<
            List<ControlledStochasticDecisionV111>> { new() };
        var decisionCursor = 0;
        foreach (var step in replay.Steps)
        {
            if (step.Kind == "nature")
            {
                if (decisionCursor >= decisions.Length)
                    throw new InvalidDataException(
                        "ReferenceNature event-choice chronology exceeds its typed decision trace.");
                decisionsByAction[^1].Add(decisions[decisionCursor++]);
                continue;
            }

            var playerAction = step.PlayerAction ??
                throw new InvalidDataException(
                    "ReferenceNature event-choice step has no player action.");
            actions.Add(playerAction);
            decisionsByAction.Add([]);
        }
        if (decisionCursor != decisions.Length)
            throw new InvalidDataException(
                "ReferenceNature event-choice typed decision trace exceeds its chronology.");
        var actionValues = actions.ToArray();
        var typedDecisionsByAction = decisionsByAction
            .Select(action => action.ToArray()).ToArray();
        var actionReplay = ReplayScriptedActionBoundaries(
            replay.Origin, actionValues, typedDecisionsByAction,
            replay.Run);
        var capture = FromScriptedDecisions(
            replay.Origin, actionValues, decisions, replay.Run,
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureEventChoiceReplayValidated,
            actionReplay.ActionBoundaryStates,
            actionReplay.DrawnCardInstanceIdsByAction,
            actionReplay.CombatEventHookTranscriptByAction);
        if (natureSteps.Length == 0)
            return capture;

        var laws = new ChanceLawV111[natureSteps.Length];
        var semanticTranscript = new SimulationProviderReplaySemanticSelectionV111[
            natureSteps.Length];
        using (RandomBoundaryJournalV111.Begin())
        {
            var prefix = ReferenceNatureControlledEventChoiceSessionV111
                .BeginForCapture(replay.Origin, replay.ChoiceId,
                    replay.InitialNatureState);
            var natureIndex = 0;
            foreach (var step in replay.Steps)
            {
                if (step.Kind == "nature")
                {
                    var law = ChanceLawEngineV111
                        .QueryControlledEventChoice(prefix.Run);
                    law.Validate();
                    var selection = step.NatureDecision ??
                        throw new InvalidDataException(
                            "ReferenceNature event-choice Nature step has no selection.");
                    if (selection.LawId != law.LawId ||
                        selection.EventId != law.EventId ||
                        selection.OutcomeIndex < 0 ||
                        selection.OutcomeIndex >= law.SupportCount ||
                        selection.OutcomeId != law.OutcomeIdAt(
                            selection.OutcomeIndex))
                        throw new InvalidDataException(
                            "ReferenceNature event-choice outcome differs from its replayed Chance Law.");
                    laws[natureIndex] = law;
                    semanticTranscript[natureIndex] = new(
                        selection.LawId, selection.EventId,
                        selection.OutcomeId, selection.OutcomeIndex);
                    prefix = prefix.ResolveNext();
                    natureIndex++;
                }
                else
                {
                    prefix = prefix.AdvancePlayerAction(
                        step.PlayerAction ?? throw new InvalidDataException(
                            "ReferenceNature event-choice player step has no action."));
                }
            }
            if (natureIndex != natureSteps.Length || !prefix.Complete ||
                CanonicalJson.Sha256(prefix.Steps) !=
                    CanonicalJson.Sha256(replay.Steps) ||
                CanonicalJson.Sha256(prefix.Run) !=
                    CanonicalJson.Sha256(replay.Run) ||
                CanonicalJson.Sha256(prefix.NatureState) !=
                    CanonicalJson.Sha256(replay.NatureState))
                throw new InvalidDataException(
                    "ReferenceNature event-choice semantic replay differs from its saved chronology.");
        }

        var semanticOutcomes = BuildReferenceNatureSemanticOutcomes(
            decisions, semanticTranscript, index => laws[index]);
        return AttachSemanticOutcomes(
            capture, semanticOutcomes,
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureEventChoiceSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Builds a capture for the run-setup interval owned by a completed Neow
    /// offer session. The interval has no player actions; its request plan is
    /// reconstructed from the validated selected prefix.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureNeowOfferSession(
            ReferenceNatureNeowOfferSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNatureNeowOfferSessionV111 validated;
        using (RandomBoundaryJournalV111.Begin())
            validated = ReferenceNatureNeowOfferSessionV111.Restore(
                ReferenceNatureNeowOfferSessionV111.Save(session));
        if (!validated.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature Neow-offer session.");

        var providerDecisions = validated.ProviderDecisions;
        var transcript = validated.DecisionTranscript.Select(selection =>
            new SimulationProviderReplaySemanticSelectionV111(
                selection.LawId, selection.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        return FromReferenceNatureSemanticRunSetup(
            validated.Origin, validated.Run, providerDecisions, transcript,
            index =>
            {
                var priorIndices = providerDecisions.Take(index).Select(
                    decision => decision.IndexDecision?.SelectedIndex ??
                        throw new InvalidDataException(
                            "ReferenceNature Neow-offer prefix has no index outcome."))
                    .ToArray();
                return ChanceLawEngineV111.QueryNeowOffer(
                    validated.Origin.RunSeed!, validated.Origin.Ascension,
                    index, priorIndices);
            },
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureNeowOfferSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Builds a capture for a completed Ancient-offer run-setup interval.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureAncientOfferSession(
            ReferenceNatureAncientOfferSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNatureAncientOfferSessionV111 validated;
        using (RandomBoundaryJournalV111.Begin())
            validated = ReferenceNatureAncientOfferSessionV111.Restore(
                ReferenceNatureAncientOfferSessionV111.Save(session));
        if (!validated.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature Ancient-offer session.");

        var providerDecisions = validated.ProviderDecisions;
        var transcript = validated.DecisionTranscript.Select(selection =>
            new SimulationProviderReplaySemanticSelectionV111(
                selection.LawId, selection.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        return FromReferenceNatureSemanticRunSetup(
            validated.Origin, validated.Run, providerDecisions, transcript,
            index => ChanceLawEngineV111.QueryAncientOfferSessionStep(
                validated.Origin, validated.SiteId,
                validated.DecisionTranscript.Take(index).ToArray()),
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureAncientOfferSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Builds a capture for a completed no-player-action ReferenceNature
    /// setup interval from its revalidated typed stochastic prefix.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureRunSetupSession(
            IReferenceNatureRunSetupSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        bool complete;
        RunState origin;
        RunState run;
        ControlledStochasticDecisionV111[] decisions;
        using (RandomBoundaryJournalV111.Begin())
        {
            session.Validate();
            complete = session.Complete;
            origin = session.Origin.Fork();
            run = session.Run.Fork();
            decisions = complete ? session.ProviderDecisions : [];
        }
        if (!complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature run-setup session.");
        return FromScriptedDecisions(
            origin, [], decisions, run,
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureRunSetupReplayValidated,
            [], []);
    }

    /// <summary>
    /// Builds a capture from a completed ReferenceNature session that owns
    /// one player action and its replay-validated typed stochastic prefix.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureControlledActionSession(
            IReferenceNatureControlledActionSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        bool complete;
        ControlledStochasticDecisionV111[] decisions;
        using (RandomBoundaryJournalV111.Begin())
        {
            session.Validate();
            complete = session.Complete;
            decisions = complete ? session.ProviderDecisions : [];
        }
        if (!complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature controlled action.");
        var capture = FromReplayValidatedAction(
            session.Origin, session.Action, decisions, session.Run,
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureControlledActionReplayValidated);
        var semanticOutcomes = TryCaptureScriptedSemanticOutcomes(
            session.Origin,
            [(Action: session.Action, Decisions: decisions)]);
        return semanticOutcomes is null
            ? capture
            : AttachSemanticOutcomes(capture, semanticOutcomes,
                SimulationProviderReplayCaptureProvenanceV111
                    .ReferenceNatureSemanticIdentityReplayValidated);
    }

    private static SimulationProviderReplayCaptureV111
        FromReferenceNatureSemanticAction(
            RunState origin, GameAction action, RunState result,
            ControlledStochasticDecisionV111[] providerDecisions,
            SimulationProviderReplaySemanticSelectionV111[] transcript,
            Func<int, ChanceLawV111> queryLaw,
            string semanticProvenance)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(providerDecisions);
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentNullException.ThrowIfNull(queryLaw);
        var semanticOutcomes = BuildReferenceNatureSemanticOutcomes(
            providerDecisions, transcript, queryLaw);
        var capture = FromReplayValidatedAction(
            origin, action, providerDecisions, result,
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureControlledActionReplayValidated);
        return AttachSemanticOutcomes(
            capture, semanticOutcomes, semanticProvenance);
    }

    private static SimulationProviderReplayCaptureV111
        FromReferenceNatureSemanticRunSetup(
            RunState origin, RunState result,
            ControlledStochasticDecisionV111[] providerDecisions,
            SimulationProviderReplaySemanticSelectionV111[] transcript,
            Func<int, ChanceLawV111> queryLaw,
            string semanticProvenance)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(providerDecisions);
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentNullException.ThrowIfNull(queryLaw);
        var semanticOutcomes = BuildReferenceNatureSemanticOutcomes(
            providerDecisions, transcript, queryLaw);
        var capture = FromScriptedDecisions(
            origin, [], providerDecisions, result,
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureRunSetupReplayValidated,
            [], []);
        return AttachSemanticOutcomes(
            capture, semanticOutcomes, semanticProvenance);
    }

    private static SimulationProviderReplaySemanticOutcomeV111[]
        BuildReferenceNatureSemanticOutcomes(
            ControlledStochasticDecisionV111[] providerDecisions,
            SimulationProviderReplaySemanticSelectionV111[] transcript,
            Func<int, ChanceLawV111> queryLaw)
    {
        if (providerDecisions.Length != transcript.Length)
            throw new InvalidDataException(
                "ReferenceNature decisions and outcomes have different lengths.");

        var semanticOutcomes = new SimulationProviderReplaySemanticOutcomeV111[
            transcript.Length];
        var ordinalCounts = new Dictionary<string, int>(
            StringComparer.Ordinal);
        for (var index = 0; index < transcript.Length; index++)
        {
            var decision = providerDecisions[index];
            var law = queryLaw(index);
            law.Validate();
            var selection = transcript[index];
            if (selection is null ||
                selection.LawId != law.LawId ||
                selection.EventId != law.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >= law.SupportCount ||
                selection.OutcomeId != law.OutcomeIdAt(
                    selection.OutcomeIndex))
                throw new InvalidDataException(
                    "ReferenceNature action outcome differs from its replayed Chance Law.");

            var semanticSupportIds =
                SimulationProviderReplaySemanticIdentityV111.SupportIds(
                    law, decision.CandidateLabels);
            var semanticOrdinal = law.Ordinal;
            if (SimulationProviderReplayNativeSemanticOutcomeTraceV111
                    .SemanticOrdinalGroup(law.SiteId) is
                { } ordinalGroup)
            {
                semanticOrdinal = ordinalCounts.GetValueOrDefault(
                    ordinalGroup);
                ordinalCounts[ordinalGroup] = semanticOrdinal + 1;
            }
            semanticOutcomes[index] = new(
                index + 1, law.EventId, law.LawId,
                law.OutcomeIdAt(selection.OutcomeIndex),
                selection.OutcomeIndex)
            {
                SiteId = law.SiteId,
                OperationKind = law.OperationKind,
                OwnerId = NormalizeSemanticOwnerId(law.SiteId,
                    law.OwnerId),
                Ordinal = semanticOrdinal,
                SupportFingerprint = CanonicalJson.Sha256(
                    semanticSupportIds),
                SemanticOutcomeId = semanticSupportIds[
                    selection.OutcomeIndex]
            };
        }
        return semanticOutcomes;
    }

    private static SimulationProviderReplayCaptureV111 AttachSemanticOutcomes(
        SimulationProviderReplayCaptureV111 capture,
        SimulationProviderReplaySemanticOutcomeV111[] semanticOutcomes,
        string semanticProvenance)
    {
        capture = capture with
        {
            Simulation = capture.Simulation with
            {
                Stochastic = new SimulationProviderReplayStochasticCaptureV111(
                    capture.Stochastic.Boundaries, semanticOutcomes)
            },
            CaptureProvenance = semanticProvenance
        };
        capture.Validate();
        return capture;
    }

    /// <summary>
    /// Captures a completed Native Treasure action with both its typed
    /// provider boundaries and the validated rarity/relic identity outcomes.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureTreasureSession(
            ReferenceNatureTreasureSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNatureTreasureSessionV111 validated;
        using (RandomBoundaryJournalV111.Begin())
            validated = ReferenceNatureTreasureSessionV111.Restore(
                ReferenceNatureTreasureSessionV111.Save(session));
        if (!validated.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature Treasure session.");

        var providerDecisions = validated.ProviderDecisions;
        var transcript = validated.DecisionTranscript.Select(selection =>
            new SimulationProviderReplaySemanticSelectionV111(
                selection.LawId, selection.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        return FromReferenceNatureSemanticAction(
            validated.Origin, validated.Action, validated.Run,
            providerDecisions, transcript, index =>
            {
                var decision = providerDecisions[index];
                return ReferenceNatureTreasureSessionV111.QueryLawForCapture(
                    validated.Origin, decision.ToRequest(),
                    decision.CandidateLabels);
            },
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureTreasureSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Captures a completed Native Shovel action with its typed provider
    /// boundaries and replay-validated relic rarity/identity outcomes.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureNativeShovelSession(
            ReferenceNatureNativeShovelSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNatureNativeShovelSessionV111 validated;
        using (RandomBoundaryJournalV111.Begin())
            validated = ReferenceNatureNativeShovelSessionV111.Restore(
                ReferenceNatureNativeShovelSessionV111.Save(session));
        if (!validated.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature Native Shovel session.");

        var providerDecisions = validated.ProviderDecisions;
        var transcript = validated.DecisionTranscript.Select(selection =>
            new SimulationProviderReplaySemanticSelectionV111(
                selection.LawId, selection.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        return FromReferenceNatureSemanticAction(
            validated.Origin, validated.Action, validated.Run,
            providerDecisions, transcript, index =>
            {
                var decision = providerDecisions[index];
                return ReferenceNatureNativeShovelSessionV111
                    .QueryLawForCapture(validated.Origin, validated.Action,
                        decision.ToRequest(), decision.CandidateLabels,
                        providerDecisions.Take(index).ToArray());
            },
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureNativeShovelSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Captures a completed Native Event-entry action with its typed provider
    /// boundaries and replay-validated event outcomes.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureNativeEventGoldSession(
            ReferenceNatureNativeEventGoldSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNatureNativeEventGoldSessionV111 validated;
        using (RandomBoundaryJournalV111.Begin())
            validated = ReferenceNatureNativeEventGoldSessionV111.Restore(
                ReferenceNatureNativeEventGoldSessionV111.Save(session));
        if (!validated.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature Native Event-entry session.");

        var providerDecisions = validated.ProviderDecisions;
        var transcript = validated.DecisionTranscript.Select(selection =>
            new SimulationProviderReplaySemanticSelectionV111(
                selection.LawId, selection.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        return FromReferenceNatureSemanticAction(
            validated.Origin, validated.Action, validated.Run,
            providerDecisions, transcript, index =>
            {
                var decision = providerDecisions[index];
                return ReferenceNatureNativeEventGoldSessionV111
                    .QueryLawForCapture(validated.Origin,
                        validated.ActionId, validated.EventId,
                        decision.ToRequest());
            },
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureNativeEventGoldSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Captures a completed ReferenceNature Merchant action with semantic
    /// identities for its replay-validated offer and purchase decisions.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureMerchantSession(
            ReferenceNatureMerchantSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNatureMerchantSessionV111 validated;
        using (RandomBoundaryJournalV111.Begin())
            validated = ReferenceNatureMerchantSessionV111.Restore(
                ReferenceNatureMerchantSessionV111.Save(session));
        if (!validated.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature Merchant session.");

        var providerDecisions = validated.ProviderDecisions;
        var transcript = validated.DecisionTranscript.Select(selection =>
            new SimulationProviderReplaySemanticSelectionV111(
                selection.LawId, selection.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        return FromReferenceNatureSemanticAction(
            validated.Origin, validated.Action, validated.Run,
            providerDecisions, transcript, index =>
            {
                var decision = providerDecisions[index];
                return ReferenceNatureMerchantSessionV111.QueryLawForCapture(
                    validated.Origin, validated.Action,
                    decision.ToRequest(), decision.CandidateLabels,
                    providerDecisions.Take(index).ToArray());
            },
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureMerchantSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Captures a completed Native event-relic action with typed provider
    /// boundaries and replay-validated rarity/relic identity outcomes.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureNativeEventRelicSession(
            ReferenceNatureNativeEventRelicSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNatureNativeEventRelicSessionV111 validated;
        using (RandomBoundaryJournalV111.Begin())
            validated = ReferenceNatureNativeEventRelicSessionV111.Restore(
                ReferenceNatureNativeEventRelicSessionV111.Save(session));
        if (!validated.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature Native event-relic session.");

        var providerDecisions = validated.ProviderDecisions;
        var transcript = validated.DecisionTranscript.Select(selection =>
            new SimulationProviderReplaySemanticSelectionV111(
                selection.LawId, selection.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        return FromReferenceNatureSemanticAction(
            validated.Origin, validated.Action, validated.Run,
            providerDecisions, transcript, index =>
            {
                var decision = providerDecisions[index];
                return ReferenceNatureNativeEventRelicSessionV111
                    .QueryLawForCapture(validated.Origin, validated.Action,
                        decision.ToRequest(), decision.CandidateLabels,
                        providerDecisions.Take(index).ToArray());
            },
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureNativeEventRelicSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Captures a completed Glory Trial choice with its typed boundary and
    /// replay-validated case identity.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureTrialSession(ReferenceNatureTrialSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNatureTrialSessionV111 validated;
        using (RandomBoundaryJournalV111.Begin())
            validated = ReferenceNatureTrialSessionV111.Restore(
                ReferenceNatureTrialSessionV111.Save(session));
        if (!validated.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature Trial session.");

        var providerDecisions = validated.ProviderDecisions;
        var transcript = validated.DecisionTranscript.Select(selection =>
            new SimulationProviderReplaySemanticSelectionV111(
                selection.LawId, selection.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        return FromReferenceNatureSemanticAction(
            validated.Origin, validated.Action, validated.Run,
            providerDecisions, transcript,
            _ => ChanceLawEngineV111.QueryTrial(validated.Origin),
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureTrialSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Captures a completed Trash Heap reward choice with its typed boundary
    /// and replay-validated reward identity.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureTrashHeapSession(
            ReferenceNatureTrashHeapSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNatureTrashHeapSessionV111 validated;
        using (RandomBoundaryJournalV111.Begin())
            validated = ReferenceNatureTrashHeapSessionV111.Restore(
                ReferenceNatureTrashHeapSessionV111.Save(session));
        if (!validated.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature Trash Heap session.");

        var providerDecisions = validated.ProviderDecisions;
        var transcript = validated.DecisionTranscript.Select(selection =>
            new SimulationProviderReplaySemanticSelectionV111(
                selection.LawId, selection.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        return FromReferenceNatureSemanticAction(
            validated.Origin, validated.Action, validated.Run,
            providerDecisions, transcript, index =>
            {
                var request = providerDecisions[index].ToRequest()
                    .IndexRequest ?? throw new InvalidDataException(
                        "ReferenceNature Trash Heap decision has no index request.");
                return ChanceLawEngineV111.QueryTrashHeap(
                    validated.Origin, request.SiteId);
            },
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureTrashHeapSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Captures a completed Tablet of Truth page choice with its typed
    /// boundary and replay-validated physical card identity.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureTabletOfTruthSession(
            ReferenceNatureTabletOfTruthSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNatureTabletOfTruthSessionV111 validated;
        using (RandomBoundaryJournalV111.Begin())
            validated = ReferenceNatureTabletOfTruthSessionV111.Restore(
                ReferenceNatureTabletOfTruthSessionV111.Save(session));
        if (!validated.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature Tablet of Truth session.");

        var providerDecisions = validated.ProviderDecisions;
        var transcript = validated.DecisionTranscript.Select(selection =>
            new SimulationProviderReplaySemanticSelectionV111(
                selection.LawId, selection.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        return FromReferenceNatureSemanticAction(
            validated.Origin, validated.Action, validated.Run,
            providerDecisions, transcript,
            _ => ChanceLawEngineV111.QueryTabletOfTruth(validated.Origin),
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureTabletOfTruthSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Captures an Infested Automaton choice with its typed boundary and
    /// replay-validated selected card identity.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureInfestedAutomatonSession(
            ReferenceNatureInfestedAutomatonSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNatureInfestedAutomatonSessionV111 validated;
        using (RandomBoundaryJournalV111.Begin())
            validated = ReferenceNatureInfestedAutomatonSessionV111.Restore(
                ReferenceNatureInfestedAutomatonSessionV111.Save(session));
        if (!validated.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature Infested Automaton session.");

        var providerDecisions = validated.ProviderDecisions;
        var transcript = validated.DecisionTranscript.Select(selection =>
            new SimulationProviderReplaySemanticSelectionV111(
                selection.LawId, selection.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        return FromReferenceNatureSemanticAction(
            validated.Origin, validated.Action, validated.Run,
            providerDecisions, transcript,
            _ => ChanceLawEngineV111.QueryNativeEventCardPick(
                validated.Origin,
                PrototypeNativeHiveEvents.InfestedAutomatonCardOfferSiteId,
                0, actionId: validated.ChoiceId),
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureInfestedAutomatonSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Captures completed Glory Reflections with each conditioned card
    /// identity in its ordered downgrade/upgrade prefix.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureGloryReflectionsSession(
            ReferenceNatureGloryReflectionsSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNatureGloryReflectionsSessionV111 validated;
        using (RandomBoundaryJournalV111.Begin())
            validated = ReferenceNatureGloryReflectionsSessionV111.Restore(
                ReferenceNatureGloryReflectionsSessionV111.Save(session));
        if (!validated.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature Glory Reflections session.");

        var providerDecisions = validated.ProviderDecisions;
        var transcript = validated.DecisionTranscript.Select(selection =>
            new SimulationProviderReplaySemanticSelectionV111(
                selection.LawId, selection.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        return FromReferenceNatureSemanticAction(
            validated.Origin, validated.Action, validated.Run,
            providerDecisions, transcript, index =>
            {
                var request = providerDecisions[index].IndexDecision?.Request ??
                    throw new InvalidDataException(
                        "ReferenceNature Glory Reflections decision has no index request.");
                var priorIndices = providerDecisions.Take(index).Select(
                    decision => decision.IndexDecision?.SelectedIndex ??
                        throw new InvalidDataException(
                            "ReferenceNature Glory Reflections prefix has no index outcome."))
                    .ToArray();
                return ChanceLawEngineV111.QueryNativeEventCardPick(
                    validated.Origin, request.SiteId, index, priorIndices,
                    "touch_mirror");
            },
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureGloryReflectionsSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Captures a completed Native event-shuffle action with the semantic
    /// identity of each outcome in its conditioned request prefix.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureNativeEventShuffleSession(
            ReferenceNatureNativeEventShuffleSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNatureNativeEventShuffleSessionV111 validated;
        using (RandomBoundaryJournalV111.Begin())
            validated = ReferenceNatureNativeEventShuffleSessionV111.Restore(
                ReferenceNatureNativeEventShuffleSessionV111.Save(session));
        if (!validated.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature Native event-shuffle session.");

        var providerDecisions = validated.ProviderDecisions;
        var transcript = validated.DecisionTranscript.Select(selection =>
            new SimulationProviderReplaySemanticSelectionV111(
                selection.LawId, selection.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var plan = NativeEventShufflePlanBuilderV111.Build(
            validated.Origin, validated.SiteId, validated.ActionId,
            validated.PlannedCandidateIds);
        return FromReferenceNatureSemanticAction(
            validated.Origin, validated.Action, validated.Run,
            providerDecisions, transcript, index =>
            {
                var indexDecision = providerDecisions[index].IndexDecision ??
                    throw new InvalidDataException(
                        "ReferenceNature Native event-shuffle decision has no index outcome.");
                if (plan.IsCombatEndIndex)
                    return ReferenceNatureNativeEventShuffleSessionV111
                        .QueryCombatEndLawForCapture(plan,
                            indexDecision.Request);
                var priorIndices = providerDecisions.Take(index).Select(
                    decision => decision.IndexDecision?.SelectedIndex ??
                        throw new InvalidDataException(
                            "ReferenceNature Native event-shuffle prefix has no index outcome."))
                    .ToArray();
                return ReferenceNatureNativeEventShuffleSessionV111
                    .QueryBoundaryLawForCapture(plan, validated.Origin,
                        index, priorIndices, validated.ActionId,
                        validated.PlannedCandidateIds);
            },
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureNativeEventShuffleSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Captures an Endless Conveyor action with semantic outcomes for its
    /// ordered dish, potion, and card boundaries.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureEndlessConveyorSession(
            ReferenceNatureEndlessConveyorSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNatureEndlessConveyorSessionV111 validated;
        using (RandomBoundaryJournalV111.Begin())
            validated = ReferenceNatureEndlessConveyorSessionV111.Restore(
                ReferenceNatureEndlessConveyorSessionV111.Save(session));
        if (!validated.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature Endless Conveyor session.");

        var providerDecisions = validated.ProviderDecisions;
        var transcript = validated.DecisionTranscript.Select(selection =>
            new SimulationProviderReplaySemanticSelectionV111(
                selection.LawId, selection.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        return FromReferenceNatureSemanticAction(
            validated.Origin, validated.Action, validated.Run,
            providerDecisions, transcript, index =>
            {
                var request = providerDecisions[index].ToRequest();
                return ReferenceNatureEndlessConveyorSessionV111
                    .QueryLawForCapture(validated.Origin, validated.ActionId,
                        request.SiteId, providerDecisions.Take(index).ToArray(),
                        request.Ordinal);
            },
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureEndlessConveyorSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Captures Punch Off fight-entry HP offsets as semantic outcomes beside
    /// their typed request boundaries.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNaturePunchOffFightSession(
            ReferenceNaturePunchOffFightSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNaturePunchOffFightSessionV111 validated;
        using (RandomBoundaryJournalV111.Begin())
            validated = ReferenceNaturePunchOffFightSessionV111.Restore(
                ReferenceNaturePunchOffFightSessionV111.Save(session));
        if (!validated.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature Punch Off fight session.");

        var providerDecisions = validated.ProviderDecisions;
        var transcript = validated.DecisionTranscript.Select(selection =>
            new SimulationProviderReplaySemanticSelectionV111(
                selection.LawId, selection.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        return FromReferenceNatureSemanticAction(
            validated.Origin, validated.Action, validated.Run,
            providerDecisions, transcript, index =>
            {
                var request = providerDecisions[index].IndexDecision?.Request ??
                    throw new InvalidDataException(
                        "ReferenceNature Punch Off fight decision has no index request.");
                var priorIndices = providerDecisions.Take(index).Select(
                    decision => decision.IndexDecision?.SelectedIndex ??
                        throw new InvalidDataException(
                            "ReferenceNature Punch Off fight prefix has no index outcome."))
                    .ToArray();
                return ChanceLawEngineV111.QueryNativePunchOffFight(
                    validated.Origin, request.SiteId, index, priorIndices,
                    validated.ActionId);
            },
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNaturePunchOffFightSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Captures Punch Off's conditional relic and potion reward identities
    /// beside their typed request boundaries.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNaturePunchOffRewardSession(
            ReferenceNaturePunchOffRewardSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNaturePunchOffRewardSessionV111 validated;
        using (RandomBoundaryJournalV111.Begin())
            validated = ReferenceNaturePunchOffRewardSessionV111.Restore(
                ReferenceNaturePunchOffRewardSessionV111.Save(session));
        if (!validated.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature Punch Off reward session.");

        var providerDecisions = validated.ProviderDecisions;
        var transcript = validated.DecisionTranscript.Select(selection =>
            new SimulationProviderReplaySemanticSelectionV111(
                selection.LawId, selection.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        return FromReferenceNatureSemanticAction(
            validated.Origin, validated.Action, validated.Run,
            providerDecisions, transcript, index =>
            {
                var request = providerDecisions[index].IndexDecision?.Request ??
                    throw new InvalidDataException(
                        "ReferenceNature Punch Off reward decision has no index request.");
                var priorIndices = providerDecisions.Take(index).Select(
                    decision => decision.IndexDecision?.SelectedIndex ??
                        throw new InvalidDataException(
                            "ReferenceNature Punch Off reward prefix has no index outcome."))
                    .ToArray();
                return ChanceLawEngineV111.QueryNativePunchOffReward(
                    validated.Origin, request.SiteId, request.Ordinal,
                    priorIndices, validated.ActionId);
            },
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNaturePunchOffRewardSemanticIdentityReplayValidated);
    }

    /// <summary>
    /// Captures a completed combat victory and its atomic ReferenceNature
    /// reward transcript as provider-independent semantic outcomes.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureCombatRewardSession(
            ReferenceNatureCombatRewardSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        using var validationJournal = RandomBoundaryJournalV111.Begin();
        var validated = ReferenceNatureCombatRewardSessionV111.Restore(
            ReferenceNatureCombatRewardSessionV111.Save(session));
        if (!validated.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature combat reward session.");

        var providerDecisions = validated.Stochastic.ProviderDecisions;
        var transcript = validated.Stochastic.DecisionTranscript;
        if (providerDecisions.Length != transcript.Length)
            throw new InvalidDataException(
                "ReferenceNature combat reward decisions and outcomes have different lengths.");

        var semanticOutcomes = new SimulationProviderReplaySemanticOutcomeV111[
            transcript.Length];
        var rewardSemanticOrdinalCounts = new Dictionary<string, int>(
            StringComparer.Ordinal);
        for (var index = 0; index < transcript.Length; index++)
        {
            var decision = providerDecisions[index];
            var request = decision.ToRequest();
            var law = ReferenceNatureCombatRewardSessionV111
                .QueryLawForCapture(validated.Origin, validated.Action,
                    request, decision.CandidateLabels,
                    providerDecisions.Take(index).ToArray());
            var selection = transcript[index];
            if (selection.LawId != law.LawId ||
                selection.EventId != law.EventId ||
                selection.OutcomeIndex >= law.SupportCount ||
                selection.OutcomeId != law.OutcomeIdAt(
                    selection.OutcomeIndex))
                throw new InvalidDataException(
                    "ReferenceNature combat reward outcome differs from its replayed Chance Law.");

            var semanticSupportIds =
                SimulationProviderReplaySemanticIdentityV111.SupportIds(
                    law, decision.CandidateLabels);
            var semanticOrdinal = rewardSemanticOrdinalCounts
                .GetValueOrDefault(law.SiteId);
            rewardSemanticOrdinalCounts[law.SiteId] = semanticOrdinal + 1;
            semanticOutcomes[index] = new(
                index + 1, law.EventId, law.LawId, selection.OutcomeId,
                selection.OutcomeIndex)
            {
                SiteId = law.SiteId,
                OperationKind = law.OperationKind,
                OwnerId = SimulationProviderReplayCaptureV111
                    .NormalizeSemanticOwnerId(law.SiteId, law.OwnerId),
                Ordinal = semanticOrdinal,
                SupportFingerprint = CanonicalJson.Sha256(
                    semanticSupportIds),
                SemanticOutcomeId = semanticSupportIds[
                    selection.OutcomeIndex]
            };
        }

        var typedCapture = FromReplayValidatedAction(
            validated.Origin, validated.Action, providerDecisions,
            validated.Run,
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureControlledActionReplayValidated);
        var capture = typedCapture with
        {
            Simulation = typedCapture.Simulation with
            {
                Stochastic = new SimulationProviderReplayStochasticCaptureV111(
                    typedCapture.Stochastic.Boundaries, semanticOutcomes)
            },
            CaptureProvenance =
                SimulationProviderReplayCaptureProvenanceV111
                    .ReferenceNatureCombatRewardSemanticIdentityReplayValidated
        };
        capture.Validate();
        return capture;
    }

    /// <summary>
    /// Builds a capture for the player-action interval of a validated Native
    /// policy baseline. The baseline's setup and initial-intent trace is used
    /// only to locate the beginning of the interval; it is excluded from the
    /// capture because the origin is the resulting initial player boundary.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromSemanticNativePolicySession(SemanticPilotCombatV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        // Restore and action replay invoke provider callbacks. Keep them out
        // of any journal owned by the caller.
        using var validationJournal = RandomBoundaryJournalV111.Begin();
        var validated = SemanticPilotCombatEngineV111.Restore(
            SemanticPilotCombatEngineV111.Save(session));
        var entry = validated.Stochastic.SeededEntry;
        if (entry is not { NativePolicyBaseline: true } ||
            !validated.UsesNativePolicyBaseline)
            throw new InvalidOperationException(
                "Provider replay capture requires a seeded Native policy baseline.");

        var finalAudit = validated.Stochastic.NativePolicyAudit ??
            throw new InvalidDataException(
                "Native policy session is missing its provider trace.");
        var replay = SemanticPilotCombatEngineV111.BeginSeeded(entry);
        var initialAudit = replay.Stochastic.NativePolicyAudit ??
            throw new InvalidDataException(
                "Native policy entry replay is missing its provider trace.");
        var initialTrace = initialAudit.ProviderTrace;
        var completeTrace = finalAudit.ProviderTrace;
        var initial = replay;
        if (initialTrace.Length > completeTrace.Length)
            throw new InvalidDataException(
                "Native policy entry trace is longer than the saved session trace.");
        for (var index = 0; index < initialTrace.Length; index++)
            if (CanonicalJson.Sha256(initialTrace[index]) !=
                CanonicalJson.Sha256(completeTrace[index]))
                throw new InvalidDataException(
                    "Native policy entry trace is not a prefix of the saved session trace.");

        var environment = new PrototypeAiEnvironment();
        var actions = new List<GameAction>(validated.History.Length);
        var actionStartStates = new List<RunState>(
            validated.History.Length);
        var actionBoundaryStates = new List<RunState>(
            validated.History.Length);
        var drawnCardsByAction = new List<long[]>(
            validated.History.Length);
        var combatEventHookTranscripts = new List<
            NativeEngineJournalEntryV111[]>(validated.History.Length);
        var actionTraceRanges = new List<
            SimulationProviderReplayNativeSemanticOutcomeTraceV111.ActionRange>(
                validated.History.Length);
        foreach (var transition in validated.History)
        {
            if (transition.Kind != "player" ||
                string.IsNullOrWhiteSpace(transition.ActionId))
                throw new NotSupportedException(
                    "Native policy capture requires a player-action-only history.");
            var resolvedAction = environment.ResolvePlayerAction(
                replay.Run, transition.ActionId);
            actions.Add(resolvedAction.Action);
            actionStartStates.Add(replay.Run);
            var beforeAudit = replay.Stochastic.NativePolicyAudit ??
                throw new InvalidDataException(
                    "Native policy replay lost its provider trace before an action.");
            var beforeDrawCount =
                SimulationProviderReplayNativeSemanticOutcomeTraceV111
                    .GetCardsDrawnCount(replay.Run);
            using var drawJournal =
                SemanticDrawIdentityJournalV111.Begin();
            using var eventJournal =
                NativeEngineEventJournalV111.Begin();
            replay = SemanticPilotCombatEngineV111
                .StepResolvedNativePolicyBaseline(
                    replay, replay.Fingerprint, transition.ActionId,
                    resolvedAction);
            if (!IsCommittedSemanticCombatBoundary(replay))
                throw new NotSupportedException(
                    "Native policy capture requires every action replay to settle at a committed boundary.");
            actionBoundaryStates.Add(replay.Run);
            drawnCardsByAction.Add(drawJournal.CardInstanceIds);
            combatEventHookTranscripts.Add(
                eventJournal.Entries.ToArray());
            var afterAudit = replay.Stochastic.NativePolicyAudit ??
                throw new InvalidDataException(
                    "Native policy replay lost its provider trace after an action.");
            var afterDrawCount =
                SimulationProviderReplayNativeSemanticOutcomeTraceV111
                    .GetCardsDrawnCount(replay.Run);
            actionTraceRanges.Add(
                new SimulationProviderReplayNativeSemanticOutcomeTraceV111
                    .ActionRange(actions.Count - 1,
                        beforeAudit.ProviderTrace.Length,
                        afterAudit.ProviderTrace.Length,
                        beforeDrawCount is { } before &&
                        afterDrawCount is { } after
                            ? after - before : null));
        }
        if (CanonicalJson.Sha256(replay) !=
            CanonicalJson.Sha256(validated))
            throw new InvalidDataException(
                "Native policy action replay differs from the saved session.");

        var boundaries = completeTrace.Skip(initialTrace.Length)
            .Select((boundary, index) => boundary with
            {
                Sequence = index + 1
            }).ToArray();
        var semanticOutcomes =
            SimulationProviderReplayNativeSemanticOutcomeTraceV111.TryCapture(
                finalAudit, completeTrace, initialTrace.Length,
                actionTraceRanges, drawnCardsByAction,
                actionStartStates);
        var capture = Create(initial.Run, actions, boundaries, validated.Run,
            semanticOutcomes ?? [], actionBoundaryStates,
            drawnCardsByAction, combatEventHookTranscripts) with
        {
            CaptureProvenance = semanticOutcomes is null
                ? SimulationProviderReplayCaptureProvenanceV111
                    .SemanticNativePolicyReplayValidated
                : SimulationProviderReplayCaptureProvenanceV111
                    .SemanticNativePolicySemanticIdentityReplayValidated
        };
        capture.Validate();
        return capture;
    }

    /// <summary>
    /// Captures a replay-validated semantic ReferenceNature combat interval.
    /// Player actions are recovered from the mixed action/outcome history;
    /// Chance Law selections remain semantic outcomes and are not rewritten
    /// as primitive provider draws.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureSemanticCombatSession(
            ReferenceNatureSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        // Restore replays the mechanics history and validates every saved
        // Nature selection against its law. Keep replay callbacks out of any
        // journal owned by the caller.
        using var validationJournal = RandomBoundaryJournalV111.Begin();
        var validated = ReferenceNatureSessionV111.Restore(
            ReferenceNatureSessionV111.Save(session));
        if (validated.Combat.NativePolicyAudit is not null)
            throw new InvalidOperationException(
                "Semantic ReferenceNature capture does not accept Native-policy audit sessions.");
        var historyIndices = validated.Stochastic.DecisionHistoryIndices;
        if (historyIndices is null || historyIndices.Any(index => index is null))
            throw new NotSupportedException(
                "Semantic ReferenceNature capture requires a fully indexed Nature transcript.");
        var initialHistoryLength =
            validated.Stochastic.InitialHistoryLength ??
            throw new InvalidDataException(
                "Semantic ReferenceNature session is missing its initial history boundary.");
        var origin = SemanticPilotCombatEngineV111.ReplayHistoryPrefix(
            validated.Combat, initialHistoryLength);
        if (!IsCommittedSemanticCombatBoundary(origin))
            throw new NotSupportedException(
                "Semantic ReferenceNature capture requires a committed origin boundary.");

        var replay = origin;
        var environment = new PrototypeAiEnvironment();
        var history = validated.Combat.History;
        var decisions = validated.Stochastic.DecisionTranscript;
        var semanticOutcomesByAction = new List<
            List<SimulationProviderReplaySemanticOutcomeV111>>(
                decisions.Length);
        var actions = new List<GameAction>(history.Length - initialHistoryLength);
        var actionBoundaryStates = new List<RunState>(
            history.Length - initialHistoryLength);
        var drawnCardsByAction = new List<long[]>(
            history.Length - initialHistoryLength);
        var combatEventHookTranscripts = new List<
            NativeEngineJournalEntryV111[]>(
                history.Length - initialHistoryLength);
        NativeEngineEventJournalV111? activeEventJournal = null;

        void FinishActionEventJournal()
        {
            if (activeEventJournal is null)
                return;
            activeEventJournal.Dispose();
            combatEventHookTranscripts.Add(
                activeEventJournal.Entries.ToArray());
            activeEventJournal = null;
        }

        var decisionIndex = 0;
        try
        {
            for (var historyIndex = initialHistoryLength;
                historyIndex < history.Length; historyIndex++)
            {
                var transition = history[historyIndex];
                if (transition.Kind == "player")
                {
                    FinishActionEventJournal();
                    if (actions.Count > actionBoundaryStates.Count)
                    {
                        if (!IsCommittedSemanticCombatBoundary(replay))
                            throw new NotSupportedException(
                                "Semantic ReferenceNature capture requires each action continuation to settle at a committed boundary.");
                        actionBoundaryStates.Add(replay.Run);
                    }
                    if (string.IsNullOrWhiteSpace(transition.ActionId))
                        throw new InvalidDataException(
                            "Semantic ReferenceNature player history is missing its action ID.");
                    actions.Add(environment.ResolvePlayerAction(
                        replay.Run, transition.ActionId).Action);
                    semanticOutcomesByAction.Add([]);
                    drawnCardsByAction.Add([]);
                    activeEventJournal =
                        NativeEngineEventJournalV111.Begin();
                }
                else if (actions.Count == 0)
                    throw new InvalidDataException(
                        "Semantic ReferenceNature outcome precedes its first captured player action.");

                if (decisionIndex < decisions.Length &&
                    historyIndices[decisionIndex] == historyIndex)
                {
                    if (!IsPendingSemanticDraw(replay))
                        semanticOutcomesByAction[^1].Add(
                            CaptureReferenceNatureOutcome(
                                replay, decisions[decisionIndex], 0));
                    decisionIndex++;
                }
                using var drawJournal =
                    SemanticDrawIdentityJournalV111.Begin();
                replay = SemanticPilotCombatEngineV111.ReplayHistoryTransition(
                    replay, transition);
                drawnCardsByAction[^1] = drawnCardsByAction[^1]
                    .Concat(drawJournal.CardInstanceIds).ToArray();
            }
            FinishActionEventJournal();
        }
        finally
        {
            activeEventJournal?.Dispose();
        }
        if (actions.Count > actionBoundaryStates.Count)
        {
            if (!IsCommittedSemanticCombatBoundary(replay))
                throw new NotSupportedException(
                    "Semantic ReferenceNature capture requires each action continuation to settle at a committed boundary.");
            actionBoundaryStates.Add(replay.Run);
        }
        if (decisionIndex != decisions.Length)
            throw new InvalidDataException(
                "Semantic ReferenceNature capture did not consume its indexed outcome transcript.");
        if (CanonicalJson.Sha256(replay) !=
            CanonicalJson.Sha256(validated.Combat))
            throw new InvalidDataException(
                "Semantic ReferenceNature capture replay differs from the saved session.");
        if (!IsCommittedSemanticCombatBoundary(validated.Combat))
            throw new NotSupportedException(
                "Semantic ReferenceNature capture requires a committed result boundary.");

        var semanticOutcomes = new List<
            SimulationProviderReplaySemanticOutcomeV111>();
        var previousOutcomeCount = 0;
        for (var actionIndex = 0;
            actionIndex < semanticOutcomesByAction.Count; actionIndex++)
        {
            var actionOutcomes = semanticOutcomesByAction[actionIndex];
            for (var eventIndex = 0; eventIndex < actionOutcomes.Count;
                eventIndex++)
                semanticOutcomes.Add(actionOutcomes[eventIndex] with
                {
                    TransitionIndex = actionIndex + previousOutcomeCount +
                        eventIndex + 1
                });
            previousOutcomeCount += actionOutcomes.Count;
        }
        var capture = Create(origin.Run, actions, [], validated.Combat.Run,
            semanticOutcomes, actionBoundaryStates,
            drawnCardsByAction, combatEventHookTranscripts) with
        {
            CaptureProvenance =
                SimulationProviderReplayCaptureProvenanceV111
                    .ReferenceNatureSemanticIdentityReplayValidated
        };
        capture.Validate();
        return capture;
    }

    private static bool IsCommittedSemanticCombatBoundary(
        SemanticPilotCombatV111 combat) => combat.AtPlayerBoundary ||
        combat.AtPlayerChoiceBoundary || combat.CombatTerminal;

    private static bool IsPendingSemanticDraw(
        SemanticPilotCombatV111 combat) =>
        combat.Run.World?.Combat?.Stochastic.SemanticDeck?.Pending is
            { Purpose: "draw", SiteId: "combat.draw_pile.draw_card" };

    private static SimulationProviderReplaySemanticOutcomeV111
        CaptureReferenceNatureOutcome(
            SemanticPilotCombatV111 combat,
            ReferenceNatureSelectionV111 selection,
            int transitionIndex)
    {
        ChanceLawV111 law;
        string siteId;
        string operationKind;
        string ownerId;
        int ordinal;
        string[] semanticSupportIds;
        string semanticOutcomeId;
        if (combat.IntentPending is not null)
        {
            var intentEvent = SemanticPilotCombatEngineV111
                .DescribePendingIntentEvent(combat);
            law = ChanceLawEngineV111.QueryIntent(intentEvent);
            siteId = intentEvent.SiteId;
            operationKind = intentEvent.OperationKind;
            ownerId = intentEvent.OwnerId;
            ordinal = intentEvent.Ordinal;
            semanticSupportIds = intentEvent.Support.Candidates
                .Select(candidate => candidate.IntentId).ToArray();
            semanticOutcomeId = selection.OutcomeId;
        }
        else
        {
            var pending = SemanticPilotCombatEngineV111
                .DescribePendingIndexChanceEvent(combat) ??
                throw new InvalidDataException(
                    "Indexed ReferenceNature outcome has no pending semantic event.");
            law = SemanticChanceLawDispatchV111.QueryPendingIndexLaw(
                combat, pending);
            siteId = pending.Event.SiteId;
            operationKind = pending.Event.OperationKind;
            ownerId = pending.Event.OwnerId;
            ordinal = pending.Event.Ordinal;
            semanticSupportIds = pending.SemanticOutcomeIds;
            if (selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >= pending.SemanticOutcomeIds.Length)
                throw new InvalidDataException(
                    "ReferenceNature outcome index is outside the semantic event support.");
            semanticOutcomeId = pending.SemanticOutcomeIds[
                selection.OutcomeIndex];
        }
        ownerId = NormalizeSemanticOwnerId(siteId, ownerId);

        if (selection.EventId != law.EventId ||
            selection.LawId != law.LawId ||
            selection.OutcomeIndex < 0 ||
            selection.OutcomeIndex >= law.SupportCount ||
            selection.OutcomeId != law.OutcomeIdAt(selection.OutcomeIndex))
            throw new InvalidDataException(
                "ReferenceNature outcome differs from its replayed Chance Law.");

        return new SimulationProviderReplaySemanticOutcomeV111(
            transitionIndex, selection.EventId, selection.LawId,
            selection.OutcomeId, selection.OutcomeIndex)
        {
            SiteId = siteId,
            OperationKind = operationKind,
            OwnerId = ownerId,
            Ordinal = ordinal,
            SupportFingerprint = CanonicalJson.Sha256(semanticSupportIds),
            SemanticOutcomeId = semanticOutcomeId
        };
    }

    private static SimulationProviderReplayCaptureV111 FromScriptedDecisions(
        RunState origin, IReadOnlyList<GameAction> actions,
        IEnumerable<ControlledStochasticDecisionV111> decisions,
        RunState result, string captureProvenance,
        IEnumerable<RunState>? actionBoundaryStates = null,
        IEnumerable<long[]>? drawnCardInstanceIdsByAction = null,
        IEnumerable<NativeEngineJournalEntryV111[]>?
            combatEventHookTranscriptByAction = null)
    {
        using var journal = RandomBoundaryJournalV111.Begin();
        foreach (var decision in decisions)
        {
            decision.Validate();
            if (decision.IndexDecision is { } index)
                RandomBoundaryJournalV111.Record(index);
            else if (decision.UnitFloatDecision is { } unitFloat)
                RandomBoundaryJournalV111.Record(unitFloat);
            else if (decision.RangedFloatDecision is { } rangedFloat)
                RandomBoundaryJournalV111.Record(rangedFloat);
            else if (decision.RangedDoubleDecision is { } rangedDouble)
                RandomBoundaryJournalV111.Record(rangedDouble);
            else if (decision.GaussianIntDecision is { } gaussianInt)
                RandomBoundaryJournalV111.Record(gaussianInt);
            else
                RandomBoundaryJournalV111.Record(
                    decision.GaussianDoubleDecision ??
                    throw new InvalidDataException(
                        "Scripted run history contains an empty typed decision."));
        }

        var boundaries = journal.OrderedTranscript;
        var capture = Create(origin, actions, boundaries, result, null,
            actionBoundaryStates, drawnCardInstanceIdsByAction,
            combatEventHookTranscriptByAction ??
                (actions.Count == 0
                    ? Array.Empty<NativeEngineJournalEntryV111[]>()
                    : null)) with
        {
            CaptureProvenance = captureProvenance
        };
        capture.Validate();
        return capture;
    }

    private static SimulationProviderReplaySemanticOutcomeV111[]?
        TryCaptureScriptedSemanticOutcomes(
            RunState origin,
            IReadOnlyList<(GameAction Action,
                ControlledStochasticDecisionV111[] Decisions)> actionGroups,
            IReadOnlyList<RunState>? actionOrigins = null)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(actionGroups);
        if (actionOrigins is not null &&
            actionOrigins.Count != actionGroups.Count)
            throw new ArgumentException(
                "Scripted semantic action origins must align with their action groups.",
                nameof(actionOrigins));
        var outcomes = new List<
            SimulationProviderReplaySemanticOutcomeV111>();
        var ordinalCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var replayRun = origin.Fork();
        using (RandomBoundaryJournalV111.Begin())
        {
            for (var actionIndex = 0;
                actionIndex < actionGroups.Count; actionIndex++)
            {
                var (action, decisions) = actionGroups[actionIndex];
                var replay = ScriptedRunActionSessionV111.Begin(
                    actionOrigins?[actionIndex] ?? replayRun, action);
                var natureOutcomePrefix = new List<string?>();
                for (var decisionIndex = 0;
                    decisionIndex < decisions.Length; decisionIndex++)
                {
                    var decision = decisions[decisionIndex];
                    var request = replay.PendingRequest;
                    if (request is null ||
                        CanonicalJson.Sha256(request) !=
                        CanonicalJson.Sha256(decision.ToRequest()))
                        throw new InvalidDataException(
                            "Scripted semantic replay differs from its saved provider request.");

                    ChanceLawV111? law;
                    try
                    {
                        law = ReferenceNatureScriptedRunSessionV111
                            .QueryNatureLaw(replay, request,
                                natureOutcomePrefix.ToArray());
                        law?.Validate();
                    }
                    catch (Exception exception) when (
                        exception is InvalidDataException or
                        NotSupportedException or
                        InvalidOperationException)
                    {
                        return null;
                    }
                    if (law is null)
                        return null;

                    var outcomeIndex =
                        ReferenceNatureScriptedRunSessionV111
                            .TryFindScriptedNatureOutcomeIndex(
                                replay, decision, law);
                    if (outcomeIndex is not { } selectedIndex)
                        return null;

                    var candidateLabels = decision.CandidateLabels ??
                        replay.PendingCandidateLabels;
                    if (candidateLabels is not null &&
                        candidateLabels.Length != law.SupportCount)
                        return null;
                    var semanticSupportIds =
                        SimulationProviderReplaySemanticIdentityV111
                            .SupportIds(law, candidateLabels);
                    var semanticOrdinal = law.Ordinal;
                    if (SimulationProviderReplayNativeSemanticOutcomeTraceV111
                            .SemanticOrdinalGroup(law.SiteId) is
                        { } ordinalGroup)
                    {
                        semanticOrdinal = ordinalCounts
                            .GetValueOrDefault(ordinalGroup);
                        ordinalCounts[ordinalGroup] = semanticOrdinal + 1;
                    }
                    outcomes.Add(
                        new SimulationProviderReplaySemanticOutcomeV111(
                            outcomes.Count + 1, law.EventId, law.LawId,
                            law.OutcomeIdAt(selectedIndex), selectedIndex)
                        {
                            SiteId = law.SiteId,
                            OperationKind = law.OperationKind,
                            OwnerId = NormalizeSemanticOwnerId(
                                law.SiteId, law.OwnerId),
                            Ordinal = semanticOrdinal,
                            SupportFingerprint = CanonicalJson.Sha256(
                                semanticSupportIds),
                            SemanticOutcomeId =
                                semanticSupportIds[selectedIndex]
                        });
                    natureOutcomePrefix.Add(
                        law.OutcomeIdAt(selectedIndex));
                    replay = replay.ResolveNext(
                        ScriptedRunActionOutcomeV111.FromDecision(
                            decision, decisionIndex + 1));
                }
                if (!replay.Complete || replay.Run is null)
                    throw new InvalidDataException(
                        "Scripted semantic replay did not settle its action.");
                replayRun = replay.Run.Fork();
            }
        }
        return outcomes.ToArray();
    }

    private sealed record ScriptedActionReplayBoundariesV111(
        RunState[] ActionBoundaryStates,
        long[][] DrawnCardInstanceIdsByAction,
        NativeEngineJournalEntryV111[][]
            CombatEventHookTranscriptByAction);

    private static SimulationProviderReplayCaptureV111
        FromReplayValidatedAction(
            RunState origin, GameAction action,
            IReadOnlyList<ControlledStochasticDecisionV111> decisions,
            RunState expectedResult, string captureProvenance)
    {
        var replay = ReplayScriptedActionBoundaries(
            origin, [action], [decisions.ToArray()], expectedResult);
        return FromScriptedDecisions(
            origin, [action], decisions, expectedResult, captureProvenance,
            replay.ActionBoundaryStates,
            replay.DrawnCardInstanceIdsByAction,
            replay.CombatEventHookTranscriptByAction);
    }

    private static ScriptedActionReplayBoundariesV111
        ReplayScriptedActionBoundaries(
        RunState origin, IReadOnlyList<GameAction> actions,
        IReadOnlyList<ControlledStochasticDecisionV111[]> decisionsByAction,
        RunState expectedResult)
    {
        if (actions.Count != decisionsByAction.Count)
            throw new InvalidDataException(
                "Provider replay action and decision group counts differ.");

        var boundaries = new RunState[actions.Count];
        var drawnCardIds = new long[actions.Count][];
        var eventHookTranscripts =
            new NativeEngineJournalEntryV111[actions.Count][];
        var replay = origin.Fork();
        using (RandomBoundaryJournalV111.Begin())
        {
            for (var index = 0; index < actions.Count; index++)
            {
                using var drawJournal =
                    SemanticDrawIdentityJournalV111.Begin();
                using var eventJournal =
                    NativeEngineEventJournalV111.Begin();
                var replayedAction =
                    ScriptedRunActionSessionV111.ReplayCompleted(
                        replay, actions[index], decisionsByAction[index]);
                replay = replayedAction.Run?.Fork() ??
                    throw new InvalidDataException(
                        "Completed scripted action replay has no run state.");
                boundaries[index] = replay;
                drawnCardIds[index] = drawJournal.CardInstanceIds;
                eventHookTranscripts[index] =
                    eventJournal.Entries.ToArray();
            }
        }
        if (CanonicalJson.Sha256(replay) !=
            CanonicalJson.Sha256(expectedResult))
            throw new InvalidDataException(
                "Provider replay action-boundary replay differs from the saved result.");
        return new(boundaries, drawnCardIds, eventHookTranscripts);
    }

    public SimulationProviderReplayCaptureV111 Fork()
    {
        Validate();
        return this with
        {
            CaptureProvenance = CaptureProvenance,
            Simulation = new SimulationStateV111<
                SimulationProviderReplayMechanicsCaptureV111,
                SimulationProviderReplayStochasticCaptureV111>(
                    Mechanics.Fork(), Stochastic.Fork())
        };
    }

    public void Validate()
    {
        if (SchemaId is not (CurrentSchemaId or PreviousSchemaId or
                PreviousPreviousSchemaId or PreviousPreviousPreviousSchemaId or
                PreviousPreviousPreviousPreviousSchemaId or
                PreviousPreviousPreviousPreviousPreviousSchemaId or
                PreviousPreviousPreviousPreviousPreviousPreviousSchemaId or
                PreviousPreviousPreviousPreviousPreviousPreviousPreviousSchemaId or
                OlderSchemaId) ||
            !SimulationProviderReplayCaptureProvenanceV111
                .IsKnown(CaptureProvenance) || Simulation is null ||
            Simulation.Mechanics is null || Simulation.Stochastic is null)
            throw new InvalidDataException(
                "Provider replay capture envelope is invalid.");
        Simulation.Mechanics.Validate();
        Simulation.Stochastic.Validate();
        if (SchemaId is not (CurrentSchemaId or PreviousSchemaId or
                PreviousPreviousSchemaId or PreviousPreviousPreviousSchemaId or
                PreviousPreviousPreviousPreviousSchemaId or
                PreviousPreviousPreviousPreviousPreviousSchemaId) &&
            Simulation.Mechanics.HasActionBoundaryStates)
            throw new InvalidDataException(
                "Legacy provider replay captures cannot contain action-boundary mechanics snapshots.");
        if (SchemaId is not (CurrentSchemaId or PreviousSchemaId or
                PreviousPreviousSchemaId or
                PreviousPreviousPreviousSchemaId) &&
            Simulation.Mechanics.DrawnCardInstanceIdsByAction is not null)
            throw new InvalidDataException(
                "Legacy provider replay captures cannot contain drawn-card identity traces.");
        if ((SchemaId is CurrentSchemaId or PreviousSchemaId or
                PreviousPreviousSchemaId or
                PreviousPreviousPreviousSchemaId) &&
            CaptureProvenance !=
                SimulationProviderReplayCaptureProvenanceV111.CallerSupplied &&
            Simulation.Mechanics.DrawnCardInstanceIdsByAction is null)
            throw new InvalidDataException(
                "Replay-validated v7 through v10 provider captures require drawn-card identity traces.");
        if (SchemaId is not (CurrentSchemaId or PreviousSchemaId) &&
            Simulation.Mechanics.CombatEventHookTranscriptByAction is not null)
            throw new InvalidDataException(
                "Legacy provider replay captures cannot contain v9 combat event/hook transcripts.");
        if (SchemaId != CurrentSchemaId &&
            Simulation.Mechanics.PlayerBoundaryViews is not null)
            throw new InvalidDataException(
                "Legacy provider replay captures cannot contain v10 public boundary views.");
        if ((SchemaId is CurrentSchemaId or PreviousSchemaId or
                PreviousPreviousSchemaId) &&
            CaptureProvenance !=
                SimulationProviderReplayCaptureProvenanceV111.CallerSupplied &&
            !Simulation.Mechanics.HasActionBoundaryStates)
            throw new InvalidDataException(
                "Replay-validated v8 through v10 provider captures require action-boundary mechanics snapshots.");
        if ((SchemaId is CurrentSchemaId or PreviousSchemaId) &&
            CaptureProvenance !=
                SimulationProviderReplayCaptureProvenanceV111.CallerSupplied &&
            Simulation.Mechanics.CombatEventHookTranscriptByAction is null)
            throw new InvalidDataException(
                "Replay-validated v9 and v10 provider captures require combat event/hook transcripts.");
        if (SchemaId == CurrentSchemaId &&
            CaptureProvenance !=
                SimulationProviderReplayCaptureProvenanceV111.CallerSupplied &&
            Simulation.Mechanics.PlayerBoundaryViews is null)
            throw new InvalidDataException(
                "Replay-validated v10 provider captures require public boundary views.");
        if (SchemaId == OlderSchemaId &&
            Simulation.Stochastic.SemanticOutcomes.Length != 0)
            throw new InvalidDataException(
                "Legacy provider replay captures cannot contain semantic outcomes.");
        if ((SchemaId is OlderSchemaId or
                PreviousPreviousPreviousPreviousPreviousPreviousSchemaId) &&
            SimulationProviderReplayCaptureProvenanceV111
                .HasProviderIndependentSemanticIdentity(CaptureProvenance))
            throw new InvalidDataException(
                "Legacy provider replay captures cannot claim provider-independent semantic identities.");
        if (SimulationProviderReplayCaptureProvenanceV111
                .HasProviderIndependentSemanticIdentity(CaptureProvenance) &&
            Simulation.Stochastic.SemanticOutcomes.Any(outcome =>
                !outcome.HasProviderIndependentIdentity))
            throw new InvalidDataException(
                "Provider-independent semantic capture contains an outcome without its common identity.");
    }
}

/// <summary>
/// Projects existing, replay-validated Native semantic annotations into the
/// same identity shape as Nature outcomes. It claims completeness only when
/// every provider boundary in the action interval has one supported semantic
/// annotation. Physical draw identities are validated separately.
/// </summary>
internal static class SimulationProviderReplayNativeSemanticOutcomeTraceV111
{
    internal sealed record ActionRange(int ActionIndex,
        int TraceCountBefore, int TraceCountAfter, int? CardsDrawnDelta);

    public static int? GetCardsDrawnCount(RunState run) =>
        run.World?.Combat?.CounterState.CardsDrawnThisCombat;

    public static SimulationProviderReplaySemanticOutcomeV111[]? TryCapture(
        SemanticNativePolicyAuditV111 audit,
        RandomBoundaryTranscriptEntryV111[] providerTrace,
        int initialTraceCount, IReadOnlyList<ActionRange> actionRanges,
        IReadOnlyList<long[]> drawnCardsByAction,
        IReadOnlyList<RunState> actionStartStates)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(providerTrace);
        ArgumentNullException.ThrowIfNull(actionRanges);
        ArgumentNullException.ThrowIfNull(drawnCardsByAction);
        ArgumentNullException.ThrowIfNull(actionStartStates);
        if (initialTraceCount < 0 || initialTraceCount > providerTrace.Length)
            return null;
        if (drawnCardsByAction.Count != actionRanges.Count ||
            actionStartStates.Count != actionRanges.Count)
            return null;

        var sequenceByDecision = new Dictionary<string, int>(
            StringComparer.Ordinal);
        var sequenceByUnitFloatDecision = new Dictionary<string, int>(
            StringComparer.Ordinal);
        var sequencesByRequestAndIndex = new Dictionary<string, List<int>>(
            StringComparer.Ordinal);
        var ordinalBySequence = new Dictionary<int, int>();
        var semanticRelicBagsByAction = new Dictionary<int,
            PrototypeRelicBagState>();
        var semanticRelicOrdinalByAction = new Dictionary<int, int>();
        var semanticOrdinalCounts = new Dictionary<string, int>(
            StringComparer.Ordinal);
        foreach (var entry in providerTrace)
        {
            if (entry.OperationKind == "unit_float")
            {
                var unitBoundary = entry.DeserializeEvent<
                    RandomUnitFloatBoundaryEventV111>();
                var unitDecision = new RandomUnitFloatDecisionV111(
                    unitBoundary.Request, unitBoundary.SelectedValue,
                    unitBoundary.ProviderMode,
                    unitBoundary.NativeCursorBefore,
                    unitBoundary.NativeCursorAfter);
                if (!sequenceByUnitFloatDecision.TryAdd(
                        CanonicalJson.Sha256(unitDecision),
                        unitBoundary.Sequence))
                    return null;
                if (SemanticOrdinalGroup(unitBoundary.Request.SiteId) is
                        { } unitOrdinalGroup &&
                    unitBoundary.Sequence > initialTraceCount)
                {
                    var ordinal = semanticOrdinalCounts.GetValueOrDefault(
                        unitOrdinalGroup);
                    ordinalBySequence.Add(unitBoundary.Sequence, ordinal);
                    semanticOrdinalCounts[unitOrdinalGroup] = ordinal + 1;
                }
                continue;
            }
            if (entry.OperationKind != "index")
                continue;
            var boundary = entry.DeserializeEvent<RandomBoundaryEventV111>();
            if (SemanticOrdinalGroup(boundary.Request.SiteId) is
                    { } ordinalGroup &&
                boundary.Sequence > initialTraceCount)
            {
                var ordinal = semanticOrdinalCounts.GetValueOrDefault(
                    ordinalGroup);
                ordinalBySequence.Add(boundary.Sequence, ordinal);
                semanticOrdinalCounts[ordinalGroup] = ordinal + 1;
            }
            var decision = new RandomIndexDecisionV111(
                boundary.Request, boundary.SelectedIndex,
                boundary.ProviderMode, boundary.NativeCursorBefore,
                boundary.NativeCursorAfter);
            var decisionFingerprint = CanonicalJson.Sha256(decision);
            if (!sequenceByDecision.TryAdd(decisionFingerprint,
                    boundary.Sequence))
                return null;
            var requestKey = RequestAndIndexFingerprint(
                boundary.Request, boundary.SelectedIndex);
            if (!sequencesByRequestAndIndex.TryGetValue(requestKey,
                    out var sequences))
                sequencesByRequestAndIndex.Add(requestKey, sequences = []);
            sequences.Add(boundary.Sequence);
        }

        var candidates = new Dictionary<int,
            List<SimulationProviderReplaySemanticOutcomeV111>>();
        var coveredSequences = new HashSet<int>();
        bool AddOutcome(int sequence,
            SimulationProviderReplaySemanticOutcomeV111 outcome,
            bool coversProviderBoundary)
        {
            if (coversProviderBoundary && !coveredSequences.Add(sequence))
                return false;
            if (!candidates.TryGetValue(sequence, out var outcomes))
                candidates.Add(sequence, outcomes = []);
            outcomes.Add(outcome);
            return true;
        }

        bool AddDecision(RandomIndexDecisionV111 decision,
            Func<SimulationProviderReplaySemanticOutcomeV111?> createOutcome)
        {
            var fingerprint = CanonicalJson.Sha256(decision);
            if (!sequenceByDecision.TryGetValue(fingerprint,
                    out var sequence))
                return false;
            if (sequence <= initialTraceCount)
                return true;
            var outcome = createOutcome();
            if (outcome is null)
                return false;
            if (ordinalBySequence.TryGetValue(sequence,
                    out var semanticOrdinal))
                outcome = outcome with { Ordinal = semanticOrdinal };
            return AddOutcome(sequence, outcome, coversProviderBoundary: true);
        }

        bool AddUnitFloatDecision(
            RandomUnitFloatDecisionV111 decision,
            Func<ChanceLawV111> queryLaw, string selectedOutcomeId)
        {
            if (!sequenceByUnitFloatDecision.TryGetValue(
                    CanonicalJson.Sha256(decision), out var sequence))
                return false;
            if (sequence <= initialTraceCount)
                return true;
            ChanceLawV111 law;
            try
            {
                law = queryLaw();
            }
            catch (NotSupportedException)
            {
                return false;
            }
            law.Validate();
            var request = decision.Request;
            if (law.SiteId != request.SiteId ||
                law.OwnerId != request.OwnerId ||
                law.Ordinal != request.Ordinal)
                throw new InvalidDataException(
                    "Native unit-float outcome law differs from its provider request.");
            var selectedIndex = Array.FindIndex(law.Outcomes,
                outcome => outcome.OutcomeId == selectedOutcomeId);
            if (selectedIndex < 0)
                return false;
            var outcome = CreateLawOutcome(law, selectedIndex);
            if (ordinalBySequence.TryGetValue(sequence,
                    out var semanticOrdinal))
                outcome = outcome with { Ordinal = semanticOrdinal };
            return AddOutcome(sequence, outcome, coversProviderBoundary: true);
        }

        bool AddNativeRelicIdentityOutcome(
            SemanticRelicRewardNativeAuditV111 relicOutcome)
        {
            if (relicOutcome.Merchant)
                return false;
            var rarityDecision = relicOutcome.ProviderDecision;
            if (!sequenceByUnitFloatDecision.TryGetValue(
                    CanonicalJson.Sha256(rarityDecision), out var sequence))
                return false;
            if (sequence <= initialTraceCount)
                return true;
            var actionIndex = orderedRangeIndex(sequence);
            if (actionIndex < 0)
                return false;
            var source = actionStartStates[actionIndex];
            if (source.World is not { } sourceWorld)
                return false;
            if (!semanticRelicBagsByAction.TryGetValue(actionIndex,
                    out var bag))
            {
                var projected = ReferenceNatureRunProjectionV111
                    .CreateNatureReplayOrigin(source,
                        createRelicBagWhenMissing: true);
                bag = projected.World?.Stochastic.RelicBag ??
                    throw new InvalidDataException(
                        "Native relic reward projection has no Nature bag.");
                semanticRelicBagsByAction.Add(actionIndex, bag);
            }
            var plan = PrototypeNativeRelicGrabBag.PlanNaturePull(
                bag, relicOutcome.RequestedRarity, merchant: false,
                source.Player);
            var auditSupport = relicOutcome.CandidateRelicIds
                .OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (plan.SelectedTier != relicOutcome.SelectedTier ||
                !plan.CandidateIds.SequenceEqual(auditSupport,
                    StringComparer.Ordinal))
                return false;
            if (relicOutcome.SelectedRelicId is null)
                return plan.CandidateIds.Length == 0;

            var selectedIndex = Array.IndexOf(plan.CandidateIds,
                relicOutcome.SelectedRelicId);
            if (selectedIndex < 0)
                return false;
            var modelOrdinal = semanticRelicOrdinalByAction
                .GetValueOrDefault(actionIndex);
            semanticRelicOrdinalByAction[actionIndex] = modelOrdinal + 1;
            var semanticOutcome = CreateNativeRelicIdentityOutcome(
                sourceWorld, relicOutcome, plan.CandidateIds,
                selectedIndex, modelOrdinal);
            if (!AddOutcome(sequence, semanticOutcome,
                    coversProviderBoundary: false))
                return false;
            semanticRelicBagsByAction[actionIndex] =
                PrototypeNativeRelicGrabBag.PullNatureSelected(
                    bag, relicOutcome.RequestedRarity, plan.CandidateIds,
                    selectedIndex, merchant: false, source.Player).Bag;
            return true;
        }

        int orderedRangeIndex(int sequence)
        {
            for (var index = 0; index < actionRanges.Count; index++)
                if (sequence > actionRanges[index].TraceCountBefore &&
                    sequence <= actionRanges[index].TraceCountAfter)
                    return actionRanges[index].ActionIndex;
            return -1;
        }

        SimulationProviderReplaySemanticOutcomeV111 CreateLawOutcome(
            ChanceLawV111 law, int selectedIndex,
            string[]? semanticSupportIds = null)
        {
            law.Validate();
            var supportIds = semanticSupportIds ??
                SimulationProviderReplaySemanticIdentityV111.SupportIds(law);
            if (selectedIndex < 0 || selectedIndex >= law.SupportCount ||
                supportIds.Length != law.SupportCount)
                throw new InvalidDataException(
                    "Native semantic law outcome is outside its declared support.");
            return new SimulationProviderReplaySemanticOutcomeV111(
                0, law.EventId, law.LawId,
                law.OutcomeIdAt(selectedIndex), selectedIndex)
            {
                SiteId = law.SiteId,
                OperationKind = law.OperationKind,
                OwnerId = SimulationProviderReplayCaptureV111
                    .NormalizeSemanticOwnerId(law.SiteId, law.OwnerId),
                Ordinal = law.Ordinal,
                SupportFingerprint = CanonicalJson.Sha256(supportIds),
                SemanticOutcomeId = supportIds[selectedIndex]
            };
        }

        SimulationProviderReplaySemanticOutcomeV111
            CreateNativeRelicIdentityOutcome(
                RunWorldState sourceWorld,
                SemanticRelicRewardNativeAuditV111 auditOutcome,
                string[] supportIds, int selectedIndex, int ordinal)
        {
            if (selectedIndex < 0 || selectedIndex >= supportIds.Length ||
                auditOutcome.SelectedRelicId != supportIds[selectedIndex])
                throw new InvalidDataException(
                    "Native relic identity is outside its canonical reward support.");
            var siteId = PrototypeNativeRelicGrabBag
                .NativeTreasureModelSiteId;
            var ownerId = PrototypeNativeRelicGrabBag.TreasureModelOwnerId(
                sourceWorld, auditOutcome.RequestedRarity, supportIds);
            var supportFingerprint = CanonicalJson.Sha256(supportIds);
            var eventId = CanonicalJson.Sha256(new
            {
                siteId,
                ownerId,
                ordinal,
                supportFingerprint
            });
            return new SimulationProviderReplaySemanticOutcomeV111(
                0, eventId,
                "combat-reward-relic-identity-v1",
                $"relic_model:{supportIds[selectedIndex]}:index:{selectedIndex}",
                selectedIndex)
            {
                SiteId = siteId,
                OperationKind = "choose_index",
                OwnerId = ownerId,
                Ordinal = ordinal,
                SupportFingerprint = supportFingerprint,
                SemanticOutcomeId = supportIds[selectedIndex]
            };
        }

        bool AddPoolShuffle(SemanticPoolShuffleDecisionV111 decision)
        {
            var request = decision.Event.Request;
            var requestKey = RequestAndIndexFingerprint(
                request, decision.SelectedSwapIndex);
            if (!sequencesByRequestAndIndex.TryGetValue(requestKey,
                    out var sequences))
                return false;
            var intervalSequences = sequences.Where(sequence =>
                sequence > initialTraceCount).ToArray();
            if (intervalSequences.Length == 0)
                return true;
            if (intervalSequences.Length != 1)
                return false;
            var sequence = intervalSequences[0];
            if (!IsCombatPoolShuffle(decision.Event.SiteId) &&
                !IsCombatRewardPoolShuffle(decision.Event.SiteId))
                return false;
            var law = decision.Event.SiteId switch
            {
                StochasticSiteRegistryV111.RewardBossRelicOfferShuffleSiteId =>
                    ChanceLawEngineV111.QueryNativeBossRelicOfferShuffleIndex(
                        request.OwnerId, request.SupportCount,
                        request.Ordinal),
                StochasticSiteRegistryV111.RewardLavaRockRelicOfferShuffleSiteId =>
                    ChanceLawEngineV111.QueryNativeLavaRockRelicOfferShuffleIndex(
                        request.OwnerId, request.SupportCount,
                        request.Ordinal),
                _ => ChanceLawEngineV111.QueryPoolShuffle(decision.Event)
            };
            var outcome = CreateLawOutcome(law,
                decision.SelectedSwapIndex);
            if (outcome is null)
                return false;
            if (ordinalBySequence.TryGetValue(sequence,
                    out var semanticOrdinal))
                outcome = outcome with { Ordinal = semanticOrdinal };
            return AddOutcome(sequence, outcome,
                coversProviderBoundary: true);
        }

        foreach (var outcome in audit.WeightedIntentOutcomes)
        {
            if (!AddDecision(outcome.ProviderDecision, () =>
                {
                    var intentEvent = outcome.Outcome.Event;
                    var selectedIndex = Array.FindIndex(
                        intentEvent.Support.Candidates,
                        candidate => candidate.IntentId ==
                            outcome.Outcome.IntentId);
                    if (selectedIndex < 0)
                        return null;
                    return CreateIntentOutcome(intentEvent,
                        outcome.Outcome.IntentId, selectedIndex);
                }))
                return null;
        }

        foreach (var outcome in audit.CardInstanceIndexOutcomes)
            if (!AddDecision(outcome.ProviderDecision, () =>
                CreateCardInstanceOutcome(outcome)))
                return null;

        foreach (var outcome in audit.CardEnergyCostOutcomes)
            if (!AddDecision(outcome.ProviderDecision, () =>
                CreateCostOutcome(outcome)))
                return null;

        foreach (var outcome in audit.DrawInsertionOutcomes)
            if (!AddDecision(outcome.ProviderDecision, () =>
                CreateChanceOutcome(outcome.Outcome.Event,
                    outcome.Outcome.SelectedPosition,
                    () => ChanceLawEngineV111.QueryDrawInsertion(
                        outcome.Outcome.Event))))
                return null;

        foreach (var outcome in audit.EnemyPileInsertionOutcomes)
        {
            if (!AddDecision(outcome.ProviderDecision, () =>
                {
                    outcome.Validate();
                    var insertion = outcome.Outcome;
                    var insertionEvent = insertion.Event;
                    var request = insertionEvent.Request;
                    var law = ChanceLawEngineV111
                        .QueryNativeBoundedIntegerLawSite(
                            request.SiteId, request.OwnerId,
                            request.SupportCount, request.Ordinal);
                    law.Validate();
                    if (law.SiteId != request.SiteId ||
                        law.OperationKind != request.OperationKind ||
                        law.OwnerId != request.OwnerId ||
                        law.Ordinal != request.Ordinal ||
                        law.SupportCount != request.SupportCount)
                        throw new InvalidDataException(
                            "Native enemy pile-insertion law differs from its provider request.");
                    var semanticSupportIds =
                        SemanticDrawPileAdapterV111
                            .DescribePhysicalPileInsertionGapLabels(
                                insertionEvent.GeneratedCardModelId,
                                insertionEvent.PileName,
                                insertionEvent.ExistingPileInstanceIds);
                    return CreateLawOutcome(
                        law, insertion.SelectedPosition,
                        semanticSupportIds);
                }))
                return null;
        }

        foreach (var outcome in audit.PoolShuffleOutcomes)
            if (!AddPoolShuffle(outcome))
                return null;

        foreach (var outcome in audit.GeneratedCardModelOutcomes)
            if (!AddDecision(outcome.ProviderDecision, () =>
                CreateChanceOutcome(outcome.Outcome.Event,
                    outcome.Outcome.SelectedIndex,
                    () => ChanceLawEngineV111.QueryGeneratedCard(
                        outcome.Outcome.Event))))
                return null;

        foreach (var outcome in audit.CardTransformOutcomes)
            if (!AddDecision(outcome.ProviderDecision, () =>
                CreateChanceOutcome(outcome.Outcome.Event,
                    outcome.Outcome.SelectedIndex,
                    () => ChanceLawEngineV111.QueryCardTransform(
                        outcome.Outcome.Event))))
                return null;

        foreach (var outcome in audit.RandomTargetOutcomes)
            if (!AddDecision(outcome.ProviderDecision, () =>
                CreateChanceOutcome(outcome.Outcome.Event,
                    outcome.Outcome.SelectedIndex,
                    () => ChanceLawEngineV111.QueryTarget(
                        outcome.Outcome.Event))))
                return null;

        foreach (var outcome in audit.PotionGenerationOutcomes)
            if (!AddDecision(outcome.ProviderDecision, () =>
                CreateChanceOutcome(outcome.Outcome.Event,
                    outcome.Outcome.SelectedIndex,
                    () => ChanceLawEngineV111.QueryPotionGeneration(
                        outcome.Outcome.Event))))
                return null;

        foreach (var outcome in audit.CardOddsOutcomes)
        {
            var request = outcome.ProviderDecision.Request;
            ChanceLawV111 law;
            string selectedOutcomeId;
            if (outcome.OutcomeKind ==
                SemanticCardOddsNativeAuditV111.RarityOperation)
            {
                if (outcome.Source is not { } source ||
                    outcome.CurrentOffsetBasisPoints is not { } offset ||
                    outcome.Ascension is not { } ascension ||
                    outcome.RolledRarity is not { } rarity)
                    return null;
                law = ChanceLawEngineV111.QueryNativeCardRarity(
                    source, offset, ascension, request.OwnerId,
                    request.Ordinal);
                selectedOutcomeId = "rarity:" +
                    rarity.ToString().ToLowerInvariant();
            }
            else if (outcome.OutcomeKind ==
                SemanticCardOddsNativeAuditV111.UpgradeOperation)
            {
                if (outcome.CardId is not { } cardId ||
                    outcome.Act is not { } act ||
                    outcome.Ascension is not { } ascension ||
                    outcome.Upgraded is not { } upgraded)
                    return null;
                law = ChanceLawEngineV111.QueryNativeEncounterCardUpgrade(
                    cardId, act, ascension, request.Ordinal);
                selectedOutcomeId = upgraded
                    ? "upgrade_check:passed"
                    : "upgrade_check:not_passed";
            }
            else
                return null;
            if (!AddUnitFloatDecision(outcome.ProviderDecision,
                    () => law, selectedOutcomeId))
                return null;
        }

        foreach (var outcome in audit.PotionRewardOddsOutcomes)
        {
            var law = ChanceLawEngineV111.QueryNativePotionRewardOdds(
                outcome.CurrentThousandths, outcome.Elite, forced: false,
                outcome.ProviderDecision.Request.Ordinal);
            if (!AddUnitFloatDecision(outcome.ProviderDecision,
                    () => law, outcome.Offered
                        ? "potion_reward:offered"
                        : "potion_reward:not_offered"))
                return null;
        }

        foreach (var outcome in audit.PotionFactoryOutcomes)
        {
            var rarityLaw = ChanceLawEngineV111
                .QueryNativePotionFactoryRarity(
                    outcome.RarityDecision.Request);
            if (!AddUnitFloatDecision(outcome.RarityDecision,
                    () => rarityLaw, "rarity:" +
                        outcome.Rarity.ToString().ToLowerInvariant()))
                return null;
            if (outcome.ModelDecision.Request.Ordinal != 0)
                return null;
            var modelLaw = ChanceLawEngineV111
                .QueryNativePotionFactoryModelIndex(
                    outcome.ModelDecision.Request, outcome.Rarity, []);
            if (!AddDecision(outcome.ModelDecision, () =>
                    CreateLawOutcome(modelLaw,
                        outcome.ModelDecision.SelectedIndex,
                        outcome.CandidatePotionIds)))
                return null;
        }

        foreach (var outcome in audit.CombatGoldOutcomes)
        {
            var law = ChanceLawEngineV111.QueryNativeCombatGold(
                outcome.Room, outcome.Ascension,
                outcome.Room == PrototypeRoomType.Combat
                    ? outcome.GoldProportion : null,
                outcome.ProviderDecision.Request.Ordinal);
            if (!AddDecision(outcome.ProviderDecision, () =>
                    CreateLawOutcome(law,
                        outcome.ProviderDecision.SelectedIndex)))
                return null;
        }

        foreach (var outcome in audit.RelicRewardOutcomes)
        {
            var law = ChanceLawEngineV111.QueryNativeRelicRarity(
                outcome.ProviderDecision.Request);
            if (!AddUnitFloatDecision(outcome.ProviderDecision,
                    () => law, "rarity:" +
                        outcome.RequestedRarity.ToString()
                            .ToLowerInvariant()))
                return null;
            if (!AddNativeRelicIdentityOutcome(outcome))
                return null;
        }

        var orderedRanges = actionRanges.OrderBy(range => range.ActionIndex)
            .ToArray();
        var expectedTraceCount = initialTraceCount;
        if (orderedRanges.Length == 0 &&
            providerTrace.Length != initialTraceCount)
            return null;
        for (var index = 0; index < orderedRanges.Length; index++)
        {
            var range = orderedRanges[index];
            if (range.ActionIndex != index ||
                range.TraceCountBefore != expectedTraceCount ||
                range.TraceCountAfter < range.TraceCountBefore ||
                range.TraceCountAfter > providerTrace.Length)
                return null;
            expectedTraceCount = range.TraceCountAfter;
        }
        if (expectedTraceCount != providerTrace.Length ||
            coveredSequences.Count != providerTrace.Length - initialTraceCount)
            return null;
        for (var sequence = initialTraceCount + 1;
            sequence <= providerTrace.Length; sequence++)
            if (!coveredSequences.Contains(sequence))
                return null;

        var captured = new List<SimulationProviderReplaySemanticOutcomeV111>(
            candidates.Count);
        var previousOutcomeCount = 0;
        foreach (var range in orderedRanges)
        {
            var actionCandidates = candidates
                .Where(pair => pair.Key > range.TraceCountBefore &&
                    pair.Key <= range.TraceCountAfter)
                .OrderBy(pair => pair.Key)
                .SelectMany(pair => pair.Value)
                .ToArray();
            if (range.CardsDrawnDelta is { } cardsDrawn &&
                (cardsDrawn < 0 || cardsDrawn !=
                    drawnCardsByAction[range.ActionIndex].Length))
                return null;
            for (var eventIndex = 0; eventIndex < actionCandidates.Length;
                eventIndex++)
                captured.Add(actionCandidates[eventIndex] with
                {
                    TransitionIndex = range.ActionIndex +
                        previousOutcomeCount + eventIndex + 1
                });
            previousOutcomeCount += actionCandidates.Length;
        }
        return captured.ToArray();
    }

    private static SimulationProviderReplaySemanticOutcomeV111?
        CreateCardInstanceOutcome(
            SemanticCardInstanceIndexAuditV111 audit)
    {
        var request = audit.Request;
        var (operationKind, ownerId) = request.SiteId switch
        {
            StochasticSiteRegistryV111
                .CombatRandomHandCardSelectSiteId =>
                    (SemanticHandCostSelectionChoiceV111.OperationKind,
                        "effect:" + PrototypeCombatEffectKind
                            .SetRandomHandCardEnergyCostUntilTurnEndOrPlayed +
                        ":hand:" + CanonicalJson.Sha256(
                            audit.OrderedCardInstanceIds)),
            StochasticSiteRegistryV111
                .CombatRandomReplayCardSelectSiteId or
            StochasticSiteRegistryV111
                .CombatAutoplayRandomCardSelectSiteId =>
                    (SemanticCardShortlistChoiceV111
                            .OperationKindForSite(request.SiteId),
                        SimulationProviderReplayCaptureV111
                            .NormalizeSemanticOwnerId(request.SiteId,
                                request.OwnerId)),
            StochasticSiteRegistryV111
                .CombatJeweledMaskRandomPowerDrawCardSiteId =>
                    ("choose_card_instance", "player:draw_pile"),
            _ => (null, null)
        };
        if (operationKind is null || ownerId is null)
            return null;
        var semanticSupportIds = audit.OrderedCardInstanceIds
            .Select(identity => identity.ToString(
                System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        var semanticOutcomeId = semanticSupportIds[audit.SelectedIndex];
        return new SimulationProviderReplaySemanticOutcomeV111(
            0, audit.EventId,
            "native-card-instance-semantic-projection-v1",
            semanticOutcomeId, audit.SelectedIndex)
        {
            SiteId = request.SiteId,
            OperationKind = operationKind,
            OwnerId = ownerId,
            Ordinal = request.Ordinal,
            SupportFingerprint = CanonicalJson.Sha256(semanticSupportIds),
            SemanticOutcomeId = semanticOutcomeId
        };
    }

    private static SimulationProviderReplaySemanticOutcomeV111?
        CreateCostOutcome(SemanticCardEnergyCostAuditV111 audit)
    {
        var costEvent = audit.Outcome.Event;
        var outcome = CreateChanceOutcome(costEvent,
            audit.Outcome.SelectedIndex,
            () => ChanceLawEngineV111.QueryCost(costEvent));
        if (outcome is null || costEvent.Request.SiteId !=
                StochasticSiteRegistryV111
                    .CombatRandomHandCardEnergyCostSiteId)
            return outcome;
        var targetCardInstanceId = costEvent.TargetCardInstanceId ??
            throw new InvalidDataException(
                "Native hand-cost outcome has no selected card identity.");
        var selectedCardIndex = Array.IndexOf(
            audit.OrderedEligibleCardInstanceIds, targetCardInstanceId);
        if (selectedCardIndex < 0)
            throw new InvalidDataException(
                "Native hand-cost identity is outside its ordered hand support.");
        var ownerId = "effect:" + PrototypeCombatEffectKind
            .RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed + ":hand:" +
            CanonicalJson.Sha256(audit.OrderedEligibleCardInstanceIds) +
            ":index:" + selectedCardIndex;
        return outcome with { OwnerId = ownerId };
    }

    private static SimulationProviderReplaySemanticOutcomeV111?
        CreateIntentOutcome(SemanticIntentEventV111 intentEvent,
            string intentId, int selectedIndex)
    {
        ChanceLawV111 law;
        try
        {
            law = ChanceLawEngineV111.QueryIntent(intentEvent);
        }
        catch (NotSupportedException)
        {
            return null;
        }
        var semanticSupportIds = intentEvent.Support.Candidates
            .Select(candidate => candidate.IntentId).ToArray();
        return new SimulationProviderReplaySemanticOutcomeV111(
            0, law.EventId, law.LawId,
            law.OutcomeIdAt(selectedIndex), selectedIndex)
        {
            SiteId = intentEvent.SiteId,
            OperationKind = intentEvent.OperationKind,
            OwnerId = intentEvent.OwnerId,
            Ordinal = intentEvent.Ordinal,
            SupportFingerprint = CanonicalJson.Sha256(semanticSupportIds),
            SemanticOutcomeId = intentId
        };
    }

    private static SimulationProviderReplaySemanticOutcomeV111?
        CreateChanceOutcome(ISemanticChanceEventV111 chanceEvent,
            int selectedIndex, Func<ChanceLawV111> queryLaw)
    {
        var descriptor = SemanticPendingIndexChanceEventV111.Create(
            chanceEvent.EventId, chanceEvent);
        if (selectedIndex < 0 || selectedIndex >= descriptor.SupportCount)
            throw new InvalidDataException(
                "Native semantic outcome is outside its event support.");
        ChanceLawV111 law;
        try
        {
            law = queryLaw();
        }
        catch (NotSupportedException)
        {
            return null;
        }
        law.Validate();
        if (law.EventId != chanceEvent.EventId ||
            law.SiteId != chanceEvent.SiteId ||
            law.OperationKind != chanceEvent.OperationKind ||
            law.OwnerId != chanceEvent.OwnerId ||
            law.Ordinal != chanceEvent.Ordinal ||
            law.SupportCount != descriptor.SupportCount)
            throw new InvalidDataException(
                "Native semantic outcome law differs from its event support.");
        for (var index = 0; index < descriptor.SupportCount; index++)
            if (law.OutcomeIdAt(index) != descriptor.OrderedOutcomeIds[index])
                throw new InvalidDataException(
                    "Native semantic outcome law changed the event's ordered support.");
        var semanticSupportIds = descriptor.SemanticOutcomeIds;
        return new SimulationProviderReplaySemanticOutcomeV111(
            0, law.EventId, law.LawId,
            law.OutcomeIdAt(selectedIndex), selectedIndex)
        {
            SiteId = chanceEvent.SiteId,
            OperationKind = chanceEvent.OperationKind,
            OwnerId = SimulationProviderReplayCaptureV111
                .NormalizeSemanticOwnerId(chanceEvent.SiteId,
                    chanceEvent.OwnerId),
            Ordinal = chanceEvent.Ordinal,
            SupportFingerprint = CanonicalJson.Sha256(semanticSupportIds),
            SemanticOutcomeId = semanticSupportIds[selectedIndex]
        };
    }

    private static string RequestAndIndexFingerprint(
        RandomIndexRequestV111 request, int selectedIndex) =>
        CanonicalJson.Sha256(new { request, selectedIndex });

    internal static string? SemanticOrdinalGroup(string siteId)
    {
        if (IsCombatRewardPoolShuffle(siteId) || siteId is
                StochasticSiteRegistryV111.RewardPotionOddsUnitFloatSiteId or
                StochasticSiteRegistryV111.NativeCombatGoldSiteId or
                StochasticSiteRegistryV111.NativePotionFactoryRarityRollSiteId or
                StochasticSiteRegistryV111.NativePotionFactoryModelSiteId or
                StochasticSiteRegistryV111.CardRarityRollUnitFloatSiteId or
                StochasticSiteRegistryV111.CardRarityUpgradeRollUnitFloatSiteId or
                StochasticSiteRegistryV111.NativeRewardCardModelSiteId or
                StochasticSiteRegistryV111.RelicGrabBagRaritySiteId or
                PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId or
                "card_rarity.encounter_model.index_draw")
            return "semantic_reward:" + siteId;
        if (StochasticSiteRegistryV111.CombatRandomTargetSiteIds.Contains(
                siteId, StringComparer.Ordinal))
            return "semantic_target";
        if (siteId is
                StochasticSiteRegistryV111.CombatRandomReplayCardSelectSiteId or
                StochasticSiteRegistryV111.CombatAutoplayRandomCardSelectSiteId)
            return "semantic_card_shortlist";
        if (siteId is StochasticSiteRegistryV111.CombatGeneratedCardSelectSiteId
                or StochasticSiteRegistryV111.CombatRandomDrawAttackCardModelSiteId)
            return "semantic_generated_card";
        if (siteId == SemanticPotionGenerationChoiceV111.SiteId)
            return "semantic_potion_generation";
        if (siteId == SemanticCardTransformChoiceV111.SiteId)
            return "semantic_card_transform";
        if (StochasticSiteRegistryV111.CombatCardEnergyCostAuditSiteIds
                .Contains(siteId, StringComparer.Ordinal))
            return "semantic_cost";
        if (siteId == StochasticSiteRegistryV111
                .CombatRandomHandCardSelectSiteId)
            return "semantic_hand_cost_selection";
        if (StochasticSiteRegistryV111.CombatCardPoolShuffleSiteIds.Contains(
                siteId, StringComparer.Ordinal) ||
            siteId == ControlledRecycleShuffleEngineV111.SiteId)
            return "semantic_pool_shuffle";
        if (siteId == StochasticSiteRegistryV111.DrawPileRandomInsertSiteId)
            return "semantic_draw_insertion";
        if (StochasticSiteRegistryV111.CombatEnemyPileInsertionSiteIds
                .Contains(siteId, StringComparer.Ordinal))
            return "semantic_enemy_pile_insertion:" + siteId;
        return null;
    }

    private static bool IsCombatPoolShuffle(string siteId) =>
        StochasticSiteRegistryV111.CombatCardPoolShuffleSiteIds.Contains(
            siteId, StringComparer.Ordinal) ||
        siteId == ControlledRecycleShuffleEngineV111.SiteId;

    private static bool IsCombatRewardPoolShuffle(string siteId) =>
        siteId is StochasticSiteRegistryV111.RewardBossRelicOfferShuffleSiteId or
            StochasticSiteRegistryV111.RewardLavaRockRelicOfferShuffleSiteId;
}

public sealed record SimulationProviderReplaySemanticOutcomeDiffV111(
    string Status, int Compared, int? FirstMismatchIndex,
    string? MismatchField);

public sealed record SimulationProviderReplayActionBoundaryMechanicsDiffV111(
    string Status, int Compared, int? FirstMismatchActionIndex,
    SimulationMechanicsComparisonV111? FirstMismatch);

public sealed record SimulationProviderReplayDrawnCardIdentityDiffV111(
    string Status, int Compared, int? FirstMismatchActionIndex,
    int? FirstMismatchDrawIndex);

public sealed record SimulationProviderReplayCombatEventHookTranscriptDiffV111(
    string Status, int ComparedEntryCount, int? FirstMismatchActionIndex,
    int? FirstMismatchEntryIndex);

public sealed record SimulationProviderReplayPublicBoundaryViewDiffV111(
    string Status, int ComparedBoundaryCount,
    int? FirstMismatchBoundaryIndex, string? MismatchField);

/// <summary>
/// Composed evidence for comparing two provider trajectories under the same
/// player actions: matching origin and final mechanics, plus agreement from
/// every complete comparable stochastic trace available on both captures. It
/// does not establish source-hook parity or law fidelity.
/// </summary>
public sealed record SimulationProviderReplayComparisonV111(
    string SchemaId,
    string LeftActionFingerprint,
    string RightActionFingerprint,
    SimulationMechanicsComparisonV111 OriginMechanics,
    RandomBoundaryDiffV111 RandomBoundaries,
    SimulationMechanicsComparisonV111 ResultMechanics,
    bool RequireNativeCursorEquality)
{
    public const string CurrentSchemaId =
        "sts2-v111-simulation-provider-replay-comparison-v10";

    public string LeftCaptureProvenance { get; init; } =
        SimulationProviderReplayCaptureProvenanceV111.CallerSupplied;
    public string RightCaptureProvenance { get; init; } =
        SimulationProviderReplayCaptureProvenanceV111.CallerSupplied;
    public SimulationProviderReplaySemanticOutcomeDiffV111 SemanticOutcomes
        { get; init; } = new(
            "SEMANTIC_OUTCOME_TRACE_UNAVAILABLE", 0, null, null);
    public SimulationProviderReplayActionBoundaryMechanicsDiffV111
        ActionBoundaryMechanics { get; init; } = new(
            "ACTION_BOUNDARY_MECHANICS_UNAVAILABLE", 0, null, null);
    public SimulationProviderReplayDrawnCardIdentityDiffV111
        DrawnCardIdentities { get; init; } = new(
            "DRAWN_CARD_IDENTITIES_UNAVAILABLE", 0, null, null);
    public SimulationProviderReplayCombatEventHookTranscriptDiffV111
        CombatEventHookTranscript { get; init; } = new(
            "COMBAT_EVENT_HOOK_TRANSCRIPT_UNAVAILABLE", 0, null, null);
    public SimulationProviderReplayPublicBoundaryViewDiffV111
        PlayerBoundaryViews { get; init; } = new(
            "PLAYER_BOUNDARY_VIEWS_UNAVAILABLE", 0, null, null);

    public bool SamePlayerActions => StringComparer.Ordinal.Equals(
        LeftActionFingerprint, RightActionFingerprint);

    public bool SameOriginMechanics => OriginMechanics.Equivalent;

    public bool SameTypedRandomBoundaries =>
        TypedRandomBoundaryComparisonAvailable &&
        RandomBoundaries.Status == "RANDOM_BOUNDARIES_CONSISTENT";

    public bool TypedRandomBoundaryComparisonAvailable =>
        SimulationProviderReplayCaptureProvenanceV111
            .HasCompleteTypedBoundaryTrace(LeftCaptureProvenance) &&
        SimulationProviderReplayCaptureProvenanceV111
            .HasCompleteTypedBoundaryTrace(RightCaptureProvenance);

    public bool SemanticOutcomeComparisonAvailable =>
        SimulationProviderReplayCaptureProvenanceV111
            .HasCompleteSemanticOutcomeTrace(LeftCaptureProvenance) &&
        SimulationProviderReplayCaptureProvenanceV111
            .HasCompleteSemanticOutcomeTrace(RightCaptureProvenance);

    public bool SameSemanticOutcomes =>
        SemanticOutcomeComparisonAvailable &&
        SemanticOutcomes.Status == "SEMANTIC_OUTCOMES_CONSISTENT";

    public bool StochasticOutcomeComparisonAvailable =>
        TypedRandomBoundaryComparisonAvailable ||
        SemanticOutcomeComparisonAvailable ||
        DrawnCardIdentityComparisonAvailable;

    public bool DrawnCardIdentityComparisonAvailable =>
        DrawnCardIdentities.Status is
            "DRAWN_CARD_IDENTITIES_CONSISTENT" or
            "DRAWN_CARD_IDENTITY_MISMATCH" or
            "DRAWN_CARD_IDENTITY_COUNT_MISMATCH";

    public bool SameDrawnCardIdentities =>
        DrawnCardIdentityComparisonAvailable &&
        DrawnCardIdentities.Status ==
            "DRAWN_CARD_IDENTITIES_CONSISTENT";

    public bool CombatEventHookTranscriptComparisonAvailable =>
        CombatEventHookTranscript.Status is
            "COMBAT_EVENT_HOOK_TRANSCRIPT_CONSISTENT" or
            "COMBAT_EVENT_HOOK_TRANSCRIPT_MISMATCH" or
            "COMBAT_EVENT_HOOK_ACTION_COUNT_MISMATCH" or
            "COMBAT_EVENT_HOOK_ACTIONS_DIFFER";

    public bool SameCombatEventHookTranscript =>
        CombatEventHookTranscriptComparisonAvailable &&
        CombatEventHookTranscript.Status ==
            "COMBAT_EVENT_HOOK_TRANSCRIPT_CONSISTENT";

    public bool PlayerBoundaryViewComparisonAvailable =>
        PlayerBoundaryViews.Status is
            "PLAYER_BOUNDARY_VIEWS_CONSISTENT" or
            "PLAYER_BOUNDARY_VIEW_MISMATCH" or
            "PLAYER_BOUNDARY_VIEW_COUNT_MISMATCH" or
            "PLAYER_BOUNDARY_VIEW_ACTIONS_DIFFER";

    public bool SamePlayerBoundaryViews =>
        PlayerBoundaryViewComparisonAvailable &&
        PlayerBoundaryViews.Status == "PLAYER_BOUNDARY_VIEWS_CONSISTENT";

    public bool SameStochasticOutcomes
    {
        get
        {
            var hasComparableTrace = false;
            if (TypedRandomBoundaryComparisonAvailable)
            {
                hasComparableTrace = true;
                if (!SameTypedRandomBoundaries)
                    return false;
            }
            if (SemanticOutcomeComparisonAvailable)
            {
                hasComparableTrace = true;
                if (!SameSemanticOutcomes)
                    return false;
            }
            if (DrawnCardIdentityComparisonAvailable)
            {
                hasComparableTrace = true;
                if (!SameDrawnCardIdentities)
                    return false;
            }
            return hasComparableTrace;
        }
    }

    /// <summary>
    /// Reports endpoint mechanics agreement for captures with the same player
    /// actions and origin. This does not require matching typed random traces
    /// and does not claim that their intermediate outcomes matched.
    /// </summary>
    public bool SameMechanicsAtCapturedBoundariesForSameActions =>
        SchemaId == CurrentSchemaId && SamePlayerActions &&
        SameOriginMechanics && SameResultMechanics;

    public bool ActionBoundaryMechanicsComparisonAvailable =>
        ActionBoundaryMechanics.Status is
            "ACTION_BOUNDARY_MECHANICS_CONSISTENT" or
            "ACTION_BOUNDARY_MECHANICS_MISMATCH";

    public bool SameMechanicsAtEveryActionBoundaryForSameActions =>
        SchemaId == CurrentSchemaId && SamePlayerActions &&
        SameOriginMechanics && SameResultMechanics &&
        ActionBoundaryMechanicsComparisonAvailable &&
        ActionBoundaryMechanics.Status ==
            "ACTION_BOUNDARY_MECHANICS_CONSISTENT";

    /// <summary>
    /// S4 acceptance predicate for two providers that resolved the same
    /// provider-independent semantic outcomes. Provider-specific primitive
    /// requests may differ; origin, result, and every captured action-boundary
    /// mechanics projection must agree.
    /// </summary>
    public bool SameMechanicsForSameSemanticOutcomes =>
        SchemaId == CurrentSchemaId && SamePlayerActions &&
        SameOriginMechanics && SameSemanticOutcomes &&
        SameResultMechanics &&
        ActionBoundaryMechanicsComparisonAvailable &&
        SameMechanicsAtEveryActionBoundaryForSameActions &&
        CombatEventHookTranscriptComparisonAvailable &&
        SameCombatEventHookTranscript &&
        PlayerBoundaryViewComparisonAvailable &&
        SamePlayerBoundaryViews &&
        (!DrawnCardIdentityComparisonAvailable || SameDrawnCardIdentities);

    public bool SameResultMechanics => ResultMechanics.Equivalent;

    public bool EquivalentUnderComparedActions =>
        SchemaId == CurrentSchemaId && SamePlayerActions &&
        SameOriginMechanics && SameStochasticOutcomes &&
        SameResultMechanics &&
        (!ActionBoundaryMechanicsComparisonAvailable ||
            SameMechanicsAtEveryActionBoundaryForSameActions) &&
        (!CombatEventHookTranscriptComparisonAvailable ||
            SameCombatEventHookTranscript) &&
        (!PlayerBoundaryViewComparisonAvailable || SamePlayerBoundaryViews);
}

/// <summary>
/// Composes typed provider-trace, semantic outcome, physical draw identity,
/// and mechanics-projection comparators. Inputs must cover the same action
/// interval and each complete trace it claims to provide. The result is
/// diagnostic evidence, not a native-fidelity or probability-law claim.
/// </summary>
public static class SimulationProviderReplayComparisonEngineV111
{
    public static SimulationProviderReplayComparisonV111 Compare(
        SimulationProviderReplayCaptureV111 left,
        SimulationProviderReplayCaptureV111 right,
        bool requireNativeCursorEquality = false,
        int maximumMechanicsDifferences = 64)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        left.Validate();
        right.Validate();
        var comparison = Compare(
            left.Mechanics.Origin, left.Mechanics.Actions,
            left.Stochastic.Boundaries, left.Mechanics.Result,
            right.Mechanics.Origin, right.Mechanics.Actions,
            right.Stochastic.Boundaries, right.Mechanics.Result,
            requireNativeCursorEquality, maximumMechanicsDifferences);
        return comparison with
        {
            LeftCaptureProvenance = left.CaptureProvenance,
            RightCaptureProvenance = right.CaptureProvenance,
            SemanticOutcomes = CompareSemanticOutcomes(
                left.Stochastic.SemanticOutcomes,
                right.Stochastic.SemanticOutcomes,
                SimulationProviderReplayCaptureProvenanceV111
                    .HasCompleteSemanticOutcomeTrace(left.CaptureProvenance) &&
                SimulationProviderReplayCaptureProvenanceV111
                    .HasCompleteSemanticOutcomeTrace(right.CaptureProvenance)),
            ActionBoundaryMechanics = CompareActionBoundaryMechanics(
                left, right, maximumMechanicsDifferences),
            DrawnCardIdentities = CompareDrawnCardIdentities(left, right),
            CombatEventHookTranscript =
                CompareCombatEventHookTranscripts(left, right),
            PlayerBoundaryViews = ComparePlayerBoundaryViews(left, right)
        };
    }

    private static SimulationProviderReplayPublicBoundaryViewDiffV111
        ComparePlayerBoundaryViews(
            SimulationProviderReplayCaptureV111 left,
            SimulationProviderReplayCaptureV111 right)
    {
        var leftViews = left.Mechanics.PlayerBoundaryViews;
        var rightViews = right.Mechanics.PlayerBoundaryViews;
        if (leftViews is null || rightViews is null)
            return new("PLAYER_BOUNDARY_VIEWS_UNAVAILABLE", 0, null, null);
        if (!StringComparer.Ordinal.Equals(
                FingerprintActions(left.Mechanics.Actions),
                FingerprintActions(right.Mechanics.Actions)))
            return new("PLAYER_BOUNDARY_VIEW_ACTIONS_DIFFER", 0, null, null);
        if (leftViews.Length != rightViews.Length)
        {
            var shared = Math.Min(leftViews.Length, rightViews.Length);
            return new("PLAYER_BOUNDARY_VIEW_COUNT_MISMATCH", shared,
                shared, null);
        }

        for (var index = 0; index < leftViews.Length; index++)
        {
            var expected = leftViews[index];
            var actual = rightViews[index];
            if (expected.ObservationSchemaId != actual.ObservationSchemaId)
                return new("PLAYER_BOUNDARY_VIEW_MISMATCH", index + 1,
                    index, "observation_schema");
            if (expected.ObservationHash != actual.ObservationHash)
                return new("PLAYER_BOUNDARY_VIEW_MISMATCH", index + 1,
                    index, "observation");
            if (expected.LegalActionsFingerprint !=
                actual.LegalActionsFingerprint ||
                expected.LegalActionCount != actual.LegalActionCount)
                return new("PLAYER_BOUNDARY_VIEW_MISMATCH", index + 1,
                    index, "legal_actions");
        }
        return new("PLAYER_BOUNDARY_VIEWS_CONSISTENT", leftViews.Length,
            null, null);
    }

    private static SimulationProviderReplayCombatEventHookTranscriptDiffV111
        CompareCombatEventHookTranscripts(
            SimulationProviderReplayCaptureV111 left,
            SimulationProviderReplayCaptureV111 right)
    {
        var leftTranscript =
            left.Mechanics.CombatEventHookTranscriptByAction;
        var rightTranscript =
            right.Mechanics.CombatEventHookTranscriptByAction;
        if (leftTranscript is null || rightTranscript is null)
            return new(
                "COMBAT_EVENT_HOOK_TRANSCRIPT_UNAVAILABLE", 0, null, null);
        if (!StringComparer.Ordinal.Equals(
                FingerprintActions(left.Mechanics.Actions),
                FingerprintActions(right.Mechanics.Actions)))
            return new(
                "COMBAT_EVENT_HOOK_ACTIONS_DIFFER", 0, null, null);
        if (leftTranscript.Length != rightTranscript.Length)
            return new(
                "COMBAT_EVENT_HOOK_ACTION_COUNT_MISMATCH", 0,
                Math.Min(leftTranscript.Length, rightTranscript.Length),
                null);

        var compared = 0;
        for (var actionIndex = 0; actionIndex < leftTranscript.Length;
            actionIndex++)
        {
            var expected = leftTranscript[actionIndex];
            var actual = rightTranscript[actionIndex];
            var shared = Math.Min(expected.Length, actual.Length);
            for (var entryIndex = 0; entryIndex < shared; entryIndex++)
            {
                compared++;
                if (expected[entryIndex] != actual[entryIndex])
                    return new(
                        "COMBAT_EVENT_HOOK_TRANSCRIPT_MISMATCH", compared,
                        actionIndex, entryIndex);
            }
            if (expected.Length != actual.Length)
                return new(
                    "COMBAT_EVENT_HOOK_TRANSCRIPT_MISMATCH", compared,
                    actionIndex, shared);
        }
        return new(
            "COMBAT_EVENT_HOOK_TRANSCRIPT_CONSISTENT", compared, null, null);
    }

    private static SimulationProviderReplayDrawnCardIdentityDiffV111
        CompareDrawnCardIdentities(
            SimulationProviderReplayCaptureV111 left,
            SimulationProviderReplayCaptureV111 right)
    {
        var leftCards = left.Mechanics.DrawnCardInstanceIdsByAction;
        var rightCards = right.Mechanics.DrawnCardInstanceIdsByAction;
        var complete =
            SimulationProviderReplayCaptureProvenanceV111
                .HasReplayValidatedDrawIdentityTrace(
                    left.CaptureProvenance) &&
            SimulationProviderReplayCaptureProvenanceV111
                .HasReplayValidatedDrawIdentityTrace(
                    right.CaptureProvenance) &&
            leftCards is not null && rightCards is not null;
        if (!complete)
            return new("DRAWN_CARD_IDENTITIES_UNAVAILABLE", 0, null, null);
        if (!StringComparer.Ordinal.Equals(
                FingerprintActions(left.Mechanics.Actions),
                FingerprintActions(right.Mechanics.Actions)))
            return new("DRAWN_CARD_ACTIONS_DIFFER", 0, null, null);

        var compared = 0;
        for (var actionIndex = 0; actionIndex < leftCards!.Length;
            actionIndex++)
        {
            var a = leftCards[actionIndex];
            var b = rightCards![actionIndex];
            var shared = Math.Min(a.Length, b.Length);
            for (var drawIndex = 0; drawIndex < shared; drawIndex++)
            {
                compared++;
                if (a[drawIndex] != b[drawIndex])
                    return new("DRAWN_CARD_IDENTITY_MISMATCH", compared,
                        actionIndex, drawIndex);
            }
            if (a.Length != b.Length)
                return new("DRAWN_CARD_IDENTITY_COUNT_MISMATCH",
                    compared, actionIndex, shared);
        }
        return new("DRAWN_CARD_IDENTITIES_CONSISTENT", compared, null, null);
    }

    private static SimulationProviderReplayActionBoundaryMechanicsDiffV111
        CompareActionBoundaryMechanics(
            SimulationProviderReplayCaptureV111 left,
            SimulationProviderReplayCaptureV111 right,
            int maximumMechanicsDifferences)
    {
        var leftStates = left.Mechanics.ActionBoundaryStates;
        var rightStates = right.Mechanics.ActionBoundaryStates;
        if (leftStates is null || rightStates is null)
            return new("ACTION_BOUNDARY_MECHANICS_UNAVAILABLE", 0, null,
                null);
        if (!StringComparer.Ordinal.Equals(
                FingerprintActions(left.Mechanics.Actions),
                FingerprintActions(right.Mechanics.Actions)))
            return new("ACTION_BOUNDARY_ACTIONS_DIFFER", 0, null, null);
        if (leftStates.Length != rightStates.Length)
            return new("ACTION_BOUNDARY_COUNT_MISMATCH",
                Math.Min(leftStates.Length, rightStates.Length),
                Math.Min(leftStates.Length, rightStates.Length), null);
        if (leftStates.Length == 0)
            return new("ACTION_BOUNDARY_MECHANICS_EMPTY", 0, null, null);

        for (var index = 0; index < leftStates.Length; index++)
        {
            var comparison = SimulationMechanicsProjectionEngineV111.Compare(
                leftStates[index], rightStates[index],
                maximumMechanicsDifferences);
            if (!comparison.Equivalent)
                return new("ACTION_BOUNDARY_MECHANICS_MISMATCH", index + 1,
                    index, comparison);
        }
        return new("ACTION_BOUNDARY_MECHANICS_CONSISTENT",
            leftStates.Length, null, null);
    }

    private static SimulationProviderReplaySemanticOutcomeDiffV111
        CompareSemanticOutcomes(
            SimulationProviderReplaySemanticOutcomeV111[] left,
            SimulationProviderReplaySemanticOutcomeV111[] right,
            bool available)
    {
        if (!available)
            return new("SEMANTIC_OUTCOME_TRACE_UNAVAILABLE", 0, null, null);
        var shared = Math.Min(left.Length, right.Length);
        for (var index = 0; index < shared; index++)
        {
            var expected = left[index];
            var actual = right[index];
            var mismatchField = expected.HasProviderIndependentIdentity &&
                actual.HasProviderIndependentIdentity
                ? CompareProviderIndependentIdentity(expected, actual)
                : CompareLegacyIdentity(expected, actual);
            if (mismatchField is not null)
                return new("SEMANTIC_OUTCOME_MISMATCH", index, index,
                    mismatchField);
        }
        if (left.Length != right.Length)
            return new("SEMANTIC_OUTCOME_LENGTH_MISMATCH", shared, shared,
                "outcome_count");
        return new("SEMANTIC_OUTCOMES_CONSISTENT", shared, null, null);
    }

    private static string? CompareProviderIndependentIdentity(
        SimulationProviderReplaySemanticOutcomeV111 expected,
        SimulationProviderReplaySemanticOutcomeV111 actual) =>
        expected.TransitionIndex != actual.TransitionIndex
            ? "transition_index"
            : expected.SiteId != actual.SiteId ? "site_id"
            : expected.OperationKind != actual.OperationKind
                ? "operation_kind"
            : expected.OwnerId != actual.OwnerId ? "owner_id"
            : expected.Ordinal != actual.Ordinal ? "ordinal"
            : expected.SupportFingerprint != actual.SupportFingerprint
                ? "support_fingerprint"
            : expected.SemanticOutcomeId != actual.SemanticOutcomeId
                ? "semantic_outcome_id"
            : expected.OutcomeIndex != actual.OutcomeIndex
                ? "outcome_index" : null;

    private static string? CompareLegacyIdentity(
        SimulationProviderReplaySemanticOutcomeV111 expected,
        SimulationProviderReplaySemanticOutcomeV111 actual) =>
        expected.TransitionIndex != actual.TransitionIndex
            ? "transition_index"
            : expected.EventId != actual.EventId ? "event_id"
            : expected.LawId != actual.LawId ? "law_id"
            : expected.OutcomeId != actual.OutcomeId ? "outcome_id"
            : expected.OutcomeIndex != actual.OutcomeIndex
                ? "outcome_index" : null;

    public static SimulationProviderReplayComparisonV111 Compare(
        RunState leftOrigin,
        IReadOnlyList<GameAction> leftActions,
        IEnumerable<RandomBoundaryTranscriptEntryV111> leftBoundaries,
        RunState leftResult,
        RunState rightOrigin,
        IReadOnlyList<GameAction> rightActions,
        IEnumerable<RandomBoundaryTranscriptEntryV111> rightBoundaries,
        RunState rightResult,
        bool requireNativeCursorEquality = false,
        int maximumMechanicsDifferences = 64)
    {
        ArgumentNullException.ThrowIfNull(leftOrigin);
        ArgumentNullException.ThrowIfNull(leftActions);
        ArgumentNullException.ThrowIfNull(leftBoundaries);
        ArgumentNullException.ThrowIfNull(leftResult);
        ArgumentNullException.ThrowIfNull(rightOrigin);
        ArgumentNullException.ThrowIfNull(rightActions);
        ArgumentNullException.ThrowIfNull(rightBoundaries);
        ArgumentNullException.ThrowIfNull(rightResult);
        if (maximumMechanicsDifferences <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maximumMechanicsDifferences));

        var leftActionFingerprint = FingerprintActions(leftActions);
        var rightActionFingerprint = FingerprintActions(rightActions);
        return new SimulationProviderReplayComparisonV111(
            SimulationProviderReplayComparisonV111.CurrentSchemaId,
            leftActionFingerprint,
            rightActionFingerprint,
            SimulationMechanicsProjectionEngineV111.Compare(
                leftOrigin, rightOrigin, maximumMechanicsDifferences),
            RandomBoundaryComparisonV111.CompareOrdered(
                leftBoundaries, rightBoundaries,
                requireNativeCursorEquality),
            SimulationMechanicsProjectionEngineV111.Compare(
                leftResult, rightResult, maximumMechanicsDifferences),
            requireNativeCursorEquality);
    }

    private static string FingerprintActions(
        IReadOnlyList<GameAction> actions)
    {
        var ownedActions = new GameAction[actions.Count];
        for (var index = 0; index < actions.Count; index++)
        {
            var action = actions[index] ?? throw new InvalidDataException(
                "Provider replay action sequence contains a null action.");
            ownedActions[index] = action with { Payload = action.Payload };
        }
        return CanonicalJson.Sha256(ownedActions);
    }
}
