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
        JsonElement pendingEvent)
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

        var metadata = new Dictionary<string, JsonElement>(
            StringComparer.Ordinal);
        foreach (var property in pendingEvent.EnumerateObject())
            if (property.Name is not "chanceLaw")
            {
                var sanitized = StripProviderMetadata(property.Value);
                if (sanitized is { } value)
                    metadata.Add(property.Name, value);
            }

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
            ContainsPrivateProviderMetadata(EventMetadata) ||
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

    private static JsonElement? StripProviderMetadata(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var properties = new Dictionary<string, JsonElement>(
                StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (IsPrivateProviderMetadataName(property.Name))
                    continue;
                var sanitized = StripProviderMetadata(property.Value);
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
                var sanitized = StripProviderMetadata(item);
                if (sanitized is { } child)
                    items.Add(child);
            }
            return JsonSerializer.SerializeToElement(items, WebJsonOptions);
        }

        return value.Clone();
    }

    private static bool ContainsPrivateProviderMetadata(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
            return value.EnumerateObject().Any(property =>
                IsPrivateProviderMetadataName(property.Name) ||
                ContainsPrivateProviderMetadata(property.Value));
        return value.ValueKind == JsonValueKind.Array &&
            value.EnumerateArray().Any(ContainsPrivateProviderMetadata);
    }

    private static bool IsPrivateProviderMetadataName(string name)
    {
        var normalized = new string(name.Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant).ToArray());
        return normalized.Contains("stream", StringComparison.Ordinal) ||
            normalized.Contains("seed", StringComparison.Ordinal) ||
            normalized.Contains("cursor", StringComparison.Ordinal) ||
            normalized.Contains("rng", StringComparison.Ordinal) ||
            normalized.Contains("randomstate", StringComparison.Ordinal) ||
            normalized.Contains("providerstate", StringComparison.Ordinal) ||
            normalized.Contains("callcount", StringComparison.Ordinal);
    }

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
