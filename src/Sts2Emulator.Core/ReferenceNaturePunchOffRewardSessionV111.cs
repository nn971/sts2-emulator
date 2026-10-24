using System.Text.Json;

namespace Sts2Emulator.Core;

internal sealed record NativePunchOffRewardPlanV111(
    string[] RelicCandidates,
    string[] PotionCandidates,
    RandomIndexRequestV111 RelicRequest,
    RandomIndexRequestV111 PotionRequest,
    NativeRngSnapshot RewardsCursorBefore,
    NativeRngSnapshot? EventCursorBefore,
    int RequestedOrdinal,
    string SiteId,
    string ConditioningFingerprint);

internal static class NativePunchOffRewardPlanBuilderV111
{
    public static NativePunchOffRewardPlanV111 Build(
        RunState origin, string siteId, int ordinal, int[] priorIndices,
        string actionId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentNullException.ThrowIfNull(priorIndices);
        if (priorIndices.Any(index => index < 0))
            throw new InvalidOperationException(
                "Punch Off reward queries require nonnegative prior indices.");
        if (string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.Stochastic.NativeRunRng is null || origin.Phase != RunPhase.Combat ||
            origin.World is not { Act: 1, Event: { } evt, Combat: { } combat } world ||
            evt.EventId != PrototypeNativePunchOff.EventId ||
            evt.NativePageIndex != 2 || evt.ChosenChoiceId is not null ||
            evt.Stochastic.RandomOutcomes.NativeEventPotionSlot is not null ||
            evt.Stochastic.ProviderMode is not null ||
            world.ActiveRoom != PrototypeRoomType.Combat ||
            world.Reward is not null || combat.Enemies.Any(enemy => enemy.Hp <= 0) ||
            origin.Stochastic.ControlledEventRewardChoice is not null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world))
            throw new InvalidOperationException(
                "Punch Off reward laws require its live configured fight before an immediate victory.");

