namespace Sts2Emulator.Core;

/// <summary>
/// Outcome transcript for a controlled stochastic run action that is not an
/// event choice. Probability status remains unverified until source-law audit.
/// </summary>
public sealed record ControlledRunActionTraceV111(
    string ActionId,
    RandomIndexDecisionV111[] Decisions,
    string[][] CandidateLabels,
    string ProbabilityStatus = "UNVERIFIED")
{
    public ControlledRunActionTraceV111 Fork() => this with
    {
        Decisions = Decisions.Select(
            PrototypeControlledEventChoiceContinuationV111.CloneDecision)
            .ToArray(),
        CandidateLabels = CandidateLabels.Select(labels =>
            (string[])labels.Clone()).ToArray()
    };

    public void Validate()
    {
        if (ActionId != PrototypeNativeRestSiteActionsV111.Dig
            || Decisions is null || CandidateLabels is null
            || Decisions.Length == 0
            || Decisions.Length != CandidateLabels.Length
            || ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Malformed controlled run-action transcript.");
        for (var index = 0; index < Decisions.Length; index++)
        {
            var decision = Decisions[index];
            decision.Validate();
            if (decision.ProviderMode != "controlled-v111"
                || decision.NativeCursorBefore is not null
                || decision.NativeCursorAfter is not null
                || decision.Request.SiteId !=
                    "relic.unowned_random_selection.index_draw"
                || CandidateLabels[index] is null
                || CandidateLabels[index].Length !=
                    decision.Request.SupportCount
                || CandidateLabels[index].Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException(
                    "Controlled run-action transcript contains an invalid outcome.");
        }
    }
}

/// <summary>
/// Serializable boundary for a simple run action whose first controlled
/// outcome is reached before any action effects are committed.
/// </summary>
public sealed record PrototypeControlledRunActionContinuationV111(
    RunState PartialState,
    string ActionId,
    RandomIndexDecisionV111[] Decisions,
    string[][] DecisionCandidateLabels,
    RandomIndexRequestV111 PendingRequest,
    string[] PendingCandidateLabels)
{
    public PrototypeControlledRunActionContinuationV111 Fork() => this with
    {
        PartialState = PartialState.Fork(),
        Decisions = Decisions.Select(
            PrototypeControlledEventChoiceContinuationV111.CloneDecision)
            .ToArray(),
        DecisionCandidateLabels = DecisionCandidateLabels.Select(labels =>
            (string[])labels.Clone()).ToArray(),
        PendingRequest =
            PrototypeControlledEventChoiceContinuationV111.CloneRequest(
                PendingRequest),
        PendingCandidateLabels = (string[])PendingCandidateLabels.Clone()
    };

    public void Validate()
    {
        if (PartialState is null || ActionId is null
            || Decisions is null || DecisionCandidateLabels is null
            || DecisionCandidateLabels.Length != Decisions.Length
            || PendingRequest is null || PendingCandidateLabels is null)
            throw new InvalidDataException(
                "Malformed controlled run-action continuation.");

        var state = PartialState;
        if (ActionId != PrototypeNativeRestSiteActionsV111.Dig
            || state.Phase != RunPhase.Rest
            || state.ControlledRunActionChoice is not null
            || state.ControlledEventRewardChoice is not null
            || state.World?.Event?.ControlledChoice is not null
            || !PrototypeNativeRestSiteRelics.CanDig(state.Player))
            throw new InvalidDataException(
                "Controlled run-action frame is outside the legacy Shovel boundary.");

        var available = PrototypeContent.RelicPool
            .Where(id => !state.Player.Relics.Any(relic =>
                StringComparer.Ordinal.Equals(relic.RelicId, id)))
            .ToArray();
        PendingRequest.Validate();
        if (PendingRequest.SiteId !=
                "relic.unowned_random_selection.index_draw"
            || PendingRequest.StreamId != "event"
            || PendingRequest.OwnerId != "rest:shovel:pool:"
                + CanonicalJson.Sha256(available)
            || !PendingRequest.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, available.Length))
            || PendingCandidateLabels.Length != available.Length
            || !PendingCandidateLabels.SequenceEqual(available,
                StringComparer.Ordinal))
            throw new InvalidDataException(
                "Controlled Shovel candidates do not match the current eligible relic pool.");

        for (var index = 0; index < Decisions.Length; index++)
        {
            var decision = Decisions[index];
            decision.Validate();
            if (decision.ProviderMode != "controlled-v111"
                || decision.NativeCursorBefore is not null
                || decision.NativeCursorAfter is not null
                || decision.Request.SiteId !=
                    "relic.unowned_random_selection.index_draw"
                || decision.Request.StreamId != "event"
                || decision.Request.SupportCount !=
                    DecisionCandidateLabels[index].Length
                || DecisionCandidateLabels[index].Any(
                    string.IsNullOrWhiteSpace))
                throw new InvalidDataException(
                    "Controlled Shovel decision prefix is malformed.");
        }

        PrototypeStateInvariants.Validate(state);
    }
}

/// <summary>
/// Opt-in controlled boundary for supported stochastic run actions. Ordinary
/// player steps retain their existing provider behavior.
/// </summary>
public static class ControlledRunActionEngineV111
{
    public const string SchemaId = "sts2-v111-controlled-run-action-v1";

    public static RunState BeginShovelDig(RunState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        PrototypeStateInvariants.Validate(source);
        if (source.Phase != RunPhase.Rest
            || source.ControlledRunActionChoice is not null
            || !PrototypeNativeRestSiteRelics.CanDig(source.Player))
            throw new InvalidOperationException(
                "Controlled Shovel dig requires an unsuspended rest-site action.");

        var action = new PrototypeGameEngine().GetLegalActions(source)
            .SingleOrDefault(candidate =>
                candidate.Kind == PrototypeNativeRestSiteActionsV111.Dig)
            ?? throw new InvalidOperationException(
                "Shovel dig is not legal in the current state.");
        using var guard = ControlledChanceGuardV111.Enter();
        using var replay = ControlledEventChoiceReplayV111.Begin([]);
        var result = new PrototypeGameEngine().Step(source, action).State;
        replay.EnsureConsumed();
        if (result.ControlledRunActionChoice is null)
            throw new NotSupportedException(
                "Shovel dig did not reach a supported legacy controlled chance site.");
        PrototypeStateInvariants.Validate(result);
        return result;
    }

    public static RunState Resolve(RunState source, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.ControlledRunActionChoice is null)
            throw new InvalidOperationException(
                "No controlled run-action outcome is pending.");
        return Step(source, GameAction.Create(
            "choose_controlled_run_action_outcome",
            new ChooseControlledEventOutcomePayload(selectedIndex)));
    }

    private static RunState Step(RunState source, GameAction action)
    {
        using var guard = ControlledChanceGuardV111.Enter();
        var result = new PrototypeGameEngine().Step(source, action).State;
        PrototypeStateInvariants.Validate(result);
        return result;
    }
}
