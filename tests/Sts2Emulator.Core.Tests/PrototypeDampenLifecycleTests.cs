using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

[Collection("MutablePrototypeContent")]
public sealed class PrototypeDampenLifecycleTests
{
    private const string DampenSourceEnemyId =
        "proto.test.dampen_source";

    [Fact]
    public void DampenDowngradesExistingCardsAndRestoresOnSourceDeath()
    {
        WithDampenSourceDefinition(() =>
        {
            var tracked = Card(
                1,
                "proto.silent.defend",
                upgradeLevel: 2);
            var killer = Card(
                2,
                "proto.silent.strike");
            var filler = Card(
                3,
                "proto.silent.defend");

            var engine = new PrototypeGameEngine();
            var state = CreateState(
                hand: [filler],
                drawPile: [tracked, killer],
                enemies:
                [
                    Enemy(
                        1,
                        DampenSourceEnemyId,
                        hp: 1),
                    Enemy(
                        2,
                        "proto.enemy.crawler",
                        hp: 999)
                ]);

            state = EndTurn(engine, state);

            var combat = state.World!.Combat!;
            var dampen = Assert.Single(
                combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.dampen");
            Assert.Equal(1, dampen.SourceEnemyInstanceId);

            var downgraded = combat.Cards.Single(
                card => card.InstanceId
                    == tracked.InstanceId);
            Assert.Equal(0, downgraded.UpgradeLevel);
            Assert.Equal(
                2,
                downgraded.SuppressedUpgradeLevels);

            state = PlayCard(
                engine,
                state,
                killer.InstanceId,
                targetEnemyId: 1);

            combat = state.World!.Combat!;
            Assert.DoesNotContain(
                combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.dampen");

            var restored = combat.Cards.Single(
                card => card.InstanceId
                    == tracked.InstanceId);
            Assert.Equal(2, restored.UpgradeLevel);
            Assert.Equal(
                0,
                restored.SuppressedUpgradeLevels);
        });
    }

    [Fact]
    public void DampenWaitsForLastCasterBeforeRestoring()
    {
        WithDampenSourceDefinition(() =>
        {
            var tracked = Card(
                1,
                "proto.silent.defend",
                upgradeLevel: 1);
            var firstStrike = Card(
                2,
                "proto.silent.strike");
            var secondStrike = Card(
                3,
                "proto.silent.strike");
            var filler = Card(
                4,
                "proto.silent.defend");

            var engine = new PrototypeGameEngine();
            var state = CreateState(
                hand: [filler],
                drawPile:
                [
                    tracked,
                    firstStrike,
                    secondStrike
                ],
                enemies:
                [
                    Enemy(
                        1,
                        DampenSourceEnemyId,
                        hp: 1),
                    Enemy(
                        2,
                        DampenSourceEnemyId,
                        hp: 1),
                    Enemy(
                        3,
                        "proto.enemy.crawler",
                        hp: 999)
                ]);

            state = EndTurn(engine, state);

            var combat = state.World!.Combat!;
            Assert.Equal(
                2,
                combat.PlayerPowers.Count(
                    power => power.PowerId
                        == "proto.power.dampen"));
            Assert.Equal(
                0,
                combat.Cards.Single(card =>
                    card.InstanceId == tracked.InstanceId)
                    .UpgradeLevel);

            state = PlayCard(
                engine,
                state,
                firstStrike.InstanceId,
                targetEnemyId: 1);

            combat = state.World!.Combat!;
            var remaining = Assert.Single(
                combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.dampen");
            Assert.Equal(2, remaining.SourceEnemyInstanceId);

            var stillDowngraded = combat.Cards.Single(
                card => card.InstanceId
                    == tracked.InstanceId);
            Assert.Equal(
                0,
                stillDowngraded.UpgradeLevel);
            Assert.Equal(
                1,
                stillDowngraded.SuppressedUpgradeLevels);

            state = PlayCard(
                engine,
                state,
                secondStrike.InstanceId,
                targetEnemyId: 2);

            combat = state.World!.Combat!;
            Assert.DoesNotContain(
                combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.dampen");

            var restored = combat.Cards.Single(
                card => card.InstanceId
                    == tracked.InstanceId);
            Assert.Equal(1, restored.UpgradeLevel);
            Assert.Equal(
                0,
                restored.SuppressedUpgradeLevels);
        });
    }

    [Fact]
    public void ArtifactBlocksDampenBeforeAnyCardIsDowngraded()
    {
        WithDampenSourceDefinition(() =>
        {
            var upgraded = Card(
                1,
                "proto.silent.defend",
                upgradeLevel: 1);

            var engine = new PrototypeGameEngine();
            var state = CreateState(
                hand: [],
                drawPile: [upgraded],
                enemies:
                [
                    Enemy(
                        1,
                        DampenSourceEnemyId,
                        hp: 10)
                ],
                powers:
                [
                    new PrototypePowerInstanceState(
                        "proto.power.artifact",
                        1,
                        1)
                ]);

            state = EndTurn(engine, state);

            var combat = state.World!.Combat!;
            Assert.DoesNotContain(
                combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.dampen");
            Assert.DoesNotContain(
                combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.artifact");

            var card = combat.Cards.Single(
                item => item.InstanceId
                    == upgraded.InstanceId);
            Assert.Equal(1, card.UpgradeLevel);
            Assert.Equal(
                0,
                card.SuppressedUpgradeLevels);
        });
    }

    [Fact]
    public void NightmareCopiesRecoverStoredUpgradeAndEscapeDampen()
    {
        WithDampenSourceDefinition(() =>
        {
            var nightmare = Card(
                1,
                "proto.silent.nightmare");
            var selected = Card(
                2,
                "proto.silent.defend",
                upgradeLevel: 0) with
            {
                SuppressedUpgradeLevels = 1
            };
            var drawPile = Enumerable.Range(3, 5)
                .Select(id =>
                    Card(id, "proto.silent.strike"))
                .ToArray();

            var engine = new PrototypeGameEngine();
            var state = CreateState(
                hand: [nightmare, selected],
                drawPile: drawPile,
                enemies:
                [
                    Enemy(
                        1,
                        DampenSourceEnemyId,
                        hp: 10)
                ],
                powers:
                [
                    new PrototypePowerInstanceState(
                        "proto.power.dampen",
                        1,
                        1,
                        SourceEnemyInstanceId: 1)
                ]);

            state = PlayCard(
                engine,
                state,
                nightmare.InstanceId);
            state = SelectOnly(
                engine,
                state,
                selected.InstanceId);

            var payload = Assert.Single(
                state.World!.Combat!.PlayerPowers,
                power => power.PowerId
                    == "proto.power.nightmare")
                .CardPayload!;

            Assert.Equal(1, payload.UpgradeLevel);
            Assert.Equal(
                0,
                payload.SuppressedUpgradeLevels);

            state = EndTurn(engine, state);

            var combat = state.World!.Combat!;
            var generated = combat.Cards
                .Where(card =>
                    card.IsTemporary
                    && card.CardId
                        == "proto.silent.defend")
                .ToArray();

            Assert.Equal(3, generated.Length);
            Assert.All(
                generated,
                card =>
                {
                    Assert.Equal(1, card.UpgradeLevel);
                    Assert.Equal(
                        0,
                        card.SuppressedUpgradeLevels);
                });

            var original = combat.Cards.Single(
                card => card.InstanceId
                    == selected.InstanceId);
            Assert.Equal(0, original.UpgradeLevel);
            Assert.Equal(
                1,
                original.SuppressedUpgradeLevels);
        });
    }

    private static void WithDampenSourceDefinition(
        Action action)
    {
        var enemies = Assert.IsType<
            Dictionary<string, PrototypeEnemyDefinition>>(
                PrototypeContent.Enemies);

        enemies.Add(
            DampenSourceEnemyId,
            new PrototypeEnemyDefinition(
                DampenSourceEnemyId,
                "Dampen Source Fixture",
                10,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "dampen",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind
                                    .ApplyPlayerPower,
                                1,
                                PowerId: "proto.power.dampen")
                        ])
                ]));

        try
        {
            action();
        }
        finally
        {
            enemies.Remove(DampenSourceEnemyId);
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

    private static RunState SelectOnly(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "select_cards"
                && item.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual(
                        [cardInstanceId]));
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
        string cardId,
        int upgradeLevel = 0) =>
        new(
            instanceId,
            1000 + instanceId,
            cardId,
            upgradeLevel,
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
        EnemyCombatState[] enemies,
        PrototypePowerInstanceState[]? powers = null)
    {
        powers ??= [];
        var cards = hand.Concat(drawPile).ToArray();
        var empty = PrototypeJson.EmptyObject();

        var player = new PlayerState(
            70,
            70,
            0,
            cards.Select(card => new CardInstance(
                card.PersistentCardInstanceId!.Value,
                card.CardId,
                card.UpgradeLevel
                    + card.SuppressedUpgradeLevels,
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
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: enemies,
            NextCardInstanceId:
                cards.Length == 0
                    ? 1
                    : cards.Max(
                        card => card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: powers,
            NextPowerApplicationOrder:
                powers.Length == 0
                    ? 1
                    : powers.Max(
                        power => power.ApplicationOrder) + 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "dampen-lifecycle-test",
            "dampen-lifecycle-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "dampen-lifecycle-test"),
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
