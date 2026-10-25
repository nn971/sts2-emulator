namespace Sts2Emulator.Core;

/// <summary>
/// Shared immutable request projection for suspended controlled chance
/// boundaries. Context owners supply their boundary fingerprint; this owner
/// protects the request and labels and derives the stable Chance Law event ID.
/// </summary>
internal sealed class ControlledChancePendingBoundaryV111
{
    private readonly string[] _candidateLabels;

    public RandomIndexRequestV111 Request { get; }
    public string[] CandidateLabels => (string[])_candidateLabels.Clone();
    public string BoundaryFingerprint { get; }
    public string ChanceEventId { get; }

    private ControlledChancePendingBoundaryV111(
        RandomIndexRequestV111 request, string[] candidateLabels,
        string boundaryFingerprint, string chanceEventId)
    {
        Request = request with
        {
            OrderedCandidates = request.OrderedCandidates
        };
        _candidateLabels = (string[])candidateLabels.Clone();
        BoundaryFingerprint = boundaryFingerprint;
        ChanceEventId = chanceEventId;
    }

    public static ControlledChancePendingBoundaryV111 Create(
        string schemaId, RandomIndexRequestV111 request,
        string[] candidateLabels, string boundaryFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateLabels);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        var chanceEventId = CanonicalJson.Sha256(new
        {
            schemaId,
            boundaryFingerprint
        });
        return new ControlledChancePendingBoundaryV111(
            request, candidateLabels, boundaryFingerprint, chanceEventId);
    }
}
