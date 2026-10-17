namespace Sts2Emulator.Core;

/// <summary>
/// Source-pinned v0.111.0 EndlessConveyor: dish order, weights, eligibility,
/// no immediate dish repetition and every fifth dish forced to Seapunk Salad.
/// Other card pools retain explicitly documented prototype approximations.
/// </summary>
public static class PrototypeNativeEndlessConveyor
{
    public const string EventId = "proto.native.underdocks.endless_conveyor";
    public const string NativeRngEventId = "ENDLESS_CONVEYOR";
    public const string DishWeightedTicketSiteId =
        "event.endless_conveyor.dish_weighted_ticket";
    public const string UpgradeCardSiteId =
        "event.endless_conveyor.random_upgrade.card_instance";
    public const string ColorlessCardSiteId =
        "event.endless_conveyor.colorless_card.index_draw";
    public const string FeedingFrenzyId = "proto.native.underdocks.feeding_frenzy";

    public static PrototypeCardDefinition[] EventCards { get; } =
    [
        new(
            FeedingFrenzyId,
            "Feeding Frenzy",
            0,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind.ApplyPlayerPower, 5, 2,
                    PowerId: "proto.power.temporary_strength")
            ],
            Rarity: PrototypeCardRarity.Event,
            RewardEligible: false,
            Type: PrototypeCardType.Skill)
    ];

    private static readonly (string Id, int Weight)[] BaseDishes =
    [
        ("CAVIAR", 6),
        ("SPICY_SNAPPY", 3),
        ("JELLY_LIVER", 3),
        ("FRIED_EEL", 3)
    ];

    public static RunState RollNextDish(RunState state)
    {
        var world = state.World
            ?? throw new InvalidOperationException(
                "Endless Conveyor requires an active run world.");
        var current = world.Event
            ?? throw new InvalidOperationException(
                "Endless Conveyor has no active event.");
        var nextRoll = current.NativeDishCount + 1;
        if (nextRoll % 5 == 0)
        {
            // The source forces Seapunk Salad without calling NextFloat.
            return state with
            {
                World = world with
                {
                    Event = current with
                    {
                        NativeDishCount = nextRoll,
                        NativeLastDishId = "SEAPUNK_SALAD",
                        NativeDishId = "SEAPUNK_SALAD",
                        NativeEventRngProviderMode = null
                    }
                }
            };
        }

        var candidates = GetWeightedDishCandidates(state, current);
        var totalWeight = candidates.Sum(dish => dish.Weight);
        if (totalWeight <= 0)
        {
            throw new InvalidOperationException(
                "Endless Conveyor has no available dishes.");
        }

        float roll;
        var ownerId = $"dish:{nextRoll}:pool:{CanonicalJson.Sha256(candidates)}";
        if (state.NativeRunRng is not null
            && (current.NativeEventRng is not null
                || current.NativeDishCount == 0))
        {
            var draw = NativeEventContextRngV111.ResolveUnitFloat(
                state,
                current,
                NativeRngEventId,
                DishWeightedTicketSiteId,
                ownerId);
            state = draw.State;
            world = state.World!;
            current = world.Event!;
            roll = draw.SelectedValue * totalWeight;
        }
        else
        {
            // Preserve the legacy prototype's integer-weight approximation.
            roll = LegacyRandomChoiceV111.ChooseBoundedInteger(
                state.Rng,
                DishWeightedTicketSiteId,
                "event",
                ownerId,
                totalWeight).SelectedIndex;
        }
        var selected = candidates[^1].Id;
        foreach (var dish in candidates)
        {
            if (roll < dish.Weight)
            {
                selected = dish.Id;
                break;
            }
            roll -= dish.Weight;
        }
        return state with
        {
            World = world with
            {
                Event = current with
                {
                    NativeDishCount = nextRoll,
                    NativeDishId = selected,
                    NativeLastDishId = selected,
                    NativeEventRngProviderMode = null
                }
            }
        };
    }

    internal static (string Id, int Weight)[] GetWeightedDishCandidates(
        RunState state, EventState current)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(current);
        if (current.EventId != EventId ||
            (current.NativeDishCount + 1) % 5 == 0)
            throw new InvalidOperationException(
                "Endless Conveyor dish candidate query is not at a weighted roll.");
        var candidates = BaseDishes.ToList();
        if (state.Player.PotionSlots.Any(slot => slot is null))
            candidates.Add(("SUSPICIOUS_CONDIMENT", 3));
        if (state.Player.Hp < state.Player.MaxHp)
            candidates.Add(("CLAM_ROLL", 6));
        if (current.NativeDishCount + 1 > 1)
            candidates.Add(("GOLDEN_FYSH", 1));
        candidates.RemoveAll(dish => dish.Id == current.NativeLastDishId);
        return candidates.ToArray();
    }

    internal static long[] GetUpgradeCardCandidates(RunState state) =>
        state.Player.Deck.Where(card =>
            PrototypeGameEngine.CanSelectEventDeckCard(card,
                PrototypePersistentDeckChoiceKind.Upgrade,
                null, null, false))
            .Select(card => card.InstanceId).ToArray();

    internal static string[] GetColorlessCardCandidates() =>
        PrototypeColorlessCards.Implemented
            .Where(card => card.MechanicsImplemented && !card.MultiplayerOnly)
            .Select(card => card.Id).ToArray();

    internal static bool IsValidScriptedDishState(
        RunState state, EventState current)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(current);
        var initialEntryBoundary = current.NativePageIndex == 0 &&
            current.ChosenChoiceId is null && !current.NativeDishRollPending;
        var completedObserveBoundary =
            current.NativePageIndex ==
                PrototypeNativeEventCompletionPages.TerminalChoicePageIndex(
                    EventId) &&
            current.ChosenChoiceId == "observe" &&
            !current.NativeDishRollPending;
        var deferredGrabBoundary = current.NativePageIndex == 1 &&
            current.ChosenChoiceId == "grab" && current.NativeDishRollPending &&
            (current.NativeDishId == "JELLY_LIVER" &&
                (current.PendingDeckChoice is not null ||
                 current.RemainingDeckChoices.Length > 0) ||
             current.NativeDishId == "SUSPICIOUS_CONDIMENT" &&
                (current.PendingPotionReplacement is not null ||
                 current.RemainingPotionIds.Length > 0));
        var completedGrabBoundary = current.NativePageIndex == 1 &&
            current.ChosenChoiceId == "grab" && !current.NativeDishRollPending &&
            current.NativeDishCount >= 2;
        if (state.Phase != RunPhase.Event ||
            state.World?.Combat is not null ||
            current.EventId != EventId ||
            !(initialEntryBoundary || completedObserveBoundary || deferredGrabBoundary ||
                completedGrabBoundary) ||
            current.NativeDishCount < 1 || current.NativeDishCount % 5 == 0 ||
            current.NativeDishId is null ||
            current.NativeDishId != current.NativeLastDishId ||
            current.NativeDishId == "SEAPUNK_SALAD" ||
            string.IsNullOrWhiteSpace(state.RunSeed) ||
            state.NativeRunRng is null || current.NativeEventRng is not { } stream)
            return false;

        var expected = NativeRngV111Contexts.ForEvent(
            state.RunSeed, NativeRngEventId);
        if (stream.StreamId != expected.StreamId)
            return false;
        var precedingRollCount = initialEntryBoundary
            ? 0 : current.NativeDishCount - 1;
        var precedingEvent = new EventState(EventId) with
        {
            NativeDishCount = precedingRollCount
        };
        var candidates = GetWeightedDishCandidates(
            state, precedingEvent);
        return candidates.Any(candidate =>
            candidate.Id == current.NativeDishId);
    }
}

