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
    // All ticket decisions are preserved in a single chance transcript.
    // The in-flight intent checkpoint additionally stores the unmodified
    // post-enemy-action combat state and the current pure selector stage.
    public ControlledTurnIntentsV111? TurnIntents { get; init; }
    public RandomIndexDecisionV111[] TurnTranscript { get; init; } = [];
    public ControlledRecycleShuffleV111? Recycle { get; init; }
    public RandomIndexDecisionV111[] RecycleTranscript { get; init; } = [];
    // Chronological live chance transcript, interleaving intents/recycles.
    public RandomIndexDecisionV111[] LiveTranscript { get; init; } = [];
    public RandomIndexDecisionV111[] TargetTranscript { get; init; } = [];
    public RandomIndexDecisionV111[] GeneratedCardTranscript { get; init; } = [];
    public RandomIndexDecisionV111[] DrawInsertionTranscript { get; init; } = [];
    public RandomIndexDecisionV111[] PoolShuffleTranscript { get; init; } = [];
    public RandomIndexDecisionV111[] RandomCostTranscript { get; init; } = [];
    public RandomIndexDecisionV111[] DrawCostTranscript { get; init; } = [];
    public RandomIndexDecisionV111[] PotionGenerationTranscript { get; init; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public RandomIndexRequestV111? ActivePending =>
        DrawCostPending ?? RandomCostPending ?? PotionGenerationPending ?? PoolShufflePending ?? DrawInsertionPending ??
        GeneratedCardPending ?? TargetPending ??
        Recycle?.Pending ?? TurnIntents?.Pending ?? Pending;
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
            var shuffle = combat?.PendingChoice?.ControlledPoolShuffle;
            if (shuffle is null) return null;
            return new RandomIndexRequestV111(
                "combat.generation.full_pool_shuffle.swap_index",
                shuffle.Operation.GeneratedChoiceUsesNativeCardGenerationRng &&
                    combat!.NativeRng?.RunStreams is not null
                    ? "run.combat_card_generation" : "prototype.combat",
                "card:" +
                    (combat!.PendingChoice!.SourceCardInstanceId?.ToString() ??
                        "effect") + ":operation:" + shuffle.Operation.Kind +
                    ":pool:" + CanonicalJson.Sha256(shuffle.OriginalCardIds),
                PoolShuffleTranscript.Length, [],
                OperationKind: "bounded_integer",
                BoundedCount: shuffle.NextSwapIndex + 1);
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
            return new RandomIndexRequestV111(
                "combat.draw_pile.random_insert_index", "prototype.combat",
                owner + ":model:" + choice.SelectedCardId +
                    ":pile:" + CanonicalJson.Sha256(choice.EligibleDrawPile),
                DrawInsertionTranscript.Length, [],
                OperationKind: "bounded_integer",
                BoundedCount: choice.EligibleDrawPile.Length + 1);
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
            return new RandomIndexRequestV111(
                "combat.generated_card.select", "prototype.combat",
                "card:" + (Run.World!.Combat!.PendingChoice!.SourceCardInstanceId?.ToString()
                    ?? "effect") + ":pool:" + CanonicalJson.Sha256(pool),
                GeneratedCardTranscript.Length,
                Enumerable.Range(0, pool.Length).ToArray(),
                OperationKind: "choose_index");
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
            return new RandomIndexRequestV111(
                "combat.potion_generation.random_model", "prototype.combat",
                owner, PotionGenerationTranscript.Length,
                Enumerable.Range(0, generation.CandidatePotionIds.Length)
                    .ToArray(), OperationKind: "choose_index");
        }
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public RandomIndexRequestV111? TargetPending
    {
        get
        {
            var context = Run.World?.Combat?.PendingChoice?.ControlledTarget;
            if (context is null) return null;
            return new RandomIndexRequestV111(
                context.Kind == "sly_auto_play"
                    ? "combat.sly_autoplay.random_enemy"
                    : "combat.effect.random_enemy",
                "run.combat_targets",
                context.SlyCardInstanceId is { } cardId
                    ? "card:" + cardId : "effect_operation",
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
    public ControlledPlayableCombatV111 Fork() => this with
    {
        Origin = Origin.Fork(),
        Run = Run.Fork(),
        ShuffleTranscript = ShuffleTranscript.Select(x => x with
        {
            Request = x.Request with
            {
                OrderedCandidates = (int[])x.Request.OrderedCandidates.Clone()
            }
        }).ToArray(),
        PlayerActions = (string[])PlayerActions.Clone(),
        TurnIntents = TurnIntents?.Fork(),
        Recycle = Recycle?.Fork(),
        TurnTranscript = DeepCopy(TurnTranscript),
        RecycleTranscript = DeepCopy(RecycleTranscript),
        LiveTranscript = DeepCopy(LiveTranscript),
        TargetTranscript = DeepCopy(TargetTranscript),
        GeneratedCardTranscript = DeepCopy(GeneratedCardTranscript),
        DrawInsertionTranscript = DeepCopy(DrawInsertionTranscript),
        PoolShuffleTranscript = DeepCopy(PoolShuffleTranscript),
        RandomCostTranscript = DeepCopy(RandomCostTranscript),
        DrawCostTranscript = DeepCopy(DrawCostTranscript),
        PotionGenerationTranscript = DeepCopy(PotionGenerationTranscript),
        Pending = Pending is null ? null : Pending with
        {
            OrderedCandidates = (int[])Pending.OrderedCandidates.Clone()
        }
    };

    private static RandomIndexDecisionV111[] DeepCopy(RandomIndexDecisionV111[] decisions) =>
        decisions.Select(d => d with
        {
            Request = d.Request with
            {
                OrderedCandidates = (int[])d.Request.OrderedCandidates.Clone()
            }
        }).ToArray();
}

public static class ControlledPlayableCombatEngineV111
{
    public const string SchemaId = "sts2-v111-controlled-playable-fixture-v1";
    public const string ShuffleSite = "controlled.fixture.opening_shuffle.swap_index";

    public static ControlledPlayableCombatV111 Begin(
        ControlledIntentStateV111 completed, string seed)
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
            Hand = [],
            DrawPile = cards.Select(c => c.InstanceId).ToArray(),
            DiscardPile = [],
            ExhaustPile = [],
            Relics = relics,
            Potions = potions,
            CurrentMapNodeId = run.World!.Map.CurrentNodeId,
            IsPlayerTurn = true
        };
        run = run with
        {
            Phase = RunPhase.Combat,
            World = run.World! with
            {
                ActiveRoom = PrototypeRoomType.Combat,
                Combat = combat
            }
        };
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
        if (state.ActivePending is null)
            throw new InvalidOperationException(
                "Controlled state is awaiting a player action, not Nature.");
        if (!StringComparer.Ordinal.Equals(state.Fingerprint, fingerprint))
            throw new InvalidOperationException(
                "Stale controlled chance fingerprint.");
        VerifyStage(state);
        if (state.DrawCostPending is not null)
            return ResolveDrawCost(state, selectedIndex);
        if (state.RandomCostPending is not null)
            return ResolveRandomCost(state, selectedIndex);
        if (state.PotionGenerationPending is not null)
            return ResolvePotionGeneration(state, selectedIndex);
        if (state.PoolShufflePending is not null)
            return ResolvePoolShuffle(state, selectedIndex);
        if (state.DrawInsertionPending is not null)
            return ResolveDrawInsertion(state, selectedIndex);
        if (state.GeneratedCardPending is not null)
            return ResolveGeneratedCard(state, selectedIndex);
        if (state.TargetPending is not null)
            return ResolveTarget(state, selectedIndex);
        if (state.Recycle is not null)
            return ResolveRecycle(state, selectedIndex);
        if (state.TurnIntents is not null)
            return ResolveTurnIntents(state, selectedIndex);
        var fresh = state.Fork();
        var request = fresh.Pending!;
        var decision = new ControlledRandomIndexProviderV111(selectedIndex)
            .Resolve(request, fresh.Origin.ReferenceStreams);
        var combat = fresh.Run.World!.Combat!;
        var drawPile = (long[])combat.DrawPile.Clone();
        (drawPile[fresh.NextShuffleIndex], drawPile[selectedIndex]) =
            (drawPile[selectedIndex], drawPile[fresh.NextShuffleIndex]);
        var nextIndex = fresh.NextShuffleIndex - 1;
        combat = combat with { DrawPile = drawPile };
        var next = fresh with
        {
            Run = fresh.Run with
            {
                World = fresh.Run.World with { Combat = combat }
            },
            NextShuffleIndex = nextIndex,
            ShuffleTranscript = fresh.ShuffleTranscript.Append(decision).ToArray(),
            Pending = MakePending(nextIndex, fresh.ShuffleTranscript.Length + 1)
        };
        return next.Pending is null ? CompleteOpeningHand(next) : next;
    }

    public static ControlledPlayableCombatV111 Step(
        ControlledPlayableCombatV111 state, string actionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        VerifyStage(state);
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
        var decision = new ControlledRandomIndexProviderV111(selectedIndex)
            .Resolve(request, state.Origin.ReferenceStreams);
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.ResumeControlledDrawCost(
                state.Run.Fork(), selectedIndex);
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
        var decision = new ControlledRandomIndexProviderV111(selectedIndex)
            .Resolve(request, state.Origin.ReferenceStreams);
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.ResumeControlledRandomCost(
                state.Run.Fork(), selectedIndex);
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
        var choice = new ControlledRandomIndexProviderV111(selectedIndex)
            .Resolve(request, state.Origin.ReferenceStreams);
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.ResumeControlledPoolShuffle(
                state.Run.Fork(), selectedIndex);
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
        var choice = new ControlledRandomIndexProviderV111(selectedIndex)
            .Resolve(request, state.Origin.ReferenceStreams);
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.ResumeControlledDrawInsertion(
                state.Run.Fork(), selectedIndex);
        var next = state.Fork() with
        {
            Run = result,
            DrawInsertionTranscript = state.DrawInsertionTranscript
                .Append(choice).ToArray(),
            LiveTranscript = state.LiveTranscript.Append(choice).ToArray()
        };
        return StartPendingTurn(next);
    }

    private static ControlledPlayableCombatV111 ResolveGeneratedCard(
        ControlledPlayableCombatV111 state, int selectedIndex)
    {
        var request = state.GeneratedCardPending ??
            throw new InvalidOperationException("Missing generated-card Nature ticket.");
        var choice = new ControlledRandomIndexProviderV111(selectedIndex)
            .Resolve(request, state.Origin.ReferenceStreams);
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.ResumeControlledGeneratedCard(
                state.Run.Fork(), selectedIndex);
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
        var choice = new ControlledRandomIndexProviderV111(selectedIndex)
            .Resolve(request, state.Origin.ReferenceStreams);
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.ResumeControlledPotionGeneration(
                state.Run.Fork(), selectedIndex);
        var next = state.Fork() with
        {
            Run = result,
            PotionGenerationTranscript = state.PotionGenerationTranscript
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
        var choice = new ControlledRandomIndexProviderV111(selectedIndex)
            .Resolve(request, state.Origin.ReferenceStreams);
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.ResumeControlledRandomTarget(
                state.Run.Fork(), selectedIndex);
        var next = state.Fork() with
        {
            Run = result,
            TargetTranscript = state.TargetTranscript.Append(choice).ToArray(),
            LiveTranscript = state.LiveTranscript.Append(choice).ToArray()
        };
        return StartPendingTurn(next);
    }

    private static ControlledPlayableCombatV111 StartPendingTurn(
        ControlledPlayableCombatV111 state)
    {
        if (state.Run.Phase != RunPhase.Combat ||
            state.Run.World?.Combat is not { } combat ||
            combat.AutomaticPipelineContinuation is not { } continuation)
            return state;
        int index = continuation.NextStepIndex;
        if (index < 0 || index >= PrototypeContent.Rules.EndTurnPipeline.Length)
            throw new InvalidOperationException("Saved combat pipeline cursor is invalid.");
        if (continuation.ControlledDrawCostRemaining is not null)
        {
            if (PrototypeContent.Rules.EndTurnPipeline[index].Kind !=
                    PrototypeAutomaticStepKind.DrawPlayerHand ||
                combat.PendingChoice?.ControlledDrawCost is null)
                throw new InvalidDataException("Malformed saved draw-cost boundary.");
            return state;
        }
        if (continuation.ControlledDrawRemaining is not null)
        {
            if (PrototypeContent.Rules.EndTurnPipeline[index].Kind !=
                    PrototypeAutomaticStepKind.DrawPlayerHand ||
                state.Recycle is not null || state.TurnIntents is not null ||
                combat.DrawPile.Length != 0 || combat.DiscardPile.Length == 0)
                throw new InvalidDataException("Malformed saved controlled recycle boundary.");
            var recycle = ControlledRecycleShuffleEngineV111.Begin(
                combat.DiscardPile, state.RecycleTranscript.Length);
            var recycleState = state with { Recycle = recycle };
            return recycle.Complete ? FinishRecycle(recycleState) : recycleState;
        }
        if (PrototypeContent.Rules.EndTurnPipeline[index].Kind !=
                PrototypeAutomaticStepKind.AdvanceTurn)
            return state;
        if (state.TurnIntents is not null)
            throw new InvalidOperationException("Nested intent stage already pending.");
        if (combat.Enemies.Length != state.Origin.Formation.Enemies.Length ||
            combat.Enemies.Where((e, i) =>
                e.EnemyId != state.Origin.Formation.Enemies[i].EnemyId ||
                e.InstanceId != state.Origin.Formation.MonsterContexts[i].CombatId)
                .Any())
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE site=combat.next_turn_intent.formation_changed operation=formation");
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
            RecycleTranscript = state.RecycleTranscript.Append(choice).ToArray(),
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
            Run = result
        });
    }

    private static ControlledPlayableCombatV111 FinishTurnIntents(
        ControlledPlayableCombatV111 state)
    {
        var current = state.TurnIntents ??
            throw new InvalidOperationException("No turn intents to commit.");
        if (!current.Complete)
            throw new InvalidOperationException("Nature intent tickets still pending.");
        ControlledTurnIntentEngineV111.Validate(current);
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.ResumeControlledTurnAfterIntents(
                state.Run.Fork(), current.Combat.Fork());
        return StartPendingTurn(state with
        {
            TurnIntents = null,
            Run = result
        });
    }

    public static string Save(ControlledPlayableCombatV111 state)
    {
        VerifyStage(state);
        return JsonSerializer.Serialize(state);
    }

    public static ControlledPlayableCombatV111 Restore(string snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot);
        var stored = JsonSerializer.Deserialize<ControlledPlayableCombatV111>(snapshot)
            ?? throw new InvalidDataException("Null controlled playable snapshot.");
        // Full provenance replay, from source-verified controlled HP+intent
        // completion through the entire ordered shuffle and player transcript.
        var current = Begin(stored.Origin, stored.Seed);
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
                stored.PotionGenerationTranscript))
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
                    CanonicalJson.Sha256(stored.PotionGenerationTranscript))
                throw new InvalidDataException("Chance transcripts diverged.");
        }
        if (CanonicalJson.Sha256(current) != CanonicalJson.Sha256(stored))
            throw new InvalidDataException(
                "Controlled playable snapshot diverges from its provenance.");
        return current;
    }

    internal static void VerifyStage(ControlledPlayableCombatV111 state)
    {
        if (state.Mode != "controlled-v111-playable-fixture-v1" ||
            state.Run.Phase is not (RunPhase.Combat or RunPhase.Reward or RunPhase.Terminal) ||
            state.Run.World?.Combat is null && state.Pending is not null ||
            state.ShuffleTranscript is null || state.PlayerActions is null ||
            state.TurnTranscript is null || state.RecycleTranscript is null ||
            state.LiveTranscript is null || state.TargetTranscript is null ||
            state.GeneratedCardTranscript is null ||
            state.DrawInsertionTranscript is null ||
            state.PoolShuffleTranscript is null ||
            state.RandomCostTranscript is null ||
            state.DrawCostTranscript is null ||
            state.PotionGenerationTranscript is null || state.Origin is null ||
            (state.TurnIntents is not null && state.Pending is not null) ||
            (state.Recycle is not null &&
                (state.Pending is not null || state.TurnIntents is not null)))
            throw new InvalidDataException("Malformed controlled playable stage.");
        var drawnCostCombat = state.Run.World?.Combat;
        var drawnCost = drawnCostCombat?.PendingChoice?.ControlledDrawCost;
        if (drawnCost is not null)
        {
            var automatic = drawnCostCombat?.AutomaticPipelineContinuation;
            if (state.Pending is not null || state.Recycle is not null ||
                state.TurnIntents is not null ||
                drawnCostCombat?.PendingChoice?.ControlledPotionGeneration is not null ||
                !drawnCost.FromHandDraw ||
                automatic is null ||
                automatic.ControlledDrawCostRemaining != drawnCost.RemainingCount ||
                automatic.ControlledDrawRemaining is not null ||
                !PrototypeGameEngine.ValidateControlledDrawCost(
                    state.Run.Player, drawnCostCombat!, drawnCost))
                throw new InvalidDataException(
                    "Saved drawn-card cost is not source-reachable.");
        }
        var costCombat = state.Run.World?.Combat;
        var randomCost = costCombat?.PendingChoice?.ControlledRandomCost;
        if (randomCost is not null &&
            (state.Pending is not null || state.Recycle is not null ||
             state.TurnIntents is not null ||
             costCombat?.PendingChoice?.ControlledTarget is not null ||
             costCombat?.PendingChoice?.ControlledGeneration is not null ||
             costCombat?.PendingChoice?.ControlledPotionGeneration is not null ||
             costCombat?.PendingChoice?.ControlledDrawInsertion is not null ||
             costCombat?.PendingChoice?.ControlledPoolShuffle is not null ||
             !PrototypeGameEngine.ValidateControlledRandomCost(
                 costCombat!, randomCost)))
            throw new InvalidDataException(
                "Saved hand-cost ticket disagrees with source eligibility.");
        var poolCombat = state.Run.World?.Combat;
        var pool = poolCombat?.PendingChoice?.ControlledPoolShuffle;
        if (pool is not null &&
            (state.Pending is not null || state.Recycle is not null ||
             state.TurnIntents is not null ||
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
            if (consumed < 0 || consumed > state.PoolShuffleTranscript.Length)
                throw new InvalidDataException(
                    "Pool shuffle cursor exceeds recorded Nature decisions.");
            var current = (string[])pool.OriginalCardIds.Clone();
            var active = state.PoolShufflePending!;
            var offset = state.PoolShuffleTranscript.Length - consumed;
            for (int step = 0; step < consumed; step++)
            {
                var recorded = state.PoolShuffleTranscript[offset + step];
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
        var insertionCombat = state.Run.World?.Combat;
        var insertion = insertionCombat?.PendingChoice?.ControlledDrawInsertion;
        if (insertion is not null &&
            (state.Pending is not null || state.Recycle is not null ||
             state.TurnIntents is not null ||
             insertionCombat?.PendingChoice?.ControlledTarget is not null ||
             insertionCombat?.PendingChoice?.ControlledGeneration is not null ||
             insertionCombat?.PendingChoice?.ControlledPotionGeneration is not null ||
             !PrototypeGameEngine.ValidateControlledDrawInsertion(
                 insertionCombat!, insertion)))
            throw new InvalidDataException(
                "Draw insertion chance disagrees with source eligibility.");
        var generated = state.Run.World?.Combat?.PendingChoice?.ControlledGeneration;
        if (generated is not null &&
            (state.Pending is not null || state.Recycle is not null ||
             state.TurnIntents is not null ||
             state.Run.World?.Combat?.PendingChoice?.ControlledTarget is not null ||
             state.Run.World?.Combat?.PendingChoice?.ControlledPotionGeneration is not null ||
             !PrototypeGameEngine.ValidateControlledGenerationSupport(generated)))
            throw new InvalidDataException(
                "Malformed or stale controlled generated-card candidate pool.");
        var potionCombat = state.Run.World?.Combat;
        var potionGeneration = potionCombat?.PendingChoice?.ControlledPotionGeneration;
        if (potionGeneration is not null &&
            (state.Pending is not null || state.Recycle is not null ||
             state.TurnIntents is not null ||
             potionCombat?.PendingChoice?.ControlledTarget is not null ||
             potionCombat?.PendingChoice?.ControlledGeneration is not null ||
             potionCombat?.PendingChoice?.ControlledPoolShuffle is not null ||
             !PrototypeGameEngine.ValidateControlledPotionGeneration(
                 potionCombat!, potionGeneration,
                 potionCombat!.PendingChoice?.SourceCardInstanceId)))
            throw new InvalidDataException(
                "Malformed or stale controlled potion-generation candidate pool.");
        var controlledTarget = state.Run.World?.Combat?.PendingChoice?.ControlledTarget;
        if (controlledTarget is not null)
        {
            var combat = state.Run.World?.Combat ??
                throw new InvalidDataException("Target choice lacks combat state.");
            var eligible = combat.Enemies
                .Where(enemy => enemy.Hp > 0 &&
                    (controlledTarget.Kind != "sly_auto_play" || !enemy.Escaped))
                .Select(enemy => enemy.InstanceId).ToArray();
            if (state.Recycle is not null || state.TurnIntents is not null ||
                state.Pending is not null ||
                combat.PendingChoice?.ControlledPotionGeneration is not null ||
                controlledTarget.Kind is not ("effect_random_enemy" or "sly_auto_play") ||
                eligible.Length == 0 ||
                !eligible.SequenceEqual(controlledTarget.EligibleEnemyInstanceIds) ||
                (controlledTarget.Kind == "sly_auto_play" &&
                    (controlledTarget.SlyCardInstanceId is not { } cardId ||
                     !combat.DiscardPile.Contains(cardId))))
                throw new InvalidDataException(
                    "Malformed or stale controlled target-choice support.");
        }
        var expected = MakePending(state.NextShuffleIndex, state.ShuffleTranscript.Length);
        if (CanonicalJson.Sha256(expected) != CanonicalJson.Sha256(state.Pending))
            throw new InvalidDataException("Controlled shuffle stage is stale.");
        if (state.Recycle is { } recycle)
        {
            ControlledRecycleShuffleEngineV111.Validate(recycle);
            var combat = state.Run.World?.Combat;
            if (combat is null ||
                !combat.DiscardPile.SequenceEqual(recycle.Baseline) ||
                combat.DrawPile.Length != 0 ||
                combat.AutomaticPipelineContinuation?.ControlledDrawRemaining is not > 0)
                throw new InvalidDataException(
                    "Recycle chance state disagrees with suspended draw.");
        }
        if (state.TurnIntents is { } intent)
        {
            ControlledTurnIntentEngineV111.Validate(intent);
            if (state.Run.World?.Combat is not { } combat ||
                CanonicalJson.Sha256(combat) !=
                    CanonicalJson.Sha256(intent.BaselineCombat))
                throw new InvalidDataException(
                    "Controlled turn-intent checkpoint lost its precommit state.");
        }
    }

    private static RandomIndexRequestV111? MakePending(int index, int ordinal) =>
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
        combat = combat with
        {
            Hand = hand.ToArray(),
            DrawPile = pile.ToArray()
        };
        return state with
        {
            Run = state.Run with
            {
                World = state.Run.World with { Combat = combat }
            }
        };
    }
}
