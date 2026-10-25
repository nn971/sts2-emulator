namespace Sts2Emulator.Core;

public sealed record NativeTanxEventAuditV111
{
    private RunState? _state;
    private RandomIndexDecisionV111[]? _choices;

    [System.Text.Json.Serialization.JsonConstructor]
    public NativeTanxEventAuditV111(
        RunState State, RandomIndexDecisionV111[] Choices, string ProviderMode)
    {
        _state = State.Fork();
        _choices = CloneDecisions(Choices);
        this.ProviderMode = ProviderMode;
    }

    public RunState State
    {
        get => (_state ?? throw new InvalidDataException(
            "Tanx event audit has no run state.")).Fork();
        init => _state = value.Fork();
    }

    public RandomIndexDecisionV111[] Choices
    {
        get => CloneDecisions(_choices)!;
        init => _choices = CloneDecisions(value);
    }

    public string ProviderMode { get; init; }

    public NativeTanxEventAuditV111 Fork() => this with
    {
        State = _state ?? throw new InvalidDataException(
            "Tanx event audit has no run state."),
        Choices = _choices!
    };

    public void Deconstruct(
        out RunState State, out RandomIndexDecisionV111[] Choices,
        out string ProviderMode)
    {
        State = this.State;
        Choices = this.Choices;
        ProviderMode = this.ProviderMode;
    }

    private static RandomIndexDecisionV111[]? CloneDecisions(
        RandomIndexDecisionV111[]? values) => values?.Select(decision =>
            decision is null ? null! :
                PrototypeControlledEventChoiceContinuationV111
                    .CloneDecision(decision)).ToArray();
}

/// <summary>
/// Source-backed Tanx Ancient offer generation and its named Act 3 entry
/// fixture. The later-act route prepares normal act-local Ancient assignment.
/// Source: STS2-source@706ef1c9fa2219220065849fd5265328607ece20.
/// </summary>
public static class NativeTanxEventV111
{
    public const string EventId = "proto.native.glory.tanx";
    public const string ShuffleSiteId =
        "event.tanx.relic_offer.shuffle_swap_index";
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

    public static PrototypeEventDefinition Definition =>
        PrototypeDefinitionCopiesV111.CloneEventDefinition(
            DefinitionValue);

    private static readonly PrototypeEventDefinition DefinitionValue = new(
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

    internal static bool HasTriBoomerangEligibility(PlayerState player) =>
        player.Deck.Count(card =>
            PrototypeContent.Card(card.CardId).Type == PrototypeCardType.Attack
            && PrototypeGameEngine.CanSelectEventDeckCard(card,
                PrototypePersistentDeckChoiceKind.Enchant, null,
                PrototypeCardEnchantmentKind.Instinct, false)) >= 3;

    internal static string[] CandidateRelics(bool includeTriBoomerang) =>
        includeTriBoomerang
            ? [.. BaseRelicIds, NativeGloryAncientsV111.TriBoomerangId]
            : (string[])BaseRelicIds.Clone();

    internal static string[] CandidateRelics(PlayerState player,
        bool? includeTriBoomerang)
    {
        return CandidateRelics(includeTriBoomerang
            ?? HasTriBoomerangEligibility(player));
    }

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
                "Tanx requires an unvisited Glory Ancient boundary.");
    }

    /// <summary>
    /// Begin Tanx at Glory's Ancient boundary without selecting the run's
    /// assigned Ancient. The event samples three distinct relics using the
    /// native event-local RNG and descending Fisher-Yates shuffle.
    /// </summary>
    public static RunState BeginOpening(RunState state) =>
        BeginOpeningWithAudit(state, new NativeRandomIndexProviderV111()).State;

    /// <summary>
    /// Resolve Tanx's source-ordered relic shuffle through the supplied
    /// provider. Nonnative mode is stored with the completed offers.
    /// </summary>
    public static NativeTanxEventAuditV111 BeginOpeningWithAudit(
        RunState state, IRandomIndexProviderV111 provider)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(provider);
        var providerMode = provider.Mode;
        if (providerMode is not ("native-v111" or "scripted-v111"
            or "controlled-v111"))
            throw new ArgumentException(
                "Tanx offer generation requires a supported random provider mode.",
                nameof(provider));
        ValidateOpeningBoundary(state);
        var world = state.World!;

        var fork = state.Fork();
        var stream = NativeRngV111Contexts.ForEvent(fork.RunSeed, RngEventId);
        var bundle = new RngBundle([stream]);
        var includeTriBoomerang = HasTriBoomerangEligibility(fork.Player);
        var candidates = CandidateRelics(includeTriBoomerang);
        var choices = NativeFisherYatesV111.ShuffleInPlace(
            candidates, bundle, provider, stream.StreamId,
            "event:TANX:act:3:floor:0", ShuffleSiteId);
        var offers = candidates.Take(3).Select(ChoiceId).ToArray();
        var result = fork with
        {
            Phase = RunPhase.Event,
            World = fork.World!.AppendEventId(EventId) with
            {
                Event = new EventState(EventId,
                    OfferedChoiceIds: offers,
                    NativeEventRng: bundle.GetStream(stream.StreamId),
                    NativeTanxTriEligible: includeTriBoomerang,
                    NativeEventRngProviderMode: providerMode == "native-v111"
                        ? null : providerMode)
            }
        };
        return new NativeTanxEventAuditV111(
            result, choices, providerMode);
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
        var eligibleOffers = candidates.Select(ChoiceId).ToArray();
        if (evt.OfferedChoiceIds is not { Length: 3 } offers
            || offers.Distinct(StringComparer.Ordinal).Count() != 3
            || offers.Any(id => !eligibleOffers.Contains(id,
                StringComparer.Ordinal)))
            throw new InvalidOperationException(
                "Tanx offers must be three distinct relics from its source pool.");
        if (evt.ChosenChoiceId is { } chosen
            && !offers.Contains(chosen, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Tanx selected a relic that was not among its three offers.");
        if (evt.Stochastic.ProviderMode is { } mode)
        {
            if (mode is not ("scripted-v111" or "controlled-v111"))
                throw new InvalidOperationException(
                    "Tanx has an unsupported persisted random provider mode.");
            if (NativeRngV111.ToSnapshot(stream)
                != NativeRngV111.ToSnapshot(context))
                throw new InvalidOperationException(
                    "Counterfactual Tanx providers must not advance the native event stream.");
            return;
        }
        _ = NativeFisherYatesV111.ShuffleInPlace(
            candidates,
            bundle,
            new NativeRandomIndexProviderV111(recordToJournal: false),
            context.StreamId,
            "event:TANX:act:3:floor:0",
            ShuffleSiteId);
        var expectedOffers = candidates.Take(3).Select(ChoiceId).ToArray();
        if (!offers.SequenceEqual(expectedOffers, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Tanx offers do not match its native event shuffle.");
        if (NativeRngV111.ToSnapshot(stream)
            != NativeRngV111.ToSnapshot(
                bundle.GetStream(context.StreamId)))
            throw new InvalidOperationException("Tanx event RNG cursor does not match its completed offer shuffle.");
    }
}
