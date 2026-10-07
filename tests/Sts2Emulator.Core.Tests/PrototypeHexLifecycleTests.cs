using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

[Collection("MutablePrototypeContent")]
public sealed class PrototypeHexLifecycleTests
{
    private const string HexSourceEnemyId =
        "proto.test.hex_source";

    [Fact]
    public void HexAfflictsAllExistingCardsAndSkipsOtherAfflictions()
    {
        WithHexSourceDefinition(() =>
        {
            var cards = new[]
            {
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.defend"),
                Card(3, "proto.silent.strike"),
                Card(4, "proto.status.dazed"),
                Card(5, "proto.silent.defend") with
                {
                    Affliction = new PrototypeCardAffliction(
                        PrototypeCardAfflictionKind.Entangled)
                }
            };

            var engine = new PrototypeGameEngine();
            var state = CreateState(
                hand: [cards[0]],
                drawPile: [cards[1], cards[4]],
                discardPile: [cards[2]],
                exhaustPile: [cards[3]],
                enemies:
                [
                    Enemy(
                        1,
                        HexSourceEnemyId,
                        hp: 10)
                ]);

            state = EndTurn(engine, state);

            var combat = state.World!.Combat!;
            var hex = Assert.Single(
                combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.hex");
            Assert.Equal(2, hex.Stacks);
            Assert.Equal(1, hex.SourceEnemyInstanceId);

            foreach (var instanceId in new long[]
                     {
                         1, 2, 3, 4
                     })
            {
                var affliction = Assert.IsType<
                    PrototypeCardAffliction>(
                    combat.Cards.Single(card =>
                        card.InstanceId == instanceId)
                    .Affliction);
                Assert.Equal(
                    PrototypeCardAfflictionKind.Hexed,
                    affliction.Kind);
                Assert.Equal(
                    1,
                    affliction.SourceEnemyInstanceId);
            }

            var preserved =
                combat.Cards.Single(card =>
                    card.InstanceId == 5)
                .Affliction;
            Assert.NotNull(preserved);
            Assert.Equal(
                PrototypeCardAfflictionKind.Entangled,
                preserved!.Kind);
            Assert.Null(
                preserved.SourceEnemyInstanceId);
        });
    }

    [Fact]
    public void CardsGeneratedWhileHexIsActiveBecomeHexed()
    {
        WithHexSourceDefinition(() =>
        {
            var initialHand =
                Card(1, "proto.silent.strike");
            var bladeDance =
                Card(6, "proto.silent.blade_dance");
            var drawPile = new[]
            {
                Card(2, "proto.silent.strike"),
                Card(3, "proto.silent.defend"),
                Card(4, "proto.silent.strike"),
                Card(5, "proto.silent.defend"),
                bladeDance
            };

            var engine = new PrototypeGameEngine();
            var state = CreateState(
                hand: [initialHand],
                drawPile: drawPile,
                discardPile: [],
                exhaustPile: [],
                enemies:
                [
                    Enemy(
                        1,
                        HexSourceEnemyId,
                        hp: 10)
                ]);

            state = EndTurn(engine, state);
            var combat = state.World!.Combat!;
            Assert.Contains(
                bladeDance.InstanceId,
                combat.Hand);

            state = PlayCard(
                engine,
                state,
                bladeDance.InstanceId);

            combat = state.World!.Combat!;
            var generatedShivs = combat.Cards
                .Where(card =>
                    card.IsTemporary
                    && card.CardId
                        == "proto.silent.shiv")
                .ToArray();

            Assert.Equal(3, generatedShivs.Length);
            Assert.All(
                generatedShivs,
                shiv =>
                {
                    var affliction = Assert.IsType<
                        PrototypeCardAffliction>(
                        shiv.Affliction);
                    Assert.Equal(
                        PrototypeCardAfflictionKind.Hexed,
                        affliction.Kind);
                    Assert.Equal(
                        1,
                        affliction.SourceEnemyInstanceId);
                });
        });
    }

    [Fact]
    public void KillingHexSourceClearsOnlyItsHexedAfflictions()
    {
        WithHexSourceDefinition(() =>
        {
            var initial = Card(
                1,
                "proto.silent.defend");
            var strike = Card(
                2,
                "proto.silent.strike");
            var dazed = Card(
                3,
                "proto.status.dazed");
            var defend = Card(
                4,
                "proto.silent.defend");
            var entangled = Card(
                5,
                "proto.silent.defend") with
            {
                Affliction = new PrototypeCardAffliction(
                    PrototypeCardAfflictionKind.Entangled)
            };
            var filler = Card(
                6,
                "proto.silent.strike");

            var engine = new PrototypeGameEngine();
            var state = CreateState(
                hand: [initial],
                drawPile:
                [
                    filler,
                    entangled,
                    defend,
                    dazed,
                    strike
                ],
                discardPile: [],
                exhaustPile: [],
                enemies:
                [
                    Enemy(
                        1,
                        HexSourceEnemyId,
                        hp: 1),
                    Enemy(
                        2,
                        "proto.enemy.crawler",
                        hp: 999)
                ]);

            state = EndTurn(engine, state);
            var combat = state.World!.Combat!;
            Assert.Contains(
                combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.hex");

            state = PlayCard(
                engine,
                state,
                strike.InstanceId,
                targetEnemyId: 1);

            combat = state.World!.Combat!;
            Assert.DoesNotContain(
                combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.hex");

            foreach (var card in combat.Cards)
            {
                Assert.False(
                    card.Affliction is
                    {
                        Kind:
                            PrototypeCardAfflictionKind.Hexed,
                        SourceEnemyInstanceId: 1
                    });
            }

            var preserved = combat.Cards.Single(
                card => card.InstanceId
                    == entangled.InstanceId);
            Assert.NotNull(preserved.Affliction);
            Assert.Equal(
                PrototypeCardAfflictionKind.Entangled,
                preserved.Affliction!.Kind);

            Assert.Contains(
                dazed.InstanceId,
                combat.Hand);
            Assert.Contains(
                defend.InstanceId,
                combat.Hand);

            state = EndTurn(engine, state);
            combat = state.World!.Combat!;

            // Dazed keeps its intrinsic Ethereal after Hexed is removed.
            Assert.Contains(
                dazed.InstanceId,
                combat.ExhaustPile);

            // Ordinary Defend is no longer Ethereal once Hex is gone.
            Assert.Contains(
                defend.InstanceId,
                combat.DiscardPile);
        });
    }

    [Fact]
    public void ReapplyingNonStackingHexDoesNotIncreaseStacks()
    {
        WithHexSourceDefinition(() =>
        {
            var engine = new PrototypeGameEngine();
            var state = CreateState(
                hand: [],
                drawPile: [],
                discardPile: [],
                exhaustPile: [],
                enemies:
                [
                    Enemy(
                        1,
                        HexSourceEnemyId,
                        hp: 10)
                ]);

            state = EndTurn(engine, state);
            state = EndTurn(engine, state);

            var hex = Assert.Single(
                state.World!.Combat!.PlayerPowers,
                power => power.PowerId
                    == "proto.power.hex");
            Assert.Equal(2, hex.Stacks);
            Assert.Equal(1, hex.SourceEnemyInstanceId);
        });
    }

    private static void WithHexSourceDefinition(
        Action action)
    {
        var enemies = Assert.IsType<
            Dictionary<string, PrototypeEnemyDefinition>>(
                PrototypeContent.Enemies);

        enemies.Add(
            HexSourceEnemyId,
            new PrototypeEnemyDefinition(
                HexSourceEnemyId,
                "Hex Source Fixture",
                10,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "hex",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind
                                    .ApplyPlayerPower,
                                2,
                                PowerId: "proto.power.hex")
                        ])
                ]));

