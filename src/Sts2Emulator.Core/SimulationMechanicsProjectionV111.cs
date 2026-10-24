namespace Sts2Emulator.Core;

public sealed record SimulationMechanicsDifferenceV111(
    string Path, string LeftJson, string RightJson);

public sealed record SimulationMechanicsComparisonV111(
    string LeftFingerprint,
    string RightFingerprint,
    SimulationMechanicsDifferenceV111[] Differences,
    bool DifferencesTruncated)
{
    private SimulationMechanicsDifferenceV111[] _differences =
        CloneDifferences(Differences);

    public const string SchemaId =
        "sts2-v111-simulation-mechanics-comparison-v19";

    public SimulationMechanicsDifferenceV111[] Differences
    {
        get => CloneDifferences(_differences);
        init => _differences = CloneDifferences(value);
    }

    public bool Equivalent => StringComparer.Ordinal.Equals(
        LeftFingerprint, RightFingerprint);

    private static SimulationMechanicsDifferenceV111[] CloneDifferences(
        SimulationMechanicsDifferenceV111[] differences)
    {
        ArgumentNullException.ThrowIfNull(differences);
        return (SimulationMechanicsDifferenceV111[])differences.Clone();
    }
}

/// <summary>
/// Provider-independent projection of one run's committed game mechanics.
/// Nested domain owners are projected through their existing mechanics
/// envelopes; unresolved provider payloads and private stochastic state are
/// omitted.
/// </summary>
public sealed record SimulationMechanicsProjectionV111(
    RunMechanicsStateV111 Run,
    RunWorldMechanicsStateV111? World,
    CombatMechanicsStateV111? Combat,
    EventMechanicsStateV111? Event,
    SimulationDrawContinuationProjectionV111? PendingDrawContinuation = null,
    SimulationPoolShuffleContinuationProjectionV111?
        PendingPoolShuffleContinuation = null,
    SimulationPendingChoiceMechanicsProjectionV111?
        PendingChoiceMechanics = null,
    SemanticDeckPublicViewV111? PublicDrawPileKnowledge = null,
    SimulationPendingDeckOperationProjectionV111?
        PendingDeckOperation = null,
    SimulationAutomaticHandDrawProgressProjectionV111?
        AutomaticHandDrawProgress = null,
    SimulationControlledTurnIntentProgressProjectionV111?
        ControlledTurnIntentProgress = null,
    SimulationControlledEnemyActionProgressProjectionV111?
        ControlledEnemyActionProgress = null,
    SimulationPendingRunActionMechanicsProjectionV111?
        PendingRunAction = null,
    SimulationPendingEventRewardMechanicsProjectionV111?
        PendingEventRewardChoice = null,
    SimulationPendingEventEffectMechanicsProjectionV111?
        PendingEventEffectChoice = null);

/// <summary>Provider-independent progress for a suspended run action. The
/// action identity and map boundary remain visible while its request support
/// and selected stochastic prefix are omitted.</summary>
public sealed record SimulationPendingRunActionMechanicsProjectionV111(
    string ActionId, string? MapNodeId, GameAction? RunAction);

/// <summary>Provider-independent progress for a suspended post-reward event
/// action. The committed logical prefix length remains visible; random
/// requests and selected values remain provider state.</summary>
public sealed record SimulationPendingEventRewardMechanicsProjectionV111(
    string EventId, string ChoiceId, int DecisionPrefixLength,
    GameAction? ResumeAction);

/// <summary>Provider-independent progress and queued consequences for a
/// suspended event-effect sequence.</summary>
public sealed record SimulationPendingEventEffectMechanicsProjectionV111(
    string EventId, string ChoiceId, int NextEffectIndex,
    int EffectDecisionStartIndex, string[] QueuedPotionIds,
    PrototypePendingEventDeckChoiceState[] QueuedDeckChoices,
    RewardState? DeferredReward, int DeferredHpLoss,
    int DeferredGoldLoss, string? DeferredCardId);

/// <summary>
/// Provider-independent cursor for a pending semantic deck request. The
/// operation and position identify mechanics progress; candidate identities,
/// pool membership, and the selected outcome remain provider data.
/// </summary>
public sealed record SimulationPendingDeckOperationProjectionV111(
    string Purpose, int TopOffset);

