using System.Globalization;

namespace Sts2Emulator.Core;

/// <summary>
/// Shares a temporary candidate-label projection with whichever controlled
/// replay owner is active at the current mechanics boundary.
/// </summary>
internal static class ControlledStochasticCandidateLabelsV111
{
    private sealed class Scope(IDisposable[] leases) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            for (var index = leases.Length - 1; index >= 0; index--)
                leases[index].Dispose();
            _disposed = true;
        }
    }

    public static bool IsActive =>
        ControlledStochasticChanceReplayV111.IsActive ||
        ControlledEventChoiceReplayV111.IsActive;

    public static bool IsNumericRange(string[] labels, int count)
    {
        ArgumentNullException.ThrowIfNull(labels);
        return count >= 0 && labels.SequenceEqual(
            Enumerable.Range(0, count).Select(index =>
                index.ToString(CultureInfo.InvariantCulture)),
            StringComparer.Ordinal);
    }

    public static bool IsLegacyNumericRange(
        string[] labels, int supportCount, int maximumInlineCount)
    {
        ArgumentNullException.ThrowIfNull(labels);
        if (supportCount <= 0 || maximumInlineCount <= 0)
            return false;
        return supportCount > maximumInlineCount
            ? labels.Length == 0
            : IsNumericRange(labels, supportCount);
    }

    public static IDisposable Describe(string[]? candidateLabels)
    {
        var leases = new List<IDisposable>(2);
        try
        {
            leases.Add(ControlledStochasticChanceReplayV111
                .DescribeCandidates(candidateLabels));
            leases.Add(ControlledEventChoiceReplayV111
                .DescribeCandidates(candidateLabels));
            return new Scope(leases.ToArray());
        }
        catch
        {
            for (var index = leases.Count - 1; index >= 0; index--)
                leases[index].Dispose();
            throw;
        }
    }
}
