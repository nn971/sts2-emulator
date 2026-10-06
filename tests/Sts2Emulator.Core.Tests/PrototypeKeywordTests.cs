using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeKeywordTests
{
    [Fact]
    public void PinnedBackstabAndSnakebiteExposeNativeKeywordFlags()
    {
        var backstab = PrototypeContent.Card("proto.silent.backstab");
        Assert.True(backstab.Innate);
        Assert.True(backstab.ExhaustOnUse);
        Assert.Equal(0, backstab.Cost.Amount);
        Assert.Equal(11, Assert.Single(backstab.Effects).Amount);

        var snakebite = PrototypeContent.Card("proto.silent.snakebite");
        Assert.True(snakebite.Retain);
        Assert.Equal(2, snakebite.Cost.Amount);
        var poison = Assert.Single(snakebite.Effects);
        Assert.Equal(PrototypeCombatEffectKind.ApplyEnemyStatus, poison.Kind);
        Assert.Equal(7, poison.Amount);
        Assert.Equal(3, poison.UpgradeDelta);
    }

    [Fact]
    public void InnateCardStartsInOpeningHandWithoutIncreasingOpeningHandTarget()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create("innate-opening-hand");

        state = engine.Step(
            state,
            Assert.Single(engine.GetLegalActions(state))).State;

        var empty = PrototypeJson.EmptyObject();
        var world = state.World!;
        var backstabPersistentId = world.NextCardInstanceId;
        state = state with
        {
            Player = state.Player with
            {
                Deck = state.Player.Deck
                    .Append(new CardInstance(
                        backstabPersistentId,
                        "proto.silent.backstab",
                        0,
                        empty))
                    .ToArray()
            },
            World = world with
            {
                NextCardInstanceId = backstabPersistentId + 1,
                Map = new MapState(
                    [
                        new MapNodeState(
                            "keyword-combat",
                            1,
                            1,
                            PrototypeRoomType.Combat)
                    ],
                    EntryNodeIds: ["keyword-combat"])
            }
        };

        state = engine.Step(
            state,
            GameAction.Create(
                "choose_map_node",
                new ChooseMapNodePayload("keyword-combat"))).State;

        var combat = state.World!.Combat!;
        var backstab = Assert.Single(
            combat.Cards,
            card => card.CardId == "proto.silent.backstab");

        Assert.Contains(backstab.InstanceId, combat.Hand);
        Assert.Equal(
            PrototypeContent.Rules.HandSize
                + PrototypeContent.Relic("proto.relic.silent_ring").FirstTurnDrawBonus,
            combat.Hand.Length);
    }

    [Fact]
    public void RetainKeepsCardInHandAcrossEndTurn()
    {
        var state = CreateCombatState(
            hand:
            [
                new CombatCardInstance(
                    1,
                    101,
                    "proto.silent.snakebite",
                    0,
                    false,
                    PrototypeJson.EmptyObject()),
                new CombatCardInstance(
                    2,
                    102,
                    "proto.silent.strike",
                    0,
                    false,
                    PrototypeJson.EmptyObject())
            ],
            drawPile: [3, 4, 5, 6, 7]);

        var engine = new PrototypeGameEngine();
        state = engine.Step(state, GameAction.Empty("end_turn")).State;

        var combat = state.World!.Combat!;
        Assert.Contains(1, combat.Hand);
        Assert.Contains(2, combat.DiscardPile);
    }

    private static RunState CreateCombatState(
        CombatCardInstance[] hand,
        long[] drawPile)
    {
        var empty = PrototypeJson.EmptyObject();
        var cards = hand.ToList();
        foreach (var instanceId in drawPile)
        {
            cards.Add(new CombatCardInstance(
                instanceId,
                1000 + instanceId,
                "proto.silent.defend",
                0,
                false,
                empty));
        }

        var player = new PlayerState(
            Hp: 70,
            MaxHp: 70,
            Gold: 0,
            Deck: cards
                .Select(card => new CardInstance(
                    card.PersistentCardInstanceId ?? card.InstanceId,
                    card.CardId,
                    card.UpgradeLevel,
                    empty))
                .ToArray(),
            Relics: Array.Empty<RelicInstance>(),
            PotionSlots: new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: hand.Select(card => card.InstanceId).ToArray(),
            DrawPile: drawPile,
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    999,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: cards.Max(card => card.InstanceId) + 1,
            Cards: cards.ToArray(),
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: 1);

        return new RunState(
            GameBuild: "prototype-unbound",
            EmulatorSchema: "prototype-0.1",
            RunId: "keyword-test",
            RunSeed: "keyword-test",
            DecisionIndex: 0,
            Phase: RunPhase.Combat,
            Player: player,
            Rng: PrototypeRng.CreateBundle("keyword-test"),
            ExtensionState: empty,
            World: new RunWorldState(
                RulesetId: PrototypeContent.RulesetId,
                CharacterId: PrototypeContent.CharacterId,
                Act: 1,
                Floor: 1,
                NextCardInstanceId: 2000,
                ActiveRoom: PrototypeRoomType.Combat,
                Map: new MapState(Array.Empty<MapNodeState>()),
                Combat: combat,
                Reward: null,
                Shop: null,
                Event: null,
                TerminalOutcome: null));
    }
}
