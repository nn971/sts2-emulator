using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Sts2Emulator.Core;

public sealed record NativeRngSnapshot(
    int Counter, ulong State0, ulong State1, ulong State2, ulong State3);

/// <summary>
/// Independently implemented v111 RNG codec: SplitMix64 seeding, the public
/// domain xoshiro256** recurrence, and the audited native sampling contracts.
/// This codec does not establish engine stream ownership or call-order parity.
/// </summary>
public static class NativeRngV111
{
    public const string Codec = "sts2-v111-xoshiro256-starstar-v1";
    private static readonly string[] RunNames =
    [
        "up_front", "shuffle", "unknown_map_point", "combat_card_generation",
        "combat_potion_generation", "combat_card_selection", "combat_energy_costs",
        "combat_targets", "monster_ai", "niche", "combat_orbs", "treasure_room_relics"
    ];
    private static readonly string[] PlayerNames = ["rewards", "shops", "transformations"];

    public static RngBundle CreateBundle(string seed, int playerSlot = 0)
    {
        if (playerSlot != 0)
            throw new NotSupportedException("The emulator supports a single player in slot zero.");
        var runSeed = RunSeed(seed);
        // PlayerRngSet is initialized from the modern string hash even when
        // RunRngSet uses the historical 'old' prefix adapter.
        var playerSeed = HashString(seed);
        return new RngBundle(RunNames.Select(name => CreateStream(
            "run." + name, unchecked(runSeed + HashString(name))))
            .Concat(PlayerNames.Select(name => CreateStream(
                "player." + name, unchecked(playerSeed + HashString(name)))))
            .ToArray());
    }

    public static RngStreamState CreateContentStream(
        string streamId, ulong runSeed, string modelEntry, ulong mixin = 0) =>
        CreateStream(streamId, unchecked(runSeed + HashString(modelEntry) + mixin));

    public static RngStreamState CreateStream(string streamId, ulong seed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        var words = new ulong[4];
        for (var index = 0; index < words.Length; index++)
        {
            seed = unchecked(seed + 0x9e3779b97f4a7c15UL);
            var mixed = seed;
            mixed = unchecked((mixed ^ (mixed >> 30)) * 0xbf58476d1ce4e5b9UL);
            mixed = unchecked((mixed ^ (mixed >> 27)) * 0x94d049bb133111ebUL);
            words[index] = mixed ^ (mixed >> 31);
        }
        return FromSnapshot(streamId, new(0, words[0], words[1], words[2], words[3]));
    }

