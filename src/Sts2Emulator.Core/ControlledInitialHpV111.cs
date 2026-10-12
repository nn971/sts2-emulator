using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// Serializable, fork-isolated initial-monster-HP Nature boundary. This D3
/// slice deliberately does not impersonate an exact native-seeded combat:
/// controlled HP selections leave native cursors untouched and finish with
/// Combat.NativeRng = null. Other chance sites remain unsupported.
/// </summary>
public sealed record ControlledInitialHpStateV111(
    CombatState Combat,
    NativeEncounterFormationV111 Formation,
    RngBundle ReferenceStreams,
    int NextEnemyIndex,
    RandomIndexDecisionV111[] Transcript,
    RandomIndexRequestV111? Pending,
    string Mode = "controlled-v111-initial-hp-v1")
{
    [JsonIgnore]
    public bool Complete => Pending is null;

    public ControlledInitialHpStateV111 Fork() => this with
    {
        Combat = Combat.Fork(),
        Formation = Formation.Fork(),
        ReferenceStreams = ReferenceStreams.Fork(),
        Transcript = Transcript.Select(d => d with
        {
            Request = d.Request with
            {
                OrderedCandidates = (int[])d.Request.OrderedCandidates.Clone()
            }
        }).ToArray(),
        Pending = Pending is null ? null : Pending with
        {
            OrderedCandidates = (int[])Pending.OrderedCandidates.Clone()
        }
    };

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);
}

