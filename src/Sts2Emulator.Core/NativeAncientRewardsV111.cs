using System.Text.Json;

namespace Sts2Emulator.Core;

public sealed record NativeTezcataraOfferAuditV111(
    RunState State, RandomIndexDecisionV111[] Choices, string ProviderMode)
{
    private RunState? _state = State.Fork();
    private RandomIndexDecisionV111[]? _choices = CloneDecisions(Choices);

    public RunState State
    {
        get => (_state ?? throw new InvalidDataException(
            "Tezcatara offer audit has no run state.")).Fork();
        init => _state = value.Fork();
    }

    public RandomIndexDecisionV111[] Choices
    {
        get => CloneDecisions(_choices)!;
        init => _choices = CloneDecisions(value);
    }

    public NativeTezcataraOfferAuditV111 Fork() => this with
    {
        State = _state ?? throw new InvalidDataException(
            "Tezcatara offer audit has no run state."),
        Choices = _choices!
    };

    private static RandomIndexDecisionV111[]? CloneDecisions(
        RandomIndexDecisionV111[]? values) => values?.Select(decision =>
            decision is null ? null! :
                PrototypeControlledEventChoiceContinuationV111
                    .CloneDecision(decision)).ToArray();
}

/// <summary>
/// Source-backed Tezcatara reward dependencies. Named reward fixtures reuse
/// ordinary event acquisition and combat continuations. Tezcatara's complete
/// offer pools use the existing native event context codec; normal Ancient
/// assignment is separate. Pin: STS2-source@706ef1c9fa2219220065849fd5265328607ece20.
/// </summary>
public static class NativeAncientRewardsV111
{
    public const string TezcataraId = "proto.native.ancient.tezcatara";
    public const string TezcataraOfferSiteId =
        "event.tezcatara.relic_offer.index_draw";
    public const string VeryHotCocoaId = "proto.native.ancient.very_hot_cocoa";
    public const string YummyCookieId = "proto.native.ancient.yummy_cookie";
    public const string ToastyMittensId = "proto.native.ancient.toasty_mittens";
    public const string SealOfGoldId = "proto.native.ancient.seal_of_gold";
    public const string PumpkinCandleId = "proto.native.ancient.pumpkin_candle";
    public const string StorybookId = "proto.native.ancient.storybook";
    public const string BrightestFlameId = "proto.native.ancient.brightest_flame";
    public const string BiiigHugId = "proto.native.ancient.biiig_hug";
    public const string SootId = "proto.native.ancient.soot";
    public const string NutritiousSoupId = "proto.native.ancient.nutritious_soup";
    public const string GoldenCompassId = "proto.native.ancient.golden_compass";
    public const string ToyBoxId = "proto.native.ancient.toy_box";

    private static readonly PrototypeCardDefinition[] CardDefinitions =
    [
        new(BrightestFlameId, "Brightest Flame", 0, PrototypeCardTarget.None,
        [
            new(PrototypeCombatEffectKind.GainEnergy, 2, UpgradeDelta: 1),
            new(PrototypeCombatEffectKind.DrawCards, 2, UpgradeDelta: 1),
            new(PrototypeCombatEffectKind.LosePlayerMaxHp, 2)
        ], Rarity: PrototypeCardRarity.Ancient, Type: PrototypeCardType.Skill,
            RewardEligible: false, CanBeGeneratedInCombat: false),
        new(SootId, "Soot", -1, PrototypeCardTarget.None, [],
            Rarity: PrototypeCardRarity.Status, Type: PrototypeCardType.Status,
            Unplayable: true, MaxUpgradeLevel: 0,
            RewardEligible: false, CanBeGeneratedInCombat: false)
    ];

    public static PrototypeCardDefinition[] Cards => CardDefinitions
        .Select(PrototypeDefinitionCopiesV111.CloneCardDefinition)
        .ToArray();

    private static PrototypeCombatEffectSpec[] MittensEffects =>
    [
        new(PrototypeCombatEffectKind.ChooseCards, 0,
            Selection: new(PrototypeCardZone.Hand, 1, 1,
                PrototypeCardSelectionResolutionKind.MoveToExhaust)),
        new(PrototypeCombatEffectKind.ApplyPlayerPower, 1,
            PowerId: "proto.power.strength")
    ];

