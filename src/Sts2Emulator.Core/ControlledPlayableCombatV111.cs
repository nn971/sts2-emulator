using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// A playable, controlled opening fixture assembled from a VERIFIED HP→intent
/// continuation. Only opening Fisher–Yates tickets are resumable in v1.
/// Ordinary player actions use the actual engine, guarded against unowned RNG.
/// It is deliberately not a complete native combat-entry hook implementation.
/// </summary>
public sealed record ControlledPlayableCombatV111(
    string Seed,
    ControlledIntentStateV111 Origin,
    RunState Run,
    int NextShuffleIndex,
    RandomIndexDecisionV111[] ShuffleTranscript,
    string[] PlayerActions,
    RandomIndexRequestV111? Pending,
    string Mode = "controlled-v111-playable-fixture-v1")
{
    /// <summary>
    /// Atomic mechanics/provider envelope for the playable combat fixture.
    /// Existing snapshot properties remain flattened projections.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SimulationStateV111<ControlledPlayableCombatMechanicsStateV111,
        ControlledPlayableCombatStochasticStateV111> Simulation { get; init; } =
        new(
            new ControlledPlayableCombatMechanicsStateV111(
                Seed, Origin, Run, PlayerActions, Mode),
            new ControlledPlayableCombatStochasticStateV111
            {
                NextShuffleIndex = NextShuffleIndex,
                ShuffleTranscript = ShuffleTranscript,
                Pending = Pending
            });

    [System.Text.Json.Serialization.JsonIgnore]
    public ControlledPlayableCombatMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    /// <summary>
    /// Provider-owned pending requests, cursors and decision history. The
    /// existing public fields below remain flattened snapshot projections.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ControlledPlayableCombatStochasticStateV111 Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public string Seed
    {
        get => Mechanics.Seed;
        init => Mechanics = Mechanics with { Seed = value };
    }

    public ControlledIntentStateV111 Origin
    {
        get => Mechanics.Origin;
        init => Mechanics = Mechanics with { Origin = value };
    }

    public RunState Run
    {
        get => Mechanics.Run;
        init => Mechanics = Mechanics with { Run = value };
    }

    public string[] PlayerActions
    {
        get => Mechanics.PlayerActions;
        init => Mechanics = Mechanics with { PlayerActions = value };
    }

    public string Mode
    {
        get => Mechanics.Mode;
        init => Mechanics = Mechanics with { Mode = value };
    }

    public int NextShuffleIndex
    {
        get => Stochastic.NextShuffleIndex;
        init => Stochastic = Stochastic with { NextShuffleIndex = value };
    }

    public RandomIndexDecisionV111[] ShuffleTranscript
    {
        get => Stochastic.ShuffleTranscript;
        init => Stochastic = Stochastic with { ShuffleTranscript = value };
    }

    public RandomIndexRequestV111? Pending
    {
        get => Stochastic.Pending;
        init => Stochastic = Stochastic with { Pending = value };
    }

    // The in-flight intent checkpoint stores the unmodified post-enemy-action
    // combat state and the current pure selector stage.
    public ControlledTurnIntentsV111? TurnIntents
    {
        get => Run.World?.Combat?.AutomaticPipelineContinuation?
            .ControlledTurnIntents;
        init => Run = PrototypeGameEngine
            .SetControlledTurnIntentContinuation(Run, value);
    }

    public RandomIndexDecisionV111[] TurnTranscript
    {
        get => Stochastic.TurnTranscript;
        init => Stochastic = Stochastic with { TurnTranscript = value };
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public SemanticIntentEventV111? IntentChoicePending
    {
        get
        {
            var intents = TurnIntents;
            if (intents?.Pending is not { } request)
                return null;
            var support = ControlledTurnIntentEngineV111
                .DescribePendingSemanticIntent(intents);
            return support is null ? null :
                SemanticIntentChoiceV111.CreateEvent(
                    Fingerprint, request, support);
        }
    }

    public ControlledRecycleShuffleV111? Recycle
    {
        get => Stochastic.Recycle;
        init
        {
            var prefix = Stochastic.RecycleTranscriptPrefix ??
                throw new InvalidDataException(
                    "Missing completed recycle transcript prefix.");
            if (value is { Transcript.Length: > 0 } stage &&
                prefix.Length >= stage.Transcript.Length &&
                CanonicalJson.Sha256(prefix[^stage.Transcript.Length..]) ==
                    CanonicalJson.Sha256(stage.Transcript))
                prefix = prefix[..^stage.Transcript.Length];
            Stochastic = Stochastic with
            {
                Recycle = value,
                RecycleTranscriptPrefix = prefix
            };
        }
    }

    public RandomIndexDecisionV111[] RecycleTranscript
    {
        get
        {
            var prefix = Stochastic.RecycleTranscriptPrefix;
            if (prefix is null) return null!;
            if (Recycle is not { } stage) return prefix;
            return stage.Transcript is null
                ? null!
                : prefix.Concat(stage.Transcript).ToArray();
        }
        init
        {
            var prefix = value ?? throw new InvalidDataException(
                "Missing recycle transcript projection.");
            if (Recycle is { } stage)
            {
                if (value is null || stage.Transcript is null ||
                    value.Length < stage.Transcript.Length ||
                    CanonicalJson.Sha256(value[^stage.Transcript.Length..]) !=
                        CanonicalJson.Sha256(stage.Transcript))
                    throw new InvalidDataException(
                        "Recycle transcript projection disagrees with its active stage.");
                prefix = value[..^stage.Transcript.Length];
            }
            Stochastic = Stochastic with
            {
                RecycleTranscriptPrefix = prefix
            };
        }
    }

    // Chronological live chance transcript, interleaving intents/recycles.
    public RandomIndexDecisionV111[] LiveTranscript
    {
        get => Stochastic.LiveTranscript;
        init => Stochastic = Stochastic with { LiveTranscript = value };
    }

    public string[]?[]? LiveTranscriptCandidateLabels
    {
        get => Stochastic.LiveTranscriptCandidateLabels;
        init => Stochastic = Stochastic with
        { LiveTranscriptCandidateLabels = value };
    }

    public RandomIndexDecisionV111[] TargetTranscript
    {
        get => Stochastic.TargetTranscript;
        init => Stochastic = Stochastic with { TargetTranscript = value };
    }

    public RandomIndexDecisionV111[] GeneratedCardTranscript
    {
        get => Stochastic.GeneratedCardTranscript;
        init => Stochastic = Stochastic with { GeneratedCardTranscript = value };
    }

    public RandomIndexDecisionV111[] DrawInsertionTranscript
    {
        get => Stochastic.DrawInsertionTranscript;
        init => Stochastic = Stochastic with { DrawInsertionTranscript = value };
    }

    public RandomIndexDecisionV111[] PoolShuffleTranscript
    {
        get => Stochastic.PoolShuffleTranscript;
        init => Stochastic = Stochastic with { PoolShuffleTranscript = value };
    }

    public RandomIndexDecisionV111[] RandomCostTranscript
    {
        get => Stochastic.RandomCostTranscript;
        init => Stochastic = Stochastic with { RandomCostTranscript = value };
    }

    public RandomIndexDecisionV111[] DrawCostTranscript
    {
        get => Stochastic.DrawCostTranscript;
        init => Stochastic = Stochastic with { DrawCostTranscript = value };
    }

    public RandomIndexDecisionV111[] PotionGenerationTranscript
    {
        get => Stochastic.PotionGenerationTranscript;
        init => Stochastic = Stochastic with
        { PotionGenerationTranscript = value };
    }

    public RandomIndexDecisionV111[] CardTransformTranscript
    {
        get => Stochastic.CardTransformTranscript;
        init => Stochastic = Stochastic with { CardTransformTranscript = value };
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public RandomIndexRequestV111? ActivePending =>
        DrawCostPending ?? RandomCostPending ?? PotionGenerationPending ?? CardTransformPending ?? PoolShufflePending ?? DrawInsertionPending ??
        EnemyActionPending ?? GeneratedCardPending ?? TargetPending ??
        Stochastic.Recycle?.Pending ?? TurnIntents?.Pending ?? Stochastic.Pending;
    [System.Text.Json.Serialization.JsonIgnore]
    public RandomIndexRequestV111? EnemyActionPending =>
        Run.World?.Combat?.AutomaticPipelineContinuation?
            .ControlledEnemyAction?.PendingRequest;
    [System.Text.Json.Serialization.JsonIgnore]
    public RandomIndexRequestV111? DrawCostPending
    {
        get
        {
            var cost = Run.World?.Combat?.PendingChoice?.ControlledDrawCost;
            if (cost is null) return null;
            var relic = cost.Stage == "relic";
            var card = Run.World!.Combat!.Cards.Single(c =>
                c.InstanceId == cost.CardInstanceId);
            return new RandomIndexRequestV111(
                relic ? StochasticSiteRegistryV111.CombatDrawRelicEnergyCostSiteId
                      : StochasticSiteRegistryV111.CombatDrawSlitherEnergyCostSiteId,
                "prototype.combat",
                "card_instance:" + cost.CardInstanceId + ":model:" +
                    card.CardId + ":stage:" + cost.Stage,
                Stochastic.DrawCostTranscript.Length, [],
                OperationKind: "bounded_integer",
                BoundedCount: relic ? cost.RelicMaxExclusive : 4);
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public RandomIndexRequestV111? RandomCostPending
    {
        get
        {
            var cost = Run.World?.Combat?.PendingChoice?.ControlledRandomCost;
            if (cost is null) return null;
            var single = cost.Operation.Kind ==
                PrototypeCombatEffectKind.SetRandomHandCardEnergyCostUntilTurnEndOrPlayed;
            return new RandomIndexRequestV111(
                single ? StochasticSiteRegistryV111.CombatRandomHandCardSelectSiteId
                       : StochasticSiteRegistryV111.CombatRandomHandCardEnergyCostSiteId,
                "prototype.combat",
                "effect:" + cost.Operation.Kind +
                    ":hand:" + CanonicalJson.Sha256(cost.EligibleCardInstanceIds) +
                    ":index:" + cost.NextCardIndex,
                Stochastic.RandomCostTranscript.Length,
                single
                    ? Enumerable.Range(0, cost.EligibleCardInstanceIds.Length).ToArray()
                    : [],
                OperationKind: single ? "choose_index" : "bounded_integer",
                BoundedCount: single ? null : Math.Max(1, cost.Operation.Amount));
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public RandomIndexRequestV111? PoolShufflePending
    {
        get
        {
            var combat = Run.World?.Combat;
            if (combat?.PendingChoice?.ControlledPoolShuffle is null)
                return null;
            return SemanticPoolShuffleChoiceV111.CreateRequest(
                combat, Stochastic.PoolShuffleTranscript.Length);
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public RandomIndexRequestV111? DrawInsertionPending
    {
        get
        {
            var choice = Run.World?.Combat?.PendingChoice?.ControlledDrawInsertion;
            if (choice is null) return null;
            var owner = "card:" +
                (Run.World!.Combat!.PendingChoice!.SourceCardInstanceId?.ToString()
                    ?? "effect") + ":op:" + choice.Operation.Kind;
            if (choice.Stage == "card_model")
                return new RandomIndexRequestV111(
                    "combat.random_draw_attack.card_model", "prototype.combat",
                    owner + ":pool:" + CanonicalJson.Sha256(choice.CandidateCardIds),
                    Stochastic.DrawInsertionTranscript.Length,
                    Enumerable.Range(0, choice.CandidateCardIds.Length).ToArray(),
                    OperationKind: "choose_index");
            bool sourceTimedInsertion =
                choice.PersonalHiveDamageContinuation is not null ||
                choice.AfterShuffleRelic is not null;
            var requestOwnerId = sourceTimedInsertion
                ? choice.RequestOwnerId + ":card:" + choice.SelectedCardId +
                    ":draw_pile:" +
                    CanonicalJson.Sha256(choice.EligibleDrawPile)
                : owner + ":model:" + choice.SelectedCardId + ":pile:" +
                    CanonicalJson.Sha256(choice.EligibleDrawPile);
            var requestOrdinal = sourceTimedInsertion
                ? choice.RequestOrdinal
                : Stochastic.DrawInsertionTranscript.Length;
            return SemanticDrawInsertionEventV111.CreateRequest(
                choice.RequestSiteId, nativeStreamAvailable: false,
                legacyOwnerId: requestOwnerId,
                nativeOwnerId: requestOwnerId,
                ordinal: requestOrdinal,
                supportCount: choice.EligibleDrawPile.Length + 1);
        }
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public RandomIndexRequestV111? GeneratedCardPending
    {
        get
        {
            var choice = Run.World?.Combat?.PendingChoice?.ControlledGeneration;
            if (choice is null) return null;
            var pool = choice.CandidateCardIds;
            var ownerId = "card:" +
                (Run.World!.Combat!.PendingChoice!.SourceCardInstanceId?
                    .ToString() ?? "effect") + ":pool:" +
                CanonicalJson.Sha256(pool);
            return SemanticGenerationChoiceV111.CreateRequest(
                "combat.generated_card.select", ownerId,
                Stochastic.GeneratedCardTranscript.Length, pool.Length);
        }
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public RandomIndexRequestV111? PotionGenerationPending
    {
        get
        {
            var combat = Run.World?.Combat;
            var generation = combat?.PendingChoice?.ControlledPotionGeneration;
            if (generation is null) return null;
            var owner = "effect:" + generation.Operation.Kind +
                ":source:" + (combat!.PendingChoice!.SourceCardInstanceId?
                    .ToString() ?? "none") + ":pool:" +
                CanonicalJson.Sha256(generation.CandidatePotionIds);
            return SemanticPotionGenerationChoiceV111.CreateRequest(
                owner, Stochastic.PotionGenerationTranscript.Length,
                generation.CandidatePotionIds.Length);
        }
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public RandomIndexRequestV111? CardTransformPending
    {
        get
        {
            var combat = Run.World?.Combat;
            var transform = combat?.PendingChoice?.ControlledCardTransform;
            if (transform is null) return null;
            var instanceId = transform.SelectedCardInstanceIds[
                transform.NextTransformIndex];
            var ownerId = "card_instance:" + instanceId + ":pool:" +
                CanonicalJson.Sha256(transform.CandidateCardIds);
            return SemanticCardTransformChoiceV111.CreateRequest(
                ownerId, Stochastic.CardTransformTranscript.Length,
                transform.CandidateCardIds.Length);
        }
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public RandomIndexRequestV111? TargetPending
    {
        get
        {
            var context = Run.World?.Combat?.PendingChoice?.ControlledTarget;
            if (context is null) return null;
            var siteId = SemanticTargetChoiceV111.SiteIdForTargetKind(
                context.Kind) ?? throw new NotSupportedException(
                    "Controlled target does not support kind '" +
                    context.Kind + "'.");
            var ownerId = context.SlyCardInstanceId is { } slyCardId
                ? "card:" + slyCardId
                : context.AutoPlayCardInstanceId is { } autoPlayCardId
                    ? "card:" + autoPlayCardId : "effect_operation";
            return new RandomIndexRequestV111(
                siteId, "run.combat_targets", ownerId,
                Stochastic.TargetTranscript.Length,
                context.EligibleEnemyInstanceIds,
                OperationKind: "choose_index");
        }
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool AtPlayerBoundary => ActivePending is null &&
        TurnIntents is null && Stochastic.Recycle is null &&
        (Run.Phase switch
        {
            RunPhase.Combat =>
                PrototypeGameEngine.IsAtCombatPlayerDecisionBoundary(Run),
            RunPhase.Reward or RunPhase.Terminal => true,
            _ => false
        });
    [System.Text.Json.Serialization.JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);
    public void Validate()
    {
        this.Mechanics.Continuation.Validate(
            PrototypeMechanicsContinuationKindV111.ControlledPlayableCombat);
        this.Run.Validate();
        if (this.Mode != "controlled-v111-playable-fixture-v1" ||
            this.Run.Phase is not (RunPhase.Combat or RunPhase.Reward or RunPhase.Terminal) ||
            this.Run.World?.Combat is null && this.Stochastic.Pending is not null ||
            this.Stochastic.ShuffleTranscript is null || this.PlayerActions is null ||
            this.Stochastic.RecycleTranscriptPrefix is null ||
            this.Stochastic.TurnTranscript is null || this.RecycleTranscript is null ||
            this.Stochastic.LiveTranscript is null ||
            (this.Stochastic.LiveTranscriptCandidateLabels is { } liveLabels &&
                liveLabels.Length != this.Stochastic.LiveTranscript.Length) ||
            this.Stochastic.TargetTranscript is null ||
            this.Stochastic.GeneratedCardTranscript is null ||
            this.Stochastic.DrawInsertionTranscript is null ||
            this.Stochastic.PoolShuffleTranscript is null ||
            this.Stochastic.RandomCostTranscript is null ||
            this.Stochastic.DrawCostTranscript is null ||
            this.Stochastic.PotionGenerationTranscript is null ||
            this.Stochastic.CardTransformTranscript is null || this.Origin is null ||
            (this.TurnIntents is not null && this.Stochastic.Pending is not null) ||
            (this.Stochastic.Recycle is not null &&
                (this.Stochastic.Pending is not null || this.TurnIntents is not null)))
            throw new InvalidDataException("Malformed controlled playable stage.");
        if (this.Stochastic.LiveTranscriptCandidateLabels is { } candidateLabels)
        {
            for (int index = 0; index < candidateLabels.Length; index++)
            {
                if (candidateLabels[index] is { } labels)
                    ControlledStochasticDecisionV111.FromIndex(
                        this.Stochastic.LiveTranscript[index], labels).Validate();
            }
        }
        var enemyAction = this.Run.World?.Combat?
            .AutomaticPipelineContinuation?.Stochastic.ControlledEnemyAction;
        if (enemyAction is not null)
        {
            enemyAction.Validate(PrototypeContent.Rules.EndTurnPipeline.Length);
            if (this.Stochastic.Pending is not null || this.Stochastic.Recycle is not null ||
                this.TurnIntents is not null ||
                !ControlledStochasticChanceReplayV111.RequestsMatch(
                    this.EnemyActionPending, enemyAction.PendingRequest))
                throw new InvalidDataException(
                    "Enemy-action chance conflicts with another controlled boundary.");
        }
        var drawnCostCombat = this.Run.World?.Combat;
        var drawnCost = drawnCostCombat?.PendingChoice?.ControlledDrawCost;
        if (drawnCost is not null)
        {
            var automatic = drawnCostCombat?.AutomaticPipelineContinuation;
            bool savedDrawCostProgress = automatic is not null &&
                automatic.Stochastic.TryGetActiveAutomaticHandDrawProgress(
                    automatic.Mechanics, out var progress) &&
                progress.Kind == PrototypeAutomaticHandDrawProgressKindV111
                    .ControlledDrawCost &&
                progress.RemainingCount == drawnCost.RemainingCount;
            if (this.Stochastic.Pending is not null || this.Stochastic.Recycle is not null ||
                this.TurnIntents is not null ||
                drawnCostCombat?.PendingChoice?.ControlledPotionGeneration is not null ||
                !drawnCost.FromHandDraw ||
                !savedDrawCostProgress ||
                !PrototypeGameEngine.ValidateControlledDrawCost(
                    this.Run.Player, drawnCostCombat!, drawnCost))
                throw new InvalidDataException(
                    "Saved drawn-card cost is not source-reachable.");
        }
        var costCombat = this.Run.World?.Combat;
        var randomCost = costCombat?.PendingChoice?.ControlledRandomCost;
        if (randomCost is not null &&
            (this.Stochastic.Pending is not null || this.Stochastic.Recycle is not null ||
             this.TurnIntents is not null ||
             costCombat?.PendingChoice?.ControlledTarget is not null ||
             costCombat?.PendingChoice?.ControlledGeneration is not null ||
             costCombat?.PendingChoice?.ControlledPotionGeneration is not null ||
             costCombat?.PendingChoice?.ControlledDrawInsertion is not null ||
             costCombat?.PendingChoice?.ControlledPoolShuffle is not null ||
             !PrototypeGameEngine.ValidateControlledRandomCost(
                 costCombat!, randomCost)))
            throw new InvalidDataException(
                "Saved hand-cost ticket disagrees with source eligibility.");
        var poolCombat = this.Run.World?.Combat;
        var pool = poolCombat?.PendingChoice?.ControlledPoolShuffle;
        if (pool is not null &&
            (this.Stochastic.Pending is not null || this.Stochastic.Recycle is not null ||
             this.TurnIntents is not null ||
             poolCombat?.PendingChoice?.ControlledTarget is not null ||
             poolCombat?.PendingChoice?.ControlledGeneration is not null ||
             poolCombat?.PendingChoice?.ControlledPotionGeneration is not null ||
             poolCombat?.PendingChoice?.ControlledDrawInsertion is not null ||
             !PrototypeGameEngine.ValidateControlledPoolShuffle(
                 poolCombat!, pool,
                 poolCombat?.PendingChoice?.SourceCardInstanceId)))
            throw new InvalidDataException(
                "Saved full-pool shuffle disagrees with source eligibility.");
        if (pool is not null)
        {
            // Replay only pure swap arithmetic to establish reachability.
            // No card hooks or earlier operations are re-executed here.
            int consumed = pool.OriginalCardIds.Length - 1 - pool.NextSwapIndex;
            if (consumed < 0 || consumed > this.Stochastic.PoolShuffleTranscript.Length)
                throw new InvalidDataException(
                    "Pool shuffle cursor exceeds recorded Nature decisions.");
            var current = (string[])pool.OriginalCardIds.Clone();
            var active = this.PoolShufflePending!;
            var offset = this.Stochastic.PoolShuffleTranscript.Length - consumed;
            for (int step = 0; step < consumed; step++)
            {
                var recorded = this.Stochastic.PoolShuffleTranscript[offset + step];
                recorded.Validate();
                var expectedRequest = active with
                {
                    Ordinal = offset + step,
                    BoundedCount = pool.OriginalCardIds.Length - step
                };
                if (recorded.ProviderMode != "controlled-v111" ||
                    recorded.NativeCursorBefore is not null ||
                    recorded.NativeCursorAfter is not null ||
                    !ControlledStochasticChanceReplayV111.RequestsMatch(
                        recorded.Request, expectedRequest))
                    throw new InvalidDataException(
                        "Pool shuffle swap transcript is not source-reachable.");
                var i = pool.OriginalCardIds.Length - 1 - step;
                (current[i], current[recorded.SelectedIndex]) =
                    (current[recorded.SelectedIndex], current[i]);
            }
            if (!current.SequenceEqual(pool.CurrentCardIds))
                throw new InvalidDataException(
                    "Saved pool permutation disagrees with its Nature swap prefix.");
        }
        var insertionCombat = this.Run.World?.Combat;
        var insertion = insertionCombat?.PendingChoice?.ControlledDrawInsertion;
        if (insertion is not null &&
            (this.Stochastic.Pending is not null || this.Stochastic.Recycle is not null ||
             this.TurnIntents is not null ||
             insertionCombat?.PendingChoice?.ControlledTarget is not null ||
             insertionCombat?.PendingChoice?.ControlledGeneration is not null ||
             insertionCombat?.PendingChoice?.ControlledPotionGeneration is not null ||
             !PrototypeGameEngine.ValidateControlledDrawInsertion(
                 insertionCombat!, insertion)))
            throw new InvalidDataException(
                "Draw insertion chance disagrees with source eligibility.");
        var generated = this.Run.World?.Combat?.PendingChoice?.ControlledGeneration;
        if (generated is not null &&
            (this.Stochastic.Pending is not null || this.Stochastic.Recycle is not null ||
             this.TurnIntents is not null ||
             this.Run.World?.Combat?.PendingChoice?.ControlledTarget is not null ||
             this.Run.World?.Combat?.PendingChoice?.ControlledPotionGeneration is not null ||
             !PrototypeGameEngine.ValidateControlledGenerationSupport(generated)))
            throw new InvalidDataException(
                "Malformed or stale controlled generated-card candidate pool.");
        var potionCombat = this.Run.World?.Combat;
        var potionGeneration = potionCombat?.PendingChoice?.ControlledPotionGeneration;
        if (potionGeneration is not null &&
            (this.Stochastic.Pending is not null || this.Stochastic.Recycle is not null ||
             this.TurnIntents is not null ||
             potionCombat?.PendingChoice?.ControlledTarget is not null ||
             potionCombat?.PendingChoice?.ControlledGeneration is not null ||
             potionCombat?.PendingChoice?.ControlledPoolShuffle is not null ||
             !PrototypeGameEngine.ValidateControlledPotionGeneration(
                 potionCombat!, potionGeneration,
                 potionCombat!.PendingChoice?.SourceCardInstanceId)))
            throw new InvalidDataException(
                "Malformed or stale controlled potion-generation candidate pool.");
        var transformCombat = this.Run.World?.Combat;
        var cardTransform = transformCombat?.PendingChoice?
            .ControlledCardTransform;
        if (cardTransform is not null &&
            (this.Stochastic.Pending is not null || this.Stochastic.Recycle is not null ||
             this.TurnIntents is not null ||
             transformCombat?.PendingChoice?.ControlledTarget is not null ||
             transformCombat?.PendingChoice?.ControlledGeneration is not null ||
             transformCombat?.PendingChoice?.ControlledPotionGeneration is not null ||
             transformCombat?.PendingChoice?.ControlledPoolShuffle is not null ||
             !PrototypeGameEngine.ValidateControlledCardTransform(
                 transformCombat!, cardTransform)))
            throw new InvalidDataException(
                "Malformed or stale controlled card-transform candidate pool.");
        var controlledTarget = this.Run.World?.Combat?.PendingChoice?.ControlledTarget;
        if (controlledTarget is not null)
        {
            var combat = this.Run.World?.Combat ??
                throw new InvalidDataException("Target choice lacks combat this.");
            if (this.Stochastic.Recycle is not null || this.TurnIntents is not null ||
                this.Stochastic.Pending is not null ||
                combat.PendingChoice?.ControlledPotionGeneration is not null ||
                combat.PendingChoice is not { } targetChoice ||
                !SemanticTargetChoiceV111.HasValidLiveSupport(
                    combat, targetChoice))
                throw new InvalidDataException(
                    "Malformed or stale controlled target-choice support.");
        }
        var expected = ControlledPlayableCombatEngineV111.MakePending(
            this.Stochastic.NextShuffleIndex,
            this.Stochastic.ShuffleTranscript.Length);
        if (CanonicalJson.Sha256(expected) != CanonicalJson.Sha256(this.Stochastic.Pending))
            throw new InvalidDataException("Controlled shuffle stage is stale.");
        if (this.Stochastic.Recycle is { } recycle)
        {
            ControlledRecycleShuffleEngineV111.Validate(recycle);
            var combat = this.Run.World?.Combat;
            if (combat is null ||
                !combat.DiscardPile.SequenceEqual(recycle.Baseline) ||
                combat.DrawPile.Length != 0 ||
                combat.AutomaticPipelineContinuation is not { } continuation ||
                !continuation.Stochastic.TryGetControlledHandDrawRemainder(
                    continuation.Mechanics, out int remaining) || remaining <= 0)
                throw new InvalidDataException(
                    "Recycle chance state disagrees with suspended draw.");
        }
    }

    public ControlledPlayableCombatV111 Fork() => this with
    {
        Origin = Origin.Fork(),
        Run = Run.Fork(),
        PlayerActions = (string[])PlayerActions.Clone(),
        Stochastic = Stochastic.Fork()
    };
}

public static class ControlledPlayableCombatEngineV111
{
    public const string SchemaId = "sts2-v111-controlled-playable-fixture-v1";
    public const string ShuffleSite = "controlled.fixture.opening_shuffle.swap_index";

    public static ControlledPlayableCombatV111 Begin(
        ControlledIntentStateV111 completed, string seed,
        string? encounterId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        var verified = ControlledIntentsV111.Restore(
            ControlledIntentsV111.Save(completed));
        if (!verified.Complete || verified.PriorHpTranscript is null)
            throw new InvalidOperationException(
                "A playable fixture requires fully controlled HP followed by intents.");
        if (CanonicalJson.Sha256(verified.ReferenceStreams) !=
            CanonicalJson.Sha256(NativeRngV111.CreateBundle(seed)))
            throw new InvalidOperationException(
                "Seed disagrees with saved controlled HP/intent stream provenance.");

        int act = verified.Combat.Act;
        if (act is not (1 or 2 or 3))
            throw new NotSupportedException(
                "Source-backed playable Nature fixtures support Acts 1–3.");
        // Construct the starter character from the original gameplay
        // engine. Act 1 is restricted to source-pinned fixed weak
        // formations at the earlier pre-entry validation boundary.
        // This is still an isolated fixture, not a full route replay.
        var run = act == 1
            ? new PrototypeGameEngine().Step(
                PrototypeGameFactory.Create(seed, verified.Combat.Ascension),
                GameAction.Empty("start_run")).State
            : PrototypeNativeLaterActFixture.Create(
                seed, act, verified.Combat.Ascension);
        var player = run.Player;
        var cards = player.Deck.Select((c, i) => new CombatCardInstance(
            i + 1L, c.InstanceId, c.CardId, c.UpgradeLevel, false,
            c.PersistentState.Clone(), Enchantment: c.Enchantment)).ToArray();
        var relics = player.Relics.Select((r, i) =>
        {
            var d = PrototypeContent.Relic(r.RelicId);
            return new CombatRelicState(i, r.RelicId, i + 1L,
                new int[(d.Triggers ?? []).Length], r.IsMelted);
        }).ToArray();
        var potions = player.PotionSlots.Select((p, i) =>
            p is null ? null : new CombatPotionState(
                i, p.PotionId, p.PersistentState.Clone()))
            .Where(p => p is not null).Select(p => p!).ToArray();
        var combat = verified.Combat.Fork() with
        {
            Cards = cards,
            NextCardInstanceId = cards.Length + 1L,
            Relics = relics,
            Potions = potions,
            CurrentMapNodeId = run.World!.Map.CurrentNodeId,
            IsPlayerTurn = true
        };
        combat = SemanticDrawPileAdapterV111.ApplyDrawPileInitialization(
            combat,
            combat.Mechanics.CardZones.WithZoneChanges(
                new Dictionary<PrototypeCardZone, long[]>
                {
                    [PrototypeCardZone.Hand] = [],
                    [PrototypeCardZone.DrawPile] = cards
                        .Select(c => c.InstanceId).ToArray(),
                    [PrototypeCardZone.DiscardPile] = [],
                    [PrototypeCardZone.ExhaustPile] = [],
                    [PrototypeCardZone.PlayPile] = [],
                    [PrototypeCardZone.ChoicePool] = []
                }));
        run = run with
        {
            Phase = RunPhase.Combat,
            World = run.World! with
            {
                ActiveRoom = PrototypeRoomType.Combat,
                Combat = combat
            }
        };
        if (encounterId is not null)
        {
            var encounter = PrototypeContent.Encounter(encounterId);
            if (encounter.RoomType != PrototypeRoomType.Combat ||
                act < encounter.MinAct || act > encounter.MaxAct)
                throw new InvalidDataException(
                    "Fixed playable encounter is ineligible for its run entry.");
            var world = run.World ?? throw new InvalidDataException(
                "Playable combat fixture lost its run world.");
            run = run with
            {
                World = world.AppendEncounterId(encounterId)
            };
        }
        int nextIndex = combat.DrawPile.Length - 1;
        var state = new ControlledPlayableCombatV111(
            seed, verified, run, nextIndex, [], [],
            MakePending(nextIndex, 0));
        return state;
    }

    public static ControlledPlayableCombatV111 Resolve(
        ControlledPlayableCombatV111 state,
        string fingerprint, int selectedIndex)
    {
        ValidatePendingResolveRequest(state, fingerprint);
        return ResolveValidated(state, selectedIndex);
    }

    /// <summary>
    /// Resolve a generated-card outcome by its stable model ID. The saved
    /// controlled-playable transcript remains the source-compatible index
    /// ticket, translated through the live ordered support at resume.
    /// </summary>
    public static ControlledPlayableCombatV111 ResolveGeneratedCardById(
        ControlledPlayableCombatV111 state, string fingerprint,
        string cardId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        ValidatePendingResolveRequest(state, fingerprint);
        var candidates = state.Run.World?.Combat?.PendingChoice?
            .ControlledGeneration?.CandidateCardIds ??
            throw new InvalidOperationException(
                "No controlled generated-card outcome is pending.");
        return ResolveValidated(state, UniqueCandidateIndex(
            candidates, cardId, "generated-card model"));
    }

    /// <summary>
    /// Resolve a combat-potion outcome by its stable model ID while
    /// preserving the established index decision transcript.
    /// </summary>
    public static ControlledPlayableCombatV111 ResolvePotionGenerationById(
        ControlledPlayableCombatV111 state, string fingerprint,
        string potionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(potionId);
        ValidatePendingResolveRequest(state, fingerprint);
        var candidates = state.Run.World?.Combat?.PendingChoice?
            .ControlledPotionGeneration?.CandidatePotionIds ??
            throw new InvalidOperationException(
                "No controlled potion-generation outcome is pending.");
        return ResolveValidated(state, UniqueCandidateIndex(
            candidates, potionId, "potion model"));
    }

    /// <summary>
    /// Resolve a transform replacement by its stable card-model ID while
    /// preserving the established index decision transcript.
    /// </summary>
    public static ControlledPlayableCombatV111 ResolveCardTransformById(
        ControlledPlayableCombatV111 state, string fingerprint,
        string cardId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        ValidatePendingResolveRequest(state, fingerprint);
        var candidates = state.Run.World?.Combat?.PendingChoice?
            .ControlledCardTransform?.CandidateCardIds ??
            throw new InvalidOperationException(
                "No controlled card-transform outcome is pending.");
        return ResolveValidated(state, UniqueCandidateIndex(
            candidates, cardId, "card-transform model"));
    }

    /// <summary>
    /// Resolve a target outcome by its stable enemy instance ID while
    /// preserving the established index decision transcript.
    /// </summary>
    public static ControlledPlayableCombatV111
        ResolveTargetByEnemyInstanceId(
            ControlledPlayableCombatV111 state, string fingerprint,
            int enemyInstanceId)
    {
        ValidatePendingResolveRequest(state, fingerprint);
        var candidates = state.Run.World?.Combat?.PendingChoice?
            .ControlledTarget?.EligibleEnemyInstanceIds ??
            throw new InvalidOperationException(
                "No controlled target outcome is pending.");
        return ResolveValidated(state, UniqueCandidateIndex(
            candidates, enemyInstanceId, "enemy instance"));
    }

    /// <summary>
    /// Resolve a controlled enemy action by its saved semantic candidate
    /// label, sharing the same label validation used by semantic end-turn.
    /// </summary>
    public static ControlledPlayableCombatV111
        ResolveEnemyActionByCandidateLabel(
            ControlledPlayableCombatV111 state, string fingerprint,
            string candidateLabel)
    {
        ValidatePendingResolveRequest(state, fingerprint);
        var enemyAction =
            state.Run.World?.Combat?.AutomaticPipelineContinuation
                ?.Stochastic.ControlledEnemyAction ??
            throw new InvalidOperationException(
                "No controlled enemy-action outcome is pending.");
        var selectedIndex = ControlledChanceContinuationExecutorV111
            .IndexOfCandidateLabel(
                enemyAction.PendingCandidateLabels,
                enemyAction.PendingRequest.SupportCount,
                candidateLabel);
        return ResolveValidated(state, selectedIndex);
    }

    /// <summary>
    /// Resolve a weighted enemy intent by its stable semantic intent ID.
    /// The source selector receives the first legal primitive ticket in that
    /// intent's weight interval, preserving the controlled index transcript.
    /// </summary>
    public static ControlledPlayableCombatV111 ResolveIntentById(
        ControlledPlayableCombatV111 state, string fingerprint,
        string eventId, string intentId)
    {
        ValidatePendingResolveRequest(state, fingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        var pending = state.IntentChoicePending ??
            throw new InvalidOperationException(
                "No weighted semantic intent choice is pending.");
        if (!StringComparer.Ordinal.Equals(pending.EventId, eventId))
            throw new InvalidOperationException(
                "Semantic intent event ID is stale or mismatched.");
        var selection = SemanticIntentChoiceV111.Select(pending, intentId);
        return ResolveValidated(state, selection.SourceTicket);
    }

    private static void ValidatePendingResolveRequest(
        ControlledPlayableCombatV111 state, string fingerprint)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.ActivePending is null)
            throw new InvalidOperationException(
                "Controlled state is awaiting a player action, not Nature.");
        if (!StringComparer.Ordinal.Equals(state.Fingerprint, fingerprint))
            throw new InvalidOperationException(
                "Stale controlled chance fingerprint.");
        state.Validate();
    }

    private static int UniqueCandidateIndex<T>(
        T[] candidates, T selected, string outcomeKind)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var found = -1;
        for (var index = 0; index < candidates.Length; index++)
        {
            if (!EqualityComparer<T>.Default.Equals(
                    candidates[index], selected))
                continue;
            if (found >= 0)
                throw new InvalidOperationException(
                    "Selected " + outcomeKind +
                    " is ambiguous in the pending source support.");
            found = index;
        }
        return found >= 0
            ? found
            : throw new InvalidOperationException(
                "Selected " + outcomeKind +
                " is outside the pending source support.");
    }

    private static ControlledPlayableCombatStochasticStateV111
        AppendLiveDecision(
            ControlledPlayableCombatV111 state,
            ControlledPlayableLiveDecisionKindV111 kind,
            RandomIndexDecisionV111 decision) =>
        state.Stochastic.AppendLiveDecision(
            kind, decision, PendingCandidateLabels(state));

    private static string[]? PendingCandidateLabels(
        ControlledPlayableCombatV111 state)
    {
        if (state.DrawCostPending is not null)
            return null;
        var randomCost = state.Run.World?.Combat?.PendingChoice?
            .ControlledRandomCost;
        if (state.RandomCostPending is not null)
        {
            if (randomCost?.Operation.Kind !=
                PrototypeCombatEffectKind.SetRandomHandCardEnergyCostUntilTurnEndOrPlayed)
                return null;
            return randomCost.EligibleCardInstanceIds.Select(instanceId =>
                "card_instance:" + instanceId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        }
        var potionGeneration = state.Run.World?.Combat?.PendingChoice?
            .ControlledPotionGeneration;
        if (state.PotionGenerationPending is not null)
            return potionGeneration?.CandidatePotionIds.Select(potionId =>
                "potion_model:" + potionId).ToArray();
        var cardTransform = state.Run.World?.Combat?.PendingChoice?
            .ControlledCardTransform;
        if (state.CardTransformPending is not null)
            return cardTransform?.CandidateCardIds.Select(cardId =>
                "card_model:" + cardId).ToArray();
        if (state.PoolShufflePending is not null)
            return null;
        var drawInsertion = state.Run.World?.Combat?.PendingChoice?
            .ControlledDrawInsertion;
        if (state.DrawInsertionPending is not null)
            return drawInsertion?.Stage == "card_model"
                ? drawInsertion.CandidateCardIds.Select(cardId =>
                    "card_model:" + cardId).ToArray()
                : null;
        if (state.EnemyActionPending is not null)
            return state.Run.World?.Combat?.AutomaticPipelineContinuation?
                .Stochastic.ControlledEnemyAction?.PendingCandidateLabels;
        var generated = state.Run.World?.Combat?.PendingChoice?
            .ControlledGeneration;
        if (state.GeneratedCardPending is not null)
            return generated?.CandidateCardIds.Select(cardId =>
                "card_model:" + cardId).ToArray();
        if (state.TargetPending is { } target)
            return target.OrderedCandidates.Select(enemyId =>
                "enemy_instance:" + enemyId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (state.Stochastic.Recycle?.Pending is not null)
            return null;
        if (state.TurnIntents?.Pending is { } intentRequest)
        {
            var support = state.IntentChoicePending?.Support ??
                throw new InvalidDataException(
                    "Pending turn-intent request has no semantic support.");
            if (support.TotalRawWeight != intentRequest.SupportCount)
                throw new InvalidDataException(
                    "Turn-intent labels disagree with the pending ticket bound.");
            var labels = new string[support.TotalRawWeight];
            foreach (var candidate in support.Candidates)
            {
                if (candidate.TicketStart < 0 || candidate.RawWeight <= 0 ||
                    candidate.TicketStart + candidate.RawWeight > labels.Length)
                    throw new InvalidDataException(
                        "Turn-intent label interval is outside its pending ticket.");
                Array.Fill(labels, candidate.IntentId, candidate.TicketStart,
                    candidate.RawWeight);
            }
            return labels;
        }
        return null;
    }

    private static ControlledPlayableCombatV111 ResolveValidated(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        if (state.DrawCostPending is not null)
            return ResolveDrawCost(state, selectedIndex);
        if (state.RandomCostPending is not null)
            return ResolveRandomCost(state, selectedIndex);
        if (state.PotionGenerationPending is not null)
            return ResolvePotionGeneration(state, selectedIndex);
        if (state.CardTransformPending is not null)
            return ResolveCardTransform(state, selectedIndex);
        if (state.PoolShufflePending is not null)
            return ResolvePoolShuffle(state, selectedIndex);
        if (state.DrawInsertionPending is not null)
            return ResolveDrawInsertion(state, selectedIndex);
        if (state.EnemyActionPending is not null)
            return ResolveEnemyAction(state, selectedIndex);
        if (state.GeneratedCardPending is not null)
            return ResolveGeneratedCard(state, selectedIndex);
        if (state.TargetPending is not null)
            return ResolveTarget(state, selectedIndex);
        if (state.Stochastic.Recycle is not null)
            return ResolveRecycle(state, selectedIndex);
        if (state.TurnIntents is not null)
            return ResolveTurnIntents(state, selectedIndex);
        var request = state.Stochastic.Pending!;
        var next = ControlledChanceContinuationExecutorV111
            .ResolveSelectedIndex<ControlledPlayableCombatV111,
                ControlledPlayableCombatV111>(
                state, request, selectedIndex, (checkpoint, replayedIndex) =>
                {
                    var fresh = checkpoint.Fork();
                    var combat = fresh.Run.World!.Combat!;
                    var drawPile = (long[])combat.DrawPile.Clone();
                    (drawPile[fresh.Stochastic.NextShuffleIndex], drawPile[replayedIndex]) =
                        (drawPile[replayedIndex], drawPile[fresh.Stochastic.NextShuffleIndex]);
                    var nextIndex = fresh.Stochastic.NextShuffleIndex - 1;
                    combat = SemanticDrawPileAdapterV111
                        .ApplyDrawPileOrderCommit(combat, drawPile);
                    var decision = new RandomIndexDecisionV111(
                        request, replayedIndex, "controlled-v111");
                    decision.Validate();
                    return fresh with
                    {
                        Run = fresh.Run with
                        {
                            World = fresh.Run.World with { Combat = combat }
                        },
                        Stochastic = fresh.Stochastic.AdvanceOpeningShuffle(
                            decision, nextIndex,
                            MakePending(nextIndex,
                                fresh.Stochastic.ShuffleTranscript.Length + 1))
                    };
                });
        return next.Stochastic.Pending is null
            ? CompleteOpeningHand(next)
            : next;
    }

    public static ControlledPlayableCombatV111 Step(
        ControlledPlayableCombatV111 state, string actionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        state.Validate();
        if (!state.AtPlayerBoundary)
            throw new InvalidOperationException(
                "Resolve the pending Nature shuffle choice first.");
        var environment = new PrototypeAiEnvironment();
        var frame = environment.Observe(state.Run);
        if (frame.LegalActions.Count(x => x.ActionId == actionId) != 1)
            throw new InvalidOperationException(
                "Illegal or ambiguous controlled player action: " + actionId);
        // Real engine mechanics execute only on the freshly forked state.
        // Unmigrated RNG always aborts BEFORE the session stores a child.
        // No rerunning earlier effects is used to resolve an action.
        var input = state.Run.Fork();
        var result = ControlledChanceGuardV111.ExecuteGuarded(
            () => environment.Step(input, actionId).State);
        var next = state.Fork() with
        {
            Mechanics = state.Mechanics.AppendPlayerAction(actionId, result)
        };
        return StartPendingTurn(next);
    }

    private static ControlledPlayableCombatV111 ResolveDrawCost(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.DrawCostPending ??
            throw new InvalidOperationException("Missing drawn-card cost ticket.");
        var (resume, decision) = ResolveIndexAndRun(
            state, request, selectedIndex,
            PrototypeGameEngine.ResumeControlledDrawCost);
        var next = state.Fork() with
        {
            Run = resume.Run,
            Stochastic = AppendLiveDecision(state,
                ControlledPlayableLiveDecisionKindV111.DrawCost, decision)
        };
        return StartPendingTurn(next, resume);
    }

    private static ControlledPlayableCombatV111 ResolveRandomCost(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.RandomCostPending ??
            throw new InvalidOperationException("Missing random-cost ticket.");
        var (resume, decision) = ResolveIndexAndPipeline(
            state, request, selectedIndex,
            PrototypeGameEngine.ResumeControlledRandomCostWithBoundary);
        var next = state.Fork() with
        {
            Run = resume.Run,
            Stochastic = AppendLiveDecision(state,
                ControlledPlayableLiveDecisionKindV111.RandomCost, decision)
        };
        return StartPendingTurn(next, resume);
    }

    private static ControlledPlayableCombatV111 ResolvePoolShuffle(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.PoolShufflePending ??
            throw new InvalidOperationException("Missing pool-shuffle ticket.");
        var (resume, choice) = ResolveIndexAndPipeline(
            state, request, selectedIndex,
            PrototypeGameEngine.ResumeControlledPoolShuffleWithBoundary);
        var next = state.Fork() with
        {
            Run = resume.Run,
            Stochastic = AppendLiveDecision(state,
                ControlledPlayableLiveDecisionKindV111.PoolShuffle, choice)
        };
        return StartPendingTurn(next, resume);
    }

    private static ControlledPlayableCombatV111 ResolveDrawInsertion(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.DrawInsertionPending ??
            throw new InvalidOperationException("Missing draw insertion ticket.");
        var (resume, choice) = ResolveIndexAndPipeline(
            state, request, selectedIndex,
            PrototypeGameEngine.ResumeControlledDrawInsertionWithBoundary);
        var next = state.Fork() with
        {
            Run = resume.Run,
            Stochastic = AppendLiveDecision(state,
                ControlledPlayableLiveDecisionKindV111.DrawInsertion, choice)
        };
        return StartPendingTurn(next, resume);
    }

    private static ControlledPlayableCombatV111 ResolveEnemyAction(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.EnemyActionPending ??
            throw new InvalidOperationException(
                "Missing controlled enemy-action ticket.");
        var (resume, decision) = ResolveIndexAndPipeline(
            state, request, selectedIndex,
            PrototypeGameEngine.ResumeControlledEnemyAction);
        var next = state.Fork() with
        {
            Run = resume.Run,
            Stochastic = AppendLiveDecision(state,
                ControlledPlayableLiveDecisionKindV111.LiveOnly, decision)
        };
        return StartPendingTurn(next, resume);
    }

    private static ControlledPlayableCombatV111 ResolveGeneratedCard(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.GeneratedCardPending ??
            throw new InvalidOperationException("Missing generated-card Nature ticket.");
        var (resume, choice) = ResolveIndexAndPipeline(
            state, request, selectedIndex,
            (checkpoint, replayedIndex) =>
            {
                var candidates = checkpoint.World?.Combat?.PendingChoice?.ControlledGeneration?.CandidateCardIds ??
                    throw new InvalidDataException(
                        "Generated-card continuation lost its ordered model support.");
                if (replayedIndex < 0 || replayedIndex >= candidates.Length)
                    throw new InvalidDataException(
                        "Generated-card provider index is outside its source support.");
                return PrototypeGameEngine
                    .ResumeControlledGeneratedCardWithBoundary(
                        checkpoint, candidates[replayedIndex]);
            });
        var next = state.Fork() with
        {
            Run = resume.Run,
            Stochastic = AppendLiveDecision(state,
                ControlledPlayableLiveDecisionKindV111.GeneratedCard, choice)
        };
        return StartPendingTurn(next, resume);
    }

    private static ControlledPlayableCombatV111 ResolvePotionGeneration(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.PotionGenerationPending ??
            throw new InvalidOperationException(
                "Missing random-potion Nature ticket.");
        var (resume, choice) = ResolveIndexAndPipeline(
            state, request, selectedIndex,
            (checkpoint, replayedIndex) =>
            {
                var pending = checkpoint.World?.Combat?.PendingChoice;
                var generation = pending?.ControlledPotionGeneration ??
                    throw new InvalidDataException(
                        "Potion-generation continuation lost its source support.");
                var candidates = generation.CandidatePotionIds;
                if (replayedIndex < 0 || replayedIndex >= candidates.Length)
                    throw new InvalidDataException(
                        "Potion-generation provider index is outside its source support.");
                return PrototypeGameEngine
                    .ResumeControlledPotionGenerationWithBoundary(
                        checkpoint, candidates[replayedIndex]);
            });
        var next = state.Fork() with
        {
            Run = resume.Run,
            Stochastic = AppendLiveDecision(state,
                ControlledPlayableLiveDecisionKindV111.PotionGeneration,
                choice)
        };
        return StartPendingTurn(next, resume);
    }

    private static ControlledPlayableCombatV111 ResolveCardTransform(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.CardTransformPending ??
            throw new InvalidOperationException(
                "Missing random card-transform Nature ticket.");
        var (resume, choice) = ResolveIndexAndRun(
            state, request, selectedIndex,
            (checkpoint, replayedIndex) =>
            {
                var pending = checkpoint.World?.Combat?.PendingChoice;
                var transform = pending?.ControlledCardTransform ??
                    throw new InvalidDataException(
                        "Card-transform continuation lost its source support.");
                var candidates = transform.CandidateCardIds;
                if (replayedIndex < 0 || replayedIndex >= candidates.Length)
                    throw new InvalidDataException(
                        "Card-transform provider index is outside its source support.");
                return PrototypeGameEngine.ResumeControlledCardTransform(
                    checkpoint, candidates[replayedIndex]);
            });
        var next = state.Fork() with
        {
            Run = resume.Run,
            Stochastic = AppendLiveDecision(state,
                ControlledPlayableLiveDecisionKindV111.CardTransform, choice)
        };
        return StartPendingTurn(next, resume);
    }

    private static ControlledPlayableCombatV111 ResolveTarget(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.TargetPending ??
            throw new InvalidOperationException("No controlled target ticket.");
        var (resume, choice) = ResolveIndexAndPipeline(
            state, request, selectedIndex,
            (checkpoint, replayedIndex) =>
                PrototypeGameEngine.ResumeControlledRandomTargetWithBoundary(
                    checkpoint, request.OrderedCandidates[replayedIndex]));
        var next = state.Fork() with
        {
            Run = resume.Run,
            Stochastic = AppendLiveDecision(state,
                ControlledPlayableLiveDecisionKindV111.Target, choice)
        };
        return StartPendingTurn(next, resume);
    }

    private static (
        PrototypeAutomaticPipelineResumeResultV111 Resume,
        RandomIndexDecisionV111 Decision)
        ResolveIndexAndRun(
            ControlledPlayableCombatV111 state,
            RandomIndexRequestV111 request,
            int selectedIndex,
            Func<RunState, int, RunState> resume)
        => ResolveIndexAndPipeline(
            state, request, selectedIndex,
            (checkpoint, replayedIndex) =>
                PrototypeAutomaticPipelineResumeResultV111.FromRun(
                    resume(checkpoint, replayedIndex)));

    private static (
        PrototypeAutomaticPipelineResumeResultV111 Resume,
        RandomIndexDecisionV111 Decision)
        ResolveIndexAndPipeline(
            ControlledPlayableCombatV111 state,
            RandomIndexRequestV111 request,
            int selectedIndex,
            Func<RunState, int,
                PrototypeAutomaticPipelineResumeResultV111> resume)
    {
        return ControlledChanceContinuationExecutorV111
            .ResolveSelectedIndexValueWithDecision<ControlledPlayableCombatV111,
                PrototypeAutomaticPipelineResumeResultV111>(
                state, request, selectedIndex, (checkpoint, replayedIndex) =>
                    ControlledChanceGuardV111.ExecuteGuarded(
                        () => resume(
                            checkpoint.Run.Fork(), replayedIndex)));
    }

    private static ControlledPlayableCombatV111 StartPendingTurn(
        ControlledPlayableCombatV111 state,
        PrototypeAutomaticPipelineResumeResultV111? resumeResult = null)
    {
        if (state.Run.Phase != RunPhase.Combat ||
            state.Run.World?.Combat is not { } combat ||
            combat.AutomaticPipelineContinuation is not { } continuation)
            return state;
        var stepKind = continuation.Mechanics.CurrentStepKind;
        bool hasActiveHandDrawProgress =
            continuation.Stochastic.TryGetActiveAutomaticHandDrawProgress(
                continuation.Mechanics, out var handDrawProgress);
        bool isBeforeHandDraw =
            continuation.Stochastic.IsAtSemanticBeforeHandDraw(
                continuation.Mechanics) ||
            continuation.Stochastic.IsAtControlledBeforeHandDraw(
                continuation.Mechanics);
        if (continuation.Stochastic.HasSavedHandDrawProgress() &&
            !hasActiveHandDrawProgress && !isBeforeHandDraw)
            throw new InvalidDataException(
                "Saved automatic hand-draw progress is outside its source stage.");
        if (hasActiveHandDrawProgress && handDrawProgress.Kind ==
            PrototypeAutomaticHandDrawProgressKindV111
                .ControlledRecycleStratagem)
            return state;
        if (hasActiveHandDrawProgress && handDrawProgress.Kind ==
            PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawCost)
        {
            if (stepKind != PrototypeAutomaticStepKind.DrawPlayerHand ||
                combat.PendingChoice?.ControlledDrawCost is null)
                throw new InvalidDataException("Malformed saved draw-cost boundary.");
            return state;
        }
        if (continuation.Stochastic.TryGetControlledHandDrawRemainder(
                continuation.Mechanics, out _))
        {
            if (stepKind != PrototypeAutomaticStepKind.DrawPlayerHand ||
                state.Stochastic.Recycle is not null || state.TurnIntents is not null ||
                combat.DrawPile.Length != 0 || combat.DiscardPile.Length == 0)
                throw new InvalidDataException("Malformed saved controlled recycle boundary.");
            var recycle = ControlledRecycleShuffleEngineV111.Begin(
                combat.DiscardPile, state.RecycleTranscript.Length);
            var recycleState = state with { Recycle = recycle };
            return recycle.Complete ? FinishRecycle(recycleState) : recycleState;
        }
        var boundary = resumeResult?.Boundary ?? PrototypeGameEngine
            .ClassifyAutomaticPipelineResumeBoundary(state.Run);
        if (boundary !=
            PrototypeAutomaticPipelineResumeBoundaryKindV111.AdvanceTurn)
            return state;
        if (state.TurnIntents is not null)
            throw new InvalidOperationException("Nested intent stage already pending.");
        // Recompute against the live formation. Enemy actions may change the
        // formation before the next visible intent boundary; each current
        // enemy already carries its source AI state and stable instance
        // identity, so the initial formation is not the eligibility authority
        // for this later commitment.
        var intents = PrototypeGameEngine
            .BeginAutomaticPipelineIntents(
                state.Run, state.Origin.Formation.NativeEncounterEntry);
        var next = state with { TurnIntents = intents };
        return intents.Complete ? FinishTurnIntents(next) : next;
    }

    private static ControlledPlayableCombatV111 ResolveTurnIntents(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var current = state.TurnIntents ??
            throw new InvalidOperationException("Turn-intent stage missing.");
        var nextIntents = ControlledTurnIntentEngineV111.Resolve(
            current, selectedIndex, state.Origin.ReferenceStreams);
        var fresh = state.Fork() with
        {
            TurnIntents = nextIntents,
            Stochastic = AppendLiveDecision(state,
                ControlledPlayableLiveDecisionKindV111.TurnIntent,
                nextIntents.Transcript[^1])
        };
        return nextIntents.Complete ? FinishTurnIntents(fresh) : fresh;
    }

    private static ControlledPlayableCombatV111 ResolveRecycle(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var stage = state.Stochastic.Recycle ??
            throw new InvalidOperationException("Missing recycle stage.");
        var next = ControlledRecycleShuffleEngineV111.Resolve(
            stage, selectedIndex, state.Origin.ReferenceStreams);
        var choice = next.Transcript[^1];
        var updated = state.Fork() with
        {
            Stochastic = AppendLiveDecision(state,
                ControlledPlayableLiveDecisionKindV111.LiveOnly, choice) with
            {
                Recycle = next
            }
        };
        return next.Complete ? FinishRecycle(updated) : updated;
    }

    private static ControlledPlayableCombatV111 FinishRecycle(
        ControlledPlayableCombatV111 state)
    {
        var stage = state.Stochastic.Recycle ??
            throw new InvalidOperationException("Missing recycle stage.");
        if (!stage.Complete)
            throw new InvalidOperationException("Nature recycle swaps are pending.");
        ControlledRecycleShuffleEngineV111.Validate(stage);
        var result = ControlledChanceGuardV111.ExecuteGuarded(
            () => PrototypeGameEngine.ResumeControlledDrawAfterRecycle(
                state.Run.Fork(), stage.Shuffled));
        return StartPendingTurn(state with
        {
            Recycle = null,
            RecycleTranscript = state.RecycleTranscript,
            Run = result
        });
    }

    private static ControlledPlayableCombatV111 FinishTurnIntents(
        ControlledPlayableCombatV111 state)
    {
        var resume = ControlledChanceGuardV111.ExecuteGuarded(
            () => PrototypeGameEngine.ResumeControlledTurnAfterIntents(
                state.Run.Fork()));
        return StartPendingTurn(state with
        {
            TurnIntents = null,
            Run = resume.Run
        }, resume);
    }

    public static string Save(ControlledPlayableCombatV111 state)
    {
        state.Validate();
        return JsonSerializer.Serialize(state);
    }

    public static ControlledPlayableCombatV111 Restore(string snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot);
        var stored = JsonSerializer.Deserialize<ControlledPlayableCombatV111>(snapshot)
            ?? throw new InvalidDataException("Null controlled playable snapshot.");
        stored.Validate();
        // Full provenance replay, from source-verified controlled HP+intent
        // completion through the entire ordered shuffle and player transcript.
        var current = Begin(stored.Origin, stored.Seed);
        foreach (var decision in stored.Stochastic.ShuffleTranscript)
        {
            decision.Validate();
            if (decision.ProviderMode != "controlled-v111" ||
                decision.ProbabilityStatus != "UNVERIFIED" ||
                decision.NativeCursorBefore is not null ||
                decision.NativeCursorAfter is not null ||
                current.Stochastic.Pending is null ||
                !ControlledStochasticChanceReplayV111.RequestsMatch(
                    decision.Request, current.Stochastic.Pending))
                throw new InvalidDataException(
                    "Unreachable controlled shuffle transcript.");
            current = Resolve(current, current.Fingerprint, decision.SelectedIndex);
        }
        int liveCursor = 0;
        foreach (var action in stored.PlayerActions)
        {
            if (!current.AtPlayerBoundary)
                throw new InvalidDataException(
                    "Player action attempted before Nature resolved prior chance.");
            current = Step(current, action);
            while (current.ActivePending is not null &&
                liveCursor < stored.Stochastic.LiveTranscript.Length)
            {
                int decisionIndex = liveCursor++;
                var recorded = stored.Stochastic.LiveTranscript[decisionIndex];
                recorded.Validate();
                if (recorded.ProviderMode != "controlled-v111" ||
                    recorded.ProbabilityStatus != "UNVERIFIED" ||
                    recorded.NativeCursorBefore is not null ||
                    recorded.NativeCursorAfter is not null ||
                    !ControlledStochasticChanceReplayV111.RequestsMatch(
                        recorded.Request, current.ActivePending))
                    throw new InvalidDataException(
                        "Unreachable live Nature decision in replay.");
                if (stored.Stochastic.LiveTranscriptCandidateLabels is { } labels &&
                    !ControlledChanceContinuationExecutorV111.CandidateLabelsMatch(
                        labels[decisionIndex],
                        PendingCandidateLabels(current)))
                    throw new InvalidDataException(
                        "Live Nature candidate labels diverged during replay.");
                current = Resolve(current, current.Fingerprint, recorded.SelectedIndex);
            }
        }
        if (liveCursor != stored.Stochastic.LiveTranscript.Length)
            throw new InvalidDataException(
                "Unconsumed or out-of-order live Nature transcript.");
        if (stored.Stochastic.LiveTranscriptCandidateLabels is null)
            current = current with
            {
                Stochastic = current.Stochastic with
                { LiveTranscriptCandidateLabels = null }
            };
        if (!current.Stochastic.TurnTranscript.SequenceEqual(stored.Stochastic.TurnTranscript) ||
            !current.RecycleTranscript.SequenceEqual(stored.RecycleTranscript) ||
            !current.Stochastic.TargetTranscript.SequenceEqual(
                stored.Stochastic.TargetTranscript) ||
            !current.Stochastic.GeneratedCardTranscript.SequenceEqual(
                stored.Stochastic.GeneratedCardTranscript) ||
            !current.Stochastic.DrawInsertionTranscript.SequenceEqual(
                stored.Stochastic.DrawInsertionTranscript) ||
            !current.Stochastic.PoolShuffleTranscript.SequenceEqual(
                stored.Stochastic.PoolShuffleTranscript) ||
            !current.Stochastic.RandomCostTranscript.SequenceEqual(
                stored.Stochastic.RandomCostTranscript) ||
            !current.Stochastic.DrawCostTranscript.SequenceEqual(
                stored.Stochastic.DrawCostTranscript) ||
            !current.Stochastic.PotionGenerationTranscript.SequenceEqual(
                stored.Stochastic.PotionGenerationTranscript) ||
            !current.Stochastic.CardTransformTranscript.SequenceEqual(
                stored.Stochastic.CardTransformTranscript))
        {
            // Records hold arrays; canonical comparison below is decisive.
            if (CanonicalJson.Sha256(current.Stochastic.TurnTranscript) !=
                    CanonicalJson.Sha256(stored.Stochastic.TurnTranscript) ||
                CanonicalJson.Sha256(current.RecycleTranscript) !=
                    CanonicalJson.Sha256(stored.RecycleTranscript) ||
                CanonicalJson.Sha256(current.Stochastic.TargetTranscript) !=
                    CanonicalJson.Sha256(stored.Stochastic.TargetTranscript) ||
                CanonicalJson.Sha256(current.Stochastic.GeneratedCardTranscript) !=
                    CanonicalJson.Sha256(stored.Stochastic.GeneratedCardTranscript) ||
                CanonicalJson.Sha256(current.Stochastic.DrawInsertionTranscript) !=
                    CanonicalJson.Sha256(stored.Stochastic.DrawInsertionTranscript) ||
                CanonicalJson.Sha256(current.Stochastic.PoolShuffleTranscript) !=
                    CanonicalJson.Sha256(stored.Stochastic.PoolShuffleTranscript) ||
                CanonicalJson.Sha256(current.Stochastic.RandomCostTranscript) !=
                    CanonicalJson.Sha256(stored.Stochastic.RandomCostTranscript) ||
                CanonicalJson.Sha256(current.Stochastic.DrawCostTranscript) !=
                    CanonicalJson.Sha256(stored.Stochastic.DrawCostTranscript) ||
                CanonicalJson.Sha256(current.Stochastic.PotionGenerationTranscript) !=
                    CanonicalJson.Sha256(stored.Stochastic.PotionGenerationTranscript) ||
                CanonicalJson.Sha256(current.Stochastic.CardTransformTranscript) !=
                    CanonicalJson.Sha256(stored.Stochastic.CardTransformTranscript))
                throw new InvalidDataException("Chance transcripts diverged.");
        }
        if (CanonicalJson.Sha256(current) != CanonicalJson.Sha256(stored))
            throw new InvalidDataException(
                "Controlled playable snapshot diverges from its provenance.");
        return current;
    }

    internal static void VerifyStage(ControlledPlayableCombatV111 state) =>
        state.Validate();

    internal static RandomIndexRequestV111? MakePending(int index, int ordinal) =>
        index < 1 ? null : new RandomIndexRequestV111(
            ShuffleSite, "run.shuffle", "player:0", ordinal,
            [], OperationKind: "bounded_integer", BoundedCount: index + 1);

    private static ControlledPlayableCombatV111 CompleteOpeningHand(
        ControlledPlayableCombatV111 state)
    {
        var combat = state.Run.World!.Combat!;
        var pile = combat.DrawPile.ToList();
        var hand = new List<long>();
        for (int n = 0; n < Math.Min(5, pile.Count); n++)
        {
            int index = pile.Count - 1;
            hand.Add(pile[index]);
            pile.RemoveAt(index);
        }
        combat = SemanticDrawPileAdapterV111.ApplyDrawPileRemoval(
            combat, hand,
            combat.Mechanics.CardZones.WithZoneChanges(
                new Dictionary<PrototypeCardZone, long[]>
                {
                    [PrototypeCardZone.DrawPile] = pile.ToArray(),
                    [PrototypeCardZone.Hand] = hand.ToArray()
                }));
        return state with
        {
            Run = state.Run with
            {
                World = state.Run.World with { Combat = combat }
            }
        };
    }
}