/// <summary>
/// Source-pinned PunchOff.cs. The fight uses the real existing Punch
/// Construct enemy definition with a special two-enemy event encounter.
/// </summary>
public static class PrototypeNativePunchOff
{
    public const string EventId = "proto.native.underdocks.punch_off";
    public const string NativeRngEventId = "PUNCH_OFF";
    public const string EncounterId = "proto.encounter.punch_off_event";
    public const string NativeEncounterEntry = "PunchOffEventEncounter";
    public const string ConstructHpOffsetSiteId =
        "event.punch_off.construct_hp_offset.index_draw";
    public const string RelicRewardSiteId =
        "event.punch_off.relic_reward.index_draw";
    public const string PotionRewardSiteId =
        "event.punch_off.potion_reward.index_draw";

    public static PrototypeEncounterDefinition Encounter { get; } = new(
        EncounterId,
        PrototypeRoomType.Combat,
        ["proto.enemy.punch_construct", "proto.enemy.punch_construct"],
        MinAct: 1,
        MaxAct: 1,
        Weight: 0);

    public static NativeEncounterFormationV111 PrepareNativeCombatFormation(
        RunState state, RunWorldState world,
        PrototypeEncounterDefinition encounter)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(encounter);
        if (state.NativeRunRng is null
            || world.Act != 1
            || world.ActOneRegion != PrototypeActOneRegion.Underdocks
            || world.Event?.EventId != EventId
            || encounter.Id != EncounterId
            || world.Map.GenerationProfileId
                != PrototypeNativeUnderdocks.GenerationProfileId)
            throw new NotSupportedException(
                "Native Punch Off combat requires its configured Act 1 event run.");

