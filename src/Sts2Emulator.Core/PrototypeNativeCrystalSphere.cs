namespace Sts2Emulator.Core;

/// <summary>
/// Source-shaped Act 2/3 Crystal Sphere event. The minigame board and each
/// discovered item persist in EventState so action-by-action runs remain
/// forkable and serializable.
/// </summary>
public static class PrototypeNativeCrystalSphere
{
    public const string EventId = "proto.native.event.crystal_sphere";
    public const int GridSize = 11;
    public const string UncoverFutureChoiceId = "uncover_future";
    public const string PaymentPlanChoiceId = "payment_plan";

    public static string[] CoveredBranches { get; } =
    [
        "initial/PAYMENT_PLAN",
        "initial/UNCOVER_FUTURE",
        "page/FINISH",
        "page/INITIAL",
        "page/INITIAL/PAYMENT_PLAN",
        "page/INITIAL/UNCOVER_FUTURE",
        "page/PAYMENT_PLAN",
        "page/UNCOVER_FUTURE"
    ];

    public static PrototypeEventDefinition Definition { get; } = new(
        EventId,
        "Crystal Sphere",
        [
            new(UncoverFutureChoiceId,
                "Pay the displayed gold cost to uncover the future",
                [new(PrototypeRunEffectKind.LoseGold, 50)]),
            new(PaymentPlanChoiceId,
                "Add Debt to uncover more of the future",
                [new(PrototypeRunEffectKind.AddCard,
                    CardId: NativeGloryAncientsV111.DebtCardId)])
        ],
        MinAct: 2,
        MaxAct: 3);

    internal static PrototypeCrystalSphereState CreateBoard(
        int divinations, RngBundle rng)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var cleared = CreateInitialClearedCells();
            var itemByCell = Enumerable.Repeat(-1, GridSize * GridSize)
                .ToArray();
            var items = CreateItems();
            var failed = false;

            for (var itemId = 0; itemId < items.Length; itemId++)
            {
                var item = items[itemId];
                var candidates = new List<(int X, int Y)>();
                for (var x = 0; x < GridSize; x++)
                for (var y = 0; y < GridSize; y++)
                {
                    if (CanPlace(itemByCell, cleared, x, y,
                            item.Width, item.Height))
                        candidates.Add((x, y));
                }

                if (candidates.Count == 0)
                {
                    failed = true;
                    break;
                }

                var position = candidates[PrototypeRng.NextInt(
                    rng, "event", candidates.Count)];
                items[itemId] = item with
                {
                    X = position.X,
                    Y = position.Y
                };
                for (var x = 0; x < item.Width; x++)
                for (var y = 0; y < item.Height; y++)
                {
                    itemByCell[CellIndex(position.X + x, position.Y + y)] =
                        itemId;
                }
            }

            if (!failed)
            {
                return new PrototypeCrystalSphereState(
                    divinations,
                    SmallToolSelected: false,
                    itemByCell,
                    cleared,
                    items,
                    new bool[items.Length],
                    []);
            }
        }

        throw new InvalidOperationException(
            "Crystal Sphere could not place its source item set.");
    }

    internal static IReadOnlyList<(int X, int Y)> CellsClearedByPick(
        int x, int y, bool smallToolSelected)
    {
        if (smallToolSelected)
            return [(x, y)];

        var cells = new List<(int X, int Y)>(9);
        Add(x - 1, y);
        Add(x + 1, y);
        Add(x, y - 1);
        Add(x, y + 1);
        foreach (var dx in new[] { -1, 1 })
        foreach (var dy in new[] { -1, 1 })
            Add(x + dx, y + dy);
        Add(x, y);
        return cells;

        void Add(int cellX, int cellY)
        {
            if (cellX >= 0 && cellX < GridSize
                && cellY >= 0 && cellY < GridSize)
                cells.Add((cellX, cellY));
        }
    }

    internal static int CellIndex(int x, int y) => x * GridSize + y;

    private static bool[] CreateInitialClearedCells()
    {
        var cleared = new bool[GridSize * GridSize];
        var corners = new[]
        {
            (X: 0, Y: 0),
            (X: GridSize - 1, Y: 0),
            (X: GridSize - 1, Y: GridSize - 1),
            (X: 0, Y: GridSize - 1)
        };
        foreach (var corner in corners)
        for (var x = 0; x < GridSize; x++)
        for (var y = 0; y < GridSize; y++)
        {
            if (Math.Abs(x - corner.X) + Math.Abs(y - corner.Y) <= 2)
                cleared[CellIndex(x, y)] = true;
        }
        return cleared;
    }

    private static PrototypeCrystalSphereItemState[] CreateItems() =>
    [
        new("relic", 0, 0, 4, 4),
        new("potion_common", 0, 0, 1, 3),
        new("potion_common", 0, 0, 1, 3),
        new("potion_rare", 0, 0, 2, 2),
        new("card_common", 0, 0, 2, 2),
        new("card_uncommon", 0, 0, 2, 2),
        new("card_rare", 0, 0, 2, 2),
        new("curse", 0, 0, 2, 2),
        new("gold_small", 0, 0, 1, 1),
        new("gold_small", 0, 0, 1, 1),
        new("gold_small", 0, 0, 1, 1),
        new("gold_small", 0, 0, 1, 1),
        new("gold_small", 0, 0, 1, 1),
        new("gold_large", 0, 0, 2, 1),
        new("gold_large", 0, 0, 2, 1)
    ];

    private static bool CanPlace(
        int[] itemByCell,
        bool[] cleared,
        int x,
        int y,
        int width,
        int height)
    {
        if (x + width > GridSize || y + height > GridSize)
            return false;
        for (var offsetX = 0; offsetX < width; offsetX++)
        for (var offsetY = 0; offsetY < height; offsetY++)
        {
            var index = CellIndex(x + offsetX, y + offsetY);
            if (cleared[index] || itemByCell[index] >= 0)
                return false;
        }
        return true;
    }
}

