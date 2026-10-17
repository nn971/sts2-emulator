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
    public ControlledTurnIntentsV111 Fork() => this with
    {
        BaselineCombat = BaselineCombat.Fork(),
        Combat = Combat.Fork(),
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

public static class ControlledTurnIntentEngineV111
{
    public const string SiteId = NativeCombatRngEntryV111.NextTurnIntentSiteId;

    public static ControlledTurnIntentsV111 Begin(
        CombatState combat, string nativeEncounterEntry,
        string siteId = SiteId)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEncounterEntry);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        var state = new ControlledTurnIntentsV111(
            combat.Fork(), combat.Fork(), nativeEncounterEntry, 0, [], null,
            siteId);
        return AdvanceToBoundary(state);
    }

    public static ControlledTurnIntentsV111 Resolve(
        ControlledTurnIntentsV111 state, int selectedIndex, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Pending is null)
            throw new InvalidOperationException("No pending next-turn intent ticket.");
        Validate(state);
        var working = state.Fork();
        var chosen = new ControlledRandomIndexProviderV111(selectedIndex)
            .Resolve(working.Pending!, streams);
        return AdvanceToBoundary(working with
        {
            Transcript = working.Transcript.Append(chosen).ToArray(),
            Pending = null
        });
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
        if (state.Pending is not { } pending)
            return null;
        if (state.NextEnemyIndex >= state.Combat.Enemies.Length)
            throw new InvalidDataException("Pending intent has no enemy.");
        var enemy = state.Combat.Enemies[state.NextEnemyIndex];
        var owner = "creature:" + enemy.InstanceId;
        if (pending.OwnerId != owner ||
            pending.StreamId != "run.monster_ai")
            throw new InvalidDataException("Pending semantic intent owner mismatch.");
        var prefix = state.Transcript.Where(decision =>
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

    public static void Validate(ControlledTurnIntentsV111 state)
    {
        if (string.IsNullOrWhiteSpace(state.NativeEncounterEntry) ||
            string.IsNullOrWhiteSpace(state.SiteId) ||
            state.Transcript is null ||
            state.NextEnemyIndex < 0 ||
            state.NextEnemyIndex > state.BaselineCombat.Enemies.Length ||
            state.BaselineCombat.Enemies.Length != state.Combat.Enemies.Length)
            throw new InvalidDataException("Malformed controlled turn-intent continuation.");
        // Recomputes only pure enemy move-choice evaluation from saved
        // eligibility/ticket prefixes, never gameplay effect stages.
        var replay = AdvanceToBoundary(state with
        {
            Combat = state.BaselineCombat.Fork(),
            NextEnemyIndex = 0,
            Transcript = [],
            Pending = null
        });
        var streams = NativeRngV111.CreateBundle("validation-no-native-consumption");
        foreach (var choice in state.Transcript)
        {
            choice.Validate();
            if (choice.ProviderMode != "controlled-v111" ||
                choice.NativeCursorBefore is not null ||
                choice.NativeCursorAfter is not null ||
                replay.Pending is null ||
                CanonicalJson.Sha256(replay.Pending) != CanonicalJson.Sha256(choice.Request))
                throw new InvalidDataException(
                    "Turn-intent decisions diverge from saved eligibility.");
            // Do not recursively validate while reconstructing the prefix.
            var ticket = new ControlledRandomIndexProviderV111(choice.SelectedIndex)
                .Resolve(choice.Request, streams);
            replay = AdvanceToBoundary(replay with
            {
                Transcript = replay.Transcript.Append(ticket).ToArray(),
                Pending = null
            });
        }
        if (CanonicalJson.Sha256(replay) != CanonicalJson.Sha256(state))
            throw new InvalidDataException(
                "Turn-intent continuation differs from its pure ticket replay.");
    }

    private sealed class PendingTicket(RandomIndexRequestV111 request) : Exception
    {
        public RandomIndexRequestV111 Request { get; } = request;
    }

    private static ControlledTurnIntentsV111 AdvanceToBoundary(
        ControlledTurnIntentsV111 start)
    {
        var state = start.Fork();
        var formation = state.Combat.Enemies.Select(e => e.Fork()).ToArray();
        int index = state.NextEnemyIndex;
        while (index < formation.Length)
        {
            var enemy = formation[index];
            var prior = state.Transcript.Where(d =>
                d.Request.OwnerId == "creature:" + enemy.InstanceId).ToArray();
            int position = 0;
            Func<int, int> getTicket = bound =>
            {
                if (bound <= 0)
                    throw new InvalidOperationException("Nonpositive turn-intent ticket bound.");
                var request = new RandomIndexRequestV111(
                    state.SiteId, "run.monster_ai",
                    "creature:" + enemy.InstanceId,
                    state.Transcript.Length - prior.Length + position,
                    [], OperationKind: "bounded_integer", BoundedCount: bound);
                if (position >= prior.Length)
                    throw new PendingTicket(request);
                var choice = prior[position++];
                if (choice.ProviderMode != "controlled-v111" ||
                    CanonicalJson.Sha256(choice.Request) != CanonicalJson.Sha256(request))
                    throw new InvalidDataException(
                        "Turn-intent ticket prefix diverged.");
                choice.Validate();
                return choice.SelectedIndex;
            };
            try
            {
                var chosen = PrototypeGameEngine.EvaluateEnemyIntentChoice(
                    formation, index, state.NativeEncounterEntry, getTicket);
                if (position != prior.Length)
                    throw new InvalidDataException("Unused turn-intent tickets.");
                formation[index] = PrototypeGameEngine
                    .ApplyEnemyIntentOutcome(
                        enemy, chosen.MoveIndex, chosen.NextAiStateId);
                index++;
            }
            catch (PendingTicket pending)
            {
                return state with
                {
                    Combat = state.Combat with { Enemies = formation },
                    NextEnemyIndex = index, Pending = pending.Request
                };
            }
        }
        return state with
        {
            Combat = state.Combat with { Enemies = formation },
            NextEnemyIndex = index, Pending = null
        };
    }
}
