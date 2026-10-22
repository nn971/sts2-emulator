namespace Sts2Emulator.Core;

public sealed record NativeOrobasEventAuditV111
{
    private RunState? _state;
    private RandomIndexDecisionV111[]? _choices;

    [System.Text.Json.Serialization.JsonConstructor]
    public NativeOrobasEventAuditV111(
        RunState State, RandomIndexDecisionV111[] Choices, string ProviderMode)
    {
        _state = State.Fork();
        _choices = CloneDecisions(Choices);
        this.ProviderMode = ProviderMode;
    }

    public RunState State
    {
        get => (_state ?? throw new InvalidDataException(
            "Orobas event audit has no run state.")).Fork();
        init => _state = value.Fork();
    }

    public RandomIndexDecisionV111[] Choices
    {
        get => CloneDecisions(_choices)!;
        init => _choices = CloneDecisions(value);
    }

    public string ProviderMode { get; init; }

    public NativeOrobasEventAuditV111 Fork() => this with
    {
        State = _state ?? throw new InvalidDataException(
            "Orobas event audit has no run state."),
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
/// Orobas's executable Silent offers. The foreign-character Sea Glass and
/// Prismatic Gem alternative in the first pool is deferred by scope.
/// Offer generation owns an event-local cursor for later RNG replacement.
/// </summary>
public static class NativeOrobasEventV111
{
    public const string EventId = "proto.native.hive.orobas";
    public const string LockedChoiceId = "option_pool_3_locked";
    public const string OfferSiteId = "event.orobas.relic_offer.index_draw";

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

    public static PrototypeEventDefinition Definition =>
        PrototypeDefinitionCopiesV111.CloneEventDefinition(
            DefinitionValue);

    private static readonly PrototypeEventDefinition DefinitionValue = new(
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

    internal static string[] OfferCandidateIds(PlayerState player, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (ordinal < 0 || ordinal >= RelicPools.Length)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var eligible = RelicPools[ordinal].Select(ChoiceId)
            .Where(id => IsChoiceEligible(player, id)).ToArray();
        return eligible.Length == 0 ? [LockedChoiceId] : eligible;
    }

    internal static void ValidateOpeningBoundary(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.World is not { Act: 2, Floor: 0, Combat: null,
                Reward: null, Event: null } world
            || world.Map.CurrentNodeId is not null
            || state.Phase != RunPhase.MapChoice
            || world.EventIds.Contains(EventId, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Orobas requires an unvisited Hive Ancient boundary.");
    }

    public static bool IsOpeningEvent(RunWorldState world) =>
        world.Act == 2 && world.Floor == 0 && world.Map.CurrentNodeId is null
        && world.Event?.EventId == EventId;

    public static RunState BeginOpening(RunState state) =>
        BeginOpeningWithAudit(state, new NativeRandomIndexProviderV111()).State;

    /// <summary>
    /// Resolve the three event-entry offer choices through the supplied
    /// provider. Provider mode and decisions are returned separately; only
    /// the compatibility <see cref="BeginOpening(RunState)"/> entry point
    /// guarantees native-v111 mode.
    /// </summary>
    public static NativeOrobasEventAuditV111 BeginOpeningWithAudit(
        RunState state, IRandomIndexProviderV111 provider)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(provider);
        var providerMode = provider.Mode;
        if (providerMode is not ("native-v111" or "scripted-v111"
            or "controlled-v111"))
            throw new ArgumentException(
                "Orobas offer generation requires a supported random provider mode.",
                nameof(provider));
        ValidateOpeningBoundary(state);

        var world = state.World!;
        var fork = state.Fork();
        var context = NativeRngV111Contexts.ForEvent(fork.RunSeed, "OROBAS");
        var rng = new RngBundle([context]);
        var decisions = new List<RandomIndexDecisionV111>(RelicPools.Length);
        var offers = Enumerable.Range(0, RelicPools.Length).Select(ordinal =>
        {
            var eligible = OfferCandidateIds(fork.Player, ordinal);
            var decision = StochasticProviderScopeV111.ResolveIndex(
                provider, new RandomIndexRequestV111(
                    OfferSiteId, context.StreamId,
                    "event:OROBAS:act:2:floor:0", ordinal,
                    Enumerable.Range(0, eligible.Length).ToArray()), rng);
            decision.Validate();
            decisions.Add(decision);
            return eligible[decision.SelectedIndex];
        }).ToArray();
        var result = fork with
        {
            Phase = RunPhase.Event,
            World = fork.World! with
            {
                Event = new EventState(EventId, OfferedChoiceIds: offers,
                    NativeEventRng: rng.GetStream(context.StreamId),
                    NativeEventRngProviderMode: providerMode == "native-v111"
                        ? null : providerMode),
                EventHistory = world.EventIds.Append(EventId).ToArray()
            }
        };
        return new NativeOrobasEventAuditV111(
            result, decisions.ToArray(), providerMode);
    }

    public static RunState CreateOpeningFixture(string seed, int ascension = 0) =>
        BeginOpening(PrototypeNativeLaterActFixture.Create(seed, 2, ascension));

    internal static void ValidateEventContext(RunState state)
    {
        if (state.World?.Event is not { EventId: EventId } evt)
            return;
        if (evt.Stochastic.NativeRng is not { } stream
            || stream.StreamId != "context.event.OROBAS.player_0"
            || evt.OfferedChoiceIds is not { Length: 3 } offers)
            throw new InvalidOperationException(
                "Orobas requires three persisted offers and its event RNG context.");
        var initial = NativeRngV111Contexts.ForEvent(state.RunSeed, "OROBAS");
        if (evt.Stochastic.ProviderMode is { } mode)
        {
            if (mode is not ("scripted-v111" or "controlled-v111"))
                throw new InvalidOperationException(
                    "Orobas has an unsupported persisted random provider mode.");
            if (NativeRngV111.ToSnapshot(stream)
                != NativeRngV111.ToSnapshot(initial))
                throw new InvalidOperationException(
                    "Counterfactual Orobas providers must not advance the native event stream.");
        }
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

        if (evt.Stochastic.ProviderMode is null
            && evt.ChosenChoiceId is null)
        {
            var bundle = new RngBundle([initial]);
            var replayProvider = new NativeRandomIndexProviderV111(
                recordToJournal: false);
            var expectedOffers = Enumerable.Range(0, RelicPools.Length)
                .Select(ordinal =>
                {
                    var eligible = OfferCandidateIds(state.Player, ordinal);
                    var decision = replayProvider.Resolve(
                        new RandomIndexRequestV111(
                            OfferSiteId,
                            initial.StreamId,
                            "event:OROBAS:act:2:floor:0",
                            ordinal,
                            Enumerable.Range(0, eligible.Length).ToArray()),
                        bundle);
                    return eligible[decision.SelectedIndex];
                }).ToArray();
            if (!offers.SequenceEqual(expectedOffers, StringComparer.Ordinal)
                || NativeRngV111.ToSnapshot(stream)
                    != NativeRngV111.ToSnapshot(
                        bundle.GetStream(initial.StreamId)))
                throw new InvalidOperationException(
                    "Orobas offers or event RNG cursor do not match its native offer draws.");
        }
    }
}
