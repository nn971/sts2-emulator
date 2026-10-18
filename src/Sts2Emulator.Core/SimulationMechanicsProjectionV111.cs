namespace Sts2Emulator.Core;

public sealed record SimulationMechanicsDifferenceV111(
    string Path, string LeftJson, string RightJson);

public sealed record SimulationMechanicsComparisonV111(
    string LeftFingerprint,
    string RightFingerprint,
    SimulationMechanicsDifferenceV111[] Differences,
    bool DifferencesTruncated)
{
    public const string SchemaId =
        "sts2-v111-simulation-mechanics-comparison-v4";

    public bool Equivalent => StringComparer.Ordinal.Equals(
        LeftFingerprint, RightFingerprint);
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
        PendingPoolShuffleContinuation = null);

/// <summary>Provider-independent progress for one suspended draw operation.
/// Candidate supports and selected provider outcomes remain outside the view.
/// </summary>
public sealed record SimulationDrawContinuationProjectionV111(
    PrototypeControlledSemanticDrawV111? SemanticDraw,
    SimulationDrawCostContinuationProjectionV111? DrawCost,
    SimulationDrawInsertionContinuationProjectionV111? DrawInsertion);

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
        // The owners contain arrays for compatibility with existing update
        // code, so project a fork to keep this read-only view from exposing
        // mutable aliases into the caller's snapshot.
        var snapshot = run.Fork();
        var world = snapshot.World;
        var combat = world?.Combat?.Mechanics;
        SimulationDrawContinuationProjectionV111? pendingDrawContinuation = null;
        SimulationPoolShuffleContinuationProjectionV111?
            pendingPoolShuffleContinuation = null;
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
            if (semanticDraw is not null || costProgress is not null ||
                insertionProgress is not null)
                pendingDrawContinuation =
                    new SimulationDrawContinuationProjectionV111(
                        semanticDraw, costProgress, insertionProgress);

            var pipeline = combat.AutomaticPipelineContinuation;
            if (pipeline is not null)
            {
                var stochastic = pipeline.Stochastic;
                pipeline = pipeline with
                {
                    Stochastic = new PrototypeAutomaticPipelineStochasticStateV111(
                        HandDrawProgress: stochastic.HandDrawProgress)
                };
            }

            combat = combat with
            {
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
            pendingPoolShuffleContinuation);
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
        PendingCombatChoiceState? pending) => pending is null
            ? null
            : pending with
            {
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
                ControlledSemanticDraw = null
            };

    private static PrototypeQueuedOperation MechanicsOperation(
        PrototypeQueuedOperation operation) => operation with
    {
        // The resolver mode is provider policy; the operation and selected
        // mechanics parameters remain part of the comparison.
        GeneratedChoiceUsesNativeCardGenerationRng = false,
        DeferredDrawnCardInstanceIds = operation.DeferredDrawnCardInstanceIds
            is null ? null : (long[])operation.DeferredDrawnCardInstanceIds.Clone()
    };

    private static PrototypePersonalHiveDamageContinuationV111?
        MechanicsHiveDamage(
            PrototypePersonalHiveDamageContinuationV111? continuation) =>
        continuation is null ? null : continuation with
        {
            Operation = MechanicsOperation(continuation.Operation),
            SplashTarget = continuation.SplashTarget.Fork()
        };
}
