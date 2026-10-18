namespace Sts2Emulator.Core;

/// <summary>
/// Small request-construction helpers for source-pinned native run sites.
/// They preserve the existing provider interfaces and keep mechanics
/// responsible for candidate order and occurrence identity. SiteIds must be
/// registered Chance Law query sites; callers retain responsibility for the
/// operation, support, stream, owner, and source occurrence.
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
        ValidateSiteId(siteId, "index");
        if (candidateCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(candidateCount));
        return StochasticProviderScopeV111.ResolveIndex(
            provider,
            new RandomIndexRequestV111(
                siteId, streamId, ownerId, ordinal,
                Enumerable.Range(0, candidateCount).ToArray()),
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
        ValidateSiteId(siteId, "bounded-integer");
        if (boundedCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(boundedCount));
        return StochasticProviderScopeV111.ResolveIndex(
            provider,
            new RandomIndexRequestV111(
                siteId, streamId, ownerId, ordinal, [],
                OperationKind: "bounded_integer",
                BoundedCount: boundedCount),
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
        ValidateSiteId(siteId, "unit-float");
        return StochasticProviderScopeV111.ResolveUnitFloat(
            provider,
            new RandomUnitFloatRequestV111(
                siteId, streamId, ownerId, ordinal),
            streams);
    }

    private static void ValidateSiteId(string siteId, string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (!StochasticSiteRegistryV111.IsChanceLawQuerySite(siteId))
            throw new NotSupportedException(
                $"Native random choice does not support {operation} site " +
                $"'{siteId}'.");
    }
}
