using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111RelicAcquisitionCallbacksTests
{
    [Fact]
    public void DuplicateOldCoinsGrantGoldOnlyForNewlyAcquiredInstance()
    {
        const string id = "proto.relic.old_coin";
        var player = PrototypeNativeUnderdocksRunFactory.Create(
            "duplicate-old-coin").Player with
        {
            Gold = 100,
            Relics = [Relic(id), Relic(id)]
        };
        var hash = CanonicalJson.Sha256(player);
        var gained = Dispatch(player, PrototypeRunEventKind.RelicAcquired, id);
        Assert.Equal(400, gained.Gold);
        Assert.Equal(2, gained.Relics.Length);
        Assert.Equal(hash, CanonicalJson.Sha256(player));
        Assert.Equal(CanonicalJson.Sha256(gained),
            CanonicalJson.Sha256(Dispatch(player,
                PrototypeRunEventKind.RelicAcquired, id)));
    }

    [Fact]
    public void DuplicateMangoesOnlyApplyTheNewRelicMaxHpBonus()
    {
        const string id = "proto.relic.mango";
        var player = PrototypeNativeUnderdocksRunFactory.Create(
            "duplicate-mango").Player with
        {
            Hp = 40, MaxHp = 70,
            Relics = [Relic(id), Relic(id)]
        };
        var gained = Dispatch(player, PrototypeRunEventKind.RelicAcquired, id);
        Assert.Equal(84, gained.MaxHp);
        Assert.Equal(54, gained.Hp);
        Assert.Equal(70, player.MaxHp);
        Assert.Equal(40, player.Hp);
    }

    [Fact]
    public void NonAcquisitionHooksStillApplyToEachIndependentRelic()
    {
        var id = PrototypeNativeOvergrowthEvents.NeowRelicId(
            "StoneHumidifier");
        var player = PrototypeNativeUnderdocksRunFactory.Create(
            "duplicate-stone-humidifiers").Player with
        {
            Hp = 30, MaxHp = 70,
            Relics = [Relic(id), Relic(id)]
        };
        var healed = Dispatch(player, PrototypeRunEventKind.RestSiteHealed);
        Assert.Equal(80, healed.MaxHp);
        Assert.Equal(40, healed.Hp);
        Assert.Equal(70, player.MaxHp);
        Assert.Equal(30, player.Hp);
    }

    [Fact]
    public void AcquiringRelicWithoutAnyExistingMatchDoesNotTriggerOthers()
    {
        var player = PrototypeNativeUnderdocksRunFactory.Create(
            "no-matching-acquisition").Player with
        {
            Relics = [Relic("proto.relic.old_coin")]
        };
        var unchanged = Dispatch(player, PrototypeRunEventKind.RelicAcquired,
            "proto.relic.mango");
        Assert.Equal(CanonicalJson.Sha256(player),
            CanonicalJson.Sha256(unchanged));
    }

    [Fact]
    public void DuplicateFresnelLensesApplyOnlyOneNonstackableNimble()
    {
        const string lensId = "proto.native.underdocks.fresnel_lens";
        var source = PrototypeNativeUnderdocksRunFactory.Create(
            "duplicate-nimble");
        var card = new CardInstance(500, "proto.silent.defend", 0,
            PrototypeJson.EmptyObject());
        var once = source.Player with
        {
            Relics = [Relic(lensId)]
        };
        var twice = source.Player with
        {
            Relics = [Relic(lensId), Relic(lensId)]
        };

        var one = ApplyNewCardEnchantments(once, card);
        var two = ApplyNewCardEnchantments(twice, card);
        Assert.NotNull(one.Enchantment);
        Assert.NotNull(two.Enchantment);
        Assert.Equal(PrototypeCardEnchantmentKind.Nimble,
            two.Enchantment.Kind);
        Assert.Equal(2, one.Enchantment.Amount);
        Assert.Equal(2, two.Enchantment.Amount);
        Assert.Equal(CanonicalJson.Sha256(once),
            CanonicalJson.Sha256(once.Fork()));
        Assert.Equal(CanonicalJson.Sha256(twice),
            CanonicalJson.Sha256(twice.Fork()));

        var alreadyEnchanted = card with
        {
            Enchantment = new PrototypeCardEnchantment(
                PrototypeCardEnchantmentKind.Steady, 1)
        };
        var preserved = ApplyNewCardEnchantments(
            twice, alreadyEnchanted);
        Assert.Equal(alreadyEnchanted.Enchantment,
            preserved.Enchantment);
        Assert.Null(ApplyNewCardEnchantments(source.Player, card)
            .Enchantment);
    }

    private static CardInstance ApplyNewCardEnchantments(
        PlayerState player, CardInstance card)
    {
        var method = typeof(PrototypeGameEngine).GetMethod(
            "ApplyNewCardEnchantments", BindingFlags.NonPublic |
                BindingFlags.Static);
        Assert.NotNull(method);
        return (CardInstance)method!.Invoke(null, [player, card])!;
    }

    private static RelicInstance Relic(string id) =>
        new(id, PrototypeJson.EmptyObject());

    private static PlayerState Dispatch(
        PlayerState player, PrototypeRunEventKind kind,
        string? acquired = null)
    {
        var method = typeof(PrototypeGameEngine).GetMethod(
            "ApplyRelicRunEvent", BindingFlags.NonPublic |
                BindingFlags.Static);
        Assert.NotNull(method);
        return (PlayerState)method!.Invoke(null,
            [player, kind, null, acquired, null, null])!;
    }
}
