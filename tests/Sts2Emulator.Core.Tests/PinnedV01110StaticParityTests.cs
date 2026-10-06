using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PinnedV01110StaticParityTests
{
    [Fact]
    public void SilentStartingBaselineMatchesPinnedStaticReference()
    {
        Assert.Equal(70, PrototypeContent.Rules.StartingHp);
        Assert.Equal(99, PrototypeContent.Rules.StartingGold);
        Assert.Equal(3, PrototypeContent.Rules.BaseEnergy);

        var deckNames = PrototypeContent.StartingDeck
            .Select(PrototypeContent.Card)
            .Select(card => card.Name)
            .ToArray();

        Assert.Equal(12, deckNames.Length);
        Assert.Equal(5, deckNames.Count(name => name == "Strike"));
        Assert.Equal(5, deckNames.Count(name => name == "Defend"));
        Assert.Single(deckNames, name => name == "Neutralize");
        Assert.Single(deckNames, name => name == "Survivor");

        var startingRelicId = Assert.Single(PrototypeContent.StartingRelics);
        var startingRelic = PrototypeContent.Relic(startingRelicId);

        Assert.Equal("Ring of the Snake", startingRelic.Name);
        Assert.Equal(2, startingRelic.FirstTurnDrawBonus);
    }

    [Fact]
    public void CorrectedSilentCardsMatchPinnedV01110BaseAndUpgradeScalars()
    {
        var bladeDance = PrototypeContent.Card("proto.silent.blade_dance");
        Assert.True(bladeDance.ExhaustOnUse);
        var bladeDanceCreate = Assert.Single(
            bladeDance.Effects,
            effect => effect.Kind == PrototypeCombatEffectKind.CreateCardsInHand);
        Assert.Equal(3, bladeDanceCreate.Amount);
        Assert.Equal(1, bladeDanceCreate.UpgradeDelta);

        var skewer = PrototypeContent.Card("proto.silent.skewer");
        Assert.Equal(PrototypeCardCostKind.X, skewer.Cost.Kind);
        var skewerDamage = Assert.Single(
            skewer.Effects,
            effect => effect.Kind == PrototypeCombatEffectKind.DamageEnemy);
        Assert.Equal(8, skewerDamage.Amount);
        Assert.Equal(3, skewerDamage.UpgradeDelta);
        Assert.Equal(1, skewerDamage.RepetitionsPerEnergySpent);

        var slice = PrototypeContent.Card("proto.silent.slice");
        var sliceDamage = Assert.Single(
            slice.Effects,
            effect => effect.Kind == PrototypeCombatEffectKind.DamageEnemy);
        Assert.Equal(6, sliceDamage.Amount);
        Assert.Equal(3, sliceDamage.UpgradeDelta);

        var suckerPunch = PrototypeContent.Card("proto.silent.sucker_punch");
        var suckerPunchDamage = Assert.Single(
            suckerPunch.Effects,
            effect => effect.Kind == PrototypeCombatEffectKind.DamageEnemy);
        Assert.Equal(8, suckerPunchDamage.Amount);
        Assert.Equal(2, suckerPunchDamage.UpgradeDelta);

        var suckerPunchWeak = Assert.Single(
            suckerPunch.Effects,
            effect => effect.Kind == PrototypeCombatEffectKind.ApplyEnemyStatus);
        Assert.Equal(1, suckerPunchWeak.Amount);
        Assert.Equal(1, suckerPunchWeak.UpgradeDelta);
        Assert.Equal("proto.status.weak", suckerPunchWeak.StatusId);
    }
}
