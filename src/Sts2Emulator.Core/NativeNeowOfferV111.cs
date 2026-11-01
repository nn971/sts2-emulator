namespace Sts2Emulator.Core;

/// <summary>
/// Read-only reconstruction of the pinned v0.111 Neow initial offer using
/// NativeRngV111 rather than the live prototype event/map streams.
///
/// Source: Neow.GenerateInitialOptions; EventModel.BeginEvent;
/// RelicModel.IsAllowedAtNeow; MegaRandom; ListExtensions.UnstableShuffle
/// (Flaaax/STS2-source@706ef1c9). Two unlock-dependent availability
/// gates remain explicit hypotheses. The native game is never invoked.
/// </summary>
public static class NativeNeowOfferV111
{
    public const string Schema = "sts2-v111-native-neow-rng-probe-v1";
    private const string StreamId = "event.neow";

    public sealed record Prediction(
        bool KaleidoscopeAllowed,
        bool ScrollBoxesAllowed,
        string[] OfferedRelicIds,
        NativeRngSnapshot EventRng,
        int PositivePoolSize,
        string CursedRelicId);

    public static Prediction[] PredictAll(string runSeed) =>
    [
        Predict(runSeed, kaleidoscopeAllowed: false, scrollBoxesAllowed: false),
        Predict(runSeed, kaleidoscopeAllowed: false, scrollBoxesAllowed: true),
        Predict(runSeed, kaleidoscopeAllowed: true, scrollBoxesAllowed: false),
        Predict(runSeed, kaleidoscopeAllowed: true, scrollBoxesAllowed: true)
    ];

    public static Prediction Predict(
        string runSeed, bool kaleidoscopeAllowed, bool scrollBoxesAllowed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runSeed);
        // Singleplayer slot zero, a nonshared Neow event and no modifiers.
        // EventModel.BeginEvent seeds new Rng(runSeed + hash("NEOW")).
        var eventStream = NativeRngV111.CreateContentStream(
            StreamId, NativeRngV111.RunSeed(runSeed), "NEOW");
        var random = new RngBundle([eventStream]);

        var curses = PrototypeNativeOvergrowthEvents.NeowCursedRelicNames;
        var curse = curses[NativeRngV111.NextInt(random, StreamId, curses.Length)];

        // Neow's canonical order matters: UnstableShuffle depends on it.
        // MassiveScroll.IsAllowed requires multiplayer.
        var positives = PrototypeNativeOvergrowthEvents.NeowPositiveRelicNames
            .Where(name => name != "MassiveScroll"
                && (name != "Kaleidoscope" || kaleidoscopeAllowed)
                && (name != "ScrollBoxes" || scrollBoxesAllowed))
            .ToList();

        switch (curse)
        {
            case "CursedPearl":
                positives.Remove("GoldenPearl");
                break;
            case "HeftyTablet":
                positives.Remove("ArcaneScroll");
                break;
            case "LeafyPoultice":
                positives.Remove("NewLeaf");
                break;
            case "PrecariousShears":
                positives.Remove("PreciseScissors");
                break;
            case "NeowsSacrifice":
                positives.Remove("PhialHolster");
                positives.Remove("LostCoffer");
                break;
        }

        if (curse != "LargeCapsule")
            positives.Add(NativeRngV111.NextBool(random, StreamId)
                ? "LavaRock" : "SmallCapsule");
        positives.Add(NativeRngV111.NextBool(random, StreamId)
            ? "NutritiousOyster" : "StoneHumidifier");
        positives.Add(NativeRngV111.NextBool(random, StreamId)
            ? "NeowsTalisman" : "Pomander");

        int positiveCount = positives.Count;
        NativeRngV111.Shuffle(random, StreamId, positives);

        string[] offers = positives.Take(2).Append(curse)
            .Select(PrototypeNativeOvergrowthEvents.NeowRelicId)
            .ToArray();
        return new Prediction(kaleidoscopeAllowed, scrollBoxesAllowed,
            offers, NativeRngV111.ToSnapshot(random.Streams.Single()),
            positiveCount, PrototypeNativeOvergrowthEvents.NeowRelicId(curse));
    }
}
