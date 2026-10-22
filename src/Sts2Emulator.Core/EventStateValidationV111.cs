namespace Sts2Emulator.Core;

public sealed partial record EventState
{
    public void Validate(PlayerState player) => ValidateEvent(player, this);

    private static void ValidateEvent(
        PlayerState player,
        EventState eventState)
    {
        var definition =
            PrototypeContent.Event(
                eventState.EventId);
        eventState.Stochastic.ValidateFuturePotionOffers(
            eventState, player);

        if (eventState.OfferedChoiceIds is { } offered
            && (offered.Length == 0
                || offered.Distinct(StringComparer.Ordinal).Count()
                    != offered.Length
                || offered.Any(id =>
                    !definition.Choices.Any(choice =>
                        StringComparer.Ordinal.Equals(choice.Id, id)))))
        {
            throw new InvalidOperationException(
                "Event option offer list is empty, duplicated or unknown.");
        }

        if (eventState.EventId
                == PrototypeNativeOvergrowthEvents.NeowEventId
            && eventState.OfferedChoiceIds is { Length: not 3 })
        {
            throw new InvalidOperationException(
                "Neow must offer exactly three distinct choices.");
        }

        eventState.Stochastic.ValidateNativePotionSlot(eventState, player);

        if (eventState.DeferredHpLoss < 0)
        {
            throw new InvalidOperationException(
                "Event deferred HP loss cannot be negative.");
        }

        if (eventState.NativePageIndex < 0
            || (eventState.EventId ==
                    PrototypeNativeTabletOfTruth.EventId
                ? eventState.NativePageIndex > 4
                    && eventState.NativePageIndex !=
                        PrototypeNativeEventCompletionPages
                            .TerminalChoicePageIndex(eventState.EventId)
                : eventState.EventId ==
                    PrototypeNativeHiveEvents.WarHistorianRepyId
                    ? eventState.NativePageIndex > 2
                : eventState.EventId ==
                    PrototypeNativeGloryEvents.TinkerTimeId
                    ? eventState.NativePageIndex > 2
                : eventState.EventId ==
                    PrototypeNativeGloryEvents.BattlewornDummyId
                    ? eventState.NativePageIndex > 4
                : eventState.EventId == PrototypeNativeGloryEvents.TrialId
                    ? eventState.NativePageIndex > 6
                : eventState.EventId ==
                    PrototypeNativeGloryEvents.RoundTeaPartyId
                    ? eventState.NativePageIndex > 2
                    : eventState.EventId ==
                        PrototypeNativeHiveEvents.LanternKeyEventId
                        ? eventState.NativePageIndex > 3
                    : eventState.EventId ==
                        PrototypeNativeDenseVegetation.EventId
                        ? eventState.NativePageIndex > 3
                    : eventState.EventId ==
                        PrototypeNativeEndlessConveyor.EventId
                        ? eventState.NativePageIndex > 1
                            && eventState.NativePageIndex !=
                                PrototypeNativeEventCompletionPages
                                    .TerminalChoicePageIndex(
                                        eventState.EventId)
                        : eventState.EventId ==
                            PrototypeNativePunchOff.EventId
                            ? eventState.NativePageIndex > 2
                                && eventState.NativePageIndex !=
                                    PrototypeNativeEventCompletionPages
                                        .TerminalChoicePageIndex(
                                            eventState.EventId)
                            : PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                                eventState.EventId)
                                ? eventState.NativePageIndex >
                                    PrototypeNativeEventCompletionPages.TerminalChoicePageIndex(
                                        eventState.EventId)
                                : eventState.EventId ==
                                "proto.native.underdocks.abyssal_baths"
                                ? false
                                : eventState.NativePageIndex != 0))
        {
            throw new InvalidOperationException(
                "Native event page progress is out of range.");
        }

