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
        Assert.Equal(nativeHand,
            initial.Hand.Select(id =>
                NativeCardToken(cards[id].CardId)).ToArray());
        var nativeToEmulatorCard = initial.Hand
            .Select((id, index) => (NativeId: index, EmulatorId: id))
            .ToDictionary(item => item.NativeId, item => item.EmulatorId);

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
            Assert.Equal(1, currentEnemy.Powers?.Count(power =>
                Token(power.PowerId) == "WEAK" && power.Stacks == 1) ?? 0);
        }
    }
}
