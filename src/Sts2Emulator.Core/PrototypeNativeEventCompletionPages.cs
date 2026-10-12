namespace Sts2Emulator.Core;

/// <summary>
/// Finished-page metadata for source events that call SetEventFinished.
/// </summary>
public static class PrototypeNativeEventCompletionPages
{
    public static bool HasTerminalChoicePage(string eventId) =>
        eventId is PrototypeNativeHiveEvents.AmalgamatorId
            or PrototypeNativeHiveEvents.LostWispId
            or PrototypeNativeHiveEvents.SpiritGrafterId
            or PrototypeNativeHiveEvents.ColossalFlowerId
            or "proto.native.underdocks.doors_of_light_and_dark"
            or "proto.native.underdocks.drowning_beacon"
            or "proto.native.underdocks.sunken_treasury"
            or "proto.native.underdocks.sunken_statue";

    public static int TerminalChoicePageIndex(string eventId) =>
        eventId == PrototypeNativeHiveEvents.ColossalFlowerId ? 3 : 1;

    public static bool HasTerminalChoice(
        string eventId, string? choiceId) =>
        eventId == PrototypeNativeHiveEvents.AmalgamatorId
            && choiceId is "combine_strikes" or "combine_defends"
        || eventId == PrototypeNativeHiveEvents.LostWispId
            && choiceId is "claim" or "search"
        || eventId == PrototypeNativeHiveEvents.SpiritGrafterId
            && choiceId is "let_it_in" or "rejection"
        || eventId == PrototypeNativeHiveEvents.ColossalFlowerId
            && choiceId is "extract" or "relic"
        || eventId == "proto.native.underdocks.doors_of_light_and_dark"
            && choiceId is "light" or "dark"
        || eventId == "proto.native.underdocks.drowning_beacon"
            && choiceId is "bottle" or "climb"
        || eventId == "proto.native.underdocks.sunken_treasury"
            && choiceId is "first_chest" or "second_chest"
        || eventId == "proto.native.underdocks.sunken_statue"
            && choiceId is "sword" or "dive";
}
