using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveHunterAndLouseTests
{
    private const string Hunter =
        PrototypeNativeHiveRemainingNormals.HunterKillerId;
    private const string Louse =
        PrototypeNativeHiveRemainingNormals.LouseProgenitorId;
    private const string Tender =
        PrototypeNativeHiveRemainingNormals.TenderId;
    private const string CurlUp =
        PrototypeNativeHiveRemainingNormals.CurlUpId;

    [Theory]
    [InlineData(0, 121, 17, 7, 134, 136, 9, 14, 5)]
    [InlineData(8, 126, 17, 7, 138, 141, 9, 18, 5)]
    [InlineData(9, 126, 19, 8, 138, 141, 10, 18, 7)]
    public void BothModelsRespectPinnedAscensionBreakpoints(
        int asc, int hunterHp, int bite, int puncture,
        int louseMin, int louseMax, int web, int curl, int grow)
    {
        var hunter = PrototypeContent.Enemy(Hunter);
        Assert.Equal((hunterHp, hunterHp), hunter.HpRangeAt(2, asc));
        Assert.Equal(new[] { "tenderizing_goop", "bite", "puncture" },
            hunter.Moves.Select(m => m.Id));
        Assert.Equal(bite, hunter.Moves[1].Effects[0].AmountAt(2, asc));
        Assert.Equal(puncture, hunter.Moves[2].Effects[0].AmountAt(2, asc));
        Assert.Equal(3, hunter.Moves[2].Effects[0].Repetitions);
        Assert.Equal(2, hunter.Ai!.States.Single(s => s.Id == "random")
            .Branches![1].Weight);

        var louse = PrototypeContent.Enemy(Louse);
        Assert.Equal((louseMin, louseMax), louse.HpRangeAt(2, asc));
        Assert.Equal(new[] { "web_cannon", "curl_and_grow", "pounce" },
            louse.Moves.Select(m => m.Id));
        Assert.Equal(web, louse.Moves[0].Effects[0].AmountAt(2, asc));
        Assert.Equal(2, louse.Moves[0].Effects[1].Amount);
        Assert.Equal(curl, louse.Moves[1].Effects[0].AmountAt(2, asc));
        Assert.Equal(grow, louse.Moves[1].Effects[1].AmountAt(2, asc));
        Assert.Equal(curl, Assert.Single(louse.StartingPowers!)
            .StacksAt(asc));
        Assert.Equal(0, louse.MoveLoopStartIndex);
    }

    [Fact]
    public void HunterGoopThenWeightedCommittedAttacksNeverRepeatBite()
    {
        var engine = new PrototypeGameEngine();
        for (var seed = 0; seed < 32; seed++)
        {
            var state = Commit(State(Hunter, "hive-hunter-" + seed));
            state = EndTurn(engine, state);
            Assert.Equal("tenderizing_goop",
                Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
            Assert.Equal(1, Assert.Single(state.World.Combat.PlayerPowers,
                p => p.PowerId == Tender).Stacks);

            string? previous = null;
            for (var i = 0; i < 6; i++)
            {
                var before = CanonicalJson.Sha256(state);
                var planned = Assert.Single(state.World!.Combat!.Enemies)
                    .PlannedMoveIndex;
                Assert.Contains(planned, new int?[] { 1, 2 });
                Assert.Equal(before, CanonicalJson.Sha256(state));
                state = EndTurn(engine, state);
                var move = Assert.Single(state.World!.Combat!.Enemies)
                    .LastMoveId;
                Assert.Contains(move, new[] { "bite", "puncture" });
                Assert.False(previous == "bite" && move == "bite");
                previous = move;
            }
        }
    }

    [Fact]
    public void TenderAppliesAfterCardCompletionAndRestoresAtSideTurnEnd()
    {
        var engine = new PrototypeGameEngine();
        var state = EndTurn(engine,
            Commit(State(Hunter, "hive-tender-card")));
        state = WithStrikeInHand(state);
        var original = CanonicalJson.Sha256(state);
        var played = engine.Step(state, engine.GetLegalActions(state)
            .Single(a => a.Kind == "play_card")).State;
        var powers = played.World!.Combat!.PlayerPowers;
        Assert.Equal(-1, Assert.Single(powers,
            p => p.PowerId == "proto.power.strength").Stacks);
        Assert.Equal(-1, Assert.Single(powers,
            p => p.PowerId == "proto.power.dexterity").Stacks);
        Assert.Equal(1, Assert.Single(powers,
            p => p.PowerId == Tender).StoredValue);
        Assert.Equal(original, CanonicalJson.Sha256(state));

        played = EndTurn(engine, played);
        Assert.DoesNotContain(played.World!.Combat!.PlayerPowers,
            p => p.PowerId == "proto.power.strength"
                || p.PowerId == "proto.power.dexterity");
        Assert.Equal(0, Assert.Single(played.World.Combat.PlayerPowers,
            p => p.PowerId == Tender).StoredValue);
    }

    [Fact]
    public void LouseWebCurlAndPounceRepeatWithCorrectBlockAndStrength()
    {
        var engine = new PrototypeGameEngine();
        var state = Commit(State(Louse, "hive-louse-cycle"));
        state = EndTurn(engine, state);
        Assert.Equal("web_cannon",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(2, Assert.Single(state.World.Combat.PlayerPowers,
            p => p.PowerId == "proto.power.frail").Stacks);
        state = EndTurn(engine, state);
        var curled = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("curl_and_grow", curled.LastMoveId);
        Assert.Equal(14, curled.Block);
        Assert.Equal(5, Assert.Single(curled.PowerStates,
            p => p.PowerId == "proto.power.strength").Stacks);
        state = EndTurn(engine, state);
        Assert.Equal("pounce",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
        state = EndTurn(engine, state);
        Assert.Equal("web_cannon",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
    }

    [Fact]
    public void CurlUpTriggersAfterFullyBlockedCardAndIsConsumedOnce()
    {
        var engine = new PrototypeGameEngine();
        var state = State(Louse, "hive-louse-curl");
        var originalEnemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(14, Assert.Single(originalEnemy.PowerStates,
            p => p.PowerId == CurlUp).Stacks);
        state = state with
        {
            World = state.World with
            {
                Combat = state.World.Combat! with
                {
                    Enemies = [originalEnemy with { Block = 100 }]
                }
            }
        };
        state = WithStrikeInHand(Commit(state));
        var played = engine.Step(state, engine.GetLegalActions(state)
            .Single(a => a.Kind == "play_card")).State;
        var enemy = Assert.Single(played.World!.Combat!.Enemies);
        Assert.Equal(114, enemy.Block);
        Assert.Equal(originalEnemy.Hp, enemy.Hp);
        Assert.DoesNotContain(enemy.PowerStates,
            p => p.PowerId == CurlUp);
        Assert.Equal(100, Assert.Single(state.World!.Combat!.Enemies).Block);
    }

    [Fact]
    public void BothNativeNormalFixturesRemainExcludedFromRandomRoutes()
    {
        foreach (var id in new[]
        {
            PrototypeNativeHiveRemainingNormals.HunterKillerNormalId,
            PrototypeNativeHiveRemainingNormals.LouseProgenitorNormalId
        })
        {
            var encounter = PrototypeContent.Encounter(id);
            Assert.Equal((2, 2, 0),
                (encounter.MinAct, encounter.MaxAct, encounter.Weight));
            Assert.Single(encounter.ResolveEnemySpecs(
                PrototypeRng.CreateBundle(id)));
            Assert.DoesNotContain(id,
                PrototypeContent.OvergrowthNormalEncounterPool);
            Assert.DoesNotContain(id,
                PrototypeNativeUnderdocks.NativeNormalEncounterIds);
        }
    }

    private static RunState WithStrikeInHand(RunState state)
    {
        var card = new CardInstance(1, "proto.silent.strike", 0,
            PrototypeJson.EmptyObject());
        var combatCard = new CombatCardInstance(1, 1, card.CardId,
            0, false, card.PersistentState);
        return state with
        {
            Player = state.Player with { Deck = [card] },
            World = state.World! with
            {
                Combat = state.World.Combat! with
                {
                    Hand = [1], Cards = [combatCard],
                    NextCardInstanceId = 2
                }
            }
        };
    }

    private static RunState State(string id, string seed)
    {
        var def = PrototypeContent.Enemy(id);
        long order = 1;
        var enemyPowers = (def.StartingPowers ??
            Array.Empty<PrototypeStartingPowerSpec>())
            .Select(power => new PrototypePowerInstanceState(
                power.PowerId, power.StacksAt(0), order++))
            .ToArray();
        var enemy = new EnemyCombatState(1, id, def.HpAt(2, 0), 0, 0,
            new Dictionary<string, int>(StringComparer.Ordinal),
            Powers: enemyPowers, AiStateId: def.Ai?.InitialStateId);
        var combat = new CombatState(1, 3, 0, [], [], [], [],
            [enemy], 1, [], [], order, Act: 2);
        return new RunState("prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat,
            new PlayerState(500, 500, 0, [], [],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 2, 1, 1,
                PrototypeRoomType.Combat, new MapState([]),
                combat, null, null, null, null));
    }

    private static RunState Commit(RunState state) =>
        state with
        {
            World = state.World! with
            {
                Combat = PrototypeGameEngine.CommitEnemyIntents(
                    state.World.Combat!, state.Rng)
            }
        };

    private static RunState EndTurn(
        PrototypeGameEngine engine, RunState state) =>
        engine.Step(state, engine.GetLegalActions(state)
            .Single(a => a.Kind == "end_turn")).State;
}
