using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record SemanticCardSelectionCandidateV111(
    string ActionId, long[] CardInstanceIds)
{
    private long[]? _cardInstanceIds = CloneOptional(CardInstanceIds);

    public long[] CardInstanceIds
    {
        get => CloneOptional(_cardInstanceIds)!;
        init => _cardInstanceIds = CloneOptional(value);
    }

    public SemanticCardSelectionCandidateV111 Fork() => this with
    {
        CardInstanceIds = _cardInstanceIds!
    };

    private static long[]? CloneOptional(long[]? values) => values is null
        ? null
        : (long[])values.Clone();
}

public sealed record SemanticCardSelectionEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, long? SourceCardInstanceId,
    string SourceZone, string Resolution, int MinimumSelections,
    int MaximumSelections, long[] CandidateCardInstanceIds,
    string[] CandidateCardModelIds,
    SemanticCardSelectionCandidateV111[] Candidates)
{
    private long[]? _candidateCardInstanceIds =
        CloneOptional(CandidateCardInstanceIds);
    private string[]? _candidateCardModelIds =
        CloneOptional(CandidateCardModelIds);
    private SemanticCardSelectionCandidateV111[]? _candidates =
        CloneCandidates(Candidates);

    public long[] CandidateCardInstanceIds
    {
        get => CloneOptional(_candidateCardInstanceIds)!;
        init => _candidateCardInstanceIds = CloneOptional(value);
    }
    public string[] CandidateCardModelIds
    {
        get => CloneOptional(_candidateCardModelIds)!;
        init => _candidateCardModelIds = CloneOptional(value);
    }
    public SemanticCardSelectionCandidateV111[] Candidates
    {
        get => CloneCandidates(_candidates)!;
        init => _candidates = CloneCandidates(value);
    }

    public const string EventSchemaId =
        "sts2-v111-semantic-card-selection-choice-v1";
    public const string SequentialEventSchemaId =
        "sts2-v111-semantic-card-selection-choice-v2";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long[]? SequentialSelectedCardInstanceIds
    {
        get => CloneOptional(_sequentialSelectedCardInstanceIds);
        init => _sequentialSelectedCardInstanceIds = CloneOptional(value);
    }
    private long[]? _sequentialSelectedCardInstanceIds;

    public SemanticCardSelectionEventV111 Fork() => this with
    {
        CandidateCardInstanceIds = _candidateCardInstanceIds!,
        CandidateCardModelIds = _candidateCardModelIds!,
        SequentialSelectedCardInstanceIds =
            _sequentialSelectedCardInstanceIds,
        Candidates = _candidates!
    };

    public void Validate()
    {
        bool sequential = SchemaId == SequentialEventSchemaId;
        var candidateCardInstanceIds = _candidateCardInstanceIds;
        var candidateCardModelIds = _candidateCardModelIds;
        var candidates = _candidates;
        var sequentialSelectedCardInstanceIds =
            _sequentialSelectedCardInstanceIds;
        if (SchemaId is not (EventSchemaId or SequentialEventSchemaId) ||
            sequential != (sequentialSelectedCardInstanceIds is not null) ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            string.IsNullOrWhiteSpace(SourceZone) ||
            string.IsNullOrWhiteSpace(Resolution) ||
            candidateCardInstanceIds is null ||
            candidateCardModelIds is null ||
            candidateCardModelIds.Length != candidateCardInstanceIds.Length ||
            candidateCardModelIds.Any(string.IsNullOrWhiteSpace) ||
            candidates is null ||
            candidateCardInstanceIds.Distinct().Count() !=
                candidateCardInstanceIds.Length || candidates.Length == 0 ||
            candidates.Any(candidate => candidate is null ||
                string.IsNullOrWhiteSpace(candidate.ActionId) ||
                candidate.CardInstanceIds is null ||
                candidate.CardInstanceIds.Distinct().Count() !=
                    candidate.CardInstanceIds.Length) ||
            sequentialSelectedCardInstanceIds is { } sequentialSelected &&
                (sequentialSelected.Distinct().Count() !=
                     sequentialSelected.Length ||
                 sequentialSelected.Any(
                     candidateCardInstanceIds!.Contains)))
            throw new InvalidDataException(
                "Invalid semantic card-selection event.");
    }

    private static long[]? CloneOptional(long[]? values) => values is null
        ? null
        : (long[])values.Clone();

    private static string[]? CloneOptional(string[]? values) => values is null
        ? null
        : (string[])values.Clone();

    private static SemanticCardSelectionCandidateV111[]? CloneCandidates(
        SemanticCardSelectionCandidateV111[]? values) => values?.Select(
            candidate => candidate is null ? null! : candidate.Fork()).ToArray();
}

