using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111LaterActEventRoutingTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Fact]
    public void CatalogsMatchPinnedActListsInSourceOrder()
    {
        Assert.Equal(new[]
        {
            PrototypeNativeHiveEvents.AmalgamatorId,
            PrototypeNativeLaterActs.HiveBugslayerId,
            PrototypeNativeLaterActEventPool.ColorfulPhilosophersId,
            PrototypeNativeHiveEvents.ColossalFlowerId,
            PrototypeNativeHiveEvents.FieldOfManSizedHolesId,
            PrototypeNativeHiveEvents.InfestedAutomatonId,
            PrototypeNativeHiveEvents.LostWispId,
            PrototypeNativeHiveEvents.SpiritGrafterId,
            PrototypeNativeHiveEvents.LanternKeyEventId,
            PrototypeNativeHiveEvents.ZenWeaverId
        }, PrototypeNativeLaterActEventPool.NativeEventIds(2));
        Assert.Equal(new[]
        {
            PrototypeNativeGloryEvents.BattlewornDummyId,
            PrototypeNativeGloryEvents.GraveOfTheForgottenId,
            PrototypeNativeGloryEvents.HungryForMushroomsId,
            PrototypeNativeGloryEvents.ReflectionsId,
            PrototypeNativeGloryEvents.RoundTeaPartyId,
            PrototypeNativeGloryEvents.TrialId,
            PrototypeNativeGloryEvents.TinkerTimeId
        }, PrototypeNativeLaterActEventPool.NativeEventIds(3));
        Assert.All(PrototypeNativeLaterActEventPool.NativeEventIds(2)
            .Concat(PrototypeNativeLaterActEventPool.NativeEventIds(3)),
            id =>
            {
                var evt = PrototypeContent.Event(id);
                Assert.Equal(0, evt.Weight);
                Assert.True(evt.OncePerRun);
            });
        Assert.DoesNotContain(PrototypeNativeHiveEvents.WarHistorianRepyId,
            PrototypeNativeLaterActEventPool.NativeEventIds(3));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrototypeNativeLaterActEventPool.NativeEventIds(1));
    }

    [Fact]
    public void HiveEligibilityUsesSourceConditionsAndDoesNotCrossActs()
    {
        var start = PrototypeNativeLaterActFixture.Create("hive-eligible", 2);
        var world = start.World!;
        var eligible = Pool(start);
        Assert.Contains(PrototypeNativeHiveEvents.LanternKeyEventId, eligible);
        Assert.Contains(PrototypeNativeLaterActs.HiveBugslayerId, eligible);
        Assert.Contains(PrototypeNativeLaterActEventPool.ColorfulPhilosophersId,
            eligible);
        Assert.DoesNotContain(PrototypeNativeGloryEvents.TrialId, eligible);
        Assert.DoesNotContain(PrototypeNativeHiveEvents.WarHistorianRepyId,
            eligible);

        var depleted = start with
        {
            Player = start.Player with
            {
                Gold = 0,
                Hp = 10,
                Deck = start.Player.Deck.Where(card =>
                    card.CardId is not ("proto.silent.strike"
                        or "proto.silent.defend")).ToArray()
            }
        };
        var choices = Pool(depleted);
        Assert.DoesNotContain(PrototypeNativeHiveEvents.AmalgamatorId, choices);
        Assert.DoesNotContain(PrototypeNativeHiveEvents.ZenWeaverId, choices);
        Assert.DoesNotContain(PrototypeNativeHiveEvents.ColossalFlowerId, choices);
        Assert.Equal(eligible, Pool(start));
        Assert.Equal(world.EventIds, start.World!.EventIds);
    }

    [Fact]
    public void GloryUsesSourceHpAndEnchantEligibilityAndNeverIncludesQuest()
    {
        var state = PrototypeNativeLaterActFixture.Create("glory-eligible", 3);
        var normal = Pool(state);
        Assert.Contains(PrototypeNativeGloryEvents.TrialId, normal);
        Assert.Contains(PrototypeNativeGloryEvents.TinkerTimeId, normal);
        Assert.Contains(PrototypeNativeGloryEvents.BattlewornDummyId, normal);
        Assert.DoesNotContain(PrototypeNativeHiveEvents.LanternKeyEventId, normal);
        Assert.DoesNotContain(PrototypeNativeHiveEvents.WarHistorianRepyId, normal);

        var lowHp = state with
        {
            Player = state.Player with { Hp = 11 }
        };
        Assert.DoesNotContain(PrototypeNativeGloryEvents.RoundTeaPartyId,
            Pool(lowHp));
        Assert.Contains(PrototypeNativeGloryEvents.RoundTeaPartyId,
            Pool(state));

        var exhaustCard = new CardInstance(state.World!.NextCardInstanceId,
            PrototypeNativeHiveEvents.MetamorphosisId, 0,
            PrototypeJson.EmptyObject());
        var withExhaust = state with
        {
            Player = state.Player with
            {
                Deck = state.Player.Deck.Append(exhaustCard).ToArray()
            },
            World = state.World with
            {
                NextCardInstanceId = state.World.NextCardInstanceId + 1
            }
        };
        Assert.Contains(PrototypeNativeGloryEvents.GraveOfTheForgottenId,
            Pool(withExhaust));
    }

    [Fact]
    public void OneTimeEventHistoryFiltersNativePoolAndDoesNotMutateOriginal()
    {
        var state = PrototypeNativeLaterActFixture.Create("hive-history", 2);
        var id = PrototypeNativeHiveEvents.InfestedAutomatonId;
        var modified = state with
        {
            World = state.World! with { EventHistory = [id] }
        };
        Assert.DoesNotContain(id, Pool(modified));
        Assert.Contains(id, Pool(state));

        // Full native roster remains explicit even when exhausted.
        var exhausted = modified with
        {
            World = modified.World! with
            {
                EventHistory = PrototypeNativeLaterActEventPool
                    .NativeEventIds(2)
            }
        };
        Assert.Empty(Pool(exhausted));
        Assert.Throws<NotSupportedException>(() =>
            EnterEvent(exhausted));
        Assert.Equal(new[] { id }, modified.World!.EventIds);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void MapChoiceStartsNativeEventRatherThanLegacyPool(int act)
    {
        bool foundExecutable = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < 45; index++)
        {
            var initial = PrototypeNativeLaterActFixture.Create(
                $"native-map-event-{act}-{index}", act);
            var state = EventAtMapChoice(initial);
            var source = CanonicalJson.Sha256(state);
            var nextNode = state.World!.Map.AvailableNodes()
                .Single(n => n.RoomType == PrototypeRoomType.Event);
            var action = GameAction.Create("choose_map_node",
                new ChooseMapNodePayload(nextNode.NodeId));

            try
            {
                var result = Engine.Step(state, action).State;
                Assert.Equal(RunPhase.Event, result.Phase);
                var id = result.World!.Event!.EventId;
                Assert.Contains(id,
                    PrototypeNativeLaterActEventPool.NativeEventIds(act));
                Assert.NotEqual(PrototypeNativeHiveEvents.WarHistorianRepyId,
                    id);
                Assert.Equal(id, result.World.EventIds[^1]);
                Assert.Equal(PrototypeRoomType.Event, result.World.ActiveRoom);
                Assert.Equal(source, CanonicalJson.Sha256(state));
                Assert.Equal(CanonicalJson.Sha256(result),
                    CanonicalJson.Sha256(Engine.Step(state.Fork(), action).State));
                PrototypeStateInvariants.Validate(result);
                Assert.NotEmpty(Engine.GetLegalActions(result));
                foundExecutable = true;
                seen.Add(id);
            }
            catch (NotSupportedException e) when (
                e.Message.Contains("colorful_philosophers",
                    StringComparison.Ordinal))
            {
                Assert.Equal(2, act);
                Assert.Equal(source, CanonicalJson.Sha256(state));
            }
        }
        Assert.True(foundExecutable);
        Assert.True(seen.Count >= 3);
    }

    [Fact]
    public void UnimplementedColorfulPhilosophersFailsBeforeStateMutation()
    {
        var state = PrototypeNativeLaterActFixture.Create("colorful-only", 2);
        var left = PrototypeNativeLaterActEventPool.ColorfulPhilosophersId;
        state = state with
        {
            World = state.World! with
            {
                EventHistory = PrototypeNativeLaterActEventPool
                    .NativeEventIds(2)
                    .Where(id => id != left).ToArray()
            }
        };
        Assert.Equal(new[] { left }, Pool(state));
        var mapChoice = EventAtMapChoice(state);
        var hash = CanonicalJson.Sha256(mapChoice);
        var node = mapChoice.World!.Map.AvailableNodes()
            .Single(x => x.RoomType == PrototypeRoomType.Event);
        var action = GameAction.Create("choose_map_node",
            new ChooseMapNodePayload(node.NodeId));
        var error = Assert.Throws<NotSupportedException>(() =>
            Engine.Step(mapChoice, action));
        Assert.Contains("cross-character", error.Message);
        Assert.Equal(hash, CanonicalJson.Sha256(mapChoice));
        Assert.Null(mapChoice.World!.Event);
    }

    [Fact]
    public void NativeLaterActCombatEntriesUseRegionalEncounterPools()
    {
        foreach (var act in new[] { 2, 3 })
        {
            var state = PrototypeNativeLaterActFixture.Create(
                $"gate-normal-{act}", act);
            var hash = CanonicalJson.Sha256(state);
            var combat = Engine.GetLegalActions(state)
                .First(a => state.World!.Map.AvailableNodes()
                    .Any(n => n.NodeId ==
                        a.ReadPayload<ChooseMapNodePayload>().NodeId
                        && n.RoomType == PrototypeRoomType.Combat));
            var entered = Engine.Step(state, combat).State;
            Assert.Equal(RunPhase.Combat, entered.Phase);
            Assert.Equal(PrototypeRoomType.Combat,
                entered.World!.ActiveRoom);
            Assert.Contains(entered.World.EncounterIds[^1],
                PrototypeNativeLaterActCombatPool.Names(
                    act, PrototypeRoomType.Combat, weak: true));
            Assert.Equal(hash, CanonicalJson.Sha256(state));
            PrototypeStateInvariants.Validate(entered);
        }
    }

    [Theory]
    [InlineData(2, 12)]
    [InlineData(3, 11)]
    public void LaterMapFloorValidationUsesNativeRoomCount(int act, int floor)
    {
        var initial = PrototypeNativeLaterActFixture.Create(
            $"late-floor-{act}", act);
        var destination = initial.World!.Map.Nodes
            .First(n => n.Floor == floor);
        var state = initial with
        {
            World = initial.World with
            {
                Floor = floor,
                Map = initial.World.Map with
                {
                    CurrentNodeId = destination.NodeId
                }
            }
        };
        PrototypeStateInvariants.Validate(state);
    }

    private static string[] Pool(RunState state) =>
        PrototypeNativeLaterActEventPool.EligibleEventIds(
            state.World!, state.Player);

    private static RunState EventAtMapChoice(RunState initial)
    {
        var world = initial.World!;
        // Maintain the source-shaped geometry, replacing only one
        // non-fixed second-row node for a targeted room-dispatch test.
        var entry = world.Map.Nodes.First(n =>
            n.Floor == 1 && n.NextNodeIds is { Length: > 0 });
        var childId = entry.NextNodeIds![0];
        var nodes = world.Map.Nodes.Select(node =>
            node.NodeId == childId
                ? node with { RoomType = PrototypeRoomType.Event }
                : node).ToArray();
        return initial with
        {
            World = world with
            {
                Floor = 1,
                Map = world.Map with
                {
                    Nodes = nodes,
                    CurrentNodeId = entry.NodeId
                }
            }
        };
    }

    private static RunState EnterEvent(RunState state)
    {
        state = EventAtMapChoice(state);
        var node = state.World!.Map.AvailableNodes()
            .Single(x => x.RoomType == PrototypeRoomType.Event);
        return Engine.Step(state, GameAction.Create(
            "choose_map_node",
            new ChooseMapNodePayload(node.NodeId))).State;
    }
}
