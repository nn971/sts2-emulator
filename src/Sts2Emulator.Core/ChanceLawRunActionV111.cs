namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    public static ChanceLawV111 QueryControlledRunAction(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var continuation = run.Stochastic.ControlledRunActionChoice ??
            throw new InvalidOperationException(
                "No controlled run-action chance is pending.");
        continuation.Validate();
        var boundaryFingerprint = ControlledRunActionBoundaryFingerprint(run);
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ControlledRunActionEngineV111.SchemaId,
            boundaryFingerprint
        });
        return QueryRandomIndex(eventId, continuation.PendingRequest);
    }

    /// <summary>Fingerprint the pending controlled action and request without
    /// hashing the enclosing RunState (which contains private provider state).</summary>
    public static string ControlledRunActionBoundaryFingerprint(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var continuation = run.Stochastic.ControlledRunActionChoice ??
            throw new InvalidOperationException(
                "No controlled run-action chance is pending.");
        continuation.Validate();
        var boundary = new
        {
            schemaId = ControlledRunActionEngineV111.SchemaId,
            continuation.ActionId,
            continuation.MapNodeId,
            request = continuation.PendingRequest,
            candidateLabels = continuation.PendingCandidateLabels
        };
        return continuation.RunAction is null
            ? CanonicalJson.Sha256(boundary)
            : CanonicalJson.Sha256(new
            {
                boundary,
                actionFingerprint = PrototypeAiEnvironment.StableActionId(
                    continuation.RunAction)
            });
    }
}
