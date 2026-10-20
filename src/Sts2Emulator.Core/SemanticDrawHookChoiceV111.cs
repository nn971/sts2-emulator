namespace Sts2Emulator.Core;

public sealed record SemanticDrawHookChoiceCandidateV111(
    string ActionId, string Kind, string PayloadFingerprint);

/// <summary>A source action choice opened by a draw hook while a standalone
/// draw, automatic hand-draw stage, or supported autoplay is suspended.</summary>
public sealed record SemanticDrawHookChoiceEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, string ChoiceId,
    SemanticDrawHookChoiceCandidateV111[] Candidates,
    bool AutomaticHandDraw = false,
    bool BeforeHandDraw = false,
    bool AutomaticPipelineAutoplay = false)
{
    public const string EventSchemaId =
        "sts2-v111-semantic-draw-hook-choice-v2";
    public const string AutomaticPipelineEventSchemaId =
        "sts2-v111-semantic-draw-hook-choice-v3";

    public SemanticDrawHookChoiceEventV111 Fork() => this with
    {
        Candidates = (SemanticDrawHookChoiceCandidateV111[])Candidates.Clone()
    };

    public void Validate()
    {
        if (SchemaId != (AutomaticPipelineAutoplay
                ? AutomaticPipelineEventSchemaId : EventSchemaId) ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            string.IsNullOrWhiteSpace(ChoiceId) ||
            Candidates is not { Length: > 0 } ||
            Candidates.Any(candidate => candidate is null ||
                string.IsNullOrWhiteSpace(candidate.ActionId) ||
                string.IsNullOrWhiteSpace(candidate.Kind) ||
                string.IsNullOrWhiteSpace(candidate.PayloadFingerprint)) ||
            (BeforeHandDraw && !AutomaticHandDraw) ||
            (AutomaticPipelineAutoplay &&
             (AutomaticHandDraw || BeforeHandDraw)) ||
            Candidates.Select(candidate => candidate.ActionId).Distinct()
                .Count() != Candidates.Length ||
            SupportFingerprint != CanonicalJson.Sha256(new
            {
                ChoiceId,
                Candidates
            }) ||
            EventId != CreateEventId(SchemaId, BoundaryFingerprint,
                SupportFingerprint, AutomaticHandDraw, BeforeHandDraw,
                AutomaticPipelineAutoplay))
            throw new InvalidDataException(
                "Invalid semantic draw-hook choice event.");
    }

    internal static string CreateEventId(string schemaId,
        string boundaryFingerprint, string supportFingerprint,
        bool automaticHandDraw, bool beforeHandDraw,
        bool automaticPipelineAutoplay) => automaticPipelineAutoplay
        ? CanonicalJson.Sha256(new
        {
            SchemaId = schemaId,
            BoundaryFingerprint = boundaryFingerprint,
            SupportFingerprint = supportFingerprint,
            AutomaticHandDraw = automaticHandDraw,
            BeforeHandDraw = beforeHandDraw,
            AutomaticPipelineAutoplay = automaticPipelineAutoplay
        })
        : CanonicalJson.Sha256(new
        {
            SchemaId = schemaId,
            BoundaryFingerprint = boundaryFingerprint,
            SupportFingerprint = supportFingerprint,
            AutomaticHandDraw = automaticHandDraw,
            BeforeHandDraw = beforeHandDraw
        });
}

public sealed record SemanticDrawHookChoiceDecisionV111(
    SemanticDrawHookChoiceEventV111 Event, string ActionId)
{
    public SemanticDrawHookChoiceDecisionV111 Fork() => this with
    {
        Event = Event.Fork()
    };

    public void Validate()
    {
        Event.Validate();
        if (!Event.Candidates.Any(candidate => candidate.ActionId == ActionId))
            throw new InvalidDataException(
                "Draw-hook choice action is outside its source support.");
    }
}

