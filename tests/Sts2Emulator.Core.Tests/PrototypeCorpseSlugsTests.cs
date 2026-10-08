using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeCorpseSlugsTests
{
    private const string SlugId = "proto.enemy.corpse_slug";
    private const string WeakEncounterId = "proto.encounter.corpse_slugs_weak";

    [Fact]
    public void CorpseSlugModelsPinnedHpMoveCycleAndRavenous()
    {
        var slug = PrototypeContent.Enemy(SlugId);
        Assert.Equal((25, 27), slug.HpRangeAt(1, 0));
        Assert.Equal((27, 29), slug.HpRangeAt(1, 8));
        Assert.Equal(new[] { "whip_slap", "glomp", "goop" },
            slug.Moves.Select(move => move.Id));
        Assert.Equal(2, slug.Moves[0].Effects[0].Repetitions);
        Assert.Equal(2, slug.Moves[2].Effects[0].Amount);
        Assert.Equal("proto.power.frail", slug.Moves[2].Effects[0].PowerId);
        Assert.Equal(4, slug.StartingPowers![0].StacksAt(0));
        Assert.Equal(5, slug.StartingPowers![0].StacksAt(9));

        var ravenous = PrototypeContent.Power("proto.power.ravenous");
        Assert.Equal(1, ravenous.AllyDeathStrengthPerStack);
        Assert.True(ravenous.StunOnAllyDeath);
        var weak = PrototypeContent.Encounter(WeakEncounterId);
        Assert.Equal(2, weak.EnemyIds.Length);
        Assert.Equal(new[] { "whip", "glomp", "goop" },
            weak.CyclicOpeningAiStateIds);
        Assert.DoesNotContain(WeakEncounterId,
            PrototypeContent.OvergrowthWeakEncounterPool);
    }

    [Fact]
    public void EncounterUsesDistinctCyclicStartingMovesAndForkedDeterminism()
    {
        var starts = new HashSet<string>(StringComparer.Ordinal);
        for (var seedIndex = 0; seedIndex < 42; seedIndex++)
        {
            var state = FindCorpseSlugsCombat("slug-cycle-" + seedIndex);
            var slugs = state.World!.Combat!.Enemies;
            Assert.Equal(2, slugs.Length);
            Assert.All(slugs, slug =>
            {
                Assert.Equal(SlugId, slug.EnemyId);
                Assert.Single(slug.PowerStates);
                Assert.Equal("proto.power.ravenous", slug.PowerStates[0].PowerId);
            });
            Assert.NotEqual(slugs[0].AiStateId, slugs[1].AiStateId);
            var cycle = new[] { "whip", "glomp", "goop" };
            var first = Array.IndexOf(cycle, slugs[0].AiStateId);
            Assert.InRange(first, 0, 2);
            Assert.Equal(cycle[(first + 1) % 3], slugs[1].AiStateId);
            starts.Add(slugs[0].AiStateId!);
            PrototypeStateInvariants.Validate(state);
            Assert.Equal(CanonicalJson.Sha256(state),
                CanonicalJson.Sha256(state.Fork()));
        }
        Assert.Equal(3, starts.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RavenousSurvivorGainsStrengthAndStunsOnAllyDeath(
        bool killedByPoison)
    {
        var engine = new PrototypeGameEngine();
        var state = FindCorpseSlugsCombat(
            killedByPoison ? "ravenous-poison" : "ravenous-strike");
        var combat = state.World!.Combat!;
        var deadTarget = combat.Enemies[0].InstanceId;
        var survivorId = combat.Enemies[1].InstanceId;

        if (killedByPoison)
        {
            combat = combat with
            {
                Enemies = combat.Enemies.Select(enemy =>
                    enemy.InstanceId == deadTarget
                        ? enemy with
                        {
                            Hp = 1,
                            Statuses = new Dictionary<string, int>(
                                StringComparer.Ordinal)
                            {
                                ["proto.status.poison"] = 2
                            }
                        }
                        : enemy).ToArray()
            };
            state = state with
            {
                World = state.World with { Combat = combat }
            };
            var before = state.Player.Hp;
            var endTurn = engine.GetLegalActions(state).Single(action =>
                action.Kind == "end_turn");
            state = engine.Step(state, endTurn).State;
            Assert.Equal(before, state.Player.Hp);
            Assert.True(state.World!.Combat!.Enemies[0].Hp == 0);
            // The ally is stunned during the same upcoming enemy turn.
            Assert.Equal(0, state.World.Combat.Enemies[1]
                .EnemyActionSkipsRemaining);
        }
        else
        {
            var strike = combat.Cards.First(card =>
                card.CardId == "proto.silent.strike");
            combat = combat with
            {
                Hand = [strike.InstanceId],
                DrawPile = combat.Cards
                    .Where(card => card.InstanceId != strike.InstanceId)
                    .Select(card => card.InstanceId).ToArray(),
                DiscardPile = [],
                ExhaustPile = [],
                PlayPile = [],
                Energy = 3,
                Enemies = combat.Enemies.Select(enemy =>
                    enemy.InstanceId == deadTarget
                        ? enemy with { Hp = 1 }
                        : enemy).ToArray()
            };
            state = state with
            {
                World = state.World with { Combat = combat }
            };
            var play = engine.GetLegalActions(state).Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().TargetEnemyId
                    == deadTarget);
            state = engine.Step(state, play).State;
            Assert.Equal(1, state.World!.Combat!.Enemies[1]
                .EnemyActionSkipsRemaining);
        }

        var next = state.World!.Combat!;
        var surviving = next.Enemies.Single(enemy =>
            enemy.InstanceId == survivorId);
        Assert.True(surviving.Hp > 0);
        Assert.Equal(4, Assert.Single(surviving.PowerStates,
            power => power.PowerId == "proto.power.ravenous").Stacks);
        Assert.Equal(4, Assert.Single(surviving.PowerStates,
            power => power.PowerId == "proto.power.strength").Stacks);
        Assert.DoesNotContain(next.Enemies[0].PowerStates,
            p => p.PowerId == "proto.power.strength");
    }

    private static RunState FindCorpseSlugsCombat(string seed)
    {
        var engine = new PrototypeGameEngine();
        for (var attempt = 0; attempt < 64; attempt++)
        {
            var initial = PrototypeNativeUnderdocksRunFactory.Create(
                seed + "-" + attempt);
            var state = initial with
            {
                Phase = RunPhase.MapChoice,
                World = initial.World! with { Event = null }
            };
            var action = engine.GetLegalActions(state)
                .First(a => a.Kind == "choose_map_node");
            state = engine.Step(state, action).State;
            if (state.World!.EncounterIds[^1] == WeakEncounterId)
            {
                return state;
            }
        }
        throw new InvalidOperationException(
            "No Corpse Slugs weak encounter found across 64 seeds.");
    }
}