        var coordinate = world.Map.CurrentNodeId?.Split(':');
        if (coordinate is not { Length: 3 }
            || coordinate[0] != "1"
            || !int.TryParse(coordinate[1], out var floor)
            || !int.TryParse(coordinate[2], out var column)
            || floor != world.Floor
            || floor is < 1 or > 16
            || column is < 0 or >= PrototypeNativeOvergrowthMap.MapWidth)
            throw new NotSupportedException(
                "Native Punch Off combat requires a valid Act 1 map coordinate.");

        return NativeEncounterFormationFactoryV111.Generate(
            state.RunSeed,
            encounter,
            NativeEncounterEntry,
            totalFloor: world.Floor,
            currentActIndex: 0,
            firstMonsterCombatId: 1,
            mapColumn: column,
            mapRow: floor - 1);
    }
}

public sealed partial class PrototypeGameEngine
{
    private static IReadOnlyList<GameAction> GetEndlessConveyorActions(
        RunState state, EventState evt)
    {
        var result = new List<GameAction>(2);
        if (state.Player.Gold >= 40)
        {
            result.Add(GameAction.Create(
                "event_choice", new EventChoicePayload("grab")));
        }
        result.Add(GameAction.Create(
            "event_choice",
            new EventChoicePayload(
                evt.NativeDishCount == 1 ? "observe" : "leave")));
        return result;
    }

