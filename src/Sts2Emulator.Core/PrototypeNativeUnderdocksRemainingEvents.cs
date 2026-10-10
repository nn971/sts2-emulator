namespace Sts2Emulator.Core;

/// <summary>
/// Source-pinned v0.111.0 EndlessConveyor: dish order, weights, eligibility,
/// no immediate dish repetition and every fifth dish forced to Seapunk Salad.
/// Other card pools retain explicitly documented prototype approximations.
/// </summary>
public static class PrototypeNativeEndlessConveyor
{
    public const string EventId = "proto.native.underdocks.endless_conveyor";
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

    public static EventState RollNextDish(
        EventState current, PlayerState player, RngBundle rng)
    {
        var nextRoll = current.NativeDishCount + 1;
        if (nextRoll % 5 == 0)
        {
            // The source forces Seapunk Salad without calling NextFloat.
            return current with
            {
                NativeDishCount = nextRoll,
                NativeLastDishId = "SEAPUNK_SALAD",
                NativeDishId = "SEAPUNK_SALAD"
            };
        }

        var candidates = BaseDishes.ToList();
        if (player.PotionSlots.Any(slot => slot is null))
        {
            candidates.Add(("SUSPICIOUS_CONDIMENT", 3));
        }
        if (player.Hp < player.MaxHp)
        {
            candidates.Add(("CLAM_ROLL", 6));
        }
        if (nextRoll > 1)
        {
            candidates.Add(("GOLDEN_FYSH", 1));
        }
        candidates.RemoveAll(dish => dish.Id == current.NativeLastDishId);
        var totalWeight = candidates.Sum(dish => dish.Weight);
        if (totalWeight <= 0)
        {
            throw new InvalidOperationException(
                "Endless Conveyor has no available dishes.");
        }

        // Native uses NextFloat()*weight; the prototype uses a single
        // integer draw in the same event stream, with the same weights.
        var roll = PrototypeRng.NextInt(rng, "event", totalWeight);
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
        return current with
        {
            NativeDishCount = nextRoll,
            NativeDishId = selected,
            NativeLastDishId = selected
        };
    }
}

/// <summary>
/// Source-pinned PunchOff.cs. The fight uses the real existing Punch
/// Construct enemy definition with a special two-enemy event encounter.
/// </summary>
public static class PrototypeNativePunchOff
{
    public const string EventId = "proto.native.underdocks.punch_off";
    public const string EncounterId = "proto.encounter.punch_off_event";

    public static PrototypeEncounterDefinition Encounter { get; } = new(
        EncounterId,
        PrototypeRoomType.Combat,
        ["proto.enemy.punch_construct", "proto.enemy.punch_construct"],
        MinAct: 1,
        MaxAct: 1);
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
            var player = UpgradeRandomEligibleConveyorCard(
                state.Player, state.Rng);
            return CompleteRoomToMap(state with { Player = player });
        }
        if (choice == "leave" && evt.NativeDishCount > 1)
        {
            return CompleteRoomToMap(state);
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
                playerAfter = UpgradeRandomEligibleConveyorCard(
                    playerAfter, state.Rng);
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
                var pool = PrototypeColorlessCards.Implemented
                    .Where(card => card.MechanicsImplemented
                        && !card.MultiplayerOnly)
                    .Select(card => card.Id).ToArray();
                if (pool.Length == 0)
                {
                    throw new NotSupportedException(
                        "Fried Eel has no supported Colorless card.");
                }
                var id = pool[PrototypeRng.NextInt(
                    state.Rng, "event", pool.Length)];
                playerAfter = AppendCard(
                    playerAfter, nextCardId++, id, state.Rng);
                break;
            }

            case "SUSPICIOUS_CONDIMENT":
                if (PrototypeContent.PotionPool.Length == 0)
                {
                    throw new NotSupportedException(
                        "Suspicious Condiment has no supported potions.");
                }
                pendingPotions =
                [
                    PrototypeContent.PotionPool[PrototypeRng.NextInt(
                        state.Rng, "reward",
                        PrototypeContent.PotionPool.Length)]
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
                    Gold = playerAfter.Gold + 75
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

    private static PlayerState UpgradeRandomEligibleConveyorCard(
        PlayerState player, RngBundle rng)
    {
        var candidates = player.Deck.Where(card =>
                CanSelectEventDeckCard(card,
                    PrototypePersistentDeckChoiceKind.Upgrade,
                    null, null, false))
            .Select(card => card.InstanceId).ToArray();
        if (candidates.Length == 0)
        {
            return player;
        }
        var chosen = candidates[
            PrototypeRng.NextInt(rng, "event", candidates.Length)];
        return player with
        {
            Deck = player.Deck.Select(card =>
                card.InstanceId == chosen
                    ? card with { UpgradeLevel = card.UpgradeLevel + 1 }
                    : card).ToArray()
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
            var player = AppendCard(
                state.Player, world.NextCardInstanceId,
                "proto.native.neow.injury", state.Rng);
            var reward = CreateDeferredEventReward(
                state, PrototypeRunEffectKind.OfferRandomRelic,
                player, evt.EventId);
            return AdvanceEventContinuations(state with
            {
                Player = player,
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
            var enemies = combat.Enemies.Select((enemy, index) =>
                enemy with
                {
                    Hp = Math.Max(1, enemy.Hp -
                        (2 + PrototypeRng.NextInt(
                            state.Rng, "combat", 8))),
                    MoveIndex = index == 0 ? 1 : 0
                }).ToArray();
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
        var relic = available[PrototypeRng.NextInt(
            state.Rng, "reward", available.Length)];
        var potion = PrototypeContent.PotionPool[
            PrototypeRng.NextInt(
                state.Rng, "reward", PrototypeContent.PotionPool.Length)];

        var world = RequireWorld(state);
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
            World = world with
            {
                Combat = null,
                Reward = reward
            },
            Phase = RunPhase.Reward
        };
    }
}
