namespace Sts2Emulator.Core;

/// <summary>
/// Event-scoped outcomes and draw-relevant continuation values already
/// committed by a provider. They remain available to mechanics through the
/// flattened EventState compatibility properties.
/// </summary>
public sealed record EventCommittedRandomStateV111(
    string? DeferredCardId = null,
    int NativeEventGold = 0,
    int NativeEventSecondaryGold = 0,
    int? NativeEventPotionSlot = null,
    string? NativeDishId = null,
    string? NativeLastDishId = null,
    int NativeDishCount = 0,
    bool NativeDishRollPending = false,
    string[]? RepyChestPotionIds = null,
    string[]? RepyChestRelicIds = null,
    string? NativeTinkerType = null,
    PrototypeFuturePotionOffer[]? FuturePotionOffers = null)
{
    private string[]? _repyChestPotionIds = Copy(RepyChestPotionIds);
    private string[]? _repyChestRelicIds = Copy(RepyChestRelicIds);
    private PrototypeFuturePotionOffer[]? _futurePotionOffers =
        Copy(FuturePotionOffers);

    public string[]? RepyChestPotionIds
    {
        get => Copy(_repyChestPotionIds);
        init => _repyChestPotionIds = Copy(value);
    }

    public string[]? RepyChestRelicIds
    {
        get => Copy(_repyChestRelicIds);
        init => _repyChestRelicIds = Copy(value);
    }

    public PrototypeFuturePotionOffer[]? FuturePotionOffers
    {
        get => Copy(_futurePotionOffers);
        init => _futurePotionOffers = Copy(value);
    }

    public EventCommittedRandomStateV111 Fork() => this with
    {
        RepyChestPotionIds = _repyChestPotionIds,
        RepyChestRelicIds = _repyChestRelicIds,
        FuturePotionOffers = _futurePotionOffers
    };

    private static string[]? Copy(string[]? values) =>
        values is null ? null : (string[])values.Clone();

    private static PrototypeFuturePotionOffer[]? Copy(
        PrototypeFuturePotionOffer[]? values) =>
        values is null ? null : (PrototypeFuturePotionOffer[])values.Clone();
}

