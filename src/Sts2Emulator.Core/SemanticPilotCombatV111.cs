using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Sts2Emulator.Core;

/// <summary>One externally replayable action or Nature decision.</summary>
public sealed record SemanticPilotTransitionV111(
    string Kind, string? ActionId = null, int? SelectedIndex = null);

/// <summary>
/// First external playable semantic boundary. Initial opening order is
/// already committed by the separate controlled fixture: this provider
/// copies those exact hidden positions without issuing new Nature tickets.
/// Subsequent fresh recycle epochs are deferred one card at a time.
/// </summary>
public sealed record SemanticPilotCombatV111(
    ControlledPlayableCombatV111 OpeningSource,
    RunState Run, SemanticEndTurnV111? PendingEndTurn,
    SemanticPilotTransitionV111[] History,
    string NativeEncounterEntry,
    string Mode = "sts2-v111-semantic-playable-p0-v1")
{
    public const string SchemaId = "sts2-v111-semantic-playable-p0-v1";
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    [JsonIgnore] public bool AtPlayerBoundary =>
        PendingEndTurn is null &&
        Run.Phase == RunPhase.Combat &&
        Run.World?.Combat is { IsPlayerTurn: true,
            PendingChoice: null, AutomaticPipelineContinuation: null };
    [JsonIgnore] public bool CombatTerminal =>
        Run.Phase is RunPhase.Reward or RunPhase.Terminal;
    [JsonIgnore] public string? CombatOutcome =>
        Run.Phase switch
        {
            RunPhase.Reward => "victory",
            RunPhase.Terminal => "defeat",
            _ => null
        };
    [JsonIgnore] public RandomIndexRequestV111? IntentPending =>
        PendingEndTurn?.IntentPending;
    [JsonIgnore] public SemanticDeckEventV111? DrawPending =>
        PendingEndTurn?.DrawPending;

    public SemanticPilotCombatV111 Fork() => this with
    {
        OpeningSource = OpeningSource.Fork(),
        Run = Run.Fork(),
        PendingEndTurn = PendingEndTurn?.Fork(),
        History = (SemanticPilotTransitionV111[])History.Clone()
    };
}

public static class SemanticPilotCombatEngineV111
{
    public static SemanticPilotCombatV111 Begin(
        ControlledPlayableCombatV111 completedOpening)
    {
        ArgumentNullException.ThrowIfNull(completedOpening);
        // The source fixture is independently replayable. Never accept its
        // partially committed legacy shuffle or a non-player checkpoint.
        var source = ControlledPlayableCombatEngineV111.Restore(
            ControlledPlayableCombatEngineV111.Save(completedOpening));
        if (!source.AtPlayerBoundary || source.Run.Phase != RunPhase.Combat ||
            source.Run.World?.Combat is not { } combat ||
            combat.SemanticDeck is not null ||
            combat.AutomaticPipelineContinuation is not null ||
            combat.PendingChoice is not null)
            throw new InvalidOperationException(
                "Semantic playable entry requires a completely committed opening fixture.");
        var deck = SemanticDeckEngineV111.FromCommittedOpening(
            combat.DrawPile.Reverse().ToArray());
        var run = source.Run.Fork();
        run = run with
        {
            World = run.World! with
            {
                Combat = combat.Fork() with { SemanticDeck = deck }
            }
        };
        var state = new SemanticPilotCombatV111(
            source.Fork(), run, null, [],
            source.Origin.Formation.NativeEncounterEntry);
        Validate(state);
        return state;
    }