public sealed record SemanticCardSelectionDecisionV111(
    SemanticCardSelectionEventV111 Event, string ActionId,
    long[] SelectedCardInstanceIds)
{
    private SemanticCardSelectionEventV111? _event = Event?.Fork();
    private long[]? _selectedCardInstanceIds =
        CloneOptional(SelectedCardInstanceIds);

    public SemanticCardSelectionEventV111 Event
    {
        get => _event!.Fork();
        init => _event = value?.Fork();
    }
    public long[] SelectedCardInstanceIds
    {
        get => CloneOptional(_selectedCardInstanceIds)!;
        init => _selectedCardInstanceIds = CloneOptional(value);
    }

    public SemanticCardSelectionDecisionV111 Fork() => this with
    {
        Event = _event!,
        SelectedCardInstanceIds = _selectedCardInstanceIds!
    };

    public void Validate()
    {
        var semanticEvent = _event ?? throw new InvalidDataException(
            "Semantic card-selection decision has no event.");
        var selectedCardInstanceIds = _selectedCardInstanceIds;
        semanticEvent.Validate();
        var candidate = semanticEvent.Candidates.SingleOrDefault(item =>
            item.ActionId == ActionId);
        if (candidate is null || selectedCardInstanceIds is null ||
            !candidate.CardInstanceIds.SequenceEqual(
                selectedCardInstanceIds))
            throw new InvalidDataException(
                "Semantic card-selection decision is outside its event support.");
    }

    private static long[]? CloneOptional(long[]? values) => values is null
        ? null
        : (long[])values.Clone();
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
    private long[]? _selectedCardInstanceIds =
        CloneOptional(SelectedCardInstanceIds);

    public long[] SelectedCardInstanceIds
    {
        get => CloneOptional(_selectedCardInstanceIds)!;
        init => _selectedCardInstanceIds = CloneOptional(value);
    }

    public const string SchemaId = "sts2-v111-semantic-card-selection-v1";
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    public SemanticCardSelectionV111 Fork() => this with
    {
        Origin = Origin.Fork(), Run = Run.Fork(),
        SelectedCardInstanceIds = _selectedCardInstanceIds!,
        Decision = Decision?.Fork()
    };

    private static long[]? CloneOptional(long[]? values) => values is null
        ? null
        : (long[])values.Clone();
}

