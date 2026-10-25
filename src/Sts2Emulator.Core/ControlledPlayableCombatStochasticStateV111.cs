namespace Sts2Emulator.Core;

internal enum ControlledPlayableLiveDecisionKindV111
{
    LiveOnly,
    TurnIntent,
    Target,
    GeneratedCard,
    DrawInsertion,
    PoolShuffle,
    RandomCost,
    DrawCost,
    PotionGeneration,
    CardTransform
}

/// <summary>
/// Provider cursor, unresolved requests and outcome transcripts for the
/// controlled playable combat session. The session keeps its established
/// flattened properties as projections of this owner.
/// </summary>
public sealed record ControlledPlayableCombatStochasticStateV111
{
    private RandomIndexDecisionV111[] _shuffleTranscript = [];
    private RandomIndexDecisionV111[] _turnTranscript = [];
    private RandomIndexDecisionV111[] _recycleTranscriptPrefix = [];
    private RandomIndexDecisionV111[] _liveTranscript = [];
    private RandomIndexDecisionV111[] _targetTranscript = [];
    private RandomIndexDecisionV111[] _generatedCardTranscript = [];
    private RandomIndexDecisionV111[] _drawInsertionTranscript = [];
    private RandomIndexDecisionV111[] _poolShuffleTranscript = [];
    private RandomIndexDecisionV111[] _randomCostTranscript = [];
    private RandomIndexDecisionV111[] _drawCostTranscript = [];
    private RandomIndexDecisionV111[] _potionGenerationTranscript = [];
    private RandomIndexDecisionV111[] _cardTransformTranscript = [];
    private string[]?[]? _liveTranscriptCandidateLabels;
    private RandomIndexRequestV111? _pending;
    private ControlledRecycleShuffleV111? _recycle;

    public int NextShuffleIndex { get; init; }
    public RandomIndexDecisionV111[] ShuffleTranscript
    {
        get => DeepCopy(_shuffleTranscript)!;
        init => _shuffleTranscript = DeepCopy(value)!;
    }
    public RandomIndexRequestV111? Pending
    {
        get => CloneRequest(_pending);
        init => _pending = CloneRequest(value);
    }
    public RandomIndexDecisionV111[] TurnTranscript
    {
        get => DeepCopy(_turnTranscript)!;
        init => _turnTranscript = DeepCopy(value)!;
    }
    public ControlledRecycleShuffleV111? Recycle
    {
        get => _recycle?.Fork();
        init => _recycle = value?.Fork();
    }
    /// <summary>
    /// Completed recycle decisions. While a recycle is active, its current
    /// segment remains solely in <see cref="Recycle"/>.
    /// </summary>
    public RandomIndexDecisionV111[] RecycleTranscriptPrefix
    {
        get => DeepCopy(_recycleTranscriptPrefix)!;
        init => _recycleTranscriptPrefix = DeepCopy(value)!;
    }
    public RandomIndexDecisionV111[] LiveTranscript
    {
        get => DeepCopy(_liveTranscript)!;
        init => _liveTranscript = DeepCopy(value)!;
    }
    public string[]?[]? LiveTranscriptCandidateLabels
    {
        get => CloneCandidateLabels(_liveTranscriptCandidateLabels);
        init => _liveTranscriptCandidateLabels =
            CloneCandidateLabels(value);
    }
    public RandomIndexDecisionV111[] TargetTranscript
    {
        get => DeepCopy(_targetTranscript)!;
        init => _targetTranscript = DeepCopy(value)!;
    }
    public RandomIndexDecisionV111[] GeneratedCardTranscript
    {
        get => DeepCopy(_generatedCardTranscript)!;
        init => _generatedCardTranscript = DeepCopy(value)!;
    }
    public RandomIndexDecisionV111[] DrawInsertionTranscript
    {
        get => DeepCopy(_drawInsertionTranscript)!;
        init => _drawInsertionTranscript = DeepCopy(value)!;
    }
    public RandomIndexDecisionV111[] PoolShuffleTranscript
    {
        get => DeepCopy(_poolShuffleTranscript)!;
        init => _poolShuffleTranscript = DeepCopy(value)!;
    }
    public RandomIndexDecisionV111[] RandomCostTranscript
    {
        get => DeepCopy(_randomCostTranscript)!;
        init => _randomCostTranscript = DeepCopy(value)!;
    }
    public RandomIndexDecisionV111[] DrawCostTranscript
    {
        get => DeepCopy(_drawCostTranscript)!;
        init => _drawCostTranscript = DeepCopy(value)!;
    }
    public RandomIndexDecisionV111[] PotionGenerationTranscript
    {
        get => DeepCopy(_potionGenerationTranscript)!;
        init => _potionGenerationTranscript = DeepCopy(value)!;
    }
    public RandomIndexDecisionV111[] CardTransformTranscript
    {
        get => DeepCopy(_cardTransformTranscript)!;
        init => _cardTransformTranscript = DeepCopy(value)!;
    }

