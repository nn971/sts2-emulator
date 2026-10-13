using System.Text.Json;
using Sts2Emulator.Core;
using Sts2Emulator.Trace;

namespace Sts2ReferenceMod.Capture;

/// <summary>
/// Implement against audited native game APIs. All methods run on the Godot main
/// thread at a proven input-ready checkpoint; no method may draw RNG.
/// </summary>
public interface INativeCaptureAdapter
{
    string Phase { get; }
    bool IsAtStablePlayerChoice { get; }
    string SemanticBoundaryKey { get; }
    JsonElement CaptureCanonicalState();
    IReadOnlyList<GameAction> GetCanonicalLegalActions();
    CaptureCoverage GetCoverage();
    string ObservationHash { get; }
}
