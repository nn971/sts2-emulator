using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// One ordinary source-generated select_cards action that changes the
/// draw pile while retaining the provider-owned Nature constraints.
/// Separate from the broad play-card controller: the source pending choice
/// may be created by an earlier card, relic or potion continuation.
/// </summary>
public sealed record SemanticCardSelectionV111(
    RunState Origin, RunState Run, string ActionId,
    long[] SelectedCardInstanceIds, string Operation)
{
    public const string SchemaId = "sts2-v111-semantic-card-selection-v1";
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    public SemanticCardSelectionV111 Fork() => this with
    {
        Origin = Origin.Fork(), Run = Run.Fork(),
        SelectedCardInstanceIds = (long[])SelectedCardInstanceIds.Clone()
    };
}

public static class SemanticCardSelectionEngineV111
{
    public static SemanticCardSelectionV111 Step(
        RunState source, string actionId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        var combat = source.World?.Combat;
        var choice = combat?.PendingChoice;
        if (source.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: null,
                PendingKnowledgeDemonChoice: null } ||
            choice is not { ChoiceId: "select_cards" } ||
            combat.SemanticDeck is not { } deck ||
            deck.Pending is not null)
            throw new InvalidOperationException(
                "Expected a source card-selection choice on a semantic player turn.");
        SemanticDeckEngineV111.Validate(deck);
        if (!SemanticDeckEngineV111.AllCardInstanceIds(deck)
            .Order().SequenceEqual(combat.DrawPile.Order()))
            throw new InvalidDataException(
                "Semantic card-selection source has inconsistent physical pile.");
        var spec = choice.Selection;
        var supportedTop =
            spec.Resolution == PrototypeCardSelectionResolutionKind.MoveToDrawTop &&
            spec.SourceZone is PrototypeCardZone.Hand
                or PrototypeCardZone.DiscardPile
                or PrototypeCardZone.ExhaustPile;
        var supportedDrawRemoval =
            spec.SourceZone == PrototypeCardZone.DrawPile &&
            spec.Resolution is PrototypeCardSelectionResolutionKind.MoveToHand
                or PrototypeCardSelectionResolutionKind.MoveToDiscard
                or PrototypeCardSelectionResolutionKind.MoveToExhaust;
        if ((!supportedTop && !supportedDrawRemoval) ||
            spec.RemoveUnselectedFromSource ||
            spec.SequentialOptional ||
            spec.DrawEqualToSelectionsOnCompletion ||
            choice.SourceCardInstanceId is not null ||
            choice.Continuation.Length != 0 ||
            choice.CompletionEvents.Length != 0 ||
            choice.OuterChoiceContinuation is not null ||
            choice.CardPlaySeries is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.card_selection " +
                "operation=unsupported_nested_source_choice");

        var environment = new PrototypeAiEnvironment();
        var action = environment.Observe(source).LegalActions
            .SingleOrDefault(a => a.ActionId == actionId) ??
            throw new InvalidOperationException("Illegal selected-card action ID.");
        if (action.Kind != "select_cards")
            throw new InvalidOperationException(
                "Expected source select_cards action.");
        var selected = action.Payload.Deserialize<SelectCardsPayload>()
            ?.CardInstanceIds ?? throw new InvalidDataException(
                "Invalid selected-card action payload.");

        // The concrete cards in an unknown draw-pile pool do not yet
        // have established positions. An explicit source selection from
        // the draw pile needs a revealed location, or a new Nature ticket.
        if (supportedDrawRemoval && selected.Any(id =>
            !deck.Slots.Any(slot => slot.KnownCardInstanceId == id)))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.card_selection " +
                "operation=unknown_source_position");

        RunState advanced;
        using (ControlledChanceGuardV111.Enter())
            advanced = environment.Step(source.Fork(), actionId).State;
        SemanticPlayerActionEngineV111.ValidateBoundary(advanced);
        var result = new SemanticCardSelectionV111(
            source.Fork(), advanced, actionId, (long[])selected.Clone(),
            supportedTop ? "move_to_draw_top" : "remove_known_draw_card");
        Validate(result);
        return result;
    }

    public static string Save(SemanticCardSelectionV111 state)
    {
        Validate(state);
        return JsonSerializer.Serialize(state);
    }

    public static SemanticCardSelectionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var saved = JsonSerializer.Deserialize<SemanticCardSelectionV111>(json)
            ?? throw new InvalidDataException("Null semantic card selection.");
        Validate(saved);
        var replayed = Step(saved.Origin, saved.ActionId);
        if (CanonicalJson.Sha256(replayed) != CanonicalJson.Sha256(saved))
            throw new InvalidDataException(
                "Source card-selection mechanical replay disagreed.");
        return replayed;
    }

    public static void Validate(SemanticCardSelectionV111 state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var from = state.Origin.World?.Combat;
        var to = state.Run.World?.Combat;
        if (from?.PendingChoice?.ChoiceId != "select_cards" ||
            to is null || state.SelectedCardInstanceIds is null ||
            state.SelectedCardInstanceIds.Length == 0 ||
            state.SelectedCardInstanceIds.Distinct().Count() !=
                state.SelectedCardInstanceIds.Length ||
            state.Operation is not ("move_to_draw_top" or
                "remove_known_draw_card"))
            throw new InvalidDataException(
                "Invalid saved source card selection.");
        SemanticPlayerActionEngineV111.ValidateBoundary(state.Run);
        var initial = from.SemanticDeck ??
            throw new InvalidDataException("Original semantic deck is absent.");
        SemanticDeckEngineV111.Validate(initial);
        var expected = initial;
        if (state.Operation == "move_to_draw_top")
        {
            foreach (var id in state.SelectedCardInstanceIds)
                expected = SemanticDeckEngineV111.PlaceTop(expected, id);
        }
        else
        {
            foreach (var id in state.SelectedCardInstanceIds)
                expected = SemanticDeckEngineV111.RemoveKnown(expected, id);
        }
        if (expected.Fingerprint != to.SemanticDeck!.Fingerprint ||
            !SemanticDeckEngineV111.AllCardInstanceIds(expected)
                .Order().SequenceEqual(to.DrawPile.Order()))
            throw new InvalidDataException(
                "Source choice mutated the hidden deck inconsistently.");
    }
}
