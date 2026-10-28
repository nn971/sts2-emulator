using System.Threading;

namespace Sts2Emulator.Core;

/// <summary>
/// Fail closed when legacy/unmigrated randomness is encountered in an opt-in
/// controlled player action. Ambient state is scoped to the synchronous
/// transition and isolated by async execution context (including workers).
/// </summary>
public static class ControlledChanceGuardV111
{
    private sealed record NativeBoundaryAuthorization(
        string SiteId, string StreamId, string OperationKind);

    private static readonly Dictionary<string, string> EngineOwnedRewardSites = new(
        StringComparer.Ordinal)
    {
        ["reward.card_rarity.ticket"] = "bounded_integer",
        ["reward.card_model.index_draw"] = "choose_index",
        // Elite postcombat reward is engine-owned just like card/potion
        // reward offers. It never contributes to an active combat outcome.
        ["reward.elite_relic.model"] = "choose_index",
        ["reward.random_potion.drop.ticket"] = "bounded_integer",
        ["reward.random_potion.model"] = "choose_index",
        // After a boss is defeated, the source engine initializes the
        // reward offer before publishing the terminal-combat boundary.
        // Delegate only this postcombat reward shuffle to the engine;
        // never leak/control its outcomes through combat Nature.
        [StochasticSiteRegistryV111.RewardBossRelicOfferShuffleSiteId] =
            "bounded_integer",
        [PrototypeGameEngine.PaelsToothCardReturnSiteId] = "choose_index"
    };
    private static readonly AsyncLocal<int> Depth = new();
    private static readonly AsyncLocal<int> EngineOwnedRewardDepth = new();
    private static readonly AsyncLocal<int> EngineOwnedDeathSummonHpDepth = new();
    private static readonly AsyncLocal<int> EngineOwnedNativeDepth = new();
    private static readonly AsyncLocal<NativeBoundaryAuthorization?>
        NativeBoundary = new();
    private static readonly IDisposable NoopScope = new NoopExitScope();
    public static bool Active => Depth.Value > 0;
    internal static bool EngineOwnedNativeProviderActive =>
        EngineOwnedNativeDepth.Value > 0;
    internal static bool IsEngineOwnedPrototypeRewardRequest(
        string siteId, string streamId, string operationKind) =>
        Active && EngineOwnedRewardDepth.Value > 0 && streamId == "reward" &&
        EngineOwnedRewardSites.TryGetValue(siteId, out var expectedOperation) &&
        operationKind == expectedOperation;

    // Legacy-profile death summons have a finite source HP pool. During
    // H9 combat, this one draw is assigned to the engine's own recorded
    // combat stream; it is not a draw-card or enemy-intent Nature decision.
    // The scope is entered only by the death-summon HP resolver.
    internal static bool IsEngineOwnedDeathSummonHpRequest(
        string siteId, string streamId, string operationKind) =>
        Active && EngineOwnedDeathSummonHpDepth.Value > 0 &&
        siteId == NativeCombatRngEntryV111.InitialUniqueHpSiteId &&
        streamId == "combat" && operationKind == "choose_index";

    internal static IDisposable AllowEngineOwnedDeathSummonHp()
    {
        if (!Active)
            throw new InvalidOperationException(
                "Death summon HP delegation requires a guarded combat action.");
        EngineOwnedDeathSummonHpDepth.Value++;
        return new DeathSummonHpExitScope();
    }

    private sealed class DeathSummonHpExitScope : IDisposable
    {
        private bool _exited;
        public void Dispose()
        {
            if (_exited) return;
            _exited = true;
            EngineOwnedDeathSummonHpDepth.Value--;
        }
    }

    /// <summary>
    /// An explicit, narrowly scoped engine-owned reward fallback. It permits
    /// the prototype reward stream and the exact allowlisted legacy reward
    /// sites used by a victory tail or terminal reward after a legal action.
    /// It does not authorize other reward sites or combat/targeting/deck/monster RNG.
    /// </summary>
    public static IDisposable AllowEngineOwnedPrototypeReward()
    {
        if (!Active)
            throw new InvalidOperationException(
                "Engine-owned reward delegation requires a guarded semantic action.");
        EngineOwnedRewardDepth.Value++;
        return new RewardExitScope();
    }

