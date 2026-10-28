namespace Sts2Emulator.Core;

/// <summary>
/// H8's live boundary identity. No canonical serialization or cryptographic
/// hash participates in normal local stale-decision rejection. Tokens from a
/// different fork are never interchangeable even at the same version.
/// </summary>
public readonly record struct H8BoundaryTokenV1(Guid RuntimeId, long Version);

/// <summary>
/// Compact ordered player/Nature journal. Physical IDs and provider identity
/// remain explicit so a script can reproduce choices without a private RNG
/// seed. The journal does not store copied combat states.
/// </summary>
public sealed record H8DecisionEntryV1(
    long Version, string Kind, string ProviderId,
    string? SiteId = null, string? ActionId = null,
    string? IntentId = null, long? CardInstanceId = null);

/// <summary>
/// First executable H8 combat runtime slice.
///
/// Transitional mechanics adapter: v111 remains the live run mechanics owner
/// while H8 establishes session-local versioned decisions and its own compact
/// journal. Only the controlled physical card-draw route is trusted-fast in
/// this slice; player actions and intent decisions still use the checked v111
/// bridge. M2 will replace the nested v111 states with a single H8 mechanics
/// owner/cursor. Do not treat this bridge as the completed H8 architecture.
/// </summary>
public sealed class H8CombatRuntimeV1
{
    private readonly SemanticSeededEntryV111 _entry;
    private readonly List<H8DecisionEntryV1> _journal;
    private readonly Guid _runtimeId;
    private readonly bool _useM2AutomaticDraw;
    private readonly bool _useNativeDrawTransition;
    private readonly bool _useNativePlayerEndTurn;
    private readonly bool _useTrustedIntent;
    private SemanticPilotCombatV111 _state;
    private long _version;
    // A projected automatic-hand-draw continuation belongs to exactly one
    // H8 boundary version. Repeated status/support queries and the eventual
    // selection all reuse the same projection; it is never copied to a fork.
    private H8DrawCursorV1? _drawCursor;
    private bool _drawCursorCaptured;
    // A single source-public H7 observation+menu projection per committed
    // player boundary, shared by H8Status and h8_observe. Do not recompute
    // observation hashing/action projection two or three times per step.
    private PrototypeAiFrame? _playerFrame;
    // Exact pre-choice pilot fingerprint reused when the same version-owned
    // intent Chance Law has already been exposed to the external provider.
    private string? _pendingIntentPilotFingerprint;
    // One typed boundary discriminator per committed H8 version. Resolve
    // cheap phase/intent/direct-hand-draw markers before asking v111 for
    // its heavyweight derived pending-choice projections. A failed choice
    // never invalidates this cache; Commit always does.
    private H8PendingKindV1? _pendingBoundaryKind;
    private H8IntentSupportV1? _intentSupport;

    private enum H8PendingKindV1
    {
        Terminal, DrawCard, EnemyIntent, PlayerChoice, Player, Unsupported
    }

    private sealed record H8IntentSupportV1(string[] Ids, long[] RawWeights);

    private sealed record H8DrawCursorV1(
        SemanticDeckEventV111 Event, long[] EligibleCards,
        SemanticEndTurnV111? AutomaticEndTurn,
        bool CanUseFastAutomaticDraw);

    private H8CombatRuntimeV1(
        SemanticSeededEntryV111 entry,
        SemanticPilotCombatV111 state,
        Guid runtimeId,
        long version,
        List<H8DecisionEntryV1> journal,
        bool useM2AutomaticDraw,
        bool useNativeDrawTransition,
        bool useNativePlayerEndTurn,
        bool useTrustedIntent)
    {
        _entry = entry;
        _useM2AutomaticDraw = useM2AutomaticDraw;
        _useNativeDrawTransition = useNativeDrawTransition;
        _useNativePlayerEndTurn = useNativePlayerEndTurn;
        _useTrustedIntent = useTrustedIntent;
        // H8's version-owned journal already records every player/Nature
        // selection. Keep only an immutable legacy continuation trail in
        // the live v111 adapter so long fights do not copy History on each
        // committed draw/card play. Explicit audit/fork still materializes.
        _state = state.H8HistoryTrail is null
            ? state with
            {
                H8HistoryTrail = H8HistoryTrailV1.FromHistory(state.History)
            }
            : state;
        _runtimeId = runtimeId;
        _version = version;
        _journal = journal;
    }