/// <summary>Provider-independent progress for one suspended draw operation.
/// Candidate supports and selected provider outcomes remain outside the view.
/// </summary>
public sealed record SimulationDrawContinuationProjectionV111(
    SimulationSemanticDrawProgressProjectionV111? SemanticDraw,
    SimulationDrawCostContinuationProjectionV111? DrawCost,
    SimulationDrawInsertionContinuationProjectionV111? DrawInsertion);

/// <summary>Provider-independent progress for a suspended semantic draw.
/// Provider segment tags are omitted while effect timing, deferred keyword
/// application, and Stratagem stage remain visible.</summary>
public sealed record SimulationSemanticDrawProgressProjectionV111(
    int RemainingCount,
    bool FromHandDraw,
    int EventDepth,
    PrototypeCardKeywordOverrideSpec? DrawnCardKeyword,
    long[]? DeferredKeywordCardInstanceIds,
    bool StratagemSelectionPending,
    int? DrawEqualSelectionCount);

/// <summary>Provider-independent remaining-card progress for an active
/// automatic hand draw. Provider mode and semantic requested-count metadata
/// are excluded; the active pipeline cursor supplies the stage.</summary>
public sealed record SimulationAutomaticHandDrawProgressProjectionV111(
    int RemainingCount);

/// <summary>Provider-independent enemy cursor for a suspended initial or
/// next-turn intent pass. The selected intent tickets remain in combat
/// mechanics; provider requests and transcript details are omitted.</summary>
public sealed record SimulationControlledTurnIntentProgressProjectionV111(
    int NextEnemyIndex);

/// <summary>Provider-independent operation and completed-request count for a
/// suspended enemy-action pipeline. Request identity, support and outcomes
/// remain provider data.</summary>
public sealed record SimulationControlledEnemyActionProgressProjectionV111(
    ControlledEnemyActionMechanicsOperationV111 Operation,
    int CompletedChoiceCount);

/// <summary>Provider-independent mechanics cursor for a deferred draw-pile
/// insertion. The eligible pile, candidate support and random request identity
/// remain provider data and are excluded.</summary>
public sealed record SimulationDrawInsertionContinuationProjectionV111(
    PrototypeQueuedOperation Operation,
    string Stage,
    string? SelectedCardId,
    PrototypeAfterShuffleRelicInsertionV111? ActiveAfterShuffleRelic,
    PrototypeAfterShuffleRelicInsertionV111[] CompletedAfterShuffleRelics,
    PrototypeAfterShuffleRelicInsertionV111[] RemainingAfterShuffleRelics,
    PrototypePersonalHiveDamageContinuationV111? PersonalHiveDamage,
    int? AutomaticHandDrawRemaining);

/// <summary>Provider-independent progress for one pending full-pool shuffle.
/// The eligible pool and partial permutation remain provider data.</summary>
public sealed record SimulationPoolShuffleContinuationProjectionV111(
    PrototypeQueuedOperation Operation,
    int CompletedSwapCount,
    int SelectedCount);

/// <summary>Mechanics-owned details for pending choices whose active
/// operation would otherwise be lost when provider payloads are removed.</summary>
public sealed record SimulationPendingChoiceMechanicsProjectionV111(
    SimulationRandomTargetMechanicsProgressV111? RandomTarget,
    SimulationRandomPileCardMechanicsProgressV111? RandomPileCard,
    PrototypeQueuedOperation? GeneratedCardOperation,
    PrototypeQueuedOperation? PotionGenerationOperation,
    SimulationCardTransformMechanicsProgressV111? CardTransform,
    SimulationRandomCostMechanicsProgressV111? RandomCost,
    SimulationCardShortlistMechanicsProgressV111? CardShortlist);

/// <summary>Provider-independent cursor for a random physical-card choice.
/// Candidate identities and their support fingerprint remain provider data.</summary>
public sealed record SimulationRandomPileCardMechanicsProgressV111(
    string Operation, int OccurrenceOrdinal);

public sealed record SimulationRandomTargetMechanicsProgressV111(
    string Kind,
    long? SlyCardInstanceId,
    long? AutoPlayCardInstanceId);

public sealed record SimulationCardTransformMechanicsProgressV111(
    PrototypeCardSelectionSpec Selection,
    long[] SelectedCardInstanceIds,
    int NextTransformIndex);

public sealed record SimulationRandomCostMechanicsProgressV111(
    PrototypeQueuedOperation Operation,
    int NextCardIndex);

