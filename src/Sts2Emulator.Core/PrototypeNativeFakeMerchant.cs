using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sts2Emulator.Core;

/// <summary>
/// The shared Act 2/3 Fake Merchant event, its event-only relics, and the
/// one-enemy challenge started by throwing a Foul Potion.
/// Source: Flaaax/STS2-source@706ef1c9, FakeMerchant and
/// FakeMerchantEventEncounter.
/// </summary>
public static class PrototypeNativeFakeMerchant
{
    public const string EventId = "proto.native.event.fake_merchant";
    public const string EncounterId =
        "proto.native.encounter.fake_merchant_event";
    public const string MonsterId = "proto.native.event.fake_merchant_monster";
    public const string RugId = "proto.native.event.fake_merchants_rug";
    public const string FoulPotionId =
        PrototypeNativePotionCourier.FoulPotionId;

    public const string FakeAnchorId = "proto.native.event.fake_anchor";
    public const string FakeBloodVialId = "proto.native.event.fake_blood_vial";
    public const string FakeHappyFlowerId = "proto.native.event.fake_happy_flower";
    public const string FakeLeesWaffleId = "proto.native.event.fake_lees_waffle";
    public const string FakeMangoId = "proto.native.event.fake_mango";
    public const string FakeOrichalcumId = "proto.native.event.fake_orichalcum";
    public const string FakeSneckoEyeId = "proto.native.event.fake_snecko_eye";
    public const string FakeStrikeDummyId = "proto.native.event.fake_strike_dummy";
    public const string FakeTeaSetId = "proto.native.event.fake_venerable_tea_set";
    private const string FakeStrikeDummyPowerId =
        "proto.native.event.fake_strike_dummy_power";

    public const string LeaveChoiceId = "leave";
    public const string ThrowPotionAction = "throw_foul_potion_at_fake_merchant";
    public const int RelicCost = 50;

    public static string[] StockRelicIds { get; } =
    [
        FakeAnchorId,
        FakeBloodVialId,
        FakeHappyFlowerId,
        FakeLeesWaffleId,
        FakeMangoId,
        FakeOrichalcumId,
        FakeSneckoEyeId,
        FakeStrikeDummyId,
        FakeTeaSetId
    ];

    public static PrototypeEventDefinition Definition { get; } = new(
        EventId,
        "Fake Merchant",
        StockRelicIds.Select(id => new PrototypeEventChoiceDefinition(
            ChoiceId(id),
            "Buy " + RelicDisplayName(id),
            [
                new(PrototypeRunEffectKind.LoseGold, RelicCost),
                new(PrototypeRunEffectKind.GainRelic, RelicId: id)
            ])).Append(new PrototypeEventChoiceDefinition(
                LeaveChoiceId, "Leave", [])).ToArray(),
        MinAct: 2,
        MaxAct: 3,
        Weight: 0);

