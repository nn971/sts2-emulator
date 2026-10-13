namespace Sts2Emulator.Core;

public static class PrototypeGameFactory
{
    public static RunState Create(
        string seed,
        int ascension = 0)
    {
        if (ascension < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ascension));
        }
        var empty = PrototypeJson.EmptyObject();
        return new RunState(
            GameBuild: "prototype-unbound",
            EmulatorSchema: "prototype-0.1",
            RunId: $"prototype-{seed}",
            RunSeed: seed,
            DecisionIndex: 0,
            Phase: RunPhase.RunStart,
            Player: new PlayerState(
                Hp: 0,
                MaxHp: 0,
                Gold: 0,
                Deck: Array.Empty<CardInstance>(),
                Relics: Array.Empty<RelicInstance>(),
                PotionSlots: Array.Empty<PotionInstance?>()),
            Rng: RngBundle.Empty,
            ExtensionState: empty,
            World: null,
            Ascension: ascension);
    }
}
