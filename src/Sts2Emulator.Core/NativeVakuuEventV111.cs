namespace Sts2Emulator.Core;

public sealed record NativeVakuuEventAuditV111(
    RunState State, RandomIndexDecisionV111[] Choices, string ProviderMode);

/// <summary>
/// Source-backed three-pool offer generation for Vakuu.
/// Source: STS2-source@706ef1c9fa2219220065849fd5265328607ece20.
/// </summary>
public static class NativeVakuuEventV111
{
    public const string EventId = "proto.native.glory.vakuu";
    public const string ShuffleSiteId =
        "event.vakuu.relic_offer.shuffle_swap_index";
    private const string RngEventId = "VAKUU";

    private static readonly string[][] SourceRelicPools =
    [
        [
            NativeGloryAncientsV111.BloodSoakedRoseId,
            NativeGloryAncientsV111.WhisperingEarringId,
            NativeGloryAncientsV111.FiddleId
        ],
        [
            NativeGloryAncientsV111.PreservedFogId,
            NativeGloryAncientsV111.SereTalonId,
            NativeGloryAncientsV111.DistinguishedCapeId
        ],
        [
            NativeGloryAncientsV111.ChoicesParadoxId,
            NativeGloryAncientsV111.MusicBoxId,
            NativeGloryAncientsV111.LordsParasolId,
            NativeGloryAncientsV111.JeweledMaskId
        ]
    ];

    public static string[][] RelicPools => SourceRelicPools
        .Select(pool => (string[])pool.Clone())
        .ToArray();

    public static PrototypeEventDefinition Definition { get; } = new(
        EventId,
        "Vakuu",
        RelicPools.SelectMany(pool => pool).Select(id =>
        {
            var relic = NativeGloryAncientsV111.Relics.Single(item =>
                item.Id == id);
            return new PrototypeEventChoiceDefinition(
                ChoiceId(id), relic.Name,
                [new(PrototypeRunEffectKind.GainRelic, RelicId: id)]);
        }).ToArray(),
        MinAct: 3, MaxAct: 3, Weight: 0);

    private static string ChoiceId(string relicId) => "take_" + relicId;

    public static bool IsOpeningEvent(RunWorldState world) =>
        world.Act == 3 && world.Floor == 0
        && world.Map.CurrentNodeId is null
        && world.Event?.EventId == EventId;

    internal static void ValidateOpeningBoundary(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.World is not { Act: 3, Floor: 0, Combat: null,
                Reward: null, Event: null } world
            || world.Map.CurrentNodeId is not null
            || state.Phase != RunPhase.MapChoice
            || world.EventIds.Contains(EventId, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Vakuu requires an unvisited Glory Ancient boundary.");
    }

    public static RunState BeginOpening(RunState state) =>
        BeginOpeningWithAudit(state, new NativeRandomIndexProviderV111()).State;

    /// <summary>
    /// Resolve Vakuu's three source-ordered relic shuffles through the
    /// supplied provider. Nonnative mode is stored with the completed offers.
    /// </summary>
    public static NativeVakuuEventAuditV111 BeginOpeningWithAudit(
        RunState state, IRandomIndexProviderV111 provider)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(provider);
        var providerMode = provider.Mode;
        if (providerMode is not ("native-v111" or "scripted-v111"
            or "controlled-v111"))
            throw new ArgumentException(
                "Vakuu offer generation requires a supported random provider mode.",
                nameof(provider));
        ValidateOpeningBoundary(state);
        var world = state.World!;

        var fork = state.Fork();
        var stream = NativeRngV111Contexts.ForEvent(
            fork.RunSeed, RngEventId);
        var bundle = new RngBundle([stream]);
        var pools = RelicPools;
        var choices = new List<RandomIndexDecisionV111>();
        var ownerId = "event:VAKUU:act:3:floor:0";
        foreach (var pool in pools)
            choices.AddRange(NativeFisherYatesV111.ShuffleInPlace(
                pool, bundle, provider, stream.StreamId, ownerId,
                ShuffleSiteId, choices.Count));
        var offers = pools.Select(pool => ChoiceId(pool[0])).ToArray();
        var result = fork with
        {
            Phase = RunPhase.Event,
            World = fork.World! with
            {
                Event = new EventState(
                    EventId,
                    OfferedChoiceIds: offers,
                    NativeEventRng: bundle.Streams[0],
                    NativeEventRngProviderMode: providerMode == "native-v111"
                        ? null : providerMode),
                EventHistory = world.EventIds.Append(EventId).ToArray()
            }
        };
        return new NativeVakuuEventAuditV111(
            result, choices.ToArray(), providerMode);
    }

    public static RunState CreateOpeningFixture(
        string seed,
        int ascension = 0) =>
        BeginOpening(PrototypeNativeLaterActFixture.Create(
            seed, 3, ascension));

    internal static void ValidateEventContext(RunState state)
    {
        if (state.World?.Event is not { EventId: EventId,
                NativeEventRng: { } stream } evt)
            return;

        var initial = NativeRngV111Contexts.ForEvent(
            state.RunSeed, RngEventId);
        if (stream.StreamId != initial.StreamId)
            throw new InvalidOperationException(
                "Vakuu event RNG stream has the wrong native context.");

        var bundle = new RngBundle([initial]);
        var pools = RelicPools;
        if (evt.OfferedChoiceIds is not { Length: 3 } offers
            || offers.Where((offer, index) =>
                !pools[index].Select(ChoiceId).Contains(offer,
                    StringComparer.Ordinal)).Any())
            throw new InvalidOperationException(
                "Vakuu must retain one offer from each source relic pool.");
        if (evt.ChosenChoiceId is { } selected
            && !offers.Contains(selected, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Vakuu selected a relic outside its three offers.");
        if (evt.NativeEventRngProviderMode is { } mode)
        {
            if (mode is not ("scripted-v111" or "controlled-v111"))
                throw new InvalidOperationException(
                    "Vakuu has an unsupported persisted random provider mode.");
            if (NativeRngV111.ToSnapshot(stream)
                != NativeRngV111.ToSnapshot(initial))
                throw new InvalidOperationException(
                    "Counterfactual Vakuu providers must not advance the native event stream.");
            return;
        }
        var expectedOffers = new List<string>(3);
        var replayProvider = new NativeRandomIndexProviderV111(
            recordToJournal: false);
        var ordinalOffset = 0;
        foreach (var sourcePool in pools)
        {
            var pool = (string[])sourcePool.Clone();
            var choices = NativeFisherYatesV111.ShuffleInPlace(
                pool,
                bundle,
                replayProvider,
                initial.StreamId,
                "event:VAKUU:act:3:floor:0",
                ShuffleSiteId,
                ordinalOffset);
            ordinalOffset += choices.Length;
            expectedOffers.Add(ChoiceId(pool[0]));
        }

        if (!offers.SequenceEqual(expectedOffers, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Vakuu must retain one offer from each native relic pool.");
        if (NativeRngV111.ToSnapshot(stream)
            != NativeRngV111.ToSnapshot(bundle.Streams[0]))
            throw new InvalidOperationException(
                "Vakuu event RNG cursor does not match its three pool shuffles.");
    }
}
