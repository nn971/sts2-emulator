using System.Text.Json;

namespace Sts2Emulator.Core;

public sealed record NativePaelOfferAuditV111(
    RunState State, RandomIndexDecisionV111[] Choices, string ProviderMode)
{
    private RunState? _state = State.Fork();
    private RandomIndexDecisionV111[]? _choices = CloneDecisions(Choices);

    public RunState State
    {
        get => (_state ?? throw new InvalidDataException(
            "Pael offer audit has no run state.")).Fork();
        init => _state = value.Fork();
    }

    public RandomIndexDecisionV111[] Choices
    {
        get => CloneDecisions(_choices)!;
        init => _choices = CloneDecisions(value);
    }

    public NativePaelOfferAuditV111 Fork() => this with
    {
        State = _state ?? throw new InvalidDataException(
            "Pael offer audit has no run state."),
        Choices = _choices!
    };

    private static RandomIndexDecisionV111[]? CloneDecisions(
        RandomIndexDecisionV111[]? values) => values?.Select(decision =>
            decision is null ? null! :
                PrototypeControlledEventChoiceContinuationV111
                    .CloneDecision(decision)).ToArray();
}

/// <summary>Source-backed Pael reward mechanics and the three native offer pools.</summary>
public static class NativePaelRewardsV111
{
    public const string PaelId = "proto.native.ancient.pael";
    public const string OfferSiteId = "event.pael.relic_offer.index_draw";
    public const string LegionSkinSiteId =
        "relic.paels_legion.skin.index_draw";
    public const string HornId = "proto.native.ancient.paels_horn";
    public const string FleshId = "proto.native.ancient.paels_flesh";
    public const string BloodId = "proto.native.ancient.paels_blood";
    public const string TearsId = "proto.native.ancient.paels_tears";
    public const string WingId = "proto.native.ancient.paels_wing";
    public const string ClawId = "proto.native.ancient.paels_claw";
    public const string GrowthId = "proto.native.ancient.paels_growth";
    public const string ToothId = "proto.native.ancient.paels_tooth";
    public const string EyeId = "proto.native.ancient.paels_eye";
    public const string LegionId = "proto.native.ancient.paels_legion";
    public const string LegionPetId = "proto.pet.paels_legion";
    private static readonly string[] LegionSkinValues =
        ["eyes", "horns", "spikes", "wings"];
    public static string[] LegionSkins =>
        (string[])LegionSkinValues.Clone();
    public const string RelaxId = "proto.native.ancient.relax";

    private static readonly PrototypeCardDefinition[] CardDefinitions =
    [
        new(RelaxId, "Relax", 3, PrototypeCardTarget.None,
        [
            new(PrototypeCombatEffectKind.GainPlayerBlock, 16, UpgradeDelta: 2),
            new(PrototypeCombatEffectKind.ApplyPlayerPower, 2, UpgradeDelta: 1,
                PowerId: "proto.power.draw_cards_next_turn"),
            new(PrototypeCombatEffectKind.ApplyPlayerPower, 2, UpgradeDelta: 1,
                PowerId: "proto.power.energy_next_turn")
        ], ExhaustOnUse: true, Rarity: PrototypeCardRarity.Ancient,
            Type: PrototypeCardType.Skill, RewardEligible: false, CanBeGeneratedInCombat: false)
    ];

    public static PrototypeCardDefinition[] Cards => CardDefinitions
        .Select(PrototypeDefinitionCopiesV111.CloneCardDefinition)
        .ToArray();

