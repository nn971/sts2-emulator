using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>Source-independent comparison of the engine's authoritative
/// mechanical projection, regardless of the RNG realization.</summary>
public sealed class StochasticMechanicsProjectionAcceptanceTests
{
    [Fact]
    public void ProviderPrivateRngChangesDoNotChangeMechanicalComparison()
    {
        var original = PrototypeGameFactory.Create(
            "source-independent-stochastic-mechanics", 0);
        original.Validate();
        var differentProvider = original.Fork() with
        {
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                original.Stochastic,
                PrototypeRng.CreateBundle("independent-replay-provider"))
        };
        differentProvider.Validate();

        var mechanics = SimulationMechanicsProjectionEngineV111.Compare(
            original, differentProvider);
        Assert.True(mechanics.Equivalent);
        Assert.Empty(mechanics.Differences);
        Assert.Equal(mechanics.LeftFingerprint, mechanics.RightFingerprint);

        // Comparing mechanical differences must remain stricter than merely
        // comparing stochastic trace or player action shapes.
        var changedGold = differentProvider with
        {
            Player = differentProvider.Player with
            {
                Gold = differentProvider.Player.Gold + 1
            }
        };
        changedGold.Validate();
        var changed = SimulationMechanicsProjectionEngineV111.Compare(
            original, changedGold);
        Assert.False(changed.Equivalent);
        Assert.NotEmpty(changed.Differences);
    }

    [Fact]
    public void NativeAndReferenceNatureCapturesMatchForDeterministicPlayerAction()
    {
        var entry = new SemanticSeededEntryV111(
            "source-independent-native-reference-action", 2,
            PrototypeNativeHiveNormals.ExoskeletonsNormalId,
            "ExoskeletonsNormal", 20, 0, 1, 3, 5);
        var native = SemanticPilotCombatEngineV111.BeginSeeded(
            entry with { NativePolicyBaseline = true });
        var natureCombat = SemanticPilotCombatEngineV111.BeginSeeded(entry);
        var actionId = FindDeterministicAttackAction(natureCombat.Run);

        Assert.Equal(actionId, FindDeterministicAttackAction(native.Run));
        var referenceNature = ReferenceNatureSessionV111.Begin(
            natureCombat, 17);
        var steppedNature = SemanticPilotCombatEngineV111.Step(
            referenceNature.Combat, referenceNature.Combat.Fingerprint,
            actionId);
        referenceNature = referenceNature with { Combat = steppedNature };
        referenceNature.Validate();
        native = SemanticPilotCombatEngineV111.Step(
            native, native.Fingerprint, actionId);

        var nativeCapture = SimulationProviderReplayCaptureV111
            .FromSemanticNativePolicySession(native);
        var natureCapture = SimulationProviderReplayCaptureV111
            .FromReferenceNatureSemanticCombatSession(referenceNature);
        var comparison = SimulationProviderReplayComparisonEngineV111.Compare(
            nativeCapture, natureCapture);

        Assert.True(comparison.SamePlayerActions);
        Assert.True(comparison.SameOriginMechanics,
            comparison.OriginMechanics.ToString());
        Assert.True(comparison.SameMechanicsAtEveryActionBoundaryForSameActions,
            comparison.ActionBoundaryMechanics.ToString());
        Assert.True(comparison.SameResultMechanics,
            comparison.ResultMechanics.ToString());
    }

    private static string FindDeterministicAttackAction(RunState run)
    {
        var cards = run.World?.Combat?.Cards.ToDictionary(
            card => card.InstanceId) ?? throw new InvalidOperationException(
                "Seeded combat fixture has no card instances.");
        var environment = new PrototypeAiEnvironment();
        var action = environment.Observe(run).LegalActions
            .Where(candidate => candidate.Kind == "play_card")
            .Where(candidate => cards.TryGetValue(
                environment.ResolvePlayerAction(run, candidate.ActionId).Action
                    .ReadPayload<PlayCardPayload>().CardInstanceId,
                out var card) && card.CardId == "proto.silent.strike")
            .FirstOrDefault() ?? throw new InvalidOperationException(
                "Seeded combat fixture has no deterministic Strike action.");
        return action.ActionId;
    }

}
