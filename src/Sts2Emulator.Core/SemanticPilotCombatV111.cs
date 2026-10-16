using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Sts2Emulator.Core;

/// <summary>One externally replayable action or Nature decision.</summary>
public sealed record SemanticPilotTransitionV111(
    string Kind, string? ActionId = null, int? SelectedIndex = null);

/// <summary>
/// Independently replayable source-shaped setup provenance. These explicit
/// encounter parameters initialize the opening using engine-owned seeded
/// streams. The fixture still omits full native combat-entry hooks.
/// </summary>
/// <summary>
    /// Combat-ended reward selection is outside the two-site Nature game:
    /// legacy prototype reward RNG, isolated under the guarded source stream.
    /// Its exact cursor use is replayable, while native source parity is
    /// explicitly UNVERIFIED.
    /// </summary>
public sealed record SemanticRewardAuditV111(
    string ProviderId, ulong BeforeCalls, ulong AfterCalls,
    string ProbabilityStatus = "UNVERIFIED")
{
    public const string PrototypeProviderId =
        "engine-owned-prototype-reward-unverified-v1";
    public ulong Calls => AfterCalls - BeforeCalls;
}

public sealed record SemanticNativePolicyAuditV111(
    string ProviderId, RandomBoundaryEventV111[] Decisions,
    string ProbabilityStatus = "UNVERIFIED")
{
    public const string NativeProviderId =
        "engine-owned-native-v111-comparator-unverified-v1";
}

public sealed record SemanticSeededEntryV111(
    string Seed, int Act, string EncounterId, string NativeEncounterEntry,
    int TotalFloor, int Ascension, long FirstMonsterCombatId,
    int? MapColumn, int? MapRow,
    string? OtherChanceProviderId = null,
    string? FixtureDrawCostRelicId = null,
    bool ReplayOpeningDrawHooks = false,
    bool NativePolicyBaseline = false);

/// <summary>
/// First external playable semantic boundary. Initial opening order is
/// already committed by the separate controlled fixture: this provider
/// copies those exact hidden positions without issuing new Nature tickets.
/// Subsequent fresh recycle epochs are deferred one card at a time.
/// </summary>
public sealed record SemanticPilotCombatV111(
    ControlledPlayableCombatV111 OpeningSource,
    RunState Run, SemanticEndTurnV111? PendingEndTurn,
    SemanticPilotTransitionV111[] History,
    string NativeEncounterEntry,
    string Mode = "sts2-v111-semantic-playable-p0-v1")
{
    public const string SchemaId = "sts2-v111-semantic-playable-p0-v1";
    /// <summary>
    /// Null for legacy precommitted fixtures. Seeded entries are verified by
    /// regenerating the entire source-shaped opening on snapshot restoration.
    /// This field is provider-private and never part of fair observation.
    /// </summary>
    public SemanticSeededEntryV111? SeededEntry { get; init; }
    /// <summary>Private state for explicitly opted-in non-Nature RNG.</summary>
    public SemanticOtherChanceStateV111? OtherChance { get; init; }
    public SemanticRewardAuditV111? RewardAudit { get; init; }
    public SemanticNativePolicyAuditV111? NativePolicyAudit { get; init; }
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    [JsonIgnore] public bool AtPlayerBoundary =>
        PendingEndTurn is null &&
        Run.Phase == RunPhase.Combat &&
        Run.World?.Combat is { IsPlayerTurn: true,
            PendingChoice: null, AutomaticPipelineContinuation: null };
    [JsonIgnore] public bool CombatTerminal =>
        Run.Phase is RunPhase.Reward or RunPhase.Terminal;
    [JsonIgnore] public bool UsesNativePolicyBaseline =>
        SeededEntry?.NativePolicyBaseline ?? false;
    [JsonIgnore] public string? SourceOpeningFingerprint =>
        SeededEntry is { } entry
            ? CanonicalJson.Sha256(new
            {
                Entry = entry with { NativePolicyBaseline = false },
                OpeningSource
            })
            : null;
    [JsonIgnore] public string? CombatOutcome =>
        Run.Phase switch
        {
            RunPhase.Reward => "victory",
            RunPhase.Terminal => "defeat",
            _ => null
        };
    [JsonIgnore] public RandomIndexRequestV111? IntentPending =>
        PendingEndTurn?.IntentPending;
    [JsonIgnore] public SemanticDeckEventV111? DrawPending =>
        PendingEndTurn?.DrawPending;

    public SemanticPilotCombatV111 Fork() => this with
    {
        OpeningSource = OpeningSource.Fork(),
        Run = Run.Fork(),
        PendingEndTurn = PendingEndTurn?.Fork(),
        History = (SemanticPilotTransitionV111[])History.Clone(),
        OtherChance = OtherChance?.Fork(),
        NativePolicyAudit = NativePolicyAudit is { } audit
            ? audit with
            {
                Decisions = audit.Decisions.Select(decision => decision with
                {
                    Request = decision.Request with
                    {
                        OrderedCandidates = (int[])decision.Request
                            .OrderedCandidates.Clone()
                    }
                }).ToArray()
            }
            : null
    };
}

