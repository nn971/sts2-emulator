using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Sts2Emulator.Core;

public static class PrototypeRng
{
    public const string Codec = "prototype-splitmix64-v0";
    private static readonly string[] StreamIds =
    [
        "map", "combat", "combat_targets", "reward", "shop", "event"
    ];

    public static RngBundle CreateBundle(string seed)
    {
        return new RngBundle(
            StreamIds
                .Select(streamId => new RngStreamState(
                    streamId,
                    Codec,
                    SeedBytes(seed, streamId),
                    0))
                .ToArray());
    }

    /// <summary>
    /// Creates six independently selected SplitMix64 states for a separate
    /// synthetic prior. Exactly six initial stream states are required.
    /// This is deliberately NOT the native game seed distribution.
    /// </summary>
    public static RngBundle CreateFactorizedInitialBundle(
        IReadOnlyDictionary<string, string> initialStateHex)
    {
        if (initialStateHex is null)
        {
            throw new ArgumentNullException(nameof(initialStateHex));
        }

        if (initialStateHex.Count != StreamIds.Length
            || StreamIds.Any(id => !initialStateHex.ContainsKey(id)))
        {
            throw new ArgumentException(
                "A factorized prior requires exactly six named RNG streams.",
                nameof(initialStateHex));
        }

        var streams = new RngStreamState[StreamIds.Length];
        for (var i = 0; i < StreamIds.Length; i++)
        {
            var hex = initialStateHex[StreamIds[i]];
            if (hex is null || hex.Length != 16
                || !ulong.TryParse(hex, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out var raw))
            {
                throw new ArgumentException(
                    $"Stream '{StreamIds[i]}' requires a 16-character hexadecimal state.",
                    nameof(initialStateHex));
            }
            var bytes = new byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, raw);
            streams[i] = new RngStreamState(StreamIds[i], Codec, bytes, 0);
        }
        return new RngBundle(streams);
    }

    internal static void ValidateBundle(
        RngBundle bundle, bool allowEmpty = false)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (bundle.Streams is null)
            throw new InvalidDataException(
                "Prototype RNG bundle has no stream collection.");
        if (allowEmpty && bundle.Streams.Length == 0)
            return;

        var expected = StreamIds.Order(StringComparer.Ordinal).ToArray();
        var actual = bundle.Streams.Select(stream =>
                stream?.StreamId ?? "")
            .Order(StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidDataException(
                "Prototype RNG must own exactly its six named streams.");
        foreach (var stream in bundle.Streams)
        {
            if (stream is null || stream.Codec != Codec ||
                stream.StateBytes is null ||
                stream.StateBytes.Length != sizeof(ulong))
                throw new InvalidDataException(
                    "Prototype RNG stream has an incompatible codec or state.");
        }
    }

    public static int NextInt(RngBundle bundle, string streamId, int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        }

        ControlledChanceGuardV111.RequireMigrated(
            "prototype_rng." + streamId, "next_int");
        var value = NextUInt64(bundle, streamId);
        return (int)(value % (ulong)maxExclusive);
    }

    public static bool NextBool(RngBundle bundle, string streamId, int numerator, int denominator)
    {
        if (denominator <= 0 || numerator < 0 || numerator > denominator)
        {
            throw new ArgumentOutOfRangeException(nameof(numerator));
        }

        return NextInt(bundle, streamId, denominator) < numerator;
    }

    public static void Shuffle<T>(RngBundle bundle, string streamId, T[] values)
    {
        for (var index = values.Length - 1; index > 0; index--)
        {
            var swapIndex = NextInt(bundle, streamId, index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }

    private static ulong NextUInt64(RngBundle bundle, string streamId)
    {
        var stream = bundle.GetStream(streamId);
        if (!StringComparer.Ordinal.Equals(stream.Codec, Codec) || stream.StateBytes.Length != sizeof(ulong))
        {
            throw new InvalidOperationException(
                $"Prototype RNG stream '{streamId}' has incompatible codec/state.");
        }

        var state = BinaryPrimitives.ReadUInt64LittleEndian(stream.StateBytes);
        state += 0x9E3779B97F4A7C15UL;

        var z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;

        var bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, state);
        bundle.ReplaceStream(streamId, stream with
        {
            StateBytes = bytes,
            CallCount = (stream.CallCount ?? 0) + 1
        });

        return z;
    }

    private static byte[] SeedBytes(string seed, string streamId)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}\n{streamId}"));
        return digest[..sizeof(ulong)];
    }
}
