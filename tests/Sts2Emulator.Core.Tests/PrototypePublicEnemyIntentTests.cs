using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypePublicEnemyIntentTests
{
    [Fact]
    public void DeterministicTwoHitIntentMatchesActualDamageWithStrengthAndVulnerable()
    {
        // Assassin's announced flurry is two attacks for 5 each.
        // Strength +2 raises each hit to 7, and Vulnerable (3/2)
        // raises the displayed per-hit attack damage to 10.
        var state = CreateState(
            "proto.enemy.assassin",
            enemyPowers:
            [
                new PrototypePowerInstanceState("proto.power.strength", 2, 1)
            ],
            playerPowers:
            [
                new PrototypePowerInstanceState("proto.power.vulnerable", 2, 1)
            ]);
        var environment = new PrototypeAiEnvironment();
        var frame = environment.Observe(state);
        var intent = Assert.Single(frame.Observation.Combat!.Enemies);

        Assert.Equal("proto.enemy.assassin", intent.EnemyId);
        Assert.Equal("flurry", intent.MoveId);
        Assert.Equal(10, intent.IntentDamage);
        Assert.Equal(2, intent.IntentHits);

        using (var json = JsonDocument.Parse(
            CanonicalJson.Serialize(frame.Observation)))
        {
            var enemy = json.RootElement.GetProperty("combat")
                .GetProperty("enemies")[0];
            Assert.Equal(10, enemy.GetProperty("intent_damage").GetInt32());
            Assert.Equal(2, enemy.GetProperty("intent_hits").GetInt32());
        }

        // Projection never mutates the state/RNG and the real combat resolves
        // using the shared damage calculation.
        var before = CanonicalJson.Sha256(state);
        Assert.Equal(frame.ObservationHash, environment.Observe(state).ObservationHash);
        Assert.Equal(before, CanonicalJson.Sha256(state));
        var endTurn = frame.LegalActions.Single(action => action.Kind == "end_turn");
        var next = environment.Step(state, endTurn.ActionId).State;
        Assert.Equal(80, next.Player.Hp);
    }

    [Fact]
    public void AscensionAndOpenedRandomEnemyShowCorrectKnownAttack()
    {
        // The random-after-opener policy has a forced FIRST move at index 2
        // ("ram"), not the definition's index-zero "war_chant".
        var state = CreateState("proto.enemy.flail_knight", ascension: 9);
        var frame = new PrototypeAiEnvironment().Observe(state);
        var intent = Assert.Single(frame.Observation.Combat!.Enemies);
        Assert.Equal("ram", intent.MoveId);
        Assert.Equal(17, intent.IntentDamage);
        Assert.Equal(1, intent.IntentHits);

        var endTurn = frame.LegalActions.Single(action => action.Kind == "end_turn");
        var next = new PrototypeAiEnvironment().Step(state, endTurn.ActionId).State;
        Assert.Equal(83, next.Player.Hp);
    }

    [Fact]
    public void SequentialLoopAndNonattackingMovesHaveHonestMetadata()
    {
        // The raider's guard_slash gains block and then attacks for 6.
        var frame = new PrototypeAiEnvironment().Observe(
            CreateState("proto.enemy.raider", moveIndex: 1));
        var enemy = Assert.Single(frame.Observation.Combat!.Enemies);
        Assert.Equal("guard_slash", enemy.MoveId);
        Assert.Equal(6, enemy.IntentDamage);
        Assert.Equal(1, enemy.IntentHits);

        // The Flail Knight's forced opener is "ram" (index two) rather
        // than definition-index-zero "war_chant".
        var opening = new PrototypeAiEnvironment().Observe(
            CreateState("proto.enemy.flail_knight", moveIndex: 0));
        Assert.Equal("ram", Assert.Single(
            opening.Observation.Combat!.Enemies).MoveId);

        // Ceremonial Beast starts at a deterministic state-machine
        // nonattack move; after the transition its next move is plow.
        var stamp = new PrototypeAiEnvironment().Observe(
            CreateState("proto.enemy.ceremonial_beast"));
        var nonAttack = Assert.Single(stamp.Observation.Combat!.Enemies);
        Assert.Equal("stamp", nonAttack.MoveId);
        Assert.Null(nonAttack.IntentDamage);
        Assert.Null(nonAttack.IntentHits);
        AssertNoDamageOrHits(stamp);

        var plow = new PrototypeAiEnvironment().Observe(
            CreateState("proto.enemy.ceremonial_beast",
                aiStateId: "plow_move"));
        var nextMove = Assert.Single(plow.Observation.Combat!.Enemies);
        Assert.Equal("plow", nextMove.MoveId);
        Assert.Equal(18, nextMove.IntentDamage);
        Assert.Equal(1, nextMove.IntentHits);
    }

    [Fact]
    public void UnresolvedRandomMoveDoesNotLeakPrivateFutureIntent()
    {
        // Once its forced opening move has been used, the Flail Knight
        // randomly selects a new move when the enemy turn resolves.
        // The projection must not expose the impending RNG choice.
        var frame = new PrototypeAiEnvironment().Observe(
            CreateState("proto.enemy.flail_knight", moveIndex: 1));
        var enemy = Assert.Single(frame.Observation.Combat!.Enemies);
        Assert.Null(enemy.MoveId);
        Assert.Null(enemy.IntentDamage);
        Assert.Null(enemy.IntentHits);
        AssertNoDamageOrHits(frame);
    }

    [Fact]
    public void RunicDomeHidesMoveDamageAndHitsInEveryPublicView()
    {
        var frame = new PrototypeAiEnvironment().Observe(
            CreateState("proto.enemy.assassin", hideIntents: true));
        var enemy = Assert.Single(frame.Observation.Combat!.Enemies);
        Assert.Equal("proto.enemy.assassin", enemy.EnemyId);
        Assert.Null(enemy.MoveId);
        Assert.Null(enemy.IntentDamage);
        Assert.Null(enemy.IntentHits);
        AssertNoDamageOrHits(frame);

        // Neither serialization nor repeated observation may mutate
        // canonical state or advance the random stream.
        var frameAgain = new PrototypeAiEnvironment().Observe(
            CreateState("proto.enemy.assassin", hideIntents: true));
        Assert.Equal(frame.ObservationHash, frameAgain.ObservationHash);
    }

    [Fact]
    public void VisibleWeakStatusReducesDamageBeforeIncomingModifiers()
    {
        var state = CreateState(
            "proto.enemy.assassin",
            enemyStatuses: new Dictionary<string, int>(
                StringComparer.Ordinal)
            {
                ["proto.status.weak"] = 2
            });
        var intent = Assert.Single(
            new PrototypeAiEnvironment().Observe(state)
                .Observation.Combat!.Enemies);
        // Weak scales 5 damage by 3/4 with integer rounding.
        Assert.Equal(3, intent.IntentDamage);
        Assert.Equal(2, intent.IntentHits);
    }

    private static void AssertNoDamageOrHits(PrototypeAiFrame frame)
    {
        using var json = JsonDocument.Parse(
            CanonicalJson.Serialize(frame.Observation));
        var enemy = json.RootElement.GetProperty("combat")
            .GetProperty("enemies")[0];
        Assert.False(enemy.TryGetProperty("intent_damage", out _));
        Assert.False(enemy.TryGetProperty("intent_hits", out _));
    }

    private static RunState CreateState(
        string enemyId,
        int moveIndex = 0,
        int ascension = 0,
        bool hideIntents = false,
        PrototypePowerInstanceState[]? enemyPowers = null,
        PrototypePowerInstanceState[]? playerPowers = null,
        Dictionary<string, int>? enemyStatuses = null,
        string? aiStateId = null)
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            100, 100, 0,
            [new CardInstance(1, "proto.silent.defend", 0, empty)],
            hideIntents
                ? [new RelicInstance("proto.relic.runic_dome", empty)]
                : [],
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);
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
                    enemyId,
                    80,
                    0,
                    moveIndex,
                    enemyStatuses
                        ?? new Dictionary<string, int>(StringComparer.Ordinal),
                    Powers: enemyPowers,
                    AiStateId: aiStateId)
            ],
            NextCardInstanceId: 1,
            Cards: [],
            PlayerPowers: playerPowers ?? [],
            NextPowerApplicationOrder: 5,
            Act: 1,
            Ascension: ascension);
        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "visible-intent-test",
            "visible-intent-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("visible-intent-test"),
            empty,
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