public static class SemanticPilotCombatEngineV111
{
    public static SemanticPilotCombatV111 Begin(
        ControlledPlayableCombatV111 completedOpening)
    {
        ArgumentNullException.ThrowIfNull(completedOpening);
        // The source fixture is independently replayable. Never accept its
        // partially committed legacy shuffle or a non-player checkpoint.
        var source = ControlledPlayableCombatEngineV111.Restore(
            ControlledPlayableCombatEngineV111.Save(completedOpening));
        if (!source.AtPlayerBoundary || source.Run.Phase != RunPhase.Combat ||
            source.Run.World?.Combat is not { } combat ||
            combat.SemanticDeck is not null ||
            combat.AutomaticPipelineContinuation is not null ||
            combat.PendingChoice is not null)
            throw new InvalidOperationException(
                "Semantic playable entry requires a completely committed opening fixture.");
        var deck = SemanticDeckEngineV111.FromCommittedOpening(
            combat.DrawPile.Reverse().ToArray());
        var run = source.Run.Fork();
        run = run with
        {
            World = run.World! with
            {
                Combat = combat.Fork() with { SemanticDeck = deck }
            }
        };
        var state = new SemanticPilotCombatV111(
            source.Fork(), run, null, [],
            source.Origin.Formation.NativeEncounterEntry);
        Validate(state);
        return state;
    }