/// <summary>
/// Provider-private state for an active event: its context stream, selected
/// provider mode, suspended controlled random-choice continuation, and
/// committed event-local random outcomes.
/// </summary>
public sealed record EventStochasticStateV111(
    RngStreamState? NativeRng = null,
    string? ProviderMode = null,
    PrototypeControlledEventChoiceContinuationV111? ControlledChoice = null,
    EventCommittedRandomStateV111? CommittedRandom = null)
{
    private PrototypeControlledEventChoiceContinuationV111?
        _controlledChoice = ControlledChoice?.Fork();

    public PrototypeControlledEventChoiceContinuationV111? ControlledChoice
    {
        get => _controlledChoice?.Fork();
        init => _controlledChoice = value?.Fork();
    }

    public EventCommittedRandomStateV111 RandomOutcomes =>
        CommittedRandom ?? new EventCommittedRandomStateV111();

    public void ValidateFuturePotionOffers(
        EventState owner, PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(player);
        if (owner.EventId == PrototypeNativeFutureOfPotions.EventId)
        {
            if (owner.NativePageIndex == 0
                && owner.ChosenChoiceId is null)
            {
                var owned = player.PotionSlots
                    .Select((potion, slot) => (Potion: potion, Slot: slot))
                    .Where(item => item.Potion is not null)
                    .ToArray();
                var expected = owned.Take(3).ToArray();
                var offers = RandomOutcomes.FuturePotionOffers;
                if (owned.Length < 2
                    || offers is null
                    || offers.Length != expected.Length
                    || offers.Select(offer => offer.Slot).Distinct().Count()
                        != offers.Length
                    || offers.Where((offer, index) =>
                        offer.Slot != expected[index].Slot
                        || offer.PotionId != expected[index].Potion!.PotionId
                        || !PrototypeNativeFutureOfPotions.TryGetCardRarity(
                            offer.PotionId, out var rarity)
                        || offer.CardRarity != rarity
                        || offer.CardType is not (PrototypeCardType.Attack
                            or PrototypeCardType.Skill
                            or PrototypeCardType.Power)
                        || offer.CardRarity == PrototypeCardRarity.Common
                            && offer.CardType == PrototypeCardType.Power)
                        .Any())
                {
                    throw new InvalidOperationException(
                        "The Future of Potions opening offers are invalid.");
                }
            }
            else if (RandomOutcomes.FuturePotionOffers is not null)
            {
                throw new InvalidOperationException(
                    "The Future of Potions retained offers after the player traded a potion.");
            }
        }
        else if (RandomOutcomes.FuturePotionOffers is not null)
        {
            throw new InvalidOperationException(
                "Future of Potions offers leaked into another event.");
        }
    }

    public void ValidateNativeGoldRolls(EventState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var outcomes = RandomOutcomes;
        if (outcomes.NativeEventGold != 0
            && (owner.EventId switch
            {
                PrototypeNativeDenseVegetation.EventId =>
                    outcomes.NativeEventGold is >= 61 and <= 99,
                "proto.native.event.whispering_hollow" =>
                    outcomes.NativeEventGold is >= 26 and <= 44,
                "proto.native.event.sunken_statue" =>
                    outcomes.NativeEventGold is >= 101 and <= 121,
                "proto.native.event.luminous_choir" =>
                    outcomes.NativeEventGold is >= 100 and <= 149,
                "proto.native.event.jungle_maze_adventure" =>
                    outcomes.NativeEventGold is >= 135 and < 165,
                "proto.native.underdocks.sunken_statue" =>
                    outcomes.NativeEventGold is >= 101 and <= 121,
                "proto.native.underdocks.sunken_treasury" =>
                    outcomes.NativeEventGold is >= 52 and <= 67,
                PrototypeNativeHiveEvents.LostWispId =>
                    outcomes.NativeEventGold is >= 45 and <= 75,
                PrototypeNativePunchOff.EventId =>
                    outcomes.NativeEventGold is >= 91 and <= 98,
                PrototypeNativeSharedEvents.ThisOrThatId =>
                    outcomes.NativeEventGold is >= 41 and <= 68,
                _ => false
            }) == false)
        {
            throw new InvalidOperationException(
                "Native event gold roll is invalid.");
        }

        if (outcomes.NativeEventSecondaryGold != 0
            && !(owner.EventId switch
            {
                "proto.native.event.jungle_maze_adventure" =>
                    outcomes.NativeEventSecondaryGold is >= 35 and < 65,
                "proto.native.underdocks.sunken_treasury" =>
                    outcomes.NativeEventSecondaryGold is >= 303 and <= 363,
                _ => false
            }))
        {
            throw new InvalidOperationException(
                "Native event secondary gold roll is invalid.");
        }

        if (owner.EventId == "proto.native.event.jungle_maze_adventure"
            && (outcomes.NativeEventGold == 0)
                != (outcomes.NativeEventSecondaryGold == 0))
        {
            throw new InvalidOperationException(
                "Jungle Maze must preserve both independent gold rolls.");
        }

        if (owner.EventId == "proto.native.underdocks.sunken_treasury"
            && (outcomes.NativeEventGold == 0)
                != (outcomes.NativeEventSecondaryGold == 0))
        {
            throw new InvalidOperationException(
                "Sunken Treasury must preserve both independent gold rolls.");
        }
    }

    public void ValidateNativePotionSlot(
        EventState owner, PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(player);
        if (owner.EventId == PrototypeNativeStoneOfAllTime.EventId)
        {
            if (RandomOutcomes.NativeEventPotionSlot is { } potionSlot
                && (potionSlot < 0
                    || potionSlot >= player.PotionSlots.Length
                    || player.PotionSlots[potionSlot] is null))
            {
                throw new InvalidOperationException(
                    "Stone of All Time's saved potion slot is invalid.");
            }

            if (owner.ChosenChoiceId == "lift"
                ? RandomOutcomes.NativeEventPotionSlot is not null
                : RandomOutcomes.NativeEventPotionSlot is null)
            {
                throw new InvalidOperationException(
                    "Stone of All Time's potion choice state is inconsistent.");
            }
        }
        else if (RandomOutcomes.NativeEventPotionSlot is not null)
        {
            throw new InvalidOperationException(
                "A non-Stone event cannot retain a selected potion slot.");
        }
    }

    public bool ValidateNativeTinkerType(EventState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.EventId == PrototypeNativeGloryEvents.TinkerTimeId)
        {
            var expected = owner.NativePageIndex switch
            {
                0 => 0,
                1 or 2 => 2,
                _ => throw new InvalidOperationException(
                    "Tinker event page is out of range.")
            };
            if (owner.ChosenChoiceId is not null
                || (owner.OfferedChoiceIds?.Length ?? 0) != expected
                || (owner.NativePageIndex == 2
                    ? RandomOutcomes.NativeTinkerType is not
                        ("attack" or "skill" or "power")
                    : RandomOutcomes.NativeTinkerType is not null))
                throw new InvalidOperationException(
                    "Tinker selected type and choice offers are inconsistent.");
            return true;
        }
        if (RandomOutcomes.NativeTinkerType is not null)
            throw new InvalidOperationException(
                "Tinker-only state leaked into another event.");
        return false;
    }

    public bool ValidateNativeDishProgress(EventState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var outcomes = RandomOutcomes;
        if (owner.EventId == PrototypeNativeEndlessConveyor.EventId)
        {
            string[] dishes =
            [
                "CAVIAR", "SPICY_SNAPPY", "JELLY_LIVER", "FRIED_EEL",
                "SUSPICIOUS_CONDIMENT", "CLAM_ROLL", "GOLDEN_FYSH",
                "SEAPUNK_SALAD"
            ];
            if (outcomes.NativeDishCount < 1
                || !dishes.Contains(
                    outcomes.NativeDishId, StringComparer.Ordinal)
                || outcomes.NativeDishId != outcomes.NativeLastDishId
                || (outcomes.NativeDishCount % 5 == 0
                    && outcomes.NativeDishId != "SEAPUNK_SALAD")
                || (outcomes.NativeDishCount % 5 != 0
                    && outcomes.NativeDishId == "SEAPUNK_SALAD"))
            {
                throw new InvalidOperationException(
                    "Endless Conveyor dish progression is inconsistent.");
            }
            return true;
        }

        if (outcomes.NativeDishId is not null
            || outcomes.NativeLastDishId is not null
            || outcomes.NativeDishCount != 0
            || outcomes.NativeDishRollPending)
            throw new InvalidOperationException(
                "Conveyor-specific state leaked into another event.");
        return false;
    }

    public bool ValidateRepyChestOutcomes(EventState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var outcomes = RandomOutcomes;
        if (owner.EventId == PrototypeNativeHiveEvents.WarHistorianRepyId)
        {
            if (owner.NativePageIndex == 0
                    && owner.RepyFirstChoiceId is not null
                || owner.NativePageIndex > 0
                    && owner.RepyFirstChoiceId is not
                        ("unlock_chest" or "unlock_cage")
                || (outcomes.RepyChestPotionIds is null)
                    != (outcomes.RepyChestRelicIds is null)
                || owner.RepyChestGroupsResolved is < 0 or > 1
                || outcomes.RepyChestPotionIds is null
                    && owner.RepyChestGroupsResolved != 0
                || outcomes.RepyChestPotionIds is { } potions
                    && (potions.Length != 2
                        || potions.Any(id =>
                            !PrototypeContent.Potions.ContainsKey(id)))
                || outcomes.RepyChestRelicIds is { } relics
                    && (relics.Length != 2
                        || relics.Distinct(StringComparer.Ordinal).Count() != 2
                        || relics.Any(id =>
                            !PrototypeContent.Relics.ContainsKey(id))))
                throw new InvalidOperationException(
                    "War Historian Repy has invalid page or chest reward state.");
            return true;
        }
        if (owner.RepyFirstChoiceId is not null
            || outcomes.RepyChestPotionIds is not null
            || outcomes.RepyChestRelicIds is not null
            || owner.RepyChestGroupsResolved != 0)
            throw new InvalidOperationException(
                "Repy-specific reward state leaked into another event.");
        return false;
    }

    public void ValidateDeferredCardOutcome(
        EventState owner, PrototypeEventChoiceDefinition? choice)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (RandomOutcomes.DeferredCardId is not { } deferredCardId)
            return;
        if (owner.ChosenChoiceId is null)
            throw new InvalidOperationException(
                "Pending event continuation has no chosen event option.");

        _ = PrototypeContent.Card(deferredCardId);
        if (choice is null
            || !choice.Effects.Any(effect =>
                effect.Kind == PrototypeRunEffectKind.AddCardAfterDeckChoices
                && StringComparer.Ordinal.Equals(
                    effect.CardId, deferredCardId)))
        {
            throw new InvalidOperationException(
                "Deferred card addition disagrees with source event choice.");
        }
    }

    public void ValidateControlledChoice(
        EventState owner, RunState run)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(run);
        if (ControlledChoice is not { } controlledChoice)
            return;

        controlledChoice.Validate();
        if (run.Phase != RunPhase.Event
            || run.DecisionIndex < controlledChoice.PartialState.DecisionIndex
            || owner.EventId != controlledChoice.EventId
            || (owner.ChosenChoiceId is not null
                && !(controlledChoice.EventId ==
                         PrototypeNativeStoneOfAllTime.EventId
                     && controlledChoice.ChoiceId == "push"
                     && owner.ChosenChoiceId == "push"
                     && owner.NativePageIndex == 0))
            || owner.PendingDeckChoice is not null
            || owner.PendingPotionReplacement is not null
            || owner.RemainingDeckChoices.Length != 0
            || owner.RemainingPotionIds.Length != 0
            || owner.PendingReward is not null)
            throw new InvalidOperationException(
                "Suspended controlled event choice conflicts with committed event state.");
    }

    public EventStochasticStateV111 Fork() => this with
    {
        NativeRng = NativeRng?.Fork(),
        ControlledChoice = _controlledChoice,
        CommittedRandom = CommittedRandom?.Fork()
    };

    internal static EventStochasticStateV111 WithNativeRng(
        EventStochasticStateV111? current, RngStreamState? nativeRng) =>
        (current ?? new EventStochasticStateV111()) with
        {
            NativeRng = nativeRng
        };

    internal static EventStochasticStateV111 WithProviderMode(
        EventStochasticStateV111? current, string? providerMode) =>
        (current ?? new EventStochasticStateV111()) with
        {
            ProviderMode = providerMode
        };

    internal static EventStochasticStateV111 WithControlledChoice(
        EventStochasticStateV111? current,
        PrototypeControlledEventChoiceContinuationV111? controlledChoice) =>
        (current ?? new EventStochasticStateV111()) with
        {
            ControlledChoice = controlledChoice
        };

    internal static EventStochasticStateV111 WithCommittedRandom(
        EventStochasticStateV111? current,
        EventCommittedRandomStateV111 committedRandom) =>
        (current ?? new EventStochasticStateV111()) with
        {
            CommittedRandom = committedRandom
        };
}
