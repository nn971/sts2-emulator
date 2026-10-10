using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public enum ActIdentity { Overgrowth, Underdocks, Hive, Glory }

public static class V111Build
{
    public const string Version = "v0.111.0";
    public const string Commit = "41cef1ea";
    public const string Fingerprint = "3bb5598a35f7763c9de22078643ac2777190994b2be85ebd4ebf5c9d154aa45e";
    public const string CorpusCommit = "b59c6f96043051afee8683237d04f943309ced35";
    public const string SourceCommit = "706ef1c9fa2219220065849fd5265328607ece20";
    public const string Id = Version + "/" + Commit + "/" + Fingerprint;
    public const string RulesetId = "sts2-v111-single-player-v1";
    public const string StateSchema = "sts2-run-v111-v1";
}

public sealed record SimulationProfile(
    bool FullyUnlocked, string[] UnlockedCharacters, string[] RevealedEpochs)
{
    public static SimulationProfile Unlocked() => new(true, [], []);
    public SimulationProfile Fork() => this with
    {
        UnlockedCharacters = (string[])UnlockedCharacters.Clone(),
        RevealedEpochs = (string[])RevealedEpochs.Clone()
    };
}

public sealed record RunConfiguration(
    string CharacterId, int Ascension, ActIdentity[] Acts,
    SimulationProfile Profile, string RulesetId = V111Build.RulesetId,
    string BuildFingerprint = V111Build.Fingerprint,
    string FidelityId = "v111-mechanics-prototype-rng-v1")
{
    public static RunConfiguration Create(
        string characterId = "silent", int ascension = 0,
        ActIdentity firstAct = ActIdentity.Overgrowth,
        SimulationProfile? profile = null) =>
        new(characterId, ascension, [firstAct, ActIdentity.Hive, ActIdentity.Glory],
            profile?.Fork() ?? SimulationProfile.Unlocked());

    public RunConfiguration Fork() => this with
    {
        Acts = (ActIdentity[])Acts.Clone(), Profile = Profile.Fork()
    };

    public void Validate()
    {
        if (Acts is null || Profile is null || Profile.UnlockedCharacters is null || Profile.RevealedEpochs is null)
            throw new InvalidDataException("Run configuration requires explicit route and profile arrays.");
        _ = V111Characters.Get(CharacterId);
        if (Ascension is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(Ascension), "v111 supports Ascension 0 through 10.");
        if (RulesetId != V111Build.RulesetId || BuildFingerprint != V111Build.Fingerprint
            || FidelityId != "v111-mechanics-prototype-rng-v1")
            throw new NotSupportedException("Configured runs require the pinned v111 ruleset and fingerprint.");
        if (Acts.Length != 3 || Acts[0] is not (ActIdentity.Overgrowth or ActIdentity.Underdocks)
            || Acts[1] != ActIdentity.Hive || Acts[2] != ActIdentity.Glory)
            throw new ArgumentException("v111 route must be Overgrowth or Underdocks, then Hive and Glory.");
        if (!Profile.FullyUnlocked && CharacterId != "ironclad"
            && !Profile.UnlockedCharacters.Contains(CharacterId, StringComparer.Ordinal))
            throw new NotSupportedException($"Character '{CharacterId}' is locked in this profile.");
    }

    public static void ValidateState(RunState state)
    {
        if (state.Configuration is not { } configuration)
        {
            if (state.GameBuild == V111Build.Id || state.EmulatorSchema == V111Build.StateSchema
                || state.World?.RulesetId == V111Build.RulesetId)
                throw new InvalidDataException("v111 state requires explicit run configuration.");
            return;
        }
        configuration.Validate();
        if (state.GameBuild != V111Build.Id || state.EmulatorSchema != V111Build.StateSchema
            || state.Ascension != configuration.Ascension)
            throw new InvalidDataException("Run state does not match its v111 configuration/build identity.");
        if (state.World is { } world && (world.RulesetId != configuration.RulesetId
            || world.CharacterId != configuration.CharacterId || world.Act is < 1 or > 3
            || world.ActIdentity != configuration.Acts[world.Act - 1]))
            throw new InvalidDataException("World ruleset, character or act does not match its configuration.");
        if (state.World is { } initialized)
        {
            if (configuration.CharacterId != "silent")
                throw new NotSupportedException("Initialized v111 character mechanics are currently limited to Silent.");
            if (initialized.Act == 1)
            {
                var region = configuration.Acts[0] == ActIdentity.Overgrowth
                    ? PrototypeActOneRegion.Overgrowth : PrototypeActOneRegion.Underdocks;
                var mapProfile = region == PrototypeActOneRegion.Overgrowth
                    ? PrototypeNativeOvergrowthMap.GenerationProfileId : PrototypeNativeUnderdocks.GenerationProfileId;
                if (initialized.ActOneRegion != region
                    || initialized.Map.GenerationProfileId != mapProfile
                    || initialized.LaterActEncounterPool is not null)
                    throw new InvalidDataException(
                        "Act 1 region, map or encounter pool does not match the configured route.");
            }
            else
            {
                var mapProfile = initialized.Act == 2
                    ? PrototypeNativeLaterActRouting.HiveMapProfile
                    : PrototypeNativeLaterActRouting.GloryMapProfile;
                if (initialized.Map.GenerationProfileId != mapProfile
                    || initialized.LaterActEncounterPool is null
                    || initialized.LaterActEncounterPool.Act != initialized.Act
                    || initialized.ActOneEncounterPool is not null)
                    throw new InvalidDataException(
                        "Later-act map and encounter bag must match the configured native route.");
            }
        }
    }
}