    public static PrototypeRelicDefinition[] Relics { get; } =
    [
        new(FakeAnchorId, "Fake Anchor", Triggers:
        [
            new(PrototypeCombatEventKind.CombatStarted,
                [new(PrototypeCombatEffectKind.GainPlayerBlock, 4)])
        ]),
        new(FakeBloodVialId, "Fake Blood Vial", Triggers:
        [
            new(PrototypeCombatEventKind.PlayerTurnStarted,
                [new(PrototypeCombatEffectKind.HealPlayer, 1)],
                TurnEquals: 1)
        ]),
        new(FakeHappyFlowerId, "Fake Happy Flower", Triggers:
        [
            new(PrototypeCombatEventKind.PlayerSideTurnStarted,
                [new(PrototypeCombatEffectKind.GainEnergy, 1)],
                EveryNth: 5)
        ], PersistentTriggerCountProperty: "TurnsSeen"),
        new(FakeLeesWaffleId, "Fake Lee's Waffle", RunTriggers:
        [
            new(PrototypeRunEventKind.RelicAcquired,
                [new(PrototypeRunEffectKind.HealPercentMaxHp, 10)])
        ]),
        new(FakeMangoId, "Fake Mango", RunTriggers:
        [
            new(PrototypeRunEventKind.RelicAcquired,
                [new(PrototypeRunEffectKind.GainMaxHp, 3)])
        ]),
        new(FakeOrichalcumId, "Fake Orichalcum", Triggers:
        [
            new(PrototypeCombatEventKind.PlayerTurnEnded,
                [new(PrototypeCombatEffectKind.GainPlayerBlock, 3)],
                RequiresZeroPlayerBlock: true,
                RequiresPlayerSide: true)
        ]),
        new(FakeSneckoEyeId, "Fake Snecko Eye",
            RandomizeDrawnCardCostMaxExclusive: 4),
        new(FakeStrikeDummyId, "Fake Strike Dummy", Triggers:
        [
            new(PrototypeCombatEventKind.CombatStarted,
                [new(PrototypeCombatEffectKind.ApplyPlayerPower, 1,
                    PowerId: FakeStrikeDummyPowerId)])
        ]),
        new(FakeTeaSetId, "Fake Venerable Tea Set"),
        new(RugId, "Fake Merchant's Rug")
    ];

