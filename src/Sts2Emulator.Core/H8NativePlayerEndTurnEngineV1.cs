namespace Sts2Emulator.Core;

/// <summary>
/// H8's direct player end-turn transition. The session has sole ownership of
/// its combat state and version token; the input state is never mutated.
/// The actual player action and automatic effects are resolved by the same
/// deterministic game engine as v111, including reward/terminal transitions.
/// A temporary legacy continuation is published only after mechanics return,
/// for exact checkpoint/replay comparison during migration.
/// </summary>
internal static class H8NativePlayerEndTurnEngineV1
{
    internal static bool IsEndTurn(
        SemanticPilotCombatV111 current, string actionId,
        PrototypeAiFrame? playerFrame = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        if (current.UsesNativePolicyBaseline ||
            current.PendingEndTurn is not null ||
            current.PendingInitialIntents is not null ||
            !PrototypeGameEngine
                .IsAtCombatPlayerDecisionBoundary(current.Run))
            return false;

        // One source observation establishes both legal action and kind.
        // It avoids the separate v111 Step -> EndTurn double observation.
        var action = (playerFrame ??
            new PrototypeAiEnvironment().Observe(current.Run))
            .LegalActions.SingleOrDefault(item => item.ActionId == actionId);
        return action?.Kind == "end_turn";
    }

    /// <summary>Returns null when this boundary is a different legal
    /// player-action kind; only the native end-turn route is migrated.</summary>
    internal static SemanticPilotCombatV111? TryAdvance(
        SemanticPilotCombatV111 current, string actionId,
        PrototypeAiFrame? playerFrame = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (!IsEndTurn(current, actionId, playerFrame))
            return null;
        var combat = current.Run.World?.Combat ??
            throw new InvalidOperationException(
                "H8 end-turn requires active combat mechanics.");
        if (!SemanticDrawPileAdapterV111
                .IsSupportedSemanticHandDrawSourceDeck(combat))
            throw new InvalidOperationException(
                "H8 native end-turn requires a supported Nature-owned deck.");

        // Only the mechanics-owned state is forked. The source remains the
        // immutable checkpoint for the replay journal and next-turn cursor.
        RunState advanced;
        var environment = new PrototypeAiEnvironment();
        using (H8HotPathProfilerV1.Begin("h8.endturn.native_mechanics"))
        using (ControlledChanceGuardV111.Enter())
        using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
            advanced = environment.Step(
                current.Run.Fork(), actionId).State;

        using var projected = H8HotPathProfilerV1.Begin("h8.endturn.legacy_projection");
        var (continuation, result) =
            SemanticEndTurnEngineV111.ContinueFromH8PlayerEndTurn(
                current.Run, advanced,
                current.NativeEncounterEntry, actionId);
        return SemanticPilotCombatEngineV111.PublishH8PlayerEndTurn(
            current, continuation, result, actionId);
    }
}
