using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Independent native-source turn-boundary regression from a second actual
/// v111 Silent A10 fight on seed 6JK8MD0VQQTA (2026-11-01).
/// This native play sequence differs from the earlier golden test.
/// Source turn checkpoints are provisional: matching projected mechanics
/// does not certify full queue settlement, native RNG-call coverage, or a
/// live in-process parity inspector.
/// </summary>
public sealed class NativeObservedTurnCheckpointsV111Tests
{
    private const string Seed = "6JK8MD0VQQTA";
    private sealed record SourcePlayerStart(
        int Turn, int NativeOrdinal, int EnemyHp, int Strength,
        long[] Hand, long[] DrawTopFirst, long[] Discard);

    private static readonly SourcePlayerStart[] PlayerStarts =
    [
        new(1, 47, 59, 0,
            [0, 1, 2, 3, 4, 5, 6], [7, 8, 9, 10, 11, 12], []),
        new(2, 224, 44, 0,
            [7, 8, 9, 10, 11], [12], [1, 3, 0, 4, 5, 6]),
        new(3, 404, 32, 7,
            [12, 6, 1, 0, 11], [8, 10, 4, 5, 9, 7, 3], []),
        new(4, 561, 23, 7,
            [8, 10, 4, 5, 9], [7, 3], [1, 11, 12, 6, 0]),
        new(5, 750, 17, 7,
            [7, 3, 11, 12, 5], [8, 1, 9, 6, 0, 10, 4], []),
        new(6, 835, 11, 14,
            [8, 1, 9, 6, 0], [10, 4], [7, 3, 11, 12, 5]),
    ];

