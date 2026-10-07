using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeLifecycleRelicPotionTests
{
    [Fact]
    public void CombatStartRelicsApplyBlockStrengthAndHealing()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create("lifecycle-start");
        state = engine.Step(
            state,
            Assert.Single(engine.GetLegalActions(state))).State;

        var empty = PrototypeJson.EmptyObject();
        state = state with
        {
            Player = state.Player with
            {
                Hp = 50,
                Relics:
                [
                    new RelicInstance("proto.relic.anchor", empty),
                    new RelicInstance("proto.relic.vajra", empty),
                    new RelicInstance("proto.relic.blood_vial", empty)
                ]
            },
            World = state.World! with
            {
                Map = new MapState(
                    [
                        new MapNodeState(
                            "test-combat",
                            1,
                            1,
                            PrototypeRoomType.Combat,
                            [])
                    ],
                    EntryNodeIds: ["test-combat"])
            }
        };

        state = engine.Step(
            state,
            GameAction.Create(
                "choose_map_node",
                new ChooseMapNodePayload("test-combat"))).State;

        Assert.Equal(RunPhase.Combat, state.Phase);
        Assert.Equal(52, state.Player.Hp);
        Assert.Equal(10, state.World!.Combat!.PlayerBlock);
        var strength = Assert.Single(
            state.World.Combat.PlayerPowers,
            power => power.PowerId == "proto.power.strength");
        Assert.Equal(1, strength.Stacks);
    }

    [Fact]
    public void StrengthPotionAddsGenericAttackDamage()
    {
        var state = CombatStateWith(
            hp: 70,
            maxHp: 70,
            cards:
            [
                Card(1, "proto.silent.strike")
            ],
            hand: [1],
            enemies: [Enemy(1, 20)],
            potionId: "proto.potion.strength");
        var engine = new PrototypeGameEngine();

        var potion = engine.GetLegalActions(state)
            .Single(action => action.Kind == "use_potion");
        state = engine.Step(state, potion).State;

        var strength = Assert.Single(
            state.World!.Combat!.PlayerPowers,
            power => power.PowerId == "proto.power.strength");
        Assert.Equal(2, strength.Stacks);

        var strike = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 1);
        state = engine.Step(state, strike).State;

        Assert.Equal(
            12,
            state.World!.Combat!.Enemies
                .Single(enemy => enemy.InstanceId == 1).Hp);
    }

    [Fact]
    public void RegenPotionHealsThenDecaysAtNextTurnStart()
    {
        var state = CombatStateWith(
            hp: 1,
            maxHp: 70,
            cards:
            [
                Card(1, "proto.silent.defend")
            ],
            hand: [1],
            enemies: [Enemy(1, 100)],
            potionId: "proto.potion.regen",
            playerBlock: 999);
        var engine = new PrototypeGameEngine();

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "use_potion")).State;

        Assert.Equal(
            5,
            Assert.Single(
                state.World!.Combat!.PlayerPowers,
                power => power.PowerId == "proto.power.regen")
                .Stacks);

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;

        Assert.Equal(6, state.Player.Hp);
        Assert.Equal(
            4,
            Assert.Single(
                state.World!.Combat!.PlayerPowers,
                power => power.PowerId == "proto.power.regen")
                .Stacks);
    }

    [Fact]
    public void MeatOnTheBoneHealsOnVictoryAtOrBelowHalfHp()
    {
        var state = CombatStateWith(
            hp: 30,
            maxHp: 70,
            cards:
            [
                Card(1, "proto.silent.strike")
            ],
            hand: [1],
            enemies: [Enemy(1, 1)],
            relicId: "proto.relic.meat_on_the_bone");
        var engine = new PrototypeGameEngine();

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "play_card")).State;

        Assert.Equal(RunPhase.Reward, state.Phase);
        Assert.Equal(42, state.Player.Hp);
    }

    [Fact]
    public void MeatOnTheBoneDoesNotHealAboveHalfHp()
    {
        var state = CombatStateWith(
            hp: 40,
            maxHp: 70,
            cards:
            [
                Card(1, "proto.silent.strike")
            ],
            hand: [1],
            enemies: [Enemy(1, 1)],
            relicId: "proto.relic.meat_on_the_bone");
        var engine = new PrototypeGameEngine();

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "play_card")).State;

        Assert.Equal(RunPhase.Reward, state.Phase);
        Assert.Equal(40, state.Player.Hp);
    }

    private static CombatCardInstance Card(
        long id,
        string cardId) =>
        new(
            id,
            1000 + id,
            cardId,
            0,
            false,
            PrototypeJson.EmptyObject());

    private static EnemyCombatState Enemy(
        int id,
        int hp) =>
        new(
            id,
            "proto.enemy.crawler",
            hp,
            0,
            0,
            new Dictionary<string, int>(
                StringComparer.Ordinal));

    private static RunState CombatStateWith(
        int hp,
        int maxHp,
        CombatCardInstance[] cards,
        long[] hand,
        EnemyCombatState[] enemies,
        string? potionId = null,
        string? relicId = null,
        int playerBlock = 0)
    {
        var empty = PrototypeJson.EmptyObject();
        var potionSlots =
            new PotionInstance?[PrototypeContent.Rules.PotionSlots];
        CombatPotionState[] combatPotions = [];
        if (potionId is not null)
        {
            potionSlots[0] =
                new PotionInstance(potionId, empty);
            combatPotions =
            [
                new CombatPotionState(0, potionId, empty)
            ];
        }

        RelicInstance[] relics = [];
        CombatRelicState[] combatRelics = [];
        if (relicId is not null)
        {
            relics =
            [
                new RelicInstance(relicId, empty)
            ];
            var definition = PrototypeContent.Relic(relicId);
            combatRelics =
            [
                new CombatRelicState(
                    0,
                    relicId,
                    1,
                    new int[
                        (definition.Triggers
                            ?? Array.Empty<PrototypeRelicTriggerSpec>())
                        .Length])
            ];
        }

        var persistent = cards
            .Select(card => new CardInstance(
                card.PersistentCardInstanceId!.Value,
                card.CardId,
                card.UpgradeLevel,
                empty))
            .ToArray();
        var player = new PlayerState(
            hp,
            maxHp,
            0,
            persistent,
            relics,
            potionSlots);
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: playerBlock,
            Hand: hand,
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: enemies,
            NextCardInstanceId:
                cards.Max(card => card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder:
                combatRelics.Length + 1,
            Relics: combatRelics,
            Potions: combatPotions);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "lifecycle-content-test",
            "lifecycle-content-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "lifecycle-content-test"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                5000,
                PrototypeRoomType.Combat,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null));
    }
}
