using Xunit;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Sanitized source-observed native ActModel._rooms queues from a real
/// pinned v0.111 Silent A10 run, seed 6JK8MD0VQQTA, captured at
/// RunManager.RunStarted on 2026-11-01. Contains ONLY model identities;
/// no personal identifiers, user paths or game assets.
/// </summary>
public sealed class NativeStartupRoomQueueCapturedEvidenceV111Tests
{
    private sealed record NativeAct(
        string Events, string Normals, string Elites,
        string Boss, string Ancient, string? SecondBoss);

    private static readonly NativeAct[] Observed =
    [
        new(
            "LUMINOUS_CHOIR RANWID_THE_ELDER JUNGLE_MAZE_ADVENTURE DENSE_VEGETATION BRAIN_LEECH WELLSPRING THE_FUTURE_OF_POTIONS FAKE_MERCHANT BYRDONIS_NEST AROMA_OF_CHAOS TABLET_OF_TRUTH THE_LEGENDS_WERE_TRUE SYMBIOTE UNREST_SITE CRYSTAL_SPHERE WOOD_CARVINGS WELCOME_TO_WONGOS STONE_OF_ALL_TIME POTION_COURIER SLIPPERY_BRIDGE TEA_MASTER THIS_OR_THAT RELIC_TRADER MORPHIC_GROVE WAR_HISTORIAN_REPY ROOM_FULL_OF_CHEESE SUNKEN_STATUE SAPPHIRE_SEED WHISPERING_HOLLOW SELF_HELP_BOOK DOLL_ROOM",
            "FUZZY_WURM_CRAWLER_WEAK SLIMES_WEAK NIBBITS_WEAK NIBBITS_NORMAL RUBY_RAIDERS_NORMAL SLITHERING_STRANGLER_NORMAL FOGMOG_NORMAL SLIMES_NORMAL VINE_SHAMBLER_NORMAL INKLETS_NORMAL SNAPPING_JAXFRUIT_NORMAL CUBEX_CONSTRUCT_NORMAL OVERGROWTH_CRAWLERS MAWLER_NORMAL FLYCONID_NORMAL",
            "PHROG_PARASITE_ELITE BYGONE_EFFIGY_ELITE BYRDONIS_ELITE BYGONE_EFFIGY_ELITE BYRDONIS_ELITE PHROG_PARASITE_ELITE BYRDONIS_ELITE PHROG_PARASITE_ELITE BYGONE_EFFIGY_ELITE BYRDONIS_ELITE BYGONE_EFFIGY_ELITE PHROG_PARASITE_ELITE BYRDONIS_ELITE BYGONE_EFFIGY_ELITE PHROG_PARASITE_ELITE",
            "THE_KIN_BOSS", "NEOW", null),
        new(
            "SPIRIT_GRAFTER RANWID_THE_ELDER POTION_COURIER DOLL_ROOM BRAIN_LEECH RELIC_TRADER CRYSTAL_SPHERE COLOSSAL_FLOWER COLORFUL_PHILOSOPHERS WELCOME_TO_WONGOS SYMBIOTE STONE_OF_ALL_TIME FIELD_OF_MAN_SIZED_HOLES SLIPPERY_BRIDGE WAR_HISTORIAN_REPY THIS_OR_THAT THE_LANTERN_KEY SELF_HELP_BOOK ROOM_FULL_OF_CHEESE ZEN_WEAVER LOST_WISP THE_LEGENDS_WERE_TRUE AMALGAMATOR BUGSLAYER FAKE_MERCHANT THE_FUTURE_OF_POTIONS INFESTED_AUTOMATON TEA_MASTER",
            "BOWLBUGS_WEAK TUNNELER_WEAK OVICOPTER_NORMAL MYTES_NORMAL CHOMPERS_NORMAL BOWLBUGS_NORMAL SPINY_TOAD_NORMAL SLUMBERING_BEETLE_NORMAL THE_OBSCURA_NORMAL EXOSKELETONS_NORMAL HUNTER_KILLER_NORMAL LOUSE_PROGENITOR_NORMAL THE_OBSCURA_NORMAL EXOSKELETONS_NORMAL",
            "DECIMILLIPEDE_ELITE INFESTED_PRISMS_ELITE ENTOMANCER_ELITE DECIMILLIPEDE_ELITE ENTOMANCER_ELITE INFESTED_PRISMS_ELITE DECIMILLIPEDE_ELITE ENTOMANCER_ELITE INFESTED_PRISMS_ELITE DECIMILLIPEDE_ELITE ENTOMANCER_ELITE INFESTED_PRISMS_ELITE ENTOMANCER_ELITE DECIMILLIPEDE_ELITE INFESTED_PRISMS_ELITE",
            "KAISER_CRAB_BOSS", "OROBAS", null),
        new(
            "BRAIN_LEECH CRYSTAL_SPHERE HUNGRY_FOR_MUSHROOMS TINKER_TIME POTION_COURIER THE_FUTURE_OF_POTIONS SLIPPERY_BRIDGE WAR_HISTORIAN_REPY BATTLEWORN_DUMMY DOLL_ROOM GRAVE_OF_THE_FORGOTTEN TEA_MASTER ROOM_FULL_OF_CHEESE RELIC_TRADER ROUND_TEA_PARTY FAKE_MERCHANT THE_LEGENDS_WERE_TRUE RANWID_THE_ELDER SELF_HELP_BOOK THIS_OR_THAT REFLECTIONS SYMBIOTE STONE_OF_ALL_TIME WELCOME_TO_WONGOS TRIAL",
            "TURRET_OPERATOR_WEAK SCROLLS_OF_BITING_WEAK SLIMED_BERSERKER_NORMAL SCROLLS_OF_BITING_NORMAL FABRICATOR_NORMAL FROG_KNIGHT_NORMAL AXEBOTS_NORMAL CONSTRUCT_MENAGERIE_NORMAL THE_LOST_AND_FORGOTTEN_NORMAL OWL_MAGISTRATE_NORMAL GLOBE_HEAD_NORMAL CONSTRUCT_MENAGERIE_NORMAL SLIMED_BERSERKER_NORMAL",
            "KNIGHTS_ELITE MECHA_KNIGHT_ELITE SOUL_NEXUS_ELITE KNIGHTS_ELITE SOUL_NEXUS_ELITE MECHA_KNIGHT_ELITE KNIGHTS_ELITE SOUL_NEXUS_ELITE MECHA_KNIGHT_ELITE KNIGHTS_ELITE SOUL_NEXUS_ELITE MECHA_KNIGHT_ELITE SOUL_NEXUS_ELITE KNIGHTS_ELITE MECHA_KNIGHT_ELITE",
            "TEST_SUBJECT_BOSS", "TANX", "QUEEN_BOSS")
    ];