public sealed partial class PrototypeGameEngine
{
    private static IReadOnlyList<GameAction> GetNativeCrystalSphereActions(
        EventState eventState)
    {
        if (eventState.NativePageIndex == 0
            && eventState.NativeCrystalSphere is null)
        {
            return
            [
                GameAction.Create("event_choice",
                    new EventChoicePayload(
                        PrototypeNativeCrystalSphere.UncoverFutureChoiceId)),
                GameAction.Create("event_choice",
                    new EventChoicePayload(
                        PrototypeNativeCrystalSphere.PaymentPlanChoiceId))
            ];
        }

        if (eventState.NativePageIndex != 1
            || eventState.NativeCrystalSphere is not { } board)
        {
            throw new InvalidOperationException(
                "Crystal Sphere has no minigame in progress.");
        }

        var actions = new List<GameAction>
        {
            GameAction.Create("crystal_sphere_set_tool",
                new SetCrystalSphereToolPayload(SmallToolSelected: false)),
            GameAction.Create("crystal_sphere_set_tool",
                new SetCrystalSphereToolPayload(SmallToolSelected: true))
        };
        for (var x = 0; x < PrototypeNativeCrystalSphere.GridSize; x++)
        for (var y = 0; y < PrototypeNativeCrystalSphere.GridSize; y++)
        {
            actions.Add(GameAction.Create("crystal_sphere_reveal_cell",
                new ChooseCrystalSphereCellPayload(x, y)));
        }
        return actions;
    }

