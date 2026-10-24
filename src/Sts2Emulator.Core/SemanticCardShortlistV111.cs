namespace Sts2Emulator.Core;

/// <summary>One pick in a source random pile shortlist, sampled without
/// replacement and in the order the source offer will present it.</summary>
public sealed record SemanticCardShortlistEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, string SiteId, string StreamId,
    string OwnerId, int Ordinal, string OperationKind,
    long[] EligibleCardInstanceIds, string[] EligibleCardModelIds,
    long[] SelectedCardInstanceIds, long[] CandidateCardInstanceIds,
    string[] CandidateCardModelIds, int RequiredCount,
    string ProbabilityStatus = "UNVERIFIED")
    : ISemanticChanceEventV111
{
    private long[]? _eligibleCardInstanceIds =
        CloneOptional(EligibleCardInstanceIds);
    private string[]? _eligibleCardModelIds =
        CloneOptional(EligibleCardModelIds);
    private long[]? _selectedCardInstanceIds =
        CloneOptional(SelectedCardInstanceIds);
    private long[]? _candidateCardInstanceIds =
        CloneOptional(CandidateCardInstanceIds);
    private string[]? _candidateCardModelIds =
        CloneOptional(CandidateCardModelIds);

    public long[] EligibleCardInstanceIds
    {
        get => CloneOptional(_eligibleCardInstanceIds)!;
        init => _eligibleCardInstanceIds = CloneOptional(value);
    }
    public string[] EligibleCardModelIds
    {
        get => CloneOptional(_eligibleCardModelIds)!;
        init => _eligibleCardModelIds = CloneOptional(value);
    }
    public long[] SelectedCardInstanceIds
    {
        get => CloneOptional(_selectedCardInstanceIds)!;
        init => _selectedCardInstanceIds = CloneOptional(value);
    }
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

    public const string EventSchemaId = SemanticCardShortlistChoiceV111.SchemaId;

    public SemanticCardShortlistEventV111 Fork() => this with
    {
        EligibleCardInstanceIds = _eligibleCardInstanceIds!,
        EligibleCardModelIds = _eligibleCardModelIds!,
        SelectedCardInstanceIds = _selectedCardInstanceIds!,
        CandidateCardInstanceIds = _candidateCardInstanceIds!,
        CandidateCardModelIds = _candidateCardModelIds!
    };

    public void Validate()
    {
        var eligibleCardInstanceIds = _eligibleCardInstanceIds;
        var eligibleCardModelIds = _eligibleCardModelIds;
        var selectedCardInstanceIds = _selectedCardInstanceIds;
        var candidateCardInstanceIds = _candidateCardInstanceIds;
        var candidateCardModelIds = _candidateCardModelIds;
        var remaining = eligibleCardInstanceIds is null ||
                selectedCardInstanceIds is null
            ? null
            : eligibleCardInstanceIds
                .Where(id => !selectedCardInstanceIds.Contains(id)).ToArray();
        var expectedOperationKind =
            SemanticCardShortlistChoiceV111.OperationKindForSite(SiteId);
        if (SchemaId != EventSchemaId ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            expectedOperationKind is null ||
            StreamId is not (SemanticCardShortlistChoiceV111.StreamId or
                SemanticCardShortlistChoiceV111.NativeStreamId) ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            OperationKind != expectedOperationKind ||
            eligibleCardInstanceIds is not { Length: > 1 } ||
            eligibleCardModelIds is null ||
            eligibleCardModelIds.Length != eligibleCardInstanceIds.Length ||
            eligibleCardModelIds.Any(string.IsNullOrWhiteSpace) ||
            selectedCardInstanceIds is null ||
            candidateCardInstanceIds is not { Length: > 0 } ||
            candidateCardModelIds is null ||
            candidateCardModelIds.Length != candidateCardInstanceIds.Length ||
            candidateCardModelIds.Any(string.IsNullOrWhiteSpace) ||
            eligibleCardInstanceIds.Any(id => id <= 0) ||
            eligibleCardInstanceIds.Distinct().Count() !=
                eligibleCardInstanceIds.Length ||
            selectedCardInstanceIds.Distinct().Count() !=
                selectedCardInstanceIds.Length ||
            selectedCardInstanceIds.Any(id =>
                !eligibleCardInstanceIds.Contains(id)) ||
            !candidateCardInstanceIds.SequenceEqual(remaining!) ||
            candidateCardInstanceIds.Any(id => id <= 0) ||
            candidateCardInstanceIds.Distinct().Count() !=
                candidateCardInstanceIds.Length ||
            RequiredCount < 1 ||
            RequiredCount >= eligibleCardInstanceIds.Length ||
            (SiteId == StochasticSiteRegistryV111.CombatRandomReplayCardSelectSiteId &&
             RequiredCount != 1) ||
            selectedCardInstanceIds.Length >= RequiredCount ||
            ProbabilityStatus != "UNVERIFIED" ||
            SupportFingerprint != CanonicalJson.Sha256(new
            {
                eligibleCardInstanceIds,
                eligibleCardModelIds,
                selectedCardInstanceIds,
                candidateCardInstanceIds,
                candidateCardModelIds,
                requiredCount = RequiredCount
            }) ||
            EventId != CanonicalJson.Sha256(new
            {
                SchemaId,
                BoundaryFingerprint,
                SupportFingerprint
            }))
            throw new InvalidDataException(
                "Invalid semantic card-shortlist event.");
    }

    private static long[]? CloneOptional(long[]? values) => values is null
        ? null
        : (long[])values.Clone();

    private static string[]? CloneOptional(string[]? values) => values is null
        ? null
        : (string[])values.Clone();
}

