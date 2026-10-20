using System.Text.Json;
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

public sealed record RandomUnitFloatBoundaryEventV111(
    int Sequence,
    RandomUnitFloatRequestV111 Request,
    float SelectedValue,
    string ProviderMode,
    NativeRngSnapshot? NativeCursorBefore,
    NativeRngSnapshot? NativeCursorAfter);

public sealed record RandomRangedFloatBoundaryEventV111(
    int Sequence,
    RandomRangedFloatRequestV111 Request,
    float SelectedValue,
    string ProviderMode,
    NativeRngSnapshot? NativeCursorBefore,
    NativeRngSnapshot? NativeCursorAfter);

public sealed record RandomRangedDoubleBoundaryEventV111(
    int Sequence,
    RandomRangedDoubleRequestV111 Request,
    double SelectedValue,
    string ProviderMode,
    NativeRngSnapshot? NativeCursorBefore,
    NativeRngSnapshot? NativeCursorAfter);

public sealed record RandomGaussianIntBoundaryEventV111(
    int Sequence,
    RandomGaussianIntRequestV111 Request,
    int SelectedValue,
    string ProviderMode,
    NativeRngSnapshot? NativeCursorBefore,
    NativeRngSnapshot? NativeCursorAfter);

public sealed record RandomGaussianDoubleBoundaryEventV111(
    int Sequence,
    RandomGaussianDoubleRequestV111 Request,
    double SelectedValue,
    string ProviderMode,
    NativeRngSnapshot? NativeCursorBefore,
    NativeRngSnapshot? NativeCursorAfter);

