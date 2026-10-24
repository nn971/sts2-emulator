namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private readonly record struct EffectContinuationContext(
        long? SourceCardInstanceId,
        PrototypeCardZone SourceCardDestination,
        PrototypeCombatEvent[]? CompletionEvents,
        PrototypeCardPlaySeriesState? CardPlaySeries,
        bool MoveSourceCardOnCompletion,
        bool RemoveSourceCardOnCompletion,
        bool SourceCardAlreadyMoved,
        PrototypeEventDispatchContinuationState? EventDispatchContinuation);

    private static PrototypeChoiceResolutionContinuationState
        CreateEffectContinuation(
            EffectContinuationContext context,
            IEnumerable<PrototypeQueuedOperation> remainingOperations) =>
        new(
            SourceCardInstanceId: context.SourceCardInstanceId,
            SourceCardDestination: context.SourceCardDestination,
            Operations: remainingOperations.ToArray(),
            PendingDiscardEvents: [],
            PendingSlyCardInstanceIds: [],
            CompletionEvents: context.CompletionEvents?.ToArray() ?? [],
            CardPlaySeries: context.CardPlaySeries,
            MoveSourceCardOnCompletion: context.MoveSourceCardOnCompletion,
            RemoveSourceCardOnCompletion:
                context.RemoveSourceCardOnCompletion,
            SourceCardAlreadyMoved: context.SourceCardAlreadyMoved,
            EventDispatchContinuation:
                context.EventDispatchContinuation?.Fork());

    private static (PlayerState Player, CombatState Combat)
        ResumeChoiceResolutionContinuation(
            PlayerState player,
            CombatState combat,
            RngBundle rng,
            PrototypeChoiceResolutionContinuationState continuation,
            bool resumeCardPlaySeries = true)
    {
        for (var eventIndex = 0;
             eventIndex < continuation.PendingDiscardEvents.Length;
             eventIndex++)
        {
            var pendingEvent =
                continuation.PendingDiscardEvents[eventIndex];
            var pendingEventDepth =
                pendingEvent.Kind ==
                    PrototypeCombatEventKind.EnemyDefeated &&
                continuation.EnemyDefeatFollowUp is { } defeatFollowUp
                    ? defeatFollowUp.EventDepth + 1
                    : 0;
            var dispatched = DispatchCombatEvent(
                player,
                combat,
                pendingEvent,
                rng,
                pendingEventDepth,
                allowSuspension: true);
            player = dispatched.Player;
            combat = dispatched.Combat;

            if (combat.PendingChoice is not null)
            {
                var remaining = continuation with
                {
                    PendingDiscardEvents =
                        continuation.PendingDiscardEvents
                            .Skip(eventIndex + 1)
                            .ToArray()
                };
                combat = AttachOuterChoiceContinuation(
                    combat,
                    remaining);
                return (player, combat);
            }
        }

        if (continuation.EnemyDefeatFollowUp is { } pendingDefeatFollowUp)
        {
            var completedDefeat = CompleteEnemyDefeatFollowUp(
                player, combat, rng, pendingDefeatFollowUp);
            player = completedDefeat.Player;
            combat = completedDefeat.Combat;
            continuation = continuation with
            {
                EnemyDefeatFollowUp = null
            };
        }

        if (continuation.DamageRepeatContinuation is { } damageRepeat)
        {
            continuation = continuation with
            {
                DamageRepeatContinuation = null
            };
            var resumedRepeat = ResolveDamageAllEnemiesRepeatPerKill(
                player, combat, damageRepeat.Operation, rng,
                continuation.SourceCardInstanceId,
                damageRepeat.EventDepth, continuation, damageRepeat);
            player = resumedRepeat.Player;
            combat = resumedRepeat.Combat;
            if (combat.PendingChoice is not null)
                return (player, combat);
        }

        if (continuation.CompletePlayerHandCleanup)
        {
            combat = ClearTemporaryCardKeywordOverrides(
                combat,
                PrototypeCardKeywordOverrideExpiry.EndOfTurn);
            combat = DecrementPlayerPowers(
                combat,
                definition => definition.DecrementAfterHandCleanup);
            continuation = continuation with
            {
                CompletePlayerHandCleanup = false
            };
        }

        for (var slyIndex = 0;
             slyIndex < continuation.PendingSlyCardInstanceIds.Length;
             slyIndex++)
        {
            if (AllEnemiesDefeated(combat))
            {
                break;
            }

            var slyCardId =
                continuation.PendingSlyCardInstanceIds[slyIndex];
            if (!combat.DiscardPile.Contains(slyCardId))
            {
                continue;
            }

            if (ControlledChanceGuardV111.Active)
            {
                var slyDefinition = PrototypeContent.Card(
                    RequireCombatCard(combat, slyCardId).CardId);
                if (slyDefinition.Target == PrototypeCardTarget.Enemy)
                {
                    var eligible = combat.Enemies
                        .Where(enemy => enemy.Hp > 0 && !enemy.Escaped)
                        .Select(enemy => enemy.InstanceId).ToArray();
                    if (eligible.Length > 0)
                    {
                        var remaining = continuation with
                        {
                            PendingDiscardEvents = [],
                            PendingSlyCardInstanceIds = continuation
                                .PendingSlyCardInstanceIds.Skip(slyIndex + 1)
                                .ToArray()
                        };
                        return (player, MakeControlledRandomTargetChoice(
                            combat, "sly_auto_play", eligible,
                            remaining, slyCardId));
                    }
                }
            }

            var autoPlayed = AutoPlaySlyCard(
                player,
                combat,
                slyCardId,
                rng);
            player = autoPlayed.Player;
            combat = autoPlayed.Combat;

            if (combat.PendingChoice is not null)
            {
                var remaining = continuation with
                {
                    PendingDiscardEvents =
                        Array.Empty<PrototypeCombatEvent>(),
                    PendingSlyCardInstanceIds =
                        continuation.PendingSlyCardInstanceIds
                            .Skip(slyIndex + 1)
                            .ToArray()
                };
                combat = AttachOuterChoiceContinuation(
                    combat,
                    remaining);
                return (player, combat);
            }
        }

        var resolved = ResolveOperations(
            player,
            combat,
            new Queue<PrototypeQueuedOperation>(
                continuation.Operations),
            rng,
            continuation.SourceCardInstanceId,
            continuation.SourceCardDestination,
            continuation.CompletionEvents,
            cardPlaySeries: continuation.CardPlaySeries,
            moveSourceCardOnCompletion:
                continuation.MoveSourceCardOnCompletion,
            removeSourceCardOnCompletion:
                continuation.RemoveSourceCardOnCompletion,
            sourceCardAlreadyMoved:
                continuation.SourceCardAlreadyMoved,
            eventDispatchContinuation:
                continuation.EventDispatchContinuation);

        if (resolved.Combat.PendingChoice is not null)
        {
            return (
                resolved.Player,
                AttachParentChoiceContinuation(
                    resolved.Combat,
                    continuation.Parent));
        }

        if (continuation.EventDispatchContinuation is not null)
        {
            resolved = ResumeEventDispatchContinuation(
                resolved.Player,
                resolved.Combat,
                continuation.EventDispatchContinuation,
                rng);
            if (resolved.Combat.PendingChoice is not null)
            {
                return (
                    resolved.Player,
                    AttachParentChoiceContinuation(
                        resolved.Combat,
                        continuation.Parent));
            }
        }

        if (resumeCardPlaySeries
            && continuation.CardPlaySeries is
                { HasRemainingExecutions: true } series)
        {
            if (AllEnemiesDefeated(resolved.Combat))
            {
                var finalized = FinalizeInterruptedCardPlaySeries(
                        resolved.Player,
                        resolved.Combat,
                        rng,
                        series);
                resolved = ApplyVigorousAfterCardPlayed(
                    finalized.Player,
                    finalized.Combat,
                    series.SourceCardInstanceId);
            }
            else
            {
                resolved = ResolveCardPlaySeries(
                    resolved.Player,
                    resolved.Combat,
                    rng,
                    series);
            }

            if (resolved.Combat.PendingChoice is not null)
            {
                return (
                    resolved.Player,
                    AttachParentChoiceContinuation(
                        resolved.Combat,
                        continuation.Parent));
            }
        }

        if (continuation.CardPlaySeries is
                { HasRemainingExecutions: false } completedSeries
            && resolved.Combat.PendingChoice is null
            && resolved.Player.Hp > 0)
        {
            resolved = (resolved.Player, CompletePaelsLegionCardPlay(
                resolved.Combat, completedSeries.SourceCardInstanceId));
            resolved = ApplyVigorousAfterCardPlayed(
                resolved.Player,
                resolved.Combat,
                completedSeries.SourceCardInstanceId);
        }

        return continuation.Parent is null
            ? resolved
            : ResumeChoiceResolutionContinuation(
                resolved.Player,
                resolved.Combat,
                rng,
                continuation.Parent);
    }

    private static CombatState AttachOuterChoiceContinuation(
        CombatState combat,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        var pending = combat.PendingChoice
            ?? throw new InvalidOperationException(
                "Nested choice continuation requires a pending choice.");
        var combined = pending.OuterChoiceContinuation is null
            ? continuation.Fork()
            : AppendChoiceContinuationParent(
                pending.OuterChoiceContinuation,
                continuation);
        return combat with
        {
            PendingChoice = pending with
            {
                OuterChoiceContinuation = combined
            }
        };
    }

    private static CombatState AttachParentChoiceContinuation(
        CombatState combat,
        PrototypeChoiceResolutionContinuationState? parent) =>
        parent is null
            ? combat
            : AttachOuterChoiceContinuation(combat, parent);

    private static PrototypeChoiceResolutionContinuationState
        AppendChoiceContinuationParent(
            PrototypeChoiceResolutionContinuationState current,
            PrototypeChoiceResolutionContinuationState parent) =>
        current.Parent is null
            ? current with { Parent = parent.Fork() }
            : current with
            {
                Parent = AppendChoiceContinuationParent(
                    current.Parent,
                    parent)
            };

}
