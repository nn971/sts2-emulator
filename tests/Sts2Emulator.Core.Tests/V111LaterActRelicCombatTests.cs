using System.Reflection;
using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111LaterActRelicCombatTests
{
    [Fact]
    public void LostWispHitsEnemyForEightAfterPlayingPower()
    {
        var engine = new PrototypeGameEngine();
        var state = Start(
            "wisp-power-trigger",
            [PrototypeNativeLaterActEventExpansion.LostWispRelicId],
            ["proto.silent.footwork"]);
        var enemy = Assert.Single(state.World!.Combat!.Enemies);
        var footwork = Assert.Single(state.World.Combat.Hand);
        var action = engine.GetLegalActions(state).Single(a =>
            a.Kind == "play_card" &&
            a.ReadPayload<PlayCardPayload>().CardInstanceId == footwork);
        var after = engine.Step(state, action).State;
        var hit = Assert.Single(after.World!.Combat!.Enemies);
        Assert.Equal(enemy.Hp - 8, hit.Hp);
    }

    [Fact]
    public void RoyalPoisonDealsFourAtFirstTurnStartThroughAnchorBlock()
    {
        var original = Start("poison-first-turn",
            ["proto.relic.anchor"], []);
        var poisoned = Start("poison-first-turn",
            ["proto.relic.anchor",
                PrototypeNativeLaterActEventExpansion.RoyalPoisonRelicId], []);
        Assert.Equal(original.World!.Combat!.PlayerBlock,
            poisoned.World!.Combat!.PlayerBlock);
        Assert.True(original.World.Combat.PlayerBlock >= 10);
        Assert.Equal(4, original.Player.Hp - poisoned.Player.Hp);
    }

    [Fact]
    public void PollinousCoreAddsTwoRealDrawnCardsOnFourthHandDraw()
    {
        var deck = PrototypeContent.StartingDeck.Take(10).ToArray();
        Assert.True(deck.Length >= 7);
        var control = Start("core-fourth-draw", [], deck);
        var boosted = Start("core-fourth-draw",
            [PrototypeNativeLaterActEventExpansion.PollinousCoreRelicId], deck,
            pollinousTurnsSeen: 3);
        Assert.Equal(control.World!.Combat!.Hand.Length + 2,
            boosted.World!.Combat!.Hand.Length);
        Assert.Equal(0, boosted.Player.Relics.Single()
            .PersistentState.GetProperty("turnsSeen").GetInt32());
        Assert.Single(boosted.Player.Relics);
    }

    private static RunState Start(string seed,
        string[] relicIds, string[] deckIds, int pollinousTurnsSeen = 0)
    {
        var player = new PlayerState(9000, 9000, 0,
            deckIds.Select((id, index) =>
                new CardInstance(index + 1, id, 0,
                    PrototypeJson.EmptyObject())).ToArray(),
            relicIds.Select(id => new RelicInstance(id,
                id == PrototypeNativeLaterActEventExpansion.PollinousCoreRelicId
                    ? JsonSerializer.SerializeToElement(
                        new { turnsSeen = pollinousTurnsSeen })
                    : PrototypeJson.EmptyObject())).ToArray(),
            new PotionInstance?[2]);
        var initial = new RunState("prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat, player,
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 3, 1, 2,
                PrototypeRoomType.Elite, new MapState([]),
                new CombatState(1, 3, 0, [], [], [], [], [],
                    1, [], [], 1, Act: 3),
                null, null, null, null,
                NextCardInstanceId: 100));
        var method = typeof(PrototypeGameEngine).GetMethod(
            "StartCombat", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<RunState>(method!.Invoke(null,
        [
            initial, PrototypeRoomType.Elite,
            PrototypeContent.Encounter(
                PrototypeNativeGloryElites.SoulNexusEncounterId)
        ]));
    }
}
