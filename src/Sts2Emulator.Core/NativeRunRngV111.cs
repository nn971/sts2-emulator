namespace Sts2Emulator.Core;

/// <summary>
/// Optional persistent native RNG ownership across run phases.
/// The legacy six-stream RunState.Rng stays available for prototype
/// mechanics; this distinct native bundle holds the audited 12 run and
/// 3 player streams. Every public operation preserves copy-on-write.
/// </summary>
public static class NativeRunRngV111
{
    private static readonly string[] RequiredStreams =
        NativeRngV111.CreateBundle("schema-inventory").Streams
            .Select(stream => stream.StreamId)
            .Order(StringComparer.Ordinal).ToArray();

    public static RunState Enable(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.NativeRunRng is not null)
            throw new InvalidOperationException(
                "Native run RNG ownership is already enabled.");
        if (state.World?.Combat?.NativeRng is not null)
            throw new InvalidOperationException(
                "Cannot introduce a run RNG owner after native combat has begun.");
        return state with
        {
            Stochastic = RunStochasticStateV111.WithNativeRunRng(
                state.Stochastic,
                NativeRngV111.CreateBundle(state.RunSeed))
        };
    }

    /// <summary>
    /// Validate the full native inventory, xoshiro snapshots, and the
    /// synchronization invariant between combat and run stream owners.
    /// No RNG advances on validation.
    /// </summary>
    public static void Validate(RunState state)
        => Validate(state, state?.Stochastic.NativeRunRng);

    internal static void Validate(RunState state, RngBundle? runRng)
    {
        ArgumentNullException.ThrowIfNull(state);
        var combat = state.World?.Combat;
        var active = combat?.Stochastic.NativeRng;
        if (runRng is null)
        {
            if (active is not null)
                throw new InvalidOperationException(
                    "Native combat RNG requires persistent run RNG ownership.");
            return;
        }

        ValidateBundle(runRng);
        if (active is null)
            return;
        NativeCombatRngEntryV111.Validate(combat!, active);
        foreach (var stream in active.RunStreams.Streams)
        {
            var runStream = runRng.GetStream(stream.StreamId);
            if (NativeRngV111.ToSnapshot(stream)
                != NativeRngV111.ToSnapshot(runStream))
                throw new InvalidOperationException(
                    $"Run and combat RNG cursor '{stream.StreamId}' disagree.");
        }
    }

    public static void ValidateBundle(RngBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (bundle.Streams is null)
            throw new InvalidOperationException(
                "Native RNG bundle has no stream collection.");
        if (bundle.Streams.Any(stream => stream is null))
            throw new InvalidOperationException(
                "Native RNG bundle contains a missing stream owner.");
        var actual = bundle.Streams.Select(stream => stream.StreamId)
            .Order(StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(RequiredStreams, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Native run RNG must own exactly 12 run and 3 player streams.");
        foreach (var stream in bundle.Streams)
            _ = NativeRngV111.ToSnapshot(stream);
    }

    /// <summary>
    /// Opt-in stream draw for auditing an entire run across room boundaries.
    /// When a native combat is active, the draw updates both the combat
    /// and run views atomically. Prototype RNG is never touched.
    /// </summary>
    public static RunState SampleNextInt(
        RunState state, string streamId, int maxExclusive, out int value)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.NativeRunRng is null)
            throw new InvalidOperationException(
                "Native run RNG ownership is not enabled.");
        Validate(state);
        var advanced = state.NativeRunRng.Fork();
        value = NativeRngV111.NextInt(advanced, streamId, maxExclusive);
        if (state.World?.Combat?.NativeRng is { } active)
        {
            var sourceCombat = state.World.Combat;
            var combat = sourceCombat with
            {
                Stochastic = CombatStochasticStateV111.WithNativeRng(
                    sourceCombat.Stochastic,
                    active with { RunStreams = advanced.Fork() })
            };
            return state with
            {
                Stochastic = RunStochasticStateV111.WithNativeRunRng(
                    state.Stochastic, advanced),
                World = state.World with { Combat = combat }
            };
        }
        return state with
        {
            Stochastic = RunStochasticStateV111.WithNativeRunRng(
                state.Stochastic, advanced)
        };
    }

    /// <summary>
    /// Attach native formation and roll HP in a currently active typed
    /// combat using the *current*, not freshly seeded, native run cursors.
    /// Existing prototype formation/card setup is unchanged. The caller
    /// must supply an exactly matching native formation and real combat
    /// IDs; source-specific encounter hooks remain outside this API.
    /// </summary>
    public static RunState BeginEncounter(
        RunState state, NativeEncounterFormationV111 formation)
    {
        ArgumentNullException.ThrowIfNull(state);
        var world = state.World
            ?? throw new InvalidOperationException("Run has no world.");
        if (state.Phase != RunPhase.Combat || world.Combat is null)
            throw new InvalidOperationException(
                "Native encounter requires an active typed combat.");
        if (state.NativeRunRng is null)
            throw new InvalidOperationException(
                "Enable native run ownership before entering combat.");
        Validate(state);
        var attached = NativeCombatRngEntryV111.AttachAndRollInitialHp(
            world.Combat, formation, state.NativeRunRng);
        return ReplaceCombat(state, attached);
    }

    /// <summary>
    /// Update an in-progress native combat, copying the advanced run
    /// streams back to the long-lived run owner. This is useful for
    /// opt-in snapshot benchmarks and explicit monster-local effects.
    /// </summary>
    public static RunState ReplaceCombat(RunState state, CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(combat);
        if (state.World?.Combat is null || state.Phase != RunPhase.Combat
            || state.NativeRunRng is null || combat.NativeRng is null)
            throw new InvalidOperationException(
                "Replacing a native combat requires an active run-owned combat.");
        NativeCombatRngEntryV111.Validate(combat);
        ValidateBundle(combat.NativeRng.RunStreams);
        var updated = state with
        {
            Stochastic = RunStochasticStateV111.WithNativeRunRng(
                state.Stochastic, combat.NativeRng.RunStreams.Fork()),
            World = state.World with { Combat = combat.Fork() }
        };
        Validate(updated);
        return updated;
    }

    /// <summary>
    /// Explicit handoff when the combat ends. The run retains all native
    /// run/player RNG cursors; encounter and monster-local streams expire
    /// with their combat owner. No prototype RNG or player state is changed.
    /// </summary>
    public static RunState ReleaseCombat(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.World?.Combat?.NativeRng is not { } active
            || state.NativeRunRng is null)
            throw new InvalidOperationException(
                "No native combat RNG to release.");
        var sourceCombat = state.World.Combat;
        var combat = sourceCombat with
        {
            Stochastic = CombatStochasticStateV111.WithNativeRng(
                sourceCombat.Stochastic, null)
        };
        var result = state with
        {
            Stochastic = RunStochasticStateV111.WithNativeRunRng(
                state.Stochastic, active.RunStreams.Fork()),
            World = state.World with
            {
                Combat = combat
            }
        };
        Validate(result);
        return result;
    }

    /// <summary>
    /// Engine transition reconciliation. When a native combat continues,
    /// copy its advanced run RNG cursors to the long-lived run owner.
    /// When combat exits, retain the post-step run owner: exit hooks may
    /// synchronize combat draws, then reward generation may advance player
    /// streams after the combat object has been removed.
    /// This remains inert for every ordinary prototype run.
    /// </summary>
    internal static RunState ReconcileAfterStep(
        RunState before, RunState after)
    {
        var afterRunStreams = after.NativeRunRng;
        if (afterRunStreams is null)
            return after;
        var live = after.World?.Combat?.NativeRng;
        var ended = before.World?.Combat?.NativeRng;
        if (live is null && ended is null)
            return after;
        if (live is not null)
        {
            // The native sidecar currently binds the initial source
            // encounter formation. Prototype summoning, transformation
            // or enemy removal cannot silently invalidate that binding:
            // future creature-local RNG ownership and native call order
            // are not migrated yet. Refuse to return an unsnapshotable
            // combat state until those mechanics have dedicated support.
            try
            {
                NativeCombatRngEntryV111.Validate(after.World!.Combat!);
            }
            catch (InvalidOperationException ex)
            {
                throw new NotSupportedException(
                    "Experimental native combat cannot continue after " +
                    "an unimplemented enemy formation change (summon, " +
                    "transformation or removal). Source-specific creature " +
                    "RNG and lifecycle integration is required.", ex);
            }
        }
        var source = live?.RunStreams ?? afterRunStreams;
        ValidateBundle(source);
        return after with
        {
            Stochastic = RunStochasticStateV111.WithNativeRunRng(
                after.Stochastic, source.Fork())
        };
    }
}
