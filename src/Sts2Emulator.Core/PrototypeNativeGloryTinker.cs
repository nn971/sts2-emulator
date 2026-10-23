using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Pinned TinkerTime/MadScience semantics. One persistent card ID carries
/// native-like selected type and rider in the card's JSON payload, so
/// forks, transforms and copies preserve the player's customization.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    public const string ImprovementCardSiteId =
        "combat.glory_tinker.improvement_card.index_draw";

    private static readonly string[] TinkerTypes = ["attack", "skill", "power"];

    internal static string[] TinkerTypeOfferCandidates() =>
        (string[])TinkerTypes.Clone();

    internal static string[] TinkerRiderOfferCandidates(string type) =>
        TinkerRiders(type);

    internal static bool IsValidScriptedEventShuffleState(EventState evt)
    {
        if (evt.EventId == PrototypeNativeFakeMerchant.EventId)
        {
            var validChoices = PrototypeNativeFakeMerchant.StockRelicIds
                .Select(PrototypeNativeFakeMerchant.ChoiceId)
                .ToHashSet(StringComparer.Ordinal);
            return evt.NativePageIndex == 0 &&
                evt.ChosenChoiceId is null &&
                evt.OfferedChoiceIds is { Length: 7 } fakeOffers &&
                fakeOffers[^1] ==
                    PrototypeNativeFakeMerchant.LeaveChoiceId &&
                fakeOffers.Take(6).All(validChoices.Contains) &&
                fakeOffers.Take(6).Distinct(StringComparer.Ordinal).Count() == 6;
        }
        if (evt.EventId == PrototypeNativeGloryEvents.BattlewornDummyId)
            return evt.NativePageIndex ==
                    PrototypeNativeEventCompletionPages
                        .TerminalChoicePageIndex(evt.EventId) &&
                evt.ChosenChoiceId == "victory";
        if (evt.EventId == PrototypeNativeGloryEvents.ReflectionsId)
            return evt.NativePageIndex ==
                    PrototypeNativeEventCompletionPages
                        .TerminalChoicePageIndex(evt.EventId) &&
                evt.ChosenChoiceId == "touch_mirror" &&
                evt.OfferedChoiceIds is null;
        if (evt.EventId ==
            PrototypeNativeGloryEvents.HungryForMushroomsId)
            return evt.NativePageIndex ==
                    PrototypeNativeEventCompletionPages
                        .TerminalChoicePageIndex(evt.EventId) &&
                evt.ChosenChoiceId == "fragrant_mushroom" &&
                evt.OfferedChoiceIds is null;
        if (evt.EventId != PrototypeNativeGloryEvents.TinkerTimeId ||
            evt.OfferedChoiceIds is not { Length: 2 } offers ||
            offers.Distinct(StringComparer.Ordinal).Count() != offers.Length)
            return false;
        return evt.NativePageIndex switch
        {
            1 => evt.Stochastic.RandomOutcomes.NativeTinkerType is null &&
                offers.All(type => TinkerTypes.Contains(
                    type, StringComparer.Ordinal)),
            2 => evt.Stochastic.RandomOutcomes.NativeTinkerType is { } type &&
                TinkerTypes.Contains(type, StringComparer.Ordinal) &&
                offers.All(rider => TinkerRiders(type).Contains(
                    rider, StringComparer.Ordinal)),
            _ => false
        };
    }

    private static string[] TinkerRiders(string type) => type switch
    {
        "attack" => ["sapping", "violence", "choking"],
        "skill" => ["energized", "wisdom", "chaos"],
        "power" => ["expertise", "curious", "improvement"],
        _ => throw new InvalidOperationException(
            $"Unknown Tinker card type '{type}'.")
    };

    private static IReadOnlyList<GameAction> GetNativeTinkerActions(
        EventState evt)
    {
        string[] ids = evt.NativePageIndex switch
        {
            0 => ["choose_card_type"],
            1 or 2 => evt.OfferedChoiceIds
                ?? throw new InvalidOperationException(
                    "Tinker choice page has no persisted random offers."),
            _ => throw new InvalidOperationException(
                "Tinker Time has no further event pages.")
        };
        return ids.Select(id => GameAction.Create("event_choice",
            new EventChoicePayload(id))).ToArray();
    }

    private static RunState StepNativeTinker(RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        var evt = world.Event;
        if (world.Act != 3 ||
            evt?.EventId != PrototypeNativeGloryEvents.TinkerTimeId)
            throw new InvalidOperationException(
                "Tinker Time requires its Glory event fixture.");
        var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
        if (!GetNativeTinkerActions(evt).Any(a =>
                a.ReadPayload<EventChoicePayload>().ChoiceId == choice))
            throw new InvalidOperationException(
                $"Tinker choice '{choice}' is unavailable on this page.");

        if (evt.NativePageIndex == 0)
        {
            var choices = (string[])TinkerTypes.Clone();
            state = NativeEventContextRngV111.ShuffleInPlace(
                state,
                evt,
                PrototypeNativeGloryEvents.TinkerTimeNativeRngEventId,
                choices,
                PrototypeNativeGloryEvents.TinkerTypeOfferShuffleSiteId,
                "event:glory_tinker:types");
            world = RequireWorld(state);
            evt = world.Event!;
            return state with
            {
                World = world with
                {
                    Event = evt with
                    {
                        NativePageIndex = 1,
                        OfferedChoiceIds = choices.Take(2).ToArray()
                    }
                }
            };
        }

        if (evt.NativePageIndex == 1)
        {
            var riders = TinkerRiders(choice);
            state = NativeEventContextRngV111.ShuffleInPlace(
                state,
                evt,
                PrototypeNativeGloryEvents.TinkerTimeNativeRngEventId,
                riders,
                PrototypeNativeGloryEvents.TinkerRiderOfferShuffleSiteId,
                "event:glory_tinker:type:" + choice);
            world = RequireWorld(state);
            evt = world.Event!;
            return state with
            {
                World = world with
                {
                    Event = evt with
                    {
                        NativePageIndex = 2,
                        Stochastic =
                            EventStochasticStateV111.WithCommittedRandom(
                                evt.Stochastic,
                                evt.Stochastic.RandomOutcomes with
                                {
                                    NativeTinkerType = choice
                                }),
                        OfferedChoiceIds = riders.Take(2).ToArray()
                    }
                }
            };
        }

        if (evt.NativePageIndex != 2 ||
            evt.Stochastic.RandomOutcomes.NativeTinkerType is null)
            throw new InvalidOperationException(
                "Tinker Time has no rider selection in progress.");

        // Native MadScience stores TinkerTimeType and TinkerTimeRider
        // as saved card properties. One immutable JSON payload models
        // both and stays attached to the physical deck instance.
        var payload = JsonSerializer.SerializeToElement(new
        {
            tinkerType = evt.Stochastic.RandomOutcomes.NativeTinkerType,
            tinkerRider = choice
        });
        var added = new CardInstance(world.NextCardInstanceId,
            PrototypeNativeGloryEvents.MadScienceId, 0, payload);
        var player = state.Player with
        {
            Deck = state.Player.Deck.Append(added).ToArray()
        };
        var prototypeRng = state.Stochastic.PrototypeRng;
        player = ApplyRelicRunEvent(player,
            PrototypeRunEventKind.CardAdded, addedCard: added, rng: prototypeRng);
        var stochastic = RunStochasticStateV111.WithPrototypeRng(
            state.Stochastic, prototypeRng);
        return CompleteRoomToMap(state with
        {
            Player = player,
            Stochastic = stochastic,
            World = world with
            {
                NextCardInstanceId = world.NextCardInstanceId + 1
            }
        });
    }

    private static PrototypeCardDefinition RuntimeCardDefinition(
        CombatCardInstance card)
    {
        var model = PrototypeContent.Card(card.CardId);
        if (card.CardId != PrototypeNativeGloryEvents.MadScienceId)
            return model;
        return MadScienceDefinition(card.State);
    }

    internal static bool IsMadScienceDrawCard(
        CombatCardInstance card) =>
        card.CardId == PrototypeNativeGloryEvents.MadScienceId &&
        RuntimeCardDefinition(card).Effects.Any(effect =>
            effect.Kind == PrototypeCombatEffectKind.DrawCards);

    internal static bool IsSupportedMadScienceCardAction(
        CombatCardInstance card)
    {
        if (card.CardId != PrototypeNativeGloryEvents.MadScienceId)
            return false;
        var effects = RuntimeCardDefinition(card).Effects;
        return effects.Length > 0 &&
            effects.All(effect => effect.Kind is
                PrototypeCombatEffectKind.DamageEnemy or
                PrototypeCombatEffectKind.ApplyEnemyStatus or
                PrototypeCombatEffectKind.ApplyEnemyPower or
                PrototypeCombatEffectKind.GainPlayerBlock or
                PrototypeCombatEffectKind.GainEnergy or
                PrototypeCombatEffectKind.DrawCards or
                PrototypeCombatEffectKind.CreateRandomFreeCharacterCardInHand or
                PrototypeCombatEffectKind.ApplyPlayerPower);
    }

    internal static bool IsMadScienceChaosGenerationCard(
        CombatCardInstance card)
    {
        if (card.CardId != PrototypeNativeGloryEvents.MadScienceId)
            return false;
        var definition = RuntimeCardDefinition(card);
        var expectedEffects = new[]
        {
            new PrototypeCombatEffectSpec(
                PrototypeCombatEffectKind.GainPlayerBlock, 8),
            new PrototypeCombatEffectSpec(
                PrototypeCombatEffectKind.CreateRandomFreeCharacterCardInHand,
                1)
        };
        return definition.Type == PrototypeCardType.Skill &&
            CanonicalJson.Sha256(definition.Effects) ==
                CanonicalJson.Sha256(expectedEffects);
    }

    private static PrototypeCardDefinition MadScienceDefinition(
        JsonElement state)
    {
        if (state.ValueKind != JsonValueKind.Object
            || !state.TryGetProperty("tinkerType", out var typeValue)
            || !state.TryGetProperty("tinkerRider", out var riderValue))
            throw new InvalidOperationException(
                "Mad Science requires its saved Tinker type and rider.");

        var type = typeValue.GetString()
            ?? throw new InvalidOperationException("Missing Mad Science type.");
        var rider = riderValue.GetString()
            ?? throw new InvalidOperationException("Missing Mad Science rider.");
        if (!TinkerRiders(type).Contains(rider, StringComparer.Ordinal))
            throw new InvalidOperationException(
                $"Incompatible Mad Science rider '{rider}' for '{type}'.");

        var cardType = type switch
        {
            "attack" => PrototypeCardType.Attack,
            "skill" => PrototypeCardType.Skill,
            "power" => PrototypeCardType.Power,
            _ => throw new InvalidOperationException(
                "Invalid Mad Science type.")
        };
        var effects = type switch
        {
            "attack" => MadScienceAttack(rider),
            "skill" => MadScienceSkill(rider),
            "power" => MadSciencePower(rider),
            _ => throw new InvalidOperationException()
        };
        return PrototypeContent.Card(
            PrototypeNativeGloryEvents.MadScienceId) with
        {
            Type = cardType,
            Target = cardType == PrototypeCardType.Attack
                ? PrototypeCardTarget.Enemy : PrototypeCardTarget.None,
            Effects = effects
        };
    }

    private static PrototypeCombatEffectSpec[] MadScienceAttack(
        string rider)
    {
        var attack = new PrototypeCombatEffectSpec(
            PrototypeCombatEffectKind.DamageEnemy, 12,
            Repetitions: rider == "violence" ? 3 : 1);
        return rider switch
        {
            "sapping" =>
            [
                attack,
                new(PrototypeCombatEffectKind.ApplyEnemyStatus, 2,
                    StatusId: "proto.status.weak"),
                new(PrototypeCombatEffectKind.ApplyEnemyStatus, 2,
                    StatusId: "proto.status.vulnerable")
            ],
            "violence" => [attack],
            "choking" =>
            [
                attack,
                new(PrototypeCombatEffectKind.ApplyEnemyPower, 6,
                    PowerId: PrototypeNativeGloryEvents.StrangleId)
            ],
            _ => throw new InvalidOperationException()
        };
    }

    private static PrototypeCombatEffectSpec[] MadScienceSkill(
        string rider) => rider switch
    {
        "energized" =>
        [
            new(PrototypeCombatEffectKind.GainPlayerBlock, 8),
            new(PrototypeCombatEffectKind.GainEnergy, 2)
        ],
        "wisdom" =>
        [
            new(PrototypeCombatEffectKind.GainPlayerBlock, 8),
            new(PrototypeCombatEffectKind.DrawCards, 3)
        ],
        "chaos" =>
        [
            new(PrototypeCombatEffectKind.GainPlayerBlock, 8),
            new(PrototypeCombatEffectKind.CreateRandomFreeCharacterCardInHand, 1)
        ],
        _ => throw new InvalidOperationException()
    };

    private static PrototypeCombatEffectSpec[] MadSciencePower(
        string rider) => rider switch
    {
        "expertise" =>
        [
            new(PrototypeCombatEffectKind.ApplyPlayerPower, 2,
                PowerId: "proto.power.strength"),
            new(PrototypeCombatEffectKind.ApplyPlayerPower, 2,
                PowerId: "proto.power.dexterity")
        ],
        "curious" =>
        [
            new(PrototypeCombatEffectKind.ApplyPlayerPower, 1,
                PowerId: PrototypeNativeGloryEvents.CuriousId)
        ],
        "improvement" =>
        [
            new(PrototypeCombatEffectKind.ApplyPlayerPower, 1,
                PowerId: PrototypeNativeGloryEvents.ImprovementId)
        ],
        _ => throw new InvalidOperationException()
    };

    private static PlayerState ApplyMadScienceImprovement(
        PlayerState player, CombatState combat, RngBundle rng)
    {
        var amount = combat.PlayerPowers
            .Where(p => p.PowerId == PrototypeNativeGloryEvents.ImprovementId)
            .Sum(p => Math.Max(0, p.Stacks));
        var candidates = player.Deck.Where(card =>
                CanSelectEventDeckCard(card,
                    PrototypePersistentDeckChoiceKind.Upgrade,
                    null, null, false))
            .Select(card => card.InstanceId).ToList();
        var upgrades = new HashSet<long>();
        for (var i = 0; i < amount && candidates.Count > 0; i++)
        {
            var index = LegacyRandomChoiceV111.ChooseIndexRange(
                rng,
                ImprovementCardSiteId,
                "combat",
                $"mad_science_improvement:pick:{i}:pool:"
                    + CanonicalJson.Sha256(candidates),
                candidates.Count).SelectedIndex;
            upgrades.Add(candidates[index]);
            candidates.RemoveAt(index);
        }
        return upgrades.Count == 0 ? player : player with
        {
            Deck = player.Deck.Select(card =>
                upgrades.Contains(card.InstanceId)
                    ? card with { UpgradeLevel = card.UpgradeLevel + 1 }
                    : card).ToArray()
        };
    }

}
