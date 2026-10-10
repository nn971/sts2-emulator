namespace Sts2Emulator.Core;

/// <summary>
/// v0.111.0 Hive Spirit Grafter + Zen Weaver, Glory Hungry for Mushrooms.
/// All event choices, event-only cards and relic mechanics are source-pinned,
/// while native RNG/discovery distribution remains unverified.
/// </summary>
public static class PrototypeNativeLaterActEventExpansionV3
{
    public const string SpiritGrafterId = "proto.native.hive.spirit_grafter";
    public const string ZenWeaverId = "proto.native.hive.zen_weaver";
    public const string HungryForMushroomsId =
        "proto.native.glory.hungry_for_mushrooms";
    public const string MetamorphosisId = "proto.event.metamorphosis";
    public const string EnlightenmentId = "proto.event.enlightenment";
    public const string BigMushroomId =
        "proto.native.glory.relic.big_mushroom";
    public const string FragrantMushroomId =
        "proto.native.glory.relic.fragrant_mushroom";

    public static PrototypeCardDefinition[] Cards { get; } =
    [
        new(MetamorphosisId, "Metamorphosis", 2,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind
                    .CreateRandomCharacterAttackCardsInDrawPile,
                    3, UpgradeDelta: 2)
            ], Rarity: PrototypeCardRarity.Event,
            Type: PrototypeCardType.Skill,
            ExhaustOnUse: true, RewardEligible: false,
            CanBeGeneratedInCombat: false),
        new(EnlightenmentId, "Enlightenment", 0,
            PrototypeCardTarget.None,
            [
                new(PrototypeCombatEffectKind
                    .SetHandCardsEnergyCostMaxOne, 1)
            ], Rarity: PrototypeCardRarity.Event,
            Type: PrototypeCardType.Skill,
            ExhaustOnUse: true, RewardEligible: false,
            CanBeGeneratedInCombat: false)
    ];

    public static PrototypeRelicDefinition[] Relics { get; } =
    [
        new(BigMushroomId, "Big Mushroom"),
        new(FragrantMushroomId, "Fragrant Mushroom")
    ];

    public static PrototypeEventDefinition[] Events { get; } =
    [
        new(SpiritGrafterId, "Spirit Grafter",
        [
            new("let_it_in", "Heal 25 and receive Metamorphosis", []),
            new("rejection", "Upgrade one card, lose 10 HP", [])
        ], MinAct: 2, MaxAct: 2, Weight: 0),
        new(ZenWeaverId, "Zen Weaver",
        [
            new("breathing_techniques", "Pay 50 Gold for two Enlightenments", []),
            new("emotional_awareness", "Pay 125 Gold to remove one card", []),
            new("arachnid_acupuncture", "Pay 250 Gold to remove two cards", [])
        ], MinAct: 2, MaxAct: 2, Weight: 0),
        new(HungryForMushroomsId, "Hungry for Mushrooms",
        [
            new("big_mushroom", "Big Mushroom: +20 maximum HP, -2 first-turn draw", []),
            new("fragrant_mushroom", "Fragrant Mushroom: lose 15 HP, upgrade two random cards", [])
        ], MinAct: 3, MaxAct: 3, Weight: 0)
    ];

    public static bool IsEligible(string id, PlayerState player) =>
        id != ZenWeaverId || player.Gold >= 125;

    public static int BigMushroomFirstTurnDrawPenalty(PlayerState player) =>
        player.Relics.Count(relic => relic.RelicId == BigMushroomId) * 2;
}

public sealed partial class PrototypeGameEngine
{
    private static bool IsLaterActV3Event(string id) =>
        id is PrototypeNativeLaterActEventExpansionV3.SpiritGrafterId
            or PrototypeNativeLaterActEventExpansionV3.ZenWeaverId
            or PrototypeNativeLaterActEventExpansionV3.HungryForMushroomsId;

    private static IReadOnlyList<GameAction> GetLaterActV3Actions(
        RunState state)
    {
        var evt = RequireWorld(state).Event
            ?? throw new InvalidOperationException("Event state missing.");
        var options = PrototypeContent.Event(evt.EventId).Choices;
        return options
            .Where(choice => evt.EventId !=
                    PrototypeNativeLaterActEventExpansionV3.ZenWeaverId
                || state.Player.Gold >= (choice.Id switch
                {
                    "breathing_techniques" => 50,
                    "emotional_awareness" => 125,
                    "arachnid_acupuncture" => 250,
                    _ => int.MaxValue
                }))
            .Select(choice => GameAction.Create(
                "event_choice", new EventChoicePayload(choice.Id)))
            .ToArray();
    }

