using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Independent first-combat action-prefix regression against the original
/// game's v0.111 passive source observations for Silent A10 seed
/// 6JK8MD0VQQTA (2026-11-01). Only observed mechanical values and
/// native card positions are retained; no user/run identifiers are stored.
/// The native executor callback itself is not a certified settled boundary.
/// </summary>
public sealed class NativeObservedFirstCombatActionsV111Tests
{
    private const string CapturedSeed = "6JK8MD0VQQTA";

    private sealed record ObservedAction(
        int NativeExecutorActionId,
        int NativeCardInstanceId,
        int SourceCompleteOrdinal,
        int Energy,
        int PlayerBlock,
        int EnemyHp,
        long[] HandNativeIds,
        long[] DiscardNativeIds);

    // Original-game action_completed source callback projections.
    // Action 1 Neutralize, 2 Defend, 3/4 Strike. The card-instance IDs
    // refer to the source initial hand ordered by its visible positions.
    private static readonly ObservedAction[] Observed =
    [
        new(1, 1, 86, 3, 0, 56, [0, 2, 3, 4, 5, 6], [1]),
        new(2, 5, 107, 2, 5, 56, [0, 2, 3, 4, 6], [1, 5]),
        new(3, 6, 141, 1, 5, 50, [0, 2, 3, 4], [1, 5, 6]),
        new(4, 4, 175, 0, 5, 44, [0, 2, 3], [1, 5, 6, 4])
    ];

