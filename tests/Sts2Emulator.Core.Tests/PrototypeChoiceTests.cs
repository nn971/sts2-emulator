using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeChoiceTests
{
    [Fact]
    public void SurvivorSuspendsForDiscardThenResumesResolution()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            Hp: 70,
            MaxHp: 70,
            Gold: 0,
            Deck:
            [
                new CardInstance(1, "proto.silent.survivor", 0, empty),
                new CardInstance(2, "proto.silent.strike", 0, empty)
            ],
            Relics: Array.Empty<RelicInstance>(),
            PotionSlots: new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1, 2],
            DrawPile: Array.Empty<long>(),
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    24,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 3,
            Cards:
            [
                new CombatCardInstance(1, 1, "proto.silent.survivor", 0, false, empty),
                new CombatCardInstance(2, 2, "proto.silent.strike", 0, false, empty)
            ]);

        var state = new RunState(
            GameBuild: "prototype-unbound",
            EmulatorSchema: "prototype-0.1",
            RunId: "choice-test",
            RunSeed: "choice-test",
            DecisionIndex: 0,
            Phase: RunPhase.Combat,
            Player: player,
            Rng: PrototypeRng.CreateBundle("choice-test"),
            ExtensionState: empty,
            World: new RunWorldState(
                RulesetId: PrototypeContent.RulesetId,
                CharacterId: PrototypeContent.CharacterId,
                Act: 1,
                Floor: 1,
                NextCardInstanceId: 3,
                ActiveRoom: PrototypeRoomType.Combat,
                Map: new MapState(Array.Empty<MapNodeState>()),
                Combat: combat,
                Reward: null,
                Shop: null,
                Event: null,
                TerminalOutcome: null));

        var engine = new PrototypeGameEngine();
        PrototypeStateInvariants.Validate(state);

        var playSurvivor = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);

        state = engine.Step(state, playSurvivor).State;
        PrototypeStateInvariants.Validate(state);

        Assert.Equal(8, state.World!.Combat!.PlayerBlock);
        Assert.NotNull(state.World.Combat.PendingChoice);
        Assert.DoesNotContain(1, state.World.Combat.Hand);

        var choice = Assert.Single(engine.GetLegalActions(state));
        Assert.Equal("select_cards", choice.Kind);
        Assert.Equal(new long[] { 2 }, choice.ReadPayload<SelectCardsPayload>().CardInstanceIds);

        state = engine.Step(state, choice).State;
        PrototypeStateInvariants.Validate(state);

        Assert.Null(state.World!.Combat!.PendingChoice);
        Assert.Empty(state.World.Combat.Hand);
        Assert.Equal(new long[] { 2, 1 }, state.World.Combat.DiscardPile);
        Assert.Equal(2, state.World.Combat.Energy);
    }

    [Fact]
    public void BladeDanceCreatesTemporaryShivsThatExhaustOnUse()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            Hp: 70,
            MaxHp: 70,
            Gold: 0,
            Deck:
            [
                new CardInstance(10, "proto.silent.blade_dance", 0, empty)
            ],
            Relics: Array.Empty<RelicInstance>(),
            PotionSlots: new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1],
            DrawPile: Array.Empty<long>(),
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    24,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 2,
            Cards:
            [
                new CombatCardInstance(1, 10, "proto.silent.blade_dance", 0, false, empty)
            ]);

        var state = new RunState(
            GameBuild: "prototype-unbound",
            EmulatorSchema: "prototype-0.1",
            RunId: "generated-card-test",
            RunSeed: "generated-card-test",
            DecisionIndex: 0,
            Phase: RunPhase.Combat,
            Player: player,
            Rng: PrototypeRng.CreateBundle("generated-card-test"),
            ExtensionState: empty,
            World: new RunWorldState(
                RulesetId: PrototypeContent.RulesetId,
                CharacterId: PrototypeContent.CharacterId,
                Act: 1,
                Floor: 1,
                NextCardInstanceId: 11,
                ActiveRoom: PrototypeRoomType.Combat,
                Map: new MapState(Array.Empty<MapNodeState>()),
                Combat: combat,
                Reward: null,
                Shop: null,
                Event: null,
                TerminalOutcome: null));

        var engine = new PrototypeGameEngine();
        var playBladeDance = engine.GetLegalActions(state)
            .Single(action => action.Kind == "play_card");

        state = engine.Step(state, playBladeDance).State;
        PrototypeStateInvariants.Validate(state);

        combat = state.World!.Combat!;
        Assert.Equal(3, combat.Hand.Length);
        Assert.Equal(new long[] { 1 }, combat.DiscardPile);
        Assert.All(
            combat.Hand,
            id =>
            {
                var card = combat.Cards.Single(item => item.InstanceId == id);
                Assert.True(card.IsTemporary);
                Assert.Null(card.PersistentCardInstanceId);
                Assert.Equal("proto.silent.shiv", card.CardId);
            });

        var shivId = combat.Hand[0];
        var playShiv = engine.GetLegalActions(state)
            .First(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == shivId);

        state = engine.Step(state, playShiv).State;
        PrototypeStateInvariants.Validate(state);

        combat = state.World!.Combat!;
        Assert.Contains(shivId, combat.ExhaustPile);
        Assert.DoesNotContain(shivId, combat.DiscardPile);
        Assert.Equal(20, combat.Enemies.Single().Hp);
    }

}
