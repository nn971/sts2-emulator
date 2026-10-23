namespace Sts2Emulator.Core;

/// <summary>A source-ordered physical-card support for a random pile
/// selection that occurs inside an automatic mechanics continuation.</summary>
public sealed record SemanticRandomPileCardEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, string SiteId, string StreamId,
    string OwnerId, int Ordinal, string OperationKind,
    long[] CandidateCardInstanceIds, string[] CandidateCardModelIds,
    string ProbabilityStatus = "UNVERIFIED") : ISemanticChanceEventV111
{
    private long[]? _candidateCardInstanceIds =
        CloneOptional(CandidateCardInstanceIds);
    private string[]? _candidateCardModelIds =
        CloneOptional(CandidateCardModelIds);

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

    public const string EventSchemaId =
        "sts2-v111-semantic-random-pile-card-v1";

    public void Validate()
    {
        var candidateCardInstanceIds = _candidateCardInstanceIds;
        var candidateCardModelIds = _candidateCardModelIds;
        if (SchemaId != EventSchemaId ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            SiteId != StochasticSiteRegistryV111
                .CombatJeweledMaskRandomPowerDrawCardSiteId ||
            StreamId != "semantic.draw_pile" ||
            OwnerId != "player:draw_pile" || Ordinal < 0 ||
            OperationKind != "choose_card_instance" ||
            candidateCardInstanceIds is not { Length: > 0 } ||
            candidateCardModelIds is null ||
            candidateCardModelIds.Length != candidateCardInstanceIds.Length ||
            candidateCardInstanceIds.Any(id => id <= 0) ||
            candidateCardInstanceIds.Distinct().Count() !=
                candidateCardInstanceIds.Length ||
            candidateCardModelIds.Any(string.IsNullOrWhiteSpace) ||
            SupportFingerprint != CanonicalJson.Sha256(new
            {
                candidateCardInstanceIds,
                candidateCardModelIds
            }) ||
            EventId != CanonicalJson.Sha256(new
            {
                SchemaId,
                BoundaryFingerprint,
                SupportFingerprint
            }) ||
            ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Invalid semantic random pile-card event.");
    }

    public SemanticRandomPileCardEventV111 Fork() => this with
    {
        CandidateCardInstanceIds = _candidateCardInstanceIds!,
        CandidateCardModelIds = _candidateCardModelIds!
    };

    private static long[]? CloneOptional(long[]? values) => values is null
        ? null
        : (long[])values.Clone();

    private static string[]? CloneOptional(string[]? values) => values is null
        ? null
        : (string[])values.Clone();
}

public sealed record SemanticRandomPileCardDecisionV111(
    SemanticRandomPileCardEventV111 Event, int SelectedIndex,
    long SelectedCardInstanceId, string SelectedCardModelId)
{
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
                "Random pile-card decision is outside its ordered support.");
    }

    public SemanticRandomPileCardDecisionV111 Fork() => this with
    {
        Event = Event.Fork()
    };
}

public static class SemanticRandomPileCardEngineV111
{
    public const string MechanicsOperationId = "random_draw_pile_card";

    public static SemanticRandomPileCardEventV111? DescribePending(
        RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var combat = run.World?.Combat;
        var pending = combat?.PendingChoice?.ControlledRandomPileCard;
        if (combat?.Stochastic.SemanticDeck is not { } deck || pending is null)
            return null;
        var ids = (long[])pending.CandidateCardInstanceIds.Clone();
        var models = ids.Select(id => combat.Cards.Single(card =>
            card.InstanceId == id).CardId).ToArray();
        var support = CanonicalJson.Sha256(new
        {
            candidateCardInstanceIds = ids,
            candidateCardModelIds = models
        });
        if (pending.SiteId !=
                StochasticSiteRegistryV111
                    .CombatJeweledMaskRandomPowerDrawCardSiteId ||
            pending.SupportFingerprint != support)
            throw new InvalidDataException(
                "Saved random pile-card support no longer matches combat.");
        var boundary = CanonicalJson.Sha256(new
        {
            combat.Turn,
            deck.Fingerprint,
            pending.SiteId,
            pending.Ordinal,
            ids,
            models
        });
        var schemaId = SemanticRandomPileCardEventV111.EventSchemaId;
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId = schemaId,
            BoundaryFingerprint = boundary,
            SupportFingerprint = support
        });
        var semanticEvent = new SemanticRandomPileCardEventV111(
            schemaId, eventId, boundary, support, pending.SiteId,
            "semantic.draw_pile", "player:draw_pile", pending.Ordinal,
            "choose_card_instance", ids, models);
        semanticEvent.Validate();
        return semanticEvent;
    }

    public static SemanticRandomPileCardDecisionV111 Resolve(
        SemanticRandomPileCardEventV111 pending, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        if (selectedIndex < 0 ||
            selectedIndex >= pending.CandidateCardInstanceIds.Length)
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));
        var decision = new SemanticRandomPileCardDecisionV111(
            pending.Fork(), selectedIndex,
            pending.CandidateCardInstanceIds[selectedIndex],
            pending.CandidateCardModelIds[selectedIndex]);
        decision.Validate();
        return decision;
    }

    public static SemanticRandomPileCardDecisionV111 Select(
        SemanticRandomPileCardEventV111 pending, string eventId,
        long cardInstanceId)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Random pile-card event ID is stale or mismatched.");
        var index = Array.IndexOf(
            pending.CandidateCardInstanceIds, cardInstanceId);
        if (index < 0)
            throw new InvalidOperationException(
                "Card instance is outside the random pile-card support.");
        return Resolve(pending, index);
    }

    internal static void ValidateDecision(
        SemanticRandomPileCardEventV111? pending,
        SemanticRandomPileCardDecisionV111 decision, int sourceIndex)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        if (pending is null || sourceIndex != decision.SelectedIndex ||
            CanonicalJson.Sha256(pending) !=
                CanonicalJson.Sha256(decision.Event))
            throw new InvalidOperationException(
                "Random pile-card decision is stale or outside current support.");
    }
}