    private static readonly PrototypeRelicDefinition[] RelicDefinitions =
    [
        new(VeryHotCocoaId, "Very Hot Cocoa", Triggers:
        [
            new(PrototypeCombatEventKind.PlayerSideTurnStarted,
                [new(PrototypeCombatEffectKind.GainEnergy, 4)], TurnEquals: 1)
        ]),
        new(YummyCookieId, "Yummy Cookie", AcquisitionDeckChoice:
            new(PrototypePersistentDeckChoiceKind.Upgrade, 4)),
        new(ToastyMittensId, "Toasty Mittens", Triggers:
        [
            new(PrototypeCombatEventKind.OpeningPlayerTurnStarted, MittensEffects),
            new(PrototypeCombatEventKind.PlayerTurnStarted, MittensEffects, MinTurn: 2)
        ]),
        new(SealOfGoldId, "Seal of Gold", Triggers:
        [
            new(PrototypeCombatEventKind.PlayerSideTurnStarted,
                [new(PrototypeCombatEffectKind.GainEnergyForGold, 1, PlayerGoldCost: 3)])
        ]),
        new(PumpkinCandleId, "Pumpkin Candle"),
        new(StorybookId, "Storybook", RunTriggers:
            [new(PrototypeRunEventKind.RelicAcquired,
                [new(PrototypeRunEffectKind.AddCard, CardId: BrightestFlameId)])]),
        new(BiiigHugId, "Biiig Hug", AcquisitionDeckChoice:
            new(PrototypePersistentDeckChoiceKind.Remove, 4),
            AfterShuffleGeneratedCardId: SootId),
        new(NutritiousSoupId, "Nutritious Soup"),
        new(GoldenCompassId, "Golden Compass"),
        new(ToyBoxId, "Toy Box")
    ];

    public static PrototypeRelicDefinition[] Relics => RelicDefinitions
        .Select(PrototypeDefinitionCopiesV111.CloneRelicDefinition)
        .ToArray();

    private static readonly PrototypeEventDefinition
        TezcataraRewardDefinitionValue =
        new(TezcataraId, "Tezcatara",
            RelicDefinitions.Select(relic => new PrototypeEventChoiceDefinition(
                "take_" + relic.Id, relic.Name,
                [new(PrototypeRunEffectKind.GainRelic, RelicId: relic.Id)])).ToArray(),
            MinAct: 2, MaxAct: 2, Weight: 0);

    public static PrototypeEventDefinition TezcataraRewardDefinition =>
        PrototypeDefinitionCopiesV111.CloneEventDefinition(
            TezcataraRewardDefinitionValue);

    public static bool IsOpeningEvent(RunWorldState world) =>
        world.Act == 2 && world.Floor == 0 && world.Map.CurrentNodeId is null
        && world.Event?.EventId is TezcataraId or NativePaelRewardsV111.PaelId
            or NativeOrobasEventV111.EventId
        || NativeDarvEventV111.IsOpeningEvent(world);

    private static string[][] CompleteTezcataraPools() =>
    [
        [VeryHotCocoaId, YummyCookieId, NutritiousSoupId],
        [BiiigHugId, StorybookId, ToastyMittensId],
        [GoldenCompassId, PumpkinCandleId, ToyBoxId, SealOfGoldId]
    ];

    internal static string[][] TezcataraPools(PlayerState player)
    {
        var pools = CompleteTezcataraPools();
        if (!player.Deck.Any(card => PrototypeContent.Card(card.CardId).Rarity == PrototypeCardRarity.Basic
                && PrototypeContent.Card(card.CardId).Tags?.Contains("Strike", StringComparer.Ordinal) == true))
            pools[0] = pools[0][..2];
        return pools;
    }