    public ControlledPlayableCombatStochasticStateV111 Fork() => this with
    {
        ShuffleTranscript = DeepCopy(_shuffleTranscript)!,
        Pending = CloneRequest(_pending),
        TurnTranscript = DeepCopy(_turnTranscript)!,
        Recycle = _recycle?.Fork(),
        RecycleTranscriptPrefix = DeepCopy(_recycleTranscriptPrefix)!,
        LiveTranscript = DeepCopy(_liveTranscript)!,
        TargetTranscript = DeepCopy(_targetTranscript)!,
        GeneratedCardTranscript = DeepCopy(_generatedCardTranscript)!,
        DrawInsertionTranscript = DeepCopy(_drawInsertionTranscript)!,
        PoolShuffleTranscript = DeepCopy(_poolShuffleTranscript)!,
        RandomCostTranscript = DeepCopy(_randomCostTranscript)!,
        DrawCostTranscript = DeepCopy(_drawCostTranscript)!,
        PotionGenerationTranscript = DeepCopy(_potionGenerationTranscript)!,
        CardTransformTranscript = DeepCopy(_cardTransformTranscript)!,
        LiveTranscriptCandidateLabels =
            CloneCandidateLabels(_liveTranscriptCandidateLabels)
    };

    internal ControlledPlayableCombatStochasticStateV111 AppendLiveDecision(
        ControlledPlayableLiveDecisionKindV111 kind,
        RandomIndexDecisionV111 decision,
        string[]? candidateLabels = null)
    {
        ValidateControlledDecision(decision);
        if (candidateLabels is not null)
            ControlledStochasticDecisionV111.FromIndex(
                decision, candidateLabels).Validate();
        if (kind == ControlledPlayableLiveDecisionKindV111.TurnIntent &&
            decision.Request.SiteId != ControlledTurnIntentEngineV111.SiteId)
            throw new InvalidDataException(
                "Controlled playable turn-intent decision has an unexpected site.");
        if (kind == ControlledPlayableLiveDecisionKindV111.LiveOnly &&
            !PrototypeControlledEnemyActionContinuationV111.IsEnemyActionSite(
                decision.Request.SiteId) &&
            decision.Request.SiteId != ControlledRecycleShuffleEngineV111.SiteId)
            throw new InvalidDataException(
                "Controlled playable live decision has no typed transcript owner.");

        var liveTranscript = AppendDecision(_liveTranscript, decision);
        var liveCandidateLabels = AppendCandidateLabels(
            _liveTranscriptCandidateLabels, _liveTranscript.Length,
            candidateLabels);
        var next = kind switch
        {
            ControlledPlayableLiveDecisionKindV111.LiveOnly => this with
            {
                LiveTranscript = liveTranscript
            },
            ControlledPlayableLiveDecisionKindV111.TurnIntent => this with
            {
                TurnTranscript = AppendDecision(_turnTranscript, decision),
                LiveTranscript = liveTranscript
            },
            ControlledPlayableLiveDecisionKindV111.Target => this with
            {
                TargetTranscript = AppendDecision(_targetTranscript, decision),
                LiveTranscript = liveTranscript
            },
            ControlledPlayableLiveDecisionKindV111.GeneratedCard => this with
            {
                GeneratedCardTranscript = AppendDecision(
                    _generatedCardTranscript, decision),
                LiveTranscript = liveTranscript
            },
            ControlledPlayableLiveDecisionKindV111.DrawInsertion => this with
            {
                DrawInsertionTranscript = AppendDecision(
                    _drawInsertionTranscript, decision),
                LiveTranscript = liveTranscript
            },
            ControlledPlayableLiveDecisionKindV111.PoolShuffle => this with
            {
                PoolShuffleTranscript = AppendDecision(
                    _poolShuffleTranscript, decision),
                LiveTranscript = liveTranscript
            },
            ControlledPlayableLiveDecisionKindV111.RandomCost => this with
            {
                RandomCostTranscript = AppendDecision(
                    _randomCostTranscript, decision),
                LiveTranscript = liveTranscript
            },
            ControlledPlayableLiveDecisionKindV111.DrawCost => this with
            {
                DrawCostTranscript = AppendDecision(
                    _drawCostTranscript, decision),
                LiveTranscript = liveTranscript
            },
            ControlledPlayableLiveDecisionKindV111.PotionGeneration => this with
            {
                PotionGenerationTranscript = AppendDecision(
                    _potionGenerationTranscript, decision),
                LiveTranscript = liveTranscript
            },
            ControlledPlayableLiveDecisionKindV111.CardTransform => this with
            {
                CardTransformTranscript = AppendDecision(
                    _cardTransformTranscript, decision),
                LiveTranscript = liveTranscript
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return next with
        {
            LiveTranscriptCandidateLabels = liveCandidateLabels
        };
    }

    internal ControlledPlayableCombatStochasticStateV111 AdvanceOpeningShuffle(
        RandomIndexDecisionV111 decision, int nextShuffleIndex,
        RandomIndexRequestV111? nextPending)
    {
        ValidateControlledDecision(decision);
        if (NextShuffleIndex < 1 || nextShuffleIndex < 0 ||
            decision.Request.SiteId != ControlledPlayableCombatEngineV111.ShuffleSite ||
            decision.Request.StreamId != "run.shuffle" ||
            decision.Request.OwnerId != "player:0" ||
            decision.Request.OperationKind != "bounded_integer" ||
            decision.Request.Ordinal != _shuffleTranscript.Length ||
            decision.Request.SupportCount != NextShuffleIndex + 1 ||
            nextShuffleIndex != NextShuffleIndex - 1 ||
            (nextShuffleIndex < 1) != (nextPending is null) ||
            (nextPending is { } pending &&
             (pending.SiteId != ControlledPlayableCombatEngineV111.ShuffleSite ||
              pending.Ordinal != _shuffleTranscript.Length + 1 ||
              pending.SupportCount != nextShuffleIndex + 1)))
            throw new InvalidDataException(
                "Opening shuffle decision does not advance its saved cursor.");
        return this with
        {
            NextShuffleIndex = nextShuffleIndex,
            ShuffleTranscript = AppendDecision(_shuffleTranscript, decision),
            Pending = nextPending
        };
    }

    private static void ValidateControlledDecision(
        RandomIndexDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        if (decision.ProviderMode != "controlled-v111" ||
            decision.NativeCursorBefore is not null ||
            decision.NativeCursorAfter is not null ||
            decision.ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Controlled playable transcript has invalid provenance.");
    }

    private static RandomIndexDecisionV111[] AppendDecision(
        RandomIndexDecisionV111[] transcript,
        RandomIndexDecisionV111 decision) => transcript.Append(
            CloneDecision(decision)).ToArray();

    private static RandomIndexRequestV111? CloneRequest(
        RandomIndexRequestV111? request) => request is null ? null : request with
    {
        OrderedCandidates = (int[])request.OrderedCandidates.Clone()
    };

    private static RandomIndexDecisionV111[]? DeepCopy(
        RandomIndexDecisionV111[]? decisions) => decisions?.Select(decision =>
        decision is null
            ? null!
            : CloneDecision(decision)).ToArray();

    private static string[]?[]? AppendCandidateLabels(
        string[]?[]? labels, int existingDecisionCount,
        string[]? candidateLabels)
    {
        if (labels is not null && labels.Length != existingDecisionCount)
            throw new InvalidDataException(
                "Live transcript labels do not align with prior decisions.");
        if (labels is null && candidateLabels is null)
            return null;
        var prefix = labels is null
            ? new string[]?[existingDecisionCount]
            : CloneCandidateLabels(labels)!;
        return prefix.Append(candidateLabels is null
            ? null : (string[])candidateLabels.Clone()).ToArray();
    }

    private static string[]?[]? CloneCandidateLabels(
        string[]?[]? labels) => labels?.Select(candidateLabels =>
            candidateLabels is null
                ? null
                : (string[])candidateLabels.Clone()).ToArray();

    private static RandomIndexDecisionV111 CloneDecision(
        RandomIndexDecisionV111 decision) => decision with
    {
        Request = decision.Request with
        {
            OrderedCandidates = (int[])decision.Request.OrderedCandidates.Clone()
        }
    };
}
