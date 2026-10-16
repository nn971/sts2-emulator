using System.Threading;

namespace Sts2Emulator.Core;

/// <summary>
/// Opt-in diagnostic journal at the *actual shared random-choice provider
/// boundary*. Never exposed as a fair/player-visible observation: it contains
/// hidden stream ownership and (native mode only) RNG cursor words. It does
/// not itself create or resolve a random choice.
/// </summary>
public sealed record RandomBoundaryEventV111(
    int Sequence,
    RandomIndexRequestV111 Request,
    int SelectedIndex,
    int SelectedValue,
    string ProviderMode,
    NativeRngSnapshot? NativeCursorBefore,
    NativeRngSnapshot? NativeCursorAfter);

public sealed class RandomBoundaryJournalV111 : IDisposable
{
    private static readonly AsyncLocal<RandomBoundaryJournalV111?> Active = new();
    private readonly RandomBoundaryJournalV111? _previous;
    private readonly List<RandomBoundaryEventV111> _events = [];
    private bool _disposed;

    private RandomBoundaryJournalV111()
    {
        _previous = Active.Value;
        Active.Value = this;
    }

    public static RandomBoundaryJournalV111 Begin() => new();

    public IReadOnlyList<RandomBoundaryEventV111> Entries =>
        _events.ToArray();

    public static void Record(RandomIndexDecisionV111 decision)
    {
        var scope = Active.Value;
        if (scope is null) return;
        if (scope._disposed)
            throw new InvalidOperationException("Random boundary journal is closed.");
        // All request/cursor data are copied after provider validation.
        // Neither game RNG nor mechanical continuation is touched.
        scope._events.Add(new RandomBoundaryEventV111(
            scope._events.Count + 1,
            decision.Request with
            {
                OrderedCandidates = (int[])decision.Request.OrderedCandidates.Clone()
            },
            decision.SelectedIndex,
            decision.SelectedValue,
            decision.ProviderMode,
            decision.NativeCursorBefore,
            decision.NativeCursorAfter));
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (!ReferenceEquals(Active.Value, this))
            throw new InvalidOperationException("Random boundary scopes must close in LIFO order.");
        Active.Value = _previous;
        _disposed = true;
    }
}

/// <summary>
/// Compare two already resolved provider traces on source site, stream,
/// owner, eligible pool and selected outcome. Provider mode and cursor may
/// differ between native and counterfactual scripted replay. This is *only*
/// a consistency test of these provider calls; it cannot establish native
/// game call-site completeness or hidden hook-order parity.
/// </summary>
public sealed record RandomBoundaryDiffV111(
    string Status, int Compared, int? FirstMismatchIndex,
    string? MismatchField, string SourceHookParity = "NOT_TESTED");

public static class RandomBoundaryComparisonV111
{
    public static RandomBoundaryDiffV111 Compare(
        IEnumerable<RandomBoundaryEventV111> expected,
        IEnumerable<RandomBoundaryEventV111> actual,
        bool requireNativeCursorEquality = false)
    {
        var left = expected.ToArray();
        var right = actual.ToArray();
        int shared = Math.Min(left.Length, right.Length);
        for (int i = 0; i < shared; i++)
        {
            var a = left[i]; var b = right[i];
            string? mismatch =
                a.Sequence != i + 1 || b.Sequence != i + 1 ? "sequence" :
                a.Request.SiteId != b.Request.SiteId ? "site_id" :
                a.Request.StreamId != b.Request.StreamId ? "stream_id" :
                a.Request.OwnerId != b.Request.OwnerId ? "owner_id" :
                a.Request.Ordinal != b.Request.Ordinal ? "ordinal" :
                a.Request.SourceVersion != b.Request.SourceVersion ? "source_version" :
                a.Request.OperationKind != b.Request.OperationKind ? "operation_kind" :
                a.Request.BoundedCount != b.Request.BoundedCount ? "bounded_count" :
                !a.Request.OrderedCandidates.SequenceEqual(b.Request.OrderedCandidates)
                    ? "ordered_candidates" :
                a.SelectedIndex != b.SelectedIndex ? "selected_index" :
                a.SelectedValue != b.SelectedValue ? "selected_value" :
                requireNativeCursorEquality && (
                    a.NativeCursorBefore is null || b.NativeCursorBefore is null ||
                    a.NativeCursorAfter is null || b.NativeCursorAfter is null ||
                    a.NativeCursorBefore != b.NativeCursorBefore ||
                    a.NativeCursorAfter != b.NativeCursorAfter) ? "native_cursors" :
                null;
            if (mismatch is not null)
                return new("RANDOM_BOUNDARY_MISMATCH", i, i, mismatch);
        }
        if (left.Length != right.Length)
            return new("RANDOM_BOUNDARY_LENGTH_MISMATCH", shared,
                shared, "request_count");
        return new("RANDOM_BOUNDARIES_CONSISTENT", shared, null, null);
    }
}
