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
            or "proto.native.event.luminous_choir"
            or "proto.native.event.jungle_maze_adventure"
            or "proto.native.event.sapphire_seed"
            or "proto.native.event.unrest_site"
            or "proto.native.event.byrdonis_nest"
            or "proto.native.event.wood_carvings"
            or PrototypeNativeDenseVegetation.EventId
            or PrototypeNativeTabletOfTruth.EventId
            or "proto.native.underdocks.waterlogged_scriptorium"
            or PrototypeNativeUnderdocksTrashHeap.EventId
            or "proto.native.underdocks.abyssal_baths"
            or PrototypeNativeEndlessConveyor.EventId
            or PrototypeNativePunchOff.EventId
            or PrototypeNativeGloryEvents.BattlewornDummyId
            or PrototypeNativeHiveEvents.AmalgamatorId
            or PrototypeNativeHiveEvents.InfestedAutomatonId
            or PrototypeNativeHiveEvents.LostWispId
            or PrototypeNativeHiveEvents.SpiritGrafterId
            or PrototypeNativeHiveEvents.FieldOfManSizedHolesId
            or PrototypeNativeHiveEvents.ColossalFlowerId
            or PrototypeNativeStoneOfAllTime.EventId
            or PrototypeNativeGloryEvents.ReflectionsId
            or PrototypeNativeGloryEvents.RoundTeaPartyId
            or PrototypeNativeGloryEvents.TeaMasterId
            or PrototypeNativeGloryEvents.TrialId
            or PrototypeNativeGloryEvents.HungryForMushroomsId
            or PrototypeNativeGloryEvents.GraveOfTheForgottenId
            or PrototypeNativeHiveEvents.LanternKeyEventId
            or PrototypeNativeHiveEvents.ZenWeaverId
            or NativeAncientRewardsV111.TezcataraId
            or NativePaelRewardsV111.PaelId
            or NativeOrobasEventV111.EventId
            or NativeTanxEventV111.EventId
            or NativeVakuuEventV111.EventId
            or NativeNonupeipeEventV111.EventId
            or NativeDarvEventV111.EventId
            or PrototypeNativeSharedEvents.ThisOrThatId
            or "proto.native.underdocks.doors_of_light_and_dark"
            or "proto.native.underdocks.drowning_beacon"
            or "proto.native.underdocks.sunken_treasury"
            or "proto.native.event.sunken_statue"
            or "proto.native.underdocks.sunken_statue";

    public static int TerminalChoicePageIndex(string eventId) =>
        eventId is PrototypeNativeHiveEvents.ColossalFlowerId
            or PrototypeNativeDenseVegetation.EventId ? 3
            : eventId == PrototypeNativeGloryEvents.BattlewornDummyId ? 4
            : eventId == PrototypeNativeGloryEvents.RoundTeaPartyId ? 2
            : eventId == PrototypeNativeGloryEvents.TrialId ? 6
            : eventId == PrototypeNativeHiveEvents.LanternKeyEventId ? 3
            : eventId is "proto.native.underdocks.abyssal_baths"
                or PrototypeNativeEndlessConveyor.EventId
                or PrototypeNativePunchOff.EventId
                or PrototypeNativeTabletOfTruth.EventId
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
        || eventId == "proto.native.event.luminous_choir"
            && choiceId is "reach" or "tribute"
        || eventId == "proto.native.event.jungle_maze_adventure"
            && choiceId is "solo" or "join"
        || eventId == "proto.native.event.sapphire_seed"
            && choiceId is "eat" or "plant"
        || eventId == "proto.native.event.unrest_site"
            && choiceId is "rest" or "kill"
        || eventId == "proto.native.event.byrdonis_nest"
            && choiceId is "eat" or "take"
        || eventId == "proto.native.event.wood_carvings"
            && choiceId is "bird" or "snake" or "torus"
        || eventId == PrototypeNativeDenseVegetation.EventId
            && choiceId is "trudge"
        || eventId == PrototypeNativeTabletOfTruth.EventId
            && choiceId is "smash" or "give_up" or "decipher"
        || eventId == "proto.native.underdocks.waterlogged_scriptorium"
            && choiceId is "bloody_ink" or "tentacle_quill"
                or "prickly_sponge"
        || eventId == PrototypeNativeUnderdocksTrashHeap.EventId
            && choiceId is "dive" or "grab"
        || eventId == "proto.native.underdocks.abyssal_baths"
            && choiceId is "abstain" or "exit"
        || eventId == PrototypeNativeEndlessConveyor.EventId
            && choiceId is "observe" or "leave"
        || eventId == PrototypeNativePunchOff.EventId
            && choiceId is "nab"
        || eventId == PrototypeNativeGloryEvents.BattlewornDummyId
            && choiceId is "victory" or "defeat"
        || eventId == PrototypeNativeHiveEvents.AmalgamatorId
            && choiceId is "combine_strikes" or "combine_defends"
        || eventId == PrototypeNativeHiveEvents.InfestedAutomatonId
            && choiceId is "study" or "touch_core"
        || eventId == PrototypeNativeHiveEvents.LostWispId
            && choiceId is "claim" or "search"
        || eventId == PrototypeNativeHiveEvents.SpiritGrafterId
            && choiceId is "let_it_in" or "rejection"
        || eventId == PrototypeNativeHiveEvents.FieldOfManSizedHolesId
            && choiceId is "resist" or "enter_your_hole"
        || eventId == PrototypeNativeHiveEvents.ColossalFlowerId
            && choiceId is "extract" or "relic"
        || eventId == PrototypeNativeStoneOfAllTime.EventId
            && choiceId is "lift" or "push"
        || eventId == PrototypeNativeGloryEvents.ReflectionsId
            && choiceId is "touch_mirror" or "shatter"
        || eventId == PrototypeNativeGloryEvents.RoundTeaPartyId
            && choiceId is "enjoy_tea" or "continue_fight"
        || eventId == PrototypeNativeGloryEvents.TeaMasterId
            && choiceId is "bone_tea" or "ember_tea"
                or "tea_of_discourtesy"
        || eventId == PrototypeNativeGloryEvents.TrialId
            && choiceId is "merchant_guilty" or "merchant_innocent"
                or "noble_guilty" or "noble_innocent"
                or "nondescript_guilty" or "nondescript_innocent"
        || eventId == PrototypeNativeHiveEvents.LanternKeyEventId
            && choiceId is "return_key"
        || eventId == PrototypeNativeHiveEvents.ZenWeaverId
            && choiceId is "breathing" or "emotional" or "arachnid"
        || eventId == PrototypeNativeGloryEvents.HungryForMushroomsId
            && choiceId is "big_mushroom" or "fragrant_mushroom"
        || eventId == PrototypeNativeGloryEvents.GraveOfTheForgottenId
            && choiceId is "confront" or "accept"
        || IsAncientResultChoice(eventId, choiceId)
        || eventId == PrototypeNativeSharedEvents.ThisOrThatId
            && choiceId is "plain" or "ornate"
        || eventId == "proto.native.underdocks.doors_of_light_and_dark"
            && choiceId is "light" or "dark"
        || eventId == "proto.native.underdocks.drowning_beacon"
            && choiceId is "bottle" or "climb"
        || eventId == "proto.native.underdocks.sunken_treasury"
            && choiceId is "first_chest" or "second_chest"
        || eventId == "proto.native.event.sunken_statue"
            && choiceId is "grab" or "dive"
        || eventId == "proto.native.underdocks.sunken_statue"
            && choiceId is "sword" or "dive";

    private static bool IsAncientResultChoice(
        string eventId, string? choiceId) =>
        choiceId is not null
        && (eventId is NativeAncientRewardsV111.TezcataraId
            or NativePaelRewardsV111.PaelId
            or NativeOrobasEventV111.EventId
            or NativeTanxEventV111.EventId
            or NativeVakuuEventV111.EventId
            or NativeNonupeipeEventV111.EventId
            or NativeDarvEventV111.EventId)
        && PrototypeContent.Event(eventId).Choices.Any(choice =>
            StringComparer.Ordinal.Equals(choice.Id, choiceId));
}
