namespace Sts2Emulator.Core;

/// <summary>
/// Mechanical state for one combat. Native RNG and semantic deck state are
/// paired with this checkpoint through the combat simulation envelope.
/// </summary>
public sealed record CombatMechanicsStateV111(
    int Turn,
    int Energy,
    int PlayerBlock,
    CombatCardZonesV111 CardZones,
    EnemyCombatState[] Enemies,
    long NextCardInstanceId,
    CombatCardInstance[] Cards,
    PrototypePowerInstanceState[] PlayerPowers,
    long NextPowerApplicationOrder,
    CombatRelicState[]? Relics = null,
    CombatPotionState[]? Potions = null,
    PendingCombatChoiceState? PendingChoice = null,
    PrototypeCombatCounters? Counters = null,
    bool IsPlayerTurn = true,
    PrototypeAutomaticPipelineContinuationState? AutomaticPipelineContinuation = null,
    int Act = 1,
    int Ascension = 0,
    int ExtraCardRewardsEarned = 0,
    PrototypeToricShield[]? ToricShields = null,
    long[]? TransformedPersistentCardIds = null,
    bool BoundCardPlayedThisTurn = false,
    PrototypeKnowledgeDemonChoiceState? PendingKnowledgeDemonChoice = null,
    int KnowledgeDemonChoiceResolvedTurn = 0,
    PrototypeAutoPlayTargetRoll[]? AutoPlayTargetRolls = null,
    bool PaelsEyeExtraTurnPending = false,
    PrototypeCombatPetState[]? Pets = null,
    string? CurrentMapNodeId = null,
    int PotionExtraTurnsPending = 0,
    bool IsPlayerSideActive = true)
{
    private EnemyCombatState[] _enemies = CloneEnemies(Enemies);
    private CombatCardInstance[] _cards = CloneCards(Cards);
    private PrototypePowerInstanceState[] _playerPowers =
        ClonePlayerPowers(PlayerPowers);
    private CombatRelicState[]? _relics = CloneRelics(Relics);
    private CombatPotionState[]? _potions = ClonePotions(Potions);
    private PrototypeToricShield[]? _toricShields = Copy(ToricShields);
    private long[]? _transformedPersistentCardIds =
        Copy(TransformedPersistentCardIds);
    private PrototypeAutoPlayTargetRoll[]? _autoPlayTargetRolls =
        Copy(AutoPlayTargetRolls);
    private PrototypeCombatPetState[]? _pets = Copy(Pets);
    private PendingCombatChoiceState? _pendingChoice = PendingChoice?.Fork();
    private PrototypeAutomaticPipelineContinuationState?
        _automaticPipelineContinuation = AutomaticPipelineContinuation?.Fork();
    private PrototypeKnowledgeDemonChoiceState? _pendingKnowledgeDemonChoice =
        PendingKnowledgeDemonChoice?.Fork();
    private PrototypeCombatCounters? _counters = Counters?.Fork();
    private string[]? _configuredUnlockedColorlessEpochs;

    public bool? ConfiguredColorlessGenerationFullyUnlocked { get; init; }

    public string[]? ConfiguredUnlockedColorlessEpochs
    {
        get => Copy(_configuredUnlockedColorlessEpochs);
        init => _configuredUnlockedColorlessEpochs = Copy(value);
    }

    public EnemyCombatState[] Enemies
    {
        get => CloneEnemies(_enemies);
        init => _enemies = CloneEnemies(value);
    }

    public CombatCardInstance[] Cards
    {
        get => CloneCards(_cards);
        init => _cards = CloneCards(value);
    }

    public PrototypePowerInstanceState[] PlayerPowers
    {
        get => ClonePlayerPowers(_playerPowers);
        init => _playerPowers = ClonePlayerPowers(value);
    }

    public CombatRelicState[]? Relics
    {
        get => CloneRelics(_relics);
        init => _relics = CloneRelics(value);
    }

    public CombatPotionState[]? Potions
    {
        get => ClonePotions(_potions);
        init => _potions = ClonePotions(value);
    }

    public PendingCombatChoiceState? PendingChoice
    {
        get => _pendingChoice?.Fork();
        init => _pendingChoice = value?.Fork();
    }

    public PrototypeAutomaticPipelineContinuationState?
        AutomaticPipelineContinuation
    {
        get => _automaticPipelineContinuation?.Fork();
        init => _automaticPipelineContinuation = value?.Fork();
    }

    public PrototypeKnowledgeDemonChoiceState?
        PendingKnowledgeDemonChoice
    {
        get => _pendingKnowledgeDemonChoice?.Fork();
        init => _pendingKnowledgeDemonChoice = value?.Fork();
    }

    public PrototypeCombatCounters? Counters
    {
        get => _counters?.Fork();
        init => _counters = value?.Fork();
    }

    public PrototypeToricShield[]? ToricShields
    {
        get => Copy(_toricShields);
        init => _toricShields = Copy(value);
    }

    public long[]? TransformedPersistentCardIds
    {
        get => Copy(_transformedPersistentCardIds);
        init => _transformedPersistentCardIds = Copy(value);
    }

    public PrototypeAutoPlayTargetRoll[]? AutoPlayTargetRolls
    {
        get => Copy(_autoPlayTargetRolls);
        init => _autoPlayTargetRolls = Copy(value);
    }

    public PrototypeCombatPetState[]? Pets
    {
        get => Copy(_pets);
        init => _pets = Copy(value);
    }

    public CombatMechanicsStateV111 Fork() => this with
    {
        CardZones = CardZones.Fork(),
        Enemies = _enemies,
        Cards = _cards,
        PlayerPowers = _playerPowers,
        Relics = _relics,
        Potions = _potions,
        PendingChoice = _pendingChoice,
        Counters = _counters,
        AutomaticPipelineContinuation = _automaticPipelineContinuation,
        ToricShields = _toricShields,
        TransformedPersistentCardIds = _transformedPersistentCardIds,
        PendingKnowledgeDemonChoice = _pendingKnowledgeDemonChoice,
        AutoPlayTargetRolls = _autoPlayTargetRolls,
        Pets = _pets,
        ConfiguredUnlockedColorlessEpochs = _configuredUnlockedColorlessEpochs
    };

    private static EnemyCombatState[] CloneEnemies(
        EnemyCombatState[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Select(enemy => enemy.Fork()).ToArray();
    }

    private static CombatCardInstance[] CloneCards(
        CombatCardInstance[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Select(card => card.Fork()).ToArray();
    }

    private static PrototypePowerInstanceState[] ClonePlayerPowers(
        PrototypePowerInstanceState[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Select(power => power.Fork()).ToArray();
    }

    private static CombatRelicState[]? CloneRelics(
        CombatRelicState[]? values) =>
        values?.Select(relic => relic.Fork()).ToArray();

    private static CombatPotionState[]? ClonePotions(
        CombatPotionState[]? values) =>
        values?.Select(potion => potion.Fork()).ToArray();

    private static T[]? Copy<T>(T[]? values) =>
        values is null ? null : (T[])values.Clone();

    // Compatibility projections used by the existing immutable mechanics
    // updates. The zone arrays themselves have one owner: CardZones.
    public long[] Hand
    {
        get => CardZones.Hand;
        init => CardZones = CardZones with { Hand = value };
    }

    public long[] DrawPile
    {
        get => CardZones.DrawPile;
        init => CardZones = CardZones with { DrawPile = value };
    }

    public long[] DiscardPile
    {
        get => CardZones.DiscardPile;
        init => CardZones = CardZones with { DiscardPile = value };
    }

    public long[] ExhaustPile
    {
        get => CardZones.ExhaustPile;
        init => CardZones = CardZones with { ExhaustPile = value };
    }

    public long[]? PlayPile
    {
        get => CardZones.PlayPile;
        init => CardZones = CardZones with { PlayPile = value };
    }

    public long[]? ChoicePool
    {
        get => CardZones.ChoicePool;
        init => CardZones = CardZones with { ChoicePool = value };
    }
}
