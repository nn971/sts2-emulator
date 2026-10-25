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
    // Explicit experimental opt-in. Prototype room selection/card/effect
    // randomness is NOT made native by this combat-entry milestone.
    public const string NativeWeakCombatEntryFidelityId =
        "v111-experimental-native-weak-combat-entry-v1";
    // Explicit opt-in to a deliberately restricted three-act Silent route.
    // Its late-act encounter/event bags and combat RNG are provisional.
    public const string ExperimentalSilentThreeActRouteFidelityId =
        "v111-experimental-silent-three-act-route-v1";
}

public sealed record SimulationProfile
{
    private string[]? _unlockedCharacters;
    private string[]? _revealedEpochs;
    private string[]? _unlockedColorlessEpochs;
    private string[]? _seenBossEncounters = [];

    [JsonConstructor]
    public SimulationProfile(
        bool FullyUnlocked,
        string[] UnlockedCharacters,
        string[] RevealedEpochs)
    {
        this.FullyUnlocked = FullyUnlocked;
        this.UnlockedCharacters = UnlockedCharacters;
        this.RevealedEpochs = RevealedEpochs;
    }

    public bool FullyUnlocked { get; init; }
    public string[] UnlockedCharacters
    {
        get => _unlockedCharacters is null
            ? null! : (string[])_unlockedCharacters.Clone();
        init => _unlockedCharacters = Copy(value);
    }
    public string[] RevealedEpochs
    {
        get => _revealedEpochs is null
            ? null! : (string[])_revealedEpochs.Clone();
        init => _revealedEpochs = Copy(value);
    }

    /// <summary>
    /// Colorless1Epoch through Colorless5Epoch classes unlocked on this
    /// account. On a partial profile, null means unknown; an empty array means
    /// no Colorless epochs are unlocked.
    /// A supplied list is authoritative even when FullyUnlocked is true;
    /// omitting it on a fully unlocked profile admits all five epochs.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? UnlockedColorlessEpochs
    {
        get => Copy(_unlockedColorlessEpochs);
        init => _unlockedColorlessEpochs = Copy(value);
    }

    /// <summary>
    /// Bosses already discovered by the player before this run. ActModel's
    /// discovery-order modification uses this history when it replaces the
    /// randomly preselected Hive/Glory boss.
    /// </summary>
    public string[] SeenBossEncounters
    {
        get => _seenBossEncounters is null
            ? null! : (string[])_seenBossEncounters.Clone();
        init => _seenBossEncounters = Copy(value);
    }

    public static SimulationProfile Unlocked() => new(true, [], []);

    public SimulationProfile Fork() => new(
        FullyUnlocked, _unlockedCharacters!, _revealedEpochs!)
    {
        UnlockedColorlessEpochs = _unlockedColorlessEpochs,
        SeenBossEncounters = _seenBossEncounters!
    };

    public void Deconstruct(
        out bool FullyUnlocked,
        out string[] UnlockedCharacters,
        out string[] RevealedEpochs)
    {
        FullyUnlocked = this.FullyUnlocked;
        UnlockedCharacters = this.UnlockedCharacters;
        RevealedEpochs = this.RevealedEpochs;
    }

    private static string[]? Copy(string[]? values) =>
        values is null ? null : (string[])values.Clone();
}

public sealed record RunConfiguration
{
    private ActIdentity[]? _acts;
    private SimulationProfile? _profile;

    [JsonConstructor]
    public RunConfiguration(
        string CharacterId,
        int Ascension,
        ActIdentity[] Acts,
        SimulationProfile Profile,
        string RulesetId = V111Build.RulesetId,
        string BuildFingerprint = V111Build.Fingerprint,
        string FidelityId = "v111-mechanics-prototype-rng-v1")
    {
        this.CharacterId = CharacterId;
        this.Ascension = Ascension;
        this.Acts = Acts;
        this.Profile = Profile;
        this.RulesetId = RulesetId;
        this.BuildFingerprint = BuildFingerprint;
        this.FidelityId = FidelityId;
    }

    public string CharacterId { get; init; }
    public int Ascension { get; init; }
    public ActIdentity[] Acts
    {
        get => _acts is null ? null! : (ActIdentity[])_acts.Clone();
        init => _acts = Copy(value);
    }
    public SimulationProfile Profile
    {
        get => _profile!;
        init => _profile = value?.Fork();
    }
    public string RulesetId { get; init; }
    public string BuildFingerprint { get; init; }
    public string FidelityId { get; init; }

