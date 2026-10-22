namespace Sts2Emulator.Core;

/// <summary>
/// Single player Tea Master event and its three charged event relics.
/// Pins: TeaMaster.cs, BoneTea.cs, EmberTea.cs and TeaOfDiscourtesy.cs,
/// v0.111.0.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private const string TeaCombatsLeftProperty = "combats_left";

    private static IReadOnlyList<GameAction> GetNativeTeaMasterActions(
        RunState state, EventState eventState)
    {
        if (eventState.NativePageIndex != 0)
            throw new InvalidOperationException(
                "Tea Master has no choices on its result page.");

        var player = RemoveExpiredTeaRelics(state.Player);
        return PrototypeContent.Event(eventState.EventId).Choices
            .Where(choice => player.Gold >= TeaPrice(choice.Id)
                && FindChargedTeaRelic(
                    player, TeaRelicForChoice(choice.Id)) < 0)
            .Select(choice => GameAction.Create("event_choice",
                new EventChoicePayload(choice.Id))).ToArray();
    }

    private static RunState StepNativeTeaMaster(
        RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        if (world.Act != 3 || world.Event?.EventId !=
                PrototypeNativeGloryEvents.TeaMasterId
            || world.Event.NativePageIndex != 0)
        {
            throw new InvalidOperationException(
                "Tea Master requires its active Glory event page.");
        }

        var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
        var (relicId, price, combatsLeft) = choice switch
        {
            "bone_tea" =>
                (PrototypeNativeGloryEvents.BoneTeaId, 50, 1),
            "ember_tea" =>
                (PrototypeNativeGloryEvents.EmberTeaId, 150, 5),
            "tea_of_discourtesy" =>
                (PrototypeNativeGloryEvents.TeaOfDiscourtesyId, 0, 1),
            _ => throw new InvalidOperationException(
                $"Unknown Tea Master choice '{choice}'.")
        };
        var playerBeforePurchase = RemoveExpiredTeaRelics(
            state.Player);
        if (playerBeforePurchase.Gold < price)
        {
            throw new InvalidOperationException(
                "Tea Master choice costs more gold than the player has.");
        }
        if (FindChargedTeaRelic(playerBeforePurchase, relicId) >= 0)
        {
            throw new InvalidOperationException(
                $"Player already owns relic '{relicId}'.");
        }

        var relic = new RelicInstance(
            relicId,
            System.Text.Json.JsonSerializer.SerializeToElement(
                new Dictionary<string, int>
                {
                    [TeaCombatsLeftProperty] = combatsLeft
                }));
        var player = playerBeforePurchase with
        {
            Gold = playerBeforePurchase.Gold - price,
            Relics = playerBeforePurchase.Relics.Append(relic).ToArray()
        };
        var prototypeRng = state.Stochastic.PrototypeRng;
        player = ApplyRelicRunEvent(
            player,
            PrototypeRunEventKind.RelicAcquired,
            acquiredRelicId: relicId,
            rng: prototypeRng);
        var stochastic = RunStochasticStateV111.WithPrototypeRng(
            state.Stochastic, prototypeRng);
        return PreserveNativeEventCompletionPage(
            state with { Player = player, Stochastic = stochastic }, choice);
    }

    private static (PlayerState Player, CombatState Combat)
        ApplyTeaOfDiscourtesyBeforeCombat(
            PlayerState player,
            CombatState combat,
            RngBundle rng)
    {
        var relicIndex = FindChargedTeaRelic(
            player, PrototypeNativeGloryEvents.TeaOfDiscourtesyId);
        if (relicIndex < 0)
            return (player, combat);

        for (var i = 0; i < 2; i++)
        {
            var instanceId = combat.NextCardInstanceId;
            var card = new CombatCardInstance(
                InstanceId: instanceId,
                PersistentCardInstanceId: null,
                CardId: "proto.status.dazed",
                UpgradeLevel: 0,
                IsTemporary: true,
                State: PrototypeJson.EmptyObject());
            var position = LegacyRandomChoiceV111.ChooseIndexRange(
                rng,
                StochasticSiteRegistryV111.DrawPileRandomInsertSiteId,
                "combat",
                $"relic:tea_of_discourtesy:insert:{i}:count:{combat.DrawPile.Length}",
                combat.DrawPile.Length + 1).SelectedIndex;
            combat = SemanticDrawPileAdapterV111
                .ApplyNewDrawPileInsertion(combat, position, card);
        }

        return (
            ConsumeTeaCharge(player, relicIndex),
            combat);
    }

    private static PlayerState RemoveExpiredTeaRelics(PlayerState player) =>
        player with
        {
            Relics = player.Relics.Where(relic =>
                relic.RelicId is not (
                    PrototypeNativeGloryEvents.BoneTeaId
                    or PrototypeNativeGloryEvents.EmberTeaId
                    or PrototypeNativeGloryEvents.TeaOfDiscourtesyId)
                || ReadTeaCharges(relic) > 0).ToArray()
        };

    private static (PlayerState Player, CombatState Combat)
        ApplyBoneTeaAfterPlayerTurnStart(
            PlayerState player, CombatState combat)
    {
        var relicIndex = FindChargedTeaRelic(
            player, PrototypeNativeGloryEvents.BoneTeaId);
        if (relicIndex < 0 || combat.Turn != 1)
            return (player, combat);

        var hand = combat.Hand.ToHashSet();
        combat = combat with
        {
            Cards = combat.Cards.Select(card =>
            {
                var definition = PrototypeContent.Card(card.CardId);
                return hand.Contains(card.InstanceId)
                    && definition.Type is not
                        (PrototypeCardType.Status
                        or PrototypeCardType.Curse)
                    && card.UpgradeLevel < definition.MaxUpgradeLevel
                    ? card with
                    {
                        UpgradeLevel = card.UpgradeLevel + 1
                    }
                    : card;
            }).ToArray()
        };
        return (ConsumeTeaCharge(player, relicIndex), combat);
    }

    private static int FindChargedTeaRelic(
        PlayerState player, string relicId)
    {
        for (var index = 0; index < player.Relics.Length; index++)
        {
            var relic = player.Relics[index];
            if (relic.RelicId == relicId
                && !relic.IsMelted
                && ReadTeaCharges(relic) > 0)
            {
                return index;
            }
        }
        return -1;
    }

    private static int ReadTeaCharges(RelicInstance relic)
    {
        if (relic.PersistentState.ValueKind ==
                System.Text.Json.JsonValueKind.Object
            && relic.PersistentState.TryGetProperty(
                TeaCombatsLeftProperty, out var value)
            && value.ValueKind == System.Text.Json.JsonValueKind.Number)
        {
            return Math.Max(0, value.GetInt32());
        }
        return 0;
    }

    private static int TeaPrice(string choiceId) => choiceId switch
    {
        "bone_tea" => 50,
        "ember_tea" => 150,
        "tea_of_discourtesy" => 0,
        _ => int.MaxValue
    };

    private static string TeaRelicForChoice(string choiceId) => choiceId switch
    {
        "bone_tea" => PrototypeNativeGloryEvents.BoneTeaId,
        "ember_tea" => PrototypeNativeGloryEvents.EmberTeaId,
        "tea_of_discourtesy" =>
            PrototypeNativeGloryEvents.TeaOfDiscourtesyId,
        _ => throw new InvalidOperationException(
            $"Unknown Tea Master choice '{choiceId}'.")
    };

    private static PlayerState ConsumeTeaCharge(
        PlayerState player, int relicIndex)
    {
        var relics = (RelicInstance[])player.Relics.Clone();
        var relic = relics[relicIndex];
        var remaining = Math.Max(0, ReadTeaCharges(relic) - 1);
        relics[relicIndex] = relic with
        {
            PersistentState =
                System.Text.Json.JsonSerializer.SerializeToElement(
                    new Dictionary<string, int>
                    {
                        [TeaCombatsLeftProperty] = remaining
                    })
        };
        return player with { Relics = relics };
    }
}