    private static RunState StepNativeCrystalSphere(
        RunState state, GameAction action)
    {
        var world = RequireWorld(state);
        var eventState = world.Event
            ?? throw new InvalidOperationException(
                "Crystal Sphere has no active event.");
        if (world.Act is not (2 or 3)
            || eventState.EventId != PrototypeNativeCrystalSphere.EventId)
        {
            throw new InvalidOperationException(
                "Crystal Sphere requires an Act 2 or Act 3 event fixture.");
        }

        if (eventState.NativePageIndex == 0
            && eventState.NativeCrystalSphere is null)
        {
            RequireKind(action, "event_choice");
            var choiceId = action.ReadPayload<EventChoicePayload>().ChoiceId;
            if (state.Player.Gold < 100)
                throw new InvalidOperationException(
                    "Crystal Sphere requires at least 100 gold on entry.");

            var player = state.Player;
            var nextCardId = world.NextCardInstanceId;
            var divinations = choiceId switch
            {
                PrototypeNativeCrystalSphere.UncoverFutureChoiceId => 3,
                PrototypeNativeCrystalSphere.PaymentPlanChoiceId => 6,
                _ => throw new InvalidOperationException(
                    $"Unknown Crystal Sphere choice '{choiceId}'.")
            };
            if (choiceId ==
                PrototypeNativeCrystalSphere.UncoverFutureChoiceId)
            {
                if (eventState.NativeEventGold <= 0
                    || player.Gold < eventState.NativeEventGold)
                    throw new InvalidOperationException(
                        "Crystal Sphere's gold payment is unavailable.");
                player = player with
                {
                    Gold = player.Gold - eventState.NativeEventGold
                };
            }
            else
            {
                player = AppendCard(
                    player,
                    nextCardId++,
                    NativeGloryAncientsV111.DebtCardId,
                    state.Rng,
                    allocateCardId: () => nextCardId++);
            }

            return state with
            {
                Player = player,
                World = world with
                {
                    NextCardInstanceId = nextCardId,
                    Event = eventState with
                    {
                        ChosenChoiceId = choiceId,
                        NativePageIndex = 1,
                        NativeCrystalSphere =
                            PrototypeNativeCrystalSphere.CreateBoard(
                                divinations, state.Rng)
                    }
                }
            };
        }

        if (eventState.NativePageIndex != 1
            || eventState.NativeCrystalSphere is not { } currentBoard)
            throw new InvalidOperationException(
                "Crystal Sphere has no active board.");

        if (action.Kind == "crystal_sphere_set_tool")
        {
            var payload = action.ReadPayload<SetCrystalSphereToolPayload>();
            return state with
            {
                World = world with
                {
                    Event = eventState with
                    {
                        NativeCrystalSphere = currentBoard with
                        {
                            SmallToolSelected = payload.SmallToolSelected
                        }
                    }
                }
            };
        }

        RequireKind(action, "crystal_sphere_reveal_cell");
        var cell = action.ReadPayload<ChooseCrystalSphereCellPayload>();
        if ((uint)cell.X >= PrototypeNativeCrystalSphere.GridSize
            || (uint)cell.Y >= PrototypeNativeCrystalSphere.GridSize)
            throw new InvalidOperationException(
                "Crystal Sphere cell is outside the board.");

        var clearedCells = (bool[])currentBoard.ClearedCells.Clone();
        var revealedItems = (bool[])currentBoard.RevealedItems.Clone();
        var revealedOrder = currentBoard.RevealedItemOrder.ToList();
        var playerAfterPick = state.Player;
        var nextId = world.NextCardInstanceId;
        foreach (var (x, y) in
                 PrototypeNativeCrystalSphere.CellsClearedByPick(
                     cell.X, cell.Y, currentBoard.SmallToolSelected))
        {
            clearedCells[PrototypeNativeCrystalSphere.CellIndex(x, y)] = true;
            for (var itemId = 0;
                 itemId < currentBoard.Items.Length;
                 itemId++)
            {
                if (revealedItems[itemId])
                    continue;
                var item = currentBoard.Items[itemId];
                var completed = true;
                for (var offsetX = 0; offsetX < item.Width; offsetX++)
                for (var offsetY = 0; offsetY < item.Height; offsetY++)
                {
                    if (!clearedCells[
                            PrototypeNativeCrystalSphere.CellIndex(
                                item.X + offsetX, item.Y + offsetY)])
                        completed = false;
                }
                if (!completed)
                    continue;

                revealedItems[itemId] = true;
                revealedOrder.Add(itemId);
                if (item.Kind == "curse")
                {
                    playerAfterPick = AppendCard(
                        playerAfterPick,
                        nextId++,
                        PrototypeNativeGloryEvents.DoubtId,
                        state.Rng,
                        allocateCardId: () => nextId++);
                }
            }
        }

        var remaining = currentBoard.DivinationsRemaining - 1;
        var updatedBoard = currentBoard with
        {
            DivinationsRemaining = remaining,
            ClearedCells = clearedCells,
            RevealedItems = revealedItems,
            RevealedItemOrder = revealedOrder.ToArray()
        };
        var updatedEvent = eventState with
        {
            NativeCrystalSphere = updatedBoard
        };
        var updatedWorld = world with
        {
            NextCardInstanceId = nextId,
            Event = updatedEvent
        };
        if (remaining > 0)
        {
            return state with
            {
                Player = playerAfterPick,
                World = updatedWorld
            };
        }

        updatedEvent = updatedEvent with
        {
            // SetEventFinished happens after the custom reward continuation
            // completes. AdvanceEventContinuations turns page zero plus the
            // selected choice into the native finished page.
            NativePageIndex = 0,
            NativeCrystalSphere = null
        };
        updatedWorld = updatedWorld with { Event = updatedEvent };
        var rewardResult = CreateCrystalSphereReward(
            state, updatedBoard, playerAfterPick, ref updatedWorld);
        playerAfterPick = rewardResult.Player;
        var reward = rewardResult.Reward;
        if (reward is null)
        {
            return AdvanceEventContinuations(state with
            {
                Player = playerAfterPick,
                World = updatedWorld,
                Phase = RunPhase.Event
            });
        }

        return state with
        {
            Player = playerAfterPick,
            World = updatedWorld with { Reward = reward },
            Phase = RunPhase.Reward
        };
    }