    private static RunState StepLaterActV3Event(
        RunState state, string choiceId)
    {
        var world = RequireWorld(state);
        var evt = world.Event
            ?? throw new InvalidOperationException("Event state missing.");
        if (!GetLaterActV3Actions(state).Any(action =>
                action.ReadPayload<EventChoicePayload>().ChoiceId == choiceId))
            throw new InvalidOperationException(
                $"Event option '{choiceId}' is not currently available.");

        var player = state.Player;
        var nextId = world.NextCardInstanceId;
        var queued = new List<PrototypePendingEventDeckChoiceState>();
        var deferredHp = 0;

        if (evt.EventId == PrototypeNativeLaterActEventExpansionV3.SpiritGrafterId)
        {
            if (choiceId == "let_it_in")
            {
                player = player with
                {
                    Hp = Math.Min(player.MaxHp, player.Hp + 25)
                };
                player = AppendCard(player, nextId++,
                    PrototypeNativeLaterActEventExpansionV3.MetamorphosisId,
                    state.Rng);
            }
            else
            {
                queued.Add(new PrototypePendingEventDeckChoiceState(
                    choiceId, PrototypePersistentDeckChoiceKind.Upgrade,
                    1, []));
                deferredHp = 10;
            }
        }
        else if (evt.EventId ==
            PrototypeNativeLaterActEventExpansionV3.ZenWeaverId)
        {
            var cost = choiceId switch
            {
                "breathing_techniques" => 50,
                "emotional_awareness" => 125,
                "arachnid_acupuncture" => 250,
                _ => throw new InvalidOperationException(
                    "Invalid Zen Weaver option.")
            };
            player = player with { Gold = player.Gold - cost };
            if (choiceId == "breathing_techniques")
            {
                for (var i = 0; i < 2; i++)
                    player = AppendCard(player, nextId++,
                        PrototypeNativeLaterActEventExpansionV3.EnlightenmentId,
                        state.Rng);
            }
            else
                queued.Add(new PrototypePendingEventDeckChoiceState(
                    choiceId, PrototypePersistentDeckChoiceKind.Remove,
                    choiceId == "arachnid_acupuncture" ? 2 : 1, []));
        }
        else if (evt.EventId ==
            PrototypeNativeLaterActEventExpansionV3.HungryForMushroomsId)
        {
            if (choiceId == "big_mushroom")
            {
                player = ObtainLaterActEventRelic(player,
                    PrototypeNativeLaterActEventExpansionV3.BigMushroomId,
                    state.Rng);
                player = player with
                {
                    Hp = player.Hp + 20,
                    MaxHp = player.MaxHp + 20
                };
            }
            else
            {
                player = ObtainLaterActEventRelic(player,
                    PrototypeNativeLaterActEventExpansionV3.FragrantMushroomId,
                    state.Rng);
                player = player with
                {
                    Hp = Math.Max(0, player.Hp - 15)
                };
                if (player.Hp == 0)
                    return EndRun(state with { Player = player }, "defeat");
                var upgrades = player.Deck.Where(card =>
                    card.UpgradeLevel <
                        PrototypeContent.Card(card.CardId).MaxUpgradeLevel)
                    .Select(card => card.InstanceId)
                    .ToArray();
                PrototypeRng.Shuffle(state.Rng, "niche", upgrades);
                var chosen = upgrades.Take(2).ToHashSet();
                player = player with
                {
                    Deck = player.Deck.Select(card =>
                        chosen.Contains(card.InstanceId)
                        ? card with { UpgradeLevel = card.UpgradeLevel + 1 }
                        : card).ToArray()
                };
            }
        }

        return AdvanceEventContinuations(state with
        {
            Player = player,
            World = world with
            {
                NextCardInstanceId = nextId,
                Event = evt with
                {
                    ChosenChoiceId = choiceId,
                    QueuedDeckChoices = queued.ToArray(),
                    DeferredHpLoss = deferredHp
                }
            }
        });
    }
}
