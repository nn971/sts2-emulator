namespace Sts2Emulator.Core;

/// <summary>
/// Ordered mapping from the legacy Unknown-room ticket to its emulator room
/// result, shared by room mechanics, controlled replay, and Chance Law.
/// </summary>
public sealed class PrototypeUnknownRoomOddsSupportV111
{
    private const int MaximumTicketCount = PrototypeUnknownRoomOddsState.Scale;
    private readonly PrototypeUnknownRoomOddsState _odds;

    public int Act { get; }
    public int Floor { get; }
    public bool EventOnly { get; }
    public int TotalTicketCount => MaximumTicketCount;
    public string RequestOwnerId => RequestOwnerIdFor(Act, Floor);

    private PrototypeUnknownRoomOddsSupportV111(
        PrototypeUnknownRoomOddsState odds,
        int act,
        int floor,
        bool eventOnly)
    {
        ArgumentNullException.ThrowIfNull(odds);
        _odds = odds;
        Act = act;
        Floor = floor;
        EventOnly = eventOnly;
    }

    public static PrototypeUnknownRoomOddsSupportV111 Describe(
        RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var world = state.World ?? throw new InvalidOperationException(
            "Unknown-room odds require an active run world.");
        return new PrototypeUnknownRoomOddsSupportV111(
            world.UnknownRoomOdds ?? new PrototypeUnknownRoomOddsState(),
            world.Act, world.Floor,
            PrototypeGoldenPathMapV111.IsActive(state.Player, world.Act));
    }

    internal static PrototypeUnknownRoomOddsSupportV111 ForRoomResolution(
        RunWorldState world, bool eventOnly) =>
        new(world.UnknownRoomOdds ?? new PrototypeUnknownRoomOddsState(),
            world.Act, world.Floor, eventOnly);

    public static string RequestOwnerIdFor(int act, int floor) =>
        $"act:{act}:floor:{floor}:unknown_room";

    public PrototypeRoomType RoomAtTicket(int ticket) =>
        _odds.SelectRoom(ticket, EventOnly);

    public string OutcomeIdAtTicket(int ticket)
    {
        var roomId = RoomId(RoomAtTicket(ticket));
        return $"room:{roomId}:ticket:{ticket}";
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

        for (var ticket = 0; ticket < labels.Length; ticket++)
        {
            var label = labels[ticket];
            if (!label.StartsWith("room:", StringComparison.Ordinal))
                return false;
            var ticketMarker = label.LastIndexOf(
                ":ticket:", StringComparison.Ordinal);
            if (ticketMarker <= "room:".Length)
                return false;
            var roomId = label.Substring(
                "room:".Length, ticketMarker - "room:".Length);
            var ticketText = label[(ticketMarker + ":ticket:".Length)..];
            if (!int.TryParse(ticketText,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var labelTicket) ||
                labelTicket != ticket ||
                labelTicket.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
                    != ticketText)
                return false;
            if (roomId is not ("combat" or "treasure" or "shop" or "event"))
                return false;
        }
        return true;
    }

    private static string RoomId(PrototypeRoomType roomType) => roomType switch
    {
        PrototypeRoomType.Combat => "combat",
        PrototypeRoomType.Treasure => "treasure",
        PrototypeRoomType.Shop => "shop",
        PrototypeRoomType.Event => "event",
        _ => throw new InvalidDataException(
            $"Unknown-room odds produced unsupported room type '{roomType}'.")
    };
}