public static class SemanticCardSelectionEngineV111
{
    public static SemanticCardSelectionEventV111 DescribePending(
        RunState source, string boundaryFingerprint)
    {
        ValidatePendingBoundary(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        var combat = source.World!.Combat!;
        var choice = combat.PendingChoice!;
        ValidateSupportedChoice(combat, choice);
        var actions = new PrototypeAiEnvironment().Observe(source).LegalActions
            .Where(action => action.Kind == "select_cards")
            .Select(action => new SemanticCardSelectionCandidateV111(
                action.ActionId,
                action.Payload.Deserialize<SelectCardsPayload>()?.CardInstanceIds
                    ?? throw new InvalidDataException(
                        "Invalid source selected-card action payload.")))
            .ToArray();
        var candidateModels = choice.CandidateCardInstanceIds.Select(id =>
            combat.Cards.Single(card => card.InstanceId == id).CardId).ToArray();
        var sequentialSelected = choice.Selection.SequentialOptional
            ? (choice.SequentialSelectedCardInstanceIds ?? []).ToArray()
            : null;
        var schema = sequentialSelected is null
            ? SemanticCardSelectionEventV111.EventSchemaId
            : SemanticCardSelectionEventV111.SequentialEventSchemaId;
        object support = sequentialSelected is null
            ? new
            {
                choice.ChoiceId,
                choice.SourceCardInstanceId,
                choice.Selection,
                choice.CandidateCardInstanceIds,
                CandidateCardModelIds = candidateModels,
                Candidates = actions
            }
            : new
            {
                choice.ChoiceId,
                choice.SourceCardInstanceId,
                choice.Selection,
                choice.CandidateCardInstanceIds,
                CandidateCardModelIds = candidateModels,
                SequentialSelectedCardInstanceIds = sequentialSelected,
                Candidates = actions
            };
        var supportFingerprint = CanonicalJson.Sha256(support);
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId = schema,
            BoundaryFingerprint = boundaryFingerprint,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticCardSelectionEventV111(
            schema, eventId,
            boundaryFingerprint, supportFingerprint,
            choice.SourceCardInstanceId,
            choice.Selection.SourceZone.ToString(),
            choice.Selection.Resolution.ToString(),
            choice.Selection.MinSelections, choice.Selection.MaxSelections,
            (long[])choice.CandidateCardInstanceIds.Clone(),
            candidateModels, actions)
        {
            SequentialSelectedCardInstanceIds = sequentialSelected
        };
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
        bool automaticPipelineChoice = combat is not null && choice is not null &&
            PrototypeGameEngine
                .IsAutomaticPipelineSemanticCardSelectionContinuation(
                    combat, choice);
        bool automaticHandDrawHookChoice = combat is not null &&
            choice is not null && PrototypeGameEngine
                .IsAutomaticHandDrawHookCardSelectionContinuation(
                    combat, choice);
        if (source.Phase != RunPhase.Combat ||
            combat is not { PendingKnowledgeDemonChoice: null } ||
            (!automaticStratagem && !automaticPipelineChoice &&
             !automaticHandDrawHookChoice &&
             !combat.IsPlayerTurn) ||
            choice is not { ChoiceId: "select_cards" } ||
            combat.Stochastic.SemanticDeck is not { } deck || deck.Pending is not null)
            throw new InvalidOperationException(
                "Expected a source card-selection choice on a semantic player turn.");
        if (automaticStratagem)
        {
            if (choice!.SemanticStratagemDrawSelection ||
                !PrototypeGameEngine
                    .HasSemanticAutomaticHandDrawProgress(combat!))
                throw new InvalidDataException(
                    "Automatic Stratagem draw cursor is inconsistent.");
        }
        else if (!automaticPipelineChoice && !automaticHandDrawHookChoice &&
                 combat!.AutomaticPipelineContinuation is not null)
            throw new InvalidOperationException(
                "Expected an ordinary semantic player-turn choice.");
        SemanticDrawPileAdapterV111.ValidateSynchronized(
            combat, requireIdle: true);
        if (automaticStratagem || automaticPipelineChoice ||
            automaticHandDrawHookChoice)
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
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        var semanticEvent = DescribePending(
            source, CanonicalJson.Sha256(source));
        var decision = Select(
            semanticEvent, semanticEvent.EventId, actionId);
        return ResumeValidated(source, semanticEvent, decision).Selection;
    }

    public static SemanticCardSelectionV111 Resume(
        RunState source, SemanticCardSelectionDecisionV111 decision)
        => ResumeWithBoundary(source, decision).Selection;

    internal static (
        SemanticCardSelectionV111 Selection,
        PrototypeAutomaticPipelineResumeResultV111 Resume)
        ResumeWithBoundary(
            RunState source, SemanticCardSelectionDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var current = DescribePending(source, CanonicalJson.Sha256(source));
        ValidateDecision(current, decision);
        return ResumeValidated(source, current, decision.Fork());
    }

    private static (
        SemanticCardSelectionV111 Selection,
        PrototypeAutomaticPipelineResumeResultV111 Resume) ResumeValidated(
        RunState source, SemanticCardSelectionEventV111 semanticEvent,
        SemanticCardSelectionDecisionV111 decision)
    {
        decision.Validate();
        ValidateDecision(semanticEvent, decision);
        var combat = source.World?.Combat;
        var choice = combat!.PendingChoice!;
        var deck = combat!.Stochastic.SemanticDeck!;
        SemanticDeckEngineV111.Validate(deck);
        var automaticPipelineChoice =
            PrototypeGameEngine
                .IsAutomaticPipelineSemanticCardSelectionContinuation(
                    combat, choice);
        var automaticHandDrawHookChoice = PrototypeGameEngine
            .IsAutomaticHandDrawHookCardSelectionContinuation(
                combat, choice);
        var spec = choice.Selection;
        var supportedTop = IsSupportedTopMove(spec);
        var supportedDrawRemoval = IsSupportedDrawRemoval(spec);
        var actionId = decision.ActionId;
        var selected = decision.SelectedCardInstanceIds;
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

        var pipelineResume = ControlledChanceGuardV111.ExecuteGuarded(
            () => PrototypeGameEngine.ResumeSemanticCardSelectionWithBoundary(
                source.Fork(), selected),
            allowEngineOwnedPrototypeReward: true);
        var advanced = pipelineResume.Run;
        if (advanced.Phase == RunPhase.Combat)
        {
            if (choice.AutomaticSemanticStratagemDrawSelection)
            {
                if (advanced.World?.Combat is not { PendingChoice: null } after ||
                    !PrototypeGameEngine
                        .HasSemanticAutomaticHandDrawProgress(after))
                    throw new InvalidDataException(
                        "Automatic Stratagem selection lost its draw cursor.");
                PrototypeStateInvariants.Validate(advanced);
            }
            else if (automaticPipelineChoice)
                ValidateAutomaticPipelineResult(advanced);
            else if (automaticHandDrawHookChoice)
                ValidateAutomaticHandDrawHookResult(advanced);
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
        var result = new SemanticCardSelectionV111(
            source.Fork(), advanced, actionId, (long[])selected.Clone(),
            supportedTop ? "move_to_draw_top" :
                supportedDrawRemoval
                    ? selectedUnknownPosition
                        ? "remove_unresolved_draw_card_identity"
                        : "remove_known_draw_card"
                    :
                    "preserve_draw_pile", decision.Fork());
        Validate(result);
        return (result, pipelineResume);
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
        var automaticPipelineChoice = from is not null &&
            from.PendingChoice is { } sourceChoice &&
            PrototypeGameEngine
                .IsAutomaticPipelineSemanticCardSelectionContinuation(
                    from, sourceChoice);
        var automaticHandDrawHookChoice = from is not null &&
            from.PendingChoice is { } handDrawSourceChoice &&
            PrototypeGameEngine
                .IsAutomaticHandDrawHookCardSelectionContinuation(
                    from, handDrawSourceChoice);
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
                    !PrototypeGameEngine
                        .HasSemanticAutomaticHandDrawProgress(to!))
                    throw new InvalidDataException(
                        "Saved automatic Stratagem selection lost its draw cursor.");
                PrototypeStateInvariants.Validate(state.Run);
            }
            else if (automaticPipelineChoice)
                ValidateAutomaticPipelineResult(state.Run);
            else if (automaticHandDrawHookChoice)
                ValidateAutomaticHandDrawHookResult(state.Run);
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
        var initial = from.Stochastic.SemanticDeck ??
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
        else if (!reward && initial.Fingerprint !=
            to!.Stochastic.SemanticDeck!.Fingerprint)
            throw new InvalidDataException(
                "Visible-zone card selection unexpectedly changed the hidden deck.");
        var resumesGeneratedSequentialDraw =
            IsCompletedSequentialSelectionWithGeneratedDraw(
                from, state.SelectedCardInstanceIds);
        var resumesDraw = HasPendingDrawOperation(from.PendingChoice) ||
            resumesGeneratedSequentialDraw;
        if (!reward && resumesDraw)
        {
            var resultCombat = to ?? throw new InvalidDataException(
                "Resumed card draw lost its combat state.");
            if (resultCombat.Stochastic.SemanticDeck is null)
                throw new InvalidDataException(
                    "Resumed card draw lost the provider-owned deck.");
            SemanticDrawPileAdapterV111.ValidateSynchronized(resultCombat);
            var replayed = ControlledChanceGuardV111.ExecuteGuarded(
                () => new PrototypeAiEnvironment().Step(
                    state.Origin.Fork(), state.ActionId).State,
                allowEngineOwnedPrototypeReward: true);
            if (CanonicalJson.Sha256(replayed) !=
                CanonicalJson.Sha256(state.Run))
                throw new InvalidDataException(
                    "Saved draw continuation disagrees with source-choice replay.");
        }
        else if (!reward)
        {
            if (expected.Fingerprint !=
                to!.Stochastic.SemanticDeck!.Fingerprint)
                throw new InvalidDataException(
                    "Source choice mutated the hidden deck inconsistently.");
            SemanticDrawPileAdapterV111.ValidateProviderCorrespondence(
                expected, to.DrawPile);
        }
    }

