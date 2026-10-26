namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    /// <summary>
    /// Query the legacy event's random-upgrade choice over its current ordered
    /// eligible physical deck instances. The equal-index model and Native
    /// source parity remain unverified.
    /// </summary>
    public static ChanceLawV111 QueryLegacyEventRandomUpgradeCardInstance(
        RunState source, string eventId, string choiceId, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(choiceId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        if (source.Phase != RunPhase.Event ||
            source.World?.Event is not { } eventState ||
            eventState.EventId != eventId ||
            eventState.ChosenChoiceId is not null ||
            !PrototypeGameEngine.IsControlledEventChoice(eventId, choiceId))
            throw new InvalidOperationException(
                "Legacy event random-upgrade law is outside its active event-choice boundary.");
        var choice = PrototypeContent.Event(eventId).Choices
            .SingleOrDefault(candidate => candidate.Id == choiceId);
        if (choice is null || !choice.Effects.Any(effect =>
                effect.Kind == PrototypeRunEffectKind.UpgradeRandomCard))
            throw new InvalidOperationException(
                "Legacy event random-upgrade law requires an option with a random-upgrade effect.");

        var eligibleCards = source.Player.Deck.Where(card =>
            PrototypeGameEngine.CanSelectEventDeckCard(
                card, PrototypePersistentDeckChoiceKind.Upgrade,
                null, null, false)).ToArray();
        if (eligibleCards.Length == 0)
            throw new InvalidOperationException(
                "Legacy event random-upgrade law has no eligible card support.");

        var instanceIds = eligibleCards.Select(card => card.InstanceId)
            .ToArray();
        var candidateLabels = eligibleCards.Select(card =>
            card.CardId + "#" + card.InstanceId.ToString(
                System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        const string siteId = "event.random_upgrade.card_instance";
        const string streamId = "event";
        var poolFingerprint = CanonicalJson.Sha256(instanceIds);
        var ownerId = $"event:{eventId}:choice:{choiceId}:pool:" +
            poolFingerprint;
        var outcomes = instanceIds.Select(instanceId =>
            new ChanceLawOutcomeV111(
                "card_instance:" + instanceId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture), 1L))
            .ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            eventId,
            choiceId,
            orderedEligibleCardInstances = instanceIds,
            orderedCandidateLabels = candidateLabels,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-event-random-upgrade-physical-card-v1", conditioning,
            "Equal index weights over the ordered eligible physical deck " +
            "instances; source probability, event support completeness, and " +
            "Native parity remain unverified.", outcomes,
            operationKind: "choose_index");
    }
}
