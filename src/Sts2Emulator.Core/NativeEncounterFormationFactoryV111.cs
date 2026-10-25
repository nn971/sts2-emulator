namespace Sts2Emulator.Core;

/// <summary>
/// A generated monster's native creature-local RNG context. The combat id is
/// supplied by the owning CombatState, which includes the player creature and
/// all earlier spawns; formation position is not the native combat id.
/// </summary>
public sealed record NativeEncounterMonsterContextV111(
    long CombatId,
    PrototypeEncounterEnemySpec Enemy,
    RngStreamState Rng)
{
    public NativeEncounterMonsterContextV111 Fork() =>
        this with { Enemy = Enemy with { }, Rng = Rng.Fork() };
}

/// <summary>
/// A complete hand-off from encounter formation to combat creation. The
/// encounter RNG is retained *after* generating the formation so encounter
/// hooks can continue its sequence. No run, player, prototype combat, or
/// monster-specific stream has been consumed by formation.
/// </summary>
public sealed record NativeEncounterFormationV111(
    string NativeEncounterEntry,
    int TotalFloor,
    PrototypeEncounterEnemySpec[] Enemies,
    RngStreamState EncounterRng,
    NativeEncounterMonsterContextV111[] MonsterContexts,
    // Encounter-specific initial AI state rotations are sampled during
    // GenerateMonsters(), not by the combat's run.monster_ai stream.
    int? EncounterOpeningOffset = null,
    // Immutable provenance for native mid-combat creature creation.
    // Older serialized fixtures can omit these nullable fields.
    string? RunSeed = null,
    int? NativeActIndex = null,
    int? MapColumn = null,
    int? MapRow = null)
{
    private PrototypeEncounterEnemySpec[] _enemies = Copy(Enemies);
    private NativeEncounterMonsterContextV111[] _monsterContexts =
        CloneContexts(MonsterContexts);

    public PrototypeEncounterEnemySpec[] Enemies
    {
        get => Copy(_enemies);
        init => _enemies = Copy(value);
    }

    public NativeEncounterMonsterContextV111[] MonsterContexts
    {
        get => CloneContexts(_monsterContexts);
        init => _monsterContexts = CloneContexts(value);
    }

    public NativeEncounterFormationV111 Fork() => this with
    {
        Enemies = _enemies,
        EncounterRng = EncounterRng.Fork(),
        MonsterContexts = _monsterContexts
    };

    private static PrototypeEncounterEnemySpec[] Copy(
        PrototypeEncounterEnemySpec[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return (PrototypeEncounterEnemySpec[])values.Clone();
    }

    private static NativeEncounterMonsterContextV111[] CloneContexts(
        NativeEncounterMonsterContextV111[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Select(context => context.Fork()).ToArray();
    }
}

/// <summary>Formation output plus the typed native RNG requests made while
/// selecting its source-defined enemy indices.</summary>
public sealed record NativeEncounterFormationAuditV111(
    NativeEncounterFormationV111 Formation,
    RandomIndexDecisionV111[] Choices, string ProviderMode)
{
    private RandomIndexDecisionV111[] _choices = CloneChoices(Choices);
    private NativeInitialHpOutcomeV111[]? _initialHpOutcomes = [];

    public RandomIndexDecisionV111[] Choices
    {
        get => CloneChoices(_choices);
        init => _choices = CloneChoices(value);
    }

    public NativeInitialHpOutcomeV111[] InitialHpOutcomes
    {
        get => CloneInitialHpOutcomes(_initialHpOutcomes)!;
        init => _initialHpOutcomes = CloneInitialHpOutcomes(value);
    }

    public NativeEncounterFormationAuditV111 Fork() => this with
    {
        Formation = Formation.Fork(),
        Choices = _choices,
        InitialHpOutcomes = _initialHpOutcomes!
    };

    private static NativeInitialHpOutcomeV111[]?
        CloneInitialHpOutcomes(NativeInitialHpOutcomeV111[]? values) =>
        values?.Select(value => value is null ? null! : value.Fork())
            .ToArray();

    private static RandomIndexDecisionV111[] CloneChoices(
        RandomIndexDecisionV111[] choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        return choices.Select(choice => choice with
        {
            Request = choice.Request with
            {
                OrderedCandidates =
                    (int[])choice.Request.OrderedCandidates.Clone()
            }
        }).ToArray();
    }
}

/// <summary>
/// Opt-in native RNG boundary for source-shaped encounter formations.
/// Unlike the legacy ResolveEnemySpecs(RngBundle) path, this consumes
/// EncounterModel's independent RNG seeded from run seed, total floor and
/// the exact native EncounterModel.Id.Entry. Generates untouched initial
/// monster-local streams from their real combat ids. Monster HP, per-monster
/// AI draws, and exact source-specific GenerateMonsters call order are NOT
/// claimed or simulated here.
/// </summary>
public static class NativeEncounterFormationFactoryV111
{
    public const string FormationSiteId =
        "combat.encounter.formation.index_draw";

    public static NativeEncounterFormationV111 Generate(
        string seed,
        PrototypeEncounterDefinition encounter,
        string nativeEncounterEntry,
        int totalFloor,
        int currentActIndex,
        long firstMonsterCombatId,
        int? mapColumn = null,
        int? mapRow = null,
        Func<Func<int, int>,
            (PrototypeEncounterEnemySpec[] Enemies, int? OpeningOffset)>?
            sourceResolver = null) =>
        GenerateWithAudit(seed, encounter, nativeEncounterEntry,
            totalFloor, currentActIndex, firstMonsterCombatId,
            mapColumn, mapRow, sourceResolver).Formation;

    public static NativeEncounterFormationAuditV111 GenerateWithAudit(
        string seed,
        PrototypeEncounterDefinition encounter,
        string nativeEncounterEntry,
        int totalFloor,
        int currentActIndex,
        long firstMonsterCombatId,
        int? mapColumn = null,
        int? mapRow = null,
        Func<Func<int, int>,
            (PrototypeEncounterEnemySpec[] Enemies, int? OpeningOffset)>?
            sourceResolver = null,
        IRandomIndexProviderV111? indexProvider = null)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEncounterEntry);
        if (currentActIndex is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(currentActIndex));
        if (firstMonsterCombatId < 0)
            throw new ArgumentOutOfRangeException(nameof(firstMonsterCombatId));
        if (mapColumn is < 0 or > 6)
            throw new ArgumentOutOfRangeException(nameof(mapColumn));
        if (mapRow is < 0)
            throw new ArgumentOutOfRangeException(nameof(mapRow));

        // The native encounter RNG is not part of the persistent 12 run
        // streams, and is not the native run.monster_ai stream either.
        var ownedStream = NativeRngV111Contexts.ForEncounterFormation(
            seed, nativeEncounterEntry, totalFloor);
        var ownedBundle = new RngBundle([ownedStream]);
        var provider = indexProvider ?? new NativeRandomIndexProviderV111();
        var choices = new List<RandomIndexDecisionV111>();
        var ordinal = 0;
        Func<int, int> nextIndex = maxExclusive =>
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            var request = new RandomIndexRequestV111(
                FormationSiteId, ownedStream.StreamId,
                $"encounter:{nativeEncounterEntry}:floor:{totalFloor}",
                ordinal, Enumerable.Range(0, maxExclusive).ToArray());
            var candidateLabels = Enumerable.Range(0, maxExclusive)
                .Select(index => index.ToString(
                    System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
            using var eventLabels =
                ControlledEventChoiceReplayV111.DescribeCandidates(
                    candidateLabels);
            var decision = StochasticProviderScopeV111.ResolveIndex(
                provider, request, ownedBundle);
            choices.Add(decision);
            ordinal++;
            return decision.SelectedIndex;
        };
        var (formation, openingOffset) = sourceResolver is null
            ? (encounter.ResolveEnemySpecs(nextIndex), (int?)null)
            : sourceResolver(nextIndex);
        if (formation is null || openingOffset is < 0)
            throw new InvalidOperationException(
                "Native encounter source resolver returned an invalid formation or opener.");

        if (formation.Length > long.MaxValue - firstMonsterCombatId)
            throw new ArgumentOutOfRangeException(nameof(firstMonsterCombatId));

        var monsters = formation.Select((enemy, index) =>
        {
            var combatId = firstMonsterCombatId + index;
            return new NativeEncounterMonsterContextV111(
                combatId,
                enemy with { },
                NativeRngV111Contexts.ForMonster(
                    seed, currentActIndex, combatId, mapColumn, mapRow));
        }).ToArray();

        var result = new NativeEncounterFormationV111(
            nativeEncounterEntry, totalFloor,
            formation, ownedBundle.GetStream(ownedStream.StreamId).Fork(),
            monsters,
            openingOffset, seed, currentActIndex, mapColumn, mapRow);
        return new NativeEncounterFormationAuditV111(
            result, choices.ToArray(), provider.Mode);
    }

    public static ReferenceNatureNativeEncounterFormationResultV111
        GenerateWithReferenceNature(
            string seed, PrototypeEncounterDefinition encounter,
            string nativeEncounterEntry, int totalFloor, int currentActIndex,
            long firstMonsterCombatId,
            ReferenceNatureStateV111 initialNatureState,
            int? mapColumn = null, int? mapRow = null,
            Func<Func<int, int>,
                (PrototypeEncounterEnemySpec[] Enemies,
                    int? OpeningOffset)>? sourceResolver = null)
    {
        ArgumentNullException.ThrowIfNull(initialNatureState);
        initialNatureState.Validate();
        var provider = new ReferenceNatureEncounterFormationIndexProviderV111(
            nativeEncounterEntry, totalFloor, initialNatureState);
        var audit = GenerateWithAudit(
            seed, encounter, nativeEncounterEntry, totalFloor,
            currentActIndex, firstMonsterCombatId, mapColumn, mapRow,
            sourceResolver, provider);
        var result = new ReferenceNatureNativeEncounterFormationResultV111(
            audit, initialNatureState.Fork(), provider.NatureState,
            provider.ChanceLaws, provider.DecisionTranscript);
        result.Validate();
        return result;
    }
}

/// <summary>
/// ReferenceNature resolves only the source-shaped encounter-local index
/// calls. The caller-provided source resolver still owns their order and
/// dynamic supports. The Native local stream is retained at its initial
/// cursor; this counterfactual path does not advance it for Nature outcomes.
/// </summary>
internal sealed class ReferenceNatureEncounterFormationIndexProviderV111
    : IRandomIndexProviderV111
{
    private readonly string _encounterEntry;
    private readonly int _totalFloor;
    private readonly List<ChanceLawV111> _chanceLaws = [];
    private ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        _stochastic;

    public ReferenceNatureEncounterFormationIndexProviderV111(
        string encounterEntry, int totalFloor,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterEntry);
        if (totalFloor < 0)
            throw new ArgumentOutOfRangeException(nameof(totalFloor));
        ArgumentNullException.ThrowIfNull(initialNatureState);
        initialNatureState.Validate();
        _encounterEntry = encounterEntry;
        _totalFloor = totalFloor;
        _stochastic = new(initialNatureState.Fork(),
            initialNatureState.Fork(), []);
    }

    public string Mode => ReferenceNatureV111.ProviderId;
    public ReferenceNatureStateV111 NatureState =>
        _stochastic.NatureState.Fork();
    public ChanceLawV111[] ChanceLaws => _chanceLaws
        .Select(law => law.Fork()).ToArray();
    public ReferenceNatureDecisionV111[] DecisionTranscript =>
        _stochastic.DecisionTranscript;

    public RandomIndexDecisionV111 Resolve(
        RandomIndexRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.ValidateForNativeProvider();
        var expectedStream = NativeRngV111Contexts
            .EncounterFormationStreamId(_encounterEntry, _totalFloor);
        var expectedOwner =
            $"encounter:{_encounterEntry}:floor:{_totalFloor}";
        if (request.SiteId != NativeEncounterFormationFactoryV111
                .FormationSiteId ||
            request.StreamId != expectedStream ||
            request.OwnerId != expectedOwner ||
            request.OperationKind != "choose_index" ||
            request.Ordinal != _stochastic.DecisionTranscript.Length ||
            request.SupportCount < 1 ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, request.SupportCount)))
            throw new InvalidDataException(
                "Native encounter formation request differs from its source-local boundary.");
        if (streams.GetStream(request.StreamId).Codec != NativeRngV111.Codec)
            throw new InvalidDataException(
                "Native encounter formation request differs from its source-local boundary.");

        var law = ChanceLawEngineV111.QueryNativeEncounterFormationIndex(
            _encounterEntry, _totalFloor, request.SupportCount,
            request.Ordinal);
        if (law.SiteId != request.SiteId ||
            law.OperationKind != request.OperationKind ||
            law.OwnerId != request.OwnerId || law.Ordinal != request.Ordinal)
            throw new InvalidDataException(
                "Native encounter formation Chance Law changed its typed request.");
        var selection = ReferenceNatureV111.SampleValidated(
            law, _stochastic.NatureState);
        _chanceLaws.Add(law.Fork());
        _stochastic = _stochastic.Advance(selection.NextState,
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex));
        var result = new RandomIndexDecisionV111(
            request with
            {
                OrderedCandidates = (int[])request.OrderedCandidates.Clone()
            }, selection.OutcomeIndex, Mode);
        result.Validate();
        return result;
    }
}