    public static RngStreamState FromSnapshot(string streamId, NativeRngSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        if ((snapshot.State0 | snapshot.State1 | snapshot.State2 | snapshot.State3) == 0)
            throw new InvalidDataException("An all-zero xoshiro state is invalid.");
        var bytes = new byte[32];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(0, 8), snapshot.State0);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8, 8), snapshot.State1);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(16, 8), snapshot.State2);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24, 8), snapshot.State3);
        return new(streamId, Codec, bytes, unchecked((uint)snapshot.Counter));
    }

    public static NativeRngSnapshot ToSnapshot(RngStreamState stream)
    {
        if (stream.Codec != Codec || stream.StateBytes.Length != 32
            || stream.CallCount is null or > uint.MaxValue)
            throw new InvalidDataException("Invalid v111 RNG codec, state length or counter.");
        var bytes = stream.StateBytes.AsSpan();
        var snapshot = new NativeRngSnapshot(unchecked((int)(uint)stream.CallCount.Value),
            BinaryPrimitives.ReadUInt64LittleEndian(bytes[..8]),
            BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(8, 8)),
            BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(16, 8)),
            BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(24, 8)));
        if ((snapshot.State0 | snapshot.State1 | snapshot.State2 | snapshot.State3) == 0)
            throw new InvalidDataException("An all-zero xoshiro state is invalid.");
        return snapshot;
    }

    public static ulong NextUInt64(RngBundle bundle, string streamId)
    {
        var index = RequireStream(bundle, streamId);
        var current = ToSnapshot(bundle.Streams[index]);
        var result = unchecked(BitOperations.RotateLeft(unchecked(current.State1 * 5), 7) * 9);
        var temporary = current.State1 << 17;
        var c = current.State2 ^ current.State0;
        var d = current.State3 ^ current.State1;
        var b = current.State1 ^ c;
        var a = current.State0 ^ d;
        c ^= temporary;
        d = BitOperations.RotateLeft(d, 45);
        bundle.Streams[index] = FromSnapshot(streamId,
            new(unchecked(current.Counter + 1), a, b, c, d));
        return result;
    }

    public static double NextDouble(RngBundle bundle, string streamId) =>
        (NextUInt64(bundle, streamId) >> 11) * (1.0 / 9007199254740992.0);

    public static double NextDouble(RngBundle bundle, string streamId, double min, double max)
    {
        if (min > max) throw new ArgumentOutOfRangeException(nameof(min));
        return NextDouble(bundle, streamId) * (max - min) + min;
    }

    public static float NextFloat(RngBundle bundle, string streamId, float min = 0, float max = 1)
    {
        if (min > max) throw new ArgumentOutOfRangeException(nameof(min));
        // Native Rng uses a double sample and rounds the final expression to
        // float; MegaRandom.NextFloat's 24-bit sampler is a different API.
        return (float)(NextDouble(bundle, streamId) * (double)(max - min) + min);
    }

    public static int NextInt(RngBundle bundle, string streamId, int maxExclusive = int.MaxValue) =>
        NextInt(bundle, streamId, 0, maxExclusive);

    public static int NextInt(RngBundle bundle, string streamId, int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        var range = (long)maxExclusive - minInclusive;
        return (int)((long)(NextDouble(bundle, streamId) * range) + minInclusive);
    }

    public static bool NextBool(RngBundle bundle, string streamId) => NextInt(bundle, streamId, 2) == 0;

    public static uint NextUInt32(RngBundle bundle, string streamId, uint minInclusive, uint maxExclusive)
    {
        if (minInclusive >= maxExclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return minInclusive + (uint)(NextDouble(bundle, streamId) * (maxExclusive - minInclusive));
    }

    public static ulong NextUInt64(RngBundle bundle, string streamId, ulong maxExclusive) =>
        maxExclusive == ulong.MaxValue ? NextUInt64(bundle, streamId) : NextUInt64(bundle, streamId, 0, maxExclusive);

    public static ulong NextUInt64(RngBundle bundle, string streamId, ulong minInclusive, ulong maxExclusive)
    {
        if (minInclusive >= maxExclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return minInclusive + (ulong)(NextDouble(bundle, streamId) * (maxExclusive - minInclusive));
    }

    public static int NextGaussianInt(RngBundle bundle, string streamId, int mean, int stdDev, int min, int max)
    {
        if (min > max || mean < min || mean > max) throw new ArgumentOutOfRangeException(nameof(mean));
        while (true)
        {
            var radius = Math.Sqrt(-2 * Math.Log(1 - NextDouble(bundle, streamId)));
            var angle = 2 * Math.PI * (1 - NextDouble(bundle, streamId));
            var value = (int)Math.Round(mean + stdDev * radius * Math.Sin(angle));
            if (value >= min && value <= max) return value;
        }
    }

    public static double NextGaussianDouble(RngBundle bundle, string streamId,
        double mean = 0, double stdDev = 1, double min = 0, double max = 1)
    {
        if (min > max || mean < 0 || mean > 1) throw new ArgumentOutOfRangeException(nameof(mean));
        while (true)
        {
            var radius = Math.Sqrt(-2 * Math.Log(1 - NextDouble(bundle, streamId)));
            var angle = 2 * Math.PI * (1 - NextDouble(bundle, streamId));
            var unit = mean + stdDev * radius * Math.Cos(angle);
            if (unit >= 0 && unit <= 1) return unit * (max - min) + min;
        }
    }

    public static void Shuffle<T>(RngBundle bundle, string streamId, IList<T> values)
    {
        for (var last = values.Count - 1; last > 0; last--)
        {
            var chosen = NextInt(bundle, streamId, last + 1);
            (values[last], values[chosen]) = (values[chosen], values[last]);
        }
    }

    public static T? NextItem<T>(RngBundle bundle, string streamId, IReadOnlyList<T> values) =>
        values.Count == 0 ? default : values[NextInt(bundle, streamId, values.Count)];

    public static T WeightedNextItem<T>(RngBundle bundle, string streamId,
        IReadOnlyList<T> values, Func<T, float> weight, T fallback)
    {
        var sample = NextFloat(bundle, streamId);
        var total = values.Sum(weight);
        var remaining = sample * total;
        foreach (var value in values)
        {
            remaining -= weight(value);
            if (remaining <= 0) return value;
        }
        return fallback;
    }

    private static int RequireStream(RngBundle bundle, string id)
    {
        var matches = bundle.Streams.Select((stream, index) => (stream, index))
            .Where(x => x.stream.StreamId == id).ToArray();
        return matches.Length == 1 ? matches[0].index
            : throw new InvalidDataException($"Expected exactly one RNG stream '{id}'.");
    }

    public static ulong RunSeed(string seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        if (!seed.StartsWith("old", StringComparison.Ordinal)) return HashString(seed);
        var value = seed.AsSpan(3);
        uint even = 0x15051505, odd = even;
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 1) == 0) even = unchecked(even * 33) ^ value[index];
            else odd = unchecked(odd * 33) ^ value[index];
        }
        return unchecked(even + odd * 1566083941U);
    }

    public static ulong HashString(string value) => XxHash64(Encoding.UTF8.GetBytes(value));

    // Standard xxHash64, seed zero, with little-endian lane reads. Kept local
    // to avoid a new dependency or runtime-dependent string hashing.
    public static ulong XxHash64(ReadOnlySpan<byte> bytes)
    {
        const ulong p1 = 11400714785074694791, p2 = 14029467366897019727;
        const ulong p3 = 1609587929392839161, p4 = 9650029242287828579, p5 = 2870177450012600261;
        static ulong Round(ulong accumulator, ulong lane) =>
            unchecked(BitOperations.RotateLeft(unchecked(accumulator + lane * p2), 31) * p1);
        static ulong Merge(ulong hash, ulong lane) => unchecked((hash ^ Round(0, lane)) * p1 + p4);
        var offset = 0;
        ulong hash;
        if (bytes.Length >= 32)
        {
            ulong a = unchecked(p1 + p2), b = p2, c = 0, d = unchecked(0UL - p1);
            while (offset <= bytes.Length - 32)
            {
                a = Round(a, BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(offset, 8)));
                b = Round(b, BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(offset + 8, 8)));
                c = Round(c, BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(offset + 16, 8)));
                d = Round(d, BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(offset + 24, 8)));
                offset += 32;
            }
            hash = unchecked(BitOperations.RotateLeft(a, 1) + BitOperations.RotateLeft(b, 7)
                + BitOperations.RotateLeft(c, 12) + BitOperations.RotateLeft(d, 18));
            hash = Merge(Merge(Merge(Merge(hash, a), b), c), d);
        }
        else hash = p5;
        hash = unchecked(hash + (ulong)bytes.Length);
        while (offset <= bytes.Length - 8)
        {
            hash ^= Round(0, BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(offset, 8)));
            hash = unchecked(BitOperations.RotateLeft(hash, 27) * p1 + p4);
            offset += 8;
        }
        if (offset <= bytes.Length - 4)
        {
            hash ^= unchecked((ulong)BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4)) * p1);
            hash = unchecked(BitOperations.RotateLeft(hash, 23) * p2 + p3);
            offset += 4;
        }
        for (; offset < bytes.Length; offset++)
        {
            hash ^= unchecked(bytes[offset] * p5);
            hash = unchecked(BitOperations.RotateLeft(hash, 11) * p1);
        }
        hash ^= hash >> 33;
        hash = unchecked(hash * p2);
        hash ^= hash >> 29;
        hash = unchecked(hash * p3);
        return hash ^ (hash >> 32);
    }
}
