using System.Text.Json;

namespace Sts2Emulator.Core;

public sealed record PrototypeAiFairActionV111(
    string ActionId, string Kind, string PayloadJson);

/// <summary>
/// One versioned fair-player frame shared by ordinary, controlled, semantic,
/// and native-policy combat handles. It contains only the existing public
/// observation schema and its legal actions.
/// </summary>
public sealed record PrototypeAiFairObservationEnvelopeV111(
    string SchemaId,
    string ViewKind,
    string Handle,
    string PolicyId,
    string ObservationSchemaId,
    string ObservationHash,
    string PayloadJson,
    PrototypeAiFairActionV111[] Actions,
    bool Terminal)
{
    private PrototypeAiFairActionV111[]? _actions = CloneActions(Actions);

    public const string CurrentSchemaId =
        "sts2-v111-fair-observation-envelope-v1";
    public const string FairPolicyId = "prototype-fair-v0";

    private static readonly string[] SupportedViewKinds =
    [
        "run_state", "controlled_combat", "semantic_combat",
        "semantic_reference", "semantic_native_policy"
    ];

    public PrototypeAiFairActionV111[] Actions
    {
        get => CloneActions(_actions)!;
        init => _actions = CloneActions(value);
    }

    public static string[] ViewKinds => (string[])SupportedViewKinds.Clone();

    public static PrototypeAiFairObservationEnvelopeV111 Create(
        string viewKind, string handle, string policyId,
        PrototypeAiFrame frame, bool terminal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId);
        ArgumentNullException.ThrowIfNull(frame);
        var envelope = new PrototypeAiFairObservationEnvelopeV111(
            CurrentSchemaId, viewKind, handle, policyId, frame.SchemaId,
            frame.ObservationHash,
            CanonicalJson.Serialize(frame.Observation),
            frame.LegalActions.Select(action =>
                new PrototypeAiFairActionV111(
                    action.ActionId, action.Kind,
                    CanonicalJson.Serialize(action.Payload))).ToArray(),
            terminal);
        envelope.Validate();
        return envelope;
    }

    public PrototypeAiFairObservationEnvelopeV111 Fork() => this with
    {
        Actions = CloneActions(_actions)!
    };

    public void Validate()
    {
        var actions = Actions;
        if (SchemaId != CurrentSchemaId ||
            !SupportedViewKinds.Contains(ViewKind, StringComparer.Ordinal) ||
            string.IsNullOrWhiteSpace(Handle) ||
            PolicyId != FairPolicyId ||
            ObservationSchemaId != PrototypeAiEnvironment.SchemaId ||
            string.IsNullOrWhiteSpace(ObservationHash) ||
            string.IsNullOrWhiteSpace(PayloadJson) || actions is null ||
            actions.Any(action => action is null ||
                string.IsNullOrWhiteSpace(action.ActionId) ||
                string.IsNullOrWhiteSpace(action.Kind) ||
                string.IsNullOrWhiteSpace(action.PayloadJson)) ||
            actions.Select(action => action.ActionId)
                .Distinct(StringComparer.Ordinal).Count() != actions.Length)
            throw new InvalidDataException(
                "Invalid fair-player observation envelope.");

        using var payload = JsonDocument.Parse(PayloadJson);
        if (payload.RootElement.ValueKind != JsonValueKind.Object ||
            CanonicalJson.Sha256(payload.RootElement) != ObservationHash)
            throw new InvalidDataException(
                "Fair-player observation hash differs from its payload.");
    }

    private static PrototypeAiFairActionV111[]? CloneActions(
        PrototypeAiFairActionV111[]? actions) => actions?.Select(action =>
            action is null ? null! : action with
            {
                PayloadJson = action.PayloadJson
            }).ToArray();
}
