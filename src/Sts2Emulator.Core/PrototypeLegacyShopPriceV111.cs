namespace Sts2Emulator.Core;

/// <summary>
/// Fixed price outcomes shared by legacy Shop generation and its Chance
/// Laws, keeping the indexed support identical at both boundaries.
/// </summary>
internal static class PrototypeLegacyShopPriceV111
{
    private static readonly int[] PotionOutcomeValues =
        Enumerable.Range(40, 21).ToArray();

    private static readonly int[] RelicOutcomeValues =
        Enumerable.Range(100, 41).ToArray();

    public static int[] PotionOutcomes =>
        (int[])PotionOutcomeValues.Clone();

    public static int[] RelicOutcomes =>
        (int[])RelicOutcomeValues.Clone();

    public static int[] CardOutcomes(string cardId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        return PrototypeContent.Card(cardId).Rarity switch
        {
            PrototypeCardRarity.Common =>
                Enumerable.Range(45, 16).ToArray(),
            PrototypeCardRarity.Uncommon =>
                Enumerable.Range(65, 21).ToArray(),
            PrototypeCardRarity.Rare =>
                Enumerable.Range(100, 31).ToArray(),
            _ => throw new InvalidOperationException(
                $"Basic card '{cardId}' cannot be a normal shop offer.")
        };
    }
}
