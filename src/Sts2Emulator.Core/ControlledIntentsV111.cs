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
    string Mode = "controlled-v111-committed-intent-v1",
    RandomIndexDecisionV111[]? PriorHpTranscript = null)
{
    private RandomIndexDecisionV111[] _transcript =
        CloneTranscript(Transcript);
    private RandomIndexRequestV111? _pending = CloneRequest(Pending);
    private RandomIndexDecisionV111[]? _priorHpTranscript =
        CloneOptionalTranscript(PriorHpTranscript);

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

    public RandomIndexDecisionV111[]? PriorHpTranscript
    {
        get => CloneOptionalTranscript(_priorHpTranscript);
        init => _priorHpTranscript = CloneOptionalTranscript(value);
    }

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
        PriorHpTranscript = _priorHpTranscript,
        Transcript = _transcript,
        Pending = _pending
    };

    internal ControlledIntentStateV111 AppendResolvedDecision(
        RandomIndexDecisionV111 choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        choice.Validate();
        var pending = _pending ?? throw new InvalidOperationException(
            "No pending controlled intent request can be resolved.");
        if (choice.ProviderMode != "controlled-v111" ||
            choice.NativeCursorBefore is not null ||
            choice.NativeCursorAfter is not null ||
            choice.ProbabilityStatus != "UNVERIFIED" ||
            choice.Request.SiteId !=
                NativeCombatRngEntryV111.CommittedIntentSiteId ||
            choice.Request.StreamId != "run.monster_ai" ||
            choice.Request.Ordinal != _transcript.Length ||
            !StringComparer.Ordinal.Equals(
                CanonicalJson.Sha256(choice.Request),
                CanonicalJson.Sha256(pending)))
            throw new InvalidDataException(
                "Controlled intent decision does not resolve the saved request.");
        return this with
        {
            Transcript = _transcript.Append(CloneDecision(choice)).ToArray(),
            Pending = null
        };
    }

    private static RandomIndexDecisionV111[] CloneTranscript(
        RandomIndexDecisionV111[] decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        return decisions.Select(decision => decision with
        {
            Request = CloneRequest(decision.Request)!
        }).ToArray();
    }

    private static RandomIndexDecisionV111[]? CloneOptionalTranscript(
        RandomIndexDecisionV111[]? decisions) =>
        decisions is null ? null : CloneTranscript(decisions);

    private static RandomIndexRequestV111? CloneRequest(
        RandomIndexRequestV111? request) =>
        request is null
            ? null
            : request with
            {
                OrderedCandidates =
                    (int[])request.OrderedCandidates.Clone()
            };

    private static RandomIndexDecisionV111 CloneDecision(
        RandomIndexDecisionV111 decision) => decision with
    {
        Request = CloneRequest(decision.Request)!
    };
}

public static class ControlledIntentsV111
{
    public static ControlledIntentStateV111 Begin(CombatState nativePrepared)
    {
        ArgumentNullException.ThrowIfNull(nativePrepared);
        NativeCombatRngEntryV111.Validate(nativePrepared);
        var native = nativePrepared.Stochastic.NativeRng ??
            throw new InvalidOperationException("Intent boundary requires a native-owned combat source.");
        var combatWithoutNativeRng = nativePrepared.Fork();
        combatWithoutNativeRng = combatWithoutNativeRng with
        {
            Stochastic = CombatStochasticStateV111.WithNativeRng(
                combatWithoutNativeRng.Stochastic, null)
        };
        return BeginPrepared(combatWithoutNativeRng,
            native.Formation, native.RunStreams, null);
    }

    /// <summary>
    /// Compose the actual completed controlled HP results into the next
    /// source-shaped intent commitment. No native HP draws are rerun and
    /// the controlled HP transcript remains inside the persisted child.
    /// </summary>
    public static ControlledIntentStateV111 BeginAfterControlledHp(
        ControlledInitialHpStateV111 hp)
    {
        ArgumentNullException.ThrowIfNull(hp);
        var verified = ControlledInitialHpV111.Restore(
            ControlledInitialHpV111.Save(hp));
        if (!verified.Complete)
            throw new InvalidOperationException(
                "Finish all pending HP decisions before committing intents.");
        return BeginPrepared(verified.Combat, verified.Formation,
            verified.ReferenceStreams, verified.Transcript);
    }