public sealed record V111CharacterDefinition(
    string Id, int StartingHp, int StartingGold, int BaseEnergy,
    int OrbSlots, string[] StartingDeck, string[] StartingRelics)
{
    public V111CharacterDefinition Fork() => this with
    {
        StartingDeck = (string[])StartingDeck.Clone(),
        StartingRelics = (string[])StartingRelics.Clone()
    };
}

public static class V111Characters
{
    private static readonly V111CharacterDefinition[] Definitions =
    [
        new("silent", 70, 99, 3, 0,
            ["STRIKE_SILENT", "STRIKE_SILENT", "STRIKE_SILENT", "STRIKE_SILENT", "STRIKE_SILENT",
             "DEFEND_SILENT", "DEFEND_SILENT", "DEFEND_SILENT", "DEFEND_SILENT", "DEFEND_SILENT", "NEUTRALIZE", "SURVIVOR"], ["RING_OF_THE_SNAKE"]),
        new("ironclad", 80, 99, 3, 0,
            ["STRIKE_IRONCLAD", "STRIKE_IRONCLAD", "STRIKE_IRONCLAD", "STRIKE_IRONCLAD", "STRIKE_IRONCLAD",
             "DEFEND_IRONCLAD", "DEFEND_IRONCLAD", "DEFEND_IRONCLAD", "DEFEND_IRONCLAD", "BASH"], ["BURNING_BLOOD"]),
        new("defect", 75, 99, 3, 3,
            ["STRIKE_DEFECT", "STRIKE_DEFECT", "STRIKE_DEFECT", "STRIKE_DEFECT",
             "DEFEND_DEFECT", "DEFEND_DEFECT", "DEFEND_DEFECT", "DEFEND_DEFECT", "ZAP", "DUALCAST"], ["CRACKED_CORE"]),
        new("regent", 75, 99, 3, 0,
            ["STRIKE_REGENT", "STRIKE_REGENT", "STRIKE_REGENT", "STRIKE_REGENT",
             "DEFEND_REGENT", "DEFEND_REGENT", "DEFEND_REGENT", "DEFEND_REGENT", "FALLING_STAR", "VENERATE"], ["DIVINE_RIGHT"]),
        new("necrobinder", 66, 99, 3, 0,
            ["STRIKE_NECROBINDER", "STRIKE_NECROBINDER", "STRIKE_NECROBINDER", "STRIKE_NECROBINDER",
             "DEFEND_NECROBINDER", "DEFEND_NECROBINDER", "DEFEND_NECROBINDER", "DEFEND_NECROBINDER", "BODYGUARD", "UNLEASH"], ["BOUND_PHYLACTERY"])
    ];
    public static V111CharacterDefinition[] All => Definitions.Select(x => x.Fork()).ToArray();
    public static V111CharacterDefinition Get(string id) =>
        (Definitions.SingleOrDefault(x => x.Id == id)
            ?? throw new NotSupportedException($"Unknown single-player v111 character '{id}'.")).Fork();
}

public sealed record OrbState(string Kind, int StoredValue = 0);
public sealed record SecondaryCreatureState(int Hp, int MaxHp, int SummonAmount);
public sealed record CharacterResourceState(
    int Stars = 0, int Forge = 0, int OrbSlots = 0, OrbState[]? Orbs = null,
    SecondaryCreatureState? Osty = null)
{
    public CharacterResourceState Fork() => this with { Orbs = Orbs is null ? null : (OrbState[])Orbs.Clone() };
}

public static class V111RunFactory
{
    public static RunState Create(string seed, RunConfiguration? configuration = null)
    {
        var owned = (configuration ?? RunConfiguration.Create()).Fork();
        owned.Validate();
        return PrototypeGameFactory.Create(seed, owned.Ascension) with
        {
            GameBuild = V111Build.Id, EmulatorSchema = V111Build.StateSchema,
            RunId = $"v111-{owned.CharacterId}-{seed}", Configuration = owned
        };
    }

    internal static RunState Initialize(RunState input)
    {
        var config = input.Configuration ?? throw new InvalidDataException("Missing v111 configuration.");
        if (config.CharacterId != "silent")
            throw new NotSupportedException($"v111 '{config.CharacterId}' mechanics are not implemented; no Silent substitution is allowed.");
        if (!config.Profile.FullyUnlocked)
            throw new NotSupportedException("Restricted-profile acquisition/progression rules are not implemented; choose an explicit fully unlocked profile.");
        var opening = config.Acts[0] == ActIdentity.Overgrowth
            ? PrototypeNativeOvergrowthRunFactory.Create(input.RunSeed, config.Ascension)
            : PrototypeNativeUnderdocksRunFactory.Create(input.RunSeed, config.Ascension);
        // Restlessness is a historical training adapter addition, not a native
        // A0 starter. Keep it only in the explicit legacy factories.
        var deck = config.Ascension < 5
            ? opening.Player.Deck.Where(card => card.CardId != "proto.common.restlessness").ToArray()
            : opening.Player.Deck;
        return opening with
        {
            GameBuild = input.GameBuild, EmulatorSchema = input.EmulatorSchema,
            RunId = input.RunId, DecisionIndex = input.DecisionIndex,
            Configuration = config.Fork(), Player = opening.Player with { Deck = deck },
            World = opening.World! with { RulesetId = config.RulesetId, ActIdentity = config.Acts[0] }
        };
    }
}
