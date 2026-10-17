namespace Sts2Emulator.Core;

/// <summary>
/// Atomic run-level ownership of provider-private stochastic state. The
/// compatibility properties on <see cref="RunState"/> project this envelope
/// under their existing serialized names until the snapshot wire schema is
/// deliberately migrated.
/// </summary>
public sealed record RunStochasticStateV111(
    RngBundle PrototypeRng,
    RngBundle? NativeRunRng = null,
    ReferenceNatureStateV111? ReferenceNature = null)
{
    public RunStochasticStateV111 Fork() => this with
    {
        PrototypeRng = PrototypeRng.Fork(),
        NativeRunRng = NativeRunRng?.Fork(),
        ReferenceNature = ReferenceNature?.Fork()
    };

    public void Validate(RunState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        NativeRunRngV111.Validate(owner);
        ReferenceNature?.Validate();
    }

    internal static RunStochasticStateV111 WithNativeRunRng(
        RunStochasticStateV111 current, RngBundle? nativeRunRng)
    {
        return current with { NativeRunRng = nativeRunRng };
    }

    internal static RunStochasticStateV111 WithReferenceNature(
        RunStochasticStateV111 current,
        ReferenceNatureStateV111? referenceNature)
    {
        return current with { ReferenceNature = referenceNature };
    }

    internal static RunStochasticStateV111 WithPrototypeRng(
        RunStochasticStateV111 current, RngBundle prototypeRng) =>
        current with { PrototypeRng = prototypeRng };
}
