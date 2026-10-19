namespace Sts2Emulator.Core;

/// <summary>
/// One explicit native-v111 provider across every typed random boundary.
/// Each operation delegates to its existing source-pinned adapter so cursor
/// handling, journaling and controlled-site authorization stay centralized.
/// </summary>
public sealed class NativeRandomBoundaryProviderV111 :
    IRandomBoundaryProviderV111
{
    private readonly NativeRandomIndexProviderV111 _index;
    private readonly NativeRandomUnitFloatProviderV111 _unitFloat = new();
    private readonly NativeRandomRangedFloatProviderV111 _rangedFloat = new();
    private readonly NativeRandomRangedDoubleProviderV111 _rangedDouble = new();
    private readonly NativeRandomGaussianIntProviderV111 _gaussianInt = new();
    private readonly NativeRandomGaussianDoubleProviderV111 _gaussianDouble = new();

    public string Mode => "native-v111";

    public NativeRandomBoundaryProviderV111(bool recordIndexToJournal = true)
    {
        _index = new NativeRandomIndexProviderV111(recordIndexToJournal);
    }

    public RandomIndexDecisionV111 Resolve(
        RandomIndexRequestV111 request, RngBundle streams) =>
        _index.Resolve(request, streams);

    public RandomUnitFloatDecisionV111 Resolve(
        RandomUnitFloatRequestV111 request, RngBundle streams) =>
        _unitFloat.Resolve(request, streams);

    public RandomRangedFloatDecisionV111 Resolve(
        RandomRangedFloatRequestV111 request, RngBundle streams) =>
        _rangedFloat.Resolve(request, streams);

    public RandomRangedDoubleDecisionV111 Resolve(
        RandomRangedDoubleRequestV111 request, RngBundle streams) =>
        _rangedDouble.Resolve(request, streams);

    public RandomGaussianIntDecisionV111 Resolve(
        RandomGaussianIntRequestV111 request, RngBundle streams) =>
        _gaussianInt.Resolve(request, streams);

    public RandomGaussianDoubleDecisionV111 Resolve(
        RandomGaussianDoubleRequestV111 request, RngBundle streams) =>
        _gaussianDouble.Resolve(request, streams);
}