public static class SemanticDrawHookChoiceEngineV111
{
    public static SemanticDrawHookChoiceEventV111? DescribePending(
        RunState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var combat = source.World?.Combat;
        var choice = combat?.PendingChoice;
        bool automatic = choice?.AutomaticSemanticDrawHookChoice == true;
        bool beforeHandDraw = automatic &&
            combat?.AutomaticPipelineContinuation?
                .ControlledSemanticBeforeHandDrawPending == true;
        bool automaticPipelineAutoplay = choice is not null &&
            combat is not null &&
            PrototypeGameEngine
                .IsAutomaticPipelineSemanticDrawHookChoiceContinuation(
                    combat, choice);
        if (choice?.StandaloneSemanticDrawHookChoice != true && !automatic)
            return null;
        if (choice is null || source.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true,
                PendingKnowledgeDemonChoice: null,
                SemanticDeck: { Mode: SemanticDeckStateV111.NatureMode,
                    Pending: null } } ||
            choice.ControlledTarget is not null ||
            choice.ControlledGeneration is not null ||
            choice.ControlledPotionGeneration is not null ||
            choice.ControlledDrawCost is not null ||
            choice.ControlledSemanticDraw is not null ||
            choice.ControlledDrawInsertion is not null ||
            choice.ControlledPoolShuffle is not null ||
            choice.ControlledRandomCost is not null ||
            choice.ControlledCardShortlist is not null ||
            choice.ControlledRandomPileCard is not null ||
            choice.SemanticStratagemDrawSelection ||
            choice.AutomaticSemanticStratagemDrawSelection ||
            (automatic
                ? beforeHandDraw
                    ? combat.AutomaticPipelineContinuation?.Mechanics.IsAtStep(
                          PrototypeAutomaticStepKind.DrawPlayerHand) != true ||
                      combat.AutomaticPipelineContinuation
                          .ControlledSemanticDrawRemaining is not null ||
                      combat.AutomaticPipelineContinuation
                          .ControlledSemanticDrawRequested is not null
                    : !PrototypeGameEngine.IsSemanticAutomaticHandDraw(combat)
                : (!automaticPipelineAutoplay &&
                   combat.AutomaticPipelineContinuation is not null) ||
                  !HasStandaloneDrawResume(choice)))
            throw new InvalidDataException(
                "Semantic draw-hook choice cursor is inconsistent.");
        PrototypeStateInvariants.Validate(source);

