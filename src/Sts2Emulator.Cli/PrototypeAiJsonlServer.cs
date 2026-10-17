using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Cli;

internal static class PrototypeAiJsonlServer
{
    public const string WireSchemaId = "prototype-ai-jsonl-v0";
    public const string FairPolicyId = "prototype-fair-v0";
    public const string NativeOvergrowthResetId =
        "prototype-native-overgrowth-reset-v1";
    // A semantic capability: v1 guarantees committed, non-rerolling
    // public enemy intents with optional per-hit base/current damage.
    public const string PublicEnemyIntentId =
        "prototype-committed-public-enemy-intents-v1";

    public const string NatureBoundarySchemaId =
        "sts2-v111-nature-isolated-boundary-v1";
    public const string ControlledChanceSchemaId =
        ControlledPlayableCombatEngineV111.SchemaId;
    public const string SemanticChanceSchemaId =
        SemanticPilotCombatV111.SchemaId;

    public static void Run(TextReader input, TextWriter output)
    {
        var environment = new PrototypeAiEnvironment();
        var states = new Dictionary<string, RunState>(StringComparer.Ordinal);
        // Opaque handle provenance is managed at the bridge boundary.
        // Generic reset handles are never eligible for draw-order injection.
        var hypotheticalHandles = new HashSet<string>(StringComparer.Ordinal);
        var freshCombatHandles = new HashSet<string>(StringComparer.Ordinal);
        long nextHandle = 1;
        // Experimental Nature handles are a separate namespace. They are NOT
        // playable RunState handles; only explicitly supported isolated entry
        // mechanics may be resumed. Existing Step() remains unaffected.
        var natureHp = new Dictionary<string, ControlledInitialHpStateV111>(
            StringComparer.Ordinal);
        var natureIntents = new Dictionary<string, ControlledIntentStateV111>(
            StringComparer.Ordinal);
        long nextNatureHandle = 1;
        var controlledStates = new Dictionary<string, ControlledPlayableCombatV111>(
            StringComparer.Ordinal);
        long nextControlledHandle = 1;
        var semanticStates = new Dictionary<string, SemanticPilotCombatV111>(
            StringComparer.Ordinal);
        long nextSemanticHandle = 1;
        var referenceNatureStates = new Dictionary<string, ReferenceNatureSessionV111>(
            StringComparer.Ordinal);
        long nextReferenceNatureHandle = 1;
        var referenceNatureRunActionStates =
            new Dictionary<string, ReferenceNatureRunActionSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureRunActionRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureRunActionHandle = 1;
        var referenceNatureEventChoiceStates =
            new Dictionary<string, ReferenceNatureControlledEventChoiceSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureEventChoiceRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureEventChoiceHandle = 1;
        var referenceNatureAncientOfferStates =
            new Dictionary<string, ReferenceNatureAncientOfferSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureAncientOfferRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureAncientOfferHandle = 1;
        var referenceNatureMerchantStates =
            new Dictionary<string, ReferenceNatureMerchantSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureMerchantRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureMerchantHandle = 1;
        var referenceNatureTreasureStates =
            new Dictionary<string, ReferenceNatureTreasureSessionV111>(
                StringComparer.Ordinal);
        var referenceNatureTreasureRunStateHandles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        long nextReferenceNatureTreasureHandle = 1;

        string StoreSemantic(SemanticPilotCombatV111 state)
        {
            var handle = "m" + nextSemanticHandle++;
            semanticStates.Add(handle, state.Fork());
            return handle;
        }

        SemanticPilotCombatV111 RequireSemantic(JsonElement request)
        {
            var handle = RequiredString(request, "semantic_handle");
            return semanticStates.TryGetValue(handle, out var state)
                ? state
                : throw new InvalidOperationException(
                    "Unknown semantic session handle: " + handle);
        }

        string StoreReferenceNature(ReferenceNatureSessionV111 session)
        {
            var handle = "r" + nextReferenceNatureHandle++;
            referenceNatureStates.Add(handle, session.Fork());
            return handle;
        }

        ReferenceNatureSessionV111 RequireReferenceNature(JsonElement request)
        {
            var handle = RequiredString(request, "reference_nature_handle");
            return referenceNatureStates.TryGetValue(handle, out var state)
                ? state
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature session handle: " + handle);
        }

        string StoreReferenceNatureRunAction(
            ReferenceNatureRunActionSessionV111 session)
        {
            var handle = "u" + nextReferenceNatureRunActionHandle++;
            referenceNatureRunActionStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureRunActionRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureRunActionSessionV111 RequireReferenceNatureRunAction(
            JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_run_action_handle");
            return referenceNatureRunActionStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature run-action session handle: " + handle);
        }

        string StoreReferenceNatureEventChoice(
            ReferenceNatureControlledEventChoiceSessionV111 session)
        {
            var handle = "v" + nextReferenceNatureEventChoiceHandle++;
            referenceNatureEventChoiceStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureEventChoiceRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureControlledEventChoiceSessionV111
            RequireReferenceNatureEventChoice(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_event_choice_handle");
            return referenceNatureEventChoiceStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature event-choice session handle: " +
                    handle);
        }

        string StoreReferenceNatureAncientOffer(
            ReferenceNatureAncientOfferSessionV111 session)
        {
            var handle = "w" + nextReferenceNatureAncientOfferHandle++;
            referenceNatureAncientOfferStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureAncientOfferRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureAncientOfferSessionV111
            RequireReferenceNatureAncientOffer(JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_ancient_offer_handle");
            return referenceNatureAncientOfferStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature Ancient-offer session handle: " +
                    handle);
        }

        string StoreReferenceNatureMerchant(
            ReferenceNatureMerchantSessionV111 session)
        {
            var handle = "x" + nextReferenceNatureMerchantHandle++;
            referenceNatureMerchantStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureMerchantRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureMerchantSessionV111 RequireReferenceNatureMerchant(
            JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_merchant_handle");
            return referenceNatureMerchantStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature merchant session handle: " +
                    handle);
        }

        string StoreReferenceNatureTreasure(
            ReferenceNatureTreasureSessionV111 session)
        {
            var handle = "y" + nextReferenceNatureTreasureHandle++;
            referenceNatureTreasureStates.Add(handle, session.Fork());
            if (session.Complete)
                referenceNatureTreasureRunStateHandles.Add(handle,
                    Store(session.Run.Fork()));
            return handle;
        }

        ReferenceNatureTreasureSessionV111 RequireReferenceNatureTreasure(
            JsonElement request)
        {
            var handle = RequiredString(request,
                "reference_nature_treasure_handle");
            return referenceNatureTreasureStates.TryGetValue(handle,
                    out var session)
                ? session
                : throw new InvalidOperationException(
                    "Unknown ReferenceNature treasure session handle: " +
                    handle);
        }

        object ReferenceNatureResult(
            ReferenceNatureSessionV111 session, string handle,
            string? parent = null)
        {
            var combat = session.Combat;
            return new
            {
                ok = true,
                schemaId = ReferenceNatureSessionV111.CurrentSchemaId,
                referenceNatureHandle = handle,
                parent,
                fingerprint = session.Fingerprint,
                combatFingerprint = combat.Fingerprint,
                boundary = combat.CombatTerminal ? "terminal" :
                    combat.AtPlayerBoundary ? "player" :
                    combat.AtPlayerChoiceBoundary ? "player_choice" : "nature",
                pending = DescribeSemanticPending(combat),
                terminal = combat.CombatTerminal,
                outcome = combat.CombatOutcome,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus,
                sourceOpeningFingerprint = combat.SourceOpeningFingerprint,
                sourceRouteProvider = combat.SourceRouteAudit?.ProviderId,
                otherChanceProvider = combat.OtherChance?.ProviderId ??
                    "engine-owned-deterministic-unverified-v1",
                otherChanceDecisionCount = combat.OtherChance?.Decisions.Length ?? 0,
                rewardProvider = combat.RewardAudit?.ProviderId,
                rewardDrawCount = combat.RewardAudit?.Calls
            };
        }

        object ReferenceNatureRunActionResult(
            ReferenceNatureRunActionSessionV111 session, string handle,
            string? parent = null)
        {
            var continuation = session.Run.ControlledRunActionChoice;
            var chanceLaw = continuation is null ? null :
                ChanceLawEngineV111.QueryControlledRunAction(session.Run);
            var runFingerprint = CanonicalJson.Sha256(session.Run);
            var boundaryFingerprint = continuation is null ? null :
                ChanceLawEngineV111.ControlledRunActionBoundaryFingerprint(
                    session.Run);
            object? pending = continuation is null ? null : new
            {
                kind = "run_action_random_choice",
                eventId = chanceLaw!.EventId,
                boundaryFingerprint,
                actionId = continuation.ActionId,
                request = continuation.PendingRequest,
                candidateLabels = continuation.PendingCandidateLabels,
                chanceLaw,
                probabilityStatus = chanceLaw.ProbabilityStatus
            };
            return new
            {
                ok = true,
                schemaId = ReferenceNatureRunActionSessionV111.CurrentSchemaId,
                referenceNatureRunActionHandle = handle,
                stateHandle = referenceNatureRunActionRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint,
                actionId = session.Action.Kind,
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                pending,
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureEventChoiceResult(
            ReferenceNatureControlledEventChoiceSessionV111 session,
            string handle, string? parent = null)
        {
            var effect = session.Run.World?.Event?.ControlledChoice;
            var reward = session.Run.ControlledEventRewardChoice;
            var chanceLaw = session.NaturePending
                ? ChanceLawEngineV111.QueryControlledEventChoice(session.Run)
                : null;
            var boundaryFingerprint = session.NaturePending
                ? ChanceLawEngineV111.ControlledEventChoiceBoundaryFingerprint(
                    session.Run)
                : null;
            var request = effect?.PendingRequest ?? reward?.PendingRequest;
            var candidateLabels = effect?.PendingCandidateLabels ??
                reward?.PendingCandidateLabels;
            object? pending = session.NaturePending ? new
            {
                kind = effect is null
                    ? "event_reward_random_choice" : "event_random_choice",
                eventId = chanceLaw!.EventId,
                choiceId = session.ChoiceId,
                boundaryFingerprint,
                request,
                candidateLabels,
                chanceLaw,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            } : null;
            var actions = session.CanAdvancePlayer
                ? environment.Observe(session.Run).LegalActions
                : Array.Empty<PrototypeAiAction>();
            return new
            {
                ok = true,
                schemaId =
                    ReferenceNatureControlledEventChoiceSessionV111.CurrentSchemaId,
                referenceNatureEventChoiceHandle = handle,
                stateHandle = referenceNatureEventChoiceRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                eventId = session.Origin.World?.Event?.EventId,
                choiceId = session.ChoiceId,
                runPhase = session.Run.Phase.ToString(),
                boundary = session.NaturePending ? "nature" :
                    session.CanAdvancePlayer ? "player" : "committed",
                pending,
                actions = actions.Select(ActionWire).ToArray(),
                decisionTranscript = session.DecisionTranscript,
                playerActionTranscript = session.Steps
                    .Where(step => step.Kind == "player_action")
                    .Select(step => step.PlayerAction).ToArray(),
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureAncientOfferResult(
            ReferenceNatureAncientOfferSessionV111 session,
            string handle, string? parent = null)
        {
            var eventState = session.Run.World?.Event;
            var priorIndices =
                ChanceLawEngineV111.IsShuffleAncientOfferSite(session.SiteId)
                    ? session.DecisionTranscript
                        .Select(decision => decision.OutcomeIndex).ToArray()
                    : null;
            var law = session.Complete ? null :
                ChanceLawEngineV111.QueryAncientOffer(
                    session.Origin, session.SiteId, session.NextOrdinal,
                    priorIndices);
            return new
            {
                ok = true,
                schemaId = ReferenceNatureAncientOfferSessionV111.CurrentSchemaId,
                referenceNatureAncientOfferHandle = handle,
                stateHandle = referenceNatureAncientOfferRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                siteId = session.SiteId,
                eventId = session.SiteId switch
                {
                    var site when site == NativeOrobasEventV111.OfferSiteId =>
                        NativeOrobasEventV111.EventId,
                    var site when site == NativePaelRewardsV111.OfferSiteId =>
                        NativePaelRewardsV111.PaelId,
                    var site when site ==
                        NativeAncientRewardsV111.TezcataraOfferSiteId =>
                        NativeAncientRewardsV111.TezcataraId,
                    var site when site ==
                        NativeNonupeipeEventV111.ShuffleSiteId =>
                        NativeNonupeipeEventV111.EventId,
                    var site when site == NativeTanxEventV111.ShuffleSiteId =>
                        NativeTanxEventV111.EventId,
                    _ => NativeVakuuEventV111.EventId
                },
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending = law is null ? null : new
                {
                    kind = "ancient_event_offer_random_choice",
                    boundaryFingerprint = law.EventId,
                    chanceLaw = law,
                    probabilityStatus = law.ProbabilityStatus
                },
                offeredChoiceIds = eventState?.OfferedChoiceIds,
                eventRandomProvider = eventState?.NativeEventRngProviderMode ??
                    (eventState is null ? null : "native-v111"),
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureMerchantResult(
            ReferenceNatureMerchantSessionV111 session, string handle,
            string? parent = null)
        {
            var law = session.PendingLaw;
            object? pending = law is null ? null : new
            {
                kind = "native_merchant_random_choice",
                request = session.PendingRequest,
                candidateLabels = session.PendingCandidateLabels,
                chanceLaw = law,
                probabilityStatus = law.ProbabilityStatus
            };
            return new
            {
                ok = true,
                schemaId = ReferenceNatureMerchantSessionV111.CurrentSchemaId,
                referenceNatureMerchantHandle = handle,
                stateHandle = referenceNatureMerchantRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                actionId = PrototypeAiEnvironment.StableActionId(session.Action),
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending,
                shop = session.Complete ? session.Run.World?.Shop : null,
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object ReferenceNatureTreasureResult(
            ReferenceNatureTreasureSessionV111 session, string handle,
            string? parent = null)
        {
            var law = session.PendingLaw;
            object? pending = law is null ? null : new
            {
                kind = "native_treasure_random_choice",
                request = session.PendingRequest,
                candidateLabels = session.PendingCandidateLabels,
                chanceLaw = law,
                probabilityStatus = law.ProbabilityStatus
            };
            return new
            {
                ok = true,
                schemaId = ReferenceNatureTreasureSessionV111.CurrentSchemaId,
                referenceNatureTreasureHandle = handle,
                stateHandle = referenceNatureTreasureRunStateHandles
                    .GetValueOrDefault(handle),
                parent,
                fingerprint = session.Fingerprint,
                runFingerprint = CanonicalJson.Sha256(session.Run),
                actionId = PrototypeAiEnvironment.StableActionId(session.Action),
                runPhase = session.Run.Phase.ToString(),
                boundary = session.Complete ? "committed" : "nature",
                nextOrdinal = session.NextOrdinal,
                pending,
                reward = session.Complete ? session.Run.World?.Reward : null,
                decisionTranscript = session.DecisionTranscript,
                natureProvider = ReferenceNatureV111.ProviderId,
                natureSampleCount = session.NatureState.SampleCount,
                probabilityStatus = ChanceLawV111.UnverifiedStatus
            };
        }

        object? DescribeSemanticPending(SemanticPilotCombatV111 state)
        {
            if (state.DrawHookChoicePending is { } hookChoice)
            {
                return new
                {
                    kind = "draw_hook_action_choice",
                    schemaId = hookChoice.SchemaId,
                    eventId = hookChoice.EventId,
                    boundaryFingerprint = hookChoice.BoundaryFingerprint,
                    supportFingerprint = hookChoice.SupportFingerprint,
                    choiceId = hookChoice.ChoiceId,
                    automaticHandDraw = hookChoice.AutomaticHandDraw,
                    candidates = hookChoice.Candidates.Select(candidate => new
                    {
                        actionId = candidate.ActionId,
                        kind = candidate.Kind,
                        payloadFingerprint = candidate.PayloadFingerprint
                    }).ToArray()
                };
            }
            if (state.IntentPending is { } intent)
            {
                var semanticEvent = SemanticPilotCombatEngineV111
                    .DescribePendingIntentEvent(state);
                var support = semanticEvent.Support;
                var chanceLaw = ChanceLawEngineV111.QueryIntent(semanticEvent);
                return new
                {
                    kind = "enemy_intent",
                    siteId = intent.SiteId,
                    streamId = intent.StreamId,
                    ownerId = intent.OwnerId,
                    ordinal = intent.Ordinal,
                    operationKind = intent.OperationKind,
                    supportCount = intent.SupportCount,
                    eligibleIndices = Enumerable.Range(0, intent.SupportCount),
                    semanticIntentSupportSchemaId = SemanticIntentSupportV111.SchemaId,
                    semanticIntentEventSchemaId =
                        SemanticIntentEventV111.EventSchemaId,
                    semanticEvent,
                    chanceLaw,
                    eventId = semanticEvent.EventId,
                    semanticCandidates =
                        support.Candidates.Select(candidate => new
                        {
                            candidate.IntentId,
                            candidate.MoveId,
                            candidate.MoveIndex,
                            candidate.RawWeight,
                            candidate.TicketStart,
                            normalizedWeight = (double)candidate.RawWeight /
                                support.TotalRawWeight
                        }).ToArray(),
                    rawEligibleWeights = support.Candidates
                        .Select(candidate => candidate.RawWeight).ToArray(),
                    totalRawWeight = support.TotalRawWeight,
                    semanticSupportFingerprint = CanonicalJson.Sha256(support),
                    weightsVerified = false,
                    probabilityStatus = "UNVERIFIED",
                    probabilityProvenance = "source-shaped-unverified",
                    supportFingerprint = CanonicalJson.Sha256(intent),
                    sourceScope = "source_filtered_semantic_eligible_moves"
                };
            }
            if (state.EnemyActionEventPending is { } enemyActionEvent)
            {
                var enemyAction = enemyActionEvent.Request;
                var chanceLaw = ChanceLawEngineV111.QueryEndTurnIndex(
                    enemyActionEvent);
                return new
                {
                    kind = "enemy_action_chance",
                    schemaId = enemyActionEvent.SchemaId,
                    eventId = enemyActionEvent.EventId,
                    boundaryFingerprint =
                        enemyActionEvent.BoundaryFingerprint,
                    siteId = enemyAction.SiteId,
                    streamId = enemyAction.StreamId,
                    ownerId = enemyAction.OwnerId,
                    ordinal = enemyAction.Ordinal,
                    operationKind = enemyAction.OperationKind,
                    supportCount = enemyAction.SupportCount,
                    eligibleIndices = Enumerable.Range(
                        0, enemyAction.SupportCount).ToArray(),
                    supportFingerprint =
                        enemyActionEvent.SupportFingerprint,
                    chanceLaw,
                    probabilityStatus = chanceLaw.ProbabilityStatus
                };
            }
            if (state.RandomPileCardPending is { } pileCard)
            {
                var chanceLaw = ChanceLawEngineV111
                    .QueryRandomPileCard(pileCard);
                return new
                {
                    kind = "random_pile_card",
                    schemaId = pileCard.SchemaId,
                    eventId = pileCard.EventId,
                    siteId = pileCard.SiteId,
                    streamId = pileCard.StreamId,
                    ownerId = pileCard.OwnerId,
                    ordinal = pileCard.Ordinal,
                    operationKind = pileCard.OperationKind,
                    supportFingerprint = pileCard.SupportFingerprint,
                    chanceLaw,
                    probabilityStatus = pileCard.ProbabilityStatus,
                    candidates = pileCard.CandidateCardInstanceIds
                        .Select((id, index) => new
                        {
                            index,
                            cardInstanceId = id,
                            cardModelId = pileCard.CandidateCardModelIds[index]
                        }).ToArray()
                };
            }
            if (state.DrawPending is { } drawn)
            {
                var combat = state.Run.World?.Combat ??
                    throw new InvalidDataException("Draw event missing its combat.");
                var chanceLaw = ChanceLawEngineV111.QueryDraw(
                    combat.SemanticDeck ?? throw new InvalidDataException(
                        "Draw event missing its provider-owned deck."));
                return new
                {
                    kind = "draw_card",
                    schemaId = drawn.SchemaId,
                    eventId = drawn.EventId,
                    siteId = drawn.SiteId,
                    streamId = drawn.StreamId,
                    ownerId = drawn.OwnerId,
                    ordinal = drawn.Ordinal,
                    shuffleEpoch = combat.SemanticDeck!.ShuffleEpoch,
                    topOffset = drawn.TopOffset,
                    operationKind = drawn.OperationKind,
                    supportFingerprint = drawn.SupportFingerprint,
                    constraintFingerprint = combat.SemanticDeck.Fingerprint,
                    chanceLaw,
                    probabilityStatus = drawn.ProbabilityStatus,
                    candidates = drawn.CandidateCardInstanceIds
                        .Select((id, index) => new
                        {
                            index,
                            cardInstanceId = id,
                            cardModelId = combat.Cards.Single(c =>
                                c.InstanceId == id).CardId
                        }).ToArray()
                };
            }
            if (state.DeckRevealPending is { } reveal)
            {
                var combat = state.Run.World?.Combat ??
                    throw new InvalidDataException(
                        "Deck-reveal event missing its combat.");
                var deck = combat.SemanticDeck ??
                    throw new InvalidDataException(
                        "Deck-reveal event missing its provider-owned deck.");
                var chanceLaw = ChanceLawEngineV111.QueryDeckReveal(deck);
                return new
                {
                    kind = "deck_reveal",
                    schemaId = SemanticDeckRevealDecisionV111.CurrentSchemaId,
                    eventId = reveal.EventId,
                    siteId = reveal.SiteId,
                    purpose = reveal.Purpose,
                    streamId = reveal.StreamId,
                    ownerId = reveal.OwnerId,
                    ordinal = reveal.Ordinal,
                    topOffset = reveal.TopOffset,
                    operationKind = reveal.OperationKind,
                    supportFingerprint = reveal.SupportFingerprint,
                    constraintFingerprint = deck.Fingerprint,
                    chanceLaw,
                    probabilityStatus = reveal.ProbabilityStatus,
                    candidates = reveal.CandidateCardInstanceIds
                        .Select((id, index) => new
                        {
                            index,
                            cardInstanceId = id,
                            cardModelId = combat.Cards.Single(c =>
                                c.InstanceId == id).CardId
                        }).ToArray()
                };
            }
            if (state.TargetPending is { } target)
            {
                var chanceLaw = ChanceLawEngineV111.QueryTarget(target);
                return new
                {
                    kind = "random_target",
                    schemaId = target.SchemaId,
                    eventId = target.EventId,
                    siteId = target.SiteId,
                    streamId = target.StreamId,
                    ownerId = target.OwnerId,
                    ordinal = target.Ordinal,
                    targetKind = target.TargetKind,
                    operationKind = target.OperationKind,
                    supportFingerprint = target.SupportFingerprint,
                    chanceLaw,
                    probabilityStatus = target.ProbabilityStatus,
                    candidates = target.CandidateEnemyInstanceIds
                        .Select((id, index) => new
                        {
                            index,
                            enemyInstanceId = id
                        }).ToArray()
                };
            }
            if (state.GeneratedCardPending is { } generated)
            {
                var chanceLaw = ChanceLawEngineV111
                    .QueryGeneratedCard(generated);
                return new
                {
                    kind = "generated_card",
                    schemaId = generated.SchemaId,
                    eventId = generated.EventId,
                    siteId = generated.SiteId,
                    streamId = generated.StreamId,
                    ownerId = generated.OwnerId,
                    ordinal = generated.Ordinal,
                    operationKind = generated.OperationKind,
                    supportFingerprint = generated.SupportFingerprint,
                    chanceLaw,
                    probabilityStatus = generated.ProbabilityStatus,
                    candidates = generated.CandidateCardIds
                        .Select((id, index) => new
                        {
                            index,
                            cardModelId = id
                        }).ToArray()
                };
            }
            if (state.PotionGenerationPending is { } potionGeneration)
            {
                var chanceLaw = ChanceLawEngineV111
                    .QueryPotionGeneration(potionGeneration);
                return new
                {
                    kind = "potion_generation",
                    schemaId = potionGeneration.SchemaId,
                    eventId = potionGeneration.EventId,
                    siteId = potionGeneration.SiteId,
                    streamId = potionGeneration.StreamId,
                    ownerId = potionGeneration.OwnerId,
                    ordinal = potionGeneration.Ordinal,
                    operationKind = potionGeneration.OperationKind,
                    supportFingerprint = potionGeneration.SupportFingerprint,
                    chanceLaw,
                    probabilityStatus = potionGeneration.ProbabilityStatus,
                    candidates = potionGeneration.CandidatePotionIds
                        .Select((id, index) => new
                        {
                            index,
                            potionModelId = id
                        }).ToArray()
                };
            }
            if (state.CardTransformPending is { } cardTransform)
            {
                var chanceLaw = ChanceLawEngineV111
                    .QueryCardTransform(cardTransform);
                return new
                {
                    kind = "card_transform",
                    schemaId = cardTransform.SchemaId,
                    eventId = cardTransform.EventId,
                    siteId = cardTransform.SiteId,
                    streamId = cardTransform.StreamId,
                    ownerId = cardTransform.OwnerId,
                    ordinal = cardTransform.Ordinal,
                    operationKind = cardTransform.OperationKind,
                    supportFingerprint = cardTransform.SupportFingerprint,
                    chanceLaw,
                    sourceCardInstanceId = cardTransform.SourceCardInstanceId,
                    originalCardId = cardTransform.OriginalCardId,
                    probabilityStatus = cardTransform.ProbabilityStatus,
                    candidates = cardTransform.CandidateCardIds
                        .Select((id, index) => new
                        {
                            index,
                            cardModelId = id
                        }).ToArray()
                };
            }
            if (state.CardSelectionPending is { } selection)
            {
                return new
                {
                    kind = "player_card_selection",
                    schemaId = selection.SchemaId,
                    eventId = selection.EventId,
                    supportFingerprint = selection.SupportFingerprint,
                    sourceZone = selection.SourceZone,
                    resolution = selection.Resolution,
                    sourceCardInstanceId = selection.SourceCardInstanceId,
                    minimumSelections = selection.MinimumSelections,
                    maximumSelections = selection.MaximumSelections,
                    candidates = selection.CandidateCardInstanceIds
                        .Select((id, index) => new
                        {
                            cardInstanceId = id,
                            cardModelId = selection.CandidateCardModelIds[index]
                        }).ToArray(),
                    actions = selection.Candidates.Select(candidate => new
                    {
                        actionId = candidate.ActionId,
                        cardInstanceIds = candidate.CardInstanceIds
                    }).ToArray()
                };
            }
            if (state.HandCostSelectionPending is { } handCost)
            {
                var chanceLaw = ChanceLawEngineV111
                    .QueryHandCostSelection(handCost);
                return new
                {
                    kind = "random_hand_card_selection",
                    schemaId = handCost.SchemaId,
                    eventId = handCost.EventId,
                    siteId = handCost.SiteId,
                    streamId = handCost.StreamId,
                    ownerId = handCost.OwnerId,
                    ordinal = handCost.Ordinal,
                    operationKind = handCost.OperationKind,
                    supportFingerprint = handCost.SupportFingerprint,
                    chanceLaw,
                    appliedCost = handCost.AppliedCost,
                    probabilityStatus = handCost.ProbabilityStatus,
                    candidates = handCost.CandidateCardInstanceIds
                        .Select((id, index) => new
                        {
                            index,
                            cardInstanceId = id,
                            cardModelId = handCost.CandidateCardModelIds[index]
                        }).ToArray()
                };
            }
            if (state.CardShortlistPending is { } shortlist)
            {
                var chanceLaw = ChanceLawEngineV111
                    .QueryCardShortlist(shortlist);
                return new
                {
                    kind = shortlist.SiteId switch
                    {
                        "combat.autoplay.random_card.select" =>
                            "random_auto_play_card",
                        "combat.draw_pile.random_replay_card.select" =>
                            "random_replay_card",
                        _ => "random_pile_selection_shortlist"
                    },
                    schemaId = shortlist.SchemaId,
                    eventId = shortlist.EventId,
                    siteId = shortlist.SiteId,
                    streamId = shortlist.StreamId,
                    ownerId = shortlist.OwnerId,
                    ordinal = shortlist.Ordinal,
                    operationKind = shortlist.OperationKind,
                    supportFingerprint = shortlist.SupportFingerprint,
                    chanceLaw,
                    requiredCount = shortlist.RequiredCount,
                    eligibleCards = shortlist.EligibleCardInstanceIds
                        .Select((id, index) => new
                        {
                            cardInstanceId = id,
                            cardModelId = shortlist.EligibleCardModelIds[index]
                        }).ToArray(),
                    selectedCardInstanceIds =
                        shortlist.SelectedCardInstanceIds,
                    candidates = shortlist.CandidateCardInstanceIds
                        .Select((id, index) => new
                        {
                            index,
                            cardInstanceId = id,
                            cardModelId = shortlist.CandidateCardModelIds[index]
                        }).ToArray(),
                    probabilityStatus = shortlist.ProbabilityStatus
                };
            }
            if (state.CostPending is { } cost)
            {
                var chanceLaw = ChanceLawEngineV111.QueryCost(cost);
                return new
                {
                    kind = cost.TargetCardInstanceId is null
                        ? "drawn_card_cost" : "random_hand_card_cost",
                    schemaId = cost.SchemaId,
                    eventId = cost.EventId,
                    siteId = cost.Request.SiteId,
                    streamId = cost.Request.StreamId,
                    ownerId = cost.Request.OwnerId,
                    ordinal = cost.Request.Ordinal,
                    operationKind = cost.Request.OperationKind,
                    supportFingerprint = cost.SupportFingerprint,
                    chanceLaw,
                    probabilityStatus = cost.ProbabilityStatus,
                    targetCardInstanceId = cost.TargetCardInstanceId,
                    targetCardModelId = cost.TargetCardModelId,
                    candidates = cost.CandidateCosts.Select((value, index) =>
                        new { index, cost = value }).ToArray()
                };
            }
            if (state.PoolShufflePending is { } pool)
            {
                var chanceLaw = ChanceLawEngineV111.QueryPoolShuffle(pool);
                return new
                {
                    kind = pool.SiteId ==
                        "combat.draw_pile.random_rare_to_hand.shuffle_swap_index"
                            ? "random_rare_to_hand_shuffle"
                            : pool.SiteId ==
                                "combat.hand_return.draw_pile.shuffle_swap_index"
                                ? "hand_return_shuffle"
                            : "generated_card_pool_shuffle",
                    schemaId = pool.SchemaId,
                    eventId = pool.EventId,
                    siteId = pool.SiteId,
                    streamId = pool.StreamId,
                    ownerId = pool.OwnerId,
                    ordinal = pool.Ordinal,
                    operationKind = pool.OperationKind,
                    sourceOperationKind = pool.SourceOperationKind,
                    supportFingerprint = pool.SupportFingerprint,
                    chanceLaw,
                    nextSwapIndex = pool.NextSwapIndex,
                    selectedCount = pool.SelectedCount,
                    sourceCardInstanceId = pool.SourceCardInstanceId,
                    originalCardIds = pool.OriginalCardIds,
                    currentCardIds = pool.CurrentCardIds,
                    candidates = Enumerable.Range(0, pool.Request.SupportCount)
                        .Select(index => new { index, swapIndex = index })
                        .ToArray(),
                    probabilityStatus = pool.ProbabilityStatus
                };
            }
            if (state.DrawInsertionPending is { } insertion)
            {
                var chanceLaw = ChanceLawEngineV111
                    .QueryDrawInsertion(insertion);
                return new
                {
                    kind = "generated_draw_pile_insertion",
                    schemaId = insertion.SchemaId,
                    eventId = insertion.EventId,
                    siteId = insertion.SiteId,
                    streamId = insertion.StreamId,
                    ownerId = insertion.OwnerId,
                    ordinal = insertion.Ordinal,
                    operationKind = insertion.OperationKind,
                    supportFingerprint = insertion.SupportFingerprint,
                    chanceLaw,
                    generatedCardModelId = insertion.GeneratedCardModelId,
                    eligibleDrawPileInstanceIds =
                        insertion.EligibleDrawPileInstanceIds,
                    candidates = Enumerable.Range(
                            0, insertion.Request.SupportCount)
                        .Select(position => new { position })
                        .ToArray(),
                    probabilityStatus = insertion.ProbabilityStatus
                };
            }
            return null;
        }

        SemanticNatureChoiceViewV111? CreateNatureChoiceView(
            object? pending)
        {
            if (pending is null) return null;
            var payload = JsonSerializer.SerializeToElement(
                pending, pending.GetType(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (!payload.TryGetProperty("chanceLaw", out _)) return null;
            return SemanticNatureChoiceViewV111.Create(payload);
        }

        SemanticNatureChoiceViewV111? DescribeSemanticNatureChoice(
            SemanticPilotCombatV111 state) =>
            state.UsesNativePolicyBaseline
                ? null
                : CreateNatureChoiceView(DescribeSemanticPending(state));

        (string Fingerprint, SemanticNatureChoiceViewV111? Choice)
            DescribeNatureChoiceStatus(string sessionKind, string handle)
        {
            object status = sessionKind switch
            {
                "semantic_combat" when semanticStates.TryGetValue(
                    handle, out var semantic) => SemanticResult(semantic, handle),
                "reference_combat" when referenceNatureStates.TryGetValue(
                    handle, out var combatSession) =>
                    ReferenceNatureResult(combatSession, handle),
                "run_action" when referenceNatureRunActionStates.TryGetValue(
                    handle, out var runActionSession) =>
                    ReferenceNatureRunActionResult(runActionSession, handle),
                "event_choice" when referenceNatureEventChoiceStates.TryGetValue(
                    handle, out var eventChoiceSession) =>
                    ReferenceNatureEventChoiceResult(eventChoiceSession, handle),
                "ancient_offer" when referenceNatureAncientOfferStates.TryGetValue(
                    handle, out var ancientOfferSession) =>
                    ReferenceNatureAncientOfferResult(ancientOfferSession, handle),
                "merchant" when referenceNatureMerchantStates.TryGetValue(
                    handle, out var merchantSession) =>
                    ReferenceNatureMerchantResult(merchantSession, handle),
                "treasure" when referenceNatureTreasureStates.TryGetValue(
                    handle, out var treasureSession) =>
                    ReferenceNatureTreasureResult(treasureSession, handle),
                "controlled_event_choice" when states.TryGetValue(
                    handle, out var eventRun) =>
                    ControlledEventChoiceResult(eventRun, handle, null),
                "controlled_run_action" when states.TryGetValue(
                    handle, out var actionRun) =>
                    ControlledRunActionResult(actionRun, handle, null),
                "semantic_combat" or "reference_combat" or "run_action" or
                    "event_choice" or "ancient_offer" or "merchant" or
                    "treasure" or "controlled_event_choice" or
                    "controlled_run_action" => throw new InvalidOperationException(
                        "Unknown Nature session handle for kind " + sessionKind +
                        ": " + handle),
                _ => throw new ArgumentException(
                    "Unsupported Nature session kind: " + sessionKind)
            };
            var result = JsonSerializer.SerializeToElement(
                status, status.GetType(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            string fingerprint = RequiredString(result, "fingerprint");
            var pending = result.TryGetProperty("pending", out var pendingValue) &&
                pendingValue.ValueKind == JsonValueKind.Object
                    ? pendingValue
                    : default;
            var choice = pending.ValueKind == JsonValueKind.Object &&
                pending.TryGetProperty("chanceLaw", out _)
                    ? SemanticNatureChoiceViewV111.Create(pending)
                    : null;
            return (fingerprint, choice);
        }

        string[] SemanticChanceSites(SemanticPilotCombatV111 state)
        {
            if (state.UsesNativePolicyBaseline) return [];
            var sites = new List<string> { "enemy_intent", "draw_card" };
            if (state.DeckRevealPending is not null)
                sites.Add("deck_reveal");
            if (state.RandomPileCardPending is not null ||
                state.Run.Player.ActiveRelics.Any(relic =>
                    relic.RelicId == "proto.relic.jeweled_mask"))
                sites.Add("random_pile_card");
            if (state.SeededEntry?.FixtureRandomTargetCardId is not null)
                sites.Add("random_target");
            if (state.SeededEntry?.FixtureGenerationPotionId is not null)
                sites.Add("generated_card");
            if (state.SeededEntry?.FixturePotionGenerationCardId is not null ||
                state.PotionGenerationPending is not null)
                sites.Add("random_potion_generation");
            if (state.CardTransformPending is not null)
                sites.Add("random_card_transform");
            if (state.SeededEntry?.FixtureNatureDrawCosts == true)
                sites.Add("drawn_card_cost");
            if (state.SeededEntry?.FixturePoolGenerationPotionId is not null ||
                state.PoolShufflePending?.SiteId ==
                    "combat.generation.full_pool_shuffle.swap_index")
                sites.Add("generated_card_pool_shuffle");
            if (state.Run.Player.PotionSlots.Any(potion =>
                    potion?.PotionId == "proto.potion.bottled_potential") ||
                state.PoolShufflePending?.SiteId ==
                    "combat.hand_return.draw_pile.shuffle_swap_index")
                sites.Add("hand_return_shuffle");
            if (state.EnemyActionPending is not null)
                sites.Add("enemy_action_chance");
            if (state.SeededEntry?.FixtureSelectionCardId is
                    "proto.colorless.beat_down" or
                    "proto.colorless.catastrophe" ||
                state.CardShortlistPending?.SiteId ==
                    "combat.autoplay.random_card.select")
                sites.Add("random_auto_play_card");
            if (state.SeededEntry?.FixtureSelectionCardId ==
                    "proto.colorless.hidden_gem" ||
                state.CardShortlistPending?.SiteId ==
                    "combat.draw_pile.random_replay_card.select")
                sites.Add("random_replay_card");
            if (state.SeededEntry?.FixtureSelectionCardId ==
                    "proto.colorless.anointed" ||
                state.PoolShufflePending?.SiteId ==
                    "combat.draw_pile.random_rare_to_hand.shuffle_swap_index")
                sites.Add("random_rare_to_hand_shuffle");
            if (state.SeededEntry?.FixtureGeneratedDrawInsertionCardId is not null)
                sites.Add("generated_draw_pile_insertion");
            if (state.DrawInsertionPending?.SiteId ==
                    PrototypeControlledDrawInsertionV111
                        .PersonalHiveDazedSiteId &&
                !sites.Contains("generated_draw_pile_insertion"))
                sites.Add("generated_draw_pile_insertion");
            if (state.SeededEntry?.FixtureRandomFreeAttackDrawInsertion == true)
            {
                sites.Add("random_draw_attack_card_model");
                sites.Add("generated_draw_pile_insertion");
            }
            if ((state.Run.World?.Combat?.PendingChoice?
                    .ControlledDrawInsertion?.AfterShuffleRelic is not null ||
                 state.Run.Player.ActiveRelics.Any(relic =>
                     PrototypeContent.Relic(relic.RelicId)
                         .AfterShuffleGeneratedCardId is not null)) &&
                !sites.Contains("generated_draw_pile_insertion"))
                sites.Add("generated_draw_pile_insertion");
            if (state.SeededEntry?.FixtureRandomizeHandCostsMaxExclusive is not null)
                sites.Add("random_hand_card_energy_cost");
            if (state.SeededEntry?.FixtureSetRandomHandCardCost is not null)
                sites.Add("random_hand_card_select");
            if (state.SeededEntry?.FixtureSelectionCardId ==
                "proto.colorless.seeker_strike")
                sites.Add("random_pile_selection_shortlist");
            if (state.CardShortlistPending is not null &&
                !sites.Contains("random_pile_selection_shortlist"))
                sites.Add("random_pile_selection_shortlist");
            if (state.HandCostSelectionPending is not null &&
                !sites.Contains("random_hand_card_select"))
                sites.Add("random_hand_card_select");
            if (state.TargetPending is not null &&
                !sites.Contains("random_target"))
                sites.Add("random_target");
            if (state.GeneratedCardPending is not null &&
                !sites.Contains("generated_card"))
                sites.Add("generated_card");
            if (state.PotionGenerationPending is not null &&
                !sites.Contains("random_potion_generation"))
                sites.Add("random_potion_generation");
            if (state.CardTransformPending is not null &&
                !sites.Contains("random_card_transform"))
                sites.Add("random_card_transform");
            if (state.CostPending is
                    { TargetCardInstanceId: null } &&
                !sites.Contains("drawn_card_cost"))
                sites.Add("drawn_card_cost");
            if (state.CostPending?.TargetCardInstanceId is not null &&
                !sites.Contains("random_hand_card_energy_cost"))
                sites.Add("random_hand_card_energy_cost");
            return sites.ToArray();
        }

        string SemanticControllerMode(SemanticPilotCombatV111 state) =>
            state.UsesNativePolicyBaseline
                ? "engine-owned-native-v111-policy-baseline"
                : state.SeededEntry?.FixtureRandomTargetCardId is not null ||
                  state.SeededEntry?.FixtureGenerationPotionId is not null ||
                  state.SeededEntry?.FixturePotionGenerationCardId is not null ||
                  state.SeededEntry?.FixtureSelectionCardId is not null ||
                  state.SeededEntry?.FixtureNatureDrawCosts == true ||
                  state.SeededEntry?.FixturePoolGenerationPotionId is not null ||
                  state.SeededEntry?.FixtureGeneratedDrawInsertionCardId is not null ||
                  state.SeededEntry?.FixtureRandomFreeAttackDrawInsertion == true ||
                  state.DeckRevealPending is not null ||
                  state.DrawInsertionPending?.SiteId ==
                    PrototypeControlledDrawInsertionV111
                        .PersonalHiveDazedSiteId ||
                  state.SeededEntry?.FixtureRandomizeHandCostsMaxExclusive is not null ||
                  state.SeededEntry?.FixtureSetRandomHandCardCost is not null ||
                  state.EnemyActionPending is not null ||
                  state.RandomPileCardPending is not null ||
                  state.Run.Player.ActiveRelics.Any(relic =>
                      relic.RelicId == "proto.relic.jeweled_mask") ||
                  state.Run.World?.Combat?.PendingChoice?
                      .ControlledDrawInsertion?.AfterShuffleRelic is not null ||
                  state.Run.Player.ActiveRelics.Any(relic =>
                      PrototypeContent.Relic(relic.RelicId)
                          .AfterShuffleGeneratedCardId is not null) ||
                  state.CardShortlistPending is not null ||
                  state.HandCostSelectionPending is not null ||
                  state.CostPending?.TargetCardInstanceId is not null
                    ? "nature-extended-fixture"
                    : "nature-two-site";

        object SemanticResult(
            SemanticPilotCombatV111 state, string handle,
            string? parent = null)
        {
            return new
            {
                ok = true, schemaId = SemanticChanceSchemaId,
                semanticHandle = handle, parent,
                fingerprint = state.Fingerprint,
                sourceOpeningFingerprint = state.SourceOpeningFingerprint,
                sourceRouteProvider = state.SourceRouteAudit?.ProviderId,
                sourceRouteActionCount = state.SourceRouteAudit?.ActionIds.Length ?? 0,
                sourceRouteEncounterId = state.SourceRouteAudit?.EncounterId,
                sourceRouteNativeEntry = state.SourceRouteAudit?.NativeEncounterEntry,
                sourceRouteProbabilityStatus = state.SourceRouteAudit?.ProbabilityStatus,
                boundary = state.CombatTerminal ? "terminal" :
                    state.AtPlayerBoundary ? "player" :
                    state.AtPlayerChoiceBoundary ? "player_choice" : "nature",
                pending = DescribeSemanticPending(state),
                terminal = state.CombatTerminal,
                outcome = state.CombatOutcome,
                exitHp = state.CombatTerminal ? state.Run.Player.Hp : (int?)null,
                chanceSites = SemanticChanceSites(state),
                controllerMode = SemanticControllerMode(state),
                nativePolicyProvider = state.UsesNativePolicyBaseline
                    ? SemanticNativePolicyAuditV111.NativeProviderId
                    : null,
                nativePolicyDecisionCount = state.NativePolicyAudit?.Decisions.Length ?? 0,
                nativePolicyShuffleCount = state.NativePolicyAudit?.Decisions.Count(
                    decision => decision.Request.SiteId ==
                        NativeFisherYatesV111.SiteId) ?? 0,
                otherChanceProvider = state.UsesNativePolicyBaseline
                    ? null
                    : state.OtherChance?.ProviderId ??
                        "engine-owned-deterministic-unverified-v1",
                otherChanceDecisionCount = state.OtherChance?.Decisions.Length ?? 0,
                rewardProvider = state.RewardAudit?.ProviderId,
                rewardDrawCount = state.RewardAudit?.Calls,
                sourceOpeningProvider = state.SeededEntry is null
                    ? "precommitted-fixture-v1"
                    : state.SeededEntry.ReplayOpeningDrawHooks
                        ? "engine-owned-seeded-source-shaped-draw-hooks-unverified-v1"
                        : "engine-owned-seeded-source-shaped-unverified-v1",
                probabilityStatus = "UNVERIFIED"
            };
        }

        object ControlledEventChoiceResult(
            RunState state, string handle, string? parent)
        {
            var continuation = state.World?.Event?.ControlledChoice;
            var rewardContinuation = state.ControlledEventRewardChoice;
            var stateFingerprint = CanonicalJson.Sha256(state);
            if (continuation is not null && rewardContinuation is not null)
                throw new InvalidDataException(
                    "A state cannot suspend both an event effect and its reward.");
            var isPending = continuation is not null
                || rewardContinuation is not null;
            var frame = environment.Observe(state);
            var actions = !isPending
                ? frame.LegalActions
                : new PrototypeGameEngine().GetLegalActions(state)
                    .Select(action => new PrototypeAiAction(
                        PrototypeAiEnvironment.StableActionId(action),
                        action.Kind,
                        action.Payload.Clone()))
                    .ToArray();
            var history = state.ControlledEventChoiceHistory;
            RunState? parentState = null;
            if (parent is not null
                && states.TryGetValue(parent, out var foundParent))
                parentState = foundParent;
            var parentHistory = parentState?.ControlledEventChoiceHistory;
            var changedTraceIndex = -1;
            if (history is not null)
            {
                for (var index = 0; index < history.Length; index++)
                {
                    if (parentHistory is null
                        || index >= parentHistory.Length
                        || CanonicalJson.Sha256(history[index]) !=
                            CanonicalJson.Sha256(parentHistory[index]))
                        changedTraceIndex = index;
                }
            }
            var completedTrace = changedTraceIndex >= 0
                ? history![changedTraceIndex]
                : null;
            object? transcript = null;
            if (continuation is not null)
                transcript = new
                {
                    eventId = continuation.EventId,
                    choiceId = continuation.ChoiceId,
                    decisions = continuation.Decisions,
                    candidateLabels = continuation.DecisionCandidateLabels,
                    probabilityStatus = "UNVERIFIED"
                };
            if (rewardContinuation is not null)
                transcript = new
                {
                    eventId = rewardContinuation.EventId,
                    choiceId = rewardContinuation.ChoiceId,
                    decisions = rewardContinuation.Decisions,
                    candidateLabels = rewardContinuation.DecisionCandidateLabels,
                    probabilityStatus = "UNVERIFIED"
                };
            if (!isPending)
                transcript = completedTrace;
            object? pending = continuation is not null
                ? (object)new
                {
                    kind = "event_random_choice",
                    eventId = continuation.EventId,
                    choiceId = continuation.ChoiceId,
                    boundaryFingerprint = CanonicalJson.Sha256(new
                    {
                        schemaId = ControlledEventChoiceEngineV111.SchemaId,
                        continuation.EventId,
                        continuation.ChoiceId,
                        continuation.NextEffectIndex,
                        request = continuation.PendingRequest,
                        candidateLabels = continuation.PendingCandidateLabels
                    }),
                    request = continuation.PendingRequest,
                    candidateLabels = continuation.PendingCandidateLabels,
                    chanceLaw = ChanceLawEngineV111.QueryRandomIndex(
                        continuation.EventId, continuation.PendingRequest),
                    probabilityStatus = "UNVERIFIED"
                }
                : rewardContinuation is not null
                    ? (object)new
                    {
                        kind = "event_reward_random_choice",
                        eventId = rewardContinuation.EventId,
                        choiceId = rewardContinuation.ChoiceId,
                        boundaryFingerprint = CanonicalJson.Sha256(new
                        {
                            schemaId = ControlledEventChoiceEngineV111.SchemaId,
                            rewardContinuation.EventId,
                            rewardContinuation.ChoiceId,
                            rewardContinuation.DecisionPrefixLength,
                            request = rewardContinuation.PendingRequest,
                            candidateLabels = rewardContinuation.PendingCandidateLabels
                        }),
                        request = rewardContinuation.PendingRequest,
                        candidateLabels = rewardContinuation.PendingCandidateLabels,
                        chanceLaw = ChanceLawEngineV111.QueryRandomIndex(
                            rewardContinuation.EventId,
                            rewardContinuation.PendingRequest),
                        probabilityStatus = "UNVERIFIED"
                    }
                    : null;
            return new
            {
                ok = true,
                schemaId = ControlledEventChoiceEngineV111.SchemaId,
                stateHandle = handle,
                parent,
                fingerprint = stateFingerprint,
                boundary = isPending ? "nature" : "committed",
                pending,
                transcript,
                actions = actions.Select(ActionWire).ToArray()
            };
        }

        object ControlledRunActionResult(RunState state, string handle,
            string? parent)
        {
            var continuation = state.ControlledRunActionChoice;
            var stateFingerprint = CanonicalJson.Sha256(state);
            var boundaryFingerprint = continuation is null ? null :
                ChanceLawEngineV111.ControlledRunActionBoundaryFingerprint(state);
            var chanceLaw = continuation is null
                ? null : ChanceLawEngineV111.QueryControlledRunAction(state);
            var actions = continuation is null
                ? environment.Observe(state).LegalActions
                : new PrototypeGameEngine().GetLegalActions(state)
                    .Select(action => new PrototypeAiAction(
                        PrototypeAiEnvironment.StableActionId(action),
                        action.Kind,
                        action.Payload.Clone()))
                    .ToArray();
            object? transcript = continuation is not null
                ? new
                {
                    actionId = continuation.ActionId,
                    decisions = continuation.Decisions,
                    candidateLabels = continuation.DecisionCandidateLabels,
                    probabilityStatus = "UNVERIFIED"
                }
                : state.ControlledRunActionHistory is { Length: > 0 } history
                    ? history[^1]
                    : null;
            object? pending = continuation is null
                ? null
                : new
                {
                    kind = "run_action_random_choice",
                    eventId = chanceLaw!.EventId,
                    boundaryFingerprint,
                    actionId = continuation.ActionId,
                    request = continuation.PendingRequest,
                    candidateLabels = continuation.PendingCandidateLabels,
                    chanceLaw,
                    probabilityStatus = "UNVERIFIED"
                };
            return new
            {
                ok = true,
                schemaId = ControlledRunActionEngineV111.SchemaId,
                stateHandle = handle,
                parent,
                fingerprint = stateFingerprint,
                boundary = continuation is null ? "committed" : "nature",
                pending,
                transcript,
                actions = actions.Select(ActionWire).ToArray()
            };
        }

        string StoreControlled(ControlledPlayableCombatV111 state)
        {
            string handle = "c" + nextControlledHandle++;
            controlledStates.Add(handle, state.Fork());
            return handle;
        }

        ControlledPlayableCombatV111 RequireControlled(JsonElement request)
        {
            var handle = RequiredString(request, "controlled_handle");
            return controlledStates.TryGetValue(handle, out var value)
                ? value
                : throw new InvalidOperationException(
                    "Unknown controlled handle: " + handle);
        }

        string StoreNature(ControlledInitialHpStateV111 state)
        {
            string handle = "n" + nextNatureHandle++;
            natureHp.Add(handle, state.Fork());
            return handle;
        }

        string StoreNatureIntent(ControlledIntentStateV111 state)
        {
            string handle = "n" + nextNatureHandle++;
            natureIntents.Add(handle, state.Fork());
            return handle;
        }




        string Store(
            RunState state,
            string? parentHandle = null,
            bool rootHypothetical = false)
        {
            var handle = $"s{nextHandle++}";
            if (rootHypothetical
                || (parentHandle is not null && hypotheticalHandles.Contains(parentHandle)))
            {
                hypotheticalHandles.Add(handle);
            }
            if (parentHandle is not null)
            {
                var parent = states[parentHandle];
                if (state.Phase == RunPhase.Combat
                    && (parent.Phase != RunPhase.Combat
                        || (freshCombatHandles.Contains(parentHandle)
                            && parent.DecisionIndex == state.DecisionIndex)))
                {
                    freshCombatHandles.Add(handle);
                }
            }
            states.Add(handle, state);
            return handle;
        }

        RunState RequireState(JsonElement request)
        {
            var handle = RequiredString(request, "state_handle");
            return states.TryGetValue(handle, out var state)
                ? state
                : throw new InvalidOperationException($"Unknown state handle '{handle}'.");
        }

        void Write(object response)
        {
            output.WriteLine(JsonSerializer.Serialize(
                response,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            output.Flush();
        }

        string? line;
        while ((line = input.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string? requestId = null;
            try
            {
                using var document = JsonDocument.Parse(line);
                var request = document.RootElement;
                requestId = OptionalString(request, "request_id");
                var operation = RequiredString(request, "op");

                switch (operation)
                {
                    case "hello":
                        Write(new
                        {
                            requestId,
                            ok = true,
                            wireSchemaId = WireSchemaId,
                            aiSchemaId = PrototypeAiEnvironment.SchemaId,
                            rulesetId = PrototypeContent.RulesetId,
                            fairPolicyId = FairPolicyId,
                            hypotheticalDrawOrderId = PrototypeHypotheticalDrawOrder.SchemaId,
                            conditionalCombatEntryId = PrototypeConditionedCombatEntry.SchemaId,
                            factorizedInitialStreamsId = PrototypeFactorizedRunFactory.SchemaId,
                            pristineRewardProposalId = PrototypePristineRewardProposal.SchemaId,
                            nativeOvergrowthResetId = NativeOvergrowthResetId,
                            publicEnemyIntentId = PublicEnemyIntentId,
                            natureBoundarySchemaId = NatureBoundarySchemaId,
                            natureBoundaryScope = "isolated-combat-entry-only",
                            natureEventMetadataSchemaId = NatureBoundaryMetadataV111.SchemaId,
                            naturePlayable = false,
                            controlledChanceSchemaId = ControlledChanceSchemaId,
                            semanticChanceSchemaId = SemanticChanceSchemaId,
                            stochasticCapabilities =
                                StochasticCapabilitiesV111.Create(),
                            stochasticSessionCoverage =
                                StochasticSiteRegistryV111.CreateSessionCoverage(),
                            stochasticChanceLawQueryCoverage =
                                StochasticSiteRegistryV111
                                    .CreateChanceLawQueryCoverage(),
                            referenceNatureStateSnapshotSchemaId =
                                RunSnapshot.ReferenceNatureSchemaId,
                            natureRelicBagSnapshotSchemaId =
                                RunSnapshot.NatureRelicBagOrderSchemaId,
                            referenceNatureSessionSchemaId =
                                ReferenceNatureSessionV111.CurrentSchemaId,
                            referenceNatureRunActionSessionSchemaId =
                                ReferenceNatureRunActionSessionV111.CurrentSchemaId,
                            referenceNatureEventChoiceSessionSchemaId =
                                ReferenceNatureControlledEventChoiceSessionV111
                                    .CurrentSchemaId,
                            referenceNatureProviderId =
                                ReferenceNatureV111.ProviderId,
                            controlledEventChoiceSchemaId =
                                ControlledEventChoiceEngineV111.SchemaId,
                            controlledEventChoiceScope =
                                "saved-event-effect-and-event-reward-frame",
                            controlledRunActionSchemaId =
                                ControlledRunActionEngineV111.SchemaId,
                            controlledRunActionScope =
                                "legacy-run-start-map-event-shop-treasure-entry-and-shovel-selection",
                            controlledRunActionSiteGroups = new
                            {
                                runStartAndMapGeneration =
                                    StochasticSiteRegistryV111.RunActionSiteIds,
                                eventNodeEntry =
                                    StochasticSiteRegistryV111
                                        .RunActionEventEntrySiteIds,
                                legacyShopNodeGeneration =
                                    StochasticSiteRegistryV111
                                        .RunActionShopSiteIds,
                                legacyTreasureNodeSelection =
                                    StochasticSiteRegistryV111
                                        .RunActionTreasureSiteIds,
                                shovelDig =
                                    StochasticSiteRegistryV111
                                        .RunActionShovelSiteIds
                            },
                            controlledRunActionMaximumInlineOutcomeActions =
                                ControlledRunActionEngineV111
                                    .MaximumInlineOutcomeActions,
                            controlledRunActionSites =
                                StochasticSiteRegistryV111
                                    .ControlledRunActionSiteIds,
                            controlledEventChoiceEnumerationSchemaId =
                                ControlledEventChoiceEnumeratorV111.SchemaId,
                            controlledEventChoiceEnumerationMaximumOutcomes =
                                ControlledEventChoiceEnumeratorV111
                                    .MaximumSupportedOutcomes,
                            controlledEventChoiceSites =
                                StochasticSiteRegistryV111
                                    .ControlledEventChoiceSiteIds,
                            semanticPersistentDeckActionSchemaId =
                                SemanticPlayerActionV111.SchemaId,
                            semanticPersistentDeckActionScope =
                                "draw-pile-neutral-single-player",
                            semanticPersistentDeckActionCardIds =
                                SemanticPlayerActionEngineV111.SupportedCardIds,
                            semanticNativePolicyComparatorId =
                                "engine-owned-native-v111-comparator-unverified-v1",
                            semanticSeededRouteEntryProviderId =
                                SemanticSourceRouteAuditV111.SourceRouteProviderId,
                            semanticPlayable = true,
                            semanticPlayableScope =
                                "seeded-source-route-or-post-committed-opening-fixture-restricted-Silent-combat",
                            semanticControlledSites = new[]
                            {
                                "enemy_intent", "draw_card"
                            },
                            semanticOtherChanceProvider =
                                "engine-owned-deterministic-unverified-v1",
                            semanticOtherChanceProviders = new[]
                            {
                                "engine-owned-deterministic-unverified-v1",
                                SemanticOtherChanceEngineV111.ProviderId
                            },
                            semanticRewardProvider =
                                SemanticRewardAuditV111.PrototypeProviderId,
                            semanticOpeningProvider = "precommitted-fixture-v1",
                            semanticSeededOpeningProvider =
                                "engine-owned-seeded-source-shaped-unverified-v1",
                            semanticSeededOpeningDrawHooksId =
                                "engine-owned-seeded-source-shaped-draw-hooks-unverified-v1",
                            semanticIntentSupportSchemaId =
                                SemanticIntentSupportV111.SchemaId,
                            semanticIntentEventSchemaId =
                                SemanticIntentEventV111.EventSchemaId,
                            semanticChanceEventContractId =
                                SemanticChanceEventContractV111.SchemaId,
                            semanticEndTurnIndexEventSchemaId =
                                SemanticEndTurnIndexEventV111.CurrentSchemaId,
                            semanticDrawChoiceSchemaId =
                                SemanticDeckStateV111.SchemaId,
                            semanticDeckRevealChoiceSchemaId =
                                SemanticDeckRevealDecisionV111.CurrentSchemaId,
                            semanticDrawHookChoiceSchemaId =
                                SemanticDrawHookChoiceEventV111.EventSchemaId,
                            semanticRandomPileCardChoiceSchemaId =
                                SemanticRandomPileCardEventV111.EventSchemaId,
                            semanticTargetChoiceSchemaId =
                                SemanticTargetChoiceV111.SchemaId,
                            semanticGeneratedCardChoiceSchemaId =
                                SemanticGenerationChoiceV111.SchemaId,
                            semanticPotionGenerationChoiceSchemaId =
                                SemanticPotionGenerationChoiceV111.SchemaId,
                            semanticCardTransformChoiceSchemaId =
                                SemanticCardTransformEventV111.EventSchemaId,
                            semanticCardSelectionChoiceSchemaId =
                                SemanticCardSelectionEventV111.EventSchemaId,
                            semanticCostChoiceSchemaId =
                                SemanticCostEventV111.EventSchemaId,
                            semanticHandCostSelectionChoiceSchemaId =
                                SemanticHandCostSelectionEventV111.EventSchemaId,
                            semanticCardShortlistChoiceSchemaId =
                                SemanticCardShortlistEventV111.EventSchemaId,
                            semanticPoolShuffleChoiceSchemaId =
                                SemanticPoolShuffleEventV111.EventSchemaId,
                            semanticDrawInsertionChoiceSchemaId =
                                SemanticDrawInsertionEventV111.EventSchemaId,
                            semanticEnumerationSchemaId =
                                SemanticNatureEnumeratorV111.SchemaId,
                            semanticEnumerationMaximumOutcomes =
                                SemanticNatureEnumeratorV111.MaximumSupportedOutcomes,
                            semanticExtendedControlledSites = new[]
                            {
                                "random_enemy_target", "enemy_action_chance",
                                "generated_card_pool",
                                "drawn_card_cost", "generated_card_pool_shuffle",
                                "generated_draw_pile_insertion",
                                "random_draw_attack_card_model",
                                "random_hand_card_energy_cost",
                                "random_hand_card_select",
                                "random_pile_selection_shortlist",
                                "random_pile_card", "random_auto_play_card",
                                "random_replay_card",
                                "random_rare_to_hand_shuffle",
                                "hand_return_shuffle",
                                "random_potion_generation", "random_card_transform"
                            },
                            semanticExtendedPlayerChoices = new[]
                            {
                                "select_cards", "card_drawn_hook_action"
                            },
                            semanticProbabilityStatus = "UNVERIFIED",
                            controlledChanceScope = "isolated-playable-combat-stage-continuations-v1",
                            controlledChanceSupportedSites = new[]
                            {
                                ControlledPlayableCombatEngineV111.ShuffleSite,
                                NativeCombatRngEntryV111.InitialUniqueHpSiteId,
                                NativeCombatRngEntryV111.ToughEggHatchHpSiteId,
                                ControlledTurnIntentEngineV111.SiteId,
                                ControlledRecycleShuffleEngineV111.SiteId,
                                "combat.effect.random_enemy",
                                "combat.sly_autoplay.random_enemy",
                                "combat.autoplay.random_enemy",
                                "combat.generated_card.select",
                                "combat.potion_generation.random_model",
                                "combat.card_transform.replacement.index_draw",
                                "combat.random_draw_attack.card_model",
                                "combat.draw_pile.random_insert_index",
                                PrototypeControlledDrawInsertionV111
                                    .PersonalHiveDazedSiteId,
                                "combat.enemy.theft_card.select",
                                "combat.enemy.summon_pool.select",
                                "combat.hand_return.draw_pile.shuffle_swap_index",
                                "combat.generation.full_pool_shuffle.swap_index",
                                "combat.random_hand_card.select",
                                "combat.random_hand_card.energy_cost",
                                "combat.card_selection.random_shortlist"
                            },
                            controlledChanceLimitations =
                                "Opt-in HP/intents, shuffle, turn intents, hand-draw recycle, " +
                                "random-enemy, Sly, and auto-play targeting, five generated-card " +
                                "effects, draw insertion, " +
                                "full-pool generation shuffles, " +
                                "hand-cost selection/rerolls, random pile shortlists, " +
                                "per-potion model generation, selected-card transforms, " +
                                "Bottled Potential hand-return shuffles, and their relic insertions; " +
                                "legacy enemy summon pools, summoned/hatchling HP and " +
                                "persistent-card theft during end turn; " +
                                "other chance sites fail closed; native entry and laws unverified.",
                            natureBoundaryKinds = new[] { "initial_hp", "intents" },
                            coverageSchemaId = V111Coverage.SchemaId,
                            snapshotSchemaId = RunSnapshot.SchemaId,
                            batchSchemaId = DeterministicBatch.SchemaId,
                            v111StateSchemaId = V111Build.StateSchema,
                            v111FidelityId = "v111-mechanics-prototype-rng-v1"
                        });
                        break;

                    case "manifest":
                        Write(new
                        {
                            requestId,
                            ok = true,
                            manifest = PrototypeCapabilities.Create()
                        });
                        break;

                    case "chance_law_query":
                    {
                        var siteId = RequiredString(request, "site_id");
                        if (!StochasticSiteRegistryV111
                                .IsChanceLawQuerySite(siteId))
                            throw new NotSupportedException(
                                $"Chance Law query does not support site '{siteId}'.");
                        int[]? priorIndices = null;
                        if (request.TryGetProperty(
                                "prior_indices", out var priorIndicesElement))
                        {
                            if (priorIndicesElement.ValueKind !=
                                JsonValueKind.Array)
                                throw new ArgumentException(
                                    "Request field 'prior_indices' must be an integer array.");
                            var parsedPriorIndices = new List<int>();
                            foreach (var element in
                                priorIndicesElement.EnumerateArray())
                            {
                                if (element.ValueKind != JsonValueKind.Number ||
                                    !element.TryGetInt32(out var selectedIndex) ||
                                    selectedIndex < 0)
                                    throw new ArgumentException(
                                        "Request field 'prior_indices' must contain nonnegative integers.");
                                parsedPriorIndices.Add(selectedIndex);
                            }
                            priorIndices = parsedPriorIndices.ToArray();
                        }
                        var run = RequireState(request);
                        var ordinal = OptionalInt(request, "ordinal") ??
                            throw new ArgumentException(
                                "Request field 'ordinal' is required.");
                        var cardType = OptionalString(request, "card_type");
                        var rarity = OptionalString(request, "rarity");
                        var cardId = OptionalString(request, "card_id");
                        var potionId = OptionalString(request, "potion_id");
                        var relicId = OptionalString(request, "relic_id");
                        var colorlessOffer = OptionalBool(
                            request, "colorless_offer");
                        var saleReprice = OptionalBool(
                            request, "sale_reprice");
                        var ownerIdParameter = OptionalString(
                            request, "owner_id");
                        var excludedPotionIds = OptionalStringArray(
                            request, "excluded_potion_ids");
                        var offerOrdinal = OptionalInt(
                            request, "offer_ordinal");
                        var relicCandidateIds = OptionalStringArray(
                            request, "relic_candidate_ids");
                        var excludedRelicIds = OptionalStringArray(
                            request, "excluded_relic_ids");
                        ChanceLawV111 law;
                        if (StochasticSiteRegistryV111
                            .AncientOfferSessionSiteIds.Contains(
                                siteId, StringComparer.Ordinal))
                        {
                            if (cardType is not null || rarity is not null ||
                                cardId is not null || potionId is not null ||
                                relicId is not null || colorlessOffer ||
                                saleReprice || excludedPotionIds is not null ||
                                offerOrdinal is not null ||
                                relicCandidateIds is not null ||
                                excludedRelicIds is not null)
                                throw new ArgumentException(
                                    "Ancient offer queries do not accept merchant parameters.");
                            law = ChanceLawEngineV111.QueryAncientOffer(
                                run, siteId, ordinal, priorIndices);
                        }
                        else
                        {
                            if (priorIndices is not null)
                                throw new ArgumentException(
                                    "prior_indices apply only to Ancient offer shuffle queries.");
                            if (siteId == "shop.merchant.card_upgrade_roll.unit_float")
                            {
                                if (cardType is not null || rarity is not null ||
                                    cardId is not null || potionId is not null ||
                                    relicId is not null || colorlessOffer ||
                                    saleReprice || excludedPotionIds is not null ||
                                    offerOrdinal is not null ||
                                    relicCandidateIds is not null ||
                                    excludedRelicIds is not null)
                                    throw new ArgumentException(
                                        "Merchant no-effect float query does not accept pool parameters.");
                                var typedRequest = new RandomUnitFloatRequestV111(
                                    siteId, "player.shops",
                                    OptionalString(request, "owner_id") ??
                                        throw new ArgumentException(
                                            "Merchant no-effect float query requires owner_id."),
                                    ordinal);
                                law = ChanceLawEngineV111
                                    .QueryNativeMerchantNoEffectUnitFloat(
                                        run, typedRequest);
                            }
                            else if (StochasticSiteRegistryV111
                                .IsNativeMerchantPriceSite(siteId))
                            {
                                if (cardType is not null || rarity is not null ||
                                    excludedPotionIds is not null ||
                                    offerOrdinal is not null ||
                                    relicCandidateIds is not null ||
                                    excludedRelicIds is not null)
                                    throw new ArgumentException(
                                        "Native merchant price queries do not accept model-pool parameters.");
                                law = ChanceLawEngineV111
                                    .QueryNativeMerchantPrice(
                                        run, siteId, ordinal, cardId,
                                        potionId, relicId, colorlessOffer,
                                        saleReprice);
                            }
                            else if (siteId == "relic_grab_bag.rarity.unit_float" &&
                                ownerIdParameter == PrototypeNativeRelicGrabBag
                                    .NativeTreasureRarityOwnerId)
                            {
                                if (cardType is not null || rarity is not null ||
                                    cardId is not null || potionId is not null ||
                                    relicId is not null || colorlessOffer ||
                                    saleReprice || excludedPotionIds is not null ||
                                    offerOrdinal is not null ||
                                    relicCandidateIds is not null ||
                                    excludedRelicIds is not null)
                                    throw new ArgumentException(
                                        "Native treasure rarity queries accept only the source owner and ordinal.");
                                var typedRequest = new RandomUnitFloatRequestV111(
                                    siteId, "player.rewards", ownerIdParameter,
                                    ordinal);
                                law = ChanceLawEngineV111
                                    .QueryNativeTreasureRarity(run, typedRequest);
                            }
                            else if (siteId == "treasure.relic_grab_bag.draw")
                            {
                                if (cardType is not null || rarity is not null ||
                                    cardId is not null || potionId is not null ||
                                    relicId is not null || colorlessOffer ||
                                    saleReprice || excludedPotionIds is not null ||
                                    offerOrdinal is not null ||
                                    relicCandidateIds is not null ||
                                    excludedRelicIds is not null)
                                    throw new ArgumentException(
                                        "Native treasure relic queries do not accept merchant parameters.");
                                law = ChanceLawEngineV111
                                    .QueryNativeTreasureRelicDraw(
                                        run, siteId, ordinal);
                            }
                            else if (siteId == PrototypeNativeRelicGrabBag
                                .NativeTreasureModelSiteId)
                            {
                                if (cardType is not null || cardId is not null ||
                                    potionId is not null || relicId is not null ||
                                    colorlessOffer || saleReprice ||
                                    excludedPotionIds is not null ||
                                    offerOrdinal is not null ||
                                    excludedRelicIds is not null ||
                                    rarity is null || relicCandidateIds is null ||
                                    run.World is null ||
                                    !Enum.TryParse<PrototypeRelicRarity>(
                                        rarity, ignoreCase: false,
                                        out var treasureRarity) ||
                                    !Enum.IsDefined(treasureRarity))
                                    throw new ArgumentException(
                                        "Native treasure relic selection requires rarity and relic_candidate_ids only.");
                                var ownerId = PrototypeNativeRelicGrabBag
                                    .TreasureModelOwnerId(
                                        run.World, treasureRarity,
                                        relicCandidateIds);
                                var typedRequest = new RandomIndexRequestV111(
                                    siteId,
                                    "player.rewards",
                                    ownerId, ordinal,
                                    Enumerable.Range(0,
                                        relicCandidateIds.Length).ToArray());
                                law = ChanceLawEngineV111
                                    .QueryNativeTreasureRelicSelection(
                                        run, typedRequest, treasureRarity,
                                        relicCandidateIds);
                            }
                            else if (siteId == PrototypeNativeRelicGrabBag
                                .NativeMerchantModelSiteId)
                            {
                                if (cardType is not null || cardId is not null ||
                                    potionId is not null || relicId is not null ||
                                    colorlessOffer || saleReprice ||
                                    excludedPotionIds is not null ||
                                    offerOrdinal is null || rarity is null ||
                                    relicCandidateIds is null ||
                                    excludedRelicIds is null ||
                                    run.World is null ||
                                    !Enum.TryParse<PrototypeRelicRarity>(
                                        rarity, true, out var relicRarity) ||
                                    !Enum.IsDefined(relicRarity))
                                    throw new ArgumentException(
                                        "Native merchant relic selection requires offer_ordinal, rarity, relic_candidate_ids and excluded_relic_ids only.");
                                var ownerId = PrototypeNativeRelicGrabBag
                                    .MerchantModelOwnerId(
                                        run.World, offerOrdinal.Value,
                                        relicRarity, relicCandidateIds);
                                var typedRequest = new RandomIndexRequestV111(
                                    siteId,
                                    "player.shops",
                                    ownerId, ordinal,
                                    Enumerable.Range(0,
                                        relicCandidateIds.Length).ToArray());
                                law = ChanceLawEngineV111
                                    .QueryNativeMerchantRelicSelection(
                                        run, typedRequest,
                                        offerOrdinal.Value, relicRarity,
                                        relicCandidateIds,
                                        excludedRelicIds);
                            }
                            else
                            {
                                if (cardId is not null || potionId is not null ||
                                    relicId is not null || colorlessOffer ||
                                    saleReprice || offerOrdinal is not null ||
                                    relicCandidateIds is not null ||
                                    excludedRelicIds is not null)
                                    throw new ArgumentException(
                                        "Native merchant offer queries do not accept item-price parameters.");
                                law = ChanceLawEngineV111
                                    .QueryNativeMerchantOffer(
                                        run, siteId, ordinal, cardType, rarity,
                                        excludedPotionIds);
                            }
                        }
                        Write(new
                        {
                            requestId,
                            ok = true,
                            chanceLawSchemaId = ChanceLawV111.CurrentSchemaId,
                            law
                        });
                        break;
                    }

                    case "v111_coverage":
                        Write(new { requestId, ok = true, coverage = V111Coverage.Create() });
                        break;

                    case "nature_begin":
                    {
                        var kind = RequiredString(request, "kind");
                        var seed = RequiredString(request, "seed");
                        int act = OptionalInt(request, "act") ?? 2;
                        var encounterId = RequiredString(request, "encounter_id");
                        var nativeEntry = RequiredString(request, "native_entry");
                        int floor = OptionalInt(request, "total_floor") ?? 20;
                        int ascension = OptionalInt(request, "ascension") ?? 0;
                        int column = OptionalInt(request, "map_column") ?? 3;
                        int row = OptionalInt(request, "map_row") ?? 5;
                        if (act is not (2 or 3))
                            throw new NotSupportedException(
                                "Nature v1 isolated formation fixture supports Hive/Glory only.");
                        if (kind == "initial_hp")
                        {
                            var pre = PrototypeNativeLaterActFixture.CreateCombatRngPreEntry(
                                seed, act, encounterId, nativeEntry, floor,
                                ascension, 1, column, row);
                            var state = ControlledInitialHpV111.Begin(
                                pre.Combat, pre.Formation, pre.NativeStreams);
                            var handle = StoreNature(state);
                            Write(new { requestId, ok = true,
                                schemaId = NatureBoundarySchemaId,
                                kind, natureHandle = handle,
                                fingerprint = state.Fingerprint,
                                pending = state.Pending, complete = state.Complete,
                                mode = state.Mode });
                        }
                        else if (kind == "intents")
                        {
                            var pre = PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
                                seed, act, encounterId, nativeEntry, floor,
                                ascension, 1, column, row,
                                commitInitialIntents: false);
                            var state = ControlledIntentsV111.Begin(pre);
                            var handle = StoreNatureIntent(state);
                            Write(new { requestId, ok = true,
                                schemaId = NatureBoundarySchemaId,
                                kind, natureHandle = handle,
                                fingerprint = state.Fingerprint,
                                pending = state.Pending, complete = state.Complete,
                                mode = state.Mode });
                        }
                        else throw new NotSupportedException(
                            "Unsupported Nature boundary kind.");
                        break;
                    }

                    case "nature_advance":
                    {
                        // Compose completed controlled HP outcomes into source
                        // intent selection without sampling native RNG again.
                        var handle = RequiredString(request, "nature_handle");
                        if (!natureHp.TryGetValue(handle, out var hp))
                            throw new InvalidOperationException(
                                "nature_advance requires an initial_hp handle.");
                        var next = ControlledIntentsV111.BeginAfterControlledHp(hp);
                        string childHandle = StoreNatureIntent(next);
                        Write(new { requestId, ok = true,
                            schemaId = NatureBoundarySchemaId,
                            kind = "intents", parent = handle,
                            child = childHandle, natureHandle = childHandle,
                            fingerprint = next.Fingerprint,
                            pending = next.Pending, complete = next.Complete,
                            mode = next.Mode });
                        break;
                    }

                    case "nature_resolve":
                    {
                        var handle = RequiredString(request, "nature_handle");
                        var fingerprint = RequiredString(request, "fingerprint");
                        int selectedIndex = OptionalInt(request, "selected_index") ??
                            throw new ArgumentException("Nature requires selected_index.");
                        if (natureHp.TryGetValue(handle, out var hp))
                        {
                            var child = ControlledInitialHpV111.Resolve(
                                hp, fingerprint, selectedIndex);
                            string childHandle = StoreNature(child);
                            Write(new { requestId, ok = true,
                                schemaId = NatureBoundarySchemaId,
                                kind = "initial_hp", parent = handle,
                                child = childHandle, natureHandle = childHandle,
                                fingerprint = child.Fingerprint,
                                pending = child.Pending, complete = child.Complete,
                                mode = child.Mode });
                        }
                        else if (natureIntents.TryGetValue(handle, out var intent))
                        {
                            var child = ControlledIntentsV111.Resolve(
                                intent, fingerprint, selectedIndex);
                            string childHandle = StoreNatureIntent(child);
                            Write(new { requestId, ok = true,
                                schemaId = NatureBoundarySchemaId,
                                kind = "intents", parent = handle,
                                child = childHandle, natureHandle = childHandle,
                                fingerprint = child.Fingerprint,
                                pending = child.Pending, complete = child.Complete,
                                mode = child.Mode });
                        }
                        else throw new InvalidOperationException(
                            "Unknown Nature handle: " + handle);
                        break;
                    }

                    case "nature_inspect":
                    {
                        // The chance controller may inspect support/ordered
                        // outcomes and replay provenance, not the player's
                        // hidden exact state. Ordinary fair observe remains
                        // exclusive to playable state handles.
                        var handle = RequiredString(request, "nature_handle");
                        NatureBoundaryViewV111 view;
                        if (natureHp.TryGetValue(handle, out var hp))
                            view = NatureBoundaryMetadataV111.Describe(hp);
                        else if (natureIntents.TryGetValue(handle, out var intents))
                            view = NatureBoundaryMetadataV111.Describe(intents);
                        else throw new InvalidOperationException(
                            "Unknown Nature handle: " + handle);
                        Write(new { requestId, ok = true,
                            schemaId = NatureBoundaryMetadataV111.SchemaId,
                            kind = view.Kind, mode = view.Mode,
                            natureHandle = handle, fingerprint = view.Fingerprint,
                            complete = view.Complete,
                            pending = view.Pending, transcript = view.Transcript });
                        break;
                    }

                    case "nature_fork":
                    {
                        var handle = RequiredString(request, "nature_handle");
                        if (natureHp.TryGetValue(handle, out var hp))
                        {
                            var child = hp.Fork();
                            string newHandle = StoreNature(child);
                            Write(new { requestId, ok = true,
                                kind = "initial_hp", parent = handle,
                                natureHandle = newHandle,
                                fingerprint = child.Fingerprint,
                                pending = child.Pending, complete = child.Complete });
                        }
                        else if (natureIntents.TryGetValue(handle, out var intent))
                        {
                            var child = ControlledIntentsV111.Restore(
                                ControlledIntentsV111.Save(intent));
                            string newHandle = StoreNatureIntent(child);
                            Write(new { requestId, ok = true,
                                kind = "intents", parent = handle,
                                natureHandle = newHandle,
                                fingerprint = child.Fingerprint,
                                pending = child.Pending, complete = child.Complete });
                        }
                        else throw new InvalidOperationException(
                            "Unknown Nature handle: " + handle);
                        break;
                    }

                    case "nature_release_many":
                    {
                        if (!request.TryGetProperty("nature_handles", out var elements) ||
                            elements.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "nature_release_many requires nature_handles array.");
                        // Validate the complete batch before removing any
                        // handle so malformed requests leave session intact.
                        var handles = elements.EnumerateArray().Select(element =>
                            element.ValueKind == JsonValueKind.String
                                ? element.GetString()
                                : throw new ArgumentException(
                                    "nature_handles entries must be strings")).ToArray();
                        if (handles.Any(string.IsNullOrWhiteSpace))
                            throw new ArgumentException("Empty Nature handle.");
                        int released = 0;
                        foreach (var handle in handles)
                        {
                            if (natureHp.Remove(handle!) || natureIntents.Remove(handle!))
                                released++;
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "nature_save":
                    {
                        var handle = RequiredString(request, "nature_handle");
                        if (natureHp.TryGetValue(handle, out var hp))
                            Write(new { requestId, ok = true,
                                schemaId = NatureBoundarySchemaId,
                                kind = "initial_hp",
                                snapshot = ControlledInitialHpV111.Save(hp) });
                        else if (natureIntents.TryGetValue(handle, out var intent))
                            Write(new { requestId, ok = true,
                                schemaId = NatureBoundarySchemaId,
                                kind = "intents",
                                snapshot = ControlledIntentsV111.Save(intent) });
                        else throw new InvalidOperationException(
                            "Unknown Nature handle: " + handle);
                        break;
                    }

                    case "nature_load":
                    {
                        var kind = RequiredString(request, "kind");
                        var snapshot = RequiredString(request, "snapshot");
                        if (kind == "initial_hp")
                        {
                            var state = ControlledInitialHpV111.Restore(snapshot);
                            string handle = StoreNature(state);
                            Write(new { requestId, ok = true,
                                schemaId = NatureBoundarySchemaId, kind,
                                natureHandle = handle,
                                fingerprint = state.Fingerprint,
                                pending = state.Pending, complete = state.Complete });
                        }
                        else if (kind == "intents")
                        {
                            var state = ControlledIntentsV111.Restore(snapshot);
                            string handle = StoreNatureIntent(state);
                            Write(new { requestId, ok = true,
                                schemaId = NatureBoundarySchemaId, kind,
                                natureHandle = handle,
                                fingerprint = state.Fingerprint,
                                pending = state.Pending, complete = state.Complete });
                        }
                        else throw new NotSupportedException(
                            "Unsupported Nature snapshot kind.");
                        break;
                    }

                    case "semantic_begin":
                    {
                        // P0 migration boundary: a previously completed
                        // source-shaped opening fixture. Opening shuffle
                        // positions are retained as engine-committed known
                        // positions, never offered as deferred Nature choices.
                        var sourceHandle = RequiredString(request, "controlled_handle");
                        if (!controlledStates.TryGetValue(sourceHandle, out var source))
                            throw new InvalidOperationException(
                                "semantic_begin requires a valid completed controlled_handle.");
                        var next = SemanticPilotCombatEngineV111.Begin(source);
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle) });
                        break;
                    }

                    case "semantic_begin_seeded":
                    {
                        // Only intent and deferred draws can reach the
                        // external Nature policy. The opening is generated
                        // independently by engine-owned seeded streams,
                        // without externally selected HP or shuffle tickets.
                        var sourceRouteEntry = OptionalBool(request,
                            "source_route_entry");
                        var routeRegionText = OptionalString(request,
                            "route_region") ?? nameof(ActIdentity.Overgrowth);
                        if (!Enum.TryParse<ActIdentity>(routeRegionText,
                                ignoreCase: true, out var routeRegion) ||
                            routeRegion is not (ActIdentity.Overgrowth or
                                ActIdentity.Underdocks))
                            throw new ArgumentException(
                                "route_region must be Overgrowth or Underdocks.");
                        var entry = new SemanticSeededEntryV111(
                            RequiredString(request, "seed"),
                            OptionalInt(request, "act") ?? (sourceRouteEntry ? 1 :
                                throw new ArgumentException("Missing act.")),
                            OptionalString(request, "encounter_id") ??
                                (sourceRouteEntry ? "@source-route" :
                                    throw new ArgumentException("Missing encounter_id.")),
                            OptionalString(request, "native_entry") ??
                                (sourceRouteEntry ? "@source-route" :
                                    throw new ArgumentException("Missing native_entry.")),
                            OptionalInt(request, "total_floor") ??
                                (sourceRouteEntry ? 1 :
                                    throw new ArgumentException("Missing total_floor.")),
                            OptionalInt(request, "ascension") ?? 0,
                            OptionalInt(request, "first_monster_combat_id") ?? 1,
                            OptionalInt(request, "map_column"),
                            OptionalInt(request, "map_row"),
                            OptionalString(request, "other_chance_provider_id"),
                            OptionalString(request, "fixture_draw_cost_relic_id"),
                            OptionalBool(request, "replay_opening_draw_hooks"),
                            OptionalBool(request, "native_policy_baseline"),
                            sourceRouteEntry, routeRegion,
                            OptionalString(request, "route_neow_choice_id"),
                            OptionalString(request, "route_map_node_id"),
                            OptionalString(request, "fixture_random_target_card_id"),
                            OptionalString(request, "fixture_generation_potion_id"),
                            OptionalString(request, "fixture_selection_card_id"),
                            OptionalBool(request, "fixture_nature_draw_costs"),
                            OptionalString(request,
                                "fixture_pool_generation_potion_id"),
                            OptionalString(request,
                                "fixture_generated_draw_insertion_card_id"),
                            OptionalBool(request,
                                "fixture_random_free_attack_draw_insertion"),
                            OptionalInt(request,
                                "fixture_randomize_hand_costs_max_exclusive"),
                            OptionalInt(request,
                                "fixture_set_random_hand_card_cost"),
                            OptionalString(request,
                                "fixture_selection_potion_id"),
                            OptionalString(request,
                                "fixture_potion_generation_card_id"));
                        var next = SemanticPilotCombatEngineV111.BeginSeeded(entry);
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle) });
                        break;
                    }

                    case "semantic_status":
                    {
                        var current = RequireSemantic(request);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(current,
                                RequiredString(request, "semantic_handle")) });
                        break;
                    }

                    case "semantic_observe":
                    {
                        var current = RequireSemantic(request);
                        if (RequiredString(request, "policy_id") != FairPolicyId)
                            throw new NotSupportedException(
                                "Semantic observation requires the existing fair policy.");
                        if (!current.AtPlayerBoundary &&
                            !current.AtPlayerChoiceBoundary)
                            throw new InvalidOperationException(
                                "Semantic observation is available only at a player boundary.");
                        var frame = environment.Observe(current.Run);
                        Write(new { requestId, ok = true,
                            schemaId = SemanticChanceSchemaId,
                            policyId = FairPolicyId,
                            payloadJson = CanonicalJson.Serialize(frame.Observation),
                            observationHash = frame.ObservationHash,
                            actions = frame.LegalActions.Select(ActionWire).ToArray(),
                            terminal = false });
                        break;
                    }

                    case "semantic_nature_observe":
                    {
                        var handle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var natureChoice =
                            DescribeSemanticNatureChoice(current);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            schemaId =
                                SemanticNatureChoiceViewV111.CurrentSchemaId,
                            semanticHandle = handle,
                            fingerprint = current.Fingerprint,
                            natureChoice
                        });
                        break;
                    }

                    case "nature_choice_observe":
                    {
                        var sessionKind = RequiredString(request,
                            "session_kind");
                        var handle = RequiredString(request, "session_handle");
                        var (fingerprint, natureChoice) =
                            DescribeNatureChoiceStatus(sessionKind, handle);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            schemaId =
                                SemanticNatureChoiceViewV111.CurrentSchemaId,
                            sessionKind,
                            sessionHandle = handle,
                            fingerprint,
                            natureChoice
                        });
                        break;
                    }

                    case "semantic_step":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.Step(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "action_id"));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.Resolve(
                            current, RequiredString(request, "fingerprint"),
                            OptionalInt(request, "selected_index") ??
                                throw new ArgumentException("Missing selected_index."));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_draw_card":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveDrawCard(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            RequiredLong(request, "card_instance_id"));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_deck_reveal":
                    {
                        var parentHandle = RequiredString(request,
                            "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveDeckReveal(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            RequiredLong(request, "card_instance_id"));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_random_pile_card":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveRandomPileCard(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            RequiredLong(request, "card_instance_id"));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_random_target":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveRandomTarget(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            OptionalInt(request, "enemy_instance_id") ??
                                throw new ArgumentException(
                                    "Missing enemy_instance_id."));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_generated_card":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveGeneratedCard(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            RequiredString(request, "card_model_id"));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_potion_generation":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolvePotionGeneration(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            RequiredString(request, "potion_model_id"));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_card_transform":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveCardTransform(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            RequiredString(request, "card_model_id"));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_cost":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveCost(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            OptionalInt(request, "cost") ??
                                throw new ArgumentException("Missing cost."));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_hand_cost_selection":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111
                            .ResolveHandCostSelection(
                                current, RequiredString(request, "fingerprint"),
                                RequiredString(request, "event_id"),
                                RequiredLong(request, "card_instance_id"));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_card_shortlist":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveCardShortlist(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            RequiredLong(request, "card_instance_id"));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_pool_shuffle":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolvePoolShuffle(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            OptionalInt(request, "swap_index") ??
                                throw new ArgumentException(
                                    "Missing swap_index."));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_draw_insertion":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveDrawInsertion(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "event_id"),
                            OptionalInt(request, "position") ??
                                throw new ArgumentException("Missing position."));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_intent":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.ResolveIntent(
                            current, RequiredString(request, "fingerprint"),
                            RequiredString(request, "intent_id"));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_resolve_enemy_action":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var next = SemanticPilotCombatEngineV111.Resolve(
                            current, RequiredString(request, "fingerprint"),
                            OptionalInt(request, "selected_index") ??
                                throw new ArgumentException(
                                    "Missing selected_index."));
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_enumerate":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var current = RequireSemantic(request);
                        var enumeration = SemanticNatureEnumeratorV111.Enumerate(
                            current, RequiredString(request, "fingerprint"),
                            OptionalInt(request, "maximum_outcomes") ?? 32);
                        var outcomes = enumeration.Outcomes.Select(outcome =>
                        {
                            var handle = StoreSemantic(outcome.Child);
                            return new
                            {
                                outcomeId = outcome.OutcomeId,
                                result = SemanticResult(
                                    outcome.Child, handle, parentHandle)
                            };
                        }).ToArray();
                        Write(new { requestId, ok = true,
                            schemaId = enumeration.SchemaId,
                            eventKind = enumeration.EventKind,
                            eventId = enumeration.EventId, outcomes });
                        break;
                    }

                    case "semantic_fork":
                    {
                        var parentHandle = RequiredString(request, "semantic_handle");
                        var next = RequireSemantic(request).Fork();
                        var handle = StoreSemantic(next);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_save":
                    {
                        var state = RequireSemantic(request);
                        Write(new { requestId, ok = true,
                            schemaId = SemanticChanceSchemaId,
                            snapshot = SemanticPilotCombatEngineV111.Save(state) });
                        break;
                    }

                    case "semantic_load":
                    {
                        var state = SemanticPilotCombatEngineV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreSemantic(state);
                        Write(new { requestId, ok = true,
                            result = SemanticResult(state, handle) });
                        break;
                    }

                    case "reference_nature_run_action_begin":
                    {
                        var source = RequireState(request);
                        var actionId = OptionalString(request, "action_id");
                        GameAction action;
                        if (actionId is not null)
                        {
                            var actionView = environment.Observe(source)
                                .LegalActions.SingleOrDefault(candidate =>
                                    StringComparer.Ordinal.Equals(
                                        candidate.ActionId, actionId))
                                ?? throw new InvalidOperationException(
                                    "ReferenceNature run-action begin requires a current legal action.");
                            action = GameAction.Create(actionView.Kind,
                                actionView.Payload);
                        }
                        else
                        {
                            action = new PrototypeGameEngine().GetLegalActions(source)
                                .SingleOrDefault(candidate => candidate.Kind ==
                                    PrototypeNativeRestSiteActionsV111.Dig)
                                ?? throw new InvalidOperationException(
                                    "Missing action_id; only Shovel dig has a default.");
                        }
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureRunActionSessionV111.Begin(
                                source, action, natureSeed)
                            : ReferenceNatureRunActionSessionV111.Begin(
                                source, action);
                        var handle = StoreReferenceNatureRunAction(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureRunActionResult(session, handle) });
                        break;
                    }

                    case "reference_nature_run_action_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_run_action_handle");
                        var session = RequireReferenceNatureRunAction(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureRunActionResult(session, handle) });
                        break;
                    }

                    case "reference_nature_run_action_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_run_action_handle");
                        var session = RequireReferenceNatureRunAction(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature run-action session fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureRunAction(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureRunActionResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_run_action_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_run_action_handle");
                        var next = RequireReferenceNatureRunAction(request).Fork();
                        var handle = StoreReferenceNatureRunAction(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureRunActionResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_run_action_save":
                    {
                        var session = RequireReferenceNatureRunAction(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureRunActionSessionV111.CurrentSchemaId,
                            snapshot = ReferenceNatureRunActionSessionV111.Save(session) });
                        break;
                    }

                    case "reference_nature_run_action_load":
                    {
                        var session = ReferenceNatureRunActionSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureRunAction(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureRunActionResult(session, handle) });
                        break;
                    }

                    case "reference_nature_run_action_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_run_action_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_run_action_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature run-action handle.");
                            if (referenceNatureRunActionStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNatureRunActionRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_event_choice_begin":
                    {
                        var source = RequireState(request);
                        var choiceId = RequiredString(request, "choice_id");
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureControlledEventChoiceSessionV111.Begin(
                                source, choiceId, natureSeed)
                            : ReferenceNatureControlledEventChoiceSessionV111.Begin(
                                source, choiceId);
                        var handle = StoreReferenceNatureEventChoice(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureEventChoiceResult(session, handle) });
                        break;
                    }

                    case "reference_nature_event_choice_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_event_choice_handle");
                        var session = RequireReferenceNatureEventChoice(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureEventChoiceResult(session, handle) });
                        break;
                    }

                    case "reference_nature_event_choice_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_event_choice_handle");
                        var session = RequireReferenceNatureEventChoice(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature event-choice fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureEventChoice(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureEventChoiceResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_event_choice_advance":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_event_choice_handle");
                        var session = RequireReferenceNatureEventChoice(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature event-choice fingerprint.");
                        var actionId = RequiredString(request, "action_id");
                        var action = environment.Observe(session.Run)
                            .LegalActions.SingleOrDefault(candidate =>
                                StringComparer.Ordinal.Equals(
                                    candidate.ActionId, actionId))
                            ?? throw new InvalidOperationException(
                                "ReferenceNature event advance requires a current legal reward action.");
                        var next = session.AdvancePlayerAction(
                            GameAction.Create(action.Kind, action.Payload));
                        var handle = StoreReferenceNatureEventChoice(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureEventChoiceResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_event_choice_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_event_choice_handle");
                        var next = RequireReferenceNatureEventChoice(request).Fork();
                        var handle = StoreReferenceNatureEventChoice(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureEventChoiceResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_event_choice_save":
                    {
                        var session = RequireReferenceNatureEventChoice(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureControlledEventChoiceSessionV111
                                    .CurrentSchemaId,
                            snapshot =
                                ReferenceNatureControlledEventChoiceSessionV111
                                    .Save(session) });
                        break;
                    }

                    case "reference_nature_event_choice_load":
                    {
                        var session =
                            ReferenceNatureControlledEventChoiceSessionV111.Restore(
                                RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureEventChoice(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureEventChoiceResult(session, handle) });
                        break;
                    }

                    case "reference_nature_event_choice_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_event_choice_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_event_choice_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature event-choice handle.");
                            if (referenceNatureEventChoiceStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNatureEventChoiceRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_ancient_offer_begin":
                    {
                        var source = RequireState(request);
                        var siteId = RequiredString(request, "site_id");
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureAncientOfferSessionV111.Begin(
                                source, siteId, natureSeed)
                            : ReferenceNatureAncientOfferSessionV111.Begin(
                                source, siteId);
                        var handle = StoreReferenceNatureAncientOffer(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureAncientOfferResult(session, handle) });
                        break;
                    }

                    case "reference_nature_ancient_offer_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_ancient_offer_handle");
                        var session = RequireReferenceNatureAncientOffer(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureAncientOfferResult(session, handle) });
                        break;
                    }

                    case "reference_nature_ancient_offer_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_ancient_offer_handle");
                        var session = RequireReferenceNatureAncientOffer(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature Ancient-offer fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureAncientOffer(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureAncientOfferResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_ancient_offer_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_ancient_offer_handle");
                        var next = RequireReferenceNatureAncientOffer(request).Fork();
                        var handle = StoreReferenceNatureAncientOffer(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureAncientOfferResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_ancient_offer_save":
                    {
                        var session = RequireReferenceNatureAncientOffer(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureAncientOfferSessionV111.CurrentSchemaId,
                            snapshot =
                                ReferenceNatureAncientOfferSessionV111.Save(session) });
                        break;
                    }

                    case "reference_nature_ancient_offer_load":
                    {
                        var session = ReferenceNatureAncientOfferSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureAncientOffer(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureAncientOfferResult(session, handle) });
                        break;
                    }

                    case "reference_nature_ancient_offer_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_ancient_offer_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_ancient_offer_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature Ancient-offer handle.");
                            if (referenceNatureAncientOfferStates.Remove(
                                    element.GetString()!))
                                released++;
                            referenceNatureAncientOfferRunStateHandles.Remove(
                                element.GetString()!);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_merchant_begin":
                    {
                        var source = RequireState(request);
                        var actionId = RequiredString(request, "action_id");
                        var action = environment.Observe(source).LegalActions
                            .SingleOrDefault(candidate =>
                                StringComparer.Ordinal.Equals(
                                    candidate.ActionId, actionId))
                            ?? throw new InvalidOperationException(
                                "ReferenceNature merchant begin requires a current legal Shop-node action.");
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureMerchantSessionV111.Begin(
                                source, GameAction.Create(
                                    action.Kind, action.Payload), natureSeed)
                            : ReferenceNatureMerchantSessionV111.Begin(
                                source, GameAction.Create(
                                    action.Kind, action.Payload));
                        var handle = StoreReferenceNatureMerchant(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureMerchantResult(session, handle) });
                        break;
                    }

                    case "reference_nature_merchant_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_merchant_handle");
                        var session = RequireReferenceNatureMerchant(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureMerchantResult(session, handle) });
                        break;
                    }

                    case "reference_nature_merchant_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_merchant_handle");
                        var session = RequireReferenceNatureMerchant(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature merchant fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureMerchant(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureMerchantResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_merchant_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_merchant_handle");
                        var next = RequireReferenceNatureMerchant(request).Fork();
                        var handle = StoreReferenceNatureMerchant(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureMerchantResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_merchant_save":
                    {
                        var session = RequireReferenceNatureMerchant(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureMerchantSessionV111.CurrentSchemaId,
                            snapshot =
                                ReferenceNatureMerchantSessionV111.Save(session) });
                        break;
                    }

                    case "reference_nature_merchant_load":
                    {
                        var session = ReferenceNatureMerchantSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureMerchant(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureMerchantResult(session, handle) });
                        break;
                    }

                    case "reference_nature_merchant_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_merchant_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_merchant_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature merchant handle.");
                            var handle = element.GetString()!;
                            if (referenceNatureMerchantStates.Remove(handle))
                                released++;
                            referenceNatureMerchantRunStateHandles.Remove(handle);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reference_nature_treasure_begin":
                    {
                        var source = RequireState(request);
                        var actionId = RequiredString(request, "action_id");
                        var action = environment.Observe(source).LegalActions
                            .SingleOrDefault(candidate =>
                                StringComparer.Ordinal.Equals(
                                    candidate.ActionId, actionId))
                            ?? throw new InvalidOperationException(
                                "ReferenceNature treasure begin requires a current legal Treasure-node action.");
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureTreasureSessionV111.Begin(
                                source, GameAction.Create(
                                    action.Kind, action.Payload), natureSeed)
                            : ReferenceNatureTreasureSessionV111.Begin(
                                source, GameAction.Create(
                                    action.Kind, action.Payload));
                        var handle = StoreReferenceNatureTreasure(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTreasureResult(session, handle) });
                        break;
                    }

                    case "reference_nature_treasure_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_treasure_handle");
                        var session = RequireReferenceNatureTreasure(request);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTreasureResult(session, handle) });
                        break;
                    }

                    case "reference_nature_treasure_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_treasure_handle");
                        var session = RequireReferenceNatureTreasure(request);
                        if (session.Fingerprint != RequiredString(request,
                                "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature treasure fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNatureTreasure(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTreasureResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_treasure_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_treasure_handle");
                        var next = RequireReferenceNatureTreasure(request).Fork();
                        var handle = StoreReferenceNatureTreasure(next);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTreasureResult(
                                next, handle, parentHandle) });
                        break;
                    }

                    case "reference_nature_treasure_save":
                    {
                        var session = RequireReferenceNatureTreasure(request);
                        Write(new { requestId, ok = true,
                            schemaId =
                                ReferenceNatureTreasureSessionV111.CurrentSchemaId,
                            snapshot = ReferenceNatureTreasureSessionV111
                                .Save(session) });
                        break;
                    }

                    case "reference_nature_treasure_load":
                    {
                        var session = ReferenceNatureTreasureSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNatureTreasure(session);
                        Write(new { requestId, ok = true, result =
                            ReferenceNatureTreasureResult(session, handle) });
                        break;
                    }

                    case "reference_nature_treasure_release_many":
                    {
                        if (!request.TryGetProperty(
                                "reference_nature_treasure_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_treasure_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature treasure handle.");
                            var handle = element.GetString()!;
                            if (referenceNatureTreasureStates.Remove(handle))
                                released++;
                            referenceNatureTreasureRunStateHandles
                                .Remove(handle);
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "semantic_reference_begin":
                    {
                        var combat = RequireSemantic(request);
                        if (combat.UsesNativePolicyBaseline)
                            throw new NotSupportedException(
                                "ReferenceNature cannot start from a native-policy session.");
                        var seed = OptionalULong(request, "nature_seed");
                        var session = seed is { } natureSeed
                            ? ReferenceNatureSessionV111.Begin(combat, natureSeed)
                            : ReferenceNatureSessionV111.Begin(combat);
                        var handle = StoreReferenceNature(session);
                        Write(new { requestId, result =
                            ReferenceNatureResult(session, handle) });
                        break;
                    }

                    case "semantic_reference_status":
                    {
                        var handle = RequiredString(request,
                            "reference_nature_handle");
                        var session = RequireReferenceNature(request);
                        Write(new { requestId, result =
                            ReferenceNatureResult(session, handle) });
                        break;
                    }

                    case "semantic_reference_observe":
                    {
                        var session = RequireReferenceNature(request);
                        if (RequiredString(request, "policy_id") != FairPolicyId)
                            throw new NotSupportedException(
                                "ReferenceNature observation requires the existing fair policy.");
                        if (!session.Combat.AtPlayerBoundary &&
                            !session.Combat.AtPlayerChoiceBoundary)
                            throw new InvalidOperationException(
                                "ReferenceNature observation is available only at a player boundary.");
                        var frame = environment.Observe(session.Combat.Run);
                        Write(new { requestId, ok = true,
                            schemaId = SemanticChanceSchemaId,
                            policyId = FairPolicyId,
                            payloadJson = CanonicalJson.Serialize(frame.Observation),
                            observationHash = frame.ObservationHash,
                            actions = frame.LegalActions.Select(ActionWire).ToArray(),
                            terminal = false });
                        break;
                    }

                    case "semantic_reference_step":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_handle");
                        var session = RequireReferenceNature(request);
                        if (session.Fingerprint != RequiredString(request, "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature session fingerprint.");
                        var combat = SemanticPilotCombatEngineV111.Step(
                            session.Combat, session.Combat.Fingerprint,
                            RequiredString(request, "action_id"));
                        var next = session with { Combat = combat };
                        next.Validate();
                        var handle = StoreReferenceNature(next);
                        Write(new { requestId, result =
                            ReferenceNatureResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_reference_resolve_next":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_handle");
                        var session = RequireReferenceNature(request);
                        if (session.Fingerprint != RequiredString(request, "fingerprint"))
                            throw new InvalidOperationException(
                                "Stale ReferenceNature session fingerprint.");
                        var next = session.ResolveNext();
                        var handle = StoreReferenceNature(next);
                        Write(new { requestId, result =
                            ReferenceNatureResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_reference_fork":
                    {
                        var parentHandle = RequiredString(request,
                            "reference_nature_handle");
                        var next = RequireReferenceNature(request).Fork();
                        var handle = StoreReferenceNature(next);
                        Write(new { requestId, result =
                            ReferenceNatureResult(next, handle, parentHandle) });
                        break;
                    }

                    case "semantic_reference_save":
                    {
                        var session = RequireReferenceNature(request);
                        Write(new { requestId, ok = true,
                            schemaId = ReferenceNatureSessionV111.CurrentSchemaId,
                            snapshot = ReferenceNatureSessionV111.Save(session) });
                        break;
                    }

                    case "semantic_reference_load":
                    {
                        var session = ReferenceNatureSessionV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreReferenceNature(session);
                        Write(new { requestId, result =
                            ReferenceNatureResult(session, handle) });
                        break;
                    }

                    case "semantic_reference_release_many":
                    {
                        if (!request.TryGetProperty("reference_nature_handles",
                                out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "reference_nature_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException(
                                    "Invalid ReferenceNature handle.");
                            if (referenceNatureStates.Remove(element.GetString()!))
                                released++;
                        }
                        Write(new { requestId, released });
                        break;
                    }

                    case "semantic_release_many":
                    {
                        if (!request.TryGetProperty("semantic_handles", out var handles) ||
                            handles.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException(
                                "semantic_handles must be an array.");
                        var released = 0;
                        foreach (var element in handles.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(element.GetString()))
                                throw new ArgumentException("Invalid semantic handle.");
                            if (semanticStates.Remove(element.GetString()!))
                                released++;
                        }
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "controlled_begin":
                    {
                        // Only a completed HP→intent chain may supply enemy
                        // state. The source provenance is checked before any
                        // playable state handle is published.
                        var sourceHandle = RequiredString(request, "nature_handle");
                        if (!natureIntents.TryGetValue(sourceHandle, out var intents))
                            throw new InvalidOperationException(
                                "controlled_begin requires an intent Nature handle.");
                        var state = ControlledPlayableCombatEngineV111.Begin(
                            intents, RequiredString(request, "seed"));
                        var handle = StoreControlled(state);
                        var view = NatureBoundaryMetadataV111.Describe(state);
                        Write(new { requestId, ok = true,
                            schemaId = ControlledChanceSchemaId,
                            controlledHandle = handle, fingerprint = state.Fingerprint,
                            parent = sourceHandle, phase = "opening_shuffle",
                            pending = view.Pending, transcript = view.Transcript,
                            terminal = false });
                        break;
                    }

                    case "controlled_resolve":
                    {
                        var parentHandle = RequiredString(request, "controlled_handle");
                        var parent = RequireControlled(request);
                        int selectedIndex = OptionalInt(request, "selected_index")
                            ?? throw new ArgumentException("Missing selected_index.");
                        var next = ControlledPlayableCombatEngineV111.Resolve(
                            parent, RequiredString(request, "fingerprint"), selectedIndex);
                        var handle = StoreControlled(next);
                        var view = NatureBoundaryMetadataV111.Describe(next);
                        Write(new { requestId, ok = true,
                            schemaId = ControlledChanceSchemaId,
                            parent = parentHandle, controlledHandle = handle,
                            fingerprint = next.Fingerprint,
                            phase = view.Kind, pending = view.Pending,
                            transcript = view.Transcript,
                            terminal = next.Run.Phase == RunPhase.Terminal });
                        break;
                    }

                    case "controlled_trace":
                    {
                        var current = RequireControlled(request);
                        var view = NatureBoundaryMetadataV111.Describe(current);
                        Write(new { requestId, ok = true,
                            schemaId = view.SchemaId, kind = view.Kind,
                            fingerprint = view.Fingerprint,
                            pending = view.Pending, transcript = view.Transcript,
                            playerActionIds = current.PlayerActions,
                            complete = view.Complete });
                        break;
                    }

                    case "controlled_observe":
                    {
                        var current = RequireControlled(request);
                        var policy = RequiredString(request, "policy_id");
                        if (policy != FairPolicyId)
                            throw new NotSupportedException(
                                "Controlled combat uses the existing fair policy.");
                        if (!current.AtPlayerBoundary)
                            throw new InvalidOperationException(
                                "Nature shuffle is pending; player observation unavailable.");
                        var frame = environment.Observe(current.Run);
                        Write(new { requestId, ok = true,
                            policyId = policy, schemaId = frame.SchemaId,
                            payloadJson = CanonicalJson.Serialize(frame.Observation),
                            observationHash = frame.ObservationHash,
                            actions = frame.LegalActions.Select(ActionWire).ToArray(),
                            terminal = current.Run.Phase == RunPhase.Terminal });
                        break;
                    }

                    case "controlled_legal_actions":
                    {
                        var current = RequireControlled(request);
                        if (!current.AtPlayerBoundary)
                            throw new InvalidOperationException(
                                "Nature shuffle is pending; player choices unavailable.");
                        var frame = environment.Observe(current.Run);
                        Write(new { requestId, ok = true,
                            actions = frame.LegalActions.Select(ActionWire).ToArray() });
                        break;
                    }

                    case "controlled_step":
                    {
                        var parentHandle = RequiredString(request, "controlled_handle");
                        var current = RequireControlled(request);
                        var next = ControlledPlayableCombatEngineV111.Step(
                            current, RequiredString(request, "action_id"));
                        var handle = StoreControlled(next);
                        var boundary = NatureBoundaryMetadataV111.Describe(next);
                        Write(new { requestId, ok = true,
                            schemaId = ControlledChanceSchemaId,
                            parent = parentHandle, controlledHandle = handle,
                            fingerprint = next.Fingerprint,
                            phase = boundary.Kind, pending = boundary.Pending,
                            transcript = boundary.Transcript,
                            terminal = next.Run.Phase == RunPhase.Terminal });
                        break;
                    }

                    case "controlled_fork":
                    {
                        var parentHandle = RequiredString(request, "controlled_handle");
                        var current = RequireControlled(request);
                        var next = current.Fork();
                        var handle = StoreControlled(next);
                        Write(new { requestId, ok = true, parent = parentHandle,
                            controlledHandle = handle, fingerprint = next.Fingerprint });
                        break;
                    }

                    case "controlled_save":
                    {
                        var current = RequireControlled(request);
                        Write(new { requestId, ok = true,
                            schemaId = ControlledChanceSchemaId,
                            snapshot = ControlledPlayableCombatEngineV111.Save(current) });
                        break;
                    }

                    case "controlled_load":
                    {
                        var next = ControlledPlayableCombatEngineV111.Restore(
                            RequiredString(request, "snapshot"));
                        var handle = StoreControlled(next);
                        var view = NatureBoundaryMetadataV111.Describe(next);
                        Write(new { requestId, ok = true,
                            schemaId = ControlledChanceSchemaId,
                            controlledHandle = handle, fingerprint = next.Fingerprint,
                            phase = view.Kind, pending = view.Pending,
                            terminal = next.Run.Phase == RunPhase.Terminal });
                        break;
                    }

                    case "controlled_release_many":
                    {
                        if (!request.TryGetProperty("controlled_handles", out var elements) ||
                            elements.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException("controlled_handles must be an array.");
                        var handles = elements.EnumerateArray().Select(element =>
                            element.ValueKind == JsonValueKind.String
                                ? element.GetString()
                                : throw new ArgumentException("Invalid controlled handle")).ToArray();
                        if (handles.Any(string.IsNullOrWhiteSpace))
                            throw new ArgumentException("Empty controlled handle.");
                        int released = 0;
                        foreach (var handle in handles)
                            if (controlledStates.Remove(handle!))
                                released++;
                        Write(new { requestId, ok = true, released });
                        break;
                    }

                    case "reset_v111":
                    {
                        var region = OptionalString(request, "first_act") ?? "Overgrowth";
                        var character = OptionalString(request, "character") ?? "silent";
                        var configuration = RunConfiguration.Create(character,
                            OptionalInt(request, "ascension") ?? 0,
                            Enum.Parse<ActIdentity>(region, ignoreCase: true));
                        var requestedFidelity = OptionalString(request, "fidelity_id");
                        if (requestedFidelity is not null)
                            configuration = configuration with
                            {
                                FidelityId = requestedFidelity
                            };
                        configuration.Validate();
                        var state = V111RunFactory.Create(RequiredString(request, "seed"), configuration);
                        var handle = Store(state);
                        Write(new { requestId, ok = true, stateHandle = handle,
                            stateSchemaId = V111Build.StateSchema, configuration.FidelityId,
                            exactHash = CanonicalJson.Sha256(state) });
                        break;
                    }

                    case "controlled_event_begin":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var source = RequireState(request);
                        var next = ControlledEventChoiceEngineV111.Begin(
                            source, RequiredString(request, "choice_id"));
                        var handle = Store(next, parentHandle);
                        Write(ControlledEventChoiceResult(
                            next, handle, parentHandle));
                        break;
                    }

                    case "controlled_event_advance":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var source = RequireState(request);
                        var actionId = RequiredString(request, "action_id");
                        var frame = environment.Observe(source);
                        var action = frame.LegalActions.SingleOrDefault(candidate =>
                            StringComparer.Ordinal.Equals(
                                candidate.ActionId, actionId))
                            ?? throw new InvalidOperationException(
                                $"Action '{actionId}' is not a player-legal reward action in state '{parentHandle}'.");
                        var next = ControlledEventChoiceEngineV111.Advance(
                            source,
                            GameAction.Create(action.Kind, action.Payload));
                        var handle = Store(next, parentHandle);
                        Write(ControlledEventChoiceResult(
                            next, handle, parentHandle));
                        break;
                    }

                    case "controlled_event_inspect":
                    {
                        var handle = RequiredString(request, "state_handle");
                        var state = RequireState(request);
                        if (state.World?.Event?.ControlledChoice is null)
                        {
                            if (state.ControlledEventRewardChoice is null)
                                throw new InvalidOperationException(
                                    "State has no pending controlled event choice.");
                        }
                        Write(ControlledEventChoiceResult(state, handle, null));
                        break;
                    }

                    case "controlled_event_resolve":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var source = RequireState(request);
                        var selectedIndex = OptionalInt(request, "selected_index")
                            ?? throw new ArgumentException("Missing selected_index.");
                        var next = ControlledEventChoiceEngineV111.Resolve(
                            source, selectedIndex);
                        var handle = Store(next, parentHandle);
                        Write(ControlledEventChoiceResult(
                            next, handle, parentHandle));
                        break;
                    }

                    case "controlled_event_enumerate":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var current = RequireState(request);
                        var enumeration =
                            ControlledEventChoiceEnumeratorV111.Enumerate(
                                current,
                                RequiredString(request, "fingerprint"),
                                OptionalInt(request, "maximum_outcomes") ?? 32);
                        var outcomes = enumeration.Outcomes.Select(outcome =>
                        {
                            var handle = Store(outcome.Child, parentHandle);
                            return new
                            {
                                outcomeId = outcome.SelectedIndex,
                                outcome.CandidateLabel,
                                result = ControlledEventChoiceResult(
                                    outcome.Child, handle, parentHandle)
                            };
                        }).ToArray();
                        Write(new
                        {
                            requestId,
                            ok = true,
                            schemaId = enumeration.SchemaId,
                            enumeration.EventId,
                            enumeration.ChoiceId,
                            outcomes
                        });
                        break;
                    }

                    case "controlled_run_action_begin":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var source = RequireState(request);
                        var engine = new PrototypeGameEngine();
                        GameAction action;
                        if (OptionalString(request, "action_id") is { } actionId)
                        {
                            var frame = environment.Observe(source);
                            var actionView = frame.LegalActions.SingleOrDefault(candidate =>
                                StringComparer.Ordinal.Equals(
                                    candidate.ActionId, actionId))
                                ?? throw new InvalidOperationException(
                                    "Controlled run-action begin requires a current player-legal action.");
                            action = GameAction.Create(
                                actionView.Kind, actionView.Payload);
                        }
                        else
                        {
                            action = engine.GetLegalActions(source)
                                .SingleOrDefault(candidate =>
                                    candidate.Kind ==
                                        PrototypeNativeRestSiteActionsV111.Dig)
                                ?? throw new InvalidOperationException(
                                    "Missing action_id; only Shovel dig can use the legacy default.");
                        }
                        var next = ControlledRunActionEngineV111.Begin(
                            source, action);
                        var handle = Store(next, parentHandle);
                        Write(ControlledRunActionResult(next, handle, parentHandle));
                        break;
                    }

                    case "controlled_run_action_inspect":
                    {
                        var handle = RequiredString(request, "state_handle");
                        var state = RequireState(request);
                        if (state.ControlledRunActionChoice is null)
                            throw new InvalidOperationException(
                                "State has no pending controlled run action.");
                        Write(ControlledRunActionResult(state, handle, null));
                        break;
                    }

                    case "controlled_run_action_resolve":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var source = RequireState(request);
                        var selectedIndex = OptionalInt(request, "selected_index")
                            ?? throw new ArgumentException("Missing selected_index.");
                        var next = ControlledRunActionEngineV111.Resolve(
                            source, selectedIndex);
                        var handle = Store(next, parentHandle);
                        Write(ControlledRunActionResult(next, handle, parentHandle));
                        break;
                    }

                    case "save_snapshot":
                        Write(new { requestId, ok = true, schemaId = RunSnapshot.SchemaId,
                            snapshot = RunSnapshot.Save(RequireState(request)) });
                        break;

                    case "load_snapshot":
                    {
                        var state = RunSnapshot.Load(RequiredString(request, "snapshot"));
                        var handle = Store(state);
                        Write(new { requestId, ok = true, stateHandle = handle,
                            exactHash = CanonicalJson.Sha256(state) });
                        break;
                    }

                    case "step_batch":
                    {
                        if (!request.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException("step_batch requires a steps array.");
                        var parents = steps.EnumerateArray().Select(step => new
                        {
                            Handle = RequiredString(step, "state_handle"),
                            State = RequireState(step), ActionId = RequiredString(step, "action_id")
                        }).ToArray();
                        // Resolve every action before storing any children. Errors
                        // leave the session unchanged and results preserve order.
                        var transitions = DeterministicBatch.Step(parents.Select(parent => new BatchStepRequest(
                            parent.State, new PrototypeGameEngine().GetLegalActions(parent.State)
                                .Single(action => PrototypeAiEnvironment.StableActionId(action) == parent.ActionId))).ToArray(),
                            OptionalInt(request, "parallelism") ?? 1);
                        var children = transitions.Select((transition, index) => new
                        {
                            parent = parents[index].Handle,
                            child = Store(transition.State, parents[index].Handle),
                            exactHash = CanonicalJson.Sha256(transition.State)
                        }).ToArray();
                        Write(new { requestId, ok = true, schemaId = DeterministicBatch.SchemaId, children });
                        break;
                    }

                    case "reset":
                    {
                        var seed = RequiredString(request, "seed");
                        var ascension =
                            OptionalInt(request, "ascension")
                            ?? 0;
                        var state = environment.Reset(
                            seed,
                            ascension);
                        var handle = Store(state);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            stateHandle = handle,
                            exactHash = CanonicalJson.Sha256(state),
                            terminal = state.Phase == RunPhase.Terminal
                        });
                        break;
                    }

                    case "reset_native_overgrowth":
                    {
                        // A genuine live-run root, never a hypothetical search
                        // state. Use the factory itself so Neow, map layout,
                        // encounter pools and reward odds are initialized in
                        // exactly the same path as native-structure CLI runs.
                        var seed = RequiredString(request, "seed");
                        var ascension = OptionalInt(request, "ascension") ?? 0;
                        var state = PrototypeNativeOvergrowthRunFactory.Create(
                            seed, ascension);
                        var handle = Store(state);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            stateHandle = handle,
                            schemaId = NativeOvergrowthResetId
                        });
                        break;
                    }

                    case "reset_factorized_hypothetical":
                    {
                        if (!request.TryGetProperty("initial_streams", out var streams)
                            || streams.ValueKind != JsonValueKind.Object)
                        {
                            throw new ArgumentException(
                                "initial_streams must contain six named hexadecimal states.");
                        }

                        var initialStreams = new Dictionary<string, string>(
                            StringComparer.Ordinal);
                        foreach (var property in streams.EnumerateObject())
                        {
                            if (property.Value.ValueKind != JsonValueKind.String
                                || !initialStreams.TryAdd(
                                    property.Name, property.Value.GetString()!))
                            {
                                throw new ArgumentException(
                                    "Invalid or duplicated factorized RNG stream field.");
                            }
                        }

                        var ascension = OptionalInt(request, "ascension") ?? 0;
                        var state = PrototypeFactorizedRunFactory.Create(
                            initialStreams, ascension);
                        var handle = Store(state, rootHypothetical: true);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            stateHandle = handle,
                            hypothetical = true,
                            schemaId = PrototypeFactorizedRunFactory.SchemaId
                        });
                        break;
                    }

                    case "reset_hypothetical":
                    {
                        // Separate declared search-seed namespace from live resets.
                        // The caller MUST generate search_seed independently of
                        // any actual game's seed or internal RNG cursor.
                        var searchSeed = RequiredString(request, "search_seed");
                        if (searchSeed.Length != 32
                            || searchSeed.Any(ch => !Uri.IsHexDigit(ch)))
                        {
                            throw new ArgumentException(
                                "search_seed must be an independent 128-bit hex value.");
                        }
                        var ascension = OptionalInt(request, "ascension") ?? 0;
                        var state = environment.Reset(
                            $"fair-hypothetical:{searchSeed.ToLowerInvariant()}", ascension);
                        var handle = Store(state, rootHypothetical: true);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            stateHandle = handle,
                            terminal = state.Phase == RunPhase.Terminal,
                            hypothetical = true
                        });
                        break;
                    }

                    case "hypothetical_draw_order":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var state = RequireState(request);
                        if (!hypotheticalHandles.Contains(parentHandle)
                            || !freshCombatHandles.Contains(parentHandle))
                        {
                            throw new InvalidOperationException(
                                "Draw-order injection requires a declared hypothetical "
                                + "fresh-combat handle; live, oracle and previously "
                                + "acted-on combat states are ineligible.");
                        }
                        if (!request.TryGetProperty("ordered_cards", out var cards)
                            || cards.ValueKind != JsonValueKind.Array)
                        {
                            throw new ArgumentException(
                                "ordered_cards must be an array of public card variants.");
                        }
                        var order = cards.EnumerateArray().Select(card =>
                        {
                            if (card.ValueKind != JsonValueKind.Object)
                            {
                                throw new ArgumentException(
                                    "Every ordered_cards entry must be an object.");
                            }
                            return new PrototypePublicDrawCard(
                                RequiredString(card, "card_id"),
                                OptionalInt(card, "upgrade_level")
                                ?? throw new ArgumentException(
                                    "Every ordered card requires upgrade_level."));
                        }).ToArray();
                        var branch = PrototypeHypotheticalDrawOrder.Apply(state, order);
                        var child = Store(branch, parentHandle);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            child,
                            schemaId = PrototypeHypotheticalDrawOrder.SchemaId,
                            // No exact-state hash or hidden card instances in
                            // this operation's response.
                            policyId = FairPolicyId,
                            observationHash = environment.Observe(branch).ObservationHash
                        });
                        break;
                    }

                    case "condition_combat_entry":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var state = RequireState(request);
                        if (!hypotheticalHandles.Contains(parentHandle))
                        {
                            throw new InvalidOperationException(
                                "Only independent hypothetical source handles "
                                + "may condition a combat-entry stream.");
                        }

                        var actionId = RequiredString(request, "action_id");
                        var publicObservationHash = RequiredString(
                            request, "expected_observation_hash");
                        var searchSeed = RequiredString(request, "search_seed");
                        var budget = OptionalInt(request, "max_candidates") ?? 256;
                        if (!request.TryGetProperty(
                                "expected_legal_action_ids", out var legalIds)
                            || legalIds.ValueKind != JsonValueKind.Array)
                        {
                            throw new ArgumentException(
                                "Expected public legal action IDs must be an array.");
                        }
                        var expectedActions = legalIds.EnumerateArray().Select(item =>
                        {
                            if (item.ValueKind != JsonValueKind.String)
                            {
                                throw new ArgumentException(
                                    "Every expected legal action ID must be a string.");
                            }
                            return item.GetString()!;
                        }).ToArray();

                        var conditioned = PrototypeConditionedCombatEntry.Sample(
                            state,
                            actionId,
                            publicObservationHash,
                            expectedActions,
                            searchSeed,
                            budget);
                        var child = Store(conditioned.State, parentHandle);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            child,
                            schemaId = PrototypeConditionedCombatEntry.SchemaId,
                            policyId = FairPolicyId,
                            observationHash = publicObservationHash,
                            candidates = conditioned.Candidates
                        });
                        break;
                    }

                    case "propose_pristine_reward":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var state = RequireState(request);
                        if (!hypotheticalHandles.Contains(parentHandle))
                        {
                            throw new InvalidOperationException(
                                "Only independently created hypothetical handles "
                                + "may branch the pristine reward stream.");
                        }

                        var actionId = RequiredString(request, "action_id");
                        var initialHex = RequiredString(request, "reward_initial_state_hex");
                        var next = PrototypePristineRewardProposal.Branch(
                            state, actionId, initialHex);
                        var child = Store(next, parentHandle);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            child,
                            schemaId = PrototypePristineRewardProposal.SchemaId,
                            policyId = FairPolicyId
                        });
                        break;
                    }

                    case "legal_actions":
                    {
                        var state = RequireState(request);
                        var frame = environment.Observe(state);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            actions = frame.LegalActions.Select(ActionWire).ToArray()
                        });
                        break;
                    }

                    case "observe":
                    {
                        var state = RequireState(request);
                        var policyId = RequiredString(request, "policy_id");
                        if (!StringComparer.Ordinal.Equals(policyId, FairPolicyId))
                        {
                            throw new NotSupportedException(
                                $"Prototype JSONL bridge supports only information policy '{FairPolicyId}'.");
                        }

                        var frame = environment.Observe(state);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            policyId,
                            payloadJson = CanonicalJson.Serialize(frame.Observation),
                            observationHash = frame.ObservationHash,
                            schemaId = frame.SchemaId
                        });
                        break;
                    }

                    case "step":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var state = states.TryGetValue(parentHandle, out var found)
                            ? found
                            : throw new InvalidOperationException(
                                $"Unknown state handle '{parentHandle}'.");
                        var actionId = RequiredString(request, "action_id");
                        var frame = environment.Observe(state);
                        var action = frame.LegalActions.SingleOrDefault(candidate =>
                            StringComparer.Ordinal.Equals(candidate.ActionId, actionId))
                            ?? throw new InvalidOperationException(
                                $"Action '{actionId}' is not legal in state '{parentHandle}'.");

                        var next = environment.Step(state, actionId).State;
                        var childHandle = Store(next, parentHandle);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            parent = parentHandle,
                            action = ActionWire(action),
                            child = childHandle,
                            terminal = next.Phase == RunPhase.Terminal,
                            exactHash = CanonicalJson.Sha256(next)
                        });
                        break;
                    }

                    case "expand":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var state = states.TryGetValue(parentHandle, out var found)
                            ? found
                            : throw new InvalidOperationException(
                                $"Unknown state handle '{parentHandle}'.");

                        var expansions = environment.Expand(state)
                            .Select(expansion =>
                            {
                                var childHandle = Store(expansion.State, parentHandle);
                                return new
                                {
                                    parent = parentHandle,
                                    action = ActionWire(expansion.Action),
                                    child = childHandle,
                                    terminal = expansion.State.Phase == RunPhase.Terminal,
                                    exactHash = expansion.CanonicalStateHash
                                };
                            })
                            .ToArray();

                        Write(new
                        {
                            requestId,
                            ok = true,
                            expansions
                        });
                        break;
                    }

                    case "batch_step":
                    {
                        if (!request.TryGetProperty("items", out var itemsElement)
                            || itemsElement.ValueKind != JsonValueKind.Array)
                        {
                            throw new InvalidOperationException(
                                "Request field 'items' must be an array.");
                        }

                        var transitions = itemsElement.EnumerateArray()
                            .Select(item =>
                            {
                                if (item.ValueKind != JsonValueKind.Object)
                                {
                                    throw new InvalidOperationException(
                                        "Every batch_step item must be an object.");
                                }

                                var parentHandle = RequiredString(item, "state_handle");
                                var actionId = RequiredString(item, "action_id");
                                var state = states.TryGetValue(parentHandle, out var found)
                                    ? found
                                    : throw new InvalidOperationException(
                                        $"Unknown state handle '{parentHandle}'.");

                                var frame = environment.Observe(state);
                                var action = frame.LegalActions.SingleOrDefault(candidate =>
                                    StringComparer.Ordinal.Equals(candidate.ActionId, actionId))
                                    ?? throw new InvalidOperationException(
                                        $"Action '{actionId}' is not legal in state '{parentHandle}'.");

                                var next = environment.Step(state, actionId).State;
                                var childHandle = Store(next, parentHandle);
                                return new
                                {
                                    parent = parentHandle,
                                    action = ActionWire(action),
                                    child = childHandle,
                                    terminal = next.Phase == RunPhase.Terminal,
                                    exactHash = CanonicalJson.Sha256(next)
                                };
                            })
                            .ToArray();

                        Write(new
                        {
                            requestId,
                            ok = true,
                            transitions
                        });
                        break;
                    }

                    case "batch_step_frame":
                    {
                        var policyId = RequiredString(request, "policy_id");
                        if (!StringComparer.Ordinal.Equals(policyId, FairPolicyId))
                        {
                            throw new NotSupportedException(
                                $"Prototype JSONL bridge supports only information policy '{FairPolicyId}'.");
                        }

                        if (!request.TryGetProperty("items", out var itemsElement)
                            || itemsElement.ValueKind != JsonValueKind.Array)
                        {
                            throw new InvalidOperationException(
                                "Request field 'items' must be an array.");
                        }

                        var frames = itemsElement.EnumerateArray()
                            .Select(item =>
                            {
                                if (item.ValueKind != JsonValueKind.Object)
                                {
                                    throw new InvalidOperationException(
                                        "Every batch_step_frame item must be an object.");
                                }

                                var parentHandle = RequiredString(item, "state_handle");
                                var actionId = RequiredString(item, "action_id");
                                var state = states.TryGetValue(parentHandle, out var found)
                                    ? found
                                    : throw new InvalidOperationException(
                                        $"Unknown state handle '{parentHandle}'.");

                                var stepped = environment.StepFrame(state, actionId);
                                var childHandle = Store(stepped.State, parentHandle);
                                var frame = stepped.Frame;
                                return new
                                {
                                    parent = parentHandle,
                                    action = ActionWire(stepped.Action),
                                    child = childHandle,
                                    terminal = stepped.State.Phase == RunPhase.Terminal,
                                    exactHash = frame.CanonicalStateHash,
                                    policyId,
                                    payloadJson = CanonicalJson.Serialize(frame.Observation),
                                    observationHash = frame.ObservationHash,
                                    schemaId = frame.SchemaId,
                                    legalActions = frame.LegalActions.Select(ActionWire).ToArray()
                                };
                            })
                            .ToArray();

                        Write(new
                        {
                            requestId,
                            ok = true,
                            frames
                        });
                        break;
                    }

                    case "batch_rollout_step_frame":
                    {
                        var policyId = RequiredString(request, "policy_id");
                        if (!StringComparer.Ordinal.Equals(policyId, FairPolicyId))
                        {
                            throw new NotSupportedException(
                                $"Prototype JSONL bridge supports only information policy '{FairPolicyId}'.");
                        }

                        if (!request.TryGetProperty("items", out var itemsElement)
                            || itemsElement.ValueKind != JsonValueKind.Array)
                        {
                            throw new InvalidOperationException(
                                "Request field 'items' must be an array.");
                        }

                        var frames = itemsElement.EnumerateArray()
                            .Select(item =>
                            {
                                if (item.ValueKind != JsonValueKind.Object)
                                {
                                    throw new InvalidOperationException(
                                        "Every batch_rollout_step_frame item must be an object.");
                                }

                                var parentHandle = RequiredString(item, "state_handle");
                                var actionId = RequiredString(item, "action_id");
                                var state = states.TryGetValue(parentHandle, out var found)
                                    ? found
                                    : throw new InvalidOperationException(
                                        $"Unknown state handle '{parentHandle}'.");

                                var stepped = environment.RolloutStepFrame(state, actionId);
                                var childHandle = Store(stepped.State, parentHandle);
                                return new
                                {
                                    parent = parentHandle,
                                    child = childHandle,
                                    terminal = stepped.State.Phase == RunPhase.Terminal,
                                    policyId,
                                    schemaId = PrototypeAiEnvironment.SchemaId,
                                    payloadJson = CanonicalJson.Serialize(stepped.Observation),
                                    legalActions = stepped.LegalActions.Select(ActionWire).ToArray()
                                };
                            })
                            .ToArray();

                        Write(new
                        {
                            requestId,
                            ok = true,
                            frames
                        });
                        break;
                    }

                    case "batch_observe":
                    {
                        var policyId = RequiredString(request, "policy_id");
                        if (!StringComparer.Ordinal.Equals(policyId, FairPolicyId))
                        {
                            throw new NotSupportedException(
                                $"Prototype JSONL bridge supports only information policy '{FairPolicyId}'.");
                        }

                        if (!request.TryGetProperty("state_handles", out var handlesElement)
                            || handlesElement.ValueKind != JsonValueKind.Array)
                        {
                            throw new InvalidOperationException(
                                "Request field 'state_handles' must be an array.");
                        }

                        var observations = handlesElement.EnumerateArray()
                            .Select(element =>
                            {
                                if (element.ValueKind != JsonValueKind.String)
                                {
                                    throw new InvalidOperationException(
                                        "Every state_handles entry must be a string.");
                                }

                                var handle = element.GetString()
                                    ?? throw new InvalidOperationException(
                                        "state_handles entry is null.");
                                var state = states.TryGetValue(handle, out var found)
                                    ? found
                                    : throw new InvalidOperationException(
                                        $"Unknown state handle '{handle}'.");
                                var frame = environment.Observe(state);
                                return new
                                {
                                    stateHandle = handle,
                                    policyId,
                                    payloadJson = CanonicalJson.Serialize(frame.Observation),
                                    observationHash = frame.ObservationHash,
                                    schemaId = frame.SchemaId
                                };
                            })
                            .ToArray();

                        Write(new
                        {
                            requestId,
                            ok = true,
                            observations
                        });
                        break;
                    }

                    case "batch_expand":
                    {
                        if (!request.TryGetProperty("state_handles", out var handlesElement)
                            || handlesElement.ValueKind != JsonValueKind.Array)
                        {
                            throw new InvalidOperationException(
                                "Request field 'state_handles' must be an array.");
                        }

                        var handles = handlesElement.EnumerateArray()
                            .Select(element =>
                                element.ValueKind == JsonValueKind.String
                                    ? element.GetString()
                                    : throw new InvalidOperationException(
                                        "Every state_handles entry must be a string."))
                            .Select(handle => handle
                                ?? throw new InvalidOperationException(
                                    "state_handles entry is null."))
                            .ToArray();

                        var batches = handles.Select(parentHandle =>
                        {
                            var state = states.TryGetValue(parentHandle, out var found)
                                ? found
                                : throw new InvalidOperationException(
                                    $"Unknown state handle '{parentHandle}'.");

                            var expansions = environment.Expand(state)
                                .Select(expansion =>
                                {
                                    var childHandle = Store(expansion.State, parentHandle);
                                    return new
                                    {
                                        parent = parentHandle,
                                        action = ActionWire(expansion.Action),
                                        child = childHandle,
                                        terminal = expansion.State.Phase == RunPhase.Terminal,
                                        exactHash = expansion.CanonicalStateHash
                                    };
                                })
                                .ToArray();

                            return new
                            {
                                parent = parentHandle,
                                expansions
                            };
                        }).ToArray();

                        Write(new
                        {
                            requestId,
                            ok = true,
                            batches
                        });
                        break;
                    }

                    case "release_many":
                    {
                        if (!request.TryGetProperty("state_handles", out var handlesElement)
                            || handlesElement.ValueKind != JsonValueKind.Array)
                        {
                            throw new InvalidOperationException(
                                "Request field 'state_handles' must be an array.");
                        }

                        var released = 0;
                        foreach (var element in handlesElement.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String)
                            {
                                throw new InvalidOperationException(
                                    "Every state_handles entry must be a string.");
                            }

                            var handle = element.GetString()
                                ?? throw new InvalidOperationException(
                                    "state_handles entry is null.");
                            if (states.Remove(handle))
                            {
                                hypotheticalHandles.Remove(handle);
                                freshCombatHandles.Remove(handle);
                                released++;
                            }
                        }

                        Write(new
                        {
                            requestId,
                            ok = true,
                            released
                        });
                        break;
                    }

                    case "fork":
                    {
                        var parentHandle = RequiredString(request, "state_handle");
                        var state = RequireState(request);
                        var child = Store(environment.Fork(state), parentHandle);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            child,
                            exactHash = CanonicalJson.Sha256(states[child])
                        });
                        break;
                    }

                    case "exact_hash":
                    {
                        var state = RequireState(request);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            exactHash = CanonicalJson.Sha256(state)
                        });
                        break;
                    }

                    case "is_terminal":
                    {
                        var state = RequireState(request);
                        Write(new
                        {
                            requestId,
                            ok = true,
                            terminal = state.Phase == RunPhase.Terminal
                        });
                        break;
                    }

                    case "close":
                        Write(new { requestId, ok = true });
                        return;

                    default:
                        throw new InvalidOperationException($"Unknown JSONL operation '{operation}'.");
                }
            }
            catch (Exception exception)
            {
                Write(new
                {
                    requestId,
                    ok = false,
                    errorType = exception.GetType().Name,
                    error = exception.Message
                });
            }
        }
    }

    private static object ActionWire(PrototypeAiAction action) =>
        new
        {
            actionId = action.ActionId,
            kind = action.Kind,
            payloadJson = CanonicalJson.Serialize(action.Payload)
        };

    private static string RequiredString(JsonElement request, string name)
    {
        if (!request.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Request field '{name}' must be a string.");
        }

        return value.GetString()
            ?? throw new InvalidOperationException($"Request field '{name}' is null.");
    }

    private static long RequiredLong(JsonElement request, string name)
    {
        if (!request.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out var result))
            throw new InvalidOperationException(
                $"Request field '{name}' must be an integer.");
        return result;
    }

    private static ulong RequiredULong(JsonElement request, string name)
    {
        if (!request.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetUInt64(out var result))
            throw new InvalidOperationException(
                $"Request field '{name}' must be a non-negative integer.");
        return result;
    }

    private static ulong? OptionalULong(JsonElement request, string name)
    {
        if (!request.TryGetProperty(name, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetUInt64(out var result))
            throw new InvalidOperationException(
                $"Request field '{name}' must be a non-negative integer.");
        return result;
    }

    private static bool OptionalBool(JsonElement request, string name)
    {
        if (!request.TryGetProperty(name, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return false;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new ArgumentException(
                "Request field '" + name + "' must be a boolean.")
        };
    }

    private static int? OptionalInt(
        JsonElement request,
        string name)
    {
        if (!request.TryGetProperty(name, out var value)
            || value.ValueKind is
                JsonValueKind.Null
                or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var result))
        {
            throw new InvalidOperationException(
                $"Request field '{name}' must be an integer.");
        }

        return result;
    }

    private static string? OptionalString(JsonElement request, string name)
    {
        if (!request.TryGetProperty(name, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Request field '{name}' must be a string.");
        }

        return value.GetString();
    }

    private static string[]? OptionalStringArray(
        JsonElement request, string name)
    {
        if (!request.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind != JsonValueKind.Array)
            throw new ArgumentException(
                $"Request field '{name}' must be an array of strings.");
        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(item.GetString()))
                throw new ArgumentException(
                    $"Request field '{name}' must contain nonempty strings.");
            items.Add(item.GetString()!);
        }
        return items.ToArray();
    }
}
