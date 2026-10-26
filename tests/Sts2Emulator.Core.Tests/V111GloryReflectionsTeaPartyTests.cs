using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111GloryReflectionsTeaPartyTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Fact]
    public void NewEventCataloguesAndEventOnlyContentAreGated()
    {
        var reflections = PrototypeContent.Event(
            PrototypeNativeGloryEvents.ReflectionsId);
        var party = PrototypeContent.Event(
            PrototypeNativeGloryEvents.RoundTeaPartyId);
        Assert.Equal((3, 3, 0),
            (reflections.MinAct, reflections.MaxAct, reflections.Weight));
        Assert.Equal((3, 3, 0),
            (party.MinAct, party.MaxAct, party.Weight));
        Assert.Equal(new[] { "touch_mirror", "shatter" },
            reflections.Choices.Select(x => x.Id));
        Assert.Equal(new[] { "enjoy_tea", "pick_fight" },
            party.Choices.Select(x => x.Id));
        var curse = PrototypeContent.Card(
            PrototypeNativeGloryEvents.BadLuckId);
        Assert.True(curse.Eternal);
        Assert.True(curse.Unplayable);
        Assert.Equal(13, curse.EndTurnDamageIfInHand);
        Assert.True(curse.EndTurnDamageUnblockable);
        Assert.Equal(0, curse.MaxUpgradeLevel);
        Assert.DoesNotContain(curse.Id, PrototypeContent.RewardCardPool);
        Assert.DoesNotContain(PrototypeNativeGloryEvents.RoyalPoisonId,
            PrototypeContent.RelicPool);
    }

    [Fact]
    public void ReflectionsMirrorUsesTwoDistinctDowngradesThenFourUpgrades()
    {
        var start = Glory(PrototypeNativeGloryEvents.ReflectionsId,
            "mirror-two-stage");
        var deck = start.Player.Deck;
        var upgradable = deck.Where(c =>
            PrototypeContent.Card(c.CardId).MaxUpgradeLevel >= 1)
            .ToArray();
        Assert.True(upgradable.Length >= 6);
        var upgraded = upgradable.Take(2)
            .Select(c => c.InstanceId).ToHashSet();
        var modified = deck.Select(c => upgraded.Contains(c.InstanceId)
            ? c with { UpgradeLevel = 1 } : c).ToArray();
        start = start with
        {
            Player = start.Player with { Deck = modified }
        };
        var initialHash = CanonicalJson.Sha256(start);
        var result = Choose(start, "touch_mirror");
        result = PrototypeEventPageTestHelperV111.ContinueIfFinished(result);
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Equal(deck.Length, result.Player.Deck.Length);
        Assert.Equal(4, result.Player.Deck.Count(c => c.UpgradeLevel == 1));
        Assert.Equal(result.Player.Deck.Select(c => c.InstanceId),
            modified.Select(c => c.InstanceId));
        Assert.Equal(initialHash, CanonicalJson.Sha256(start));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(
                PrototypeEventPageTestHelperV111.ContinueIfFinished(
                    Choose(start.Fork(), "touch_mirror"))));
        PrototypeStateInvariants.Validate(result);
    }

    [Fact]
    public void MirrorRecomputesCandidatesAfterDowngradeEvenIfAllCardsWereUpgraded()
    {
        var start = Glory(PrototypeNativeGloryEvents.ReflectionsId,
            "mirror-all-upgraded");
        var original = start.Player.Deck.ToArray();
        start = start with
        {
            Player = start.Player with
            {
                Deck = original.Select(c => c with
                {
                    UpgradeLevel = PrototypeContent.Card(c.CardId)
                        .MaxUpgradeLevel > 0 ? 1 : 0
                }).ToArray()
            }
        };
        var result = Choose(start, "touch_mirror");
        // Two downgraded cards are eligible again for the second
        // pass; no other already-upgraded card can be upgraded.
        Assert.Equal(start.Player.Deck.Count(c => c.UpgradeLevel > 0),
            result.Player.Deck.Count(c => c.UpgradeLevel > 0));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(Choose(start.Fork(), "touch_mirror")));
    }

    [Fact]
    public void ShatterCopiesOnlyOriginalDeckAndPreservesIdentityPayload()
    {
        var start = Glory(PrototypeNativeGloryEvents.ReflectionsId,
            "shatter-identity");
        var first = start.Player.Deck[0];
        var enchanted = first with
        {
            UpgradeLevel = 1,
            Enchantment = new PrototypeCardEnchantment(
                PrototypeCardEnchantmentKind.Spiral, 1)
        };
        start = start with
        {
            Player = start.Player with
            {
                Deck = start.Player.Deck.Select(card =>
                    card.InstanceId == first.InstanceId
                        ? enchanted : card).ToArray()
            }
        };
        var before = start.Player.Deck;
        var initialHash = CanonicalJson.Sha256(start);
        var next = start.World!.NextCardInstanceId;
        var result = Choose(start, "shatter");
        result = PrototypeEventPageTestHelperV111.ContinueIfFinished(result);
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Equal(2 * before.Length + 1, result.Player.Deck.Length);
        Assert.Equal(next + before.Length + 1,
            result.World!.NextCardInstanceId);
        Assert.Equal(2 * before.Length + 1,
            result.Player.Deck.Select(c => c.InstanceId).Distinct().Count());
        var originals = result.Player.Deck.Take(before.Length).ToArray();
        var copies = result.Player.Deck.Skip(before.Length)
            .Take(before.Length).ToArray();
        for (var index = 0; index < before.Length; index++)
        {
            Assert.Equal(before[index], originals[index]);
            Assert.Equal(before[index].CardId, copies[index].CardId);
            Assert.Equal(before[index].UpgradeLevel,
                copies[index].UpgradeLevel);
            Assert.Equal(before[index].Enchantment,
                copies[index].Enchantment);
            Assert.NotEqual(before[index].InstanceId,
                copies[index].InstanceId);
        }
        Assert.Equal(PrototypeNativeGloryEvents.BadLuckId,
            result.Player.Deck[^1].CardId);
        Assert.Equal(initialHash, CanonicalJson.Sha256(start));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(
                PrototypeEventPageTestHelperV111.ContinueIfFinished(
                    Choose(start.Fork(), "shatter"))));
        PrototypeStateInvariants.Validate(result);
    }

    [Fact]
    public void BadLuckInHandDealsThirteenUnblockableAtEndOfTurn()
    {
        var state = BattleFixture();
        var combat = state.World!.Combat!;
        var curseId = combat.NextCardInstanceId;
        var curse = new CombatCardInstance(curseId,
            null, PrototypeNativeGloryEvents.BadLuckId, 0, true,
            PrototypeJson.EmptyObject());
        var buffed = combat with
        {
            NextCardInstanceId = curseId + 1,
            Cards = combat.Cards.Append(curse).ToArray(),
            Hand = combat.Hand.Append(curseId).ToArray(),
            PlayerBlock = 100
        };
        state = state with
        {
            World = state.World with { Combat = buffed }
        };
        var control = state with
        {
            World = state.World with
            {
                Combat = buffed with
                {
                    Hand = buffed.Hand.Where(id => id != curseId).ToArray(),
                    DiscardPile = buffed.DiscardPile.Append(curseId).ToArray()
                }
            }
        };
        var withCurse = EndTurn(state);
        var withoutCurse = EndTurn(control);
        Assert.Equal(withoutCurse.Player.Hp - 13,
            withCurse.Player.Hp);
        Assert.Equal(CanonicalJson.Sha256(withCurse),
            CanonicalJson.Sha256(EndTurn(state.Fork())));
    }

    [Fact]
    public void TeaPartyEnjoyHealsToFullAndAddsRoyalPoison()
    {
        var start = Glory(PrototypeNativeGloryEvents.RoundTeaPartyId,
            "tea-enjoy");
        start = start with
        {
            Player = start.Player with
            {
                Hp = start.Player.MaxHp - 30
            }
        };
        var result = Choose(start, "enjoy_tea");
        result = PrototypeEventPageTestHelperV111.ContinueIfFinished(result);
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Equal(result.Player.MaxHp, result.Player.Hp);
        Assert.Single(result.Player.Relics, r => r.RelicId
            == PrototypeNativeGloryEvents.RoyalPoisonId);
        PrototypeStateInvariants.Validate(result);
    }

    [Fact]
    public void TeaPartyFightNeedsSecondChoiceThenDealsElevenAndGrantsOneRelic()
    {
        var start = Glory(PrototypeNativeGloryEvents.RoundTeaPartyId,
            "tea-fight");
        // Guarantee that whichever normal rarity the reward bag
        // chooses, the reward cannot itself change player HP.
        var bag = new PrototypeRelicBagState(
            ["proto.relic.lantern"], ["proto.relic.nunchaku"],
            ["proto.relic.shuriken"], []);
        start = start with
        {
            World = start.World! with { RelicBag = bag }
        };
        var hash = CanonicalJson.Sha256(start);
        var first = Choose(start, "pick_fight");
        Assert.Equal(RunPhase.Event, first.Phase);
        Assert.Equal(1, first.World!.Event!.NativePageIndex);
        Assert.Equal(start.Player.Hp, first.Player.Hp);
        Assert.Equal(start.Player.Relics.Length, first.Player.Relics.Length);
        Assert.Equal(new[] { "continue_fight" }, EventChoices(first));
        Assert.Equal(hash, CanonicalJson.Sha256(start));
        PrototypeStateInvariants.Validate(first);

        var result = Choose(first, "continue_fight");
        result = PrototypeEventPageTestHelperV111.ContinueIfFinished(result);
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Equal(start.Player.Hp - 11, result.Player.Hp);
        Assert.Single(result.Player.Relics.Except(start.Player.Relics));
        Assert.DoesNotContain(
            PrototypeNativeGloryEvents.ReflectionsId,
            result.World!.EventIds);
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(
                PrototypeEventPageTestHelperV111.ContinueIfFinished(
                    Choose(first.Fork(), "continue_fight"))));
        PrototypeStateInvariants.Validate(result);
    }

    [Fact]
    public void TeaPartyFightCanKillBeforeRelicIsGranted()
    {
        var state = Glory(PrototypeNativeGloryEvents.RoundTeaPartyId,
            "tea-fatal");
        state = Choose(state, "pick_fight");
        state = state with
        {
            Player = state.Player with { Hp = 7 }
        };
        var result = Choose(state, "continue_fight");
        Assert.Equal(RunPhase.Terminal, result.Phase);
        Assert.Equal(0, result.Player.Hp);
        Assert.Equal(state.Player.Relics.Length, result.Player.Relics.Length);
    }

    [Fact]
    public void RoyalPoisonDamagesOnlyOnOpeningPlayerTurn()
    {
        var baseline = BattleFixture();
        var equipped = BattleFixture(royalPoisonCount: 1);
        var stacked = BattleFixture(royalPoisonCount: 2);
        Assert.Equal(baseline.Player.Hp - 4, equipped.Player.Hp);
        Assert.Equal(baseline.Player.Hp - 8, stacked.Player.Hp);
        Assert.Equal(baseline.Player.Hp - EndTurn(baseline).Player.Hp,
            equipped.Player.Hp - EndTurn(equipped).Player.Hp);
        Assert.Equal(CanonicalJson.Sha256(equipped),
            CanonicalJson.Sha256(equipped.Fork()));
    }

    [Theory]
    [InlineData("incorrect")]
    [InlineData("continue_fight")]
    public void InitialGloryEventRejectsWrongChoiceWithoutMutation(
        string choiceId)
    {
        var state = Glory(PrototypeNativeGloryEvents.RoundTeaPartyId,
            "tea-invalid");
        var hash = CanonicalJson.Sha256(state);
        Assert.Throws<InvalidOperationException>(() =>
            Choose(state, choiceId));
        Assert.Equal(hash, CanonicalJson.Sha256(state));
    }

    private static RunState Glory(string eventId, string seed) =>
        PrototypeNativeLaterActFixture.CreateGloryEvent(seed, eventId);

    private static RunState Choose(RunState state, string id) =>
        Engine.Step(state, GameAction.Create("event_choice",
            new EventChoicePayload(id))).State;

    private static string[] EventChoices(RunState state) =>
        Engine.GetLegalActions(state)
            .Where(action => action.Kind == "event_choice")
            .Select(action => action.ReadPayload<EventChoicePayload>().ChoiceId)
            .ToArray();

    private static RunState EndTurn(RunState state) =>
        Engine.Step(state, Engine.GetLegalActions(state)
            .Single(a => a.Kind == "end_turn")).State;

    private static RunState BattleFixture(int royalPoisonCount = 0)
    {
        var state = Engine.Step(
            PrototypeGameFactory.Create("v111-tea-royal-poison-combat"),
            GameAction.Empty("start_run")).State;
        var relics = state.Player.Relics.Concat(
            Enumerable.Range(0, royalPoisonCount).Select(i =>
                new RelicInstance(
                    PrototypeNativeGloryEvents.RoyalPoisonId,
                    PrototypeJson.EmptyObject()))).ToArray();
        state = state with
        {
            Phase = RunPhase.Event,
            Player = state.Player with { Relics = relics },
            World = state.World! with
            {
                Act = 2, Floor = 1,
                ActiveRoom = PrototypeRoomType.Event,
                Event = new EventState(
                    PrototypeNativeHiveEvents.LanternKeyEventId),
                Map = new MapState(
                [
                    new("event", 2, 1, PrototypeRoomType.Event, ["next"]),
                    new("next", 2, 2, PrototypeRoomType.Combat, [])
                ], "event", ["event"])
            }
        };
        state = Choose(state, "keep_key");
        state = Choose(state, "fight");
        Assert.Equal(RunPhase.Combat, state.Phase);
        return state;
    }
}