    internal static void ValidateTezcataraOpeningBoundary(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.World is not { Act: 2, Floor: 0, Combat: null,
                Reward: null, Event: null } world
            || world.Map.CurrentNodeId is not null
            || state.Phase != RunPhase.MapChoice
            || world.EventIds.Contains(TezcataraId, StringComparer.Ordinal)
            || world.EventIds.Contains(NativePaelRewardsV111.PaelId,
                StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Tezcatara requires an unvisited Hive Ancient boundary.");
    }

    internal static void ValidateEventContext(RunState state)
    {
        NativeNeowEventV111.ValidateEventContext(state);
        NativeOrobasEventV111.ValidateEventContext(state);
        NativeDarvEventV111.ValidateEventContext(state);
        if (state.World is { } world
            && (IsOpeningEvent(world)
                || NativeTanxEventV111.IsOpeningEvent(world)
                || NativeVakuuEventV111.IsOpeningEvent(world)
                || NativeNonupeipeEventV111.IsOpeningEvent(world)
                || NativeDarvEventV111.IsOpeningEvent(world))
            && world.Stochastic.LaterActEncounterBag?.AssignedAncientId is
                { } assigned
            && !StringComparer.Ordinal.Equals(
                assigned, world.Event!.EventId))
            throw new InvalidOperationException(
                "Opening Ancient disagrees with the prepared assignment.");

        if (state.World?.Event is { EventId: NativeTanxEventV111.EventId,
                NativeEventRng: null })
            throw new InvalidOperationException(
                "Tanx requires its persisted native event RNG context.");
        if (state.World?.Event is { EventId: NativeVakuuEventV111.EventId,
                NativeEventRng: null })
            throw new InvalidOperationException(
                "Vakuu requires its persisted native event RNG context.");
        if (state.World?.Event is { EventId: NativeNonupeipeEventV111.EventId,
                NativeEventRng: null })
            throw new InvalidOperationException(
                "Nonupeipe requires its persisted native event RNG context.");

        if (state.World?.Event is not { NativeEventRng: { } stream } evt)
            return;
        if (evt.EventId == NativeOrobasEventV111.EventId)
            return;
        if (evt.EventId == NativeDarvEventV111.EventId)
            return;
        if (evt.EventId == PrototypeNativeOvergrowthEvents.NeowEventId)
            return;
        if (evt.EventId == NativePaelRewardsV111.PaelId)
        {
            NativePaelRewardsV111.ValidateOpeningOfferState(state, evt, stream);
            return;
        }
        if (evt.EventId == NativeTanxEventV111.EventId)
        {
            NativeTanxEventV111.ValidateEventContext(state);
            return;
        }
        if (evt.EventId == NativeVakuuEventV111.EventId)
        {
            NativeVakuuEventV111.ValidateEventContext(state);
            return;
        }
        if (evt.EventId == NativeNonupeipeEventV111.EventId)
        {
            NativeNonupeipeEventV111.ValidateEventContext(state);
            return;
        }
        if (evt.EventId != TezcataraId)
            return;
        if (stream.StreamId != "context.event.TEZCATARA.player_0")
            throw new InvalidOperationException(
                "Tezcatara event context has the wrong native RNG stream.");
        var pools = TezcataraPools(state.Player);
        if (evt.OfferedChoiceIds is not { Length: 3 } offers
            || offers.Any(id => !id.StartsWith("take_", StringComparison.Ordinal)))
            throw new InvalidOperationException("Sampled Tezcatara requires one persisted offer from each source pool, in order.");
        if (evt.Stochastic.ProviderMode is { } mode
            && mode is not ("scripted-v111" or "controlled-v111"))
            throw new InvalidOperationException("Tezcatara has an unsupported persisted random provider mode.");
        if (evt.Stochastic.ProviderMode is not null)
        {
            var counterfactualInitial = NativeRngV111Contexts.ForEvent(
                state.RunSeed, "TEZCATARA");
            if (NativeRngV111.ToSnapshot(stream)
                != NativeRngV111.ToSnapshot(counterfactualInitial))
                throw new InvalidOperationException(
                    "Counterfactual Tezcatara offer providers must not advance the native event stream.");
            for (var index = 0; index < pools.Length; index++)
                if (!pools[index].Any(id => "take_" + id == offers[index]))
                    throw new InvalidOperationException(
                        "Counterfactual Tezcatara offer is outside its ordered source pool.");
            return;
        }
        if (NativeRngV111.ToSnapshot(stream).Counter != 3)
            throw new InvalidOperationException(
                "Tezcatara event context must retain its three completed offer draws.");
        var initial = NativeRngV111Contexts.ForEvent(state.RunSeed, "TEZCATARA");
        var expected = new RngBundle([initial]);
        var replayProvider = new NativeRandomIndexProviderV111(
            recordToJournal: false);
        var expectedOffers = pools.Select((pool, ordinal) =>
        {
            var decision = replayProvider.Resolve(
                new RandomIndexRequestV111(
                    TezcataraOfferSiteId,
                    initial.StreamId,
                    "event:TEZCATARA:act:2:floor:0",
                    ordinal,
                    Enumerable.Range(0, pool.Length).ToArray()),
                expected);
            return "take_" + pool[decision.SelectedIndex];
        }).ToArray();
        if (!offers.SequenceEqual(expectedOffers, StringComparer.Ordinal))
            throw new InvalidOperationException("Tezcatara offers do not match its source pools and event RNG draws.");
        if (NativeRngV111.ToSnapshot(stream) != NativeRngV111.ToSnapshot(
                expected.GetStream(initial.StreamId)))
            throw new InvalidOperationException("Tezcatara event RNG does not match its seed and completed offer draws.");
    }

    /// <summary>
    /// Begin specifically assigned Tezcatara at Hive's Ancient boundary.
    /// Uses all three source pools, including Soup based on Basic Strike
    /// presence (not enchantability). Does not select the act's Ancient.
    /// </summary>
    public static RunState BeginTezcataraOpening(RunState state) =>
        BeginTezcataraOpeningWithAudit(
            state, new NativeRandomIndexProviderV111()).State;

    /// <summary>
    /// Resolve Tezcatara's three ordered offer pools through the supplied
    /// provider. Nonnative provider mode is persisted on the completed event.
    /// </summary>
    public static NativeTezcataraOfferAuditV111 BeginTezcataraOpeningWithAudit(
        RunState state, IRandomIndexProviderV111 provider)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(provider);
        var providerMode = provider.Mode;
        if (providerMode is not ("native-v111" or "scripted-v111"
            or "controlled-v111"))
            throw new ArgumentException(
                "Tezcatara offer generation requires a supported random provider mode.",
                nameof(provider));
        ValidateTezcataraOpeningBoundary(state);
        var world = state.World!;
        var fork = state.Fork();
        var pools = TezcataraPools(fork.Player);
        var stream = NativeRngV111Contexts.ForEvent(fork.RunSeed, "TEZCATARA");
        var bundle = new RngBundle([stream]);
        var decisions = new List<RandomIndexDecisionV111>(pools.Length);
        var offers = pools.Select((pool, ordinal) =>
        {
            var decision = StochasticProviderScopeV111.ResolveIndex(
                provider, new RandomIndexRequestV111(
                    TezcataraOfferSiteId, stream.StreamId,
                    "event:TEZCATARA:act:2:floor:0", ordinal,
                    Enumerable.Range(0, pool.Length).ToArray()), bundle);
            decision.Validate();
            decisions.Add(decision);
            return "take_" + pool[decision.SelectedIndex];
        }).ToArray();
        var result = fork with
        {
            Phase = RunPhase.Event,
            World = fork.World!.AppendEventId(TezcataraId) with
            {
                Event = new EventState(TezcataraId, OfferedChoiceIds: offers,
                    NativeEventRng: bundle.GetStream(stream.StreamId),
                    NativeEventRngProviderMode: providerMode == "native-v111"
                        ? null : providerMode)
            }
        };
        return new NativeTezcataraOfferAuditV111(
            result, decisions.ToArray(), providerMode);
    }

