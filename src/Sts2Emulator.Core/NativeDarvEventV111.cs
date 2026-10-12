namespace Sts2Emulator.Core;

/// <summary>
/// Silent's executable shared Ancient offer. Cross-character cards are not
/// used by this event; the seven choices reuse existing Silent mechanics.
/// </summary>
public static class NativeDarvEventV111
{
    public const string EventId = "proto.native.shared.darv";
    public const string EpochId = "DARV_EPOCH";
    private const string RngEventId = "DARV";

    public static readonly string[] SupportedRelicIds =
    [
        "proto.relic.astrolabe",
        "proto.relic.empty_cage",
        "proto.relic.runic_pyramid",
        "proto.relic.snecko_eye",
        "proto.relic.sozu",
        "proto.relic.philosophers_stone",
        "proto.relic.velvet_choker"
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
            id != "proto.relic.sozu" || act == 2).ToArray();
    }

    private static string RelicName(string id) => id switch
    {
        "proto.relic.astrolabe" => "Astrolabe",
        "proto.relic.empty_cage" => "Empty Cage",
        "proto.relic.runic_pyramid" => "Runic Pyramid",
        "proto.relic.snecko_eye" => "Snecko Eye",
        "proto.relic.sozu" => "Sozu",
        "proto.relic.philosophers_stone" => "Philosopher's Stone",
        "proto.relic.velvet_choker" => "Velvet Choker",
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };

    public static bool IsOpeningEvent(RunWorldState world) =>
        world.Act is 2 or 3 && world.Floor == 0
        && world.Map.CurrentNodeId is null
        && world.Event?.EventId == EventId;

    public static RunState BeginOpening(RunState state)
    {
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
        var relics = SupportedRelicIdsForAct(world.Act);
        NativeRngV111.Shuffle(bundle, bundle.Streams[0].StreamId, relics);
        var offers = relics.Take(3).Select(ChoiceId).ToArray();
        return fork with
        {
            Phase = RunPhase.Event,
            World = world with
            {
                Event = new EventState(EventId,
                    OfferedChoiceIds: offers,
                    NativeEventRng: bundle.Streams[0]),
                EventHistory = world.EventIds.Append(EventId).ToArray()
            }
        };
    }

    public static RunState CreateOpeningFixture(string seed, int act = 2,
        int ascension = 0) => BeginOpening(
        PrototypeNativeLaterActFixture.Create(seed, act, ascension));

    internal static void ValidateEventContext(RunState state)
    {
        if (state.World?.Event is not { EventId: EventId } evt)
            return;
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
        _ = NativeRngV111.ToSnapshot(stream);
        if (evt.ChosenChoiceId is { } selected
            && !offers.Contains(selected, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Darv selected a relic that was not offered.");
    }
}
