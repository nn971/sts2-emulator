using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111DelicateFrondTests
{
    [Fact]
    public void FillsOpenPotionSlotsFromCombatPotionGenerationBeforeCombat()
    {
        const string seed = "glory-delicate-frond-native";
        var expectedRng = NativeRngV111.CreateBundle(seed);
        var expectedPotions = Enumerable.Range(0, 2)
            .Select(_ => PrototypeNativePotionShop
                .PickWeightedDistinctNative(expectedRng,
                    "run.combat_potion_generation", 1)[0])
            .ToArray();
        var (engine, state) = CombatEntry(seed,
            new RelicInstance(NativeGloryAncientsV111.DelicateFrondId,
                PrototypeJson.EmptyObject()));

        state = EnterCombat(engine, state);

        Assert.Equal(expectedPotions,
            state.Player.PotionSlots.Select(potion => potion?.PotionId));
        Assert.Equal(NativeRngV111.ToSnapshot(expectedRng.Streams.Single(
                stream => stream.StreamId
                    == "run.combat_potion_generation")),
            NativeRngV111.ToSnapshot(state.NativeRunRng!.Streams.Single(
                stream => stream.StreamId
                    == "run.combat_potion_generation")));
        var snapshot = RunSnapshot.Load(RunSnapshot.Save(state));
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(snapshot));
    }

    [Fact]
    public void SozuRejectsProcurementAfterOnePotionGeneration()
    {
        const string seed = "glory-delicate-frond-sozu";
        var expectedRng = NativeRngV111.CreateBundle(seed);
        _ = PrototypeNativePotionShop.PickWeightedDistinctNative(
            expectedRng, "run.combat_potion_generation", 1);
        var (engine, state) = CombatEntry(seed,
            new RelicInstance(NativeGloryAncientsV111.DelicateFrondId,
                PrototypeJson.EmptyObject()),
            new RelicInstance("proto.relic.sozu",
                PrototypeJson.EmptyObject()));

        state = EnterCombat(engine, state);

        Assert.All(state.Player.PotionSlots, Assert.Null);
        Assert.Equal(NativeRngV111.ToSnapshot(expectedRng.Streams.Single(
                stream => stream.StreamId
                    == "run.combat_potion_generation")),
            NativeRngV111.ToSnapshot(state.NativeRunRng!.Streams.Single(
                stream => stream.StreamId
                    == "run.combat_potion_generation")));
    }

    private static (PrototypeGameEngine Engine, RunState State) CombatEntry(
        string seed, params RelicInstance[] relics)
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create(seed);
        state = engine.Step(state,
            Assert.Single(engine.GetLegalActions(state))).State;
        state = state with
        {
            Player = state.Player with
            {
                PotionSlots = [null, null],
                Relics = [..state.Player.Relics, ..relics]
            },
            NativeRunRng = NativeRngV111.CreateBundle(seed)
        };
        return (engine, state);
    }

    private static RunState EnterCombat(
        PrototypeGameEngine engine, RunState state)
    {
        var nodeId = state.World!.Map.AvailableNodes()[0].NodeId;
        return engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload(nodeId))).State;
    }
}
