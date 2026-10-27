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
    private SemanticPilotCombatV111 _state;
    private long _version;
    // A projected automatic-hand-draw continuation belongs to exactly one
    // H8 boundary version. Repeated status/support queries and the eventual
    // selection all reuse the same projection; it is never copied to a fork.
    private H8DrawCursorV1? _drawCursor;
    private bool _drawCursorCaptured;

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
        bool useNativeDrawTransition)
    {
        _entry = entry;
        _useM2AutomaticDraw = useM2AutomaticDraw;
        _useNativeDrawTransition = useNativeDrawTransition;
        _state = state;
        _runtimeId = runtimeId;
        _version = version;
        _journal = journal;
    }

    public static H8CombatRuntimeV1 BeginSeeded(
        SemanticSeededEntryV111 entry, bool useM2AutomaticDraw = true,
        bool useNativeDrawTransition = true)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var state = SemanticPilotCombatEngineV111.BeginSeeded(entry);
        return new H8CombatRuntimeV1(
            entry, state, Guid.NewGuid(), 0,
            new List<H8DecisionEntryV1>(), useM2AutomaticDraw,
            useNativeDrawTransition);
    }

    public H8BoundaryTokenV1 Boundary =>
        new(_runtimeId, _version);

    public bool Terminal => _state.CombatTerminal;
    public bool AtPlayerBoundary =>
        _state.AtPlayerBoundary || _state.AtPlayerChoiceBoundary;
    public bool HasPendingCardDraw => CurrentDrawCursor() is not null;
    public bool HasPendingIntent => _state.IntentPending is not null;

    public IReadOnlyList<H8DecisionEntryV1> DecisionHistory =>
        Array.AsReadOnly(_journal.ToArray());

    public string[] LegalPlayerActionIds()
    {
        if (!AtPlayerBoundary)
            throw new InvalidOperationException(
                "H8 player actions require a player boundary.");
        if (_state.AtPlayerChoiceBoundary)
            throw new NotSupportedException(
                "H8 player-choice sub-boundaries are pending migration.");
        return new PrototypeAiEnvironment().Observe(_state.Run)
            .LegalActions.Select(action => action.ActionId).ToArray();
    }

    public string EndTurnActionId()
    {
        if (!_state.AtPlayerBoundary)
            throw new InvalidOperationException(
                "End turn is available only at a full player boundary.");
        return new PrototypeAiEnvironment().Observe(_state.Run)
            .LegalActions.Single(action => action.Kind == "end_turn").ActionId;
    }

    public string[] PendingIntentIds()
    {
        var intents = _state.PendingInitialIntents ??
            _state.PendingEndTurn?.Intents;
        if (intents?.Pending is null)
            throw new InvalidOperationException(
                "H8 intent query requires an active intent request.");
        var support = ControlledTurnIntentEngineV111
            .DescribePendingSemanticIntent(intents) ??
            throw new InvalidDataException(
                "H8 intent support is absent.");
        return support.Candidates
            .Select(candidate => candidate.IntentId).ToArray();
    }

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
        // Resolve the nested v111 end-turn projection once per boundary.
        // M2 will replace this temporary projected frame with a compact
        // tagged effect cursor owned by the new mechanics runtime.
        var frame = _state.PendingEndTurn;
        var pending = frame?.DrawPending ?? _state.DrawPending;
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
        RequireBoundary(boundary);
        if (!AtPlayerBoundary)
            throw new InvalidOperationException(
                "H8 player action is not the next pending boundary.");
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        var next = SemanticPilotCombatEngineV111.Step(
            _state, _state.Fingerprint, actionId);
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
        RequireBoundary(boundary);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        if (!HasPendingIntent)
            throw new InvalidOperationException(
                "H8 intent is not the next pending boundary.");
        var next = SemanticPilotCombatEngineV111.ResolveIntent(
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
                ? SemanticPilotCombatEngineV111.AdvanceH8AutomaticPhysicalDraw(
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
    /// Branch from the same logical boundary. A fork has a distinct live ID;
    /// both mechanics and journal must advance independently from this point.
    /// Deep forking is currently handled by v111 until H8 checkpointing.
    /// </summary>
    public H8CombatRuntimeV1 Fork() =>
        new(_entry, _state.Fork(), Guid.NewGuid(), _version,
            new List<H8DecisionEntryV1>(_journal), _useM2AutomaticDraw,
            _useNativeDrawTransition);

    /// <summary>
    /// Explicitly replay the journal from its source-valid seeded checkpoint,
    /// checking that the final mechanics/provenance exactly match. This does
    /// not run in the normal decision path.
    /// </summary>
    public void AuditReplay()
    {
        var replay = BeginSeeded(
            _entry, _useM2AutomaticDraw, _useNativeDrawTransition);
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
        // All selection/legality/mechanics work completes before advancing
        // the live boundary or committing a journal entry.
        ArgumentNullException.ThrowIfNull(next);
        var nextVersion = checked(_version + 1);
        _journal.Add(entry);
        _state = next;
        _version = nextVersion;
        _drawCursor = null;
        _drawCursorCaptured = false;
    }
}
