using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record PrototypeAiAction(
    string ActionId,
    string Kind,
    JsonElement Payload);

public sealed record PrototypeAiCard(
    long InstanceId,
    string CardId,
    int UpgradeLevel,
    bool IsTemporary,
    JsonElement State,
    // The native UI reveals an affliction on each visible card.
    // Never expose draw-pile order through this field.
    PrototypeCardAffliction? Affliction = null);

public sealed record PrototypeAiRelic(
    string RelicId,
    JsonElement State);

public sealed record PrototypeAiPotion(
    int Slot,
    string PotionId,
    JsonElement State);

public sealed record PrototypeAiPower(
    string PowerId,
    int Stacks);

public sealed record PrototypeAiEnemy(
    int InstanceId,
    string EnemyId,
    int Hp,
    int Block,
    string? MoveId,
    IReadOnlyDictionary<string, int> Statuses,
    PrototypeAiPower[] Powers,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? IntentDamage = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? IntentHits = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? IntentBaseDamage = null);

public sealed record PrototypeAiCombat(
    int Turn,
    int Energy,
    int PlayerBlock,
    PrototypeAiCard[] Hand,
    int DrawPileCount,
    PrototypeAiCard[] DiscardPile,
    PrototypeAiCard[] ExhaustPile,
    PrototypeAiCard[] PlayPile,
    PrototypeAiEnemy[] Enemies,
    string? PendingChoiceId,
    // Active player powers are visible on the combat HUD.
    PrototypeAiPower[]? PlayerPowers = null);

public sealed record PrototypeAiMapNode(
    string NodeId,
    int Act,
    int Floor,
    PrototypeRoomType RoomType,
    string[] NextNodeIds);

public sealed record PrototypeAiDeckChoice(
    string SourceRelicId,
    PrototypePersistentDeckChoiceKind Kind,
    int RemainingSelections,
    long[] CandidateCardInstanceIds);

public sealed record PrototypeAiRewardCardGroup(
    int GroupIndex,
    string[] CardOptions,
    bool[] OptionUpgradeFlags);

public sealed record PrototypeAiExtraRelicReward(
    int GroupIndex,
    string RelicId);

public sealed record PrototypeAiExtraGoldReward(int GroupIndex, int Amount);

public sealed record PrototypeAiReward(
    string[] CardOptions,
    string? PotionOption,
    string? RelicOption,
    bool CardResolved,
    bool PotionResolved,
    bool RelicResolved,
    int ExtraCardRewardGroupsRemaining = 0,
    string[]? RelicOptions = null,
    PrototypeAiDeckChoice? PendingDeckChoice = null,
    bool[]? CardOptionUpgradeFlags = null,
    int? GoldOption = null,
    bool GoldResolved = true,
    bool IndependentSelection = false,
    PrototypeAiRewardCardGroup[]? PendingCardGroups = null,
    PrototypeAiExtraRelicReward[]? PendingExtraRelicRewards = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    PrototypeAiExtraGoldReward[]? PendingExtraGoldRewards = null);

public sealed record PrototypeAiEventDeckChoice(
    string ChoiceId,
    PrototypePersistentDeckChoiceKind Kind,
    int RemainingSelections,
    long[] CandidateCardInstanceIds,
    bool UpgradeTransformedCards,
    string? SourceRelicId = null);

public sealed record PrototypeAiEventPotionReplacement(
    string ChoiceId,
    string PotionId,
    int[] CandidateSlots);

public sealed record PrototypeAiEventQueuedDeckChoice(
    PrototypePersistentDeckChoiceKind Kind,
    int RequestedSelections,
    string? SourceRelicId);

/// <summary>
/// Numbers printed on an event's current choice page. They are computed
/// solely from revealed event variables, never from future RNG draws.
/// Positive MaxHpDelta is a gain; negative is a sacrifice.
/// </summary>
public sealed record PrototypeAiEventChoiceValue(
    string ChoiceId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? GoldGain = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? GoldCost = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? HpLoss = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Heal = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? MaxHpDelta = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? GuaranteedCardId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? GuaranteedRelicId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? GuaranteedPotionId = null);

