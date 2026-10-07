namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine : IDeterministicEngine
{
    public IReadOnlyList<GameAction> GetLegalActions(RunState state)
    {
        var phaseActions = state.Phase switch
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

        if (state.Phase is RunPhase.RunStart
            or RunPhase.Combat
            or RunPhase.Terminal)
        {
            return phaseActions;
        }

        return phaseActions
            .Concat(GetAnyTimePotionActions(state))
            .ToArray();
    }

    public TransitionResult Step(RunState state, GameAction action)
    {
        var fork = state.Fork();
        fork = fork with { DecisionIndex = fork.DecisionIndex + 1 };

        var next =
            StringComparer.Ordinal.Equals(
                action.Kind,
                "use_potion")
            && state.Phase != RunPhase.Combat
                ? UsePotionOutsideCombat(
                    fork,
                    action.ReadPayload<UsePotionPayload>())
                : state.Phase switch
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

    private static IReadOnlyList<GameAction>
        GetAnyTimePotionActions(RunState state) =>
        state.Player.PotionSlots
            .Select((potion, slot) =>
                new { Potion = potion, Slot = slot })
            .Where(item =>
                item.Potion is not null
                && PrototypeContent.Potion(
                    item.Potion.PotionId)
                    .UsableOutsideCombat)
            .Select(item =>
                GameAction.Create(
                    "use_potion",
                    new UsePotionPayload(
                        item.Slot,
                        null)))
            .ToArray();

    private static RunState UsePotionOutsideCombat(
        RunState state,
        UsePotionPayload payload)
    {
        if (payload.TargetEnemyId is not null)
        {
            throw new InvalidOperationException(
                "Out-of-combat potions cannot target an enemy.");
        }

        if (payload.Slot < 0
            || payload.Slot
                >= state.Player.PotionSlots.Length)
        {
            throw new InvalidOperationException(
                $"Potion slot {payload.Slot} is invalid.");
        }

        var potion =
            state.Player.PotionSlots[payload.Slot]
            ?? throw new InvalidOperationException(
                $"Potion slot {payload.Slot} is empty.");
        var definition =
            PrototypeContent.Potion(potion.PotionId);
        if (!definition.UsableOutsideCombat)
        {
            throw new InvalidOperationException(
                $"Potion '{definition.Name}' is combat-only.");
        }

        var player = ConsumePotionSlot(
            state.Player,
            payload.Slot);
        player = ApplyPotionRunEffects(
            player,
            definition,
            state.Rng);

        return state with { Player = player };
    }

    private static PlayerState ConsumePotionSlot(
        PlayerState player,
        int slot)
    {
        var slots =
            (PotionInstance?[])player.PotionSlots.Clone();
        slots[slot] = null;
        return player with { PotionSlots = slots };
    }

    private static PlayerState ApplyPotionRunEffects(
        PlayerState player,
        PrototypePotionDefinition definition,
        RngBundle rng) =>
        ApplyPlayerRunEffects(
            player,
            definition.RunEffects
                ?? Array.Empty<PrototypeRunEffectSpec>(),
            rng);

    private static PlayerState ApplyPlayerRunEffects(
        PlayerState player,
        IEnumerable<PrototypeRunEffectSpec> effects,
        RngBundle? rng = null)
    {
        foreach (var effect in effects)
        {
            switch (effect.Kind)
            {
                case PrototypeRunEffectKind.Heal:
                    player = player with
                    {
                        Hp = Math.Min(
                            player.MaxHp,
                            player.Hp + effect.Amount)
                    };
                    break;

                case PrototypeRunEffectKind.HealToFull:
                    player = player with
                    {
                        Hp = player.MaxHp
                    };
                    break;

                case PrototypeRunEffectKind.HealPercentMaxHp:
                {
                    var amount = Math.Max(
                        0,
                        (player.MaxHp
                            * effect.Amount) / 100);
                    player = player with
                    {
                        Hp = Math.Min(
                            player.MaxHp,
                            player.Hp + amount)
                    };
                    break;
                }

                case PrototypeRunEffectKind.GainMaxHp:
                {
                    var amount = Math.Max(
                        0,
                        effect.Amount);
                    player = player with
                    {
                        MaxHp = player.MaxHp + amount,
                        Hp = player.Hp + amount
                    };
                    break;
                }

                case PrototypeRunEffectKind.FillPotionSlots:
                {
                    if (rng is null)
                    {
                        throw new InvalidOperationException(
                            "Filling potion slots requires an RNG bundle.");
                    }

                    var slots =
                        (PotionInstance?[])
                        player.PotionSlots.Clone();
                    for (var index = 0;
                         index < slots.Length;
                         index++)
                    {
                        if (slots[index] is not null)
                        {
                            continue;
                        }

                        var potionId =
                            PrototypeContent.PotionPool[
                                PrototypeRng.NextInt(
                                    rng,
                                    "reward",
                                    PrototypeContent
                                        .PotionPool.Length)];
                        slots[index] =
                            new PotionInstance(
                                potionId,
                                PrototypeJson
                                    .EmptyObject());
                    }

                    player = player with
                    {
                        PotionSlots = slots
                    };
                    break;
                }

                case PrototypeRunEffectKind.LoseHp:
                    player = player with
                    {
                        Hp = Math.Max(
                            0,
                            player.Hp - effect.Amount)
                    };
                    break;

                case PrototypeRunEffectKind.GainGold:
                    player = player with
                    {
                        Gold =
                            player.Gold + effect.Amount
                    };
                    break;

                case PrototypeRunEffectKind.AddCard:
                    throw new NotSupportedException(
                        "Generic run effects do not currently create persistent cards directly.");

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return player;
    }

    private static PlayerState ApplyRelicRunEvent(
        PlayerState player,
        PrototypeRunEventKind eventKind,
        CardInstance? addedCard = null,
        string? acquiredRelicId = null,
        RngBundle? rng = null)
    {
        foreach (var relic in player.Relics)
        {
            if (eventKind == PrototypeRunEventKind.RelicAcquired
                && !StringComparer.Ordinal.Equals(
                    relic.RelicId,
                    acquiredRelicId))
            {
                continue;
            }

            var definition =
                PrototypeContent.Relic(relic.RelicId);
            foreach (var trigger in
                     definition.RunTriggers
                     ?? Array.Empty<PrototypeRelicRunTriggerSpec>())
            {
                if (trigger.EventKind != eventKind)
                {
                    continue;
                }

                if (trigger.RequiredCardType is not null)
                {
                    if (addedCard is null
                        || PrototypeContent.Card(
                            addedCard.CardId).Type
                            != trigger.RequiredCardType.Value)
                    {
                        continue;
                    }
                }

                player = ApplyPlayerRunEffects(
                    player,
                    trigger.Effects,
                    rng);
            }
        }

        return player;
    }

    private static RunState StartRun(RunState state, GameAction action)
    {
        RequireKind(action, "start_run");

        var rules = PrototypeContent.Rules;
        var rng = PrototypeRng.CreateBundle(state.RunSeed);
        var actOneBossEncounterId =
            PrototypeContent.OvergrowthBossEncounterPool[
                PrototypeRng.NextInt(
                    rng,
                    "combat",
                    PrototypeContent
                        .OvergrowthBossEncounterPool.Length)];
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
                        .Clone(),
                RemainingEliteEncounterIds:
                    (string[])PrototypeContent
                        .OvergrowthEliteEncounterPool
                        .Clone(),
                BossEncounterId:
                    actOneBossEncounterId));

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
        var definition = PrototypeContent.Card(cardId);
        var upgradeLevel = player.Relics.Any(relic =>
        {
            var relicDefinition =
                PrototypeContent.Relic(relic.RelicId);
            return relicDefinition.UpgradeAddedCardType
                    == definition.Type
                && definition.Type is not
                    PrototypeCardType.Status
                    and not PrototypeCardType.Curse;
        })
            ? 1
            : 0;

        var card = new CardInstance(
            instanceId,
            cardId,
            upgradeLevel,
            PrototypeJson.EmptyObject());
        player = player with
        {
            Deck = player.Deck.Append(card).ToArray()
        };

        return ApplyRelicRunEvent(
            player,
            PrototypeRunEventKind.CardAdded,
            addedCard: card);
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
