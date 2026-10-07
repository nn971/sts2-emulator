using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeSpectralKnightTests
{
    [Fact]
    public void DefinitionUsesPinnedA0OpeningAndAlternatingLoop()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.spectral_knight");

        Assert.Equal("Spectral Knight", enemy.Name);
        Assert.Equal(93, enemy.MaxHp);
        Assert.Equal(0, enemy.HpPerAct);
        Assert.Equal(1, enemy.MoveLoopStartIndex);
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
    public void CombatPatternHexThenAlternatesSlashAndFlame()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState();

        state = EndTurn(engine, state);
        var combat = state.World!.Combat!;
        Assert.Equal(70, state.Player.Hp);
        var hex = Assert.Single(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.hex");
        Assert.Equal(2, hex.Stacks);
        Assert.Equal(1, hex.SourceEnemyInstanceId);

        state = EndTurn(engine, state);
        Assert.Equal(55, state.Player.Hp);

        state = EndTurn(engine, state);
        Assert.Equal(46, state.Player.Hp);

        state = EndTurn(engine, state);
        Assert.Equal(31, state.Player.Hp);

        combat = state.World!.Combat!;
        Assert.Equal(
            4,
            Assert.Single(combat.Enemies).MoveIndex);
        Assert.Equal(
            2,
            Assert.Single(
                combat.PlayerPowers,
                power => power.PowerId == "proto.power.hex")
                .Stacks);
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
        PrototypePowerInstanceState[]? powers = null)
    {
        powers ??= [];
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
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