public sealed record SimulationCardShortlistMechanicsProgressV111(
    PrototypeQueuedOperation Operation,
    int RequiredCount,
    int SelectedCount);

/// <summary>Mechanics progress for a card already drawn and awaiting a cost
/// choice. The cost bound and provider support are intentionally omitted.
/// </summary>
public sealed record SimulationDrawCostContinuationProjectionV111(
    long CardInstanceId, int RemainingCount, bool FromHandDraw,
    int EventDepth, string Stage);

/// <summary>
/// Builds stable provider-independent mechanics projections for replay and
/// cross-provider comparisons. Callers should compare states reached after
/// applying the same semantic outcomes.
/// </summary>
public static class SimulationMechanicsProjectionEngineV111
{
    public static SimulationMechanicsProjectionV111 Project(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        // A provider-neutral projection is meaningful only when the complete
        // mechanics/provider owner is internally consistent. Validate before
        // removing provider state from the comparison view.
        run.Validate();
        // The owners contain arrays for compatibility with existing update
        // code, so project a fork to keep this read-only view from exposing
        // mutable aliases into the caller's snapshot.
        var snapshot = run.Fork();
        var world = snapshot.World;
        var combatState = world?.Combat;
        var combat = combatState?.Mechanics;
        SimulationDrawContinuationProjectionV111? pendingDrawContinuation = null;
        SimulationAutomaticHandDrawProgressProjectionV111?
            automaticHandDrawProgress = null;
        SimulationPoolShuffleContinuationProjectionV111?
            pendingPoolShuffleContinuation = null;
        SimulationPendingChoiceMechanicsProjectionV111?
            pendingChoiceMechanics = null;
        SimulationControlledTurnIntentProgressProjectionV111?
            controlledTurnIntentProgress = null;
        SimulationControlledEnemyActionProgressProjectionV111?
            controlledEnemyActionProgress = null;
        var controlledRunAction =
            snapshot.Stochastic.ControlledRunActionChoice;
        var pendingRunAction = controlledRunAction is null
            ? null
            : new SimulationPendingRunActionMechanicsProjectionV111(
                controlledRunAction.ActionId,
                controlledRunAction.MapNodeId,
                MechanicsAction(controlledRunAction.RunAction));
        var controlledEventReward =
            snapshot.Stochastic.ControlledEventRewardChoice;
        var pendingEventReward = controlledEventReward is null
            ? null
            : new SimulationPendingEventRewardMechanicsProjectionV111(
                controlledEventReward.EventId,
                controlledEventReward.ChoiceId,
                controlledEventReward.DecisionPrefixLength,
                MechanicsAction(controlledEventReward.ResumeAction));
        SimulationPendingEventEffectMechanicsProjectionV111?
            pendingEventEffect = null;
        if (world?.Event?.Stochastic.ControlledChoice is
            { } controlledEventEffect)
        {
            var mechanics = controlledEventEffect.Mechanics;
            pendingEventEffect =
                new SimulationPendingEventEffectMechanicsProjectionV111(
                    mechanics.EventId, mechanics.ChoiceId,
                    mechanics.NextEffectIndex,
                    mechanics.EffectDecisionStartIndex,
                    mechanics.QueuedPotionIds,
                    mechanics.QueuedDeckChoices,
                    mechanics.DeferredReward?.Fork(),
                    mechanics.DeferredHpLoss,
                    mechanics.DeferredGoldLoss,
                    mechanics.DeferredCardId);
        }
        if (combat is not null)
        {
            var pendingChoice = combat.PendingChoice;
            var choiceDraw = pendingChoice?.ControlledSemanticDraw;
            var insertion = pendingChoice?.ControlledDrawInsertion;
            var insertionDraw = insertion?.SemanticDrawContinuation;
            if (choiceDraw is not null && insertionDraw is not null)
                throw new InvalidDataException(
                    "A pending combat choice has overlapping semantic draw continuations.");
            var semanticDraw = (choiceDraw ?? insertionDraw)?.Fork();
            var insertionProgress = insertion is null
                ? null
                : new SimulationDrawInsertionContinuationProjectionV111(
                    MechanicsOperation(insertion.Operation),
                    insertion.Stage,
                    insertion.SelectedCardId,
                    insertion.AfterShuffleRelic is null
                        ? null : insertion.AfterShuffleRelic with { },
                    (insertion.CompletedAfterShuffleRelics ?? [])
                        .Select(item => item with { }).ToArray(),
                    (insertion.RemainingAfterShuffleRelics ?? [])
                        .Select(item => item with { }).ToArray(),
                    MechanicsHiveDamage(
                        insertion.PersonalHiveDamageContinuation),
                    insertion.AutomaticHandDrawRemaining);
            var drawCost = pendingChoice?.ControlledDrawCost;
            var costProgress = drawCost is null
                ? null
                : new SimulationDrawCostContinuationProjectionV111(
                    drawCost.CardInstanceId, drawCost.RemainingCount,
                    drawCost.FromHandDraw, drawCost.EventDepth,
                    drawCost.Stage);
            if (pendingChoice?.ControlledPoolShuffle is { } poolShuffle)
            {
                if (poolShuffle.OriginalCardIds is null ||
                    poolShuffle.CurrentCardIds is null ||
                    poolShuffle.OriginalCardIds.Length <= 1 ||
                    poolShuffle.CurrentCardIds.Length !=
                        poolShuffle.OriginalCardIds.Length ||
                    poolShuffle.NextSwapIndex < 1 ||
                    poolShuffle.NextSwapIndex >=
                        poolShuffle.OriginalCardIds.Length ||
                    poolShuffle.SelectedCount <= 0)
                    throw new InvalidDataException(
                        "Pending pool-shuffle mechanics progress is malformed.");
                int completedSwapCount = poolShuffle.OriginalCardIds.Length -
                    poolShuffle.NextSwapIndex - 1;
                pendingPoolShuffleContinuation =
                    new SimulationPoolShuffleContinuationProjectionV111(
                        MechanicsOperation(poolShuffle.Operation),
                        completedSwapCount, poolShuffle.SelectedCount);
            }
            if (pendingChoice is not null)
                pendingChoiceMechanics = ProjectPendingChoiceMechanics(
                    pendingChoice);
            if (semanticDraw is not null || costProgress is not null ||
                insertionProgress is not null)
                pendingDrawContinuation =
                    new SimulationDrawContinuationProjectionV111(
                        semanticDraw is null
                            ? null : ProjectSemanticDrawProgress(semanticDraw),
                        costProgress, insertionProgress);

            var pipeline = combat.AutomaticPipelineContinuation;
            if (pipeline is not null)
            {
                var stochastic = pipeline.Stochastic;
                if (stochastic.ControlledTurnIntents is { } turnIntents)
                    controlledTurnIntentProgress =
                        new SimulationControlledTurnIntentProgressProjectionV111(
                            turnIntents.NextEnemyIndex);
                if (stochastic.ControlledEnemyAction is { } enemyAction)
                {
                    if (!PrototypeControlledEnemyActionContinuationV111
                            .TryGetMechanicsOperation(
                                enemyAction.PendingRequest.SiteId,
                                out var operation))
                        throw new InvalidDataException(
                            "Unsupported controlled enemy-action mechanics operation.");
                    controlledEnemyActionProgress =
                        new SimulationControlledEnemyActionProgressProjectionV111(
                            operation,
                            enemyAction.Decisions.Length);
                }
                if (stochastic.HandDrawProgress is { } drawProgress)
                {
                    drawProgress.Validate();
                    if (drawProgress.RemainingCount is { } remaining)
                        automaticHandDrawProgress =
                            new SimulationAutomaticHandDrawProgressProjectionV111(
                                remaining);
                }
                pipeline = pipeline with
                {
                    Stochastic = new PrototypeAutomaticPipelineStochasticStateV111()
                };
            }

            combat = combat with
            {
                // Keep mechanics-owned membership while removing Native
                // order from this comparison-only projection.
                DrawPile = combat.DrawPile.Order().ToArray(),
                PendingChoice = WithoutProviderPayload(
                    combat.PendingChoice),
                AutomaticPipelineContinuation = pipeline
            };
        }

        var runMechanics = snapshot.Mechanics with
        {
            // The seed selects provider realizations, not game mechanics.
            RunSeed = string.Empty,
            World = null
        };
        RunWorldMechanicsStateV111? worldMechanics = world is null
            ? null
            : world.Mechanics with
            {
                Combat = null,
                Event = null
            };

        return new SimulationMechanicsProjectionV111(
            runMechanics,
            worldMechanics,
            combat,
            world?.Event?.Mechanics,
            pendingDrawContinuation,
            pendingPoolShuffleContinuation,
            pendingChoiceMechanics,
            combatState?.Stochastic.SemanticDeck is { } semanticDeck
                ? SemanticDeckEngineV111.Observe(semanticDeck)
                : null,
            combatState?.Stochastic.SemanticDeck?.Pending is { } pendingDeck
                ? new SimulationPendingDeckOperationProjectionV111(
                    pendingDeck.Purpose, pendingDeck.TopOffset)
                : null,
            automaticHandDrawProgress,
            controlledTurnIntentProgress,
            controlledEnemyActionProgress,
            pendingRunAction,
            pendingEventReward,
            pendingEventEffect);
    }

