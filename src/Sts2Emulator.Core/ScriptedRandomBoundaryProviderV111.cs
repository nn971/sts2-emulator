namespace Sts2Emulator.Core;

/// <summary>
/// Replays one mixed primitive-choice transcript in the exact order in which
/// the mechanics request its operations. Outcomes are counterfactual scripted
/// choices; this provider never advances an RNG stream.
/// </summary>
public sealed class ScriptedRandomBoundaryProviderV111 :
    IRandomBoundaryProviderV111
{
    public const string TranscriptSchemaId =
        "sts2-v111-random-boundary-transcript-v1";

    private readonly RandomBoundaryTranscriptEntryV111[] _transcript;
    private int _cursor;

    public string Mode => "scripted-v111";
    public int Remaining => _transcript.Length - _cursor;

    public ScriptedRandomBoundaryProviderV111(
        IEnumerable<RandomBoundaryTranscriptEntryV111> transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        _transcript = transcript.Select(entry => entry with
        {
            Event = entry.Event.Clone()
        }).ToArray();
        for (var index = 0; index < _transcript.Length; index++)
        {
            _transcript[index].Validate();
            if (_transcript[index].Sequence != index + 1)
                throw new InvalidDataException(
                    "Scripted random-boundary transcript is not contiguous.");
        }
    }

    public RandomIndexDecisionV111 Resolve(
        RandomIndexRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var (entry, expected) = Read<RandomBoundaryEventV111>("index");
        if (!ControlledStochasticChanceReplayV111.RequestsMatch(
                request, expected.Request))
            throw RequestMismatch(entry);
        var result = new RandomIndexDecisionV111(
            request with
            {
                OrderedCandidates =
                    (int[])request.OrderedCandidates.Clone()
            }, expected.SelectedIndex, Mode);
        result.Validate();
        if (expected.SelectedValue != result.SelectedValue)
            throw new InvalidDataException(
                "Scripted random-boundary index value disagrees with its index.");
        Consume(entry);
        RandomBoundaryJournalV111.Record(result);
        return result;
    }

    public RandomUnitFloatDecisionV111 Resolve(
        RandomUnitFloatRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var (entry, expected) = Read<RandomUnitFloatBoundaryEventV111>(
            "unit_float");
        if (!ControlledStochasticChanceReplayV111.RequestsMatch(
                request, expected.Request))
            throw RequestMismatch(entry);
        var result = new RandomUnitFloatDecisionV111(
            request with { }, expected.SelectedValue, Mode);
        result.Validate();
        Consume(entry);
        RandomBoundaryJournalV111.Record(result);
        return result;
    }

    public RandomRangedFloatDecisionV111 Resolve(
        RandomRangedFloatRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var (entry, expected) = Read<RandomRangedFloatBoundaryEventV111>(
            "ranged_float");
        if (!ControlledStochasticChanceReplayV111.RequestsMatch(
                request, expected.Request))
            throw RequestMismatch(entry);
        var result = new RandomRangedFloatDecisionV111(
            request with { }, expected.SelectedValue, Mode);
        result.Validate();
        Consume(entry);
        RandomBoundaryJournalV111.Record(result);
        return result;
    }

    public RandomRangedDoubleDecisionV111 Resolve(
        RandomRangedDoubleRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var (entry, expected) = Read<RandomRangedDoubleBoundaryEventV111>(
            "ranged_double");
        if (!ControlledStochasticChanceReplayV111.RequestsMatch(
                request, expected.Request))
            throw RequestMismatch(entry);
        var result = new RandomRangedDoubleDecisionV111(
            request with { }, expected.SelectedValue, Mode);
        result.Validate();
        Consume(entry);
        RandomBoundaryJournalV111.Record(result);
        return result;
    }

    public RandomGaussianIntDecisionV111 Resolve(
        RandomGaussianIntRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var (entry, expected) = Read<RandomGaussianIntBoundaryEventV111>(
            "gaussian_int");
        if (!ControlledStochasticChanceReplayV111.RequestsMatch(
                request, expected.Request))
            throw RequestMismatch(entry);
        var result = new RandomGaussianIntDecisionV111(
            request with { }, expected.SelectedValue, Mode);
        result.Validate();
        Consume(entry);
        RandomBoundaryJournalV111.Record(result);
        return result;
    }

    public RandomGaussianDoubleDecisionV111 Resolve(
        RandomGaussianDoubleRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var (entry, expected) = Read<RandomGaussianDoubleBoundaryEventV111>(
            "gaussian_double");
        if (!ControlledStochasticChanceReplayV111.RequestsMatch(
                request, expected.Request))
            throw RequestMismatch(entry);
        var result = new RandomGaussianDoubleDecisionV111(
            request with { }, expected.SelectedValue, Mode);
        result.Validate();
        Consume(entry);
        RandomBoundaryJournalV111.Record(result);
        return result;
    }

    public void AssertComplete()
    {
        if (Remaining != 0)
            throw new InvalidOperationException(
                "Unconsumed scripted random-boundary operations: " +
                Remaining);
    }

    private (RandomBoundaryTranscriptEntryV111 Entry, T Event) Read<T>(
        string operationKind) where T : class
    {
        if (_cursor >= _transcript.Length)
            throw new InvalidOperationException(
                "Scripted random-boundary transcript exhausted.");
        var entry = _transcript[_cursor];
        if (entry.OperationKind != operationKind)
            throw new InvalidOperationException(
                "Scripted random-boundary operation order mismatch at entry " +
                _cursor + ": expected " + entry.OperationKind +
                ", received " + operationKind + ".");
        var boundaryEvent = entry.DeserializeEvent<T>();
        return (entry, boundaryEvent);
    }

    private void Consume(RandomBoundaryTranscriptEntryV111 entry)
    {
        if (!ReferenceEquals(_transcript[_cursor], entry))
            throw new InvalidOperationException(
                "Scripted random-boundary cursor changed during resolution.");
        _cursor++;
    }

    private static InvalidOperationException RequestMismatch(
        RandomBoundaryTranscriptEntryV111 entry) => new(
        "Scripted random-boundary request mismatch at entry " +
        (entry.Sequence - 1) + ".");

}
