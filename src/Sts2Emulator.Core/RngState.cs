namespace Sts2Emulator.Core;

/// <summary>
/// Opaque, versioned full state for one game RNG stream.
/// We intentionally do not guess the game's PRNG algorithm in the scaffold.
/// </summary>
public sealed record RngStreamState(
    string StreamId,
    string Codec,
    byte[] StateBytes,
    ulong? CallCount = null)
{
    public RngStreamState Fork() => this with { StateBytes = (byte[])StateBytes.Clone() };
}

public sealed record RngBundle(RngStreamState[] Streams)
{
    public static RngBundle Empty { get; } = new(Array.Empty<RngStreamState>());

    public RngBundle Fork() => new(Streams.Select(stream => stream.Fork()).ToArray());
}
