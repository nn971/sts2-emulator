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

    [System.Text.Json.Serialization.JsonIgnore]
    public RandomIndexRequestV111? ActivePending =>
        TurnIntents?.Pending ?? Pending;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool AtPlayerBoundary => ActivePending is null && TurnIntents is null;
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
        TurnTranscript = TurnTranscript.Select(d => d with
        {
            Request = d.Request with
            {
                OrderedCandidates = (int[])d.Request.OrderedCandidates.Clone()
            }
        }).ToArray(),
        Pending = Pending is null ? null : Pending with
        {
            OrderedCandidates = (int[])Pending.OrderedCandidates.Clone()
        }
    };
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
        if (act is not (2 or 3))
            throw new NotSupportedException(
                "First playable Nature fixture is restricted to Hive/Glory.");
        // Construct real player/route state; copy already-decided enemy HP and
        // intents without ANY native RNG callback or enemy re-commit.
        var run = PrototypeNativeLaterActFixture.Create(
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

    private static ControlledPlayableCombatV111 StartPendingTurn(
        ControlledPlayableCombatV111 state)
    {
        if (state.Run.Phase != RunPhase.Combat ||
            state.Run.World?.Combat is not { } combat ||
            combat.AutomaticPipelineContinuation is not { } continuation)
            return state;
        int index = continuation.NextStepIndex;
        if (index < 0 || index >= PrototypeContent.Rules.EndTurnPipeline.Length ||
            PrototypeContent.Rules.EndTurnPipeline[index].Kind !=
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
                nextIntents.Transcript[^1]).ToArray()
        };
        return nextIntents.Complete ? FinishTurnIntents(fresh) : fresh;
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
        int turnCursor = 0;
        foreach (var action in stored.PlayerActions)
        {
            current = Step(current, action);
            while (current.TurnIntents?.Pending is not null &&
                turnCursor < stored.TurnTranscript.Length)
            {
                var recorded = stored.TurnTranscript[turnCursor++];
                recorded.Validate();
                if (recorded.ProviderMode != "controlled-v111" ||
                    recorded.ProbabilityStatus != "UNVERIFIED" ||
                    recorded.NativeCursorBefore is not null ||
                    recorded.NativeCursorAfter is not null ||
                    CanonicalJson.Sha256(recorded.Request) !=
                        CanonicalJson.Sha256(current.ActivePending))
                    throw new InvalidDataException(
                        "Unreachable controlled next-turn choice.");
                current = Resolve(current, current.Fingerprint,
                    recorded.SelectedIndex);
            }
        }
        if (turnCursor != stored.TurnTranscript.Length)
            throw new InvalidDataException("Unconsumed controlled turn transcript.");
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
            state.TurnTranscript is null || state.Origin is null ||
            (state.TurnIntents is not null && state.Pending is not null))
            throw new InvalidDataException("Malformed controlled playable stage.");
        var expected = MakePending(state.NextShuffleIndex, state.ShuffleTranscript.Length);
        if (CanonicalJson.Sha256(expected) != CanonicalJson.Sha256(state.Pending))
            throw new InvalidDataException("Controlled shuffle stage is stale.");
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
