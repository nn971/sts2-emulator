using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeTwigSlimeMTests
{
    [Fact]
    public void DefinitionMatchesPinnedActOneData()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.twig_slime_m");

        Assert.Equal("Twig Slime (M)", enemy.Name);
        Assert.Equal((26, 28), enemy.HpRangeAt(1, 0));
        Assert.Equal((27, 29), enemy.HpRangeAt(1, 8));
        Assert.Equal(
            PrototypeEnemyMovePolicy.StateMachine,
            enemy.MovePolicy);

        Assert.Collection(
            enemy.Moves,
            pounce =>
            {
                Assert.Equal("pokey_pounce", pounce.Id);
                var damage = Assert.Single(pounce.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.DamagePlayer,
                    damage.Kind);
                Assert.Equal(11, damage.AmountAt(1, 8));
                Assert.Equal(12, damage.AmountAt(1, 9));
            },
            sticky =>
            {
                Assert.Equal("sticky_shot", sticky.Id);
                var status = Assert.Single(sticky.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.AddCardsToDiscard,
                    status.Kind);
                Assert.Equal("proto.status.slimed", status.CardId);
                Assert.Equal(1, status.Amount);
            });

        var ai = Assert.IsType<
            PrototypeEnemyAiDefinition>(enemy.Ai);
        Assert.Equal("sticky_shot_move", ai.InitialStateId);

        var random = Assert.Single(
            ai.States,
            state => state.Id == "rand");
        Assert.Collection(
            random.Branches!,
            pounce =>
            {
                Assert.Equal(
                    "pokey_pounce_move",
                    pounce.TargetStateId);
                Assert.Equal(
                    PrototypeEnemyAiRepeatRule.CanRepeatXTimes,
                    pounce.RepeatRule);
                Assert.Equal(2, pounce.MaxTimes);
            },
            sticky =>
            {
                Assert.Equal(
                    "sticky_shot_move",
                    sticky.TargetStateId);
                Assert.Equal(
                    PrototypeEnemyAiRepeatRule.CannotRepeat,
                    sticky.RepeatRule);
            });
    }

    [Fact]
    public void NativeOpenerAddsOneSlimed()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState("twig-slime-m-opener");

        state = EndTurn(engine, state);

        var combat = state.World!.Combat!;
        Assert.Equal(
            "sticky_shot",
            Assert.Single(combat.Enemies).LastMoveId);
        var slimedId = Assert.Single(combat.DiscardPile);
        Assert.Equal(
            "proto.status.slimed",
            Assert.Single(
                combat.Cards,
                card => card.InstanceId == slimedId)
                .CardId);
        Assert.Equal(100, state.Player.Hp);
    }

    [Fact]
    public void PounceMayRepeatTwiceButNeverThreeTimes()
    {
        var engine = new PrototypeGameEngine();

        var sawSecondPounce = false;
        for (var seed = 0; seed < 64; seed++)
        {
            var state = CreateState(
                $"twig-slime-m-repeat-{seed}",
                aiStateId: "rand",
                lastMoveId: "pokey_pounce",
                consecutiveMoveUses: 1);

            state = EndTurn(engine, state);
            if (Assert.Single(
                    state.World!.Combat!.Enemies)
                    .LastMoveId == "pokey_pounce")
            {
                sawSecondPounce = true;
                break;
            }
        }

        Assert.True(
            sawSecondPounce,
            "Pokey Pounce should remain eligible after one consecutive use.");

        var capped = CreateState(
            "twig-slime-m-cap",
            aiStateId: "rand",
            lastMoveId: "pokey_pounce",
            consecutiveMoveUses: 2);

        capped = EndTurn(engine, capped);

        Assert.Equal(
            "sticky_shot",
            Assert.Single(
                capped.World!.Combat!.Enemies)
                .LastMoveId);
    }

    [Fact]
    public void A9PounceUsesScaledDamage()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            "twig-slime-m-a9",
            ascension: 9,
            aiStateId: "pokey_pounce_move");

        state = EndTurn(engine, state);

        Assert.Equal(88, state.Player.Hp);
        Assert.Equal(
            "pokey_pounce",
            Assert.Single(
                state.World!.Combat!.Enemies)
                .LastMoveId);
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
        string seed,
        int ascension = 0,
        string? aiStateId = null,
        string? lastMoveId = null,
        int consecutiveMoveUses = 0)
    {
        var player = new PlayerState(
            100,
            100,
            0,
            [],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);

        var fillerCards = Enumerable.Range(1, 5)
            .Select(instanceId =>
                new CombatCardInstance(
                    instanceId,
                    1000 + instanceId,
                    "proto.silent.strike",
                    0,
                    false,
                    PrototypeJson.EmptyObject()))
            .ToArray();

        var enemy = PrototypeContent.Enemy(
            "proto.enemy.twig_slime_m");

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [],
            DrawPile: fillerCards
                .Select(card => card.InstanceId)
                .ToArray(),
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    enemy.Id,
                    enemy.HpRangeAt(1, ascension).Max,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal),
                    LastMoveId: lastMoveId,
                    ConsecutiveMoveUses:
                        consecutiveMoveUses,
                    AiStateId: aiStateId,
                    MoveUseCounts:
                        new Dictionary<string, int>(
                            StringComparer.Ordinal))
            ],
            NextCardInstanceId: 6,
            Cards: fillerCards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            seed,
            seed,
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                5000,
                PrototypeRoomType.Combat,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null),
            Ascension: ascension);
    }
}
