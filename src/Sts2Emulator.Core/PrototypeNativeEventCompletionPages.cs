namespace Sts2Emulator.Core;

/// <summary>
/// Finished-page metadata for source events that call SetEventFinished.
/// </summary>
public static class PrototypeNativeEventCompletionPages
{
    public static bool HasTerminalChoicePage(string eventId) =>
        eventId is "proto.native.event.aroma_of_chaos"
            or PrototypeNativeBrainLeech.EventId
            or PrototypeNativeSelfHelpBook.EventId
            or PrototypeNativePotionCourier.EventId
            or "proto.native.event.morphic_grove"
            or "proto.native.event.wellspring"
            or "proto.native.event.whispering_hollow"
            or PrototypeNativeDenseVegetation.EventId
            or "proto.native.underdocks.waterlogged_scriptorium"
            or PrototypeNativeUnderdocksTrashHeap.EventId
            or "proto.native.underdocks.abyssal_baths"
            or PrototypeNativeEndlessConveyor.EventId
            or PrototypeNativeHiveEvents.AmalgamatorId
            or PrototypeNativeHiveEvents.LostWispId
            or PrototypeNativeHiveEvents.SpiritGrafterId
            or PrototypeNativeHiveEvents.FieldOfManSizedHolesId
            or PrototypeNativeHiveEvents.ColossalFlowerId
            or PrototypeNativeGloryEvents.ReflectionsId
            or PrototypeNativeGloryEvents.RoundTeaPartyId
            or PrototypeNativeGloryEvents.HungryForMushroomsId
            or PrototypeNativeGloryEvents.GraveOfTheForgottenId
            or "proto.native.underdocks.doors_of_light_and_dark"
            or "proto.native.underdocks.drowning_beacon"
            or "proto.native.underdocks.sunken_treasury"
            or "proto.native.underdocks.sunken_statue";

    public static int TerminalChoicePageIndex(string eventId) =>
        eventId is PrototypeNativeHiveEvents.ColossalFlowerId
            or PrototypeNativeDenseVegetation.EventId ? 3
            : eventId == PrototypeNativeGloryEvents.RoundTeaPartyId ? 2
            : eventId is "proto.native.underdocks.abyssal_baths"
                or PrototypeNativeEndlessConveyor.EventId
                ? int.MaxValue : 1;

    public static bool HasTerminalChoice(
        string eventId, string? choiceId) =>
        eventId == "proto.native.event.aroma_of_chaos"
            && choiceId is "let_go" or "maintain_control"
        || eventId == PrototypeNativeBrainLeech.EventId
            && choiceId is PrototypeNativeBrainLeech.ShareKnowledgeChoiceId
                or PrototypeNativeBrainLeech.RipChoiceId
        || eventId == PrototypeNativeSelfHelpBook.EventId
            && choiceId is PrototypeNativeSelfHelpBook.SharpChoiceId
                or PrototypeNativeSelfHelpBook.NimbleChoiceId
                or PrototypeNativeSelfHelpBook.SwiftChoiceId
                or PrototypeNativeSelfHelpBook.NoOptionsChoiceId
        || eventId == PrototypeNativePotionCourier.EventId
            && choiceId is PrototypeNativePotionCourier.GrabPotionsChoiceId
                or PrototypeNativePotionCourier.RansackChoiceId
        || eventId == "proto.native.event.morphic_grove"
            && choiceId is "group" or "loner"
        || eventId == "proto.native.event.wellspring"
            && choiceId is "bottle" or "bathe"
        || eventId == "proto.native.event.whispering_hollow"
            && choiceId is "gold" or "hug"
        || eventId == PrototypeNativeDenseVegetation.EventId
            && choiceId is "trudge"
        || eventId == "proto.native.underdocks.waterlogged_scriptorium"
            && choiceId is "bloody_ink" or "tentacle_quill"
                or "prickly_sponge"
        || eventId == PrototypeNativeUnderdocksTrashHeap.EventId
            && choiceId is "dive" or "grab"
        || eventId == "proto.native.underdocks.abyssal_baths"
            && choiceId is "abstain" or "exit"
        || eventId == PrototypeNativeEndlessConveyor.EventId
            && choiceId is "observe" or "leave"
        || eventId == PrototypeNativeHiveEvents.AmalgamatorId
            && choiceId is "combine_strikes" or "combine_defends"
        || eventId == PrototypeNativeHiveEvents.LostWispId
            && choiceId is "claim" or "search"
        || eventId == PrototypeNativeHiveEvents.SpiritGrafterId
            && choiceId is "let_it_in" or "rejection"
        || eventId == PrototypeNativeHiveEvents.FieldOfManSizedHolesId
            && choiceId is "resist" or "enter_your_hole"
        || eventId == PrototypeNativeHiveEvents.ColossalFlowerId
            && choiceId is "extract" or "relic"
        || eventId == PrototypeNativeGloryEvents.ReflectionsId
            && choiceId is "touch_mirror" or "shatter"
        || eventId == PrototypeNativeGloryEvents.RoundTeaPartyId
            && choiceId is "enjoy_tea" or "continue_fight"
        || eventId == PrototypeNativeGloryEvents.HungryForMushroomsId
            && choiceId is "big_mushroom" or "fragrant_mushroom"
        || eventId == PrototypeNativeGloryEvents.GraveOfTheForgottenId
            && choiceId is "confront" or "accept"
        || eventId == "proto.native.underdocks.doors_of_light_and_dark"
            && choiceId is "light" or "dark"
        || eventId == "proto.native.underdocks.drowning_beacon"
            && choiceId is "bottle" or "climb"
        || eventId == "proto.native.underdocks.sunken_treasury"
            && choiceId is "first_chest" or "second_chest"
        || eventId == "proto.native.underdocks.sunken_statue"
            && choiceId is "sword" or "dive";
}
