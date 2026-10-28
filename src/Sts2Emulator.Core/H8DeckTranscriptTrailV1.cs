namespace Sts2Emulator.Core;

/// <summary>
/// Immutable decision history for a live H8 semantic deck. Each committed
/// Nature choice shares the complete previous prefix without copying it.
/// The final decision and total count are accessible in O(1); complete
/// legacy arrays are constructed only for external snapshots and audits.
/// </summary>
internal sealed class H8DeckTranscriptTrailV1
{
    private readonly H8DeckTranscriptTrailV1? _previous;
    internal SemanticDeckChoiceV111? LastChoice { get; }
    internal int Count { get; }

    private H8DeckTranscriptTrailV1(
        H8DeckTranscriptTrailV1? previous,
        SemanticDeckChoiceV111? lastChoice,
        int count)
    {
        _previous = previous;
        LastChoice = lastChoice;
        Count = count;
    }

    internal static H8DeckTranscriptTrailV1 FromHistory(
        SemanticDeckChoiceV111[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var result = new H8DeckTranscriptTrailV1(null, null, 0);
        foreach (var choice in entries)
            result = result.Append(choice);
        return result;
    }

    internal H8DeckTranscriptTrailV1 Append(SemanticDeckChoiceV111 choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        if (choice.Ordinal != Count)
            throw new InvalidDataException("H8 deck transcript ordinal diverged.");
        return new H8DeckTranscriptTrailV1(this, choice, checked(Count + 1));
    }

    internal SemanticDeckChoiceV111[] Materialize()
    {
        var result = new SemanticDeckChoiceV111[Count];
        var cursor = this;
        for (var i = Count - 1; i >= 0; i--)
        {
            result[i] = cursor.LastChoice ?? throw new InvalidDataException(
                "H8 deck transcript has a missing choice.");
            cursor = cursor._previous ?? throw new InvalidDataException(
                "H8 deck transcript has a broken prefix.");
        }
        return result;
    }
}
