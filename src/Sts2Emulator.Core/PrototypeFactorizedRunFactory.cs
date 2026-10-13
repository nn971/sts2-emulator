namespace Sts2Emulator.Core;

/// <summary>
/// An explicitly alternative probability model: six independent, caller-chosen
/// initial SplitMix64 stream states, not a native or single-seed STS2 run.
/// The RunStart action consumes these streams using ordinary engine mechanics.
/// The states must be chosen only by a search-side hypothetical sampler.
/// </summary>
public static class PrototypeFactorizedRunFactory
{
    public const string SchemaId = "prototype-independent-initial-streams-v1";

    public static RunState Create(
        IReadOnlyDictionary<string, string> initialStateHex,
        int ascension = 0)
    {
        var streams = PrototypeRng.CreateFactorizedInitialBundle(initialStateHex);
        var original = PrototypeGameFactory.Create(
            "experimental-factorized-prior-v1", ascension);
        return original with { Rng = streams };
    }
}
