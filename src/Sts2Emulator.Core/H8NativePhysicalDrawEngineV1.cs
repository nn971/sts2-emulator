namespace Sts2Emulator.Core;

/// <summary>
/// Direct H8 execution of a suspended physical automatic-hand draw.
///
/// The mechanics RunState is the sole owner of live card and zone state while
/// the draw advances. Selection is committed to the same provider-owned
/// hidden deck that the deterministic engine reads, then the engine executes
/// the real draw and CardDrawn hooks. Legacy continuation projections are
/// recreated once at the output boundary for replay/audit compatibility;
/// neither pilot nor end-turn nor hand-draw v111 dispatchers execute.
///
/// The H8 runtime checks its opaque boundary version and physical support
/// before calling this engine. Failure never commits the candidate result
/// to the live H8 session.
/// </summary>
internal static class H8NativePhysicalDrawEngineV1
{
    internal static SemanticPilotCombatV111 AdvanceAutomaticHandDraw(
        SemanticPilotCombatV111 current,
        SemanticEndTurnV111 endTurn,
        SemanticDeckEventV111 drawEvent,
        int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(endTurn);
        ArgumentNullException.ThrowIfNull(drawEvent);
        if (endTurn.Complete ||
            endTurn.HandDraw is not
                { AtBoundary: false, Pending: not null,
                  AutomaticHandDraw: true, FromHandDraw: true,
                  CostPending: null, RandomPileCardPending: null,
                  DrawInsertionPending: null, StratagemSelectionPending: null,
                  DrawHookChoicePending: null } hand ||
            endTurn.EnemyActionPending is not null ||
            endTurn.IntentPending is not null ||
            drawEvent.Purpose != "draw" ||
            selectedIndex < 0 ||
            selectedIndex >= drawEvent.CandidateCardInstanceIds.Length ||
            current.Run.World?.Combat?.PendingChoice is
                { ChoiceId: "controlled_semantic_draw",
                  ControlledSemanticDraw: not null })
            throw new InvalidOperationException(
                "H8 native draw requires a clean automatic hand-draw cursor.");

        // The versioned H8 session has exclusive ownership of this
        // checkpoint. SemanticDrawChoice.Commit and BeginSemanticCombatDraw
        // use functional record transitions: they construct new card-zone,
        // provider and RNG owners rather than mutating the source arrays.
        // Publishing happens only after the complete transition succeeds.
        // A full RunState.Fork here used to copy every unrelated room/map,
        // relic/potion, RNG and combat field once for EACH drawn card.
        using var total = H8HotPathProfilerV1.Begin("h8.draw.native_transition");
        var selected = SemanticDrawChoiceV111.Select(drawEvent, selectedIndex);
        var run = current.Run;
        var world = run.World ?? throw new InvalidDataException(
            "H8 automatic draw has no world.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "H8 automatic draw has no combat.");

        CombatState committed;
        using (H8HotPathProfilerV1.Begin("h8.draw.provider_commit"))
            committed = SemanticDrawStageProfilerV111.Measure(
                "sub.h8_native_draw_provider_commit",
                () => SemanticDrawChoiceV111.Commit(combat, selected, trustedH8: true));
        run = run with { World = world with { Combat = committed } };
        var (advanced, remaining) = SemanticDrawStageProfilerV111.Measure(
            "sub.h8_native_draw_engine_mechanics", () =>
            {
                using (ControlledChanceGuardV111.Enter())
                    return PrototypeGameEngine.BeginSemanticCombatDraw(
                        run, hand.RemainingCount, hand.FromHandDraw);
            });
        var recorded = SemanticDrawStageProfilerV111.Measure(
            "sub.h8_native_draw_recorded_choice",
            () => SemanticDrawChoiceV111.ValidateRecordedChoice(
                advanced, drawEvent, selectedIndex, trustedH8: true));

        // One output projection, only for the still-supported legacy
        // snapshot/replay bridge. H8 M3 will instead commit its native cursor.
        using var projection = H8HotPathProfilerV1.Begin("h8.draw.legacy_projection");
        var advancedHand = hand with
        {
            Run = advanced,
            RemainingCount = remaining
        };
        var endTurnAtBoundary =
            H8NativeTurnContinuationV1.AdvancePhysicalHandDraw(
                endTurn, advancedHand);
        return SemanticPilotCombatEngineV111.PublishH8AdvancedDraw(
            current, endTurnAtBoundary, selectedIndex, recorded);
    }
}
