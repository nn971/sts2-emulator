using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Source PlowPower/ShriekPower.AfterDamageReceived require positive
/// unblocked damage. Losing HP to poison or direct HP loss never invokes
/// that damage callback. These source rules apply only in native opt-in.
/// </summary>
public sealed class NativeDamageOnlyPhaseTriggersV111Tests
{
    private static readonly PrototypeGameEngine Engine = new();

    private static RunState Enter(string seed,
        ActIdentity act, PrototypeRoomType room, string id,
        int ascension)
    {
        var config = RunConfiguration.Create(
            firstAct: act, ascension: ascension) with
        {
            FidelityId = V111Build.NativeWeakCombatEntryFidelityId
        };
        var state = Engine.Step(
            V111RunFactory.Create(seed, config),
            GameAction.Empty("start_run")).State;
        for (var i = 0; i < 30
            && state.Phase is RunPhase.Event or RunPhase.Reward; i++)
            state = Engine.Step(
                state, Engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        var world = state.World!;
        var nodes = world.Map.Nodes.ToDictionary(x => x.NodeId,
            StringComparer.Ordinal);
        var edge = world.Map.Nodes
            .SelectMany(parent => (parent.NextNodeIds ?? [])
                .Select(nextId => (Parent: parent, Child: nodes[nextId])))
            .First(x => x.Child.RoomType == room
                && (room != PrototypeRoomType.Elite
                    || x.Child.Floor is >= 3 and < 16));
        var pool = world.ActOneEncounterPool!;
        state = state with
        {
            World = world with
            {
                Floor = edge.Parent.Floor,
                ActiveRoom = null,
                ActOneEncounterPool = pool with
                {
                    BossEncounterId = room == PrototypeRoomType.Boss
                        ? id : pool.BossEncounterId,
                    RemainingEliteEncounterIds = room == PrototypeRoomType.Elite
                        ? [id] : pool.RemainingEliteEncounterIds
                },
                Map = world.Map with
                {
                    CurrentNodeId = edge.Parent.NodeId
                }
            }
        };
        var action = Engine.GetLegalActions(state)
            .Where(option => option.Kind == "choose_map_node")
            .First(option => state.World!.Map.Nodes.Any(node =>
                node.NodeId
                    == option.ReadPayload<ChooseMapNodePayload>().NodeId
                && node.RoomType == room));
        var entered = Engine.Step(state, action).State;
        Assert.Equal(RunPhase.Combat, entered.Phase);
        Assert.Equal(id, entered.World!.EncounterIds.Last());
        NativeRunRngV111.Validate(entered);
        return entered;
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (var asc in new[] { 0, 8, 10 })
        {
            yield return
            [
                ActIdentity.Overgrowth, PrototypeRoomType.Boss,
                "proto.encounter.ceremonial_beast_boss",
                "proto.enemy.ceremonial_beast",
                "proto.power.plow", asc >= 9 ? 160 : 150,
                "stun_move", 2, asc
            ];
            yield return
            [
                ActIdentity.Underdocks, PrototypeRoomType.Elite,
                "proto.encounter.terror_eel_elite",
                "proto.enemy.terror_eel",
                "proto.power.shriek", asc >= 8 ? 75 : 70,
                "stun", 2, asc
            ];
        }
    }

    private static NativeRngSnapshot Niche(CombatState combat) =>
        NativeRngV111.ToSnapshot(
            combat.NativeRng!.RunStreams.Streams.Single(
                stream => stream.StreamId == "run.niche"));

    [Theory]
    [MemberData(nameof(Cases))]
    public void UnblockedDamageButNotRawHpLossTriggersSourcePhase(
        ActIdentity act, PrototypeRoomType room, string encounter,
        string monsterId, string thresholdPowerId, int threshold,
        string stunnedStateId, int stunMoveIndex, int ascension)
    {
        for (var i = 0; i < 8; i++)
        {
            var state = Enter(
                $"native-threshold-{encounter}-{ascension}-{i}",
                act, room, encounter, ascension);
            var combat = state.World!.Combat!;
            var enemy = Assert.Single(combat.Enemies);
            Assert.Equal(monsterId, enemy.EnemyId);

            // Set up a source-equivalent active threshold. Ceremonial
            // Beast gets Plow from Stamp on its first enemy turn;
            // Terror Eel starts with Shriek. Set HP one above threshold.
            var powers = enemy.PowerStates
                .Where(power => power.PowerId != thresholdPowerId)
                .Append(new PrototypePowerInstanceState(
                    thresholdPowerId, threshold,
                    combat.NextPowerApplicationOrder))
                .Append(new PrototypePowerInstanceState(
                    "proto.power.strength", 4,
                    combat.NextPowerApplicationOrder + 1))
                .ToArray();
            enemy = enemy with
            {
                Hp = threshold + 1,
                Powers = powers,
                Block = 0
            };
            combat = combat with
            {
                Enemies = [enemy],
                NextPowerApplicationOrder = combat.NextPowerApplicationOrder + 2
            };
            NativeCombatRngEntryV111.Validate(combat);
            var original = CanonicalJson.Sha256(combat);
            var niche = Niche(combat);
            var originalAi = NativeRngV111.ToSnapshot(
                combat.NativeRng!.RunStreams.Streams.Single(x =>
                    x.StreamId == "run.monster_ai"));

            // Unblocked damage hook did not fire. This raw HP loss
            // crosses the threshold, but the phase and power survive.
            var poison = PrototypeGameEngine.ApplyRawHpLossForSourceRegression(
                combat, enemy.InstanceId, 2);
            var afterPoison = Assert.Single(poison.Enemies);
            Assert.Equal(threshold - 1, afterPoison.Hp);
            Assert.Contains(afterPoison.PowerStates,
                power => power.PowerId == thresholdPowerId);
            Assert.NotEqual(stunnedStateId, afterPoison.AiStateId);
            Assert.Equal(enemy.PlannedMoveIndex,
                afterPoison.PlannedMoveIndex);
            NativeCombatRngEntryV111.Validate(poison);

            // A hit absorbed entirely by Block also cannot stun.
            var blocked = PrototypeGameEngine.ApplyHitForSourceRegression(
                poison with
                {
                    Enemies = [afterPoison with { Block = 6 }]
                }, enemy.InstanceId, 5);
            var afterBlocked = Assert.Single(blocked.Enemies);
            Assert.Equal(threshold - 1, afterBlocked.Hp);
            Assert.Equal(1, afterBlocked.Block);
            Assert.Contains(afterBlocked.PowerStates,
                power => power.PowerId == thresholdPowerId);
            Assert.NotEqual(stunnedStateId, afterBlocked.AiStateId);

            // Even though HP has already passed the threshold,
            // an *unblocked* hit now invokes AfterDamageReceived and
            // must consume the phase power and force Stun.
            var unblocked = PrototypeGameEngine.ApplyHitForSourceRegression(
                blocked, enemy.InstanceId, 1);
            var afterHit = Assert.Single(unblocked.Enemies);
            Assert.Equal(threshold - 1, afterHit.Hp);
            Assert.Contains(afterHit.PowerStates,
                power => power.PowerId == thresholdPowerId);
            // Remaining Block consumed that entire hit.
            var triggered = PrototypeGameEngine.ApplyHitForSourceRegression(
                unblocked, enemy.InstanceId, 1);
            var afterTrigger = Assert.Single(triggered.Enemies);
            Assert.Equal(threshold - 2, afterTrigger.Hp);
            Assert.Equal(stunnedStateId, afterTrigger.AiStateId);
            Assert.Equal(stunMoveIndex, afterTrigger.PlannedMoveIndex);
            Assert.DoesNotContain(afterTrigger.PowerStates,
                power => power.PowerId == thresholdPowerId);
            if (thresholdPowerId == "proto.power.plow")
                Assert.DoesNotContain(afterTrigger.PowerStates,
                    power => power.PowerId == "proto.power.strength");
            else
                Assert.Contains(afterTrigger.PowerStates,
                    power => power.PowerId == "proto.power.strength"
                        && power.Stacks == 4);

            Assert.Equal(original, CanonicalJson.Sha256(combat));
            Assert.Equal(niche, Niche(triggered));
            Assert.Equal(originalAi, NativeRngV111.ToSnapshot(
                triggered.NativeRng!.RunStreams.Streams.Single(x =>
                    x.StreamId == "run.monster_ai")));
            NativeCombatRngEntryV111.Validate(triggered);
            Assert.Equal(CanonicalJson.Sha256(triggered),
                CanonicalJson.Sha256(triggered.Fork()));
        }
    }

    [Fact]
    public void LegacyPrototypeStillTriggersThresholdOnRawHpLoss()
    {
        var native = Enter("native-threshold-legacy-control",
            ActIdentity.Underdocks, PrototypeRoomType.Elite,
            "proto.encounter.terror_eel_elite", 0);
        var combat = native.World!.Combat!;
        var enemy = combat.Enemies[0] with
        {
            Hp = 71,
            Powers =
            [
                new PrototypePowerInstanceState("proto.power.shriek", 70, 1)
            ]
        };
        var legacy = combat with
        {
            NativeRng = null,
            Enemies = [enemy]
        };
        var result = PrototypeGameEngine.ApplyRawHpLossForSourceRegression(
            legacy, enemy.InstanceId, 2);
        Assert.Equal("stun", result.Enemies[0].AiStateId);
    }
}
