using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveNormalEncountersTests
{
    private const string Rock = "proto.native.hive.bowlbug_rock";
    private const string Egg = "proto.native.hive.bowlbug_egg";
    private const string Silk = "proto.native.hive.bowlbug_silk";
    private const string Nectar = "proto.native.hive.bowlbug_nectar";
    private const string Exo = "proto.native.hive.exoskeleton";

    [Fact]
    public void BowlbugsNormalAlwaysHasRockAndTwoDistinctNativeWorkers()
    {
        var encounter = PrototypeContent.Encounter(
            PrototypeNativeHiveNormals.BowlbugsNormalId);
        Assert.Equal((2, 2, 0), (encounter.MinAct,
            encounter.MaxAct, encounter.Weight));
        var pairs = new HashSet<string>();
        for (var i = 0; i < 128; i++)
        {
            var seed = "hive-normal-bowlbugs-" + i;
            var rng = PrototypeRng.CreateBundle(seed);
            var before = CanonicalJson.Sha256(rng);
            var specs = encounter.ResolveEnemySpecs(rng);
            Assert.Equal(3, specs.Length);
            Assert.Equal((Rock, 0, "first"),
                (specs[0].EnemyId, specs[0].FormationPosition,
                    specs[0].SlotName));
            Assert.Equal(new[] { "middle", "last" },
                specs.Skip(1).Select(s => s.SlotName));
            Assert.All(specs.Skip(1), worker =>
                Assert.Contains(worker.EnemyId,
                    new[] { Egg, Silk, Nectar }));
            Assert.NotEqual(specs[1].EnemyId, specs[2].EnemyId);
            pairs.Add(string.Join("+",
                specs.Skip(1).Select(w => w.EnemyId)
                    .Order(StringComparer.Ordinal)));
            Assert.NotEqual(before, CanonicalJson.Sha256(rng));
            Assert.Equal(specs, encounter.ResolveEnemySpecs(
                PrototypeRng.CreateBundle(seed)));
        }

        Assert.Equal(3, pairs.Count);
    }

    [Fact]
    public void ExoskeletonsNormalFourthSlotCommitsRandomOpeningOnce()
    {
        var encounter = PrototypeContent.Encounter(
            PrototypeNativeHiveNormals.ExoskeletonsNormalId);
        var specs = encounter.ResolveEnemySpecs(
            PrototypeRng.CreateBundle("hive-four-exos"));
        Assert.Equal(new[] { "first", "second", "third", "fourth" },
            specs.Select(spec => spec.SlotName));
        Assert.All(specs, spec => Assert.Equal(Exo, spec.EnemyId));
        var fourthMoves = new HashSet<int>();
        for (var i = 0; i < 128; i++)
        {
            var state = State(specs, "hive-four-exos-" + i);
            var before = CanonicalJson.Sha256(state);
            var committed = Commit(state);
            var indexes = committed.World!.Combat!.Enemies
                .Select(enemy => enemy.PlannedMoveIndex).ToArray();
            Assert.Equal(0, indexes[0]);
            Assert.Equal(1, indexes[1]);
            Assert.Equal(2, indexes[2]);
            Assert.Contains(indexes[3], new int?[] { 0, 1 });
            fourthMoves.Add(indexes[3]!.Value);
            Assert.Equal(before, CanonicalJson.Sha256(state));
            Assert.Equal(CanonicalJson.Sha256(committed),
                CanonicalJson.Sha256(Commit(State(
                    specs, "hive-four-exos-" + i))));
            // Inspecting legal actions cannot cause another opening
            // roll, including for the stochastic fourth slot.
            var hash = CanonicalJson.Sha256(committed);
            var engine = new PrototypeGameEngine();
            _ = engine.GetLegalActions(committed);
            _ = engine.GetLegalActions(committed);
            Assert.Equal(hash, CanonicalJson.Sha256(committed));
        }

        Assert.Equal(new[] { 0, 1 }, fourthMoves.Order());
    }

    [Theory]
    [InlineData(0, 60, 64, 8)]
    [InlineData(8, 63, 67, 8)]
    [InlineData(9, 63, 67, 9)]
    public void ChomperHasNativeHpDamageAndTwoArtifacts(
        int ascension, int minHp, int maxHp, int clamp)
    {
        var enemy = PrototypeContent.Enemy(
            PrototypeNativeHiveNormals.ChomperId);
        Assert.Equal((minHp, maxHp), enemy.HpRangeAt(2, ascension));
        var artifact = Assert.Single(enemy.StartingPowers!);
        Assert.Equal("proto.power.artifact", artifact.PowerId);
        Assert.Equal(2, artifact.StacksAt(ascension));
        Assert.Equal(2, enemy.Moves.Length);
        Assert.Equal(clamp,
            enemy.Moves[0].Effects[0].AmountAt(2, ascension));
        Assert.Equal(2, enemy.Moves[0].Effects[0].Repetitions);
        Assert.Equal(3, enemy.Moves[1].Effects[0].Amount);
        Assert.Equal("proto.status.dazed", enemy.Moves[1].Effects[0].CardId);
    }

    [Fact]
    public void ChompersNormalUsesDistinctInstanceOpeningFlagsAndAlternates()
    {
        var encounter = PrototypeContent.Encounter(
            PrototypeNativeHiveNormals.ChompersNormalId);
        Assert.Equal(0, encounter.Weight);
        var specs = encounter.ResolveEnemySpecs(
            PrototypeRng.CreateBundle("chompers-normal"));
        Assert.Equal(new[] { "clamp-first", "screech-first" },
            specs.Select(spec => spec.SlotName));
        var state = Commit(State(specs, "chomper-turn-cycle"));
        Assert.Equal(new int?[] { 0, 1 },
            state.World!.Combat!.Enemies
                .Select(enemy => enemy.PlannedMoveIndex).ToArray());
        var engine = new PrototypeGameEngine();
        state = EndTurn(engine, state);
        Assert.Equal(484, state.Player.Hp);
        Assert.Equal(new[] { "clamp", "screech" },
            state.World!.Combat!.Enemies.Select(e => e.LastMoveId));
        Assert.Equal(3, state.World.Combat.Cards.Count(card =>
            card.CardId == "proto.status.dazed"));
        Assert.All(state.World.Combat.Enemies, enemy =>
            Assert.Equal(2, Assert.Single(enemy.PowerStates).Stacks));

        state = EndTurn(engine, state);
        Assert.Equal(468, state.Player.Hp);
        Assert.Equal(new[] { "screech", "clamp" },
            state.World!.Combat!.Enemies.Select(e => e.LastMoveId));
        Assert.Equal(6, state.World.Combat.Cards.Count(card =>
            card.CardId == "proto.status.dazed"));
    }

    [Theory]
    [InlineData(0, 61, 67, 13, 4, 2)]
    [InlineData(8, 64, 69, 13, 4, 2)]
    [InlineData(9, 64, 69, 15, 6, 3)]
    public void MyteUsesNativeHpAscensionMovesAndTwoToxicStatusCards(
        int ascension, int minHp, int maxHp, int bite, int suck,
        int strength)
    {
        var enemy = PrototypeContent.Enemy(
            PrototypeNativeHiveNormals.MyteId);
        Assert.Equal((minHp, maxHp), enemy.HpRangeAt(2, ascension));
        Assert.Equal(new[] { "toxic", "bite", "suck" },
            enemy.Moves.Select(move => move.Id));
        Assert.Equal(2, enemy.Moves[0].Effects[0].Amount);
        Assert.Equal(PrototypeEnemyEffectKind.AddCardsToHand,
            enemy.Moves[0].Effects[0].Kind);
        Assert.Equal(PrototypeNativeHiveNormals.ToxicCardId,
            enemy.Moves[0].Effects[0].CardId);
        Assert.Equal(bite, enemy.Moves[1].Effects[0].AmountAt(2, ascension));
        Assert.Equal(suck, enemy.Moves[2].Effects[0].AmountAt(2, ascension));
        Assert.Equal(strength,
            enemy.Moves[2].Effects[1].AmountAt(2, ascension));
    }

    [Fact]
    public void MytesNormalStartsToxicAndSuckThenToxicHurtsInHand()
    {
        var encounter = PrototypeContent.Encounter(
            PrototypeNativeHiveNormals.MytesNormalId);
        var specs = encounter.ResolveEnemySpecs(
            PrototypeRng.CreateBundle("mytes-normal"));
        Assert.Equal(new[] { "first", "second" },
            specs.Select(spec => spec.SlotName));
        var state = Commit(State(specs, "mytes-normal-steps"));
        Assert.Equal(new int?[] { 0, 2 },
            state.World!.Combat!.Enemies
                .Select(enemy => enemy.PlannedMoveIndex).ToArray());
        var engine = new PrototypeGameEngine();

        state = EndTurn(engine, state);
        Assert.Equal(496, state.Player.Hp);
        Assert.Equal(new[] { "toxic", "suck" },
            state.World!.Combat!.Enemies.Select(e => e.LastMoveId));
        Assert.Equal(2, state.World.Combat.Hand.Length);
        Assert.Equal(2, state.World.Combat.Cards.Count(card =>
            card.CardId == PrototypeNativeHiveNormals.ToxicCardId));
        Assert.All(state.World.Combat.Hand, id =>
        {
            var card = Assert.Single(state.World.Combat.Cards,
                c => c.InstanceId == id);
            Assert.Equal(PrototypeNativeHiveNormals.ToxicCardId,
                card.CardId);
            Assert.True(card.IsTemporary);
            Assert.Null(card.PersistentCardInstanceId);
        });

        state = EndTurn(engine, state);
        Assert.Equal(473, state.Player.Hp);
        Assert.Equal(new[] { "bite", "toxic" },
            state.World!.Combat!.Enemies.Select(e => e.LastMoveId));
        Assert.Equal(4, state.World.Combat.Cards.Count(card =>
            card.CardId == PrototypeNativeHiveNormals.ToxicCardId));
    }

    [Fact]
    public void ToxicIsPlayableExhaustingTemporaryStatusAndNotAReward()
    {
        var card = PrototypeContent.Card(
            PrototypeNativeHiveNormals.ToxicCardId);
        Assert.Equal(PrototypeCardType.Status, card.Type);
        Assert.Equal(PrototypeCardRarity.Status, card.Rarity);
        Assert.False(card.Unplayable);
        Assert.True(card.ExhaustOnUse);
        Assert.Equal(1, card.Cost.AmountAt(0));
        Assert.Equal(5, card.EndTurnDamageIfInHand);
        Assert.False(card.EndTurnDamageUnblockable);
        Assert.Equal(0, card.MaxUpgradeLevel);
        Assert.False(card.RewardEligible);
        Assert.DoesNotContain(card.Id, PrototypeContent.RewardCardPool);
    }

    [Fact]
    public void EnemyGeneratedToxicCardsRespectHandCapacityAndIdentity()
    {
        var initial = Enumerable.Range(1, 9).Select(i =>
            new CombatCardInstance(i, null,
                PrototypeNativeHiveNormals.ToxicCardId,
                0, true, PrototypeJson.EmptyObject())).ToArray();
        var combat = State([], "mytes-full-hand").World!.Combat! with
        {
            Hand = initial.Select(c => c.InstanceId).ToArray(),
            Cards = initial,
            NextCardInstanceId = 10
        };
        var before = CanonicalJson.Sha256(combat);
        var method = typeof(PrototypeGameEngine).GetMethod(
            "AddGeneratedEnemyCardsToHand",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var after = (CombatState)method!.Invoke(null,
            [combat, PrototypeNativeHiveNormals.ToxicCardId, 2])!;
        Assert.Equal(10, after.Hand.Length);
        Assert.Equal(11, after.Cards.Length);
        Assert.Equal(new long[] { 11 }, after.DiscardPile);
        Assert.Equal(12, after.NextCardInstanceId);
        Assert.Equal(before, CanonicalJson.Sha256(combat));
        Assert.Equal(CanonicalJson.Sha256(after),
            CanonicalJson.Sha256(after.Fork()));
    }

    [Fact]
    public void AllNewHiveNormalEncountersAreExplicitlyExcludedFromRoutes()
    {
        foreach (var id in new[]
        {
            PrototypeNativeHiveNormals.BowlbugsNormalId,
            PrototypeNativeHiveNormals.ExoskeletonsNormalId,
            PrototypeNativeHiveNormals.ChompersNormalId,
            PrototypeNativeHiveNormals.MytesNormalId
        })
        {
            var encounter = PrototypeContent.Encounter(id);
            Assert.Equal((2, 2, 0), (encounter.MinAct,
                encounter.MaxAct, encounter.Weight));
            Assert.DoesNotContain(id,
                PrototypeContent.OvergrowthNormalEncounterPool);
            Assert.DoesNotContain(id,
                PrototypeNativeUnderdocks.NativeNormalEncounterIds);
        }
    }

    private static RunState State(
        PrototypeEncounterEnemySpec[] specs, string seed,
        int ascension = 0)
    {
        long order = 1;
        var enemies = specs.Select((spec, index) =>
        {
            var def = PrototypeContent.Enemy(spec.EnemyId);
            var powers = (def.StartingPowers
                ?? Array.Empty<PrototypeStartingPowerSpec>())
                .Select(power => new PrototypePowerInstanceState(
                    power.PowerId, power.StacksAt(ascension), order++))
                .ToArray();
            return new EnemyCombatState(index + 1,
                spec.EnemyId, def.HpAt(2, ascension), 0, 0,
                new Dictionary<string, int>(StringComparer.Ordinal),
                SlotName: spec.SlotName,
                FormationPosition: spec.FormationPosition,
                Powers: powers,
                AiStateId: def.Ai?.InitialStateId);
        }).ToArray();
        var combat = new CombatState(1, 3, 0, [], [], [], [],
            enemies, 1, [], [], order, Act: 2, Ascension: ascension);
        return new RunState("prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat,
            new PlayerState(500, 500, 0, [], [],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 2, 1, 1,
                PrototypeRoomType.Combat,
                new MapState([]), combat, null, null, null, null),
            Ascension: ascension);
    }

    private static RunState Commit(RunState state) => state with
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
