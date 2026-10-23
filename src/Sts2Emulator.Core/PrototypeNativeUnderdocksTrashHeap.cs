namespace Sts2Emulator.Core;

/// <summary>
/// Source-backed Trash Heap lists from v0.111.0 TrashHeap.cs.
/// Ordering is significant: EventModel.Rng.NextItem selects one uniformly.
/// The event-only cards are deliberately NOT part of the Silent reward pool.
/// </summary>
public static class PrototypeNativeUnderdocksTrashHeap
{
    public const string EventId = "proto.native.underdocks.trash_heap";
    public const string NativeRngEventId = "TRASH_HEAP";
    public const string CardRewardSiteId =
        StochasticSiteRegistryV111.NativeTrashHeapCardRewardSiteId;
    public const string RelicRewardSiteId =
        StochasticSiteRegistryV111.NativeTrashHeapRelicRewardSiteId;
    public const string DistractionCardId =
        "proto.native.trash_heap.distraction";
    public const string RipAndTearCardId =
        "proto.native.trash_heap.rip_and_tear";

    private static readonly string[] RelicIdValues =
    [
        "proto.relic.darkstone_periapt",
        "proto.native.trash_heap.dream_catcher",
        "proto.native.trash_heap.hand_drill",
        "proto.native.trash_heap.maw_bank",
        "proto.native.trash_heap.the_boot"
    ];

    private static readonly string[] CardIdValues =
    [
        "proto.native.trash_heap.caltrops",
        "proto.native.trash_heap.clash",
        DistractionCardId,
        "proto.native.trash_heap.dual_wield",
        "proto.native.trash_heap.entrench",
        "proto.native.trash_heap.hello_world",
        "proto.native.trash_heap.outmaneuver",
        "proto.native.trash_heap.rebound",
        RipAndTearCardId,
        "proto.native.trash_heap.stack"
    ];

    public static string[] RelicIds =>
        (string[])RelicIdValues.Clone();

    public static string[] CardIds =>
        (string[])CardIdValues.Clone();

    // Event-only relics remain excluded from ordinary relic bags.
    public static PrototypeRelicDefinition[] Relics => RelicDefinitions
        .Select(PrototypeDefinitionCopiesV111.CloneRelicDefinition)
        .ToArray();

    private static readonly PrototypeRelicDefinition[] RelicDefinitions =
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
    public static PrototypeCardDefinition[] Cards => CardDefinitions
        .Select(PrototypeDefinitionCopiesV111.CloneCardDefinition)
        .ToArray();

    private static readonly PrototypeCardDefinition[] CardDefinitions =
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

    public static PrototypePowerDefinition[] Powers => PowerDefinitions
        .Select(PrototypeDefinitionCopiesV111.ClonePowerDefinition)
        .ToArray();

    private static readonly PrototypePowerDefinition[] PowerDefinitions =
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
        var evt = world.Event
            ?? throw new InvalidOperationException("Trash Heap is not active.");
        if (evt.EventId != PrototypeNativeUnderdocksTrashHeap.EventId)
        {
            throw new InvalidOperationException("Trash Heap is not active.");
        }

        var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
        switch (choice)
        {
            case "grab":
            {
                var draw = NativeEventContextRngV111.ResolveIndex(
                    state, evt,
                    PrototypeNativeUnderdocksTrashHeap.NativeRngEventId,
                    PrototypeNativeUnderdocksTrashHeap.CardRewardSiteId,
                    "event:trash_heap:cards",
                    PrototypeNativeUnderdocksTrashHeap.CardIds.Length);
                state = draw.State;
                world = RequireWorld(state);
                evt = world.Event!;
                var chosen = PrototypeNativeUnderdocksTrashHeap.CardIds[
                    draw.SelectedIndex];
                var player = state.Player with
                {
                    Gold = GoldAfterGain(state.Player, 100)
                };
                var prototypeRng = state.Stochastic.PrototypeRng;
                player = AppendCard(
                    player, world.NextCardInstanceId, chosen, prototypeRng);
                var stochastic = RunStochasticStateV111.WithPrototypeRng(
                    state.Stochastic, prototypeRng);
                var result = state with
                {
                    Player = player,
                    Stochastic = stochastic,
                    World = world with
                    {
                        NextCardInstanceId = world.NextCardInstanceId + 1
                    }
                };
                return PreserveNativeEventCompletionPage(result, choice);
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

                var draw = NativeEventContextRngV111.ResolveIndex(
                    state, evt,
                    PrototypeNativeUnderdocksTrashHeap.NativeRngEventId,
                    PrototypeNativeUnderdocksTrashHeap.RelicRewardSiteId,
                    "event:trash_heap:relics",
                    PrototypeNativeUnderdocksTrashHeap.RelicIds.Length);
                state = draw.State;
                world = RequireWorld(state);
                evt = world.Event!;
                var relicId = PrototypeNativeUnderdocksTrashHeap.RelicIds[
                    draw.SelectedIndex];
                // RelicCmd.Obtain / Player.AddRelicInternal append a new
                // mutable instance even when its model ID is already owned.
                // A new Maw Bank starts active regardless of an older one's
                // purchase history. The fixed event pool is never rerolled.
                player = player with
                {
                    Relics = player.Relics.Append(
                        new RelicInstance(
                            relicId, PrototypeJson.EmptyObject())).ToArray()
                };
                var prototypeRng = state.Stochastic.PrototypeRng;
                player = ApplyRelicRunEvent(
                    player, PrototypeRunEventKind.RelicAcquired,
                    acquiredRelicId: relicId, rng: prototypeRng);
                var stochastic = RunStochasticStateV111.WithPrototypeRng(
                    state.Stochastic, prototypeRng);
                var result = state with
                {
                    Player = player,
                    Stochastic = stochastic,
                    World = world with { Event = evt }
                };
                return PreserveNativeEventCompletionPage(result, choice);
            }
            default:
                throw new InvalidOperationException(
                    $"Unknown Trash Heap choice '{choice}'.");
        }
    }

}
