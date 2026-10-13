using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveSummoningNormalTests
{
    private const string Ovi = PrototypeNativeHiveSummoningNormals.OvicopterId;
    private const string Egg = PrototypeNativeHiveSummoningNormals.ToughEggId;
    private const string Hatchling =
        PrototypeNativeHiveSummoningNormals.HatchlingId;
    private const string Obscura =
        PrototypeNativeHiveSummoningNormals.ObscuraId;
    private const string Parafright =
        PrototypeNativeHiveSummoningNormals.ParafrightId;

    [Theory]
    [InlineData(0, 124, 130, 14, 18, 19, 22, 16, 7, 3)]
    [InlineData(8, 126, 132, 15, 19, 20, 23, 16, 7, 3)]
    [InlineData(9, 126, 132, 15, 19, 20, 23, 17, 8, 4)]
    public void OvicopterAndEggsUsePinnedNumbers(
        int asc, int min, int max, int eggMin, int eggMax,
        int hatchMin, int hatchMax, int smash, int tenderize, int paste)
    {
        var ovi = PrototypeContent.Enemy(Ovi);
        Assert.Equal((min, max), ovi.HpRangeAt(2, asc));
        Assert.Equal(smash, ovi.Moves[1].Effects[0].AmountAt(2, asc));
        Assert.Equal(tenderize, ovi.Moves[2].Effects[0].AmountAt(2, asc));
        Assert.Equal(paste, ovi.Moves[3].Effects[0].AmountAt(2, asc));
        Assert.Equal(2, ovi.Moves[2].Effects[1].Amount);
        Assert.Equal(3, ovi.Moves[0].Effects[0].Repetitions);
        Assert.True(ovi.Moves[0].Effects[0].ReserveSummonSlotAfterDeath);
        Assert.Equal((eggMin, eggMax),
            PrototypeContent.Enemy(Egg).HpRangeAt(2, asc));
        Assert.Equal((hatchMin, hatchMax),
            PrototypeContent.Enemy(Hatchling).HpRangeAt(2, asc));
        Assert.Equal(asc >= 9 ? 5 : 4,
            PrototypeContent.Enemy(Hatchling).Moves[0]
                .Effects[0].AmountAt(2, asc));
        Assert.True(PrototypeContent.Enemy(Egg).IsMinion);
        Assert.True(PrototypeContent.Enemy(Hatchling).IsMinion);
    }

    [Theory]
    [InlineData(0, 123, 10, 6, 16)]
    [InlineData(8, 129, 10, 6, 16)]
    [InlineData(9, 129, 11, 7, 17)]
    public void ObscuraAndParafrightUsePinnedNumbers(
        int asc, int hp, int gaze, int hardening, int slam)
    {
        var obscura = PrototypeContent.Enemy(Obscura);
        Assert.Equal((hp, hp), obscura.HpRangeAt(2, asc));
        Assert.Equal(new[] { "illusion", "piercing_gaze", "wail",
            "hardening_strike" }, obscura.Moves.Select(m => m.Id));
        Assert.Equal(gaze,
            obscura.Moves[1].Effects[0].AmountAt(2, asc));
        Assert.Equal(3, obscura.Moves[2].Effects[0].Amount);
        Assert.Equal(PrototypeEnemyEffectKind.ApplyAllEnemyPower,
            obscura.Moves[2].Effects[0].Kind);
        Assert.Equal(hardening,
            obscura.Moves[3].Effects[0].AmountAt(2, asc));
        Assert.Equal(hardening,
            obscura.Moves[3].Effects[1].AmountAt(2, asc));
        var para = PrototypeContent.Enemy(Parafright);
        Assert.Equal((21, 21), para.HpRangeAt(2, asc));
        Assert.Equal(slam, para.Moves[0].Effects[0].AmountAt(2, asc));
        Assert.True(para.IsMinion);
        Assert.True(para.RevivesOnEnemyTurn);
    }

    [Fact]
    public void BothEncountersAreSinglePrimaryWithNativeSlotsAndGated()
    {
        foreach (var (id, enemy, slot, position) in new[]
        {
            (PrototypeNativeHiveSummoningNormals.OvicopterNormalId,
                Ovi, "ovicopter", 5),
            (PrototypeNativeHiveSummoningNormals.ObscuraNormalId,
                Obscura, "obscura", 1)
        })
        {
            var encounter = PrototypeContent.Encounter(id);
            Assert.Equal((2, 2, 0),
                (encounter.MinAct, encounter.MaxAct, encounter.Weight));
            var spec = Assert.Single(encounter.ResolveEnemySpecs(
                PrototypeRng.CreateBundle("hive-summon-formation")));
            Assert.Equal((enemy, slot, position),
                (spec.EnemyId, spec.SlotName, spec.FormationPosition));
            Assert.DoesNotContain(id,
                PrototypeContent.OvergrowthNormalEncounterPool);
            Assert.DoesNotContain(id,
                PrototypeNativeUnderdocks.NativeNormalEncounterIds);
        }
    }

    [Fact]
    public void OvicopterLaysThreeDistinctReverseOrderedEggsThenTheyHatch()
    {
        var engine = new PrototypeGameEngine();
        var state = Commit(State(Ovi, "hive-ovi-spawn"));
        var parentHash = CanonicalJson.Sha256(state);
        state = EndTurn(engine, state);
        var formation = state.World!.Combat!.Enemies;
        Assert.Equal(4, formation.Length);
        var parent = Assert.Single(formation, e => e.EnemyId == Ovi);
        Assert.Equal(1, parent.InstanceId);
        Assert.Equal("lay_eggs", parent.LastMoveId);
        var eggs = formation.Where(e => e.EnemyId == Egg).ToArray();
        Assert.Equal(3, eggs.Length);
        Assert.Equal(new[] { "egg5", "egg4", "egg3" },
            eggs.Select(e => e.SlotName));
        Assert.Equal(new[] { 2, 3, 4 },
            eggs.Select(e => e.InstanceId));
        Assert.All(eggs, egg =>
        {
            Assert.Equal(1, Assert.Single(egg.PowerStates,
                p => p.PowerId ==
                    PrototypeNativeHiveSummoningNormals.HatchPowerId).Stacks);
            Assert.Equal(1, egg.LeaderEnemyInstanceId);
            Assert.NotNull(egg.PlannedMoveIndex);
        });
        Assert.Equal(parentHash,
            CanonicalJson.Sha256(Commit(State(Ovi, "hive-ovi-spawn"))));

        state = EndTurn(engine, state);
        var after = state.World!.Combat!.Enemies;
        Assert.Equal("smash", Assert.Single(after,
            e => e.EnemyId == Ovi).LastMoveId);
        var hatchlings = after.Where(e => e.EnemyId == Hatchling).ToArray();
        Assert.Equal(3, hatchlings.Length);
        Assert.Equal(new[] { 2, 3, 4 },
            hatchlings.Select(e => e.InstanceId));
        Assert.All(hatchlings, hatch =>
        {
            Assert.InRange(hatch.Hp, 19, 22);
            Assert.Equal("hatch", hatch.LastMoveId);
            Assert.Contains(hatch.PowerStates,
                power => power.PowerId == "proto.power.minion");
            Assert.DoesNotContain(hatch.PowerStates,
                power => power.PowerId ==
                    PrototypeNativeHiveSummoningNormals.HatchPowerId);
        });

        state = EndTurn(engine, state);
        Assert.All(state.World!.Combat!.Enemies
            .Where(e => e.EnemyId == Hatchling),
            hatch => Assert.Equal("nibble", hatch.LastMoveId));
    }

    [Fact]
    public void OvicopterPasteOrLayIsCommittedAgainstLivingEnemyCount()
    {
        var engine = new PrototypeGameEngine();
        var original = Commit(State(Ovi, "hive-ovi-branch"));
        var full = EndTurn(engine, original);
        full = EndTurn(engine, full);
        full = EndTurn(engine, full);
        var normal = Assert.Single(full.World!.Combat!.Enemies,
            e => e.EnemyId == Ovi);
        Assert.Equal("tenderizer", normal.LastMoveId);
        Assert.Equal(3, normal.PlannedMoveIndex); // > 3 living: Paste

        // Kill two minions while their occupied slots remain in the
        // formation. This makes the next decision choose Lay again.
        var killed = original;
        killed = EndTurn(engine, killed);
        var enemies = killed.World!.Combat!.Enemies
            .Select((e, i) => i is 1 or 2 ? e with { Hp = 0 } : e)
            .ToArray();
        killed = killed with
        {
            World = killed.World with
            {
                Combat = killed.World.Combat with { Enemies = enemies }
            }
        };
        killed = EndTurn(engine, killed);
        killed = EndTurn(engine, killed);
        var laying = Assert.Single(killed.World!.Combat!.Enemies,
            e => e.EnemyId == Ovi);
        Assert.Equal("tenderizer", laying.LastMoveId);
        Assert.Equal(0, laying.PlannedMoveIndex);
        killed = EndTurn(engine, killed);
        var newEggs = killed.World!.Combat!.Enemies
            .Where(e => e.EnemyId == Egg && e.Hp > 0).ToArray();
        Assert.Equal(new[] { "egg2", "egg1" },
            newEggs.Select(e => e.SlotName).ToArray());
        Assert.DoesNotContain(killed.World.Combat.Enemies,
            e => e.InstanceId > 6);
    }

    [Fact]
    public void ObscuraSummonsOneInstanceAndWailBuffsEveryLivingTeammate()
    {
        var engine = new PrototypeGameEngine();
        var state = Commit(State(Obscura, "hive-obscura-wail"));
        state = EndTurn(engine, state);
        var para = Assert.Single(state.World!.Combat!.Enemies,
            e => e.EnemyId == Parafright);
        Assert.Equal("illusion", para.SlotName);
        Assert.Equal(1, para.LeaderEnemyInstanceId);
        Assert.Equal(2, para.InstanceId);
        Assert.Equal(21, para.Hp);

        var observedWail = false;
        for (var round = 0; round < 12; round++)
        {
            var next = EndTurn(engine, state);
            var owner = Assert.Single(next.World!.Combat!.Enemies,
                e => e.EnemyId == Obscura);
            if (owner.LastMoveId == "wail")
            {
                var oldOwner = Assert.Single(state.World!.Combat!.Enemies,
                    e => e.EnemyId == Obscura);
                var oldPara = Assert.Single(state.World.Combat.Enemies,
                    e => e.EnemyId == Parafright);
                var newPara = Assert.Single(next.World.Combat.Enemies,
                    e => e.EnemyId == Parafright);
                Assert.Equal(Strength(oldOwner) + 3, Strength(owner));
                Assert.Equal(Strength(oldPara) + 3, Strength(newPara));
                observedWail = true;
                break;
            }
            state = next;
        }
        Assert.True(observedWail);
    }

    [Fact]
    public void ParafrightDiesThenRevivesOnlyWhileObscuraIsAlive()
    {
        var engine = new PrototypeGameEngine();
        var state = EndTurn(engine,
            Commit(State(Obscura, "hive-obscura-revive")));
        var para = Assert.Single(state.World!.Combat!.Enemies,
            e => e.EnemyId == Parafright);
        var killedEnemies = state.World.Combat.Enemies.Select(e =>
            e.InstanceId == para.InstanceId
                ? e with { Hp = 0, Block = 0 } : e).ToArray();
        state = state with
        {
            World = state.World with
            {
                Combat = state.World.Combat with { Enemies = killedEnemies }
            }
        };
        state = EndTurn(engine, state);
        var revived = Assert.Single(state.World!.Combat!.Enemies,
            e => e.InstanceId == para.InstanceId);
        Assert.Equal(Parafright, revived.EnemyId);
        Assert.Equal(21, revived.Hp);
        Assert.Equal("revive", revived.LastMoveId);
        Assert.Equal(1, revived.LeaderEnemyInstanceId);
        state = EndTurn(engine, state);
        Assert.Equal("slam", Assert.Single(state.World!.Combat!.Enemies,
            e => e.InstanceId == para.InstanceId).LastMoveId);
    }

    [Fact]
    public void DefeatingPrimaryFinishesCombatWhileSecondaryIllusionLives()
    {
        var engine = new PrototypeGameEngine();
        var state = EndTurn(engine,
            Commit(State(Obscura, "hive-obscura-minion-victory")));
        var parent = Assert.Single(state.World!.Combat!.Enemies,
            e => e.EnemyId == Obscura);
        var enemies = state.World.Combat.Enemies.Select(e =>
            e.InstanceId == parent.InstanceId
                ? e with { Hp = 1, Block = 0 } : e).ToArray();
        state = state with
        {
            World = state.World with
            {
                Combat = state.World.Combat with { Enemies = enemies }
            }
        };
        state = WithStrike(state);
        var play = engine.GetLegalActions(state).Single(a =>
            a.Kind == "play_card"
            && a.ReadPayload<PlayCardPayload>().TargetEnemyId == 1);
        var result = engine.Step(state, play).State;
        Assert.NotEqual(RunPhase.Combat, result.Phase);
    }

    private static int Strength(EnemyCombatState enemy) =>
        enemy.PowerStates.Where(p => p.PowerId == "proto.power.strength")
            .Sum(p => p.Stacks);

    private static RunState WithStrike(RunState state)
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

    private static RunState State(string enemyId, string seed)
    {
        var definition = PrototypeContent.Enemy(enemyId);
        var instance = new EnemyCombatState(1, enemyId,
            definition.HpAt(2, 0), 0, 0,
            new Dictionary<string, int>(StringComparer.Ordinal),
            AiStateId: definition.Ai?.InitialStateId,
            FormationPosition: 5,
            SlotName: enemyId == Ovi ? "ovicopter" : "obscura");
        var combat = new CombatState(1, 3, 0, [], [], [], [],
            [instance], 1, [], [], 1, Act: 2);
        return new RunState("prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat,
            new PlayerState(9000, 9000, 0, [], [],
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
