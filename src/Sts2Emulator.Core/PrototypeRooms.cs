namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static IReadOnlyList<GameAction> GetMapActions(RunState state)
    {
        var world = RequireWorld(state);
        return world.Map.AvailableNodes()
            .Select(node => GameAction.Create(
                "choose_map_node",
                new ChooseMapNodePayload(node.NodeId)))
            .ToArray();
    }

    private static RunState StepMap(RunState state, GameAction action)
    {
        RequireKind(action, "choose_map_node");
        var payload = action.ReadPayload<ChooseMapNodePayload>();
        var world = RequireWorld(state);
        var node = world.Map.AvailableNodes().FirstOrDefault(
            option => StringComparer.Ordinal.Equals(option.NodeId, payload.NodeId))
            ?? throw new InvalidOperationException($"Unknown map node '{payload.NodeId}'.");

        world = world with
        {
            Floor = node.Floor,
            ActiveRoom = node.RoomType,
            Map = world.Map with { CurrentNodeId = node.NodeId },
            Combat = null,
            Reward = null,
            Shop = null,
            Event = null
        };
        state = state with { World = world };

        return node.RoomType switch
        {
            PrototypeRoomType.Combat => StartCombat(state, PrototypeRoomType.Combat),
            PrototypeRoomType.Elite => StartCombat(state, PrototypeRoomType.Elite),
            PrototypeRoomType.Boss => StartCombat(state, PrototypeRoomType.Boss),
            PrototypeRoomType.Event => StartEvent(state),
            PrototypeRoomType.Shop => StartShop(state),
            PrototypeRoomType.Rest => state with { Phase = RunPhase.Rest },
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static MapState GenerateActMap(int act, RngBundle rng)
    {
        var rules = PrototypeContent.Rules;
        var layers = new List<MapNodeState[]>();

        for (var floor = 1; floor <= rules.FloorsPerAct; floor++)
        {
            var width = floor switch
            {
                var value when value == rules.FloorsPerAct => 1,
                1 => 2,
                var value when value == rules.FloorsPerAct - 1 => 2,
                _ => 3
            };

            var floorRule = rules.MapFloorRules.SingleOrDefault(rule =>
                floor >= rule.MinFloor && floor <= rule.MaxFloor)
                ?? throw new InvalidOperationException(
                    $"No prototype map rule covers floor {floor}.");

            if (floorRule.RoomPool.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Prototype map rule for floor {floor} has an empty room pool.");
            }

            var nodes = new MapNodeState[width];
            var usedSpecialRooms = new HashSet<PrototypeRoomType>();
            for (var index = 0; index < width; index++)
            {
                var candidates = floorRule.RoomPool;
                if (!floorRule.AllowDuplicateSpecialRooms)
                {
                    var filtered = candidates
                        .Where(room =>
                            room == PrototypeRoomType.Combat
                            || !usedSpecialRooms.Contains(room))
                        .ToArray();
                    if (filtered.Length > 0)
                    {
                        candidates = filtered;
                    }
                }

                var room = candidates[
                    PrototypeRng.NextInt(rng, "map", candidates.Length)];
                if (room != PrototypeRoomType.Combat)
                {
                    usedSpecialRooms.Add(room);
                }

                nodes[index] = new MapNodeState(
                    NodeId: $"{act}:{floor}:{index}:{room}",
                    Act: act,
                    Floor: floor,
                    RoomType: room,
                    NextNodeIds: Array.Empty<string>());
            }

            layers.Add(nodes);
        }

        for (var layerIndex = 0; layerIndex < layers.Count - 1; layerIndex++)
        {
            var current = layers[layerIndex];
            var next = layers[layerIndex + 1];

            for (var nodeIndex = 0; nodeIndex < current.Length; nodeIndex++)
            {
                var targets = next.Length == 1
                    ? new[] { next[0].NodeId }
                    : new[]
                    {
                        next[nodeIndex % next.Length].NodeId,
                        next[(nodeIndex + 1) % next.Length].NodeId
                    }
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                current[nodeIndex] = current[nodeIndex] with { NextNodeIds = targets };
            }
        }

        var allNodes = layers.SelectMany(layer => layer).ToArray();
        return new MapState(
            Nodes: allNodes,
            CurrentNodeId: null,
            EntryNodeIds: layers[0].Select(node => node.NodeId).ToArray());
    }

    private static RunState StartEvent(RunState state)
    {
        var world = RequireWorld(state);
        var eligible = PrototypeContent.Events.Values
            .Where(evt =>
                world.Act >= evt.MinAct
                && world.Act <= evt.MaxAct
                && evt.Weight > 0
                && (!evt.OncePerRun
                    || !world.EventIds.Contains(evt.Id, StringComparer.Ordinal)))
            .ToArray();

        if (eligible.Length == 0)
        {
            throw new InvalidOperationException(
                $"No prototype event is eligible in act {world.Act}.");
        }

        var previousEvent = world.EventIds.LastOrDefault();
        if (previousEvent is not null && eligible.Length > 1)
        {
            var withoutImmediateRepeat = eligible
                .Where(evt =>
                    !StringComparer.Ordinal.Equals(evt.Id, previousEvent))
                .ToArray();
            if (withoutImmediateRepeat.Length > 0)
            {
                eligible = withoutImmediateRepeat;
            }
        }

        var totalWeight = eligible.Sum(evt => evt.Weight);
        var roll = PrototypeRng.NextInt(state.Rng, "event", totalWeight);
        var selected = eligible[^1];
        foreach (var evt in eligible)
        {
            if (roll < evt.Weight)
            {
                selected = evt;
                break;
            }

            roll -= evt.Weight;
        }

        world = world with
        {
            Event = new EventState(selected.Id),
            EventHistory = world.EventIds.Append(selected.Id).ToArray()
        };

        return state with
        {
            World = world,
            Phase = RunPhase.Event
        };
    }

    private static IReadOnlyList<GameAction> GetEventActions(RunState state)
    {
        var eventState = RequireWorld(state).Event
            ?? throw new InvalidOperationException("Event phase has no event state.");
        var definition = PrototypeContent.Event(eventState.EventId);

        return definition.Choices
            .Select(choice => GameAction.Create(
                "event_choice",
                new EventChoicePayload(choice.Id)))
            .ToArray();
    }

    private static RunState StepEvent(RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var payload = action.ReadPayload<EventChoicePayload>();
        var world = RequireWorld(state);
        var eventState = world.Event
            ?? throw new InvalidOperationException("Event phase has no event state.");
        var definition = PrototypeContent.Event(eventState.EventId);
        var choice = definition.Choices.FirstOrDefault(
            item => StringComparer.Ordinal.Equals(item.Id, payload.ChoiceId))
            ?? throw new InvalidOperationException($"Unknown event choice '{payload.ChoiceId}'.");

        var player = state.Player;
        var nextId = world.NextCardInstanceId;

        foreach (var effect in choice.Effects)
        {
            switch (effect.Kind)
            {
                case PrototypeRunEffectKind.Heal:
                    player = player with { Hp = Math.Min(player.MaxHp, player.Hp + effect.Amount) };
                    break;

                case PrototypeRunEffectKind.LoseHp:
                    player = player with { Hp = Math.Max(0, player.Hp - effect.Amount) };
                    break;

                case PrototypeRunEffectKind.GainGold:
                    player = player with { Gold = player.Gold + effect.Amount };
                    break;

                case PrototypeRunEffectKind.AddCard:
                    if (effect.CardId is null)
                    {
                        throw new InvalidOperationException("Add-card event effect is missing a card ID.");
                    }

                    player = AppendCard(player, nextId++, effect.CardId);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        state = state with
        {
            Player = player,
            World = world with { NextCardInstanceId = nextId }
        };

        if (player.Hp <= 0)
        {
            return EndRun(state, "defeat");
        }

        return CompleteRoomToMap(state);
    }

    private static RunState StartShop(RunState state)
    {
        var world = RequireWorld(state);
        var cardIds = PickShopCards(
            world.Act,
            3,
            state.Rng);

        var offers = cardIds
            .Select((cardId, index) =>
            {
                var basePrice =
                    ShopCardPrice(cardId, state.Rng);
                return new ShopOffer(
                    index + 1,
                    cardId,
                    basePrice,
                    false,
                    BasePrice: basePrice);
            })
            .ToArray();

        var potionId = PrototypeContent.PotionPool[
            PrototypeRng.NextInt(state.Rng, "shop", PrototypeContent.PotionPool.Length)];
        var relicId = PrototypeContent.RelicPool[
            PrototypeRng.NextInt(state.Rng, "shop", PrototypeContent.RelicPool.Length)];
        var potionBasePrice =
            40 + PrototypeRng.NextInt(
                state.Rng,
                "shop",
                21);
        var relicBasePrice =
            100 + PrototypeRng.NextInt(
                state.Rng,
                "shop",
                41);
        var removalBasePrice =
            75 + ((world.Act - 1) * 15);

        var shop = new ShopState(
            offers,
            new ShopOffer(
                100,
                potionId,
                potionBasePrice,
                false,
                BasePrice: potionBasePrice),
            new ShopOffer(
                200,
                relicId,
                relicBasePrice,
                false,
                BasePrice: relicBasePrice),
            RemovalPrice: removalBasePrice,
            BaseRemovalPrice: removalBasePrice);

        var player = ApplyRelicRunEvent(
            state.Player,
            PrototypeRunEventKind.ShopEntered,
            rng: state.Rng);
        shop = RepriceShop(
            shop,
            player);
        world = world with { Shop = shop };

        return state with
        {
            Player = player,
            World = world,
            Phase = RunPhase.Shop
        };
    }

    private static IReadOnlyList<GameAction> GetShopActions(RunState state)
    {
        var world = RequireWorld(state);
        var shop = world.Shop
            ?? throw new InvalidOperationException("Shop phase has no shop state.");
        var actions = new List<GameAction>();

        foreach (var offer in shop.CardOffers.Where(offer => !offer.Sold && offer.Price <= state.Player.Gold))
        {
            actions.Add(GameAction.Create("buy_card", new BuyOfferPayload(offer.OfferId)));
        }

        if (shop.PotionOffer is { Sold: false } potion
            && potion.Price <= state.Player.Gold
            && CanAcquirePotion(state.Player))
        {
            var emptySlot =
                Array.IndexOf(
                    state.Player.PotionSlots,
                    null);
            if (emptySlot >= 0)
            {
                actions.Add(
                    GameAction.Create(
                        "buy_potion",
                        new BuyOfferPayload(
                            potion.OfferId)));
            }
            else
            {
                actions.AddRange(
                    state.Player.PotionSlots
                        .Select((item, slot) =>
                            (Potion: item, Slot: slot))
                        .Where(item =>
                            item.Potion is not null)
                        .Select(item =>
                            GameAction.Create(
                                "replace_shop_potion",
                                new ReplaceShopPotionPayload(
                                    potion.OfferId,
                                    item.Slot))));
            }
        }

        if (shop.RelicOffer is { Sold: false } relic && relic.Price <= state.Player.Gold)
        {
            actions.Add(GameAction.Create("buy_relic", new BuyOfferPayload(relic.OfferId)));
        }

        if (!shop.RemovalUsed && shop.RemovalPrice <= state.Player.Gold)
        {
            actions.AddRange(
                state.Player.Deck
                    .Where(card => !PrototypeContent.Card(card.CardId).Eternal)
                    .Select(card => GameAction.Create(
                        "remove_card",
                        new RemoveCardPayload(card.InstanceId))));
        }

        actions.Add(GameAction.Empty("leave_shop"));
        return actions;
    }

    private static RunState StepShop(RunState state, GameAction action)
    {
        var world = RequireWorld(state);
        var shop = world.Shop
            ?? throw new InvalidOperationException("Shop phase has no shop state.");

        if (StringComparer.Ordinal.Equals(action.Kind, "leave_shop"))
        {
            return CompleteRoomToMap(state);
        }

        var player = state.Player;
        var nextId = world.NextCardInstanceId;

        if (StringComparer.Ordinal.Equals(action.Kind, "remove_card"))
        {
            if (shop.RemovalUsed)
            {
                throw new InvalidOperationException("Shop card removal was already used.");
            }

            if (player.Gold < shop.RemovalPrice)
            {
                throw new InvalidOperationException("Shop card removal is unaffordable.");
            }

            var payload = action.ReadPayload<RemoveCardPayload>();
            var cardToRemove = player.Deck.FirstOrDefault(
                card => card.InstanceId == payload.CardInstanceId)
                ?? throw new InvalidOperationException(
                    $"Card instance {payload.CardInstanceId} is missing.");

            if (PrototypeContent.Card(cardToRemove.CardId).Eternal)
            {
                throw new InvalidOperationException(
                    $"Eternal card {payload.CardInstanceId} cannot be removed from the deck.");
            }

            player = player with
            {
                Gold = player.Gold - shop.RemovalPrice,
                Deck = player.Deck
                    .Where(card => card.InstanceId != payload.CardInstanceId)
                    .ToArray()
            };
            shop = shop with { RemovalUsed = true };
        }
        else if (StringComparer.Ordinal.Equals(action.Kind, "buy_card"))
        {
            var payload = action.ReadPayload<BuyOfferPayload>();
            var offer = shop.CardOffers.FirstOrDefault(item => item.OfferId == payload.OfferId)
                ?? throw new InvalidOperationException($"Unknown card offer {payload.OfferId}.");
            EnsurePurchasable(player, offer);
            player = AppendCard(player with { Gold = player.Gold - offer.Price }, nextId++, offer.ItemId);
            shop = shop with
            {
                CardOffers = shop.CardOffers
                    .Select(item => item.OfferId == offer.OfferId ? item with { Sold = true } : item)
                    .ToArray()
            };
        }
        else if (StringComparer.Ordinal.Equals(action.Kind, "buy_potion"))
        {
            var payload = action.ReadPayload<BuyOfferPayload>();
            var offer = shop.PotionOffer
                ?? throw new InvalidOperationException("Shop has no potion offer.");
            if (offer.OfferId != payload.OfferId)
            {
                throw new InvalidOperationException($"Unknown potion offer {payload.OfferId}.");
            }

            EnsurePurchasable(player, offer);
            if (!CanAcquirePotion(player))
            {
                throw new InvalidOperationException(
                    "Player cannot acquire potions.");
            }

            var slot = Array.IndexOf(player.PotionSlots, null);
            if (slot < 0)
            {
                throw new InvalidOperationException("No potion slot is available.");
            }

            var slots = (PotionInstance?[])player.PotionSlots.Clone();
            slots[slot] = new PotionInstance(offer.ItemId, PrototypeJson.EmptyObject());
            player = player with
            {
                Gold = player.Gold - offer.Price,
                PotionSlots = slots
            };
            shop = shop with { PotionOffer = offer with { Sold = true } };
        }
        else if (StringComparer.Ordinal.Equals(
                     action.Kind,
                     "replace_shop_potion"))
        {
            var payload =
                action.ReadPayload<
                    ReplaceShopPotionPayload>();
            var offer = shop.PotionOffer
                ?? throw new InvalidOperationException(
                    "Shop has no potion offer.");
            if (offer.OfferId != payload.OfferId)
            {
                throw new InvalidOperationException(
                    $"Unknown potion offer {payload.OfferId}.");
            }

            EnsurePurchasable(player, offer);
            if (!CanAcquirePotion(player))
            {
                throw new InvalidOperationException(
                    "Player cannot acquire potions.");
            }

            if (Array.IndexOf(
                    player.PotionSlots,
                    null) >= 0)
            {
                throw new InvalidOperationException(
                    "Potion replacement is only needed when all slots are occupied.");
            }

            player = player with
            {
                Gold =
                    player.Gold - offer.Price,
                PotionSlots = ReplacePotionSlot(
                    player.PotionSlots,
                    payload.Slot,
                    offer.ItemId)
            };
            shop = shop with
            {
                PotionOffer =
                    offer with { Sold = true }
            };
        }
        else if (StringComparer.Ordinal.Equals(action.Kind, "buy_relic"))
        {
            var payload = action.ReadPayload<BuyOfferPayload>();
            var offer = shop.RelicOffer
                ?? throw new InvalidOperationException("Shop has no relic offer.");
            if (offer.OfferId != payload.OfferId)
            {
                throw new InvalidOperationException($"Unknown relic offer {payload.OfferId}.");
            }

            EnsurePurchasable(player, offer);
            player = player with
            {
                Gold = player.Gold - offer.Price,
                Relics = player.Relics.Append(
                    new RelicInstance(offer.ItemId, PrototypeJson.EmptyObject())).ToArray()
            };
            player = ApplyRelicRunEvent(
                player,
                PrototypeRunEventKind.RelicAcquired,
                acquiredRelicId: offer.ItemId,
                rng: state.Rng);
            shop = shop with
            {
                RelicOffer = offer with { Sold = true }
            };
            shop = RepriceShop(
                shop,
                player);
        }
        else
        {
            throw new InvalidOperationException($"Unknown shop action '{action.Kind}'.");
        }

        world = world with
        {
            Shop = shop,
            NextCardInstanceId = nextId
        };

        return state with
        {
            Player = player,
            World = world
        };
    }

    private static PotionInstance?[] ReplacePotionSlot(
        PotionInstance?[] potionSlots,
        int slot,
        string potionId)
    {
        if (slot < 0 || slot >= potionSlots.Length)
        {
            throw new InvalidOperationException(
                $"Potion slot {slot} is invalid.");
        }

        if (potionSlots[slot] is null)
        {
            throw new InvalidOperationException(
                $"Potion slot {slot} is empty and cannot be replaced.");
        }

        _ = PrototypeContent.Potion(potionId);
        var slots =
            (PotionInstance?[])potionSlots.Clone();
        slots[slot] = new PotionInstance(
            potionId,
            PrototypeJson.EmptyObject());
        return slots;
    }

    private static void EnsurePurchasable(PlayerState player, ShopOffer offer)
    {
        if (offer.Sold)
        {
            throw new InvalidOperationException($"Offer {offer.OfferId} is already sold.");
        }

        if (player.Gold < offer.Price)
        {
            throw new InvalidOperationException($"Offer {offer.OfferId} is unaffordable.");
        }
    }

    private static IReadOnlyList<GameAction> GetRestActions(RunState state)
    {
        var actions = new List<GameAction>();
        if (CanRestHeal(state.Player))
        {
            actions.Add(GameAction.Empty("rest_heal"));
        }

        if (PrototypeContent.Rules.RestTrainMaxHp > 0)
        {
            actions.Add(GameAction.Empty("rest_train"));
        }

        if (CanRestUpgrade(state.Player))
        {
            actions.AddRange(
                state.Player.Deck
                    .Where(card => card.UpgradeLevel == 0)
                    .Select(card => GameAction.Create(
                        "rest_upgrade",
                        new UpgradeCardPayload(card.InstanceId))));
        }

        return actions;
    }

    private static RunState StepRest(RunState state, GameAction action)
    {
        if (StringComparer.Ordinal.Equals(action.Kind, "rest_heal"))
        {
            if (!CanRestHeal(state.Player))
            {
                throw new InvalidOperationException(
                    "A relic prevents resting to heal.");
            }

            var amount = Math.Max(
                1,
                (state.Player.MaxHp * PrototypeContent.Rules.RestHealPercent) / 100);
            state = state with
            {
                Player = state.Player with
                {
                    Hp = Math.Min(state.Player.MaxHp, state.Player.Hp + amount)
                }
            };
            return CompleteRoomToMap(state);
        }

        if (StringComparer.Ordinal.Equals(action.Kind, "rest_train"))
        {
            var gain = PrototypeContent.Rules.RestTrainMaxHp;
            if (gain <= 0)
            {
                throw new InvalidOperationException("Rest training is disabled by the active ruleset.");
            }

            state = state with
            {
                Player = state.Player with
                {
                    MaxHp = state.Player.MaxHp + gain,
                    Hp = state.Player.Hp + gain
                }
            };
            return CompleteRoomToMap(state);
        }

        RequireKind(action, "rest_upgrade");
        if (!CanRestUpgrade(state.Player))
        {
            throw new InvalidOperationException(
                "A relic prevents upgrading cards at rest sites.");
        }

        var payload = action.ReadPayload<UpgradeCardPayload>();
        var found = false;
        var deck = state.Player.Deck
            .Select(card =>
            {
                if (card.InstanceId != payload.CardInstanceId)
                {
                    return card;
                }

                if (card.UpgradeLevel != 0)
                {
                    throw new InvalidOperationException("Prototype cards may currently be upgraded once.");
                }

                found = true;
                return card with { UpgradeLevel = 1 };
            })
            .ToArray();

        if (!found)
        {
            throw new InvalidOperationException($"Card instance {payload.CardInstanceId} is missing.");
        }

        state = state with { Player = state.Player with { Deck = deck } };
        return CompleteRoomToMap(state);
    }

    private static IReadOnlyList<GameAction> GetRewardActions(RunState state)
    {
        var reward = RequireWorld(state).Reward
            ?? throw new InvalidOperationException("Reward phase has no reward state.");

        if (reward.PendingDeckChoice is { } deckChoice)
        {
            return deckChoice.CandidateCardInstanceIds
                .Select(cardInstanceId =>
                    GameAction.Create(
                        "choose_relic_deck_card",
                        new ChooseDeckCardPayload(
                            cardInstanceId)))
                .ToArray();
        }

        if (!reward.CardResolved)
        {
            var actions = reward.CardOptions
                .Select((_, index) => GameAction.Create(
                    "take_reward_card",
                    new ChooseCardPayload(index)))
                .ToList();
            actions.Add(GameAction.Empty("skip_reward_card"));
            return actions;
        }

        var extraCardOptions =
            reward.ExtraCardOptions
            ?? Array.Empty<string[]>();
        if (reward.ExtraCardRewardsResolved
            < extraCardOptions.Length)
        {
            var currentOptions =
                extraCardOptions[
                    reward.ExtraCardRewardsResolved];
            var actions = currentOptions
                .Select((_, index) => GameAction.Create(
                    "take_reward_card",
                    new ChooseCardPayload(index)))
                .ToList();
            actions.Add(
                GameAction.Empty("skip_reward_card"));
            return actions;
        }

        if (!reward.PotionResolved)
        {
            var actions = new List<GameAction>();
            if (CanAcquirePotion(state.Player))
            {
                var emptySlot = Array.IndexOf(
                    state.Player.PotionSlots,
                    null);
                if (emptySlot >= 0)
                {
                    actions.Add(
                        GameAction.Empty(
                            "take_reward_potion"));
                }
                else
                {
                    actions.AddRange(
                        state.Player.PotionSlots
                            .Select((item, slot) =>
                                (Potion: item, Slot: slot))
                            .Where(item =>
                                item.Potion is not null)
                            .Select(item =>
                                GameAction.Create(
                                    "replace_reward_potion",
                                    new ReplaceRewardPotionPayload(
                                        item.Slot))));
                }
            }

            actions.Add(GameAction.Empty("skip_reward_potion"));
            return actions;
        }

        if (!reward.RelicResolved)
        {
            var options = reward.CurrentRelicOptions;
            if (options.Length == 0)
            {
                throw new InvalidOperationException(
                    "Unresolved relic reward has no options.");
            }

            return options
                .Select((_, index) => GameAction.Create(
                    "take_reward_relic",
                    new ChooseRelicPayload(index)))
                .ToArray();
        }

        return [GameAction.Empty("leave_reward")];
    }

    private static RunState StepReward(RunState state, GameAction action)
    {
        var world = RequireWorld(state);
        var reward = world.Reward
            ?? throw new InvalidOperationException("Reward phase has no reward state.");
        var player = state.Player;
        var nextId = world.NextCardInstanceId;

        if (reward.PendingDeckChoice is { } deckChoice)
        {
            RequireKind(
                action,
                "choose_relic_deck_card");
            var payload =
                action.ReadPayload<ChooseDeckCardPayload>();
            if (!deckChoice.CandidateCardInstanceIds
                    .Contains(payload.CardInstanceId))
            {
                throw new InvalidOperationException(
                    $"Card instance {payload.CardInstanceId} is not eligible for the relic deck choice.");
            }

            var selectedCard =
                player.Deck.FirstOrDefault(card =>
                    card.InstanceId
                        == payload.CardInstanceId)
                ?? throw new InvalidOperationException(
                    $"Card instance {payload.CardInstanceId} is missing.");

            if (PrototypeContent.Card(
                    selectedCard.CardId).Eternal)
            {
                throw new InvalidOperationException(
                    $"Eternal card {payload.CardInstanceId} cannot be changed by this relic.");
            }

            player = deckChoice.Kind switch
            {
                PrototypePersistentDeckChoiceKind.Remove =>
                    player with
                    {
                        Deck = player.Deck
                            .Where(card =>
                                card.InstanceId
                                    != payload.CardInstanceId)
                            .ToArray()
                    },
                PrototypePersistentDeckChoiceKind.Transform =>
                    TransformPersistentDeckCard(
                        player,
                        payload.CardInstanceId,
                        deckChoice.UpgradeTransformedCards,
                        state.Rng),
                _ => throw new ArgumentOutOfRangeException()
            };

            var remainingCandidates =
                deckChoice.CandidateCardInstanceIds
                    .Where(id =>
                        id != payload.CardInstanceId)
                    .Where(id =>
                        player.Deck.Any(card =>
                            card.InstanceId == id))
                    .ToArray();
            var remainingSelections =
                deckChoice.RemainingSelections - 1;
            reward = reward with
            {
                PendingDeckChoice =
                    remainingSelections > 0
                    && remainingCandidates.Length > 0
                        ? deckChoice with
                        {
                            RemainingSelections =
                                remainingSelections,
                            CandidateCardInstanceIds =
                                remainingCandidates
                        }
                        : null
            };
        }
        else if (!reward.CardResolved)
        {
            if (StringComparer.Ordinal.Equals(action.Kind, "take_reward_card"))
            {
                var payload = action.ReadPayload<ChooseCardPayload>();
                if (payload.Index < 0 || payload.Index >= reward.CardOptions.Length)
                {
                    throw new InvalidOperationException($"Reward card index {payload.Index} is invalid.");
                }

                player = AppendCard(player, nextId++, reward.CardOptions[payload.Index]);
            }
            else
            {
                RequireKind(action, "skip_reward_card");
            }

            reward = reward with { CardResolved = true };
        }
        else if (reward.ExtraCardRewardsResolved
                 < (reward.ExtraCardOptions?.Length ?? 0))
        {
            var extraCardOptions =
                reward.ExtraCardOptions
                ?? throw new InvalidOperationException(
                    "Extra card reward state is missing.");
            var currentOptions =
                extraCardOptions[
                    reward.ExtraCardRewardsResolved];

            if (StringComparer.Ordinal.Equals(
                    action.Kind,
                    "take_reward_card"))
            {
                var payload =
                    action.ReadPayload<ChooseCardPayload>();
                if (payload.Index < 0
                    || payload.Index >= currentOptions.Length)
                {
                    throw new InvalidOperationException(
                        $"Extra reward card index {payload.Index} is invalid.");
                }

                player = AppendCard(
                    player,
                    nextId++,
                    currentOptions[payload.Index]);
            }
            else
            {
                RequireKind(action, "skip_reward_card");
            }

            reward = reward with
            {
                ExtraCardRewardsResolved =
                    reward.ExtraCardRewardsResolved + 1
            };
        }
        else if (!reward.PotionResolved)
        {
            if (StringComparer.Ordinal.Equals(
                    action.Kind,
                    "take_reward_potion"))
            {
                if (reward.PotionOption is null)
                {
                    throw new InvalidOperationException(
                        "Reward has no potion.");
                }

                if (!CanAcquirePotion(player))
                {
                    throw new InvalidOperationException(
                        "Player cannot acquire potions.");
                }

                var slot = Array.IndexOf(
                    player.PotionSlots,
                    null);
                if (slot < 0)
                {
                    throw new InvalidOperationException(
                        "No potion slot is available.");
                }

                var slots =
                    (PotionInstance?[])
                    player.PotionSlots.Clone();
                slots[slot] =
                    new PotionInstance(
                        reward.PotionOption,
                        PrototypeJson.EmptyObject());
                player = player with
                {
                    PotionSlots = slots
                };
            }
            else if (StringComparer.Ordinal.Equals(
                         action.Kind,
                         "replace_reward_potion"))
            {
                if (reward.PotionOption is null)
                {
                    throw new InvalidOperationException(
                        "Reward has no potion.");
                }

                if (!CanAcquirePotion(player))
                {
                    throw new InvalidOperationException(
                        "Player cannot acquire potions.");
                }

                if (Array.IndexOf(
                        player.PotionSlots,
                        null) >= 0)
                {
                    throw new InvalidOperationException(
                        "Potion replacement is only needed when all slots are occupied.");
                }

                var payload =
                    action.ReadPayload<
                        ReplaceRewardPotionPayload>();
                player = player with
                {
                    PotionSlots = ReplacePotionSlot(
                        player.PotionSlots,
                        payload.Slot,
                        reward.PotionOption)
                };
            }
            else
            {
                RequireKind(
                    action,
                    "skip_reward_potion");
            }

            reward = reward with
            {
                PotionResolved = true
            };
        }
        else if (!reward.RelicResolved)
        {
            RequireKind(action, "take_reward_relic");
            var options = reward.CurrentRelicOptions;
            if (options.Length == 0)
            {
                throw new InvalidOperationException(
                    "Reward has no relic options.");
            }

            var payload =
                action.ReadPayload<ChooseRelicPayload>();
            if (payload.Index < 0
                || payload.Index >= options.Length)
            {
                throw new InvalidOperationException(
                    $"Reward relic index {payload.Index} is invalid.");
            }

            var relicId = options[payload.Index];
            player = player with
            {
                Relics = player.Relics.Append(
                    new RelicInstance(
                        relicId,
                        PrototypeJson.EmptyObject()))
                    .ToArray()
            };
            player = ApplyRelicRunEvent(
                player,
                PrototypeRunEventKind.RelicAcquired,
                acquiredRelicId: relicId,
                rng: state.Rng);
            var acquisitionDeckChoice =
                CreateRelicDeckChoice(
                    player,
                    relicId);
            reward = reward with
            {
                RelicResolved = true,
                PendingDeckChoice =
                    acquisitionDeckChoice
            };
        }
        else
        {
            RequireKind(action, "leave_reward");
            state = state with
            {
                Player = player,
                World = world with
                {
                    Reward = reward,
                    NextCardInstanceId = nextId
                }
            };
            return CompleteReward(state);
        }

        world = world with
        {
            Reward = reward,
            NextCardInstanceId = nextId
        };

        return state with
        {
            Player = player,
            World = world
        };
    }

    private static RunState EnterReward(RunState state)
    {
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException(
                "Combat reward has no combat state.");
        var room = world.ActiveRoom
            ?? throw new InvalidOperationException("Combat reward has no active room.");

        var gold = room switch
        {
            PrototypeRoomType.Elite => 40 + (world.Act * 5),
            PrototypeRoomType.Boss => 60 + (world.Act * 5),
            _ => 20 + (world.Act * 5)
        };

        var cardChoiceCount =
            RewardCardChoiceCount(state.Player);
        var cardOptions = PickRewardCards(
            world.Act,
            cardChoiceCount,
            state.Rng);
        var extraCardRewardGroups =
            Math.Max(
                0,
                combat.ExtraCardRewardsEarned)
            + (room == PrototypeRoomType.Combat
                ? ExtraNormalCombatCardRewardGroups(
                    state.Player)
                : 0);
        var extraCardOptions = Enumerable.Range(
                0,
                extraCardRewardGroups)
            .Select(_ => PickRewardCards(
                world.Act,
                cardChoiceCount,
                state.Rng))
            .ToArray();

        var potion = CanAcquirePotion(state.Player)
            && PrototypeRng.NextBool(
                state.Rng,
                "reward",
                1,
                2)
            ? PrototypeContent.PotionPool[
                PrototypeRng.NextInt(
                    state.Rng,
                    "reward",
                    PrototypeContent.PotionPool.Length)]
            : null;

        var relic = room == PrototypeRoomType.Elite
            ? PrototypeContent.RelicPool[
                PrototypeRng.NextInt(
                    state.Rng,
                    "reward",
                    PrototypeContent.RelicPool.Length)]
            : null;
        var relicOptions =
            room == PrototypeRoomType.Boss
                ? PickDistinct(
                    PrototypeContent.BossRelicPool
                        .Where(id =>
                            !state.Player.Relics.Any(relic =>
                                StringComparer.Ordinal.Equals(
                                    relic.RelicId,
                                    id)))
                        .ToArray(),
                    3,
                    state.Rng,
                    "reward")
                : null;

        var reward = new RewardState(
            SourceRoom: room.ToString(),
            CardOptions: cardOptions,
            PotionOption: potion,
            RelicOption: relic,
            CardResolved: false,
            PotionResolved: potion is null,
            RelicResolved:
                relic is null
                && (relicOptions is null
                    || relicOptions.Length == 0),
            EndsAct: room == PrototypeRoomType.Boss,
            ExtraCardOptions: extraCardOptions,
            RelicOptions: relicOptions);

        world = world with
        {
            Combat = null,
            Reward = reward
        };

        return state with
        {
            Player = state.Player with { Gold = state.Player.Gold + gold },
            World = world,
            Phase = RunPhase.Reward
        };
    }

    private static RunState CompleteReward(RunState state)
    {
        var world = RequireWorld(state);
        var reward = world.Reward
            ?? throw new InvalidOperationException("Reward phase has no reward state.");

        world = world with { Reward = null };
        state = state with { World = world };

        if (!reward.EndsAct)
        {
            return CompleteRoomToMap(state);
        }

        if (world.Act >= PrototypeContent.Rules.Acts)
        {
            return EndRun(state, "victory");
        }

        return state with { Phase = RunPhase.ActTransition };
    }

    private static RunState CompleteRoomToMap(RunState state)
    {
        var world = RequireWorld(state) with
        {
            ActiveRoom = null,
            Combat = null,
            Reward = null,
            Shop = null,
            Event = null
        };

        if (world.Map.AvailableNodes().Length == 0)
        {
            throw new InvalidOperationException(
                "Completed non-boss room has no reachable next map node.");
        }

        return state with
        {
            World = world,
            Phase = RunPhase.MapChoice
        };
    }

    private static string[] PickShopCards(
        int act,
        int count,
        RngBundle rng)
    {
        var available = PrototypeContent.RewardCardPool.ToList();
        var selected = new List<string>(Math.Min(count, available.Count));

        while (selected.Count < count && available.Count > 0)
        {
            var weights = PrototypeContent.ShopRarityWeights(act)
                .Where(item =>
                    item.Weight > 0
                    && available.Any(cardId =>
                        PrototypeContent.Card(cardId).Rarity == item.Rarity))
                .ToArray();
            if (weights.Length == 0)
            {
                break;
            }

            var roll = PrototypeRng.NextInt(
                rng,
                "shop",
                weights.Sum(item => item.Weight));
            var rarity = weights[^1].Rarity;
            foreach (var item in weights)
            {
                if (roll < item.Weight)
                {
                    rarity = item.Rarity;
                    break;
                }

                roll -= item.Weight;
            }

            var candidates = available
                .Where(cardId => PrototypeContent.Card(cardId).Rarity == rarity)
                .ToArray();
            var selectedId = candidates[
                PrototypeRng.NextInt(rng, "shop", candidates.Length)];
            selected.Add(selectedId);
            available.Remove(selectedId);
        }

        return selected.ToArray();
    }

    private static int ShopCardPrice(string cardId, RngBundle rng)
    {
        var rarity = PrototypeContent.Card(cardId).Rarity;
        var (minimum, spread) = rarity switch
        {
            PrototypeCardRarity.Common => (45, 16),
            PrototypeCardRarity.Uncommon => (65, 21),
            PrototypeCardRarity.Rare => (100, 31),
            _ => throw new InvalidOperationException(
                $"Basic card '{cardId}' cannot be a normal shop offer.")
        };

        return minimum + PrototypeRng.NextInt(rng, "shop", spread);
    }

    private static string[] PickRewardCards(
        int act,
        int count,
        RngBundle rng)
    {
        var available = PrototypeContent.RewardCardPool.ToList();
        var selected = new List<string>(Math.Min(count, available.Count));

        while (selected.Count < count && available.Count > 0)
        {
            var rarityWeights = PrototypeContent.RewardRarityWeights(act)
                .Where(item =>
                    item.Weight > 0
                    && available.Any(cardId =>
                        PrototypeContent.Card(cardId).Rarity == item.Rarity))
                .ToArray();

            if (rarityWeights.Length == 0)
            {
                break;
            }

            var totalWeight = rarityWeights.Sum(item => item.Weight);
            var roll = PrototypeRng.NextInt(rng, "reward", totalWeight);
            var rarity = rarityWeights[^1].Rarity;
            foreach (var item in rarityWeights)
            {
                if (roll < item.Weight)
                {
                    rarity = item.Rarity;
                    break;
                }

                roll -= item.Weight;
            }

            var candidates = available
                .Where(cardId => PrototypeContent.Card(cardId).Rarity == rarity)
                .ToArray();
            var selectedId = candidates[
                PrototypeRng.NextInt(rng, "reward", candidates.Length)];

            selected.Add(selectedId);
            available.Remove(selectedId);
        }

        return selected.ToArray();
    }

    private static PrototypePendingDeckChoiceState?
        CreateRelicDeckChoice(
            PlayerState player,
            string relicId)
    {
        var spec =
            PrototypeContent.Relic(relicId)
                .AcquisitionDeckChoice;
        if (spec is null || spec.Selections <= 0)
        {
            return null;
        }

        var candidates = player.Deck
            .Where(card =>
                !PrototypeContent.Card(card.CardId)
                    .Eternal)
            .Select(card => card.InstanceId)
            .ToArray();
        var selections =
            Math.Min(spec.Selections, candidates.Length);
        return selections <= 0
            ? null
            : new PrototypePendingDeckChoiceState(
                relicId,
                spec.Kind,
                selections,
                candidates,
                spec.UpgradeTransformedCards);
    }

    private static PlayerState TransformPersistentDeckCard(
        PlayerState player,
        long cardInstanceId,
        bool upgradeResult,
        RngBundle rng)
    {
        var original = player.Deck
            .Single(card =>
                card.InstanceId == cardInstanceId);
        var pool = PrototypeContent.RewardCardPool
            .Where(cardId =>
                !StringComparer.Ordinal.Equals(
                    cardId,
                    original.CardId))
            .ToArray();
        if (pool.Length == 0)
        {
            throw new InvalidOperationException(
                "No prototype card is available for transformation.");
        }

        var transformedCardId =
            pool[PrototypeRng.NextInt(
                rng,
                "reward",
                pool.Length)];
        return player with
        {
            Deck = player.Deck
                .Select(card =>
                    card.InstanceId != cardInstanceId
                        ? card
                        : new CardInstance(
                            card.InstanceId,
                            transformedCardId,
                            upgradeResult ? 1 : 0,
                            PrototypeJson.EmptyObject()))
                .ToArray()
        };
    }

    private static string[] PickDistinct(
        string[] pool,
        int count,
        RngBundle rng,
        string streamId)
    {
        if (pool.Length == 0)
        {
            return Array.Empty<string>();
        }

        var copy = (string[])pool.Clone();
        PrototypeRng.Shuffle(rng, streamId, copy);
        return copy.Take(Math.Min(count, copy.Length)).ToArray();
    }
}