public static class ControlledInitialHpV111
{
    public static ControlledInitialHpStateV111 Begin(
        CombatState combat, NativeEncounterFormationV111 formation,
        RngBundle referenceStreams)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(formation);
        ArgumentNullException.ThrowIfNull(referenceStreams);
        if (combat.NativeRng is not null)
            throw new InvalidOperationException("Controlled entry requires unattached combat.");
        if (combat.Enemies.Length != formation.Enemies.Length ||
            formation.MonsterContexts.Length != combat.Enemies.Length)
            throw new InvalidOperationException("Formation and combat sizes disagree.");
        var next = new ControlledInitialHpStateV111(
            combat.Fork(), formation.Fork(), referenceStreams.Fork(),
            0, [], null);
        ValidateOwners(next);
        return next with { Pending = MakePending(next) };
    }

    public static ControlledInitialHpStateV111 Resolve(
        ControlledInitialHpStateV111 state, string expectedFingerprint,
        int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedFingerprint);
        if (state.Complete)
            throw new InvalidOperationException("No pending Nature boundary to resolve.");
        // Strict stale / fork / repeated-command guard, before any mutation.
        if (!string.Equals(expectedFingerprint, state.Fingerprint,
                StringComparison.Ordinal))
            throw new InvalidOperationException("Stale or mismatched Nature boundary fingerprint.");
        ValidateOwners(state);
        var expected = MakePending(state);
        if (expected is null || !SameRequest(expected, state.Pending!))
            throw new InvalidOperationException("Persisted pending eligibility is stale.");
        var working = state.Fork();
        var request = working.Pending!;
        var choice = new ControlledRandomIndexProviderV111(selectedIndex)
            .Resolve(request, working.ReferenceStreams);
        int hp = choice.SelectedValue;
        var enemies = working.Combat.Enemies.Select(e => e.Fork()).ToArray();
        enemies[working.NextEnemyIndex] = enemies[working.NextEnemyIndex] with
        {
            Hp = hp, MaxHp = hp
        };
        var next = working with
        {
            Combat = working.Combat with { Enemies = enemies },
            NextEnemyIndex = working.NextEnemyIndex + 1,
            Transcript = working.Transcript.Append(choice).ToArray(),
            Pending = null
        };
        return next with { Pending = MakePending(next) };
    }

    public static ControlledInitialHpStateV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var state = JsonSerializer.Deserialize<ControlledInitialHpStateV111>(json)
            ?? throw new InvalidDataException("Controlled HP snapshot was null.");
        ValidateOwners(state);
        if (!state.Complete && !SameRequest(
                MakePending(state) ?? throw new InvalidDataException("Missing pending owner."),
                state.Pending!))
            throw new InvalidDataException("Controlled HP pending eligibility mismatch.");
        if (state.Complete && state.NextEnemyIndex != state.Combat.Enemies.Length)
            throw new InvalidDataException("Controlled HP completed too early.");
        return state.Fork();
    }

    public static string Save(ControlledInitialHpStateV111 state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateOwners(state);
        return JsonSerializer.Serialize(state);
    }

    private static RandomIndexRequestV111? MakePending(ControlledInitialHpStateV111 state)
    {
        if (state.NextEnemyIndex == state.Combat.Enemies.Length) return null;
        var owner = state.Formation.MonsterContexts[state.NextEnemyIndex];
        var used = state.Combat.Enemies.Take(state.NextEnemyIndex)
            .Select(e => e.MaxHp).ToHashSet();
        var candidates = NativeCombatRngEntryV111.InitialHpPool(
            state.Combat, state.Combat.Enemies[state.NextEnemyIndex], used);
        return new RandomIndexRequestV111(
            "combat.initial_unique_hp", "run.niche",
            "creature:" + owner.CombatId, state.NextEnemyIndex, candidates);
    }

    private static bool SameRequest(
        RandomIndexRequestV111 a, RandomIndexRequestV111 b) =>
        a.SiteId == b.SiteId && a.StreamId == b.StreamId &&
        a.OwnerId == b.OwnerId && a.Ordinal == b.Ordinal &&
        a.SourceVersion == b.SourceVersion &&
        a.OperationKind == b.OperationKind &&
        a.BoundedCount == b.BoundedCount &&
        a.OrderedCandidates.SequenceEqual(b.OrderedCandidates);

    private static void ValidateOwners(ControlledInitialHpStateV111 state)
    {
        if (state.Mode != "controlled-v111-initial-hp-v1" ||
            state.Combat.NativeRng is not null ||
            state.NextEnemyIndex < 0 ||
            state.NextEnemyIndex > state.Combat.Enemies.Length ||
            state.Formation.Enemies.Length != state.Combat.Enemies.Length ||
            state.Formation.MonsterContexts.Length != state.Combat.Enemies.Length ||
            state.Transcript.Length != state.NextEnemyIndex)
            throw new InvalidDataException("Invalid controlled HP continuation ownership.");
        for (int i = 0; i < state.Combat.Enemies.Length; i++)
        {
            var e = state.Combat.Enemies[i];
            var spec = state.Formation.Enemies[i];
            var owner = state.Formation.MonsterContexts[i];
            if (e.EnemyId != spec.EnemyId ||
                e.FormationPosition != spec.FormationPosition ||
                e.SlotName != spec.SlotName ||
                owner.Enemy != spec || owner.CombatId < 0)
                throw new InvalidDataException("Controlled HP creature ownership mismatch.");
            if (i < state.NextEnemyIndex)
            {
                var choice = state.Transcript[i];
                choice.Validate();
                if (choice.ProviderMode != "controlled-v111" ||
                    choice.NativeCursorBefore is not null ||
                    choice.NativeCursorAfter is not null ||
                    choice.Request.Ordinal != i ||
                    choice.Request.OwnerId != "creature:" + owner.CombatId ||
                    e.Hp != choice.SelectedValue || e.MaxHp != choice.SelectedValue)
                    throw new InvalidDataException("Controlled HP transcript does not match current state.");
            }
        }
        if ((state.Pending is null) !=
            (state.NextEnemyIndex == state.Combat.Enemies.Length))
        {
            // Begin constructs its initial pending boundary immediately after
            // validating the new empty state, so that special case is valid.
            if (!(state.NextEnemyIndex == 0 && state.Transcript.Length == 0 &&
                state.Pending is null))
                throw new InvalidDataException("Controlled HP pending stage mismatch.");
        }
        _ = NativeRngV111.ToSnapshot(state.ReferenceStreams.Streams.Single(
            s => s.StreamId == "run.niche"));
    }
}