        if (eventState.NativePageIndex > 0
            && eventState.ChosenChoiceId is not null
            && eventState.EventId != PrototypeNativeGloryEvents.TrialId
            && eventState.EventId != PrototypeNativeGloryEvents.TinkerTimeId
            && eventState.EventId != PrototypeNativeGloryEvents.BattlewornDummyId
            && eventState.EventId != PrototypeNativeEndlessConveyor.EventId
            && eventState.EventId != PrototypeNativePunchOff.EventId
            && !PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                eventState.EventId)
            && eventState.EventId != "proto.native.underdocks.abyssal_baths")
        {
            throw new InvalidOperationException(
                "Native followup page cannot have a completed choice.");
        }

        if (PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                eventState.EventId)
            && eventState.NativePageIndex ==
                PrototypeNativeEventCompletionPages.TerminalChoicePageIndex(
                    eventState.EventId))
        {
            if (!PrototypeNativeEventCompletionPages.HasTerminalChoice(
                    eventState.EventId, eventState.ChosenChoiceId)
                || eventState.PendingDeckChoice is not null
                || eventState.PendingPotionReplacement is not null
                || eventState.RemainingDeckChoices.Length != 0
                || eventState.RemainingPotionIds.Length != 0
                || eventState.PendingReward is not null
                || eventState.DeferredHpLoss != 0
                || eventState.DeferredGoldLoss != 0
                || eventState.DeferredCardId is not null
                || eventState.Stochastic.RandomOutcomes.NativeDishRollPending)
                throw new InvalidOperationException(
                    "Native event completion page has unresolved or invalid state.");
            return;
        }

        eventState.Stochastic.ValidateNativeGoldRolls(eventState);

        if (eventState.Stochastic.ValidateNativeDishProgress(eventState))
        {
            ValidateConveyorEvent(player, eventState);
            return;
        }

        if (eventState.Stochastic.ValidateRepyChestOutcomes(eventState))
            return;

        if (eventState.EventId == "proto.native.underdocks.abyssal_baths")
        {
            if (eventState.ChosenChoiceId is not null
                || eventState.PendingDeckChoice is not null
                || eventState.PendingPotionReplacement is not null)
            {
                throw new InvalidOperationException(
                    "Abyssal Baths must remain on a resumable choice page.");
            }
            return;
        }

        if (eventState.EventId == PrototypeNativePunchOff.EventId
            && eventState.ChosenChoiceId is null
            && eventState.PendingDeckChoice is null
            && eventState.PendingPotionReplacement is null)
        {
            // Initial and challenge/fight pages have their own choices.
            return;
        }

        if (eventState.Stochastic.ValidateNativeTinkerType(eventState))
            return;

        if (eventState.EventId ==
                PrototypeNativeGloryEvents.BattlewornDummyId
            && eventState.NativePageIndex > 0
            && eventState.NativePageIndex <
                PrototypeNativeEventCompletionPages
                    .TerminalChoicePageIndex(eventState.EventId)
            && eventState.ChosenChoiceId is not null
            && eventState.PendingDeckChoice is null
            && eventState.PendingReward is null
            && eventState.RemainingDeckChoices.Length == 0)
        {
            // Event combat occupies Combat phase and retains the choice.
            return;
        }

        if (eventState.ChosenChoiceId is null)
        {
            eventState.Stochastic.ValidateDeferredCardOutcome(
                eventState, choice: null);
            if (eventState.PendingDeckChoice is not null
                || eventState.PendingPotionReplacement is not null
                || eventState.RemainingPotionIds.Length > 0
                || eventState.RemainingDeckChoices.Length > 0
                || eventState.PendingReward is not null
                || eventState.DeferredHpLoss != 0)
            {
                throw new InvalidOperationException(
                    "Pending event continuation has no chosen event option.");
            }

            return;
        }

        var choice = definition.Choices
            .FirstOrDefault(item =>
                StringComparer.Ordinal.Equals(
                    item.Id,
                    eventState.ChosenChoiceId))
            ?? throw new InvalidOperationException(
                "Event state references an unknown chosen option.");
        eventState.Stochastic.ValidateDeferredCardOutcome(
            eventState, choice);

        if (eventState.PendingReward is { } deferred)
        {
            if (!StringComparer.Ordinal.Equals(
                    deferred.SourceRoom, "Event:" + eventState.EventId))
            {
                throw new InvalidOperationException(
                    "Deferred event reward references a foreign source event.");
            }
        }

        var pending =
            eventState.PendingDeckChoice;
        var potionReplacement =
            eventState.PendingPotionReplacement;
        if (pending is null
            && potionReplacement is null
            && eventState.PendingReward is null)
        {
            throw new InvalidOperationException(
                "Resolved event choice should not remain in Event phase without an active continuation.");
        }

        if (pending is not null
            && potionReplacement is not null)
        {
            throw new InvalidOperationException(
                "An event cannot present two continuation prompts simultaneously.");
        }

        foreach (var queuedPotionId in eventState.RemainingPotionIds)
        {
            _ = PrototypeContent.Potion(queuedPotionId);
            if (!choice.Effects.Any(effect =>
                    (effect.Kind == PrototypeRunEffectKind.GainPotion
                        && StringComparer.Ordinal.Equals(
                            effect.PotionId, queuedPotionId))
                    || effect.Kind is PrototypeRunEffectKind.GainRandomPotion
                        or PrototypeRunEffectKind.GainRandomUncommonPotion))
            {
                throw new InvalidOperationException(
                    "Queued event potion has no matching source effect.");
            }
        }

        foreach (var queuedDeckChoice in eventState.RemainingDeckChoices)
        {
            if (!StringComparer.Ordinal.Equals(
                    queuedDeckChoice.ChoiceId,
                    choice.Id)
                || queuedDeckChoice.RemainingSelections <= 0)
            {
                throw new InvalidOperationException(
                    "Queued event deck continuation has invalid provenance or selections.");
            }

            if (queuedDeckChoice.SourceRelicId is { } sourceRelic)
            {
                if (!player.Relics.Any(relic =>
                        StringComparer.Ordinal.Equals(
                            relic.RelicId, sourceRelic)))
                {
                    throw new InvalidOperationException(
                        "Queued event deck continuation references an unowned relic.");
                }

                var sourceSpec = PrototypeContent.Relic(sourceRelic)
                    .AcquisitionDeckChoice
                    ?? throw new InvalidOperationException(
                        "Queued event deck continuation has no relic source specification.");
                if (sourceSpec.Kind != queuedDeckChoice.Kind
                    || sourceSpec.UpgradeTransformedCards
                        != queuedDeckChoice.UpgradeTransformedCards
                    || sourceSpec.EnchantmentKind != queuedDeckChoice.EnchantmentKind)
                {
                    throw new InvalidOperationException(
                        "Queued event deck continuation disagrees with its relic.");
                }
            }
            else if (choice.DeckChoice is not { } eventSpec
                || eventSpec.Kind != queuedDeckChoice.Kind
                || eventSpec.UpgradeTransformedCards
                    != queuedDeckChoice.UpgradeTransformedCards)
            {
                throw new InvalidOperationException(
                    "Queued event deck continuation disagrees with its source choice.");
            }
        }

        if (pending is not null)
        {
            PrototypePersistentDeckChoiceKind expectedKind;
            bool expectedUpgradeTransformedCards;
            PrototypeCardEnchantmentKind? expectedEnchantmentKind;
            string? expectedTransformToCardId;
            bool expectedBasicCardsOnly;
            bool expectedRequiresUpgradable;
            bool expectedPreserveUpgradeOnTransformation;
            bool expectedPreserveEnchantmentOnTransformation;
            int expectedMinimumSelections;

            if (pending.SourceRelicId is { } sourceRelicId)
            {
                if (!player.Relics.Any(relic =>
                        StringComparer.Ordinal.Equals(
                            relic.RelicId,
                            sourceRelicId)))
                {
                    throw new InvalidOperationException(
                        "Pending relic-sourced event deck choice references an unowned relic.");
                }

                var relicSpec =
                    PrototypeContent.Relic(
                        sourceRelicId)
                        .AcquisitionDeckChoice
                    ?? throw new InvalidOperationException(
                        "Pending relic-sourced event deck choice has no source specification.");
                expectedKind = relicSpec.Kind;
                expectedUpgradeTransformedCards =
                    relicSpec.UpgradeTransformedCards;
                expectedEnchantmentKind = relicSpec.EnchantmentKind;
                expectedTransformToCardId = relicSpec.TransformToCardId;
                expectedBasicCardsOnly = false;
                expectedRequiresUpgradable = relicSpec.RequiresUpgradable;
                expectedPreserveUpgradeOnTransformation =
                    relicSpec.PreserveUpgradeOnTransformation;
                expectedPreserveEnchantmentOnTransformation =
                    relicSpec.PreserveEnchantmentOnTransformation;
                expectedMinimumSelections = relicSpec.MinimumSelections
                    ?? relicSpec.Selections;
            }
            else
            {
                var spec = choice.DeckChoice
                    ?? throw new InvalidOperationException(
                        "Pending event deck choice source option has no deck-choice specification.");
                expectedKind = spec.Kind;
                expectedUpgradeTransformedCards =
                    spec.UpgradeTransformedCards;
                expectedEnchantmentKind = spec.EnchantmentKind;
                expectedTransformToCardId = spec.TransformToCardId;
                expectedBasicCardsOnly = spec.BasicCardsOnly;
                expectedRequiresUpgradable = false;
                expectedPreserveUpgradeOnTransformation = false;
                expectedPreserveEnchantmentOnTransformation = false;
                expectedMinimumSelections = spec.Selections;
            }

            var minimumSelections = pending.MinimumSelectionsRemaining
                ?? pending.RemainingSelections;
            if (!StringComparer.Ordinal.Equals(
                    pending.ChoiceId,
                    choice.Id)
                || pending.Kind != expectedKind
                || pending.UpgradeTransformedCards
                    != expectedUpgradeTransformedCards
                || pending.EnchantmentKind != expectedEnchantmentKind
                || pending.TransformToCardId != expectedTransformToCardId
                || pending.BasicCardsOnly != expectedBasicCardsOnly
                || pending.RequiresUpgradable != expectedRequiresUpgradable
                || pending.PreserveUpgradeOnTransformation
                    != expectedPreserveUpgradeOnTransformation
                || pending.PreserveEnchantmentOnTransformation
                    != expectedPreserveEnchantmentOnTransformation
                || minimumSelections > expectedMinimumSelections
                || minimumSelections < 0
                || minimumSelections > pending.RemainingSelections)
            {
                throw new InvalidOperationException(
                    "Pending event deck choice disagrees with its source.");
            }

            if (pending.RemainingSelections <= 0
                || pending.CandidateCardInstanceIds.Length
                    < pending.RemainingSelections)
            {
                throw new InvalidOperationException(
                    "Pending event deck choice has an invalid selection count.");
            }

            if (pending.CandidateCardInstanceIds.Length
                != pending.CandidateCardInstanceIds
                    .Distinct()
                    .Count())
            {
                throw new InvalidOperationException(
                    "Pending event deck choice contains duplicate card instances.");
            }

            foreach (var cardInstanceId in
                     pending.CandidateCardInstanceIds)
            {
                var card = player.Deck
                    .FirstOrDefault(item =>
                        item.InstanceId
                            == cardInstanceId)
                    ?? throw new InvalidOperationException(
                        $"Pending event deck choice references missing card {cardInstanceId}.");

                if (!PrototypeGameEngine.CanSelectEventDeckCard(
                        card, pending.Kind,
                        pending.TransformToCardId,
                        pending.EnchantmentKind,
                        pending.BasicCardsOnly))
                {
                    throw new InvalidOperationException(
                        "Pending event deck choice includes an ineligible card.");
                }
                if (pending.RequiresUpgradable
                    && card.UpgradeLevel >= PrototypeContent.Card(card.CardId).MaxUpgradeLevel)
                    throw new InvalidOperationException("Pending event deck choice includes a non-upgradable card.");
                if (pending.RequiredCardType is { } requiredCardType
                    && PrototypeContent.Card(card.CardId).Type != requiredCardType)
                    throw new InvalidOperationException("Pending event deck choice includes a card of the wrong type.");
                if (pending.EnchantmentAmount <= 0)
                    throw new InvalidOperationException("Pending event enchant amount must be positive.");
            }
        }

        if (potionReplacement is not null)
        {
            if (!StringComparer.Ordinal.Equals(
                    potionReplacement.ChoiceId,
                    choice.Id))
            {
                throw new InvalidOperationException(
                    "Pending event potion replacement disagrees with its source option.");
            }

            if (!choice.Effects.Any(effect =>
                    (effect.Kind
                        == PrototypeRunEffectKind.GainPotion
                        && StringComparer.Ordinal.Equals(
                            effect.PotionId,
                            potionReplacement.PotionId))
                    || effect.Kind
                        is PrototypeRunEffectKind.GainRandomPotion
                            or PrototypeRunEffectKind.GainRandomUncommonPotion))
            {
                throw new InvalidOperationException(
                    "Pending event potion replacement has no matching acquisition effect.");
            }

            _ = PrototypeContent.Potion(
                potionReplacement.PotionId);
            if (potionReplacement.CandidateSlots.Length == 0
                || potionReplacement.CandidateSlots.Length
                    != potionReplacement.CandidateSlots
                        .Distinct()
                        .Count())
            {
                throw new InvalidOperationException(
                    "Pending event potion replacement has invalid slot candidates.");
            }

            if (PrototypeNativeOvergrowthEvents.UsesNativePotionOffer(eventState.EventId)
                || PrototypeNativeUnderdocksEvents.UsesNativePotionOffer(eventState.EventId))
            {
                var firstEmpty = Array.IndexOf(player.PotionSlots, null);
                var expectedSlots = firstEmpty >= 0
                    ? new[] { firstEmpty }
                    : Enumerable.Range(0, player.PotionSlots.Length).ToArray();
                if (!potionReplacement.CandidateSlots.SequenceEqual(expectedSlots))
                {
                    throw new InvalidOperationException(
                        "Native event potion offer candidates disagree with available slots.");
                }
            }

            foreach (var slot in potionReplacement.CandidateSlots)
            {
                if (slot < 0
                    || slot >= player.PotionSlots.Length
                    || (player.PotionSlots[slot] is null
                        && !PrototypeNativeOvergrowthEvents.UsesNativePotionOffer(eventState.EventId)
                        && !PrototypeNativeUnderdocksEvents.UsesNativePotionOffer(eventState.EventId)))
                {
                    throw new InvalidOperationException(
                        $"Pending event potion reward references invalid slot {slot}.");
                }
            }
        }
    }

    private static void ValidateConveyorEvent(
        PlayerState player, EventState evt)
    {
        if (evt.NativePageIndex == 0)
        {
            if (evt.Stochastic.RandomOutcomes.NativeDishCount != 1
                || evt.ChosenChoiceId is not null
                || evt.Stochastic.RandomOutcomes.NativeDishRollPending)
            {
                throw new InvalidOperationException(
                    "Endless Conveyor initial page is inconsistent.");
            }
        }
        else if (evt.ChosenChoiceId != "grab")
        {
            throw new InvalidOperationException(
                "Endless Conveyor followup requires a prior grab.");
        }

        if (evt.PendingDeckChoice is not null
            && evt.PendingPotionReplacement is not null)
        {
            throw new InvalidOperationException(
                "Endless Conveyor has two active selection prompts.");
        }
        if (evt.Stochastic.RandomOutcomes.NativeDishRollPending
            && evt.PendingDeckChoice is null
            && evt.PendingPotionReplacement is null
            && evt.RemainingDeckChoices.Length == 0
            && evt.RemainingPotionIds.Length == 0)
        {
            throw new InvalidOperationException(
                "Endless Conveyor has an orphaned pending dish roll.");
        }
        if ((evt.PendingDeckChoice is not null
            || evt.PendingPotionReplacement is not null
            || evt.RemainingDeckChoices.Length > 0
            || evt.RemainingPotionIds.Length > 0)
            && !evt.Stochastic.RandomOutcomes.NativeDishRollPending)
        {
            throw new InvalidOperationException(
                "Endless Conveyor selection lacks a deferred dish roll.");
        }

        if (evt.PendingDeckChoice is { } deckChoice)
        {
            if (deckChoice.ChoiceId != "grab"
                || deckChoice.Kind != PrototypePersistentDeckChoiceKind.Transform
                || deckChoice.RemainingSelections != 1
                || deckChoice.CandidateCardInstanceIds.Length == 0
                || deckChoice.CandidateCardInstanceIds
                    .Distinct().Count() !=
                    deckChoice.CandidateCardInstanceIds.Length
                || deckChoice.CandidateCardInstanceIds.Any(id =>
                    !player.Deck.Any(card => card.InstanceId == id)))
            {
                throw new InvalidOperationException(
                    "Endless Conveyor Jelly Liver selection is invalid.");
            }
        }

        if (evt.PendingPotionReplacement is { } potion)
        {
            var firstEmpty = Array.IndexOf(player.PotionSlots, null);
            var expected = firstEmpty >= 0
                ? new[] { firstEmpty }
                : Enumerable.Range(0, player.PotionSlots.Length).ToArray();
            if (potion.ChoiceId != "grab"
                || !potion.CandidateSlots.SequenceEqual(expected))
            {
                throw new InvalidOperationException(
                    "Endless Conveyor potion offer has invalid slots.");
            }
            _ = PrototypeContent.Potion(potion.PotionId);
        }

        foreach (var queued in evt.RemainingPotionIds)
        {
            _ = PrototypeContent.Potion(queued);
        }
        foreach (var queued in evt.RemainingDeckChoices)
        {
            if (queued.ChoiceId != "grab"
                || queued.Kind != PrototypePersistentDeckChoiceKind.Transform)
            {
                throw new InvalidOperationException(
                    "Endless Conveyor queued transformation is invalid.");
            }
        }
    }
}
