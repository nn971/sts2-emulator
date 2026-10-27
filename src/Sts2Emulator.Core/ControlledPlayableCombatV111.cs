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
    // Replay provenance for an explicitly conditioned encounter. This is not
    // a provider choice and is never part of the player's fair observation.
    public string? FixtureEncounterId { get; init; }
    // Distinct source-conditioned persistent starter deck recipe. It must be
    // replayed before the initial engine shuffle, just like encounter ID.
    public string? FixtureDeckPresetId { get; init; }
    // Opt-in, validated initial conditions, kept with the replay envelope.
    public ResearchCombatFixtureV111? FixtureSpec { get; init; }

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
        Recycle?.Pending ?? TurnIntents?.Pending ?? Pending;
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
                relic ? "combat.card_draw.relic_energy_cost"
                      : "combat.card_draw.slither_energy_cost",
                "prototype.combat",
                "card_instance:" + cost.CardInstanceId + ":model:" +
                    card.CardId + ":stage:" + cost.Stage,
                DrawCostTranscript.Length, [],
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
                single ? "combat.random_hand_card.select"
                       : "combat.random_hand_card.energy_cost",
                "prototype.combat",
                "effect:" + cost.Operation.Kind +
                    ":hand:" + CanonicalJson.Sha256(cost.EligibleCardInstanceIds) +
                    ":index:" + cost.NextCardIndex,
                RandomCostTranscript.Length,
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
                combat, PoolShuffleTranscript.Length);
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
                    DrawInsertionTranscript.Length,
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
                ? choice.RequestOrdinal : DrawInsertionTranscript.Length;
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
                GeneratedCardTranscript.Length, pool.Length);
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
                owner, PotionGenerationTranscript.Length,
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
                ownerId, CardTransformTranscript.Length,
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
                TargetTranscript.Length,
                context.EligibleEnemyInstanceIds,
                OperationKind: "choose_index");
        }
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool AtPlayerBoundary => ActivePending is null &&
        TurnIntents is null && Recycle is null;
    [System.Text.Json.Serialization.JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);
    public void Validate()
    {
        this.Mechanics.Continuation.Validate(
            PrototypeMechanicsContinuationKindV111.ControlledPlayableCombat);
        if (FixtureEncounterId is { } fixedEncounter &&
            (string.IsNullOrWhiteSpace(fixedEncounter) ||
             Run.World?.EncounterHistory?.LastOrDefault() != fixedEncounter))
            throw new InvalidDataException(
                "Fixed encounter replay provenance disagrees with run history.");
        this.Run.Validate();
        if (this.Mode != "controlled-v111-playable-fixture-v1" ||
            this.Run.Phase is not (RunPhase.Combat or RunPhase.Reward or RunPhase.Terminal) ||
            this.Run.World?.Combat is null && this.Pending is not null ||
            this.ShuffleTranscript is null || this.PlayerActions is null ||
            this.Stochastic.RecycleTranscriptPrefix is null ||
            this.TurnTranscript is null || this.RecycleTranscript is null ||
            this.LiveTranscript is null || this.TargetTranscript is null ||
            this.GeneratedCardTranscript is null ||
            this.DrawInsertionTranscript is null ||
            this.PoolShuffleTranscript is null ||
            this.RandomCostTranscript is null ||
            this.DrawCostTranscript is null ||
            this.PotionGenerationTranscript is null ||
            this.CardTransformTranscript is null || this.Origin is null ||
            (this.TurnIntents is not null && this.Pending is not null) ||
            (this.Recycle is not null &&
                (this.Pending is not null || this.TurnIntents is not null)))
            throw new InvalidDataException("Malformed controlled playable stage.");
        var enemyAction = this.Run.World?.Combat?
            .AutomaticPipelineContinuation?.ControlledEnemyAction;
        if (enemyAction is not null)
        {
            enemyAction.Validate(PrototypeContent.Rules.EndTurnPipeline.Length);
            if (this.Pending is not null || this.Recycle is not null ||
                this.TurnIntents is not null ||
                CanonicalJson.Sha256(this.EnemyActionPending) !=
                    CanonicalJson.Sha256(enemyAction.PendingRequest))
                throw new InvalidDataException(
                    "Enemy-action chance conflicts with another controlled boundary.");
        }
        var drawnCostCombat = this.Run.World?.Combat;
        var drawnCost = drawnCostCombat?.PendingChoice?.ControlledDrawCost;
        if (drawnCost is not null)
        {
            var automatic = drawnCostCombat?.AutomaticPipelineContinuation;
            if (this.Pending is not null || this.Recycle is not null ||
                this.TurnIntents is not null ||
                drawnCostCombat?.PendingChoice?.ControlledPotionGeneration is not null ||
                !drawnCost.FromHandDraw ||
                automatic is null ||
                automatic.ControlledDrawCostRemaining != drawnCost.RemainingCount ||
                automatic.ControlledDrawRemaining is not null ||
                !PrototypeGameEngine.ValidateControlledDrawCost(
                    this.Run.Player, drawnCostCombat!, drawnCost))
                throw new InvalidDataException(
                    "Saved drawn-card cost is not source-reachable.");
        }
        var costCombat = this.Run.World?.Combat;
        var randomCost = costCombat?.PendingChoice?.ControlledRandomCost;
        if (randomCost is not null &&
            (this.Pending is not null || this.Recycle is not null ||
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
            (this.Pending is not null || this.Recycle is not null ||
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
            if (consumed < 0 || consumed > this.PoolShuffleTranscript.Length)
                throw new InvalidDataException(
                    "Pool shuffle cursor exceeds recorded Nature decisions.");
            var current = (string[])pool.OriginalCardIds.Clone();
            var active = this.PoolShufflePending!;
            var offset = this.PoolShuffleTranscript.Length - consumed;
            for (int step = 0; step < consumed; step++)
            {
                var recorded = this.PoolShuffleTranscript[offset + step];
                recorded.Validate();
                var expectedRequest = active with
                {
                    Ordinal = offset + step,
                    BoundedCount = pool.OriginalCardIds.Length - step
                };
                if (recorded.ProviderMode != "controlled-v111" ||
                    recorded.NativeCursorBefore is not null ||
                    recorded.NativeCursorAfter is not null ||
                    CanonicalJson.Sha256(recorded.Request) !=
                        CanonicalJson.Sha256(expectedRequest))
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
            (this.Pending is not null || this.Recycle is not null ||
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
            (this.Pending is not null || this.Recycle is not null ||
             this.TurnIntents is not null ||
             this.Run.World?.Combat?.PendingChoice?.ControlledTarget is not null ||
             this.Run.World?.Combat?.PendingChoice?.ControlledPotionGeneration is not null ||
             !PrototypeGameEngine.ValidateControlledGenerationSupport(generated)))
            throw new InvalidDataException(
                "Malformed or stale controlled generated-card candidate pool.");
        var potionCombat = this.Run.World?.Combat;
        var potionGeneration = potionCombat?.PendingChoice?.ControlledPotionGeneration;
        if (potionGeneration is not null &&
            (this.Pending is not null || this.Recycle is not null ||
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
            (this.Pending is not null || this.Recycle is not null ||
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
            var eligible = combat.Enemies
                .Where(enemy => enemy.Hp > 0 &&
                    (controlledTarget.Kind != "sly_auto_play" || !enemy.Escaped))
                .Select(enemy => enemy.InstanceId).ToArray();
            var autoPlayContinuation =
                combat.PendingChoice?.OuterChoiceContinuation;
            if (this.Recycle is not null || this.TurnIntents is not null ||
                this.Pending is not null ||
                combat.PendingChoice?.ControlledPotionGeneration is not null ||
                controlledTarget.Kind is not ("effect_random_enemy" or
                    "sly_auto_play" or "autoplay_random_enemy") ||
                eligible.Length == 0 ||
                !eligible.SequenceEqual(controlledTarget.EligibleEnemyInstanceIds) ||
                (controlledTarget.Kind == "sly_auto_play" &&
                    (controlledTarget.SlyCardInstanceId is not { } cardId ||
                     !combat.DiscardPile.Contains(cardId) ||
                     controlledTarget.AutoPlayCardInstanceId is not null)) ||
                (controlledTarget.Kind == "autoplay_random_enemy" &&
                    (controlledTarget.AutoPlayCardInstanceId is not { } autoPlayCardId ||
                     !combat.PlayCardIds.Contains(autoPlayCardId) ||
                     controlledTarget.SlyCardInstanceId is not null ||
                     autoPlayContinuation is null ||
                     autoPlayContinuation.Operations.Length == 0 ||
                     autoPlayContinuation.Operations[0].Kind !=
                        PrototypeCombatEffectKind.AutoPlayCombatCard ||
                     autoPlayContinuation.Operations[0].CardInstanceId != autoPlayCardId ||
                     autoPlayContinuation.Operations[0].TargetEnemyId is not null)) ||
                (controlledTarget.Kind == "effect_random_enemy" &&
                    (controlledTarget.SlyCardInstanceId is not null ||
                     controlledTarget.AutoPlayCardInstanceId is not null)))
                throw new InvalidDataException(
                    "Malformed or stale controlled target-choice support.");
        }
        var expected = ControlledPlayableCombatEngineV111.MakePending(this.NextShuffleIndex, this.ShuffleTranscript.Length);
        if (CanonicalJson.Sha256(expected) != CanonicalJson.Sha256(this.Pending))
            throw new InvalidDataException("Controlled shuffle stage is stale.");
        if (this.Recycle is { } recycle)
        {
            ControlledRecycleShuffleEngineV111.Validate(recycle);
            var combat = this.Run.World?.Combat;
            if (combat is null ||
                !combat.DiscardPile.SequenceEqual(recycle.Baseline) ||
                combat.DrawPile.Length != 0 ||
                combat.AutomaticPipelineContinuation?.ControlledDrawRemaining is not > 0)
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
        string? encounterId = null, string? fixtureDeckPresetId = null,
        ResearchCombatFixtureV111? fixtureSpec = null)
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
        if (fixtureSpec is not null)
        {
            // The spec is validated at the seeded entry *before* Chance Law
            // sampling, then applied before physical cards and opening hooks.
            fixtureSpec.Validate();
            if (fixtureDeckPresetId is not null || encounterId != fixtureSpec.EncounterId ||
                act != 1)
                throw new NotSupportedException(
                    "UNSUPPORTED_RESEARCH_FIXTURE: incompatible opening context");
            player = fixtureSpec.ApplyBeforeOpening(player);
            run = run with
            {
                Player = player,
                World = run.World! with
                {
                    NextCardInstanceId = Math.Max(
                        run.World!.NextCardInstanceId,
                        player.Deck.Max(c => c.InstanceId) + 1)
                }
            };
        }
        if (fixtureDeckPresetId is not null)
        {
            if (act != 1)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.conditioned_deck " +
                    "operation=requires_act1_silent");
            player = player with
            {
                Deck = SemanticConditionedDeckPresetV111.Apply(
                    player.Deck, fixtureDeckPresetId),
                Relics = SemanticConditionedDeckPresetV111.ApplyBossRelics(
                    player.Relics, seed, fixtureDeckPresetId)
            };
            // Set persistent allocator before creating the actual engine-owned
            // combat card registry. The boss loadout is a 20-card deck, not a
            // cosmetic replacement of a 12-card opening hand.
            run = run with
            {
                Player = player,
                World = run.World! with
                {
                    NextCardInstanceId = Math.Max(
                        run.World!.NextCardInstanceId,
                        player.Deck.Max(c => c.InstanceId) + 1)
                }
            };
        }
        var cards = player.Deck.Select((c, i) => new CombatCardInstance(
            i + 1L, c.InstanceId, c.CardId, c.UpgradeLevel, false,
            c.PersistentState.Clone(), Enchantment: c.Enchantment)).ToArray();
        // The source formation already assigned application-order tickets
        // to its starting enemy powers. Allocate the player's starter relics
        // after those tickets, using the combat mechanics owner.
        var lastPowerOrder = verified.Combat.PlayerPowers
            .Select(power => power.ApplicationOrder)
            .Concat(verified.Combat.Enemies.SelectMany(enemy =>
                enemy.PowerStates.Select(power => power.ApplicationOrder)))
            .DefaultIfEmpty(0L).Max();
        var firstRelicOrder = Math.Max(
            verified.Combat.NextPowerApplicationOrder, lastPowerOrder + 1);
        var relics = player.Relics.Select((r, i) =>
        {
            var d = PrototypeContent.Relic(r.RelicId);
            return new CombatRelicState(i, r.RelicId, firstRelicOrder + i,
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
            NextPowerApplicationOrder = firstRelicOrder + relics.Length,
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
                ActiveRoom = encounterId is not null &&
                     (SemanticConditionedDeckPresetV111.IsBossDeck(fixtureDeckPresetId) ||
                     fixtureSpec?.IsBoss == true)
                    ? PrototypeRoomType.Boss : PrototypeRoomType.Combat,
                Combat = combat
            }
        };
        if (encounterId is not null)
        {
            var encounter = PrototypeContent.Encounter(encounterId);
            if (act < encounter.MinAct || act > encounter.MaxAct ||
                (encounter.RoomType != PrototypeRoomType.Combat &&
                 !(encounter.RoomType == PrototypeRoomType.Boss &&
                   (SemanticConditionedDeckPresetV111.IsBossDeck(fixtureDeckPresetId) ||
                    fixtureSpec?.IsBoss == true))))
                throw new InvalidDataException(
                    "Fixed playable encounter is ineligible for its run entry.");
            var world = run.World ?? throw new InvalidDataException(
                "Playable combat fixture lost its run world.");
            if (act == 1)
            {
                // This deliberately conditioned fixture enters a real node of
                // the generated map. The map, encounter history, floor, and
                // combat's current node must describe the same room.
                var node = world.Map.Nodes
                    .Where(candidate =>
                        candidate.Floor == verified.Formation.TotalFloor &&
                        candidate.RoomType == encounter.RoomType)
                    .OrderBy(candidate => candidate.NodeId, StringComparer.Ordinal)
                    .FirstOrDefault() ?? throw new NotSupportedException(
                        "UNSUPPORTED_SEMANTIC_OPENING site=act1.fixed_entry " +
                        "operation=no_eligible_combat_map_node");
                combat = combat with { CurrentMapNodeId = node.NodeId };
                world = world with
                {
                    Floor = node.Floor,
                    Map = world.Map with { CurrentNodeId = node.NodeId },
                    Combat = combat
                };
            }
            run = run with
            {
                World = world with
                {
                    EncounterHistory = (world.EncounterHistory ?? [])
                        .Append(encounterId).ToArray()
                }
            };
        }
        int nextIndex = combat.DrawPile.Length - 1;
        var state = new ControlledPlayableCombatV111(
            seed, verified, run, nextIndex, [], [],
            MakePending(nextIndex, 0))
        {
            FixtureEncounterId = encounterId,
            FixtureDeckPresetId = fixtureDeckPresetId,
            FixtureSpec = fixtureSpec
        };
        return state;
    }

    public static ControlledPlayableCombatV111 Resolve(
        ControlledPlayableCombatV111 state,
        string fingerprint, int selectedIndex)
    {
        if (state.ActivePending is null)
            throw new InvalidOperationException(
                "Controlled state is awaiting a player action, not Nature.");
        if (!StringComparer.Ordinal.Equals(state.Fingerprint, fingerprint))
            throw new InvalidOperationException(
                "Stale controlled chance fingerprint.");
        state.Validate();
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
        if (state.Recycle is not null)
            return ResolveRecycle(state, selectedIndex);
        if (state.TurnIntents is not null)
            return ResolveTurnIntents(state, selectedIndex);
        var request = state.Pending!;
        var next = ControlledChanceContinuationExecutorV111
            .ResolveSelectedIndex<ControlledPlayableCombatV111,
                ControlledPlayableCombatV111>(
                state, request, selectedIndex, (checkpoint, replayedIndex) =>
                {
                    var fresh = checkpoint.Fork();
                    var combat = fresh.Run.World!.Combat!;
                    var drawPile = (long[])combat.DrawPile.Clone();
                    (drawPile[fresh.NextShuffleIndex], drawPile[replayedIndex]) =
                        (drawPile[replayedIndex], drawPile[fresh.NextShuffleIndex]);
                    var nextIndex = fresh.NextShuffleIndex - 1;
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
                        NextShuffleIndex = nextIndex,
                        ShuffleTranscript = fresh.ShuffleTranscript
                            .Append(decision).ToArray(),
                        Pending = MakePending(
                            nextIndex, fresh.ShuffleTranscript.Length + 1)
                    };
                });
        return next.Pending is null ? CompleteOpeningHand(next) : next;
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
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = environment.Step(input, actionId).State;
        var next = state.Fork() with
        {
            Run = result,
            PlayerActions = state.PlayerActions.Append(actionId).ToArray()
        };
        return StartPendingTurn(next);
    }

    private static ControlledPlayableCombatV111 ResolveDrawCost(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.DrawCostPending ??
            throw new InvalidOperationException("Missing drawn-card cost ticket.");
        var (result, decision) = ResolveIndexAndRun(
            state, request, selectedIndex,
            PrototypeGameEngine.ResumeControlledDrawCost);
        var next = state.Fork() with
        {
            Run = result,
            DrawCostTranscript = state.DrawCostTranscript
                .Append(decision).ToArray(),
            LiveTranscript = state.LiveTranscript.Append(decision).ToArray()
        };
        return StartPendingTurn(next);
    }

    private static ControlledPlayableCombatV111 ResolveRandomCost(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.RandomCostPending ??
            throw new InvalidOperationException("Missing random-cost ticket.");
        var (result, decision) = ResolveIndexAndRun(
            state, request, selectedIndex,
            PrototypeGameEngine.ResumeControlledRandomCost);
        var next = state.Fork() with
        {
            Run = result,
            RandomCostTranscript = state.RandomCostTranscript
                .Append(decision).ToArray(),
            LiveTranscript = state.LiveTranscript.Append(decision).ToArray()
        };
        return StartPendingTurn(next);
    }

    private static ControlledPlayableCombatV111 ResolvePoolShuffle(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.PoolShufflePending ??
            throw new InvalidOperationException("Missing pool-shuffle ticket.");
        var (result, choice) = ResolveIndexAndRun(
            state, request, selectedIndex,
            PrototypeGameEngine.ResumeControlledPoolShuffle);
        var next = state.Fork() with
        {
            Run = result,
            PoolShuffleTranscript = state.PoolShuffleTranscript
                .Append(choice).ToArray(),
            LiveTranscript = state.LiveTranscript.Append(choice).ToArray()
        };
        return StartPendingTurn(next);
    }

    private static ControlledPlayableCombatV111 ResolveDrawInsertion(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.DrawInsertionPending ??
            throw new InvalidOperationException("Missing draw insertion ticket.");
        var (result, choice) = ResolveIndexAndRun(
            state, request, selectedIndex,
            PrototypeGameEngine.ResumeControlledDrawInsertion);
        var next = state.Fork() with
        {
            Run = result,
            DrawInsertionTranscript = state.DrawInsertionTranscript
                .Append(choice).ToArray(),
            LiveTranscript = state.LiveTranscript.Append(choice).ToArray()
        };
        return StartPendingTurn(next);
    }

    private static ControlledPlayableCombatV111 ResolveEnemyAction(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.EnemyActionPending ??
            throw new InvalidOperationException(
                "Missing controlled enemy-action ticket.");
        var (result, decision) = ResolveIndexAndRun(
            state, request, selectedIndex,
            PrototypeGameEngine.ResumeControlledEnemyAction);
        var next = state.Fork() with
        {
            Run = result,
            LiveTranscript = state.LiveTranscript.Append(decision).ToArray()
        };
        return StartPendingTurn(next);
    }

    private static ControlledPlayableCombatV111 ResolveGeneratedCard(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.GeneratedCardPending ??
            throw new InvalidOperationException("Missing generated-card Nature ticket.");
        var (result, choice) = ResolveIndexAndRun(
            state, request, selectedIndex,
            PrototypeGameEngine.ResumeControlledGeneratedCard);
        var next = state.Fork() with
        {
            Run = result,
            GeneratedCardTranscript = state.GeneratedCardTranscript
                .Append(choice).ToArray(),
            LiveTranscript = state.LiveTranscript.Append(choice).ToArray()
        };
        return StartPendingTurn(next);
    }

    private static ControlledPlayableCombatV111 ResolvePotionGeneration(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.PotionGenerationPending ??
            throw new InvalidOperationException(
                "Missing random-potion Nature ticket.");
        var (result, choice) = ResolveIndexAndRun(
            state, request, selectedIndex,
            PrototypeGameEngine.ResumeControlledPotionGeneration);
        var next = state.Fork() with
        {
            Run = result,
            PotionGenerationTranscript = state.PotionGenerationTranscript
                .Append(choice).ToArray(),
            LiveTranscript = state.LiveTranscript.Append(choice).ToArray()
        };
        return StartPendingTurn(next);
    }

    private static ControlledPlayableCombatV111 ResolveCardTransform(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.CardTransformPending ??
            throw new InvalidOperationException(
                "Missing random card-transform Nature ticket.");
        var (result, choice) = ResolveIndexAndRun(
            state, request, selectedIndex,
            PrototypeGameEngine.ResumeControlledCardTransform);
        var next = state.Fork() with
        {
            Run = result,
            CardTransformTranscript = state.CardTransformTranscript
                .Append(choice).ToArray(),
            LiveTranscript = state.LiveTranscript.Append(choice).ToArray()
        };
        return StartPendingTurn(next);
    }

    private static ControlledPlayableCombatV111 ResolveTarget(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.TargetPending ??
            throw new InvalidOperationException("No controlled target ticket.");
        var (result, choice) = ResolveIndexAndRun(
            state, request, selectedIndex,
            PrototypeGameEngine.ResumeControlledRandomTarget);
        var next = state.Fork() with
        {
            Run = result,
            TargetTranscript = state.TargetTranscript.Append(choice).ToArray(),
            LiveTranscript = state.LiveTranscript.Append(choice).ToArray()
        };
        return StartPendingTurn(next);
    }

    private static (RunState Result, RandomIndexDecisionV111 Decision)
        ResolveIndexAndRun(
            ControlledPlayableCombatV111 state,
            RandomIndexRequestV111 request,
            int selectedIndex,
            Func<RunState, int, RunState> resume)
    {
        var decision = new RandomIndexDecisionV111(
            request with
            {
                OrderedCandidates = (int[])request.OrderedCandidates.Clone()
            }, selectedIndex, "controlled-v111");
        decision.Validate();
        var result = ControlledChanceContinuationExecutorV111
            .ResolveSelectedIndex<ControlledPlayableCombatV111, RunState>(
                state, request, selectedIndex, (checkpoint, replayedIndex) =>
                {
                    using (ControlledChanceGuardV111.Enter())
                        return resume(checkpoint.Run.Fork(), replayedIndex);
                });
        return (result, decision);
    }

    private static ControlledPlayableCombatV111 StartPendingTurn(
        ControlledPlayableCombatV111 state)
    {
        if (state.Run.Phase != RunPhase.Combat ||
            state.Run.World?.Combat is not { } combat ||
            combat.AutomaticPipelineContinuation is not { } continuation)
            return state;
        var stepKind = continuation.Mechanics.CurrentStepKind;
        if (continuation.ControlledRecycleStratagemPending)
            return state;
        if (continuation.ControlledDrawCostRemaining is not null)
        {
            if (stepKind != PrototypeAutomaticStepKind.DrawPlayerHand ||
                combat.PendingChoice?.ControlledDrawCost is null)
                throw new InvalidDataException("Malformed saved draw-cost boundary.");
            return state;
        }
        if (continuation.ControlledDrawRemaining is not null)
        {
            if (stepKind != PrototypeAutomaticStepKind.DrawPlayerHand ||
                state.Recycle is not null || state.TurnIntents is not null ||
                combat.DrawPile.Length != 0 || combat.DiscardPile.Length == 0)
                throw new InvalidDataException("Malformed saved controlled recycle boundary.");
            var recycle = ControlledRecycleShuffleEngineV111.Begin(
                combat.DiscardPile, state.RecycleTranscript.Length);
            var recycleState = state with { Recycle = recycle };
            return recycle.Complete ? FinishRecycle(recycleState) : recycleState;
        }
        if (PrototypeGameEngine
                .HasPendingControlledAutomaticPipelineChoice(state.Run))
            return state;
        if (stepKind != PrototypeAutomaticStepKind.AdvanceTurn)
            return state;
        if (state.TurnIntents is not null)
            throw new InvalidOperationException("Nested intent stage already pending.");
        // Recompute against the live formation. Enemy actions may change the
        // formation before the next visible intent boundary; each current
        // enemy already carries its source AI state and stable instance
        // identity, so the initial formation is not the eligibility authority
        // for this later commitment.
        var intents = ControlledTurnIntentEngineV111.Begin(
            combat, state.Origin.Formation.NativeEncounterEntry);
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
            TurnTranscript = state.TurnTranscript.Append(
                nextIntents.Transcript[^1]).ToArray(),
            LiveTranscript = state.LiveTranscript.Append(
                nextIntents.Transcript[^1]).ToArray()
        };
        return nextIntents.Complete ? FinishTurnIntents(fresh) : fresh;
    }

    private static ControlledPlayableCombatV111 ResolveRecycle(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var stage = state.Recycle ??
            throw new InvalidOperationException("Missing recycle stage.");
        var next = ControlledRecycleShuffleEngineV111.Resolve(
            stage, selectedIndex, state.Origin.ReferenceStreams);
        var choice = next.Transcript[^1];
        var updated = state.Fork() with
        {
            Recycle = next,
            LiveTranscript = state.LiveTranscript.Append(choice).ToArray()
        };
        return next.Complete ? FinishRecycle(updated) : updated;
    }

    private static ControlledPlayableCombatV111 FinishRecycle(
        ControlledPlayableCombatV111 state)
    {
        var stage = state.Recycle ??
            throw new InvalidOperationException("Missing recycle stage.");
        if (!stage.Complete)
            throw new InvalidOperationException("Nature recycle swaps are pending.");
        ControlledRecycleShuffleEngineV111.Validate(stage);
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.ResumeControlledDrawAfterRecycle(
                state.Run.Fork(), stage.Shuffled);
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
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.ResumeControlledTurnAfterIntents(
                state.Run.Fork());
        return StartPendingTurn(state with
        {
            TurnIntents = null,
            Run = result
        });
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
        // Full provenance replay, from source-verified controlled HP+intent
        // completion through the entire ordered shuffle and player transcript.
        var current = Begin(stored.Origin, stored.Seed,
            stored.FixtureEncounterId, stored.FixtureDeckPresetId);
        foreach (var decision in stored.ShuffleTranscript)
        {
            decision.Validate();
            if (decision.ProviderMode != "controlled-v111" ||
                decision.ProbabilityStatus != "UNVERIFIED" ||
                decision.NativeCursorBefore is not null ||
                decision.NativeCursorAfter is not null ||
                current.Pending is null ||
                CanonicalJson.Sha256(decision.Request) !=
                    CanonicalJson.Sha256(current.Pending))
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
                liveCursor < stored.LiveTranscript.Length)
            {
                var recorded = stored.LiveTranscript[liveCursor++];
                recorded.Validate();
                if (recorded.ProviderMode != "controlled-v111" ||
                    recorded.ProbabilityStatus != "UNVERIFIED" ||
                    recorded.NativeCursorBefore is not null ||
                    recorded.NativeCursorAfter is not null ||
                    CanonicalJson.Sha256(recorded.Request) !=
                        CanonicalJson.Sha256(current.ActivePending))
                    throw new InvalidDataException(
                        "Unreachable live Nature decision in replay.");
                current = Resolve(current, current.Fingerprint, recorded.SelectedIndex);
            }
        }
        if (liveCursor != stored.LiveTranscript.Length)
            throw new InvalidDataException(
                "Unconsumed or out-of-order live Nature transcript.");
        if (!current.TurnTranscript.SequenceEqual(stored.TurnTranscript) ||
            !current.RecycleTranscript.SequenceEqual(stored.RecycleTranscript) ||
            !current.TargetTranscript.SequenceEqual(stored.TargetTranscript) ||
            !current.GeneratedCardTranscript.SequenceEqual(stored.GeneratedCardTranscript) ||
            !current.DrawInsertionTranscript.SequenceEqual(stored.DrawInsertionTranscript) ||
            !current.PoolShuffleTranscript.SequenceEqual(stored.PoolShuffleTranscript) ||
            !current.RandomCostTranscript.SequenceEqual(stored.RandomCostTranscript) ||
            !current.DrawCostTranscript.SequenceEqual(stored.DrawCostTranscript) ||
            !current.PotionGenerationTranscript.SequenceEqual(
                stored.PotionGenerationTranscript) ||
            !current.CardTransformTranscript.SequenceEqual(
                stored.CardTransformTranscript))
        {
            // Records hold arrays; canonical comparison below is decisive.
            if (CanonicalJson.Sha256(current.TurnTranscript) !=
                    CanonicalJson.Sha256(stored.TurnTranscript) ||
                CanonicalJson.Sha256(current.RecycleTranscript) !=
                    CanonicalJson.Sha256(stored.RecycleTranscript) ||
                CanonicalJson.Sha256(current.TargetTranscript) !=
                    CanonicalJson.Sha256(stored.TargetTranscript) ||
                CanonicalJson.Sha256(current.GeneratedCardTranscript) !=
                    CanonicalJson.Sha256(stored.GeneratedCardTranscript) ||
                CanonicalJson.Sha256(current.DrawInsertionTranscript) !=
                    CanonicalJson.Sha256(stored.DrawInsertionTranscript) ||
                CanonicalJson.Sha256(current.PoolShuffleTranscript) !=
                    CanonicalJson.Sha256(stored.PoolShuffleTranscript) ||
                CanonicalJson.Sha256(current.RandomCostTranscript) !=
                    CanonicalJson.Sha256(stored.RandomCostTranscript) ||
                CanonicalJson.Sha256(current.DrawCostTranscript) !=
                    CanonicalJson.Sha256(stored.DrawCostTranscript) ||
                CanonicalJson.Sha256(current.PotionGenerationTranscript) !=
                    CanonicalJson.Sha256(stored.PotionGenerationTranscript) ||
                CanonicalJson.Sha256(current.CardTransformTranscript) !=
                    CanonicalJson.Sha256(stored.CardTransformTranscript))
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
