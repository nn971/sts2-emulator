namespace Sts2Emulator.Core;

/// <summary>
/// Small request-construction helpers for source-pinned native run sites.
/// They preserve the existing provider interfaces and keep mechanics
/// responsible for candidate order and occurrence identity. Each helper
/// validates against the Native provider's registered operation family;
/// direct Chance Law query coverage is negotiated separately.
/// </summary>
public static class NativeRandomChoiceV111
{
    public static RandomIndexDecisionV111 ChooseIndexRange(
        RngBundle streams,
        string siteId,
        string streamId,
        string ownerId,
        int candidateCount,
        int ordinal,
        IRandomIndexProviderV111? provider = null)
    {
        if (candidateCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(candidateCount));
        var request = new RandomIndexRequestV111(
            siteId, streamId, ownerId, ordinal,
            Enumerable.Range(0, candidateCount).ToArray());
        request.ValidateForNativeProvider();
        return StochasticProviderScopeV111.ResolveIndex(
            provider,
            request,
            streams);
    }

    public static RandomIndexDecisionV111 ChooseBoundedInteger(
        RngBundle streams,
        string siteId,
        string streamId,
        string ownerId,
        int boundedCount,
        int ordinal,
        IRandomIndexProviderV111? provider = null)
    {
        if (boundedCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(boundedCount));
        var request = new RandomIndexRequestV111(
            siteId, streamId, ownerId, ordinal, [],
            OperationKind: "bounded_integer",
            BoundedCount: boundedCount);
        request.ValidateForNativeProvider();
        return StochasticProviderScopeV111.ResolveIndex(
            provider,
            request,
            streams);
    }

    public static RandomUnitFloatDecisionV111 NextUnitFloat(
        RngBundle streams,
        string siteId,
        string streamId,
        string ownerId,
        int ordinal,
        IRandomUnitFloatProviderV111? provider = null)
    {
        var request = new RandomUnitFloatRequestV111(
            siteId, streamId, ownerId, ordinal);
        request.Validate();
        return StochasticProviderScopeV111.ResolveUnitFloat(
            provider,
            request,
            streams);
    }
}
