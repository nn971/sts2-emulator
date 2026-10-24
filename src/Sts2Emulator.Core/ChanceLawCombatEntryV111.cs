using System.Globalization;

namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    /// <summary>
    /// Query one source-shaped Native encounter-formation index. The query
    /// binds the declared model entry and floor to the encounter-local
    /// context stream, while leaving the candidate formation and preceding
    /// GenerateMonsters prefix with the caller.
    /// </summary>
    public static ChanceLawV111 QueryNativeEncounterFormationIndex(
        string encounterEntry, int totalFloor, int supportCount, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterEntry);
        if (totalFloor < 0)
            throw new ArgumentOutOfRangeException(nameof(totalFloor));
        if (supportCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(supportCount));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var siteId = NativeEncounterFormationFactoryV111.FormationSiteId;
        var streamId = NativeRngV111Contexts.EncounterFormationStreamId(
            encounterEntry, totalFloor);
        var ownerId = $"encounter:{encounterEntry}:floor:{totalFloor}";
        var request = new RandomIndexRequestV111(
            siteId, streamId, ownerId, ordinal,
            Enumerable.Range(0, supportCount).ToArray());
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native-encounter-formation-index",
            request
        });
        return QueryRandomIndex(eventId, request);
    }

    /// <summary>
    /// Query one Native unique-monster-HP index over a caller-declared
    /// ordered HP support. The caller owns the live eligibility pool and
    /// creature creation history; this law covers only the run.niche index.
    /// </summary>
    public static ChanceLawV111 QueryNativeInitialHpIndex(
        string ownerId, int[] orderedHpSupport, int ordinal)
    {
        ValidateNativeCreatureOwnerId(ownerId);
        ArgumentNullException.ThrowIfNull(orderedHpSupport);
        if (orderedHpSupport.Length == 0 ||
            orderedHpSupport.Any(hp => hp <= 0) ||
            orderedHpSupport.Distinct().Count() != orderedHpSupport.Length)
            throw new ArgumentException(
                "Native initial-HP support must contain distinct positive HP values.",
                nameof(orderedHpSupport));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var request = new RandomIndexRequestV111(
            NativeCombatRngEntryV111.InitialUniqueHpSiteId,
            "run.niche", ownerId, ordinal,
            orderedHpSupport);
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native-initial-unique-hp-index",
            request
        });
        return QueryRandomIndex(eventId, request);
    }

    /// <summary>
    /// Query the Native in-place Tough Egg hatch HP index. Ascension selects
    /// the source interval; the caller owns the event/creature eligibility.
    /// </summary>
    public static ChanceLawV111 QueryNativeToughEggHatchHpIndex(
        string ownerId, int ascension, int ordinal)
    {
        ValidateNativeCreatureOwnerId(ownerId);
        if (ascension < 0)
            throw new ArgumentOutOfRangeException(nameof(ascension));
        if (ordinal != 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Tough Egg hatch HP is the creature's ordinal-zero draw.");

        var minHp = ascension >= 8 ? 20 : 19;
        var orderedHpSupport = Enumerable.Range(minHp, 4).ToArray();
        var request = new RandomIndexRequestV111(
            NativeCombatRngEntryV111.ToughEggHatchHpSiteId,
            "run.niche", ownerId, ordinal, orderedHpSupport);
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native-tough-egg-hatch-hp-index",
            ascension,
            request
        });
        return QueryRandomIndex(eventId, request);
    }

    private static void ValidateNativeCreatureOwnerId(string ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        const string ownerPrefix = "creature:";
        if (!ownerId.StartsWith(ownerPrefix, StringComparison.Ordinal) ||
            !long.TryParse(ownerId.AsSpan(ownerPrefix.Length),
                NumberStyles.None, CultureInfo.InvariantCulture,
                out var combatId) || combatId < 0 ||
            ownerId != ownerPrefix + combatId.ToString(
                CultureInfo.InvariantCulture))
            throw new ArgumentException(
                "Native HP owner must identify one canonical creature CombatId.",
                nameof(ownerId));
    }

}