public sealed record SemanticCardShortlistDecisionV111(
    SemanticCardShortlistEventV111 Event, int SelectedIndex,
    long SelectedCardInstanceId, string SelectedCardModelId)
{
    public SemanticCardShortlistDecisionV111 Fork() => this with
    {
        Event = Event.Fork()
    };

    public void Validate()
    {
        Event.Validate();
        if (SelectedIndex < 0 ||
            SelectedIndex >= Event.CandidateCardInstanceIds.Length ||
            Event.CandidateCardInstanceIds[SelectedIndex] !=
                SelectedCardInstanceId ||
            Event.CandidateCardModelIds[SelectedIndex] !=
                SelectedCardModelId)
            throw new InvalidDataException(
                "Card-shortlist decision is outside its ordered support.");
    }
}

public static class SemanticCardShortlistChoiceV111
{
    public const string SchemaId = "sts2-v111-semantic-card-shortlist-v1";
    public const string ShortlistSiteId =
        StochasticSiteRegistryV111.CombatRandomShortlistShuffleSiteId;
    public const string AutoplaySiteId =
        StochasticSiteRegistryV111.CombatAutoplayRandomCardSelectSiteId;
    public const string ReplaySiteId =
        StochasticSiteRegistryV111.CombatRandomReplayCardSelectSiteId;
    internal const string StreamId = "prototype.combat";
    internal const string NativeStreamId = "run.combat_card_selection";
    internal const string ChooseCardInstanceOperation =
        "choose_card_instance";
    internal const string ChooseCardInstanceWithoutReplacementOperation =
        "choose_card_instance_without_replacement";

    internal static string SiteIdForOperation(
        PrototypeCombatEffectKind operationKind) => operationKind switch
    {
        PrototypeCombatEffectKind.AutoPlayRandomCardsFromZone =>
            AutoplaySiteId,
        PrototypeCombatEffectKind.EmpowerRandomDrawCardReplay => ReplaySiteId,
        _ => ShortlistSiteId
    };

    internal static string? OperationKindForSite(string siteId) => siteId switch
    {
        ReplaySiteId => ChooseCardInstanceOperation,
        ShortlistSiteId or AutoplaySiteId =>
            ChooseCardInstanceWithoutReplacementOperation,
        _ => null
    };

