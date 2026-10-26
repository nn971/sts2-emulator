namespace Sts2Emulator.Core;

/// <summary>
/// Exact v0.111.0 seed derivation for gameplay RNGs constructed outside
/// RunRngSet and PlayerRngSet. Every method returns an independent mutable
/// stream. The caller owns its lifetime and must retain its state whenever
/// the corresponding native object persists across decisions.
///
/// Source: Flaaax/STS2-source@706ef1c9fa2219220065849fd5265328607ece20
/// (StandardActMap, EncounterModel, CombatState, EventModel and Rng).
/// </summary>
public static class NativeRngV111Contexts
{
    /// <summary>Recognize the context-stream families constructed by this
    /// source-pinned helper. Seed inputs and request ownership are separate
    /// from this namespace check.</summary>
    public static bool IsContextStreamId(string streamId) =>
        !string.IsNullOrWhiteSpace(streamId) &&
        ((streamId is "context.act_1_map" or "context.act_2_map" or
            "context.act_3_map") ||
         streamId.StartsWith("context.encounter.", StringComparison.Ordinal) ||
         streamId.StartsWith("context.monster.", StringComparison.Ordinal) ||
         streamId.StartsWith("context.event.", StringComparison.Ordinal) ||
         streamId.StartsWith("context.content.", StringComparison.Ordinal));

    /// <summary>
    /// Native StandardActMap.CreateFor constructs
    /// Rng(runSeed, "act_{CurrentActIndex+1}_map").
    /// Act number is one-based; Overgrowth and Underdocks both use act 1.
    /// </summary>
    public static RngStreamState ForActMap(string seed, int actNumber)
    {
        if (actNumber is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(actNumber));
        var name = $"act_{actNumber}_map";
        return NativeRngV111.CreateContentStream(
            $"context.{name}", NativeRngV111.RunSeed(seed), name);
    }

    /// <summary>
    /// EncounterModel.GenerateMonstersWithSlots creates its own RNG from
    /// runSeed + TotalFloor + xxHash64(encounter.Id.Entry).
    /// Call once per encounter instance and retain the stream while the
    /// encounter is active. The ID is the pinned native model entry,
    /// including its original casing, rather than a localized name.
    /// </summary>
    public static RngStreamState ForEncounterFormation(
        string seed, string encounterEntry, int totalFloor)
    {
        var streamId = EncounterFormationStreamId(
            encounterEntry, totalFloor);
        return NativeRngV111.CreateContentStream(
            streamId,
            NativeRngV111.RunSeed(seed), encounterEntry, (ulong)totalFloor);
    }

    /// <summary>
    /// Return the source-owned identity of the encounter-local formation
    /// stream without deriving or inspecting its seed or cursor.
    /// </summary>
    public static string EncounterFormationStreamId(
        string encounterEntry, int totalFloor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterEntry);
        if (totalFloor < 0)
            throw new ArgumentOutOfRangeException(nameof(totalFloor));
        return $"context.encounter.{encounterEntry}.floor_{totalFloor}";
    }

    /// <summary>
    /// CombatState.CreateCreature initializes MonsterModel.Rng from
    /// runSeed + mapColumn + mapRow + CurrentActIndex + CombatId.
    /// CurrentActIndex is zero-based. Null map coordinates mean zero,
    /// as in native event/boss contexts without a current map point.
    /// This stream is separate from run.monster_ai and run.niche.
    /// </summary>
    public static RngStreamState ForMonster(
        string seed, int currentActIndex, long combatId,
        int? mapColumn = null, int? mapRow = null)
    {
        if (currentActIndex is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(currentActIndex));
        if (combatId < 0)
            throw new ArgumentOutOfRangeException(nameof(combatId));
        if (mapColumn is < 0 or > 6)
            throw new ArgumentOutOfRangeException(nameof(mapColumn));
        if (mapRow is < 0)
            throw new ArgumentOutOfRangeException(nameof(mapRow));
        var mixin = unchecked((ulong)currentActIndex + (ulong)combatId
            + (ulong)(mapColumn ?? 0) + (ulong)(mapRow ?? 0));
        return NativeRngV111.CreateStream(
            $"context.monster.{combatId}.act_{currentActIndex}.map_{mapColumn ?? 0}_{mapRow ?? 0}",
            unchecked(NativeRngV111.RunSeed(seed) + mixin));
    }

    /// <summary>
    /// EventModel.Begin creates an independent RNG from
    /// runSeed + (IsShared ? 0 : ownerSlot) + xxHash64(event.Id.Entry).
    /// The same event ID and owner recreate the same starting stream;
    /// native EventModel may retain and advance it across event pages.
    /// </summary>
    public static RngStreamState ForEvent(
        string seed, string eventEntry, int playerSlot = 0, bool shared = false)
    {
        return NativeRngV111.CreateContentStream(
            EventStreamId(eventEntry, playerSlot, shared),
            NativeRngV111.RunSeed(seed), eventEntry,
            shared ? 0UL : (ulong)playerSlot);
    }

    public static string EventStreamId(
        string eventEntry, int playerSlot = 0, bool shared = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventEntry);
        if (playerSlot < 0)
            throw new ArgumentOutOfRangeException(nameof(playerSlot));
        return $"context.event.{eventEntry}.{(shared ? "shared" : $"player_{playerSlot}")}";
    }

    /// <summary>
    /// Rng(Player, ModelId, mixin) combines run seed, player SLOT,
    /// xxHash64(model.Id.Entry), and caller-supplied mixin. The optional
    /// mixin distinguishes repeat occurrences of the same content.
    /// </summary>
    public static RngStreamState ForPlayerContent(
        string seed, string modelEntry, ulong mixin = 0, int playerSlot = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelEntry);
        if (playerSlot < 0)
            throw new ArgumentOutOfRangeException(nameof(playerSlot));
        return NativeRngV111.CreateContentStream(
            PlayerContentStreamId(modelEntry, mixin, playerSlot),
            NativeRngV111.RunSeed(seed), modelEntry,
            unchecked((ulong)playerSlot + mixin));
    }

    /// <summary>
    /// Return a player-content context stream identity without deriving its
    /// seed or reading its cursor. Pure law queries use this to validate the
    /// declared source namespace.
    /// </summary>
    public static string PlayerContentStreamId(
        string modelEntry, ulong mixin = 0, int playerSlot = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelEntry);
        if (playerSlot < 0)
            throw new ArgumentOutOfRangeException(nameof(playerSlot));
        return $"context.content.{modelEntry}.player_{playerSlot}.mixin_{mixin}";
    }
}
