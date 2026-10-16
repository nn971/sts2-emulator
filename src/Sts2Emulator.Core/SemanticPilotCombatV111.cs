using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Sts2Emulator.Core;

/// <summary>One externally replayable action or Nature decision.</summary>
public sealed record SemanticPilotTransitionV111(
    string Kind, string? ActionId = null, int? SelectedIndex = null,
    SemanticIntentDecisionV111? IntentDecision = null,
    SemanticDrawDecisionV111? DrawDecision = null,
    SemanticTargetDecisionV111? TargetDecision = null,
    SemanticGeneratedCardDecisionV111? GeneratedCardDecision = null,
    SemanticCardSelectionDecisionV111? CardSelectionDecision = null,
    SemanticCostDecisionV111? CostDecision = null,
    SemanticPoolShuffleDecisionV111? PoolShuffleDecision = null,
    SemanticDrawInsertionDecisionV111? DrawInsertionDecision = null,
    SemanticHandCostSelectionDecisionV111? HandCostSelectionDecision = null,
    SemanticCardShortlistDecisionV111? CardShortlistDecision = null,
    SemanticRandomPileCardDecisionV111? RandomPileCardDecision = null);

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
    string? RouteMapNodeId = null,
    string? FixtureRandomTargetCardId = null,
    string? FixtureGenerationPotionId = null,
    string? FixtureSelectionCardId = null,
    bool FixtureNatureDrawCosts = false,
    string? FixturePoolGenerationPotionId = null,
    string? FixtureGeneratedDrawInsertionCardId = null,
    bool FixtureRandomFreeAttackDrawInsertion = false,
    int? FixtureRandomizeHandCostsMaxExclusive = null,
    int? FixtureSetRandomHandCardCost = null,
    string? FixtureSelectionPotionId = null);

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
    [JsonIgnore] public SemanticTargetEventV111? TargetPending =>
        SemanticPilotCombatEngineV111.DescribePendingTargetEvent(this);
    [JsonIgnore] public SemanticGeneratedCardEventV111? GeneratedCardPending =>
        SemanticPilotCombatEngineV111.DescribePendingGeneratedCardEvent(this);
    [JsonIgnore] public SemanticCardSelectionEventV111? CardSelectionPending =>
        SemanticPilotCombatEngineV111.DescribePendingCardSelectionEvent(this);
    [JsonIgnore] public SemanticCostEventV111? CostPending =>
        SemanticPilotCombatEngineV111.DescribePendingCostEvent(this);
    [JsonIgnore] public SemanticHandCostSelectionEventV111?
        HandCostSelectionPending =>
            SemanticPilotCombatEngineV111
                .DescribePendingHandCostSelectionEvent(this);
    [JsonIgnore] public SemanticCardShortlistEventV111?
        CardShortlistPending =>
            SemanticPilotCombatEngineV111
                .DescribePendingCardShortlistEvent(this);
    [JsonIgnore] public SemanticPoolShuffleEventV111? PoolShufflePending =>
        SemanticPilotCombatEngineV111.DescribePendingPoolShuffleEvent(this);
    [JsonIgnore] public SemanticDrawInsertionEventV111? DrawInsertionPending =>
        SemanticPilotCombatEngineV111.DescribePendingDrawInsertionEvent(this);
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    [JsonIgnore] public bool AtPlayerBoundary =>
        PendingInitialIntents is null &&
        PendingEndTurn is null &&
        Run.Phase == RunPhase.Combat &&
        Run.World?.Combat is { IsPlayerTurn: true,
            PendingChoice: null, AutomaticPipelineContinuation: null };
    [JsonIgnore] public bool CombatTerminal =>
        Run.Phase is RunPhase.Reward or RunPhase.Terminal;
    [JsonIgnore] public bool AtPlayerChoiceBoundary =>
        PendingEndTurn is null && PendingInitialIntents is null &&
        !CombatTerminal && CardSelectionPending is not null;
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
    [JsonIgnore] public SemanticDeckEventV111? DrawPending
    {
        get
        {
            if (PendingEndTurn?.DrawPending is { } endTurnDraw)
                return endTurnDraw;
            var combat = Run.World?.Combat;
            return combat?.PendingChoice?.ControlledSemanticDraw is not null &&
                combat.SemanticDeck is { } deck
                    ? SemanticDeckEngineV111.Inspect(deck) : null;
        }
    }
    [JsonIgnore] public SemanticRandomPileCardEventV111?
        RandomPileCardPending => PendingEndTurn?.RandomPileCardPending;

    public SemanticPilotCombatV111 Fork() => this with
    {
        OpeningSource = OpeningSource.Fork(),
        Run = Run.Fork(),
        PendingEndTurn = PendingEndTurn?.Fork(),
        PendingInitialIntents = PendingInitialIntents?.Fork(),
        History = History.Select(transition => transition switch
        {
            { IntentDecision: { } decision } => transition with
            {
                IntentDecision = decision with
                {
                    Event = decision.Event with
                    {
                        Support = decision.Event.Support with
                        {
                            Candidates = (SemanticIntentCandidateV111[])
                                decision.Event.Support.Candidates.Clone()
                        }
                    }
                }
            },
            { DrawDecision: { } decision } => transition with
            {
                DrawDecision = decision.Fork()
            },
            { RandomPileCardDecision: { } decision } => transition with
            {
                RandomPileCardDecision = decision.Fork()
            },
            { TargetDecision: { } decision } => transition with
            {
                TargetDecision = decision.Fork()
            },
            { GeneratedCardDecision: { } decision } => transition with
            {
                GeneratedCardDecision = decision.Fork()
            },
            { CardSelectionDecision: { } decision } => transition with
            {
                CardSelectionDecision = decision.Fork()
            },
            { CostDecision: { } decision } => transition with
            {
                CostDecision = decision.Fork()
            },
            { PoolShuffleDecision: { } decision } => transition with
            {
                PoolShuffleDecision = decision.Fork()
            },
            { DrawInsertionDecision: { } decision } => transition with
            {
                DrawInsertionDecision = decision.Fork()
            },
            { HandCostSelectionDecision: { } decision } => transition with
            {
                HandCostSelectionDecision = decision.Fork()
            },
            { CardShortlistDecision: { } decision } => transition with
            {
                CardShortlistDecision = decision.Fork()
            },
            _ => transition
        }).ToArray(),
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
        if (entry.FixtureNatureDrawCosts &&
            (entry.FixtureDrawCostRelicId != "proto.relic.snecko_eye" ||
             entry.OtherChanceProviderId is not null))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_COST site=combat.seeded_entry " +
                "operation=fixture_draw_cost_choices_require_only_snecko_fixture");
        if (entry.FixtureGeneratedDrawInsertionCardId is not null &&
            entry.FixtureRandomFreeAttackDrawInsertion)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_INSERTION site=combat.seeded_entry " +
                "operation=choose_one_draw_insertion_fixture");
        if (entry.FixtureRandomizeHandCostsMaxExclusive is < 1 or
                > SemanticNatureEnumeratorV111.MaximumSupportedOutcomes)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_COST site=combat.seeded_entry " +
                "operation=hand_cost_max_exclusive_out_of_range");
        if (entry.FixtureSetRandomHandCardCost is < 0)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_COST site=combat.seeded_entry " +
                "operation=hand_cost_value_out_of_range");
        if (entry.FixtureRandomizeHandCostsMaxExclusive is not null &&
            entry.FixtureSetRandomHandCardCost is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_COST site=combat.seeded_entry " +
                "operation=choose_one_hand_cost_fixture");
        if ((entry.FixtureRandomizeHandCostsMaxExclusive is not null ||
             entry.FixtureSetRandomHandCardCost is not null) &&
            (entry.FixtureRandomTargetCardId is not null ||
             entry.FixtureGenerationPotionId is not null ||
             entry.FixtureSelectionCardId is not null ||
             entry.FixtureSelectionPotionId is not null ||
             entry.FixturePoolGenerationPotionId is not null ||
             entry.FixtureGeneratedDrawInsertionCardId is not null ||
             entry.FixtureRandomFreeAttackDrawInsertion))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_COST site=combat.seeded_entry " +
                "operation=hand_cost_fixture_conflicts_with_other_pending_choice");
        if (entry.NativePolicyBaseline &&
            (entry.OtherChanceProviderId is not null ||
             entry.FixtureDrawCostRelicId is not null ||
             entry.FixtureRandomTargetCardId is not null ||
             entry.FixtureGenerationPotionId is not null ||
             entry.FixtureSelectionCardId is not null ||
             entry.FixtureSelectionPotionId is not null ||
             entry.FixtureNatureDrawCosts ||
             entry.FixturePoolGenerationPotionId is not null ||
             entry.FixtureGeneratedDrawInsertionCardId is not null ||
             entry.FixtureRandomFreeAttackDrawInsertion ||
             entry.FixtureRandomizeHandCostsMaxExclusive is not null ||
             entry.FixtureSetRandomHandCardCost is not null))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_NATIVE site=combat.seeded_entry " +
                "operation=native_baseline_fixture_loadout_conflict");
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
        if (entry.FixtureRandomTargetCardId is { } targetCardId)
            state = ApplyFixtureRandomTargetCard(state, targetCardId);
        if (entry.FixtureGenerationPotionId is { } potionId)
            state = ApplyFixtureGenerationPotion(state, potionId);
        if (entry.FixtureSelectionCardId is { } selectionCardId)
            state = ApplyFixtureSelectionCard(state, selectionCardId);
        if (entry.FixtureSelectionPotionId is { } selectionPotionId)
            state = ApplyFixtureSelectionPotion(state, selectionPotionId);
        if (entry.FixturePoolGenerationPotionId is { } poolPotionId)
            state = ApplyFixturePoolGenerationPotion(state, poolPotionId);
        if (entry.FixtureGeneratedDrawInsertionCardId is { } insertionCardId)
            state = ApplyFixtureGeneratedDrawInsertion(state, insertionCardId);
        if (entry.FixtureRandomFreeAttackDrawInsertion)
            state = ApplyFixtureRandomAttackDrawInsertion(state);
        if (entry.FixtureRandomizeHandCostsMaxExclusive is { } maxExclusive)
            state = ApplyFixtureRandomizeHandCosts(state, maxExclusive);
        if (entry.FixtureSetRandomHandCardCost is { } setCost)
            state = ApplyFixtureSetRandomHandCardCost(state, setCost);
        Validate(state);
        return state;
    }

    private static SemanticPilotCombatV111 ApplyFixtureGeneratedDrawInsertion(
        SemanticPilotCombatV111 state, string cardId)
    {
        var operation = new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions,
            Amount: 1, CardId: cardId);
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.BeginControlledDrawInsertionFixture(
                state.Run.Fork(), operation);
        ValidateDrawInsertionBoundary(result);
        return state with { Run = result };
    }

    private static SemanticPilotCombatV111 ApplyFixtureRandomAttackDrawInsertion(
        SemanticPilotCombatV111 state)
    {
        var operation = new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile,
            Amount: 1);
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.BeginControlledDrawInsertionFixture(
                state.Run.Fork(), operation);
        ValidateDrawInsertionBoundary(result);
        return state with { Run = result };
    }

    private static SemanticPilotCombatV111 ApplyFixtureRandomizeHandCosts(
        SemanticPilotCombatV111 state, int maxExclusive)
    {
        var operation = new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed,
            Amount: maxExclusive);
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.BeginControlledRandomCostFixture(
                state.Run.Fork(), operation);
        ValidateRandomHandCostBoundary(result);
        return state with { Run = result };
    }

    private static SemanticPilotCombatV111 ApplyFixtureSetRandomHandCardCost(
        SemanticPilotCombatV111 state, int cost)
    {
        var operation = new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.SetRandomHandCardEnergyCostUntilTurnEndOrPlayed,
            Amount: cost);
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.BeginControlledRandomCostFixture(
                state.Run.Fork(), operation);
        ValidateRandomHandCostBoundary(result);
        return state with { Run = result };
    }

    private static SemanticPilotCombatV111 ApplyFixturePoolGenerationPotion(
        SemanticPilotCombatV111 state, string potionId)
    {
        if (potionId != "proto.potion.colorless")
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                "operation=fixture_pool_generation_potion:" + potionId);
        var world = state.Run.World ?? throw new InvalidDataException(
            "Pool-generation fixture is missing its combat world.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "Pool-generation fixture is missing combat.");
        var slot = Array.FindIndex(state.Run.Player.PotionSlots,
            potion => potion is null);
        if (slot < 0 || (combat.Potions ?? []).Any(potion =>
                potion.PotionId == potionId))
            throw new InvalidOperationException(
                "Pool-generation fixture requires an empty potion slot.");
        var slots = (PotionInstance?[])state.Run.Player.PotionSlots.Clone();
        slots[slot] = new PotionInstance(potionId, PrototypeJson.EmptyObject());
        var potions = (combat.Potions ?? []).Append(
            new CombatPotionState(slot, potionId, PrototypeJson.EmptyObject()))
            .OrderBy(potion => potion.Slot).ToArray();
        return state with
        {
            Run = state.Run with
            {
                Player = state.Run.Player with { PotionSlots = slots },
                World = world with { Combat = combat with { Potions = potions } }
            }
        };
    }

    private static SemanticPilotCombatV111 ApplyFixtureSelectionPotion(
        SemanticPilotCombatV111 state, string potionId)
    {
        if (!IsSemanticSelectionPotion(potionId))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                "operation=fixture_selection_potion:" + potionId);
        var world = state.Run.World ?? throw new InvalidDataException(
            "Selection-potion fixture is missing its combat world.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "Selection-potion fixture is missing combat.");
        var slot = Array.FindIndex(state.Run.Player.PotionSlots,
            potion => potion is null);
        if (slot < 0 || (combat.Potions ?? []).Any(potion =>
                potion.PotionId == potionId))
            throw new InvalidOperationException(
                "Selection-potion fixture requires an empty potion slot.");
        var slots = (PotionInstance?[])state.Run.Player.PotionSlots.Clone();
        slots[slot] = new PotionInstance(potionId, PrototypeJson.EmptyObject());
        var potions = (combat.Potions ?? []).Append(
            new CombatPotionState(slot, potionId, PrototypeJson.EmptyObject()))
            .OrderBy(potion => potion.Slot).ToArray();
        return state with
        {
            Run = state.Run with
            {
                Player = state.Run.Player with { PotionSlots = slots },
                World = world with { Combat = combat with { Potions = potions } }
            }
        };
    }

    private static bool IsSemanticSelectionPotion(string? potionId) =>
        potionId is ("proto.potion.touch_of_insanity" or
            "proto.potion.droplet_of_precognition" or
            "proto.potion.liquid_memories");

    private static bool IsSemanticEffectDrawCard(string cardId) =>
        cardId is ("proto.silent.backflip" or
            "proto.silent.quick_slash" or
            "proto.silent.adrenaline" or
            "proto.silent.escape_plan");

    private static SemanticPilotCombatV111 ApplyFixtureSelectionCard(
        SemanticPilotCombatV111 state, string cardId)
    {
        if (cardId is not ("proto.silent.survivor" or
            "proto.colorless.seeker_strike" or
            "proto.silent.nightmare" or
            NativeGloryAncientsV111.WishCardId))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                "operation=fixture_selection_card:" + cardId);
        var world = state.Run.World ?? throw new InvalidDataException(
            "Card-selection fixture is missing its combat world.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "Card-selection fixture is missing combat.");
        if (combat.Hand.Length == 0)
            throw new InvalidOperationException(
                "Card-selection fixture requires an opening-hand card.");
        var selected = combat.Cards.Single(card =>
            card.InstanceId == combat.Hand[0]);
        var persistentId = selected.PersistentCardInstanceId ??
            throw new InvalidDataException(
                "Card-selection fixture card has no persistent deck identity.");
        var deckIndex = Array.FindIndex(state.Run.Player.Deck, card =>
            card.InstanceId == persistentId);
        if (deckIndex < 0)
            throw new InvalidDataException(
                "Card-selection fixture is absent from the player deck.");
        var cards = combat.Cards.Select(card =>
            card.InstanceId == selected.InstanceId
                ? card with { CardId = cardId }
                : card).ToArray();
        var playerDeck = (CardInstance[])state.Run.Player.Deck.Clone();
        playerDeck[deckIndex] = playerDeck[deckIndex] with { CardId = cardId };
        return state with
        {
            Run = state.Run with
            {
                Player = state.Run.Player with { Deck = playerDeck },
                World = world with { Combat = combat with { Cards = cards } }
            }
        };
    }

    private static SemanticPilotCombatV111 ApplyFixtureRandomTargetCard(
        SemanticPilotCombatV111 state, string cardId)
    {
        if (cardId != "proto.silent.ricochet")
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                "operation=fixture_random_target_card:" + cardId);
        var world = state.Run.World ?? throw new InvalidDataException(
            "Random-target fixture is missing its combat world.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "Random-target fixture is missing combat.");
        if (combat.Hand.Length == 0)
            throw new InvalidOperationException(
                "Random-target fixture requires a card in the opening hand.");
        var selected = combat.Cards.Single(card =>
            card.InstanceId == combat.Hand[0]);
        var persistentId = selected.PersistentCardInstanceId ??
            throw new InvalidDataException(
                "Random-target fixture card has no persistent deck identity.");
        var deckIndex = Array.FindIndex(state.Run.Player.Deck, card =>
            card.InstanceId == persistentId);
        if (deckIndex < 0)
            throw new InvalidDataException(
                "Random-target fixture card is absent from the player deck.");
        var cards = combat.Cards.Select(card =>
            card.InstanceId == selected.InstanceId
                ? card with { CardId = cardId }
                : card).ToArray();
        var playerDeck = (CardInstance[])state.Run.Player.Deck.Clone();
        playerDeck[deckIndex] = playerDeck[deckIndex] with { CardId = cardId };
        return state with
        {
            Run = state.Run with
            {
                Player = state.Run.Player with { Deck = playerDeck },
                World = world with
                {
                    Combat = combat with { Cards = cards }
                }
            }
        };
    }

    private static SemanticPilotCombatV111 ApplyFixtureGenerationPotion(
        SemanticPilotCombatV111 state, string potionId)
    {
        if (potionId != "proto.potion.orobic_acid")
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                "operation=fixture_generation_potion:" + potionId);
        var world = state.Run.World ?? throw new InvalidDataException(
            "Generation fixture is missing its combat world.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "Generation fixture is missing combat.");
        var slot = Array.FindIndex(state.Run.Player.PotionSlots,
            potion => potion is null);
        if (slot < 0 || (combat.Potions ?? []).Any(potion =>
                potion.PotionId == potionId))
            throw new InvalidOperationException(
                "Generation fixture requires an empty potion slot.");
        var slots = (PotionInstance?[])state.Run.Player.PotionSlots.Clone();
        slots[slot] = new PotionInstance(potionId, PrototypeJson.EmptyObject());
        var potions = (combat.Potions ?? []).Append(
            new CombatPotionState(slot, potionId, PrototypeJson.EmptyObject()))
            .OrderBy(potion => potion.Slot).ToArray();
        return state with
        {
            Run = state.Run with
            {
                Player = state.Run.Player with { PotionSlots = slots },
                World = world with { Combat = combat with { Potions = potions } }
            }
        };
    }

    private static SemanticPilotCombatV111 BeginSourceRoute(
        SemanticSeededEntryV111 entry)
    {
        if (entry.Act != 1 || entry.Ascension is < 0 or > 10 ||
            entry.RouteActOneRegion is not (ActIdentity.Overgrowth or
                ActIdentity.Underdocks) || entry.OtherChanceProviderId is not null ||
            entry.FixtureDrawCostRelicId is not null ||
            entry.FixtureRandomTargetCardId is not null ||
            entry.FixtureGenerationPotionId is not null ||
            entry.FixtureSelectionCardId is not null ||
            entry.FixtureSelectionPotionId is not null ||
            entry.FixturePoolGenerationPotionId is not null ||
            entry.FixtureGeneratedDrawInsertionCardId is not null ||
            entry.FixtureRandomFreeAttackDrawInsertion ||
            entry.FixtureRandomizeHandCostsMaxExclusive is not null ||
            entry.FixtureSetRandomHandCardCost is not null ||
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
            OtherChance = entry.NativePolicyBaseline
                ? null
                : SemanticOtherChanceEngineV111.Begin(
                    nativeCombat.RunStreams),
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
        if (current.AtPlayerChoiceBoundary)
            return StepCardSelection(current, actionId);
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
            var semanticSelectionCard = card?.CardId is
                ("proto.silent.survivor" or "proto.silent.hand_trick" or
                    "proto.silent.nightmare" or
                    NativeGloryAncientsV111.WishCardId);
            var semanticDrawCard = card is not null &&
                IsSemanticEffectDrawCard(card.CardId);
            var fixtureSelectionCard = card?.CardId ==
                    current.SeededEntry?.FixtureSelectionCardId &&
                card?.CardId == "proto.colorless.seeker_strike";
            var supportedCardAction = card is not null &&
                (SemanticPlayerActionEngineV111.SupportsCard(card.CardId) ||
                 card.CardId == "proto.silent.ricochet" ||
                 semanticDrawCard ||
                 semanticSelectionCard ||
                 fixtureSelectionCard);
            if (!supportedCardAction)
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
            {
                if (result.World?.Combat?.PendingChoice?
                        .ControlledSemanticDraw is not null)
                    ValidateSemanticActionDrawBoundary(result);
                else if (result.World?.Combat?.PendingChoice?
                        .ControlledCardShortlist is not null)
                    ValidateCardShortlistBoundary(result);
                else if (result.World?.Combat?.PendingChoice?.ChoiceId ==
                    "select_cards")
                    SemanticCardSelectionEngineV111
                        .ValidatePendingBoundary(result);
                else if (result.World?.Combat?.PendingChoice?
                    .ControlledPoolShuffle is not null)
                    ValidatePoolShuffleBoundary(result);
                else
                    ValidateSemanticCombatBoundary(result);
            }
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
            {
                if (!semanticDrawCard || next is null)
                    throw new NotSupportedException(
                        "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                        "operation=unmodeled_draw_pile_mutation");
                PrototypeStateInvariants.Validate(result);
            }
        }
        else if (action.Kind == "use_potion")
        {
            var payload = action.Payload.Deserialize<UsePotionPayload>() ??
                throw new InvalidDataException("Missing potion action payload.");
            var fixturePotion = current.SeededEntry?.FixtureGenerationPotionId;
            var poolPotion = current.SeededEntry?.FixturePoolGenerationPotionId;
            var validSlot = payload.Slot >= 0 &&
                payload.Slot < source.Player.PotionSlots.Length;
            var isGenerationPotion = fixturePotion ==
                    "proto.potion.orobic_acid" && validSlot &&
                source.Player.PotionSlots[payload.Slot]?.PotionId == fixturePotion;
            var isPoolPotion = poolPotion == "proto.potion.colorless" &&
                validSlot &&
                source.Player.PotionSlots[payload.Slot]?.PotionId == poolPotion;
            var isSelectionPotion = validSlot && IsSemanticSelectionPotion(
                source.Player.PotionSlots[payload.Slot]?.PotionId);
            if ((!isGenerationPotion && !isPoolPotion &&
                 !isSelectionPotion) ||
                payload.TargetEnemyId is not null)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=potion_effect_requires_semantic_audit");
            using (ControlledChanceGuardV111.Enter())
                result = environment.Step(source, actionId).State;
            if (result.Phase != RunPhase.Combat)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=unsupported_post_potion_phase");
            if (result.World?.Combat?.PendingChoice?.ControlledPoolShuffle is not null)
                ValidatePoolShuffleBoundary(result);
            else if (result.World?.Combat?.PendingChoice?.ChoiceId ==
                "select_cards")
                SemanticCardSelectionEngineV111.ValidatePendingBoundary(result);
            else
                ValidateSemanticCombatBoundary(result);
            var previous = current.Run.World!.Combat!;
            var next = result.World?.Combat;
            if (next is null ||
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

    private static SemanticPilotCombatV111 StepCardSelection(
        SemanticPilotCombatV111 current, string actionId)
    {
        var selection = SemanticCardSelectionEngineV111.Step(
            current.Run, actionId);
        var decision = selection.Decision ??
            throw new InvalidDataException(
                "Source card selection omitted its typed decision.");
        if (CanonicalJson.Sha256(decision.Event) !=
            CanonicalJson.Sha256(DescribePendingCardSelectionEvent(current)))
            throw new InvalidDataException(
                "Source card-selection event changed before resolution.");
        var next = current with
        {
            Run = selection.Run,
            History = current.History.Append(
                new SemanticPilotTransitionV111(
                    "semantic_card_selection",
                    CardSelectionDecision: decision.Fork())).ToArray()
        };
        Validate(next);
        return next;
    }

    /// <summary>
    /// Resolve one suspended random-enemy effect by stable physical enemy
    /// identity, then resume the original source continuation.
    /// </summary>
    public static SemanticPilotCombatV111 ResolveRandomTarget(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, int enemyInstanceId)
    {
        Validate(current);
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic target fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = current.TargetPending ??
            throw new InvalidOperationException(
                "No semantic random-target event is pending.");
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Semantic target event ID is stale or mismatched.");
        var index = Array.IndexOf(pending.CandidateEnemyInstanceIds,
            enemyInstanceId);
        if (index < 0)
            throw new InvalidOperationException(
                "Enemy instance is outside the pending target support.");
        var expected = new SemanticTargetDecisionV111(
            pending, index, enemyInstanceId);
        expected.Validate();

        RunState result;
        using (ControlledChanceGuardV111.Enter())
        using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
            result = PrototypeGameEngine.ResumeControlledRandomTarget(
                current.Run.Fork(), index);
        if (result.Phase == RunPhase.Combat)
        {
            ValidateSemanticCombatBoundary(result);
            var priorCombat = current.Run.World!.Combat!;
            var nextCombat = result.World!.Combat!;
            if (!priorCombat.DrawPile.SequenceEqual(nextCombat.DrawPile) ||
                CanonicalJson.Sha256(priorCombat.SemanticDeck) !=
                    CanonicalJson.Sha256(nextCombat.SemanticDeck))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.random_target " +
                    "operation=unmodeled_draw_pile_mutation");
        }
        else if (result.Phase is not (RunPhase.Reward or RunPhase.Terminal))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.random_target " +
                "operation=unsupported_post_target_phase");
        var next = current with
        {
            Run = result,
            RewardAudit = result.Phase == RunPhase.Reward
                ? RecordRewardAudit(current.Run, result)
                : current.RewardAudit,
            History = current.History.Append(
                new SemanticPilotTransitionV111(
                    "semantic_target", TargetDecision: expected.Fork()))
                .ToArray()
        };
        return NormalizeNoncontrolledCosts(next);
    }

    /// <summary>
    /// Resolve one generated card model from its ordered source pool and
    /// resume the original generation/trigger continuation.
    /// </summary>
    public static SemanticPilotCombatV111 ResolveGeneratedCard(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, string cardId)
    {
        Validate(current);
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic generated-card fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        var pending = current.GeneratedCardPending ??
            throw new InvalidOperationException(
                "No semantic generated-card event is pending.");
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Generated-card event ID is stale or mismatched.");
        var index = Array.IndexOf(pending.CandidateCardIds, cardId);
        if (index < 0)
            throw new InvalidOperationException(
                "Card model is outside the pending generated-card support.");
        var expected = new SemanticGeneratedCardDecisionV111(
            pending, index, cardId);
        expected.Validate();

        var drawInsertionPending = current.Run.World?.Combat?.PendingChoice?
            .ControlledDrawInsertion is { Stage: "card_model" };
        RunState result;
        using (ControlledChanceGuardV111.Enter())
        using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
            result = drawInsertionPending
                ? PrototypeGameEngine.ResumeControlledDrawInsertion(
                    current.Run.Fork(), index)
                : PrototypeGameEngine.ResumeControlledGeneratedCard(
                    current.Run.Fork(), index);
        if (result.Phase == RunPhase.Combat)
        {
            ValidateSemanticCombatBoundary(result);
            var priorCombat = current.Run.World!.Combat!;
            var nextCombat = result.World!.Combat!;
            if (!priorCombat.DrawPile.SequenceEqual(nextCombat.DrawPile) ||
                CanonicalJson.Sha256(priorCombat.SemanticDeck) !=
                    CanonicalJson.Sha256(nextCombat.SemanticDeck))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.generated_card " +
                    "operation=unmodeled_draw_pile_mutation");
        }
        else if (result.Phase is not (RunPhase.Reward or RunPhase.Terminal))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.generated_card " +
                "operation=unsupported_post_generation_phase");
        var next = current with
        {
            Run = result,
            RewardAudit = result.Phase == RunPhase.Reward
                ? RecordRewardAudit(current.Run, result)
                : current.RewardAudit,
            History = current.History.Append(
                new SemanticPilotTransitionV111(
                    "semantic_generated_card",
                    GeneratedCardDecision: expected.Fork())).ToArray()
        };
        return NormalizeNoncontrolledCosts(next);
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
        => Resolve(current, fingerprint, selectedIndex, null, null, false, null);

    public static SemanticPilotCombatV111 ResolveRandomPileCard(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, long cardInstanceId)
    {
        Validate(current);
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic random pile-card fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = current.RandomPileCardPending ??
            throw new InvalidOperationException(
                "No random pile-card Nature event is pending.");
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Random pile-card event ID is stale or mismatched.");
        var selectedIndex = Array.IndexOf(
            pending.CandidateCardInstanceIds, cardInstanceId);
        if (selectedIndex < 0)
            throw new InvalidOperationException(
                "Card instance is outside the random pile-card support.");
        var decision = SemanticRandomPileCardEngineV111.Resolve(
            pending, selectedIndex);
        return Resolve(current, fingerprint, selectedIndex,
            null, null, false, null, decision);
    }

    public static SemanticPilotCombatV111 ResolveCost(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, int selectedCost)
    {
        Validate(current);
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic cost fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = current.CostPending ??
            throw new InvalidOperationException(
                "No Nature-controlled drawn-card cost is pending.");
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Semantic cost event ID is stale or mismatched.");
        var index = Array.IndexOf(pending.CandidateCosts, selectedCost);
        if (index < 0)
            throw new InvalidOperationException(
                "Cost value is outside the pending event support.");
        var decision = new SemanticCostDecisionV111(
            pending, index, selectedCost);
        decision.Validate();
        if (pending.TargetCardInstanceId is not null)
        {
            ValidateCostDecision(current.CostPending, decision, index);
            ValidateRandomHandCostBoundary(current.Run);
            var priorCombat = current.Run.World!.Combat!;
            RunState result;
            using (ControlledChanceGuardV111.Enter())
                result = PrototypeGameEngine.ResumeControlledRandomCost(
                    current.Run.Fork(), index);
            var nextCombat = result.World?.Combat;
            if (result.Phase != RunPhase.Combat || nextCombat is null)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_COST site=combat.random_hand_card.energy_cost " +
                    "operation=unsupported_post_cost_phase");
            if (nextCombat.PendingChoice?.ControlledRandomCost is not null)
                ValidateRandomHandCostBoundary(result);
            else
                ValidateSemanticCombatBoundary(result);
            if (!priorCombat.DrawPile.SequenceEqual(nextCombat.DrawPile) ||
                CanonicalJson.Sha256(priorCombat.SemanticDeck) !=
                    CanonicalJson.Sha256(nextCombat.SemanticDeck))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.random_hand_card.energy_cost " +
                    "operation=unmodeled_draw_pile_mutation");
            var next = current with
            {
                Run = result,
                History = current.History.Append(
                    new SemanticPilotTransitionV111(
                        "semantic_cost", CostDecision: decision.Fork()))
                    .ToArray()
            };
            Validate(next);
            return next;
        }
        return Resolve(current, fingerprint, index, null, null, false, decision);
    }

    public static SemanticPilotCombatV111 ResolveHandCostSelection(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, long cardInstanceId)
    {
        Validate(current);
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic hand-cost selection fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = current.HandCostSelectionPending ??
            throw new InvalidOperationException(
                "No random hand-card cost selection is pending.");
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Hand-cost selection event ID is stale or mismatched.");
        var index = Array.IndexOf(pending.CandidateCardInstanceIds,
            cardInstanceId);
        if (index < 0)
            throw new InvalidOperationException(
                "Card is outside the pending hand-cost selection support.");
        var decision = new SemanticHandCostSelectionDecisionV111(
            pending, index, cardInstanceId,
            pending.CandidateCardModelIds[index]);
        decision.Validate();
        ValidateHandCostSelectionDecision(
            current.HandCostSelectionPending, decision, index);
        ValidateRandomHandCostBoundary(current.Run);
        var priorCombat = current.Run.World!.Combat!;
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.ResumeControlledRandomCost(
                current.Run.Fork(), index);
        var nextCombat = result.World?.Combat;
        if (result.Phase != RunPhase.Combat || nextCombat is null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_COST site=combat.random_hand_card.select " +
                "operation=unsupported_post_cost_phase");
        if (nextCombat.PendingChoice?.ControlledRandomCost is not null)
            ValidateRandomHandCostBoundary(result);
        else
            ValidateSemanticCombatBoundary(result);
        if (!priorCombat.DrawPile.SequenceEqual(nextCombat.DrawPile) ||
            CanonicalJson.Sha256(priorCombat.SemanticDeck) !=
                CanonicalJson.Sha256(nextCombat.SemanticDeck))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.random_hand_card.select " +
                "operation=unmodeled_draw_pile_mutation");
        var next = current with
        {
            Run = result,
            History = current.History.Append(
                new SemanticPilotTransitionV111(
                    "semantic_hand_cost_selection",
                    HandCostSelectionDecision: decision.Fork()))
                .ToArray()
        };
        Validate(next);
        return next;
    }

    public static SemanticPilotCombatV111 ResolveCardShortlist(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, long cardInstanceId)
    {
        Validate(current);
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic card-shortlist fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = current.CardShortlistPending ??
            throw new InvalidOperationException(
                "No random card-pile shortlist is pending.");
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Card-shortlist event ID is stale or mismatched.");
        var index = Array.IndexOf(pending.CandidateCardInstanceIds,
            cardInstanceId);
        if (index < 0)
            throw new InvalidOperationException(
                "Card is outside the pending shortlist support.");
        var decision = new SemanticCardShortlistDecisionV111(
            pending, index, cardInstanceId,
            pending.CandidateCardModelIds[index]);
        decision.Validate();
        ValidateCardShortlistDecision(
            current.CardShortlistPending, decision, index);
        ValidateCardShortlistBoundary(current.Run);
        var priorCombat = current.Run.World!.Combat!;
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.ResumeControlledCardShortlist(
                current.Run.Fork(), index);
        var nextCombat = result.World?.Combat;
        if (result.Phase != RunPhase.Combat || nextCombat is null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_SHORTLIST " +
                "site=combat.card_selection.random_shortlist " +
                "operation=unsupported_post_shortlist_phase");
        if (nextCombat.PendingChoice?.ControlledCardShortlist is not null)
            ValidateCardShortlistBoundary(result);
        else if (nextCombat.PendingChoice?.ChoiceId == "select_cards")
            SemanticCardSelectionEngineV111.ValidatePendingBoundary(result);
        else
            ValidateSemanticCombatBoundary(result);
        if (!priorCombat.DrawPile.SequenceEqual(nextCombat.DrawPile) ||
            CanonicalJson.Sha256(priorCombat.SemanticDeck) !=
                CanonicalJson.Sha256(nextCombat.SemanticDeck))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK " +
                "site=combat.card_selection.random_shortlist " +
                "operation=unmodeled_draw_pile_mutation");
        var next = current with
        {
            Run = result,
            History = current.History.Append(
                new SemanticPilotTransitionV111(
                    "semantic_card_shortlist",
                    CardShortlistDecision: decision.Fork()))
                .ToArray()
        };
        Validate(next);
        return next;
    }

    public static SemanticPilotCombatV111 ResolvePoolShuffle(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, int swapIndex)
    {
        Validate(current);
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic pool-shuffle fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = current.PoolShufflePending ??
            throw new InvalidOperationException(
                "No generated-card pool shuffle is pending.");
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Pool-shuffle event ID is stale or mismatched.");
        var decision = new SemanticPoolShuffleDecisionV111(
            pending, swapIndex);
        decision.Validate();

        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.ResumeControlledPoolShuffle(
                current.Run.Fork(), swapIndex);
        if (result.Phase != RunPhase.Combat)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_POOL site=combat.generation.full_pool_shuffle " +
                "operation=unsupported_post_shuffle_phase");
        var resultCombat = result.World?.Combat ??
            throw new InvalidDataException(
                "Full-pool shuffle continuation lost combat.");
        if (resultCombat.PendingChoice?.ControlledPoolShuffle is not null)
            ValidatePoolShuffleBoundary(result);
        else if (resultCombat.PendingChoice?.ChoiceId == "select_cards")
        {
            SemanticCardSelectionEngineV111.ValidatePendingBoundary(result);
            _ = SemanticCardSelectionEngineV111.DescribePending(
                result, CanonicalJson.Sha256(result));
        }
        else
            ValidateSemanticCombatBoundary(result);
        var priorCombat = current.Run.World!.Combat!;
        if (!priorCombat.DrawPile.SequenceEqual(resultCombat.DrawPile) ||
            CanonicalJson.Sha256(priorCombat.SemanticDeck) !=
                CanonicalJson.Sha256(resultCombat.SemanticDeck))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.generation.full_pool_shuffle " +
                "operation=unmodeled_draw_pile_mutation");
        var next = current with
        {
            Run = result,
            History = current.History.Append(
                new SemanticPilotTransitionV111(
                    "semantic_pool_shuffle",
                    PoolShuffleDecision: decision.Fork())).ToArray()
        };
        Validate(next);
        return next;
    }

    public static SemanticPilotCombatV111 ResolveDrawInsertion(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, int position)
    {
        Validate(current);
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic draw-insertion fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = current.DrawInsertionPending ??
            throw new InvalidOperationException(
                "No generated draw-pile insertion is pending.");
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Draw-insertion event ID is stale or mismatched.");
        var decision = new SemanticDrawInsertionDecisionV111(pending, position);
        decision.Validate();

        var priorCombat = current.Run.World!.Combat!;
        var insertedInstanceId = priorCombat.NextCardInstanceId;
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.ResumeControlledDrawInsertion(
                current.Run.Fork(), position);
        var resultCombat = result.World?.Combat;
        var expectedPile = priorCombat.DrawPile.ToList();
        expectedPile.Insert(position, insertedInstanceId);
        if (result.Phase != RunPhase.Combat || resultCombat is not
                { IsPlayerTurn: true, AutomaticPipelineContinuation: null,
                    PendingChoice: null } ||
            resultCombat.NextCardInstanceId != insertedInstanceId + 1 ||
            !resultCombat.DrawPile.SequenceEqual(expectedPile) ||
            resultCombat.Cards.SingleOrDefault(card =>
                card.InstanceId == insertedInstanceId)?.CardId !=
                pending.GeneratedCardModelId)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw_pile.random_insert_index " +
                "operation=unmodeled_post_insertion_continuation");

        // Source positions index the physical bottom-first DrawPile array;
        // semantic slots are top-first. Convert the source index while
        // preserving the exact selected insertion point.
        var topOffset = priorCombat.DrawPile.Length - position;
        var deck = SemanticDeckEngineV111.InsertKnown(
            priorCombat.SemanticDeck!, topOffset, insertedInstanceId,
            publiclyKnown: false);
        result = result with
        {
            World = result.World! with
            {
                Combat = resultCombat with { SemanticDeck = deck }
            }
        };
        ValidateSemanticCombatBoundary(result);
        var next = current with
        {
            Run = result,
            History = current.History.Append(
                new SemanticPilotTransitionV111(
                    "semantic_draw_insertion",
                    DrawInsertionDecision: decision.Fork())).ToArray()
        };
        Validate(next);
        return next;
    }

    private static SemanticPilotCombatV111 Resolve(
        SemanticPilotCombatV111 current, string fingerprint, int selectedIndex,
        SemanticIntentDecisionV111? semanticIntent,
        SemanticDrawDecisionV111? expectedDeckDecision,
        bool recordSemanticDraw,
        SemanticCostDecisionV111? expectedCostDecision,
        SemanticRandomPileCardDecisionV111? expectedRandomPileCardDecision = null)
    {
        Validate(current);
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException("Stale semantic Nature fingerprint.");
        if (semanticIntent is not null)
            ValidateSemanticIntentDecision(current, semanticIntent, selectedIndex);
        var drawEvent = current.DrawPending;
        var randomPileCardEvent = current.RandomPileCardPending;
        if (expectedDeckDecision is not null)
            ValidateDeckDecision(drawEvent, expectedDeckDecision);
        if (expectedRandomPileCardDecision is not null &&
            (randomPileCardEvent is null ||
             CanonicalJson.Sha256(expectedRandomPileCardDecision.Event) !=
                CanonicalJson.Sha256(randomPileCardEvent) ||
             expectedRandomPileCardDecision.SelectedIndex != selectedIndex))
            throw new InvalidOperationException(
                "Random pile-card decision is stale or outside current support.");
        if (current.CardShortlistPending is not null)
            throw new InvalidOperationException(
                "Resolve the pending pile shortlist by card instance ID.");
        var costEvent = current.CostPending;
        if (current.HandCostSelectionPending is not null &&
            expectedCostDecision is null)
            throw new InvalidOperationException(
                "Resolve the pending hand-cost selection event by card instance ID.");
        if (expectedCostDecision is not null)
            ValidateCostDecision(costEvent, expectedCostDecision, selectedIndex);
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
                History = current.History.Append(NatureTransition(
                    selectedIndex, semanticIntent, null)).ToArray()
            };
            Validate(initialState);
            return initialState;
        }
        if (current.Run.World?.Combat?.PendingChoice?
                .ControlledSemanticDraw is not null)
        {
            if (drawEvent is null)
                throw new InvalidDataException(
                    "Card-effect draw continuation lost its Nature event.");
            if (expectedDeckDecision is not null)
                ValidateDeckDecision(drawEvent, expectedDeckDecision);
            if (selectedIndex < 0 ||
                selectedIndex >= drawEvent.CandidateCardInstanceIds.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            var effectDrawDecision = new SemanticDrawDecisionV111(
                drawEvent, selectedIndex,
                drawEvent.CandidateCardInstanceIds[selectedIndex]);
            effectDrawDecision.Validate();
            if (expectedDeckDecision is not null &&
                CanonicalJson.Sha256(expectedDeckDecision) !=
                    CanonicalJson.Sha256(effectDrawDecision))
                throw new InvalidOperationException(
                    "Semantic card-effect draw decision is stale or outside support.");
            RunState effectDrawResult;
            using (ControlledChanceGuardV111.Enter())
            using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
                effectDrawResult = PrototypeGameEngine
                    .ResumeControlledSemanticEffectDraw(
                        current.Run.Fork(), selectedIndex);
            if (effectDrawResult.Phase == RunPhase.Combat)
            {
                if (effectDrawResult.World?.Combat?.PendingChoice?
                        .ControlledSemanticDraw is not null)
                    ValidateSemanticActionDrawBoundary(effectDrawResult);
                else if (effectDrawResult.World?.Combat?.PendingChoice?
                    .ChoiceId == "select_cards")
                    SemanticCardSelectionEngineV111
                        .ValidatePendingBoundary(effectDrawResult);
                else
                    ValidateSemanticCombatBoundary(effectDrawResult);
                PrototypeStateInvariants.Validate(effectDrawResult);
                var deck = effectDrawResult.World?.Combat?.SemanticDeck ??
                    throw new InvalidDataException(
                        "Resumed card-effect draw lost its semantic deck.");
                var recorded = deck.Transcript?.SingleOrDefault(choice =>
                    choice.EventId == drawEvent.EventId) ??
                    throw new InvalidDataException(
                        "Card-effect draw did not record the selected identity.");
                if (recorded.SiteId != drawEvent.SiteId ||
                    recorded.Purpose != drawEvent.Purpose ||
                    recorded.TopOffset != drawEvent.TopOffset ||
                    recorded.Ordinal != drawEvent.Ordinal ||
                    recorded.ProbabilityStatus !=
                        drawEvent.ProbabilityStatus ||
                    !recorded.CandidateCardInstanceIds.SequenceEqual(
                        drawEvent.CandidateCardInstanceIds) ||
                    recorded.SelectedIndex != selectedIndex ||
                    recorded.SelectedCardInstanceId !=
                        effectDrawDecision.SelectedCardInstanceId ||
                    recorded.SelectedCardInstanceId !=
                        drawEvent.CandidateCardInstanceIds[selectedIndex])
                    throw new InvalidDataException(
                        "Card-effect draw transcript diverged from its event.");
            }
            else if (effectDrawResult.Phase is not (
                RunPhase.Reward or RunPhase.Terminal))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.card_effect.draw " +
                    "operation=unsupported_post_draw_phase");
            var effectDrawState = current with
            {
                Run = effectDrawResult,
                RewardAudit = effectDrawResult.Phase == RunPhase.Reward
                    ? RecordRewardAudit(current.Run, effectDrawResult)
                    : current.RewardAudit,
                History = current.History.Append(
                    new SemanticPilotTransitionV111(
                        "semantic_draw",
                        DrawDecision: effectDrawDecision.Fork())).ToArray()
            };
            Validate(effectDrawState);
            return effectDrawState;
        }
        var pending = current.PendingEndTurn ??
            throw new InvalidOperationException("No Nature decision is pending.");
        if (pending.IntentPending is null && pending.DrawPending is null &&
            pending.RandomPileCardPending is null && pending.CostPending is null)
            throw new InvalidOperationException(
                "No semantic enemy intent, draw or cost choice is pending.");
        var resolved = SemanticEndTurnEngineV111.Resolve(
            pending, pending.Fingerprint, selectedIndex);
        SemanticDrawDecisionV111? deckDecision = null;
        if (drawEvent is not null)
        {
            var deck = resolved.Run.World?.Combat?.SemanticDeck ??
                throw new InvalidDataException(
                    "Semantic card draw lost its provider-owned deck.");
            var recordedChoice = deck.Transcript?.SingleOrDefault(choice =>
                choice.EventId == drawEvent.EventId) ??
                throw new InvalidDataException(
                    "Semantic card draw did not record its source event.");
            if (recordedChoice.SiteId != drawEvent.SiteId ||
                recordedChoice.Purpose != drawEvent.Purpose ||
                recordedChoice.TopOffset != drawEvent.TopOffset ||
                recordedChoice.Ordinal != drawEvent.Ordinal ||
                recordedChoice.ProbabilityStatus != drawEvent.ProbabilityStatus ||
                !recordedChoice.CandidateCardInstanceIds.SequenceEqual(
                    drawEvent.CandidateCardInstanceIds))
                throw new InvalidDataException(
                    "Semantic card draw transcript diverged from its event.");
            deckDecision = new SemanticDrawDecisionV111(drawEvent,
                recordedChoice.SelectedIndex,
                recordedChoice.SelectedCardInstanceId);
            ValidateDeckDecision(drawEvent, deckDecision);
            if (recordedChoice.SelectedIndex != selectedIndex)
                throw new InvalidDataException(
                    "Semantic card draw selected a different candidate index.");
        }
        if (expectedDeckDecision is not null &&
            CanonicalJson.Sha256(expectedDeckDecision) !=
                CanonicalJson.Sha256(deckDecision))
            throw new InvalidOperationException(
                "Semantic card decision is stale or outside current support.");
        SemanticCostDecisionV111? costDecision = null;
        if (costEvent is not null)
        {
            var transcript = resolved.HandDraw?.CostTranscript ?? [];
            var recorded = transcript.LastOrDefault(decision =>
                CanonicalJson.Sha256(decision.Request) ==
                CanonicalJson.Sha256(costEvent.Request)) ??
                throw new InvalidDataException(
                    "Semantic draw cost did not record its source event.");
            if (recorded.SelectedIndex != selectedIndex)
                throw new InvalidDataException(
                    "Semantic draw cost selected a different bounded value.");
            costDecision = new SemanticCostDecisionV111(
                costEvent, recorded.SelectedIndex,
                costEvent.CandidateCosts[recorded.SelectedIndex]);
            costDecision.Validate();
        }
        if (expectedCostDecision is not null &&
            CanonicalJson.Sha256(expectedCostDecision) !=
                CanonicalJson.Sha256(costDecision))
            throw new InvalidOperationException(
                "Semantic cost decision is stale or outside current support.");
        SemanticRandomPileCardDecisionV111? randomPileCardDecision = null;
        if (randomPileCardEvent is not null)
        {
            var transcript = resolved.HandDraw?.RandomPileCardTranscript ?? [];
            randomPileCardDecision = transcript.LastOrDefault(decision =>
                decision.Event.EventId == randomPileCardEvent.EventId) ??
                throw new InvalidDataException(
                    "Random pile-card selection did not record its event.");
            if (CanonicalJson.Sha256(randomPileCardDecision.Event) !=
                    CanonicalJson.Sha256(randomPileCardEvent) ||
                randomPileCardDecision.SelectedIndex != selectedIndex)
                throw new InvalidDataException(
                    "Random pile-card selection diverged from its support.");
        }
        if (expectedRandomPileCardDecision is not null &&
            CanonicalJson.Sha256(expectedRandomPileCardDecision) !=
                CanonicalJson.Sha256(randomPileCardDecision))
            throw new InvalidOperationException(
                "Semantic random pile-card decision differs from replay.");
        return NormalizeNoncontrolledCosts(current with
        {
            Run = resolved.Run,
            PendingEndTurn = resolved.Complete ? null : resolved,
            History = current.History.Append(NatureTransition(
                selectedIndex, semanticIntent,
                recordSemanticDraw ? deckDecision : null,
                costDecision, randomPileCardDecision)).ToArray()
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
        var semanticEvent = MakeSemanticIntentEvent(current, ticket.Pending,
            support);
        var decision = new SemanticIntentDecisionV111(
            semanticEvent, candidate.IntentId);
        decision.Validate();
        return Resolve(current, fingerprint, candidate.TicketStart,
            decision, null, false, null);
    }

    /// <summary>
    /// Resolve one deferred draw by its stable semantic event and physical
    /// card-instance identity. The source-compatible numeric resolver stays
    /// available, but semantic consumers need not interpret an index.
    /// </summary>
    public static SemanticPilotCombatV111 ResolveDrawCard(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, long cardInstanceId)
    {
        Validate(current);
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic draw fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = current.DrawPending ??
            throw new InvalidOperationException(
                "Only a pending semantic draw supports card-instance resolution.");
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Semantic draw event ID is stale or mismatched.");
        var index = Array.IndexOf(pending.CandidateCardInstanceIds,
            cardInstanceId);
        if (index < 0)
            throw new InvalidOperationException(
                "Card instance is outside the pending semantic draw support.");
        var expected = new SemanticDrawDecisionV111(pending, index,
            cardInstanceId);
        expected.Validate();
        return Resolve(current, fingerprint, index, null, expected, true, null);
    }

    /// <summary>
    /// Stable event identity for the current Nature-facing weighted intent
    /// support. It is bound to the exact pre-choice state and eligible pool.
    /// </summary>
    public static SemanticIntentEventV111 DescribePendingIntentEvent(
        SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var ticket = current.PendingInitialIntents ??
            current.PendingEndTurn?.Intents;
        if (ticket?.Pending is not { } request)
            throw new InvalidOperationException(
                "No semantic intent event is pending.");
        var support = ControlledTurnIntentEngineV111
            .DescribePendingSemanticIntent(ticket) ??
            throw new InvalidDataException("Missing weighted intent support.");
        return MakeSemanticIntentEvent(current, request, support);
    }

    public static SemanticTargetEventV111? DescribePendingTargetEvent(
        SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var combat = current.Run.World?.Combat;
        var choice = combat?.PendingChoice;
        var target = choice?.ControlledTarget;
        if (target is null) return null;
        ValidateTargetBoundary(current.Run);
        var candidates = (int[])target.EligibleEnemyInstanceIds.Clone();
        var siteId = target.Kind == "sly_auto_play"
            ? "combat.sly_autoplay.random_enemy"
            : "combat.effect.random_enemy";
        var ownerId = target.SlyCardInstanceId is { } slyCardId
            ? "card_instance:" + slyCardId
            : choice!.SourceCardInstanceId is { } sourceCardId
                ? "card_instance:" + sourceCardId
                : "effect_operation";
        var ordinal = current.History.Count(transition =>
            transition.Kind == "semantic_target");
        var supportFingerprint = CanonicalJson.Sha256(candidates);
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId = SemanticTargetChoiceV111.SchemaId,
            BoundaryFingerprint = current.Fingerprint,
            SiteId = siteId,
            OwnerId = ownerId,
            Ordinal = ordinal,
            SupportFingerprint = supportFingerprint,
            TargetKind = target.Kind
        });
        var semanticEvent = new SemanticTargetEventV111(
            SemanticTargetChoiceV111.SchemaId, eventId, siteId,
            "run.combat_targets", ownerId, ordinal,
            "choose_enemy_instance", candidates, supportFingerprint,
            TargetKind: target.Kind);
        semanticEvent.Validate();
        return semanticEvent;
    }

    public static SemanticGeneratedCardEventV111?
        DescribePendingGeneratedCardEvent(SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var combat = current.Run.World?.Combat;
        var choice = combat?.PendingChoice;
        var generation = choice?.ControlledGeneration;
        var drawInsertion = choice?.ControlledDrawInsertion;
        var isDrawInsertionModelChoice = generation is null &&
            drawInsertion is { Stage: "card_model" };
        if (generation is null && !isDrawInsertionModelChoice) return null;
        if (isDrawInsertionModelChoice)
        {
            if (current.SeededEntry?.FixtureRandomFreeAttackDrawInsertion != true)
                throw new InvalidDataException(
                    "Random draw-Attack model choice lacks fixture provenance.");
            ValidateDrawInsertionBoundary(current.Run);
        }
        else
            ValidateGeneratedCardBoundary(current.Run);
        var candidates = (string[])(generation?.CandidateCardIds ??
            drawInsertion!.CandidateCardIds).Clone();
        var siteId = isDrawInsertionModelChoice
            ? "combat.random_draw_attack.card_model"
            : "combat.generated_card.select";
        var operation = generation?.Operation.Kind ??
            drawInsertion!.Operation.Kind;
        var ownerId = choice!.SourceCardInstanceId is { } sourceCardId
            ? "card_instance:" + sourceCardId
            : "effect:" + operation;
        var ordinal = current.History.Count(transition =>
            transition.Kind == "semantic_generated_card");
        var supportFingerprint = CanonicalJson.Sha256(candidates);
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId = SemanticGenerationChoiceV111.SchemaId,
            BoundaryFingerprint = current.Fingerprint,
            SiteId = siteId,
            OwnerId = ownerId,
            Ordinal = ordinal,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticGeneratedCardEventV111(
            SemanticGenerationChoiceV111.SchemaId, eventId,
            siteId, "prototype.combat", ownerId,
            ordinal, "choose_card_model", candidates, supportFingerprint);
        semanticEvent.Validate();
        return semanticEvent;
    }

    public static SemanticCardSelectionEventV111?
        DescribePendingCardSelectionEvent(SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.Run.World?.Combat?.PendingChoice?.ChoiceId != "select_cards")
            return null;
        if (current.PendingEndTurn is not null ||
            current.PendingInitialIntents is not null ||
            current.TargetPending is not null ||
            current.GeneratedCardPending is not null ||
            current.UsesNativePolicyBaseline || current.CombatTerminal)
            throw new InvalidDataException(
                "Source card selection conflicts with another semantic boundary.");
        return SemanticCardSelectionEngineV111.DescribePending(
            current.Run, CanonicalJson.Sha256(current.Run));
    }

    public static SemanticCostEventV111? DescribePendingCostEvent(
        SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        RandomIndexRequestV111 request;
        long? targetCardInstanceId = null;
        string? targetCardModelId = null;
        if (current.PendingEndTurn?.CostPending is { } drawCostRequest &&
            current.SeededEntry?.FixtureNatureDrawCosts == true)
            request = drawCostRequest;
        else if (current.Run.World?.Combat?.PendingChoice?
                    .ControlledRandomCost is { } randomCost &&
                randomCost.Operation.Kind == PrototypeCombatEffectKind
                    .RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed)
        {
            ValidateRandomHandCostBoundary(current.Run);
            if (current.SeededEntry?.FixtureRandomizeHandCostsMaxExclusive is
                    { } fixtureMaxExclusive &&
                randomCost.Operation.Amount != fixtureMaxExclusive)
                throw new InvalidDataException(
                    "Random hand-cost operation differs from fixture provenance.");
            var maxExclusive = Math.Max(1, randomCost.Operation.Amount);
            var combat = current.Run.World!.Combat!;
            targetCardInstanceId = randomCost.EligibleCardInstanceIds[
                randomCost.NextCardIndex];
            targetCardModelId = combat.Cards.Single(card =>
                card.InstanceId == targetCardInstanceId).CardId;
            request = new RandomIndexRequestV111(
                "combat.random_hand_card.energy_cost", "prototype.combat",
                "effect:" + randomCost.Operation.Kind +
                    ":hand:" + CanonicalJson.Sha256(
                        randomCost.EligibleCardInstanceIds) +
                    ":index:" + randomCost.NextCardIndex,
                current.History.Count(transition =>
                    transition.Kind == "semantic_cost"), [],
                OperationKind: "bounded_integer",
                BoundedCount: maxExclusive);
        }
        else
            return null;
        request.Validate();
        if (request.SupportCount < 1 || request.OperationKind != "bounded_integer")
            throw new InvalidDataException(
                "Semantic cost event has invalid bounded support.");
        var costs = Enumerable.Range(0, request.SupportCount).ToArray();
        var supportFingerprint = targetCardInstanceId is null
            ? CanonicalJson.Sha256(new { request, costs })
            : CanonicalJson.Sha256(new
            {
                request, costs, targetCardInstanceId, targetCardModelId
            });
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId = SemanticCostEventV111.EventSchemaId,
            BoundaryFingerprint = current.Fingerprint,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticCostEventV111(
            SemanticCostEventV111.EventSchemaId, eventId,
            current.Fingerprint, supportFingerprint,
            request with
            {
                OrderedCandidates = (int[])request.OrderedCandidates.Clone()
            }, costs, TargetCardInstanceId: targetCardInstanceId,
            TargetCardModelId: targetCardModelId);
        semanticEvent.Validate();
        return semanticEvent;
    }

    public static SemanticHandCostSelectionEventV111?
        DescribePendingHandCostSelectionEvent(SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var combat = current.Run.World?.Combat;
        var randomCost = combat?.PendingChoice?.ControlledRandomCost;
        if (randomCost is null ||
            randomCost.Operation.Kind != PrototypeCombatEffectKind
                .SetRandomHandCardEnergyCostUntilTurnEndOrPlayed)
            return null;
        ValidateRandomHandCostBoundary(current.Run);
        if ((current.SeededEntry?.FixtureSetRandomHandCardCost is
                { } fixtureCost &&
             randomCost.Operation.Amount != fixtureCost) ||
            current.PendingEndTurn is not null ||
            current.PendingInitialIntents is not null ||
            current.CostPending is not null || current.UsesNativePolicyBaseline ||
            current.CombatTerminal)
            throw new InvalidDataException(
                "Random hand-card selection conflicts with another boundary.");
        var cardIds = (long[])randomCost.EligibleCardInstanceIds.Clone();
        var modelIds = cardIds.Select(id => combat!.Cards.Single(card =>
            card.InstanceId == id).CardId).ToArray();
        var supportFingerprint = CanonicalJson.Sha256(new
        {
            candidateCardInstanceIds = cardIds,
            candidateCardModelIds = modelIds,
            appliedCost = Math.Max(0, randomCost.Operation.Amount)
        });
        const string siteId = "combat.random_hand_card.select";
        const string streamId = "prototype.combat";
        const string operationKind = "choose_hand_card_instance";
        var ownerId = "effect:" + randomCost.Operation.Kind + ":hand:" +
            CanonicalJson.Sha256(randomCost.EligibleCardInstanceIds);
        var ordinal = current.History.Count(transition =>
            transition.Kind == "semantic_hand_cost_selection");
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId = SemanticHandCostSelectionEventV111.EventSchemaId,
            BoundaryFingerprint = current.Fingerprint,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticHandCostSelectionEventV111(
            SemanticHandCostSelectionEventV111.EventSchemaId,
            eventId, current.Fingerprint, supportFingerprint,
            siteId, streamId, ownerId, ordinal, operationKind,
            cardIds, modelIds, Math.Max(0, randomCost.Operation.Amount));
        semanticEvent.Validate();
        return semanticEvent;
    }

    public static SemanticCardShortlistEventV111?
        DescribePendingCardShortlistEvent(SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var combat = current.Run.World?.Combat;
        var shortlist = combat?.PendingChoice?.ControlledCardShortlist;
        if (shortlist is null) return null;
        ValidateCardShortlistBoundary(current.Run);
        var eligibleIds =
            (long[])shortlist.OriginalCandidateCardInstanceIds.Clone();
        var eligibleModels = eligibleIds.Select(id => combat!.Cards.Single(
            card => card.InstanceId == id).CardId).ToArray();
        var candidateIds =
            (long[])shortlist.RemainingCandidateCardInstanceIds.Clone();
        var candidateModels = candidateIds.Select(id => combat!.Cards.Single(
            card => card.InstanceId == id).CardId).ToArray();
        var selectedIds =
            (long[])shortlist.SelectedCandidateCardInstanceIds.Clone();
        var selection = shortlist.Operation.Selection!;
        var supportFingerprint = CanonicalJson.Sha256(new
        {
            eligibleCardInstanceIds = eligibleIds,
            eligibleCardModelIds = eligibleModels,
            selectedCardInstanceIds = selectedIds,
            candidateCardInstanceIds = candidateIds,
            candidateCardModelIds = candidateModels,
            requiredCount = shortlist.RequiredCount
        });
        const string siteId = "combat.card_selection.random_shortlist";
        const string streamId = "prototype.combat";
        const string operationKind =
            "choose_card_instance_without_replacement";
        var ownerId = "selection:" + selection.SourceZone + ":pool:" +
            CanonicalJson.Sha256(eligibleIds);
        var ordinal = current.History.Count(transition =>
            transition.Kind == "semantic_card_shortlist");
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId = SemanticCardShortlistEventV111.EventSchemaId,
            BoundaryFingerprint = current.Fingerprint,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticCardShortlistEventV111(
            SemanticCardShortlistEventV111.EventSchemaId,
            eventId, current.Fingerprint, supportFingerprint,
            siteId, streamId, ownerId, ordinal, operationKind,
            eligibleIds, eligibleModels, selectedIds, candidateIds,
            candidateModels, shortlist.RequiredCount);
        semanticEvent.Validate();
        return semanticEvent;
    }

    public static SemanticPoolShuffleEventV111?
        DescribePendingPoolShuffleEvent(SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var combat = current.Run.World?.Combat;
        var choice = combat?.PendingChoice;
        var shuffle = choice?.ControlledPoolShuffle;
        if (shuffle is null) return null;
        if (current.SeededEntry?.FixturePoolGenerationPotionId !=
                "proto.potion.colorless" ||
            current.PendingEndTurn is not null ||
            current.PendingInitialIntents is not null ||
            current.TargetPending is not null ||
            current.GeneratedCardPending is not null ||
            current.CardSelectionPending is not null ||
            current.UsesNativePolicyBaseline || current.CombatTerminal)
            throw new InvalidDataException(
                "Full-pool shuffle conflicts with another semantic boundary.");
        ValidatePoolShuffleBoundary(current.Run);
        var original = (string[])shuffle.OriginalCardIds.Clone();
        var currentPool = (string[])shuffle.CurrentCardIds.Clone();
        var sourceCardId = choice!.SourceCardInstanceId;
        var owner = "card:" + (sourceCardId?.ToString() ?? "effect") +
            ":operation:" + shuffle.Operation.Kind +
            ":pool:" + CanonicalJson.Sha256(original);
        var ordinal = current.History.Count(transition =>
            transition.Kind == "semantic_pool_shuffle");
        var streamId = shuffle.Operation.GeneratedChoiceUsesNativeCardGenerationRng &&
            combat!.NativeRng?.RunStreams is not null
                ? "run.combat_card_generation" : "prototype.combat";
        var request = new RandomIndexRequestV111(
            "combat.generation.full_pool_shuffle.swap_index", streamId,
            owner, ordinal, [], OperationKind: "bounded_integer",
            BoundedCount: shuffle.NextSwapIndex + 1);
        request.Validate();
        var supportFingerprint = CanonicalJson.Sha256(new
        {
            request, Original = original, Current = currentPool,
            shuffle.NextSwapIndex, shuffle.SelectedCount,
            Operation = shuffle.Operation.Kind.ToString(), sourceCardId
        });
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId = SemanticPoolShuffleEventV111.EventSchemaId,
            BoundaryFingerprint = current.Fingerprint,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticPoolShuffleEventV111(
            SemanticPoolShuffleEventV111.EventSchemaId, eventId,
            current.Fingerprint, supportFingerprint, request, original,
            currentPool, shuffle.NextSwapIndex, shuffle.SelectedCount,
            shuffle.Operation.Kind.ToString(), sourceCardId);
        semanticEvent.Validate();
        return semanticEvent;
    }

    private static void ValidatePoolShuffleBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var shuffle = choice?.ControlledPoolShuffle;
        if (run.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: null } ||
            choice is not { ChoiceId: "controlled_pool_shuffle",
                OuterChoiceContinuation: not null } ||
            shuffle is null ||
            !PrototypeGameEngine.ValidateControlledPoolShuffle(
                combat, shuffle, choice.SourceCardInstanceId))
            throw new InvalidDataException(
                "Generated-card pool shuffle is not source-reachable.");
        var deck = combat.SemanticDeck ?? throw new InvalidDataException(
            "Generated-card pool shuffle lost the provider-owned deck.");
        SemanticDeckEngineV111.Validate(deck);
        if (deck.Mode != SemanticDeckStateV111.NatureMode ||
            deck.Pending is not null ||
            !SemanticDeckEngineV111.AllCardInstanceIds(deck)
                .Order().SequenceEqual(combat.DrawPile.Order()))
            throw new InvalidDataException(
                "Generated-card pool shuffle changed the hidden draw pile.");
    }

    public static SemanticDrawInsertionEventV111?
        DescribePendingDrawInsertionEvent(SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var combat = current.Run.World?.Combat;
        var choice = combat?.PendingChoice;
        var insertion = choice?.ControlledDrawInsertion;
        if (insertion is null) return null;
        if (insertion.Stage != "position") return null;
        var fixedFixtureMatches =
            insertion.Operation.Kind == PrototypeCombatEffectKind
                .CreateGeneratedCardsAtRandomDrawPositions &&
            current.SeededEntry?.FixtureGeneratedDrawInsertionCardId ==
                insertion.Operation.CardId &&
            current.SeededEntry?.FixtureRandomFreeAttackDrawInsertion != true;
        var randomFixtureMatches =
            insertion.Operation.Kind == PrototypeCombatEffectKind
                .CreateRandomFreeAttacksInDrawPile &&
            current.SeededEntry?.FixtureGeneratedDrawInsertionCardId is null &&
            current.SeededEntry?.FixtureRandomFreeAttackDrawInsertion == true;
        if ((!fixedFixtureMatches && !randomFixtureMatches) ||
            current.PendingEndTurn is not null ||
            current.PendingInitialIntents is not null ||
            current.TargetPending is not null ||
            current.GeneratedCardPending is not null ||
            current.CardSelectionPending is not null ||
            current.PoolShufflePending is not null ||
            current.UsesNativePolicyBaseline || current.CombatTerminal)
            throw new InvalidDataException(
                "Draw insertion conflicts with another semantic boundary.");
        ValidateDrawInsertionBoundary(current.Run);
        var pile = (long[])insertion.EligibleDrawPile.Clone();
        var request = new RandomIndexRequestV111(
            "combat.draw_pile.random_insert_index", "prototype.combat",
            "effect:" + insertion.Operation.Kind +
                ":card:" + insertion.SelectedCardId +
                ":pile:" + CanonicalJson.Sha256(pile),
            current.History.Count(transition =>
                transition.Kind == "semantic_draw_insertion"), [],
            OperationKind: "bounded_integer",
            BoundedCount: pile.Length + 1);
        request.Validate();
        var supportFingerprint = CanonicalJson.Sha256(new
        {
            Request = request,
            GeneratedCardModelId = insertion.SelectedCardId!,
            EligibleDrawPileInstanceIds = pile
        });
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId = SemanticDrawInsertionEventV111.EventSchemaId,
            BoundaryFingerprint = current.Fingerprint,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticDrawInsertionEventV111(
            SemanticDrawInsertionEventV111.EventSchemaId, eventId,
            current.Fingerprint, supportFingerprint, request,
            insertion.SelectedCardId!, pile);
        semanticEvent.Validate();
        return semanticEvent;
    }

    private static void ValidateDrawInsertionBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var insertion = choice?.ControlledDrawInsertion;
        var supportedStage = insertion is
            {
                Stage: "position",
                Operation.Kind:
                    PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions,
                SelectedCardId: not null
            } or
            {
                Stage: "card_model",
                Operation.Kind:
                    PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile,
                SelectedCardId: null
            } or
            {
                Stage: "position",
                Operation.Kind:
                    PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile,
                SelectedCardId: not null
            };
        if (run.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: null } ||
            choice is not { ChoiceId: "controlled_draw_insertion",
                OuterChoiceContinuation: not null } ||
            insertion is null || !supportedStage ||
            !PrototypeGameEngine.ValidateControlledDrawInsertion(
                combat, insertion))
            throw new InvalidDataException(
                "Generated draw-pile insertion is not source-reachable.");
        var deck = combat.SemanticDeck ?? throw new InvalidDataException(
            "Generated draw-pile insertion lost the provider-owned deck.");
        SemanticDeckEngineV111.Validate(deck);
        if (deck.Mode != SemanticDeckStateV111.NatureMode ||
            deck.Pending is not null ||
            !SemanticDeckEngineV111.AllCardInstanceIds(deck)
                .Order().SequenceEqual(combat.DrawPile.Order()))
            throw new InvalidDataException(
                "Generated draw-pile insertion changed the hidden pile early.");
    }

    private static void ValidateRandomHandCostBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var randomCost = choice?.ControlledRandomCost;
        if (run.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: null } ||
            choice is not { ChoiceId: "controlled_random_hand_cost",
                OuterChoiceContinuation: not null } ||
            randomCost is null ||
            randomCost.Operation.Kind is not (
                PrototypeCombatEffectKind
                    .RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed or
                PrototypeCombatEffectKind
                    .SetRandomHandCardEnergyCostUntilTurnEndOrPlayed) ||
            !PrototypeGameEngine.ValidateControlledRandomCost(
                combat, randomCost))
            throw new InvalidDataException(
                "Random hand-cost continuation is not source-reachable.");
        var deck = combat.SemanticDeck ?? throw new InvalidDataException(
            "Random hand-cost continuation lost the provider-owned deck.");
        SemanticDeckEngineV111.Validate(deck);
        if (deck.Mode != SemanticDeckStateV111.NatureMode ||
            deck.Pending is not null ||
            !SemanticDeckEngineV111.AllCardInstanceIds(deck)
                .Order().SequenceEqual(combat.DrawPile.Order()))
            throw new InvalidDataException(
                "Random hand-cost continuation changed the hidden draw pile.");
    }

    private static void ValidateCardShortlistBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var shortlist = choice?.ControlledCardShortlist;
        if (run.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: null,
                PendingKnowledgeDemonChoice: null } ||
            choice is not { ChoiceId: "controlled_card_shortlist",
                OuterChoiceContinuation: not null } ||
            shortlist is null ||
            !PrototypeGameEngine.ValidateControlledCardShortlist(
                combat, shortlist))
            throw new InvalidDataException(
                "Random card shortlist is not source-reachable.");
        var deck = combat.SemanticDeck ?? throw new InvalidDataException(
            "Card shortlist lost the provider-owned semantic deck.");
        SemanticDeckEngineV111.Validate(deck);
        if (deck.Mode != SemanticDeckStateV111.NatureMode ||
            deck.Pending is not null ||
            !SemanticDeckEngineV111.AllCardInstanceIds(deck)
                .Order().SequenceEqual(combat.DrawPile.Order()))
            throw new InvalidDataException(
                "Card shortlist changed the hidden draw-pile state.");
    }

    private static void ValidateCostDecision(
        SemanticCostEventV111? pending,
        SemanticCostDecisionV111 decision, int sourceIndex)
    {
        decision.Validate();
        if (pending is null || sourceIndex != decision.SelectedIndex ||
            CanonicalJson.Sha256(pending) !=
                CanonicalJson.Sha256(decision.Event))
            throw new InvalidOperationException(
                "Semantic cost decision is stale or outside current support.");
    }

    private static void ValidateHandCostSelectionDecision(
        SemanticHandCostSelectionEventV111? pending,
        SemanticHandCostSelectionDecisionV111 decision, int sourceIndex)
    {
        decision.Validate();
        if (pending is null || sourceIndex != decision.SelectedIndex ||
            CanonicalJson.Sha256(pending) !=
                CanonicalJson.Sha256(decision.Event))
            throw new InvalidOperationException(
                "Hand-cost selection is stale or outside current support.");
    }

    private static void ValidateCardShortlistDecision(
        SemanticCardShortlistEventV111? pending,
        SemanticCardShortlistDecisionV111 decision, int sourceIndex)
    {
        decision.Validate();
        if (pending is null || sourceIndex != decision.SelectedIndex ||
            CanonicalJson.Sha256(pending) !=
                CanonicalJson.Sha256(decision.Event))
            throw new InvalidOperationException(
                "Card-shortlist decision is stale or outside current support.");
    }

    private static void ValidateSemanticCombatBoundary(RunState run)
    {
        var choice = run.World?.Combat?.PendingChoice;
        if (choice?.ControlledTarget is not null)
        {
            ValidateTargetBoundary(run);
            return;
        }
        if (choice?.ControlledGeneration is not null)
        {
            ValidateGeneratedCardBoundary(run);
            return;
        }
        if (choice?.ControlledDrawInsertion is not null)
        {
            ValidateDrawInsertionBoundary(run);
            return;
        }
        if (choice?.ControlledRandomCost is not null)
        {
            ValidateRandomHandCostBoundary(run);
            return;
        }
        if (choice?.ControlledCardShortlist is not null)
        {
            ValidateCardShortlistBoundary(run);
            return;
        }
        SemanticPlayerActionEngineV111.ValidateBoundary(run);
    }

    private static void ValidateSemanticActionDrawBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var draw = choice?.ControlledSemanticDraw;
        var deck = combat?.SemanticDeck;
        if (run.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: null,
                PendingKnowledgeDemonChoice: null } ||
            choice is not { ChoiceId: "controlled_semantic_draw",
                ControlledSemanticDraw: not null,
                OuterChoiceContinuation: not null,
                ControlledTarget: null, ControlledGeneration: null,
                ControlledDrawInsertion: null, ControlledPoolShuffle: null,
                ControlledRandomCost: null, ControlledDrawCost: null,
                ControlledCardShortlist: null,
                ControlledRandomPileCard: null } ||
            draw is null || draw.RemainingCount <= 0 || draw.EventDepth < 0 ||
            deck is not { Mode: SemanticDeckStateV111.NatureMode,
                Pending: not null })
            throw new InvalidDataException(
                "Card-effect draw is not at a source-reachable Nature boundary.");
        SemanticDeckEngineV111.Validate(deck);
        if (!SemanticDeckEngineV111.AllCardInstanceIds(deck)
                .Order().SequenceEqual(combat.DrawPile.Order()) ||
            SemanticDeckEngineV111.Inspect(deck) is null)
            throw new InvalidDataException(
                "Card-effect draw diverged from its semantic pile state.");
        PrototypeStateInvariants.Validate(run);
    }

    private static void ValidateGeneratedCardBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var generation = choice?.ControlledGeneration;
        if (run.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: null } ||
            choice is not { ChoiceId: "controlled_generated_card",
                OuterChoiceContinuation: not null } ||
            generation is null ||
            !PrototypeGameEngine.ValidateControlledGenerationSupport(generation))
            throw new InvalidDataException(
                "Semantic generated-card pool is not source-reachable.");
        var deck = combat.SemanticDeck ?? throw new InvalidDataException(
            "Generated-card continuation lost its provider-owned deck.");
        SemanticDeckEngineV111.Validate(deck);
        if (deck.Mode != SemanticDeckStateV111.NatureMode ||
            deck.Pending is not null ||
            !SemanticDeckEngineV111.AllCardInstanceIds(deck)
                .Order().SequenceEqual(combat.DrawPile.Order()))
            throw new InvalidDataException(
                "Generated-card continuation changed the hidden draw pile.");
    }

    private static void ValidateTargetBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var target = choice?.ControlledTarget;
        if (run.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: null } ||
            choice is not { ChoiceId: "controlled_random_target",
                OuterChoiceContinuation: not null } ||
            target is null ||
            target.Kind is not ("effect_random_enemy" or "sly_auto_play"))
            throw new InvalidDataException(
                "Semantic target continuation is not at a supported player action.");
        var eligible = combat.Enemies.Where(enemy => enemy.Hp > 0 &&
                (target.Kind != "sly_auto_play" || !enemy.Escaped))
            .Select(enemy => enemy.InstanceId).ToArray();
        if (eligible.Length == 0 ||
            !eligible.SequenceEqual(target.EligibleEnemyInstanceIds))
            throw new InvalidDataException(
                "Semantic target support disagrees with live enemy eligibility.");
        if (target.Kind == "sly_auto_play" &&
            (target.SlyCardInstanceId is not { } cardId ||
             !combat.DiscardPile.Contains(cardId)))
            throw new InvalidDataException(
                "Sly auto-play target lost its discarded source card.");
        var deck = combat.SemanticDeck ?? throw new InvalidDataException(
            "Semantic target continuation lost its provider-owned deck.");
        SemanticDeckEngineV111.Validate(deck);
        if (deck.Mode != SemanticDeckStateV111.NatureMode ||
            deck.Pending is not null ||
            !SemanticDeckEngineV111.AllCardInstanceIds(deck)
                .Order().SequenceEqual(combat.DrawPile.Order()))
            throw new InvalidDataException(
                "Semantic target continuation changed the hidden draw pile.");
    }

    private static SemanticIntentEventV111 MakeSemanticIntentEvent(
        SemanticPilotCombatV111 current, RandomIndexRequestV111 request,
        SemanticIntentSupportV111 support)
    {
        var detachedSupport = support with
        {
            Candidates = (SemanticIntentCandidateV111[])support.Candidates.Clone()
        };
        var supportFingerprint = CanonicalJson.Sha256(support);
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId = SemanticIntentEventV111.EventSchemaId,
            BoundaryFingerprint = current.Fingerprint,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticIntentEventV111(
            SemanticIntentEventV111.EventSchemaId,
            eventId, current.Fingerprint, supportFingerprint,
            request.SiteId, request.StreamId, request.OwnerId,
            request.Ordinal, detachedSupport);
        semanticEvent.Validate();
        return semanticEvent;
    }

    private static void ValidateSemanticIntentDecision(
        SemanticPilotCombatV111 current,
        SemanticIntentDecisionV111 decision, int sourceTicket)
    {
        decision.Validate();
        var ticket = current.PendingInitialIntents ??
            current.PendingEndTurn?.Intents;
        if (ticket?.Pending is not { } request)
            throw new InvalidOperationException(
                "Semantic intent decision has no matching pending event.");
        var support = ControlledTurnIntentEngineV111
            .DescribePendingSemanticIntent(ticket) ??
            throw new InvalidDataException("Missing weighted intent support.");
        var semanticEvent = MakeSemanticIntentEvent(current, request, support);
        var candidate = support.Candidates.SingleOrDefault(x =>
            x.IntentId == decision.IntentId);
        if (candidate is null || sourceTicket != candidate.TicketStart ||
            CanonicalJson.Sha256(semanticEvent) !=
                CanonicalJson.Sha256(decision.Event))
            throw new InvalidOperationException(
                "Semantic intent event is stale or outside current support.");
        decision.Validate();
    }

    private static void ValidateDeckDecision(
        SemanticDeckEventV111? pending,
        SemanticDrawDecisionV111 decision)
    {
        decision.Validate();
        var recordedEvent = decision.Event;
        if (pending is null ||
            CanonicalJson.Sha256(recordedEvent) !=
                CanonicalJson.Sha256(pending) ||
            !recordedEvent.CandidateCardInstanceIds.SequenceEqual(
                pending.CandidateCardInstanceIds) ||
            decision.SelectedIndex < 0 ||
            decision.SelectedIndex >= pending.CandidateCardInstanceIds.Length ||
            pending.CandidateCardInstanceIds[decision.SelectedIndex] !=
                decision.SelectedCardInstanceId)
            throw new InvalidOperationException(
                "Semantic draw decision is outside its pending event support.");
    }

    private static SemanticPilotTransitionV111 NatureTransition(
        int selectedIndex, SemanticIntentDecisionV111? semanticIntent,
        SemanticDrawDecisionV111? deckDecision,
        SemanticCostDecisionV111? costDecision = null,
        SemanticRandomPileCardDecisionV111? randomPileCardDecision = null) =>
        costDecision is not null
            ? new SemanticPilotTransitionV111("semantic_cost",
                CostDecision: costDecision.Fork())
            : randomPileCardDecision is not null
            ? new SemanticPilotTransitionV111("semantic_random_pile_card",
                RandomPileCardDecision: randomPileCardDecision.Fork())
            : semanticIntent is not null
            ? new SemanticPilotTransitionV111("semantic_intent",
                IntentDecision: semanticIntent)
            : deckDecision is not null
                ? new SemanticPilotTransitionV111("semantic_draw",
                    DrawDecision: deckDecision.Fork())
                : new SemanticPilotTransitionV111("nature",
                    SelectedIndex: selectedIndex);

    /// <summary>
    /// The initial two-site pilot does not optimize drawn-card costs.
    /// Bound and source hook are taken from the actual semantic cost
    /// continuation, but sampling is an explicitly declared engine-owned
    /// deterministic fallback, NOT an audited native RNG distribution.
    /// </summary>
    private static SemanticPilotCombatV111 NormalizeNoncontrolledCosts(
        SemanticPilotCombatV111 current)
    {
        if (current.SeededEntry?.FixtureNatureDrawCosts == true)
            return current;
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
            if (endTurn.Complete ||
                (endTurn.CostPending is not null &&
                 (state.CostPending is null ||
                  state.CostPending.TargetCardInstanceId is not null ||
                  CanonicalJson.Sha256(endTurn.CostPending) !=
                      CanonicalJson.Sha256(state.CostPending.Request))) ||
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
        if (state.Run.World?.Combat?.PendingChoice?
                .ControlledSemanticDraw is not null)
        {
            ValidateSemanticActionDrawBoundary(state.Run);
            if (state.PendingEndTurn is not null ||
                state.PendingInitialIntents is not null ||
                state.IntentPending is not null ||
                state.DrawPending is null ||
                state.RandomPileCardPending is not null ||
                state.CostPending is not null ||
                state.TargetPending is not null ||
                state.GeneratedCardPending is not null ||
                state.CardSelectionPending is not null ||
                state.PoolShufflePending is not null ||
                state.DrawInsertionPending is not null ||
                state.CardShortlistPending is not null ||
                state.HandCostSelectionPending is not null ||
                state.UsesNativePolicyBaseline || state.CombatTerminal)
                throw new InvalidDataException(
                    "Card-effect draw conflicts with another semantic boundary.");
        }
        if (state.RandomPileCardPending is { } randomPileCardPending)
        {
            randomPileCardPending.Validate();
            if (state.PendingEndTurn is null ||
                state.PendingInitialIntents is not null ||
                state.IntentPending is not null || state.DrawPending is not null ||
                state.CostPending is not null || state.TargetPending is not null ||
                state.GeneratedCardPending is not null ||
                state.CardSelectionPending is not null ||
                state.PoolShufflePending is not null ||
                state.DrawInsertionPending is not null ||
                state.CardShortlistPending is not null ||
                state.HandCostSelectionPending is not null ||
                state.UsesNativePolicyBaseline || state.CombatTerminal ||
                state.Run.World?.Combat?.PendingChoice?
                    .ControlledRandomPileCard is null)
                throw new InvalidDataException(
                    "Random pile-card continuation conflicts with another boundary.");
        }
        if (state.TargetPending is { } targetPending)
        {
            targetPending.Validate();
            if (state.PendingEndTurn is not null ||
                state.PendingInitialIntents is not null ||
                state.UsesNativePolicyBaseline || state.CombatTerminal)
                throw new InvalidDataException(
                    "Random-target continuation conflicts with another boundary.");
        }
        if (state.GeneratedCardPending is { } generatedCardPending)
        {
            generatedCardPending.Validate();
            if (state.TargetPending is not null ||
                state.PendingEndTurn is not null ||
                state.PendingInitialIntents is not null ||
                state.UsesNativePolicyBaseline || state.CombatTerminal)
                throw new InvalidDataException(
                    "Generated-card continuation conflicts with another boundary.");
        }
        if (state.CardSelectionPending is { } cardSelectionPending)
        {
            cardSelectionPending.Validate();
            SemanticCardSelectionEngineV111.ValidatePendingBoundary(state.Run);
            if (state.PendingEndTurn is not null ||
                state.PendingInitialIntents is not null ||
                state.TargetPending is not null ||
                state.GeneratedCardPending is not null ||
                state.UsesNativePolicyBaseline || state.CombatTerminal)
                throw new InvalidDataException(
                    "Card-selection continuation conflicts with another boundary.");
        }
        if (state.PoolShufflePending is { } poolShufflePending)
        {
            poolShufflePending.Validate();
            ValidatePoolShuffleBoundary(state.Run);
            if (state.PendingEndTurn is not null ||
                state.PendingInitialIntents is not null ||
                state.IntentPending is not null || state.DrawPending is not null ||
                state.TargetPending is not null ||
                state.GeneratedCardPending is not null ||
                state.CardSelectionPending is not null ||
                state.CostPending is not null ||
                state.UsesNativePolicyBaseline || state.CombatTerminal)
                throw new InvalidDataException(
                    "Full-pool shuffle conflicts with another semantic boundary.");
        }
        if (state.DrawInsertionPending is { } drawInsertionPending)
        {
            drawInsertionPending.Validate();
            ValidateDrawInsertionBoundary(state.Run);
            if (state.PendingEndTurn is not null ||
                state.PendingInitialIntents is not null ||
                state.IntentPending is not null || state.DrawPending is not null ||
                state.TargetPending is not null ||
                state.GeneratedCardPending is not null ||
                state.CardSelectionPending is not null ||
                state.PoolShufflePending is not null ||
                state.CostPending is not null ||
                state.UsesNativePolicyBaseline || state.CombatTerminal)
                throw new InvalidDataException(
                    "Draw insertion conflicts with another semantic boundary.");
        }
        if (state.CardShortlistPending is { } cardShortlistPending)
        {
            cardShortlistPending.Validate();
            ValidateCardShortlistBoundary(state.Run);
            if (state.PendingEndTurn is not null ||
                state.PendingInitialIntents is not null ||
                state.IntentPending is not null || state.DrawPending is not null ||
                state.TargetPending is not null ||
                state.GeneratedCardPending is not null ||
                state.CardSelectionPending is not null ||
                state.CostPending is not null ||
                state.HandCostSelectionPending is not null ||
                state.PoolShufflePending is not null ||
                state.DrawInsertionPending is not null ||
                state.UsesNativePolicyBaseline || state.CombatTerminal)
                throw new InvalidDataException(
                    "Card shortlist conflicts with another semantic boundary.");
        }
        if (state.HandCostSelectionPending is { } handCostSelectionPending)
        {
            handCostSelectionPending.Validate();
            ValidateRandomHandCostBoundary(state.Run);
            if (state.PendingEndTurn is not null ||
                state.PendingInitialIntents is not null ||
                state.IntentPending is not null || state.DrawPending is not null ||
                state.TargetPending is not null ||
                state.GeneratedCardPending is not null ||
                state.CardSelectionPending is not null ||
                state.CostPending is not null ||
                state.PoolShufflePending is not null ||
                state.DrawInsertionPending is not null ||
                state.UsesNativePolicyBaseline || state.CombatTerminal)
                throw new InvalidDataException(
                    "Hand-cost selection conflicts with another semantic boundary.");
        }
        if (state.CostPending is { } costPending)
        {
            costPending.Validate();
            var handCost = costPending.TargetCardInstanceId is not null;
            if (handCost)
                ValidateRandomHandCostBoundary(state.Run);
            if (state.IntentPending is not null || state.DrawPending is not null ||
                state.CardSelectionPending is not null ||
                state.TargetPending is not null ||
                state.GeneratedCardPending is not null ||
                state.PoolShufflePending is not null ||
                state.DrawInsertionPending is not null ||
                (handCost
                    ? state.Run.World?.Combat?.PendingChoice?
                        .ControlledRandomCost is null
                    : state.PendingEndTurn?.CostPending is null))
                throw new InvalidDataException(
                    "Semantic cost continuation conflicts with another boundary.");
        }
        if (state.Run.World?.Combat?.PendingChoice?
                .ControlledRandomCost is not null &&
            state.CostPending?.TargetCardInstanceId is null &&
            state.HandCostSelectionPending is null)
            throw new InvalidDataException(
                "Source random hand-cost continuation has no semantic event.");
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
        if (state.SeededEntry is { } entry)
        {
            var routeCostProviderExpected = entry.SourceRouteEntry &&
                !entry.NativePolicyBaseline;
            var otherChanceExpected = entry.OtherChanceProviderId is not null ||
                routeCostProviderExpected;
            if (entry.Seed != state.OpeningSource.Seed ||
                entry.Act != state.OpeningSource.Origin.Combat.Act ||
                entry.NativeEncounterEntry != state.NativeEncounterEntry ||
                entry.TotalFloor != state.OpeningSource.Origin.Formation.TotalFloor ||
                otherChanceExpected != (state.OtherChance is not null) ||
                (entry.OtherChanceProviderId is not null &&
                 entry.OtherChanceProviderId != state.OtherChance?.ProviderId) ||
                (routeCostProviderExpected && state.OtherChance?.ProviderId !=
                 SemanticOtherChanceEngineV111.ProviderId))
                throw new InvalidDataException(
                    "Seeded Nature entry provenance disagrees with its opening.");
        }
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
                "semantic_intent" when transition.IntentDecision is not null =>
                    ResolveIntent(replay, replay.Fingerprint,
                        transition.IntentDecision.IntentId),
                "semantic_draw" when transition.DrawDecision is not null =>
                    ResolveDrawCard(replay, replay.Fingerprint,
                        transition.DrawDecision.Event.EventId,
                        transition.DrawDecision.SelectedCardInstanceId),
                "semantic_random_pile_card" when
                    transition.RandomPileCardDecision is not null =>
                    ResolveRandomPileCard(replay, replay.Fingerprint,
                        transition.RandomPileCardDecision.Event.EventId,
                        transition.RandomPileCardDecision.SelectedCardInstanceId),
                "semantic_target" when transition.TargetDecision is not null =>
                    ResolveRandomTarget(replay, replay.Fingerprint,
                        transition.TargetDecision.Event.EventId,
                        transition.TargetDecision.SelectedEnemyInstanceId),
                "semantic_generated_card" when
                    transition.GeneratedCardDecision is not null =>
                    ResolveGeneratedCard(replay, replay.Fingerprint,
                        transition.GeneratedCardDecision.Event.EventId,
                        transition.GeneratedCardDecision.SelectedCardId),
                "semantic_card_selection" when
                    transition.CardSelectionDecision is not null =>
                    Step(replay, replay.Fingerprint,
                        transition.CardSelectionDecision.ActionId),
                "semantic_cost" when transition.CostDecision is not null =>
                    ResolveCost(replay, replay.Fingerprint,
                        transition.CostDecision.Event.EventId,
                        transition.CostDecision.SelectedCost),
                "semantic_hand_cost_selection" when
                    transition.HandCostSelectionDecision is not null =>
                    ResolveHandCostSelection(replay, replay.Fingerprint,
                        transition.HandCostSelectionDecision.Event.EventId,
                        transition.HandCostSelectionDecision.SelectedCardInstanceId),
                "semantic_card_shortlist" when
                    transition.CardShortlistDecision is not null =>
                    ResolveCardShortlist(replay, replay.Fingerprint,
                        transition.CardShortlistDecision.Event.EventId,
                        transition.CardShortlistDecision.SelectedCardInstanceId),
                "semantic_pool_shuffle" when
                    transition.PoolShuffleDecision is not null =>
                    ResolvePoolShuffle(replay, replay.Fingerprint,
                        transition.PoolShuffleDecision.Event.EventId,
                        transition.PoolShuffleDecision.SelectedSwapIndex),
                "semantic_draw_insertion" when
                    transition.DrawInsertionDecision is not null =>
                    ResolveDrawInsertion(replay, replay.Fingerprint,
                        transition.DrawInsertionDecision.Event.EventId,
                        transition.DrawInsertionDecision.SelectedPosition),
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
