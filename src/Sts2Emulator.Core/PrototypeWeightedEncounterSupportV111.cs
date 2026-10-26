using System.Globalization;

namespace Sts2Emulator.Core;

/// <summary>
/// Ordered eligibility, weights, and raw-ticket mapping for the ordinary
/// prototype encounter selector.
/// </summary>
public sealed class PrototypeWeightedEncounterSupportV111
{
    private readonly PrototypeEncounterDefinition[] _encounters;

    public PrototypeRoomType RoomType { get; }
    public int Act { get; }
    public int Floor { get; }
    public string RequestOwnerId { get; }
    public int TotalTicketCount { get; }
    public string[] EncounterIds => _encounters
        .Select(encounter => encounter.Id).ToArray();
    public int[] TicketWeights => _encounters
        .Select(encounter => encounter.Weight).ToArray();

    private PrototypeWeightedEncounterSupportV111(
        PrototypeRoomType roomType,
        int act,
        int floor,
        PrototypeEncounterDefinition[] encounters)
    {
        RoomType = roomType;
        Act = act;
        Floor = floor;
        _encounters = encounters;
        TotalTicketCount = _encounters.Aggregate(
            0, (total, encounter) => checked(total + encounter.Weight));
        if (_encounters.Length == 0 || TotalTicketCount <= 0 ||
            _encounters.Any(encounter => encounter.Weight <= 0 ||
                string.IsNullOrWhiteSpace(encounter.Id)))
            throw new InvalidDataException(
                "Weighted encounter support must contain positive tickets.");

        RequestOwnerId = "pool:" + CanonicalJson.Sha256(
            _encounters.Select(encounter => new
            {
                encounter.Id,
                encounter.Weight
            }).ToArray());
    }

    public static PrototypeWeightedEncounterSupportV111 Describe(
        RunWorldState world, PrototypeRoomType roomType)
    {
        ArgumentNullException.ThrowIfNull(world);
        var encounters = PrototypeContent.Encounters
            .Where(candidate => candidate.RoomType == roomType
                && world.Act >= candidate.MinAct
                && world.Act <= candidate.MaxAct
                && world.Floor >= candidate.MinFloor
                && world.Floor <= candidate.MaxFloor
                && candidate.Weight > 0)
            .ToArray();
        if (encounters.Length == 0)
            throw new InvalidOperationException(
                $"No prototype encounter is eligible for {roomType} " +
                $"at act {world.Act}, floor {world.Floor}.");

        var previousEncounter = world.EncounterIds.LastOrDefault();
        if (previousEncounter is not null && encounters.Length > 1)
        {
            var withoutImmediateRepeat = encounters
                .Where(candidate => !StringComparer.Ordinal.Equals(
                    candidate.Id, previousEncounter))
                .ToArray();
            if (withoutImmediateRepeat.Length > 0)
                encounters = withoutImmediateRepeat;
        }

        return new PrototypeWeightedEncounterSupportV111(
            roomType, world.Act, world.Floor, encounters);
    }

    public string EncounterIdAtTicket(int ticket) =>
        EncounterAtTicket(ticket).Id;

    internal PrototypeEncounterDefinition EncounterAtTicket(int ticket)
    {
        if ((uint)ticket >= (uint)TotalTicketCount)
            throw new ArgumentOutOfRangeException(nameof(ticket));
        var remaining = ticket;
        foreach (var encounter in _encounters)
        {
            if (remaining < encounter.Weight)
                return encounter;
            remaining -= encounter.Weight;
        }
        throw new InvalidDataException(
            "Encounter ticket escaped its declared weighted support.");
    }

    public string OutcomeIdAtTicket(int ticket)
    {
        if ((uint)ticket >= (uint)TotalTicketCount)
            throw new ArgumentOutOfRangeException(nameof(ticket));
        var remaining = ticket;
        foreach (var encounter in _encounters)
        {
            if (remaining < encounter.Weight)
                return $"encounter:{encounter.Id}:ticket:"
                    + remaining.ToString(CultureInfo.InvariantCulture);
            remaining -= encounter.Weight;
        }
        throw new InvalidDataException(
            "Encounter ticket escaped its declared weighted support.");
    }

    public string[] TicketOutcomeIds()
    {
        var outcomeIds = new string[TotalTicketCount];
        for (var ticket = 0; ticket < outcomeIds.Length; ticket++)
            outcomeIds[ticket] = OutcomeIdAtTicket(ticket);
        return outcomeIds;
    }

    public bool MatchesTicketOutcomeLabels(
        string[] labels, int maximumInlineCount)
    {
        ArgumentNullException.ThrowIfNull(labels);
        if (TotalTicketCount > maximumInlineCount)
            return labels.Length == 0;
        return labels.SequenceEqual(TicketOutcomeIds(), StringComparer.Ordinal)
            || ControlledStochasticCandidateLabelsV111.IsLegacyNumericRange(
                labels, TotalTicketCount, maximumInlineCount);
    }

    public static bool IsTicketOutcomeSequence(
        string[] labels, int totalTicketCount, int maximumInlineCount)
    {
        ArgumentNullException.ThrowIfNull(labels);
        if (totalTicketCount <= 0 || maximumInlineCount <= 0)
            return false;
        if (totalTicketCount > maximumInlineCount)
            return labels.Length == 0;
        if (ControlledStochasticCandidateLabelsV111.IsLegacyNumericRange(
                labels, totalTicketCount, maximumInlineCount))
            return true;
        if (labels.Length != totalTicketCount)
            return false;

        var seenEncounterIds = new HashSet<string>(StringComparer.Ordinal);
        string? currentEncounterId = null;
        var nextLocalTicket = 0;
        foreach (var label in labels)
        {
            if (!label.StartsWith("encounter:", StringComparison.Ordinal))
                return false;
            var ticketMarker = label.LastIndexOf(
                ":ticket:", StringComparison.Ordinal);
            if (ticketMarker <= "encounter:".Length)
                return false;
            var encounterId = label.Substring(
                "encounter:".Length, ticketMarker - "encounter:".Length);
            var ticketText = label[(ticketMarker + ":ticket:".Length)..];
            if (string.IsNullOrWhiteSpace(encounterId) ||
                !int.TryParse(ticketText, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var localTicket) ||
                localTicket.ToString(CultureInfo.InvariantCulture) != ticketText)
                return false;

            if (!StringComparer.Ordinal.Equals(encounterId, currentEncounterId))
            {
                if (localTicket != 0 || !seenEncounterIds.Add(encounterId))
                    return false;
                currentEncounterId = encounterId;
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
