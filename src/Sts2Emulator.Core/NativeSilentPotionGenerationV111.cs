namespace Sts2Emulator.Core;

/// <summary>
/// Source-ordered, full-unlock v0.111 PotionFactory.CreateRandomPotionsOutOfCombat
/// for Silent. Includes ALL potion models, even when only two are selected.
/// Source: Silent4Epoch.Potions; SharedPotionPool.GenerateAllPotions;
/// PotionFactory.CreateRandomPotions (STS2-source@706ef1c9).
/// </summary>
public static class NativeSilentPotionGenerationV111
{
    public enum Rarity { Common, Uncommon, Rare }
    public sealed record Entry(string Id, Rarity Rarity);

    // GetPotionOptions returns character pool FIRST, then shared pool.
    // The order within each pool is source-defined, not dictionary order.
    public static readonly Entry[] FullyUnlockedPool =
    [
        // Silent4Epoch.Potions:
        new("proto.potion.poison", Rarity.Common),
        new("proto.potion.ghost_in_a_jar", Rarity.Rare),
        new("proto.potion.cunning", Rarity.Uncommon),
        // SharedPotionPool.GenerateAllPotions:
        new("proto.potion.attack", Rarity.Common),
        new("proto.potion.beetle_juice", Rarity.Rare),
        new("proto.potion.blessing_of_the_forge", Rarity.Uncommon),
        new("proto.potion.block", Rarity.Common),
        new("proto.potion.bottled_potential", Rarity.Rare),
        new("proto.potion.clarity", Rarity.Uncommon),
        new("proto.potion.colorless", Rarity.Common),
        new("proto.potion.cure_all", Rarity.Uncommon),
        new("proto.potion.dexterity", Rarity.Common),
        new("proto.potion.distilled_chaos", Rarity.Rare),
        new("proto.potion.droplet_of_precognition", Rarity.Rare),
        new("proto.potion.duplicator", Rarity.Uncommon),
        new("proto.potion.energy", Rarity.Common),
        new("proto.potion.entropic_brew", Rarity.Rare),
        new("proto.potion.explosive_ampoule", Rarity.Common),
        new("proto.potion.fairy_in_a_bottle", Rarity.Rare),
        new("proto.potion.fire", Rarity.Common),
        new("proto.potion.flex", Rarity.Common),
        new("proto.potion.fortifier", Rarity.Uncommon),
        new("proto.potion.fruit_juice", Rarity.Rare),
        new("proto.potion.fysh_oil", Rarity.Uncommon),
        new("proto.potion.gamblers_brew", Rarity.Uncommon),
        new("proto.potion.gigantification", Rarity.Rare),
        new("proto.potion.heart_of_iron", Rarity.Uncommon),
        new("proto.potion.liquid_bronze", Rarity.Uncommon),
        new("proto.potion.liquid_memories", Rarity.Rare),
        new("proto.potion.lucky_tonic", Rarity.Rare),
        new("proto.potion.mazaleths_gift", Rarity.Rare),
        new("proto.potion.orobic_acid", Rarity.Rare),
        new("proto.potion.binding", Rarity.Uncommon),
        new("proto.potion.powdered_demise", Rarity.Uncommon),
        new("proto.potion.power", Rarity.Common),
        new("proto.potion.radiant_tincture", Rarity.Uncommon),
        new("proto.potion.regen", Rarity.Uncommon),
        new("proto.potion.shackling", Rarity.Rare),
        new("proto.potion.ship_in_a_bottle", Rarity.Rare),
        new("proto.potion.skill", Rarity.Common),
        new("proto.potion.snecko_oil", Rarity.Rare),
        new("proto.potion.speed", Rarity.Common),
        new("proto.potion.stable_serum", Rarity.Uncommon),
        new("proto.potion.strength", Rarity.Common),
        new("proto.potion.swift", Rarity.Common),
        new("proto.potion.touch_of_insanity", Rarity.Uncommon),
        new("proto.potion.vulnerable", Rarity.Common),
        new("proto.potion.weak", Rarity.Common)
    ];

    public sealed record Generation(string[] PotionIds,
        NativeRngSnapshot CursorAfter, Rarity[] RolledRarities);

    /// <summary>
    /// Native PotionFactory: rarity float (Rare <= 0.1, Uncommon <= 0.35,
    /// otherwise Common), then NextItem over eligible rarity-filtered
    /// candidates. Remove only the selected potion between batch draws.
    /// Native Rng.NextItem always consumes one NextInt, even for a singleton.
    /// </summary>
    public static Generation Generate(RngBundle persistentStreams, int count)
    {
        ArgumentNullException.ThrowIfNull(persistentStreams);
        if (count is < 0 or > 48)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (FullyUnlockedPool.Length != 48
            || FullyUnlockedPool.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != 48
            || FullyUnlockedPool.Any(p => !PrototypeContent.Potions.ContainsKey(p.Id)))
            throw new InvalidDataException(
                "Pinned fully-unlocked Silent potion pool is incomplete.");
        const string stream = "run.combat_potion_generation";
        var remaining = FullyUnlockedPool.ToList();
        var selected = new List<string>(count);
        var rarities = new List<Rarity>(count);
        for (var i = 0; i < count; i++)
        {
            var roll = NativeRngV111.NextFloat(persistentStreams, stream);
            var rarity = roll <= 0.1f
                ? Rarity.Rare
                : roll <= 0.35f ? Rarity.Uncommon : Rarity.Common;
            var eligible = remaining.Where(p => p.Rarity == rarity).ToArray();
            if (eligible.Length == 0)
                throw new InvalidOperationException(
                    "Source potion rarity exhausted within selection batch.");
            var index = NativeRngV111.NextInt(persistentStreams, stream, eligible.Length);
            var picked = eligible[index];
            selected.Add(picked.Id);
            rarities.Add(rarity);
            remaining.Remove(picked);
        }
        return new Generation(selected.ToArray(),
            NativeRngV111.ToSnapshot(persistentStreams.Streams.Single(
                s => s.StreamId == stream)), rarities.ToArray());
    }

    /// <summary>
    /// Source PhialHolster.AfterObtained grants one potion capacity, then
    /// generates two distinct potions in a single source factory invocation.
    /// Caller owns an isolated fork of the persistent native streams.
    /// </summary>
    public static PlayerState ApplyPhialHolster(PlayerState player,
        RngBundle persistentStreams)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(persistentStreams);
        var slots = player.PotionSlots.Concat(new PotionInstance?[1]).ToArray();
        if (slots.Count(p => p is null) < 2)
            throw new NotSupportedException(
                "Native Phial Holster potion-replacement continuation is not implemented.");
        var result = Generate(persistentStreams, 2);
        foreach (var id in result.PotionIds)
        {
            var index = Array.IndexOf(slots, null);
            if (index < 0)
                throw new InvalidOperationException("Missing source potion slot.");
            slots[index] = new PotionInstance(id, PrototypeJson.EmptyObject());
        }
        return player with { PotionSlots = slots };
    }
}