    public bool UsesExperimentalNativeWeakCombatEntry =>
        FidelityId == V111Build.NativeWeakCombatEntryFidelityId;
    public bool UsesExperimentalSilentThreeActRoute =>
        FidelityId == V111Build.ExperimentalSilentThreeActRouteFidelityId;

    public static RunConfiguration Create(
        string characterId = "silent", int ascension = 0,
        ActIdentity firstAct = ActIdentity.Overgrowth,
        SimulationProfile? profile = null) =>
        new(characterId, ascension, [firstAct, ActIdentity.Hive, ActIdentity.Glory],
            profile?.Fork() ?? SimulationProfile.Unlocked());

    public RunConfiguration Fork() => new(
        CharacterId,
        Ascension,
        _acts!,
        _profile!,
        RulesetId,
        BuildFingerprint,
        FidelityId);

    public void Deconstruct(
        out string CharacterId,
        out int Ascension,
        out ActIdentity[] Acts,
        out SimulationProfile Profile,
        out string RulesetId,
        out string BuildFingerprint,
        out string FidelityId)
    {
        CharacterId = this.CharacterId;
        Ascension = this.Ascension;
        Acts = this.Acts;
        Profile = this.Profile;
        RulesetId = this.RulesetId;
        BuildFingerprint = this.BuildFingerprint;
        FidelityId = this.FidelityId;
    }

    private static ActIdentity[]? Copy(ActIdentity[]? values) =>
        values is null ? null : (ActIdentity[])values.Clone();

    public void Validate()
    {
        if (Acts is null || Profile is null || Profile.UnlockedCharacters is null
            || Profile.RevealedEpochs is null || Profile.SeenBossEncounters is null)
            throw new InvalidDataException("Run configuration requires explicit route and profile arrays.");
        if (Profile.SeenBossEncounters.Any(string.IsNullOrWhiteSpace)
            || Profile.SeenBossEncounters.Distinct(StringComparer.Ordinal).Count()
                != Profile.SeenBossEncounters.Length)
            throw new ArgumentException(
                "Profile boss discovery history must contain distinct non-empty encounter IDs.");
        if (Profile.UnlockedColorlessEpochs is { } colorlessEpochs &&
            (colorlessEpochs.Any(epoch => string.IsNullOrWhiteSpace(epoch) ||
                !PrototypeColorlessCards.NativeEpochIds.Contains(
                    epoch, StringComparer.Ordinal)) ||
             colorlessEpochs.Distinct(StringComparer.Ordinal).Count() !=
                colorlessEpochs.Length))
            throw new ArgumentException(
                "Profile Colorless epochs must contain distinct known epoch IDs.");
        _ = V111Characters.Get(CharacterId);
        if (Ascension is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(Ascension), "v111 supports Ascension 0 through 10.");
        if (RulesetId != V111Build.RulesetId || BuildFingerprint != V111Build.Fingerprint
            || FidelityId is not ("v111-mechanics-prototype-rng-v1"
                or V111Build.NativeWeakCombatEntryFidelityId
                or V111Build.ExperimentalSilentThreeActRouteFidelityId))
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
            if (initialized.Combat is { } combat &&
                combat.ConfiguredColorlessGenerationFullyUnlocked is
                    { } colorlessFullyUnlocked &&
                colorlessFullyUnlocked !=
                    configuration.Profile.FullyUnlocked)
                throw new InvalidDataException(
                    "Combat Colorless-generation profile differs from run configuration.");
            if (initialized.Combat is { } colorlessCombat)
            {
                var combatColorlessEpochs =
                    colorlessCombat.ConfiguredUnlockedColorlessEpochs;
                var configuredColorlessEpochs =
                    configuration.Profile.UnlockedColorlessEpochs;
                if ((combatColorlessEpochs is null) !=
                        (configuredColorlessEpochs is null) ||
                    (combatColorlessEpochs is not null &&
                     (combatColorlessEpochs.Distinct(StringComparer.Ordinal)
                        .Count() != combatColorlessEpochs.Length ||
                      !combatColorlessEpochs.ToHashSet(StringComparer.Ordinal)
                        .SetEquals(configuredColorlessEpochs!))))
                    throw new InvalidDataException(
                        "Combat Colorless epoch unlocks differ from run configuration.");
            }
            if (configuration.CharacterId != "silent")
                throw new NotSupportedException("Initialized v111 character mechanics are currently limited to Silent.");
            if (initialized.Act > 1)
            {
                if (!configuration.UsesExperimentalSilentThreeActRoute)
                    throw new NotSupportedException(
                        "Initialized native Hive/Glory state requires an explicit experimental route opt-in.");
                if (initialized.ActOneRegion is not null
                    || initialized.Stochastic.ActOneEncounterPool is not null
                    || initialized.Stochastic.LaterActEncounterBag?.Act !=
                        initialized.Act
                    || (initialized.Map.GenerationProfileId !=
                        PrototypeNativeLaterActMap.ProfileId(initialized.Act)
                        && !(initialized.Map.GenerationProfileId == PrototypeGoldenPathMapV111.ProfileId
                            && PrototypeGoldenPathMapV111.IsActive(state.Player, initialized.Act))))
                    throw new InvalidDataException(
                        "Later-act route must own its native map profile without Act 1 encounter bags.");
            }
            else
            {
                var region = configuration.Acts[0] == ActIdentity.Overgrowth
                    ? PrototypeActOneRegion.Overgrowth : PrototypeActOneRegion.Underdocks;
                var mapProfileMatches = region == PrototypeActOneRegion.Overgrowth
                    ? PrototypeNativeOvergrowthMap.IsGenerationProfile(
                        initialized.Map.GenerationProfileId)
                    : initialized.Map.GenerationProfileId ==
                        PrototypeNativeUnderdocks.GenerationProfileId;
                if (initialized.ActOneRegion != region || !mapProfileMatches)
                    throw new InvalidDataException("Act region or map generation profile does not match configured v111 route.");
            }
        }
    }
}