        var actions = new PrototypeAiEnvironment().Observe(source).LegalActions;
        var candidates = actions.Select(action =>
            new SemanticDrawHookChoiceCandidateV111(
                action.ActionId, action.Kind,
                CanonicalJson.Sha256(action.Payload))).ToArray();
        if (candidates.Length == 0)
            throw new InvalidDataException(
                "Source draw-hook choice has no legal actions.");
        var boundary = CanonicalJson.Sha256(source);
        var support = CanonicalJson.Sha256(new
        {
            choice.ChoiceId,
            Candidates = candidates
        });
        var schemaId = automaticPipelineAutoplay
            ? SemanticDrawHookChoiceEventV111.AutomaticPipelineEventSchemaId
            : SemanticDrawHookChoiceEventV111.EventSchemaId;
        var result = new SemanticDrawHookChoiceEventV111(
            schemaId,
            SemanticDrawHookChoiceEventV111.CreateEventId(
                schemaId, boundary, support, automatic, beforeHandDraw,
                automaticPipelineAutoplay),
            boundary, support, choice.ChoiceId, candidates,
            AutomaticHandDraw: automatic, BeforeHandDraw: beforeHandDraw,
            AutomaticPipelineAutoplay: automaticPipelineAutoplay);
        result.Validate();
        return result;
    }

    public static (RunState Run, SemanticDrawHookChoiceDecisionV111 Decision)
        Resolve(RunState source, SemanticDrawHookChoiceEventV111 pending,
            string actionId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        var current = DescribePending(source);
        if (current is null ||
            CanonicalJson.Sha256(current) != CanonicalJson.Sha256(pending))
            throw new InvalidDataException(
                "Stale semantic draw-hook choice event.");
        var candidate = pending.Candidates.SingleOrDefault(item =>
            item.ActionId == actionId) ?? throw new InvalidOperationException(
                "Action is outside the pending draw-hook choice support.");
        var action = new PrototypeAiEnvironment().Observe(source).LegalActions
            .SingleOrDefault(item => item.ActionId == actionId) ??
            throw new InvalidOperationException(
                "Draw-hook action is no longer legal.");
        if (action.Kind != candidate.Kind ||
            CanonicalJson.Sha256(action.Payload) !=
                candidate.PayloadFingerprint)
            throw new InvalidDataException(
                "Draw-hook action payload changed during resolution.");

        var environment = new PrototypeAiEnvironment();
        RunState advanced;
        bool beforeHandDraw = pending.BeforeHandDraw;
        string site = beforeHandDraw
            ? "combat.BeforeHandDraw" : "combat.draw.CardDrawn";
        using (ControlledChanceGuardV111.Enter())
        using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
            advanced = environment.Step(source.Fork(), actionId).State;
        if (advanced.Phase != RunPhase.Combat ||
            advanced.World?.Combat is not { } advancedCombat)
            throw new NotSupportedException(
                $"UNSUPPORTED_SEMANTIC_DECK site={site} " +
                "operation=unsupported_nested_choice_result");
        if (pending.AutomaticHandDraw &&
            advancedCombat.PendingChoice is { } nextChoice)
        {
            bool nestedBeforeHandDrawRandomPileCard =
                pending.BeforeHandDraw &&
                nextChoice.ChoiceId == "controlled_random_pile_card" &&
                nextChoice.ControlledRandomPileCard is not null;
            if (nextChoice.ControlledTarget is not null ||
                nextChoice.ControlledGeneration is not null ||
                nextChoice.ControlledPotionGeneration is not null ||
                nextChoice.ControlledDrawCost is not null ||
                nextChoice.ControlledSemanticDraw is not null ||
                nextChoice.ControlledDrawInsertion is not null ||
                nextChoice.ControlledPoolShuffle is not null ||
                nextChoice.ControlledRandomCost is not null ||
                nextChoice.ControlledCardShortlist is not null ||
                (nextChoice.ControlledRandomPileCard is not null &&
                 !nestedBeforeHandDrawRandomPileCard) ||
                nextChoice.SemanticStratagemDrawSelection ||
                nextChoice.AutomaticSemanticStratagemDrawSelection)
                throw new NotSupportedException(
                    $"UNSUPPORTED_SEMANTIC_DECK site={site} " +
                    "operation=nested_controlled_choice");
            if (nestedBeforeHandDrawRandomPileCard)
                _ = SemanticRandomPileCardEngineV111.DescribePending(advanced)
                    ?? throw new InvalidDataException(
                        "Nested BeforeHandDraw random pile-card choice has no semantic event.");
            else
            {
                advancedCombat = advancedCombat with
                {
                    PendingChoice = nextChoice with
                    {
                        AutomaticSemanticDrawHookChoice = true
                    }
                };
                advanced = advanced with
                {
                    World = advanced.World! with { Combat = advancedCombat }
                };
            }
        }
        else if (beforeHandDraw && advancedCombat.PendingChoice is not null)
            throw new NotSupportedException(
                $"UNSUPPORTED_SEMANTIC_DECK site={site} " +
                "operation=nested_controlled_choice");
        PrototypeStateInvariants.Validate(advanced);

        var decision = new SemanticDrawHookChoiceDecisionV111(
            pending.Fork(), actionId);
        decision.Validate();
        return (advanced, decision);
    }

    internal static bool HasStandaloneDrawResume(
        PendingCombatChoiceState choice)
    {
        if (choice.Continuation.Any(operation =>
                operation.Kind == PrototypeCombatEffectKind.DrawCards &&
                operation.StandaloneSemanticDrawSegment))
            return true;
        for (var continuation = choice.OuterChoiceContinuation;
             continuation is not null;
             continuation = continuation.Parent)
            if (continuation.Operations.Any(operation =>
                    operation.Kind == PrototypeCombatEffectKind.DrawCards &&
                    operation.StandaloneSemanticDrawSegment))
                return true;
        return false;
    }
}