public sealed record PrototypeAiEvent(
    string EventId,
    string? ChosenChoiceId,
    PrototypeAiEventDeckChoice? PendingDeckChoice,
    PrototypeAiEventPotionReplacement? PendingPotionReplacement = null,
    string[]? QueuedPotionIds = null,
    PrototypeAiEventQueuedDeckChoice[]? QueuedDeckChoices = null,
    string[]? OfferedChoiceIds = null,
    // The current dish is player-visible; it changes only after the
    // previous dish's card/potion continuation has been resolved.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CurrentDishId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? CurrentDishNumber = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    PrototypeAiEventChoiceValue[]? VisibleChoiceValues = null);

public sealed record PrototypeAiShop(
    ShopOffer[] CardOffers,
    ShopOffer? PotionOffer,
    ShopOffer? RelicOffer,
    int RemovalPrice,
    bool RemovalUsed,
    ShopOffer[]? PotionOffers = null,
    ShopOffer[]? RelicOffers = null,
    PrototypeAiDeckChoice? PendingDeckChoice = null);

public sealed record PrototypeAiObservation(
    string RulesetId,
    string CharacterId,
    long DecisionIndex,
    RunPhase Phase,
    int? Act,
    int? Floor,
    PrototypeActOneRegion? ActOneRegion,
    string? ActOneBossEncounterId,
    int Hp,
    int MaxHp,
    int Gold,
    PrototypeAiCard[] Deck,
    PrototypeAiRelic[] Relics,
    PrototypeAiPotion[] Potions,
    PrototypeAiMapNode[] Map,
    string? CurrentMapNodeId,
    PrototypeAiCombat? Combat,
    PrototypeAiReward? Reward,
    PrototypeAiShop? Shop,
    string? EventId,
    string? TerminalOutcome,
    PrototypeAiEvent? Event = null,
    string? MapGenerationProfileId = null,
    PrototypeCompletedRoomRecord[]? CompletedRooms = null);

public sealed record PrototypeAiFrame(
    string SchemaId,
    PrototypeAiObservation Observation,
    PrototypeAiAction[] LegalActions,
    string ObservationHash,
    string CanonicalStateHash);

public sealed record PrototypeAiExpansion(
    PrototypeAiAction Action,
    RunState State,
    string CanonicalStateHash);

public sealed record PrototypeAiStepFrame(
    PrototypeAiAction Action,
    RunState State,
    PrototypeAiFrame Frame);

public sealed record PrototypeAiRolloutFrame(
    PrototypeAiAction Action,
    RunState State,
    PrototypeAiObservation Observation,
    PrototypeAiAction[] LegalActions);

/// <summary>
/// Stateless adapter for search/learning consumers. The canonical RunState remains the source of
/// truth; this layer exposes a player-facing observation plus stable semantic action IDs.
/// </summary>
public sealed class PrototypeAiEnvironment
{
    public const string SchemaId = "prototype-ai-v0";

    private readonly PrototypeGameEngine _engine = new();

    public RunState Reset(
        string seed,
        int ascension = 0) =>
        PrototypeGameFactory.Create(
            seed,
            ascension);

    public RunState Fork(RunState state) => state.Fork();

    public PrototypeAiFrame Observe(RunState state)
    {
        var observation = CreateObservation(state);
        var legalActions = _engine.GetLegalActions(state)
            .Select(CreateAction)
            .ToArray();

        if (legalActions.Select(action => action.ActionId).Distinct(StringComparer.Ordinal).Count()
            != legalActions.Length)
        {
            throw new InvalidOperationException("AI action IDs are not unique in the current state.");
        }

        return new PrototypeAiFrame(
            SchemaId,
            observation,
            legalActions,
            CanonicalJson.Sha256(observation),
            CanonicalJson.Sha256(state));
    }

    public PrototypeAiExpansion[] Expand(RunState state)
    {
        return _engine.GetLegalActions(state)
            .Select(action =>
            {
                var view = CreateAction(action);
                var next = _engine.Step(state, action).State;
                return new PrototypeAiExpansion(
                    view,
                    next,
                    CanonicalJson.Sha256(next));
            })
            .ToArray();
    }

    public TransitionResult Step(RunState state, string actionId)
    {
        var resolved = ResolveAction(state, actionId);
        return _engine.Step(state, resolved.Action);
    }

    public PrototypeAiStepFrame StepFrame(RunState state, string actionId)
    {
        var resolved = ResolveAction(state, actionId);
        var next = _engine.Step(state, resolved.Action).State;
        return new PrototypeAiStepFrame(
            resolved.View,
            next,
            Observe(next));
    }

    public PrototypeAiRolloutFrame RolloutStepFrame(RunState state, string actionId)
    {
        var resolved = ResolveAction(state, actionId);
        var next = _engine.Step(state, resolved.Action).State;
        var observation = CreateObservation(next);
        var legalActions = _engine.GetLegalActions(next)
            .Select(CreateAction)
            .ToArray();

        if (legalActions.Select(action => action.ActionId).Distinct(StringComparer.Ordinal).Count()
            != legalActions.Length)
        {
            throw new InvalidOperationException("AI action IDs are not unique in the next state.");
        }

        return new PrototypeAiRolloutFrame(
            resolved.View,
            next,
            observation,
            legalActions);
    }

    public static string StableActionId(GameAction action)
    {
        var payloadHash = CanonicalJson.Sha256(action.Payload);
        return $"{action.Kind}:{payloadHash}";
    }

    private (GameAction Action, PrototypeAiAction View) ResolveAction(
        RunState state,
        string actionId)
    {
        var candidates = _engine.GetLegalActions(state)
            .Select(action => (Action: action, View: CreateAction(action)))
            .Where(item => StringComparer.Ordinal.Equals(item.View.ActionId, actionId))
            .ToArray();

        return candidates.Length switch
        {
            1 => candidates[0],
            0 => throw new InvalidOperationException(
                $"Action ID '{actionId}' is not legal in the current state."),
            _ => throw new InvalidOperationException(
                $"Action ID '{actionId}' is ambiguous in the current state.")
        };
    }

    private static PrototypeAiAction CreateAction(GameAction action) =>
        new(
            StableActionId(action),
            action.Kind,
            action.Payload.Clone());

    private static PrototypeAiObservation CreateObservation(RunState state)
    {
        var world = state.World;
        return new PrototypeAiObservation(
            RulesetId: world?.RulesetId ?? state.Configuration?.RulesetId ?? PrototypeContent.RulesetId,
            CharacterId: world?.CharacterId ?? state.Configuration?.CharacterId ?? PrototypeContent.CharacterId,
            DecisionIndex: state.DecisionIndex,
            Phase: state.Phase,
            Act: world?.Act,
            Floor: world?.Floor,
            ActOneRegion: world?.ActOneRegion,
            ActOneBossEncounterId:
                world?.ActOneEncounterPool?.BossEncounterId,
            Hp: state.Player.Hp,
            MaxHp: state.Player.MaxHp,
            Gold: state.Player.Gold,
            Deck: state.Player.Deck.Select(card =>
                new PrototypeAiCard(
                    card.InstanceId,
                    card.CardId,
                    card.UpgradeLevel,
                    IsTemporary: false,
                    card.PersistentState.Clone())).ToArray(),
            Relics: state.Player.Relics.Select(relic =>
                new PrototypeAiRelic(
                    relic.RelicId,
                    relic.PersistentState.Clone())).ToArray(),
            Potions: state.Player.PotionSlots
                .Select((potion, slot) => potion is null
                    ? null
                    : new PrototypeAiPotion(
                        slot,
                        potion.PotionId,
                        potion.PersistentState.Clone()))
                .Where(potion => potion is not null)
                .Select(potion => potion!)
                .ToArray(),
            Map: world is null
                ? Array.Empty<PrototypeAiMapNode>()
                : world.Map.Nodes.Select(node =>
                    new PrototypeAiMapNode(
                        node.NodeId,
                        node.Act,
                        node.Floor,
                        node.RoomType,
                        node.NextNodeIds is null
                            ? Array.Empty<string>()
                            : (string[])node.NextNodeIds.Clone()))
                    .ToArray(),
            CurrentMapNodeId: world?.Map.CurrentNodeId,
            Combat: world?.Combat is null
                ? null
                : CreateCombatObservation(
                    world.Combat,
                    world.Act,
                    state.Ascension,
                    state.Player.Relics.Any(relic =>
                        PrototypeContent.Relic(
                            relic.RelicId)
                            .HideEnemyIntents)),
            Reward: world?.Reward is null
                ? null
                : new PrototypeAiReward(
                    (string[])world.Reward.CurrentCardOptions.Clone(),
                    world.Reward.PotionOption,
                    world.Reward.RelicOption,
                    world.Reward.CardResolved,
                    world.Reward.PotionResolved,
                    world.Reward.RelicResolved,
                    Math.Max(
                        0,
                        (world.Reward.ExtraCardOptions?.Length ?? 0)
                        - world.Reward.ExtraCardRewardsResolved),
                    (string[])world.Reward.CurrentRelicOptions.Clone(),
                    world.Reward.PendingDeckChoice is null
                        ? null
                        : new PrototypeAiDeckChoice(
                            world.Reward.PendingDeckChoice.SourceRelicId,
                            world.Reward.PendingDeckChoice.Kind,
                            world.Reward.PendingDeckChoice.RemainingSelections,
                            (long[])world.Reward.PendingDeckChoice
                                .CandidateCardInstanceIds.Clone()),
                    (bool[])world.Reward.CurrentCardOptionUpgradeFlags.Clone(),
                    world.Reward.GoldOption,
                    world.Reward.GoldResolved,
                    world.Reward.IndependentSelection,
                    world.Reward.IndependentSelection
                        ? Enumerable.Range(0,
                                1 + (world.Reward.ExtraCardOptions?.Length ?? 0))
                            .Where(index => index == 0
                                ? !world.Reward.CardResolved
                                : !world.Reward.IsExtraCardGroupResolved(index - 1))
                            .Select(index => new PrototypeAiRewardCardGroup(
                                index,
                                (string[])world.Reward.CardOptionsForGroup(index).Clone(),
                                world.Reward.CardUpgradeFlagsForGroup(index)))
                            .ToArray()
                        : null,
                    world.Reward.IndependentSelection
                        ? Enumerable.Range(0,
                                world.Reward.ExtraRelicRewardIds?.Length ?? 0)
                            .Where(index => !world.Reward.IsExtraRelicGroupResolved(index))
                            .Select(index => new PrototypeAiExtraRelicReward(
                                index,
                                world.Reward.ExtraRelicRewardIds![index]))
                            .ToArray()
                        : null,
                    world.Reward.ExtraGoldOptions is { } extraGold
                        ? Enumerable.Range(0, extraGold.Length)
                            .Where(index => !world.Reward.ExtraGoldGroupsResolved![index])
                            .Select(index => new PrototypeAiExtraGoldReward(index, extraGold[index]))
                            .ToArray()
                        : null),
            Shop: world?.Shop is null
                ? null
                : new PrototypeAiShop(
                    (ShopOffer[])world.Shop.CardOffers.Clone(),
                    world.Shop.PotionOffer is null ? null : world.Shop.PotionOffer with { },
                    world.Shop.RelicOffer is null ? null : world.Shop.RelicOffer with { },
                    world.Shop.RemovalPrice,
                    world.Shop.RemovalUsed,
                    world.Shop.PotionOffers
                        .Select(offer => offer with { })
                        .ToArray(),
                    world.Shop.RelicOffers
                        .Select(offer => offer with { })
                        .ToArray(),
                    world.Shop.PendingDeckChoice is null
                        ? null
                        : new PrototypeAiDeckChoice(
                            world.Shop.PendingDeckChoice.SourceRelicId,
                            world.Shop.PendingDeckChoice.Kind,
                            world.Shop.PendingDeckChoice.RemainingSelections,
                            (long[])world.Shop.PendingDeckChoice
                                .CandidateCardInstanceIds.Clone())),
            EventId: world?.Event?.EventId,
            TerminalOutcome: world?.TerminalOutcome,
            Event: world?.Event is null
                ? null
                : new PrototypeAiEvent(
                    world.Event.EventId,
                    world.Event.ChosenChoiceId,
                    world.Event.PendingDeckChoice is null
                        ? null
                        : new PrototypeAiEventDeckChoice(
                            world.Event.PendingDeckChoice.ChoiceId,
                            world.Event.PendingDeckChoice.Kind,
                            world.Event.PendingDeckChoice.RemainingSelections,
                            (long[])world.Event.PendingDeckChoice
                                .CandidateCardInstanceIds.Clone(),
                            world.Event.PendingDeckChoice
                                .UpgradeTransformedCards,
                            world.Event.PendingDeckChoice
                                .SourceRelicId),
                    world.Event.PendingPotionReplacement is null
                        ? null
                        : new PrototypeAiEventPotionReplacement(
                            world.Event.PendingPotionReplacement.ChoiceId,
                            world.Event.PendingPotionReplacement.PotionId,
                            (int[])world.Event.PendingPotionReplacement
                                .CandidateSlots.Clone()),
                    (string[])world.Event.RemainingPotionIds.Clone(),
                    world.Event.RemainingDeckChoices
                        .Select(request => new PrototypeAiEventQueuedDeckChoice(
                            request.Kind,
                            request.RemainingSelections,
                            request.SourceRelicId))
                        .ToArray(),
                    world.Event.OfferedChoiceIds is null
                        ? null
                        : (string[])world.Event.OfferedChoiceIds.Clone(),
                    world.Event.EventId == PrototypeNativeEndlessConveyor.EventId
                        ? world.Event.NativeDishId
                        : null,
                    world.Event.EventId == PrototypeNativeEndlessConveyor.EventId
                        ? world.Event.NativeDishCount
                        : null,
                    VisibleEventChoiceValues(world.Event, state.Player)),
            MapGenerationProfileId:
                world?.Map.GenerationProfileId,
            CompletedRooms: world is null
                ? Array.Empty<PrototypeCompletedRoomRecord>()
                : (PrototypeCompletedRoomRecord[])world.CompletedRooms.Clone());
    }

    /// <summary>
    /// Only values already displayed by the pinned event page are exposed.
    /// In particular, Punch Off's unused entry-time gold roll and future
    /// dish outcomes remain private, even though they are in exact state.
    /// </summary>
    private static PrototypeAiEventChoiceValue[]? VisibleEventChoiceValues(
        EventState evt, PlayerState player)
    {
        // While a selected action is suspended inside a deck/potion prompt,
        // the event's main-page options are not actionable.
        if (evt.PendingDeckChoice is not null
            || evt.PendingPotionReplacement is not null
            || evt.RemainingDeckChoices.Length > 0
            || evt.RemainingPotionIds.Length > 0)
        {
            return null;
        }

        return evt.EventId switch
        {
            "proto.native.underdocks.sunken_treasury" =>
            [
                new("first_chest", GoldGain: evt.NativeEventGold),
                new("second_chest", GoldGain: evt.NativeEventSecondaryGold,
                    GuaranteedCardId: "proto.native.underdocks.greed")
            ],
            "proto.native.underdocks.sunken_statue"
                or "proto.native.event.sunken_statue" =>
            [
                new("dive", GoldGain: evt.NativeEventGold, HpLoss: 7),
                new(evt.EventId == "proto.native.underdocks.sunken_statue"
                        ? "sword" : "grab",
                    GuaranteedRelicId: "proto.native.event.sword_of_stone")
            ],
            "proto.native.event.whispering_hollow" =>
            [
                new("gold", GoldCost: evt.NativeEventGold),
                new("hug", HpLoss: 9)
            ],
            "proto.native.event.luminous_choir" =>
            [
                new("tribute", GoldCost: evt.NativeEventGold),
                new("reach", GuaranteedCardId:
                    "proto.native.event.spore_mind")
            ],
            "proto.native.event.jungle_maze_adventure" =>
            [
                new("solo", GoldGain: evt.NativeEventGold, HpLoss: 18),
                new("join", GoldGain: evt.NativeEventSecondaryGold)
            ],
            PrototypeNativeDenseVegetation.EventId =>
            [
                new("trudge", GoldGain: evt.NativeEventGold, HpLoss: 8),
                new("rest", Heal: Math.Min(
                    player.MaxHp - player.Hp,
                    Math.Max(1, player.MaxHp *
                        PrototypeContent.Rules.RestHealPercent / 100)))
            ],
            "proto.native.event.byrdonis_nest" =>
            [
                new("eat", MaxHpDelta: 7),
                new("take", GuaranteedCardId:
                    "proto.native.event.byrdonis_egg")
            ],
            "proto.native.event.morphic_grove" =>
            [
                new("group", GoldCost: player.Gold),
                new("loner", MaxHpDelta: 5)
            ],
            "proto.native.event.sapphire_seed" =>
            [
                new("eat", Heal: 9)
            ],
            "proto.native.event.unrest_site" =>
            [
                new("rest", Heal: player.MaxHp - player.Hp,
                    GuaranteedCardId: "proto.native.event.poor_sleep"),
                new("kill", MaxHpDelta: -8)
            ],
            "proto.native.event.wellspring" =>
            [
                new("bathe", GuaranteedCardId:
                    "proto.native.event.guilty")
            ],
            "proto.native.event.wood_carvings" =>
            [
                new("bird", GuaranteedCardId:
                    "proto.native.event.peck"),
                new("torus", GuaranteedCardId:
                    "proto.native.event.toric_toughness")
            ],
            "proto.native.underdocks.abyssal_baths" =>
                evt.NativePageIndex == 0
                    ?
                    [
                        new("immerse", HpLoss: 3, MaxHpDelta: 2),
                        new("abstain", Heal: 10)
                    ]
                    :
                    [
                        new("linger", HpLoss: checked(3 + evt.NativePageIndex),
                            MaxHpDelta: 2)
                    ],
            "proto.native.underdocks.spiraling_whirlpool" =>
            [
                new("drink", Heal: (player.MaxHp * 33) / 100)
            ],
            "proto.native.underdocks.trash_heap" =>
            [
                new("dive", HpLoss: 8),
                new("grab", GoldGain: 100)
            ],
            "proto.native.underdocks.waterlogged_scriptorium" =>
            [
                new("bloody_ink", MaxHpDelta: 6),
                new("tentacle_quill", GoldCost: 55),
                new("prickly_sponge", GoldCost: 99)
            ],
            "proto.native.underdocks.drowning_beacon" =>
            [
                new("climb", MaxHpDelta: -13,
                    GuaranteedRelicId:
                        "proto.native.underdocks.fresnel_lens"),
                new("bottle", GuaranteedPotionId:
                    "proto.native.underdocks.glowwater_potion")
            ],
            PrototypeNativePunchOff.EventId =>
            [
                new("nab", GuaranteedCardId: "proto.native.neow.injury")
            ],
            PrototypeNativeEndlessConveyor.EventId =>
                evt.NativeDishId == "GOLDEN_FYSH"
                    ?
                    [
                        new("grab", GoldGain: 75)
                    ]
                    :
                    [
                        new("grab", GoldCost: 40,
                            Heal: evt.NativeDishId == "CLAM_ROLL" ? 10 : null,
                            MaxHpDelta: evt.NativeDishId == "CAVIAR" ? 4 : null,
                            GuaranteedCardId: evt.NativeDishId == "SEAPUNK_SALAD"
                                ? PrototypeNativeEndlessConveyor.FeedingFrenzyId
                                : null)
                    ],
            _ => null
        };
    }

    // Only an action already stored by the combat engine can be public.
    // Never perform enemy AI selection from an observation: that would
    // consume hidden RNG and reveal a future rather than a chosen intent.
    private static PrototypeEnemyMoveDefinition? PlannedMove(
        EnemyCombatState enemy,
        bool hideEnemyIntents)
    {
        if (hideEnemyIntents
            || enemy.Hp <= 0
            || enemy.SkipNextEnemyAction
            || enemy.EnemyActionSkipsRemaining > 0
            || enemy.PlannedMoveIndex is not { } index)
        {
            return null;
        }

        var definition = PrototypeContent.Enemy(enemy.EnemyId);
        return index >= 0 && index < definition.Moves.Length
            ? definition.Moves[index]
            : null;
    }

    private static (int? BaseDamage, int? Damage, int? Hits) VisibleAttackIntent(
        CombatState combat,
        EnemyCombatState enemy,
        PrototypeEnemyMoveDefinition move,
        int act,
        int ascension)
    {
        int? perHitDamage = null;
        int? perHitBaseDamage = null;
        var hits = 0;
        var earlierSelfBuff = false;
        foreach (var effect in move.Effects)
        {
            if (effect.Kind != PrototypeEnemyEffectKind.DamagePlayer)
            {
                // A self-buff AFTER all attacks does not change the HUD
                // attack damage. A buff BEFORE a later attack might.
                earlierSelfBuff |=
                    effect.Kind == PrototypeEnemyEffectKind.ApplyEnemyPower;
                continue;
            }

            if (earlierSelfBuff)
            {
                return (null, null, null);
            }

            // The v1 interface represents a homogeneous attack as one
            // per-hit damage value and one hit count. Mixed attack/nonattack
            // damage and nonuniform attack amounts cannot fit this shape.
            if (!effect.IsAttack)
            {
                return (null, null, null);
            }

            var repetitions = effect.RepetitionsAt(ascension);
            if (repetitions <= 0)
            {
                continue;
            }

            // Base damage includes the public act/ascension scaling, but
            // no dynamic Strength/Weak/Vulnerable or damage caps.
            var priorUses = enemy.MoveUseCounts?.GetValueOrDefault(move.Id) ?? 0;
            var baseDamage = Math.Max(0, effect.UseStoredEnemyDamage
                ? enemy.StoredEnemyDamage
                : checked(effect.AmountAt(act, ascension)
                    + effect.ExtraAmountPerPriorMoveUse * priorUses));
            var damage = Math.Max(0,
                PrototypeGameEngine.EnemyHitDamage(
                    enemy, combat, effect, act, ascension, priorUses));
            if ((perHitDamage is not null && perHitDamage != damage)
                || (perHitBaseDamage is not null
                    && perHitBaseDamage != baseDamage))
            {
                return (null, null, null);
            }

            perHitBaseDamage = baseDamage;
            perHitDamage = damage;
            hits += repetitions;
        }

        return hits > 0
            && perHitDamage is not null
            && perHitBaseDamage is not null
            ? (perHitBaseDamage, perHitDamage, hits)
            : (null, null, null);
    }

    private static PrototypeAiCombat CreateCombatObservation(
        CombatState combat,
        int act,
        int ascension,
        bool hideEnemyIntents)
    {
        var byId = combat.Cards.ToDictionary(card => card.InstanceId);

        PrototypeAiCard Card(long instanceId)
        {
            var card = byId.TryGetValue(instanceId, out var value)
                ? value
                : throw new InvalidOperationException(
                    $"Combat observation references missing card {instanceId}.");

            return new PrototypeAiCard(
                card.InstanceId,
                card.CardId,
                card.UpgradeLevel,
                card.IsTemporary,
                card.State.Clone(),
                card.Affliction is null
                    ? null
                    : card.Affliction with { });
        }

        return new PrototypeAiCombat(
            Turn: combat.Turn,
            Energy: combat.Energy,
            PlayerBlock: combat.PlayerBlock,
            Hand: combat.Hand.Select(Card).ToArray(),
            DrawPileCount: combat.DrawPile.Length,
            DiscardPile: combat.DiscardPile.Select(Card).ToArray(),
            ExhaustPile: combat.ExhaustPile.Select(Card).ToArray(),
            PlayPile: combat.PlayCardIds.Select(Card).ToArray(),
            Enemies: combat.Enemies.Select(enemy =>
            {
                var move = PlannedMove(enemy, hideEnemyIntents);
                var threat = move is null
                    ? (BaseDamage: (int?)null,
                       Damage: (int?)null,
                       Hits: (int?)null)
                    : VisibleAttackIntent(combat, enemy, move, act, ascension);
                return new PrototypeAiEnemy(
                    enemy.InstanceId,
                    enemy.EnemyId,
                    enemy.Hp,
                    enemy.Block,
                    move?.Id,
                    new Dictionary<string, int>(enemy.Statuses, StringComparer.Ordinal),
                    enemy.PowerStates.Select(power =>
                        new PrototypeAiPower(power.PowerId, power.Stacks)).ToArray(),
                    threat.Damage,
                    threat.Hits,
                    threat.BaseDamage);
            }).ToArray(),
            PendingChoiceId: combat.PendingChoice?.ChoiceId,
            PlayerPowers: combat.PlayerPowers
                .Select(power => new PrototypeAiPower(
                    power.PowerId, power.Stacks))
                .ToArray());
    }
}
