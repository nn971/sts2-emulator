using System.Text.Json;

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
    JsonElement State);

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
    PrototypeAiPower[] Powers);

public sealed record PrototypeAiCombat(
    int Turn,
    int Energy,
    int PlayerBlock,
    PrototypeAiCard[] Hand,
    int DrawPileCount,
    PrototypeAiCard[] DiscardPile,
    PrototypeAiCard[] ExhaustPile,
    PrototypeAiEnemy[] Enemies,
    string? PendingChoiceId);

public sealed record PrototypeAiMapNode(
    string NodeId,
    int Act,
    int Floor,
    PrototypeRoomType RoomType,
    string[] NextNodeIds);

public sealed record PrototypeAiReward(
    string[] CardOptions,
    string? PotionOption,
    string? RelicOption,
    bool CardResolved,
    bool PotionResolved,
    bool RelicResolved);

public sealed record PrototypeAiShop(
    ShopOffer[] CardOffers,
    ShopOffer? PotionOffer,
    ShopOffer? RelicOffer,
    int RemovalPrice,
    bool RemovalUsed);

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
    string? TerminalOutcome);

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
                : CreateCombatObservation(world.Combat),
            Reward: world?.Reward is null
                ? null
                : new PrototypeAiReward(
                    (string[])world.Reward.CardOptions.Clone(),
                    world.Reward.PotionOption,
                    world.Reward.RelicOption,
                    world.Reward.CardResolved,
                    world.Reward.PotionResolved,
                    world.Reward.RelicResolved),
            Shop: world?.Shop is null
                ? null
                : new PrototypeAiShop(
                    (ShopOffer[])world.Shop.CardOffers.Clone(),
                    world.Shop.PotionOffer is null ? null : world.Shop.PotionOffer with { },
                    world.Shop.RelicOffer is null ? null : world.Shop.RelicOffer with { },
                    world.Shop.RemovalPrice,
                    world.Shop.RemovalUsed),
            EventId: world?.Event?.EventId,
            TerminalOutcome: world?.TerminalOutcome);
    }

    private static PrototypeAiCombat CreateCombatObservation(CombatState combat)
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
                card.State.Clone());
        }

        return new PrototypeAiCombat(
            Turn: combat.Turn,
            Energy: combat.Energy,
            PlayerBlock: combat.PlayerBlock,
            Hand: combat.Hand.Select(Card).ToArray(),
            DrawPileCount: combat.DrawPile.Length,
            DiscardPile: combat.DiscardPile.Select(Card).ToArray(),
            ExhaustPile: combat.ExhaustPile.Select(Card).ToArray(),
            Enemies: combat.Enemies.Select(enemy =>
            {
                var definition = PrototypeContent.Enemy(enemy.EnemyId);
                var moveId = definition.Moves.Length == 0
                    ? null
                    : definition.Moves[enemy.MoveIndex % definition.Moves.Length].Id;

                return new PrototypeAiEnemy(
                    enemy.InstanceId,
                    enemy.EnemyId,
                    enemy.Hp,
                    enemy.Block,
                    moveId,
                    new Dictionary<string, int>(enemy.Statuses, StringComparer.Ordinal),
                    enemy.PowerStates.Select(power =>
                        new PrototypeAiPower(power.PowerId, power.Stacks)).ToArray());
            }).ToArray(),
            PendingChoiceId: combat.PendingChoice?.ChoiceId);
    }
}
