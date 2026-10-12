using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111PaelsLegionTests
{
    private static readonly PrototypeGameEngine Engine = new();

    private static RunState Combat()
    {
        var state = NativePaelRewardsV111.CreateRewardFixture("paels-legion",
            NativePaelRewardsV111.LegionId);
        state = Engine.Step(state, Assert.Single(Engine.GetLegalActions(state))).State;
        state = state with
        {
            Player = state.Player with { Hp = 1000, MaxHp = 1000 },
            World = state.World! with
            {
                Map = new MapState([], GenerationProfileId: PrototypeNativeLaterActMap.HiveProfileId),
                Floor = 1, ActiveRoom = PrototypeRoomType.Combat, Event = null, Reward = null
            }
        };
        var method = typeof(PrototypeGameEngine).GetMethod("StartCombat", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (RunState)method.Invoke(null, [state, PrototypeRoomType.Combat,
            PrototypeContent.Encounter("proto.native.hive.encounter.tunneler_weak")])!;
    }

    private static RunState PlayDefend(RunState state, long id)
    {
        var combat = state.World!.Combat!;
        combat = combat with
        {
            Energy = 3,
            Hand = combat.Hand.Contains(id) ? combat.Hand : combat.Hand.Append(id).ToArray(),
            DrawPile = combat.DrawPile.Where(cardId => cardId != id).ToArray(),
            DiscardPile = combat.DiscardPile.Where(cardId => cardId != id).ToArray(),
            ExhaustPile = combat.ExhaustPile.Where(cardId => cardId != id).ToArray()
        };
        state = state with { World = state.World with { Combat = combat } };
        var play = Engine.GetLegalActions(state).Single(action => action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId == id);
        return Engine.Step(state, play).State;
    }

    private static RunState EndTurn(RunState state)
    {
        var action = Engine.GetLegalActions(state).Single(action => action.Kind == "end_turn");
        var result = Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State;
        Assert.Equal(CanonicalJson.Sha256(result), CanonicalJson.Sha256(Engine.Step(state, action).State));
        PrototypeStateInvariants.Validate(result);
        return result;
    }

    [Fact]
    public void LegionPersistsPetIdentityAndSkinAndDoublesCardBlockOnCooldown()
    {
        var state = Combat();
        var relic = Assert.Single(state.Player.Relics, item => item.RelicId == NativePaelRewardsV111.LegionId);
        var skin = NativePaelRewardsV111.LegionSkin(relic);
        var combat = state.World!.Combat!;
        var pet = Assert.Single(combat.Pets!);
        Assert.Equal(NativePaelRewardsV111.LegionPetId, pet.PetId);
        Assert.Equal(skin, pet.SkinId);
        Assert.Equal(Array.IndexOf(state.Player.Relics, relic), pet.SourceRelicIndex);
        PrototypeStateInvariants.Validate(state);

        var defend = state.Player.Deck.First(card => card.CardId == "proto.silent.defend");
        state = PlayDefend(state, defend.InstanceId);
        Assert.Equal(10, state.World!.Combat!.PlayerBlock);
        var legion = Assert.Single(state.World.Combat.RelicStates,
            item => item.RelicId == NativePaelRewardsV111.LegionId);
        Assert.Equal(2, legion.PaelsLegionCooldown);
        Assert.True(legion.PaelsLegionTriggeredBlockLastTurn);
        Assert.Null(legion.PaelsLegionAffectedCardPlaySourceId);

        state = EndTurn(state);
        Assert.Equal(1, state.World!.Combat!.RelicStates.Single(item =>
            item.RelicId == NativePaelRewardsV111.LegionId).PaelsLegionCooldown);
        state = EndTurn(state);
        Assert.Equal(0, state.World!.Combat!.RelicStates.Single(item =>
            item.RelicId == NativePaelRewardsV111.LegionId).PaelsLegionCooldown);

        state = PlayDefend(state, defend.InstanceId);
        Assert.Equal(10, state.World!.Combat!.PlayerBlock);
        Assert.Equal(2, state.World.Combat.RelicStates.Single(item =>
            item.RelicId == NativePaelRewardsV111.LegionId).PaelsLegionCooldown);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void SkinAndPetIdentitySurviveSaveLoadAndFork()
    {
        var state = Combat();
        var hash = CanonicalJson.Sha256(state);
        var loaded = RunSnapshot.Load(RunSnapshot.Save(state));
        var fork = loaded.Fork();
        Assert.Equal(hash, CanonicalJson.Sha256(loaded));
        Assert.Equal(state.World!.Combat!.Pets, fork.World!.Combat!.Pets);
        Assert.NotSame(state.World.Combat.Pets, fork.World.Combat!.Pets);
        PrototypeStateInvariants.Validate(fork);
    }
}