    private static string Token(string value) => string.Concat(
        value.Split('.').Last().Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant));

    private static string NativeCardToken(string value) =>
        Token(value) switch
        {
            "STRIKE" => "STRIKESILENT",
            "DEFEND" => "DEFENDSILENT",
            var valueToken => valueToken
        };

    [Fact]
    public void DistinctNativeActionsMatchAllSixObservedPlayerTurnStarts()
    {
        var configuration = RunConfiguration.Create(
            "silent", 10, ActIdentity.Overgrowth) with
        {
            FidelityId = V111Build.NativeStandardMapOpeningFidelityId
        };
        var engine = new PrototypeGameEngine();
        var state = V111RunFactory.Create(Seed, configuration);
        state = engine.Step(state, engine.GetLegalActions(state).Single()).State;
        var phial = engine.GetLegalActions(state).Single(action =>
            action.Kind == "event_choice"
            && action.Payload.GetRawText().Contains(
                "take_proto.native.neow.phialholster",
                StringComparison.Ordinal));
        state = engine.Step(state, phial).State;
        var continuation = engine.GetLegalActions(state);
        if (continuation.Count(action => action.Kind == "continue_event") == 1)
            state = engine.Step(state, continuation.Single(action =>
                action.Kind == "continue_event")).State;
        var leftmost = engine.GetLegalActions(state).Single(action =>
            action.Kind == "choose_map_node"
            && action.Payload.GetRawText().Contains(
                "1:1:0", StringComparison.Ordinal));
        state = engine.Step(state, leftmost).State;
        Assert.Equal(RunPhase.Combat, state.Phase);

        var initial = Assert.IsType<CombatState>(state.World!.Combat);
        var originalHand = new[]
        {
            "STRIKE_SILENT", "NEUTRALIZE", "ASCENDERS_BANE",
            "DEFEND_SILENT", "STRIKE_SILENT", "DEFEND_SILENT",
            "STRIKE_SILENT"
        };
        var originalDraw = new[]
        {
            "STRIKE_SILENT", "STRIKE_SILENT", "SURVIVOR",
            "DEFEND_SILENT", "DEFEND_SILENT", "DEFEND_SILENT"
        };
        var cards = initial.Cards.ToDictionary(card => card.InstanceId);
        Assert.Equal(originalHand.Select(Token), initial.Hand.Select(id =>
            NativeCardToken(cards[id].CardId)));
        var emuDrawTopFirst = initial.DrawPile.Reverse().ToArray();
        Assert.Equal(originalDraw.Select(Token), emuDrawTopFirst.Select(id =>
            NativeCardToken(cards[id].CardId)));

        var cardIds = initial.Hand.Select((id, i) =>
                (Native: i, Emulator: id))
            .ToDictionary(pair => pair.Native, pair => pair.Emulator);
        for (int i = 0; i < emuDrawTopFirst.Length; i++)
            cardIds.Add(i + 7, emuDrawTopFirst[i]);
        var enemyId = Assert.Single(initial.Enemies).InstanceId;

        void AssertPlayerStart(int index)
        {
            var expected = PlayerStarts[index];
            var combat = Assert.IsType<CombatState>(state.World!.Combat);
            var target = Assert.Single(combat.Enemies);
            Assert.True(state.Player.Hp == 56 && combat.Turn == expected.Turn
                && combat.Energy == 3 && combat.PlayerBlock == 0
                && target.Hp == expected.EnemyHp && target.Block == 0,
                $"Source player start {expected.Turn} at ordinal {expected.NativeOrdinal}: "
                + $"hp={state.Player.Hp}, energy={combat.Energy}, "
                + $"block={combat.PlayerBlock}, enemy={target.Hp}");
            Assert.Equal(expected.Hand.Select(id =>
                cardIds[checked((int)id)]), combat.Hand);
            Assert.Equal(expected.DrawTopFirst.Select(id =>
                cardIds[checked((int)id)]), combat.DrawPile.Reverse());
            Assert.Equal(expected.Discard.Select(id =>
                cardIds[checked((int)id)]), combat.DiscardPile);
            Assert.Equal(new long[] { 2 }.Take(expected.Turn == 1 ? 0 : 1)
                .Select(id => cardIds[checked((int)id)]),
                combat.ExhaustPile);
            var nativeStrength = target.Powers?
                .Where(power => Token(power.PowerId) == "STRENGTH")
                .Sum(power => power.Stacks) ?? 0;
            Assert.Equal(expected.Strength, nativeStrength);
            NativeRunRngV111.Validate(state);
        }
        void Play(int nativeCardId, bool target)
        {
            var action = engine.GetLegalActions(state).Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId ==
                    cardIds[nativeCardId]
                && action.ReadPayload<PlayCardPayload>().TargetEnemyId ==
                    (target ? enemyId : null));
            state = engine.Step(state, action).State;
        }
        void EndTurn()
        {
            state = engine.Step(state, engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;
        }

        AssertPlayerStart(0);
        Play(1, true);  // Neutralize
        Play(3, false); // Defend
        Play(0, true);  // Strike
        Play(4, true);  // Strike
        EndTurn();

        AssertPlayerStart(1);
        Play(7, true);
        Play(8, true);
        EndTurn();

        AssertPlayerStart(2);
        Play(1, true);
        Play(11, false);
        Play(12, false);
        Play(6, true);
        EndTurn();

        AssertPlayerStart(3);
        Play(9, false); // Survivor, native chooses Defend #10 for discard
        var choice = engine.GetLegalActions(state).Single(action =>
            action.Kind == "select_cards"
            && action.ReadPayload<SelectCardsPayload>().CardInstanceIds
                .SequenceEqual(new long[] { cardIds[10] }));
        state = engine.Step(state, choice).State;
        Play(5, false);
        Play(8, true);
        EndTurn();

        AssertPlayerStart(4);
        var shuffle = Assert.Single(state.NativeRunRng!.Streams,
            stream => stream.StreamId == "run.shuffle");
        Assert.Equal(new NativeRngSnapshot(
            31, 4498395649810740633UL, 12157284108367839612UL,
            332782078443390178UL, 1557917576211562617UL),
            NativeRngV111.ToSnapshot(shuffle));
        Play(7, true);
        EndTurn();

        AssertPlayerStart(5);
        Play(0, true);
        Play(8, true);
        Assert.Equal(56, state.Player.Hp);
        Assert.Equal(RunPhase.Reward, state.Phase);
        Assert.Null(state.World!.Combat);
        Assert.NotNull(state.World.Reward);
        NativeRunRngV111.Validate(state);
    }
}
