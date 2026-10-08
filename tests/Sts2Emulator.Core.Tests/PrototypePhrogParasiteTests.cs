using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypePhrogParasiteTests
{
    [Fact]
    public void DefinitionMatchesPinnedActOneData()
    {
        var phrog = PrototypeContent.Enemy(
            "proto.enemy.phrog_parasite");

        Assert.Equal("Phrog Parasite", phrog.Name);
        Assert.Equal((61, 64), phrog.HpRangeAt(1, 0));
        Assert.Equal((66, 68), phrog.HpRangeAt(1, 8));

        Assert.Collection(
            phrog.Moves,
            infect =>
            {
                Assert.Equal("infect", infect.Id);
                var add = Assert.Single(infect.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.AddCardsToDiscard,
                    add.Kind);
                Assert.Equal(3, add.Amount);
                Assert.Equal("proto.status.infection", add.CardId);
            },
            lash =>
            {
                Assert.Equal("lash", lash.Id);
                var damage = Assert.Single(lash.Effects);
                Assert.Equal(4, damage.AmountAt(1, 8));
                Assert.Equal(5, damage.AmountAt(1, 9));
                Assert.Equal(4, damage.Repetitions);
            });

        var infested = Assert.Single(phrog.StartingPowers!);
        Assert.Equal("proto.power.infested", infested.PowerId);

        Assert.Collection(
            phrog.DeathSummons!,
            first => AssertWrigglerSummon(first, 0, "wriggler1"),
            second => AssertWrigglerSummon(second, 1, "wriggler2"),
            third => AssertWrigglerSummon(third, 2, "wriggler3"),
            fourth => AssertWrigglerSummon(fourth, 3, "wriggler4"));
    }

    [Fact]
    public void ParasiteAlternatesInfectAndLash()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            hp: 64);

        state = EndTurn(engine, state);
        Assert.Equal(100, state.Player.Hp);
        Assert.Equal(
            "infect",
            Phrog(state).LastMoveId);
        Assert.Equal(
            3,
            state.World!.Combat!.Cards.Count(
                card => card.CardId == "proto.status.infection"));

        state = EndTurn(engine, state);
        Assert.Equal(75, state.Player.Hp);
        Assert.Equal(
            "lash",
            Phrog(state).LastMoveId);
    }

    [Fact]
    public void PlayerTurnDeathStunsWrigglersForUpcomingEnemyTurnOnly()
    {
        var engine = new PrototypeGameEngine();
        var strike = new CombatCardInstance(
            1,
            null,
            "proto.silent.strike",
            0,
            true,
            PrototypeJson.EmptyObject());
        var state = CreateState(
            hp: 6,
            cards: [strike]);

        state = PlayCard(
            engine,
            state,
            strike.InstanceId,
            enemyId: 1);

        var wrigglers = Wrigglers(state);
        Assert.Equal(4, wrigglers.Length);
        Assert.Equal(
            new[] { "wriggler1", "wriggler2", "wriggler3", "wriggler4" },
            wrigglers.Select(wriggler => wriggler.SlotName).ToArray());
        Assert.All(
            wrigglers,
            wriggler => Assert.Equal(
                1,
                wriggler.EnemyActionSkipsRemaining));

        state = EndTurn(engine, state);

        Assert.Equal(100, state.Player.Hp);
        Assert.All(
            Wrigglers(state),
            wriggler =>
            {
                Assert.Equal(
                    0,
                    wriggler.EnemyActionSkipsRemaining);
                Assert.Null(wriggler.LastMoveId);
            });

        state = EndTurn(engine, state);

        Assert.Equal(88, state.Player.Hp);
        Assert.Equal(
            2,
            state.World!.Combat!.Cards.Count(
                card => card.CardId == "proto.status.infection"));
        Assert.Equal(
            2,
            Wrigglers(state).Count(
                wriggler =>
                    wriggler.LastMoveId == "nasty_bite"));
        Assert.Equal(
            2,
            Wrigglers(state).Count(
                wriggler =>
                    wriggler.LastMoveId == "wriggle"));
    }

    [Fact]
    public void EnemyTurnStartDeathCarriesStunAcrossNextPlayerTurn()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            hp: 1,
            statuses:
                new Dictionary<string, int>(
                    StringComparer.Ordinal)
                {
                    ["proto.status.poison"] = 1
                });

        state = EndTurn(engine, state);

        Assert.Equal(100, state.Player.Hp);
        Assert.All(
            Wrigglers(state),
            wriggler =>
            {
                Assert.Equal(
                    1,
                    wriggler.EnemyActionSkipsRemaining);
                Assert.Null(wriggler.LastMoveId);
            });

        state = EndTurn(engine, state);

        Assert.Equal(100, state.Player.Hp);
        Assert.All(
            Wrigglers(state),
            wriggler =>
            {
                Assert.Equal(
                    0,
                    wriggler.EnemyActionSkipsRemaining);
                Assert.Null(wriggler.LastMoveId);
            });

        state = EndTurn(engine, state);

        Assert.Equal(88, state.Player.Hp);
        Assert.Equal(
            2,
            Wrigglers(state).Count(
                wriggler =>
                    wriggler.LastMoveId == "nasty_bite"));
        Assert.Equal(
            2,
            Wrigglers(state).Count(
                wriggler =>
                    wriggler.LastMoveId == "wriggle"));
    }

    [Fact]
    public void EliteReferenceFormationStartsWithParasiteAlone()
    {
        var spec = Assert.Single(
            PrototypeContent.Encounter(
                    "proto.encounter.phrog_parasite_elite")
                .ResolveEnemySpecs(
                    PrototypeRng.CreateBundle(
                        "phrog-formation")));

        Assert.Equal(
            "proto.enemy.phrog_parasite",
            spec.EnemyId);
        Assert.Equal(0, spec.FormationPosition);
    }

    private static void AssertWrigglerSummon(
        PrototypeEnemyDeathSummonSpec summon,
        int position,
        string slotName)
    {
        Assert.Equal("proto.enemy.wriggler", summon.EnemyId);
        Assert.Equal(position, summon.FormationPosition);
        Assert.Equal(slotName, summon.SlotName);
    }

    private static EnemyCombatState Phrog(
        RunState state) =>
        state.World!.Combat!.Enemies
            .Single(enemy =>
                enemy.EnemyId
                    == "proto.enemy.phrog_parasite");

    private static EnemyCombatState[] Wrigglers(
        RunState state) =>
        state.World!.Combat!.Enemies
            .Where(enemy =>
                enemy.EnemyId == "proto.enemy.wriggler")
            .OrderBy(enemy => enemy.FormationPosition)
            .ToArray();

    private static RunState PlayCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId,
        int enemyId)
    {
        var action = engine.GetLegalActions(state)
            .Single(candidate =>
            {
                if (candidate.Kind != "play_card")
                {
                    return false;
                }

                var payload =
                    candidate.ReadPayload<PlayCardPayload>();
                return payload.CardInstanceId
                        == cardInstanceId
                    && payload.TargetEnemyId == enemyId;
            });
        return engine.Step(state, action).State;
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static RunState CreateState(
        int hp,
        CombatCardInstance[]? cards = null,
        Dictionary<string, int>? statuses = null)
    {
        cards ??= [];
        statuses ??=
            new Dictionary<string, int>(
                StringComparer.Ordinal);

        var phrog = PrototypeContent.Enemy(
            "proto.enemy.phrog_parasite");
        var powers =
            (phrog.StartingPowers
                ?? Array.Empty<PrototypeStartingPowerSpec>())
            .Select((power, index) =>
                new PrototypePowerInstanceState(
                    power.PowerId,
                    power.Stacks,
                    index + 1L))
            .ToArray();

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
            PlayerBlock: 0,
            Hand: cards.Select(card => card.InstanceId).ToArray(),
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    phrog.Id,
                    hp,
                    0,
                    0,
                    statuses,
                    Powers: powers,
                    FormationPosition: 0)
            ],
            NextCardInstanceId:
                cards.Length == 0
                    ? 1
                    : cards.Max(card => card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder:
                powers.Length + 1L,
            Act: 1,
            Ascension: 0);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "phrog-parasite-test",
            "phrog-parasite-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "phrog-parasite-test"),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
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
