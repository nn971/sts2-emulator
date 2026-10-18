namespace Sts2Emulator.Core;

/// <summary>One source-bounded position for a generated draw-pile card.</summary>
public sealed record SemanticDrawInsertionEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, RandomIndexRequestV111 Request,
    string GeneratedCardModelId, long[] EligibleDrawPileInstanceIds,
    string ProbabilityStatus = "UNVERIFIED") : ISemanticChanceEventV111
{
    public const string EventSchemaId =
        "sts2-v111-semantic-draw-insertion-v1";
    public string SiteId => Request.SiteId;
    public string OperationKind => Request.OperationKind;
    public string StreamId => Request.StreamId;
    public string OwnerId => Request.OwnerId;
    public int Ordinal => Request.Ordinal;

    public SemanticDrawInsertionEventV111 Fork() => this with
    {
        Request = Request with
        {
            OrderedCandidates = (int[])Request.OrderedCandidates.Clone()
        },
        EligibleDrawPileInstanceIds =
            (long[])EligibleDrawPileInstanceIds.Clone()
    };

    internal static RandomIndexRequestV111 CreateRequest(
        string siteId, bool nativeStreamAvailable,
        string legacyOwnerId, string nativeOwnerId, int ordinal,
        int supportCount)
    {
        var request = new RandomIndexRequestV111(
            siteId,
            nativeStreamAvailable ? "run.shuffle" : "prototype.combat",
            nativeStreamAvailable ? nativeOwnerId : legacyOwnerId,
            ordinal, [], OperationKind: "bounded_integer",
            BoundedCount: supportCount);
        request.Validate();
        return request;
    }

    internal static SemanticDrawInsertionEventV111 CreateEvent(
        string boundaryFingerprint, RandomIndexRequestV111 request,
        string generatedCardModelId, long[] eligibleDrawPileInstanceIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(generatedCardModelId);
        ArgumentNullException.ThrowIfNull(eligibleDrawPileInstanceIds);
        request.Validate();
        var pile = (long[])eligibleDrawPileInstanceIds.Clone();
        var supportFingerprint = CanonicalJson.Sha256(new
        {
            Request = request,
            GeneratedCardModelId = generatedCardModelId,
            EligibleDrawPileInstanceIds = pile
        });
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId = EventSchemaId,
            BoundaryFingerprint = boundaryFingerprint,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticDrawInsertionEventV111(
            EventSchemaId, eventId, boundaryFingerprint, supportFingerprint,
            request, generatedCardModelId, pile);
        semanticEvent.Validate();
        return semanticEvent;
    }

    public void Validate()
    {
        Request.Validate();
        var standardSite = Request.SiteId ==
            StochasticSiteRegistryV111.DrawPileRandomInsertSiteId;
        var standardOwnerMatchesStream =
            Request.StreamId == "prototype.combat"
                ? Request.OwnerId == "prototype.combat"
                : IsNativePositionOwner(Request.OwnerId);
        var supportedSite = standardSite &&
                standardOwnerMatchesStream ||
            Request.SiteId ==
                "combat.relic.after_shuffle.generated_card.random_insert_index" &&
                Request.OwnerId.StartsWith("relic:", StringComparison.Ordinal) &&
                Request.OwnerId.Contains(":application:",
                    StringComparison.Ordinal) &&
                HasInsertedCardAndPile(Request.OwnerId) ||
            Request.SiteId ==
                PrototypeControlledDrawInsertionV111.PersonalHiveDazedSiteId &&
                Request.OwnerId.StartsWith("enemy:", StringComparison.Ordinal) &&
                Request.OwnerId.Contains(":power:personal_hive:source:",
                    StringComparison.Ordinal) &&
                HasInsertedCardAndPile(Request.OwnerId) &&
                GeneratedCardModelId == "proto.status.dazed";
        if (SchemaId != EventSchemaId ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(GeneratedCardModelId) ||
            !supportedSite ||
            Request.StreamId is not ("prototype.combat" or "run.shuffle") ||
            Request.OperationKind != "bounded_integer" ||
            EligibleDrawPileInstanceIds is null ||
            EligibleDrawPileInstanceIds.Distinct().Count() !=
                EligibleDrawPileInstanceIds.Length ||
            Request.SupportCount != EligibleDrawPileInstanceIds.Length + 1 ||
            ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Invalid semantic draw-insertion event.");
        var expectedSupportFingerprint = CanonicalJson.Sha256(new
        {
            Request, GeneratedCardModelId,
            EligibleDrawPileInstanceIds
        });
        if (SupportFingerprint != expectedSupportFingerprint)
            throw new InvalidDataException(
                "Draw-insertion support fingerprint is inconsistent.");
        var expectedEventId = CanonicalJson.Sha256(new
        {
            SchemaId,
            BoundaryFingerprint,
            SupportFingerprint
        });
        if (EventId != expectedEventId)
            throw new InvalidDataException(
                "Draw-insertion event ID is inconsistent.");
    }

    private static bool IsNativePositionOwner(string ownerId) =>
        (ownerId.StartsWith("card_instance:", StringComparison.Ordinal) ||
         ownerId.StartsWith("effect:", StringComparison.Ordinal)) &&
        HasInsertedCardAndPile(ownerId);

    private static bool HasInsertedCardAndPile(string ownerId) =>
        ownerId.Contains(":card:", StringComparison.Ordinal) &&
        ownerId.Contains(":draw_pile:", StringComparison.Ordinal);
}

/// <summary>
/// SelectedPosition is the source insertion index in CombatState's
/// bottom-first DrawPile array. Provider-owned semantic deck slots are
/// top-first, so the engine converts this position when committing it.
/// </summary>
public sealed record SemanticDrawInsertionDecisionV111(
    SemanticDrawInsertionEventV111 Event, int SelectedPosition)
{
    public SemanticDrawInsertionDecisionV111 Fork() => this with
    {
        Event = Event.Fork()
    };

    public void Validate()
    {
        Event.Validate();
        if (SelectedPosition < 0 ||
            SelectedPosition >= Event.Request.SupportCount)
            throw new InvalidDataException(
                "Draw insertion position is outside its bounded support.");
    }
}

public static class SemanticDrawInsertionChoiceV111
{
    internal static void ValidateBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var insertion = choice?.ControlledDrawInsertion;
        var supportedStage = insertion is
            {
                Stage: "position",
                Operation.Kind:
                    PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions,
                SelectedCardId: not null
            } or
            {
                Stage: "card_model",
                Operation.Kind:
                    PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile,
                SelectedCardId: null
            } or
            {
                Stage: "position",
                Operation.Kind:
                    PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile,
                SelectedCardId: not null
            };
        if (run.Phase != RunPhase.Combat ||
            combat is not { PendingKnowledgeDemonChoice: null } ||
            (combat.AutomaticPipelineContinuation is null &&
             !combat.IsPlayerTurn) ||
            choice is not { ChoiceId: "controlled_draw_insertion",
                OuterChoiceContinuation: not null } ||
            insertion is null || !supportedStage ||
            !PrototypeGameEngine.ValidateControlledDrawInsertion(
                combat, insertion))
            throw new InvalidDataException(
                "Generated draw-pile insertion is not source-reachable.");
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Generated draw-pile insertion lost the provider-owned deck.",
            "Generated draw-pile insertion changed the hidden pile early.");
        PrototypeStateInvariants.Validate(run);
    }

    public static SemanticDrawInsertionDecisionV111 Select(
        SemanticDrawInsertionEventV111 pending,
        string eventId,
        int position)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Draw-insertion event ID is stale or mismatched.");
        var decision = new SemanticDrawInsertionDecisionV111(
            pending.Fork(), position);
        decision.Validate();
        return decision;
    }

    /// <summary>Resume the ordinary insertion continuation under the shared
    /// controlled provider scope using the saved source position.</summary>
    internal static RunState Resume(
        RunState checkpoint,
        SemanticDrawInsertionDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        using (ControlledChanceGuardV111.Enter())
            return PrototypeGameEngine.ResumeControlledDrawInsertion(
                checkpoint.Fork(), decision.SelectedPosition);
    }
}
