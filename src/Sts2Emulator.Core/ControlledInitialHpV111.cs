using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// A persisted chance boundary during initial enemy HP assignment. Identity
/// is derived from the exact partial state, formation and consumed transcript;
/// it does not depend on a C# stack frame or an in-memory object address.
/// </summary>
public sealed record PendingInitialHpChoiceV111(
    string BoundaryId, RandomIndexRequestV111 Request)
{
    public PendingInitialHpChoiceV111 Fork() => this with
    {
        Request = Request with
        {
            OrderedCandidates = (int[])Request.OrderedCandidates.Clone()
        }
    };
}

/// <summary>
/// Complete, forkable mechanics continuation for the initial-HP site only.
/// Combat.Enemies[0..NextEnemyIndex) already have their selected HP applied.
/// The next candidate pool is generated AFTER those effects. Other mechanics
/// (intent state machines, hooks, combat turns) are not resumable yet.
/// </summary>
public sealed record ControlledInitialHpContinuationV111(
    string SchemaId,
    string ProviderMode,
    CombatState Combat,
    NativeEncounterFormationV111 Formation,
    int NextEnemyIndex,
    PendingInitialHpChoiceV111? Pending,
    RandomIndexDecisionV111[] Transcript)
{
    public bool Complete => Pending is null;

    public ControlledInitialHpContinuationV111 Fork() => this with
    {
        Combat = Combat.Fork(),
        Formation = Formation.Fork(),
        Pending = Pending?.Fork(),
        Transcript = Transcript.Select(x => x with
        {
            Request = x.Request with
            {
                OrderedCandidates = (int[])x.Request.OrderedCandidates.Clone()
            }
        }).ToArray()
    };
}

/// <summary>
/// D3 first vertical slice: genuine suspension/resumption inside the source
/// shaped initial monster HP *mechanic*. The native and controlled paths
/// share pool eligibility, owner validation and applied HP mutation.
/// It does not attach a misleading NativeCombatRngV111 sidecar to a
/// counterfactual combat or permit unimplemented normal combat continuation.
/// </summary>
public static class ControlledInitialHpV111
{
    public const string SchemaId = "sts2-v111-controlled-initial-hp-v1";
    public const string ProviderMode = "controlled-v111";
    public const string SiteId = "combat.initial_unique_hp";