    public static string Fingerprint(RunState run) =>
        CanonicalJson.Sha256(Project(run));

    public static bool MechanicallyEquivalent(
        RunState left, RunState right) =>
        StringComparer.Ordinal.Equals(
            Fingerprint(left), Fingerprint(right));

    /// <summary>
    /// Compares provider-independent mechanics and reports the first bounded
    /// set of differing JSON-pointer paths. This is a replay diagnostic, not
    /// an assertion that either provider made equivalent choices.
    /// </summary>
    public static SimulationMechanicsComparisonV111 Compare(
        RunState left, RunState right, int maximumDifferences = 64)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (maximumDifferences <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumDifferences));

        var leftProjection = Project(left);
        var rightProjection = Project(right);
        var leftFingerprint = CanonicalJson.Sha256(leftProjection);
        var rightFingerprint = CanonicalJson.Sha256(rightProjection);
        var differences = new List<SimulationMechanicsDifferenceV111>();
        bool truncated = false;
        if (!StringComparer.Ordinal.Equals(
                leftFingerprint, rightFingerprint))
        {
            using var leftDocument = System.Text.Json.JsonDocument.Parse(
                CanonicalJson.Serialize(leftProjection));
            using var rightDocument = System.Text.Json.JsonDocument.Parse(
                CanonicalJson.Serialize(rightProjection));
            CompareJson(
                leftDocument.RootElement, true,
                rightDocument.RootElement, true,
                string.Empty, differences, maximumDifferences,
                ref truncated);
        }

        return new SimulationMechanicsComparisonV111(
            leftFingerprint, rightFingerprint, differences.ToArray(),
            truncated);
    }

    private static void CompareJson(
        System.Text.Json.JsonElement left, bool hasLeft,
        System.Text.Json.JsonElement right, bool hasRight,
        string path,
        List<SimulationMechanicsDifferenceV111> differences,
        int maximumDifferences, ref bool truncated)
    {
        if (truncated) return;
        if (!hasLeft || !hasRight)
        {
            AddDifference(path, left, hasLeft, right, hasRight,
                differences, maximumDifferences, ref truncated);
            return;
        }
        if (left.ValueKind != right.ValueKind)
        {
            AddDifference(path, left, true, right, true,
                differences, maximumDifferences, ref truncated);
            return;
        }

        if (left.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            var leftProperties = left.EnumerateObject().ToDictionary(
                property => property.Name, property => property.Value,
                StringComparer.Ordinal);
            var rightProperties = right.EnumerateObject().ToDictionary(
                property => property.Name, property => property.Value,
                StringComparer.Ordinal);
            foreach (var name in leftProperties.Keys
                         .Union(rightProperties.Keys, StringComparer.Ordinal)
                         .OrderBy(name => name, StringComparer.Ordinal))
            {
                if (truncated) return;
                bool foundLeft = leftProperties.TryGetValue(name,
                    out var leftValue);
                bool foundRight = rightProperties.TryGetValue(name,
                    out var rightValue);
                CompareJson(leftValue, foundLeft, rightValue, foundRight,
                    path + "/" + EscapeJsonPointer(name), differences,
                    maximumDifferences, ref truncated);
            }
            return;
        }

        if (left.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            int leftLength = left.GetArrayLength();
            int rightLength = right.GetArrayLength();
            if (leftLength != rightLength)
            {
                AddRawDifference(path + "/length",
                    leftLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    rightLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    differences, maximumDifferences, ref truncated);
                if (truncated) return;
            }
            int commonLength = Math.Min(leftLength, rightLength);
            for (int index = 0; index < commonLength; index++)
            {
                if (truncated) return;
                CompareJson(left[index], true, right[index], true,
                    path + "/" + index.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    differences, maximumDifferences, ref truncated);
            }
            return;
        }

        if (!JsonScalarsEqual(left, right))
            AddDifference(path, left, true, right, true,
                differences, maximumDifferences, ref truncated);
    }

    private static bool JsonScalarsEqual(
        System.Text.Json.JsonElement left,
        System.Text.Json.JsonElement right) => left.ValueKind switch
    {
        System.Text.Json.JsonValueKind.String => StringComparer.Ordinal.Equals(
            left.GetString(), right.GetString()),
        System.Text.Json.JsonValueKind.Number or
            System.Text.Json.JsonValueKind.True or
            System.Text.Json.JsonValueKind.False or
            System.Text.Json.JsonValueKind.Null =>
                StringComparer.Ordinal.Equals(left.GetRawText(), right.GetRawText()),
        _ => true
    };

    private static string EscapeJsonPointer(string segment) =>
        segment.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);

    private static void AddDifference(
        string path,
        System.Text.Json.JsonElement left, bool hasLeft,
        System.Text.Json.JsonElement right, bool hasRight,
        List<SimulationMechanicsDifferenceV111> differences,
        int maximumDifferences, ref bool truncated) =>
        AddRawDifference(path,
            hasLeft ? left.GetRawText() : "<missing>",
            hasRight ? right.GetRawText() : "<missing>",
            differences, maximumDifferences, ref truncated);

    private static void AddRawDifference(
        string path, string leftJson, string rightJson,
        List<SimulationMechanicsDifferenceV111> differences,
        int maximumDifferences, ref bool truncated)
    {
        if (differences.Count >= maximumDifferences)
        {
            truncated = true;
            return;
        }
        differences.Add(new SimulationMechanicsDifferenceV111(
            path.Length == 0 ? "/" : path, leftJson, rightJson));
    }

    private static PendingCombatChoiceState? WithoutProviderPayload(
        PendingCombatChoiceState? pending)
    {
        if (pending is null) return null;
        return pending with
        {
            Continuation = pending.Continuation
                .Select(MechanicsOperation).ToArray(),
            EventDispatchContinuation = MechanicsEventDispatch(
                pending.EventDispatchContinuation),
            OuterChoiceContinuation = MechanicsChoiceContinuation(
                pending.OuterChoiceContinuation),
            ControlledTarget = null,
            ControlledGeneration = null,
            ControlledPotionGeneration = null,
            ControlledCardTransform = null,
            ControlledDrawInsertion = null,
            ControlledPoolShuffle = null,
            ControlledRandomCost = null,
            ControlledDrawCost = null,
            ControlledCardShortlist = null,
            ControlledRandomPileCard = null,
            ControlledSemanticDraw = null,
            SemanticStratagemDrawSelection = false,
            AutomaticSemanticStratagemDrawSelection = false,
            StandaloneSemanticDrawHookChoice = false,
            AutomaticSemanticDrawHookChoice = false
        };
    }

    private static PrototypeChoiceResolutionContinuationState?
        MechanicsChoiceContinuation(
            PrototypeChoiceResolutionContinuationState? continuation) =>
        continuation is null
            ? null
            : continuation with
            {
                Operations = continuation.Operations
                    .Select(MechanicsOperation).ToArray(),
                EventDispatchContinuation = MechanicsEventDispatch(
                    continuation.EventDispatchContinuation),
                Parent = MechanicsChoiceContinuation(continuation.Parent),
                EnemyDefeatFollowUp =
                    continuation.EnemyDefeatFollowUp is { } followUp
                        ? followUp with
                        {
                            Operation = MechanicsOperation(
                                followUp.Operation)
                        }
                        : null,
                DamageRepeatContinuation =
                    MechanicsDamageRepeat(
                        continuation.DamageRepeatContinuation)
            };

    private static PrototypeRepeatedDamageContinuationV111?
        MechanicsDamageRepeat(
            PrototypeRepeatedDamageContinuationV111? continuation) =>
        continuation is null
            ? null
            : continuation with
            {
                Operation = MechanicsOperation(continuation.Operation),
                CurrentRoundTargetIds =
                    continuation.CurrentRoundTargetIds
            };

    private static PrototypeEventDispatchContinuationState?
        MechanicsEventDispatch(
            PrototypeEventDispatchContinuationState? continuation) =>
        continuation is null
            ? null
            : continuation with
            {
                CurrentSubscriber = MechanicsSubscriber(
                    continuation.CurrentSubscriber),
                RemainingSubscribers = continuation.RemainingSubscribers
                    .Select(MechanicsSubscriber).ToArray()
            };

    private static PrototypeEventSubscriberState MechanicsSubscriber(
        PrototypeEventSubscriberState subscriber)
    {
        var forked = subscriber.Fork();
        return forked with
        {
            Effects = forked.Effects.Select(effect => effect with
            {
                GeneratedChoiceUsesNativeCardGenerationRng = false
            }).ToArray()
        };
    }

    private static PrototypeQueuedOperation MechanicsOperation(
        PrototypeQueuedOperation operation) => operation with
    {
        // The resolver mode is provider policy; the operation and selected
        // mechanics parameters remain part of the comparison.
        GeneratedChoiceUsesNativeCardGenerationRng = false,
        StandaloneSemanticDrawSegment = false,
        SemanticStratagemDrawSelection = false,
        AutomaticSemanticStratagemDrawSelection = false,
        DeferredDrawnCardInstanceIds = operation.DeferredDrawnCardInstanceIds
            is null ? null : (long[])operation.DeferredDrawnCardInstanceIds.Clone()
    };

    private static SimulationSemanticDrawProgressProjectionV111
        ProjectSemanticDrawProgress(
            PrototypeControlledSemanticDrawV111 draw) => new(
                draw.RemainingCount,
                draw.FromHandDraw,
                draw.EventDepth,
                draw.DrawnCardKeyword is null
                    ? null : draw.DrawnCardKeyword with { },
                draw.DeferredKeywordCardInstanceIds is null
                    ? null : (long[])draw.DeferredKeywordCardInstanceIds.Clone(),
                draw.StratagemSelectionPending,
                draw.DrawEqualSelectionCount);

    private static SimulationPendingChoiceMechanicsProjectionV111?
        ProjectPendingChoiceMechanics(PendingCombatChoiceState choice)
    {
        var target = choice.ControlledTarget;
        var randomPileCard = choice.ControlledRandomPileCard;
        var generation = choice.ControlledGeneration;
        var potionGeneration = choice.ControlledPotionGeneration;
        var transform = choice.ControlledCardTransform;
        var randomCost = choice.ControlledRandomCost;
        var shortlist = choice.ControlledCardShortlist;
        if (target is null && randomPileCard is null && generation is null &&
            potionGeneration is null && transform is null &&
            randomCost is null && shortlist is null)
            return null;

        return new SimulationPendingChoiceMechanicsProjectionV111(
            target is null ? null : new SimulationRandomTargetMechanicsProgressV111(
                target.Kind, target.SlyCardInstanceId,
                target.AutoPlayCardInstanceId),
            randomPileCard is null ? null :
                new SimulationRandomPileCardMechanicsProgressV111(
                    SemanticRandomPileCardEngineV111.MechanicsOperationId,
                    randomPileCard.Ordinal),
            generation is null ? null : MechanicsOperation(generation.Operation),
            potionGeneration is null
                ? null : MechanicsOperation(potionGeneration.Operation),
            transform is null ? null :
                new SimulationCardTransformMechanicsProgressV111(
                    transform.OriginalChoice.Selection with { },
                    (long[])transform.SelectedCardInstanceIds.Clone(),
                    transform.NextTransformIndex),
            randomCost is null ? null :
                new SimulationRandomCostMechanicsProgressV111(
                    MechanicsOperation(randomCost.Operation),
                    randomCost.NextCardIndex),
            shortlist is null ? null :
                new SimulationCardShortlistMechanicsProgressV111(
                    MechanicsOperation(shortlist.Operation),
                    shortlist.RequiredCount,
                    shortlist.SelectedCandidateCardInstanceIds.Length));
    }

    private static PrototypePersonalHiveDamageContinuationV111?
        MechanicsHiveDamage(
            PrototypePersonalHiveDamageContinuationV111? continuation) =>
        continuation is null ? null : continuation with
        {
            Operation = MechanicsOperation(continuation.Operation),
            SplashTarget = continuation.SplashTarget.Fork()
        };

    private static GameAction? MechanicsAction(GameAction? action) =>
        action is null ? null : action with
        {
            Payload = action.Payload.Clone()
        };
}
