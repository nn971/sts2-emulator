using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Sts2Emulator.Core;

/// <summary>One externally replayable action or Nature decision.</summary>
public sealed record SemanticPilotTransitionV111(
    string Kind, string? ActionId = null, int? SelectedIndex = null);

/// <summary>
/// Independently replayable setup provenance. The default entry is a
/// source-shaped encounter fixture; SourceRouteEntry instead replays the
/// configured Act 1 start into the real native weak-combat entry hook.
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

public sealed record SemanticSourceRouteAuditV111(
    string ProviderId, ActIdentity ActOneRegion, string[] ActionIds,
    string MapNodeId, string EncounterId, string NativeEncounterEntry,
    RandomBoundaryEventV111[] NativeEntryDecisions,
    string ProbabilityStatus = "UNVERIFIED")
{
    public const string SourceRouteProviderId =
        "engine-owned-v111-native-weak-route-entry-unverified-v1";
}

public sealed record SemanticSeededEntryV111(
    string Seed, int Act, string EncounterId, string NativeEncounterEntry,
    int TotalFloor, int Ascension, long FirstMonsterCombatId,
    int? MapColumn, int? MapRow,
    string? OtherChanceProviderId = null,
    string? FixtureDrawCostRelicId = null,
    bool ReplayOpeningDrawHooks = false,
    bool NativePolicyBaseline = false,
    bool SourceRouteEntry = false,
    ActIdentity RouteActOneRegion = ActIdentity.Overgrowth,
    string? RouteNeowChoiceId = null,
    string? RouteMapNodeId = null);

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
    public SemanticSourceRouteAuditV111? SourceRouteAudit { get; init; }
    public ControlledTurnIntentsV111? PendingInitialIntents { get; init; }
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    [JsonIgnore] public bool AtPlayerBoundary =>
        PendingInitialIntents is null &&
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
                OpeningSource,
                SourceRouteAudit
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
        PendingInitialIntents?.Pending ?? PendingEndTurn?.IntentPending;
    [JsonIgnore] public SemanticDeckEventV111? DrawPending =>
        PendingEndTurn?.DrawPending;

    public SemanticPilotCombatV111 Fork() => this with
    {
        OpeningSource = OpeningSource.Fork(),
        Run = Run.Fork(),
        PendingEndTurn = PendingEndTurn?.Fork(),
        PendingInitialIntents = PendingInitialIntents?.Fork(),
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
            : null,
        SourceRouteAudit = SourceRouteAudit is { } route
            ? route with
            {
                ActionIds = (string[])route.ActionIds.Clone(),
                NativeEntryDecisions = route.NativeEntryDecisions.Select(
                    decision => decision with
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
    /// choose the initial HP, intent or opening shuffle. By default this is a
    /// source-shaped encounter fixture; the opt-in source-route mode replays
    /// the configured Act 1 route and enters combat through the real engine.
    /// Opening order is imported as known and is never randomized again by a
    /// deferred draw ticket. Route probabilities and noncombat RNG remain
    /// explicitly unverified.
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
        if (entry.SourceRouteEntry)
            return BeginSourceRoute(entry);
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

    private static SemanticPilotCombatV111 BeginSourceRoute(
        SemanticSeededEntryV111 entry)
    {
        if (entry.Act != 1 || entry.Ascension is < 0 or > 10 ||
            entry.RouteActOneRegion is not (ActIdentity.Overgrowth or
                ActIdentity.Underdocks) || entry.OtherChanceProviderId is not null ||
            entry.FixtureDrawCostRelicId is not null ||
            entry.ReplayOpeningDrawHooks)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_ROUTE site=act1.entry " +
                "operation=route_configuration");

        var configuration = RunConfiguration.Create(
            "silent", entry.Ascension, entry.RouteActOneRegion) with
        {
            FidelityId = V111Build.NativeWeakCombatEntryFidelityId
        };
        var engine = new PrototypeGameEngine();
        var run = V111RunFactory.Create(entry.Seed, configuration);
        var actions = new List<string>();
        string? neowChoice = null;
        string? mapNodeId = null;
        using var setupJournal = RandomBoundaryJournalV111.Begin();
        using var initialIntentCapture =
            SemanticInitialIntentCaptureV111.Begin();

        for (int step = 0; step < 32 && run.Phase != RunPhase.Combat; step++)
        {
            var legal = engine.GetLegalActions(run);
            if (legal.Count == 0)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_ROUTE site=act1.entry " +
                    "operation=no_legal_route_action");

            GameAction selected;
            if (run.Phase == RunPhase.MapChoice)
            {
                var world = run.World ?? throw new InvalidDataException(
                    "Source route lost its world before the first combat.");
                var available = legal.Where(action =>
                {
                    if (action.Kind != "choose_map_node") return false;
                    var nodeId = action.ReadPayload<ChooseMapNodePayload>()
                        .NodeId;
                    var node = world.Map.Nodes.SingleOrDefault(candidate =>
                        candidate.NodeId == nodeId);
                    return node is not null && node.Floor == 1 &&
                        (world.Map.EntryNodeIds ?? []).Contains(nodeId,
                            StringComparer.Ordinal) &&
                        node.RoomType == PrototypeRoomType.Combat;
                }).ToArray();
                selected = entry.RouteMapNodeId is { Length: > 0 } requested
                    ? available.SingleOrDefault(action =>
                        action.ReadPayload<ChooseMapNodePayload>().NodeId == requested)
                        ?? throw new InvalidOperationException(
                            "Requested source-route map node is not a legal first-floor combat.")
                    : available.FirstOrDefault()
                        ?? throw new NotSupportedException(
                            "UNSUPPORTED_SEMANTIC_ROUTE site=act1.map " +
                            "operation=no_first_floor_combat_entry");
                mapNodeId = selected.ReadPayload<ChooseMapNodePayload>().NodeId;
            }
            else if (run.Phase == RunPhase.Event && run.World?.Event is { } evt &&
                     evt.EventId == PrototypeNativeOvergrowthEvents.NeowEventId &&
                     neowChoice is null)
            {
                var choices = legal.Where(action => action.Kind == "event_choice")
                    .ToArray();
                selected = entry.RouteNeowChoiceId is { Length: > 0 } requested
                    ? choices.SingleOrDefault(action =>
                        action.ReadPayload<EventChoicePayload>().ChoiceId == requested)
                        ?? throw new InvalidOperationException(
                            "Requested Neow route choice is not currently offered.")
                    : choices.FirstOrDefault()
                        ?? throw new NotSupportedException(
                            "UNSUPPORTED_SEMANTIC_ROUTE site=act1.neow " +
                            "operation=no_legal_starting_choice");
                neowChoice = selected.ReadPayload<EventChoicePayload>().ChoiceId;
            }
            else if (run.Phase is RunPhase.Event)
            {
                // Source-shaped opening relics can add a small follow-up
                // choice. Resolve it deterministically before entering the
                // first map node; route actions are recorded for replay.
                selected = legal[0];
            }
            else if (run.Phase == RunPhase.Reward)
            {
                // Some source-shaped Neow relics open a follow-up reward.
                // The first legal option is a deterministic route choice;
                // its exact action is preserved in the replay audit.
                selected = legal[0];
            }
            else if (run.Phase == RunPhase.RunStart)
            {
                selected = legal.Single(action => action.Kind == "start_run");
            }
            else
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_ROUTE site=act1.entry phase=" +
                    run.Phase);

            actions.Add(PrototypeAiEnvironment.StableActionId(selected));
            try
            {
                run = engine.Step(run, selected).State;
            }
            catch (SemanticInitialIntentBoundaryException)
            {
                run = initialIntentCapture.State ??
                    throw new InvalidDataException(
                        "Native combat entry suspended without a saved source state.");
                break;
            }
        }

        if (initialIntentCapture.State is null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_ROUTE site=act1.entry " +
                "operation=initial_intent_boundary_not_reached");
        if (run.Phase != RunPhase.Combat || run.World?.Combat is not { } combat ||
            run.World.ActiveRoom != PrototypeRoomType.Combat ||
            combat.PendingChoice is not null ||
            combat.AutomaticPipelineContinuation is not null ||
            combat.Enemies.Any(enemy => enemy.PlannedMoveIndex is not null ||
                enemy.PlannedNextAiStateId is not null) ||
            combat.NativeRng is not { } nativeCombat ||
            run.NativeRunRng is null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_ROUTE site=act1.entry " +
                "operation=not_a_native_player_boundary");
        NativeCombatRngEntryV111.Validate(combat);
        NativeRunRngV111.Validate(run);

        var routeWorld = run.World;
        var actualEncounterId = (routeWorld.EncounterHistory ?? [])
            .LastOrDefault()
            ?? throw new InvalidDataException(
                "Source route reached combat without encounter history.");
        var actualNativeEntry = nativeCombat.Formation.NativeEncounterEntry;
        var actualNodeId = mapNodeId ?? routeWorld.Map.CurrentNodeId ??
            throw new InvalidDataException(
                "Source route reached combat without a map node.");
        var actualNode = routeWorld.Map.Nodes.Single(node =>
            node.NodeId == actualNodeId);
        var nodeParts = actualNodeId.Split(':');
        if (nodeParts.Length != 3 ||
            !int.TryParse(nodeParts[2], out var mapColumn))
            throw new InvalidDataException(
                "Source route map node lacks its pinned Act 1 coordinate.");
        if ((entry.EncounterId != "@source-route" &&
             entry.EncounterId != actualEncounterId) ||
            (entry.NativeEncounterEntry != "@source-route" &&
             entry.NativeEncounterEntry != actualNativeEntry) ||
            routeWorld.Floor != 1 ||
            actualNode.RoomType != PrototypeRoomType.Combat)
            throw new InvalidDataException(
                "Source route differs from the requested pinned combat entry.");

        var nativeDecisions = setupJournal.Entries
            .Where(decision => decision.ProviderMode == "native-v111")
            .Select((decision, index) => decision with
            {
                Sequence = index + 1,
                Request = decision.Request with
                {
                    OrderedCandidates = (int[])decision.Request
                        .OrderedCandidates.Clone()
                }
            }).ToArray();
        var routeAudit = new SemanticSourceRouteAuditV111(
            SemanticSourceRouteAuditV111.SourceRouteProviderId,
            entry.RouteActOneRegion, actions.ToArray(), actualNodeId,
            actualEncounterId, actualNativeEntry, nativeDecisions);

        var resolvedEntry = entry with
        {
            EncounterId = actualEncounterId,
            NativeEncounterEntry = actualNativeEntry,
            TotalFloor = routeWorld.Floor,
            FirstMonsterCombatId = nativeCombat.Formation.MonsterContexts
                .First().CombatId,
            MapColumn = mapColumn,
            MapRow = actualNode.Floor - 1,
            RouteNeowChoiceId = neowChoice,
            RouteMapNodeId = actualNodeId
        };
        var baselineCombat = combat.Fork() with { SemanticDeck = null };
        var origin = new ControlledIntentStateV111(
            baselineCombat.Fork() with { NativeRng = null },
            baselineCombat with { NativeRng = null },
            nativeCombat.Formation.Fork(), nativeCombat.RunStreams.Fork(),
            combat.Enemies.Length, [], null,
            Mode: "controlled-v111-source-route-origin-v1",
            PriorHpTranscript: []);
        var sourceOpening = new ControlledPlayableCombatV111(
            entry.Seed, origin, run.Fork(), -1, [], [], null,
            Mode: "controlled-v111-source-route-origin-v1");

        CombatState semanticCombat;
        RunState semanticRun;
        ControlledTurnIntentsV111? pendingInitialIntents = null;
        RandomBoundaryEventV111[] nativePolicyDecisions = nativeDecisions;
        if (entry.NativePolicyBaseline)
        {
            RandomBoundaryEventV111[] committedIntentDecisions;
            using (var intentJournal = RandomBoundaryJournalV111.Begin())
            {
                PrototypeGameEngine.NativeCommittedIntentAuditV111 committed;
                using (ControlledChanceGuardV111.Enter())
                using (ControlledChanceGuardV111.AllowEngineOwnedNativeProvider())
                using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
                    committed = PrototypeGameEngine.CommitEnemyIntentsWithAudit(
                        combat, run.Rng);
                using (ControlledChanceGuardV111.Enter())
                using (ControlledChanceGuardV111.AllowEngineOwnedNativeProvider())
                using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
                    run = PrototypeGameEngine.ResumeSemanticInitialCombatEntry(
                        run, committed.Combat);
                committedIntentDecisions = intentJournal.Entries
                    .Where(decision => decision.ProviderMode == "native-v111")
                    .ToArray();
            }
            nativePolicyDecisions = nativeDecisions
                .Concat(committedIntentDecisions)
                .Select((decision, index) => decision with
                {
                    Sequence = index + 1,
                    Request = decision.Request with
                    {
                        OrderedCandidates = (int[])decision.Request
                            .OrderedCandidates.Clone()
                    }
                }).ToArray();
            var completedWorld = run.World ?? throw new InvalidDataException(
                "Native source route lost its world after intent resolution.");
            var completedCombat = completedWorld.Combat ??
                throw new InvalidDataException(
                    "Native source route lost combat after intent resolution.");
            semanticCombat = completedCombat with
            {
                SemanticDeck = SemanticDeckEngineV111.BeginNative(
                    completedCombat.DrawPile.Reverse().ToArray())
            };
            semanticRun = run with
            {
                World = completedWorld with { Combat = semanticCombat }
            };
        }
        else
        {
            semanticCombat = combat with
            {
                NativeRng = null,
                SemanticDeck = null
            };
            semanticRun = run with
            {
                NativeRunRng = null,
                World = routeWorld with { Combat = semanticCombat }
            };
            pendingInitialIntents = ControlledTurnIntentEngineV111.Begin(
                semanticCombat, actualNativeEntry,
                siteId: "combat.committed_intent.index_draw");
            if (pendingInitialIntents.Complete)
            {
                using (ControlledChanceGuardV111.Enter())
                    semanticRun = PrototypeGameEngine
                        .ResumeSemanticInitialCombatEntry(
                            semanticRun, pendingInitialIntents.Combat);
                var completedWorld = semanticRun.World ??
                    throw new InvalidDataException(
                        "Nature source route lost its world after intent resolution.");
                var completedCombat = completedWorld.Combat ??
                    throw new InvalidDataException(
                        "Nature source route lost combat after intent resolution.");
                semanticCombat = completedCombat with
                {
                    SemanticDeck = SemanticDeckEngineV111
                        .FromCommittedOpening(
                            completedCombat.DrawPile.Reverse().ToArray())
                };
                semanticRun = semanticRun with
                {
                    World = completedWorld with { Combat = semanticCombat }
                };
            }
            else
            {
                semanticCombat = pendingInitialIntents.Combat.Fork();
                semanticRun = semanticRun with
                {
                    World = routeWorld with { Combat = semanticCombat }
                };
            }
        }

        var state = new SemanticPilotCombatV111(
            sourceOpening, semanticRun, null, [], actualNativeEntry)
        {
            SeededEntry = resolvedEntry,
            SourceRouteAudit = routeAudit,
            PendingInitialIntents = pendingInitialIntents is { Complete: false }
                ? pendingInitialIntents
                : null,
            NativePolicyAudit = entry.NativePolicyBaseline
                ? new SemanticNativePolicyAuditV111(
                    SemanticNativePolicyAuditV111.NativeProviderId,
                    nativePolicyDecisions)
                : null
        };
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
        var recorded = boundaries.Entries
            .Where(entry => entry.ProviderMode == "native-v111")
            .Select((entry, index) => entry with
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
        if (current.PendingInitialIntents is { Pending: not null } initialIntents)
        {
            var resolvedInitial = ControlledTurnIntentEngineV111.Resolve(
                initialIntents, selectedIndex, current.Run.Rng);
            var world = current.Run.World ?? throw new InvalidDataException(
                "Initial intent resolution lost the source route world.");
            var nextRun = current.Run with
            {
                World = world with { Combat = resolvedInitial.Combat }
            };
            ControlledTurnIntentsV111? pendingInitial = resolvedInitial.Complete
                ? null : resolvedInitial;
            if (resolvedInitial.Complete)
            {
                using (ControlledChanceGuardV111.Enter())
                    nextRun = PrototypeGameEngine
                        .ResumeSemanticInitialCombatEntry(
                            nextRun, resolvedInitial.Combat);
                var completedWorld = nextRun.World ??
                    throw new InvalidDataException(
                        "Initial intent completion lost the source route world.");
                var completedCombat = completedWorld.Combat ??
                    throw new InvalidDataException(
                        "Initial intent completion lost combat.");
                if (nextRun.Phase != RunPhase.Combat ||
                    !completedCombat.IsPlayerTurn ||
                    completedCombat.PendingChoice is not null ||
                    completedCombat.AutomaticPipelineContinuation is not null)
                    throw new NotSupportedException(
                        "UNSUPPORTED_SEMANTIC_ROUTE site=combat.opening " +
                        "operation=unmodeled_opening_continuation");
                var semanticCombat = completedCombat with
                {
                    SemanticDeck = SemanticDeckEngineV111
                        .FromCommittedOpening(
                            completedCombat.DrawPile.Reverse().ToArray())
                };
                nextRun = nextRun with
                {
                    World = completedWorld with { Combat = semanticCombat }
                };
            }
            var initialState = current with
            {
                Run = nextRun,
                PendingInitialIntents = pendingInitial,
                History = current.History.Append(
                    new SemanticPilotTransitionV111("nature",
                        SelectedIndex: selectedIndex)).ToArray()
            };
            Validate(initialState);
            return initialState;
        }
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
        var ticket = current.PendingInitialIntents ??
            current.PendingEndTurn?.Intents;
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
        if (state.PendingInitialIntents is { } initialIntents)
        {
            ControlledTurnIntentEngineV111.Validate(initialIntents);
            if (initialIntents.Complete || state.PendingEndTurn is not null ||
                state.SeededEntry?.SourceRouteEntry != true ||
                state.Run.World?.Combat is not { } entryCombat ||
                CanonicalJson.Sha256(entryCombat) !=
                    CanonicalJson.Sha256(initialIntents.Combat) ||
                entryCombat.NativeRng is not null ||
                entryCombat.SemanticDeck is not null ||
                state.Run.NativeRunRng is not null)
                throw new InvalidDataException(
                    "Initial intent continuation diverged from the source-route boundary.");
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
        if (state.SeededEntry is { SourceRouteEntry: true } routeEntry)
        {
            var route = state.SourceRouteAudit ?? throw new InvalidDataException(
                "Source-route semantic session is missing its route audit.");
            var world = state.Run.World ?? throw new InvalidDataException(
                "Source-route semantic session is missing its run world.");
            if (route.ProviderId != SemanticSourceRouteAuditV111.SourceRouteProviderId ||
                route.ProbabilityStatus != "UNVERIFIED" ||
                route.ActOneRegion != routeEntry.RouteActOneRegion ||
                route.ActionIds is null || route.ActionIds.Length == 0 ||
                route.ActionIds.Any(string.IsNullOrWhiteSpace) ||
                route.MapNodeId != routeEntry.RouteMapNodeId ||
                route.EncounterId != routeEntry.EncounterId ||
                route.NativeEncounterEntry != routeEntry.NativeEncounterEntry ||
                world.Map.CurrentNodeId != route.MapNodeId ||
                (world.EncounterHistory ?? []).LastOrDefault() != route.EncounterId ||
                route.NativeEntryDecisions is null)
                throw new InvalidDataException(
                    "Source-route audit disagrees with the seeded run entry.");
        }
        else if (state.SourceRouteAudit is not null)
            throw new InvalidDataException(
                "Fixture semantic session cannot retain a source-route audit.");
        if (state.UsesNativePolicyBaseline)
        {
            if (state.OtherChance is not null || state.PendingEndTurn is not null ||
                state.PendingInitialIntents is not null)
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
