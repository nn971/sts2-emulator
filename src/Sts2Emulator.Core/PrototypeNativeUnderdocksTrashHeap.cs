namespace Sts2Emulator.Core;

/// <summary>
/// Source-backed Trash Heap lists from v0.111.0 TrashHeap.cs.
/// Ordering is significant: EventModel.Rng.NextItem selects one uniformly.
/// The event-only cards are deliberately NOT part of the Silent reward pool.
/// </summary>
public static class PrototypeNativeUnderdocksTrashHeap
{
    public const string EventId = "proto.native.underdocks.trash_heap";

    public static string[] RelicIds { get; } =
    [
        "proto.relic.darkstone_periapt",
        "proto.native.trash_heap.dream_catcher",
        "proto.native.trash_heap.hand_drill",
        "proto.native.trash_heap.maw_bank",
        "proto.native.trash_heap.the_boot"
    ];

    public static string[] CardIds { get; } =
    [
        "proto.native.trash_heap.caltrops",
        "proto.native.trash_heap.clash",
        "proto.native.trash_heap.distraction",
        "proto.native.trash_heap.dual_wield",
        "proto.native.trash_heap.entrench",
        "proto.native.trash_heap.hello_world",
        "proto.native.trash_heap.outmaneuver",
        "proto.native.trash_heap.rebound",
        "proto.native.trash_heap.rip_and_tear",
        "proto.native.trash_heap.stack"
    ];

    // Event-only relics remain excluded from ordinary relic bags.
    public static PrototypeRelicDefinition[] Relics { get; } =
    [
        new("proto.native.trash_heap.dream_catcher", "Dream Catcher",
            RestHealCardRewardCount: 3),
        new("proto.native.trash_heap.hand_drill", "Hand Drill",
            EnemyVulnerableOnBlockBroken: 2),
        new("proto.native.trash_heap.maw_bank", "Maw Bank",
            GoldOnRoomEntryUntilPurchase: 12),
        new("proto.native.trash_heap.the_boot", "The Boot",
            MinPoweredAttackHpLoss: 5)
    ];

    // Cross-character event acquisitions use the same typed combat kernel.
    // Event acquisitions stay outside ordinary rewards and combat generation.
    public static PrototypeCardDefinition[] Cards { get; } =
    [
        new(
            CardIds[0], "Caltrops", 1, PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.ApplyPlayerPower, 3, 2,
                PowerId: "proto.power.thorns")],
            Rarity: PrototypeCardRarity.Event, RewardEligible: false,
            Type: PrototypeCardType.Power),
        new(
            CardIds[1], "Clash", 0, PrototypeCardTarget.Enemy,
            [new(PrototypeCombatEffectKind.DamageEnemy, 14, 4)],
            Rarity: PrototypeCardRarity.Event, RewardEligible: false,
            Type: PrototypeCardType.Attack,
            PlayCondition: new(PrototypeCombatPredicateKind.HandHasOnlyAttacks)),
        new(
            CardIds[2], "Distraction",
            new(PrototypeCardCostKind.Fixed, 1, -1),
            PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.CreateRandomCharacterSkillCardsInHand, 1)],
            ExhaustOnUse: true, Rarity: PrototypeCardRarity.Event,
            RewardEligible: false, Type: PrototypeCardType.Skill),
        new(
            CardIds[3], "Dual Wield", 1, PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.ChooseCards, 0,
                Selection: new(PrototypeCardZone.Hand, 1, 1,
                    PrototypeCardSelectionResolutionKind.CopyToHand,
                    RequireAttackOrPower: true, CopiesPerSelection: 1, CopiesPerSelectionUpgradeDelta: 1))],
            Rarity: PrototypeCardRarity.Event, RewardEligible: false,
            Type: PrototypeCardType.Skill),
        new(
            CardIds[4], "Entrench",
            new(PrototypeCardCostKind.Fixed, 2, -1),
            PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.MultiplyPlayerBlock, 2)],
            Rarity: PrototypeCardRarity.Event, RewardEligible: false,
            Type: PrototypeCardType.Skill),
        new(
            CardIds[5], "Hello World", 1, PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.ApplyPlayerPower, 1, PowerId: "proto.power.hello_world")],
            Rarity: PrototypeCardRarity.Event, RewardEligible: false,
            Type: PrototypeCardType.Power, InnateOnUpgrade: true),
        new(
            CardIds[6], "Outmaneuver", 1, PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.ApplyPlayerPower, 2, 1,
                PowerId: "proto.power.energy_next_turn")],
            Rarity: PrototypeCardRarity.Event, RewardEligible: false,
            Type: PrototypeCardType.Skill),
        new(
            CardIds[7], "Rebound", 1, PrototypeCardTarget.Enemy,
            [new(PrototypeCombatEffectKind.DamageEnemy, 9, 3),
             new(PrototypeCombatEffectKind.ApplyPlayerPower, 1, PowerId: "proto.power.rebound")],
            Rarity: PrototypeCardRarity.Event, RewardEligible: false,
            Type: PrototypeCardType.Attack),
        new(
            CardIds[8], "Rip and Tear", 1, PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.DamageEnemy, 7, 2,
                Target: PrototypeEffectTarget.RandomEnemy, Repetitions: 2)],
            Rarity: PrototypeCardRarity.Event, RewardEligible: false,
            Type: PrototypeCardType.Attack),
        new(
            CardIds[9], "Stack", 1, PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.GainPlayerBlock, 0, 3,
                CountKind: PrototypeCombatCountKind.DiscardPileCards, AmountPerCount: 1)],
            Rarity: PrototypeCardRarity.Event, RewardEligible: false,
            Type: PrototypeCardType.Skill)
    ];

    public static PrototypePowerDefinition[] Powers { get; } =
    [
        new("proto.power.hello_world", "Hello World", 0,
            [new(PrototypeCombatEventKind.BeforeHandDraw,
                [new(PrototypeCombatEffectKind.CreateDistinctCharacterCommonCardsInHand, 0, AmountPerPowerStack: 1)])],
            SnapshotGenerationStacksAtTurnStart: true),
        new("proto.power.rebound", "Rebound", 0, [],
            RemoveAtPlayerTurnEnd: true, ReturnNextDiscardedCardToDraw: true)
    ];
}

