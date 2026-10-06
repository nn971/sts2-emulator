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
    public void CountScaledDamageCardsMatchPinnedV01110Scalars()
    {
        var memento = PrototypeContent.Card("proto.silent.memento_mori");
        Assert.Equal(PrototypeCardType.Attack, memento.Type);
        Assert.Equal(1, memento.Cost.Amount);
        Assert.Equal(PrototypeCardRarity.Uncommon, memento.Rarity);

        var mementoDamage = Assert.Single(memento.Effects);
        Assert.Equal(9, mementoDamage.Amount);
        Assert.Equal(2, mementoDamage.UpgradeDelta);
        Assert.Equal(
            PrototypeCombatCountKind.CardsDiscardedThisTurn,
            mementoDamage.CountKind);
        Assert.Equal(4, mementoDamage.AmountPerCount);
        Assert.Equal(1, mementoDamage.AmountPerCountUpgradeDelta);

        var murder = PrototypeContent.Card("proto.silent.murder");
        Assert.Equal(PrototypeCardType.Attack, murder.Type);
        Assert.Equal(3, murder.Cost.Amount);
        Assert.Equal(-1, murder.Cost.UpgradeDelta);
        Assert.Equal(2, murder.Cost.AmountAt(1));
        Assert.Equal(PrototypeCardRarity.Rare, murder.Rarity);

        var murderDamage = Assert.Single(murder.Effects);
        Assert.Equal(1, murderDamage.Amount);
        Assert.Equal(
            PrototypeCombatCountKind.CardsDrawnThisCombat,
            murderDamage.CountKind);
        Assert.Equal(1, murderDamage.AmountPerCount);

        var preciseCut = PrototypeContent.Card("proto.silent.precise_cut");
        Assert.Equal(PrototypeCardType.Attack, preciseCut.Type);
        Assert.Equal(0, preciseCut.Cost.Amount);
        Assert.Equal(PrototypeCardRarity.Uncommon, preciseCut.Rarity);

        var preciseDamage = Assert.Single(preciseCut.Effects);
        Assert.Equal(13, preciseDamage.Amount);
        Assert.Equal(3, preciseDamage.UpgradeDelta);
        Assert.Equal(
            PrototypeCombatCountKind.OtherCardsInHand,
            preciseDamage.CountKind);
        Assert.Equal(-2, preciseDamage.AmountPerCount);
    }

    [Fact]
    public void PinpointMatchesPinnedV01110DynamicCostAndDamage()
    {
        var pinpoint = PrototypeContent.Card("proto.silent.pinpoint");

        Assert.Equal(PrototypeCardType.Attack, pinpoint.Type);
        Assert.Equal(PrototypeCardRarity.Uncommon, pinpoint.Rarity);
        Assert.Equal(3, pinpoint.Cost.Amount);
        Assert.Equal(
            PrototypeCombatCountKind.SkillsPlayedThisTurn,
            pinpoint.Cost.ReductionCountKind);
        Assert.Equal(1, pinpoint.Cost.ReductionPerCount);
        Assert.Equal(0, pinpoint.Cost.MinimumAmount);

        var damage = Assert.Single(pinpoint.Effects);
        Assert.Equal(PrototypeCombatEffectKind.DamageEnemy, damage.Kind);
        Assert.Equal(15, damage.Amount);
        Assert.Equal(4, damage.UpgradeDelta);
    }

    [Fact]
    public void CountBackedCardsMatchPinnedV01110Scalars()
    {
        var finisher = PrototypeContent.Card("proto.silent.finisher");
        Assert.Equal(PrototypeCardType.Attack, finisher.Type);
        Assert.Equal(1, finisher.Cost.Amount);
        Assert.Equal(PrototypeCardRarity.Uncommon, finisher.Rarity);

        var finisherDamage = Assert.Single(finisher.Effects);
        Assert.Equal(PrototypeCombatEffectKind.DamageEnemy, finisherDamage.Kind);
        Assert.Equal(6, finisherDamage.Amount);
        Assert.Equal(2, finisherDamage.UpgradeDelta);
        Assert.Equal(0, finisherDamage.Repetitions);
        Assert.Equal(
            PrototypeCombatCountKind.AttacksPlayedThisTurn,
            finisherDamage.CountKind);
        Assert.Equal(1, finisherDamage.RepetitionsPerCount);

        var flechettes = PrototypeContent.Card("proto.silent.flechettes");
        Assert.Equal(PrototypeCardType.Attack, flechettes.Type);
        Assert.Equal(1, flechettes.Cost.Amount);
        Assert.Equal(PrototypeCardRarity.Uncommon, flechettes.Rarity);

        var flechettesDamage = Assert.Single(flechettes.Effects);
        Assert.Equal(PrototypeCombatEffectKind.DamageEnemy, flechettesDamage.Kind);
        Assert.Equal(5, flechettesDamage.Amount);
        Assert.Equal(2, flechettesDamage.UpgradeDelta);
        Assert.Equal(0, flechettesDamage.Repetitions);
        Assert.Equal(
            PrototypeCombatCountKind.SkillsInHand,
            flechettesDamage.CountKind);
        Assert.Equal(1, flechettesDamage.RepetitionsPerCount);
    }

    [Fact]
    public void PredicateBackedCardsMatchPinnedV01110Scalars()
    {
        var grandFinale = PrototypeContent.Card("proto.silent.grand_finale");
        Assert.Equal(PrototypeCardType.Attack, grandFinale.Type);
        Assert.Equal(0, grandFinale.Cost.Amount);
        Assert.Equal(PrototypeCardRarity.Rare, grandFinale.Rarity);
        Assert.Equal(
            PrototypeCombatPredicateKind.DrawPileEmpty,
            grandFinale.PlayCondition?.Kind);

        var finaleDamage = Assert.Single(grandFinale.Effects);
        Assert.Equal(PrototypeCombatEffectKind.DamageEnemy, finaleDamage.Kind);
        Assert.Equal(60, finaleDamage.Amount);
        Assert.Equal(15, finaleDamage.UpgradeDelta);
        Assert.Equal(PrototypeEffectTarget.AllEnemies, finaleDamage.Target);

        var bubble = PrototypeContent.Card("proto.silent.bubble_bubble");
        Assert.Equal(PrototypeCardType.Skill, bubble.Type);
        Assert.Equal(1, bubble.Cost.Amount);
        Assert.Equal(PrototypeCardRarity.Uncommon, bubble.Rarity);

        var bubblePoison = Assert.Single(bubble.Effects);
        Assert.Equal(PrototypeCombatEffectKind.ApplyEnemyStatus, bubblePoison.Kind);
        Assert.Equal("proto.status.poison", bubblePoison.StatusId);
        Assert.Equal(9, bubblePoison.Amount);
        Assert.Equal(3, bubblePoison.UpgradeDelta);
        Assert.Equal(
            PrototypeCombatPredicateKind.TargetHasStatus,
            bubblePoison.Condition?.Kind);
        Assert.Equal("proto.status.poison", bubblePoison.Condition?.StatusId);
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
    public void RandomTargetSilentCardsMatchPinnedV01110Scalars()
    {
        var bouncingFlask = PrototypeContent.Card(
            "proto.silent.bouncing_flask");
        Assert.Equal(PrototypeCardType.Skill, bouncingFlask.Type);
        Assert.Equal(2, bouncingFlask.Cost.Amount);
        Assert.Equal(PrototypeCardRarity.Uncommon, bouncingFlask.Rarity);

        var poison = Assert.Single(bouncingFlask.Effects);
        Assert.Equal(
            PrototypeCombatEffectKind.ApplyEnemyStatus,
            poison.Kind);
        Assert.Equal("proto.status.poison", poison.StatusId);
        Assert.Equal(3, poison.Amount);
        Assert.Equal(3, poison.Repetitions);
        Assert.Equal(1, poison.RepetitionUpgradeDelta);
        Assert.Equal(PrototypeEffectTarget.RandomEnemy, poison.Target);

        var serpentForm = PrototypeContent.Card("proto.silent.serpent_form");
        Assert.Equal(PrototypeCardType.Power, serpentForm.Type);
        Assert.Equal(3, serpentForm.Cost.Amount);
        Assert.Equal(PrototypeCardRarity.Rare, serpentForm.Rarity);

        var apply = Assert.Single(serpentForm.Effects);
        Assert.Equal(
            PrototypeCombatEffectKind.ApplyPlayerPower,
            apply.Kind);
        Assert.Equal("proto.power.serpent_form", apply.PowerId);
        Assert.Equal(4, apply.Amount);
        Assert.Equal(2, apply.UpgradeDelta);
    }

    [Fact]
    public void DrawReactiveSilentCardsMatchPinnedV01110()
    {
        var corrosiveWave = PrototypeContent.Card(
            "proto.silent.corrosive_wave");
        Assert.Equal(PrototypeCardType.Skill, corrosiveWave.Type);
        Assert.Equal(PrototypeCardRarity.Rare, corrosiveWave.Rarity);
        Assert.Equal(1, corrosiveWave.Cost.Amount);

        var corrosiveApply = Assert.Single(corrosiveWave.Effects);
        Assert.Equal(
            PrototypeCombatEffectKind.ApplyPlayerPower,
            corrosiveApply.Kind);
        Assert.Equal("proto.power.corrosive_wave", corrosiveApply.PowerId);
        Assert.Equal(2, corrosiveApply.Amount);
        Assert.Equal(1, corrosiveApply.UpgradeDelta);

        var speedster = PrototypeContent.Card("proto.silent.speedster");
        Assert.Equal(PrototypeCardType.Power, speedster.Type);
        Assert.Equal(PrototypeCardRarity.Uncommon, speedster.Rarity);
        Assert.Equal(2, speedster.Cost.Amount);
        Assert.False(speedster.Innate);
        Assert.True(speedster.InnateOnUpgrade);

        var speedsterApply = Assert.Single(speedster.Effects);
        Assert.Equal(
            PrototypeCombatEffectKind.ApplyPlayerPower,
            speedsterApply.Kind);
        Assert.Equal("proto.power.speedster", speedsterApply.PowerId);
        Assert.Equal(2, speedsterApply.Amount);
        Assert.Equal(0, speedsterApply.UpgradeDelta);
    }

    [Fact]
    public void UpMySleeveMatchesPinnedV01110CardLocalCostMutation()
    {
        var card = PrototypeContent.Card(
            "proto.silent.up_my_sleeve");

        Assert.Equal(2, card.Cost.Amount);
        Assert.Equal(PrototypeCardType.Skill, card.Type);
        Assert.Equal(
            PrototypeCardRarity.Uncommon,
            card.Rarity);

        var shivs = Assert.Single(
            card.Effects,
            effect =>
                effect.Kind
                == PrototypeCombatEffectKind.CreateCardsInHand);
        Assert.Equal(3, shivs.Amount);
        Assert.Equal(1, shivs.UpgradeDelta);
        Assert.Equal("proto.silent.shiv", shivs.CardId);

        var cost = Assert.Single(
            card.Effects,
            effect =>
                effect.Kind
                == PrototypeCombatEffectKind.ModifySourceCardEnergyCost);
        Assert.Equal(-1, cost.Amount);
    }

    [Fact]
    public void InfiniteBladesAndSuppressMatchPinnedV01110()
    {
        var infiniteBlades = PrototypeContent.Card(
            "proto.silent.infinite_blades");
        Assert.Equal(1, infiniteBlades.Cost.Amount);
        Assert.Equal(PrototypeCardType.Power, infiniteBlades.Type);
        Assert.Equal(
            PrototypeCardRarity.Uncommon,
            infiniteBlades.Rarity);
        Assert.False(infiniteBlades.Innate);
        Assert.True(infiniteBlades.InnateOnUpgrade);

        var infiniteEffect = Assert.Single(
            infiniteBlades.Effects);
        Assert.Equal(
            PrototypeCombatEffectKind.ApplyPlayerPower,
            infiniteEffect.Kind);
        Assert.Equal(
            "proto.power.infinite_blades",
            infiniteEffect.PowerId);

        var infinitePower = PrototypeContent.Power(
            "proto.power.infinite_blades");
        var trigger = Assert.Single(infinitePower.Triggers);
        Assert.Equal(
            PrototypeCombatEventKind.BeforeHandDraw,
            trigger.EventKind);
        var shiv = Assert.Single(trigger.Effects);
        Assert.Equal(
            PrototypeCombatEffectKind.CreateCardsInHand,
            shiv.Kind);
        Assert.Equal("proto.silent.shiv", shiv.CardId);
        Assert.Equal(1, shiv.AmountPerPowerStack);

        var suppress = PrototypeContent.Card(
            "proto.silent.suppress");
        Assert.Equal(0, suppress.Cost.Amount);
        Assert.Equal(PrototypeCardType.Attack, suppress.Type);
        Assert.Equal(
            PrototypeCardRarity.Ancient,
            suppress.Rarity);
        Assert.True(suppress.Innate);

        var damage = Assert.Single(
            suppress.Effects,
            effect =>
                effect.Kind
                == PrototypeCombatEffectKind.DamageEnemy);
        Assert.Equal(11, damage.Amount);
        Assert.Equal(6, damage.UpgradeDelta);

        var weak = Assert.Single(
            suppress.Effects,
            effect =>
                effect.Kind
                == PrototypeCombatEffectKind.ApplyEnemyStatus);
        Assert.Equal("proto.status.weak", weak.StatusId);
        Assert.Equal(3, weak.Amount);
        Assert.Equal(2, weak.UpgradeDelta);

        Assert.DoesNotContain(
            "proto.silent.suppress",
            PrototypeContent.RewardCardPool);
    }

    [Fact]
    public void AnticipateMatchesPinnedV01110TemporaryDexterity()
    {
        var anticipate = PrototypeContent.Card("proto.silent.anticipate");

        Assert.Equal(0, anticipate.Cost.Amount);
        Assert.Equal(PrototypeCardTarget.None, anticipate.Target);
        Assert.Equal(PrototypeCardType.Skill, anticipate.Type);
        Assert.Equal(PrototypeCardRarity.Common, anticipate.Rarity);

        var effect = Assert.Single(anticipate.Effects);
        Assert.Equal(
            PrototypeCombatEffectKind.ApplyPlayerPower,
            effect.Kind);
        Assert.Equal(2, effect.Amount);
        Assert.Equal(2, effect.UpgradeDelta);
        Assert.Equal(
            "proto.power.temporary_dexterity",
            effect.PowerId);

        var power = PrototypeContent.Power(
            "proto.power.temporary_dexterity");
        Assert.Equal(1, power.BlockBonusPerStack);
        Assert.True(power.AllowNegative);
        Assert.True(power.RemoveAtPlayerTurnEnd);
    }

    [Fact]
    public void PounceMatchesPinnedV01110OneShotSkillCostSemantics()
    {
        var pounce = PrototypeContent.Card("proto.silent.pounce");

        Assert.Equal(2, pounce.Cost.Amount);
        Assert.Equal(PrototypeCardTarget.Enemy, pounce.Target);
        Assert.Equal(PrototypeCardType.Attack, pounce.Type);
        Assert.Equal(PrototypeCardRarity.Uncommon, pounce.Rarity);

        var damage = Assert.Single(
            pounce.Effects,
            effect => effect.Kind == PrototypeCombatEffectKind.DamageEnemy);
        Assert.Equal(14, damage.Amount);
        Assert.Equal(6, damage.UpgradeDelta);

        var freeSkill = Assert.Single(
            pounce.Effects,
            effect => effect.Kind == PrototypeCombatEffectKind.ApplyPlayerPower);
        Assert.Equal(1, freeSkill.Amount);
        Assert.Equal("proto.power.free_next_skill", freeSkill.PowerId);

        var power = PrototypeContent.Power("proto.power.free_next_skill");
        Assert.Equal(PrototypeCardType.Skill, power.FreeCardType);
        Assert.True(power.ConsumeOnMatchingCardPlay);
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
