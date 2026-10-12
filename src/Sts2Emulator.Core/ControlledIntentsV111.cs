using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// Counterfactual, serializable enemy-intent commitment. Deterministic
/// preceding enemies are already committed in Combat; only an unfinished
/// enemy's PURE move selector is re-evaluated against its saved ticket prefix.
/// Game actions, power hooks and previous enemy commits are never replayed.
/// Native RNG cursors are carried for provenance only, never advanced.
/// </summary>
public sealed record ControlledIntentStateV111(
    CombatState BaselineCombat,
    CombatState Combat,
    NativeEncounterFormationV111 Formation,
    RngBundle ReferenceStreams,
    int NextEnemyIndex,
    RandomIndexDecisionV111[] Transcript,
    RandomIndexRequestV111? Pending,
    string Mode = "controlled-v111-committed-intent-v1")
{
    [JsonIgnore]
    public bool Complete => Pending is null;

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    public ControlledIntentStateV111 Fork() => this with
    {
        BaselineCombat = BaselineCombat.Fork(),
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
}

public static class ControlledIntentsV111
{
    public static ControlledIntentStateV111 Begin(CombatState nativePrepared)
    {
        ArgumentNullException.ThrowIfNull(nativePrepared);
        NativeCombatRngEntryV111.Validate(nativePrepared);
        var native = nativePrepared.NativeRng ??
            throw new InvalidOperationException("Intent boundary requires a native-owned combat source.");
        var baseline = nativePrepared.Fork() with { NativeRng = null };
        var start = new ControlledIntentStateV111(
            baseline.Fork(), baseline, native.Formation.Fork(),
            native.RunStreams.Fork(), 0, [], null);
        return AdvanceToBoundary(start);
    }

    public static ControlledIntentStateV111 Resolve(
        ControlledIntentStateV111 state, string expectedFingerprint, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedFingerprint);
        if (state.Complete)
            throw new InvalidOperationException("No pending enemy intent to resolve.");
        if (expectedFingerprint != state.Fingerprint)
            throw new InvalidOperationException("Stale or mismatched Nature fingerprint.");
        Verify(state);
        return ApplyUnchecked(state, selectedIndex);
    }

    public static string Save(ControlledIntentStateV111 state)
    {
        Verify(state);
        return JsonSerializer.Serialize(state);
    }

    public static ControlledIntentStateV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var state = JsonSerializer.Deserialize<ControlledIntentStateV111>(json)
            ?? throw new InvalidDataException("Null intent continuation");
        Verify(state);
        return state.Fork();
    }

    private static ControlledIntentStateV111 ApplyUnchecked(
        ControlledIntentStateV111 state, int selectedIndex)
    {
        var request = state.Pending ??
            throw new InvalidOperationException("Pending intent unavailable.");
        var choice = new ControlledRandomIndexProviderV111(selectedIndex)
            .Resolve(request, state.ReferenceStreams);
        return AdvanceToBoundary(state.Fork() with
        {
            Transcript = state.Transcript.Append(choice).ToArray(),
            Pending = null
        });
    }

    private sealed class ChoiceBoundary(RandomIndexRequestV111 request) : Exception
    {
        public RandomIndexRequestV111 Request { get; } = request;
    }

    private static ControlledIntentStateV111 AdvanceToBoundary(ControlledIntentStateV111 start)
    {
        var next = start.Fork();
        var enemies = next.Combat.Enemies.Select(e => e.Fork()).ToArray();
        int index = next.NextEnemyIndex;
        while (index < enemies.Length)
        {
            var enemy = enemies[index];
            string ownerId = "creature:" + enemy.InstanceId;
            var prefix = next.Transcript.Where(t => t.Request.OwnerId == ownerId).ToArray();
            int prefixCursor = 0;
            Func<int, int> ticket = maxExclusive =>
            {
                if (maxExclusive <= 0)
                    throw new InvalidOperationException("Nonpositive native AI ticket range.");
                var request = new RandomIndexRequestV111(
                    "combat.committed_intent.index_draw", "run.monster_ai",
                    ownerId, next.Transcript.Length - prefix.Length + prefixCursor,
                    [], OperationKind: "bounded_integer", BoundedCount: maxExclusive);
                if (prefixCursor >= prefix.Length)
                    throw new ChoiceBoundary(request);
                var recorded = prefix[prefixCursor++];
                if (!SameRequest(recorded.Request, request) ||
                    recorded.ProviderMode != "controlled-v111" ||
                    recorded.NativeCursorBefore is not null ||
                    recorded.NativeCursorAfter is not null)
                    throw new InvalidDataException("Saved AI ticket prefix diverged from source eligibility.");
                recorded.Validate();
                return recorded.SelectedIndex;
            };
            try
            {
                var selected = PrototypeGameEngine.EvaluateEnemyIntentChoice(
                    enemies, index, next.Formation.NativeEncounterEntry, ticket);
                if (prefixCursor != prefix.Length)
                    throw new InvalidDataException("Unused tickets for completed AI selection.");
                enemies[index] = enemy with
                {
                    PlannedMoveIndex = selected.MoveIndex,
                    PlannedNextAiStateId = selected.NextAiStateId
                };
                index++;
            }
            catch (ChoiceBoundary pending)
            {
                return next with
                {
                    Combat = next.Combat with { Enemies = enemies },
                    NextEnemyIndex = index,
                    Pending = pending.Request
                };
            }
        }
        return next with
        {
            Combat = next.Combat with { Enemies = enemies },
            NextEnemyIndex = index,
            Pending = null
        };
    }

    private static void Verify(ControlledIntentStateV111 state)
    {
        if (state.Mode != "controlled-v111-committed-intent-v1" ||
            state.BaselineCombat.NativeRng is not null ||
            state.Combat.NativeRng is not null ||
            state.NextEnemyIndex < 0 ||
            state.NextEnemyIndex > state.Combat.Enemies.Length ||
            state.Transcript is null || state.Formation is null ||
            state.ReferenceStreams is null ||
            state.Formation.Enemies.Length != state.Combat.Enemies.Length ||
            state.Formation.MonsterContexts.Length != state.Combat.Enemies.Length)
            throw new InvalidDataException("Invalid controlled intent continuation owner/stage.");
        var niche = state.ReferenceStreams.Streams.Single(s => s.StreamId == "run.niche");
        var intent = state.ReferenceStreams.Streams.Single(s => s.StreamId == "run.monster_ai");
        _ = NativeRngV111.ToSnapshot(niche);
        _ = NativeRngV111.ToSnapshot(intent);
        if (state.BaselineCombat.Enemies.Length != state.Combat.Enemies.Length)
            throw new InvalidDataException("Intent baseline enemy count differs.");
        for (int i = 0; i < state.Combat.Enemies.Length; i++)
        {
            var original = state.BaselineCombat.Enemies[i];
            var spec = state.Formation.Enemies[i];
            var context = state.Formation.MonsterContexts[i];
            if (original.EnemyId != spec.EnemyId ||
                original.FormationPosition != spec.FormationPosition ||
                original.SlotName != spec.SlotName ||
                original.InstanceId != context.CombatId ||
                context.Enemy != spec)
                throw new InvalidDataException("Intent baseline creature ownership changed.");
        }

        var replay = AdvanceToBoundary(state with
        {
            Combat = state.BaselineCombat.Fork(),
            NextEnemyIndex = 0,
            Transcript = [],
            Pending = null
        });
        foreach (var choice in state.Transcript)
        {
            if (replay.Pending is null ||
                !SameRequest(replay.Pending, choice.Request) ||
                choice.ProviderMode != "controlled-v111" ||
                choice.NativeCursorAfter is not null ||
                choice.NativeCursorBefore is not null)
                throw new InvalidDataException("Intent transcript not reachable from baseline.");
            replay = ApplyUnchecked(replay, choice.SelectedIndex);
        }
        if (CanonicalJson.Sha256(replay) != CanonicalJson.Sha256(state))
            throw new InvalidDataException("Controlled intent state was not source-reachable.");
    }

    private static bool SameRequest(RandomIndexRequestV111 a, RandomIndexRequestV111 b) =>
        a.SiteId == b.SiteId && a.StreamId == b.StreamId &&
        a.OwnerId == b.OwnerId && a.Ordinal == b.Ordinal &&
        a.OperationKind == b.OperationKind &&
        a.SourceVersion == b.SourceVersion &&
        a.BoundedCount == b.BoundedCount &&
        a.OrderedCandidates.SequenceEqual(b.OrderedCandidates);
}
