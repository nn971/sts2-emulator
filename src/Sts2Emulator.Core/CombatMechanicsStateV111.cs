namespace Sts2Emulator.Core;

/// <summary>
/// Mechanical state for one combat. Native RNG and semantic deck state are
/// paired with this checkpoint through the combat simulation envelope.
/// </summary>
public sealed record CombatMechanicsStateV111(
    int Turn,
    int Energy,
    int PlayerBlock,
    long[] Hand,
    long[] DrawPile,
    long[] DiscardPile,
    long[] ExhaustPile,
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
    long[]? ChoicePool = null,
    long[]? PlayPile = null,
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
    bool IsPlayerSideActive = true);
