namespace Sts2Emulator.Core;

/// <summary>
/// Act-scoped pinned v0.111.0 event catalogs, in Hive.AllEvents and
/// Glory.AllEvents declaration order. Eligibility is checked before
/// drawing; unsupported entries remain visible and fail explicitly
/// if drawn, rather than silently biasing the prototype distribution.
/// Native event-bag weights/RNG call order are not established yet.
/// </summary>
public static class PrototypeNativeLaterActEventPool
{
    public const string ColorfulPhilosophersId =
        "proto.native.hive.colorful_philosophers";

    // Pinned Hive.cs AllEvents list, including the unavailable
    // cross-character ColorfulPhilosophers event.
    public static string[] HiveEventIds { get; } =
    [
        PrototypeNativeHiveEvents.AmalgamatorId,
        PrototypeNativeLaterActs.HiveBugslayerId,
        ColorfulPhilosophersId,
        PrototypeNativeHiveEvents.ColossalFlowerId,
        PrototypeNativeHiveEvents.FieldOfManSizedHolesId,
        PrototypeNativeHiveEvents.InfestedAutomatonId,
        PrototypeNativeHiveEvents.LostWispId,
        PrototypeNativeHiveEvents.SpiritGrafterId,
        PrototypeNativeHiveEvents.LanternKeyEventId,
        PrototypeNativeHiveEvents.ZenWeaverId
    ];

    // Pinned Glory.cs AllEvents list. WarHistorianRepy is a
    // LanternKey override, NOT a member of Glory.AllEvents.
    public static string[] GloryEventIds { get; } =
    [
        PrototypeNativeGloryEvents.BattlewornDummyId,
        PrototypeNativeGloryEvents.GraveOfTheForgottenId,
        PrototypeNativeGloryEvents.HungryForMushroomsId,
        PrototypeNativeGloryEvents.ReflectionsId,
        PrototypeNativeGloryEvents.RoundTeaPartyId,
        PrototypeNativeGloryEvents.TrialId,
        PrototypeNativeGloryEvents.TinkerTimeId
    ];

    public static string[] NativeEventIds(int act) => act switch
    {
        2 => (string[])HiveEventIds.Clone(),
        3 => (string[])GloryEventIds.Clone(),
        _ => throw new ArgumentOutOfRangeException(nameof(act),
            "The native later-act event pool requires Hive or Glory.")
    };

    /// <summary>
    /// This prototype assumes a fully unlocked cross-character profile
    /// for ColorfulPhilosophers candidate inclusion, since its native
    /// CharacterCardPools unlock list is not represented in RunState.
    /// If selected, execution fails with NotSupportedException.
    /// </summary>
    public static string[] EligibleEventIds(
        RunWorldState world, PlayerState player)
    {
        var expectedProfile =
            PrototypeNativeLaterActMap.ProfileId(world.Act);
        if (world.Map.GenerationProfileId != expectedProfile)
            throw new NotSupportedException(
                "Native later-act event selection requires a matching map profile.");

        var candidates = NativeEventIds(world.Act)
            .Where(id =>
            {
                var definition = PrototypeContent.Event(id);
                return world.Act >= definition.MinAct
                    && world.Act <= definition.MaxAct
                    && (!definition.OncePerRun
                        || !world.EventIds.Contains(id, StringComparer.Ordinal))
                    && (world.Act == 2
                        ? id == ColorfulPhilosophersId
                            || PrototypeNativeHiveEvents.IsEligible(
                                definition, player)
                        : IsGloryEligible(id, player));
            })
            .ToArray();

        var previous = world.EventIds.LastOrDefault();
        if (previous is not null && candidates.Length > 1)
        {
            // Avoid an immediate repeat when another choice is
            // eligible. Unlike the legacy selector, native events
            // are kept in their act's exact source catalog order.
            var notPrevious = candidates.Where(id =>
                !StringComparer.Ordinal.Equals(id, previous)).ToArray();
            if (notPrevious.Length > 0)
                candidates = notPrevious;
        }
        return candidates;
    }

    public static bool IsGloryEligible(string id, PlayerState player)
    {
        if (id == PrototypeNativeGloryEvents.RoundTeaPartyId)
            return player.Hp >= 12;
        if (id == PrototypeNativeGloryEvents.GraveOfTheForgottenId)
            return player.Deck.Any(card =>
                PrototypeGameEngine.CanSelectEventDeckCard(
                    card, PrototypePersistentDeckChoiceKind.Enchant,
                    null, PrototypeCardEnchantmentKind.SoulsPower,
                    false));
        return true;
    }

    public static bool HasExecutableChoices(string id) =>
        id != ColorfulPhilosophersId;
}

public sealed partial class PrototypeGameEngine
{
    private static RunState StartNativeLaterActEvent(RunState state)
    {
        var world = RequireWorld(state);
        var candidates = PrototypeNativeLaterActEventPool
            .EligibleEventIds(world, state.Player);
        if (candidates.Length == 0)
            throw new NotSupportedException(
                $"No eligible native events remain in act {world.Act}.");

        // Equal-weight prototype prior, NOT a native-bag parity claim.
        // We deliberately keep unimplemented native entries in the
        // candidate list; a drawn ColorfulPhilosophers is a hard error.
        var id = candidates[
            PrototypeRng.NextInt(state.Rng, "event", candidates.Length)];
        if (!PrototypeNativeLaterActEventPool.HasExecutableChoices(id))
            throw new NotSupportedException(
                $"Native event '{id}' requires unavailable " +
                "cross-character unlock and card reward pools.");

        var evt = new EventState(
            id,
            NativeEventGold:
                id == PrototypeNativeHiveEvents.LostWispId
                    ? 45 + PrototypeRng.NextInt(state.Rng, "event", 31)
                    : 0);

        return state with
        {
            World = world with
            {
                Event = evt,
                EventHistory = world.EventIds.Append(id).ToArray()
            },
            Phase = RunPhase.Event
        };
    }
}