public sealed record ReferenceNatureNativeEncounterFormationResultV111(
    NativeEncounterFormationAuditV111 Audit,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ChanceLawV111[] ChanceLaws,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    private ChanceLawV111[]? _chanceLaws = ChanceLaws?.Select(law =>
        law.Fork()).ToArray();

    [System.Text.Json.Serialization.JsonIgnore]
    public ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        Stochastic { get; init; } = new(InitialNatureState, NatureState,
            DecisionTranscript);

    public ReferenceNatureStateV111 InitialNatureState
    {
        get => Stochastic.InitialNatureState;
        init => Stochastic = Stochastic with { InitialNatureState = value };
    }

    public ReferenceNatureStateV111 NatureState
    {
        get => Stochastic.NatureState;
        init => Stochastic = Stochastic with { NatureState = value };
    }

    public ChanceLawV111[] ChanceLaws
    {
        get => _chanceLaws?.Select(law => law.Fork()).ToArray()!;
        init => _chanceLaws = value?.Select(law => law.Fork()).ToArray();
    }

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => Stochastic.DecisionTranscript;
        init => Stochastic = Stochastic with { DecisionTranscript = value };
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Audit);
        ArgumentNullException.ThrowIfNull(Audit.Formation);
        Stochastic.Validate();
        var stochastic = Stochastic;
        var transcript = stochastic.DecisionTranscript;
        if (Audit.ProviderMode != ReferenceNatureV111.ProviderId ||
            ChanceLaws is null || transcript is null ||
            Audit.Choices.Length != ChanceLaws.Length ||
            Audit.Choices.Length != transcript.Length)
            throw new InvalidDataException(
                "Invalid ReferenceNature encounter formation result.");
        var formation = Audit.Formation;
        var expectedStream = NativeRngV111Contexts
            .EncounterFormationStreamId(
                formation.NativeEncounterEntry, formation.TotalFloor);
        var expectedOwner =
            $"encounter:{formation.NativeEncounterEntry}:floor:{formation.TotalFloor}";
        if (string.IsNullOrWhiteSpace(formation.RunSeed) ||
            CanonicalJson.Sha256(formation.EncounterRng) !=
            CanonicalJson.Sha256(NativeRngV111Contexts.ForEncounterFormation(
                formation.RunSeed, formation.NativeEncounterEntry,
                formation.TotalFloor)))
            throw new InvalidDataException(
                "ReferenceNature formation must retain the initial Native encounter cursor.");
        var expectedState = stochastic.InitialNatureState.Fork();
        for (var index = 0; index < ChanceLaws.Length; index++)
        {
            var law = ChanceLaws[index];
            var choice = Audit.Choices[index];
            var decision = transcript[index];
            law.Validate();
            choice.Validate();
            decision.Validate();
            var request = choice.Request;
            if (request.SiteId != NativeEncounterFormationFactoryV111
                    .FormationSiteId ||
                request.OperationKind != "choose_index" ||
                request.Ordinal != index ||
                !request.OrderedCandidates.SequenceEqual(
                    Enumerable.Range(0, request.SupportCount)) ||
                request.SiteId != law.SiteId ||
                request.OperationKind != law.OperationKind ||
                request.OwnerId != law.OwnerId ||
                request.StreamId != expectedStream ||
                request.OwnerId != expectedOwner ||
                request.Ordinal != law.Ordinal ||
                request.SupportCount != law.Outcomes.Length ||
                choice.ProviderMode != ReferenceNatureV111.ProviderId)
                throw new InvalidDataException(
                    "ReferenceNature formation audit differs from its Chance Law request.");
            var expectedLaw = ChanceLawEngineV111
                .QueryNativeEncounterFormationIndex(
                    formation.NativeEncounterEntry, formation.TotalFloor,
                    request.SupportCount, request.Ordinal);
            if (CanonicalJson.Sha256(expectedLaw) !=
                CanonicalJson.Sha256(law))
                throw new InvalidDataException(
                    "ReferenceNature formation Chance Law differs from its source query.");
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedState);
            var expectedDecision = new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            if (CanonicalJson.Sha256(expectedDecision) !=
                CanonicalJson.Sha256(decision) ||
                choice.SelectedIndex != selection.OutcomeIndex)
                throw new InvalidDataException(
                    "ReferenceNature formation transcript does not replay.");
            expectedState = selection.NextState;
        }
        if (CanonicalJson.Sha256(expectedState) !=
            CanonicalJson.Sha256(stochastic.NatureState))
            throw new InvalidDataException(
                "ReferenceNature formation cursor differs from its transcript.");
    }
}
