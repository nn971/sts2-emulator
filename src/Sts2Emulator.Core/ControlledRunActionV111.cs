using System.Globalization;

namespace Sts2Emulator.Core;

/// <summary>
/// Immutable projection of a suspended controlled run action. It binds the
/// request and ordered labels to the action boundary used by Chance Law and
/// JSONL views.
/// </summary>
public sealed class ControlledRunActionPendingV111
{
    private readonly ControlledChancePendingBoundaryV111 _chanceBoundary;

    public string ActionId { get; }
    public string? MapNodeId { get; }
    public RandomIndexRequestV111 Request => _chanceBoundary.Request;
    public string[] CandidateLabels => _chanceBoundary.CandidateLabels;
    public string BoundaryFingerprint =>
        _chanceBoundary.BoundaryFingerprint;
    public string ChanceEventId => _chanceBoundary.ChanceEventId;

    private ControlledRunActionPendingV111(
        string actionId, string? mapNodeId,
        ControlledChancePendingBoundaryV111 chanceBoundary)
    {
        ActionId = actionId;
        MapNodeId = mapNodeId;
        _chanceBoundary = chanceBoundary ??
            throw new ArgumentNullException(nameof(chanceBoundary));
    }

    public static ControlledRunActionPendingV111 Describe(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var continuation = run.Stochastic.ControlledRunActionChoice ??
            throw new InvalidOperationException(
                "No controlled run-action chance is pending.");
        continuation.Validate();
        var request = continuation.PendingRequest;
        var candidateLabels = continuation.PendingCandidateLabels;
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateLabels);
        var boundary = new
        {
            schemaId = ControlledRunActionEngineV111.SchemaId,
            continuation.ActionId,
            continuation.MapNodeId,
            request,
            candidateLabels
        };
        var boundaryFingerprint = continuation.RunAction is null
            ? CanonicalJson.Sha256(boundary)
            : CanonicalJson.Sha256(new
            {
                boundary,
                actionFingerprint = PrototypeAiEnvironment.StableActionId(
                    continuation.RunAction)
            });
        return new ControlledRunActionPendingV111(
            continuation.ActionId, continuation.MapNodeId,
            ControlledChancePendingBoundaryV111.Create(
                ControlledRunActionEngineV111.SchemaId, request,
                candidateLabels, boundaryFingerprint));
    }
}

/// <summary>
/// Outcome transcript for a supported controlled stochastic run action.
/// Probability status remains unverified until source-law audit.
/// </summary>
public sealed record ControlledRunActionTraceV111(
    string ActionId,
    RandomIndexDecisionV111[] Decisions,
    string[][] CandidateLabels,
    string ProbabilityStatus = "UNVERIFIED",
    string? MapNodeId = null,
    string? ActionFingerprint = null)
{
    public const string IndexTranscriptSchemaId =
        "sts2-v111-controlled-run-action-transcript-v1";
    public const string MixedPrimitiveTranscriptSchemaId =
        "sts2-v111-controlled-run-action-transcript-v2";

    private RandomIndexDecisionV111[]? _decisions =
        CloneDecisions(Decisions);
    private string[][]? _candidateLabels = CloneCandidateLabels(
        CandidateLabels);
    private ControlledStochasticDecisionV111[]? _typedDecisions;

    /// <summary>
    /// Explicit version for new completed run-action transcripts. Null is the
    /// legacy snapshot form, whose index or typed payload determines the
    /// compatible interpretation.
    /// </summary>
    public string? DecisionTranscriptSchemaId { get; init; }

    public RandomIndexDecisionV111[] Decisions
    {
        get => CloneDecisions(_decisions)!;
        init => _decisions = CloneDecisions(value);
    }

    public string[][] CandidateLabels
    {
        get => CloneCandidateLabels(_candidateLabels)!;
        init => _candidateLabels = CloneCandidateLabels(value);
    }

    public ControlledStochasticDecisionV111[]? TypedDecisions
    {
        get => CloneTypedDecisions(_typedDecisions);
        init => _typedDecisions = CloneTypedDecisions(value);
    }

    public ControlledRunActionTraceV111 Fork() => this with
    {
        Decisions = _decisions!,
        CandidateLabels = _candidateLabels!,
        TypedDecisions = _typedDecisions
    };

    public void Validate()
    {
        var typedDecisions = _typedDecisions;
        var mapNodeAction = ActionId == "choose_map_node";
        var shopPurchaseAction =
            ControlledRunActionEngineV111.IsShopPurchaseActionKind(ActionId);
        var rewardRelicAction =
            ControlledRunActionEngineV111
                .IsCombatRewardRelicActionKind(ActionId);
        var relicDeckTransformAction =
            ControlledRunActionEngineV111
                .IsRelicDeckTransformActionKind(ActionId);
        var restHealRewardAction = ActionId ==
            PrototypeNativeRestSiteActionsV111.Heal;
        var entropicBrewAction = ActionId == "use_potion";
        var actionPayloadRequired = shopPurchaseAction || rewardRelicAction
            || relicDeckTransformAction || restHealRewardAction
            || entropicBrewAction;
        if (ActionId is not ("start_run" or "continue_act"
                or PrototypeNativeRestSiteActionsV111.Dig
                or "choose_map_node")
                && !actionPayloadRequired
            || (mapNodeAction
                ? string.IsNullOrWhiteSpace(MapNodeId)
                : MapNodeId is not null)
            || actionPayloadRequired != (ActionFingerprint is not null)
            || actionPayloadRequired && string.IsNullOrWhiteSpace(ActionFingerprint)
            || Decisions is null || CandidateLabels is null
            || entropicBrewAction && typedDecisions is null
            || (typedDecisions is null && Decisions.Length == 0)
            || typedDecisions is { Length: 0 }
            || Decisions.Length != CandidateLabels.Length
            || ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Malformed controlled run-action transcript.");
        if ((DecisionTranscriptSchemaId is not null &&
             DecisionTranscriptSchemaId is not (
                 IndexTranscriptSchemaId or
                 MixedPrimitiveTranscriptSchemaId)) ||
            (DecisionTranscriptSchemaId == IndexTranscriptSchemaId &&
             typedDecisions is not null) ||
            (DecisionTranscriptSchemaId ==
                MixedPrimitiveTranscriptSchemaId &&
             typedDecisions is null))
            throw new InvalidDataException(
                "Controlled run-action transcript schema does not match its payload.");
        if (typedDecisions is not null)
        {
            ValidateTypedTranscript(typedDecisions);
            return;
        }
        for (var index = 0; index < Decisions.Length; index++)
        {
            var decision = Decisions[index];
            var labels = CandidateLabels[index];
            if (labels is null)
                throw new InvalidDataException(
                    "Controlled run-action transcript is missing candidate labels.");
            decision.Validate();
            var request = decision.Request;
            var isOvergrowthBossDraw = request.SiteId ==
                "run.encounter.overgrowth_boss.index_draw";
            var isUnderdocksBossDraw = ActionId == "start_run"
                && ControlledRunActionEngineV111
                    .IsConfiguredNativeUnderdocksBossRequest(
                        ControlledStochasticRequestV111.FromIndex(request));
            var isBossDraw = isOvergrowthBossDraw || isUnderdocksBossDraw;
            var isMapDraw = request.SiteId.StartsWith(
                "map.legacy.", StringComparison.Ordinal);
            var isEventEntryDraw = mapNodeAction
                && ControlledEventChoiceReplayV111
                    .IsRunActionEventEntrySite(request.SiteId);
            var isShopDraw = mapNodeAction
                && ControlledEventChoiceReplayV111
                    .IsRunActionShopSite(request.SiteId);
            var isTreasureDraw = mapNodeAction
                && ControlledEventChoiceReplayV111
                    .IsRunActionTreasureSite(request.SiteId);
            var isCombatDraw = mapNodeAction
                && ControlledEventChoiceReplayV111
                    .IsRunActionCombatSite(request.SiteId);
            var isNativeFormationDraw = mapNodeAction &&
                ControlledRunActionEngineV111.IsNativeEncounterFormationRequest(
                    ControlledStochasticRequestV111.FromIndex(request));
            var isCombatPotionDraw = mapNodeAction
                && ControlledEventChoiceReplayV111
                    .IsRunActionCombatEntryPotionSite(request.SiteId);
            var isShopPurchaseDraw = (shopPurchaseAction || rewardRelicAction)
                && StochasticSiteRegistryV111
                    .IsRunActionShopPurchaseSite(request.SiteId);
            var isRestHealRewardDraw = restHealRewardAction
                && StochasticSiteRegistryV111
                    .IsRunActionRestHealRewardSite(request.SiteId);
            var isRelicDeckTransformDraw = relicDeckTransformAction
                && ControlledRunActionEngineV111
                    .IsSupportedRelicDeckTransformRequest(request);
            var isNativeShovelFallbackDraw = ActionId ==
                    PrototypeNativeRestSiteActionsV111.Dig
                && (request.SiteId is "relic_grab_bag.rarity.ticket"
                    or PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId);
            var prefixStream = (ActionId ==
                    PrototypeNativeRestSiteActionsV111.Dig
                    && !isNativeShovelFallbackDraw)
                || isEventEntryDraw
                ? "event" : isShopDraw ? "shop"
                : isNativeShovelFallbackDraw ? "reward"
                : isTreasureDraw ? "reward"
                : isRelicDeckTransformDraw ? "reward"
                : isCombatPotionDraw ? "event"
                : isNativeFormationDraw ? request.StreamId
                : isCombatDraw ? "combat"
                : isBossDraw ? "combat" : "map";
            var validStream = isShopPurchaseDraw
                ? IsSupportedShopPurchaseStream(request)
                : isRestHealRewardDraw
                    ? request.StreamId == "reward"
                : isNativeFormationDraw
                    ? ControlledRunActionEngineV111
                        .IsNativeEncounterFormationRequest(
                            ControlledStochasticRequestV111.FromIndex(request))
                    : request.StreamId == prefixStream;
            var validSite = ActionId == "start_run"
                ? ControlledEventChoiceReplayV111.IsRunActionSite(
                    request.SiteId)
                : ActionId == "continue_act"
                    ? isMapDraw
                        && ControlledEventChoiceReplayV111.IsRunActionSite(
                            request.SiteId)
                    : mapNodeAction
                    ? isEventEntryDraw || isShopDraw || isTreasureDraw
                        || isCombatDraw || isCombatPotionDraw
                    : actionPayloadRequired
                        ? isShopPurchaseDraw || isRestHealRewardDraw
                            || isRelicDeckTransformDraw
                    : ControlledEventChoiceReplayV111
                        .IsRunActionShovelSite(request.SiteId);
            var prefixOwner = isOvergrowthBossDraw
                ? "run:act:1:boss_pool:" + CanonicalJson.Sha256(labels)
                : isUnderdocksBossDraw
                    ? "act:1:underdocks:boss_pool:" + CanonicalJson.Sha256(
                        PrototypeNativeUnderdocks.NativeBossEncounterIds)
                : ActionId == PrototypeNativeRestSiteActionsV111.Dig
                    && !isNativeShovelFallbackDraw
                    ? "rest:shovel:pool:" + CanonicalJson.Sha256(labels)
                    : request.OwnerId;
            var prefixCandidates = request.OperationKind == "bounded_integer"
                ? Array.Empty<int>()
                : isBossDraw || ActionId ==
                    PrototypeNativeRestSiteActionsV111.Dig
                    ? Enumerable.Range(0, labels.Length).ToArray()
                    : (int[])request.OrderedCandidates.Clone();
            var omittedLabels = (isMapDraw || isEventEntryDraw || isShopDraw
                || isTreasureDraw || isCombatDraw || isCombatPotionDraw)
                && request.SupportCount >
                    ControlledRunActionEngineV111.MaximumInlineOutcomeActions
                && labels.Length == 0;
            omittedLabels |= isRestHealRewardDraw
                && request.SupportCount >
                    ControlledRunActionEngineV111.MaximumInlineOutcomeActions
                && labels.Length == 0;
            var nativeRelicBagLabels = IsLegacyNativeRelicBagLabelSupport(
                request, labels);
            var numericLabels = !(isMapDraw || isEventEntryDraw || isShopDraw
                || isTreasureDraw || isNativeShovelFallbackDraw
                || isCombatDraw || isCombatPotionDraw
                || isShopPurchaseDraw || isRestHealRewardDraw
                || isRelicDeckTransformDraw)
                || nativeRelicBagLabels
                || omittedLabels ||
                ControlledStochasticChanceReplayV111.CandidateLabelsMatch(
                    labels, Enumerable.Range(0, labels.Length).Select(index =>
                        index.ToString(CultureInfo.InvariantCulture)));
            var numericUnderdocksBossLabels = !isUnderdocksBossDraw
                || ControlledStochasticChanceReplayV111.CandidateLabelsMatch(
                    labels, Enumerable.Range(0, labels.Length)
                    .Select(index => index.ToString(
                        CultureInfo.InvariantCulture)));
            if (decision.ProviderMode != "controlled-v111"
                || decision.NativeCursorBefore is not null
                || decision.NativeCursorAfter is not null
                || !validSite
                || !validStream
                || request.OwnerId != prefixOwner
                || !request.OrderedCandidates.SequenceEqual(prefixCandidates)
                || (labels.Length != request.SupportCount && !omittedLabels)
                || !numericLabels
                || !numericUnderdocksBossLabels
                || labels.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException(
                    "Controlled run-action transcript contains an invalid outcome.");
        }
    }

    private void ValidateTypedTranscript(
        ControlledStochasticDecisionV111[] typedDecisions)
    {
        var mapNodeAction = ActionId == "choose_map_node";
        var restHealAction = ActionId ==
            PrototypeNativeRestSiteActionsV111.Heal;
        var entropicBrewAction = ActionId == "use_potion";
        if (!(mapNodeAction || restHealAction || entropicBrewAction) ||
            (mapNodeAction
                ? string.IsNullOrWhiteSpace(MapNodeId) ||
                  ActionFingerprint is not null
                : MapNodeId is not null ||
                  string.IsNullOrWhiteSpace(ActionFingerprint)))
            throw new InvalidDataException(
                "Typed controlled run-action transcript has an unsupported action boundary.");

        foreach (var decision in typedDecisions)
        {
            if (decision is null)
                throw new InvalidDataException(
                    "Typed controlled run-action transcript contains a null decision.");
            decision.Validate();
            if (decision.IndexDecision is { } indexDecision)
            {
                var request = ControlledStochasticRequestV111.FromIndex(
                    indexDecision.Request);
                var nativeEncounter =
                    mapNodeAction && ControlledRunActionEngineV111
                        .IsNativeActOneCombatEntryRequest(request);
                var nativePotionModel = mapNodeAction && request.SiteId ==
                    "potion_factory.model_within_rarity.index_draw" &&
                    request.StreamId == "run.combat_potion_generation";
                var nativeRestModel = restHealAction && request.SiteId ==
                    "card_rarity.encounter_model.index_draw" &&
                    request.StreamId == NativePlayerRngV111.RewardsStreamId &&
                    request.OperationKind == "choose_index" &&
                    request.OrderedCandidates.SequenceEqual(
                        Enumerable.Range(0, request.SupportCount));
                var entropicBrewModel = entropicBrewAction &&
                    ControlledRunActionEngineV111
                        .IsEntropicBrewFillRequestShape(request) &&
                    (request.StreamId ==
                            NativePlayerRngV111.RewardsStreamId
                        ? decision.CandidateLabels is null
                        : request.StreamId == "reward" &&
                          decision.CandidateLabels is { } potionLabels &&
                          potionLabels.SequenceEqual(
                              PrototypeContent.PotionPool,
                              StringComparer.Ordinal));
                if (indexDecision.ProviderMode != "controlled-v111" ||
                    indexDecision.NativeCursorBefore is not null ||
                    indexDecision.NativeCursorAfter is not null ||
                    !(nativeEncounter || nativePotionModel || nativeRestModel ||
                        entropicBrewModel) ||
                    (nativePotionModel || nativeRestModel) &&
                        (decision.CandidateLabels is not null ||
                         request.OperationKind != "choose_index") ||
                    decision.CandidateLabels is { } labels &&
                        labels.Length != request.SupportCount)
                    throw new InvalidDataException(
                        "Typed controlled run-action index decision is outside a supported run-action boundary.");
            }
            else if (decision.UnitFloatDecision is { } floatDecision)
            {
                var request = floatDecision.Request;
                var nativePotionRarity = mapNodeAction &&
                    request.SiteId ==
                        "potion_factory.rarity_roll.unit_float" &&
                    request.StreamId == "run.combat_potion_generation";
                var nativeRestRarity = restHealAction &&
                    request.SiteId == "card_rarity.roll.unit_float" &&
                    request.StreamId == NativePlayerRngV111.RewardsStreamId;
                var nativeRestUpgrade = restHealAction &&
                    request.SiteId ==
                        "card_rarity.upgrade_roll.unit_float" &&
                    request.StreamId == NativePlayerRngV111.RewardsStreamId;
                if (!(nativePotionRarity || nativeRestRarity ||
                        nativeRestUpgrade) ||
                    floatDecision.ProviderMode != "controlled-v111" ||
                    floatDecision.NativeCursorBefore is not null ||
                    floatDecision.NativeCursorAfter is not null ||
                    decision.CandidateLabels is not null)
                    throw new InvalidDataException(
                        "Typed controlled run-action unit-float decision is outside a supported Native run-action boundary.");
            }
            else
            {
                throw new InvalidDataException(
                    "Typed controlled run-action transcript has an unsupported provider primitive.");
            }
        }

        var indexDecisions = typedDecisions.Where(decision =>
                decision.IndexDecision is not null).ToArray();
        var expectedDecisions = indexDecisions.Select(decision =>
            decision.IndexDecision!).ToArray();
        var expectedLabels = indexDecisions.Select(decision =>
            decision.CandidateLabels ?? Array.Empty<string>()).ToArray();
        if (CanonicalJson.Sha256(expectedDecisions) !=
                CanonicalJson.Sha256(Decisions) ||
            CanonicalJson.Sha256(expectedLabels) !=
                CanonicalJson.Sha256(CandidateLabels))
            throw new InvalidDataException(
                "Typed controlled run-action compatibility projection differs from its mixed transcript.");
    }

    internal static bool IsLegacyNativeRelicBagLabelSupport(
        RandomIndexRequestV111 request, string[] labels)
    {
        var rarityTicket = request.SiteId ==
                "relic_grab_bag.rarity.ticket"
            && request.StreamId == "reward"
            && request.OwnerId == PrototypeNativeRelicGrabBag
                .NativeTreasureRarityOwnerId + ":reward"
            && request.OperationKind == "bounded_integer"
            && request.SupportCount == 100
            && request.BoundedCount == 100
            && request.OrderedCandidates.Length == 0
            && ControlledStochasticChanceReplayV111.CandidateLabelsMatch(
                labels,
                PrototypeNativeRelicGrabBag.LegacyRarityTicketLabels());
        var identity = request.SiteId == PrototypeNativeRelicGrabBag
                .NativeTreasureModelSiteId
            && request.StreamId == "reward"
            && request.OperationKind == "choose_index"
            && request.SupportCount == labels.Length
            && labels.Length > 0
            && request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, labels.Length))
            && labels.Distinct(StringComparer.Ordinal).Count() ==
                labels.Length
            && ControlledStochasticChanceReplayV111.CandidateLabelsMatch(
                labels, labels.OrderBy(
                    label => label, StringComparer.Ordinal).ToArray());
        return rarityTicket || identity;
    }

    internal static bool IsSupportedShopPurchaseStream(
        RandomIndexRequestV111 request) => request.SiteId switch
    {
        NativeGloryAncientsV111.BeautifulBraceletShuffleSiteId or
            NativeGloryAncientsV111.DistinguishedCapeCurseSiteId =>
            request.StreamId is "event" or "run.niche",
        "relic.sand_castle.acquisition_upgrade.shuffle_swap_index" or
            "event.potion.fill_empty_slots.model" =>
            request.StreamId == "event",
        "relic.acquisition.random_rare_card.model" =>
            request.StreamId is "reward" or "player.rewards",
        "relic.acquisition.random_potion.model" =>
            request.StreamId is "event" or "player.rewards",
        "run.potion.fill_empty_slots.model" =>
            request.StreamId is "reward" or "player.rewards",
        NativePaelRewardsV111.LegionSkinSiteId =>
            request.StreamId == "reward",
        "reward.card_transform.replacement.index_draw" =>
            request.StreamId == "reward",
        _ => false
    };

    private static RandomIndexDecisionV111[]? CloneDecisions(
        RandomIndexDecisionV111[]? decisions) => decisions?.Select(decision =>
            decision is null ? null! :
                PrototypeControlledEventChoiceContinuationV111
                    .CloneDecision(decision)).ToArray();

    private static string[][]? CloneCandidateLabels(string[][]? labels) =>
        labels?.Select(candidateLabels => candidateLabels is null
            ? null! : (string[])candidateLabels.Clone()).ToArray();

    private static ControlledStochasticDecisionV111[]? CloneTypedDecisions(
        ControlledStochasticDecisionV111[]? decisions) => decisions?
            .Select(decision => decision?.Fork()!).ToArray();
}

