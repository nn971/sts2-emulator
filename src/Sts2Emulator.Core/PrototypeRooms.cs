namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static IReadOnlyList<GameAction> GetMapActions(RunState state)
    {
        var world = RequireWorld(state);
        return world.Map.Options
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
        var node = world.Map.Options.FirstOrDefault(
            option => StringComparer.Ordinal.Equals(option.NodeId, payload.NodeId))
            ?? throw new InvalidOperationException($"Unknown map node '{payload.NodeId}'.");

        world = world with
        {
            Floor = node.Floor,
            ActiveRoom = node.RoomType,
            Map = new MapState(Array.Empty<MapNodeState>()),
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

    private static MapState GenerateMapOptions(RunWorldState world, RngBundle rng)
    {
        var rules = PrototypeContent.Rules;
        var nextFloor = world.Floor + 1;
        if (nextFloor > rules.FloorsPerAct)
        {
            return new MapState(Array.Empty<MapNodeState>());
        }

        if (nextFloor == rules.FloorsPerAct)
        {
            return new MapState(
            [
                new MapNodeState(
                    $"{world.Act}:{nextFloor}:boss",
                    world.Act,
                    nextFloor,
                    PrototypeRoomType.Boss)
            ]);
        }

        var selected = new List<PrototypeRoomType>(2);
        var attempts = 0;
        while (selected.Count < 2 && attempts++ < 32)
        {
            var room = rules.RoomPool[
                PrototypeRng.NextInt(rng, "map", rules.RoomPool.Length)];

            if (!selected.Contains(room))
            {
                selected.Add(room);
            }
        }

        if (selected.Count == 0)
        {
            selected.Add(PrototypeRoomType.Combat);
        }

        return new MapState(
            selected
                .Select((room, index) => new MapNodeState(
                    $"{world.Act}:{nextFloor}:{index}:{room}",
                    world.Act,
                    nextFloor,
                    room))
                .ToArray());
    }

    private static RunState StartEvent(RunState state)
    {
        var ids = PrototypeContent.Events.Keys.Order(StringComparer.Ordinal).ToArray();
        var id = ids[PrototypeRng.NextInt(state.Rng, "event", ids.Length)];
        var world = RequireWorld(state) with { Event = new EventState(id) };

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
                    player = player with { Hp = player.Hp - effect.Amount };
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
        var cardIds = PickDistinct(
            PrototypeContent.RewardCardPool,
            3,
            state.Rng,
            "shop");

        var offers = cardIds
            .Select((cardId, index) => new ShopOffer(
                index + 1,
                cardId,
                45 + PrototypeRng.NextInt(state.Rng, "shop", 31),
                false))
            .ToArray();

        var potionId = PrototypeContent.PotionPool[
            PrototypeRng.NextInt(state.Rng, "shop", PrototypeContent.PotionPool.Length)];
        var relicId = PrototypeContent.RelicPool[
            PrototypeRng.NextInt(state.Rng, "shop", PrototypeContent.RelicPool.Length)];

        var shop = new ShopState(
            offers,
            new ShopOffer(100, potionId, 45, false),
            new ShopOffer(200, relicId, 120, false));

        world = world with { Shop = shop };

        return state with
        {
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
            && Array.IndexOf(state.Player.PotionSlots, null) >= 0)
        {
            actions.Add(GameAction.Create("buy_potion", new BuyOfferPayload(potion.OfferId)));
        }

        if (shop.RelicOffer is { Sold: false } relic && relic.Price <= state.Player.Gold)
        {
            actions.Add(GameAction.Create("buy_relic", new BuyOfferPayload(relic.OfferId)));
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

        var payload = action.ReadPayload<BuyOfferPayload>();
        var player = state.Player;
        var nextId = world.NextCardInstanceId;

        if (StringComparer.Ordinal.Equals(action.Kind, "buy_card"))
        {
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
            var offer = shop.PotionOffer
                ?? throw new InvalidOperationException("Shop has no potion offer.");
            if (offer.OfferId != payload.OfferId)
            {
                throw new InvalidOperationException($"Unknown potion offer {payload.OfferId}.");
            }

            EnsurePurchasable(player, offer);
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
        else if (StringComparer.Ordinal.Equals(action.Kind, "buy_relic"))
        {
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
            shop = shop with { RelicOffer = offer with { Sold = true } };
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
        var actions = new List<GameAction> { GameAction.Empty("rest_heal") };
        actions.AddRange(
            state.Player.Deck
                .Where(card => card.UpgradeLevel == 0)
                .Select(card => GameAction.Create(
                    "rest_upgrade",
                    new UpgradeCardPayload(card.InstanceId))));
        return actions;
    }

    private static RunState StepRest(RunState state, GameAction action)
    {
        if (StringComparer.Ordinal.Equals(action.Kind, "rest_heal"))
        {
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

        RequireKind(action, "rest_upgrade");
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

        if (!reward.PotionResolved)
        {
            var actions = new List<GameAction>();
            if (Array.IndexOf(state.Player.PotionSlots, null) >= 0)
            {
                actions.Add(GameAction.Empty("take_reward_potion"));
            }

            actions.Add(GameAction.Empty("skip_reward_potion"));
            return actions;
        }

        if (!reward.RelicResolved)
        {
            return [GameAction.Empty("take_reward_relic")];
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

        if (!reward.CardResolved)
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
        else if (!reward.PotionResolved)
        {
            if (StringComparer.Ordinal.Equals(action.Kind, "take_reward_potion"))
            {
                if (reward.PotionOption is null)
                {
                    throw new InvalidOperationException("Reward has no potion.");
                }

                var slot = Array.IndexOf(player.PotionSlots, null);
                if (slot < 0)
                {
                    throw new InvalidOperationException("No potion slot is available.");
                }

                var slots = (PotionInstance?[])player.PotionSlots.Clone();
                slots[slot] = new PotionInstance(reward.PotionOption, PrototypeJson.EmptyObject());
                player = player with { PotionSlots = slots };
            }
            else
            {
                RequireKind(action, "skip_reward_potion");
            }

            reward = reward with { PotionResolved = true };
        }
        else if (!reward.RelicResolved)
        {
            RequireKind(action, "take_reward_relic");
            if (reward.RelicOption is null)
            {
                throw new InvalidOperationException("Reward has no relic.");
            }

            player = player with
            {
                Relics = player.Relics.Append(
                    new RelicInstance(reward.RelicOption, PrototypeJson.EmptyObject())).ToArray()
            };
            reward = reward with { RelicResolved = true };
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
        var room = world.ActiveRoom
            ?? throw new InvalidOperationException("Combat reward has no active room.");

        var gold = room switch
        {
            PrototypeRoomType.Elite => 40 + (world.Act * 5),
            PrototypeRoomType.Boss => 60 + (world.Act * 5),
            _ => 20 + (world.Act * 5)
        };

        var cardOptions = PickDistinct(
            PrototypeContent.RewardCardPool,
            3,
            state.Rng,
            "reward");

        var potion = PrototypeRng.NextBool(state.Rng, "reward", 1, 2)
            ? PrototypeContent.PotionPool[
                PrototypeRng.NextInt(state.Rng, "reward", PrototypeContent.PotionPool.Length)]
            : null;

        var relic = room is PrototypeRoomType.Elite or PrototypeRoomType.Boss
            ? PrototypeContent.RelicPool[
                PrototypeRng.NextInt(state.Rng, "reward", PrototypeContent.RelicPool.Length)]
            : null;

        var reward = new RewardState(
            SourceRoom: room.ToString(),
            CardOptions: cardOptions,
            PotionOption: potion,
            RelicOption: relic,
            CardResolved: false,
            PotionResolved: potion is null,
            RelicResolved: relic is null,
            EndsAct: room == PrototypeRoomType.Boss);

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
        world = world with { Map = GenerateMapOptions(world, state.Rng) };

        return state with
        {
            World = world,
            Phase = RunPhase.MapChoice
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