    private static readonly PrototypeRelicDefinition[] RelicDefinitions =
    [
        new(HornId, "Pael's Horn", RunTriggers:
        [
            new(PrototypeRunEventKind.RelicAcquired,
            [
                new(PrototypeRunEffectKind.AddCard, CardId: RelaxId),
                new(PrototypeRunEffectKind.AddCard, CardId: RelaxId)
            ])
        ]),
        new(FleshId, "Pael's Flesh", EnergyPerTurnBonus: 1, EnergyBonusMinTurn: 3),
        new(BloodId, "Pael's Blood", HandDrawBonus: 1),
        new(WingId, "Pael's Wing"),
        new(ClawId, "Pael's Claw"),
        new(GrowthId, "Pael's Growth", AcquisitionDeckChoice:
            new(PrototypePersistentDeckChoiceKind.Enchant, 1,
                EnchantmentKind: PrototypeCardEnchantmentKind.Clone, EnchantmentAmount: 4)),
        new(EyeId, "Pael's Eye"),
        new(LegionId, "Pael's Legion", AddsCombatPet: true, AddsPet: true),
        new(ToothId, "Pael's Tooth", AcquisitionDeckChoice:
            new(PrototypePersistentDeckChoiceKind.Remove, 5, RequiresUpgradable: true)),
        new(TearsId, "Pael's Tears", Triggers:
            [new(PrototypeCombatEventKind.PlayerSideTurnStarted,
                [new(PrototypeCombatEffectKind.GainEnergy, 2)])])
    ];

    internal static PlayerState EnchantClawDefends(PlayerState player) => player with
    {
        Deck = player.Deck.Select(card => PrototypeGameEngine.CanSelectEventDeckCard(card,
            PrototypePersistentDeckChoiceKind.Enchant, null, PrototypeCardEnchantmentKind.Goopy, false)
                ? card with { Enchantment = new(PrototypeCardEnchantmentKind.Goopy, 1) }
                : card).ToArray()
    };

    public static int RewardsSacrificed(RelicInstance relic)
    {
        if (relic.RelicId != WingId)
            throw new InvalidOperationException("Sacrifice counter requires Pael's Wing.");
        if (relic.PersistentState.ValueKind != JsonValueKind.Object
            || !relic.PersistentState.TryGetProperty("RewardsSacrificed", out var value)
            || !value.TryGetInt32(out var count) || count < 0)
            throw new InvalidOperationException("Invalid Pael's Wing sacrifice counter.");
        return count;
    }

