using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Silent-profile relics that add the native Dig and Lift rest-site actions.
/// </summary>
public static class PrototypeNativeRestSiteRelics
{
    public const string ShovelId = "proto.relic.shovel";
    public const string GiryaId = "proto.relic.girya";
    public const int GiryaLiftLimit = 3;

    private sealed record GiryaState(int LiftCount);

    private static readonly PrototypeRelicDefinition[] RelicDefinitions =
    [
        new(ShovelId, "Shovel"),
        new(GiryaId, "Girya")
    ];

    public static PrototypeRelicDefinition[] Relics => RelicDefinitions
        .Select(PrototypeDefinitionCopiesV111.CloneRelicDefinition)
        .ToArray();

    public static bool CanDig(PlayerState player) =>
        player.ActiveRelics.Any(relic => relic.RelicId == ShovelId);

    public static bool CanLift(PlayerState player) =>
        player.ActiveRelics.Any(relic => relic.RelicId == GiryaId
            && LiftCount(relic) < GiryaLiftLimit);

    public static int CombatStrengthBonus(PlayerState player) =>
        player.ActiveRelics
            .Where(relic => relic.RelicId == GiryaId)
            .Sum(LiftCount);

    public static PlayerState Lift(PlayerState player)
    {
        var relics = (RelicInstance[])player.Relics.Clone();
        var index = Array.FindIndex(relics, relic =>
            !relic.IsMelted && relic.RelicId == GiryaId);
        if (index < 0)
        {
            throw new InvalidOperationException("Lifting requires Girya.");
        }

        var liftCount = LiftCount(relics[index]);
        if (liftCount >= GiryaLiftLimit)
        {
            throw new InvalidOperationException(
                "Girya has reached its three-lift limit.");
        }

        relics[index] = relics[index] with
        {
            PersistentState = JsonSerializer.SerializeToElement(
                new GiryaState(liftCount + 1))
        };
        return player with { Relics = relics };
    }

    private static int LiftCount(RelicInstance relic)
    {
        var state = relic.PersistentState;
        if (state.ValueKind == JsonValueKind.Undefined
            || state.ValueKind == JsonValueKind.Null
            || state.ValueKind == JsonValueKind.Object
                && !state.TryGetProperty(nameof(GiryaState.LiftCount), out _))
        {
            return 0;
        }

        if (state.ValueKind != JsonValueKind.Object
            || !state.TryGetProperty(nameof(GiryaState.LiftCount), out var value)
            || !value.TryGetInt32(out var count)
            || count < 0 || count > GiryaLiftLimit)
        {
            throw new InvalidOperationException(
                "Girya requires a persisted lift count from zero to three.");
        }

        return count;
    }
}

/// <summary>
/// Engine action IDs mapped from the pinned single-player native rest options.
/// </summary>
public static class PrototypeNativeRestSiteActionsV111
{
    public const string Clone = "rest_clone";
    public const string Cook = "rest_cook";
    public const string Dig = "rest_dig";
    public const string Hatch = "rest_hatch";
    public const string Heal = "rest_heal";
    public const string Kindle = "rest_kindle";
    public const string Lift = "rest_lift";
    public const string Smith = "rest_upgrade";

    public static string? ForNativeOption(string optionId) => optionId switch
    {
        "CLONE" => Clone,
        "COOK" => Cook,
        "DIG" => Dig,
        "HATCH" => Hatch,
        "HEAL" => Heal,
        "KINDLE" => Kindle,
        "LIFT" => Lift,
        "SMITH" => Smith,
        _ => null
    };
}
