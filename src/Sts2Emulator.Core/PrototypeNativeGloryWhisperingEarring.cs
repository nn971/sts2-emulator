namespace Sts2Emulator.Core;

/// <summary>
/// Source-backed first-turn autoplay for Glory's Vakuu Whispering Earring.
/// The relic pays each card's current cost, then uses the normal autoplay
/// pipeline so effects, counters, and suspended choices remain shared.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private static RunState ResolveWhisperingEarringAutoPlay(RunState state)
    {
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException(
                "Whispering Earring requires active combat.");
        if (combat.Turn != 1 || !combat.IsPlayerTurn
            || AllEnemiesDefeated(combat)
            || !state.Player.ActiveRelics.Any(relic =>
                relic.RelicId == NativeGloryAncientsV111
                    .WhisperingEarringId))
        {
            return state;
        }

        var result = ResolveOperations(
            state.Player,
            combat,
            new Queue<PrototypeQueuedOperation>([
                new(PrototypeCombatEffectKind
                    .AutoPlayPlayableCardsFromHand,
                    NativeGloryAncientsV111.WhisperingEarringMaxCards,
                    SourceKind: PrototypeEffectSourceKind.Relic)
            ]),
            state.Stochastic.PrototypeRngMutable,
            moveSourceCardOnCompletion: false);

        return state with
        {
            Player = result.Player,
            World = world with { Combat = result.Combat }
        };
    }
}
