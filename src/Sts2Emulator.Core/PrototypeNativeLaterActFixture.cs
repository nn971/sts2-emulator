namespace Sts2Emulator.Core;

/// <summary>
/// An explicit later-act map inspection/quest fixture. This is not a
/// playable native route, and regular Hive/Glory rooms stay unsupported.
/// </summary>
public static class PrototypeNativeLaterActFixture
{
    public static RunState Create(
        string seed, int act, int ascension = 0, int lanternKeys = 0)
    {
        _ = PrototypeNativeLaterActMap.ProfileId(act);
        if (lanternKeys < 0 || lanternKeys > 32
            || (lanternKeys > 0 && act != 3))
            throw new ArgumentOutOfRangeException(nameof(lanternKeys),
                "Lantern Keys are supported by this fixture in Glory only.");

        var engine = new PrototypeGameEngine();
        var state = engine.Step(
            PrototypeGameFactory.Create(seed, ascension),
            GameAction.Empty("start_run")).State;
        var world = state.World
            ?? throw new InvalidOperationException("Run did not initialize.");

        // Do not inherit six-floor prototype map generation consumption.
        var rng = state.Rng.Fork();
        var clean = PrototypeRng.CreateBundle(seed);
        var index = Array.FindIndex(rng.Streams,
            stream => stream.StreamId == "map");
        var cleanIndex = Array.FindIndex(clean.Streams,
            stream => stream.StreamId == "map");
        rng.Streams[index] = clean.Streams[cleanIndex].Fork();

        var map = PrototypeNativeLaterActMap.Generate(rng, act, ascension);
        var keyId = PrototypeNativeHiveEvents.LanternKeyCardId;
        var deck = state.Player.Deck.Concat(
            Enumerable.Range(0, lanternKeys).Select(i =>
                new CardInstance(world.NextCardInstanceId + i,
                    keyId, 0, PrototypeJson.EmptyObject()))).ToArray();

        return state with
        {
            Player = state.Player with { Deck = deck },
            Rng = rng,
            World = world with
            {
                Act = act,
                Floor = 0,
                Map = map,
                ActiveRoom = null,
                Combat = null,
                Event = null,
                Reward = null,
                Shop = null,
                ActOneRegion = null,
                ActOneEncounterPool = null,
                NativeOvergrowthOpening = false,
                NextCardInstanceId = world.NextCardInstanceId + lanternKeys
            },
            Phase = RunPhase.MapChoice
        };
    }
}
