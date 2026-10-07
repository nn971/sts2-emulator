using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeSilentCardCatalogTests
{
    private static readonly string[] CatalogOnlyNames =
    [
        "Blade of Ink",
        "Blade Symphony",
        "Concoct",
        "Fade",
        "Flanking",
        "Knife Trap",
        "Sneaky",
        "The Hunt",
        "Well-Laid Plans"
    ];

    private static readonly string[] MultiplayerOnlyNames =
    [
        "Blade Symphony",
        "Concoct",
        "Fade",
        "Flanking",
        "Sneaky"
    ];

    [Fact]
    public void NativeV01110SilentPoolHasExactly91UniqueCards()
    {
        Assert.Equal(91, PrototypeContent.NativeSilentCardPool.Length);
        Assert.Equal(
            91,
            PrototypeContent.NativeSilentCardPool
                .Distinct(StringComparer.Ordinal)
                .Count());

        foreach (var id in PrototypeContent.NativeSilentCardPool)
        {
            Assert.True(
                PrototypeContent.Cards.ContainsKey(id),
                $"Native Silent card '{id}' is missing from the prototype catalog.");
        }
    }

    [Fact]
    public void NewlyCataloguedCardsAreExplicitlyUnsupported()
    {
        var actual = PrototypeContent.NativeSilentCardPool
            .Select(PrototypeContent.Card)
            .Where(card => !card.MechanicsImplemented)
            .Select(card => card.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            CatalogOnlyNames.Order(StringComparer.Ordinal),
            actual);
    }

    [Fact]
    public void MultiplayerOnlyCardsAreTypedAndExcludedFromSinglePlayerRewards()
    {
        var multiplayer = PrototypeContent.NativeSilentCardPool
            .Select(PrototypeContent.Card)
            .Where(card => card.MultiplayerOnly)
            .Select(card => card.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            MultiplayerOnlyNames.Order(StringComparer.Ordinal),
            multiplayer);

        Assert.DoesNotContain(
            PrototypeContent.RewardCardPool,
            id => PrototypeContent.Card(id).MultiplayerOnly);
    }

    [Fact]
    public void RewardPoolContainsOnlyNativeImplementedSinglePlayerCards()
    {
        var native = PrototypeContent.NativeSilentCardPool
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(
            PrototypeContent.RewardCardPool,
            id =>
            {
                Assert.Contains(id, native);
                var card = PrototypeContent.Card(id);
                Assert.True(card.MechanicsImplemented);
                Assert.False(card.MultiplayerOnly);
            });
    }

    [Theory]
    [InlineData("proto.silent.quick_slash")]
    [InlineData("proto.silent.concentrate")]
    [InlineData("proto.silent.catalyst")]
    [InlineData("proto.silent.crippling_cloud")]
    [InlineData("proto.silent.die_die_die")]
    public void CompatibilityCardsStayOutsidePinnedNativePool(string id)
    {
        Assert.True(PrototypeContent.Cards.ContainsKey(id));
        Assert.DoesNotContain(id, PrototypeContent.NativeSilentCardPool);
        Assert.DoesNotContain(id, PrototypeContent.RewardCardPool);
    }
}