        var action = new PrototypeGameEngine().GetLegalActions(origin)
            .SingleOrDefault(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) == actionId)
            ?? throw new InvalidOperationException(
                "Punch Off reward action is not legal in the current combat state.");
        var relicCandidates = PrototypeContent.RelicPool
            .Where(id => !origin.Player.Relics.Any(relic =>
                StringComparer.Ordinal.Equals(relic.RelicId, id)))
            .ToArray();
        var potionCandidates = (string[])PrototypeContent.PotionPool.Clone();
        if (relicCandidates.Length == 0 || potionCandidates.Length == 0)
            throw new InvalidOperationException(
                "Punch Off reward law requires nonempty relic and potion pools.");

        var nativeStreams = origin.Stochastic.NativeRunRng;
        if (nativeStreams is null || !nativeStreams.TryGetStream(
                NativePlayerRngV111.RewardsStreamId, out var rewardStream))
            throw new InvalidOperationException(
                "Punch Off reward law requires the Native player.rewards stream.");
        var cursorBefore = NativeRngV111.ToSnapshot(rewardStream);
        var relicOwner = "event:punch_off:relic_pool:" +
            CanonicalJson.Sha256(relicCandidates);
        var potionOwner = "event:punch_off:potion_pool:" +
            CanonicalJson.Sha256(potionCandidates);

        using var journal = RandomBoundaryJournalV111.Begin();
        using (StochasticProviderScopeV111.Enter(
                   new NativeRandomIndexProviderV111(),
                   overrideExplicitProviders: false))
        {
            var projected = new PrototypeGameEngine().Step(
                origin.Fork(), action).State;
            var matching = journal.Entries.Where(entry =>
                entry.Request.SiteId is
                    PrototypeNativePunchOff.RelicRewardSiteId or
                    PrototypeNativePunchOff.PotionRewardSiteId).ToArray();
            var rewardsEntries = journal.Entries.Where(entry =>
                entry.Request.StreamId ==
                    NativePlayerRngV111.RewardsStreamId).ToArray();
            RngStreamState? projectedRewardsStream = null;
            if (projected.Stochastic.NativeRunRng is { } projectedRng)
                projectedRng.TryGetStream(
                    NativePlayerRngV111.RewardsStreamId,
                    out projectedRewardsStream);
            var projectedEventCursor = projected.World?.Event?.Stochastic.NativeRng
                is { } projectedEventStream
                    ? NativeRngV111.ToSnapshot(projectedEventStream) : null;
            var originEventCursor = evt.Stochastic.NativeRng is { } originEventStream
                ? NativeRngV111.ToSnapshot(originEventStream) : null;
            if (projected.Phase != RunPhase.Reward ||
                projected.World?.Combat is not null ||
                projected.World?.Reward is not { RelicOption: not null,
                    PotionOption: not null } ||
                matching.Length != 2 ||
                rewardsEntries.Length != 2 ||
                !rewardsEntries.SequenceEqual(matching) ||
                projectedRewardsStream is null ||
                unchecked((uint)(NativeRngV111.ToSnapshot(
                    projectedRewardsStream).Counter - cursorBefore.Counter)) != 2 ||
                projectedEventCursor != originEventCursor ||
                matching[0].Request.SiteId !=
                    PrototypeNativePunchOff.RelicRewardSiteId ||
                matching[1].Request.SiteId !=
                    PrototypeNativePunchOff.PotionRewardSiteId ||
                matching.Any(entry =>
                    entry.ProviderMode != "native-v111" ||
                    entry.Request.StreamId !=
                        NativePlayerRngV111.RewardsStreamId ||
                    entry.Request.OperationKind != "choose_index"))
                throw new InvalidOperationException(
                    "Punch Off requires an immediate win with exactly the ordered relic and potion reward draws.");
            var expectedRelicRequest = new RandomIndexRequestV111(
                PrototypeNativePunchOff.RelicRewardSiteId,
                NativePlayerRngV111.RewardsStreamId, relicOwner,
                cursorBefore.Counter,
                Enumerable.Range(0, relicCandidates.Length).ToArray());
            var expectedPotionRequest = new RandomIndexRequestV111(
                PrototypeNativePunchOff.PotionRewardSiteId,
                NativePlayerRngV111.RewardsStreamId, potionOwner,
                unchecked(cursorBefore.Counter + 1),
                Enumerable.Range(0, potionCandidates.Length).ToArray());
            if (!ControlledStochasticChanceReplayV111.RequestsMatch(
                    matching[0].Request, expectedRelicRequest) ||
                !ControlledStochasticChanceReplayV111.RequestsMatch(
                    matching[1].Request, expectedPotionRequest))
                throw new InvalidOperationException(
                    "Punch Off reward requests do not match the ordered candidate pools or player.rewards cursor.");
        }

        var request = siteId switch
        {
            PrototypeNativePunchOff.RelicRewardSiteId =>
                new RandomIndexRequestV111(
                    PrototypeNativePunchOff.RelicRewardSiteId,
                    NativePlayerRngV111.RewardsStreamId, relicOwner,
                    cursorBefore.Counter,
                    Enumerable.Range(0, relicCandidates.Length).ToArray()),
            PrototypeNativePunchOff.PotionRewardSiteId =>
                new RandomIndexRequestV111(
                    PrototypeNativePunchOff.PotionRewardSiteId,
                    NativePlayerRngV111.RewardsStreamId, potionOwner,
                    unchecked(cursorBefore.Counter + 1),
                    Enumerable.Range(0, potionCandidates.Length).ToArray()),
            _ => throw new InvalidOperationException(
                $"Unsupported Punch Off reward site '{siteId}'.")
        };
        var expectedPriorCount = siteId ==
            PrototypeNativePunchOff.RelicRewardSiteId ? 0 : 1;
        if (priorIndices.Length != expectedPriorCount ||
            ordinal != request.Ordinal ||
            expectedPriorCount == 1 &&
                (uint)priorIndices[0] >= (uint)relicCandidates.Length)
            throw new InvalidOperationException(
                "Punch Off reward law requires the complete earlier relic-index prefix and request ordinal.");

        var eventCursorBefore = origin.World.Event?.Stochastic.NativeRng is { } eventStream
            ? NativeRngV111.ToSnapshot(eventStream) : null;
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            boundary = "punch_off.combat_victory_rewards",
            eventId = evt.EventId,
            actionId,
            siteId,
            relicCandidateIds = relicCandidates,
            potionCandidateIds = potionCandidates,
            priorIndices,
            rewardsCursorBefore = cursorBefore,
            eventCursorBefore,
            request,
            probabilityModel = "source-nextint-grid-under-player-rewards-stream-hypothesis"
        });
        return new(relicCandidates, potionCandidates,
            new RandomIndexRequestV111(
                PrototypeNativePunchOff.RelicRewardSiteId,
                NativePlayerRngV111.RewardsStreamId, relicOwner,
                cursorBefore.Counter,
                Enumerable.Range(0, relicCandidates.Length).ToArray()),
            new RandomIndexRequestV111(
                PrototypeNativePunchOff.PotionRewardSiteId,
                NativePlayerRngV111.RewardsStreamId, potionOwner,
                unchecked(cursorBefore.Counter + 1),
                Enumerable.Range(0, potionCandidates.Length).ToArray()),
            cursorBefore, eventCursorBefore, ordinal, siteId,
            conditioningFingerprint);
    }

    public static string[] Candidates(
        NativePunchOffRewardPlanV111 plan) =>
        plan.SiteId == PrototypeNativePunchOff.RelicRewardSiteId
            ? plan.RelicCandidates : plan.PotionCandidates;

    public static RandomIndexRequestV111 Request(
        NativePunchOffRewardPlanV111 plan) =>
        plan.SiteId == PrototypeNativePunchOff.RelicRewardSiteId
            ? plan.RelicRequest : plan.PotionRequest;

}
