namespace Sts2Emulator.Core;

/// <summary>
/// Shared native player-stream owners used by configured run generation.
/// Requests still flow through the common typed providers and boundary journal.
/// </summary>
internal static class NativePlayerRngV111
{
    public const string ShopsStreamId = "player.shops";
    public const string RewardsStreamId = "player.rewards";

    public static int NextOrdinal(RngBundle streams, string streamId)
    {
        ArgumentNullException.ThrowIfNull(streams);
        if (StochasticProviderScopeV111.GetNextOrdinal(
                streams, streamId) is { } scopedOrdinal)
            return scopedOrdinal;
        return NativeRngV111.ToSnapshot(
            streams.GetStream(streamId)).Counter;
    }

    public static int ChooseIndex(
        RngBundle streams,
        string siteId,
        string streamId,
        string ownerId,
        int count) =>
        ChooseIndexDecision(
            streams, siteId, streamId, ownerId, count).SelectedIndex;

    public static RandomIndexDecisionV111 ChooseIndexDecision(
        RngBundle streams,
        string siteId,
        string streamId,
        string ownerId,
        int count) =>
        NativeRandomChoiceV111.ChooseIndexRange(
            streams, siteId, streamId, ownerId, count,
            NextOrdinal(streams, streamId));

    public static int ChooseBoundedInteger(
        RngBundle streams,
        string siteId,
        string streamId,
        string ownerId,
        int boundedCount) =>
        NativeRandomChoiceV111.ChooseBoundedInteger(
            streams, siteId, streamId, ownerId, boundedCount,
            NextOrdinal(streams, streamId)).SelectedIndex;

    public static float NextUnitFloat(
        RngBundle streams,
        string siteId,
        string streamId,
        string ownerId) =>
        NativeRandomChoiceV111.NextUnitFloat(
            streams, siteId, streamId, ownerId,
            NextOrdinal(streams, streamId)).SelectedValue;
}
