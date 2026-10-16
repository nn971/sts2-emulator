using Sts2Emulator.Core;

namespace Sts2ReferenceMod.Trace;

/// <summary>
/// Pinned native Creature.SetUniqueMonsterHpValue source execution. The
/// creature does not necessarily have a stable CombatId until it is attached
/// later, so CreationOrdinal/MonsterModelId are provenance, NOT an inferred
/// emulator creature ID.
/// </summary>
public sealed record NativeInitialHpRollEvidenceV111(
    long CreationOrdinal,
    string MonsterModelId,
    int[] OrderedEligibleHp,
    int SelectedIndex,
    int SelectedHp,
    bool UniqueEligibleValuesExhausted,
    NativeRngSnapshot CursorBefore,
    NativeRngSnapshot CursorAfter,
    string SourceVersion = "v0.111.0")
{
    public void Validate()
    {
        if (CreationOrdinal <= 0 || SourceVersion != "v0.111.0"
            || string.IsNullOrWhiteSpace(MonsterModelId)
            || OrderedEligibleHp is null || OrderedEligibleHp.Length == 0
            || OrderedEligibleHp.Distinct().Count() != OrderedEligibleHp.Length
            || SelectedIndex < 0 || SelectedIndex >= OrderedEligibleHp.Length
            || SelectedHp != OrderedEligibleHp[SelectedIndex]
            || CursorBefore is null || CursorAfter is null
            || CursorBefore.Counter < 0
            || CursorAfter.Counter != CursorBefore.Counter + 1)
            throw new ArgumentException("Invalid native pre-frame initial HP source evidence.");
    }
}
