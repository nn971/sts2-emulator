using System.Globalization;

namespace Sts2Emulator.Core;

/// <summary>
/// Experimental, non-fair controlled end-turn continuation rooted at a REAL
/// first-turn native-structure/prototype-RNG combat entry. This consumes
/// ordinary combat mechanics and the existing typed SemanticEndTurn resume
/// engine; no encounter or card-effect fixture is fabricated.
///
/// The independent candidate is NOT a public-history conditional posterior.
/// Audited player actions, end_turn and the listed typed Chance Law
/// boundaries are available. Every unsupported site fails closed.
/// </summary>
public sealed record H15ControlledCombatSessionV1(
    RunState Run, SemanticEndTurnV111? PendingTurn,
    string NativeEncounterEntry, long DecisionVersion,
    string CandidateModelId, ReferenceNatureStateV111 NatureState)
{
    public const string SchemaId =
        "sts2-h15-controlled-prototype-combat-endturn-experimental-v1";
    public const string FairContinuationCapability = "unavailable";
    public const string ExternalNatureProvider = "h15-external-nature-unverified-v1";
    private const string UnverifiedProvenance =
        "h15-prototype-rng-independent-candidate-not-history-conditioned-v1";

    public static H15ControlledCombatSessionV1 Begin(
        RunState source, string independentSeed)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.World?.Combat?.NativeRng is not null)
            throw new NotSupportedException(
                "H15 experimental end-turn session currently supports " +
                "prototype-combat-rng only.");
        var encounterId = source.World?.EncounterHistory?.LastOrDefault() ??
            throw new NotSupportedException(
                "Combat entry lacks a committed encounter identity.");
        // This source is the native-structure *prototype-RNG* simulator.
        // The native formation names are consulted only for encounter
        // coverage. They must not select native-specific slot rewrites:
        // e.g. prototype The Kin followers already have "quick"/"dance"
        // slots, whereas Native TheKinBoss uses "slot1"/"slot2".
        var supported = NativeActOneWeakCombatEntryV111
                .ResearchNativeEntry(encounterId)
            ?? NativeActOneNormalCombatEntryV111
                .ResearchNativeEntry(encounterId)
            ?? NativeActOneEliteBossCombatEntryV111
                .ResearchNativeEntry(encounterId);
        if (supported is null)
            throw new NotSupportedException(
                "Encounter has no pinned semantic intent mapping: " +
                encounterId);
        var prototypeEntry = "h15-prototype:" + encounterId;
        var candidate = H15NativeCombatEntryCandidateV1.Build(
            source, independentSeed);
        if (H15NativeCombatEntryCandidateV1.ProviderClass(source) !=
            H15NativeCombatEntryCandidateV1.PrototypeCombatProviderClass)
            throw new InvalidOperationException(
                "H15 experimental end turn lost prototype RNG provenance.");
        return new H15ControlledCombatSessionV1(
            candidate, null, prototypeEntry, 0,
            H15NativeCombatEntryCandidateV1.ModelId(source),
            // The session, not the player policy or an external Python
            // sampler, owns its independent ReferenceNature cursor.
            ReferenceNatureRunCursorV111.Begin(
                candidate,
                NativeRngV111.HashString(
                    independentSeed + "\nH15ReferenceNatureV111"),
                "H15 experimental combat"));
    }

    public string BoundaryKind
    {
        get
        {
            if (Run.Phase is RunPhase.Reward or RunPhase.Terminal)
                return "combat_exit";
            if (Run.Phase != RunPhase.Combat)
                return "unsupported";
            if (PendingTurn is null)
                return "player";
            var turn = PendingTurn;
            if (turn.EnemyActionEventPending is not null)
                return "enemy_action";
            if (turn.IntentPending is not null)
                return "enemy_intent";
            if (turn.DrawPending is not null)
                return "draw_card";
            if (turn.TargetPending is not null)
                return "random_target";
            if (turn.Complete && turn.AtPlayerBoundary)
                return "player";
            return "unsupported";
        }
    }

    public ChanceLawV111 PendingLaw()
    {
        var turn = PendingTurn ??
            throw new InvalidOperationException(
                "H15 Nature law requested without an active continuation.");
        ChanceLawV111 law;
        ChanceLawOutcomeV111[]? alternativeWeights = null;
        if (turn.EnemyActionEventPending is { } enemy)
        {
            law = ChanceLawEngineV111.QueryEndTurnIndex(enemy);
            alternativeWeights = law.Outcomes.Select(choice =>
                choice with { Weight = 1L }).ToArray();
        }
        else if (turn.IntentPending is { } pending)
        {
            var support = ControlledTurnIntentEngineV111
                .DescribePendingSemanticIntent(turn.Intents!) ??
                throw new InvalidDataException(
                    "Pending intent lacks source-shaped support.");
            var evt = SemanticIntentChoiceV111.CreateEvent(
                turn.Fingerprint, pending, support);
            law = ChanceLawEngineV111.QueryIntent(evt);
            alternativeWeights = support.Candidates.Select(candidate =>
                new ChanceLawOutcomeV111(
                    candidate.IntentId, candidate.RawWeight)).ToArray();
        }
        else if (turn.DrawPending is not null)
        {
            var deck = turn.Run.World?.Combat?.SemanticDeck ??
                throw new InvalidDataException(
                    "H15 suspended draw has no semantic deck.");
            law = ChanceLawEngineV111.QueryDraw(deck);
        }
        else if (turn.TargetPending is { } target)
        {
            law = ChanceLawEngineV111.QueryTarget(target);
            alternativeWeights = law.Outcomes.Select(choice =>
                choice with { Weight = 1L }).ToArray();
        }
        else
            throw new NotSupportedException(
                "UNSUPPORTED_H15_CHANCE boundary=" + BoundaryKind +
                " : continuation requires additional typed Chance Law ownership.");
        return law.WithUnverifiedProvenance(
            UnverifiedProvenance, alternativeWeights);
    }

    /// <summary>
    /// Executes one source-legal, audited player card or potion action.
    /// The existing semantic card-action adapter owns deterministic cards.
    /// Potions are restricted to source-implemented, deck-neutral effects;
    /// the controlled chance guard rejects any unowned random site.
    /// All failures leave this immutable session unchanged.
    /// </summary>
    public H15ControlledCombatSessionV1 PlayerAction(string actionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        if (BoundaryKind != "player" || PendingTurn is not null)
            throw new InvalidOperationException(
                "H15 player action requires an unsuspended player boundary.");
        // All audited, draw-pile-neutral cards AND potions share the same
        // source-mechanics replay, chance guard and semantic-deck validation.
        // H15 does not maintain a second allowlist or potion resolver.
        var advanced = SemanticPlayerActionEngineV111.Step(
            Run, actionId).Run;
        PrototypeStateInvariants.Validate(advanced);
        return this with
        {
            Run = advanced,
            DecisionVersion = checked(DecisionVersion + 1)
        };
    }

    /// <summary>
    /// Executes exactly one ordinary end_turn action through existing
    /// mechanics, stopping at its first owned Nature boundary.
    /// </summary>
    public H15ControlledCombatSessionV1 EndTurn(string actionId)
    {
        if (BoundaryKind != "player" || PendingTurn is not null)
            throw new InvalidOperationException(
                "H15 end_turn requires an unsuspended player boundary.");
        var frame = new PrototypeAiEnvironment().Observe(Run);
        var action = frame.LegalActions.SingleOrDefault(choice =>
            choice.ActionId == actionId && choice.Kind == "end_turn") ??
            throw new InvalidOperationException(
                "H15 experimental session only accepts the legal end_turn action.");
        var (continuation, advanced) =
            SemanticEndTurnEngineV111.BeginFromPlayerActionOrTerminal(
                Run, NativeEncounterEntry);
        // Complete turns leave no continuation but retain their genuine
        // next player or reward/terminal mechanics state.
        return this with
        {
            Run = advanced,
            PendingTurn = continuation is { Complete: false }
                ? continuation : null,
            DecisionVersion = checked(DecisionVersion + 1)
        };
    }

    /// <summary>
    /// The existing ReferenceNatureV111 is the sole reference sampler for
    /// H15's current, explicitly UNVERIFIED experimental Chance Law. The
    /// reference cursor, mechanics continuation, and version advance publish
    /// atomically. Scripted/learned external Choose remains available and
    /// deliberately does not consume the reference cursor.
    /// </summary>
    public (H15ControlledCombatSessionV1 Next,
        ReferenceNatureSelectionV111 Selection) ChooseReference()
    {
        NatureState.Validate();
        var law = PendingLaw();
        if (law.ProbabilityStatus != ChanceLawV111.UnverifiedStatus)
            throw new InvalidDataException(
                "H15 experimental reference law has invalid probability provenance.");
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        // SampleValidated has already checked the exact law/support.
        // Resolve that same law once; no redundant law query/hash.
        // A mechanics failure leaves the original immutable cursor intact.
        var next = ResolveDeclaredChoice(law, selection.OutcomeId);
        return (next with { NatureState = selection.NextState }, selection);
    }

    /// <summary>
    /// Advance the *existing* ReferenceNature and SemanticEndTurn owners
    /// through consecutive typed chance requests in a single transaction.
    /// Every sampled outcome receives the normal explicit event/law receipt.
    /// A failed mechanics site, unsupported boundary or exhausted event
    /// budget leaves this immutable source session and cursor unchanged.
    /// </summary>
    public (H15ControlledCombatSessionV1 Next,
        ReferenceNatureSelectionV111[] Selections) AdvanceReferenceToPlayer(
        int maxNatureDecisions)
    {
        if (maxNatureDecisions <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxNatureDecisions));
        if (BoundaryKind is not ("enemy_action" or "enemy_intent"
                or "draw_card" or "random_target"))
            throw new InvalidOperationException(
                "H15 reference continuation requires a Nature boundary.");

        var current = this;
        var selected = new List<ReferenceNatureSelectionV111>();
        while (current.BoundaryKind is "enemy_action" or "enemy_intent"
            or "draw_card" or "random_target")
        {
            if (selected.Count >= maxNatureDecisions)
                throw new NotSupportedException(
                    "UNSUPPORTED_H15_NATURE_BUDGET : " +
                    "ReferenceNature continuation reached its event limit.");
            var (next, receipt) = current.ChooseReference();
            selected.Add(receipt);
            current = next;
        }
        if (current.BoundaryKind is not ("player" or "combat_exit"))
            throw new NotSupportedException(
                "UNSUPPORTED_H15_CHANCE boundary=" + current.BoundaryKind +
                " : ReferenceNature continuation has no semantic owner.");
        return (current, selected.ToArray());
    }

    /// <summary>
    /// Nature selects an outcome from the CURRENT declared event. Reject
    /// stale IDs and unsupported sites before mutating a continuation.
    /// </summary>
    public H15ControlledCombatSessionV1 Choose(
        ChanceLawV111 advertised, string outcomeId)
    {
        ArgumentNullException.ThrowIfNull(advertised);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcomeId);
        var expected = PendingLaw();
        advertised.Validate();
        if (expected.LawId != advertised.LawId ||
            CanonicalJson.Sha256(expected) != CanonicalJson.Sha256(advertised))
            throw new InvalidOperationException(
                "H15 stale or mismatched Chance Law.");
        return ResolveDeclaredChoice(expected, outcomeId);
    }

    private H15ControlledCombatSessionV1 ResolveDeclaredChoice(
        ChanceLawV111 expected, string outcomeId)
    {
        if (!expected.Outcomes.Any(choice => choice.OutcomeId == outcomeId))
            throw new InvalidOperationException(
                "Nature selected an outcome outside the declared support.");

        var turn = PendingTurn!;
        int selectedIndex;
        if (BoundaryKind == "enemy_intent")
        {
            var support = ControlledTurnIntentEngineV111
                .DescribePendingSemanticIntent(turn.Intents!)!;
            selectedIndex = support.Candidates.Single(candidate =>
                candidate.IntentId == outcomeId).TicketStart;
        }
        else if (BoundaryKind == "random_target")
        {
            var target = turn.TargetPending!;
            var id = int.Parse(
                outcomeId["enemy_instance:".Length..],
                CultureInfo.InvariantCulture);
            selectedIndex = Array.IndexOf(target.CandidateEnemyInstanceIds, id);
        }
        else if (BoundaryKind == "draw_card")
        {
            var draw = turn.DrawPending!;
            var id = long.Parse(
                outcomeId["card_instance:".Length..],
                CultureInfo.InvariantCulture);
            selectedIndex = Array.IndexOf(draw.CandidateCardInstanceIds, id);
        }
        else
        {
            selectedIndex = int.Parse(
                outcomeId["choice_index:".Length..],
                CultureInfo.InvariantCulture);
        }
        if (selectedIndex < 0)
            throw new InvalidOperationException(
                "Declared Nature outcome has no matching mechanics index.");
        var next = SemanticEndTurnEngineV111.Resolve(
            turn, turn.Fingerprint, selectedIndex);
        return this with
        {
            Run = next.Run,
            PendingTurn = next.Complete ? null : next,
            DecisionVersion = checked(DecisionVersion + 1)
        };
    }
}
