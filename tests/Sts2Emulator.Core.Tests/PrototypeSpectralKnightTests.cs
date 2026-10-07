using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeSpectralKnightTests
{
    [Fact]
    public void DefinitionUsesPinnedOpeningAndRandomRepeatLimits()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.spectral_knight");

        Assert.Equal("Spectral Knight", enemy.Name);
        Assert.Equal(93, enemy.MaxHp);
        Assert.Equal(0, enemy.HpPerAct);
        Assert.Equal(
            PrototypeEnemyMovePolicy.UniformRandomAfterOpener,
            enemy.MovePolicy);
        Assert.Equal([0, 1], enemy.OpeningMoveIndices);
        Assert.Equal(1, enemy.RandomMovePoolStartIndex);
        Assert.Equal(
            ["hex", "soul_slash", "soul_flame"],
            enemy.Moves.Select(move => move.Id).ToArray());

        var hex = Assert.Single(enemy.Moves[0].Effects);
        Assert.Equal(
            PrototypeEnemyEffectKind.ApplyPlayerPower,
            hex.Kind);
        Assert.Equal(2, hex.Amount);
        Assert.Equal("proto.power.hex", hex.PowerId);

        var slash = Assert.Single(enemy.Moves[1].Effects);
        Assert.Equal(
            PrototypeEnemyEffectKind.DamagePlayer,
            slash.Kind);
        Assert.Equal(15, slash.Amount);

        var flame = Assert.Single(enemy.Moves[2].Effects);
        Assert.Equal(
            PrototypeEnemyEffectKind.DamagePlayer,
            flame.Kind);
        Assert.Equal(3, flame.Amount);
        Assert.Equal(3, flame.Repetitions);
        Assert.Equal(2, enemy.Moves[1].MaxConsecutiveUses);
        Assert.Equal(1, enemy.Moves[2].MaxConsecutiveUses);
    }

    [Fact]
    public void ArtifactBlocksOpeningHexAndConsumesOneStack()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            powers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.artifact",
                    2,
                    1)
            ]);

        state = EndTurn(engine, state);

        var combat = state.World!.Combat!;
        Assert.DoesNotContain(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.hex");

        var artifact = Assert.Single(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.artifact");
        Assert.Equal(1, artifact.Stacks);

        Assert.All(
            combat.Cards,
            card => Assert.False(
                card.Affliction?.Kind
                    == PrototypeCardAfflictionKind.Hexed));

        // Spectral Knight proceeds to its normal post-opener loop;
        // blocked Hex is not retried.
        state = EndTurn(engine, state);
        Assert.Equal(55, state.Player.Hp);
        Assert.DoesNotContain(
            state.World!.Combat!.PlayerPowers,
            power => power.PowerId == "proto.power.hex");
    }

    [Fact]
    public void CombatPatternStartsHexSlashThenRespectsRandomLimits()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            hp: 10_000);
        var observed = new List<string>();

        for (var turn = 0; turn < 30; turn++)
        {
            state = EndTurn(engine, state);
            observed.Add(
                Assert.Single(
                    state.World!.Combat!.Enemies)
                    .LastMoveId!);
        }

        Assert.Equal("hex", observed[0]);
        Assert.Equal("soul_slash", observed[1]);
        Assert.All(
            observed.Skip(2),
            move => Assert.Contains(
                move,
                new[] { "soul_slash", "soul_flame" }));

        AssertRepeatLimits(
            observed.Skip(1).ToArray());

        var hex = Assert.Single(
            state.World!.Combat!.PlayerPowers,
            power => power.PowerId == "proto.power.hex");
        Assert.Equal(2, hex.Stacks);
        Assert.Equal(1, hex.SourceEnemyInstanceId);
    }

    [Fact]
    public void NativeCapture003SequenceFitsCorrectedPolicy()
    {
        var observed = new[]
        {
            "hex",
            "soul_slash",
            "soul_slash",
            "soul_flame",
            "soul_slash",
            "soul_flame",
            "soul_slash",
            "soul_slash",
            "soul_flame"
        };

        Assert.Equal("hex", observed[0]);
        Assert.Equal("soul_slash", observed[1]);
        AssertRepeatLimits(
            observed.Skip(1).ToArray());
    }

    private static void AssertRepeatLimits(
        string[] observed)
    {
        for (var index = 0; index < observed.Length;)
        {
            var move = observed[index];
            var end = index + 1;
            while (end < observed.Length
                && StringComparer.Ordinal.Equals(
                    observed[end],
                    move))
            {
                end++;
            }

            var runLength = end - index;
            var maximum = move switch
            {
                "soul_slash" => 2,
                "soul_flame" => 1,
                _ => int.MaxValue
            };
            Assert.InRange(runLength, 1, maximum);
            index = end;
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

    private static RunState CreateState(
        PrototypePowerInstanceState[]? powers = null,
        int hp = 70)
    {
        powers ??= [];
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            hp,
            hp,
            0,
            [],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.spectral_knight",
                    93,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            NextCardInstanceId: 1,
            Cards: [],
            PlayerPowers: powers,
            NextPowerApplicationOrder:
                powers.Length == 0
                    ? 1
                    : powers.Max(
                        power => power.ApplicationOrder) + 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "spectral-knight-test",
            "spectral-knight-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "spectral-knight-test"),
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