/// <summary>
/// Serializable checkpoint for a supported run action replayed from its
/// pre-action state until the next unresolved controlled outcome.
/// </summary>
public sealed record PrototypeControlledRunActionContinuationV111(
    RunState PartialState,
    string ActionId,
    RandomIndexDecisionV111[] Decisions,
    string[][] DecisionCandidateLabels,
    RandomIndexRequestV111 PendingRequest,
    string[] PendingCandidateLabels,
    string? MapNodeId = null,
    GameAction? RunAction = null)
{
    /// <summary>
    /// Atomic mechanics/provider envelope for the suspended run action. The
    /// chance payload and continuation fields remain flattened projections.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SimulationStateV111<
        PrototypeControlledRunActionMechanicsStateV111,
        ControlledChanceContinuationStateV111> Simulation { get; init; } = new(
            new PrototypeControlledRunActionMechanicsStateV111(
                PartialState, ActionId, MapNodeId, RunAction),
            new ControlledChanceContinuationStateV111(
                Decisions, PendingRequest, PendingCandidateLabels,
                DecisionCandidateLabels));

    [System.Text.Json.Serialization.JsonIgnore]
    public PrototypeControlledRunActionMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public ControlledChanceContinuationStateV111 Chance
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public RunState PartialState
    {
        get => Mechanics.PartialState;
        init => Mechanics = Mechanics with { PartialState = value };
    }

    public string ActionId
    {
        get => Mechanics.ActionId;
        init => Mechanics = Mechanics with { ActionId = value };
    }

    public string? MapNodeId
    {
        get => Mechanics.MapNodeId;
        init => Mechanics = Mechanics with { MapNodeId = value };
    }

    public GameAction? RunAction
    {
        get => Mechanics.RunAction;
        init => Mechanics = Mechanics with { RunAction = value };
    }

    public RandomIndexDecisionV111[] Decisions
    {
        get => Chance.Decisions;
        init => Chance = Chance with { Decisions = value };
    }

    public string[][] DecisionCandidateLabels
    {
        get => Chance.DecisionCandidateLabels!;
        init => Chance = Chance with { DecisionCandidateLabels = value };
    }

    public RandomIndexRequestV111 PendingRequest
    {
        get => Chance.PendingRequest;
        init => Chance = Chance with { PendingRequest = value };
    }

    public string[] PendingCandidateLabels
    {
        get => Chance.PendingCandidateLabels!;
        init => Chance = Chance with { PendingCandidateLabels = value };
    }

    public PrototypeControlledRunActionContinuationV111 Fork() => this with
    {
        PartialState = PartialState.Fork(),
        RunAction = RunAction is null ? null : CloneAction(RunAction),
        Chance = Chance.Fork()
    };

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };

    public void Validate()
    {
        this.Mechanics.Continuation.Validate(
            PrototypeMechanicsContinuationKindV111.ControlledRunAction);
        if (PartialState is null || ActionId is null
            || Decisions is null || DecisionCandidateLabels is null
            || DecisionCandidateLabels.Length != Decisions.Length
            || PendingRequest is null || PendingCandidateLabels is null)
            throw new InvalidDataException(
                "Malformed controlled run-action continuation.");

        var state = PartialState;
        if (state.Stochastic.ControlledRunActionChoice is not null
            || state.Stochastic.ControlledEventRewardChoice is not null
            || state.World?.Event?.Stochastic.ControlledChoice is not null)
            throw new InvalidDataException(
                "Controlled run-action frame contains another pending controller.");

        PendingRequest.Validate();
        string expectedSite;
        string expectedStream;
        string expectedOwner;
        int[] expectedCandidates;
        string[] expectedLabels;
        var mapNodeRequest = false;
        var shopPurchaseRequest = false;
        var rewardRelicRequest = false;
        var relicDeckTransformRequest = false;
        var restHealRewardRequest = false;
        switch (ActionId)
        {
            case "start_run":
            {
                var configuredUnderdocksStart =
                    ControlledRunActionEngineV111
                        .IsConfiguredNativeReferenceNatureUnderdocksRunStartSource(
                            state);
                var configuredOvergrowthStart =
                    ControlledRunActionEngineV111
                        .IsConfiguredNativeReferenceNatureOvergrowthRunStartSource(
                            state);
                var configuredNativeStart = configuredUnderdocksStart
                    || configuredOvergrowthStart;
                if (state.Phase != RunPhase.RunStart
                    || state.World is not null
                    || state.Configuration is not null
                        && !configuredNativeStart)
                    throw new InvalidDataException(
                        "Controlled run-start frame is outside a supported run-start boundary.");
                if ((!configuredNativeStart || configuredOvergrowthStart)
                    && PendingRequest.SiteId ==
                    "run.encounter.overgrowth_boss.index_draw")
                {
                    expectedSite = "run.encounter.overgrowth_boss.index_draw";
                    expectedStream = "combat";
                    var bossPool = PrototypeContent.OvergrowthBossEncounterPool;
                    expectedOwner = "run:act:1:boss_pool:"
                        + CanonicalJson.Sha256(bossPool);
                    expectedCandidates = Enumerable.Range(
                        0, bossPool.Length).ToArray();
                    expectedLabels = (string[])bossPool.Clone();
                }
                else if (configuredUnderdocksStart
                    && ControlledRunActionEngineV111
                        .IsConfiguredNativeUnderdocksBossRequest(
                            ControlledStochasticRequestV111.FromIndex(
                                PendingRequest)))
                {
                    var bossPool = PrototypeNativeUnderdocks
                        .NativeBossEncounterIds;
                    expectedSite = PrototypeNativeUnderdocks
                        .BossEncounterIndexSiteId;
                    expectedStream = "combat";
                    expectedOwner = "act:1:underdocks:boss_pool:"
                        + CanonicalJson.Sha256(bossPool);
                    expectedCandidates = Enumerable.Range(
                        0, bossPool.Length).ToArray();
                    expectedLabels = expectedCandidates.Select(index =>
                        index.ToString(CultureInfo.InvariantCulture)).ToArray();
                }
                else if (state.Configuration is null
                    && IsLegacyMapRequest(PendingRequest))
                {
                    (expectedSite, expectedStream, expectedOwner,
                        expectedCandidates, expectedLabels) =
                        ResolveLegacyMapRequest(PendingRequest);
                }
                else
                {
                    throw new InvalidDataException(
                        "Controlled run-start frame has an unsupported chance site.");
                }
                break;
            }
            case "continue_act":
            {
                if (state.Phase != RunPhase.ActTransition
                    || state.Configuration is not null
                    || state.Stochastic.NativeRunRng is not null
                    || state.World is not { } world
                    || world.Act >= PrototypeContent.Rules.Acts
                    || !new PrototypeGameEngine().GetLegalActions(state)
                        .Any(action => action.Kind == "continue_act"))
                    throw new InvalidDataException(
                        "Controlled act-transition frame is outside the legacy prototype map boundary.");
                (expectedSite, expectedStream, expectedOwner,
                    expectedCandidates, expectedLabels) =
                    ResolveLegacyMapRequest(PendingRequest);
                break;
            }
            case PrototypeNativeRestSiteActionsV111.Dig:
            {
                if (state.Phase != RunPhase.Rest
                    || !PrototypeNativeRestSiteRelics.CanDig(state.Player))
                    throw new InvalidDataException(
                        "Controlled Shovel frame is outside the rest-site boundary.");
                var nativeShovelFallback = state.Stochastic.NativeRunRng is null
                    && state.World is { } shovelWorld
                    && PrototypeGameEngine
                        .UsesNativeRegionalSystems(shovelWorld);
                if (nativeShovelFallback)
                {
                    var request = ResolveLegacyNativeRelicBagRequest(
                        state, PendingRequest, Decisions,
                        DecisionCandidateLabels, "Native-regional Shovel");
                    expectedSite = request.SiteId;
                    expectedStream = request.StreamId;
                    expectedOwner = request.OwnerId;
                    expectedCandidates = request.OrderedCandidates;
                    expectedLabels = request.CandidateLabels;
                }
                else
                {
                    expectedSite = StochasticSiteRegistryV111
                        .UnownedRandomRelicSelectionSiteId;
                    expectedStream = "event";
                    expectedLabels = PrototypeContent.RelicPool
                        .Where(id => !state.Player.Relics.Any(relic =>
                            StringComparer.Ordinal.Equals(relic.RelicId, id)))
                        .ToArray();
                    expectedOwner = "rest:shovel:pool:"
                        + CanonicalJson.Sha256(expectedLabels);
                    expectedCandidates = Enumerable.Range(
                        0, expectedLabels.Length).ToArray();
                }
                break;
            }
            case "choose_map_node":
            {
                var nativeFormationDraw =
                    ControlledRunActionEngineV111
                        .IsNativeEncounterFormationRequest(PendingRequest);
                var nativeActOneCombatRequest =
                    ControlledRunActionEngineV111
                        .IsNativeActOneCombatEntryRequest(PendingRequest);
                var nativeMapAction = MapNodeId is { } nativeNodeId
                    ? GameAction.Create("choose_map_node",
                        new ChooseMapNodePayload(nativeNodeId))
                    : null;
                var nativeActOneCombatEntry = nativeMapAction is not null &&
                    ControlledRunActionEngineV111
                        .IsNativeActOneCombatEntryAction(
                            state, nativeMapAction);
                if (state.Phase != RunPhase.MapChoice
                    || state.World is not { } world
                    || state.Stochastic.NativeRunRng is not null &&
                        !(nativeActOneCombatRequest &&
                            nativeActOneCombatEntry)
                    || string.IsNullOrWhiteSpace(MapNodeId))
                    throw new InvalidDataException(
                        "Controlled map-node frame is outside a supported entry boundary.");
                var node = PrototypeNativeWingedBoots.AvailableNodes(
                        world, state.Player)
                    .SingleOrDefault(candidate => candidate.NodeId == MapNodeId);
                var eventEntrySite =
                    ControlledEventChoiceReplayV111.IsRunActionEventEntrySite(
                        PendingRequest.SiteId);
                var shopSite = ControlledEventChoiceReplayV111
                    .IsRunActionShopSite(PendingRequest.SiteId);
                var treasureSite = ControlledEventChoiceReplayV111
                    .IsRunActionTreasureSite(PendingRequest.SiteId);
                var combatSite = ControlledEventChoiceReplayV111
                    .IsRunActionCombatSite(PendingRequest.SiteId);
                var unknownRoomSite = PendingRequest.SiteId ==
                    "event.unknown_room.odds.ticket";
                var combatPotionSite = ControlledEventChoiceReplayV111
                    .IsRunActionCombatEntryPotionSite(
                        PendingRequest.SiteId);
                var overgrowthCombatEntrySite =
                    ControlledEventChoiceReplayV111
                        .IsRunActionOvergrowthCombatEntrySite(
                            PendingRequest.SiteId);
                var underdocksCombatEntrySite =
                    ControlledEventChoiceReplayV111
                        .IsRunActionUnderdocksCombatEntrySite(
                            PendingRequest.SiteId);
                var legacyUnknownRoomNode = node?.RoomType ==
                        PrototypeRoomType.Unknown
                    && state.Configuration is null
                    && state.Stochastic.NativeRunRng is null
                    && PrototypeGameEngine.UsesNativeRegionalSystems(world);
                var legacyShopNode = node?.RoomType == PrototypeRoomType.Shop
                    && !PrototypeGameEngine.UsesNativeRegionalSystems(world);
                var legacyNativeTreasureNode = node?.RoomType ==
                        PrototypeRoomType.Treasure
                    && state.Stochastic.NativeRunRng is null
                    && PrototypeGameEngine.UsesNativeRegionalSystems(world)
                    && !PrototypeGameEngine.WillSkipNextTreasure(state.Player);
                var availableRelics = node?.RoomType == PrototypeRoomType.Treasure
                    ? PrototypeContent.RelicPool
                        .Where(id => !state.Player.Relics.Any(relic =>
                            StringComparer.Ordinal.Equals(relic.RelicId, id)))
                        .ToArray()
                    : Array.Empty<string>();
                var legacyTreasureNode =
                    node?.RoomType == PrototypeRoomType.Treasure
                    && !PrototypeGameEngine.UsesNativeRegionalSystems(world)
                    && !PrototypeGameEngine.WillSkipNextTreasure(state.Player)
                    && availableRelics.Length > 0;
                var supportedTreasureNode = legacyTreasureNode ||
                    legacyNativeTreasureNode;
                var legacyOvergrowthCombatNode = node is not null
                    && ControlledRunActionEngineV111
                        .IsLegacyOvergrowthCombatRoom(state, node.RoomType);
                var legacyUnderdocksCombatNode = node is not null
                    && ControlledRunActionEngineV111
                        .IsLegacyUnderdocksCombatRoom(state, node.RoomType);
                var nativeActOneCombatNode = node is not null &&
                    nativeActOneCombatEntry &&
                    node.RoomType is PrototypeRoomType.Combat or
                        PrototypeRoomType.Elite or PrototypeRoomType.Boss;
                if (node is null
                    || (node.RoomType != PrototypeRoomType.Event
                        && !legacyShopNode && !supportedTreasureNode
                        && !legacyUnknownRoomNode
                        && !legacyOvergrowthCombatNode
                        && !legacyUnderdocksCombatNode
                        && !nativeActOneCombatNode))
                    throw new InvalidDataException(
                        "Controlled map-node frame does not reference an available supported node.");
                if (!(node.RoomType == PrototypeRoomType.Event && eventEntrySite)
                    && !(legacyShopNode && shopSite)
                    && !(supportedTreasureNode && treasureSite)
                    && !(legacyUnknownRoomNode && unknownRoomSite)
                    && !(legacyOvergrowthCombatNode
                        && overgrowthCombatEntrySite)
                    && !(legacyUnderdocksCombatNode
                        && underdocksCombatEntrySite)
                    && !(nativeActOneCombatNode
                        && nativeActOneCombatRequest
                        && nativeMapAction is not null
                        && ControlledRunActionEngineV111
                            .IsNativeActOneCombatEntrySite(state,
                                nativeMapAction,
                                PendingRequest.SiteId)))
                    throw new InvalidDataException(
                        "Controlled map-node frame has an unsupported chance site.");
                expectedSite = PendingRequest.SiteId;
                expectedStream = nativeFormationDraw ? PendingRequest.StreamId
                    : combatSite ? "combat"
                    : combatPotionSite ? "event"
                    : shopSite ? "shop"
                    : treasureSite ? "reward" : "event";
                expectedOwner = PendingRequest.OwnerId;
                expectedCandidates = PendingRequest.OperationKind ==
                        "bounded_integer"
                    ? []
                    : (int[])PendingRequest.OrderedCandidates.Clone();
                if (unknownRoomSite)
                {
                    var oddsOwner =
                        $"act:{world.Act}:floor:{world.Floor}:unknown_room";
                    if (!legacyUnknownRoomNode ||
                        PendingRequest.OperationKind != "bounded_integer" ||
                        PendingRequest.SupportCount !=
                            PrototypeUnknownRoomOddsState.Scale ||
                        PendingRequest.OwnerId != oddsOwner)
                        throw new InvalidDataException(
                            "Controlled Unknown-room request differs from its regional map boundary.");
                    expectedOwner = oddsOwner;
                }
                expectedLabels = PendingRequest.SupportCount >
                    ControlledRunActionEngineV111.MaximumInlineOutcomeActions
                    ? []
                    : Enumerable.Range(0, PendingRequest.SupportCount)
                        .Select(index => index.ToString(
                            CultureInfo.InvariantCulture)).ToArray();
                if (treasureSite && legacyNativeTreasureNode)
                {
                    var request = ResolveLegacyNativeRelicBagRequest(
                        state, PendingRequest, Decisions,
                        DecisionCandidateLabels,
                        "Native-regional Treasure");
                    expectedSite = request.SiteId;
                    expectedStream = request.StreamId;
                    expectedOwner = request.OwnerId;
                    expectedCandidates = request.OrderedCandidates;
                    expectedLabels = request.CandidateLabels;
                }
                else if (treasureSite)
                {
                    expectedSite = "treasure.relic_model.index_draw";
                    expectedStream = "reward";
                    expectedOwner = $"act:{world.Act}:floor:{world.Floor}:pool:"
                        + CanonicalJson.Sha256(availableRelics);
                    expectedCandidates = Enumerable.Range(
                        0, availableRelics.Length).ToArray();
                    expectedLabels = availableRelics.Length >
                        ControlledRunActionEngineV111.MaximumInlineOutcomeActions
                        ? []
                        : Enumerable.Range(0, availableRelics.Length)
                            .Select(index => index.ToString(
                                CultureInfo.InvariantCulture)).ToArray();
                }
                mapNodeRequest = true;
                break;
            }
            case "buy_card":
            case "buy_potion":
            case "replace_shop_potion":
            case "remove_card":
            case "buy_relic":
            {
                if (state.Phase != RunPhase.Shop
                    || RunAction is null
                    || RunAction.Kind != ActionId
                    || MapNodeId is not null
                    || !StochasticSiteRegistryV111
                        .IsRunActionShopPurchaseSite(PendingRequest.SiteId)
                    || !ControlledRunActionEngineV111
                        .IsSupportedShopPurchaseStream(PendingRequest)
                    || !new PrototypeGameEngine().GetLegalActions(state)
                        .Any(action => PrototypeAiEnvironment.StableActionId(
                                action) ==
                            PrototypeAiEnvironment.StableActionId(RunAction!)))
                    throw new InvalidDataException(
                        "Controlled Shop-purchase frame is outside its legal action boundary.");
                expectedSite = PendingRequest.SiteId;
                expectedStream = PendingRequest.StreamId;
                expectedOwner = PendingRequest.OwnerId;
                expectedCandidates = (int[])PendingRequest.OrderedCandidates
                    .Clone();
                expectedLabels = (string[])PendingCandidateLabels.Clone();
                shopPurchaseRequest = true;
                break;
            }
            case "take_reward_relic":
            case "take_reward_extra_relic":
            {
                if (RunAction is null
                    || !ControlledRunActionEngineV111
                        .IsCombatRewardRelicAction(state, RunAction)
                    || RunAction.Kind != ActionId
                    || MapNodeId is not null
                    || !StochasticSiteRegistryV111
                        .IsRunActionShopPurchaseSite(PendingRequest.SiteId)
                    || !ControlledRunActionEngineV111
                        .IsSupportedShopPurchaseStream(PendingRequest)
                    || !new PrototypeGameEngine().GetLegalActions(state)
                        .Any(action => PrototypeAiEnvironment.StableActionId(
                                action) ==
                            PrototypeAiEnvironment.StableActionId(RunAction)))
                    throw new InvalidDataException(
                        "Controlled reward-relic frame is outside its legal combat reward boundary.");
                expectedSite = PendingRequest.SiteId;
                expectedStream = PendingRequest.StreamId;
                expectedOwner = PendingRequest.OwnerId;
                expectedCandidates = (int[])PendingRequest.OrderedCandidates
                    .Clone();
                expectedLabels = (string[])PendingCandidateLabels.Clone();
                rewardRelicRequest = true;
                break;
            }
            case "choose_shop_relic_deck_card":
            case "choose_relic_deck_card":
            {
                if (RunAction is null
                    || RunAction.Kind != ActionId
                    || MapNodeId is not null
                    || !ControlledRunActionEngineV111
                        .IsRandomRelicDeckTransformAction(state, RunAction)
                    || !new PrototypeGameEngine().GetLegalActions(state)
                        .Any(action => PrototypeAiEnvironment.StableActionId(
                                action) ==
                            PrototypeAiEnvironment.StableActionId(RunAction)))
                    throw new InvalidDataException(
                        "Controlled relic deck-transform frame is outside its legal pending-choice boundary.");
                var payload = RunAction.ReadPayload<ChooseDeckCardPayload>();
                var pending = ActionId == "choose_shop_relic_deck_card"
                    ? state.World?.Shop?.PendingDeckChoice
                    : state.World?.Reward?.PendingDeckChoice;
                var selected = state.Player.Deck.Single(card =>
                    card.InstanceId == payload.CardInstanceId);
                var pool = PrototypeContent.RewardCardPool
                    .Where(cardId => !StringComparer.Ordinal.Equals(
                        cardId, selected.CardId))
                    .ToArray();
                if (pending is null || pool.Length == 0)
                    throw new InvalidDataException(
                        "Controlled relic deck-transform frame has no replacement support.");
                expectedSite = "reward.card_transform.replacement.index_draw";
                expectedStream = "reward";
                expectedOwner = "card_instance:" + payload.CardInstanceId
                    + ":original:" + selected.CardId + ":pool:"
                    + CanonicalJson.Sha256(pool);
                expectedCandidates = Enumerable.Range(0, pool.Length).ToArray();
                expectedLabels = pool;
                relicDeckTransformRequest = true;
                break;
            }
            case PrototypeNativeRestSiteActionsV111.Heal:
            {
                if (RunAction is null
                    || !ControlledRunActionEngineV111
                        .IsLegacyRestHealRewardAction(state, RunAction)
                    || RunAction.Kind != ActionId
                    || MapNodeId is not null
                    || !ControlledRunActionEngineV111
                        .IsSupportedRestHealRewardRequest(PendingRequest)
                    || !new PrototypeGameEngine().GetLegalActions(state)
                        .Any(action => PrototypeAiEnvironment.StableActionId(
                                action) ==
                            PrototypeAiEnvironment.StableActionId(RunAction)))
                    throw new InvalidDataException(
                        "Controlled rest-reward frame is outside its legal rest-heal boundary.");
                expectedSite = PendingRequest.SiteId;
                expectedStream = PendingRequest.StreamId;
                expectedOwner = PendingRequest.OwnerId;
                expectedCandidates = (int[])PendingRequest.OrderedCandidates
                    .Clone();
                expectedLabels = (string[])PendingCandidateLabels.Clone();
                restHealRewardRequest = true;
                break;
            }
            default:
                throw new InvalidDataException(
                    "Controlled run-action frame uses an unsupported action.");
        }
        if ((!mapNodeRequest && MapNodeId is not null)
            || (!(shopPurchaseRequest || rewardRelicRequest ||
                    relicDeckTransformRequest || restHealRewardRequest)
                && RunAction is not null)
            || PendingRequest.SiteId != expectedSite
            || PendingRequest.StreamId != expectedStream
            || PendingRequest.OwnerId != expectedOwner
            || !PendingRequest.OrderedCandidates.SequenceEqual(expectedCandidates)
            || PendingCandidateLabels.Length != expectedLabels.Length
            || !ControlledStochasticChanceReplayV111.CandidateLabelsMatch(
                PendingCandidateLabels, expectedLabels))
            throw new InvalidDataException(
                "Controlled run-action candidates do not match their source pool.");

        if (shopPurchaseRequest)
            ControlledRunActionEngineV111.ValidatePendingShopPurchase(
                state, RunAction!, Decisions, DecisionCandidateLabels,
                PendingRequest,
                PendingCandidateLabels);
        if (rewardRelicRequest)
            ControlledRunActionEngineV111
                .ValidatePendingRewardRelicAcquisition(
                    state, RunAction!, Decisions, DecisionCandidateLabels,
                    PendingRequest,
                    PendingCandidateLabels);
        if (relicDeckTransformRequest)
            ControlledRunActionEngineV111
                .ValidatePendingRelicDeckTransform(
                    state, RunAction!, Decisions, DecisionCandidateLabels,
                    PendingRequest, PendingCandidateLabels);
        if (restHealRewardRequest)
            ControlledRunActionEngineV111.ValidatePendingRestHealReward(
                state, RunAction!, Decisions, DecisionCandidateLabels,
                PendingRequest,
                PendingCandidateLabels);

        for (var index = 0; index < Decisions.Length; index++)
        {
            var decision = Decisions[index];
            var labels = DecisionCandidateLabels[index];
            if (labels is null)
                throw new InvalidDataException(
                    "Controlled run-action decision prefix is missing candidate labels.");
            decision.Validate();
            var request = decision.Request;
            var isOvergrowthBossDraw = request.SiteId ==
                "run.encounter.overgrowth_boss.index_draw";
            var isUnderdocksBossDraw = ActionId == "start_run"
                && ControlledRunActionEngineV111
                    .IsConfiguredNativeUnderdocksBossRequest(
                        ControlledStochasticRequestV111.FromIndex(request));
            var isBossDraw = isOvergrowthBossDraw || isUnderdocksBossDraw;
            var isMapDraw = request.SiteId.StartsWith(
                "map.legacy.", StringComparison.Ordinal);
            var isEventEntryDraw = ActionId == "choose_map_node"
                && ControlledEventChoiceReplayV111
                    .IsRunActionEventEntrySite(request.SiteId);
            var isShopDraw = ActionId == "choose_map_node"
                && ControlledEventChoiceReplayV111
                    .IsRunActionShopSite(request.SiteId);
            var isTreasureDraw = ActionId == "choose_map_node"
                && ControlledEventChoiceReplayV111
                    .IsRunActionTreasureSite(request.SiteId);
            var isNativeShovelFallbackDraw = ActionId ==
                    PrototypeNativeRestSiteActionsV111.Dig
                && (request.SiteId is "relic_grab_bag.rarity.ticket"
                    or PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId);
            var isCombatDraw = ActionId == "choose_map_node"
                && ControlledEventChoiceReplayV111
                    .IsRunActionCombatSite(request.SiteId);
            var isCombatPotionDraw = ActionId == "choose_map_node"
                && ControlledEventChoiceReplayV111
                    .IsRunActionCombatEntryPotionSite(request.SiteId);
            var isRelicDeckTransformDraw =
                ControlledRunActionEngineV111
                    .IsRelicDeckTransformActionKind(ActionId)
                && ControlledRunActionEngineV111
                    .IsSupportedRelicDeckTransformRequest(request);
            var mapAction = ActionId == "choose_map_node"
                ? GameAction.Create(
                    "choose_map_node",
                    new ChooseMapNodePayload(MapNodeId ??
                        throw new InvalidDataException(
                            "Controlled map-node prefix is missing its node ID.")))
                : null;
            var isCombatEntryDraw = mapAction is not null
                && ControlledRunActionEngineV111
                    .IsLegacyActOneCombatEntrySite(
                        state, mapAction, request.SiteId);
            var isNativeFormationDraw = mapAction is not null &&
                ControlledRunActionEngineV111
                    .IsNativeActOneCombatEntrySite(
                        state, mapAction, request.SiteId) &&
                ControlledRunActionEngineV111
                    .IsNativeEncounterFormationRequest(
                        ControlledStochasticRequestV111.FromIndex(request));
            var isNativeActOneCombatEntryDraw = mapAction is not null &&
                ControlledRunActionEngineV111
                    .IsNativeActOneCombatEntrySite(
                        state, mapAction, request.SiteId) &&
                ControlledRunActionEngineV111
                    .IsNativeActOneCombatEntryRequest(
                        ControlledStochasticRequestV111.FromIndex(request));
            var prefixStream = (ActionId ==
                    PrototypeNativeRestSiteActionsV111.Dig
                    && !isNativeShovelFallbackDraw)
                || isEventEntryDraw
                ? "event" : isShopDraw ? "shop"
                : isNativeShovelFallbackDraw ? "reward"
                : isTreasureDraw ? "reward"
                : isRelicDeckTransformDraw ? "reward"
                : isCombatPotionDraw ? "event"
                : isNativeFormationDraw ? request.StreamId
                : isCombatDraw ? "combat"
                : isBossDraw ? "combat" : "map";
            var validSite = ActionId == "start_run"
                ? ControlledEventChoiceReplayV111.IsRunActionSite(
                    request.SiteId)
                : ActionId == "continue_act"
                    ? isMapDraw
                        && ControlledEventChoiceReplayV111.IsRunActionSite(
                            request.SiteId)
                    : ActionId == "choose_map_node"
                    ? isEventEntryDraw || isShopDraw || isTreasureDraw
                        || isCombatEntryDraw
                        || isNativeActOneCombatEntryDraw
                    : IsRelicDeckTransformActionKind(ActionId)
                        ? isRelicDeckTransformDraw
                    : ControlledEventChoiceReplayV111
                        .IsRunActionShovelSite(request.SiteId);
            var prefixOwner = isOvergrowthBossDraw
                ? "run:act:1:boss_pool:" + CanonicalJson.Sha256(labels)
                : isUnderdocksBossDraw
                    ? "act:1:underdocks:boss_pool:" + CanonicalJson.Sha256(
                        PrototypeNativeUnderdocks.NativeBossEncounterIds)
                : ActionId == PrototypeNativeRestSiteActionsV111.Dig
                    && !isNativeShovelFallbackDraw
                    ? "rest:shovel:pool:" + CanonicalJson.Sha256(labels)
                    : request.OwnerId;
            var prefixCandidates = request.OperationKind == "bounded_integer"
                ? Array.Empty<int>()
                : isBossDraw || ActionId ==
                    PrototypeNativeRestSiteActionsV111.Dig
                    ? Enumerable.Range(0, labels.Length).ToArray()
                    : (int[])request.OrderedCandidates.Clone();
            var omittedLabels = (isMapDraw || isEventEntryDraw || isShopDraw
                || isTreasureDraw || isCombatDraw || isCombatPotionDraw)
                && request.SupportCount >
                    ControlledRunActionEngineV111.MaximumInlineOutcomeActions
                && labels.Length == 0;
            var nativeRelicBagLabels =
                ControlledRunActionTraceV111
                    .IsLegacyNativeRelicBagLabelSupport(request, labels);
            var numericLabels = !(isMapDraw || isEventEntryDraw || isShopDraw
                || isTreasureDraw || isNativeShovelFallbackDraw
                || isCombatDraw || isCombatPotionDraw
                || isRelicDeckTransformDraw)
                || nativeRelicBagLabels
                || omittedLabels ||
                ControlledStochasticChanceReplayV111.CandidateLabelsMatch(
                    labels, Enumerable.Range(0, labels.Length).Select(index =>
                        index.ToString(CultureInfo.InvariantCulture)));
            var numericUnderdocksBossLabels = !isUnderdocksBossDraw
                || ControlledStochasticChanceReplayV111.CandidateLabelsMatch(
                    labels, Enumerable.Range(0, labels.Length)
                        .Select(index => index.ToString(
                            CultureInfo.InvariantCulture)));
            if (decision.ProviderMode != "controlled-v111"
                || decision.NativeCursorBefore is not null
                || decision.NativeCursorAfter is not null
                || !validSite
                || request.StreamId != prefixStream
                || request.OwnerId != prefixOwner
                || !request.OrderedCandidates.SequenceEqual(prefixCandidates)
                || (request.SupportCount != labels.Length && !omittedLabels)
                || !numericLabels
                || !numericUnderdocksBossLabels
                || labels.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException(
                    "Controlled run-action decision prefix is malformed.");
        }

        PrototypeStateInvariants.Validate(state);
    }

    private static PrototypeRelicRarity RequireLegacyNativeRelicBagRarity(
        RandomIndexDecisionV111[] decisions, string[][] decisionLabels)
    {
        var expectedLabels = PrototypeNativeRelicGrabBag
            .LegacyRarityTicketLabels();
        if (decisions.Length != 1 || decisionLabels.Length != 1 ||
            !decisionLabels[0].SequenceEqual(expectedLabels,
                StringComparer.Ordinal))
            throw new InvalidDataException(
                "Legacy Native relic-bag identity request lacks its rarity ticket prefix.");
        var rarityDecision = decisions[0];
        rarityDecision.Validate();
        var request = rarityDecision.Request;
        if (request.SiteId != "relic_grab_bag.rarity.ticket" ||
            request.StreamId != "reward" ||
            request.OwnerId != PrototypeNativeRelicGrabBag
                .NativeTreasureRarityOwnerId + ":reward" ||
            request.OperationKind != "bounded_integer" ||
            request.SupportCount != expectedLabels.Length ||
            request.BoundedCount != expectedLabels.Length ||
            request.OrderedCandidates.Length != 0 ||
            rarityDecision.SelectedIndex < 0 ||
            rarityDecision.SelectedIndex >= expectedLabels.Length)
            throw new InvalidDataException(
                "Legacy Native relic-bag rarity ticket differs from its source request.");
        return rarityDecision.SelectedIndex <
                PrototypeNativeRelicGrabBag.CommonRarityWeight / 100
            ? PrototypeRelicRarity.Common
            : rarityDecision.SelectedIndex <
                (PrototypeNativeRelicGrabBag.CommonRarityWeight +
                 PrototypeNativeRelicGrabBag.UncommonRarityWeight) / 100
                ? PrototypeRelicRarity.Uncommon
                : PrototypeRelicRarity.Rare;
    }

    private static (string SiteId, string StreamId, string OwnerId,
        int[] OrderedCandidates, string[] CandidateLabels)
        ResolveLegacyNativeRelicBagRequest(
            RunState state, RandomIndexRequestV111 request,
            RandomIndexDecisionV111[] decisions,
            string[][] decisionCandidateLabels, string boundary)
    {
        var expectedLabels = PrototypeNativeRelicGrabBag
            .LegacyRarityTicketLabels();
        ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
            state, $"{boundary} replay");
        if (request.SiteId == "relic_grab_bag.rarity.ticket")
        {
            if (request.StreamId != "reward" ||
                request.OwnerId != PrototypeNativeRelicGrabBag
                    .NativeTreasureRarityOwnerId + ":reward" ||
                request.OperationKind != "bounded_integer" ||
                request.SupportCount != expectedLabels.Length ||
                request.BoundedCount != expectedLabels.Length ||
                request.OrderedCandidates.Length != 0 ||
                decisions.Length != 0)
                throw new InvalidDataException(
                    $"{boundary} rarity request differs from its source boundary.");
            return (request.SiteId, "reward",
                PrototypeNativeRelicGrabBag.NativeTreasureRarityOwnerId
                    + ":reward", [], expectedLabels);
        }

        if (request.SiteId != PrototypeNativeRelicGrabBag
                .NativeTreasureModelSiteId ||
            request.StreamId != "reward" ||
            request.OperationKind != "choose_index")
            throw new InvalidDataException(
                $"{boundary} reached an unregistered relic-bag fallback request.");

        var rarity = RequireLegacyNativeRelicBagRarity(
            decisions, decisionCandidateLabels);
        var world = state.World ?? throw new InvalidDataException(
            $"{boundary} lost its run world.");
        var bag = world.Stochastic.RelicBag ??
            PrototypeNativeRelicGrabBag.CreateNatureUnordered(state.Player);
        var plan = PrototypeNativeRelicGrabBag.PlanNaturePull(
            bag, rarity, merchant: false, state.Player);
        var candidates = plan.CandidateIds;
        if (candidates.Length == 0)
            throw new InvalidDataException(
                $"{boundary} relic request has no eligible source support.");
        return (request.SiteId, "reward",
            PrototypeNativeRelicGrabBag.TreasureModelOwnerId(
                world, rarity, candidates),
            Enumerable.Range(0, candidates.Length).ToArray(), candidates);
    }

    private static bool IsLegacyMapRequest(RandomIndexRequestV111 request) =>
        ControlledEventChoiceReplayV111.IsRunActionSite(request.SiteId)
        && request.SiteId.StartsWith("map.legacy.", StringComparison.Ordinal);

    private static (string SiteId, string StreamId, string OwnerId,
        int[] OrderedCandidates, string[] CandidateLabels)
        ResolveLegacyMapRequest(RandomIndexRequestV111 request)
    {
        if (!IsLegacyMapRequest(request) || request.StreamId != "map")
            throw new InvalidDataException(
                "Controlled legacy map request is outside its registered map stream.");
        var labels = request.SupportCount >
            ControlledRunActionEngineV111.MaximumInlineOutcomeActions
            ? []
            : Enumerable.Range(0, request.SupportCount).Select(index =>
                index.ToString(CultureInfo.InvariantCulture)).ToArray();
        var candidates = request.OperationKind == "bounded_integer"
            ? []
            : (int[])request.OrderedCandidates.Clone();
        return (request.SiteId, "map", request.OwnerId, candidates, labels);
    }
}