/// <summary>
/// Trash Heap is a source-ordered random choice: health loss before a relic
/// roll; fixed gold before a card roll. A lethal Dive never samples a relic.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private static IReadOnlyList<GameAction> GetTrashHeapActions() =>
    [
        GameAction.Create("event_choice", new EventChoicePayload("dive")),
        GameAction.Create("event_choice", new EventChoicePayload("grab"))
    ];

    private static RunState StepTrashHeap(RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        if (world.Event?.EventId != PrototypeNativeUnderdocksTrashHeap.EventId)
        {
            throw new InvalidOperationException("Trash Heap is not active.");
        }

        var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
        switch (choice)
        {
            case "grab":
            {
                var chosen = PrototypeNativeUnderdocksTrashHeap.CardIds[
                    PrototypeRng.NextInt(
                        state.Rng, "event",
                        PrototypeNativeUnderdocksTrashHeap.CardIds.Length)];
                var player = state.Player with
                {
                    Gold = checked(state.Player.Gold + 100)
                };
                player = AppendCard(
                    player, world.NextCardInstanceId, chosen, state.Rng);
                return CompleteRoomToMap(state with
                {
                    Player = player,
                    World = world with
                    {
                        NextCardInstanceId = world.NextCardInstanceId + 1
                    }
                });
            }
            case "dive":
            {
                var player = state.Player with
                {
                    Hp = Math.Max(0, state.Player.Hp - 8)
                };
                state = state with { Player = player };
                if (player.Hp == 0)
                {
                    return EndRun(state, "defeat");
                }

                var relicId = PrototypeNativeUnderdocksTrashHeap.RelicIds[
                    PrototypeRng.NextInt(
                        state.Rng, "event",
                        PrototypeNativeUnderdocksTrashHeap.RelicIds.Length)];
                if (player.Relics.Any(relic =>
                        StringComparer.Ordinal.Equals(relic.RelicId, relicId)))
                {
                    // Native duplicate RelicCmd.Obtain needs separate oracle
                    // validation. Do not secretly replace a duplicate roll
                    // with a different relic and bias the source distribution.
                    throw new NotSupportedException(
                        $"Trash Heap rolled already owned relic '{relicId}'; native duplicate handling is not implemented.");
                }

                player = player with
                {
                    Relics = player.Relics.Append(
                        new RelicInstance(
                            relicId, PrototypeJson.EmptyObject())).ToArray()
                };
                player = ApplyRelicRunEvent(
                    player, PrototypeRunEventKind.RelicAcquired,
                    acquiredRelicId: relicId, rng: state.Rng);
                return CompleteRoomToMap(state with { Player = player });
            }
            default:
                throw new InvalidOperationException(
                    $"Unknown Trash Heap choice '{choice}'.");
        }
    }
}
