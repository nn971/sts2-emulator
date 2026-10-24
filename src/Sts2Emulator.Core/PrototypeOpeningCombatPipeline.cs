namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static readonly PrototypeOpeningCombatStage[] OpeningCombatStages =
    [
        PrototypeOpeningCombatStage.PlayerSideStart,
        PrototypeOpeningCombatStage.BeforeHandDraw,
        PrototypeOpeningCombatStage.DrawHand,
        PrototypeOpeningCombatStage.AfterHandDraw,
        PrototypeOpeningCombatStage.CombatStarted,
        PrototypeOpeningCombatStage.PlayerTurnStarted,
        PrototypeOpeningCombatStage.BoneTeaAfterTurnStart,
        PrototypeOpeningCombatStage.AutoPrePlay,
        PrototypeOpeningCombatStage.AfterAutoPrePlayPolicy,
        PrototypeOpeningCombatStage.ImbuedAutoPlay,
        PrototypeOpeningCombatStage.LateRelicAutoPlay,
        PrototypeOpeningCombatStage.Complete
    ];

    /// <summary>
    /// Opening side/draw/player-start stages reuse the ordinary dispatch and
    /// choice kernel. Their saved cursor does not change end-turn indices.
    /// Current modeled CombatStarted subscribers have fixed effects; their
    /// scoped review is recorded in the stochastic ownership inventory.
    /// Revisit this stage whenever its subscriber set changes.
    /// </summary>
    private static RunState ResumeOpeningCombat(RunState state, PrototypeOpeningCombatStage start)
    {
        var startIndex = Array.IndexOf(OpeningCombatStages, start);
        if (startIndex < 0)
            throw new InvalidOperationException(
                $"Unknown opening combat stage '{start}'.");
        for (var stageIndex = startIndex;
             stageIndex < OpeningCombatStages.Length - 1;
             stageIndex++)
        {
            var stage = OpeningCombatStages[stageIndex];
            var world = RequireWorld(state);
            var combat = world.Combat ?? throw new InvalidOperationException("Opening combat disappeared.");
            if (AllEnemiesDefeated(combat))
                return state;
            var eventKind = stage switch
            {
                PrototypeOpeningCombatStage.PlayerSideStart => PrototypeCombatEventKind.PlayerSideTurnStarted,
                PrototypeOpeningCombatStage.BeforeHandDraw => PrototypeCombatEventKind.BeforeHandDraw,
                PrototypeOpeningCombatStage.CombatStarted => PrototypeCombatEventKind.CombatStarted,
                PrototypeOpeningCombatStage.PlayerTurnStarted => PrototypeCombatEventKind.OpeningPlayerTurnStarted,
                PrototypeOpeningCombatStage.AutoPrePlay => PrototypeCombatEventKind.AutoPrePlayPhaseEntered,
                _ => (PrototypeCombatEventKind?)null
            };
            if (eventKind is { } kind)
            {
                var prototypeRng = state.Stochastic.PrototypeRng;
                var dispatched = DispatchCombatEvent(
                    state.Player,
                    combat,
                    new PrototypeCombatEvent(kind),
                    prototypeRng,
                    allowSuspension: true);
                state = state with
                {
                    Player = dispatched.Player,
                    Stochastic = RunStochasticStateV111.WithPrototypeRng(
                        state.Stochastic, prototypeRng),
                    World = world with { Combat = dispatched.Combat }
                };
            }
            else if (stage ==
                PrototypeOpeningCombatStage.AfterAutoPrePlayPolicy)
                state = ResolveHistoryCourseAutoPlay(state);
            else if (stage ==
                PrototypeOpeningCombatStage.BoneTeaAfterTurnStart)
            {
                var tea = ApplyBoneTeaAfterPlayerTurnStart(
                    state.Player, combat);
                state = state with
                {
                    Player = tea.Player,
                    World = world with { Combat = tea.Combat }
                };
            }
            else if (stage == PrototypeOpeningCombatStage.ImbuedAutoPlay)
                state = ResolveImbuedAutoPlay(state);
            else if (stage == PrototypeOpeningCombatStage.LateRelicAutoPlay)
                state = ResolveWhisperingEarringAutoPlay(state);
            else if (stage == PrototypeOpeningCombatStage.DrawHand)
                state = DrawOpeningHand(state);
            else if (stage == PrototypeOpeningCombatStage.AfterHandDraw)
                state = state with { World = world with { Combat = RemovePlayerPowers(
                    combat, definition => definition.RemoveAfterHandDraw) } };

            world = RequireWorld(state);
            combat = world.Combat!;
            if (combat.PendingChoice is not null)
                return SetOpeningCombatContinuation(
                    state, OpeningCombatStages[stageIndex + 1]);
        }
        return state;
    }

    private static RunState SetOpeningCombatContinuation(
        RunState state, PrototypeOpeningCombatStage nextStage)
    {
        var world = RequireWorld(state);
        var combat = world.Combat ??
            throw new InvalidOperationException(
                "Opening combat disappeared before its cursor was saved.");
        var continuation = new PrototypeAutomaticPipelineContinuationState(
            PrototypeContent.Rules.EndTurnPipeline.Length,
            OpeningStage: nextStage);
        var updatedCombat = AttachValidatedAutomaticPipelineContinuation(
            combat, continuation);
        return state with
        {
            World = world with
            {
                Combat = updatedCombat
            }
        };
    }

    private static RunState DrawOpeningHand(RunState state)
    {
        var world = RequireWorld(state);
        var combat = world.Combat!;
        var pollinous = ApplyPollinousCoreHandDraw(state.Player);
        var player = pollinous.Player;
        var conch = world.ActiveRoom == PrototypeRoomType.Elite && player.ActiveRelics.Any(relic =>
            relic.RelicId == PrototypeNativeOvergrowthEvents.NeowRelicId("BoomingConch"));
        var target = PrototypeContent.Rules.HandSize + (conch ? 2 : 0)
            + FirstTurnDrawBonus(player) + pollinous.Bonus
            + PrototypeNativeOrobasRelics.HandDrawBonusForTurn(
                player, combat.Turn)
            + combat.PlayerPowers.Sum(power => PrototypeContent.Power(power.PowerId).HandDrawBonusPerStack * power.Stacks);
        // This engine already places persistent Innate cards in hand at entry.
        // BeforeHandDraw-generated cards are additional and do not reduce draw.
        var innateAlreadyDealt = combat.Hand.Count(id =>
        {
            var card = RequireCombatCard(combat, id);
            return !card.IsTemporary && IsInnate(card);
        });
        var prototypeRng = state.Stochastic.PrototypeRng;
        var suspensionPolicy = ControlledChanceGuardV111.Active &&
            combat.Stochastic.SemanticDeck is
                { Mode: SemanticDeckStateV111.NatureMode }
            ? PrototypeDrawCardsSuspensionPolicyV111.SemanticDrawCards()
            : PrototypeDrawCardsSuspensionPolicyV111.None;
        var drawn = DrawCards(player, combat, Math.Max(0, target - innateAlreadyDealt),
            prototypeRng, fromHandDraw: true,
            suspensionPolicy: suspensionPolicy);
        player = drawn.Player;
        combat = drawn.Combat;
        if (drawn.BoundaryKind ==
            PrototypeDrawCardsBoundaryKind.ShuffleSelection)
        {
            var operations = new Queue<PrototypeQueuedOperation>(
            [
                StratagemChoiceOperation(combat),
                new(PrototypeCombatEffectKind.DrawCards, drawn.RemainingCount, FromHandDraw: true)
            ]);
            var resolved = ResolveOperations(
                player, combat, operations, prototypeRng,
                moveSourceCardOnCompletion: false);
            player = resolved.Player;
            combat = resolved.Combat;
        }
        else if (drawn.BoundaryKind !=
            PrototypeDrawCardsBoundaryKind.Complete)
        {
            throw new NotSupportedException(
                "UNSUPPORTED_OPENING_DRAW " +
                "site=combat.opening_draw operation=" +
                drawn.BoundaryKind);
        }
        state = state with
        {
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng)
        };
        return PublishDrawMechanicsState(state, player, combat);
    }
}
