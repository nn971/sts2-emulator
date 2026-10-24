using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    public const string TrialConditioningPolicyId =
        "glory-trial-case-choice-v1";

    public static string[] TrialSessionSiteIds =>
        ReferenceNatureTrialSessionV111.TrialSessionSiteIds;

    public static ChanceLawV111 QueryTrial(RunState origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var plan = TrialCaseChoicePlanBuilderV111.Build(origin);
        var nativeIndexLaw = NativeRngV111.IsCanonicalStreamId(
            plan.Request.StreamId);
        var indexWeights = nativeIndexLaw
            ? NativeNextIntGridWeights(plan.Request.BoundedCount!.Value)
            : Enumerable.Repeat(1L, plan.Request.BoundedCount!.Value)
                .ToArray();
        var outcomes = plan.OutcomeIds.Select((outcomeId, index) =>
            new ChanceLawOutcomeV111(outcomeId, indexWeights[index])).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "glory.trial.case_choice",
            plan.ConditioningFingerprint
        });
        return Create(eventId, plan.Request.SiteId,
            plan.Request.OperationKind, plan.Request.OwnerId,
            plan.Request.Ordinal, TrialConditioningPolicyId,
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
