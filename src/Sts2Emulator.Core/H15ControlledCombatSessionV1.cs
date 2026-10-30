using System.Globalization;

namespace Sts2Emulator.Core;

/// <summary>
/// Experimental, non-fair controlled end-turn continuation rooted at a REAL
/// first-turn native-structure/prototype-RNG combat entry. This consumes
/// ordinary combat mechanics and the existing typed SemanticEndTurn resume
/// engine; no encounter or card-effect fixture is fabricated.
///
/// The independent candidate is NOT a public-history conditional posterior.
/// Only end_turn actions and the listed typed Chance Law boundaries are
/// available. Every other stochastic/player-action boundary fails closed.
/// </summary>
public sealed record H15ControlledCombatSessionV1(
    RunState Run, SemanticEndTurnV111? PendingTurn,
    string NativeEncounterEntry, long DecisionVersion,
    string CandidateModelId)
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
        var nativeEntry = NativeActOneWeakCombatEntryV111
                .ResearchNativeEntry(encounterId)
            ?? NativeActOneNormalCombatEntryV111
                .ResearchNativeEntry(encounterId)
            ?? NativeActOneEliteBossCombatEntryV111
                .ResearchNativeEntry(encounterId)
            ?? throw new NotSupportedException(
                "Encounter has no pinned semantic intent mapping: " +
                encounterId);
        var candidate = H15NativeCombatEntryCandidateV1.Build(
            source, independentSeed);
        if (H15NativeCombatEntryCandidateV1.ProviderClass(source) !=
            H15NativeCombatEntryCandidateV1.PrototypeCombatProviderClass)
            throw new InvalidOperationException(
                "H15 experimental end turn lost prototype RNG provenance.");
        return new H15ControlledCombatSessionV1(
            candidate, null, nativeEntry, 0,
            H15NativeCombatEntryCandidateV1.ModelId(source));
    }

    public string BoundaryKind
    {
        get
        {
            if (Run.Phase != RunPhase.Combat)
                return "terminal";
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
        return Unverified(law, alternativeWeights);
    }

    private static ChanceLawV111 Unverified(
        ChanceLawV111 original, ChanceLawOutcomeV111[]? newOutcomes)
    {
        var outcomes = newOutcomes ?? original.Outcomes;
        var downgraded = original with
        {
            Outcomes = outcomes,
            TotalWeight = outcomes.Sum(choice => choice.Weight),
            SupportFingerprint = CanonicalJson.Sha256(outcomes),
            ProbabilityStatus = ChanceLawV111.UnverifiedStatus,
            ProbabilityProvenance = UnverifiedProvenance
        };
        // ChanceLawV111.Validate defines its canonical identifier over
        // these exact typed fields. Never carry a SOURCE_VERIFIED law ID
        // across the experimental provenance downgrade.
        var lawId = CanonicalJson.Sha256(new
        {
            downgraded.SchemaId,
            downgraded.SiteId,
            downgraded.OperationKind,
            downgraded.OwnerId,
            downgraded.Ordinal,
            downgraded.SupportFingerprint,
            downgraded.ConditioningPolicyId,
            downgraded.ConditioningFingerprint,
            downgraded.ProbabilityStatus,
            downgraded.ProbabilityProvenance
        });
        var result = downgraded with { LawId = lawId };
        result.Validate();
        return result;
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
