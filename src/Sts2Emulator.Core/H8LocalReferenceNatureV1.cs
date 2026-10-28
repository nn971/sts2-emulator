using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Sts2Emulator.Core;

/// <summary>
/// Local reference Nature sampler for a source-visible, unresolved H8 event.
///
/// Reference Nature can be executed inside the single owning emulator process
/// instead of exposing a Chance Law, waiting for Python, and receiving its
/// selected outcome over two JSONL round trips per decision. External learned
/// and scripted Nature still use the explicit H8 provider boundary as before.
///
/// This is a versioned reference provider with stable, event-keyed SHA256
/// randomness, not a claim of reproducing the native game's RNG state or the
/// Python SeededLawSampler's MT19937 draws. It samples the SOURCE_VERIFIED
/// 53-bit NextDouble grid for enemy intents, and uniform eligible physical
/// card instances for ordinary draws. The H8 journal records every actual
/// choice so even this provider can be replayed without knowing its seed.
/// </summary>
internal static class H8LocalReferenceNatureV1
{
    internal const string ProviderId = "h8-local-reference-v1";
    private const ulong NativeDoubleGrid = 1UL << 53;

    /// <summary>
    /// Uniform bounded integer using counter-based SHA256 rejection sampling.
    /// The event identity is independent of which worker executes it.
    /// </summary>
    private static ulong Ticket(
        long seed, string eventId, string siteId, ulong limit)
    {
        if (limit == 0)
            throw new InvalidDataException("Empty local Nature support.");
        if (string.IsNullOrWhiteSpace(eventId) ||
            string.IsNullOrWhiteSpace(siteId))
            throw new InvalidDataException("Missing Nature event identity.");

        // For unsigned 64-bit samples, reject the incomplete initial
        // remainder so the modulo reduction is exactly unbiased.
        var threshold = unchecked(0UL - limit) % limit;
        for (ulong attempt = 0; ; attempt++)
        {
            var data = Encoding.UTF8.GetBytes(
                "sts2-h8-local-reference-v1|" +
                seed.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "|" + siteId + "|" + eventId + "|" +
                attempt.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var hash = SHA256.HashData(data);
            var sample = BinaryPrimitives.ReadUInt64LittleEndian(hash);
            if (sample >= threshold)
                return sample % limit;
        }
    }

    internal static int UniformIndex(
        long seed, string eventId, string siteId, int count)
    {
        if (count <= 0)
            throw new InvalidDataException("No eligible physical draw cards.");
        return checked((int)Ticket(seed, eventId, siteId, (ulong)count));
    }

    /// <summary>
    /// Draw an event-keyed random-target index with the source-verified
    /// NativeNextInt 53-bit grid masses. Ordinary physical card draws still
    /// use their separate uniform reference distribution.
    /// </summary>
    internal static int NativeTargetIndex(
        long seed, string eventId, string siteId, int count)
    {
        var weights = ChanceLawEngineV111.NativeNextIntGridWeights(count);
        var ticket = Ticket(seed, eventId, siteId, NativeDoubleGrid);
        ulong cumulative = 0;
        for (var i = 0; i < weights.Length; i++)
        {
            cumulative = checked(cumulative + (ulong)weights[i]);
            if (ticket < cumulative)
                return i;
        }
        throw new InvalidDataException(
            "Source-verified random-target grid does not cover sampled ticket.");
    }

    /// <summary>
    /// The NativeNextDouble 53-bit bucket boundary is the exact integer
    /// boundary used by the existing v111 source-verified intent law. The
    /// formula avoids rounding native weights through binary64.
    /// </summary>
    private static ulong NativeGridBoundary(long boundary, long total)
    {
        if (boundary == 0)
            return 0;
        if (boundary <= 0 || total <= 0 || boundary > total)
            throw new InvalidDataException("Invalid weighted intent boundary.");
        var unsigned = checked((ulong)boundary);
        var exponent = BitOperations.Log2(unsigned);
        var powerOfTwo = (unsigned & (unsigned - 1)) == 0;
        var shift = 53 - exponent + (powerOfTwo ? 1 : 0);
        if (shift < 0)
            throw new NotSupportedException(
                "Intent source weight exceeds native 53-bit grid range.");
        var scale = BigInteger.One << shift;
        var numerator = ((BigInteger)boundary * scale - 1) * NativeDoubleGrid;
        var denominator = scale * total;
        return checked((ulong)((numerator + denominator - 1) / denominator));
    }

    internal static int WeightedIntentIndex(
        long seed, string eventId, string siteId,
        IReadOnlyList<long> rawWeights)
    {
        if (rawWeights.Count == 0)
            throw new InvalidDataException("Empty weighted intent support.");
        long total = 0;
        foreach (var weight in rawWeights)
        {
            if (weight <= 0)
                throw new InvalidDataException("Nonpositive intent weight.");
            total = checked(total + weight);
        }
        var ticket = Ticket(seed, eventId, siteId, NativeDoubleGrid);
        ulong previous = 0;
        long count = 0;
        for (var i = 0; i < rawWeights.Count; i++)
        {
            count = checked(count + rawWeights[i]);
            var stop = NativeGridBoundary(count, total);
            if (stop <= previous)
                throw new InvalidDataException(
                    "Source-verified intent has empty native-grid support.");
            if (ticket < stop)
                return i;
            previous = stop;
        }
        throw new InvalidDataException(
            "Source-verified intent grid does not cover native ticket.");
    }
}
