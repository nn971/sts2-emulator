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
    int? IntentHits = null);

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
    PrototypeAiExtraRelicReward[]? PendingExtraRelicRewards = null);

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

public sealed record PrototypeAiEvent(
    string EventId,
    string? ChosenChoiceId,
    PrototypeAiEventDeckChoice? PendingDeckChoice,
    PrototypeAiEventPotionReplacement? PendingPotionReplacement = null,
    string[]? QueuedPotionIds = null,
    PrototypeAiEventQueuedDeckChoice[]? QueuedDeckChoices = null,
    string[]? OfferedChoiceIds = null);

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
            RulesetId: world?.RulesetId ?? PrototypeContent.RulesetId,
            CharacterId: world?.CharacterId ?? PrototypeContent.CharacterId,
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
                        : (string[])world.Event.OfferedChoiceIds.Clone()),
            MapGenerationProfileId:
                world?.Map.GenerationProfileId,
            CompletedRooms: world is null
                ? Array.Empty<PrototypeCompletedRoomRecord>()
                : (PrototypeCompletedRoomRecord[])world.CompletedRooms.Clone());
    }

    // The current combat engine rolls many random/state-machine moves when
    // the enemy ACTS, not when the turn is first displayed. Do not turn that
    // private future RNG into a fake public prediction. Only show a move
    // that is already forced by the current publicly visible turn state.
    private static PrototypeEnemyMoveDefinition? VisibleDeterministicMove(
        EnemyCombatState enemy,
        bool hideEnemyIntents)
    {
        if (hideEnemyIntents
            || enemy.Hp <= 0
            || enemy.SkipNextEnemyAction
            || enemy.EnemyActionSkipsRemaining > 0)
        {
            return null;
        }

        var definition = PrototypeContent.Enemy(enemy.EnemyId);
        if (definition.Moves.Length == 0 || enemy.MoveIndex < 0)
        {
            return null;
        }

        if (definition.MovePolicy == PrototypeEnemyMovePolicy.SequentialLoop)
        {
            var index = enemy.MoveIndex;
            if (index >= definition.Moves.Length)
            {
                var start = definition.MoveLoopStartIndex;
                if (start < 0 || start >= definition.Moves.Length)
                {
                    return null;
                }

                index = start + ((index - start) % (definition.Moves.Length - start));
            }

            return definition.Moves[index];
        }

        if (definition.MovePolicy
            == PrototypeEnemyMovePolicy.UniformRandomAfterOpener)
        {
            var opening = definition.OpeningMoveIndices
                ?? [definition.OpeningMoveIndex];
            if (enemy.MoveIndex < opening.Length)
            {
                var index = opening[enemy.MoveIndex];
                return index >= 0 && index < definition.Moves.Length
                    ? definition.Moves[index]
                    : null;
            }
        }

        // State-machine choices and random moves cannot currently be
        // guaranteed without advancing the private RNG or simulating hidden
        // AI branches. Correct future handling needs a distinct planned
        // public intent in canonical combat state.
        return null;
    }

    private static (int? Damage, int? Hits) VisibleAttackIntent(
        CombatState combat,
        EnemyCombatState enemy,
        PrototypeEnemyMoveDefinition move,
        int act,
        int ascension)
    {
        int? perHitDamage = null;
        var hits = 0;
        foreach (var effect in move.Effects)
        {
            if (effect.Kind != PrototypeEnemyEffectKind.DamagePlayer)
            {
                // A preceding self-buff could alter subsequent hit damage.
                // Do not approximate it using the enemy's pre-move powers.
                if (effect.Kind == PrototypeEnemyEffectKind.ApplyEnemyPower)
                {
                    return (null, null);
                }

                continue;
            }

            // The v1 interface represents a homogeneous attack as one
            // per-hit damage value and one hit count. Mixed attack/nonattack
            // damage and nonuniform attack amounts cannot fit this shape.
            if (!effect.IsAttack)
            {
                return (null, null);
            }

            var repetitions = effect.RepetitionsAt(ascension);
            if (repetitions <= 0)
            {
                continue;
            }

            var damage = Math.Max(0,
                PrototypeGameEngine.EnemyHitDamage(
                    enemy, combat, effect, act, ascension));
            if (perHitDamage is not null && perHitDamage != damage)
            {
                return (null, null);
            }

            perHitDamage = damage;
            hits += repetitions;
        }

        return hits > 0 && perHitDamage is not null
            ? (perHitDamage, hits)
            : (null, null);
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
                var move = VisibleDeterministicMove(enemy, hideEnemyIntents);
                var threat = move is null
                    ? (Damage: (int?)null, Hits: (int?)null)
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
                    threat.Hits);
            }).ToArray(),
            PendingChoiceId: combat.PendingChoice?.ChoiceId,
            PlayerPowers: combat.PlayerPowers
                .Select(power => new PrototypeAiPower(
                    power.PowerId, power.Stacks))
                .ToArray());
    }
}
