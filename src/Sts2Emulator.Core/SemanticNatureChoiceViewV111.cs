using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Versioned Nature-only projection of one pending semantic chance boundary.
/// It carries the validated law and event metadata, but no engine snapshot,
/// native cursor, selected outcome, or fair-player observation.
/// </summary>
public sealed record SemanticNatureChoiceViewV111(
    string SchemaId,
    string EventKind,
    string EventId,
    string SiteId,
    string OperationKind,
    string EventSupportFingerprint,
    string LawSupportFingerprint,
    string ConditioningPolicyId,
    string ConditioningFingerprint,
    string? BoundaryFingerprint,
    string ProbabilityStatus,
    string ProbabilityProvenance,
    ChanceLawV111 ChanceLaw,
    JsonElement EventMetadata)
{
    public const string CurrentSchemaId =
        "sts2-v111-semantic-nature-choice-view-v2";

    private static readonly JsonSerializerOptions WebJsonOptions =
        new(JsonSerializerDefaults.Web);

    public static SemanticNatureChoiceViewV111 Create(
        JsonElement pendingEvent) => Create(pendingEvent, null);

    public static SemanticNatureChoiceViewV111 Create(
        JsonElement pendingEvent,
        IReadOnlyList<string>? semanticOutcomeIds)
    {
        if (pendingEvent.ValueKind != JsonValueKind.Object ||
            !pendingEvent.TryGetProperty("chanceLaw", out var lawElement))
            throw new InvalidDataException(
                "Nature-choice view requires a pending event with a Chance Law.");
        var law = lawElement.Deserialize<ChanceLawV111>(WebJsonOptions) ??
            throw new InvalidDataException(
                "Nature-choice view contains a null Chance Law.");
        law.Validate();

        string eventKind = RequiredString(pendingEvent, "kind");
        string eventId = OptionalString(pendingEvent, "eventId") ?? law.EventId;
        string siteId = OptionalString(pendingEvent, "siteId") ?? law.SiteId;
        string operationKind =
            OptionalString(pendingEvent, "operationKind") ?? law.OperationKind;
        string eventSupportFingerprint =
            OptionalString(pendingEvent, "supportFingerprint") ??
            law.SupportFingerprint;
        string? boundaryFingerprint =
            OptionalString(pendingEvent, "boundaryFingerprint");

        bool rawRunStateEnvelope = LooksLikeRawRunState(pendingEvent);
        var metadata = new Dictionary<string, JsonElement>(
            StringComparer.Ordinal);
        foreach (var property in pendingEvent.EnumerateObject())
        {
            if (property.Name is "chanceLaw" ||
                IsForbiddenMetadataName(property.Name) ||
                rawRunStateEnvelope &&
                !IsNatureEnvelopeMetadataName(property.Name))
                continue;
            // ChanceLaw owns probability provenance; event metadata can keep
            // a conservative compatibility status from an older snapshot.
            if (property.Name is "probabilityStatus" &&
                property.Value.ValueKind == JsonValueKind.String &&
                property.Value.GetString() != law.ProbabilityStatus)
                continue;

            var sanitized = StripForbiddenMetadata(property.Value);
            if (sanitized is { } value)
                metadata.Add(property.Name, value);
        }
        if (semanticOutcomeIds is not null)
            metadata["semanticOutcomeIds"] =
                JsonSerializer.SerializeToElement(
                    semanticOutcomeIds, WebJsonOptions);

        var result = new SemanticNatureChoiceViewV111(
            CurrentSchemaId, eventKind, eventId, siteId, operationKind,
            eventSupportFingerprint, law.SupportFingerprint,
            law.ConditioningPolicyId, law.ConditioningFingerprint,
            boundaryFingerprint, law.ProbabilityStatus,
            law.ProbabilityProvenance, law.Fork(),
            JsonSerializer.SerializeToElement(metadata, WebJsonOptions));
        result.Validate();
        return result;
    }

    public SemanticNatureChoiceViewV111 Fork() => this with
    {
        ChanceLaw = ChanceLaw.Fork(),
        EventMetadata = EventMetadata.Clone()
    };

    public void Validate()
    {
        ChanceLaw.Validate();
        if (SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(EventKind) ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(SiteId) ||
            string.IsNullOrWhiteSpace(OperationKind) ||
            string.IsNullOrWhiteSpace(EventSupportFingerprint) ||
            string.IsNullOrWhiteSpace(LawSupportFingerprint) ||
            string.IsNullOrWhiteSpace(ConditioningPolicyId) ||
            string.IsNullOrWhiteSpace(ConditioningFingerprint) ||
            ProbabilityStatus != ChanceLaw.ProbabilityStatus ||
            ProbabilityProvenance != ChanceLaw.ProbabilityProvenance ||
            EventId != ChanceLaw.EventId || SiteId != ChanceLaw.SiteId ||
            OperationKind != ChanceLaw.OperationKind ||
            LawSupportFingerprint != ChanceLaw.SupportFingerprint ||
            ConditioningPolicyId != ChanceLaw.ConditioningPolicyId ||
            ConditioningFingerprint != ChanceLaw.ConditioningFingerprint ||
            EventMetadata.ValueKind != JsonValueKind.Object ||
            EventMetadata.TryGetProperty("chanceLaw", out _) ||
            !HasValidSemanticOutcomeIds(EventMetadata, ChanceLaw.SupportCount) ||
            ContainsForbiddenMetadata(EventMetadata) ||
            OptionalString(EventMetadata, "kind") is { } metadataKind &&
                metadataKind != EventKind ||
            OptionalString(EventMetadata, "eventId") is { } metadataEventId &&
                metadataEventId != EventId ||
            OptionalString(EventMetadata, "siteId") is { } metadataSiteId &&
                metadataSiteId != SiteId ||
            OptionalString(EventMetadata, "operationKind") is
                { } metadataOperationKind &&
                metadataOperationKind != OperationKind ||
            OptionalString(EventMetadata, "supportFingerprint") is
                { } metadataSupportFingerprint &&
                metadataSupportFingerprint != EventSupportFingerprint ||
            OptionalString(EventMetadata, "boundaryFingerprint") is
                { } metadataBoundaryFingerprint &&
                metadataBoundaryFingerprint != BoundaryFingerprint ||
            OptionalString(EventMetadata, "probabilityStatus") is
                { } metadataProbabilityStatus &&
                metadataProbabilityStatus != ProbabilityStatus)
            throw new InvalidDataException(
                "Invalid semantic Nature-choice view envelope.");
    }

    private static JsonElement? StripForbiddenMetadata(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (LooksLikeRawRunState(value))
                return null;
            var properties = new Dictionary<string, JsonElement>(
                StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (IsForbiddenMetadataName(property.Name))
                    continue;
                var sanitized = StripForbiddenMetadata(property.Value);
                if (sanitized is { } child)
                    properties.Add(property.Name, child);
            }
            return JsonSerializer.SerializeToElement(
                properties, WebJsonOptions);
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            var items = new List<JsonElement>();
            foreach (var item in value.EnumerateArray())
            {
                var sanitized = StripForbiddenMetadata(item);
                if (sanitized is { } child)
                    items.Add(child);
            }
            return JsonSerializer.SerializeToElement(items, WebJsonOptions);
        }

        return value.Clone();
    }

    private static bool HasValidSemanticOutcomeIds(
        JsonElement metadata, int supportCount)
    {
        if (!metadata.TryGetProperty("semanticOutcomeIds", out var ids))
            return true;
        if (ids.ValueKind != JsonValueKind.Array ||
            ids.GetArrayLength() != supportCount)
            return false;
        var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids.EnumerateArray())
            if (id.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(id.GetString()) ||
                !uniqueIds.Add(id.GetString()!))
                return false;
        return true;
    }

    private static bool ContainsForbiddenMetadata(JsonElement value)
    {
        if (LooksLikeRawRunState(value)) return true;
        if (value.ValueKind == JsonValueKind.Object)
            return value.EnumerateObject().Any(property =>
                IsForbiddenMetadataName(property.Name) ||
                LooksLikeRawRunState(property.Value) ||
                ContainsForbiddenMetadata(property.Value));
        return value.ValueKind == JsonValueKind.Array &&
            value.EnumerateArray().Any(ContainsForbiddenMetadata);
    }

    private static bool IsForbiddenMetadataName(string name)
    {
        var normalized = new string(name.Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant).ToArray());
        return IsPrivateProviderMetadataName(normalized) ||
            normalized is "run" or "snapshot" or "rawstate" or
                "checkpoint" or "originrun" or "currentrun" or
                "partialrun" or "partialstate" or "runcheckpoint" or
                "selectedoutcome" or "selectedoutcomeindex" or
                "selectedindex" or "selectedcardinstanceid" or
                "selectedenemyinstanceid" or "selectedactionid" or
                "selectedposition" or "selectedcost" or "selectedcase" or
                "selectedticket" or "selectedmodelid" ||
            normalized.Contains("runstate", StringComparison.Ordinal) ||
            normalized.Contains("runsnapshot", StringComparison.Ordinal);
    }

    private static bool IsPrivateProviderMetadataName(string normalized) =>
        normalized.Contains("stream", StringComparison.Ordinal) ||
            normalized.Contains("seed", StringComparison.Ordinal) ||
            normalized.Contains("cursor", StringComparison.Ordinal) ||
            normalized.Contains("rng", StringComparison.Ordinal) ||
            normalized.Contains("randomstate", StringComparison.Ordinal) ||
            normalized.Contains("providerstate", StringComparison.Ordinal) ||
            normalized.Contains("callcount", StringComparison.Ordinal);

    private static bool LooksLikeRawRunState(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) return false;
        var names = value.EnumerateObject().Select(property =>
            new string(property.Name.Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant).ToArray())).ToHashSet(
                    StringComparer.Ordinal);
        return names.Contains("player") && names.Contains("world") &&
            (names.Contains("phase") || names.Contains("runid") ||
             names.Contains("runseed"));
    }

    private static bool IsNatureEnvelopeMetadataName(string name) =>
        name is "kind" or "eventId" or "siteId" or "operationKind" or
            "supportFingerprint" or "boundaryFingerprint" or
            "probabilityStatus";

    private static string RequiredString(
        JsonElement element, string propertyName) =>
        OptionalString(element, propertyName) ?? throw new InvalidDataException(
            $"Nature-choice event is missing {propertyName}.");

    private static string? OptionalString(
        JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
