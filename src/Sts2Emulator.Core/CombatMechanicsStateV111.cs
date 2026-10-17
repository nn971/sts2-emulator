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
