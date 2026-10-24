using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    public const string NeowOfferEventId = "act1.neow.initial_options";
    public const string NeowOfferConditioningPolicyId =
        "neow-sequential-offer-generator-v1";

    public static string[] NeowOfferSessionSiteIds =>
        ReferenceNatureNeowOfferSessionV111.NeowOfferSessionSiteIds;

    public static ChanceLawV111 QueryNeowOffer(
        string seed, int ascension, int ordinal, int[]? priorIndices = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        if (ascension < 0 || ordinal < 0)
            throw new ArgumentOutOfRangeException(
                ascension < 0 ? nameof(ascension) : nameof(ordinal));
        priorIndices ??= [];
        if (priorIndices.Length != ordinal)
            throw new ArgumentException(
                "Neow offer queries require one prior index per earlier decision.",
                nameof(priorIndices));
        var plan = NeowOfferDecisionPlanBuilderV111.Build(
            seed, ascension, priorIndices);
        var step = plan.PendingStep ?? throw new ArgumentOutOfRangeException(
            nameof(ordinal), "Neow offer generation is already complete.");
        if (!ReferenceNatureNeowOfferSessionV111
                .NeowOfferSessionSiteIds.Contains(
                    step.Request.SiteId, StringComparer.Ordinal))
            throw new NotSupportedException(
                $"Neow offer query does not support site '{step.Request.SiteId}'.");

        if (step.OutcomeIds.Length != step.Request.SupportCount)
            throw new InvalidDataException(
                "Neow offer labels differ from the Native bounded-index support.");
        var weights = NativeNextIntGridWeights(step.Request.SupportCount);
        var outcomes = step.OutcomeIds.Select((id, index) =>
            new ChanceLawOutcomeV111(id, weights[index])).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = NeowOfferEventId,
            seed,
            ascension,
            ordinal,
            step.ConditioningFingerprint
        });
        return Create(eventId, step.Request.SiteId,
            step.Request.OperationKind, step.Request.OwnerId,
            step.Request.Ordinal, NeowOfferConditioningPolicyId,
            step.ConditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    public static ChanceLawV111 QueryNeowOffer(
        RunState origin, int ordinal, int[]? priorIndices = null)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (string.IsNullOrWhiteSpace(origin.RunSeed) ||
            CanonicalJson.Sha256(PrototypeGameFactory.Create(
                origin.RunSeed, origin.Ascension)) !=
            CanonicalJson.Sha256(origin))
            throw new InvalidOperationException(
                "Neow offer queries require a pristine run-start state before offer generation.");
        return QueryNeowOffer(origin.RunSeed, origin.Ascension,
            ordinal, priorIndices);
    }
}
