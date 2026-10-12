using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111NonupeipeEventTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Fact]
    public void OffersThreeRelicsFromConditionalPoolWithNativeEventShuffle()
    {
        const string seed = "nonupeipe-offer-shuffle";
        var opening = PrototypeNativeLaterActFixture.Create(seed, 3);
        opening = opening with
        {
            Player = opening.Player with
            {
                Deck = EligibleSwiftDeck(4)
            }
        };
        var expected = BasePool.Append(
            NativeGloryAncientsV111.BeautifulBraceletId).ToArray();
        var stream = NativeRngV111Contexts.ForEvent(seed, "NONUPEIPE");
        var bundle = new RngBundle([stream]);
        NativeRngV111.Shuffle(bundle, stream.StreamId, expected);

        var state = NativeNonupeipeEventV111.BeginOpening(opening);

        Assert.Equal(expected.Take(3).Select(id => "take_" + id),
            state.World!.Event!.OfferedChoiceIds);
        Assert.Equal(9, NativeRngV111.ToSnapshot(
            state.World.Event.NativeEventRng!).Counter);
        Assert.Equal(NativeRngV111.ToSnapshot(bundle.Streams[0]),
            NativeRngV111.ToSnapshot(state.World.Event.NativeEventRng!));
        Assert.Equal(state.World.Event.OfferedChoiceIds,
            Engine.GetLegalActions(state).Select(action =>
                action.ReadPayload<EventChoicePayload>().ChoiceId));
        PrototypeStateInvariants.Validate(state);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(
            RunSnapshot.Load(RunSnapshot.Save(state))));
        Assert.False(ExperimentalSilentThreeActRouteV111.ContainsEvent(
            NativeNonupeipeEventV111.EventId, 3));
    }

    [Fact]
    public void BraceletIsExcludedUntilFourCardsCanReceiveSwift()
    {
        const string seed = "nonupeipe-bracelet-gate";
        var initial = PrototypeNativeLaterActFixture.Create(seed, 3);
        var opening = initial with
        {
            Player = initial.Player with
            {
                Deck = EligibleSwiftDeck(3)
            }
        };
        var expected = (string[])BasePool.Clone();
        NativeRngV111.Shuffle(
            new RngBundle([NativeRngV111Contexts.ForEvent(seed, "NONUPEIPE")]),
            "context.event.NONUPEIPE.player_0", expected);

        var state = NativeNonupeipeEventV111.BeginOpening(opening);

        Assert.DoesNotContain("take_" +
            NativeGloryAncientsV111.BeautifulBraceletId,
            state.World!.Event!.OfferedChoiceIds!);
        Assert.Equal(expected.Take(3).Select(id => "take_" + id),
            state.World.Event.OfferedChoiceIds);
        Assert.Equal(8, NativeRngV111.ToSnapshot(
            state.World.Event.NativeEventRng!).Counter);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void FurCoatMarksEightEligibleCombatsAndPersistsItsContentRng()
    {
        RunState? opening = null;
        for (var i = 0; i < 200; i++)
        {
            var seed = "nonupeipe-fur-coat-" + i;
            var candidate = PrototypeNativeLaterActFixture.Create(seed, 3);
            candidate = NativeRunRngV111.Enable(candidate with
            {
                Player = candidate.Player with { Deck = EligibleSwiftDeck(4) }
            });
            candidate = NativeNonupeipeEventV111.BeginOpening(candidate);
            if (candidate.World!.Event!.OfferedChoiceIds!.Contains(
                    "take_" + NativeGloryAncientsV111.FurCoatId,
                    StringComparer.Ordinal))
            {
                opening = candidate;
                break;
            }
        }
        var state = Assert.IsType<RunState>(opening);
        var offered = Engine.GetLegalActions(state).Single(action =>
            action.ReadPayload<EventChoicePayload>().ChoiceId
                == "take_" + NativeGloryAncientsV111.FurCoatId);

        state = Engine.Step(state, offered).State;

        Assert.Null(state.World!.Event);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        var relic = Assert.Single(state.Player.Relics, relic =>
            relic.RelicId == NativeGloryAncientsV111.FurCoatId);
        var marked = relic.PersistentState.GetProperty("MarkedNodeIds")
            .EnumerateArray().Select(value => value.GetString()!).ToArray();
        var eligible = state.World!.Map.Nodes.Where(node => node.Act == 3
            && node.RoomType is PrototypeRoomType.Combat
                or PrototypeRoomType.Elite).ToArray();
        Assert.Equal(Math.Min(8, eligible.Length), marked.Length);
        Assert.Equal(marked.Length, marked.Distinct(StringComparer.Ordinal).Count());
        Assert.All(marked, id => Assert.Contains(eligible,
            node => node.NodeId == id));
        Assert.All(marked, id => Assert.True(
            NativeGloryAncientsV111.IsFurCoatCombat(
                state.Player, 3, id)));
        Assert.False(NativeGloryAncientsV111.IsFurCoatCombat(
            state.Player, 2, marked[0]));
        Assert.Equal(eligible.Length - 1,
            relic.PersistentState.GetProperty("Rng").GetProperty("Counter")
                .GetInt32());
        PrototypeStateInvariants.Validate(state);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(
            RunSnapshot.Load(RunSnapshot.Save(state))));

        var combat = new CombatState(1, 3, 0, [], [], [], [],
        [
            Enemy(1, 63),
            Enemy(2, 47),
            Enemy(3, 0)
        ], 1, [], [], 1, Act: 3, CurrentMapNodeId: marked[0]);
        var furCoatCombat = NativeGloryAncientsV111.ApplyFurCoatCombatStart(
            combat, state.Player);
        Assert.Equal(new[] { 1, 1, 0 },
            furCoatCombat.Enemies.Select(enemy => enemy.Hp));
        Assert.Equal(new[] { 63, 47, 40 },
            furCoatCombat.Enemies.Select(enemy => enemy.MaxHp));
        var unmarked = NativeGloryAncientsV111.ApplyFurCoatCombatStart(
            combat with { CurrentMapNodeId = "not-marked" }, state.Player);
        Assert.Equal(combat.Enemies, unmarked.Enemies);
    }

    [Theory]
    [InlineData(NativeGloryAncientsV111.BlessedAntlerId)]
    [InlineData(NativeGloryAncientsV111.BeautifulBraceletId)]
    [InlineData(NativeGloryAncientsV111.BrilliantScarfId)]
    [InlineData(NativeGloryAncientsV111.DelicateFrondId)]
    [InlineData(NativeGloryAncientsV111.DiamondDiademId)]
    [InlineData(NativeGloryAncientsV111.FurCoatId)]
    [InlineData(NativeGloryAncientsV111.GlitterId)]
    [InlineData(NativeGloryAncientsV111.JewelryBoxId)]
    [InlineData(NativeGloryAncientsV111.LoomingFruitId)]
    [InlineData(NativeGloryAncientsV111.SignetRingId)]
    public void EveryNonupeipeRelicBranchCanBeTakenFromItsOffer(
        string relicId)
    {
        RunState? opening = null;
        for (var i = 0; i < 100; i++)
        {
            var seed = $"nonupeipe-branch-{relicId}-{i}";
            var candidate = PrototypeNativeLaterActFixture.Create(seed, 3);
            candidate = NativeRunRngV111.Enable(candidate with
            {
                Player = candidate.Player with
                {
                    Deck = EligibleSwiftDeck(4)
                }
            });
            candidate = NativeNonupeipeEventV111.BeginOpening(candidate);
            if (candidate.World!.Event!.OfferedChoiceIds!.Contains(
                    "take_" + relicId, StringComparer.Ordinal))
            {
                opening = candidate;
                break;
            }
        }

        var state = Assert.IsType<RunState>(opening);
        var action = Engine.GetLegalActions(state).Single(item =>
            item.ReadPayload<EventChoicePayload>().ChoiceId
                == "take_" + relicId);
        state = Engine.Step(state, action).State;

        Assert.Contains(state.Player.Relics, relic =>
            relic.RelicId == relicId);
        Assert.Null(state.World!.Event);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        PrototypeStateInvariants.Validate(state);
    }

    private static string[] BasePool =>
    [
        NativeGloryAncientsV111.BlessedAntlerId,
        NativeGloryAncientsV111.BrilliantScarfId,
        NativeGloryAncientsV111.DelicateFrondId,
        NativeGloryAncientsV111.DiamondDiademId,
        NativeGloryAncientsV111.FurCoatId,
        NativeGloryAncientsV111.GlitterId,
        NativeGloryAncientsV111.JewelryBoxId,
        NativeGloryAncientsV111.LoomingFruitId,
        NativeGloryAncientsV111.SignetRingId
    ];

    private static CardInstance[] EligibleSwiftDeck(int count) =>
        new[]
        {
            "proto.silent.strike", "proto.silent.defend",
            "proto.silent.neutralize", "proto.silent.survivor"
        }.Take(count).Select((id, index) => new CardInstance(
            index + 1, id, 0, PrototypeJson.EmptyObject())).ToArray();

    private static EnemyCombatState Enemy(int id, int hp) => new(
        InstanceId: id,
        EnemyId: "proto.enemy.ash_rat",
        Hp: hp,
        MaxHp: hp == 0 ? 40 : hp,
        Block: 0,
        MoveIndex: 0,
        Statuses: new Dictionary<string, int>());
}
