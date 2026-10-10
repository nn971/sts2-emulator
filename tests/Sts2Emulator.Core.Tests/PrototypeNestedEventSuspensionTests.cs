using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

[Collection("MutablePrototypeContent")]
public sealed class PrototypeNestedEventSuspensionTests
{
    private const string ChoicePowerId =
        "proto.test.card_play_choice";

    [Fact]
    public void CardPlayedHookCanSuspendInsideReplayAndResumeOuterFrames()
    {
        var powers = Assert.IsType<
            Dictionary<string, PrototypePowerDefinition>>(
                PrototypeContent.Powers);

        powers.Add(
            ChoicePowerId,
            new PrototypePowerDefinition(
                ChoicePowerId,
                "Card Play Choice Fixture",
                BlockBonusPerStack: 0,
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.CardPlayed,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.ChooseCards,
                                0,
                                Selection: new(
                                    PrototypeCardZone.Hand,
                                    1,
                                    1,
                                    PrototypeCardSelectionResolutionKind
                                        .MoveToDiscard)),
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.GainPlayerBlock,
                                2)
                        ])
                ]));

        try
        {
            var source = Card(
                1,
                "proto.silent.defend",
                replayCount: 1) with
            {
                TemporaryEnergyCost =
                    new PrototypeTemporaryCardCost(
                        1,
                        PrototypeTemporaryCardCostExpiry.WhenPlayed),
                KeywordOverrides =
                [
                    new PrototypeCardKeywordOverride(
                        PrototypeCardKeyword.Retain,
                        true,
                        PrototypeCardKeywordOverrideExpiry.WhenPlayed)
                ]
            };
            var firstDiscard = Card(
                2,
                "proto.silent.strike");
            var secondDiscard = Card(
                3,
                "proto.silent.strike");

            var engine = new PrototypeGameEngine();
            var state = CreateState(
                energy: 3,
                hand:
                [
                    source,
                    firstDiscard,
                    secondDiscard
                ],
                powers:
                [
                    new PrototypePowerInstanceState(
                        ChoicePowerId,
                        1,
                        1),
                    new PrototypePowerInstanceState(
                        "proto.power.afterimage",
                        1,
                        2)
                ]);

            state = PlayCard(
                engine,
                state,
                source.InstanceId);

            var combat = state.World!.Combat!;
            Assert.NotNull(combat.PendingChoice);
            Assert.Equal(5, combat.PlayerBlock);
            Assert.Equal(2, combat.Energy);
            Assert.DoesNotContain(
                source.InstanceId,
                combat.Hand);
            Assert.DoesNotContain(
                source.InstanceId,
                combat.DrawPile);
            Assert.DoesNotContain(
                source.InstanceId,
                combat.DiscardPile);
            Assert.DoesNotContain(
                source.InstanceId,
                combat.ExhaustPile);

            state = SelectOnly(
                engine,
                state,
                firstDiscard.InstanceId);

            combat = state.World!.Combat!;
            Assert.NotNull(combat.PendingChoice);

            // First hook resumes: +2 from the synthetic subscriber and
            // +1 from later-applied Afterimage. Replay then runs the card
            // a second time, adding another 5 Block before suspending on
            // the second CardPlayed event.
            Assert.Equal(13, combat.PlayerBlock);
            Assert.Equal(2, combat.Energy);
            Assert.Contains(
                firstDiscard.InstanceId,
                combat.DiscardPile);

            // The final execution has already performed its result-pile
            // movement before CardPlayed subscribers run, but WhenPlayed
            // cleanup waits for the suspended completion event to finish.
            Assert.Equal(
                1,
                combat.DiscardPile.Count(
                    id => id == source.InstanceId));
            var sourceDuringSecondHook =
                combat.Cards.Single(
                    card => card.InstanceId
                        == source.InstanceId);
            Assert.NotNull(
                sourceDuringSecondHook.TemporaryEnergyCost);
            Assert.NotNull(
                sourceDuringSecondHook.KeywordOverrides);

            state = SelectOnly(
                engine,
                state,
                secondDiscard.InstanceId);

            combat = state.World!.Combat!;
            Assert.Null(combat.PendingChoice);
            Assert.Equal(16, combat.PlayerBlock);
            Assert.Equal(2, combat.Energy);
            Assert.Equal(
                2,
                combat.CounterState.SkillsPlayedThisTurn);
            Assert.Equal(
                1,
                combat.DiscardPile.Count(
                    id => id == source.InstanceId));
            Assert.Contains(
                firstDiscard.InstanceId,
                combat.DiscardPile);
            Assert.Contains(
                secondDiscard.InstanceId,
                combat.DiscardPile);

            var finalizedSource =
                combat.Cards.Single(
                    card => card.InstanceId
                        == source.InstanceId);
            Assert.Null(
                finalizedSource.TemporaryEnergyCost);
            Assert.Null(
                finalizedSource.KeywordOverrides);
        }
        finally
        {
            powers.Remove(ChoicePowerId);
        }
    }

    [Fact]
    public void PowerCardRemovalWaitsForSuspendedCardPlayedHook()
    {
        var powers = Assert.IsType<
            Dictionary<string, PrototypePowerDefinition>>(
                PrototypeContent.Powers);

        powers.Add(
            ChoicePowerId,
            new PrototypePowerDefinition(
                ChoicePowerId,
                "Card Play Choice Fixture",
                BlockBonusPerStack: 0,
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.CardPlayed,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.ChooseCards,
                                0,
                                Selection: new(
                                    PrototypeCardZone.Hand,
                                    1,
                                    1,
                                    PrototypeCardSelectionResolutionKind
                                        .MoveToDiscard)),
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.GainPlayerBlock,
                                2)
                        ])
                ]));

        try
        {
            var footwork = Card(
                1,
                "proto.silent.footwork");
            var discard = Card(
                2,
                "proto.silent.strike");
            var engine = new PrototypeGameEngine();
            var state = CreateState(
                energy: 3,
                hand:
                [
                    footwork,
                    discard
                ],
                powers:
                [
                    new PrototypePowerInstanceState(
                        ChoicePowerId,
                        1,
                        1)
                ]);

            state = PlayCard(
                engine,
                state,
                footwork.InstanceId);

            var combat = state.World!.Combat!;
            Assert.NotNull(combat.PendingChoice);
            Assert.Contains(
                combat.Cards,
                card => card.InstanceId
                    == footwork.InstanceId);
            Assert.DoesNotContain(
                footwork.InstanceId,
                combat.Hand);
            Assert.DoesNotContain(
                footwork.InstanceId,
                combat.DrawPile);
            Assert.DoesNotContain(
                footwork.InstanceId,
                combat.DiscardPile);
            Assert.DoesNotContain(
                footwork.InstanceId,
                combat.ExhaustPile);
            Assert.Contains(
                combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.dexterity");

            state = SelectOnly(
                engine,
                state,
                discard.InstanceId);

            combat = state.World!.Combat!;
            Assert.Null(combat.PendingChoice);
            Assert.DoesNotContain(
                combat.Cards,
                card => card.InstanceId
                    == footwork.InstanceId);
            Assert.Equal(2, combat.PlayerBlock);
            Assert.Contains(
                discard.InstanceId,
                combat.DiscardPile);
            Assert.Contains(
                combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.dexterity");
        }
        finally
        {
            powers.Remove(ChoicePowerId);
        }
    }

    private static RunState PlayCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "play_card"
                && item.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == cardInstanceId);
        return engine.Step(state, action).State;
    }

    private static RunState SelectOnly(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "select_cards"
                && item.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual(
                        [cardInstanceId]));
        return engine.Step(state, action).State;
    }

    private static CombatCardInstance Card(
        long instanceId,
        string cardId,
        int replayCount = 0) =>
        new(
            instanceId,
            1000 + instanceId,
            cardId,
            0,
            false,
            PrototypeJson.EmptyObject(),
            ReplayCount: replayCount);

    private static RunState CreateState(
        int energy,
        CombatCardInstance[] hand,
        PrototypePowerInstanceState[] powers)
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            hand.Select(card => new CardInstance(
                card.PersistentCardInstanceId!.Value,
                card.CardId,
                card.UpgradeLevel,
                card.State.Clone())).ToArray(),
            Array.Empty<RelicInstance>(),
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: energy,
            PlayerBlock: 0,
            Hand: hand.Select(
                card => card.InstanceId).ToArray(),
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    999,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            NextCardInstanceId:
                hand.Max(card => card.InstanceId) + 1,
            Cards: hand,
            PlayerPowers: powers,
            NextPowerApplicationOrder:
                powers.Max(
                    power => power.ApplicationOrder) + 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "nested-event-suspension-test",
            "nested-event-suspension-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "nested-event-suspension-test"),
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
                null));
    }
}
