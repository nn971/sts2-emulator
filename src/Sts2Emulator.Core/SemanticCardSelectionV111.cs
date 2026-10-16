using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record SemanticCardSelectionCandidateV111(
    string ActionId, long[] CardInstanceIds);

public sealed record SemanticCardSelectionEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, long? SourceCardInstanceId,
    string SourceZone, string Resolution, int MinimumSelections,
    int MaximumSelections, long[] CandidateCardInstanceIds,
    SemanticCardSelectionCandidateV111[] Candidates)
{
    public const string EventSchemaId =
        "sts2-v111-semantic-card-selection-choice-v1";

    public SemanticCardSelectionEventV111 Fork() => this with
    {
        CandidateCardInstanceIds = (long[])CandidateCardInstanceIds.Clone(),
        Candidates = Candidates.Select(candidate => candidate with
        {
            CardInstanceIds = (long[])candidate.CardInstanceIds.Clone()
        }).ToArray()
    };

    public void Validate()
    {
        if (SchemaId != EventSchemaId || string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            string.IsNullOrWhiteSpace(SourceZone) ||
            string.IsNullOrWhiteSpace(Resolution) ||
            CandidateCardInstanceIds is null || Candidates is null ||
            CandidateCardInstanceIds.Distinct().Count() !=
                CandidateCardInstanceIds.Length || Candidates.Length == 0 ||
            Candidates.Any(candidate => candidate is null ||
                string.IsNullOrWhiteSpace(candidate.ActionId) ||
                candidate.CardInstanceIds is null ||
                candidate.CardInstanceIds.Distinct().Count() !=
                    candidate.CardInstanceIds.Length))
            throw new InvalidDataException(
                "Invalid semantic card-selection event.");
    }
}

public sealed record SemanticCardSelectionDecisionV111(
    SemanticCardSelectionEventV111 Event, string ActionId,
    long[] SelectedCardInstanceIds)
{
    public SemanticCardSelectionDecisionV111 Fork() => this with
    {
        Event = Event.Fork(),
        SelectedCardInstanceIds = (long[])SelectedCardInstanceIds.Clone()
    };

    public void Validate()
    {
        Event.Validate();
        var candidate = Event.Candidates.SingleOrDefault(item =>
            item.ActionId == ActionId);
        if (candidate is null || SelectedCardInstanceIds is null ||
            !candidate.CardInstanceIds.SequenceEqual(SelectedCardInstanceIds))
            throw new InvalidDataException(
                "Semantic card-selection decision is outside its event support.");
    }
}

/// <summary>
/// One ordinary source-generated select_cards action that changes the
/// draw pile while retaining the provider-owned Nature constraints.
/// Separate from the broad play-card controller: the source pending choice
/// may be created by an earlier card, relic or potion continuation.
/// </summary>
public sealed record SemanticCardSelectionV111(
    RunState Origin, RunState Run, string ActionId,
    long[] SelectedCardInstanceIds, string Operation,
    SemanticCardSelectionDecisionV111? Decision = null)
{
    public const string SchemaId = "sts2-v111-semantic-card-selection-v1";
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    public SemanticCardSelectionV111 Fork() => this with
    {
        Origin = Origin.Fork(), Run = Run.Fork(),
        SelectedCardInstanceIds = (long[])SelectedCardInstanceIds.Clone(),
        Decision = Decision?.Fork()
    };
}

public static class SemanticCardSelectionEngineV111
{
    public static SemanticCardSelectionEventV111 DescribePending(
        RunState source, string boundaryFingerprint)
    {
        ValidatePendingBoundary(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        var choice = source.World!.Combat!.PendingChoice!;
        ValidateSupportedChoice(choice);
        var actions = new PrototypeAiEnvironment().Observe(source).LegalActions
            .Where(action => action.Kind == "select_cards")
            .Select(action => new SemanticCardSelectionCandidateV111(
                action.ActionId,
                action.Payload.Deserialize<SelectCardsPayload>()?.CardInstanceIds
                    ?? throw new InvalidDataException(
                        "Invalid source selected-card action payload.")))
            .ToArray();
        var supportFingerprint = CanonicalJson.Sha256(new
        {
            choice.ChoiceId,
            choice.SourceCardInstanceId,
            choice.Selection,
            choice.CandidateCardInstanceIds,
            Candidates = actions
        });
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId = SemanticCardSelectionEventV111.EventSchemaId,
            BoundaryFingerprint = boundaryFingerprint,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticCardSelectionEventV111(
            SemanticCardSelectionEventV111.EventSchemaId, eventId,
            boundaryFingerprint, supportFingerprint,
            choice.SourceCardInstanceId,
            choice.Selection.SourceZone.ToString(),
            choice.Selection.Resolution.ToString(),
            choice.Selection.MinSelections, choice.Selection.MaxSelections,
            (long[])choice.CandidateCardInstanceIds.Clone(), actions);
        semanticEvent.Validate();
        return semanticEvent;
    }

    public static void ValidatePendingBoundary(RunState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var combat = source.World?.Combat;
        var choice = combat!.PendingChoice!;
        if (source.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: null,
                PendingKnowledgeDemonChoice: null } ||
            choice is not { ChoiceId: "select_cards" } ||
            combat.SemanticDeck is not { } deck || deck.Pending is not null)
            throw new InvalidOperationException(
                "Expected a source card-selection choice on a semantic player turn.");
        SemanticDeckEngineV111.Validate(deck);
        if (!SemanticDeckEngineV111.AllCardInstanceIds(deck)
            .Order().SequenceEqual(combat.DrawPile.Order()))
            throw new InvalidDataException(
                "Semantic card-selection source has inconsistent physical pile.");
    }

