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
    public void SourceBackedSlyCardsUsePinnedV01110Scalars()
    {
        var reflex = PrototypeContent.Card("proto.silent.reflex");
        Assert.True(reflex.Sly);
        Assert.Equal(3, reflex.Cost.Amount);
        var reflexDraw = Assert.Single(reflex.Effects);
        Assert.Equal(PrototypeCombatEffectKind.DrawCards, reflexDraw.Kind);
        Assert.Equal(2, reflexDraw.Amount);
        Assert.Equal(1, reflexDraw.UpgradeDelta);

        var tactician = PrototypeContent.Card("proto.silent.tactician");
        Assert.True(tactician.Sly);
        Assert.Equal(3, tactician.Cost.Amount);
        var tacticianEnergy = Assert.Single(tactician.Effects);
        Assert.Equal(PrototypeCombatEffectKind.GainEnergy, tacticianEnergy.Kind);
        Assert.Equal(1, tacticianEnergy.Amount);
        Assert.Equal(1, tacticianEnergy.UpgradeDelta);

        var untouchable = PrototypeContent.Card("proto.silent.untouchable");
        Assert.True(untouchable.Sly);
        Assert.Equal(2, untouchable.Cost.Amount);
        var untouchableBlock = Assert.Single(untouchable.Effects);
        Assert.Equal(6, untouchableBlock.Amount);
        Assert.Equal(3, untouchableBlock.UpgradeDelta);

        var flickFlack = PrototypeContent.Card("proto.silent.flick_flack");
        Assert.True(flickFlack.Sly);
        Assert.Equal(1, flickFlack.Cost.Amount);
        var flickFlackDamage = Assert.Single(flickFlack.Effects);
        Assert.Equal(7, flickFlackDamage.Amount);
        Assert.Equal(2, flickFlackDamage.UpgradeDelta);
        Assert.Equal(PrototypeEffectTarget.AllEnemies, flickFlackDamage.Target);
    }

    [Fact]
    public void NewlyImportedSimpleSilentCardsMatchPinnedV01110()
    {
        var haze = PrototypeContent.Card("proto.silent.haze");
        Assert.Equal(2, haze.Cost.Amount);
        Assert.Equal(PrototypeCardTarget.None, haze.Target);
        var hazePoison = Assert.Single(
            haze.Effects,
            effect =>
                effect.Kind == PrototypeCombatEffectKind.ApplyEnemyStatus
                && effect.StatusId == "proto.status.poison");
        Assert.Equal(4, hazePoison.Amount);
        Assert.Equal(2, hazePoison.UpgradeDelta);
        Assert.Equal(PrototypeEffectTarget.AllEnemies, hazePoison.Target);

        var hazeWeak = Assert.Single(
            haze.Effects,
            effect =>
                effect.Kind == PrototypeCombatEffectKind.ApplyEnemyStatus
                && effect.StatusId == "proto.status.weak");
        Assert.Equal(1, hazeWeak.Amount);
        Assert.Equal(1, hazeWeak.UpgradeDelta);
        Assert.Equal(PrototypeEffectTarget.AllEnemies, hazeWeak.Target);

        var leadingStrike = PrototypeContent.Card("proto.silent.leading_strike");
        Assert.Equal(1, leadingStrike.Cost.Amount);
        Assert.Equal(PrototypeCardTarget.Enemy, leadingStrike.Target);

        var damage = Assert.Single(
            leadingStrike.Effects,
            effect => effect.Kind == PrototypeCombatEffectKind.DamageEnemy);
        Assert.Equal(3, damage.Amount);
        Assert.Equal(3, damage.UpgradeDelta);

        var shivs = Assert.Single(
            leadingStrike.Effects,
            effect => effect.Kind == PrototypeCombatEffectKind.CreateCardsInHand);
        Assert.Equal(2, shivs.Amount);
        Assert.Equal("proto.silent.shiv", shivs.CardId);
    }

    [Fact]
    public void AssassinateMatchesPinnedV01110AttackScalars()
    {
        var assassinate = PrototypeContent.Card("proto.silent.assassinate");

        Assert.Equal(PrototypeCardType.Attack, assassinate.Type);
        Assert.Equal(0, assassinate.Cost.Amount);
        Assert.Equal(PrototypeCardTarget.Enemy, assassinate.Target);
        Assert.True(assassinate.ExhaustOnUse);
        Assert.True(assassinate.Innate);
        Assert.Equal(PrototypeCardRarity.Rare, assassinate.Rarity);

        var damage = Assert.Single(
            assassinate.Effects,
            effect => effect.Kind == PrototypeCombatEffectKind.DamageEnemy);
        Assert.Equal(10, damage.Amount);
        Assert.Equal(3, damage.UpgradeDelta);

        var vulnerable = Assert.Single(
            assassinate.Effects,
            effect =>
                effect.Kind == PrototypeCombatEffectKind.ApplyEnemyStatus
                && effect.StatusId == "proto.status.vulnerable");
        Assert.Equal(1, vulnerable.Amount);
        Assert.Equal(1, vulnerable.UpgradeDelta);
    }

    [Fact]
    public void AbrasiveMatchesPinnedV01110PowerScalars()
    {
        var abrasive = PrototypeContent.Card("proto.silent.abrasive");

        Assert.True(abrasive.Sly);
        Assert.Equal(3, abrasive.Cost.Amount);
        Assert.Equal(PrototypeCardTarget.None, abrasive.Target);
        Assert.Equal(PrototypeCardRarity.Rare, abrasive.Rarity);

        var dexterity = Assert.Single(
            abrasive.Effects,
            effect => effect.PowerId == "proto.power.dexterity");
        Assert.Equal(PrototypeCombatEffectKind.ApplyPlayerPower, dexterity.Kind);
        Assert.Equal(1, dexterity.Amount);
        Assert.Equal(0, dexterity.UpgradeDelta);

        var thorns = Assert.Single(
            abrasive.Effects,
            effect => effect.PowerId == "proto.power.thorns");
        Assert.Equal(PrototypeCombatEffectKind.ApplyPlayerPower, thorns.Kind);
        Assert.Equal(4, thorns.Amount);
        Assert.Equal(2, thorns.UpgradeDelta);
    }

    [Fact]
    public void RicochetMatchesPinnedV01110RandomTargetScalars()
    {
        var ricochet = PrototypeContent.Card("proto.silent.ricochet");

        Assert.True(ricochet.Sly);
        Assert.Equal(2, ricochet.Cost.Amount);
        Assert.Equal(PrototypeCardTarget.None, ricochet.Target);

        var damage = Assert.Single(ricochet.Effects);
        Assert.Equal(PrototypeCombatEffectKind.DamageEnemy, damage.Kind);
        Assert.Equal(3, damage.Amount);
        Assert.Equal(4, damage.Repetitions);
        Assert.Equal(1, damage.RepetitionUpgradeDelta);
        Assert.Equal(PrototypeEffectTarget.RandomEnemy, damage.Target);
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
