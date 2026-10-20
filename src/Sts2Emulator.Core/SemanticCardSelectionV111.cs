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
    string[] CandidateCardModelIds,
    SemanticCardSelectionCandidateV111[] Candidates)
{
    public const string EventSchemaId =
        "sts2-v111-semantic-card-selection-choice-v1";

    public SemanticCardSelectionEventV111 Fork() => this with
    {
        CandidateCardInstanceIds = (long[])CandidateCardInstanceIds.Clone(),
        CandidateCardModelIds = (string[])CandidateCardModelIds.Clone(),
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
            CandidateCardInstanceIds is null || CandidateCardModelIds is null ||
            CandidateCardModelIds.Length != CandidateCardInstanceIds.Length ||
            CandidateCardModelIds.Any(string.IsNullOrWhiteSpace) ||
            Candidates is null ||
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
/// One ordinary source-generated select_cards action and its resulting
/// provider-owned draw-pile projection. Separate from the broad play-card
/// controller: the source pending choice may be created by an earlier card,
/// relic or potion continuation.
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
            CandidateCardModelIds = choice.CandidateCardInstanceIds.Select(id =>
                source.World!.Combat!.Cards.Single(card =>
                    card.InstanceId == id).CardId).ToArray(),
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
            (long[])choice.CandidateCardInstanceIds.Clone(),
            choice.CandidateCardInstanceIds.Select(id =>
                source.World!.Combat!.Cards.Single(card =>
                    card.InstanceId == id).CardId).ToArray(), actions);
        semanticEvent.Validate();
        return semanticEvent;
    }

    public static void ValidatePendingBoundary(RunState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var combat = source.World?.Combat;
        var choice = combat?.PendingChoice;
        bool automaticStratagem =
            choice?.AutomaticSemanticStratagemDrawSelection == true;
        bool automaticAutoplay = combat is not null && choice is not null &&
            PrototypeGameEngine
                .IsAutomaticPipelineSemanticCardSelectionContinuation(
                    combat, choice);
        if (source.Phase != RunPhase.Combat ||
            combat is not { PendingKnowledgeDemonChoice: null } ||
            (!automaticStratagem && !automaticAutoplay &&
             !combat.IsPlayerTurn) ||
            choice is not { ChoiceId: "select_cards" } ||
            combat.SemanticDeck is not { } deck || deck.Pending is not null)
            throw new InvalidOperationException(
                "Expected a source card-selection choice on a semantic player turn.");
        if (automaticStratagem)
        {
            if (choice!.SemanticStratagemDrawSelection ||
                !PrototypeGameEngine.IsSemanticAutomaticHandDraw(combat!))
                throw new InvalidDataException(
                    "Automatic Stratagem draw cursor is inconsistent.");
        }
        else if (!automaticAutoplay &&
                 combat!.AutomaticPipelineContinuation is not null)
            throw new InvalidOperationException(
                "Expected an ordinary semantic player-turn choice.");
        SemanticDrawPileAdapterV111.ValidateSynchronized(
            combat, requireIdle: true);
        if (automaticStratagem || automaticAutoplay)
            PrototypeStateInvariants.Validate(source);
    }

    public static SemanticCardSelectionDecisionV111 Select(
        SemanticCardSelectionEventV111 pending, string eventId,
        string actionId)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Card-selection event ID is stale or mismatched.");
        var candidate = pending.Candidates.SingleOrDefault(item =>
            item.ActionId == actionId) ?? throw new InvalidOperationException(
                "Action is outside the pending card-selection support.");
        var decision = new SemanticCardSelectionDecisionV111(
            pending.Fork(), actionId,
            (long[])candidate.CardInstanceIds.Clone());
        decision.Validate();
        return decision;
    }

    internal static void ValidateDecision(
        SemanticCardSelectionEventV111? pending,
        SemanticCardSelectionDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        if (pending is null || pending.EventId != decision.Event.EventId ||
            CanonicalJson.Sha256(pending) !=
                CanonicalJson.Sha256(decision.Event))
            throw new InvalidDataException(
                "Card-selection decision differs from its live semantic event.");
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
        var automaticAutoplay =
            PrototypeGameEngine
                .IsAutomaticPipelineSemanticCardSelectionContinuation(
                    combat, choice);
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
        var selectedUnknownPosition = supportedDrawRemoval && selected.Any(id =>
            !deck.Slots.Any(slot => slot.KnownCardInstanceId == id));

        // Removing an unresolved identity is exact when its shared pool is
        // one contiguous run: deleting any one exchangeable slot leaves the
        // same remaining order constraints. A pool split around known
        // anchors needs an additional latent-position continuation.
        if (supportedDrawRemoval)
        {
            deck = SemanticDeckEngineV111.RemoveCardInstances(deck, selected);
        }

        RunState advanced;
        using (ControlledChanceGuardV111.Enter())
        using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
            advanced = environment.Step(source.Fork(), actionId).State;
        if (advanced.Phase == RunPhase.Combat)
        {
            if (choice.AutomaticSemanticStratagemDrawSelection)
            {
                if (advanced.World?.Combat is not { PendingChoice: null } after ||
                    !PrototypeGameEngine.IsSemanticAutomaticHandDraw(after))
                    throw new InvalidDataException(
                        "Automatic Stratagem selection lost its draw cursor.");
                PrototypeStateInvariants.Validate(advanced);
            }
            else if (automaticAutoplay)
                ValidateAutomaticPipelineResult(advanced);
            else
                SemanticPilotCombatEngineV111.ValidateSemanticCombatBoundary(
                    advanced);
        }
        else if (advanced.Phase == RunPhase.Reward &&
                 advanced.World?.Reward is not null &&
                 advanced.World.Combat is null)
            PrototypeStateInvariants.Validate(advanced);
        else
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.card_selection " +
                "operation=unsupported_post_selection_phase");
        var selectedAction = semanticEvent.Candidates.Single(candidate =>
            candidate.ActionId == actionId);
        var decision = new SemanticCardSelectionDecisionV111(
            semanticEvent, actionId,
            (long[])selectedAction.CardInstanceIds.Clone());
        decision.Validate();
        var result = new SemanticCardSelectionV111(
            source.Fork(), advanced, actionId, (long[])selected.Clone(),
            supportedTop ? "move_to_draw_top" :
                supportedDrawRemoval
                    ? selectedUnknownPosition
                        ? "remove_unresolved_draw_card_identity"
                        : "remove_known_draw_card"
                    :
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
        var automaticAutoplay = from is not null &&
            from.PendingChoice is { } sourceChoice &&
            PrototypeGameEngine
                .IsAutomaticPipelineSemanticCardSelectionContinuation(
                    from, sourceChoice);
        var reward = state.Run.Phase == RunPhase.Reward &&
            state.Run.World?.Reward is not null && to is null;
        if (from?.PendingChoice?.ChoiceId != "select_cards" ||
            (to is null && !reward) || state.SelectedCardInstanceIds is null ||
            state.SelectedCardInstanceIds.Distinct().Count() !=
                state.SelectedCardInstanceIds.Length ||
            state.Decision is null ||
            state.Operation is not ("move_to_draw_top" or
                "remove_known_draw_card" or
                "remove_unresolved_draw_card_identity" or
                "preserve_draw_pile"))
            throw new InvalidDataException(
                "Invalid saved source card selection.");
        if (state.Run.Phase == RunPhase.Combat)
        {
            if (from.PendingChoice?.AutomaticSemanticStratagemDrawSelection ==
                true)
            {
                if (to?.PendingChoice is not null ||
                    !PrototypeGameEngine.IsSemanticAutomaticHandDraw(to!))
                    throw new InvalidDataException(
                        "Saved automatic Stratagem selection lost its draw cursor.");
                PrototypeStateInvariants.Validate(state.Run);
            }
            else if (automaticAutoplay)
                ValidateAutomaticPipelineResult(state.Run);
            else
                SemanticPilotCombatEngineV111.ValidateSemanticCombatBoundary(
                    state.Run);
        }
        else if (reward)
            PrototypeStateInvariants.Validate(state.Run);
        else
            throw new InvalidDataException(
                "Saved source card selection has an unsupported result phase.");
        state.Decision.Validate();
        if (state.Decision.ActionId != state.ActionId ||
            !state.Decision.SelectedCardInstanceIds.SequenceEqual(
                state.SelectedCardInstanceIds) ||
            state.Decision.Event.BoundaryFingerprint !=
                CanonicalJson.Sha256(state.Origin))
            throw new InvalidDataException(
                "Saved card-selection event differs from its source state.");
        var describedEvent = DescribePending(state.Origin,
            CanonicalJson.Sha256(state.Origin));
        if (CanonicalJson.Sha256(describedEvent) !=
            CanonicalJson.Sha256(state.Decision.Event))
            throw new InvalidDataException(
                "Saved card-selection event differs from its source support.");
        var selectionSpec = from!.PendingChoice!.Selection;
        var topMove = IsSupportedTopMove(selectionSpec);
        var drawRemoval = IsSupportedDrawRemoval(selectionSpec);
        var initial = from.SemanticDeck ??
            throw new InvalidDataException("Original semantic deck is absent.");
        SemanticDeckEngineV111.Validate(initial);
        var selectedUnknown = state.SelectedCardInstanceIds.Any(id =>
            initial.Pools.Any(pool =>
                pool.RemainingCardInstanceIds.Contains(id)));
        var expectedOperation = topMove
            ? "move_to_draw_top"
            : drawRemoval
                ? selectedUnknown
                    ? "remove_unresolved_draw_card_identity"
                    : "remove_known_draw_card"
                : "preserve_draw_pile";
        if (state.Operation != expectedOperation)
            throw new InvalidDataException(
                "Saved card-selection operation differs from its source effect.");
        if ((state.Operation == "remove_known_draw_card" && selectedUnknown) ||
            (state.Operation == "remove_unresolved_draw_card_identity" &&
             !selectedUnknown))
            throw new InvalidDataException(
                "Saved draw-pile removal kind differs from its source identities.");
        var expected = initial;
        if (state.Operation == "move_to_draw_top")
        {
            foreach (var id in state.SelectedCardInstanceIds)
                expected = SemanticDeckEngineV111.PlaceTop(expected, id);
        }
        else if (state.Operation is "remove_known_draw_card" or
            "remove_unresolved_draw_card_identity")
        {
            expected = state.Operation == "remove_known_draw_card"
                ? state.SelectedCardInstanceIds.Aggregate(expected,
                    SemanticDeckEngineV111.RemoveKnown)
                : SemanticDeckEngineV111.RemoveCardInstances(
                    expected, state.SelectedCardInstanceIds);
        }
        else if (!reward && initial.Fingerprint != to!.SemanticDeck!.Fingerprint)
            throw new InvalidDataException(
                "Visible-zone card selection unexpectedly changed the hidden deck.");
        var resumesDraw = from.PendingChoice.Continuation.Any(operation =>
            operation.Kind == PrototypeCombatEffectKind.DrawCards);
        if (!reward && resumesDraw)
        {
            var resultCombat = to ?? throw new InvalidDataException(
                "Resumed card draw lost its combat state.");
            if (resultCombat.SemanticDeck is null)
                throw new InvalidDataException(
                    "Resumed card draw lost the provider-owned deck.");
            SemanticDrawPileAdapterV111.ValidateSynchronized(resultCombat);
            RunState replayed;
            using (ControlledChanceGuardV111.Enter())
            using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
                replayed = new PrototypeAiEnvironment().Step(
                    state.Origin.Fork(), state.ActionId).State;
            if (CanonicalJson.Sha256(replayed) !=
                CanonicalJson.Sha256(state.Run))
                throw new InvalidDataException(
                    "Saved draw continuation disagrees with source-choice replay.");
        }
        else if (!reward)
        {
            if (expected.Fingerprint != to!.SemanticDeck!.Fingerprint)
                throw new InvalidDataException(
                    "Source choice mutated the hidden deck inconsistently.");
            SemanticDrawPileAdapterV111.ValidateProviderCorrespondence(
                expected, to.DrawPile);
        }
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
        var supportedGeneratedChoicePool =
            spec.SourceZone == PrototypeCardZone.ChoicePool &&
            spec.Resolution == PrototypeCardSelectionResolutionKind.MoveToHand &&
            spec.RemoveUnselectedFromSource;
        if ((!supportedTop && !supportedDrawRemoval &&
             !supportedVisibleZoneMove && !supportedGeneratedChoicePool) ||
            (spec.RemoveUnselectedFromSource &&
             !supportedGeneratedChoicePool) ||
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

    private static void ValidateAutomaticPipelineResult(RunState run)
    {
        var combat = run.World?.Combat;
        bool supportedPending =
            PrototypeGameEngine.HasPendingControlledAutomaticPipelineChoice(
                run) ||
            PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineSemanticChoice(run);
        bool supportedPipelineCheckpoint =
            combat?.AutomaticPipelineContinuation is not null &&
            ((combat.PendingChoice is null &&
              combat.PendingKnowledgeDemonChoice is null) || supportedPending);
        if (!PrototypeGameEngine.IsAtCombatPlayerDecisionBoundary(run) &&
            !supportedPipelineCheckpoint)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.card_selection " +
                "operation=unsupported_automatic_pipeline_result");
        PrototypeStateInvariants.Validate(run);
    }
}