/// <summary>One entry in the mixed-operation order produced by an actual
/// emulator transition. Event is the serialized typed boundary payload.</summary>
public sealed record RandomBoundaryTranscriptEntryV111(
    int Sequence, string OperationKind, JsonElement Event,
    string SchemaId = "sts2-v111-random-boundary-transcript-v1")
{
    private static readonly JsonSerializerOptions TranscriptJsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

    public void Validate()
    {
        if (SchemaId != "sts2-v111-random-boundary-transcript-v1" ||
            Sequence < 1 ||
            OperationKind is not ("index" or "unit_float" or
                "ranged_float" or "ranged_double" or "gaussian_int" or
                "gaussian_double") || Event.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(
                "Invalid ordered random-boundary transcript entry.");

        switch (OperationKind)
        {
            case "index":
                ValidateEvent(DeserializeEvent<RandomBoundaryEventV111>());
                break;
            case "unit_float":
                ValidateEvent(DeserializeEvent<RandomUnitFloatBoundaryEventV111>());
                break;
            case "ranged_float":
                ValidateEvent(DeserializeEvent<RandomRangedFloatBoundaryEventV111>());
                break;
            case "ranged_double":
                ValidateEvent(DeserializeEvent<RandomRangedDoubleBoundaryEventV111>());
                break;
            case "gaussian_int":
                ValidateEvent(DeserializeEvent<RandomGaussianIntBoundaryEventV111>());
                break;
            case "gaussian_double":
                ValidateEvent(DeserializeEvent<RandomGaussianDoubleBoundaryEventV111>());
                break;
        }
    }

    internal T DeserializeEvent<T>() where T : class =>
        Event.Deserialize<T>(TranscriptJsonOptions) ??
        throw new InvalidDataException(
            "Ordered random-boundary entry has no typed event payload.");

    internal string ReadProviderMode()
    {
        if (!Event.TryGetProperty("provider_mode", out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException(
                "Ordered random-boundary entry has no provider mode.");
        return value.GetString()!;
    }

    internal static JsonElement SerializeEvent(object boundaryEvent) =>
        JsonSerializer.SerializeToElement(
            boundaryEvent, boundaryEvent.GetType(), TranscriptJsonOptions);

    private static void ValidateEvent(RandomBoundaryEventV111 value)
    {
        if (value.Sequence < 1 || value.Request is null)
            throw new InvalidDataException(
                "Ordered index boundary event has invalid metadata.");
        var decision = new RandomIndexDecisionV111(
            value.Request, value.SelectedIndex, value.ProviderMode,
            value.NativeCursorBefore, value.NativeCursorAfter);
        decision.Validate();
        if (value.SelectedValue != decision.SelectedValue)
            throw new InvalidDataException(
                "Ordered index boundary value disagrees with its selected index.");
    }

    private static void ValidateEvent(
        RandomUnitFloatBoundaryEventV111 value)
    {
        if (value.Sequence < 1 || value.Request is null)
            throw new InvalidDataException(
                "Ordered unit-float boundary event has invalid metadata.");
        new RandomUnitFloatDecisionV111(value.Request, value.SelectedValue,
            value.ProviderMode, value.NativeCursorBefore,
            value.NativeCursorAfter).Validate();
    }

    private static void ValidateEvent(RandomRangedFloatBoundaryEventV111 value)
    {
        if (value.Sequence < 1 || value.Request is null)
            throw new InvalidDataException(
                "Ordered ranged-float boundary event has invalid metadata.");
        new RandomRangedFloatDecisionV111(value.Request, value.SelectedValue,
            value.ProviderMode, value.NativeCursorBefore,
            value.NativeCursorAfter).Validate();
    }

    private static void ValidateEvent(RandomRangedDoubleBoundaryEventV111 value)
    {
        if (value.Sequence < 1 || value.Request is null)
            throw new InvalidDataException(
                "Ordered ranged-double boundary event has invalid metadata.");
        new RandomRangedDoubleDecisionV111(value.Request, value.SelectedValue,
            value.ProviderMode, value.NativeCursorBefore,
            value.NativeCursorAfter).Validate();
    }

    private static void ValidateEvent(RandomGaussianIntBoundaryEventV111 value)
    {
        if (value.Sequence < 1 || value.Request is null)
            throw new InvalidDataException(
                "Ordered Gaussian-integer boundary event has invalid metadata.");
        new RandomGaussianIntDecisionV111(value.Request, value.SelectedValue,
            value.ProviderMode, value.NativeCursorBefore,
            value.NativeCursorAfter).Validate();
    }

    private static void ValidateEvent(
        RandomGaussianDoubleBoundaryEventV111 value)
    {
        if (value.Sequence < 1 || value.Request is null)
            throw new InvalidDataException(
                "Ordered Gaussian-double boundary event has invalid metadata.");
        new RandomGaussianDoubleDecisionV111(value.Request,
            value.SelectedValue, value.ProviderMode,
            value.NativeCursorBefore, value.NativeCursorAfter).Validate();
    }
}

internal static class RandomBoundaryCursorSpanV111
{
    internal static void Validate(
        string streamId, string providerMode,
        NativeRngSnapshot? cursorBefore,
        NativeRngSnapshot? cursorAfter)
    {
        bool isNative = providerMode == "native-v111";
        if ((cursorBefore is null) != (cursorAfter is null) ||
            isNative != (cursorBefore is not null))
            throw new ArgumentException(
                "Native provider decisions require both cursor snapshots; other providers cannot claim Native cursors.");

        if (!isNative)
            return;

        ValidateSnapshot(streamId, cursorBefore!);
        ValidateSnapshot(streamId, cursorAfter!);
    }

    private static void ValidateSnapshot(
        string streamId, NativeRngSnapshot snapshot)
    {
        var restored = NativeRngV111.FromSnapshot(streamId, snapshot);
        if (NativeRngV111.ToSnapshot(restored) != snapshot)
            throw new ArgumentException(
                "Native cursor snapshot does not round-trip through its registered codec.");
    }
}

public sealed class RandomBoundaryJournalV111 : IDisposable
{
    private static readonly AsyncLocal<RandomBoundaryJournalV111?> Active = new();
    private readonly RandomBoundaryJournalV111? _previous;
    private readonly List<RandomBoundaryEventV111> _events = [];
    private readonly List<RandomUnitFloatBoundaryEventV111> _unitFloatEvents = [];
    private readonly List<RandomRangedFloatBoundaryEventV111> _rangedFloatEvents = [];
    private readonly List<RandomRangedDoubleBoundaryEventV111> _rangedDoubleEvents = [];
    private readonly List<RandomGaussianIntBoundaryEventV111> _gaussianIntEvents = [];
    private readonly List<RandomGaussianDoubleBoundaryEventV111> _gaussianDoubleEvents = [];
    private readonly List<RandomBoundaryTranscriptEntryV111> _orderedTranscript = [];
    private bool _disposed;

    private RandomBoundaryJournalV111()
    {
        _previous = Active.Value;
        Active.Value = this;
    }

    public static RandomBoundaryJournalV111 Begin() => new();

    public IReadOnlyList<RandomBoundaryEventV111> Entries =>
        _events.ToArray();
    public IReadOnlyList<RandomUnitFloatBoundaryEventV111> UnitFloatEntries =>
        _unitFloatEvents.ToArray();
    public IReadOnlyList<RandomRangedFloatBoundaryEventV111> RangedFloatEntries =>
        _rangedFloatEvents.ToArray();
    public IReadOnlyList<RandomRangedDoubleBoundaryEventV111> RangedDoubleEntries =>
        _rangedDoubleEvents.ToArray();
    public IReadOnlyList<RandomGaussianIntBoundaryEventV111> GaussianIntEntries =>
        _gaussianIntEvents.ToArray();
    public IReadOnlyList<RandomGaussianDoubleBoundaryEventV111> GaussianDoubleEntries =>
        _gaussianDoubleEvents.ToArray();
    public IReadOnlyList<RandomBoundaryTranscriptEntryV111> OrderedTranscript =>
        _orderedTranscript.ToArray();

    /// <summary>Returns a provider-specific mixed transcript in the order
    /// recorded by the active journal. Its sequence numbers are local to the
    /// returned trace; each payload retains its original typed event shape.</summary>
    public RandomBoundaryTranscriptEntryV111[] EntriesForProvider(
        string providerMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerMode);
        return _orderedTranscript
            .Where(entry => entry.Event.TryGetProperty(
                    "provider_mode", out var mode) &&
                mode.ValueKind == System.Text.Json.JsonValueKind.String &&
                mode.GetString() == providerMode)
            .Select((entry, index) => entry with
            {
                Sequence = index + 1,
                Event = entry.Event.Clone()
            })
            .ToArray();
    }

    public static RandomBoundaryTranscriptEntryV111[] ConcatOrdered(
        IEnumerable<RandomBoundaryTranscriptEntryV111> first,
        IEnumerable<RandomBoundaryTranscriptEntryV111> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return first.Concat(second).Select((entry, index) => entry with
        {
            Sequence = index + 1,
            Event = entry.Event.Clone()
        }).ToArray();
    }

    public static void Record(RandomIndexDecisionV111 decision)
    {
        var scope = Active.Value;
        if (scope is null) return;
        if (scope._disposed)
            throw new InvalidOperationException("Random boundary journal is closed.");
        // All request/cursor data are copied after provider validation.
        // Neither game RNG nor mechanical continuation is touched.
        var entry = new RandomBoundaryEventV111(
            scope._events.Count + 1,
            decision.Request with
            {
                OrderedCandidates = (int[])decision.Request.OrderedCandidates.Clone()
            },
            decision.SelectedIndex,
            decision.SelectedValue,
            decision.ProviderMode,
            decision.NativeCursorBefore,
            decision.NativeCursorAfter);
        scope._events.Add(entry);
        scope.RecordOrdered("index", entry);
    }

    public static void Record(RandomUnitFloatDecisionV111 decision)
    {
        var scope = Active.Value;
        if (scope is null) return;
        if (scope._disposed)
            throw new InvalidOperationException("Random boundary journal is closed.");
        var entry = new RandomUnitFloatBoundaryEventV111(
            scope._unitFloatEvents.Count + 1,
            decision.Request with { },
            decision.SelectedValue,
            decision.ProviderMode,
            decision.NativeCursorBefore,
            decision.NativeCursorAfter);
        scope._unitFloatEvents.Add(entry);
        scope.RecordOrdered("unit_float", entry);
    }

    public static void Record(RandomRangedFloatDecisionV111 decision)
    {
        var scope = Active.Value;
        if (scope is null) return;
        if (scope._disposed)
            throw new InvalidOperationException("Random boundary journal is closed.");
        var entry = new RandomRangedFloatBoundaryEventV111(
            scope._rangedFloatEvents.Count + 1,
            decision.Request with { },
            decision.SelectedValue,
            decision.ProviderMode,
            decision.NativeCursorBefore,
            decision.NativeCursorAfter);
        scope._rangedFloatEvents.Add(entry);
        scope.RecordOrdered("ranged_float", entry);
    }

    public static void Record(RandomRangedDoubleDecisionV111 decision)
    {
        var scope = Active.Value;
        if (scope is null) return;
        if (scope._disposed)
            throw new InvalidOperationException("Random boundary journal is closed.");
        var entry = new RandomRangedDoubleBoundaryEventV111(
            scope._rangedDoubleEvents.Count + 1,
            decision.Request with { },
            decision.SelectedValue,
            decision.ProviderMode,
            decision.NativeCursorBefore,
            decision.NativeCursorAfter);
        scope._rangedDoubleEvents.Add(entry);
        scope.RecordOrdered("ranged_double", entry);
    }

    public static void Record(RandomGaussianIntDecisionV111 decision)
    {
        var scope = Active.Value;
        if (scope is null) return;
        if (scope._disposed)
            throw new InvalidOperationException("Random boundary journal is closed.");
        var entry = new RandomGaussianIntBoundaryEventV111(
            scope._gaussianIntEvents.Count + 1,
            decision.Request with { },
            decision.SelectedValue,
            decision.ProviderMode,
            decision.NativeCursorBefore,
            decision.NativeCursorAfter);
        scope._gaussianIntEvents.Add(entry);
        scope.RecordOrdered("gaussian_int", entry);
    }

    public static void Record(RandomGaussianDoubleDecisionV111 decision)
    {
        var scope = Active.Value;
        if (scope is null) return;
        if (scope._disposed)
            throw new InvalidOperationException("Random boundary journal is closed.");
        var entry = new RandomGaussianDoubleBoundaryEventV111(
            scope._gaussianDoubleEvents.Count + 1,
            decision.Request with { },
            decision.SelectedValue,
            decision.ProviderMode,
            decision.NativeCursorBefore,
            decision.NativeCursorAfter);
        scope._gaussianDoubleEvents.Add(entry);
        scope.RecordOrdered("gaussian_double", entry);
    }

    private void RecordOrdered(string operationKind, object boundaryEvent)
    {
        var transcript = new RandomBoundaryTranscriptEntryV111(
            _orderedTranscript.Count + 1, operationKind,
            RandomBoundaryTranscriptEntryV111.SerializeEvent(boundaryEvent));
        transcript.Validate();
        _orderedTranscript.Add(transcript);
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
/// Compare already resolved index, float/double, Gaussian-integer or
/// Gaussian-double provider traces on source site, stream, owner, request
/// parameters and outcome.
/// Provider mode and cursor may differ between native and counterfactual
/// scripted replay. This is only a consistency test of provider calls; it
/// cannot establish native game call-site completeness or hidden hook-order
/// parity.
/// </summary>
public sealed record RandomBoundaryDiffV111(
    string Status, int Compared, int? FirstMismatchIndex,
    string? MismatchField, string SourceHookParity = "NOT_TESTED");

public static class RandomBoundaryComparisonV111
{
    /// <summary>Compare mixed primitive operations in their actual global
    /// provider-call order while retaining each typed request comparison.</summary>
    public static RandomBoundaryDiffV111 CompareOrdered(
        IEnumerable<RandomBoundaryTranscriptEntryV111> expected,
        IEnumerable<RandomBoundaryTranscriptEntryV111> actual,
        bool requireNativeCursorEquality = false)
    {
        var left = expected.ToArray();
        var right = actual.ToArray();
        var shared = Math.Min(left.Length, right.Length);
        for (int index = 0; index < shared; index++)
        {
            var a = left[index];
            var b = right[index];
            if (a is null || b is null)
                return new("RANDOM_BOUNDARY_MISMATCH", index, index,
                    "event_payload");
            string? mismatch =
                a.Sequence != index + 1 || b.Sequence != index + 1
                    ? "sequence"
                    : a.OperationKind != b.OperationKind
                        ? "operation_kind" : null;
            if (mismatch is null)
                mismatch = CompareOrderedPayload(
                    a, b, requireNativeCursorEquality);
            if (mismatch is not null)
                return new("RANDOM_BOUNDARY_MISMATCH", index, index, mismatch);
        }
        if (left.Length != right.Length)
            return new("RANDOM_BOUNDARY_LENGTH_MISMATCH", shared,
                shared, "request_count");
        return new("RANDOM_BOUNDARIES_CONSISTENT", shared, null, null);
    }

    private static string? CompareOrderedPayload(
        RandomBoundaryTranscriptEntryV111 expected,
        RandomBoundaryTranscriptEntryV111 actual,
        bool requireNativeCursorEquality)
    {
        expected.Validate();
        actual.Validate();
        try
        {
            return expected.OperationKind switch
            {
                "index" => CompareOne(
                    expected.DeserializeEvent<RandomBoundaryEventV111>(),
                    actual.DeserializeEvent<RandomBoundaryEventV111>(),
                    requireNativeCursorEquality),
                "unit_float" => CompareOne(
                    expected.DeserializeEvent<RandomUnitFloatBoundaryEventV111>(),
                    actual.DeserializeEvent<RandomUnitFloatBoundaryEventV111>(),
                    requireNativeCursorEquality),
                "ranged_float" => CompareOne(
                    expected.DeserializeEvent<RandomRangedFloatBoundaryEventV111>(),
                    actual.DeserializeEvent<RandomRangedFloatBoundaryEventV111>(),
                    requireNativeCursorEquality),
                "ranged_double" => CompareOne(
                    expected.DeserializeEvent<RandomRangedDoubleBoundaryEventV111>(),
                    actual.DeserializeEvent<RandomRangedDoubleBoundaryEventV111>(),
                    requireNativeCursorEquality),
                "gaussian_int" => CompareOne(
                    expected.DeserializeEvent<RandomGaussianIntBoundaryEventV111>(),
                    actual.DeserializeEvent<RandomGaussianIntBoundaryEventV111>(),
                    requireNativeCursorEquality),
                "gaussian_double" => CompareOne(
                    expected.DeserializeEvent<RandomGaussianDoubleBoundaryEventV111>(),
                    actual.DeserializeEvent<RandomGaussianDoubleBoundaryEventV111>(),
                    requireNativeCursorEquality),
                _ => "operation_kind"
            };
        }
        catch (JsonException)
        {
            return "event_payload";
        }
        catch (InvalidDataException)
        {
            return "event_payload";
        }
        catch (Exception exception) when (exception is ArgumentException or
            InvalidOperationException or NullReferenceException)
        {
            return "event_payload";
        }
    }

    private static string? CompareOne<T>(
        T? expected, T? actual, bool requireNativeCursorEquality)
        where T : class
    {
        if (expected is null || actual is null)
            return "event_payload";
        RandomBoundaryDiffV111 result = (expected, actual) switch
        {
            (RandomBoundaryEventV111 a, RandomBoundaryEventV111 b) =>
                Compare([a with { Sequence = 1 }],
                    [b with { Sequence = 1 }], requireNativeCursorEquality),
            (RandomUnitFloatBoundaryEventV111 a,
                RandomUnitFloatBoundaryEventV111 b) =>
                Compare([a with { Sequence = 1 }],
                    [b with { Sequence = 1 }], requireNativeCursorEquality),
            (RandomRangedFloatBoundaryEventV111 a,
                RandomRangedFloatBoundaryEventV111 b) =>
                Compare([a with { Sequence = 1 }],
                    [b with { Sequence = 1 }], requireNativeCursorEquality),
            (RandomRangedDoubleBoundaryEventV111 a,
                RandomRangedDoubleBoundaryEventV111 b) =>
                Compare([a with { Sequence = 1 }],
                    [b with { Sequence = 1 }], requireNativeCursorEquality),
            (RandomGaussianIntBoundaryEventV111 a,
                RandomGaussianIntBoundaryEventV111 b) =>
                Compare([a with { Sequence = 1 }],
                    [b with { Sequence = 1 }], requireNativeCursorEquality),
            (RandomGaussianDoubleBoundaryEventV111 a,
                RandomGaussianDoubleBoundaryEventV111 b) =>
                Compare([a with { Sequence = 1 }],
                    [b with { Sequence = 1 }], requireNativeCursorEquality),
            _ => new RandomBoundaryDiffV111(
                "RANDOM_BOUNDARY_MISMATCH", 0, 0, "event_payload")
        };
        return result.Status == "RANDOM_BOUNDARIES_CONSISTENT"
            ? null : result.MismatchField ?? "event_payload";
    }

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

    public static RandomBoundaryDiffV111 Compare(
        IEnumerable<RandomUnitFloatBoundaryEventV111> expected,
        IEnumerable<RandomUnitFloatBoundaryEventV111> actual,
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

    public static RandomBoundaryDiffV111 Compare(
        IEnumerable<RandomGaussianIntBoundaryEventV111> expected,
        IEnumerable<RandomGaussianIntBoundaryEventV111> actual,
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
                a.Request.Mean != b.Request.Mean ? "mean" :
                a.Request.StandardDeviation != b.Request.StandardDeviation ? "standard_deviation" :
                a.Request.MinInclusive != b.Request.MinInclusive ? "min_inclusive" :
                a.Request.MaxInclusive != b.Request.MaxInclusive ? "max_inclusive" :
                a.Request.SourceVersion != b.Request.SourceVersion ? "source_version" :
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

    public static RandomBoundaryDiffV111 Compare(
        IEnumerable<RandomRangedFloatBoundaryEventV111> expected,
        IEnumerable<RandomRangedFloatBoundaryEventV111> actual,
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
                a.Request.MinInclusive != b.Request.MinInclusive ? "min_inclusive" :
                a.Request.MaxInclusive != b.Request.MaxInclusive ? "max_inclusive" :
                a.Request.SourceVersion != b.Request.SourceVersion ? "source_version" :
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

    public static RandomBoundaryDiffV111 Compare(
        IEnumerable<RandomRangedDoubleBoundaryEventV111> expected,
        IEnumerable<RandomRangedDoubleBoundaryEventV111> actual,
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
                a.Request.MinInclusive != b.Request.MinInclusive ? "min_inclusive" :
                a.Request.MaxInclusive != b.Request.MaxInclusive ? "max_inclusive" :
                a.Request.SourceVersion != b.Request.SourceVersion ? "source_version" :
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

    public static RandomBoundaryDiffV111 Compare(
        IEnumerable<RandomGaussianDoubleBoundaryEventV111> expected,
        IEnumerable<RandomGaussianDoubleBoundaryEventV111> actual,
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
                a.Request.Mean != b.Request.Mean ? "mean" :
                a.Request.StandardDeviation != b.Request.StandardDeviation ? "standard_deviation" :
                a.Request.MinInclusive != b.Request.MinInclusive ? "min_inclusive" :
                a.Request.MaxInclusive != b.Request.MaxInclusive ? "max_inclusive" :
                a.Request.SourceVersion != b.Request.SourceVersion ? "source_version" :
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
