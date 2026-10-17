namespace Sts2Emulator.Core;

/// <summary>
/// Mechanics boundary and committed event-choice prefix for a suspended
/// post-reward event chance.
/// </summary>
public sealed record PrototypeControlledEventRewardChoiceMechanicsStateV111(
    RunState PartialState,
    string EventId,
    string ChoiceId,
    int DecisionPrefixLength);