    public static SemanticPilotCombatV111 Step(
        SemanticPilotCombatV111 current, string fingerprint, string actionId)
    {
        Validate(current);
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException("Stale semantic player-action fingerprint.");
        if (!current.AtPlayerBoundary)
            throw new InvalidOperationException("Nature decision or combat terminal is pending.");
        var environment = new PrototypeAiEnvironment();
        var frame = environment.Observe(current.Run);
        var action = frame.LegalActions.SingleOrDefault(a =>
            a.ActionId == actionId) ?? throw new InvalidOperationException(
                "Illegal semantic player action.");
        var source = current.Run.Fork();
        SemanticEndTurnV111? endTurn = null;
        RunState result;
        if (action.Kind == "end_turn")
        {
            endTurn = SemanticEndTurnEngineV111.BeginFromPlayerAction(
                source, current.NativeEncounterEntry);
            result = endTurn.Run;
        }
        else if (action.Kind == "play_card")
        {
            var play = action.Payload.Deserialize<PlayCardPayload>() ??
                throw new InvalidDataException("Missing source card payload.");
            var card = source.World!.Combat!.Cards.SingleOrDefault(c =>
                c.InstanceId == play.CardInstanceId);
            if (card?.CardId is not ("proto.silent.strike" or "proto.silent.defend"))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=card_effect_requires_semantic_audit");
            // Preserve actual source damage, relic/power hooks and victory
            // transition. The private deck must remain unchanged.
            using (ControlledChanceGuardV111.Enter())
                result = environment.Step(source, actionId).State;
            if (result.Phase is not (RunPhase.Combat or RunPhase.Reward))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=unsupported_post_card_phase");
            if (result.Phase == RunPhase.Combat)
                SemanticPlayerActionEngineV111.ValidateBoundary(result);
            var previous = current.Run.World!.Combat!;
            var next = result.World?.Combat;
            if (next is null ||
                !previous.DrawPile.SequenceEqual(next.DrawPile) ||
                CanonicalJson.Sha256(previous.SemanticDeck) !=
                    CanonicalJson.Sha256(next.SemanticDeck))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=unmodeled_draw_pile_mutation");
        }
        else
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action operation=" +
                action.Kind);
        var nextState = current with
        {
            Run = result, PendingEndTurn = endTurn,
            History = current.History.Append(
                new SemanticPilotTransitionV111("player", actionId)).ToArray()
        };
        return NormalizeNoncontrolledCosts(nextState);
    }

    public static SemanticPilotCombatV111 Resolve(
        SemanticPilotCombatV111 current, string fingerprint, int selectedIndex)
    {
        Validate(current);
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException("Stale semantic Nature fingerprint.");
        var pending = current.PendingEndTurn ??
            throw new InvalidOperationException("No Nature decision is pending.");
        if (pending.IntentPending is null && pending.DrawPending is null)
            throw new InvalidOperationException(
                "Only semantic enemy_intent and draw_card are Nature-controlled.");
        var resolved = SemanticEndTurnEngineV111.Resolve(
            pending, pending.Fingerprint, selectedIndex);
        return NormalizeNoncontrolledCosts(current with
        {
            Run = resolved.Run,
            PendingEndTurn = resolved.Complete ? null : resolved,
            History = current.History.Append(
                new SemanticPilotTransitionV111("nature",
                    SelectedIndex: selectedIndex)).ToArray()
        });
    }

    /// <summary>
    /// The initial two-site pilot does not optimize drawn-card costs.
    /// Bound and source hook are taken from the actual semantic cost
    /// continuation, but sampling is an explicitly declared engine-owned
    /// deterministic fallback, NOT an audited native RNG distribution.
    /// </summary>
    private static SemanticPilotCombatV111 NormalizeNoncontrolledCosts(
        SemanticPilotCombatV111 current)
    {
        while (current.PendingEndTurn is { CostPending: { } cost } endTurn)
        {
            var bound = cost.SupportCount;
            if (bound < 1)
                throw new InvalidDataException("Invalid engine-owned cost bound.");
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
                current.OpeningSource.Seed + ":" +
                endTurn.Fingerprint + ":" + cost.SiteId));
            var index = (int)(BitConverter.ToUInt32(hash, 0) % (uint)bound);
            var resolved = SemanticEndTurnEngineV111.Resolve(
                endTurn, endTurn.Fingerprint, index);
            current = current with
            {
                Run = resolved.Run,
                PendingEndTurn = resolved.Complete ? null : resolved
            };
        }
        Validate(current);
        return current;
    }

    public static void Validate(SemanticPilotCombatV111 state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Mode != SemanticPilotCombatV111.SchemaId ||
            state.History is null ||
            state.NativeEncounterEntry != state.OpeningSource.Origin
                .Formation.NativeEncounterEntry ||
            state.Run.Phase is not (
                RunPhase.Combat or RunPhase.Reward or RunPhase.Terminal))
            throw new InvalidDataException("Invalid semantic playable session.");
        if (state.PendingEndTurn is { } endTurn)
        {
            SemanticEndTurnEngineV111.Validate(endTurn);
            if (endTurn.Complete || endTurn.CostPending is not null ||
                CanonicalJson.Sha256(endTurn.Run) != CanonicalJson.Sha256(state.Run))
                throw new InvalidDataException(
                    "Semantic playable and end-turn continuation diverged.");
        }
        if (state.AtPlayerBoundary)
            SemanticPlayerActionEngineV111.ValidateBoundary(state.Run);
        if (state.CombatTerminal && state.PendingEndTurn is not null)
            throw new InvalidDataException(
                "Terminal combat retained an unresolved chance.");
    }

    public static string Save(SemanticPilotCombatV111 state)
    {
        Validate(state);
        return JsonSerializer.Serialize(state);
    }

    public static SemanticPilotCombatV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var saved = JsonSerializer.Deserialize<SemanticPilotCombatV111>(json)
            ?? throw new InvalidDataException("Null semantic playable snapshot.");
        Validate(saved);
        var replay = Begin(saved.OpeningSource);
        foreach (var transition in saved.History)
        {
            replay = transition.Kind switch
            {
                "player" when transition.ActionId is not null =>
                    Step(replay, replay.Fingerprint, transition.ActionId),
                "nature" when transition.SelectedIndex is not null =>
                    Resolve(replay, replay.Fingerprint,
                        transition.SelectedIndex.Value),
                _ => throw new InvalidDataException(
                    "Unknown semantic playable transcript stage.")
            };
        }
        if (CanonicalJson.Sha256(replay) != CanonicalJson.Sha256(saved))
            throw new InvalidDataException(
                "Semantic playable snapshot disagrees with action/Nature replay.");
        return replay;
    }
}