    internal static SemanticCardShortlistEventV111? DescribePending(
        RunState run, string boundaryFingerprint, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var shortlist = choice?.ControlledCardShortlist;
        if (shortlist is null) return null;
        ValidateBoundary(run);

        var eligibleIds =
            (long[])shortlist.OriginalCandidateCardInstanceIds.Clone();
        var eligibleModels = eligibleIds.Select(id => combat!.Cards.Single(
            card => card.InstanceId == id).CardId).ToArray();
        var candidateIds =
            (long[])shortlist.RemainingCandidateCardInstanceIds.Clone();
        var candidateModels = candidateIds.Select(id => combat!.Cards.Single(
            card => card.InstanceId == id).CardId).ToArray();
        var selectedIds =
            (long[])shortlist.SelectedCandidateCardInstanceIds.Clone();
        var operationKind = shortlist.Operation.Kind;
        var selection = shortlist.Operation.Selection;
        if (operationKind is not (
                PrototypeCombatEffectKind.AutoPlayRandomCardsFromZone or
                PrototypeCombatEffectKind.EmpowerRandomDrawCardReplay) &&
            selection is null)
            throw new InvalidDataException(
                "Card shortlist lost its source selection specification.");

        var siteId = SiteIdForOperation(operationKind);
        var requestOperationKind =
            OperationKindForSite(siteId) ?? throw new InvalidOperationException(
                "Card shortlist site has no selection operation.");
        var ownerId = operationKind ==
                PrototypeCombatEffectKind.AutoPlayRandomCardsFromZone
            ? "effect:" + operationKind + ":source:" +
              (choice!.SourceCardInstanceId?.ToString() ?? "none") +
              ":zone:" + shortlist.Operation.AutoPlaySourceZone +
              ":pool:" + CanonicalJson.Sha256(eligibleIds)
            : operationKind ==
                PrototypeCombatEffectKind.EmpowerRandomDrawCardReplay
                ? "effect:" + operationKind + ":source:" +
                  (choice!.SourceCardInstanceId?.ToString() ?? "none") +
                  ":pool:" + CanonicalJson.Sha256(eligibleIds)
                : "selection:" + selection!.SourceZone + ":pool:" +
                  CanonicalJson.Sha256(eligibleIds);
        var supportFingerprint = CanonicalJson.Sha256(new
        {
            eligibleCardInstanceIds = eligibleIds,
            eligibleCardModelIds = eligibleModels,
            selectedCardInstanceIds = selectedIds,
            candidateCardInstanceIds = candidateIds,
            candidateCardModelIds = candidateModels,
            requiredCount = shortlist.RequiredCount
        });
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId,
            BoundaryFingerprint = boundaryFingerprint,
            SupportFingerprint = supportFingerprint
        });
        // Keep event identity stable across the Native stream-label correction
        // so saved prototype-labeled decisions remain replayable.
        var streamId = combat!.Stochastic.NativeRng is not null
            ? NativeStreamId : StreamId;
        var semanticEvent = new SemanticCardShortlistEventV111(
            SchemaId, eventId, boundaryFingerprint, supportFingerprint,
            siteId, streamId, ownerId, ordinal, requestOperationKind,
            eligibleIds, eligibleModels, selectedIds, candidateIds,
            candidateModels, shortlist.RequiredCount);
        semanticEvent.Validate();
        return semanticEvent;
    }

    internal static void ValidateBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var shortlist = choice?.ControlledCardShortlist;
        if (run.Phase != RunPhase.Combat ||
            combat is not { PendingKnowledgeDemonChoice: null } ||
            (combat.AutomaticPipelineContinuation is null &&
             !combat.IsPlayerTurn) ||
            choice is not { ChoiceId: "controlled_card_shortlist",
                OuterChoiceContinuation: not null } ||
            shortlist is null ||
            !PrototypeGameEngine.ValidateControlledCardShortlist(
                combat, shortlist))
            throw new InvalidDataException(
                "Random card shortlist is not source-reachable.");
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Card shortlist lost the provider-owned semantic deck.",
            "Card shortlist changed the hidden draw-pile state.");
        PrototypeStateInvariants.Validate(run);
    }

    public static SemanticCardShortlistDecisionV111 Select(
        SemanticCardShortlistEventV111 pending, string eventId,
        long cardInstanceId)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Card-shortlist event ID is stale or mismatched.");
        var index = Array.IndexOf(
            pending.CandidateCardInstanceIds, cardInstanceId);
        if (index < 0)
            throw new InvalidOperationException(
                "Card is outside the pending shortlist support.");
        var decision = new SemanticCardShortlistDecisionV111(
            pending.Fork(), index, cardInstanceId,
            pending.CandidateCardModelIds[index]);
        decision.Validate();
        return decision;
    }

    internal static void ValidateDecision(
        SemanticCardShortlistEventV111? pending,
        SemanticCardShortlistDecisionV111 decision,
        long cardInstanceId)
    {
        ArgumentNullException.ThrowIfNull(decision);
        pending?.Validate();
        decision.Validate();
        bool legacyPrototypeStream = pending is not null &&
            pending.StreamId == NativeStreamId &&
            decision.Event.StreamId == StreamId &&
            CanonicalJson.Sha256(pending with
            {
                StreamId = decision.Event.StreamId
            }) == CanonicalJson.Sha256(decision.Event);
        if (pending is null || pending.EventId != decision.Event.EventId ||
            (CanonicalJson.Sha256(pending) !=
                CanonicalJson.Sha256(decision.Event) &&
             !legacyPrototypeStream) ||
            decision.SelectedCardInstanceId != cardInstanceId)
            throw new InvalidDataException(
                "Card-shortlist decision differs from its live semantic event.");
    }

    /// <summary>Revalidate the live ordered shortlist and resume the saved
    /// selection, replay, or autoplay operation.</summary>
    internal static RunState Resume(
        RunState checkpoint, SemanticCardShortlistDecisionV111 decision)
        => ResumeWithBoundary(checkpoint, decision).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeWithBoundary(
            RunState checkpoint, SemanticCardShortlistDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var combat = checkpoint.World?.Combat ??
            throw new InvalidDataException(
                "Card shortlist lost its source combat.");
        if (decision.Event.StreamId == NativeStreamId &&
            combat.Stochastic.NativeRng is null)
            throw new InvalidDataException(
                "Native card-shortlist stream attribution has no Native combat RNG owner.");
        var choice = combat.PendingChoice;
        var shortlist = choice?.ControlledCardShortlist;
        if (shortlist is null ||
            !PrototypeGameEngine.ValidateControlledCardShortlist(
                combat, shortlist))
            throw new InvalidDataException(
                "Card shortlist is not source-reachable.");
        ValidateBoundary(checkpoint);
        ValidateLiveSupport(combat, shortlist, decision.Event);
        var randomAutoplay = shortlist.Operation.Kind ==
            PrototypeCombatEffectKind.AutoPlayRandomCardsFromZone;

        var resumed = ControlledChanceGuardV111.ExecuteGuarded(
            () => PrototypeGameEngine
                .ResumeControlledCardShortlistWithBoundary(
                    checkpoint.Fork(), decision.SelectedCardInstanceId));
        if (PrototypeGameEngine
                .IsAtCombatRewardOrTerminalBoundary(resumed.Run))
        {
            PrototypeStateInvariants.Validate(resumed.Run);
            return resumed;
        }
        var nextCombat = resumed.Run.World?.Combat;
        if (resumed.Run.Phase != RunPhase.Combat || nextCombat is null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_SHORTLIST " +
                "site=combat.card_selection.random_shortlist " +
                "operation=unsupported_post_shortlist_phase");
        if (nextCombat.PendingChoice?.ControlledCardShortlist is not null)
            ValidateBoundary(resumed.Run);
        else if (nextCombat.PendingChoice?.ChoiceId == "select_cards")
            SemanticCardSelectionEngineV111.ValidatePendingBoundary(
                resumed.Run);
        else
            SemanticPilotCombatEngineV111.ValidateSemanticCombatBoundary(
                resumed.Run);
        if (!randomAutoplay)
            SemanticDrawPileAdapterV111.RequireUnchangedDrawPile(
                combat, nextCombat,
                "UNSUPPORTED_SEMANTIC_DECK " +
                "site=combat.card_selection.random_shortlist " +
                "operation=unmodeled_draw_pile_mutation");
        if (randomAutoplay)
            PrototypeStateInvariants.Validate(resumed.Run);
        return resumed;
    }

    private static void ValidateLiveSupport(
        CombatState combat,
        PrototypeControlledCardShortlistV111 shortlist,
        SemanticCardShortlistEventV111 pending)
    {
        var eligibleIds = shortlist.OriginalCandidateCardInstanceIds;
        var candidateIds = shortlist.RemainingCandidateCardInstanceIds;
        var selectedIds = shortlist.SelectedCandidateCardInstanceIds;
        var eligibleModels = eligibleIds.Select(id => combat.Cards.Single(
            card => card.InstanceId == id).CardId).ToArray();
        var candidateModels = candidateIds.Select(id => combat.Cards.Single(
            card => card.InstanceId == id).CardId).ToArray();
        var siteId = SiteIdForOperation(shortlist.Operation.Kind);
        if (pending.SiteId != siteId ||
            pending.RequiredCount != shortlist.RequiredCount ||
            !eligibleIds.SequenceEqual(pending.EligibleCardInstanceIds) ||
            !eligibleModels.SequenceEqual(pending.EligibleCardModelIds) ||
            !selectedIds.SequenceEqual(pending.SelectedCardInstanceIds) ||
            !candidateIds.SequenceEqual(pending.CandidateCardInstanceIds) ||
            !candidateModels.SequenceEqual(pending.CandidateCardModelIds))
            throw new InvalidDataException(
                "Card-shortlist decision no longer matches its live support.");
    }
}
