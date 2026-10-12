using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111TezcataraRewardTests
{
    private static readonly PrototypeGameEngine Engine = new();

    private static RunState Acquire(string relicId)
    {
        var state = NativeAncientRewardsV111.CreateTezcataraRewardFixture(
            "tezcatara-reward-" + relicId, relicId);
        PrototypeStateInvariants.Validate(state);
        return Engine.Step(state, Assert.Single(Engine.GetLegalActions(state))).State;
    }

    private static RunState Combat(RunState state)
    {
        // Isolate reward combat semantics from unfinished Ancient assignment.
        var method = typeof(PrototypeGameEngine).GetMethod("StartCombat",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        state = state with
        {
            Player = state.Player with { Hp = 1000, MaxHp = 1000 },
            World = state.World! with
            {
                Map = new MapState([], GenerationProfileId: PrototypeNativeLaterActMap.HiveProfileId), Floor = 1,
                ActiveRoom = PrototypeRoomType.Combat, Event = null, Reward = null, Shop = null
            }
        };
        return (RunState)method.Invoke(null,
            [state, PrototypeRoomType.Combat,
                PrototypeContent.Encounter("proto.native.hive.encounter.tunneler_weak")])!;
    }

    [Fact]
    public void CocoaAddsFourOpeningEnergyPerCombatWithoutRecurring()
    {
        var acquired = Acquire(NativeAncientRewardsV111.VeryHotCocoaId);
        Assert.Equal(RunPhase.MapChoice, acquired.Phase);
        Assert.Empty(acquired.World!.CompletedRooms);
        var state = Combat(acquired);
        Assert.Equal(7, state.World!.Combat!.Energy);
        var action = Engine.GetLegalActions(state).Single(a => a.Kind == "end_turn");
        var next = Engine.Step(state, action).State;
        Assert.Equal(2, next.World!.Combat!.Turn);
        Assert.Equal(3, next.World.Combat.Energy);
        Assert.Equal(7, Combat(acquired.Fork()).World!.Combat!.Energy);
    }

    [Fact]
    public void CookieUpgradesFourDistinctChosenInstancesBeforeMapTraversal()
    {
        var pending = Acquire(NativeAncientRewardsV111.YummyCookieId);
        var original = CanonicalJson.Sha256(pending);
        Assert.Equal(4, pending.World!.Event!.PendingDeckChoice!.RemainingSelections);
        var first = Engine.GetLegalActions(pending).First();
        Assert.Equal(CanonicalJson.Sha256(Engine.Step(pending, first).State),
            CanonicalJson.Sha256(Engine.Step(RunSnapshot.Load(RunSnapshot.Save(pending)), first).State));
        var state = pending;
        for (var i = 0; i < 4; i++)
            state = Engine.Step(state, Engine.GetLegalActions(state).First()).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(4, state.Player.Deck.Count(card => card.UpgradeLevel == 1));
        Assert.Empty(state.World!.CompletedRooms);
        Assert.Null(state.World.Event);
        Assert.Equal(original, CanonicalJson.Sha256(pending));
    }

    [Fact]
    public void MittensExhaustChoicePrecedesStrengthAndResumesEachTurn()
    {
        var state = Combat(Acquire(NativeAncientRewardsV111.ToastyMittensId));
        for (var turn = 1; turn <= 3; turn++)
        {
            var combat = state.World!.Combat!;
            Assert.Equal(turn, combat.Turn);
            Assert.NotNull(combat.PendingChoice);
            Assert.Equal(turn - 1, combat.PlayerPowers
                .Where(p => p.PowerId == "proto.power.strength").Sum(p => p.Stacks));
            var options = Engine.GetLegalActions(state);
            Assert.All(options, action => Assert.Equal("select_cards", action.Kind));
            var selected = options[0].ReadPayload<SelectCardsPayload>().CardInstanceIds.Single();
            var hash = CanonicalJson.Sha256(state);
            var restored = RunSnapshot.Load(RunSnapshot.Save(state));
            var resolved = Engine.Step(state, options[0]).State;
            Assert.Equal(hash, CanonicalJson.Sha256(state));
            Assert.Equal(CanonicalJson.Sha256(resolved),
                CanonicalJson.Sha256(Engine.Step(restored, options[0]).State));
            Assert.Null(resolved.World!.Combat!.PendingChoice);
            Assert.Contains(selected, resolved.World.Combat.ExhaustPile);
            Assert.DoesNotContain(selected, resolved.World.Combat.Hand);
            Assert.Equal(turn, resolved.World.Combat.PlayerPowers
                .Where(p => p.PowerId == "proto.power.strength").Sum(p => p.Stacks));
            if (turn < 3)
                state = Engine.Step(resolved, Engine.GetLegalActions(resolved)
                    .Single(action => action.Kind == "end_turn")).State;
        }
    }

    [Fact]
    public void SealSpendsThreeGoldOnlyWhenAffordableAtEachSideStart()
    {
        var acquired = Acquire(NativeAncientRewardsV111.SealOfGoldId);
        var state = Combat(acquired with { Player = acquired.Player with { Gold = 8 } });
        Assert.Equal(5, state.Player.Gold);
        Assert.Equal(4, state.World!.Combat!.Energy);
        state = Engine.Step(state, Engine.GetLegalActions(state)
            .Single(action => action.Kind == "end_turn")).State;
        Assert.Equal(2, state.Player.Gold);
        Assert.Equal(4, state.World!.Combat!.Energy);
        state = Engine.Step(state, Engine.GetLegalActions(state)
            .Single(action => action.Kind == "end_turn")).State;
        Assert.Equal(2, state.Player.Gold);
        Assert.Equal(3, state.World!.Combat!.Energy);
    }

    [Fact]
    public void DuplicateSealsEvaluateFundsInSubscriberOrder()
    {
        var acquired = Acquire(NativeAncientRewardsV111.SealOfGoldId);
        acquired = acquired with
        {
            Player = acquired.Player with
            {
                Gold = 5,
                Relics = acquired.Player.Relics.Append(
                    new RelicInstance(NativeAncientRewardsV111.SealOfGoldId,
                        PrototypeJson.EmptyObject())).ToArray()
            }
        };
        var state = Combat(acquired);
        Assert.Equal(2, state.Player.Gold);
        Assert.Equal(4, state.World!.Combat!.Energy);
    }

    [Fact]
    public void PumpkinChargesLastFiveCombatsAndKindleAddsFiveWithoutHealing()
    {
        var state = Acquire(NativeAncientRewardsV111.PumpkinCandleId);
        Assert.Equal(5, NativeAncientRewardsV111.KindleCount(
            state.Player.Relics.Single(relic => relic.RelicId == NativeAncientRewardsV111.PumpkinCandleId)));
        state = state with { Player = state.Player with { Deck = state.Player.Deck.Take(1).ToArray() } };
        for (var count = 5; count >= 0; count--)
        {
            var battle = Combat(state);
            Assert.Equal(count > 0 ? 4 : 3, battle.World!.Combat!.Energy);
            battle = battle with
            {
                World = battle.World with
                {
                    Combat = battle.World.Combat with
                    {
                        Enemies = battle.World.Combat.Enemies.Select(enemy => enemy with { Hp = 1, Block = 0 }).ToArray()
                    }
                }
            };
            var hash = CanonicalJson.Sha256(battle);
            var attack = Engine.GetLegalActions(battle).First(action => action.Kind == "play_card");
            state = Engine.Step(battle, attack).State;
            Assert.Equal(RunPhase.Reward, state.Phase);
            Assert.Equal(Math.Max(0, count - 1), NativeAncientRewardsV111.KindleCount(
                state.Player.Relics.Single(relic => relic.RelicId == NativeAncientRewardsV111.PumpkinCandleId)));
            Assert.Equal(hash, CanonicalJson.Sha256(battle));
            Assert.Equal(CanonicalJson.Sha256(state),
                CanonicalJson.Sha256(Engine.Step(RunSnapshot.Load(RunSnapshot.Save(battle)), attack).State));
        }

        var rest = Acquire(NativeAncientRewardsV111.PumpkinCandleId);
        var node = rest.World!.Map.Nodes.First(node => node.RoomType == PrototypeRoomType.Rest);
        rest = rest with
        {
            Player = rest.Player with { Hp = 30 },
            World = rest.World with
            {
                Floor = node.Floor, ActiveRoom = PrototypeRoomType.Rest,
                Map = rest.World.Map with { CurrentNodeId = node.NodeId }
            },
            Phase = RunPhase.Rest
        };
        var rested = Engine.Step(rest, Engine.GetLegalActions(rest)
            .Single(action => action.Kind == "rest_kindle")).State;
        Assert.Equal(RunPhase.MapChoice, rested.Phase);
        Assert.Equal(30, rested.Player.Hp);
        Assert.Equal(10, NativeAncientRewardsV111.KindleCount(
            rested.Player.Relics.Single(relic => relic.RelicId == NativeAncientRewardsV111.PumpkinCandleId)));
        Assert.Equal(CanonicalJson.Sha256(rested),
            CanonicalJson.Sha256(Engine.Step(RunSnapshot.Load(RunSnapshot.Save(rest)),
                GameAction.Empty("rest_kindle")).State));
    }

    [Fact]
    public void CandleConsumesOneChargeOnDefeatAndRejectsMalformedState()
    {
        var state = Combat(Acquire(NativeAncientRewardsV111.PumpkinCandleId));
        state = state with { Player = state.Player with { Hp = 1 } };
        for (var i = 0; i < 4 && state.Phase != RunPhase.Terminal; i++)
            state = Engine.Step(state, Engine.GetLegalActions(state).Single(action => action.Kind == "end_turn")).State;
        Assert.Equal(RunPhase.Terminal, state.Phase);
        Assert.Equal(4, NativeAncientRewardsV111.KindleCount(
            state.Player.Relics.Single(relic => relic.RelicId == NativeAncientRewardsV111.PumpkinCandleId)));
        var malformed = new RelicInstance(NativeAncientRewardsV111.PumpkinCandleId,
            System.Text.Json.JsonSerializer.SerializeToElement(new { KindleCount = -1 }));
        Assert.Throws<InvalidOperationException>(() => NativeAncientRewardsV111.KindleCount(malformed));
        Assert.Throws<InvalidOperationException>(() => PrototypeStateInvariants.Validate(state with
        {
            Player = state.Player with { Relics = [malformed] }
        }));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 3)]
    public void StorybookCardGainsEnergyDrawsThenLosesMaxHp(int upgrade, int gain)
    {
        var acquired = Acquire(NativeAncientRewardsV111.StorybookId);
        var flame = Assert.Single(acquired.Player.Deck, card => card.CardId == NativeAncientRewardsV111.BrightestFlameId);
        Assert.False(PrototypeContent.Card(flame.CardId).ExhaustOnUse);
        acquired = acquired with
        {
            Player = acquired.Player with
            {
                Deck = acquired.Player.Deck.Select(card => card.InstanceId == flame.InstanceId
                    ? card with { UpgradeLevel = upgrade } : card).ToArray()
            }
        };
        var state = Combat(acquired);
        var combat = state.World!.Combat!;
        var instance = combat.Cards.Single(card => card.CardId == flame.CardId);
        // Locate the physical acquired card in hand to isolate its effect sequence.
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Hand = combat.Hand.Contains(instance.InstanceId) ? combat.Hand : combat.Hand.Append(instance.InstanceId).ToArray(),
                    DrawPile = combat.DrawPile.Where(id => id != instance.InstanceId).ToArray()
                }
            }
        };
        var action = Engine.GetLegalActions(state).Single(action => action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId == instance.InstanceId);
        var result = Engine.Step(state, action).State;
        Assert.Equal(combat.Energy + gain, result.World!.Combat!.Energy);
        Assert.Equal(combat.CounterState.CardsDrawnThisCombat + gain,
            result.World.Combat.CounterState.CardsDrawnThisCombat);
        Assert.Equal(998, result.Player.MaxHp);
        Assert.Equal(998, result.Player.Hp);
        Assert.Contains(instance.InstanceId, result.World.Combat.DiscardPile);
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
    }

    [Fact]
    public void HugRemovesFourDistinctDeckInstancesThroughAcquisitionChoices()
    {
        var state = Acquire(NativeAncientRewardsV111.BiiigHugId);
        var deckSize = state.Player.Deck.Length;
        Assert.Equal(4, state.World!.Event!.PendingDeckChoice!.RemainingSelections);
        for (var index = 0; index < 4; index++)
        {
            var action = Engine.GetLegalActions(state).First();
            var next = Engine.Step(state, action).State;
            Assert.Equal(CanonicalJson.Sha256(next), CanonicalJson.Sha256(
                Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
            state = next;
        }
        Assert.Equal(deckSize - 4, state.Player.Deck.Length);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Empty(state.World!.CompletedRooms);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HugAddsOnePhysicalSootBeforeResumableShuffleDraw(bool stratagem)
    {
        var acquired = Acquire(NativeAncientRewardsV111.VeryHotCocoaId);
        acquired = acquired with
        {
            Player = acquired.Player with
            {
                Relics = [new(NativeAncientRewardsV111.BiiigHugId, PrototypeJson.EmptyObject())],
                Deck = [new(1, "proto.colorless.finesse", 0, PrototypeJson.EmptyObject()),
                    new(2, "proto.silent.strike", 0, PrototypeJson.EmptyObject()),
                    new(3, "proto.silent.defend", 0, PrototypeJson.EmptyObject())]
            }
        };
        var state = Combat(acquired);
        var combat = state.World!.Combat!;
        Assert.DoesNotContain(combat.Cards, card => card.CardId == NativeAncientRewardsV111.SootId);
        var finesse = combat.Cards.Single(card => card.CardId == "proto.colorless.finesse").InstanceId;
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Hand = [finesse], DrawPile = [],
                    DiscardPile = combat.Cards.Where(card => card.InstanceId != finesse)
                        .Select(card => card.InstanceId).ToArray(),
                    PlayerPowers = stratagem
                        ? [new("proto.power.stratagem", 1, combat.NextPowerApplicationOrder)] : [],
                    NextPowerApplicationOrder = combat.NextPowerApplicationOrder + (stratagem ? 1 : 0)
                }
            }
        };
        var action = Engine.GetLegalActions(state).Single(action => action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId == finesse);
        var parentHash = CanonicalJson.Sha256(state);
        var result = Engine.Step(state, action).State;
        Assert.Equal(parentHash, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(result), CanonicalJson.Sha256(
            Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
        var soot = Assert.Single(result.World!.Combat!.Cards,
            card => card.CardId == NativeAncientRewardsV111.SootId);
        Assert.True(soot.IsTemporary);
        Assert.Null(soot.PersistentCardInstanceId);
        Assert.True(PrototypeContent.Card(soot.CardId).Unplayable);
        if (stratagem)
        {
            Assert.Contains(soot.InstanceId, result.World.Combat.PendingChoice!.CandidateCardInstanceIds);
            var choose = Engine.GetLegalActions(result).Single(action => action.Kind == "select_cards"
                && action.ReadPayload<SelectCardsPayload>().CardInstanceIds.SequenceEqual([soot.InstanceId]));
            var resumed = Engine.Step(result, choose).State;
            Assert.Equal(CanonicalJson.Sha256(resumed), CanonicalJson.Sha256(
                Engine.Step(RunSnapshot.Load(RunSnapshot.Save(result)), choose).State));
            Assert.Single(resumed.World!.Combat!.Cards, card => card.CardId == soot.CardId);
            Assert.Contains(soot.InstanceId, resumed.World.Combat.Hand);
            Assert.Null(resumed.World.Combat.PendingChoice);
            PrototypeStateInvariants.Validate(resumed);
        }
        else
            PrototypeStateInvariants.Validate(result);
    }

    [Fact]
    public void StorybookMaxHpLossWaitsForNestedDrawChoice()
    {
        var state = Combat(Acquire(NativeAncientRewardsV111.StorybookId));
        var combat = state.World!.Combat!;
        var flame = combat.Cards.Single(card => card.CardId == NativeAncientRewardsV111.BrightestFlameId);
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Hand = [flame.InstanceId], DrawPile = [],
                    DiscardPile = combat.Cards.Where(card => card.InstanceId != flame.InstanceId)
                        .Select(card => card.InstanceId).ToArray(),
                    PlayerPowers = [new("proto.power.stratagem", 1, combat.NextPowerApplicationOrder)],
                    NextPowerApplicationOrder = combat.NextPowerApplicationOrder + 1
                }
            }
        };
        var action = Engine.GetLegalActions(state).Single(action => action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId == flame.InstanceId);
        var pending = Engine.Step(state, action).State;
        Assert.NotNull(pending.World!.Combat!.PendingChoice);
        Assert.Equal(1000, pending.Player.MaxHp);
        Assert.Equal(combat.Energy + 2, pending.World.Combat.Energy);
        var choice = Engine.GetLegalActions(pending).First();
        var resolved = Engine.Step(pending, choice).State;
        Assert.Equal(998, resolved.Player.MaxHp);
        Assert.Equal(998, resolved.Player.Hp);
        Assert.Null(resolved.World!.Combat!.PendingChoice);
        Assert.Contains(flame.InstanceId, resolved.World.Combat.DiscardPile);
        Assert.Equal(CanonicalJson.Sha256(resolved), CanonicalJson.Sha256(
            Engine.Step(RunSnapshot.Load(RunSnapshot.Save(pending)), choice).State));
        Assert.Equal(1000, pending.Player.MaxHp);
    }

    [Fact]
    public void SoupEnchantsOnlyEligibleExistingBasicStrikesWithoutReplacingInstances()
    {
        var fixture = NativeAncientRewardsV111.CreateTezcataraRewardFixture(
            "soup-enchantment-eligibility", NativeAncientRewardsV111.NutritiousSoupId);
        var firstStrike = fixture.Player.Deck.First(card => card.CardId == "proto.silent.strike");
        fixture = fixture with
        {
            Player = fixture.Player with
            {
                Deck = fixture.Player.Deck.Select(card => card.InstanceId == firstStrike.InstanceId
                    ? card with { Enchantment = new(PrototypeCardEnchantmentKind.Nimble) }
                    : card).ToArray()
            }
        };
        var hash = CanonicalJson.Sha256(fixture);
        var action = Assert.Single(Engine.GetLegalActions(fixture));
        var acquired = Engine.Step(fixture, action).State;
        Assert.Equal(hash, CanonicalJson.Sha256(fixture));
        Assert.Equal(CanonicalJson.Sha256(acquired), CanonicalJson.Sha256(
            Engine.Step(RunSnapshot.Load(RunSnapshot.Save(fixture)), action).State));
        Assert.Equal(fixture.Player.Deck.Select(card => card.InstanceId),
            acquired.Player.Deck.Select(card => card.InstanceId));
        Assert.Equal(4, acquired.Player.Deck.Count(card =>
            card.Enchantment?.Kind == PrototypeCardEnchantmentKind.TezcatarasEmber));
        Assert.Equal(PrototypeCardEnchantmentKind.Nimble,
            acquired.Player.Deck.Single(card => card.InstanceId == firstStrike.InstanceId).Enchantment!.Kind);
        Assert.All(acquired.Player.Deck.Where(card => card.CardId != "proto.silent.strike"),
            card => Assert.Null(card.Enchantment));
    }

    [Theory]
    [InlineData(0, 9)]
    [InlineData(1, 12)]
    public void EmberStrikeIsFreeAndAddsDamageBeforeWeakness(int upgrade, int damage)
    {
        var acquired = Acquire(NativeAncientRewardsV111.NutritiousSoupId);
        acquired = acquired with { Player = acquired.Player with
        {
            Deck = acquired.Player.Deck.Select(card => card.CardId == "proto.silent.strike"
                ? card with { UpgradeLevel = upgrade } : card).ToArray()
        }};
        var state = Combat(acquired);
        var combat = state.World!.Combat!;
        var strike = combat.Cards.First(card => card.CardId == "proto.silent.strike");
        var enemy = combat.Enemies.First();
        state = state with { World = state.World with { Combat = combat with
        {
            Hand = [strike.InstanceId],
            DrawPile = combat.Cards.Where(card => card.InstanceId != strike.InstanceId)
                .Select(card => card.InstanceId).ToArray(), Energy = 0,
            PlayerPowers = [new("proto.power.weak", 1, combat.NextPowerApplicationOrder)],
            NextPowerApplicationOrder = combat.NextPowerApplicationOrder + 1,
            Enemies = combat.Enemies.Select(item => item.InstanceId == enemy.InstanceId
                ? item with { Hp = 100, MaxHp = 100, Block = 0 } : item).ToArray()
        }}};
        var action = Engine.GetLegalActions(state).First(action => action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId == strike.InstanceId);
        var result = Engine.Step(state, action).State;
        Assert.Equal(0, result.World!.Combat!.Energy);
        Assert.Equal(100 - damage * 3 / 4, result.World.Combat.Enemies.First().Hp);
        Assert.True(PrototypeCardRules.IsEternal(strike));
        Assert.Equal(CanonicalJson.Sha256(result), CanonicalJson.Sha256(
            Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
    }

    [Fact]
    public void EmberEternalProtectsRemovalAndTransformButAllowsUpgrade()
    {
        var acquired = Acquire(NativeAncientRewardsV111.NutritiousSoupId);
        foreach (var relicId in new[] { NativeAncientRewardsV111.BiiigHugId, NativeAncientRewardsV111.YummyCookieId })
        {
            var fixture = NativeAncientRewardsV111.CreateTezcataraRewardFixture("soup-then-" + relicId, relicId);
            fixture = fixture with { Player = acquired.Player };
            var pending = Engine.Step(fixture, Assert.Single(Engine.GetLegalActions(fixture))).State;
            var candidates = pending.World!.Event!.PendingDeckChoice!.CandidateCardInstanceIds;
            var strikes = acquired.Player.Deck.Where(PrototypeCardRules.IsEternal).Select(card => card.InstanceId);
            if (relicId == NativeAncientRewardsV111.BiiigHugId)
                Assert.DoesNotContain(candidates, id => strikes.Contains(id));
            else
                Assert.All(strikes, id => Assert.Contains(id, candidates));
            PrototypeStateInvariants.Validate(pending);
        }
        var method = typeof(PrototypeGameEngine).GetMethod("CanSelectEventDeckCard",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var card = acquired.Player.Deck.First(PrototypeCardRules.IsEternal);
        Assert.False((bool)method.Invoke(null,
            [card, PrototypePersistentDeckChoiceKind.Transform, null, null, false])!);
    }

    [Fact]
    public void OpeningMittensWaitsForEarlierCombatStartChoiceEvenWhenOwnedFirst()
    {
        var acquired = Acquire(NativeAncientRewardsV111.ToastyMittensId);
        acquired = acquired with { Player = acquired.Player with { Relics =
        [
            acquired.Player.Relics.Single(relic => relic.RelicId == NativeAncientRewardsV111.ToastyMittensId),
            new("proto.relic.toolbox", PrototypeJson.EmptyObject())
        ] }};
        var state = Combat(acquired);
        var pending = state.World!.Combat!.PendingChoice!;
        Assert.Equal(PrototypeCardZone.ChoicePool, pending.Selection.SourceZone);
        Assert.Empty(state.World.Combat.Hand);
        Assert.Equal(PrototypeOpeningCombatStage.DrawHand,
            state.World.Combat.AutomaticPipelineContinuation!.OpeningStage);
        Assert.DoesNotContain(state.World.Combat.PlayerPowers, power => power.PowerId == "proto.power.strength");
        var action = Engine.GetLegalActions(state).First(action => action.Kind == "select_cards"
            && action.ReadPayload<SelectCardsPayload>().CardInstanceIds.Length == 1);
        var selected = action.ReadPayload<SelectCardsPayload>().CardInstanceIds.Single();
        var result = Engine.Step(state, action).State;
        Assert.Equal(CanonicalJson.Sha256(result), CanonicalJson.Sha256(
            Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
        Assert.Equal(PrototypeCardZone.Hand, result.World!.Combat!.PendingChoice!.Selection.SourceZone);
        Assert.Equal(6, result.World.Combat.Hand.Length);
        Assert.Contains(selected, result.World.Combat.PendingChoice.CandidateCardInstanceIds);
        Assert.DoesNotContain(result.World.Combat.PlayerPowers, power => power.PowerId == "proto.power.strength");
        var exhaust = Engine.GetLegalActions(result).First();
        var completed = Engine.Step(result, exhaust).State;
        Assert.Null(completed.World!.Combat!.PendingChoice);
        Assert.Null(completed.World.Combat.AutomaticPipelineContinuation);
        Assert.Equal(1, completed.World.Combat.PlayerPowers.Single(power => power.PowerId == "proto.power.strength").Stacks);
        PrototypeStateInvariants.Validate(completed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MittensAndGamblingChipShareOpeningHookInOwnershipOrder(bool chipFirst)
    {
        var acquired = Acquire(NativeAncientRewardsV111.ToastyMittensId);
        var mittens = acquired.Player.Relics.Single(relic => relic.RelicId == NativeAncientRewardsV111.ToastyMittensId);
        var chip = new RelicInstance("proto.relic.gambling_chip", PrototypeJson.EmptyObject());
        acquired = acquired with { Player = acquired.Player with { Relics = chipFirst ? [chip, mittens] : [mittens, chip] } };
        var state = Combat(acquired);
        for (var index = 0; index < 2; index++)
        {
            var isChip = chipFirst == (index == 0);
            Assert.Equal(isChip, state.World!.Combat!.PendingChoice!.Selection.SequentialOptional);
            var action = Engine.GetLegalActions(state).First(action => action.Kind == "select_cards"
                && action.ReadPayload<SelectCardsPayload>().CardInstanceIds.Length == (isChip ? 0 : 1));
            var resolved = Engine.Step(state, action).State;
            Assert.Equal(CanonicalJson.Sha256(resolved), CanonicalJson.Sha256(
                Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
            state = resolved;
        }
        Assert.Null(state.World!.Combat!.PendingChoice);
        Assert.Null(state.World.Combat.AutomaticPipelineContinuation);
        Assert.Equal(1, state.World.Combat.PlayerPowers.Single(power => power.PowerId == "proto.power.strength").Stacks);
        state = Engine.Step(state, Engine.GetLegalActions(state).Single(action => action.Kind == "end_turn")).State;
        Assert.Equal(2, state.World!.Combat!.Turn);
        Assert.False(state.World.Combat.PendingChoice!.Selection.SequentialOptional);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void UnsupportedRewardCannotBecomeATruncatedRandomAncientOffer()
    {
        Assert.Throws<NotSupportedException>(() =>
            NativeAncientRewardsV111.CreateTezcataraRewardFixture("tezcatara-unknown", "toy_box"));
        Assert.DoesNotContain(NativeAncientRewardsV111.TezcataraId,
            ExperimentalSilentThreeActRouteV111.HiveEvents);
    }
}
