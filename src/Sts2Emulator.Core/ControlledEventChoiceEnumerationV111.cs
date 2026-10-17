namespace Sts2Emulator.Core;

public sealed record ControlledEventChoiceEnumeratedOutcomeV111(
    int SelectedIndex,
    string CandidateLabel,
    RunState Child);

public sealed record ControlledEventChoiceEnumerationResultV111(
    string SchemaId,
    string EventId,
    string ChoiceId,
    ControlledEventChoiceEnumeratedOutcomeV111[] Outcomes);

/// <summary>
/// Resolves each index in one small pending event support against an
/// independent ordinary run state. Follow-on pending draws remain pending.
/// </summary>
public static class ControlledEventChoiceEnumeratorV111
{
    public const string SchemaId =
        "sts2-v111-controlled-event-enumeration-v1";
    public const int MaximumSupportedOutcomes = 256;

    public static ControlledEventChoiceEnumerationResultV111 Enumerate(
        RunState current,
        string fingerprint,
        int maximumOutcomes = 32)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        if (maximumOutcomes is < 1 or > MaximumSupportedOutcomes)
            throw new ArgumentOutOfRangeException(
                nameof(maximumOutcomes),
                $"Enumeration cap must be between 1 and {MaximumSupportedOutcomes}.");

        PrototypeStateInvariants.Validate(current);
        if (!StringComparer.Ordinal.Equals(
                CanonicalJson.Sha256(current), fingerprint))
            throw new InvalidOperationException(
                "Stale controlled event-choice enumeration fingerprint.");
        var continuation = current.World?.Event?.ControlledChoice
            ?? throw new InvalidOperationException(
                "No controlled event outcome is pending to enumerate.");
        var supportCount = continuation.PendingRequest.SupportCount;
        if (supportCount == 0)
            throw new InvalidDataException(
                "A controlled event choice cannot have empty support.");
        if (supportCount > maximumOutcomes)
            throw new InvalidOperationException(
                $"Pending support has {supportCount} outcomes, exceeding " +
                $"the requested enumeration cap of {maximumOutcomes}.");

        var labels = continuation.PendingCandidateLabels!;
        var outcomes = Enumerable.Range(0, supportCount)
            .Select(index => new ControlledEventChoiceEnumeratedOutcomeV111(
                index,
                labels[index],
                ControlledEventChoiceEngineV111.Resolve(current, index)))
            .ToArray();
        return new ControlledEventChoiceEnumerationResultV111(
            SchemaId,
            continuation.EventId,
            continuation.ChoiceId,
            outcomes);
    }
}
