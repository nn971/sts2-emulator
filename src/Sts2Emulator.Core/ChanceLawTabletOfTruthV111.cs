using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    public const string TabletOfTruthConditioningPolicyId =
        "tablet-of-truth-page-eligible-card-v1";

    public static string[] TabletOfTruthSessionSiteIds =>
        ReferenceNatureTabletOfTruthSessionV111.TabletOfTruthSessionSiteIds;

    public static ChanceLawV111 QueryTabletOfTruth(RunState origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var plan = TabletOfTruthChoicePlanBuilderV111.Build(origin);
        var nativeIndexLaw = NativeRngV111.IsCanonicalStreamId(
            plan.Request.StreamId);
        var indexWeights = nativeIndexLaw
            ? NativeNextIntGridWeights(plan.Request.SupportCount)
            : Enumerable.Repeat(1L, plan.Request.SupportCount).ToArray();
        var outcomes = plan.CandidateCardInstanceIds.Select((instanceId, index) =>
            new ChanceLawOutcomeV111(
                "card_instance:" + instanceId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                indexWeights[index]))
            .ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "overgrowth.tablet_of_truth.page_choice",
            plan.ConditioningFingerprint
        });
        return Create(eventId, plan.Request.SiteId,
            plan.Request.OperationKind, plan.Request.OwnerId,
            plan.Request.Ordinal, TabletOfTruthConditioningPolicyId,
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
