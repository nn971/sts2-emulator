namespace Sts2Emulator.Core;

/// <summary>
/// Opaque, versioned full state for one game RNG stream.
/// We intentionally do not guess the game's PRNG algorithm in the scaffold.
/// </summary>
public sealed record RngStreamState
{
    private byte[] _stateBytes = Array.Empty<byte>();

    [System.Text.Json.Serialization.JsonConstructor]
    public RngStreamState(
        string StreamId,
        string Codec,
        byte[] StateBytes,
        ulong? CallCount = null)
    {
        this.StreamId = StreamId;
        this.Codec = Codec;
        this.StateBytes = StateBytes;
        this.CallCount = CallCount;
    }

    public string StreamId { get; init; }

    public string Codec { get; init; }

    public byte[] StateBytes
    {
        get => (byte[])_stateBytes.Clone();
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _stateBytes = (byte[])value.Clone();
        }
    }

    public ulong? CallCount { get; init; }

    public RngStreamState Fork() =>
        new(StreamId, Codec, _stateBytes, CallCount);

    public void Deconstruct(
        out string StreamId,
        out string Codec,
        out byte[] StateBytes,
        out ulong? CallCount)
    {
        StreamId = this.StreamId;
        Codec = this.Codec;
        StateBytes = this.StateBytes;
        CallCount = this.CallCount;
    }
}

public sealed record RngBundle
{
    private RngStreamState[]? _streams;

    [System.Text.Json.Serialization.JsonConstructor]
    public RngBundle(RngStreamState[] Streams)
    {
        this.Streams = Streams;
    }

    public RngStreamState[] Streams
    {
        get => _streams is null ? null! : (RngStreamState[])_streams.Clone();
        init => _streams = CloneAndValidate(value, nameof(Streams));
    }

    public static RngBundle Empty { get; } = new(Array.Empty<RngStreamState>());

    public RngBundle Fork() =>
        new(_streams!.Select(stream => stream.Fork()).ToArray());

    public void Deconstruct(out RngStreamState[] Streams) =>
        Streams = this.Streams;

    internal void ReplaceStreamAt(int index, RngStreamState stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var replacement = (RngStreamState[])_streams!.Clone();
        replacement[index] = stream;
        _streams = CloneAndValidate(replacement, nameof(Streams));
    }

    private static RngStreamState[] CloneAndValidate(
        RngStreamState[] streams, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(streams, parameterName);
        var owned = (RngStreamState[])streams.Clone();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stream in owned)
        {
            if (stream is null ||
                string.IsNullOrWhiteSpace(stream.StreamId) ||
                string.IsNullOrWhiteSpace(stream.Codec))
                throw new ArgumentException(
                    "RNG bundles require non-null streams with named IDs and codecs.",
                    parameterName);
            if (!seen.Add(stream.StreamId))
                throw new ArgumentException(
                    $"RNG stream ID '{stream.StreamId}' appears more than once.",
                    parameterName);
        }
        return owned;
    }
}