    private static string Normalize(string source) =>
        new(source.Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant).ToArray());

    private static string[] Names(string source) =>
        source.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize).ToArray();

    [Fact]
    public void CompleteThreeActQueuesMatchIndependentlyObservedNativeIdentities()
    {
        var rng = NativeRngV111.CreateBundle("6JK8MD0VQQTA");
        var bags = NativeUpFrontRelicBagsV111.Populate(rng);
        var generated = NativeUpFrontRoomsV111.Generate(rng, 10);

        Assert.Equal(112, bags.AfterSharedBag.Counter);
        Assert.Equal(230, bags.AfterPlayerBag.Counter);
        Assert.Equal(232, generated.AfterSharedAncients.Counter);
        Assert.Equal(412, generated.FinalRng.Counter);
        Assert.Equal(3, generated.Acts.Length);
        for (var index = 0; index < Observed.Length; index++)
        {
            var source = Observed[index];
            var predicted = generated.Acts[index];
            Assert.Equal(index + 1, predicted.Act);
            Assert.Equal(Names(source.Events), predicted.Events.Select(Normalize));
            Assert.Equal(Names(source.Normals), predicted.NormalEncounters.Select(Normalize));
            Assert.Equal(Names(source.Elites), predicted.EliteEncounters.Select(Normalize));
            Assert.Equal(Normalize(source.Boss), Normalize(predicted.BossEncounter));
            Assert.Equal(Normalize(source.Ancient), Normalize(predicted.Ancient));
            Assert.Equal(source.SecondBoss is null ? null : Normalize(source.SecondBoss),
                predicted.SecondBossEncounter is null
                    ? null : Normalize(predicted.SecondBossEncounter));
        }
    }
}
