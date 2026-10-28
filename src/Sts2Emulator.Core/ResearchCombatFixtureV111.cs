using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>One persistent, physical card in a research opening deck.</summary>
public sealed record ResearchCombatCardV111(
    string CardId, int UpgradeLevel = 0);

/// <summary>
/// Opt-in, source-supported A10 Silent combat *initial conditions*. The spec is
/// independent of the chance seed; encounter RNG, initial shuffle, card hooks,
/// intents and future card order remain engine/provider owned.
/// </summary>
public sealed record ResearchCombatFixtureV111(
    string EncounterId,
    string NativeEncounterEntry,
    int TotalFloor,
    ResearchCombatCardV111[] Deck,
    string[] RelicIds,
    string?[] PotionIds,
    int Hp = 70,
    int MaxHp = 70,
    int MapColumn = 3,
    int MapRow = 1,
    int Act = 1,
    int Ascension = 10,
    string DistributionId = ResearchCombatFixtureGeneratorV111.DistributionId)
{
    [JsonIgnore]
    public string SpecHash => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool IsBoss => TotalFloor == 6;

    public void Validate()
    {
        if (DistributionId != ResearchCombatFixtureGeneratorV111.DistributionId ||
            Act != 1 || Ascension != 10 || MapColumn != 3 ||
            !ResearchCombatFixtureGeneratorV111.SupportedEncounters.Any(e =>
                e.EncounterId == EncounterId &&
                e.NativeEntry == NativeEncounterEntry &&
                e.TotalFloor == TotalFloor && e.MapRow == MapRow))
            throw new NotSupportedException(
                "UNSUPPORTED_RESEARCH_FIXTURE: unapproved Act 1 A10 single-enemy encounter/floor/distribution");

        if (Deck is null || Deck.Length is < 12 or > 24 ||
            Deck.Any(card => card is null))
            throw new InvalidDataException(
                "INVALID_RESEARCH_FIXTURE: deck must contain 12–24 physical cards");
        foreach (var card in Deck)
        {
            if (!ResearchCombatFixtureGeneratorV111.SupportedCardIds.Contains(
                    card.CardId, StringComparer.Ordinal))
                throw new NotSupportedException(
                    "UNSUPPORTED_RESEARCH_FIXTURE: card " + card.CardId);
            var model = PrototypeContent.Card(card.CardId);
            if (card.UpgradeLevel < 0 ||
                card.UpgradeLevel > model.MaxUpgradeLevel ||
                !model.MechanicsImplemented || model.MultiplayerOnly)
                throw new InvalidDataException(
                    "INVALID_RESEARCH_FIXTURE: card upgrade/implementation " +
                    card.CardId);
        }
        if (RelicIds is null || RelicIds.Length is < 1 or > 3 ||
            RelicIds[0] != "proto.relic.silent_ring" ||
            RelicIds.Distinct(StringComparer.Ordinal).Count() != RelicIds.Length)
            throw new InvalidDataException(
                "INVALID_RESEARCH_FIXTURE: Silent starter relic plus at most two distinct research relics");
        foreach (var relicId in RelicIds)
        {
            if (!ResearchCombatFixtureGeneratorV111.SupportedRelicIds.Contains(
                    relicId, StringComparer.Ordinal))
                throw new NotSupportedException(
                    "UNSUPPORTED_RESEARCH_FIXTURE: relic " + relicId);
            _ = PrototypeContent.Relic(relicId);
        }
        if (PotionIds is null || PotionIds.Length != PrototypeContent.Rules.PotionSlots)
            throw new InvalidDataException(
                "INVALID_RESEARCH_FIXTURE: potion slots must match the Silent ruleset");
        foreach (var potionId in PotionIds)
        {
            if (potionId is null)
                continue;
            if (!ResearchCombatFixtureGeneratorV111.SupportedPotionIds.Contains(
                    potionId, StringComparer.Ordinal))
                throw new NotSupportedException(
                    "UNSUPPORTED_RESEARCH_FIXTURE: potion " + potionId);
            _ = PrototypeContent.Potion(potionId);
        }
        // Source-shaped initial health is conditioned *before* combat, never
        // patched into a future continuation or a probability result.
        if (MaxHp != PrototypeContent.Rules.StartingHp ||
            Hp < 1 || Hp > MaxHp)
            throw new InvalidDataException(
                "INVALID_RESEARCH_FIXTURE: bounded A10 Silent starting health");
    }

    public void ValidateFor(SemanticSeededEntryV111 entry)
    {
        Validate();
        if (entry.Act != Act || entry.Ascension != Ascension ||
            entry.EncounterId != EncounterId ||
            entry.NativeEncounterEntry != NativeEncounterEntry ||
            entry.TotalFloor != TotalFloor ||
            entry.MapColumn != MapColumn || entry.MapRow != MapRow ||
            entry.NativePolicyBaseline || entry.SourceRouteEntry ||
            entry.FixtureDeckPresetId is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_RESEARCH_FIXTURE: entry/spec disagreement or incompatible research mode");
    }

    public PlayerState ApplyBeforeOpening(PlayerState starter)
    {
        Validate();
        if (starter.Deck.Length != PrototypeContent.StartingDeck.Length)
            throw new InvalidDataException(
                "INVALID_RESEARCH_FIXTURE: expected unmodified Silent starter");
        return starter with
        {
            Deck = Deck.Select((card, i) =>
                new CardInstance(i + 1L, card.CardId, card.UpgradeLevel,
                    PrototypeJson.EmptyObject())).ToArray(),
            Relics = RelicIds.Select(id =>
                new RelicInstance(id, PrototypeJson.EmptyObject())).ToArray(),
            PotionSlots = PotionIds.Select(id =>
                id is null ? null :
                    new PotionInstance(id, PrototypeJson.EmptyObject())).ToArray(),
            Hp = Hp,
            MaxHp = MaxHp
        };
    }
}

/// <summary>
/// Auditable research distribution; the spec index and curriculum seed select
/// structural loadouts, while SemanticSeededEntry.Seed selects independent
/// engine-owned stochastic outcomes. No claim of native game frequencies.
/// </summary>
public static class ResearchCombatFixtureGeneratorV111
{
    public const string DistributionId = "silent-a10-structured-combat-v1";

    public static readonly (string EncounterId, string NativeEntry,
        int TotalFloor, int MapRow)[] SupportedEncounters =
    [
        ("proto.encounter.shrinker_beetle_weak", "ShrinkerBeetleWeak", 2, 1),
        ("proto.encounter.nibbits_weak", "NibbitsWeak", 2, 1),
        ("proto.encounter.mawler_normal", "MawlerNormal", 2, 1),
        ("proto.encounter.cubex_construct_normal", "CubexConstructNormal", 2, 1),
        ("proto.encounter.vine_shambler_normal", "VineShamblerNormal", 2, 1),
        ("proto.encounter.ceremonial_beast_boss", "CeremonialBeastBoss", 6, 5),
        ("proto.encounter.vantom_boss", "VantomBoss", 6, 5)
    ];

    // Conservative explicit support list: newly registered cards must pass
    // real playable-combat tests before admission.
    public static readonly string[] SupportedCardIds =
    [
        "proto.silent.strike", "proto.silent.defend",
        "proto.silent.neutralize", "proto.silent.survivor",
        "proto.silent.acrobatics", "proto.silent.backflip",
        "proto.silent.slice", "proto.silent.poisoned_stab",
        "proto.silent.deadly_poison", "proto.silent.catalyst"
    ];

    public static readonly string[] SupportedRelicIds =
    [
        "proto.relic.silent_ring",
        "proto.relic.lantern", "proto.relic.vajra",
        "proto.relic.ornamental_fan", "proto.relic.orichalcum"
    ];

    public static readonly string[] SupportedPotionIds =
    [
        "proto.potion.block", "proto.potion.fire", "proto.potion.poison"
    ];

    private static readonly string[] Additions =
    [
        "proto.silent.acrobatics", "proto.silent.backflip",
        "proto.silent.slice", "proto.silent.poisoned_stab",
        "proto.silent.deadly_poison", "proto.silent.catalyst"
    ];

    private static ulong Roll(int curriculumSeed, int index, string domain, int ordinal)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{DistributionId}:{curriculumSeed}:{index}:{domain}:{ordinal}"));
        return BinaryPrimitives.ReadUInt64BigEndian(hash);
    }

    public static ResearchCombatFixtureV111 Generate(int index, int curriculumSeed = 0)
    {
        if (index < 0 || curriculumSeed < 0)
            throw new ArgumentOutOfRangeException(nameof(index),
                "Research fixture index and curriculum seed must be nonnegative");
        var encounter = SupportedEncounters[
            (int)(Roll(curriculumSeed, index, "encounter", 0) %
                (ulong)SupportedEncounters.Length)];
        var cards = PrototypeContent.StartingDeck
            .Select(id => new ResearchCombatCardV111(id)).ToList();
        for (int i = 0; i < 8; i++)
        {
            string id = Additions[(int)(Roll(curriculumSeed, index,
                "card", i) % (ulong)Additions.Length)];
            int level = Roll(curriculumSeed, index, "upgrade", i) % 5 == 0 ? 1 : 0;
            cards.Add(new ResearchCombatCardV111(id, level));
        }
        // Canonical multiset order prevents counting a mere card permutation
        // as a structurally new deck.
        var deck = cards.OrderBy(c => c.CardId, StringComparer.Ordinal)
            .ThenBy(c => c.UpgradeLevel).ToArray();
        var extraRelics = SupportedRelicIds.Skip(1).ToArray();
        int first = (int)(Roll(curriculumSeed, index, "relic", 0) %
            (ulong)extraRelics.Length);
        int second = (first + 1 + (int)(Roll(curriculumSeed, index,
            "relic", 1) % (ulong)(extraRelics.Length - 1))) %
            extraRelics.Length;
        var relicIds = new[]
        {
            "proto.relic.silent_ring", extraRelics[first], extraRelics[second]
        };
        var potionSlots = new string?[PrototypeContent.Rules.PotionSlots];
        if (Roll(curriculumSeed, index, "potion-count", 0) % 3 == 0)
            potionSlots[0] = SupportedPotionIds[
                (int)(Roll(curriculumSeed, index, "potion-kind", 0) %
                    (ulong)SupportedPotionIds.Length)];
        var spec = new ResearchCombatFixtureV111(
            encounter.EncounterId, encounter.NativeEntry, encounter.TotalFloor,
            deck, relicIds, potionSlots,
            Hp: new[] { 45, 55, 65, 70 }[(int)(
                Roll(curriculumSeed, index, "hp", 0) % 4)],
            MapRow: encounter.MapRow);
        spec.Validate();
        return spec;
    }

    public static ResearchCombatFixtureV111[] GenerateDistinct(
        int count, int curriculumSeed = 0)
    {
        if (count < 1 || count > 10000)
            throw new ArgumentOutOfRangeException(nameof(count));
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<ResearchCombatFixtureV111>(count);
        // Deliberately bounded; colliding decks are rejected rather than
        // counted as separate research contexts.
        for (int i = 0; i < count * 20 && results.Count < count; i++)
        {
            var spec = Generate(i, curriculumSeed);
            if (hashes.Add(spec.SpecHash))
                results.Add(spec);
        }
        if (results.Count != count)
            throw new InvalidOperationException(
                "Research curriculum lacks the requested distinct configurations");
        return results.ToArray();
    }
}