    public static CardInstance[] ToothCards(RelicInstance relic)
    {
        if (relic.RelicId != ToothId)
            throw new InvalidOperationException("Saved-card pool requires Pael's Tooth.");
        if (relic.PersistentState.ValueKind != JsonValueKind.Object
            || !relic.PersistentState.TryGetProperty("Cards", out var cardsElement)
            || cardsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Invalid Pael's Tooth saved-card pool.");

        var cards = cardsElement.Deserialize<CardInstance[]>()
            ?? throw new InvalidOperationException("Invalid Pael's Tooth saved-card pool.");
        if (cards.Any(card => card.InstanceId <= 0
                || card.UpgradeLevel < 0
                || card.UpgradeLevel > PrototypeContent.Card(card.CardId).MaxUpgradeLevel)
            || cards.Select(card => card.InstanceId).Distinct().Count() != cards.Length)
            throw new InvalidOperationException("Invalid Pael's Tooth saved card.");
        return cards.Select(card => card with { PersistentState = card.PersistentState.Clone() }).ToArray();
    }

    internal static PlayerState InitializeTooth(PlayerState player, int index)
    {
        var relics = (RelicInstance[])player.Relics.Clone();
        relics[index] = relics[index] with
        { PersistentState = JsonSerializer.SerializeToElement(new { Cards = Array.Empty<CardInstance>() }) };
        return player with { Relics = relics };
    }

    internal static PlayerState InitializeLegion(PlayerState player, int index, RngBundle rng)
    {
        var relics = (RelicInstance[])player.Relics.Clone();
        if (!rng.TryGetStream("reward", out var source))
            throw new InvalidOperationException(
                "Pael's Legion skin selection requires the reward RNG state.");
        var seed = Convert.ToHexString(source.StateBytes) + ":paels-legion:" + index;
        var skinRng = PrototypeRng.CreateBundle(seed);
        var skin = LegionSkins[LegacyRandomChoiceV111.ChooseIndexRange(
            skinRng,
            LegionSkinSiteId,
            "reward",
            $"relic:paels_legion:instance:{index}",
            LegionSkins.Length).SelectedIndex];
        relics[index] = relics[index] with
        { PersistentState = JsonSerializer.SerializeToElement(new { Skin = skin }) };
        return player with { Relics = relics };
    }

    public static string LegionSkin(RelicInstance relic)
    {
        if (relic.RelicId != LegionId)
            throw new InvalidOperationException("Skin lookup requires Pael's Legion.");
        if (relic.PersistentState.ValueKind != JsonValueKind.Object
            || !relic.PersistentState.TryGetProperty("Skin", out var skinElement)
            || skinElement.ValueKind != JsonValueKind.String
            || skinElement.GetString() is not { } skin
            || !LegionSkins.Contains(skin, StringComparer.Ordinal))
            throw new InvalidOperationException("Invalid Pael's Legion saved skin.");
        return skin;
    }

    internal static PrototypeCombatPetState[] CreateCombatPets(PlayerState player) =>
        player.Relics.Select((relic, index) => (Relic: relic, Index: index))
            .Where(item => !item.Relic.IsMelted
                && PrototypeContent.Relic(item.Relic.RelicId).AddsCombatPet)
            .Select(item => new PrototypeCombatPetState(
                LegionPetId, item.Index, LegionSkin(item.Relic)))
            .ToArray();

    internal static RunState SynchronizeLegionPet(RunState state)
    {
        if (state.World?.Combat is not { } combat)
            return state;
        var pets = CreateCombatPets(state.Player);
        return state with { World = state.World with { Combat = combat with { Pets = pets } } };
    }

    internal static PlayerState RememberToothRemoval(PlayerState player, string? sourceRelicId, CardInstance card)
    {
        if (sourceRelicId != ToothId)
            return player;
        var index = Array.FindLastIndex(player.Relics, relic => relic.RelicId == ToothId && !relic.IsMelted);
        if (index < 0)
            throw new InvalidOperationException("Pael's Tooth removal has no active relic instance.");
        var relics = (RelicInstance[])player.Relics.Clone();
        var savedCards = ToothCards(relics[index]);
        if (savedCards.Any(saved => saved.InstanceId == card.InstanceId))
            throw new InvalidOperationException("Pael's Tooth already contains this card instance.");
        relics[index] = relics[index] with
        {
            PersistentState = JsonSerializer.SerializeToElement(new
            {
                Cards = savedCards.Append(card with { PersistentState = card.PersistentState.Clone() }).ToArray()
            })
        };
        return player with { Relics = relics };
    }

    internal static PlayerState RemoveToothCard(PlayerState player, int relicIndex, int savedIndex)
    {
        var relics = (RelicInstance[])player.Relics.Clone();
        var cards = ToothCards(relics[relicIndex]);
        relics[relicIndex] = relics[relicIndex] with
        {
            PersistentState = JsonSerializer.SerializeToElement(new
            {
                Cards = cards.Where((_, index) => index != savedIndex).ToArray()
            })
        };
        return player with { Relics = relics };
    }

    internal static PlayerState SetRewardsSacrificed(PlayerState player, int index, int count)
    {
        var relics = (RelicInstance[])player.Relics.Clone();
        relics[index] = relics[index] with
        { PersistentState = JsonSerializer.SerializeToElement(new { RewardsSacrificed = count }) };
        return player with { Relics = relics };
    }

    public static PrototypeRelicDefinition[] Relics => RelicDefinitions
        .Select(PrototypeDefinitionCopiesV111.CloneRelicDefinition)
        .ToArray();

    private static readonly PrototypeEventDefinition RewardDefinitionValue =
        new(PaelId, "Pael", RelicDefinitions.Select(relic => new PrototypeEventChoiceDefinition(
            "take_" + relic.Id, relic.Name,
            [new(PrototypeRunEffectKind.GainRelic, RelicId: relic.Id)])).ToArray(),
            MinAct: 2, MaxAct: 2, Weight: 0);

    public static PrototypeEventDefinition RewardDefinition =>
        PrototypeDefinitionCopiesV111.CloneEventDefinition(
            RewardDefinitionValue);

    public static string[][] OfferPools(PlayerState player)
    {
        var first = new[] { FleshId, HornId, TearsId };
        var second = new List<string> { WingId };
        var goopyEligible = player.Deck.Count(card =>
            PrototypeGameEngine.CanSelectEventDeckCard(card,
                PrototypePersistentDeckChoiceKind.Enchant, null,
                PrototypeCardEnchantmentKind.Goopy, false));
        if (goopyEligible >= 3)
            second.Add(ClawId);
        var removable = player.Deck.Count(card =>
            PrototypeGameEngine.CanSelectEventDeckCard(card,
                PrototypePersistentDeckChoiceKind.Remove, null, null, false));
        if (removable >= 5)
            second.Add(ToothId);
        var doubled = second.Concat(second).Append(GrowthId).ToArray();

        var third = new List<string> { EyeId, BloodId };
        if (!HasEventPet(player))
            third.Add(LegionId);
        return [first, doubled, third.ToArray()];
    }

    internal static void ValidateOpeningBoundary(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.World is not { Act: 2, Floor: 0, Combat: null,
                Reward: null, Event: null } world
            || world.Map.CurrentNodeId is not null
            || state.Phase != RunPhase.MapChoice
            || world.EventIds.Contains(PaelId, StringComparer.Ordinal)
            || world.EventIds.Contains(NativeAncientRewardsV111.TezcataraId,
                StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Pael requires an unvisited Hive Ancient boundary.");
    }

    public static bool HasEventPet(PlayerState player) =>
        PrototypeNativeEventPetRules.HasEventPet(player);

    public static RunState BeginPaelOpening(RunState state) =>
        BeginPaelOpeningWithAudit(state, new NativeRandomIndexProviderV111()).State;

    /// <summary>
    /// Resolve Pael's three ordered offer pools through the supplied provider.
    /// Nonnative provider mode is persisted because state validation otherwise
    /// assumes the source-seeded offers and cursor.
    /// </summary>
    public static NativePaelOfferAuditV111 BeginPaelOpeningWithAudit(
        RunState state, IRandomIndexProviderV111 provider)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(provider);
        var providerMode = provider.Mode;
        if (providerMode is not ("native-v111" or "scripted-v111"
            or "controlled-v111"))
            throw new ArgumentException(
                "Pael offer generation requires a supported random provider mode.",
                nameof(provider));
        ValidateOpeningBoundary(state);
        var world = state.World!;

        var fork = state.Fork();
        var stream = NativeRngV111Contexts.ForEvent(fork.RunSeed, "PAEL");
        var bundle = new RngBundle([stream]);
        var pools = OfferPools(fork.Player);
        var decisions = new List<RandomIndexDecisionV111>(pools.Length);
        var offers = pools.Select((pool, ordinal) =>
        {
            var decision = StochasticProviderScopeV111.ResolveIndex(
                provider, new RandomIndexRequestV111(
                    OfferSiteId, stream.StreamId,
                    "event:PAEL:act:2:floor:0", ordinal,
                    Enumerable.Range(0, pool.Length).ToArray()), bundle);
            decision.Validate();
            decisions.Add(decision);
            return "take_" + pool[decision.SelectedIndex];
        }).ToArray();
        var result = fork with
        {
            Phase = RunPhase.Event,
            World = fork.World! with
            {
                Event = new EventState(PaelId, OfferedChoiceIds: offers,
                    NativeEventRng: bundle.GetStream(stream.StreamId),
                    NativeEventRngProviderMode: providerMode == "native-v111"
                        ? null : providerMode),
                EventHistory = world.EventIds.Append(PaelId).ToArray()
            }
        };
        return new NativePaelOfferAuditV111(
            result, decisions.ToArray(), providerMode);
    }

    internal static void ValidateOpeningOfferState(RunState state, EventState evt,
        RngStreamState stream)
    {
        if (evt.EventId != PaelId || stream.StreamId != "context.event.PAEL.player_0")
            throw new InvalidOperationException("Pael event context must retain its three completed offer draws.");
        var pools = OfferPools(state.Player);
        if (evt.OfferedChoiceIds is not { Length: 3 } offers
            || offers.Any(id => !id.StartsWith("take_", StringComparison.Ordinal)))
            throw new InvalidOperationException("Sampled Pael requires one persisted offer from each source pool, in order.");
        if (evt.Stochastic.ProviderMode is { } mode
            && mode is not ("scripted-v111" or "controlled-v111"))
            throw new InvalidOperationException("Pael has an unsupported persisted random provider mode.");
        if (evt.Stochastic.ProviderMode is not null)
        {
            var counterfactualInitial = NativeRngV111Contexts.ForEvent(
                state.RunSeed, "PAEL");
            if (NativeRngV111.ToSnapshot(stream)
                != NativeRngV111.ToSnapshot(counterfactualInitial))
                throw new InvalidOperationException(
                    "Counterfactual Pael offer providers must not advance the native event stream.");
            for (var index = 0; index < pools.Length; index++)
                if (!pools[index].Any(id => "take_" + id == offers[index]))
                    throw new InvalidOperationException(
                        "Counterfactual Pael offer is outside its ordered source pool.");
            return;
        }
        if (NativeRngV111.ToSnapshot(stream).Counter != 3)
            throw new InvalidOperationException("Pael event context must retain its three completed offer draws.");
        var initial = NativeRngV111Contexts.ForEvent(state.RunSeed, "PAEL");
        var expected = new RngBundle([initial]);
        var replayProvider = new NativeRandomIndexProviderV111(
            recordToJournal: false);
        var expectedOffers = pools.Select((pool, ordinal) =>
        {
            var decision = replayProvider.Resolve(
                new RandomIndexRequestV111(
                    OfferSiteId,
                    initial.StreamId,
                    "event:PAEL:act:2:floor:0",
                    ordinal,
                    Enumerable.Range(0, pool.Length).ToArray()),
                expected);
            return "take_" + pool[decision.SelectedIndex];
        }).ToArray();
        if (!offers.SequenceEqual(expectedOffers, StringComparer.Ordinal))
            throw new InvalidOperationException("Pael offers do not match its source pools and event RNG draws.");
        if (NativeRngV111.ToSnapshot(stream) != NativeRngV111.ToSnapshot(
                expected.GetStream(initial.StreamId)))
            throw new InvalidOperationException("Pael event RNG does not match its seed and completed offer draws.");
    }

    public static RunState CreatePaelOpeningFixture(string seed, int ascension = 0) =>
        BeginPaelOpening(PrototypeNativeLaterActFixture.Create(seed, 2, ascension));

    public static RunState CreateRewardFixture(string seed, string relicId, int ascension = 0)
    {
        if (!Relics.Any(relic => relic.Id == relicId))
            throw new NotSupportedException("This Pael reward is not executable yet.");
        var state = PrototypeNativeLaterActFixture.Create(seed, 2, ascension);
        return state with
        {
            Phase = RunPhase.Event,
            World = state.World! with
            {
                Event = new(PaelId, OfferedChoiceIds: ["take_" + relicId]),
                EventHistory = state.World.EventIds.Append(PaelId).ToArray()
            }
        };
    }
}
