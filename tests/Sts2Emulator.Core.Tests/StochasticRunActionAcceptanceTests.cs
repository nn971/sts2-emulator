using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>Source-independent acceptance for saved stochastic run actions.</summary>
public sealed class StochasticRunActionAcceptanceTests
{
    [Fact]
    public void ShopShuffleFailsClosedWithoutSavedReplayProvider()
    {
        var rng = PrototypeRng.CreateBundle("stochastic-shop-guard");
        using var guard = ControlledChanceGuardV111.Enter();

        var error = Assert.Throws<NotSupportedException>(() =>
            LegacyFisherYatesV111.ShuffleInPlace(
                new List<int> { 1, 2 }, rng, "shop",
                "shop.potion_offer.shuffle_swap_index",
                "test:shop-offer"));

        Assert.Contains("requires a saved continuation", error.Message);
    }

    [Fact]
    public void ReferenceNatureShopEntryResumesEveryGenerationSite()
    {
        var origin = ShopEntryOrigin();
        var engine = new PrototypeGameEngine();
        var action = Assert.Single(engine.GetLegalActions(origin));
        Assert.Equal("choose_map_node", action.Kind);

        var before = CanonicalJson.Sha256(origin);
        var session = ReferenceNatureRunActionSessionV111.Begin(
            origin, action, 0x175UL);
        Assert.False(session.Complete);

        var sites = new List<string>();
        var restoredMidway = false;
        for (var boundary = 0; boundary < 256 && !session.Complete; boundary++)
        {
            var law = Assert.IsType<ChanceLawV111>(session.PendingLaw);
            Assert.Equal(ChanceLawV111.UnverifiedStatus, law.ProbabilityStatus);
            sites.Add(law.SiteId);

            if (!restoredMidway && boundary == 2)
            {
                session = ReferenceNatureRunActionSessionV111.Restore(
                    ReferenceNatureRunActionSessionV111.Save(session));
                restoredMidway = true;
            }

            session = session.ResolveNext();
        }

        Assert.True(restoredMidway);
        Assert.True(session.Complete,
            $"Shop entry stopped at {session.PendingLaw?.SiteId} after {sites.Count} decisions.");
        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "shop.card_rarity.ticket",
                "shop.card_model.index_draw",
                "shop.card.price.index_draw",
                "shop.potion_offer.shuffle_swap_index",
                "shop.potion_offer.price.index_draw",
                "shop.relic_offer.shuffle_swap_index",
                "shop.relic_offer.price.index_draw"
            },
            sites.ToHashSet(StringComparer.Ordinal));
        Assert.Equal("UNVERIFIED", Assert.Single(session.Run.Stochastic
            .ControlledRunActionHistory!).ProbabilityStatus);
        Assert.NotNull(session.Run.World?.Shop);
        PrototypeStateInvariants.Validate(session.Run);
        Assert.Equal(before, CanonicalJson.Sha256(origin));
        Assert.Equal(
            CanonicalJson.Sha256(origin.Stochastic.PrototypeRng),
            CanonicalJson.Sha256(session.Run.Stochastic.PrototypeRng));
    }

    private static RunState ShopEntryOrigin()
    {
        var empty = PrototypeJson.EmptyObject();
        var availableRelics = PrototypeContent.RelicPool
            .Where(id => !StringComparer.Ordinal.Equals(
                id, NativeGloryAncientsV111.LordsParasolId))
            .Take(3)
            .Append(NativeGloryAncientsV111.LordsParasolId)
            .ToHashSet(StringComparer.Ordinal);
        var ownedRelics = PrototypeContent.RelicPool
            .Where(id => !availableRelics.Contains(id))
            .Select(id => new RelicInstance(
                id, empty, IsWax: true, IsMelted: true))
            .ToArray();
        var potionSlotCount = PrototypeContent.Rules.PotionSlots
            + ownedRelics.Sum(relic => PrototypeContent.Relic(
                relic.RelicId).ExtraPotionSlots);
        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "stochastic-shop-entry-acceptance",
            "stochastic-shop-entry-acceptance",
            0,
            RunPhase.MapChoice,
            new PlayerState(
                70, 70, 999,
                [], ownedRelics,
                new PotionInstance?[potionSlotCount]),
            PrototypeRng.CreateBundle("stochastic-shop-entry-acceptance"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                1,
                null,
                new MapState(
                    [
                        new MapNodeState(
                            "entry", 1, 1, PrototypeRoomType.Combat, ["shop"]),
                        new MapNodeState(
                            "shop", 1, 2, PrototypeRoomType.Shop, ["floor3"]),
                        new MapNodeState(
                            "floor3", 1, 3, PrototypeRoomType.Combat, ["floor4"]),
                        new MapNodeState(
                            "floor4", 1, 4, PrototypeRoomType.Combat, ["rest"]),
                        new MapNodeState(
                            "rest", 1, 5, PrototypeRoomType.Rest, ["boss"]),
                        new MapNodeState(
                            "boss", 1, 6, PrototypeRoomType.Boss, [])
                    ],
                    CurrentNodeId: "entry",
                    EntryNodeIds: ["entry"]),
                null,
                null,
                null,
                null,
                null));

        return state;
    }
}
