namespace Sts2Emulator.Core;

public sealed record NativeDarvEventAuditV111(
    RunState State, RandomIndexDecisionV111[] Choices, string ProviderMode);

/// <summary>
/// Silent's executable shared Ancient offer. Cross-character cards are not
/// used by this event; its supported choices use implemented Silent mechanics.
/// </summary>
public static class NativeDarvEventV111
{
    public const string EventId = "proto.native.shared.darv";
    public const string EpochId = "DARV_EPOCH";
    public const string CallingBellId = "proto.relic.calling_bell";
    public const string DustyTomeId = "proto.relic.dusty_tome";
    public const string PandorasBoxId = "proto.relic.pandoras_box";
    public const string DustyTomeCardId = "proto.silent.wraith_form";
    public const string CurseOfTheBellCardId = "proto.native.shared.curse_of_the_bell";
    private const string RngEventId = "DARV";
    public const string OfferVariantSiteId =
        "event.darv.offer_variant.index_draw";
    public const string RelicShuffleSiteId =
        "event.darv.relic_offer.shuffle_swap_index";

    public static PrototypeCardDefinition[] Cards =>
    [
        new PrototypeCardDefinition(
            CurseOfTheBellCardId,
            "Curse of the Bell",
            -1,
            PrototypeCardTarget.None,
            [],
            Unplayable: true,
            Eternal: true,
            RewardEligible: false,
            Rarity: PrototypeCardRarity.Curse,
            Type: PrototypeCardType.Curse,
            MaxUpgradeLevel: 0)
    ];

    public static PrototypeRelicDefinition[] Relics =>
    [
        new PrototypeRelicDefinition(CallingBellId, "Calling Bell"),
        new PrototypeRelicDefinition(DustyTomeId, "Dusty Tome"),
        new PrototypeRelicDefinition(PandorasBoxId, "Pandora's Box")
    ];

    public static readonly string[] SupportedRelicIds =
    [
        "proto.relic.astrolabe",
        "proto.relic.black_star",
        CallingBellId,
        PandorasBoxId,
        "proto.relic.empty_cage",
        "proto.relic.runic_pyramid",
        "proto.relic.snecko_eye",
        "proto.relic.ectoplasm",
        "proto.relic.sozu",
        "proto.relic.philosophers_stone",
        "proto.relic.velvet_choker",
        DustyTomeId
    ];

    public static PrototypeEventDefinition Definition { get; } = new(
        EventId,
        "Darv",
        SupportedRelicIds.Select(id => new PrototypeEventChoiceDefinition(
            ChoiceId(id), RelicName(id),
            [new(PrototypeRunEffectKind.GainRelic, RelicId: id)])).ToArray(),
        MinAct: 2, MaxAct: 3, Weight: 0);

    private static string ChoiceId(string relicId) => "take_" + relicId;

    private static string[] SupportedRelicIdsForAct(int act)
    {
        if (act is not (2 or 3))
            throw new ArgumentOutOfRangeException(nameof(act));
        return SupportedRelicIds.Where(id =>
            (id != "proto.relic.sozu"
                && id != "proto.relic.ectoplasm") || act == 2).ToArray();
    }

    private static string[] PoolOneRelicIdsForAct(
        int act, PlayerState player) => SupportedRelicIdsForAct(act)
        .Where(id => id != DustyTomeId
            && (id != PandorasBoxId || HasPandorasBoxTarget(player)))
        .ToArray();

    public static bool IsPandorasBoxTarget(CardInstance card)
    {
        var definition = PrototypeContent.Card(card.CardId);
        return !PrototypeCardRules.IsEternal(card)
            && definition.Tags?.Any(tag => tag is "Strike" or "Defend") == true;
    }

    public static bool HasPandorasBoxTarget(PlayerState player) =>
        player.Deck.Any(IsPandorasBoxTarget);

    private static string RelicName(string id) => id switch
    {
        "proto.relic.astrolabe" => "Astrolabe",
        "proto.relic.black_star" => "Black Star",
        CallingBellId => "Calling Bell",
        DustyTomeId => "Dusty Tome",
        PandorasBoxId => "Pandora's Box",
        "proto.relic.empty_cage" => "Empty Cage",
        "proto.relic.runic_pyramid" => "Runic Pyramid",
        "proto.relic.snecko_eye" => "Snecko Eye",
        "proto.relic.ectoplasm" => "Ectoplasm",
        "proto.relic.sozu" => "Sozu",
        "proto.relic.philosophers_stone" => "Philosopher's Stone",
        "proto.relic.velvet_choker" => "Velvet Choker",
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };

    public static bool IsOpeningEvent(RunWorldState world) =>
        world.Act is 2 or 3 && world.Floor == 0
        && world.Map.CurrentNodeId is null
        && world.Event?.EventId == EventId;

    public static RunState BeginOpening(RunState state) =>
        BeginOpeningWithAudit(state, new NativeRandomIndexProviderV111()).State;

