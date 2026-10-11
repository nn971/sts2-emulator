using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111GloryTinkerTests
{
    private static readonly PrototypeGameEngine Engine = new();
    private const string Tinker = PrototypeNativeGloryEvents.TinkerTimeId;
    private const string Science = PrototypeNativeGloryEvents.MadScienceId;

    [Fact]
    public void SourceCatalogAndSinglePersistentCardIdentityAreGated()
    {
        var definition = PrototypeContent.Event(Tinker);
        Assert.Equal((3, 3, 0),
            (definition.MinAct, definition.MaxAct, definition.Weight));
        var card = PrototypeContent.Card(Science);
        Assert.Equal("Mad Science", card.Name);
        Assert.Equal(PrototypeCardRarity.Event, card.Rarity);
        Assert.True(card.InnateOnUpgrade);
        Assert.DoesNotContain(Science, PrototypeContent.RewardCardPool);
        Assert.Equal(new[] { "choose_card_type" },
            Choices(Glory("tinker-initial")));
    }

    [Fact]
    public void TwoRandomTypesAndRidersArePersistedAndForkStable()
    {
        var seen = new HashSet<string>();
        for (var i = 0; i < 45; i++)
        {
            var start = Glory($"tinker-random-{i}");
            var hash = CanonicalJson.Sha256(start);
            var typePage = Choose(start, "choose_card_type");
            var types = Choices(typePage);
            Assert.Equal(2, types.Length);
            Assert.Equal(2, types.Distinct().Count());
            Assert.All(types, t => Assert.Contains(t,
                new[] { "attack", "skill", "power" }));
            Assert.Equal(hash, CanonicalJson.Sha256(start));
            Assert.Equal(CanonicalJson.Sha256(typePage),
                CanonicalJson.Sha256(Choose(start.Fork(),
                    "choose_card_type")));
            PrototypeStateInvariants.Validate(typePage);

            var type = types[0];
            seen.Add(type);
            var riderPage = Choose(typePage, type);
            Assert.Equal(type, riderPage.World!.Event!.NativeTinkerType);
            var riders = Choices(riderPage);
            Assert.Equal(2, riders.Length);
            Assert.Equal(2, riders.Distinct().Count());
            Assert.All(riders, rider => Assert.Contains(rider,
                Riders(type)));
            PrototypeStateInvariants.Validate(riderPage);

            var result = Choose(riderPage, riders[0]);
            Assert.Equal(RunPhase.MapChoice, result.Phase);
            Assert.Equal(start.Player.Deck.Length + 1,
                result.Player.Deck.Length);
            var card = result.Player.Deck[^1];
            Assert.Equal(Science, card.CardId);
            Assert.Equal(type, card.PersistentState.GetProperty(
                "tinkerType").GetString());
            Assert.Equal(riders[0], card.PersistentState.GetProperty(
                "tinkerRider").GetString());
            Assert.Equal(CanonicalJson.Sha256(result),
                CanonicalJson.Sha256(Choose(riderPage.Fork(), riders[0])));
            PrototypeStateInvariants.Validate(result);
        }
        Assert.True(seen.SetEquals(new[] { "attack", "skill", "power" }));
    }

    [Theory]
    [InlineData("attack", "sapping")]
    [InlineData("attack", "violence")]
    [InlineData("attack", "choking")]
    [InlineData("skill", "energized")]
    [InlineData("skill", "wisdom")]
    [InlineData("skill", "chaos")]
    [InlineData("power", "expertise")]
    [InlineData("power", "curious")]
    [InlineData("power", "improvement")]
    public void AllNineRidersPersistOnTheOriginalMadScienceModel(
        string type, string rider)
    {
        var initial = Glory($"tinker-{type}-{rider}");
        var typePage = Choose(initial, "choose_card_type");
        typePage = typePage with
        {
            World = typePage.World! with
            {
                Event = typePage.World.Event! with
                {
                    OfferedChoiceIds = [type, AnotherType(type)]
                }
            }
        };
        var riderPage = Choose(typePage, type);
        riderPage = riderPage with
        {
            World = riderPage.World! with
            {
                Event = riderPage.World.Event! with
                {
                    OfferedChoiceIds = [rider, Riders(type).First(x => x != rider)]
                }
            }
        };
        var result = Choose(riderPage, rider);
        var added = result.Player.Deck[^1];
        Assert.Equal(Science, added.CardId);
        Assert.Equal(type, added.PersistentState.GetProperty(
            "tinkerType").GetString());
        Assert.Equal(rider, added.PersistentState.GetProperty(
            "tinkerRider").GetString());
        PrototypeStateInvariants.Validate(result);
    }

    [Fact]
    public void SappingAndViolenceHaveDistinctAttackEffects()
    {
        var sapping = Combat("attack", "sapping");
        var hit = Play(sapping);
        var target = Assert.Single(hit.World!.Combat!.Enemies);
        Assert.Equal(75 - 12, target.Hp);
        Assert.Equal(2, target.Statuses["proto.status.weak"]);
        Assert.Equal(2, target.Statuses["proto.status.vulnerable"]);

        var violence = Combat("attack", "violence");
        var triple = Play(violence);
        Assert.Equal(75 - 36,
            Assert.Single(triple.World!.Combat!.Enemies).Hp);
        Assert.Equal(CanonicalJson.Sha256(triple),
            CanonicalJson.Sha256(Play(violence.Fork())));
    }

    [Fact]
    public void ChokingAppliesStrangleAfterAttack()
    {
        var state = Combat("attack", "choking");
        var first = Play(state);
        var enemy = Assert.Single(first.World!.Combat!.Enemies);
        Assert.Equal(75 - 12, enemy.Hp);
        Assert.Single(enemy.PowerStates, p =>
            p.PowerId == PrototypeNativeGloryEvents.StrangleId
            && p.Stacks == 6);
        // The same card play that creates the debuff is excluded.
        var strike = AddHandCard(first, "proto.silent.strike");
        var after = Play(strike, strike.World!.Combat!.NextCardInstanceId - 1);
        Assert.Equal(enemy.Hp - 6 - 6,
            Assert.Single(after.World!.Combat!.Enemies).Hp);
    }

    [Theory]
    [InlineData("energized")]
    [InlineData("wisdom")]
    [InlineData("chaos")]
    public void SkillRidersHaveBlockAndCorrectSecondaryEffect(
        string rider)
    {
        var state = Combat("skill", rider);
        var before = state.World!.Combat!;
        var played = Play(state);
        var after = played.World!.Combat!;
        Assert.Equal(8, after.PlayerBlock);
        if (rider == "energized")
            Assert.Equal(before.Energy + 1, after.Energy);
        else if (rider == "wisdom")
            Assert.True(after.Hand.Length >= before.Hand.Length - 1);
        else
        {
            Assert.Contains(after.Cards, c =>
                c.IsTemporary && c.TemporaryEnergyCost?.Cost == 0);
        }
        Assert.Equal(CanonicalJson.Sha256(played),
            CanonicalJson.Sha256(Play(state.Fork())));
    }

    [Theory]
    [InlineData("expertise", "proto.power.strength")]
    [InlineData("curious", PrototypeNativeGloryEvents.CuriousId)]
    [InlineData("improvement", PrototypeNativeGloryEvents.ImprovementId)]
    public void PowerRidersApplyTheirPlayerPowers(
        string rider, string expectedPower)
    {
        var state = Combat("power", rider);
        var result = Play(state);
        Assert.Contains(result.World!.Combat!.PlayerPowers,
            p => p.PowerId == expectedPower);
        if (rider == "expertise")
            Assert.Contains(result.World!.Combat!.PlayerPowers,
                p => p.PowerId == "proto.power.dexterity" && p.Stacks == 2);
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(Play(state.Fork())));
    }

    [Fact]
    public void CuriousReducesPowerEnergyCostAndImprovementUpgradesOnVictory()
    {
        var curious = Play(Combat("power", "curious"));
        var injected = AddHandCard(curious, "proto.silent.accuracy");
        var id = injected.World!.Combat!.NextCardInstanceId - 1;
        var before = injected.World.Combat.Energy;
        var after = Play(injected, id);
        Assert.Equal(before, after.World!.Combat!.Energy);

        var powered = Play(Combat("power", "improvement"));
        var combat = powered.World!.Combat!;
        var enemy = Assert.Single(combat.Enemies);
        var beforeUpgrades = powered.Player.Deck.Sum(c => c.UpgradeLevel);
        powered = powered with
        {
            World = powered.World with
            {
                Combat = combat with
                {
                    Enemies = [enemy with { Hp = 1, Block = 0 }]
                }
            }
        };
        var strike = AddHandCard(powered, "proto.silent.strike");
        var victory = Play(strike, strike.World!.Combat!.NextCardInstanceId - 1);
        Assert.Equal(RunPhase.Reward, victory.Phase);
        Assert.Equal(beforeUpgrades + 1,
            victory.Player.Deck.Sum(c => c.UpgradeLevel));
    }

    [Fact]
    public void InvalidPageActionAndCorruptedRiderCannotMutateState()
    {
        var start = Glory("tinker-invalid");
        var hash = CanonicalJson.Sha256(start);
        Assert.Throws<InvalidOperationException>(() => Choose(start, "attack"));
        Assert.Equal(hash, CanonicalJson.Sha256(start));

        var combat = Combat("attack", "sapping");
        var cardId = combat.World!.Combat!.Hand
            .Single(id => combat.World.Combat.Cards
                .Any(c => c.InstanceId == id && c.CardId == Science));
        var curr = combat.World.Combat;
        combat = combat with
        {
            World = combat.World with
            {
                Combat = curr with
                {
                    Cards = curr.Cards.Select(c =>
                        c.InstanceId == cardId
                            ? c with
                            {
                                State = JsonSerializer.SerializeToElement(new
                                {
                                    tinkerType = "attack",
                                    tinkerRider = "wisdom"
                                })
                            }
                            : c).ToArray()
                }
            }
        };
        Assert.Throws<InvalidOperationException>(() =>
            Engine.GetLegalActions(combat));
    }

    private static RunState Glory(string seed) =>
        PrototypeNativeLaterActFixture.CreateGloryEvent(seed, Tinker);

    private static RunState Combat(string type, string rider)
    {
        var state = PrototypeNativeLaterActFixture.CreateGloryEvent(
            $"science-combat-{type}-{rider}",
            PrototypeNativeGloryEvents.BattlewornDummyId);
        var id = state.World!.NextCardInstanceId;
        var science = new CardInstance(id, Science, 0,
            JsonSerializer.SerializeToElement(new
            {
                tinkerType = type, tinkerRider = rider
            }));
        state = state with
        {
            Player = state.Player with
            {
                Deck = state.Player.Deck.Append(science).ToArray()
            },
            World = state.World with { NextCardInstanceId = id + 1 }
        };
        state = Choose(state, "setting_1");
        Assert.Equal(RunPhase.Combat, state.Phase);
        var combat = state.World!.Combat!;
        var cardId = combat.Cards.Single(c =>
            c.CardId == Science).InstanceId;
        return state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    DrawPile = combat.DrawPile.Where(x => x != cardId).ToArray(),
                    DiscardPile = combat.DiscardPile.Where(x => x != cardId).ToArray(),
                    Hand = combat.Hand.Contains(cardId)
                        ? combat.Hand : combat.Hand.Append(cardId).ToArray()
                }
            }
        };
    }

    private static RunState AddHandCard(RunState state, string cardId)
    {
        var combat = state.World!.Combat!;
        var id = combat.NextCardInstanceId;
        return state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Cards = combat.Cards.Append(new CombatCardInstance(
                        id, null, cardId, 0, true,
                        PrototypeJson.EmptyObject())).ToArray(),
                    Hand = combat.Hand.Append(id).ToArray(),
                    NextCardInstanceId = id + 1
                }
            }
        };
    }

    private static RunState Play(RunState state, long? sourceId = null)
    {
        var combat = state.World!.Combat!;
        var id = sourceId ?? combat.Cards.Single(c =>
            c.CardId == Science).InstanceId;
        var card = combat.Cards.Single(c => c.InstanceId == id);
        return Engine.Step(state, GameAction.Create("play_card",
            new PlayCardPayload(id,
                card.CardId == Science && card.State.GetProperty(
                    "tinkerType").GetString() != "attack"
                    ? null : 1))).State;
    }

    private static string AnotherType(string selected) =>
        new[] { "attack", "skill", "power" }.First(x => x != selected);
    private static string[] Riders(string type) => type switch
    {
        "attack" => ["sapping", "violence", "choking"],
        "skill" => ["energized", "wisdom", "chaos"],
        "power" => ["expertise", "curious", "improvement"],
        _ => throw new InvalidOperationException()
    };
    private static string[] Choices(RunState state) =>
        Engine.GetLegalActions(state)
            .Where(a => a.Kind == "event_choice")
            .Select(a => a.ReadPayload<EventChoicePayload>().ChoiceId)
            .ToArray();
    private static RunState Choose(RunState state, string id) =>
        Engine.Step(state, GameAction.Create("event_choice",
            new EventChoicePayload(id))).State;
}