    public static PrototypePowerDefinition[] Powers { get; } =
    [
        new(FakeStrikeDummyPowerId, "Strike Dummy", 0, [],
            AttackDamageBonusRequiredCardTag: "Strike",
            AttackDamageBonusPerStack: 1)
    ];

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        new(MonsterId, "Fake Merchant", 165, 0,
        [
            new("swipe", [new(PrototypeEnemyEffectKind.DamagePlayer, 13,
                AscensionDeltas: [new(9, 2)])]),
            new("spew_coins", [new(PrototypeEnemyEffectKind.DamagePlayer,
                2, Repetitions: 8)]),
            new("throw_relic",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 9,
                    AscensionDeltas: [new(9, 1)]),
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 1,
                    PowerId: "proto.power.frail", IsAttack: false)
            ]),
            new("enrage", [new(PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                PowerId: "proto.power.strength", IsAttack: false)])
        ],
        HpAscensionDeltas: [new(8, 10)],
        MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
        Ai: new("swipe",
        [
            new("swipe", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 0, NextStateId: "random"),
            new("spew_coins", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 1, NextStateId: "random"),
            new("throw_relic", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 2, NextStateId: "random_attacks"),
            new("enrage", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 3, NextStateId: "random"),
            new("random", PrototypeEnemyAiStateKind.Random,
                Branches:
                [
                    new("swipe", 1,
                        PrototypeEnemyAiRepeatRule.CannotRepeat),
                    new("spew_coins", 1,
                        PrototypeEnemyAiRepeatRule.CannotRepeat),
                    new("throw_relic", 1,
                        PrototypeEnemyAiRepeatRule.CannotRepeat),
                    new("enrage", 3,
                        PrototypeEnemyAiRepeatRule.CannotRepeat)
                ]),
            new("random_attacks", PrototypeEnemyAiStateKind.Random,
                Branches:
                [
                    new("swipe", 1,
                        PrototypeEnemyAiRepeatRule.CannotRepeat),
                    new("spew_coins", 1,
                        PrototypeEnemyAiRepeatRule.CannotRepeat),
                    new("throw_relic", 1,
                        PrototypeEnemyAiRepeatRule.CannotRepeat)
                ])
        ]))
    ];

    public static PrototypeEncounterDefinition Encounter { get; } = new(
        EncounterId,
        PrototypeRoomType.Combat,
        [MonsterId],
        MinAct: 2,
        MaxAct: 3,
        Weight: 0,
        Formation: [new(MonsterId, 0, "merchant")]);

    public static PrototypeEncounterDefinition[] Encounters { get; } =
        [Encounter];

    public static string ChoiceId(string relicId) => "buy_" + relicId;

    private static string RelicDisplayName(string relicId) => relicId switch
    {
        FakeAnchorId => "Fake Anchor",
        FakeBloodVialId => "Fake Blood Vial",
        FakeHappyFlowerId => "Fake Happy Flower",
        FakeLeesWaffleId => "Fake Lee's Waffle",
        FakeMangoId => "Fake Mango",
        FakeOrichalcumId => "Fake Orichalcum",
        FakeSneckoEyeId => "Fake Snecko Eye",
        FakeStrikeDummyId => "Fake Strike Dummy",
        FakeTeaSetId => "Fake Venerable Tea Set",
        _ => throw new ArgumentOutOfRangeException(nameof(relicId))
    };

    public static string RelicForChoice(string choiceId) =>
        StockRelicIds.Single(id => ChoiceId(id) == choiceId);

    public static bool IsEligible(PlayerState player) =>
        player.Gold >= 100
        || player.PotionSlots.Any(potion =>
            potion?.PotionId == FoulPotionId);

    public static EventState RollInventory(EventState eventState, RngBundle rng)
    {
        var ids = (string[])StockRelicIds.Clone();
        _ = LegacyFisherYatesV111.ShuffleInPlace(
            ids,
            rng,
            "event",
            "event.fake_merchant.relic_offer.shuffle_swap_index",
            "event:" + eventState.EventId + ":stock_relics");
        return eventState with
        {
            OfferedChoiceIds = ids.Take(6)
                .Select(ChoiceId)
                .Append(LeaveChoiceId)
                .ToArray()
        };
    }

    public static bool HasFoulPotion(PlayerState player) =>
        player.PotionSlots.Any(potion => potion?.PotionId == FoulPotionId);

    public static bool HasTeaSetCharge(RelicInstance relic) =>
        relic.RelicId == FakeTeaSetId
        && !relic.IsMelted
        && relic.PersistentState.ValueKind == JsonValueKind.Object
        && relic.PersistentState.TryGetProperty(
            "GainEnergyInNextCombat", out var value)
        && value.ValueKind == JsonValueKind.True;

    public static (PlayerState Player, int Bonus) ConsumeTeaSetCharges(
        PlayerState player)
    {
        var bonus = 0;
        var relics = player.Relics.ToArray();
        for (var i = 0; i < relics.Length; i++)
        {
            if (!HasTeaSetCharge(relics[i]))
                continue;
            bonus++;
            var persistent = JsonNode.Parse(
                relics[i].PersistentState.GetRawText()) as JsonObject
                ?? new JsonObject();
            persistent["GainEnergyInNextCombat"] = false;
            relics[i] = relics[i] with
            {
                PersistentState = JsonSerializer.SerializeToElement(persistent)
            };
        }
        return (player with { Relics = relics }, bonus);
    }

    public static PlayerState MarkTeaSetsAfterRestSite(PlayerState player)
    {
        var relics = player.Relics.ToArray();
        var changed = false;
        for (var i = 0; i < relics.Length; i++)
        {
            if (relics[i].RelicId != FakeTeaSetId || relics[i].IsMelted)
                continue;
            var persistent = relics[i].PersistentState.ValueKind == JsonValueKind.Object
                ? JsonNode.Parse(relics[i].PersistentState.GetRawText()) as JsonObject
                    ?? new JsonObject()
                : new JsonObject();
            persistent["GainEnergyInNextCombat"] = true;
            relics[i] = relics[i] with
            {
                PersistentState = JsonSerializer.SerializeToElement(persistent)
            };
            changed = true;
        }
        return changed ? player with { Relics = relics } : player;
    }
}

