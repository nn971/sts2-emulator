namespace Sts2Emulator.Core;

public sealed record NativeNeowOfferAuditV111(
    string[] OfferedChoiceIds,
    RngStreamState NativeEventRng,
    RandomIndexDecisionV111[] Choices,
    string ProviderMode);

/// <summary>
/// Neow's existing single-player offer construction routed through the
/// persisted v0.111 event context and shared typed random providers. Source
/// option filtering and complete RNG call-order parity remain open.
/// </summary>
public static class NativeNeowEventV111
{
    public static NativeNeowOfferAuditV111 GenerateInitialOptions(
        string seed,
        IRandomIndexProviderV111? provider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        provider ??= new NativeRandomIndexProviderV111();
        if (provider.Mode is not ("native-v111" or "scripted-v111"
            or "controlled-v111"))
            throw new ArgumentException(
                "Neow offers require a supported random provider mode.",
                nameof(provider));

        var stream = NativeRngV111Contexts.ForEvent(
            seed, PrototypeNativeOvergrowthEvents.NeowRngEventEntry);
        var bundle = new RngBundle([stream]);
        var decisions = new List<RandomIndexDecisionV111>();
        int ChooseIndex(string siteId, string ownerId, int count)
        {
            var request = new RandomIndexRequestV111(
                siteId, stream.StreamId, ownerId, 0,
                Enumerable.Range(0, count).ToArray());
            var decision = StochasticProviderScopeV111.ResolveIndex(
                provider, request, bundle);
            decision.Validate();
            decisions.Add(decision);
            return decision.SelectedIndex;
        }

        void Shuffle(string[] values, string ownerId)
        {
            decisions.AddRange(NativeFisherYatesV111.ShuffleInPlace(
                values, bundle, provider, stream.StreamId, ownerId,
                PrototypeNativeOvergrowthEvents.NeowPositiveRelicShuffleSiteId));
        }

        var offers = PrototypeNativeOvergrowthEvents
            .GenerateNeowOfferedChoiceIds(ChooseIndex, Shuffle);
        return new NativeNeowOfferAuditV111(
            offers,
            bundle.Streams[0].Fork(),
            decisions.ToArray(),
            provider.Mode);
    }

    internal static void ValidateEventContext(RunState state)
    {
        if (state.World?.Event is not { EventId:
                PrototypeNativeOvergrowthEvents.NeowEventId } evt)
            return;

        if (evt.NativeEventRng is not { } stream)
        {
            if (evt.NativeEventRngProviderMode is not null)
                throw new InvalidOperationException(
                    "Neow provider mode is present without its persisted native event context.");
            // Older saved Overgrowth runs only retained the prototype
            // `event` stream. Keep those event states loadable.
            return;
        }

        var initial = NativeRngV111Contexts.ForEvent(
            state.RunSeed, PrototypeNativeOvergrowthEvents.NeowRngEventEntry);
        if (stream.StreamId != initial.StreamId)
            throw new InvalidOperationException(
                "Neow event RNG stream has the wrong native context.");
        if (evt.OfferedChoiceIds is not { Length: 3 } offers
            || !HasValidOfferShape(offers))
            throw new InvalidOperationException(
                "Native Neow offers must contain two eligible positives and one curse.");

        var mode = evt.NativeEventRngProviderMode;
        if (mode is not null
            && mode is not ("scripted-v111" or "controlled-v111"))
            throw new InvalidOperationException(
                "Neow has an unsupported persisted random provider mode.");

        if (mode is not null)
        {
            if (NativeRngV111.ToSnapshot(stream)
                != NativeRngV111.ToSnapshot(initial))
                throw new InvalidOperationException(
                    "Counterfactual Neow offer providers must not advance the native event stream.");
            return;
        }

        var replay = GenerateInitialOptions(
            state.RunSeed,
            new NativeRandomIndexProviderV111(recordToJournal: false));
        if (!offers.SequenceEqual(replay.OfferedChoiceIds,
                StringComparer.Ordinal)
            || NativeRngV111.ToSnapshot(stream)
                != NativeRngV111.ToSnapshot(replay.NativeEventRng))
            throw new InvalidOperationException(
                "Neow offers or event cursor disagree with their native context draws.");
    }

    private static bool HasValidOfferShape(string[] offers)
    {
        if (offers.Distinct(StringComparer.Ordinal).Count() != 3
            || !offers[2].StartsWith("take_", StringComparison.Ordinal))
            return false;

        var curseChoice = offers[2];
        var cursedName = PrototypeNativeOvergrowthEvents.NeowCursedRelicNames
            .SingleOrDefault(name => StringComparer.Ordinal.Equals(
                "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(name),
                curseChoice));
        if (cursedName is null)
            return false;

        var positiveNames = PrototypeNativeOvergrowthEvents
            .NeowPositiveRelicNames
            .Concat(PrototypeNativeOvergrowthEvents
                .NeowConditionalPositiveRelicNames)
            .Where(name => name is not ("MassiveScroll" or "Kaleidoscope"))
            .ToHashSet(StringComparer.Ordinal);
        if (offers.Take(2).Any(choice =>
                !positiveNames.Any(name => StringComparer.Ordinal.Equals(
                    choice,
                    "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(name)))))
            return false;

        var positiveOffers = offers.Take(2).ToHashSet(StringComparer.Ordinal);
        if (ContainsBoth("LavaRock", "SmallCapsule")
            || ContainsBoth("NutritiousOyster", "StoneHumidifier")
            || ContainsBoth("NeowsTalisman", "Pomander"))
            return false;

        bool ContainsBoth(string first, string second) =>
            positiveOffers.Contains("take_" +
                PrototypeNativeOvergrowthEvents.NeowRelicId(first))
            && positiveOffers.Contains("take_" +
                PrototypeNativeOvergrowthEvents.NeowRelicId(second));

        if (cursedName == "LargeCapsule"
            && (positiveOffers.Contains("take_" +
                    PrototypeNativeOvergrowthEvents.NeowRelicId("LavaRock"))
                || positiveOffers.Contains("take_" +
                    PrototypeNativeOvergrowthEvents.NeowRelicId("SmallCapsule"))))
            return false;

        var excluded = cursedName switch
        {
            "CursedPearl" => "GoldenPearl",
            "HeftyTablet" => "ArcaneScroll",
            "LeafyPoultice" => "NewLeaf",
            "PrecariousShears" => "PreciseScissors",
            "NeowsSacrifice" => "PhialHolster",
            _ => null
        };
        if (excluded is not null && positiveOffers.Contains(
                "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(excluded)))
            return false;
        return cursedName != "NeowsSacrifice"
            || !positiveOffers.Contains("take_" +
                PrototypeNativeOvergrowthEvents.NeowRelicId("LostCoffer"));
    }
}