    private static bool HasPendingDrawOperation(
        PendingCombatChoiceState choice)
    {
        if (choice.Continuation.Any(operation =>
                operation.Kind == PrototypeCombatEffectKind.DrawCards))
            return true;
        for (var continuation = choice.OuterChoiceContinuation;
             continuation is not null;
             continuation = continuation.Parent)
            if (continuation.Operations.Any(operation =>
                    operation.Kind == PrototypeCombatEffectKind.DrawCards))
                return true;
        return false;
    }

    internal static void ValidateAutomaticHandDrawHookResult(RunState run)
    {
        var combat = run.World?.Combat;
        if (run.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true,
                PendingKnowledgeDemonChoice: null,
                SemanticDeck: { Mode: SemanticDeckStateV111.NatureMode } } ||
            combat.AutomaticPipelineContinuation is not { } cursor ||
            !(cursor.Stochastic.TryGetSemanticHandDrawProgress(
                  cursor.Mechanics, out _, out _) ||
              cursor.Stochastic.IsAtSemanticBeforeHandDraw(
                  cursor.Mechanics)) ||
            cursor.Stochastic.IsAtSemanticBeforeHandDraw(
                cursor.Mechanics) &&
                cursor.Stochastic.CompletedSemanticHandDrawRequested is not null)
            throw new InvalidDataException(
                "Automatic hand-draw child lost its semantic cursor.");
        PrototypeStateInvariants.Validate(run);
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Card-selection child diverged from its semantic draw pile.",
            "Card-selection child diverged from its semantic draw pile.",
            allowPending: true);
    }

    private static bool IsCompletedSequentialSelectionWithGeneratedDraw(
        CombatState combat, long[] selectedByAction)
    {
        if (combat.PendingChoice is not { } choice ||
            !choice.Selection.SequentialOptional ||
            !choice.Selection.DrawEqualToSelectionsOnCompletion ||
            (!PrototypeGameEngine.IsAutomaticPipelineSemanticCardSelectionContinuation(
                 combat, choice) &&
             !IsSupportedManualPotionSequentialChoice(combat, choice)))
            return false;

        var accumulated = choice.SequentialSelectedCardInstanceIds?.Length ?? 0;
        bool completed = selectedByAction.Length == 0 ||
            accumulated + selectedByAction.Length >=
                choice.Selection.MaxSelections ||
            selectedByAction.Length == choice.CandidateCardInstanceIds.Length;
        return completed && accumulated + selectedByAction.Length > 0;
    }

    private static void ValidateSupportedChoice(
        CombatState combat, PendingCombatChoiceState choice)
    {
        var spec = choice.Selection;
        bool supportedSequential = spec.SequentialOptional &&
            spec.DrawEqualToSelectionsOnCompletion &&
            (PrototypeGameEngine
                 .IsAutomaticPipelineSemanticCardSelectionContinuation(
                     combat, choice) ||
             IsSupportedManualPotionSequentialChoice(combat, choice));
        bool automaticHandDrawHookChoice =
            PrototypeGameEngine
                .IsAutomaticHandDrawHookCardSelectionContinuation(
                    combat, choice);
        bool supportedAutomaticTransform =
            spec.SourceZone == PrototypeCardZone.Hand &&
            spec.Resolution ==
                PrototypeCardSelectionResolutionKind.TransformRandom &&
            (automaticHandDrawHookChoice ||
             PrototypeGameEngine
                 .IsPlayerTurnStartedEntropySelection(combat, choice));
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
        var supportedDualWieldCopy =
            IsSupportedDualWieldChoice(combat, choice);
        var supportedGeneratedChoicePool =
            spec.SourceZone == PrototypeCardZone.ChoicePool &&
            spec.Resolution == PrototypeCardSelectionResolutionKind.MoveToHand &&
            spec.RemoveUnselectedFromSource;
        if ((!supportedTop && !supportedDrawRemoval &&
             !supportedAutomaticTransform &&
             !supportedVisibleZoneMove && !supportedGeneratedChoicePool &&
             !supportedDualWieldCopy) ||
            (spec.RemoveUnselectedFromSource &&
             !supportedGeneratedChoicePool) ||
            (spec.SequentialOptional && !supportedSequential) ||
            (spec.DrawEqualToSelectionsOnCompletion &&
             !supportedSequential) ||
            (choice.OuterChoiceContinuation is not null &&
             !automaticHandDrawHookChoice))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.card_selection " +
                "operation=unsupported_nested_source_choice");
    }

    private static bool IsSupportedDualWieldChoice(
        CombatState combat, PendingCombatChoiceState choice)
    {
        if (!combat.IsPlayerTurn || choice.SourceCardInstanceId is not { } sourceId ||
            choice.OuterChoiceContinuation is not null ||
            choice.EventDispatchContinuation is not null ||
            choice.Continuation.Length != 0 ||
            choice.SourceCardDestination != PrototypeCardZone.DiscardPile ||
            !choice.MoveSourceCardOnCompletion ||
            choice.RemoveSourceCardOnCompletion ||
            choice.SourceCardAlreadyMoved ||
            choice.CardPlaySeries is not
                { SourceCardDestination: PrototypeCardZone.DiscardPile,
                  PlayCount: 1, NextPlayIndex: 0,
                  RemoveSourceCardOnCompletion: false, IsAutoPlay: false } series ||
            series.SourceCardInstanceId != sourceId)
            return false;

        var sourceCard = combat.Cards.SingleOrDefault(card =>
            card.InstanceId == sourceId);
        if (sourceCard?.CardId !=
                PrototypeNativeUnderdocksTrashHeap.DualWieldCardId ||
            !combat.Hand.Contains(sourceId))
            return false;

        var effects = PrototypeContent.Card(sourceCard.CardId).Effects;
        if (effects.Length != 1 ||
            effects[0].Kind != PrototypeCombatEffectKind.ChooseCards ||
            effects[0].Selection is not { } sourceSelection)
            return false;

        var expectedSelection = sourceSelection with
        {
            MaxSelections = sourceSelection.MaxSelections +
                sourceSelection.MaxSelectionsUpgradeDelta *
                    sourceCard.UpgradeLevel,
            CopiesPerSelection = sourceSelection.CopiesPerSelection +
                sourceSelection.CopiesPerSelectionUpgradeDelta *
                    sourceCard.UpgradeLevel
        };
        var expectedCandidates = combat.Hand.Where(id =>
        {
            var candidate = combat.Cards.Single(card =>
                card.InstanceId == id);
            return PrototypeContent.Card(candidate.CardId).Type is
                PrototypeCardType.Attack or PrototypeCardType.Power;
        }).ToArray();
        var selection = choice.Selection;
        return expectedCandidates.Length > 0 &&
            choice.CandidateCardInstanceIds.SequenceEqual(expectedCandidates) &&
            selection.SourceZone == PrototypeCardZone.Hand &&
            selection.Resolution ==
                PrototypeCardSelectionResolutionKind.CopyToHand &&
            selection.MinSelections == 1 && selection.MaxSelections == 1 &&
            selection.RequiredCardType is null &&
            selection.SelectionsPerPowerStack == 0 &&
            !selection.RemoveUnselectedFromSource &&
            !selection.RequireEnergyCostingCard &&
            !selection.SequentialOptional &&
            !selection.DrawEqualToSelectionsOnCompletion &&
            selection.RandomCandidateCount == 0 &&
            selection.RequireAttackOrPower &&
            selection.CopiesPerSelection == expectedSelection.CopiesPerSelection &&
            CanonicalJson.Sha256(selection) ==
                CanonicalJson.Sha256(expectedSelection);
    }

    private static bool IsSupportedManualPotionSequentialChoice(
        CombatState combat, PendingCombatChoiceState choice)
    {
        var spec = choice.Selection;
        var accumulated = choice.SequentialSelectedCardInstanceIds?.Length ?? 0;
        return combat.IsPlayerTurn &&
            choice.SourceCardInstanceId is null &&
            choice.OuterChoiceContinuation is null &&
            choice.Continuation.Length == 0 &&
            choice.CompletionEvents is
                [{ Kind: PrototypeCombatEventKind.PotionUsed }] &&
            spec.SourceZone == PrototypeCardZone.Hand &&
            spec.Resolution == PrototypeCardSelectionResolutionKind.MoveToDiscard &&
            spec.MinSelections == 0 &&
            spec.RequiredCardType is null &&
            spec.SelectionsPerPowerStack == 0 &&
            !spec.RemoveUnselectedFromSource &&
            !spec.RequireEnergyCostingCard &&
            spec.SequentialOptional &&
            spec.DrawEqualToSelectionsOnCompletion &&
            spec.MaxSelections ==
                accumulated + choice.CandidateCardInstanceIds.Length;
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
        bool supportedPending = PrototypeGameEngine
            .HasPendingSupportedAutomaticPipelineChoice(run);
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
