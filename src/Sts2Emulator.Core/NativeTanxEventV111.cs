namespace Sts2Emulator.Core;

/// <summary>
/// Source-backed Tanx Ancient offer generation and its named Act 3 entry
/// fixture. The later-act route prepares normal act-local Ancient assignment.
/// Source: STS2-source@706ef1c9fa2219220065849fd5265328607ece20.
/// </summary>
public static class NativeTanxEventV111
{
    public const string EventId = "proto.native.glory.tanx";
    private const string RngEventId = "TANX";

    private static readonly string[] BaseRelicIds =
    [
        NativeGloryAncientsV111.ClawsId,
        NativeGloryAncientsV111.CrossbowId,
        NativeGloryAncientsV111.IronClubId,
        NativeGloryAncientsV111.MeatCleaverId,
        NativeGloryAncientsV111.SaiId,
        NativeGloryAncientsV111.SpikedGauntletsId,
        NativeGloryAncientsV111.TanxsWhistleId,
        NativeGloryAncientsV111.ThrowingAxeId,
        NativeGloryAncientsV111.WarHammerId
    ];

    public static PrototypeEventDefinition Definition { get; } = new(
        EventId,
        "Tanx",
        BaseRelicIds.Append(NativeGloryAncientsV111.TriBoomerangId)
            .Select(id => new PrototypeEventChoiceDefinition(
                ChoiceId(id), NativeGloryAncientsV111.Relics
                    .Single(relic => relic.Id == id).Name,
                [new(PrototypeRunEffectKind.GainRelic, RelicId: id)]))
            .ToArray(),
        MinAct: 3, MaxAct: 3, Weight: 0);

    private static string ChoiceId(string relicId) => "take_" + relicId;

    private static bool HasTriBoomerangEligibility(PlayerState player) =>
        player.Deck.Count(card =>
            PrototypeContent.Card(card.CardId).Type == PrototypeCardType.Attack
            && PrototypeGameEngine.CanSelectEventDeckCard(card,
                PrototypePersistentDeckChoiceKind.Enchant, null,
                PrototypeCardEnchantmentKind.Instinct, false)) >= 3;

    private static string[] CandidateRelics(bool includeTriBoomerang) =>
        includeTriBoomerang
            ? [.. BaseRelicIds, NativeGloryAncientsV111.TriBoomerangId]
            : (string[])BaseRelicIds.Clone();

    private static string[] CandidateRelics(PlayerState player,
        bool? includeTriBoomerang)
    {
        return CandidateRelics(includeTriBoomerang
            ?? HasTriBoomerangEligibility(player));
    }

    public static bool IsOpeningEvent(RunWorldState world) =>
        world.Act == 3 && world.Floor == 0
        && world.Map.CurrentNodeId is null
        && world.Event?.EventId == EventId;

    /// <summary>
    /// Begin Tanx at Glory's Ancient boundary without selecting the run's
    /// assigned Ancient. The event samples three distinct relics using the
    /// native event-local RNG and descending Fisher-Yates shuffle.
    /// </summary>
    public static RunState BeginOpening(RunState state)
    {
        if (state.World is not { Act: 3, Floor: 0, Combat: null,
                Reward: null, Event: null } world
            || world.Map.CurrentNodeId is not null
            || state.Phase != RunPhase.MapChoice
            || world.EventIds.Contains(EventId, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Tanx requires an unvisited Glory Ancient boundary.");

        var fork = state.Fork();
        var stream = NativeRngV111Contexts.ForEvent(fork.RunSeed, RngEventId);
        var bundle = new RngBundle([stream]);
        var includeTriBoomerang = HasTriBoomerangEligibility(fork.Player);
        var candidates = CandidateRelics(includeTriBoomerang);
        NativeRngV111.Shuffle(bundle, stream.StreamId, candidates);
        var offers = candidates.Take(3).Select(ChoiceId).ToArray();
        return fork with
        {
            Phase = RunPhase.Event,
            World = fork.World! with
            {
                Event = new EventState(EventId,
                    OfferedChoiceIds: offers,
                    NativeEventRng: bundle.Streams[0],
                    NativeTanxTriEligible: includeTriBoomerang),
                EventHistory = world.EventIds.Append(EventId).ToArray()
            }
        };
    }

    public static RunState CreateOpeningFixture(string seed, int ascension = 0) =>
        BeginOpening(PrototypeNativeLaterActFixture.Create(seed, 3, ascension));

    internal static void ValidateEventContext(RunState state)
    {
        if (state.World?.Event is not { EventId: EventId,
                NativeEventRng: { } stream } evt)
            return;
        var context = NativeRngV111Contexts.ForEvent(state.RunSeed, RngEventId);
        if (stream.StreamId != context.StreamId)
            throw new InvalidOperationException("Tanx event RNG stream has the wrong native context.");
        if (evt.NativeTanxTriEligible is not { } includeTriBoomerang)
            throw new InvalidOperationException(
                "Tanx event context is missing its entry-time relic-pool condition.");
        if (evt.ChosenChoiceId is null
            && includeTriBoomerang != HasTriBoomerangEligibility(state.Player))
            throw new InvalidOperationException(
                "Tanx relic-pool condition disagrees with the unmodified Ancient entry deck.");
        var bundle = new RngBundle([context]);
        var candidates = CandidateRelics(state.Player,
            includeTriBoomerang);
        NativeRngV111.Shuffle(bundle, context.StreamId, candidates);
        var expectedOffers = candidates.Take(3).Select(ChoiceId).ToArray();
        if (evt.OfferedChoiceIds is not { Length: 3 } offers
            || !offers.SequenceEqual(expectedOffers, StringComparer.Ordinal))
            throw new InvalidOperationException("Tanx offers do not match its source pool and native event shuffle.");
        if (evt.ChosenChoiceId is { } chosen
            && !offers.Contains(chosen, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Tanx selected a relic that was not among its three offers.");
        if (NativeRngV111.ToSnapshot(stream)
            != NativeRngV111.ToSnapshot(bundle.Streams[0]))
            throw new InvalidOperationException("Tanx event RNG cursor does not match its completed offer shuffle.");
    }
}
