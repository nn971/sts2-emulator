using System.Buffers.Binary;
using System.Globalization;

namespace Sts2Emulator.Core;

/// <summary>
/// Replaces only an UNCONSUMED reward RNG stream at a hypothetical
/// combat-to-reward boundary, then executes the ordinary public action.
///
/// Under the explicitly independent initial six-stream research prior,
/// a stream with zero calls has independent uniform initial state, even
/// after observing the other five streams. Repeating this operation with
/// iid uniform 64-bit values provides valid joint reward proposals.
///
/// It is NOT valid for native seeds, a previously consumed reward stream,
/// or arbitrary post-reward conditional rekeying.
/// </summary>
public static class PrototypePristineRewardProposal
{
    public const string SchemaId = "prototype-pristine-reward-branch-v1";
    public const string IndependentRunSeed = "experimental-factorized-prior-v1";

    public static RunState Branch(
        RunState source,
        string publicActionId,
        string independentRewardStateHex)
    {
        if (source.RunSeed != IndependentRunSeed
            || source.Phase != RunPhase.Combat)
        {
            throw new InvalidOperationException(
                "Pristine reward proposals require an experimental "
                + "independent-stream hypothetical combat state.");
        }

        if (independentRewardStateHex is null
            || independentRewardStateHex.Length != 16
            || !ulong.TryParse(
                independentRewardStateHex,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var newInitialState))
        {
            throw new ArgumentException(
                "Reward proposal must be an independent unsigned "
                + "64-bit stream state encoded as 16 hexadecimal characters.");
        }

        var existing = source.Rng.Streams.SingleOrDefault(
            stream => StringComparer.Ordinal.Equals(stream.StreamId, "reward"));
        if (existing is null
            || existing.Codec != PrototypeRng.Codec
            || existing.StateBytes.Length != sizeof(ulong)
            || existing.CallCount != 0)
        {
            throw new InvalidOperationException(
                "Reward RNG stream has already been consumed or is invalid; "
                + "rekeying would corrupt the conditional game law.");
        }

        var environment = new PrototypeAiEnvironment();
        if (!environment.Observe(source).LegalActions.Any(
            action => StringComparer.Ordinal.Equals(action.ActionId, publicActionId)))
        {
            throw new InvalidOperationException(
                "Requested reward-triggering public action is not legal.");
        }

        var candidate = source.Fork();
        var index = Array.FindIndex(
            candidate.Rng.Streams,
            stream => StringComparer.Ordinal.Equals(stream.StreamId, "reward"));
        var bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, newInitialState);
        candidate.Rng.Streams[index] = existing with
        {
            StateBytes = bytes,
            CallCount = 0
        };

        var result = environment.Step(candidate, publicActionId).State;
        if (result.Phase != RunPhase.Reward)
        {
            throw new InvalidOperationException(
                "Chosen public action did not enter the first reward phase; "
                + "reward proposals cannot be applied speculatively.");
        }

        return result;
    }
}
