namespace Sts2Emulator.Core;

/// <summary>
/// Shared cursor and typed-request validation for controlled random-index
/// continuations. Boundary adapters retain their own site and ordinal rules.
/// </summary>
internal sealed class ControlledRandomIndexReplayPrefixV111
{
    private readonly RandomIndexDecisionV111[] _decisions;
    private int _nextDecision;

    public IReadOnlyList<RandomIndexDecisionV111> Decisions => _decisions;
    public int ConsumedCount => _nextDecision;

    public ControlledRandomIndexReplayPrefixV111(
        RandomIndexDecisionV111[] decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        _decisions = decisions.Select(CloneDecision).ToArray();
    }

    public bool TryConsume(
        RandomIndexRequestV111 request,
        string divergenceMessage,
        out int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        if (_nextDecision >= _decisions.Length)
        {
            selectedIndex = default;
            return false;
        }

        var decision = _decisions[_nextDecision++];
        decision.Validate();
        if (CanonicalJson.Sha256(decision.Request) !=
            CanonicalJson.Sha256(request))
            throw new InvalidDataException(divergenceMessage);

        selectedIndex = decision.SelectedIndex;
        return true;
    }

    public void EnsureConsumed(string errorMessage)
    {
        if (_nextDecision != _decisions.Length)
            throw new InvalidDataException(errorMessage);
    }

    private static RandomIndexDecisionV111 CloneDecision(
        RandomIndexDecisionV111 decision) => decision with
    {
        Request = decision.Request with
        {
            OrderedCandidates =
                (int[])decision.Request.OrderedCandidates.Clone()
        }
    };
}
