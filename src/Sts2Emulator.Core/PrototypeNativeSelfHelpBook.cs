namespace Sts2Emulator.Core;

/// <summary>
/// Shared event that applies one of the three native card enchantments.
/// Disabled choices remain catalogued but are omitted from legal actions.
/// </summary>
public static class PrototypeNativeSelfHelpBook
{
    public const string EventId = "proto.native.event.self_help_book";

    public const string SharpChoiceId = "read_the_back";
    public const string NimbleChoiceId = "read_passage";
    public const string SwiftChoiceId = "read_entire_book";
    public const string NoOptionsChoiceId = "no_options";

    public static PrototypeEventDefinition Definition =>
        PrototypeDefinitionCopiesV111.CloneEventDefinition(
            DefinitionValue);

    private static readonly PrototypeEventDefinition DefinitionValue = new(
        EventId,
        "Self-Help Book",
        [
            new(
                SharpChoiceId,
                "Read the Back",
                [],
                new PrototypeEventDeckChoiceSpec(
                    PrototypePersistentDeckChoiceKind.Enchant,
                    1,
                    EnchantmentKind: PrototypeCardEnchantmentKind.Sharp,
                    RequiredCardType: PrototypeCardType.Attack,
                    EnchantmentAmount: 2)),
            new(
                NimbleChoiceId,
                "Read a Random Passage",
                [],
                new PrototypeEventDeckChoiceSpec(
                    PrototypePersistentDeckChoiceKind.Enchant,
                    1,
                    EnchantmentKind: PrototypeCardEnchantmentKind.Nimble,
                    RequiredCardType: PrototypeCardType.Skill,
                    EnchantmentAmount: 2)),
            new(
                SwiftChoiceId,
                "Read the Entire Book",
                [],
                new PrototypeEventDeckChoiceSpec(
                    PrototypePersistentDeckChoiceKind.Enchant,
                    1,
                    EnchantmentKind: PrototypeCardEnchantmentKind.Swift,
                    RequiredCardType: PrototypeCardType.Power,
                    EnchantmentAmount: 2)),
            new("read_the_back_locked", "Locked", []),
            new("read_passage_locked", "Locked", []),
            new("read_entire_book_locked", "Locked", []),
            new(NoOptionsChoiceId, "Move On", [])
        ],
        MinAct: 1,
        MaxAct: 3);

    public static bool IsChoiceAvailable(
        PlayerState player,
        string choiceId) => choiceId switch
    {
        SharpChoiceId => HasEligibleCard(
            player, PrototypeCardType.Attack,
            PrototypeCardEnchantmentKind.Sharp),
        NimbleChoiceId => HasEligibleCard(
            player, PrototypeCardType.Skill,
            PrototypeCardEnchantmentKind.Nimble),
        SwiftChoiceId => HasEligibleCard(
            player, PrototypeCardType.Power,
            PrototypeCardEnchantmentKind.Swift),
        "read_the_back_locked" or "read_passage_locked"
            or "read_entire_book_locked" => false,
        NoOptionsChoiceId => !HasEligibleCard(
                player, PrototypeCardType.Attack,
                PrototypeCardEnchantmentKind.Sharp)
            && !HasEligibleCard(
                player, PrototypeCardType.Skill,
                PrototypeCardEnchantmentKind.Nimble)
            && !HasEligibleCard(
                player, PrototypeCardType.Power,
                PrototypeCardEnchantmentKind.Swift),
        _ => false
    };

    private static bool HasEligibleCard(
        PlayerState player,
        PrototypeCardType cardType,
        PrototypeCardEnchantmentKind enchantmentKind) =>
        player.Deck.Any(card =>
            PrototypeContent.Card(card.CardId).Type == cardType
            && PrototypeGameEngine.CanSelectEventDeckCard(
                card,
                PrototypePersistentDeckChoiceKind.Enchant,
                null,
                enchantmentKind,
                basicCardsOnly: false));
}
