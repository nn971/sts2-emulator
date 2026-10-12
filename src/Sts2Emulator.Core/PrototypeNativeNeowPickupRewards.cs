namespace Sts2Emulator.Core;

/// <summary>
/// Runs Neow relic pickup continuations after a relic is selected from a
/// reward. Opening choices already run their effects in the event resolver;
/// this path covers nested relic rewards such as Neow's Bones.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private static RunState ApplyNeowRelicPickupRewards(
        RunState before,
        RunState after)
    {
        if (before.Phase != RunPhase.Reward
            || before.World?.Event?.EventId
                != PrototypeNativeOvergrowthEvents.NeowEventId
            || after.Phase != RunPhase.Reward
            || after.World?.Event?.EventId
                != PrototypeNativeOvergrowthEvents.NeowEventId
            || after.Player.Relics.Length <= before.Player.Relics.Length)
        {
            return after;
        }

        var acquiredRelicId = after.Player.Relics[^1].RelicId;
        if (!acquiredRelicId.StartsWith(
                "proto.native.neow.", StringComparison.Ordinal))
        {
            return after;
        }

        var choiceId = "take_" + acquiredRelicId;
        var choice = PrototypeNativeOvergrowthEvents.NeowDefinition.Choices
            .SingleOrDefault(candidate => candidate.Id == choiceId);
        if (choice is null || choice.Effects.Length <= 1)
        {
            return after;
        }

        var player = after.Player;
        var world = RequireWorld(after);
        var nextId = world.NextCardInstanceId;
        RewardState? nestedReward = null;
        var additionalRelicIds = new List<string>();
        var cardsAfterReward = new List<string>();

        foreach (var effect in choice.Effects.Skip(1))
        {
            if (effect.Kind is PrototypeRunEffectKind.OfferThreeRareCards
                or PrototypeRunEffectKind.OfferThreeCardsAndPotion
                or PrototypeRunEffectKind.OfferFiveClassCards
                or PrototypeRunEffectKind.OfferThreeColorlessCards
                or PrototypeRunEffectKind.OfferTwoColorlessCards
                or PrototypeRunEffectKind.OfferCardBundles
                or PrototypeRunEffectKind.OfferNeowsBonesRelics
                or PrototypeRunEffectKind.OfferRandomRelic
                or PrototypeRunEffectKind.OfferWaxRelics)
            {
                if (nestedReward is not null || additionalRelicIds.Count > 0)
                {
                    throw new NotSupportedException(
                        "A Neow relic pickup cannot queue multiple reward groups yet.");
                }

                nestedReward = CreateDeferredEventReward(
                    after, effect.Kind, ref player,
                    PrototypeNativeOvergrowthEvents.NeowEventId,
                    ref world);
                continue;
            }

            if (effect.Kind == PrototypeRunEffectKind.GainRandomRelic)
            {
                string relicId;
                if (UsesNativeRegionalSystems(world))
                {
                    var draw = PrototypeNativeRelicGrabBag.Draw(
                        world, player, after.Rng, merchant: false);
                    world = draw.World;
                    relicId = draw.Id ?? throw new InvalidOperationException(
                        "Native Neow relic draw did not produce a result.");
                }
                else
                {
                    relicId = PickUnownedEventRelic(player, after.Rng);
                }
                additionalRelicIds.Add(relicId);
                continue;
            }

            if (effect.Kind == PrototypeRunEffectKind.AddCard)
            {
                cardsAfterReward.Add(effect.CardId
                    ?? throw new InvalidOperationException(
                        "Neow relic pickup card grant requires a card ID."));
                continue;
            }

            // Precarious Shears' delayed damage is attached to its
            // acquisition deck choice in CreateRelicDeckChoice.
            if (effect.Kind == PrototypeRunEffectKind.LoseHpAfterDeckChoices)
                continue;

            throw new NotSupportedException(
                $"Neow relic pickup effect '{effect.Kind}' is not implemented.");
        }

        if (additionalRelicIds.Count > 0)
        {
            if (nestedReward is not null)
            {
                throw new NotSupportedException(
                    "A Neow relic pickup cannot combine relic and custom reward groups.");
            }

            nestedReward = new RewardState(
                SourceRoom: "Event:" + PrototypeNativeOvergrowthEvents.NeowEventId,
                CardOptions: [],
                PotionOption: null,
                RelicOption: null,
                CardResolved: true,
                PotionResolved: true,
                RelicResolved: true,
                EndsAct: false,
                AddCardsAfterReward: cardsAfterReward.Count == 0
                    ? null : cardsAfterReward.ToArray(),
                ExtraRelicRewardIds: additionalRelicIds.ToArray(),
                IndependentSelection: true,
                ExtraCardGroupsResolved: [],
                ExtraRelicGroupsResolved:
                    new bool[additionalRelicIds.Count]);
        }
        else if (nestedReward is not null && cardsAfterReward.Count > 0)
        {
            nestedReward = nestedReward with
            {
                AddCardsAfterReward = cardsAfterReward.ToArray()
            };
        }
        else
        {
            foreach (var cardId in cardsAfterReward)
            {
                player = AppendCard(player, nextId++, cardId, after.Rng);
            }
        }

        if (nestedReward is null)
        {
            return after with
            {
                Player = player,
                World = world with { NextCardInstanceId = nextId }
            };
        }

        var returnState = new PrototypeCustomRewardReturnState(
            after.Phase, world.Reward, world.CustomRewardReturn);
        return after with
        {
            Player = player,
            Phase = RunPhase.Reward,
            World = world with
            {
                NextCardInstanceId = nextId,
                Reward = nestedReward,
                CustomRewardReturn = returnState
            }
        };
    }
}
