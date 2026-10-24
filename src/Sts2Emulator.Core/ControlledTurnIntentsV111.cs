namespace Sts2Emulator.Core;

/// <summary>
/// A suspended next-turn intent commitment. All prior end-turn hooks have
/// already executed on BaselineCombat. The only replay while evaluating
/// tickets is the pure source-shaped intent selector; no gameplay hooks or
/// earlier turn stages are rerun.
/// </summary>
public sealed record ControlledTurnIntentsV111(
    CombatState BaselineCombat,
    CombatState Combat,
    string NativeEncounterEntry,
    int NextEnemyIndex,
    RandomIndexDecisionV111[] Transcript,
    RandomIndexRequestV111? Pending,
    string SiteId = ControlledTurnIntentEngineV111.SiteId)
{
    public bool Complete => Pending is null;

    /// <summary>
    /// Atomic mechanics/provider envelope for a suspended intent pass.
    /// Existing continuation properties remain flattened snapshot fields.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SimulationStateV111<ControlledTurnIntentMechanicsStateV111,
        ControlledTurnIntentStochasticStateV111> Simulation { get; init; } =
        new(
            new ControlledTurnIntentMechanicsStateV111(
                BaselineCombat, Combat, NativeEncounterEntry,
                NextEnemyIndex, SiteId),
            new ControlledTurnIntentStochasticStateV111(
                Transcript, Pending));

    [System.Text.Json.Serialization.JsonIgnore]
    public ControlledTurnIntentMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public ControlledTurnIntentStochasticStateV111 Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public CombatState BaselineCombat
    {
        get => Mechanics.BaselineCombat;
        init => Mechanics = Mechanics with { BaselineCombat = value };
    }

    public CombatState Combat
    {
        get => Mechanics.Combat;
        init => Mechanics = Mechanics with { Combat = value };
    }

    public string NativeEncounterEntry
    {
        get => Mechanics.NativeEncounterEntry;
        init => Mechanics = Mechanics with { NativeEncounterEntry = value };
    }

    public int NextEnemyIndex
    {
        get => Mechanics.NextEnemyIndex;
        init => Mechanics = Mechanics with { NextEnemyIndex = value };
    }

    public string SiteId
    {
        get => Mechanics.SiteId;
        init => Mechanics = Mechanics with { SiteId = value };
    }

    public RandomIndexDecisionV111[] Transcript
    {
        get => Stochastic.Transcript;
        init => Stochastic = Stochastic with { Transcript = value };
    }

    public RandomIndexRequestV111? Pending
    {
        get => Stochastic.Pending;
        init => Stochastic = Stochastic with { Pending = value };
    }

    public void Validate()
    {
        this.Stochastic.Validate();
        if (string.IsNullOrWhiteSpace(this.NativeEncounterEntry) ||
            !ControlledTurnIntentEngineV111.IsSupportedSiteId(this.SiteId) ||
            this.NextEnemyIndex < 0 ||
            this.NextEnemyIndex > this.BaselineCombat.Enemies.Length ||
            this.BaselineCombat.Enemies.Length != this.Combat.Enemies.Length)
            throw new InvalidDataException("Malformed controlled turn-intent continuation.");
        // Recomputes only pure enemy move-choice evaluation from saved
        // eligibility/ticket prefixes, never gameplay effect stages.
        var replay = ControlledTurnIntentEngineV111.AdvanceToBoundary(this with
        {
            Combat = this.BaselineCombat.Fork(),
            NextEnemyIndex = 0,
            Transcript = [],
            Pending = null
        });
        var streams = NativeRngV111.CreateBundle("validation-no-native-consumption");
        foreach (var choice in this.Stochastic.Transcript)
        {
            if (replay.Stochastic.Pending is not { } pending ||
                !ControlledStochasticChanceReplayV111.RequestsMatch(
                    pending, choice.Request))
                throw new InvalidDataException(
                    "Turn-intent decisions diverge from saved eligibility.");
            // Do not recursively validate while reconstructing the prefix.
            var ticket = new ControlledRandomIndexProviderV111(choice.SelectedIndex)
                .Resolve(choice.Request, streams);
            replay = ControlledTurnIntentEngineV111.AdvanceToBoundary(replay with
            {
                Stochastic = replay.Stochastic.AppendResolvedDecision(ticket)
            });
        }
        if (CanonicalJson.Sha256(replay) != CanonicalJson.Sha256(this))
            throw new InvalidDataException(
                "Turn-intent continuation differs from its pure ticket replay.");
    }

    public ControlledTurnIntentsV111 Fork() => this with
    {
        BaselineCombat = BaselineCombat.Fork(),
        Combat = Combat.Fork(),
        Stochastic = Stochastic.Fork()
    };
}

