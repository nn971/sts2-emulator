using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Core.Tests;

public sealed class H15NativeCombatEntryCandidateV1Tests
{
    private static readonly PrototypeGameEngine Engine = new();

    private static RunState NativeWeakEntry()
    {
        var configuration = RunConfiguration.Create(
            firstAct: ActIdentity.Overgrowth) with
        {
            FidelityId = V111Build.NativeWeakCombatEntryFidelityId
        };
        for (var index = 0; index < 48; index++)
        {
            var seed = "h15-native-candidate-" + index;
            var state = Engine.Step(
                V111RunFactory.Create(seed, configuration),
                GameAction.Empty("start_run")).State;
            for (var i = 0;
                i < 30 && state.Phase is RunPhase.Event or RunPhase.Reward;
                i++)
                state = Engine.Step(
                    state, Engine.GetLegalActions(state)[0]).State;
            if (state.Phase != RunPhase.MapChoice)
                continue;
            var mapChoice = Engine.GetLegalActions(state)
                .First(x => x.Kind == "choose_map_node");
            try
            {
                var entered = Engine.Step(state, mapChoice).State;
                var combat = entered.World?.Combat;
                if (entered.Phase == RunPhase.Combat &&
                    combat?.NativeRng is not null &&
                    combat.Turn == 1 && combat.IsPlayerTurn &&
                    combat.PendingChoice is null &&
                    combat.AutomaticPipelineContinuation is null &&
                    combat.PendingKnowledgeDemonChoice is null)
                    return entered;
            }
            catch (NotSupportedException)
            {
                // Some seeds can select source-audited encounters outside
                // the specific first-combat fidelity gate.
            }
        }
        throw new InvalidOperationException(
            "No settled native weak-combat entry was found.");
    }

    [Fact]
    public void NativeCandidatePreservesPublicDecisionAndReplacesFutureRng()
    {
        var source = NativeWeakEntry();
        var sourceHash = CanonicalJson.Sha256(source);
        var view = new PrototypeAiEnvironment();
        var before = view.Observe(source);

        var first = H15NativeCombatEntryCandidateV1.Build(
            source, "independent-h15-1");
        var second = H15NativeCombatEntryCandidateV1.Build(
            source, "independent-h15-2");
        PrototypeStateInvariants.Validate(first);
        PrototypeStateInvariants.Validate(second);
        Assert.Equal(sourceHash, CanonicalJson.Sha256(source));

        foreach (var candidate in new[] { first, second })
        {
            var visible = view.Observe(candidate);
            Assert.Equal(before.ObservationHash, visible.ObservationHash);
            Assert.Equal(
                before.LegalActions.Select(x => x.ActionId),
                visible.LegalActions.Select(x => x.ActionId));
            Assert.Equal(
                before.LegalActions.Select(x => x.Kind),
                visible.LegalActions.Select(x => x.Kind));
            Assert.Equal(
                SemanticDeckStateV111.NatureMode,
                candidate.World!.Combat!.SemanticDeck!.Mode);
            Assert.Equal(
                source.World!.Combat!.DrawPile.Order(),
                candidate.World.Combat.DrawPile.Order());
            Assert.All(candidate.World.Combat.SemanticDeck.Slots,
                slot => Assert.Null(slot.KnownCardInstanceId));
            Assert.NotEqual(CanonicalJson.Sha256(source.NativeRunRng),
                CanonicalJson.Sha256(candidate.NativeRunRng));
            Assert.Equal(
                CanonicalJson.Sha256(candidate.NativeRunRng),
                CanonicalJson.Sha256(
                    candidate.World.Combat.NativeRng!.RunStreams));
        }
        Assert.NotEqual(CanonicalJson.Sha256(first.NativeRunRng),
            CanonicalJson.Sha256(second.NativeRunRng));
        Assert.Equal("unavailable",
            H15NativeCombatEntryCandidateV1.FairSearchCapabilityId);
    }

    [Fact]
    public void NativeStructurePrototypeRngRootHasSeparateCandidateProvenance()
    {
        var source = PrototypeNativeOvergrowthRunFactory.Create("native-entry");
        var neow = Engine.GetLegalActions(source)[0];
        source = Engine.Step(source, neow).State;
        Assert.Equal(RunPhase.MapChoice, source.Phase);
        source = Engine.Step(
            source, Engine.GetLegalActions(source)[0]).State;
        Assert.Equal(RunPhase.Combat, source.Phase);
        Assert.Null(source.NativeRunRng);
        Assert.Null(source.World!.Combat!.NativeRng);
        var originalHash = CanonicalJson.Sha256(source);

        var candidate = H15NativeCombatEntryCandidateV1.Build(
            source, "h15-independent-prototype-candidate");
        Assert.Equal(originalHash, CanonicalJson.Sha256(source));
        Assert.Equal(
            H15NativeCombatEntryCandidateV1.PrototypeCombatProviderClass,
            H15NativeCombatEntryCandidateV1.ProviderClass(source));
        Assert.Equal(
            H15NativeCombatEntryCandidateV1.PrototypeCandidateModelId,
            H15NativeCombatEntryCandidateV1.ModelId(source));
        Assert.Null(candidate.NativeRunRng);
        Assert.Null(candidate.World!.Combat!.NativeRng);
        Assert.Equal(
            SemanticDeckStateV111.NatureMode,
            candidate.World.Combat.SemanticDeck!.Mode);
        Assert.Equal("unavailable",
            H15NativeCombatEntryCandidateV1.FairSearchCapabilityId);
        Assert.Equal(
            new PrototypeAiEnvironment().Observe(source).ObservationHash,
            new PrototypeAiEnvironment().Observe(candidate).ObservationHash);
        PrototypeStateInvariants.Validate(candidate);
    }

    [Fact]
    public void NativeCandidateRequiresGenuineUnresolvedNativeEntry()
    {
        var native = NativeWeakEntry();
        var sourceHash = CanonicalJson.Sha256(native);
        Assert.Throws<NotSupportedException>(() =>
            H15NativeCombatEntryCandidateV1.Build(
                native with { NativeRunRng = null }, "independent"));
        Assert.Throws<NotSupportedException>(() =>
            H15NativeCombatEntryCandidateV1.Build(
                native with { ReferenceNatureState =
                    ReferenceNatureStateV111.Create(1) }, "independent"));
        Assert.Throws<NotSupportedException>(() =>
            H15NativeCombatEntryCandidateV1.Build(
                native with { World = native.World! with
                {
                    Combat = native.World.Combat! with { Turn = 2 }
                } }, "independent"));
        Assert.Equal(sourceHash, CanonicalJson.Sha256(native));
    }
}