    /// <summary>
    /// Allow the versioned native-v111 provider while a guarded comparator
    /// action runs. Prototype RNG remains rejected, so a chance site that has
    /// not migrated to the native provider cannot silently contaminate the
    /// native-policy baseline.
    /// </summary>
    public static IDisposable AllowEngineOwnedNativeProvider()
    {
        if (!Active)
            throw new InvalidOperationException(
                "Native provider delegation requires a guarded comparator action.");
        EngineOwnedNativeDepth.Value++;
        return new NativeExitScope();
    }

    /// <summary>
    /// Authorize one typed, finite native boundary while the guarded native
    /// control provider is active. The authorization covers the sampler's
    /// internal primitive draws on its named stream and is removed when the
    /// typed request returns.
    /// </summary>
    internal static IDisposable AuthorizeEngineOwnedNativeBoundary(
        string siteId, string streamId, string operationKind)
    {
        if (!Active || EngineOwnedNativeDepth.Value <= 0)
            return NoopScope;

        if (!IsEngineOwnedNativeStream(streamId) ||
            !StochasticSiteRegistryV111.SupportsEngineOwnedNativeBoundary(
                siteId, streamId, operationKind))
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE site=" + siteId +
                " stream=" + streamId + " operation=" + operationKind +
                " : native provider boundary is not registered.");

        var previous = NativeBoundary.Value;
        var authorization = new NativeBoundaryAuthorization(
            siteId, streamId, operationKind);
        NativeBoundary.Value = authorization;
        return new NativeBoundaryExitScope(authorization, previous);
    }

    private sealed class RewardExitScope : IDisposable
    {
        private bool _exited;
        public void Dispose()
        {
            if (_exited) return;
            _exited = true;
            EngineOwnedRewardDepth.Value--;
        }
    }

    public static IDisposable Enter()
    {
        Depth.Value++;
        return new ExitScope();
    }

    public static void RequireMigrated(string site, string operation)
    {
        if (Active && EngineOwnedNativeDepth.Value > 0 &&
            operation == "next_uint64" &&
            NativeBoundary.Value is { } authorization &&
            site == "native_rng." + authorization.StreamId)
            return;
        if (Active && !(EngineOwnedRewardDepth.Value > 0 &&
                site == "prototype_rng.reward" && operation == "next_int"))
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE site=" + site +
                " operation=" + operation +
                " : gameplay RNG site lacks resumable Nature ownership.");
    }

    private static bool IsEngineOwnedNativeStream(string streamId) =>
        streamId is "run.monster_ai" or
            "run.shuffle" or
            "run.combat_energy_costs" or
            "run.combat_targets" or
            "run.combat_card_selection" or
            "run.combat_card_generation" or
            "run.combat_potion_generation" or
            "run.niche" or
            "run.treasure_room_relics" or
            NativePlayerRngV111.RewardsStreamId;

    private sealed class NativeBoundaryExitScope(
        NativeBoundaryAuthorization authorization,
        NativeBoundaryAuthorization? previous) : IDisposable
    {
        private bool _exited;

        public void Dispose()
        {
            if (_exited)
                return;
            if (!ReferenceEquals(NativeBoundary.Value, authorization))
                throw new InvalidOperationException(
                    "Native boundary authorizations must be disposed in stack order.");
            NativeBoundary.Value = previous;
            _exited = true;
        }
    }

    private sealed class NoopExitScope : IDisposable
    {
        public void Dispose() { }
    }

    private sealed class NativeExitScope : IDisposable
    {
        private bool _exited;
        public void Dispose()
        {
            if (_exited) return;
            _exited = true;
            EngineOwnedNativeDepth.Value--;
        }
    }

    private sealed class ExitScope : IDisposable
    {
        private bool _exited;
        public void Dispose()
        {
            if (_exited)
                return;
            _exited = true;
            Depth.Value--;
        }
    }
}