    public static SemanticCardSelectionV111 Step(
        RunState source, string actionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ValidatePendingBoundary(source);
        var combat = source.World?.Combat;
        var choice = combat!.PendingChoice!;
        var deck = combat!.SemanticDeck!;
        SemanticDeckEngineV111.Validate(deck);
        var semanticEvent = DescribePending(source,
            CanonicalJson.Sha256(source));
        var spec = choice.Selection;
        var supportedTop = IsSupportedTopMove(spec);
        var supportedDrawRemoval = IsSupportedDrawRemoval(spec);

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
        var selectedAction = semanticEvent.Candidates.Single(candidate =>
            candidate.ActionId == actionId);
        var decision = new SemanticCardSelectionDecisionV111(
            semanticEvent, actionId,
            (long[])selectedAction.CardInstanceIds.Clone());
        decision.Validate();
        var result = new SemanticCardSelectionV111(
            source.Fork(), advanced, actionId, (long[])selected.Clone(),
            supportedTop ? "move_to_draw_top" :
                supportedDrawRemoval ? "remove_known_draw_card" :
                    "preserve_draw_pile", decision);
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
            state.SelectedCardInstanceIds.Distinct().Count() !=
                state.SelectedCardInstanceIds.Length ||
            state.Decision is null ||
            state.Operation is not ("move_to_draw_top" or
                "remove_known_draw_card" or "preserve_draw_pile"))
            throw new InvalidDataException(
                "Invalid saved source card selection.");
        SemanticPlayerActionEngineV111.ValidateBoundary(state.Run);
        state.Decision.Validate();
        if (state.Decision.ActionId != state.ActionId ||
            !state.Decision.SelectedCardInstanceIds.SequenceEqual(
                state.SelectedCardInstanceIds) ||
            state.Decision.Event.BoundaryFingerprint !=
                CanonicalJson.Sha256(state.Origin))
            throw new InvalidDataException(
                "Saved card-selection event differs from its source state.");
        var initial = from.SemanticDeck ??
            throw new InvalidDataException("Original semantic deck is absent.");
        SemanticDeckEngineV111.Validate(initial);
        var expected = initial;
        if (state.Operation == "move_to_draw_top")
        {
            foreach (var id in state.SelectedCardInstanceIds)
                expected = SemanticDeckEngineV111.PlaceTop(expected, id);
        }
        else if (state.Operation == "remove_known_draw_card")
        {
            foreach (var id in state.SelectedCardInstanceIds)
                expected = SemanticDeckEngineV111.RemoveKnown(expected, id);
        }
        else if (initial.Fingerprint != to.SemanticDeck!.Fingerprint)
            throw new InvalidDataException(
                "Visible-zone card selection unexpectedly changed the hidden deck.");
        if (expected.Fingerprint != to.SemanticDeck!.Fingerprint ||
            !SemanticDeckEngineV111.AllCardInstanceIds(expected)
                .Order().SequenceEqual(to.DrawPile.Order()))
            throw new InvalidDataException(
                "Source choice mutated the hidden deck inconsistently.");
    }

    private static void ValidateSupportedChoice(PendingCombatChoiceState choice)
    {
        var spec = choice.Selection;
        var supportedTop = IsSupportedTopMove(spec);
        var supportedDrawRemoval = IsSupportedDrawRemoval(spec);
        var supportedVisibleZoneMove =
            (spec.SourceZone is PrototypeCardZone.Hand or
                PrototypeCardZone.DiscardPile or PrototypeCardZone.ExhaustPile) &&
            (spec.Resolution is PrototypeCardSelectionResolutionKind.Preserve or
                PrototypeCardSelectionResolutionKind.MoveToHand or
                PrototypeCardSelectionResolutionKind.MoveToDiscard or
                PrototypeCardSelectionResolutionKind.MoveToExhaust) &&
            !spec.RemoveUnselectedFromSource;
        if ((!supportedTop && !supportedDrawRemoval &&
             !supportedVisibleZoneMove) || spec.RemoveUnselectedFromSource ||
            spec.SequentialOptional ||
            spec.DrawEqualToSelectionsOnCompletion ||
            choice.OuterChoiceContinuation is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.card_selection " +
                "operation=unsupported_nested_source_choice");
    }

    private static bool IsSupportedTopMove(
        PrototypeCardSelectionSpec spec) =>
        spec.Resolution == PrototypeCardSelectionResolutionKind.MoveToDrawTop &&
        spec.SourceZone is PrototypeCardZone.Hand or
            PrototypeCardZone.DiscardPile or PrototypeCardZone.ExhaustPile;

    private static bool IsSupportedDrawRemoval(
        PrototypeCardSelectionSpec spec) =>
        spec.SourceZone == PrototypeCardZone.DrawPile &&
        spec.Resolution is PrototypeCardSelectionResolutionKind.MoveToHand or
            PrototypeCardSelectionResolutionKind.MoveToDiscard or
            PrototypeCardSelectionResolutionKind.MoveToExhaust;
}