public static class ControlledTurnIntentEngineV111
{
    public const string SiteId = NativeCombatRngEntryV111.NextTurnIntentSiteId;

    public static bool IsSupportedSiteId(string? siteId) =>
        siteId is NativeCombatRngEntryV111.NextTurnIntentSiteId or
            NativeCombatRngEntryV111.CommittedIntentSiteId;

    public static ControlledTurnIntentsV111 Begin(
        CombatState combat, string nativeEncounterEntry,
        string siteId = SiteId)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEncounterEntry);
        if (!IsSupportedSiteId(siteId))
            throw new ArgumentOutOfRangeException(nameof(siteId));
        var state = new ControlledTurnIntentsV111(
            combat.Fork(), combat.Fork(), nativeEncounterEntry, 0, [], null,
            siteId);
        return AdvanceToBoundary(state);
    }

    public static ControlledTurnIntentsV111 Resolve(
        ControlledTurnIntentsV111 state, int selectedIndex, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(streams);
        if (state.Stochastic.Pending is null)
            throw new InvalidOperationException("No pending next-turn intent ticket.");
        Validate(state);
        var working = state.Fork();
        var request = working.Stochastic.Pending!;
        var continuation = new ControlledStochasticContinuationStateV111(
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            working.Stochastic.Transcript.Select(decision =>
                ControlledStochasticDecisionV111.FromIndex(decision)).ToArray(),
            ControlledStochasticRequestV111.FromIndex(request));
        var replay = ControlledChanceContinuationExecutorV111
            .ResolveTypedSelectedIndex(
            working,
            continuation,
            request,
            selectedIndex,
            (checkpoint, chosen) => ReplayIntentPass(checkpoint with
            {
                Stochastic = checkpoint.Stochastic
                    .AppendResolvedDecision(chosen)
            }));
        return replay.Value ?? throw new InvalidDataException(
            "Intent resolution suspended outside its saved pending boundary.");
    }

    private sealed class InspectIntentBoundary : Exception { }

    /// <summary>
    /// Inspect exactly the current suspended source ticket. Replay any prior
    /// per-enemy ticket prefix through the same pure selector and capture
    /// candidates immediately before the next real random draw. This adds
    /// no gameplay hooks and consumes no native RNG.
    /// </summary>
    public static SemanticIntentSupportV111? DescribePendingSemanticIntent(
        ControlledTurnIntentsV111 state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Validate(state);
        if (state.Stochastic.Pending is not { } pending)
            return null;
        if (state.NextEnemyIndex >= state.Combat.Enemies.Length)
            throw new InvalidDataException("Pending intent has no enemy.");
        var enemy = state.Combat.Enemies[state.NextEnemyIndex];
        var owner = "creature:" + enemy.InstanceId;
        if (pending.OwnerId != owner ||
            pending.StreamId != "run.monster_ai")
            throw new InvalidDataException("Pending semantic intent owner mismatch.");
        var prefix = state.Stochastic.Transcript.Where(decision =>
            decision.Request.OwnerId == owner).ToArray();
        int position = 0;
        SemanticIntentCandidateV111[]? candidates = null;
        try
        {
            _ = PrototypeGameEngine.EvaluateEnemyIntentChoice(
                state.Combat.Enemies, state.NextEnemyIndex,
                state.NativeEncounterEntry,
                bound =>
                {
                    if (position < prefix.Length)
                    {
                        var choice = prefix[position++];
                        if (choice.Request.SupportCount != bound)
                            throw new InvalidDataException(
                                "Recorded intent prefix bound diverged.");
                        return choice.SelectedIndex;
                    }
                    if (bound != pending.SupportCount)
                        throw new InvalidDataException(
                            "Semantic intent weight total differs from source ticket.");
                    throw new InspectIntentBoundary();
                },
                support =>
                {
                    if (position == prefix.Length)
                        candidates = support;
                });
            throw new InvalidDataException(
                "Expected the suspended source intent ticket.");
        }
        catch (InspectIntentBoundary)
        {
            // Normal suspension: no random value was selected.
        }
        if (position != prefix.Length || candidates is not { Length: > 0 })
            throw new InvalidDataException(
                "Pending semantic intent has no source-filtered support.");
        var total = checked(candidates.Sum(candidate => candidate.RawWeight));
        if (candidates.Any(candidate => candidate.RawWeight <= 0 ||
                candidate.TicketStart < 0 ||
                candidate.TicketStart + candidate.RawWeight > total) ||
            total != pending.SupportCount)
            throw new InvalidDataException(
                "Semantic intent weights disagree with primitive source support.");
        return new SemanticIntentSupportV111(owner, candidates, total);
    }

    public static void Validate(ControlledTurnIntentsV111 state) =>
        state.Validate();

    internal static ControlledTurnIntentsV111 AdvanceToBoundary(
        ControlledTurnIntentsV111 start)
    {
        var checkpoint = start with
        {
            Combat = start.BaselineCombat.Fork(),
            NextEnemyIndex = 0,
            Pending = null
        };
        var decisions = start.Stochastic.Transcript.Select(decision =>
            ControlledStochasticDecisionV111.FromIndex(decision)).ToArray();
        var replay = ControlledChanceContinuationExecutorV111.CaptureTyped(
            checkpoint,
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            decisions,
            ReplayIntentPass);
        return replay.Value ?? throw new InvalidDataException(
            "Intent replay suspended outside its saved pending boundary.");
    }

    private static ControlledTurnIntentsV111 ReplayIntentPass(
        ControlledTurnIntentsV111 checkpoint)
    {
        var mechanics = checkpoint with
        {
            Combat = checkpoint.BaselineCombat.Fork(),
            NextEnemyIndex = 0,
            Pending = null
        };
        int requestOrdinal = 0;
        // This pass only verifies and reapplies recorded outcomes. Keep its
        // replayed provider calls out of any caller-owned transition journal.
        using var replayJournal = RandomBoundaryJournalV111.Begin();
        var formation = mechanics.Combat.Enemies
            .Select(enemy => enemy.Fork()).ToArray();
        int index = 0;
        var siteId = mechanics.SiteId;
        if (siteId is not (
                NativeCombatRngEntryV111.NextTurnIntentSiteId or
                NativeCombatRngEntryV111.CommittedIntentSiteId))
            throw new NotSupportedException(
                $"UNSUPPORTED_CONTROLLED_CHANCE site={siteId}");
        while (index < formation.Length)
        {
            var enemy = formation[index];
            Func<int, int> getTicket = bound =>
            {
                if (bound <= 0)
                    throw new InvalidOperationException(
                        "Nonpositive turn-intent ticket bound.");
                var request = new RandomIndexRequestV111(
                    siteId, "run.monster_ai",
                    "creature:" + enemy.InstanceId,
                    requestOrdinal++, [],
                    OperationKind: "bounded_integer",
                    BoundedCount: bound);
                return StochasticProviderScopeV111
                    .ResolveIndexAtDeclaredOrdinal(
                        null, request, RngBundle.Empty).SelectedIndex;
            };
            try
            {
                var chosen = PrototypeGameEngine.EvaluateEnemyIntentChoice(
                    formation, index,
                    mechanics.NativeEncounterEntry, getTicket);
                formation[index] = PrototypeGameEngine
                    .ApplyEnemyIntentOutcome(
                        enemy, chosen.MoveIndex, chosen.NextAiStateId);
                index++;
            }
            catch (ControlledStochasticChanceRequiredExceptionV111 pending)
            {
                var request = pending.Request.IndexRequest ??
                    throw new InvalidDataException(
                        "Turn-intent replay suspended on a non-index request.");
                return mechanics with
                {
                    Combat = mechanics.Combat with { Enemies = formation },
                    NextEnemyIndex = index,
                    Pending = request
                };
            }
        }
        return mechanics with
        {
            Combat = mechanics.Combat with { Enemies = formation },
            NextEnemyIndex = index,
            Pending = null
        };
    }
}