    /// <summary>
    /// Resolve Darv's offer-variant and relic-shuffle choices through the
    /// supplied provider. The compatibility entry point uses native-v111.
    /// </summary>
    public static NativeDarvEventAuditV111 BeginOpeningWithAudit(
        RunState state, IRandomIndexProviderV111 provider)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(provider);
        var providerMode = provider.Mode;
        if (providerMode is not ("native-v111" or "scripted-v111"
            or "controlled-v111"))
            throw new ArgumentException(
                "Darv offer generation requires a supported random provider mode.",
                nameof(provider));
        if (state.World is not { Act: 2 or 3, Floor: 0, Combat: null,
                Reward: null, Event: null } world
            || world.Map.CurrentNodeId is not null
            || state.Phase != RunPhase.MapChoice
            || world.EventIds.Contains(EventId, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Darv requires an unvisited Hive or Glory Ancient boundary.");

        var fork = state.Fork();
        var bundle = new RngBundle([NativeRngV111Contexts.ForEvent(
            fork.RunSeed, RngEventId)]);
        // Source sets do not exclude relics already owned by the player.
        // Sozu is only eligible in Hive; its source set is filtered in Glory.
        var streamId = bundle.Streams[0].StreamId;
        var ownerId = $"event:DARV:act:{world.Act}:floor:0";
        var decisions = new List<RandomIndexDecisionV111>();
        var variant = provider.Resolve(new RandomIndexRequestV111(
            OfferVariantSiteId, streamId, ownerId, 0,
            Array.Empty<int>(), OperationKind: "bounded_integer",
            BoundedCount: 2), bundle);
        variant.Validate();
        decisions.Add(variant);
        var offerDustyTome = variant.SelectedValue == 0;
        var relics = PoolOneRelicIdsForAct(world.Act, state.Player);
        decisions.AddRange(NativeFisherYatesV111.ShuffleInPlace(
            relics, bundle, provider, streamId, ownerId,
            RelicShuffleSiteId));
        var offers = relics.Take(offerDustyTome ? 2 : 3)
            .Select(ChoiceId)
            .Concat(offerDustyTome ? [ChoiceId(DustyTomeId)] : [])
            .ToArray();
        var result = fork with
        {
            Phase = RunPhase.Event,
            World = world with
            {
                Event = new EventState(EventId,
                    OfferedChoiceIds: offers,
                    NativeEventRng: bundle.Streams[0],
                    NativeEventRngProviderMode: providerMode == "native-v111"
                        ? null : providerMode),
                EventHistory = world.EventIds.Append(EventId).ToArray()
            }
        };
        return new NativeDarvEventAuditV111(
            result, decisions.ToArray(), providerMode);
    }

    public static RunState CreateOpeningFixture(string seed, int act = 2,
        int ascension = 0) => BeginOpening(
        PrototypeNativeLaterActFixture.Create(seed, act, ascension));

    internal static void ValidateEventContext(RunState state)
    {
        if (state.World?.Event is not { EventId: EventId } evt)
            return;
        var initial = NativeRngV111Contexts.ForEvent(state.RunSeed, RngEventId);
        if (state.World.Act is not (2 or 3)
            || evt.NativeEventRng is not { } stream
            || stream.StreamId != "context.event.DARV.player_0"
            || evt.OfferedChoiceIds is not { Length: 3 } offers
            || offers.Distinct(StringComparer.Ordinal).Count() != 3
            || offers.Any(id => !SupportedRelicIdsForAct(state.World.Act)
                .Select(ChoiceId)
                .Contains(id, StringComparer.Ordinal)))
            throw new InvalidOperationException(
                "Darv requires three distinct supported relic offers and its event RNG context.");
        var dustyTomeChoice = ChoiceId(DustyTomeId);
        if (offers.Contains(dustyTomeChoice, StringComparer.Ordinal)
            ? !StringComparer.Ordinal.Equals(offers[^1], dustyTomeChoice)
                || offers.Take(2).Any(id => id == dustyTomeChoice)
            : offers.Any(id => id == dustyTomeChoice))
            throw new InvalidOperationException(
                "Darv can offer Dusty Tome only as the third choice beside two pool-one relics.");
        _ = NativeRngV111.ToSnapshot(stream);
        if (evt.ChosenChoiceId is { } selected
            && !offers.Contains(selected, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Darv selected a relic that was not offered.");
        if (evt.NativeEventRngProviderMode is { } mode)
        {
            if (mode is not ("scripted-v111" or "controlled-v111"))
                throw new InvalidOperationException(
                    "Darv has an unsupported persisted random provider mode.");
            if (NativeRngV111.ToSnapshot(stream)
                != NativeRngV111.ToSnapshot(initial))
                throw new InvalidOperationException(
                    "Counterfactual Darv providers must not advance the native event stream.");
        }

        if (evt.ChosenChoiceId is null)
        {
            var poolOne = PoolOneRelicIdsForAct(state.World.Act, state.Player)
                .Select(ChoiceId).ToArray();
            if (offers.Where(id => id != ChoiceId(DustyTomeId))
                .Any(id => !poolOne.Contains(id, StringComparer.Ordinal)))
                throw new InvalidOperationException(
                    "Darv pool-one offer is outside its eligible relic pool.");
        }

        if (evt.NativeEventRngProviderMode is null
            && evt.ChosenChoiceId is null)
        {
            var bundle = new RngBundle([initial]);
            var offerVariant = NativeRngV111.NextInt(
                bundle, initial.StreamId, 2);
            var relics = PoolOneRelicIdsForAct(state.World.Act, state.Player);
            NativeRngV111.Shuffle(bundle, initial.StreamId, relics);
            var expectedOffers = relics.Take(offerVariant == 0 ? 2 : 3)
                .Select(ChoiceId)
                .Concat(offerVariant == 0 ? [ChoiceId(DustyTomeId)] : [])
                .ToArray();
            if (!offers.SequenceEqual(expectedOffers, StringComparer.Ordinal)
                || NativeRngV111.ToSnapshot(stream)
                    != NativeRngV111.ToSnapshot(bundle.Streams[0]))
                throw new InvalidOperationException(
                    "Darv offers or event RNG cursor do not match its native offer draws.");
        }
    }
}