        try
        {
            action();
        }
        finally
        {
            enemies.Remove(HexSourceEnemyId);
        }
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static RunState PlayCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId,
        int? targetEnemyId = null)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
            {
                if (item.Kind != "play_card")
                {
                    return false;
                }

                var payload =
                    item.ReadPayload<PlayCardPayload>();
                return payload.CardInstanceId
                        == cardInstanceId
                    && payload.TargetEnemyId
                        == targetEnemyId;
            });
        return engine.Step(state, action).State;
    }

    private static CombatCardInstance Card(
        long instanceId,
        string cardId) =>
        new(
            instanceId,
            1000 + instanceId,
            cardId,
            0,
            false,
            PrototypeJson.EmptyObject());

    private static EnemyCombatState Enemy(
        int instanceId,
        string enemyId,
        int hp) =>
        new(
            instanceId,
            enemyId,
            hp,
            0,
            0,
            new Dictionary<string, int>(
                StringComparer.Ordinal));

    private static RunState CreateState(
        CombatCardInstance[] hand,
        CombatCardInstance[] drawPile,
        CombatCardInstance[] discardPile,
        CombatCardInstance[] exhaustPile,
        EnemyCombatState[] enemies)
    {
        var cards = hand
            .Concat(drawPile)
            .Concat(discardPile)
            .Concat(exhaustPile)
            .ToArray();
        var empty = PrototypeJson.EmptyObject();

        var player = new PlayerState(
            70,
            70,
            0,
            cards.Select(card => new CardInstance(
                card.PersistentCardInstanceId!.Value,
                card.CardId,
                card.UpgradeLevel,
                card.State.Clone())).ToArray(),
            Array.Empty<RelicInstance>(),
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: hand.Select(
                card => card.InstanceId).ToArray(),
            DrawPile: drawPile.Select(
                card => card.InstanceId).ToArray(),
            DiscardPile: discardPile.Select(
                card => card.InstanceId).ToArray(),
            ExhaustPile: exhaustPile.Select(
                card => card.InstanceId).ToArray(),
            Enemies: enemies,
            NextCardInstanceId:
                cards.Length == 0
                    ? 1
                    : cards.Max(
                        card => card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "hex-lifecycle-test",
            "hex-lifecycle-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "hex-lifecycle-test"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                2,
                1,
                5000,
                PrototypeRoomType.Elite,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null));
    }
}
