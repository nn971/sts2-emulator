using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksTrashHeapTests
{
    private const string EventId =
        PrototypeNativeUnderdocksTrashHeap.EventId;

    [Fact]
    public void CatalogPreservesExactSourcePoolsAndDoesNotPolluteNormalRewards()
    {
        Assert.Equal(10, PrototypeNativeUnderdocksTrashHeap.CardIds.Length);
        Assert.Equal(5, PrototypeNativeUnderdocksTrashHeap.RelicIds.Length);
        Assert.Equal(10, PrototypeNativeUnderdocksTrashHeap.CardIds
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(5, PrototypeNativeUnderdocksTrashHeap.RelicIds
            .Distinct(StringComparer.Ordinal).Count());
        foreach (var id in PrototypeNativeUnderdocksTrashHeap.CardIds)
        {
            var card = PrototypeContent.Card(id);
            Assert.Equal(PrototypeCardRarity.Event, card.Rarity);
            Assert.DoesNotContain(id, PrototypeContent.RewardCardPool);
        }
        foreach (var id in PrototypeNativeUnderdocksTrashHeap.RelicIds)
        {
            _ = PrototypeContent.Relic(id);
            if (id != "proto.relic.darkstone_periapt")
            {
                Assert.DoesNotContain(id, PrototypeContent.RelicPool);
            }
        }
        Assert.Contains(EventId,
            PrototypeNativeUnderdocksEvents.SupportedRegionEventIds);
    }

    [Fact]
    public void TrashHeapOnlySpawnsAboveFiveHpAndOffersPotentiallyLethalDive()
    {
        var evt = PrototypeContent.Event(EventId);
        var player = PrototypeNativeUnderdocksRunFactory.Create(
            "trash-eligibility").Player;
        Assert.False(PrototypeNativeUnderdocksEvents.IsEligible(
            evt, player with { Hp = 5 }));
        Assert.True(PrototypeNativeUnderdocksEvents.IsEligible(
            evt, player with { Hp = 6 }));

        var engine = new PrototypeGameEngine();
        var run = FindTrashHeap(engine);
        run = run with { Player = run.Player with { Hp = 6 } };
        var legal = engine.GetLegalActions(run)
            .Select(action => action.ReadPayload<EventChoicePayload>().ChoiceId)
            .ToArray();
        Assert.Equal(new[] { "dive", "grab" }, legal);

        var originalRng = CanonicalJson.Sha256(run.Rng);
        var dead = Choose(engine, run, "dive");
        Assert.Equal(RunPhase.Terminal, dead.Phase);
        Assert.Equal("defeat", dead.World!.TerminalOutcome);
        Assert.Equal(0, dead.Player.Hp);
        // The native event does not draw a relic if the 8 HP loss killed us.
        Assert.Equal(originalRng, CanonicalJson.Sha256(dead.Rng));
    }

    [Fact]
    public void GrabGrantsExactlyOneUniformPoolCardAndHundredGold()
    {
        var engine = new PrototypeGameEngine();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var seedIndex = 0; seedIndex < 80; seedIndex++)
        {
            var entered = FindTrashHeap(engine, "trash-grab-" + seedIndex);
            var rngPrediction = entered.Fork();
            var expectedIndex = PrototypeRng.NextInt(
                rngPrediction.Rng, "event",
                PrototypeNativeUnderdocksTrashHeap.CardIds.Length);
            var chosen = PrototypeNativeUnderdocksTrashHeap.CardIds[expectedIndex];
            var resolved = Choose(engine, entered, "grab");
            Assert.Equal(RunPhase.MapChoice, resolved.Phase);
            Assert.Equal(entered.Player.Gold + 100, resolved.Player.Gold);
            Assert.Equal(entered.Player.Hp, resolved.Player.Hp);
            Assert.Equal(entered.Player.Deck.Length + 1, resolved.Player.Deck.Length);
            Assert.Equal(chosen, resolved.Player.Deck.Last().CardId);
            Assert.Equal(CanonicalJson.Sha256(rngPrediction.Rng),
                CanonicalJson.Sha256(resolved.Rng));
            Assert.Equal(CanonicalJson.Sha256(resolved),
                CanonicalJson.Sha256(resolved.Fork()));
            seen.Add(chosen);
        }
        Assert.True(seen.Count >= 5,
            $"Only {seen.Count} native Trash Heap cards were sampled.");
    }

    [Fact]
    public void DiveTakesEightUnblockableHpThenDrawsOneOfFiveRelics()
    {
        var engine = new PrototypeGameEngine();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var seedIndex = 0; seedIndex < 80; seedIndex++)
        {
            var entered = FindTrashHeap(engine, "trash-dive-" + seedIndex);
            var rngPrediction = entered.Fork();
            var expectedIndex = PrototypeRng.NextInt(
                rngPrediction.Rng, "event",
                PrototypeNativeUnderdocksTrashHeap.RelicIds.Length);
            var chosen = PrototypeNativeUnderdocksTrashHeap.RelicIds[expectedIndex];
            var resolved = Choose(engine, entered, "dive");
            Assert.Equal(RunPhase.MapChoice, resolved.Phase);
            Assert.Equal(entered.Player.Hp - 8, resolved.Player.Hp);
            Assert.Equal(entered.Player.Gold, resolved.Player.Gold);
            Assert.Contains(resolved.Player.Relics,
                relic => relic.RelicId == chosen);
            Assert.Equal(entered.Player.Relics.Length + 1,
                resolved.Player.Relics.Length);
            Assert.Equal(CanonicalJson.Sha256(rngPrediction.Rng),
                CanonicalJson.Sha256(resolved.Rng));
            seen.Add(chosen);
        }
        Assert.Equal(5, seen.Count);
    }

    [Fact]
    public void NativeSilentItemsAreSeparateFromExplicitlyUnsupportedCrossCharacterCards()
    {
        var cards = PrototypeNativeUnderdocksTrashHeap.CardIds
            .Select(PrototypeContent.Card).ToArray();
        // The two simple Silent event cards remain executable.
        Assert.True(cards[0].MechanicsImplemented); // Caltrops
        Assert.Equal(3, cards[0].Effects.Single().Amount);
        Assert.Equal(2, cards[0].Effects.Single().UpgradeDelta);
        Assert.True(cards[6].MechanicsImplemented); // Outmaneuver
        Assert.Equal(2, cards[6].Effects.Single().Amount);
        Assert.Equal(1, cards[6].Effects.Single().UpgradeDelta);

        // Ironclad: Clash, Dual Wield, Entrench.
        // Defect: Hello World, Rebound, Rip and Tear, Stack.
        // All seven keep their exact reward identities but have no
        // executable gameplay effects until cross-character support is
        // deliberately enabled in a later development phase.
        foreach (var index in new[] { 1, 3, 4, 5, 7, 8, 9 })
        {
            Assert.False(cards[index].MechanicsImplemented);
            Assert.Empty(cards[index].Effects);
        }

        // Distraction is a native Silent card; it remains unsupported
        // for its own generated-card mechanics, not for its character.
        Assert.False(cards[2].MechanicsImplemented);
        Assert.Empty(cards[2].Effects);
    }

    private static RunState Choose(
        PrototypeGameEngine engine, RunState state, string choice)
    {
        var action = engine.GetLegalActions(state).Single(action =>
            action.Kind == "event_choice"
            && action.ReadPayload<EventChoicePayload>().ChoiceId == choice);
        return engine.Step(state, action).State;
    }

    private static RunState FindTrashHeap(
        PrototypeGameEngine engine, string prefix = "trash-seed")
    {
        for (var i = 0; i < 256; i++)
        {
            var state = StartUnderdocksEvent(engine, prefix + i);
            if (state.World!.Event!.EventId == EventId)
            {
                return state;
            }
        }
        throw new InvalidOperationException("Trash Heap was never selected.");
    }

    private static RunState StartUnderdocksEvent(
        PrototypeGameEngine engine, string seed)
    {
        var state = PrototypeNativeUnderdocksRunFactory.Create(seed);
        var node = new MapNodeState(
            "test-trash-event", 1, 1,
            PrototypeRoomType.Event, ["next-room"]);
        var next = new MapNodeState(
            "next-room", 1, 2, PrototypeRoomType.Combat, []);
        state = state with
        {
            Phase = RunPhase.MapChoice,
            World = state.World! with
            {
                Floor = 0,
                ActiveRoom = null,
                Map = new MapState(
                    [node, next], CurrentNodeId: null,
                    EntryNodeIds: [node.NodeId],
                    GenerationProfileId:
                        PrototypeNativeUnderdocks.GenerationProfileId),
                Event = null,
                Combat = null,
                Reward = null
            }
        };
        return engine.Step(
            state, Assert.Single(engine.GetLegalActions(state))).State;
    }
}