    public static RunState CreateTezcataraOpeningFixture(string seed, int ascension = 0) =>
        BeginTezcataraOpening(PrototypeNativeLaterActFixture.Create(seed, 2, ascension));

    internal static PlayerState EnchantSoupStrikes(PlayerState player) => player with
    {
        Deck = player.Deck.Select(card =>
        {
            var definition = PrototypeContent.Card(card.CardId);
            return definition.Rarity == PrototypeCardRarity.Basic
                && definition.Tags?.Contains("Strike", StringComparer.Ordinal) == true
                && PrototypeGameEngine.CanSelectEventDeckCard(card,
                    PrototypePersistentDeckChoiceKind.Enchant, null,
                    PrototypeCardEnchantmentKind.TezcatarasEmber, false)
                ? card with { Enchantment = new(PrototypeCardEnchantmentKind.TezcatarasEmber) }
                : card;
        }).ToArray()
    };

    public static int KindleCount(RelicInstance relic)
    {
        if (relic.RelicId != PumpkinCandleId)
            return 0;
        if (relic.PersistentState.ValueKind != JsonValueKind.Object
            || !relic.PersistentState.TryGetProperty("KindleCount", out var count)
            || !count.TryGetInt32(out var value) || value < 0)
            throw new InvalidOperationException("Pumpkin Candle requires a nonnegative persisted KindleCount.");
        return value;
    }

