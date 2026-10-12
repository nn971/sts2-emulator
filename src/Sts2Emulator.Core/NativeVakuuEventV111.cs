namespace Sts2Emulator.Core;

/// <summary>
/// Source-backed three-pool offer generation for Vakuu.
/// Source: STS2-source@706ef1c9fa2219220065849fd5265328607ece20.
/// </summary>
public static class NativeVakuuEventV111
{
    public const string EventId = "proto.native.glory.vakuu";
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

    public static RunState BeginOpening(RunState state)
    {
        if (state.World is not { Act: 3, Floor: 0, Combat: null,
                Reward: null, Event: null } world
            || world.Map.CurrentNodeId is not null
            || state.Phase != RunPhase.MapChoice
            || world.EventIds.Contains(EventId, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Vakuu requires an unvisited Glory Ancient boundary.");

        var fork = state.Fork();
        var stream = NativeRngV111Contexts.ForEvent(
            fork.RunSeed, RngEventId);
        var bundle = new RngBundle([stream]);
        var pools = RelicPools;
        foreach (var pool in pools)
            NativeRngV111.Shuffle(bundle, stream.StreamId, pool);
        var offers = pools.Select(pool => ChoiceId(pool[0])).ToArray();
        return fork with
        {
            Phase = RunPhase.Event,
            World = fork.World! with
            {
                Event = new EventState(
                    EventId,
                    OfferedChoiceIds: offers,
                    NativeEventRng: bundle.Streams[0]),
                EventHistory = world.EventIds.Append(EventId).ToArray()
            }
        };
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
        var expectedOffers = new List<string>(3);
        foreach (var sourcePool in RelicPools)
        {
            var pool = (string[])sourcePool.Clone();
            NativeRngV111.Shuffle(bundle, initial.StreamId, pool);
            expectedOffers.Add(ChoiceId(pool[0]));
        }

        if (evt.OfferedChoiceIds is not { Length: 3 } offers
            || !offers.SequenceEqual(expectedOffers, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Vakuu must retain one offer from each native relic pool.");
        if (evt.ChosenChoiceId is { } chosen
            && !offers.Contains(chosen, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Vakuu selected a relic outside its three offers.");
        if (NativeRngV111.ToSnapshot(stream)
            != NativeRngV111.ToSnapshot(bundle.Streams[0]))
            throw new InvalidOperationException(
                "Vakuu event RNG cursor does not match its three pool shuffles.");
    }
}
