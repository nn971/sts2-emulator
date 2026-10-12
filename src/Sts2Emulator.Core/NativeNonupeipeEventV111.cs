namespace Sts2Emulator.Core;

/// <summary>
/// Source-backed Nonupeipe offer generation at Glory's Ancient boundary.
/// Normal run-start Ancient assignment remains owned by the regional route.
/// Source: STS2-source@706ef1c9fa2219220065849fd5265328607ece20.
/// </summary>
public static class NativeNonupeipeEventV111
{
    public const string EventId = "proto.native.glory.nonupeipe";
    private const string RngEventId = "NONUPEIPE";

    private static readonly string[] BaseRelicIds =
    [
        NativeGloryAncientsV111.BlessedAntlerId,
        NativeGloryAncientsV111.BrilliantScarfId,
        NativeGloryAncientsV111.DelicateFrondId,
        NativeGloryAncientsV111.DiamondDiademId,
        NativeGloryAncientsV111.FurCoatId,
        NativeGloryAncientsV111.GlitterId,
        NativeGloryAncientsV111.JewelryBoxId,
        NativeGloryAncientsV111.LoomingFruitId,
        NativeGloryAncientsV111.SignetRingId
    ];

    public static PrototypeEventDefinition Definition { get; } = new(
        EventId,
        "Nonupeipe",
        CandidateRelics(includeBeautifulBracelet: true)
            .Select(id => new PrototypeEventChoiceDefinition(
                ChoiceId(id), NativeGloryAncientsV111.Relics
                    .Single(relic => relic.Id == id).Name,
                [new(PrototypeRunEffectKind.GainRelic, RelicId: id)])
            ).ToArray(),
        MinAct: 3, MaxAct: 3, Weight: 0, OncePerRun: true);

    private static string ChoiceId(string relicId) => "take_" + relicId;

    private static string[] CandidateRelics(bool includeBeautifulBracelet) =>
        includeBeautifulBracelet
            ? [.. BaseRelicIds, NativeGloryAncientsV111.BeautifulBraceletId]
            : (string[])BaseRelicIds.Clone();

    private static bool HasBeautifulBraceletEligibility(PlayerState player) =>
        NativeGloryAncientsV111.IsBeautifulBraceletEligible(player);

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
                "Nonupeipe requires an unvisited Glory Ancient boundary.");

        var fork = state.Fork();
        var includeBracelet = HasBeautifulBraceletEligibility(fork.Player);
        var candidates = CandidateRelics(includeBracelet);
        var stream = NativeRngV111Contexts.ForEvent(fork.RunSeed, RngEventId);
        var bundle = new RngBundle([stream]);
        NativeRngV111.Shuffle(bundle, stream.StreamId, candidates);
        return fork with
        {
            Phase = RunPhase.Event,
            World = fork.World! with
            {
                Event = new EventState(EventId,
                    OfferedChoiceIds: candidates.Take(3)
                        .Select(ChoiceId).ToArray(),
                    NativeEventRng: bundle.Streams[0],
                    NativeNonupeipeBraceletEligible: includeBracelet),
                EventHistory = world.EventIds.Append(EventId).ToArray()
            }
        };
    }

    internal static void ValidateEventContext(RunState state)
    {
        if (state.World?.Event is not { EventId: EventId,
                NativeEventRng: { } saved } evt)
            return;
        if (saved.StreamId != "context.event.NONUPEIPE.player_0"
            || evt.NativeNonupeipeBraceletEligible is not { } includeBracelet)
            throw new InvalidOperationException(
                "Nonupeipe requires its persisted native event context and pool condition.");
        if (evt.ChosenChoiceId is null
            && includeBracelet != HasBeautifulBraceletEligibility(state.Player))
            throw new InvalidOperationException(
                "Nonupeipe's Bracelet pool condition disagrees with its entry deck.");

        var initial = NativeRngV111Contexts.ForEvent(state.RunSeed,
            RngEventId);
        var bundle = new RngBundle([initial]);
        var candidates = CandidateRelics(includeBracelet);
        NativeRngV111.Shuffle(bundle, initial.StreamId, candidates);
        var expected = candidates.Take(3).Select(ChoiceId).ToArray();
        if (evt.OfferedChoiceIds is not { Length: 3 } offers
            || !offers.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Nonupeipe offers do not match its source pool and native shuffle.");
        if (evt.ChosenChoiceId is { } chosen
            && !offers.Contains(chosen, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Nonupeipe selected a relic outside its three offers.");
        if (NativeRngV111.ToSnapshot(saved)
            != NativeRngV111.ToSnapshot(bundle.Streams[0]))
            throw new InvalidOperationException(
                "Nonupeipe event RNG cursor does not match the completed offer shuffle.");
    }

    public static RunState CreateOpeningFixture(string seed,
        int ascension = 0) => BeginOpening(
            PrototypeNativeLaterActFixture.Create(seed, 3, ascension));
}