    internal static PlayerState Rekindle(PlayerState player, int relicIndex, bool acquisition = false)
    {
        var relics = (RelicInstance[])player.Relics.Clone();
        var relic = relics[relicIndex];
        if (relic.RelicId != PumpkinCandleId)
            throw new InvalidOperationException("Kindle requires Pumpkin Candle.");
        var count = acquisition ? 0 : KindleCount(relic);
        relics[relicIndex] = relic with
        {
            PersistentState = JsonSerializer.SerializeToElement(new { KindleCount = checked(count + 5) })
        };
        return player with { Relics = relics };
    }

    internal static PlayerState AfterCombatEnd(PlayerState player)
    {
        if (!player.ActiveRelics.Any(relic => relic.RelicId == PumpkinCandleId))
            return player;
        return player with
        {
            Relics = player.Relics.Select(relic => relic.IsMelted || relic.RelicId != PumpkinCandleId
                ? relic : relic with
                {
                    PersistentState = JsonSerializer.SerializeToElement(
                        new { KindleCount = Math.Max(0, KindleCount(relic) - 1) })
                }).ToArray()
        };
    }

    public static int ToyBoxCombatsSeen(RelicInstance relic)
    {
        if (relic.RelicId != ToyBoxId || relic.PersistentState.ValueKind != JsonValueKind.Object
            || !relic.PersistentState.TryGetProperty("CombatsSeen", out var count)
            || !count.TryGetInt32(out var value) || value is < 0 or > 15)
            throw new InvalidOperationException("Toy Box requires a persisted combat count from zero through fifteen.");
        return value;
    }

    internal static PlayerState InitializeToyBox(PlayerState player, int index)
    {
        var relics = (RelicInstance[])player.Relics.Clone();
        relics[index] = relics[index] with
        { PersistentState = JsonSerializer.SerializeToElement(new { CombatsSeen = 0 }) };
        return player with { Relics = relics };
    }

    internal static RunState ApplyCombatEnd(RunState state)
    {
        var world = state.World ?? throw new InvalidOperationException("Combat exit requires a world.");
        if (world.LastCombatEndDecisionIndex == state.DecisionIndex
            || !state.Player.ActiveRelics.Any(relic => relic.RelicId == PumpkinCandleId
                || relic.RelicId == ToyBoxId && ToyBoxCombatsSeen(relic) < 15
                || relic.RelicId == NativePaelRewardsV111.ToothId
                    && NativePaelRewardsV111.ToothCards(relic).Length > 0))
            return state;
        var player = AfterCombatEnd(state.Player);
        var relics = (RelicInstance[])player.Relics.Clone();
        for (var index = 0; index < relics.Length; index++)
        {
            var relic = relics[index];
            if (relic.IsMelted || relic.RelicId != ToyBoxId || ToyBoxCombatsSeen(relic) >= 15)
                continue;
            var count = ToyBoxCombatsSeen(relic) + 1;
            relics[index] = relic with
            { PersistentState = JsonSerializer.SerializeToElement(new { CombatsSeen = count }) };
            if (count % 3 == 0)
            {
                var waxIndex = Array.FindIndex(relics, candidate => candidate.IsWax && !candidate.IsMelted);
                if (waxIndex >= 0)
                    relics[waxIndex] = relics[waxIndex] with { IsMelted = true };
            }
        }
        var result = state with
        {
            Player = player with { Relics = relics },
            World = world with { LastCombatEndDecisionIndex = state.DecisionIndex }
        };
        return PrototypeGameEngine.ApplyPaelsToothAfterCombat(result);
    }

    /// <summary>
    /// Explicit source reward fixture, with one named reward. Never samples
    /// a truncated pool as though it were native Tezcatara's three offers.
    /// </summary>
    public static RunState CreateTezcataraRewardFixture(
        string seed, string relicId, int ascension = 0)
    {
        if (!Relics.Any(relic => relic.Id == relicId))
            throw new NotSupportedException("This Tezcatara reward is not executable yet.");
        var state = PrototypeNativeLaterActFixture.Create(seed, 2, ascension);
        return state with
        {
            World = state.World!.AppendEventId(TezcataraId) with
            {
                Event = new EventState(TezcataraId,
                    OfferedChoiceIds: ["take_" + relicId])
            },
            Phase = RunPhase.Event
        };
    }
}