    private static string Token(string value) => string.Concat(
        value.Split('.').Last().Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant));

    private static string NativeCardToken(string value) => Token(value) switch
    {
        "STRIKE" => "STRIKESILENT",
        "DEFEND" => "DEFENDSILENT",
        var other => other
    };

    [Fact]
    public void NativeFirstFourCardPlaysMatchIndependentEmulatorMechanics()
    {
        var configuration = RunConfiguration.Create(
            "silent", 10, ActIdentity.Overgrowth) with
        {
            FidelityId = V111Build.NativeStandardMapOpeningFidelityId
        };
        var engine = new PrototypeGameEngine();
        var state = V111RunFactory.Create(CapturedSeed, configuration);
        state = engine.Step(state, engine.GetLegalActions(state).Single()).State;
        var phial = engine.GetLegalActions(state).Single(action =>
            action.Kind == "event_choice"
            && action.Payload.GetRawText().Contains(
                "take_proto.native.neow.phialholster",
                StringComparison.Ordinal));
        state = engine.Step(state, phial).State;
        var continuation = engine.GetLegalActions(state);
        if (continuation.Count(action => action.Kind == "continue_event") == 1)
            state = engine.Step(state,
                continuation.Single(action => action.Kind == "continue_event")).State;
        var leftmost = engine.GetLegalActions(state).Single(action =>
            action.Kind == "choose_map_node"
            && action.Payload.GetRawText().Contains(
                "1:1:0", StringComparison.Ordinal));
        state = engine.Step(state, leftmost).State;

        Assert.Equal(RunPhase.Combat, state.Phase);
        var initial = Assert.IsType<CombatState>(state.World!.Combat);
        var enemy = Assert.Single(initial.Enemies);
        Assert.Equal(59, enemy.Hp);
        Assert.Equal(56, state.Player.Hp);
        Assert.Equal(3, initial.Energy);
        Assert.Equal(1, initial.Turn);
        Assert.Equal(7, initial.Hand.Length);

        // Original native first hand:
        // Strike, Neutralize, Ascender's Bane, Defend, Strike, Defend, Strike.
        var nativeHand = new[]
        {
            "STRIKE_SILENT", "NEUTRALIZE", "ASCENDERS_BANE",
            "DEFEND_SILENT", "STRIKE_SILENT", "DEFEND_SILENT",
            "STRIKE_SILENT"
        };
        var cards = initial.Cards.ToDictionary(card => card.InstanceId);
        Assert.Equal(nativeHand.Select(Token),
            initial.Hand.Select(id =>
                NativeCardToken(cards[id].CardId)));
        var nativeToEmulatorCard = initial.Hand
            .Select((id, index) => (NativeId: index, EmulatorId: id))
            .ToDictionary(item => item.NativeId, item => item.EmulatorId);
        // Native draw_pile is top-first; the emulator stores bottom-first.
        // Positional pairing is grounded in independently matching both
        // source-observed card model sequences (no assumed matching IDs).
        var nativeDraw = new[]
        {
            "STRIKE_SILENT", "STRIKE_SILENT", "SURVIVOR",
            "DEFEND_SILENT", "DEFEND_SILENT", "DEFEND_SILENT"
        };
        var emulatorDrawTopFirst = initial.DrawPile.Reverse().ToArray();
        Assert.Equal(nativeDraw.Select(Token), emulatorDrawTopFirst
            .Select(id => NativeCardToken(cards[id].CardId)));
        for (var index = 0; index < emulatorDrawTopFirst.Length; index++)
            nativeToEmulatorCard.Add(index + 7, emulatorDrawTopFirst[index]);

        foreach (var observed in Observed)
        {
            var chosen = engine.GetLegalActions(state).Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId ==
                    nativeToEmulatorCard[observed.NativeCardInstanceId]
                && action.ReadPayload<PlayCardPayload>().TargetEnemyId ==
                    (observed.NativeExecutorActionId is 1 or 3 or 4
                        ? enemy.InstanceId : null));
            state = engine.Step(state, chosen).State;
            var combat = Assert.IsType<CombatState>(state.World!.Combat);
            var currentEnemy = Assert.Single(combat.Enemies);

            Assert.True(
                state.Player.Hp == 56
                && combat.Turn == 1
                && combat.Energy == observed.Energy
                && combat.PlayerBlock == observed.PlayerBlock
                && currentEnemy.Hp == observed.EnemyHp
                && currentEnemy.Block == 0,
                $"First native action difference: executor={observed.NativeExecutorActionId}; " +
                $"source_ordinal={observed.SourceCompleteOrdinal}; " +
                $"player_hp={state.Player.Hp}; turn={combat.Turn}; " +
                $"energy={combat.Energy}; block={combat.PlayerBlock}; " +
                $"enemy_hp={currentEnemy.Hp}; enemy_block={currentEnemy.Block}");

            Assert.Equal(observed.HandNativeIds.Select(id =>
                nativeToEmulatorCard[checked((int)id)]), combat.Hand);
            Assert.Equal(observed.DiscardNativeIds.Select(id =>
                nativeToEmulatorCard[checked((int)id)]), combat.DiscardPile);
            // Source serializes Weak as a power, whereas this emulator
            // represents its temporary combat effect as proto.status.weak.
            Assert.True(currentEnemy.Statuses.TryGetValue(
                    "proto.status.weak", out var weak) && weak == 1,
                $"Native Weak(1) differs after executor {observed.NativeExecutorActionId} " +
                $"at source ordinal {observed.SourceCompleteOrdinal}; " +
                $"emulator statuses={string.Join(",", currentEnemy.Statuses.Select(
                    pair => pair.Key + ":" + pair.Value))}");
        }

        // Original-game stable decision 6 at sidecar ordinal 231 is the
        // next player-turn input boundary. The intervening source actions
        // include EndPlayerTurnAction and ReadyToBeginEnemyTurnAction.
        // One emulator end_turn must independently resolve that interval.
        state = engine.Step(state, engine.GetLegalActions(state)
            .Single(action => action.Kind == "end_turn")).State;
        var turnTwo = Assert.IsType<CombatState>(state.World!.Combat);
        var turnTwoEnemy = Assert.Single(turnTwo.Enemies);
        Assert.True(state.Player.Hp == 56 && turnTwo.Turn == 2
            && turnTwo.Energy == 3 && turnTwo.PlayerBlock == 0
            && turnTwoEnemy.Hp == 44 && turnTwoEnemy.Block == 0,
            $"Native turn-two decision 6 differs: hp={state.Player.Hp}, " +
            $"turn={turnTwo.Turn}, energy={turnTwo.Energy}, " +
            $"block={turnTwo.PlayerBlock}, enemy_hp={turnTwoEnemy.Hp}");
        Assert.False(turnTwoEnemy.Statuses.TryGetValue(
            "proto.status.weak", out var remainingWeak)
            && remainingWeak > 0);
        Assert.Equal(new long[] { 7, 8, 9, 10, 11 }
            .Select(id => nativeToEmulatorCard[checked((int)id)]),
            turnTwo.Hand);
        Assert.Equal(new long[] { 1, 5, 6, 4, 0, 3 }
            .Select(id => nativeToEmulatorCard[checked((int)id)]),
            turnTwo.DiscardPile);
        Assert.Equal(new long[] { 12 }
            .Select(id => nativeToEmulatorCard[checked((int)id)]),
            turnTwo.DrawPile.Reverse());

        // Native action_started 7 at ordinal 236 and action_completed
        // 7/8 at 262/296: two Strikes on the first turn-two enemy.
        foreach (var step in new[]
        {
            new ObservedAction(7, 7, 262, 2, 0, 38,
                [8, 9, 10, 11], [1, 5, 6, 4, 0, 3, 7]),
            new ObservedAction(8, 8, 296, 1, 0, 32,
                [9, 10, 11], [1, 5, 6, 4, 0, 3, 7, 8])
        })
        {
            var play = engine.GetLegalActions(state).Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId ==
                    nativeToEmulatorCard[step.NativeCardInstanceId]
                && action.ReadPayload<PlayCardPayload>().TargetEnemyId ==
                    enemy.InstanceId);
            state = engine.Step(state, play).State;
            var current = Assert.IsType<CombatState>(state.World!.Combat);
            var target = Assert.Single(current.Enemies);
            Assert.True(state.Player.Hp == 56
                && current.Turn == 2 && current.Energy == step.Energy
                && current.PlayerBlock == step.PlayerBlock
                && target.Hp == step.EnemyHp && target.Block == 0,
                $"Native action {step.NativeExecutorActionId} differs " +
                $"at sidecar ordinal {step.SourceCompleteOrdinal}; " +
                $"hp={state.Player.Hp}, energy={current.Energy}, " +
                $"enemy_hp={target.Hp}");
            Assert.Equal(step.HandNativeIds.Select(id =>
                nativeToEmulatorCard[checked((int)id)]), current.Hand);
            Assert.Equal(step.DiscardNativeIds.Select(id =>
                nativeToEmulatorCard[checked((int)id)]), current.DiscardPile);
        }

        // Native stable decision 9 (ordinal 414): the second end-turn
        // resolves Fuzzy Wurm's Inhale, applying Strength(7), and produces
        // a reshuffled five-card hand. This tests a second enemy turn and
        // the previously reconstructed source shuffle stream.
        state = engine.Step(state, engine.GetLegalActions(state)
            .Single(action => action.Kind == "end_turn")).State;
        var turnThree = Assert.IsType<CombatState>(state.World!.Combat);
        var turnThreeEnemy = Assert.Single(turnThree.Enemies);
        Assert.True(state.Player.Hp == 56 && turnThree.Turn == 3
            && turnThree.Energy == 3 && turnThree.PlayerBlock == 0
            && turnThreeEnemy.Hp == 32 && turnThreeEnemy.Block == 0,
            $"Native decision 9 at ordinal 414 differs: " +
            $"hp={state.Player.Hp}, turn={turnThree.Turn}, " +
            $"energy={turnThree.Energy}, block={turnThree.PlayerBlock}, " +
            $"enemy_hp={turnThreeEnemy.Hp}, enemy_block={turnThreeEnemy.Block}");
        Assert.True(turnThreeEnemy.Powers?.Any(power =>
                Token(power.PowerId) == "STRENGTH"
                && power.Stacks == 7) == true,
            $"Native Fuzzy Wurm Strength(7) differs; emulator powers=" +
            string.Join(",", turnThreeEnemy.Powers?.Select(power =>
                power.PowerId + ":" + power.Stacks) ?? []));
        Assert.Equal(new long[] { 12, 0, 1, 6, 11 }.Select(id =>
            nativeToEmulatorCard[checked((int)id)]), turnThree.Hand);
        Assert.Equal(new long[] { 8, 10, 4, 3, 9, 7, 5 }.Select(id =>
            nativeToEmulatorCard[checked((int)id)]),
            turnThree.DrawPile.Reverse());
        Assert.Empty(turnThree.DiscardPile);
        // Source recycle shuffle operation #2 (native ordinal 350):
        // ten native bounded choices advance run.shuffle from 12 to 22.
        // This verifies all four xoshiro words independently of card
        // ordering, so a matching hand alone cannot hide RNG drift.
        var shuffle = Assert.Single(state.NativeRunRng!.Streams,
            stream => stream.StreamId == "run.shuffle");
        var expectedShuffle = new NativeRngSnapshot(
            22, 11424736245114868788UL, 4415749080287556382UL,
            2605170461699982399UL, 2806277323105153668UL);
        Assert.Equal(expectedShuffle, NativeRngV111.ToSnapshot(shuffle));
        var combatShuffle = Assert.Single(turnThree.NativeRng!.RunStreams.Streams,
            stream => stream.StreamId == "run.shuffle");
        Assert.Equal(expectedShuffle, NativeRngV111.ToSnapshot(combatShuffle));
        // The combat and persistent run owners must agree across all streams.
        NativeRunRngV111.Validate(state);
        // Original source executor observations beyond strict decision 9:
        // Neutralize #11 (453), Defend #12 (471), Defend #13 (491),
        // Strike #14 (524). Their callbacks are not fresh decision boundaries.
        foreach (var step in new[]
        {
            new ObservedAction(11, 1, 453, 3, 0, 29, [12, 0, 6, 11], [1]),
            new ObservedAction(12, 11, 471, 2, 5, 29, [12, 0, 6], [1, 11]),
            new ObservedAction(13, 12, 491, 1, 10, 29, [0, 6], [1, 11, 12]),
            new ObservedAction(14, 0, 524, 0, 10, 23, [6], [1, 11, 12, 0])
        })
        {
            var play = engine.GetLegalActions(state).Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId ==
                    nativeToEmulatorCard[step.NativeCardInstanceId]
                && action.ReadPayload<PlayCardPayload>().TargetEnemyId ==
                    (step.NativeExecutorActionId is 11 or 14 ? enemy.InstanceId : null));
            state = engine.Step(state, play).State;
            var combat = Assert.IsType<CombatState>(state.World!.Combat);
            var target = Assert.Single(combat.Enemies);
            Assert.True(state.Player.Hp == 56 && combat.Turn == 3
                && combat.Energy == step.Energy
                && combat.PlayerBlock == step.PlayerBlock
                && target.Hp == step.EnemyHp,
                $"Native executor {step.NativeExecutorActionId} at {step.SourceCompleteOrdinal}: " +
                $"energy={combat.Energy}; block={combat.PlayerBlock}; enemy={target.Hp}");
            Assert.Equal(step.HandNativeIds.Select(id =>
                nativeToEmulatorCard[checked((int)id)]), combat.Hand);
            Assert.Equal(step.DiscardNativeIds.Select(id =>
                nativeToEmulatorCard[checked((int)id)]), combat.DiscardPile);
        }

        // Native executor #17 pre-action observation (ordinal 577):
        // complete turn 3 and compare the next turn's dealt hand.
        state = engine.Step(state, engine.GetLegalActions(state)
            .Single(action => action.Kind == "end_turn")).State;
        var turnFour = Assert.IsType<CombatState>(state.World!.Combat);
        var targetFour = Assert.Single(turnFour.Enemies);
        Assert.True(state.Player.Hp == 56 && turnFour.Turn == 4
            && turnFour.Energy == 3 && turnFour.PlayerBlock == 0
            && targetFour.Hp == 23 && targetFour.Block == 0,
            $"Native turn 4 executor prestep differs: hp={state.Player.Hp}, " +
            $"turn={turnFour.Turn}, energy={turnFour.Energy}, " +
            $"block={turnFour.PlayerBlock}, enemy_hp={targetFour.Hp}");
        Assert.Equal(new long[] { 8, 10, 4, 3, 9 }.Select(id =>
            nativeToEmulatorCard[checked((int)id)]), turnFour.Hand);
        Assert.Equal(new long[] { 7, 5 }.Select(id =>
            nativeToEmulatorCard[checked((int)id)]), turnFour.DrawPile.Reverse());
        Assert.Equal(new long[] { 1, 11, 12, 0, 6 }.Select(id =>
            nativeToEmulatorCard[checked((int)id)]), turnFour.DiscardPile);
        NativeRunRngV111.Validate(state);
    }
}
