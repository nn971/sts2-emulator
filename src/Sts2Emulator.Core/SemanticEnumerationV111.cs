using System.Globalization;

namespace Sts2Emulator.Core;

public sealed record SemanticEnumeratedOutcomeV111(
    string OutcomeId, SemanticPilotCombatV111 Child);

public sealed record SemanticEnumerationResultV111(
    string SchemaId, string EventKind, string EventId,
    SemanticEnumeratedOutcomeV111[] Outcomes);

/// <summary>
/// Enumerates one small semantic chance support by resolving each legal
/// outcome against an independent child session. It never enumerates player
/// actions or silently truncates a support.
/// </summary>
public static class SemanticNatureEnumeratorV111
{
    public const string SchemaId = "sts2-v111-semantic-enumeration-v1";
    public const int MaximumSupportedOutcomes = 256;

    public static SemanticEnumerationResultV111 Enumerate(
        SemanticPilotCombatV111 current, string fingerprint,
        int maximumOutcomes = 32)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        if (maximumOutcomes is < 1 or > MaximumSupportedOutcomes)
            throw new ArgumentOutOfRangeException(nameof(maximumOutcomes),
                $"Enumeration cap must be between 1 and {MaximumSupportedOutcomes}.");
        SemanticPilotCombatEngineV111.Validate(current);
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic enumeration fingerprint.");
        var currentFingerprint = current.Fingerprint;
        string eventKind;
        string eventId;
        string[] outcomeIds;
        Func<string, SemanticPilotCombatV111> resolve;

        if (current.IntentPending is not null)
        {
            var pending =
                SemanticPilotCombatEngineV111.DescribePendingIntentEvent(current);
            eventKind = "enemy_intent";
            eventId = pending.EventId;
            outcomeIds = pending.Support.Candidates
                .Select(candidate => candidate.IntentId).ToArray();
            resolve = outcomeId =>
                SemanticPilotCombatEngineV111.ResolveIntent(
                    current, currentFingerprint, outcomeId);
        }
        else if (current.DrawPending is { } draw)
        {
            eventKind = "draw_card";
            eventId = draw.EventId;
            outcomeIds = draw.CandidateCardInstanceIds.Select(id =>
                id.ToString(CultureInfo.InvariantCulture)).ToArray();
            resolve = outcomeId =>
                SemanticPilotCombatEngineV111.ResolveDrawCard(
                    current, currentFingerprint, draw.EventId,
                    long.Parse(outcomeId, CultureInfo.InvariantCulture));
        }
        else if (current.TargetPending is { } target)
        {
            eventKind = "random_target";
            eventId = target.EventId;
            outcomeIds = target.CandidateEnemyInstanceIds.Select(id =>
                id.ToString(CultureInfo.InvariantCulture)).ToArray();
            resolve = outcomeId =>
                SemanticPilotCombatEngineV111.ResolveRandomTarget(
                    current, currentFingerprint, target.EventId,
                    int.Parse(outcomeId, CultureInfo.InvariantCulture));
        }
        else if (current.GeneratedCardPending is { } generated)
        {
            eventKind = generated.SiteId ==
                "combat.random_draw_attack.card_model"
                    ? "random_draw_attack_card_model" : "generated_card";
            eventId = generated.EventId;
            outcomeIds = (string[])generated.CandidateCardIds.Clone();
            resolve = outcomeId =>
                SemanticPilotCombatEngineV111.ResolveGeneratedCard(
                    current, currentFingerprint, generated.EventId, outcomeId);
        }
        else if (current.CostPending is { } cost)
        {
            eventKind = cost.TargetCardInstanceId is null
                ? "drawn_card_cost" : "random_hand_card_cost";
            eventId = cost.EventId;
            outcomeIds = cost.CandidateCosts.Select(value =>
                value.ToString(CultureInfo.InvariantCulture)).ToArray();
            resolve = outcomeId =>
                SemanticPilotCombatEngineV111.ResolveCost(
                    current, currentFingerprint, cost.EventId,
                    int.Parse(outcomeId, CultureInfo.InvariantCulture));
        }
        else if (current.HandCostSelectionPending is { } handCost)
        {
            eventKind = "random_hand_card_selection";
            eventId = handCost.EventId;
            outcomeIds = handCost.CandidateCardInstanceIds.Select(id =>
                id.ToString(CultureInfo.InvariantCulture)).ToArray();
            resolve = outcomeId =>
                SemanticPilotCombatEngineV111.ResolveHandCostSelection(
                    current, currentFingerprint, handCost.EventId,
                    long.Parse(outcomeId, CultureInfo.InvariantCulture));
        }
        else if (current.PoolShufflePending is { } pool)
        {
            eventKind = "generated_card_pool_shuffle";
            eventId = pool.EventId;
            outcomeIds = Enumerable.Range(0, pool.Request.SupportCount)
                .Select(value => value.ToString(CultureInfo.InvariantCulture))
                .ToArray();
            resolve = outcomeId =>
                SemanticPilotCombatEngineV111.ResolvePoolShuffle(
                    current, currentFingerprint, pool.EventId,
                    int.Parse(outcomeId, CultureInfo.InvariantCulture));
        }
        else if (current.DrawInsertionPending is { } insertion)
        {
            eventKind = "generated_draw_pile_insertion";
            eventId = insertion.EventId;
            outcomeIds = Enumerable.Range(0, insertion.Request.SupportCount)
                .Select(value => value.ToString(CultureInfo.InvariantCulture))
                .ToArray();
            resolve = outcomeId =>
                SemanticPilotCombatEngineV111.ResolveDrawInsertion(
                    current, currentFingerprint, insertion.EventId,
                    int.Parse(outcomeId, CultureInfo.InvariantCulture));
        }
        else
            throw new InvalidOperationException(
                "No semantic Nature chance event is pending to enumerate.");

        if (outcomeIds.Length == 0)
            throw new InvalidDataException(
                "A semantic chance event cannot have empty support.");
        if (outcomeIds.Length > maximumOutcomes)
            throw new InvalidOperationException(
                $"Pending support has {outcomeIds.Length} outcomes, exceeding " +
                $"the requested enumeration cap of {maximumOutcomes}.");
        var outcomes = outcomeIds.Select(outcomeId =>
            new SemanticEnumeratedOutcomeV111(outcomeId, resolve(outcomeId)))
            .ToArray();
        return new SemanticEnumerationResultV111(
            SchemaId, eventKind, eventId, outcomes);
    }
}