    public static H8CombatRuntimeV1 BeginSeeded(
        SemanticSeededEntryV111 entry, bool useM2AutomaticDraw = true,
        bool useNativeDrawTransition = true,
        bool useNativePlayerEndTurn = true,
        bool useTrustedIntent = true)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var state = SemanticPilotCombatEngineV111.BeginSeeded(entry);
        return new H8CombatRuntimeV1(
            entry, state, Guid.NewGuid(), 0,
            new List<H8DecisionEntryV1>(), useM2AutomaticDraw,
            useNativeDrawTransition, useNativePlayerEndTurn,
            useTrustedIntent);
    }

    public H8BoundaryTokenV1 Boundary =>
        new(_runtimeId, _version);

    public bool Terminal => _state.CombatTerminal;
    public string? Outcome => Terminal ? _state.CombatOutcome : null;
    public int? ExitHp => Terminal ? _state.Run.Player.Hp : null;
    public int? TerminalEnemyHp => Terminal &&
        _state.CombatOutcome == "defeat"
        ? _state.Run.World?.DefeatRemainingEnemyHp
        : null;
    public bool AtPlayerBoundary =>
        PendingBoundaryKind() is H8PendingKindV1.Player or
            H8PendingKindV1.PlayerChoice;
    public bool AtPlayerChoiceBoundary =>
        PendingBoundaryKind() == H8PendingKindV1.PlayerChoice;
    public bool HasPendingCardDraw =>
        PendingBoundaryKind() == H8PendingKindV1.DrawCard;
    public bool HasPendingIntent =>
        PendingBoundaryKind() == H8PendingKindV1.EnemyIntent;

    /// <summary>
    /// Stable boundary tag for this version. H8Status and the subsequent
    /// explicit player/Nature provider invocation share one classification
    /// without scanning legacy continuation properties again.
    /// </summary>
    public string BoundaryKind => PendingBoundaryKind() switch
    {
        H8PendingKindV1.Terminal => "terminal",
        H8PendingKindV1.DrawCard => "draw_card",
        H8PendingKindV1.EnemyIntent => "enemy_intent",
        H8PendingKindV1.PlayerChoice => "player_choice",
        H8PendingKindV1.Player => "player",
        _ => "unsupported"
    };

    private H8PendingKindV1 PendingBoundaryKind()
    {
        if (_pendingBoundaryKind is { } kind)
            return kind;
        using var scope = H8HotPathProfilerV1.Begin("h8.boundary.classify");
        var endTurn = _state.PendingEndTurn;
        // The direct automatic hand-draw ticket requires no v111 derived
        // event projection. This must be tested before intent and player
        // kinds to preserve the original H8 boundary precedence.
        if (_state.CombatTerminal)
            kind = H8PendingKindV1.Terminal;
        else if (endTurn?.HandDraw?.Pending is not null)
            kind = H8PendingKindV1.DrawCard;
        else if (_state.PendingInitialIntents?.Pending is not null ||
                 endTurn?.IntentPending is not null)
            kind = H8PendingKindV1.EnemyIntent;
        else if (_state.AtPlayerBoundary)
            kind = H8PendingKindV1.Player;
        else if (_state.AtPlayerChoiceBoundary)
            kind = H8PendingKindV1.PlayerChoice;
        else if (CurrentDrawCursor() is not null)
            kind = H8PendingKindV1.DrawCard;
        else
            kind = H8PendingKindV1.Unsupported;
        _pendingBoundaryKind = kind;
        return kind;
    }

    // O(1) on every status response. A materialized journal is audit-only.
    public int DecisionCount => _journal.Count;

    public IReadOnlyList<H8DecisionEntryV1> DecisionHistory =>
        Array.AsReadOnly(_journal.ToArray());

    /// <summary>
    /// Observe only the source-visible player frame. This is the identical
    /// public observation/action encoder consumed by the H7 Transformer,
    /// with no legacy pilot snapshot, JSON round trip, or pilot fingerprint.
    /// The H8 player boundary must be real; future H8 work will expose
    /// nested selection actions through a tagged cursor.
    /// </summary>
    public PrototypeAiFrame ObservePlayerFrame()
    {
        if (!AtPlayerBoundary)
            throw new InvalidOperationException(
                "H8 public player observation requires a full player boundary.");
        if (_playerFrame is null)
        {
            using var scope = H8HotPathProfilerV1.Begin("h8.observe.public_frame");
            _playerFrame = new PrototypeAiEnvironment().ObserveH8(_state.Run);
        }
        return _playerFrame;
    }

    public string[] LegalPlayerActionIds()
    {
        if (!AtPlayerBoundary)
            throw new InvalidOperationException(
                "H8 player actions require a player boundary.");
        // Pending nested player selections remain checked-v111 mechanics;
        // the H7 player must restrict this full public menu to the exact
        // pending choice support before sampling.
        return ObservePlayerFrame()
            .LegalActions.Select(action => action.ActionId).ToArray();
    }

    public string EndTurnActionId()
    {
        if (PendingBoundaryKind() != H8PendingKindV1.Player)
            throw new InvalidOperationException(
                "End turn is available only at a full player boundary.");
        return ObservePlayerFrame()
            .LegalActions.Single(action => action.Kind == "end_turn").ActionId;
    }

    /// <summary>
    /// Exact event-scoped v111 reference Chance Law at an H8 Nature boundary.
    /// Only called by the explicit h8_reference_law API, not by h8_status
    /// or by the native hot path. It queries the live run without Fork(),
    /// a complete-state SHA, or a legacy semantic snapshot.
    ///
    /// The wire deliberately retains the same Chance Law and candidate
    /// fields as semantic pending, so the H7 DeclaredLaw and SeededLawSampler
    /// can operate unchanged. Future providers can consume this typed law
    /// without owning or serializing the mechanics state.
    /// </summary>
    public object DescribePendingReferenceLaw()
    {
        using var scope = H8HotPathProfilerV1.Begin("h8.chance_law.total");
        if (HasPendingCardDraw && CurrentDrawCursor() is { } cursor)
        {
            var drawn = cursor.Event;
            var deck = _state.Run.World?.Combat?.SemanticDeck ??
                throw new InvalidDataException(
                    "H8 reference draw lacks a provider-owned semantic deck.");
            var law = ChanceLawEngineV111.QueryDraw(deck);
            return new
            {
                kind = "draw_card",
                eventId = drawn.EventId,
                siteId = drawn.SiteId,
                streamId = drawn.StreamId,
                ownerId = drawn.OwnerId,
                ordinal = drawn.Ordinal,
                operationKind = drawn.OperationKind,
                // The declared law has already computed this exact deck
                // fingerprint. Reuse it instead of serializing the growing
                // provider transcript a second time on every draw request.
                constraintFingerprint = law.ConditioningFingerprint,
                probabilityStatus = drawn.ProbabilityStatus,
                chanceLaw = law,
                candidates = drawn.CandidateCardInstanceIds.Select(id =>
                    new { cardInstanceId = id }).ToArray()
            };
        }
        if (_state.IntentPending is { } intent)
        {
            var evt = SemanticPilotCombatEngineV111
                .DescribePendingIntentEvent(
                    _state,
                    _pendingIntentPilotFingerprint ??= _state.Fingerprint);
            var support = evt.Support;
            var law = ChanceLawEngineV111.QueryIntent(evt);
            return new
            {
                kind = "enemy_intent",
                siteId = intent.SiteId,
                streamId = intent.StreamId,
                ownerId = intent.OwnerId,
                ordinal = intent.Ordinal,
                eventId = evt.EventId,
                operationKind = evt.OperationKind,
                probabilityStatus = law.ProbabilityStatus,
                chanceLaw = law,
                totalRawWeight = support.TotalRawWeight,
                semanticCandidates = support.Candidates.Select(candidate => new
                {
                    intentId = candidate.IntentId,
                    rawWeight = candidate.RawWeight
                }).ToArray()
            };
        }
        throw new InvalidOperationException(
            "H8 reference law requires an unresolved Nature event.");
    }

    public string[] PendingIntentIds() =>
        (string[])CurrentIntentSupport().Ids.Clone();

    private H8IntentSupportV1 CurrentIntentSupport()
    {
        if (_intentSupport is { } cached)
            return cached;
        if (PendingBoundaryKind() != H8PendingKindV1.EnemyIntent)
            throw new InvalidOperationException(
                "H8 intent support requires an active intent request.");
        using var scope = H8HotPathProfilerV1.Begin("h8.intent.support_capture");
        var intents = _state.PendingInitialIntents ??
            _state.PendingEndTurn?.Intents;
        if (intents?.Pending is null)
            throw new InvalidOperationException(
                "H8 intent query requires an active intent request.");
        var support = ControlledTurnIntentEngineV111
            .DescribePendingSemanticIntent(intents) ??
            throw new InvalidDataException("H8 intent support is absent.");
        _intentSupport = new H8IntentSupportV1(
            support.Candidates.Select(candidate => candidate.IntentId).ToArray(),
            support.Candidates.Select(candidate => (long)candidate.RawWeight).ToArray());
        return _intentSupport;
    }

    /// <summary>
    /// Preserve the *real source* weighted enemy-intent support in a compact
    /// policy-independent form. The sample source RNG is owned by Nature,
    /// never by the H7 player model.
    /// </summary>
    public long[] PendingIntentRawWeights() =>
        (long[])CurrentIntentSupport().RawWeights.Clone();

    public long[] PendingDrawCardInstanceIds()
    {
        var cursor = CurrentDrawCursor() ??
            throw new InvalidOperationException(
                "H8 draw query requires an active card-draw request.");
        // No caller may retain a mutable alias to the session's live support.
        return (long[])cursor.EligibleCards.Clone();
    }

    private H8DrawCursorV1? CurrentDrawCursor()
    {
        if (_drawCursorCaptured)
            return _drawCursor;
        using var scope = H8HotPathProfilerV1.Begin("h8.draw.cursor_capture");
        // Resolve the nested v111 end-turn projection once per boundary.
        // M2 will replace this temporary projected frame with a compact
        // tagged effect cursor owned by the new mechanics runtime.
        var frame = _state.PendingEndTurn;
        // Direct hand-draw support is already an immutable event. Only
        // fallback to the legacy derived pending properties for nonstandard
        // nested/card-effect draw events.
        var pending = frame?.HandDraw?.Pending ??
            frame?.DrawPending ?? _state.DrawPending;
        if (pending is not null)
        {
            // Only fast-path a plain physical automatic hand draw.
            // A pending cost/insertion/selection/hook remains owned by its
            // separate typed continuation and must keep the checked fallback.
            var automatic = frame is
                { HandDraw:
                    { AtBoundary: false, Pending: not null,
                      CostPending: null, RandomPileCardPending: null,
                      DrawInsertionPending: null, StratagemSelectionPending: null,
                      DrawHookChoicePending: null },
                  EnemyActionPending: null, IntentPending: null } &&
                _state.Run.World?.Combat?.PendingChoice is not
                { ChoiceId: "controlled_semantic_draw",
                  ControlledSemanticDraw: not null };
            _drawCursor = new H8DrawCursorV1(
                pending, pending.CandidateCardInstanceIds,
                automatic ? frame : null, automatic);
        }
        _drawCursorCaptured = true;
        return _drawCursor;
    }

    /// <summary>
    /// The legacy mechanics adapter still validates this player path. This
    /// will be replaced by the H8 in-place effect cursor in M2.
    /// </summary>
    public void ChoosePlayerAction(H8BoundaryTokenV1 boundary, string actionId)
    {
        using var scope = H8HotPathProfilerV1.Begin("h8.player.total");
        RequireBoundary(boundary);
        if (!AtPlayerBoundary)
            throw new InvalidOperationException(
                "H8 player action is not the next pending boundary.");
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        // Only ordinary end_turn is migrated: card plays, potions and
        // selection hooks remain checked until their native cursor exists.
        // No player mutation or journal append occurs until this succeeds.
        // The version-owned frame is also the legality/menu snapshot already
        // presented to the policy at this boundary. Reuse it for both native
        // end-turn and checked-mechanics card/selection actions; the H8 token
        // provides freshness without serializing the entire combat state.
        var frame = ObservePlayerFrame();
        var next = _useNativePlayerEndTurn
            ? H8NativePlayerEndTurnEngineV1.TryAdvance(
                _state, actionId, frame)
            : null;
        next ??= SemanticPilotCombatEngineV111
            .StepFromH8TrustedBoundary(_state, actionId, frame);
        Commit(next, new H8DecisionEntryV1(
            _version, "player", "player", ActionId: actionId));
    }

    /// <summary>
    /// Legacy checked intent bridge; records the exact semantic intent ID.
    /// M3 will route this request to a pluggable typed stochastic provider.
    /// </summary>
    public void ChooseIntent(
        H8BoundaryTokenV1 boundary, string intentId,
        string providerId = "reference-nature")
    {
        using var scope = H8HotPathProfilerV1.Begin("h8.intent.total");
        RequireBoundary(boundary);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        if (!HasPendingIntent)
            throw new InvalidOperationException(
                "H8 intent is not the next pending boundary.");
        var next = _useTrustedIntent
            ? SemanticPilotCombatEngineV111
                .ResolveIntentFromH8TrustedBoundary(
                    _state, intentId, _pendingIntentPilotFingerprint)
            : SemanticPilotCombatEngineV111.ResolveIntent(
                _state, _state.Fingerprint, intentId);
        Commit(next, new H8DecisionEntryV1(
            _version, "intent", providerId,
            SiteId: "enemy_intent", IntentId: intentId));
    }

    /// <summary>
    /// First trusted-fast H8 path. The session token proves the request is
    /// being applied to the current in-memory boundary, so only its physical
    /// card support is checked. The legacy engine currently performs the
    /// actual card/hook/continuation mechanics and output validation.
    /// </summary>
    public void ChooseDrawCard(
        H8BoundaryTokenV1 boundary, long cardInstanceId,
        string providerId = "reference-nature")
    {
        using var scope = H8HotPathProfilerV1.Begin("h8.draw.total");
        RequireBoundary(boundary);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        var cursor = CurrentDrawCursor() ??
            throw new InvalidOperationException(
                "H8 draw is not the next pending boundary.");
        var selectedIndex = Array.IndexOf(
            cursor.EligibleCards, cardInstanceId);
        if (selectedIndex < 0)
            throw new InvalidOperationException(
                "H8 card instance is outside the pending Nature support.");
        SemanticPilotCombatV111 next;
        if (_useM2AutomaticDraw && cursor.CanUseFastAutomaticDraw)
        {
            next = _useNativeDrawTransition
                ? H8NativePhysicalDrawEngineV1.AdvanceAutomaticHandDraw(
                    _state, cursor.AutomaticEndTurn!, cursor.Event, selectedIndex)
                : SemanticPilotCombatEngineV111.ResolveFromH8AutomaticDrawCursor(
                    _state, cursor.AutomaticEndTurn!, cursor.Event, selectedIndex);
        }
        else
            next = SemanticPilotCombatEngineV111.ResolveFromH8TrustedDrawBoundary(
                _state, cardInstanceId);
        Commit(next, new H8DecisionEntryV1(
            _version, "draw_card", providerId,
            SiteId: cursor.Event.SiteId, CardInstanceId: cardInstanceId));
    }

    /// <summary>
    /// Consume consecutive reference-owned stochastic events inside the
    /// mechanics process, stopping at the next player/terminal/unsupported
    /// boundary. An externally controlled learned or scripted Nature provider
    /// continues to use ChooseIntent / ChooseDrawCard instead.
    ///
    /// Each resolved choice still passes the same trusted H8 mechanics
    /// resolver and appends the identical journal entry structure. Only the
    /// reference distribution's actual random sample moves into C#.
    /// </summary>
    public int AdvanceLocalReferenceNature(
        H8BoundaryTokenV1 boundary, long seed, int maxNatureDecisions = 1200)
    {
        RequireBoundary(boundary);
        if (maxNatureDecisions <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxNatureDecisions));
        var resolved = 0;
        while (resolved < maxNatureDecisions)
        {
            var kind = PendingBoundaryKind();
            if (kind == H8PendingKindV1.DrawCard)
            {
                var cursor = CurrentDrawCursor() ??
                    throw new InvalidDataException("H8 draw cursor absent.");
                var selected = H8LocalReferenceNatureV1.UniformIndex(
                    seed, cursor.Event.EventId, cursor.Event.SiteId,
                    cursor.EligibleCards.Length);
                ChooseDrawCard(Boundary, cursor.EligibleCards[selected],
                    H8LocalReferenceNatureV1.ProviderId);
            }
            else if (kind == H8PendingKindV1.EnemyIntent)
            {
                // Use the identical source identity exposed by the v111
                // event-scoped Chance Law; reuse its fingerprint during the
                // ensuing trusted resolver to avoid rehashing.
                var evt = SemanticPilotCombatEngineV111
                    .DescribePendingIntentEvent(
                        _state,
                        _pendingIntentPilotFingerprint ??= _state.Fingerprint);
                var support = CurrentIntentSupport();
                var selected = H8LocalReferenceNatureV1.WeightedIntentIndex(
                    seed, evt.EventId, evt.SiteId, support.RawWeights);
                ChooseIntent(Boundary, support.Ids[selected],
                    H8LocalReferenceNatureV1.ProviderId);
            }
            else
            {
                return resolved;
            }
            resolved++;
        }
        if (PendingBoundaryKind() is H8PendingKindV1.DrawCard or
            H8PendingKindV1.EnemyIntent)
            throw new InvalidOperationException(
                "H8 local Nature continuation exceeded its decision limit.");
        return resolved;
    }

    /// <summary>
    /// Branch from the same logical boundary. A fork has a distinct live ID;
    /// both mechanics and journal must advance independently from this point.
    /// Deep forking is currently handled by v111 until H8 checkpointing.
    /// </summary>
    public H8CombatRuntimeV1 Fork() =>
        new(_entry, _state.Fork(), Guid.NewGuid(), _version,
            new List<H8DecisionEntryV1>(_journal), _useM2AutomaticDraw,
            _useNativeDrawTransition, _useNativePlayerEndTurn,
            _useTrustedIntent);

    /// <summary>
    /// Explicitly replay the journal from its source-valid seeded checkpoint,
    /// checking that the final mechanics/provenance exactly match. This does
    /// not run in the normal decision path.
    /// </summary>
    public void AuditReplay()
    {
        var replay = BeginSeeded(
            _entry, _useM2AutomaticDraw, _useNativeDrawTransition,
            _useNativePlayerEndTurn, _useTrustedIntent);
        foreach (var item in _journal)
        {
            if (replay.Boundary.Version != item.Version)
                throw new InvalidDataException(
                    "H8 journal contains a missing or reordered boundary.");
            var token = replay.Boundary;
            switch (item.Kind)
            {
                case "player" when item.ActionId is not null:
                    replay.ChoosePlayerAction(token, item.ActionId);
                    break;
                case "intent" when item.IntentId is not null:
                    replay.ChooseIntent(token, item.IntentId, item.ProviderId);
                    break;
                case "draw_card" when item.CardInstanceId is long id:
                    replay.ChooseDrawCard(token, id, item.ProviderId);
                    break;
                default:
                    throw new InvalidDataException(
                        "H8 journal contains an unknown decision kind.");
            }
        }

        if (CanonicalJson.Sha256(replay._state) !=
            CanonicalJson.Sha256(_state))
            throw new InvalidDataException(
                "H8 replay differs from committed combat mechanics/provider state.");
        if (!_journal.SequenceEqual(replay._journal))
            throw new InvalidDataException(
                "H8 replay decision journal differs.");
    }

    /// <summary>
    /// Explicitly requested audit checkpoint for paired native/checked
    /// measurements. Costly canonical serialization is performed only on
    /// demand, never during ordinary H8 state transitions or status queries.
    /// </summary>
    public string AuditStateFingerprint() => CanonicalJson.Sha256(_state);

    /// <summary>
    /// Audit-only legacy snapshot accessor. Deliberately leaves the fast
    /// choice protocol independent of the old canonical SHA fingerprint.
    /// </summary>
    public SemanticPilotCombatV111 LegacySnapshotForAudit() =>
        _state.Fork();

    private void RequireBoundary(H8BoundaryTokenV1 boundary)
    {
        if (boundary.RuntimeId != _runtimeId ||
            boundary.Version != _version)
            throw new InvalidOperationException(
                "Stale H8 combat boundary or token from another fork.");
    }

    private void Commit(
        SemanticPilotCombatV111 next, H8DecisionEntryV1 entry)
    {
        using var scope = H8HotPathProfilerV1.Begin("h8.commit");
        // All selection/legality/mechanics work completes before advancing
        // the live boundary or committing a journal entry.
        ArgumentNullException.ThrowIfNull(next);
        var nextVersion = checked(_version + 1);
        _journal.Add(entry);
        _state = next;
        _version = nextVersion;
        _drawCursor = null;
        _drawCursorCaptured = false;
        _playerFrame = null;
        _pendingIntentPilotFingerprint = null;
        _pendingBoundaryKind = null;
        _intentSupport = null;
    }
}
