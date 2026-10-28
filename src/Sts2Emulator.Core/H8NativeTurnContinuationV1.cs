namespace Sts2Emulator.Core;

/// <summary>
/// H8-owned execution of the ordinary automatic physical hand-draw continuation.
/// The engine takes an already committed player/Nature draw effect and advances
/// the real source mechanics through the remainder of the turn before emitting
/// the v111 compatibility projection.
///
/// An H8 cursor holds only the current mechanics, hand draw, and terminal/next
/// boundary status. It never owns the legacy pilot history, stochastic choices,
/// or a copied end-turn stack. The enclosing H8 session owns the authoritative
/// versioned player/Nature decision journal.
///
/// The final v111 projection remains necessary for H8 mechanics not yet migrated.
/// A later owner can retain this cursor over multiple decisions and project
/// only for explicit audits; all actual automatic turn advancement here is
/// already independent of SemanticEndTurnEngineV111.
/// </summary>
internal static class H8NativeTurnContinuationV1
{
    private readonly record struct Cursor(
        RunState ActiveRun, SemanticCombatDrawV111 HandDraw,
        bool Complete, bool SuspendedNestedChoice);

    internal static SemanticEndTurnV111 AdvancePhysicalHandDraw(
        SemanticEndTurnV111 origin,
        SemanticCombatDrawV111 advancedDraw)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(advancedDraw);
        if (origin.Complete ||
            origin.HandDraw is not
                { AtBoundary: false, Pending: not null,
                  AutomaticHandDraw: true, FromHandDraw: true } ||
            !advancedDraw.AutomaticHandDraw ||
            !advancedDraw.FromHandDraw)
            throw new InvalidOperationException(
                "H8 turn cursor needs an active automatic physical hand draw.");

        using var total = H8HotPathProfilerV1.Begin("h8.turn_cursor.total");
        var cursor = new Cursor(advancedDraw.Run, advancedDraw, false, false);
        if (advancedDraw.AtBoundary)
            cursor = ResumeActualTurn(cursor);

        // The output projection preserves the public v111 audit shape and
        // provider history, but it performs no automatic turn mechanics.
        using var projection =
            H8HotPathProfilerV1.Begin("h8.turn_cursor.compat_projection");
        // First publish the hand progress to the compatibility record,
        // then attach any resumed pipeline result. These are distinct source
        // checkpoints: the HandDraw remains at its committed physical draw,
        // while EndTurn.Run advances to the following Nature/player boundary.
        // Preserve the exact v111 write order for nested Simulation envelopes.
        var projected = origin with
        {
            Run = advancedDraw.Run,
            HandDraw = advancedDraw
        };
        if (advancedDraw.AtBoundary)
            projected = projected with { Run = cursor.ActiveRun };
        return cursor.Complete ? projected with { Complete = true } : projected;
    }

    private static Cursor ResumeActualTurn(Cursor cursor)
    {
        var draw = cursor.HandDraw;
        // The underlying automatic pipeline may modify card zones, powers,
        // combat outcome, or issue further Nature requests. The existing
        // game engine remains the single authority for those effects.
        RunState resumed;
        // ResumeAutomaticPipelineContinuation is a functional mechanics
        // transition. Keeping the committed draw.Run as an immutable source
        // allows the compatibility HandDraw checkpoint to remain replayable
        // without another deep clone of the entire run per completed draw.
        using (H8HotPathProfilerV1.Begin("h8.turn_cursor.resume_mechanics"))
        using (ControlledChanceGuardV111.Enter())
            resumed = PrototypeGameEngine.ResumeAutomaticPipelineContinuation(
                draw.Run);

        if (PrototypeGameEngine.IsAtCombatRewardOrTerminalBoundary(resumed))
            return cursor with { ActiveRun = resumed, Complete = true };

        if (resumed.World?.Combat?.PendingChoice?.ControlledTarget is not null ||
            PrototypeGameEngine.HasPendingSupportedAutomaticPipelineSemanticChoice(
                resumed))
            return cursor with
            {
                ActiveRun = resumed,
                SuspendedNestedChoice = true
            };

        using (H8HotPathProfilerV1.Begin("h8.turn_cursor.validate_player"))
        {
            if (!PrototypeGameEngine.IsAtCombatPlayerDecisionBoundary(resumed))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.semantic_end_turn " +
                    "operation=not_at_next_player_boundary");
            var combat = resumed.World?.Combat ??
                throw new InvalidDataException(
                    "Completed H8 end-turn lost its combat state.");
            SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
                combat,
                "Completed semantic end-turn has no Nature deck.",
                "Completed semantic end-turn has an invalid Nature deck boundary.");
            // H8's next boundary already constructs a source-public frame
            // for the actor. Validate action availability there, using the
            // same legal-action menu, instead of constructing and discarding
            // a second complete menu during automatic-turn continuation.
        }
        return cursor with { ActiveRun = resumed, Complete = true };
    }
}
