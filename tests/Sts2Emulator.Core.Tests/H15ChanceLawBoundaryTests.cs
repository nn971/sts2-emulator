using System.Text.Json;
using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Core.Tests;

/// <summary>Nature chooses IDs; the emulator declares and validates each law.</summary>
public sealed class H15ChanceLawBoundaryTests
{
    private static H8CombatRuntimeV1 Mawler(string seed) =>
        H8CombatRuntimeV1.BeginSeeded(new SemanticSeededEntryV111(
            seed, 1, "proto.encounter.mawler_normal", "MawlerNormal",
            2, 10, 1, 3, 1, ReplayOpeningDrawHooks: true));

    private static ChanceLawV111 PendingLaw(H8CombatRuntimeV1 runtime)
    {
        var payload = JsonSerializer.SerializeToElement(
            runtime.DescribePendingReferenceLaw());
        return payload.GetProperty("chanceLaw")
            .Deserialize<ChanceLawV111>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) ??
            throw new InvalidDataException("H8 Chance Law missing.");
    }

    private static H8CombatRuntimeV1 ReachPendingIntent(string seed)
    {
        var runtime = Mawler(seed);
        // The seeded fixture can start at a player boundary. Advance only
        // through legal mechanics decisions until an actual intent is pending.
        for (var steps = 0; !runtime.HasPendingIntent; steps++)
        {
            Assert.True(steps < 200, "Expected a pending H8 enemy intent.");
            Assert.False(runtime.Terminal, "Combat ended before an intent request.");
            if (runtime.HasPendingCardDraw)
                runtime.ChooseDrawCard(
                    runtime.Boundary, runtime.PendingDrawCardInstanceIds()[0]);
            else if (runtime.AtPlayerBoundary && !runtime.AtPlayerChoiceBoundary)
                runtime.ChoosePlayerAction(
                    runtime.Boundary, runtime.EndTurnActionId());
            else
                throw new InvalidOperationException(
                    "Unexpected boundary while advancing to enemy intent: " +
                    runtime.BoundaryKind);
        }
        return runtime;
    }

    [Fact]
    public void ExternalNatureSelectsADeclaredIntentAndRejectsStaleOrUnknownChoices()
    {
        var runtime = ReachPendingIntent("h15-h8-law-intent-test");
        Assert.Equal("enemy_intent", runtime.BoundaryKind);
        var law = PendingLaw(runtime);
        Assert.Equal(ChanceLawV111.SourceVerifiedStatus, law.ProbabilityStatus);
        var token = runtime.Boundary;
        var previousDecisions = runtime.DecisionCount;
        var outcome = law.Outcomes[0].OutcomeId;

        Assert.Throws<InvalidOperationException>(() =>
            runtime.ChooseChanceLawOutcome(
                token, law with { EventId = "another-event" },
                outcome, "test-nature"));
        Assert.Throws<InvalidOperationException>(() =>
            runtime.ChooseChanceLawOutcome(
                token, law, "unknown-intent", "test-nature"));
        Assert.Equal(token, runtime.Boundary);
        Assert.Equal(previousDecisions, runtime.DecisionCount);

        runtime.ChooseChanceLawOutcome(token, law, outcome, "test-nature");
        Assert.Equal(previousDecisions + 1, runtime.DecisionCount);
        var item = runtime.DecisionHistory.Last();
        Assert.Equal("test-nature", item.ProviderId);
        Assert.Equal(outcome, item.IntentId);
        Assert.Throws<InvalidOperationException>(() =>
            runtime.ChooseChanceLawOutcome(token, law, outcome, "test-nature"));
        runtime.AuditReplay();
    }

    [Fact]
    public void ExternalNatureSelectsPhysicalCardFromDeclaredDrawLaw()
    {
        var runtime = Mawler("h15-h8-law-draw-test");
        for (var steps = 0; !runtime.HasPendingCardDraw; steps++)
        {
            Assert.True(steps < 200, "Expected to reach an H8 draw boundary");
            if (runtime.HasPendingIntent)
                runtime.ChooseIntent(runtime.Boundary, runtime.PendingIntentIds()[0]);
            else if (runtime.AtPlayerBoundary && !runtime.Terminal)
                runtime.ChoosePlayerAction(runtime.Boundary, runtime.EndTurnActionId());
            else
                throw new InvalidOperationException(
                    "Unsupported boundary before pending physical draw.");
        }

        var law = PendingLaw(runtime);
        var outcome = law.Outcomes[0].OutcomeId;
        Assert.StartsWith("card_instance:", outcome);
        var id = long.Parse(outcome["card_instance:".Length..],
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains(id, runtime.PendingDrawCardInstanceIds());

        var previous = runtime.DecisionCount;
        runtime.ChooseChanceLawOutcome(runtime.Boundary, law, outcome, "test-nature");
        Assert.Equal(previous + 1, runtime.DecisionCount);
        Assert.Equal(id, runtime.DecisionHistory.Last().CardInstanceId);
        Assert.Equal("test-nature", runtime.DecisionHistory.Last().ProviderId);
        runtime.AuditReplay();
    }
}
