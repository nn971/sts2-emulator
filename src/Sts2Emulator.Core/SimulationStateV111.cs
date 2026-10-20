namespace Sts2Emulator.Core;

/// <summary>
/// Atomic composite state for mechanics and its active stochastic provider.
/// The domain-specific records own their contents; this envelope groups their
/// lifecycle and publication boundary and requires both owners to be present.
/// </summary>
public sealed record SimulationStateV111<TMechanics, TStochastic>
{
    private TMechanics _mechanics = default!;
    private TStochastic _stochastic = default!;

    public SimulationStateV111(TMechanics Mechanics, TStochastic Stochastic)
    {
        this.Mechanics = Mechanics;
        this.Stochastic = Stochastic;
    }

    public TMechanics Mechanics
    {
        get => _mechanics;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Mechanics));
            _mechanics = value;
        }
    }

    public TStochastic Stochastic
    {
        get => _stochastic;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Stochastic));
            _stochastic = value;
        }
    }

    public void Deconstruct(
        out TMechanics Mechanics,
        out TStochastic Stochastic)
    {
        Mechanics = this.Mechanics;
        Stochastic = this.Stochastic;
    }
}