    public static ControlledInitialHpContinuationV111 Begin(
        CombatState initial, NativeEncounterFormationV111 formation)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(formation);
        if (initial.NativeRng is not null)
            throw new InvalidOperationException(
                "Controlled initial HP must start before native RNG is attached.");
        if (initial.Enemies.Length != formation.Enemies.Length ||
            formation.Enemies.Length != formation.MonsterContexts.Length)
            throw new InvalidOperationException(
                "Initial enemy/formation/owner counts must agree.");
        if (formation.NativeActIndex is { } nativeAct &&
            nativeAct != initial.Act - 1)
            throw new InvalidOperationException(
                "Native encounter act index belongs to another combat.");
        _ = NativeRngV111.ToSnapshot(formation.EncounterRng);
        for (var index = 0; index < initial.Enemies.Length; index++)
            NativeCombatRngEntryV111.ValidateInitialHpEnemyBinding(
                initial.Enemies[index],
                formation.Enemies[index],
                formation.MonsterContexts[index]);
        var frozen = initial.Fork();
        var ownedFormation = formation.Fork();
        var result = Create(frozen, ownedFormation, 0, []);
        Validate(result);
        return result;
    }

    public static ControlledInitialHpContinuationV111 Resolve(
        ControlledInitialHpContinuationV111 state,
        string boundaryId, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryId);
        Validate(state);
        var pending = state.Pending ??
            throw new InvalidOperationException("Initial-HP continuation already complete.");
        if (!StringComparer.Ordinal.Equals(boundaryId, pending.BoundaryId))
            throw new InvalidOperationException(
                "Stale or wrong Nature boundary ID; no state was advanced.");

        // Validate choice before applying any mechanics. This provider is
        // intentionally cursorless and must not mutate native seeded streams.
        var selected = new ControlledRandomIndexProviderV111(selectedIndex)
            .Resolve(pending.Request, RngBundle.Empty);
        var combat = state.Combat.Fork();
        int index = state.NextEnemyIndex;
        var enemies = combat.Enemies;
        enemies[index] = NativeCombatRngEntryV111.ApplyInitialHp(
            enemies[index], selected.SelectedValue);
        combat = combat with { Enemies = enemies };
        var transcript = state.Transcript.Select(x => x with
        {
            Request = x.Request with
            {
                OrderedCandidates = (int[])x.Request.OrderedCandidates.Clone()
            }
        }).Append(selected).ToArray();
        var next = Create(combat, state.Formation.Fork(), index + 1, transcript);
        Validate(next);
        return next;
    }

    public static void Validate(ControlledInitialHpContinuationV111 state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SchemaId != SchemaId || state.ProviderMode != ProviderMode ||
            state.Combat.NativeRng is not null ||
            state.Combat.Enemies.Length != state.Formation.Enemies.Length ||
            state.Formation.Enemies.Length != state.Formation.MonsterContexts.Length ||
            state.NextEnemyIndex < 0 ||
            state.NextEnemyIndex > state.Combat.Enemies.Length ||
            state.Transcript.Length != state.NextEnemyIndex ||
            state.Formation.NativeActIndex is { } act &&
                act != state.Combat.Act - 1)
            throw new InvalidDataException(
                "Invalid controlled initial-HP continuation metadata.");
        _ = NativeRngV111.ToSnapshot(state.Formation.EncounterRng);

        var consumed = new HashSet<int>();
        for (int index = 0; index < state.Combat.Enemies.Length; index++)
        {
            var enemy = state.Combat.Enemies[index];
            var owner = state.Formation.MonsterContexts[index];
            NativeCombatRngEntryV111.ValidateInitialHpEnemyBinding(
                enemy, state.Formation.Enemies[index], owner);
            if (index >= state.NextEnemyIndex) continue;
            var expected = MakeRequest(state.Combat, state.Formation, index, consumed);
            var recorded = state.Transcript[index];
            recorded.Validate();
            if (recorded.ProviderMode != ProviderMode ||
                recorded.NativeCursorBefore is not null ||
                recorded.NativeCursorAfter is not null ||
                recorded.Request.SiteId != expected.SiteId ||
                recorded.Request.StreamId != expected.StreamId ||
                recorded.Request.OwnerId != expected.OwnerId ||
                recorded.Request.Ordinal != expected.Ordinal ||
                recorded.Request.SourceVersion != expected.SourceVersion ||
                recorded.Request.OperationKind != expected.OperationKind ||
                recorded.Request.BoundedCount != expected.BoundedCount ||
                !recorded.Request.OrderedCandidates.SequenceEqual(
                    expected.OrderedCandidates) ||
                enemy.Hp != recorded.SelectedValue ||
                enemy.MaxHp != recorded.SelectedValue)
                throw new InvalidDataException(
                    "Controlled initial-HP transcript disagrees with applied mechanics.");
            consumed.Add(enemy.MaxHp);
        }

        if (state.NextEnemyIndex == state.Combat.Enemies.Length)
        {
            if (state.Pending is not null)
                throw new InvalidDataException(
                    "Completed initial-HP continuation has a pending choice.");
            return;
        }

        var request = MakeRequest(
            state.Combat, state.Formation, state.NextEnemyIndex, consumed);
        var pending = state.Pending ??
            throw new InvalidDataException(
                "Unfinished initial-HP continuation is missing its choice.");
        pending.Request.Validate();
        if (pending.Request.SiteId != request.SiteId ||
            pending.Request.StreamId != request.StreamId ||
            pending.Request.OwnerId != request.OwnerId ||
            pending.Request.Ordinal != request.Ordinal ||
            pending.Request.SourceVersion != request.SourceVersion ||
            pending.Request.OperationKind != request.OperationKind ||
            pending.Request.BoundedCount != request.BoundedCount ||
            !pending.Request.OrderedCandidates.SequenceEqual(request.OrderedCandidates) ||
            pending.BoundaryId != ComputeBoundaryId(
                state.Combat, state.Formation, state.NextEnemyIndex,
                state.Transcript, request))
            throw new InvalidDataException(
                "Pending Nature boundary is stale or its eligible outcomes changed.");
    }

    public static ControlledInitialHpContinuationV111 Restore(string snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot);
        var state = JsonSerializer.Deserialize<ControlledInitialHpContinuationV111>(snapshot)
            ?? throw new InvalidDataException("Empty controlled HP snapshot.");
        Validate(state);
        return state.Fork();
    }

    public static string Save(ControlledInitialHpContinuationV111 state)
    {
        Validate(state);
        return JsonSerializer.Serialize(state);
    }

    private static ControlledInitialHpContinuationV111 Create(
        CombatState combat, NativeEncounterFormationV111 formation,
        int nextEnemyIndex, RandomIndexDecisionV111[] transcript)
    {
        PendingInitialHpChoiceV111? pending = null;
        if (nextEnemyIndex < combat.Enemies.Length)
        {
            var used = transcript.Select(x => x.SelectedValue).ToHashSet();
            var request = MakeRequest(combat, formation, nextEnemyIndex, used);
            pending = new PendingInitialHpChoiceV111(
                ComputeBoundaryId(
                    combat, formation, nextEnemyIndex, transcript, request),
                request);
        }
        return new ControlledInitialHpContinuationV111(
            SchemaId, ProviderMode, combat, formation,
            nextEnemyIndex, pending, transcript);
    }

    private static RandomIndexRequestV111 MakeRequest(
        CombatState combat, NativeEncounterFormationV111 formation,
        int index, IReadOnlySet<int> used)
    {
        var pool = NativeCombatRngEntryV111.InitialHpCandidatePool(
            combat, combat.Enemies[index], used);
        return new RandomIndexRequestV111(
            SiteId, "run.niche",
            "creature:" + formation.MonsterContexts[index].CombatId,
            index, pool);
    }

    private static string ComputeBoundaryId(
        CombatState combat, NativeEncounterFormationV111 formation,
        int index, RandomIndexDecisionV111[] transcript,
        RandomIndexRequestV111 request) =>
        CanonicalJson.Sha256(new
        {
            SchemaId,
            ProviderMode,
            CombatHash = CanonicalJson.Sha256(combat),
            FormationHash = CanonicalJson.Sha256(formation),
            NextIndex = index,
            SourceSite = request.SiteId,
            SourceOwner = request.OwnerId,
            CandidateValues = request.OrderedCandidates,
            Prefix = transcript.Select(x => x.SelectedIndex).ToArray()
        });
}
