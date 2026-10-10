namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine : IDeterministicEngine
{
    public IReadOnlyList<GameAction> GetLegalActions(RunState state)
    {
        RunConfiguration.ValidateState(state);
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
        RunConfiguration.ValidateState(state);
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

        state = state with { Player = player };

        // An event's full-belt potion offer is still pending when a
        // usable potion is consumed from the belt. The newly vacated
        // slot can accept that offer directly; leaving its old replacement
        // candidates active would expose an invalid slot and could make
        // the event impossible to finish.
        if (state.Phase == RunPhase.Event
            && state.World?.Event?.PendingPotionReplacement
                is { } replacement)
        {
            var emptySlot = Array.IndexOf(
                player.PotionSlots,
                null);
            if (emptySlot >= 0)
            {
                var slots =
                    (PotionInstance?[])player.PotionSlots.Clone();
                slots[emptySlot] = new PotionInstance(
                    replacement.PotionId,
                    PrototypeJson.EmptyObject());
                var world = RequireWorld(state);
                state = state with
                {
                    Player = player with { PotionSlots = slots },
                    World = world with
                    {
                        Event = world.Event! with
                        {
                            PendingPotionReplacement = null
                        }
                    }
                };
                return AdvanceEventContinuations(state);
            }
        }

        return state;
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

                case PrototypeRunEffectKind.LoseMaxHp:
                {
                    var amount = Math.Max(
                        0,
                        effect.Amount);
                    var maxHp = Math.Max(
                        1,
                        player.MaxHp - amount);
                    player = player with
                    {
                        MaxHp = maxHp,
                        Hp = Math.Min(
                            player.Hp,
                            maxHp)
                    };
                    break;
                }

                case PrototypeRunEffectKind.FillPotionSlots:
                {
                    if (!CanAcquirePotion(player))
                    {
                        break;
                    }

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

                case PrototypeRunEffectKind.LoseGold:
                    player = player with
                    {
                        Gold = Math.Max(
                            0,
                            player.Gold - effect.Amount)
                    };
                    break;

                case PrototypeRunEffectKind.LoseAllGold:
                    player = player with { Gold = 0 };
                    break;

                case PrototypeRunEffectKind.UpgradeLastCardOfId:
                {
                    var id = effect.CardId ?? throw new InvalidOperationException(
                        "Upgrading the last matching card requires a card ID.");
                    var index = Array.FindLastIndex(
                        player.Deck,
                        card => StringComparer.Ordinal.Equals(card.CardId, id));
                    if (index >= 0 && player.Deck[index].UpgradeLevel == 0)
                    {
                        var deck = (CardInstance[])player.Deck.Clone();
                        deck[index] = deck[index] with { UpgradeLevel = 1 };
                        player = player with { Deck = deck };
                    }

                    break;
                }

                case PrototypeRunEffectKind.AddCard:
                    throw new NotSupportedException(
                        "Generic run effects do not currently create persistent cards directly.");

                case PrototypeRunEffectKind.GainRelic:
                case PrototypeRunEffectKind.GainPotion:
                    throw new NotSupportedException(
                        "Item acquisition run effects require run-state context.");

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
        RngBundle? rng = null,
        RunWorldState? world = null)
    {
        // RelicAcquired models RelicModel.AfterObtained on the newly
        // appended instance, not a broadcast to every owned relic with the
        // same model ID. Older copies retain their independent state and
        // must not replay acquisition-only rewards (e.g. Old Coin).
        var acquiredIndex = eventKind == PrototypeRunEventKind.RelicAcquired
            ? Array.FindLastIndex(player.Relics, relic =>
                StringComparer.Ordinal.Equals(relic.RelicId, acquiredRelicId))
            : -1;
        for (var relicIndex = 0; relicIndex < player.Relics.Length;
             relicIndex++)
        {
            if (eventKind == PrototypeRunEventKind.RelicAcquired
                && relicIndex != acquiredIndex)
            {
                continue;
            }

            var relic = player.Relics[relicIndex];
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

                if (trigger.RouteCondition is not null
                    && (world is null
                        || !MatchesRouteCondition(
                            world,
                            trigger.RouteCondition)))
                {
                    continue;
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

        if (state.Configuration is not null)
            return V111RunFactory.Initialize(state);

        var rules = PrototypeContent.Rules;
        // Ordinary starts use the historical correlated run-seed streams.
        // A distinct, hypothetical factorized prior may instead supply
        // all six independent stream states *before* the first RNG event.
        var rng = state.Rng.Streams.Length == 0
            ? PrototypeRng.CreateBundle(state.RunSeed)
            : state.Rng.Fork();
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

        if (state.Configuration is not null)
            throw new NotSupportedException("Native Hive/Glory mechanics are not implemented; prototype later-act fallback is disabled for v111 runs.");

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

        if (!StringComparer.Ordinal.Equals(world.RulesetId, state.Configuration?.RulesetId ?? PrototypeContent.RulesetId))
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

    private static int RewardCardChoiceCount(PlayerState player) =>
        Math.Max(
            0,
            3 + player.Relics.Sum(relic =>
                PrototypeContent.Relic(relic.RelicId)
                    .RewardCardChoiceBonus));

    private static int ExtraNormalCombatCardRewardGroups(
        PlayerState player) =>
        Math.Max(
            0,
            player.Relics.Sum(relic =>
                PrototypeContent.Relic(relic.RelicId)
                    .ExtraNormalCombatCardRewardGroups));

    private static bool CanAcquirePotion(PlayerState player) =>
        !player.Relics.Any(relic =>
            PrototypeContent.Relic(relic.RelicId)
                .PreventPotionAcquisition);

    private static bool CanRestHeal(PlayerState player) =>
        !player.Relics.Any(relic =>
            PrototypeContent.Relic(relic.RelicId)
                .PreventRestHealing);

    private static bool CanRestUpgrade(PlayerState player) =>
        !player.Relics.Any(relic =>
            PrototypeContent.Relic(relic.RelicId)
                .PreventRestUpgrade);

    private static bool HideEnemyIntents(PlayerState player) =>
        player.Relics.Any(relic =>
            PrototypeContent.Relic(relic.RelicId)
                .HideEnemyIntents);

    private static int ApplyShopPriceModifiers(
        PlayerState player,
        int basePrice)
    {
        long numerator = Math.Max(0, basePrice);
        long denominator = 1;

        foreach (var relic in player.Relics)
        {
            var definition =
                PrototypeContent.Relic(relic.RelicId);
            if (definition.ShopPriceNumerator < 0
                || definition.ShopPriceDenominator <= 0)
            {
                throw new InvalidOperationException(
                    $"Relic '{definition.Id}' has an invalid shop-price multiplier.");
            }

            numerator *= definition.ShopPriceNumerator;
            denominator *= definition.ShopPriceDenominator;
        }

        return (int)Math.Max(
            0,
            numerator / denominator);
    }

    private static int ShopRemovalPrice(
        PlayerState player,
        int basePrice)
    {
        var overridePrices = player.Relics
            .Select(relic =>
                PrototypeContent.Relic(relic.RelicId)
                    .ShopRemovalPriceOverride)
            .Where(price => price is not null)
            .Select(price => price!.Value)
            .ToArray();
        var undiscounted = overridePrices.Length == 0
            ? basePrice
            : overridePrices.Min();
        return ApplyShopPriceModifiers(
            player,
            undiscounted);
    }

    private static ShopState RepriceShop(
        ShopState shop,
        PlayerState player)
    {
        ShopOffer Reprice(ShopOffer offer)
        {
            var basePrice = offer.UndiscountedPrice;
            return offer with
            {
                Price = ApplyShopPriceModifiers(
                    player,
                    basePrice),
                BasePrice = basePrice
            };
        }

        var baseRemovalPrice =
            shop.UndiscountedRemovalPrice;
        return shop with
        {
            CardOffers = shop.CardOffers
                .Select(Reprice)
                .ToArray(),
            PotionOffer = shop.PotionOffer is null
                ? null
                : Reprice(shop.PotionOffer),
            RelicOffer = shop.RelicOffer is null
                ? null
                : Reprice(shop.RelicOffer),
            AdditionalPotionOffers =
                shop.AdditionalPotionOffers is null
                    ? null
                    : shop.AdditionalPotionOffers
                        .Select(Reprice)
                        .ToArray(),
            AdditionalRelicOffers =
                shop.AdditionalRelicOffers is null
                    ? null
                    : shop.AdditionalRelicOffers
                        .Select(Reprice)
                        .ToArray(),
            RemovalPrice = ShopRemovalPrice(
                player,
                baseRemovalPrice),
            BaseRemovalPrice = baseRemovalPrice
        };
    }

    private static PlayerState AppendCard(
        PlayerState player,
        long instanceId,
        string cardId,
        RngBundle rng)
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

        var card = ApplyNewCardEnchantments(
            player,
            new CardInstance(
                instanceId,
                cardId,
                upgradeLevel,
                PrototypeJson.EmptyObject()));
        player = player with
        {
            Deck = player.Deck.Append(card).ToArray()
        };

        return ApplyRelicRunEvent(
            player,
            PrototypeRunEventKind.CardAdded,
            addedCard: card,
            rng: rng);
    }

    /// <summary>
    /// Apply acquisition-time relic enchantments to persistent cards.
    /// Cards that already carry a persistent enchantment preserve it.
    /// Source: FresnelLens.TryModifyCardBeingAddedToDeck.
    /// </summary>
    private static CardInstance ApplyNewCardEnchantments(
        PlayerState player, CardInstance card)
    {
        if (card.Enchantment is not null)
        {
            return card;
        }

        var definition = PrototypeContent.Card(card.CardId);
        var gainsBlock = definition.Effects.Any(effect =>
            effect.Kind is
                PrototypeCombatEffectKind.GainPlayerBlock
                or PrototypeCombatEffectKind.GainToricToughnessBlock
                or PrototypeCombatEffectKind.GainPlayerBlockFromEnemyStatusTotal
                or PrototypeCombatEffectKind.GainPlayerBlockAndApplyPowerFromActualGain);
        if (!gainsBlock)
        {
            return card;
        }

        // Fresnel Lens attaches Nimble through an ordered relic hook.
        // The pinned Nimble EnchantmentModel is NOT stackable, so the
        // first owned Lens enchants the card; later copies cannot add
        // their amounts again to an already enchanted card.
        var amount = player.Relics
            .Select(relic => PrototypeContent.Relic(relic.RelicId)
                .EnchantNewBlockCardsNimble)
            .FirstOrDefault(value => value > 0);
        return amount > 0
            ? card with
            {
                Enchantment = new PrototypeCardEnchantment(
                    PrototypeCardEnchantmentKind.Nimble, amount)
            }
            : card;
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
