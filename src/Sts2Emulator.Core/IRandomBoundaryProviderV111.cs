namespace Sts2Emulator.Core;

/// <summary>
/// One provider for every typed primitive random boundary. Implementations
/// can be installed as a single scoped provider for mixed-operation replay.
/// </summary>
public interface IRandomBoundaryProviderV111 :
    IRandomIndexProviderV111,
    IRandomUnitFloatProviderV111,
    IRandomRangedFloatProviderV111,
    IRandomRangedDoubleProviderV111,
    IRandomGaussianIntProviderV111,
    IRandomGaussianDoubleProviderV111;