    private static RunState StepEndlessConveyor(RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        var evt = world.Event
            ?? throw new InvalidOperationException("No Conveyor event.");
        var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
        if (choice == "observe" && evt.NativeDishCount == 1)
        {
            state = UpgradeRandomEligibleConveyorCard(state);
            world = RequireWorld(state);
            evt = world.Event!;
            var result = state with
            {
                World = world with
                {
                    Event = evt with
                    {
                        ChosenChoiceId = choice,
                        NativeEventRngProviderMode = null
                    }
                }
            };
            return PreserveNativeEventCompletionPage(result, choice);
        }
        if (choice == "leave" && evt.NativeDishCount > 1)
        {
            var result = state with
            {
                World = world with
                {
                    Event = evt with { ChosenChoiceId = choice }
                }
            };
            return PreserveNativeEventCompletionPage(result, choice);
        }
        if (choice != "grab" || state.Player.Gold < 40)
        {
            throw new InvalidOperationException(
                $"Endless Conveyor choice '{choice}' is unavailable.");
        }

        var playerAfter = state.Player with
        {
            Gold = state.Player.Gold
                - (evt.NativeDishId == "GOLDEN_FYSH" ? 0 : 40)
        };
        var nextCardId = world.NextCardInstanceId;
        PrototypePendingEventDeckChoiceState[] pendingCards = [];
        string[] pendingPotions = [];
        switch (evt.NativeDishId)
        {
            case "CAVIAR":
                playerAfter = playerAfter with
                {
                    MaxHp = playerAfter.MaxHp + 4,
                    Hp = playerAfter.Hp + 4
                };
                break;

            case "SPICY_SNAPPY":
                state = UpgradeRandomEligibleConveyorCard(
                    state with { Player = playerAfter });
                world = RequireWorld(state);
                evt = world.Event!;
                playerAfter = state.Player;
                break;

            case "JELLY_LIVER":
                pendingCards =
                [
                    new PrototypePendingEventDeckChoiceState(
                        "grab", PrototypePersistentDeckChoiceKind.Transform,
                        1, [])
                ];
                break;

            case "FRIED_EEL":
            {
                // Only implemented, solo-safe Colorless cards are
                // currently catalogued. This restricted generation
                // pool is intentionally disclosed in the docs.
                var pool = PrototypeNativeEndlessConveyor
                    .GetColorlessCardCandidates();
                if (pool.Length == 0)
                {
                    throw new NotSupportedException(
                        "Fried Eel has no supported Colorless card.");
                }
                var ownerId = $"dish:{evt.NativeDishCount}:pool:"
                    + CanonicalJson.Sha256(pool);
                int selectedIndex;
                if (state.NativeRunRng is not null
                    && evt.NativeEventRng is null)
                {
                    selectedIndex = LegacyRandomChoiceV111.ChooseIndexRange(
                        state.Rng,
                        PrototypeNativeEndlessConveyor.ColorlessCardSiteId,
                        "event",
                        ownerId,
                        pool.Length).SelectedIndex;
                }
                else
                {
                    var draw = NativeEventContextRngV111.ResolveIndex(
                        state,
                        evt,
                        PrototypeNativeEndlessConveyor.NativeRngEventId,
                        PrototypeNativeEndlessConveyor.ColorlessCardSiteId,
                        ownerId,
                        pool.Length);
                    state = draw.State;
                    world = RequireWorld(state);
                    evt = world.Event!;
                    selectedIndex = draw.SelectedIndex;
                }
                var id = pool[selectedIndex];
                playerAfter = AppendCard(
                    playerAfter, nextCardId++, id, state.Rng);
                break;
            }

            case "SUSPICIOUS_CONDIMENT":
                if (PrototypeNativePotionShop.NativePotionPoolOrder.Count == 0)
                {
                    throw new NotSupportedException(
                        "Suspicious Condiment has no supported potions.");
                }
                pendingPotions =
                [
                    PrototypeNativePotionShop.PickUniform(
                        state.Rng,
                        "reward",
                        $"event:endless_conveyor:dish:{evt.NativeDishCount}:potion")
                ];
                break;

            case "CLAM_ROLL":
                playerAfter = playerAfter with
                {
                    Hp = Math.Min(playerAfter.MaxHp, playerAfter.Hp + 10)
                };
                break;

            case "GOLDEN_FYSH":
                playerAfter = playerAfter with
                {
                    Gold = GoldAfterGain(playerAfter, 75)
                };
                break;

            case "SEAPUNK_SALAD":
                playerAfter = AppendCard(
                    playerAfter, nextCardId++,
                    PrototypeNativeEndlessConveyor.FeedingFrenzyId,
                    state.Rng);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown Conveyor dish '{evt.NativeDishId}'.");
        }

        // Native awaits the entire dish action, including a card-selection
        // or potion-reward continuation, before sampling the next dish.
        // Preserve that ordering so choices can consume the event RNG first.
        evt = evt with
        {
            ChosenChoiceId = "grab",
            NativePageIndex = 1,
            QueuedDeckChoices = pendingCards,
            QueuedPotionIds = pendingPotions,
            NativeDishRollPending = true
        };
        return AdvanceEventContinuations(state with
        {
            Player = playerAfter,
            World = world with
            {
                Event = evt,
                NextCardInstanceId = nextCardId
            }
        });
    }

