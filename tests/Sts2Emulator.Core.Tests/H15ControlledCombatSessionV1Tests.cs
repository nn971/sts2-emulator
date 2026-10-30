using System.Text.Json;
using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Core.Tests;

public sealed class H15ControlledCombatSessionV1Tests
{
    private static RunState NativeStructureEntry()
    {
        var engine = new PrototypeGameEngine();
        var source = PrototypeNativeOvergrowthRunFactory.Create(
            "native-entry");
        for (var step = 0;
             step < 30 && source.Phase is RunPhase.Event or RunPhase.Reward;
             step++)
            source = engine.Step(
                source, engine.GetLegalActions(source)[0]).State;
        Assert.Equal(RunPhase.MapChoice, source.Phase);
        source = engine.Step(
            source, engine.GetLegalActions(source)
                .First(action => action.Kind == "choose_map_node")).State;
        Assert.Equal(RunPhase.Combat, source.Phase);
        Assert.Null(source.World!.Combat!.NativeRng);
        return source;
    }

    [Fact]
    public void BeginKeepsRealCombatMechanicsAndSeparateNatureOwnership()
    {
        var source = NativeStructureEntry();
        var originalHash = CanonicalJson.Sha256(source);
        var frame = new PrototypeAiEnvironment().Observe(source);
        var session = H15ControlledCombatSessionV1.Begin(
            source, "h15-independent-experimental-seed");
        Assert.Equal("player", session.BoundaryKind);
        Assert.Equal(H15NativeCombatEntryCandidateV1.PrototypeCandidateModelId,
            session.CandidateModelId);
        Assert.Equal("unavailable",
            H15ControlledCombatSessionV1.FairContinuationCapability);
        Assert.Equal(frame.ObservationHash,
            new PrototypeAiEnvironment().Observe(session.Run).ObservationHash);
        Assert.Null(session.Run.World!.Combat!.NativeRng);
        Assert.Equal(SemanticDeckStateV111.NatureMode,
            session.Run.World.Combat.SemanticDeck!.Mode);
        Assert.Equal(originalHash, CanonicalJson.Sha256(source));
        Assert.Throws<InvalidOperationException>(() =>
            session.EndTurn("not-a-legal-end-turn"));
        Assert.Equal(0L, session.DecisionVersion);
    }

    [Fact]
    public void AuditedStarterCardAdvancesRealPlayerStateBeforeEndTurn()
    {
        var source = NativeStructureEntry();
        var originalHash = CanonicalJson.Sha256(source);
        var session = H15ControlledCombatSessionV1.Begin(
            source, "h15-guarded-player-card-seed");
        var environment = new PrototypeAiEnvironment();
        var legal = environment.Observe(session.Run).LegalActions;
        var cardAction = legal.FirstOrDefault(action =>
        {
            if (action.Kind != "play_card")
                return false;
            var payload = action.Payload.Deserialize<PlayCardPayload>();
            return payload is not null &&
                session.Run.World!.Combat!.Cards.Any(card =>
                    card.InstanceId == payload.CardInstanceId &&
                    card.CardId is "proto.silent.strike" or
                        "proto.silent.defend" or "proto.silent.neutralize");
        });
        Assert.NotNull(cardAction);
        var after = session.PlayerAction(cardAction!.ActionId);
        Assert.Equal(1L, after.DecisionVersion);
        Assert.Equal("player", after.BoundaryKind);
        Assert.NotEqual(CanonicalJson.Sha256(session.Run),
            CanonicalJson.Sha256(after.Run));
        Assert.Equal(originalHash, CanonicalJson.Sha256(source));
        Assert.Equal(
            CanonicalJson.Sha256(session.Run.World!.Combat!.SemanticDeck),
            CanonicalJson.Sha256(after.Run.World!.Combat!.SemanticDeck));
        Assert.Equal("unavailable",
            H15ControlledCombatSessionV1.FairContinuationCapability);

        var endTurn = environment.Observe(after.Run).LegalActions
            .Single(action => action.Kind == "end_turn");
        try
        {
            var next = after.EndTurn(endTurn.ActionId);
            Assert.Equal(2L, next.DecisionVersion);
        }
        catch (NotSupportedException failure)
        {
            Assert.Contains("UNSUPPORTED_", failure.Message);
        }
    }

    [Fact]
    public void AuditedPotionUsesSharedSemanticActionAndPreservesNatureCursor()
    {
        var session = H15ControlledCombatSessionV1.Begin(
            NativeStructureEntry(), "h15-shared-potion-adapter");
        var empty = PrototypeJson.EmptyObject();
        var slots = (PotionInstance?[])session.Run.Player.PotionSlots.Clone();
        slots[0] = new PotionInstance("proto.potion.block", empty);
        var combat = session.Run.World!.Combat!;
        var withPotion = session with
        {
            Run = session.Run with
            {
                Player = session.Run.Player with { PotionSlots = slots },
                World = session.Run.World with
                {
                    Combat = combat with
                    {
                        Potions = (combat.Potions ??
                            Array.Empty<CombatPotionState>()).Append(
                            new CombatPotionState(
                                0, "proto.potion.block", empty)).ToArray()
                    }
                }
            }
        };
        PrototypeStateInvariants.Validate(withPotion.Run);
        var action = new PrototypeAiEnvironment().Observe(withPotion.Run)
            .LegalActions.Single(a => a.Kind == "use_potion" &&
                a.Payload.Deserialize<UsePotionPayload>()!.Slot == 0);
        var result = withPotion.PlayerAction(action.ActionId);
        Assert.Equal("player", result.BoundaryKind);
        Assert.Equal(1L, result.DecisionVersion);
        Assert.Null(result.Run.Player.PotionSlots[0]);
        Assert.Equal(0L, result.NatureState.SampleCount);
        Assert.Equal(
            CanonicalJson.Sha256(withPotion.Run.World!.Combat!.SemanticDeck),
            CanonicalJson.Sha256(result.Run.World!.Combat!.SemanticDeck));
        // The shared replay contract, rather than a second H15 potion
        // resolver, is authoritative for the same source action.
        var shared = SemanticPlayerActionEngineV111.Step(
            withPotion.Run, action.ActionId);
        Assert.Equal(CanonicalJson.Sha256(result.Run),
            CanonicalJson.Sha256(shared.Run));
    }

