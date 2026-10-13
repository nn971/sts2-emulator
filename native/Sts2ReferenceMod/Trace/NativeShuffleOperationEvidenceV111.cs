using Sts2Emulator.Core;

namespace Sts2ReferenceMod.Trace;

/// <summary>
/// An independently source-observed v0.111 shuffle: input to the native
/// Fisher-Yates loop, each primitive draw and the hook-visible order before
/// and after ModifyShuffleOrder. Tokens are distinct only WITHIN this
/// operation. Combat card-index translation is a separate identity gate.
/// </summary>
public sealed record NativeShuffleChoiceV111(
    int BoundedCount, int SelectedIndex,
    NativeRngSnapshot CursorBefore, NativeRngSnapshot CursorAfter);

public sealed record NativeShuffleOperationEvidenceV111(
    long OperationId, string Kind, string[] OrderedInput,
    NativeShuffleChoiceV111[] Choices,
    string[] PreHookOrder, string[] PostHookOrder,
    string[] CardModels,
    string SourceVersion = "v0.111.0")
{
    public void Validate()
    {
        if (OperationId <= 0 || Kind is not ("initial" or "recycle")
            || SourceVersion != "v0.111.0"
            || OrderedInput is null || Choices is null
            || PreHookOrder is null || PostHookOrder is null
            || CardModels is null || CardModels.Length != OrderedInput.Length)
            throw new ArgumentException("Invalid pinned native shuffle evidence.");
        var tokens = OrderedInput.ToArray();
        if (tokens.Any(string.IsNullOrWhiteSpace) ||
            tokens.Distinct(StringComparer.Ordinal).Count() != tokens.Length)
            throw new ArgumentException("Shuffle requires distinct source card tokens.");
        if (Choices.Length != Math.Max(0, tokens.Length - 1))
            throw new ArgumentException("Incomplete Fisher-Yates source choices.");
        for (int i = 0; i < Choices.Length; i++)
        {
            int end = tokens.Length - 1 - i;
            var c = Choices[i];
            if (c.BoundedCount != end + 1 ||
                c.SelectedIndex < 0 || c.SelectedIndex > end ||
                c.CursorBefore is null || c.CursorAfter is null ||
                c.CursorAfter.Counter != c.CursorBefore.Counter + 1 ||
                (i > 0 && c.CursorBefore != Choices[i - 1].CursorAfter))
                throw new ArgumentException("Shuffle RNG bounds/cursors diverged.");
            (tokens[end], tokens[c.SelectedIndex]) =
                (tokens[c.SelectedIndex], tokens[end]);
        }
        if (!tokens.SequenceEqual(PreHookOrder))
            throw new ArgumentException("Native shuffle choices do not reproduce pre-hook order.");
        if (PostHookOrder.Length != tokens.Length ||
            !PostHookOrder.Order(StringComparer.Ordinal).SequenceEqual(
                OrderedInput.Order(StringComparer.Ordinal)))
            throw new ArgumentException("Native shuffle hook did not preserve card identity.");
    }
}
