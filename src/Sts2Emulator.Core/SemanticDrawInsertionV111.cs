namespace Sts2Emulator.Core;

/// <summary>One source-bounded position for a generated draw-pile card.</summary>
public sealed record SemanticDrawInsertionEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, RandomIndexRequestV111 Request,
    string GeneratedCardModelId, long[] EligibleDrawPileInstanceIds,
    string ProbabilityStatus = "UNVERIFIED") : ISemanticChanceEventV111
{
    private long[]? _eligibleDrawPileInstanceIds =
        CloneOptional(EligibleDrawPileInstanceIds);

    public long[] EligibleDrawPileInstanceIds
    {
        get => CloneOptional(_eligibleDrawPileInstanceIds)!;
        init => _eligibleDrawPileInstanceIds = CloneOptional(value);
    }

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
        EligibleDrawPileInstanceIds = _eligibleDrawPileInstanceIds!
    };

    internal static RandomIndexRequestV111 CreateRequest(
        string siteId, bool nativeStreamAvailable,
        string legacyOwnerId, string nativeOwnerId, int ordinal,
        int supportCount)
    {
        if (!StochasticSiteRegistryV111.ControlledDrawInsertionSiteIds.Contains(
                siteId, StringComparer.Ordinal))
            throw new NotSupportedException(
                $"UNSUPPORTED_RANDOM_SITE site={siteId} " +
                "operation=semantic_draw_insertion.");
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
        var eligibleDrawPileInstanceIds = _eligibleDrawPileInstanceIds;
        var standardSite = Request.SiteId ==
            StochasticSiteRegistryV111.DrawPileRandomInsertSiteId;
        var standardOwnerMatchesStream =
            Request.StreamId == "prototype.combat"
                ? Request.OwnerId == "prototype.combat"
                : IsNativePositionOwner(Request.OwnerId);
        var supportedSite = standardSite &&
                standardOwnerMatchesStream ||
            Request.SiteId ==
                StochasticSiteRegistryV111.AfterShuffleRelicDrawPileInsertSiteId &&
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
            eligibleDrawPileInstanceIds is null ||
            eligibleDrawPileInstanceIds.Distinct().Count() !=
                eligibleDrawPileInstanceIds.Length ||
            Request.SupportCount != eligibleDrawPileInstanceIds.Length + 1 ||
            ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Invalid semantic draw-insertion event.");
        var expectedSupportFingerprint = CanonicalJson.Sha256(new
        {
            Request, GeneratedCardModelId,
            EligibleDrawPileInstanceIds = eligibleDrawPileInstanceIds
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

    private static long[]? CloneOptional(long[]? values) => values is null
        ? null
        : (long[])values.Clone();

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

/// <summary>Native insertion outcome paired with the exact bounded ticket.</summary>
public sealed record SemanticDrawInsertionNativeAuditV111(
    string SchemaId, string EventId,
    SemanticDrawInsertionDecisionV111 Outcome,
    RandomIndexDecisionV111 ProviderDecision)
{
    public const string CurrentSchemaId =
        "sts2-v111-semantic-draw-insertion-native-audit-v1";

    public SemanticDrawInsertionNativeAuditV111 Fork() => this with
    {
        Outcome = Outcome.Fork(),
        ProviderDecision = CloneDecision(ProviderDecision)
    };

    private static RandomIndexDecisionV111 CloneDecision(
        RandomIndexDecisionV111 value) => value with
    {
        Request = value.Request with
        {
            OrderedCandidates = (int[])value.Request.OrderedCandidates.Clone()
        }
    };

    public static SemanticDrawInsertionNativeAuditV111 Create(
        RandomIndexDecisionV111 providerDecision,
        IReadOnlyList<long> eligibleDrawPileInstanceIds,
        string generatedCardModelId)
    {
        ArgumentNullException.ThrowIfNull(providerDecision);
        ArgumentNullException.ThrowIfNull(eligibleDrawPileInstanceIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(generatedCardModelId);
        providerDecision.Validate();
        var request = providerDecision.Request;
        var pile = eligibleDrawPileInstanceIds.ToArray();
        if (providerDecision.ProviderMode != "native-v111" ||
            !StochasticSiteRegistryV111.ControlledDrawInsertionSiteIds
                .Contains(request.SiteId, StringComparer.Ordinal) ||
            pile.Any(instanceId => instanceId <= 0) ||
            pile.Distinct().Count() != pile.Length ||
            request.OperationKind != "bounded_integer" ||
            request.SupportCount != pile.Length + 1 ||
            !HasExactPileOwner(request, generatedCardModelId, pile))
            throw new InvalidDataException(
                "Native draw-insertion audit has an unsupported request or pile support.");

        var boundaryFingerprint = BoundaryFingerprint(
            request, generatedCardModelId, pile);
        var semanticEvent = SemanticDrawInsertionEventV111.CreateEvent(
            boundaryFingerprint, request, generatedCardModelId, pile);
        var outcome = new SemanticDrawInsertionDecisionV111(
            semanticEvent, providerDecision.SelectedIndex);
        outcome.Validate();
        var audit = new SemanticDrawInsertionNativeAuditV111(
            CurrentSchemaId,
            CanonicalJson.Sha256(new
            {
                schemaId = CurrentSchemaId,
                semanticOutcome = outcome,
                providerDecision
            }),
            outcome, CloneDecision(providerDecision));
        audit.Validate();
        return audit;
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Outcome);
        ArgumentNullException.ThrowIfNull(ProviderDecision);
        Outcome.Validate();
        ProviderDecision.Validate();
        var semanticEvent = Outcome.Event;
        var request = semanticEvent.Request;
        if (SchemaId != CurrentSchemaId ||
            ProviderDecision.ProviderMode != "native-v111" ||
            !ControlledStochasticChanceReplayV111.RequestsMatch(
                request, ProviderDecision.Request) ||
            Outcome.SelectedPosition != ProviderDecision.SelectedIndex ||
            !HasExactPileOwner(request,
                semanticEvent.GeneratedCardModelId,
                semanticEvent.EligibleDrawPileInstanceIds) ||
            semanticEvent.BoundaryFingerprint != BoundaryFingerprint(
                request, semanticEvent.GeneratedCardModelId,
                semanticEvent.EligibleDrawPileInstanceIds) ||
            EventId != CanonicalJson.Sha256(new
            {
                schemaId = SchemaId,
                semanticOutcome = Outcome,
                providerDecision = ProviderDecision
            }))
            throw new InvalidDataException(
                "Invalid Native draw-insertion audit outcome.");
    }

    private static string BoundaryFingerprint(
        RandomIndexRequestV111 request, string generatedCardModelId,
        long[] eligibleDrawPileInstanceIds) => CanonicalJson.Sha256(new
        {
            schemaId = CurrentSchemaId,
            request,
            generatedCardModelId,
            eligibleDrawPileInstanceIds
        });

    private static bool HasExactPileOwner(
        RandomIndexRequestV111 request, string generatedCardModelId,
        long[] eligibleDrawPileInstanceIds) =>
        request.OwnerId.EndsWith(
            ":card:" + generatedCardModelId + ":draw_pile:" +
                CanonicalJson.Sha256(eligibleDrawPileInstanceIds),
            StringComparison.Ordinal);
}

/// <summary>One random position in a player pile changed by an enemy effect.</summary>
public sealed record SemanticEnemyPileInsertionEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, RandomIndexRequestV111 Request,
    string GeneratedCardModelId, string PileName,
    long[] ExistingPileInstanceIds,
    string ProbabilityStatus = "UNVERIFIED")
{
    private long[]? _existingPileInstanceIds = ExistingPileInstanceIds is null
        ? null : (long[])ExistingPileInstanceIds.Clone();

    public long[] ExistingPileInstanceIds
    {
        get => _existingPileInstanceIds is null
            ? null! : (long[])_existingPileInstanceIds.Clone();
        init => _existingPileInstanceIds = value is null
            ? null : (long[])value.Clone();
    }

    public const string EventSchemaId =
        "sts2-v111-semantic-enemy-pile-insertion-v1";

    public SemanticEnemyPileInsertionEventV111 Fork() => this with
    {
        Request = Request with
        {
            OrderedCandidates = (int[])Request.OrderedCandidates.Clone()
        },
        ExistingPileInstanceIds = _existingPileInstanceIds!
    };

    internal static SemanticEnemyPileInsertionEventV111 CreateEvent(
        string boundaryFingerprint, RandomIndexRequestV111 request,
        string generatedCardModelId, string pileName,
        long[] existingPileInstanceIds)
    {
        var supportFingerprint = ComputeSupportFingerprint(
            request, generatedCardModelId, pileName, existingPileInstanceIds);
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId = EventSchemaId,
            BoundaryFingerprint = boundaryFingerprint,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticEnemyPileInsertionEventV111(
            EventSchemaId, eventId, boundaryFingerprint, supportFingerprint,
            request, generatedCardModelId, pileName, existingPileInstanceIds);
        semanticEvent.Validate();
        return semanticEvent;
    }

    public void Validate()
    {
        Request.Validate();
        var pile = _existingPileInstanceIds;
        bool discard = Request.SiteId ==
            StochasticSiteRegistryV111.CombatEnemyDiscardRandomInsertSiteId;
        bool draw = Request.SiteId ==
            StochasticSiteRegistryV111.CombatEnemyDrawRandomInsertSiteId;
        var expectedPileName = discard ? "discard" : "draw";
        var expectedSuffix = ":card:" + GeneratedCardModelId + ":" +
            expectedPileName + "_pile:" + CanonicalJson.Sha256(
                pile ?? Array.Empty<long>());
        if (SchemaId != EventSchemaId ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(GeneratedCardModelId) ||
            (!discard && !draw) || PileName != expectedPileName ||
            Request.StreamId != "run.shuffle" ||
            Request.OperationKind != "bounded_integer" ||
            pile is null || pile.Any(instanceId => instanceId <= 0) ||
            pile.Distinct().Count() != pile.Length ||
            Request.SupportCount != pile.Length + 1 ||
            !Request.OwnerId.StartsWith("creature:", StringComparison.Ordinal) ||
            !Request.OwnerId.Contains(":move:", StringComparison.Ordinal) ||
            !Request.OwnerId.Contains(":effect:", StringComparison.Ordinal) ||
            !Request.OwnerId.EndsWith(expectedSuffix, StringComparison.Ordinal) ||
            ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Invalid Native enemy pile-insertion event.");
        if (SupportFingerprint != ComputeSupportFingerprint(
                Request, GeneratedCardModelId, PileName, pile))
            throw new InvalidDataException(
                "Enemy pile-insertion support fingerprint is inconsistent.");
        var expectedEventId = CanonicalJson.Sha256(new
        {
            SchemaId,
            BoundaryFingerprint,
            SupportFingerprint
        });
        if (EventId != expectedEventId)
            throw new InvalidDataException(
                "Enemy pile-insertion event ID is inconsistent.");
    }

    private static string ComputeSupportFingerprint(
        RandomIndexRequestV111 request, string generatedCardModelId,
        string pileName, long[] existingPileInstanceIds) =>
        CanonicalJson.Sha256(new
        {
            request,
            generatedCardModelId,
            pileName,
            existingPileInstanceIds
        });
}

public sealed record SemanticEnemyPileInsertionDecisionV111(
    SemanticEnemyPileInsertionEventV111 Event, int SelectedPosition)
{
    public SemanticEnemyPileInsertionDecisionV111 Fork() => this with
    {
        Event = Event.Fork()
    };

    public void Validate()
    {
        Event.Validate();
        if (SelectedPosition < 0 ||
            SelectedPosition >= Event.Request.SupportCount)
            throw new InvalidDataException(
                "Enemy pile-insertion position is outside its bounded support.");
    }
}

public sealed record SemanticEnemyPileInsertionNativeAuditV111(
    string SchemaId, string EventId,
    SemanticEnemyPileInsertionDecisionV111 Outcome,
    RandomIndexDecisionV111 ProviderDecision)
{
    public const string CurrentSchemaId =
        "sts2-v111-semantic-enemy-pile-insertion-native-audit-v1";

    public SemanticEnemyPileInsertionNativeAuditV111 Fork() => this with
    {
        Outcome = Outcome.Fork(),
        ProviderDecision = ProviderDecision with
        {
            Request = ProviderDecision.Request with
            {
                OrderedCandidates = (int[])ProviderDecision.Request
                    .OrderedCandidates.Clone()
            }
        }
    };

    public static SemanticEnemyPileInsertionNativeAuditV111 Create(
        RandomIndexDecisionV111 providerDecision,
        IReadOnlyList<long> existingPileInstanceIds,
        string generatedCardModelId, string pileName)
    {
        ArgumentNullException.ThrowIfNull(providerDecision);
        ArgumentNullException.ThrowIfNull(existingPileInstanceIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(generatedCardModelId);
        providerDecision.Validate();
        var request = providerDecision.Request;
        var pile = existingPileInstanceIds.ToArray();
        var validSite = (request.SiteId ==
                StochasticSiteRegistryV111.CombatEnemyDiscardRandomInsertSiteId &&
                pileName == "discard") ||
            (request.SiteId ==
                StochasticSiteRegistryV111.CombatEnemyDrawRandomInsertSiteId &&
                pileName == "draw");
        if (providerDecision.ProviderMode != "native-v111" || !validSite ||
            pile.Any(instanceId => instanceId <= 0) ||
            pile.Distinct().Count() != pile.Length ||
            request.SupportCount != pile.Length + 1)
            throw new InvalidDataException(
                "Native enemy pile-insertion audit has an unsupported request or support.");
        var boundaryFingerprint = CanonicalJson.Sha256(new
        {
            schemaId = CurrentSchemaId,
            request,
            generatedCardModelId,
            pileName,
            pileFingerprint = CanonicalJson.Sha256(pile)
        });
        var semanticEvent = SemanticEnemyPileInsertionEventV111.CreateEvent(
            boundaryFingerprint, request, generatedCardModelId, pileName, pile);
        var outcome = new SemanticEnemyPileInsertionDecisionV111(
            semanticEvent, providerDecision.SelectedIndex);
        outcome.Validate();
        var audit = new SemanticEnemyPileInsertionNativeAuditV111(
            CurrentSchemaId,
            CanonicalJson.Sha256(new
            {
                schemaId = CurrentSchemaId,
                semanticOutcome = outcome,
                providerDecision
            }),
            outcome, providerDecision);
        audit.Validate();
        return audit;
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Outcome);
        ArgumentNullException.ThrowIfNull(ProviderDecision);
        Outcome.Validate();
        ProviderDecision.Validate();
        var semanticEvent = Outcome.Event;
        var request = semanticEvent.Request;
        var pile = semanticEvent.ExistingPileInstanceIds;
        var expectedBoundaryFingerprint = CanonicalJson.Sha256(new
        {
            schemaId = CurrentSchemaId,
            request,
            generatedCardModelId = semanticEvent.GeneratedCardModelId,
            pileName = semanticEvent.PileName,
            pileFingerprint = CanonicalJson.Sha256(pile)
        });
        if (SchemaId != CurrentSchemaId ||
            ProviderDecision.ProviderMode != "native-v111" ||
            !ControlledStochasticChanceReplayV111.RequestsMatch(
                request, ProviderDecision.Request) ||
            Outcome.SelectedPosition != ProviderDecision.SelectedIndex ||
            semanticEvent.BoundaryFingerprint != expectedBoundaryFingerprint ||
            EventId != CanonicalJson.Sha256(new
            {
                schemaId = SchemaId,
                semanticOutcome = Outcome,
                providerDecision = ProviderDecision
            }))
            throw new InvalidDataException(
                "Invalid Native enemy pile-insertion audit outcome.");
    }
}

public static class SemanticDrawInsertionChoiceV111
{
    internal static SemanticDrawInsertionEventV111?
        DescribeAutomaticSemanticDrawContinuation(
            RunState run, string boundaryFingerprint)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var insertion = choice?.ControlledDrawInsertion;
        if (combat is null || choice is null || insertion is null ||
            !PrototypeGameEngine
                .IsAutomaticPipelineSemanticDrawInsertionContinuation(
                    combat, choice))
            return null;

        return DescribePendingEvent(
            run, boundaryFingerprint, insertion.RequestOrdinal,
            insertion.RequestOwnerId, insertion.RequestOwnerId,
            appendPileIdentityToLegacyOwner: true,
            appendPileIdentityToNativeOwner: true);
    }

    internal static SemanticDrawInsertionEventV111 DescribePendingEvent(
        RunState run, string boundaryFingerprint, int ordinal,
        string legacyOwnerPrefix, string nativeOwnerPrefix,
        bool appendPileIdentityToLegacyOwner,
        bool appendPileIdentityToNativeOwner)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyOwnerPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeOwnerPrefix);
        var combat = run.World?.Combat;
        var insertion = combat?.PendingChoice?.ControlledDrawInsertion;
        if (insertion is null || insertion.Stage != "position" ||
            insertion.SelectedCardId is not { } cardId)
            throw new InvalidDataException(
                "Draw-insertion request has no selected card position stage.");
        ValidateBoundary(run);
        var pile = (long[])insertion.EligibleDrawPile.Clone();
        var pileSuffix = ":card:" + cardId +
            ":draw_pile:" + CanonicalJson.Sha256(pile);
        var legacyOwnerId = legacyOwnerPrefix +
            (appendPileIdentityToLegacyOwner ? pileSuffix : "");
        var nativeOwnerId = nativeOwnerPrefix +
            (appendPileIdentityToNativeOwner ? pileSuffix : "");
        var request = SemanticDrawInsertionEventV111.CreateRequest(
            insertion.RequestSiteId,
            combat!.Stochastic.NativeRng?.RunStreams is not null,
            legacyOwnerId, nativeOwnerId, ordinal, pile.Length + 1);
        return SemanticDrawInsertionEventV111.CreateEvent(
            boundaryFingerprint, request, cardId, pile);
    }

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

    internal static void ValidateDecision(
        SemanticDrawInsertionEventV111? pending,
        SemanticDrawInsertionDecisionV111 decision, int sourcePosition)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        if (pending is null || sourcePosition != decision.SelectedPosition ||
            CanonicalJson.Sha256(pending) !=
                CanonicalJson.Sha256(decision.Event))
            throw new InvalidOperationException(
                "Draw-insertion decision is stale or outside current support.");
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
        return ControlledChanceGuardV111.ExecuteGuarded(
            () => PrototypeGameEngine.ResumeControlledDrawInsertion(
                checkpoint.Fork(), decision.SelectedPosition),
            allowEngineOwnedPrototypeReward: true);
    }
}