    [Fact]
    public void KillingCardCanPublishGenuineCombatRewardBoundary()
    {
        // Test-only low-HP mechanics setup from a real native-structure
        // root. This checks that a final hit exits combat without a false
        // unsupported-intermediate-player-boundary error.
        var original = NativeStructureEntry();
        var session = H15ControlledCombatSessionV1.Begin(
            original, "h15-killing-card-transition");
        var combat = session.Run.World!.Combat!;
        var attack = new PrototypeAiEnvironment().Observe(session.Run)
            .LegalActions.FirstOrDefault(action =>
            {
                if (action.Kind != "play_card")
                    return false;
                var payload = action.Payload.Deserialize<PlayCardPayload>();
                return payload?.TargetEnemyId is not null &&
                    combat.Cards.Any(card =>
                        card.InstanceId == payload.CardInstanceId &&
                        card.CardId is ("proto.silent.strike" or
                            "proto.silent.neutralize"));
            });
        Assert.NotNull(attack);
        var target = attack!.Payload.Deserialize<PlayCardPayload>()!
            .TargetEnemyId!.Value;
        var enemies = combat.Enemies.Select(enemy =>
            enemy with { Hp = enemy.InstanceId == target ? 1 : 0 })
            .ToArray();
        var reducedCombat = combat with { Enemies = enemies };
        var reducedRun = session.Run with
        {
            World = session.Run.World with { Combat = reducedCombat }
        };
        var reduced = session with { Run = reducedRun };
        var progressed = reduced.PlayerAction(attack.ActionId);
        Assert.Equal("combat_exit", progressed.BoundaryKind);
        Assert.Equal(RunPhase.Reward, progressed.Run.Phase);
        Assert.NotNull(progressed.Run.World!.Reward);
        Assert.Null(progressed.Run.World.Combat);
        Assert.Equal(1L, progressed.DecisionVersion);
        Assert.Equal("player", session.BoundaryKind);
    }

    [Fact]
    public void RealEndTurnIsEitherTypedOrExplicitlyUnsupported()
    {
        var source = NativeStructureEntry();
        var session = H15ControlledCombatSessionV1.Begin(
            source, "h15-experimental-endturn-seed");
        var legal = new PrototypeAiEnvironment().Observe(session.Run)
            .LegalActions.Single(action => action.Kind == "end_turn");
        try
        {
            var advanced = session.EndTurn(legal.ActionId);
            Assert.Equal(1L, advanced.DecisionVersion);
            Assert.Equal("player", session.BoundaryKind);
            Assert.Null(session.PendingTurn);
            if (advanced.BoundaryKind is "enemy_action" or "enemy_intent"
                or "draw_card" or "random_target")
            {
                var law = advanced.PendingLaw();
                Assert.Equal(ChanceLawV111.UnverifiedStatus,
                    law.ProbabilityStatus);
                Assert.NotEmpty(law.Outcomes);
                law.Validate();
                var original = advanced.DecisionVersion;
                Assert.Throws<InvalidOperationException>(() =>
                    advanced.Choose(
                        law with { EventId = "stale-event" },
                        law.Outcomes[0].OutcomeId));
                Assert.Equal(original, advanced.DecisionVersion);

                // H15 consumes the existing ReferenceNatureV111 sampler.
                // Sampler state and mechanics advance atomically and the
                // explicit external (scripted/learned) path remains separate.
                var (referenceNext, selection) = advanced.ChooseReference();
                Assert.Equal(law.LawId, selection.LawId);
                Assert.Equal(law.EventId, selection.EventId);
                Assert.Contains(law.Outcomes,
                    outcome => outcome.OutcomeId == selection.OutcomeId);
                Assert.Equal(0L, advanced.NatureState.SampleCount);
                Assert.Equal(1L, referenceNext.NatureState.SampleCount);
                Assert.Equal(original + 1, referenceNext.DecisionVersion);
                var (repeatedNext, repeatedSelection) =
                    advanced.ChooseReference();
                Assert.Equal(selection.OutcomeId, repeatedSelection.OutcomeId);
                Assert.Equal(
                    CanonicalJson.Sha256(referenceNext.Run),
                    CanonicalJson.Sha256(repeatedNext.Run));
                var scriptedNext = advanced.Choose(
                    law, selection.OutcomeId);
                Assert.Equal(0L, scriptedNext.NatureState.SampleCount);
                Assert.Equal(
                    CanonicalJson.Sha256(referenceNext.Run),
                    CanonicalJson.Sha256(scriptedNext.Run));
            }
            else
                Assert.Contains(advanced.BoundaryKind,
                    new[] { "player", "terminal", "unsupported" });
            Assert.Equal("unavailable",
                H15ControlledCombatSessionV1.FairContinuationCapability);
        }
        catch (NotSupportedException unsupported)
        {
            // The controlled chance guard must refuse any still-unowned
            // gameplay RNG call. No legacy RNG fallback is permitted.
            Assert.Contains("UNSUPPORTED_", unsupported.Message);
        }
    }
}
