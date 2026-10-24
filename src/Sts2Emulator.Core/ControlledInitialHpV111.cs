using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// Serializable, fork-isolated initial-monster-HP Nature boundary. This D3
/// slice deliberately does not impersonate an exact native-seeded combat:
/// controlled HP selections leave native cursors untouched and finish with
/// Combat.Stochastic.NativeRng = null. Other chance sites remain unsupported.
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
    private RandomIndexDecisionV111[] _transcript =
        CloneTranscript(Transcript);
    private RandomIndexRequestV111? _pending = CloneRequest(Pending);

    public RandomIndexDecisionV111[] Transcript
    {
        get => CloneTranscript(_transcript);
        init => _transcript = CloneTranscript(value);
    }

    public RandomIndexRequestV111? Pending
    {
        get => CloneRequest(_pending);
        init => _pending = CloneRequest(value);
    }

    [JsonIgnore]
    public bool Complete => Pending is null;

    public ControlledInitialHpStateV111 Fork() => this with
    {
        Combat = Combat.Fork(),
        Formation = Formation.Fork(),
        ReferenceStreams = ReferenceStreams.Fork(),
        Transcript = _transcript,
        Pending = _pending
    };

    internal ControlledInitialHpStateV111 ApplyChoice(
        RandomIndexDecisionV111 choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        choice.Validate();
        var pending = _pending ?? throw new InvalidOperationException(
            "No initial HP choice is pending.");
        if (choice.ProviderMode != "controlled-v111" ||
            choice.NativeCursorBefore is not null ||
            choice.NativeCursorAfter is not null ||
            choice.ProbabilityStatus != "UNVERIFIED" ||
            choice.Request.SiteId != "combat.initial_unique_hp" ||
            choice.Request.StreamId != "run.niche" ||
            choice.Request.Ordinal != NextEnemyIndex ||
            !ControlledStochasticChanceReplayV111.RequestsMatch(
                choice.Request, pending))
            throw new InvalidDataException(
                "Initial HP decision does not resolve its saved request.");

        var enemies = Combat.Enemies.Select(enemy => enemy.Fork()).ToArray();
        enemies[NextEnemyIndex] = enemies[NextEnemyIndex] with
        {
            Hp = choice.SelectedValue,
            MaxHp = choice.SelectedValue
        };
        var advanced = this with
        {
            Combat = Combat with { Enemies = enemies },
            NextEnemyIndex = NextEnemyIndex + 1,
            Transcript = _transcript.Append(CloneDecision(choice)).ToArray(),
            Pending = null
        };
        return advanced with
        {
            Pending = ControlledInitialHpV111.MakePending(advanced)
        };
    }

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    private static RandomIndexDecisionV111[] CloneTranscript(
        RandomIndexDecisionV111[] decisions) => decisions?.Select(
            decision => decision is null ? null! : CloneDecision(decision))
            .ToArray() ?? throw new ArgumentNullException(nameof(decisions));

    private static RandomIndexDecisionV111 CloneDecision(
        RandomIndexDecisionV111 decision) => decision with
    {
        Request = CloneRequest(decision.Request)!
    };

    private static RandomIndexRequestV111? CloneRequest(
        RandomIndexRequestV111? request) =>
        request is null
            ? null
            : request with
            {
                OrderedCandidates =
                    (int[])request.OrderedCandidates.Clone()
            };
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
        if (combat.Stochastic.NativeRng is not null)
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
        if (expected is null ||
            !ControlledChanceContinuationExecutorV111.RequestsMatch(
                expected, state.Pending!))
            throw new InvalidOperationException("Persisted pending eligibility is stale.");
        var working = state.Fork();
        var request = working.Pending!;
        return ControlledChanceContinuationExecutorV111
            .ResolveSelectedIndex<ControlledInitialHpStateV111,
                ControlledInitialHpStateV111>(
                working, request, selectedIndex, (checkpoint, replayedIndex) =>
                {
                    var choice = new RandomIndexDecisionV111(
                        request with
                        {
                            OrderedCandidates = (int[])
                                request.OrderedCandidates.Clone()
                        }, replayedIndex, "controlled-v111");
                    return checkpoint.ApplyChoice(choice);
                });
    }

    public static ControlledInitialHpStateV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var state = JsonSerializer.Deserialize<ControlledInitialHpStateV111>(json)
            ?? throw new InvalidDataException("Controlled HP snapshot was null.");
        ValidateOwners(state);
        if (!state.Complete &&
            !ControlledChanceContinuationExecutorV111.RequestsMatch(
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

    internal static RandomIndexRequestV111? MakePending(
        ControlledInitialHpStateV111 state)
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

    private static void ValidateOwners(ControlledInitialHpStateV111 state)
    {
        if (state.Mode != "controlled-v111-initial-hp-v1" ||
            state.Combat.Stochastic.NativeRng is not null ||
            state.NextEnemyIndex < 0 ||
            state.NextEnemyIndex > state.Combat.Enemies.Length ||
            state.Formation.Enemies.Length != state.Combat.Enemies.Length ||
            state.Formation.MonsterContexts.Length != state.Combat.Enemies.Length ||
            state.Transcript.Length != state.NextEnemyIndex)
            throw new InvalidDataException("Invalid controlled HP continuation ownership.");
        // Every player-controlled chance boundary is bound to one native
        // creature identity. Two monster contexts with the same CombatId
        // would make the choice transcript ambiguous after fork/restore.
        var seenCreatureOwners = new HashSet<long>();
        for (int i = 0; i < state.Combat.Enemies.Length; i++)
        {
            var e = state.Combat.Enemies[i];
            var spec = state.Formation.Enemies[i];
            var owner = state.Formation.MonsterContexts[i];
            if (e.EnemyId != spec.EnemyId ||
                e.FormationPosition != spec.FormationPosition ||
                e.SlotName != spec.SlotName ||
                owner.Enemy != spec || owner.CombatId < 0 ||
                !seenCreatureOwners.Add(owner.CombatId))
                throw new InvalidDataException("Controlled HP creature ownership mismatch or duplicate CombatId.");
            if (i < state.NextEnemyIndex)
            {
                var choice = state.Transcript[i];
                choice.Validate();
                var used = state.Combat.Enemies.Take(i)
                    .Select(previous => previous.MaxHp).ToHashSet();
                var expected = new RandomIndexRequestV111(
                    "combat.initial_unique_hp", "run.niche",
                    "creature:" + owner.CombatId, i,
                    NativeCombatRngEntryV111.InitialHpPool(state.Combat, e, used));
                if (choice.ProviderMode != "controlled-v111" ||
                    choice.NativeCursorBefore is not null ||
                    choice.NativeCursorAfter is not null ||
                    !ControlledChanceContinuationExecutorV111.RequestsMatch(
                        expected, choice.Request) ||
                    e.Hp != choice.SelectedValue || e.MaxHp != choice.SelectedValue)
                    throw new InvalidDataException("Controlled HP transcript does not match source eligibility/state.");
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
        _ = NativeRngV111.ToSnapshot(
            state.ReferenceStreams.GetStream("run.niche"));
    }
}
