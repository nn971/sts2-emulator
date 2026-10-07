using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypePhrogParasiteTests
{
    [Fact]
    public void DefinitionsMatchPinnedOvergrowthData()
    {
        var phrog = PrototypeContent.Enemy(
            "proto.enemy.phrog_parasite");
        Assert.Equal((61, 64), phrog.HpRangeAt(1, 0));
        Assert.Equal((66, 68), phrog.HpRangeAt(1, 8));
        Assert.Collection(
            phrog.Moves,
            infect =>
            {
                Assert.Equal("infect", infect.Id);
                var effect = Assert.Single(infect.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.AddCardsToDiscard,
                    effect.Kind);
                Assert.Equal("proto.status.infection", effect.CardId);
                Assert.Equal(3, effect.Amount);
            },
            lash =>
            {
                Assert.Equal("lash", lash.Id);
                var damage = Assert.Single(lash.Effects);
                Assert.Equal(4, damage.Repetitions);
                Assert.Equal(4, damage.AmountAt(1, 8));
                Assert.Equal(5, damage.AmountAt(1, 9));
            });
        Assert.Equal(4, phrog.DeathSummons!.Length);

        var wriggler = PrototypeContent.Enemy(
            "proto.enemy.wriggler");
        Assert.Equal((17, 21), wriggler.HpRangeAt(1, 0));
        Assert.Equal((18, 22), wriggler.HpRangeAt(1, 8));
        Assert.Collection(
            wriggler.Moves,
            bite =>
            {
                Assert.Equal("nasty_bite", bite.Id);
                var damage = Assert.Single(bite.Effects);
                Assert.Equal(6, damage.AmountAt(1, 8));
                Assert.Equal(7, damage.AmountAt(1, 9));
            },
            wriggle =>
            {
                Assert.Equal("wriggle", wriggle.Id);
                Assert.Collection(
                    wriggle.Effects,
                    infection =>
                    {
                        Assert.Equal(
                            PrototypeEnemyEffectKind.AddCardsToDiscard,
                            infection.Kind);
                        Assert.Equal(
                            "proto.status.infection",
                            infection.CardId);
                        Assert.Equal(1, infection.Amount);
                    },
                    strength =>
                    {
                        Assert.Equal(
                            PrototypeEnemyEffectKind.ApplyEnemyPower,
                            strength.Kind);
                        Assert.Equal(
                            "proto.power.strength",
                            strength.PowerId);
                        Assert.Equal(2, strength.Amount);
                    });
            });

        var infection = PrototypeContent.Card(
            "proto.status.infection");
        Assert.True(infection.Unplayable);
        Assert.Equal(PrototypeCardType.Status, infection.Type);
        Assert.Equal(3, infection.EndTurnDamageIfInHand);
    }

    [Fact]
    public void InfectionDamageIsBlockableBeforeHandDiscard()
    {
        var infection1 = Card(
            1,
            "proto.status.infection");
        var infection2 = Card(
            2,
            "proto.status.infection");
        var filler = Enumerable.Range(3, 5)
            .Select(id =>
                Card(id, "proto.silent.strike"))
            .ToArray();
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.bygone_effigy",
                    127,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            cards: new[] { infection1, infection2 }
                .Concat(filler)
                .ToArray(),
            hand: [1, 2],
            drawPile: filler
                .Select(card => card.InstanceId)
                .ToArray(),
            playerBlock: 4);

        state = EndTurn(engine, state);

        Assert.Equal(98, state.Player.Hp);
        Assert.DoesNotContain(1, state.World!.Combat!.Hand);
        Assert.DoesNotContain(2, state.World.Combat.Hand);
        Assert.Contains(1, state.World.Combat.DiscardPile);
        Assert.Contains(2, state.World.Combat.DiscardPile);
    }

    [Fact]
    public void PhrogStrictlyAlternatesInfectAndLash()
    {
        var filler = Enumerable.Range(1, 10)
            .Select(id =>
                Card(id, "proto.silent.strike"))
            .ToArray();
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            enemies:
            [
                Phrog(1, hp: 64)
            ],
            cards: filler,
            drawPile: filler
                .Select(card => card.InstanceId)
                .ToArray());

        state = EndTurn(engine, state);
        Assert.Equal("infect", PhrogState(state).LastMoveId);
        Assert.Equal(
            3,
            state.World!.Combat!.Cards.Count(card =>
                card.CardId == "proto.status.infection"));
        Assert.Equal(100, state.Player.Hp);

        state = EndTurn(engine, state);
        Assert.Equal("lash", PhrogState(state).LastMoveId);
        Assert.Equal(84, state.Player.Hp);

        state = EndTurn(engine, state);
        Assert.Equal("infect", PhrogState(state).LastMoveId);
    }

    [Fact]
    public void KillingPhrogSpawnsFourStunnedOffsetWrigglers()
    {
        var strike = Card(
            1,
            "proto.silent.strike");
        var filler = Enumerable.Range(2, 11)
            .Select(id =>
                Card(id, "proto.silent.defend"))
            .ToArray();
        var allCards = new[] { strike }
            .Concat(filler)
            .ToArray();
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            enemies:
            [
                Phrog(1, hp: 6)
            ],
            cards: allCards,
            hand: [1],
            drawPile: filler
                .Select(card => card.InstanceId)
                .ToArray());

        var attack = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 1
                && action.ReadPayload<PlayCardPayload>()
                    .TargetEnemyId == 1);
        state = engine.Step(state, attack).State;

        var combat = state.World!.Combat!;
        Assert.Equal(
            0,
            combat.Enemies.Single(enemy =>
                enemy.InstanceId == 1).Hp);
        var wrigglers = combat.Enemies
            .Where(enemy =>
                enemy.EnemyId == "proto.enemy.wriggler")
            .OrderBy(enemy => enemy.FormationPosition)
            .ToArray();
        Assert.Equal(4, wrigglers.Length);
        Assert.Equal(
            new[] { "bite", "wriggle", "bite", "wriggle" },
            wrigglers.Select(enemy => enemy.SlotName));
        Assert.All(
            wrigglers,
            enemy => Assert.True(enemy.SkipNextEnemyAction));

        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        Assert.Equal(100, state.Player.Hp);
        Assert.DoesNotContain(
            combat.Cards,
            card => card.CardId == "proto.status.infection");

        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        Assert.Equal(88, state.Player.Hp);
        Assert.Equal(
            2,
            combat.Cards.Count(card =>
                card.CardId == "proto.status.infection"));

        var activeWrigglers = combat.Enemies
            .Where(enemy =>
                enemy.EnemyId == "proto.enemy.wriggler")
            .OrderBy(enemy => enemy.FormationPosition)
            .ToArray();
        Assert.Equal("nasty_bite", activeWrigglers[0].LastMoveId);
        Assert.Equal("wriggle", activeWrigglers[1].LastMoveId);
        Assert.Equal("nasty_bite", activeWrigglers[2].LastMoveId);
        Assert.Equal("wriggle", activeWrigglers[3].LastMoveId);
        Assert.Equal(
            2,
            activeWrigglers[1].PowerStates.Single(power =>
                power.PowerId == "proto.power.strength").Stacks);
        Assert.Equal(
            2,
            activeWrigglers[3].PowerStates.Single(power =>
                power.PowerId == "proto.power.strength").Stacks);
    }

    [Fact]
    public void A8DeathWaveRollsWrigglersFromElevatedHpRange()
    {
        var strike = Card(
            1,
            "proto.silent.strike");
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            enemies:
            [
                Phrog(1, hp: 6)
            ],
            cards: [strike],
            hand: [1],
            ascension: 8);

        var attack = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .TargetEnemyId == 1);
        state = engine.Step(state, attack).State;

        Assert.All(
            state.World!.Combat!.Enemies.Where(enemy =>
                enemy.EnemyId == "proto.enemy.wriggler"),
            enemy => Assert.InRange(enemy.Hp, 18, 22));
    }

    [Fact]
    public void ReferenceEliteStartsWithPhrogAlone()
    {
        var specs = PrototypeContent.Encounter(
                "proto.encounter.phrog_parasite_elite")
            .ResolveEnemySpecs(
                PrototypeRng.CreateBundle(
                    "phrog-formation"));

        var phrog = Assert.Single(specs);
        Assert.Equal("proto.enemy.phrog_parasite", phrog.EnemyId);
    }

    private static EnemyCombatState Phrog(
        int instanceId,
        int hp) =>
        new(
            instanceId,
            "proto.enemy.phrog_parasite",
            hp,
            0,
            0,
            new Dictionary<string, int>(
                StringComparer.Ordinal),
            Powers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.infested",
                    1,
                    1)
            ]);

    private static EnemyCombatState PhrogState(
        RunState state) =>
        state.World!.Combat!.Enemies.Single(enemy =>
            enemy.EnemyId == "proto.enemy.phrog_parasite");

    private static CombatCardInstance Card(
        long instanceId,
        string cardId) =>
        new(
            instanceId,
            null,
            cardId,
            0,
            true,
            PrototypeJson.EmptyObject());

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static RunState CreateState(
        EnemyCombatState[] enemies,
        CombatCardInstance[]? cards = null,
        long[]? hand = null,
        long[]? drawPile = null,
        int playerBlock = 0,
        int ascension = 0)
    {
        cards ??= [];
        hand ??= [];
        drawPile ??= [];

        var player = new PlayerState(
            100,
            100,
            0,
            [],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: playerBlock,
            Hand: hand,
            DrawPile: drawPile,
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: enemies,
            NextCardInstanceId:
                cards.Length == 0
                    ? 1
                    : cards.Max(card =>
                        card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 2,
            Act: 1,
            Ascension: ascension);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "phrog-test",
            "phrog-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "phrog-test"),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                6,
                5000,
                PrototypeRoomType.Elite,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null),
            Ascension: ascension);
    }
}
