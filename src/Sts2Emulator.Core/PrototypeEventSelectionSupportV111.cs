using System.Globalization;

namespace Sts2Emulator.Core;

public sealed record PrototypeEventSelectionCandidateV111(
    string EventId, int TicketWeight);

/// <summary>
/// Ordered event eligibility and raw-ticket mapping shared by ordinary event
/// entry, controlled replay, and the read-only Chance Law query.
/// </summary>
public sealed class PrototypeEventSelectionSupportV111
{
    private readonly PrototypeEventSelectionCandidateV111[] _candidates;

    public int Act { get; }
    public int Floor { get; }
    public bool UsesProvisionalUniformWeights { get; }
    public string RequestOwnerId { get; }
    public PrototypeEventSelectionCandidateV111[] Candidates =>
        (PrototypeEventSelectionCandidateV111[])_candidates.Clone();
    public int TotalTicketCount { get; }

    private PrototypeEventSelectionSupportV111(
        int act,
        int floor,
        bool usesProvisionalUniformWeights,
        string requestOwnerId,
        PrototypeEventSelectionCandidateV111[] candidates)
    {
        Act = act;
        Floor = floor;
        UsesProvisionalUniformWeights = usesProvisionalUniformWeights;
        RequestOwnerId = requestOwnerId;
        _candidates = (PrototypeEventSelectionCandidateV111[])
            candidates.Clone();
        TotalTicketCount = _candidates.Aggregate(
            0, (total, candidate) => checked(total + candidate.TicketWeight));
        if (TotalTicketCount <= 0 ||
            _candidates.Length == 0 ||
            _candidates.Any(candidate =>
                string.IsNullOrWhiteSpace(candidate.EventId) ||
                candidate.TicketWeight <= 0) ||
            _candidates.Select(candidate => candidate.EventId)
                .Distinct(StringComparer.Ordinal).Count() != _candidates.Length)
            throw new InvalidDataException(
                "Event-selection support must contain unique positive-weight event identities.");
    }

    public static PrototypeEventSelectionSupportV111 Describe(
        RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var world = state.World ?? throw new InvalidOperationException(
            "Event selection requires an active run world.");
        var nativeOvergrowth = world.Act == 1
            && PrototypeNativeOvergrowthMap.IsGenerationProfile(
                world.Map.GenerationProfileId);
        var nativeUnderdocks = world.Act == 1
            && world.Map.GenerationProfileId
                == PrototypeNativeUnderdocks.GenerationProfileId;
        var laterRoute = world.Act is 2 or 3
            && state.Configuration?.UsesExperimentalSilentThreeActRoute == true;
        var eligible = PrototypeContent.Events.Values
            .Where(evt =>
                (laterRoute
                    ? ExperimentalSilentThreeActRouteV111.ContainsEvent(
                        evt.Id, world.Act)
                    : nativeUnderdocks
                        ? PrototypeNativeUnderdocksEvents.IsSupported(evt.Id)
                        : nativeOvergrowth
                            ? PrototypeNativeOvergrowthEvents
                                  .IsNativeRegionEvent(evt.Id)
                                || PrototypeNativeSharedEvents.IsSupported(
                                    evt.Id)
                            : !PrototypeNativeOvergrowthEvents
                                  .IsNativeRegionEvent(evt.Id)
                                && !PrototypeNativeUnderdocksEvents
                                    .IsNativeRegionEvent(evt.Id))
                && (!nativeOvergrowth
                    || PrototypeNativeOvergrowthEvents.IsEligible(
                        evt, state.Player))
                && (!nativeUnderdocks
                    || PrototypeNativeUnderdocksEvents.IsEligible(
                        evt, state.Player)
                    && PrototypeNativeUnderdocksEvents.IsFloorEligible(
                        evt, world))
                && (!laterRoute || world.Act != 2
                    || PrototypeNativeHiveEvents.IsEligible(evt, state.Player))
                && (!laterRoute || world.Act != 3
                    || PrototypeNativeGloryEvents.IsEligible(evt, state.Player))
                && world.Act >= evt.MinAct
                && world.Act <= evt.MaxAct
                && (evt.Id != PrototypeNativeFakeMerchant.EventId
                    || PrototypeNativeFakeMerchant.IsEligible(state.Player))
                && (evt.Id != PrototypeNativeFutureOfPotions.EventId
                    || laterRoute
                    && PrototypeNativeFutureOfPotions.IsEligible(state.Player))
                && (laterRoute || evt.Weight > 0)
                && (!evt.OncePerRun
                    || !world.EventIds.Contains(evt.Id, StringComparer.Ordinal)))
            .ToArray();

        if (eligible.Length == 0)
            throw new InvalidOperationException(
                $"No prototype event is eligible in act {world.Act}.");

        if (laterRoute)
        {
            // Without replacement where possible; one-off native events
            // stay excluded by OncePerRun. Pool exhaustion is deliberately
            // provisional until the source event-bag rolls are audited.
            var unseen = eligible.Where(evt =>
                !world.EventIds.Contains(evt.Id, StringComparer.Ordinal))
                .ToArray();
            if (unseen.Length > 0)
                eligible = unseen;
        }

        var previousEvent = world.EventIds.LastOrDefault();
        if (previousEvent is not null && eligible.Length > 1)
        {
            var withoutImmediateRepeat = eligible
                .Where(evt => !StringComparer.Ordinal.Equals(
                    evt.Id, previousEvent))
                .ToArray();
            if (withoutImmediateRepeat.Length > 0)
                eligible = withoutImmediateRepeat;
        }

        var candidates = eligible.Select(evt =>
            new PrototypeEventSelectionCandidateV111(
                evt.Id, laterRoute ? 1 : evt.Weight)).ToArray();
        // Preserve the established request-owner fingerprint and its member
        // names so existing typed transcripts continue to replay unchanged.
        var weightedSupport = candidates.Select(candidate => new
        {
            Id = candidate.EventId,
            Weight = candidate.TicketWeight
        }).ToArray();
        var ownerId = $"act:{world.Act}:floor:{world.Floor}:pool:"
            + CanonicalJson.Sha256(weightedSupport);
        return new PrototypeEventSelectionSupportV111(
            world.Act, world.Floor, laterRoute, ownerId, candidates);
    }