public sealed record V111CharacterDefinition
{
    private string[]? _startingDeck;
    private string[]? _startingRelics;

    [JsonConstructor]
    public V111CharacterDefinition(
        string Id,
        int StartingHp,
        int StartingGold,
        int BaseEnergy,
        int OrbSlots,
        string[] StartingDeck,
        string[] StartingRelics)
    {
        this.Id = Id;
        this.StartingHp = StartingHp;
        this.StartingGold = StartingGold;
        this.BaseEnergy = BaseEnergy;
        this.OrbSlots = OrbSlots;
        this.StartingDeck = StartingDeck;
        this.StartingRelics = StartingRelics;
    }

    public string Id { get; init; }
    public int StartingHp { get; init; }
    public int StartingGold { get; init; }
    public int BaseEnergy { get; init; }
    public int OrbSlots { get; init; }
    public string[] StartingDeck
    {
        get => _startingDeck is null
            ? null! : (string[])_startingDeck.Clone();
        init => _startingDeck = Copy(value);
    }
    public string[] StartingRelics
    {
        get => _startingRelics is null
            ? null! : (string[])_startingRelics.Clone();
        init => _startingRelics = Copy(value);
    }

    public V111CharacterDefinition Fork() => new(
        Id, StartingHp, StartingGold, BaseEnergy, OrbSlots,
        _startingDeck!, _startingRelics!);

    public void Deconstruct(
        out string Id,
        out int StartingHp,
        out int StartingGold,
        out int BaseEnergy,
        out int OrbSlots,
        out string[] StartingDeck,
        out string[] StartingRelics)
    {
        Id = this.Id;
        StartingHp = this.StartingHp;
        StartingGold = this.StartingGold;
        BaseEnergy = this.BaseEnergy;
        OrbSlots = this.OrbSlots;
        StartingDeck = this.StartingDeck;
        StartingRelics = this.StartingRelics;
    }

    private static string[]? Copy(string[]? values) =>
        values is null ? null : (string[])values.Clone();
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
public sealed record CharacterResourceState
{
    private OrbState[]? _orbs;

    [System.Text.Json.Serialization.JsonConstructor]
    public CharacterResourceState(
        int Stars = 0,
        int Forge = 0,
        int OrbSlots = 0,
        OrbState[]? Orbs = null,
        SecondaryCreatureState? Osty = null)
    {
        this.Stars = Stars;
        this.Forge = Forge;
        this.OrbSlots = OrbSlots;
        this.Orbs = Orbs;
        this.Osty = Osty;
    }

