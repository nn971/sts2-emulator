namespace Sts2Emulator.Core;

/// <summary>
/// Orobas's executable Silent offers. The foreign-character Sea Glass and
/// Prismatic Gem alternative in the first pool is deferred by scope.
/// Offer generation owns an event-local cursor for later RNG replacement.
/// </summary>
public static class NativeOrobasEventV111
{
    public const string EventId = "proto.native.hive.orobas";
    public const string LockedChoiceId = "option_pool_3_locked";

    private static readonly string[][] RelicPools =
    [
        [PrototypeNativeOrobasRelics.ElectricShrympId,
            PrototypeNativeOrobasRelics.GlassEyeId],
        [PrototypeNativeOrobasRelics.AlchemicalCofferId,
            PrototypeNativeOrobasRelics.DriftwoodId,
            PrototypeNativeOrobasRelics.RadiantPearlId,
            PrototypeNativeOrobasRelics.SandCastleId],
        [PrototypeNativeOrobasRelics.TouchOfOrobasId,
            PrototypeNativeOrobasRelics.ArchaicToothId]
    ];

    public static PrototypeEventDefinition Definition { get; } = new(
        EventId, "Orobas",
        RelicPools.SelectMany(pool => pool).Select(id =>
            new PrototypeEventChoiceDefinition(ChoiceId(id),
                PrototypeNativeOrobasRelics.Relics.Single(r => r.Id == id).Name,
                [new(PrototypeRunEffectKind.GainRelic, RelicId: id)]))
            .Append(new(LockedChoiceId, "No eligible starter replacement", []))
            .ToArray(),
        MinAct: 2, MaxAct: 2, Weight: 0);

    private static string ChoiceId(string id) => "take_" + id;

    internal static bool IsChoiceEligible(PlayerState player, string choiceId) =>
        choiceId != LockedChoiceId
        && (choiceId != ChoiceId(PrototypeNativeOrobasRelics.TouchOfOrobasId)
            || PrototypeNativeOrobasRelics.IsTouchOfOrobasEligible(player))
        && (choiceId != ChoiceId(PrototypeNativeOrobasRelics.ArchaicToothId)
            || PrototypeNativeOrobasRelics.IsArchaicToothEligible(player));

    public static bool IsOpeningEvent(RunWorldState world) =>
        world.Act == 2 && world.Floor == 0 && world.Map.CurrentNodeId is null
        && world.Event?.EventId == EventId;

    public static RunState BeginOpening(RunState state)
    {
        if (state.World is not { Act: 2, Floor: 0, Combat: null,
                Reward: null, Event: null } world
            || world.Map.CurrentNodeId is not null
            || state.Phase != RunPhase.MapChoice
            || world.EventIds.Contains(EventId, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Orobas requires an unvisited Hive Ancient boundary.");

        var fork = state.Fork();
        var context = NativeRngV111Contexts.ForEvent(fork.RunSeed, "OROBAS");
        var rng = new RngBundle([context]);
        var offers = RelicPools.Select(pool =>
        {
            var eligible = pool.Select(ChoiceId)
                .Where(id => IsChoiceEligible(fork.Player, id)).ToArray();
            if (eligible.Length == 0)
                eligible = [LockedChoiceId];
            return NativeRngV111.NextItem(rng, context.StreamId,
                (IReadOnlyList<string>)eligible)
                ?? throw new InvalidOperationException("Orobas offer pool is empty.");
        }).ToArray();
        return fork with
        {
            Phase = RunPhase.Event,
            World = fork.World! with
            {
                Event = new EventState(EventId, OfferedChoiceIds: offers,
                    NativeEventRng: rng.Streams[0]),
                EventHistory = world.EventIds.Append(EventId).ToArray()
            }
        };
    }

    public static RunState CreateOpeningFixture(string seed, int ascension = 0) =>
        BeginOpening(PrototypeNativeLaterActFixture.Create(seed, 2, ascension));

    internal static void ValidateEventContext(RunState state)
    {
        if (state.World?.Event is not { EventId: EventId } evt)
            return;
        if (evt.NativeEventRng is not { } stream
            || stream.StreamId != "context.event.OROBAS.player_0"
            || evt.OfferedChoiceIds is not { Length: 3 } offers)
            throw new InvalidOperationException(
                "Orobas requires three persisted offers and its event RNG context.");
        _ = NativeRngV111.ToSnapshot(stream);
        for (var index = 0; index < offers.Length; index++)
        {
            if (!(index == 2 && offers[index] == LockedChoiceId)
                && !RelicPools[index].Select(ChoiceId).Contains(offers[index],
                    StringComparer.Ordinal))
                throw new InvalidOperationException(
                    "Orobas offer does not belong to its relic pool.");
            if (evt.ChosenChoiceId is null
                && offers[index] != LockedChoiceId
                && !IsChoiceEligible(state.Player, offers[index]))
                throw new InvalidOperationException(
                    "Orobas offered an ineligible starter replacement.");
        }
        if (evt.ChosenChoiceId is { } chosen
            && (chosen == LockedChoiceId
                || !offers.Contains(chosen, StringComparer.Ordinal)))
            throw new InvalidOperationException(
                "Orobas selected a locked or unoffered choice.");
    }
}