    private static ControlledIntentStateV111 BeginPrepared(
        CombatState combat, NativeEncounterFormationV111 formation,
        RngBundle referenceStreams, RandomIndexDecisionV111[]? priorHp)
    {
        if (combat.Stochastic.NativeRng is not null)
            throw new InvalidOperationException(
                "Controlled intent baseline cannot own a fake native RNG cursor.");
        var baseline = combat.Fork();
        var start = new ControlledIntentStateV111(
            baseline.Fork(), baseline, formation.Fork(),
            referenceStreams.Fork(), 0, [], null,
            PriorHpTranscript: priorHp?.Select(d => d with
            {
                Request = d.Request with
                {
                    OrderedCandidates = (int[])d.Request.OrderedCandidates.Clone()
                }
            }).ToArray());
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
        return ControlledChanceContinuationExecutorV111
            .ResolveSelectedIndex<ControlledIntentStateV111,
                ControlledIntentStateV111>(
                state, request, selectedIndex, (checkpoint, replayedIndex) =>
                {
                    var choice = new RandomIndexDecisionV111(
                        request with
                        {
                            OrderedCandidates = (int[])
                                request.OrderedCandidates.Clone()
                        }, replayedIndex, "controlled-v111");
                    return AdvanceToBoundary(checkpoint.Fork()
                        .AppendResolvedDecision(choice));
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
                if (!ControlledChanceContinuationExecutorV111.RequestsMatch(
                        recorded.Request, request) ||
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
                enemies[index] = PrototypeGameEngine
                    .ApplyEnemyIntentOutcome(
                        enemy, selected.MoveIndex, selected.NextAiStateId);
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
            state.BaselineCombat.Stochastic.NativeRng is not null ||
            state.Combat.Stochastic.NativeRng is not null ||
            state.NextEnemyIndex < 0 ||
            state.NextEnemyIndex > state.Combat.Enemies.Length ||
            state.Transcript is null || state.Formation is null ||
            state.ReferenceStreams is null ||
            state.Formation.Enemies.Length != state.Combat.Enemies.Length ||
            state.Formation.MonsterContexts.Length != state.Combat.Enemies.Length)
            throw new InvalidDataException("Invalid controlled intent continuation owner/stage.");
        var niche = state.ReferenceStreams.GetStream("run.niche");
        var intent = state.ReferenceStreams.GetStream("run.monster_ai");
        _ = NativeRngV111.ToSnapshot(niche);
        _ = NativeRngV111.ToSnapshot(intent);
        if (state.BaselineCombat.Enemies.Length != state.Combat.Enemies.Length)
            throw new InvalidDataException("Intent baseline enemy count differs.");
        // The source-eligible prior HP history must agree with the actual
        // initial HP values in the completed controlled-entry baseline.
        // Intent choices alone do not erase Nature's earlier decisions.
        if (state.PriorHpTranscript is { } earlier)
        {
            if (earlier.Length != state.BaselineCombat.Enemies.Length)
                throw new InvalidDataException("Incomplete HP provenance before intent stage.");
            var usedHp = new HashSet<int>();
            for (int i = 0; i < earlier.Length; i++)
            {
                var enemy = state.BaselineCombat.Enemies[i];
                var context = state.Formation.MonsterContexts[i];
                var expectedRequest = new RandomIndexRequestV111(
                    "combat.initial_unique_hp", "run.niche",
                    "creature:" + context.CombatId, i,
                    NativeCombatRngEntryV111.InitialHpPool(
                        state.BaselineCombat, enemy, usedHp));
                var decision = earlier[i];
                decision.Validate();
                if (!ControlledChanceContinuationExecutorV111.RequestsMatch(
                        expectedRequest, decision.Request) ||
                    decision.ProviderMode != "controlled-v111" ||
                    decision.NativeCursorBefore is not null ||
                    decision.NativeCursorAfter is not null ||
                    enemy.Hp != decision.SelectedValue ||
                    enemy.MaxHp != decision.SelectedValue)
                    throw new InvalidDataException("HP-to-intent provenance is not source-reachable.");
                usedHp.Add(decision.SelectedValue);
            }
        }
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
                !ControlledChanceContinuationExecutorV111.RequestsMatch(
                    replay.Pending, choice.Request) ||
                choice.ProviderMode != "controlled-v111" ||
                choice.NativeCursorAfter is not null ||
                choice.NativeCursorBefore is not null)
                throw new InvalidDataException("Intent transcript not reachable from baseline.");
            replay = ApplyUnchecked(replay, choice.SelectedIndex);
        }
        if (CanonicalJson.Sha256(replay) != CanonicalJson.Sha256(state))
            throw new InvalidDataException("Controlled intent state was not source-reachable.");
    }

}