/// <summary>
/// Opt-in controlled boundary for supported stochastic run actions. Ordinary
/// player steps retain their existing provider behavior.
/// </summary>
public static class ControlledRunActionEngineV111
{
    public const string SchemaId = "sts2-v111-controlled-run-action-v1";
    public const int MaximumInlineOutcomeActions = 4096;

    public static RunState BeginLegacyRunStart(RunState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Phase != RunPhase.RunStart
            || source.Configuration is not null)
            throw new InvalidOperationException(
                "Controlled run start requires an unsuspended legacy run-start state.");
        return Begin(source, GameAction.Empty("start_run"));
    }

    public static RunState BeginLegacyActTransition(RunState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Phase != RunPhase.ActTransition
            || source.Stochastic.ControlledRunActionChoice is not null
            || source.Configuration is not null
            || source.Stochastic.NativeRunRng is not null
            || source.World is not { } world
            || world.Act >= PrototypeContent.Rules.Acts)
            throw new InvalidOperationException(
                "Controlled act transition requires an unsuspended legacy prototype transition before the final act.");
        return Begin(source, GameAction.Empty("continue_act"));
    }

    public static RunState BeginShovelDig(RunState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Phase != RunPhase.Rest
            || source.Stochastic.ControlledRunActionChoice is not null
            || !PrototypeNativeRestSiteRelics.CanDig(source.Player))
            throw new InvalidOperationException(
                "Controlled Shovel dig requires an unsuspended rest-site action.");

        var action = new PrototypeGameEngine().GetLegalActions(source)
            .SingleOrDefault(candidate =>
                candidate.Kind == PrototypeNativeRestSiteActionsV111.Dig)
            ?? throw new InvalidOperationException(
                "Shovel dig is not legal in the current state.");
        return Begin(source, action);
    }

    public static bool IsShopPurchaseActionKind(string actionKind) =>
        actionKind is "buy_card" or "buy_potion" or
            "replace_shop_potion" or "remove_card" or "buy_relic";

    internal static bool IsRelicDeckTransformActionKind(string actionKind) =>
        actionKind is "choose_shop_relic_deck_card"
            or "choose_relic_deck_card";

    internal static bool IsRandomRelicDeckTransformAction(
        RunState source, GameAction action)
    {
        if (action.Kind is not ("choose_shop_relic_deck_card"
                or "choose_relic_deck_card"))
            return false;
        var pending = action.Kind == "choose_shop_relic_deck_card"
            ? source.Phase == RunPhase.Shop
                ? source.World?.Shop?.PendingDeckChoice : null
            : source.Phase == RunPhase.Reward
                ? source.World?.Reward?.PendingDeckChoice : null;
        if (pending is not { Kind: PrototypePersistentDeckChoiceKind.Transform }
            || PrototypeContent.Relic(pending.SourceRelicId)
                .AcquisitionDeckChoice is not
                {
                    Kind: PrototypePersistentDeckChoiceKind.Transform,
                    TransformToCardId: null
                })
            return false;
        var cardInstanceId = action.ReadPayload<ChooseDeckCardPayload>()
            .CardInstanceId;
        return pending.CandidateCardInstanceIds.Contains(cardInstanceId)
            && source.Player.Deck.Any(card =>
                card.InstanceId == cardInstanceId
                && !PrototypeCardRules.IsEternal(card));
    }

    internal static bool IsSupportedRelicDeckTransformRequest(
        RandomIndexRequestV111 request) =>
        request.SiteId == "reward.card_transform.replacement.index_draw"
        && request.StreamId == "reward"
        && request.OperationKind == "choose_index"
        && request.SupportCount > 0;

    internal static bool IsConfiguredNativeReferenceNatureRunStartSource(
        RunState source)
    {
        var configuration = source.Configuration;
        var acts = configuration?.Acts;
        return source.Phase == RunPhase.RunStart
            && source.World is null
            && source.GameBuild == V111Build.Id
            && source.EmulatorSchema == V111Build.StateSchema
            && source.Stochastic.NativeRunRng is not null
            && acts is { Length: 3 }
            && (acts[0] is ActIdentity.Overgrowth or ActIdentity.Underdocks)
            && configuration!.CharacterId == "silent"
            && configuration.Profile.FullyUnlocked;
    }

    internal static bool IsConfiguredNativeReferenceNatureOvergrowthRunStartSource(
        RunState source) =>
        IsConfiguredNativeReferenceNatureRunStartSource(source)
        && source.Configuration!.Acts[0] == ActIdentity.Overgrowth;

    internal static bool IsConfiguredNativeReferenceNatureUnderdocksRunStartSource(
        RunState source) =>
        IsConfiguredNativeReferenceNatureRunStartSource(source)
        && source.Configuration!.Acts[0] == ActIdentity.Underdocks;

    internal static bool IsConfiguredNativeReferenceNatureRunStartAction(
        RunState source, GameAction action) =>
        action.Kind == "start_run"
        && IsConfiguredNativeReferenceNatureRunStartSource(source);

    internal static bool IsConfiguredNativeReferenceNatureRunStartRequest(
        RunState source, ControlledStochasticRequestV111 request) =>
        IsConfiguredNativeReferenceNatureRunStartSource(source)
        && (IsConfiguredNativeReferenceNatureOvergrowthRunStartSource(source)
                ? IsConfiguredNativeOvergrowthBossRequest(request)
                : IsConfiguredNativeUnderdocksBossRequest(request));

    internal static bool IsConfiguredNativeOvergrowthBossRequest(
        ControlledStochasticRequestV111 request)
    {
        if (request.IndexRequest is not { } index)
            return false;
        var bossPool = PrototypeContent.OvergrowthBossEncounterPool;
        return index.SiteId == "run.encounter.overgrowth_boss.index_draw"
            && index.StreamId == "combat"
            && index.OperationKind == "choose_index"
            && index.OwnerId == "run:act:1:boss_pool:"
                + CanonicalJson.Sha256(bossPool)
            && index.Ordinal == 0
            && index.SupportCount == bossPool.Length
            && index.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, bossPool.Length));
    }

    internal static bool IsConfiguredNativeUnderdocksBossRequest(
        ControlledStochasticRequestV111 request)
    {
        if (request.IndexRequest is not { } index)
            return false;
        var bossPool = PrototypeNativeUnderdocks.NativeBossEncounterIds;
        return index.SiteId == PrototypeNativeUnderdocks
                .BossEncounterIndexSiteId
            && index.StreamId == "combat"
            && index.OperationKind == "choose_index"
            && index.OwnerId == "act:1:underdocks:boss_pool:"
                + CanonicalJson.Sha256(bossPool)
            && index.Ordinal == 0
            && index.SupportCount == bossPool.Length
            && index.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, bossPool.Length));
    }

    internal static bool IsShopPurchaseAction(
        RunState source, GameAction action) =>
        source.Phase == RunPhase.Shop
        && IsShopPurchaseActionKind(action.Kind);

    internal static bool IsCombatRewardRelicAction(
        RunState source, GameAction action) =>
        source.Phase == RunPhase.Reward
        && source.Stochastic.NativeRunRng is null
        && (source.World?.Reward?.SourceRoom is
            "Combat" or "Elite" or "Boss")
        && IsCombatRewardRelicActionKind(action.Kind);

    internal static bool IsCombatRewardRelicActionKind(string actionKind) =>
        actionKind is "take_reward_relic" or "take_reward_extra_relic";

    internal static bool IsSupportedOutsideCombatPotionAction(
        RunState source, GameAction action)
    {
        var pendingEventReplacement =
            source.Phase == RunPhase.Event
            && source.Stochastic.ControlledEventRewardChoice is null
            && source.World?.Event is
                { PendingPotionReplacement: not null } eventState
            && eventState.Stochastic.ControlledChoice is null;
        if (action.Kind != "use_potion"
            || (!SupportsOutsideCombatPotionPhase(source)
                && !pendingEventReplacement))
            return false;

        return IsModeledDeterministicOutsideCombatPotion(source, action);
    }

    internal static bool IsSupportedEventRewardPotionAction(
        RunState source, GameAction action)
    {
        return IsEventRewardPotionContext(source, action)
            && (IsModeledDeterministicOutsideCombatPotion(source, action)
                || IsEntropicBrewPotionAction(source, action));
    }

    internal static bool IsSupportedDeterministicEventRewardPotionAction(
        RunState source, GameAction action) =>
        IsEventRewardPotionContext(source, action)
        && IsModeledDeterministicOutsideCombatPotion(source, action);

    private static bool IsEventRewardPotionContext(
        RunState source, GameAction action)
    {
        var eventState = source.World?.Event;
        var reward = source.World?.Reward;
        var choiceId = eventState?.ChosenChoiceId;
        if (action.Kind != "use_potion"
            || source.Phase != RunPhase.Reward
            || source.Stochastic.ControlledEventRewardChoice is not null
            || eventState is null
            || eventState.PendingPotionReplacement is not null
            || eventState.Stochastic.ControlledChoice is not null
            || choiceId is null
            || reward is null
            || reward.SourceRoom != "Event:" + eventState.EventId
            || !PrototypeGameEngine.IsControlledEventChoice(
                eventState.EventId, choiceId)
            || source.Stochastic.ControlledEventChoiceHistory is
                not { Length: > 0 } history
            || history[^1].EventId != eventState.EventId
            || history[^1].ChoiceId != choiceId)
            return false;

        return action.Kind == "use_potion";
    }

    private static bool IsModeledDeterministicOutsideCombatPotion(
        RunState source, GameAction action)
    {
        var payload = action.ReadPayload<UsePotionPayload>();
        if (payload.TargetEnemyId is not null
            || payload.Slot < 0
            || payload.Slot >= source.Player.PotionSlots.Length
            || source.Player.PotionSlots[payload.Slot] is not { } instance)
            return false;

        var definition = PrototypeContent.Potion(instance.PotionId);
        if (!definition.UsableOutsideCombat)
            return false;

        var effects = definition.RunEffects ?? [];
        return instance.PotionId == "proto.potion.fruit_juice"
                && effects.SequenceEqual([
                    new PrototypeRunEffectSpec(
                        PrototypeRunEffectKind.GainMaxHp, 5)])
            || instance.PotionId == "proto.potion.blood"
                && effects.SequenceEqual([
                    new PrototypeRunEffectSpec(
                        PrototypeRunEffectKind.HealPercentMaxHp, 20)])
            || instance.PotionId == "proto.native.neow.ambergris"
                && effects.SequenceEqual([
                    new PrototypeRunEffectSpec(
                        PrototypeRunEffectKind.HealPercentMaxHp, 50)]);
    }

    internal static bool IsEntropicBrewRunAction(
        RunState source, GameAction action)
    {
        return IsEntropicBrewPotionAction(source, action)
            && (SupportsOutsideCombatPotionPhase(source)
                || SupportsCombatEntropicBrewAction(source, action));
    }

    private static bool SupportsCombatEntropicBrewAction(
        RunState source, GameAction action)
    {
        var combat = source.World?.Combat;
        if (source.Phase != RunPhase.Combat
            || combat is null
            || combat.PendingChoice is not null
            || combat.PendingKnowledgeDemonChoice is not null
            || (source.Stochastic.NativeRunRng is null
                && combat.Stochastic.NativeRng is not null))
            return false;

        var slot = action.ReadPayload<UsePotionPayload>().Slot;
        return combat.PotionStates.Any(potion =>
            potion.Slot == slot
            && potion.PotionId == source.Player.PotionSlots[slot]?.PotionId);
    }

    internal static bool IsEntropicBrewPotionAction(
        RunState source, GameAction action)
    {
        if (action.Kind != "use_potion")
            return false;

        var payload = action.ReadPayload<UsePotionPayload>();
        if (payload.TargetEnemyId is not null
            || payload.Slot < 0
            || payload.Slot >= source.Player.PotionSlots.Length
            || source.Player.PotionSlots[payload.Slot] is not { } potion
            || potion.PotionId != "proto.potion.entropic_brew")
            return false;

        var definition = PrototypeContent.Potion(potion.PotionId);
        return definition.UsableOutsideCombat
            && (definition.RunEffects ?? []).SequenceEqual([
                new PrototypeRunEffectSpec(
                    PrototypeRunEffectKind.FillPotionSlots)]);
    }

    internal static bool IsEntropicBrewFillRequest(
        RunState source, GameAction action,
        RandomIndexRequestV111 request)
    {
        if (!IsEntropicBrewPotionAction(source, action)
            || request.SiteId != "run.potion.fill_empty_slots.model"
            || request.OperationKind != "choose_index"
            || source.Player.ActiveRelics.Any(relic =>
                PrototypeContent.Relic(relic.RelicId)
                    .PreventPotionAcquisition))
            return false;

        var expectedStream = source.Stochastic.NativeRunRng is null
            ? "reward" : NativePlayerRngV111.RewardsStreamId;
        if (request.StreamId != expectedStream)
            return false;

        var pool = PrototypeContent.PotionPool;
        var selectedSlot = action.ReadPayload<UsePotionPayload>().Slot;
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var validOwners = source.Player.PotionSlots
            .Select((potion, slot) => (potion, slot))
            .Where(item => item.potion is null || item.slot == selectedSlot)
            .Select(item =>
                $"player:potion_slot:{item.slot}:pool:{poolFingerprint}");
        return request.SupportCount == pool.Length
            && request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, pool.Length))
            && validOwners.Contains(request.OwnerId, StringComparer.Ordinal);
    }

    internal static bool IsEntropicBrewFillRequestShape(
        RandomIndexRequestV111 request)
    {
        if (request.SiteId != "run.potion.fill_empty_slots.model"
            || request.OperationKind != "choose_index"
            || request.StreamId is not ("reward" or
                NativePlayerRngV111.RewardsStreamId))
            return false;

        var pool = PrototypeContent.PotionPool;
        var poolFingerprint = CanonicalJson.Sha256(pool);
        const string prefix = "player:potion_slot:";
        var suffix = ":pool:" + poolFingerprint;
        if (!request.OwnerId.StartsWith(prefix, StringComparison.Ordinal)
            || !request.OwnerId.EndsWith(suffix, StringComparison.Ordinal))
            return false;
        var slotText = request.OwnerId[prefix.Length..^suffix.Length];
        return int.TryParse(slotText, NumberStyles.None,
                CultureInfo.InvariantCulture, out var slot)
            && slot >= 0
            && request.SupportCount == pool.Length
            && request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, pool.Length));
    }

    private static bool SupportsOutsideCombatPotionPhase(
        RunState source) =>
        source.Phase is (RunPhase.MapChoice or RunPhase.Shop or RunPhase.Rest
            or RunPhase.ActTransition)
        || source.Phase == RunPhase.Reward
            && !IsEventRewardContext(source)
        || source.Phase == RunPhase.Event
            && source.World?.Event is
                { PendingPotionReplacement: null } eventState
            && eventState.Stochastic.ControlledChoice is null;

    private static bool IsEventRewardContext(RunState source) =>
        source.World?.Event is { ChosenChoiceId: not null } eventState
        && source.World?.Reward is { } reward
        && reward.SourceRoom == "Event:" + eventState.EventId;

    internal static bool IsLegacyRestHealRewardAction(
        RunState source, GameAction action) =>
        source.Phase == RunPhase.Rest
        && source.Stochastic.NativeRunRng is null
        && source.World?.ActiveRoom == PrototypeRoomType.Rest
        && action.Kind == PrototypeNativeRestSiteActionsV111.Heal
        && source.Player.ActiveRelics.Any(relic =>
            PrototypeContent.Relic(relic.RelicId).RestHealCardRewardCount > 0);

    internal static bool IsNativeRestHealRewardAction(
        RunState source, GameAction action) =>
        source.Phase == RunPhase.Rest
        && source.Stochastic.NativeRunRng is not null
        && source.World is { Act: 1 } world
        && world.ActiveRoom == PrototypeRoomType.Rest
        && PrototypeGameEngine.UsesNativeActOneSystems(world)
        && action.Kind == PrototypeNativeRestSiteActionsV111.Heal
        && source.Player.ActiveRelics.Any(relic =>
            PrototypeContent.Relic(relic.RelicId).RestHealCardRewardCount > 0);

    internal static bool IsNativeRestHealRewardRequest(
        RunState source, GameAction action,
        ControlledStochasticRequestV111 request)
    {
        if (!IsNativeRestHealRewardAction(source, action) ||
            request.StreamId != NativePlayerRngV111.RewardsStreamId)
            return false;
        return request.IndexRequest is { } indexRequest
            ? indexRequest.SiteId ==
                    "card_rarity.encounter_model.index_draw"
                && indexRequest.OperationKind == "choose_index"
                && indexRequest.OrderedCandidates.SequenceEqual(
                    Enumerable.Range(0, indexRequest.SupportCount))
            : request.UnitFloatRequest is { } floatRequest
                && floatRequest.SiteId is
                    "card_rarity.roll.unit_float" or
                    "card_rarity.upgrade_roll.unit_float";
    }

    internal static bool IsSupportedRestHealRewardRequest(
        RandomIndexRequestV111 request) =>
        StochasticSiteRegistryV111.IsRunActionRestHealRewardSite(
            request.SiteId)
        && request.StreamId == "reward";

    internal static bool RequiresRunActionFingerprint(
        RunState source, GameAction action) =>
        IsShopPurchaseAction(source, action)
        || IsCombatRewardRelicAction(source, action)
        || IsRandomRelicDeckTransformAction(source, action)
        || IsLegacyRestHealRewardAction(source, action)
        || IsNativeRestHealRewardAction(source, action)
        || IsEntropicBrewRunAction(source, action);

    internal static void ValidatePendingShopPurchase(
        RunState source,
        GameAction action,
        RandomIndexDecisionV111[] decisions,
        string[][] decisionCandidateLabels,
        RandomIndexRequestV111 expectedRequest,
        string[] expectedCandidateLabels)
        => ValidatePendingRunActionChoice(
            source, action, decisions, decisionCandidateLabels,
            expectedRequest,
            expectedCandidateLabels,
            PendingRunActionRequestKind.ShopPurchase);

    internal static void ValidatePendingRewardRelicAcquisition(
        RunState source,
        GameAction action,
        RandomIndexDecisionV111[] decisions,
        string[][] decisionCandidateLabels,
        RandomIndexRequestV111 expectedRequest,
        string[] expectedCandidateLabels)
        => ValidatePendingRunActionChoice(
            source, action, decisions, decisionCandidateLabels,
            expectedRequest,
            expectedCandidateLabels,
            PendingRunActionRequestKind.RewardRelic);

    internal static void ValidatePendingRelicDeckTransform(
        RunState source,
        GameAction action,
        RandomIndexDecisionV111[] decisions,
        string[][] decisionCandidateLabels,
        RandomIndexRequestV111 expectedRequest,
        string[] expectedCandidateLabels) =>
        ValidatePendingRunActionChoice(
            source, action, decisions, decisionCandidateLabels,
            expectedRequest, expectedCandidateLabels,
            PendingRunActionRequestKind.RelicDeckTransform);

    internal static void ValidatePendingRestHealReward(
        RunState source,
        GameAction action,
        RandomIndexDecisionV111[] decisions,
        string[][] decisionCandidateLabels,
        RandomIndexRequestV111 expectedRequest,
        string[] expectedCandidateLabels) =>
        ValidatePendingRunActionChoice(
            source, action, decisions, decisionCandidateLabels,
            expectedRequest,
            expectedCandidateLabels,
            PendingRunActionRequestKind.RestHealReward);

    private enum PendingRunActionRequestKind
    {
        ShopPurchase,
        RewardRelic,
        RelicDeckTransform,
        RestHealReward
    }

    private static void ValidatePendingRunActionChoice(
        RunState source,
        GameAction action,
        RandomIndexDecisionV111[] decisions,
        string[][] decisionCandidateLabels,
        RandomIndexRequestV111 expectedRequest,
        string[] expectedCandidateLabels,
        PendingRunActionRequestKind requestKind)
    {
        var actionBoundaryValid = requestKind switch
        {
            PendingRunActionRequestKind.ShopPurchase =>
                IsShopPurchaseAction(source, action),
            PendingRunActionRequestKind.RewardRelic =>
                IsCombatRewardRelicAction(source, action),
            PendingRunActionRequestKind.RelicDeckTransform =>
                IsRandomRelicDeckTransformAction(source, action),
            PendingRunActionRequestKind.RestHealReward =>
                IsLegacyRestHealRewardAction(source, action),
            _ => false
        };
        var requestBoundaryValid = requestKind switch
        {
            PendingRunActionRequestKind.ShopPurchase =>
                StochasticSiteRegistryV111.IsRunActionShopPurchaseSite(
                    expectedRequest.SiteId)
                && IsSupportedShopPurchaseStream(expectedRequest),
            PendingRunActionRequestKind.RewardRelic =>
                StochasticSiteRegistryV111.IsRunActionShopPurchaseSite(
                    expectedRequest.SiteId)
                && IsSupportedShopPurchaseStream(expectedRequest),
            PendingRunActionRequestKind.RelicDeckTransform =>
                IsSupportedRelicDeckTransformRequest(expectedRequest),
            PendingRunActionRequestKind.RestHealReward =>
                IsSupportedRestHealRewardRequest(expectedRequest),
            _ => false
        };
        if (!actionBoundaryValid || !requestBoundaryValid
            || !new PrototypeGameEngine().GetLegalActions(source).Any(
                candidate => PrototypeAiEnvironment.StableActionId(candidate)
                    == PrototypeAiEnvironment.StableActionId(action)))
            throw new InvalidDataException(
                "Controlled run-action replay has an invalid action boundary.");

        ControlledChanceReplayResultV111 replay =
            ControlledChanceGuardV111.ExecuteGuarded(() =>
                ControlledChanceContinuationExecutorV111.Resume(
                    source, decisions, checkpoint => new PrototypeGameEngine()
                        .Step(checkpoint with
                        {
                            DecisionIndex = checked(checkpoint.DecisionIndex - 1)
                        }, CloneAction(action)).State,
                    decisionCandidateLabels: decisionCandidateLabels));

        if (replay.PendingChance is not { } pending
            || !ControlledStochasticChanceReplayV111.RequestsMatch(
                pending.Request, expectedRequest)
            || pending.CandidateLabels is null
            || !ControlledStochasticChanceReplayV111.CandidateLabelsMatch(
                pending.CandidateLabels, expectedCandidateLabels))
            throw new InvalidDataException(
                "Controlled run-action request differs from its source replay.");
    }

    public static RunState Begin(RunState source, GameAction action)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        PrototypeStateInvariants.Validate(source);
        var mapNodeId = action.Kind == "choose_map_node"
            ? action.ReadPayload<ChooseMapNodePayload>().NodeId
            : null;
        var supportedMapNodeAction = source.Phase == RunPhase.MapChoice
            && source.Stochastic.NativeRunRng is null
            && source.World is { } mapNodeWorld
            && PrototypeNativeWingedBoots.AvailableNodes(
                    mapNodeWorld, source.Player)
                .Any(node => node.NodeId == mapNodeId
                    && (node.RoomType == PrototypeRoomType.Event
                        || node.RoomType == PrototypeRoomType.Shop
                            && !PrototypeGameEngine
                                .UsesNativeRegionalSystems(mapNodeWorld)
                        || node.RoomType == PrototypeRoomType.Treasure
                            && !PrototypeGameEngine
                                .UsesNativeRegionalSystems(mapNodeWorld)
                            && !PrototypeGameEngine
                                .WillSkipNextTreasure(source.Player)
                            && PrototypeContent.RelicPool.Any(id =>
                                !source.Player.Relics.Any(relic =>
                                    StringComparer.Ordinal.Equals(
                                        relic.RelicId, id)))
                        || IsLegacyOvergrowthCombatRoom(
                            source, node.RoomType)
                        || IsLegacyUnderdocksCombatRoom(
                            source, node.RoomType)));
        supportedMapNodeAction |=
            IsLegacyUnknownRoomEntryAction(source, action);
        supportedMapNodeAction |=
            IsLegacyNativeTreasureEntryAction(source, action);
        var nativeActOneCombatEntryAction =
            IsNativeActOneCombatEntryAction(source, action);
        supportedMapNodeAction |= nativeActOneCombatEntryAction;
        var overgrowthCombatEntryAction =
            IsLegacyOvergrowthCombatEntryAction(source, action);
        var underdocksCombatEntryAction =
            IsLegacyUnderdocksCombatEntryAction(source, action);
        var legacyActOneCombatEntryAction = overgrowthCombatEntryAction
            || underdocksCombatEntryAction;
        var configuredNativeRunStartAction =
            V111RunFactory.ControlledReferenceNatureRunStartActive
            && IsConfiguredNativeReferenceNatureRunStartAction(
                source, action);
        var shopPurchaseAction = IsShopPurchaseAction(
            source, action);
        var rewardRelicAction = IsCombatRewardRelicAction(
            source, action);
        var relicDeckTransformAction =
            IsRandomRelicDeckTransformAction(source, action);
        var acquisitionAction = shopPurchaseAction || rewardRelicAction;
        var restHealRewardAction = IsLegacyRestHealRewardAction(
            source, action);
        var outsideCombatPotionAction =
            IsSupportedOutsideCombatPotionAction(source, action);
        var supportedLegacyActTransitionAction =
            action.Kind == "continue_act"
            && source.Phase == RunPhase.ActTransition
            && source.Configuration is null
            && source.Stochastic.NativeRunRng is null
            && source.World is { } transitionWorld
            && transitionWorld.Act < PrototypeContent.Rules.Acts;
        if (source.Stochastic.ControlledRunActionChoice is not null
            || (action.Kind is not ("start_run" or "continue_act"
                or PrototypeNativeRestSiteActionsV111.Dig
                or "choose_map_node")
                && !acquisitionAction && !restHealRewardAction
                && !outsideCombatPotionAction
                && !relicDeckTransformAction)
            || action.Kind == "continue_act"
                && !supportedLegacyActTransitionAction
            || action.Kind == "choose_map_node" && !supportedMapNodeAction)
            throw new InvalidOperationException(
                "Controlled run action requires an unsuspended supported action.");
        var engine = new PrototypeGameEngine();
        var actionId = PrototypeAiEnvironment.StableActionId(action);
        if (!engine.GetLegalActions(source).Any(candidate =>
                StringComparer.Ordinal.Equals(
                    PrototypeAiEnvironment.StableActionId(candidate), actionId)))
            throw new InvalidOperationException(
                "Controlled run action requires a current legal action.");
        var replaySource = IsLegacyNativeTreasureEntryAction(source, action)
            || IsLegacyNativeShovelDigAction(source, action)
            ? ReferenceNatureRunProjectionV111.CreateNatureReplayOrigin(
                source, createRelicBagWhenMissing: true)
            : source.Fork();
        RunState result = ControlledChanceGuardV111.ExecuteGuarded(() =>
        {
            Func<ControlledStochasticRequestV111, bool>? requestFilter =
                configuredNativeRunStartAction
                    ? request =>
                        IsConfiguredNativeReferenceNatureRunStartRequest(
                            source, request)
                    : nativeActOneCombatEntryAction
                        ? IsNativeActOneCombatEntryRequest
                        : legacyActOneCombatEntryAction
                            ? IsLegacyActOneCombatEntryRequest : null;
            var replay = ControlledChanceContinuationExecutorV111.Resume(
                replaySource, [], checkpoint => engine.Step(checkpoint, action).State,
                requestFilter: requestFilter);
            if (replay.PendingChance is { } pendingChance)
            {
                var supportedStartRunSite = configuredNativeRunStartAction
                    && IsConfiguredNativeReferenceNatureRunStartRequest(
                        source,
                        ControlledStochasticRequestV111.FromIndex(
                            pendingChance.Request));
                var supportedActTransitionSite =
                    supportedLegacyActTransitionAction
                    && ControlledEventChoiceReplayV111.IsRunActionSite(
                        pendingChance.Request.SiteId)
                    && pendingChance.Request.SiteId.StartsWith(
                        "map.legacy.", StringComparison.Ordinal)
                    && pendingChance.Request.StreamId == "map";
                var supportedShopPurchaseSite = shopPurchaseAction
                    && StochasticSiteRegistryV111
                        .IsRunActionShopPurchaseSite(
                            pendingChance.Request.SiteId);
                var supportedRewardRelicSite = rewardRelicAction
                    && StochasticSiteRegistryV111
                        .IsRunActionShopPurchaseSite(
                            pendingChance.Request.SiteId)
                    && IsSupportedShopPurchaseStream(
                        pendingChance.Request);
                var supportedRelicDeckTransformSite =
                    relicDeckTransformAction
                    && IsSupportedRelicDeckTransformRequest(
                        pendingChance.Request);
                var supportedRestHealRewardSite = restHealRewardAction
                    && IsSupportedRestHealRewardRequest(
                        pendingChance.Request);
                var supportedShovelSite = action.Kind ==
                        PrototypeNativeRestSiteActionsV111.Dig
                    && StochasticSiteRegistryV111
                        .IsRunActionShovelSite(
                            pendingChance.Request.SiteId);
                var supportedMapNodeSite = supportedMapNodeAction
                    && (ControlledEventChoiceReplayV111
                            .IsRunActionEventEntrySite(pendingChance.Request.SiteId)
                        || ControlledEventChoiceReplayV111
                            .IsRunActionShopSite(pendingChance.Request.SiteId)
                        || ControlledEventChoiceReplayV111
                            .IsRunActionTreasureSite(pendingChance.Request.SiteId)
                        || IsLegacyActOneCombatEntrySite(
                            source, action, pendingChance.Request.SiteId)
                        || IsNativeActOneCombatEntrySite(
                            source, action, pendingChance.Request.SiteId));
                var labelsValid = pendingChance.CandidateLabels is { } labels
                    && ((supportedShopPurchaseSite
                            || supportedRewardRelicSite
                            || supportedRelicDeckTransformSite)
                        ? labels.Length == pendingChance.Request.SupportCount
                        : labels.Length == pendingChance.Request.SupportCount
                            || labels.Length == 0
                                && pendingChance.Request.SupportCount >
                                    MaximumInlineOutcomeActions);
                if ((!supportedStartRunSite && !supportedActTransitionSite
                        && !supportedMapNodeSite
                        && !supportedShopPurchaseSite
                        && !supportedRewardRelicSite
                        && !supportedRelicDeckTransformSite
                        && !supportedRestHealRewardSite
                        && !supportedShovelSite)
                    || !labelsValid)
                    throw new NotSupportedException(
                        "Controlled run action reached an unsupported chance request.");

                var partialState = replaySource.Fork() with
                {
                    DecisionIndex = checked(replaySource.DecisionIndex + 1)
                };
                var continuation =
                    new PrototypeControlledRunActionContinuationV111(
                        partialState,
                        supportedMapNodeAction
                            ? "choose_map_node" : action.Kind,
                        [],
                        [],
                        pendingChance.Request,
                        pendingChance.CandidateLabels!,
                        mapNodeId,
                        acquisitionAction || restHealRewardAction
                            || relicDeckTransformAction
                            ? CloneAction(action) : null);
                var suspended = RunStochasticStateV111
                    .SuspendControlledRunActionChoice(
                        partialState, continuation);
                PrototypeStateInvariants.Validate(suspended);
                return suspended;
            }

            return replay.Run ?? throw new InvalidDataException(
                "Controlled run-action begin returned neither a run nor a pending chance request.");
        });
        if (result.Stochastic.ControlledRunActionChoice is null)
        {
            if (acquisitionAction || restHealRewardAction ||
                outsideCombatPotionAction || relicDeckTransformAction ||
                supportedLegacyActTransitionAction ||
                legacyActOneCombatEntryAction ||
                nativeActOneCombatEntryAction)
            {
                PrototypeStateInvariants.Validate(result);
                return result;
            }
            throw new NotSupportedException(
                "The selected action did not reach a supported legacy controlled chance site.");
        }
        PrototypeStateInvariants.Validate(result);
        return result;
    }

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };

    internal static bool IsLegacyOvergrowthCombatRoom(
        RunState state, PrototypeRoomType roomType) =>
        state.Phase == RunPhase.MapChoice
        && state.Configuration is null
        && state.Stochastic.NativeRunRng is null
        && state.World is
        {
            Act: 1,
            ActOneRegion: PrototypeActOneRegion.Overgrowth,
            ActOneEncounterPool.Region: PrototypeActOneRegion.Overgrowth
        }
        && roomType is PrototypeRoomType.Combat or PrototypeRoomType.Elite;

    internal static bool IsLegacyUnderdocksCombatRoom(
        RunState state, PrototypeRoomType roomType) =>
        state.Phase == RunPhase.MapChoice
        && state.Configuration is null
        && state.Stochastic.NativeRunRng is null
        && state.World is
        {
            Act: 1,
            ActOneRegion: PrototypeActOneRegion.Underdocks,
            ActOneEncounterPool.Region: PrototypeActOneRegion.Underdocks,
            Map.GenerationProfileId:
                PrototypeNativeUnderdocks.GenerationProfileId
        }
        && roomType is PrototypeRoomType.Combat or PrototypeRoomType.Elite;

    internal static bool IsLegacyOvergrowthCombatEntryAction(
        RunState source, GameAction action)
    {
        if (action.Kind != "choose_map_node"
            || source.Phase != RunPhase.MapChoice
            || source.World is not { } world
            || !IsLegacyOvergrowthCombatRoom(
                source, PrototypeRoomType.Combat))
            return false;
        var nodeId = action.ReadPayload<ChooseMapNodePayload>().NodeId;
        return PrototypeNativeWingedBoots.AvailableNodes(world, source.Player)
            .Any(node => node.NodeId == nodeId
                && IsLegacyOvergrowthCombatRoom(source, node.RoomType));
    }

    internal static bool IsLegacyUnderdocksCombatEntryAction(
        RunState source, GameAction action)
    {
        if (action.Kind != "choose_map_node"
            || source.Phase != RunPhase.MapChoice
            || source.World is not { } world
            || !IsLegacyUnderdocksCombatRoom(
                source, PrototypeRoomType.Combat))
            return false;
        var nodeId = action.ReadPayload<ChooseMapNodePayload>().NodeId;
        return PrototypeNativeWingedBoots.AvailableNodes(world, source.Player)
            .Any(node => node.NodeId == nodeId
                && IsLegacyUnderdocksCombatRoom(source, node.RoomType));
    }

    internal static bool IsLegacyUnknownRoomEntryAction(
        RunState source, GameAction action)
    {
        if (action.Kind != "choose_map_node"
            || source.Phase != RunPhase.MapChoice
            || source.Configuration is not null
            || source.Stochastic.NativeRunRng is not null
            || source.World is not { } world
            || !PrototypeGameEngine.UsesNativeRegionalSystems(world))
            return false;
        var nodeId = action.ReadPayload<ChooseMapNodePayload>().NodeId;
        return PrototypeNativeWingedBoots.AvailableNodes(world, source.Player)
            .Any(node => node.NodeId == nodeId
                && node.RoomType == PrototypeRoomType.Unknown);
    }

    internal static bool IsLegacyNativeTreasureEntryAction(
        RunState source, GameAction action)
    {
        if (action.Kind != "choose_map_node"
            || source.Phase != RunPhase.MapChoice
            || source.Stochastic.NativeRunRng is not null
            || source.World is not { } world
            || !PrototypeGameEngine.UsesNativeRegionalSystems(world)
            || PrototypeGameEngine.WillSkipNextTreasure(source.Player))
            return false;
        var nodeId = action.ReadPayload<ChooseMapNodePayload>().NodeId;
        return PrototypeNativeWingedBoots.AvailableNodes(world, source.Player)
            .Any(node => node.NodeId == nodeId
                && node.RoomType == PrototypeRoomType.Treasure);
    }

    internal static bool IsLegacyNativeShovelDigAction(
        RunState source, GameAction action) =>
        action.Kind == PrototypeNativeRestSiteActionsV111.Dig
        && source.Phase == RunPhase.Rest
        && source.Stochastic.NativeRunRng is null
        && source.World is { } world
        && PrototypeGameEngine.UsesNativeRegionalSystems(world)
        && PrototypeNativeRestSiteRelics.CanDig(source.Player);

    internal static bool IsLegacyActOneCombatEntryAction(
        RunState source, GameAction action) =>
        IsLegacyOvergrowthCombatEntryAction(source, action)
        || IsLegacyUnderdocksCombatEntryAction(source, action);

    internal static bool IsLegacyActOneCombatEntrySite(
        RunState source, GameAction action, string siteId) =>
        IsLegacyOvergrowthCombatEntryAction(source, action)
            ? ControlledEventChoiceReplayV111
                .IsRunActionOvergrowthCombatEntrySite(siteId)
            : IsLegacyUnderdocksCombatEntryAction(source, action)
                && ControlledEventChoiceReplayV111
                    .IsRunActionUnderdocksCombatEntrySite(siteId);

    internal static bool IsLegacyActOneCombatEntryRequest(
        ControlledStochasticRequestV111 request) =>
        request.StreamId == "combat"
        || request.StreamId == "event"
            && ControlledEventChoiceReplayV111
                .IsRunActionCombatEntryPotionSite(request.SiteId);

    internal static bool IsNativeEncounterFormationRequest(
        ControlledStochasticRequestV111 request)
    {
        if (request.IndexRequest is not { } index ||
            index.SiteId != NativeEncounterFormationFactoryV111.FormationSiteId ||
            index.OperationKind != "choose_index" ||
            !index.StreamId.StartsWith("context.encounter.",
                StringComparison.Ordinal) ||
            !index.OwnerId.StartsWith("encounter:", StringComparison.Ordinal))
            return false;
        var floorSeparator = index.OwnerId.LastIndexOf(":floor:",
            StringComparison.Ordinal);
        if (floorSeparator <= "encounter:".Length ||
            !int.TryParse(index.OwnerId[(floorSeparator + ":floor:".Length)..],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var floor) || floor < 0)
            return false;
        var entry = index.OwnerId["encounter:".Length..floorSeparator];
        return entry.Length > 0 && index.StreamId ==
            NativeRngV111Contexts.EncounterFormationStreamId(entry, floor);
    }

    internal static bool IsNativeEncounterSelectionRequest(
        ControlledStochasticRequestV111 request)
    {
        if (request.IndexRequest is not { } index ||
            index.OperationKind != "choose_index" ||
            index.StreamId != "combat" ||
            index.OrderedCandidates.Length == 0 ||
            !index.OrderedCandidates.SequenceEqual(Enumerable.Range(
                0, index.OrderedCandidates.Length)))
            return false;
        return index.SiteId is
            "combat.encounter.overgrowth_elite.index_draw" or
            "combat.encounter.overgrowth_weak.index_draw" or
            "combat.encounter.overgrowth_normal.index_draw" or
            "combat.encounter.underdocks.elite.index_draw" or
            "combat.encounter.underdocks.weak.index_draw" or
            "combat.encounter.underdocks.normal.index_draw";
    }

    internal static bool IsNativeActOneCombatEntryAction(
        RunState source, GameAction action)
    {
        if (action.Kind != "choose_map_node" ||
            source.Phase != RunPhase.MapChoice ||
            source.Configuration?.UsesExperimentalNativeWeakCombatEntry != true ||
            source.Stochastic.NativeRunRng is null || source.World is not { Act: 1 } world ||
            world.Stochastic.ActOneEncounterPool?.Region != world.ActOneRegion)
            return false;
        var routeMatches = world.ActOneRegion switch
        {
            PrototypeActOneRegion.Overgrowth =>
                PrototypeNativeOvergrowthMap.IsGenerationProfile(
                    world.Map.GenerationProfileId),
            PrototypeActOneRegion.Underdocks =>
                world.Map.GenerationProfileId ==
                    PrototypeNativeUnderdocks.GenerationProfileId,
            _ => false
        };
        if (!routeMatches)
            return false;
        var nodeId = action.ReadPayload<ChooseMapNodePayload>().NodeId;
        return PrototypeNativeWingedBoots.AvailableNodes(world, source.Player)
            .Any(node => node.NodeId == nodeId &&
                node.RoomType is PrototypeRoomType.Combat or
                    PrototypeRoomType.Elite or PrototypeRoomType.Boss);
    }

    internal static bool IsNativeActOneCombatEntrySite(
        RunState source, GameAction action, string siteId)
    {
        if (!IsNativeActOneCombatEntryAction(source, action) ||
            source.World is not { } world)
            return false;
        var nodeId = action.ReadPayload<ChooseMapNodePayload>().NodeId;
        var node = PrototypeNativeWingedBoots.AvailableNodes(
                world, source.Player)
            .SingleOrDefault(candidate => candidate.NodeId == nodeId);
        if (node is null)
            return false;
        if (siteId == NativeEncounterFormationFactoryV111.FormationSiteId)
            return true;

        return (world.ActOneRegion, node.RoomType, siteId) switch
        {
            (PrototypeActOneRegion.Overgrowth,
                PrototypeRoomType.Elite,
                "combat.encounter.overgrowth_elite.index_draw") => true,
            (PrototypeActOneRegion.Overgrowth,
                PrototypeRoomType.Combat,
                "combat.encounter.overgrowth_weak.index_draw" or
                "combat.encounter.overgrowth_normal.index_draw") => true,
            (PrototypeActOneRegion.Underdocks,
                PrototypeRoomType.Elite,
                "combat.encounter.underdocks.elite.index_draw") => true,
            (PrototypeActOneRegion.Underdocks,
                PrototypeRoomType.Combat,
                "combat.encounter.underdocks.weak.index_draw" or
                "combat.encounter.underdocks.normal.index_draw") => true,
            _ => false
        };
    }

    internal static bool IsNativeActOneCombatEntryRequest(
        ControlledStochasticRequestV111 request) =>
        IsNativeEncounterFormationRequest(request)
        || IsNativeEncounterSelectionRequest(request);

    public static RunState Resolve(RunState source, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        var pending = ControlledRunActionPendingV111.Describe(source);
        if ((uint)selectedIndex >= (uint)pending.Request.SupportCount)
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));
        return Step(source, GameAction.Create(
            "choose_controlled_run_action_outcome",
            new ChooseControlledEventOutcomePayload(selectedIndex)));
    }

    private static RunState Step(RunState source, GameAction action)
    {
        return ControlledChanceGuardV111.ExecuteGuarded(() =>
        {
            var result = new PrototypeGameEngine().Step(source, action).State;
            PrototypeStateInvariants.Validate(result);
            return result;
        });
    }
}
