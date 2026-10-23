namespace Sts2Emulator.Core;

/// <summary>
/// v0.111.0 Hive event: discard a random potion for 10 Max HP, or lose
/// 6 HP and enchant an Attack with Vigorous 8.
/// </summary>
public static class PrototypeNativeStoneOfAllTime
{
    public const string EventId = "proto.native.hive.stone_of_all_time";
    public const string NativeRngEventId = "STONE_OF_ALL_TIME";
    public const string PotionSlotSiteId =
        StochasticSiteRegistryV111.NativeStoneOfAllTimePotionSlotSiteId;
    public const string PushTicketSiteId =
        StochasticSiteRegistryV111.NativeStoneOfAllTimePushTicketSiteId;
    public const string LiftTicketSiteId =
        StochasticSiteRegistryV111.NativeStoneOfAllTimeLiftTicketSiteId;

    public static PrototypeEventDefinition Definition =>
        PrototypeDefinitionCopiesV111.CloneEventDefinition(
            DefinitionValue);

    private static readonly PrototypeEventDefinition DefinitionValue = new(
        EventId,
        "Stone of All Time",
        [
            new(
                "lift",
                "Discard a random potion and gain 10 Max HP",
                []),
            new(
                "push",
                "Lose 6 HP and enchant an Attack with Vigorous 8",
                [new(PrototypeRunEffectKind.LoseHp, 6)],
                new PrototypeEventDeckChoiceSpec(
                    PrototypePersistentDeckChoiceKind.Enchant,
                    1,
                    EnchantmentKind: PrototypeCardEnchantmentKind.Vigorous,
                    EnchantmentAmount: 8))
        ],
        MinAct: 2,
        MaxAct: 2,
        Weight: 0);

    public static int[] OccupiedPotionSlots(PlayerState player) =>
        player.PotionSlots
            .Select((potion, slot) => (Potion: potion, Slot: slot))
            .Where(item => item.Potion is not null)
            .Select(item => item.Slot)
            .ToArray();

    public static int RollPotionSlot(PlayerState player, RngBundle rng)
    {
        var slots = OccupiedPotionSlots(player);
        if (slots.Length == 0)
        {
            throw new InvalidOperationException(
                "Stone of All Time requires an owned potion.");
        }

        return slots[LegacyRandomChoiceV111.ChooseIndexRange(
            rng,
            PotionSlotSiteId,
            "event",
            "event:stone_of_all_time:potion_slots:"
                + CanonicalJson.Sha256(slots),
            slots.Length).SelectedIndex];
    }
}

public sealed partial class PrototypeGameEngine
{
    private static RunState StepNativeStoneOfAllTimeLift(RunState state)
    {
        var world = RequireWorld(state);
        var current = world.Event
            ?? throw new InvalidOperationException(
                "Stone of All Time has no event state.");
        if (current.EventId != PrototypeNativeStoneOfAllTime.EventId
            || current.NativePageIndex != 0
            || current.ChosenChoiceId is not null
            || current.Stochastic.RandomOutcomes.NativeEventPotionSlot is not
                { } slot
            || slot < 0
            || slot >= state.Player.PotionSlots.Length
            || state.Player.PotionSlots[slot] is null)
        {
            throw new InvalidOperationException(
                "Stone of All Time's saved potion is unavailable.");
        }

        var potions = (PotionInstance?[])state.Player.PotionSlots.Clone();
        potions[slot] = null;
        var result = state with
        {
            Player = state.Player with
            {
                PotionSlots = potions,
                Hp = state.Player.Hp + 10,
                MaxHp = state.Player.MaxHp + 10
            },
            World = world with
            {
                Event = current with
                {
                    Stochastic = EventStochasticStateV111.WithCommittedRandom(
                        current.Stochastic,
                        current.Stochastic.RandomOutcomes with
                        {
                            NativeEventPotionSlot = null
                        })
                }
            }
        };

        if (result.Stochastic.NativeRunRng is not null)
        {
            if (current.Stochastic.NativeRng is null)
                throw new InvalidOperationException(
                    "Native Stone of All Time has no saved event RNG.");
            var draw = NativeEventContextRngV111.ResolveBoundedInteger(
                result,
                result.World!.Event!,
                PrototypeNativeStoneOfAllTime.NativeRngEventId,
                LiftTicketSiteId,
                "event:stone_of_all_time:lift",
                100, ordinal: 1);
            result = draw.State;
        }
        else
        {
            var prototypeRng = result.Stochastic.PrototypeRng;
            _ = LegacyRandomChoiceV111.ChooseBoundedInteger(
                prototypeRng,
                LiftTicketSiteId,
                "event",
                "event:stone_of_all_time:lift",
                100);
            result = result with
            {
                Stochastic = RunStochasticStateV111.WithPrototypeRng(
                    result.Stochastic, prototypeRng)
            };
        }
        return PreserveNativeEventCompletionPage(result, "lift");
    }
}