    /// <summary>
    /// Build a playable two-site session without asking an external worker to
    /// choose HP, initial intent tickets or opening Fisher-Yates swaps.
    /// Source-shaped eligibility and the pinned native RNG codec decide these
    /// setup events on separately owned streams. The later player/Nature
    /// protocol remains identical; opening order is imported as known and
    /// will never be randomized again by a Nature draw ticket.
    ///
    /// This is an isolated seeded Hive/Glory fixture, not a claim that all
    /// native route history, entry hooks or opening draw effects are audited.
    /// </summary>
    public static SemanticPilotCombatV111 BeginSeeded(
        SemanticSeededEntryV111 entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Seed);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.EncounterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.NativeEncounterEntry);
        if (entry.Act is not (1 or 2 or 3) || entry.TotalFloor < 1 ||
            entry.FirstMonsterCombatId < 1)
            throw new ArgumentOutOfRangeException(nameof(entry),
                "Seeded Nature combat requires a positive floor and monster IDs.");
        if (entry.NativePolicyBaseline &&
            (entry.OtherChanceProviderId is not null ||
             entry.FixtureDrawCostRelicId is not null))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_NATIVE site=combat.seeded_entry " +
                "operation=native_baseline_fixture_cost_provider_conflict");
        // Act 1 fixed weak entry is deliberately isolated from the
        // Hive/Glory fixture's pre-existing public 2/3-only contract.
        var prepared = entry.Act == 1
            ? PrototypeNativeActOneWeakNatureFixtureV111.CreateCombatRngPreEntry(
                entry.Seed, entry.EncounterId, entry.NativeEncounterEntry,
                entry.TotalFloor, entry.Ascension, entry.FirstMonsterCombatId,
                entry.MapColumn, entry.MapRow)
            : PrototypeNativeLaterActFixture.CreateCombatRngPreEntry(
                entry.Seed, entry.Act, entry.EncounterId,
                entry.NativeEncounterEntry, entry.TotalFloor, entry.Ascension,
                entry.FirstMonsterCombatId, entry.MapColumn, entry.MapRow);
        var rng = prepared.NativeStreams.Fork();
        using var setupJournal = RandomBoundaryJournalV111.Begin();
        var nativeProvider = new NativeRandomIndexProviderV111();
        var hp = ControlledInitialHpV111.Begin(
            prepared.Combat, prepared.Formation, prepared.NativeStreams);
        while (!hp.Complete)
        {
            var pending = hp.Pending!;
            var decision = nativeProvider.Resolve(pending, rng);
            hp = ControlledInitialHpV111.Resolve(
                hp, hp.Fingerprint, decision.SelectedIndex);
        }
        var intents = ControlledIntentsV111.BeginAfterControlledHp(hp);
        while (!intents.Complete)
        {
            var pending = intents.Pending!;
            var decision = nativeProvider.Resolve(pending, rng);
            intents = ControlledIntentsV111.Resolve(
                intents, intents.Fingerprint, decision.SelectedIndex);
        }
        var opening = ControlledPlayableCombatEngineV111.Begin(
            intents, entry.Seed);
        while (!opening.AtPlayerBoundary)
        {
            var pending = opening.Pending ??
                throw new InvalidDataException(
                    "Engine-owned opening encountered an unsupported chance site.");
            if (pending.SiteId != ControlledPlayableCombatEngineV111.ShuffleSite)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_OPENING site=" + pending.SiteId);
            var decision = nativeProvider.Resolve(pending with
            {
                SiteId = NativeFisherYatesV111.SiteId
            }, rng);
            opening = ControlledPlayableCombatEngineV111.Resolve(
                opening, opening.Fingerprint, decision.SelectedIndex);
        }
        if (entry.OtherChanceProviderId is not null &&
            entry.OtherChanceProviderId != SemanticOtherChanceEngineV111.ProviderId)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE provider=" +
                entry.OtherChanceProviderId);
        var state = Begin(opening) with
        {
            SeededEntry = entry,
            OtherChance = entry.OtherChanceProviderId is null
                ? null : SemanticOtherChanceEngineV111.Begin(rng)
        };
        if (entry.ReplayOpeningDrawHooks)
            state = ReapplyCommittedOpeningThroughDrawCards(state);
        if (entry.NativePolicyBaseline)
            state = AttachNativePolicyBaseline(state, rng,
                opening.Origin.Formation,
                setupJournal.Entries.Where(decision =>
                    decision.ProviderMode == "native-v111").ToArray());
        if (entry.FixtureDrawCostRelicId is { } relicId)
        {
            if (relicId != "proto.relic.snecko_eye")
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                    "operation=fixture_relic:" + relicId);
            // Explicitly test-only loadout. This is an actual relic used
            // by the real DrawCards source mechanic; its acquisition
            // history is NOT asserted to be native-authentic.
            var world = state.Run.World!;
            var combat = world.Combat!;
            if (state.Run.Player.Relics.Any(x => x.RelicId == relicId))
                throw new InvalidOperationException("Fixture relic already owned.");
            var definition = PrototypeContent.Relic(relicId);
            int index = (combat.Relics ?? []).Length;
            var player = state.Run.Player with
            {
                Relics = state.Run.Player.Relics.Append(
                    new RelicInstance(relicId, PrototypeJson.EmptyObject()))
                    .ToArray()
            };
            var updated = combat with
            {
                Relics = (combat.Relics ?? []).Append(
                    new CombatRelicState(index, relicId, index + 1L,
                        new int[(definition.Triggers ?? []).Length], false))
                    .ToArray()
            };
            state = state with
            {
                Run = state.Run with
                {
                    Player = player,
                    World = world with { Combat = updated }
                }
            };
        }
        Validate(state);
        return state;
    }

    private static SemanticPilotCombatV111 AttachNativePolicyBaseline(
        SemanticPilotCombatV111 state, RngBundle nativeStreams,
        NativeEncounterFormationV111 formation,
        RandomBoundaryEventV111[] setupDecisions)
    {
        ArgumentNullException.ThrowIfNull(nativeStreams);
        ArgumentNullException.ThrowIfNull(formation);
        var run = state.Run;
        var world = run.World ?? throw new InvalidDataException(
            "Native policy entry is missing its run world.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "Native policy entry is missing its combat.");
        if (run.NativeRunRng is not null || combat.NativeRng is not null ||
            combat.SemanticDeck is null ||
            combat.SemanticDeck.Mode != SemanticDeckStateV111.NatureMode)
            throw new InvalidOperationException(
                "Native policy entry requires a fresh semantic seeded opening.");

        var exactDeck = SemanticDeckEngineV111.BeginNative(
            combat.DrawPile.Reverse().ToArray());
        var nativeCombat = combat with
        {
            NativeRng = new NativeCombatRngV111(
                nativeStreams.Fork(), formation.Fork()),
            SemanticDeck = exactDeck
        };
        NativeCombatRngEntryV111.Validate(nativeCombat);
        var nativeRun = run with
        {
            NativeRunRng = nativeStreams.Fork(),
            World = world with { Combat = nativeCombat }
        };
        NativeRunRngV111.Validate(nativeRun);
        return state with
        {
            Run = nativeRun,
            NativePolicyAudit = new SemanticNativePolicyAuditV111(
                SemanticNativePolicyAuditV111.NativeProviderId,
                setupDecisions.Select((decision, index) => decision with
                {
                    Sequence = index + 1,
                    Request = decision.Request with
                    {
                        OrderedCandidates = (int[])decision.Request
                            .OrderedCandidates.Clone()
                    }
                }).ToArray())
        };
    }

    /// <summary>
    /// Recreate the same already committed opening permutation from the
    /// original post-shuffle draw pile and the dealt hand. Execute actual
    /// DrawCards/CardDrawn mechanics once, with all positions private and
    /// known, before the first player decision. The restricted initial
    /// starter hand has no random draw-cost or nested player choices.
    /// This is an opt-in source-mechanic bridge, not full native combat
    /// entry / BeforeHandDraw hook parity.
    /// </summary>
    private static SemanticPilotCombatV111 ReapplyCommittedOpeningThroughDrawCards(
        SemanticPilotCombatV111 state)
    {
        var run = state.Run;
        var world = run.World!;
        var combat = world.Combat!;
        var dealt = (long[])combat.Hand.Clone();
        if (dealt.Length == 0 || combat.PendingChoice is not null ||
            combat.AutomaticPipelineContinuation is not null ||
            combat.SemanticDeck?.Pending is not null)
            throw new InvalidOperationException(
                "Opening hook replay requires a committed ordinary dealt hand.");
        var full = combat.DrawPile.Concat(dealt.Reverse()).ToArray();
        if (full.Length != full.Distinct().Count())
            throw new InvalidDataException(
                "Reconstituted opening pile duplicated physical card identity.");
        var prepared = run with
        {
            World = world with
            {
                Combat = combat with
                {
                    Hand = [],
                    DrawPile = full,
                    SemanticDeck = SemanticDeckEngineV111.FromCommittedOpening(
                        full.Reverse().ToArray())
                }
            }
        };
        RunState drawn;
        int remaining;
        using (ControlledChanceGuardV111.Enter())
            (drawn, remaining) = PrototypeGameEngine.BeginSemanticCombatDraw(
                prepared, dealt.Length, fromHandDraw: true);
        var result = drawn.World?.Combat;
        if (remaining != 0 || drawn.Phase != RunPhase.Combat ||
            result is null || result.PendingChoice is not null ||
            result.AutomaticPipelineContinuation is not null ||
            result.SemanticDeck?.Pending is not null ||
            !result.Hand.SequenceEqual(dealt) ||
            !result.DrawPile.SequenceEqual(combat.DrawPile))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OPENING site=combat.opening.DrawCards " +
                "operation=unmodeled_initial_draw_hook");
        return state with { Run = drawn };
    }

    public static SemanticPilotCombatV111 Step(
        SemanticPilotCombatV111 current, string fingerprint, string actionId)
    {
        Validate(current);
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException("Stale semantic player-action fingerprint.");
        if (!current.AtPlayerBoundary)
            throw new InvalidOperationException("Nature decision or combat terminal is pending.");
        if (current.UsesNativePolicyBaseline)
            return StepNativePolicyBaseline(current, actionId);
        var environment = new PrototypeAiEnvironment();
        var frame = environment.Observe(current.Run);
        var action = frame.LegalActions.SingleOrDefault(a =>
            a.ActionId == actionId) ?? throw new InvalidOperationException(
                "Illegal semantic player action.");
        var source = current.Run.Fork();
        SemanticEndTurnV111? endTurn = null;
        RunState result;
        if (action.Kind == "end_turn")
        {
            (endTurn, result) =
                SemanticEndTurnEngineV111.BeginFromPlayerActionOrTerminal(
                    source, current.NativeEncounterEntry);
        }
        else if (action.Kind == "play_card")
        {
            var play = action.Payload.Deserialize<PlayCardPayload>() ??
                throw new InvalidDataException("Missing source card payload.");
            var card = source.World!.Combat!.Cards.SingleOrDefault(c =>
                c.InstanceId == play.CardInstanceId);
            if (card?.CardId is not ("proto.silent.strike" or "proto.silent.defend"))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=card_effect_requires_semantic_audit");
            // Preserve actual source damage, relic/power hooks and victory
            // transition. The private deck must remain unchanged.
            using (ControlledChanceGuardV111.Enter())
            using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
                result = environment.Step(source, actionId).State;
            if (result.Phase is not (RunPhase.Combat or RunPhase.Reward))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=unsupported_post_card_phase");
            if (result.Phase == RunPhase.Combat)
                SemanticPlayerActionEngineV111.ValidateBoundary(result);
            var previous = current.Run.World!.Combat!;
            var next = result.World?.Combat;
            if (result.Phase == RunPhase.Reward)
            {
                // The original EnterReward consumes the defeated combat
                // and publishes RewardState. It is a genuine terminal
                // combat victory; no provider-owned deck survives to
                // mutate or expose on the next player decision.
                if (result.World?.Reward is null ||
                    result.World.Combat is not null)
                    throw new InvalidDataException(
                        "Victory did not enter a genuine combat reward boundary.");
            }
            else if (next is null ||
                     !previous.DrawPile.SequenceEqual(next.DrawPile) ||
                     CanonicalJson.Sha256(previous.SemanticDeck) !=
                         CanonicalJson.Sha256(next.SemanticDeck))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=unmodeled_draw_pile_mutation");
        }
        else
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action operation=" +
                action.Kind);
        var nextState = current with
        {
            Run = result,
            RewardAudit = result.Phase == RunPhase.Reward
                ? RecordRewardAudit(source, result)
                : current.RewardAudit,
            // A genuine fully deterministic end-turn continuation can
            // finish without exposing an intent or draw Nature ticket.
            // The next player decision must be immediately available.
            PendingEndTurn = endTurn is { Complete: true } ? null : endTurn,
            History = current.History.Append(
                new SemanticPilotTransitionV111("player", actionId)).ToArray()
        };
        return NormalizeNoncontrolledCosts(nextState);
    }

    private static SemanticPilotCombatV111 StepNativePolicyBaseline(
        SemanticPilotCombatV111 current, string actionId)
    {
        var environment = new PrototypeAiEnvironment();
        var frame = environment.Observe(current.Run);
        if (frame.LegalActions.Count(a => a.ActionId == actionId) != 1)
            throw new InvalidOperationException(
                "Illegal or ambiguous native-policy player action.");
        RunState result;
        using var boundaries = RandomBoundaryJournalV111.Begin();
        using (ControlledChanceGuardV111.Enter())
        using (ControlledChanceGuardV111.AllowEngineOwnedNativeProvider())
        using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
            result = environment.Step(current.Run.Fork(), actionId).State;

        var audit = current.NativePolicyAudit ??
            throw new InvalidDataException(
                "Native policy baseline is missing provider provenance.");
        int offset = audit.Decisions.Length;
        var recorded = boundaries.Entries.Select((entry, index) => entry with
        {
            Sequence = offset + index + 1,
            Request = entry.Request with
            {
                OrderedCandidates = (int[])entry.Request.OrderedCandidates.Clone()
            }
        }).ToArray();

        if (result.Phase == RunPhase.Combat)
            ValidateNativePolicyBoundary(result);
        else if (result.Phase is not (RunPhase.Reward or RunPhase.Terminal))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_NATIVE site=combat.player_action " +
                "operation=unsupported_post_action_phase");

        var next = current with
        {
            Run = result,
            PendingEndTurn = null,
            RewardAudit = result.Phase == RunPhase.Reward
                ? RecordRewardAudit(current.Run, result)
                : current.RewardAudit,
            NativePolicyAudit = audit with
            {
                Decisions = audit.Decisions.Concat(recorded).ToArray()
            },
            History = current.History.Append(
                new SemanticPilotTransitionV111("player", actionId)).ToArray()
        };
        Validate(next);
        return next;
    }

    private static SemanticRewardAuditV111 RecordRewardAudit(
        RunState source, RunState result)
    {
        static ulong Count(RunState run) =>
            run.Rng.Streams.Single(x =>
                x.StreamId == "reward" &&
                x.Codec == PrototypeRng.Codec).CallCount
            ?? throw new InvalidDataException(
                "Reward RNG stream has no saved call count.");
        var before = Count(source);
        var after = Count(result);
        if (after < before)
            throw new InvalidDataException(
                "Engine-owned reward RNG cursor rewound on victory.");
        return new SemanticRewardAuditV111(
            SemanticRewardAuditV111.PrototypeProviderId, before, after);
    }

    public static SemanticPilotCombatV111 Resolve(
        SemanticPilotCombatV111 current, string fingerprint, int selectedIndex)
    {
        Validate(current);
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException("Stale semantic Nature fingerprint.");
        var pending = current.PendingEndTurn ??
            throw new InvalidOperationException("No Nature decision is pending.");
        if (pending.IntentPending is null && pending.DrawPending is null)
            throw new InvalidOperationException(
                "Only semantic enemy_intent and draw_card are Nature-controlled.");
        var resolved = SemanticEndTurnEngineV111.Resolve(
            pending, pending.Fingerprint, selectedIndex);
        return NormalizeNoncontrolledCosts(current with
        {
            Run = resolved.Run,
            PendingEndTurn = resolved.Complete ? null : resolved,
            History = current.History.Append(
                new SemanticPilotTransitionV111("nature",
                    SelectedIndex: selectedIndex)).ToArray()
        });
    }

    /// <summary>
    /// Resolve one source-filtered weighted semantic intent by its stable
    /// candidate ID, translating it into the first legal native-shaped
    /// ticket of the corresponding weight interval. Old numeric ticket
    /// resolution stays available for protocol compatibility.
    /// </summary>
    public static SemanticPilotCombatV111 ResolveIntent(
        SemanticPilotCombatV111 current, string fingerprint, string intentId)
    {
        Validate(current);
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException("Stale semantic intent fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        var ticket = current.PendingEndTurn?.Intents;
        if (ticket?.Pending is null)
            throw new InvalidOperationException(
                "Only a pending enemy_intent supports semantic intent IDs.");
        var support = ControlledTurnIntentEngineV111
            .DescribePendingSemanticIntent(ticket) ??
            throw new InvalidDataException("Missing weighted intent support.");
        var candidate = support.Candidates.SingleOrDefault(x =>
            x.IntentId == intentId) ??
            throw new InvalidOperationException(
                "Illegal or stale semantic intent candidate ID.");
        return Resolve(current, fingerprint, candidate.TicketStart);
    }

    /// <summary>
    /// The initial two-site pilot does not optimize drawn-card costs.
    /// Bound and source hook are taken from the actual semantic cost
    /// continuation, but sampling is an explicitly declared engine-owned
    /// deterministic fallback, NOT an audited native RNG distribution.
    /// </summary>
    private static SemanticPilotCombatV111 NormalizeNoncontrolledCosts(
        SemanticPilotCombatV111 current)
    {
        while (current.PendingEndTurn is { CostPending: { } cost } endTurn)
        {
            var bound = cost.SupportCount;
            if (bound < 1)
                throw new InvalidDataException("Invalid engine-owned cost bound.");
            SemanticOtherChanceStateV111? nextOther = null;
            int index;
            if (current.OtherChance is { } other)
            {
                var choice = SemanticOtherChanceEngineV111.ResolveDrawCost(
                    other, cost);
                nextOther = choice.Next;
                index = choice.Decision.SelectedIndex;
            }
            else
            {
                // Preserve pre-existing p0-v1 sessions unless the caller
                // explicitly negotiates the named native-codec provider.
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
                    current.OpeningSource.Seed + ":" +
                    endTurn.Fingerprint + ":" + cost.SiteId));
                index = (int)(BitConverter.ToUInt32(hash, 0) % (uint)bound);
            }
            var resolved = SemanticEndTurnEngineV111.Resolve(
                endTurn, endTurn.Fingerprint, index);
            current = current with
            {
                Run = resolved.Run,
                PendingEndTurn = resolved.Complete ? null : resolved,
                OtherChance = nextOther ?? current.OtherChance
            };
        }
        Validate(current);
        return current;
    }

    public static void Validate(SemanticPilotCombatV111 state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Mode != SemanticPilotCombatV111.SchemaId ||
            state.History is null ||
            state.NativeEncounterEntry != state.OpeningSource.Origin
                .Formation.NativeEncounterEntry ||
            state.Run.Phase is not (
                RunPhase.Combat or RunPhase.Reward or RunPhase.Terminal))
            throw new InvalidDataException("Invalid semantic playable session.");
        if (state.PendingEndTurn is { } endTurn)
        {
            SemanticEndTurnEngineV111.Validate(endTurn);
            if (endTurn.Complete || endTurn.CostPending is not null ||
                CanonicalJson.Sha256(endTurn.Run) != CanonicalJson.Sha256(state.Run))
                throw new InvalidDataException(
                    "Semantic playable and end-turn continuation diverged.");
        }
        if (state.OtherChance is not null)
            SemanticOtherChanceEngineV111.Validate(state.OtherChance);
        if (state.RewardAudit is { } reward &&
            (state.Run.Phase != RunPhase.Reward ||
             reward.ProviderId != SemanticRewardAuditV111.PrototypeProviderId ||
             reward.ProbabilityStatus != "UNVERIFIED" ||
             reward.AfterCalls < reward.BeforeCalls ||
             state.Run.Rng.Streams.Single(x =>
                 x.StreamId == "reward").CallCount != reward.AfterCalls))
            throw new InvalidDataException(
                "Invalid source-timed reward RNG audit.");
        if (state.SeededEntry is { } entry &&
            (entry.Seed != state.OpeningSource.Seed ||
             entry.Act != state.OpeningSource.Origin.Combat.Act ||
             entry.NativeEncounterEntry != state.NativeEncounterEntry ||
             entry.TotalFloor != state.OpeningSource.Origin.Formation.TotalFloor ||
             (entry.OtherChanceProviderId is null) !=
                 (state.OtherChance is null) ||
             (entry.OtherChanceProviderId is not null &&
              entry.OtherChanceProviderId != state.OtherChance?.ProviderId)))
            throw new InvalidDataException(
                "Seeded Nature entry provenance disagrees with its opening.");
        if (state.UsesNativePolicyBaseline)
        {
            if (state.OtherChance is not null || state.PendingEndTurn is not null)
                throw new InvalidDataException(
                    "Native policy baseline cannot retain Nature-only continuations.");
            var nativeAudit = state.NativePolicyAudit ??
                throw new InvalidDataException(
                    "Native policy baseline is missing provider provenance.");
            if (nativeAudit.ProviderId !=
                    SemanticNativePolicyAuditV111.NativeProviderId ||
                nativeAudit.ProbabilityStatus != "UNVERIFIED" ||
                nativeAudit.Decisions is null)
                throw new InvalidDataException(
                    "Native policy baseline has invalid provider identity.");
            for (int index = 0; index < nativeAudit.Decisions.Length; index++)
            {
                var decision = nativeAudit.Decisions[index];
                if (decision is null || decision.Sequence != index + 1)
                    throw new InvalidDataException(
                        "Native policy decision sequence is invalid at " + index + ".");
                if (decision.ProviderMode != "native-v111")
                    throw new InvalidDataException(
                        "Native policy decision provider is invalid at " + index + ".");
                if (decision.NativeCursorBefore is null ||
                    decision.NativeCursorAfter is null)
                    throw new InvalidDataException(
                        "Native policy decision cursor is missing at " + index + ".");
                if (decision.Request is null || decision.Request.Ordinal < 0 ||
                    string.IsNullOrWhiteSpace(decision.Request.StreamId))
                    throw new InvalidDataException(
                        "Native policy decision request is invalid at " + index + ".");
            }
            if (state.AtPlayerBoundary)
                ValidateNativePolicyBoundary(state.Run);
            else
                NativeRunRngV111.Validate(state.Run);
        }
        else if (state.AtPlayerBoundary)
            SemanticPlayerActionEngineV111.ValidateBoundary(state.Run);
        else if (state.NativePolicyAudit is not null)
            throw new InvalidDataException(
                "Nature-controlled sessions cannot retain native policy decisions.");
        if (state.CombatTerminal && state.PendingEndTurn is not null)
            throw new InvalidDataException(
                "Terminal combat retained an unresolved chance.");
    }

    public static string Save(SemanticPilotCombatV111 state)
    {
        Validate(state);
        return JsonSerializer.Serialize(state);
    }

    public static SemanticPilotCombatV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var saved = JsonSerializer.Deserialize<SemanticPilotCombatV111>(json)
            ?? throw new InvalidDataException("Null semantic playable snapshot.");
        Validate(saved);
        var replay = saved.SeededEntry is { } entry
            ? BeginSeeded(entry)
            : Begin(saved.OpeningSource);
        foreach (var transition in saved.History)
        {
            replay = transition.Kind switch
            {
                "player" when transition.ActionId is not null =>
                    Step(replay, replay.Fingerprint, transition.ActionId),
                "nature" when transition.SelectedIndex is not null =>
                    Resolve(replay, replay.Fingerprint,
                        transition.SelectedIndex.Value),
                _ => throw new InvalidDataException(
                    "Unknown semantic playable transcript stage.")
            };
        }
        if (CanonicalJson.Sha256(replay) != CanonicalJson.Sha256(saved))
            throw new InvalidDataException(
                "Semantic playable snapshot disagrees with action/Nature replay.");
        return replay;
    }

    private static void ValidateNativePolicyBoundary(RunState run)
    {
        NativeRunRngV111.Validate(run);
        if (run.Phase != RunPhase.Combat || run.World?.Combat is not { } combat)
            throw new InvalidDataException(
                "Native policy action did not reach an active combat boundary.");
        if (!combat.IsPlayerTurn || combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            combat.AutomaticPipelineContinuation is not null)
            throw new InvalidDataException(
                "Native policy action did not reach an idle player boundary.");
        if (combat.NativeRng is null || run.NativeRunRng is null)
            throw new InvalidDataException(
                "Native policy combat lost its RNG stream owner.");
        if (combat.SemanticDeck is not { } deck ||
            deck.Mode != SemanticDeckStateV111.NativeMode ||
            deck.Pending is not null)
            throw new InvalidDataException(
                "Native policy combat lost its exact semantic deck state.");
        if (!SemanticDeckEngineV111.AllCardInstanceIds(deck)
                .Order().SequenceEqual(combat.DrawPile.Order()))
            throw new InvalidDataException(
                "Native policy exact deck does not match the physical draw pile.");
        SemanticDeckEngineV111.Validate(deck);
    }
}
