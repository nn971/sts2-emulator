namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine : IDeterministicEngine
{
    public IReadOnlyList<GameAction> GetLegalActions(RunState state)
    {
        return state.Phase switch
        {
            RunPhase.RunStart => [GameAction.Empty("start_run")],
            RunPhase.MapChoice => GetMapActions(state),
            RunPhase.Combat => GetCombatActions(state),
            RunPhase.Reward => GetRewardActions(state),
            RunPhase.Shop => GetShopActions(state),
            RunPhase.Event => GetEventActions(state),
            RunPhase.Rest => GetRestActions(state),
            RunPhase.ActTransition => [GameAction.Empty("continue_act")],
            RunPhase.Terminal => Array.Empty<GameAction>(),
            _ => Array.Empty<GameAction>()
        };
    }

    public TransitionResult Step(RunState state, GameAction action)
    {
        var fork = state.Fork();
        fork = fork with { DecisionIndex = fork.DecisionIndex + 1 };

        var next = state.Phase switch
        {
            RunPhase.RunStart => StartRun(fork, action),
            RunPhase.MapChoice => StepMap(fork, action),
            RunPhase.Combat => StepCombat(fork, action),
            RunPhase.Reward => StepReward(fork, action),
            RunPhase.Shop => StepShop(fork, action),
            RunPhase.Event => StepEvent(fork, action),
            RunPhase.Rest => StepRest(fork, action),
            RunPhase.ActTransition => StepActTransition(fork, action),
            RunPhase.Terminal => throw new InvalidOperationException("A terminal run has no legal actions."),
            _ => throw new NotSupportedException($"Prototype phase '{state.Phase}' is unsupported.")
        };

        return new TransitionResult(next, Array.Empty<string>());
    }

    private static RunState StartRun(RunState state, GameAction action)
    {
        RequireKind(action, "start_run");

        var rules = PrototypeContent.Rules;
        var rng = PrototypeRng.CreateBundle(state.RunSeed);
        var nextId = 1L;
        var deck = PrototypeContent.StartingDeck
            .Select(cardId => new CardInstance(
                nextId++,
                cardId,
                0,
                PrototypeJson.EmptyObject()))
            .ToArray();

        var player = new PlayerState(
            Hp: rules.StartingHp,
            MaxHp: rules.StartingHp,
            Gold: rules.StartingGold,
            Deck: deck,
            Relics: PrototypeContent.StartingRelics
                .Select(relicId => new RelicInstance(
                    relicId,
                    PrototypeJson.EmptyObject()))
                .ToArray(),
            PotionSlots: new PotionInstance?[rules.PotionSlots]);

        var world = new RunWorldState(
            RulesetId: rules.Id,
            CharacterId: PrototypeContent.CharacterId,
            Act: 1,
            Floor: 0,
            NextCardInstanceId: nextId,
            ActiveRoom: null,
            Map: new MapState(Array.Empty<MapNodeState>()),
            Combat: null,
            Reward: null,
            Shop: null,
            Event: null,
            TerminalOutcome: null,
            ActOneRegion: PrototypeActOneRegion.Overgrowth,
            ActOneEncounterPool: new PrototypeActOneEncounterPoolState(
                PrototypeActOneRegion.Overgrowth,
                OrdinaryCombatsStarted: 0,
                RemainingWeakEncounterIds:
                    (string[])PrototypeContent
                        .OvergrowthWeakEncounterPool
                        .Clone(),
                RemainingNormalEncounterIds:
                    (string[])PrototypeContent
                        .OvergrowthNormalEncounterPool
                        .Clone()));

        world = world with { Map = GenerateActMap(world.Act, rng) };

        return state with
        {
            Player = player,
            Rng = rng,
            World = world,
            Phase = RunPhase.MapChoice
        };
    }

    private static RunState StepActTransition(RunState state, GameAction action)
    {
        RequireKind(action, "continue_act");
        var world = RequireWorld(state);

        if (world.Act >= PrototypeContent.Rules.Acts)
        {
            throw new InvalidOperationException("The final act cannot transition to another act.");
        }

        world = world with
        {
            Act = world.Act + 1,
            Floor = 0,
            ActiveRoom = null,
            Combat = null,
            Reward = null,
            Shop = null,
            Event = null
        };
        world = world with { Map = GenerateActMap(world.Act, state.Rng) };

        return state with
        {
            World = world,
            Phase = RunPhase.MapChoice
        };
    }

    private static RunWorldState RequireWorld(RunState state)
    {
        var world = state.World
            ?? throw new InvalidOperationException("Prototype world state has not been initialized.");

        if (!StringComparer.Ordinal.Equals(world.RulesetId, PrototypeContent.RulesetId))
        {
            throw new InvalidOperationException(
                $"Prototype engine cannot run ruleset '{world.RulesetId}'.");
        }

        return world;
    }

    private static void RequireKind(GameAction action, string expected)
    {
        if (!StringComparer.Ordinal.Equals(action.Kind, expected))
        {
            throw new InvalidOperationException(
                $"Expected action '{expected}', received '{action.Kind}'.");
        }
    }

    private static CardInstance RequireCard(PlayerState player, long instanceId) =>
        player.Deck.FirstOrDefault(card => card.InstanceId == instanceId)
        ?? throw new InvalidOperationException($"Card instance {instanceId} is missing.");

    private static int EnergyPerTurn(PlayerState player) =>
        PrototypeContent.Rules.BaseEnergy
        + player.Relics.Sum(relic => PrototypeContent.Relic(relic.RelicId).EnergyPerTurnBonus);

    private static int FirstTurnDrawBonus(PlayerState player) =>
        player.Relics.Sum(relic => PrototypeContent.Relic(relic.RelicId).FirstTurnDrawBonus);

    private static PlayerState AppendCard(PlayerState player, long instanceId, string cardId)
    {
        var deck = player.Deck.Append(
            new CardInstance(
                instanceId,
                cardId,
                0,
                PrototypeJson.EmptyObject())).ToArray();

        return player with { Deck = deck };
    }

    private static RunState EndRun(RunState state, string outcome)
    {
        var world = RequireWorld(state) with
        {
            Combat = null,
            Reward = null,
            Shop = null,
            Event = null,
            ActiveRoom = null,
            TerminalOutcome = outcome
        };

        return state with
        {
            World = world,
            Phase = RunPhase.Terminal
        };
    }
}