    public string EventIdAtTicket(int ticket)
    {
        if ((uint)ticket >= (uint)TotalTicketCount)
            throw new ArgumentOutOfRangeException(nameof(ticket));
        var remaining = ticket;
        foreach (var candidate in _candidates)
        {
            if (remaining < candidate.TicketWeight)
                return candidate.EventId;
            remaining -= candidate.TicketWeight;
        }
        throw new InvalidDataException(
            "Event ticket escaped its declared weighted support.");
    }

    public string OutcomeIdAtTicket(int ticket)
    {
        if ((uint)ticket >= (uint)TotalTicketCount)
            throw new ArgumentOutOfRangeException(nameof(ticket));
        var remaining = ticket;
        foreach (var candidate in _candidates)
        {
            if (remaining < candidate.TicketWeight)
                return $"event:{candidate.EventId}:ticket:"
                    + remaining.ToString(CultureInfo.InvariantCulture);
            remaining -= candidate.TicketWeight;
        }
        throw new InvalidDataException(
            "Event ticket escaped its declared weighted support.");
    }

    public string[] TicketOutcomeIds()
    {
        var outcomeIds = new string[TotalTicketCount];
        for (var ticket = 0; ticket < outcomeIds.Length; ticket++)
            outcomeIds[ticket] = OutcomeIdAtTicket(ticket);
        return outcomeIds;
    }

    public bool MatchesTicketOutcomeLabels(
        string[] outcomeIds, int maximumInlineCount)
    {
        ArgumentNullException.ThrowIfNull(outcomeIds);
        if (TotalTicketCount > maximumInlineCount)
            return outcomeIds.Length == 0;
        return outcomeIds.SequenceEqual(TicketOutcomeIds(),
                   StringComparer.Ordinal)
            || ControlledStochasticCandidateLabelsV111.IsLegacyNumericRange(
                outcomeIds, TotalTicketCount, maximumInlineCount);
    }

    public static bool IsTicketOutcomeSequence(
        string[] outcomeIds, int totalTicketCount, int maximumInlineCount)
    {
        ArgumentNullException.ThrowIfNull(outcomeIds);
        if (totalTicketCount <= 0 || maximumInlineCount <= 0)
            return false;
        if (totalTicketCount > maximumInlineCount)
            return outcomeIds.Length == 0;
        if (ControlledStochasticCandidateLabelsV111.IsLegacyNumericRange(
                outcomeIds, totalTicketCount, maximumInlineCount))
            return true;
        if (outcomeIds.Length != totalTicketCount)
            return false;

        var seenEventIds = new HashSet<string>(StringComparer.Ordinal);
        string? currentEventId = null;
        var nextLocalTicket = 0;
        foreach (var outcomeId in outcomeIds)
        {
            if (string.IsNullOrWhiteSpace(outcomeId))
                return false;
            var ticketMarker = outcomeId.LastIndexOf(
                ":ticket:", StringComparison.Ordinal);
            if (ticketMarker <= "event:".Length ||
                !outcomeId.StartsWith("event:", StringComparison.Ordinal))
                return false;
            var eventId = outcomeId.Substring(
                "event:".Length, ticketMarker - "event:".Length);
            var ticketText = outcomeId[(ticketMarker + ":ticket:".Length)..];
            if (string.IsNullOrWhiteSpace(eventId) ||
                !int.TryParse(ticketText, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var localTicket) ||
                localTicket.ToString(CultureInfo.InvariantCulture) != ticketText)
                return false;

            if (!StringComparer.Ordinal.Equals(eventId, currentEventId))
            {
                if (localTicket != 0 || !seenEventIds.Add(eventId))
                    return false;
                currentEventId = eventId;
                nextLocalTicket = 1;
            }
            else
            {
                if (localTicket != nextLocalTicket)
                    return false;
                nextLocalTicket++;
            }
        }
        return true;
    }
}
