using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// A restricted real player-action segment between controlled Nature
/// boundaries. The semantic provider is NOT reinitialized. Only actions
/// with audited, draw-pile-neutral semantics are allowed in this version;
/// all unsupported card/relic effects fail before a child is published.
/// </summary>
public sealed record SemanticPlayerActionV111(
    RunState Origin, RunState Run, string ActionId, string ActionKind)
{
    public const string SchemaId = "sts2-v111-semantic-player-action-v1";
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    [JsonIgnore] public bool AtPlayerBoundary =>
        Run.Phase == RunPhase.Combat &&
        Run.World?.Combat is { IsPlayerTurn: true,
            PendingChoice: null, PendingKnowledgeDemonChoice: null,
            AutomaticPipelineContinuation: null };

    public SemanticPlayerActionV111 Fork() => this with
    {
        Origin = Origin.Fork(), Run = Run.Fork()
    };
}

public static class SemanticPlayerActionEngineV111
{
    // This first gate has verified source card effects with no primitive
    // draw-pile mutation. The full action/effect state machine will replace
    // this allowlist as semantic continuations become available.
    private static readonly HashSet<string> SupportedCards =
        new(StringComparer.Ordinal) { "proto.silent.strike", "proto.silent.defend" };

    public static SemanticPlayerActionV111 Step(
        RunState source, string actionId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ValidateBoundary(source);
        var environment = new PrototypeAiEnvironment();
        var action = environment.Observe(source).LegalActions
            .SingleOrDefault(a => a.ActionId == actionId) ??
            throw new InvalidOperationException(
                "Illegal or ambiguous semantic player action.");
        if (action.Kind != "play_card")
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                "operation=" + action.Kind);
        var payload = action.Payload.Deserialize<PlayCardPayload>()
            ?? throw new InvalidDataException("Invalid play-card payload.");
        var combat = source.World!.Combat!;
        var card = combat.Cards.SingleOrDefault(c =>
            c.InstanceId == payload.CardInstanceId);
        if (card is null || !combat.Hand.Contains(payload.CardInstanceId) ||
            !SupportedCards.Contains(card.CardId))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                "operation=card_effect_requires_semantic_audit");

        RunState run;
        using (ControlledChanceGuardV111.Enter())
            run = environment.Step(source.Fork(), actionId).State;
        // The allowlisted action may still activate an unmodeled relic,
        // power, draw hook or enemy mechanic. Assert the hidden deck is
        // untouched before accepting the advanced player boundary.
        ValidateBoundary(run);
        if (!combat.DrawPile.SequenceEqual(run.World!.Combat!.DrawPile) ||
            CanonicalJson.Sha256(combat.SemanticDeck) !=
                CanonicalJson.Sha256(run.World.Combat.SemanticDeck))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                "operation=unmodeled_draw_pile_mutation");

        var result = new SemanticPlayerActionV111(
            source.Fork(), run, actionId, action.Kind);
        Validate(result);
        return result;
    }

    public static string Save(SemanticPlayerActionV111 state)
    {
        Validate(state);
        return JsonSerializer.Serialize(state);
    }

    public static SemanticPlayerActionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var saved = JsonSerializer.Deserialize<SemanticPlayerActionV111>(json)
            ?? throw new InvalidDataException("Null semantic player action.");
        Validate(saved);
        var reproduced = Step(saved.Origin, saved.ActionId);
        if (CanonicalJson.Sha256(reproduced) != CanonicalJson.Sha256(saved))
            throw new InvalidDataException(
                "Semantic player action differs from full source replay.");
        return reproduced;
    }

    public static void Validate(SemanticPlayerActionV111 state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateBoundary(state.Origin);
        ValidateBoundary(state.Run);
        if (state.ActionKind != "play_card" ||
            string.IsNullOrWhiteSpace(state.ActionId) ||
            !state.Origin.World!.Combat!.DrawPile.SequenceEqual(
                state.Run.World!.Combat!.DrawPile) ||
            CanonicalJson.Sha256(state.Origin.World.Combat.SemanticDeck) !=
                CanonicalJson.Sha256(state.Run.World.Combat.SemanticDeck))
            throw new InvalidDataException(
                "Semantic player action changed provider-owned hidden deck.");
    }

    public static void ValidateBoundary(RunState run)
    {
        if (run.Phase != RunPhase.Combat ||
            run.World?.Combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: null, PendingChoice: null,
                PendingKnowledgeDemonChoice: null } combat ||
            combat.SemanticDeck is not { } deck)
            throw new InvalidOperationException(
                "Expected an idle semantic Nature player combat boundary.");
        SemanticDeckEngineV111.Validate(deck);
        if (deck.Mode != SemanticDeckStateV111.NatureMode ||
            deck.Pending is not null ||
            !SemanticDeckEngineV111.AllCardInstanceIds(deck)
                .Order().SequenceEqual(combat.DrawPile.Order()))
            throw new InvalidDataException(
                "Player-boundary physical pile diverges from Nature's private deck.");
    }
}
