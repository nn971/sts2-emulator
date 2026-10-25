namespace Sts2Emulator.Core;

public static class SemanticChanceEventContractV111
{
    public const string SchemaId = "sts2-v111-semantic-chance-event-v1";
}

/// <summary>
/// Shared description of a mechanic-level stochastic event. Semantic event
/// schemas carry support and conditioning metadata independently of the
/// source provider's primitive stream/cursor transcript.
/// </summary>
public interface ISemanticChanceEventV111
{
    string SchemaId { get; }
    string EventId { get; }
    string SiteId { get; }
    string OperationKind { get; }
    string StreamId { get; }
    string OwnerId { get; }
    int Ordinal { get; }
    string SupportFingerprint { get; }
    string ProbabilityStatus { get; }
}

/// <summary>
/// The single active index-valued semantic event exposed to providers. The
/// event remains owned by its typed mechanic; this envelope only fixes its
/// dispatch precedence and checked support size.
/// </summary>
internal sealed record SemanticPendingIndexChanceEventV111(
    ISemanticChanceEventV111 Event, int SupportCount)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Event);
        if (string.IsNullOrWhiteSpace(Event.EventId) || SupportCount < 1)
            throw new InvalidDataException(
                "Invalid pending semantic index chance event.");
    }
}
