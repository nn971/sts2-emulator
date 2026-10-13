using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeSlitheringStranglerTests
{
    [Fact]
    public void DefinitionMatchesPinnedOvergrowthData()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.slithering_strangler");

        Assert.Equal("Slithering Strangler", enemy.Name);
        Assert.Equal((53, 55), enemy.HpRangeAt(1, 0));
        Assert.Equal((54, 56), enemy.HpRangeAt(1, 8));

        Assert.Collection(
            enemy.Moves,
            constrict =>
            {
                Assert.Equal("constrict", constrict.Id);
                var apply = Assert.Single(constrict.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.ApplyPlayerPower,
                    apply.Kind);
                Assert.Equal("proto.power.constrict", apply.PowerId);
                Assert.Equal(3, apply.Amount);
            },
            thwack =>
            {
                Assert.Equal("thwack", thwack.Id);
                Assert.Collection(
                    thwack.Effects,
                    damage =>
                    {
                        Assert.Equal(7, damage.AmountAt(1, 8));
                        Assert.Equal(8, damage.AmountAt(1, 9));
                    },
                    block =>
                    {
                        Assert.Equal(
                            PrototypeEnemyEffectKind.GainBlock,
                            block.Kind);
                        Assert.Equal(5, block.Amount);
                    });
            },
            lash =>
            {
                Assert.Equal("lash", lash.Id);
                var damage = Assert.Single(lash.Effects);
                Assert.Equal(12, damage.AmountAt(1, 8));
                Assert.Equal(13, damage.AmountAt(1, 9));
            });

        var constrictPower = PrototypeContent.Power(
            "proto.power.constrict");
        Assert.True(constrictPower.SourceBoundToEnemy);
        Assert.True(constrictPower.IsDebuff);
        var trigger = Assert.Single(constrictPower.Triggers);
        Assert.Equal(
            PrototypeCombatEventKind.PlayerTurnEnded,
            trigger.EventKind);
        var effect = Assert.Single(trigger.Effects);
        Assert.Equal(
            PrototypeCombatEffectKind.DamagePlayer,
            effect.Kind);
        Assert.Equal(1, effect.AmountPerPowerStack);
    }

    [Fact]
    public void ConstrictStacksAndTicksAtPlayerTurnEnd()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState();

        state = EndTurn(engine, state);
        Assert.Equal(100, state.Player.Hp);
        Assert.Equal(3, ConstrictStacks(state));
        Assert.Equal("constrict", LastMove(state));

        state = EndTurn(engine, state);
        Assert.True(state.Player.Hp is 85 or 90);
        Assert.Equal(3, ConstrictStacks(state));
        Assert.Contains(
            LastMove(state),
            new[] { "thwack", "lash" });

        var hpBeforeThirdEnd = state.Player.Hp;
        state = EndTurn(engine, state);
        Assert.Equal(hpBeforeThirdEnd - 3, state.Player.Hp);
        Assert.Equal(6, ConstrictStacks(state));
        Assert.Equal("constrict", LastMove(state));

        var hpBeforeFourthEnd = state.Player.Hp;
        state = EndTurn(engine, state);
        Assert.True(
            state.Player.Hp == hpBeforeFourthEnd - 13
            || state.Player.Hp == hpBeforeFourthEnd - 18);
    }

    [Fact]
    public void StranglerAndFriendVariantsAreAllReachable()
    {
        var seenJaxfruit = false;
        var seenLeafMedium = false;
        var seenTwigMedium = false;
        var seenSmallPair = false;

        for (var seed = 0; seed < 128; seed++)
        {
            var specs = PrototypeContent.Encounter(
                    "proto.encounter.slithering_strangler_normal")
                .ResolveEnemySpecs(
                    PrototypeRng.CreateBundle(
                        $"strangler-variant-{seed}"));

            Assert.Equal(
                "proto.enemy.slithering_strangler",
                specs[0].EnemyId);

            var companions = specs
                .Skip(1)
                .Select(spec => spec.EnemyId)
                .ToArray();

            if (companions.SequenceEqual(
                    ["proto.enemy.snapping_jaxfruit"]))
            {
                seenJaxfruit = true;
            }
            else if (companions.SequenceEqual(
                         ["proto.enemy.leaf_slime_m"]))
            {
                seenLeafMedium = true;
            }
            else if (companions.SequenceEqual(
                         ["proto.enemy.twig_slime_m"]))
            {
                seenTwigMedium = true;
            }
            else if (companions.SequenceEqual(
                         [
                             "proto.enemy.leaf_slime_s",
                             "proto.enemy.twig_slime_s"
                         ]))
            {
                seenSmallPair = true;
            }
            else
            {
                throw new Xunit.Sdk.XunitException(
                    $"Unexpected Strangler companion composition: {string.Join(",", companions)}");
            }

            if (seenJaxfruit
                && seenLeafMedium
                && seenTwigMedium
                && seenSmallPair)
            {
                break;
            }
        }

        Assert.True(seenJaxfruit);
        Assert.True(seenLeafMedium);
        Assert.True(seenTwigMedium);
        Assert.True(seenSmallPair);
    }

    [Fact]
    public void KillingSourceClearsConstrictWhileCombatContinues()
    {
        var strike = new CombatCardInstance(
            1,
            null,
            "proto.silent.strike",
            0,
            true,
            PrototypeJson.EmptyObject());
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            stranglerHp: 4,
            extraEnemy: true,
            cards: [strike],
            drawPile: [1]);

        state = EndTurn(engine, state);

        Assert.Equal(3, ConstrictStacks(state));

        var strikeAction = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 1
                && action.ReadPayload<PlayCardPayload>()
                    .TargetEnemyId == 1);
        state = engine.Step(state, strikeAction).State;

        var combat = state.World!.Combat!;
        Assert.Equal(
            0,
            combat.Enemies
                .Single(enemy => enemy.InstanceId == 1)
                .Hp);
        Assert.DoesNotContain(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.constrict");
    }

    private static int ConstrictStacks(RunState state) =>
        Assert.Single(
            state.World!.Combat!.PlayerPowers,
            power => power.PowerId == "proto.power.constrict")
            .Stacks;

    private static string LastMove(RunState state) =>
        state.World!.Combat!.Enemies
            .Single(enemy => enemy.InstanceId == 1)
            .LastMoveId!;

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static RunState CreateState(
        int stranglerHp = 55,
        bool extraEnemy = false,
        CombatCardInstance[]? cards = null,
        long[]? drawPile = null)
    {
        cards ??= [];
        drawPile ??= [];
        var enemies = new List<EnemyCombatState>
        {
            new(
                1,
                "proto.enemy.slithering_strangler",
                stranglerHp,
                0,
                0,
                new Dictionary<string, int>(
                    StringComparer.Ordinal),
                FormationPosition: 0)
        };
        if (extraEnemy)
        {
            enemies.Add(
                new EnemyCombatState(
                    2,
                    "proto.enemy.twig_slime_s",
                    11,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal),
                    FormationPosition: 1));
        }

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
            Hand: [],
            DrawPile: drawPile,
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: enemies.ToArray(),
            NextCardInstanceId:
                cards.Length == 0 ? 1 : cards.Max(card => card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "strangler-test",
            "strangler-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "strangler-test"),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                4,
                5000,
                PrototypeRoomType.Combat,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null),
            Ascension: 0);
    }
}