    private static RunState UpgradeRandomEligibleConveyorCard(RunState state)
    {
        var world = RequireWorld(state);
        var evt = world.Event
            ?? throw new InvalidOperationException(
                "Endless Conveyor has no active event.");
        var candidates = PrototypeNativeEndlessConveyor
            .GetUpgradeCardCandidates(state);
        if (candidates.Length == 0)
        {
            return state;
        }
        var ownerId = "endless_conveyor:deck:"
            + CanonicalJson.Sha256(candidates);
        int selectedIndex;
        if (state.NativeRunRng is not null && evt.NativeEventRng is null)
        {
            selectedIndex = LegacyRandomChoiceV111.ChooseIndexRange(
                state.Rng,
                PrototypeNativeEndlessConveyor.UpgradeCardSiteId,
                "event",
                ownerId,
                candidates.Length).SelectedIndex;
        }
        else
        {
            var draw = NativeEventContextRngV111.ResolveIndex(
                state,
                evt,
                PrototypeNativeEndlessConveyor.NativeRngEventId,
                PrototypeNativeEndlessConveyor.UpgradeCardSiteId,
                ownerId,
                candidates.Length);
            state = draw.State;
            selectedIndex = draw.SelectedIndex;
        }
        var chosen = candidates[selectedIndex];
        return state with
        {
            Player = state.Player with
            {
                Deck = state.Player.Deck.Select(card =>
                    card.InstanceId == chosen
                        ? card with { UpgradeLevel = card.UpgradeLevel + 1 }
                        : card).ToArray()
            }
        };
    }

    private static IReadOnlyList<GameAction> GetPunchOffActions(
        EventState evt) =>
        evt.NativePageIndex == 0
            ?
            [
                GameAction.Create(
                    "event_choice", new EventChoicePayload("nab")),
                GameAction.Create(
                    "event_choice", new EventChoicePayload("take_them"))
            ]
            :
            [
                GameAction.Create(
                    "event_choice", new EventChoicePayload("fight"))
            ];

    private static RunState StepPunchOff(RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        var evt = world.Event
            ?? throw new InvalidOperationException("No Punch Off event.");
        var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;

        if (evt.NativePageIndex == 0 && choice == "nab")
        {
            var nativeRunRng = UsesNativeRegionalSystems(world)
                ? state.NativeRunRng?.Fork()
                : null;
            var player = AppendCard(
                state.Player, world.NextCardInstanceId,
                "proto.native.neow.injury", state.Rng,
                nativeRunRng);
            var reward = CreateDeferredEventReward(
                state, PrototypeRunEffectKind.OfferRandomRelic,
                player, evt.EventId, nativeRunRng);
            return AdvanceEventContinuations(state with
            {
                Player = player,
                NativeRunRng = nativeRunRng ?? state.NativeRunRng,
                World = world with
                {
                    NextCardInstanceId = world.NextCardInstanceId + 1,
                    Event = evt with
                    {
                        ChosenChoiceId = "nab",
                        PendingReward = reward
                    }
                }
            });
        }

        if (evt.NativePageIndex == 0 && choice == "take_them")
        {
            return state with
            {
                World = world with
                {
                    Event = evt with { NativePageIndex = 1 }
                }
            };
        }

        if (evt.NativePageIndex == 1 && choice == "fight")
        {
            // Native: two Punch Constructs with independent reductions
            // of 2–9 HP; left starts on Fast Punch, right on Ready.
            var started = StartCombat(
                state, PrototypeRoomType.Combat,
                PrototypeNativePunchOff.Encounter);
            var combat = RequireWorld(started).Combat!;
            if (combat.Enemies.Length != 2)
            {
                throw new InvalidOperationException(
                    "Punch Off must start exactly two Constructs.");
            }
            var enemies = new EnemyCombatState[combat.Enemies.Length];
            for (var index = 0; index < combat.Enemies.Length; index++)
            {
                var enemy = combat.Enemies[index];
                int selectedIndex;
                if (combat.NativeRng is not null)
                {
                    combat = NativeCombatRngEntryV111.AdvanceMonsterLocal(
                        combat,
                        enemy.InstanceId,
                        PrototypeNativePunchOff.ConstructHpOffsetSiteId,
                        $"event:punch_off:construct_combat_id:{enemy.InstanceId}",
                        maxExclusive: 8,
                        ordinal: 0,
                        out selectedIndex);
                }
                else
                {
                    selectedIndex = LegacyRandomChoiceV111
                        .ChooseBoundedInteger(
                            state.Rng,
                            PrototypeNativePunchOff.ConstructHpOffsetSiteId,
                            "combat",
                            $"enemy:{enemy.InstanceId}:construct:{index}",
                            8).SelectedIndex;
                }
                enemies[index] = enemy with
                {
                    Hp = Math.Max(1, enemy.Hp - (2 + selectedIndex)),
                    MoveIndex = index == 0 ? 1 : 0
                };
            }
            var combatWorld = RequireWorld(started);
            return started with
            {
                World = combatWorld with
                {
                    Combat = combat with { Enemies = enemies },
                    Event = evt with { NativePageIndex = 2 }
                }
            };
        }

        throw new InvalidOperationException(
            $"Punch Off choice '{choice}' is unavailable.");
    }