    public int Stars { get; init; }
    public int Forge { get; init; }
    public int OrbSlots { get; init; }
    public OrbState[]? Orbs
    {
        get => _orbs is null ? null : (OrbState[])_orbs.Clone();
        init => _orbs = value is null ? null : (OrbState[])value.Clone();
    }
    public SecondaryCreatureState? Osty { get; init; }

    public CharacterResourceState Fork() =>
        new(Stars, Forge, OrbSlots, _orbs, Osty);

    public void Deconstruct(
        out int Stars,
        out int Forge,
        out int OrbSlots,
        out OrbState[]? Orbs,
        out SecondaryCreatureState? Osty)
    {
        Stars = this.Stars;
        Forge = this.Forge;
        OrbSlots = this.OrbSlots;
        Orbs = this.Orbs;
        Osty = this.Osty;
    }
}

public static class V111RunFactory
{
    private static readonly System.Threading.AsyncLocal<int>
        ControlledReferenceNatureRunStartDepth = new();

    private sealed class ControlledRunStartScope(int previousDepth) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            if (ControlledReferenceNatureRunStartDepth.Value !=
                previousDepth + 1)
                throw new InvalidOperationException(
                    "Controlled ReferenceNature run-start scopes must be disposed in stack order.");
            ControlledReferenceNatureRunStartDepth.Value = previousDepth;
            _disposed = true;
        }
    }

    internal static bool ControlledReferenceNatureRunStartActive =>
        ControlledReferenceNatureRunStartDepth.Value > 0;

    internal static IDisposable EnterControlledReferenceNatureRunStart()
    {
        var previousDepth = ControlledReferenceNatureRunStartDepth.Value;
        ControlledReferenceNatureRunStartDepth.Value =
            checked(previousDepth + 1);
        return new ControlledRunStartScope(previousDepth);
    }

    public static RunState Create(string seed, RunConfiguration? configuration = null)
    {
        var owned = (configuration ?? RunConfiguration.Create()).Fork();
        owned.Validate();
        var run = PrototypeGameFactory.Create(seed, owned.Ascension);
        return run with
        {
            GameBuild = V111Build.Id, EmulatorSchema = V111Build.StateSchema,
            RunId = $"v111-{owned.CharacterId}-{seed}", Configuration = owned,
            Stochastic = RunStochasticStateV111.WithNativeRunRng(
                run.Stochastic, NativeRngV111.CreateBundle(seed))
        };
    }

    public static RunState CreateWithReferenceNature(string seed,
        ulong natureSeed, RunConfiguration? configuration = null)
    {
        var run = Create(seed, configuration);
        return run with
        {
            Stochastic = RunStochasticStateV111.WithReferenceNature(
                run.Stochastic,
                ReferenceNatureStateV111.Create(natureSeed))
        };
    }

    internal static RunState Initialize(RunState input)
    {
        var config = input.Configuration ?? throw new InvalidDataException("Missing v111 configuration.");
        if (config.CharacterId != "silent")
            throw new NotSupportedException($"v111 '{config.CharacterId}' mechanics are not implemented; no Silent substitution is allowed.");
        if (!config.Profile.FullyUnlocked)
            throw new NotSupportedException("Restricted-profile acquisition/progression rules are not implemented; choose an explicit fully unlocked profile.");
        var controlledReferenceNatureRunStart =
            ControlledReferenceNatureRunStartActive
            && (config.Acts[0] is ActIdentity.Overgrowth or ActIdentity.Underdocks);
        var controlledReferenceNatureOvergrowthStart =
            controlledReferenceNatureRunStart
            && config.Acts[0] == ActIdentity.Overgrowth;
        RunState opening;
        var initialNatureState = controlledReferenceNatureRunStart
            ? null
            : input.Stochastic.ReferenceNature?.Fork();
        var runStartChanceLaws = new List<ChanceLawV111>();
        ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>?
            runStartStochastic = initialNatureState is null ? null : new(
                initialNatureState, initialNatureState.Fork(), []);
        ReferenceNatureMapGenerationAuditV111? actOneMapGeneration = null;
        if (config.Acts[0] == ActIdentity.Overgrowth)
        {
            if (controlledReferenceNatureOvergrowthStart)
            {
                opening = PrototypeNativeOvergrowthRunFactory
                    .CreateWithAudit(input.RunSeed, config.Ascension,
                        generateNativeMap: false).State;
            }
            else if (initialNatureState is { } overgrowthNatureState)
            {
                var result = PrototypeNativeOvergrowthRunFactory
                    .CreateWithReferenceNature(input.RunSeed,
                        config.Ascension, overgrowthNatureState);
                opening = result.Run;
                runStartChanceLaws.AddRange(result.ChanceLaws);
                runStartStochastic = runStartStochastic!.Append(
                    result.Stochastic);
                actOneMapGeneration = result.MapGenerationAudit?.Fork();
            }
            else
            {
                opening = PrototypeNativeOvergrowthRunFactory.Create(
                    input.RunSeed, config.Ascension);
            }
        }
        else if (initialNatureState is { } underdocksNatureState)
        {
            var result = PrototypeNativeUnderdocksRunFactory
                .CreateWithReferenceNature(input.RunSeed,
                    config.Ascension, underdocksNatureState);
            opening = result.Run;
            runStartChanceLaws.AddRange(result.ChanceLaws);
            runStartStochastic = runStartStochastic!.Append(
                result.Stochastic);
        }
        else
        {
            opening = PrototypeNativeUnderdocksRunFactory.Create(
                input.RunSeed, config.Ascension);
        }
        // Restlessness is a historical training adapter addition, not a native
        // A0 starter. Keep it only in the explicit legacy factories.
        var deck = config.Ascension < 5
            ? opening.Player.Deck.Where(card => card.CardId != "proto.common.restlessness").ToArray()
            : opening.Player.Deck;
        var nativeRunRng = input.Stochastic.NativeRunRng
            ?? NativeRngV111.CreateBundle(input.RunSeed);
        var natureState = opening.Stochastic.ReferenceNature
            ?? input.Stochastic.ReferenceNature;
        // Nature owns the persistent pool as an unordered identity set. Its
        // later pull continuations choose identities without committing the
        // Native queue permutation or advancing that Native-only stream.
        var natureOwnsRelicBag = natureState is not null
            || controlledReferenceNatureRunStart;
        var existingRelicBag = opening.World!.Stochastic.RelicBag;
        var relicBag = natureOwnsRelicBag
            ? PrototypeNativeRelicGrabBag.CreateNatureUnordered(
                opening.Player, existingRelicBag)
            : existingRelicBag ?? PrototypeNativeRelicGrabBag.CreateNative(
                opening.Player, nativeRunRng);
        PrototypeLaterActEncounterBagV111[]? preparedLaterActBags = null;
        if (config.UsesExperimentalSilentThreeActRoute)
        {
            if (!controlledReferenceNatureRunStart)
            {
                var prototypeRng = opening.Stochastic.PrototypeRng;
                var prepared = BuildPreparedLaterActBags(
                    prototypeRng, config, natureState);
                opening = opening with
                {
                    Stochastic = RunStochasticStateV111.WithPrototypeRng(
                        opening.Stochastic, prototypeRng)
                };
                preparedLaterActBags = prepared.Bags;
                if (prepared.Stochastic is { } preparedStochastic)
                {
                    natureState = preparedStochastic.NatureState;
                    runStartStochastic = runStartStochastic!.Append(
                        preparedStochastic);
                }
                runStartChanceLaws.AddRange(prepared.ChanceLaws);
            }
        }
        ReferenceNatureRunInitializationTranscriptV111?
            runStartTranscript = null;
        if (initialNatureState is not null)
        {
            var initialization = runStartStochastic
                ?? throw new InvalidDataException(
                    "ReferenceNature run initialization lost its stochastic owner.");
            natureState = initialization.NatureState;
            runStartTranscript =
                new ReferenceNatureRunInitializationTranscriptV111(
                    initialization.InitialNatureState,
                    initialization.NatureState,
                    runStartChanceLaws.ToArray(),
                    initialization.DecisionTranscript)
                {
                    ActOneMapGeneration = actOneMapGeneration,
                    LaterActPreparationInitialPrototypeRng =
                        config.UsesExperimentalSilentThreeActRoute
                            ? opening.Stochastic.PrototypeRng : null
                };
        }
        runStartTranscript?.Validate();
        var stochastic = RunStochasticStateV111.WithNativeRunRng(
            opening.Stochastic, nativeRunRng);
        stochastic = RunStochasticStateV111
            .CommitReferenceNatureRunInitialization(
                stochastic, natureState, runStartTranscript,
                controlledReferenceNatureRunStart &&
                config.UsesExperimentalSilentThreeActRoute);
        var openingWorld = opening.World!;
        var worldStochastic = RunWorldStochasticStateV111.WithRelicBag(
            openingWorld.Stochastic, relicBag);
        if (preparedLaterActBags is not null)
            worldStochastic = RunWorldStochasticStateV111
                .CommitPreparedLaterActEncounterBags(
                    openingWorld, worldStochastic, config.Ascension,
                    config.Profile.RevealedEpochs.Contains(
                        NativeDarvEventV111.EpochId, StringComparer.Ordinal),
                    preparedLaterActBags);
        return opening with
        {
            GameBuild = input.GameBuild, EmulatorSchema = input.EmulatorSchema,
            RunId = input.RunId, DecisionIndex = input.DecisionIndex,
            Configuration = config.Fork(), Player = opening.Player with { Deck = deck },
            Stochastic = stochastic,
            World = opening.World! with
            {
                RulesetId = config.RulesetId,
                ActIdentity = config.Acts[0],
                Stochastic = worldStochastic
            }
        };
    }

    private static (PrototypeLaterActEncounterBagV111[] Bags,
        ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>?
            Stochastic, ChanceLawV111[] ChanceLaws)
        BuildPreparedLaterActBags(
        RngBundle rng, RunConfiguration configuration,
        ReferenceNatureStateV111? natureState)
    {
        var chanceLaws = new List<ChanceLawV111>();
        ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>?
            stochastic = natureState is null ? null : new(
                natureState, natureState.Fork(), []);
        string[] assigned;
        if (natureState is null)
        {
            assigned = ExperimentalSilentThreeActRouteV111
                .PrepareAncientAssignments(rng, configuration.Ascension,
                    configuration.Profile);
        }
        else
        {
            var result = ExperimentalSilentThreeActRouteV111
                .PrepareAncientAssignmentsWithReferenceNature(
                    rng, configuration.Ascension, configuration.Profile,
                    natureState);
            assigned = result.Assignments;
            natureState = result.NatureState;
            chanceLaws.AddRange(result.ChanceLaws);
            stochastic = stochastic!.Append(result.Stochastic);
        }
        PrototypeLaterActEncounterBagV111[] bags;
        if (natureState is null)
        {
            bags = new[] { 2, 3 }.Select((act, index) =>
                ExperimentalSilentThreeActRouteV111.InitialBag(
                    act, rng, configuration.Ascension,
                    configuration.Profile, assignAncient: false) with
                {
                    AssignedAncientId = assigned[index]
                }).ToArray();
        }
        else
        {
            var initialNatureState = natureState;
            var initialPrototypeRng = rng.Fork();
            var provider = new ReferenceNatureLaterActEncounterIndexProviderV111(
                rng, initialNatureState, configuration.Ascension);
            using (StochasticProviderScopeV111.Enter(
                       indexProvider: provider,
                       overrideExplicitProviders: true))
            {
                bags = new[] { 2, 3 }.Select((act, index) =>
                    ExperimentalSilentThreeActRouteV111.InitialBag(
                        act, rng, configuration.Ascension,
                        configuration.Profile, assignAncient: false) with
                    {
                        AssignedAncientId = assigned[index]
                    }).ToArray();
            }
            var expectedQueueDrawCount =
                ExperimentalSilentThreeActRouteV111.OrdinaryRoomCount(2) + 15 +
                ExperimentalSilentThreeActRouteV111.OrdinaryRoomCount(3) + 15;
            if (provider.QueueDrawCount != expectedQueueDrawCount ||
                CanonicalJson.Sha256(initialPrototypeRng) !=
                    CanonicalJson.Sha256(rng))
                throw new InvalidDataException(
                    "ReferenceNature later-act bag preparation advanced the legacy prototype RNG.");
            var result = new ReferenceNatureLaterActEncounterPreparationV111(
                bags, initialPrototypeRng, initialNatureState,
                provider.NatureState,
                provider.InitialCombatOrdinal, configuration.Ascension,
                provider.ChanceLaws, provider.DecisionTranscript);
            result.Validate(configuration.Profile);
            bags = result.Bags;
            natureState = result.NatureState;
            chanceLaws.AddRange(result.ChanceLaws);
            stochastic = stochastic!.Append(result.Stochastic);
        }
        return (bags, stochastic, chanceLaws.ToArray());
    }
}