public sealed partial class PrototypeGameEngine
{
    private static RunState StepNativeFakeMerchant(
        RunState state, GameAction action)
    {
        var world = RequireWorld(state);
        var evt = world.Event
            ?? throw new InvalidOperationException(
                "Fake Merchant has no event state.");
        if (evt.EventId != PrototypeNativeFakeMerchant.EventId)
            throw new InvalidOperationException(
                "Unexpected Fake Merchant event state.");

        if (action.Kind == PrototypeNativeFakeMerchant.ThrowPotionAction)
        {
            var payload = action.ReadPayload<UsePotionPayload>();
            if (payload.Slot < 0
                || payload.Slot >= state.Player.PotionSlots.Length
                || state.Player.PotionSlots[payload.Slot]?.PotionId
                    != PrototypeNativeFakeMerchant.FoulPotionId)
                throw new InvalidOperationException(
                    "The selected slot does not contain a Foul Potion.");

            var slots = (PotionInstance?[])state.Player.PotionSlots.Clone();
            slots[payload.Slot] = null;
            var encounter = PrototypeContent.Encounter(
                PrototypeNativeFakeMerchant.EncounterId);
            return StartCombat(state with
            {
                Player = state.Player with { PotionSlots = slots }
            }, PrototypeRoomType.Combat, encounter);
        }

        RequireKind(action, "event_choice");
        var choiceId = action.ReadPayload<EventChoicePayload>().ChoiceId;
        if (choiceId == PrototypeNativeFakeMerchant.LeaveChoiceId)
            return CompleteRoomToMap(state);

        var offered = evt.OfferedChoiceIds ?? Array.Empty<string>();
        if (!offered.Contains(choiceId, StringComparer.Ordinal))
            throw new InvalidOperationException(
                $"Fake Merchant offer '{choiceId}' is not stocked.");
        var relicId = PrototypeNativeFakeMerchant.RelicForChoice(choiceId);
        if (state.Player.Gold < PrototypeNativeFakeMerchant.RelicCost)
            throw new InvalidOperationException(
                "Fake Merchant relic costs 50 gold.");

        var purchased = new RelicInstance(
            relicId, PrototypeJson.EmptyObject());
        var player = state.Player with
        {
            Gold = state.Player.Gold - PrototypeNativeFakeMerchant.RelicCost,
            Relics = state.Player.Relics.Append(purchased).ToArray()
        };
        player = ApplyRelicRunEvent(player,
            PrototypeRunEventKind.RelicAcquired,
            acquiredRelicId: relicId,
            rng: state.Rng);
        return state with
        {
            Player = player,
            World = world with
            {
                Event = evt with
                {
                    OfferedChoiceIds = offered
                        .Where(id => id != choiceId).ToArray()
                }
            }
        };
    }

    private static RunState CompleteNativeFakeMerchant(
        RunState state, CombatState combat)
    {
        var world = RequireWorld(state);
        var evt = world.Event
            ?? throw new InvalidOperationException(
                "Fake Merchant fight has no originating event.");
        if (evt.EventId != PrototypeNativeFakeMerchant.EventId)
            throw new InvalidOperationException(
                "Unexpected Fake Merchant combat completion.");

        var stockedIds = (evt.OfferedChoiceIds ?? Array.Empty<string>())
            .Where(id => id != PrototypeNativeFakeMerchant.LeaveChoiceId)
            .Select(PrototypeNativeFakeMerchant.RelicForChoice)
            .ToArray();
        var rewards = new[] { PrototypeNativeFakeMerchant.RugId }
            .Concat(stockedIds).ToArray();
        return state with
        {
            World = world with
            {
                Combat = null,
                Reward = new RewardState(
                    "Event:" + evt.EventId,
                    [], null, null,
                    CardResolved: true,
                    PotionResolved: true,
                    RelicResolved: true,
                    EndsAct: false,
                    ExtraRelicRewardIds: rewards,
                    GoldOption: 300,
                    GoldResolved: false,
                    IndependentSelection: true,
                    ExtraRelicGroupsResolved: new bool[rewards.Length])
            },
            Phase = RunPhase.Reward
        };
    }
}