    private static RunState EnterPunchOffReward(RunState state)
    {
        // Event fight rewards: two guaranteed custom reward components
        // (RelicReward + PotionReward), not a generic silent victory
        // or the Nab option's single RelicReward.
        var player = state.Player;
        var available = PrototypeContent.RelicPool.Where(id =>
            !player.Relics.Any(relic =>
                StringComparer.Ordinal.Equals(relic.RelicId, id))).ToArray();
        if (available.Length == 0 || PrototypeContent.PotionPool.Length == 0)
        {
            throw new NotSupportedException(
                "Punch Off requires supported relic and potion rewards.");
        }
        var world = RequireWorld(state);
        var nativeRunRng = UsesNativeRegionalSystems(world)
            ? state.NativeRunRng?.Fork()
            : null;
        var relicOwner = "event:punch_off:relic_pool:"
            + CanonicalJson.Sha256(available);
        var relicIndex = nativeRunRng is { } nativeRewardRng
            ? NativePlayerRngV111.ChooseIndex(
                nativeRewardRng,
                PrototypeNativePunchOff.RelicRewardSiteId,
                NativePlayerRngV111.RewardsStreamId,
                relicOwner,
                available.Length)
            : LegacyRandomChoiceV111.ChooseIndexRange(
                state.Rng,
                PrototypeNativePunchOff.RelicRewardSiteId,
                "reward",
                relicOwner,
                available.Length).SelectedIndex;
        var relic = available[relicIndex];
        var potionPool = PrototypeContent.PotionPool;
        var potionOwner = "event:punch_off:potion_pool:"
            + CanonicalJson.Sha256(potionPool);
        var potionIndex = nativeRunRng is { } nativePotionRng
            ? NativePlayerRngV111.ChooseIndex(
                nativePotionRng,
                PrototypeNativePunchOff.PotionRewardSiteId,
                NativePlayerRngV111.RewardsStreamId,
                potionOwner,
                potionPool.Length)
            : LegacyRandomChoiceV111.ChooseIndexRange(
                state.Rng,
                PrototypeNativePunchOff.PotionRewardSiteId,
                "reward",
                potionOwner,
                potionPool.Length).SelectedIndex;
        var potion = potionPool[potionIndex];

        var reward = new RewardState(
            SourceRoom: "Combat",
            CardOptions: [],
            PotionOption: potion,
            RelicOption: relic,
            CardResolved: true,
            PotionResolved: false,
            RelicResolved: false,
            EndsAct: false,
            IndependentSelection: true);
        return state with
        {
            NativeRunRng = nativeRunRng ?? state.NativeRunRng,
            World = world with
            {
                Combat = null,
                Reward = reward
            },
            Phase = RunPhase.Reward
        };
    }
}