    private static (PlayerState Player, RewardState? Reward)
        CreateCrystalSphereReward(
        RunState state,
        PrototypeCrystalSphereState board,
        PlayerState player,
        ref RunWorldState world)
    {
        var cards = new List<(string[] Options, PrototypeCardRarity Rarity)>();
        var potions = new List<string>();
        var gold = new List<int>();
        string? relic = null;
        foreach (var itemId in board.RevealedItemOrder)
        {
            var item = board.Items[itemId];
            switch (item.Kind)
            {
                case "card_common":
                case "card_uncommon":
                case "card_rare":
                {
                    var rarity = item.Kind switch
                    {
                        "card_common" => PrototypeCardRarity.Common,
                        "card_uncommon" => PrototypeCardRarity.Uncommon,
                        _ => PrototypeCardRarity.Rare
                    };
                    var pool = PrototypeContent.NativeSilentRewardCardPool
                        .Where(id => PrototypeContent.Card(id).Rarity
                            == rarity)
                        .ToArray();
                    if (pool.Length < 3)
                        throw new InvalidOperationException(
                            $"Crystal Sphere has fewer than three supported {rarity} cards.");
                    cards.Add((PickDistinct(pool, 3, state.Rng, "event"),
                        rarity));
                    break;
                }
                case "potion_common":
                case "potion_rare":
                {
                    if (!CanAcquirePotion(player))
                        break;
                    var rarity = item.Kind == "potion_common"
                        ? PrototypeNativePotionRarity.Common
                        : PrototypeNativePotionRarity.Rare;
                    var pool = PrototypeNativePotionShop.NativePotionPoolOrder
                        .Where(id => PrototypeNativePotionShop.TryGetRarity(
                                id, out var itemRarity)
                            && itemRarity == rarity)
                        .ToArray();
                    if (pool.Length == 0)
                        break;
                    potions.Add(pool[PrototypeRng.NextInt(
                        state.Rng, "event", pool.Length)]);
                    break;
                }
                case "relic":
                {
                    if (UsesNativeRegionalSystems(world))
                    {
                        var draw = PrototypeNativeRelicGrabBag.Draw(
                            world, player, state.Rng, merchant: false);
                        world = draw.World;
                        relic = draw.Id;
                    }
                    else
                    {
                        var pool = PrototypeContent.RelicPool
                            .Where(id => !player.Relics.Any(owned =>
                                StringComparer.Ordinal.Equals(
                                    owned.RelicId, id)))
                            .ToArray();
                        if (pool.Length > 0)
                            relic = pool[PrototypeRng.NextInt(
                                state.Rng, "event", pool.Length)];
                    }
                    break;
                }
                case "gold_small":
                    gold.Add(10);
                    break;
                case "gold_large":
                    gold.Add(30);
                    break;
                case "curse":
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown Crystal Sphere item '{item.Kind}'.");
            }
        }

        if (cards.Count == 0 && potions.Count == 0 && relic is null
            && gold.Count == 0)
            return (player, null);

        var crucible = ApplySilverCrucibleToCardRewardGeneration(
            player, cards.Count);
        player = crucible.Player;
        var cardGroups = cards.Select(item => item.Options).ToArray();
        var optionFlags = cards.Count == 0
            ? null
            : cardGroups.Select((options, index) =>
                Enumerable.Repeat(crucible.UpgradeGroups[index],
                    options.Length).ToArray()).ToArray();
        var act = world.Act;
        var generators = cards.Select(item =>
            (PrototypeCardRewardGenerator?)new(
                PrototypeCardRewardGeneratorKind.RarityPool,
                act,
                Rarity: item.Rarity)).ToArray();
        var firstCard = cardGroups.FirstOrDefault() ?? [];
        var extraCards = cardGroups.Skip(1).ToArray();
        var firstOptionsUpgraded = optionFlags?.FirstOrDefault();
        var extraOptionsUpgraded = optionFlags?.Skip(1).ToArray();
        var reward = new RewardState(
            SourceRoom: "Event:" + PrototypeNativeCrystalSphere.EventId,
            CardOptions: firstCard,
            PotionOption: potions.FirstOrDefault(),
            RelicOption: relic,
            CardResolved: cardGroups.Length == 0,
            PotionResolved: potions.Count == 0,
            RelicResolved: relic is null,
            EndsAct: false,
            ExtraCardOptions: extraCards.Length == 0 ? null : extraCards,
            ExtraRelicRewardIds: null,
            CardOptionUpgradeFlags: firstOptionsUpgraded,
            ExtraCardOptionUpgradeFlags: extraOptionsUpgraded,
            GoldOption: gold.Count > 0 ? gold[0] : null,
            GoldResolved: gold.Count == 0,
            IndependentSelection: true,
            ExtraCardGroupsResolved: new bool[extraCards.Length],
            ExtraRelicGroupsResolved: [],
            ExtraGoldOptions: gold.Count <= 1
                ? null : gold.Skip(1).ToArray(),
            ExtraGoldGroupsResolved: gold.Count <= 1
                ? null : new bool[gold.Count - 1],
            CardRewardGenerators: generators.Length == 0
                ? null : generators,
            ExtraPotionOptions: potions.Count <= 1
                ? null : potions.Skip(1).ToArray(),
            ExtraPotionGroupsResolved: potions.Count <= 1
                ? null : new bool[potions.Count - 1]);
        return (player, reward);
    }
}
