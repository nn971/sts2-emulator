using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class ResearchCombatFixtureV111Tests
{
    [Fact]
    public void CuratedDistributionProvidesOneThousandStructurallyDistinctSpecs()
    {
        var fixtures = ResearchCombatFixtureGeneratorV111.GenerateDistinct(1000, 20261027);
        Assert.Equal(1000, fixtures.Length);
        Assert.Equal(1000, fixtures.Select(spec => spec.SpecHash).Distinct().Count());
        Assert.Equal(1000, fixtures.Select(spec =>
            string.Join("|", spec.EncounterId,
                string.Join(",", spec.Deck.Select(card =>
                    card.CardId + ":" + card.UpgradeLevel)),
                string.Join(",", spec.RelicIds), string.Join(",", spec.PotionIds),
                spec.Hp.ToString())).Distinct().Count());
        Assert.All(fixtures, spec =>
        {
            spec.Validate();
            Assert.Equal(20, spec.Deck.Length);
            Assert.Equal(10, spec.Ascension);
            Assert.Equal(3, spec.RelicIds.Length);
            Assert.True(spec.Hp > 0 && spec.Hp <= spec.MaxHp);
        });
        Assert.Equal(
            fixtures.Select(spec => spec.SpecHash),
            ResearchCombatFixtureGeneratorV111.GenerateDistinct(1000, 20261027)
                .Select(spec => spec.SpecHash));
    }

    [Fact]
    public void SourceWorldSeedIsIndependentOfFixtureIdentity()
    {
        var spec = ResearchCombatFixtureGeneratorV111.Generate(0, 20261027);
        Assert.Equal(spec.SpecHash,
            ResearchCombatFixtureGeneratorV111.Generate(0, 20261027).SpecHash);
        var first = Entry(spec, "research-a10-opening-seed-one");
        var second = Entry(spec, "research-a10-opening-seed-two");
        spec.ValidateFor(first);
        spec.ValidateFor(second);
        Assert.NotEqual(first.Seed, second.Seed);
        Assert.Equal(first.FixtureSpec!.SpecHash, second.FixtureSpec!.SpecHash);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ResearchOpeningPreservesPersistentDeckUpgradesRelicsAndReplay(int index)
    {
        var spec = ResearchCombatFixtureGeneratorV111.Generate(index, 20261027);
        var entry = Entry(spec, "research-a10-open-" + index);
        var first = SemanticPilotCombatEngineV111.BeginSeeded(entry);
        var replay = SemanticPilotCombatEngineV111.Restore(
            SemanticPilotCombatEngineV111.Save(first));
        Assert.Equal(first.Fingerprint, replay.Fingerprint);
        Assert.True(first.AtPlayerBoundary);
        Assert.Equal(spec.Deck.Length, first.Run.Player.Deck.Length);
        Assert.Equal(spec.Deck.Length, first.Run.World!.Combat!.Cards.Length);
        Assert.Equal(spec.Deck.Select(card => card.CardId),
            first.Run.Player.Deck.Select(card => card.CardId));
        Assert.Equal(spec.Deck.Select(card => card.UpgradeLevel),
            first.Run.Player.Deck.Select(card => card.UpgradeLevel));
        Assert.Equal(spec.RelicIds,
            first.Run.Player.Relics.Select(relic => relic.RelicId));
        Assert.Equal(spec.Hp, first.Run.Player.Hp);
        Assert.Equal(spec.MaxHp, first.Run.Player.MaxHp);
        Assert.Equal(spec.PotionIds,
            first.Run.Player.PotionSlots.Select(potion => potion?.PotionId));
        Assert.Equal(spec.SpecHash, first.OpeningSource.FixtureSpec!.SpecHash);
        Assert.Equal(spec.Deck.Length,
            first.Run.Player.Deck.Select(card => card.InstanceId).Distinct().Count());
        Assert.Equal(spec.Deck.Length,
            first.Run.World.Combat.Cards
                .Select(card => card.PersistentCardInstanceId)
                .Distinct().Count());
    }

    [Fact]
    public void BadInitialConditionsFailBeforeEntryChanceSampling()
    {
        var valid = ResearchCombatFixtureGeneratorV111.Generate(0);
        Assert.Throws<NotSupportedException>(() =>
            (valid with { Deck = [new ResearchCombatCardV111("unknown.card")] })
                .Validate());
        Assert.Throws<InvalidDataException>(() =>
            (valid with
            {
                Deck = valid.Deck.Select(card => card with
                {
                    UpgradeLevel = 999
                }).ToArray()
            }).Validate());
        Assert.Throws<InvalidDataException>(() =>
            (valid with
            {
                RelicIds = ["proto.relic.silent_ring", "proto.relic.lantern",
                    "proto.relic.lantern"]
            }).Validate());
        Assert.Throws<NotSupportedException>(() =>
            (valid with
            {
                PotionIds = ["proto.potion.snecko_oil", null]
            }).Validate());
        Assert.Throws<InvalidDataException>(() =>
            (valid with { Hp = 0 }).Validate());
        Assert.Throws<NotSupportedException>(() =>
            (valid with { TotalFloor = 3 }).Validate());
        Assert.Throws<NotSupportedException>(() =>
            valid.ValidateFor(Entry(valid, "research-invalid-entry") with
            {
                FixtureDeckPresetId = SemanticConditionedDeckPresetV111.PoisonKit
            }));
    }

    private static SemanticSeededEntryV111 Entry(
        ResearchCombatFixtureV111 spec, string sourceSeed) => new(
        sourceSeed, spec.Act, spec.EncounterId, spec.NativeEncounterEntry,
        spec.TotalFloor, spec.Ascension, 1, spec.MapColumn, spec.MapRow,
        ReplayOpeningDrawHooks: true,
        FixtureSpec: spec);
}
