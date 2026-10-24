using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    public const string TrashHeapConditioningPolicyId =
        "trash-heap-single-choice-source-pool-v1";

    public static string[] TrashHeapSessionSiteIds =>
        ReferenceNatureTrashHeapSessionV111.TrashHeapSessionSiteIds;

    public static ChanceLawV111 QueryTrashHeap(
        RunState origin, string siteId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var plan = TrashHeapChoicePlanBuilderV111.Build(origin, siteId);
        var nativeIndexLaw = NativeRngV111.IsCanonicalStreamId(
            plan.Request.StreamId);
        var indexWeights = nativeIndexLaw
            ? NativeNextIntGridWeights(plan.Request.SupportCount)
            : Enumerable.Repeat(1L, plan.Request.SupportCount).ToArray();
        var outcomes = plan.OutcomeIds.Select((id, index) =>
            new ChanceLawOutcomeV111(
                $"trash_heap_reward:{id}:index:{index}",
                indexWeights[index])).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "underdocks.trash_heap.choice",
            siteId,
            plan.ConditioningFingerprint
        });
        return Create(eventId, plan.Request.SiteId,
            plan.Request.OperationKind, plan.Request.OwnerId,
            plan.Request.Ordinal, TrashHeapConditioningPolicyId,
            plan.ConditioningFingerprint,
            nativeIndexLaw
                ? NativeBoundedIndexSourceVerifiedProvenance
                : "uniform-saved-index-support-unverified",
            outcomes,
            nativeIndexLaw
                ? ChanceLawV111.SourceVerifiedStatus
                : ChanceLawV111.UnverifiedStatus);
    }
}
